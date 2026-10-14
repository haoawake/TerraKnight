using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Development only: a second copy of the game started beside the first for a LAN test
    /// ("-tk-copy client -tk-savedir D:\somewhere -tk-suite net2client -tk-slot 1"). It keeps its
    /// saves in its own folder (the game's and the mod's, never the player's), writes its own log
    /// and screenshots, and runs the suite given on its command line instead of the config's.
    /// Without "-tk-copy" nothing here does anything.
    /// </summary>
    [HarmonyPatch]
    internal static class DevCopy
    {
        /// <summary>This copy's name ("client"), or null for an ordinary game.</summary>
        public static string Name { get; private set; }
        public static string SaveDir { get; private set; }
        public static string Suite { get; private set; }
        public static int Slot { get; private set; }

        public static void Read()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-tk-copy": Name = args[i + 1]; break;
                    case "-tk-savedir": SaveDir = args[i + 1]; break;
                    case "-tk-suite": Suite = args[i + 1]; break;
                    case "-tk-slot": Slot = int.TryParse(args[i + 1], out int s) ? s : 0; break;
                }
            }
            if (Name == null)
            {
                SaveDir = null;
                return;
            }
            if (string.IsNullOrEmpty(SaveDir)) SaveDir = Path.Combine(Paths.BepInExRootPath, "TerraKnight", "dev", "saves_" + Name);
            Directory.CreateDirectory(SaveDir);
            if (Slot < 1 || Slot > 4) Slot = 1;
            BepInEx.Logging.Logger.Listeners.Add(new FileListener(Path.Combine(Paths.BepInExRootPath, "TerraKnight", "dev", Name + ".log")));
            Plugin.Log.LogInfo($"[副本] 测试副本 {Name}：存档在 {SaveDir}，测试 {Suite}，存档位 {Slot}");
            Redirect(global::Platform.Current as DesktopPlatform);
        }

        static readonly AccessTools.FieldRef<DesktopPlatform, string> saveDirPath = AccessTools.FieldRefAccess<DesktopPlatform, string>("saveDirPath");

        /// <summary>The game's platform writes its saves into this copy's folder (checked before a copy starts a game).</summary>
        public static bool Verified
        {
            get
            {
                var p = global::Platform.Current as DesktopPlatform;
                if (p == null || SaveDir == null) return false;
                Redirect(p);
                return string.Equals(Path.GetFullPath(saveDirPath(p)), Path.GetFullPath(SaveDir), StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>The game's save folder made this copy's (its platform may be set up already, or not yet).</summary>
        static void Redirect(DesktopPlatform p)
        {
            if (p == null || SaveDir == null) return;
            saveDirPath(p) = SaveDir;
            Plugin.Log.LogInfo($"[副本] 游戏存档目录改为 {SaveDir}");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DesktopPlatform), "Awake")]
        static void AfterPlatformAwake(DesktopPlatform __instance) => Redirect(__instance);

        sealed class FileListener : ILogListener
        {
            readonly StreamWriter writer;

            public FileListener(string path)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            }

            public void LogEvent(object sender, LogEventArgs e)
            {
                // The title screen's "Couldn't find GameCameras" every frame says nothing.
                if (e.Data is string s && s.StartsWith("Couldn't find")) return;
                lock (writer) writer.WriteLine($"[{e.Level,-7}:{e.Source.SourceName,10}] {e.Data}");
            }

            public void Dispose() => writer.Dispose();
        }
    }
}
