using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The Nightmare King's tent (Godhome's hall and the dream's) has no roof over its arena: Grimm rises out of the view in
    /// his moves, so the game never closed it, and Terraria's wings carried the player up out of the picture to the scene's
    /// top (the user's 10-29 "神居王格林房顶有bug，可以无限上天"). There the player stops at the top of what the camera shows
    /// (the view of the camera lock he is in), as under a ceiling; nothing else is stopped (Grimm still goes up and comes down
    /// as the game has him).
    /// </summary>
    [HarmonyPatch]
    internal static class ArenaCeiling
    {
        static readonly HashSet<string> Rooms = new HashSet<string> { "GG_Grimm_Nightmare", "Grimm_Nightmare" };
        public static int DevStops;
        public static float DevTop => top;

        static string topFor;
        static float top = float.NaN, lookedAt = -10f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null || ___rb2d == null || !Rooms.Contains(gm.sceneName)) return;
            var cs = __instance.cState;
            if (gm.IsInSceneTransition || cs.transitioning || cs.dead || cs.hazardDeath || cs.hazardRespawning) return;
            if (!GameHooks.PlayerHasControl() || Player.CutsceneAttack.KnightHidden()) return;
            var col = __instance.GetComponent<BoxCollider2D>();
            if (col == null) return;
            var b = col.bounds;
            if (!Top(gm.sceneName, b.center, out float y)) return;
            var v = ___rb2d.linearVelocity;
            if (b.max.y + Mathf.Max(0f, v.y) * Time.fixedDeltaTime <= y) return;
            if (v.y > 0f) ___rb2d.linearVelocity = new Vector2(v.x, 0f);
            if (b.max.y > y)
            {
                var p = __instance.transform.position + Vector3.down * (b.max.y - y);
                __instance.transform.position = p;
                ___rb2d.position = p;
            }
            DevStops++;
        }

        /// <summary>The room's ceiling: the top of its camera lock's view round the player, looked for every 2 s until found.</summary>
        static bool Top(string scene, Vector2 hero, out float y)
        {
            if (scene != topFor)
            {
                topFor = scene;
                top = float.NaN;
                lookedAt = -10f;
            }
            if (float.IsNaN(top) && Time.time - lookedAt > 2f)
            {
                lookedAt = Time.time;
                var region = TerrainSystem.Find(scene);
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene);
                if (region != null && region.Root != null && s.IsValid() && s.isLoaded)
                {
                    var room = new Rect(region.Root.position, new Vector2(region.Grid.Width, region.Grid.Height) * CellGrid.CellSize);
                    var view = BossArenaZone.View(s, room, hero);
                    if (view != null)
                    {
                        top = view.Value.yMax;
                        Plugin.Log.LogInfo($"[场地顶] {scene}：头顶没有封顶，玩家最高到 y {top:F1}（镜头锁定的画面顶，场景顶 {room.yMax:F1}）");
                    }
                }
            }
            y = top;
            return !float.IsNaN(top);
        }
    }
}
