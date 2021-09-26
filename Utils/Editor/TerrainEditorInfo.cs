namespace TerrainEditor.Utils.Editor
{
    public struct TerrainEditorInfo
    {
        public float brushSize { get; set; }
        public float brushFallof { get; set; }
        public float strength { get; set; }
        public float radius { get; set; }
        public float height { get; set; }
        public int layer { get; set; }
        public bool inverse { get; set; }
        public float noiseAmount { get; set; }
        public float noiseScale { get; set; }

        public BrushFallOfType brushFallofType { get; set; }
    }
}