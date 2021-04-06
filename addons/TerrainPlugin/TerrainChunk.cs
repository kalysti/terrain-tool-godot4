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

        [Export]
        public float offset = 0;

        [Export]
        public float height = 1;

        public float ChunkSizeNextLOD = 0.0f;
        public float lod = 0.0f;

        protected RID instanceRid;

        protected RID materialId;

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
            RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, offset, size)));

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

        public void Draw(TerrainPatch patch, TerrainPatchInfo info, RID scenario, RID meshId, ref ImageTexture heightMap, Terrain3D tf, Vector3 patchoffset, RID shaderRid)
        {
            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            instanceRid = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetScenario(instanceRid, scenario); //adding to the scene

            RenderingServer.InstanceSetBase(instanceRid, meshId);
            RenderingServer.InstanceAttachObjectInstanceId(instanceRid, tf.GetInstanceId()); // attach to node

            RenderingServer.InstanceSetVisible(instanceRid, true);

            RenderingServer.MeshSetCustomAabb(meshId, new AABB(new Vector3(), new Vector3(size, offset, size)));
            RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, offset, size)));

            materialId = RenderingServer.MaterialCreate();
            RenderingServer.MaterialSetShader(materialId, shaderRid);

            RenderingServer.InstanceGeometrySetMaterialOverride(instanceRid, materialId);
            RenderingServer.MaterialSetParam(materialId, "heigtmap", heightMap);
            RenderingServer.MaterialSetParam(materialId, "TerrainChunkSizeLOD0", TerrainChunkSizeLOD0);
            RenderingServer.MaterialSetParam(materialId, "ChunkSizeNextLOD", 1);

            RenderingServer.MaterialSetParam(materialId, "uv_scale", getUVScale());
            //    RenderingServer.MaterialSetParam(materialId, "useSmotthlod", true);

            var offsetUv = new Vector2((float)(patch.position.x * Terrain3D.CHUNKS_COUNT_EDGE + position.x), (float)(patch.position.y * Terrain3D.CHUNKS_COUNT_EDGE + position.y));
            RenderingServer.MaterialSetParam(materialId, "OffsetUV", offsetUv);
            RenderingServer.MaterialSetParam(materialId, "CurrentLOD", 0);
        }
    }
}
