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

        public Color[] CacheSplatMap(int id)
        {
            if (cachedSplatMap == null || cachedSplatMap.Count != 2)
            {
                cachedSplatMap = new Godot.Collections.Array<Color[]>();
                cachedSplatMap.Resize(2);
            }

            if (cachedSplatMap.Count < id || cachedSplatMap[id] == null || cachedSplatMap[id].Length <= 0)
            {
                var splatmapGen = new TerrainSplatMapGenerator(this);
                return splatmapGen.CacheSplatmap(id);
            }
            else
                return cachedSplatMap[id];
        }
    }
}
