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
        readonly List<(NailAttackProxy proxy, Vector2 at)> live = new List<(NailAttackProxy, Vector2)>();
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

        /// <summary>One hit on a collider: a stand-in nail slash on it, if it is a prop that answers one.</summary>
        public static void Pulse(Collider2D col, Vector2 from, float facing, int damage)
        {
            if (col == null || !col.enabled || !Listens(col)) return;
            int id = col.GetInstanceID();
            float now = Time.time;
            if (lastPulse.TryGetValue(id, out float last) && now - last < Cooldown) return;
            var self = Get();
            if (self == null) return;
            NailAttackProxy free = null;
            foreach (var p in self.pool)
                if (p.Live == null && !self.live.Exists(l => l.proxy == p)) { free = p; break; }
            if (free == null) return;
            lastPulse[id] = now;
            if (lastPulse.Count > 512) lastPulse.Clear();
            Vector2 at = col.ClosestPoint(from);
            free.Begin(facing, damage);
            free.Tick(at, Vector2.one * Size);
            self.live.Add((free, at));
            DevPulses++;
            DevLast = $"{col.gameObject.scene.name}/{col.name} @ {at:F2}";
        }

        void Update()
        {
            // Held out for the slash's time, then put away (NailAttackProxy.Tick does that).
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var (proxy, at) = live[i];
                proxy.Tick(at, Vector2.one * Size);
                if (proxy.Live == null) live.RemoveAt(i);
            }
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
                }
            return false;
        }

        /// <summary>A stand-in slash is a hit landing on a prop, not the Knight's blade at the wall: no recoil off hard things for it.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TinkEffect), "OnTriggerEnter2D")]
        static bool NoTinkFromPulses(Collider2D collision) => collision == null || !pulseObjects.Contains(collision.gameObject);
    }
}
