using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot.Collections;
using TerrainEditor.Utils;
using TerrainEditor.Utils.Editor;
using TerrainEditor.Utils.Editor.Sculpt;
using TerrainEditor.Utils.Editor.Paint;
using Array = System.Array;

namespace TerrainEditor;
#if TOOLS
[Tool]
public partial class TerrainPlugin : EditorPlugin
{
	protected Terrain3D? SelectedTerrain;
	protected ConfirmationDialog CreateDialog = new();
	protected TerrainGizmoPlugin GizmoPlugin = new();
	private bool handleClicked;
	protected bool DockAttached;
	protected TerrainSculptMode CurrentSculptMode = TerrainSculptMode.SCULPT;
	protected TerrainToolMode CurrentToolMode = TerrainToolMode.NONE;

	private Vector2 mousePosition = Vector2.Zero;
	private Camera3D? editorCamera;
	private MenuButton menuButton = new();
	private SpinBox patchXControl = new();
	private SpinBox patchYControl = new();
	private SpinBox importHeightScale = new();
	private OptionButton chunkSizeControl = new();
	private OptionButton heightmapAlgoControl = new();
	private Button chooseTextureButton = new();
	private Button chooseTextureSplatmap1Button = new();
	private Button chooseTextureSplatmap2Button = new();
	private FileDialog fileDialog = new();
	private FileDialog fileDialogSplatmap1 = new();
	private FileDialog fileDialogSplatmap2 = new();
	private FileDialog fileDialogExport = new();
	private VBoxContainer editorPanel = new();
	protected Dictionary<string, Control> PanelControls = new();
	protected string? HeightMapPath;
	protected string? SplatmapPath1;
	protected string? SplatmapPath2;

	public override int _Forward3DGuiInput(Camera3D? camera, InputEvent @event)
	{
		editorCamera = camera;

		if (SelectedTerrain == null)
			return 0;

		if (CurrentToolMode != TerrainToolMode.NONE)
			switch (@event)
			{
				case InputEventMouseButton mouseButton:
				{
					if (mouseButton.IsPressed() && mouseButton.ButtonIndex == MouseButton.Left)
					{
						handleClicked = true;
						mousePosition = mouseButton.Position;
						return 1;
					}
					else
					{
						handleClicked = false;
					}

					break;
				}
				case InputEventMouseMotion mouseMotion:
				{
					mousePosition = mouseMotion.Position;
					break;
				}
			}
		else
			handleClicked = false;

		return base._Forward3DGuiInput(camera, @event);
	}

	protected static void SetMaterialParams(TerrainEditorInfo info, TerrainChunk chunk, Vector3 position, Color color)
	{
		// Data 0: XYZ: position, W: radius
		// Data 1: X: falloff, Y: type
		float halfSize = info.BrushSize * 0.5f; // 2000
		float falloff = halfSize * info.BrushFalloff; // 1000
		float radius = halfSize - falloff; // 1000

		chunk.UpdateInspectorMaterial(color, new Plane(position, radius), new Plane(falloff, (float)info.BrushFalloffType, 0, 0));
	}

	protected static void ResetMaterialParams(TerrainChunk chunk)
	{
		chunk.UpdateInspectorMaterial(new Color(), new Plane(Vector3.Zero, 0f), new Plane(Vector3.Zero, 0f));
	}

