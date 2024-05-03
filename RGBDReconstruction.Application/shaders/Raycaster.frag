#version 420 core

uniform mat4 viewMatrix;
uniform mat4 inverseProjectionMatrix;
uniform vec2 screenSize;

uniform mat3 intrinsicMatrix;

uniform sampler2D rgbMaps[6];
uniform sampler2D depthMaps[6];


uniform mat4 depthMapCamPoses[6];

out vec4 fragColor;
//out vec4 o_color;
//
//float distance_from_sphere(in vec3 p, in vec3 c, float r)
//{
//    return length(p - c) - r;
//}
//
//vec3 ray_march(in vec3 ro, in vec3 rd)
//{
//    float total_distance_traveled = 0.0;
//    const int NUMBER_OF_STEPS = 32;
//    const float MINIMUM_HIT_DISTANCE = 0.001;
//    const float MAXIMUM_TRACE_DISTANCE = 1000.0;
//
//    for (int i = 0; i < NUMBER_OF_STEPS; ++i)
//    {
//        vec3 current_position = ro + total_distance_traveled * rd;
//
//        float distance_to_closest = distance_from_sphere(current_position, vec3(0.0), 1.0);
//
//        if (distance_to_closest < MINIMUM_HIT_DISTANCE)
//        {
//            return vec3(1.0, 0.0, 0.0);
//        }
//
//        if (total_distance_traveled > MAXIMUM_TRACE_DISTANCE)
//        {
//            break;
//        }
//        total_distance_traveled += distance_to_closest;
//    }
//    return vec3(0.0);
//}
//
//void main()
//{
//    vec2 uv = (gl_FragCoord.xy-0.5*screenSize)/(screenSize.y);// * 2.0 - 1.0;
//
//    vec3 camera_position = vec3(0.0, 0.0, -0.0);
//    vec3 ro = (viewMatrix*vec4(camera_position,1.0)).xyz;
//    vec3 rd = normalize((viewMatrix*vec4(uv, -1.0, 0.0)).xyz);
//
//    vec3 shaded_color = ray_march(ro, rd);
//
//    o_color = vec4(shaded_color, 1.0);
//}

float unpackDepthFromRG(vec2 RG) {
    int RBits = int(RG.x*255);
    int GBits = int(RG.y*255);
    
    uint packedValue = (RBits << 8) | GBits;
    // R is the upper 8 bits and G is the lower 8 bits
//    return intBitsToFloat((RBits << 8) + (GBits));

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
    
    float result = 0.0;

    if (exponent == 0) {
        if (mantissa == 0)
        result = 0.0;
        else
        result = ldexp(float(mantissa) / 1024.0, -14); // Subnormal numbers
    } else if (exponent == 31) {
        if (mantissa == 0)
        result = (packedValue & 0x8000u) != 0 ? -1.0 / 0.0 : 1.0 / 0.0; // Infinites
        else
        result = 0.0 / 0.0; // NaNs
    } else {
        result = ldexp(float(mantissa) / 1024.0, int(exponent) - 14); // Normal numbers
    }
    return (packedValue & 0x8000u) != 0 ? -result : result; // Apply sign bit
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

    //coords.y = 1080 - coords.y;

    if (coords.x < 0 || coords.y < 0 || coords.x >= 1920 || coords.y >= 1080) {
        //outofboundsCol = 1f;
        return vec4(-1,-1,1/0f, 1/0f);
        //return vec4(0, 1, 0, 0);
    }

    float depth = unpackDepthFromRG(texture(depthMap, ivec2(coords)/vec2(1920, 1080)).rg);
    
//    if (depth > 1000) {
//        depth *= -1;
//    }

    //            if (depth > 10000) {
    //                s = 0.01f;
    //                //outofboundsCol = 1f;
    //            } else {
    //                s = (p.z - depth);
    //            }

    int signS = s == 0? 1 : int(sign(s));
    int signD = p.z - depth == 0 ? 1 : int(sign(p.z - depth));
    bool signChanged = signS != signD;
//    bool signChanged = sign(p.z - depth) != sign(s);
    float absDist;
    
    // avoid the ray from 'bouncing' back and forth from a point in which the depth map is 'in front' of the point from the depth camera's perspective
    float dist = signChanged ? -1* (p.z - depth) : (p.z - depth);
    float outDepth = depth;
    
    if (signChanged) {
//        return vec3(0,0,0);
        // See if we crossed a surface, or went through a discontinuity
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
        //float LBDepth = texture(depthMap, vec2(LBImg.x/LBImg.z, LBImg.y/LBImg.z)/vec2(1920, 1080)).r;
        float UBDepth = depth;

        for (int i = 0; i < searchLimit+1; i++) {
            avg = (UB + LB)/2f;

            newP = avg;

            newImgCoords = intrinsicMatrix * (newP).xyz;

            newCoords = vec2(newImgCoords.x/newImgCoords.z, 1080 - newImgCoords.y/newImgCoords.z);

            //coords.y = 1080 - coords.y;

            if (newCoords.x < 0 || newCoords.y < 0 || newCoords.x >= 1920 || newCoords.y >= 1080) {
                //outofboundsCol = 1f;
                break;
                //return vec3(0, 0, 0);
            }

            newDepth = unpackDepthFromRG(texture(depthMap, newCoords/vec2(1920, 1080)).rg);
            newDist = newP.z - newDepth;
            
            if (sign(UB.z - UBDepth) == sign(newDist)) {
                UB = avg;
                UBDepth = newDepth;
            } else {
                LB = avg;
            }

            if (abs(newDist) < threshold && abs(newDist) < abs(dist)) {
//                coords = vec2(0,0);
                dist = newDist;
                coords = newCoords;
                outDepth = newDepth;
//                break;
            }
            
            

//            dist = newDist * -sign(newDist);
        }
    }
    
    
    return vec4(coords, dist, outDepth);
}

