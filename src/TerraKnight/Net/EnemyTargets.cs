using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// Terraria's NPC.TargetClosest on the server, for the game that runs a scene's enemies: each enemy, a boss as much as
    /// any other, goes for the living player with the least |dx| + |dy| (in Terraria's pixels, centre to centre) less
    /// that player's aggro (TryTrackingTarget: Player.aggro, from Flesh Knuckles, Putrid Scent, the Turtle, Beetle and
    /// Solar Flare armours, invisibility and stealth). The players it can go for are those in its room; a boss's, those in
    /// its arena. Hallownest's enemies run their own scripts, which look for the Knight: those of an enemy going for
    /// another player find a stand-in at that player's place instead (its FSMs' Knight, GetHero, the Knight as an action's
    /// owner), and its alert ranges and line of sight look at that player. A boss picks again at most every second and a
    /// half (its own attacks pick theirs at their start), and at once when its player is gone; the others every frame,
    /// keeping the one they have unless another is clearly nearer.
    /// </summary>
    [HarmonyPatch]
    internal static class EnemyTargets
    {
        sealed class Target
        {
            public HealthManager Hm;
            public GameObject Proxy;
            public PlayMakerFSM[] Fsms;
            public int Slot = -1;
            public float Picked = -10f;
            public int Frame = -1;
            public bool Boss;
            /// <summary>Where the picked player is (this world), and whether the stand-in leads him (the new enemy AI's aim).</summary>
            public Vector2 At;
            public bool Lead;
        }
        static readonly Dictionary<HealthManager, Target> targets = new Dictionary<HealthManager, Target>();
        static readonly AccessTools.FieldRef<LineOfSightDetector, bool> sight = AccessTools.FieldRefAccess<LineOfSightDetector, bool>("canSeeHero");
        static readonly AccessTools.FieldRef<LineOfSightDetector, AlertRange[]> ranges = AccessTools.FieldRefAccess<LineOfSightDetector, AlertRange[]>("alertRanges");

        /// <summary>One Terraria pixel in world units.</summary>
        const float Px = 1f / 32f;
        /// <summary>How much nearer (Terraria pixels) another player must be to take an ordinary enemy off the one it has.</summary>
        const float Keep = 32f;
        /// <summary>How long a boss keeps the player it picked (seconds), unless that player is gone.</summary>
        const float BossHold = 1.5f;

        public static int DevPicks, DevRemotePicks;

        static bool Eligible(HealthManager hm) => hm != null && !hm.isDead && Enemies.Owns(hm.gameObject.scene.name);

        static Target Get(HealthManager hm)
        {
            if (!Eligible(hm)) return null;
            if (targets.TryGetValue(hm, out var t)) return t;
            var proxy = new GameObject("TK Enemy Target");
            Object.DontDestroyOnLoad(proxy);
            t = new Target { Hm = hm, Proxy = proxy, Fsms = hm.GetComponentsInChildren<PlayMakerFSM>(true), Boss = Items.EnemyStats.Of(hm).Boss };
            targets[hm] = t;
            return t;
        }

        /// <summary>
        /// Player.aggro for the local player, as Terraria sums it each tick: the armour's and accessories' (Bonuses.Aggro),
        /// then invisibility (no more than -750 while not using an item, -250 while using one), then the stealth of the
        /// Psycho Knife (-750 at full), the Shroomite set (-750 at full) and the Vortex set (-1200 at full).
        /// </summary>
        public static int LocalAggro()
        {
            var b = Items.Equipment.Current;
            int aggro = b.Aggro;
            if (b.Invisible)
            {
                bool using_ = TerraMod.Instance != null && TerraMod.Instance.IsUsingItem;
                if (!using_ && aggro > -750) aggro = -750;
                else if (aggro > -250) aggro = -250;
            }
            float stealth = Items.ArmorEffects.Stealth;
            var set = Items.Equipment.FullSet;
            if (Items.MeleeSpecials.PsychoHeld && set != Items.ArmorSet.Vortex) aggro -= (int)((1f - stealth) * 750f);
            else if (set == Items.ArmorSet.Shroomite)
            {
                // Utils.Remap(stealth, 1, 0.75, 1, 0).
                float k = 1f - Mathf.Clamp01((1f - stealth) / 0.25f);
                aggro -= (int)((1f - k) * 750f);
            }
            else if (set == Items.ArmorSet.Vortex && Items.ArmorEffects.VortexStealth) aggro -= (int)((1f - stealth) * 1200f);
            return aggro;
        }

        /// <summary>A hit on the enemy: its waking (EnemyAggro). Terraria's NPCs don't turn on who hit them; TargetClosest goes on as before.</summary>
        public static void Struck(HealthManager hm, int slot)
        {
            if (Get(hm) == null) return;
            World.EnemyAggro.Struck(hm, Items.EnemyStats.Of(hm));
        }

        static string Room(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>Within the boss's arena (its room's own ground, the whole room where it has none).</summary>
        static bool InArena(HealthManager hm, Vector2 at)
        {
            var region = World.TerrainSystem.Find(Room(hm.gameObject.scene.name));
            return region?.OwnZone == null || region.OwnZone.Value.Contains(at);
        }

        /// <summary>Where a player is (their hitbox's middle, in this world) and their aggro; false when the enemy can't go for them.</summary>
        static bool Candidate(Target t, int slot, out Vector2 pos, out int aggro)
        {
            pos = default;
            aggro = 0;
            var hm = t.Hm;
            string room = Room(hm.gameObject.scene.name);
            if (slot == NetSession.MySlot)
            {
                var hero = HeroController.SilentInstance;
                if (hero == null || hero.cState.dead) return false;
                pos = hero.GetComponent<BoxCollider2D>().bounds.center;
                var cell = World.CellGrid.WorldToCell(pos);
                string scene = World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName ?? NetScenes.Current;
                if (Room(scene) != room) return false;
                aggro = LocalAggro();
            }
            else
            {
                if (!NetSession.Players.TryGetValue(slot, out var player) || !player.HasState || player.Latest.Dead || !player.Latest.Shown
                    || Room(player.Latest.Scene) != room || Time.unscaledTime - player.LastStateAt > 2f) return false;
                if (!NetScenes.ToWorld(player.Latest.Scene, player.Latest.BoxCenter, out pos)) return false;
                aggro = player.Latest.Aggro;
            }
            return !t.Boss || InArena(hm, pos);
        }

        /// <summary>NPC.TryTrackingTarget's measure: |dx| + |dy| in Terraria's pixels less the player's aggro.</summary>
        static float Measure(Vector2 from, Vector2 to, int aggro) => (Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y)) / Px - aggro;

        static void Update(Target t)
        {
            if (t.Frame == Time.frameCount) return;
            t.Frame = Time.frameCount;
            var col = t.Hm.GetComponent<Collider2D>();
            Vector2 at = col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)t.Hm.transform.position;
            Vector2 best = at;
            int slot = -1;
            bool hold = t.Boss && t.Slot >= 0 && Time.time - t.Picked < BossHold;
            if (hold && Candidate(t, t.Slot, out var kept, out _))
            {
                slot = t.Slot;
                best = kept;
            }
            else
            {
                // NPC.TargetClosest over every living player.
                float distance = float.PositiveInfinity;
                void Consider(int candidate)
                {
                    if (!Candidate(t, candidate, out var p, out int aggro)) return;
                    float d = Measure(at, p, aggro);
                    if (candidate == t.Slot && !t.Boss) d -= Keep;
                    if (d < distance) { distance = d; slot = candidate; best = p; }
                }
                Consider(NetSession.MySlot);
                foreach (var p in NetSession.Players.Values) Consider(p.Slot);
                if (slot != t.Slot)
                {
                    DevPicks++;
                    if (slot >= 0 && slot != NetSession.MySlot) DevRemotePicks++;
                }
                if (slot != t.Slot || !hold) t.Picked = Time.time;
            }
            t.Slot = slot;
            t.At = best;
            // The new enemy AI (EnemyAi.Brain) leads the enemy's own aim: the stand-in goes ahead of the player, this game's too.
            var brain = slot >= 0 ? t.Hm.GetComponent<EnemyAi.Brain>() : null;
            t.Lead = brain != null && brain.Leading;
            var aim = t.Lead ? best + brain.LeadOffset : best;
            t.Proxy.transform.position = new Vector3(aim.x, aim.y, t.Hm.transform.position.z);
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var target = slot >= 0 && (slot != NetSession.MySlot || t.Lead) ? t.Proxy : hero.gameObject;
            foreach (var fsm in t.Fsms)
            {
                if (fsm == null) continue;
                foreach (var variable in fsm.FsmVariables.GameObjectVariables)
                    if (variable.Value == hero.gameObject || variable.Value == t.Proxy) variable.Value = target;
            }
        }

        public static GameObject For(HealthManager hm)
        {
            var t = Get(hm);
            if (t == null) return null;
            Update(t);
            return t.Slot >= 0 && (t.Slot != NetSession.MySlot || t.Lead) ? t.Proxy : null;
        }

        /// <summary>The player an enemy goes for (his slot) and where he is in this world; false when none can be gone for.</summary>
        public static bool Where(HealthManager hm, out Vector2 at, out int slot)
        {
            at = default;
            slot = -1;
            var t = Get(hm);
            if (t == null) return false;
            Update(t);
            at = t.At;
            slot = t.Slot;
            return slot >= 0;
        }

        public static int DevTarget(HealthManager hm) => targets.TryGetValue(hm, out var t) ? t.Slot : -1;

        public static void Tick()
        {
            if (!NetSession.Active || !Enemies.Enabled) return;
            foreach (var info in Items.EnemyStats.All)
                if (Eligible(info.Hm) && info.Hm.gameObject.activeInHierarchy) Get(info.Hm);
            foreach (var kv in new List<KeyValuePair<HealthManager, Target>>(targets))
            {
                if (!Eligible(kv.Key)) Remove(kv.Value);
                else Update(kv.Value);
            }
        }

        static void Remove(Target t)
        {
            var hero = HeroController.SilentInstance;
            foreach (var fsm in t.Fsms)
                if (fsm != null)
                    foreach (var variable in fsm.FsmVariables.GameObjectVariables)
                        if (variable.Value == t.Proxy) variable.Value = hero != null ? hero.gameObject : null;
            Object.Destroy(t.Proxy);
            targets.Remove(t.Hm);
        }
        public static void Reset() { foreach (var t in new List<Target>(targets.Values)) Remove(t); }
        public static void OwnerChanged(string scene)
        {
            foreach (var t in new List<Target>(targets.Values))
                if (t.Hm == null || t.Hm.gameObject.scene.name == scene) Remove(t);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GetHero), nameof(GetHero.OnEnter))]
        static void Hero(GetHero __instance)
        {
            if (!NetSession.Active || __instance.Fsm?.GameObject == null) return;
            var target = For(__instance.Fsm.GameObject.GetComponentInParent<HealthManager>());
            if (target != null) __instance.storeResult.Value = target;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Fsm), nameof(Fsm.GetOwnerDefaultTarget))]
        static void Resolve(Fsm __instance, ref GameObject __result)
        {
            var hero = HeroController.SilentInstance;
            if (!NetSession.Active || hero == null || __result != hero.gameObject || __instance.GameObject == null) return;
            var target = For(__instance.GameObject.GetComponentInParent<HealthManager>());
            if (target != null) __result = target;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(AlertRange), "get_IsHeroInRange")]
        static void Alert(AlertRange __instance, ref bool __result)
        {
            if (!NetSession.Active) return;
            var target = For(__instance.GetComponentInParent<HealthManager>());
            if (target == null) return;
            if (World.EnemyAggro.Wakes(__instance.gameObject.name) && World.EnemyAggro.IsAngry(__instance.GetComponentInParent<HealthManager>()))
            { __result = true; return; }
            __result = false;
            foreach (var collider in __instance.GetComponents<Collider2D>())
                if (collider.enabled && collider.OverlapPoint(target.transform.position)) { __result = true; break; }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LineOfSightDetector), "Update")]
        static void Sight(LineOfSightDetector __instance)
        {
            if (!NetSession.Active) return;
            var hm = __instance.GetComponentInParent<HealthManager>();
            var target = For(hm);
            if (target == null) return;
            bool inRange = ranges(__instance).Length == 0 || World.EnemyAggro.IsAngry(hm);
            foreach (var r in ranges(__instance)) if (r != null && r.IsHeroInRange) inRange = true;
            Vector2 delta = target.transform.position - __instance.transform.position;
            sight(__instance) = inRange && !Physics2D.Raycast(__instance.transform.position, delta.normalized, delta.magnitude, 256);
        }
    }
}
