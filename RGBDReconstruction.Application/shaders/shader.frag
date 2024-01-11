#version 420 core
out vec4 FragColor;

uniform vec3 lightPos;

void main()
{
    //FragColor = mix(texture(texture1, vColor), texture(texture2, vColor), 0.2);
    FragColor = vec4(1.0);
}