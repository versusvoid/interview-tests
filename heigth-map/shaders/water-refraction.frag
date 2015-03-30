#version 130
const vec3 AbovewaterColor = vec3(0.25, 1.0, 1.25);

uniform float Alpha;
uniform vec3 Eye;
uniform vec3 LightPosition;
uniform sampler2DShadow DepthMap;

in vec3 position;
in vec3 normal;
in vec4 depthViewPosition;

void main() {
    vec3 refractedColor = vec3(0.0);

    vec3 reflectedColor = AbovewaterColor;
    vec3 incomingRay = normalize(position - Eye);
    vec3 reflectedRay = reflect(incomingRay, normal);
    // Sun highlight
    reflectedColor += vec3(pow(max(0.0, dot(normalize(LightPosition - position), reflectedRay)), 500.0)) * vec3(10.0, 8.0, 6.0);

    float fresnel = mix(0.25, 1.0, pow(1.0 - dot(normal, -incomingRay), 3.0));

    float alpha = Alpha * textureProj(DepthMap, depthViewPosition);

    gl_FragColor = vec4(mix(refractedColor, reflectedColor, fresnel), alpha);
}

