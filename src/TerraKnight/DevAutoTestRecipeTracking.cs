using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator GuardedRecipeTracking()
        {
            var stack=new Stack<IEnumerator>();stack.Push(RecipeTrackingTest());
            try {while(stack.Count>0){var next=stack.Peek();bool more;
                try{more=next.MoveNext();}catch(Exception e){Plugin.Log.LogError("[AutoTest] RECIPE_TRACKING 不通过 "+e);break;}
                if(!more){(next as IDisposable)?.Dispose();stack.Pop();continue;}
                if(next.Current is IEnumerator nested)stack.Push(nested);else yield return next.Current;
            }}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
        }
        IEnumerator RecipeTrackingTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(1.5f);
            int checks=0,failures=0,savedStage=Shop.DevStage;
            bool creative=mod.Creative,infinite=TerraMod.DevAllInfinite,drive=TerraCursor.DevDrives;
            var pointer=TerraCursor.DevAt;var inv=mod.Inventory;var beforeBag=(ItemStack[])inv.Slots.Clone();var beforeMouse=inv.Mouse;
            void Check(string what,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] RECIPE_TRACKING {what} {(pass?"通过":"不通过")}");}
            IEnumerator Click(string kind,int index){yield return null;var r=InventoryGui.DevRectOf(kind,index);Check("hit-region/"+kind+"/"+index,r.HasValue);if(r.HasValue){yield return ClickAt(r.Value,.1f);yield return null;}}
            int Ingredient(ItemType type)=>Array.FindIndex(RecipeTracking.Current.Recipe.Ingredients,g=>g.Type==type);
            try {
                Shop.DevStage=5;mod.ApplyCreative(true);TerraCursor.DevDrives=true;
                while(!RecipeTracking.SourcesReady){RecipeTracking.TickSources();yield return null;}
                var hand=RecipeTracking.For(ItemType.HandOfCreation);
                Check("hand-recipe",hand.Count>0&&hand[0].Ingredients.Length==4);
                Check("architect-lower-recipe",RecipeTracking.For(ItemType.ArchitectGizmoPack).Count>0);
                Check("drop-source-chisel",RecipeTracking.Farmable(ItemType.AncientChisel));
                Check("shop-leaf-stool",RecipeTracking.For(ItemType.PortableStool).Count==0&&!RecipeTracking.Farmable(ItemType.PortableStool));
                Check("shop-source-stool",!string.IsNullOrEmpty(TownNpcs.RecipeSources(ItemType.PortableStool)));
                Check("shop-source-traveler",!string.IsNullOrEmpty(TownNpcs.RecipeSources(ItemType.BrickLayer)));
                var spellWeapon=SpellRewards.All.First();
                Check("shop-source-spell-gate",TownNpcs.RecipeSources(spellWeapon).Contains(SpellRewards.LockText(SpellRewards.SpellOf(spellWeapon))));
                foreach(var group in Recipes.All.Where(r=>!r.IsBought).GroupBy(r=>r.Result))
                    Check("complete-recipe-index/"+group.Key,RecipeTracking.For(group.Key).Count==group.Count());
                var platform=RecipeTracking.For(ItemType.WoodPlatform).First();
                var quantity=new RecipeTracking.Node{Type=ItemType.WoodPlatform,Required=platform.Count+1};
                Check("multi-output-quantity",RecipeTracking.Needed(quantity,platform.Ingredients[0].Count)==2*platform.Ingredients[0].Count);
                Check("mixed-craft-and-drop",RecipeTracking.For(ItemType.Wood).Count>0&&RecipeTracking.Farmable(ItemType.Wood));
                RecipeTracking.Start(platform);RecipeTracking.Enter(ItemType.Wood,1);RecipeTracking.Enter(ItemType.WoodPlatform,1);
                Check("conversion-cycle-bounded",RecipeTracking.Path.Count==1);
                RecipeTracking.Clear();
                var at=TownNpcs.DevPos("Guide");Check("guide-present",at.HasValue);
                bool talked=false;var hero=HeroController.instance;PlayerData.instance.isInvincible=true;
                for(int i=0;i<5&&!talked&&at.HasValue;i++){
                    float bottom=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
                    hero.transform.position=Clear(hero,new Vector2(at.Value.x-1.5f,at.Value.y-20f/32f-bottom+.05f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                    yield return new WaitForSecondsRealtime(.4f);
                    talked=TownNpcs.TryRightClick(TownNpcs.DevPos("Guide").Value);
                }
                Check("guide-talk",talked&&TownNpcs.TalkingIsGuide);
                if(!talked)yield break;
                TownNpcs.OpenGuideCraft();inv.Mouse=ItemStack.Of(ItemType.AncientChisel);
                InventoryGui.DevGuideSlot();yield return new WaitForSecondsRealtime(.3f);
                int result=InventoryGui.DevFocusGuideResult(ItemType.HandOfCreation);
                Check("guide-offers-hand",result>=0);yield return null;
                yield return Click("Recipe",result);
                Check("result-opens-tracker",InventoryGui.DevTrackingType==ItemType.HandOfCreation);
                Check("query-never-crafts",inv.Mouse.IsEmpty&&TownNpcs.GuideItem.Type==ItemType.AncientChisel&&TownNpcs.GuideItem.Count==1);
                Shot("Town","recipe_tracking_hand");
                yield return Click("RecipeTraceIngredient",Ingredient(ItemType.ArchitectGizmoPack));
                Check("recursive-lower-tier",InventoryGui.DevTrackingType==ItemType.ArchitectGizmoPack&&InventoryGui.DevTrackingDepth==2);
                Shot("Town","recipe_tracking_architect");
                yield return Click("RecipeTraceAction",1);
                Check("back-preserves-parent",InventoryGui.DevTrackingType==ItemType.HandOfCreation&&InventoryGui.DevTrackingDepth==1);
                InventoryGui.DevBestiarySelect(-13);
                yield return Click("RecipeTraceIngredient",Ingredient(ItemType.AncientChisel));
                Check("drop-opens-book",InventoryGui.BestiaryOpen);
                Check("book-search-filled",InventoryGui.DevBookSearch==ItemDb.Get(ItemType.AncientChisel).Name);
                Check("old-stage-filter-cleared",InventoryGui.DevBestiaryDropStage==-1);
                yield return new WaitForSecondsRealtime(.25f);
                Check("actual-book-results",InventoryGui.DevBookRows().Count>0);
                Shot("Town","recipe_tracking_chisel_book");
                yield return Click("RecipeTraceAction",8);
                Check("book-return-preserves-tree",!InventoryGui.BestiaryOpen&&InventoryGui.DevTrackingType==ItemType.HandOfCreation);
                yield return Click("RecipeTraceIngredient",Ingredient(ItemType.PortableStool));
                Check("shop-leaf-does-not-fake-drop-search",!InventoryGui.BestiaryOpen&&InventoryGui.DevTrackingType==ItemType.PortableStool);
                Shot("Town","recipe_tracking_shop_leaf");
                yield return Click("RecipeTraceAction",2);
                Check("return-target",InventoryGui.DevTrackingType==ItemType.HandOfCreation);
                Check("guide-material-retained",TownNpcs.GuideItem.Type==ItemType.AncientChisel&&TownNpcs.GuideItem.Count==1&&inv.Mouse.IsEmpty);
                int before=Recipes.Count(inv,ItemType.AncientChisel);InventoryGui.Open=false;TownNpcs.Close(false);
                Check("close-clears-tracking",!InventoryGui.DevTrackingActive);
                Check("close-returns-material-once",TownNpcs.GuideItem.IsEmpty&&Recipes.Count(inv,ItemType.AncientChisel)==before+1);
                mod.ApplyCreative(false);TerraMod.DevAllInfinite=false;mod.SetInfiniteItems(false);
                inv.Mouse=default;inv.Slots[30]=ItemStack.Of(ItemType.Wood,10);InventoryGui.Open=true;
                int woodBefore=Recipes.Count(inv,ItemType.Wood);
                Check("normal-crafting-still-works",InventoryGui.DevCraft(ItemType.WoodPlatform));
                Check("normal-crafting-consumes-wood",Recipes.Count(inv,ItemType.Wood)<woodBefore);
            }finally{
                InventoryGui.Open=false;TownNpcs.Close(false);RecipeTracking.Clear();
                Shop.DevStage=savedStage;mod.ApplyCreative(creative);TerraMod.DevAllInfinite=infinite;mod.SetInfiniteItems(infinite);
                Array.Copy(beforeBag,inv.Slots,beforeBag.Length);inv.Mouse=beforeMouse;
                TerraCursor.DevDrives=drive;TerraCursor.DevAt=pointer;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"recipe-tracking.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Scope="Complete recipe index and real Guide UI clicks, recursive materials, source search/return, material safety and normal crafting. Single client."},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] RECIPE_TRACKING {checks} checks {failures} failures {(failures==0?"通过":"不通过")}");
        }
    }
}
