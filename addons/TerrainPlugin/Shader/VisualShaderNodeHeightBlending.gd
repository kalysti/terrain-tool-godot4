# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeHeightBlending

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
	return 0

func _get_output_port_name(port):
	return "layer"+str(port)

func _get_output_port_type(port):
	return VisualShaderNode.PORT_TYPE_SCALAR

func _get_global_code(mode):
	return """

	"""


func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	if type == 1:
		heightStr = ""

	return heightStr;