vec4 raycastDepthMaps(vec3 worldRayStart, vec3 worldRayDirection) {
    float signedDistances[6];
    bool intersected[6];
    float finalDepths[6];
    vec2 finalCoords[6];
    
    bool anyIntersection = false;

    float truncDist = 0.02f;
    int maxIters = 1000;
    float smallestS = -truncDist;
    float threshold = 0.001f;
    
    vec3 currentPos = worldRayStart;
    int smallestIdx = -1;
    vec2 smallestSCoords;
    vec3 oldPos = worldRayStart;
    
    int searchLimit = int(log2(truncDist/threshold)+1);
    
    for(int i = 0; i < maxIters; i++) {
        
//        if(abs(smallestS) < threshold && smallestIdx >= 0) {
//            // TODO: Do proper colour blending!
//            vec4 pixelColour = texture(rgbMaps[smallestIdx], vec2(smallestSCoords.x, smallestSCoords.y)/vec2(1920, 1080));
//            //vec4 pixelColour = vec4(smallestSCoords/vec2(1920,1080), 0,1);
////            vec4 pixelColour = vec4(1,1,0,1);
//            return pixelColour;
//        }

        if(anyIntersection) {
            
            int smallestidx = -1;
            float smallestSD = 1/0f;
            float smallestDepth = 1/0f;
            for (int k = 0; k < 6; k++) {
                if (intersected[k] && finalDepths[k] < smallestDepth && signedDistances[k] < smallestSD) {
                    smallestidx = k;
                    smallestDepth = finalDepths[k];
                    smallestSD = signedDistances[k];
                }
            }
            
            // TODO: Do proper colour blending!
            vec4 pixelColour = texture(rgbMaps[smallestidx], vec2(finalCoords[smallestidx].x, finalCoords[smallestidx].y)/vec2(1920, 1080));
            //vec4 pixelColour = vec4(smallestSCoords/vec2(1920,1080), 0,1);
            //            vec4 pixelColour = vec4(1,1,0,1);
            return pixelColour;
        }
        
        smallestS = clamp(smallestS, -truncDist, truncDist);
        oldPos = vec3(currentPos);
        currentPos = currentPos + smallestS * 0.8f * worldRayDirection;
        
        float newSmallestS = 1/0f;
        
        // Change to number of cameras
        for (int j = 0; j < 6; j++) {
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
        
        if (newSmallestS < 1/0f) {
            smallestS = newSmallestS;
        }
    }

    return vec4(0,0,0,1);
}

//vec4 raycast(vec3 worldRayStart, vec3 worldRayDirection) {
//    vec4 rayO = depthMapCamPoses[0] * vec4(worldRayStart, 1.0f);
//    vec4 rayD = normalize(depthMapCamPoses[0] * vec4(worldRayDirection, 0.0f));
//
////    float s = 0.1f;
////    vec4 p = rayO + s*rayD;
////    vec3 imageCoords = (intrinsicMatrix * (p).xyz);
////
////    float zOffset = imageCoords.z == 0 ? 0.001f : 0f;
////
////    vec2 coords = vec2(imageCoords.x/(imageCoords.z + zOffset), imageCoords.y/(imageCoords.z + zOffset));
//    
//    //coords.y = 1080 - coords.y;
//
////    if (coords.x >= 1920 || coords.y >= 1080) {
////        return vec4(0, 1, 0, 0);
////    }
////
////    float depth = texture(depthMap, coords/vec2(1920,1080)).r;
////
////    int maxIters = 1000;
////
////    s = (p.z - depth);
//    float depth;
//    int maxIters = 1000;
//    vec2 coords;
//    vec3 imageCoords;
//    vec4 p = vec4(worldRayStart, 1.0f);
//    vec4 depthP;
//    float s = -0.02f;
//    bool signChanged = false;
//    float outofboundsCol = 0;
//    for (int i = 0; i < maxIters; i++) {
//        if (abs(s) < 0.02f) {
//            vec4 pixelColour = texture(rgbMaps[0], coords/vec2(1920, 1080));
//            return pixelColour;
////            return vec4(1f,outofboundsCol,0, 1.0f);
//        } else {
//            if (i > 0) {
//                s = clamp(s, -0.02f, 0.02f);
//            } else {
//                s = clamp(s, -0.02f, 0.02f);
//            }
//            p = p + vec4(worldRayDirection, 0f)*s*0.8f;
//            depthP = depthMapCamPoses[0] * p;
//
//            imageCoords = intrinsicMatrix * (depthP).xyz;
//
//            coords = vec2(imageCoords.x/imageCoords.z, imageCoords.y/imageCoords.z);
//
//            //coords.y = 1080 - coords.y;
//
//            if (coords.x < 0 || coords.y < 0 || coords.x >= 1920 || coords.y >= 1080) {
//                //outofboundsCol = 1f;
//                continue;
//                //return vec4(0, 1, 0, 0);
//            }
//
//            depth = texture(depthMaps[0], coords/vec2(1920, 1080)).r;
//
////            if (depth > 10000) {
////                s = 0.01f;
////                //outofboundsCol = 1f;
////            } else {
////                s = (p.z - depth);
////            }
//            
//            signChanged = sign(depthP.z - depth) != sign(s);
//            // avoid the ray from 'bouncing' back and forth from a point in which the depth map is 'in front' of the point from the depth camera's perspective
//            s = signChanged ? -1* (depthP.z - depth) : (depthP.z - depth);
//        }
//    }
////    if (abs(s) < 0.02f) {
////        return vec4(1f,outofboundsCol,0, 1.0f);
////    }
//    return vec4(0,outofboundsCol,abs(s),0);
//}

void main() {
//    if(texture(depthMap, gl_FragCoord.xy / screenSize).r >= 1.0f) {
//        fragColor = vec4(1,0,0,1);
//        return;
//    } else {
//        fragColor = vec4(0,0,0,0);
//        return;
//    }

    // Convert to normalized device coordinates
//    vec2 ndc = (gl_FragCoord.xy / screenSize) * 2.0 - 1.0;
//    //ndc.y = 1.0 - ndc.y;
//
//    // Unproject to camera space
//    vec4 rayClip = vec4(ndc, -1.0, 1.0);
//    vec4 rayCamera = inverseProjectionMatrix * rayClip;
//    rayCamera.z = -1.0; 
//    rayCamera.w = 0.0;
//
//    vec3 rayWorld = normalize((viewMatrix * rayCamera).xyz);
//    vec3 cameraPosition = -(viewMatrix * vec4(0,0,0,1)).xyz;

//    vec2 uv = (gl_FragCoord.xy-0.5*screenSize)/(screenSize.y);// * 2.0 - 1.0;
//    
//    uv.y = 1 - uv.y;
//    
//    
//
//    vec3 camera_position = vec3(0.0, 0.0, -0.0);
//    vec3 ro = (viewMatrix*vec4(camera_position,1.0)).xyz;
//    vec3 rd = normalize((viewMatrix*vec4(uv, -1.0, 0.0)).xyz);
//
//    vec4 raycastResult = raycast(ro, rd);
    
//    fragColor = vec4(texture(depthMaps[1], gl_FragCoord.xy/screenSize).xyz, 1);
//    return;
    
    vec2 ndc = (2.0 * (gl_FragCoord.xy/screenSize) - 1.0);

    float fov = 0.69111111612;//radians(45.0f);
//    float fov = radians(20.75625f); // Example FOV of 45 degrees
    float aspectRatio = screenSize.x / screenSize.y;
    float scale = tan(fov * 0.5);

    vec3 rayDirCameraSpace = vec3(
    ndc.x * scale, // Scale x by aspect ratio and FOV
    ndc.y * scale * 9/16f,               // Scale y by FOV
    -1.0                         // Assuming the camera looks towards negative z in camera space
    );
    
//    vec4 clipSpacePixelPos = vec4(ndc, -1.0f, 1.0f);
//    vec4 cameraSpacePixelPos = inverseProjectionMatrix * clipSpacePixelPos;
//    //cameraSpacePixelPos /= cameraSpacePixelPos.w;
//    //cameraSpacePixelPos.z = -1.0f;
    
    vec4 worldSpacePixelPos = viewMatrix * vec4(rayDirCameraSpace, 0.0f);
    vec3 rd = normalize(worldSpacePixelPos.xyz);
    
    vec3 ro = viewMatrix[3].xyz;
    
    //vec3 ro = (viewMatrix * vec4(0,0,0,1.0f)).xyz;
   
    
    //vec4 raycastResult = raycast(ro, rd);
    vec4 raycastResult = raycastDepthMaps(ro, rd);

    fragColor = raycastResult;
}