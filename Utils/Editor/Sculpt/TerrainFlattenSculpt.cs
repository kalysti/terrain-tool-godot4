using Godot;
using System;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Sculpt
{
    public class TerrainFlattenSculpt : TerrainBaseSculpt
    {
        public TerrainFlattenSculpt(Terrain3D _selectedTerrain, TerrainEditorInfo info) : base(_selectedTerrain, info)
        {
        }

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            float[]? sourceHeightMap = patch.CacheHeightData();

            float targetHeight = applyInfo.height;
            float strength = Saturate(editorStrength);

            int bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            for (int z = 0; z < modifiedSize.y; z++)
            {
                int zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    int xx = x + modifiedOffset.x;
                    float sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    Vector3 samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    float paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFalloffType, applyInfo.brushFalloff, applyInfo.brushSize, pos, samplePositionWorld);

                    // Blend between the height and the target value
                    int id = z * modifiedSize.x + x;
                    buffer[id] = Mathf.Lerp(sourceHeight, targetHeight, paintAmount);
                }
            }

            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}