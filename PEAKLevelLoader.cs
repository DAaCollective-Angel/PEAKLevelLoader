using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;
using PEAKLevelLoader.Core;
using BepInEx.Bootstrap;
using System.Reflection;
using UnityEngine.TextCore.Text;
using System.Linq;
using Zorro.Core;
using System.Collections.Generic;
using System.Collections;
using static UnityEngine.InputSystem.Controls.AxisControl;

namespace PEAKLevelLoader
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]

    public class PEAKLevelLoader : BaseUnityPlugin
    {
        public static PEAKLevelLoader Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        internal static Harmony Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private LevelPackCollection _packs = new LevelPackCollection();
        private AssetBundleLoader assetBundleLoader = null!;
        public ConfigFile config = null!;
        internal static bool AllowEmbeddedBundles = false;

        private void Awake() {
            Logger = base.Logger;
            Instance = this;

            InitializeConfig();
            PEAKLevelLoader.Logger.LogWarning($"< Config initialized >");

            Logger.LogInfo($"< PEAKLevelLoader main sector loaded... >");
            
            Harmony.PatchAll();
            PatchHooks.ApplyPatches(Harmony);

            GameObject assetBundleLoaderObject = new GameObject("PEAKLevelLoader-AssetBundleLoader");
            assetBundleLoader = assetBundleLoaderObject.AddComponent<AssetBundleLoader>();
            if (Application.isEditor) DontDestroyOnLoad(assetBundleLoaderObject); else assetBundleLoaderObject.hideFlags = HideFlags.HideAndDontSave;

            PEAKBundleManager.Start();
            StartCoroutine(WaitForMapHandlerAndApply());

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has fully loaded!");
        }
        private void InitializeConfig() {
            string configFolderPath = Path.Combine(Paths.ConfigPath, "DAa Mods/PEAKLevelLoader");
            if (!Directory.Exists(configFolderPath)) {
                Directory.CreateDirectory(configFolderPath);
            }
            string configFilePath = Path.Combine(configFolderPath, "config.cfg");
            config = new ConfigFile(configFilePath, true);

            if (config == null)
            {
                Logger.LogError($"The config file is null and the program cannot continue operations.");
                return;
            }

            string ConfigVersion = config.Bind("Version", "Current Version", "").Value;
            if (ConfigVersion != MyPluginInfo.PLUGIN_VERSION)
            {
                config.Clear();
            }
            DefineConfig();
        }
        private void DefineConfig() {
            config.Bind("Version", "Current Version", MyPluginInfo.PLUGIN_VERSION, "Autoupdates the config / lets the mod know what version of config it is.");

            config.Bind("PEAKLevelLoader", "EnableCustoms", true, "Will custom segments load.");
            AllowEmbeddedBundles = config.Bind("PEAKLevelLoader", "AllowEmbeddedBundles", false, "If true, the loader will extract .pll resources embedded in already-loaded plugin assemblies. Not recommended due to extra memory usage.").Value;
        }

        public LevelPackCollection GetPacks() => _packs ?? new LevelPackCollection();
        public void AddPacks(LevelPackCollection newPacks)
        {
            if (newPacks == null || newPacks.packs == null || newPacks.packs.Length == 0) return;

            var existing = _packs?.packs ?? Array.Empty<LevelPack>();
            var merged = existing.Concat(newPacks.packs).ToArray();

            var deduped = merged
                .GroupBy(p => string.IsNullOrEmpty(p.id) ? p.packName : p.id)
                .Select(g => g.First())
                .ToArray();

            _packs!.packs = deduped;
            Logger.LogInfo($"PEAKLevelLoader: Added {newPacks.packs.Length} packs. Total unique packs: {_packs.packs.Length}");
        }
        private IEnumerator WaitForMapHandlerAndApply()
        {
            float timeout = 10f;
            float start = Time.realtimeSinceStartup;
            while (Singleton<MapHandler>.Instance == null)
            {
                if (Time.realtimeSinceStartup - start > timeout) yield break;
                yield return null;
            }
            float start2 = Time.realtimeSinceStartup;
            while (!PEAKBundleManager.HasFinalisedFoundContent)
            {
                if (Time.realtimeSinceStartup - start2 > (timeout * 3f))
                {
                    PEAKLevelLoader.Logger.LogWarning("WaitForMapHandlerAndApply: timed out waiting for PEAKBundleManager to finish.");
                    break;
                }
                yield return null;
            }

            try
            {
                ApplyPacksToMapHandler(Singleton<MapHandler>.Instance);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error applying packs to MapHandler: " + ex);
            }
        }
        private IEnumerator EnsureGroupLoadedThenResolve(AssetBundleGroup group, string prefabCandidate, Action<GameObject?> onResolved)
        {
            if (group == null) { onResolved(null); yield break; }
            if (group.LoadedStatus != AssetBundleGroupLoadedStatus.Loaded)
            {
                try { group.TryLoadGroup(); } catch { }
                float start = Time.realtimeSinceStartup;
                float timeout = 10f;
                while (group.LoadedStatus != AssetBundleGroupLoadedStatus.Loaded)
                {
                    if (Time.realtimeSinceStartup - start > timeout)
                    {
                        PEAKLevelLoader.Logger.LogWarning($"EnsureGroupLoadedThenResolve: timed out loading group {group.GroupName}");
                        break;
                    }
                    yield return null;
                }
            }
            GameObject? prefab = AssetBundleGroupDebugger.ResolvePrefabFromGroup(group, prefabCandidate);
            onResolved(prefab);
        }

        public void ApplyPacksToMapHandler(MapHandler mapHandlerInstance)
        {
            StartCoroutine(ApplyPacksToMapHandlerCoroutine(mapHandlerInstance));
        }
        private IEnumerator ApplyPacksToMapHandlerCoroutine(MapHandler mapHandlerInstance)
        {
            if (mapHandlerInstance == null) yield break;
            if (_packs == null || _packs.packs == null || _packs.packs.Length == 0) yield break;

            var originalSegmentsArray = mapHandlerInstance.segments ?? Array.Empty<MapHandler.MapSegment>();
            var originalList = originalSegmentsArray.ToList();
            int origLen = originalList.Count;

            var groupsObj = assetBundleLoader?.AssetBundleGroups ?? AssetBundleLoader.Instance?.AssetBundleGroups;
            List<AssetBundleGroup> groups = groupsObj?.ToList() ?? new List<AssetBundleGroup>();

            if (groups.Count == 0)
            {
                Logger.LogWarning("ApplyPacksToMapHandlerCoroutine: no AssetBundleGroups available.");
            }
            foreach (var pack in _packs.packs)
            {
                GameObject? resolvedPrefab = null;
                yield return StartCoroutine(FindPrefabAcrossGroups(pack.prefabName, prefab => resolvedPrefab = prefab));

                if (resolvedPrefab == null)
                {
                    Logger.LogWarning($"ApplyPacksToMapHandler: couldn't find prefab '{pack.prefabName}' for pack '{pack.packName}'.");
                    yield return null;
                    continue;
                }

                GameObject? campfirePrefab = null;
                if (!string.IsNullOrEmpty(pack.campfirePrefabName))
                {
                    yield return StartCoroutine(FindPrefabAcrossGroups(pack.campfirePrefabName, prefab => campfirePrefab = prefab));
                }

                GameObject instGO = null!;
                try
                {
                    var parentTransform = mapHandlerInstance.globalParent;
                    instGO = UnityEngine.Object.Instantiate(resolvedPrefab, parentTransform);
                    instGO.name = $"ModSegment_{pack.packName}_{pack.prefabName}";
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ApplyPacksToMapHandler: failed to instantiate prefab for pack {pack.packName}: {ex}");
                    continue;
                }

                var mapSegObj = (MapHandler.MapSegment)Activator.CreateInstance(typeof(MapHandler.MapSegment))!;
                var mapSegTypeLocal = typeof(MapHandler.MapSegment);

                void TrySetBackingField(string fieldName, object? value)
                {
                    var f = mapSegTypeLocal.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (f != null)
                    {
                        try
                        {
                            var targetType = f.FieldType;
                            if (value == null) { f.SetValue(mapSegObj, null); return; }
                            if (targetType.IsAssignableFrom(value.GetType())) { f.SetValue(mapSegObj, value); return; }
                            if (targetType == typeof(Transform) && value is GameObject go) { f.SetValue(mapSegObj, go.transform); return; }
                            if (targetType == typeof(GameObject) && value is Transform tr) { f.SetValue(mapSegObj, tr.gameObject); return; }
                            Logger.LogWarning($"TrySetBackingField: type mismatch for {fieldName}.");
                        }
                        catch (Exception ex) { Logger.LogWarning($"TrySetBackingField: {ex}"); }
                    }
                    var p = mapSegTypeLocal.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.CanWrite)
                    {
                        try
                        {
                            var targetType = p.PropertyType;
                            if (value == null) { p.SetValue(mapSegObj, null); return; }
                            if (targetType.IsAssignableFrom(value.GetType())) { p.SetValue(mapSegObj, value); return; }
                            if (targetType == typeof(Transform) && value is GameObject go2) { p.SetValue(mapSegObj, go2.transform); return; }
                            if (targetType == typeof(GameObject) && value is Transform tr2) { p.SetValue(mapSegObj, tr2.gameObject); return; }
                            Logger.LogWarning($"TrySetBackingField(prop): type mismatch for {fieldName}.");
                        }
                        catch (Exception ex) { Logger.LogWarning($"TrySetBackingField(prop): {ex}"); }
                    }
                }

                object? biomeEnum = null;
                if (!string.IsNullOrEmpty(pack.biome))
                {
                    try { biomeEnum = Enum.Parse(typeof(Biome.BiomeType), pack.biome, true); } catch { biomeEnum = null; }
                }

                if (biomeEnum != null) TrySetBackingField("_biome", biomeEnum);
                TrySetBackingField("_segmentParent", instGO);
                TrySetBackingField("_segmentCampfire", null);

                SpawnerHelper.RegisterSegmentContentsSync(instGO, pack, campfirePrefab, mapSegObj, (name, val) =>
                {
                    var f = typeof(MapHandler.MapSegment).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (f != null) { f.SetValue(mapSegObj, val); return; }
                    var p = typeof(MapHandler.MapSegment).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.CanWrite) p.SetValue(mapSegObj, val);
                });

                int insertionIndex = (pack.index < origLen) ? pack.index : originalList.Count;
                PlaceholderProcessor.ProcessPlaceholdersSync(instGO, pack, mapSegObj, insertionIndex); 
                try
                {
                    if (campfirePrefab != null)
                    {
                        Transform? anchor = instGO.transform.Find("CampfireAnchor")
                            ?? instGO.transform.Find("campfireAnchor")
                            ?? instGO.transform.Find("Campfire")
                            ?? instGO.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("campfire", StringComparison.OrdinalIgnoreCase) >= 0);

                        var campfireInstance = UnityEngine.Object.Instantiate(campfirePrefab);
                        campfireInstance.name = $"ModCampfire_{pack.packName}_{pack.campfirePrefabName}";
                        var desiredWorldScale = campfireInstance.transform.lossyScale;
                        var targetParent = (anchor != null) ? anchor : instGO.transform;
                        var parentLossy = targetParent != null ? targetParent.lossyScale : Vector3.one;
                        parentLossy.x = Mathf.Approximately(parentLossy.x, 0f) ? 1f : parentLossy.x;
                        parentLossy.y = Mathf.Approximately(parentLossy.y, 0f) ? 1f : parentLossy.y;
                        parentLossy.z = Mathf.Approximately(parentLossy.z, 0f) ? 1f : parentLossy.z;

                        campfireInstance.transform.SetParent(targetParent, false);
                        campfireInstance.transform.localScale = new Vector3(
                            desiredWorldScale.x / parentLossy.x,
                            desiredWorldScale.y / parentLossy.y,
                            desiredWorldScale.z / parentLossy.z
                        );
                        if (anchor != null)
                        {
                            campfireInstance.transform.localPosition = Vector3.zero;
                            campfireInstance.transform.localRotation = Quaternion.identity;
                        }
                        TrySetBackingField("_segmentCampfire", campfireInstance);
                    }

                    var reconnectTransform = instGO.transform.Find("ReconnectSpawnPos")
                                          ?? instGO.transform.Find("reconnectSpawnPos")
                                          ?? instGO.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("reconnect", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (reconnectTransform != null)
                    {
                        var reconnectField = typeof(MapHandler.MapSegment).GetField("reconnectSpawnPos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (reconnectField != null) reconnectField.SetValue(mapSegObj, reconnectTransform);
                    }

                    var wallNextTrans = instGO.transform.Find("WallNext") ?? instGO.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("wallnext", StringComparison.OrdinalIgnoreCase) >= 0);
                    var wallPrevTrans = instGO.transform.Find("WallPrevious") ?? instGO.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("wallprevious", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (wallNextTrans != null)
                    {
                        var wf = typeof(MapHandler.MapSegment).GetField("wallNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (wf != null) wf.SetValue(mapSegObj, wallNextTrans.gameObject);
                    }
                    if (wallPrevTrans != null)
                    {
                        var wf = typeof(MapHandler.MapSegment).GetField("wallPrevious", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (wf != null) wf.SetValue(mapSegObj, wallPrevTrans.gameObject);
                    }

                    var mapSegType = typeof(MapHandler.MapSegment);
                    var hasVariantF = mapSegType.GetField("hasVariant", BindingFlags.Public | BindingFlags.Instance);
                    var variantBiomeF = mapSegType.GetField("variantBiome", BindingFlags.Public | BindingFlags.Instance);
                    var isVariantF = mapSegType.GetField("isVariant", BindingFlags.Public | BindingFlags.Instance);

                    if (hasVariantF != null) hasVariantF.SetValue(mapSegObj, pack.isVariant);
                    if (isVariantF != null) isVariantF.SetValue(mapSegObj, pack.isVariant);
                    if (variantBiomeF != null && biomeEnum != null && pack.isVariant) variantBiomeF.SetValue(mapSegObj, biomeEnum);

                    if (pack.index < origLen)
                    {
                        if (pack.replace) originalList[pack.index] = mapSegObj;
                        else originalList.Insert(pack.index + 1, mapSegObj);
                    }
                    else
                    {
                        originalList.Add(mapSegObj);
                    }

                    string keyForTags = !string.IsNullOrEmpty(pack.bundlePath) ? pack.bundlePath : pack.packName;
                    if (!string.IsNullOrEmpty(keyForTags) && PatchedContent.ModDefinedTags != null)
                    {
                        if (PatchedContent.ModDefinedTags.TryGetValue(keyForTags, out var createdTags) && createdTags != null && createdTags.Count > 0)
                        {
                            var mt = instGO.GetComponent<ModContentTagMarker>() ?? instGO.AddComponent<ModContentTagMarker>();
                            mt.tags = createdTags.ToArray();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ApplyPacksToMapHandler error for pack {pack.packName}: {ex}");
                }

                yield return null;
            }

            MapHandler.MapSegment[] newArr = originalList.ToArray();
            mapHandlerInstance.segments = newArr;

            if (Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                PEAKLevelLoader.Logger.LogInfo(" < Awaiting for sync from non host clients. > ");
                var packs = _packs.packs;
                var sync = PEAKPackSyncPhoton.Instance;
                if (sync != null)
                {
                    sync.SendPacksToClients(packs, waitForAcks: true, onComplete: success =>
                    {
                        if (!success) PEAKLevelLoader.Logger.LogWarning("Not all clients ACK'd pack application in time.");
                        else PEAKLevelLoader.Logger.LogInfo("All clients acknowledged pack application.");
                    });
                }
                else
                {
                    PEAKLevelLoader.Logger.LogWarning("PEAKPackSyncPhoton.Instance is null. Ensure it exists in scene and is created in Awake.");
                }
            }

            Logger.LogInfo($"ApplyPacksToMapHandler: finished applying packs, segments length {newArr.Length}");
            yield break;
        }

        private IEnumerator FindPrefabAcrossGroups(string prefabName, Action<GameObject?> onFound)
        {
            GameObject? found = null;

            var groupsObj = assetBundleLoader?.AssetBundleGroups ?? AssetBundleLoader.Instance?.AssetBundleGroups;
            if (groupsObj == null) { onFound(null); yield break; }

            foreach (var g in groupsObj)
            {
                if (g == null) continue;
                var group = g as AssetBundleGroup;
                if (group == null) continue;

                yield return StartCoroutine(EnsureGroupLoadedThenResolve(group, prefabName, prefab => found = prefab));
                if (found != null) break;
            }

            onFound(found);
            yield break;
        }

        internal static void TrySoftPatch(string pluginName, Type type)
        {
            if (Chainloader.PluginInfos.ContainsKey(pluginName))
            {
                Harmony.CreateClassProcessor(type, true).Patch();
                Logger.LogInfo($"{pluginName} found, enabling compatability patches.");
            }
        }
    }
}