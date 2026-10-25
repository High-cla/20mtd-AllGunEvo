using System.Reflection;
using HarmonyLib;
using flanne;
using flanne.Core;
using flanne.PerkSystem;
using flanne.Pickups;

namespace AllGunEvo.Support;

/// <summary>Single place where every game member handle is resolved and validated.</summary>
internal static class GameApi
{
    internal static readonly MethodInfo CombatEnter = AccessTools.Method(typeof(CombatState), "Enter");
    internal static readonly MethodInfo EquipEvolution = AccessTools.Method(typeof(PlayerPerks), "Equip", new[] { typeof(GunEvolution) });
    internal static readonly MethodInfo PickupCoroutine = AccessTools.Method(typeof(Pickup), "PickupCR");
    internal static readonly FieldInfo PickupRoutineField = AccessTools.Field(typeof(Pickup), "pickUpCoroutine");
    internal static readonly PropertyInfo CurrentState = AccessTools.Property(typeof(StateMachine), "CurrentState");

    internal static bool Validate()
    {
        bool ok = true;
        ok &= Check(CombatEnter, "CombatState.Enter");
        ok &= Check(EquipEvolution, "PlayerPerks.Equip(GunEvolution)");
        ok &= Check(PickupCoroutine, "Pickup.PickupCR");
        ok &= Check(PickupRoutineField, "Pickup.pickUpCoroutine");
        ok &= Check(CurrentState, "StateMachine.CurrentState");
        ok &= Check(AccessTools.Field(typeof(GunData), "gunEvolutions"), "GunData.gunEvolutions");
        return ok;
    }

    private static bool Check(MemberInfo member, string label)
    {
        if (member != null)
        {
            AllGunEvoPlugin.Log.LogInfo("ok " + label);
            return true;
        }
        AllGunEvoPlugin.Log.LogError("missing game member: " + label);
        return false;
    }
}
