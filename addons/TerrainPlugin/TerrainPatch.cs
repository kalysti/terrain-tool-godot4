using Godot;
using System;
using Godot.Collections;
using TerrainEditor.Generators;
using Array = System.Array;

namespace TerrainEditor;

[Tool]
public partial class TerrainPatch : Resource
{
    private Array<ImageTexture> splatMaps = new();

    //cache for meshes
    private Dictionary<int, ArrayMesh> meshCache = new();
    protected HeightMapShape3D? ShapeHeight;
    private ImageTexture? heightMap;

    protected Rid? BodyRid;

    [Export]
    public Vector2I PatchCoordinates { get; set; }

    [Export]
    public Vector3 Offset { get; set; }

    [Export]
    public Array<TerrainChunk> Chunks { get; set; } = new();

    [Export]
    public ImageTexture? HeightMap
    {
        get => heightMap;
        set => heightMap = value;
    }

    [Export]
    public Array<ImageTexture> SplatMaps
    {
        get => splatMaps;
        set => splatMaps = value;
    }

    [Export]
    public TerrainPatchInfo Info { get; set; } = new();

    [Export]
    public ArrayMesh? CurrentMesh { get; set; }

    public Dictionary<int, ArrayMesh> MeshCache => meshCache;

    /// <summary>
    /// Clear rendering device by removing body and collider
    /// </summary>
    public void ClearDraw()
    {
        foreach (TerrainChunk? chunk in Chunks) chunk.ClearDraw();

        if (BodyRid.HasValue)
        {
            PhysicsServer3D.BodyClearShapes(BodyRid.Value);
            BodyRid = null;
        }

        ShapeHeight = null;
    }

    /// <summary>
    /// Initalize a patch by given chunksize and optional exist heightmap data
    /// </summary>
    public void Init(int chunkSize)
    {
        //reset prev cache
        CachedHeightMapData = Array.Empty<float>(); //TODO: empty out the old one if exist
        CachedHolesMask = Array.Empty<byte>();
        CachedSplatMap = new Array<Color[]>();

        //generate info file
        CreateInfoResource(chunkSize);

        //creating chunks
        Chunks.Clear();
        for (var i = 0; i < Terrain3D.PATCH_CHUNKS_AMOUNT; i++)
        {
            Variant script = GD.Load<CSharpScript>(TerrainPlugin.ResourcePath($"{nameof(TerrainChunk)}.cs")).New();
            if (script.Obj is TerrainChunk res)
            {
                res.ResourceLocalToScene = true;

                int px = i % Terrain3D.PATCH_CHUNK_EDGES;
                int py = i / Terrain3D.PATCH_CHUNK_EDGES;
                res.Position = new Vector2I(px, py);

                //  res.ChunkSizeNextLOD = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);
                res.TerrainChunkSizeLod0 = Terrain3D.UNITS_PER_VERTEX * Info.ChunkSize;
                Chunks.Add(res);
            }
            else
            {
                GD.PrintErr($"script.Obj ({script.Obj}) is not a TerrainChunk");
            }
        }
    }

    /// <summary>
    /// Creating a heightmap by given data
    /// </summary>
    public void CreateHeightmap(float[]? heightMapData = null)
    {
        //generate heightmap
        var heightmapGen = new TerrainHeightMapGenerator(this);
        Image? image = heightmapGen.CreateImage();
        if (image == null)
        {
            GD.PrintErr("Generated height-map image is null.");
            return;
        }

        byte[] imageBuffer = image.GetData();

        if (heightMapData == null)
        {
            heightMapData = new float[Info.HeightMapSize * Info.HeightMapSize];
            for (var i = 0; i < heightMapData.Length; i++) heightMapData[i] = 0f;
        }

        var chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
        var chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];

        //calculate height ranges
        Vector2? result = heightmapGen.CalculateHeightRange(heightMapData, ref chunkOffsets, ref chunkHeights);

