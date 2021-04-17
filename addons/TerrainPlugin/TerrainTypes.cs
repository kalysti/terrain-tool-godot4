using System.ComponentModel;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;
using System.Runtime.InteropServices;

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

    public enum GIMode
    {
        Disabled,
        Baked,
        Dynamic
    };

    public enum LightmapScale
    {
        LIGHTMAP_SCALE_1X,
        LIGHTMAP_SCALE_2X,
        LIGHTMAP_SCALE_4X,
        LIGHTMAP_SCALE_8X,
        LIGHTMAP_SCALE_MAX,
    };


    [StructLayout(LayoutKind.Sequential)]
    public struct RGBA
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;
    }


}