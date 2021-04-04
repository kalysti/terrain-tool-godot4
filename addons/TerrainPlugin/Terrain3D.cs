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

        public AABB bounds = new AABB();

        public RID instanceRID;

        public Node root;

        public RID bodyRid;

        public void updateTransform()
        {
            foreach (var patch in terrainPatches)
                patch.UpdateTransform(this);
        }


        public bool RayCast(Vector3 origin, Vector3 direction, float resultHitDistance, float maxDistance)
        {
            /*
            float minDistance = float.MaxValue;
            bool result = false;
            var ray = new AABB(origin, direction);

            foreach (var patch in terrainPatches)
            {
                if (patch.HasCollision() &&
                    patch.bounds.Intersects(ray) &&
                    patch.RayCast(GetWorld3d().DirectSpaceState, direction, resultHitDistance, maxDistance) &&
                    resultHitDistance < minDistance)
                {
                    minDistance = resultHitDistance;
                    result = true;
                }
            }

            resultHitDistance = minDistance;
             return result;
            */

            return false;

        }

        public void Generate(int patchX, int patchY, int chunkSize, EditorInterface ef = null)
        {
            var start = OS.GetTicksMsec();
            Clear();
            terrainPatches.Clear();

            for (int x = 0; x < patchX; x++)
            {
                for (int y = 0; y < patchY; y++)
                {
                    var script = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatch.cs").New();
                    var patch = script as TerrainPatch;
                    patch.ResourceLocalToScene = true;
                    patch.patchCoord = new Vector2((float)x, (float)y);
                    terrainPatches.Add(patch);

                    //create chunks
                    patch.createHeightmap(chunkSize);
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
            GD.Print("Chunk cook time: " + (OS.GetTicksMsec() - start) + " ms");

            var x = bounds.Size.x * 0.00001f;
            GD.Print("Terrain Size:  " + x.ToString("n6") + "km");
        }

        public void updateDebug()
        {
            UpdateGizmo();
        }

        public override void _Notification(int what)
        {

            if (what == NotificationVisibilityChanged)
            {
                GD.Print("visible:" + IsVisibleInTree());
            }
            else if (what == NotificationExitWorld)
            {

                Clear();
                PhysicsServer3D.FreeRid(bodyRid);

            }
            else if (what == NotificationPredelete)
            {
                GD.Print("pre delete");
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
