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
        /// Terrain使用的shader
        /// </summary>
        public Shader terrainShader;

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
        /// 大世界物体的BatchRenderGroup
        /// </summary>
        private BigWorldObjectBatchRenderGroup m_objectBrg;
        public BigWorldObjectBatchRenderGroup objectBrg => m_objectBrg;

        /// <summary>
        /// 大世界Terrain的BatchRenderGroup
        /// </summary>
        private BigWorldTerrainBatchRenderGroup m_terrainBrg;
        public BigWorldTerrainBatchRenderGroup terrainBrg => m_terrainBrg;
        
        /// <summary>
        /// 256*256的Cell列表
        /// </summary>
        private readonly List<BigWorldCell> m_cells = new List<BigWorldCell>();

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
            m_objectBrg.initialized = () =>
            {
                //加载大世界配置
                var bigWorldConfig = Resources.Load<BigWorldConfig>($"BigWorld/{worldName}/config/bigworld");
                foreach (var cellConfig in bigWorldConfig.cellConfigs)
                {
                    var cell = new BigWorldCell();
                    cell.Initialize(cellConfig, worldName);
                    m_cells.Add(cell);
                }
            };
            
            //创建Terrain的BatchRenderGroup
            m_terrainBrg = gameObject.AddComponent<BigWorldTerrainBatchRenderGroup>();
            m_terrainBrg.terrainShader = terrainShader;
            m_terrainBrg.useJobForCulling = useJobForCulling;
            m_terrainBrg.initialized = () =>
            {
                //测试，加载大世界Terrain
                var terrainConfig = Resources.Load<BigWorldTerrainBatchGroupConfig>($"BigWorld/{worldName}/config/terrain");
                m_terrainBrg.AddBatchGroup(terrainConfig);
            };
        }
        
        void Update()
        {
            m_objectBrg.cullCenter = player.transform.position;
        }

        private void OnDestroy()
        {
            foreach (var cell in m_cells)
            {
                cell.Destroy();
            }
            m_cells.Clear();
            
            s_instance = null;
        }
    }
}