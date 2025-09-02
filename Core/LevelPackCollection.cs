using System;

namespace PEAKLevelLoader.Core
{
    [Serializable]
    public class LevelPack
    {
        public int index = 0;
        public bool replace = false;
        public bool isVariant = false;
        public string biome = string.Empty;
        public string bundlePath = string.Empty;
        public string prefabName = string.Empty;
        public string campfirePrefabName = string.Empty;
        public string packName = string.Empty;
        public string id = string.Empty;
        public SpawnMapping[] spawnMappings = Array.Empty<SpawnMapping>();
    }

    [Serializable]
    public class LevelPackCollection
    {
        public LevelPack[] packs = Array.Empty<LevelPack>();
    }

    [Serializable]
    public class SpawnMapping
    {
        public string spawnerMarker = "";
        public string spawnableName = "";
    }
}
