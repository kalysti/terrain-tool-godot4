namespace TerrainEditor.Utils;

///<author email="Sythelux Rikd">Sythelux Rikd</author>
public static class ToolSettingKeys
{
    public const string TOOL_MODE = "mode";
    public const string TOOL_SCULPT_MODE = "sculpt_mode";
    public const string TOOL_STRENGTH = "strength";
    public const string TOOL_RADIUS = "radius";
    public const string TOOL_HEIGHT = "height";
    public const string TOOL_NOISE_AMOUNT = "noise_amount";
    public const string TOOL_NOISE_SCALE = "noise_scale";
    public const string TOOL_BRUSH_SIZE = "brush_size";
    public const string TOOL_BRUSH_FALLOFF = "brush_falloff";
    public const string TOOL_LAYER = "layer";
    public const string TOOL_BRUSH_FALLOFF_TYPE = "brush_falloff_type";
    public const string TOOL_SHOW_AABB = "show_aabb";
    public const string TOOL_SHOW_COLLIDER = "show_collider";
}

public static class UserInterfaceNames
{
    public const string TERRAIN = "Terrain";
    public const string TERRAIN_CREATE = "Create terrain";
    public const string EXPORT_HEIGHTMAP = "Export heightmap (16bit)";
    public const string EXPORT_SPLATMAP = "Export splatmap (16bit)";
    public const string IMPORT_MAPBOX = "Mapbox import";
}

public static class MaterialParameterNames
{
    public const string UV_SCALE = "uv_scale";
    public const string COLOR = "Color";
    public const string BRUSH_DATA0 = "BrushData0";
    public const string BRUSH_DATA1 = "BrushData1";
    public const string TERRAIN_DEFAULT_MATERIAL = "terrainDefaultMaterial";
    public const string TERRAIN_HEIGHT_MAP = "terrainHeightMap";
    public const string TERRAIN_CHUNK_SIZE = "terrainChunkSize";
    public const string TERRAIN_NEXT_LOD_CHUNK_SIZE = "terrainNextLodChunkSize";
    public const string TERRAIN_CURRENT_LOD_LEVEL = "terrainCurrentLodLevel";
    public const string TERRAIN_SPLATMAP1 = "terrainSplatmap1";
    public const string TERRAIN_SPLATMAP2 = "terrainSplatmap2";
    public const string TERRAIN_UV_SCALE = "terrainUvScale";
    public const string TERRAIN_NEIGHBOR_LOD = "terrainNeighborLod";
    public const string TERRAIN_SMOOTHING = "terrainSmoothing";
    public const string TERRAIN_UV_OFFSET = "terrainUvOffset";
}