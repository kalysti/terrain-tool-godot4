# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeUVMultiply3D

func _get_name():
	return "UVMultiply3D"


func _get_category():
	return "TerrainTools"


func _get_description():
	return "Default Terrain Material"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_SCALAR

func _init():
	set_input_port_default_value(0, Vector3(1.0, 1.0, 0.0))

func _get_input_port_name(port):
	return "scale"

func _get_input_port_type(port):
	return VisualShaderNode.PORT_TYPE_VECTOR_3D

func _get_input_port_count():
	return 1

func _get_output_port_count():
	return 1
	
func _get_output_port_name(port):
	return "packed"

func _get_output_port_type(port):
	return VisualShaderNode.PORT_TYPE_VECTOR_3D

func _get_global_code(mode):
	return """
	
	"""

func _get_code(input_vars, output_vars, mode, type):

	var heightStr = "vec2 scaled = UV * vec2("+input_vars[0]+".x, "+input_vars[0]+".y);\n"
	heightStr +=  output_vars[0]+" = vec3(scaled.x, scaled.y, 0.0f);\n"

	return heightStr;
