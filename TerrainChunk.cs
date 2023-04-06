using System.Data.SqlTypes;
using System.Transactions;
using System.Runtime.InteropServices;
using Godot;
using System;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainChunk : Resource
    {
        [Export]
        public Vector2I position = new Vector2I();

        [Export]
        public Vector2 offsetUv = new Vector2();

        [Export]
        public float TerrainChunkSizeLOD0 = 0.0f;

        [Export]
        public float offset = 0;

        [Export]
        public float height = 1;

        protected Rid? instanceRid;

        protected Rid? materialId;

        protected Material materialInUse;

        protected Rid? meshId;

        protected Mesh mesh { get; set; }

        protected Godot.Collections.Array<TerrainChunk> _neighbors = new Godot.Collections.Array<TerrainChunk>();

        [Export]
        public int _cachedDrawLOD; //not finish

        /**
		*   Get the bounding box of this chunk
		*/
        public Aabb getBounds(TerrainPatchInfo info, Vector3 patchOffset)
        {
            float size = (float)info.chunkSize * Terrain3D.UNITS_PER_VERTEX;
            Vector3 Origin = patchOffset + new Vector3(position.X * size, offset, position.Y * size);
            var bounds = new Aabb(Origin, new Vector3(size, height, size));

            return bounds;
        }

        /**
		*  Detecting lod levels of neighbors
		*/
        protected Plane getNeighbors()
        {
            int lod = _cachedDrawLOD;
            int minLod = Math.Max(lod + 1, 0);

            Plane pl = new Plane();
            pl.X = (float)Math.Clamp(_neighbors[0]._cachedDrawLOD, lod, minLod);
            pl.Y = (float)Math.Clamp(_neighbors[1]._cachedDrawLOD, lod, minLod);
            pl.Z = (float)Math.Clamp(_neighbors[2]._cachedDrawLOD, lod, minLod);
            pl.D = (float)Math.Clamp(_neighbors[3]._cachedDrawLOD, lod, minLod);

            return pl;
        }

        /**
		*  Clear and reset draw (free on physic server)
		*/
        public void ClearDraw()
        {
            if (meshId.HasValue)
            {
                RenderingServer.FreeRid(meshId.Value);
            }

            if (instanceRid.HasValue)
            {
                RenderingServer.FreeRid(instanceRid.Value);
            }
            mesh = null;
            materialInUse = null;
            meshId = null;
            materialId = null;
            instanceRid = null;
        }

        /**
		*  Updating position of chunk
		*/
        public Transform3D UpdatePosition(TerrainPatchInfo info, Transform3D terrainTransform, Vector3 patchoffset)
        {
            float size = (info.chunkSize) * Terrain3D.UNITS_PER_VERTEX;
            Vector3 localPosition = patchoffset + new Vector3(position.X * size, info.patchOffset, position.Y * size);
            Transform3D localTransform = new Transform3D();
            localTransform.Origin = localPosition;
            localTransform.Basis = new Basis(Quaternion.Identity);
            localTransform.Basis = localTransform.Basis.Scaled(new Vector3(1.0f, info.patchHeight, 1.0f));

            Transform3D global = terrainTransform * localTransform;

            if (instanceRid.HasValue)
                RenderingServer.InstanceSetTransform(instanceRid.Value, global);

            if (materialId.HasValue)
                RenderingServer.MaterialSetParam(materialId.Value, "uv_scale", getUVScale());

            return global;
        }


        /**
		*  Get the UV Scale for the chunk
		*/
        private Plane getUVScale()
        {
            Quaternion q = new Quaternion(1.0f, 1.0f, position.X, position.Y) * (1.0f / Terrain3D.PATCH_CHUNK_EDGES);
            return new Plane(q.X, q.Y, q.Z, q.W);
        }

        /**
		*  Send inspector material to shader
		*/
        public void UpdateInspectorMaterial(Color color, Plane BrushData0, Plane BrushData1)
        {
            RenderingServer.MaterialSetParam(materialId.Value, "Color", color);
            RenderingServer.MaterialSetParam(materialId.Value, "BrushData0", BrushData0);
            RenderingServer.MaterialSetParam(materialId.Value, "BrushData1", BrushData1);
        }

        /**
		*  Send default material to shader
		*/
        public void SetDefaultMaterial(Texture2D image)
        {
            RenderingServer.MaterialSetParam(materialId.Value, "terrainDefaultMaterial", image.GetRid());
        }

        /**
		*  Cache neighbours for lod detection
		*/
        public void CacheNeighbors(Terrain3D terrainNode, TerrainPatch currentPatch)
        {
            _neighbors.Clear();
            _neighbors.Add(this);
            _neighbors.Add(this);
            _neighbors.Add(this);
            _neighbors.Add(this);

            // 0: bottom
            if (position.Y > 0)
            {
                _neighbors[0] = currentPatch.chunks[(position.Y - 1) * Terrain3D.PATCH_CHUNK_EDGES + position.X];
            }
            else
            {
                TerrainPatch? patch = terrainNode.GetPatch(position.X, position.Y - 1);
                if (patch != null)
                    _neighbors[0] = patch.chunks[(Terrain3D.PATCH_CHUNK_EDGES - 1) * Terrain3D.PATCH_CHUNK_EDGES + position.X];
            }

            // 1: left
            if (position.X > 0)
            {
                _neighbors[1] = currentPatch.chunks[position.Y * Terrain3D.PATCH_CHUNK_EDGES + (position.X - 1)];
            }
            else
            {
                TerrainPatch? patch = terrainNode.GetPatch(position.X - 1, position.Y);
                if (patch != null)
                    _neighbors[1] = patch.chunks[position.Y * Terrain3D.PATCH_CHUNK_EDGES + (Terrain3D.PATCH_CHUNK_EDGES - 1)];
            }

            // 2: right
            if (position.X < Terrain3D.PATCH_CHUNK_EDGES - 1)
            {
                _neighbors[2] = currentPatch.chunks[position.Y * Terrain3D.PATCH_CHUNK_EDGES + (position.X + 1)];
            }
            else
            {
                TerrainPatch? patch = terrainNode.GetPatch(position.X + 1, position.Y);
                if (patch != null)
                    _neighbors[2] = patch.chunks[position.Y * Terrain3D.PATCH_CHUNK_EDGES];
            }

            // 3: top
            if (position.Y < Terrain3D.PATCH_CHUNK_EDGES - 1)
            {
                _neighbors[3] = currentPatch.chunks[(position.Y + 1) * Terrain3D.PATCH_CHUNK_EDGES + position.X];
            }
            else
            {
                TerrainPatch? patch = terrainNode.GetPatch(position.X, position.Y + 1);
                if (patch != null)
                    _neighbors[3] = patch.chunks[position.X];
            }
        }

        /**
		 *  Draw the chunk on physic server
		 */
        public void Draw(TerrainPatch patch, TerrainPatchInfo info, Rid scenario, ref ImageTexture heightMap, ref Godot.Collections.Array<ImageTexture> splatMaps, Terrain3D tf, Vector3 patchoffset, Material mat)
        {
            _cachedDrawLOD = 0;
            int lod = _cachedDrawLOD;
            int minLod = Math.Max(lod + 1, 0);
            int chunkSize = info.chunkSize;

            mesh = GenerateMesh(patch, info.chunkSize, 0);
            meshId = mesh.GetRid();

            float size = (float)info.chunkSize * Terrain3D.UNITS_PER_VERTEX;

            instanceRid = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetScenario(instanceRid.Value, scenario); //adding to the scene
            RenderingServer.InstanceSetBase(instanceRid.Value, meshId.Value);
            RenderingServer.InstanceAttachObjectInstanceId(instanceRid.Value, tf.GetInstanceId()); // attach to node
            RenderingServer.MeshSetCustomAabb(meshId.Value, new Aabb(new Vector3(), new Vector3(size, height, size)));
            RenderingServer.InstanceSetCustomAabb(instanceRid.Value, new Aabb(new Vector3(), new Vector3(size, height, size)));

            materialInUse = mat.Duplicate() as Material;
            materialId = materialInUse.GetRid();

            RenderingServer.InstanceGeometrySetMaterialOverride(instanceRid.Value, materialId.Value);
            var nextChunkSizeLod = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);

            RenderingServer.MaterialSetParam(materialId.Value, "terrainHeightMap", heightMap.GetRid());
            RenderingServer.MaterialSetParam(materialId.Value, "terrainChunkSize", TerrainChunkSizeLOD0);
            RenderingServer.MaterialSetParam(materialId.Value, "terrainNextLodChunkSize", nextChunkSizeLod);

            RenderingServer.MaterialSetParam(materialId.Value, "terrainCurrentLodLevel", lod);

            RenderingServer.MaterialSetParam(materialId.Value, "terrainSplatmap1", splatMaps[0].GetRid());
            RenderingServer.MaterialSetParam(materialId.Value, "terrainSplatmap2", splatMaps[1].GetRid());

            RenderingServer.MaterialSetParam(materialId.Value, "terrainUvScale", getUVScale());
            RenderingServer.MaterialSetParam(materialId.Value, "terrainNeighborLod", getNeighbors());


            RenderingServer.MaterialSetParam(materialId.Value, "terrainSmoothing", true);

            offsetUv = new Vector2((float)(patch.patchCoord.X * Terrain3D.PATCH_CHUNK_EDGES + position.X), (float)(patch.patchCoord.Y * Terrain3D.PATCH_CHUNK_EDGES + position.Y));
            RenderingServer.MaterialSetParam(materialId.Value, "terrainUvOffset", offsetUv);
            RenderingServer.InstanceSetVisible(instanceRid.Value, false);

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

            RenderingServer.InstanceSetVisible(instanceRid.Value, tf.IsVisibleInTree());
            RenderingServer.InstanceGeometrySetCastShadowsSetting(instanceRid.Value, tf.castShadow);

            switch (tf.giMode)
            {
                case GIMode.Disabled:
                    {
                        RenderingServer.InstanceGeometrySetFlag(instanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, false);
                        RenderingServer.InstanceGeometrySetFlag(instanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGI, false);
                    }
                    break;
                case GIMode.Baked:
                    {

                        RenderingServer.InstanceGeometrySetFlag(instanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, true);
                        RenderingServer.InstanceGeometrySetFlag(instanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGI, false);
                    }
                    break;
                case GIMode.Dynamic:
                    {
                        RenderingServer.InstanceGeometrySetFlag(instanceRid.Value, RenderingServer.InstanceFlags.UseBakedLight, false);
                        RenderingServer.InstanceGeometrySetFlag(instanceRid.Value, RenderingServer.InstanceFlags.UseDynamicGI, true);
                    }
                    break;
            }

            RenderingServer.InstanceGeometrySetVisibilityRange(instanceRid.Value, tf.lodMinDistance, tf.lodMaxDistance, tf.lodMinHysteresis, tf.lodMaxHysteresis, RenderingServer.VisibilityRangeFadeMode.Dependencies);
            RenderingServer.InstanceSetExtraVisibilityMargin(instanceRid.Value, tf.extraCullMargin);
        }

        /**
		 * Generating a mesh this chunk by given lodLevel
		 */
        public ArrayMesh GenerateMesh(TerrainPatch patch, int chunkSize, int lodIndex)
        {
            if (patch.meshCache.ContainsKey(lodIndex))
            {
                return patch.meshCache[lodIndex].Duplicate() as ArrayMesh;
            }

            int chunkSizeLOD0 = chunkSize;

            // Prepare
            int vertexCount = (chunkSize + 1) >> lodIndex;
            chunkSize = vertexCount - 1;
            int indexCount = chunkSize * chunkSize * 2 * 3;
            int vertexCount2 = vertexCount * vertexCount;

            float vertexTexelSnapTexCoord = 1.0f / chunkSize;

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            for (int z = 0; z < vertexCount; z++)
            {
                for (int x = 0; x < vertexCount; x++)
                {
                    var buff = new Vector3(x * vertexTexelSnapTexCoord, 0f, z * vertexTexelSnapTexCoord);

                    // Smooth LODs morphing based on Barycentric coordinates to morph to the lower LOD near chunk edges
                    var coord = new Quaternion(buff.Z, buff.X, 1.0f - buff.X, 1.0f - buff.Z);

                    // Apply some contrast
                    const float AdjustPower = 0.3f;

                    var color = new Color();
                    color.R = Convert.ToSingle(Math.Pow(coord.X, AdjustPower));
                    color.G = Convert.ToSingle(Math.Pow(coord.Y, AdjustPower));
                    color.B = Convert.ToSingle(Math.Pow(coord.Z, AdjustPower));
                    color.A = Convert.ToSingle(Math.Pow(coord.W, AdjustPower));


                    st.SetColor(color);
                    var uv = new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord);
                    st.SetUV(uv);

                    st.SetNormal(Vector3.Up);
                    st.AddVertex(buff); //x
                }
            }

            for (int z = 0; z < chunkSize; z++)
            {
                for (int x = 0; x < chunkSize; x++)
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
            patch.meshCache.Add(lodIndex, mesh);

            return mesh.Duplicate() as ArrayMesh;
        }
    }
}
