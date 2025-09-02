using UnityEngine;
public class Placeholder : MonoBehaviour
{
    [Tooltip("Key name used by SpawnableRegistry or the prefab name to look for in bundles.")]
    public string? spawnableName;
    [Tooltip("Optional role (e.g. 'campfire', 'spawner', 'fogOrigin', etc.).")]
    public string? role;
    [Tooltip("Optional JSON/options string used by loader (e.g. for fog size).")]
    [TextArea(2, 6)]
    public string? options;
    [Tooltip("Optional author notes.")]
    public string? notes;
}
