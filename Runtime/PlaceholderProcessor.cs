using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using PEAKLevelLoader.Core;
using static UnityEngine.UIElements.UIR.Implementation.UIRStylePainter;
using UnityEngine.Rendering.VirtualTexturing;
public static class PlaceholderProcessor
{
    private static readonly string[] LuggageTypeNames = new string[] {
        "Luggage", "LuggageItem", "LuggageController", "Item", "Pickup", "LuggageBehaviour"
    };

    private static readonly string[] FogOriginTypeNames = new string[] {
        "FogSphereOrigin",
    };

    private static readonly string[] CampfireTypeNames = new string[] {
        "Campfire"
    };

    public static void ProcessPlaceholdersSync(GameObject instGO, LevelPack pack, object mapSegObj, int insertionIndex)
    {
        if (instGO == null || pack == null) return;

        try
        {
            var placeholders = instGO.GetComponentsInChildren<Placeholder>(true) ?? Array.Empty<Placeholder>();
            foreach (var ph in placeholders)
            {
                try
                {
                    HandleSinglePlaceholder(ph.gameObject, ph, pack, mapSegObj, insertionIndex);
                }
                catch (Exception ex)
                {
                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: failed processing placeholder '{ph.name}': {ex}");
                }
            }

            var fogCandidates = instGO.GetComponentsInChildren<Transform>(true)
                                      .Where(t => t.name.IndexOf("fogorigin", StringComparison.OrdinalIgnoreCase) >= 0
                                               || t.name.IndexOf("fog_origin", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            foreach (var t in fogCandidates)
            {
                try
                {
                    HandleFogOriginGameObject(t.gameObject, pack, insertionIndex);
                }
                catch (Exception ex)
                {
                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: FogOrigin handle failed for {t.name}: {ex}");
                }
            }

            var luggageCandidates = instGO.GetComponentsInChildren<Transform>(true)
                                      .Where(t => t.name.IndexOf("luggage_", StringComparison.OrdinalIgnoreCase) >= 0
                                               || t.name.IndexOf("luggage", StringComparison.OrdinalIgnoreCase) >= 0)
                                      .Select(t => t.gameObject).ToArray();
            foreach (var go in luggageCandidates)
            {
                try
                {
                    HandleLuggageGameObject(go, pack);
                }
                catch (Exception ex)
                {
                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: Luggage handle failed for {go.name}: {ex}");
                }
            }

            // -!> DO NOT ENABLE <!- //
            /*var campfireObjs = instGO.GetComponentsInChildren<Transform>(true)
                                     .Where(t => t.name.IndexOf("campfire", StringComparison.OrdinalIgnoreCase) >= 0)
                                     .Select(t => t.gameObject).ToArray();
            foreach (var go in campfireObjs)
            {
                try { EnsureRuntimeCampfire(go, pack); }
                catch (Exception ex) { PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: Campfire wiring failed for {go.name}: {ex}"); }
            }*/
        }
        catch (Exception outerEx)
        {
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogError($"PlaceholderProcessor: top-level failure: {outerEx}");
        }
    }

    private static void HandleSinglePlaceholder(GameObject placeholderGO, Placeholder ph, LevelPack pack, object mapSegObj, int insertionIndex)
    {
        if (placeholderGO == null) return;

        string role = (ph.role ?? "").Trim().ToLowerInvariant();
        string spawnableName = (ph.spawnableName ?? "").Trim();

        if (!string.IsNullOrEmpty(role))
        {
            if (role == "fogorigin" || role == "fog_origin" || role == "fog")
            {
                HandleFogOriginGameObject(placeholderGO, pack, insertionIndex, ph);
                return;
            }
            if (role.Contains("luggage") || role.Contains("bag") || role.Contains("suitcase"))
            {
                HandleLuggageGameObject(placeholderGO, pack, ph);
                return;
            }
            if (role.Contains("campfire"))
            {
                EnsureRuntimeCampfire(placeholderGO, pack);
                return;
            }
        }

        if (!string.IsNullOrEmpty(spawnableName))
        {
            UnityEngine.GameObject? resolved = null; 
            if (PatchedContent.SpawnableRegistry.Registry.TryGetValue(spawnableName, out var registryEntry))
            {
                try
                {
                    if (SpawnerResolveHelper.TryResolvePrefab(registryEntry, out var prefab) && prefab != null)
                    {
                        var inst = UnityEngine.Object.Instantiate(resolved, placeholderGO.transform.position, placeholderGO.transform.rotation, placeholderGO.transform.parent);
                        inst!.name = $"ModSpawn_{pack.packName}_{resolved!.name}";
                        inst.transform.localScale = placeholderGO.transform.localScale;
                        UnityEngine.Object.DestroyImmediate(placeholderGO);
                        if (spawnableName.IndexOf("luggage", StringComparison.OrdinalIgnoreCase) >= 0)
                            EnsureLuggageBehaviour(inst, pack);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: SpawnableResolver invocation failed: {ex}");
                }
            }
        }

        if (placeholderGO.transform.childCount > 0)
        {
            var clone = UnityEngine.Object.Instantiate(placeholderGO, placeholderGO.transform.parent);
            clone.name = $"ModProp_{pack.packName}_{placeholderGO.name}";
            if (placeholderGO.name.IndexOf("luggage", StringComparison.OrdinalIgnoreCase) >= 0)
                EnsureLuggageBehaviour(clone, pack);
            UnityEngine.Object.DestroyImmediate(placeholderGO);
            return;
        }
    }

    private static void HandleFogOriginGameObject(GameObject fogGO, LevelPack pack, int insertionIndex, Placeholder ph = null!)
    {
        if (fogGO == null) return;

        var fogType = FindTypeInLoadedAssembliesByName(FogOriginTypeNames);
        if (fogType == null)
        {
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo("PlaceholderProcessor: FogSphereOrigin type not found at runtime (will create plain GameObject and register).");
        }

        object compInstance = null!;
        if (fogType != null)
        {
            var comp = fogGO.GetComponent(fogType);
            if (comp != null)
            {
                compInstance = comp;
            }
            else
            {
                try
                {
                    compInstance = fogGO.AddComponent(fogType);
                }
                catch (Exception ex)
                {
                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: Failed to AddComponent({fogType.Name}) on {fogGO.name}: {ex}");
                    compInstance = null!;
                }
            }
        }
        else
        {
            var proxy = fogGO.GetComponent<FogSphereOriginProxy>() ?? fogGO.AddComponent<FogSphereOriginProxy>();
            compInstance = proxy;
        }

        float size = 650f;
        bool disableFog = false;
        float moveOnHeight = 0f;
        float moveOnForward = 0f;
        try
        {
            if (ph != null && !string.IsNullOrEmpty(ph.options))
            {
                var opts = ParseOptionsString(ph.options);
                size = TryParseFloatFromDict(opts, "size", size);
                disableFog = TryParseBoolFromDict(opts, "disableFog", disableFog);
                moveOnHeight = TryParseFloatFromDict(opts, "moveOnHeight", moveOnHeight);
                moveOnForward = TryParseFloatFromDict(opts, "moveOnForward", moveOnForward);
            }
        }
        catch { }

        if (compInstance != null)
        {
            TrySetFieldOrProperty(compInstance, "size", size);
            TrySetFieldOrProperty(compInstance, "disableFog", disableFog);
            TrySetFieldOrProperty(compInstance, "moveOnHeight", moveOnHeight);
            TrySetFieldOrProperty(compInstance, "moveOnForward", moveOnForward);
            TrySetFieldOrProperty(compInstance, "moveOnForward", moveOnForward);
        }

        try
        {
            FogOriginRegistrar.InsertOriginAtIndex((FogSphereOrigin)compInstance!, insertionIndex);
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"PlaceholderProcessor: Registered fog origin '{fogGO.name}' at index {insertionIndex} for pack {pack.packName}");
        }
        catch (Exception ex)
        {
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: failed to register fog origin: {ex}");
        }
    }

    private static void HandleLuggageGameObject(GameObject luggageGO, LevelPack pack, Placeholder ph = null!)
    {
        if (luggageGO == null) return;

        string machineName = luggageGO.name;
        string spawnableKey = machineName;

        GameObject? resolvedPrefab = null;
        try
        {

            if (SpawnerResolveHelper.TryResolvePrefab(spawnableKey, out var prefab) && prefab != null)
            {
                var parent = luggageGO.transform.parent;
                var pos = luggageGO.transform.position;
                var rot = luggageGO.transform.rotation;
                var scale = luggageGO.transform.localScale;
                var inst = UnityEngine.Object.Instantiate(resolvedPrefab, pos, rot, parent);
                inst!.transform.localScale = scale;
                inst.name = $"ModLuggage_{pack.packName}_{resolvedPrefab!.name}";
                UnityEngine.Object.DestroyImmediate(luggageGO);
                EnsureLuggageBehaviour(inst, pack);
                return;
            }
        }
        catch {  }

        EnsureLuggageBehaviour(luggageGO, pack);
    }

    private static void EnsureRuntimeCampfire(GameObject campfireGO, LevelPack pack)
    {
        if (campfireGO == null) return;

        var campfireType = FindTypeInLoadedAssembliesByName(CampfireTypeNames);
        if (campfireType == null)
        {
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"PlaceholderProcessor: Campfire runtime type not found — leaving visual '{campfireGO.name}' as-is.");
            return;
        }

        var comp = campfireGO.GetComponent(campfireType);
        if (comp == null)
        {
            try
            {
                comp = campfireGO.AddComponent(campfireType);
                TrySetFieldOrProperty(comp, "burnsFor", 180f);
                PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"PlaceholderProcessor: attached runtime Campfire component to '{campfireGO.name}'");
            }
            catch (Exception ex)
            {
                PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: failed to add Campfire component to {campfireGO.name}: {ex}");
            }
        }
    }

