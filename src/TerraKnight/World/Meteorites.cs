using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's meteorite (1.4.5: NPC.DoDeathEvents's spawnMeteor, Main.HandleMeteorFall, WorldGen.dropMeteor and
    /// meteor). A boss beaten queues one, its first defeat always, a later one half the time; it comes down at the next
    /// midnight (Main.time 16200 into the night: 1170 s into the day here) in a room the player has been through, never
    /// the one they stand in, Dirtmouth, a boss's room, a dream, Godhome or the White Palace. It lands on a floor the player
    /// has seen there, with room to stand above it, deep rock below and nothing of the room's (a bench, a door, a lever, a
    /// stag) near, and smashes its crater as Terraria's five passes do (the Meteorite body, the pit, the clean-up, two
    /// rings of specks), at half Terraria's size for Hallownest's rooms. "A meteorite has landed!" comes in the chat, and
    /// the map shows its cells bright at once. A Meteorite block wants a 50% pickaxe, glows, and burns whoever touches it
    /// without fire-walking gear (Burning: 30 life a second, no regeneration, half speed).
    /// </summary>
    internal static class Meteorites
    {
        public const int Burning = 67;
        /// <summary>Main.HandleMeteorFall's 16200 ticks into the night.</summary>
        const float Midnight = TerraTime.DaySeconds + 270f;
        const float Scale = 0.5f;

        /// <summary>WorldGen.spawnMeteor: one is coming (kept in the save).</summary>
        public static bool Pending;
        /// <summary>How many have come down in this save.</summary>
        public static int Landed;

        public static int DevQueued, DevDropped, DevRefused, DevBurnTicks;
        public static string DevLast = "";
        /// <summary>Dev: where the last one came down (the scene, the impact cell in it).</summary>
        public static string LastScene;
        public static Vector2Int LastAt;
        /// <summary>Dev: midnight now, whatever the clock says.</summary>
        public static bool DevAtMidnight;

        public static void Clear()
        {
            Pending = false;
            Landed = 0;
            burnCarry = 0f;
            burnSum = 0;
        }

        /// <summary>A boss's kill counted (Items.Bestiary.Credit).</summary>
        public static void OnBossDown(bool firstKill)
        {
            if (Net.WorldSync.Guest) return;
            if (!firstKill && Random.Range(0, 2) != 0) return;
            if (!Pending) Plugin.Log.LogInfo("[陨石] 首领倒下：今晚午夜有陨石落下");
            Pending = true;
            DevQueued++;
        }

        /// <summary>Each frame in play: the burning, and a pending meteorite at midnight.</summary>
        public static void Tick(HeroController hero, TerraMod mod)
        {
            Burn(hero, mod);
            if (!Pending || hero == null || Net.WorldSync.Guest) return;
            if (!DevAtMidnight && (TerraTime.IsDay || TerraTime.Now < Midnight)) return;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) return;
            // Main.HandleMeteorFall: the flag goes whether it found somewhere or not.
            Pending = false;
            Drop(gm.sceneName);
        }

        internal static bool Excluded(string scene, string here)
        {
            if (string.IsNullOrEmpty(scene)) return true;
            if (here != null && (scene == here || scene.StartsWith(here + "_") || here.StartsWith(scene + "_"))) return true;
            if (scene == "Town" || scene.StartsWith("Room_") || scene.StartsWith("GG_") || scene.StartsWith("Dream_") || scene.StartsWith("White_Palace")
                || scene.StartsWith("Cinematic") || scene.StartsWith("Menu") || scene.Contains("_boss") || scene.StartsWith("Grimm_") || scene.StartsWith("Abyss_15"))
                return true;
            if (BossRooms.IsRoom(scene)) return true;
            return Areas.LevelOf(scene) < 0;
        }

        /// <summary>WorldGen.dropMeteor: somewhere to land in the rooms the player has been through; false when none would do.</summary>
        public static bool Drop(string here, string only = null)
        {
            var rooms = UI.WorldMap.VisitedRooms();
            rooms.RemoveAll(s => (only != null ? s != only : Excluded(s, here)));
            if (rooms.Count == 0) { DevRefused++; Plugin.Log.LogInfo("[陨石] 没有能落的房间"); return false; }
            for (int attempt = 0; attempt < 400; attempt++)
            {
                string scene = rooms[Random.Range(0, rooms.Count)];
                if (!UI.WorldMap.RoomSize(scene, out int w, out int h) || w < 60 || h < 30) continue;
                int x = Random.Range(20, w - 20);
                if (!Floor(scene, x, h, out int j)) continue;
                if (!Dense(scene, x, j)) continue;
                if (UI.WorldMap.ThingNear(scene, x, j, 16)) continue;
                Smash(scene, x, j);
                return true;
            }
            DevRefused++;
            Plugin.Log.LogInfo("[陨石] 没找到落点");
            return false;
        }

        /// <summary>
        /// Down the column from the top to the first floor the player has seen: open air they walked through, four cells
        /// of headroom, the room's rock under it and below that rock for the whole pit's depth.
        /// </summary>
        static bool Floor(string scene, int x, int h, out int j)
        {
            j = -1;
            for (int y = h - 6; y >= 12; y--)
            {
                if (UI.WorldMap.CellOf(scene, x, y) != UI.WorldMap.MeteorCell.Air || UI.WorldMap.CellOf(scene, x, y - 1) != UI.WorldMap.MeteorCell.Rock) continue;
                bool open = true;
                for (int k = 0; k < 4 && open; k++)
                    for (int dx = -1; dx <= 1 && open; dx++)
                        open = UI.WorldMap.CellOf(scene, x + dx, y + k) == UI.WorldMap.MeteorCell.Air;
                if (!open || !UI.WorldMap.SeenIn(scene, x, y)) continue;
                bool deep = true;
                for (int k = 1; k <= 9 && deep; k++) deep = UI.WorldMap.CellOf(scene, x, y - k) == UI.WorldMap.MeteorCell.Rock;
                if (!deep) continue;
                j = y - 1;
                return true;
            }
            return false;
        }

        /// <summary>dropMeteor's 30×30 box scored: enough rock round the impact (two thirds of the half-size box), no water.</summary>
        static bool Dense(string scene, int x, int j)
        {
            int score = 0, n = 0;
            for (int dx = -8; dx < 8; dx++)
                for (int dy = -8; dy < 8; dy++)
                {
                    n++;
                    var c = UI.WorldMap.CellOf(scene, x + dx, j + dy);
                    if (c == UI.WorldMap.MeteorCell.Rock || c == UI.WorldMap.MeteorCell.Block) score++;
                    else if (c == UI.WorldMap.MeteorCell.None || c == UI.WorldMap.MeteorCell.Other) score -= 4;
                }
            return score * 3 >= n * 2 - n / 3;
        }

        static int R(int a, int b) => Random.Range(a, b + 1);
        static int Half(int a) => Mathf.RoundToInt(a * Scale);

        /// <summary>
        /// WorldGen.meteor's passes on the room's cells (Terraria's rows counted down, here up: "above" is +y), written
        /// into the save's edits for the scene and onto the map. The body turns the rock it covers to Meteorite; the pit
        /// takes the middle out below the floor; a Meteorite cell left with no solid neighbour falls away; two rings of
        /// specks turn a tenth and a twentieth of the rock further out.
        /// </summary>
        static void Smash(string scene, int i, int j)
        {
            UI.WorldMap.RoomSize(scene, out int w, out int h);
            var state = new Dictionary<int, UI.WorldMap.MeteorCell>();
            UI.WorldMap.MeteorCell Get(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return UI.WorldMap.MeteorCell.None;
                return state.TryGetValue(y * w + x, out var c) ? c : UI.WorldMap.CellOf(scene, x, y);
            }
            var meteor = new HashSet<int>();
            var hollow = new HashSet<int>();
            bool Solid(int x, int y) { var c = Get(x, y); return c == UI.WorldMap.MeteorCell.Rock || c == UI.WorldMap.MeteorCell.Block || meteor.Contains(y * w + x); }
            void ToMeteor(int x, int y)
            {
                int k = y * w + x;
                meteor.Add(k);
                hollow.Remove(k);
                state[k] = UI.WorldMap.MeteorCell.Block;
            }
            void ToAir(int x, int y)
            {
                int k = y * w + x;
                meteor.Remove(k);
                hollow.Add(k);
                state[k] = UI.WorldMap.MeteorCell.Air;
            }
            float Dist(int x, int y) => Mathf.Sqrt((x - i) * (x - i) + (y - j) * (y - j));
            // 1, the body: the rock in a disc cut off a little over the impact turns to Meteorite.
            int r = Half(R(17, 22));
            for (int x = i - r; x < i + r; x++)
                for (int y = j - r; y < j + r; y++)
                {
                    if (y >= j + Half(5) + Random.Range(-1, 2)) continue;
                    if (Dist(x, y) >= 0.9f * r + Random.Range(-2, 3)) continue;
                    if (Get(x, y) == UI.WorldMap.MeteorCell.Rock || Get(x, y) == UI.WorldMap.MeteorCell.Block) ToMeteor(x, y);
                }
            // 2, the pit: the middle under the impact taken out.
            r = Half(R(8, 13));
            for (int x = i - r; x < i + r; x++)
                for (int y = j - r; y < j + r; y++)
                {
                    if (y >= j + Half(4) + Random.Range(-1, 2)) continue;
                    if (Dist(x, y) >= 0.8f * r + Random.Range(-1, 2)) continue;
                    if (Solid(x, y)) ToAir(x, y);
                }
            // 3, the clean-up: a Meteorite cell with no solid neighbour falls away.
            r = Half(R(25, 34));
            for (int x = i - r; x < i + r; x++)
                for (int y = j - r; y < j + r; y++)
                    if (meteor.Contains(y * w + x) && !Solid(x - 1, y) && !Solid(x + 1, y) && !Solid(x, y - 1) && !Solid(x, y + 1)) ToAir(x, y);
            // 4 and 5, the specks: a tenth, then a twentieth, of the rock further out.
            foreach (var (lo, hi, above, chance, edge) in new[] { (23, 31, 3, 10, 0.8f), (30, 37, 0, 20, 0.85f) })
            {
                r = Half(R(lo, hi));
                for (int x = i - r; x < i + r; x++)
                    for (int y = j - r; y < j + r; y++)
                    {
                        if (y >= j + Half(above) + Random.Range(-1, 2)) continue;
                        if (Random.Range(0, chance) != 0 || Dist(x, y) >= edge * r) continue;
                        var c = Get(x, y);
                        if (c == UI.WorldMap.MeteorCell.Rock || c == UI.WorldMap.MeteorCell.Block) ToMeteor(x, y);
                    }
            }
            // Into the scene's edits: the room's rock cut (dug) where it changed, as digging does.
            var edits = WorldEdits.For(scene);
            var mapCells = new List<(int x, int y, bool meteorite)>();
            foreach (int k in meteor)
            {
                edits[k] = WorldEdits.Pack(CellKind.Meteorite, true);
                mapCells.Add((k % w, k / w, true));
            }
            foreach (int k in hollow)
            {
                edits[k] = WorldEdits.Pack(CellKind.Empty, true);
                mapCells.Add((k % w, k / w, false));
            }
            UI.WorldMap.MeteorLanded(scene, mapCells);
            LastScene = scene;
            LastAt = new Vector2Int(i, j);
            Landed++;
            DevDropped++;
            DevLast = $"{scene} ({i}, {j})：陨石 {meteor.Count} 格，坑 {hollow.Count} 格";
            Plugin.Log.LogInfo($"[陨石] 落在 {DevLast}");
            // LegacyWorldGen.59 in the world's chat colour (ChatColors.World).
            string text = Terraria.TerrariaText.Get(Lang.Culture, "LegacyWorldGen.59") ?? Lang.T("有一颗陨石落地了！");
            Net.NetChat.Add(text, new Color(50 / 255f, 1f, 130 / 255f));
            TerraMod.Instance?.PlaySound("Item_14");
        }

        static float burnCarry, burnShown;
        static int burnSum;

        /// <summary>
        /// Collision.HurtTiles and Player.ApplyTouchDamage: the player's box against a Meteorite block (standing on it,
        /// pressed to it) without fire-walking burns for 20 ticks more; Burning takes 60 lifeRegen (30 life a second).
        /// </summary>
        static void Burn(HeroController hero, TerraMod mod)
        {
            if (hero == null || mod == null || mod.Creative) return;
            if (!Items.Equipment.Current.FireWalk && Touching(hero)) Player.PlayerBuffs.Add(Burning, 20);
            mod.Stats.Burning = Player.PlayerBuffs.Has(Burning);
            if (!mod.Stats.Burning || mod.Stats.IsDead || PlayerData.instance == null || PlayerData.instance.isInvincible) { burnCarry = 0f; return; }
            DevBurnTicks++;
            burnCarry += 30f * Time.deltaTime;
            int whole = (int)burnCarry;
            if (whole <= 0) return;
            burnCarry -= whole;
            var stats = mod.Stats;
            stats.Life = Mathf.Max(0, stats.Life - whole);
            burnSum += whole;
            if (Time.time >= burnShown)
            {
                burnShown = Time.time + 0.4f;
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, burnSum.ToString(), UI.CombatText.PlayerHurt);
                burnSum = 0;
            }
            if (stats.IsDead) hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
        }

        static bool Touching(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            if (col == null) return false;
            var b = col.bounds;
            // Half a Terraria pixel of give each way (a cell is 16 of them).
            const float give = CellGrid.CellSize / 32f;
            var lo = CellGrid.WorldToCell((Vector2)b.min - new Vector2(give, give));
            var hi = CellGrid.WorldToCell((Vector2)b.max + new Vector2(give, give));
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                    if (TerraWorld.Get(x, y) == CellKind.Meteorite) return true;
            return false;
        }
    }
}
