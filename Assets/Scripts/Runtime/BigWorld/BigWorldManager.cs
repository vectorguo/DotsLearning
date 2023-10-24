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
        /// 大世界RenderGroup
        /// </summary>
        private BigWorldBatchRenderGroup m_batchRenderGroup;
        public BigWorldBatchRenderGroup BatchRenderGroup => m_batchRenderGroup;
        
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
            //创建RenderGroup
            m_batchRenderGroup = gameObject.AddComponent<BigWorldBatchRenderGroup>();
            m_batchRenderGroup.cullDistance = cullDistance;
            m_batchRenderGroup.useJobForCulling = useJobForCulling;
            m_batchRenderGroup.useJobForUpdate = useJobForUpdate;
            m_batchRenderGroup.initialized = () =>
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
        }
        
        void Update()
        {
            m_batchRenderGroup.cullCenter = player.transform.position;
        }

        private void OnDestroy()
        {
            s_instance = null;
        }
    }
}