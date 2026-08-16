using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using EFT;
using System;
using QuickSell.Patches;
using System.IO;
using Newtonsoft.Json.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace QuickSell
{

    [BepInPlugin("QuickSell.UniqueGUID", "QuickSell", "2.3.1")]
    [BepInDependency("com.SPT.core", "4.1.0")]
    [BepInDependency("Tyfon.UIFixes", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly System.Version UIFixesMinimumVersion = new(2, 5);
        private const string UIFixesPluginId = "Tyfon.UIFixes";

        public static bool EnableQuickSellFlea = true;

        public static bool ShowConfirmationDialog = true;

        public static bool DisableKeybinds = false;

        public static bool EnableUIFixesIntegration = false;
        public static bool UIFixesDetected =>
            Chainloader.PluginInfos.TryGetValue(UIFixesPluginId, out var pluginInfo) &&
            pluginInfo?.Metadata?.Version >= UIFixesMinimumVersion;

        internal static ConfigEntry<KeyboardShortcut> KeybindSellFlea;
        internal static ConfigEntry<KeyboardShortcut> KeybindSellFleaImmediate;

        public static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;
            new ContextMenuPatch().Enable();

            try
            {
                var modPath = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                if (!string.IsNullOrEmpty(modPath))
                {
                    LoadConfig(modPath.Replace('\\', '/'));
                }
                else
                {
                    Logger.LogWarning("Could not determine plugin path, using defaults");
                    EnableUIFixesIntegration = UIFixesDetected;
                }

                Logger.LogInfo(UIFixesDetected
                    ? $"UIFixes detected, multi-select integration {(EnableUIFixesIntegration ? "enabled" : "disabled in config")}"
                    : "UIFixes not found, multi-select integration disabled");

                if (!DisableKeybinds)
                {
                    KeybindSellFlea = Config.Bind(
                        "QuickSell",
                        "SellFleaWithConfirmation",
                        new KeyboardShortcut(KeyCode.Mouse2, KeyCode.M),
                        "Hold M and middle-click to sell on the flea (with confirmation)");
                    KeybindSellFleaImmediate = Config.Bind(
                        "QuickSell",
                        "SellFleaImmediate",
                        new KeyboardShortcut(KeyCode.Mouse2, KeyCode.N),
                        "Hold N and middle-click to immediately sell on the flea");
                    KeybindPatches.Enable();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

        }

        private void LoadConfig(string path)
        {
            var configPath = Path.Combine(path, "config.json");
            if (!File.Exists(configPath))
            {
                Logger.LogWarning("config.json not found, using defaults");
                EnableUIFixesIntegration = UIFixesDetected;
                return;
            }

            var config = JObject.Parse(File.ReadAllText(configPath));

            Logger.LogInfo("Loading config");

            if (config.ContainsKey("EnableQuickSellFlea"))
            {
                EnableQuickSellFlea = (bool)config["EnableQuickSellFlea"];
            }

            if (config.ContainsKey("ShowConfirmationDialog"))
            {
                ShowConfirmationDialog = (bool)config["ShowConfirmationDialog"];
            }

            if (config.ContainsKey("DisableKeybinds"))
            {
                DisableKeybinds = (bool)config["DisableKeybinds"];
            }

            if (config.ContainsKey("EnableUIFixesIntegration"))
            {
                EnableUIFixesIntegration = (bool)config["EnableUIFixesIntegration"];
            }
            else
            {
                EnableUIFixesIntegration = UIFixesDetected;
            }
        }
    }
}
