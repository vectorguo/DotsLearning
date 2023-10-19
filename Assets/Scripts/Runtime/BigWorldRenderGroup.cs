using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
public class BigWorldRenderGroup : MonoBehaviour
{
    #region 常量数据
    private const uint c_sizeOfMatrix = sizeof(float) * 4 * 4;
    private const uint c_sizeOfPackedMatrix = sizeof(float) * 4 * 3;
    private const uint c_sizeOfFloat4 = sizeof(float) * 4;
    private const uint c_sizeOfGBufferHead = c_sizeOfPackedMatrix * 2;
    private const uint c_sizeOfPerInstance = (c_sizeOfPackedMatrix + c_sizeOfFloat4 + sizeof(int) - 1) / sizeof(int) * sizeof(int);  //确保是sizeof(int)的整数倍
    #endregion

    #region PackedMatrix
    struct PackedMatrix
    {
        public float c0x; public float c0y; public float c0z;
        public float c1x; public float c1y; public float c1z;
        public float c2x; public float c2y; public float c2z;
        public float c3x; public float c3y; public float c3z;
        
        public PackedMatrix(Matrix4x4 m)
        {
            c0x = m.m00; c0y = m.m10; c0z = m.m20;
            c1x = m.m01; c1y = m.m11; c1z = m.m21;
            c2x = m.m02; c2y = m.m12; c2z = m.m22;
            c3x = m.m03; c3y = m.m13; c3z = m.m23;
        }
    }
    #endregion

    #region BatchInfo
    public class BigWorldBatch
    {
        public BatchID batchID;
        public int batchIndex;
        public int instanceCount;
        public int instanceOffset;
        public int visibleOffset;
        public GraphicsBuffer instanceData;

        /// <summary>
        /// 用于存储每个Instance的Lightmap数据
        /// </summary>
        private NativeArray<float> m_systemBufferLightmapST;
        private NativeArray<float> m_systemBufferLightmapIndex;


        public BigWorldBatch(int instanceOffset, int instanceCount, int lodLevel, BigWorldBatchGroupConfig batchGroupConfig, BatchRendererGroup brg)
        {
            this.instanceCount = instanceCount;
            this.instanceOffset = instanceOffset;

            //填充数据
            var lightmapScaleOffsets = lodLevel == 0 ? batchGroupConfig.hqLightmapScaleOffsets : (lodLevel == 1 ? batchGroupConfig.mqLightmapScaleOffsets : batchGroupConfig.lqLightmapScaleOffsets);
            var localToWorld = new PackedMatrix[instanceCount];
            var lightmapScalOffset = new float4[instanceCount];
            for (var i = 0; i < instanceCount; ++i)
            {
                var index = instanceOffset + i;
                localToWorld[i] = new PackedMatrix(Matrix4x4.TRS(batchGroupConfig.positions[index], batchGroupConfig.rotations[index], batchGroupConfig.scales[index]));
                lightmapScalOffset[i] = new float4(lightmapScaleOffsets[index]);
            }

            //创建GBuffer
            uint byteAddressLocalToWorld = c_sizeOfGBufferHead;
            uint byteAddressLightmapScalOffset = byteAddressLocalToWorld + (uint)(c_sizeOfPackedMatrix * instanceCount);
            var buffSize = instanceCount * c_sizeOfPerInstance + c_sizeOfGBufferHead;
            instanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, (int)buffSize / sizeof(int), sizeof(int));
            instanceData.SetData(new[] { Matrix4x4.zero }, 0, 0, 1);
            instanceData.SetData(localToWorld, 0, (int)(byteAddressLocalToWorld / c_sizeOfPackedMatrix), localToWorld.Length);
            instanceData.SetData(lightmapScalOffset, 0, (int)(byteAddressLightmapScalOffset / c_sizeOfFloat4), lightmapScalOffset.Length);

            //metadata
            var metadata = new NativeArray<MetadataValue>(2, Allocator.Temp);
            metadata[0] = new MetadataValue { NameID = Shader.PropertyToID("unity_ObjectToWorld"), Value = 0x80000000 | byteAddressLocalToWorld };
            metadata[1] = new MetadataValue { NameID = Shader.PropertyToID("_LightmapST"), Value = 0x80000000 | byteAddressLightmapScalOffset };

