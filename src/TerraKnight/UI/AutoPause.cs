using BepInEx.Configuration;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's Autopause (Main.autoPause, the settings' "Autopause On / Off", LegacyMenu.69/70): in a
    /// single-player game the world stops while the inventory is open (Main.CanPauseGame: playerInventory),
    /// and always while the settings are open (ingameOptionsWindow). The game stands still as its own
    /// inventory and map stop it, by the time scale (GameHooks.PlayerHasControl reads that as no control,
    /// so nothing is used meanwhile); the inventory's panels keep working. Never in a LAN game, and the
    /// game's pause menu keeps its own say over the time scale.
    /// </summary>
    internal sealed class AutoPause : MonoBehaviour
    {
        static ConfigEntry<bool> setting;
        static bool holding;

        /// <summary>The switch (off by default, as in Terraria).</summary>
        public static bool On { get; private set; }

        /// <summary>Whether the world is stopped by it now.</summary>
        public static bool Holding => holding;

        public static void Bind(ConfigEntry<bool> entry)
        {
            setting = entry;
            On = entry.Value;
            var go = new GameObject("TerraKnight AutoPause");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<AutoPause>();
        }

        public static void Set(bool on)
        {
            On = on;
            if (setting != null) setting.Value = on;
            Plugin.Log.LogInfo($"[设置] 自动暂停：{(on ? "开" : "关")}");
        }

        static bool Wanted(GameManager gm)
        {
            if (gm == null || !GameHooks.IsGameplay(gm) || gm.isPaused || gm.IsInSceneTransition || gm.gameState != GlobalEnums.GameState.PLAYING) return false;
            if (Net.NetSession.Active || TerraMod.Instance == null) return false;
            return IngameOptions.Open || (On && Items.InventoryGui.Open);
        }

        void LateUpdate()
        {
            var gm = GameManager.UnsafeInstance;
            if (Wanted(gm))
            {
                holding = true;
                if (Time.timeScale != 0f) Time.timeScale = 0f;
            }
            else if (holding)
            {
                holding = false;
                // The pause menu (or a scene change) has the time scale now: left to the game.
                if (gm != null && !gm.isPaused && Time.timeScale == 0f) Time.timeScale = 1f;
            }
        }
    }
}
