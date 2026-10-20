using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Spikes and thorns grow out of the terrain, and digging that terrain out cuts their art away
    /// with it (the dug cells, and the open cells next to them that the terrain's edge hangs over).
    /// Where their art has been cut away they no longer hurt: a spike hit counts only if the hero
    /// touches the hazard in a spike cell still standing.
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
            bool dugAway = DugAway(go, heroBox.bounds, out var uncut, out bool anyCut);
            // The hero's box reports the touch every frame; note it once per hazard.
            if (dugAway && go != lastLogged)
            {
                lastLogged = go;
                Plugin.Log.LogInfo($"[挖掉的尖刺] {go.name}（{go.scene.name}）碰到的部分都已挖掉，不受伤");
            }
            else if (!dugAway && anyCut && Time.unscaledTime >= nextHurtLog)
            {
                // Hurt beside a dug-out stretch: the cell still standing, for a report of thorns that "were mined".
                nextHurtLog = Time.unscaledTime + 2f;
                Plugin.Log.LogInfo($"[挖掉的尖刺] {go.name}（{go.scene.name}）旁边挖过，但碰到了没挖的格 {uncut}，照常受伤");
            }
            return !dugAway;
        }

        static GameObject lastLogged;
        static float nextHurtLog;

        static readonly Collider2D[] probe = new Collider2D[16];

        /// <summary>A cell of open air where a spike or thorn hazard stands (a DamageHero of hazard type 2 over it), not dug yet.</summary>
        public static bool IsSpikeCell(Vector2Int cell)
        {
            if (TerraWorld.Get(cell.x, cell.y) != CellKind.Empty || TerraWorld.IsDug(cell.x, cell.y) || !TerraWorld.InWorld(cell.x, cell.y)) return false;
            if (BossRooms.Locked(cell.x, cell.y)) return false;
            return HazardOver(cell);
        }

        /// <summary>A spike or thorn hazard's collider reaches into the cell (its middle 80%), dug or not.</summary>
        public static bool HazardOver(Vector2Int cell)
        {
            var c = CellGrid.CellCenter(cell.x, cell.y);
            int n = Physics2D.OverlapBoxNonAlloc(c, Vector2.one * CellGrid.CellSize * 0.8f, 0f, probe);
            for (int k = 0; k < n; k++)
            {
                var col = probe[k];
                if (col == null || !col.enabled) continue;
                var dh = col.GetComponent<DamageHero>() ?? col.GetComponentInParent<DamageHero>();
                if (dh != null && dh.hazardType == Spikes) return true;
            }
            return false;
        }

        /// <summary>
        /// A spike's cell mined out: its art cut away and harmless there, kept in the save (CellGrid.CutOpenArt); the tips
        /// of its picture over the open cells beside it go too (<see cref="TidyArt"/>).
        /// </summary>
        public static void CutSpike(Vector2Int cell)
        {
            CutOne(cell);
            TidyArt(cell, cell);
        }

        static void CutOne(Vector2Int cell)
        {
            var r = TerraWorld.RegionAt(cell.x, cell.y);
            if (r == null) return;
            r.Grid.CutOpenArt(cell.x - r.CellOffset.x, cell.y - r.CellOffset.y);
            DevSpikesCut++;
        }

        public static int DevSpikesCut, DevArtTidied;

        /// <summary>Ground dug out from under spikes: theirs within two cells go with it, none left hanging in the air.</summary>
        public static void CutLooseSpikes(Vector2Int dug)
        {
            CutLoose(dug);
            TidyArt(dug, dug);
        }

        static void CutLoose(Vector2Int dug)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var c = new Vector2Int(dug.x + dx, dug.y + dy);
                    if (IsSpikeCell(c)) CutOne(c);
                }
        }

        /// <summary>
        /// A blast (Projectile.ExplodeTiles, TerraProjectiles.BlastTiles): the spikes and thorns in its circle blown away as a
        /// pickaxe takes them, those the blasted ground held up with it, and what is left of their pictures round it.
        /// </summary>
        public static int Blasted(Vector2Int min, Vector2Int max, System.Func<int, int, bool> inside, System.Collections.Generic.List<Vector2Int> dug)
        {
            int n = 0;
            for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (!inside(x, y) || !IsSpikeCell(c)) continue;
                    CutOne(c);
                    n++;
                }
            foreach (var c in dug) CutLoose(c);
            TidyArt(min, max);
            if (n > 0) Plugin.Log.LogInfo($"[挖掉的尖刺] 爆炸炸掉 {n} 格尖刺");
            return n;
        }

        static TerrainRegion tidied;

        /// <summary>
        /// Once in each room entered: the spikes mined out there before (a save's cuts) tidied as a new cut is, so none
        /// mined in an older version is left with its tips in the air. A guest leaves the host's world as it is.
        /// </summary>
        public static void TidyEntered()
        {
            var r = TerraWorld.Live;
            if (r == null || r == tidied || r.Holes == null) return;
            tidied = r;
            if (!r.Holes.HasSpikeArt || Net.WorldSync.Guest) return;
            var g = r.Grid;
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (g.IsDug(x, y) && !g.IsDugGround(x, y))
                    {
                        var c = new Vector2Int(x + r.CellOffset.x, y + r.CellOffset.y);
                        TidyArt(c, c);
                    }
        }

        /// <summary>How far round a cut its spike's picture is looked for (cells): a row's tips, a thorn bush's fringe.</summary>
        const int ArtReach = 3;

        /// <summary>
        /// A spike's or thorn's picture over the open cells round a cut, where no hazard reaches (a row's tips over its box, a
        /// bush's leaves past its collider): cut away (CellGrid.CutOpenArt) where the nearest hazard cells to it are all cut
        /// (mined, blown up, or gone with their ground), so no stub of a spike is left in the air; the tips of a spike still
        /// standing next to it stay with that spike.
        /// </summary>
        public static void TidyArt(Vector2Int min, Vector2Int max)
        {
            over.Clear();
            int cut = 0;
            for (int y = min.y - ArtReach; y <= max.y + ArtReach; y++)
                for (int x = min.x - ArtReach; x <= max.x + ArtReach; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (TerraWorld.Get(x, y) != CellKind.Empty || TerraWorld.IsDug(x, y)) continue;
                    var r = TerraWorld.RegionAt(x, y);
                    if (r?.Holes == null || !r.Holes.HasSpikeArt || BossRooms.Locked(x, y)) continue;
                    if (!r.Holes.SpikeArtOver(x - r.CellOffset.x, y - r.CellOffset.y) || Over(c) || !NearestCut(c)) continue;
                    r.Grid.CutOpenArt(x - r.CellOffset.x, y - r.CellOffset.y);
                    cut++;
                }
            DevArtTidied += cut;
            if (cut > 0) Plugin.Log.LogInfo($"[挖掉的尖刺] 旁边 {cut} 格只剩尖刺图的空格一起抠掉");
        }

        /// <summary>The hazard cells nearest a cell (within <see cref="ArtReach"/>) are all cut: dug, or their art cut with dug ground's edge.</summary>
        static bool NearestCut(Vector2Int c)
        {
            int best = int.MaxValue;
            bool allCut = false;
            for (int dy = -ArtReach; dy <= ArtReach; dy++)
                for (int dx = -ArtReach; dx <= ArtReach; dx++)
                {
                    int d = dx * dx + dy * dy;
                    if (d == 0 || d > ArtReach * ArtReach || d > best) continue;
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (!Over(n)) continue;
                    bool isCut = TerraWorld.ArtCutAt(n.x, n.y);
                    if (!isCut && Cells.Blocks(TerraWorld.Get(n.x, n.y))) continue;
                    if (d < best)
                    {
                        best = d;
                        allCut = isCut;
                    }
                    else allCut &= isCut;
                }
            return best != int.MaxValue && allCut;
        }

        static readonly System.Collections.Generic.Dictionary<Vector2Int, bool> over = new System.Collections.Generic.Dictionary<Vector2Int, bool>();

        /// <summary><see cref="HazardOver"/>, asked once per cell in a tidy.</summary>
        static bool Over(Vector2Int c)
        {
            if (!over.TryGetValue(c, out bool v)) over[c] = v = HazardOver(c);
            return v;
        }

        enum Touch { Neutral, Cut, Standing }

        static readonly System.Collections.Generic.Dictionary<Vector2Int, Touch> touched = new System.Collections.Generic.Dictionary<Vector2Int, Touch>();

        /// <summary>
        /// What touching the hazard in a cell means: its art cut away there (mined, or the overhang of dug ground) is
        /// harmless; a spike cell still standing (one the pick can take: open air the hazard reaches into) hurts; the
        /// rest — solid ground the box only grazes, a cell the hazard's edge only skims past the pick's reach — doesn't
        /// decide either way.
        /// </summary>
        static Touch TouchAt(Vector2Int c)
        {
            if (!touched.TryGetValue(c, out var t))
                touched[c] = t = TerraWorld.ArtCutAt(c.x, c.y) ? Touch.Cut : IsSpikeCell(c) ? Touch.Standing : Touch.Neutral;
            return t;
        }

        /// <summary>
        /// The hero touches the hazard only where it has been dug away: some point of the overlap in a cut cell, none in a
        /// spike cell still standing. False when the overlap can't be found (touching at an edge) or lies only in cells that
        /// decide nothing: the hit then stands.
        /// </summary>
        static bool DugAway(GameObject hazard, Bounds hero, out Vector2Int uncut, out bool anyCut)
        {
            anyCut = false;
            bool standing = false;
            uncut = default;
            touched.Clear();
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
                    var c = CellGrid.WorldToCell(p);
                    var t = TouchAt(c);
                    if (t == Touch.Cut) anyCut = true;
                    else if (t == Touch.Standing) { standing = true; uncut = c; }
                }
            }
            return anyCut && !standing;
        }
    }
}
