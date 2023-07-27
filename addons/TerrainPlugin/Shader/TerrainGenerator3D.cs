#nullable enable
using Godot;
using System.Reflection;
using Godot.Collections;
using TerrainEditor.Utils;

namespace TerrainEditor.Shader;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
[Tool]
[GlobalClass]
public partial class TerrainGenerator3D : VisualShaderNodeCustom
{
    public override string _GetName() => GetType().Name;

    public override string _GetCategory() => VisualShaderNodeStrings.CATEGORY_TERRAIN_TOOL;

    public override string _GetDescription() => "Generate terrain (only vertex shader!)";

    public override PortType _GetReturnIconType() => PortType.Scalar;

    public override int _GetInputPortCount() => 0;

    public override int _GetOutputPortCount() => 8;

    public override string _GetOutputPortName(int port) =>
        port switch
        {
            0 => "vertex",
            1 => "height",
            2 => "normal",
            3 => "tangent",
            4 => "bionormal",
            5 => "red",
            6 => "green",
            7 => "uv",
            _ => "undefined"
        };

    public override PortType _GetOutputPortType(int port) =>
        port switch
        {
            0 or 2 or 3 or 4 or 7 => PortType.Vector3D,
            1 or 5 or 6 => PortType.Scalar,
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
        heightStr += "float lodCalculated = calculateLOD(terrainSmoothing, terrainCurrentLodLevel, terrainNeighborLod, UV, COLOR);";
        heightStr += "float lodValue = terrainCurrentLodLevel;";
        heightStr += "float morphAlpha = lodCalculated - terrainCurrentLodLevel;";

        heightStr += "vec4 heightMapValues = getHeightmap(UV, terrainChunkSize, terrainSmoothing, terrainUvScale, terrainHeightMap, morphAlpha,  terrainNextLodChunkSize,  terrainCurrentLodLevel);\n";
        heightStr += "float height = getHeight(heightMapValues);\n";

        heightStr += "vec3 position = getPosition(UV, terrainChunkSize, terrainCurrentLodLevel, terrainSmoothing, terrainNextLodChunkSize, lodCalculated);\n";
        heightStr += "mat3 tangentToLocal = CalcTangentBasisFromWorldNormal(getNormal(heightMapValues));\n";
        heightStr += "mat3 tangentToWorld = CalcTangentToWorld(MODEL_MATRIX, tangentToLocal);\n";
        heightStr += "vec3 worldNormal = tangentToWorld[2];\n";
        heightStr += "mat3 calculatedTBNWorld = CalcTangentBasisFromWorldNormal(worldNormal);\n";

        heightStr += "position.y = height;\n";

        heightStr += outputVars[0] + " = position;\n";
        heightStr += outputVars[1] + " = height;\n";

        heightStr += outputVars[2] + " = calculatedTBNWorld[2];\n";
        heightStr += outputVars[3] + " = calculatedTBNWorld[0];\n";
        heightStr += outputVars[4] + " = calculatedTBNWorld[1];\n";

        heightStr += outputVars[5] + " = heightMapValues.r;\n";
        heightStr += outputVars[6] + " = heightMapValues.g;\n";
        heightStr += "vec2 newUV = vec2(position.x, position.z) * (1.0f / terrainChunkSize) + terrainUvOffset;\n";
        heightStr += outputVars[7] + " = vec3(newUV.x, newUV.y, 0.0);\n";

        return heightStr;
    }
}