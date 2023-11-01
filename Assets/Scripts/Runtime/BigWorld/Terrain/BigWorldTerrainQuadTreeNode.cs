using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldTerrainQuadTreeNode
    {
        /// <summary>
        /// X轴坐标
        /// </summary>
        private readonly float m_posX;
        public float posX => m_posX;
        
        /// <summary>
        /// Z轴坐标
        /// </summary>
        private readonly float m_posZ;
        public float posZ => m_posZ;
        
        /// <summary>
        /// 深度
        /// </summary>
        private readonly int m_depth;
        public int depth => m_depth;

        /// <summary>
        /// 节点大小
        /// </summary>
        private readonly float m_size;
        public float size => m_size;

        /// <summary>
        /// 该节点是否可见
        /// </summary>
        private bool m_visible;
        public bool visible => m_visible;

        /// <summary>
        /// 是否是叶子结点
        /// </summary>
        private bool isLeaf => m_depth == BigWorldTerrainChunk.maxQuadTreeDepth;

        /// <summary>
        /// 包围盒
        /// </summary>
        private Bounds m_bounds;
        
        /// <summary>
        /// 子节点
        /// </summary>
        private readonly BigWorldTerrainQuadTreeNode[] m_children;
        public BigWorldTerrainQuadTreeNode[] children;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="posX">该节点的X轴起始坐标</param>
        /// <param name="posZ">该节点的Z轴起始坐标</param>
        /// <param name="depth">该节点的深度</param>
        public BigWorldTerrainQuadTreeNode(float posX, float posZ, int depth)
        {
            m_posX = posX;
            m_posZ = posZ;
            m_depth = depth;
            m_size = BigWorldUtility.chunkSize >> depth;
            
            //创建包围盒
            var halfSize = m_size / 2;
            m_bounds = new Bounds(new Vector3(m_posX + halfSize, 0, m_posZ + halfSize), new Vector3(m_size, m_size, m_size));

            //创建子节点
            if (!isLeaf)
            {
                var childDepth = m_depth + 1;
                var childSize = m_size / 2;
                m_children = new[]
                {
                    new BigWorldTerrainQuadTreeNode(m_posX, m_posZ, childDepth),
                    new BigWorldTerrainQuadTreeNode(m_posX, m_posZ + childSize, childDepth),
                    new BigWorldTerrainQuadTreeNode(m_posX + childSize, m_posZ, childDepth),
                    new BigWorldTerrainQuadTreeNode(m_posX + childSize, m_posZ + childSize, childDepth),
                };
            }
        }

        /// <summary>
        /// 刷新
        /// </summary>
        /// <param name="centerBounds">中心点包围盒</param>
        /// <param name="visibleNodes">可见的Node</param>
        public void Refresh(Bounds centerBounds, List<BigWorldTerrainQuadTreeNode> visibleNodes)
        {
            if (m_bounds.Intersects(centerBounds))
            {
                if (isLeaf)
                {
                    //center在该节点内，并且该节点是叶子结点
                    m_visible = true;
                    visibleNodes.Add(this);
                }
                else
                {
                    //center在该节点内，则该节点设置成不可见，继续遍历子节点
                    m_visible = false;
                    if (m_children != null)
                    {
                        var centerBoundsSize = Mathf.Max(centerBounds.size.x / 2, 64.0f);
                        centerBounds.size = new Vector3(centerBoundsSize, centerBoundsSize, centerBoundsSize);
                        foreach (var child in m_children)
                        {
                            child.Refresh(centerBounds, visibleNodes);
                        }
                    }
                }
            }
            else
            {
                //center不在该Node内，则将该节点设置成可见，所有子节点设置成不可见
                m_visible = true;
                visibleNodes.Add(this);
                
                if (m_children != null)
                {
                    foreach (var child in m_children)
                    {
                        child.m_visible = false;
                    }
                }
            }
        }
    }
}