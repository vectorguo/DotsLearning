using System;
using UnityEngine;

namespace BigCat.BigWorld
{
    [Serializable]
    public class BigWorldCellConfig : ScriptableObject
    {
        /// <summary>
        /// 世界空间下基于Origin的坐标
        /// </summary>
        public int x;
        public int z;

        /// <summary>
        /// BatchGroup数量
        /// </summary>
        public int batchGroupCount;

        /// <summary>
        /// 不同精度的Lightmap数量
        /// </summary>
        public int hqLightmapCount;
        public int mqLightmapCount;
        public int lqLightmapCount;
        public int totalLightmapCount => hqLightmapCount + mqLightmapCount + lqLightmapCount;
    }
}