using Godot;
using System;
using TerrainEditor.Utils.Editor.Brush;
using TerrainEditor.Editor.Utilities;

namespace TerrainEditor.Utils.Editor.Sculpt
{
    public class TerrainNoiseSculpt : TerrainBaseSculpt
    {
        public TerrainNoiseSculpt(Terrain3D _selectedTerrain, TerrainEditorInfo info) : base(_selectedTerrain, info)
        {
        }

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
        {
            float[]? sourceHeightMap = patch.CacheHeightData();
            float strength = editorStrength * 1000.0f;

            int bufferSize = modifiedSize.Y * modifiedSize.X;
            var buffer = new float[bufferSize];

            int patchSize = patch.info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES;
            Vector2I patchOffset = patch.patchCoord * patchSize;

            var noise = new PerlinNoise(0, applyInfo.noiseScale, editorStrength * applyInfo.noiseAmount);

            for (int z = 0; z < modifiedSize.Y; z++)
            {
                int zz = z + modifiedOffset.Y;
                for (int x = 0; x < modifiedSize.X; x++)
                {
                    int xx = x + modifiedOffset.X;
                    float sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    Vector3 samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    float noiseSample = noise.Sample(xx + patchOffset.X, zz + patchOffset.Y);
                    float paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFalloffType, applyInfo.brushFalloff, applyInfo.brushSize, pos, samplePositionWorld);

                    int id = z * modifiedSize.X + x;
                    buffer[id] = sourceHeight + noiseSample * paintAmount;
                }
            }
            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}