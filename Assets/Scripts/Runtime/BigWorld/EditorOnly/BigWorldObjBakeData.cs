using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldObjBakeData : MonoBehaviour
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
        private MeshRenderer[][] m_renderers;
        public MeshRenderer[][] renderers
        {
            get
            {
                if (m_renderers == null)
                {
                    //暂时默认不同LODLevel的Renderer是一一对应的

                    m_renderers = new MeshRenderer[lodGroup.lodCount][];
                    for (var lodLevel = 0; lodLevel < lodGroup.lodCount; ++lodLevel)
                    {
                        var lod = lodGroup.GetLODs()[lodLevel];
                        m_renderers[lodLevel] = new MeshRenderer[lod.renderers.Length];
                        for (var i = 0; i < lod.renderers.Length; ++i)
                        {
                            m_renderers[lodLevel][i] = (MeshRenderer)lod.renderers[i];
                        }
                    }
                }
                return m_renderers;
            }
        }
        #endregion
    }   
}