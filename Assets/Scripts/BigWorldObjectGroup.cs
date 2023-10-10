using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "BigWorld/ObjectGroup")]
public class BigWorldObjectGroup : ScriptableObject
{
    public Mesh mesh;
    public Material material;
    public int count;
    public List<float3> positions;
    public List<quaternion> rotations;
    public List<float3> scales;
}
