using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldCell
    {
        /// <summary>
        /// Cell配置数据
        /// </summary>
        private BigWorldCellConfig m_config;
        
        /// <summary>
        /// Lightmap
        /// </summary>
        private Texture2DArray m_lightmaps;

        /// <summary>
        /// Batch ID数组
        /// </summary>
        private int[] m_batchIDs;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="config">配置数据</param>
        /// <param name="worldName">大世界名称</param>
        public void Initialize(BigWorldCellConfig config, string worldName)
        {
            m_config = config;
            
            //初始化Lightmap
            InitializeLightmap(worldName);
            
            //初始化Batch
            InitializeBatchGroup(worldName);
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
        }

        private void InitializeLightmap(string worldName)
        {
            if (m_config.totalLightmapCount == 0)
            {
                return;
            }
            
            m_lightmaps = new Texture2DArray(1024, 1024, m_config.totalLightmapCount, BigWorldUtility.lightmapTextureFormat, false);
            for (var i = 0; i < m_config.hqLightmapCount; ++i)
            {
                var lightmap = Resources.Load<Texture2D>($"BigWorld/{worldName}/cell_{m_config.x}_{m_config.z}/shq_lightmap_{i}");
                m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, i);
            }

            var offset = m_config.hqLightmapCount;
            for (var i = 0; i < m_config.mqLightmapCount; ++i)
            {
                var lightmap = Resources.Load<Texture2D>($"BigWorld/{worldName}/cell_{m_config.x}_{m_config.z}/smq_lightmap_{i}");
                m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, offset + i);
            }

            offset += m_config.mqLightmapCount;
            for (var i = 0; i < m_config.lqLightmapCount; ++i)
            {
                var lightmap = Resources.Load<Texture2D>($"BigWorld/{worldName}/cell_{m_config.x}_{m_config.z}/slq_lightmap_{i}");
                m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, offset + i);
            }
            m_lightmaps.Apply();
        }

        private void InitializeBatchGroup(string worldName)
        {
            if (m_config.batchGroupCount == 0)
            {
                return;
            }
            
            m_batchIDs = new int[m_config.batchGroupCount];
            for (var i = 0; i < m_config.batchGroupCount; ++i)
            {
                var batchGroupConfig = Resources.Load<BigWorldObjectBatchGroupConfig>($"BigWorld/{worldName}/cell_{m_config.x}_{m_config.z}/batchGroupConfig_{i}");
                m_batchIDs[i] = BigWorldObjectBatchRenderGroup.instance.AddBatchGroup(batchGroupConfig, m_lightmaps);
            }
        }
    }
}