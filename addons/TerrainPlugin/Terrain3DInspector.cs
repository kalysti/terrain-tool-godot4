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

        public int chunkSize = 32;
        public uint collisionLayer = 1;
        public uint collisionMask = 1;
        public bool collsionEnabled = true;
        public StreamTexture2D terrainDefaultTexture;

        public override Godot.Collections.Array _GetPropertyList()
        {
            var arr = new Godot.Collections.Array();
           
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

            return String.Join(",", array.ToArray());
        }



    }
}