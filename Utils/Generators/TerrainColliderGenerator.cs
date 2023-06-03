using Godot;
using System;
using TerrainEditor.Converters;

namespace TerrainEditor.Generators;

public class TerrainColliderGenerator : TerrainBaseGenerator
{
    /// <summary>
    /// Constructor
    /// </summary>
    public TerrainColliderGenerator(TerrainPatch patch) : base(patch)
    {
    }


    /// <summary>
    /// Generate collider data by given lod level
    /// </summary>
    public float[] GenerateLod(int collisionLod)
    {
        GD.Print("Create height collider");
        int newCollisionLod = Mathf.Clamp(collisionLod, 0, 2);

        // Prepare data
        if (Patch == null)
        {
            GD.PrintErr($"{nameof(Patch)} is null");
        }
        else
        {
            int heightFieldChunkSize = ((Patch.Info.ChunkSize + 1) >> newCollisionLod) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;
            // int heightFieldLength = heightFieldSize * heightFieldSize;

            int vertexCountEdgeMip = Patch.Info.VertexCountEdge >> newCollisionLod;
            int textureSizeMip = Patch.Info.TextureSize >> newCollisionLod;

            Image? img = Patch.HeightMap?.GetImage();
            if (img == null)
            {
                GD.PrintErr("Patch.HeightMap?.GetImage is null.");
            }
            else
            {
                Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(img.GetData());

                int heightMapLength = Patch.Info.HeightMapSize * Patch.Info.HeightMapSize;
                var heightField = new float[heightMapLength];

                GD.Print($"size:{Patch.Info.HeightMapSize}");

                for (var chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
                {
                    int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                    int chunkStartZ = chunkZ * heightFieldChunkSize;

                    for (var chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
                    {
                        int chunkTextureX = chunkX * vertexCountEdgeMip;
                        int chunkStartX = chunkX * heightFieldChunkSize;

                        for (var z = 0; z < vertexCountEdgeMip; z++)
                        for (var x = 0; x < vertexCountEdgeMip; x++)
                        {
                            // int textureIndexZ = (chunkTextureZ + z) * textureSizeMip;
                            // int textureIndexX = chunkTextureX + x;
                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                            float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRgbaBuffer[textureIndex]);
                            float height = normalizedHeight * Patch.Info.PatchHeight + Patch.Info.PatchOffset;

                            // bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRgbaBuffer[textureIndex]);
                            int heightmapX = chunkStartX + x;
                            int heightmapZ = chunkStartZ + z;

                            int dstIndex = heightmapX + heightmapZ * heightFieldSize;
                            heightField[dstIndex] = height;
                        }
                    }
                }

                return heightField;
            }
        }

        return Array.Empty<float>();
    }

    /// <summary>
    /// Modify collider data (todo)
    /// </summary>
    public float[] ModifyCollision(byte[] buffer, int collisionLod, Vector2I modifiedOffset, Vector2I modifiedSize, float[] heightFieldData) //modifing
    {
        if (Patch == null)
        {
            GD.PrintErr($"{nameof(Patch)} is null.");
            return Array.Empty<float>();
        }

        GD.Print("modify height collider");
        var newHeightFieldData = (float[])heightFieldData.Clone();
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        // Prepare data
        var modifiedOffsetRatio = new Vector2((float)modifiedOffset.X / Patch.Info.HeightMapSize, (float)modifiedOffset.Y / Patch.Info.HeightMapSize);
        var modifiedSizeRatio = new Vector2((float)modifiedSize.X / Patch.Info.HeightMapSize, (float)modifiedSize.Y / Patch.Info.HeightMapSize);

        int newCollisionLod = Mathf.Clamp(collisionLod, 0, 0); //from mip //TODO clamp 0,0 makes it 0, please double check when working on it.
        int heightFieldChunkSize = ((Patch.Info.ChunkSize + 1) >> newCollisionLod) - 1;
        int heightFieldSize = heightFieldChunkSize * Terrain3D.PATCH_CHUNK_EDGES + 1;

        var samplesOffset = new Vector2I(Mathf.FloorToInt(modifiedOffsetRatio.X * heightFieldSize), Mathf.FloorToInt(modifiedOffsetRatio.Y * heightFieldSize));
        var samplesSize = new Vector2I(Mathf.CeilToInt(modifiedSizeRatio.X * heightFieldSize), Mathf.CeilToInt(modifiedSizeRatio.Y * heightFieldSize));

        samplesSize.X = Math.Max(samplesSize.X, 1);
        samplesSize.Y = Math.Max(samplesSize.Y, 1);

        Vector2I samplesEnd = samplesOffset + samplesSize;
        samplesEnd.X = Math.Min(samplesEnd.X, heightFieldSize);
        samplesEnd.Y = Math.Min(samplesEnd.Y, heightFieldSize);

        // Setup terrain collision information
        int vertexCountEdgeMip = Patch.Info.VertexCountEdge >> newCollisionLod;
        int textureSizeMip = Patch.Info.TextureSize >> newCollisionLod;

        for (var chunkZ = 0; chunkZ < Terrain3D.PATCH_CHUNK_EDGES; chunkZ++)
        {
            int chunkTextureZ = chunkZ * vertexCountEdgeMip;
            int chunkStartZ = chunkZ * heightFieldChunkSize;

            // Skip unmodified chunks
            if (chunkStartZ >= samplesEnd.Y || chunkStartZ + vertexCountEdgeMip < samplesOffset.Y)
                continue;

            for (var chunkX = 0; chunkX < Terrain3D.PATCH_CHUNK_EDGES; chunkX++)
            {
                int chunkTextureX = chunkX * vertexCountEdgeMip;
                int chunkStartX = chunkX * heightFieldChunkSize;

                // Skip unmodified chunks
                if (chunkStartX >= samplesEnd.X || chunkStartX + vertexCountEdgeMip < samplesOffset.X)
                    continue;

                for (var z = 0; z < vertexCountEdgeMip; z++)
                {
                    // Skip unmodified columns
                    int heightmapZ = chunkStartZ + z;
                    int heightmapLocalZ = heightmapZ - samplesOffset.Y;
                    if (heightmapLocalZ < 0 || heightmapLocalZ >= samplesSize.Y)
                        continue;

                    // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                    for (var x = 0; x < vertexCountEdgeMip; x++)
                    {
                        // Skip unmodified rows
                        int heightmapX = chunkStartX + x;
                        int heightmapLocalX = heightmapX - samplesOffset.X;
                        if (heightmapLocalX < 0 || heightmapLocalX >= samplesSize.X)
                            continue;

                        int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;

                        float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRgbaBuffer[textureIndex]);
                        float height = normalizedHeight * Patch.Info.PatchHeight + Patch.Info.PatchOffset;

                        bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRgbaBuffer[textureIndex]);
                        int dstIndex = heightmapLocalX * samplesSize.Y + heightmapLocalZ;

                        if (isHole)
                            height = 0f;

                        newHeightFieldData[dstIndex] = height;
                    }
                }
            }
        }

        return newHeightFieldData;
    }
}