#version 450
#extension GL_ARB_gpu_shader_int64 : enable

layout (local_size_x = 1) in;

layout (std430, binding = 0) buffer MortonCodes {
    uint64_t mortonCodes[];
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
    //return (v1 + v2 + v3)/3;
    float maxX = max(v1.x, max(v2.x, v3.x));
    float minX = min(v1.x, min(v2.x, v3.x));
    float maxY = max(v1.y, max(v2.y, v3.y));
    float minY = min(v1.y, min(v2.y, v3.y));
    float maxZ = max(v1.z, max(v2.z, v3.z));
    float minZ = min(v1.z, min(v2.z, v3.z));
    
    return vec3((maxX + minX)/2f, (maxY + minY)/2f, (maxZ + minZ)/2f);
}

uint expandBits(uint v) {
    v = (v * 0x00010001u) & 0xFF0000FFu;
    v = (v * 0x00000101u) & 0x0F00F00Fu;
    v = (v * 0x00000011u) & 0xC30C30C3u;
    v = (v * 0x00000005u) & 0x49249249u;
    return v;
}

uint64_t expandBits64(uint a) {
    // Taken from https://www.forceflow.be/2013/10/07/morton-encodingdecoding-through-bit-interleaving-implementations/,
    // under the Creative Commons Attribute-NonCommercial license
    
    uint64_t v = a & 0x1fffffu; // we only look at the first 21 bits
    v = (v | (v << 32)) & 0x1f00000000fffful;// & v1; // shift left 32 bits, OR with self, and 00011111000000000000000000000000000000001111111111111111
    v = (v | (v << 16)) & 0x1f0000ff0000fful; // shift left 32 bits, OR with self, and 00011111000000000000000011111111000000000000000011111111
    v = (v | (v << 8)) & 0x100f00f00f00f00ful; // shift left 32 bits, OR with self, and 0001000000001111000000001111000000001111000000001111000000000000
    v = (v | (v << 4)) & 0x10c30c30c30c30c3ul; // shift left 32 bits, OR with self, and 0001000011000011000011000011000011000011000011000011000100000000
    v = (v | (v << 2)) & 0x1249249249249249ul;
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

uint64_t getMortonCode64(vec3 p) {
    //uint64_t answer = 0ul;
    //answer |= expandBits64(uint(p.x)) | expandBits64(uint(p.y)) << 1 | expandBits64(uint(p.z)) << 2;
    //return answer;
    return expandBits64(floatBitsToUint(p.x)) + expandBits64(floatBitsToUint(p.y)) * 2ul + expandBits64(floatBitsToUint(p.z)) * 4ul;
}

uint64_t getUniqueMortonCode64(vec3 p, uint idx) {
    float x = p.x;
    float y = p.y;
    float z = p.z;
    x = min(max(x * 1024.0f, 0.0f), 1023.0f);
    y = min(max(y * 1024.0f, 0.0f), 1023.0f);
    z = min(max(z * 1024.0f, 0.0f), 1023.0f);
    uint xx = expandBits(uint(x));
    uint yy = expandBits(uint(y));
    uint zz = expandBits(uint(z));
    
    uint64_t uniqueCode = ((xx * 4 + yy * 2 + zz) << 32) + idx;
    return uniqueCode;
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
    //v1 = (v1 - minCoords)/(maxCoords - minCoords);

    vec3 v2 = vec3(positionBufferArray[3*indexBufferArray[i2]],
    positionBufferArray[3*indexBufferArray[i2]+1],
    positionBufferArray[3*indexBufferArray[i2]+2]);

    //v2 = (v2 - minCoords)/(maxCoords - minCoords);

    vec3 v3 = vec3(positionBufferArray[3*indexBufferArray[i3]],
    positionBufferArray[3*indexBufferArray[i3]+1],
    positionBufferArray[3*indexBufferArray[i3]+2]);

    //v3 = (v3 - minCoords)/(maxCoords - minCoords);
    
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
    
    centre = (centre - minCoords)/(maxCoords - minCoords);
    
    mortonCodes[gl_GlobalInvocationID.x] = getUniqueMortonCode64(centre, gl_GlobalInvocationID.x); //getMortonCode64(centre);
    
}