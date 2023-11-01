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
    public class BigWorldTerrainBatchRenderGroup : MonoBehaviour
    {
        public const uint sizeOfGBufferHead = BigWorldUtility.sizeOfPackedMatrix * 2;
        public const uint sizeOfPerInstance = (BigWorldUtility.sizeOfPackedMatrix + BigWorldUtility.sizeOfFloat4 + BigWorldUtility.sizeOfFloat + BigWorldUtility.sizeOfFloat + sizeof(int) - 1) / sizeof(int) * sizeof(int); //确保是sizeof(int)的整数倍
        
        /// <summary>
        /// 单例
        /// </summary>
        private static BigWorldTerrainBatchRenderGroup s_instance;
        public static BigWorldTerrainBatchRenderGroup instance => s_instance;

        /// <summary>
        /// 每个Batch的最大Instance数量
        /// </summary>
        private static int s_maxInstanceCountPerBatch;
        public static int maxInstanceCountPerBatch => s_maxInstanceCountPerBatch;

        /// <summary>
        /// 初始化完成的回调函数
        /// </summary>
        public Action onInitialized { get; set; }

        /// <summary>
        /// brg
        /// </summary>
        private BatchRendererGroup m_brg;

        /// <summary>
        /// Terrain的Mesh
        /// </summary>
        private Mesh m_mesh;
        
        /// <summary>
        /// Mesh在BRG的注册ID
        /// </summary>
        private BatchMeshID m_meshID;

        /// <summary>
        /// Batch数据
        /// </summary>
        private List<BigWorldTerrainBatchGroup> m_batchGroups;

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
            s_maxInstanceCountPerBatch = (int)((BigWorldUtility.maxGraphicsBufferSize - sizeOfGBufferHead) / sizeOfPerInstance);
            Debug.LogError("terrain maxInstanceCountPerBatch = " + s_maxInstanceCountPerBatch);
            
            //创建BRG
            m_brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            m_batchGroups = new List<BigWorldTerrainBatchGroup>();
            m_nextBatchGroupID = 0;

            //创建Mesh，并在BRG中注册
            m_mesh = CreateMesh();
            m_meshID = m_brg.RegisterMesh(m_mesh);

            //调用初始化完成的回调
            onInitialized?.Invoke();
        }

        private void OnDestroy()
        {
            //销毁材质和Mesh
            m_brg.UnregisterMesh(m_meshID);
            Destroy(m_mesh);
            
            //销毁BatchGroup
            foreach (var batchInfo in m_batchGroups)
            {
                batchInfo.Destroy(m_brg);
            }
            
            //销毁brg
            m_brg.Dispose();
            
            s_instance = null;
        }

        /// <summary>
        /// 添加BatchGroup
        /// </summary>
        /// <returns>BatchGroup的ID</returns>
        public int AddBatchGroup()
        {
            var id = ++m_nextBatchGroupID;
            m_batchGroups.Add(new BigWorldTerrainBatchGroup(id, m_brg));
            return id;
        }

        /// <summary>
        /// 获取BatchGroup
        /// </summary>
        public BigWorldTerrainBatchGroup GetBatchGroup(int batchID)
        {
            return m_batchGroups.Find((g) => g.id == batchID);
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

        [BurstCompile]
        private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            //统计并更新Batch数据
            var batchCount = m_batchGroups.Count;
            var instanceCount = m_batchGroups.Count * BigWorldTerrainBatchGroup.instanceCount;
            
            //DrawCommand
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            drawCommands->drawCommands = BigWorldUtility.Malloc<BatchDrawCommand>((uint)batchCount);
            drawCommands->drawCommandCount = batchCount;
            drawCommands->visibleInstances = BigWorldUtility.Malloc<int>((uint)instanceCount);
            drawCommands->visibleInstanceCount = instanceCount;
            drawCommands->drawCommandPickingInstanceIDs = null;
            drawCommands->instanceSortingPositions = null;
            drawCommands->instanceSortingPositionFloatCount = 0;

            //DrawRange
            drawCommands->drawRanges = BigWorldUtility.Malloc<BatchDrawRange>(1);
            drawCommands->drawRangeCount = 1;
            var drawRange = drawCommands->drawRanges;
            drawRange->drawCommandsBegin = 0;
            drawRange->drawCommandsCount = (uint)batchCount;
            drawRange->filterSettings = new BatchFilterSettings
            {
                renderingLayerMask = 0xffffffff
            };
            
            //裁剪，暂时全部可见
            for (var batchIndex = 0; batchIndex < m_batchGroups.Count; ++batchIndex)
            {
                var batchGroup = m_batchGroups[batchIndex];
                var visibleOffset = batchIndex * BigWorldTerrainBatchGroup.instanceCount;
                for (var j = 0; j < batchGroup.visibleInstanceCount; ++j)
                {
                    drawCommands->visibleInstances[visibleOffset + j] = j;
                }

                var drawCommand = drawCommands->drawCommands + batchIndex;
                drawCommand->visibleCount = (uint)batchGroup.visibleInstanceCount;
                drawCommand->visibleOffset = (uint)visibleOffset;
                drawCommand->batchID = batchGroup.batchID;
                drawCommand->materialID = batchGroup.materialID;
                drawCommand->meshID = m_meshID;
                drawCommand->submeshIndex = 0;
                drawCommand->splitVisibilityMask = 0xff;
                drawCommand->flags = 0;
                drawCommand->sortingPosition = 0;
            }
            return new JobHandle();
        }
        
        #endregion
        
        #region Material和Mesh
        /// <summary>
        /// 创建Terrain所需的Mesh
        /// </summary>
        private static Mesh CreateMesh()
        {
            var vertices = new Vector3[4];
            var uv = new Vector2[4];
            var uv2 = new Vector2[4];
            var triangles = new int[6];

            vertices[0] = new Vector3(0, 0, 1);
            vertices[1] = new Vector3(1, 0, 1);
            vertices[2] = new Vector3(0, 0, 0);
            vertices[3] = new Vector3(1, 0, 0);
            uv[0] = new Vector2(0, 1);
            uv[1] = new Vector2(1, 1);
            uv[2] = new Vector2(0, 0);
            uv[3] = new Vector2(1, 0);
            uv2[0] = new Vector2(0, 1);
            uv2[1] = new Vector2(1, 1);
            uv2[2] = new Vector2(0, 0);
            uv2[3] = new Vector2(1, 0);
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            triangles[3] = 2;
            triangles[4] = 1;
            triangles[5] = 3;

            var mesh = new Mesh()
            {
                vertices = vertices,
                uv = uv,
                uv2 = uv2,
                triangles = triangles
            };
            return mesh;
        }
        #endregion
    }

    public class BigWorldTerrainBatchGroup
    {
        /// <summary>
        /// Lightmap最大数量
        /// </summary>
        private const int c_lightmapCount = 9;
        
        /// <summary>
        /// Lightmap贴图尺寸
        /// </summary>
        private const int c_lightmapTextureSize = 512;
        
        /// <summary>
        /// 最大实例数量
        /// </summary>
        public const int instanceCount = 40;
        
        /// <summary>
        /// ShaderProperty
        /// </summary>
        private static readonly int shaderPropertyOtw = Shader.PropertyToID("unity_ObjectToWorld");
        private static readonly int shaderPropertyLm = Shader.PropertyToID("_Lightmaps");
        private static readonly int shaderPropertyLmSt = Shader.PropertyToID("_LightmapST");
        private static readonly int shaderPropertyLmIndex = Shader.PropertyToID("_LightmapIndex");
        private static readonly int shaderPropertyLmAmount = Shader.PropertyToID("_LightmapAmount");

        /// <summary>
        /// BatchGroup的唯一ID
        /// </summary>
        private readonly int m_id;
        public int id => m_id;

        /// <summary>
        /// 可见的Instance数量
        /// </summary>
        private int m_visibleInstanceCount;
        public int visibleInstanceCount => m_visibleInstanceCount;

        /// <summary>
        /// 渲染Terrain需要的材质
        /// </summary>
        private readonly Material m_material;

        /// <summary>
        /// 渲染Terrain需要的Lightmap
        /// </summary>
        private readonly Texture2DArray m_lightmaps;

        /// <summary>
        /// 该Batch在BRG中的ID
        /// </summary>
        private readonly BatchID m_batchID;
        public BatchID batchID => m_batchID;

        /// <summary>
        /// 材质在BRG中的注册ID
        /// </summary>
        private readonly BatchMaterialID m_materialID;
        public BatchMaterialID materialID => m_materialID;
        
        /// <summary>
        /// 存储绘制Instance的数据的GBuffer
        /// </summary>
        private readonly GraphicsBuffer m_instanceData;
        public GraphicsBuffer instanceData => m_instanceData;
        
        /// <summary>
        /// 用于存储每个Instance的变化的数据
        /// </summary>
        public NativeArray<BigWorldUtility.PackedMatrix> m_systemBufferL2w;
        public NativeArray<float4> m_systemBufferLmSt;
        public NativeArray<float> m_systemBufferLmIndex;
        public NativeArray<float> m_systemBufferLmAmount;

        public BigWorldTerrainBatchGroup(int id, BatchRendererGroup brg)
        {
            m_id = id;
            
            //Lightmap
            m_lightmaps = new Texture2DArray(c_lightmapTextureSize, c_lightmapTextureSize, c_lightmapCount, BigWorldUtility.lightmapTextureFormat, false);

            //创建材质
            var shader = Resources.Load<Shader>("Shader/brg_terrain");
            m_material = new Material(shader);
            m_material.SetTexture(shaderPropertyLm, m_lightmaps);
            m_material.EnableKeyword("DOTS_INSTANCING_ON");
            m_materialID = brg.RegisterMaterial(m_material);

            //创建SystemBuffer
            m_systemBufferL2w = new NativeArray<BigWorldUtility.PackedMatrix>(instanceCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_systemBufferLmSt = new NativeArray<float4>(instanceCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_systemBufferLmIndex = new NativeArray<float>(instanceCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_systemBufferLmAmount = new NativeArray<float>(instanceCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            //创建GBuffer
            var buffSize = instanceCount * BigWorldTerrainBatchRenderGroup.sizeOfPerInstance + BigWorldTerrainBatchRenderGroup.sizeOfGBufferHead;
            m_instanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, (int)buffSize / sizeof(int), sizeof(int));
            m_instanceData.SetData(new[] { Matrix4x4.zero }, 0, 0, 1);

            //metadata
            uint byteAddressOtw = BigWorldTerrainBatchRenderGroup.sizeOfGBufferHead;
            uint byteAddressLmSt = byteAddressOtw + (uint)(BigWorldUtility.sizeOfPackedMatrix * instanceCount);
            uint byteAddressLmIndex = byteAddressLmSt + (uint)(BigWorldUtility.sizeOfFloat4 * instanceCount);
            uint byteAddressLmAmount = byteAddressLmIndex + (uint)(BigWorldUtility.sizeOfFloat * instanceCount);
            var metadata = new NativeArray<MetadataValue>(4, Allocator.Temp);
            metadata[0] = new MetadataValue { NameID = shaderPropertyOtw, Value = 0x80000000 | byteAddressOtw };
            metadata[1] = new MetadataValue { NameID = shaderPropertyLmSt, Value = 0x80000000 | byteAddressLmSt };
            metadata[2] = new MetadataValue { NameID = shaderPropertyLmIndex, Value = 0x80000000 | byteAddressLmIndex };
            metadata[3] = new MetadataValue { NameID = shaderPropertyLmAmount, Value = 0x80000000 | byteAddressLmAmount };

            //add batch
            m_batchID = brg.AddBatch(metadata, m_instanceData.bufferHandle);
        }

        public void Destroy(BatchRendererGroup brg)
        {
            brg.UnregisterMaterial(m_materialID);
            brg.RemoveBatch(m_batchID);
            
            m_instanceData.Dispose();
            m_systemBufferL2w.Dispose();
            m_systemBufferLmSt.Dispose();
            m_systemBufferLmIndex.Dispose();
            m_systemBufferLmAmount.Dispose();

            UnityEngine.Object.Destroy(m_material);
            UnityEngine.Object.Destroy(m_lightmaps);
        }

        public void SetLightmap(int chunkX, int chunkZ)
        {
            var lightmap = Resources.Load<Texture2D>($"BigWorld/{BigWorldManager.instance.worldName}/terrain_lm/chunk_{chunkX}_{chunkZ}/tlqlm");
            m_lightmaps.SetPixelData(lightmap.GetPixelData<byte>(0), 0, 0);
            m_lightmaps.Apply();
        }

        /// <summary>
        /// 刷新BatchGroup数据
        /// </summary>
        /// <param name="renderNodes">需要渲染的Terrain节点</param>
        public void Refresh(List<BigWorldTerrainQuadTreeNode> renderNodes)
        {
            var chunkX = BigWorldUtility.GetChunkCoordinate(renderNodes[0].posX);
            var chunkZ = BigWorldUtility.GetChunkCoordinate(renderNodes[0].posZ);
            var lqlm = Resources.Load<Texture2D>($"BigWorld/{BigWorldManager.instance.worldName}/terrain_lm/chunk_{chunkX}_{chunkZ}/tlqlm");
            m_lightmaps.SetPixelData(lqlm.GetPixelData<byte>(0), 0, 0);

            var lightmaps = new List<Texture2D>() { lqlm };

            m_visibleInstanceCount = renderNodes.Count;
            for (var i = 0; i < m_visibleInstanceCount; ++i)
            {
                var renderNode = renderNodes[i];
                var position = new Vector3(renderNode.posX, 0, renderNode.posZ);
                var scale = new Vector3(renderNode.size, renderNode.size, renderNode.size);
                m_systemBufferL2w[i] = new BigWorldUtility.PackedMatrix(Matrix4x4.TRS(position, Quaternion.identity, scale));
                m_systemBufferLmAmount[i] = (3 - renderNode.lod) / 4.0f;

                if (renderNode.lod == 0)
                {
                    var stepX = BigWorldUtility.GetStepCoordinate(renderNode.posX);
                    var stepZ = BigWorldUtility.GetStepCoordinate(renderNode.posZ);
                    var lightmapName = $"thqlm_{stepX}_{stepZ}";
                    var index = lightmaps.FindIndex((lm) => lm.name == lightmapName);
                    if (index < 0)
                    {
                        index = lightmaps.Count;
                        var hqlm = Resources.Load<Texture2D>($"BigWorld/{BigWorldManager.instance.worldName}/terrain_lm/chunk_{chunkX}_{chunkZ}/{lightmapName}");
                        hqlm.name = lightmapName;
                        m_lightmaps.SetPixelData(hqlm.GetPixelData<byte>(0), 0, index);
                        lightmaps.Add(hqlm);
                    }

                    m_systemBufferLmSt[i] = new float4(1, 1, 0, 0);
                    m_systemBufferLmIndex[i] = index;
                }
                else if (renderNode.lod == 1)
                {
                    var blockX = BigWorldUtility.GetBlockCoordinate(renderNode.posX);
                    var blockZ = BigWorldUtility.GetBlockCoordinate(renderNode.posZ);
                    var lightmapName = $"tmqlm_{blockX}_{blockZ}";
                    var index = lightmaps.FindIndex((lm) => lm.name == lightmapName);
                    if (index < 0)
                    {
                        index = lightmaps.Count;
                        var mqlm = Resources.Load<Texture2D>($"BigWorld/{BigWorldManager.instance.worldName}/terrain_lm/chunk_{chunkX}_{chunkZ}/{lightmapName}");
                        mqlm.name = lightmapName;
                        m_lightmaps.SetPixelData(mqlm.GetPixelData<byte>(0), 0, index);
                        lightmaps.Add(mqlm);
                    }

                    var x = renderNode.posX / BigWorldUtility.blockSize;
                    var y = renderNode.posZ / BigWorldUtility.blockSize;
                    var s = renderNode.size / BigWorldUtility.blockSize;
                    m_systemBufferLmSt[i] = new float4(s, s, x, y);
                    m_systemBufferLmIndex[i] = index;
                }
                else
                {
                    var x = renderNode.posX / BigWorldUtility.chunkSize;
                    var y = renderNode.posZ / BigWorldUtility.chunkSize;
                    var s = renderNode.size / BigWorldUtility.chunkSize;
                    m_systemBufferLmSt[i] = new float4(s, s, x, y);
                    m_systemBufferLmIndex[i] = 0;
                }
            }
            m_lightmaps.Apply();

            uint byteAddressOtw = BigWorldTerrainBatchRenderGroup.sizeOfGBufferHead;
            uint byteAddressLmSt = byteAddressOtw + (uint)(BigWorldUtility.sizeOfPackedMatrix * instanceCount);
            uint byteAddressLmIndex = byteAddressLmSt + (uint)(BigWorldUtility.sizeOfFloat4 * instanceCount);
            uint byteAddressLmAmount = byteAddressLmIndex + (uint)(BigWorldUtility.sizeOfFloat * instanceCount);
            m_instanceData.SetData(m_systemBufferL2w, 0, (int)(byteAddressOtw / BigWorldUtility.sizeOfPackedMatrix), m_visibleInstanceCount);
            m_instanceData.SetData(m_systemBufferLmSt, 0, (int)(byteAddressLmSt / BigWorldUtility.sizeOfFloat4), m_visibleInstanceCount);
            m_instanceData.SetData(m_systemBufferLmIndex, 0, (int)(byteAddressLmIndex / BigWorldUtility.sizeOfFloat), m_visibleInstanceCount);
            m_instanceData.SetData(m_systemBufferLmAmount, 0, (int)(byteAddressLmAmount / BigWorldUtility.sizeOfFloat), m_visibleInstanceCount);
        }
    }
}