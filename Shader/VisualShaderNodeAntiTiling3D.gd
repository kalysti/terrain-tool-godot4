@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeAntiTiling3D

func _init():
	set_input_port_default_value(5, Vector3(0,0,0))
	set_input_port_default_value(6, 0.5)
	set_input_port_default_value(7, 1.0)
	set_input_port_default_value(8, 0.0)

func _get_name():
	return "AntiTiling3D"

func _get_category():
	return "TerrainTools"

func _get_description():
	return "Splatmap height reader"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_SAMPLER

func _get_input_port_count():
	return 9

func _get_output_port_count():
	return 2

func _get_output_port_name(port):
	match port:
		0:
			return "packed"
		1:
			return "packed original"

func _get_output_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_TRANSFORM
		1:
			return VisualShaderNode.PORT_TYPE_TRANSFORM

func _get_input_port_name(port):
	match port:
		0:
			return "color"
		1:
			return "displacement"
		2:
			return "normal"
		3:
			return "roughness"
		4:
			return "ao"
		5:
			return "uv"
		6:
			return "randomize"
		7:
			return "tiling"
		8:
			return "merge"


func _get_input_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_SAMPLER
		1:
			return VisualShaderNode.PORT_TYPE_SAMPLER
		2:
			return VisualShaderNode.PORT_TYPE_SAMPLER
		3:
			return VisualShaderNode.PORT_TYPE_SAMPLER
		4:
			return VisualShaderNode.PORT_TYPE_SAMPLER
		5:
			return VisualShaderNode.PORT_TYPE_VECTOR_3D
		6:
			return VisualShaderNode.PORT_TYPE_SCALAR
		7:
			return VisualShaderNode.PORT_TYPE_SCALAR
		8:
			return VisualShaderNode.PORT_TYPE_SCALAR

func _get_global_code(mode):
	return """
		float rand(vec2 input) {
			return fract(sin(dot(input.xy, vec2(12.9898,78.233))) * 43758.5453123);
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
		
	"""

func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	heightStr += "vec4 colorMap = texture("+input_vars[0]+", "+input_vars[5]+".xy);\n"
	heightStr += "vec4 dispMap = texture("+input_vars[1]+",  "+input_vars[5]+".xy);\n"
	heightStr += "vec4 normalMap = texture("+input_vars[2]+",  "+input_vars[5]+".xy);\n"
	heightStr += "vec4 roughMap = texture("+input_vars[3]+",  "+input_vars[5]+".xy);\n"
	heightStr += "vec4 aoMap = texture("+input_vars[4]+",  "+input_vars[5]+".xy);\n"

	heightStr += "mat4 packedOriginal = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n"
	heightStr +=  "packedOriginal[0].rgb = colorMap.rgb;\n"
	heightStr +=  "packedOriginal[1].r = dispMap.r;\n"
	heightStr +=  "packedOriginal[2].rg = normalMap.rg;\n"
	heightStr +=  "packedOriginal[3].r = roughMap.r;\n"
	heightStr +=  "packedOriginal[3].b = aoMap.r;\n"

	heightStr +=  output_vars[1]+" = packedOriginal;\n"

	heightStr += "vec2 newUv = rotatedUV("+input_vars[5]+".xy, "+input_vars[7]+", "+input_vars[6]+");"
	heightStr += "vec4 colorMapRot = texture("+input_vars[0]+", newUv);\n"
	heightStr += "vec4 dispMapRot = texture("+input_vars[1]+",  newUv);\n"
	heightStr += "vec4 normalMapRot = texture("+input_vars[2]+",  newUv);\n"
	heightStr += "vec4 roughMapRot = texture("+input_vars[3]+",  newUv);\n"
	heightStr += "vec4 aoMapRot = texture("+input_vars[4]+",  newUv);\n"

	heightStr += "mat4 packedMixed = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n"
	heightStr +=  "packedMixed[0].rgb = colorMapRot.rgb;\n"
	heightStr +=  "packedMixed[1].r = dispMapRot.r;\n"
	heightStr +=  "packedMixed[2].rg = normalMapRot.rg;\n"
	heightStr +=  "packedMixed[3].r = roughMapRot.r;\n"
	heightStr +=  "packedMixed[3].b = aoMapRot.r;\n"

	heightStr +=  "packedMixed[0] = mix(packedOriginal[0], packedMixed[0],  "+input_vars[8]+");\n"
	heightStr +=  "packedMixed[1] = mix(packedOriginal[1], packedMixed[1],  "+input_vars[8]+");\n"
	heightStr +=  "packedMixed[2] = mix(packedOriginal[2], packedMixed[2],  "+input_vars[8]+");\n"
	heightStr +=  "packedMixed[3] = mix(packedOriginal[3], packedMixed[3],  "+input_vars[8]+");\n"

	heightStr +=  output_vars[1]+" = packedOriginal;\n"
	heightStr +=  output_vars[0]+" = packedMixed;\n"

	return heightStr;
