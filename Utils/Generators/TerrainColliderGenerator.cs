using Godot;
using System;
using TerrainEditor.Converters;
using System.Linq;

namespace TerrainEditor.Generators
{

    public class TerrainColliderGenerator : TerrainBaseGenerator
    {
        /**
         * Constructor
         */
        public TerrainColliderGenerator(TerrainPatch _patch) : base(_patch)
        {
        }


        /**
         * Generate collider datas by given lod level
         */
        public float[] GenereateLOD(int collisionLod)
        {
            GD.Print("Create height collider");
            int collisionLOD = Mathf.Clamp(collisionLod, 0, 2);

            // Prepare datas
            int heightFieldChunkSize = ((patch.info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            int vertexCountEdgeMip = patch.info.vertexCountEdge >> collisionLOD;
            int textureSizeMip = patch.info.textureSize >> collisionLOD;

            var img = patch.heightmap.GetImage();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(img.GetData());

            int heightMapLength = patch.info.heightMapSize * patch.info.heightMapSize;
            var heightField = new float[heightMapLength];

            GD.Print("size:" + patch.info.heightMapSize);

            for (int chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
            {
                int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                int chunkStartZ = chunkZ * heightFieldChunkSize;

                for (int chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
                {
                    int chunkTextureX = chunkX * vertexCountEdgeMip;
                    int chunkStartX = chunkX * heightFieldChunkSize;

                    for (int z = 0; z < vertexCountEdgeMip; z++)
                    {
                        for (int x = 0; x < vertexCountEdgeMip; x++)
                        {
                            int textureIndexZ = (chunkTextureZ + z) * textureSizeMip;
                            int textureIndexX = chunkTextureX + x;
                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                            float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                            float height = (normalizedHeight * patch.info.patchHeight) + patch.info.patchOffset;
                            bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRGBABuffer[textureIndex]);

                            int heightmapX = chunkStartX + x;
                            int heightmapZ = chunkStartZ + z;

                            int dstIndex = heightmapX + (heightmapZ * heightFieldSize);
                            heightField[dstIndex] =  height;
                        }
                    }
                }
            }

            return heightField;
        }

        /**
         * Modify collider datas (todo)
         */
        public float[] ModifyCollision(byte[] buffer, int collisionLod, Vector2i modifiedOffset, Vector2i modifiedSize, float[] heightFieldData) //modifing
        {
            GD.Print("modify height collider");
            var newHeightfieldData = (float[]) heightFieldData.Clone() ;
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            // Prepare data
            Vector2 modifiedOffsetRatio = new Vector2((float)modifiedOffset.x / patch.info.heightMapSize, (float)modifiedOffset.y / patch.info.heightMapSize);
            Vector2 modifiedSizeRatio = new Vector2((float)modifiedSize.x / patch.info.heightMapSize, (float)modifiedSize.y / patch.info.heightMapSize);

            int collisionLOD = Mathf.Clamp(collisionLod, 0, 0); //from mip
            int heightFieldChunkSize = ((patch.info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;

            Vector2i samplesOffset = new Vector2i(Mathf.FloorToInt(modifiedOffsetRatio.x * (float)heightFieldSize), Mathf.FloorToInt(modifiedOffsetRatio.y * (float)heightFieldSize));
            Vector2i samplesSize = new Vector2i(Mathf.CeilToInt(modifiedSizeRatio.x * (float)heightFieldSize), Mathf.CeilToInt(modifiedSizeRatio.y * (float)heightFieldSize));

            samplesSize.x = Math.Max(samplesSize.x, 1);
            samplesSize.y = Math.Max(samplesSize.y, 1);

            Vector2i samplesEnd = samplesOffset + samplesSize;
            samplesEnd.x = Math.Min(samplesEnd.x, heightFieldSize);
            samplesEnd.y = Math.Min(samplesEnd.y, heightFieldSize);

            // Setup terrain collision information
            int vertexCountEdgeMip = patch.info.vertexCountEdge >> collisionLOD;
            int textureSizeMip = patch.info.textureSize >> collisionLOD;

            for (int chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
            {
                int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                int chunkStartZ = chunkZ * heightFieldChunkSize;

                // Skip unmodified chunks
                if (chunkStartZ >= samplesEnd.y || chunkStartZ + vertexCountEdgeMip < samplesOffset.y)
                    continue;

                for (int chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
                {
                    int chunkTextureX = chunkX * vertexCountEdgeMip;
                    int chunkStartX = chunkX * heightFieldChunkSize;

                    // Skip unmodified chunks
                    if (chunkStartX >= samplesEnd.x || chunkStartX + vertexCountEdgeMip < samplesOffset.x)
                        continue;

                    for (int z = 0; z < vertexCountEdgeMip; z++)
                    {
                        // Skip unmodified columns
                        int heightmapZ = chunkStartZ + z;
                        int heightmapLocalZ = heightmapZ - samplesOffset.y;
                        if (heightmapLocalZ < 0 || heightmapLocalZ >= samplesSize.y)
                            continue;

                        // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                        for (int x = 0; x < vertexCountEdgeMip; x++)
                        {
                            // Skip unmodified rows
                            int heightmapX = chunkStartX + x;
                            int heightmapLocalX = heightmapX - samplesOffset.x;
                            if (heightmapLocalX < 0 || heightmapLocalX >= samplesSize.x)
                                continue;

                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                            float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                            float height = (normalizedHeight * patch.info.patchHeight) + patch.info.patchOffset;

                            bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRGBABuffer[textureIndex]);
                            int dstIndex = (heightmapLocalX * samplesSize.y) + heightmapLocalZ;
                           
                            if(isHole)
                                height = 0f;

                           //newHeightfieldData[dstIndex] = height;
                        }
                    }
                }
            }

            return newHeightfieldData;
        }
    }
}
