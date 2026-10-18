using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17bossdrops"): a boss's loot where it lands. In a few boss rooms, drops let out as a boss's are
    /// (EnemyDrops' own spot, WorldItems.Drop's hop) above the floor, just in it and deep in it; after they settle, each
    /// item's bottom against the ground's real top under it (the colliders the room plays on): sunk, resting or floating.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] bossDropRooms = { "Crossroads_10", "Fungus1_04", "Waterways_05", "Ruins1_24", "Deepnest_32", "Fungus2_15", "Abyss_19", "Mines_18" };

        static readonly Collider2D[] overlapBuf = new Collider2D[4];

        static float? GroundTop(Vector2 from, float down = 12f)
        {
            var hit = Physics2D.Raycast(from, Vector2.down, down, 1 << World.ChunkColliders.TerrainLayer);
            return hit.collider != null ? hit.point.y : (float?)null;
        }

        IEnumerator BossDrops17()
        {
            WorldItems.DevNoGrab = true;
            var types = new[] { ItemType.Wood, ItemType.StoneBlock, ItemType.Gel, ItemType.Torch, ItemType.Lens, ItemType.CopperBar, ItemType.IronBar, ItemType.Glass, ItemType.Daybloom };
            int sunkAll = 0, total = 0, hopAll = 0;
            var report = new List<string>();
            foreach (var scene in bossDropRooms)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(2f);
                WorldItems.DevClear();
                var hero = HeroController.instance;
                Vector2 h = hero.GetComponent<BoxCollider2D>().bounds.center;
                var spots = new List<(ItemType T, Vector2 At, string How)>();
                int k = 0;
                foreach (float dx in new[] { 3f, 6f, 9f })
                {
                    float sx = h.x + (hero.cState.facingRight ? dx : -dx);
                    float? floor = GroundTop(new Vector2(sx, h.y + 4f), 14f);
                    if (floor == null) continue;
                    foreach (var (dy, how) in new[] { (1f, "地上"), (-0.2f, "半埋"), (-1.2f, "埋深") })
                    {
                        var at = EnemyDrops.DevBossDropPoint(new Vector2(sx, floor.Value + dy));
                        var t = types[k++ % types.Length];
                        spots.Add((t, at, how));
                        WorldItems.Drop(ItemStack.Of(t), at);
                    }
                }
                yield return new WaitForSeconds(3f);
                bool own = World.TerraWorld.OwnGroundAt(h);
                int sunk = 0, floating = 0;
                var lines = new List<string>();
                foreach (var it in WorldItems.DevItems.ToList())
                {
                    var spot = spots.FirstOrDefault(s => s.T == it.Stack.Type);
                    // Sunk: its box (a little smaller) in the room's ground, or its middle in a solid cell; floating: no ground
                    // within a tile under its bottom.
                    total++;
                    bool inGround = World.TerraWorld.OwnGroundAt(it.At) ? Physics2D.OverlapBox(it.At, new Vector2(0.36f, 0.36f), 0f, new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << World.ChunkColliders.TerrainLayer }, overlapBuf) > 0 : World.TerraWorld.IsSolidAt(it.At);
                    float? top = GroundTop(new Vector2(it.At.x, it.At.y - 0.2f), 3f);
                    float gap = top != null ? it.At.y - 0.25f - top.Value : 9f;
                    if (inGround) { sunk++; sunkAll++; lines.Add($"{spot.How}卡在地里 @{it.At.x:F1},{it.At.y:F1}"); }
                    else if (gap > 0.3f) { floating++; lines.Add($"{spot.How}悬空 {gap:F2}"); }
                }
                Shot(scene, "bossdrops");
                yield return new WaitForSeconds(0.3f);
                // Settled: each stays at one height for a second and a half (none hopping on its floor).
                var low = new Dictionary<ItemType, float>();
                var high = new Dictionary<ItemType, float>();
                for (float end = Time.time + 1.5f; Time.time < end;)
                {
                    foreach (var it in WorldItems.DevItems)
                    {
                        var t = it.Stack.Type;
                        low[t] = low.TryGetValue(t, out float lo) ? Mathf.Min(lo, it.At.y) : it.At.y;
                        high[t] = high.TryGetValue(t, out float hi) ? Mathf.Max(hi, it.At.y) : it.At.y;
                    }
                    yield return null;
                }
                int hopping = 0;
                foreach (var kv in low)
                    if (high[kv.Key] - kv.Value > 0.03f) { hopping++; hopAll++; lines.Add($"{ItemDb.Get(kv.Key).Name}上下跳 {high[kv.Key] - kv.Value:F2}"); }
                report.Add($"{scene}（{(own ? "原版地面" : "格子")}）{spots.Count} 个：陷进 {sunk}，悬空 {floating}，乱跳 {hopping}{(lines.Count > 0 ? "：" + string.Join("，", lines.Take(5)) : "")}");
            }
            WorldItems.DevClear();
            WorldItems.DevNoGrab = false;
            FxCheck("Boss 掉落物不陷进地里", total > 0 && sunkAll == 0, string.Join("；", report));
            FxCheck("掉落物落地后不上下乱跳", total > 0 && hopAll == 0, $"乱跳 {hopAll} 个");
        }
    }
}
