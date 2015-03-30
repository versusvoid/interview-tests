#version 130
const vec4 VertexColor = vec4(0.3, 0.5, 0.7, 1.0);

uniform sampler2D DepthMap;
uniform mat4 MVPMatrix;
uniform mat4 DepthMVPMatrix;
uniform vec2 SelectedLandscapeCell;

in vec4 vertexPosition;
in vec3 vertexNormal;
in vec2 vertexIndexInMatrix;

flat out vec4 color;
out vec3 normal;
out vec4 position;

float gl_ClipDistance[1];

void main() {
    if (vertexIndexInMatrix == SelectedLandscapeCell) {
        color = vec4(1.0, 0.0, 0.0, 1.0);
    } else {
        color = VertexColor;
    }
    normal = vertexNormal;
    position = vertexPosition;

    vec4 depthViewPosition = DepthMVPMatrix * vertexPosition;
    depthViewPosition /= depthViewPosition.w;
    gl_ClipDistance[0] = texture2D(DepthMap, depthViewPosition.xy).r - depthViewPosition.z;

    gl_Position = MVPMatrix * (vertexPosition + noise4(vertexPosition));
}

