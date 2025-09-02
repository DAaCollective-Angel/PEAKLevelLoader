using System.IO;
using System;
using System.Collections.Generic;
using PEAKLevelLoader.Core;
using UnityEngine;
using System.Linq;
using static AssetBundleGroupDebugger;
using static PEAKLevelLoader.Core.PatchedContent;
public static class SpawnerHelper
{
    public static Vector3 GetLossyScale(this Transform t)
    {
        Vector3 s = t.localScale;
        Transform p = t.parent;
        while (p != null)
        {
            s = Vector3.Scale(s, p.localScale);
            p = p.parent;
        }
        return s;
    }
    public static void SetParentPreserveWorld(Transform child, Transform parent)
    {
        var worldPos = child.position;
        var worldRot = child.rotation;
        var worldScale = child.GetLossyScale();

        child.SetParent(parent, true);
        Vector3 parentLossy = parent != null ? parent.GetLossyScale() : Vector3.one;
        parentLossy.x = Mathf.Approximately(parentLossy.x, 0f) ? 1f : parentLossy.x;
        parentLossy.y = Mathf.Approximately(parentLossy.y, 0f) ? 1f : parentLossy.y;
        parentLossy.z = Mathf.Approximately(parentLossy.z, 0f) ? 1f : parentLossy.z;

        child.localScale = new Vector3(
            worldScale.x / parentLossy.x,
            worldScale.y / parentLossy.y,
            worldScale.z / parentLossy.z
        );

        child.position = worldPos;
        child.rotation = worldRot;
    }
    internal static void RegisterSegmentContentsSync(GameObject instGO, LevelPack pack, GameObject? campfirePrefab, object mapSegObj, Action<string, object?> TrySetBackingField)
    {
        try
        {
            Campfire existingCampfire = instGO.GetComponentInChildren<Campfire>(true);
            GameObject? campfireInstance = null;

            Transform anchor = instGO.transform.Find("CampfireAnchor")
                            ?? instGO.transform.Find("campfireAnchor")
                            ?? instGO.transform.Find("Campfire")
                            ?? instGO.GetComponentsInChildren<Transform>(true)
                                  .FirstOrDefault(t => t.name.IndexOf("campfire", System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (existingCampfire != null)
            {
                campfireInstance = existingCampfire.gameObject;
                if (anchor != null && campfireInstance.transform.parent != anchor)
                    SetParentPreserveWorld(campfireInstance.transform, anchor);
            }
            else if (campfirePrefab != null)
            {
                campfireInstance = UnityEngine.Object.Instantiate(campfirePrefab);
                campfireInstance.name = $"ModCampfire_{pack.packName}_{pack.campfirePrefabName ?? "campfire"}";

                var targetParent = anchor ?? instGO.transform;
                SetParentPreserveWorld(campfireInstance.transform, targetParent);

                if (anchor != null)
                {
                    campfireInstance.transform.localPosition = Vector3.zero;
                    campfireInstance.transform.localRotation = Quaternion.identity;
                }
            }

            if (campfireInstance != null)
            {
                TrySetBackingField("_segmentCampfire", campfireInstance);
                // campfireInstance.SetActive(false);
            }
            else
            {
                PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"RegisterSegmentContents: no campfire found/created for pack {pack.packName}.");
            }
        }
        catch (Exception ex)
        {
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"RegisterSegmentContents: campfire registration failed for {pack.packName}: {ex}");
        }

        try
        {
            var spawners = instGO.GetComponentsInChildren<Spawner>(true);
            if (spawners != null && spawners.Length > 0)
            {
                foreach (var sp in spawners)
                {
                    if (sp.spawnSpots == null || sp.spawnSpots.Count == 0)
                    {
                        var spots = sp.GetComponentsInChildren<Transform>(true)
                                      .Where(t => t.name.IndexOf("spawnspot", System.StringComparison.OrdinalIgnoreCase) >= 0
                                               || t.name.IndexOf("spawn_point", System.StringComparison.OrdinalIgnoreCase) >= 0
                                               || t.name.IndexOf("spawn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                      .Select(t => t).ToList();
                        if (spots.Count > 0)
                            sp.spawnSpots = spots;
                    }
                    foreach (var mapping in pack!.spawnMappings ?? Array.Empty<SpawnMapping>())
                    {
                        var marker = instGO.transform.Find(mapping.spawnerMarker)
                                 ?? instGO.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(t => t.name.IndexOf(mapping.spawnerMarker, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (marker == null) continue;
                        if (!SpawnableRegistry.Registry.TryGetValue(mapping.spawnableName, out var entry))
                            continue;

                        if (SpawnerResolveHelper.TryResolvePrefab(entry, out var rp) && rp != null)
                        {
                            sp.spawnedObjectPrefab = rp;
                            PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"Wired spawner '{sp.name}' -> prefab '{rp.name}'");
                        }
                    }

                    if ((sp.spawnedObjectPrefab == null || sp.spawnedObjectPrefab.name == "null") && pack != null)
                    {
                        foreach (var mapping in pack.spawnMappings ?? Array.Empty<SpawnMapping>())
                        {
                            var marker = instGO.transform.Find(mapping.spawnerMarker)
                                      ?? instGO.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.IndexOf(mapping.spawnerMarker, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (marker == null) continue;
                            if (!marker.IsChildOf(sp.transform) && marker != sp.transform) continue;

                            if (SpawnableRegistry.Registry.TryGetValue(mapping.spawnableName, out var entry))
                            {
                                var prefab = AssetBundleGroupDebugger.BundleResolveHelper.ResolvePrefabFromLoadedGroups(entry.prefab);
                                if (prefab != null)
                                {
                                    sp.spawnedObjectPrefab = prefab;
                                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"Wired spawner '{sp.name}' -> prefab '{entry.prefab}'");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                var markerGroups = instGO.GetComponentsInChildren<Transform>(true)
                                   .Where(t => t.name.IndexOf("spawner", System.StringComparison.OrdinalIgnoreCase) >= 0
                                            || t.name.IndexOf("prop_spawner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                   .ToList();
                foreach (var group in markerGroups)
                {
                    var newSpawner = group.gameObject.AddComponent<Spawner>();
                    newSpawner.spawnOnStart = true;
                    var spots = group.GetComponentsInChildren<Transform>(true)
                                     .Where(t => t.name.IndexOf("spawnspot", System.StringComparison.OrdinalIgnoreCase) >= 0
                                              || t.name.IndexOf("spawn_point", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                     .Select(t => t).ToList();
                    newSpawner.spawnSpots = spots;
                }
            }
        }
        catch (Exception ex)
        {
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"RegisterSegmentContents: spawner registration failed for {pack.packName}: {ex}");
        }
    }
}
