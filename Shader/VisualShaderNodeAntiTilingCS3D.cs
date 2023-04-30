using Godot;
using Godot.Collections;

[Tool]
public partial class VisualShaderNodeAntiTilingCS3D : VisualShaderNodeCustom
{

	public VisualShaderNodeAntiTilingCS3D() : base()
	{

		SetInputPortDefaultValue(5, new Vector3(0, 0, 0));
		SetInputPortDefaultValue(6, 0.5);
		SetInputPortDefaultValue(7, 1.0);
		SetInputPortDefaultValue(8, 0.0);

	}

	public override string _GetName()
	{
		return "AntiTilingCS3D";
	}


	public override string _GetCategory()
	{
		return "TerrainTools";
	}

	public override string _GetDescription()
	{
		return "Anti tiling for terrain textures";

	}
	public override VisualShaderNode.PortType _GetReturnIconType()
	{
		return (int)VisualShaderNode.PortType.Scalar;
	}

	public override int _GetInputPortCount()
	{
		return 9;
	}


	public override int _GetOutputPortCount()
	{
		return 2;
	}

	public override string _GetOutputPortName(int port)
	{
		switch (port)
		{
			case 0: return "Packed";
			case 1: return "Original";
		}

		return "";
	}

	public override VisualShaderNode.PortType _GetOutputPortType(int port)
	{
		switch (port)
		{
			case 0: return VisualShaderNode.PortType.Transform;
			case 1: return VisualShaderNode.PortType.Transform;
		}
		return 0;
	}


	public override string _GetInputPortName(int port)
	{
		switch (port)
		{
			case 0: return "Color";
			case 1: return "Displacement";
			case 2: return "Normal";
			case 3: return "Roughness";
			case 4: return "AO";
			case 5: return "UV";
			case 6: return "Randomize";
			case 7: return "Tiling";
			case 8: return "Mix";
		}

		return "";
	}

	public override VisualShaderNode.PortType _GetInputPortType(int port)
	{
		switch (port)
		{
			case 0: return VisualShaderNode.PortType.Sampler;
			case 1: return VisualShaderNode.PortType.Sampler;
			case 2: return VisualShaderNode.PortType.Sampler;
			case 3: return VisualShaderNode.PortType.Sampler;
			case 4: return VisualShaderNode.PortType.Sampler;
			case 5: return VisualShaderNode.PortType.Sampler;
			case 6: return VisualShaderNode.PortType.Sampler;
			case 7: return VisualShaderNode.PortType.Sampler;
			case 8: return VisualShaderNode.PortType.Sampler;
		}

		return 0;
	}
	public override string _GetGlobalCode(Shader.Mode mode)
	{
		return @"float rand(vec2 input)
				{
					return fract(sin(dot(input.xy, vec2(12.9898, 78.233))) * 43758.5453123);
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
				}";
	}

	public override string _GetCode(Array<string> input_vars, Array<string> output_vars, Shader.Mode mode, VisualShader.Type type)
	{
		var heightStr = "";

		heightStr += "vec4 colorMap = texture(" + input_vars[0] + ", " + input_vars[5] + ".xy);\n";
		heightStr += "vec4 dispMap = texture(" + input_vars[1] + ",  " + input_vars[5] + ".xy);\n";
		heightStr += "vec4 normalMap = texture(" + input_vars[2] + ",  " + input_vars[5] + ".xy);\n";
		heightStr += "vec4 roughMap = texture(" + input_vars[3] + ",  " + input_vars[5] + ".xy);\n";
		heightStr += "vec4 aoMap = texture(" + input_vars[4] + ",  " + input_vars[5] + ".xy);\n";

		heightStr += "mat4 packedOriginal = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n";
		heightStr += "packedOriginal[0].rgb = colorMap.rgb;\n";
		heightStr += "packedOriginal[1].r = dispMap.r;\n";
		heightStr += "packedOriginal[2].rg = normalMap.rg;\n";
		heightStr += "packedOriginal[3].r = roughMap.r;\n";
		heightStr += "packedOriginal[3].b = aoMap.r;\n";


		heightStr += output_vars[1] + " = packedOriginal;\n";


		heightStr += "vec2 newUv = rotatedUV(" + input_vars[5] + ".xy, " + input_vars[7] + ", " + input_vars[6] + ");";
		heightStr += "vec4 colorMapRot = texture(" + input_vars[0] + ", newUv);\n";
		heightStr += "vec4 dispMapRot = texture(" + input_vars[1] + ",  newUv);\n";
		heightStr += "vec4 normalMapRot = texture(" + input_vars[2] + ",  newUv);\n";
		heightStr += "vec4 roughMapRot = texture(" + input_vars[3] + ",  newUv);\n";
		heightStr += "vec4 aoMapRot = texture(" + input_vars[4] + ",  newUv);\n";


		heightStr += "mat4 packedMixed = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n";
		heightStr += "packedMixed[0].rgb = colorMapRot.rgb;\n";
		heightStr += "packedMixed[1].r = dispMapRot.r;\n";
		heightStr += "packedMixed[2].rg = normalMapRot.rg;\n";
		heightStr += "packedMixed[3].r = roughMapRot.r;\n";
		heightStr += "packedMixed[3].b = aoMapRot.r;\n";

		heightStr += "packedMixed[0] = mix(packedOriginal[0], packedMixed[0],  " + input_vars[8] + ");\n";
		heightStr += "packedMixed[1] = mix(packedOriginal[1], packedMixed[1],  " + input_vars[8] + ");\n";
		heightStr += "packedMixed[2] = mix(packedOriginal[2], packedMixed[2],  " + input_vars[8] + ");\n";
		heightStr += "packedMixed[3] = mix(packedOriginal[3], packedMixed[3],  " + input_vars[8] + ");\n";

		heightStr += output_vars[1] + " = packedOriginal;\n";
		heightStr += output_vars[0] + " = packedMixed;\n";


		return heightStr;

	}


}
