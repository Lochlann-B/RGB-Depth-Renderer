#version 420 core
out vec4 FragColor;

in vec2 vColor;

uniform sampler2D texture1;
uniform sampler2D texture2;

void main()
{
    FragColor = mix(texture(texture1, vColor), texture(texture2, vColor), 0.2);
}