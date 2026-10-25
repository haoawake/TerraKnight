using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The 10-21 batch: the weapons, the tool and the accessories of Terraria 1.4.5 the mod was still missing (the user's,
        /// "电弧涌动是不是没做进去？…重新找一下有没有还没做进去的饰品，装备，武器"). Item.SetDefaults' numbers (I:47556, 38154,
        /// 36975, 10757, 35696, 39367, 39379, 40613, 6128, 20861); names and tooltips are Terraria's zh-Hans. The Paper Airplanes
        /// stay out: nothing in vanilla gives them (their drop is Conditions.NeverTrue).
        /// </summary>
        static void AddBatch1021(List<ItemDef> list)
        {
            // DefaultToStaff(1122, 8, 18, 18), useTime 6: three bolts a use, the item not drawn (noUseGraphic).
            var arc = Staff(ItemType.ArcSurge, "电弧涌动", 6173, 180, 4f, 18, 8f, 18, ProjKind.ArcSurgeBolt, "Item_15", 50000, 5, rare: 10,
                tip: "释放强力闪电轰击敌人\n“无限力量！”");
            arc.UseTime = 6 * Tick;
            arc.AutoReuse = true;
            arc.NoUseGraphic = true;
            list.Add(arc);
            // DefaultToBow(19, 9, autoReuse): its arrows always blood ones, from the sky (Pl:47877-47909, 54148).
            var bow = Bow(ItemType.BloodRainBow, "血雨弓", 4381, 14, 3f, 19, 9f, 50000, 1, rare: 3, tip: "鲜血从天而降\n“血腥统治”");
            bow.AutoReuse = true;
            list.Add(bow);
            // I:36975: useTime 11 in a swing of 27, hammer 80, axe 30 (150%).
            list.Add(new ItemDef
            {
                Type = ItemType.BloodHamaxe, Name = "血锤斧", Texture = "Item_4317", Class = DamageClass.Melee, Damage = 30, Knockback = 7f,
                UseAnimation = 27 * Tick, UseTime = 11 * Tick, Rare = 4, HammerPower = 80, AxePower = 30, UseSound = "Item_1",
            });
            // I:10757: the Musket's crimson twin, 20 ticks, no autoReuse, Item11.
            var gun = Gun(ItemType.TheUndertaker, "夺命枪", 800, 19, 2f, 20, 6f, "Item_11", 75000, 1, rare: 1, scale: 0.9f);
            gun.AutoReuse = false;
            list.Add(gun);
            // DefaultToSpear(730, 3.5, 28): its bolt at the thrust's start (TerraProjectilesBatch1021).
            var spear = Spear(ItemType.ThunderSpear, "风暴长矛", 4061, 14, 6f, 28, ProjKind.ThunderSpearThrust, 3.5f, 15000, 1, rare: 1);
            spear.Scale = 1f;
            spear.AutoReuse = false;
            list.Add(spear);
            // DefaultToSpear(877 / 878, 3.5, 24), channel: held out while the button is.
            ItemDef Lance(ItemType t, string name, int id, int damage, float kb, ProjKind proj, int rare, string tip)
            {
                var d = Spear(t, name, id, damage, kb, 24, proj, 3.5f, 0, 3, rare, tip);
                d.Channel = true;
                d.Scale = 1f;
                d.AutoReuse = false;
                return d;
            }
            list.Add(Lance(ItemType.JoustingLance, "骑枪", 4788, 60, 12f, ProjKind.JoustingLanceThrust, 4, "提升速度来加强攻击力\n“纳命来！”"));
            list.Add(Lance(ItemType.ShadowJoustingLance, "暗影骑枪", 4789, 130, 14f, ProjKind.ShadowJoustingLanceThrust, 8, "提升速度来加强攻击力"));
            // DefaultToStaff(950, 10, 25, 18): the ring where the line to the cursor meets a block (Pl:48179-48184).
            var scepter = Staff(ItemType.PrincessWeapon, "共鸣权杖", 5065, 70, 5f, 25, 10f, 18, ProjKind.PrincessRing, "Item_43", 500000, 5, rare: 5);
            scepter.AutoReuse = true;
            list.Add(scepter);
            // Player.UpdateEquips: the Toolbelt's blockRange + 1 (Pl:15537), the Toolbox's tileRangeX and Y + 1 (Pl:15066).
            list.Add(Acc(ItemType.Toolbelt, "工具腰带", 407, 1000, 0, rare: 3, tip: "物块放置范围扩大1格", bonus: new Bonuses { BlockRange = 1 }));
            list.Add(Acc(ItemType.Toolbox, "工具箱", 1923, 500, 1, rare: 2, tip: "物块放置范围和工具范围扩大1格", bonus: new Bonuses { TileRangeX = 1, TileRangeY = 1 }));
        }

        /// <summary>Their stage, source and value (Item.value, copper).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> Batch1021Sources()
        {
            // The Martian Saucer's 1 in 50: Oro & Mato carry its drops (EssentialDrops).
            yield return (ItemType.ArcSurge, 5, ItemSource.Drop, 50000);
            // The Blood Moon's fishing enemies' 1 in 8 (beside the Vampire Frog Staff): Loodle's and the Fool Eater's.
            yield return (ItemType.BloodRainBow, 1, ItemSource.Drop, 50000);
            // Hardmode's Blood Moon fishing (the Hemogoblin Shark, the Blood Eel): the Primal Aspid's, beside the Sharp Tears.
            yield return (ItemType.BloodHamaxe, 4, ItemSource.Drop, 100000);
            // A Crimson Heart's, the Musket's twin: the Mosskin's, beside the Musket.
            yield return (ItemType.TheUndertaker, 1, ItemSource.Drop, 75000);
            // The Oasis Crate's (FishingAreaCrates).
            yield return (ItemType.ThunderSpear, 1, ItemSource.Drop, 15000);
            // The Zoologist's in hardmode (Chest.SetupShop case 23).
            yield return (ItemType.JoustingLance, 3, ItemSource.Shop, 60000);
            // The Dungeon Spirit's 1 in 25: the Wingmould carries its drops.
            yield return (ItemType.ShadowJoustingLance, 5, ItemSource.Drop, 500000);
            // The Princess drops it in hardmode; nobody dies in Dirtmouth, so she sells it.
            yield return (ItemType.PrincessWeapon, 5, ItemSource.Shop, 500000);
            // The Goblin Tinkerer's (Chest.SetupShop case 6); the Toolbox comes out of Christmas presents, so him too.
            yield return (ItemType.Toolbelt, 0, ItemSource.Shop, 100000);
            yield return (ItemType.Toolbox, 1, ItemSource.Shop, 50000);
        }
    }
}
