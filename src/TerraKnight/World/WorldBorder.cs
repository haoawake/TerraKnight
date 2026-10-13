using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The edge of the world, as Terraria's invisible walls at the map's border: where no loaded scene
    /// carries on past the one the player is in (Dirtmouth's sky, a cliff's open side, a room's far
    /// side with nothing beyond), the player stops at its edge as at a wall instead of flying off into
    /// nothing. A gate there (a door into a scene not loaded, a transition the seamless world leaves
    /// to the game) still lets the player through to it.
    /// </summary>
    [HarmonyPatch]
    internal static class WorldBorder
    {
        public static bool Enabled = true;
        public static int DevStops;
        public static string DevLast = "";

        const float Inset = 0.02f;
        /// <summary>How near a gate's trigger the edge is open (units).</summary>
        const float GateReach = 1.5f;

        static readonly List<Bounds> gates = new List<Bounds>();
        static int gatesForScenes = -1;
        static float gatesAt = -10f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Enabled || TerraMod.Instance == null || TerrainSystem.Current == null || ___rb2d == null) return;
            var gm = GameManager.UnsafeInstance;
            var cs = __instance.cState;
            if (gm == null || gm.IsInSceneTransition || cs.transitioning || cs.dead || cs.hazardDeath || cs.hazardRespawning) return;
            var col = __instance.GetComponent<BoxCollider2D>();
            if (col == null) return;
            var b = col.bounds;
            var centre = CellGrid.WorldToCell(b.center);
            var home = TerraWorld.RegionAt(centre.x, centre.y);
            // Outside every scene already (a cutscene, a teleport on its way): not ours to hold.
            if (home == null) return;
            RefreshGates();
            const float c = CellGrid.CellSize;
            var cr = home.CellRect;
            float left = cr.xMin * c, right = cr.xMax * c, bottom = cr.yMin * c, top = cr.yMax * c;
            float dt = Time.fixedDeltaTime;
            var v = ___rb2d.linearVelocity;
            Vector2 shift = Vector2.zero;
            string side = null;
            // Each side by its middle, where it will be after this step.
            if (Out(new Vector2(b.center.x, b.max.y + Mathf.Max(0f, v.y) * dt)) && b.max.y + Mathf.Max(0f, v.y) * dt > top - Inset)
            {
                shift.y = Mathf.Min(0f, top - Inset - b.max.y);
                if (v.y > 0f) v.y = 0f;
                side = "上";
            }
            else if (Out(new Vector2(b.center.x, b.min.y + Mathf.Min(0f, v.y) * dt)) && b.min.y + Mathf.Min(0f, v.y) * dt < bottom + Inset)
            {
                shift.y = Mathf.Max(0f, bottom + Inset - b.min.y);
                if (v.y < 0f) v.y = 0f;
                side = "下";
            }
            if (Out(new Vector2(b.max.x + Mathf.Max(0f, v.x) * dt, b.center.y)) && b.max.x + Mathf.Max(0f, v.x) * dt > right - Inset)
            {
                shift.x = Mathf.Min(0f, right - Inset - b.max.x);
                if (v.x > 0f) v.x = 0f;
                side = (side ?? "") + "右";
            }
            else if (Out(new Vector2(b.min.x + Mathf.Min(0f, v.x) * dt, b.center.y)) && b.min.x + Mathf.Min(0f, v.x) * dt < left + Inset)
            {
                shift.x = Mathf.Max(0f, left + Inset - b.min.x);
                if (v.x < 0f) v.x = 0f;
                side = (side ?? "") + "左";
            }
            if (side == null) return;
            ___rb2d.linearVelocity = v;
            if (shift != Vector2.zero)
            {
                var p = __instance.transform.position + (Vector3)shift;
                __instance.transform.position = p;
                ___rb2d.position = p;
            }
            DevStops++;
            DevLast = $"{home.SceneName} {side}";
        }

        /// <summary>A point in no loaded scene, and not by a gate.</summary>
        static bool Out(Vector2 p)
        {
            var cell = CellGrid.WorldToCell(p);
            if (TerraWorld.RegionAt(cell.x, cell.y) != null) return false;
            foreach (var g in gates)
            {
                var e = g;
                e.Expand(GateReach * 2f);
                if (e.Contains(new Vector3(p.x, p.y, e.center.z))) return false;
            }
            return true;
        }

        /// <summary>The loaded scenes' gate triggers, looked up again when scenes come and go (and now and then: some are switched on late).</summary>
        static void RefreshGates()
        {
            int loaded = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (loaded == gatesForScenes && Time.time - gatesAt < 2f) return;
            gatesForScenes = loaded;
            gatesAt = Time.time;
            gates.Clear();
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                var tc = tp.GetComponent<Collider2D>();
                gates.Add(tc != null ? tc.bounds : new Bounds(tp.transform.position, Vector3.one));
            }
        }
    }
}
