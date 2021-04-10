# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeTerrainGenerator3D

func _get_name():
	return "TerrainGenerator3D"


func _get_category():
	return "TerrainTools"


func _get_description():
	return "Generate terrain (only vertex shader!)"


func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_SCALAR


func _get_input_port_count():
	return 0

func _get_output_port_count():
	return 6

func _get_output_port_name(port):
	match port:
		0:
			return "vertex"
		1:
			return "height"
		2:
			return "normal"
		3:
			return "red"
		4:
			return "green"
		5:
			return "uv"


func _get_output_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_VECTOR
		1:
			return VisualShaderNode.PORT_TYPE_SCALAR
		2:
			return VisualShaderNode.PORT_TYPE_VECTOR
		3:
			return VisualShaderNode.PORT_TYPE_SCALAR
		4:
			return VisualShaderNode.PORT_TYPE_SCALAR
		5:
			return VisualShaderNode.PORT_TYPE_VECTOR

func _get_global_code(mode):
	return """

		uniform vec4 terrainUvScale;
		uniform vec2 terrainUvOffset;
		uniform sampler2D terrainHeightMap;

		uniform float terrainChunkSize = 0;
		uniform float terrainNextLodChunkSize = 0;
		uniform float terrainNeighborLodLevel = 0;
		uniform float terrainCurrentLodLevel = 0;
		uniform bool terrainSmoothing = false;
		
		uniform sampler2D terrainSplatmap1 : hint_albedo;
		uniform sampler2D terrainSplatmap2 : hint_albedo;
		uniform bool terrainSplatMapDebug = false;

		float calculateLOD(bool _smoothing, float _currentLod, float _neighborLod, vec2 xy, vec4 morph)
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
			vec3 tangent = cross(normal, vec3(1, 0, 0));
			vec3 bitangent = cross(normal, tangent);
			return mat3(tangent, bitangent, normal);
		}

		vec4 getHeightmap(vec2 uv, vec4 uv_scale, sampler2D heightmap, float currentLODLevel){

			vec2 heightmapUVs = uv * uv_scale.xy + uv_scale.zw;
			return textureLod(heightmap, heightmapUVs, currentLODLevel);
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

			float c = clamp(dot(normalTemp, normalTemp), 0.0, 1.0);
			vec3 normal = vec3(normalTemp.x, sqrt(1.0 -c), normalTemp.y);
			bool isHole = (heightmapValue.b + heightmapValue.a) >= 1.9f;

			if (isHole)
			{
				normal = vec3(0, 1, 0);
			}
			
			normal = normalize(normal);
			mat3 tangents  = CalcTangentBasisFromWorldNormal(normal);

			return tangents[2];
		}

		vec3 getPosition(float _terrainChunkSize, float _terrainCurrentLodLevel, float _terrainNeighborLodLevel, bool _smoothing, float _terrainNextLodChunkSize, vec4 color, vec2 uv)
		{
			float lodValue = _terrainCurrentLodLevel;
			vec2 positionXZ = vec2(0,0);

			if(_smoothing)
			{		
					float lodCalculated = calculateLOD(_smoothing, _terrainCurrentLodLevel, _terrainNeighborLodLevel, uv, color);

					vec2 nextLODPos = round(uv * _terrainNextLodChunkSize) / _terrainNextLodChunkSize;
					float morphAlpha = lodCalculated - _terrainCurrentLodLevel;

					vec2 positionXZThisLOD = uv * _terrainChunkSize;
					vec2 positionXZNextLOD = nextLODPos * _terrainChunkSize;
					positionXZ = mix(positionXZThisLOD, positionXZNextLOD, morphAlpha);
			}
			else {
				positionXZ = uv * _terrainChunkSize;
			}
			
			return vec3(positionXZ.x, 0f, positionXZ.y);
		
		//	VERTEX = position;
		//	NORMAL = triangles[2];

		//	UV =  positionXZ * (1.0f / _terrainChunkSize) + OffsetUV;
		}


	"""

func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""

	heightStr = "vec4 heightMapValues = getHeightmap(UV, terrainUvScale, terrainHeightMap, terrainCurrentLodLevel);\n"
	heightStr += "float height = getHeight(heightMapValues);\n"
	heightStr += "vec3 position = getPosition(terrainChunkSize, terrainCurrentLodLevel, terrainNeighborLodLevel, terrainSmoothing, terrainNextLodChunkSize, COLOR, UV);\n"
	heightStr += "vec3 normal = getNormal(heightMapValues);\n"
	heightStr += "position.y = height;\n"

	heightStr += output_vars[0] + " = position;\n"
	heightStr += output_vars[1] + " = height;\n" 
	heightStr += output_vars[2] + " = normal;\n"
	
	heightStr += output_vars[3] + " = heightMapValues.r;\n"
	heightStr += output_vars[4] + " = heightMapValues.g;\n"
	heightStr += "vec2 newUV = vec2(position.x, position.z) * (1.0f / terrainChunkSize) + terrainUvOffset;\n"
	heightStr += output_vars[5] + " = vec3(newUV.x, newUV.y, 0.0);\n"


	return heightStr;
