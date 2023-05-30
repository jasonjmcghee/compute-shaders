#[compute]
#version 460

struct Params {
    int width;
    int height;
    float delta;
    float evaporateSpeed;
    float diffuseSpeed;
};

// Invocations in the (x, y, z) dimension
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) readonly buffer ParamsBuffer
{
    Params params;
}
params_buffer;

layout(set = 0, binding = 1, std430) buffer trailMap_in
{
    uint data[];
}
trailMap_in_buffer;

layout(set = 0, binding = 2, std430) buffer trailMap_out
{
    uint data[];
}
trailMap_out_buffer;

vec4 parseCombinedColor(uint combinedColor) {
    uint bitMask = 255u;
    float alpha = float((combinedColor >> 24u) & bitMask) / 255.0;
    float blue = float((combinedColor >> 16u) & bitMask) / 255.0;
    float green = float((combinedColor >> 8u) & bitMask) / 255.0;
    float red = float(combinedColor & bitMask) / 255.0;

    return vec4(red, green, blue, alpha);
}


uint combineColorComponents(vec4 color) {
    uint combinedColor = uint(color.a * 255.0) << 24u |
    uint(color.b * 255.0) << 16u |
    uint(color.g * 255.0) << 8u |
    uint(color.r * 255.0);
    return combinedColor;
}

void main() {
    uint id = gl_GlobalInvocationID.x;

    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    float delta = params_buffer.params.delta;
    float evaporateSpeed = params_buffer.params.evaporateSpeed;
    float diffuseSpeed = params_buffer.params.diffuseSpeed;

    if (id >= width * height) {
        return;
    }

    int x = int(id % width);
    int y = int(id / width);

    vec4 sum = vec4(0.0, 0.0, 0.0, 1.0);
    // 3x3 blur
    for (int offsetX = -1; offsetX <= 1; offsetX++) {
        for (int offsetY = -1; offsetY <= 1; offsetY++) {
            int sampleX = min(width - 1, max(0, x + offsetX));
            int sampleY = min(height - 1, max(0, y + offsetY));

            uint cell = sampleY * width + sampleX;
            sum += parseCombinedColor(trailMap_in_buffer.data[cell]);
        }
    }

    vec4 blurredCol = sum / 9.0;
    float diffuseWeight = clamp(diffuseSpeed * delta, 0.0, 1.0);

    vec4 original = parseCombinedColor(trailMap_in_buffer.data[id]);
    blurredCol = mix(original, blurredCol, diffuseWeight) - evaporateSpeed * delta;

    vec4 newVal = vec4(max(0, blurredCol.r), max(0, blurredCol.g), max(0, blurredCol.b), 1.0);
    
    trailMap_out_buffer.data[id] = combineColorComponents(newVal);
}
