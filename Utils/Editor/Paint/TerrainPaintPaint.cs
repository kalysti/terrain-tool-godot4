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

        Color[] sourceSplatmap = patch.CacheSplatMap(splatmapIndex);
        // float strength = editorStrength * 1000.0f;

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

                float dstWeight = colorComponent switch
                {
                    0 => source.r + paintAmount,
                    1 => source.g + paintAmount,
                    2 => source.b + paintAmount,
                    3 => source.a + paintAmount,
                    _ => 0f
                };

                if (dstWeight >= 1.0f) buffer[id] = Colors.Transparent;

                switch (colorComponent)
                {
                    case 0:
                        buffer[id].r = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                    case 1:
                        buffer[id].g = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                    case 2:
                        buffer[id].b = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                    case 3:
                        buffer[id].a = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                }
            }
        }

        patch.UpdateSplatMap(splatmapIndex, SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}