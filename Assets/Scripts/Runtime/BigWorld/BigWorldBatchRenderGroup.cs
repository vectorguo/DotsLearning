using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BigCat.BigWorld
{
    [BurstCompile]
    public class BigWorldBatchRenderGroup : MonoBehaviour
    {
        public const uint sizeOfMatrix = sizeof(float) * 4 * 4;
        public const uint sizeOfPackedMatrix = sizeof(float) * 4 * 3;
        public const uint sizeOfFloat = sizeof(float);
        public const uint sizeOfFloat4 = sizeof(float) * 4;
        public const uint sizeOfGBufferHead = sizeOfPackedMatrix * 2;
        public const uint sizeOfPerInstance = (sizeOfPackedMatrix + sizeOfFloat4 + sizeOfFloat + sizeof(int) - 1) / sizeof(int) * sizeof(int); //确保是sizeof(int)的整数倍

        /// <summary>
        /// 单例
        /// </summary>
        private static BigWorldBatchRenderGroup s_instance;
        public static BigWorldBatchRenderGroup instance => s_instance;
        
        /// <summary>
        /// maxGraphicsBufferSize
        /// </summary>
        public static long maxGraphicsBufferSize = 32 * 1024;

        /// <summary>
        /// 每个Batch的最大Instance数量
        /// </summary>
        public static int maxInstanceCountPerBatch;

        /// <summary>
        /// 高精度Lightmap的显示距离
        /// </summary>
        public static readonly float hqLightmapDistance = 10;
        public static float hqLightmapDistanceSq => hqLightmapDistance * hqLightmapDistance;

        /// <summary>
        /// 中精度Lightmap的显示距离
        /// </summary>
        public static readonly float mqLightmapDistance = 20;
        public static float mqLightmapDistanceSq => mqLightmapDistance * mqLightmapDistance;

        /// <summary>
        /// 裁剪的中心点
        /// </summary>
        public Vector3 cullCenter { get; set; }
        
        /// <summary>
        /// 裁剪距离
        /// </summary>
        public float cullDistance { get; set; }
        
        /// <summary>
        /// 是否使用Job才裁剪
        /// </summary>
        public bool useJobForCulling { get; set; }
        
        /// <summary>
        /// 是否使用Job来更新
        /// </summary>
        public bool useJobForUpdate { get; set; }
        
        /// <summary>
        /// 初始化完成的回调函数
        /// </summary>
        public Action initialized { get; set; }

        /// <summary>
        /// brg
        /// </summary>
        private BatchRendererGroup m_brg;

        /// <summary>
        /// Batch数据
        /// </summary>
        private List<BigWorldBatchGroup> m_batchGroups;

        /// <summary>
        /// 下一个BatchGroup的ID
        /// </summary>
        private int m_nextBatchGroupID;

        private void Awake()
        {
            s_instance = this;
        }

        private void Start()
        {
            //初始化参数
            maxGraphicsBufferSize = Math.Min(maxGraphicsBufferSize, SystemInfo.maxGraphicsBufferSize);
            maxInstanceCountPerBatch = (int)((maxGraphicsBufferSize - sizeOfGBufferHead) / sizeOfPerInstance);
            Debug.LogError("maxInstanceCountPerBatch = " + maxInstanceCountPerBatch);

            //创建BRG
            m_brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            m_batchGroups = new List<BigWorldBatchGroup>();
            
            //调用初始化完成的回调
            initialized?.Invoke();
        }

        private void Update()
        {
            //更新Lightmap
            UpdateLightmap();
        }

        private void OnDestroy()
        {
            foreach (var batchInfo in m_batchGroups)
            {
                batchInfo.Destroy(m_brg);
            }
            m_brg.Dispose();
            
            s_instance = null;
        }

        /// <summary>
        /// 添加BatchGroup
        /// </summary>
        /// <param name="batchGroupConfig">配置信息</param>
        /// <param name="lightmaps">该BatchGroup使用的Lightmap</param>
        /// <returns>BatchGroup的ID</returns>
        public int AddBatchGroup(BigWorldBatchGroupConfig batchGroupConfig, Texture2DArray lightmaps)
        {
            var id = ++m_nextBatchGroupID;
            m_batchGroups.Add(new BigWorldBatchGroup(id, batchGroupConfig, lightmaps, m_brg));
            return id;
        }

        /// <summary>
        /// 删除BatchGroup
        /// </summary>
        /// <param name="batchID">BatchGroup的ID</param>
        public void RemoveBatchGroup(int batchID)
        {
            for (var i = 0; i < m_batchGroups.Count; ++i)
            {
                if (m_batchGroups[i].id == batchID)
                {
                    m_batchGroups[i].Destroy(m_brg);
                    m_batchGroups.RemoveAt(i);
                    break;
                }
            }
        }

        #region Culling
        
        #region CullJob
        [BurstCompile]
        public unsafe struct BatchCullJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [WriteOnly]
            public BatchCullingOutputDrawCommands* drawCommands;

            [NativeDisableUnsafePtrRestriction] [WriteOnly]
            public BatchDrawCommand* drawCommand;

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
        
        [BurstCompile]
        private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            //统计并更新Batch数据
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
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            drawCommands->drawCommands = Malloc<BatchDrawCommand>((uint)batchCount);
            drawCommands->drawCommandCount = batchCount;
            drawCommands->visibleInstances = Malloc<int>((uint)instanceCount);
            drawCommands->visibleInstanceCount = instanceCount;
            drawCommands->drawCommandPickingInstanceIDs = null;
            drawCommands->instanceSortingPositions = null;
            drawCommands->instanceSortingPositionFloatCount = 0;

            //DrawRange
            drawCommands->drawRanges = Malloc<BatchDrawRange>(1);
            drawCommands->drawRangeCount = 1;
            var drawRange = drawCommands->drawRanges;
            drawRange->drawCommandsBegin = 0;
            drawRange->drawCommandsCount = (uint)batchCount;
            drawRange->filterSettings = new BatchFilterSettings
            {
                renderingLayerMask = 0xffffffff
            };

            //创建裁剪数据
            var cullDistanceSq = cullDistance * cullDistance;
            var cullingPlanes = new NativeArray<float4>(cullingContext.cullingPlanes.Length, Allocator.TempJob);
            for (var i = 0; i < cullingPlanes.Length; ++i)
            {
                var plane = cullingContext.cullingPlanes[i];
                cullingPlanes[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }

            //裁剪
            float3 playerPosition = cullCenter;
            if (useJobForCulling)
            {
                var handles = new NativeArray<JobHandle>(batchCount, Allocator.Temp);
                foreach (var batchGroup in m_batchGroups)
                {
                    foreach (var lod in batchGroup.lods)
                    {
                        foreach (var batch in lod.batches)
                        {
                            var drawCommand = drawCommands->drawCommands + batch.batchIndex;
                            drawCommand->batchID = batch.batchID;
                            drawCommand->materialID = lod.materialID;
                            drawCommand->meshID = lod.meshID;
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
                                lodMaxDistance = lod.lodMaxDistance,
                                lodMaxDistanceSq = lod.lodMaxDistanceSq,
                                lodMinDistanceSq = lod.lodMinDistanceSq,
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
                foreach (var batchGroup in m_batchGroups)
                {
                    foreach (var batchLodInfo in batchGroup.lods)
                    {
                        foreach (var batch in batchLodInfo.batches)
                        {
                            //可见和LOD裁剪
                            int visibleCount = 0;
                            int visibleOffset = batch.visibleOffset;
                            for (var j = 0; j < batch.instanceCount; ++j)
                            {
                                //检测可见性
                                var instanceIndex = batch.instanceOffset + j;
                                var intersectResult = Unity.Rendering.FrustumPlanes.Intersect(cullingPlanes, batchGroup.bounds[instanceIndex]);
                                if (intersectResult != Unity.Rendering.FrustumPlanes.IntersectResult.Out)
                                {
                                    var distanceSq = math.lengthsq(playerPosition - batchGroup.positions[instanceIndex]);
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

                            var drawCommand = drawCommands->drawCommands + batch.batchIndex;
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
        
        /// <summary>
        /// 分配内存
        /// </summary>
        public static unsafe T* Malloc<T>(uint count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
        }
        #endregion
        
        #region Lightmap
        /// <summary>
        /// 更新Lightmap
        /// </summary>
        private void UpdateLightmap()
        {
            float3 playerPosition = cullCenter;
            foreach (var batchGroup in m_batchGroups)
            {
                foreach (var batchLodInfo in batchGroup.lods)
                {
                    foreach (var batch in batchLodInfo.batches)
                    {
                        for (var j = 0; j < batch.instanceCount; ++j)
                        {
                            var instanceIndex = batch.instanceOffset + j;
                            var distanceSq = math.lengthsq(playerPosition - batchGroup.positions[instanceIndex]);
                            if (distanceSq <= hqLightmapDistanceSq)
                            {
                                //高精度Lightmap
                                batch.systemBufferLightmapIndex[j] = batchGroup.hqLightmapIndices[instanceIndex];
                                batch.systemBufferLightmapST[j] = batchGroup.hqLightmapScaleOffsets[instanceIndex];
                            }
                            else if (distanceSq <= mqLightmapDistanceSq)
                            {
                                //中精度Lightmap
                                batch.systemBufferLightmapIndex[j] = batchGroup.mqLightmapIndices[instanceIndex];
                                batch.systemBufferLightmapST[j] = batchGroup.mqLightmapScaleOffsets[instanceIndex];
                            }
                            else
                            {
                                //低精度Lightmap
                                batch.systemBufferLightmapIndex[j] = batchGroup.lqLightmapIndices[instanceIndex];
                                batch.systemBufferLightmapST[j] = batchGroup.lqLightmapScaleOffsets[instanceIndex];
                            }
                        }

                        uint byteAddressLightmapScaleOffset = sizeOfGBufferHead + (uint)(sizeOfPackedMatrix * batch.instanceCount);
                        uint byteAddressLightmapIndex = byteAddressLightmapScaleOffset + (uint)(sizeOfFloat4 * batch.instanceCount);
                        batch.instanceData.SetData(batch.systemBufferLightmapST, 0, (int)(byteAddressLightmapScaleOffset / sizeOfFloat4), batch.instanceCount);
                        batch.instanceData.SetData(batch.systemBufferLightmapIndex, 0, (int)(byteAddressLightmapIndex / sizeOfFloat), batch.instanceCount);
                    }
                }
            }
        }
        #endregion
    }
    
    public class BigWorldBatchGroup
    {
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
        
        public class Batch
        {
            /// <summary>
            /// 该Batch在BRG中的ID
            /// </summary>
            public BatchID batchID;
            
            /// <summary>
            /// 存储绘制Instance的数据的GBuffer
            /// </summary>
            public readonly GraphicsBuffer instanceData;
            
            /// <summary>
            /// 该Batch绘制的所有的Instance的数量
            /// </summary>
            public readonly int instanceCount;
            
            /// <summary>
            /// Instance在所属的BatchGroup里的偏移
            /// </summary>
            public readonly int instanceOffset;
            
            /// <summary>
            /// 该Batch在整个绘制阶段所有Batch列表里的偏移
            /// </summary>
            public int batchIndex;
            
            /// <summary>
            /// Instance在DrawCommands可见列表里的偏移
            /// </summary>
            public int visibleOffset;

            /// <summary>
            /// 用于存储每个Instance的Lightmap数据
            /// </summary>
            public NativeArray<float4> systemBufferLightmapST;
            public NativeArray<float> systemBufferLightmapIndex;
            
            public Batch(int instanceOffset, int instanceCount, BigWorldBatchGroupConfig batchGroupConfig, BatchRendererGroup brg)
            {
                this.instanceCount = instanceCount;
                this.instanceOffset = instanceOffset;
                systemBufferLightmapST = new NativeArray<float4>(instanceCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                systemBufferLightmapIndex = new NativeArray<float>(instanceCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                //填充数据
                var localToWorld = new PackedMatrix[instanceCount];
                for (var i = 0; i < instanceCount; ++i)
                {
                    var index = instanceOffset + i;
                    localToWorld[i] = new PackedMatrix(Matrix4x4.TRS(batchGroupConfig.positions[index], batchGroupConfig.rotations[index], batchGroupConfig.scales[index]));
                }

                //创建GBuffer
                uint byteAddressLocalToWorld = BigWorldBatchRenderGroup.sizeOfGBufferHead;
                uint byteAddressLightmapScaleOffset = byteAddressLocalToWorld + (uint)(BigWorldBatchRenderGroup.sizeOfPackedMatrix * instanceCount);
                uint byteAddressLightmapIndex = byteAddressLightmapScaleOffset + (uint)(BigWorldBatchRenderGroup.sizeOfFloat4 * instanceCount);
                var buffSize = instanceCount * BigWorldBatchRenderGroup.sizeOfPerInstance + BigWorldBatchRenderGroup.sizeOfGBufferHead;
                instanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, (int)buffSize / sizeof(int), sizeof(int));
                instanceData.SetData(new[] { Matrix4x4.zero }, 0, 0, 1);
                instanceData.SetData(localToWorld, 0, (int)(byteAddressLocalToWorld / BigWorldBatchRenderGroup.sizeOfPackedMatrix), localToWorld.Length);

                //metadata
                var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
                metadata[0] = new MetadataValue { NameID = shaderPropertyO2W, Value = 0x80000000 | byteAddressLocalToWorld };
                metadata[1] = new MetadataValue { NameID = shaderPropertyLightmapST, Value = 0x80000000 | byteAddressLightmapScaleOffset };
                metadata[2] = new MetadataValue { NameID = shaderPropertyLightmapIndex, Value = 0x80000000 | byteAddressLightmapIndex };

                //add batch
                batchID = brg.AddBatch(metadata, instanceData.bufferHandle);
            }

            public void Destroy(BatchRendererGroup brg)
            {
                brg.RemoveBatch(batchID);

                instanceData.Dispose();
                systemBufferLightmapST.Dispose();
                systemBufferLightmapIndex.Dispose();
            }
        }
        
        public class Lod
        {
            /// <summary>
            /// 材质在BRG中的注册ID
            /// </summary>
            public BatchMaterialID materialID;
            
            /// <summary>
            /// Mesh在BRG中的注册ID
            /// </summary>
            public BatchMeshID meshID;
            
            /// <summary>
            /// LOD
            /// </summary>
            public readonly float lodMinDistance;
            public readonly float lodMaxDistance;
            public float lodMinDistanceSq => lodMinDistance * lodMinDistance;
            public float lodMaxDistanceSq => lodMaxDistance * lodMaxDistance;

            /// <summary>
            /// Batch列表
            /// </summary>
            public readonly Batch[] batches;

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="batchGroupConfig">BatchGroup配置</param>
            /// <param name="lodLevel">LOD等级</param>
            /// <param name="lightmaps">绘制时使用的Lightmap</param>
            /// <param name="brg">BRG</param>
            public Lod(BigWorldBatchGroupConfig batchGroupConfig, int lodLevel, Texture2DArray lightmaps, BatchRendererGroup brg)
            {
                var lodConfig = batchGroupConfig.lods[lodLevel];
                lodMinDistance = lodConfig.lodMinDistance;
                lodMaxDistance = lodConfig.lodMaxDistance;
                
                //创建并注册材质
                var material = new Material(lodConfig.material);
                material.SetTexture(shaderPropertyLightmaps, lightmaps);
                material.EnableKeyword("LIGHTMAP_ON");
                materialID = brg.RegisterMaterial(material);
                
                //注册Mesh
                meshID = brg.RegisterMesh(lodConfig.mesh);
                
                //创建Batch
                var batchCount = (batchGroupConfig.count + BigWorldBatchRenderGroup.maxInstanceCountPerBatch - 1) / BigWorldBatchRenderGroup.maxInstanceCountPerBatch;
                batches = new Batch[batchCount];
                for (var i = 0; i < batchCount; ++i)
                {
                    var instanceOffset = i * BigWorldBatchRenderGroup.maxInstanceCountPerBatch;
                    var instanceCount = Math.Min(BigWorldBatchRenderGroup.maxInstanceCountPerBatch, batchGroupConfig.count - instanceOffset);
                    batches[i] = new Batch(instanceOffset, instanceCount, batchGroupConfig, brg);
                }
            }

            /// <summary>
            /// 销毁
            /// </summary>
            /// <param name="brg">BRG</param>
            public void Destroy(BatchRendererGroup brg)
            {
                //销毁Batch
                foreach (var batch in batches)
                {
                    batch.Destroy(brg);
                }
                
                //注销材质和Mesh
                brg.UnregisterMaterial(materialID);
                brg.UnregisterMesh(meshID);
            }
        }
        
        /// <summary>
        /// ShaderProperty
        /// </summary>
        private static readonly int shaderPropertyLightmaps = Shader.PropertyToID("_Lightmaps");
        private static readonly int shaderPropertyO2W = Shader.PropertyToID("unity_ObjectToWorld");
        private static readonly int shaderPropertyLightmapST = Shader.PropertyToID("_LightmapST");
        private static readonly int shaderPropertyLightmapIndex = Shader.PropertyToID("_LightmapIndex");
        
        /// <summary>
        /// BatchGroup的唯一ID
        /// </summary>
        private readonly int m_id;
        public int id => m_id;
        
        /// <summary>
        /// LOD
        /// </summary>
        public readonly Lod[] lods;

        /// <summary>
        /// instance的位置数组
        /// </summary>
        public NativeArray<float3> positions;
        
        /// <summary>
        /// instance的包围盒数据
        /// </summary>
        public NativeArray<AABB> bounds;

        /// <summary>
        /// instance使用的高精度Lightmap索引
        /// </summary>
        public NativeArray<int> hqLightmapIndices;
        
        /// <summary>
        /// instance的高精度Lightmap的ScaleOffset
        /// </summary>
        public NativeArray<float4> hqLightmapScaleOffsets;
        
        /// <summary>
        /// instance使用的中精度Lightmap索引
        /// </summary>
        public NativeArray<int> mqLightmapIndices;
        
        /// <summary>
        /// instance的中精度Lightmap的ScaleOffset
        /// </summary>
        public NativeArray<float4> mqLightmapScaleOffsets;
        
        /// <summary>
        /// instance使用的低精度Lightmap索引
        /// </summary>
        public NativeArray<int> lqLightmapIndices;

        /// <summary>
        /// instance的低精度Lightmap的ScaleOffset
        /// </summary>
        public NativeArray<float4> lqLightmapScaleOffsets;

        public BigWorldBatchGroup(int id, BigWorldBatchGroupConfig batchGroupConfig, Texture2DArray lightmaps, BatchRendererGroup brg)
        {
            m_id = id;
            
            //初始化LOD
            lods = new Lod[batchGroupConfig.lods.Length];
            for (var i = 0; i < batchGroupConfig.lods.Length; ++i)
            {
                lods[i] = new Lod(batchGroupConfig, i, lightmaps, brg);
            }

            positions = new NativeArray<float3>(batchGroupConfig.count, Allocator.Persistent);
            bounds = new NativeArray<AABB>(batchGroupConfig.count, Allocator.Persistent);
            hqLightmapIndices = new NativeArray<int>(batchGroupConfig.count, Allocator.Persistent);
            hqLightmapScaleOffsets = new NativeArray<float4>(batchGroupConfig.count, Allocator.Persistent);
            mqLightmapIndices = new NativeArray<int>(batchGroupConfig.count, Allocator.Persistent);
            mqLightmapScaleOffsets = new NativeArray<float4>(batchGroupConfig.count, Allocator.Persistent);
            lqLightmapIndices = new NativeArray<int>(batchGroupConfig.count, Allocator.Persistent);
            lqLightmapScaleOffsets = new NativeArray<float4>(batchGroupConfig.count, Allocator.Persistent);
            
            for (var i = 0; i < batchGroupConfig.count; ++i)
            {
                positions[i] = batchGroupConfig.positions[i];
                bounds[i] = batchGroupConfig.bounds[i];
                hqLightmapIndices[i] = batchGroupConfig.hqLightmapIndices[i];
                hqLightmapScaleOffsets[i] = batchGroupConfig.hqLightmapScaleOffsets[i];
                mqLightmapIndices[i] = batchGroupConfig.mqLightmapIndices[i];
                mqLightmapScaleOffsets[i] = batchGroupConfig.mqLightmapScaleOffsets[i];
                lqLightmapIndices[i] = batchGroupConfig.lqLightmapIndices[i];
                lqLightmapScaleOffsets[i] = batchGroupConfig.lqLightmapScaleOffsets[i];
            }
        }

        public void Destroy(BatchRendererGroup brg)
        {
            foreach (var lod in lods)
            {
                lod.Destroy(brg);
            }

            positions.Dispose();
            bounds.Dispose();
            hqLightmapIndices.Dispose();
            hqLightmapScaleOffsets.Dispose();
            mqLightmapIndices.Dispose();
            mqLightmapScaleOffsets.Dispose();
            lqLightmapIndices.Dispose();
            lqLightmapScaleOffsets.Dispose();
        }
    }
}