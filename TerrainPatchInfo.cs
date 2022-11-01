using Godot;

namespace TerrainEditor;

[Tool]
public partial class TerrainPatchInfo : Resource
{
    [Export]
    public float PatchOffset { get; set; }

    [Export]
    public float PatchHeight { get; set; }

    [Export]
    public int ChunkSize { get; set; }

    [Export]
    public int VertexCountEdge { get; set; }

    [Export]
    public int HeightMapSize { get; set; }

    [Export]
    public int TextureSize { get; set; }
}