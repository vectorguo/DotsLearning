using System;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldTerrainManager : MonoBehaviour
    {
        /// <summary>
        /// 单例
        /// </summary>
        private static BigWorldTerrainManager s_instance;
        public static BigWorldTerrainManager instance => s_instance;

        /// <summary>
        /// 是否初始化完成
        /// </summary>
        public bool isInitialized { get; private set; }

        /// <summary>
        /// 初始化完成的回调函数
        /// </summary>
        public Action onInitialized { get; set; }

        /// <summary>
        /// 大世界Terrain的BatchRenderGroup
        /// </summary>
        private BigWorldTerrainBatchRenderGroup m_terrainBrg;
        public BigWorldTerrainBatchRenderGroup terrainBrg => m_terrainBrg;

        private void Awake()
        {
            s_instance = this;
        }

        private void Start()
        {
            //创建Terrain的BatchRenderGroup
            m_terrainBrg = gameObject.AddComponent<BigWorldTerrainBatchRenderGroup>();
            m_terrainBrg.onInitialized = () =>
            {
                isInitialized = true;
                onInitialized?.Invoke();
            };
        }

        private void Update()
        {

        }

        private void OnDestroy()
        {
            s_instance = null;
        }

        public void AddBlock(int blockX, int blockZ)
        {

        }
    }
}