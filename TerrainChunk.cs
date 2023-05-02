using Godot;
using System;
using TerrainEditor.Utils;

namespace TerrainEditor;

[Tool]
public partial class TerrainChunk : Resource
{
    [Export]
    public Vector2I Position { get; set; }

    [Export]
    public Vector2 OffsetUv { get; set; }

    [Export]
    public float TerrainChunkSizeLod0 { get; set; }

    [Export]
    public float Offset { get; set; }

    [Export]
    public float Height { get; set; } = 1;

    protected Rid? InstanceRid;

    protected Rid? MaterialId;

    protected Material? MaterialInUse;

    protected Rid? MeshId;

    protected Mesh? Mesh { get; set; }

    protected Godot.Collections.Array<TerrainChunk> Neighbors = new();

    [Export]
    public int CachedDrawLod { get; set; } //not finish

    /// <summary>
    ///   Get the bounding box of this chunk
    /// </summary>
    public Aabb GetBounds(TerrainPatchInfo info, Vector3 patchOffset)
    {
        float size = info.ChunkSize * Terrain3D.UNITS_PER_VERTEX;
        Vector3 origin = patchOffset + new Vector3(Position.X * size, Offset, Position.Y * size);
        var bounds = new Aabb(origin, new Vector3(size, Height, size));

        return bounds;
    }

    /// <summary>
    ///  Detecting lod levels of neighbors
    /// </summary>
    protected Plane GetNeighbors()
    {
        int lod = CachedDrawLod;
        int minLod = Math.Max(lod + 1, 0);

        var pl = new Plane
        {
            X = Math.Clamp(Neighbors[0].CachedDrawLod, lod, minLod),
            Y = Math.Clamp(Neighbors[1].CachedDrawLod, lod, minLod),
            Z = Math.Clamp(Neighbors[2].CachedDrawLod, lod, minLod),
            D = Math.Clamp(Neighbors[3].CachedDrawLod, lod, minLod)
        };

        return pl;
    }

    /// <summary>
    ///  Clear and reset draw (free on physic server)
    /// </summary>
    public void ClearDraw()
    {
        if (MeshId.HasValue) RenderingServer.FreeRid(MeshId.Value);

        if (InstanceRid.HasValue) RenderingServer.FreeRid(InstanceRid.Value);

        Mesh = null;
        MaterialInUse = null;
        MeshId = null;
        MaterialId = null;
        InstanceRid = null;
    }

    /// <summary>
    ///  Updating position of chunk
    /// </summary>
    public Transform3D UpdatePosition(TerrainPatchInfo info, Transform3D terrainTransform, Vector3 patchOffset)
    {
        float size = info.ChunkSize * Terrain3D.UNITS_PER_VERTEX;
        Vector3 localPosition = patchOffset + new Vector3(Position.X * size, info.PatchOffset, Position.Y * size);
        var localTransform = new Transform3D
        {
            Origin = localPosition,
            Basis = new Basis(Quaternion.Identity)
        };
        localTransform.Basis = localTransform.Basis.Scaled(new Vector3(1.0f, info.PatchHeight, 1.0f));

        Transform3D global = terrainTransform * localTransform;

        if (InstanceRid.HasValue)
            RenderingServer.InstanceSetTransform(InstanceRid.Value, global);

        if (MaterialId.HasValue)
            RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.UV_SCALE, GetUvScale());

