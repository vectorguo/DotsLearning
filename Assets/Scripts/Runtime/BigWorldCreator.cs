using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public class BigWorldCreator : MonoBehaviour
{
    public List<BigWorldObjectGroup> objectGroups;
    
    void Start()
    {
        if (objectGroups == null || objectGroups.Count == 0)
        {
            return;
        }
        
        //创建RenderMeshArray和RenderBounds
        var objectCount = 0;
        var materials = new Material[objectGroups.Count];
        var meshes = new Mesh[objectGroups.Count];
        var renderBounds = new NativeArray<RenderBounds>(objectGroups.Count, Allocator.TempJob);
        for (var i = 0; i < objectGroups.Count; ++i)
        {
            var group = objectGroups[i];
            objectCount += group.count;
            materials[i] = group.material;
            meshes[i] = group.mesh;
            renderBounds[i] = new RenderBounds { Value = group.mesh.bounds.ToAABB() };
        }
        var renderMeshArray = new RenderMeshArray(materials, meshes);
        
        //Transform数据
        var renderIndices = new NativeArray<int>(objectCount, Allocator.TempJob);
        var positions = new NativeArray<float3>(objectCount, Allocator.TempJob);
        var rotations = new NativeArray<quaternion>(objectCount, Allocator.TempJob);
        var scales = new NativeArray<float3>(objectCount, Allocator.TempJob);
        for (int i = 0, objectIndex = 0; i < objectGroups.Count; ++i)
        {
            var group = objectGroups[i];
            for (var j = 0; j < group.count; ++j)
            {
                renderIndices[objectIndex] = i;
                positions[objectIndex] = group.positions[j];
                rotations[objectIndex] = group.rotations[j];
                scales[objectIndex] = group.scales[j];
                ++objectIndex;
            }
        }
        
        //创建RenderMeshDescription
        var filterSettings = RenderFilterSettings.Default;
        filterSettings.ShadowCastingMode = ShadowCastingMode.Off;
        filterSettings.ReceiveShadows = false;
        var renderMeshDescription = new RenderMeshDescription
        {
            FilterSettings = filterSettings,
            LightProbeUsage = LightProbeUsage.Off
        };
        
        //创建Entity模板
        var entityWorld = World.DefaultGameObjectInjectionWorld;
        var entityManager = entityWorld.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var entityTemplate = entityManager.CreateEntity();
        RenderMeshUtility.AddComponents(
            entityTemplate,
            entityManager,
            renderMeshDescription,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        
        //创建SpawnJob
        var job = new SpawnJob
        {
            entityTemplate = entityTemplate,
            ecb = ecb.AsParallelWriter(),
            positions = positions,
            rotations = rotations,
            scales = scales,
            renderBounds = renderBounds,
            renderIndices = renderIndices
        };
        var jobHandle = job.Schedule(objectCount, 128);
        positions.Dispose(jobHandle);
        rotations.Dispose(jobHandle);
        scales.Dispose(jobHandle);
        renderBounds.Dispose(jobHandle);
        renderIndices.Dispose(jobHandle);
        jobHandle.Complete();
        
        //ecb回放
        ecb.Playback(entityManager);
        ecb.Dispose();
        entityManager.DestroyEntity(entityTemplate);
    }
    
    #region SpawnJob
    public struct SpawnJob : IJobParallelFor
    {
        public Entity entityTemplate;
        public EntityCommandBuffer.ParallelWriter ecb;
        
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<quaternion> rotations;
        [ReadOnly] public NativeArray<float3> scales;
        [ReadOnly] public NativeArray<RenderBounds> renderBounds;
        [ReadOnly] public NativeArray<int> renderIndices;

        public void Execute(int index)
        {
            var renderIndex = renderIndices[index];
            var entity = ecb.Instantiate(index, entityTemplate);
            ecb.SetComponent(index, entity, new LocalToWorld { Value = float4x4.TRS(positions[index], rotations[index], scales[index]) });
            ecb.SetComponent(index, entity, MaterialMeshInfo.FromRenderMeshArrayIndices(renderIndex, renderIndex));
            ecb.SetComponent(index, entity, renderBounds[renderIndex]);
        }
    }
    #endregion
}
