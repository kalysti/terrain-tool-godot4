shader_type spatial;
render_mode depth_draw_always;

uniform sampler2D DefaultTexture;
uniform sampler2D DefaultNormal : hint_normal;

uniform sampler2D heigtmap ;
uniform vec4 uv_scale;
uniform float TerrainChunkSizeLOD0;
uniform float ChunkSizeNextLOD;
uniform bool useSmotthlod =false;
uniform float CurrentLOD;
uniform float NeighborLOD;
uniform vec2 OffsetUV;
varying vec4 currentHeight;
varying vec3 colorMap ;

//inspector vars
uniform vec4 BrushData0 ;
uniform vec4 BrushData1 ;
uniform vec4 Color : hint_color;

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
		currentHeight = heightmapValue;
		int heightR = int((heightmapValue.x * 255.0));
		int heightG = int((heightmapValue.y * 255.0)) << 8;

		int sum = heightR + heightG;
		
	    float height = float(sum) / 65535.0;
		//float height = (float)((int)(heightmapValue.x * 255.0) + ((int)(heightmapValue.y * 255) << 8)) / 65535.0;

		vec2 normalTemp = vec2(heightmapValue.b, heightmapValue.a) * 2.0f - 1.0f;
		float c = clamp(dot(normalTemp, normalTemp), 0.0, 1.0);
	    vec3 normal = vec3(normalTemp.x, sqrt(1.0 -c), normalTemp.y);

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
		NORMAL = triangles[2];
	    UV =  positionXZ * (1.0f / TerrainChunkSizeLOD0) + OffsetUV;
}

vec3 genInspectorHighlite(mat4 CAMMATRIX, vec3 vertexLocal)
{
	
	// ScalarFloat:12
	float n_out12p0 = 0.000000;

// Input:6
	mat4 n_out6p0 =  CAMMATRIX;

// Input:7
	vec3 n_out7p0 = vertexLocal;

// TransformVectorMult:8
	vec3 n_out8p0 = (n_out6p0 * vec4(n_out7p0, 1.0)).xyz;

// ColorUniform:2
	vec3 n_out2p0 = BrushData0.rgb;
	float n_out2p1 = BrushData0.a;

// Distance:4
	float n_out4p0 = distance(n_out8p0, n_out2p0);

// ColorUniform:3
	vec3 n_out3p0 = BrushData1.rgb;
	float n_out3p1 = BrushData1.a;

// VectorDecompose:10
	float n_out10p0 = n_out3p0.x;
	float n_out10p1 = n_out3p0.y;
	float n_out10p2 = n_out3p0.z;

// Expression:9
	float n_out9p0;
	n_out9p0 = 0.0;
	{
		float dist = n_out4p0;
		float radius = n_out2p1;
		float falloff = n_out10p0;
		float falloffType = n_out10p1;
		
		// Output0 = brush intensity
		n_out9p0 = 0f;
		
		if (dist < radius)
		{
			n_out9p0 = 1f;
		}
		else if (dist < radius + falloff)
		{
			float valueLinear = mix(1, 0, (dist - radius) / falloff);
			if (falloffType == 0f) // Smooth
				n_out9p0 = valueLinear * valueLinear * (3f - 2f * valueLinear);
			else if (falloffType == 1f) // Linear
				n_out9p0 = valueLinear;
			else if (falloffType == 2f) // Spherical
				n_out9p0 = sqrt(1.0f - ((dist - radius) / falloff) * (dist - radius) / falloff);
			else if (falloffType == 3f) // Tip
				n_out9p0 = 1.0f - sqrt(1.0f - ((falloff + radius - dist) / falloff) * (falloff + radius - dist) / falloff);
		}
		
	}

// Mix:11
	float n_in11p1 = 0.20000;
	float n_out11p0 = mix(n_out12p0, n_in11p1, n_out9p0);

// Color:16
	vec3 n_out16p0 = vec3(0.968750, 1.000000, 0.000000);
	float n_out16p1 = 1.000000;

// Expression:15
	float n_out15p0;
	n_out15p0 = 0f;
	{
		float Width = 7.0f;
		if (abs(n_out4p0) < Width || abs(n_out4p0 - n_out2p1) < Width)
		    n_out15p0 = 1f;
		else if (abs(n_out4p0 - n_out2p1 - n_out10p0) < Width)
		    n_out15p0 = 1f;
		else
		    n_out15p0 = 0f;
		
	}

// Mix:19
	float n_out19p0 = mix(n_out11p0, n_out16p1, n_out15p0);

// ColorUniform:13
	vec3 n_out13p0 = Color.rgb;
	float n_out13p1 = Color.a;

// FloatOp:20
	float n_out20p0 = n_out19p0 * n_out13p1;

// VectorCompose:18
	vec3 n_out18p0 = vec3(n_out15p0, n_out15p0, n_out15p0);

// Mix:17
	vec3 n_out17p0 = mix(n_out13p0, n_out16p0, n_out18p0);

// VectorCompose:22
	vec3 n_out22p0 = vec3(n_out13p1, n_out13p1, n_out13p1);

// VectorOp:21
	vec3 n_out21p0 = n_out17p0 * n_out22p0;
	
	return mix(vec3(0f,0f,0f), n_out21p0, n_out20p0);
}

void fragment()
{
	ALBEDO = vec3(currentHeight.r, currentHeight.g, 0f);
	ALPHA = 1.0f;
	EMISSION = genInspectorHighlite(CAMERA_MATRIX, VERTEX);
}
