using Godot;
using System;
using System.Runtime.InteropServices;

namespace TerrainEditor;

public enum TerrainToolMode
{
    SCULPT,
    PAINT,
    NONE
}

public enum TerrainSculptMode
{
    FLATTEN,
    HOLES,
    NOISE,
    SCULPT,
    SMOOTH
}

public enum BrushFallOffType
{
    SMOOTH,
    LINEAR,
    SPHERICAL,
    TIP
}

public enum TerrainBrushType
{
    CIRCLE
}

public struct VertexResult
{
    public int I { get; set; }
    public Vector3 V { get; set; }
}

public enum HeightmapAlgo
{
    R16,
    RGBA8_NORMAL,
    RGBA8_HALF,
    RGB8_FULL
};

public enum GiMode
{
    DISABLED,
    BAKED,
    DYNAMIC
};

public enum LightMapScale
{
    LIGHTMAP_SCALE_1_X,
    LIGHTMAP_SCALE_2_X,
    LIGHTMAP_SCALE_4_X,
    LIGHTMAP_SCALE_8_X,
    LIGHTMAP_SCALE_MAX
};

[StructLayout(LayoutKind.Sequential)]
public struct Rgba
{
    public byte r;
    public byte g;
    public byte b;
    public byte a;
}

[StructLayout(LayoutKind.Sequential)]
public struct R16
{
    public ushort r;
}