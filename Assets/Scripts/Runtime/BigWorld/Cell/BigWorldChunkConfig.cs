using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldChunkConfig : ScriptableObject
    {
        /// <summary>
        /// Chunk下的Block索引数组
        /// </summary>
        public List<int> blockIndices;
    }
}