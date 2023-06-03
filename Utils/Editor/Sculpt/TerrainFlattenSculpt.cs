using Godot;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Sculpt;

public class TerrainFlattenSculpt : TerrainBaseSculpt
{
    public TerrainFlattenSculpt(Terrain3D selectedTerrain, TerrainEditorInfo info) : base(selectedTerrain, info)
    {
    }

    public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
    {
        float[] sourceHeightMap = patch.CacheHeightData();

        float targetHeight = ApplyInfo.Height;
        // float strength = Saturate(editorStrength);

        int bufferSize = modifiedSize.Y * modifiedSize.X;
        var buffer = new float[bufferSize];

        for (var z = 0; z < modifiedSize.Y; z++)
        {
            int zz = z + modifiedOffset.Y;
            for (var x = 0; x < modifiedSize.X; x++)
            {
                int xx = x + modifiedOffset.X;
                float sourceHeight = sourceHeightMap[zz * patch.Info.HeightMapSize + xx];

                Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                Vector3 samplePositionWorld = SelectedTerrain.ToGlobal(samplePositionLocal);

                float paintAmount = TerrainEditorBrush.Sample(ApplyInfo.BrushFalloffType, ApplyInfo.BrushFalloff, ApplyInfo.BrushSize, pos, samplePositionWorld);

                // Blend between the height and the target value
                int id = z * modifiedSize.X + x;
                buffer[id] = Mathf.Lerp(sourceHeight, targetHeight, paintAmount);
            }
        }

        patch.UpdateHeightMap(SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}