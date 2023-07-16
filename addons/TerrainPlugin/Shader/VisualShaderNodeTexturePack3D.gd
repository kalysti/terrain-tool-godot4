# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeTexturePack3D

func _get_name():
	return "TexturePack3D"


func _get_category():
	return "TerrainTools"


func _get_description():
	return "Default Terrain Material"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_TRANSFORM

func _init():
	set_input_port_default_value(0, Vector3(0.0, 0.0, 0.0))
	set_input_port_default_value(1, 0.0)
	set_input_port_default_value(2, Vector3(0.0, 0.0, 0.0))
	set_input_port_default_value(3, Vector3(0.0, 0.0, 0.0))
	set_input_port_default_value(4, 0.0)

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

func _get_input_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_VECTOR_3D
		1:
			return VisualShaderNode.PORT_TYPE_SCALAR
		2:
			return VisualShaderNode.PORT_TYPE_VECTOR_3D
		3:
			return VisualShaderNode.PORT_TYPE_SCALAR
		4:
			return VisualShaderNode.PORT_TYPE_SCALAR

func _get_input_port_count():
	return 5

func _get_output_port_count():
	return 1
	
func _get_output_port_name(port):
	return "packed"

func _get_output_port_type(port):
	return VisualShaderNode.PORT_TYPE_TRANSFORM

func _get_global_code(mode):
	return """
	
	"""

func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	heightStr = "mat4 packed = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n"
	heightStr +=  "packed[0].rgb =  "+input_vars[0]+".rgb;\n"
	heightStr +=  "packed[1].r =  "+input_vars[1]+";\n"
	heightStr +=  "packed[2].rgb =  "+input_vars[2]+".rgb;\n"
	heightStr +=  "packed[3].r =  "+input_vars[3]+";\n"
	heightStr +=  "packed[3].b =  "+input_vars[4]+";\n"
	heightStr +=  output_vars[0]+" = packed;\n"

	return heightStr;
