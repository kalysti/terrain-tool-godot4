using Godot;
using System;
using TerrainEditor.Utils.Editor.Brush;

namespace TerrainEditor.Utils.Editor.Sculpt
{
    public class TerrainHoleSculpt : TerrainBaseSculpt
    {
        public TerrainHoleSculpt(Terrain3D _selectedTerrain, TerrainEditorInfo info) : base(_selectedTerrain, info)
        {
        }

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            var sourceHolesData = patch.CacheHeightData();

            var targetHeight = applyInfo.height;
            var strength = Saturate(editorStrength);

            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new byte[bufferSize];

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var xx = x + modifiedOffset.x;
                    var sourceMask = sourceHolesData[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceMask , zz * Terrain3D.UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);
                    samplePositionWorld.y = pos.y;

                    var paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFallofType, applyInfo.brushFallof, applyInfo.brushSize, pos, samplePositionWorld);

                    // Blend between the height and the target value
                    var id = z * modifiedSize.x + x;
                    buffer[id] = (byte)((sourceMask + paintAmount * strength) < 0.8f ? 0 : 255);
                }
            }

            patch.UpdateHolesMask(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}