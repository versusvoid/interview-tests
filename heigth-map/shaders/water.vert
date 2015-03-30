#version 130
uniform mat4 MVPMatrix;

in vec4 vertexPosition;
in vec3 vertexNormal;

out vec3 position;
out vec3 normal;
out vec4 viewPosition;

void main() {
    position = vertexPosition.xyz;
    normal = vertexNormal;
    viewPosition = MVPMatrix * vertexPosition;
    gl_Position = viewPosition;
}

