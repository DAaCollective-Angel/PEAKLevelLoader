using System.Collections.Generic;
using System;
using UnityEngine;

namespace PEAKLevelLoader.Core
{
    [CreateAssetMenu(fileName = "ExtendedSegment", menuName = "PEAK/ExtendedContent/ExtendedSegment", order = 12)]
    public class ExtendedSegment : ExtendedContent<GameObject>
    {
        public int index = 0;
        public bool replace = false;
        public bool isVariant = false;
        public string biome = string.Empty;
        public string prefabName = string.Empty;
        public string campfirePrefabName = string.Empty;
        public string packName = string.Empty;
        public string id = string.Empty;
        [NonSerialized] public Dictionary<string, GameObject> ResolvedSpawnables = new Dictionary<string, GameObject>();

        internal override void Register(ExtendedMod mod)
        {
            mod.RegisterExtendedContentInternal(this);
        }
    }
}