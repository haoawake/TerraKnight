using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hollow Knight's props that the nail sets off (a stag station's bell, levers, vines, grass,
    /// lifeblood cocoons, infection bubbles, breakable poles and walls) mostly don't take hits
    /// through HitTaker: they watch for a trigger tagged "Nail Attack" (their own scripts, or an
    /// FSM's ReceivedDamage). A swing sends its stand-in slash out over the blade (MeleeHits), but
    /// Terraria hits everything that way: shortswords' stabs, spells, bullets, arrows, whips,
    /// yoyos, minions and sentries are all hits, and each should set such a prop off as the nail
    /// would. Every attack's hit test passes the props it covers here; each gets a stand-in slash
    /// for a moment where the hit lands, no more than once every quarter of a second (a minigun
    /// mustn't break a wall in a second that takes the nail a dozen blows).
    /// </summary>
    [HarmonyPatch]
    internal sealed class PropHits : MonoBehaviour
    {
        /// <summary>The game's own components that answer a "Nail Attack" trigger (TinkEffect, the recoil off hard things, left out).</summary>
        static readonly HashSet<string> listenerTypes = new HashSet<string>
        {
            "BossStatueLever", "BreakableInfectedVine", "BreakableObject", "BreakablePoleSimple", "BridgeLever", "FlipPlatform", "GlobControl",
            "GrassCut", "GrassWind", "HealthCocoon", "InfectedBurstLarge", "InfectedBurstSmall", "JellyEgg", "StalactiteControl", "VinePlatformCut",
        };

        const float Cooldown = 0.25f;
        const int PoolSize = 8;
        const float Size = 0.3f;

        static PropHits instance;
        readonly List<NailAttackProxy> pool = new List<NailAttackProxy>();
        readonly List<(NailAttackProxy proxy, Vector2 at, Collider2D on, Vector2 from)> live = new List<(NailAttackProxy, Vector2, Collider2D, Vector2)>();
        static readonly HashSet<GameObject> pulseObjects = new HashSet<GameObject>();
        static readonly Dictionary<int, bool> listens = new Dictionary<int, bool>();
        static readonly Dictionary<int, float> lastPulse = new Dictionary<int, float>();

        /// <summary>Dev: stand-in slashes sent out so far, and on what last.</summary>
        public static int DevPulses;
        public static string DevLast = "";

        static PropHits Get()
        {
            if (instance != null) return instance;
            var mod = TerraMod.Instance;
            if (mod == null) return null;
            var go = new GameObject("Terra Prop Hits");
            go.transform.SetParent(mod.transform, false);
            instance = go.AddComponent<PropHits>();
            for (int i = 0; i < PoolSize; i++)
            {
                var proxy = new NailAttackProxy(go.transform);
                instance.pool.Add(proxy);
                pulseObjects.Add(proxy.Object);
            }
            return instance;
        }

        /// <summary>
        /// Every collider an attack's hit test covered: the props among them that answer the nail
        /// are set off where the blow comes from (from, in world units).
        /// </summary>
        public static void Touch(List<Collider2D> covered, Vector2 from, float facing, int damage)
        {
            if (covered == null) return;
            for (int i = 0; i < covered.Count; i++) Pulse(covered[i], from, facing, damage);
        }

        public static void Touch(Collider2D[] covered, int count, Vector2 from, float facing, int damage)
        {
            for (int i = 0; i < count; i++) Pulse(covered[i], from, facing, damage);
        }

        /// <summary>
        /// One hit on a collider: a stand-in nail slash on it, if it is a prop that answers one. A blow on its own (a
        /// minion's, a sentry's, a spore's: <paramref name="onItsOwn"/>) doesn't work a lift (<see cref="LiftControl"/>).
        /// </summary>
        public static void Pulse(Collider2D col, Vector2 from, float facing, int damage, bool onItsOwn = false)
        {
            if (col == null || !col.enabled) return;
            if (Revek(col)) return;
            if (onItsOwn && LiftControl(col)) { DevLiftsSpared++; return; }
            var tink = PropTink(col);
            if (tink == null && !Listens(col)) return;
            int id = col.GetInstanceID();
            float now = Time.time;
            if (lastPulse.TryGetValue(id, out float last) && now - last < Cooldown) return;
            if (tink != null)
            {
                lastPulse[id] = now;
                Tink(tink, facing > 0f ? 0f : 180f);
                return;
            }
            var self = Get();
            if (self == null) return;
            NailAttackProxy free = null;
            foreach (var p in self.pool)
                if (p.Live == null && !self.live.Exists(l => l.proxy == p)) { free = p; break; }
            if (free == null) return;
            lastPulse[id] = now;
            if (lastPulse.Count > 512) lastPulse.Clear();
            Vector2 at = col.ClosestPoint(from);
            PropHitFx.Mark();
            free.Begin(facing, damage);
            free.Tick(at, SizeOn(col));
            self.live.Add((free, at, col, from));
            DevPulses++;
            DevLast = $"{col.gameObject.scene.name}/{col.name} @ {at:F2}";
        }

        void Update()
        {
            // Held out for the slash's time, then put away (NailAttackProxy.Tick does that).
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var (proxy, at, on, from) = live[i];
                // A thing on the move (a dung ball bouncing, a barrel falling): the slash goes with it, over its middle.
                if (on != null && on.enabled && Moving(on)) at = on.bounds.center;
                proxy.Tick(at, on != null ? SizeOn(on) : Vector2.one * Size);
                if (proxy.Live == null) live.RemoveAt(i);
            }
        }

        /// <summary>Dev: how a collider is taken: its tink, whether it answers the nail, a lift's control.</summary>
        public static string DevWhy(Collider2D col) => col == null ? "无" : $"{col.GetType().Name} 层{col.gameObject.layer} 触发器 {col.isTrigger} 叮 {PropTink(col) != null} 听骨钉 {Listens(col)} 电梯 {LiftControl(col)}";

        static readonly Dictionary<int, bool> liftControls = new Dictionary<int, bool>();
        public static int DevLiftsSpared;

        /// <summary>
        /// A lift's control (the user's, 2026-10-20: a minion called the City's lifts away): a lift's call lever ("Call
        /// Lever"), its car and the lever in it (a RuinsLift, an FSM named for a lift: "Lift Control", the great lift's
        /// "Lift Move"), on the collider's object or above it. Doors' levers aren't.
        /// </summary>
        public static bool LiftControl(Collider2D col)
        {
            int id = col.GetInstanceID();
            if (liftControls.TryGetValue(id, out bool known)) return known;
            bool yes = false;
            for (var t = col.transform; t != null && !yes; t = t.parent)
            {
                if (t.GetComponent<RuinsLift>() != null) yes = true;
                foreach (var f in t.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "Call Lever" || f.FsmName.IndexOf("Lift", System.StringComparison.OrdinalIgnoreCase) >= 0) { yes = true; break; }
            }
            if (liftControls.Count > 4096) liftControls.Clear();
            liftControls[id] = yes;
            return yes;
        }

        static bool Moving(Collider2D col)
        {
            var body = col.attachedRigidbody;
            return body != null && body.bodyType == RigidbodyType2D.Dynamic && body.velocity.sqrMagnitude > 0.25f;
        }

        /// <summary>The slash's size on a prop: a point's, or most of a moving thing it must not slip off.</summary>
        static Vector2 SizeOn(Collider2D col)
        {
            if (!Moving(col)) return Vector2.one * Size;
            var b = col.bounds.size;
            return Vector2.one * Mathf.Clamp(Mathf.Min(b.x, b.y) * 0.8f, Size, 2f);
        }

        /// <summary>A prop that answers the nail's slash: one of the game's listening components, or an FSM that does, on the collider's own object or its body's.</summary>
        static bool Listens(Collider2D col)
        {
            int id = col.GetInstanceID();
            if (listens.TryGetValue(id, out bool known)) return known;
            bool yes = false;
            if (col.GetComponentInParent<HealthManager>() == null)
            {
                yes = On(col.gameObject);
                var body = col.attachedRigidbody;
                if (!yes && body != null && body.gameObject != col.gameObject) yes = On(body.gameObject);
            }
            if (listens.Count > 4096) listens.Clear();
            listens[id] = yes;
            return yes;
        }

        static bool On(GameObject go)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (listenerTypes.Contains(mb.GetType().Name)) return true;
                if (mb is PlayMakerFSM fsm && FsmListens(fsm)) return true;
            }
            return false;
        }

        static bool FsmListens(PlayMakerFSM fsm)
        {
            if (fsm.FsmStates == null) return false;
            foreach (var state in fsm.FsmStates)
                foreach (var a in state.Actions)
                {
                    if (a is HutongGames.PlayMaker.Actions.ReceivedDamage) return true;
                    if (a is HutongGames.PlayMaker.Actions.Trigger2dEvent te && te.collideTag != null && te.collideTag.Value == "Nail Attack") return true;
                    // False Knight's falling barrels: the tag and the slash's layer.
                    if (a is HutongGames.PlayMaker.Actions.Trigger2dEventLayer tl && tl.collideTag != null && tl.collideTag.Value == "Nail Attack") return true;
                }
            return false;
        }

        /// <summary>
        /// What a boss sends out that the nail knocks away or breaks (Hornet Sentinel's barbs, False Knight's falling
        /// barrels, Dung Defender's dung balls, Hive Knight's droppers): a prop that answers the nail and hurts the
        /// Knight. A minion leaves the world's props alone but hits these as any weapon does.
        /// </summary>
        public static bool Hostile(Collider2D col)
        {
            if (col == null || !col.enabled) return false;
            if (PropTink(col) == null && !Listens(col)) return false;
            if (col.GetComponent<DamageHero>() != null) return true;
            var body = col.attachedRigidbody;
            return body != null && body.GetComponent<DamageHero>() != null;
        }

        /// <summary>A swing over a collider: a prop's tink told it was struck (facing: the swing's side, + right).</summary>
        public static void TinkStruck(Collider2D col, float facing)
        {
            if (col == null || !col.enabled) return;
            var tink = PropTink(col);
            if (tink != null) Tink(tink, facing > 0f ? 0f : 180f);
        }

        static readonly AccessTools.FieldRef<TinkEffect, float> nextTink = AccessTools.FieldRefAccess<TinkEffect, float>("nextTinkTime");

        /// <summary>
        /// A TinkEffect that tells its FSM it was struck (a Hornet Sentinel's barb breaks on its BLOCKED HIT), on the
        /// collider's object or its body's; not one on an enemy's armour (that tink is a blow it parries).
        /// </summary>
        static TinkEffect PropTink(Collider2D col)
        {
            var tink = col.GetComponent<TinkEffect>();
            var body = col.attachedRigidbody;
            if (tink == null && body != null) tink = body.GetComponent<TinkEffect>();
            if (tink == null || tink.fsm == null || !(tink.sendFSMEvent || tink.sendDirectionalFSMEvents)) return null;
            return tink.GetComponentInParent<HealthManager>() == null ? tink : null;
        }

        /// <summary>
        /// The struck prop's part of TinkEffect.OnTriggerEnter2D, its FSM's events (no more often than its repeat
        /// delay): not the Knight's recoil, the camera's shake or the clank.
        /// </summary>
        static void Tink(TinkEffect tink, float direction)
        {
            if (Time.time < nextTink(tink)) return;
            nextTink(tink) = Time.time + 0.25f;
            if (tink.sendDirectionalFSMEvents)
            {
                int dir = DirectionUtils.GetCardinalDirection(direction);
                tink.fsm.SendEvent(dir == 0 ? "TINK RIGHT" : dir == 1 ? "TINK UP" : dir == 2 ? "TINK LEFT" : "TINK DOWN");
            }
            if (tink.sendFSMEvent) tink.fsm.SendEvent(tink.FSMEvent);
            DevTinkEvents++;
            DevLast = $"{tink.gameObject.scene.name}/{tink.name} 的 {tink.FSMEvent}";
        }

        public static int DevTinkEvents;

        /// <summary>
        /// Revek, the Spirits' Glade's guard, has no health: only the nail's parry of his slash
        /// (nail_clash_tink, then TAKE DAMAGE) or the Dream Nail disperses him for a while. Terraria
        /// has no parry: any hit of the player's on him or his blade while he waits to strike, winds up
        /// or slashes (his Control's states that take TAKE DAMAGE) counts as one. True for any of his.
        /// </summary>
        public static bool Revek(Collider2D col)
        {
            var t = col.transform;
            Transform root = null;
            for (int i = 0; i < 3 && t != null && root == null; i++, t = t.parent)
                if (t.name == "Ghost Battle Revek") root = t;
            if (root == null) return false;
            var control = PlayMakerFSM.FindFsmOnGameObject(root.gameObject, "Control");
            string st = control != null ? control.ActiveStateName : "";
            if (st == "Slash Idle" || st == "Slash Antic" || st == "Slash")
            {
                FSMUtility.SendEventToGameObject(root.gameObject, "TAKE DAMAGE");
                DevRevek++;
                Plugin.Log.LogInfo($"[瑞维克] 在 {st} 被打中：打散");
            }
            return true;
        }

        public static int DevRevek;

        /// <summary>
        /// A stand-in slash (a pulse's, a weapon swing's, a blast's) is a hit landing on a prop, not
        /// the Knight's blade at the wall: no recoil off hard things for it (TinkEffect pushes the
        /// Knight back off spikes, armour and shells it hits), nor its clank. Terraria's weapons
        /// never push the player back for what they hit. A prop the tink tells it was struck (a
        /// barb) still hears it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TinkEffect), "OnTriggerEnter2D")]
        static bool NoTinkFromPulses(TinkEffect __instance, Collider2D collision)
        {
            if (collision == null) return true;
            var go = collision.gameObject;
            if (!pulseObjects.Contains(go) && !NailAttackProxy.IsAnyProxy(go)) return true;
            DevTinksStopped++;
            if (go.CompareTag("Nail Attack") && __instance.fsm != null && (__instance.sendFSMEvent || __instance.sendDirectionalFSMEvents)
                && __instance.GetComponentInParent<HealthManager>() == null)
            {
                var slash = PlayMakerFSM.FindFsmOnGameObject(go, "damages_enemy");
                Tink(__instance, slash != null ? slash.FsmVariables.FindFsmFloat("direction").Value : 0f);
            }
            return false;
        }

        public static int DevTinksStopped;
    }
}
