using System;
using System.Collections.Generic;
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
        /// center的坐标索引
        /// </summary>
        private int m_centerX;
        private int m_centerZ;
        
        /// <summary>
        /// 中心点
        /// </summary>
        private Vector3 m_center;
        public Vector3 center
        {
            set
            {
               m_center = value;
               var x = (int)(BigWorldUtility.GetStepCoordinate_Float(m_center.x) * 2);
               var z = (int)(BigWorldUtility.GetStepCoordinate_Float(m_center.z) * 2);
               if (x != m_centerX || z != m_centerZ)
               {
                   m_centerX = x;
                   m_centerZ = z;
                   RefreshQuadTrees();   
               }
            }
        }
        
        /// <summary>
        /// 大世界Terrain的BatchRenderGroup
        /// </summary>
        private BigWorldTerrainBatchRenderGroup m_terrainBrg;
        public BigWorldTerrainBatchRenderGroup terrainBrg => m_terrainBrg;

        /// <summary>
        /// Chunk块列表
        /// </summary>
        private readonly Dictionary<int, BigWorldTerrainChunk> m_chunks = new Dictionary<int, BigWorldTerrainChunk>();

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

        private void OnDestroy()
        {
            s_instance = null;
        }

        /// <summary>
        /// 添加Chunk
        /// </summary>
        /// <param name="chunkX">Chunk在X轴的索引，不是坐标</param>
        /// <param name="chunkZ">Chunk在Z轴的索引，不是坐标</param>
        public void AddChunk(int chunkX, int chunkZ)
        {
            var chunkIndex = BigWorldUtility.GetCellIndex(chunkX, chunkZ);
            if (m_chunks.ContainsKey(chunkIndex))
            {
                throw new Exception("Terrain - 重复添加相同的Chunk块");
            }
            m_chunks.Add(chunkIndex, new BigWorldTerrainChunk(chunkX, chunkZ));

            //刷新四叉树
            RefreshQuadTrees();
        }

        /// <summary>
        /// 删除Chunk
        /// </summary>
        /// <param name="chunkX">Chunk在X轴的索引，不是坐标</param>
        /// <param name="chunkZ">Chunk在Z轴的索引，不是坐标</param>
        public void RemoveChunk(int chunkX, int chunkZ)
        {
            var chunkIndex = BigWorldUtility.GetCellIndex(chunkX, chunkZ);
            if (m_chunks.TryGetValue(chunkIndex, out var quadTree))
            {
                quadTree.Destroy();
                m_chunks.Remove(chunkIndex);
                
                //刷新四叉树
                RefreshQuadTrees();
            }
        }

        /// <summary>
        /// 刷新四叉树
        /// </summary>
        private void RefreshQuadTrees()
        {
            foreach (var pair in m_chunks)
            {
                pair.Value.Refresh(m_center);
            }
        }
    }
}