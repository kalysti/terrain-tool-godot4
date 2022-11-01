using Godot;
using System;
using TerrainEditor.Converters;

namespace TerrainEditor.Generators;

public class TerrainHeightMapGenerator : TerrainBaseGenerator
{
    /**
         * Constructor
         */
    public TerrainHeightMapGenerator(TerrainPatch patch) : base(patch)
    {
    }

    /**
         * Writing heigtmap image to an array for caching
         */
    public void CacheHeights(ref float[] cachedHeightMap, ref byte[] cachedHolesMask)
    {
        if (Patch.Heightmap == null)
        {
            GD.PrintErr("Cant load heightmap");
            return;
        }

        int heightMapLength = Patch.Info.HeightMapSize * Patch.Info.HeightMapSize;

        // Allocate data
        cachedHeightMap = new float[heightMapLength];
        cachedHolesMask = new byte[heightMapLength];

        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(Patch.Heightmap.GetImage().GetData());

        // Extract heightmap data and denormalize it to get the pure height field
        float patchOffset = Patch.Info.PatchOffset;
        float patchHeight = Patch.Info.PatchHeight;

        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkTextureX = Patch.Chunks[chunkIndex].Position.x * Patch.Info.VertexCountEdge;
            int chunkTextureZ = Patch.Chunks[chunkIndex].Position.y * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = Patch.Chunks[chunkIndex].Position.x * Patch.Info.ChunkSize;
            int chunkHeightmapZ = Patch.Chunks[chunkIndex].Position.y * Patch.Info.ChunkSize;

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

                    float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRgbaBuffer[textureIndex]);
                    bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRgbaBuffer[textureIndex]);
                    float height = (normalizedHeight * patchHeight) + patchOffset;

                    cachedHeightMap[heightmapIndex] = height;
                    cachedHolesMask[heightmapIndex] = (isHole) ? byte.MinValue : byte.MaxValue;
                }
            }
        }
    }

    /**
         * Write height on an image
         */
    public void WriteHeights(ref byte[] buffer, ref float[] heightmapData)
    {
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        var textureIndexTest = 0;
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

                    float normalizedHeight = (heightmapData[heightmapIndex] - Patch.Info.PatchOffset) / Patch.Info.PatchHeight;
                    var quantizedHeight = (UInt16)(normalizedHeight * UInt16.MaxValue);

                    imgRgbaBuffer[textureIndex].r = (byte)(quantizedHeight & 0xff);
                    imgRgbaBuffer[textureIndex].g = (byte)((quantizedHeight >> 8) & 0xff);

                    if (textureIndex > textureIndexTest)
                        textureIndexTest = textureIndex;
                }
            }
        }

        buffer = ToByteArray(imgRgbaBuffer);
    }

    /**
         * Writing normals to image (with smoothing)
         */
    public void WriteNormals(ref byte[] buffer, float[] heightmapData, byte[] holesMask, Vector2i modifiedOffset, Vector2i modifiedSize)
    {
        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        // Expand the area for the normals to prevent issues on the edges (for the averaged normals)
        int heightMapSize = Patch.Info.HeightMapSize;
        Vector2i modifiedEnd = modifiedOffset + modifiedSize;
        var normalsStart = new Vector2i(Math.Max(0, modifiedOffset.x - 1), Math.Max(0, modifiedOffset.y - 1));
        var normalsEnd = new Vector2i(Math.Min(heightMapSize, modifiedEnd.x + 1), Math.Min(heightMapSize, modifiedEnd.y + 1));
        Vector2i normalsSize = normalsEnd - normalsStart;

        // Prepare memory
        int normalsLength = normalsSize.x * normalsSize.y;
        var normalsPerVertex = new Vector3[normalsLength];

        Func<int, int, int, int, VertexResult> getVertex = (a, b, x, z) =>
        {
            int i = (z + (b) - normalsStart.y) * normalsSize.x + (x + (a) - normalsStart.x);
            int h = (z + (b)) * heightMapSize + (x + (a));

            var v = new Vector3();
            v.x = (x + (a)) * Terrain3D.UNITS_PER_VERTEX;
            v.y = heightmapData[h]; // << takes time
            v.z = (z + (b)) * Terrain3D.UNITS_PER_VERTEX;

            return new VertexResult
            {
                V = v,
                I = i
            };
        };

        Func<int, int, int, int, VertexResult> getNormal = (a, b, x, z) =>
        {
            int i = (z + (b - 1)) * normalsSize.x + (x + (a - 1));
            Vector3 v = normalsPerVertex[i].Normalized();

            return new VertexResult
            {
                V = v,
                I = i
            };
        };

        // Calculate per-quad normals and apply them to nearby vertices
        for (int z = normalsStart.y; z < normalsEnd.y - 1; z++)
        {
            for (int x = normalsStart.x; x < normalsEnd.x - 1; x++)
            {
                // Get four vertices from the quad
                VertexResult v00 = getVertex(0, 0, x, z);
                VertexResult v10 = getVertex(1, 0, x, z);
                VertexResult v01 = getVertex(0, 1, x, z);
                VertexResult v11 = getVertex(1, 1, x, z);

                // Calculate normals for quad two vertices
                Vector3 n0 = ((v00.V - v01.V).Cross(v01.V - v10.V)).Normalized();
                Vector3 n1 = ((v11.V - v10.V).Cross(v10.V - v01.V)).Normalized();
                Vector3 n2 = n0 + n1;

                // Apply normal to each vertex using it
                normalsPerVertex[v00.I] += n1;
                normalsPerVertex[v01.I] += n2;
                normalsPerVertex[v10.I] += n2;
                normalsPerVertex[v11.I] += n0;

            }
        }

        // Smooth normals
        for (var z = 1; z < normalsSize.y - 1; z++)
        {
            for (var x = 1; x < normalsSize.x - 1; x++)
            {
                VertexResult n00 = getNormal(0, 0, x, z);
                VertexResult n10 = getNormal(1, 0, x, z);
                VertexResult n01 = getNormal(0, 1, x, z);
                VertexResult n11 = getNormal(1, 1, x, z);

                VertexResult n20 = getNormal(2, 0, x, z);

                VertexResult n21 = getNormal(2, 1, x, z);
                VertexResult n02 = getNormal(0, 2, x, z);
                VertexResult n12 = getNormal(1, 2, x, z);
                VertexResult n22 = getNormal(2, 2, x, z);

                /*
                 * The current vertex is (11). Calculate average for the nearby vertices.
                 * 00   01   02
                 * 10  (11)  12
                 * 20   21   22
                 */

                // Get four normals for the nearby quads
                Vector3 avg = (n00.V + n01.V + n02.V + n10.V + n11.V + n12.V + n20.V + n21.V + n22.V) * (1.0f / 9.0f);

                // Smooth normals by performing interpolation to average for nearby quads
                normalsPerVertex[n11.I] = n11.V.Lerp(avg, 0.6f);
            }
        }

        // Write back to the data container
        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkX = (chunkIndex % Terrain3D.PATCH_CHUNK_EDGES);
            int chunkZ = (chunkIndex / Terrain3D.PATCH_CHUNK_EDGES);

            int chunkTextureX = chunkX * Patch.Info.VertexCountEdge;
            int chunkTextureZ = chunkZ * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = chunkX * Patch.Info.ChunkSize;
            int chunkHeightmapZ = chunkZ * Patch.Info.ChunkSize;

            // Skip unmodified chunks
            if (chunkHeightmapX >= modifiedEnd.x || chunkHeightmapX + Patch.Info.ChunkSize < modifiedOffset.x ||
                chunkHeightmapZ >= modifiedEnd.y || chunkHeightmapZ + Patch.Info.ChunkSize < modifiedOffset.y)
                continue;

            // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
            for (var z = 0; z < Patch.Info.VertexCountEdge; z++)
            {
                // Skip unmodified columns
                int dz = chunkHeightmapZ + z - modifiedOffset.y;
                if (dz < 0 || dz >= modifiedSize.y)
                    continue;
                int hz = (chunkHeightmapZ + z) * heightMapSize;
                int sz = (chunkHeightmapZ + z - normalsStart.y) * normalsSize.x;
                int tz = (chunkTextureZ + z) * Patch.Info.TextureSize;

                // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                for (var x = 0; x < Patch.Info.VertexCountEdge; x++)
                {
                    // Skip unmodified rows
                    int dx = chunkHeightmapX + x - modifiedOffset.x;

                    if (dx < 0 || dx >= modifiedSize.x)
                        continue;

                    int hx = chunkHeightmapX + x;
                    int sx = chunkHeightmapX + x - normalsStart.x;
                    int tx = chunkTextureX + x;

                    int textureIndex = tz + tx;
                    int heightmapIndex = hz + hx;
                    int normalIndex = sz + sx;

                    Vector3 normal = normalsPerVertex[normalIndex].Normalized() * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);

                    //its a hole :-)
                    if (holesMask != null && holesMask.Length >= heightmapIndex && holesMask[heightmapIndex] == 0)
                        normal = Vector3.One;

                    imgRgbaBuffer[textureIndex].b = (byte)(normal.x * byte.MaxValue);
                    imgRgbaBuffer[textureIndex].a = (byte)(normal.z * byte.MaxValue);
                }
            }
        }

        buffer = ToByteArray(imgRgbaBuffer);
    }

    /**
         * Detect heightmap ranges for chunks and returns a vec2(patchOffset, patchHeight)
         */
    public Vector2 CalculateHeightRange(float[] heightmap, ref float[] chunkOffsets, ref float[] chunkHeights)
    {
        var minPatchHeight = float.MaxValue;
        var maxPatchHeight = float.MinValue;

        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkX = (chunkIndex % Terrain3D.PATCH_CHUNK_EDGES) * Patch.Info.ChunkSize;
            int chunkZ = (chunkIndex / Terrain3D.PATCH_CHUNK_EDGES) * Patch.Info.ChunkSize;

            var minHeight = float.MaxValue;
            var maxHeight = float.MinValue;

            for (var z = 0; z < Patch.Info.VertexCountEdge; z++)
            {
                int sz = (chunkZ + z) * Patch.Info.HeightMapSize;
                for (var x = 0; x < Patch.Info.VertexCountEdge; x++)
                {
                    int sx = chunkX + x;
                    float height = heightmap[sz + sx];

                    minHeight = Math.Min(minHeight, height);
                    maxHeight = Math.Max(maxHeight, height);
                }
            }

            chunkOffsets[chunkIndex] = minHeight;
            chunkHeights[chunkIndex] = Math.Max(maxHeight - minHeight, 1.0f);

            minPatchHeight = Math.Min(minPatchHeight, minHeight);
            maxPatchHeight = Math.Max(maxPatchHeight, maxHeight);
        }

        double error = 1.0 / UInt16.MaxValue;
        minPatchHeight = AlignHeight(minPatchHeight - error, error);
        maxPatchHeight = AlignHeight(maxPatchHeight + error, error);

        //patchOffset, patchHeight
        return new Vector2(minPatchHeight, Math.Max(maxPatchHeight - minPatchHeight, 1.0f));
    }

    private float AlignHeight(double height, double error)
    {
        double heightCount = height / error;
        var heightCountInt = (Int64)heightCount;
        return (float)(heightCountInt * error);
    }
}