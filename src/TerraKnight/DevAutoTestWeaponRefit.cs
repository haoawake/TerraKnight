using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator WeaponRefitTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int passed = 0, total = 0;
            void Check(string label, bool ok) { total++; if (ok) passed++; Plugin.Log.LogInfo($"[AutoTest] 武器改制 {label}：{(ok ? "通过" : "不通过")}"); }
            var inv = mod.Inventory; int stage = Shop.DevStage; var mode = WeaponMode.Current;
            Shop.DevStage = 5; mod.ApplyCreative(false); inv.Infinite = false; TerraMod.DevAllInfinite = false;
            PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            Player.TerraControls.DevNoRealKeys = true;
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(4f);
            try
            {
                var at = TownNpcs.DevPos("GoblinTinkerer"); Check("哥布林可交互", at.HasValue);
                if (!at.HasValue) yield break;
                var hero = HeroController.instance;
                hero.transform.position = Clear(hero, at.Value + Vector2.left * .8f);
                yield return null; TownNpcs.Close(false); TownNpcs.TryRightClick(at.Value); yield return new WaitForSeconds(.2f);
                Check("对话出现改制入口", InventoryGui.DevRectOf("ChatButton", 10).HasValue);
                InventoryGui.DevHomeClick("ChatButton", 10); yield return new WaitForSeconds(.2f);
                Check("真实按钮打开改制", TownNpcs.RefitMode && InventoryGui.Open);
                Check("拒绝材料", !InventoryGui.DevPutReforge(ItemStack.Of(ItemType.Wood)) && TownNpcs.ReforgeItem.IsEmpty); inv.Mouse = default;
                Check("拒绝饰品和召唤杖", !WeaponRefitting.CanRefit(ItemDb.Get(ItemType.HermesBoots)) && !WeaponRefitting.CanRefit(ItemDb.Get(ItemType.FinchStaff)));
                WeaponMode.Current = WeaponKind.Vanilla;
                var katana = ItemStack.Of(ItemType.Katana); katana.Prefix = 81; katana.Forge = 2; katana.Favorite = true;
                Check("接收武器并保留元数据", InventoryGui.DevPutReforge(katana) && TownNpcs.ReforgeItem.Prefix == 81);
                long cost = WeaponRefitting.Cost(katana); Shop.Earn(cost * 4);
                Check("价格使用基础阶段稀有度且中等", cost >= 25000 && cost <= 382500 && cost == WeaponRefitting.Cost(ItemStack.Of(ItemType.Katana)));
                long purse = Shop.Purse; InventoryGui.DevReleaseRefit();
                Check("原版改新版扣款准确", InventoryGui.DevReforge() && Shop.Purse == purse - cost && TownNpcs.ReforgeItem.WeaponVersion == 2);
                var remade = TownNpcs.ReforgeItem;
                Check("改制保留数量词缀锻造收藏", remade.Count == 1 && remade.Prefix == 81 && remade.Forge == 2 && remade.Favorite);
                Check("单把改制不影响世界和同名武器", WeaponMode.Current == WeaponKind.Vanilla && BladeArts.Active(remade.Def) && !BladeArts.Active(katana.Def));
                Check("按键冷却防止一次连敲反转", !InventoryGui.DevReforge() && Shop.Purse == purse - cost);
                yield return new WaitForSeconds(.15f); Shot("Town", "goblin_weapon_refit_new"); yield return new WaitForSeconds(.2f);
                InventoryGui.DevReleaseRefit(); purse = Shop.Purse;
                Check("新版改回原版扣款准确", InventoryGui.DevReforge() && Shop.Purse == purse - cost && TownNpcs.ReforgeItem.WeaponVersion == 1);
                WeaponMode.Current = WeaponKind.New;
                Check("新版世界的单把原版照常原版", !BladeArts.Active(TownNpcs.ReforgeItem.Def) && BladeArts.Active(ItemStack.Of(ItemType.Katana).Def));
                var packed = Save.ModSave.DevRoundTrip(remade);
                Check("存档读写保留版本词缀锻造", packed.WeaponVersion == 2 && packed.Prefix == 81 && packed.Forge == 2);
                var wire = Net.WorldSync.Stack.From(remade).Item();
                Check("联机容器传递保留版本", wire.WeaponVersion == 2 && wire.Prefix == 81 && wire.Forge == 2);
                var old = Save.ModSave.DevRoundTrip(ItemStack.Of(ItemType.Katana));
                Check("旧武器继续跟随世界", old.WeaponVersion == 0 && BladeArts.Active(old.Def));
                var drop = new WorldItems.Saved { Type = remade.Type, Count = 1, Prefix = remade.Prefix, Forge = remade.Forge, WeaponVersion = remade.WeaponVersion };
                var oldDrops = WorldItems.Save();
                WorldItems.Load(new System.Collections.Generic.List<WorldItems.Saved> { drop });
                Check("地上物品记录保留版本", WorldItems.Save().Any(s => s.Type == remade.Type && s.WeaponVersion == 2 && s.Forge == 2));
                WorldItems.Load(oldDrops);
                TownNpcs.ReforgeItem = remade; TownNpcs.Close(false);
                Check("关闭返还武器保留版本收藏", TownNpcs.ReforgeItem.IsEmpty && inv.Slots.Any(s => s.Type == remade.Type && s.WeaponVersion == 2 && s.Forge == 2 && s.Favorite));
                // Real use of the actual converted stack, not the default item definition.
                WeaponMode.Current = WeaponKind.Vanilla; inv.Slots[0] = remade; inv.Selected = 0; inv.Override = -1; inv.Mouse = default;
                InventoryGui.Open = false;
                int moves = SwordFx.DevArtCuts;
                mod.DevUseSelected((Vector2)hero.transform.position + Vector2.right * 3f); yield return new WaitForSeconds(.15f);
                Check("原版世界改制剑实战使用新招式", SwordFx.DevArtCuts > moves);
                yield return new WaitForSeconds(.7f);
                inv.Slots[0].WeaponVersion = 1; WeaponMode.Current = WeaponKind.New; moves = SwordFx.DevArtCuts;
                mod.DevUseSelected((Vector2)hero.transform.position + Vector2.right * 3f); yield return new WaitForSeconds(.15f);
                Check("新版世界原版剑实战不使用新招式", SwordFx.DevArtCuts == moves);
                var projectiles = TerraProjectiles.Instance;
                projectiles.Clear();
                inv.Slots[0] = remade;
                projectiles.Shoot(ProjKind.WoodenArrow, (Vector2)hero.transform.position + Vector2.up * 3f, Vector2.right * 2f, 10, 0, 0);
                Check("改制武器发射弹幕记录新版", projectiles.DevRemadeShots == 1);
                inv.Slots[0].WeaponVersion = 1;
                projectiles.Shoot(ProjKind.WoodenArrow, (Vector2)hero.transform.position + Vector2.up * 3f, Vector2.right * 2f, 10, 0, 0);
                Check("切换武器不会改变已发射弹幕版本", projectiles.DevRemadeShots == 1);
                projectiles.Clear();
                foreach (var type in new[] { ItemType.SDMG, ItemType.WaterBolt, ItemType.LeatherWhip, ItemType.Terrarian })
                {
                    var s = ItemStack.Of(type); Check(type + "有改制支持", WeaponRefitting.CanRefit(s.Def));
                    s.WeaponVersion = 2; Check(type + "定义保留新版标记", s.Def.WeaponVersion == 2);
                }
                var expensive = remade; Shop.Spend(Shop.Purse); long empty = Shop.Purse;
                Check("钱不够不改变武器", !WeaponRefitting.Convert(ref expensive) && expensive.WeaponVersion == remade.WeaponVersion && Shop.Purse == empty);
            }
            finally { TownNpcs.Close(false); Shop.DevStage = stage; WeaponMode.Current = mode; Player.TerraControls.DevNoRealKeys = false; }
            Plugin.Log.LogInfo($"[AutoTest] 武器改制 合计 {passed}/{total}");
        }
    }
}
