#nullable enable
using Godot;
using System.Reflection;
using Godot.Collections;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class PerlinNoise2D : VisualShaderNodeCustom
{
    public PerlinNoise2D()
    {
        SetInputPortDefaultValue(1, new Vector3(0, 0, 0));
        SetInputPortDefaultValue(2, 5.0f);
        SetInputPortDefaultValue(3, new Vector3(0.0f, 0.0f, 0.0f));
    }

    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => "RGBA";

    // public override string _GetSubCategory()
    // {
    //     return "Noise";
    // }

    public override string _GetDescription() => "";

    public override PortType _GetReturnIconType() => PortType.Scalar;

    public override int _GetInputPortCount() => 4;

    public override string _GetInputPortName(int port) =>
        port switch
        {
            0 => "uv",
            1 => "offset",
            2 => "scale",
            3 => "period",
            _ => "Invalid port"
        };

    public override PortType _GetInputPortType(int port) =>
        port switch
        {
            0 or 1 or 3 => PortType.Vector3D,
            2 => PortType.Scalar,
            _ => 0
        };

    public override int _GetOutputPortCount() => 1;

    public override string _GetOutputPortName(int port) =>
        port switch
        {
            0 => "result",
            _ => "Invalid port"
        };

    public override PortType _GetOutputPortType(int port) =>
        port switch
        {
            0 => PortType.Scalar,
            _ => 0
        };

    public override string _GetGlobalCode(Godot.Shader.Mode mode)
    {
	    string? calculatedPath = GetType().GetCustomAttribute<ScriptPathAttribute>()?.Path.Replace(".cs", ".gdshader");
	    return ResourceLoader.Load<Godot.Shader>(calculatedPath).Code.Replace("shader_type spatial;", "");
    }

    public override string _GetCode(Array<string> inputVars, Array<string> outputVars, Godot.Shader.Mode mode, VisualShader.Type type)
    {
        var uv = "UV";

        if (inputVars[0] != null)
        {
            uv = inputVars[0];
        }

        return $"{outputVars[0]} = perlin2dN0iseFunc(({uv}.xy+{inputVars[1]}.xy)*{inputVars[2]}, {inputVars[3]}.xy);";
    }
}