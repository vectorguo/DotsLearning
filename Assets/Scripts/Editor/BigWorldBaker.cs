using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class BigWorldBaker
{
    //[MenuItem("BigWorld/BakeSceneForECS")]
    //private static void BakeForEntity()
    //{
    //    var sceneRoot = GameObject.Find("Scene");
    //    if (sceneRoot == null)
    //    {
    //        Debug.LogError("Bake Failed");
    //        return;
    //    }

    //    var objectGroups = new List<BigWorldObjectGroup>();
        
    //    var meshRenderers = sceneRoot.GetComponentsInChildren<MeshRenderer>();
    //    foreach (var meshRenderer in meshRenderers)
    //    {
    //        if (meshRenderer.sharedMaterial != null && meshRenderer.gameObject.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
    //        {
    //            var material = meshRenderer.sharedMaterial;
    //            var mesh = meshFilter.sharedMesh;
                
    //            var transform = meshRenderer.transform;
    //            var position = transform.position;
    //            var rotation = transform.rotation;
    //            var scale = transform.lossyScale;
                
    //            var group = objectGroups.Find((g) => g.material == material && g.mesh == mesh);
    //            if (group == null)
    //            {
    //                group = ScriptableObject.CreateInstance<BigWorldObjectGroup>();
    //                group.material = material;
    //                group.mesh = mesh;
    //                group.count = 1;
    //                group.positions = new List<float3> { new float3(position.x, position.y, position.z) };
    //                group.rotations = new List<quaternion> { new quaternion(rotation.x, rotation.y, rotation.z, rotation.w) };
    //                group.scales = new List<float3> { new float3(scale.x, scale.y, scale.z) };
    //                objectGroups.Add(group);
    //            }
    //            else
    //            {
    //                ++group.count;
    //                group.positions.Add(new float3(position.x, position.y, position.z));
    //                group.rotations.Add(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
    //                group.scales.Add(new float3(scale.x, scale.y, scale.z));
    //            }
    //        }
    //    }

    //    var scene = SceneManager.GetActiveScene();
    //    var scenePath = scene.path.Replace($"{scene.name}.unity", string.Empty) + "/ObjectGroups";
    //    if (Directory.Exists(scenePath))
    //    {
    //        Directory.Delete(scenePath, true);
    //    }

    //    Directory.CreateDirectory(scenePath);
    //    for (var i = 0; i < objectGroups.Count; ++i)
    //    {
    //        AssetDatabase.CreateAsset(objectGroups[i], $"{scenePath}/{i}.asset");
    //    }
    //    AssetDatabase.Refresh();
    //    Debug.LogError("Bake Success");
    //}

    [MenuItem("BigWorld/BakeForBRG")]
    private static void BakeForBRG()
    {
        var sceneRoot = GameObject.Find("Scene");
        if (sceneRoot == null)
        {
            Debug.LogError("Bake Failed");
            return;
        }

        var objectGroups = new List<BigWorldObjectGroupForBRG>();

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

    private static void BakeLod(Transform root, List<BigWorldObjectGroupForBRG> objectGroups)
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
                    group = ScriptableObject.CreateInstance<BigWorldObjectGroupForBRG>();
                    group.name = objGroupName;
                    group.lods = new BigWorldObjectGroupForBRG.Lod[]
                    {
                        new BigWorldObjectGroupForBRG.Lod
                        {
                            mesh = renderer0.GetComponent<MeshFilter>().sharedMesh,
                            material = renderer0.sharedMaterial,
                            lodMinDistance = 0,
                            lodMaxDistance = 15.0f,
                        },
                        new BigWorldObjectGroupForBRG.Lod
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

    private static void BakeNonLod(Transform root, List<BigWorldObjectGroupForBRG> objectGroups)
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
                group = ScriptableObject.CreateInstance<BigWorldObjectGroupForBRG>();
                group.name = objGroupName;
                group.lods = new BigWorldObjectGroupForBRG.Lod[]
                {
                    new BigWorldObjectGroupForBRG.Lod
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

    [MenuItem("BigWorld/PlaceObject")]
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

    [MenuItem("BigWorld/PlaceToNavmesh")]
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
}
