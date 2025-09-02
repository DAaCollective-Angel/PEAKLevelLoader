using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using PEAKLevelLoader.Core;
using BepInEx;
using System.Linq;
using BepInEx.Bootstrap;
using System.Reflection;
using Mono.Cecil;
using static PEAKLevelLoader.Core.PatchedContent;

namespace PEAKLevelLoader
{
    public static class PEAKBundleManager
    {
        public enum ModProcessingStatus { Inactive, Loading, Complete }
        public static ModProcessingStatus CurrentStatus { get; internal set; } = ModProcessingStatus.Inactive;
        public static event Action? OnFinishedProcessing;
        public static bool HasFinalisedFoundContent { get; internal set; }

        internal static void Start()
        {
            PEAKLevelLoader.Logger.LogInfo("PEAKBundleManager: Starting!");
            TryLoadPEAKBundles();
        }

        private static bool TryLoadPEAKBundles()
        {
            PEAKLevelLoader.Logger.LogInfo("PEAKBundleManager: Now scanning entire BepInEx plugins directory for .pll files...");

            var folders = new List<string>();
            try
            {
                string pluginsRoot = Paths.PluginPath;
                if (Directory.Exists(pluginsRoot))
                {
                    folders.Add(pluginsRoot);
                }
                else
                {
                    PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: Plugins root not found: {pluginsRoot}");
                }

                string tempExtractionRoot = Path.Combine(Path.GetTempPath(), "PEAKLevelLoader_embedded");
                if (!Directory.Exists(tempExtractionRoot)) Directory.CreateDirectory(tempExtractionRoot);

                foreach (var kv in Chainloader.PluginInfos)
                {
                    try
                    {
                        var pluginInfo = kv.Value;
                        if (pluginInfo == null) continue;

                        Assembly? asm = null;
                        if (pluginInfo.Instance != null)
                        {
                            asm = pluginInfo.Instance.GetType().Assembly;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(pluginInfo.Location))
                            {
                                try
                                {
                                    var pluginFolder = Path.GetDirectoryName(pluginInfo.Location);
                                    if (!string.IsNullOrEmpty(pluginFolder) && Directory.Exists(pluginFolder))
                                    {
                                        foreach (var file in Directory.EnumerateFiles(pluginFolder, "*.pll", SearchOption.TopDirectoryOnly))
                                        {
                                            if (!folders.Contains(pluginFolder)) folders.Add(pluginFolder);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: failed scanning plugin folder for on-disk bundles: {ex}");
                                }
                            }

                            if (!PEAKLevelLoader.AllowEmbeddedBundles)
                            {
                                PEAKLevelLoader.Logger.LogDebug($"PEAKBundleManager: plugin {pluginInfo.Metadata?.GUID ?? pluginInfo.Location} not loaded into domain; skipping manifest resource extraction.");
                                continue;
                            }

                            try
                            {
                                var cecilAsmType = Type.GetType("Mono.Cecil.AssemblyDefinition, Mono.Cecil");
                                if (cecilAsmType == null)
                                {
                                    PEAKLevelLoader.Logger.LogInfo("PEAKBundleManager: Mono.Cecil not available. Skipping embedded resource extraction for unloaded plugin.");
                                }
                                else if (!string.IsNullOrEmpty(pluginInfo.Location) && File.Exists(pluginInfo.Location))
                                {
                                    try
                                    {
                                        var asmDef = Mono.Cecil.AssemblyDefinition.ReadAssembly(pluginInfo.Location);
                                        foreach (var res in asmDef.MainModule.Resources)
                                        {
                                            if (res is Mono.Cecil.EmbeddedResource er && er.Name.EndsWith(".pll", StringComparison.OrdinalIgnoreCase))
                                            {
                                                try
                                                {
                                                    using (var erStream = er.GetResourceStream())
                                                    {
                                                        if (erStream == null) continue;
                                                        string pluginSafeName = (pluginInfo.Metadata?.GUID ?? pluginInfo.Location?.GetHashCode().ToString() ?? "plugin").ReplaceInvalidFileNameChars();
                                                        string outDir = Path.Combine(tempExtractionRoot, pluginSafeName);
                                                        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                                                        string outFile = Path.Combine(outDir, Path.GetFileName(er.Name));
                                                        using (var fs = File.Create(outFile))
                                                        {
                                                            erStream.CopyTo(fs);
                                                        }
                                                        if (!folders.Contains(outDir)) folders.Add(outDir);
                                                    }
                                                }
                                                catch (Exception exRes)
                                                {
                                                    PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: Failed to extract embedded resource '{res.Name}' from plugin '{pluginInfo.Location}': {exRes}");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception exCecil)
                                    {
                                        PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: Mono.Cecil extraction failed for {pluginInfo.Location}: {exCecil}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: error checking for Mono.Cecil: {ex}");
                            }
                        }
                        if (asm == null) continue;

                        foreach (var resName in asm.GetManifestResourceNames())
                        {
                            if (resName.EndsWith(".pll", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    using (var stream = asm.GetManifestResourceStream(resName))
                                    {
                                        if (stream == null) continue;
                                        string pluginSafeName = (pluginInfo.Metadata?.GUID ?? pluginInfo.Location?.GetHashCode().ToString() ?? "plugin").ReplaceInvalidFileNameChars();
                                        string outDir = Path.Combine(tempExtractionRoot, pluginSafeName);
                                        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                                        string outFile = Path.Combine(outDir, Path.GetFileName(resName));
                                        using (var fs = File.Create(outFile))
                                        {
                                            stream.CopyTo(fs);
                                        }
                                        if (!folders.Contains(outDir)) folders.Add(outDir);
                                    }
                                }
                                catch (Exception exRes)
                                {
                                    PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: Failed to extract embedded resource '{resName}' from plugin '{pluginInfo.Location}': {exRes}");
                                }
                            }
                        }
                    }
                    catch (Exception exPlugin)
                    {
                        PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: Failed inspecting plugin for embedded bundles: {exPlugin}");
                    }
                }
            }
            catch (Exception ex)
            {
                PEAKLevelLoader.Logger.LogError($"PEAKBundleManager: Exception gathering folders to scan: {ex}");
            }

            if (folders.Count == 0)
            {
                PEAKLevelLoader.Logger.LogInfo("PEAKBundleManager: No plugin folders found to scan for .pll files.");
                return false;
            }

            var onGroupEvent = new ParameterEvent<Core.AssetBundleGroup>();
            onGroupEvent.AddListener(OnAssetBundleGroupCreated);

            bool ok = Core.AssetBundleLoader.LoadAllBundlesRequest(
                directory: null,
                specifiedFileName: "*",
                specifiedFileExtension: ".pll",
                onProcessedCallback: onGroupEvent,
                foldersToScan: folders
            );

            if (ok)
            {
                PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: Initiated load request — scanning {folders.Count} folder(s).");
                Core.AssetBundleLoader.OnBundlesFinishedProcessing.AddListener(OnAssetBundleLoadRequestFinished);
            }
            else
            {
                PEAKLevelLoader.Logger.LogWarning("PEAKBundleManager: LoadAllBundlesRequest returned false (no bundles found).");
            }

            return ok;
        }

        private static void OnAssetBundleGroupCreated(AssetBundleGroup group)
        {
            try
            {
                var sceneNames = group.GetSceneNames();
                foreach (var s in sceneNames)
                    if (!PatchedContent.AllLevelSceneNames.Contains(s))
                        PatchedContent.AllLevelSceneNames.Add(s);

                var textAssets = group.LoadAllAssets<UnityEngine.TextAsset>();
                UnityEngine.TextAsset? metadataAsset = null;
                foreach (var ta in textAssets)
                {
                    if (ta == null) continue;
                    if (ta.name.Equals("mod.json", StringComparison.OrdinalIgnoreCase))
                    {
                        metadataAsset = ta;
                        break;
                    }
                }
                try
                {
                    List<ExtendedMod> foundMods = new List<ExtendedMod>();
                    try
                    {
                        var extMods = group.LoadAllAssets<ExtendedMod>();
                        if (extMods != null && extMods.Count > 0)
                        {
                            foreach (var em in extMods) if (em != null) foundMods.Add(em);
                        }
                    }
                    catch {  }
                    List<ExtendedSegment> foundSegments = new List<ExtendedSegment>();
                    try
                    {
                        var extSegs = group.LoadAllAssets<ExtendedSegment>();
                        if (extSegs != null && extSegs.Count > 0)
                        {
                            foreach (var es in extSegs) if (es != null) foundSegments.Add(es);
                        }
                    }
                    catch { }

                    if (foundMods.Count > 0 || foundSegments.Count > 0)
                    {
                        foreach (var extMod in foundMods)
                        {
                            var modKey = extMod.ModName ?? group.GroupName ?? "mod";
                            if (!PatchedContent.ModDefinedTags.ContainsKey(modKey))
                                PatchedContent.ModDefinedTags[modKey] = new List<ContentTag>();

                            foreach (var extContent in extMod.ExtendedContents)
                            {
                                if (extContent is ExtendedSegment es)
                                {
                                    var lp = new LevelPack
                                    {
                                        index = es.index,
                                        replace = es.replace,
                                        isVariant = es.isVariant,
                                        biome = es.biome ?? string.Empty,
                                        bundlePath = group.GroupName!,
                                        prefabName = es.prefabName ?? string.Empty,
                                        campfirePrefabName = es.campfirePrefabName ?? string.Empty,
                                        packName = es.packName ?? es.name ?? Guid.NewGuid().ToString(),
                                        id = es.id ?? Guid.NewGuid().ToString(),
                                        spawnMappings = es.ResolvedSpawnables?.Select(kv => new SpawnMapping { spawnerMarker = kv.Key, spawnableName = kv.Value?.name ?? "" }).ToArray() ?? Array.Empty<SpawnMapping>()
                                    };
                                    if (PEAKLevelLoader.Instance != null)
                                    {
                                        PEAKLevelLoader.Instance.AddPacks(new LevelPackCollection { packs = new[] { lp } });
                                        PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: registered ExtendedSegment {lp.packName} from group {group.GroupName}");
                                    }
                                }
                            }
                            PatchedContent.ExtendedMods.Add(extMod);
                            ContentTagManager.MergeExtendedModTags(extMod);
                        }
                        foreach (var es in foundSegments)
                        {
                            var lp = new LevelPack
                            {
                                index = es.index,
                                replace = es.replace,
                                isVariant = es.isVariant,
                                biome = es.biome ?? string.Empty,
                                bundlePath = group.GroupName!,
                                prefabName = es.prefabName ?? string.Empty,
                                campfirePrefabName = es.campfirePrefabName ?? string.Empty,
                                packName = es.packName ?? es.name ?? Guid.NewGuid().ToString(),
                                id = es.id ?? Guid.NewGuid().ToString()
                            };
                            if (PEAKLevelLoader.Instance != null) PEAKLevelLoader.Instance.AddPacks(new LevelPackCollection { packs = new[] { lp } });
                            PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: registered ExtendedSegment {lp.packName} from group {group.GroupName}");
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: extended asset manifest load attempt failed for {group.GroupName}: {ex}");
                }

                if (metadataAsset == null)
                {
                    foreach (var ta in textAssets)
                    {
                        if (ta == null || string.IsNullOrWhiteSpace(ta.text)) continue;
                        var txt = ta.text?.Trim();
                        if (!string.IsNullOrEmpty(txt) && txt.StartsWith("{") && txt.Contains("modName"))
                        {
                            metadataAsset = ta;
                            break;
                        }
                    }
                }

                if (metadataAsset != null)
                {
                    try
                    {
                        var raw = StringExtensions.SanitizeJson(metadataAsset.text!);

                        var modJson = JsonUtility.FromJson<ModJson>(raw) ?? new ModJson();
                        var segs = modJson.segments ?? Array.Empty<ModJsonSegment>();

                        if ((segs.Length == 0) && raw.IndexOf("\"segments\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: raw JSON contains 'segments' substring? True - attempting manual extraction for group {group.GroupName}.");
                            string arr = StringExtensions.ExtractTopLevelArray(raw, "segments");
                            if (!string.IsNullOrEmpty(arr))
                            {
                                var objs = StringExtensions.SplitTopLevelObjects(arr);
                                var manual = new List<ModJsonSegment>();
                                for (int oi = 0; oi < objs.Count; oi++)
                                {
                                    try
                                    {
                                        var s = JsonUtility.FromJson<ModJsonSegment>(objs[oi]);
                                        if (s != null) manual.Add(s);
                                    }
                                    catch (Exception ex)
                                    {
                                        PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: manual parse failed for object {oi} in group {group.GroupName}: {ex}");
                                    }
                                }
                                if (manual.Count > 0)
                                {
                                    segs = manual.ToArray();
                                    modJson.segments = segs;
                                    PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: manual fallback parsed segments length = {segs.Length} for group {group.GroupName}.");
                                }
                                else
                                {
                                    PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: manual fallback found array but parsed 0 segment objects for group {group.GroupName}.");
                                }
                            }
                            else
                            {
                                PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: ExtractTopLevelArray didn't find a 'segments' array for group {group.GroupName}.");
                            }
                        }

                        if (modJson.segments != null && modJson.segments.Length > 0 && !string.IsNullOrEmpty(modJson.modName))
                        {
                            if (string.IsNullOrWhiteSpace(modJson.version))
                                PEAKLevelLoader.Logger.LogError($"PEAKBundleManager: The compatiblity version is not defined, please define the version entry...\nAdditionally, the version check will be added later.");

                            PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: Parsed mod.json from {group.GroupName}: {modJson.modName}");
                            var extendedMod = ExtendedMod.CreateNewMod(modJson.modName, modJson.author ?? "Unknown", modJson.version);

                            var createdTags = new Dictionary<string, ContentTag>(StringComparer.OrdinalIgnoreCase);
                            if (modJson.contentTags != null)
                            {
                                foreach (var t in modJson.contentTags)
                                {
                                    if (string.IsNullOrEmpty(t)) continue;
                                    if (!createdTags.ContainsKey(t))
                                        createdTags[t] = ContentTag.Create(t);
                                }
                            }
                            if (modJson.spawnables != null)
                            {
                                foreach (var s in modJson.spawnables)
                                {
                                    if (string.IsNullOrEmpty(s.name) || string.IsNullOrEmpty(s.prefabName)) continue;
                                    string registryGroup = !string.IsNullOrEmpty(s.bundlePath) ? s.bundlePath : group.GroupName ?? (modJson.modName ?? "unknown");
                                    SpawnableRegistry.Registry[s.name] = new SpawnableEntry(registryGroup, s.prefabName);
                                    PEAKLevelLoader.Logger.LogInfo($"Spawnable registered: '{s.name}' -> {registryGroup}::{s.prefabName}");
                                }
                            }

                            segs = modJson.segments ?? Array.Empty<ModJsonSegment>();
                            var levelPacks = new List<LevelPack>();
                            foreach (var seg in segs)
                            {
                                var lp = new LevelPack()
                                {
                                    index = seg.index,
                                    replace = seg.replace,
                                    isVariant = seg.isVariant,
                                    biome = seg.biome ?? string.Empty,
                                    bundlePath = group.GroupName!,
                                    prefabName = seg.segmentPrefab ?? string.Empty,
                                    campfirePrefabName = seg.campfirePrefab ?? string.Empty,
                                    packName = $"{modJson.modName}_{seg.id ?? seg.index.ToString()}",
                                    id = seg.id ?? Guid.NewGuid().ToString()
                                };
                                lp.spawnMappings = (seg.spawnMappings != null) ?
                                    seg.spawnMappings.Select(sm => new SpawnMapping { spawnerMarker = sm.spawnerMarker, spawnableName = sm.spawnableName }).ToArray()
                                    : Array.Empty<SpawnMapping>();
                                levelPacks.Add(lp);

                                var extSeg = ScriptableObject.CreateInstance<ExtendedSegment>();
                                extSeg.name = lp.packName;
                                extSeg.index = lp.index;
                                extSeg.replace = lp.replace;
                                extSeg.isVariant = lp.isVariant;
                                extSeg.biome = lp.biome;
                                extSeg.prefabName = lp.prefabName;
                                extSeg.campfirePrefabName = lp.campfirePrefabName;
                                extSeg.packName = lp.packName;
                                extSeg.id = lp.id;
                                extSeg.ContentType = ContentType.Custom;

                                foreach (var kv in createdTags) extSeg.ContentTags.Add(kv.Value);
                                extendedMod.TryRegisterExtendedContent(extSeg);
                            }
                            var packs = new LevelPackCollection { packs = levelPacks.ToArray() };
                            if (PEAKLevelLoader.Instance != null && levelPacks.Count > 0)
                            {
                                PEAKLevelLoader.Instance.AddPacks(packs);
                                PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: Added {levelPacks.Count} LevelPack(s) to PEAKLevelLoader.");
                            }
                            else
                            {
                                PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: no level packs to add for group {group.GroupName}.");
                            }

                            var modKey = extendedMod.ModName ?? group.GroupName;
                            if (!PatchedContent.ModDefinedTags.ContainsKey(modKey!))
                                PatchedContent.ModDefinedTags[modKey!] = new List<ContentTag>();
                            foreach (var ct in createdTags.Values)
                                if (!PatchedContent.ModDefinedTags[modKey!].Contains(ct))
                                    PatchedContent.ModDefinedTags[modKey!].Add(ct);
                            if (!PatchedContent.ModDefinedTags.ContainsKey(group.GroupName!))
                                PatchedContent.ModDefinedTags[group.GroupName!] = new List<ContentTag>(PatchedContent.ModDefinedTags[modKey!]);

                            PatchedContent.ExtendedMods.Add(extendedMod);
                            ContentTagManager.MergeExtendedModTags(extendedMod);
                            ContentTagManager.MergeAllExtendedModTags();
                            ContentTagManager.PopulateContentTagData();

                            PatchedContent.SortExtendedMods();

                            PEAKLevelLoader.Logger.LogInfo($"PEAKBundleManager: Registered runtime ExtendedMod for {modJson.modName} with {segs.Length} segments and {createdTags.Count} tags.");
                        }
                    }
                    catch (Exception ex)
                    {
                        PEAKLevelLoader.Logger.LogWarning($"PEAKBundleManager: failed to parse mod.json in {group.GroupName}: {ex}");
                    }
                }

                foreach (var info in group.GetAssetBundleInfos())
                {
                    var hash = AssetBundleUtilities.ComputeSHA256(info.AssetBundleFilePath);
                    if (!string.IsNullOrEmpty(hash) && !PatchedContent.LoadedBundleHashes.Contains(hash))
                    {
                        PatchedContent.LoadedBundleHashes.Add(hash);
                        PatchedContent.LoadedBundleNames.Add(info.AssetBundleName);
                    }
                }
            }
            catch (Exception ex)
            {
                PEAKLevelLoader.Logger.LogError($"PEAKBundleManager.OnAssetBundleGroupCreated exception: {ex}");
            }
        }

        private static void OnAssetBundleLoadRequestFinished()
        {
            AssetBundleLoader.OnBundlesFinishedProcessing.RemoveListener(OnAssetBundleLoadRequestFinished);
            PEAKLevelLoader.Logger.LogInfo("PEAKBundleManager: Finished processing bundles.");
            CurrentStatus = ModProcessingStatus.Complete;
            HasFinalisedFoundContent = true;
            OnFinishedProcessing?.Invoke();
        }
    }
}
