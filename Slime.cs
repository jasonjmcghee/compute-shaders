using Godot;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Godot.Collections;

public partial class Slime : Node2D {
    [Export] public static int width = 1920;
    [Export] public static int height = 1080;
    [Export] public Sprite2D displaySprite;
    [Export] public static float baseSpeed = 2.0f;

    private Array<Rid> to_free = new();
    private RenderingDevice rd;

    public enum SpawnMode {
        Random,
        Point,
        InwardCircle,
        RandomCircle,
        Other
    }

    private Rid agentComputePipeline;
    private Rid agentUniformSet;

    private Agent[] agents;
    private byte[] agentData;
    private Rid agentsBuffer;

    private byte[] trailMapReadData;
    private byte[] trailMapWriteData;
    private ImageTexture trailMapDisplayTexture;

    private SpawnMode spawnMode = SpawnMode.Other;
    private Random random = new Random();

    private ImageTexture displayTexture;
    private ShaderMaterial displayShaderMaterial;

    private Params settings = new() {
        numAgents = 300000,
        width = width,
        height = height,
        delta = 0f,
        time = 0f,
        moveSpeed = 20f * baseSpeed,
        sensorSize = 1,
        sensorOffsetDst = 25.0f,
        sensorAngleDegrees = 30.0f,
        turnSpeed = 1f * (baseSpeed * 0.5f),
        trailWeight = 40f,
        maxLifetime = 40,
        mouseX = 0,
        mouseY = 0,
        mouseLeft = false,
        mouseRight = false,
    };

    private DiffuseParams diffuseParams = new() {
        width = width,
        height = height,
        delta = 0f,
        evaporateSpeed = 0.2f * baseSpeed * baseSpeed,
        diffuseSpeed = 3f * baseSpeed * baseSpeed,
    };

    private Rid paramsBuffer;
    private double time;
    private Rid trailMapTexture;
    private Rid trailMapReadBuffer;
    private Rid trailMapWriteBuffer;
    private Rid diffuseUniformSet;
    private Rid diffuseComputePipeline;
    private Rid diffuseParamsBuffer;
    private Rid diffuseTrailMapReadBuffer;
    private Rid diffuseTrailMapWriteBuffer;

