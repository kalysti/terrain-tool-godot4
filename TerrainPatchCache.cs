using Godot;
using TerrainEditor.Generators;

namespace TerrainEditor;

public partial class TerrainPatch : Resource
{
	protected float[] CachedHeightMapData;

	protected byte[] CachedHolesMask;

	protected Godot.Collections.Array<Color[]> CachedSplatMap;

	public float[] CacheHeightData()
	{
		if (CachedHeightMapData == null || CachedHeightMapData.Length <= 0)
		{
			var heightmapGen = new TerrainHeightMapGenerator(this);

			var heights = new float[0];
			var holes = new byte[0];

			heightmapGen.CacheHeights(ref heights, ref holes);
			CachedHeightMapData = heights;
			CachedHolesMask = holes;
		}

		return CachedHeightMapData;
	}
	public byte[] CacheHoleMask()
	{
		if (CachedHolesMask == null || CachedHolesMask.Length <= 0)
		{
			var heightmapGen = new TerrainHeightMapGenerator(this);
			var heights = new float[0];
			var holes = new byte[0];

			heightmapGen.CacheHeights(ref heights, ref holes);
			CachedHolesMask = holes;
		}

		return CachedHolesMask;
	}

	public Color[] CacheSplatMap(int idx)
	{
		if (CachedSplatMap == null || CachedSplatMap.Count < (idx + 1))
		{
			CachedSplatMap.Resize(idx +1);
		}

		if (CachedSplatMap.Count < idx || CachedSplatMap[idx] == null || CachedSplatMap[idx].Length <= 0)
		{
			var splatmapGen = new TerrainSplatMapGenerator(this);
			return splatmapGen.CacheSplatmap(idx);
		}
		else
			return CachedSplatMap[idx];
	}
}