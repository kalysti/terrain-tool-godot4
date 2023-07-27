#nullable enable
using System.Reflection;
using Godot;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class DepthBlending3D : VisualShaderNodeCustom
{
    public DepthBlending3D()
    {
        SetInputPortDefaultValue(0, new Transform3D());
        SetInputPortDefaultValue(1, new Transform3D());
        SetInputPortDefaultValue(2, 0.5f);
        SetInputPortDefaultValue(3, 0.05f);
        SetInputPortDefaultValue(4, false);
    }

    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Splatmap height reader";

    public override PortType _GetReturnIconType() => PortType.Transform;

    public override int _GetInputPortCount() => 5;

    public override int _GetOutputPortCount() => 1;

    public override string _GetOutputPortName(int port) => "mixed";

    public override PortType _GetOutputPortType(int port) => PortType.Transform;

    public override string _GetInputPortName(int port) =>
        port switch
        {
            0 => "top packed",
            1 => "bottom packed",
            2 => "mix",
            3 => "blending",
            4 => "grayscale",
            _ => "Invalid port"
        };

    public override PortType _GetInputPortType(int port) =>
        port switch
        {
            0 or 1 => PortType.Transform,
            2 or 3 => PortType.Scalar,
            4 => PortType.Boolean,
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

        heightStr = "vec3 tex1 = " + inputVars[0] + "[0].rgb;\n";
        heightStr += "vec3 tex2 = " + inputVars[1] + "[0].rgb;\n";

        heightStr += "float avg1 = (tex1.r + tex1.g + tex1.b) / 3.0f;\n";
        heightStr += "float avg2 = (tex2.r + tex2.g + tex2.b) / 3.0f;\n";

        heightStr += "vec3 bump1 = " + inputVars[0] + "[2].rgb;\n";
        heightStr += "vec3 bump2 = " + inputVars[1] + "[2].rgb;\n";

        heightStr += "bump1.xy = bump1.xy / bump1.z;\n";
        heightStr += "bump2.xy = bump2.xy / bump2.z;\n";

        heightStr += "bump1.z = 1.0;\n";
        heightStr += "bump2.z = 1.0;\n";

        heightStr += "if(" + inputVars[4] + " == false) { \n";
        heightStr += "	avg1 = " + inputVars[0] + "[1].r;\n";
        heightStr += "	avg2 = " + inputVars[1] + "[1].r;\n";
        heightStr += "}\n";

        heightStr += "mat4 result = mat4(vec4(0.0f,0.0f,0.0f,0.0f),vec4(0.0f,0.0f,0.0f,0.0f),vec4(0.0f,0.0f,0.0f,0.0f),vec4(0.0f,0.0f,0.0f,0.0f));\n";
        heightStr += "result[0].rgb = HeightLerp(" + inputVars[3] + ", " + inputVars[0] + "[0].rgb, avg1, " + inputVars[1] + "[0].rgb, avg2, " + inputVars[2] + ");\n";
        heightStr += "result[1].rgb = HeightLerp(" + inputVars[3] + ", " + inputVars[0] + "[1].rgb, avg1, " + inputVars[1] + "[1].rgb, avg2, " + inputVars[2] + ");\n";
        heightStr += "result[2].rgb = mix( " + inputVars[0] + "[2].rgb,  " + inputVars[1] + "[2].rgb, " + inputVars[2] + ");\n";
        //needs to finish it
        //heightStr += "result[2].rgb =  normalize((mix(bump1, bump2, "+input_vars[2]+") * 2.0) - 1.0);\n"
        heightStr += "result[3].rgb = HeightLerp(" + inputVars[3] + ", " + inputVars[0] + "[3].rgb, avg1, " + inputVars[1] + "[3].rgb, avg2, " + inputVars[2] + ");\n";
        heightStr += outputVars[0] + " = result;\n";

        return heightStr;
    }
}