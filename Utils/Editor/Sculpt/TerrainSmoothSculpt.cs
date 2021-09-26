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

        public override void Apply( TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            var radius = Mathf.Max(Mathf.CeilToInt(applyInfo.radius * 0.01f * applyInfo.brushSize), 2);
            var sourceHeightMap = patch.CacheHeightData();
            var strength = Saturate(editorStrength);
            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var id = z * modifiedSize.x + x;
                    var xx = x + modifiedOffset.x;

                    var sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);
                    var paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFallofType, applyInfo.brushFallof, applyInfo.brushSize, pos, samplePositionWorld) * strength;
                    var max = patch.info.heightMapSize - 1;
                    if (paintAmount > 0)
                    {
                        // Blend between the height and the target value

                        float smoothValue = 0;
                        int smoothValueSamples = 0;
                        int minX = Math.Max(x - radius + modifiedOffset.x, 0);
                        int minZ = Math.Max(z - radius + modifiedOffset.y, 0);
                        int maxX = Math.Min(x + radius + modifiedOffset.x, max);
                        int maxZ = Math.Min(z + radius + modifiedOffset.y, max);
                        for (int dz = minZ; dz <= maxZ; dz++)
                        {
                            for (int dx = minX; dx <= maxX; dx++)
                            {
                                var height = sourceHeightMap[dz * patch.info.heightMapSize + dx];
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