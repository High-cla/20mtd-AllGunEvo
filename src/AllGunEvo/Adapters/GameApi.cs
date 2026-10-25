using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using flanne;
using flanne.Core;
using flanne.PerkSystem;
using flanne.Pickups;
using flanne.UI;
using AllGunEvo.Core;

namespace AllGunEvo.Adapters;

/// <summary>
/// The single reflection point in the whole plugin. Implements the driven ports the core needs.
/// Public game members are resolved with nameof (compile-time checked); private ones need literals,
/// which are declared once as constants and reused by both AccessTools and [HarmonyPatch].
/// Hot-path calls use cached delegates instead of MethodInfo.Invoke.
/// </summary>
internal sealed class GameApi : IEvolutionGateway, IVacuumGateway
{
    internal const string SetFullscreenMethodName = "SetFullscreen";
    internal const string SetResolutionMethodName = "SetResolution";

    private const string PickupCoroutineMethodName = "PickupCR";
    private const string PickupRoutineFieldName = "pickUpCoroutine";

    internal static readonly MethodInfo CombatEnter = AccessTools.Method(typeof(CombatState), nameof(CombatState.Enter));
    internal static readonly MethodInfo EquipEvolution = AccessTools.Method(typeof(PlayerPerks), nameof(PlayerPerks.Equip), new[] { typeof(GunEvolution) });
    internal static readonly MethodInfo PickupCoroutine = AccessTools.Method(typeof(Pickup), PickupCoroutineMethodName);
    internal static readonly FieldInfo PickupRoutineField = AccessTools.Field(typeof(Pickup), PickupRoutineFieldName);
    internal static readonly FieldInfo GunEvolutionsField = AccessTools.Field(typeof(GunData), nameof(GunData.gunEvolutions));
    internal static readonly MethodInfo SetFullscreen = AccessTools.Method(typeof(OptionsSetter), SetFullscreenMethodName);
    internal static readonly MethodInfo SetResolution = AccessTools.Method(typeof(OptionsSetter), SetResolutionMethodName);

    private static readonly Func<Pickup, GameObject, IEnumerator> PickupInvoker = CreatePickupInvoker();
    private static readonly AccessTools.FieldRef<Pickup, IEnumerator> PickupRoutineRef = CreatePickupRoutineRef();

    private readonly ILogPort log;
    private PlayerController player;
    private List<GunEvolution> evolutions;

    internal GameApi(ILogPort log)
    {
        this.log = log;
    }

    private static Func<Pickup, GameObject, IEnumerator> CreatePickupInvoker()
    {
        if (PickupCoroutine == null) return null;
        try
        {
            return (Func<Pickup, GameObject, IEnumerator>)Delegate.CreateDelegate(
                typeof(Func<Pickup, GameObject, IEnumerator>), PickupCoroutine);
        }
        catch (Exception e)
        {
            logMissing("Pickup.PickupCR delegate bind failed: " + e);
            return null;
        }
    }

    private static AccessTools.FieldRef<Pickup, IEnumerator> CreatePickupRoutineRef()
    {
        if (PickupRoutineField == null) return null;
        try
        {
            return AccessTools.FieldRefAccess<Pickup, IEnumerator>(PickupRoutineFieldName);
        }
        catch (Exception e)
        {
            logMissing("Pickup.pickUpCoroutine field ref failed: " + e);
            return null;
        }
    }

    private static void logMissing(string message) => AllGunEvoPlugin.LogSource?.LogError(message);

    internal static bool Validate(ILogPort log)
    {
        List<string> missing = new List<string>();
        Check(CombatEnter, nameof(CombatState.Enter), log, missing);
        Check(EquipEvolution, nameof(PlayerPerks.Equip), log, missing);
        Check(PickupCoroutine, PickupCoroutineMethodName, log, missing);
        Check(PickupRoutineField, PickupRoutineFieldName, log, missing);
        Check(GunEvolutionsField, nameof(GunData.gunEvolutions), log, missing);
        Check(SetFullscreen, SetFullscreenMethodName, log, missing);
        Check(SetResolution, SetResolutionMethodName, log, missing);

        if (missing.Count == 0) return true;

        log?.Error("missing game members: " + string.Join(", ", missing.ToArray()));
        return false;
    }

    private static void Check(MemberInfo member, string label, ILogPort log, List<string> missing)
    {
        if (member != null)
        {
            log?.Info("ok " + label);
            return;
        }
        missing.Add(label);
    }

    internal void BindRun(PlayerController controller)
    {
        player = controller;
        evolutions = controller != null && controller.gun != null && controller.gun.gunData != null
            ? controller.gun.gunData.gunEvolutions
            : null;
    }

    // ---- IEvolutionGateway ----

    public string GunName
    {
        get
        {
            GunData data = player != null && player.gun != null ? player.gun.gunData : null;
            return data != null ? data.name : string.Empty;
        }
    }

    public int Count => evolutions != null ? evolutions.Count : 0;

    public EvolutionHandle Get(int index)
    {
        GunEvolution evolution = evolutions[index];
        return new EvolutionHandle(evolution != null ? evolution.name : string.Empty, evolution);
    }

    public void Equip(EvolutionHandle handle)
    {
        if (player == null || player.playerPerks == null) throw new InvalidOperationException("player perks unavailable");
        if (!(handle.Native is GunEvolution evolution)) throw new InvalidOperationException("handle is not a GunEvolution");
        player.playerPerks.Equip(evolution);
    }

    // ---- IVacuumGateway ----

    public int CollectAll()
    {
        if (player == null) return 0;

        XPPickup[] pickups = UnityEngine.Object.FindObjectsOfType<XPPickup>();
        GameObject pickupper = player.gameObject;
        int collected = 0;

        for (int i = 0; i < pickups.Length; i++)
        {
            XPPickup pickup = pickups[i];
            if (pickup == null || !pickup.isActiveAndEnabled) continue;
            if (PickupRoutineRef != null && PickupRoutineRef(pickup) != null) continue;

            if (PickupInvoker != null)
            {
                IEnumerator routine = PickupInvoker(pickup, pickupper);
                if (routine != null)
                {
                    if (PickupRoutineRef != null) PickupRoutineRef(pickup) = routine;
                    pickup.StartCoroutine(routine);
                    collected++;
                    continue;
                }
            }

            pickup.transform.position = pickupper.transform.position;
            collected++;
        }

        return collected;
    }

    /// <summary>CurrentState is a public virtual property, so no reflection is needed here.</summary>
    internal static bool IsCombat(GameController controller)
    {
        return controller != null && controller.CurrentState is CombatState;
    }
}
