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

        public TerrainToolMode toolMode = TerrainToolMode.None;
        public TerrainSculptMode toolSculptMode = TerrainSculptMode.Sculpt;

        public TerrainBrushType brushTypeMode = TerrainBrushType.Circle;

        public float toolStrength = 1.2f;
        public float toolFilterRadius = 0.4f;
        public float toolTargetHeight = 0f;
        public float toolNoiseAmount = 10000f;
        public float toolNoiseScale = 128f;
        public float brushSize = 4000;
        public float brushFallof = 0.5f;
        public int chunkSize = 32;
        public BrushFallOfType brushFallOfType = BrushFallOfType.Smooth;
        public uint collisionLayer = 1;
        public uint collisionMask = 1;
        public bool collsionEnabled = true;
        public StreamTexture2D terrainDefaultTexture;



        public override Godot.Collections.Array _GetPropertyList()
        {
            var arr = new Godot.Collections.Array();
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Terrain Tools"},
                {"type",  Variant.Type.Nil},
                {"usage", PropertyUsageFlags.Category | PropertyUsageFlags.Editor}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Tool Mode"},
                {"type",  Variant.Type.Nil},
                {"usage", PropertyUsageFlags.Group| PropertyUsageFlags.Editor},
                {"hint_string", "tool"}
            });

            //tools
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "toolMode"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Enum},
                {"usage", PropertyUsageFlags.Editor},
                {"hint_string", enumToString<TerrainToolMode>()}
            });

            var editTypeMode = (toolMode != TerrainToolMode.None) ? PropertyUsageFlags.Editor : PropertyUsageFlags.Noeditor;

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "toolSculptMode"},
                {"type", Variant.Type.Int },
                {"hint", PropertyHint.Enum},
                {"usage", editTypeMode},
                {"hint_string", enumToString<TerrainSculptMode>()}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "toolStrength"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage", PropertyUsageFlags.Editor},
                {"hint_string", "0f, 10f, 0.01f"}
            });

            var smoothMode = (toolSculptMode == TerrainSculptMode.Smooth && toolMode != TerrainToolMode.None) ? PropertyUsageFlags.Editor : PropertyUsageFlags.Noeditor;

            //smooth 
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "toolFilterRadius"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",  smoothMode},
                {"hint_string", "0f, 1f, 0.01f"}
            });

            var flattenMode = (toolSculptMode == TerrainSculptMode.Flatten && toolMode != TerrainToolMode.None) ? PropertyUsageFlags.Editor : PropertyUsageFlags.Noeditor;

            //flatten
            arr.Add(new Godot.Collections.Dictionary()
                {
                    {"name", "toolTargetHeight"},
                    {"type", Variant.Type.Float},
                    {"hint", PropertyHint.Range},
                    { "usage",  flattenMode},
                });


            var noideMode = (toolSculptMode == TerrainSculptMode.Noise && toolMode != TerrainToolMode.None) ? PropertyUsageFlags.Editor : PropertyUsageFlags.Noeditor;

            //noises
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "toolNoiseAmount"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",  noideMode},
                {"hint_string", "0f, 100000f, 0.1f"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "toolNoiseScale"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",  noideMode},
                {"hint_string", "0f, 10000f, 0.1f"}
            });

            //brushing
            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Brush Mode"},
                {"type",  Variant.Type.Nil},
                {"usage", PropertyUsageFlags.Group | PropertyUsageFlags.Editor},
                {"hint_string", "brush"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "brushTypeMode"},
                {"type", Variant.Type.String },
                {"hint", PropertyHint.Enum},
                {"usage",   PropertyUsageFlags.Editor},
                {"hint_string", enumToString<TerrainBrushType>()}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "brushSize"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",   editTypeMode},
                {"hint_string", "0f, 1000000f, 0.1f"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "brushFallof"},
                {"type", Variant.Type.Float },
                {"hint", PropertyHint.Range},
                {"usage",   editTypeMode},
                {"hint_string", "0f, 1f, 0.1f"}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "brushFallOfType"},
                {"type", Variant.Type.String },
                {"hint", PropertyHint.Enum},
                {"usage",   editTypeMode},
                {"hint_string", enumToString<BrushFallOfType>()}
            });

            arr.Add(new Godot.Collections.Dictionary()
            {
                {"name", "Terrain Group"},
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

            return String.Join(',', array.ToArray());
        }



    }
}