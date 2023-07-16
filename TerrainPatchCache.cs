using Godot;
using Godot.Collections;
using TerrainEditor.Generators;
using Array = System.Array;

namespace TerrainEditor;

public partial class TerrainPatch : Resource
{
    protected float[] CachedHeightMapData = Array.Empty<float>();

    protected byte[] CachedHolesMask = Array.Empty<byte>();

    protected Array<Color[]> CachedSplatMap = new();

    public float[] CacheHeightData()
    {
        if (CachedHeightMapData.Length <= 0)
        {
            var heightmapGen = new TerrainHeightMapGenerator(this);

            float[] heights = Array.Empty<float>();
            byte[] holes = Array.Empty<byte>();

            heightmapGen.CacheHeights(ref heights, ref holes);
            CachedHeightMapData = heights;
            CachedHolesMask = holes;
        }

        return CachedHeightMapData;
    }

    public byte[] CacheHoleMask()
    {
        if (CachedHolesMask.Length <= 0)
        {
            var heightmapGen = new TerrainHeightMapGenerator(this);
            float[] heights = Array.Empty<float>();
            byte[] holes = Array.Empty<byte>();

            heightmapGen.CacheHeights(ref heights, ref holes);
            CachedHolesMask = holes;
        }

        return CachedHolesMask;
    }

    public Color[] CacheSplatMap(int idx)
    {
        if (CachedSplatMap.Count < idx + 1)
            CachedSplatMap.Resize(idx + 1);

        if (CachedSplatMap.Count < idx || CachedSplatMap[idx] == null || CachedSplatMap[idx].Length <= 0)
        {
            var splatmapGen = new TerrainSplatMapGenerator(this);
            return splatmapGen.CacheSplatmap(idx) ?? Array.Empty<Color>();
        }

        return CachedSplatMap[idx] ?? Array.Empty<Color>();
    }
}