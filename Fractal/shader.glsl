#version 460

// Input vertex attributes (from vertex shader)
in vec2 fragTexCoord;
in vec4 fragColor;

// Output fragment color
out vec4 finalColor;

uniform float iTime;
uniform vec2 screenDims; // Dimensions of the screen
uniform vec3 palette[7];
uniform float zoom;
uniform vec2 offset;
uniform vec2 c;
uniform int useDouble;

const int MAX_ITERATIONS = 255; // Max iterations to do.
const int COLOR_CYCLES = 6;

// Square a complex number
vec2 complex_square(vec2 z)
{
    return vec2(
    z.x * z.x - z.y * z.y,
    z.x * z.y * 2.0
    );
}
dvec2 complex_square_double(dvec2 z)
{
    return dvec2(
    z.x * z.x - z.y * z.y,
    z.x * z.y * 2.0
    );
}

vec3 colorize(vec2 z, int i) {
    float log_mag = log(pow(z.x, 2) + pow(z.y, 2)) / 2;
    float nu = log(log_mag / log(2)) / log(2);
    float corrected_i = float(i) + 1 - nu;

    float r = corrected_i / MAX_ITERATIONS * COLOR_CYCLES * (palette.length() - 1);
    vec3 color1 = palette[int(mod(floor(r), palette.length()-1))];
    vec3 color2 = palette[int(mod(floor(r), palette.length()-1)) + 1];

    return color1 + fract(r) * (color2 - color1);
}

// Julia loop using complex DD arithmetic.
vec3 julia(vec2 z) {
    for (int i = 0; i < MAX_ITERATIONS; i++)
    {
        z = complex_square(z) + c;
        if (z.x*z.x + z.y*z.y >= 4.0)
        {
            return colorize(z, i);
        }
    }
    return colorize(z, MAX_ITERATIONS);
}

// Julia loop using complex DD arithmetic.
vec3 julia_double(dvec2 z) {
    for (int i = 0; i < MAX_ITERATIONS; i++)
    {
        z = complex_square_double(z) + c;
        if (z.x*z.x + z.y*z.y >= 4.0)
        {
            return colorize(vec2(z), i);
        }
    }
    return colorize(vec2(z), MAX_ITERATIONS-1);
}

// Main shader function.
void main() {
    vec2 uv = 2*fragTexCoord.xy - 1;

    const float screenRatio = screenDims.x / screenDims.y;
    uv.x *= screenRatio;
    uv.y *= -1;

    if (useDouble == 1) {
        dvec2 uv_d = dvec2(uv);
        uv_d /= zoom;
        uv_d += offset * 2;

        // Initialize complex DD numbers:
        dvec2 c = uv_d; // c from pixel coordinate

        // Iteration loop.
        vec3 color = julia_double(c);

        // Return grayscale color based on the iteration ratio.
        finalColor = vec4(color, 1.0);
    } else {
        uv /= zoom;
        uv += offset * 2;

        // Initialize complex DD numbers:
        vec2 c = uv; // c from pixel coordinate

        // Iteration loop.
        vec3 color = julia(c);

        // Return grayscale color based on the iteration ratio.
        finalColor = vec4(color, 1.0);
    }
}
