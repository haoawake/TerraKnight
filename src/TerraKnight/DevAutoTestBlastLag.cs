using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "blastlag", the user's 10-26 "用雷管炸地图越炸越卡"): in Dirtmouth, rounds of six dynamite sticks dropped
    /// across the ground by the hero (the craters deepening round by round, the blocks dropping as items, none picked up), the
    /// frames measured after each round with every script timed (DevFrameProfiler) beside what has piled up: items lying,
    /// the room's hole masks, hole shapes made, visible renderers, objects. Then the items cleared and measured again. Lines
    /// "[AutoTest] 炸图 …" and "[AutoTest] 卡顿 炸图 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Dev (suite "blastlook"): two dynamite craters by the hero in Dirtmouth, shot (Town_blastlook_1, _2) to compare how
        /// the holes look between builds.</summary>
        IEnumerator BlastLookTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var hero = HeroController.instance;
            var stand = Clear(hero, (Vector2)hero.transform.position + new Vector2(10f, 0f));
            hero.transform.position = stand;
            yield return new WaitForSeconds(1f);
            var feet = hero.GetComponent<BoxCollider2D>().bounds.min;
            var cell = CellGrid.WorldToCell(new Vector2(feet.x, feet.y + 0.1f));
            for (int k = 1; k <= 2; k++)
            {
                TerraProjectiles.Instance.Shoot(ProjKind.Dynamite, CellGrid.CellCenter(cell.x + (k == 1 ? 8 : -6), cell.y + 2), Vector2.zero, 0, 0f, 0f);
                yield return new WaitForSeconds(6.5f);
                hero.transform.position = Clear(hero, stand);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1.5f);
                Shot("Town", $"blastlook_{k}");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 炸图外观 遮罩的格子 {TerrainSystem.Current?.Region?.Holes?.MaskCount}，遮罩 {Object.FindObjectsByType<SpriteMask>(FindObjectsSortMode.None).Length} 个");
        }

        IEnumerator BlastLagTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            // The blocks drop as in the player's game; none picked up (a full bag, or him away from them).
            inv.Infinite = false;
            WorldItems.DevNoGrab = true;
            var hero = HeroController.instance;
            var stand = (Vector2)hero.transform.position + new Vector2(10f, 0f);
            hero.transform.position = Clear(hero, stand);
            yield return new WaitForSeconds(1f);
            var feet = hero.GetComponent<BoxCollider2D>().bounds.min;
            var baseCell = CellGrid.WorldToCell(new Vector2(feet.x, feet.y + 0.1f));

            string Piled()
            {
                var region = TerrainSystem.Current != null ? TerrainSystem.Current.Region : null;
                int masks = region?.Holes != null ? region.Holes.MaskCount : -1;
                int seen = 0, all = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    all++;
                    if (r.enabled && r.isVisible) seen++;
                }
                return $"地上物品 {WorldItems.Count}，遮罩的格子 {masks}（遮罩 {Object.FindObjectsByType<SpriteMask>(FindObjectsSortMode.None).Length} 个），洞形状 {HoleShapes.DevShapes}，渲染器 {seen} 可见/{all}，物体 {Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}";
            }

            IEnumerator Settle(string label)
            {
                hero.transform.position = Clear(hero, stand);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 炸图 {label}：{Piled()}");
                yield return Measure("炸图 " + label, 4f);
            }

            yield return Settle("开炸前");
            const int rounds = 6;
            int sticks = 0;
            for (int r = 1; r <= rounds; r++)
            {
                for (int k = 0; k < 6; k++)
                {
                    // Over the ground left and right of where he stands, falling into the last round's craters.
                    int x = baseCell.x - 20 + k * 8;
                    TerraProjectiles.Instance.Shoot(ProjKind.Dynamite, CellGrid.CellCenter(x, baseCell.y + 3), Vector2.zero, 0, 0f, 0f);
                    sticks++;
                }
                // The fuse (5 s) and the dust settling.
                yield return new WaitForSeconds(6.5f);
                if (r == 1 || r % 2 == 0) yield return Settle($"第 {r} 轮后（{sticks} 根）");
            }
            Shot("Town", "blastlag_craters");
            WorldItems.DevClear();
            yield return null;
            yield return Settle("清掉地上物品后");

            // What the frames go to: the holes' masks, the holes' pictures, the light map, each switched off a while.
            var root = TerrainSystem.Current?.Region?.Root;
            Plugin.Log.LogInfo($"[AutoTest] 炸图 被洞切开的场景图 {TerrainSystem.Current?.Region?.Holes?.ArtCount}");
            if (root != null)
            {
                var masks = root.GetComponentsInChildren<SpriteMask>(false);
                var cells = new System.Collections.Generic.List<SpriteRenderer>();
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(false))
                    if (sr.name == "Terra Cell") cells.Add(sr);
                foreach (var m in masks) m.enabled = false;
                yield return Measure($"炸图 关掉 {masks.Length} 个洞遮罩", 3f);
                foreach (var m in masks) m.enabled = true;
                foreach (var c in cells) c.enabled = false;
                yield return Measure($"炸图 关掉 {cells.Count} 个格子贴图", 3f);
                foreach (var m in masks) m.enabled = false;
                yield return Measure("炸图 遮罩和格子贴图都关掉", 3f);
                foreach (var m in masks) m.enabled = true;
                foreach (var c in cells) c.enabled = true;
            }
            LightMap.Enabled = false;
            yield return Measure("炸图 关掉光照图", 3f);
            LightMap.Enabled = true;
            // The holes out of view: the camera far off along the town.
            hero.transform.position = Clear(hero, stand + new Vector2(-40f, 0f));
            yield return new WaitForSeconds(1.5f);
            yield return Measure("炸图 走远看不见洞", 3f);
            WorldItems.DevNoGrab = false;
            inv.Infinite = infinite;
        }
    }
}
