using Godot;
using System;
using TerrainEditor.Converters;

namespace TerrainEditor.Generators;

public class TerrainHeightMapGenerator : TerrainBaseGenerator
{
    /// <summary>
    /// Constructor
    /// </summary>
    public TerrainHeightMapGenerator(TerrainPatch patch) : base(patch)
    {
    }

    /// <summary>
    /// Writing heigtmap image to an array for caching
    /// </summary>
    public void CacheHeights(ref float[] cachedHeightMap, ref byte[] cachedHolesMask)
    {
        if (Patch?.HeightMap == null)
        {
            GD.PrintErr("Cant load heightmap");
            return;
        }

        int heightMapLength = Patch.Info.HeightMapSize * Patch.Info.HeightMapSize;

        // Allocate data
        cachedHeightMap = new float[heightMapLength];
        cachedHolesMask = new byte[heightMapLength];

        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(Patch.HeightMap.GetImage().GetData());

        // Extract heightmap data and denormalize it to get the pure height field
        float patchOffset = Patch.Info.PatchOffset;
        float patchHeight = Patch.Info.PatchHeight;

        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkTextureX = Patch.Chunks[chunkIndex].Position.X * Patch.Info.VertexCountEdge;
            int chunkTextureZ = Patch.Chunks[chunkIndex].Position.Y * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = Patch.Chunks[chunkIndex].Position.X * Patch.Info.ChunkSize;
            int chunkHeightmapZ = Patch.Chunks[chunkIndex].Position.Y * Patch.Info.ChunkSize;

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
                    float height = normalizedHeight * patchHeight + patchOffset;

                    cachedHeightMap[heightmapIndex] = height;
                    cachedHolesMask[heightmapIndex] = isHole ? byte.MinValue : byte.MaxValue;
                }
            }
        }
    }

    /// <summary>
    /// Write height on an image
    /// </summary>
    public void WriteHeights(ref byte[] buffer, ref float[] heightmapData)
    {
        if (Patch == null)
        {
            GD.PrintErr($"{nameof(Patch)} is null.");
            return;
        }

        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        var textureIndexTest = 0;
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

                    float normalizedHeight = (heightmapData[heightmapIndex] - Patch.Info.PatchOffset) / Patch.Info.PatchHeight;
                    var quantizedHeight = (ushort)(normalizedHeight * ushort.MaxValue);

                    imgRgbaBuffer[textureIndex].r = (byte)(quantizedHeight & 0xff);
                    imgRgbaBuffer[textureIndex].g = (byte)((quantizedHeight >> 8) & 0xff);

                    if (textureIndex > textureIndexTest)
                        textureIndexTest = textureIndex;
                }
            }
        }

        buffer = ToByteArray(imgRgbaBuffer);
    }

    /// <summary>
    /// Writing normals to image (with smoothing)
    /// </summary>
    public void WriteNormals(ref byte[] buffer, float[] heightmapData, byte[] holesMask, Vector2I modifiedOffset, Vector2I modifiedSize)
    {
        if (Patch == null)
        {
            GD.PrintErr($"{nameof(Patch)} is null.");
            return;
        }

        Rgba[] imgRgbaBuffer = FromByteArray<Rgba>(buffer);

        // Expand the area for the normals to prevent issues on the edges (for the averaged normals)
        int heightMapSize = Patch.Info.HeightMapSize;
        Vector2I modifiedEnd = modifiedOffset + modifiedSize;
        var normalsStart = new Vector2I(Math.Max(0, modifiedOffset.X - 1), Math.Max(0, modifiedOffset.Y - 1));
        var normalsEnd = new Vector2I(Math.Min(heightMapSize, modifiedEnd.X + 1), Math.Min(heightMapSize, modifiedEnd.Y + 1));
        Vector2I normalsSize = normalsEnd - normalsStart;

        // Prepare memory
        int normalsLength = normalsSize.X * normalsSize.Y;
        var normalsPerVertex = new Vector3[normalsLength];

        Func<int, int, int, int, VertexResult> getVertex = (a, b, x, z) =>
        {
            int i = (z + b - normalsStart.Y) * normalsSize.X + (x + a - normalsStart.X);
            int h = (z + b) * heightMapSize + x + a;

            var v = new Vector3();
            v.X = (x + a) * Terrain3D.UNITS_PER_VERTEX;
            v.Y = heightmapData[h]; // << takes time
            v.Z = (z + b) * Terrain3D.UNITS_PER_VERTEX;

            return new VertexResult
            {
                V = v,
                I = i
            };
        };

        Func<int, int, int, int, VertexResult> getNormal = (a, b, x, z) =>
        {
            int i = (z + (b - 1)) * normalsSize.X + x + (a - 1);
            Vector3 v = normalsPerVertex[i].Normalized();

            return new VertexResult
            {
                V = v,
                I = i
            };
        };

        // Calculate per-quad normals and apply them to nearby vertices
        for (int z = normalsStart.Y; z < normalsEnd.Y - 1; z++)
        for (int x = normalsStart.X; x < normalsEnd.X - 1; x++)
        {
            // Get four vertices from the quad
            VertexResult v00 = getVertex(0, 0, x, z);
            VertexResult v10 = getVertex(1, 0, x, z);
            VertexResult v01 = getVertex(0, 1, x, z);
            VertexResult v11 = getVertex(1, 1, x, z);

            // Calculate normals for quad two vertices
            Vector3 n0 = (v00.V - v01.V).Cross(v01.V - v10.V).Normalized();
            Vector3 n1 = (v11.V - v10.V).Cross(v10.V - v01.V).Normalized();
            Vector3 n2 = n0 + n1;

            // Apply normal to each vertex using it
            normalsPerVertex[v00.I] += n1;
            normalsPerVertex[v01.I] += n2;
            normalsPerVertex[v10.I] += n2;
            normalsPerVertex[v11.I] += n0;
        }

        // Smooth normals
        for (var z = 1; z < normalsSize.Y - 1; z++)
        for (var x = 1; x < normalsSize.X - 1; x++)
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

        // Write back to the data container
        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkX = chunkIndex % Terrain3D.PATCH_CHUNK_EDGES;
            int chunkZ = chunkIndex / Terrain3D.PATCH_CHUNK_EDGES;

            int chunkTextureX = chunkX * Patch.Info.VertexCountEdge;
            int chunkTextureZ = chunkZ * Patch.Info.VertexCountEdge;

            int chunkHeightmapX = chunkX * Patch.Info.ChunkSize;
            int chunkHeightmapZ = chunkZ * Patch.Info.ChunkSize;

            // Skip unmodified chunks
            if (chunkHeightmapX >= modifiedEnd.X || chunkHeightmapX + Patch.Info.ChunkSize < modifiedOffset.X ||
                chunkHeightmapZ >= modifiedEnd.Y || chunkHeightmapZ + Patch.Info.ChunkSize < modifiedOffset.Y)
                continue;

            // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
            for (var z = 0; z < Patch.Info.VertexCountEdge; z++)
            {
                // Skip unmodified columns
                int dz = chunkHeightmapZ + z - modifiedOffset.Y;
                if (dz < 0 || dz >= modifiedSize.Y)
                    continue;
                int hz = (chunkHeightmapZ + z) * heightMapSize;
                int sz = (chunkHeightmapZ + z - normalsStart.Y) * normalsSize.X;
                int tz = (chunkTextureZ + z) * Patch.Info.TextureSize;

                // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                for (var x = 0; x < Patch.Info.VertexCountEdge; x++)
                {
                    // Skip unmodified rows
                    int dx = chunkHeightmapX + x - modifiedOffset.X;

                    if (dx < 0 || dx >= modifiedSize.X)
                        continue;

                    int hx = chunkHeightmapX + x;
                    int sx = chunkHeightmapX + x - normalsStart.X;
                    int tx = chunkTextureX + x;

                    int textureIndex = tz + tx;
                    int heightmapIndex = hz + hx;
                    int normalIndex = sz + sx;

                    Vector3 normal = normalsPerVertex[normalIndex].Normalized() * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);

                    //its a hole :-)
                    if (holesMask != null && holesMask.Length > heightmapIndex && holesMask[heightmapIndex] == 0)
                        normal = Vector3.One;

                    imgRgbaBuffer[textureIndex].b = (byte)(normal.X * byte.MaxValue);
                    imgRgbaBuffer[textureIndex].a = (byte)(normal.Z * byte.MaxValue);
                }
            }
        }

        buffer = ToByteArray(imgRgbaBuffer);
    }

    /// <summary>
    /// Detect heightmap ranges for chunks and returns a vec2(patchOffset, patchHeight)
    /// </summary>
    public Vector2? CalculateHeightRange(float[] heightmap, ref float[] chunkOffsets, ref float[] chunkHeights)
    {
        if (Patch == null)
        {
            GD.PrintErr($"{nameof(Patch)} is null.");
            return null;
        }

        var minPatchHeight = float.MaxValue;
        var maxPatchHeight = float.MinValue;

        for (var chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
        {
            int chunkX = chunkIndex % Terrain3D.PATCH_CHUNK_EDGES * Patch.Info.ChunkSize;
            int chunkZ = chunkIndex / Terrain3D.PATCH_CHUNK_EDGES * Patch.Info.ChunkSize;

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

        double error = 1.0 / ushort.MaxValue;
        minPatchHeight = AlignHeight(minPatchHeight - error, error);
        maxPatchHeight = AlignHeight(maxPatchHeight + error, error);

        //patchOffset, patchHeight
        return new Vector2(minPatchHeight, Math.Max(maxPatchHeight - minPatchHeight, 1.0f));
    }

    private float AlignHeight(double height, double error)
    {
        double heightCount = height / error;
        var heightCountInt = (long)heightCount;
        return (float)(heightCountInt * error);
    }
}