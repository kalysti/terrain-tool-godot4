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

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            float[]? sourceHeightMap = patch.CacheHeightData();
            float strength = editorStrength * 1000.0f;

            int bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            int patchSize = patch.info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES;
            Vector2i patchOffset = patch.patchCoord * patchSize;

            var noise = new PerlinNoise(0, applyInfo.noiseScale, editorStrength * applyInfo.noiseAmount);

            for (int z = 0; z < modifiedSize.y; z++)
            {
                int zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    int xx = x + modifiedOffset.x;
                    float sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    Vector3 samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    float noiseSample = noise.Sample(xx + patchOffset.x, zz + patchOffset.y);
                    float paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFalloffType, applyInfo.brushFalloff, applyInfo.brushSize, pos, samplePositionWorld);

                    int id = z * modifiedSize.x + x;
                    buffer[id] = sourceHeight + noiseSample * paintAmount;
                }
            }
            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}