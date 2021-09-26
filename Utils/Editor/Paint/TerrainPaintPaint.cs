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

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            int splatmapIndex = applyInfo.layer < 4 ? 0 : 1;

            var sourceSplatmap = patch.CacheSplatMap(splatmapIndex);
            float strength = editorStrength * 1000.0f;

            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new Color[bufferSize];

            var colorComponent = applyInfo.layer % 4;

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var xx = x + modifiedOffset.x;
                    var source = sourceSplatmap[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, 0, zz * Terrain3D.UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    var paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFallofType, applyInfo.brushFallof, applyInfo.brushSize, pos, samplePositionWorld) * applyInfo.strength;

                    var id = z * modifiedSize.x + x;

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

            patch.UpdateSplatMap(splatmapIndex, selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}