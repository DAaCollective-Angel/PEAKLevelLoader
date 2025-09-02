using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PEAKLevelLoader
{
    internal static class PatchHooks
    {
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Type mapHandlerType = Type.GetType("MapHandler") ?? Type.GetType("MapHandler, Assembly-CSharp");
                if (mapHandlerType != null)
                {
                    var awakeMethod = mapHandlerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (awakeMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PatchHooks).GetMethod(nameof(MapHandler_Awake_Postfix), BindingFlags.Static | BindingFlags.NonPublic));
                        harmony.Patch(awakeMethod, postfix: postfix);
                        Debug.Log("PatchHooks: patched MapHandler.Awake");
                    }
                }

                Type bakerType = Type.GetType("MapBaker") ?? Type.GetType("MapBaker, Assembly-CSharp");
                if (bakerType != null)
                {
                    var getBiome = bakerType.GetMethod("GetBiomeID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (getBiome != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PatchHooks).GetMethod(nameof(MapBaker_GetBiomeID_Postfix), BindingFlags.Static | BindingFlags.NonPublic));
                        harmony.Patch(getBiome, postfix: postfix);
                        Debug.Log("PatchHooks: patched MapBaker.GetBiomeID");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PatchHooks.ApplyPatches failed: " + ex);
            }
        }

        static void MapHandler_Awake_Postfix(object __instance)
        {
        }

        static void MapBaker_GetBiomeID_Postfix(object __instance, int levelIndex, ref string __result)
        {
            try
            {
                if (PEAKLevelLoader.Instance == null) return;
                var packs = PEAKLevelLoader.Instance.GetPacks();
                if (packs == null || packs.packs == null) return;
                foreach (var p in packs.packs)
                {
                    if (p == null) continue;
                    if (p.index == levelIndex)
                    {
                        if (!string.IsNullOrEmpty(p.biome))
                            __result = p.biome;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(__result))
                {
                    foreach (var p in packs.packs)
                    {
                        if (p == null) continue;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("MapBaker_GetBiomeID_Postfix error: " + ex); }
        }
    }
}
