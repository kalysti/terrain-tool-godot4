#nullable enable
using Godot;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class UVMultiply3D : VisualShaderNodeCustom
{
    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Default Terrain Material";

    public override PortType _GetReturnIconType() => PortType.Scalar;

    public UVMultiply3D() => SetInputPortDefaultValue(0, new Vector3(1.0f, 1.0f, 0.0f));

    public override string _GetInputPortName(int port) => port == 0 ? "scale" : "unknown";

    public override PortType _GetInputPortType(int port) => PortType.Vector3D;

    public override int _GetInputPortCount() => 1;

    public override int _GetOutputPortCount() => 1;

    public override string _GetOutputPortName(int port) => port == 0 ? "packed" : "unknown";

    public override PortType _GetOutputPortType(int port) => port == 0 ? PortType.Vector3D : 0;

    public override string _GetCode(Array<string> inputVars, Array<string> outputVars, Godot.Shader.Mode mode, VisualShader.Type type)
    {
        var heightStr = $"vec2 scaled = UV * vec2({inputVars[0]}.x, {inputVars[0]}.y);\n";
        heightStr += $"{outputVars[0]} = vec3(scaled.x, scaled.y, 0.0f);\n";

        return heightStr;
    }
}