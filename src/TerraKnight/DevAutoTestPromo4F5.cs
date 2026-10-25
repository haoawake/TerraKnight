using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>Dev (suite "p4_v5F_scout_zh"): recorder F's scouting, not a clip: floors, waters and thorns of some rooms, photographed.</summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The longest flat floor of the live room with five free cells over it, well inside the room.</summary>
        static int F5Floor(out Vector2Int start)
        {
            start = default;
            var region = TerraWorld.Live;
            if (region == null) return 0;
            var cr = region.CellRect;
            int best = 0;
            for (int x = cr.x + 4; x < cr.xMax - 4; x++)
                for (int y = cr.y + 4; y < cr.yMax - 8; y++)
                {
                    if (!C4Inside(region, x, y, 20, 10) || !C4Stand(x, y) || C4Stand(x - 1, y)) continue;
                    int run = 0;
                    while (run < 80 && C4Stand(x + run, y) && !TerraWorld.IsSolid(x + run, y + 4)) run++;
                    if (run > best) { best = run; start = new Vector2Int(x, y); }
                }
            return best;
        }

        IEnumerator P4_v5F_scout(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            var floors = new[] { "Crossroads_01", "Crossroads_02", "Crossroads_04", "Crossroads_13", "Fungus2_01", "Fungus2_02", "Ruins1_01", "Ruins2_01", "RestingGrounds_04", "Fungus3_01", "Deepnest_01b", "Cliffs_02" };
            var ponds = new[] { "Fungus3_04", "Fungus3_10", "Waterways_01", "Waterways_02", "Abyss_04", "RestingGrounds_09", "Hive_01", "Deepnest_East_06", "Fungus2_07", "Ruins1_04" };
            var thorns = new[] { "Fungus1_02", "Fungus1_03", "Fungus1_05", "Fungus1_06", "Fungus3_05", "Fungus3_08", "Crossroads_07" };
            foreach (var scene in floors.Concat(ponds).Concat(thorns).Distinct())
            {
                if (!entryGates.ContainsKey(scene)) { F5Log($"侦察 {scene}：没有入口"); continue; }
                yield return EnterRoom(scene);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != scene) { F5Log($"侦察 {scene}：没进去"); continue; }
                F5Safe();
                QuietAround(hero.transform.position, 400f);
                int run = F5Floor(out var start);
                if (run > 0)
                {
                    C4PutOn(hero, new Vector2Int(start.x + 2, start.y));
                    C4Face(hero, 1);
                    yield return new WaitForSeconds(0.8f);
                    yield return F5Snap($"floor_{scene}");
                }
                F5Log($"侦察 {scene}：平地 {run} 格 从 {start}（x {CellGrid.CellCenter(start.x, start.y).x:F1} y {CellGrid.CellCenter(start.x, start.y).y:F1}），亮度见图");
                var ps = Fishing.DevPonds().Where(p => p.Scene == scene).OrderByDescending(p => p.Tiles).ToList();
                foreach (var p in ps.Take(3)) F5Log($"侦察 {scene}：{(p.Acid ? "酸" : "水")} {p.Tiles} 格 {p.Area.min:F1}..{p.Area.max:F1}");
                foreach (bool acid in new[] { false, true })
                    if (C4Shore(scene, acid, out var water, out var stand, out int face))
                    {
                        C4PutOn(hero, stand);
                        C4Face(hero, face);
                        yield return new WaitForSeconds(0.8f);
                        yield return F5Snap($"pond_{scene}_{(acid ? "acid" : "water")}");
                    }
                var spikes = new List<Collider2D>();
                foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                    if (dh.gameObject.scene.name == scene && dh.hazardType == 2) foreach (var c in dh.GetComponents<Collider2D>()) spikes.Add(c);
                int thorn = spikes.Count(c => Player.Poison.IsThorn(c.gameObject));
                F5Log($"侦察 {scene}：尖刺 {spikes.Count} 个（荆棘 {thorn}）{string.Join(" ", spikes.Take(8).Select(c => c.name + "@" + ((Vector2)c.bounds.center).ToString("F0") + "/" + ((Vector2)c.bounds.size).ToString("F0")))}");
                if (thorn > 0)
                {
                    var c = spikes.First(s => Player.Poison.IsThorn(s.gameObject));
                    hero.transform.position = Clear(hero, (Vector2)c.bounds.center + new Vector2(-c.bounds.extents.x - 2f, c.bounds.extents.y + 1f));
                    yield return new WaitForSeconds(0.8f);
                    yield return F5Snap($"thorn_{scene}");
                }
            }
            F5Log("储物箱计划 " + RegionalChests.DevDescribe());
        }
    }
}
