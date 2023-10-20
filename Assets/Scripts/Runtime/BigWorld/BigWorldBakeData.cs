using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldBakeData : MonoBehaviour
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

        #region LOD
        /// <summary>
        /// LODGroup
        /// </summary>
        public LODGroup lodGroup;
        #endregion
        
        #region Render
        /// <summary>
        /// 不带LODGroup时烘焙使用的MeshRenderer
        /// </summary>
        public new MeshRenderer renderer;
        
        /// <summary>
        /// 带LODGroup时烘焙使用的MeshRenderer
        /// </summary>
        public MeshRenderer[][] renderers;
        #endregion
    }   
}