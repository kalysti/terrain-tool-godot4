using Godot;
using System;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Sculpt;

public class TerrainSmoothSculpt : TerrainBaseSculpt
{
    public TerrainSmoothSculpt(Terrain3D selectedTerrain, TerrainEditorInfo info) : base(selectedTerrain, info)
    {
    }

    public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
    {
        int radius = Mathf.Max(Mathf.CeilToInt(ApplyInfo.Radius * 0.01f * ApplyInfo.BrushSize), 2);
        float[] sourceHeightMap = patch.CacheHeightData();
        float strength = Saturate(editorStrength);
        int bufferSize = modifiedSize.Y * modifiedSize.X;
        var buffer = new float[bufferSize];

        for (var z = 0; z < modifiedSize.Y; z++)
        {
            int zz = z + modifiedOffset.Y;
            for (var x = 0; x < modifiedSize.X; x++)
            {
                int id = z * modifiedSize.X + x;
                int xx = x + modifiedOffset.X;

                float sourceHeight = sourceHeightMap[zz * patch.Info.HeightMapSize + xx];

                Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                Vector3 samplePositionWorld = SelectedTerrain.ToGlobal(samplePositionLocal);
                float paintAmount = TerrainEditorBrush.Sample(ApplyInfo.BrushFalloffType, ApplyInfo.BrushFalloff, ApplyInfo.BrushSize, pos, samplePositionWorld) * strength;
                int max = patch.Info.HeightMapSize - 1;
                if (paintAmount > 0)
                {
                    // Blend between the height and the target value

                    float smoothValue = 0;
                    var smoothValueSamples = 0;
                    int minX = Math.Max(x - radius + modifiedOffset.X, 0);
                    int minZ = Math.Max(z - radius + modifiedOffset.Y, 0);
                    int maxX = Math.Min(x + radius + modifiedOffset.X, max);
                    int maxZ = Math.Min(z + radius + modifiedOffset.Y, max);
                    for (int dz = minZ; dz <= maxZ; dz++)
                    for (int dx = minX; dx <= maxX; dx++)
                    {
                        float height = sourceHeightMap[dz * patch.Info.HeightMapSize + dx];
                        smoothValue += height;
                        smoothValueSamples++;
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

        patch.UpdateHeightMap(SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}