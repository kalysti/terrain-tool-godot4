using System.Xml.Schema;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices;
using Godot;
using System;
using System.Linq;
using System.IO;

using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
namespace TerrainEditor
{

    [Tool]
    public partial class TerrainPatch : Resource
    {

        public Godot.Collections.Dictionary<int, ArrayMesh> meshCache = new Godot.Collections.Dictionary<int, ArrayMesh>();


        [Export]
        public Vector2i patchCoord = new Vector2i();

        [Export]

        public Vector3 offset = new Vector3();


        [Export]
        public Godot.Collections.Array<TerrainChunk> chunks = new Godot.Collections.Array<TerrainChunk>();

        [Export]
        public ImageTexture heightmap;

        public AABB bounds = new AABB();


        protected RID shapeRid;

        protected RID bodyRid;

        [Export]
        public TerrainPatchInfo info = new TerrainPatchInfo();

        public void Clear()
        {
            cachedHeightMapData = null;

            if (shapeRid != null)
            {
                PhysicsServer3D.FreeRid(shapeRid);
                shapeRid = null;
            }

            if (bodyRid != null)
                PhysicsServer3D.BodyClearShapes(bodyRid);

            foreach (var chunk in chunks)
            {
                chunk.Clear();
            }

            if (bodyRid != null)
                PhysicsServer3D.BodyClearShapes(bodyRid);

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
                res.position = new Vector2i((int)px, (int)py);

                //  res.ChunkSizeNextLOD = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);
                res.TerrainChunkSizeLOD0 = Terrain3D.TERRAIN_UNITS_PER_VERTEX * info.chunkSize;

                chunks.Add(res);
            }

        }

