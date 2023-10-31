using BigCat.BigWorld;
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// 烘焙输出目录
        /// </summary>
        public static string bakeTempOutputPath => $"{Application.dataPath.Replace("Assets", string.Empty)}BigWorldOutput/{s_worldName}";

        [MenuItem("BigWorld/Bake")]
        public static void Bake()
        {
            //删除旧资源
            ClearOldResources();

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
            var sceneBakeData = BigWorldBakerHelper.GetSceneBakeData();

            //烘焙Lightmap
            BigWorldLightmapBaker.BakeLightmap(sceneBakeData);
            BigWorldLightmapBaker.PostprocessLightmap();

            //创建BatchGroup配置
            CreateBatchGroupConfigs(sceneBakeData);

            //创建大世界运行时配置
            CreateBigWorldConfigs();

            //烘焙完成
            AssetDatabase.Refresh();
            Debug.LogError("烘焙完成");
        }

        #region BatchGroupConfig
        private static void CreateBatchGroupConfigs(Dictionary<int, BigWorldBakerHelper.BigWorldBakeDataOfCell> bakeGroupsMap)
        {
            foreach (var pair in bakeGroupsMap)
            {
                BigWorldUtility.GetCellCoordinates(pair.Key, out var cellX, out var cellZ);

                for (var index = 0; index < pair.Value.bakeGroups.Count; ++index)
                {
                    var bakeGroup = pair.Value.bakeGroups[index];
                    var batchGroupConfig = ScriptableObject.CreateInstance<BigWorldObjectBatchGroupConfig>();

                    //LOD
                    batchGroupConfig.lods = new BigWorldObjectBatchGroupConfig.Lod[bakeGroup.lods.Count];
                    for (var i = 0; i < bakeGroup.lods.Count; ++i)
                    {
                        batchGroupConfig.lods[i] = new BigWorldObjectBatchGroupConfig.Lod
                        {
                            mesh = bakeGroup.lods[i].mesh,
                            material = bakeGroup.lods[i].material,
                            lodMinDistance = 30.0f * i,
                            lodMaxDistance = 30.0f * i + 30.0f,
                        };
                    }
                    batchGroupConfig.lods[bakeGroup.lods.Count - 1].lodMaxDistance = -1.0f;

                    //计算Lightmap偏移
                    var folder = $"Assets/Resources/BigWorld/{s_worldName}/block/block_{cellX}_{cellZ}";
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
            var config = ScriptableObject.CreateInstance<BigWorldConfig>();
            var chunkConfigs = new Dictionary<int, BigWorldChunkConfig>();

            var blockFolders = Directory.GetDirectories($"Assets/Resources/BigWorld/{s_worldName}/block", "block_*", SearchOption.TopDirectoryOnly);
            foreach (var folder in blockFolders)
            {
                var folderName = Path.GetFileNameWithoutExtension(folder);
                var folderSs = folderName.Split('_');
                var blockX = Convert.ToInt32(folderSs[1]);
                var blockZ = Convert.ToInt32(folderSs[2]);
                var blockIndex = BigWorldUtility.GetCellIndex(blockX, blockZ);
                var blockConfig = ScriptableObject.CreateInstance<BigWorldBlockConfig>();

                //计算BatchGroup数量
                var batchGroupConfigs = Directory.GetFiles(folder, "batchGroupConfig_*.asset", SearchOption.TopDirectoryOnly);
                blockConfig.batchGroupCount = batchGroupConfigs.Length;

                //计算Lightmap数量
                var hqLightmaps = Directory.GetFiles(folder, "shq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                blockConfig.hqLightmapCount = hqLightmaps.Length;
                var mqLightmaps = Directory.GetFiles(folder, "smq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                blockConfig.mqLightmapCount = mqLightmaps.Length;
                var lqLightmaps = Directory.GetFiles(folder, "slq_lightmap_*.exr", SearchOption.TopDirectoryOnly);
                blockConfig.lqLightmapCount = lqLightmaps.Length;

                //保存Block配置
                AssetDatabase.CreateAsset(blockConfig, $"{folder}/config.asset");

                var chunkX = blockX / 4;
                var chunkZ = blockZ / 4;
                var chunkIndex = BigWorldUtility.GetCellIndex(chunkX, chunkZ);
                if (chunkConfigs.TryGetValue(chunkIndex, out var chunkConfig))
                {
                    chunkConfig.blockIndices.Add(blockIndex);
                }
                else
                {
                    chunkConfigs.Add(chunkIndex, new BigWorldChunkConfig
                    {
                        blockIndices = new List<int> { blockIndex }
                    });
                }
            }

            //保存Chunk配置
            foreach (var pair in chunkConfigs)
            {
                BigWorldUtility.GetCellCoordinates(pair.Key, out var chunkX, out var chunkZ);

                //检查保存路径
                var chunkConfigPath = $"Assets/Resources/BigWorld/{s_worldName}/chunk/chunk_{chunkX}_{chunkZ}";
                if (Directory.Exists(chunkConfigPath))
                {
                    Directory.Delete(chunkConfigPath, true);
                }
                Directory.CreateDirectory(chunkConfigPath);

                //保存配置
                AssetDatabase.CreateAsset(pair.Value, $"{chunkConfigPath}/config.asset");
            }

            //保存大世界配置
            config.chunkIndices = new List<int>(chunkConfigs.Keys);
            AssetDatabase.CreateAsset(config, $"Assets/Resources/BigWorld/{s_worldName}/bigworld.asset");
        }
        #endregion

        #region Utility
        /// <summary>
        /// 清理旧资源
        /// </summary>
        private static void ClearOldResources()
        {
            var path1 = $"Assets/Resources/BigWorld/{s_worldName}";
            if (Directory.Exists(path1))
            {
                Directory.Delete(path1, true);
            }
            Directory.CreateDirectory(path1);

            if (Directory.Exists(bakeTempOutputPath))
            {
                Directory.Delete(bakeTempOutputPath, true);
            }
            Directory.CreateDirectory(bakeTempOutputPath);

            //刷新
            AssetDatabase.Refresh();
        }
        #endregion
    }
}