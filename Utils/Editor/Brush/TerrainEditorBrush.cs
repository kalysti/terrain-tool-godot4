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
            float x = value1.X - value2.X;
            float z = value1.Z - value2.Z;

            result = (float)Math.Sqrt(x * x + z * z);
        }
        public static float Sample(BrushFallOffType type, float brushFalloff, float brushSize, Vector3 brushPosition, Vector3 samplePosition)
        {
            DistanceXZ(brushPosition, samplePosition, out float distanceXZ);

            float halfSize = brushSize * 0.5f;
            float falloff = halfSize * brushFalloff;
            float radius = halfSize - falloff;

            return type switch
            {
                BrushFallOffType.Smooth => CalculateFalloff_Smooth(distanceXZ, radius, falloff),
                BrushFallOffType.Linear => CalculateFalloff_Linear(distanceXZ, radius, falloff),
                BrushFallOffType.Spherical => CalculateFalloff_Spherical(distanceXZ, radius, falloff),
                BrushFallOffType.Tip => CalculateFalloff_Tip(distanceXZ, radius, falloff),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}