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
    /// too, except those standing at one of the scene's gates, which keep enemies from walking
    /// off into the next scene.
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

        public static void Init() => USM.sceneLoaded += (scene, mode) => Apply(scene);

        /// <summary>
        /// A solid "Enemy Blocker" collider. The layer has other things on it that must stay: each
        /// gate's EnemyDetector, the spikes' Enemy Msg box (enemies touching spikes).
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

        public static void Apply(Scene scene)
        {
            if (!Enabled || !scene.IsValid() || !scene.isLoaded || KeepsBlockers(scene.name)) return;
            var gates = new List<(string name, Bounds bounds)>();
            var blockers = new List<Collider2D>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                {
                    var gc = tp.GetComponent<Collider2D>();
                    if (gc != null) gates.Add((tp.name, gc.bounds));
                }
                foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                    if (IsBlocker(c)) blockers.Add(c);
            }
            foreach (var c in blockers)
            {
                // Inactive objects report empty bounds: their box from the transform instead.
                var b = BoundsOf(c);
                string guard = null;
                foreach (var (name, gb) in gates)
                {
                    var near = gb;
                    near.Expand(GateReach * 2f);
                    if (near.Intersects(b)) { guard = name; break; }
                }
                if (guard != null)
                {
                    decided[c.GetInstanceID()] = "留着（守着门 " + guard + "）";
                    continue;
                }
                decided[c.GetInstanceID()] = c.enabled ? "关掉" : "本来就关着";
                if (!c.enabled) continue;
                c.enabled = false;
                switchedOff.Add(c);
            }
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
    }
}
