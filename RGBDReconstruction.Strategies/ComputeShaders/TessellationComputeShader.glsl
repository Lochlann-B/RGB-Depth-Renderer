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

uniform int xres;
uniform int yres;

uniform mat4 transformationMatrix;

struct Triangle {
    vec3 v1;
    vec3 v2;
    vec3 v3;
};

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
    if(lengthLongerThanThreshold(v1, v2, v3, 0.1f + (xres + yres)/80f)) {
        return;
    }
    vec3 normal = normal(v1, v2, v3);
    const vec3 tri1[] = vec3[](v1, v2, v3);
    const ivec3 ntri1[] = ivec3[](nv1, nv2, nv3);

    for(int i = 0; i < 3; i++) {
        vec3 pos = tri1[i];
        pos.z *= -1;
        pos.x *= -1;
        
        // UV Coordinate is a simple xy plane projection
        vec2 texCoord = vec2((fy*pos.x/pos.z + cy)/height, (fx*pos.y/pos.z + cx)/width);

        ivec3 nPos = ntri1[i];
        int idx = ((nPos.y)/(yres) * (width/xres) + (nPos.x/xres));

        int idxidx = 6*((y/yres)*(width/xres) + x/xres) + i + stride;
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

Triangle getProjectionCorrection(vec3 v1, vec3 v2, vec3 v3) {
    float zThreshold = 0.004f*yres;
    if (abs(v1.z - v2.z) > zThreshold || abs(v2.z - v3.z) > zThreshold || abs(v1.z - v3.z) > zThreshold) {
        // Assume that the triangle should be flat
        vec3 v1m = v1;
        vec3 v2m = vec3(v2.x, v1.y, v2.z);
        vec3 v3m = vec3(v3.x, v1.y, v3.z);
        
        // Calculate the angle between viewing direction and surface (viewing direction given by (0, 0, 1) since are already working in camera view space)
        vec3 n = normalize(normal(v1m, v2m, v3m));
        float adjCoeff = pow(abs(n.z), 1/4f); // dot(normal, vec3(0,0,1))
        
        // okay chatgpt's idea turned out to be a bit shit... will try making all the y coordinates the same manually as they should be by finding the pair with the smallest diff in y, and subtracting the diff within this pair from the larger coord and third coord
        
        float nDiff1 = dot((v1 - v2), n);
        float nDiff2 = dot((v2 - v3), n);
        float nDiff3 = dot((v3 - v1), n);

        float v1Comp = dot(v1, n);
        float v2Comp = dot(v2, n);
        float v3Comp = dot(v3, n);
        
        if (nDiff1 <= nDiff2 && nDiff1 <= nDiff3) {
            // If nDiff1 has the smallest diff, then either v3 has largest uncorrected values or both v1 and v2 do. In either case, subtract from larger vertices
            if (v3Comp > v1Comp || v3Comp > v2Comp) {
                v3 -= v3Comp*(v3 - v1);
            } else {
                v1 -= v1Comp*(v3 - v1);
                v2 -= v2Comp*(v2 - v3);
            }
        } else if (nDiff2 <= nDiff1 && nDiff2 <= nDiff3) {
            if (v1Comp > v2Comp || v1Comp > v3Comp) {
                v1 -= v1Comp*(v1 - v2);
            } else {
                v3 -= v3Comp*(v3 - v1);
                v2 -= v2Comp*(v1 - v2);
            }
        } else if (nDiff3 <= nDiff1 && nDiff3 <= nDiff2) {
            if (v3Comp > v2Comp || v3Comp > v1Comp) {
                v2 -= v2Comp*(v2 - v3);
            } else {
                v1 -= v1Comp*(v1 - v2);
                v3 -= v3Comp*(v2 - v2);
            }
        }


        Triangle adjTri;
        adjTri.v1 = v1;
        adjTri.v3 = v3;
        adjTri.v2 = v2;
    }
    
    Triangle tri;
    tri.v3 = v3;
    tri.v2 = v2;
    tri.v1 = v1;
    return tri;
}

void main()
{
    
    ivec2 storePos = ivec2(gl_GlobalInvocationID.xy);
    int y = storePos.x*yres;
    int x = storePos.y*xres;
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
    
    int cenx = width/2;
    int ceny = height/2;
    float fx = getFocal(50f, 36f, 1920f);
//    float fy = getFocal(28.125f, 36*(9/16f), 1080f);
    float fy = getFocal(50f, 36f, 1080f);

    // Perform tessellation based on depth value
    // Calculate index, position, and texture coordinate for this pixel

    // Example: Calculating index
    //uint index = (storePos.y * width + storePos.x)*4;
    
    mat4 rotation = transpose(transformationMatrix);
    vec4 translation = vec4(rotation[3][0], rotation[3][1], rotation[3][2], 0f);
//    vec4 translation = vec4(0);
    rotation[3][0] = 0f;
    rotation[3][1] = 0f;
    rotation[3][2] = 0f;
    
    //float canvDist = 50/36f;

//    vec3 v1 = (rotation * (translation + vec4( (depths.x + canvDist)*((x-cx)/fx), (depths.x + canvDist)*((y-cy)/fy), depths.x + canvDist, 1.0))).xyz;
//    vec3 v2 = (rotation * (translation + vec4( (depths.y + canvDist)*((x-cx)/fx), (depths.y + canvDist)*((y+yres-cy)/fy), depths.y + canvDist, 1.0))).xyz;
//    vec3 v3 = (rotation * (translation + vec4( (depths.z + canvDist)*((x+xres-cx)/fx), (depths.z + canvDist)*((y+yres-cy)/fy), depths.z + canvDist, 1.0 ))).xyz;
//    vec3 v4 = (rotation * (translation + vec4( (depths.w + canvDist)*((x+xres-cx)/fx), (depths.w + canvDist)*((y-cy)/fy), depths.w + canvDist, 1.0 ))).xyz;
    
//    vec3 v1 = (rotation * (translation + vec4( (depths.x)*((x-cx)/fx), (depths.x)*((y-cy)/fy), depths.x, 1.0))).xyz;
//    vec3 v2 = (rotation * (translation + vec4( (depths.y)*((x-cx)/fx), (depths.y)*((y+yres-cy)/fy), depths.y, 1.0))).xyz;
//    vec3 v3 = (rotation * (translation + vec4( (depths.z)*((x+xres-cx)/fx), (depths.z)*((y+yres-cy)/fy), depths.z, 1.0 ))).xyz;
//    vec3 v4 = (rotation * (translation + vec4( (depths.w)*((x+xres-cx)/fx), (depths.w)*((y-cy)/fy), depths.w, 1.0 ))).xyz;

    vec3 v1 = (rotation * (translation + vec4( (depths.x)*((x-cenx)/fx), -(depths.x)*((height-y-ceny)/fy), depths.x, 1.0))).xyz;
    vec3 v2 = (rotation * (translation + vec4( (depths.y)*((x-cenx)/fx), -(depths.y)*((height-y-yres-ceny)/fy), depths.y, 1.0))).xyz;
    vec3 v3 = (rotation * (translation + vec4( (depths.z)*((x+xres-cenx)/fx), -(depths.z)*((height-y-yres-ceny)/fy), depths.z, 1.0 ))).xyz;
    vec3 v4 = (rotation * (translation + vec4( (depths.w)*((x+xres-cenx)/fx), -(depths.w)*((height-y-ceny)/fy), depths.w, 1.0 ))).xyz;
    
    
//    Triangle adjTri1 = getProjectionCorrection(v1, v2, v3);
//    Triangle adjTri2 = getProjectionCorrection(v1, v3, v4);
//    
//    v1 = adjTri1.v1;
//    v2 = adjTri2.v2;
//    v3 = adjTri2.v2;
//    v4 = adjTri3.v3;
    
//    float z1Adj = getProjectionCorrection(v1, v2, v3);
//    float z2Adj = getProjectionCorrection(v1, v3, v4);
//    float zAvAdj = (z1Adj + z2Adj)/2f;

//    v1.y = -(zAvAdj*depths.x)*((height-y-yres-ceny)/fy);
//    v2.y = -(z1Adj*depths.y)*((height-y-yres-ceny)/fy);
//    v3.y = -(zAvAdj*depths.z)*((height-y-yres-ceny)/fy);
//    v4.y = -(z2Adj*depths.w)*((height-y-yres-ceny)/fy);
//    v2 = (rotation * (translation + vec4( (depths.y)*((x-cenx)/fx), -(z1Adj*depths.y)*((height-y-yres-ceny)/fy), depths.y, 1.0))).xyz;
//    v3 = (rotation * (translation + vec4( (depths.z)*((x+xres-cenx)/fx), -(zAvAdj*depths.z)*((height-y-yres-ceny)/fy), depths.z, 1.0 ))).xyz;
//    v4 = (rotation * (translation + vec4( (depths.w)*((x+xres-cenx)/fx), -(z2Adj*depths.w)*((height-y-ceny)/fy), depths.w, 1.0 ))).xyz;
    

//    vec3 v1 = (transpose(transformationMatrix) * vec4( depths.x*((x-cx)/fx), depths.x*((y-cy)/fy), depths.x, 1.0)).xyz;
//    vec3 v2 = (transpose(transformationMatrix) * vec4( depths.y*((x-cx)/fx), depths.y*((y+yres-cy)/fy), depths.y, 1.0)).xyz;
//    vec3 v3 = (transpose(transformationMatrix) * vec4( depths.z*((x+xres-cx)/fx), depths.z*((y+yres-cy)/fy), depths.z, 1.0 )).xyz;
//    vec3 v4 = (transpose(transformationMatrix) * vec4( depths.w*((x+xres-cx)/fx), depths.w*((y-cy)/fy), depths.w, 1.0 )).xyz;

//    vec3 v1 = vec3( x, y, depths.x);
//    vec3 v2 = vec3( x, y+yres, depths.y);
//    vec3 v3 = vec3( x+xres, y+yres, depths.z );
//    vec3 v4 = vec3( x+xres, y, depths.w );
    
    // Non-projected vertices - used for indexing
    int adjXRes = xres;
    int adjYRes = yres;
    if (y >= height - yres) {
        adjYRes -= yres;
    }
    if (x >= width - xres) {
        adjXRes -= xres;
    }
    ivec3 nv1 = ivec3( x, y, 1);
    ivec3 nv2 = ivec3( x, y+adjYRes, 1);
    ivec3 nv3 = ivec3( x+adjXRes, y+adjYRes, 1 );
    ivec3 nv4 = ivec3( x+adjXRes, y, 1 );
    
    // Triangle 1 - vertices 1, 2, 3
//    if (z1Adj > 1e-4) {
        writeTriangleData(v1, v2, v3, nv1, nv2, nv3, 0, fx, fy, width, height, cx, cy, x, y);
//    }
    
    // Triangle 2 - vertices 1, 3, 4
//    if (z2Adj > 1e-4) {
        writeTriangleData(v1, v3, v4, nv1, nv3, nv4, 3, fx, fy, width, height, cx, cy, x, y);
//    }
}