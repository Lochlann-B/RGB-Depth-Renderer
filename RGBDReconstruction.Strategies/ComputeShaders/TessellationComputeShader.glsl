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
    float zThreshold = 100000f;//0.004f*yres;
    if (abs(v1.z - v2.z) > zThreshold || abs(v2.z - v3.z) > zThreshold || abs(v1.z - v3.z) > zThreshold) {
        // Assume that the triangle should be flat
        vec3 v1m = v1;
        vec3 v2m = vec3(v2.x, v1.y, v2.z);
        vec3 v3m = vec3(v3.x, v1.y, v3.z);
        
        // Calculate the angle between viewing direction and surface (viewing direction given by (0, 0, 1) since are already working in camera view space)
        vec3 n = normalize(normal(v1m, v2m, v3m));
        float adjCoeff = pow(abs(n.z), 1/4f); // dot(normal, vec3(0,0,1))
        
        
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
    
    vec4 depths = vec4(
    imageLoad(depthBuffer, pos).r,
    imageLoad(depthBuffer, ivec2(pos.x, pos.y+yres)).r,
    imageLoad(depthBuffer, ivec2(pos.x+xres, pos.y+yres)).r,
    imageLoad(depthBuffer, ivec2(pos.x+xres, pos.y)).r
    );
    
    
    int maxX = width-xres;
    int maxY = height-yres;

    int cx = (maxX + 1) / 2;
    int cy = (maxY + 1) / 2;
    
    int cenx = width/2;
    int ceny = height/2;
    float fx = getFocal(50f, 36f, 1920f);
    float fy = fx;

    // Perform tessellation based on depth value
    // Calculate index, position, and texture coordinate for this pixel
    
    

    mat4 rotation = transformationMatrix;
    vec4 translation = vec4(rotation[0][3], rotation[1][3], rotation[2][3], 0f);

    rotation[0][3] = 0f;
    rotation[1][3] = 0f;
    rotation[2][3] = 0f;
    rotation[3][0] = 0f;
    rotation[3][1] = 0f;
    rotation[3][2] = 0f;


    vec3 v1 = (-translation + (rotation * (  vec4( (depths.x)*((x-cenx)/fx), -(depths.x)*((height-y-ceny)/fy), depths.x, 1.0)))).xyz;
    vec3 v2 = (-translation + (rotation * (  vec4( (depths.y)*((x-cenx)/fx), -(depths.y)*((height-y-yres-ceny)/fy), depths.y, 1.0)))).xyz;
    vec3 v3 = (-translation +  (rotation * (  vec4( (depths.z)*((x+xres-cenx)/fx), -(depths.z)*((height-y-yres-ceny)/fy), depths.z, 1.0 )))).xyz;
    vec3 v4 = (-translation + (rotation * ( vec4( (depths.w)*((x+xres-cenx)/fx), -(depths.w)*((height-y-ceny)/fy), depths.w, 1.0 )))).xyz;
    
    
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
    

    writeTriangleData(v1, v2, v3, nv1, nv2, nv3, 0, fx, fy, width, height, cx, cy, x, y);

    writeTriangleData(v1, v3, v4, nv1, nv3, nv4, 3, fx, fy, width, height, cx, cy, x, y);
}