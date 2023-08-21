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
        internal const string ModVersion = "1.0.1";
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
            PreventError.SettingChanged += (sender, args) =>
            {
                MethodInfo targetMethod = AccessTools.Method(typeof(ZSteamMatchmaking), "<OnServerResponded>g__TryConvertTagsStringToDictionary|37_0");
                MethodInfo transpilerMethod = AccessTools.Method(typeof(ZSteamMatchmaking_OnServerResponded_Patch), "Transpiler");
                if (PreventError.Value == Toggle.Off)
                {
                    // Unpatch the transpiler method if the toggle is off
                    _harmony.Unpatch(targetMethod, HarmonyPatchType.Transpiler, _harmony.Id);
                }
                else
                {
                    // Patch the transpiler method if the toggle is on
                    _harmony.Patch(targetMethod, transpiler: new HarmonyMethod(transpilerMethod));
                }
            };


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

    [HarmonyPatch(typeof(ZSteamMatchmaking), "<OnServerResponded>g__TryConvertTagsStringToDictionary|37_0")]
    public static class ZSteamMatchmaking_OnServerResponded_Patch
    {
        // Special thank you to KG for finding a better way to fix this issue compared to version 1.0.0
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            if (SteamMatchmakingKeyFixPlugin.PreventError.Value == SteamMatchmakingKeyFixPlugin.Toggle.On)
            {
                MethodInfo toReplace = AccessTools.Method(typeof(Dictionary<string, string>), "set_Item");
                MethodInfo toFind = AccessTools.Method(typeof(Dictionary<string, string>), "Add");
                foreach (var instruction in code)
                {
                    if (instruction.opcode == OpCodes.Callvirt && (MethodInfo)instruction.operand == toFind)
                        instruction.operand = toReplace;

                    yield return instruction;
                }
            }
            else
            {
                foreach (var instruction in code)
                {
                    yield return instruction;
                }
            }
        }
    }
}