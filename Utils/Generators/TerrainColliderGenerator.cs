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

            Image? img = patch.heightmap.GetImage();
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
        public float[] ModifyCollision(byte[] buffer, int collisionLod, Vector2I modifiedOffset, Vector2I modifiedSize, float[] heightFieldData) //modifing
        {
            GD.Print("modify height collider");
            var newHeightfieldData = (float[]) heightFieldData.Clone() ;
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            // Prepare data
            Vector2 modifiedOffsetRatio = new Vector2((float)modifiedOffset.X / patch.info.heightMapSize, (float)modifiedOffset.Y / patch.info.heightMapSize);
            Vector2 modifiedSizeRatio = new Vector2((float)modifiedSize.X / patch.info.heightMapSize, (float)modifiedSize.Y / patch.info.heightMapSize);

            int collisionLOD = Mathf.Clamp(collisionLod, 0, 0); //from mip
            int heightFieldChunkSize = ((patch.info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;

            Vector2I samplesOffset = new Vector2I(Mathf.FloorToInt(modifiedOffsetRatio.X * (float)heightFieldSize), Mathf.FloorToInt(modifiedOffsetRatio.Y * (float)heightFieldSize));
            Vector2I samplesSize = new Vector2I(Mathf.CeilToInt(modifiedSizeRatio.X * (float)heightFieldSize), Mathf.CeilToInt(modifiedSizeRatio.Y * (float)heightFieldSize));

            samplesSize.X = Math.Max(samplesSize.X, 1);
            samplesSize.Y = Math.Max(samplesSize.Y, 1);

            Vector2I samplesEnd = samplesOffset + samplesSize;
            samplesEnd.X = Math.Min(samplesEnd.X, heightFieldSize);
            samplesEnd.Y = Math.Min(samplesEnd.Y, heightFieldSize);

            // Setup terrain collision information
            int vertexCountEdgeMip = patch.info.vertexCountEdge >> collisionLOD;
            int textureSizeMip = patch.info.textureSize >> collisionLOD;

            for (int chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
            {
                int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                int chunkStartZ = chunkZ * heightFieldChunkSize;

                // Skip unmodified chunks
                if (chunkStartZ >= samplesEnd.Y || chunkStartZ + vertexCountEdgeMip < samplesOffset.Y)
                    continue;

                for (int chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
                {
                    int chunkTextureX = chunkX * vertexCountEdgeMip;
                    int chunkStartX = chunkX * heightFieldChunkSize;

                    // Skip unmodified chunks
                    if (chunkStartX >= samplesEnd.X || chunkStartX + vertexCountEdgeMip < samplesOffset.X)
                        continue;

                    for (int z = 0; z < vertexCountEdgeMip; z++)
                    {
                        // Skip unmodified columns
                        int heightmapZ = chunkStartZ + z;
                        int heightmapLocalZ = heightmapZ - samplesOffset.Y;
                        if (heightmapLocalZ < 0 || heightmapLocalZ >= samplesSize.Y)
                            continue;

                        // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                        for (int x = 0; x < vertexCountEdgeMip; x++)
                        {
                            // Skip unmodified rows
                            int heightmapX = chunkStartX + x;
                            int heightmapLocalX = heightmapX - samplesOffset.X;
                            if (heightmapLocalX < 0 || heightmapLocalX >= samplesSize.X)
                                continue;

                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                            float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                            float height = (normalizedHeight * patch.info.patchHeight) + patch.info.patchOffset;

                            bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRGBABuffer[textureIndex]);
                            int dstIndex = (heightmapLocalX * samplesSize.Y) + heightmapLocalZ;
                           
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
