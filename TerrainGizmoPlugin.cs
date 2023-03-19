using Godot;
using System;
using System.Linq;

namespace TerrainEditor
{
#if TOOLS
    [Tool]
    public partial class TerrainGizmoPlugin : EditorNode3DGizmoPlugin
    {
        public bool showCollider = false;
        public bool showAabb = false;
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
                        r_from = new Vector3(position.X + size.X, position.Y, position.Z);
                        r_to = new Vector3(position.X, position.Y, position.Z);
                    }
                    break;
                case 1:
                    {
                        r_from = new Vector3(position.X + size.X, position.Y, position.Z + size.Z);
                        r_to = new Vector3(position.X + size.X, position.Y, position.Z);
                    }
                    break;
                case 2:
                    {
                        r_from = new Vector3(position.X, position.Y, position.Z + size.Z);
                        r_to = new Vector3(position.X + size.X, position.Y, position.Z + size.Z);

                    }
                    break;
                case 3:
                    {
                        r_from = new Vector3(position.X, position.Y, position.Z);
                        r_to = new Vector3(position.X, position.Y, position.Z + size.Z);

                    }
                    break;
                case 4:
                    {
                        r_from = new Vector3(position.X, position.Y + size.Y, position.Z);
                        r_to = new Vector3(position.X + size.X, position.Y + size.Y, position.Z);
                    }
                    break;
                case 5:
                    {
                        r_from = new Vector3(position.X + size.X, position.Y + size.Y, position.Z);
                        r_to = new Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                    }
                    break;
                case 6:
                    {
                        r_from = new Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                        r_to = new Vector3(position.X, position.Y + size.Y, position.Z + size.Z);

                    }
                    break;
                case 7:
                    {
                        r_from = new Vector3(position.X, position.Y + size.Y, position.Z + size.Z);
                        r_to = new Vector3(position.X, position.Y + size.Y, position.Z);

                    }
                    break;
                case 8:
                    {
                        r_from = new Vector3(position.X, position.Y, position.Z + size.Z);
                        r_to = new Vector3(position.X, position.Y + size.Y, position.Z + size.Z);

                    }
                    break;
                case 9:
                    {
                        r_from = new Vector3(position.X, position.Y, position.Z);
                        r_to = new Vector3(position.X, position.Y + size.Y, position.Z);

                    }
                    break;
                case 10:
                    {
                        r_from = new Vector3(position.X + size.X, position.Y, position.Z);
                        r_to = new Vector3(position.X + size.X, position.Y + size.Y, position.Z);

                    }
                    break;
                case 11:
                    {
                        r_from = new Vector3(position.X + size.X, position.Y, position.Z + size.Z);
                        r_to = new Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z);

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

            line_material.AlbedoTextureForceSrgb = true;
            line_material.VertexColorIsSrgb = true;
            line_material.VertexColorUseAsAlbedo = true;

            return line_material;
        }

        public ArrayMesh GetDebugMesh(Vector3[] lines)
        {
            var debug_mesh_cache = new ArrayMesh();

            Godot.Collections.Array arr = new Godot.Collections.Array();
            arr.Resize((int)Mesh.ArrayType.Max);
            arr[(int)Mesh.ArrayType.Vertex] = Variant.CreateFrom(lines);

            debug_mesh_cache.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arr);
            debug_mesh_cache.SurfaceSetMaterial(0, getDebugMaterial());

            return debug_mesh_cache;
        }


        public Vector3[] GetDebugMeshLines(TerrainPatch patch)
        {
            float[]? heightField = patch.CacheHeightData();
            int map_width = patch.info.heightMapSize;
            int map_depth = patch.info.heightMapSize;

            Vector3[]? points = Array.Empty<Vector3>();
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
                    Vector3 height = new Vector3(start.X, 0.0f, start.Y);

                    for (int w = 0; w < map_width; w++)
                    {
                        height.Y = heightField[r_offset++];

                        if (w != map_width - 1)
                        {
                            points[w_offset++] = height;
                            points[w_offset++] = new Vector3(height.X + 1.0f, heightField[r_offset], height.Z);
                        }

                        if (d != map_depth - 1)
                        {
                            points[w_offset++] = height;
                            points[w_offset++] = new Vector3(height.X, heightField[r_offset + map_width - 1], height.Z + 1.0f);
                        }

                        height.X += 1.0f;
                    }

                    start.Y += 1.0f;
                }
            }

            return points;
        }


        public override void _Redraw(EditorNode3DGizmo gizmo)
        {
            gizmo.Clear();
            var spatial = gizmo.GetNode3D() as Terrain3D;

            if (spatial == null || spatial.Visible == false || !spatial.IsInsideTree())
                return;

            foreach (TerrainPatch? patch in spatial.terrainPatches)
            {

                if (showAabb)
                {
                    var lines = new Godot.Collections.Array<Vector3>();
                    Aabb Aabb = patch.getBounds();

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 a, b;
                        a = new Vector3();
                        b = new Vector3();

                        GetEdge(i, Aabb.Position, Aabb.Size, ref a, ref b);

                        lines.Add(a);
                        lines.Add(b);
                    }

                    gizmo.AddLines(lines.ToArray(), GetMaterial("main", gizmo));
                }

                if (showCollider)
                {
                    Vector3[]? meshLines = GetDebugMeshLines(patch);

                    SurfaceTool st = new SurfaceTool();
                    Transform3D tf = patch.GetColliderPosition(spatial, false); //todo: fix scaling gizmo
                    tf.Origin = tf.Origin - spatial.GlobalTransform.Origin;
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
            arr[(int)ArrayMesh.ArrayType.Vertex] = lines;
            mt.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Lines, arr);

            return mt;
        }
    }
#endif
}
