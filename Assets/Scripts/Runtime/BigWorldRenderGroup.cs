using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

public class BigWorldRenderGroup : MonoBehaviour
{
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
    public class BatchInfo
    {
        public GraphicsBuffer instanceData;
        public int instanceCount;
        public BatchID batchID;
        public BatchMeshID meshID;
        public BatchMaterialID materialID;

        public BatchInfo(BatchRendererGroup brg, BigWorldObjectGroupForBRG group)
        {
            var totalSize = c_sizeOfPerInstance * group.count + c_sizeOfBufferHead;
            instanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, totalSize / sizeof(int), sizeof(int));
            instanceCount = group.count;
            meshID = brg.RegisterMesh(group.mesh);
            materialID = brg.RegisterMaterial(group.material);

            //组织数据
            var localToWorld = new PackedMatrix[group.count];
            for (var i = 0; i < group.count; ++i)
            {
                localToWorld[i] = new PackedMatrix(Matrix4x4.TRS(group.positions[i], group.rotations[i], group.scales[i]));
            }

            //填充GBuffer
            uint localToWorldGBufferStartIndex = c_sizeOfPackedMatrix * 2;
            instanceData.SetData(new[] { Matrix4x4.zero }, 0, 0, 1);
            instanceData.SetData(localToWorld, 0, (int)(localToWorldGBufferStartIndex / c_sizeOfPackedMatrix), localToWorld.Length);

            //metadata
            var metadata = new NativeArray<MetadataValue>(1, Allocator.Temp);
            metadata[0] = new MetadataValue
            {
                NameID = Shader.PropertyToID("unity_ObjectToWorld"),
                Value = 0x80000000 | localToWorldGBufferStartIndex,
            };

            //add batch
            batchID = brg.AddBatch(metadata, instanceData.bufferHandle);
        }
    }
    #endregion

    #region 常量数据
    private const int c_sizeOfMatrix = sizeof(float) * 4 * 4;
    private const int c_sizeOfPackedMatrix = sizeof(float) * 4 * 3;
    private const int c_sizeOfFloat4 = sizeof(float) * 4;
    private const int c_sizeOfPerInstance = (c_sizeOfPackedMatrix + sizeof(int) - 1) / sizeof(int) * sizeof(int);  //确保是sizeof(int)的整数倍
    private const int c_sizeOfBufferHead = (c_sizeOfMatrix * 2 + sizeof(int) - 1) / sizeof(int) * sizeof(int);     //确保是sizeof(int)的整数倍
    #endregion

    /// <summary>
    /// 草数据
    /// </summary>
    public BigWorldObjectGroupForBRG[] objectGroups;

    /// <summary>
    /// Player
    /// </summary>
    public Transform player;

    /// <summary>
    /// 裁剪距离
    /// </summary>
    public float cullDistance;
    
    /// <summary>
    /// brg
    /// </summary>
    private BatchRendererGroup m_renderGroup;

    /// <summary>
    /// Batch数据
    /// </summary>
    private BatchInfo[] m_batchInfos;
    
    private void Start()
    {
        if (objectGroups == null || objectGroups.Length == 0 || player == null)
        {
            return;
        }

        //创建BRG
        m_renderGroup = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

        //创建Batch数据
        m_batchInfos = new BatchInfo[objectGroups.Length];
        for (var i = 0; i < objectGroups.Length; ++i)
        {
            m_batchInfos[i] = new BatchInfo(m_renderGroup, objectGroups[i]);
        }
    }

    private void OnDisable()
    {
        if (m_renderGroup != null)
        {
            m_renderGroup.Dispose();
        }
    }

    private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        var batchCount = m_batchInfos.Length;
        var instanceCount = 0;
        foreach (var batchInfo in m_batchInfos)
        {
            instanceCount += batchInfo.instanceCount;
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

        //初始化draw command
        var playerPosition = player.position;
        var cullDistanceSq = cullDistance * cullDistance;
        uint visibleOffset = 0;
        for (var i = 0; i < m_batchInfos.Length; ++i)
        {
            //可见裁剪
            uint visibleCount = 0;
            var group = objectGroups[i];
            for (var j = 0; j < group.count; ++j)
            {
                if (Vector3.SqrMagnitude(playerPosition - group.positions[j]) <= cullDistanceSq)
                {
                    drawCommands->visibleInstances[visibleOffset + visibleCount] = j;
                    ++visibleCount;
                }
            }

            var batchInfo = m_batchInfos[i];
            var drawCommand = drawCommands->drawCommands + i;
            drawCommand->visibleCount = visibleCount;
            drawCommand->visibleOffset = visibleOffset;
            drawCommand->batchID = batchInfo.batchID;
            drawCommand->materialID = batchInfo.materialID;
            drawCommand->meshID = batchInfo.meshID;
            drawCommand->submeshIndex = 0;
            drawCommand->splitVisibilityMask = 0xff;
            drawCommand->flags = 0;
            drawCommand->sortingPosition = 0;

            visibleOffset += visibleCount;
        }

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

        //return
        return new JobHandle();
    }
}
