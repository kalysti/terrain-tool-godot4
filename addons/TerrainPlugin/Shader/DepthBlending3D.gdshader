shader_type spatial;

vec3 HeightBlend(float blending, vec3 input1, float height1, vec3 input2, float height2)
{
    float height_start = max(height1, height2) - blending;
    float b1 = max(height1 - height_start, 0.0f);
    float b2 = max(height2 - height_start, 0.0f);
    return ((input1 * b1) + (input2 * b2)) / (b1 + b2);
}

vec3 HeightLerp(float blending, vec3 input1, float height1, vec3 input2, float height2, float lerp)
{
    return HeightBlend(blending, input1, height1 * (1.0f - lerp), input2, height2 * lerp);
}