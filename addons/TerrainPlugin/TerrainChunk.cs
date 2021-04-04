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
        public Vector2 position = new Vector2();

        public AABB bounds = new AABB();
        [Export]
        public Plane heightmapUVScale = new Plane();

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

        protected RID materialId2;

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

            if (materialId != null)
            {
                RenderingServer.FreeRid(materialId);
                materialId = null;
            }
        }

        public Transform UpdateTransform(TerrainPatchInfo info, Transform terrainTransform, Vector3 patchoffset)
        {
            float size = info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            var localPosition = patchoffset + new Vector3(position.x * size, info.patchOffset, position.y * size);

            Transform localTransform = new Transform();
            localTransform.origin = localPosition;
            localTransform.basis = new Basis(Quat.Identity);
            localTransform.basis = localTransform.basis.Scaled(new Vector3(1.0f, info.patchHeight, 1.0f));

            var global = terrainTransform * localTransform;

            if (instanceRid != null)
                RenderingServer.InstanceSetTransform(instanceRid, global);

            return global;
        }

        public void UpdateHeightmap(ImageTexture heightMap)
        {
            RenderingServer.MaterialSetParam(materialId, "heigtmap", heightMap);
        }

        public void Draw(TerrainPatch patch, TerrainPatchInfo info, RID scenario, RID meshId, ImageTexture heightMap, Terrain3D tf, Vector3 patchoffset, RID shaderRid)
        {
            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            RenderingServer.MeshSetCustomAabb(meshId, new AABB(new Vector3(), new Vector3(size, info.patchHeight, size)));

            instanceRid = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetScenario(instanceRid, scenario); //adding to the scene

            RenderingServer.InstanceSetBase(instanceRid, meshId);
            RenderingServer.InstanceAttachObjectInstanceId(instanceRid, tf.GetInstanceId()); // attach to node

            RenderingServer.InstanceSetVisible(instanceRid, true);


            RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, info.patchHeight, size)));
            materialId = RenderingServer.MaterialCreate();
            RenderingServer.MaterialSetShader(materialId, shaderRid);

            // RenderingServer.MaterialSetNextPass(materialId, nextPassId);

            RenderingServer.InstanceGeometrySetMaterialOverride(instanceRid, materialId);
            RenderingServer.MaterialSetParam(materialId, "heigtmap", heightMap);
            RenderingServer.MaterialSetParam(materialId, "TerrainChunkSizeLOD0", TerrainChunkSizeLOD0);
            RenderingServer.MaterialSetParam(materialId, "uv_scale", heightmapUVScale);

            var offsetUv = new Vector2((float)(patch.position.x * Terrain3D.CHUNKS_COUNT_EDGE + position.x), (float)(patch.position.y * Terrain3D.CHUNKS_COUNT_EDGE + position.y));
            RenderingServer.MaterialSetParam(materialId, "OffsetUV", offsetUv);
            RenderingServer.MaterialSetParam(materialId, "CurrentLOD", 0);
        }
    }
}
