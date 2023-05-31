using Godot;
using System;
using ComputeShader;

public partial class Slime : Node2D {
    [Export] public float baseSpeed = 2.0f;

    private static int width = 1920;
    private static int height = 1080;
    private Sprite2D displaySprite;

    // This is our trail data - the data we keep around between frames.
    private byte[] trailMapReadData;
    
    // This is our texture - it contains the above data as an image.
    private ImageTexture trailMapDisplayTexture;
    
    // How much time has passed
    private double time;
    
    // Managers for our pipeline, agents, and trails
    private ComputeManager computeManager;
    private AgentManager agentManager;
    private DiffusionManager diffusionManager;

    // An easy way to reference bits of our pipeline
    // Effectively addresses to memory that we can read from and write to
    private enum Buffers {
        Agents,
        AgentParams,
        TrailMapReadData,
        TrailMapWriteData,
        DiffuseParams,
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        // Initialize our managers
        agentManager = new AgentManager(width, height, baseSpeed);
        diffusionManager = new DiffusionManager(width, height, baseSpeed);
        computeManager = new ComputeManager();

        // Prepare our texture that represents the world
        var trailMapImage = Image.Create(width, height, false, Image.Format.Rgba8);
        trailMapDisplayTexture = ImageTexture.CreateFromImage(trailMapImage);
        trailMapReadData = trailMapImage.GetData();
        
        // Main Pipeline
        computeManager
            .AddPipeline("res://slime.glsl", agentManager.settings.numAgents, 1, 1)
            .StoreAndAddStep((int) Buffers.Agents, agentManager.InitializeAgents())
            .StoreAndAddStep((int) Buffers.AgentParams, agentManager.settings)
            // Double buffer, so we can scan and update without mutation to the current state
            .StoreAndAddStep((int) Buffers.TrailMapReadData, trailMapReadData)
            .StoreAndAddStep((int) Buffers.TrailMapWriteData, trailMapReadData)
            .Build();

        // Diffuse Pipeline
        computeManager
            .AddPipeline("res://slime_diffuse.glsl", (uint) (width * height), 1, 1)
            .StoreAndAddStep((int) Buffers.DiffuseParams, diffusionManager.settings)
            // Double buffer, so we can scan and update without mutation to the current state
            // Note that it reuses buffers from the previous, but swaps them.
            // This allows us to not allocate memory for new ones.
            .AddStep((int) Buffers.TrailMapWriteData)
            .AddStep((int) Buffers.TrailMapReadData)
            .Build();

        // This is what we use to display the world
        var displayShaderMaterial = new ShaderMaterial {Shader = GD.Load<Shader>("res://DisplayShader.gdshader")};
        
        // Here we provide access to a sample2D that represents our world texture
        displayShaderMaterial.SetShaderParameter("trailMap", trailMapDisplayTexture);

        // Create the Sprite node
        displaySprite = new Sprite2D {
            TextureFilter = TextureFilterEnum.Linear,
            // We are using a different texture in case we want to upscale, etc.
            // This could change, without impacting the way the world is represented
            Texture = ImageTexture.CreateFromImage(
                Image.Create(width, height, false, Image.Format.Rgba8)
            ),
            Material = displayShaderMaterial
        };

        // Add it to the world
        AddChild(displaySprite);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
        time += delta;

        // Because of our double buffer + swap + double buffer, we're back to TrailMapWriteData
        computeManager.UpdateBuffer((int) Buffers.TrailMapWriteData, trailMapReadData);

        var mousePos = GetViewport().GetMousePosition();

        // Update the settings based on current state of input, time, etc.
        computeManager.UpdateBuffer(
            (int) Buffers.AgentParams,
            agentManager.BuildSettings(width, height, baseSpeed) with {
                delta = (float) delta, time = (float) time,
                mouseLeft = Input.IsMouseButtonPressed(MouseButton.Left) && Input.IsKeyPressed(Key.Space),
                mouseRight = Input.IsMouseButtonPressed(MouseButton.Right) && Input.IsKeyPressed(Key.Space),
                mouseX = mousePos.X, mouseY = mousePos.Y,
            }
        );

        // Update the settings based on current state of input, time, etc.
        computeManager.UpdateBuffer(
            (int) Buffers.DiffuseParams, 
            diffusionManager.BuildSettings(width, height, baseSpeed) with {
                delta = (float) delta,
                mouseLeft = Input.IsMouseButtonPressed(MouseButton.Left) && !Input.IsKeyPressed(Key.Space),
                mouseRight = Input.IsMouseButtonPressed(MouseButton.Right) && !Input.IsKeyPressed(Key.Space),
                mouseX = mousePos.X, mouseY = mousePos.Y,
                evaporateSpeed = 0.1f * baseSpeed * baseSpeed,
                diffuseSpeed = 3f * baseSpeed * baseSpeed,
            }
        );

        // Execute the pipeline! The is where all the compute-shader calculations happen.
        computeManager.Execute();

        // Get back the data from the last-written-to buffer, which is `TrailMapReadData`
        trailMapReadData = computeManager.GetDataFromBuffer((int) Buffers.TrailMapReadData);

        // Update the texture with the latest data.
        trailMapDisplayTexture.Update(
            Image.CreateFromData(width, height, false, Image.Format.Rgba8, trailMapReadData)
        );
    }

    public override void _ExitTree() {
        computeManager.CleanUp();
        base._ExitTree();
    }
}