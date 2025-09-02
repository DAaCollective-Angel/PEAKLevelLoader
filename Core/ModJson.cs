using System;

namespace PEAKLevelLoader.Core
{
    [Serializable]
    public class ModJsonSpawnable
    {
        public string name = "";
        public string prefabName = "";
        public string bundlePath = "";
    }

    [Serializable]
    public class ModJsonSpawnMapping
    {
        public string spawnerMarker = "";
        public string spawnableName = "";
    }

    [Serializable]
    public class ModJsonSegment
    {
        public int index;
        public bool replace;
        public bool isVariant;
        public string? biome;
        public string? segmentPrefab;
        public string? campfirePrefab;
        public string? id;
        public ModJsonSpawnable[]? spawnables;
        public ModJsonSpawnMapping[]? spawnMappings;
    }

    [Serializable]
    public class ModJsonContentTag
    {
        public string? name;
        public string? colorHex;
    }

    [Serializable]
    public class ModJson
    {
        public string? modName;
        public string? author;
        public string? version;
        public string? description;
        public string[]? bundledScenes;
        public ModJsonSegment[]? segments;

        public string[]? contentTags;
        public ModJsonContentTag[]? contentTagObjects;
        public ModJsonSpawnable[]? spawnables;
    }
}
