using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using System;
using TerrainEditor.Generators;
using System.Linq;

namespace TerrainEditor
{
	[Tool]
	public partial class TerrainPatch : Resource
	{
		[Export]
		public Vector2I patchCoord = new Vector2I();

		[Export]
		public Vector3 offset = new Vector3();

		[Export]
		public Godot.Collections.Array<TerrainChunk> chunks = new Godot.Collections.Array<TerrainChunk>();

		[Export]
		public ImageTexture heightmap;

		[Export]
		public Godot.Collections.Array<ImageTexture> splatmaps = new Godot.Collections.Array<ImageTexture>();

		[Export]
		public TerrainPatchInfo info = new TerrainPatchInfo();

		protected Rid? bodyRid;

		//cache for meshes
		public Godot.Collections.Dictionary<int, ArrayMesh> meshCache = new Godot.Collections.Dictionary<int, ArrayMesh>();
		protected HeightMapShape3D shapeHeight = null;

		[Export]
		public ArrayMesh currentMesh = null;
		/**
		 * Clear rendering device by removing body and collider
		 */
		public void ClearDraw()
		{
			foreach (TerrainChunk? chunk in chunks)
			{
				chunk.ClearDraw();
			}

			//  if (shapeRid != null)
			{
				//  PhysicsServer3D.FreeRid(shapeRid);
				// shapeRid = null;
			}

			if (bodyRid.HasValue)
			{
				PhysicsServer3D.BodyClearShapes(bodyRid.Value);
				bodyRid = null;
			}

			shapeHeight = null;

		}

		/**
		 * Initalize a patch by given chunksize and optional exist heightmap datas
		 */
		public void Init(int _chunkSize)
		{
			//reset prev cache
			cachedHeightMapData = null;
			cachedHolesMask = null;
			cachedSplatMap = new Godot.Collections.Array<Color[]>();

			//generate info file
			createInfoResource(_chunkSize);

			//creating chunks
			chunks.Clear();
			for (int i = 0; i < Terrain3D.PATCH_CHUNKS_AMOUNT; i++)
			{
				Variant script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainChunk.cs").New();
				var res = script.Obj as TerrainChunk;
				res.ResourceLocalToScene = true;

				int px = i % Terrain3D.PATCH_CHUNK_EDGES;
				int py = i / Terrain3D.PATCH_CHUNK_EDGES;
				res.position = new Vector2I((int)px, (int)py);

				//  res.ChunkSizeNextLOD = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);
				res.TerrainChunkSizeLOD0 = Terrain3D.UNITS_PER_VERTEX * info.chunkSize;
				chunks.Add(res);
			}
		}

		/**
		* Creating a heightmap by given data
		*/
		public void createHeightmap(float[] heightMapdata = null)
		{
			//generate heightmap
			var heightmapGen = new TerrainHeightMapGenerator(this);
			Image? image = heightmapGen.createImage();
			byte[]? imageBuffer = image.GetData();

			if (heightMapdata == null)
			{
				heightMapdata = new float[info.heightMapSize * info.heightMapSize];
				for (int i = 0; i < heightMapdata.Length; i++)
				{
					heightMapdata[i] = 0f;
				}
			}

			float[] chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
			float[] chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];

			//calculate height ranges
			Vector2 result = heightmapGen.CalculateHeightRange(heightMapdata, ref chunkOffsets, ref chunkHeights);

			info.patchOffset = result.X;
			info.patchHeight = result.Y;

			int chunkIndex = 0;
			foreach (TerrainChunk? chunk in chunks)
			{
				chunk.offset = chunkOffsets[chunkIndex];
				chunk.height = chunkHeights[chunkIndex];
				chunkIndex++;
			}

			heightmapGen.WriteHeights(ref imageBuffer, ref heightMapdata);
			heightmapGen.WriteNormals(ref imageBuffer, heightMapdata, null, Vector2I.Zero, new Vector2I(info.heightMapSize, info.heightMapSize));

			//store heightmap in rgba8
			image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imageBuffer);

