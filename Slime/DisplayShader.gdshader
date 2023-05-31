shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D trailMap;

// https://godotshaders.com/shader/sub-pixel-accurate-pixel-sprite-filtering/
// *** Sub-pixel Accurate Pixel-Sprite Filtering ***
// (Effectively implements analytical anti-aliasing for point-filtered textures.)
//
// A new uv-coordinate is computed that can be used to look up a texture with
// linear filtering enabled. Therefore only one texture lookup is required.
// This means you have to check the "Filter" option, when importing the texture.
//
// A side effect is, that rotated sprites also look smooth.
//
// The only use is if you require non-integer scale or rotation. Otherwise, the
// result will be identical to point-filtering.
//
// Possible Issues:
// - If a sprite has non-integer coordinates (this can be caused by using 
//   the "centered" option), or by manually moving it to a non-integer 
//   coordinate, it will appear more blurry than without filtering.
//   This is simply an effect of the algorithm.
// - There is still aliasing on the outermost edges of the sprite.
//   This cannot be solved within the fragment shader. It is due to lack of 
//   real anti-aliasing in the rasterizer.


// Additional smoothing factor. Should usually be left at 1.0
// Lower values cause a stronger smoothing.
uniform float smoothing_factor : hint_range(0.1, 1.0) = 1.0;

void fragment() {
	// compute the new uv
	vec2 uv = UV;
	vec2 uv_width = fwidth(UV);
	vec2 sprite_screen_resolution = smoothing_factor / uv_width;

	vec2 uv_pixel_src = floor(uv / TEXTURE_PIXEL_SIZE + 0.499);

	vec2 edge = uv_pixel_src;
	edge = edge * TEXTURE_PIXEL_SIZE * sprite_screen_resolution;

	vec2 uv_pixel = uv * sprite_screen_resolution;
	vec2 uv_factor = clamp(uv_pixel - edge + 0.5, 0.0, 1.0);

	uv = (mix(uv_pixel_src - 1.0, uv_pixel_src, uv_factor) + 0.5) * TEXTURE_PIXEL_SIZE;

	// now we can use the uv as always...
	COLOR = texture(trailMap, uv).rgba;
}