        return global;
    }


    /// <summary>
    ///  Get the UV Scale for the chunk
    /// </summary>
    private Plane GetUvScale()
    {
        Quaternion q = new Quaternion(1.0f, 1.0f, Position.X, Position.Y) * (1.0f / Terrain3D.PATCH_CHUNK_EDGES);
        return new Plane(q.X, q.Y, q.Z, q.W);
    }

    /// <summary>
    ///  Send inspector material to shader
    /// </summary>
    public void UpdateInspectorMaterial(Color color, Plane brushData0, Plane brushData1)
    {
        if (MaterialId == null)
        {
            GD.PrintErr($"{nameof(MaterialId)} is null");
            return;
        }

        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.COLOR, color);
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.BRUSH_DATA0, brushData0);
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.BRUSH_DATA1, brushData1);
    }

    /// <summary>
    ///  Send default material to shader
    /// </summary>
    public void SetDefaultMaterial(Texture2D image)
    {
        if (MaterialId == null)
        {
            GD.PrintErr($"{nameof(MaterialId)} is null");
            return;
        }

        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_DEFAULT_MATERIAL, image.GetRid());
    }

    /// <summary>
    ///  Cache neighbours for lod detection
    /// </summary>
    public void CacheNeighbors(Terrain3D terrainNode, TerrainPatch currentPatch)
    {
        Neighbors.Clear();
        Neighbors.Add(this);
        Neighbors.Add(this);
        Neighbors.Add(this);
        Neighbors.Add(this);

        // 0: bottom
        if (Position.Y > 0)
        {
            Neighbors[0] = currentPatch.Chunks[(Position.Y - 1) * Terrain3D.PATCH_CHUNK_EDGES + Position.X];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.X, Position.Y - 1);
            if (patch != null)
                Neighbors[0] = patch.Chunks[(Terrain3D.PATCH_CHUNK_EDGES - 1) * Terrain3D.PATCH_CHUNK_EDGES + Position.X];
        }

        // 1: left
        if (Position.X > 0)
        {
            Neighbors[1] = currentPatch.Chunks[Position.Y * Terrain3D.PATCH_CHUNK_EDGES + (Position.X - 1)];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.X - 1, Position.Y);
            if (patch != null)
                Neighbors[1] = patch.Chunks[Position.Y * Terrain3D.PATCH_CHUNK_EDGES + (Terrain3D.PATCH_CHUNK_EDGES - 1)];
        }

        // 2: right
        if (Position.X < Terrain3D.PATCH_CHUNK_EDGES - 1)
        {
            Neighbors[2] = currentPatch.Chunks[Position.Y * Terrain3D.PATCH_CHUNK_EDGES + Position.X + 1];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.X + 1, Position.Y);
            if (patch != null)
                Neighbors[2] = patch.Chunks[Position.Y * Terrain3D.PATCH_CHUNK_EDGES];
        }

        // 3: top
        if (Position.Y < Terrain3D.PATCH_CHUNK_EDGES - 1)
        {
            Neighbors[3] = currentPatch.Chunks[(Position.Y + 1) * Terrain3D.PATCH_CHUNK_EDGES + Position.X];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.X, Position.Y + 1);
            if (patch != null)
                Neighbors[3] = patch.Chunks[Position.X];
        }
    }

    /// <summary>
    ///  Draw the chunk on physic server
    /// </summary>
    public void Draw(TerrainPatch patch, TerrainPatchInfo info, Rid scenario, ref ImageTexture? heightMap, ref Godot.Collections.Array<ImageTexture> splatMaps, Terrain3D tf, Vector3 patchoffset, Material mat)
    {
        Mesh = GenerateMesh(patch, info.ChunkSize, 0);
        MeshId = Mesh?.GetRid();
        if (MeshId == null)
        {
            GD.PrintErr($"{nameof(MeshId)} is null");
            return;
        }

        CachedDrawLod = 0;
        int lod = CachedDrawLod;
        // int minLod = Math.Max(lod + 1, 0);
        // int chunkSize = info.ChunkSize;

        float size = info.ChunkSize * Terrain3D.UNITS_PER_VERTEX;

        InstanceRid = RenderingServer.InstanceCreate();
        RenderingServer.InstanceSetScenario(InstanceRid.Value, scenario); //adding to the scene
        RenderingServer.InstanceSetBase(InstanceRid.Value, MeshId.Value);
        RenderingServer.InstanceAttachObjectInstanceId(InstanceRid.Value, tf.GetInstanceId()); // attach to node
        RenderingServer.MeshSetCustomAabb(MeshId.Value, new Aabb(new Vector3(), new Vector3(size, Height, size)));
        RenderingServer.InstanceSetCustomAabb(InstanceRid.Value, new Aabb(new Vector3(), new Vector3(size, Height, size)));

        MaterialInUse = (Material)mat.Duplicate();
        MaterialId = MaterialInUse.GetRid();

        RenderingServer.InstanceGeometrySetMaterialOverride(InstanceRid.Value, MaterialId.Value);
        var nextChunkSizeLod = (float)(((info.ChunkSize + 1) >> (lod + 1)) - 1);

        if (heightMap == null)
            GD.PrintErr($"{heightMap} is null");
        else
            RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_HEIGHT_MAP, heightMap.GetRid());
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_CHUNK_SIZE, TerrainChunkSizeLod0);
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_NEXT_LOD_CHUNK_SIZE, nextChunkSizeLod);

        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_CURRENT_LOD_LEVEL, lod);

        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_SPLATMAP1, splatMaps[0].GetRid());
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_SPLATMAP2, splatMaps[1].GetRid());

        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_UV_SCALE, GetUvScale());
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_NEIGHBOR_LOD, GetNeighbors());


        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_SMOOTHING, true);

        OffsetUv = new Vector2(patch.PatchCoordinates.X * Terrain3D.PATCH_CHUNK_EDGES + Position.X, patch.PatchCoordinates.Y * Terrain3D.PATCH_CHUNK_EDGES + Position.Y);
        RenderingServer.MaterialSetParam(MaterialId.Value, MaterialParameterNames.TERRAIN_UV_OFFSET, OffsetUv);
        RenderingServer.InstanceSetVisible(InstanceRid.Value, false);

        UpdateSettings(tf);
        tf.ForceUpdateTransform();
    }

    /// <summary>
    /// Updating settings from inspector on physic server
    /// </summary>
    public void UpdateSettings(Terrain3D tf)
    {
        if (!tf.IsInsideTree())
            return;
        if (InstanceRid == null)
        {
            GD.PrintErr($"{nameof(InstanceRid)} is null");
            return;
        }

        RenderingServer.InstanceSetVisible(InstanceRid.Value, tf.IsVisibleInTree());
        RenderingServer.InstanceGeometrySetCastShadowsSetting(InstanceRid.Value, tf.CastShadow);

        switch (tf.GiMode)
        {
            default:
            case GiMode.DISABLED:
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, false);
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGI, false);
                break;
            case GiMode.BAKED:
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, true);
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGI, false);
                break;
            case GiMode.DYNAMIC:
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, false);
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGI, true);
                break;
        }

        RenderingServer.InstanceGeometrySetVisibilityRange(InstanceRid.Value, tf.LodMinDistance, tf.LodMaxDistance, tf.LodMinHysteresis, tf.LodMaxHysteresis, RenderingServer.VisibilityRangeFadeMode.Dependencies);
        RenderingServer.InstanceSetExtraVisibilityMargin(InstanceRid.Value, tf.ExtraCullMargin);
    }

    /// <summary>
    /// Generating a mesh this chunk by given lodLevel
    /// </summary>
    public static ArrayMesh GenerateMesh(TerrainPatch patch, int chunkSize, int lodIndex)
    {
        if (patch.MeshCache.ContainsKey(lodIndex))
        {
            ArrayMesh meshLod = patch.MeshCache[lodIndex];
            return (ArrayMesh)meshLod.Duplicate();
        }

        // int chunkSizeLod0 = chunkSize;

        // Prepare
        int vertexCount = (chunkSize + 1) >> lodIndex;
        chunkSize = vertexCount - 1;
        // int indexCount = chunkSize * chunkSize * 2 * 3;
        // int vertexCount2 = vertexCount * vertexCount;

        float vertexTexelSnapTexCoordinates = 1.0f / chunkSize;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var z = 0; z < vertexCount; z++)
        for (var x = 0; x < vertexCount; x++)
        {
            var buff = new Vector3(x * vertexTexelSnapTexCoordinates, 0f, z * vertexTexelSnapTexCoordinates);

            // Smooth LODs morphing based on Barycentric coordinates to morph to the lower LOD near chunk edges
            var coordinates = new Quaternion(buff.Z, buff.X, 1.0f - buff.X, 1.0f - buff.Z);

            // Apply some contrast
            const float adjustPower = 0.3f;

            var color = new Color
            {
                R = Convert.ToSingle(Math.Pow(coordinates.X, adjustPower)),
                G = Convert.ToSingle(Math.Pow(coordinates.Y, adjustPower)),
                B = Convert.ToSingle(Math.Pow(coordinates.Z, adjustPower)),
                A = Convert.ToSingle(Math.Pow(coordinates.W, adjustPower))
            };


            st.SetColor(color);
            var uv = new Vector2(x * vertexTexelSnapTexCoordinates, z * vertexTexelSnapTexCoordinates);
            st.SetUV(uv);

            st.SetNormal(Vector3.Up);
            st.AddVertex(buff); //x
        }

        for (var z = 0; z < chunkSize; z++)
        for (var x = 0; x < chunkSize; x++)
        {
            int i00 = x + 0 + (z + 0) * vertexCount;
            int i10 = x + 1 + (z + 0) * vertexCount;
            int i11 = x + 1 + (z + 1) * vertexCount;
            int i01 = x + 0 + (z + 1) * vertexCount;

            st.AddIndex(i00);
            st.AddIndex(i10);
            st.AddIndex(i11);

            st.AddIndex(i00);
            st.AddIndex(i11);
            st.AddIndex(i01);
        }

        st.GenerateTangents();

        ArrayMesh? mesh = st.Commit();
        patch.MeshCache.Add(lodIndex, mesh);

        return (ArrayMesh)mesh.Duplicate();
    }
}