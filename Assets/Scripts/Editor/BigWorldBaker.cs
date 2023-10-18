using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class BigWorldBaker
{
    #region Old
    //[MenuItem("BigWorld/BakeForBRG")]
    private static void BakeForBRG()
    {
        var sceneRoot = GameObject.Find("Scene");
        if (sceneRoot == null)
        {
            Debug.LogError("Bake Failed");
            return;
        }

        var objectGroups = new List<BigWorldBatchGroupConfig>();

        var treeRoot = sceneRoot.transform.Find("tree");
        if (treeRoot != null)
        {
            BakeLod(treeRoot, objectGroups);
        }

        var stoneRoot = sceneRoot.transform.Find("stone");
        if (stoneRoot != null)
        {
            BakeLod(stoneRoot, objectGroups);
        }

        var grassRoot = sceneRoot.transform.Find("grass");
        if (grassRoot != null)
        {
            BakeNonLod(grassRoot, objectGroups);
        }

        var scene = SceneManager.GetActiveScene();
        var scenePath = scene.path.Replace($"{scene.name}.unity", string.Empty) + "/ObjectGroups";
        if (Directory.Exists(scenePath))
        {
            Directory.Delete(scenePath, true);
        }

        Directory.CreateDirectory(scenePath);
        for (var i = 0; i < objectGroups.Count; ++i)
        {
            AssetDatabase.CreateAsset(objectGroups[i], $"{scenePath}/{objectGroups[i].name}.asset");
        }
        AssetDatabase.Refresh();
        Debug.LogError("Bake Success");
    }

    private static void BakeLod(Transform root, List<BigWorldBatchGroupConfig> objectGroups)
    {
        var lodGroups = root.GetComponentsInChildren<LODGroup>();
        foreach (var lodGroup in lodGroups)
        {
            var lods = lodGroup.GetLODs();
            if (lods.Length <= 1)
            {
                continue;
            }

            var lod0 = lods[0];
            var lod1 = lods[1];
            if (lod0.renderers.Length != lod1.renderers.Length)
            {
                continue;
            }

            for (var i = 0; i < lod0.renderers.Length; ++i)
            {
                var iii = lodGroup.gameObject.name.IndexOf('(');
                var objGroupName = (iii <= 0 ? lodGroup.gameObject.name : lodGroup.gameObject.name.Substring(0, iii)) + "_" + i;
                var group = objectGroups.Find((g) => g.name == objGroupName);
                if (group == null)
                {
                    var renderer0 =(MeshRenderer)lod0.renderers[i];
                    var renderer1 =(MeshRenderer)lod1.renderers[i];
                    group = ScriptableObject.CreateInstance<BigWorldBatchGroupConfig>();
                    group.name = objGroupName;
                    group.lods = new BigWorldBatchGroupConfig.Lod[]
                    {
                        new BigWorldBatchGroupConfig.Lod
                        {
                            mesh = renderer0.GetComponent<MeshFilter>().sharedMesh,
                            material = renderer0.sharedMaterial,
                            lodMinDistance = 0,
                            lodMaxDistance = 15.0f,
                        },
                        new BigWorldBatchGroupConfig.Lod
                        {
                            mesh = renderer1.GetComponent<MeshFilter>().sharedMesh,
                            material = renderer1.sharedMaterial,
                            lodMinDistance = 15.0f,
                            lodMaxDistance = -1.0f,
                        },
                    };

                    group.count = 1;
                    group.positions = new List<Vector3> { lodGroup.transform.position };
                    group.rotations = new List<Quaternion> { lodGroup.transform.rotation };
                    group.scales = new List<Vector3> { lodGroup.transform.lossyScale };
                    group.bounds = new List<AABB> { renderer0.bounds.ToAABB() };
                    objectGroups.Add(group);
                }
                else
                {
                    var renderer0 = (MeshRenderer)lod0.renderers[i];

                    ++group.count;
                    group.positions.Add(lodGroup.transform.position);
                    group.rotations.Add(lodGroup.transform.rotation);
                    group.scales.Add(lodGroup.transform.lossyScale);
                    group.bounds.Add(renderer0.bounds.ToAABB());
                }
            }
        }
    }

    private static void BakeNonLod(Transform root, List<BigWorldBatchGroupConfig> objectGroups)
    {
        var meshRenderers = root.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in meshRenderers)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var material = renderer.sharedMaterial;
            var objGroupName = mesh.name + "_" + material.name;
            var group = objectGroups.Find((g) => g.name == objGroupName);
            if (group == null)
            {
                group = ScriptableObject.CreateInstance<BigWorldBatchGroupConfig>();
                group.name = objGroupName;
                group.lods = new BigWorldBatchGroupConfig.Lod[]
                {
                    new BigWorldBatchGroupConfig.Lod
                    {
                        mesh = mesh,
                        material = material,
                        lodMinDistance = 0,
                        lodMaxDistance = 45.0f,
                    },
                };

                group.count = 1;
                group.positions = new List<Vector3> { renderer.transform.position };
                group.rotations = new List<Quaternion> { renderer.transform.rotation };
                group.scales = new List<Vector3> { renderer.transform.lossyScale };
                group.bounds = new List<AABB> { renderer.bounds.ToAABB() };
                objectGroups.Add(group);
            }
            else
            {
                ++group.count;
                group.positions.Add(renderer.transform.position);
                group.rotations.Add(renderer.transform.rotation);
                group.scales.Add(renderer.transform.lossyScale);
                group.bounds.Add(renderer.bounds.ToAABB());
            }
        }
    }

    //[MenuItem("BigWorld/PlaceObject")]
    private static void PlaceTrees()
    {
        var gameObject = Selection.activeGameObject;
        if (gameObject == null)
        {
            return;
        }

        for (var i = 0; i < 500; ++i)
        {
            var position = new Vector3(UnityEngine.Random.Range(128.0f, 384.0f), 0, UnityEngine.Random.Range(128.0f, 384.0f));
            if (NavMesh.SamplePosition(position, out var hit, 100.0f, NavMesh.AllAreas))
            {
                position = hit.position;
            }
            else
            {
                continue;
            }

            var scaleValue = UnityEngine.Random.Range(0.18f, 0.25f);
            var newGameObject = GameObject.Instantiate(gameObject);
            newGameObject.transform.position = position;
            newGameObject.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            newGameObject.transform.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
        }
    }

    //[MenuItem("BigWorld/PlaceToNavmesh")]
    private static void PlaceToNevmesh()
    {
        var root = GameObject.Find("Scene");
        var lodGroups = root.GetComponentsInChildren<LODGroup>();
        for (var i = 0; i < lodGroups.Length; ++i)
        {
            var child = lodGroups[i].transform;
            if (NavMesh.SamplePosition(child.position, out var hit, 100.0f, NavMesh.AllAreas))
            {
                child.position = hit.position;
            }
        }
    }
    #endregion

    enum LightmapQuality
    {
        High,
        Med,
        Low
    }

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

    private const int lightmapSize = 1024;

    /// <summary>
    /// 大世界烘焙
    /// </summary>
    [MenuItem("BigWorld/Bake")]
    private static void Bake()
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
        BakeLightmap(lightmapSize, 6);
        PostprocessLightmap(batchInfos, LightmapQuality.Low);

        //烘焙中精度
        ClearLightmap();
        BakeLightmap(lightmapSize, 9);
        PostprocessLightmap(batchInfos, LightmapQuality.Med);

        //烘焙高精度
        ClearLightmap();
        BakeLightmap(lightmapSize, 15);
        PostprocessLightmap(batchInfos, LightmapQuality.High);

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

    private static void PostprocessLightmap(Dictionary<string, BakeBatchInfo> batchInfos, LightmapQuality quality)
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
                if (quality == LightmapQuality.High)
                {
                    item.hqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                }
                else if (quality == LightmapQuality.Med)
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
                    lodMinDistance = 15.0f * i,
                    lodMaxDistance = 15.0f * i + 15.0f,
                };
            }
            group.lods[group.lods.Length - 1].lodMaxDistance = -1.0f;

            group.count = batchInfo.items.Count;
            group.positions = new List<Vector3>();
            group.rotations = new List<Quaternion>();
            group.scales = new List<Vector3>();
            group.bounds = new List<AABB>();
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
}
