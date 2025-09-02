using System;
using System.Collections.Generic;
using UnityEngine;

namespace PEAKLevelLoader.Core
{
    public enum ContentType { Vanilla, Custom, Any }

    public abstract class ExtendedContent : ScriptableObject
    {
        public ExtendedMod? ExtendedMod { get; internal set; }
        public int GameID { get; private set; }

        public ContentType ContentType { get; internal set; } = ContentType.Vanilla;

        [field: SerializeField]
        public List<ContentTag> ContentTags { get; internal set; } = new List<ContentTag>();

        public string ModName => ExtendedMod?.ModName ?? string.Empty;
        public string AuthorName => ExtendedMod?.AuthorName ?? string.Empty;
        public string UniqueIdentificationName => $"{AuthorName.ToLowerInvariant()}.{ModName.ToLowerInvariant()}.{name.ToLowerInvariant()}";

        internal abstract void Register(ExtendedMod mod);

        internal virtual void Initialize() { }
        internal virtual void OnBeforeRegistration() { }
        protected virtual void OnGameIDChanged() { }

        internal void SetGameID(int newID)
        {
            GameID = newID;
            OnGameIDChanged();
        }

        public bool TryGetTag(string tag) => ContentTags.Exists(t => t.contentTagName == tag);

        public bool TryAddTag(string tag)
        {
            if (TryGetTag(tag)) return false;
            ContentTags.Add(ContentTag.Create(tag));
            return true;
        }
    }

    public abstract class ExtendedContent<T> : ExtendedContent where T : UnityEngine.Object
    {
        public T? ContentObject { get; protected set; }
        protected void SetContent(T content) => ContentObject = content;
    }
}
