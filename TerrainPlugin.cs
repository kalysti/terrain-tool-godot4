using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Specialized;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;
using TerrainEditor.Utils.Editor;
using TerrainEditor.Utils.Editor.Brush;
using TerrainEditor.Utils.Editor.Sculpt;
using TerrainEditor.Utils.Editor.Paint;

namespace TerrainEditor
{
	[Tool]
	public partial class TerrainPlugin : EditorPlugin
	{
		protected Terrain3D selectedTerrain = null;
		protected ConfirmationDialog createDialog = new ConfirmationDialog();
		protected TerrainGizmoPlugin gizmoPlugin = new TerrainGizmoPlugin();
		private bool handle_clicked = false;
		protected bool dockAttached = false;
		protected TerrainSculptMode currentSculpMode = TerrainSculptMode.Sculpt;
		protected TerrainToolMode currentToolMode = TerrainToolMode.None;

		private Vector2 mousePosition = Vector2.Zero;
		private Camera3D editorCamera = null;
		private MenuButton menuButton = new MenuButton();
		private SpinBox patchXControl = new SpinBox();
		private SpinBox patchYControl = new SpinBox();
		private SpinBox importHeightScale = new SpinBox();
		private OptionButton chunkSizeControl = new OptionButton();
		private OptionButton heightmapAlgoControl = new OptionButton();
		private Button chooseTextureButton = new Button();
		private Button chooseTextureSplatmap1Button = new Button();
		private Button chooseTextureSplatmap2Button = new Button();
		private FileDialog fileDialog = new FileDialog();
		private FileDialog fileDialogSplatmap1 = new FileDialog();
		private FileDialog fileDialogSplatmap2 = new FileDialog();
		private FileDialog fileDialogExport = new FileDialog();
		private VBoxContainer editorPanel = new VBoxContainer();
		protected Godot.Collections.Dictionary<string, Control> panelControls = new Godot.Collections.Dictionary<string, Control>();
		protected string heightMapPath = null;
		protected string splatmapPath1 = null;
		protected string splatmapPath2 = null;

		public override bool _Forward3dGuiInput(Camera3D camera, InputEvent @event)
		{
			editorCamera = camera;

			if (selectedTerrain == null)
				return false;

			if (currentToolMode != TerrainToolMode.None)
			{
				if (@event is InputEventMouseButton)
				{
					var button = @event as InputEventMouseButton;
					if (button.IsPressed() && button.ButtonIndex == MouseButton.Left)
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
				}

			}
			else
			{
				handle_clicked = false;
			}

			return base._Forward3dGuiInput(camera, @event);
		}


		/// <inheritdoc />
		protected void SetMaterialParams(TerrainEditorInfo info, TerrainChunk chunk, Vector3 position, Color color)
		{

			// Data 0: XYZ: position, W: radius
			// Data 1: X: falloff, Y: type
			float halfSize = info.brushSize * 0.5f; // 2000
			float falloff = halfSize * info.brushFallof; // 1000
			float radius = halfSize - falloff; // 1000

			chunk.UpdateInspectorMaterial(color, new Plane(position, radius), new Plane(falloff, (float)info.brushFallofType, 0, 0));
		}

		protected void ResetMaterialParams(TerrainChunk chunk)
		{
			chunk.UpdateInspectorMaterial(new Color(), new Plane(Vector3.Zero, 0f), new Plane(Vector3.Zero, 0f));
		}

