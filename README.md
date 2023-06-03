# Experiments with Compute Shaders

## `ComputeManager`

The abstraction layer I built to dramatically simplify the complexity of compute shaders, from my perspective.

I can only keep so much complexity in my head, and this approach simplifies so much.

- (Almost) No thinking about how to turn data in C# into data GLSL will understand - essentially, only use `struct` with
  primitive fields, or lists of them.
- Write any number of pipelines which will be sequentially executed when you call `execute()` on the `ComputeManager`
- You can signify buffers with ids, for example, from an `enum` to simplify your code and not deal with `Rid`.
- Easily read from, update, or clear buffers using the above ids pattern.

### Notes

- Currently only supports `StructuredBuffer`, but could easily be extended to others with the same pattern.
- Currently does not support using a portion of a buffer (all or nothing), but again, easy to modify this.
- You can always dip down to the lower layer of abstraction (Godot layer).

### Example

Writing pipelines using this pattern is easy.

This is a slightly simplified version of what is in `Slime.cs`.

It's a multi-pipeline / multi-shader compute pipeline and easily fits on a screen.

```csharp
public override void _Ready() {
    computeManager = new ComputeManager();

    // Prepare our texture that represents the world
    var trailMapImage = Image.Create(width, height, false, Image.Format.Rgba8);
    trailMapDisplayTexture = ImageTexture.CreateFromImage(trailMapImage);
    trailMapReadData = trailMapImage.GetData();
    
    // Main Pipeline
    computeManager
        .AddPipeline("res://Slime/slime.glsl", agentManager.settings.numAgents, 1, 1)
        .StoreAndAddStep((int) Buffers.Agents, agentManager.InitializeAgents())
        .StoreAndAddStep((int) Buffers.AgentParams, agentManager.settings)
        .StoreAndAddStep((int) Buffers.TrailMapReadData, trailMapReadData)
        .StoreAndAddStep((int) Buffers.TrailMapWriteData, trailMapReadData)
        .Build();

    // Diffuse Pipeline
    computeManager
        .AddPipeline("res://Slime/slime_diffuse.glsl", (uint) (width * height), 1, 1)
        .StoreAndAddStep((int) Buffers.DiffuseParams, diffusionManager.settings)
        .AddStep((int) Buffers.TrailMapWriteData)
        .AddStep((int) Buffers.TrailMapReadData)
        .Build();
    
    displayTexture = GetNode<Sprite>("Sprite").Texture as ImageTexture;
}

public override void _Process(double delta) {
    time += delta;

    // Because of our double buffer + swap + double buffer, we're back to TrailMapWriteData
    computeManager.UpdateBuffer((int) Buffers.TrailMapWriteData, trailMapReadData);

    computeManager.UpdateBuffer((int) Buffers.AgentParams, agentManager.settings with {
        delta = (float) delta, time = (float) time,
    });

    computeManager.UpdateBuffer((int) Buffers.DiffuseParams, diffusionManager.settings with {
        delta = (float) delta
    });

    // Execute the pipeline
    computeManager.Execute();

    trailMapReadData = computeManager.GetDataFromBuffer((int) Buffers.TrailMapReadData);

    // Update the texture
    displayTexture.Update(
        Image.CreateFromData(width, height, false, Image.Format.Rgba8, trailMapReadData)
    );
}
```

## Slime

Heavily used: [Coding Adventure: Ant and Slime Simulations](https://www.youtube.com/watch?v=X-iSQQgOd1A) by Sebastian
Lague
for learning / foundation.

Updates:

- [x] Added mouse behavior to allow drawing slime

![image](https://github.com/jasonjmcghee/compute-shaders/assets/1522149/8c6a750a-2d55-4b14-b755-c4b3af2cc2ae)

## Life

_In Progress_