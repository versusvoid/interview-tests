#version 130

in vec2 color;
void main() {
    gl_FragColor = vec4(color, 0.0, 1.0);
}
