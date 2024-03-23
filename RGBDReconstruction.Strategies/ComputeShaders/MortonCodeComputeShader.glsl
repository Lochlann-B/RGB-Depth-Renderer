#version 450

layout (local_size_x = 1) in;

layout (std430, binding = 0) buffer MortonCodes {
    uint mortonCodes[];
};

layout (std430, binding = 1) buffer indexBuffer
{
    int indexBufferArray[];
};

layout (std430, binding = 2) buffer positionBuffer
{
    float positionBufferArray[];
};

uniform float xRanges[2];
uniform float yRanges[2];
uniform float zRanges[2];

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
    uint xx = expandBits(uint(x));
    uint yy = expandBits(uint(y));
    uint zz = expandBits(uint(z));
    return xx * 4 + yy * 2 + zz;
}

void main() {
    int i1 = int(3*gl_GlobalInvocationID.x);
    int i2 = i1 + 1;
    int i3 = i1 + 2;
    
    vec3 minCoords = vec3(xRanges[0], yRanges[0], zRanges[0]);
    vec3 maxCoords = vec3(xRanges[1], yRanges[1], zRanges[1]);
    
    vec3 v1 = vec3(positionBufferArray[3*indexBufferArray[i1]], 
                positionBufferArray[3*indexBufferArray[i1]+1],
                positionBufferArray[3*indexBufferArray[i1]+2]);
    
    // Map coordinates to the unit cube
    v1 = (v1 - minCoords)/(maxCoords - minCoords);

    vec3 v2 = vec3(positionBufferArray[3*indexBufferArray[3*i2]],
    positionBufferArray[3*indexBufferArray[i2]+1],
    positionBufferArray[3*indexBufferArray[i2]+2]);

    v2 = (v2 - minCoords)/(maxCoords - minCoords);

    vec3 v3 = vec3(positionBufferArray[3*indexBufferArray[i3]],
    positionBufferArray[3*indexBufferArray[i3]+1],
    positionBufferArray[3*indexBufferArray[i3]+2]);

    v3 = (v3 - minCoords)/(maxCoords - minCoords);
    
//    vec3 v1 = vec3(imageLoad(positionBuffer, int(imageLoad(indexBuffer, i1).r)).r,
//    imageLoad(positionBuffer, int(imageLoad(indexBuffer, i1+1).r)).r,
//    imageLoad(positionBuffer, int(imageLoad(indexBuffer, i1+2).r)).r);
//
//    vec3 v2 = vec3(imageLoad(positionBuffer, int(imageLoad(indexBuffer, i2).r)).r,
//    imageLoad(positionBuffer, int(imageLoad(indexBuffer, i2+1).r)).r,
//    imageLoad(positionBuffer, int(imageLoad(indexBuffer, i2+2).r)).r);
//
//    vec3 v3 = vec3(imageLoad(positionBuffer, int(imageLoad(indexBuffer, i3).r)).r,
//    imageLoad(positionBuffer, int(imageLoad(indexBuffer, i3+1).r)).r,
//    imageLoad(positionBuffer, int(imageLoad(indexBuffer, i3+2).r)).r);

    vec3 centre = findCentroid(v1, v2, v3);
    
    mortonCodes[gl_GlobalInvocationID.x] = getMortonCode(centre);
    
}