using System;
using System.Collections.Generic;

namespace PEAKLevelLoader.Core
{
    public static class PatchedContent
    {
        public static List<string> LoadedBundleHashes { get; set; } = new List<string>();
        public static List<string> AllLevelSceneNames { get; set; } = new List<string>();
        public static List<string> LoadedBundleNames { get; set; } = new List<string>();
        public static List<ExtendedMod> ExtendedMods { get; set; } = new List<ExtendedMod>();
        public static void SortExtendedMods()
        {
            ExtendedMods.Sort((a, b) => string.Compare(a.ModName, b.ModName, System.StringComparison.OrdinalIgnoreCase));
            foreach (var m in ExtendedMods) m.SortRegisteredContent();
        }
        public static Dictionary<string, List<ContentTag>> ModDefinedTags { get; internal set; } = new Dictionary<string, List<ContentTag>>();
        public struct SpawnableEntry
        {
            public string prefab;
            public string prefabName;
            public SpawnableEntry(string p, string n) { prefab = p; prefabName = n; }
        }
        public static class SpawnableRegistry
        {
            public static Dictionary<string, SpawnableEntry> Registry = new Dictionary<string, SpawnableEntry>();
        }

    }
}