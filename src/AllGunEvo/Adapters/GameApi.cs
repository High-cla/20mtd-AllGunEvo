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
/// Every Harmony string target and every AccessTools lookup lives here.
/// </summary>
internal sealed class GameApi : IEvolutionGateway, IVacuumGateway
{
    internal static readonly MethodInfo CombatEnter = AccessTools.Method(typeof(CombatState), "Enter");
    internal static readonly MethodInfo EquipEvolution = AccessTools.Method(typeof(PlayerPerks), "Equip", new[] { typeof(GunEvolution) });
    internal static readonly MethodInfo PickupCoroutine = AccessTools.Method(typeof(Pickup), "PickupCR");
    internal static readonly FieldInfo PickupRoutineField = AccessTools.Field(typeof(Pickup), "pickUpCoroutine");
    internal static readonly PropertyInfo CurrentState = AccessTools.Property(typeof(StateMachine), "CurrentState");
    internal static readonly FieldInfo GunEvolutionsField = AccessTools.Field(typeof(GunData), "gunEvolutions");
    internal static readonly MethodInfo SetFullscreen = AccessTools.Method(typeof(OptionsSetter), "SetFullscreen");
    internal static readonly MethodInfo SetResolution = AccessTools.Method(typeof(OptionsSetter), "SetResolution");

    private readonly ILogPort log;
    private PlayerController player;
    private List<GunEvolution> evolutions;

    internal GameApi(ILogPort log)
    {
        this.log = log;
    }

    internal static bool Validate(ILogPort log)
    {
        bool ok = true;
        ok &= Check(CombatEnter, "CombatState.Enter", log);
        ok &= Check(EquipEvolution, "PlayerPerks.Equip(GunEvolution)", log);
        ok &= Check(PickupCoroutine, "Pickup.PickupCR", log);
        ok &= Check(PickupRoutineField, "Pickup.pickUpCoroutine", log);
        ok &= Check(CurrentState, "StateMachine.CurrentState", log);
        ok &= Check(GunEvolutionsField, "GunData.gunEvolutions", log);
        ok &= Check(SetFullscreen, "OptionsSetter.SetFullscreen", log);
        ok &= Check(SetResolution, "OptionsSetter.SetResolution", log);
        return ok;
    }

    private static bool Check(MemberInfo member, string label, ILogPort log)
    {
        if (member != null)
        {
            log?.Info("ok " + label);
            return true;
        }
        log?.Error("missing game member: " + label);
        return false;
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
            if (PickupRoutineField != null && PickupRoutineField.GetValue(pickup) != null) continue;

            if (PickupCoroutine != null)
            {
                IEnumerator routine = PickupCoroutine.Invoke(pickup, new object[] { pickupper }) as IEnumerator;
                if (routine != null)
                {
                    if (PickupRoutineField != null) PickupRoutineField.SetValue(pickup, routine);
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

    internal static bool IsCombat(GameController controller)
    {
        if (controller == null || CurrentState == null) return false;
        return CurrentState.GetValue(controller, null) is CombatState;
    }
}
