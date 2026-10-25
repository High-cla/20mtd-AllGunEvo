using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using flanne;
using flanne.Core;
using flanne.UI;

namespace AllGunEvo.Adapters;

/// <summary>Adapter only: extract the run token and hand it to the core. No decisions here.</summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.Enter))]
internal static class RunStartedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        AdapterHost.OnRunStarted(PlayerController.Instance);
    }
}

/// <summary>
/// Adapter only: the game rebuilds its swap chain in OptionsSetter (flanne.UI/OptionsSetter.cs:166/:179)
/// and Unity resets vSyncCount on rebuild, so re-apply right after.
/// </summary>
[HarmonyPatch]
internal static class SwapChainPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionsSetter), GameApi.SetFullscreenMethodName)]
    private static void AfterFullscreen()
    {
        AdapterHost.Redisplay("options fullscreen");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OptionsSetter), GameApi.SetResolutionMethodName)]
    private static void AfterResolution()
    {
        AdapterHost.Redisplay("options resolution");
    }
}

/// <summary>Adapter only: pump the clock into the core and re-apply display settings per scene.</summary>
internal sealed class FrameRunner : MonoBehaviour
{
    private void Update()
    {
        AdapterHost.OnUpdate(Time.deltaTime);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AdapterHost.OnSceneLoaded(scene);
    }
}
