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

float minMagnitude(highp float f1, highp float f2) {
    highp float t1 = f1 < 0 ? f1 * -1 : f1;
    highp float t2 = f2 < 0 ? f2 * -1 : f2;

    if (t1 < t2) {
        return f1;
    }

    return f2;
}

vec3 marchRayDepthMap(vec3 worldPos, mat4 depthMapPose, sampler2D depthMap, sampler2D rgbTexture, float s) {
    vec4 p = depthMapPose * vec4(worldPos, 1.0);

    vec3 imageCoords = intrinsicMatrix * (p).xyz;

    vec2 coords = vec2(imageCoords.x/imageCoords.z, imageCoords.y/imageCoords.z);

    //coords.y = 1080 - coords.y;

    if (coords.x < 0 || coords.y < 0 || coords.x >= 1920 || coords.y >= 1080) {
        //outofboundsCol = 1f;
        return vec3(-1,-1,1/0f);
        //return vec4(0, 1, 0, 0);
    }

    float depth = texture(depthMap, coords/vec2(1920, 1080)).r;

    //            if (depth > 10000) {
    //                s = 0.01f;
    //                //outofboundsCol = 1f;
    //            } else {
    //                s = (p.z - depth);
    //            }

    bool signChanged = sign(p.z - depth) != sign(s);
    // avoid the ray from 'bouncing' back and forth from a point in which the depth map is 'in front' of the point from the depth camera's perspective
    float dist = signChanged ? -1* (p.z - depth) : (p.z - depth);
    return vec3(coords, dist);
}

vec4 raycastDepthMaps(vec3 worldRayStart, vec3 worldRayDirection) {
    float signedDistances[6];

    float truncDist = 0.02f;
    int maxIters = 1000;
    float smallestS = -truncDist;
    
    vec3 currentPos = worldRayStart;
    int smallestIdx = -1;
    vec2 smallestSCoords;
    
    for(int i = 0; i < maxIters; i++) {
        
        if(abs(smallestS) < 5e-3 && smallestIdx >= 0) {
            // TODO: Do proper colour blending!
            vec4 pixelColour = texture(rgbMaps[smallestIdx], smallestSCoords/vec2(1920, 1080));
            //vec4 pixelColour = vec4(smallestSCoords/vec2(1920,1080), 0,1);
            //vec4 col = vec4(1,1,0,1);
            return pixelColour;
        }
        
        smallestS = clamp(smallestS, -truncDist, truncDist);
        currentPos = currentPos + smallestS * 0.8f * worldRayDirection;
        
        float newSmallestS = 1/0f;
        
        // Change to number of cameras
        for (int j = 0; j < 6; j++) {
            vec3 marchResults = marchRayDepthMap(currentPos, depthMapCamPoses[j], depthMaps[j], rgbMaps[j], smallestS);
            signedDistances[j] = marchResults.z;
            newSmallestS = minMagnitude(newSmallestS, signedDistances[j]);
            if (newSmallestS > signedDistances[j] - 1e-4 && newSmallestS < signedDistances[j] + 1e-4 && newSmallestS < 1/0f) {
                smallestIdx = j;
                smallestSCoords = marchResults.xy;
            }
        }
        
        if (newSmallestS < 1/0f) {
            smallestS = newSmallestS;
        }
    }

    return vec4(0,1,1,1);
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
    
    vec2 ndc = (2.0 * (gl_FragCoord.xy/screenSize) - 1.0);

    float fov = radians(45.0); // Example FOV of 45 degrees
    float aspectRatio = screenSize.x / screenSize.y;
    float scale = tan(fov * 0.5);

    vec3 rayDirCameraSpace = vec3(
    ndc.x * scale * aspectRatio, // Scale x by aspect ratio and FOV
    ndc.y * scale,               // Scale y by FOV
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

    if (raycastResult.w > 0.1f) {
        fragColor = raycastResult; // Intersection color
    } else {
        fragColor = vec4(raycastResult.xyz, 1); // No intersection color
    }
}