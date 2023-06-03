using Godot;

namespace TerrainEditor.Generators;

public class TerrainSplatMapGenerator : TerrainBaseGenerator
{
    /// <summary>
    /// Constructor
    /// </summary>
    public TerrainSplatMapGenerator(TerrainPatch patch) : base(patch)
    {
    }

    public Color[]? CacheSplatmap(int id)
    {
        // Prepare
        if (Patch == null || id > Patch.SplatMaps.Count)
        {
            GD.PrintErr("Cant load splatmap");
            return null;
        }

        // Cache all the splat maps
        ImageTexture? splatmap = Patch.SplatMaps[id];
        Image? splatmapImg = splatmap.GetImage();
        int heightMapLength = Patch.Info.HeightMapSize * Patch.Info.HeightMapSize;

        // Allocate data
        var colors = new Color[heightMapLength];
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(splatmapImg.GetData());

        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkTextureX = Patch.Chunks[chunkIndex].Position.X * Patch.Info.VertexCountEdge;
            int chunkTextureZ = Patch.Chunks[chunkIndex].Position.Y * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = Patch.Chunks[chunkIndex].Position.X * Patch.Info.ChunkSize;
            int chunkHeightmapZ = Patch.Chunks[chunkIndex].Position.Y * Patch.Info.ChunkSize;

            for (var z = 0; z < Patch.Info.VertexCountEdge; z++)
            {
                int tz = chunkTextureZ + z;
                int sz = (chunkHeightmapZ + z) * Patch.Info.HeightMapSize;

                for (var x = 0; x < Patch.Info.VertexCountEdge; x++)
                {
                    int tx = chunkTextureX + x;
                    int sx = chunkHeightmapX + x;
                    int textureIndex = tz + tx;
                    int splatmapIndex = sz + sx;

                    //  colors[splatmapIndex]

                    colors[splatmapIndex].R8 = imgRgbaBuffer[textureIndex].r;
                    colors[splatmapIndex].G8 = imgRgbaBuffer[textureIndex].g;
                    colors[splatmapIndex].B8 = imgRgbaBuffer[textureIndex].b;
                    colors[splatmapIndex].A8 = imgRgbaBuffer[textureIndex].a;
                }
            }
        }

        return colors;
    }

    public void WriteColors(ref byte[] buffer, ref Color[] colorData)
    {
        if (Patch == null)
        {
            GD.PrintErr($"{nameof(Patch)} is null.");
            return;
        }

        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        var df = 0;
        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkX = chunkIndex % Terrain3D.PATCH_CHUNK_EDGES;
            int chunkZ = chunkIndex / Terrain3D.PATCH_CHUNK_EDGES;

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

                    imgRgbaBuffer[textureIndex].r = (byte)img.R8;
                    imgRgbaBuffer[textureIndex].g = (byte)img.G8;
                    imgRgbaBuffer[textureIndex].b = (byte)img.B8;
                    imgRgbaBuffer[textureIndex].a = (byte)img.A8;
                }
            }
        }

        buffer = ToByteArray(imgRgbaBuffer);
    }
}