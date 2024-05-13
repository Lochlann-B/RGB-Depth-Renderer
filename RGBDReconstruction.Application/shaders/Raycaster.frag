#version 420 core

uniform mat4 viewMatrix;
uniform mat4 inverseProjectionMatrix;
uniform vec2 screenSize;

uniform mat3 intrinsicMatrix;

// Change to number of cameras - GLSL forces array declaration to have constant size at compile-time.
uniform sampler2D rgbMaps[4];
uniform sampler2D depthMaps[4];


uniform mat4 depthMapCamPoses[4];

out vec4 fragColor;

float unpackDepthFromRG(vec2 RG) {
    int RBits = int(RG.x*255);
    int GBits = int(RG.y*255);
    
    uint packedValue = (RBits << 8) | GBits;

    uint exponent = (packedValue >> 10) & 0x1Fu;
    uint mantissa = packedValue & 0x3FFu;
    
    int exp_real;
    int sign = packedValue >> 15 == 1 ? 1 : 1;
    float mant_real = mantissa/1024.0f;
    
    if (exponent == 0) {
        exp_real = -14;
    } else {
        mant_real = mant_real + 1;
        exp_real = int(exponent) - 15;
    }
    
    return sign*ldexp(mant_real, exp_real);
}

float minMagnitude(highp float f1, highp float f2) {
    highp float t1 = f1 < 0 ? f1 * -1 : f1;
    highp float t2 = f2 < 0 ? f2 * -1 : f2;

    if (t1 < t2) {
        return f1;
    }

    return f2;
}

vec4 marchRayDepthMap(vec3 worldPos, mat4 depthMapPose, sampler2D depthMap, sampler2D rgbTexture, float s, vec3 oldPos, float threshold, int searchLimit) {
    vec4 p = depthMapPose * vec4(worldPos, 1.0);

    vec3 imageCoords = intrinsicMatrix * (p).xyz;

    vec2 coords = vec2(imageCoords.x/imageCoords.z, 1080 - imageCoords.y/imageCoords.z);

    if (coords.x < 0 || coords.y < 0 || coords.x >= 1920 || coords.y >= 1080) {
        return vec4(-1,-1,1/0f, 1/0f);
    }

    float depth = unpackDepthFromRG(texture(depthMap, ivec2(coords)/vec2(1920, 1080)).rg);///10f;


    int signS = s == 0? 1 : int(sign(s));
    int signD = p.z - depth == 0 ? 1 : int(sign(p.z - depth));
    bool signChanged = signS != signD;

    float absDist;
    
    // avoid the ray from 'bouncing' back and forth from a point in which the depth map is 'in front' of the point from the depth camera's perspective
    float dist = signChanged ? -1* (p.z - depth) : (p.z - depth);
    float outDepth = depth;
    
    if (signChanged) {
        // Search for precise root
        absDist = abs(p.z - depth);

        vec3 LB = (depthMapPose*vec4(oldPos, 1.0f)).xyz;
        vec3 UB = p.xyz;

        vec3 newP;
        vec3 avg;
        vec2 newCoords;
        vec3 newImgCoords;
        float newDepth;
        float newDist;

        vec3 LBImg = intrinsicMatrix*LB;
        
        float UBDepth = depth;

        for (int i = 0; i < searchLimit+1; i++) {
            avg = (UB + LB)/2f;

            newP = avg;

            newImgCoords = intrinsicMatrix * (newP).xyz;

            newCoords = vec2(newImgCoords.x/newImgCoords.z, 1080 - newImgCoords.y/newImgCoords.z);
            

            if (newCoords.x < 0 || newCoords.y < 0 || newCoords.x >= 1920 || newCoords.y >= 1080) {
                break;
            }

            newDepth = unpackDepthFromRG(texture(depthMap, newCoords/vec2(1920, 1080)).rg);///10f;
            newDist = newP.z - newDepth;

            if (sign(UB.z - UBDepth) == sign(newDist)) {
                UB = avg;
                UBDepth = newDepth;
            } else {
                LB = avg;
            }

            if (abs(newDist) < threshold && abs(newDist) < abs(dist)) {
                dist = newDist;
                coords = newCoords;
                outDepth = newDepth;
            }
        }
    }
    
    
    return vec4(coords, dist, outDepth);
}

