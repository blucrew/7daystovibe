using BepInEx;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HapticsPlugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony? _harmony;

        private void Awake()
        {
            // Logger must be first — everything else calls into it
            HapticsLogger.Init(Logger, Config);
            HapticsLogger.Info(LogCat.System, $"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loading…");

            // Bind all config entries (must happen before patches or GUI read them)
            HapticsConfig.Init(Config);

            // Configure XToys webhook (reads the bound config value set above)
            XToysManager.Configure(HapticsConfig.XToysWebhookId.Value);
            HapticsConfig.XToysWebhookId.SettingChanged += (_, _) =>
                XToysManager.Configure(HapticsConfig.XToysWebhookId.Value);

            // Connect to Intiface Central async (fire and forget — game keeps loading)
            _ = ButtplugManager.InitAsync();

            // Attach the in-game settings GUI
            gameObject.AddComponent<HapticsGUI>();
            HapticsLogger.Info(LogCat.System, "Settings GUI ready — press Insert in-game to open.");

            // Apply Harmony patches — one class at a time so a single missing
            // target method (e.g. a class renamed in this game version) only skips
            // that one patch instead of aborting every patch via PatchAll().
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            ApplyPatchesResilient(_harmony);
        }

        private static void ApplyPatchesResilient(Harmony harmony)
        {
            int ok = 0, skipped = 0;
            var patchTypes = AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Any());

            foreach (var type in patchTypes)
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    HapticsLogger.Info(LogCat.Patch, $"✓ {type.Name}");
                    ok++;
                }
                catch (Exception ex)
                {
                    // Unwrap to the most useful message (HarmonyException wraps the real cause).
                    string why = ex.InnerException?.Message ?? ex.Message;
                    HapticsLogger.Warning(LogCat.Patch, $"✗ {type.Name} skipped — {why}");
                    skipped++;
                }
            }

            HapticsLogger.Info(LogCat.System,
                $"Patches applied: {ok} active, {skipped} skipped. " +
                (skipped > 0 ? "Skipped patches mean those events won't fire on this game version." : "All good."));
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _ = ButtplugManager.ShutdownAsync();
            _ = XToysManager.StopAsync();   // send intensity=0 before exit
            HapticsLogger.Shutdown();
        }
    }

    internal static class PluginInfo
    {
        public const string PLUGIN_GUID    = "com.rustyblu.7dtd_haptics";
        public const string PLUGIN_NAME    = "7DTD Haptics";
        public const string PLUGIN_VERSION = "0.1.0";
    }
}
