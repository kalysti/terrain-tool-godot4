using Godot;
using System;

namespace TerrainEditor;

[Tool]
public partial class TerrainChunk : Resource
{
    [Export]
    public Vector2i Position { get; set; }

    [Export]
    public Vector2 OffsetUv { get; set; }

    [Export]
    public float TerrainChunkSizeLod0 { get; set; }

    [Export]
    public float Offset { get; set; }

    [Export]
    public float Height { get; set; } = 1;

    protected RID? InstanceRid;

    protected RID? MaterialId;

    protected Material MaterialInUse;

    protected RID? MeshId;

    protected Mesh Mesh { get; set; }

    protected Godot.Collections.Array<TerrainChunk> Neighbors = new();

    [Export]
    public int CachedDrawLod { get; set; } //not finish

    /**
		*   Get the bounding box of this chunk
		*/
    public AABB GetBounds(TerrainPatchInfo info, Vector3 patchOffset)
    {
        float size = (float)info.ChunkSize * Terrain3D.UNITS_PER_VERTEX;
        Vector3 origin = patchOffset + new Vector3(Position.x * size, Offset, Position.y * size);
        var bounds = new AABB(origin, new Vector3(size, Height, size));

        return bounds;
    }

    /**
		*  Detecting lod levels of neighbors
		*/
    protected Plane GetNeighbors()
    {
        int lod = CachedDrawLod;
        int minLod = Math.Max(lod + 1, 0);

        var pl = new Plane();
        pl.x = (float)Math.Clamp(Neighbors[0].CachedDrawLod, lod, minLod);
        pl.y = (float)Math.Clamp(Neighbors[1].CachedDrawLod, lod, minLod);
        pl.z = (float)Math.Clamp(Neighbors[2].CachedDrawLod, lod, minLod);
        pl.D = (float)Math.Clamp(Neighbors[3].CachedDrawLod, lod, minLod);

        return pl;
    }

    /**
		*  Clear and reset draw (free on physic server)
		*/
    public void ClearDraw()
    {
        if (MeshId.HasValue)
        {
            RenderingServer.FreeRid(MeshId.Value);
        }

        if (InstanceRid.HasValue)
        {
            RenderingServer.FreeRid(InstanceRid.Value);
        }

        Mesh = null;
        MaterialInUse = null;
        MeshId = null;
        MaterialId = null;
        InstanceRid = null;
    }

    /**
		*  Updating position of chunk
		*/
    public Transform3D UpdatePosition(TerrainPatchInfo info, Transform3D terrainTransform, Vector3 patchoffset)
    {
        float size = (info.ChunkSize) * Terrain3D.UNITS_PER_VERTEX;
        Vector3 localPosition = patchoffset + new Vector3(Position.x * size, info.PatchOffset, Position.y * size);
        var localTransform = new Transform3D();
        localTransform.origin = localPosition;
        localTransform.basis = new Basis(Quaternion.Identity);
        localTransform.basis = localTransform.basis.Scaled(new Vector3(1.0f, info.PatchHeight, 1.0f));

        Transform3D global = terrainTransform * localTransform;

        if (InstanceRid.HasValue)
            RenderingServer.InstanceSetTransform(InstanceRid.Value, global);

        if (MaterialId.HasValue)
            RenderingServer.MaterialSetParam(MaterialId.Value, "uv_scale", GetUvScale());

        return global;
    }


    /**
		*  Get the UV Scale for the chunk
		*/
    private Plane GetUvScale()
    {
        Quaternion q = new Quaternion(1.0f, 1.0f, Position.x, Position.y) * (1.0f / Terrain3D.PATCH_CHUNK_EDGES);
        return new Plane(q.x, q.y, q.z, q.w);
    }

    /**
		*  Send inspector material to shader
		*/
    public void UpdateInspectorMaterial(Color color, Plane brushData0, Plane brushData1)
    {
        RenderingServer.MaterialSetParam(MaterialId.Value, "Color", color);
        RenderingServer.MaterialSetParam(MaterialId.Value, "BrushData0", brushData0);
        RenderingServer.MaterialSetParam(MaterialId.Value, "BrushData1", brushData1);
    }

    /**
		*  Send default material to shader
		*/
    public void SetDefaultMaterial(Texture2D image)
    {
        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainDefaultMaterial", image.GetRid());
    }

