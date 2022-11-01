using Godot;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using TerrainEditor.Converters;

namespace TerrainEditor;

[Tool]
public partial class Terrain3D : Node3D
{
    //count of chunks per patch
    public const int PATCH_CHUNKS_AMOUNT = 16;

    //edges per chunk
    public const int PATCH_CHUNK_EDGES = 4;

    //units per vertex (also scale factor)
    public const float UNITS_PER_VERTEX = 100.0f;

    public Terrain3D() : base()
    {
        SetNotifyTransform(true);
    }

    public void UpdatePosition()
    {
        foreach (TerrainPatch? patch in TerrainPatches)
            patch?.UpdatePosition(this);
    }

    public void UpdateSettings()
    {
        foreach (TerrainPatch? patch in TerrainPatches)
            patch?.UpdateSettings(this);
    }

    private void CacheNeighbors()
    {
        foreach (TerrainPatch? patch in TerrainPatches)
        {
            for (var chunkIndex = 0; chunkIndex < PATCH_CHUNKS_AMOUNT; chunkIndex++)
            {
                patch?.Chunks[chunkIndex].CacheNeighbors(this, patch);
            }
        }
    }


    public TerrainPatch? GetPatch(int x, int z)
    {
        return TerrainPatches.FirstOrDefault(patch => patch != null && patch.PatchCoord.x == x && patch.PatchCoord.y == z);
    }

    public int GetPatchesCount()
    {
        return TerrainPatches.Count;
    }

    public TerrainPatch? GetPatch(int idx)
    {
        return TerrainPatches.Count >= idx ? TerrainPatches[idx] : null;
    }

    public static T[] FromByteArray<T>(byte[] source) where T : struct
    {
        var destination = new T[source.Length / Marshal.SizeOf(typeof(T))];
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

    /**
		 * Creating a patch grid
		 */
    public void CreatePatchGrid(int patchX, int patchY, int chunkSize)
    {
        ClearDraw();

        //delete prev patches
        TerrainPatches.Clear();

        for (var x = 0; x < patchX; x++)
        {
            for (var y = 0; y < patchY; y++)
            {
                CreatePatch(x, y, chunkSize);
            }
        }
    }

    /**
		 * Creating a patch by given coords and chunksize
		 */
    public void CreatePatch(Vector2i coord, int chunkSize)
    {
        CreatePatch(coord.x, coord.y, chunkSize);
    }

    /**
		* Creating a patch by given coords and chunksize
		*/
    public void CreatePatch(int x, int y, int chunkSize)
    {
        float size = (chunkSize - 1) * UNITS_PER_VERTEX * PATCH_CHUNK_EDGES;

        var cSharpScript = GD.Load<CSharpScript>("res://addons/TerrainPlugin/TerrainPatch.cs");
        Variant script = cSharpScript.New();

        if (script.Obj is TerrainPatch patch)
        {
            patch.Offset = new Vector3(x * size, 0.0f, y * size);
            patch.ResourceLocalToScene = true;
            patch.PatchCoord = new Vector2i(x, y);

            TerrainPatches.Add(patch);
            patch.Init(chunkSize);

            patch.CreateHeightmap();
            patch.CreateSplatmap(0);
            patch.CreateSplatmap(1);
        }
    }

    /**
		* Load heightmap from given image
		*/
    public Error LoadHeightmapFromImage(Vector2i patchCoord, Image heightMapImage, HeightmapAlgo algo = HeightmapAlgo.R16, float heightmapScale = 5000)
    {
        TerrainPatch? patch = GetPatch(patchCoord.x, patchCoord.y);
        if (patch == null)
        {
            return Error.FileNotFound;
        }

        switch (algo)
        {
            case HeightmapAlgo.R16 when heightMapImage.GetFormat() != Image.Format.L8:
                GD.PrintErr("The R16 Algorithm needs a 16bit Image with one channel (red).");
                return Error.FileCorrupt;
            case HeightmapAlgo.RGB8_FULL when heightMapImage.GetFormat() != Image.Format.Rgb8 && heightMapImage.GetFormat() != Image.Format.Rgba8:
                GD.PrintErr("The RGB8 Algorithm needs a 8bit RGB or RGBA Image.");
                return Error.FileCorrupt;
            case HeightmapAlgo.RGBA8_HALF or HeightmapAlgo.RGBA8_NORMAL when heightMapImage.GetFormat() != Image.Format.Rgba8:
                GD.PrintErr("The RGB8 Algorithm needs a 8bit RGBA Image.");
                return Error.FileCorrupt;
        }

        var heightmapData = new float[patch.Info.HeightMapSize * patch.Info.HeightMapSize];

        for (var z = 0; z < patch.Info.HeightMapSize; z++)
        {
            for (var x = 0; x < patch.Info.HeightMapSize; x++)
            {
                Color raw = heightMapImage.GetPixel(x, z);
                switch (algo)
                {
                    //my tool 
                    case HeightmapAlgo.RGBA8_HALF:
                    {
                        float normalizedHeight = TerrainByteConverter.ReadNormalizedHeight16Bit(raw);
                        heightmapData[z * patch.Info.HeightMapSize + x] = normalizedHeight * heightmapScale;
                        break;
                    }
                    //mapbox default
                    case HeightmapAlgo.RGB8_FULL:
                    {
                        float height = -10000f + ((raw.r8 * 256f * 256f + raw.g8 * 256f + raw.b8) * 0.1f);
                        float normalizedHeight = height / 50; //reduce because 24bit of mapbox

                        heightmapData[z * patch.Info.HeightMapSize + x] = normalizedHeight * heightmapScale;
                        break;
                    }
                    //industrial default
                    case HeightmapAlgo.R16:
                        heightmapData[z * patch.Info.HeightMapSize + x] = raw.r * heightmapScale;
                        break;
                    case HeightmapAlgo.RGBA8_NORMAL:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(algo), algo, null);
                }
            }
        }

        patch.CreateHeightmap(heightmapData);
        return Error.Ok;
    }

