shader_type canvas_item;

uniform sampler2D trailMap;

void fragment() {
	vec4 color = texture(trailMap, UV);
    COLOR = color;
}
