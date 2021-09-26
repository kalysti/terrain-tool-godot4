using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using System;
using TerrainEditor.Generators;
using System.Linq;

namespace TerrainEditor
{
	public partial class TerrainPatch : Resource
	{
		protected float[] cachedHeightMapData;

		protected byte[] cachedHolesMask;

		protected Godot.Collections.Array<Color[]> cachedSplatMap;

		public float[] CacheHeightData()
		{
			if (cachedHeightMapData == null || cachedHeightMapData.Length <= 0)
			{
				var heightmapGen = new TerrainHeightMapGenerator(this);

				var heights = new float[0];
				var holes = new byte[0];

				heightmapGen.CacheHeights(ref heights, ref holes);
				cachedHeightMapData = heights;
				cachedHolesMask = holes;
			}

			return cachedHeightMapData;
		}
		public byte[] CacheHoleMask()
		{
			if (cachedHolesMask == null || cachedHolesMask.Length <= 0)
			{
				var heightmapGen = new TerrainHeightMapGenerator(this);
				var heights = new float[0];
				var holes = new byte[0];

				heightmapGen.CacheHeights(ref heights, ref holes);
				cachedHolesMask = holes;
			}

			return cachedHolesMask;
		}

		public Color[] CacheSplatMap(int idx)
		{
			if (cachedSplatMap == null || cachedSplatMap.Count < (idx + 1))
			{
				cachedSplatMap.Resize(idx +1);
			}

			if (cachedSplatMap.Count < idx || cachedSplatMap[idx] == null || cachedSplatMap[idx].Length <= 0)
			{
				var splatmapGen = new TerrainSplatMapGenerator(this);
				return splatmapGen.CacheSplatmap(idx);
			}
			else
				return cachedSplatMap[idx];
		}
	}
}
