using System.Collections;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "walls"): the wall behind dug-out rock follows the area (Areas.WallFor). Which
    /// wall a list of scenes gets, then one hole dug in the floor beside the player, shown with
    /// each area's wall in turn and photographed; the hole's edits are taken out of the save after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WallsTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);

            var scenes = new[]
            {
                "Tutorial_01", "Town", "Crossroads_01", "Cliffs_01", "Fungus1_01", "Fungus3_04", "Fungus3_26", "Fungus3_archive", "Fungus2_01", "Ruins1_01",
                "Ruins_House_02", "Waterways_01", "GG_Waterways", "Mines_01", "RestingGrounds_01", "Room_Mansion", "Deepnest_01", "Deepnest_Spider_Town",
                "Deepnest_East_01", "GG_Lurker", "Room_Colosseum_01", "Hive_01", "Abyss_04", "Abyss_18", "Abyss_09", "Abyss_Lighthouse_room", "White_Palace_01",
                "GG_Atrium", "Dream_01_False_Knight", "Grimm_Main_Tent", "Room_temple", "Room_Final_Boss_Core", "Room_Queen", "Room_nailmaster_02",
            };
            var map = new System.Text.StringBuilder();
            foreach (var s in scenes) map.Append(s).Append('=').Append(Areas.WallFor(s)).Append(' ');
            Plugin.Log.LogInfo($"[AutoTest] 背景墙 对照：{map}");

            // A hole in the scene's own floor, 3 to 8 cells ahead and 3 deep.
            float side = Physics2D.Raycast(col.bounds.center, Vector2.right, 6f, 1 << ChunkColliders.TerrainLayer).collider == null ? 1f : -1f;
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            var dug = new List<Vector2Int>();
            for (int k = 3; k <= 8; k++)
            for (int d = 0; d < 3; d++)
            {
                var c = new Vector2Int(feet.x + k * (int)side, feet.y - d);
                if (TerraWorld.Get(c.x, c.y) != CellKind.Original) continue;
                TerraWorld.Set(c.x, c.y, CellKind.Empty);
                dug.Add(c);
            }
            yield return new WaitForSeconds(0.4f);
            var region = TerraWorld.RegionAt(feet.x, feet.y);
            var middle = CellGrid.CellCenter(feet.x + (int)(5.5f * side), feet.y - 1);
            Plugin.Log.LogInfo($"[AutoTest] 背景墙：在 {scene} 脚下挖了 {dug.Count} 格（{region?.SceneName}），本场景的墙 {Areas.WallFor(scene)}");
            var walls = new (string Area, string Sheet)[]
            {
                ("十字路", "Wall_57"), ("石头", "Wall_1"), ("苍绿之径", "Wall_64"), ("王后花园", "Wall_65"), ("雾之峡谷", "Wall_28"), ("真菌荒地", "Wall_187"),
                ("泪水之城", "Wall_7"), ("皇家水道", "Wall_8"), ("水晶山峰", "Wall_48"), ("安息之地", "Wall_58"), ("深巢", "Wall_62"), ("王国边缘", "Wall_178"),
                ("蜂巢", "Wall_86"), ("远古盆地", "Wall_180"), ("深渊", "Wall_79"), ("白色宫殿", "Wall_22"), ("神居", "Wall_10"), ("梦境", "Wall_222"),
                ("格林剧团", "Wall_83"), ("斗兽场", "Wall_87"), ("黑卵神殿", "Wall_20"),
            };
            if (region != null && dug.Count > 0)
            {
                int n = 0;
                foreach (var (area, sheet) in walls)
                {
                    region.DevSetDugWall(sheet);
                    yield return null;
                    yield return null;
                    yield return CloseUpAt(scene, $"wall_{n++:00}_{sheet}", middle, 170, 2);
                }
                region.DevSetDugWall(Areas.WallFor(region.SceneName));
                // Out of the save again (the live grid keeps the hole until the scene is loaded anew).
                var edits = WorldEdits.For(region.SceneName);
                foreach (var c in dug) edits.Remove((c.y - region.CellOffset.y) * region.Grid.Width + c.x - region.CellOffset.x);
            }
            Plugin.Log.LogInfo($"[AutoTest] 背景墙：拍了 {walls.Length} 种墙");

            // A new save's start (TerraMod.ResetPlayer), then the test save read back from its file.
            mod.ResetPlayer();
            for (int f = 0; f < 3; f++) yield return null;
            var inv = mod.Inventory;
            var acc = new System.Text.StringBuilder();
            foreach (var a in inv.Accessories) if (!a.IsEmpty) acc.Append(a.Def.Name).Append(a.Favorite ? "(共享) " : " ");
            var bag = new System.Text.StringBuilder();
            foreach (var s in inv.Slots) if (!s.IsEmpty) bag.Append(s.Def.Name).Append(' ');
            var e = Items.Equipment.Current;
            bool hookOk = Player.GrapplingHook.HookItem(inv) == Items.ItemType.GrapplingHook;
            inv.SwitchLoadout(1);
            for (int f = 0; f < 3; f++) yield return null;
            var acc2 = new System.Text.StringBuilder();
            foreach (var a in inv.EffectiveAccessories) if (!a.IsEmpty) acc2.Append(a.Def.Name).Append(' ');
            Plugin.Log.LogInfo($"[AutoTest] 新存档开局：背包 {bag}；饰品 {acc}；E 键勾爪 {hookOk}；贴墙 {Player.PlayerHooks.Abilities.WallCling}（攀爬爪），二段跳 {Player.PlayerHooks.Abilities.DoubleJump}（云朵瓶）；换到配装 2 后生效的饰品 {acc2}");
            Save.ModSave.Read(GameManager.instance.profileID, mod);
            yield return new WaitForSeconds(0.3f);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            mod.DevNoFallDamage = false;
        }
    }
}
