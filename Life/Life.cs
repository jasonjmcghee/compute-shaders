using System;
using System.Linq;
using Godot;
using ComputeShader.Compute;
using ComputeShader.Life;

public partial class Life : Node2D {
    [Export] public float baseSpeed = 2.0f;

    [Export] public float[] attractionMatrix = {
        1f, 0.01f, -0.02f, 0.08f, // First column
        1f, -1f, 1f, 1.0f, // Second column
        -0.02f, -0.2f, 0.2f, 0.01f, // Third column
        0.05f, 0.05f, 0.2f, -0.1f // Fourth column
    };

    private static int width = 200;
    private static int height = 200;

    // How much time has passed
    private double time;

    // Managers for our pipeline and organisms
    private ComputeManager computeManager;
    private OrganismManager organismManager;
    private int frame;
    private int organismMapDataLength;
    private MultiMesh multiMesh;
    private int multiMeshBufferLength;
    private Camera2D camera;
    private byte[] organismMapData;

    // An easy way to reference bits of our pipeline
    // Effectively addresses to memory that we can read from and write to
    private enum Buffers {
        Organisms,
        OrganismParams,
        OrganismAttraction,
        OrganismPositions,
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        camera = GetNode<Camera2D>("Camera2D");

        // Initialize our managers
        organismManager =
            new OrganismManager(10000, (int) Math.Sqrt(attractionMatrix.Length), width, height, baseSpeed);
        var organisms = organismManager.Initialize();

        computeManager = new ComputeManager();

        // Create the MultiMesh
        multiMesh = new MultiMesh {
            UseColors = true,
            InstanceCount = organisms.Length,
        };

        // Set the mesh and texture
        multiMesh.Mesh = new QuadMesh();

        Color[] colors = new Color[] {
            Colors.Red,
            Colors.Green,
            Colors.Purple,
            Colors.Blue,
            Colors.White,
            Colors.Pink,
        };

        var organismPositions = organisms.Select((o) => o.OrganismPosition).ToArray();
        var organismInfos = organisms.Select((o) => o.OrganismInfo).ToArray();

        // Set the transform for each instance
        for (int i = 0; i < organisms.Length; i++) {
            var organism = organisms[i];
            multiMesh.SetInstanceTransform2D(
                i, new Transform2D(0, new Vector2(organism.OrganismPosition.X, organism.OrganismPosition.Y))
            );

            multiMesh.SetInstanceColor(i, colors[organism.OrganismInfo.type % colors.Length]);
        }

        // Create a MultiMeshInstance2D and add it to the scene
        MultiMeshInstance2D multiMeshInstance2D = new MultiMeshInstance2D {
            Multimesh = multiMesh,
            Position = new Vector2(-width / 2, -height / 2),
            Material = GD.Load<ShaderMaterial>("res://Life/Circle.tres"),
        };
        AddChild(multiMeshInstance2D);

        var multiMeshBuffer = multiMesh.Transform2DArray;
        multiMeshBufferLength = multiMeshBuffer.Length * sizeof(float);

        // Main Pipeline
        computeManager
            .AddPipeline("res://Life/life.glsl", organismManager.settings.numOrganisms, 1, 1)
            .StoreAndAddStep((int) Buffers.Organisms, organismInfos)
            .StoreAndAddStep((int) Buffers.OrganismPositions, organismPositions)
            .StoreAndAddStep((int) Buffers.OrganismParams, organismManager.settings)
            .StoreAndAddStep((int) Buffers.OrganismAttraction, attractionMatrix)
            .Build();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
        if (Input.IsKeyPressed(Key.Bracketleft)) {
            camera.Zoom = new Vector2(Mathf.Pow(camera.Zoom.X, 0.99f), Mathf.Pow(camera.Zoom.Y, 0.99f));
        }

        if (Input.IsKeyPressed(Key.Bracketright)) {
            camera.Zoom = new Vector2(Mathf.Pow(camera.Zoom.X, 1.01f), Mathf.Pow(camera.Zoom.Y, 1.01f));
        }

        time += delta;

        // Update the settings based on current state of input, time, etc.
        computeManager.UpdateBuffer(
            (int) Buffers.OrganismParams,
            organismManager.BuildSettings(width, height, baseSpeed) with {
                delta = (float) delta,
                frameNum = computeManager.Frame
            }
        );

        computeManager.UpdateBuffer((int) Buffers.OrganismAttraction, attractionMatrix);

        // Execute the pipeline! The is where all the compute-shader calculations happen.
        computeManager.Execute();

        if (computeManager.JustSynced) {
            OrganismPosition[] positions =
                computeManager.GetDataFromBufferAsArray<OrganismPosition>((int) Buffers.OrganismPositions);
            Vector2[] transforms = new Vector2[positions.Length * 3];

            for (int i = 0; i < positions.Length; i++) {
                var pos = positions[i];
                var transform = new Transform2D(0, new Vector2(pos.X, pos.Y));
                transforms[3 * i] = transform.X;
                transforms[3 * i + 1] = transform.Y;
                transforms[3 * i + 2] = transform.Origin;
            }

            multiMesh.Transform2DArray = transforms;
            // multiMesh.Transform2DArray = computeManager.GetDataFromBufferAsArray<Vector2>((int) Buffers.OrganismPositions);
        }
    }

    public override void _ExitTree() {
        computeManager.CleanUp();
        base._ExitTree();
    }
}