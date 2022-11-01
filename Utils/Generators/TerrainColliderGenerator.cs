using Godot;
using System;
using TerrainEditor.Converters;

namespace TerrainEditor.Generators;

public class TerrainColliderGenerator : TerrainBaseGenerator
{
    /**
     * Constructor
     */
    public TerrainColliderGenerator(TerrainPatch patch) : base(patch)
    {
    }


    /**
     * Generate collider data by given lod level
     */
    public float[] GenerateLod(int collisionLod)
    {
        GD.Print("Create height collider");
        int newCollisionLod = Mathf.Clamp(collisionLod, 0, 2);

        // Prepare data
        int heightFieldChunkSize = ((Patch.Info.ChunkSize + 1) >> newCollisionLod) - 1;
        int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
        int heightFieldLength = heightFieldSize * heightFieldSize;

        int vertexCountEdgeMip = Patch.Info.VertexCountEdge >> newCollisionLod;
        int textureSizeMip = Patch.Info.TextureSize >> newCollisionLod;

        Image? img = Patch.Heightmap.GetImage();
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(img.GetData());

        int heightMapLength = Patch.Info.HeightMapSize * Patch.Info.HeightMapSize;
        var heightField = new float[heightMapLength];

        GD.Print("size:" + Patch.Info.HeightMapSize);

        for (var chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
        {
            int chunkTextureZ = chunkZ * vertexCountEdgeMip;
            int chunkStartZ = chunkZ * heightFieldChunkSize;

            for (var chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
            {
                int chunkTextureX = chunkX * vertexCountEdgeMip;
                int chunkStartX = chunkX * heightFieldChunkSize;

                for (var z = 0; z < vertexCountEdgeMip; z++)
                {
                    for (var x = 0; x < vertexCountEdgeMip; x++)
                    {
                        int textureIndexZ = (chunkTextureZ + z) * textureSizeMip;
                        int textureIndexX = chunkTextureX + x;
                        int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                        float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRgbaBuffer[textureIndex]);
                        float height = (normalizedHeight * Patch.Info.PatchHeight) + Patch.Info.PatchOffset;
                        bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRgbaBuffer[textureIndex]);

                        int heightmapX = chunkStartX + x;
                        int heightmapZ = chunkStartZ + z;

                        int dstIndex = heightmapX + (heightmapZ * heightFieldSize);
                        heightField[dstIndex] = height;
                    }
                }
            }
        }

        return heightField;
    }

    /**
     * Modify collider data (todo)
     */
    public float[] ModifyCollision(byte[] buffer, int _collisionLod, Vector2i modifiedOffset, Vector2i modifiedSize, float[] heightFieldData) //modifing
    {
        GD.Print("modify height collider");
        var newHeightfieldData = (float[])heightFieldData.Clone();
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        // Prepare data
        var modifiedOffsetRatio = new Vector2((float)modifiedOffset.x / Patch.Info.HeightMapSize, (float)modifiedOffset.y / Patch.Info.HeightMapSize);
        var modifiedSizeRatio = new Vector2((float)modifiedSize.x / Patch.Info.HeightMapSize, (float)modifiedSize.y / Patch.Info.HeightMapSize);

        int collisionLod = Mathf.Clamp(_collisionLod, 0, 0); //from mip //TODO clamp 0,0 makes it 0, please double check when working on it.
        int heightFieldChunkSize = ((Patch.Info.ChunkSize + 1) >> collisionLod) - 1;
        int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;

        var samplesOffset = new Vector2i(Mathf.FloorToInt(modifiedOffsetRatio.x * (float)heightFieldSize), Mathf.FloorToInt(modifiedOffsetRatio.y * (float)heightFieldSize));
        var samplesSize = new Vector2i(Mathf.CeilToInt(modifiedSizeRatio.x * (float)heightFieldSize), Mathf.CeilToInt(modifiedSizeRatio.y * (float)heightFieldSize));

        samplesSize.x = Math.Max(samplesSize.x, 1);
        samplesSize.y = Math.Max(samplesSize.y, 1);

        Vector2i samplesEnd = samplesOffset + samplesSize;
        samplesEnd.x = Math.Min(samplesEnd.x, heightFieldSize);
        samplesEnd.y = Math.Min(samplesEnd.y, heightFieldSize);

        // Setup terrain collision information
        int vertexCountEdgeMip = Patch.Info.VertexCountEdge >> collisionLod;
        int textureSizeMip = Patch.Info.TextureSize >> collisionLod;

        for (var chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
        {
            int chunkTextureZ = chunkZ * vertexCountEdgeMip;
            int chunkStartZ = chunkZ * heightFieldChunkSize;

            // Skip unmodified chunks
            if (chunkStartZ >= samplesEnd.y || chunkStartZ + vertexCountEdgeMip < samplesOffset.y)
                continue;

            for (var chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
            {
                int chunkTextureX = chunkX * vertexCountEdgeMip;
                int chunkStartX = chunkX * heightFieldChunkSize;

                // Skip unmodified chunks
                if (chunkStartX >= samplesEnd.x || chunkStartX + vertexCountEdgeMip < samplesOffset.x)
                    continue;

                for (var z = 0; z < vertexCountEdgeMip; z++)
                {
                    // Skip unmodified columns
                    int heightmapZ = chunkStartZ + z;
                    int heightmapLocalZ = heightmapZ - samplesOffset.y;
                    if (heightmapLocalZ < 0 || heightmapLocalZ >= samplesSize.y)
                        continue;

                    // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                    for (var x = 0; x < vertexCountEdgeMip; x++)
                    {
                        // Skip unmodified rows
                        int heightmapX = chunkStartX + x;
                        int heightmapLocalX = heightmapX - samplesOffset.x;
                        if (heightmapLocalX < 0 || heightmapLocalX >= samplesSize.x)
                            continue;

                        int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                        float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRgbaBuffer[textureIndex]);
                        float height = (normalizedHeight * Patch.Info.PatchHeight) + Patch.Info.PatchOffset;

                        bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRgbaBuffer[textureIndex]);
                        int dstIndex = (heightmapLocalX * samplesSize.y) + heightmapLocalZ;

                        if (isHole)
                            height = 0f;

                        //newHeightfieldData[dstIndex] = height;
                    }
                }
            }
        }

        return newHeightfieldData;
    }
}