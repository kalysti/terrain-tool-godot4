using Godot;
using System;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Sculpt
{
    public class TerrainSmoothSculpt : TerrainBaseSculpt
    {
        public TerrainSmoothSculpt(Terrain3D _selectedTerrain, TerrainEditorInfo info) : base(_selectedTerrain, info)
        {
        }

        public override void Apply( TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
        {
            int radius = Mathf.Max(Mathf.CeilToInt(applyInfo.radius * 0.01f * applyInfo.brushSize), 2);
            float[]? sourceHeightMap = patch.CacheHeightData();
            float strength = Saturate(editorStrength);
            int bufferSize = modifiedSize.Y * modifiedSize.X;
            var buffer = new float[bufferSize];

            for (int z = 0; z < modifiedSize.Y; z++)
            {
                int zz = z + modifiedOffset.Y;
                for (int x = 0; x < modifiedSize.X; x++)
                {
                    int id = z * modifiedSize.X + x;
                    int xx = x + modifiedOffset.X;

                    float sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    Vector3 samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);
                    float paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFalloffType, applyInfo.brushFalloff, applyInfo.brushSize, pos, samplePositionWorld) * strength;
                    int max = patch.info.heightMapSize - 1;
                    if (paintAmount > 0)
                    {
                        // Blend between the height and the target value

                        float smoothValue = 0;
                        int smoothValueSamples = 0;
                        int minX = Math.Max(x - radius + modifiedOffset.X, 0);
                        int minZ = Math.Max(z - radius + modifiedOffset.Y, 0);
                        int maxX = Math.Min(x + radius + modifiedOffset.X, max);
                        int maxZ = Math.Min(z + radius + modifiedOffset.Y, max);
                        for (int dz = minZ; dz <= maxZ; dz++)
                        {
                            for (int dx = minX; dx <= maxX; dx++)
                            {
                                float height = sourceHeightMap[dz * patch.info.heightMapSize + dx];
                                smoothValue += height;
                                smoothValueSamples++;
                            }
                        }

                        // Normalize
                        smoothValue /= smoothValueSamples;

                        // Blend between the height and smooth value
                        buffer[id] = Mathf.Lerp(sourceHeight, smoothValue, paintAmount);
                    }
                    else
                    {
                        buffer[id] = sourceHeight;
                    }
                }
            }

            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}