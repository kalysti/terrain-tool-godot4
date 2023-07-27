#nullable enable
using Godot;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class TexturePack3D : VisualShaderNodeCustom
{
    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Default Terrain Material";

    public override PortType _GetReturnIconType() => PortType.Transform;

    public TexturePack3D()
    {
        SetInputPortDefaultValue(0, new Vector3(0.0f, 0.0f, 0.0f));
        SetInputPortDefaultValue(1, 0.0f);
        SetInputPortDefaultValue(2, new Vector3(0.0f, 0.0f, 0.0f));
        SetInputPortDefaultValue(3, new Vector3(0.0f, 0.0f, 0.0f));
        SetInputPortDefaultValue(4, 0.0f);
    }

    public override string _GetInputPortName(int port) =>
        port switch
        {
            0 => "color",
            1 => "displacement",
            2 => "normal",
            3 => "roughness",
            4 => "ao",
            _ => "undefined"
        };

    public override PortType _GetInputPortType(int port) =>
        port switch
        {
            0 or 2=> PortType.Vector3D,
            1 or 3 or 4 => PortType.Scalar,
            _ => 0
        };

    public override int _GetInputPortCount() => 5;

    public override int _GetOutputPortCount() => 1;

    public override string _GetOutputPortName(int port) => "packed";

    public override PortType _GetOutputPortType(int port) => PortType.Transform;

    public override string _GetCode(Array<string> inputVars, Array<string> outputVars, Godot.Shader.Mode mode, VisualShader.Type type)
    {
        var heightStr = "";

        // Insert shader code here
        heightStr = "mat4 packed = mat4(vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 0.0));\n";
        heightStr += "packed[0].rgb =  " + inputVars[0] + ".rgb;\n";
        heightStr += "packed[1].r =  " + inputVars[1] + ";\n";
        heightStr += "packed[2].rgb =  " + inputVars[2] + ".rgb;\n";
        heightStr += "packed[3].r =  " + inputVars[3] + ";\n";
        heightStr += "packed[3].b =  " + inputVars[4] + ";\n";
        heightStr += outputVars[0] + " = packed;\n";

        return heightStr;
    }
}