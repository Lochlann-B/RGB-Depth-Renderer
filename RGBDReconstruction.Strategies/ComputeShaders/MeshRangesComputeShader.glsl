#version 450

layout(local_size_x = 256) in; 

layout(std430, binding = 0) buffer InputBuffer {
    float elements[];
} inBuf;

layout(std430, binding = 1) buffer OutputBufferx {
    float resultsx[];
} outBufx;

layout(std430, binding = 2) buffer OutputBuffery {
    float resultsy[];
} outBufy;

layout(std430, binding = 3) buffer OutputBufferz {
    float resultsz[];
} outBufz;

layout (std430, binding = 4) buffer OutputBufferAll {
    float results[];
} outBuf;

shared float sharedMinx[256];
shared float sharedMaxx[256];

shared float sharedMiny[256];
shared float sharedMaxy[256];

shared float sharedMinz[256];
shared float sharedMaxz[256];

void main() {
    uint localId = gl_LocalInvocationID.x;
    uint globalId = gl_GlobalInvocationID.x;

    // Load input elements into shared memory, initializing min and max with the first element.
    if (globalId < inBuf.elements.length()) {
        sharedMinx[localId] = inBuf.elements[3*globalId];
        sharedMaxx[localId] = inBuf.elements[3*globalId];
        
        sharedMiny[localId] = inBuf.elements[3*globalId+1];
        sharedMaxy[localId] = inBuf.elements[3*globalId+1];

        sharedMinz[localId] = inBuf.elements[3*globalId+2];
        sharedMaxz[localId] = inBuf.elements[3*globalId+2];
    } else {
        sharedMinx[localId] = float(1f/0f);
        sharedMaxx[localId] = float(-1f/0f);
        
        sharedMiny[localId] = float(1f/0f);
        sharedMaxy[localId] = float(-1f/0f);

        sharedMinz[localId] = float(1f/0f);
        sharedMaxz[localId] = float(-1f/0f);
    }
    
    barrier();

    // Perform parallel reduction within the workgroup to find min and max.
    for (uint stride = 1; stride < gl_WorkGroupSize.x; stride *= 2) {
        uint index = 2 * stride * localId;

        if (index < gl_WorkGroupSize.x) {
            sharedMinx[index] = min(sharedMinx[index], sharedMinx[index + stride]);
            sharedMaxx[index] = max(sharedMaxx[index], sharedMaxx[index + stride]);

            sharedMiny[index] = min(sharedMiny[index], sharedMiny[index + stride]);
            sharedMaxy[index] = max(sharedMaxy[index], sharedMaxy[index + stride]);

            sharedMinz[index] = min(sharedMinz[index], sharedMinz[index + stride]);
            sharedMaxz[index] = max(sharedMaxz[index], sharedMaxz[index + stride]);
        }

        barrier();
    }
    
    if (localId == 0) {
        outBufx.resultsx[gl_WorkGroupID.x * 2] = sharedMinx[0];
        outBufx.resultsx[gl_WorkGroupID.x * 2 + 1] = sharedMaxx[0];

        outBufy.resultsy[gl_WorkGroupID.x * 2] = sharedMiny[0];
        outBufy.resultsy[gl_WorkGroupID.x * 2 + 1] = sharedMaxy[0];

        outBufz.resultsz[gl_WorkGroupID.x * 2] = sharedMinz[0];
        outBufz.resultsz[gl_WorkGroupID.x * 2 + 1] = sharedMaxz[0];
        
        outBuf.results[6 * gl_WorkGroupID.x] = sharedMinx[0];
        outBuf.results[6 * gl_WorkGroupID.x + 3] = sharedMaxx[0];
        outBuf.results[6 * gl_WorkGroupID.x + 1] = sharedMiny[0];
        outBuf.results[6 * gl_WorkGroupID.x + 4] = sharedMaxy[0];
        outBuf.results[6 * gl_WorkGroupID.x + 2] = sharedMinz[0];
        outBuf.results[6 * gl_WorkGroupID.x + 5] = sharedMaxz[0];
    }
}
