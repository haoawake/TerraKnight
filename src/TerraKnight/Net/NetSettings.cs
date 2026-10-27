using System;
using BepInEx.Configuration;

namespace TerraKnight.Net
{
    /// <summary>The multiplayer menu's remembered choices (BepInEx/config, section Multiplayer).</summary>
    internal static class NetSettings
    {
        public static ConfigEntry<string> Name, LastAddress;
        public static ConfigEntry<int> Port, MaxPlayers, Look;
        public static ConfigEntry<bool> SyncEnemies;

        public static void Bind(ConfigFile cfg)
        {
            Name = cfg.Bind("Multiplayer", "PlayerName", "", "多人模式里你的名字（留空则用 Steam 昵称）。");
            Look = cfg.Bind("Multiplayer", "Look", 0, "多人模式里你的外观配色（0 = 泰拉瑞亚默认人物的颜色，1-7 其他配色），别人看到的你就是这个样子。");
            LastAddress = cfg.Bind("Multiplayer", "LastAddress", "127.0.0.1", "上次“通过IP加入”填的地址。");
            Port = cfg.Bind("Multiplayer", "Port", Server.DefaultPort, "开服和加入用的端口（泰拉瑞亚默认 7777）。");
            MaxPlayers = cfg.Bind("Multiplayer", "MaxPlayers", Server.DefaultMaxPlayers, "开服时最多几个人（含自己，最多 16）。");
            SyncEnemies = cfg.Bind("Multiplayer", "SyncEnemies", true,
                "同一个场景里大家看到同一批敌人：先进场景的玩家的游戏负责敌人的行动，别人打到的伤害交给他结算，敌人死了大家一起掉落。关掉则每个人打自己的敌人。");
        }

        /// <summary>The name to use: the one set, else the Steam persona's, else the Windows user's.</summary>
        public static string PlayerName
        {
            get
            {
                // A second copy of the game for a LAN test plays under its own name.
                if (DevCopy.Name != null) return Clip("Test " + (DevCopy.Name.StartsWith("single-") ? DevCopy.Name.Substring(7) : DevCopy.Name));
                string n = Name?.Value?.Trim();
                if (!string.IsNullOrEmpty(n)) return Clip(n);
                n = SteamName();
                if (!string.IsNullOrEmpty(n)) return Clip(n);
                try { n = Environment.UserName; }
                catch (Exception) { n = null; }
                return string.IsNullOrEmpty(n) ? "Knight" : Clip(n);
            }
        }

        static string Clip(string n) => n.Length > 20 ? n.Substring(0, 20) : n;

        static string steamName;
        static bool steamTried;

        /// <summary>SteamFriends.GetPersonaName through Steamworks.NET, when the game has Steam running.</summary>
        static string SteamName()
        {
            if (steamTried) return steamName;
            steamTried = true;
            try
            {
                var t = Type.GetType("Steamworks.SteamFriends, com.rlabrecque.steamworks.net");
                var m = t?.GetMethod("GetPersonaName", Type.EmptyTypes);
                steamName = m?.Invoke(null, null) as string;
            }
            catch (Exception)
            {
                steamName = null;
            }
            return steamName;
        }
    }
}