            //add batch
            batchID = brg.AddBatch(metadata, instanceData.bufferHandle);
        }
    }

    public class BigWorldBatchLod
    {
        public BatchMeshID meshID;
        public BatchMaterialID materialID;

        public float lodMinDistance;
        public float lodMinDistanceSq => lodMinDistance * lodMinDistance;

        public float lodMaxDistance;
        public float lodMaxDistanceSq => lodMaxDistance * lodMaxDistance;

        public BigWorldBatch[] batches;

        public BigWorldBatchLod(BatchRendererGroup brg, BigWorldBatchGroupConfig group, int lodLevel, Texture2DArray lightmaps)
        {
            var lodConfig = group.lods[lodLevel];

            //创建材质
            var material = new Material(lodConfig.material);
            material.SetFloat("_LightmapIndex", lodLevel);
            material.SetTexture("_Lightmaps", lightmaps);
            material.EnableKeyword("LIGHTMAP_ON");

            meshID = brg.RegisterMesh(lodConfig.mesh);
            materialID = brg.RegisterMaterial(material);
            lodMinDistance = lodConfig.lodMinDistance;
            lodMaxDistance = lodConfig.lodMaxDistance;

            //创建Batch
            var batchCount = (group.count + maxInstanceCountPerBatch - 1) / maxInstanceCountPerBatch;
            batches = new BigWorldBatch[batchCount];
            for (var i = 0; i < batchCount; ++i)
            {
                var instanceOffset = i * maxInstanceCountPerBatch;
                var instanceCount = Math.Min(maxInstanceCountPerBatch, group.count - instanceOffset);
                batches[i] = new BigWorldBatch(instanceOffset, instanceCount, lodLevel, group, brg);
            }
        }
    }

    public class BigWorldBatchGroup
    {
        public BigWorldBatchLod[] lods;

        public NativeArray<float3> positions;
        public NativeArray<AABB> bounds;

        /// <summary>
        /// Lightmap
        /// </summary>
        private Texture2DArray m_lightmaps;

        public BigWorldBatchGroup(BatchRendererGroup brg, BigWorldBatchGroupConfig group)
        {
            //加载Lightmap
            var hqLightmap = Resources.Load<Texture2D>($"Lightmaps/MondCity/High/{group.name}");
            var mqLightmap = Resources.Load<Texture2D>($"Lightmaps/MondCity/Med/{group.name}");
            var lqLightmap = Resources.Load<Texture2D>($"Lightmaps/MondCity/Low/{group.name}");
            m_lightmaps = new Texture2DArray(1024, 1024, 3, hqLightmap.format, false);
            m_lightmaps.SetPixelData(hqLightmap.GetPixelData<byte>(0), 0, 0);
            m_lightmaps.SetPixelData(mqLightmap.GetPixelData<byte>(0), 0, 1);
            m_lightmaps.SetPixelData(lqLightmap.GetPixelData<byte>(0), 0, 2);
            m_lightmaps.Apply();

            lods = new BigWorldBatchLod[group.lods.Length];
            for (var i = 0; i < group.lods.Length; ++i)
            {
                lods[i] = new BigWorldBatchLod(brg, group, i, m_lightmaps);
            }

            positions = new NativeArray<float3>(group.count, Allocator.Persistent);
            bounds = new NativeArray<AABB>(group.count, Allocator.Persistent);
            for (var i = 0; i < group.count; ++i)
            {
                positions[i] = group.positions[i];
                bounds[i] = group.bounds[i];
            }
        }

        public void Destroy()
        {
            positions.Dispose();
            bounds.Dispose();
        }
    }
    #endregion

    #region BatchInfoForJob
    [BurstCompile]
    public struct BigWorldBatchGroupForJob
    {
        public NativeArray<float3> positions;
        public NativeArray<AABB> bounds;
    }
    #endregion

    #region CullJob
    [BurstCompile]
    public unsafe struct BatchCullJob : IJob
    {
        [NativeDisableUnsafePtrRestriction][WriteOnly] public BatchCullingOutputDrawCommands* drawCommands;
        [NativeDisableUnsafePtrRestriction][WriteOnly] public BatchDrawCommand* drawCommand;

        /// <summary>
        /// 视锥体裁剪平面
        /// </summary>
        [ReadOnly] public NativeArray<float4> cullingPlanes;

        /// <summary>
        /// Instance位置
        /// </summary>
        [ReadOnly] public NativeArray<float3> positions;

        /// <summary>
        /// Instance包围盒
        /// </summary>
        [ReadOnly] public NativeArray<AABB> bounds;

        /// <summary>
        /// Batch的Instance数量
        /// </summary>
        public int instanceCount;

        /// <summary>
        /// Batch的Instance在BatchGroup里的偏移
        /// </summary>
        public int instanceOffset;

        /// <summary>
        /// Batch的可见物体在DrawCommands可见性列表的偏移
        /// </summary>
        public int visibleOffset;

        /// <summary>
        /// 裁剪距离的平方
        /// </summary>
        public float cullDistanceSq;

        /// <summary>
        /// LOD距离
        /// </summary>
        public float lodMaxDistance;
        public float lodMinDistanceSq;
        public float lodMaxDistanceSq;

        /// <summary>
        /// 玩家位置
        /// </summary>
        public float3 playerPosition;

        [BurstCompile]
        public void Execute()
        {
            int visibleCount = 0;
            for (var i = 0; i < instanceCount; ++i)
            {
                //检测可见性
                var instanceIndex = instanceOffset + i;
                var lengthSq = math.lengthsq(playerPosition - positions[instanceIndex]);
                if (lengthSq <= cullDistanceSq)
                {
                    var intersectResult = Unity.Rendering.FrustumPlanes.Intersect(cullingPlanes, bounds[instanceIndex]);
                    if (intersectResult != Unity.Rendering.FrustumPlanes.IntersectResult.Out)
                    {
                        if (lengthSq >= lodMinDistanceSq && (lengthSq < lodMaxDistanceSq || lodMaxDistance < 0))
                        {
                            drawCommands->visibleInstances[visibleOffset + visibleCount] = i;
                            ++visibleCount;
                        }
                    }
                }
            }

            drawCommand->visibleCount = (uint)visibleCount;
            drawCommand->visibleOffset = (uint)visibleOffset;
        }
    }
    #endregion

    /// <summary>
    /// maxGraphicsBufferSize
    /// </summary>
    public static long maxGraphicsBufferSize = 16 * 1024;

    /// <summary>
    /// 每个Batch的最大Instance数量
    /// </summary>
    public static int maxInstanceCountPerBatch = 0;

    /// <summary>
    /// 草数据
    /// </summary>
    public BigWorldBatchGroupConfig[] batchGroupConfigs;

    /// <summary>
    /// Player
    /// </summary>
    public Transform player;

    /// <summary>
    /// 裁剪距离
    /// </summary>
    public float cullDistance;

    /// <summary>
    /// 是否使用Job来裁剪
    /// </summary>
    public bool useJobForCulling;
    
    /// <summary>
    /// brg
    /// </summary>
    private BatchRendererGroup m_brg;

    /// <summary>
    /// Batch数据
    /// </summary>
    private BigWorldBatchGroup[] m_batchGroups;
    
    private void Start()
    {
        Application.targetFrameRate = 60;

        if (batchGroupConfigs == null || batchGroupConfigs.Length == 0 || player == null)
        {
            return;
        }

        //初始化参数
        maxGraphicsBufferSize = Math.Min(maxGraphicsBufferSize, SystemInfo.maxGraphicsBufferSize);
        maxInstanceCountPerBatch = (int)((maxGraphicsBufferSize - c_sizeOfGBufferHead) / c_sizeOfPerInstance);

        //创建BRG
        m_brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        //创建Batch数据
        m_batchGroups = new BigWorldBatchGroup[batchGroupConfigs.Length];
        for (var i = 0; i < batchGroupConfigs.Length; ++i)
        {
            m_batchGroups[i] = new BigWorldBatchGroup(m_brg, batchGroupConfigs[i]);
        }
    }

    private void OnDisable()
    {
        if (m_brg != null)
        {
            m_brg.Dispose();
        }

        foreach (var batchInfo in m_batchGroups)
        {
            batchInfo.Destroy();
        }

        foreach (var config in batchGroupConfigs)
        {
            config.Destroy();
        }
    }

    /// <summary>
    /// 添加BatchGroup
    /// </summary>
    /// <param name="batchGroupConfig">配置信息</param>
    public void AddBatchGroup(BigWorldBatchGroupConfig batchGroupConfig)
    {

    }

    [BurstCompile]
    private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        //统计Batch数据，并更新每个Batch的VisibleOffset
        var batchCount = 0;
        var instanceCount = 0;
        foreach (var batchGroup in m_batchGroups)
        {
            foreach (var batchLod in batchGroup.lods)
            {
                foreach (var batch in batchLod.batches)
                {
                    batch.batchIndex = batchCount++;
                    batch.visibleOffset = instanceCount;
                    instanceCount += batch.instanceCount;
                }
            }
        }

        //DrawCommand
        var alignment = UnsafeUtility.AlignOf<long>();
        var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
        drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * batchCount, alignment, Allocator.TempJob);
        drawCommands->drawCommandCount = batchCount;
        drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(instanceCount * sizeof(int), alignment, Allocator.TempJob);
        drawCommands->visibleInstanceCount = instanceCount;
        drawCommands->drawCommandPickingInstanceIDs = null;
        drawCommands->instanceSortingPositions = null;
        drawCommands->instanceSortingPositionFloatCount = 0;

        //DrawRange
        drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
        drawCommands->drawRangeCount = 1;
        var drawRange = drawCommands->drawRanges;
        drawRange->drawCommandsBegin = 0;
        drawRange->drawCommandsCount = (uint)batchCount;
        drawRange->filterSettings = new BatchFilterSettings
        {
            renderingLayerMask = 0xffffffff
        };

        //裁剪
        var cullDistanceSq = cullDistance * cullDistance;
        var cullingPlanes = new NativeArray<float4>(cullingContext.cullingPlanes.Length, Allocator.TempJob);
        for (var i = 0; i < cullingPlanes.Length; ++i)
        {
            var plane = cullingContext.cullingPlanes[i];
            cullingPlanes[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
        }
        if (useJobForCulling)
        {
            float3 playerPosition = player.position;
            var handles = new NativeArray<JobHandle>(batchCount, Allocator.Temp);
            for (var batchInfoIndex = 0; batchInfoIndex < m_batchGroups.Length; ++batchInfoIndex)
            {
                var batchGroup = m_batchGroups[batchInfoIndex];
                var batchGroupConfig = batchGroupConfigs[batchInfoIndex];

                for (var lodLevel = 0; lodLevel < batchGroup.lods.Length; ++lodLevel)
                {
                    var batchLodInfo = batchGroup.lods[lodLevel];
                    foreach (var batch in batchLodInfo.batches)
                    {
                        var drawCommand = drawCommands->drawCommands + batch.batchIndex;
                        drawCommand->batchID = batch.batchID;
                        drawCommand->materialID = batchLodInfo.materialID;
                        drawCommand->meshID = batchLodInfo.meshID;
                        drawCommand->submeshIndex = 0;
                        drawCommand->splitVisibilityMask = 0xff;
                        drawCommand->flags = 0;
                        drawCommand->sortingPosition = 0;
                        handles[batch.batchIndex] = new BatchCullJob
                        {
                            drawCommands = drawCommands,
                            drawCommand = drawCommand,
                            cullingPlanes = cullingPlanes,
                            positions = batchGroup.positions,
                            bounds = batchGroup.bounds,
                            instanceCount = batch.instanceCount,
                            instanceOffset = batch.instanceOffset,
                            visibleOffset = batch.visibleOffset,
                            cullDistanceSq = cullDistanceSq,
                            lodMaxDistance = batchLodInfo.lodMaxDistance,
                            lodMaxDistanceSq = batchLodInfo.lodMaxDistanceSq,
                            lodMinDistanceSq = batchLodInfo.lodMinDistanceSq,
                            playerPosition = playerPosition,
                        }.Schedule();
                    }
                }
            }

            //return
            JobHandle.ScheduleBatchedJobs();
            return JobHandle.CombineDependencies(handles);
        }
        else
        {
            var playerPosition = player.position;
            for (int batchInfoIndex = 0, batchIndex = 0; batchInfoIndex < m_batchGroups.Length; ++batchInfoIndex)
            {
                var batchGroup = m_batchGroups[batchInfoIndex];
                var batchGroupConfig = batchGroupConfigs[batchInfoIndex];

                for (var lodLevel = 0; lodLevel < batchGroup.lods.Length; ++lodLevel)
                {
                    var batchLodInfo = batchGroup.lods[lodLevel];
                    foreach (var batch in batchLodInfo.batches)
                    {
                        //可见和LOD裁剪
                        int visibleCount = 0;
                        int visibleOffset = batch.visibleOffset;
                        for (var j = 0; j < batch.instanceCount; ++j)
                        {
                            //检测可见性
                            var configIndex = batch.instanceOffset + j;
                            var intersectResult = Unity.Rendering.FrustumPlanes.Intersect(cullingPlanes, batchGroupConfig.bounds[configIndex]);
                            if (intersectResult != Unity.Rendering.FrustumPlanes.IntersectResult.Out)
                            {
                                var distanceSq = Vector3.SqrMagnitude(playerPosition - batchGroupConfig.positions[configIndex]);
                                if (distanceSq <= cullDistanceSq)
                                {
                                    //检测LOD
                                    if (distanceSq >= batchLodInfo.lodMinDistanceSq && (distanceSq < batchLodInfo.lodMaxDistanceSq || batchLodInfo.lodMaxDistance < 0))
                                    {
                                        drawCommands->visibleInstances[visibleOffset + visibleCount] = j;
                                        ++visibleCount;
                                    }
                                }
                            }
                        }

                        var drawCommand = drawCommands->drawCommands + batchIndex++;
                        drawCommand->visibleCount = (uint)visibleCount;
                        drawCommand->visibleOffset = (uint)visibleOffset;
                        drawCommand->batchID = batch.batchID;
                        drawCommand->materialID = batchLodInfo.materialID;
                        drawCommand->meshID = batchLodInfo.meshID;
                        drawCommand->submeshIndex = 0;
                        drawCommand->splitVisibilityMask = 0xff;
                        drawCommand->flags = 0;
                        drawCommand->sortingPosition = 0;
                    }
                }
            }
            return new JobHandle();
        }
    }

    public unsafe static T* Malloc<T>(uint count) where T : unmanaged
    {
        return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
    }
}
