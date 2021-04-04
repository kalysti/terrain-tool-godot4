using System.Xml.Schema;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{

    [Tool]
    public partial class TerrainPatch : Resource
    {
        [Export]
        public Vector3 position = new Vector3();

        [Export]
        public Vector2 patchCoord = new Vector2();

        [Export]
        public Godot.Collections.Array<TerrainChunk> chunks = new Godot.Collections.Array<TerrainChunk>();

        [Export]
        public ImageTexture heightmap = new ImageTexture();

        public AABB bounds = new AABB();

        [Export]
        public Mesh mesh = null;

        protected RID shapeRid;

        public Godot.Collections.Array<Vector3> debugLines = new Godot.Collections.Array<Vector3>();

        [Export]
        public TerrainPatchInfo info = new TerrainPatchInfo();

        public void Clear()
        {
            if (shapeRid != null)
            {
                PhysicsServer3D.FreeRid(shapeRid);
            }

            foreach (var chunk in chunks)
            {
                chunk.Clear();
            }
        }

        public void createChunks()
        {
            chunks.Clear();

            for (int i = 0; i < Terrain3D.CHUNKS_COUNT; i++)
            {
                var script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainChunk.cs").New();
                var res = script as TerrainChunk;
                res.ResourceLocalToScene = true;

                int px = i % Terrain3D.CHUNKS_COUNT_EDGE;
                int py = i / Terrain3D.CHUNKS_COUNT_EDGE;
                res.position = new Vector2((float)px, (float)py);

                var q = new Quat(1.0f, 1.0f, res.position.x, res.position.y) * (1.0f / Terrain3D.CHUNKS_COUNT_EDGE);

                res.heightmapUVScale = new Plane(q.x, q.y, q.z, q.w);

                //  res.ChunkSizeNextLOD = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);
                res.TerrainChunkSizeLOD0 = Terrain3D.TERRAIN_UNITS_PER_VERTEX * info.chunkSize;

                chunks.Add(res);
            }

        }


        public void UpdateHeightMap(Godot.Collections.Array<float> heightmapData, Vector2i modifiedOffset, Vector2i modifiedSize)
        {
            var image = createHeightMapTexutre();
            var str = new ImageTexture();

            var data = heightMapCachedData;
            if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
                modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
                modifiedOffset.x + modifiedSize.x > info.heightMapSize ||
                modifiedOffset.y + modifiedSize.y > info.heightMapSize)
            {
                GD.PrintErr("Invalid heightmap samples range.");
            }

            for (int z = 0; z < modifiedSize.y; z++)
            {
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    data[(z + modifiedOffset.y) * info.heightMapSize + (x + modifiedOffset.x)] = heightmapData[z * modifiedSize.x + x];
                }
            }

            float[] chunkOffsets = new float[Terrain3D.CHUNKS_COUNT];
            float[] chunkHeights = new float[Terrain3D.CHUNKS_COUNT];

            CalculateHeightmapRange(data, ref chunkOffsets, ref chunkHeights);

            image = DrawHeightMapOnImage(data, image);
            image = UpdateNormalsAndHoles(data, null, Vector2i.Zero, new Vector2i(info.heightMapSize, info.heightMapSize), image);
            str.CreateFromImage(image);

            int chunkIndex = 0;
            foreach (var chunk in chunks)
            {
                chunk.offset = chunkOffsets[chunkIndex];
                chunk.height = chunkHeights[chunkIndex];
                chunk.UpdateHeightmap(str);
                chunkIndex++;
            }
            
            heightMapCachedData = data;
            heightmap = str;
        }

        public void createHeightmap(int _chunkSize, Godot.Collections.Array<float> heightMapdata = null)
        {
            updateInfo(_chunkSize);
            createChunks();



            var image = createHeightMapTexutre();

            if (heightMapdata == null)
            {
                heightMapdata = new Godot.Collections.Array<float>();
                heightMapdata.Resize(info.heightMapSize * info.heightMapSize);
            }

            float[] chunkOffsets = new float[Terrain3D.CHUNKS_COUNT];
            float[] chunkHeights = new float[Terrain3D.CHUNKS_COUNT];
         
            CalculateHeightmapRange(heightMapdata, ref chunkOffsets, ref chunkHeights);

            int chunkIndex = 0;
            foreach (var chunk in chunks)
            {
                chunk.offset = chunkOffsets[chunkIndex];
                chunk.height = chunkHeights[chunkIndex];
            }
            image = DrawHeightMapOnImage(heightMapdata, image);
            image = UpdateNormalsAndHoles(heightMapdata, null, Vector2i.Zero, new Vector2i(info.heightMapSize, info.heightMapSize), image);

            //for testing
            image.SavePng("test.png");
            heightmap.CreateFromImage(image);

            heightMapCachedData = heightMapdata;
        }


        public Godot.Collections.Array<float> heightMapCachedData = new Godot.Collections.Array<float>();

        public bool RayCast(PhysicsDirectSpaceState3D state, Vector3 origin, Vector3 direction, float resultHitDistance, float maxDistance)
        {
            /*
            if (_physicsShape == nullptr)
                return false;

            // Prepare data
            PxTransform trans = _physicsActor->getGlobalPose();
            trans.p = trans.transform(_physicsShape->getLocalPose().p);
            const PxHitFlags hitFlags = (PxHitFlags)0;


            // Perform raycast test
            PxRaycastHit hit;

            state.IntersectRay();


            if (PxGeometryQuery::raycast(origin, (direction, _physicsShape->getGeometry().any(), trans, maxDistance, hitFlags, 1, &hit) != 0)
            {
                resultHitDistance = hit.distance;
                return true;
            }
            */


            return false;
        }

        public void Draw(RID scenario, Terrain3D terrainNode, RID shaderRid)
        {
            foreach (var chunk in chunks)
            {
                chunk.Clear();
            }

            GD.Print("geneate mesh");
            mesh = GenerateMesh(info.chunkSize, 0);

            foreach (var chunk in chunks)
            {
                chunk.Draw(this, info, scenario, mesh.GetRid(), heightmap, terrainNode, getOffset(), shaderRid);
                chunk.UpdateTransform(info, terrainNode.GlobalTransform, getOffset());
            }
        }

        public void UpdateTransform(Terrain3D terrainNode)
        {
            GD.Print("update transform");
            foreach (var chunk in chunks)
            {
                chunk.UpdateTransform(info, terrainNode.GlobalTransform, getOffset());
            }

            updateColliderPosition(terrainNode);
        }

        private Image createHeightMapTexutre()
        {
            var initData = new Image();
            initData.Create(info.textureSize, info.textureSize, true, Image.Format.Rgba8);

            return initData;
        }

        public void updateInfo(int _chunkSize)
        {
            var script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatchInfo.cs").New();
            var patch = script as TerrainPatchInfo;
            patch.ResourceLocalToScene = true;
            info = patch;

            var chunkSize = _chunkSize - 1;
            var vertexCountEdge = _chunkSize;
            var heightmapSize = chunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            var textureSet = vertexCountEdge * Terrain3D.CHUNKS_COUNT_EDGE;

            info.patchOffset = 0.0f;
            info.patchHeight = 1.0f;
            info.chunkSize = chunkSize;
            info.vertexCountEdge = vertexCountEdge;
            info.heightMapSize = heightmapSize;
            info.textureSize = textureSet;

        }

        public AABB getBounds()
        {
            var patchoffset = getOffset();

            int i = 0;
            foreach (var chunk in chunks)
            {
                var newBounds = chunk.getBounds(info, patchoffset);

                if (i == 0)
                {
                    bounds = newBounds;
                }
                else
                {
                    bounds = bounds.Merge(newBounds);
                }

                i++;
            }

            return bounds;
        }
        public Vector3 getOffset()
        {
            float size = info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX * Terrain3D.CHUNKS_COUNT_EDGE;
            return new Vector3(patchCoord.x * size, 0.0f, patchCoord.y * size);
        }

        public ArrayMesh GenerateMesh(int chunkSize, int lodIndex)
        {
            int chunkSizeLOD0 = chunkSize;

            // Prepare
            int vertexCount = (chunkSize + 1) >> lodIndex;
            chunkSize = vertexCount - 1;
            int indexCount = chunkSize * chunkSize * 2 * 3;
            int vertexCount2 = vertexCount * vertexCount;

            // Create vertex buffer
            float vertexTexelSnapTexCoord = 1.0f / chunkSize;


            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            for (int z = 0; z < vertexCount; z++)
            {
                for (int x = 0; x < vertexCount; x++)
                {
                    var buff = new Vector3(x * vertexTexelSnapTexCoord, 0f, z * vertexTexelSnapTexCoord);

                    //  uv_buffer.Add(new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord));
                    // Smooth LODs morphing based on Barycentric coordinates to morph to the lower LOD near chunk edges
                    var coord = new Quat(buff.z, buff.x, 1.0f - buff.x, 1.0f - buff.z);

                    // Apply some contrast
                    const float AdjustPower = 0.3f;

                    var color = new Color();
                    color.r = Convert.ToSingle(Math.Pow(coord.x, AdjustPower));
                    color.g = Convert.ToSingle(Math.Pow(coord.y, AdjustPower));
                    color.b = Convert.ToSingle(Math.Pow(coord.z, AdjustPower));
                    color.a = Convert.ToSingle(Math.Pow(coord.w, AdjustPower));

                    st.SetColor(color);
                    st.SetUv(new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord));
                    st.AddVertex(buff); //x

                    ///     color_buffer.Add(color);
                }
            }

            for (int z = 0; z < chunkSize; z++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int i00 = (x + 0) + (z + 0) * vertexCount;
                    int i10 = (x + 1) + (z + 0) * vertexCount;
                    int i11 = (x + 1) + (z + 1) * vertexCount;
                    int i01 = (x + 0) + (z + 1) * vertexCount;

                    st.AddIndex(i00);
                    st.AddIndex(i10);
                    st.AddIndex(i11);
                    st.AddIndex(i00);
                    st.AddIndex(i11);
                    st.AddIndex(i01);

                }
            }

            st.GenerateNormals();
            st.GenerateTangents();

            return st.Commit();
        }


        private Image DrawHeightMapOnImage(Godot.Collections.Array<float> heightmap, Image image)
        {
            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.CHUNKS_COUNT_EDGE);
                int chunkZ = (chunkIndex / Terrain3D.CHUNKS_COUNT_EDGE);

                int chunkTextureX = chunkX * info.vertexCountEdge;
                int chunkTextureZ = chunkZ * info.vertexCountEdge;

                int chunkHeightmapX = chunkX * info.chunkSize;
                int chunkHeightmapZ = chunkZ * info.chunkSize;

                for (int z = 0; z < info.vertexCountEdge; z++)
                {
                    int tz = (chunkTextureZ + z);
                    int sz = (chunkHeightmapZ + z) * info.heightMapSize;

                    for (int x = 0; x < info.vertexCountEdge; x++)
                    {
                        int tx = chunkTextureX + x;
                        int sx = chunkHeightmapX + x;
                        int textureIndex = tz + tx;
                        int heightmapIndex = sz + sx;

                        var newColor = WriteHeight(image.GetPixel(tz, tx), heightmap[heightmapIndex]);
                        image.SetPixel(tz, tx, newColor);
                    }
                }
            }

            return image;
        }
        private VertexResult getVertex(Godot.Collections.Array<float> heightmap, int heightMapSize, Vector2i normalsStart, Vector2i normalsSize, int a, int b, int x, int z)
        {
            int i = (z + (b) - normalsStart.y) * normalsSize.x + (x + (a) - normalsStart.x);
            int h = (z + (b)) * heightMapSize + (x + (a));

            Vector3 v = new Vector3();
            v.x = (x + (a)) * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            v.y = heightmap[h];
            v.z = (z + (b)) * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            return new VertexResult
            {
                v = v,
                i = i
            };
        }

        private VertexResult getNormal(Godot.Collections.Array<Vector3> normalsPerVertex, Vector2i normalsSize, int a, int b, int x, int z)
        {
            int i = (z + (b - 1)) * normalsSize.x + (x + (a - 1));
            Vector3 v = normalsPerVertex[i].Normalized();

            return new VertexResult
            {
                v = v,
                i = i
            };
        }

        private Image UpdateNormalsAndHoles(Godot.Collections.Array<float> heightmap, Godot.Collections.Array<bool> holesMask, Vector2i modifiedOffset, Vector2i modifiedSize, Image image)
        {
            // Expand the area for the normals to prevent issues on the edges (for the averaged normals)
            int heightMapSize = info.heightMapSize;
            Vector2i modifiedEnd = modifiedOffset + modifiedSize;
            Vector2i normalsStart = new Vector2i(Math.Max(0, modifiedOffset.x - 1), Math.Max(0, modifiedOffset.y - 1));
            Vector2i normalsEnd = new Vector2i(Math.Min(heightMapSize, modifiedEnd.x + 1), Math.Min(heightMapSize, modifiedEnd.y + 1));
            Vector2i normalsSize = normalsEnd - normalsStart;

            // Prepare memory
            int normalsLength = normalsSize.x * normalsSize.y;
            var normalsPerVertex = new Godot.Collections.Array<Vector3>();
            normalsPerVertex.Resize(normalsLength);

            // Calculate per-quad normals and apply them to nearby vertices
            for (int z = normalsStart.y; z < normalsEnd.y - 1; z++)
            {
                for (int x = normalsStart.x; x < normalsEnd.x - 1; x++)
                {
                    // Get four vertices from the quad
                    var v00 = getVertex(heightmap, heightMapSize, normalsStart, normalsSize, 0, 0, x, z);
                    var v10 = getVertex(heightmap, heightMapSize, normalsStart, normalsSize, 1, 0, x, z);
                    var v01 = getVertex(heightmap, heightMapSize, normalsStart, normalsSize, 0, 1, x, z);
                    var v11 = getVertex(heightmap, heightMapSize, normalsStart, normalsSize, 1, 1, x, z);

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
                    var n00 = getNormal(normalsPerVertex, normalsSize, 0, 0, x, z);
                    var n01 = getNormal(normalsPerVertex, normalsSize, 0, 1, x, z);
                    var n10 = getNormal(normalsPerVertex, normalsSize, 1, 0, x, z);
                    var n11 = getNormal(normalsPerVertex, normalsSize, 1, 1, x, z);
                    var n20 = getNormal(normalsPerVertex, normalsSize, 2, 0, x, z);
                    var n21 = getNormal(normalsPerVertex, normalsSize, 2, 1, x, z);
                    var n02 = getNormal(normalsPerVertex, normalsSize, 0, 2, x, z);
                    var n12 = getNormal(normalsPerVertex, normalsSize, 1, 2, x, z);
                    var n22 = getNormal(normalsPerVertex, normalsSize, 2, 2, x, z);

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

                int chunkTextureX = chunkX * info.vertexCountEdge;
                int chunkTextureZ = chunkZ * info.vertexCountEdge;

                int chunkHeightmapX = chunkX * info.chunkSize;
                int chunkHeightmapZ = chunkZ * info.chunkSize;

                // Skip unmodified chunks
                if (chunkHeightmapX >= modifiedEnd.x || chunkHeightmapX + info.chunkSize < modifiedOffset.x ||
                    chunkHeightmapZ >= modifiedEnd.y || chunkHeightmapZ + info.chunkSize < modifiedOffset.y)
                    continue;

                // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                for (int z = 0; z < info.vertexCountEdge; z++)
                {
                    // Skip unmodified columns
                    int dz = chunkHeightmapZ + z - modifiedOffset.y;
                    if (dz < 0 || dz >= modifiedSize.y)
                        continue;
                    int hz = (chunkHeightmapZ + z) * heightMapSize;
                    int sz = (chunkHeightmapZ + z - normalsStart.y) * normalsSize.x;
                    int tz = (chunkTextureZ + z); // not necceasary on c# => * info.textureSize

                    // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                    for (int x = 0; x < info.vertexCountEdge; x++)
                    {
                        // Skip unmodified rows
                        int dx = chunkHeightmapX + x - modifiedOffset.x;

                        if (dx < 0 || dx >= modifiedSize.x)
                            continue;

                        int hx = chunkHeightmapX + x;
                        int sx = chunkHeightmapX + x - normalsStart.x;
                        int tx = chunkTextureX + x;

                        // int textureIndex = tz + tx;
                        int heightmapIndex = hz + hx;
                        int normalIndex = sz + sx;

                        Vector3 normal = (normalsPerVertex[normalIndex].Normalized() * 0.5f) + new Vector3(0.5f, 0.5f, 0.5f);

                        if (holesMask != null && !holesMask[heightmapIndex])
                            normal = Vector3.One;

                        var color = image.GetPixel(tz, tx);

                        color.b8 = Convert.ToInt32((normal.x * byte.MaxValue));
                        color.a8 = Convert.ToInt32((normal.z * byte.MaxValue));
                        color.a8 = 255;

                        image.SetPixel(tz, tx, color);
                    }
                }
            }

            return image;
        }


        private Color WriteHeight(Color raw, float height)
        {
            float normalizedHeight = (height - info.patchOffset) / info.patchHeight;
            UInt16 quantizedHeight = (UInt16)(normalizedHeight * UInt16.MaxValue);

            raw.r8 = Convert.ToInt32((byte)(quantizedHeight & 0xff));
            raw.g8 = Convert.ToInt32((byte)((quantizedHeight >> 8) & 0xff));



            return raw;
        }
        private float ReadNormalizedHeight(Color raw)
        {
            var test = raw.r8 | (raw.g8 << 8);
            UInt16 quantizedHeight = Convert.ToUInt16(test);
            float normalizedHeight = (float)quantizedHeight / UInt16.MaxValue;

            return normalizedHeight;
        }


        private void CalculateHeightmapRange(Godot.Collections.Array<float> heightmap, ref float[] chunkOffsets, ref float[] chunkHeights)
        {
            float minPatchHeight = float.MaxValue;
            float maxPatchHeight = float.MinValue;

            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
            {
                int chunkX = (chunkIndex % Terrain3D.CHUNKS_COUNT_EDGE) * info.chunkSize;
                int chunkZ = (chunkIndex / Terrain3D.CHUNKS_COUNT_EDGE) * info.chunkSize;

                float minHeight = float.MaxValue;
                float maxHeight = float.MinValue;

                for (int z = 0; z < info.chunkSize + 1; z++)
                {
                    int sz = (chunkZ + z) * info.heightMapSize;
                    for (int x = 0; x < info.chunkSize + 1; x++)
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

            info.patchOffset = minPatchHeight;
            info.patchHeight = Math.Max(maxPatchHeight - minPatchHeight, 1.0f);
        }

        private float AlignHeight(double height, double error)
        {
            double heightCount = height / error;
            Int64 heightCountInt = (Int64)heightCount;
            return (float)(heightCountInt * error);
        }

        public Godot.Collections.Array<Vector3> GetDebugMeshLines(Godot.Collections.Array<float> heightField)
        {
            int map_width = info.textureSize;
            int map_depth = info.textureSize;

            Godot.Collections.Array<Vector3> points = new Godot.Collections.Array<Vector3>();

            if ((map_width != 0) && (map_depth != 0))
            {
                // This will be slow for large maps...
                // also we'll have to figure out how well bullet centers this shape...

                Vector2 size = new Vector2(map_width - 1, map_depth - 1);
                Vector2 start = size * -0.5f;

                // reserve some memory for our points..
                points.Resize(((map_width - 1) * map_depth * 2) + (map_width * (map_depth - 1) * 2));

                // now set our points
                int r_offset = 0;
                int w_offset = 0;

                for (int d = 0; d < map_depth; d++)
                {
                    Vector3 height = new Vector3(start.x, 0.0f, start.y);

                    for (int w = 0; w < map_width; w++)
                    {
                        height.y = heightField[r_offset++];

                        if (w != map_width - 1)
                        {
                            points[w_offset++] = height;
                            points[w_offset++] = new Vector3(height.x + 1.0f, heightField[r_offset], height.z);
                        }

                        if (d != map_depth - 1)
                        {
                            points[w_offset++] = height;
                            points[w_offset++] = new Vector3(height.x, heightField[r_offset + map_width - 1], height.z + 1.0f);
                        }

                        height.x += 1.0f;
                    }

                    start.y += 1.0f;
                }
            }

            return points;
        }

        private Godot.Collections.Array<float> getColliderLod(int collisionLod)
        {
            // Prepare datas
            int collisionLOD = 0;
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            // Setup terrain collision information
            // auto & mip = initData->Mips[collisionLOD];
            int vertexCountEdgeMip = info.vertexCountEdge >> collisionLOD;
            int textureSizeMip = info.textureSize >> collisionLOD;

            var img = heightmap.GetImage();

            var heightField = new Godot.Collections.Array<float>();

            for (int chunkX = 0; chunkX < Terrain3D.CHUNKS_COUNT_EDGE; chunkX++)
            {
                int chunkTextureX = chunkX * vertexCountEdgeMip;
                int chunkStartX = chunkX * heightFieldChunkSize;

                for (int chunkZ = 0; chunkZ < Terrain3D.CHUNKS_COUNT_EDGE; chunkZ++)
                {
                    int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                    int chunkStartZ = chunkZ * heightFieldChunkSize;

                    for (int z = 0; z < vertexCountEdgeMip; z++)
                    {
                        for (int x = 0; x < vertexCountEdgeMip; x++)
                        {
                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;
                            Color raw = img.GetPixel((chunkTextureX + x), (chunkTextureZ + z));
                            float normalizedHeight = ReadNormalizedHeight(raw);

                            int heightmapX = chunkStartX + x;
                            int heightmapZ = chunkStartZ + z;

                            int dstIndex = (heightmapX * heightFieldSize) + heightmapZ;

                            heightField.Add(normalizedHeight);
                        }
                    }
                }
            }
            return heightField;
        }

        public Transform transform;

        public void updateColliderPosition(Terrain3D terrain)
        {
            int collisionLOD = 0;
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            var scale = new Vector3((float)info.heightMapSize / heightFieldSize, 1, (float)info.heightMapSize / heightFieldSize);
            // var scaleFac = new Vector3(scale.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX, scale.y, scale.z * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
            var scaleFac = new Vector3(scale.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX, scale.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX, scale.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
            transform = new Transform();
            transform.origin = terrain.GlobalTransform.origin + new Vector3(chunks[0].TerrainChunkSizeLOD0 * 2, 0, chunks[0].TerrainChunkSizeLOD0 * 2);
            transform.basis = terrain.GlobalTransform.basis;
            transform.basis.Scale = scaleFac;


            var staticBody = new StaticBody3D();
            var heightShape = new HeightMapShape3D();
            var shape = new CollisionShape3D();
            heightShape.MapWidth = info.textureSize;
            heightShape.MapDepth = info.textureSize;
            shape.Shape = heightShape;

            PhysicsServer3D.BodySetState(terrain.bodyRid, PhysicsServer3D.BodyState.Transform, transform);
        }

        public void CookCollision(int collisionLod, Terrain3D terrain)
        {
            var start = OS.GetTicksMsec();

            int collisionLOD = 0;
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            var heightField = getColliderLod(collisionLOD);

            //create heightmap shape
            shapeRid = PhysicsServer3D.HeightmapShapeCreate();

            PhysicsServer3D.BodyAddShape(terrain.bodyRid, shapeRid);
            PhysicsServer3D.ShapeSetData(shapeRid, new Godot.Collections.Dictionary() {
                {
                    "width", info.textureSize
                },
                {
                    "depth", info.textureSize
                },
                {
                    "heights", heightField.ToArray()
                },
                {
                    "cell_size", 1.0f
                }
            });

            updateColliderPosition(terrain);

            PhysicsServer3D.BodySetCollisionLayer(terrain.bodyRid, terrain.collisionLayer);
            PhysicsServer3D.BodySetCollisionMask(terrain.bodyRid, terrain.collisionMask);

            debugLines = GetDebugMeshLines(heightField);
        }
    }


}
