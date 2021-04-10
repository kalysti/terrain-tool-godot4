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

        public void updateTransform()
        {
            foreach (var patch in terrainPatches)
                patch.UpdateTransform(this);
        }

        public void Generate(int patchX, int patchY, int chunkSize, float heightmapScale = 5000, Image heightMapImage = null, Image splatMapImage1 = null, Image splatMapImage2 = null)
        {
            ClearDraw();

            //delete prev patches
            terrainPatches.Clear();

            float size = (chunkSize - 1) * Terrain3D.TERRAIN_UNITS_PER_VERTEX * Terrain3D.CHUNKS_COUNT_EDGE;

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

            int patchId = 0;
            foreach (var patch in terrainPatches)
            {
                var start = OS.GetTicksMsec();
                GD.Print("[Generate Time] " + (OS.GetTicksMsec() - start) + " ms");

                if (heightMapImage == null)
                {
                    patch.Init(chunkSize);
                }
                else
                {
                    int heightmapSize = (chunkSize - 1) * Terrain3D.CHUNKS_COUNT_EDGE + 1;

                    float[] heightmapData = new float[heightmapSize * heightmapSize];

                    Color[] splatMapData1 = new Color[heightmapSize * heightmapSize];
                    Color[] splatMapData2 = new Color[heightmapSize * heightmapSize];

                    Vector2 uvPerPatch = Vector2.One / new Vector2(patchX, patchY);
                    float heightmapSizeInv = 1.0f / (heightmapSize - 1);

                    Vector2 uvStart = new Vector2(patch.patchCoord.x, patch.patchCoord.y) * uvPerPatch;
                    for (int z = 0; z < heightmapSize; z++)
                    {
                        for (int x = 0; x < heightmapSize; x++)
                        {
                            Vector2 uv = uvStart + new Vector2(x * heightmapSizeInv, z * heightmapSizeInv) * uvPerPatch;

                            if (heightMapImage != null)
                            {
                                Color raw = heightMapImage.GetPixel(x, z);

                                float normalizedHeight = ReadNormalizedHeight(raw);
                                heightmapData[z * heightmapSize + x] = normalizedHeight * heightmapScale;
                            }

                            if (splatMapImage1 != null)
                            {
                                Color raw = splatMapImage1.GetPixel(x, z);
                                splatMapData1[z * heightmapSize + x] = raw;
                            }

                            if (splatMapImage2 != null)
                            {
                                Color raw = splatMapImage2.GetPixel(x, z);
                                splatMapData2[z * heightmapSize + x] = raw;
                            }
                        }
                    }

                    patch.Init(chunkSize, heightmapData, splatMapData1, splatMapData2);
                }

                GD.Print("[Patch][" + patchId + "] Init time " + (OS.GetTicksMsec() - start) + " ms");
                patchId++;
            }


            Draw();

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


        protected void Draw()
        {
            int patchId = 0;
            foreach (var patch in terrainPatches)
            {
                var start = OS.GetTicksMsec();

                patch.Draw(this, terrainDefaultMaterial.Shader.GetRid());

                GD.Print("[Patch][" + patchId + "] Draw time " + (OS.GetTicksMsec() - start) + " ms");
                patchId++;
            }

            getBounds();
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
                ClearDraw();
            }
            else if (what == NotificationEnterWorld)
            {
                Draw();
            }
            else if (what == NotificationTransformChanged)
            {
                updateTransform();
            }
        }

        protected void ClearDraw()
        {
            GD.Print("Clearing");

            foreach (var patch in terrainPatches)
            {
                patch.ClearDraw();
            }
        }


        public void getBounds()
        {
            bounds = new AABB();
            foreach (var patch in terrainPatches)
            {
                bounds = bounds.Merge(patch.getBounds());
            }
        }

    }
}
