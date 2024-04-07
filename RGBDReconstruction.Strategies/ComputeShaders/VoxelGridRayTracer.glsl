#version 450

layout (local_size_x = 1) in;

struct BVHNode {
    vec4 minPoint;
    vec4 maxPoint;
    uint leftIdx;
    uint rightIdx;
    uint parentIdx;
    uint objID;
};

layout (std430, binding = 0) buffer positionBuffer {
    float positionArray[];
};

layout (std430, binding = 1) buffer indexBuffer {
    int indexArray[];
};

layout (std430, binding = 2) buffer BVHNodeBuffer {
    BVHNode nodes[];
};

layout (std430, binding = 3) buffer voxelBuffer {
    float voxelValues[];
};

layout (std430, binding = 4) buffer seenVoxelsBuffer {
    vec4 seenVoxels[];
};

layout (std430, binding = 5) buffer closeVoxelsBuffer {
    vec4 closeVoxels[];
};

layout (binding = 6, offset = 0) uniform atomic_uint closeVoxelsIdx;

uniform int numObjs;

uniform float resolution;

uniform int size;

uniform float xStart;
uniform float yStart;
uniform float zStart;

uniform vec3 cameraPos;

uniform int groupSize;
uniform int groupIdx;

float roundToInterval(float v, float interval) {
    if (interval == 0f) {
        return 1/0f;
    }
    
    return float(round(v / interval)) * interval;
}

int index(float px, float py, float pz) {
    float nX = roundToInterval(px - xStart, resolution);
    float nY = roundToInterval(py - yStart, resolution);
    float nZ = roundToInterval(pz - zStart, resolution);

    return int(nX / resolution) + int((nY / resolution) * size) + int((nZ / resolution) * size * size);
}

bool intersectsPlane(float minCoord, float maxCoord, vec3 n, vec3 raySource, vec3 rayDirection, vec3 axis1, vec2 bounds1, vec3 axis2, vec2 bounds2) {
    float denominator = dot(n, rayDirection);
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
    
    float minX = node.minPoint.x;
    float maxX = node.maxPoint.x;
    float minY = node.minPoint.y;
    float maxY = node.maxPoint.y;
    float minZ = node.minPoint.z;
    float maxZ = node.maxPoint.z;
    
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
    
    return dot(n, cross(v2 - v1, c1)) > 0 && dot(n, cross(v3 - v2, c2)) > 0 && dot(n, cross(v1 - v3, c3)) > 0;
}

vec4 rayIntersectsTriangle(vec3 raySource, vec3 rayDirection, BVHNode leafNode) {
    uint objId = leafNode.objID;

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
    
    vec3 n = getNormal(v1, v2, v3);
    
    if (abs(dot(n, rayDirection)) < 1e-6) {
        return vec4(0, 0, 0, 0);
    }
    
    float d = dot(n, v1);
    
    float t = (d - dot(n, raySource)) / dot(n, rayDirection);
    
    if (t < 0) {
        return vec4(0,0,0,0);
    }
    
    vec3 p = raySource + t * rayDirection;
    
    if (pointInsideTriangle(p, n, v1, v2, v3)) {
        return vec4(p, 1.0);
    }
    
    return vec4(0,0,0,0);
}

float minMagnitude(float f1, float f2) {
    float t1 = f1 < 0 ? f1 * -1 : f1;
    float t2 = f2 < 0 ? f2 * -1 : f2;
    
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
    
    float minDist = 1/0f;
    
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
            vec4 point = rayIntersectsTriangle(raySource, rayDirection, node);
            if (point.w != 0) {
                vec3 nPoint = point.xyz;
                float dist = distance(nPoint, voxel);
                float distV = length(voxel - raySource);
                float distP = length(nPoint - raySource);
                
                if (distV > distP) {
                    dist *= -1;
                }
                
                minDist = minMagnitude(dist, minDist);
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
    
//    voxelValues[index(voxel.x, voxel.y, voxel.z)] = minDist;
    seenVoxels[idx] = vec4(voxel, 1.0f);
    
    if (minDist < 1/0f) {
        //seenVoxels[atomicCounter(closeVoxelsIdx)] = vec4(voxel, 1.0f);
//        seenVoxels[idx] = vec4(voxel, 1.0f);
        voxelValues[index(voxel.x, voxel.y, voxel.z)] = minDist;
        //atomicCounterIncrement(closeVoxelsIdx);
    }
}
