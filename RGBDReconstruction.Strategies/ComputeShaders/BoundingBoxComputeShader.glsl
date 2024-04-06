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

layout (std430, binding = 0) buffer BVHInternalNodes {
    BVHNode BVHInternals[];
};

layout (std430, binding = 1) buffer BVHLeafNodes {
    BVHNode BVHLeaves[];
};

layout (std430, binding = 2) buffer sortedObjects {
    int sortedObjs[];
};

layout (std430, binding = 3) buffer indexBuffer {
    int idxArray[];
};

layout (std430, binding = 4) buffer positionArrayBuffer {
    float positionArray[];
};

//layout (std430, binding = 5) buffer internalNodeAtomicCounter {
//    atomic_uint internalNodeCounters[];
//};

uniform int numObjs;

void main() {
    uint idx = gl_GlobalInvocationID.x;
    
    uint currentIdx = idx;
    BVHNode currentNode = BVHLeaves[idx];
    uint currentParentIdx = currentNode.parentIdx;
    
    uint triIdx = currentNode.objID;
    
    int v1Idx = idxArray[3*triIdx];
    int v2Idx = idxArray[3*triIdx + 1];
    int v3Idx = idxArray[3*triIdx + 2];
    
    vec3 v1 = vec3(positionArray[3*v1Idx], positionArray[3*v1Idx + 1], positionArray[3*v1Idx + 2]);
    vec3 v2 = vec3(positionArray[3*v2Idx], positionArray[3*v2Idx + 1], positionArray[3*v2Idx + 2]);
    vec3 v3 = vec3(positionArray[3*v3Idx], positionArray[3*v3Idx + 1], positionArray[3*v3Idx + 2]);
    
    float minX = min(v1.x, min(v2.x, v3.x));
    float maxX = max(v1.x, max(v2.x, v3.x));

    float minY = min(v1.y, min(v2.y, v3.y));
    float maxY = max(v1.y, max(v2.y, v3.y));

    float minZ = min(v1.z, min(v2.z, v3.z));
    float maxZ = max(v1.z, max(v2.z, v3.z));
    
    currentNode.maxPoint = vec4(maxX, maxY, maxZ, 1.0);
    currentNode.minPoint = vec4(minX, minY, minZ, 1.0);
    
    BVHLeaves[currentIdx] = currentNode;
    
    memoryBarrier();

    currentIdx = int(currentNode.parentIdx);
    
    for(int i = 0; i < 100; i++) {
        
//        if (internalNodeCounters[currentParentIdx] == 0) {
//            atomicCounterIncrement(internalNodeCounters[currentParentIdx]);
//            return;
//        }
        
        if (currentIdx == 0 && currentParentIdx == 0) {
            return;
        }
        
        BVHNode currentNode = BVHInternals[currentIdx];
        //currentParentIdx = currentNode.parentIdx;
        
        BVHNode left;
        if (currentNode.leftIdx >= numObjs - 1) {
            left = BVHLeaves[currentNode.leftIdx - numObjs + 1];
        } else {
            left = BVHInternals[currentNode.leftIdx];
        }

        BVHNode right;
        if (currentNode.rightIdx >= numObjs - 1) {
            right = BVHLeaves[currentNode.rightIdx - numObjs + 1];
        } else {
            right = BVHInternals[currentNode.rightIdx];
        }
        
        vec4 maxLeft = left.maxPoint;
        vec4 maxRight = right.maxPoint;

        vec4 minLeft = left.minPoint;
        vec4 minRight = right.minPoint;
        
        vec4 maxPoint = vec4(max(maxLeft.x, maxRight.x), max(maxLeft.y, maxRight.y), max(maxLeft.z, maxRight.z), 1.0);
        vec4 minPoint = vec4(min(minLeft.x, minRight.x), min(minLeft.y, minRight.y), min(minLeft.z, minRight.z), 1.0);
        
        currentNode.maxPoint = maxPoint;
        currentNode.minPoint = minPoint;
        
        BVHInternals[currentIdx] = currentNode;
        
        // this might be dodgy
        //memoryBarrier();

        currentIdx = int(currentNode.parentIdx);
        currentParentIdx = BVHInternals[currentIdx].parentIdx;
    }
}
