using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PEAKLevelLoader.Core
{
    public class AssetBundleLoader : MonoBehaviour
    {
        private static AssetBundleLoader? instance;
        public static AssetBundleLoader Instance
        {
            get
            {
                if (instance == null) instance = Object.FindObjectOfType<AssetBundleLoader>();
                return instance!;
            }
        }

        internal static DirectoryInfo pluginsFolder = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent;

        internal List<AssetBundleInfo> AssetBundleInfos = new();
        internal List<AssetBundleGroup> AssetBundleGroups { get; private set; } = new();

        public static ExtendedEvent OnBeforeProcessBundles = new ExtendedEvent();
        public static ExtendedEvent OnBundlesFinishedProcessing = new ExtendedEvent();
        public static ExtendedEvent<AssetBundleInfo> OnBundleLoaded = new ExtendedEvent<AssetBundleInfo>();
        public static ExtendedEvent<AssetBundleInfo> OnBundleUnloaded = new ExtendedEvent<AssetBundleInfo>();

        private static Dictionary<(string, string), List<ParameterEvent<AssetBundleGroup>>> processedCallbacksDict = new();
        private static int processedBundleCount;
        private static int requestedBundleCount;

        private void OnEnable()
        {
            instance = this;
            OnBundlesFinishedProcessing.AddListener(() => { });
        }

        internal static bool AllowLoading { get; set; } = true;

        public static bool LoadAllBundlesRequest(DirectoryInfo? directory = null, string? specifiedFileName = null, string? specifiedFileExtension = null, ParameterEvent<AssetBundleGroup>? onProcessedCallback = null, List<string>? foldersToScan = null)
        {
            if (!AllowLoading) { Debug.LogError("AssetBundleLoader: loading disabled."); return false; }
            if (directory == null) directory = pluginsFolder;
            if (specifiedFileExtension == null) specifiedFileExtension = ".*";
            if (specifiedFileName == null) specifiedFileName = "*";

            var targetFolders = new List<DirectoryInfo>();
            if (foldersToScan != null && foldersToScan.Count > 0)
            {
                foreach (var f in foldersToScan)
                {
                    try { var di = new DirectoryInfo(f); if (!di.Exists) di.Create(); targetFolders.Add(di); } catch { }
                }
            }
            targetFolders.Add(directory);

            int found = 0;
            foreach (var dir in targetFolders)
                foreach (var f in Directory.GetFiles(dir.FullName, specifiedFileName + specifiedFileExtension, SearchOption.AllDirectories))
                    found++;

            if (found == 0) { Debug.Log($"AssetBundleLoader: no files matching {specifiedFileName}{specifiedFileExtension}"); return false; }

            LoadAllBundles(targetFolders, specifiedFileName, specifiedFileExtension, onProcessedCallback);
            return true;
        }

        private static void LoadAllBundles(List<DirectoryInfo> directories, string specifiedFileName, string specifiedFileExtension, ParameterEvent<AssetBundleGroup>? onProcessedCallback)
        {
            AllowLoading = false;
            processedBundleCount = 0;
            requestedBundleCount = 0;

            var cbName = (specifiedFileName == "*" ? string.Empty : specifiedFileName);
            var cbExt = (specifiedFileExtension == ".*" ? string.Empty : specifiedFileExtension);
            var callBackKey = (directories.First().FullName, (cbName + cbExt).ToLowerInvariant());

            if (!processedCallbacksDict.ContainsKey(callBackKey)) processedCallbacksDict.Add(callBackKey, new List<ParameterEvent<AssetBundleGroup>>());
            if (onProcessedCallback != null) processedCallbacksDict[callBackKey].Add(onProcessedCallback);

            var uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in directories)
            {
                try
                {
                    foreach (var file in Directory.GetFiles(dir.FullName, specifiedFileName + specifiedFileExtension, SearchOption.AllDirectories))
                    {
                        string full = Path.GetFullPath(file);
                        if (!uniqueFiles.Contains(full))
                        {
                            uniqueFiles.Add(full);
                            requestedBundleCount++;
                            var info = new AssetBundleInfo(Instance, full);
                            info.OnBundleLoaded.AddListener(OnAssetBundleLoadChanged);
                            info.OnBundeUnloaded.AddListener(OnAssetBundleLoadChanged);
                            Instance.AssetBundleInfos.Add(info);
                        }
                        else
                        {
                            Debug.Log($"AssetBundleLoader: skipped duplicate file path {full}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"AssetBundleLoader: error enumerating files in {dir.FullName}: {ex}");
                }
            }

            if (requestedBundleCount > 0)
            {
                OnBeforeProcessBundles.Invoke();
                OnBundleLoaded.AddListener(ProcessInitialBundleLoading);
                foreach (var info in Instance!.AssetBundleInfos) info.TryLoadBundle();
            }
            else
            {
                Debug.Log("AssetBundleLoader: no bundles found.");
                AllowLoading = true;
                OnBundlesFinishedProcessing.Invoke();
            }
        }

        private static void OnAssetBundleLoadChanged(AssetBundleInfo info) { if (info.IsAssetBundleLoaded) OnBundleLoaded.Invoke(info); else OnBundleUnloaded.Invoke(info); }

        private static void ProcessInitialBundleLoading(AssetBundleInfo info)
        {
            processedBundleCount++;
            if (processedBundleCount == Instance!.AssetBundleInfos.Count)
            {
                processedBundleCount = 0;
                OnBundleLoaded.RemoveListener(ProcessInitialBundleLoading);
                OnInitialBundlesProcessed();
            }
        }

        private static void OnInitialBundlesProcessed()
        {
            Instance!.AssetBundleGroups.Clear();
            var sceneNameDict = new Dictionary<string, List<AssetBundleInfo>>();
            var nonSceneBundles = new List<List<AssetBundleInfo>>();

            foreach (var info in Instance.AssetBundleInfos)
            {
                var names = info.GetSceneNames();
                if (names.Count > 0)
                {
                    foreach (var s in names) if (!sceneNameDict.ContainsKey(s)) sceneNameDict[s] = new List<AssetBundleInfo>();
                }
                else nonSceneBundles.Add(new List<AssetBundleInfo> { info });
            }

            var uniqueSceneGroups = new List<UniqueSceneGroup>();
            foreach (var kv in sceneNameDict) uniqueSceneGroups.Add(new UniqueSceneGroup(kv.Key));

            foreach (var info in Instance.AssetBundleInfos)
            {
                var scenes = info.GetSceneNames();
                foreach (var g in uniqueSceneGroups) if (g.TryAdd(info, scenes)) break;
            }

            foreach (var groupedInfos in uniqueSceneGroups.Select(g => g.AssetBundleInfosInGroup).Concat(nonSceneBundles))
            {
                if (groupedInfos.Count == 0) continue;
                var group = new AssetBundleGroup(groupedInfos);
                Instance.AssetBundleGroups.Add(group);

                foreach (var kvp in processedCallbacksDict)
                {
                    foreach (var info in group.GetAssetBundleInfos())
                    {
                        if (info.AssetBundleFilePath.Contains(kvp.Key.Item1) && info.AssetBundleFileName.Contains(kvp.Key.Item2))
                        {
                            foreach (var ev in kvp.Value) ev.Invoke(group);
                            break;
                        }
                    }
                }
            }

            AllowLoading = true;
            OnBundlesFinishedProcessing.Invoke();
            foreach (var info in Instance.AssetBundleInfos) info.TryUnloadBundle();
        }
    }

    internal class UniqueSceneGroup
    {
        private List<string> scenesInGroup = new();
        internal List<AssetBundleInfo> AssetBundleInfosInGroup { get; private set; } = new();
        internal string UniqueSceneName { get; private set; } = string.Empty;

        internal UniqueSceneGroup(string newUniqueSceneName) { UniqueSceneName = newUniqueSceneName; scenesInGroup.Add(UniqueSceneName); }

        internal bool TryAdd(AssetBundleInfo info, List<string> infoScenes)
        {
            if (AssetBundleInfosInGroup.Contains(info)) return true;
            if (infoScenes.Contains(UniqueSceneName)) { Add(info, infoScenes); return true; }
            foreach (var s in infoScenes) if (scenesInGroup.Contains(s)) { Add(info, infoScenes); return true; }
            return false;
        }

        private void Add(AssetBundleInfo info, List<string> infoScenes)
        {
            if (!AssetBundleInfosInGroup.Contains(info)) AssetBundleInfosInGroup.Add(info);
            foreach (var s in infoScenes) if (!scenesInGroup.Contains(s)) scenesInGroup.Add(s);
        }
    }
}
