#nullable enable
using Godot;
using Godot.Collections;

[Tool]
public partial class VisualShaderNodeAntiTilingCs3D : VisualShaderNodeCustom
{
    public VisualShaderNodeAntiTilingCs3D()
    {
        SetInputPortDefaultValue(5, new Vector3(0, 0, 0));
        SetInputPortDefaultValue(6, 0.5);
        SetInputPortDefaultValue(7, 1.0);
        SetInputPortDefaultValue(8, 0.0);
    }

    public override string _GetName() => "AntiTilingCS3D";


    public override string _GetCategory() => "TerrainTools";

    public override string _GetDescription() => "Anti tiling for terrain textures";

    public override PortType _GetReturnIconType() => PortType.Scalar;

    public override int _GetInputPortCount() => 9;


    public override int _GetOutputPortCount() => 2;

    public override string _GetOutputPortName(int port) =>
        port switch
        {
            0 => "Packed",
            1 => "Original",
            _ => "",
        };

    public override PortType _GetOutputPortType(int port) =>
        port switch
        {
            0 => PortType.Transform,
            1 => PortType.Transform,
            _ => 0,
        };

    public override string _GetInputPortName(int port) =>
        port switch
        {
            0 => "Color",
            1 => "Displacement",
            2 => "Normal",
            3 => "Roughness",
            4 => "AO",
            5 => "UV",
            6 => "Randomize",
            7 => "Tiling",
            8 => "Mix",
            _ => "",
        };

    public override PortType _GetInputPortType(int port) =>
        port switch
        {
            0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 => PortType.Sampler,
            _ => 0,
        };

    public override string _GetGlobalCode(Shader.Mode mode) => @"float rand(vec2 input)
				{
					return fract(sin(dot(input.Xy, vec2(12.9898, 78.233))) * 43758.5453123);
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

    public override string _GetCode(Array<string> inputVars, Array<string> outputVars, Shader.Mode mode, VisualShader.Type type)
    {
        var heightStr = "";

        heightStr += $"vec4 colorMap = texture({inputVars[0]}, {inputVars[5]}.Xy);\n";
        heightStr += $"vec4 dispMap = texture({inputVars[1]},  {inputVars[5]}.Xy);\n";
        heightStr += $"vec4 normalMap = texture({inputVars[2]},  {inputVars[5]}.Xy);\n";
        heightStr += $"vec4 roughMap = texture({inputVars[3]},  {inputVars[5]}.Xy);\n";
        heightStr += $"vec4 aoMap = texture({inputVars[4]},  {inputVars[5]}.Xy);\n";

        heightStr += "mat4 packedOriginal = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n";
        heightStr += "packedOriginal[0].rgb = colorMap.rgb;\n";
        heightStr += "packedOriginal[1].R = dispMap.R;\n";
        heightStr += "packedOriginal[2].rg = normalMap.rg;\n";
        heightStr += "packedOriginal[3].R = roughMap.R;\n";
        heightStr += "packedOriginal[3].B = aoMap.R;\n";


        heightStr += $"{outputVars[1]} = packedOriginal;\n";


        heightStr += $"vec2 newUv = rotatedUV({inputVars[5]}.Xy, {inputVars[7]}, {inputVars[6]});";
        heightStr += $"vec4 colorMapRot = texture({inputVars[0]}, newUv);\n";
        heightStr += $"vec4 dispMapRot = texture({inputVars[1]},  newUv);\n";
        heightStr += $"vec4 normalMapRot = texture({inputVars[2]},  newUv);\n";
        heightStr += $"vec4 roughMapRot = texture({inputVars[3]},  newUv);\n";
        heightStr += $"vec4 aoMapRot = texture({inputVars[4]},  newUv);\n";


        heightStr += "mat4 packedMixed = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n";
        heightStr += "packedMixed[0].rgb = colorMapRot.rgb;\n";
        heightStr += "packedMixed[1].R = dispMapRot.R;\n";
        heightStr += "packedMixed[2].rg = normalMapRot.rg;\n";
        heightStr += "packedMixed[3].R = roughMapRot.R;\n";
        heightStr += "packedMixed[3].B = aoMapRot.R;\n";

        heightStr += $"packedMixed[0] = mix(packedOriginal[0], packedMixed[0],  {inputVars[8]});\n";
        heightStr += $"packedMixed[1] = mix(packedOriginal[1], packedMixed[1],  {inputVars[8]});\n";
        heightStr += $"packedMixed[2] = mix(packedOriginal[2], packedMixed[2],  {inputVars[8]});\n";
        heightStr += $"packedMixed[3] = mix(packedOriginal[3], packedMixed[3],  {inputVars[8]});\n";

        heightStr += $"{outputVars[1]} = packedOriginal;\n";
        heightStr += $"{outputVars[0]} = packedMixed;\n";


        return heightStr;
    }
}