    private static void EnsureLuggageBehaviour(GameObject go, LevelPack pack)
    {
        if (go == null) return;

        var luggageType = FindTypeInLoadedAssembliesByName(LuggageTypeNames);
        if (luggageType != null)
        {
            try
            {
                var existing = go.GetComponent(luggageType);
                if (existing == null)
                {
                    var comp = go.AddComponent(luggageType);

                    TrySetFieldOrProperty(comp, "nameOverride", go.name);
                    TrySetFieldOrProperty(comp, "disableFogFakeMountain", false);
                }
                PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"PlaceholderProcessor: attached luggage-like component ({luggageType.Name}) to '{go.name}'");
            }
            catch (Exception ex)
            {
                PEAKLevelLoader.PEAKLevelLoader.Logger.LogWarning($"PlaceholderProcessor: error attaching luggage type: {ex}");
            }
        }
        else
        {
            var marker = go.GetComponent<ModLuggageMarker>() ?? go.AddComponent<ModLuggageMarker>();
            marker.packName = pack?.packName ?? "<unknown>";
            PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"PlaceholderProcessor: added ModLuggageMarker to '{go.name}' (no runtime luggage type found).");
        }

        var photonViewType = FindTypeInLoadedAssembliesByName(new string[] { "Photon.Pun.PhotonView", "PhotonView" });
        if (photonViewType != null)
        {
            try
            {
                if (go.GetComponent(photonViewType) == null)
                {
                    go.AddComponent(photonViewType);
                    PEAKLevelLoader.PEAKLevelLoader.Logger.LogInfo($"PlaceholderProcessor: added PhotonView to '{go.name}' for networking.");
                }
            }
            catch { }
        }
    }

    private static Type? FindTypeInLoadedAssembliesByName(params string[] typeNameCandidates)
    {
        foreach (var c in typeNameCandidates)
        {
            var t = FindTypeInLoadedAssembliesByName(new[] { c });
            if (t != null) return t;
        }
        return null;
    }
    private static Type? FindTypeInLoadedAssembliesByName(IEnumerable<string> candidates)
    {
        var candList = candidates.ToArray();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }
            foreach (var t in types)
            {
                if (candList.Any(c => string.Equals(t.Name, c, StringComparison.OrdinalIgnoreCase) || string.Equals(t.FullName, c, StringComparison.OrdinalIgnoreCase)))
                    return t;
            }
        }
        return null;
    }

    private static void TrySetFieldOrProperty(object target, string name, object val)
    {
        if (target == null) return;
        var t = target.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null)
        {
            try { f.SetValue(target, ConvertToType(val, f.FieldType)); return; } catch { }
        }
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.CanWrite)
        {
            try { p.SetValue(target, ConvertToType(val, p.PropertyType)); return; } catch { }
        }
    }

    private static object ConvertToType(object val, Type targetType)
    {
        if (val == null) return null!;
        try
        {
            if (targetType.IsAssignableFrom(val.GetType())) return val;
            if (targetType.IsEnum)
            {
                if (val is string s) return Enum.Parse(targetType, s, true);
                return Enum.ToObject(targetType, val);
            }
            return Convert.ChangeType(val, targetType);
        }
        catch
        {
            return val;
        }
    }

    private static Dictionary<string, string> ParseOptionsString(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(raw)) return dict;
        var parts = raw.Split(new[] { '\n', ';', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var kv = p.Split(new[] { '=' }, 2);
            if (kv.Length == 2) dict[kv[0].Trim()] = kv[1].Trim();
        }
        return dict;
    }

    private static float TryParseFloatFromDict(Dictionary<string, string> dict, string key, float fallback)
    {
        if (dict.TryGetValue(key, out var s) && float.TryParse(s, out var v)) return v;
        return fallback;
    }
    private static bool TryParseBoolFromDict(Dictionary<string, string> dict, string key, bool fallback)
    {
        if (dict.TryGetValue(key, out var s) && bool.TryParse(s, out var v)) return v;
        return fallback;
    }

    public class FogSphereOriginProxy : MonoBehaviour
    {
        public float size = 650f;
        public bool disableFog = false;
        public float moveOnHeight = 0f;
        public float moveOnForward = 0f;
    }
    public class ModLuggageMarker : MonoBehaviour
    {
        public string? packName;
    }
}
