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
        public float[] CacheHeightmap()
        {
            if (patch.heightmap == null)
            {
                GD.PrintErr("Cant load heightmap");
                return null;
            }

            int heightMapLength = patch.info.heightMapSize * patch.info.heightMapSize;

            // Allocate data
            float[] _cachedHeightMap = new float[heightMapLength];
            float[] _cachedHolesMask = new float[heightMapLength];

            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(patch.heightmap.GetImage().GetData());

            // Extract heightmap data and denormalize it to get the pure height field
            float patchOffset = patch.info.patchOffset;
            float patchHeight = patch.info.patchHeight;

            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
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
                        int heightmapIndex = sz + sx;

                        float normalizedHeight = TerrainByteConverter.ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                        bool isHole = TerrainByteConverter.ReadIsHoleByte(imgRGBABuffer[textureIndex]);
                        float height = (normalizedHeight * patchHeight) + patchOffset;

                        _cachedHeightMap[heightmapIndex] = height;
                        _cachedHolesMask[heightmapIndex] = isHole ? 0 : 255;
                    }
                }
            }

            return _cachedHeightMap;
        }

        /**
         * Write height on an image
         */
        public void WriteHeights(ref Image image, ref float[] heightmapData)
        {
            var buffer = image.GetData();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.CHUNKS_COUNT_EDGE);
                int chunkZ = (chunkIndex / Terrain3D.CHUNKS_COUNT_EDGE);

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
                    }
                }
            }

            byte[] bytes = ToByteArray(imgRGBABuffer);
            image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, bytes);
        }

        /**
         * Writing normals to image (with smoothing)
         */
        public void WriteNormals(ref Image image, float[] heightmapData, Godot.Collections.Array<bool> holesMask, Vector2i modifiedOffset, Vector2i modifiedSize)
        {
            var buffer = image.GetData();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            // Expand the area for the normals to prevent issues on the edges (for the averaged normals)
            int heightMapSize = patch.info.heightMapSize;
            Vector2i modifiedEnd = modifiedOffset + modifiedSize;
            Vector2i normalsStart = new Vector2i(Math.Max(0, modifiedOffset.x - 1), Math.Max(0, modifiedOffset.y - 1));
            Vector2i normalsEnd = new Vector2i(Math.Min(heightMapSize, modifiedEnd.x + 1), Math.Min(heightMapSize, modifiedEnd.y + 1));
            Vector2i normalsSize = normalsEnd - normalsStart;


            // Prepare memory
            int normalsLength = normalsSize.x * normalsSize.y;
            var normalsPerVertex = new Vector3[normalsLength];

            Func<int, int, int, int, VertexResult> getVertex = (a, b, x, z) =>
              {
                  int i = (z + (b) - normalsStart.y) * normalsSize.x + (x + (a) - normalsStart.x);
                  int h = (z + (b)) * heightMapSize + (x + (a));

                  Vector3 v = new Vector3();
                  v.x = (x + (a)) * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
                  v.y = heightmapData[h]; // << takes time
                  v.z = (z + (b)) * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

                  return new VertexResult
                  {
                      v = v,
                      i = i
                  };
              };

            Func<int, int, int, int, VertexResult> getNormal = (a, b, x, z) =>
            {
                int i = (z + (b - 1)) * normalsSize.x + (x + (a - 1));
                Vector3 v = normalsPerVertex[i].Normalized();

                return new VertexResult
                {
                    v = v,
                    i = i
                };
            };

            // Calculate per-quad normals and apply them to nearby vertices
            for (int z = normalsStart.y; z < normalsEnd.y - 1; z++)
            {
                for (int x = normalsStart.x; x < normalsEnd.x - 1; x++)
                {
                    // Get four vertices from the quad
                    var v00 = getVertex(0, 0, x, z);
                    var v10 = getVertex(1, 0, x, z);
                    var v01 = getVertex(0, 1, x, z);
                    var v11 = getVertex(1, 1, x, z);

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
            for (int z = 1; z < normalsSize.y - 1; z++)
            {
                for (int x = 1; x < normalsSize.x - 1; x++)
                {
                    var n00 = getNormal(0, 0, x, z);
                    var n01 = getNormal(0, 1, x, z);
                    var n10 = getNormal(1, 0, x, z);
                    var n11 = getNormal(1, 1, x, z);
                    var n20 = getNormal(2, 0, x, z);

                    var n21 = getNormal(2, 1, x, z);
                    var n02 = getNormal(0, 2, x, z);
                    var n12 = getNormal(1, 2, x, z);
                    var n22 = getNormal(2, 2, x, z);

                    // Get four normals for the nearby quads
                    Vector3 avg = (n00.v + n01.v + n02.v + n10.v + n11.v + n12.v + n20.v + n21.v + n22.v) * (1.0f / 9.0f);

                    // Smooth normals by performing interpolation to average for nearby quads
                    normalsPerVertex[n11.i] = n11.v.Lerp(avg, 0.6f);
                }
            }

            // Write back to the data container
            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.CHUNKS_COUNT_EDGE);
                int chunkZ = (chunkIndex / Terrain3D.CHUNKS_COUNT_EDGE);

                int chunkTextureX = chunkX * patch.info.vertexCountEdge;
                int chunkTextureZ = chunkZ * patch.info.vertexCountEdge;

                int chunkHeightmapX = chunkX * patch.info.chunkSize;
                int chunkHeightmapZ = chunkZ * patch.info.chunkSize;

                // Skip unmodified chunks
                if (chunkHeightmapX >= modifiedEnd.x || chunkHeightmapX + patch.info.chunkSize < modifiedOffset.x ||
                    chunkHeightmapZ >= modifiedEnd.y || chunkHeightmapZ + patch.info.chunkSize < modifiedOffset.y)
                    continue;

                // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                for (int z = 0; z < patch.info.vertexCountEdge; z++)
                {
                    // Skip unmodified columns
                    int dz = chunkHeightmapZ + z - modifiedOffset.y;
                    if (dz < 0 || dz >= modifiedSize.y)
                        continue;
                    int hz = (chunkHeightmapZ + z) * heightMapSize;
                    int sz = (chunkHeightmapZ + z - normalsStart.y) * normalsSize.x;
                    int tz = (chunkTextureZ + z) * patch.info.textureSize;

                    // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                    for (int x = 0; x < patch.info.vertexCountEdge; x++)
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

                        Vector3 normal = (normalsPerVertex[normalIndex].Normalized() * 0.5f) + new Vector3(0.5f, 0.5f, 0.5f);

                        if (holesMask != null && !holesMask[heightmapIndex])
                            normal = Vector3.One;

                        imgRGBABuffer[textureIndex].b = (byte)(normal.x * 255f);
                        imgRGBABuffer[textureIndex].a = (byte)(normal.z * 255f);
                    }
                }
            }

            byte[] bytes = ToByteArray(imgRGBABuffer);
            image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, bytes);
        }

        /**
         * Detect heightmap ranges for chunks
         */
        public Vector2 CalculateHeightRange(ref float[] heightmap, ref float[] chunkOffsets, ref float[] chunkHeights)
        {
            float minPatchHeight = float.MaxValue;
            float maxPatchHeight = float.MinValue;

            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.CHUNKS_COUNT_EDGE) * patch.info.chunkSize;
                int chunkZ = (chunkIndex / Terrain3D.CHUNKS_COUNT_EDGE) * patch.info.chunkSize;

                float minHeight = float.MaxValue;
                float maxHeight = float.MinValue;

                for (int z = 0; z < patch.info.chunkSize + 1; z++)
                {
                    int sz = (chunkZ + z) * patch.info.heightMapSize;
                    for (int x = 0; x < patch.info.chunkSize + 1; x++)
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

            const double error = 1.0 / UInt16.MaxValue;
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
