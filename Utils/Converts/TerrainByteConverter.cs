
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

        public static float ReadNormalizedHeight16Bit(Color raw)
        {
            int test = raw.R8 | (raw.G8 << 8);
            UInt16 quantizedHeight = Convert.ToUInt16(test);

            float normalizedHeight = (float)quantizedHeight / UInt16.MaxValue;
            return normalizedHeight;
        }

        public static float ReadNormalizedHeightByte(RGBA raw)
        {
            int test = raw.r | (raw.g << 8);
            UInt16 quantizedHeight = Convert.ToUInt16(test);

            float normalizedHeight = (float)quantizedHeight / UInt16.MaxValue;
            return normalizedHeight;
        }
    }
}