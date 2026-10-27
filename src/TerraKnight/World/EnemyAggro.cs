using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// An enemy struck by the player comes for him. Terraria's NPCs go for the player who hit them from wherever they are
    /// (the hit sets npc.target); Hollow Knight's only stir when the Knight comes within their alert range or into their
    /// sight, so an enemy shot from beyond its range took the shots where it stood. For <see cref="AggroTime"/> seconds
    /// after each hit the struck enemy's waking ranges (its AlertRange children named for it, "Alert Range" and the like,
    /// not those for its attacks) and its line of sight report the Knight there, and it comes after him as if he had
    /// walked up to it. Then they are as the Knight really stands. Bosses run their own fights and are left alone.
    /// </summary>
    [HarmonyPatch]
    internal static class EnemyAggro
    {
        public const float AggroTime = 10f;

        static readonly AccessTools.FieldRef<AlertRange, bool> inRange = AccessTools.FieldRefAccess<AlertRange, bool>("isHeroInRange");
        static readonly AccessTools.FieldRef<LineOfSightDetector, bool> canSee = AccessTools.FieldRefAccess<LineOfSightDetector, bool>("canSeeHero");

        sealed class Angry
        {
            public HealthManager Hm;
            public float Until;
            public AlertRange[] Ranges;
            public SightRange[] Sights;
        }

        /// <summary>
        /// A range of the kind made of a trigger and a sight line (the "alert_range" FSM: the Hive's bees, their guardians):
        /// while the Knight is in its trigger it writes whether it sees him into a bool of its enemy's machine ("Hero in
        /// range"), which the machine reads to wake. Struck, that bool is held on as the ranges are (the user's 10-25
        /// "新版敌人被攻击不进入战斗": a Hive Guardian shot from past its trigger never turned).
        /// </summary>
        sealed class SightRange
        {
            public PlayMakerFSM Range;
            public HutongGames.PlayMaker.FsmBool Sees;
        }

        /// <summary>
        /// A waking trigger of the plain kind (the "Detect Hit" FSM, named for waking: "Wake Range", "Alert Range"): it tells
        /// its enemy its Enter Event (WAKE, ALERT...) when the Knight comes into it. Struck from past it, the enemy is told
        /// it once, as if he had.
        /// </summary>
        static void WakeByTriggers(HealthManager hm)
        {
            foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.FsmName != "Detect Hit" || f.gameObject == hm.gameObject || !Wakes(f.gameObject.name)) continue;
                string ev = f.FsmVariables.FindFsmString("Enter Event")?.Value;
                var owner = f.transform.parent;
                if (string.IsNullOrEmpty(ev) || owner == null) continue;
                FSMUtility.SendEventToGameObject(owner.gameObject, ev, false);
                DevWoken++;
            }
        }

        /// <summary>Dev: waking events sent for struck enemies' triggers.</summary>
        public static int DevWoken;

        static SightRange[] SightRanges(HealthManager hm)
        {
            var list = new List<SightRange>();
            foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f == null || f.FsmName != "alert_range" || f.gameObject == hm.gameObject) continue;
                string fsmName = f.FsmVariables.FindFsmString("FSM Name")?.Value, boolName = f.FsmVariables.FindFsmString("Bool Name")?.Value;
                if (string.IsNullOrEmpty(fsmName) || string.IsNullOrEmpty(boolName) || boolName.ToLowerInvariant().Contains("unalert")) continue;
                var owner = f.transform.parent;
                if (owner == null) continue;
                foreach (var target in owner.GetComponents<PlayMakerFSM>())
                {
                    if (target.FsmName != fsmName) continue;
                    var b = target.FsmVariables.FindFsmBool(boolName);
                    if (b != null) list.Add(new SightRange { Range = f, Sees = b });
                }
            }
            return list.ToArray();
        }

        static readonly Dictionary<int, Angry> angry = new Dictionary<int, Angry>();
        static readonly List<int> gone = new List<int>();

        /// <summary>Dev: enemies made to see the player by a hit, and ranges held on for them this frame.</summary>
        public static int DevStruck, DevHeld;

        /// <summary>A range that wakes the enemy (not one that starts an attack when the Knight is close).</summary>
        internal static bool Wakes(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("attack") || n.Contains("lunge") || n.Contains("slash") || n.Contains("dive") || n.Contains("jump") || n.Contains("spit") || n.Contains("shoot")) return false;
            return n.Contains("alert") || n.Contains("wake") || n.Contains("sight") || n.Contains("detect") || n.Contains("range");
        }

        /// <summary>A player's hit on an enemy (MeleeHits.Deal): it sees him for the next while.</summary>
        public static void Struck(HealthManager hm, Items.EnemyStats.Info info)
        {
            if (hm == null || hm.isDead || (info != null && info.Boss) || Net.Enemies.IsPuppet(hm)) return;
            int id = hm.GetInstanceID();
            if (!angry.TryGetValue(id, out var a))
            {
                var ranges = new List<AlertRange>();
                foreach (var r in hm.GetComponentsInChildren<AlertRange>(true))
                    if (r != null && Wakes(r.gameObject.name)) ranges.Add(r);
                angry[id] = a = new Angry { Hm = hm, Ranges = ranges.ToArray(), Sights = SightRanges(hm) };
                WakeByTriggers(hm);
                DevStruck++;
            }
            a.Until = Time.time + AggroTime;
        }

        public static bool IsAngry(HealthManager hm) => hm != null && angry.TryGetValue(hm.GetInstanceID(), out var a) && Time.time < a.Until && !hm.isDead;

        /// <summary>Each frame of play: the struck enemies' waking ranges held on; when the while is over, as the Knight really stands.</summary>
        public static void Tick()
        {
            if (angry.Count == 0) return;
            DevHeld = 0;
            var hero = HeroController.SilentInstance;
            var heroBox = hero != null ? hero.GetComponent<Collider2D>() : null;
            float now = Time.time;
            gone.Clear();
            foreach (var kv in angry)
            {
                var a = kv.Value;
                bool over = a.Hm == null || a.Hm.isDead || now >= a.Until;
                foreach (var r in a.Ranges)
                {
                    if (r == null) continue;
                    if (!over) { inRange(r) = true; DevHeld++; }
                    else inRange(r) = heroBox != null && Touching(r, heroBox);
                }
                foreach (var s in a.Sights)
                {
                    if (s.Range == null) continue;
                    if (!over) { s.Sees.Value = true; DevHeld++; }
                    // Its own sight line writes it again while the Knight is in its trigger.
                    else if (s.Range.ActiveStateName != "Raycast") s.Sees.Value = false;
                }
                if (over) gone.Add(kv.Key);
            }
            foreach (int id in gone) angry.Remove(id);
        }

        static bool Touching(AlertRange r, Collider2D hero)
        {
            foreach (var c in r.GetComponents<Collider2D>())
                if (c != null && c.enabled && c.IsTouching(hero)) return true;
            return false;
        }

        /// <summary>A scene gone: its enemies with it.</summary>
        public static void Clear() => angry.Clear();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LineOfSightDetector), "Update")]
        static void AfterSight(LineOfSightDetector __instance)
        {
            if (angry.Count == 0 || canSee(__instance)) return;
            var hm = __instance.GetComponentInParent<HealthManager>();
            if (IsAngry(hm)) canSee(__instance) = true;
        }
    }
}
