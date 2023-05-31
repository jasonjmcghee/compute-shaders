#[compute]
#version 460

struct Organism {
    float x;
    float y;
    float velX;
    float velY;
    uint type;
};

struct Params {
    uint numOrganisms;
    int width;
    int height;
    float delta;
    float moveSpeed;
    int sensorSize;
    int spawnType;
    int numSpecies;
};

// Invocations in the (x, y, z) dimension
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// Structured buffers
layout(set = 0, binding = 0, std430) restrict buffer OrganismsBuffer {
    Organism organisms[];
}
organisms_buffer;

layout(set = 0, binding = 1, std430) readonly buffer ParamsBuffer
{
    Params params;
}
params_buffer;

layout(set = 0, binding = 2, std430) readonly buffer AttrationMatrix
{
    float data[];
}
attrationMatrix_buffer;

layout(set = 0, binding = 3, std430) buffer organismMap_in {
    uint data[];
}
organismMap_in_buffer;

layout(set = 0, binding = 4, std430) buffer organismMap_out {
    uint data[];
}
organismMap_out_buffer;

uint hash(uint state) {
    state ^= 2747636419u;
    state *= 2654435769u;
    state ^= state >> 16;
    state *= 2654435769u;
    state ^= state >> 16;
    state *= 2654435769u;
    return state;
}

float scaleToRange01(uint state) {
    return min(1, max(0, float(state) / 4294967295.0));
}

vec4 parseCombinedColor(uint combinedColor) {
    uint bitMask = 255u;
    float alpha = float((combinedColor >> 24u) & bitMask) / 255.0;
    float blue = float((combinedColor >> 16u) & bitMask) / 255.0;
    float green = float((combinedColor >> 8u) & bitMask) / 255.0;
    float red = float(combinedColor & bitMask) / 255.0;

    return vec4(red, green, blue, alpha);
}

float force(float r, float attraction) {
    float beta = 0.7;
    if (r < beta) {
        return r / beta - 1;
    } else if (beta < r && r < 1) {
        return attraction * (1 - abs(2 * r - 1 - beta) / (1 - beta));
    }
    return 0;
}

float attract(int x, int y) {
    float numSpecies = params_buffer.params.numSpecies;
    return attrationMatrix_buffer.data[y * uint(numSpecies) + x];
}

vec2 sense(Organism organism) {
    uint id = gl_GlobalInvocationID.x;

    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    int sensorSize = params_buffer.params.sensorSize;

    vec2 pos = vec2(organism.x, organism.y);

    vec2 totalForce = vec2(0.0);

    //    ivec4 senseWeight = agent.speciesMask * 2 - 1;

    for (int offsetY = -sensorSize; offsetY <= sensorSize; offsetY++) {
        int sampleY = int(mod(pos.y + offsetY, height));
        uint row = sampleY * width;

        for (int offsetX = -sensorSize; offsetX <= sensorSize; offsetX++) {
            float r = sqrt(float(offsetX * offsetX + offsetY * offsetY));

            // Continue to next iteration if the sample is outside of the circle.
            if (r == 0 || r > float(sensorSize)) continue;
            
            int sampleX = int(mod(pos.x + offsetX, width));
            uint cell = row + sampleX;

            uint color = organismMap_in_buffer.data[cell];
            
            totalForce += vec2(offsetX, offsetY) / r * force(r / float(sensorSize), attract(int(organism.type), int(color)));
        }
    }

    return totalForce * sensorSize * 5.0;
}

void main() {
    uint numOrganisms = params_buffer.params.numOrganisms;
    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    float delta = params_buffer.params.delta;
    float moveSpeed = params_buffer.params.moveSpeed;
    
    uint id = gl_GlobalInvocationID.x;
    
    if (id >= numOrganisms) {
        return;
    }
    
    Organism organism = organisms_buffer.organisms[id];
    
    vec2 pos = vec2(organism.x, organism.y);

    uint cell = uint(pos.y) * width + uint(pos.x);
    
    vec2 vel = vec2(organism.velX, organism.velY);
    
//    uint random = hash(uint(pos.y * width + pos.x * height) + hash(id + uint(time * 100000.0)));

    vec2 newVel = (vel * pow(0.5, delta / 0.04)) + sense(organism) * delta;
    vec2 newPos = pos + newVel * delta;
    
    organisms_buffer.organisms[id].x = newPos.x;
    organisms_buffer.organisms[id].y = newPos.y;
    organisms_buffer.organisms[id].velX = newVel.x;
    organisms_buffer.organisms[id].velY = newVel.y;
    organisms_buffer.organisms[id].type = organism.type;

    cell = uint(newPos.y) * width + uint(newPos.x);
    organismMap_out_buffer.data[cell] = organism.type;
}