    /**
		* Load splatmap from given image
		*/
    public Error LoadSplatmapFromImage(Vector2i patchCoord, int idx, Image splatmapImage)
    {
        TerrainPatch? patch = GetPatch(patchCoord.x, patchCoord.y);
        if (patch == null)
        {
            return Error.FileNotFound;
        }

        var splatmapData = new Color[patch.Info.HeightMapSize * patch.Info.HeightMapSize];

        for (var z = 0; z < patch.Info.HeightMapSize; z++)
        {
            for (var x = 0; x < patch.Info.HeightMapSize; x++)
            {
                splatmapData[z * patch.Info.HeightMapSize + x] = splatmapImage.GetPixel(x, z);
            }
        }

        patch.CreateSplatmap(idx, splatmapData);
        return Error.Ok;
    }

    public Error Draw()
    {
        ClearDraw();

        if (TerrainPatches.Count <= 0)
        {
            return Error.FileNotFound;
        }

        CacheNeighbors();

        var patchId = 0;

        foreach (TerrainPatch? patch in TerrainPatches)
        {
            long start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (TerrainDefaultMaterial != null)
                patch?.Draw(this, TerrainDefaultMaterial);
            long end = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            GD.Print("[Patch][" + patchId + "] Draw time " + (end - start) + " ms");
            patchId++;
        }

        UpdateGizmos();


        float kmX = GetBounds().Size.x * 0.00001f;
        float kmY = GetBounds().Size.z * 0.00001f;

        GD.Print("[Draw Size] " + kmX + "x" + kmY + "km");

        return Error.Ok;
    }

    public override void _Notification(long what)
    {
        switch (what)
        {
            case NotificationExitWorld:
                ClearDraw();
                break;
            case NotificationEnterWorld:
                Draw();
                break;
            case NotificationTransformChanged:
                UpdatePosition();
                break;
            case NotificationVisibilityChanged:
                UpdateSettings();
                break;
        }
    }

    protected void ClearDraw()
    {
        foreach (TerrainPatch? patch in TerrainPatches)
        {
            patch?.ClearDraw();
        }
    }

    public AABB GetBounds()
    {
        var bounds = new AABB();
        return TerrainPatches.Aggregate(bounds, (current, patch) => patch != null ? current.Merge(patch.GetBounds()) : default);
    }
}