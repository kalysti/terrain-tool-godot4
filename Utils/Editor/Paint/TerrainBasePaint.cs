using Godot;

namespace TerrainEditor.Utils.Editor.Paint;

public abstract class TerrainBasePaint
{
    protected Terrain3D SelectedTerrain;
    protected TerrainEditorInfo ApplyInfo;

    public TerrainBasePaint(Terrain3D selectedTerrain, TerrainEditorInfo info)
    {
        SelectedTerrain = selectedTerrain;
        ApplyInfo = info;
    }

    public abstract void Apply(TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2I modifiedSize, Vector2I modifiedOffset);

    protected float Saturate(float value)
    {
        if (value < 0f)
            return 0f;
        return value > 1f ? 1f : value;
    }
}