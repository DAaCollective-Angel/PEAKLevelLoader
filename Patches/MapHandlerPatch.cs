using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace PEAKLevelLoader.Patches
{
    [HarmonyPatch(typeof(MapHandler))]
    internal class MapHandlerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        static void Postfix_Awake(MapHandler __instance)
        {
            try
            {
                if (PEAKLevelLoader.Instance != null)
                {
                    PEAKLevelLoader.Instance.ApplyPacksToMapHandler(__instance);
                    try
                    {
                        var detectBiomes = typeof(MapHandler).GetMethod("DetectBiomes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        detectBiomes?.Invoke(__instance, null);
                    } catch (Exception e) { PEAKLevelLoader.Logger.LogWarning($"DetectBiomes failed after ApplyPacks: {e}"); }
                }
                else
                {
                    PEAKLevelLoader.Logger.LogWarning("PEAKLevelLoader.Instance is null — can't apply packs yet.");
                }
            }
            catch (Exception ex)
            {
                PEAKLevelLoader.Logger.LogError($"MapHandlerPatches.Postfix_Awake exception: {ex}");
            }
        }
    }
}
