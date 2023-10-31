using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldChunk : BigWorldCell
    {
        /// <summary>
        /// Block配置数据
        /// </summary>
        private BigWorldChunkConfig m_config;

        /// <summary>
        /// Chunk下的Block列表
        /// </summary>
        private readonly List<BigWorldBlock> m_blocks = new List<BigWorldBlock>();

        /// <summary>
        /// 构造函数
        /// </summary>
        public BigWorldChunk(int chunkIndex) : base(chunkIndex)
        {
            //暂时是同步加载，移到正式项目里需要改成异步加载
            m_config = Resources.Load<BigWorldChunkConfig>($"BigWorld/{BigWorldManager.instance.worldName}/chunk/chunk_{m_x}_{m_z}/config");

            //初始化完成的回调
            OnInitialized();
        }

        /// <summary>
        /// 更新
        /// </summary>
        public void Update()
        {
            //更新Block
            foreach (var block in m_blocks)
            {
                block.Update();
            }
        }

        public void Destroy()
        {
            //销毁Block
            foreach(var block in m_blocks)
            {
                block.Destroy();
            }
            m_blocks.Clear();

            //释放config资源
            Resources.UnloadAsset(m_config);
            m_config = null;
        }

        /// <summary>
        /// 初始化完成
        /// </summary>
        private void OnInitialized()
        {
            //创建Block
            foreach (var blockIndex in m_config.blockIndices)
            {
                m_blocks.Add(new BigWorldBlock(blockIndex));
            }
        }
    }
}