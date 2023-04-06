using Godot;
using System;
using TerrainEditor;

namespace TerrainEditor.Utils.Editor.Sculpt
{
    public abstract class TerrainBaseSculpt
    {
        protected Terrain3D selectedTerrain;
        protected TerrainEditorInfo applyInfo;
        public TerrainBaseSculpt(Terrain3D _selectedTerrain, TerrainEditorInfo info)
        {
            selectedTerrain = _selectedTerrain;
            applyInfo = info;
        }
        public abstract void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset);
        protected float Saturate(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

    }
}