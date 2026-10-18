using TerraKnight.World;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        // Original combat and tool stats; acquisition of unavailable world materials is adapted in the shops.
        static ItemDef SupplementDef(ItemType type, string name, int id, int rare, int value, int stage, ItemSource source)
        {
            var d = Mat(type, name, id, rare, "材料");
            d.Value = source == ItemSource.Shop ? System.Math.Max(value, 100) : value; d.Stage = stage; d.Source = source;
            void Accessory(Bonuses b, string tip) { d.Accessory = AccessoryEffect.Passive; d.Bonus = b; d.MaxStack = 1; d.Tooltip = tip; }
            void Weapon(int damage, float kb, int anim, int use = 0)
            {
                d.UseSound = "Item_1"; d.Material = false; d.Class = DamageClass.Melee; d.Damage = damage; d.Knockback = kb; d.MaxStack = 1;
                d.UseAnimation = anim * Tick; d.UseTime = (use == 0 ? anim : use) * Tick; d.Tooltip = null;
            }
            switch (type)
            {
                case ItemType.WoodFishingPole: d.FishingPower = 5; break;
                case ItemType.ReinforcedFishingPole: d.FishingPower = 15; break;
                case ItemType.FisherofSouls: d.FishingPower = 20; break;
                case ItemType.Fleshcatcher: d.FishingPower = 22; break;
                case ItemType.Worm: d.BaitPower = 25; d.Tooltip = "25% 鱼饵力"; break;
                case ItemType.DyeVat: d.PlacesStation = StationKind.DyeVat; d.Consumable = true; d.UseAnimation = 15 * Tick; d.UseTime = 10 * Tick; break;
                case ItemType.Stinkbug: d.BaitPower = 10; d.Tooltip = "10% 鱼饵力\n商人出售；也用于制作臭虫剑"; break;
                case ItemType.FossilPickaxe: Weapon(8, 4f, 18, 14); d.PickPower = 55; break;
                case ItemType.Flymeal: Weapon(15, 5f, 17); d.AutoReuse = false; d.Tooltip = "可驱赶城镇居民"; break;
                case ItemType.HallowJoustingLance:
                    Weapon(90, 13f, 24); d.Channel = true; d.HoldOut = true; d.NoMelee = true; d.NoUseGraphic = true;
                    d.Shoot = ProjKind.HallowLance; d.ShootSpeed = 3.5f; d.Tooltip = "按住攻击持续架枪\n朝枪尖冲刺时伤害随移动速度提高"; break;
                case ItemType.ThunderStaff:
                case ItemType.LightningStrike:
                    bool upgraded = type == ItemType.LightningStrike;
                    Weapon(upgraded ? 150 : 24, upgraded ? 10f : 3f, upgraded ? 55 : 17);
                    d.Class = DamageClass.Magic; d.Mana = upgraded ? 30 : 6; d.Crit = upgraded ? 10 : 0;
                    d.Shoot = upgraded ? ProjKind.LightningStrikeBolt : ProjKind.ThunderBolt;
                    d.ShootSpeed = 8f; d.NoMelee = true; d.HoldOut = true; d.StaffHold = true; d.UseSound = "Item_20";
                    d.Tooltip = upgraded ? "发射闪电，命中后追击附近敌人" : "发射曲折前进的霹雳"; break;
                case ItemType.BrickLayer: Accessory(new Bonuses { BlockSpeed = .5f }, "物块放置速度提高 50%"); break;
                case ItemType.PortableCementMixer: Accessory(new Bonuses { WallSpeed = .5f }, "背景墙放置速度提高 50%"); break;
                case ItemType.ExtendoGrip: Accessory(new Bonuses { BlockRange = 3 }, "放置距离增加 3 格"); break;
                case ItemType.PaintSprayer: Accessory(default, "建筑师发明背包的组件\n圣巢尚未接入方块喷漆"); break;
                case ItemType.ArchitectGizmoPack:
                    Accessory(new Bonuses { BlockSpeed = .5f, WallSpeed = .5f, BlockRange = 3 }, "提高物块和墙的放置速度与距离\n圣巢尚未接入方块喷漆"); break;
                case ItemType.HandOfCreation:
                    Accessory(new Bonuses { BlockSpeed = .5f, WallSpeed = .5f, BlockRange = 3, PickSpeed = .25f, TreasureMagnet = true, PortableStool = true },
                        "提高放置速度与距离、挖掘速度\n增加拾取范围，可使用便携式凳子\n圣巢尚未接入方块喷漆"); break;
                case ItemType.HighTestFishingLine: Accessory(new Bonuses { FishingLine = true }, "钓鱼线永不折断"); break;
                case ItemType.AnglerEarring: Accessory(new Bonuses { FishingSkill = 10 }, "钓鱼力增加 10"); break;
                case ItemType.TackleBox: Accessory(new Bonuses { TackleBox = true }, "降低鱼饵消耗概率"); break;
                case ItemType.LavaFishingHook: Accessory(new Bonuses { AcidFishing = true }, "允许在圣巢酸液中钓鱼"); break;
                case ItemType.AnglerTackleBag:
                case ItemType.LavaproofTackleBag:
                    Accessory(new Bonuses { FishingSkill = 10, FishingLine = true, TackleBox = true, AcidFishing = type == ItemType.LavaproofTackleBag },
                        "钓鱼力增加 10，钓鱼线永不折断，降低鱼饵消耗" + (type == ItemType.LavaproofTackleBag ? "\n允许在圣巢酸液中钓鱼" : "")); break;
                case ItemType.BonePlatform:
                    d.Places = CellKind.Platform; d.Consumable = true; d.UseTime = 7 * Tick; d.UseAnimation = 15 * Tick;
                    d.Tooltip = "可放置平台\n两个平台可还原为一根骨头"; break;
            }
            if (id >= 5139 && id <= 5146)
                Accessory(new Bonuses { BobberKind = id - 5138 }, "钓鱼力增加 10" + (id > 5139 ? "\n使浮标发光" : ""));
            if (d.FishingPower > 0)
            {
                d.MaxStack = 1; d.AutoReuse = false; d.HoldOut = true; d.NoMelee = true;
                d.UseAnimation = d.UseTime = 8 * Tick;
                d.Tooltip = d.FishingPower + "% 钓鱼力\n对水面点击抛竿，浮标跳动时再次点击收线\n需要背包中有鱼饵；酸液需要防熔岩钓钩";
            }
            return d;
        }
    }
}
