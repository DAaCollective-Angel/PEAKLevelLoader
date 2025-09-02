using System.Collections.Generic;
using UnityEngine;

namespace PEAKLevelLoader.Core
{
    public class AssetBundleGroup
    {
        public string GroupName { get; private set; } = string.Empty;
        private List<AssetBundleInfo> assetBundleInfos = new();

        public AssetBundleGroupLoadedStatus LoadedStatus
        {
            get
            {
                int loaded = 0, unloaded = 0;
                foreach (var info in assetBundleInfos)
                {
                    if (!info.IsAssetBundleLoaded) { unloaded++; if (loaded > 0) return AssetBundleGroupLoadedStatus.Partial; }
                    else { loaded++; if (unloaded > 0) return AssetBundleGroupLoadedStatus.Partial; }
                }
                if (loaded == assetBundleInfos.Count) return AssetBundleGroupLoadedStatus.Loaded;
                return AssetBundleGroupLoadedStatus.Unloaded;
            }
        }

        public AssetBundleGroupLoadingStatus LoadingStatus
        {
            get
            {
                AssetBundleLoadingStatus s = AssetBundleLoadingStatus.None;
                foreach (var info in assetBundleInfos)
                {
                    if (info.ActiveLoadingStatus == AssetBundleLoadingStatus.Loading) { if (s == AssetBundleLoadingStatus.Unloading) return AssetBundleGroupLoadingStatus.Mixed; s = AssetBundleLoadingStatus.Loading; }
                    else if (info.ActiveLoadingStatus == AssetBundleLoadingStatus.Unloading) { if (s == AssetBundleLoadingStatus.Loading) return AssetBundleGroupLoadingStatus.Mixed; s = AssetBundleLoadingStatus.Unloading; }
                }
                if (s == AssetBundleLoadingStatus.Loading) return AssetBundleGroupLoadingStatus.Loading;
                if (s == AssetBundleLoadingStatus.Unloading) return AssetBundleGroupLoadingStatus.Unloading;
                return AssetBundleGroupLoadingStatus.None;
            }
        }

        public float ActiveProgress
        {
            get
            {
                float combined = 0f;
                float total = assetBundleInfos.Count > 0 ? assetBundleInfos.Count : 1f;
                foreach (var info in assetBundleInfos) combined += info.ActiveProgress;
                return Mathf.InverseLerp(0f, total, combined);
            }
        }

        public ExtendedEvent OnGroupLoaded = new ExtendedEvent();
        public ExtendedEvent OnGroupUnloaded = new ExtendedEvent();
        public ExtendedEvent OnGroupLoadStatusChanged = new ExtendedEvent();

        public AssetBundleGroup(AssetBundleInfo info) => Initialize(info);
        public AssetBundleGroup(params AssetBundleInfo[] infos) => Initialize(infos);
        public AssetBundleGroup(List<AssetBundleInfo> infos) => Initialize(infos.ToArray());

        private void Initialize(params AssetBundleInfo[] infos)
        {
            foreach (var info in infos) if (info != null) assetBundleInfos.Add(info);
            foreach (var info in assetBundleInfos)
            {
                info.OnBundleLoaded.AddListener(OnAssetBundleInfoLoadChanged);
                info.OnBundeUnloaded.AddListener(OnAssetBundleInfoLoadChanged);
            }
            GroupName = AssetBundleUtilities.GetDisplayName(assetBundleInfos);
        }

        private void OnAssetBundleInfoLoadChanged(AssetBundleInfo info)
        {
            if (LoadedStatus == AssetBundleGroupLoadedStatus.Loaded) OnGroupLoaded.Invoke();
            else if (LoadedStatus == AssetBundleGroupLoadedStatus.Unloaded) OnGroupUnloaded.Invoke();
            else OnGroupUnloaded?.Invoke();
            OnGroupLoadStatusChanged.Invoke();
        }

        internal List<AssetBundleInfo> GetAssetBundleInfos() => new(assetBundleInfos);

        public List<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            var outList = new List<T>();
            foreach (var info in assetBundleInfos)
                if (info.AssetBundleMode == AssetBundleType.Standard)
                    outList.AddRange(info.LoadAllAssets<T>());
            return outList;
        }

        public void TryLoadGroup() { foreach (var info in assetBundleInfos) if (!info.IsAssetBundleLoaded) info.TryLoadBundle(); }
        public void TryUnloadGroup() { foreach (var info in assetBundleInfos) if (info.IsAssetBundleLoaded) info.TryUnloadBundle(); }

        public bool ContainsAssetBundleFile(string fullFilePath)
        {
            foreach (var info in assetBundleInfos) if (info.AssetBundleFilePath.Equals(fullFilePath)) return true;
            return false;
        }

        public bool Contains(UnityEngine.Object unityObject)
        {
            foreach (var info in assetBundleInfos) if (info.Contains(unityObject)) return true;
            return false;
        }

        public bool Contains(string sceneNameOrPath)
        {
            foreach (var info in assetBundleInfos) if (info.Contains(sceneNameOrPath)) return true;
            return false;
        }

        public List<string> GetSceneNames()
        {
            var list = new List<string>();
            foreach (var info in assetBundleInfos) list.AddRange(info.GetSceneNames());
            return list;
        }
    }
}
