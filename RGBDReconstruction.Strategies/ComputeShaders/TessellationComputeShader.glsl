#version 450

layout (local_size_x = 1, local_size_y = 1) in;

layout (binding = 0, r32f) uniform image2D depthBuffer;

layout (std430, binding = 0) buffer IndexBuffer {
    uint indexArray[];
};

layout (std430, binding = 1) buffer PositionBuffer {
    float positionArray[];
};

layout (std430, binding = 2) buffer TexCoordBuffer {
    float texCoordArray[];
};

layout (std430, binding = 3) buffer NormalBuffer {
    float normalArray[];
};

uniform int width;
uniform int height;

float getFocal(float focalLength, float sensorSize, float imgSize) {
    return focalLength * (imgSize / sensorSize);
}

vec3 normal(vec3 v1, vec3 v2, vec3 v3) {
    vec3 s12 = v2 - v1;
    vec3 s23 = v3 - v2;
    return normalize(cross(s12, s23));
}

bool lengthLongerThanThreshold(vec3 v1, vec3 v2, vec3 v3, float threshold) {
    vec3 s12 = v2 - v1;
    vec3 s23 = v3 - v2;
    vec3 s13 = v3 - v1;
    return (length(s12) >= threshold) || (length(s23) >= threshold) || (length(s13) >= threshold);
}

void writeTriangleData(vec3 v1, vec3 v2, vec3 v3, ivec3 nv1, ivec3 nv2, ivec3 nv3, int stride, float fx, float fy, int width, int height, int cx, int cy, int x, int y) {
    if(lengthLongerThanThreshold(v1, v2, v3, 0.1f)) {
        return;
    }
    vec3 normal = normal(v1, v2, v3);
    const vec3 tri1[] = vec3[](v1, v2, v3);
    const ivec3 ntri1[] = ivec3[](nv1, nv2, nv3);

    for(int i = 0; i < 3; i++) {
        vec3 pos = tri1[i];
        // UV Coordinate is a simple xy plane projection
        vec2 texCoord = vec2((fy*pos.x/pos.z + cy)/height, (fx*pos.y/pos.z + cx)/width);

        ivec3 nPos = ntri1[i];
        int idx = (nPos.y * width + nPos.x);

        int idxidx = 6*(y*width + x) + i + stride;
        indexArray[idxidx] = idx;
        positionArray[idx*3] = pos.x;
        positionArray[idx*3+1] = pos.y;
        positionArray[idx*3+2] = pos.z;
        texCoordArray[idx*2] = texCoord.x;
        texCoordArray[idx*2+1] = texCoord.y;
        normalArray[idx*3] = normal.x;
        normalArray[idx*3+1] = normal.y;
        normalArray[idx*3+2] = normal.z;
    }
}

void main()
{
    int xres = 1;
    int yres = 1;
    
    ivec2 storePos = ivec2(gl_GlobalInvocationID.xy);
    int y = storePos.x;
    int x = storePos.y;
    ivec2 pos = ivec2(x,y);
    
    //positionArray[y*height + x] = imageLoad(depthBuffer, pos).r;
    
    vec4 depths = vec4(
    imageLoad(depthBuffer, pos).r,
    imageLoad(depthBuffer, ivec2(pos.x, pos.y+yres)).r,
    imageLoad(depthBuffer, ivec2(pos.x+xres, pos.y+yres)).r,
    imageLoad(depthBuffer, ivec2(pos.x+xres, pos.y)).r
    );
    //float depth = imageLoad(depthBuffer, storePos).x;

//    positionArray[3*(y*height + x)] = x;
//    positionArray[3*(y*height + x) + 1] = y;
//    positionArray[3*(y*height + x) + 2] = depths.x;
//    
//    positionArray[3*((y+yres)*height + x)] = x;
//    positionArray[3*((y+yres)*height + x)+1] = y + yres;
//    positionArray[3*((y+yres)*height + x) + 2] = depths.y;
//    
//    positionArray[3*((y+yres)*height + x+xres)] = x+xres;
//    positionArray[3*((y+yres)*height + x+xres) + 1] = y+yres;
//    positionArray[3*((y+yres)*height + x+xres) + 2] = depths.z;
//    
//    positionArray[3*((y)*height + x+xres)] = x+xres;
//    positionArray[3*((y)*height + x+xres) + 1] = y;
//    positionArray[3*((y)*height + x+xres) + 2] = depths.w;
//    return;
    
    
    int maxX = width-xres;
    int maxY = height-yres;

    int cx = (maxX + 1) / 2;
    int cy = (maxY + 1) / 2;
    float fx = getFocal(50f, 36f, 1920f);
    float fy = getFocal(28.125f, 36*(9/16f), 1080f);

    // Perform tessellation based on depth value
    // Calculate index, position, and texture coordinate for this pixel

    // Example: Calculating index
    //uint index = (storePos.y * width + storePos.x)*4;

    vec3 v1 = vec3( depths.x*((x-cx)/fx), depths.x*((y-cy)/fy), depths.x);
    vec3 v2 = vec3( depths.y*((x-cx)/fx), depths.y*((y+yres-cy)/fy), depths.y);
    vec3 v3 = vec3( depths.z*((x+xres-cx)/fx), depths.z*((y+yres-cy)/fy), depths.z );
    vec3 v4 = vec3( depths.w*((x+xres-cx)/fx), depths.w*((y-cy)/fy), depths.w );

//    vec3 v1 = vec3( x, y, depths.x);
//    vec3 v2 = vec3( x, y+yres, depths.y);
//    vec3 v3 = vec3( x+xres, y+yres, depths.z );
//    vec3 v4 = vec3( x+xres, y, depths.w );
    
    // Non-projected vertices - used for indexing
    ivec3 nv1 = ivec3( x, y, 1);
    ivec3 nv2 = ivec3( x, y+yres, 1);
    ivec3 nv3 = ivec3( x+xres, y+yres, 1 );
    ivec3 nv4 = ivec3( x+xres, y, 1 );
    
    // Triangle 1 - vertices 1, 2, 3
    writeTriangleData(v1, v2, v3, nv1, nv2, nv3, 0, fx, fy, width, height, cx, cy, x, y);
    
    // Triangle 2 - vertices 1, 3, 4
    writeTriangleData(v1, v3, v4, nv1, nv3, nv4, 3, fx, fy, width, height, cx, cy, x, y);
}