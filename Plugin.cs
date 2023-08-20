using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace SteamMatchmakingKeyFix
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class SteamMatchmakingKeyFixPlugin : BaseUnityPlugin
    {
        internal const string ModName = "SteamMatchmakingKeyFix";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource SteamMatchmakingKeyFixLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        internal static ConfigEntry<Toggle> PreventError = null!;

        public void Awake()
        {
            PreventError = Config.Bind("1 - General", "Prevent Error", Toggle.On, "If on, the error that would otherwise be printed, is masked and will not be shown.");


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                SteamMatchmakingKeyFixLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                SteamMatchmakingKeyFixLogger.LogError($"There was an issue loading your {ConfigFileName}");
                SteamMatchmakingKeyFixLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
    }

    [HarmonyPatch(typeof(Dictionary<string, string>), "Add")]
    public static class DictionaryAddPatch
    {
        /* So I tried to transpile the original method, but until Hildir update, that's very difficult because of the compiler generated code inside it.
         Hildir update might also fix the issue, so we can wait and see.
         I also tried to patch the method (ZSteamMatchmaking.OnServerResponded) multiple ways but that didn't work well either.
         This is the next best thing I could come up with, it's not perfect, but it works. Might prevent other errors from being printed too.
         */
        public static bool Prefix(Dictionary<string, string> __instance, string key, string value)
        {
            if (!__instance.ContainsKey(key) || SteamMatchmakingKeyFixPlugin.PreventError.Value != SteamMatchmakingKeyFixPlugin.Toggle.On) return true;
#if DEBUG
            SteamMatchmakingKeyFixPlugin.SteamMatchmakingKeyFixLogger.LogDebug($"Duplicate key '{key}' with value '{value}' was prevented from being added.");
#endif
            // Key already exists, skip adding it
            return false;
        }
    }
}