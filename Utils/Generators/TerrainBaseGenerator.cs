using System.Runtime.InteropServices;
using Godot;
using System;

namespace TerrainEditor.Generators;

public abstract class TerrainBaseGenerator
{
    protected TerrainPatch Patch;

    protected TerrainBaseGenerator(TerrainPatch patch)
    {
        Patch = patch;

        if (Patch == null)
            GD.PrintErr("Patch not initlizied");
    }

    public Image CreateImage()
    {
        var initData = new Image();
        initData.Create(Patch.Info.TextureSize, Patch.Info.TextureSize, false, Image.Format.Rgba8);

        return initData;
    }

    /**
         * Convert RGBA Buffer to T buffer (faster reading)
         **/
    public static byte[] ToByteArray<T>(T[] source) where T : struct
    {
        GCHandle handle = GCHandle.Alloc(source, GCHandleType.Pinned);
        try
        {
            IntPtr pointer = handle.AddrOfPinnedObject();
            var destination = new byte[source.Length * Marshal.SizeOf(typeof(T))];
            Marshal.Copy(pointer, destination, 0, destination.Length);
            return destination;
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    /**
         * Convert byte Buffer to T buffer (faster writing)
         **/
    public static T[] FromByteArray<T>(byte[] source) where T : struct
    {
        var destination = new T[source.Length / Marshal.SizeOf(typeof(T))];
        GCHandle handle = GCHandle.Alloc(destination, GCHandleType.Pinned);
        try
        {
            IntPtr pointer = handle.AddrOfPinnedObject();
            Marshal.Copy(source, 0, pointer, source.Length);
            return destination;
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}