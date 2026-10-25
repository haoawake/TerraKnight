using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// Dying in a LAN game (the user, 2026-10-21: "联机的时候死亡会有15秒的复活cd，期间玩家会变成半透明的尸体褪去所有装备，啥也干不了"):
    /// the game's own wait before the respawn (GameManager.PlayerDead's, DEATH_WAIT) is stretched to RespawnSeconds from the
    /// death; meanwhile the Terraria character lies where the Knight fell, half seen, wearing and holding nothing (the
    /// prostrate frame of the character's own wake-up art, which the others are sent as they are sent any pose), its
    /// seconds left counted under "你被杀死了……", the inventory kept shut. Dreams and Godhome die their own way (no
    /// PlayerDead), as do hazards (a respawn by the hazard, not a death).
    /// </summary>
    [HarmonyPatch]
    internal static class NetDeath
    {
        public const float RespawnSeconds = 15f;
        public const float CorpseOpacity = 0.4f;
        static float diedAt = -1f;
        static bool wasDead;
        static Vector2 feet;
        static float facing = 1f;
        public static int DevStretched;

        /// <summary>The corpse to draw now: a LAN game's death, its wait running.</summary>
        public static bool ShowCorpse => diedAt >= 0f;
        public static Vector2 CorpseFeet => feet;
        public static float CorpseFacing => facing;

        /// <summary>Whole seconds left before the respawn (0 when not counting).</summary>
        public static int SecondsLeft => diedAt < 0f ? 0 : Mathf.Max(0, Mathf.CeilToInt(RespawnSeconds - (Time.time - diedAt)));

        /// <summary>Each frame: the death seen as it comes (where the character stood), its corpse and its lock while it lasts.</summary>
        public static void Tick(Vector2 lastFeet, float lastFacing)
        {
            var hero = HeroController.SilentInstance;
            bool dead = hero != null && hero.cState.dead;
            if (dead && !wasDead && NetSession.Active && Counts())
            {
                diedAt = Time.time;
                feet = lastFeet;
                facing = lastFacing;
            }
            if (!dead) diedAt = -1f;
            wasDead = dead;
            if (diedAt >= 0f && Items.InventoryGui.Open) Items.InventoryGui.Open = false;
        }

        /// <summary>A death of the kind that waits for its respawn (not a dream's or Godhome's).</summary>
        static bool Counts()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null) return false;
            string zone = gm.GetCurrentMapZone();
            return zone != "DREAM_WORLD" && zone != "GODS_GLORY";
        }

        /// <summary>GameManager.PlayerDead: in a LAN game its wait (the death's own few seconds) made RespawnSeconds.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDead))]
        static void Stretch(ref float waitTime)
        {
            if (!NetSession.Active) return;
            // The respawn comes RespawnSeconds after the death: less what has passed and the 0.8 s fade after the wait.
            float passed = diedAt >= 0f ? Time.time - diedAt : 0f;
            waitTime = Mathf.Max(waitTime, RespawnSeconds - passed - 0.8f);
            DevStretched++;
            Plugin.Log.LogInfo($"[联机] 倒下：{waitTime:F1} 秒后复活");
        }
    }
}
