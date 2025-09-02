using System.Text;
using UnityEngine;
using PEAKLevelLoader.Core;

public class PatchedContentDebug : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
            DumpPatchedContent();
    }

    public static void DumpPatchedContent()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== PatchedContent Dump ====");
        sb.AppendLine($"LoadedBundleHashes: {PatchedContent.LoadedBundleHashes.Count}");
        sb.AppendLine($"LoadedBundleNames: {PatchedContent.LoadedBundleNames.Count}");
        sb.AppendLine($"AllLevelSceneNames: {PatchedContent.AllLevelSceneNames.Count}");
        sb.AppendLine($"ExtendedMods: {PatchedContent.ExtendedMods.Count}");
        foreach (var mod in PatchedContent.ExtendedMods)
        {
            sb.AppendLine($" - Mod: {mod.ModName} (Author: {mod.AuthorName})");
            if (mod.ExtendedContents != null)
            {
                foreach (var ext in mod.ExtendedContents)
                {
                    sb.AppendLine($"   - Content: {ext.name} (Type: {ext.ContentType}, Tags: {string.Join(',', ext.ContentTags?.ConvertAll(t => t?.contentTagName ?? "null") ?? new System.Collections.Generic.List<string>())})");
                }
            }
        }
        sb.AppendLine("==== End Dump ====");
        Debug.Log(sb.ToString());
    }
}
