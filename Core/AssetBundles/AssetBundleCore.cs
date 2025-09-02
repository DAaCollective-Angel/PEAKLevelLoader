using System;
using System.Collections.Generic;

namespace PEAKLevelLoader.Core
{
    public enum AssetBundleType { Standard, Streaming }
    public enum AssetBundleLoadingStatus { None, Loading, Unloading }
    public enum AssetBundleGroupLoadedStatus { Unloaded, Partial, Loaded }
    public enum AssetBundleGroupLoadingStatus { None, Mixed, Loading, Unloading }

    public class ExtendedEvent
    {
        private readonly List<Action> listeners = new();
        public void AddListener(Action a) { if (a != null) listeners.Add(a); }
        public void RemoveListener(Action a) { listeners.Remove(a); }
        public void Invoke() { for (int i = 0; i < listeners.Count; i++) try { listeners[i]?.Invoke(); } catch { } }
    }

    public class ExtendedEvent<T>
    {
        private readonly List<Action<T>> listeners = new();
        public void AddListener(Action<T> a) { if (a != null) listeners.Add(a); }
        public void RemoveListener(Action<T> a) { listeners.Remove(a); }
        public void Invoke(T arg) { for (int i = 0; i < listeners.Count; i++) try { listeners[i]?.Invoke(arg); } catch { } }
    }

    public class ParameterEvent<T>
    {
        private readonly List<Action<T>> listeners = new();
        public void AddListener(Action<T> a) { if (a != null) listeners.Add(a); }
        public void RemoveListener(Action<T> a) { listeners.Remove(a); }
        public void Invoke(T arg) { for (int i = 0; i < listeners.Count; i++) try { listeners[i]?.Invoke(arg); } catch { } }
    }
}
