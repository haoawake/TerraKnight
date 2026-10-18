using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "meteor"): Terraria's meteorite. A boss down queues one; at midnight it comes down in a room the player
    /// has been through (not the one they stand in, never Dirtmouth), into the save's edits and onto the map. There: its
    /// Meteorite blocks are solid, glow, burn the player standing on them (not with an Obsidian Skull), a copper pickaxe
    /// only tinks on them and a gold one digs one out as a Meteorite. The scene's edits are put back after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MeteorTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 陨石 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);

            bool wasPending = Meteorites.Pending;
            Meteorites.Pending = false;
            Meteorites.OnBossDown(true);
            Check("首领倒下排上一颗", Meteorites.Pending, $"排队 {Meteorites.DevQueued}");
            var rooms = UI.WorldMap.VisitedRooms();
            var reachable = rooms.Where(r => entryGates.ContainsKey(r) && !Meteorites.Excluded(r, "Town")).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 陨石 去过的房间 {rooms.Count} 个，能直接传过去的 {reachable.Count} 个：{string.Join(",", reachable.Take(12))}");
            // Its landing: the clock at midnight, only the reachable rooms (the test goes there after).
            string target = null;
            Dictionary<int, ushort> before = null;
            int landed0 = Meteorites.DevDropped;
            foreach (var room in reachable.OrderBy(_ => Random.value))
            {
                before = new Dictionary<int, ushort>(WorldEdits.For(room));
                if (Meteorites.Drop("Town", room)) { target = room; break; }
            }
            Meteorites.Pending = false;
            Check("午夜落在去过的房间", target != null && Meteorites.DevDropped == landed0 + 1 && Meteorites.LastScene == target, Meteorites.DevLast);
            if (target == null) { Meteorites.Pending = wasPending; Plugin.Log.LogInfo($"[AutoTest] 陨石 合计 {ok}/{all} 不通过"); yield break; }
            var edits = WorldEdits.For(target);
            int meteorCells = edits.Values.Count(v => { WorldEdits.Unpack(v, out var k, out _); return k == CellKind.Meteorite; });
            var at = Meteorites.LastAt;
            Check("写进存档和地图", meteorCells >= 25 && UI.WorldMap.DevMeteorCells > 0 && UI.WorldMap.SeenIn(target, at.x, at.y),
                $"{target}：陨石 {meteorCells} 格，地图 {UI.WorldMap.DevMeteorCells} 格");
            Check("落在主角站得住的地方", UI.WorldMap.SeenIn(target, at.x, at.y + 1) && UI.WorldMap.CellOf(target, at.x, at.y + 3) == UI.WorldMap.MeteorCell.Air,
                $"撞击点 {at}");

            yield return GoTo(target, entryGates[target]);
            yield return new WaitForSeconds(2f);
            var region = TerraWorld.Live;
            Check("进房间就是陨石坑", region != null && region.SceneName == target, region?.SceneName);
            if (region == null || region.SceneName != target) { RestoreEdits(target, before); Plugin.Log.LogInfo($"[AutoTest] 陨石 合计 {ok}/{all} 不通过"); yield break; }
            var off = region.CellOffset;
            int live = 0;
            Vector2Int top = new Vector2Int(int.MinValue, 0);
            for (int x = at.x - 20; x <= at.x + 20; x++)
                for (int y = at.y - 20; y <= at.y + 6; y++)
                {
                    if (TerraWorld.Get(off.x + x, off.y + y) != CellKind.Meteorite) continue;
                    live++;
                    // A meteorite cell with open air over it, nearest the impact: to stand on.
                    if (!TerraWorld.IsSolid(off.x + x, off.y + y + 1) && !TerraWorld.IsSolid(off.x + x, off.y + y + 2) && !TerraWorld.IsSolid(off.x + x, off.y + y + 3)
                        && (top.x == int.MinValue || Mathf.Abs(x - at.x) < Mathf.Abs(top.x - at.x))) top = new Vector2Int(x, y);
                }
            Vector2 topWorld = CellGrid.CellCenter(off.x + top.x, off.y + top.y);
            float glow = top.x != int.MinValue ? LightMap.DevLightAt(topWorld + Vector2.up * CellGrid.CellSize) : 0f;
            // A lit room has no light map (-1): its glow only counts in the dark.
            Check("陨石块是实心的、会发光", live >= 25 && TerraWorld.IsSolid(off.x + top.x, off.y + top.y) && (glow < 0f || glow > 0.05f), $"场景里 {live} 格，表面一格 {top}，亮度 {glow:F2}");
            Shot(target, "meteor_crater");
            yield return null;

            // Standing on it: Burning, life going; with an Obsidian Skull nothing.
            var hero = HeroController.instance;
            var stats = mod.Stats;
            var inv = mod.Inventory;
            void StandOn() { hero.transform.position = new Vector3(topWorld.x, topWorld.y + CellGrid.CellSize * 0.5f + 0.7f, hero.transform.position.z); }
            PlayerData.instance.isInvincible = false;
            stats.FullHeal();
            Player.PlayerBuffs.Remove(Meteorites.Burning);
            int life0 = stats.Life;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) { StandOn(); yield return null; }
            bool burning = Player.PlayerBuffs.Has(Meteorites.Burning);
            int lost = life0 - stats.Life;
            Shot(target, "meteor_stand");
            yield return null;
            UI.WorldMap.Toggle();
            yield return new WaitForSecondsRealtime(0.5f);
            Shot(target, "meteor_map");
            yield return null;
            if (UI.WorldMap.Open) UI.WorldMap.Toggle();
            Check("踩上去燃烧掉血", burning && lost >= 25 && lost <= 60, $"燃烧 {burning}，1.5 秒掉 {lost} 血");
            stats.FullHeal();
            var savedAcc = inv.Accessories[0];
            inv.Accessories[0] = ItemStack.Of(ItemType.ObsidianSkull);
            yield return new WaitForSeconds(0.3f);
            Player.PlayerBuffs.Remove(Meteorites.Burning);
            life0 = stats.Life;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { StandOn(); yield return null; }
            Check("黑曜石骷髅不烧", !Player.PlayerBuffs.Has(Meteorites.Burning) && stats.Life >= life0, $"燃烧 {Player.PlayerBuffs.Has(Meteorites.Burning)}，血 {life0}→{stats.Life}");
            inv.Accessories[0] = savedAcc;
            PlayerData.instance.isInvincible = true;
            stats.FullHeal();

            // Mining: a copper pickaxe (35%) tinks; a gold one (55%) digs it out as a Meteorite.
            var cell = new Vector2Int(off.x + top.x, off.y + top.y);
            hero.transform.position = new Vector3(topWorld.x + 1.5f, topWorld.y + 1.2f, hero.transform.position.z);
            yield return new WaitForSeconds(0.3f);
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            for (int k = 0; k < 12; k++) { mod.Mine(ItemDb.Get(ItemType.CopperPickaxe), cell); yield return null; }
            bool held = TerraWorld.Get(cell.x, cell.y) == CellKind.Meteorite;
            int Have() => WorldItems.DevCount(ItemType.Meteorite) + Recipes.Count(inv, ItemType.Meteorite);
            int drops0 = Have();
            for (int k = 0; k < 12 && TerraWorld.Get(cell.x, cell.y) == CellKind.Meteorite; k++) { mod.Mine(ItemDb.Get(ItemType.GoldPickaxe), cell); yield return null; }
            yield return new WaitForSeconds(0.6f);
            Check("铜镐挖不动，金镐挖出陨石", held && TerraWorld.Get(cell.x, cell.y) == CellKind.Empty && Have() > drops0,
                $"铜镐后还在 {held}，金镐后 {TerraWorld.Get(cell.x, cell.y)}，陨石（地上加背包）{drops0}→{Have()}");
            mod.SetInfiniteItems(wasInfinite);
            Check("陨石能放成方块", ItemDb.Get(ItemType.Meteorite).Places == CellKind.Meteorite && ItemDb.DropFor(CellKind.Meteorite) == ItemType.Meteorite);

            RestoreEdits(target, before);
            Meteorites.Pending = wasPending;
            PlayerData.instance.isInvincible = false;
            yield return GoTo("Town", "left1");
            Plugin.Log.LogInfo($"[AutoTest] 陨石 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        static void RestoreEdits(string scene, Dictionary<int, ushort> before)
        {
            if (scene == null || before == null) return;
            var edits = WorldEdits.For(scene);
            edits.Clear();
            foreach (var kv in before) edits[kv.Key] = kv.Value;
        }
    }
}
