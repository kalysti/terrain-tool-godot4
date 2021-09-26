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
            var sourceHeightMap = patch.CacheHeightData();
            float strength = editorStrength * 1000.0f;

            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            var patchSize = patch.info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES;
            var patchOffset = patch.patchCoord * patchSize;

            var noise = new PerlinNoise(0, applyInfo.noiseScale, editorStrength * applyInfo.noiseAmount);

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var xx = x + modifiedOffset.x;
                    var sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    var noiseSample = noise.Sample(xx + patchOffset.x, zz + patchOffset.y);
                    var paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFallofType, applyInfo.brushFallof, applyInfo.brushSize, pos, samplePositionWorld);

                    var id = z * modifiedSize.x + x;
                    buffer[id] = sourceHeight + noiseSample * paintAmount;
                }
            }
            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}