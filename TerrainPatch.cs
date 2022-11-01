using Godot;
using System;
using Godot.Collections;
using TerrainEditor.Generators;

namespace TerrainEditor;

[Tool]
public partial class TerrainPatch : Resource
{
    private Array<ImageTexture> splatmaps = new();

    //cache for meshes
    private Dictionary<int, ArrayMesh> meshCache = new();
    protected HeightMapShape3D? ShapeHeight;
    private ImageTexture? heightmap;

    protected RID? BodyRid;

    [Export]
    public Vector2i PatchCoord { get; set; }

    [Export]
    public Vector3 Offset { get; set; }

    [Export]
    public Array<TerrainChunk> Chunks { get; set; } = new();

    [Export]
    public ImageTexture Heightmap
    {
        get => heightmap;
        set => heightmap = value;
    }

    [Export]
    public Array<ImageTexture> Splatmaps
    {
        get => splatmaps;
        set => splatmaps = value;
    }

    [Export]
    public TerrainPatchInfo Info { get; set; } = new();

    [Export]
    public ArrayMesh CurrentMesh { get; set; }

    public Dictionary<int, ArrayMesh> MeshCache => meshCache;

    /**
		 * Clear rendering device by removing body and collider
		 */
    public void ClearDraw()
    {
        foreach (TerrainChunk? chunk in Chunks)
        {
            chunk.ClearDraw();
        }

        //  if (shapeRid != null)
        {
            //  PhysicsServer3D.FreeRid(shapeRid);
            // shapeRid = null;
        }

        if (BodyRid.HasValue)
        {
            PhysicsServer3D.BodyClearShapes(BodyRid.Value);
            BodyRid = null;
        }

        ShapeHeight = null;
    }

    /**
		 * Initalize a patch by given chunksize and optional exist heightmap data
		 */
    public void Init(int chunkSize)
    {
        //reset prev cache
        CachedHeightMapData = null;
        CachedHolesMask = null;
        CachedSplatMap = new Array<Color[]>();

        //generate info file
        CreateInfoResource(chunkSize);

        //creating chunks
        Chunks.Clear();
        for (var i = 0; i < Terrain3D.PATCH_CHUNKS_AMOUNT; i++)
        {
            Variant script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainChunk.cs").New();
            var res = script.Obj as TerrainChunk;
            res.ResourceLocalToScene = true;

            int px = i % Terrain3D.PATCH_CHUNK_EDGES;
            int py = i / Terrain3D.PATCH_CHUNK_EDGES;
            res.Position = new Vector2i((int)px, (int)py);

            //  res.ChunkSizeNextLOD = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);
            res.TerrainChunkSizeLod0 = Terrain3D.UNITS_PER_VERTEX * Info.ChunkSize;
            Chunks.Add(res);
        }
    }

    /**
		* Creating a heightmap by given data
		*/
    public void CreateHeightmap(float[]? heightMapData = null)
    {
        //generate heightmap
        var heightmapGen = new TerrainHeightMapGenerator(this);
        Image? image = heightmapGen.CreateImage();
        byte[]? imageBuffer = image.GetData();

        if (heightMapData == null)
        {
            heightMapData = new float[Info.HeightMapSize * Info.HeightMapSize];
            for (var i = 0; i < heightMapData.Length; i++)
            {
                heightMapData[i] = 0f;
            }
        }

        var chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
        var chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];

        //calculate height ranges
        Vector2 result = heightmapGen.CalculateHeightRange(heightMapData, ref chunkOffsets, ref chunkHeights);

        Info.PatchOffset = result.x;
        Info.PatchHeight = result.y;

        var chunkIndex = 0;
        foreach (TerrainChunk? chunk in Chunks)
        {
            chunk.Offset = chunkOffsets[chunkIndex];
            chunk.Height = chunkHeights[chunkIndex];
            chunkIndex++;
        }

        heightmapGen.WriteHeights(ref imageBuffer, ref heightMapData);
        heightmapGen.WriteNormals(ref imageBuffer, heightMapData, null, Vector2i.Zero, new Vector2i(Info.HeightMapSize, Info.HeightMapSize));

