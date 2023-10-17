using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "BigWorld/ObjectGroupForBRG")]
public class BigWorldObjectGroupForBRG : ScriptableObject
{
    [Serializable]
    public class Lod
    {
        public Mesh mesh;
        public Material material;
        public float lodMinDistance;
        public float lodMaxDistance;

        public float lodMinDistanceSq => lodMinDistance * lodMinDistance;
        public float lodMaxDistanceSq => lodMaxDistance * lodMaxDistance;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public new string name;

    /// <summary>
    /// LOD
    /// </summary>
    [SerializeField]
    public Lod[] lods;

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
}
