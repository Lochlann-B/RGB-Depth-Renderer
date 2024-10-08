﻿#version 450
#extension GL_ARB_gpu_shader_fp64 : enable

layout (local_size_x = 1) in;

struct BVHNode {
    vec4 minPoint;
    vec4 maxPoint;
    uint leftIdx;
    uint rightIdx;
    uint parentIdx;
    uint objID;
};

struct Triangle {
    vec3 v1;
    vec3 v2;
    vec3 v3;
};

layout (std430, binding = 0) buffer positionBuffer {
    highp float positionArray[];
};

layout (std430, binding = 1) buffer indexBuffer {
    int indexArray[];
};

layout (std430, binding = 2) buffer BVHNodeBuffer {
    BVHNode nodes[];
};

layout (std430, binding = 3) buffer voxelBuffer {
    highp float voxelValues[];
};

layout (std430, binding = 4) buffer seenVoxelsBuffer {
    vec4 seenVoxels[];
};

layout (std430, binding = 5) buffer closeVoxelsBuffer {
    vec4 closeVoxels[];
};

layout (binding = 6, offset = 0) uniform atomic_uint closeVoxelsIdx;

layout (std430, binding = 7) buffer voxelWeightsBuffer {
    highp float voxelWeights[];
};

layout (std430, binding = 8) buffer voxelColoursBuffer {
    highp vec4 voxelColours[];
};

uniform int numObjs;

uniform highp float resolution;

uniform int size;

uniform highp float xStart;
uniform highp float yStart;
uniform highp float zStart;

uniform highp vec3 cameraPos;

uniform int groupSize;
uniform int groupIdx;

uniform sampler2D rgbMap;
uniform mat4 camPose;
uniform mat3 intrinsicMatrix;

double roundToInterval(double v, double interval) {
    if (interval == 0f) {
        return 1/0f;
    }

    double val = v / interval;
    const highp float eps = 0.00001;
    double roundedVal;
    
    double halfway = (floor(val) + ceil(val))/2f;
    
    if (val >= halfway - eps && val <= halfway + eps) {
        if (int(floor(val)) % 2 == 0) {
            roundedVal = floor(val);
        } else {
            roundedVal = ceil(val);
        }
    } else {
        roundedVal = round(val);
    }
    
    return roundedVal * interval;
}

int index(highp float px, highp float py, highp float pz) {

    double nX = roundToInterval(double(px) - double(xStart), double(resolution));
    double nY = roundToInterval(double(py) - double(yStart), double(resolution));
    double nZ = roundToInterval(double(pz) - double(zStart), double(resolution));

    return int(nX / double(resolution)) + int((nY / double(resolution))) * size + int((nZ / double(resolution))) * size * size;
}

bool intersectsPlane(highp float minCoord, highp float maxCoord, vec3 n, vec3 raySource, vec3 rayDirection, vec3 axis1, vec2 bounds1, vec3 axis2, vec2 bounds2) {
    highp float denominator = dot(n, rayDirection);
    bool doesIntersect = false;
    if (denominator != 0) {
        // min
        float d = minCoord;
        float t = (d - dot(n, raySource))/(denominator);
        if (t >= 0) {
            vec3 posmin = raySource + t*rayDirection;
            doesIntersect = doesIntersect || ((bounds1.x <= dot(axis1, posmin)) && (bounds1.y >= dot(axis1, posmin)) && (bounds2.x <= dot(axis2, posmin)) && (bounds2.y >= dot(axis2, posmin)));
        }

        // max
        d = maxCoord;
        t = (d - dot(n, raySource))/(denominator);
        if (t >= 0) {
            vec3 posmax = raySource + t*rayDirection;
            doesIntersect = doesIntersect || ((bounds1.x <= dot(axis1, posmax)) && (bounds1.y >= dot(axis1, posmax)) && (bounds2.x <= dot(axis2, posmax)) && (bounds2.y >= dot(axis2, posmax)));
        }
    }
    
    return doesIntersect;
}

