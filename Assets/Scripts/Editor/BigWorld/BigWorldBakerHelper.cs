using BigCat.BigWorld;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigCatEditor.BigWorld
{
    /// <summary>
    /// Lightmap贴图质量
    /// </summary>
    public enum LightmapQuality
    {
        High,
        Med,
        Low,
        None,
    }
    
    public static class BigWorldBakerHelper
    {
        #region 生成对象的烘焙数据
        /// <summary>
        /// 生成场景对象的烘焙数据
        /// </summary>
        /// <param name="sceneRoot">根结点</param>
        /// <param name="baseInstanceID">基础ID</param>
        public static void CreateBakeDataOfScene(GameObject sceneRoot, int baseInstanceID)
        {
            CreateBakeDataOfTerrains(sceneRoot.transform, baseInstanceID);
            CreateBakeDataOfSceneObjects(sceneRoot.transform);
            CreateBakeIDOfGameObjects(sceneRoot, baseInstanceID + 256);
        }

        private static void CreateBakeDataOfTerrains(Transform root, int baseTerrainID)
        {
            var terrainID = baseTerrainID;
            var terrainRoot = root.Find("Terrains");
            if (terrainRoot != null)
            {
                var terrains = terrainRoot.GetComponentsInChildren<Terrain>();
                foreach (var terrain in terrains)
                {
                    if (!terrain.gameObject.TryGetComponent<BigWorldTerrainBakeData>(out var bakeData))
                    {
                        bakeData = terrain.gameObject.AddComponent<BigWorldTerrainBakeData>();
                    }
                    bakeData.instanceID = terrainID++;
                    bakeData.terrain = terrain;
                    
                    //计算Cell坐标
                    var position = terrain.transform.position;
                    bakeData.blockX = BigWorldUtility.GetBlockCoordinate(position.x);
                    bakeData.blockZ = BigWorldUtility.GetBlockCoordinate(position.z);
                    bakeData.blockIndex = BigWorldUtility.GetCellIndex(bakeData.blockX, bakeData.blockZ);
                }
            }
        }
        
        private static void CreateBakeDataOfSceneObjects(Transform root)
        {
            //检查是否是Terrain
            if (root.TryGetComponent<BigWorldTerrainBakeData>(out var _))
            {
                return;
            }
            
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
                    if (root.TryGetComponent<BigWorldObjBakeData>(out var bakeData))
                    {
                        Object.DestroyImmediate(bakeData);
                    }
                }
                
                //继续向下遍历
                for (var i = 0; i < root.transform.childCount; ++i)
                {
                    CreateBakeDataOfSceneObjects(root.transform.GetChild(i));
                }
            }
        }

        private static void CreateBakeDataOfGameObject(GameObject go, LODGroup lodGroup)
        {
            if (!go.TryGetComponent<BigWorldObjBakeData>(out var bakeData))
            {
                bakeData = go.AddComponent<BigWorldObjBakeData>();
            }
            
            //计算Cell坐标
            var position = go.transform.position;
            bakeData.blockX = BigWorldUtility.GetBlockCoordinate(position.x);
            bakeData.blockZ = BigWorldUtility.GetBlockCoordinate(position.z);
            bakeData.blockIndex = BigWorldUtility.GetCellIndex(bakeData.blockX, bakeData.blockZ);
            
            //处理LODGroup
            bakeData.lodGroup = lodGroup;
            if (lodGroup == null)
            {
                bakeData.renderer = go.GetComponent<MeshRenderer>();
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
            var bakeDataComponents = sceneRoot.GetComponentsInChildren<BigWorldObjBakeData>();
            foreach (var bakeData in bakeDataComponents)
            {
                bakeData.instanceID = ++instanceID;
            }
        }
        #endregion
        
        #region 烘焙组
        /// <summary>
        /// 每个Cell的烘焙数据
        /// </summary>
        public class BigWorldBakeDataOfCell
        {
            public readonly List<BigWorldBakeGroup> bakeGroups = new List<BigWorldBakeGroup>();
            public Terrain terrain;
        }
        
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

            public BigWorldBakeGroup(int cellIndex, BigWorldObjBakeData objBakeData, int rendererIndex)
            {
                this.cellIndex = cellIndex;
                lods = new List<BigWorldBakeGroupLOD>();
                
                var lodCount = objBakeData.renderers.Length;
                for (var lodLevel = 0; lodLevel < lodCount; ++lodLevel)
                {
                    GetMaterialAndMesh(objBakeData.renderers[lodLevel][rendererIndex], out var material, out var mesh);
                    lods.Add(new BigWorldBakeGroupLOD
                    {
                        lodLevel = lodLevel,
                        material = material,
                        mesh = mesh
                    });
                }
            }

            public void AddItem(BigWorldObjBakeData objBakeData, int rendererIndex = 0)
            {
                if (objBakeData.lodGroup == null)
                {
                    items.Add(new BigWorldBakeGroupItem(objBakeData.renderer));    
                }
                else
                {
                    items.Add(new BigWorldBakeGroupItem(objBakeData.renderers[0][rendererIndex]));
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
        public static Dictionary<int, BigWorldBakeDataOfCell> GetSceneBakeData()
        {
            var result = new Dictionary<int, BigWorldBakeDataOfCell>();
            
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObject in rootObjects)
                {
                    GetSceneBakeData(rootObject, result);
                }
            }

            return result;
        }

        private static void GetSceneBakeData(GameObject root, Dictionary<int, BigWorldBakeDataOfCell> bakeInfos)
        {
            var bakeObjDataComponents = root.GetComponentsInChildren<BigWorldObjBakeData>();
            foreach (var bakeData in bakeObjDataComponents)
            {
                if (!bakeInfos.TryGetValue(bakeData.blockIndex, out var bakeInfo))
                {
                    bakeInfo = new BigWorldBakeDataOfCell();
                    bakeInfos.Add(bakeData.blockIndex, bakeInfo);
                }

                var targetBakeGroups = FindBakeGroups(bakeData, bakeInfo.bakeGroups);
                for (var i = 0; i < targetBakeGroups.Length; ++i)
                {
                    targetBakeGroups[i].AddItem(bakeData, i);
                }
            }
            
            var bakeTerrainDataComponents = root.GetComponentsInChildren<BigWorldTerrainBakeData>();
            foreach (var bakeData in bakeTerrainDataComponents)
            {
                if (!bakeInfos.TryGetValue(bakeData.blockIndex, out var bakeInfo))
                {
                    bakeInfo = new BigWorldBakeDataOfCell();
                    bakeInfos.Add(bakeData.blockIndex, bakeInfo);
                }
                bakeInfo.terrain = bakeData.terrain;
            }
        }

        private static BigWorldBakeGroup[] FindBakeGroups(BigWorldObjBakeData objBakeData, List<BigWorldBakeGroup> bakeGroups)
        {
            if (objBakeData.lodGroup == null)
            {
                GetMaterialAndMesh(objBakeData.renderer, out var material, out var mesh);
                
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
                var newBakeGroup = new BigWorldBakeGroup(objBakeData.blockIndex, material, mesh);
                bakeGroups.Add(newBakeGroup);
                return new[] { newBakeGroup };
            }
            else
            {
                var lodCount = objBakeData.renderers.Length;
                var targetBakeGroupCount = objBakeData.renderers[0].Length;
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
                                var renderer = objBakeData.renderers[lodLevel][i];
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
                        var newBakeGroup = new BigWorldBakeGroup(objBakeData.blockIndex, objBakeData, i);
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

        #region Utility
        /// <summary>
        /// 复制文件夹
        /// </summary>
        public static bool CopyFolder(string sourceFolder, string destFolder)
        {
            try
            {
                //如果目标路径不存在,则创建目标路径
                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);

                }
                //得到原文件根目录下的所有文件
                string[] files = Directory.GetFiles(sourceFolder);
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest, true);//复制文件
                }
                //得到原文件根目录下的所有文件夹
                string[] folders = Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    string dest = Path.Combine(destFolder, name);
                    CopyFolder(folder, dest);//构建目标路径,递归复制文件
                }
                return true;
            }
            catch (System.Exception _)
            {
                return false;
            }
        }
        #endregion
    }
}