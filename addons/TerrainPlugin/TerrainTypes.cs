using System.ComponentModel;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
    public enum TerrainToolMode
    {

        Sculpt,
        Paint,
        None
    }
    public enum TerrainSculptMode
    {
        Flatten,
        Holes,
        Noise,
        Sculpt,
        Smooth
    }

    public enum BrushFallOfType
    {
        Smooth,
        Linear,
        Spherical,
        Tip
    }

    public enum TerrainBrushType
    {
        Circle
    }


    public struct VertexResult
    {
        public int i { get; set; }
        public Vector3 v { get; set; }
    }


}