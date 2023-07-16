using Godot;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Paint;

public class TerrainPaintPaint : TerrainBasePaint
{
    public TerrainPaintPaint(Terrain3D selectedTerrain, TerrainEditorInfo info) : base(selectedTerrain, info)
    {
    }

    public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
    {
        int splatmapIndex = ApplyInfo.Layer < 4 ? 0 : 1;

        Color[] sourceSplatmap = patch.CacheSplatMap(splatmapIndex);
        // float strength = editorStrength * 1000.0f;

        int bufferSize = modifiedSize.Y * modifiedSize.X;
        var buffer = new Color[bufferSize];

        int colorComponent = ApplyInfo.Layer % 4;

        for (var z = 0; z < modifiedSize.Y; z++)
        {
            int zz = z + modifiedOffset.Y;
            for (var x = 0; x < modifiedSize.X; x++)
            {
                int xx = x + modifiedOffset.X;
                Color source = sourceSplatmap[zz * patch.Info.HeightMapSize + xx];

                Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, 0, zz * Terrain3D.UNITS_PER_VERTEX);
                Vector3 samplePositionWorld = SelectedTerrain.ToGlobal(samplePositionLocal);

                float paintAmount = TerrainEditorBrush.Sample(ApplyInfo.BrushFalloffType, ApplyInfo.BrushFalloff, ApplyInfo.BrushSize, pos, samplePositionWorld) * ApplyInfo.Strength;

                int id = z * modifiedSize.X + x;

                // buffer[id] = new Color(0, 0, 0, 1);
                buffer[id] = source;

                float dstWeight = colorComponent switch
                {
                    0 => source.R + paintAmount,
                    1 => source.G + paintAmount,
                    2 => source.B + paintAmount,
                    3 => source.A + paintAmount,
                    _ => 0f
                };

                if (dstWeight >= 1.0f) buffer[id] = Colors.Transparent;

                switch (colorComponent)
                {
                    case 0:
                        buffer[id].R = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                    case 1:
                        buffer[id].G = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                    case 2:
                        buffer[id].B = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                    case 3:
                        buffer[id].A = Mathf.Clamp(dstWeight, 0f, 1f);
                        break;
                }
            }
        }

        patch.UpdateSplatMap(splatmapIndex, SelectedTerrain, buffer, modifiedOffset, modifiedSize);
    }
}