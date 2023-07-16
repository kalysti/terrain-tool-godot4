# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeDepthBlending3D

func _init():
	set_input_port_default_value(0, Transform3D())
	set_input_port_default_value(1, Transform3D())
	set_input_port_default_value(2, 0.5)
	set_input_port_default_value(3, 0.05)
	set_input_port_default_value(4, false)

func _get_name():
	return "DepthBlending3D"

func _get_category():
	return "TerrainTools"


func _get_description():
	return "Splatmap height reader"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_TRANSFORM

func _get_input_port_count():
	return 5

func _get_output_port_count():
	return 1

func _get_output_port_name(port):
	return "mixed"

func _get_output_port_type(port):
	return VisualShaderNode.PORT_TYPE_TRANSFORM

func _get_input_port_name(port):
	match port:
		0:
			return "top packed"
		1:
			return "bottom packed"
		2:
			return "mix"
		3:
			return "blending"
		4:
			return "grayscale"


func _get_input_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_TRANSFORM
		1:
			return VisualShaderNode.PORT_TYPE_TRANSFORM
		2:
			return VisualShaderNode.PORT_TYPE_SCALAR
		3:
			return VisualShaderNode.PORT_TYPE_SCALAR
		4:
			return VisualShaderNode.PORT_TYPE_BOOLEAN

func _get_global_code(mode):
	return """

		vec3 HeightBlend(float blending, vec3 input1, float height1, vec3 input2, float height2)
		{
			float height_start = max(height1, height2) - blending;
			float b1 = max(height1 - height_start, 0.0f);
			float b2 = max(height2 - height_start, 0.0f);
			return ((input1 * b1) + (input2 * b2)) / (b1 + b2);
		}

		vec3 HeightLerp(float blending, vec3 input1, float height1, vec3 input2, float height2, float lerp)
		{
			return HeightBlend(blending, input1, height1 * (1.0f - lerp), input2, height2 * lerp);
		}

	"""

func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""

	heightStr = "vec3 tex1 = "+input_vars[0]+"[0].rgb;\n"
	heightStr += "vec3 tex2 = "+input_vars[1]+"[0].rgb;\n"

	heightStr += "float avg1 = (tex1.r + tex1.g + tex1.b) / 3.0f;\n"
	heightStr += "float avg2 = (tex2.r + tex2.g + tex2.b) / 3.0f;\n"

	heightStr += "vec3 bump1 = "+input_vars[0]+"[2].rgb;\n"
	heightStr += "vec3 bump2 = "+input_vars[1]+"[2].rgb;\n"

	heightStr += "bump1.xy = bump1.xy / bump1.z;\n"
	heightStr += "bump2.xy = bump2.xy / bump2.z;\n"
	
	heightStr += "bump1.z = 1.0;\n"
	heightStr += "bump2.z = 1.0;\n"

	heightStr += "if("+input_vars[4]+" == false) { \n"
	heightStr += "	avg1 = "+input_vars[0]+"[1].r;\n"
	heightStr += "	avg2 = "+input_vars[1]+"[1].r;\n"
	heightStr += "}\n"

	heightStr += "mat4 result = mat4(vec4(0.0f,0.0f,0.0f,0.0f),vec4(0.0f,0.0f,0.0f,0.0f),vec4(0.0f,0.0f,0.0f,0.0f),vec4(0.0f,0.0f,0.0f,0.0f));\n"
	heightStr += "result[0].rgb = HeightLerp("+input_vars[3]+", "+input_vars[0]+"[0].rgb, avg1, "+input_vars[1]+"[0].rgb, avg2, "+input_vars[2]+");\n"
	heightStr += "result[1].rgb = HeightLerp("+input_vars[3]+", "+input_vars[0]+"[1].rgb, avg1, "+input_vars[1]+"[1].rgb, avg2, "+input_vars[2]+");\n"
	heightStr += "result[2].rgb = mix( "+input_vars[0]+"[2].rgb,  "+input_vars[1]+"[2].rgb, "+input_vars[2]+");\n"
	#needs to finish it
	#heightStr += "result[2].rgb =  normalize((mix(bump1, bump2, "+input_vars[2]+") * 2.0) - 1.0);\n"
	heightStr += "result[3].rgb = HeightLerp("+input_vars[3]+", "+input_vars[0]+"[3].rgb, avg1, "+input_vars[1]+"[3].rgb, avg2, "+input_vars[2]+");\n"
	heightStr +=  output_vars[0]+" = result;\n"

	return heightStr;
