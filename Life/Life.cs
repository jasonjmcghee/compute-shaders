using System;
using System.Runtime.InteropServices;
using Godot;
using ComputeShader.Compute;
using ComputeShader.Life;

public partial class Life : Node2D {
    [Export] public float baseSpeed = 2.0f;
    [Export] public float[] attractionMatrix = {
        -0.8f, 0.1f, 0.2f, 0.2f, // First column
        -0.2f, -0.25f, 0.1f, 0.0f, // Second column
        -0.2f, -1.0f, 1.0f, 0.1f, // Third column
        0.2f, 0.1f, 0.0f, 1.0f  // Fourth column
    };

    private static int width = 1280;
    private static int height = 720;
    private Sprite2D displaySprite;

    // This is our organism data - the data we keep around between frames.
    private byte[] organismMapData;
    
    // This is our texture - it contains the above data as an image.
    private ImageTexture organismMapDisplayTexture;
    
    // How much time has passed
    private double time;
    
    // Managers for our pipeline and organisms
    private ComputeManager computeManager;
    private OrganismManager organismManager;

    // An easy way to reference bits of our pipeline
    // Effectively addresses to memory that we can read from and write to
    private enum Buffers {
        Organisms,
        OrganismParams,
        OrganismMapReadData,
        OrganismMapWriteData,
        OrganismAttraction
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        // Initialize our managers
        organismManager = new OrganismManager(width, height, baseSpeed);
        computeManager = new ComputeManager();

        // Prepare our texture that represents the world
        var trailMapImage = Image.Create(width, height, false, Image.Format.Rgba8);
        organismMapDisplayTexture = ImageTexture.CreateFromImage(trailMapImage);
        organismMapData = trailMapImage.GetData();
        
        // Main Pipeline
        computeManager
            .AddPipeline("res://Life/life.glsl", organismManager.settings.numOrganisms, 1, 1)
            .StoreAndAddStep((int) Buffers.Organisms, organismManager.Initialize())
            .StoreAndAddStep((int) Buffers.OrganismParams, organismManager.settings)
            .StoreAndAddStep((int) Buffers.OrganismAttraction, attractionMatrix)
            // Double buffer, so we can scan and update without mutation to the current state
            .StoreAndAddStep((int) Buffers.OrganismMapReadData, organismMapData)
            .StoreAndAddStep((int) Buffers.OrganismMapWriteData, organismMapData)
            .Build();

        // This is what we use to display the world
        var displayShaderMaterial = new ShaderMaterial {Shader = GD.Load<Shader>("res://Life/DisplayLife.gdshader")};
        
        // Here we provide access to a sample2D that represents our world texture
        displayShaderMaterial.SetShaderParameter("organismMap", organismMapDisplayTexture);
        displayShaderMaterial.SetShaderParameter("size", new Vector2(width, height));

        var size = GetViewportRect().Size;
        // Create the Sprite node
        displaySprite = new Sprite2D {
            TextureFilter = TextureFilterEnum.Linear,
            // We are using a different texture in case we want to upscale, etc.
            // This could change, without impacting the way the world is represented
            Texture = ImageTexture.CreateFromImage(
                Image.Create((int) width, (int) height, false, Image.Format.Rgba8)
            ),
            Material = displayShaderMaterial
        };

        // Add it to the world
        AddChild(displaySprite);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
        time += delta;
        
        computeManager.UpdateBuffer((int) Buffers.OrganismMapReadData, organismMapData);
        computeManager.ClearBuffer((int) Buffers.OrganismMapWriteData, organismMapData.Length);

        // Update the settings based on current state of input, time, etc.
        computeManager.UpdateBuffer(
            (int) Buffers.OrganismParams,
            organismManager.BuildSettings(width, height, baseSpeed) with { delta = (float) delta, numSpecies = (int) Math.Sqrt(attractionMatrix.Length)}
        );
        
        computeManager.UpdateBuffer((int) Buffers.OrganismAttraction, attractionMatrix);

        // Execute the pipeline! The is where all the compute-shader calculations happen.
        computeManager.Execute();

        // Get back the data from the last-written-to buffer
        organismMapData = computeManager.GetDataFromBuffer((int) Buffers.OrganismMapWriteData);

        // Update the texture with the latest data.
        organismMapDisplayTexture.Update(
            Image.CreateFromData(width, height, false, Image.Format.Rgba8, organismMapData)
        );
    }

    public override void _ExitTree() {
        computeManager.CleanUp();
        base._ExitTree();
    }
}