vec4 raycastDepthMaps(vec3 worldRayStart, vec3 worldRayDirection) {
    // Change to number of cameras
    float signedDistances[4];
    bool intersected[4];
    float finalDepths[4];
    vec2 finalCoords[4];
    
    bool anyIntersection = false;

    float truncDist = 0.02f;
    float truncDistMultiplier = 1f;
    bool inVoid = false;
    int maxIters = 500;
    float smallestS = -truncDist;
    float threshold = 0.001f;
    
    vec3 currentPos = worldRayStart;
    int smallestIdx = -1;
    vec2 smallestSCoords;
    vec3 oldPos = worldRayStart;
    
    int searchLimit = int(log2(truncDist/threshold)+1);
    
    for(int i = 0; i < maxIters; i++) {
        
        if(anyIntersection) {
            int smallestidx = -1;
            float smallestSD = 1/0f;
            float smallestDepth = 1/0f;
            
            // Change to number of cameras in scene
            for (int k = 0; k < 4; k+=1) {
                if (intersected[k] && finalDepths[k] < smallestDepth && signedDistances[k] < smallestSD) {
                    smallestidx = k;
                    smallestDepth = finalDepths[k];
                    smallestSD = signedDistances[k];
                }
            }
            
            vec4 pixelColour = texture(rgbMaps[smallestidx], vec2(finalCoords[smallestidx].x, finalCoords[smallestidx].y)/vec2(1920, 1080));
            return pixelColour;
        }
        
        if (inVoid) {
            smallestS = truncDistMultiplier*0.1f;
        } else {
            smallestS = clamp(smallestS, -truncDist, truncDist);
        }
        oldPos = vec3(currentPos);
        currentPos = currentPos + smallestS * 0.8f * worldRayDirection;
        
        float newSmallestS = 1/0f;
        
        // Change to number of cameras
        for (int j = 0; j < 4; j+=1) {
            vec4 marchResults = marchRayDepthMap(currentPos, depthMapCamPoses[j], depthMaps[j], rgbMaps[j], smallestS, oldPos, threshold, searchLimit);
            signedDistances[j] = marchResults.z;
            
            if (abs(signedDistances[j]) < threshold) {
                anyIntersection = true;
                intersected[j] = true;
                finalDepths[j] = marchResults.w;
                finalCoords[j] = marchResults.xy;
            } else {
                intersected[j] = false;
            }
            
            newSmallestS = minMagnitude(newSmallestS, signedDistances[j]);
            if (newSmallestS == signedDistances[j] && newSmallestS != 1/0f) {
                smallestIdx = j;
                smallestSCoords = marchResults.xy;
            }
        }
        
        if (abs(newSmallestS) < 3f) {
            smallestS = newSmallestS;
            inVoid = false;
        } else { 
            inVoid = true;
            truncDistMultiplier = newSmallestS > 65000 ? -1 : sign(newSmallestS);
            
        }
    }

    return vec4(0,0,0,1);
}

void main() {
    
    vec2 ndc = (2.0 * (gl_FragCoord.xy/screenSize) - 1.0);

    float fov = 0.69111111612;

    float aspectRatio = screenSize.x / screenSize.y;
    float scale = tan(fov * 0.5);

    vec3 rayDirCameraSpace = vec3(
    ndc.x * scale, 
    ndc.y * scale * 9/16f,    
    -1.0  
    );
    
    vec4 worldSpacePixelPos = viewMatrix * vec4(rayDirCameraSpace, 0.0f);
    vec3 rd = normalize(worldSpacePixelPos.xyz);
    
    vec3 ro = viewMatrix[3].xyz;
    
    vec4 raycastResult = raycastDepthMaps(ro, rd);

    fragColor = raycastResult;
}