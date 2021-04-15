using System.Runtime.InteropServices;
using Godot;
using System;
using TerrainEditor.Generators;

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

        public ArrayMesh lod0Mesh = new ArrayMesh();

        [Export]
        public Godot.Collections.Array<TerrainChunk> chunks = new Godot.Collections.Array<TerrainChunk>();

        [Export]
        public ImageTexture heightmap;

        [Export]
        public Godot.Collections.Array<ImageTexture> splatmaps = new Godot.Collections.Array<ImageTexture>();

        public AABB bounds = new AABB();

        protected RID shapeRid;

        protected RID bodyRid;

        [Export]
        public TerrainPatchInfo info = new TerrainPatchInfo();


        /**
         * Clear rendering device by removing body and collider
         */
        public void ClearDraw()
        {
            cachedHeightMapData = null;

            foreach (var chunk in chunks)
            {
                chunk.ClearDraw();
            }

            if (shapeRid != null)
            {
                PhysicsServer3D.FreeRid(shapeRid);
                shapeRid = null;
            }

            if (bodyRid != null)
            {
                PhysicsServer3D.BodyClearShapes(bodyRid);
                bodyRid = null;
            }

        }

        /**
         * Creating a patch by given chunksize and optional exist heightmap datas
         */
        public void Init(int _chunkSize, float[] heightMapdata = null, Color[] splatMapData1 = null, Color[] splatMapData2 = null)
        {
            updateInfo(_chunkSize);
            CreateChunks();

            var heightmapGen = new TerrainHeightMapGenerator(this);
            var splatmapGen = new TerrainSplatMapGenerator(this);
            var image = heightmapGen.createImage();

            if (heightMapdata == null)
            {
                heightMapdata = new float[info.heightMapSize * info.heightMapSize];
            }

            float[] chunkOffsets = new float[Terrain3D.CHUNKS_COUNT];
            float[] chunkHeights = new float[Terrain3D.CHUNKS_COUNT];

            var result = heightmapGen.CalculateHeightRange(ref heightMapdata, ref chunkOffsets, ref chunkHeights);

            info.patchOffset = result.x;
            info.patchHeight = result.y;

            int chunkIndex = 0;
            foreach (var chunk in chunks)
            {
                chunk.offset = chunkOffsets[chunkIndex];
                chunk.height = chunkHeights[chunkIndex];
                chunk.UpdateHeightmap(info);
                chunkIndex++;
            }

            heightmapGen.WriteHeights(ref image, ref heightMapdata);
            heightmapGen.WriteNormals(ref image, heightMapdata, null, Vector2i.Zero, new Vector2i(info.heightMapSize, info.heightMapSize));

            if (heightmap == null)
            {
                heightmap = new ImageTexture();
                heightmap.CreateFromImage(image);
            }
            else
            {
                heightmap.Update(image);
            }

            var splatmap1 = new ImageTexture();
            var splatmap2 = new ImageTexture();
            var firstSplatmap = splatmapGen.createImage();
            var secondSplatmap = splatmapGen.createImage();

            if (splatMapData1 == null)
            {
                firstSplatmap.Fill(new Color(1, 0, 0, 0));
            }
            else
            {
                splatmapGen.WriteColors(ref firstSplatmap, ref splatMapData1);
            }

            if (splatMapData2 == null)
            {
                secondSplatmap.Fill(new Color(0, 0, 0, 0));
            }
            else
            {
                splatmapGen.WriteColors(ref secondSplatmap, ref splatMapData2);
            }

            splatmap1.CreateFromImage(firstSplatmap);
            splatmap2.CreateFromImage(secondSplatmap);

            splatmaps.Add(splatmap1);
            splatmaps.Add(splatmap2);

            cachedHeightMapData = heightMapdata;
            cachedHeightMapData = heightMapdata;
        }

        /**
         * Drawing a patch by attaching chunks to render device
         */
        public void Draw(Terrain3D terrainNode, Material mat)
        {
            RID scenario = terrainNode.GetWorld3d().Scenario;

            //clear chunks scene
            foreach (var chunk in chunks)
            {
                chunk.ClearDraw();
            }

            bodyRid = PhysicsServer3D.BodyCreate();

            PhysicsServer3D.BodySetMode(bodyRid, PhysicsServer3D.BodyMode.Static);
            PhysicsServer3D.BodyAttachObjectInstanceId(bodyRid, terrainNode.GetInstanceId());
            PhysicsServer3D.BodySetSpace(bodyRid, terrainNode.GetWorld3d().Space);
            PhysicsServer3D.BodySetCollisionLayer(bodyRid, terrainNode.collisionLayer);
            PhysicsServer3D.BodySetCollisionMask(bodyRid, terrainNode.collisionMask);

            //create cache
            int chunkId = 0;
            foreach (var chunk in chunks)
            {
                var start = OS.GetTicksMsec();

                chunk.Draw(this, info, scenario, ref heightmap, ref splatmaps, terrainNode, getOffset(), mat);
                chunk.UpdateTransform(info, terrainNode.GlobalTransform, getOffset());
                chunk.SetDefaultMaterial(terrainNode.terrainDefaultTexture);

                GD.Print("[Chunk][" + chunkId + "] Draw time " + (OS.GetTicksMsec() - start) + "ms");
                chunkId++;
            }

            //Creating collider
            CreateCollider(0, terrainNode);


            terrainNode.getBounds();
            terrainNode.updateDebug();

            UpdateTransform(terrainNode);
        }

        
       public void UpdateSettings(Terrain3D terrainNode)
        {
            foreach (var chunk in chunks)
            {
                chunk.UpdateSettings(terrainNode);
            }

            
        }
        public void UpdateTransform(Terrain3D terrainNode)
        {
            foreach (var chunk in chunks)
            {
                chunk.UpdateTransform(info, terrainNode.GlobalTransform, getOffset());
            }

            UpdateColliderPosition(terrainNode);
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

        /** 
         *  Updating heightmap texture, collider and rendering device
         */
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

            var heightmapGen = new TerrainHeightMapGenerator(this);
            var result = heightmapGen.CalculateHeightRange(ref data, ref chunkOffsets, ref chunkHeights);

            info.patchOffset = result.x;
            info.patchHeight = result.y;

            int chunkIndex = 0;
            foreach (var chunk in chunks)
            {
                chunk.offset = chunkOffsets[chunkIndex];
                chunk.height = chunkHeights[chunkIndex];
                chunk.UpdateHeightmap(info);
                chunkIndex++;
            }

            heightmapGen.WriteHeights(ref image, ref data);
            heightmapGen.WriteNormals(ref image, data, null, modifiedOffset, modifiedSize);

            terrain.getBounds();
            terrain.updateDebug();

            var genCollider = new TerrainColliderGenerator(this);
            var newColliderData = genCollider.ModifyCollision(0, modifiedOffset, modifiedSize, data);

            UpdateColliderData(terrain, newColliderData);

            heightmap.Update(image, true);
            UpdateTransform(terrain);

            cachedHeightMapData = data;
        }


        /** 
         *  Updating splatmap texture, collider and rendering device
         */
        public void UpdateSplatMap(int splatmapIndex, Terrain3D terrain, Color[] samples, Vector2i modifiedOffset, Vector2i modifiedSize)
        {
            var image = splatmaps[splatmapIndex].GetImage().Duplicate() as Image;
            var data = CacheSplatMap(splatmapIndex);

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

            var splatMapGen = new TerrainSplatMapGenerator(this);
            splatMapGen.WriteColors(ref image, ref data);

            splatmaps[splatmapIndex].Update(image, true);
            cachedSplatMap[splatmapIndex] = data;
        }

        public AABB getBounds()
        {
            var patchoffset = getOffset();

            int i = 0;
            foreach (var chunk in chunks)
            {
                var newBounds = chunk.getBounds(info, patchoffset);

                if (i == 0)
                    bounds = newBounds;
                else
                    bounds = bounds.Merge(newBounds);

                i++;
            }

            return bounds;
        }

        public Vector3 getOffset()
        {
            float size = info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX * Terrain3D.CHUNKS_COUNT_EDGE;
            return new Vector3(patchCoord.x * size, 0.0f, patchCoord.y * size);
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


        protected float[] cachedHeightMapData;
        protected Godot.Collections.Array<Color[]> cachedSplatMap;

        public float[] CacheHeightData()
        {
            if (cachedHeightMapData == null || cachedHeightMapData.Length <= 0)
            {
                var heightmapGen = new TerrainHeightMapGenerator(this);
                cachedHeightMapData = heightmapGen.CacheHeightmap();
            }

            return cachedHeightMapData;
        }

        public Color[] CacheSplatMap(int id)
        {
            if (cachedSplatMap == null || cachedSplatMap.Count != 2)
            {
                cachedSplatMap = new Godot.Collections.Array<Color[]>();
                cachedSplatMap.Resize(2);
            }

            if (cachedSplatMap.Count < id || cachedSplatMap[id] == null || cachedSplatMap[id].Length <= 0)
            {
                var splatmapGen = new TerrainSplatMapGenerator(this);
                return splatmapGen.CacheSplatmap(id);
            }
            else
                return cachedSplatMap[id];
        }

        private void UpdateColliderData(Terrain3D terrain, float[] heightMapData)
        {
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
                }
            });

        }

        private void UpdateColliderPosition(Terrain3D terrain)
        {
            int collisionLOD = 1;
            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            var scale2 = new Vector3((float)info.heightMapSize / heightFieldSize, 1, (float)info.heightMapSize / heightFieldSize);
            var scaleFac = new Vector3(scale2.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX, scale2.x, scale2.x * Terrain3D.TERRAIN_UNITS_PER_VERTEX);

            var transform = new Transform();
            transform.origin = terrain.GlobalTransform.origin + new Vector3(chunks[0].TerrainChunkSizeLOD0 * 2, 0, chunks[0].TerrainChunkSizeLOD0 * 2) + offset;
            transform.basis = terrain.GlobalTransform.basis;
            var scale = transform.basis.Scale;
            scale.x *= Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            scale.z *= Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            transform.basis.Scale = scale;
            PhysicsServer3D.BodySetState(bodyRid, PhysicsServer3D.BodyState.Transform, transform);
        }

        private void CreateCollider(int collisionLOD, Terrain3D terrain)
        {

            var start = OS.GetTicksMsec();

            int heightFieldChunkSize = ((info.chunkSize + 1) >> collisionLOD) - 1;
            int heightFieldSize = heightFieldChunkSize * Terrain3D.CHUNKS_COUNT_EDGE + 1;
            int heightFieldLength = heightFieldSize * heightFieldSize;

            var genCollider = new TerrainColliderGenerator(this);
            var heightField = genCollider.GenereateLOD(collisionLOD);


            //create heightmap shape
            shapeRid = PhysicsServer3D.HeightmapShapeCreate();
            PhysicsServer3D.BodyAddShape(bodyRid, shapeRid);

            UpdateColliderData(terrain, heightField);
            UpdateColliderPosition(terrain);
            GD.Print("[Collider] Creation time " + (OS.GetTicksMsec() - start) + " ms");
        }

        private void CreateChunks()
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

            lod0Mesh = chunks[0].GenerateMesh(this, info.chunkSize, 0);
        }
    }


}
