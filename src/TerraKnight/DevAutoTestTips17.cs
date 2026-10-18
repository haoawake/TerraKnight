using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17tips"): the Potion Box's tooltip and its buff potions' hover (how many of the 20, kept for good once
    /// favourited), the Piggy Bank's line on the buff stations kept in it, the Hallownest items' lines (carried counts), and
    /// no ordinary enemy's table holding what only fishing gives (bosses' tables as they are).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Tips17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;

            string Tip(ItemType t) => ItemDb.Get(t).Tooltip ?? "";
            string boxTip = Tip(ItemType.PotionBox);
            FxCheck("药水箱说明写了满 20 瓶收藏后永久生效", boxTip.Contains("20") && boxTip.Contains("永久生效"), boxTip.Replace("\n", " / "));
            string piggyTip = Tip(ItemType.PiggyBank);
            FxCheck("存钱罐说明写了放进去的增益站照样有增益", piggyTip.Contains("增益站") && piggyTip.Contains("水晶球") && piggyTip.Contains("蛋糕块"), piggyTip.Replace("\n", " / "));
            string crest = Tip(ItemType.HkCityCrest), soul = Tip(ItemType.HkKingsoul), lantern = Tip(ItemType.HkLumaflyLantern);
            FxCheck("圣巢物品说明写了放在圣巢背包里就算", crest.Contains("放在圣巢背包里就算已获取") && soul.Contains("放在圣巢背包里就算佩戴着") && lantern.Contains("放在圣巢背包里就生效")
                && !crest.Contains("拿在手里才生效"),
                $"纹章「{crest.Split('\n').Last()}」国王之魂「{soul.Split('\n').Last()}」灯笼「{lantern.Split('\n').Last()}」");

            // The Potion Box open, the cursor over a buff potion: 7 of 20; over a favourited one: kept for good.
            var keptSlot = inv.Slots[40];
            inv.Slots[40] = ItemStack.Of(ItemType.PotionBox);
            var keptBox = inv.PotionBox[0].ToArray();
            System.Array.Clear(inv.PotionBox[0], 0, inv.PotionBox[0].Length);
            inv.PotionBox[0][0] = ItemStack.Of(ItemType.IronskinPotion, 7);
            var fav = ItemStack.Of(ItemType.SwiftnessPotion, 20);
            fav.Favorite = true;
            inv.PotionBox[0][1] = fav;
            inv.PotionBox[0][2] = ItemStack.Of(ItemType.GreaterHealingPotion, 30);
            InventoryGui.Open = true;
            yield return null;
            Containers.Toggle(ItemType.PotionBox);
            yield return new WaitForSeconds(0.3f);
            bool open = World.Chests.PotionBoxOpen;
            string Hover(int slot)
            {
                var r = InventoryGui.DevRectOf("ChestSlot", slot);
                if (r == null) return "（没画出第 " + slot + " 格）";
                UI.TerraCursor.DevDrives = true;
                UI.TerraCursor.DevAt = r.Value.center;
                return null;
            }
            string miss = Hover(0);
            yield return null;
            yield return null;
            string tip7 = miss ?? InventoryGui.DevTooltip;
            Shot("Town", "potionbox_hover");
            miss = Hover(1);
            yield return null;
            yield return null;
            string tipFav = miss ?? InventoryGui.DevTooltip;
            Shot("Town", "potionbox_hover_fav");
            miss = Hover(2);
            yield return null;
            yield return null;
            string tipHeal = miss ?? InventoryGui.DevTooltip;
            FxCheck("药水箱里悬浮增益药水显示攒了几瓶和满 20 瓶永久生效", open && tip7.Contains("已攒 7/20 瓶") && tip7.Contains("永久生效"), $"打开 {open}：{tip7.Replace("\n", " / ")}");
            FxCheck("悬浮已收藏的显示永久生效", tipFav.Contains("已收藏") && tipFav.Contains("永久生效"), tipFav.Replace("\n", " / "));
            FxCheck("治疗药水不显示这一行", !tipHeal.Contains("已攒") && !tipHeal.Contains("已收藏："), tipHeal.Replace("\n", " / "));
            Containers.Toggle(ItemType.PotionBox);
            yield return null;

            // The Piggy Bank in the bag, hovered.
            inv.Slots[15] = ItemStack.Of(ItemType.PiggyBank);
            yield return null;
            miss = null;
            var pr = InventoryGui.DevRectOf("Slot", 15);
            if (pr != null) { UI.TerraCursor.DevDrives = true; UI.TerraCursor.DevAt = pr.Value.center; }
            yield return null;
            yield return null;
            string piggyHover = pr != null ? InventoryGui.DevTooltip : "（没画出第 15 格）";
            Shot("Town", "piggy_hover");
            FxCheck("悬浮存钱罐看得到增益站这一行", piggyHover.Contains("增益站"), piggyHover.Replace("\n", " / "));
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevAt = null;
            InventoryGui.Open = false;
            inv.Slots[15] = default;
            inv.Slots[40] = keptSlot;
            System.Array.Copy(keptBox, inv.PotionBox[0], keptBox.Length);
            Player.PlayerBuffs.Remove(3);

            // What only fishing gives: in no ordinary enemy's table at any stage; the bosses' as they are.
            int tables = 0, fishRows = 0, bossFish = 0;
            string first = "";
            foreach (var record in EnemyDrops.DevRecords.ToList())
            {
                bool boss = ProgressionBalance.FormalBoss(record);
                for (int stage = 0; stage <= 5; stage++)
                    foreach (bool firstKill in new[] { false, true })
                    {
                        var rows = EnemyDrops.At(record, stage, firstKill, stage >= 3, stage >= 5);
                        tables++;
                        foreach (var d in rows)
                        {
                            if (!Fishing.FishOnly(d.Type)) continue;
                            if (boss) { bossFish++; continue; }
                            fishRows++;
                            if (first.Length == 0) first = $"{record} 阶段 {stage}：{ItemDb.Get(d.Type).Name}";
                        }
                    }
            }
            bool sets = Fishing.FishOnly(ItemType.Bass) && Fishing.FishOnly(ItemType.FrostDaggerfish) && !Fishing.FishOnly(ItemType.WoodenCrate) && !Fishing.FishOnly(ItemType.OldShoe);
            FxCheck("小怪的掉落表里没有只能钓上来的东西（鱼、鱼类武器、海鲜）", sets && tables > 0 && fishRows == 0,
                $"查了 {tables} 张表，小怪里 {fishRows} 行{(first.Length > 0 ? "（" + first + "）" : "")}，Boss 里 {bossFish} 行；鲈鱼/寒霜飞鱼/木匣/旧鞋判定 {sets}");
            inv.Infinite = infinite;
        }
    }
}
