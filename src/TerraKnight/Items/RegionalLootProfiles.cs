using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ProgressionLoot
    {
        static readonly string[] regionalOrder={"国王山道","遗忘十字路","苍绿之径","雾之峡谷","真菌荒地","泪水之城",
            "水晶山峰","安息之地","皇家水道","呼啸悬崖","远古盆地","王国边缘","愚人斗兽场","蜂巢","深巢",
            "王后花园","深渊","白色宫殿","黑卵神殿","梦境","格林剧团","神居","野兽巢穴"};

        internal static readonly Dictionary<string,ItemType[]> RegionalProfiles=new Dictionary<string,ItemType[]> {
            ["国王山道"]=new[]{ItemType.CopperOre,ItemType.Gel,ItemType.Feather},
            ["遗忘十字路"]=new[]{ItemType.IronOre,ItemType.Lens,ItemType.RottenChunk,ItemType.Acorn,ItemType.PinkGel,ItemType.VilePowder},
            ["苍绿之径"]=new[]{ItemType.TinOre,ItemType.Vine,ItemType.Daybloom,ItemType.CrispyHoneyBlock},
            ["雾之峡谷"]=new[]{ItemType.SilverOre,ItemType.Amethyst,ItemType.Emerald,ItemType.Coral,ItemType.PixieDust,ItemType.UnicornHorn,ItemType.LightShard,ItemType.Pearlwood,ItemType.PearlsandBlock,ItemType.DjinnLamp},
            ["真菌荒地"]=new[]{ItemType.TungstenOre,ItemType.JungleSpores,ItemType.GlowingMushroom},
            ["泪水之城"]=new[]{ItemType.DemoniteOre,ItemType.Bone,ItemType.Diamond},
            ["水晶山峰"]=new[]{ItemType.Meteorite,ItemType.Topaz,ItemType.Ruby,ItemType.Diamond,ItemType.CrystalShard,ItemType.AshWood,ItemType.FrostCore,ItemType.IceFeather},
            ["安息之地"]=new[]{ItemType.Ectoplasm,ItemType.Amber,ItemType.Moonglow},
            ["皇家水道"]=new[]{ItemType.CrimtaneOre,ItemType.RottenChunk,ItemType.Ichor},
            ["呼啸悬崖"]=new[]{ItemType.GoldOre,ItemType.Feather,ItemType.Sapphire,ItemType.FrostCore,ItemType.IceFeather},
            ["远古盆地"]=new[]{ItemType.Hellstone,ItemType.Obsidian,ItemType.CursedFlame},
            ["王国边缘"]=new[]{ItemType.AdamantiteOre,ItemType.AntlionMandible,ItemType.DarkShard},
            ["愚人斗兽场"]=new[]{ItemType.TitaniumOre,ItemType.SoulofLight,ItemType.UnicornHorn},
            ["蜂巢"]=new[]{ItemType.CobaltOre,ItemType.BeeWax,ItemType.Stinger},
            ["深巢"]=new[]{ItemType.PalladiumOre,ItemType.SpiderFang,ItemType.Cobweb},
            ["野兽巢穴"]=new[]{ItemType.PalladiumOre,ItemType.SpiderFang,ItemType.BlackLens},
            ["王后花园"]=new[]{ItemType.ChlorophyteOre,ItemType.Vine,ItemType.Waterleaf},
            ["深渊"]=new[]{ItemType.OrichalcumOre,ItemType.SoulofNight,ItemType.BlackLens},
            ["白色宫殿"]=new[]{ItemType.MythrilOre,ItemType.PixieDust,ItemType.LightShard},
            ["黑卵神殿"]=new[]{ItemType.CrystalShard,ItemType.Ectoplasm,ItemType.ShadowScale},
            ["梦境"]=new[]{ItemType.ChlorophyteOre,ItemType.Ectoplasm,ItemType.BlackLens},
            ["格林剧团"]=new[]{ItemType.AdamantiteOre,ItemType.SpookyWood,ItemType.FireFeather},
            ["神居"]=new[]{ItemType.LunarOre,ItemType.BeetleHusk,ItemType.ButterflyDust},
        };

        // Regional ore preferences, not unlock gates (see ResourceStages).
        // Keep the existing pool weights: all six Hardmode ores unlock at stage 3,
        // while stage 4 favours the top ores and introduces Chlorophyte.
        internal static readonly ItemType[][] MineralBands = {
            new[]{ItemType.CopperOre,ItemType.TinOre,ItemType.IronOre,ItemType.LeadOre,ItemType.SilverOre,ItemType.TungstenOre,ItemType.GoldOre,ItemType.PlatinumOre},
            new[]{ItemType.DemoniteOre,ItemType.CrimtaneOre},
            new[]{ItemType.Meteorite,ItemType.Hellstone},
            new[]{ItemType.CobaltOre,ItemType.PalladiumOre,ItemType.MythrilOre,ItemType.OrichalcumOre},
            new[]{ItemType.AdamantiteOre,ItemType.TitaniumOre,ItemType.ChlorophyteOre},
            new[]{ItemType.LunarOre}
        };
        internal static int AreaBand(string area) {
            switch(area) {
                case "国王山道":case "遗忘十字路":return 0;
                case "苍绿之径":case "雾之峡谷":case "真菌荒地":case "呼啸悬崖":return 1;
                case "泪水之城":case "水晶山峰":case "安息之地":case "皇家水道":return 2;
                case "深巢":case "野兽巢穴":case "远古盆地":case "王后花园":return 3;
                case "神居":return 5;
                default:return 4;
            }
        }
        internal static ItemType[] PrimaryOres(string area,int stage) {
            int band=Math.Min(Math.Max(0,stage),Math.Min(4,AreaBand(area)+1));
            if(area=="神居" && stage>=5)band=5;
            if(area=="王后花园" && band==4)return new[]{ItemType.ChlorophyteOre};
            var choices=MineralBands[band];
            int index=RegionalVariant(area,0,choices.Length);
            return choices.Length==1 ? choices : new[]{choices[index],choices[(index+1)%choices.Length]};
        }
        internal static bool GodhomeScene(string scene)=>!string.IsNullOrEmpty(scene)
            && World.Areas.AreaName(scene)=="神居";
        static void WeightRegionalOres(string area,int stage,Dictionary<ItemType,float> ores) {
            var primary=new HashSet<ItemType>(PrimaryOres(area,stage));
            // All eight natural surface ores are pre-boss in Terraria; the
            // stronger starter husks provide the alternatives without requiring Dash.
            if(area=="遗忘十字路")foreach(var t in MineralBands[0])
                if(!ores.ContainsKey(t))ores[t]=.5f;
            // Keep each earlier local ore route in the same bounded roll.
            for(int oldStage=0;oldStage<stage;oldStage++)
                foreach(var t in PrimaryOres(area,oldStage))if(!ores.ContainsKey(t))ores[t]=.1f;
            foreach(var t in primary)ores[t]=1;
            float current=0,old=0;
            foreach(var kv in ores) {if(primary.Contains(kv.Key))current+=kv.Value;else old+=kv.Value;}
            foreach(var t in new List<ItemType>(ores.Keys))
                ores[t]=primary.Contains(t) ? ores[t]/current*(old>0 ? .7f : 1f) : ores[t]/old*.3f;
        }

        static int RegionalVariant(string area,int offset,int count)
        {
            int index=Array.IndexOf(regionalOrder,area);
            if(index<0)index=(int)(StableHash(area ?? "")%1000);
            return (index+offset)%Math.Max(1,count);
        }

        static bool ProfileHas(string area,ItemType type)=>RegionalProfiles.TryGetValue(area,out var items) && Array.IndexOf(items,type)>=0;

        internal static string LootArea(string key,EnemyDrops.Entry e)
        {
            string area=e.AreaOverride;
            return area!=null && RegionalProfiles.ContainsKey(area) ? area : BestiaryRoute.AreaOf(key);
        }

        static void AddRegionalProfile(string area,int stage,bool entry,bool final,Dictionary<ItemType,float> ore,Dictionary<ItemType,float> mat)
        {
            if(!RegionalProfiles.TryGetValue(area,out var profile))return;
            foreach(var type in profile)
            {
                if(!ProgressionBalance.Unlocked(type,stage,entry,final))continue;
                var pool=ItemDb.IsOre(type) ? ore : mat;
                // A prominent local candidate, still sharing the bounded pool roll.
                pool[type]=Math.Max(Value(pool,type),1.5f);
            }
        }
    }
}
