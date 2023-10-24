using System;
using System.Collections.Generic;
using System.IO;
using BigCat.BigWorld;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigCatEditor.BigWorld
{
    public static class BigWorldBaker
    {
        /// <summary>
        /// 大世界名称
        /// </summary>
        private static string s_worldName = "MondCity";
        public static string worldName => s_worldName;

        [MenuItem("BigWorld/Bake")]
        public static void Bake()
        {
            //删除旧资源
            var path = $"Assets/Resources/BigWorld/{s_worldName}";
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
            
            //刷新
            AssetDatabase.Refresh();
            
            //生成烘焙数据
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "scene")
                {
                    BigWorldBakerHelper.CreateBakeDataOfScene(go, 0);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            
            //获取烘焙组
            var bakeGroupsMap = BigWorldBakerHelper.GetBakeGroups();
            
            //烘焙Lightmap
            BigWorldLightmapBaker.BakeLightmap(bakeGroupsMap);
            
            //创建BatchGroup配置
            CreateBatchGroupConfigs(bakeGroupsMap);

            //创建大世界运行时配置
            CreateBigWorldConfigs();

            //烘焙完成
            AssetDatabase.Refresh();
            Debug.LogError("烘焙完成");
        }

        #region BatchGroupConfig
        private static void CreateBatchGroupConfigs(Dictionary<int, List<BigWorldBakerHelper.BigWorldBakeGroup>> bakeGroupsMap)
        {
            foreach (var pair in bakeGroupsMap)
            {
                BigWorldBakerHelper.GetCellCoordinate(pair.Key, out var cellX, out var cellZ);

                for (var index = 0; index < pair.Value.Count; ++index)
                {
                    var bakeGroup = pair.Value[index];
                    var batchGroupConfig = ScriptableObject.CreateInstance<BigWorldBatchGroupConfig>();
                    
                    //LOD
                    batchGroupConfig.lods = new BigWorldBatchGroupConfig.Lod[bakeGroup.lods.Count];
                    for (var i = 0; i < bakeGroup.lods.Count; ++i)
                    {
                        batchGroupConfig.lods[i] = new BigWorldBatchGroupConfig.Lod
                        {
                            mesh = bakeGroup.lods[i].mesh,
                            material = bakeGroup.lods[i].material,
                            lodMinDistance = 30.0f * i,
                            lodMaxDistance = 30.0f * i + 30.0f,
                        };
                    }
                    batchGroupConfig.lods[bakeGroup.lods.Count - 1].lodMaxDistance = -1.0f;

                    //计算Lightmap偏移
                    var folder = $"Assets/Resources/BigWorld/{s_worldName}/cell_{cellX}_{cellZ}";
                    var hqLightmaps = Directory.GetFiles(folder, "shq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                    var hqLightmapCount = hqLightmaps.Length;
                    var mqLightmaps = Directory.GetFiles(folder, "smq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                    var mqLightmapCount = mqLightmaps.Length;
                    
                    batchGroupConfig.count = bakeGroup.items.Count;
                    batchGroupConfig.positions = new List<Vector3>();
                    batchGroupConfig.rotations = new List<Quaternion>();
                    batchGroupConfig.scales = new List<Vector3>();
                    batchGroupConfig.bounds = new List<AABB>();
                    batchGroupConfig.hqLightmapIndices = new List<int>();
                    batchGroupConfig.mqLightmapIndices = new List<int>();
                    batchGroupConfig.lqLightmapIndices = new List<int>();
                    batchGroupConfig.hqLightmapScaleOffsets = new List<Vector4>();
                    batchGroupConfig.mqLightmapScaleOffsets = new List<Vector4>();
                    batchGroupConfig.lqLightmapScaleOffsets = new List<Vector4>();
                    foreach (var item in bakeGroup.items)
                    {
                        batchGroupConfig.positions.Add(item.renderer.transform.position);
                        batchGroupConfig.rotations.Add(item.renderer.transform.rotation);
                        batchGroupConfig.scales.Add(item.renderer.transform.lossyScale);
                        batchGroupConfig.bounds.Add(item.renderer.bounds.ToAABB());
                        batchGroupConfig.hqLightmapIndices.Add(item.hqLightmapIndex);
                        batchGroupConfig.mqLightmapIndices.Add(item.mqLightmapIndex + hqLightmapCount);
                        batchGroupConfig.lqLightmapIndices.Add(item.lqLightmapIndex + hqLightmapCount + mqLightmapCount);
                        batchGroupConfig.hqLightmapScaleOffsets.Add(item.hqLightmapScaleOffset);
                        batchGroupConfig.mqLightmapScaleOffsets.Add(item.mqLightmapScaleOffset);
                        batchGroupConfig.lqLightmapScaleOffsets.Add(item.lqLightmapScaleOffset);
                    }

                    AssetDatabase.CreateAsset(batchGroupConfig, $"{folder}/batchGroupConfig_{index}.asset");
                }
            }
        }
        #endregion

        #region BigWorldConfig
        private static void CreateBigWorldConfigs()
        {
            var configPath = $"Assets/Resources/BigWorld/{s_worldName}/config";
            if (Directory.Exists(configPath))
            {
                Directory.Delete(configPath, true);
            }
            Directory.CreateDirectory(configPath);
            
            var bigWorldConfig = ScriptableObject.CreateInstance<BigWorldConfig>();
            
            var cellFolders = Directory.GetDirectories($"Assets/Resources/BigWorld/{s_worldName}", "cell_*", SearchOption.TopDirectoryOnly);
            foreach (var folder in cellFolders)
            {
                var folderName = Path.GetFileNameWithoutExtension(folder);
                var folderSs = folderName.Split('_');
                var cellX = Convert.ToInt32(folderSs[1]);
                var cellZ = Convert.ToInt32(folderSs[2]);
                var cellConfig = ScriptableObject.CreateInstance<BigWorldCellConfig>();
                cellConfig.x = cellX;
                cellConfig.z = cellZ;
                bigWorldConfig.cellConfigs.Add(cellConfig);
                
                //计算BatchGroup数量
                var batchGroupConfigs = Directory.GetFiles(folder, "batchGroupConfig_*.asset", SearchOption.TopDirectoryOnly);
                cellConfig.batchGroupCount = batchGroupConfigs.Length;
                
                //计算Lightmap数量
                var hqLightmaps = Directory.GetFiles(folder, "shq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                cellConfig.hqLightmapCount = hqLightmaps.Length;
                var mqLightmaps = Directory.GetFiles(folder, "smq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                cellConfig.mqLightmapCount = mqLightmaps.Length;
                var lqLightmaps = Directory.GetFiles(folder, "slq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                cellConfig.lqLightmapCount = lqLightmaps.Length;
                
                //保存配置
                AssetDatabase.CreateAsset(cellConfig, $"{configPath}/cell_{cellX}_{cellZ}.asset");
            }
            
            AssetDatabase.CreateAsset(bigWorldConfig, $"{configPath}/bigworld.asset");
        }
        #endregion
    }  
}