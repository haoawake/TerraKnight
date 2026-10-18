using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// The game's enemy blockers: solid boxes on a layer only enemies collide with, laid round
    /// secret passages and the like to keep the enemies outside out. Terraria has no wall the
    /// player walks through and enemies can't: a crawler turns round only at terrain
    /// (Crawler.CheckRayLocal looks at the terrain layer alone), so it pushes at a blocker for
    /// good, and a flyer chasing the player stops in mid-air at one (Cliffs_02's hidden passage
    /// under the one-way wall). They are switched off as each scene loads, seamless neighbours
    /// too. Those standing at one of the scene's edge gates keep enemies from walking off the
    /// scene into nothing, and so does the solid box every gate carries on the same layer (its
    /// child "EnemyDetector", which no script reads); where the seamless world has joined that
    /// gate to the scene beyond, there is ground on the other side, and Terraria's enemies go
    /// wherever the world goes: both are off while the gate is joined, and on again when it isn't.
    /// </summary>
    internal static class EnemyBlockers
    {
        public const int Layer = 15;
        /// <summary>How near a gate's trigger a blocker counts as guarding it, in units.</summary>
        public const float GateReach = 3f;

        public static bool Enabled = true;

        /// <summary>What was decided for each blocker seen (by collider instance id), for the dev reports.</summary>
        static readonly Dictionary<int, string> decided = new Dictionary<int, string>();
        static readonly List<Collider2D> switchedOff = new List<Collider2D>();

        /// <summary>A blocker at edge gates: off while every one of them is joined to the scene beyond.</summary>
        sealed class Guard
        {
            public Collider2D Blocker;
            public List<TransitionPoint> Gates;
            public string Names;
        }
        static readonly List<Guard> guards = new List<Guard>();

        public static void Init() => USM.sceneLoaded += (scene, mode) => Apply(scene);

        /// <summary>
        /// A solid "Enemy Blocker" collider. The layer has other things on it: each gate's
        /// EnemyDetector (a gate guard, see <see cref="Apply"/>), the spikes' Enemy Msg box (enemies
        /// touching spikes), which must stay.
        /// </summary>
        public static bool IsBlocker(Collider2D c)
        {
            if (c == null || c.isTrigger || c.gameObject.layer != Layer || c.GetComponentInParent<HealthManager>() != null) return false;
            // "Enemy Blocker", "enemy blockers", "EnemyBlocker", "Enemy Block", "Enemy Blockler" (Ruins1_23), "Blocker (2)" (Crossroads_04).
            string n = c.name.Replace(" ", "").ToLowerInvariant();
            return n.Contains("enemyblock") || n.StartsWith("blocker");
        }

        /// <summary>Boss and dream arenas keep theirs: a boss or its brood mustn't leave the fight.</summary>
        static bool KeepsBlockers(string scene) =>
            scene.Contains("_boss") || scene.StartsWith("Dream_") || scene.StartsWith("GG_") || scene.StartsWith("Room_Colosseum") || scene.StartsWith("Grimm_");

        /// <summary>A gate on the scene's edge (left, right, top, bottom), which the seamless world can join; not a door.</summary>
        static bool EdgeGate(TransitionPoint tp)
        {
            if (tp.isADoor) return false;
            string n = tp.name;
            return n.StartsWith("left") || n.StartsWith("right") || n.StartsWith("top") || n.StartsWith("bot");
        }

        public static void Apply(Scene scene)
        {
            if (!Enabled || !scene.IsValid() || !scene.isLoaded || KeepsBlockers(scene.name)) return;
            var gates = new List<(TransitionPoint tp, Bounds bounds)>();
            var blockers = new List<Collider2D>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                {
                    var gc = tp.GetComponent<Collider2D>();
                    if (gc != null) gates.Add((tp, gc.bounds));
                }
                foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                    if (IsBlocker(c)) blockers.Add(c);
            }
            // Each edge gate's own enemy box.
            foreach (var (tp, _) in gates)
            {
                if (!EdgeGate(tp)) continue;
                foreach (var c in tp.GetComponentsInChildren<Collider2D>(true))
                {
                    if (c.isTrigger || c.gameObject.layer != Layer || c.gameObject == tp.gameObject || !c.enabled) continue;
                    guards.Add(new Guard { Blocker = c, Gates = new List<TransitionPoint> { tp }, Names = tp.name });
                    decided[c.GetInstanceID()] = "门 " + tp.name + " 自带的挡怪板：门接上隔壁场景时关掉";
                }
            }
            foreach (var c in blockers)
            {
                // Inactive objects report empty bounds: their box from the transform instead.
                var b = BoundsOf(c);
                string door = null;
                List<TransitionPoint> edge = null;
                foreach (var (tp, gb) in gates)
                {
                    var near = gb;
                    near.Expand(GateReach * 2f);
                    if (!near.Intersects(b)) continue;
                    if (EdgeGate(tp)) (edge ??= new List<TransitionPoint>()).Add(tp);
                    else door ??= tp.name;
                }
                if (edge != null)
                {
                    // Only one that was on to begin with is ever switched on again.
                    if (!c.enabled)
                    {
                        decided[c.GetInstanceID()] = "本来就关着";
                        continue;
                    }
                    var names = string.Join("、", edge.ConvertAll(g => g.name));
                    guards.Add(new Guard { Blocker = c, Gates = edge, Names = names });
                    decided[c.GetInstanceID()] = "守着门 " + names + "：门接上隔壁场景时关掉";
                    continue;
                }
                if (door != null)
                {
                    decided[c.GetInstanceID()] = "留着（守着门 " + door + "）";
                    continue;
                }
                decided[c.GetInstanceID()] = c.enabled ? "关掉" : "本来就关着";
                if (!c.enabled) continue;
                c.enabled = false;
                switchedOff.Add(c);
            }
            Refresh();
        }

        /// <summary>
        /// Every gate blocker on or off by its gates: off while each is joined to the scene beyond
        /// (LiveWorld), on otherwise. Called whenever the seamless world joins or drops gates.
        /// </summary>
        public static void Refresh()
        {
            guards.RemoveAll(g => g.Blocker == null || g.Gates.TrueForAll(t => t == null));
            var world = LiveWorld.Instance;
            foreach (var g in guards)
            {
                bool open = world != null && LiveWorld.Enabled;
                if (open)
                    foreach (var tp in g.Gates)
                        if (tp != null && !world.IsJoined(tp)) { open = false; break; }
                if (g.Blocker.enabled == !open) continue;
                g.Blocker.enabled = !open;
                Plugin.Log.LogInfo($"[挡怪] {g.Blocker.gameObject.scene.name} 门 {g.Names} 旁的挡怪墙{(open ? "关掉（门接上了隔壁场景）" : "放回（门没接上）")}");
            }
            HoldStranded();
        }

        /// <summary>Dev: whether a gate blocker is on now, and the gates it stands at.</summary>
        public static string DevGuards(string scene)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var g in guards)
                if (g.Blocker != null && (scene == null || g.Blocker.gameObject.scene.name == scene))
                    sb.Append($" {g.Blocker.gameObject.scene.name}/{g.Blocker.name}@{g.Names}={(g.Blocker.enabled ? "开" : "关")}");
            return sb.Length == 0 ? " 无" : sb.ToString();
        }

        /// <summary>Dev: the gate blockers of a scene (null: any) switched off now (their gates joined), with the first of their gates.</summary>
        public static List<(Collider2D blocker, TransitionPoint gate)> DevOpen(string scene)
        {
            var list = new List<(Collider2D, TransitionPoint)>();
            foreach (var g in guards)
                if (g.Blocker != null && !g.Blocker.enabled && (scene == null || g.Blocker.gameObject.scene.name == scene) && g.Gates.Count > 0 && g.Gates[0] != null)
                    list.Add((g.Blocker, g.Gates[0]));
            return list;
        }

        public static Bounds BoundsOf(Collider2D c)
        {
            if (c.enabled && c.gameObject.activeInHierarchy) return c.bounds;
            if (c is BoxCollider2D box)
            {
                var t = c.transform;
                var center = t.TransformPoint(box.offset);
                var size = Vector3.Scale(box.size, t.lossyScale);
                return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f));
            }
            return new Bounds(c.transform.position, Vector3.one);
        }

        public static string Describe(Collider2D c) => c != null && decided.TryGetValue(c.GetInstanceID(), out var r) ? r : "没处理";

        /// <summary>Dev: the switched-off blockers on again (true) or off again (false).</summary>
        public static int DevSet(bool on)
        {
            int n = 0;
            switchedOff.RemoveAll(c => c == null);
            foreach (var c in switchedOff)
            {
                c.enabled = on;
                n++;
            }
            return n;
        }

        // --- Enemies left outside the world. ---

        static readonly HashSet<Rigidbody2D> held = new HashSet<Rigidbody2D>();
        public static int DevHeld => held.Count;

        /// <summary>
        /// With the gate blockers off, an enemy can follow the world into the scene next door; when
        /// that scene is unloaded behind the player (its own stays), it stands on nothing and would
        /// fall for ever. Terraria's NPCs out where nobody is wait (and despawn): such an enemy is
        /// held where it is, its body out of the simulation, and goes on when the scene it stood in
        /// is loaded again. Looked at whenever scenes are joined or dropped, which is the only time
        /// ground can vanish from under one (an enemy merely leaping above its scene isn't held).
        /// </summary>
        static void HoldStranded()
        {
            held.RemoveWhere(rb => rb == null);
            foreach (var rb in new List<Rigidbody2D>(held))
            {
                if (!InWorld(rb.position)) continue;
                rb.simulated = true;
                held.Remove(rb);
                Plugin.Log.LogInfo($"[挡怪] {rb.gameObject.scene.name} 的 {rb.name} 脚下的场景又载入了，接着动");
            }
            var world = LiveWorld.Instance;
            if (world == null || !LiveWorld.Enabled) return;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead || Net.Enemies.IsPuppet(hm)) continue;
                var rb = hm.GetComponent<Rigidbody2D>();
                // Only one that would fall, of a scene of the world (not one loaded on its own for a dev survey).
                if (rb == null || !rb.simulated || rb.bodyType != RigidbodyType2D.Dynamic || rb.gravityScale <= 0f || held.Contains(rb)) continue;
                if (!world.IsLoaded(hm.gameObject.scene.name) || InWorld(rb.position)) continue;
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
                held.Add(rb);
                Plugin.Log.LogInfo($"[挡怪] {hm.gameObject.scene.name} 的 {hm.name} 脚下的场景卸载了，先停在 {rb.position:F1}");
            }
        }

        static bool InWorld(Vector2 p)
        {
            var cell = CellGrid.WorldToCell(p);
            return TerraWorld.RegionAt(cell.x, cell.y) != null;
        }
    }
}