    public float RandomAngle() {
        // Generate random components on the unit circle
        double x = random.NextDouble() * 2 - 1;
        double y = random.NextDouble() * 2 - 1;

        // Calculate the angle in radians
        double angle = Math.Atan2(y, x);

        return (float) angle;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        rd = RenderingServer.CreateLocalRenderingDevice();

        var compute = GD.Load<RDShaderFile>("res://slime.glsl");
        var agentShader = rd.ShaderCreateFromSpirV(compute.GetSpirV());
        to_free.Add(agentShader);

        var diffuse = GD.Load<RDShaderFile>("res://slime_diffuse.glsl");
        var diffuseShader = rd.ShaderCreateFromSpirV(diffuse.GetSpirV());
        to_free.Add(diffuseShader);

        InitializeAgents();

        // Convert Agents to buffer
        var size = Marshal.SizeOf(typeof(Agent));

        agentData = new byte[agents.Length * size];
        Buffer.BlockCopy(agents.SelectMany(ConvertStructToBytes).ToArray(), 0, agentData, 0, agentData.Length);

        var trailMapImage = Image.Create(settings.width, settings.height, false, Image.Format.Rgba8);
        trailMapDisplayTexture = ImageTexture.CreateFromImage(trailMapImage);
        trailMapReadData = trailMapImage.GetData();
        trailMapWriteData = trailMapImage.GetData();

        var uniformArr = new Array<RDUniform>();

        agentsBuffer = CreateStorageBufferOnRd(agentData, uniformArr); // Binding 0
        paramsBuffer = CreateStorageBufferOnRd(ConvertStructToBytes(settings), uniformArr); // Binding 1
        trailMapReadBuffer = CreateStorageBufferOnRd(trailMapReadData, uniformArr); // Binding 2
        trailMapWriteBuffer = CreateStorageBufferOnRd(trailMapWriteData, uniformArr); // Binding 3

        agentUniformSet = rd.UniformSetCreate(uniformArr, agentShader, 0);
        agentComputePipeline = rd.ComputePipelineCreate(agentShader);

        to_free.Add(agentUniformSet);
        to_free.Add(agentComputePipeline);

        var uniformArr2 = new Array<RDUniform>();

        diffuseParamsBuffer = CreateStorageBufferOnRd(ConvertStructToBytes(diffuseParams), uniformArr2); // Binding 0
        diffuseTrailMapReadBuffer = CreateStorageBufferOnRdWithRid(trailMapWriteBuffer, uniformArr2); // Binding 1
        diffuseTrailMapWriteBuffer = CreateStorageBufferOnRdWithRid(trailMapReadBuffer, uniformArr2); // Binding 2

        diffuseUniformSet = rd.UniformSetCreate(uniformArr2, diffuseShader, 0);
        diffuseComputePipeline = rd.ComputePipelineCreate(diffuseShader);

        to_free.Add(diffuseUniformSet);
        to_free.Add(diffuseComputePipeline);

        displayShaderMaterial = new ShaderMaterial {Shader = GD.Load<Shader>("res://DisplayShader.gdshader")};

        // Create the Sprite node
        displaySprite = new Sprite2D {
            Texture = displayTexture,
            TextureFilter = TextureFilterEnum.Linear,
            Material = displayShaderMaterial
        };

        AddChild(displaySprite);

        var image = Image.Create(settings.width, settings.height, false, Image.Format.Rgba8);
        displaySprite.Texture = ImageTexture.CreateFromImage(image);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
        time += delta;

        // Update the read buffer with the last data
        // rd.BufferUpdate(trailMapReadBuffer, 0, (uint) trailMapReadData.Length, trailMapReadData);
        rd.BufferUpdate(diffuseTrailMapReadBuffer, 0, (uint) trailMapReadData.Length, trailMapReadData);

        var mousePos = GetViewport().GetMousePosition();

        var paramsStruct = ConvertStructToBytes(
            settings with {
                delta = (float) delta, time = (float) time, mouseLeft = Input.IsMouseButtonPressed(MouseButton.Left),
                mouseRight = Input.IsMouseButtonPressed(MouseButton.Right), mouseX = mousePos.X, mouseY = mousePos.Y
            }
        );
        rd.BufferUpdate(paramsBuffer, 0, (uint) paramsStruct.Length, paramsStruct);
        var diffuseParamsStruct = ConvertStructToBytes(
            diffuseParams with {delta = (float) delta}
        );
        rd.BufferUpdate(diffuseParamsBuffer, 0, (uint) diffuseParamsStruct.Length, diffuseParamsStruct);

        var agentComputeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(agentComputeList, agentComputePipeline);
        rd.ComputeListBindUniformSet(agentComputeList, agentUniformSet, 0);
        rd.ComputeListDispatch(agentComputeList, settings.numAgents, 1, 1);
        rd.ComputeListEnd();

        var diffuseComputeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(diffuseComputeList, diffuseComputePipeline);
        rd.ComputeListBindUniformSet(diffuseComputeList, diffuseUniformSet, 0);
        rd.ComputeListDispatch(diffuseComputeList, (uint) (settings.width * settings.height), 1, 1);
        rd.ComputeListEnd();

        rd.Submit();
        rd.Sync();

        // Read back the data from the buffers
        trailMapReadData = rd.BufferGetData(diffuseTrailMapWriteBuffer, 0);

        var trailMapImage =
            Image.CreateFromData(settings.width, settings.height, false, Image.Format.Rgba8, trailMapReadData);
        (displaySprite.Texture as ImageTexture).Update(trailMapImage);
    }

