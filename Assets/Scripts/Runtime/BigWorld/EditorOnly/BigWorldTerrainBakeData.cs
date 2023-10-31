using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldTerrainBakeData : MonoBehaviour
    {
        /// <summary>
        /// 唯一ID
        /// </summary>
        public int instanceID;

        #region Cell
        public int blockX;
        public int blockZ;
        public int blockIndex;
        #endregion
        
        #region Terrain
        public Terrain terrain;
        #endregion
    }
}