		protected void DrawInspector(Vector3 pos, bool reset = false)
		{
			if (selectedTerrain != null && selectedTerrain.IsInsideTree() && editorCamera != null)
			{
				var applyInformation = getEditorApply();
				var cursorBrush = CursorBrushBounds(applyInformation, pos);

				TerrainChunk[] selectedChunks = null;
				if (reset == false)
					selectedChunks = GetChunks(cursorBrush);

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
							SetMaterialParams(applyInformation, chunk, pos, new Color(1.0f, 0.85f, 0.0f));
						}
					}
				}
			}
		}

		public override void _PhysicsProcess(float delta)
		{
			//sculping or painting
			if (selectedTerrain != null && editorCamera != null && selectedTerrain.IsInsideTree())
			{


				var start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;;
				var cast = DoRayCast(mousePosition);

				if (cast != Vector3.Inf)
				{
					if (handle_clicked)
					{
						DoEditTerrain(cast, delta);
					}
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

				if (result.Count > 0 && result["collider"] != null)
				{
					if (result["collider"] == selectedTerrain)
						return (Vector3)result["position"];

				}
			}

			return Vector3.Inf;
		}

		protected void DoEditTerrain(Vector3 pos, float delta)
		{
			var applyInformation = getEditorApply();
			float strength = applyInformation.strength * delta;

			if (strength <= 0.0f)
				return;

			applyInformation.inverse = Input.IsKeyPressed(Key.Ctrl);
			if (applyInformation.inverse)
			{
				applyInformation.strength *= -1;
			}
			var cursorBrush = CursorBrushBounds(applyInformation, pos);
			var patches = GetPatches(cursorBrush);
			float brushExtentY = 10000.0f;
			float brushSizeHalf = applyInformation.brushSize * 0.5f;

			// Get brush bounds in terrain local space
			var bMin = selectedTerrain.ToLocal(new Vector3(pos.x - brushSizeHalf, pos.y - brushSizeHalf - brushExtentY, pos.z - brushSizeHalf));
			var bMax = selectedTerrain.ToLocal(new Vector3(pos.x + brushSizeHalf, pos.y + brushSizeHalf + brushExtentY, pos.z + brushSizeHalf));

				var start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;;

			foreach (var patch in patches)
			{
				var chunkSize = patch.info.chunkSize;

				var patchSize = chunkSize * Terrain3D.UNITS_PER_VERTEX * Terrain3D.PATCH_CHUNK_EDGES;
				var unitsPerVertexInv = 1.0f / Terrain3D.UNITS_PER_VERTEX;

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

				if (currentToolMode == TerrainToolMode.Sculpt)
				{
					if (currentSculpMode == TerrainSculptMode.Sculpt)
					{
						var mode = new TerrainSculptSculpt(selectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
					}
					else if (currentSculpMode == TerrainSculptMode.Flatten)
					{
						var mode = new TerrainFlattenSculpt(selectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
					}
					else if (currentSculpMode == TerrainSculptMode.Smooth)
					{
						var mode = new TerrainSmoothSculpt(selectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
					}
					else if (currentSculpMode == TerrainSculptMode.Noise)
					{
						var mode = new TerrainNoiseSculpt(selectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
					}
					else if (currentSculpMode == TerrainSculptMode.Holes)
					{
						var mode = new TerrainHoleSculpt(selectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
					}
				}
				else if (currentToolMode == TerrainToolMode.Paint)
				{
					var mode = new TerrainPaintPaint(selectedTerrain, applyInformation);
					mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
				}

			}
		}

		protected TerrainPatch[] GetPatches(AABB cursorBrush)
		{
			var list = new Godot.Collections.Array<TerrainPatch>();
			foreach (var patch in selectedTerrain.terrainPatches)
			{
				var patchBound = patch.getBounds();
				patchBound.Position += selectedTerrain.GlobalTransform.origin;
				if (patchBound.Intersects(cursorBrush))
				{
					list.Add(patch);
				}
			}

			return list.ToArray();
		}

		protected TerrainChunk[] GetChunks(AABB cursorBrush)
		{
			var list = new Godot.Collections.Array<TerrainChunk>();
			foreach (var patch in GetPatches(cursorBrush))
			{
				foreach (var chunk in patch.chunks)
				{
					var bound = chunk.getBounds(patch.info, patch.getOffset());
					bound.Position += selectedTerrain.GlobalTransform.origin;

					if (bound.Intersects(cursorBrush))
					{
						list.Add(chunk);
					}
				}
			}

			return list.ToArray();
		}

		protected AABB CursorBrushBounds(TerrainEditorInfo info, Vector3 hitPosition)
		{
			float brushExtentY = 10000.0f;
			float brushSizeHalf = info.brushSize * 0.5f;

			var ab = new AABB(hitPosition, new Vector3(brushSizeHalf, (brushSizeHalf + brushExtentY), brushSizeHalf));

			return ab;
		}

		public override void _EnterTree()
		{
			var editor_interface = GetEditorInterface();
			//  var inscpector = editor_interface.GetInspector();
			// inscpector.Connect("property_edited", new Callable(this, "refreshEditor"), null, (uint)ConnectFlags.Deferred);
			var base_control = editor_interface.GetBaseControl();

			var script = GD.Load<Script>("res://addons/TerrainPlugin/Terrain3D.cs");
			var scriptMapBox = GD.Load<Script>("res://addons/TerrainPlugin/TerrainMapBox3D.cs");
			var scriptPatch = GD.Load<Script>("res://addons/TerrainPlugin/TerrainPatch.cs");
			var scriptPatchhInfo = GD.Load<Script>("res://addons/TerrainPlugin/TerrainPatchInfo.cs");
			var scriptChunk = GD.Load<Script>("res://addons/TerrainPlugin/TerrainChunk.cs");
			var texture = GD.Load<Texture2D>("res://addons/TerrainPlugin/icons/terrain.png");

			AddCustomType("TerrainPatchInfo", "Resource", scriptPatchhInfo, texture);
			AddCustomType("TerrainPatch", "Resource", scriptPatch, texture);
			AddCustomType("TerrainChunk", "Resource", scriptChunk, texture);
			AddCustomType("Terrain3D", "Node3D", script, texture);
			AddCustomType("TerrainMapBox3D", "Node3D", scriptMapBox, texture);

			menuButton.SwitchOnHover = true;
			menuButton.Text = "Terrain";
			menuButton.Icon = texture;

			menuButton.GetPopup().AddItem("Create terrain", 0);
			menuButton.GetPopup().AddItem("Export heightmap (16bit)", 1);
			menuButton.GetPopup().AddItem("Export splatmap (16bit)", 2);
			menuButton.GetPopup().AddItem("Mapbox import", 3);

			menuButton.Visible = false;
			menuButton.GetPopup().Connect("id_pressed", new Callable(this, "openCreateMenu"));

			AddControlToContainer(CustomControlContainer.SpatialEditorMenu, menuButton);
			AddSpatialGizmoPlugin(gizmoPlugin);
			createImportMenu();

			editorPanel.Name = "Terrain";
			AddPanelOptionBox<TerrainToolMode>("mode", "Tool mode", TerrainToolMode.None);
			AddPanelOptionBox<TerrainSculptMode>("sculpt_mode", "Sculp mode", TerrainSculptMode.Sculpt);

			AddPanelSpinBox("strength", "Strength", 1.2f, 0f, 10f, 0.01f);
			AddPanelSpinBox("radius", "Filter Radius", 0.4f, 0f, 10f, 0.01f);
			AddPanelSpinBox("height", "Target Height", 0f, -100000f, 100000f, 0.01f);

			AddPanelSpinBox("noise_amount", "Noise amount", 10000f, 0, 100000f, 0.1f);
			AddPanelSpinBox("noise_scale", "Noise scale", 128f, 0, 100000f, 0.1f);

			AddPanelSpinBox("brush_size", "Brush size", 4000f, 0f, 1000000f, 0.1f);
			AddPanelSpinBox("brush_fallof", "Brush fallof", 0.5f, 0f, 1f, 0.1f);
			AddPanelSpinBox("layer", "Layer", 0f, 0f, 7f, 1f);

			AddPanelOptionBox<BrushFallOfType>("brush_fallof_type", "Fallof type", BrushFallOfType.Smooth);

			AddCheckBox("show_aabb", "Show AABB");
			AddCheckBox("show_collider", "Show Collider (Slow)");


			refreshPanel();
		}

		protected string[] enumToString<T>()
		{
			var array = new Godot.Collections.Array<string>();
			foreach (int i in Enum.GetValues(typeof(T)))
			{
				String name = Enum.GetName(typeof(T), i);
				array.Add(name);
			}

			return array.ToArray();
		}

		public void selectFilePathSplatmap1(string path)
		{
			splatmapPath1 = path;
		}

		public void selectFilePathSplatmap2(string path)
		{
			splatmapPath2 = path;
		}

		public void selectFilePath(string path)
		{
			heightMapPath = path;
		}

		public void openCreateMenu(int id)
		{
			heightMapPath = null;
			splatmapPath1 = null;
			splatmapPath2 = null;

			if (id == 0)
			{
				OpenDialog();
			}
			else if (id == 1)
			{
				fileDialogExport.MinSize = new Vector2i(400, 400);
				fileDialogExport.PopupCentered();
			}
			else if (id == 3)
			{
				if (selectedTerrain is TerrainMapBox3D)
					(selectedTerrain as TerrainMapBox3D).testGrid();
			}
		}

		public void OpenDialog()
		{
			createDialog.PopupCentered();
		}

		private void AddPanelSpinBox(string name, string text, float def, float min, float max, float step)
		{
			var spinbox = new SpinBox();

			spinbox.MinValue = min;
			spinbox.MaxValue = max;
			spinbox.Step = step;
			spinbox.Value = def;

			createMarginInput(editorPanel, text, spinbox);
			panelControls.Add(name, spinbox);
		}
		private void AddCheckBox(string name, string text)
		{
			var checkbox = new CheckBox();

			createMarginInput(editorPanel, text, checkbox);
			panelControls.Add(name, checkbox);
			checkbox.Connect("pressed", new Callable(this, "refreshGizmo"));
		}



		private void AddPanelOptionBox<T>(string name, string text, T def)
		{
			var option = new OptionButton();

			var defualtText = Enum.GetName(typeof(T), def);
			var selectedID = 0;

			int id = 0;
			foreach (var opt in enumToString<T>())
			{
				option.AddItem(opt, id);

				if (opt == defualtText)
					selectedID = id;
				id++;
			}

			option.Selected = selectedID;
			option.Connect("item_selected", new Callable(this, "onPanelControlSelected"));

			createMarginInput(editorPanel, text, option);
			panelControls.Add(name, option);

		}

		private T getPanelControlValue<T>(string name)
		{
			var control = panelControls[name] as OptionButton;
			return (T)Enum.Parse(typeof(T), control.GetItemText(control.GetSelectedId()));
		}

		private float getPanelControlFloatValue(string name)
		{
			var control = panelControls[name] as SpinBox;
			return (float)control.Value;
		}

		private bool getPanelControlBoolean(string name)
		{
			var control = panelControls[name] as CheckBox;
			return (bool)control.Pressed;
		}

		private TerrainEditorInfo getEditorApply()
		{
			var st = new TerrainEditorInfo();

			st.brushFallof = getPanelControlFloatValue("brush_fallof");
			st.brushSize = getPanelControlFloatValue("brush_size");
			st.strength = getPanelControlFloatValue("strength");
			st.radius = getPanelControlFloatValue("radius");
			st.height = getPanelControlFloatValue("height");
			st.layer = (int)getPanelControlFloatValue("layer");
			st.noiseAmount = getPanelControlFloatValue("noise_amount");
			st.noiseScale = getPanelControlFloatValue("noise_scale");
			st.brushFallofType = getPanelControlValue<BrushFallOfType>("brush_fallof_type");

			return st;
		}

		public void onPanelControlSelected(int item_selected)
		{
			refreshPanel();
		}

		private void refreshGizmo()
		{

			gizmoPlugin.showAABB = getPanelControlBoolean("show_aabb");
			gizmoPlugin.showCollider = getPanelControlBoolean("show_collider");

			selectedTerrain.UpdateGizmos();
		}

		private void refreshPanel()
		{
			var modeValue = getPanelControlValue<TerrainToolMode>("mode");
			var sculptValue = getPanelControlValue<TerrainSculptMode>("sculpt_mode");

			hideInspector("sculpt_mode", (modeValue == TerrainToolMode.Sculpt));
			hideInspector("layer", (modeValue == TerrainToolMode.Paint));
			hideInspector("strength", (modeValue != TerrainToolMode.None));
			hideInspector("radius", (modeValue == TerrainToolMode.Sculpt
														 && sculptValue == TerrainSculptMode.Smooth));
			hideInspector("height", (modeValue == TerrainToolMode.Sculpt
														 && sculptValue == TerrainSculptMode.Flatten));
			hideInspector("noise_amount", (modeValue == TerrainToolMode.Sculpt
														 && sculptValue == TerrainSculptMode.Noise));
			hideInspector("noise_scale", (modeValue == TerrainToolMode.Sculpt
														 && sculptValue == TerrainSculptMode.Noise));
			currentToolMode = modeValue;
			currentSculpMode = sculptValue;
		}

		private void hideInspector(string control_name, bool visible)
		{
			var control = panelControls[control_name].GetParent().GetParent() as Control;
			control.Visible = visible;
		}

		protected void createImportMenu()
		{
			AddChild(createDialog);
			AddChild(fileDialog);
			AddChild(fileDialogSplatmap1);
			AddChild(fileDialogSplatmap2);

			AddChild(fileDialogExport);
			createDialog.Connect("confirmed", new Callable(this, "generateTerrain"));
			fileDialogExport.Connect("file_selected", new Callable(this, "exportHeightmap"));

			chooseTextureButton.Connect("pressed", new Callable(this, "openFileDialog"));
			chooseTextureSplatmap1Button.Connect("pressed", new Callable(this, "openFileDialogSplatmap1"));
			chooseTextureSplatmap2Button.Connect("pressed", new Callable(this, "openFileDialogSplatmap2"));

			fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
			fileDialogSplatmap1.FileMode = FileDialog.FileModeEnum.OpenFile;
			fileDialogSplatmap2.FileMode = FileDialog.FileModeEnum.OpenFile;

			fileDialog.Connect("file_selected", new Callable(this, "selectFilePath"));
			fileDialogSplatmap1.Connect("file_selected", new Callable(this, "selectFilePathSplatmap1"));
			fileDialogSplatmap2.Connect("file_selected", new Callable(this, "selectFilePathSplatmap2"));
			fileDialog.AddFilter("*.png ; PNG Images");
			fileDialogSplatmap1.AddFilter("*.png ; PNG Images");
			fileDialogSplatmap2.AddFilter("*.png ; PNG Images");

			createDialog.Title = "Create a terrain";
			fileDialogExport.Title = "Export heightmap in 16bit raw";
			fileDialogExport.FileMode = FileDialog.FileModeEnum.SaveFile;
			fileDialogExport.ClearFilters();
			fileDialogExport.AddFilter("*.raw ; 16bit Raw Image");


			var vbox = new VBoxContainer();
			createDialog.AddChild(vbox);

			patchXControl.MinValue = 1;
			patchXControl.MaxValue = 10;
			patchXControl.Step = 1;

			patchYControl.MinValue = 1;
			patchYControl.MaxValue = 10;
			patchYControl.Step = 1;

			importHeightScale.MinValue = -100000;
			importHeightScale.MaxValue = 100000;
			importHeightScale.Step = 1;
			importHeightScale.Value = 5000;

			chunkSizeControl.AddItem("32", 0);
			chunkSizeControl.AddItem("64", 1);
			chunkSizeControl.AddItem("128", 2);
			chunkSizeControl.AddItem("256", 3);
			chunkSizeControl.Selected = 0;

			heightmapAlgoControl.AddItem("R16", 0);
			heightmapAlgoControl.AddItem("RGBA8_Normal", 1);
			heightmapAlgoControl.AddItem("RGBA8_Half", 2);
			heightmapAlgoControl.AddItem("RGB8_Full", 3);

			heightmapAlgoControl.Selected = 0;

			createMarginInput(vbox, "Patch X Size", patchYControl);
			createMarginInput(vbox, "Patch Y Size", patchXControl);
			createMarginInput(vbox, "Chunk Size", chunkSizeControl);

			createMarginInput(vbox, "Choose heightmap", chooseTextureButton);
			createMarginInput(vbox, "Heightmap algo", heightmapAlgoControl);

			createMarginInput(vbox, "Choose splatmap1", chooseTextureSplatmap1Button);
			createMarginInput(vbox, "Choose splatmap2", chooseTextureSplatmap2Button);

			createMarginInput(vbox, "Import height scale", importHeightScale);

		}


		public void generateTerrain()
		{
			if (selectedTerrain != null)
			{

				if(selectedTerrain.terrainDefaultMaterial == null)
				{
					var mat = GD.Load<ShaderMaterial>("res://addons/TerrainPlugin/Shader/TerrainVisualShader.tres");
					var dup =  mat.Duplicate() as ShaderMaterial;
					dup.ResourceLocalToScene = true;
					selectedTerrain.terrainDefaultMaterial =dup;
				}

				if(selectedTerrain.terrainDefaultTexture == null)
				{
					var mat = GD.Load<StreamTexture2D>("res://addons/TerrainPlugin/TestTextures/texel.png");
					selectedTerrain.terrainDefaultTexture = mat;
				}
				var typeImport = (HeightmapAlgo) heightmapAlgoControl.GetSelectedId();

				var chunkSize = int.Parse(chunkSizeControl.GetItemText(chunkSizeControl.GetSelectedId()));
				var patchX = (int)patchXControl.Value;
				var patchY = (int)patchYControl.Value;

				var heightScale = (int)importHeightScale.Value;

				Image heightMapImage = null;
				Image splatmap1Image = null;
				Image splatmap2Image = null;

				if (heightMapPath != null)
				{
					heightMapImage = new Image();
					heightMapImage.Load(heightMapPath);
				}

				if (splatmapPath1 != null)
				{
					splatmap1Image = new Image();
					splatmap1Image.Load(splatmapPath1);
				}

				if (splatmapPath2 != null)
				{
					splatmap2Image = new Image();
					splatmap2Image.Load(splatmapPath2);
				}

				selectedTerrain.CreatePatchGrid(patchX, patchY, chunkSize);

				if (heightMapImage != null)
					selectedTerrain.loadHeightmapFromImage(new Vector2i(0, 0), heightMapImage, typeImport);

				if (splatmapPath1 != null)
					selectedTerrain.loadSplatmapFromImage(new Vector2i(0, 0), 0, splatmap1Image);

				if (splatmap2Image != null)
					selectedTerrain.loadSplatmapFromImage(new Vector2i(0, 0), 1, splatmap2Image);

				selectedTerrain.Draw();
			}
		}

		private void createMarginInput(VBoxContainer vbox, string text, Control control)
		{
			var vboxRoot = new VBoxContainer();

			var margin = new MarginContainer();
			var label = new Label();
			label.Text = text;

			margin.AddThemeConstantOverride("margin_left", 0);
			margin.AddChild(control);

			vboxRoot.AddChild(label);
			vboxRoot.AddChild(margin);

			vbox.AddChild(vboxRoot);
		}

		private void openFileDialog()
		{
			fileDialog.MinSize = new Vector2i(400, 400);
			fileDialog.PopupCentered();
		}

		private void openFileDialogSplatmap1()
		{
			fileDialogSplatmap1.MinSize = new Vector2i(400, 400);
			fileDialogSplatmap1.PopupCentered();
		}

		private void openFileDialogSplatmap2()
		{
			fileDialogSplatmap2.MinSize = new Vector2i(400, 400);
			fileDialogSplatmap2.PopupCentered();
		}

		private void exportHeightmap(string path)
		{
			//check of terrain
			if (selectedTerrain == null)
			{
				GD.PrintErr("No terrain selected");
				return;
			}

			//check of patches
			var firstPatch = selectedTerrain.terrainPatches.FirstOrDefault();
			if (firstPatch == null)
			{
				GD.PrintErr("No heightmap found.");
				return;
			}

			// Calculate texture size
			int patchEdgeVertexCount = firstPatch.info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			int patchVertexCount = patchEdgeVertexCount * patchEdgeVertexCount;

			// Find size of heightmap in patches
			Vector2i start = firstPatch.patchCoord;
			Vector2i end = new Vector2i(start);

			for (int i = 0; i < selectedTerrain.GetPatchesCount(); i++)
			{
				var patchPos = selectedTerrain.GetPatch(i).patchCoord;

				if (patchPos.x < start.x)
					start.x = patchPos.x;
				if (patchPos.y < start.y)
					start.y = patchPos.y;
				if (patchPos.x > end.x)
					end.y = patchPos.x;
				if (patchPos.y > end.y)
					end.y = patchPos.y;
			}

			Vector2i size = (end + new Vector2i(1, 1)) - start;

			// Allocate - with space for non-existent patches
			Godot.Collections.Array<float> heightmap = new Godot.Collections.Array<float>();
			heightmap.Resize(patchVertexCount * size.x * size.y);

			var heightData = firstPatch.CacheHeightData();

			if (heightData == null || heightData.Length <= 0)
			{
				GD.PrintErr("Heightmap cache is empty..");
				return;
			}

			// Set to any element, where: min < elem < max
			for (int i = 0; i < heightmap.Count; i++)
			{
				heightmap[i] = heightData[0];
			}

			int heightmapWidth = patchEdgeVertexCount * size.x;

			// Fill heightmap with data
			for (int patchIndex = 0; patchIndex < selectedTerrain.GetPatchesCount(); patchIndex++)
			{
				// Pick a patch
				var patch = selectedTerrain.GetPatch(patchIndex);
				var data = patch.CacheHeightData();

				// Beginning of patch
				int dstIndex = (patch.patchCoord.x - start.x) * patchEdgeVertexCount +
						(patch.patchCoord.y - start.y) * size.y * patchVertexCount;

				// Iterate over lines in patch
				for (int z = 0; z < patchEdgeVertexCount; z++)
				{
					// Iterate over vertices in line
					for (int x = 0; x < patchEdgeVertexCount; x++)
					{
						heightmap[dstIndex + x] = data[z * patchEdgeVertexCount + x];
					}

					dstIndex += heightmapWidth;
				}
			}

			// Interpolate to 16-bit int
			float maxHeight, minHeight;
			maxHeight = minHeight = heightmap[0];
			for (int i = 1; i < heightmap.Count(); i++)
			{
				float h = heightmap[i];
				if (maxHeight < h)
					maxHeight = h;
				else if (minHeight > h)
					minHeight = h;
			}

			float maxValue = 65535.0f;
			float alpha = maxValue / (maxHeight - minHeight);

			// Storage for pixel data
			System.Collections.Generic.List<byte> byteHeightmap = new System.Collections.Generic.List<byte>();
			foreach (var elem in heightmap)
			{
				var mod = alpha * (elem - minHeight);
				var uint16val = Convert.ToUInt16(mod);
				byte[] bytes = BitConverter.GetBytes(uint16val);

				byteHeightmap.AddRange(bytes);
			}



			var image = new Image();
			image.CreateFromData(heightmapWidth, heightmapWidth, false, Image.Format.Rh, byteHeightmap.ToArray());
			image.SavePng(path);
		}
		public override void _ExitTree()
		{
			createDialog.Disconnect("confirmed", new Callable(this, "generateTerrain"));

			chooseTextureSplatmap1Button.Disconnect("pressed", new Callable(this, "openFileDialogSplatmap1"));
			chooseTextureSplatmap2Button.Disconnect("pressed", new Callable(this, "openFileDialogSplatmap2"));
			chooseTextureButton.Disconnect("pressed", new Callable(this, "openFileDialog"));
			fileDialogExport.Disconnect("file_selected", new Callable(this, "exportHeightmap"));

			fileDialog.Disconnect("file_selected", new Callable(this, "selectFilePath"));
			fileDialogSplatmap1.Disconnect("file_selected", new Callable(this, "selectFilePathSplatmap1"));
			fileDialogSplatmap2.Disconnect("file_selected", new Callable(this, "selectFilePathSplatmap2"));

			RemoveChild(createDialog);
			RemoveChild(fileDialog);
			RemoveChild(fileDialogSplatmap1);
			RemoveChild(fileDialogSplatmap2);
			RemoveChild(fileDialogExport);

			if (dockAttached)
				RemoveControlFromDocks(editorPanel);

			RemoveCustomType("Terrain3D");
			RemoveCustomType("TerrainMapBox3D");
			RemoveCustomType("TerrainPatch");
			RemoveCustomType("TerrainPatchInfo");
			RemoveCustomType("TerrainChunk");

			RemoveSpatialGizmoPlugin(gizmoPlugin);
			RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, menuButton);

			editorPanel.Free();
			panelControls.Clear();
		}

		public override bool _Handles(object @object)
		{
			return @object != null && (@object is Terrain3D);
		}
		public override void _MakeVisible(bool visible)
		{
			if (!visible)
				_Edit(null);
		}

		public override void _Edit(object @object)
		{
			if (@object != null && @object is Terrain3D)
			{
				menuButton.Visible = true;
				selectedTerrain = @object as Terrain3D;
				selectedTerrain.NotifyPropertyListChanged();
				AddControlToDock(DockSlot.RightUl, editorPanel);
				dockAttached = true;
			}
			else
			{
				if (selectedTerrain != null)
					DrawInspector(Vector3.Zero, true);

				RemoveControlFromDocks(editorPanel);
				menuButton.Visible = false;
				selectedTerrain = null;
				handle_clicked = false;
				dockAttached = false;
			}
		}

	}
}
