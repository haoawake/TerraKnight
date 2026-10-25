using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// A LAN game's player who falls in a boss room a teammate is still fighting in (the user, 2026-10-21: "他就回不到boss房间了，
    /// 应该直接被召唤到房间内并给予一秒无敌帧", then "死亡后应该先从复活点站起来结束复活动画，然后才被传送到boss房获得1秒的无敌"):
    /// the death and its respawn go as ever (Net.NetDeath's wait, the bench, the Knight getting up); half a second after the
    /// player has control there, if a teammate is still alive in that room, the player is taken to them by the Wormhole Potion's road
    /// (World.Wormhole.TravelTo: the teammate's place, nothing used), with a second of invincibility as control comes back
    /// in the room. Not from a dream or Godhome, whose own ending decides where the player goes; not when no one alive is
    /// left in the room by then.
    /// </summary>
    internal static class BossRejoin
    {
        const int InvincibleTicks = 60;
        /// <summary>Standing at the respawn point this long (the getting up seen through) before the trip.</summary>
        const float StoodFor = 0.5f;
        static bool wasDead, respawning, travelling;
        static string room;
        static float armedAt, upSince = -1f;
        public static string DevFrom = "";
        public static int DevRejoins, DevArmed, DevTravels;
        public static string DevLast = "";

        static string RoomOf(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>Each frame: a death in a boss room with a teammate in it remembered; up again elsewhere, off to them; there, the invincibility.</summary>
        public static void Tick()
        {
            var hero = HeroController.SilentInstance;
            var gm = GameManager.UnsafeInstance;
            if (hero == null || gm == null) return;
            bool dead = hero.cState.dead;
            if (dead && !wasDead && NetSession.Active) Arm(gm, hero);
            wasDead = dead;
            if (room == null || dead) return;
            if (Time.realtimeSinceStartup - armedAt > 90f) { Forget("太久没回去"); return; }
            if (gm.IsInSceneTransition || !GameHooks.PlayerHasControl() || !GameHooks.IsGameplay(gm)) { upSince = -1f; return; }
            string here = RoomOf(NetScenes.Current);
            if (travelling)
            {
                if (here != room) return;
                // In the room by the teammate, control back: a second of invincibility.
                Player.TerraHurt.Invulnerable(hero, InvincibleTicks);
                DevRejoins++;
                DevLast = $"回到 {room}（{(Vector2)hero.transform.position:F1}），无敌 {InvincibleTicks / 60f:F0} 秒";
                Plugin.Log.LogInfo($"[联机] 首领战中倒下：{DevLast}");
                room = null;
                travelling = false;
                return;
            }
            // Up again after the respawn (the bench, the Knight on his feet): off to the teammate, if one is still there.
            if (here == room) { Forget("已经在房里"); return; }
            if (gm.sceneName.StartsWith("GG_") || gm.sceneName.StartsWith("Dream_")) { Forget("在梦境或神居里"); return; }
            if (upSince < 0f) upSince = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - upSince < StoodFor) return;
            var mate = Mate();
            if (mate == null) { Forget("房里已经没有活着的队友"); return; }
            DevFrom = gm.sceneName;
            travelling = true;
            DevTravels++;
            World.Wormhole.TravelTo(mate.Latest.Scene, mate.Latest.Feet, $"首领战中倒下，回到 {mate.Name} 身边");
        }

        static void Forget(string why)
        {
            if (room != null) Plugin.Log.LogInfo($"[联机] 不回首领房 {room}：{why}");
            room = null;
            travelling = false;
        }

        /// <summary>A teammate alive in the room (its scene or its boss scene).</summary>
        static RemotePlayer Mate()
        {
            foreach (var p in NetSession.Players.Values)
                if (p != null && p.HasState && !p.Latest.Dead && !string.IsNullOrEmpty(p.Latest.Scene) && RoomOf(p.Latest.Scene) == room) return p;
            return null;
        }

        static void Arm(GameManager gm, HeroController hero)
        {
            travelling = false;
            room = null;
            var cell = World.CellGrid.WorldToCell(hero.transform.position);
            string here = World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName ?? gm.sceneName;
            if (string.IsNullOrEmpty(here) || here.StartsWith("GG_") || here.StartsWith("Dream_") || gm.sceneName.StartsWith("GG_") || gm.sceneName.StartsWith("Dream_")) return;
            if (!World.BossRooms.IsRoom(here)) return;
            room = RoomOf(here);
            var mate = Mate();
            if (mate == null) { room = null; return; }
            armedAt = Time.realtimeSinceStartup;
            DevArmed++;
            Plugin.Log.LogInfo($"[联机] 在首领房 {room} 倒下，{mate.Name} 还在里面：复活后回到 TA 身边");
        }
    }
}
