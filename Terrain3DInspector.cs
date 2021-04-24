using System.ComponentModel;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
    public partial class Terrain3D : Node3D
    {
        public Godot.Collections.Array<TerrainPatch> terrainPatches = new Godot.Collections.Array<TerrainPatch>();
        public ShaderMaterial terrainDefaultMaterial { get; set; }

        public uint collisionLayer = 1;
        public uint collisionMask = 1;
        public bool collsionEnabled = true;
        public StreamTexture2D terrainDefaultTexture;

        public RenderingServer.ShadowCastingSetting castShadow = RenderingServer.ShadowCastingSetting.On;
        public GIMode giMode = GIMode.Disabled;
        public LightmapScale giLightmapScale = LightmapScale.LIGHTMAP_SCALE_1X;
        public float extraCullMargin = 0.0f;
        public float lodBias = 1.0f;

        public int lodMinDistance = 0;
        public int lodMinHysteresis = 0;
        public int lodMaxDistance = 0;
        public int lodMaxHysteresis = 0;


        public override Godot.Collections.Array _GetPropertyList()
        {
            var arr = new Godot.Collections.Array();
            var test = new VisualShaderNodeCustom();
      
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Terrain3D"},
                {"type",  Variant.Type.Nil},
                {"usage", PropertyUsageFlags.Category  | PropertyUsageFlags.Editor}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Terrain Data"},
                {"type",  Variant.Type.Nil},
                {"usage", PropertyUsageFlags.Group  | PropertyUsageFlags.Editor},
                {"hint_string", "terrain"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "terrainDefaultMaterial"},
                {"type", Variant.Type.Object },
                {"hint", PropertyHint.ResourceType},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "ShaderMaterial"}
            }); 
            
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "terrainDefaultTexture"},
                {"type", Variant.Type.Object },
                {"hint", PropertyHint.ResourceType},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "StreamTexture2D"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "terrainPatches"},
                {"type", Variant.Type.Array },
                {"hint", PropertyHint.ResourceType},
                {"usage",   PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "TerrainPatch"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Collision"},
                { "type",  Variant.Type.Nil},
                { "usage", PropertyUsageFlags.Group | PropertyUsageFlags.Editor}
             });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "collsionEnabled"},
                {"type",  Variant.Type.Bool},
                {  "usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
               { "name", "collisionLayer"},
                {"type",  Variant.Type.Int},
                { "usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                { "hint", PropertyHint.Layers3dPhysics}

            });
            
            arr.Add(new Godot.Collections.Dictionary()
            {
             { "name", "collisionMask"},
                {"type",  Variant.Type.Int},
                { "usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                { "hint", PropertyHint.Layers3dRender}

            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Geometry"},
               { "type",  Variant.Type.Nil},
                { "usage", PropertyUsageFlags.Group | PropertyUsageFlags.Editor}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "castShadow"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Enum},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "Off,On,Double-Sided,Shadows Only"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "extraCullMargin"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "0,16384,0.01"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "lodBias"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "0.001,128,0.001"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Global Illumination"},
               { "type",  Variant.Type.Nil},
                { "usage", PropertyUsageFlags.Group | PropertyUsageFlags.Editor}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "giLightmapScale"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Enum},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "1x,2x,4x,8x"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "giMode"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Enum},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "Disabled,Baked,Dynamic"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Terrain LOD"},
                { "type",  Variant.Type.Nil},
                { "usage", PropertyUsageFlags.Group | PropertyUsageFlags.Editor}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "lodMinDistance"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Range},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "0,32768,0.01"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "lodMinHysteresis"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Range},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "0,32768,0.01"}
            });
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "lodMaxDistance"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Range},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "0,32768,0.01"}
            });
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "lodMaxHysteresis"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Range},
                {"usage",  PropertyUsageFlags.Editor | PropertyUsageFlags.Storage},
                {"hint_string", "0,32768,0.01"}
            });

            return arr;
        }

        public string enumToString<T>()
        {
            var array = new Godot.Collections.Array<string>();
            foreach (int i in Enum.GetValues(typeof(T)))
            {
                String name = Enum.GetName(typeof(T), i);
                array.Add(name);
            }

            return String.Join(",", array.ToArray());
        }



    }
}