    public Vector2 InsideUnitCircle() {
        double theta = 2.0 * Math.PI * random.NextDouble(); // angle
        double r = Math.Sqrt(random.NextDouble()); // radius

        float x = (float) (r * Math.Cos(theta));
        float y = (float) (r * Math.Sin(theta));

        return new Vector2(x, y);
    }


    private void InitializeAgents() {
        Vector2 center = new Vector2(settings.width / 2f, settings.height / 2f);
        Vector2 left = new Vector2(settings.width / 4f, settings.height / 2f);
        Vector2 right = new Vector2(3f * settings.width / 4f, settings.height / 2f);
        Vector2 startPos = Vector2.Zero;


        // Initialize Agents
        agents = new Agent[settings.numAgents];
        for (int i = 0; i < settings.numAgents; i++) {
            uint type = random.NextDouble() > 0.6 ? 1u : 0;

            float randomAngle = RandomAngle();
            float angle = 0;

            if (spawnMode == SpawnMode.Other) {
                angle = randomAngle;
                if (type == 0) {
                    startPos = center + InsideUnitCircle() * settings.height * 0.15f;
                    angle = Mathf.Atan2((center - startPos).Normalized().Y, (center - startPos).Normalized().X);
                }
                else {
                    if (random.NextDouble() > 0.5) {
                        startPos = left + InsideUnitCircle() * settings.height * 0.15f;
                    }
                    else {
                        startPos = right + InsideUnitCircle() * settings.height * 0.15f;
                    }
                }
            }
            else {
                if (spawnMode == SpawnMode.Point) {
                    startPos = center;
                    angle = randomAngle;
                }
                else if (spawnMode == SpawnMode.Random) {
                    startPos = new Vector2(random.Next(0, settings.width), random.Next(0, settings.height));
                    angle = randomAngle;
                }
                else if (spawnMode == SpawnMode.InwardCircle) {
                    startPos = center + InsideUnitCircle() * settings.height * 0.5f;
                    angle = Mathf.Atan2((center - startPos).Normalized().Y, (center - startPos).Normalized().X);
                }
                else if (spawnMode == SpawnMode.RandomCircle) {
                    startPos = center + InsideUnitCircle() * settings.height * 0.15f;
                    angle = randomAngle;
                }
            }

            agents[i] = new Agent {
                Position = startPos,
                Angle = angle,
                type = type,
                lifetime = settings.maxLifetime
            };
        }
    }

    private Rid CreateStorageBufferOnRd(byte[] bytes, Array<RDUniform> uniformArr) {
        var buffer = rd.StorageBufferCreate((uint) bytes.Length, bytes);
        to_free.Add(buffer);

        return CreateStorageBufferOnRdWithRid(buffer, uniformArr);
    }

    private Rid CreateStorageBufferOnRdWithRid(Rid buffer, Array<RDUniform> uniformArr) {
        var uniform = new RDUniform {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = uniformArr.Count,
            _Ids = new Array<Rid> {buffer}
        };
        uniformArr.Add(uniform);

        return buffer;
    }

    private byte[] ConvertStructToBytes<T>(T param) {
        var size = Marshal.SizeOf(param);
        var arr = new byte[size];

        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(param, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);

        return arr;
    }

    public override void _ExitTree() {
        foreach (Rid rid in to_free) {
            rd.FreeRid(rid);
        }

        rd.Free();

        base._ExitTree();
    }

    private struct Agent {
        public Vector2 Position;
        public float Angle;
        public uint type;
        public float lifetime;
    }

    struct Params {
        public uint numAgents;
        public int width;
        public int height;
        public float delta;
        public float time;
        public float moveSpeed;
        public int sensorSize;
        public float sensorOffsetDst;
        public float sensorAngleDegrees;
        public float turnSpeed;
        public float trailWeight;
        public float maxLifetime;
        public float mouseX;
        public float mouseY;
        public bool mouseLeft;
        public bool mouseRight;
    };

    struct DiffuseParams {
        public int width;
        public int height;
        public float delta;
        public float evaporateSpeed;
        public float diffuseSpeed;
    };
}