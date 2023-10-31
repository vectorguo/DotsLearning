using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldBlock : BigWorldCell
    {
        /// <summary>
        /// Block配置数据
        /// </summary>
        private BigWorldBlockConfig m_config;
        
        /// <summary>
        /// Lightmap
        /// </summary>
        private Texture2DArray m_lightmaps;

        /// <summary>
        /// Batch ID数组
        /// </summary>
        private int[] m_batchIDs;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="blockIndex"></param>
        public BigWorldBlock(int blockIndex) : base(blockIndex)
        {
            //暂时是同步加载，移到正式项目里需要改成异步加载
            m_config = Resources.Load<BigWorldBlockConfig>($"BigWorld/{BigWorldManager.instance.worldName}/block/block_{m_x}_{m_z}/config");

            //初始化完成的回调
            OnInitialized();
        }

        /// <summary>
        /// 更新
        /// </summary>
        public void Update()
        {

        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void OnInitialized()
        {
            //初始化Lightmap
            InitializeLightmap();
            
            //初始化Batch
            InitializeBatchGroup();
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Destroy()
        {
            if (m_batchIDs != null)
            {
                foreach (var batchID in m_batchIDs)
                {
                    BigWorldObjectBatchRenderGroup.instance.RemoveBatchGroup(batchID);
                }
            }

            //销毁Texture2DArray
            Object.Destroy(m_lightmaps);

            //释放config资源
            Resources.UnloadAsset(m_config);
            m_config = null;
        }

        private void InitializeLightmap()
        {
            if (m_config.totalLightmapCount == 0)
            {
                return;
            }
            
            m_lightmaps = new Texture2DArray(1024, 1024, m_config.totalLightmapCount, BigWorldUtility.lightmapTextureFormat, false);

            var worldName = BigWorldManager.instance.worldName;
            for (var i = 0; i < m_config.hqLightmapCount; ++i)
            {
                var lightmap = Resources.Load<Texture2D>($"BigWorld/{worldName}/block/block_{m_x}_{m_z}/shq_lightmap_{i}");
                m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, i);
            }

            var offset = m_config.hqLightmapCount;
            for (var i = 0; i < m_config.mqLightmapCount; ++i)
            {
                var lightmap = Resources.Load<Texture2D>($"BigWorld/{worldName}/block/block_{m_x}_{m_z}/smq_lightmap_{i}");
                m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, offset + i);
            }

            offset += m_config.mqLightmapCount;
            for (var i = 0; i < m_config.lqLightmapCount; ++i)
            {
                var lightmap = Resources.Load<Texture2D>($"BigWorld/{worldName}/block/block_{m_x}_{m_z}/slq_lightmap_{i}");
                m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, offset + i);
            }
            m_lightmaps.Apply();
        }

        private void InitializeBatchGroup()
        {
            if (m_config.batchGroupCount == 0)
            {
                return;
            }

            m_batchIDs = new int[m_config.batchGroupCount];

            var worldName = BigWorldManager.instance.worldName;
            for (var i = 0; i < m_config.batchGroupCount; ++i)
            {
                var batchGroupConfig = Resources.Load<BigWorldObjectBatchGroupConfig>($"BigWorld/{worldName}/block/block_{m_x}_{m_z}/batchGroupConfig_{i}");
                m_batchIDs[i] = BigWorldObjectBatchRenderGroup.instance.AddBatchGroup(batchGroupConfig, m_lightmaps);
            }
        }
    }
}