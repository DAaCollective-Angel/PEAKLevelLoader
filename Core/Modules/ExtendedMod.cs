using System;
using System.Collections.Generic;
using System.Net.Mime;
using UnityEngine;

namespace PEAKLevelLoader.Core
{
    [CreateAssetMenu(fileName = "ExtendedMod", menuName = "PEAK/ExtendedMod", order = 10)]
    public class ExtendedMod : ScriptableObject
    {
        [field: SerializeField] public string ModName { get; internal set; } = "Unspecified";
        [field: SerializeField] public string AuthorName { get; internal set; } = "Unknown";
        [field: SerializeField] public string Version { get; internal set; } = "0.0.1";

        [field: SerializeField] public List<string> ModNameAliases { get; internal set; } = new List<string>();
        [field: SerializeField] public List<string> StreamingBundleNames { get; private set; } = new List<string>();

        [field: SerializeField] public List<ExtendedContent> ExtendedContents { get; private set; } = new List<ExtendedContent>();

        public static ExtendedMod CreateNewMod(string? modName = null, string? authorName = null, string? version = null, params ExtendedContent[] contents)
        {
            ExtendedMod m = CreateInstance<ExtendedMod>();
            if (!string.IsNullOrEmpty(modName)) m.ModName = modName;
            if (!string.IsNullOrEmpty(authorName)) m.AuthorName = authorName;
            if (!string.IsNullOrEmpty(version)) m.Version = version;
            m.name = (modName ?? "UnnamedMod").Replace(" ", "_") + "_Mod";
            if (contents != null && contents.Length > 0) m.TryRegisterExtendedContents(contents);
            return m;
        }

        public void TryRegisterExtendedContents(params ExtendedContent[] contents)
        {
            if (contents == null) return;
            foreach (var c in contents) TryRegisterExtendedContent(c);
        }

        public void TryRegisterExtendedContent(ExtendedContent content)
        {
            if (content == null) return;
            try
            {
                if (ExtendedContents.Contains(content))
                {
                    Debug.LogWarning($"ExtendedMod: content {content.name} already registered in {ModName}");
                    return;
                }
                content.Register(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        internal void RegisterExtendedContentInternal(ExtendedContent content)
        {
            if (content == null) return;
            content.ExtendedMod = this;
            if (content.ContentType == ContentType.Custom)
                content.ContentTags.Add(ContentTag.Create("Custom"));
            ExtendedContents.Add(content);
        }

        public void UnregisterAll()
        {
            ExtendedContents.Clear();
        }

        public void SortRegisteredContent()
        {
            ExtendedContents.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
