using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static class ShopLoot
    {
        static HashSet<ItemType> equipment;
        internal static bool Equipment(ItemType type)
        {
            if(equipment==null)
            {
                var stock=new HashSet<ItemType>();
                foreach(var t in Town.TownNpcs.ShopCatalogue())
                {
                    var d=ItemDb.Get(t);
                    if(d!=null && !d.Placeholder && (d.IsWeapon || d.IsArmor || d.IsAccessory || d.IsMount
                        || d.GameTool || d.PickPower>0 || d.AxePower>0 || d.HammerPower>0
                        || d.FishingPower>0))stock.Add(t);
                }
                equipment=stock;
            }
            return equipment.Contains(type);
        }
    }
}
