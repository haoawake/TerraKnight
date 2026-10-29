using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A boss room's machines see the Knight only inside it. The rooms are joined into one world, and a room's
    /// trigger can reach past its own edge into the next: the False Knight's battle strip stands down to his arena's
    /// thin floor, and the Knight flying up under it in the room below started the fight and shut its gates on him
    /// outside. A boss room's trigger (an FSM's own, or a Trigger2dEvent's through its PlayMakerUnity2DProxy) takes
    /// the Knight's entering and staying only while he stands in that room's ground; an entering kept from it is
    /// told again once he is inside and still in the trigger (one across a doorway). Leaving always goes through.
    /// </summary>
    [HarmonyPatch]
    internal static class ArenaTriggers
    {
        public static bool Enabled = true;
        public static int DevRefused, DevReplayed;
        internal static bool DevLegacyVesselGate;

        struct Pending
        {
            public Collider2D Trigger, Toucher;
            public string Room;
            public System.Action Replay;
        }

        static readonly List<Pending> pending = new List<Pending>();
        static bool replaying;

        static string Room(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        // This seal's native ENTER action has no tag filter. Any collider (including one ahead of
        // the Knight) used to activate its solid children, shutting the Knight outside the temple.
        static bool VesselGate(GameObject owner) => !DevLegacyVesselGate && owner != null
            && owner.scene.name == "Room_Final_Boss_Core" && owner.name == "Gate"
            && PlayMakerFSM.FindFsmOnGameObject(owner, "Control") != null;

        static bool Ready(GameObject owner, string room)
        {
            if (!HeroIn(room)) return false;
            if (!VesselGate(owner)) return true;
            var hero = HeroController.SilentInstance;
            var body = hero != null ? hero.GetComponent<BoxCollider2D>() : null;
            var entry = owner.GetComponent<Collider2D>();
            // The seal stands left of this trigger. Wait until the whole body is over its inner
            // edge; a weapon, toe or sensor reaching ahead isn't an arrival in the arena.
            return body == null || entry == null || body.bounds.min.x > entry.bounds.min.x + .05f;
        }

        /// <summary>The Knight's body stands in this room's ground (or in no room's: as the game has it).</summary>
        static bool HeroIn(string room)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return true;
            var cell = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            return region == null || Room(region.SceneName) == room;
        }

        /// <summary>Whether this touch of a trigger is kept from it: the Knight (or what he swings) on a boss room's trigger from outside that room.</summary>
        static bool Outside(GameObject owner, Collider2D other, out string room)
        {
            room = null;
            if (!Enabled || replaying || owner == null || other == null) return false;
            var hero = HeroController.SilentInstance;
            if (hero != null && VesselGate(owner) && other != hero.GetComponent<BoxCollider2D>())
            {
                DevRefused++;
                room = owner.scene.name;
                return true;
            }
            if (hero == null || !other.transform.IsChildOf(hero.transform)) return false;
            string scene = owner.scene.name;
            if (!BossRooms.IsRoom(scene)) return false;
            room = Room(scene);
            if (Ready(owner, room)) return false;
            DevRefused++;
            return true;
        }

        /// <summary>An entering kept back, to be told again once the Knight is in the room and still touching the trigger.</summary>
        static void Hold(GameObject owner, Collider2D other, string room, System.Action replay)
        {
            // Non-body contacts on the seal are discarded, never replayed when the player arrives.
            if (VesselGate(owner) && other != HeroController.SilentInstance?.GetComponent<BoxCollider2D>()) return;
            Collider2D trigger = null;
            foreach (var c in owner.GetComponents<Collider2D>())
                if (c != null && c.enabled && c.isTrigger) { trigger = c; break; }
            if (trigger == null) return;
            foreach (var p in pending)
                if (p.Trigger == trigger && p.Toucher == other) return;
            pending.Add(new Pending { Trigger = trigger, Toucher = other, Room = room, Replay = replay });
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Fsm), nameof(HutongGames.PlayMaker.Fsm.OnTriggerEnter2D))]
        static bool FsmEnter(HutongGames.PlayMaker.Fsm __instance, Collider2D other)
        {
            if (!Outside(__instance.GameObject, other, out string room)) return true;
            var fsm = __instance;
            Hold(__instance.GameObject, other, room, () => fsm.OnTriggerEnter2D(other));
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Fsm), nameof(HutongGames.PlayMaker.Fsm.OnTriggerStay2D))]
        static bool FsmStay(HutongGames.PlayMaker.Fsm __instance, Collider2D other) => !Outside(__instance.GameObject, other, out _);

        static readonly System.Reflection.MethodInfo proxyEnter = AccessTools.Method(typeof(PlayMakerUnity2DProxy), "OnTriggerEnter2D");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayMakerUnity2DProxy), "OnTriggerEnter2D")]
        static bool ProxyEnter(PlayMakerUnity2DProxy __instance, Collider2D coll)
        {
            if (!Outside(__instance.gameObject, coll, out string room)) return true;
            var proxy = __instance;
            Hold(__instance.gameObject, coll, room, () => proxyEnter.Invoke(proxy, new object[] { coll }));
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayMakerUnity2DProxy), "OnTriggerStay2D")]
        static bool ProxyStay(PlayMakerUnity2DProxy __instance, Collider2D coll) => !Outside(__instance.gameObject, coll, out _);

        /// <summary>Each frame in play: the enterings kept back told now that the Knight is inside (dropped once he no longer touches the trigger).</summary>
        public static void Tick()
        {
            if (pending.Count == 0) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (p.Trigger == null || p.Toucher == null || !p.Trigger.isActiveAndEnabled || !p.Toucher.isActiveAndEnabled
                    || !(p.Trigger.IsTouching(p.Toucher) || p.Trigger.Distance(p.Toucher).isOverlapped))
                {
                    pending.RemoveAt(i);
                    continue;
                }
                if (!Ready(p.Trigger.gameObject, p.Room)) continue;
                pending.RemoveAt(i);
                replaying = true;
                try
                {
                    p.Replay();
                    DevReplayed++;
                }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[场地] 补发进入 {p.Trigger.name} 出错：{e.Message}"); }
                finally { replaying = false; }
            }
        }

        /// <summary>A save left: nothing kept back.</summary>
        public static void Clear() => pending.Clear();
    }
}
