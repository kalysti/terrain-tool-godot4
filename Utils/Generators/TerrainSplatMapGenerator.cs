using Godot;
using System;
namespace TerrainEditor.Generators
{

    public class TerrainSplatMapGenerator : TerrainBaseGenerator
    {
        /**
         * Constructor
         */
        public TerrainSplatMapGenerator(TerrainPatch _patch) : base(_patch)
        {
        }

        public Color[] CacheSplatmap(int id)
        {
            // Prepare
            if (id > patch.splatmaps.Count)
            {
                GD.PrintErr("Cant load splatmap");
                return null;
            }

            // Cache all the splatmaps
            var splatmap = patch.splatmaps[id];
            var splatmapImg = splatmap.GetImage();
            int heightMapLength = patch.info.heightMapSize * patch.info.heightMapSize;

            // Allocate data
            Color[] colors = new Color[heightMapLength];
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(splatmapImg.GetData());

            for (int chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
            {
                int chunkTextureX = patch.chunks[chunkIndex].position.x * patch.info.vertexCountEdge;
                int chunkTextureZ = patch.chunks[chunkIndex].position.y * patch.info.vertexCountEdge;

                int chunkHeightmapX = patch.chunks[chunkIndex].position.x * patch.info.chunkSize;
                int chunkHeightmapZ = patch.chunks[chunkIndex].position.y * patch.info.chunkSize;

                for (int z = 0; z < patch.info.vertexCountEdge; z++)
                {
                    int tz = (chunkTextureZ + z);
                    int sz = (chunkHeightmapZ + z) * patch.info.heightMapSize;

                    for (int x = 0; x < patch.info.vertexCountEdge; x++)
                    {
                        int tx = chunkTextureX + x;
                        int sx = chunkHeightmapX + x;
                        int textureIndex = tz + tx;
                        int splatmapIndex = sz + sx;

                        //  colors[splatmapIndex]

                        colors[splatmapIndex].r8 = (int)imgRGBABuffer[textureIndex].r;
                        colors[splatmapIndex].g8 = (int)imgRGBABuffer[textureIndex].g;
                        colors[splatmapIndex].b8 = (int)imgRGBABuffer[textureIndex].b;
                        colors[splatmapIndex].a8 = (int)imgRGBABuffer[textureIndex].a;
                    }
                }
            }

            return colors;
        }

        public void WriteColors(ref byte[] buffer, ref Color[] colorData)
        {
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            var df = 0;
            for (int chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.PATCH_CHUNK_EDGES);
                int chunkZ = (chunkIndex / Terrain3D.PATCH_CHUNK_EDGES);

                int chunkTextureX = chunkX * patch.info.vertexCountEdge;
                int chunkTextureZ = chunkZ * patch.info.vertexCountEdge;

                int chunkHeightmapX = chunkX * patch.info.chunkSize;
                int chunkHeightmapZ = chunkZ * patch.info.chunkSize;

                for (int z = 0; z < patch.info.vertexCountEdge; z++)
                {
                    int tz = (chunkTextureZ + z) * patch.info.textureSize;
                    int sz = (chunkHeightmapZ + z) * patch.info.heightMapSize;

                    for (int x = 0; x < patch.info.vertexCountEdge; x++)
                    {
                        int tx = chunkTextureX + x;
                        int sx = chunkHeightmapX + x;

                        int textureIndex = tz + tx;
                        int heightmapIndex = sz + sx;

                        if (textureIndex > df)
                            df = textureIndex;

                        Color img = colorData[heightmapIndex];

                        imgRGBABuffer[textureIndex].r = (byte)img.r8;
                        imgRGBABuffer[textureIndex].g = (byte)img.g8;
                        imgRGBABuffer[textureIndex].b = (byte)img.b8;
                        imgRGBABuffer[textureIndex].a = (byte)img.a8;
                    }
                }
            }

            buffer = ToByteArray(imgRGBABuffer);
        }
    }


}
