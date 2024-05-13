#version 420 core
out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;
in vec4 Colour;

struct Light {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    
    vec3 direction;
};

struct Material {
    sampler2D diffuse;

    float shininess;
};

uniform Material material;
uniform Light light;

void main() {
    
    FragColor = Colour;
}
