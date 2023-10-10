using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BigWorldBaker
{
    [MenuItem("BigWorld/BakeSceneForECS")]
    private static void BakeForEntity()
    {
        var sceneRoot = GameObject.Find("Scene");
        if (sceneRoot == null)
        {
            Debug.LogError("Bake Failed");
            return;
        }

        var objectGroups = new List<BigWorldObjectGroup>();
        
        var meshRenderers = sceneRoot.GetComponentsInChildren<MeshRenderer>();
        foreach (var meshRenderer in meshRenderers)
        {
            if (meshRenderer.sharedMaterial != null && meshRenderer.gameObject.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                var material = meshRenderer.sharedMaterial;
                var mesh = meshFilter.sharedMesh;
                
                var transform = meshRenderer.transform;
                var position = transform.position;
                var rotation = transform.rotation;
                var scale = transform.lossyScale;
                
                var group = objectGroups.Find((g) => g.material == material && g.mesh == mesh);
                if (group == null)
                {
                    group = ScriptableObject.CreateInstance<BigWorldObjectGroup>();
                    group.material = material;
                    group.mesh = mesh;
                    group.count = 1;
                    group.positions = new List<float3> { new float3(position.x, position.y, position.z) };
                    group.rotations = new List<quaternion> { new quaternion(rotation.x, rotation.y, rotation.z, rotation.w) };
                    group.scales = new List<float3> { new float3(scale.x, scale.y, scale.z) };
                    objectGroups.Add(group);
                }
                else
                {
                    ++group.count;
                    group.positions.Add(new float3(position.x, position.y, position.z));
                    group.rotations.Add(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
                    group.scales.Add(new float3(scale.x, scale.y, scale.z));
                }
            }
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
            AssetDatabase.CreateAsset(objectGroups[i], $"{scenePath}/{i}.asset");
        }
        AssetDatabase.Refresh();
        Debug.LogError("Bake Success");
    }
}
