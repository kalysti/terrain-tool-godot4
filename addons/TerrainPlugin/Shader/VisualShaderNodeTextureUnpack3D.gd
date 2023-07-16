# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeTextureUnpack3D

func _get_name():
	return "TextureUnpack3D"


func _get_category():
	return "TerrainTools"


func _get_description():
	return "Default Terrain Material"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_TRANSFORM

func _init():
	set_input_port_default_value(0, Transform3D())

func _get_input_port_name(port):
	match port:
		0:
			return "packed"

func _get_input_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_TRANSFORM

func _get_input_port_count():
	return 1

func _get_output_port_count():
	return 5
	
func _get_output_port_name(port):
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

func _get_output_port_type(port):
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

func _get_global_code(mode):
	return """
	
	"""


func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	heightStr +=  output_vars[0]+" = "+input_vars[0]+"[0].rgb;\n"
	heightStr +=  output_vars[1]+" = "+input_vars[0]+"[1].r;\n"
	heightStr +=  output_vars[2]+" = vec3("+input_vars[0]+"[2].r, "+input_vars[0]+"[2].g, "+input_vars[0]+"[2].b);\n"
	heightStr +=  output_vars[3]+" = "+input_vars[0]+"[3].r;\n"
	heightStr +=  output_vars[4]+" = "+input_vars[0]+"[3].b;\n"

	return heightStr;
