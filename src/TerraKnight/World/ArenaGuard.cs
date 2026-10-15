using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A boss is fought from inside its arena: Terraria's blows on a boss, or on an enemy of a room's
    /// battle (its HealthManager given a battle scene), count only with the Knight on the same side
    /// of the room's battle gates as it. Standing outside the gates while minions, sentries or shots
    /// went in did the whole fight before (and woke it without the fight's start shutting him in).
    /// Such a blow does nothing at all (no damage, no TAKE DAMAGE to wake it) and says so.
    /// Nor does a blow reach an enemy the game keeps out of its room until it is wanted: the Gruz
    /// Mother's brood waits under the floor of her arena, out of the map, for her battle to release
    /// it; the Zenith's swords, which go through walls, killed it there, and her battle, counting it
    /// down from its start, never ended and never opened its gates.
    /// </summary>
    internal static class ArenaGuard
    {
        public static bool Enabled = true;
        public static int DevBlocked;

        static readonly AccessTools.FieldRef<HealthManager, GameObject> battleOf = AccessTools.FieldRefAccess<HealthManager, GameObject>("battleScene");

        struct Gate
        {
            public string Scene;
            public float X, Bottom, Top;
        }

        static readonly List<Gate> gates = new List<Gate>();
        static int gatesForScenes = -1;
        static float gatesAt = -10f;

        /// <summary>The loaded scenes' battle gates (their FSM "BG Control"), where they stand and how tall they are.</summary>
        static void Refresh()
        {
            int loaded = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (loaded == gatesForScenes && Time.time - gatesAt < 2f) return;
            gatesForScenes = loaded;
            gatesAt = Time.time;
            gates.Clear();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "BG Control") continue;
                // An open gate has its collider off (no bounds then): its picture, or a door's height about its foot.
                Bounds b = default;
                var col = f.GetComponent<Collider2D>();
                var ren = f.GetComponent<Renderer>();
                if (col != null && col.enabled) b = col.bounds;
                else if (ren != null) b = ren.bounds;
                float x = f.transform.position.x;
                float bottom = b.size.y > 0.5f ? b.min.y : f.transform.position.y - 3f;
                float top = b.size.y > 0.5f ? b.max.y : f.transform.position.y + 3f;
                gates.Add(new Gate { Scene = Room(f.gameObject.scene.name), X = x, Bottom = bottom, Top = top });
            }
        }

        static string Room(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>An enemy kept out of every loaded room's map (more than a little past its edges): nothing reaches it there.</summary>
        public static bool OffMap(HealthManager hm)
        {
            if (!Enabled || hm == null) return false;
            var c = hm.GetComponent<Collider2D>();
            Vector2 at = c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
            const float margin = 3f;
            foreach (var p in new[] { at, at + new Vector2(0f, margin), at - new Vector2(0f, margin), at + new Vector2(margin, 0f), at - new Vector2(margin, 0f) })
            {
                var cell = CellGrid.WorldToCell(p);
                if (TerraWorld.RegionAt(cell.x, cell.y) != null) return false;
            }
            return TerrainSystem.Current != null;
        }

        /// <summary>Whether a blow on this enemy is from outside its fight: a boss or a battle's enemy, a battle gate of its room between it and the Knight.</summary>
        public static bool Outside(HealthManager hm)
        {
            if (!Enabled || hm == null) return false;
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            var info = Items.EnemyStats.Of(hm);
            if (!(info != null && info.Boss) && battleOf(hm) == null) return false;
            Refresh();
            if (gates.Count == 0) return false;
            string room = Room(hm.gameObject.scene.name);
            Vector2 a = hero.GetComponent<BoxCollider2D>().bounds.center, b = hm.transform.position;
            float lo = Mathf.Min(a.x, b.x), hi = Mathf.Max(a.x, b.x);
            float low = Mathf.Min(a.y, b.y), high = Mathf.Max(a.y, b.y);
            foreach (var g in gates)
            {
                if (g.Scene != room) continue;
                if (g.X <= lo + 0.3f || g.X >= hi - 0.3f) continue;
                // Across the gate's doorway: the way from the Knight to it passes its height.
                float y = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, g.X));
                if (y < g.Bottom - 1.5f || y > g.Top + 1.5f) continue;
                return true;
            }
            return false;
        }

        static float nextNotice;

        /// <summary>A blow from outside the fight: nothing done, and the refusal over it now and then.</summary>
        public static bool Refuse(HealthManager hm)
        {
            if (OffMap(hm)) { DevBlocked++; return true; }
            if (!Outside(hm)) return false;
            DevBlocked++;
            // In the middle of the screen, not over the boss (out of the Knight's arena, maybe out of sight).
            if (Time.unscaledTime >= nextNotice)
            {
                nextNotice = Time.unscaledTime + 1f;
                UI.CenterNotice.Show(Lang.T("先进入首领的战斗区域"));
            }
            return true;
        }
    }
}
