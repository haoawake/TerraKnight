using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Spikes and thorns grow out of the terrain, and digging that terrain out cuts their art away
    /// with it (the dug cells, and the open cells next to them that the terrain's edge hangs over).
    /// Where their art has been cut away they no longer hurt: a spike hit counts only if the hero
    /// touches the hazard somewhere it can still be seen.
    /// </summary>
    [HarmonyPatch]
    internal static class DugHazards
    {
        /// <summary>The game's hazard type for spikes and thorns (2 = SPIKES; acid 3, lava 4, pits 5).</summary>
        const int Spikes = 2;
        /// <summary>Sampling step over the hero's overlap with a hazard: a quarter of a cell.</summary>
        const float Step = CellGrid.CellSize / 4f;

        static Collider2D heroBox;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static bool BeforeTakeDamage(HeroController __instance, GameObject go, int hazardType)
        {
            if (hazardType != Spikes || go == null || TerrainSystem.Current == null) return true;
            if (heroBox == null)
            {
                var box = __instance.GetComponentInChildren<HeroBox>(true);
                heroBox = box != null ? box.GetComponent<Collider2D>() : __instance.GetComponent<Collider2D>();
            }
            if (heroBox == null) return true;
            bool dugAway = DugAway(go, heroBox.bounds);
            // The hero's box reports the touch every frame; note it once per hazard.
            if (dugAway && go != lastLogged)
            {
                lastLogged = go;
                Plugin.Log.LogInfo($"[挖掉的尖刺] {go.name}（{go.scene.name}）碰到的部分都已挖掉，不受伤");
            }
            return !dugAway;
        }

        static GameObject lastLogged;

        /// <summary>
        /// Every point where the hero's box overlaps the hazard's colliders lies in a dug cell.
        /// False when the overlap can't be found (touching at an edge): the hit then stands.
        /// </summary>
        static bool DugAway(GameObject hazard, Bounds hero)
        {
            bool any = false;
            foreach (var col in hazard.GetComponents<Collider2D>())
            {
                if (col == null || !col.enabled) continue;
                var b = col.bounds;
                float x0 = Mathf.Max(b.min.x, hero.min.x), x1 = Mathf.Min(b.max.x, hero.max.x);
                float y0 = Mathf.Max(b.min.y, hero.min.y), y1 = Mathf.Min(b.max.y, hero.max.y);
                if (x1 < x0 || y1 < y0) continue;
                int nx = Mathf.Clamp(Mathf.CeilToInt((x1 - x0) / Step), 1, 40);
                int ny = Mathf.Clamp(Mathf.CeilToInt((y1 - y0) / Step), 1, 40);
                for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= ny; j++)
                {
                    var p = new Vector2(Mathf.Lerp(x0, x1, i / (float)nx), Mathf.Lerp(y0, y1, j / (float)ny));
                    if (!col.OverlapPoint(p)) continue;
                    any = true;
                    var c = CellGrid.WorldToCell(p);
                    if (!TerraWorld.ArtCutAt(c.x, c.y)) return false;
                }
            }
            return any;
        }
    }
}
