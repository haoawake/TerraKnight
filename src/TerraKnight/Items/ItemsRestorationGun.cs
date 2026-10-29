using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        static void AddRestorationGun(List<ItemDef> list)
        {
            list.Add(new ItemDef
            {
                Type = ItemType.Clentaminator, Name = "环境改造枪", Texture = "Item_779",
                Source = ItemSource.Shop, Stage = 4, Rare = 5, Value = 2000000, MaxStack = 1,
                AutoReuse = true, NoMelee = true, HoldOut = true,
                UseTime = .1f, UseAnimation = .1f, UseSound = "Item_34",
                Shoot = ProjKind.RestorationSpray, ShootSpeed = 16f,
                Tooltip = "无需弹药或溶液\n喷射所经之处的泰拉瑞亚物块、背景墙和家具变为掉落物\n恢复被挖掘或陨石改变的原始地形\n容器及其底部承托格不受影响",
            });
        }
    }
}
