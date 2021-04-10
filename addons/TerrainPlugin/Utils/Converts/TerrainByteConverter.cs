
using Godot;
using System;
namespace TerrainEditor.Converters
{
    public static class TerrainByteConverter
    {
        public static bool ReadIsHoleByte(RGBA raw)
        {
            return (raw.b + raw.a) >= (int)(1.9f * byte.MaxValue);
        }

        public static float ReadNormalizedHeightByte(RGBA raw)
        {
            var test = raw.r | (raw.g << 8);
            UInt16 quantizedHeight = Convert.ToUInt16(test);

            float normalizedHeight = (float)quantizedHeight / UInt16.MaxValue;
            return normalizedHeight;
        }
    }
}