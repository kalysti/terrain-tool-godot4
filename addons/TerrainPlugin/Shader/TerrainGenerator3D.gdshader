shader_type spatial;
uniform vec4 terrainUvScale;
uniform vec2 terrainUvOffset;
uniform sampler2D terrainHeightMap : filter_linear, repeat_disable;

uniform float terrainChunkSize = 0;
uniform float terrainNextLodChunkSize = 0;
uniform vec4 terrainNeighborLod = vec4(0,0,0,0);
uniform float terrainCurrentLodLevel = 0;
uniform bool terrainSmoothing = false;

uniform sampler2D terrainSplatmap1 : filter_linear, repeat_disable;
uniform sampler2D terrainSplatmap2 : filter_linear, repeat_disable;

float calculateLOD(bool _smoothing, float _currentLod, vec4 _neighborLod, vec2 xy, vec4 morph)
{
    if(_smoothing)
    {
        // Use LOD value based on Barycentric coordinates to morph to the lower LOD near chunk edges
        vec4 lodCalculated = morph * _currentLod + _neighborLod * (vec4(1, 1, 1, 1) - morph);

        // Pick a quadrant (top, left, right or bottom)
        float lod;
        if ((xy.x + xy.y) > 1.0)
        {
            if (xy.x < xy.y)
            {
                lod = lodCalculated.w;
            }
            else
            {
                lod = lodCalculated.z;
            }
        }
        else
        {
            if (xy.x < xy.y)
            {
                lod = lodCalculated.y;
            }
            else
            {
                lod = lodCalculated.x;
            }
        }

        return lod;
    }
    else
        return _currentLod;
}

mat3 CalcTangentBasisFromWorldNormal(vec3 normal)
{
    vec3 tangenttest = cross(normal, vec3(1, 0, 0));
    vec3 bitangenttest = cross(normal, tangenttest);
    return mat3(tangenttest, bitangenttest, normal);
}

float getHeight(vec4 heightmapValue)
{
    int heightR = int((heightmapValue.x * 255.0));
    int heightG = int((heightmapValue.y * 255.0)) << 8;

    int sum = heightR + heightG;
    return float(sum) / 65535.0;
}

vec3 getNormal(vec4 heightmapValue)
{
    vec2 normalTemp = vec2(heightmapValue.b, heightmapValue.a) * 2.0f - 1.0f;

    float yNormalSaturated = clamp(dot(normalTemp, normalTemp), 0.0, 1.0);
    vec3 normal = vec3(normalTemp.x, sqrt(1.0 - yNormalSaturated), normalTemp.y);
    bool isHole = (heightmapValue.b + heightmapValue.a) >= 1.9f;
    normal = normalize(normal);

    if (isHole)
    {
        normal = vec3(0, 1, 0);
    }

    return normal;
}

vec3 getPosition(vec2 uv, float _terrainChunkSize, float _terrainCurrentLodLevel, bool _smoothing, float _terrainNextLodChunkSize, float lodCalculated)
{
    float lodValue = _terrainCurrentLodLevel;
    vec2 positionXZ = vec2(0,0);

    if(_smoothing)
    {		
            vec2 nextLODPos = round(uv * _terrainNextLodChunkSize) / _terrainNextLodChunkSize;
            float morphAlpha = lodCalculated - _terrainCurrentLodLevel;

            vec2 positionXZThisLOD = uv * _terrainChunkSize;
            vec2 positionXZNextLOD = nextLODPos * _terrainChunkSize;
            positionXZ = mix(positionXZThisLOD, positionXZNextLOD, morphAlpha);
    }
    else {
        positionXZ = uv * _terrainChunkSize;
    }
    
    return vec3(positionXZ.x, 0.0f, positionXZ.y);

}

vec4 getHeightmap(vec2 uv, float _terrainChunkSize,  bool _smoothing, vec4 uv_scale, sampler2D heightmap, float morphAlpha, float _terrainNextLodChunkSize, float _currentLODLevel){

    vec2 heightmapUVs = uv * uv_scale.xy + uv_scale.zw;

    if(_smoothing)
    {

        //vec4 heightmapValueThisLOD = textureLod( heightmap, heightmapUVs, _currentLODLevel);
        vec4 heightmapValueThisLOD = texture( heightmap, heightmapUVs);
        vec2 nextLODPos = round(uv * _terrainNextLodChunkSize) / _terrainNextLodChunkSize;
        vec2 heightmapUVsNextLOD = nextLODPos * uv_scale.xy + uv_scale.zw;
        vec4 heightmapValueNextLOD = textureLod( heightmap, heightmapUVsNextLOD, _currentLODLevel + 1.0f);
        vec4 heightmapValue = mix(heightmapValueThisLOD, heightmapValueNextLOD, morphAlpha);

        return heightmapValue;
    }
    else {
        return textureLod(heightmap, heightmapUVs, _currentLODLevel);
    }
}

mat3 RemoveScaleFromLocalToWorld(mat3 localToWorld)
{
    //localToWorld[0] *= WorldInvScale.x;
    //localToWorld[1] *= WorldInvScale.y;
    //localToWorld[2] *= WorldInvScale.z;

    return localToWorld;
}

mat3 CalcTangentToWorld(mat4 world, mat3 tangentToLocal)
{
    mat3 localToWorld = RemoveScaleFromLocalToWorld(mat3(world));
    return  tangentToLocal * localToWorld; 
}