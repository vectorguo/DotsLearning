using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    [Serializable]
    public class BigWorldConfig : ScriptableObject
    {
        /// <summary>
        /// 大世界所有的Chunk的索引数组
        /// </summary>
        public List<int> chunkIndices;
    }
}