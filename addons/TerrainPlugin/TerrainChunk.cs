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
        public Vector2i position = new Vector2i();

        public AABB bounds = new AABB();


        [Export]
        public float TerrainChunkSizeLOD0 = 0.0f;
        protected Mesh mesh { get; set; }

        [Export]
        public float offset = 0;

        [Export]
        public float height = 1;

        public float ChunkSizeNextLOD = 0.0f;
        public float lod = 0.0f;

        protected RID instanceRid;

        protected RID materialId;

        protected RID meshId;

        public AABB getBounds(TerrainPatchInfo info, Vector3 patchOffset)
        {
            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            var origin = patchOffset + new Vector3(position.x * size, offset, position.y * size);
            bounds = new AABB(origin, new Vector3(size, height, size));

            return bounds;
        }

        public void Clear()
        {
            if (instanceRid != null)
            {
                RenderingServer.FreeRid(instanceRid);
                instanceRid = null;
            }

            if (materialId != null)
            {
                RenderingServer.FreeRid(materialId);
                materialId = null;
            }

            if (meshId != null)
            {
                RenderingServer.FreeRid(meshId);
                meshId = null;
            }

            mesh = null;

        }

        public Transform UpdateTransform(TerrainPatchInfo info, Transform terrainTransform, Vector3 patchoffset)
        {
            float size = (info.chunkSize) * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            var localPosition = patchoffset + new Vector3(position.x * size, info.patchOffset, position.y * size);
            Transform localTransform = new Transform();
            localTransform.origin = localPosition;
            localTransform.basis = new Basis(Quat.Identity);
            localTransform.basis = localTransform.basis.Scaled(new Vector3(1.0f, info.patchHeight, 1.0f));

            var global = terrainTransform * localTransform;

            if (instanceRid != null)
                RenderingServer.InstanceSetTransform(instanceRid, global);
            if (materialId != null)
                RenderingServer.MaterialSetParam(materialId, "uv_scale", getUVScale());

            return global;
        }

        public void UpdateHeightmap(TerrainPatchInfo info)
        {
            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            //  if (instanceRid != null)
            //   RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, offset, size)));

            if (materialId != null)
                RenderingServer.MaterialSetParam(materialId, "uv_scale", getUVScale());
        }

        private Plane getUVScale()
        {
            var q = new Quat(1.0f, 1.0f, position.x, position.y) * (1.0f / Terrain3D.CHUNKS_COUNT_EDGE);
            return new Plane(q.x, q.y, q.z, q.w);
        }

        public void UpdateShaderTexture(ref ImageTexture heightMap)
        {
            RenderingServer.MaterialSetParam(materialId, "heigtmap", heightMap);
        }

        public void UpdateInspectorMaterial(Color color, Plane BrushData0, Plane BrushData1)
        {
            RenderingServer.MaterialSetParam(materialId, "Color", color);
            RenderingServer.MaterialSetParam(materialId, "BrushData0", BrushData0);
            RenderingServer.MaterialSetParam(materialId, "BrushData1", BrushData1);
        }

        public void SetDefaultMaterial(Texture2D image)
        {
            RenderingServer.MaterialSetParam(materialId, "terrainDefaultMaterial", image);
        }

        public void Draw(TerrainPatch patch, TerrainPatchInfo info, RID scenario, ref ImageTexture heightMap, Terrain3D tf, Vector3 patchoffset, RID shaderRid)
        {
            mesh = GenerateMesh(info.chunkSize, 0);
            meshId = mesh.GetRid();

            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            instanceRid = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetScenario(instanceRid, scenario); //adding to the scene

            RenderingServer.InstanceSetBase(instanceRid, meshId);
            RenderingServer.InstanceAttachObjectInstanceId(instanceRid, tf.GetInstanceId()); // attach to node

            RenderingServer.InstanceSetVisible(instanceRid, true);


            // RenderingServer.MeshSetCustomAabb(meshId, new AABB(new Vector3(), new Vector3(size, offset, size)));

            RenderingServer.MeshSetCustomAabb(meshId, new AABB(new Vector3(), new Vector3(size, height, size)));
            RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, height, size)));

            materialId = RenderingServer.MaterialCreate();

            RenderingServer.MaterialSetShader(materialId, shaderRid);

            RenderingServer.InstanceGeometrySetMaterialOverride(instanceRid, materialId);

            RenderingServer.MaterialSetParam(materialId, "terrainHeightMap", heightMap);
            RenderingServer.MaterialSetParam(materialId, "terrainChunkSize", TerrainChunkSizeLOD0);
            RenderingServer.MaterialSetParam(materialId, "terrainNextLodChunkSize", 1);
            RenderingServer.MaterialSetParam(materialId, "terrainUvScale", getUVScale());
            RenderingServer.MaterialSetParam(materialId, "terrainCurrentLodLevel", 0);

            var offsetUv = new Vector2((float)(patch.position.x * Terrain3D.CHUNKS_COUNT_EDGE + position.x), (float)(patch.position.y * Terrain3D.CHUNKS_COUNT_EDGE + position.y));
            RenderingServer.MaterialSetParam(materialId, "terrainUvOffset", offsetUv);

        }

        private ArrayMesh GenerateMesh(int chunkSize, int lodIndex)
        {
            int chunkSizeLOD0 = chunkSize;

            // Prepare
            int vertexCount = (chunkSize + 1) >> lodIndex;
            chunkSize = vertexCount - 1;
            int indexCount = chunkSize * chunkSize * 2 * 3;
            int vertexCount2 = vertexCount * vertexCount;

            // Create vertex buffer
            float vertexTexelSnapTexCoord = 1.0f / chunkSize;


            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            for (int z = 0; z < vertexCount; z++)
            {
                for (int x = 0; x < vertexCount; x++)
                {
                    var buff = new Vector3(x * vertexTexelSnapTexCoord, 0f, z * vertexTexelSnapTexCoord);

                    //  uv_buffer.Add(new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord));
                    // Smooth LODs morphing based on Barycentric coordinates to morph to the lower LOD near chunk edges
                    var coord = new Quat(buff.z, buff.x, 1.0f - buff.x, 1.0f - buff.z);

                    // Apply some contrast
                    const float AdjustPower = 0.3f;

                    var color = new Color();
                    color.r = Convert.ToSingle(Math.Pow(coord.x, AdjustPower));
                    color.g = Convert.ToSingle(Math.Pow(coord.y, AdjustPower));
                    color.b = Convert.ToSingle(Math.Pow(coord.z, AdjustPower));
                    color.a = Convert.ToSingle(Math.Pow(coord.w, AdjustPower));

                    st.SetColor(color);
                    st.SetUv(new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord));
                    st.AddVertex(buff); //x

                    ///     color_buffer.Add(color);
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

            st.GenerateNormals();
            st.GenerateTangents();

            return st.Commit();
        }
    }
}
