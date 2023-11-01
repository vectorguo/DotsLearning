using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldTerrainChunk
    {
        /// <summary>
        /// 四叉树最大深度
        /// </summary>
        public const int maxQuadTreeDepth = 4;
        
        /// <summary>
        /// 四叉树根节点
        /// </summary>
        private readonly BigWorldTerrainQuadTreeNode m_quadTreeRoot;
        public BigWorldTerrainQuadTreeNode quadTreeRoot => m_quadTreeRoot;

        /// <summary>
        /// Terrain Batch ID
        /// </summary>
        private readonly int m_batchID;

        /// <summary>
        /// 是否变化，如果发生变化，则需要刷新BatchGroup数据
        /// </summary>
        private bool m_dirty;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="chunkX"></param>
        /// <param name="chunkZ"></param>
        public BigWorldTerrainChunk(int chunkX, int chunkZ)
        {
            //创建四叉树
            var chunkPositionX = BigWorldUtility.GetChunkWorldPosition(chunkX);
            var chunkPositionZ = BigWorldUtility.GetChunkWorldPosition(chunkZ);
            m_quadTreeRoot = new BigWorldTerrainQuadTreeNode(chunkPositionX, chunkPositionZ, 0);
            
            //添加BatchGroup
            m_batchID = BigWorldTerrainBatchRenderGroup.instance.AddBatchGroup();
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Destroy()
        {
            //销毁BatchGroup
            var renderGroup = BigWorldTerrainBatchRenderGroup.instance;
            if (renderGroup != null)
            {
                renderGroup.RemoveBatchGroup(m_batchID);
            }
        }

        /// <summary>
        /// 刷新
        /// </summary>
        /// <param name="center">中心点位置</param>
        public void Refresh(Vector3 center)
        {
            var visibleNodes = new List<BigWorldTerrainQuadTreeNode>();
            var centerBoundsSize = m_quadTreeRoot.size / 2;
            var centerBounds = new Bounds(center, new Vector3(centerBoundsSize, centerBoundsSize, centerBoundsSize));
            m_quadTreeRoot.Refresh(centerBounds, visibleNodes);

            //刷新BatchGroup数据
            //需要检测可见节点是否变化 TODO
            var batchGroup = BigWorldTerrainBatchRenderGroup.instance.GetBatchGroup(m_batchID);
            if (batchGroup != null)
            {
                batchGroup.Refresh(visibleNodes);
            }
            
            // Draw(visibleNodes);
        }
        
        #region Draw
        private List<GameObject> m_debugCubes = new List<GameObject>();
        
        /// <summary>
        /// 绘制调试信息
        /// </summary>
        private void Draw(List<BigWorldTerrainQuadTreeNode> visibleNodes)
        {
            foreach (var cube in m_debugCubes)
            {
                Object.Destroy(cube);
            }
            m_debugCubes.Clear();

            foreach (var node in visibleNodes)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = new Vector3(node.posX + node.size / 2, 0, node.posZ + node.size / 2);
                cube.transform.localScale = new Vector3(node.size - 0.5f, 0.1f, node.size - 0.5f);
                m_debugCubes.Add(cube);
            }
        }
        #endregion
    }
}