        public void UpdateHeightMap(Terrain3D terrain, float[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
        {

            var image = heightmap.GetImage().Duplicate() as Image;
            var data = CacheHeightData();

            if (modifiedOffset.x < 0 || modifiedOffset.y < 0 ||
                modifiedSize.x <= 0 || modifiedSize.y <= 0 ||
                modifiedOffset.x + modifiedSize.x > info.heightMapSize ||
                modifiedOffset.y + modifiedSize.y > info.heightMapSize)
            {
                GD.PrintErr("Invalid heightmap samples range.");
            }

            info.patchOffset = 0.0f;
            info.patchHeight = 1.0f;

            for (int z = 0; z < modifiedSize.y; z++)
            {
                for (int x = 0; x < modifiedSize.x; x++)
                {
                    data[(z + modifiedOffset.y) * info.heightMapSize + (x + modifiedOffset.x)] = samples[z * modifiedSize.x + x];
                }
            }

            float[] chunkOffsets = new float[Terrain3D.CHUNKS_COUNT];
            float[] chunkHeights = new float[Terrain3D.CHUNKS_COUNT];

            CalculateHeightmapRange(data, ref chunkOffsets, ref chunkHeights);

            int chunkIndex = 0;
            foreach (var chunk in chunks)
            {
                chunk.offset = chunkOffsets[chunkIndex];
                chunk.height = chunkHeights[chunkIndex];
                chunk.UpdateHeightmap(info);
                chunkIndex++;
            }

            DrawHeightMapOnImage(ref image, ref data);
            UpdateNormalsAndHoles(ref image, data, null, modifiedOffset, modifiedSize);

            terrain.updateBounds();
            terrain.updateDebug();

            var newColliderData = ModifyCollision(0, modifiedOffset, modifiedSize, data);
            CookModifyCollision(terrain, newColliderData);

            heightmap.Update(image, true);
            UpdateTransform(terrain);
        }

        public void createEmptyHeightmap(int _chunkSize, float[] heightMapdata = null)
        {
            updateInfo(_chunkSize);
            createChunks();

            var image = createHeightMapTexutre();

            if (heightMapdata == null)
            {
                heightMapdata = new float[info.heightMapSize * info.heightMapSize];
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

            DrawHeightMapOnImage(ref image, ref heightMapdata);
            UpdateNormalsAndHoles(ref image, heightMapdata, null, Vector2i.Zero, new Vector2i(info.heightMapSize, info.heightMapSize));

            if (heightmap == null)
            {
                heightmap = new ImageTexture();
                heightmap.CreateFromImage(image);
            }

            else
                heightmap.Update(image);
        }


        public void Draw(RID scenario, Terrain3D terrainNode, RID shaderRid)
        {

            //clear chunks scene
            foreach (var chunk in chunks)
            {
                chunk.Clear();
            }

            bodyRid = PhysicsServer3D.BodyCreate();

            PhysicsServer3D.BodySetMode(bodyRid, PhysicsServer3D.BodyMode.Static);
            PhysicsServer3D.BodyAttachObjectInstanceId(bodyRid, terrainNode.GetInstanceId());
            PhysicsServer3D.BodySetSpace(bodyRid, terrainNode.GetWorld3d().Space);

            //create cache
            foreach (var chunk in chunks)
            {
                chunk.Draw(this, info, scenario, ref heightmap, terrainNode, getOffset(), shaderRid);
                chunk.UpdateTransform(info, terrainNode.GlobalTransform, getOffset());
                chunk.SetDefaultMaterial(terrainNode.terrainDefaultTexture);
            }
        }

        public void UpdateTransform(Terrain3D terrainNode)
        {
            foreach (var chunk in chunks)
            {
                chunk.UpdateTransform(info, terrainNode.GlobalTransform, getOffset());
            }

            updateColliderPosition(terrainNode);
        }

        private Image createHeightMapTexutre()
        {
            var initData = new Image();
            initData.Create(info.textureSize, info.textureSize, false, Image.Format.Rgba8);

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

        [StructLayout(LayoutKind.Sequential)]
        public struct RGBA
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }

        private static byte[] ToByteArray<T>(T[] source) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(source, GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();
                byte[] destination = new byte[source.Length * Marshal.SizeOf(typeof(T))];
                Marshal.Copy(pointer, destination, 0, destination.Length);
                return destination;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        private static T[] FromByteArray<T>(byte[] source) where T : struct
        {
            T[] destination = new T[source.Length / Marshal.SizeOf(typeof(T))];
            GCHandle handle = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();
                Marshal.Copy(source, 0, pointer, source.Length);
                return destination;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }


        private void DrawHeightMapOnImage(ref Image image, ref float[] heightmapData)
        {
            var buffer = image.GetData();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            var df = 0;
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
                    int tz = (chunkTextureZ + z) * info.textureSize;
                    int sz = (chunkHeightmapZ + z) * info.heightMapSize;

                    for (int x = 0; x < info.vertexCountEdge; x++)
                    {
                        int tx = chunkTextureX + x;
                        int sx = chunkHeightmapX + x;

                        int textureIndex = tz + tx;
                        int heightmapIndex = sz + sx;

                        if (textureIndex > df)
                            df = textureIndex;



                        float normalizedHeight = (heightmapData[heightmapIndex] - info.patchOffset) / info.patchHeight;
                        UInt16 quantizedHeight = (UInt16)(normalizedHeight * UInt16.MaxValue);

                        imgRGBABuffer[textureIndex].r = (byte)(quantizedHeight & 0xff);
                        imgRGBABuffer[textureIndex].g = (byte)((quantizedHeight >> 8) & 0xff);
                    }
                }
            }

            byte[] bytes = ToByteArray(imgRGBABuffer);
            var newImage = new Image();
            image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, bytes);
        }

        private void WriteHeightByte(ref RGBA raw, float height)
        {
            float normalizedHeight = (height - info.patchOffset) / info.patchHeight;
            UInt16 quantizedHeight = (UInt16)(normalizedHeight * UInt16.MaxValue);

            raw.r = (byte)(quantizedHeight & 0xff);
            raw.g = (byte)((quantizedHeight >> 8) & 0xff);
        }

        private void UpdateNormalsAndHoles(ref Image image, float[] heightmapData, Godot.Collections.Array<bool> holesMask, Vector2i modifiedOffset, Vector2i modifiedSize)
        {

            var buffer = image.GetData();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(buffer);

            //   var buffer = image.SavePngToBuffer();

            // Expand the area for the normals to prevent issues on the edges (for the averaged normals)
            int heightMapSize = info.heightMapSize;
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

            int d = 0;
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
                    int tz = (chunkTextureZ + z) * info.textureSize;

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


        public float ReadNormalizedHeightByte(RGBA raw)
        {
            var test = raw.r | (raw.g << 8);
            UInt16 quantizedHeight = Convert.ToUInt16(test);

            float normalizedHeight = (float)quantizedHeight / UInt16.MaxValue;
            return normalizedHeight;
        }

        public bool ReadIsHole(Color raw)
        {
            return (raw.b8 + raw.a8) >= (int)(1.9f * byte.MaxValue);
        }
        public bool ReadIsHoleByte(RGBA raw)
        {
            return (raw.b + raw.a) >= (int)(1.9f * byte.MaxValue);
        }

        private void CalculateHeightmapRange(float[] heightmap, ref float[] chunkOffsets, ref float[] chunkHeights)
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

        public Godot.Collections.Array<Vector3> GetDebugMeshLines(float[] heightField)
        {
            int map_width = info.heightMapSize;
            int map_depth = info.heightMapSize;

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
        public Transform transform;
        private void updateColliderPosition(Terrain3D terrain)
        {
            int collisionLOD = 1;
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;


            var scale2 = new Vector3((float)info.heightMapSize / heightFieldSize, 1, (float)info.heightMapSize / heightFieldSize);
            var scaleFac = new Vector3(scale2.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX, scale2.x, scale2.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX);
            transform = new Transform();
            transform.origin = terrain.GlobalTransform.origin + new Vector3(chunks[0].TerrainChunkSizeLOD0 * 2, 0, chunks[0].TerrainChunkSizeLOD0 * 2) + offset;
            transform.basis = terrain.GlobalTransform.basis;


            var scale = transform.basis.Scale;
            scale.x *= Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            scale.z *= Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            transform.basis.Scale = scale;

            PhysicsServer3D.BodySetState(bodyRid, PhysicsServer3D.BodyState.Transform, transform);

        }

        protected float[] cachedHeightMapData;

        public float[] CacheHeightData()
        {
            if (cachedHeightMapData == null || cachedHeightMapData.Length <= 0)
                return DoCaching();
            else
                return cachedHeightMapData;
        }

        protected float[] DoCaching()
        {
            if (heightmap == null)
            {
                GD.PrintErr("Cant load heightmap");
                return null;
            }

            var img = heightmap.GetImage();
            int heightMapLength = info.heightMapSize * info.heightMapSize;

            // Allocate data
            float[] _cachedHeightMap = new float[heightMapLength];
            float[] _cachedHolesMask = new float[heightMapLength];

            int d = 0;

            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(img.GetData());

            // Extract heightmap data and denormalize it to get the pure height field
            float patchOffset = info.patchOffset;
            float patchHeight = info.patchHeight;
            for (int chunkIndex = 0; chunkIndex < Terrain3D.CHUNKS_COUNT; chunkIndex++)
            {
                int chunkTextureX = chunks[chunkIndex].position.x * info.vertexCountEdge;
                int chunkTextureZ = chunks[chunkIndex].position.y * info.vertexCountEdge;

                int chunkHeightmapX = chunks[chunkIndex].position.x * info.chunkSize;
                int chunkHeightmapZ = chunks[chunkIndex].position.y * info.chunkSize;

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

                        Color raw = img.GetPixel(tx, tz);

                        float normalizedHeight = ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                        bool isHole = ReadIsHoleByte(imgRGBABuffer[textureIndex]);

                        float height = (normalizedHeight * patchHeight) + patchOffset;

                        _cachedHeightMap[heightmapIndex] = height;
                        _cachedHolesMask[heightmapIndex] = isHole ? 0 : 255;

                        d++;
                    }
                }
            }

            cachedHeightMapData = _cachedHeightMap;
            return _cachedHeightMap;
        }


        private float[] getColliderLod(int collisionLod) // init
        {
            int collisionLOD = Math.Clamp(collisionLod, 0, 2);

            // Prepare datas
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            int vertexCountEdgeMip = info.vertexCountEdge >> collisionLOD;
            int textureSizeMip = info.textureSize >> collisionLOD;

            var img = heightmap.GetImage();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(img.GetData());


            int heightMapLength = info.heightMapSize * info.heightMapSize;
            var heightField = new float[heightMapLength];

            for (int chunkZ = 0; chunkZ < Terrain3D.CHUNKS_COUNT_EDGE; chunkZ++)
            {
                int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                int chunkStartZ = chunkZ * heightFieldChunkSize;

                for (int chunkX = 0; chunkX < Terrain3D.CHUNKS_COUNT_EDGE; chunkX++)
                {
                    int chunkTextureX = chunkX * vertexCountEdgeMip;
                    int chunkStartX = chunkX * heightFieldChunkSize;

                    for (int z = 0; z < vertexCountEdgeMip; z++)
                    {
                        for (int x = 0; x < vertexCountEdgeMip; x++)
                        {
                            int textureIndexZ = (chunkTextureZ + z) * textureSizeMip;
                            int textureIndexX = chunkTextureX + x;

                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;


                            float normalizedHeight = ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                            float height = (normalizedHeight * info.patchHeight) + info.patchOffset;
                            bool isHole = ReadIsHoleByte(imgRGBABuffer[textureIndex]);

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

        public float[] ModifyCollision(int collisionLod, Vector2i modifiedOffset, Vector2i modifiedSize, float[] heightFieldData) //modifing
        {
            var img = heightmap.GetImage();
            RGBA[] imgRGBABuffer = FromByteArray<RGBA>(img.GetData());

            // Prepare data
            Vector2 modifiedOffsetRatio = new Vector2((float)modifiedOffset.x / info.heightMapSize, (float)modifiedOffset.y / info.heightMapSize);
            Vector2 modifiedSizeRatio = new Vector2((float)modifiedSize.x / info.heightMapSize, (float)modifiedSize.y / info.heightMapSize);
            int collisionLOD = Math.Clamp(collisionLod, 0, 0); //from mip
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;

            Vector2i samplesOffset = new Vector2i(Mathf.FloorToInt(modifiedOffsetRatio.x * (float)heightFieldSize), Mathf.FloorToInt(modifiedOffsetRatio.y * (float)heightFieldSize));
            Vector2i samplesSize = new Vector2i(Mathf.CeilToInt(modifiedSizeRatio.x * (float)heightFieldSize), Mathf.CeilToInt(modifiedSizeRatio.y * (float)heightFieldSize));

            samplesSize.x = Math.Max(samplesSize.x, 1);
            samplesSize.y = Math.Max(samplesSize.y, 1);

            Vector2i samplesEnd = samplesOffset + samplesSize;
            samplesEnd.x = Math.Min(samplesEnd.x, heightFieldSize);
            samplesEnd.y = Math.Min(samplesEnd.y, heightFieldSize);

            // Setup terrain collision information
            //auto & mip = initData->Mips[collisionLOD];
            int vertexCountEdgeMip = info.vertexCountEdge >> collisionLOD;
            int textureSizeMip = info.textureSize >> collisionLOD;

            for (int chunkZ = 0; chunkZ < Terrain3D.CHUNKS_COUNT_EDGE; chunkZ++)
            {
                int chunkTextureZ = chunkZ * vertexCountEdgeMip;
                int chunkStartZ = chunkZ * heightFieldChunkSize;

                // Skip unmodified chunks
                if (chunkStartZ >= samplesEnd.y || chunkStartZ + vertexCountEdgeMip < samplesOffset.y)
                    continue;

                for (int chunkX = 0; chunkX < Terrain3D.CHUNKS_COUNT_EDGE; chunkX++)
                {
                    int chunkTextureX = chunkX * vertexCountEdgeMip;
                    int chunkStartX = chunkX * heightFieldChunkSize;

                    // Skip unmodified chunks
                    if (chunkStartX >= samplesEnd.x || chunkStartX + vertexCountEdgeMip < samplesOffset.x)
                        continue;

                    for (int z = 0; z < vertexCountEdgeMip; z++)
                    {
                        // Skip unmodified columns
                        int heightmapZ = chunkStartZ + z;
                        int heightmapLocalZ = heightmapZ - samplesOffset.y;
                        if (heightmapLocalZ < 0 || heightmapLocalZ >= samplesSize.y)
                            continue;

                        // TODO: adjust loop range to reduce iterations count for edge cases (skip checking unmodified samples)
                        for (int x = 0; x < vertexCountEdgeMip; x++)
                        {
                            // Skip unmodified rows
                            int heightmapX = chunkStartX + x;
                            int heightmapLocalX = heightmapX - samplesOffset.x;
                            if (heightmapLocalX < 0 || heightmapLocalX >= samplesSize.x)
                                continue;

                            int textureIndex = (chunkTextureZ + z) * textureSizeMip + chunkTextureX + x;


                            float normalizedHeight = ReadNormalizedHeightByte(imgRGBABuffer[textureIndex]);
                            float height = (normalizedHeight * info.patchHeight) + info.patchOffset;

                            bool isHole = ReadIsHoleByte(imgRGBABuffer[textureIndex]);

                            int dstIndex = (heightmapLocalX * samplesSize.y) + heightmapLocalZ;
                            //int dstIndex = heightmapLocalX + (heightmapLocalZ * samplesSize.y);

                            heightFieldData[dstIndex] = height;
                        }
                    }
                }
            }

            return heightFieldData;
        }

        public void CookModifyCollision(Terrain3D terrain, float[] heightMapData)
        {
            var start = OS.GetTicksMsec();

            int collisionLOD = 0;
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            //create heightmap shape
            var bound = getBounds();
            PhysicsServer3D.ShapeSetData(shapeRid, new Godot.Collections.Dictionary() {
                {
                    "width", (int)  Math.Sqrt(heightMapData.Length)
                },
                {
                    "depth",  (int) Math.Sqrt(heightMapData.Length)
                },
                {
                    "heights", heightMapData
                },
                {
                    "cell_size", 1.0f
                },
                {
                    "min_height", bound.Position.y
                },
                {
                    "max_height", bound.End.y
                }
            });

        }

        public void CookCollision(int collisionLOD, Terrain3D terrain)
        {
            var start = OS.GetTicksMsec();

            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            var heightField = getColliderLod(collisionLOD);

            //create heightmap shape
            shapeRid = PhysicsServer3D.HeightmapShapeCreate();
            PhysicsServer3D.BodyAddShape(bodyRid, shapeRid);
            PhysicsServer3D.ShapeSetData(shapeRid, new Godot.Collections.Dictionary() {
                {
                    "width", (int) Math.Sqrt(heightField.Length)
                },
                {
                    "depth", (int) Math.Sqrt(heightField.Length)
                },
                {
                    "heights", heightField
                },
                {
                    "cell_size", 1.0f
                }
            });

            PhysicsServer3D.BodySetCollisionLayer(bodyRid, terrain.collisionLayer);
            PhysicsServer3D.BodySetCollisionMask(bodyRid, terrain.collisionMask);

            updateColliderPosition(terrain);

            GD.Print("[Collider] Cooking time " + (OS.GetTicksMsec() - start) + " ms");
        }
    }


}
