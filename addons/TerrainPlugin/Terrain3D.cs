using System.Xml.Schema;
using System.IO.Compression;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
    [Tool]
    public partial class Terrain3D : Node3D
    {
        public const int CHUNKS_COUNT = 16;
        public const int CHUNKS_COUNT_EDGE = 4;
        public const float TERRAIN_UNITS_PER_VERTEX = 100.0f;
        public const float COLLIDER_MULTIPLIER = 1000.0f;

        public AABB bounds = new AABB();

        public RID instanceRID;

        public Node root;

        public RID bodyRid;

        public void updateTransform()
        {
            foreach (var patch in terrainPatches)
                patch.UpdateTransform(this);
        }

        public void Generate(int patchX, int patchY, int chunkSize, float heightmapScale = 5000, Image heightMapImage = null)
        {
            if (bodyRid != null)
                PhysicsServer3D.BodyClearShapes(bodyRid);

            var start = OS.GetTicksMsec();
            Clear();
            terrainPatches.Clear();

            float size = (chunkSize - 1) * Terrain3D.TERRAIN_UNITS_PER_VERTEX * Terrain3D.CHUNKS_COUNT_EDGE;
            int heightmapSize = (chunkSize - 1) * Terrain3D.CHUNKS_COUNT_EDGE + 1;

            for (int x = 0; x < patchX; x++)
            {
                for (int y = 0; y < patchY; y++)
                {
                    var script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatch.cs").New();
                    var patch = script as TerrainPatch;
                    patch.offset = new Vector3(x * size, 0.0f, y * size);
                    patch.ResourceLocalToScene = true;
                    patch.patchCoord = new Vector2((float)x, (float)y);
                    terrainPatches.Add(patch);
                }
            }

            foreach (var patch in terrainPatches)
            {
                if (heightMapImage == null)
                {
                    patch.createEmptyHeightmap(chunkSize);
                }
                else
                {
                    float[] heightmapData = new float[heightmapSize * heightmapSize];

                    Vector2 uvPerPatch = Vector2.One / new Vector2(patchX, patchY);
                    float heightmapSizeInv = 1.0f / (heightmapSize - 1);
                    Vector2 uvStart = new Vector2(patch.patchCoord.x, patch.patchCoord.y) * uvPerPatch;
                    GD.Print("Load image from " + heightmapSize);
                    for (int z = 0; z < heightmapSize; z++)
                    {
                        for (int x = 0; x < heightmapSize; x++)
                        {
                            Vector2 uv = uvStart + new Vector2(x * heightmapSizeInv, z * heightmapSizeInv) * uvPerPatch;
                            Color raw = heightMapImage.GetPixel(x, z);

                            float normalizedHeight = patch.ReadNormalizedHeight(raw);
                            //bool isHole = patch.ReadIsHole(raw);
                            
                            heightmapData[z * heightmapSize + x] = normalizedHeight * heightmapScale;
                        }
                    }

                    patch.createEmptyHeightmap(chunkSize, heightmapData);
                }
            }


            var end = OS.GetTicksMsec();
            GD.Print("Loading time: " + (end - start) + " ms");

            Init();
        }

        public void PreInit()
        {
            bodyRid = PhysicsServer3D.BodyCreate();

            PhysicsServer3D.BodySetMode(bodyRid, PhysicsServer3D.BodyMode.Static);
            PhysicsServer3D.BodyAttachObjectInstanceId(bodyRid, GetInstanceId());
            PhysicsServer3D.BodySetSpace(bodyRid, GetWorld3d().Space);
        }

        public void Init()
        {
            var start = OS.GetTicksMsec();
            RID scenario = GetWorld3d().Scenario;
            foreach (var patch in terrainPatches)
            {
                patch.Draw(scenario, this, terrainDefaultMaterial.Shader.GetRid());
                patch.CookCollision(0, this);
            }

            updateBounds();
            updateDebug();
        }

        public void updateDebug()
        {
            UpdateGizmo();
        }

        public override void _Notification(int what)
        {

            if (what == NotificationVisibilityChanged)
            {
            }
            else if (what == NotificationExitWorld)
            {
                Clear();
                PhysicsServer3D.FreeRid(bodyRid);
            }
            else if (what == NotificationEnterWorld)
            {
                PreInit();
                Init();
            }
            else if (what == NotificationTransformChanged)
            {
                updateTransform();
            }
        }

        public void Clear()
        {
            GD.Print("Clearing");
            PhysicsServer3D.BodyClearShapes(bodyRid);

            foreach (var patch in terrainPatches)
            {
                patch.Clear();
            }
        }


        public void updateBounds()
        {
            bounds = new AABB();
            foreach (var patch in terrainPatches)
            {
                bounds = bounds.Merge(patch.getBounds());
            }
        }

    }
}
