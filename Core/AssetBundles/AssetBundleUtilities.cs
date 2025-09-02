using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Security.Cryptography;
using System.Text;

namespace PEAKLevelLoader.Core
{
    internal static class AssetBundleUtilities
    {
        public static string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return "Invalid Scene Path";
            var normalized = scenePath.Replace('\\', '/');
            if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return "Invalid Scene Path";
            return Path.GetFileNameWithoutExtension(normalized);
        }

        public static List<string> GetSceneNamesFromLoadedAssetBundle(AssetBundle assetBundle)
        {
            var sceneNames = new List<string>();
            if (assetBundle == null) return sceneNames;
            try
            {
                if (assetBundle.isStreamedSceneAssetBundle)
                {
                    foreach (var p in assetBundle.GetAllScenePaths())
                        sceneNames.Add(GetSceneName(p));
                    return sceneNames;
                }

                foreach (var assetName in assetBundle.GetAllAssetNames())
                {
                    if (assetName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        sceneNames.Add(GetSceneName(assetName));
                }
            }
            catch (Exception ex) { Debug.LogWarning($"GetSceneNamesFromLoadedAssetBundle failed: {ex}"); }
            return sceneNames;
        }

        public static string GetDisplayName(List<AssetBundleInfo> bundleInfos)
        {
            if (bundleInfos == null || bundleInfos.Count == 0) return string.Empty;
            AssetBundleInfo best = bundleInfos[0];
            var bestCount = best.GetSceneNames().Count;
            for (int i = 1; i < bundleInfos.Count; i++)
            {
                var c = bundleInfos[i].GetSceneNames().Count;
                if (c > bestCount) { best = bundleInfos[i]; bestCount = c; }
            }
            var name = best.AssetBundleName;
            if (string.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(best.AssetBundleFileName);
            if (string.IsNullOrEmpty(name)) return string.Empty;
            name = name.TrimEnd('.');
            if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return name;
        }

        public static string GetLoadingPercentage(float progress) => progress.ToString("F0") + "%";

        public static string ComputeSHA256(string fullFilePath)
        {
            if (!File.Exists(fullFilePath)) return string.Empty;
            using (var fs = File.OpenRead(fullFilePath))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }
    }
}