    /**
		*  Cache neighbours for lod detection
		*/
    public void CacheNeighbors(Terrain3D terrainNode, TerrainPatch currentPatch)
    {
        Neighbors.Clear();
        Neighbors.Add(this);
        Neighbors.Add(this);
        Neighbors.Add(this);
        Neighbors.Add(this);

        // 0: bottom
        if (Position.y > 0)
        {
            Neighbors[0] = currentPatch.Chunks[(Position.y - 1) * Terrain3D.PATCH_CHUNK_EDGES + Position.x];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.x, Position.y - 1);
            if (patch != null)
                Neighbors[0] = patch.Chunks[(Terrain3D.PATCH_CHUNK_EDGES - 1) * Terrain3D.PATCH_CHUNK_EDGES + Position.x];
        }

        // 1: left
        if (Position.x > 0)
        {
            Neighbors[1] = currentPatch.Chunks[Position.y * Terrain3D.PATCH_CHUNK_EDGES + (Position.x - 1)];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.x - 1, Position.y);
            if (patch != null)
                Neighbors[1] = patch.Chunks[Position.y * Terrain3D.PATCH_CHUNK_EDGES + (Terrain3D.PATCH_CHUNK_EDGES - 1)];
        }

        // 2: right
        if (Position.x < Terrain3D.PATCH_CHUNK_EDGES - 1)
        {
            Neighbors[2] = currentPatch.Chunks[Position.y * Terrain3D.PATCH_CHUNK_EDGES + (Position.x + 1)];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.x + 1, Position.y);
            if (patch != null)
                Neighbors[2] = patch.Chunks[Position.y * Terrain3D.PATCH_CHUNK_EDGES];
        }

        // 3: top
        if (Position.y < Terrain3D.PATCH_CHUNK_EDGES - 1)
        {
            Neighbors[3] = currentPatch.Chunks[(Position.y + 1) * Terrain3D.PATCH_CHUNK_EDGES + Position.x];
        }
        else
        {
            TerrainPatch? patch = terrainNode.GetPatch(Position.x, Position.y + 1);
            if (patch != null)
                Neighbors[3] = patch.Chunks[Position.x];
        }
    }

    /**
		 *  Draw the chunk on physic server
		 */
    public void Draw(TerrainPatch patch, TerrainPatchInfo info, RID scenario, ref ImageTexture heightMap, ref Godot.Collections.Array<ImageTexture> splatMaps, Terrain3D tf, Vector3 patchoffset, Material mat)
    {
        CachedDrawLod = 0;
        int lod = CachedDrawLod;
        int minLod = Math.Max(lod + 1, 0);
        int chunkSize = info.ChunkSize;

        Mesh = GenerateMesh(patch, info.ChunkSize, 0);
        MeshId = Mesh.GetRid();

        float size = (float)info.ChunkSize * Terrain3D.UNITS_PER_VERTEX;

        InstanceRid = RenderingServer.InstanceCreate();
        RenderingServer.InstanceSetScenario(InstanceRid.Value, scenario); //adding to the scene
        RenderingServer.InstanceSetBase(InstanceRid.Value, MeshId.Value);
        RenderingServer.InstanceAttachObjectInstanceId(InstanceRid.Value, tf.GetInstanceId()); // attach to node
        RenderingServer.MeshSetCustomAabb(MeshId.Value, new AABB(new Vector3(), new Vector3(size, Height, size)));
        RenderingServer.InstanceSetCustomAabb(InstanceRid.Value, new AABB(new Vector3(), new Vector3(size, Height, size)));

        MaterialInUse = mat.Duplicate() as Material;
        MaterialId = MaterialInUse.GetRid();

        RenderingServer.InstanceGeometrySetMaterialOverride(InstanceRid.Value, MaterialId.Value);
        var nextChunkSizeLod = (float)(((info.ChunkSize + 1) >> (lod + 1)) - 1);

        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainHeightMap", heightMap.GetRid());
        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainChunkSize", TerrainChunkSizeLod0);
        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainNextLodChunkSize", nextChunkSizeLod);

        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainCurrentLodLevel", lod);

        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainSplatmap1", splatMaps[0].GetRid());
        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainSplatmap2", splatMaps[1].GetRid());

        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainUvScale", GetUvScale());
        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainNeighborLod", GetNeighbors());


        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainSmoothing", true);

        OffsetUv = new Vector2((float)(patch.PatchCoord.x * Terrain3D.PATCH_CHUNK_EDGES + Position.x), (float)(patch.PatchCoord.y * Terrain3D.PATCH_CHUNK_EDGES + Position.y));
        RenderingServer.MaterialSetParam(MaterialId.Value, "terrainUvOffset", OffsetUv);
        RenderingServer.InstanceSetVisible(InstanceRid.Value, false);

        UpdateSettings(tf);
        tf.ForceUpdateTransform();
    }

    /**
		 * Updating settings from inspector on physic server
		 */
    public void UpdateSettings(Terrain3D tf)
    {
        if (!tf.IsInsideTree())
            return;

        RenderingServer.InstanceSetVisible(InstanceRid.Value, tf.IsVisibleInTree());
        RenderingServer.InstanceGeometrySetCastShadowsSetting(InstanceRid.Value, tf.CastShadow);

        switch (tf.GiMode)
        {
            case GiMode.DISABLED:
            {
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, false);
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGi, false);
            }
                break;
            case GiMode.BAKED:
            {
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, true);
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGi, false);
            }
                break;
            case GiMode.DYNAMIC:
            {
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, false);
                RenderingServer.InstanceGeometrySetFlag(InstanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGi, true);
            }
                break;
        }

        RenderingServer.InstanceGeometrySetVisibilityRange(InstanceRid.Value, tf.LodMinDistance, tf.LodMaxDistance, tf.LodMinHysteresis, tf.LodMaxHysteresis, RenderingServer.VisibilityRangeFadeMode.Dependencies);
        RenderingServer.InstanceSetExtraVisibilityMargin(InstanceRid.Value, tf.ExtraCullMargin);
    }

    /**
		 * Generating a mesh this chunk by given lodLevel
		 */
    public ArrayMesh GenerateMesh(TerrainPatch patch, int chunkSize, int lodIndex)
    {
        if (patch.MeshCache.ContainsKey(lodIndex))
        {
            return (ArrayMesh)patch.MeshCache[lodIndex].Duplicate();
        }

        int chunkSizeLod0 = chunkSize;

        // Prepare
        int vertexCount = (chunkSize + 1) >> lodIndex;
        chunkSize = vertexCount - 1;
        int indexCount = chunkSize * chunkSize * 2 * 3;
        int vertexCount2 = vertexCount * vertexCount;

        float vertexTexelSnapTexCoord = 1.0f / chunkSize;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var z = 0; z < vertexCount; z++)
        {
            for (var x = 0; x < vertexCount; x++)
            {
                var buff = new Vector3(x * vertexTexelSnapTexCoord, 0f, z * vertexTexelSnapTexCoord);

                // Smooth LODs morphing based on Barycentric coordinates to morph to the lower LOD near chunk edges
                var coord = new Quaternion(buff.z, buff.x, 1.0f - buff.x, 1.0f - buff.z);

                // Apply some contrast
                const float adjustPower = 0.3f;

                var color = new Color
                {
                    r = Convert.ToSingle(Math.Pow(coord.x, adjustPower)),
                    g = Convert.ToSingle(Math.Pow(coord.y, adjustPower)),
                    b = Convert.ToSingle(Math.Pow(coord.z, adjustPower)),
                    a = Convert.ToSingle(Math.Pow(coord.w, adjustPower))
                };


                st.SetColor(color);
                var uv = new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord);
                st.SetUv(uv);

                st.SetNormal(Vector3.Up);
                st.AddVertex(buff); //x
            }
        }

        for (var z = 0; z < chunkSize; z++)
        {
            for (var x = 0; x < chunkSize; x++)
            {
                int i00 = (x + 0) + (z + 0) * vertexCount;
                int i10 = (x + 1) + (z + 0) * vertexCount;
                int i11 = (x + 1) + (z + 1) * vertexCount;
                int i01 = (x + 0) + (z + 1) * vertexCount;

                st.AddIndex(i00);
                st.AddIndex(i10);
                st.AddIndex(i11);

                st.AddIndex(i00);
                st.AddIndex(i11);
                st.AddIndex(i01);
            }
        }

        st.GenerateTangents();

        ArrayMesh? mesh = st.Commit();
        patch.MeshCache.Add(lodIndex, mesh);

        return (ArrayMesh)mesh.Duplicate();
    }
}