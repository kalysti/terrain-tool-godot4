using Godot;

namespace TerrainEditor.Generators;

public class TerrainSplatMapGenerator : TerrainBaseGenerator
{
    /**
         * Constructor
         */
    public TerrainSplatMapGenerator(TerrainPatch patch) : base(patch)
    {
    }

    public Color[] CacheSplatmap(int id)
    {
        // Prepare
        if (id > Patch.Splatmaps.Count)
        {
            GD.PrintErr("Cant load splatmap");
            return null;
        }

        // Cache all the splatmaps
        ImageTexture? splatmap = Patch.Splatmaps[id];
        Image? splatmapImg = splatmap.GetImage();
        int heightMapLength = Patch.Info.HeightMapSize * Patch.Info.HeightMapSize;

        // Allocate data
        var colors = new Color[heightMapLength];
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(splatmapImg.GetData());

        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkTextureX = Patch.Chunks[chunkIndex].Position.x * Patch.Info.VertexCountEdge;
            int chunkTextureZ = Patch.Chunks[chunkIndex].Position.y * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = Patch.Chunks[chunkIndex].Position.x * Patch.Info.ChunkSize;
            int chunkHeightmapZ = Patch.Chunks[chunkIndex].Position.y * Patch.Info.ChunkSize;

            for (var z = 0; z < Patch.Info.VertexCountEdge; z++)
            {
                int tz = (chunkTextureZ + z);
                int sz = (chunkHeightmapZ + z) * Patch.Info.HeightMapSize;

                for (var x = 0; x < Patch.Info.VertexCountEdge; x++)
                {
                    int tx = chunkTextureX + x;
                    int sx = chunkHeightmapX + x;
                    int textureIndex = tz + tx;
                    int splatmapIndex = sz + sx;

                    //  colors[splatmapIndex]

                    colors[splatmapIndex].r8 = (int)imgRgbaBuffer[textureIndex].r;
                    colors[splatmapIndex].g8 = (int)imgRgbaBuffer[textureIndex].g;
                    colors[splatmapIndex].b8 = (int)imgRgbaBuffer[textureIndex].b;
                    colors[splatmapIndex].a8 = (int)imgRgbaBuffer[textureIndex].a;
                }
            }
        }

        return colors;
    }

    public void WriteColors(ref byte[] buffer, ref Color[] colorData)
    {
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        var df = 0;
        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkX = (chunkIndex % Terrain3D.PATCH_CHUNK_EDGES);
            int chunkZ = (chunkIndex / Terrain3D.PATCH_CHUNK_EDGES);

            int chunkTextureX = chunkX * Patch.Info.VertexCountEdge;
            int chunkTextureZ = chunkZ * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = chunkX * Patch.Info.ChunkSize;
            int chunkHeightmapZ = chunkZ * Patch.Info.ChunkSize;

            for (var z = 0; z < Patch.Info.VertexCountEdge; z++)
            {
                int tz = (chunkTextureZ + z) * Patch.Info.TextureSize;
                int sz = (chunkHeightmapZ + z) * Patch.Info.HeightMapSize;

                for (var x = 0; x < Patch.Info.VertexCountEdge; x++)
                {
                    int tx = chunkTextureX + x;
                    int sx = chunkHeightmapX + x;

                    int textureIndex = tz + tx;
                    int heightmapIndex = sz + sx;

                    if (textureIndex > df)
                        df = textureIndex;

                    Color img = colorData[heightmapIndex];

                    imgRgbaBuffer[textureIndex].r = (byte)img.r8;
                    imgRgbaBuffer[textureIndex].g = (byte)img.g8;
                    imgRgbaBuffer[textureIndex].b = (byte)img.b8;
                    imgRgbaBuffer[textureIndex].a = (byte)img.a8;
                }
            }
        }

        buffer = ToByteArray(imgRgbaBuffer);
    }
}