        //store heightmap in rgba8
        image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imageBuffer);

        if (Heightmap == null)
        {
            Heightmap = ImageTexture.CreateFromImage(image);
        }
        else
        {
            Heightmap.Update(image);
        }
    }

    /**
		 * Creating a splatmap by given data
		 */
    public void CreateSplatmap(int idx, Color[] splatmapDataImport = null)
    {
        if (CachedSplatMap.Count < (idx + 1))
        {
            CachedSplatMap.Resize(idx + 1);
        }

        if (Splatmaps.Count < (idx + 1))
        {
            Splatmaps.Resize(idx + 1);
        }

        var splatmapGen = new TerrainSplatMapGenerator(this);
        Image? splatmapImage = splatmapGen.CreateImage();
        var splatmapTexture = new ImageTexture();
        byte[]? splatmapData = splatmapImage.GetData();

        if (splatmapDataImport == null)
        {
            splatmapImage.Fill(new Color(1, 0, 0, 0));
        }
        else
        {
            splatmapGen.WriteColors(ref splatmapData, ref splatmapDataImport);
            splatmapImage.CreateFromData(splatmapImage.GetWidth(), splatmapImage.GetHeight(), false, Image.Format.Rgba8, splatmapData);
        }

        splatmapTexture = ImageTexture.CreateFromImage(splatmapImage);
        Splatmaps[idx] = splatmapTexture;
    }

    /**
		 * Drawing a patch by attaching chunks to render device
		 */
    public void Draw(Terrain3D terrainNode, Material mat)
    {
        RID scenario = terrainNode.GetWorld3d().Scenario;

        //clear chunks scene
        foreach (TerrainChunk? chunk in Chunks)
        {
            chunk.ClearDraw();
        }

        BodyRid = PhysicsServer3D.BodyCreate();

        PhysicsServer3D.BodySetMode(BodyRid.Value, PhysicsServer3D.BodyMode.Static);
        PhysicsServer3D.BodyAttachObjectInstanceId(BodyRid.Value, terrainNode.GetInstanceId());
        PhysicsServer3D.BodySetSpace(BodyRid.Value, terrainNode.GetWorld3d().Space);
        PhysicsServer3D.BodySetCollisionLayer(BodyRid.Value, terrainNode.CollisionLayer);
        PhysicsServer3D.BodySetCollisionMask(BodyRid.Value, terrainNode.CollisionMask);
        //    PhysicsServer3D.BodySetRayPickable(bodyRid, true);

        //create cache
        var chunkId = 0;
        foreach (TerrainChunk? chunk in Chunks)
        {
            long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            chunk.Draw(this, Info, scenario, ref heightmap, ref splatmaps, terrainNode, GetOffset(), mat);
            chunk.UpdatePosition(Info, terrainNode.GlobalTransform, GetOffset());
            chunk.SetDefaultMaterial(terrainNode.TerrainDefaultTexture);

            long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            GD.Print("[Chunk][" + chunkId + "] Draw time " + (end - start) + "ms");
            chunkId++;
        }

        //Creating collider
        CreateCollider(0, terrainNode);
        UpdatePosition(terrainNode);

        terrainNode.UpdateGizmos();

        CurrentMesh = meshCache[0];
    }


    /**
		 * Create a collider on physic server
		 */
    private void CreateCollider(int collisionLod, Terrain3D terrain)
    {
        ShapeHeight = new HeightMapShape3D();

        long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        int heightFieldChunkSize = ((Info.ChunkSize + 1) >> collisionLod) - 1;
        int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        int heightFieldLength = heightFieldSize * heightFieldSize;

        var genCollider = new TerrainColliderGenerator(this);
        float[]? heightField = genCollider.GenerateLod(collisionLod);


        //create heightmap shape
        PhysicsServer3D.BodyAddShape(BodyRid.Value, ShapeHeight.GetRid());

        UpdateColliderData(terrain, heightField);
        UpdateColliderPosition(terrain);
        long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        GD.Print("[Collider] Creation time " + (end - start) + " ms");
    }

    public void DisableCollider(bool disable = false)
    {
        if (BodyRid != null)
            PhysicsServer3D.BodySetShapeDisabled(BodyRid.Value, 0, disable);
    }

    public void UpdateSettings(Terrain3D terrainNode)
    {
        PhysicsServer3D.BodySetShapeDisabled(BodyRid.Value, 0, terrainNode.IsInsideTree());

        foreach (TerrainChunk? chunk in Chunks)
        {
            chunk.UpdateSettings(terrainNode);
        }
    }

    public void UpdatePosition(Terrain3D terrainNode)
    {
        foreach (TerrainChunk? chunk in Chunks)
        {
            chunk.UpdatePosition(Info, terrainNode.GlobalTransform, GetOffset());
        }

        UpdateColliderPosition(terrainNode);
    }

    /** 
		*  Creating info resource which contains all sizes
		*/
    private void CreateInfoResource(int chunkSize)
    {
        Variant script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatchInfo.cs").New();
        var patch = script.Obj as TerrainPatchInfo;
        patch.ResourceLocalToScene = true;
        Info = patch;

        int newChunkSize = chunkSize - 1;
        int vertexCountEdge = chunkSize;
        int heightmapSize = newChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        int textureSet = vertexCountEdge * Terrain3D.PATCH_CHUNK_EDGES;


        Info.PatchOffset = 0.0f;
        Info.PatchHeight = 1.0f;
        Info.ChunkSize = newChunkSize;
        Info.VertexCountEdge = vertexCountEdge;
        Info.HeightMapSize = heightmapSize;
        Info.TextureSize = textureSet;
    }

    /** 
		 *  Updating heightmap texture for holes, collider and rendering device
		 */
    public void UpdateHolesMask(Terrain3D terrain, byte[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
    {
        Image? image = Heightmap.GetImage();
        byte[]? holesMask = CacheHoleMask();
        float[]? heightMap = CacheHeightData();

        byte[]? imgData = image.GetData();
        if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
            modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
            modifiedOffset.x + modifiedSize.x > Info.HeightMapSize ||
            modifiedOffset.y + modifiedSize.y > Info.HeightMapSize)
        {
            GD.PrintErr("Invalid heightmap samples range.");
        }


        int heightMapSize = Info.ChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        for (var z = 0; z < modifiedSize.y; z++)
        {
            for (var x = 0; x < modifiedSize.x; x++)
            {
                holesMask[(z + modifiedOffset.y) * heightMapSize + (x + modifiedOffset.x)] = samples[z * modifiedSize.x + x];
            }
        }

        var heightmapGen = new TerrainHeightMapGenerator(this);
        heightmapGen.WriteNormals(ref imgData, heightMap, holesMask, modifiedOffset, modifiedSize);

        image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
        Heightmap.Update(image);

        UpdateColliderData(terrain, heightMap);
        UpdatePosition(terrain);

        CachedHolesMask = holesMask;
        terrain.UpdateGizmos();
    }

    /** 
		 *  Updating heightmap texture, collider and rendering device
		 */
    public void UpdateHeightMap(Terrain3D terrain, float[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
    {
        Image? image = Heightmap.GetImage();
        float[]? data = CacheHeightData();

        byte[]? imgData = image.GetData();
        if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
            modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
            modifiedOffset.x + modifiedSize.x > Info.HeightMapSize ||
            modifiedOffset.y + modifiedSize.y > Info.HeightMapSize)
        {
            GD.PrintErr("Invalid heightmap samples range.");
        }

        Info.PatchOffset = 0.0f;
        Info.PatchHeight = 1.0f;

        int heightMapSize = Info.ChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        for (var z = 0; z < modifiedSize.y; z++)
        {
            for (var x = 0; x < modifiedSize.x; x++)
            {
                int index = (z + modifiedOffset.y) * heightMapSize + (x + modifiedOffset.x);
                data[index] = samples[z * modifiedSize.x + x];
            }
        }

        var chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
        var chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
        var heightmapGen = new TerrainHeightMapGenerator(this);

        Vector2 result = heightmapGen.CalculateHeightRange(data, ref chunkOffsets, ref chunkHeights);
        Info.PatchOffset = result.x;
        Info.PatchHeight = result.y;

        heightmapGen.WriteHeights(ref imgData, ref data);
        heightmapGen.WriteNormals(ref imgData, data, null, modifiedOffset, modifiedSize);

        var chunkIndex = 0;
        foreach (TerrainChunk? chunk in Chunks)
        {
            chunk.Offset = chunkOffsets[chunkIndex];
            chunk.Height = chunkHeights[chunkIndex];
            chunkIndex++;
        }

        image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
        Heightmap.Update(image);

        var genCollider = new TerrainColliderGenerator(this);
        UpdateColliderData(terrain, data);
        UpdatePosition(terrain);

        //set the cache
        CachedHeightMapData = data;
        terrain.UpdateGizmos();
    }

    /** 
		 *  Updating splatmap texture, collider and rendering device
		 */
    public void UpdateSplatMap(int splatmapIndex, Terrain3D terrain, Color[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
    {
        var image = Splatmaps[splatmapIndex].GetImage() as Image;
        byte[]? imgData = image.GetData();

        Color[]? data = CacheSplatMap(splatmapIndex);

        if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
            modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
            modifiedOffset.x + modifiedSize.x > Info.HeightMapSize ||
            modifiedOffset.y + modifiedSize.y > Info.HeightMapSize)
        {
            GD.PrintErr("Invalid heightmap samples range.");
        }

        Info.PatchOffset = 0.0f;
        Info.PatchHeight = 1.0f;

        for (var z = 0; z < modifiedSize.y; z++)
        {
            for (var x = 0; x < modifiedSize.x; x++)
            {
                data[(z + modifiedOffset.y) * Info.HeightMapSize + (x + modifiedOffset.x)] = samples[z * modifiedSize.x + x];
            }
        }

        var splatMapGen = new TerrainSplatMapGenerator(this);
        splatMapGen.WriteColors(ref imgData, ref data);

        image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);

        Splatmaps[splatmapIndex].Update(image);
        CachedSplatMap[splatmapIndex] = data;
    }

    /**
		* Get the patch bounding box
		*/
    public AABB GetBounds()
    {
        Vector3 patchoffset = GetOffset();

        var i = 0;
        var bounds = new AABB();
        foreach (TerrainChunk? chunk in Chunks)
        {
            AABB newBounds = chunk.GetBounds(Info, patchoffset);

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
    public Vector3 GetOffset()
    {
        float size = Info.ChunkSize * Terrain3D.UNITS_PER_VERTEX * Terrain3D.PATCH_CHUNK_EDGES;
        return new Vector3(PatchCoord.x * size, 0.0f, PatchCoord.y * size);
    }

    /**
		 * Updating the collider data on physic server
		 */
    private void UpdateColliderData(Terrain3D terrain, float[] heightMapData)
    {
        //create heightmap shape
        AABB bound = GetBounds();

        ShapeHeight.MapWidth = (int)Math.Sqrt(heightMapData.Length);
        ShapeHeight.MapDepth = (int)Math.Sqrt(heightMapData.Length);
        ShapeHeight.MapData = heightMapData;
    }

    /**
		 * Updating the collider position on physic server
		 */
    private void UpdateColliderPosition(Terrain3D terrain)
    {
        PhysicsServer3D.BodySetRayPickable(BodyRid.Value, true);
        PhysicsServer3D.BodySetState(BodyRid.Value, PhysicsServer3D.BodyState.Transform, GetColliderPosition(terrain));
    }

    /**
		 * Get collider position by given terrain node
		 */
    public Transform3D GetColliderPosition(Terrain3D terrain, bool attachScale = true)
    {
        var collisionLod = 1;
        int heightFieldChunkSize = ((Info.ChunkSize + 1) >> collisionLod) - 1;
        int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        int heightFieldLength = heightFieldSize * heightFieldSize;

        var scale2 = new Vector3((float)Info.HeightMapSize / heightFieldSize, 1, (float)Info.HeightMapSize / heightFieldSize);
        var scaleFac = new Vector3(scale2.x * Terrain3D.UNITS_PER_VERTEX, scale2.x, scale2.x * Terrain3D.UNITS_PER_VERTEX);

        var transform = new Transform3D();
        transform.origin = terrain.GlobalTransform.origin + new Vector3(Chunks[0].TerrainChunkSizeLod0 * 2, 0, Chunks[0].TerrainChunkSizeLod0 * 2) + Offset;
        transform.basis = terrain.GlobalTransform.basis;

        if (attachScale)
            transform.origin *= terrain.Scale;

        Vector3 scale = transform.basis.Scale;
        scale.x *= Terrain3D.UNITS_PER_VERTEX;
        scale.z *= Terrain3D.UNITS_PER_VERTEX;
        transform.basis.Scale = scale;

        GD.Print(transform.origin);

        return transform;
    }
}