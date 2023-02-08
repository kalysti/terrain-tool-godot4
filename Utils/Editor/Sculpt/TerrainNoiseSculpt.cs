using Godot;
using TerrainEditor.Utils.Editor.Brush;
using TerrainEditor.Editor.Utilities;

namespace TerrainEditor.Utils.Editor.Sculpt;

public class TerrainNoiseSculpt : TerrainBaseSculpt
{
    public TerrainNoiseSculpt(Terrain3D selectedTerrain, TerrainEditorInfo info) : base(selectedTerrain, info)
    {
    }

    public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
    {
        float[] sourceHeightMap = patch.CacheHeightData();
        // float strength = editorStrength * 1000.0f;

        int bufferSize = modifiedSize.Y * modifiedSize.X;
        var buffer = new float[bufferSize];

        int patchSize = patch.Info.ChunkSize * Terrain3D.PATCH_CHUNK_EDGES;
        Vector2I patchOffset = patch.PatchCoordinates * patchSize;

        var noise = new PerlinNoise(0, ApplyInfo.NoiseScale, editorStrength * ApplyInfo.NoiseAmount);

        for (var z = 0; z < modifiedSize.Y; z++)
        {
            int zz = z + modifiedOffset.Y;
            for (var x = 0; x < modifiedSize.X; x++)
            {
                int xx = x + modifiedOffset.X;
                float sourceHeight = sourceHeightMap[zz * patch.Info.HeightMapSize + xx];

                Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                Vector3 samplePositionWorld = SelectedTerrain.ToGlobal(samplePositionLocal);

                float noiseSample = noise.Sample(xx + patchOffset.X, zz + patchOffset.Y);
                float paintAmount = TerrainEditorBrush.Sample(ApplyInfo.BrushFalloffType, ApplyInfo.BrushFalloff, ApplyInfo.BrushSize, pos, samplePositionWorld);

                int id = z * modifiedSize.X + x;
                buffer[id] = sourceHeight + noiseSample * paintAmount;
            }
        }

        patch.UpdateHeightMap(SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}