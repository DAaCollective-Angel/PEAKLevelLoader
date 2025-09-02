using UnityEngine;
using static UnityEngine.RemoteConfigSettingsHelper;

namespace PEAKLevelLoader.Core
{
    [CreateAssetMenu(fileName = "ContentTag", menuName = "PEAK/ContentTag", order = 20)]
    public class ContentTag : ScriptableObject
    {
        public string? contentTagName;
        public Color contentTagColor = Color.white;

        public static ContentTag Create(string name)
        {
            var ct = CreateInstance<ContentTag>();
            ct.contentTagName = name;
            ct.contentTagColor = Color.white;
            ct.name = (name ?? "UnnamedTag") + "_ContentTag";
            return ct;
        }

        public static ContentTag Create(string name, Color color)
        {
            var ct = CreateInstance<ContentTag>();
            ct.contentTagName = name;
            ct.contentTagColor = color;
            ct.name = (name ?? "UnnamedTag") + "_ContentTag";
            return ct;
        }
    }

    public class ModContentTagMarker : MonoBehaviour
    {
        public ContentTag[] tags = System.Array.Empty<ContentTag>();
    }
}
