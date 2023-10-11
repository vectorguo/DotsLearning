using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "BigWorld/ObjectGroupForBRG")]
public class BigWorldObjectGroupForBRG : ScriptableObject
{
    public Mesh mesh;
    public Material material;
    public int count;
    public List<Vector3> positions;
    public List<Quaternion> rotations;
    public List<Vector3> scales;
}
