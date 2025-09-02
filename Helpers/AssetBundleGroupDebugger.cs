using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PEAKLevelLoader.Core;
using UnityEngine;
using static PEAKLevelLoader.Core.PatchedContent;

public static class AssetBundleGroupDebugger
{
    private static readonly Type[] GenericTryTypes = new Type[] {
        typeof(GameObject),
        typeof(UnityEngine.TextAsset),
        typeof(UnityEngine.Object)
    };

    public static class BundleResolveHelper
    {
        public static GameObject? ResolvePrefabFromLoadedGroups(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            var loaderType = typeof(PEAKLevelLoader.Core.AssetBundleLoader);
            var inst = AssetBundleLoader.Instance;
            if (inst == null) return null;
            var groups = inst.AssetBundleGroups;
            if (groups == null) return null;

            foreach (var g in groups)
            {
                if (g == null) continue;
                try
                {
                    var prefab = AssetBundleGroupDebugger.ResolvePrefabFromGroup(g as AssetBundleGroup, prefabName);
                    if (prefab != null) return prefab;
                }
                catch { }
            }
            return null;
        }
    }

    public static void LogGroupContents(object group)
    {
        if (group == null)
        {
            Debug.Log("AssetBundleGroupDebugger: group is null");
            return;
        }

        Type gType = group.GetType();
        Debug.Log($"AssetBundleGroupDebugger: Inspecting group type: {gType.FullName}");

        var groupNameProp = gType.GetProperty("GroupName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                         ?? gType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        string groupName = groupNameProp?.GetValue(group) as string ?? "(unknown)";
        Debug.Log($"GroupName: {groupName}");

        object? assetBundleObj = null;
        var abProp = gType.GetProperty("AssetBundle", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (abProp != null) assetBundleObj = abProp.GetValue(group);
        if (assetBundleObj != null)
        {
            var abType = assetBundleObj.GetType();
            var getAllNamesMethod = abType.GetMethod("GetAllAssetNames", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (getAllNamesMethod != null)
            {
                try
                {
                    var arr = getAllNamesMethod.Invoke(assetBundleObj, null) as string[];
                    if (arr != null && arr.Length > 0)
                    {
                        Debug.Log($"AssetBundle: GetAllAssetNames returned {arr.Length} entries:");
                        foreach (var n in arr) Debug.Log($" - {n}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("GetAllAssetNames invocation failed: " + ex);
                }
            }
        }

        var loadAllMethods = gType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                                  .Where(m => string.Equals(m.Name, "LoadAllAssets", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Name, "LoadAll", StringComparison.OrdinalIgnoreCase))
                                  .ToArray();

        foreach (var m in loadAllMethods)
        {
            try
            {
                if (m.ContainsGenericParameters)
                {
                    foreach (var tryType in GenericTryTypes)
                    {
                        try
                        {
                            var closed = m.MakeGenericMethod(tryType);
                            object result = closed.Invoke(group, null);
                            if (TryLogEnumerableResult(result, $"LoadAllAssets<{tryType.Name}> via {m}"))
                                return;
                        }
                        catch (Exception innerEx)
                        {
                            Debug.Log($"LoadAllAssets<{tryType.Name}> failed: {innerEx.Message}");
                        }
                    }
                }
                else
                {
                    var parms = m.GetParameters();
                    if (parms.Length == 0)
                    {
                        var result = m.Invoke(group, null);
                        if (TryLogEnumerableResult(result, $"LoadAllAssets() via {m}"))
                            return;
                    }
                    else if (parms.Length == 1 && parms[0].ParameterType == typeof(Type))
                    {
                        foreach (var tryType in GenericTryTypes)
                        {
                            try
                            {
                                var result = m.Invoke(group, new object[] { tryType });
                                if (TryLogEnumerableResult(result, $"LoadAllAssets({tryType.Name}) via {m}"))
                                    return;
                            }
                            catch (Exception innerEx)
                            {
                                Debug.LogWarning($"LoadAllAssets failure: {innerEx}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LoadAllAssets invocation failed: {ex}");
            }
        }

        Debug.Log("Fallback: inspecting public enumerable fields/properties...");
        bool foundAnything = false;
        foreach (var p in gType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string))
            {
                try
                {
                    var val = p.GetValue(group);
                    if (val is IEnumerable enumerable)
                    {
                        int i = 0;
                        foreach (var x in enumerable)
                        {
                            i++;
                            if (x == null) { Debug.Log($" - property {p.Name} item {i} == null"); continue; }
                            var name = x.GetType().GetProperty("name")?.GetValue(x) as string ?? "(no name)";
                            Debug.Log($"Property {p.Name} item {i}: {name} ({x.GetType().Name})");
                            foundAnything = true;
                        }
                    }
                }
                catch { }
            }
        }

        if (!foundAnything)
            Debug.Log("AssetBundleGroupDebugger: done (no asset names found by the strategies).");
        else
            Debug.Log("AssetBundleGroupDebugger: done (fallback enumerated items).");
    }

    private static bool TryLogEnumerableResult(object result, string label)
    {
        if (result == null) return false;
        if (result is IEnumerable enumerable)
        {
            Debug.Log($"{label} returned enumerable:");
            int count = 0;
            foreach (var item in enumerable)
            {
                count++;
                if (item == null) { Debug.Log($" - {count}: NULL"); continue; }
                string name = item.GetType().GetProperty("name")?.GetValue(item) as string ?? "(no name)";
                Debug.Log($" - {count}: {name} ({item.GetType().Name})");
            }
            if (count > 0) return true;
        }
        else
        {
            Debug.Log($"{label} returned a {result.GetType().Name} (not enumerable)");
        }
        return false;
    }

    public static GameObject? ResolvePrefabFromGroup(AssetBundleGroup group, string prefabCandidate)
    {
        if (group == null || string.IsNullOrEmpty(prefabCandidate)) return null;

        try
        {
            try
            {
                var assets = group.LoadAllAssets<GameObject>();
                if (assets != null && assets.Count > 0)
                {
                    var found = assets.FirstOrDefault(a => a != null && string.Equals(a.name, prefabCandidate, StringComparison.OrdinalIgnoreCase));
                    if (found != null) return found;
                }
            }
            catch { }

            try
            {
                var infos = group.GetAssetBundleInfos();
                foreach (var info in infos)
                {
                    var ab = info.AssetBundleReference;
                    if (ab == null) continue;

                    foreach (var assetName in ab.GetAllAssetNames())
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(assetName);
                        if (string.Equals(fileName, prefabCandidate, StringComparison.OrdinalIgnoreCase))
                        {
                            var go = ab.LoadAsset<GameObject>(assetName);
                            if (go != null) return go;
                        }
                    }
                }
            }
            catch { }

            try
            {
                var all = group.LoadAllAssets<UnityEngine.Object>();
                if (all != null)
                {
                    foreach (var o in all)
                    {
                        if (o == null) continue;
                        if (o is GameObject go && string.Equals(go.name, prefabCandidate, StringComparison.OrdinalIgnoreCase))
                            return go;
                        if (o.name != null && string.Equals(System.IO.Path.GetFileNameWithoutExtension(o.name), prefabCandidate, StringComparison.OrdinalIgnoreCase))
                        {
                            if (o is GameObject go2) return go2;
                        }
                    }
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ResolvePrefabFromGroup error: {ex}");
        }

        return null;
    }

    private static GameObject? FindMatchingGameObjectInEnumerable(IEnumerable enumerable, string candidate)
    {
        if (enumerable == null) return null!;
        foreach (var item in enumerable)
        {
            if (item == null) continue;
            var itemType = item.GetType();
            string? name = itemType.GetProperty("name")?.GetValue(item) as string ?? null;
            if (!string.IsNullOrEmpty(name))
            {
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase) || string.Equals(System.IO.Path.GetFileNameWithoutExtension(name), candidate, StringComparison.OrdinalIgnoreCase))
                    return item as GameObject;
            }

            if (item is string s)
            {
                string tail = System.IO.Path.GetFileNameWithoutExtension(s);
                if (string.Equals(tail, candidate, StringComparison.OrdinalIgnoreCase) || s.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                    return null;
            }
        }
        return null;
    }
}
