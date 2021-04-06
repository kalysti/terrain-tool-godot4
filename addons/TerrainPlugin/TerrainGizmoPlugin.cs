using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainGizmoPlugin : EditorNode3DGizmoPlugin
    {
        public TerrainGizmoPlugin() : base()
        {
            CreateMaterial("main", new Color(1, 0, 0));
            CreateHandleMaterial("handles");
        }
        public override bool HasGizmo(Node3D spatial)
        {
            return spatial is Terrain3D;
        }


        void GetEdge(int p_edge, Vector3 position, Vector3 size, ref Vector3 r_from, ref Vector3 r_to)
        {
            switch (p_edge)
            {
                case 0:
                    {
                        r_from = new Vector3(position.x + size.x, position.y, position.z);
                        r_to = new Vector3(position.x, position.y, position.z);
                    }
                    break;
                case 1:
                    {
                        r_from = new Vector3(position.x + size.x, position.y, position.z + size.z);
                        r_to = new Vector3(position.x + size.x, position.y, position.z);
                    }
                    break;
                case 2:
                    {
                        r_from = new Vector3(position.x, position.y, position.z + size.z);
                        r_to = new Vector3(position.x + size.x, position.y, position.z + size.z);

                    }
                    break;
                case 3:
                    {
                        r_from = new Vector3(position.x, position.y, position.z);
                        r_to = new Vector3(position.x, position.y, position.z + size.z);

                    }
                    break;
                case 4:
                    {
                        r_from = new Vector3(position.x, position.y + size.y, position.z);
                        r_to = new Vector3(position.x + size.x, position.y + size.y, position.z);
                    }
                    break;
                case 5:
                    {
                        r_from = new Vector3(position.x + size.x, position.y + size.y, position.z);
                        r_to = new Vector3(position.x + size.x, position.y + size.y, position.z + size.z);
                    }
                    break;
                case 6:
                    {
                        r_from = new Vector3(position.x + size.x, position.y + size.y, position.z + size.z);
                        r_to = new Vector3(position.x, position.y + size.y, position.z + size.z);

                    }
                    break;
                case 7:
                    {
                        r_from = new Vector3(position.x, position.y + size.y, position.z + size.z);
                        r_to = new Vector3(position.x, position.y + size.y, position.z);

                    }
                    break;
                case 8:
                    {
                        r_from = new Vector3(position.x, position.y, position.z + size.z);
                        r_to = new Vector3(position.x, position.y + size.y, position.z + size.z);

                    }
                    break;
                case 9:
                    {
                        r_from = new Vector3(position.x, position.y, position.z);
                        r_to = new Vector3(position.x, position.y + size.y, position.z);

                    }
                    break;
                case 10:
                    {
                        r_from = new Vector3(position.x + size.x, position.y, position.z);
                        r_to = new Vector3(position.x + size.x, position.y + size.y, position.z);

                    }
                    break;
                case 11:
                    {
                        r_from = new Vector3(position.x + size.x, position.y, position.z + size.z);
                        r_to = new Vector3(position.x + size.x, position.y + size.y, position.z + size.z);

                    }
                    break;
            }
        }


        public override void Redraw(EditorNode3DGizmo gizmo)
        {
            gizmo.Clear();
            var spatial = gizmo.GetSpatialNode() as Terrain3D;
            SurfaceTool st = new SurfaceTool();

            foreach (var patch in spatial.terrainPatches)
            {
                var lines = new Godot.Collections.Array<Vector3>();
                var aabb = patch.getBounds();

                for (int i = 0; i < 8; i++)
                {

                    Vector3 a, b;
                    a = new Vector3();
                    b = new Vector3();

                    GetEdge(i, aabb.Position, aabb.Size, ref a, ref b);

                    lines.Add(a);
                    lines.Add(b);
                }

                gizmo.AddLines(lines.ToArray(), GetMaterial("main", gizmo));
            }
        }

        ArrayMesh get_debug_mesh(Godot.Collections.Array<Vector3> lines)
        {
            var mt = new ArrayMesh();
            var arr = new Godot.Collections.Array();
            arr.Resize((int)ArrayMesh.ArrayType.Max);
            arr[(int)ArrayMesh.ArrayType.Vertex] = lines.ToArray();
            mt.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Lines, arr);

            return mt;
        }
    }
}