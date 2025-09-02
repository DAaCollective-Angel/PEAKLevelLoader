using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PEAKLevelLoader.Core
{
    public static class ContentTagManager
    {
        internal static Dictionary<string, List<ContentTag>> globalContentTagDictionary = new Dictionary<string, List<ContentTag>>(System.StringComparer.OrdinalIgnoreCase);
        internal static Dictionary<string, List<ExtendedContent>> globalcontentTagExtendedContentDictionary = new Dictionary<string, List<ExtendedContent>>(System.StringComparer.OrdinalIgnoreCase);

        public static void MergeExtendedModTags(ExtendedMod extendedMod)
        {
            if (extendedMod == null) return;
            var found = new Dictionary<string, ContentTag>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var ext in extendedMod.ExtendedContents)
            {
                for (int i = ext.ContentTags.Count - 1; i >= 0; i--)
                {
                    var ct = ext.ContentTags[i];
                    if (ct == null) continue;
                    if (found.TryGetValue(ct.contentTagName!, out var existing))
                    {
                        ext.ContentTags[i] = existing;
                    }
                    else
                    {
                        found[ct.contentTagName!] = ct;
                    }
                }
            }
        }

        public static void MergeAllExtendedModTags()
        {
            var globalFound = new Dictionary<string, ContentTag>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var mod in PatchedContent.ExtendedMods)
            {
                foreach (var ext in mod.ExtendedContents)
                {
                    for (int i = 0; i < ext.ContentTags.Count; i++)
                    {
                        var ct = ext.ContentTags[i];
                        if (ct == null) continue;
                        if (globalFound.TryGetValue(ct.contentTagName!, out var existing))
                            ext.ContentTags[i] = existing;
                        else
                            globalFound[ct.contentTagName!] = ct;
                    }
                }
            }
        }

        public static void PopulateContentTagData()
        {
            globalContentTagDictionary.Clear();
            globalcontentTagExtendedContentDictionary.Clear();

            var allTags = new List<ContentTag>();
            foreach (var mod in PatchedContent.ExtendedMods)
                foreach (var ext in mod.ExtendedContents)
                    foreach (var tag in ext.ContentTags)
                        if (tag != null && !allTags.Contains(tag))
                            allTags.Add(tag);

            foreach (var tag in allTags)
            {
                if (!globalContentTagDictionary.TryGetValue(tag.contentTagName!, out var list))
                {
                    list = new List<ContentTag>();
                    globalContentTagDictionary[tag.contentTagName!] = list;
                }
                list.Add(tag);
            }

            foreach (var mod in PatchedContent.ExtendedMods)
                foreach (var ext in mod.ExtendedContents)
                    foreach (var tag in ext.ContentTags)
                    {
                        if (tag == null) continue;
                        if (!globalcontentTagExtendedContentDictionary.TryGetValue(tag.contentTagName!, out var extList))
                        {
                            extList = new List<ExtendedContent>();
                            globalcontentTagExtendedContentDictionary[tag.contentTagName!] = extList;
                        }
                        if (!extList.Contains(ext)) extList.Add(ext);
                    }
        }

        public static List<ExtendedContent> GetAllExtendedContentsByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new List<ExtendedContent>();
            if (globalcontentTagExtendedContentDictionary.TryGetValue(tag, out var list))
                return new List<ExtendedContent>(list);
            return new List<ExtendedContent>();
        }

        public static List<ContentTag> CreateNewContentTags(List<string> tags)
        {
            var result = new List<ContentTag>();
            if (tags == null) return result;
            foreach (var t in tags)
            {
                if (string.IsNullOrEmpty(t)) continue;
                result.Add(ContentTag.Create(t));
            }
            return result;
        }
    }
}