shader_type canvas_item;

uniform sampler2D organismMap;
uniform vec2 size;

vec3 rgb2hsl(vec3 color)
{
    float r = color.r;
    float g = color.g;
    float b = color.b;
    
    float max = max(r, max(g, b));
    float min = min(r, min(g, b));
    float h, s, l = (max + min) / 2.0;
    
    if(max == min)
    {
        h = s = 0.0; // achromatic
    }
    else
    {
        float d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        
        if(max == r)
            h = (g - b) / d + (g < b ? 6.0 : 0.0);
        else if(max == g)
            h = (b - r) / d + 2.0;
        else if(max == b)
            h = (r - g) / d + 4.0;
        
        h /= 6.0;
    }
    
    return vec3(h, s, l);
}

float hue2rgb(float p, float q, float t)
{
    if(t < 0.0) t += 1.0;
    if(t > 1.0) t -= 1.0;
    if(t < 1.0/6.0) return p + (q - p) * 6.0 * t;
    if(t < 1.0/2.0) return q;
    if(t < 2.0/3.0) return p + (q - p) * (2.0/3.0 - t) * 6.0;
    return p;
}

vec3 hsl2rgb(vec3 color)
{
    float h = color.r;
    float s = color.g;
    float l = color.b;
    
    float r, g, b;
    
    if (s == 0.0) {
        r = g = b = l; // achromatic
    } else {
        float q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
        float p = 2.0 * l - q;
        r = hue2rgb(p, q, h + 1.0/3.0);
        g = hue2rgb(p, q, h);
        b = hue2rgb(p, q, h - 1.0/3.0);
   }
   
   return vec3(r, g, b);
}

void fragment() {
    int type = int(texture(organismMap, UV).r * 255.0);
    vec4 color = vec4(0.0);
	
	if (type != 0) {
		color = vec4(hsl2rgb(vec3(float(type) / 10.0, 1.0, 0.5)), 1.0);
	}
    
	COLOR = color;
}
