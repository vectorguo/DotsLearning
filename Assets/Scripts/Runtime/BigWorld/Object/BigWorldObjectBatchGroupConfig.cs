using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace BigCat.BigWorld
{
    [CreateAssetMenu(menuName = "BigWorld/ObjectBatchGroupConfig")]
    public class BigWorldObjectBatchGroupConfig : ScriptableObject
    {
        [Serializable]
        public class Lod
        {
            public Mesh mesh;
            public Material material;
            public float lodMinDistance;
            public float lodMaxDistance;
        }

        /// <summary>
        /// LOD
        /// </summary>
        [SerializeField] public Lod[] lods;

        /// <summary>
        /// Object数量
        /// </summary>
        public int count;

        /// <summary>
        /// 位置
        /// </summary>
        public List<Vector3> positions;

        /// <summary>
        /// 旋转
        /// </summary>
        public List<Quaternion> rotations;

        /// <summary>
        /// 缩放
        /// </summary>
        public List<Vector3> scales;

        /// <summary>
        /// Bound
        /// </summary>
        public List<AABB> bounds;

        /// <summary>
        /// 高精度Lightmap索引
        /// </summary>
        public List<int> hqLightmapIndices;

        /// <summary>
        /// 中精度Lightmap索引
        /// </summary>
        public List<int> mqLightmapIndices;

        /// <summary>
        /// 低精度Lightmap索引
        /// </summary>
        public List<int> lqLightmapIndices;

        /// <summary>
        /// 高精度Lightmap ScaleOffset
        /// </summary>
        public List<Vector4> hqLightmapScaleOffsets;

        /// <summary>
        /// 中精度Lightmap ScaleOffset
        /// </summary>
        public List<Vector4> mqLightmapScaleOffsets;

        /// <summary>
        /// 低精度Lightmap ScaleOffset
        /// </summary>
        public List<Vector4> lqLightmapScaleOffsets;
    }
}