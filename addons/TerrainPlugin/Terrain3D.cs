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

        public Node root;

        public void updateTransform()
        {
            foreach (var patch in terrainPatches)
                patch.UpdateTransform(this);
        }

        public void Generate(int patchX, int patchY, int chunkSize, float heightmapScale = 5000, Image heightMapImage = null)
        {
            Clear();
            terrainPatches.Clear();

            var start = OS.GetTicksMsec();

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
                    patch.patchCoord = new Vector2i(x, y);
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

                            float normalizedHeight = ReadNormalizedHeight(raw);
                            //bool isHole = patch.ReadIsHole(raw);

                            heightmapData[z * heightmapSize + x] = normalizedHeight * heightmapScale;
                        }
                    }

                    patch.createEmptyHeightmap(chunkSize, heightmapData);
                }
            }


            GD.Print("[Generate Time] " + (OS.GetTicksMsec() - start) + " ms");

            start = OS.GetTicksMsec();
            Init();
            GD.Print("[Init Time] " + (OS.GetTicksMsec() - start) + " ms");

            var kmX = bounds.Size.x * 0.00001f;
            var kmY = bounds.Size.x * 0.00001f;
            GD.Print("[Init Size] " + kmX + "x" + kmY + "km");
        }

        public float ReadNormalizedHeight(Color raw)
        {
            var test = raw.r8 | (raw.g8 << 8);
            UInt16 quantizedHeight = Convert.ToUInt16(test);

            float normalizedHeight = (float)quantizedHeight / UInt16.MaxValue;
            return normalizedHeight;
        }


        public void Init()
        {
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
            }
            else if (what == NotificationEnterWorld)
            {
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
