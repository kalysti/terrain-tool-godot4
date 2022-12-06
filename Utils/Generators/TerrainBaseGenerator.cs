using System.Xml.Schema;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using Godot;
using System;
using System.Linq;
using System.IO;

using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
namespace TerrainEditor.Generators
{
    public abstract class TerrainBaseGenerator
    {
        protected TerrainPatch patch;
        public TerrainBaseGenerator(TerrainPatch _patch)
        {
            patch = _patch;

            if (patch == null)
                GD.PrintErr("Patch not initlizied");
        }

        public Image createImage()
        {
            var initData = Image.Create(patch.info.textureSize, patch.info.textureSize, false, Image.Format.Rgba8);

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
                byte[] destination = new byte[source.Length * Marshal.SizeOf(typeof(T))];
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
            T[] destination = new T[source.Length / Marshal.SizeOf(typeof(T))];
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
}
