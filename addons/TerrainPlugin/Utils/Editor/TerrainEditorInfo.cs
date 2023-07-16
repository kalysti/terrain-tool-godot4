namespace TerrainEditor.Utils.Editor;

public struct TerrainEditorInfo
{
    public float BrushSize { get; set; }
    public float BrushFalloff { get; set; }
    public float Strength { get; set; }
    public float Radius { get; set; }
    public float Height { get; set; }
    public int Layer { get; set; }
    public bool Inverse { get; set; }
    public float NoiseAmount { get; set; }
    public float NoiseScale { get; set; }

    public BrushFallOffType BrushFalloffType { get; set; }
}