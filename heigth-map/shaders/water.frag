#version 130
const vec3 AbovewaterColor = vec3(0.25, 1.0, 1.25);
const vec3 LightColor = vec3(1.0);
const vec2 RefractionBumpMagnitude = 0.1*vec2(1);

uniform float Alpha;
uniform float RefractionMagnitude;
uniform float NormalBumpMagnitude;
uniform vec3 Eye;
uniform vec3 Ambient;
uniform vec3 LightPosition;
uniform float ConstantAttenuation; 
uniform float LinearAttenuation;
uniform float QuadraticAttenuation;
uniform sampler2D Refraction;
uniform sampler2D NormalMap;
uniform vec3 NormalMapCoordinatesShift1;
uniform vec3 NormalMapCoordinatesShift2;
uniform vec3 NormalMapCoordinatesShift3;
uniform vec3 NormalMapCoordinatesShift4;

in vec3 position;
in vec3 normal;
in vec4 viewPosition;


/*
 * All functions before main() implements minimal raytracing. 
 * Such approach is possible due simplicity of overall scene,
 * do not try use it in production.
 */
uniform sampler2D HeightMatrix;
uniform vec2 Delta;
uniform vec2 ZRange;
vec3 intersect(vec3 lowerBound, vec3 upperBound, 
        vec3 rayOrigin, vec3 invRayDirection, bvec3 sign) {

    float side = -1.0;
    float tmin, tmax;
    if (sign.x) {
        side *= -1.0;
        tmin = (upperBound.x - rayOrigin.x) * invRayDirection.x;
        tmax = (lowerBound.x - rayOrigin.x) * invRayDirection.x;
    } else {
        tmin = (lowerBound.x - rayOrigin.x) * invRayDirection.x;
        tmax = (upperBound.x - rayOrigin.x) * invRayDirection.x;
    }
    
    float tymin, tymax;
    if (sign.y) {
        tymin = (upperBound.y - rayOrigin.y) * invRayDirection.y;
        tymax = (lowerBound.y - rayOrigin.y) * invRayDirection.y;
    } else {
        tymin = (lowerBound.y - rayOrigin.y) * invRayDirection.y;
        tymax = (upperBound.y - rayOrigin.y) * invRayDirection.y;
    }

    if (tmin > tymax || tymin > tmax) {
        return vec3(0.0);
    }

    if (tymin > tmin) {
        side = -2.0;
        if (sign.y) side *= -1.0;
        tmin = tymin;
    }
    if (tymax < tmax) {
        tmax = tymax;
    }

    float tzmin, tzmax;
    if (sign.z) {
        tzmin = (upperBound.z - rayOrigin.z) * invRayDirection.z;
        tzmax = (lowerBound.z - rayOrigin.z) * invRayDirection.z;
    } else {
        tzmin = (lowerBound.z - rayOrigin.z) * invRayDirection.z;
        tzmax = (upperBound.z - rayOrigin.z) * invRayDirection.z;
    }
    
    if (tmin > tzmax || tzmin > tmax) {
        return vec3(0.0);
    }
    
    if (tzmin > tmin) {
        side = 4.0;
        tmin = tzmin;
    }
    if (tzmax < tmax) {
        tmax = tzmax;
    }

    if (tmin <= 0.0) {
        return vec3(0.0);
    } else {
        return vec3(tmin, side, 1.0);
    }
}

vec3 getMatrixColor(vec3 position, float side) {
    vec3 normal;
    if (abs(side) == 1.0) {
        normal = vec3(1.0, 0.0, 0.0)*sign(side);
    } else if (abs(side) == 2.0) {
        normal = vec3(0.0, 1.0, 0.0)*sign(side);
    } else {
        normal = vec3(0.0, 0.0, 1.0);
    }

    vec3 lightDirection = LightPosition - position;
    float lightDistance = length(lightDirection);

    lightDirection = lightDirection / lightDistance;

    float attenuation = 1.0 /
        (ConstantAttenuation +
           LinearAttenuation * lightDistance +
        QuadraticAttenuation * lightDistance * lightDistance);

    float diffuse = max(0.0, dot(normal, lightDirection));

    vec3 scatteredLight = Ambient + LightColor * diffuse * attenuation;
    vec3 rgbColor = min(vec3(0.3, 0.5, 0.7) * scatteredLight, vec3(1.0));

    return rgbColor;
}

