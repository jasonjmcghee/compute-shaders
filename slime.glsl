#[compute]
#version 460

struct Agent {
    float x;
    float y;
    float angle;
    uint type;
    float lifetime;
};

struct Params {
    uint numAgents;
    int width;
    int height;
    float delta;
    float time;
    float moveSpeed;
    int sensorSize;
    float sensorOffsetDst;
    float sensorAngleDegrees;
    float turnSpeed;
    float trailWeight;
    float maxLifetime;
    float mouseX;
    float mouseY;
    bool mouseLeft;
    bool mouseRight;
};

// Invocations in the (x, y, z) dimension
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// Structured buffers
layout(set = 0, binding = 0, std430) restrict buffer AgentsBuffer {
    Agent agents[];
}
agents_buffer;

layout(set = 0, binding = 1, std430) readonly buffer ParamsBuffer
{
    Params params;
}
params_buffer;

layout(set = 0, binding = 2, std430) buffer trailMap_in {
    uint data[];
}
trailMap_in_buffer;

layout(set = 0, binding = 3, std430) buffer trailMap_out {
    uint data[];
}
trailMap_out_buffer;

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


uint combineColorComponents(vec4 color) {
    uint combinedColor = uint(color.a * 255.0) << 24u |
    uint(color.b * 255.0) << 16u |
    uint(color.g * 255.0) << 8u |
    uint(color.r * 255.0);
    return combinedColor;
}

float sense(Agent agent, float sensorAngleOffset) {
    uint id = gl_GlobalInvocationID.x;

    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    int sensorSize = params_buffer.params.sensorSize;
    float sensorOffsetDst = params_buffer.params.sensorOffsetDst;

    float sensorAngle = agent.angle + sensorAngleOffset;
    vec2 sensorDir = vec2(cos(sensorAngle), sin(sensorAngle));

    vec2 sensorPos = vec2(agent.x, agent.y) + sensorDir * sensorOffsetDst;
    int sensorCenterX = int(sensorPos.x);
    int sensorCenterY = int(sensorPos.y);

    float sum = 0.0;

    //    ivec4 senseWeight = agent.speciesMask * 2 - 1;

    for (int offsetY = -sensorSize; offsetY <= sensorSize; offsetY++) {
        int sampleY = min(height - 1, max(0, sensorCenterY + offsetY));
        uint row = sampleY * width;

        for (int offsetX = -sensorSize; offsetX <= sensorSize; offsetX++) {
            int sampleX = min(width - 1, max(0, sensorCenterX + offsetX));
            uint cell = row + sampleX;

            vec4 color = parseCombinedColor(trailMap_in_buffer.data[cell]);
            sum += dot(agent.type == 1 ? vec4(1, -0.5, -1, 0) : vec4(-4, 0.75, -1, 0), color);
        }
    }

    return sum;
}

void main() {
    uint numAgents = params_buffer.params.numAgents;
    int width = params_buffer.params.width;
    int height = params_buffer.params.height;
    float time = params_buffer.params.time;
    float delta = params_buffer.params.delta;
    float moveSpeed = params_buffer.params.moveSpeed;
    float trailWeight = params_buffer.params.trailWeight;
    float maxLifetime = params_buffer.params.maxLifetime;
    float mouseX = params_buffer.params.mouseX;
    float mouseY = params_buffer.params.mouseY;
    bool mouseLeft = params_buffer.params.mouseLeft;
    bool mouseRight = params_buffer.params.mouseRight;

    uint id = gl_GlobalInvocationID.x;

    if (id >= numAgents) {
        return;
    }

    Agent agent = agents_buffer.agents[id];

    // if (agent.lifetime <= 0) {
    //    return;
    // }

    vec2 pos = vec2(agent.x, agent.y);

    uint random = hash(uint(pos.y * width + pos.x * height) + hash(id + uint(time * 100000.0)));

    // Steer based on sensory data
    float sensorOffsetDst = params_buffer.params.sensorOffsetDst;
    float sensorAngleDegrees = params_buffer.params.sensorAngleDegrees;
    float turnSpeed = params_buffer.params.turnSpeed * 2.0 * 3.1415;

    if (agent.type == 1) {
        sensorAngleDegrees *= 0.5;
        sensorOffsetDst *= 2.0;
        turnSpeed *= 2;
        moveSpeed *= 2;
        trailWeight *= 0.7;
    }

    float sensorAngleRad = sensorAngleDegrees * (3.1415 * 0.00555555555);
    float weightForward = sense(agent, 0.0);
    float weightLeft = sense(agent, sensorAngleRad);
    float weightRight = sense(agent, -sensorAngleRad);

    float randomSteerStrength = scaleToRange01(random);

    // Continue in same direction
    if (weightForward > weightLeft && weightForward > weightRight) {
        agent.angle += 0.0;
    }
    // Turn randomly
    else if (weightForward < weightLeft && weightForward < weightRight) {
        agent.angle += (randomSteerStrength - 0.5) * 2.0 * turnSpeed * delta;
    }
    // Turn right
    else if (weightRight > weightLeft) {
        agent.angle -= randomSteerStrength * turnSpeed * delta;
    }
    // Turn left
    else if (weightLeft > weightRight) {
        agent.angle += randomSteerStrength * turnSpeed * delta;
    }

    if (weightForward + weightLeft + weightRight > 0) {
        agents_buffer.agents[id].lifetime = maxLifetime;
    } else {
        agents_buffer.agents[id].lifetime -= 1;
    }

    if ((mouseLeft || mouseRight) && randomSteerStrength > 0.99) {
        //        vec2 dir = normalize(vec2(mouseX, mouseY) - pos);
        //        agent.angle = atan(dir.y, dir.x);
        pos = vec2(mouseX, mouseY);
        agents_buffer.agents[id].type = mouseLeft ? 1 : 0;
    }

    // Update position
    vec2 direction = vec2(cos(agent.angle), sin(agent.angle));
    vec2 newPos = pos + direction * delta * moveSpeed;

    // Clamp position to map boundaries, and pick new random move dir if hit boundary
    if (newPos.x < 0.0 || newPos.x >= float(width - 1) || newPos.y < 0.0 || newPos.y >= float(height - 1)) {
        newPos.x = float(min(width-1, max(0, int(newPos.x))));
        newPos.y = float(min(height-1, max(0, int(newPos.y))));

        float randomAngle = scaleToRange01(hash(random)) * 2.0 * 3.1415;
        agent.angle = randomAngle;
    }

    agents_buffer.agents[id].angle = agent.angle;
    agents_buffer.agents[id].x = newPos.x;
    agents_buffer.agents[id].y = newPos.y;

    uint cell = uint(newPos.y) * width + uint(newPos.x);

    vec4 current = parseCombinedColor(trailMap_out_buffer.data[cell]);

    vec4 mask = vec4(agents_buffer.agents[id].type * 0.9, 1.0 - agents_buffer.agents[id].type * 0.9, 0.0, 1.0);
    vec4 newVal = mix(current, mask, trailWeight * delta);
    vec4 safe = vec4(min(1, newVal.r), min(1, newVal.g), 0.5, 1.0);
    trailMap_out_buffer.data[cell] = combineColorComponents(safe);
}

