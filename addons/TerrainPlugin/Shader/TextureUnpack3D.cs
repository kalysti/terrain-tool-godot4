#nullable enable
using Godot;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class TextureUnpack3D : VisualShaderNodeCustom
{
    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Default Terrain Material";

    public override PortType _GetReturnIconType() => PortType.Transform;

    public TextureUnpack3D() => SetInputPortDefaultValue(0, new Transform3D());

    public override string _GetInputPortName(int port) => port == 0 ? "packed" : "unknown";

    public override PortType _GetInputPortType(int port) => port == 0 ? PortType.Transform : 0;

    public override int _GetInputPortCount() => 1;

    public override int _GetOutputPortCount() => 5;

    public override string _GetOutputPortName(int port) =>
        port switch
        {
            0 => "color",
            1 => "displacement",
            2 => "normal",
            3 => "roughness",
            4 => "ao",
            _ => "unknown"
        };

    public override PortType _GetOutputPortType(int port) =>
        port switch
        {
            0 or 2 => PortType.Vector3D,
            1 or 3 or 4 => PortType.Scalar,
            _ => 0
        };

    public override string _GetCode(Array<string> inputVars, Array<string> outputVars, Godot.Shader.Mode mode, VisualShader.Type type)
    {
        var heightStr = "";
        heightStr += $"{outputVars[0]} = {inputVars[0]}[0].rgb;\n";
        heightStr += $"{outputVars[1]} = {inputVars[0]}[1].r;\n";
        heightStr += $"{outputVars[2]} = vec3({inputVars[0]}[2].r, {inputVars[0]}[2].g, {inputVars[0]}[2].b);\n";
        heightStr += $"{outputVars[3]} = {inputVars[0]}[3].r;\n";
        heightStr += $"{outputVars[4]} = {inputVars[0]}[3].b;\n";

        return heightStr;
    }
}