bool rayIntersectsAABB(vec3 raySource, vec3 rayDirection, BVHNode node) {
    // We need to check the intersection with 6 planes
    // Plane is defined as n.r = d
    // Ray is defined as r = s + td
    
    bool doesIntersect = false;

    highp float minX = node.minPoint.x;
    highp float maxX = node.maxPoint.x;
    highp float minY = node.minPoint.y;
    highp float maxY = node.maxPoint.y;
    highp float minZ = node.minPoint.z;
    highp float maxZ = node.maxPoint.z;
    
    // x-axis planes
    doesIntersect = doesIntersect || intersectsPlane(minX, maxX, vec3(1, 0, 0), raySource, rayDirection, vec3(0, 1, 0), vec2(minY, maxY), vec3(0, 0, 1), vec2(minZ, maxZ));
    
    // y-axis planes
    doesIntersect = doesIntersect || intersectsPlane(minY, maxY, vec3(0, 1, 0), raySource, rayDirection, vec3(1, 0, 0), vec2(minX, maxX), vec3(0, 0, 1), vec2(minZ, maxZ));
    
    // z-axis planes
    doesIntersect = doesIntersect || intersectsPlane(minZ, maxZ, vec3(0, 0, 1), raySource, rayDirection, vec3(0, 1, 0), vec2(minY, maxY), vec3(1, 0, 0), vec2(minX, maxX));
    
    return doesIntersect;
}

vec3 getNormal(vec3 v1, vec3 v2, vec3 v3) {
    return normalize(cross(v2 - v1, v3 - v2));
}

bool pointInsideTriangle(vec3 p, vec3 n, vec3 v1, vec3 v2, vec3 v3) {
    vec3 c1 = p - v1;
    vec3 c2 = p - v2;
    vec3 c3 = p - v3;
    
    return abs(dot(n, cross(v2 - v1, c1))) > 0 && abs(dot(n, cross(v3 - v2, c2))) > 0 && abs(dot(n, cross(v1 - v3, c3))) > 0;
}

Triangle getTriangleFromNode(BVHNode node) {
    uint objId = node.objID;

    int i1 = int(3*objId);
    int i2 = i1 + 1;
    int i3 = i1 + 2;

    vec3 v1 = vec3(positionArray[3*indexArray[i1]],
    positionArray[3*indexArray[i1]+1],
    positionArray[3*indexArray[i1]+2]);

    vec3 v2 = vec3(positionArray[3*indexArray[i2]],
    positionArray[3*indexArray[i2]+1],
    positionArray[3*indexArray[i2]+2]);

    vec3 v3 = vec3(positionArray[3*indexArray[i3]],
    positionArray[3*indexArray[i3]+1],
    positionArray[3*indexArray[i3]+2]);
    
    Triangle tri;
    tri.v1 = v1;
    tri.v2 = v2;
    tri.v3 = v3;
    
    return tri;
}

vec4 rayIntersectsTriangle(vec3 raySource, vec3 rayDirection, Triangle triangle) {
    vec3 v1 = triangle.v1;

    vec3 v2 = triangle.v2;

    vec3 v3 = triangle.v3;
    
    vec3 n = getNormal(v1, v2, v3);
    
    if (abs(dot(n, rayDirection)) < 1e-6) {
        return vec4(0, 0, 0, 0);
    }

    highp float d = dot(n, v1);

    highp float t = (d - dot(n, raySource)) / dot(n, rayDirection);
    
    if (t < 0) {
        return vec4(0,0,0,0);
    }
    
    vec3 p = raySource + t * rayDirection;
    
    if (pointInsideTriangle(p, n, v1, v2, v3)) {
        return vec4(p, 1.0);
    }
    
    return vec4(0,0,0,0);
}

float minMagnitude(highp float f1, highp float f2) {
    highp float t1 = f1 < 0 ? f1 * -1 : f1;
    highp float t2 = f2 < 0 ? f2 * -1 : f2;
    
    if (t1 < t2) {
        return f1;
    }
    
    return f2;
}

