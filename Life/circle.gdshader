shader_type canvas_item;

uniform sampler2D mask;

varying vec4 color;

void vertex() {
	color = COLOR;
}

vec2 random(vec2 uv){
    uv = vec2( dot(uv, vec2(127.1,311.7) ),
               dot(uv, vec2(269.5,183.3) ) );
    return -1.0 + 2.0 * fract(sin(uv) * 43758.5453123);
}

float noise(vec2 uv) {
    vec2 uv_index = floor(uv);
    vec2 uv_fract = fract(uv);

    vec2 blur = smoothstep(0.0, 1.0, uv_fract);

    return mix( mix( dot( random(uv_index + vec2(0.0,0.0) ), uv_fract - vec2(0.0,0.0) ),
                     dot( random(uv_index + vec2(1.0,0.0) ), uv_fract - vec2(1.0,0.0) ), blur.x),
                mix( dot( random(uv_index + vec2(0.0,1.0) ), uv_fract - vec2(0.0,1.0) ),
                     dot( random(uv_index + vec2(1.0,1.0) ), uv_fract - vec2(1.0,1.0) ), blur.x), blur.y) + 0.5;
}

void fragment() {
    vec4 mask_value = texture(mask, UV);
	
    if (mask_value.r < 0.5) {
        discard;
    }

    // Calculate the distance from the current fragment to the center
    float dist = distance(UV, vec2(0.5));

    // Normalize the distance (so it's between 0 and 1)
    dist = clamp(dist, 0.0, 1.0);

    // Get the noise value at this point
    float n = noise(UV);

    // Apply the vignette: Multiply the current color by (1 - dist), then add dist * noise
	COLOR = color * mix(COLOR, vec4(n, n, n, n), dist);
    // COLOR = color * (1.0 - vec4(distance(UV,vec2(0.5))));
}