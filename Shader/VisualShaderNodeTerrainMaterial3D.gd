# PerlinNoise3D.gd
@tool

extends VisualShaderNodeCustom
class_name VisualShaderNodeTerrainMaterial3D

func _get_name():
	return "TerrainMaterial3D"


func _get_category():
	return "TerrainTools"


func _get_description():
	return "Default Terrain Material"

func _get_return_icon_type():
	return VisualShaderNode.PORT_TYPE_SCALAR

func _get_input_port_count():
	return 0

func _get_output_port_count():
	return 2
	
func _get_output_port_name(port):
	match port:
		0:
			return "emission"
		1:
			return "albedo"

func _get_output_port_type(port):
	match port:
		0:
			return VisualShaderNode.PORT_TYPE_VECTOR_3D
		1:
			return VisualShaderNode.PORT_TYPE_VECTOR_3D

func _get_global_code(mode):
	return """

		uniform vec4 BrushData0;
		uniform vec4 BrushData1;
		uniform vec4 BrushColor	 : source_color;

		uniform sampler2D terrainDefaultMaterial : source_color;


		vec3 genInspectorHighliteTerrain(vec4 brushColor, vec4 brushData0, vec4 brushData1, mat4 CAMMATRIX, vec3 vertexLocal)
		{
			float n_out12p0 = 0.000000;
			mat4 n_out6p0 =  CAMMATRIX;
			vec3 n_out7p0 = vertexLocal;
			vec3 n_out8p0 = (n_out6p0 * vec4(n_out7p0, 1.0)).xyz;

			vec3 n_out2p0 = BrushData0.rgb;
			float n_out2p1 = BrushData0.a;

			float n_out4p0 = distance(n_out8p0, n_out2p0);

			vec3 n_out3p0 = BrushData1.rgb;
			float n_out3p1 = BrushData1.a;

			float n_out10p0 = n_out3p0.x;
			float n_out10p1 = n_out3p0.y;
			float n_out10p2 = n_out3p0.z;

			float n_out9p0;
			n_out9p0 = 0.0;
			{
				float dist = n_out4p0;
				float radius = n_out2p1;
				float falloff = n_out10p0;
				float falloffType = n_out10p1;
				
				// Output0 = brush intensity
				n_out9p0 = 0.0f;
				
				if (dist < radius)
				{
					n_out9p0 = 1.0f;
				}
				else if (dist < radius + falloff)
				{
					float valueLinear = mix(1, 0, (dist - radius) / falloff);
					if (falloffType == 0.0f) // Smooth
						n_out9p0 = valueLinear * valueLinear * (3.0f - 2.0f * valueLinear);
					else if (falloffType == 1.0f) // Linear
						n_out9p0 = valueLinear;
					else if (falloffType == 2.0f) // Spherical
						n_out9p0 = sqrt(1.0f - ((dist - radius) / falloff) * (dist - radius) / falloff);
					else if (falloffType == 3.0f) // Tip
						n_out9p0 = 1.0f - sqrt(1.0f - ((falloff + radius - dist) / falloff) * (falloff + radius - dist) / falloff);
				}
				
			}

			float n_in11p1 = 0.20000;
			float n_out11p0 = mix(n_out12p0, n_in11p1, n_out9p0);

			vec3 n_out16p0 = vec3(0.968750, 1.000000, 0.000000);
			float n_out16p1 = 1.000000;

			float n_out15p0;
			n_out15p0 = 0.0f;
			{
				float Width = 7.0f;
				if (abs(n_out4p0) < Width || abs(n_out4p0 - n_out2p1) < Width)
					n_out15p0 = 1.0f;
				else if (abs(n_out4p0 - n_out2p1 - n_out10p0) < Width)
					n_out15p0 = 1.0f;
				else
					n_out15p0 = 0.0f;
				
			}

			float n_out19p0 = mix(n_out11p0, n_out16p1, n_out15p0);

			vec3 n_out13p0 = brushColor.rgb;
			float n_out13p1 = brushColor.a;

			float n_out20p0 = n_out19p0 * n_out13p1;

			vec3 n_out18p0 = vec3(n_out15p0, n_out15p0, n_out15p0);
			vec3 n_out17p0 = mix(n_out13p0, n_out16p0, n_out18p0);
			vec3 n_out22p0 = vec3(n_out13p1, n_out13p1, n_out13p1);

			vec3 n_out21p0 = n_out17p0 * n_out22p0;
			return mix(vec3(0.0f,0.0f,0.0f), n_out21p0, n_out20p0);
		}

	"""


func _get_code(input_vars, output_vars, mode, type):

	var heightStr = ""
	if type == 1:
		heightStr = "vec4 material = texture(terrainDefaultMaterial, UV);\n"
		heightStr += output_vars[0] + " = genInspectorHighliteTerrain(BrushColor, BrushData0, BrushData1, INV_VIEW_MATRIX, VERTEX);\n"
		heightStr += output_vars[1] + " = material.rgb;\n"

	return heightStr;