vec3 nextTexturePosition(vec3 position, vec3 ray) {
    float dx = 0.00001, dy = 0.00001;
    if (ray.y < 0) {
        dy += position.y - floor(position.y / Delta.y) * Delta.y;
    } else {
        dy += ceil(position.y / Delta.y) * Delta.y - position.y;
    }

    if (ray.x < 0) {
        dx += position.x - floor(position.x / Delta.x) * Delta.x;
    } else {
        dx += ceil(position.x / Delta.x) * Delta.x - position.x;
    }

    return position + ray * min(abs(dy/ray.y), abs(dx/ray.x));

}
vec3 getReflectedColor(vec3 reflectedRay) {

    vec3 reflectedColor = AbovewaterColor;
     
    if (reflectedRay.x == 0.0 && reflectedRay.y == 0.0) {
        return reflectedColor;
    }
    
    vec3 invReflectedRay = 1.0 / reflectedRay;
    bvec3 sign = lessThan(invReflectedRay, vec3(0.0));

    vec3 currentTexturePosition = nextTexturePosition(position, reflectedRay);
    while(min(currentTexturePosition.x, currentTexturePosition.y) > 0.0 && max(currentTexturePosition.x, currentTexturePosition.y) < 1.0 
            && currentTexturePosition.z > ZRange.x && currentTexturePosition.z < ZRange.y) {
        vec3 lowerBound = vec3(Delta * floor(currentTexturePosition.xy / Delta), ZRange.r);
        vec2 z = texture2D(HeightMatrix, currentTexturePosition.xy).rg;
        vec3 upperBound = vec3(lowerBound.xy + Delta, ZRange.r + z.r + z.g);

        vec3 intersection = intersect(lowerBound, upperBound, position, invReflectedRay, sign);
        if (intersection.b > 0.0) {
            vec3 hit = position + reflectedRay*intersection.r;
            if (hit.z > z.r + 0.0001) {
                reflectedColor = AbovewaterColor;
            } else {
                reflectedColor = getMatrixColor(hit, intersection.g);
            }
            break; 
        }

        currentTexturePosition = nextTexturePosition(currentTexturePosition, reflectedRay);
    }
      
    return reflectedColor;
}




void main() {
    vec2 projectedCoordinates = viewPosition.xy / viewPosition.w;
    projectedCoordinates = (projectedCoordinates + 1.0) * 0.5;

    // Four different waves over normal map
    vec3 shiftedPosition1 = 4.0 * position + NormalMapCoordinatesShift1;
    vec3 shiftedPosition2 = 4.0 * position + NormalMapCoordinatesShift2;
    vec3 shiftedPosition3 = 4.0 * position + NormalMapCoordinatesShift3;
    vec3 shiftedPosition4 = 4.0 * position + NormalMapCoordinatesShift4;
    vec2 normalMapCoordinates1, 
         normalMapCoordinates2, 
         normalMapCoordinates3, 
         normalMapCoordinates4;
    if (normal.z != 0.0) {
        normalMapCoordinates1 = shiftedPosition1.xy;
        normalMapCoordinates2 = shiftedPosition2.xy;
        normalMapCoordinates3 = shiftedPosition3.xy;
        normalMapCoordinates4 = shiftedPosition4.xy;
    } else if (normal.y != 0.0) {
        normalMapCoordinates1 = shiftedPosition1.xz;
        normalMapCoordinates2 = shiftedPosition2.xz;
        normalMapCoordinates3 = shiftedPosition3.xz;
        normalMapCoordinates4 = shiftedPosition4.xz;
    } else {
        normalMapCoordinates1 = shiftedPosition1.yz;
        normalMapCoordinates2 = shiftedPosition2.yz;
        normalMapCoordinates3 = shiftedPosition3.yz;
        normalMapCoordinates4 = shiftedPosition4.yz;
    }


    vec3 unbumpedColor = texture2D(Refraction, projectedCoordinates).rgb;

    vec3 bump = 0.5 * (
            texture2D(NormalMap, normalMapCoordinates1).rgb +
            texture2D(NormalMap, normalMapCoordinates2).rgb +
            texture2D(NormalMap, normalMapCoordinates3).rgb +
            texture2D(NormalMap, normalMapCoordinates4).rgb) - 1.0;

    if (normal.z != 0.0) {
        // Rotate normal map to viewer. 
        // Because rotated at 90 degrees it looks awfuly unrealistic.
        vec2 angles = normalize(vec2(0.5) - Eye.yx);
        mat2x2 transform = mat2x2(vec2(angles.x, angles.y), vec2(-angles.y, angles.x));
        bump.xy = transform*bump.xy;
    }

    vec3 normal_ = normalize(normal + bump * NormalBumpMagnitude);
    projectedCoordinates += bump.xy * RefractionBumpMagnitude;
    vec4 bumpedColor = texture2D(Refraction, projectedCoordinates).rgba;
    bumpedColor /= bumpedColor.a;

    vec3 refractedColor = (unbumpedColor * (1.0 - bumpedColor.a) + bumpedColor.rgb * bumpedColor.a) * RefractionMagnitude;
    refractedColor *= AbovewaterColor;

    vec3 incomingRay = normalize(position - Eye);
    vec3 reflectedRay = reflect(incomingRay, normal_);
    vec3 reflectedColor = getReflectedColor(reflectedRay);
    // Sun highlight
    reflectedColor += vec3(pow(max(0.0, dot(normalize(LightPosition - position), normalize(reflectedRay))), 5000.0)) * vec3(10.0, 8.0, 6.0);

    float fresnel = mix(0.25, 1.0, pow(1.0 - dot(normal_, -incomingRay), 3.0));

    vec3 result = mix(refractedColor, reflectedColor, fresnel);
    gl_FragColor = vec4(result, Alpha);
}
