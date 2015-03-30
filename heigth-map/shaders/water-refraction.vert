#version 130
uniform mat4 MVPMatrix;
uniform mat4 DepthMVPMatrix;

in vec4 vertexPosition;
in vec3 vertexNormal;

out vec3 position;
out vec3 normal;
out vec4 depthViewPosition;

void main() {
    position = vertexPosition.xyz;
    normal = vertexNormal;
    gl_Position = MVPMatrix * vertexPosition;
    depthViewPosition = DepthMVPMatrix * vertexPosition;
}

