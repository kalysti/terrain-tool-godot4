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
	return 8

func _get_output_port_name(port):
	match port:
		0:
			return "vertex"
		1:
			return "height"
		2:
			return "normal"
		3:
			return "tangent"
		4:
			return "bionormal"
		5:
			return "red"
		6:
			return "green"
		7:
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
			return VisualShaderNode.PORT_TYPE_VECTOR
		4:
			return VisualShaderNode.PORT_TYPE_VECTOR
		5:
			return VisualShaderNode.PORT_TYPE_SCALAR
		6:
			return VisualShaderNode.PORT_TYPE_SCALAR
		7:
			return VisualShaderNode.PORT_TYPE_VECTOR

func _get_global_code(mode):
	return """

		uniform vec4 terrainUvScale;
		uniform vec2 terrainUvOffset;
		uniform sampler2D terrainHeightMap;

		uniform float terrainChunkSize = 0;
		uniform float terrainNextLodChunkSize = 0;
		uniform vec4 terrainNeighborLod = vec4(0,0,0,0);
		uniform float terrainCurrentLodLevel = 0;
		uniform bool terrainSmoothing = false;
		
		uniform sampler2D terrainSplatmap1 : hint_albedo;
		uniform sampler2D terrainSplatmap2 : hint_albedo;
		uniform bool terrainSplatMapDebug = false;

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

		mat3 CalcTangentBasisFromWorldNormal(vec3 normaltest)
		{
			vec3 tangenttest = cross(normaltest, vec3(1, 0, 0));
			vec3 bitangenttest = cross(normaltest, tangenttest);
			return mat3(tangenttest, bitangenttest, normaltest);
		}

		float getHeight(vec4 heightmapValue)
		{
			int heightR = int((heightmapValue.x * 255.0));
			int heightG = int((heightmapValue.y * 255.0)) << 8;

			int sum = heightR + heightG;
			return float(sum) / 65535.0;
		}

		mat3 getNormal(vec4 heightmapValue)
		{
			vec2 normalTemp = vec2(heightmapValue.b, heightmapValue.a) * 2.0f - 1.0f;

			float c = clamp(dot(normalTemp, normalTemp), 0.0, 1.0);
			vec3 normaltest = vec3(normalTemp.x, sqrt(1.0 -c), normalTemp.y);
			bool isHole = (heightmapValue.b + heightmapValue.a) >= 1.9f;
			normaltest = normalize(normaltest);

			if (isHole)
			{
				normaltest = vec3(0, 1, 0);
			}
			
			mat3 tangents  = CalcTangentBasisFromWorldNormal(normaltest);
			return tangents;
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
			
			return vec3(positionXZ.x, 0f, positionXZ.y);
	
		}

		vec4 getHeightmap(vec2 uv, bool _smoothing, vec4 uv_scale, sampler2D heightmap, float morphAlpha, float _terrainNextLodChunkSize, float _currentLODLevel){

			vec2 heightmapUVs = uv * uv_scale.xy + uv_scale.zw;

			if(_smoothing)
			{

				vec4 heightmapValueThisLOD = textureLod( heightmap, heightmapUVs, _currentLODLevel);
				vec2 nextLODPos = round(uv * _terrainNextLodChunkSize) / _terrainNextLodChunkSize;
				vec2 heightmapUVsNextLOD = nextLODPos * uv_scale.xy + uv_scale.zw;
				vec4 heightmapValueNextLOD = textureLod( heightmap, heightmapUVsNextLOD, _currentLODLevel + 1f);
				vec4 heightmapValue = mix(heightmapValueThisLOD, heightmapValueNextLOD, morphAlpha);

				return heightmapValue;
			}
			else {
				return textureLod(heightmap, heightmapUVs, _currentLODLevel);
			}
		}


	"""

func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	heightStr += "float lodCalculated = calculateLOD(terrainSmoothing, terrainCurrentLodLevel, terrainNeighborLod, UV, COLOR);";
	heightStr += "float lodValue = terrainCurrentLodLevel;";
	heightStr += "float morphAlpha = lodCalculated - terrainCurrentLodLevel;";
	
	heightStr += "vec4 heightMapValues = getHeightmap(UV, terrainSmoothing, terrainUvScale, terrainHeightMap, morphAlpha,  terrainNextLodChunkSize,  terrainCurrentLodLevel);\n"
	heightStr += "float height = getHeight(heightMapValues);\n"
	heightStr += "vec3 position = getPosition(UV, terrainChunkSize, terrainCurrentLodLevel, terrainSmoothing, terrainNextLodChunkSize, lodCalculated);\n"
	heightStr += "mat3 calculatedNormal = getNormal(heightMapValues);\n"
	heightStr += "position.y = height;\n"

	heightStr += output_vars[0] + " = position;\n"
	heightStr += output_vars[1] + " = height;\n" 
	heightStr += output_vars[2] + " = calculatedNormal[2];\n"
	heightStr += output_vars[3] + " = calculatedNormal[0];\n"
	heightStr += output_vars[4] + " = calculatedNormal[1];\n"

	heightStr += output_vars[5] + " = heightMapValues.r;\n"
	heightStr += output_vars[6] + " = heightMapValues.g;\n"
	heightStr += "vec2 newUV = vec2(position.x, position.z) * (1.0f / terrainChunkSize) + terrainUvOffset;\n"
	heightStr += output_vars[7] + " = vec3(newUV.x, newUV.y, 0.0);\n"


	return heightStr;
