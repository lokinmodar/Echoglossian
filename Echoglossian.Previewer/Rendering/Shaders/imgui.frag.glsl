#version 450

layout(set = 0, binding = 1) uniform sampler MainSampler;
layout(set = 1, binding = 0) uniform texture2D MainTexture;

layout(location = 0) in vec4 color;
layout(location = 1) in vec2 texCoord;

layout(location = 0) out vec4 outputColor;

void main()
{
    outputColor = color * texture(sampler2D(MainTexture, MainSampler), texCoord);
}
