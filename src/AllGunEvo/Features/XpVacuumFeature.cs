using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using flanne;
using flanne.Core;
using flanne.Pickups;
using AllGunEvo.Support;

namespace AllGunEvo.Features;

/// <summary>Every IntervalSeconds in combat, hands every live XP pickup to the player.</summary>
internal sealed class XpVacuumRunner : MonoBehaviour
{
    private static readonly FieldInfo RoutineField = GameApi.PickupRoutineField;
    private static readonly MethodInfo CoroutineMethod = GameApi.PickupCoroutine;
    private static readonly PropertyInfo StateProperty = GameApi.CurrentState;

    private float timer;
    private GameController controller;

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !AllGunEvoPlugin.VacuumEnabled.Value)
        {
            timer = 0f;
            return;
        }
        if (controller == null) controller = FindObjectOfType<GameController>();
        if (controller == null || !IsCombat(controller))
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        float interval = Mathf.Max(1f, AllGunEvoPlugin.VacuumInterval.Value);
        if (timer < interval) return;
        timer -= interval;
        Vacuum(player);
    }

    private static bool IsCombat(GameController gc)
    {
        if (StateProperty == null) return true;
        object state = StateProperty.GetValue(gc, null);
        return state is CombatState;
    }

    private static void Vacuum(PlayerController player)
    {
        XPPickup[] pickups = FindObjectsOfType<XPPickup>();
        GameObject pickupper = player.gameObject;
        int collected = 0;

        for (int i = 0; i < pickups.Length; i++)
        {
            XPPickup pickup = pickups[i];
            if (pickup == null || !pickup.isActiveAndEnabled) continue;
            if (RoutineField != null && RoutineField.GetValue(pickup) != null) continue;

            if (CoroutineMethod != null)
            {
                IEnumerator routine = CoroutineMethod.Invoke(pickup, new object[] { pickupper }) as IEnumerator;
                if (routine != null)
                {
                    if (RoutineField != null) RoutineField.SetValue(pickup, routine);
                    pickup.StartCoroutine(routine);
                    collected++;
                    continue;
                }
            }

            pickup.transform.position = pickupper.transform.position;
            collected++;
        }

        AllGunEvoPlugin.Log.LogInfo("Vacuum collected " + collected + " xp pickups (found " + pickups.Length + ").");
    }
}
