using HarmonyLib;

namespace HapticsPlugin.Patches
{
    /// <summary>
    /// Blood moon start — long building pulse to signal the horde is coming.
    /// </summary>
    [HarmonyPatch(typeof(AIDirectorBloodMoonComponent), "StartBloodMoon")]
    public class BloodMoonStartPatch
    {
        static void Postfix() => HapticsConfig.BloodMoonStart.Fire();
    }

    /// <summary>Blood moon end — relief pulse at dawn.</summary>
    [HarmonyPatch(typeof(AIDirectorBloodMoonComponent), "EndBloodMoon")]
    public class BloodMoonEndPatch
    {
        static void Postfix() => HapticsConfig.BloodMoonEnd.Fire();
    }
}
