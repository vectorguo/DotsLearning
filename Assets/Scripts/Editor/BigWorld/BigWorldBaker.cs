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
                    for (var i = 0; i < bakeGroup.items.Count; ++i)
                    {
                        var item = bakeGroup.items[i];
                        batchGroupConfig.positions.Add(item.renderer.transform.position);
                        batchGroupConfig.rotations.Add(item.renderer.transform.rotation);
                        batchGroupConfig.scales.Add(item.renderer.transform.lossyScale);
                        batchGroupConfig.bounds.Add(item.renderer.bounds.ToAABB());
                        batchGroupConfig.hqLightmapIndices.Add(item.hqLightmapIndex);
                        batchGroupConfig.mqLightmapIndices.Add(item.mqLightmapIndex);
                        batchGroupConfig.lqLightmapIndices.Add(item.lqLightmapIndex);
                        batchGroupConfig.hqLightmapScaleOffsets.Add(item.hqLightmapScaleOffset);
                        batchGroupConfig.mqLightmapScaleOffsets.Add(item.mqLightmapScaleOffset);
                        batchGroupConfig.lqLightmapScaleOffsets.Add(item.lqLightmapScaleOffset);
                    }

                    AssetDatabase.CreateAsset(batchGroupConfig, $"Assets/Resources/BigWorld/{s_worldName}/{cellX}_{cellZ}/batchGroupConfig_{index}.asset");
                }
            }
        }
        #endregion

        #region OLD
        class BakeBatchItemInfo
        {
            public MeshRenderer renderer;
            public Vector4 hqLightmapScaleOffset;
            public Vector4 mqLightmapScaleOffset;
            public Vector4 lqLightmapScaleOffset;
        }
        
        class BakeBatchLodInfo
        {
            public Mesh mesh;
            public Material material;
        }
        
        class BakeBatchInfo
        {
            public string batchName;
            public readonly List<BakeBatchLodInfo> lods = new List<BakeBatchLodInfo>();
            public readonly List<BakeBatchItemInfo> items = new List<BakeBatchItemInfo>();
        }

        private const int c_lightmapSize = 1024;

        /// <summary>
        /// 大世界烘焙
        /// </summary>
        //[MenuItem("BigWorld/Bake")]
        private static void BakeOld()
        {
            var sceneRoot = GameObject.Find("Scene");
            if (sceneRoot == null)
            {
                return;
            }
            
            //首先划分批次
            var batchInfos = new Dictionary<string, BakeBatchInfo>();
            var lodGroups = sceneRoot.GetComponentsInChildren<LODGroup>();
            foreach (var lodGroup in lodGroups)
            {
                if (lodGroup.lodCount == 0)
                {
                    continue;
                }
                var lods = lodGroup.GetLODs();
                var renderCount = lods[0].renderers.Length;
                for (var i = 0; i < renderCount; ++i)
                {
                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(lodGroup.gameObject);
                    var batchName = $"{Path.GetFileNameWithoutExtension(prefabPath)}_{i}";
                    if (!batchInfos.TryGetValue(batchName, out var batchInfo))
                    {
                        batchInfo = new BakeBatchInfo { batchName = batchName };
                        batchInfos.Add(batchName, batchInfo);
                    }

                    batchInfo.items.Add(new BakeBatchItemInfo
                    {
                        renderer = lods[0].renderers[i] as MeshRenderer
                    });

                    if (batchInfo.lods.Count == 0)
                    {
                        for (var j = 0; j < lodGroup.lodCount; ++j)
                        {
                            var renderer = lods[j].renderers[i];
                            batchInfo.lods.Add(new BakeBatchLodInfo
                            {
                                mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
                                material = renderer.sharedMaterial,
                            });
                        }
                    }
                }
            }

            //设置Lightmap烘焙参数
            ClearLightmapParams();
            var bakeTag = 0;
            foreach (var pair in batchInfos)
            {
                var lightmapParam = new LightmapParameters()
                {
                    bakedLightmapTag = 100 + bakeTag++,
                    limitLightmapCount = true,
                    maxLightmapCount = 1,
                };

                foreach (var item in pair.Value.items)
                {
                    var renderer = item.renderer;
                    renderer.scaleInLightmap = 1.0f;
                    renderer.stitchLightmapSeams = false;

                    var so = new SerializedObject(renderer);
                    var sp = so.FindProperty("m_LightmapParameters");
                    sp.objectReferenceValue = lightmapParam;
                    so.ApplyModifiedProperties();
                    
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
                }
            }

            //烘焙低精度
            ClearLightmap();
            BakeLightmap(c_lightmapSize, 6);
            PostprocessLightmap(batchInfos, BigWorldBakerHelper.LightmapQuality.Low);

            //烘焙中精度
            ClearLightmap();
            BakeLightmap(c_lightmapSize, 9);
            PostprocessLightmap(batchInfos, BigWorldBakerHelper.LightmapQuality.Med);

            //烘焙高精度
            ClearLightmap();
            BakeLightmap(c_lightmapSize, 15);
            PostprocessLightmap(batchInfos, BigWorldBakerHelper.LightmapQuality.High);

            //创建数据
            CreateObjectGroup(batchInfos);

            //烘焙完成
            AssetDatabase.Refresh();
            Debug.LogError("烘焙完成");
        }

        /// <summary>
        /// 清理Lightmap
        /// </summary>
        private static void ClearLightmap()
        {
            Lightmapping.Clear();
        }

        private static void ClearLightmapParams()
        {
            for (var i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                foreach (var root in scene.GetRootGameObjects())
                {
                    var children = root.GetComponentsInChildren<Transform>();
                    foreach (var child in children)
                    {
                        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                    }
                }
            }
        }

        private static void BakeLightmap(int atlasSize, float bakeResolution)
        {
            Lightmapping.lightingSettings.lightmapMaxSize = atlasSize;
            Lightmapping.lightingSettings.lightmapResolution = bakeResolution;
            Lightmapping.lightingSettings.directionalityMode = LightmapsMode.NonDirectional;
            Lightmapping.lightingSettings.lightmapCompression = LightmapCompression.None;
            Lightmapping.Bake();
        }

        private static void PostprocessLightmap(Dictionary<string, BakeBatchInfo> batchInfos, BigWorldBakerHelper.LightmapQuality quality)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var pair in batchInfos)
            {
                var firstRenderer = pair.Value.items[0].renderer;
                var lightmapIndex = firstRenderer.lightmapIndex;
                var lightmapTexture = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
                var lightmapPath = AssetDatabase.GetAssetPath(lightmapTexture);
                AssetDatabase.CopyAsset(lightmapPath, $"Assets/Resources/Lightmaps/{scene.name}/{quality}/{pair.Key}.exr");

                foreach (var item in pair.Value.items)
                {
                    if (quality == BigWorldBakerHelper.LightmapQuality.High)
                    {
                        item.hqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                    else if (quality == BigWorldBakerHelper.LightmapQuality.Med)
                    {
                        item.mqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                    else
                    {
                        item.lqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                }
            }
            AssetDatabase.Refresh();
        }

        private static void CreateObjectGroup(Dictionary<string, BakeBatchInfo> batchInfos)
        {
            var scene = SceneManager.GetActiveScene();
            var scenePath = scene.path.Replace($"{scene.name}.unity", string.Empty) + "/ObjectGroups";
            if (Directory.Exists(scenePath))
            {
                Directory.Delete(scenePath, true);
            }
            Directory.CreateDirectory(scenePath);

            foreach (var pair in  batchInfos)
            {
                var batchInfo = pair.Value;
                var group = ScriptableObject.CreateInstance<BigWorldBatchGroupConfig>();
                group.name = batchInfo.batchName;
                group.lods = new BigWorldBatchGroupConfig.Lod[batchInfo.lods.Count];
                for (var i = 0; i < batchInfo.lods.Count; ++i)
                {
                    group.lods[i] = new BigWorldBatchGroupConfig.Lod
                    {
                        mesh = batchInfo.lods[i].mesh,
                        material = batchInfo.lods[i].material,
                        lodMinDistance = 50.0f * i,
                        lodMaxDistance = 50.0f * i + 50.0f,
                    };
                }
                group.lods[group.lods.Length - 1].lodMaxDistance = -1.0f;

                group.count = batchInfo.items.Count;
                group.positions = new List<Vector3>();
                group.rotations = new List<Quaternion>();
                group.scales = new List<Vector3>();
                group.bounds = new List<AABB>();
                group.hqLightmapIndices = new List<int>();
                group.mqLightmapIndices = new List<int>();
                group.lqLightmapIndices = new List<int>();
                group.hqLightmapScaleOffsets = new List<Vector4>();
                group.mqLightmapScaleOffsets = new List<Vector4>();
                group.lqLightmapScaleOffsets = new List<Vector4>();
                for (var i = 0; i < batchInfo.items.Count; ++i)
                {
                    var item = batchInfo.items[i];
                    group.positions.Add(item.renderer.transform.position);
                    group.rotations.Add(item.renderer.transform.rotation);
                    group.scales.Add(item.renderer.transform.lossyScale);
                    group.bounds.Add(item.renderer.bounds.ToAABB());
                    group.hqLightmapScaleOffsets.Add(item.hqLightmapScaleOffset);
                    group.mqLightmapScaleOffsets.Add(item.mqLightmapScaleOffset);
                    group.lqLightmapScaleOffsets.Add(item.lqLightmapScaleOffset);
                }

                AssetDatabase.CreateAsset(group, $"{scenePath}/{group.name}.asset");
            }

            AssetDatabase.Refresh();
        }
        #endregion
    }  
}