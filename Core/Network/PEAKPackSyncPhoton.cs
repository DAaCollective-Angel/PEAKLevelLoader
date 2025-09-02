using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PEAKLevelLoader.Core;
using Zorro.Core;

public class PEAKPackSyncPhoton : MonoBehaviour, IOnEventCallback
{
    private const byte EVENT_SYNC_PACKS = 200;
    private const byte EVENT_PACKS_APPLIED_ACK = 201;

    private static PEAKPackSyncPhoton? _instance;
    public static PEAKPackSyncPhoton? Instance => _instance;

    private readonly HashSet<int> lastReceivedAcks = new HashSet<int>();
    private int expectedAckCount = 0;
    private string lastMasterSendId = Guid.Empty.ToString();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    [Serializable]
    private class LevelPackDTO
    {
        public int index;
        public bool replace;
        public bool isVariant;
        public string? biome;
        public string? bundlePath;
        public string? prefabName;
        public string? campfirePrefabName;
        public string? packName;
        public string? id;
    }

    [Serializable]
    private class LevelPackCollectionDTO
    {
        public string? sendId;
        public LevelPackDTO[]? packs;
    }

    public void SendPacksToClients(LevelPack[] packs, bool waitForAcks = true, Action<bool> onComplete = null!)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("PEAKPackSyncPhoton: not in a Photon room; skipping send.");
            onComplete?.Invoke(false);
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("PEAKPackSyncPhoton: only master should call SendPacksToClients.");
            onComplete?.Invoke(false);
            return;
        }

        var dto = new LevelPackCollectionDTO
        {
            sendId = Guid.NewGuid().ToString(),
            packs = packs.Select(p => new LevelPackDTO
            {
                index = p.index,
                replace = p.replace,
                isVariant = p.isVariant,
                biome = p.biome ?? string.Empty,
                bundlePath = p.bundlePath ?? string.Empty,
                prefabName = p.prefabName ?? string.Empty,
                campfirePrefabName = p.campfirePrefabName ?? string.Empty,
                packName = p.packName ?? string.Empty,
                id = p.id ?? string.Empty
            }).ToArray()
        };

        string json = JsonUtility.ToJson(dto);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var raiseOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others,
            CachingOption = EventCaching.DoNotCache
        };
        var sendOptions = new SendOptions { Reliability = true };

        lastReceivedAcks.Clear();
        expectedAckCount = PhotonNetwork.CurrentRoom.PlayerCount - 1;
        lastMasterSendId = dto.sendId;

        PhotonNetwork.RaiseEvent(EVENT_SYNC_PACKS, bytes, raiseOptions, sendOptions);
        Debug.Log($"PEAKPackSyncPhoton: Master sent {dto.packs.Length} packs (sendId={dto.sendId}) to clients. WaitingForAcks={waitForAcks}");

        if (waitForAcks)
        {
            StartCoroutine(WaitForAcksCoroutine(timeout: 12f, onComplete: onComplete));
        }
        else
        {
            onComplete?.Invoke(true);
        }
    }

    private IEnumerator WaitForAcksCoroutine(float timeout, Action<bool> onComplete)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < timeout)
        {
            if (lastReceivedAcks.Count >= expectedAckCount)
            {
                Debug.Log($"PEAKPackSyncPhoton: received all {lastReceivedAcks.Count}/{expectedAckCount} ACKs.");
                onComplete?.Invoke(true);
                yield break;
            }
            yield return new WaitForSeconds(0.15f);
        }

        Debug.LogWarning($"PEAKPackSyncPhoton: Timeout waiting for ACKs ({lastReceivedAcks.Count}/{expectedAckCount}).");
        onComplete?.Invoke(false);
    }

    public void OnEvent(EventData photonEvent)
    {
        try
        {
            if (photonEvent.Code == EVENT_SYNC_PACKS)
            {
                var raw = photonEvent.CustomData as byte[];
                if (raw == null) return;
                var json = System.Text.Encoding.UTF8.GetString(raw);
                var dto = JsonUtility.FromJson<LevelPackCollectionDTO>(json);
                if (dto == null || dto.packs == null || dto.packs.Length == 0)
                {
                    Debug.LogWarning("PEAKPackSyncPhoton: empty pack payload received");
                    return;
                }

                Debug.Log($"PEAKPackSyncPhoton: client received {dto.packs.Length} packs (sendId={dto.sendId}). Starting apply...");
                StartCoroutine(ClientHandleReceivedPacks(dto.sendId!, dto.packs));
                return;
            }

            if (photonEvent.Code == EVENT_PACKS_APPLIED_ACK)
            {
                var raw = photonEvent.CustomData as byte[];
                if (raw == null) return;
                var json = System.Text.Encoding.UTF8.GetString(raw);
                var ack = JsonUtility.FromJson<PackAppliedAck>(json);
                if (ack == null) return;
                if (!PhotonNetwork.IsMasterClient) return;

                if (!string.Equals(ack.sendId, lastMasterSendId, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"PEAKPackSyncPhoton: received ACK for unknown sendId {ack.sendId} (current {lastMasterSendId}). Ignoring.");
                    return;
                }

                lock (lastReceivedAcks)
                {
                    lastReceivedAcks.Add(ack.senderActorNumber);
                }
                Debug.Log($"PEAKPackSyncPhoton: master received ACK from Actor {ack.senderActorNumber} (total {lastReceivedAcks.Count}/{expectedAckCount})");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("PEAKPackSyncPhoton.OnEvent error: " + ex);
        }
    }

    [Serializable]
    private class PackAppliedAck
    {
        public string? sendId;
        public int senderActorNumber;
    }

    private IEnumerator ClientHandleReceivedPacks(string sendId, LevelPackDTO[] dtos)
    {
        var packs = dtos.Select(d => new LevelPack
        {
            index = d.index,
            replace = d.replace,
            isVariant = d.isVariant,
            biome = d.biome ?? string.Empty,
            bundlePath = d.bundlePath ?? string.Empty,
            prefabName = d.prefabName ?? string.Empty,
            campfirePrefabName = d.campfirePrefabName ?? string.Empty,
            packName = d.packName ?? string.Empty,
            id = d.id ?? string.Empty
        }).ToArray();

        float timeout = 10f;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < timeout)
        {
            bool allGood = true;
            foreach (var p in packs)
            {
                if (string.IsNullOrEmpty(p.bundlePath)) continue;
                if (!IsBundleGroupAvailableAndLoaded(p.bundlePath))
                {
                    allGood = false;
                    break;
                }
            }

            if (allGood) break;
            yield return new WaitForSeconds(0.25f);
        }

        try
        {
            if (PEAKLevelLoader.PEAKLevelLoader.Instance != null)
            {
                PEAKLevelLoader.PEAKLevelLoader.Instance.AddPacks(new LevelPackCollection { packs = packs });
                PEAKLevelLoader.PEAKLevelLoader.Instance.ApplyPacksToMapHandler(Singleton<MapHandler>.Instance);

                Debug.Log($"PEAKPackSyncPhoton: client applied {packs.Length} packs locally (sendId={sendId}). Sending ACK.");
            }
            else
            {
                Debug.LogError("PEAKPackSyncPhoton: client PEAKLevelLoader.Instance null; cannot apply packs.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("PEAKPackSyncPhoton.ClientHandleReceivedPacks error: " + ex);
        }

        var ack = new PackAppliedAck { sendId = sendId, senderActorNumber = PhotonNetwork.LocalPlayer.ActorNumber };
        var ackJson = JsonUtility.ToJson(ack);
        var ackBytes = System.Text.Encoding.UTF8.GetBytes(ackJson);

        var opt = new RaiseEventOptions();
        if (PhotonNetwork.MasterClient != null)
            opt.TargetActors = new int[] { PhotonNetwork.MasterClient.ActorNumber };

        var sendOpts = new SendOptions { Reliability = true };
        PhotonNetwork.RaiseEvent(EVENT_PACKS_APPLIED_ACK, ackBytes, opt, sendOpts);
    }

    private bool IsBundleGroupAvailableAndLoaded(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return true;

        var abLoaderType = Type.GetType("AssetBundleLoader") ?? Type.GetType("PEAKLevelLoader.AssetBundleLoader");
        if (abLoaderType == null) return false;

        var instProp = abLoaderType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var inst = instProp?.GetValue(null);
        if (inst == null) return false;

        var groupsProp = abLoaderType.GetProperty("AssetBundleGroups", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var groupsEnumerable = groupsProp?.GetValue(inst) as System.Collections.IEnumerable;
        if (groupsEnumerable == null) return false;

        foreach (var g in groupsEnumerable)
        {
            var gType = g.GetType();
            var nameProp = gType.GetProperty("GroupName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? gType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var nm = nameProp?.GetValue(g) as string ?? string.Empty;
            if (!string.Equals(nm, groupName, StringComparison.OrdinalIgnoreCase)) continue;

            var loadedProp = gType.GetProperty("LoadedStatus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                          ?? gType.GetProperty("IsLoaded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                          ?? gType.GetProperty("Loaded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (loadedProp != null)
            {
                var val = loadedProp.GetValue(g);
                if (val == null) return false;
                var sval = val.ToString();
                if (sval.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0 || sval.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                return false;
            }

            return true;
        }

        return false;
    }
}