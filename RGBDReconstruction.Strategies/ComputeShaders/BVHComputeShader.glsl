#version 450

layout (local_size_x = 1) in;

struct BVHNode {
    vec3 minPoint;
    vec3 maxPoint;
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

uniform int[] sortedMortonCodes[];

uniform int numObjs;

int delta(int i, int j, int[] k) {
    // Return longest common prefixes of binary digits between 32-bit integers i and j
    return 31 - int(floor(log2(k[i] ^ k[j])));
}

vec2i determineRange(int[] sortedMortonCodes, int numObjs, int idx) {
    int[] k = sortedMortonCodes;
    int i = idx;
    
    // Determine direction of range (+1 for right box, -1 for left box)
    int d = sign(delta(i, i+1, k) - delta(i, i-1, k));
    
    // Compute upper bound for length of range
    int delMin = delta(i, i-d, k);
    int lMax = 2;
    
    while (delta(i, i + lMax*d, k) > delMin) {
        lMax *= 2;
    }

    // Find the other end using binary search
    int l = 0;
    for (int t = lMax/2; t >= 1; t /= 2) {
        if (delta(i, i + (l + t)*d, k) > delMin) {
            l += t;
        }
    }
    int j = i + l*d;

    // Find split position using binary search
    int delNode = delta(i, j, k);
    int s = 0;
    for (int t = int(ceil(l/2)); t >= 1; t = int(ceil(t/2))) {
        if (delta(i, i + (s+t)*d, k) > delNode) {
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
        
        left = n + gamma;
    } else {
        left = gamma;
    }
    if (max(i,j) == gamma + 1) {
        // Same but for the upper bound
        right = n + gamma + 1;
    } else {
        right = gamma + 1;
    }
    
    BVHNode node;
    node.leftIdx = left;
    node.rightIdx = right;
    BVHInternals[idx] = node;
    
    return vec2i(left, right);
}

int findSplit(int[] sortedMortonCodes, int first, int last) {
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

void addTriangleToBVH(vec3 v1, vec3 v2, vec3 v3) {
    
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    vec2i range = determineRange(sortedMortonCodes, numObjs, idx);
    int first = range.x;
    int last = range.y;
    
    int split = findSplit(sortedMortonCodes, first, last);
    
    BVHNode childLeft;
    if (split == first) {
        childLeft = BVHLeaves[split];
    } else {
        childLeft = BVHInternals[split];
    }
    
    BVHNode childRight;
    if (split + 1 == last) {
        childRight = BVHLeaves[split + 1];
    } else {
        childRight = BVHInternals[split + 1];
    }
    
    BVHInternals[idx].leftIdx = childLeft;
    BVHInternals[idx].rightIdx = childRight;
    childLeft.parentIdx = idx;
    childRight.parentIdx = idx;
    
    if (split == first) {
        BVHLeaves[split] = childLeft;
    } else {
        BVHInternals[split] = childLeft;
    }
    
    if (split + 1 == last) {
        BVHLeaves[split + 1] = childRight;
    } else {
        BVHInternals[split + 1] = childRight;
    }
}