			if (heightmap == null)
			{
				heightmap = ImageTexture.CreateFromImage(image);
			}
			else
			{
				heightmap.Update(image);
			}
		}

		/**
		 * Creating a splatmap by given data
		 */
		public void createSplatmap(int idx, Color[] splatmapDataImport = null)
		{
			if (cachedSplatMap.Count < (idx + 1))
			{
				cachedSplatMap.Resize(idx + 1);
			}

			if (splatmaps.Count < (idx + 1))
			{
				splatmaps.Resize(idx + 1);
			}

			var splatmapGen = new TerrainSplatMapGenerator(this);
			Image? splatmapImage = splatmapGen.createImage();
			var splatmapTexture = new ImageTexture();
			byte[]? splatmapData = splatmapImage.GetData();

			if (splatmapDataImport == null)
			{
				splatmapImage.Fill(new Color(1, 0, 0, 0));
			}
			else
			{
				splatmapGen.WriteColors(ref splatmapData, ref splatmapDataImport);
				splatmapImage = Image.CreateFromData(splatmapImage.GetWidth(), splatmapImage.GetHeight(), false, Image.Format.Rgba8, splatmapData);
			}

			splatmapTexture = ImageTexture.CreateFromImage(splatmapImage);
			splatmaps[idx] = splatmapTexture;
		}

		/**
		 * Drawing a patch by attaching chunks to render device
		 */
		public void Draw(Terrain3D terrainNode, Material mat)
		{
			Rid scenario = terrainNode.GetWorld3D().Scenario;

			//clear chunks scene
			foreach (TerrainChunk? chunk in chunks)
			{
				chunk.ClearDraw();
			}

			bodyRid = PhysicsServer3D.BodyCreate();

			PhysicsServer3D.BodySetMode(bodyRid.Value, PhysicsServer3D.BodyMode.Static);
			PhysicsServer3D.BodyAttachObjectInstanceId(bodyRid.Value, terrainNode.GetInstanceId());
			PhysicsServer3D.BodySetSpace(bodyRid.Value, terrainNode.GetWorld3D().Space);
			PhysicsServer3D.BodySetCollisionLayer(bodyRid.Value, terrainNode.collisionLayer);
			PhysicsServer3D.BodySetCollisionMask(bodyRid.Value, terrainNode.collisionMask);
			//    PhysicsServer3D.BodySetRayPickable(bodyRid, true);

			//create cache
			int chunkId = 0;
			foreach (TerrainChunk? chunk in chunks)
			{
				long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;;

				chunk.Draw(this, info, scenario, ref heightmap, ref splatmaps, terrainNode, getOffset(), mat);
				chunk.UpdatePosition(info, terrainNode.GlobalTransform, getOffset());
				chunk.SetDefaultMaterial(terrainNode.terrainDefaultTexture);

				long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

				GD.Print("[Chunk][" + chunkId + "] Draw time " + (end - start) + "ms");
				chunkId++;
			}

			//Creating collider
			CreateCollider(0, terrainNode);
			UpdatePosition(terrainNode);

			terrainNode.UpdateGizmos();

			currentMesh = meshCache[0];
		}


		/**
		 * Create a collider on physic server
		 */
		private void CreateCollider(int collisionLOD, Terrain3D terrain)
		{
			shapeHeight = new HeightMapShape3D();

			long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

			int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
			int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			int heightFieldLength = heightFieldSize * heightFieldSize;

			var genCollider = new TerrainColliderGenerator(this);
			float[]? heightField = genCollider.GenereateLOD(collisionLOD);


			//create heightmap shape
			PhysicsServer3D.BodyAddShape(bodyRid.Value, shapeHeight.GetRid());

			UpdateColliderData(terrain, heightField);
			UpdateColliderPosition(terrain);
				long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

			GD.Print("[Collider] Creation time " + (end - start) + " ms");
		}

		public void DisableCollider(bool disable = false)
		{
			if (bodyRid != null)
				PhysicsServer3D.BodySetShapeDisabled(bodyRid.Value, 0, disable);
		}

		public void UpdateSettings(Terrain3D terrainNode)
		{
			PhysicsServer3D.BodySetShapeDisabled(bodyRid.Value, 0, terrainNode.IsInsideTree());

			foreach (TerrainChunk? chunk in chunks)
			{
				chunk.UpdateSettings(terrainNode);
			}
		}
		public void UpdatePosition(Terrain3D terrainNode)
		{
			foreach (TerrainChunk? chunk in chunks)
			{
				chunk.UpdatePosition(info, terrainNode.GlobalTransform, getOffset());
			}

			UpdateColliderPosition(terrainNode);
		}

		/** 
		*  Creating info resource which contains all sizes
		*/
		private void createInfoResource(int _chunkSize)
		{
			Variant script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatchInfo.cs").New();
			var patch = script.Obj as TerrainPatchInfo;
			patch.ResourceLocalToScene = true;
			info = patch;

			int chunkSize = _chunkSize - 1;
			int vertexCountEdge = _chunkSize;
			int heightmapSize = chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			int textureSet = vertexCountEdge * Terrain3D.PATCH_CHUNK_EDGES;


			info.patchOffset = 0.0f;
			info.patchHeight = 1.0f;
			info.chunkSize = chunkSize;
			info.vertexCountEdge = vertexCountEdge;
			info.heightMapSize = heightmapSize;
			info.textureSize = textureSet;
		}

		/** 
		 *  Updating heightmap texture for holes, collider and rendering device
		 */
		public void UpdateHolesMask(Terrain3D terrain, byte[] samples, Vector2I modifiedOffset, Vector2I modifiedSize)
		{
			Image? image = heightmap.GetImage();
			byte[]? holesMask = CacheHoleMask();
			float[]? heightMap = CacheHeightData();

			byte[]? imgData = image.GetData();
			if (modifiedOffset.X < 0 || modifiedOffset.Y < 0 ||
				modifiedSize.X <= 0 || modifiedSize.Y <= 0 ||
				modifiedOffset.X + modifiedSize.X > info.heightMapSize ||
				modifiedOffset.Y + modifiedSize.Y > info.heightMapSize)
			{
				GD.PrintErr("Invalid heightmap samples range.");
			}


			int heightMapSize = info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			for (int z = 0; z < modifiedSize.Y; z++)
			{
				for (int x = 0; x < modifiedSize.X; x++)
				{
					holesMask[(z + modifiedOffset.Y) * heightMapSize + (x + modifiedOffset.X)] = samples[z * modifiedSize.X + x];
				}
			}

			var heightmapGen = new TerrainHeightMapGenerator(this);
			heightmapGen.WriteNormals(ref imgData, heightMap, holesMask, modifiedOffset, modifiedSize);

			image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
			heightmap.Update(image);

			UpdateColliderData(terrain, heightMap);
			UpdatePosition(terrain);

			cachedHolesMask = holesMask;
			terrain.UpdateGizmos();
		}

		/** 
		 *  Updating heightmap texture, collider and rendering device
		 */
		public void UpdateHeightMap(Terrain3D terrain, float[] samples, Vector2I modifiedOffset, Vector2I modifiedSize)
		{
			Image? image = heightmap.GetImage();
			float[]? data = CacheHeightData();

			byte[]? imgData = image.GetData();
			if (modifiedOffset.X < 0 || modifiedOffset.Y < 0 ||
				modifiedSize.X <= 0 || modifiedSize.Y <= 0 ||
				modifiedOffset.X + modifiedSize.X > info.heightMapSize ||
				modifiedOffset.Y + modifiedSize.Y > info.heightMapSize)
			{
				GD.PrintErr("Invalid heightmap samples range.");
			}

			info.patchOffset = 0.0f;
			info.patchHeight = 1.0f;

			int heightMapSize = info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			for (int z = 0; z < modifiedSize.Y; z++)
			{
				for (int x = 0; x < modifiedSize.X; x++)
				{
					int index = (z + modifiedOffset.Y) * heightMapSize + (x + modifiedOffset.X);
					data[index] = samples[z * modifiedSize.X + x];
				}
			}

			float[] chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
			float[] chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
			var heightmapGen = new TerrainHeightMapGenerator(this);

			Vector2 result = heightmapGen.CalculateHeightRange(data, ref chunkOffsets, ref chunkHeights);
			info.patchOffset = result.X;
			info.patchHeight = result.Y;

			heightmapGen.WriteHeights(ref imgData, ref data);
			heightmapGen.WriteNormals(ref imgData, data, null, modifiedOffset, modifiedSize);

			int chunkIndex = 0;
			foreach (TerrainChunk? chunk in chunks)
			{
				chunk.offset = chunkOffsets[chunkIndex];
				chunk.height = chunkHeights[chunkIndex];
				chunkIndex++;
			}

			image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
			heightmap.Update(image);

			var genCollider = new TerrainColliderGenerator(this);
			UpdateColliderData(terrain, data);
			UpdatePosition(terrain);

			//set the cache
			cachedHeightMapData = data;
			terrain.UpdateGizmos();
		}

		/** 
		 *  Updating splatmap texture, collider and rendering device
		 */
		public void UpdateSplatMap(int splatmapIndex, Terrain3D terrain, Color[] samples, Vector2I modifiedOffset, Vector2I modifiedSize)
		{
			var image = splatmaps[splatmapIndex].GetImage() as Image;
			byte[]? imgData = image.GetData();

			Color[]? data = CacheSplatMap(splatmapIndex);

			if (modifiedOffset.X < 0 || modifiedOffset.Y < 0 ||
				modifiedSize.X <= 0 || modifiedSize.Y <= 0 ||
				modifiedOffset.X + modifiedSize.X > info.heightMapSize ||
				modifiedOffset.Y + modifiedSize.Y > info.heightMapSize)
			{
				GD.PrintErr("Invalid heightmap samples range.");
			}

			info.patchOffset = 0.0f;
			info.patchHeight = 1.0f;

			for (int z = 0; z < modifiedSize.Y; z++)
			{
				for (int x = 0; x < modifiedSize.X; x++)
				{
					data[(z + modifiedOffset.Y) * info.heightMapSize + (x + modifiedOffset.X)] = samples[z * modifiedSize.X + x];
				}
			}

			var splatMapGen = new TerrainSplatMapGenerator(this);
			splatMapGen.WriteColors(ref imgData, ref data);

			image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);

			splatmaps[splatmapIndex].Update(image);
			cachedSplatMap[splatmapIndex] = data;
		}

		/**
		* Get the patch bounding box
		*/
		public Aabb getBounds()
		{
			Vector3 patchoffset = getOffset();

			int i = 0;
			var bounds = new Aabb();
			foreach (TerrainChunk? chunk in chunks)
			{
				Aabb newBounds = chunk.getBounds(info, patchoffset);

				if (i == 0)
					bounds = newBounds;
				else
					bounds = bounds.Merge(newBounds);

				i++;
			}

			return bounds;
		}

		/**
		 * Get the patch offset
		 */
		public Vector3 getOffset()
		{
			float size = info.chunkSize * Terrain3D.UNITS_PER_VERTEX * Terrain3D.PATCH_CHUNK_EDGES;
			return new Vector3(patchCoord.X * size, 0.0f, patchCoord.Y * size);
		}

		/**
		 * Updating the collider data on physic server
		 */
		private void UpdateColliderData(Terrain3D terrain, float[] heightMapData)
		{
			//create heightmap shape
			Aabb bound = getBounds();

			shapeHeight.MapWidth = (int)Math.Sqrt(heightMapData.Length);
			shapeHeight.MapDepth = (int)Math.Sqrt(heightMapData.Length);
			shapeHeight.MapData = heightMapData;
		}

		/**
		 * Updating the collider position on physic server
		 */
		private void UpdateColliderPosition(Terrain3D terrain)
		{
			PhysicsServer3D.BodySetRayPickable(bodyRid.Value, true);
			PhysicsServer3D.BodySetState(bodyRid.Value, PhysicsServer3D.BodyState.Transform, GetColliderPosition(terrain));

		}

		/**
		 * Get collider position by given terrain node
		 */
		public Transform3D GetColliderPosition(Terrain3D terrain, bool attachScale = true)
		{
			int collisionLOD = 1;
			int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
			int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			int heightFieldLength = heightFieldSize * heightFieldSize;

			var scale2 = new Vector3((float)info.heightMapSize / heightFieldSize, 1, (float)info.heightMapSize / heightFieldSize);
			var scaleFac = new Vector3(scale2.X * Terrain3D.UNITS_PER_VERTEX, scale2.X, scale2.X * Terrain3D.UNITS_PER_VERTEX);

			var transform = new Transform3D();
			transform.Origin = terrain.GlobalTransform.Origin + new Vector3(chunks[0].TerrainChunkSizeLOD0 * 2, 0, chunks[0].TerrainChunkSizeLOD0 * 2) + offset;
			transform.Basis = terrain.GlobalTransform.Basis;

			if (attachScale)
				transform.Origin *= terrain.Scale;

			Vector3 scale = transform.Basis.Scale;
			scale.X *= Terrain3D.UNITS_PER_VERTEX;
			scale.Z *= Terrain3D.UNITS_PER_VERTEX;
			transform.Basis = transform.Basis.Scaled(scale); //TODO test: this is new through beta 15 refactoring

			GD.Print(transform.Origin);

			return transform;
		}
	}


}
