using Godot;
using System;

namespace TerrainEditor.Utils.Editor.Brush
{
    public static class TerrainEditorBrush
    {

        public static float CalculateFalloff_Smooth(float distance, float radius, float falloff)
        {
            // Smooth-step linear falloff
            float alpha = CalculateFalloff_Linear(distance, radius, falloff);
            return alpha * alpha * (3 - 2 * alpha);
        }

        public static float CalculateFalloff_Linear(float distance, float radius, float falloff)
        {
            if (distance < radius)
                return 1.0f;
            else
            {
                if (falloff > 0)
                    return Mathf.Max(0.0f, 1.0f - (distance - radius) / falloff);
                else
                    return 0.0f;
            }
        }

        public static float CalculateFalloff_Spherical(float distance, float radius, float falloff)
        {
            if (distance <= radius)
            {
                return 1.0f;
            }

            if (distance > radius + falloff)
            {
                return 0.0f;
            }

            // Elliptical falloff
            return Mathf.Sqrt(1.0f - Mathf.Sqrt((distance - radius) / falloff));
        }

        public static float CalculateFalloff_Tip(float distance, float radius, float falloff)
        {
            if (distance <= radius)
            {
                return 1.0f;
            }

            if (distance > radius + falloff)
            {
                return 0.0f;
            }

            // Inverse elliptical falloff
            return 1.0f - Mathf.Sqrt(1.0f - Mathf.Sqrt((falloff + radius - distance) / falloff));
        }


        public static void DistanceXZ(Vector3 value1, Vector3 value2, out float result)
        {
            float x = value1.x - value2.x;
            float z = value1.z - value2.z;

            result = (float)Math.Sqrt(x * x + z * z);
        }
        public static float Sample(BrushFallOfType type, float brushFallof, float brushSize, Vector3 brushPosition, Vector3 samplePosition)
        {
            float distanceXZ = 0f;
            DistanceXZ(brushPosition, samplePosition, out distanceXZ);

            float halfSize = brushSize * 0.5f;
            float falloff = halfSize * brushFallof;
            float radius = halfSize - falloff;

            switch (type)
            {
                case BrushFallOfType.Smooth: return CalculateFalloff_Smooth(distanceXZ, radius, falloff);
                case BrushFallOfType.Linear: return CalculateFalloff_Linear(distanceXZ, radius, falloff);
                case BrushFallOfType.Spherical: return CalculateFalloff_Spherical(distanceXZ, radius, falloff);
                case BrushFallOfType.Tip: return CalculateFalloff_Tip(distanceXZ, radius, falloff);
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}