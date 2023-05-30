#[compute]
#version 460

struct Params {
    int width;
    int height;
    float delta;
    float evaporateSpeed;
    float diffuseSpeed;
    float mouseX;
    float mouseY;
    bool mouseLeft;
    bool mouseRight;
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
    int mouseX = int(params_buffer.params.mouseX);
    int mouseY = int(params_buffer.params.mouseY);
    bool mouseLeft = params_buffer.params.mouseLeft;
    bool mouseRight = params_buffer.params.mouseRight;

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

    float distance = length(vec2(x - mouseX, y - mouseY));
    float radius = 96;
    float border = 8;

    if (mouseLeft || mouseRight) {
        if (distance < radius) {
            if (mouseLeft) {
                blurredCol.g = 0;
            }

            if (mouseRight) {
                blurredCol.r = 0;
            }
        } else if (distance > radius + border && distance <= radius + 2 * border) {
            if (mouseLeft) {
                blurredCol.r = 0;
                blurredCol.g = 1;
                blurredCol.b = 0.5;
                blurredCol.a = 0.5;
            }

            if (mouseRight) {
                blurredCol.r = 1;
                blurredCol.g = 0;
                blurredCol.b = 0.5;
                blurredCol.a = 0.5;
            }
        }
    }


    vec4 newVal = vec4(max(0, blurredCol.r), max(0, blurredCol.g), max(0, blurredCol.b), 1.0);

    trailMap_out_buffer.data[id] = combineColorComponents(newVal);
}
