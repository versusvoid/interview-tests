#version 130
uniform mat4 MVPMatrix;
uniform vec2 Dimensions;

in vec4 vertexPosition;
in vec2 vertexIndexInMatrix;

out vec2 color;

void main() {
    gl_Position = MVPMatrix * vertexPosition;
    color = vertexIndexInMatrix / Dimensions;
}
