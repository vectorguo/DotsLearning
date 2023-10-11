using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

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
    
    /// <summary>
    /// 草数据
    /// </summary>
    public BigWorldObjectGroupForBRG grass;

    /// <summary>
    /// Player
    /// </summary>
    public Transform player;
    
    /// <summary>
    /// brg
    /// </summary>
    private BatchRendererGroup m_renderGroup;

    private GraphicsBuffer m_instanceData;
    private BatchID m_batchID;
    private BatchMeshID m_meshID;
    private BatchMaterialID m_materialID;

    private const int c_sizeOfMatrix = sizeof(float) * 4 * 4;
    private const int c_sizeOfPackedMatrix = sizeof(float) * 4 * 3;
    private const int c_sizeOfFloat4 = sizeof(float) * 4;
    private const int c_sizeOfPerInstance = (c_sizeOfPackedMatrix + sizeof(int) - 1) / sizeof(int) * sizeof(int);  //确保是sizeof(int)的整数倍
    private const int c_sizeOfBufferHead = (c_sizeOfMatrix * 2 + sizeof(int) - 1) / sizeof(int) * sizeof(int);     //确保是sizeof(int)的整数倍
    
    private void Start()
    {
        if (grass == null || player == null)
        {
            return;
        }
        
        m_renderGroup = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
        m_meshID = m_renderGroup.RegisterMesh(grass.mesh);
        m_materialID = m_renderGroup.RegisterMaterial(grass.material);

        //分配GBuffer
        var totalSize = c_sizeOfPerInstance * grass.count + c_sizeOfBufferHead;
        m_instanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, totalSize / sizeof(int), sizeof(int));
        
        //
        var localToWorld = new PackedMatrix[grass.count];
        for (var i = 0; i < grass.count; ++i)
        {
            localToWorld[i] = new PackedMatrix(Matrix4x4.TRS(grass.positions[i], grass.rotations[i], grass.scales[i]));
        }
        
        //填充GBuffer
        uint localToWorldGBufferStartIndex = c_sizeOfPackedMatrix * 2;
        m_instanceData.SetData(new[]{Matrix4x4.zero}, 0, 0, 1);
        m_instanceData.SetData(localToWorld, 0, (int)(localToWorldGBufferStartIndex / c_sizeOfPackedMatrix), localToWorld.Length);

        var metadata = new NativeArray<MetadataValue>(1, Allocator.Temp);
        metadata[0] = new MetadataValue
        {
            NameID = Shader.PropertyToID("unity_ObjectToWorld"),
            Value = 0x80000000 | localToWorldGBufferStartIndex,
        };
        m_batchID = m_renderGroup.AddBatch(metadata, m_instanceData.bufferHandle);
    }

    private void OnDisable()
    {
        m_renderGroup.Dispose();
    }

    private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        var alignment = UnsafeUtility.AlignOf<long>();

        var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
        drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
        drawCommands->drawCommandCount = 1;
        drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
        drawCommands->drawRangeCount = 1;
        drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(grass.count * sizeof(int), alignment, Allocator.TempJob);
        drawCommands->visibleInstanceCount = grass.count;
        drawCommands->drawCommandPickingInstanceIDs = null;
        drawCommands->instanceSortingPositions = null;
        drawCommands->instanceSortingPositionFloatCount = 0;
        
        //填充可见数组
        var playerPosition = player.position;
        var visibleCount = 0;
        for (var i = 0; i < grass.count; ++i)
        {
            if (Vector3.SqrMagnitude(playerPosition - grass.positions[i]) <= 64.0f)
            {
                drawCommands->visibleInstances[visibleCount] = i;
                ++visibleCount;
            }
        }
        
        //初始化draw command
        var drawCommand = drawCommands->drawCommands;
        drawCommand->visibleCount = (uint)visibleCount;
        drawCommand->visibleOffset = 0;
        drawCommand->batchID = m_batchID;
        drawCommand->materialID = m_materialID;
        drawCommand->meshID = m_meshID;
        drawCommand->submeshIndex = 0;
        drawCommand->splitVisibilityMask = 0xff;
        drawCommand->flags = 0;
        drawCommand->sortingPosition = 0;

        var drawRange = drawCommands->drawRanges;
        drawRange->drawCommandsBegin = 0;
        drawRange->drawCommandsCount = 1;
        drawRange->filterSettings = new BatchFilterSettings
        {
            renderingLayerMask = 0xffffffff
        };
        
        //return
        return new JobHandle();
    }
}