void main() {
    uint idx = gl_GlobalInvocationID.x + groupIdx*groupSize;
    vec3 voxel = closeVoxels[idx].xyz;

//    voxelValues[idx] = 999f;
//    seenVoxels[idx] = vec4(voxel, 1.0);
//    return;
    
    vec3 raySource = cameraPos;
    vec3 rayDirection = normalize(voxel - raySource);
    
    //int rayIntersectObjIds[50];
    
    // Do ray intersection
    int stack[200];
    int stackPointer = 0;
    
    stack[stackPointer] = 0;
    stackPointer++;

    highp float minDist = 1/0f;
    highp float weight = 0f;
    uint objIdx = 0;
    
    //for (int i = 0; i < 5*(numObjs - 1); i++) {
    while(stackPointer > 0) {
//        if (stackPointer == 0) {
//            break;
//        }
        stackPointer--;
        int index = stack[stackPointer];
        BVHNode node = nodes[index];
        
        bool isLeaf = index >= numObjs - 1;
        
        if (isLeaf) {
            Triangle triangle = getTriangleFromNode(node);
            vec4 point = rayIntersectsTriangle(raySource, rayDirection, triangle);
//            float currWeight = 1f; //abs(dot(getNormal(triangle.v1, triangle.v2, triangle.v3), rayDirection));
            float currWeight = dot(getNormal(triangle.v1, triangle.v2, triangle.v3), rayDirection);
            if (point.w != 0) {
                vec3 nPoint = point.xyz;
                highp float dist = distance(nPoint, voxel);
                highp float distV = length(voxel - raySource);
                highp float distP = length(nPoint - raySource);
                
                if (distV > distP) {
                    dist *= -1;
                }
                
                minDist = minMagnitude(dist, minDist);
                if (minDist == dist) {
                    weight = currWeight;
                }
                objIdx = node.objID;
            }
        } else {
            if (node.leftIdx != 0 && rayIntersectsAABB(raySource, rayDirection, nodes[node.leftIdx])) {
                stack[stackPointer] = int(node.leftIdx);
                stackPointer++;
            }
            if (node.rightIdx != 0 && rayIntersectsAABB(raySource, rayDirection, nodes[node.rightIdx])) {
                stack[stackPointer] = int(node.rightIdx);
                stackPointer++;
            }
        }
    }
    

    seenVoxels[idx] = vec4(voxel, 1.0f);
    
    if (minDist < 1/0f) {
        //seenVoxels[atomicCounter(closeVoxelsIdx)] = vec4(voxel, 1.0f);
//        seenVoxels[idx] = vec4(voxel, 1.0f);
       // voxelValues[int((voxel.x - xStart)/resolution) + int((voxel.y - yStart)/resolution) * size + size * size * int((voxel.z - zStart)/resolution)] = minDist;
        int voxIdx = index(voxel.x, voxel.y, voxel.z);
        highp float W = voxelWeights[voxIdx];
        voxelValues[voxIdx] = (W*voxelValues[voxIdx] +  weight * minDist)/(W + weight);
        voxelWeights[voxIdx] = W + weight;
        
        
        // get colour
//        vec3 translation = vec3(camPose[0][3],camPose[1][3],camPose[2][3]);
        vec4 viewPos = camPose * vec4(voxel, 1.0f);
        vec3 pixelPos = intrinsicMatrix * viewPos.xyz;
        float z = pixelPos.z;
        pixelPos /= z;
//        pixelPos /= viewPos.z;
        vec2 imgCoord = pixelPos.xy;
        
        imgCoord.y = 1080 - imgCoord.y;

//        voxelColours[voxIdx] = vec4(1,0,0,1);//(voxelColours[voxIdx]*W + weight*texture(rgbMap, imgCoord))/(W + weight);
//        voxelColours[voxIdx].w = 1.0f;
        
        if (imgCoord.x < 0 || imgCoord.y < 0 || imgCoord.x >= 1920 || imgCoord.y >= 1080) {
            return;
        }
        
        
        voxelColours[voxIdx] = (voxelColours[voxIdx]*W + weight*texture(rgbMap, imgCoord/ivec2(1920,1080)))/(W + weight);
        
        
//        voxelColours[voxIdx] = texture(rgbMap, imgCoord/ivec2(1920, 1080));
        voxelColours[voxIdx].w = 1.0f;
        
//        voxelColours[voxIdx] = vec4((voxel.x + 2) / 2f, (voxel.y + 2) / 3f,
//             (voxel.z + 0.25f) / 6f, 1.0f);

    }
}
