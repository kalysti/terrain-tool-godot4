shader_type spatial;
render_mode  cull_disabled, depth_draw_always, unshaded;
uniform sampler2D heigtmap;
uniform vec4 uv_scale;
uniform float TerrainChunkSizeLOD0;
uniform float ChunkSizeNextLOD;
uniform bool useSmotthlod = false;
uniform float CurrentLOD;
uniform float NeighborLOD;
uniform vec2 OffsetUV;

varying vec4 currentHeight;

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
		
		//sample heightmap
		vec2 heightmapUVs = UV * uv_scale.xy + uv_scale.zw;
		
		vec4 heightmapValue = texture(heigtmap, heightmapUVs); // + lodValue
	    float height = float((int((heightmapValue.x * 255.0)) + (int(heightmapValue.y * 255.0)) << 8)) ;
		vec2 normalTemp = vec2(heightmapValue.b, heightmapValue.a) * 2.0f - 1.0f;
		float c = clamp(dot(normalTemp, normalTemp), 0.0, 1.0);
	    vec3 normal = vec3(normalTemp.x, sqrt(1.0 -c), normalTemp.y);
		
		currentHeight = heightmapValue;
		
		bool isHole = (heightmapValue.b + heightmapValue.a) >= 1.9f;
		if (isHole)
		{
			normal = vec3(0, 1, 0);
		}
		
		//COLOR = vec4(1,1,1,0);
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
		
        //VERTEX = ( vec4(wp, 1) * PROJECTION_MATRIX).xyz;
	 // VERTEX.y += cos(VERTEX.x * 4.0) * sin(VERTEX.z * 4.0);
		VERTEX = position;

	    UV =  positionXZ * (1.0f / TerrainChunkSizeLOD0) + OffsetUV;
		NORMAL = triangles[2];
}

void fragment()
{
	ALBEDO = vec3(currentHeight.r,currentHeight.g, 0f);
	ALPHA = 1.0f;
}
