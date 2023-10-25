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
        public int cellX;
        public int cellZ;
        public int cellIndex;
        #endregion
        
        #region Terrain
        public Terrain terrain;
        #endregion
    }
}