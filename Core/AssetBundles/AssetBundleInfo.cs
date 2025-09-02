using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PEAKLevelLoader.Core
{
    public class AssetBundleInfo
    {
        public string AssetBundleFilePath { get; private set; } = string.Empty;
        public string AssetBundleFileName { get; private set; } = string.Empty;
        public string AssetBundleName { get; private set; } = string.Empty;

        public AssetBundleType AssetBundleMode { get; private set; } = AssetBundleType.Standard;
        public bool IsAssetBundleLoaded { get; private set; } = false;
        public bool IsHotReloadable { get; set; } = false;

        public AssetBundleLoadingStatus ActiveLoadingStatus { get; private set; } = AssetBundleLoadingStatus.None;
        public float ActiveProgress { get; private set; } = 0f;

        public AssetBundle? AssetBundleReference { get; private set; } = null;

        public ExtendedEvent<AssetBundleInfo> OnBundleLoaded = new ExtendedEvent<AssetBundleInfo>();
        public ExtendedEvent<AssetBundleInfo> OnBundeUnloaded = new ExtendedEvent<AssetBundleInfo>();

        private readonly AssetBundleLoader owner;
        private AssetBundleCreateRequest? createRequest = null;

        public AssetBundleInfo(AssetBundleLoader ownerInstance, string fullFilePath)
        {
            if (ownerInstance == null) throw new ArgumentNullException(nameof(ownerInstance));
            if (string.IsNullOrEmpty(fullFilePath)) throw new ArgumentNullException(nameof(fullFilePath));

            owner = ownerInstance;
            AssetBundleFilePath = Path.GetFullPath(fullFilePath);
            AssetBundleFileName = Path.GetFileName(fullFilePath);
            AssetBundleName = Path.GetFileNameWithoutExtension(fullFilePath) ?? string.Empty;
        }

        public void TryLoadBundle()
        {
            if (IsAssetBundleLoaded || ActiveLoadingStatus == AssetBundleLoadingStatus.Loading) return;
            ActiveLoadingStatus = AssetBundleLoadingStatus.Loading;
            ActiveProgress = 0f;
            owner.StartCoroutine(LoadBundleCoroutine());
        }

        public void TryUnloadBundle()
        {
            if (!IsAssetBundleLoaded || ActiveLoadingStatus == AssetBundleLoadingStatus.Unloading) return;
            ActiveLoadingStatus = AssetBundleLoadingStatus.Unloading;
            ActiveProgress = 0f;
            owner.StartCoroutine(UnloadBundleCoroutine());
        }

        private IEnumerator LoadBundleCoroutine()
        {
            try
            {
                foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (loaded == null) continue;
                    if (string.Equals(loaded.name, AssetBundleName, StringComparison.OrdinalIgnoreCase))
                    {
                        AssetBundleReference = loaded;
                        IsAssetBundleLoaded = true;
                        ActiveLoadingStatus = AssetBundleLoadingStatus.None;
                        ActiveProgress = 100f;
                        OnBundleLoaded.Invoke(this);
                        yield break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AssetBundleInfo: failed checking already-loaded bundles: {ex}");
            }

            if (!File.Exists(AssetBundleFilePath))
            {
                Debug.LogWarning($"AssetBundleInfo: file not found -> {AssetBundleFilePath}");
                ActiveLoadingStatus = AssetBundleLoadingStatus.None;
                ActiveProgress = 0f;
                yield break;
            }

            createRequest = AssetBundle.LoadFromFileAsync(AssetBundleFilePath);
            if (createRequest == null)
            {
                Debug.LogWarning($"AssetBundleInfo: LoadFromFileAsync returned null for {AssetBundleFilePath}");
                ActiveLoadingStatus = AssetBundleLoadingStatus.None;
                ActiveProgress = 0f;
                yield break;
            }

            while (!createRequest.isDone)
            {
                ActiveProgress = createRequest.progress * 100f;
                yield return null;
            }

            AssetBundleReference = createRequest.assetBundle;
            createRequest = null;

            if (AssetBundleReference == null)
            {
                Debug.LogError($"AssetBundleInfo: Failed to load AssetBundle at {AssetBundleFilePath}");
                IsAssetBundleLoaded = false;
                ActiveLoadingStatus = AssetBundleLoadingStatus.None;
                ActiveProgress = 0f;
                yield break;
            }

            try { AssetBundleMode = AssetBundleReference.isStreamedSceneAssetBundle ? AssetBundleType.Streaming : AssetBundleType.Standard; } catch { AssetBundleMode = AssetBundleType.Standard; }

            IsAssetBundleLoaded = true;
            ActiveLoadingStatus = AssetBundleLoadingStatus.None;
            ActiveProgress = 100f;

            try { OnBundleLoaded.Invoke(this); } catch (Exception ex) { Debug.LogError(ex); }
        }

        private IEnumerator UnloadBundleCoroutine()
        {
            if (AssetBundleReference == null)
            {
                IsAssetBundleLoaded = false;
                ActiveLoadingStatus = AssetBundleLoadingStatus.None;
                ActiveProgress = 0f;
                yield break;
            }

            AssetBundleReference.Unload(true);
            AssetBundleReference = null;
            IsAssetBundleLoaded = false;

            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.15f)
            {
                ActiveProgress = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.15f) * 100f;
                yield return null;
            }

            ActiveProgress = 0f;
            ActiveLoadingStatus = AssetBundleLoadingStatus.None;

            try { OnBundeUnloaded.Invoke(this); } catch (Exception ex) { Debug.LogError(ex); }
        }

        public System.Collections.Generic.List<string> GetSceneNames()
        {
            if (!IsAssetBundleLoaded || AssetBundleReference == null) return new System.Collections.Generic.List<string>();
            try { return AssetBundleUtilities.GetSceneNamesFromLoadedAssetBundle(AssetBundleReference); }
            catch (Exception ex) { Debug.LogWarning($"GetSceneNames failed for {AssetBundleName}: {ex}"); return new System.Collections.Generic.List<string>(); }
        }

        public System.Collections.Generic.List<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            var list = new System.Collections.Generic.List<T>();
            if (!IsAssetBundleLoaded || AssetBundleReference == null) return list;
            try { list.AddRange(AssetBundleReference.LoadAllAssets<T>()); } catch (Exception ex) { Debug.LogWarning(ex); }
            return list;
        }

        public bool Contains(string sceneNameOrPath)
        {
            var scenes = GetSceneNames();
            if (scenes == null || scenes.Count == 0) return false;
            return scenes.Contains(sceneNameOrPath) || scenes.Contains(sceneNameOrPath.Replace(".unity", ""));
        }

        public bool Contains(UnityEngine.Object unityObject)
        {
            if (!IsAssetBundleLoaded || AssetBundleReference == null || unityObject == null) return false;
            try
            {
                var all = AssetBundleReference.LoadAllAssets();
                foreach (var a in all)
                    if (a != null && a.name == unityObject.name) return true;
            }
            catch { }
            return false;
        }
    }
}
