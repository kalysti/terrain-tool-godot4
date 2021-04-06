shader_type spatial;
render_mode  unshaded, depth_draw_always, cull_disabled;

uniform sampler2D heigtmap ;
uniform vec4 uv_scale;
uniform float TerrainChunkSizeLOD0;
uniform float ChunkSizeNextLOD;
uniform bool useSmotthlod =false;
uniform float CurrentLOD;
uniform float NeighborLOD;
uniform vec2 OffsetUV;

varying vec4 currentHeight;
varying float currentHeightMeters;
varying vec3 colorMap ;



float CalcLOD(vec2 xy, vec4 morph)
{
	
if(useSmotthlod)
{
	// Use LOD value based on Barycentric coordinates to morph to the lower LOD near chunk edges
	vec4 lodCalculated = morph * CurrentLOD + NeighborLOD * (vec4(1, 1, 1, 1) - morph);

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
	return CurrentLOD;
}
mat3 CalcTangentBasisFromWorldNormal(vec3 normal)
{
	vec3 tangent = cross(normal, vec3(1, 0, 0));
	vec3 bitangent = cross(normal, tangent);
	return mat3(tangent, bitangent, normal);
}

void vertex()
{
		//calculate lod
		float lodCalculated = CalcLOD(UV, COLOR);
		float lodValue = CurrentLOD;
		float morphAlpha = lodCalculated - CurrentLOD;
        vec2 nextLODPos = round(UV * ChunkSizeNextLOD) / ChunkSizeNextLOD;
		
		vec4 uvtest = uv_scale;
		
		//sample heightmapuvtest
		vec2 heightmapUVs = UV * uvtest.xy + uvtest.zw;
		
		vec4 heightmapValue = textureLod(heigtmap, heightmapUVs, lodValue); // + lodValue
		
		uint heightR = uint(heightmapValue.x* 255.0);
		uint heightG = uint(heightmapValue.y* 255.0) * uint(256);
		uint sum = heightR + heightG;
		
	    float height = float(sum ) / 65535.0;
		
		vec2 normalTemp = vec2(heightmapValue.b, heightmapValue.a) * 2.0f - 1.0f;
		float c = clamp(dot(normalTemp, normalTemp), 0.0, 1.0);
	    vec3 normal = vec3(normalTemp.x, sqrt(1.0 -c), normalTemp.y);
		
		currentHeightMeters = height;
		
		bool isHole = (heightmapValue.b + heightmapValue.a) >= 1.9f;
		if (isHole)
		{
			normal = vec3(0, 1, 0);
		}
		
		normal = normalize(normal);
		
		vec2 positionXZ = vec2(0,0);
		if(useSmotthlod)
		{
				vec2 positionXZThisLOD = UV* TerrainChunkSizeLOD0;
				vec2 positionXZNextLOD = nextLODPos * TerrainChunkSizeLOD0;
				positionXZ = mix(positionXZThisLOD, positionXZNextLOD, morphAlpha);
		}
		else {
			positionXZ = UV * TerrainChunkSizeLOD0;
		}
		
		vec3 position = vec3(positionXZ.x, height, positionXZ.y);
		mat3 triangles = CalcTangentBasisFromWorldNormal(normal);
		
		vec3 wp = (vec4(position, 1) * WORLD_MATRIX).xyz;
	
		VERTEX = position;
		//NORMAL = triangles[2];
		//UV =  positionXZ * (1.0f / TerrainChunkSizeLOD0) + OffsetUV;
}

void fragment()
{

	ALBEDO = vec3(0, currentHeightMeters , 0);
	ALPHA = 1.0f;
}
