using System.Collections.Generic;
using TerraKnight.Town;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        internal static IEnumerable<Stock> ShopStock(string npc,IEnumerable<Stock> original)
        {
            var added=new HashSet<ItemType>();
            if(npc=="Merchant")
            {
                foreach(var t in new[] {ItemType.WoodenSword,ItemType.WoodenBow,ItemType.WandofSparking,ItemType.FinchStaff,
                    ItemType.FlintlockPistol,ItemType.MusketBall,ItemType.WoodenArrow,ItemType.Seed,ItemType.LesserManaPotion,ItemType.WaterCandle})
                { added.Add(t); yield return new Stock(t,0,null); }
            }
            foreach(var stock in original)
                if(added.Add(stock.Type)) yield return stock;
        }
    }
}
