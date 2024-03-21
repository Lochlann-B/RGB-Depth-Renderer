#version 450

layout (local_size_x = 1) in;

layout (std430, binding = 0) buffer MortonCodes {
    uint mortonCodes[];
};

uniform int[] indexBuffer;

uniform float[] positionBuffer;

vec3 findCentroid(vec3 v1, vec3 v2, vec3 v3) {
    return (v1 + v2 + v3)/3;
}

uint expandBits(uint v) {
    v = (v * 0x00010001u) & 0xFF0000FFu;
    v = (v * 0x00000101u) & 0x0F00F00Fu;
    v = (v * 0x00000011u) & 0xC30C30C3u;
    v = (v * 0x00000005u) & 0x49249249u;
    return v;
}

uint getMortonCode(vec3 p) {
    float x = p.x;
    float y = p.y;
    float z = p.z;
    x = min(max(x * 1024.0f, 0.0f), 1023.0f);
    y = min(max(y * 1024.0f, 0.0f), 1023.0f);
    z = min(max(z * 1024.0f, 0.0f), 1023.0f);
    uint xx = expandBits(x);
    uint yy = expandBits(y);
    uint zz = expandBits(z);
    return xx * 4 + yy * 2 + zz;
}

void main() {
    uint i1 = 3*gl_GlobalInvocationID.x;
    uint i2 = i1 + 1;
    uint i3 = i1 + 2;
    
    vec3 v1 = vec3(positionBuffer[indexBuffer[i1]],
    positionBuffer[indexBuffer[i1]+1],
    positionBuffer[indexBuffer[i1]+2]);

    vec3 v2 = vec3(positionBuffer[indexBuffer[i2]],
    positionBuffer[indexBuffer[i2]+1],
    positionBuffer[indexBuffer[i2]+2]);

    vec3 v3 = vec3(positionBuffer[indexBuffer[i3]],
    positionBuffer[indexBuffer[i3]+1],
    positionBuffer[indexBuffer[i3]+2]);
    
    vec3 centre = findCentroid(v1, v2, v3);
    
    mortonCodes[gl_GlobalInvocationID.x] = getMortonCode(centre);
    
}