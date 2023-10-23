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
        public int m_x;
        public int m_z;
    }
}