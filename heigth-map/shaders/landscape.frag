#version 130
const vec3 LightColor = vec3(1.0);

uniform vec2 Delta;

uniform vec3 Ambient;
uniform vec3 LightPosition;

uniform float ConstantAttenuation; 
uniform float LinearAttenuation;
uniform float QuadraticAttenuation;

flat in vec4 color;
in vec3 normal;
in vec4 position;

void main() {
    vec3 lightDirection = LightPosition - position.xyz;
    float lightDistance = length(lightDirection);

    lightDirection = lightDirection / lightDistance;

    float attenuation = 1.0 /
        (ConstantAttenuation +
           LinearAttenuation * lightDistance +
        QuadraticAttenuation * lightDistance * lightDistance);

    float diffuse = max(0.0, dot(normal, lightDirection));

    vec3 scatteredLight = Ambient + LightColor * diffuse * attenuation;
    vec3 rgbColor = min(color.rgb * scatteredLight, vec3(1.0)) + 10.2*noise3(position.xyz);

    gl_FragColor = vec4(rgbColor, color.a);
}

