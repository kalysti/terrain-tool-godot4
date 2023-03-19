using Godot;
using System;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Paint
{
    public class TerrainPaintPaint : TerrainBasePaint
    {
        public TerrainPaintPaint(Terrain3D _selectedTerrain, TerrainEditorInfo info) : base(_selectedTerrain, info)
        {
        }

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
        {
            int splatmapIndex = applyInfo.layer < 4 ? 0 : 1;

            Color[]? sourceSplatmap = patch.CacheSplatMap(splatmapIndex);
            float strength = editorStrength * 1000.0f;

            int bufferSize = modifiedSize.Y * modifiedSize.X;
            var buffer = new Color[bufferSize];

            int colorComponent = applyInfo.layer % 4;

            for (int z = 0; z < modifiedSize.Y; z++)
            {
                int zz = z + modifiedOffset.Y;
                for (int x = 0; x < modifiedSize.X; x++)
                {
                    int xx = x + modifiedOffset.X;
                    Color source = sourceSplatmap[zz * patch.info.heightMapSize + xx];

                    Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, 0, zz * Terrain3D.UNITS_PER_VERTEX);
                    Vector3 samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    float paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFalloffType, applyInfo.brushFalloff, applyInfo.brushSize, pos, samplePositionWorld) * applyInfo.strength;

                    int id = z * modifiedSize.X + x;

                    // buffer[id] = new Color(0, 0, 0, 1);
                    buffer[id] = source;

                    var dstWeight = 0f;

                    if (colorComponent == 0)
                    {
                        dstWeight = source.R + paintAmount;
                    }
                    else if (colorComponent == 1)
                    {
                        dstWeight = source.G + paintAmount;
                    }
                    else if (colorComponent == 2)
                    {
                        dstWeight = source.B + paintAmount;
                    }
                    else if (colorComponent == 3)
                    {
                        dstWeight = source.A + paintAmount;
                    }

                    if (dstWeight >= 1.0f)
                    {
                        buffer[id] = Colors.Transparent;
                    }

                    if (colorComponent == 0)
                    {
                        buffer[id].R = Mathf.Clamp(dstWeight, 0f, 1f);
                    }
                    else if (colorComponent == 1)
                    {
                        buffer[id].G = Mathf.Clamp(dstWeight, 0f, 1f);
                    }
                    else if (colorComponent == 2)
                    {
                        buffer[id].B = Mathf.Clamp(dstWeight, 0f, 1f);
                    }
                    else if (colorComponent == 3)
                    {
                        buffer[id].A = Mathf.Clamp(dstWeight, 0f, 1f);
                    }
                }
            }

            patch.UpdateSplatMap(splatmapIndex, selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}