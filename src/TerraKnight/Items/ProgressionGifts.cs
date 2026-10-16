using System;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        static float nextGiftCheck;
        internal static void TickGifts(HeroController hero)
        {
            var mod=TerraMod.Instance;
            if(mod==null || hero==null || mod.Inventory.Infinite || !GameHooks.PlayerHasControl() || Time.unscaledTime<nextGiftCheck) return;
            nextGiftCheck=Time.unscaledTime+1;
            var granted=mod.GrantedAbilities;
            if(!granted.Contains("loot.starter.v1") && Shop.Available(hero))
            {
                granted.Add("loot.starter.v1");
                var at=(Vector2)hero.transform.position;
                EnemyDrops.Spill(ItemStack.Of(Core[0][UnityEngine.Random.Range(0,4)][0]),at,true);
                foreach(var t in new[] {ItemType.WoodenArrow,ItemType.Seed,ItemType.MusketBall}) EnemyDrops.Spill(ItemStack.Of(t,100),at,true);
                EnemyDrops.Spill(ItemStack.Of(ItemType.ManaCrystal,1),at,true);
                EnemyDrops.Spill(ItemStack.Of(ItemType.LesserManaPotion,5),at,true);
                EnemyDrops.Spill(ItemStack.Of(ItemType.Wood,20),at,true);
                EnemyDrops.Spill(ItemStack.Of(ItemType.Gel,12),at,true);
            }
            int stage=Mathf.Clamp(Shop.UnlockedStage(),0,5),total=0;
            foreach(var pair in Bestiary.Kills)
            {
                string kind=EnemyDrops.KindOf(pair.Key);
                if(kind=="normal" || kind=="pogo" || kind=="arena") total+=pair.Value;
            }
            string done="loot.explore."+stage;
            if(granted.Contains(done)) return;
            string prefix="loot.start."+stage+".";
            string start=null;
            foreach(var key in granted) if(key.StartsWith(prefix,StringComparison.Ordinal)) {start=key;break;}
            if(start==null) {granted.Add(prefix+total);return;}
            if(!int.TryParse(start.Substring(prefix.Length),out int before) || total-before<20) return;
            granted.Add(done);
            EnemyDrops.Spill(ItemStack.Of(Core[stage][UnityEngine.Random.Range(0,4)][1]),hero.transform.position,true);
        }
    }
}
