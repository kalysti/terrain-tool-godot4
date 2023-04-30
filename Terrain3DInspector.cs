using System.ComponentModel;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;
using Godot.Collections;

namespace TerrainEditor
{
    public partial class Terrain3D : Node3D
    {
        [ExportCategory("Terrain3D")]
        [ExportGroup("Terrain Data")]
        [Export(PropertyHint.ResourceType, "ShaderMaterial")]
        public ShaderMaterial terrainDefaultMaterial { get; set; }
        [Export(PropertyHint.ResourceType, "CompressedTexture2D")]
        public CompressedTexture2D terrainDefaultTexture;
        [Export(PropertyHint.ResourceType, "TerrainPatch")]
        public Array<TerrainPatch> terrainPatches = new();

        [ExportGroup("Collision")]
        public bool collisionEnabled = true;
        [Export(PropertyHint.Layers3DPhysics)]
        public uint collisionLayer = 1;
        [Export(PropertyHint.Layers3DRender)]
        public uint collisionMask = 1;

        [ExportGroup("Geometry")]
        [Export(PropertyHint.Enum, "Off,On,Double-Sided,Shadows Only")]
        public RenderingServer.ShadowCastingSetting castShadow = RenderingServer.ShadowCastingSetting.On;
        [Export(PropertyHint.Range, "0,16384,0.01")]
        public float extraCullMargin = 0.0f;
        [Export(PropertyHint.Range, "0.001,128,0.001")]
        public float lodBias = 1.0f;
        
        [ExportGroup("Global Illumination")]
        [Export(PropertyHint.Enum, "1x,2x,4x,8x")]
        public LightmapScale giLightmapScale = LightmapScale.LIGHTMAP_SCALE_1X;
        [Export(PropertyHint.Enum, "Disabled,Baked,Dynamic")]
        public GIMode giMode = GIMode.Disabled;

        [ExportGroup("Terrain LOD")]
        [Export(PropertyHint.Range, "0,32768,0.01")]
        public float lodMinDistance = 0;
        [Export(PropertyHint.Range, "0,32768,0.01")]
        public float lodMinHysteresis = 0;
        [Export(PropertyHint.Range, "0,32768,0.01")]
        public float lodMaxDistance = 0;
        [Export(PropertyHint.Range, "0,32768,0.01")]
        public float lodMaxHysteresis = 0;

    }
}