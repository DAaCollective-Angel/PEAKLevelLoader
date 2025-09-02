using System;
using System.Reflection;
using UnityEngine;

public static class SpawnerResolveHelper
{
    public static string? GetPrefabNameFromEntry(object? entry)
    {
        if (entry == null) return null;
        var t = entry.GetType();

        string[] candidates = new[] { "prefab", "prefabName", "PrefabName", "prefabPath", "path" };

        foreach (var cand in candidates)
        {
            var p = t.GetProperty(cand, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
            {
                try
                {
                    var val = p.GetValue(entry) as string;
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                catch { }
            }
            var f = t.GetField(cand, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                try
                {
                    var val = f.GetValue(entry) as string;
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                catch { }
            }
        }
        try
        {
            var s = entry.ToString();
            if (!string.IsNullOrEmpty(s)) return s;
        }
        catch { }

        return null;
    }
    public static GameObject? ResolvePrefabFromRegistryEntry(object? entry)
    {
        string? name = GetPrefabNameFromEntry(entry);
        if (string.IsNullOrEmpty(name)) return null;

        try
        {
            return AssetBundleGroupDebugger.BundleResolveHelper.ResolvePrefabFromLoadedGroups(name!);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SpawnerResolveHelper.ResolvePrefabFromRegistryEntry: failed resolving '{name}': {ex}");
            return null;
        }
    }
    public static bool TryResolvePrefab(object? registryEntry, out GameObject? resolvedPrefab)
    {
        resolvedPrefab = null;
        if (registryEntry == null) return false;

        if (registryEntry is string s)
        {
            resolvedPrefab = AssetBundleGroupDebugger.BundleResolveHelper.ResolvePrefabFromLoadedGroups(s);
            return resolvedPrefab != null;
        }

        var type = registryEntry.GetType();
        string[] candidateNames = new string[] { "prefab", "prefabName", "Prefab", "PrefabName", "bundlePath", "path" };

        foreach (var name in candidateNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(string))
            {
                try
                {
                    var val = prop.GetValue(registryEntry) as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        resolvedPrefab = AssetBundleGroupDebugger.BundleResolveHelper.ResolvePrefabFromLoadedGroups(val);
                        if (resolvedPrefab != null) return true;
                    }
                }
                catch { }
            }

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
            {
                try
                {
                    var val = field.GetValue(registryEntry) as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        resolvedPrefab = AssetBundleGroupDebugger.BundleResolveHelper.ResolvePrefabFromLoadedGroups(val);
                        if (resolvedPrefab != null) return true;
                    }
                }
                catch { }
            }
        }

        try
        {
            string rep = registryEntry.ToString() ?? "";
            var last = rep.Split(new char[] { ':', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (last.Length > 0)
            {
                var candidate = last[last.Length - 1];
                if (!string.IsNullOrEmpty(candidate))
                {
                    resolvedPrefab = AssetBundleGroupDebugger.BundleResolveHelper.ResolvePrefabFromLoadedGroups(candidate);
                    if (resolvedPrefab != null) return true;
                }
            }
        }
        catch { }
        return false;
    }
}
