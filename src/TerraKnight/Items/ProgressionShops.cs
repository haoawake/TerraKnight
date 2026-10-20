using System.Collections.Generic;
using TerraKnight.Town;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        // Signature purchases keep a reason to visit their NPC; these are not generic kill rewards.
        internal static readonly HashSet<ItemType> ShopOnlyWeapons=new HashSet<ItemType> {
            ItemType.Code1,ItemType.Code2,ItemType.Gatligator,ItemType.PulseBow,
            ItemType.ZapinatorGray,ItemType.ZapinatorOrange,ItemType.Katana,ItemType.SlapHand
        };
        internal static IEnumerable<Stock> ShopStock(string npc,IEnumerable<Stock> original)
        {
            var added=new HashSet<ItemType>();
            if(npc=="Merchant")
            {
                foreach(var t in new[] {ItemType.WoodenSword,ItemType.WoodenBow,ItemType.WandofSparking,ItemType.FinchStaff,ItemType.Katana,
                    ItemType.FlintlockPistol,ItemType.MusketBall,ItemType.WoodenArrow,ItemType.Seed,ItemType.LesserManaPotion,ItemType.WaterCandle})
                { added.Add(t); yield return new Stock(t,0,null); }
                foreach(var pair in new[] {(ItemType.WoodenBoomerang,0),(ItemType.LeatherWhip,0),(ItemType.TinkerersWorkshop,1),(ItemType.Hellforge,2)})
                {added.Add(pair.Item1);yield return new Stock(pair.Item1,pair.Item2,null);}
            }
            foreach(var stock in original)
                if(added.Add(stock.Type)) {
                    int sourceStage=ResourceStages.TryGetValue(stock.Type,out int reviewed) ? reviewed : stock.Stage;
                    yield return new Stock(stock.Type,System.Math.Max(sourceStage,ItemDb.Get(stock.Type).Stage),stock.Phases);
                }
        }
    }
}
