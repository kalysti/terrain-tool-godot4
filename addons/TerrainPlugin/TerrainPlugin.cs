using Godot;
using System;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainPlugin : EditorPlugin
    {

        public ConfirmationDialog createDialog = new ConfirmationDialog();
        public Terrain3D selectedTerrain = null;

        protected TerrainGizmoPlugin gizmoPlugin = new TerrainGizmoPlugin();

        private bool handle_clicked = false;


        public TerrainSculptMode currentSculpMode = TerrainSculptMode.Sculpt;
        public TerrainToolMode currentToolMode = TerrainToolMode.None;



        private Vector2 mousePosition = Vector2.Zero;
        private Camera3D editorCamera = null;
        private MenuButton menuButton = new MenuButton();

        private SpinBox patches_x = new SpinBox();
        private SpinBox patches_z = new SpinBox();
        private OptionButton chunkSize = new OptionButton();

        public override bool ForwardSpatialGuiInput(Camera3D camera, InputEvent @event)
        {
            editorCamera = camera;

            if (selectedTerrain == null)
                return false;

            if (selectedTerrain.toolMode != TerrainToolMode.None)
            {

                if (@event is InputEventMouseButton)
                {
                    var button = @event as InputEventMouseButton;

                    if (button.IsPressed() && button.ButtonIndex == (int)MouseButton.Left)
                    {
                        handle_clicked = true;
                        mousePosition = button.Position;
                        return true;
                    }
                    else
                    {
                        handle_clicked = false;
                    }
                }

                else if (@event is InputEventMouseMotion && handle_clicked)
                {
                    var motion = @event as InputEventMouseMotion;
                    mousePosition = motion.Position;
                    return true;
                }
            }
            else
            {
                handle_clicked = false;
            }

            return base.ForwardSpatialGuiInput(camera, @event);
        }

        public override void _PhysicsProcess(float delta)
        {
            //sculping or painting
            if (selectedTerrain != null && editorCamera != null && handle_clicked)
            {
                var viewport = editorCamera.GetViewport() as SubViewport;
                var viewport_container = viewport.GetParent() as SubViewportContainer;

                var screen_pos = mousePosition * viewport.Size / viewport_container.RectSize;

                var from = editorCamera.ProjectRayOrigin(screen_pos);
                var dir = editorCamera.ProjectRayNormal(screen_pos);

                var distance = editorCamera.Far * 1.2f;
                var space_state = selectedTerrain.GetWorld3d().DirectSpaceState;
                var result = space_state.IntersectRay(from, from + dir * distance);

                if (result.Count > 0 && result["rid"] != null && result["collider"] != null)
                {
                    var pos = (Vector3)result["position"];
                    ApplySculpt(pos, delta);
                }
                else
                {
                    GD.Print("no collider found");
                }
            }
        }

        protected void ApplySculpt(Vector3 pos, float delta)
        {
            bool EditHoles = false;

            float strength = selectedTerrain.toolStrength * delta;

            if (strength <= 0.0f)
                return;

            var patches = GetPatches(pos);
            float brushExtentY = 10000.0f;
            float brushSizeHalf = selectedTerrain.brushSize * 0.5f;

            // Get brush bounds in terrain local space
            var bMin = selectedTerrain.ToLocal(new Vector3(pos.x - brushSizeHalf, pos.y - brushSizeHalf - brushExtentY, pos.z - brushSizeHalf));
            var bMax = selectedTerrain.ToLocal(new Vector3(pos.x + brushSizeHalf, pos.y + brushSizeHalf + brushExtentY, pos.z + brushSizeHalf));

            GD.Print(bMin + " min to " + bMax);
            foreach (var patch in patches)
            {
                GD.Print("found patch");
                var chunkSize = patch.info.chunkSize;
                var heightmapLength = patch.info.heightMapSize * patch.info.heightMapSize;
                var patchSize = chunkSize * Terrain3D.CHUNKS_COUNT_EDGE * Terrain3D.CHUNKS_COUNT_EDGE;
                var unitsPerVertexInv = 1.0f / Terrain3D.TERRAIN_UNITS_PER_VERTEX;

                var patchPositionLocal = new Vector3(patch.patchCoord.x * patchSize, 0, patch.patchCoord.y * patchSize);

                var brushBoundsPatchLocalMin = (bMin - patchPositionLocal) * unitsPerVertexInv;
                var brushBoundsPatchLocalMax = (bMax - patchPositionLocal) * unitsPerVertexInv;

                // Calculate patch heightmap area to modify by brush
                var brushPatchMin = new Vector2i(Mathf.FloorToInt(brushBoundsPatchLocalMin.x), Mathf.FloorToInt(brushBoundsPatchLocalMin.z));
                var brushPatchMax = new Vector2i(Mathf.CeilToInt(brushBoundsPatchLocalMax.x), Mathf.FloorToInt(brushBoundsPatchLocalMax.z));
                var modifiedOffset = brushPatchMin;
                var modifiedSize = brushPatchMax - brushPatchMin;
                GD.Print("size " + modifiedOffset);
                GD.Print("offset " + modifiedSize);

                // Expand the modification area by one vertex in each direction to ensure normal vectors are updated for edge cases, also clamp to prevent overflows
                if (modifiedOffset.x < 0)
                {
                    modifiedSize.x += modifiedOffset.x;
                    modifiedOffset.x = 0;
                }

                if (modifiedOffset.y < 0)
                {
                    modifiedSize.y += modifiedOffset.y;
                    modifiedOffset.y = 0;
                }

                modifiedSize.x = Mathf.Min(modifiedSize.x + 2, patch.info.heightMapSize - modifiedOffset.x);
                modifiedSize.y = Mathf.Min(modifiedSize.y + 2, patch.info.heightMapSize - modifiedOffset.y);

                // Skip patch won't be modified at all
                if (modifiedSize.x <= 0 || modifiedSize.y <= 0)
                    continue;

                var sourceHeightMap = patch.heightMapCachedData;

                strength = strength * 1000.0f;
                //issue??????? (check)

                var bufferSize = modifiedSize.y * modifiedSize.x;
                var buffer = new float[bufferSize];

                GD.Print("size; " + bufferSize);
                for (int z = 0; z < modifiedSize.y; z++)
                {
                    var zz = z + modifiedSize.y;
                    for (int x = 0; x < modifiedSize.x; x++)
                    {
                        var xx = x + modifiedOffset.x;
                        var sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                        var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.TERRAIN_UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
                        var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                        GD.Print("pos: " + pos + " vs " + samplePositionWorld + " pp " + patchPositionLocal);

                        var paintAmount = TerrainBrush.Sample(selectedTerrain.brushFallOfType, selectedTerrain.brushFallof, selectedTerrain.brushSize, pos, samplePositionWorld);

                        if (paintAmount > 0)
                        {
                            GD.Print(paintAmount);
                        }
                        var id = z * modifiedSize.x + x;
                        buffer[id] = sourceHeight + paintAmount * strength;
                    }
                }

                patch.UpdateHeightMap(new Godot.Collections.Array<float>(buffer), modifiedOffset, modifiedSize);
            }
        }


        public Godot.Collections.Array<TerrainPatch> GetPatches(Vector3 hitPosition)
        {
            var list = new Godot.Collections.Array<TerrainPatch>();
            var cursorBrush = CursorBrushBounds(hitPosition);

            foreach (var patch in selectedTerrain.terrainPatches)
            {
                if (patch.getBounds().Intersects(cursorBrush))
                {
                    list.Add(patch);
                }
            }

            return list;
        }

        public AABB CursorBrushBounds(Vector3 hitPosition)
        {
            float brushExtentY = 10000.0f;
            float brushSizeHalf = selectedTerrain.brushSize * 0.5f;

            var ab = new AABB(hitPosition, new Vector3(brushSizeHalf, brushSizeHalf + brushExtentY, brushSizeHalf));

            return ab;
        }
        public void refreshEditor(string prop)
        {
            if (currentSculpMode != selectedTerrain.toolSculptMode || currentToolMode != selectedTerrain.toolMode)
            {
                currentSculpMode = selectedTerrain.toolSculptMode;
                currentToolMode = selectedTerrain.toolMode;

                selectedTerrain.NotifyPropertyListChanged();
            }

        }
        public override void _EnterTree()
        {
            var editor_interface = GetEditorInterface();
            var inscpector = editor_interface.GetInspector();
            inscpector.Connect("property_edited", new Callable(this, "refreshEditor"), null, (uint)ConnectFlags.Deferred);
            var base_control = editor_interface.GetBaseControl();

            var script = GD.Load<Script>("res://addons/TerrainPlugin/Terrain3D.cs");
            var scriptPatch = GD.Load<Script>("res://addons/TerrainPlugin/TerrainPatch.cs");
            var scriptPatchhInfo = GD.Load<Script>("res://addons/TerrainPlugin/TerrainPatchInfo.cs");
            var scriptChunk = GD.Load<Script>("res://addons/TerrainPlugin/TerrainChunk.cs");
            var texture = GD.Load<Texture2D>("res://addons/TerrainPlugin/icons/test.png");

            AddCustomType("TerrainPatchInfo", "Resource", scriptPatchhInfo, texture);
            AddCustomType("TerrainPatch", "Resource", scriptPatch, texture);
            AddCustomType("TerrainChunk", "Resource", scriptChunk, texture);
            AddCustomType("Terrain3D", "Node3D", script, texture);

            menuButton.SwitchOnHover = true;
            menuButton.Text = "Terrain";
            menuButton.Icon = texture;
            menuButton.GetPopup().AddItem("Create terrain");
            menuButton.Visible = false;
            menuButton.GetPopup().Connect("id_pressed", new Callable(this, "openCreateMenu"));

            AddControlToContainer(CustomControlContainer.SpatialEditorMenu, menuButton);
            AddSpatialGizmoPlugin(gizmoPlugin);


        }
        public void openCreateMenu(int id)
        {
            creationConformed();
        }

        public void creationConformed()
        {
            if (selectedTerrain != null)
            {
                selectedTerrain.Generate(1, 1, 32, GetEditorInterface());
            }
        }
        private void createMarginInput(VBoxContainer vbox, string text, Control control)
        {
            var margin = new MarginContainer();
            var label = new Label();
            label.Text = text;

            margin.AddThemeConstantOverride("margin_left", 0);
            margin.AddChild(control);

            vbox.AddChild(label);
            vbox.AddChild(margin);
        }
        public override void _ExitTree()
        {

            RemoveChild(createDialog);
            RemoveCustomType("Terrain3D");
            RemoveCustomType("TerrainPatch");
            RemoveCustomType("TerrainPatchInfo");
            RemoveCustomType("TerrainChunk");

            RemoveSpatialGizmoPlugin(gizmoPlugin);
            RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, menuButton);
        }

        public override bool Handles(Godot.Object @object)
        {
            return @object != null && (@object is Terrain3D);
        }
        public override void MakeVisible(bool visible)
        {
            if (!visible)
                Edit(null);
        }
        public override void Edit(Godot.Object @object)
        {
            if (@object != null && @object is Terrain3D)
            {
                menuButton.Visible = true;
                selectedTerrain = @object as Terrain3D;
                selectedTerrain.root = GetEditorInterface().GetEditedSceneRoot();
                selectedTerrain.NotifyPropertyListChanged();
            }
            else
            {
                menuButton.Visible = false;
                selectedTerrain = null;
                handle_clicked = false;
            }
        }

    }
}
