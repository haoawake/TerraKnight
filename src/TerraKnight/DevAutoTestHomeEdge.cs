using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "homeedge", the user's 10-29 "哪都是家检测制作站，制作站在检测边缘只要有一格在范围内就生效" and "制作站的检测
    /// 应该按照哪都是家的那个范围检测而不是玩家的距离"): every crafting station kind set at each of a home's four edges with one
    /// cell inside its range counts for that home, one more cell out it doesn't; then, the player in another room, a home
    /// opened from the bag lists recipes of its edge stations (work bench, furnace, anvil) and none of the stations by the
    /// player. Lines "[AutoTest] HomeEdge …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HomeEdgeTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] HomeEdge {name}：{detail} {(ok ? "通过" : "不通过")}"); }
            Player.TerraStats.DevImmortal = true; PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            var oldStations = Stations.Save(); var oldChests = Chests.Save(); var oldSlots = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite;
            const float cs = CellGrid.CellSize;
            try
            {
                inv.Infinite = false; TerraMod.DevAllInfinite = false; mod.ApplyCreative(false);
                const string scene = "Crossroads_02";
                var a = new Vector2(30.25f, 8f);
                int kinds = 0, edgeIn = 0, edgeOut = 0;
                var misses = new List<string>();
                foreach (var (kind, w, h, counts) in Stations.DevCraftingSpecs())
                {
                    kinds++;
                    foreach (string side in new[] { "右", "左", "上", "下" })
                        for (int outside = 0; outside <= 1; outside++)
                        {
                            Stations.Clear(); Chests.Clear();
                            Stations.PlaceIn(StationKind.HomeEverywhere, scene, a);
                            var home = Stations.Homes().Single();
                            var hb = Stations.HomeBounds(home);
                            float shift = outside * cs;
                            // Its footprint's middle across (odd: a cell's middle, even: a cell edge) and its bottom.
                            float midX = w % 2 == 1 ? hb.center.x : hb.center.x + cs / 2f, lowY = hb.yMin + 5f * cs;
                            Vector2 local = side == "右" ? new Vector2(hb.xMax - cs + shift + w * cs / 2f, lowY)
                                : side == "左" ? new Vector2(hb.xMin + cs - shift - w * cs / 2f, lowY)
                                : side == "上" ? new Vector2(midX, hb.yMax - cs + shift)
                                : new Vector2(midX, hb.yMin + cs - shift - h * cs);
                            Stations.PlaceIn(kind, scene, local);
                            var names = new List<string>();
                            Stations.HomeStations(home.Key, names);
                            bool counted = names.Contains(counts[0]);
                            if (outside == 0 && counted) edgeIn++;
                            if (outside == 1 && !counted) edgeOut++;
                            if (counted == (outside == 1)) misses.Add($"{kind}({w}x{h}) {side}边{(outside == 0 ? "一格在内" : "刚好在外")}");
                        }
                }
                Check("每种制作站在四条边上一格在范围内就算", edgeIn == kinds * 4, $"{edgeIn}/{kinds * 4}");
                Check("刚好在范围外的不算", edgeOut == kinds * 4, $"{edgeOut}/{kinds * 4}");
                if (misses.Count > 0) Plugin.Log.LogInfo($"[AutoTest] HomeEdge 不符：{string.Join("；", misses.Take(40))}");

                // The player in Dirtmouth, the home in the Crossroads with its stations at its edges; a station by the player.
                Stations.Clear(); Chests.Clear();
                Stations.PlaceIn(StationKind.HomeEverywhere, scene, a);
                var h0 = Stations.Homes().Single();
                var b0 = Stations.HomeBounds(h0);
                Stations.PlaceIn(StationKind.WorkBench, scene, new Vector2(b0.xMax - cs + cs, b0.yMin + 5f * cs));
                Stations.PlaceIn(StationKind.IronAnvil, scene, new Vector2(b0.xMin + cs - cs, b0.yMin + 5f * cs));
                Stations.PlaceIn(StationKind.Furnace, scene, new Vector2(b0.center.x, b0.yMax - cs));
                var hero = HeroController.instance;
                string town = TerraWorld.Live.SceneName;
                var heroLocal = (Vector2)hero.transform.position - (Vector2)TerraWorld.Live.Root.position;
                Stations.PlaceIn(StationKind.Loom, town, new Vector2(Mathf.Round(heroLocal.x / cs) * cs + cs / 2f, heroLocal.y - 1f));
                inv.Clear(); inv.Mouse = default;
                inv.Slots[10] = ItemStack.Of(ItemType.Wood, 99);
                inv.Slots[11] = ItemStack.Of(ItemType.IronBar, 30);
                inv.Slots[12] = ItemStack.Of(ItemType.IronOre, 30);
                inv.Slots[13] = ItemStack.Of(ItemType.Cobweb, 30);
                InventoryGui.Open = true; yield return new WaitForSeconds(.3f);
                string before = InventoryGui.DevColumnStations();
                Check("没打开家园：只有身边的制作站", !before.Contains("工作台") && !before.Contains("熔炉") && !before.Contains("砧"), before);
                InventoryGui.DevHomeClick("HomeButton", 0); yield return new WaitForSeconds(.3f);
                Check("背包里打开家园", Stations.HomeActive && Stations.SelectedHome == h0.Key);
                string after = InventoryGui.DevColumnStations();
                Check("打开家园：在别的房间也用它边上的工作台、熔炉、砧", after.Contains("工作台") && after.Contains("熔炉") && after.Contains("砧"), after);
                Check("打开家园：不混用身边的织布机", !after.Contains("织布机"), after);
                Shot("Town", "homeedge_open");
                yield return new WaitForSeconds(.3f);
                InventoryGui.Open = false; yield return null;
            }
            finally
            {
                InventoryGui.Open = false;
                Stations.Load(oldStations); Chests.Load(oldChests);
                for (int i = 0; i < oldSlots.Length; i++) inv.Slots[i] = oldSlots[i];
                inv.Mouse = default; inv.Infinite = infinite;
            }
            Plugin.Log.LogInfo($"[AutoTest] HomeEdge 合计 {pass}/{total}");
        }
    }
}
