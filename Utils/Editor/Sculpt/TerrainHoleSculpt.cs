using Godot;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Sculpt;

public class TerrainHoleSculpt : TerrainBaseSculpt
{
    public TerrainHoleSculpt(Terrain3D selectedTerrain, TerrainEditorInfo info) : base(selectedTerrain, info)
    {
    }

    public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
    {
        float[] sourceHolesData = patch.CacheHeightData();

        // float targetHeight = ApplyInfo.Height;
        float strength = Saturate(editorStrength);

        int bufferSize = modifiedSize.y * modifiedSize.x;
        var buffer = new byte[bufferSize];

        for (var z = 0; z < modifiedSize.y; z++)
        {
            int zz = z + modifiedOffset.y;
            for (var x = 0; x < modifiedSize.x; x++)
            {
                int xx = x + modifiedOffset.x;
                float sourceMask = sourceHolesData[zz * patch.Info.HeightMapSize + xx];

                Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceMask, zz * Terrain3D.UNITS_PER_VERTEX);
                Vector3 samplePositionWorld = SelectedTerrain.ToGlobal(samplePositionLocal);
                samplePositionWorld.y = pos.y;

                float paintAmount = TerrainEditorBrush.Sample(ApplyInfo.BrushFalloffType, ApplyInfo.BrushFalloff, ApplyInfo.BrushSize, pos, samplePositionWorld);

                // Blend between the height and the target value
                int id = z * modifiedSize.x + x;
                buffer[id] = (byte)(sourceMask + paintAmount * strength < 0.8f ? 0 : 255);
            }
        }

        patch.UpdateHolesMask(SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}