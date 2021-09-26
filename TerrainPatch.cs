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
		public Vector2i patchCoord = new Vector2i();

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

		protected RID bodyRid;

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
			foreach (var chunk in chunks)
			{
				chunk.ClearDraw();
			}

			//  if (shapeRid != null)
			{
				//  PhysicsServer3D.FreeRid(shapeRid);
				// shapeRid = null;
			}

			if (bodyRid != null)
			{
				PhysicsServer3D.BodyClearShapes(bodyRid);
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
				var script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainChunk.cs").New();
				var res = script as TerrainChunk;
				res.ResourceLocalToScene = true;

				int px = i % Terrain3D.PATCH_CHUNK_EDGES;
				int py = i / Terrain3D.PATCH_CHUNK_EDGES;
				res.position = new Vector2i((int)px, (int)py);

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
			var image = heightmapGen.createImage();
			var imageBuffer = image.GetData();

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
			var result = heightmapGen.CalculateHeightRange(heightMapdata, ref chunkOffsets, ref chunkHeights);

			info.patchOffset = result.x;
			info.patchHeight = result.y;

			int chunkIndex = 0;
			foreach (var chunk in chunks)
			{
				chunk.offset = chunkOffsets[chunkIndex];
				chunk.height = chunkHeights[chunkIndex];
				chunkIndex++;
			}

			heightmapGen.WriteHeights(ref imageBuffer, ref heightMapdata);
			heightmapGen.WriteNormals(ref imageBuffer, heightMapdata, null, Vector2i.Zero, new Vector2i(info.heightMapSize, info.heightMapSize));

			//store heightmap in rgba8
			image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imageBuffer);

			if (heightmap == null)
			{
				heightmap = new ImageTexture();
				heightmap.CreateFromImage(image);
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
			var splatmapImage = splatmapGen.createImage();
			var splatmapTexture = new ImageTexture();
			var splatmapData = splatmapImage.GetData();

			if (splatmapDataImport == null)
			{
				splatmapImage.Fill(new Color(1, 0, 0, 0));
			}
			else
			{
				splatmapGen.WriteColors(ref splatmapData, ref splatmapDataImport);
				splatmapImage.CreateFromData(splatmapImage.GetWidth(), splatmapImage.GetHeight(), false, Image.Format.Rgba8, splatmapData);
			}

			splatmapTexture.CreateFromImage(splatmapImage);
			splatmaps[idx] = splatmapTexture;
		}

		/**
		 * Drawing a patch by attaching chunks to render device
		 */
		public void Draw(Terrain3D terrainNode, Material mat)
		{
			RID scenario = terrainNode.GetWorld3d().Scenario;

			//clear chunks scene
			foreach (var chunk in chunks)
			{
				chunk.ClearDraw();
			}

			bodyRid = PhysicsServer3D.BodyCreate();

			PhysicsServer3D.BodySetMode(bodyRid, PhysicsServer3D.BodyMode.Static);
			PhysicsServer3D.BodyAttachObjectInstanceId(bodyRid, terrainNode.GetInstanceId());
			PhysicsServer3D.BodySetSpace(bodyRid, terrainNode.GetWorld3d().Space);
			PhysicsServer3D.BodySetCollisionLayer(bodyRid, terrainNode.collisionLayer);
			PhysicsServer3D.BodySetCollisionMask(bodyRid, terrainNode.collisionMask);
			//    PhysicsServer3D.BodySetRayPickable(bodyRid, true);

			//create cache
			int chunkId = 0;
			foreach (var chunk in chunks)
			{
				var start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;;

				chunk.Draw(this, info, scenario, ref heightmap, ref splatmaps, terrainNode, getOffset(), mat);
				chunk.UpdatePosition(info, terrainNode.GlobalTransform, getOffset());
				chunk.SetDefaultMaterial(terrainNode.terrainDefaultTexture);

				var end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

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

				var start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;;

			int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
			int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			int heightFieldLength = heightFieldSize * heightFieldSize;

			var genCollider = new TerrainColliderGenerator(this);
			var heightField = genCollider.GenereateLOD(collisionLOD);


			//create heightmap shape
			PhysicsServer3D.BodyAddShape(bodyRid, shapeHeight.GetRid());

			UpdateColliderData(terrain, heightField);
			UpdateColliderPosition(terrain);
				var end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

			GD.Print("[Collider] Creation time " + (end - start) + " ms");
		}

		public void DisableCollider(bool disable = false)
		{
			if (bodyRid != null)
				PhysicsServer3D.BodySetShapeDisabled(bodyRid, 0, disable);
		}

		public void UpdateSettings(Terrain3D terrainNode)
		{
			PhysicsServer3D.BodySetShapeDisabled(bodyRid, 0, terrainNode.IsInsideTree());

			foreach (var chunk in chunks)
			{
				chunk.UpdateSettings(terrainNode);
			}
		}
		public void UpdatePosition(Terrain3D terrainNode)
		{
			foreach (var chunk in chunks)
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
			var script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatchInfo.cs").New();
			var patch = script as TerrainPatchInfo;
			patch.ResourceLocalToScene = true;
			info = patch;

			var chunkSize = _chunkSize - 1;
			var vertexCountEdge = _chunkSize;
			var heightmapSize = chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			var textureSet = vertexCountEdge * Terrain3D.PATCH_CHUNK_EDGES;


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
		public void UpdateHolesMask(Terrain3D terrain, byte[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
		{
			var image = heightmap.GetImage();
			var holesMask = CacheHoleMask();
			var heightMap = CacheHeightData();

			var imgData = image.GetData();
			if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
				modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
				modifiedOffset.x + modifiedSize.x > info.heightMapSize ||
				modifiedOffset.y + modifiedSize.y > info.heightMapSize)
			{
				GD.PrintErr("Invalid heightmap samples range.");
			}


			int heightMapSize = info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			for (int z = 0; z < modifiedSize.y; z++)
			{
				for (int x = 0; x < modifiedSize.x; x++)
				{
					holesMask[(z + modifiedOffset.y) * heightMapSize + (x + modifiedOffset.x)] = samples[z * modifiedSize.x + x];
				}
			}

			var heightmapGen = new TerrainHeightMapGenerator(this);
			heightmapGen.WriteNormals(ref imgData, heightMap, holesMask, modifiedOffset, modifiedSize);

			image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
			heightmap.Update(image);

			UpdateColliderData(terrain, heightMap);
			UpdatePosition(terrain);

			cachedHolesMask = holesMask;
			terrain.UpdateGizmos();
		}

		/** 
		 *  Updating heightmap texture, collider and rendering device
		 */
		public void UpdateHeightMap(Terrain3D terrain, float[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
		{
			var image = heightmap.GetImage();
			var data = CacheHeightData();

			var imgData = image.GetData();
			if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
				modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
				modifiedOffset.x + modifiedSize.x > info.heightMapSize ||
				modifiedOffset.y + modifiedSize.y > info.heightMapSize)
			{
				GD.PrintErr("Invalid heightmap samples range.");
			}

			info.patchOffset = 0.0f;
			info.patchHeight = 1.0f;

			int heightMapSize = info.chunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
			for (int z = 0; z < modifiedSize.y; z++)
			{
				for (int x = 0; x < modifiedSize.x; x++)
				{
					var index = (z + modifiedOffset.y) * heightMapSize + (x + modifiedOffset.x);
					data[index] = samples[z * modifiedSize.x + x];
				}
			}

			float[] chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
			float[] chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
			var heightmapGen = new TerrainHeightMapGenerator(this);

			var result = heightmapGen.CalculateHeightRange(data, ref chunkOffsets, ref chunkHeights);
			info.patchOffset = result.x;
			info.patchHeight = result.y;

			heightmapGen.WriteHeights(ref imgData, ref data);
			heightmapGen.WriteNormals(ref imgData, data, null, modifiedOffset, modifiedSize);

			int chunkIndex = 0;
			foreach (var chunk in chunks)
			{
				chunk.offset = chunkOffsets[chunkIndex];
				chunk.height = chunkHeights[chunkIndex];
				chunkIndex++;
			}

			image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
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
		public void UpdateSplatMap(int splatmapIndex, Terrain3D terrain, Color[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
		{
			var image = splatmaps[splatmapIndex].GetImage() as Image;
			var imgData = image.GetData();

			var data = CacheSplatMap(splatmapIndex);

			if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
				modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
				modifiedOffset.x + modifiedSize.x > info.heightMapSize ||
				modifiedOffset.y + modifiedSize.y > info.heightMapSize)
			{
				GD.PrintErr("Invalid heightmap samples range.");
			}

			info.patchOffset = 0.0f;
			info.patchHeight = 1.0f;

			for (int z = 0; z < modifiedSize.y; z++)
			{
				for (int x = 0; x < modifiedSize.x; x++)
				{
					data[(z + modifiedOffset.y) * info.heightMapSize + (x + modifiedOffset.x)] = samples[z * modifiedSize.x + x];
				}
			}

			var splatMapGen = new TerrainSplatMapGenerator(this);
			splatMapGen.WriteColors(ref imgData, ref data);

			image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);

			splatmaps[splatmapIndex].Update(image);
			cachedSplatMap[splatmapIndex] = data;
		}

		/**
		* Get the patch bounding box
		*/
		public AABB getBounds()
		{
			var patchoffset = getOffset();

			int i = 0;
			var bounds = new AABB();
			foreach (var chunk in chunks)
			{
				var newBounds = chunk.getBounds(info, patchoffset);

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
			return new Vector3(patchCoord.x * size, 0.0f, patchCoord.y * size);
		}

		/**
		 * Updating the collider data on physic server
		 */
		private void UpdateColliderData(Terrain3D terrain, float[] heightMapData)
		{
			//create heightmap shape
			var bound = getBounds();

			shapeHeight.MapWidth = (int)Math.Sqrt(heightMapData.Length);
			shapeHeight.MapDepth = (int)Math.Sqrt(heightMapData.Length);
			shapeHeight.MapData = heightMapData;
		}

		/**
		 * Updating the collider position on physic server
		 */
		private void UpdateColliderPosition(Terrain3D terrain)
		{
			PhysicsServer3D.BodySetRayPickable(bodyRid, true);
			PhysicsServer3D.BodySetState(bodyRid, PhysicsServer3D.BodyState.Transform, GetColliderPosition(terrain));

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
			var scaleFac = new Vector3(scale2.x * Terrain3D.UNITS_PER_VERTEX, scale2.x, scale2.x * Terrain3D.UNITS_PER_VERTEX);

			var transform = new Transform3D();
			transform.origin = terrain.GlobalTransform.origin + new Vector3(chunks[0].TerrainChunkSizeLOD0 * 2, 0, chunks[0].TerrainChunkSizeLOD0 * 2) + offset;
			transform.basis = terrain.GlobalTransform.basis;

			if (attachScale)
				transform.origin *= terrain.Scale;

			var scale = transform.basis.Scale;
			scale.x *= Terrain3D.UNITS_PER_VERTEX;
			scale.z *= Terrain3D.UNITS_PER_VERTEX;
			transform.basis.Scale = scale;

			GD.Print(transform.origin);

			return transform;
		}
	}


}
