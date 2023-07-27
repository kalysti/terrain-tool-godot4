shader_type spatial;

vec4 _GetSplatmap(vec2 uv, float _terrainChunkSize, vec4 uv_scale, sampler2D heightmap, float currentLODLevel){
    vec2 heightmapUVs = uv * uv_scale.xy + uv_scale.zw;

    float currentChunkSize = (_terrainChunkSize / 100.0f + 1.0f) * 4.0f;
    float extraPolate = 0.5f / currentChunkSize;
    heightmapUVs = heightmapUVs + vec2(extraPolate, extraPolate);

    return textureLod(heightmap, heightmapUVs, currentLODLevel);
}