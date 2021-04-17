using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainPatchInfo : Resource
    {
        [Export]
        public float patchOffset = 0;
        [Export]
        public float patchHeight = 0;
        [Export]
        public int chunkSize = 0;
        [Export]
        public int vertexCountEdge = 0;
        [Export]
        public int heightMapSize = 0;
        [Export]
        public int textureSize = 0;

    }
}