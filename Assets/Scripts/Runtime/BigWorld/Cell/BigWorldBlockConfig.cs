using System;
using UnityEngine;

namespace BigCat.BigWorld
{
    [Serializable]
    public class BigWorldBlockConfig : ScriptableObject
    {
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