using Godot;
using Godot.Collections;

namespace TerrainEditor;

public partial class Terrain3D : Node3D
{
    [ExportCategory("Terrain3D")]
    [ExportGroup("Terrain Data")]
    [Export(PropertyHint.ResourceType, "ShaderMaterial")]
    public ShaderMaterial? TerrainDefaultMaterial { get; set; } = GD.Load<ShaderMaterial>("res://addons/TerrainPlugin/Shader/TerrainVisualShader.tres");
    [Export(PropertyHint.ResourceType, "CompressedTexture2D")]
    public CompressedTexture2D? TerrainDefaultTexture { get; set; } = GD.Load<CompressedTexture2D>("res://addons/TerrainPlugin/TestTextures/texel.png");
    [Export(PropertyHint.ResourceType, "TerrainPatch")]
    public Array<TerrainPatch?> TerrainPatches { get; set; } = new();

    [ExportGroup("Collision")]
    public bool CollisionEnabled { get; set; } = true;
    [Export(PropertyHint.Layers3dPhysics)]
    public uint CollisionLayer { get; set; } = 1;
    [Export(PropertyHint.Layers3dRender)]
    public uint CollisionMask { get; set; } = 1;

    [ExportGroup("Geometry")]
    [Export(PropertyHint.Enum, "Off,On,Double-Sided,Shadows Only")]
    public RenderingServer.ShadowCastingSetting CastShadow  { get; set; }= RenderingServer.ShadowCastingSetting.On;
    [Export(PropertyHint.Range, "0,16384,0.01")]
    public float ExtraCullMargin { get; set; }
    [Export(PropertyHint.Range, "0.001,128,0.001")]
    public float LodBias { get; set; } = 1.0f;
        
    [ExportGroup("Global Illumination")]
    [Export(PropertyHint.Enum, "1x,2x,4x,8x")]
    public LightmapScale GiLightmapScale { get; set; } = LightmapScale.LIGHTMAP_SCALE_1_X;
    [Export(PropertyHint.Enum, "Disabled,Baked,Dynamic")]
    public GiMode GiMode { get; set; } = GiMode.DISABLED;

    [ExportGroup("Terrain LOD")]
    [Export(PropertyHint.Range, "0,32768,0.01")]
    public float LodMinDistance { get; set; }
    [Export(PropertyHint.Range, "0,32768,0.01")]
    public float LodMinHysteresis { get; set; }
    [Export(PropertyHint.Range, "0,32768,0.01")]
    public float LodMaxDistance { get; set; }
    [Export(PropertyHint.Range, "0,32768,0.01")]
    public float LodMaxHysteresis { get; set; }

}