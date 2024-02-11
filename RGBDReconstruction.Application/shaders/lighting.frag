#version 420 core
out vec4 FragColor;

in vec3 Normal;
in vec3 FragPos;

in vec2 TexCoords;

struct Light {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    
    vec3 direction;
};

struct Material {
    sampler2D diffuse;
    //sampler2D specular;

    float shininess;
};

uniform Material material;
uniform Light light;

void main() {
    vec3 ambient = vec3(texture(material.diffuse, TexCoords)) * light.ambient;

    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(-(light.direction)); //normalize(lightPos - FragPos);

    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * light.diffuse * vec3(texture(material.diffuse, TexCoords));

    float specularStrength = 0.5;
    vec3 nFragPos = normalize(FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(-nFragPos, reflectDir), 0.0), material.shininess);
    //vec3 specular = vec3(texture(material.specular, TexCoords)) * spec * light.specular;
    vec3 specular = vec3(0.0) * spec * light.specular;

    vec3 result = ambient + diffuse + specular;
    FragColor = vec4(result, 1.0);
}
