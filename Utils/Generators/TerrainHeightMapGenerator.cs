using Godot;
using System;
using TerrainEditor.Converters;

namespace TerrainEditor.Generators
{

    public class TerrainHeightMapGenerator : TerrainBaseGenerator
    {
        /**
         * Constructor
         */
        public TerrainHeightMapGenerator(TerrainPatch _patch) : base(_patch)
        {
        }

        /**
         * Writing heigtmap image to an array for caching
         */
        public void CacheHeights(ref float[] _cachedHeightMap, ref byte[] _cachedHolesMask)
        {
            if (patch.heightmap == null)
            {
                GD.PrintErr("Cant load heightmap");
                return;
            }

            int heightMapLength = patch.info.heightMapSize * patch.info.heightMapSize;

            // Allocate data
            _cachedHeightMap = new float[heightMapLength];
            _cachedHolesMask = new byte[heightMapLength];

            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(patch.heightmap.GetImage().GetData());

            // Extract heightmap data and denormalize it to get the pure height field
            float patchOffset = patch.info.patchOffset;
            float patchHeight = patch.info.patchHeight;

            for (int chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
            {
                int chunkTextureX = patch.chunks[chunkIndex].position.X * patch.info.vertexCountEdge;
                int chunkTextureZ = patch.chunks[chunkIndex].position.Y * patch.info.vertexCountEdge;

                int chunkHeightmapX = patch.chunks[chunkIndex].position.X * patch.info.chunkSize;
                int chunkHeightmapZ = patch.chunks[chunkIndex].position.Y * patch.info.chunkSize;

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

                        float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                        bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRGBABuffer[textureIndex]);
                        float height = (normalizedHeight * patchHeight) + patchOffset;

                        _cachedHeightMap[heightmapIndex] = height;
                        _cachedHolesMask[heightmapIndex] = (isHole) ? byte.MinValue : byte.MaxValue;
                    }
                }
            }
        }

        /**
         * Write height on an image
         */
        public void WriteHeights(ref byte[] buffer, ref float[] heightmapData)
        {
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            int textureIndexTest = 0;
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

                        float normalizedHeight = (heightmapData[heightmapIndex] - patch.info.patchOffset) / patch.info.patchHeight;
                        UInt16 quantizedHeight = (UInt16)(normalizedHeight * UInt16.MaxValue);

                        imgRGBABuffer[textureIndex].r = (byte)(quantizedHeight & 0xff);
                        imgRGBABuffer[textureIndex].g = (byte)((quantizedHeight >> 8) & 0xff);

                        if (textureIndex > textureIndexTest)
                            textureIndexTest = textureIndex;
                    }
                }
            }

            buffer = ToByteArray(imgRGBABuffer);
        }

        /**
         * Writing normals to image (with smoothing)
         */
        public void WriteNormals(ref byte[] buffer, float[] heightmapData, byte[] holesMask, Vector2I modifiedOffset, Vector2I modifiedSize)
        {
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            // Expand the area for the normals to prevent issues on the edges (for the averaged normals)
            int heightMapSize = patch.info.heightMapSize;
            Vector2I modifiedEnd = modifiedOffset + modifiedSize;
            Vector2I normalsStart = new Vector2I(Math.Max(0, modifiedOffset.X - 1), Math.Max(0, modifiedOffset.Y - 1));
            Vector2I normalsEnd = new Vector2I(Math.Min(heightMapSize, modifiedEnd.X + 1), Math.Min(heightMapSize, modifiedEnd.Y + 1));
            Vector2I normalsSize = normalsEnd - normalsStart;

            // Prepare memory
            int normalsLength = normalsSize.X * normalsSize.Y;
            var normalsPerVertex = new Vector3[normalsLength];

            Func<int, int, int, int, VertexResult> getVertex = (a, b, x, z) =>
              {
                  int i = (z + (b) - normalsStart.Y) * normalsSize.X + (x + (a) - normalsStart.X);
                  int h = (z + (b)) * heightMapSize + (x + (a));

                  Vector3 v = new Vector3();
                  v.X = (x + (a)) * Terrain3D.UNITS_PER_VERTEX;
                  v.Y = heightmapData[h]; // << takes time
                  v.Z = (z + (b)) * Terrain3D.UNITS_PER_VERTEX;

                  return new VertexResult
                  {
                      v = v,
                      i = i
                  };
              };

            Func<int, int, int, int, VertexResult> getNormal = (a, b, x, z) =>
            {
                int i = (z + (b - 1)) * normalsSize.X + (x + (a - 1));
                Vector3 v = normalsPerVertex[i].Normalized();

                return new VertexResult
                {
                    v = v,
                    i = i
                };
            };

            // Calculate per-quad normals and apply them to nearby vertices
            for (int z = normalsStart.Y; z < normalsEnd.Y - 1; z++)
            {
                for (int x = normalsStart.X; x < normalsEnd.X - 1; x++)
                {
                    // Get four vertices from the quad
                    VertexResult v00 = getVertex(0, 0, x, z);
                    VertexResult v10 = getVertex(1, 0, x, z);
                    VertexResult v01 = getVertex(0, 1, x, z);
                    VertexResult v11 = getVertex(1, 1, x, z);

                    // Calculate normals for quad two vertices
                    Vector3 n0 = ((v00.v - v01.v).Cross(v01.v - v10.v)).Normalized();
                    Vector3 n1 = ((v11.v - v10.v).Cross(v10.v - v01.v)).Normalized();
                    Vector3 n2 = n0 + n1;

                    // Apply normal to each vertex using it
                    normalsPerVertex[v00.i] += n1;
                    normalsPerVertex[v01.i] += n2;
                    normalsPerVertex[v10.i] += n2;
                    normalsPerVertex[v11.i] += n0;

                }
            }

            // Smooth normals
            for (int z = 1; z < normalsSize.Y - 1; z++)
            {
                for (int x = 1; x < normalsSize.X - 1; x++)
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
                    Vector3 avg = (n00.v + n01.v + n02.v + n10.v + n11.v + n12.v + n20.v + n21.v + n22.v) * (1.0f / 9.0f);

                    // Smooth normals by performing interpolation to average for nearby quads
                    normalsPerVertex[n11.i] = n11.v.Lerp(avg, 0.6f);
                }
            }

            // Write back to the data container
            for (int chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.PATCH_CHUNK_EDGES);
                int chunkZ = (chunkIndex / Terrain3D.PATCH_CHUNK_EDGES);

                int chunkTextureX = chunkX * patch.info.vertexCountEdge;
                int chunkTextureZ = chunkZ * patch.info.vertexCountEdge;

                int chunkHeightmapX = chunkX * patch.info.chunkSize;
                int chunkHeightmapZ = chunkZ * patch.info.chunkSize;

                // Skip unmodified chunks
                if (chunkHeightmapX >= modifiedEnd.X || chunkHeightmapX + patch.info.chunkSize < modifiedOffset.X ||
                    chunkHeightmapZ >= modifiedEnd.Y || chunkHeightmapZ + patch.info.chunkSize < modifiedOffset.Y)
                    continue;

                // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                for (int z = 0; z < patch.info.vertexCountEdge; z++)
                {
                    // Skip unmodified columns
                    int dz = chunkHeightmapZ + z - modifiedOffset.Y;
                    if (dz < 0 || dz >= modifiedSize.Y)
                        continue;
                    int hz = (chunkHeightmapZ + z) * heightMapSize;
                    int sz = (chunkHeightmapZ + z - normalsStart.Y) * normalsSize.X;
                    int tz = (chunkTextureZ + z) * patch.info.textureSize;

                    // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                    for (int x = 0; x < patch.info.vertexCountEdge; x++)
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
                        if (holesMask != null && holesMask.Length >= heightmapIndex && holesMask[heightmapIndex] == 0)
                            normal = Vector3.One;

                        imgRGBABuffer[textureIndex].b = (byte)(normal.X * byte.MaxValue);
                        imgRGBABuffer[textureIndex].a = (byte)(normal.Z * byte.MaxValue);
                    }
                }
            }

            buffer = ToByteArray(imgRGBABuffer);
        }

        /**
         * Detect heightmap ranges for chunks and returns a vec2(patchOffset, patchHeight)
         */
        public Vector2 CalculateHeightRange(float[] heightmap, ref float[] chunkOffsets, ref float[] chunkHeights)
        {
            float minPatchHeight = float.MaxValue;
            float maxPatchHeight = float.MinValue;

            for (int chunkIndex = 0; chunkIndex < Terrain3D.PATCH_CHUNKS_AMOUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.PATCH_CHUNK_EDGES) * patch.info.chunkSize;
                int chunkZ = (chunkIndex / Terrain3D.PATCH_CHUNK_EDGES) * patch.info.chunkSize;

                float minHeight = float.MaxValue;
                float maxHeight = float.MinValue;

                for (int z = 0; z < patch.info.vertexCountEdge; z++)
                {
                    int sz = (chunkZ + z) * patch.info.heightMapSize;
                    for (int x = 0; x < patch.info.vertexCountEdge; x++)
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
            Int64 heightCountInt = (Int64)heightCount;
            return (float)(heightCountInt * error);
        }
    }
}
