using Godot;
using System;
using System.Linq;

namespace TerrainEditor;
#if TOOLS
[Tool]
public partial class TerrainGizmoPlugin : EditorNode3DGizmoPlugin
{
    public bool ShowCollider = false;
    public bool ShowAabb = false;

    public TerrainGizmoPlugin()
    {
        CreateMaterial("main", new Color(1, 0, 0));
        CreateMaterial("collider", new Color(0, 1, 0));
        CreateHandleMaterial("handles");

        var gizmoColor = new Color(0.5f, 0.7f, 1);
        CreateMaterial("shape_material", gizmoColor);
    }

    public override bool _HasGizmo(Node3D spatial)
    {
        return spatial is Terrain3D;
    }

    public override string _GetGizmoName()
    {
        return "TerrainGizmo";
    }

    public static void GetEdge(int pEdge, Vector3 position, Vector3 size, ref Vector3 rFrom, ref Vector3 rTo)
    {
        switch (pEdge)
        {
            case 0:
            {
                rFrom = new Vector3(position.X + size.X, position.Y, position.Z);
                rTo = new Vector3(position.X, position.Y, position.Z);
            }
                break;
            case 1:
            {
                rFrom = new Vector3(position.X + size.X, position.Y, position.Z + size.Z);
                rTo = new Vector3(position.X + size.X, position.Y, position.Z);
            }
                break;
            case 2:
            {
                rFrom = new Vector3(position.X, position.Y, position.Z + size.Z);
                rTo = new Vector3(position.X + size.X, position.Y, position.Z + size.Z);
            }
                break;
            case 3:
            {
                rFrom = new Vector3(position.X, position.Y, position.Z);
                rTo = new Vector3(position.X, position.Y, position.Z + size.Z);
            }
                break;
            case 4:
            {
                rFrom = new Vector3(position.X, position.Y + size.Y, position.Z);
                rTo = new Vector3(position.X + size.X, position.Y + size.Y, position.Z);
            }
                break;
            case 5:
            {
                rFrom = new Vector3(position.X + size.X, position.Y + size.Y, position.Z);
                rTo = new Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
            }
                break;
            case 6:
            {
                rFrom = new Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                rTo = new Vector3(position.X, position.Y + size.Y, position.Z + size.Z);
            }
                break;
            case 7:
            {
                rFrom = new Vector3(position.X, position.Y + size.Y, position.Z + size.Z);
                rTo = new Vector3(position.X, position.Y + size.Y, position.Z);
            }
                break;
            case 8:
            {
                rFrom = new Vector3(position.X, position.Y, position.Z + size.Z);
                rTo = new Vector3(position.X, position.Y + size.Y, position.Z + size.Z);
            }
                break;
            case 9:
            {
                rFrom = new Vector3(position.X, position.Y, position.Z);
                rTo = new Vector3(position.X, position.Y + size.Y, position.Z);
            }
                break;
            case 10:
            {
                rFrom = new Vector3(position.X + size.X, position.Y, position.Z);
                rTo = new Vector3(position.X + size.X, position.Y + size.Y, position.Z);
            }
                break;
            case 11:
            {
                rFrom = new Vector3(position.X + size.X, position.Y, position.Z + size.Z);
                rTo = new Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
            }
                break;
        }
    }

    private static StandardMaterial3D GetDebugMaterial()
    {
        var lineMaterial = new StandardMaterial3D();
        lineMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        lineMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        lineMaterial.AlbedoColor = new Color(0, 1, 1);

        lineMaterial.AlbedoTextureForceSrgb = true;
        lineMaterial.VertexColorIsSrgb = true;
        lineMaterial.VertexColorUseAsAlbedo = true;

        return lineMaterial;
    }

    public static ArrayMesh GetDebugMesh(Vector3[] lines)
    {
        var debugMeshCache = new ArrayMesh();

        var arr = new Godot.Collections.Array();
        arr.Resize((int)Mesh.ArrayType.Max);
        arr[(int)Mesh.ArrayType.Vertex] = Variant.CreateFrom(lines);

        debugMeshCache.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arr);
        debugMeshCache.SurfaceSetMaterial(0, GetDebugMaterial());

        return debugMeshCache;
    }


    public static Vector3[] GetDebugMeshLines(TerrainPatch patch)
    {
        float[] heightField = patch.CacheHeightData();
        int mapWidth = patch.Info.HeightMapSize;
        int mapDepth = patch.Info.HeightMapSize;

        Vector3[] points = Array.Empty<Vector3>();
        if (mapWidth != 0 && mapDepth != 0)
        {
            // This will be slow for large maps...
            // also we'll have to figure out how well bullet centers this shape...

            var size = new Vector2(mapWidth - 1, mapDepth - 1);
            Vector2 start = size * -0.5f;

            // reserve some memory for our points..
            points = new Vector3[(mapWidth - 1) * mapDepth * 2 + mapWidth * (mapDepth - 1) * 2];

            // now set our points
            var rOffset = 0;
            var wOffset = 0;

            for (var d = 0; d < mapDepth; d++)
            {
                var height = new Vector3(start.X, 0.0f, start.Y);

                for (var w = 0; w < mapWidth; w++)
                {
                    height.Y = heightField[rOffset++];

                    if (w != mapWidth - 1)
                    {
                        points[wOffset++] = height;
                        points[wOffset++] = new Vector3(height.X + 1.0f, heightField[rOffset], height.Z);
                    }

                    if (d != mapDepth - 1)
                    {
                        points[wOffset++] = height;
                        points[wOffset++] = new Vector3(height.X, heightField[rOffset + mapWidth - 1], height.Z + 1.0f);
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

        if (gizmo.GetNode3D() is not Terrain3D spatial || spatial.Visible == false || !spatial.IsInsideTree())
            return;

        foreach (TerrainPatch? patch in spatial.TerrainPatches)
        {
            if (ShowAabb)
            {
                var lines = new Godot.Collections.Array<Vector3>();
                Aabb Aabb = patch.GetBounds();

                for (var i = 0; i < 8; i++)
                {
                    var a = new Vector3();
                    var b = new Vector3();

                    GetEdge(i, Aabb.Position, Aabb.Size, ref a, ref b);

                    lines.Add(a);
                    lines.Add(b);
                }

                gizmo.AddLines(lines.ToArray(), GetMaterial("main", gizmo));
            }

            if (ShowCollider)
            {
                Vector3[] meshLines = GetDebugMeshLines(patch);

                var st = new SurfaceTool();
                Transform3D tf = patch.GetColliderPosition(spatial, false); //todo: fix scaling gizmo
                tf.Origin -= spatial.GlobalTransform.Origin;
                st.AppendFrom(GetDebugMesh(meshLines), 0, tf);
                gizmo.AddMesh(st.Commit(), GetMaterial("shape_material", gizmo));
            }
        }
    }


    private static ArrayMesh get_debug_mesh(Godot.Collections.Array<Vector3> lines)
    {
        var mt = new ArrayMesh();
        var arr = new Godot.Collections.Array();
        arr.Resize((int)Mesh.ArrayType.Max);
        arr[(int)Mesh.ArrayType.Vertex] = lines;
        mt.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arr);

        return mt;
    }
}
#endif