        if (result.HasValue)
        {
            Info.PatchOffset = result.Value.X;
            Info.PatchHeight = result.Value.Y;

            var chunkIndex = 0;
            foreach (TerrainChunk? chunk in Chunks)
            {
                chunk.Offset = chunkOffsets[chunkIndex];
                chunk.Height = chunkHeights[chunkIndex];
                chunkIndex++;
            }

            heightmapGen.WriteHeights(ref imageBuffer, ref heightMapData);
            heightmapGen.WriteNormals(ref imageBuffer, heightMapData, Array.Empty<byte>(), Vector2I.Zero, new Vector2I(Info.HeightMapSize, Info.HeightMapSize));

            //store heightmap in rgba8
            image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imageBuffer);

            if (HeightMap == null)
                HeightMap = ImageTexture.CreateFromImage(image);
            else
                HeightMap.Update(image);
        }
        else
        {
            GD.PrintErr("No height-range calculation possible.");
        }
    }

    /// <summary>
    /// Creating a splatmap by given data
    /// </summary>
    public void CreateSplatmap(int idx, Color[]? splatmapDataImport = null)
    {
        splatmapDataImport ??= Array.Empty<Color>();

        if (CachedSplatMap.Count < idx + 1) CachedSplatMap.Resize(idx + 1);

        if (SplatMaps.Count < idx + 1) SplatMaps.Resize(idx + 1);

        var splatmapGen = new TerrainSplatMapGenerator(this);
        Image? splatmapImage = splatmapGen.CreateImage();
        if (splatmapImage == null)
        {
            GD.PrintErr("Generated splat-map image is null");
            return;
        }

        byte[]? splatmapData = splatmapImage.GetData();

        if (splatmapDataImport.IsEmpty())
        {
            splatmapImage.Fill(new Color(1, 0, 0, 0));
        }
        else
        {
            splatmapGen.WriteColors(ref splatmapData, ref splatmapDataImport);
            Image.CreateFromData(splatmapImage.GetWidth(), splatmapImage.GetHeight(), false, Image.Format.Rgba8, splatmapData);
        }

        SplatMaps[idx] = ImageTexture.CreateFromImage(splatmapImage);
    }

    /// <summary>
    /// Drawing a patch by attaching chunks to render device
    /// </summary>
    public void Draw(Terrain3D terrainNode, Material mat)
    {
        Rid scenario = terrainNode.GetWorld3D().Scenario;

        //clear chunks scene
        foreach (TerrainChunk? chunk in Chunks) chunk.ClearDraw();

        BodyRid = PhysicsServer3D.BodyCreate();

        PhysicsServer3D.BodySetMode(BodyRid.Value, PhysicsServer3D.BodyMode.Static);
        PhysicsServer3D.BodyAttachObjectInstanceId(BodyRid.Value, terrainNode.GetInstanceId());
        PhysicsServer3D.BodySetSpace(BodyRid.Value, terrainNode.GetWorld3D().Space);
        PhysicsServer3D.BodySetCollisionLayer(BodyRid.Value, terrainNode.CollisionLayer);
        PhysicsServer3D.BodySetCollisionMask(BodyRid.Value, terrainNode.CollisionMask);
        //    PhysicsServer3D.BodySetRayPickable(bodyRid, true);

        //create cache
        var chunkId = 0;
        foreach (TerrainChunk? chunk in Chunks)
        {
            long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            chunk.Draw(this, Info, scenario, ref heightMap, ref splatMaps, terrainNode, GetOffset(), mat);
            chunk.UpdatePosition(Info, terrainNode.GlobalTransform, GetOffset());
            if (terrainNode.TerrainDefaultTexture == null)
                GD.PrintErr("TerrainDefaultTexture is null");
            else
                chunk.SetDefaultMaterial(terrainNode.TerrainDefaultTexture);

            long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            GD.Print($"[Chunk][{chunkId}] Draw time {end - start}ms");
            chunkId++;
        }

        //Creating collider
        CreateCollider(0, terrainNode);
        UpdatePosition(terrainNode);

        terrainNode.UpdateGizmos();

        CurrentMesh = meshCache[0];
    }


    /// <summary>
    /// Create a collider on physic server
    /// </summary>
    private void CreateCollider(int collisionLod, Terrain3D terrain)
    {
        ShapeHeight = new HeightMapShape3D();

        long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        // int heightFieldChunkSize = ((Info.ChunkSize + 1) >> collisionLod) - 1;
        // int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        // int heightFieldLength = heightFieldSize * heightFieldSize;

        var genCollider = new TerrainColliderGenerator(this);
        float[] heightField = genCollider.GenerateLod(collisionLod);


        //create heightmap shape
        if (BodyRid == null)
            GD.PrintErr($"{nameof(BodyRid)} is null");
        else
            PhysicsServer3D.BodyAddShape(BodyRid.Value, ShapeHeight.GetRid());

        UpdateColliderData(terrain, heightField);
        UpdateColliderPosition(terrain);
        long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        GD.Print($"[Collider] Creation time {end - start} ms");
    }

    public void DisableCollider(bool disable = false)
    {
        if (BodyRid != null)
            PhysicsServer3D.BodySetShapeDisabled(BodyRid.Value, 0, disable);
    }

    public void UpdateSettings(Terrain3D terrainNode)
    {
        if (BodyRid != null)
            PhysicsServer3D.BodySetShapeDisabled(BodyRid.Value, 0, terrainNode.IsInsideTree());

        foreach (TerrainChunk? chunk in Chunks) chunk.UpdateSettings(terrainNode);
    }

    public void UpdatePosition(Terrain3D terrainNode)
    {
        foreach (TerrainChunk? chunk in Chunks) chunk.UpdatePosition(Info, terrainNode.GlobalTransform, GetOffset());

        UpdateColliderPosition(terrainNode);
    }

    /// <summary> 
    ///  Creating info resource which contains all sizes
    /// </summary>
    private void CreateInfoResource(int chunkSize)
    {
        Variant script = GD.Load<CSharpScript>(TerrainPlugin.ResourcePath($"{nameof(TerrainPatchInfo)}.cs")).New();
        if (script.Obj is TerrainPatchInfo patch)
        {
            patch.ResourceLocalToScene = true;
            Info = patch;
        }
        else
        {
            GD.PrintErr($"script.Obj ({script.Obj}) is not a TerrainPathInfo");
        }

        int newChunkSize = chunkSize - 1;
        int heightmapSize = newChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        int textureSet = chunkSize * Terrain3D.PATCH_CHUNK_EDGES;


        Info.PatchOffset = 0.0f;
        Info.PatchHeight = 1.0f;
        Info.ChunkSize = newChunkSize;
        Info.VertexCountEdge = chunkSize;
        Info.HeightMapSize = heightmapSize;
        Info.TextureSize = textureSet;
    }

    /// <summary> 
    ///  Updating heightmap texture for holes, collider and rendering device
    /// </summary>
    public void UpdateHolesMask(Terrain3D terrain, byte[] samples, Vector2I modifiedOffset, Vector2I modifiedSize)
    {
        Image? image = HeightMap?.GetImage();
        if (image == null)
        {
            GD.PrintErr("height-map or height-map-image is null");
            return;
        }

        byte[] holesMask = CacheHoleMask();
        float[] cacheHeightData = CacheHeightData();

        byte[] imgData = image.GetData();
        if (modifiedOffset.X < 0 || modifiedOffset.Y < 0 ||
            modifiedSize.X <= 0 || modifiedSize.Y <= 0 ||
            modifiedOffset.X + modifiedSize.X > Info.HeightMapSize ||
            modifiedOffset.Y + modifiedSize.Y > Info.HeightMapSize)
            GD.PrintErr("Invalid heightmap samples range.");


        int heightMapSize = Info.ChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        for (var z = 0; z < modifiedSize.Y; z++)
        for (var x = 0; x < modifiedSize.X; x++)
            holesMask[(z + modifiedOffset.Y) * heightMapSize + x + modifiedOffset.X] = samples[z * modifiedSize.X + x];

        var heightmapGen = new TerrainHeightMapGenerator(this);
        heightmapGen.WriteNormals(ref imgData, cacheHeightData, holesMask, modifiedOffset, modifiedSize);

        image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
        heightMap?.Update(image);

        UpdateColliderData(terrain, cacheHeightData);
        UpdatePosition(terrain);

        CachedHolesMask = holesMask;
        terrain.UpdateGizmos();
    }

    /// <summary> 
    ///  Updating heightmap texture, collider and rendering device
    /// </summary>
    public void UpdateHeightMap(Terrain3D terrain, float[] samples, Vector2I modifiedOffset, Vector2I modifiedSize)
    {
        Image? image = HeightMap?.GetImage();
        if (image == null)
        {
            GD.PrintErr("height-map or height-map-image is null");
            return;
        }

        float[] data = CacheHeightData();

        byte[] imgData = image.GetData();
        if (modifiedOffset.X < 0 || modifiedOffset.Y < 0 ||
            modifiedSize.X <= 0 || modifiedSize.Y <= 0 ||
            modifiedOffset.X + modifiedSize.X > Info.HeightMapSize ||
            modifiedOffset.Y + modifiedSize.Y > Info.HeightMapSize)
            GD.PrintErr("Invalid heightmap samples range.");

        Info.PatchOffset = 0.0f;
        Info.PatchHeight = 1.0f;

        int heightMapSize = Info.ChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        for (var z = 0; z < modifiedSize.Y; z++)
        for (var x = 0; x < modifiedSize.X; x++)
        {
            int index = (z + modifiedOffset.Y) * heightMapSize + x + modifiedOffset.X;
            data[index] = samples[z * modifiedSize.X + x];
        }

        var chunkOffsets = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
        var chunkHeights = new float[Terrain3D.PATCH_CHUNKS_AMOUNT];
        var heightmapGen = new TerrainHeightMapGenerator(this);

        Vector2? result = heightmapGen.CalculateHeightRange(data, ref chunkOffsets, ref chunkHeights);
        if (result.HasValue)
        {
            Info.PatchOffset = result.Value.X;
            Info.PatchHeight = result.Value.Y;

            heightmapGen.WriteHeights(ref imgData, ref data);
            heightmapGen.WriteNormals(ref imgData, data, Array.Empty<byte>(), modifiedOffset, modifiedSize);

            var chunkIndex = 0;
            foreach (TerrainChunk? chunk in Chunks)
            {
                chunk.Offset = chunkOffsets[chunkIndex];
                chunk.Height = chunkHeights[chunkIndex];
                chunkIndex++;
            }

            image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);
            heightMap?.Update(image);

            // var genCollider = new TerrainColliderGenerator(this);
            UpdateColliderData(terrain, data);
            UpdatePosition(terrain);

            //set the cache
            CachedHeightMapData = data;
            terrain.UpdateGizmos();
        }
        else
        {
            GD.PrintErr("No height-range calculation possible.");
        }
    }

    /// <summary> 
    ///  Updating splatmap texture, collider and rendering device
    /// </summary>
    public void UpdateSplatMap(int splatmapIndex, Terrain3D terrain, Color[] samples, Vector2I modifiedOffset, Vector2I modifiedSize)
    {
        Image? image = SplatMaps[splatmapIndex].GetImage();
        if (image == null)
        {
            GD.PrintErr("height-map or height-map-image is null");
            return;
        }

        byte[] imgData = image.GetData();

        Color[] data = CacheSplatMap(splatmapIndex);

        if (modifiedOffset.X < 0 || modifiedOffset.Y < 0 ||
            modifiedSize.X <= 0 || modifiedSize.Y <= 0 ||
            modifiedOffset.X + modifiedSize.X > Info.HeightMapSize ||
            modifiedOffset.Y + modifiedSize.Y > Info.HeightMapSize)
            GD.PrintErr("Invalid heightmap samples range.");

        Info.PatchOffset = 0.0f;
        Info.PatchHeight = 1.0f;

        for (var z = 0; z < modifiedSize.Y; z++)
        for (var x = 0; x < modifiedSize.X; x++)
            data[(z + modifiedOffset.Y) * Info.HeightMapSize + x + modifiedOffset.X] = samples[z * modifiedSize.X + x];

        var splatMapGen = new TerrainSplatMapGenerator(this);
        splatMapGen.WriteColors(ref imgData, ref data);

        image = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, imgData);

        SplatMaps[splatmapIndex].Update(image);
        CachedSplatMap[splatmapIndex] = data;
    }

    /// <summary>
    /// Get the patch bounding box
    /// </summary>
    public Aabb GetBounds()
    {
        Vector3 patchOffset = GetOffset();

        var i = 0;
        var bounds = new Aabb();
        foreach (TerrainChunk? chunk in Chunks)
        {
            Aabb newBounds = chunk.GetBounds(Info, patchOffset);

            if (i == 0)
                bounds = newBounds;
            else
                bounds = bounds.Merge(newBounds);

            i++;
        }

        return bounds;
    }

    /// <summary>
    /// Get the patch offset
    /// </summary>
    public Vector3 GetOffset()
    {
        float size = Info.ChunkSize * Terrain3D.UNITS_PER_VERTEX * Terrain3D.PATCH_CHUNK_EDGES;
        return new Vector3(PatchCoordinates.X * size, 0.0f, PatchCoordinates.Y * size);
    }

    /// <summary>
    /// Updating the collider data on physic server
    /// </summary>
    private void UpdateColliderData(Terrain3D terrain, float[] heightMapData)
    {
        //create heightmap shape
        // Aabb bound = GetBounds();

        if (ShapeHeight == null)
        {
            GD.PrintErr($"{nameof(ShapeHeight)} is null");
        }
        else
        {
            ShapeHeight.MapWidth = (int)Math.Sqrt(heightMapData.Length);
            ShapeHeight.MapDepth = (int)Math.Sqrt(heightMapData.Length);
            ShapeHeight.MapData = heightMapData;
        }
    }

    /// <summary>
    /// Updating the collider position on physic server
    /// </summary>
    private void UpdateColliderPosition(Terrain3D terrain)
    {
        if (BodyRid == null)
        {
            GD.PrintErr($"{nameof(BodyRid)} is null");
        }
        else
        {
            PhysicsServer3D.BodySetRayPickable(BodyRid.Value, true);
            PhysicsServer3D.BodySetState(BodyRid.Value, PhysicsServer3D.BodyState.Transform, GetColliderPosition(terrain));
        }
    }

    /// <summary>
    /// Get collider position by given terrain node
    /// </summary>
    public Transform3D GetColliderPosition(Terrain3D terrain, bool attachScale = true)
    {
        // const int collisionLod = 1;
        // int heightFieldChunkSize = ((Info.ChunkSize + 1) >> collisionLod) - 1;
        // int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        // int heightFieldLength = heightFieldSize * heightFieldSize;

        // var scale2 = new Vector3((float)Info.HeightMapSize / heightFieldSize, 1, (float)Info.HeightMapSize / heightFieldSize);
        // var scaleFac = new Vector3(scale2.X * Terrain3D.UNITS_PER_VERTEX, scale2.X, scale2.X * Terrain3D.UNITS_PER_VERTEX);

        var transform = new Transform3D
        {
            Origin = terrain.GlobalTransform.Origin + new Vector3(Chunks[0].TerrainChunkSizeLod0 * 2, 0, Chunks[0].TerrainChunkSizeLod0 * 2) + Offset,
            Basis = terrain.GlobalTransform.Basis
        };

        if (attachScale)
            transform.Origin *= terrain.Scale;

        Vector3 scale = transform.Basis.Scale;
        scale.X *= Terrain3D.UNITS_PER_VERTEX;
        scale.Z *= Terrain3D.UNITS_PER_VERTEX;
        transform.Basis = transform.Basis.Scaled(scale); //TODO: test

        GD.Print(transform.Origin);

        return transform;
    }
}