#version 450 core

layout(location = 0) out vec4 outColor;

layout(set=0, binding=0) uniform sampler2D imguiTex; // rendered ImGui Window
layout(location = 0) in struct {
  vec2 Px; // screen pixel coord
  vec2 Uv; // screen uv coord
} In;
layout(location = 4) flat in vec4 PxRect; // bounding pixel rect for window
layout(location = 8) flat in vec4 UvRect; // bounding uv rect for window

layout(set = 0, binding = 1) uniform HudTestBuffer {
  float grayScaleLevel;
};

float luminance(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

vec3 saturate(vec3 color, float saturation)
{
    float l = luminance(color);
    return mix(color, vec3(l), saturation);
}

void main()
{
    vec4 c = textureLod(imguiTex, In.Uv, 0);
    outColor = vec4(saturate(c.rgb, grayScaleLevel), c.a);
}
