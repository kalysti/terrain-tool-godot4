#nullable enable
using System.Reflection;
using Godot;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class TerrainMaterial3D : VisualShaderNodeCustom
{
	public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Default Terrain Material";

    public override PortType _GetReturnIconType() => PortType.Scalar;

    public override int _GetInputPortCount() => 0;

    public override int _GetOutputPortCount() => 2;

    public override string _GetOutputPortName(int port) =>
        port switch
        {
            0 => "emission",
            1 => "albedo",
            _ => "undefined"
        };

    public override PortType _GetOutputPortType(int port) =>
        port switch
        {
            0 or 1 => PortType.Vector3D,
            _ => 0
        };

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
            heightStr = "vec4 material = texture(terrainDefaultMaterial, UV);\n";
            heightStr += outputVars[0] + " = genInspectorHighlightTerrain(BrushColor, BrushData0, BrushData1, INV_VIEW_MATRIX, VERTEX);\n";
            heightStr += outputVars[1] + " = material.rgb;\n";
        }

        return heightStr;
    }
}