using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigCat.BigWorld
{
    [Serializable]
    public class BigWorldConfig : ScriptableObject
    {
        public List<BigWorldCellConfig> cellConfigs = new List<BigWorldCellConfig>();
    }
}