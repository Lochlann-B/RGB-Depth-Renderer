#version 450
#extension GL_ARB_gpu_shader_int64 : enable

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

layout (std430, binding = 2) buffer sortedMortonCodesBuf {
    uint64_t sortedMortonCodes[];
};

uniform int numObjs;

int findMSB64(uint64_t value) {
    uint upper = uint(value >> 32);
    uint lower = uint(value & 0xFFFFFFFFul);

    if (upper != 0) {
        return 32 + findMSB(upper); 
    } else {
        return findMSB(lower);
    }
}

int clz(uint64_t x) {
    int msb = findMSB64(x);
    return (msb == -1) ? 64 : 63 - msb;
}

int delta(int i, int j) {
    if (i < 0 || j < 0 || i >= numObjs || j >= numObjs) {
        return -1;
    }
    if (sortedMortonCodes[i] == sortedMortonCodes[j]) {
        return clz(uint64_t(i) ^ uint64_t(j));
    }
    // Return longest common prefixes of binary digits between 64-bit integers i and j
    return clz(sortedMortonCodes[i] ^ sortedMortonCodes[j]);
}

ivec2 determineRange(uint idx) {
    int i = int(idx);
    
    // Determine direction of range (+1 for right box, -1 for left box)
    int d = sign(delta(i, i+1) - delta(i, i-1));
    if (d == 0) {
        d = 1;
    }
    
    // Compute upper bound for length of range
    int delMin = delta(i, i-d);
    int lMax = 2;
    
    while (delta(i, i + lMax*d) > delMin) {
        lMax *= 2;
    }

    // Find the other end using binary search
    int l = 0;
    for (int t = lMax/2; t >= 1; t /= 2) {
        if (delta(i, i + (l + t)*d) > delMin) {
            l += t;
        }
    }
    int j = i + l*d;

    if (i == 0) {
        j = numObjs - 2;
    } else {
        uint64_t prevCode = sortedMortonCodes[i - 1];
        uint64_t currCode = sortedMortonCodes[i];
        uint64_t nextCode = sortedMortonCodes[i + 1];
        
        int initialIdx = i;
        
        if (prevCode == currCode && nextCode == currCode) {
            int UB = numObjs - 2;
            int LB = i;
            uint64_t originalCode = currCode;
            while (i > 0 && i < numObjs - 1) {
                i = (UB + LB) / 2;
                if (i >= numObjs - 1) {
                    break;
                }
                
                if (sortedMortonCodes[i] != originalCode && originalCode != sortedMortonCodes[i + 1]) {
                    // gone too high, reduce upper bound
                    UB = i;
                }
                
                if (sortedMortonCodes[i] == originalCode && sortedMortonCodes[i + 1] == originalCode) {
                    // gone too low, increase lower bound
                    LB = i;
                }
                
                if (sortedMortonCodes[i] == originalCode && originalCode != sortedMortonCodes[i + 1]) {
                    j = i;
                    i = initialIdx;
                    break;
                }
            }
        }
    }
    
    
    
    // Find split position using binary search
    int delNode = delta(i, j);
    int s = 0;

    for (int t = (l + 1)/2; t >= 1; t /= 2) {
        if (delta(i, i + (s+t)*d) > delNode) {
            s += t;
        }
        
        if (t > 1) {
            t++;
        }
    }

    int gamma = i + s*d + min(d, 0);
    
    int left;
    int right;

    // Output child pointers
    if (min(i, j) == gamma) {
        // If the split is on the boundary, then the left node is a leaf node.
        // Left holds a reference to the leaf array, which is distinguished from
        // the internal nodes array by having the index be greater than n.
        // Internal nodes array length = n-1, leaf (object) array = n for reference.
        
        left = numObjs + gamma - 1;
    } else {
        left = gamma;
    }
    if (max(i,j) == gamma + 1) {
        // Same but for the upper bound
        right = numObjs + gamma;
    } else {
        right = gamma + 1;
    }
    
    return ivec2(left, right);
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    ivec2 range = determineRange(idx);
    int first = range.x;
    int last = range.y;
    
    int leftOffset = 0;
    int rightOffset = 0;
    
    memoryBarrierBuffer();
    
    BVHNode currentNode = BVHInternals[idx];
    BVHNode left;
    BVHNode right;

    currentNode.leftIdx = uint(first);
    currentNode.rightIdx = uint(last);

    BVHInternals[idx] = currentNode;
    
    memoryBarrierBuffer();
    
    if (first >= numObjs - 1) {
        left = BVHLeaves[first - (numObjs - 1)];
        leftOffset = numObjs - 1;
    } else {
        left = BVHInternals[first];
    }
    
    if (last >= numObjs - 1) {
        right = BVHLeaves[last - (numObjs - 1)];
        rightOffset = numObjs - 1;
    } else {
        right = BVHInternals[last];
    }
    
    memoryBarrierBuffer();
    
    left.parentIdx = idx;
    right.parentIdx = idx;
    
    memoryBarrierBuffer();
    
    if (leftOffset > 0) {
        BVHLeaves[first - leftOffset] = left;
    } else {
        BVHInternals[first] = left;
    }
    
    if (rightOffset > 0) {
        BVHLeaves[last - rightOffset] = right;
    } else {
        BVHInternals[last] = right;
    }
    
    memoryBarrier();
}