	protected void DrawInspector(Vector3 pos, bool reset = false)
	{
		if (SelectedTerrain != null && SelectedTerrain.IsInsideTree() && editorCamera != null)
		{
			TerrainEditorInfo applyInformation = GetEditorApply();
			Aabb cursorBrush = CursorBrushBounds(applyInformation, pos);

			TerrainChunk[] selectedChunks = Array.Empty<TerrainChunk>();
			if (reset == false)
				selectedChunks = GetChunks(cursorBrush);

			foreach (TerrainPatch? patch in SelectedTerrain.TerrainPatches)
			foreach (TerrainChunk? chunk in patch.Chunks)
				if (selectedChunks == null || !selectedChunks.Contains(chunk))
					ResetMaterialParams(chunk);
				else
					SetMaterialParams(applyInformation, chunk, pos, new Color(1.0f, 0.85f, 0.0f));
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		//sculpting or painting
		if (SelectedTerrain != null && editorCamera != null && SelectedTerrain.IsInsideTree())
		{
			// long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			Vector3 cast = DoRayCast(mousePosition);

			if (cast != Vector3.Inf)
			{
				if (handleClicked) DoEditTerrain(cast, delta);

				DrawInspector(cast);
			}
		}
	}

	protected Vector3 DoRayCast(Vector2 pos)
	{
		if (SelectedTerrain != null && editorCamera != null)
			if (editorCamera.GetViewport() is SubViewport viewport)
				if (viewport.GetParent() is SubViewportContainer viewportContainer)
				{
					Vector2 screenPos = pos * viewport.Size / viewportContainer.Size;

					Vector3 from = editorCamera.ProjectRayOrigin(screenPos);
					Vector3 dir = editorCamera.ProjectRayNormal(screenPos);

					float distance = editorCamera.Far * 1.2f;
					PhysicsDirectSpaceState3D? spaceState = SelectedTerrain.GetWorld3D().DirectSpaceState;

					var query = new PhysicsRayQueryParameters3D();
					query.From = from;
					query.To = from + dir * distance;
					Dictionary? result = spaceState.IntersectRay(query);

					if (result.Count > 0 && result["collider"].Obj != null)
						if (result["collider"].Obj == SelectedTerrain)
							return (Vector3)result["position"];
				}

		return Vector3.Inf;
	}

	protected void DoEditTerrain(Vector3 pos, double delta)
	{
		TerrainEditorInfo applyInformation = GetEditorApply();
		var strength = (float)(applyInformation.Strength * delta);

		if (strength <= 0.0f)
			return;

		applyInformation.Inverse = Input.IsActionPressed("ui_cancel");
		if (applyInformation.Inverse) applyInformation.Strength *= -1;

		Aabb cursorBrush = CursorBrushBounds(applyInformation, pos);
		TerrainPatch[] patches = GetPatches(cursorBrush);
		const float brushExtentY = 10000.0f;
		float brushSizeHalf = applyInformation.BrushSize * 0.5f;

		// Get brush bounds in terrain local space
		if (SelectedTerrain == null)
		{
			GD.PrintErr($"{nameof(SelectedTerrain)} is null");
		}
		else
		{
			Vector3 bMin = SelectedTerrain.ToLocal(new Vector3(pos.X - brushSizeHalf, pos.Y - brushSizeHalf - brushExtentY, pos.Z - brushSizeHalf));
			Vector3 bMax = SelectedTerrain.ToLocal(new Vector3(pos.X + brushSizeHalf, pos.Y + brushSizeHalf + brushExtentY, pos.Z + brushSizeHalf));

			// long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

			foreach (TerrainPatch? patch in patches)
			{
				int chunkSize = patch.Info.ChunkSize;

				float patchSize = chunkSize * Terrain3D.UNITS_PER_VERTEX * Terrain3D.PATCH_CHUNK_EDGES;
				float unitsPerVertexInv = 1.0f / Terrain3D.UNITS_PER_VERTEX;

				var patchPositionLocal = new Vector3(patch.PatchCoordinates.X * patchSize, 0, patch.PatchCoordinates.Y * patchSize);
				Vector3 brushBoundsPatchLocalMin = (bMin - patchPositionLocal) * unitsPerVertexInv;
				Vector3 brushBoundsPatchLocalMax = (bMax - patchPositionLocal) * unitsPerVertexInv;

				// Calculate patch heightmap area to modify by brush
				var brushPatchMin = new Vector2I(Mathf.FloorToInt(brushBoundsPatchLocalMin.X), Mathf.FloorToInt(brushBoundsPatchLocalMin.Z));
				var brushPatchMax = new Vector2I(Mathf.CeilToInt(brushBoundsPatchLocalMax.X), Mathf.FloorToInt(brushBoundsPatchLocalMax.Z));
				Vector2I modifiedOffset = brushPatchMin;
				Vector2I modifiedSize = brushPatchMax - brushPatchMin;

				// Expand the modification area by one vertex in each direction to ensure normal vectors are updated for edge cases, also clamp to prevent overflows
				if (modifiedOffset.X < 0)
				{
					modifiedSize.X += modifiedOffset.X;
					modifiedOffset.X = 0;
				}

				if (modifiedOffset.Y < 0)
				{
					modifiedSize.Y += modifiedOffset.Y;
					modifiedOffset.Y = 0;
				}

				modifiedSize.X = Mathf.Min(modifiedSize.X + 2, patch.Info.HeightMapSize - modifiedOffset.X);
				modifiedSize.Y = Mathf.Min(modifiedSize.Y + 2, patch.Info.HeightMapSize - modifiedOffset.Y);

				// Skip patch won't be modified at all
				if (modifiedSize.X <= 0 || modifiedSize.Y <= 0)
					continue;

				switch (CurrentToolMode)
				{
					case TerrainToolMode.SCULPT when CurrentSculptMode == TerrainSculptMode.SCULPT:
					{
						var mode = new TerrainSculptSculpt(SelectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
						break;
					}
					case TerrainToolMode.SCULPT when CurrentSculptMode == TerrainSculptMode.FLATTEN:
					{
						var mode = new TerrainFlattenSculpt(SelectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
						break;
					}
					case TerrainToolMode.SCULPT when CurrentSculptMode == TerrainSculptMode.SMOOTH:
					{
						var mode = new TerrainSmoothSculpt(SelectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
						break;
					}
					case TerrainToolMode.SCULPT when CurrentSculptMode == TerrainSculptMode.NOISE:
					{
						var mode = new TerrainNoiseSculpt(SelectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
						break;
					}
					case TerrainToolMode.SCULPT:
					{
						if (CurrentSculptMode == TerrainSculptMode.HOLES)
						{
							var mode = new TerrainHoleSculpt(SelectedTerrain, applyInformation);
							mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
						}

						break;
					}
					case TerrainToolMode.PAINT:
					{
						var mode = new TerrainPaintPaint(SelectedTerrain, applyInformation);
						mode.Apply(patch, pos, patchPositionLocal, strength, modifiedSize, modifiedOffset);
						break;
					}
					case TerrainToolMode.NONE:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
	}

	protected TerrainPatch[] GetPatches(Aabb cursorBrush)
	{
		var list = new Array<TerrainPatch>();
		if (SelectedTerrain == null)
			GD.PrintErr($"{nameof(SelectedTerrain)} is null");
		else
			foreach (TerrainPatch? patch in SelectedTerrain.TerrainPatches)
			{
				Aabb patchBound = patch.GetBounds();
				patchBound.Position += SelectedTerrain.GlobalTransform.Origin;
				if (patchBound.Intersects(cursorBrush)) list.Add(patch);
			}

		return list.ToArray();
	}

	protected TerrainChunk[] GetChunks(Aabb cursorBrush)
	{
		var list = new Array<TerrainChunk>();
		if (SelectedTerrain == null)
			GD.PrintErr($"{nameof(SelectedTerrain)} is null");
		else
			foreach (TerrainPatch? patch in GetPatches(cursorBrush))
			foreach (TerrainChunk? chunk in patch.Chunks)
			{
				Aabb bound = chunk.GetBounds(patch.Info, patch.GetOffset());
				bound.Position += SelectedTerrain.GlobalTransform.Origin;

				if (bound.Intersects(cursorBrush)) list.Add(chunk);
			}

		return list.ToArray();
	}

	protected static Aabb CursorBrushBounds(TerrainEditorInfo info, Vector3 hitPosition)
	{
		const float brushExtentY = 10000.0f;
		float brushSizeHalf = info.BrushSize * 0.5f;

		var ab = new Aabb(hitPosition, new Vector3(brushSizeHalf, brushSizeHalf + brushExtentY, brushSizeHalf));

		return ab;
	}

	public override void _EnterTree()
	{
		EditorInterface? editorInterface = GetEditorInterface();
		EditorInspector? inspector = editorInterface?.GetInspector();
		if (inspector != null)
			inspector.PropertyEdited += RefreshEditor; // Connect("property_edited", new Callable(this, "refreshEditor"), null, (uint)ConnectFlags.Deferred);
		// Control? baseControl = editorInterface.GetBaseControl();

		var script = GD.Load<CSharpScript>(ResourcePath($"{nameof(Terrain3D)}.cs"));
		var scriptMapBox = GD.Load<CSharpScript>(ResourcePath($"{nameof(TerrainMapBox3D)}.cs"));
		var scriptPatch = GD.Load<CSharpScript>(ResourcePath($"{nameof(TerrainPatch)}.cs"));
		var scriptPatchInfo = GD.Load<CSharpScript>(ResourcePath($"{nameof(TerrainPatchInfo)}.cs"));
		var scriptChunk = GD.Load<CSharpScript>(ResourcePath($"{nameof(TerrainChunk)}.cs"));
		var texture = GD.Load<Texture2D>(ResourcePath("icons/terrain.png"));

		AddCustomType(nameof(TerrainPatchInfo), nameof(Resource), scriptPatchInfo, texture);
		AddCustomType(nameof(TerrainPatch), nameof(Resource), scriptPatch, texture);
		AddCustomType(nameof(TerrainChunk), nameof(Resource), scriptChunk, texture);
		AddCustomType(nameof(Terrain3D), nameof(Node3D), script, texture);
		AddCustomType(nameof(TerrainMapBox3D), nameof(Node3D), scriptMapBox, texture);

		menuButton.SwitchOnHover = true;
		menuButton.Text = UserInterfaceNames.TERRAIN;
		menuButton.Icon = texture;

		menuButton.GetPopup().AddItem(UserInterfaceNames.TERRAIN_CREATE, 0);
		menuButton.GetPopup().AddItem(UserInterfaceNames.EXPORT_HEIGHTMAP, 1);
		menuButton.GetPopup().AddItem(UserInterfaceNames.EXPORT_SPLATMAP, 2);
		menuButton.GetPopup().AddItem(UserInterfaceNames.IMPORT_MAPBOX, 3);

		menuButton.Visible = false;
		menuButton.GetPopup().IdPressed += OpenCreateMenu;

		AddControlToContainer(CustomControlContainer.SpatialEditorMenu, menuButton);
		AddNode3DGizmoPlugin(GizmoPlugin);
		CreateImportMenu();

		editorPanel.Name = UserInterfaceNames.TERRAIN;
		AddPanelOptionBox(ToolSettingKeys.TOOL_MODE, "Tool mode", TerrainToolMode.NONE);
		AddPanelOptionBox(ToolSettingKeys.TOOL_SCULPT_MODE, "Sculpt mode", TerrainSculptMode.SCULPT);

		AddPanelSpinBox(ToolSettingKeys.TOOL_STRENGTH, "Strength", 1.2f, 0f, 10f, 0.01f);
		AddPanelSpinBox(ToolSettingKeys.TOOL_RADIUS, "Filter Radius", 0.4f, 0f, 10f, 0.01f);
		AddPanelSpinBox(ToolSettingKeys.TOOL_HEIGHT, "Target Height", 0f, -100000f, 100000f, 0.01f);

		AddPanelSpinBox(ToolSettingKeys.TOOL_NOISE_AMOUNT, "Noise amount", 10000f, 0, 100000f, 0.1f);
		AddPanelSpinBox(ToolSettingKeys.TOOL_NOISE_SCALE, "Noise scale", 128f, 0, 100000f, 0.1f);

		AddPanelSpinBox(ToolSettingKeys.TOOL_BRUSH_SIZE, "Brush size", 4000f, 0f, 1000000f, 0.1f);
		AddPanelSpinBox(ToolSettingKeys.TOOL_BRUSH_FALLOFF, "Brush falloff", 0.5f, 0f, 1f, 0.1f);
		AddPanelSpinBox(ToolSettingKeys.TOOL_LAYER, "Layer", 0f, 0f, 7f, 1f);

		AddPanelOptionBox(ToolSettingKeys.TOOL_BRUSH_FALLOFF_TYPE, "Falloff type", BrushFallOffType.SMOOTH);

		AddCheckBox(ToolSettingKeys.TOOL_SHOW_AABB, "Show AABB");
		AddCheckBox(ToolSettingKeys.TOOL_SHOW_COLLIDER, "Show Collider (Slow)");

		RefreshPanel();
	}

	private void RefreshEditor(string property)
	{
		RefreshPanel();
		RefreshGizmo();
	}

	public void SelectFilePathSplatmap1(string path)
	{
		SplatmapPath1 = path;
	}

	public void SelectFilePathSplatmap2(string path)
	{
		SplatmapPath2 = path;
	}

	public void SelectFilePath(string path)
	{
		HeightMapPath = path;
	}

	public void OpenCreateMenu(long id)
	{
		HeightMapPath = null;
		SplatmapPath1 = null;
		SplatmapPath2 = null;

		switch (id)
		{
			case 0:
				OpenDialog();
				break;
			case 1:
				fileDialogExport.MinSize = new Vector2I(400, 400);
				fileDialogExport.PopupCentered();
				break;
			case 3:
			{
				if (SelectedTerrain is TerrainMapBox3D terrainMapBox3D)
					terrainMapBox3D.TestGrid();
				break;
			}
		}
	}

	public void OpenDialog()
	{
		CreateDialog.PopupCentered(new Vector2I(800, 600));
	}

	private void AddPanelSpinBox(string name, string text, float def, float min, float max, float step)
	{
		var spinBox = new SpinBox();

		spinBox.MinValue = min;
		spinBox.MaxValue = max;
		spinBox.Step = step;
		spinBox.Value = def;

		CreateMarginInput(editorPanel, text, spinBox);
		PanelControls.Add(name, spinBox);
	}

	private void AddCheckBox(string name, string text)
	{
		var checkbox = new CheckBox();

		CreateMarginInput(editorPanel, text, checkbox);
		PanelControls.Add(name, checkbox);
		checkbox.Pressed += RefreshGizmo;
	}


	private void AddPanelOptionBox<T>(string name, string text, T def) where T : struct, Enum
	{
		var option = new OptionButton();

		string? defaultText = Enum.GetName(typeof(T), def);
		var selectedId = 0;

		var id = 0;
		foreach (string? opt in Enum.GetNames<T>())
		{
			option.AddItem(opt, id);

			if (opt == defaultText)
				selectedId = id;
			id++;
		}

		option.Selected = selectedId;
		option.ItemSelected += OnPanelControlSelected;

		CreateMarginInput(editorPanel, text, option);
		PanelControls.Add(name, option);
	}

	private T? GetPanelControlValue<T>(string name)
	{
		if (PanelControls[name] is OptionButton control)
			return (T)Enum.Parse(typeof(T), control.GetItemText(control.GetSelectedId()));
		return default;
	}

	private float GetPanelControlFloatValue(string name)
	{
		if (PanelControls[name] is SpinBox control)
			return (float)control.Value;
		return float.NaN;
	}

	private bool GetPanelControlBoolean(string name)
	{
		var control = PanelControls[name] as CheckBox;
		return control != null && control.ButtonPressed;
	}

	private TerrainEditorInfo GetEditorApply()
	{
		var st = new TerrainEditorInfo
		{
			BrushFalloff = GetPanelControlFloatValue(ToolSettingKeys.TOOL_BRUSH_FALLOFF),
			BrushSize = GetPanelControlFloatValue(ToolSettingKeys.TOOL_BRUSH_SIZE),
			Strength = GetPanelControlFloatValue(ToolSettingKeys.TOOL_STRENGTH),
			Radius = GetPanelControlFloatValue(ToolSettingKeys.TOOL_RADIUS),
			Height = GetPanelControlFloatValue(ToolSettingKeys.TOOL_HEIGHT),
			Layer = (int)GetPanelControlFloatValue(ToolSettingKeys.TOOL_LAYER),
			NoiseAmount = GetPanelControlFloatValue(ToolSettingKeys.TOOL_NOISE_AMOUNT),
			NoiseScale = GetPanelControlFloatValue(ToolSettingKeys.TOOL_NOISE_SCALE),
			BrushFalloffType = GetPanelControlValue<BrushFallOffType>(ToolSettingKeys.TOOL_BRUSH_FALLOFF_TYPE)
		};

		return st;
	}

	public void OnPanelControlSelected(long index)
	{
		RefreshPanel();
	}

	private void RefreshGizmo()
	{
		GizmoPlugin.ShowAabb = GetPanelControlBoolean("show_aabb");
		GizmoPlugin.ShowCollider = GetPanelControlBoolean("show_collider");

		if (SelectedTerrain == null)
			GD.PrintErr($"{nameof(SelectedTerrain)} is null");
		else
			SelectedTerrain.UpdateGizmos();
	}

	private void RefreshPanel()
	{
		var modeValue = GetPanelControlValue<TerrainToolMode>("mode");
		var sculptValue = GetPanelControlValue<TerrainSculptMode>("sculpt_mode");

		HideInspector("sculpt_mode", modeValue == TerrainToolMode.SCULPT);
		HideInspector("layer", modeValue == TerrainToolMode.PAINT);
		HideInspector("strength", modeValue != TerrainToolMode.NONE);
		HideInspector("radius", modeValue == TerrainToolMode.SCULPT
								&& sculptValue == TerrainSculptMode.SMOOTH);
		HideInspector("height", modeValue == TerrainToolMode.SCULPT
								&& sculptValue == TerrainSculptMode.FLATTEN);
		HideInspector("noise_amount", modeValue == TerrainToolMode.SCULPT
									  && sculptValue == TerrainSculptMode.NOISE);
		HideInspector("noise_scale", modeValue == TerrainToolMode.SCULPT
									 && sculptValue == TerrainSculptMode.NOISE);
		CurrentToolMode = modeValue;
		CurrentSculptMode = sculptValue;
	}

	private void HideInspector(string controlName, bool visible)
	{
		if (PanelControls[controlName].GetParent().GetParent() is Control control)
			control.Visible = visible;
	}

	protected void CreateImportMenu()
	{
		AddChild(CreateDialog);
		AddChild(fileDialog);
		AddChild(fileDialogSplatmap1);
		AddChild(fileDialogSplatmap2);

		AddChild(fileDialogExport);
		CreateDialog.Confirmed += GenerateTerrain;
		fileDialogExport.FileSelected += ExportHeightmap;

		chooseTextureButton.Text = "Open ...";
		chooseTextureButton.Pressed += OpenFileDialog;
		chooseTextureSplatmap1Button.Text = "Open ...";
		chooseTextureSplatmap1Button.Pressed += OpenFileDialogSplatmap1;
		chooseTextureSplatmap2Button.Text = "Open ...";
		chooseTextureSplatmap2Button.Pressed += OpenFileDialogSplatmap2;

		fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
		fileDialogSplatmap1.FileMode = FileDialog.FileModeEnum.OpenFile;
		fileDialogSplatmap2.FileMode = FileDialog.FileModeEnum.OpenFile;

		fileDialog.FileSelected += SelectFilePath;
		fileDialogSplatmap1.FileSelected += SelectFilePathSplatmap1;
		fileDialogSplatmap2.FileSelected += SelectFilePathSplatmap2;
		const string pngFilter = "*.png ; PNG Images";
		fileDialog.AddFilter(pngFilter);
		fileDialogSplatmap1.AddFilter(pngFilter);
		fileDialogSplatmap2.AddFilter(pngFilter);

		CreateDialog.Title = "Create a terrain";
		fileDialogExport.Title = "Export heightmap in 16bit raw";
		fileDialogExport.FileMode = FileDialog.FileModeEnum.SaveFile;
		fileDialogExport.ClearFilters();
		fileDialogExport.AddFilter("*.raw ; 16bit Raw Image");


		var vbox = new VBoxContainer();
		CreateDialog.AddChild(vbox);

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

		CreateMarginInput(vbox, "Patch X Size", patchYControl);
		CreateMarginInput(vbox, "Patch Y Size", patchXControl);
		CreateMarginInput(vbox, "Chunk Size", chunkSizeControl);

		CreateMarginInput(vbox, "Choose heightmap", chooseTextureButton);
		CreateMarginInput(vbox, "Heightmap algo", heightmapAlgoControl);

		CreateMarginInput(vbox, "Choose splatmap1", chooseTextureSplatmap1Button);
		CreateMarginInput(vbox, "Choose splatmap2", chooseTextureSplatmap2Button);

		CreateMarginInput(vbox, "Import height scale", importHeightScale);
	}


	public void GenerateTerrain()
	{
		if (SelectedTerrain != null)
		{
			if (SelectedTerrain.TerrainDefaultMaterial == null)
			{
				var mat = GD.Load<ShaderMaterial>(ResourcePath("Shader/TerrainVisualShader.tres"));
				if (mat.Duplicate() is ShaderMaterial dup)
				{
					dup.ResourceLocalToScene = true;
					SelectedTerrain.TerrainDefaultMaterial = dup;
				}
			}

			if (SelectedTerrain.TerrainDefaultTexture == null)
			{
				var mat = GD.Load<CompressedTexture2D>(ResourcePath("TestTextures/512x512 Texel Density Texture.png"));
				SelectedTerrain.TerrainDefaultTexture = mat;
			}

			var typeImport = (HeightmapAlgo)heightmapAlgoControl.GetSelectedId();

			int chunkSize = int.Parse(chunkSizeControl.GetItemText(chunkSizeControl.GetSelectedId()));
			var patchX = (int)patchXControl.Value;
			var patchY = (int)patchYControl.Value;

			// var heightScale = (int)importHeightScale.Value;

			SelectedTerrain.CreatePatchGrid(patchX, patchY, chunkSize);

			if (HeightMapPath != null)
			{
				var heightMapImage = new Image();
				heightMapImage.Load(HeightMapPath);
				SelectedTerrain.LoadHeightmapFromImage(new Vector2I(0, 0), heightMapImage, typeImport);
			}

			if (SplatmapPath1 != null)
			{
				var splatmap1Image = new Image();
				splatmap1Image.Load(SplatmapPath1);
				SelectedTerrain.LoadSplatmapFromImage(new Vector2I(0, 0), 0, splatmap1Image);
			}

			if (SplatmapPath2 != null)
			{
				var splatmap2Image = new Image();
				splatmap2Image.Load(SplatmapPath2);
				SelectedTerrain.LoadSplatmapFromImage(new Vector2I(0, 0), 1, splatmap2Image);
			}

			SelectedTerrain.Draw();
		}
	}

	private static void CreateMarginInput(Node vbox, string text, Node control)
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

	private void OpenFileDialog()
	{
		fileDialog.MinSize = new Vector2I(400, 400);
		fileDialog.PopupCentered();
	}

	private void OpenFileDialogSplatmap1()
	{
		fileDialogSplatmap1.MinSize = new Vector2I(400, 400);
		fileDialogSplatmap1.PopupCentered();
	}

	private void OpenFileDialogSplatmap2()
	{
		fileDialogSplatmap2.MinSize = new Vector2I(400, 400);
		fileDialogSplatmap2.PopupCentered();
	}

	private void ExportHeightmap(string path)
	{
		//check of terrain
		if (SelectedTerrain == null)
		{
			GD.PrintErr($"{nameof(SelectedTerrain)} is null");
			return;
		}

		//check of patches
		TerrainPatch? firstPatch = SelectedTerrain.TerrainPatches.FirstOrDefault();
		if (firstPatch == null)
		{
			GD.PrintErr("No heightmap found.");
			return;
		}

		// Calculate texture size
		int patchEdgeVertexCount = firstPatch.Info.ChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
		int patchVertexCount = patchEdgeVertexCount * patchEdgeVertexCount;

		// Find size of heightmap in patches
		Vector2I start = firstPatch.PatchCoordinates;
		var end = new Vector2I(start.X, start.Y);

		for (var i = 0; i < SelectedTerrain.GetPatchesCount(); i++)
		{
			TerrainPatch? terrainPatch = SelectedTerrain.GetPatch(i);
			if (terrainPatch == null)
			{
				GD.PrintErr($"{nameof(TerrainPatch)} is null");
				continue;
			}

			Vector2I patchPos = terrainPatch.PatchCoordinates;

			if (patchPos.X < start.X)
				start.X = patchPos.X;
			if (patchPos.Y < start.Y)
				start.Y = patchPos.Y;
			if (patchPos.X > end.X)
				end.Y = patchPos.X;
			if (patchPos.Y > end.Y)
				end.Y = patchPos.Y;
		}

		Vector2I size = end + new Vector2I(1, 1) - start;

		// Allocate - with space for non-existent patches
		var heightmap = new Array<float>();
		heightmap.Resize(patchVertexCount * size.X * size.Y);

		float[] heightData = firstPatch.CacheHeightData();

		if (heightData.Length <= 0)
		{
			GD.PrintErr("Heightmap cache is empty..");
			return;
		}

		// Set to any element, where: min < elem < max
		for (var i = 0; i < heightmap.Count; i++) heightmap[i] = heightData[0];

		int heightmapWidth = patchEdgeVertexCount * size.X;

		// Fill heightmap with data
		for (var patchIndex = 0; patchIndex < SelectedTerrain.GetPatchesCount(); patchIndex++)
		{
			// Pick a patch
			TerrainPatch? patch = SelectedTerrain.GetPatch(patchIndex);
			if (patch == null)
			{
				GD.PrintErr($"{nameof(TerrainPatch)} is null");
				continue;
			}

			float[] data = patch.CacheHeightData();

			// Beginning of patch
			int dstIndex = (patch.PatchCoordinates.X - start.X) * patchEdgeVertexCount +
						   (patch.PatchCoordinates.Y - start.Y) * size.Y * patchVertexCount;

			// Iterate over lines in patch
			for (var z = 0; z < patchEdgeVertexCount; z++)
			{
				// Iterate over vertices in line
				for (var x = 0; x < patchEdgeVertexCount; x++) heightmap[dstIndex + x] = data[z * patchEdgeVertexCount + x];

				dstIndex += heightmapWidth;
			}
		}

		// Interpolate to 16-bit int
		float minHeight;
		float maxHeight = minHeight = heightmap[0];
		for (var i = 1; i < heightmap.Count; i++)
		{
			float h = heightmap[i];
			if (maxHeight < h)
				maxHeight = h;
			else if (minHeight > h)
				minHeight = h;
		}

		const float maxValue = 65535.0f;
		float alpha = maxValue / (maxHeight - minHeight);

		// Storage for pixel data
		var byteHeightmap = new System.Collections.Generic.List<byte>();
		foreach (float elem in heightmap)
		{
			float mod = alpha * (elem - minHeight);
			var uint16Val = Convert.ToUInt16(mod);
			byte[] bytes = BitConverter.GetBytes(uint16Val);

			byteHeightmap.AddRange(bytes);
		}


		var image = Image.CreateFromData(heightmapWidth, heightmapWidth, false, Image.Format.Rh, byteHeightmap.ToArray());
		image.SavePng(path);
	}

	public override void _ExitTree()
	{
		CreateDialog.Confirmed -= GenerateTerrain;

		chooseTextureSplatmap1Button.Pressed -= OpenFileDialogSplatmap1;
		chooseTextureSplatmap2Button.Pressed -= OpenFileDialogSplatmap2;
		chooseTextureButton.Pressed -= OpenFileDialog;
		fileDialogExport.FileSelected -= ExportHeightmap;

		fileDialog.FileSelected -= SelectFilePath;
		fileDialogSplatmap1.FileSelected -= SelectFilePathSplatmap1;
		fileDialogSplatmap2.FileSelected -= SelectFilePathSplatmap2;

		RemoveChild(CreateDialog);
		RemoveChild(fileDialog);
		RemoveChild(fileDialogSplatmap1);
		RemoveChild(fileDialogSplatmap2);
		RemoveChild(fileDialogExport);

		if (DockAttached)
			RemoveControlFromDocks(editorPanel);

		RemoveCustomType(nameof(Terrain3D));
		RemoveCustomType(nameof(TerrainMapBox3D));
		RemoveCustomType(nameof(TerrainPatch));
		RemoveCustomType(nameof(TerrainPatchInfo));
		RemoveCustomType(nameof(TerrainChunk));

		RemoveNode3DGizmoPlugin(GizmoPlugin);
		RemoveControlFromContainer(CustomControlContainer.SpatialEditorMenu, menuButton);

		editorPanel.Free();
		PanelControls.Clear();
	}

	public override bool _Handles(GodotObject go)
	{
		return go is Terrain3D;
	}

	public override void _MakeVisible(bool visible)
	{
		if (!visible)
			_Edit(new Node());
	}

	public override void _Edit(GodotObject variant)
	{
		if (_Handles(variant))
		{
			menuButton.Visible = true;
			SelectedTerrain = variant as Terrain3D;
			SelectedTerrain?.NotifyPropertyListChanged();
			AddControlToDock(DockSlot.RightUl, editorPanel);
			DockAttached = true;
		}
		else
		{
			if (SelectedTerrain != null)
				DrawInspector(Vector3.Zero, true);

			RemoveControlFromDocks(editorPanel);
			menuButton.Visible = false;
			SelectedTerrain = null;
			handleClicked = false;
			DockAttached = false;
		}
	}

	public static string ResourcePath(string subPath)
	{
		// const string DefaultAddonBasePath = "res://addons/TerrainPlugin/";
		string? calculatedBasePath = typeof(TerrainPlugin).GetCustomAttribute<ScriptPathAttribute>()?.Path.Replace($"{nameof(TerrainPlugin)}.cs", "");
		return Path.Join(calculatedBasePath, subPath);
	}
}
#endif
