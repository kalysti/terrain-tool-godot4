# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeSplatmapHeight3D

func _get_name():
	return "SplatmapHeight3D"


func _get_category():
	return "TerrainTools"


func _get_description():
	return "Splatmap height reader"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_SCALAR

func _get_input_port_count():
	return 0

func _get_output_port_count():
	return 8

func _get_output_port_name(port):
	return "layer"+str(port)

func _get_output_port_type(port):
	return VisualShaderNode.PORT_TYPE_SCALAR

func _get_global_code(mode):
	return """

		vec4 getSplatmap(vec2 uv, float _terrainChunkSize, vec4 uv_scale, sampler2D heightmap, float currentLODLevel){
			vec2 heightmapUVs = uv * uv_scale.xy + uv_scale.zw;

			float currentChunkSize = (_terrainChunkSize / 100.0f + 1.0f) * 4.0f;
			float extraPolate = 0.5f / currentChunkSize;
			heightmapUVs = heightmapUVs + vec2(extraPolate, extraPolate);

			return textureLod(heightmap, heightmapUVs, currentLODLevel);
		}
		
	"""


func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	if type == 1:
		heightStr = "vec4 splatmap1Values = getSplatmap(UV, terrainChunkSize, terrainUvScale, terrainSplatmap1, terrainCurrentLodLevel);\n"
		heightStr += "vec4 splatmap2Values = getSplatmap(UV, terrainChunkSize,  terrainUvScale, terrainSplatmap2, terrainCurrentLodLevel);\n"

		heightStr += output_vars[0] + " = splatmap1Values.r;\n"
		heightStr += output_vars[1] + " = splatmap1Values.g;\n"
		heightStr += output_vars[2] + " = splatmap1Values.b;\n"
		heightStr += output_vars[3] + " = splatmap1Values.a;\n"
		
		heightStr += output_vars[4] + " = splatmap2Values.r;\n"
		heightStr += output_vars[5] + " = splatmap2Values.g;\n"
		heightStr += output_vars[6] + " = splatmap2Values.b;\n"
		heightStr += output_vars[7] + " = splatmap2Values.a;\n"

	return heightStr;
