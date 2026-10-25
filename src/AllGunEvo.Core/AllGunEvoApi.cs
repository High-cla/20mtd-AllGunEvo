using AllGunEvo.Core;

namespace AllGunEvo.Core;

/// <summary>
/// Public entry point for third-party BepInEx plugins. No loader of our own: the other plugin is
/// discovered by BepInEx as usual and simply calls <see cref="RegisterFeature"/> from its Awake.
/// </summary>
public static class AllGunEvoApi
{
    public const string PluginGuid = "local.20mtd.allgunevo";
    public const string Version = "2.0.0";

    private static readonly FeatureRegistry registry = new FeatureRegistry();

    internal static FeatureRegistry Registry => registry;

    public static int FeatureCount => registry.Count;

    public static bool RegisterFeature(IAllGunEvoFeature feature) => registry.TryRegister(feature, NullLogPort.Instance);
}
