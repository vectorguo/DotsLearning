using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    [CreateAssetMenu(menuName = "BigWorld/TerrainBatchGroupConfig")]
    public class BigWorldTerrainBatchGroupConfig : ScriptableObject
    {
        public int count;

        public List<Vector3> positions;
    }
}