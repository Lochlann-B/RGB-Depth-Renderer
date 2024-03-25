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

layout (std430, binding = 2) buffer sortedMortonCodesBuf {
    uint sortedMortonCodes[];
};

uniform int numObjs;

int clz(uint x) {
    int msb = findMSB(x);
    return (msb == -1) ? 32 : 31 - msb;
}

int delta(int i, int j) {
    if (i < 0 || j < 0 || i >= numObjs || j >= numObjs) {
        return -1;
    }
    // Return longest common prefixes of binary digits between 32-bit integers i and j
    return clz(sortedMortonCodes[i] ^ sortedMortonCodes[j]);
}

ivec2 determineRange(uint idx) {
    int i = int(idx);
    
    // Determine direction of range (+1 for right box, -1 for left box)
    int d = sign(delta(i, i+1) - delta(i, i-1));
    
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

    // Find split position using binary search
    int delNode = delta(i, j);
    int s = 0;
    
//    float div2 = 2f;
//    
//    int numIts = int(ceil(log2(l/2f)));
    
//    for (int i = 0; i < numIts; i++) {
//        int t = int(ceil(l/(pow(2, i+1))));
//        
//        if (t < 1) {
//            break;
//        }
//
//        if (delta(i, i + (s+t)*d) > delNode) {
//            s += t;
//        }
//
//        if (t == 1) {
//            break;
//        }
//    }

//    for (int t = int(ceil((l/div2))); t >= 1; div2 *= 2) {
//
//        if (delta(i, i + (s+t)*d) > delNode) {
//            s += t;
//        }
//
//        if (t == 1) {
//            break;
//        }
//    }
    
//    for (float t0 = (l/2f); t0 > 2.0f; t0 /= 2f) {
//        int t = int(ceil(t0));
//
//        if (delta(i, i + (s+t)*d) > delNode) {
//            s += t;
//        }
//
//        if (t == 1 || t0 < 0.5f) {
//            break;
//        }
//    }

    for (int t = (l + 1)/2; t >= 1; t /= 2) {
        if (delta(i, i + (s+t)*d) > delNode) {
            s += t;
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
        
        left = numObjs + gamma;
    } else {
        left = gamma;
    }
    if (max(i,j) == gamma + 1) {
        // Same but for the upper bound
        right = numObjs + gamma + 1;
    } else {
        right = gamma + 1;
    }
    
//    BVHNode node;
//    node.leftIdx = left;
//    node.rightIdx = right;
//    BVHInternals[idx] = node;
    
    return ivec2(left, right);
}

int findSplit(int first, int last) {
    uint firstCode = sortedMortonCodes[first];
    uint lastCode = sortedMortonCodes[last];
    
    if (firstCode == lastCode) {
        return (first + last) >> 1;
    }
    
    int commonPrefix = 31 - int(floor(log2(firstCode ^ lastCode)));
    
    // Use binary search to see where next bit differs
    
    int split = first;
    int step = last - first;
    
    bool doWhileFlag = true;
    
    while (doWhileFlag || step > 1) {
        doWhileFlag = false;
        step = (step + 1) >> 1;
        int newSplit = split + step;
        
        if (newSplit < last) {
            uint splitCode = sortedMortonCodes[newSplit];
            int splitPrefix = 31 - int(floor(log2(firstCode ^ splitCode)));
            if (splitPrefix > commonPrefix) {
                split = newSplit;
            }
        }
    }
    
    return split;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    ivec2 range = determineRange(idx);
    int first = range.x;
    int last = range.y;
    
    int leftOffset = 0;
    int rightOffset = 0;
    
    BVHNode currentNode = BVHInternals[idx];
    BVHNode left;
    BVHNode right;

    currentNode.leftIdx = first;
    currentNode.rightIdx = last;

    BVHInternals[idx] = currentNode;
    
    memoryBarrier();
    
    if (first > numObjs - 1) {
        left = BVHLeaves[first - (numObjs)];
        leftOffset = numObjs;
    } else {
        left = BVHInternals[first];
    }
    
    if (last > numObjs - 1) {
        right = BVHLeaves[last - (numObjs)];
        rightOffset = numObjs;
    } else {
        right = BVHInternals[last];
    }
    
    
    
    left.parentIdx = idx;
    right.parentIdx = idx;
    
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
    
    
    
//    int split = findSplit(first, last);
//    
//    int childLeftIdx;
//    int childRightIdx;
//    
//    BVHNode childLeft;
//    if (split == first) {
//        childLeft = BVHLeaves[split];
//        childLeftIdx = numObjs + split;
//    } else {
//        childLeft = BVHInternals[split];
//        childLeftIdx = split;
//    }
//    
//    BVHNode childRight;
//    if (split + 1 == last) {
//        childRight = BVHLeaves[split + 1];
//        childRightIdx = numObjs + split + 1;
//    } else {
//        childRight = BVHInternals[split + 1];
//        childRightIdx = split + 1;
//    }
//    
//    BVHInternals[idx].leftIdx = childLeftIdx;
//    BVHInternals[idx].rightIdx = childRightIdx;
//    childLeft.parentIdx = idx;
//    childRight.parentIdx = idx;
//    
//    if (split == first) {
//        BVHLeaves[split] = childLeft;
//    } else {
//        BVHInternals[split] = childLeft;
//    }
//    
//    if (split + 1 == last) {
//        BVHLeaves[split + 1] = childRight;
//    } else {
//        BVHInternals[split + 1] = childRight;
//    }
}
