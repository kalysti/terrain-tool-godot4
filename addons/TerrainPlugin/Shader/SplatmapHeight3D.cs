#nullable enable
using System.Reflection;
using Godot;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class SplatmapHeight3D : VisualShaderNodeCustom
{
    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Splatmap height reader";

    public override PortType _GetReturnIconType() => PortType.Scalar;

    public override int _GetInputPortCount() => 0;

    public override int _GetOutputPortCount() => 8;

    public override string _GetOutputPortName(int port) => "layer" + port;

    public override PortType _GetOutputPortType(int port) => PortType.Scalar;

    public override string _GetGlobalCode(Godot.Shader.Mode mode)
    {
        string? calculatedPath = GetType().GetCustomAttribute<ScriptPathAttribute>()?.Path.Replace(".cs", ".gdshader");
        return ResourceLoader.Load<Godot.Shader>(calculatedPath).Code.Replace("shader_type spatial;", "");
    }

    public override string _GetCode(Array<string> inputVars, Array<string> outputVars, Godot.Shader.Mode mode, VisualShader.Type type)
    {
        var heightStr = "";
        if (type == VisualShader.Type.Fragment)
        {
            heightStr = "vec4 splatmap1Values = _GetSplatmap(UV, terrainChunkSize, terrainUvScale, terrainSplatmap1, terrainCurrentLodLevel);\n";
            heightStr += "vec4 splatmap2Values = _GetSplatmap(UV, terrainChunkSize,  terrainUvScale, terrainSplatmap2, terrainCurrentLodLevel);\n";

            heightStr += outputVars[0] + " = splatmap1Values.r;\n";
            heightStr += outputVars[1] + " = splatmap1Values.g;\n";
            heightStr += outputVars[2] + " = splatmap1Values.b;\n";
            heightStr += outputVars[3] + " = splatmap1Values.a;\n";

            heightStr += outputVars[4] + " = splatmap2Values.r;\n";
            heightStr += outputVars[5] + " = splatmap2Values.g;\n";
            heightStr += outputVars[6] + " = splatmap2Values.b;\n";
            heightStr += outputVars[7] + " = splatmap2Values.a;\n";
        }

        return heightStr;
    }
}