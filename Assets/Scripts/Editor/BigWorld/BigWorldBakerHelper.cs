using System.Collections.Generic;
using BigCat.BigWorld;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigCatEditor.BigWorld
{
    public static class BigWorldBakerHelper
    {
        /// <summary>
        /// Lightmap贴图质量
        /// </summary>
        public enum LightmapQuality
        {
            High,
            Med,
            Low
        }
        
        /// <summary>
        /// 格子大小
        /// </summary>
        public const int cellSize = 256;
        
        /// <summary>
        /// 每一行最多有多少个
        /// </summary>
        private const int c_cellRowCount = 1024;

        /// <summary>
        /// 大世界原点的格子偏移
        /// </summary>
        public const int bigWorldOriginCellOffset = 50;
        
        /// <summary>
        /// 大世界原点偏移
        /// </summary>
        public const float bigWorldOriginOffset = -1024 * bigWorldOriginCellOffset;
        
        #region 坐标转换
        /// <summary>
        /// 获取基于大世界原点的格子坐标
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <returns>基于大世界原点的格子坐标</returns>
        public static int GetCellCoordinateBaseOri(float worldPosition)
        {
            return (int)((worldPosition - bigWorldOriginOffset) / cellSize);
        }

        /// <summary>
        /// 获取基于Zero的格子坐标
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <returns>基于大Zero的格子坐标</returns>
        public static int GetCoordinateBaseZero(float worldPosition)
        {
            return GetCellCoordinateBaseOri(worldPosition) - (1024 / cellSize) * bigWorldOriginCellOffset;
        }

        /// <summary>
        /// 获取Cell的索引
        /// </summary>
        /// <param name="cellX">Cell的X轴坐标</param>
        /// <param name="cellZ">Cell的Z轴坐标</param>
        /// <returns></returns>
        public static int GetCellIndex(int cellX, int cellZ)
        {
            return cellZ * c_cellRowCount + cellX;
        }

        /// <summary>
        /// 将Cell所以转换成对应的坐标
        /// </summary>
        /// <param name="cellIndex">Cell索引</param>
        /// <param name="cellX">Cell的X轴坐标</param>
        /// <param name="cellZ">Cell的Z轴卓表</param>
        public static void GetCellCoordinate(int cellIndex, out int cellX, out int cellZ)
        {
            cellX = cellIndex % c_cellRowCount;
            cellZ = cellIndex / c_cellRowCount;
        }
        #endregion

        #region 生成对象的烘焙数据
        /// <summary>
        /// 生成场景对象的烘焙数据
        /// </summary>
        /// <param name="sceneRoot">根结点</param>
        /// <param name="baseInstanceID">基础ID</param>
        public static void CreateBakeDataOfScene(GameObject sceneRoot, int baseInstanceID)
        {
            CreateBakeDataOfGameObjects(sceneRoot.transform);
            CreateBakeIDOfGameObjects(sceneRoot, baseInstanceID);
        }
        
        private static void CreateBakeDataOfGameObjects(Transform root)
        {
            //检查root身上是否有LODGroup组件
            if (root.TryGetComponent<LODGroup>(out var lodGroup))
            {
                //root身上带有LODGroup组件，代表此节点已经是叶子节点，不再继续向下遍历
                CreateBakeDataOfGameObject(root.gameObject, lodGroup);
            }
            else
            {
                //root身上没有LODGroup组件，检查是否是烘焙节点
                if (root.TryGetComponent<MeshRenderer>(out var _))
                {
                    CreateBakeDataOfGameObject(root.gameObject, null);
                }
                else
                {
                    //删除无效的烘焙数据组件
                    if (root.TryGetComponent<BigWorldBakeData>(out var bakeData))
                    {
                        Object.DestroyImmediate(bakeData);
                    }
                }
                
                //继续向下遍历
                for (var i = 0; i < root.transform.childCount; ++i)
                {
                    CreateBakeDataOfGameObjects(root.transform.GetChild(i));
                }
            }
        }

        private static void CreateBakeDataOfGameObject(GameObject go, LODGroup lodGroup)
        {
            if (!go.TryGetComponent<BigWorldBakeData>(out var bakeData))
            {
                bakeData = go.AddComponent<BigWorldBakeData>();
            }
            
            //计算Cell坐标
            var position = go.transform.position;
            bakeData.cellX = GetCellCoordinateBaseOri(position.x);
            bakeData.cellZ = GetCellCoordinateBaseOri(position.z);
            bakeData.cellIndex = GetCellIndex(bakeData.cellX, bakeData.cellZ);
            
            //处理LODGroup
            bakeData.lodGroup = lodGroup;
            if (lodGroup == null)
            {
                bakeData.renderer = go.GetComponent<MeshRenderer>();
            }
            else
            {
                //暂时默认不同LODLevel的Renderer是一一对应的
                bakeData.renderers = new MeshRenderer[bakeData.lodGroup.lodCount][];
                for (var lodLevel = 0; lodLevel < bakeData.lodGroup.lodCount; ++lodLevel)
                {
                    var lod = bakeData.lodGroup.GetLODs()[lodLevel];
                    bakeData.renderers[lodLevel] = new MeshRenderer[lod.renderers.Length];
                    for (var i = 0; i < lod.renderers.Length; ++i)
                    {
                        bakeData.renderers[lodLevel][i] = (MeshRenderer)lod.renderers[i];
                    }
                }
            }
        }

        /// <summary>
        /// 生成烘焙对象的InstanceID
        /// </summary>
        /// <param name="sceneRoot">场景根结点</param>
        /// <param name="baseInstanceID">基础ID</param>
        private static void CreateBakeIDOfGameObjects(GameObject sceneRoot, int baseInstanceID)
        {
            var instanceID = baseInstanceID;
            var bakeDataComponents = sceneRoot.GetComponentsInChildren<BigWorldBakeData>();
            foreach (var bakeData in bakeDataComponents)
            {
                bakeData.instanceID = ++instanceID;
            }
        }
        #endregion
        
        #region 烘焙组
        /// <summary>
        /// 大世界烘焙数据组
        /// </summary>
        public class BigWorldBakeGroup
        {
            public int cellIndex;

            public readonly List<BigWorldBakeGroupLOD> lods = new List<BigWorldBakeGroupLOD>();
            public readonly List<BigWorldBakeGroupItem> items = new List<BigWorldBakeGroupItem>();

            /// <summary>
            /// 是否带有LOD
            /// </summary>
            public bool hasLod => lods.Count > 1;

            /// <summary>
            /// 构造函数
            /// </summary>
            public BigWorldBakeGroup(int cellIndex)
            {
                this.cellIndex = cellIndex;
            }

            public BigWorldBakeGroup(int cellIndex, Material material, Mesh mesh)
            {
                this.cellIndex = cellIndex;
                lods = new List<BigWorldBakeGroupLOD>
                {
                    new BigWorldBakeGroupLOD()
                    {
                        lodLevel = 0,
                        material = material,
                        mesh = mesh
                    }
                };
            }

            public BigWorldBakeGroup(int cellIndex, BigWorldBakeData bakeData, int rendererIndex)
            {
                this.cellIndex = cellIndex;
                lods = new List<BigWorldBakeGroupLOD>();
                
                var lodCount = bakeData.renderers.Length;
                for (var lodLevel = 0; lodLevel < lodCount; ++lodLevel)
                {
                    GetMaterialAndMesh(bakeData.renderers[lodLevel][rendererIndex], out var material, out var mesh);
                    lods.Add(new BigWorldBakeGroupLOD
                    {
                        lodLevel = lodLevel,
                        material = material,
                        mesh = mesh
                    });
                }
            }

            public void AddItem(BigWorldBakeData bakeData, int rendererIndex = 0)
            {
                if (bakeData.lodGroup == null)
                {
                    items.Add(new BigWorldBakeGroupItem(bakeData.renderer));    
                }
                else
                {
                    items.Add(new BigWorldBakeGroupItem(bakeData.renderers[0][rendererIndex]));
                }
            }
        }

        public class BigWorldBakeGroupLOD
        {
            /// <summary>
            /// LOD名称，使用Mesh名称和Material名称拼接
            /// </summary>
            public string lodName => $"{mesh.name}_{material.name}";

            /// <summary>
            /// LOD Level
            /// </summary>
            public int lodLevel;

            /// <summary>
            /// 该LOD绘制时使用的材质
            /// </summary>
            public Material material;
            
            /// <summary>
            /// 该LOD绘制时使用的Mesh
            /// </summary>
            public Mesh mesh;
        }

        public class BigWorldBakeGroupItem
        {
            public readonly MeshRenderer renderer;

            public int hqLightmapIndex;
            public Vector4 hqLightmapScaleOffset;

            public int mqLightmapIndex;
            public Vector4 mqLightmapScaleOffset;

            public int lqLightmapIndex;
            public Vector4 lqLightmapScaleOffset;

            public BigWorldBakeGroupItem(MeshRenderer r)
            {
                renderer = r;
            }
        }

        /// <summary>
        /// 获取烘焙组
        /// </summary>
        public static Dictionary<int, List<BigWorldBakeGroup>> GetBakeGroups()
        {
            var result = new Dictionary<int, List<BigWorldBakeGroup>>();
            
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObject in rootObjects)
                {
                    GetBakeGroups(rootObject, result);
                }
            }

            return result;
        }

        private static void GetBakeGroups(GameObject root, Dictionary<int, List<BigWorldBakeGroup>> bakeGroupsMap)
        {
            var bakeDataComponents = root.GetComponentsInChildren<BigWorldBakeData>();
            foreach (var bakeData in bakeDataComponents)
            {
                if (!bakeGroupsMap.TryGetValue(bakeData.cellIndex, out var bakeGroups))
                {
                    bakeGroups = new List<BigWorldBakeGroup>();
                    bakeGroupsMap.Add(bakeData.cellIndex, bakeGroups);
                }

                var targetBakeGroups = FindBakeGroups(bakeData, bakeGroups);
                for (var i = 0; i < targetBakeGroups.Length; ++i)
                {
                    targetBakeGroups[i].AddItem(bakeData, i);
                }
            }
        }

        private static BigWorldBakeGroup[] FindBakeGroups(BigWorldBakeData bakeData, List<BigWorldBakeGroup> bakeGroups)
        {
            if (bakeData.lodGroup == null)
            {
                GetMaterialAndMesh(bakeData.renderer, out var material, out var mesh);
                
                //查找已有的BakeGroup
                foreach (var bakeGroup in bakeGroups)
                {
                    if (bakeGroup.hasLod) continue;
                    var bakeGroupLod = bakeGroup.lods[0];
                    if (bakeGroupLod.material == material && bakeGroupLod.mesh == mesh)
                    {
                        return new[] { bakeGroup };
                    }
                }
                
                //创建新的BakeGroup
                var newBakeGroup = new BigWorldBakeGroup(bakeData.cellIndex, material, mesh);
                bakeGroups.Add(newBakeGroup);
                return new[] { newBakeGroup };
            }
            else
            {
                var lodCount = bakeData.renderers.Length;
                var targetBakeGroupCount = bakeData.renderers[0].Length;
                var targetBakeGroups = new BigWorldBakeGroup[targetBakeGroupCount];
                for (var i = 0; i < targetBakeGroupCount; ++i)
                {
                    //查找已有的BakeGroup
                    foreach (var bakeGroup in bakeGroups)
                    {
                        if (bakeGroup.hasLod && bakeGroup.lods.Count == lodCount)
                        {
                            var matched = true;
                            for (var lodLevel = 0; lodLevel < lodCount; ++lodLevel)
                            {
                                var renderer = bakeData.renderers[lodLevel][i];
                                GetMaterialAndMesh(renderer, out var material, out var mesh);
                                
                                var bakeGroupLod = bakeGroup.lods[lodLevel];
                                if (bakeGroupLod.material != material || bakeGroupLod.mesh != mesh)
                                {
                                    matched = false;
                                }
                            }

                            if (matched)
                            {
                                //找到匹配的BakeGroup
                                targetBakeGroups[i] = bakeGroup;
                                break;
                            }
                        }
                    }
                    
                    if (targetBakeGroups[i] == null)
                    {
                        //创建新的BakeGroup
                        var newBakeGroup = new BigWorldBakeGroup(bakeData.cellIndex, bakeData, i);
                        bakeGroups.Add(newBakeGroup);
                        targetBakeGroups[i] = newBakeGroup;
                    }
                }

                return targetBakeGroups;
            }
        }

        private static void GetMaterialAndMesh(MeshRenderer meshRenderer, out Material material, out Mesh mesh)
        {
            material = meshRenderer.sharedMaterial;

            var meshFilter = meshRenderer.GetComponent<MeshFilter>();
            mesh = meshFilter.sharedMesh;
        }
        #endregion
    }   
}