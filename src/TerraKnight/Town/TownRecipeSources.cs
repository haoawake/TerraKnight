using System.Collections.Generic;
using TerraKnight.Items;

namespace TerraKnight.Town
{
    internal static partial class TownNpcs
    {
        static readonly Dictionary<(ItemType,string),string> recipeSources=new Dictionary<(ItemType,string),string>();
        internal static string RecipeSources(ItemType type)
        {
            var key=(type,Lang.Culture);if(recipeSources.TryGetValue(key,out string cached))return cached;
            var lines=new List<string>();
            foreach(var npc in AllDefs){
                if(npc.Internal=="TravelingMerchant")continue;
                foreach(var stock in npc.Items)if(stock.Type==type){
                    int stage=System.Math.Max(npc.Stage,System.Math.Max(stock.Stage,ItemDb.Get(type).Stage));
                    int spell=SpellRewards.SpellOf(type);
                    string text=spell>0?DisplayName(npc)+" · "+SpellRewards.LockText(spell):Lang.F("{0} · 阶段 {1} 起",DisplayName(npc),stage);
                    if(stock.Phases!=null&&stock.Phases.Length>0)text+=" · "+Lang.T("月相")+" "+string.Join("/",stock.Phases);
                    if(!lines.Contains(text))lines.Add(text);
                }
            }
            foreach(var stock in travelPool)if(stock.Type==type)
                lines.Add(Lang.F("旅商随机出售 · 阶段 {0} 起",System.Math.Max(stock.Stage,ItemDb.Get(type).Stage)));
            return recipeSources[key]=string.Join("\n",lines);
        }
    }
}
