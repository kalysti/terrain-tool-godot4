shader_type spatial;

float rand(vec2 input)
{
    return fract(sin(dot(input.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

vec2 rotatedUV(vec2 uv, float tiling, float randomize_rotation)
{
    vec2 tiled_UV_raw = uv * tiling;
    vec2 tiled_UV = fract(tiled_UV_raw) - 0.5f;

    vec2 unique_val = floor(uv * tiling) / tiling;
    float rotation = (rand(unique_val) * 2.0f - 1.0f) * randomize_rotation * 3.14f;
    float cosine = cos(rotation);
    float sine = sin(rotation);
    mat2 rotation_mat = mat2(vec2(cosine, -sine), vec2(sine, cosine));
    return rotation_mat * tiled_UV + 0.5f;
}