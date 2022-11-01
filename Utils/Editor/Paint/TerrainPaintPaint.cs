using Godot;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Paint;

public class TerrainPaintPaint : TerrainBasePaint
{
    public TerrainPaintPaint(Terrain3D selectedTerrain, TerrainEditorInfo info) : base(selectedTerrain, info)
    {
    }

    public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
    {
        int splatmapIndex = ApplyInfo.Layer < 4 ? 0 : 1;

        Color[]? sourceSplatmap = patch.CacheSplatMap(splatmapIndex);
        float strength = editorStrength * 1000.0f;

        int bufferSize = modifiedSize.y * modifiedSize.x;
        var buffer = new Color[bufferSize];

        int colorComponent = ApplyInfo.Layer % 4;

        for (var z = 0; z < modifiedSize.y; z++)
        {
            int zz = z + modifiedOffset.y;
            for (var x = 0; x < modifiedSize.x; x++)
            {
                int xx = x + modifiedOffset.x;
                Color source = sourceSplatmap[zz * patch.Info.HeightMapSize + xx];

                Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, 0, zz * Terrain3D.UNITS_PER_VERTEX);
                Vector3 samplePositionWorld = SelectedTerrain.ToGlobal(samplePositionLocal);

                float paintAmount = TerrainEditorBrush.Sample(ApplyInfo.BrushFalloffType, ApplyInfo.BrushFalloff, ApplyInfo.BrushSize, pos, samplePositionWorld) * ApplyInfo.Strength;

                int id = z * modifiedSize.x + x;

                // buffer[id] = new Color(0, 0, 0, 1);
                buffer[id] = source;

                var dstWeight = 0f;

                if (colorComponent == 0)
                {
                    dstWeight = source.r + paintAmount;
                }
                else if (colorComponent == 1)
                {
                    dstWeight = source.g + paintAmount;
                }
                else if (colorComponent == 2)
                {
                    dstWeight = source.b + paintAmount;
                }
                else if (colorComponent == 3)
                {
                    dstWeight = source.a + paintAmount;
                }

                if (dstWeight >= 1.0f)
                {
                    buffer[id] = Colors.Transparent;
                }

                if (colorComponent == 0)
                {
                    buffer[id].r = Mathf.Clamp(dstWeight, 0f, 1f);
                }
                else if (colorComponent == 1)
                {
                    buffer[id].g = Mathf.Clamp(dstWeight, 0f, 1f);
                }
                else if (colorComponent == 2)
                {
                    buffer[id].b = Mathf.Clamp(dstWeight, 0f, 1f);
                }
                else if (colorComponent == 3)
                {
                    buffer[id].a = Mathf.Clamp(dstWeight, 0f, 1f);
                }
            }
        }

        patch.UpdateSplatMap(splatmapIndex, SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}