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

        public override void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset)
        {
            float[]? sourceHolesData = patch.CacheHeightData();

            float targetHeight = applyInfo.height;
            float strength = Saturate(editorStrength);

            int bufferSize = modifiedSize.Y * modifiedSize.X;
            var buffer = new byte[bufferSize];

            for (int z = 0; z < modifiedSize.Y; z++)
            {
                int zz = z + modifiedOffset.Y;
                for (int x = 0; x < modifiedSize.X; x++)
                {
                    int xx = x + modifiedOffset.X;
                    float sourceMask = sourceHolesData[zz * patch.info.heightMapSize + xx];

                    Vector3 samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.UNITS_PER_VERTEX, sourceMask , zz * Terrain3D.UNITS_PER_VERTEX);
                    Vector3 samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);
                    samplePositionWorld.Y = pos.Y;

                    float paintAmount = TerrainEditorBrush.Sample(applyInfo.brushFalloffType, applyInfo.brushFalloff, applyInfo.brushSize, pos, samplePositionWorld);

                    // Blend between the height and the target value
                    int id = z * modifiedSize.X + x;
                    buffer[id] = (byte)((sourceMask + paintAmount * strength) < 0.8f ? 0 : 255);
                }
            }

            patch.UpdateHolesMask(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
    }
}