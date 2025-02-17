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

const int MAX_ITERATIONS = 255; // Max iterations to do.
const int COLOR_CYCLES = 6;

// ------------------------- Double-Double arithmetic -------------------------
struct DD {
    float hi; // High precision part
    float lo; // Low precision part (stores rounding error)
};

DD dd_add(DD a, DD b) {
    DD result;
    result.hi = a.hi + b.hi;
    float t = result.hi - a.hi;
    result.lo = ((a.hi - (result.hi - t)) + (b.hi - t)) + a.lo + b.lo;
    return result;
}

DD dd_sub(DD a, DD b) {
    DD result;

    // Compute the main subtraction (high part)
    result.hi = a.hi - b.hi;

    // Compute the error term to compensate for lost precision
    float t = result.hi - a.hi;
    result.lo = ((a.hi - (result.hi - t)) - (b.hi + t)) + (a.lo - b.lo);

    return result;
}

DD dd_mul(DD a, DD b) {
    DD result;
    result.hi = a.hi * b.hi;
    result.lo = fma(a.hi, b.hi, -result.hi) + (a.hi * b.lo + a.lo * b.hi);
    return result;
}

DD dd_div(DD a, DD b) {
    DD result;

    // Step 1: Compute an initial estimate
    float q1 = a.hi / b.hi;

    // Step 2: Compute the remainder (error term)
    DD prod = dd_mul(DD(q1, 0.0), b); // q1 * b
    DD r = dd_add(a, DD(-prod.hi, -prod.lo)); // r = a - (q1 * b)

    // Step 3: Compute a refined quotient adjustment
    float q2 = r.hi / b.hi; // Correction factor

    // Step 4: Final result
    result.hi = q1 + q2;
    result.lo = q2 * b.lo - ((result.hi - q1) * b.hi) + r.lo;

    return result;
}

double dd_to_double(DD a) {
    return double(a.hi) + double(a.lo);
}

struct DDVec2 {
    DD x;
    DD y;
};

struct DDComplex {
    DD re;
    DD im;
};

// ------------------------- END -------------------------

// Square a complex number
DDComplex ComplexSquare(DDComplex z)
{
    return DDComplex(
        dd_sub(dd_mul(z.re, z.re), dd_mul(z.im, z.im)),
        dd_mul(dd_mul(z.re, z.im), DD(2, 0))
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
vec3 julia(DDComplex z) {
    const DDComplex c = DDComplex(DD(-0.8, 0), DD(0.156, 0));

    for (int i = 0; i < MAX_ITERATIONS; i++)
    {
        z = ComplexSquare(z);
        z = DDComplex(dd_add(z.re, c.re), dd_add(z.im, c.im));

        DD mag2 = dd_add(dd_mul(z.re, z.re), dd_mul(z.im, z.im));
        if (mag2.hi >= 4.0)
        {
            return colorize(vec2(z.re.hi, z.im.hi), i);
        }
    }
    return colorize(vec2(z.re.hi, z.im.hi), MAX_ITERATIONS-1);
}

// Main shader function.
void main() {
    vec2 uv_s = 2*fragTexCoord.xy - 1;

    const float screenRatio = screenDims.x / screenDims.y;
    uv_s.x *= screenRatio;
    uv_s.y *= -1;

    DDVec2 uv = DDVec2(
        DD(uv_s.x, 0),
        DD(uv_s.y, 0)
    );

    uv = DDVec2( // Zoom
        dd_div(uv.x, DD(zoom, 0)),
        dd_div(uv.y, DD(zoom, 0))
    );
    uv = DDVec2( // Offset
        dd_add(uv.x, DD(2*offset.x, 0)),
        dd_add(uv.y, DD(2*offset.y, 0))
    );

    // Initialize complex DD number:
    DDComplex c = DDComplex(uv.x, uv.y);

    // Iteration loop.
    vec3 color = julia(c);

    // Return grayscale color based on the iteration ratio.
    finalColor = vec4(color, 1.0);
}
