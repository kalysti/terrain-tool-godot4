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
            var sourceHeightMap = patch.CacheHeightData();

            var targetHeight = applyInfo.height;
            var strength = Saturate(editorStrength);

            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var xx = x + modifiedOffset.x;
                    var sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    var paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFallofType, applyInfo.brushFallof, applyInfo.brushSize, pos, samplePositionWorld);

                    // Blend between the height and the target value
                    var id = z * modifiedSize.x + x;
                    buffer[id] = Mathf.Lerp(sourceHeight, targetHeight, paintAmount);
                }
            }

            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}