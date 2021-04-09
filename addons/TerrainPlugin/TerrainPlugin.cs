using System.Text;
using System.Security.Cryptography;
using System.Collections.Specialized;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;

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
        SpinBox patchXControl = new SpinBox();
        SpinBox patchYControl = new SpinBox();
        SpinBox importHeightScale = new SpinBox();

        OptionButton chunkSizeControl = new OptionButton();
        Button chooseTextureButton = new Button();

        FileDialog fileDialog = new FileDialog();


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

                if (@event is InputEventMouseMotion)
                {
                    var motion = @event as InputEventMouseMotion;
                    mousePosition = motion.Position;


                    //return true;
                }

            }
            else
            {
                handle_clicked = false;
            }

            return base.ForwardSpatialGuiInput(camera, @event);
        }


        /// <inheritdoc />
        public void SetMaterialParams(TerrainChunk chunk, Vector3 position, Color color)
        {
            // Data 0: XYZ: position, W: radius
            // Data 1: X: falloff, Y: type
            float halfSize = selectedTerrain.brushSize * 0.5f; // 2000
            float falloff = halfSize * selectedTerrain.brushFallof; // 1000
            float radius = halfSize - falloff; // 1000

            chunk.UpdateInspectorMaterial(color, new Plane(position, radius), new Plane(falloff, (float)selectedTerrain.brushFallOfType, 0, 0));
        }

        public void ResetMaterialParams(TerrainChunk chunk)
        {
            chunk.UpdateInspectorMaterial(new Color(), new Plane(Vector3.Zero, 0f), new Plane(Vector3.Zero, 0f));
        }

        public void DrawInspector(Vector3 pos, bool reset = false)
        {
            if (selectedTerrain != null && editorCamera != null)
            {
                TerrainChunk[] selectedChunks = null;
                if (reset == false)
                    selectedChunks = GetChunks(pos);

                foreach (var patch in selectedTerrain.terrainPatches)
                {
                    foreach (var chunk in patch.chunks)
                    {
                        if (selectedChunks == null || !selectedChunks.Contains(chunk))
                        {
                            ResetMaterialParams(chunk);
                        }
                        else
                        {
                            SetMaterialParams(chunk, pos, new Color(1.0f, 0.85f, 0.0f));
                        }
                    }
                }
            }
        }

        public override void _PhysicsProcess(float delta)
        {
            //sculping or painting
            if (selectedTerrain != null && editorCamera != null)
            {
                var start = OS.GetTicksMsec();
                var cast = DoRayCast(mousePosition);
                if (cast != Vector3.Inf)
                {
                    if (handle_clicked)
                        DoEditTerrain(cast, delta);

                    DrawInspector(cast);
                }

            }
        }

        protected Vector3 DoRayCast(Vector2 pos)
        {
            if (selectedTerrain != null && editorCamera != null)
            {
                var viewport = editorCamera.GetViewport() as SubViewport;
                var viewport_container = viewport.GetParent() as SubViewportContainer;

                var screen_pos = pos * viewport.Size / viewport_container.RectSize;

                var from = editorCamera.ProjectRayOrigin(screen_pos);
                var dir = editorCamera.ProjectRayNormal(screen_pos);

                var distance = editorCamera.Far * 1.2f;
                var space_state = selectedTerrain.GetWorld3d().DirectSpaceState;
                var result = space_state.IntersectRay(from, from + dir * distance);

                if (result.Count > 0 && result["rid"] != null && result["collider"] == selectedTerrain)
                {
                    var positionWorldSpace = (Vector3)result["position"];
                    return positionWorldSpace;
                }
            }

            return Vector3.Inf;
        }

        protected void DoEditTerrain(Vector3 pos, float delta)
        {
            bool EditHoles = false;

            float strength = selectedTerrain.toolStrength * delta;

            if (strength <= 0.0f)
                return;

            bool inverted = Input.IsKeyPressed((int)Key.Control);

            if (inverted)
            {
                strength *= -1;
            }

            var patches = GetPatches(pos);
            float brushExtentY = 10000.0f;
            float brushSizeHalf = selectedTerrain.brushSize * 0.5f;

            // Get brush bounds in terrain local space
            var bMin = selectedTerrain.ToLocal(new Vector3(pos.x - brushSizeHalf, pos.y - brushSizeHalf - brushExtentY, pos.z - brushSizeHalf));
            var bMax = selectedTerrain.ToLocal(new Vector3(pos.x + brushSizeHalf, pos.y + brushSizeHalf + brushExtentY, pos.z + brushSizeHalf));

            var start = OS.GetTicksMsec();

            foreach (var patch in patches)
            {
                var chunkSize = patch.info.chunkSize;

                var patchSize = chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX * Terrain3D.CHUNKS_COUNT_EDGE;
                var unitsPerVertexInv = 1.0f / Terrain3D.TERRAIN_UNITS_PER_VERTEX;

                var patchPositionLocal = new Vector3(patch.patchCoord.x * patchSize, 0, patch.patchCoord.y * patchSize);
                var brushBoundsPatchLocalMin = (bMin - patchPositionLocal) * unitsPerVertexInv;
                var brushBoundsPatchLocalMax = (bMax - patchPositionLocal) * unitsPerVertexInv;

                // Calculate patch heightmap area to modify by brush
                var brushPatchMin = new Vector2i(Mathf.FloorToInt(brushBoundsPatchLocalMin.x), Mathf.FloorToInt(brushBoundsPatchLocalMin.z));
                var brushPatchMax = new Vector2i(Mathf.CeilToInt(brushBoundsPatchLocalMax.x), Mathf.FloorToInt(brushBoundsPatchLocalMax.z));
                var modifiedOffset = brushPatchMin;
                var modifiedSize = brushPatchMax - brushPatchMin;

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

                if (selectedTerrain.toolSculptMode == TerrainSculptMode.Sculpt)
                {
                    ApplySculpt(inverted, patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
                }
                if (selectedTerrain.toolSculptMode == TerrainSculptMode.Flatten)
                {
                    ApplyFlatten(inverted, patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
                }
                if (selectedTerrain.toolSculptMode == TerrainSculptMode.Smooth)
                {
                    ApplySmooth(inverted, patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
                }


            }
        }

        public float Saturate(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }


        public void ApplySmooth(bool invert, TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            var radius = Mathf.Max(Mathf.CeilToInt(selectedTerrain.toolFilterRadius * 0.01f * selectedTerrain.brushSize), 2);
            var sourceHeightMap = patch.CacheHeightData();
            var strength = Saturate(editorStrength);
            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var id = z * modifiedSize.x + x;
                    var xx = x + modifiedOffset.x;

                    var sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.TERRAIN_UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);
                    var paintAmount = TerrainBrush.Sample(selectedTerrain.brushFallOfType, selectedTerrain.brushFallof, selectedTerrain.brushSize, pos, samplePositionWorld) * strength;
                    var max = patch.info.heightMapSize - 1;
                    if (paintAmount > 0)
                    {
                        // Blend between the height and the target value

                        float smoothValue = 0;
                        int smoothValueSamples = 0;
                        int minX = Math.Max(x - radius + modifiedOffset.x, 0);
                        int minZ = Math.Max(z - radius + modifiedOffset.y, 0);
                        int maxX = Math.Min(x + radius + modifiedOffset.x, max);
                        int maxZ = Math.Min(z + radius + modifiedOffset.y, max);
                        for (int dz = minZ; dz <= maxZ; dz++)
                        {
                            for (int dx = minX; dx <= maxX; dx++)
                            {
                                var height = sourceHeightMap[dz * patch.info.heightMapSize + dx];
                                smoothValue += height;
                                smoothValueSamples++;
                            }
                        }

                        // Normalize
                        smoothValue /= smoothValueSamples;

                        // Blend between the height and smooth value
                        buffer[id] = Mathf.Lerp(sourceHeight, smoothValue, paintAmount);
                    }
                    else
                    {
                        buffer[id] = sourceHeight;
                    }
                }
            }

            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }

        public void ApplyFlatten(bool invert, TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            var sourceHeightMap = patch.CacheHeightData();

            var targetHeight = selectedTerrain.toolTargetHeight;
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

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.TERRAIN_UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    var paintAmount = TerrainBrush.Sample(selectedTerrain.brushFallOfType, selectedTerrain.brushFallof, selectedTerrain.brushSize, pos, samplePositionWorld);

                    // Blend between the height and the target value
                    var id = z * modifiedSize.x + x;
                    buffer[id] = Mathf.Lerp(sourceHeight, targetHeight, paintAmount);
                }
            }

            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }
        public void ApplySculpt(bool invert, TerrainPatch patch, Vector3 pos, Vector3 patchPositionLocal, float editorStrength, Vector2i modifiedSize, Vector2i modifiedOffset)
        {
            var sourceHeightMap = patch.CacheHeightData();
            float strength = editorStrength * 1000.0f;

            var bufferSize = modifiedSize.y * modifiedSize.x;
            var buffer = new float[bufferSize];

            for (int z = 0; z < modifiedSize.y; z++)
            {
                var zz = z + modifiedOffset.y;
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    var xx = x + modifiedOffset.x;
                    var sourceHeight = sourceHeightMap[zz * patch.info.heightMapSize + xx];

                    var samplePositionLocal = patchPositionLocal + new Vector3(xx * Terrain3D.TERRAIN_UNITS_PER_VERTEX, sourceHeight, zz * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
                    var samplePositionWorld = selectedTerrain.ToGlobal(samplePositionLocal);

                    var paintAmount = TerrainBrush.Sample(selectedTerrain.brushFallOfType, selectedTerrain.brushFallof, selectedTerrain.brushSize, pos, samplePositionWorld);

                    var id = z * modifiedSize.x + x;
                    buffer[id] = sourceHeight + paintAmount * strength;
                }
            }
            patch.UpdateHeightMap(selectedTerrain, buffer, modifiedOffset, modifiedSize);
        }


        public TerrainPatch[] GetPatches(Vector3 hitPosition)
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

            return list.ToArray();
        }
        public TerrainChunk[] GetChunks(Vector3 hitPosition)
        {
            var list = new Godot.Collections.Array<TerrainChunk>();
            var cursorBrush = CursorBrushBounds(hitPosition);

            foreach (var patch in GetPatches(hitPosition))
            {
                foreach (var chunk in patch.chunks)
                {
                    if (chunk.getBounds(patch.info, patch.getOffset()).Intersects(cursorBrush))
                    {
                        list.Add(chunk);
                    }
                }
            }

            return list.ToArray();
        }

        public AABB CursorBrushBounds(Vector3 hitPosition)
        {
            float brushExtentY = 10000.0f;
            float brushSizeHalf = selectedTerrain.brushSize * 0.5f;

            var ab = new AABB(hitPosition, new Vector3(brushSizeHalf, (brushSizeHalf + brushExtentY), brushSizeHalf));

            return ab;
        }
        public void refreshEditor(string prop)
        {
            if (selectedTerrain == null) return;
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
            menuButton.GetPopup().AddItem("Import terrain");
            menuButton.Visible = false;
            menuButton.GetPopup().Connect("id_pressed", new Callable(this, "openCreateMenu"));

            AddControlToContainer(CustomControlContainer.SpatialEditorMenu, menuButton);
            AddSpatialGizmoPlugin(gizmoPlugin);

            createImportMenu();
        }

        public string heightMapPath = null;
        public void selectFilePath(string path)
        {
            heightMapPath = path;
        }

        public void openCreateMenu(int id)
        {
            heightMapPath = null;

            if (id == 0)
                OpenDialog();
        }

        public void OpenDialog()
        {
            createDialog.PopupCentered();
        }
        protected void createImportMenu()
        {
            AddChild(createDialog);
            AddChild(fileDialog);

            createDialog.Connect("confirmed", new Callable(this, "generateTerrain"));
            chooseTextureButton.Connect("pressed", new Callable(this, "fileDialogOpen"));

            fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            fileDialog.Connect("file_selected", new Callable(this, "selectFilePath"));
            fileDialog.AddFilter("*.png ; PNG Images");

            createDialog.Title = "Create a terrain";

            var vbox = new VBoxContainer();
            createDialog.AddChild(vbox);

            patchXControl.MinValue = 1;
            patchXControl.MaxValue = 10;
            patchXControl.Step = 1;

            patchYControl.MinValue = 1;
            patchYControl.MaxValue = 10;
            patchYControl.Step = 1;

            importHeightScale.MinValue = -10000;
            importHeightScale.MaxValue = 10000;
            importHeightScale.Step = 1;
            importHeightScale.Value = 5000;

            chunkSizeControl.AddItem("32", 0);
            chunkSizeControl.AddItem("64", 1);
            chunkSizeControl.AddItem("128", 2);
            chunkSizeControl.AddItem("256", 3);
            chunkSizeControl.Selected = 0;

            createMarginInput(vbox, "Patch X Size", patchYControl);
            createMarginInput(vbox, "Patch Y Size", patchXControl);
            createMarginInput(vbox, "Chunk Size", chunkSizeControl);
            createMarginInput(vbox, "Choose texture", chooseTextureButton);
            createMarginInput(vbox, "Import height scale", importHeightScale);
        }

        public void fileDialogOpen()
        {
            fileDialog.PopupCentered();
        }

        public void generateTerrain()
        {
            if (selectedTerrain != null)
            {
                var chunkSize = int.Parse(chunkSizeControl.GetItemText(chunkSizeControl.GetSelectedId()));
                var patchX = (int)patchXControl.Value;
                var patchY = (int)patchYControl.Value;

                var heightScale = (int)importHeightScale.Value;


                Image file = null;
                if (heightMapPath != null)
                {
                    file = new Image();
                    file.Load(heightMapPath);
                }


                selectedTerrain.Generate(patchX, patchY, chunkSize, heightScale, file);
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
            createDialog.Disconnect("confirmed", new Callable(this, "generateTerrain"));
            chooseTextureButton.Disconnect("pressed", new Callable(this, "fileDialogOpen"));
            fileDialog.Disconnect("file_selected", new Callable(this, "selectFilePath"));

            RemoveChild(createDialog);
            RemoveChild(fileDialog);
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
