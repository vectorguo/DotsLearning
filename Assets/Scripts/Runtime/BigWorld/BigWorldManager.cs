using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    public class BigWorldManager : MonoBehaviour
    {
        /// <summary>
        /// 大世界名称
        /// </summary>
        public string worldName;
        
        /// <summary>
        /// 裁剪距离
        /// </summary>
        public float cullDistance = 384.0f;

        /// <summary>
        /// 是否使用Job来裁剪
        /// </summary>
        public bool useJobForCulling = true;
        
        /// <summary>
        /// 是否使用Job来更新
        /// </summary>
        public bool useJobForUpdate = true;

        /// <summary>
        /// 玩家
        /// </summary>
        public GameObject player;

        /// <summary>
        /// 单例
        /// </summary>
        private static BigWorldManager s_instance;
        public static BigWorldManager instance => s_instance;

        /// <summary>
        /// 大世界配置
        /// </summary>
        private BigWorldConfig m_config;
        public BigWorldConfig config => m_config;

        /// <summary>
        /// 大世界物体的BatchRenderGroup
        /// </summary>
        private BigWorldObjectBatchRenderGroup m_objectBrg;
        public BigWorldObjectBatchRenderGroup objectBrg => m_objectBrg;

        /// <summary>
        /// Terrain管理器
        /// </summary>
        private BigWorldTerrainManager m_terrainMgr;
        public BigWorldTerrainManager terrainMgr => m_terrainMgr;
        
        /// <summary>
        /// 已经创建的Chunk的列表
        /// </summary>
        private readonly List<BigWorldChunk> m_chunks = new List<BigWorldChunk>();

        private void Awake()
        {
            s_instance = this;
            
            //设置帧率为60帧
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            //创建场景对象的BatchRenderGroup
            m_objectBrg = gameObject.AddComponent<BigWorldObjectBatchRenderGroup>();
            m_objectBrg.cullDistance = cullDistance;
            m_objectBrg.useJobForCulling = useJobForCulling;
            m_objectBrg.useJobForUpdate = useJobForUpdate;
            m_objectBrg.onInitialized = Initialize;

            //创建TerrainManager
            m_terrainMgr = gameObject.AddComponent<BigWorldTerrainManager>();
            m_terrainMgr.center = player.transform.position;
            m_terrainMgr.onInitialized = Initialize;
        }
        
        void Update()
        {
            var playerPosition = player.transform.position;
            
            //更新Object BRG
            m_objectBrg.cullCenter = playerPosition;

            //更新Terrain
            m_terrainMgr.center = playerPosition;
            
            //更新Chunk
            foreach (var chunk in m_chunks)
            {
                chunk.Update();
            }
        }

        private void OnDestroy()
        {
            //销毁所有Chunk
            foreach (var chunk in m_chunks)
            {
                chunk.Destroy();
            }
            m_chunks.Clear();

            //释放config
            Resources.UnloadAsset(m_config);
            m_config = null;

            //清空单例
            s_instance = null;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initialize()
        {
            if (!m_objectBrg.isInitialized || !m_terrainMgr.isInitialized)
            {
                return;
            }

            //加载大世界配置
            m_config = Resources.Load<BigWorldConfig>($"BigWorld/{worldName}/bigworld");

            //加载Player周围的Chunk
            var playerPosition = player.transform.position;
            var playerChunkX = BigWorldUtility.GetChunkCoordinate(playerPosition.x);
            var playerChunkZ = BigWorldUtility.GetChunkCoordinate(playerPosition.z);
            for (var m = -1; m <= 1; ++m)
            {
                for (var n = -1; n <= 1; ++n)
                {
                    var chunkX = playerChunkX + m;
                    var chunkZ = playerChunkZ + n;
                    var chunkIndex = BigWorldUtility.GetCellIndex(chunkX, chunkZ);
                    if (m_config.chunkIndices.Contains(chunkIndex))
                    {
                        m_chunks.Add(new BigWorldChunk(chunkIndex));
                    }
                }
            }
        }
    }
}