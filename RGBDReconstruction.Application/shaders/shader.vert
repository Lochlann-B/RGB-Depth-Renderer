﻿#version 420 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec4 aColour;

out vec3 Normal;
out vec3 FragPos;
out vec4 Colour;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

uniform mat3 normal;

void main()
{
    gl_Position = vec4(aPosition, 1.0) * model * view * projection;
    Normal = aNormal * normal;
    FragPos = (vec4(aPosition, 1.0) * model * view).xyz;
    Colour = aColour;
}