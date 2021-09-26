using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainGizmoPlugin : EditorNode3DGizmoPlugin
    {
        public bool showCollider = false;
        public bool showAABB = false;
        public TerrainGizmoPlugin() : base()
        {
            CreateMaterial("main", new Color(1, 0, 0));
            CreateMaterial("collider", new Color(0, 1, 0));
            CreateHandleMaterial("handles");

            Color gizmo_color = new Color(0.5f, 0.7f, 1);
            CreateMaterial("shape_material", gizmo_color);
        }
        public override bool _HasGizmo(Node3D spatial)
        {
            return spatial is Terrain3D;
        }
        public override string _GetGizmoName()
        {
            return "TerrainGizmo";
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

        private StandardMaterial3D getDebugMaterial()
        {
            StandardMaterial3D line_material = new StandardMaterial3D();
            line_material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
            line_material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            line_material.AlbedoColor = new Color(0, 1, 1, 1);

            line_material.AlbedoTexForceSrgb = true;
            line_material.VertexColorIsSrgb = true;
            line_material.VertexColorUseAsAlbedo = true;

            return line_material;
        }

        public ArrayMesh GetDebugMesh(Vector3[] lines)
        {
            var debug_mesh_cache = new ArrayMesh();

            Godot.Collections.Array arr = new Godot.Collections.Array();
            arr.Resize((int)Mesh.ArrayType.Max);
            arr[(int)Mesh.ArrayType.Vertex] = lines;

            debug_mesh_cache.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arr);
            debug_mesh_cache.SurfaceSetMaterial(0, getDebugMaterial());

            return debug_mesh_cache;
        }


        public Vector3[] GetDebugMeshLines(TerrainPatch patch)
        {
            var heightField = patch.CacheHeightData();
            var map_width = patch.info.heightMapSize;
            var map_depth = patch.info.heightMapSize;

            var points = new Vector3[0];
            if ((map_width != 0) && (map_depth != 0))
            {
                // This will be slow for large maps...
                // also we'll have to figure out how well bullet centers this shape...

                Vector2 size = new Vector2(map_width - 1, map_depth - 1);
                Vector2 start = size * -0.5f;

                // reserve some memory for our points..
                points = new Vector3[((map_width - 1) * map_depth * 2) + (map_width * (map_depth - 1) * 2)];

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


        public override void _Redraw(EditorNode3DGizmo gizmo)
        {
            gizmo.Clear();
            var spatial = gizmo.GetSpatialNode() as Terrain3D;

            if (spatial == null || spatial.Visible == false || !spatial.IsInsideTree())
                return;

            foreach (var patch in spatial.terrainPatches)
            {

                if (showAABB)
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

                if (showCollider)
                {
                    var meshLines = GetDebugMeshLines(patch);

                    SurfaceTool st = new SurfaceTool();
                    var tf = patch.GetColliderPosition(spatial, false); //todo: fix scaling gizmo
                    tf.origin = tf.origin - spatial.GlobalTransform.origin;
                    st.AppendFrom(GetDebugMesh(meshLines), 0, tf);
                    gizmo.AddMesh(st.Commit(), GetMaterial("shape_material", gizmo));
                }
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
