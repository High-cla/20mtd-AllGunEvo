using System;
using HarmonyLib;
using flanne;
using flanne.Core;

namespace AllGunEvo.Features;

/// <summary>
/// Grants every gun evolution of the gun picked for this run, once per run.
/// Runs from CombatState.Enter so that every scene object's Start() (PlayerHealth.Start resets
/// vulnerableTags) has already run; LoadoutInitializer.Start raced against it and silently
/// re-added "HarmfulToPlayer", which is why Blast Shield did nothing.
/// </summary>
[HarmonyPatch(typeof(CombatState), "Enter")]
internal static class GunEvolutionFeature
{
    private static int appliedForInstanceId;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!AllGunEvoPlugin.AllEvolutions.Value) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        int instanceId = player.GetInstanceID();
        if (instanceId == appliedForInstanceId) return;
        appliedForInstanceId = instanceId;

        Gun gun = player.gun;
        GunData data = gun != null ? gun.gunData : null;
        if (data == null || data.gunEvolutions == null || player.playerPerks == null)
        {
            AllGunEvoPlugin.Log.LogWarning("no gun evolution data available at run start.");
            return;
        }

        int applied = 0;
        for (int i = 0; i < data.gunEvolutions.Count; i++)
        {
            GunEvolution evolution = data.gunEvolutions[i];
            if (evolution == null) continue;
            try
            {
                player.playerPerks.Equip(evolution);
                applied++;
            }
            catch (Exception e)
            {
                AllGunEvoPlugin.Log.LogError("evolution " + i + " (" + evolution.name + ") failed: " + e);
            }
        }

        AllGunEvoPlugin.Log.LogInfo("Applied " + applied + "/" + data.gunEvolutions.Count +
            " evolutions for gun " + data.name + ". Vulnerable tags: " + VulnerableTags(player));
    }

    private static string VulnerableTags(PlayerController player)
    {
        if (player.playerHealth == null || player.playerHealth.vulnerableTags == null) return "n/a";
        return string.Join(",", player.playerHealth.vulnerableTags.ToArray());
    }
}
