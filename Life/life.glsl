﻿#[compute]
#version 460

struct Organism {
    float velX;
    float velY;
    uint type;
};

struct Position {
    float x;
    float y;
};

struct Params {
    uint numOrganisms;
    int width;
    int height;
    float delta;
    int sensorSize;
    int spawnType;
    int numSpecies;
    int frameNum;
};

struct Bin {
    int start;
    int end;
    int left;
    int upLeft;
    int up;
    int upRight;
    int right;
    int downRight;
    int down;
    int downLeft;
};

// Invocations in the (x, y, z) dimension
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// Structured buffers
layout(set = 0, binding = 0, std430) restrict buffer Organisms {
    Organism data[];
}
organisms;

layout(set = 0, binding = 1, std430) restrict buffer OrganismsPositions {
    Position data[];
}
positions;

layout(set = 0, binding = 2, std430) restrict readonly buffer Bins {
    Bin data[];
}
bins;

layout(set = 0, binding = 3, std430) restrict readonly buffer BinMembership {
    int data[];
}
binMembership;

layout(set = 0, binding = 4, std430) restrict readonly buffer FlatBinnedIndices {
    int data[];
}
flatBinnedIndices;

layout(set = 0, binding = 5, std430) restrict readonly buffer ParamsBuffer
{
    Params params;
}
params_buffer;

layout(set = 0, binding = 6, std430) restrict readonly buffer AttrationMatrix
{
    float data[];
}
attrationMatrix_buffer;

float force(float r, float attraction) {
    float beta = 0.3;
    if (r < beta) {
        return r / beta - 1;
    } else if (beta < r && r < 1) {
        return attraction * (1 - abs(2 * r - 1 - beta) / (1 - beta));
    }
    return 0;
}

float attract(uint x, uint y) {
    float numSpecies = params_buffer.params.numSpecies;
    uint row = x * uint(numSpecies);
    return attrationMatrix_buffer.data[row + y];
}

vec2 senseSingle(int i, Organism organism, vec2 pos) {
    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    int sensorSize = params_buffer.params.sensorSize;
    
    Position targetPosition = positions.data[i];
    vec2 targetPos = vec2(targetPosition.x, targetPosition.y);

    float distX = targetPos.x - pos.x;
    float wrappedDistX = distX - sign(distX) * width;
    float distY = targetPos.y - pos.y;
    float wrappedDistY = distY - sign(distY) * height;

    // If it wraps, force sign is flipped
    // If it doesn't, it's not.
    float effectiveDistX = abs(distX) < abs(wrappedDistX) ? distX : wrappedDistX;
    float effectiveDistY = abs(distY) < abs(wrappedDistY) ? distY : wrappedDistY;

    vec2 dist = vec2(effectiveDistX, effectiveDistY);
    float r = sqrt(float(dist.x * dist.x + dist.y * dist.y));

    // Continue to next iteration if the sample is outside of the circle.
    if (r <= 0.0 || r >= float(sensorSize)) return vec2(0);

    vec2 normalizedDist = dist / r;
    return normalizedDist * force(
        r / float(sensorSize),
        attract(organism.type, organisms.data[i].type)
    );
}

vec2 senseBin(int binIndex, Organism organism, vec2 pos) {
    uint id = gl_GlobalInvocationID.x;
    Bin bin = bins.data[binIndex];

    vec2 totalForce = vec2(0.0);
    for (int i = bin.start; i < bin.end; i++) {
        int index = flatBinnedIndices.data[i];
        if (index == id) continue;
        totalForce += senseSingle(index, organism, pos);
    }
    return totalForce;
}

vec2 sense(Organism organism, vec2 pos) {
    uint id = gl_GlobalInvocationID.x;
    uint numOrganisms = params_buffer.params.numOrganisms;
    int sensorSize = params_buffer.params.sensorSize;
    
    int binIndex = binMembership.data[id];
    Bin bin = bins.data[binIndex];

    vec2 totalForce = vec2(0.0);
    
    totalForce += senseBin(binIndex, organism, pos);
    totalForce += senseBin(bin.left, organism, pos);
    totalForce += senseBin(bin.upLeft, organism, pos);
    totalForce += senseBin(bin.up, organism, pos);
    totalForce += senseBin(bin.upRight, organism, pos);
    totalForce += senseBin(bin.right, organism, pos);
    totalForce += senseBin(bin.downRight, organism, pos);
    totalForce += senseBin(bin.down, organism, pos);
    totalForce += senseBin(bin.downLeft, organism, pos);

    return totalForce * sensorSize * 5.0;
}

void main() {
    uint numOrganisms = params_buffer.params.numOrganisms;
    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    float delta = params_buffer.params.delta;

    uint id = gl_GlobalInvocationID.x;

    if (id >= numOrganisms) {
        return;
    }

    Organism organism = organisms.data[id];
    Position position = positions.data[id];

    vec2 pos = vec2(position.x, position.y);
    vec2 vel = vec2(organism.velX, organism.velY);

    vec2 newVel = (vel * pow(0.5, delta / 0.04)) + sense(organism, pos) * delta;
    vec2 newPos = pos + newVel * delta;

    // Wrap...
    newPos.x = mod(newPos.x, float(width));
    newPos.y = mod(newPos.y, float(height));

    organisms.data[id] = Organism(newVel.x, newVel.y, organism.type);
    positions.data[id] = Position(newPos.x, newPos.y);
}