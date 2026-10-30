using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator HomeEverywhereTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Private copy required");
            int checks=0,failures=0;
            var results=new List<object>();
            void Check(string name,bool pass,string detail="")
            {
                checks++;if(!pass)failures++;results.Add(new {name,pass,detail});
                Plugin.Log.LogInfo($"[AutoTest] 哪都是家 {name}：{detail} {(pass?"通过":"不通过")}");
            }
            int stageWas=Shop.DevStage;bool infiniteWas=TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);Shop.DevStage=5;
            mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            try
            {
                yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(2f);
                var hero=HeroController.instance;var inv=mod.Inventory;inv.Infinite=false;
                Stations.Clear();Chests.Clear();
                Check("向导开售5000吉欧且可叠放",Shop.PriceOf(ItemDb.Get(ItemType.HomeEverywhere))==500000
                    && ItemDb.Get(ItemType.HomeEverywhere).MaxStack>1
                    && Town.TownNpcs.DevSourcesAt(0).Any(x=>x.Item1=="Guide"&&x.Item2==ItemType.HomeEverywhere));
                var palette=new HashSet<Color32>();for(int i=0;i<64;i++)palette.Add(Stations.HomeColor(i));
                Check("64个颜色互不相同",palette.Count==64);
                var cell=CellGrid.WorldToCell(new Vector2(30f,12f));
                int gx=cell.x+3,gy=cell.y-1;float cs=CellGrid.CellSize;
                // A stable flat test platform in this private world, with enough room for the three-tile object.
                for(int x=gx-4;x<=gx+34;x++)for(int y=gy;y<=gy+9;y++)TerraWorld.Set(x,y,y==gy?CellKind.Stone:CellKind.Empty);
                yield return new WaitForSeconds(.5f);
                hero.transform.position=Clear(hero,CellGrid.CellCenter(gx-2,gy+3));
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                yield return new WaitForSeconds(2f);
                inv.Selected=0;inv.Override=-1;inv.Slots[0]=ItemStack.Of(ItemType.HomeEverywhere,2);
                mod.DevUseSelected(new Vector2((gx+1.5f)*cs,(gy+1)*cs+.2f));
                yield return new WaitForSeconds(.5f);
                var homeA=Stations.Homes().FirstOrDefault();
                Check("真实放置消耗一件",homeA!=null&&inv.Slots[0].Count==1,Stations.DevWhy??"");
                if(homeA==null)yield break;
                var aLocal=homeA.Local;
                var bLocal=aLocal+Vector2.right*(50*cs);
                Stations.PlaceIn(StationKind.HomeEverywhere,homeA.Scene,bLocal);
                var homes=Stations.Homes();var homeB=homes.Last();
                Check("连续放置按空闲颜色分配",homeA.Color==0&&homeB.Color==1);
                Check("不能选已占用颜色",!Stations.ChangeHome(homeB.Key,homeA.Color,"错误颜色"));
                Check("能改成未占用颜色并命名",Stations.ChangeHome(homeA.Key,63,"家·甲 远程仓库")&&Stations.Home(homeA.Key).Color==63);
                homeA=Stations.Home(homeA.Key);
                var bounds=Stations.HomeBounds(homeA);
                Check("十格边界包含，第十一格排除",Stations.HomeContains(homeA.Key,homeA.Scene,new Rect(bounds.center+Vector2.right*10*cs-Vector2.one*.02f,Vector2.one*.04f))
                    &&!Stations.HomeContains(homeA.Key,homeA.Scene,new Rect(bounds.center+Vector2.right*11*cs-Vector2.one*.02f,Vector2.one*.04f)));
                var recipe=Recipes.All.First(r=>!r.IsBought&&r.Station=="砧"&&r.Ingredients.Length==1&&r.Def.MaxStack==1&&r.Ingredients.All(g=>ItemDb.Get(g.Type).MaxStack>=g.Count*5));
                var ingredient=recipe.Ingredients[0];
                var chestA=new ItemStack[40];chestA[0]=ItemStack.Of(ingredient.Type,ingredient.Count*5);
                var chestB=(ItemStack[])chestA.Clone();
                Chests.AddWorld(homeA.Scene,aLocal+Vector2.left*(4*cs),0,chestA);
                Chests.AddWorld(homeB.Scene,bLocal+Vector2.left*(4*cs),0,chestB);
                Stations.PlaceIn(StationKind.IronAnvil,homeA.Scene,aLocal+Vector2.right*(4*cs));
                Stations.PlaceIn(StationKind.WorkBench,homeB.Scene,bLocal+Vector2.right*(4*cs));
                // Two Safes (the same bank, listed once) and a Piggy Bank, which no home takes in (the user's 10-30).
                Stations.PlaceIn(StationKind.Safe,homeA.Scene,aLocal+Vector2.right*(6*cs));
                Stations.PlaceIn(StationKind.Safe,homeA.Scene,aLocal+Vector2.right*(8*cs));
                Stations.PlaceIn(StationKind.PiggyBank,homeA.Scene,aLocal+Vector2.right*(10*cs));
                Chests.AddWorld(homeA.Scene,aLocal+Vector2.right*(12*cs),0,new ItemStack[40]);
                Stations.PlaceIn(StationKind.IronAnvil,homeA.Scene,aLocal+Vector2.right*(15*cs));
                yield return new WaitForSeconds(.5f);
                int GreenCount()=>UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                    .Count(r=>r.name=="Home Connected Green Particles"&&r.isPlaying&&r.particleCount>0&&r.gameObject.activeInHierarchy);
                Check("范围内箱子与制作站显示发光绿色粒子，范围外不显示",GreenCount()==6,$"实际发光绿色粒子 {GreenCount()} 个，含两个区域；范围外箱子与铁砧不计入");
                Stations.PlaceIn(StationKind.HomeEverywhere,homeA.Scene,aLocal+Vector2.right*cs);
                var overlapHome=Stations.Homes().Last();
                yield return new WaitForSeconds(.25f);
                Stations.NetRemove(homeA.Key);yield return new WaitForSeconds(.25f);
                Check("重叠光环拆掉一个仍保留发光绿色粒子",GreenCount()==7,$"实际 {GreenCount()}；新光环还接入原范围外的箱子");
                Stations.NetRemove(overlapHome.Key);yield return new WaitForSeconds(.25f);
                Check("拆掉最后覆盖家园后粒子即时消失",GreenCount()==2,$"仅乙区保留 {GreenCount()} 个");
                Stations.NetApply(new Net.WorldSync.Record {Key=homeA.Key,Scene=homeA.Scene,X=aLocal.x,Y=aLocal.y,Kind=(int)StationKind.HomeEverywhere,HomeColor=63,HomeOrder=1,HomeName=homeA.Name});
                yield return new WaitForSeconds(.25f);
                Shot("Crossroads_01","home_placed_aura");yield return new WaitForSeconds(.25f);
                Array.Clear(inv.Slots,0,inv.Slots.Length);Array.Clear(inv.HkBag,0,inv.HkBag.Length);inv.Mouse=default;
                InventoryGui.Open=true;yield return new WaitForSeconds(.5f);
                Check("放置后背包出现贴图按钮",InventoryGui.DevRectOf("HomeButton",0).HasValue);
                Check("按钮真实命中打开家园面板",InventoryGui.DevHomeClick("HomeButton",0)&&InventoryGui.HomePanelOpen);
                yield return new WaitForSeconds(.25f);
                Check("色块真实命中选择区域",InventoryGui.DevHomeClick("HomeColor",63)&&Stations.SelectedHome==homeA.Key);
                yield return new WaitForSeconds(.25f);
                var storages=Chests.HomeStorages(homeA.Key);
                Check("普通箱子及私人银行都可连接且同一银行去重",storages.Count==2&&storages.Count(x=>x.Bank==1)==1);
                Check("存钱罐不接入家园",storages.All(x=>x.Bank!=0)&&Stations.HomeBanks(homeA.Key).All(b=>b.Kind!=0));
                Check("从区域真实箱子制作",InventoryGui.DevOffered().Contains(recipe.Result)&&InventoryGui.DevCraftRecipe(recipe)==null,recipe.Def.Name);
                int remaining=Chests.HomeStorages(homeA.Key).First(x=>x.Bank<0).Items[0].Count;
                Check("制作直接扣真实材料",remaining==ingredient.Count*4,$"{remaining}/{ingredient.Count*4}");
                inv.Mouse=default;
                InventoryGui.DevHomeSelect(homeB.Key);
                Check("乙区不能借用甲区铁砧",!InventoryGui.DevOffered().Contains(recipe.Result)&&!Stations.Near("砧",hero));
                InventoryGui.DevHomeSelect(homeA.Key);
                Chests.HomeOpenStorage(0);
                var wood=ItemStack.Of(ItemType.Wood,7);Chests.Put(ref wood);
                Check("远程界面存入真实库存",wood.IsEmpty&&Chests.OpenItems.Any(x=>x.Type==ItemType.Wood&&x.Count==7));
                Chests.LootAll(inv);
                Check("远程界面取出且不复制",inv.Slots.Where(x=>x.Type==ItemType.Wood).Sum(x=>x.Count)==7&&!Chests.OpenItems.Any(x=>!x.IsEmpty&&x.Type==ItemType.Wood));
                var cSlots=Chests.HomeStorages(homeA.Key).First(x=>x.Bank<0).Items;
                cSlots[0]=ItemStack.Of(ingredient.Type,ingredient.Count*3);
                Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;
                Stations.LeaveHome();
                // Travel to another room, leaving the source furniture and chest unloaded.
                yield return GoTo("Town",EntryGateOf("Town"));yield return new WaitForSeconds(1.5f);
                InventoryGui.DevHomeSelect(homeA.Key);Chests.HomeOpenStorage(0);
                yield return new WaitForSeconds(.5f);
                Check("卸载来源地区后不残留发光绿色粒子",GreenCount()==0);
                Check("换地区后仍能打开真实箱子",Chests.IsOpen&&Chests.HomeRemoteOpen&&Chests.OpenItems==cSlots);
                Check("换地区后仍按源区铁砧制作",InventoryGui.DevOffered().Contains(recipe.Result)&&InventoryGui.DevCraftRecipe(recipe)==null);
                inv.Mouse=default;
                var chestKey=Chests.HomeStorages(homeA.Key).First(x=>x.Bank<0).Key;
                var take=new Net.WorldSync.Action {Home=homeA.Key,Keys=new[]{chestKey},Slots=new[]{Net.WorldSync.Stack.From(ItemStack.Of(ingredient.Type,ingredient.Count))},Targets=new[]{0}};
                int beforeTake=cSlots[0].Count;
                Check("房主拒绝占用中的远程材料",!Chests.NetCraftTake(take,1,k=>true)&&cSlots[0].Count==beforeTake);
                take.Home=homeB.Key;
                Check("房主拒绝跨区域取料",!Chests.NetCraftTake(take,1,k=>false)&&cSlots[0].Count==beforeTake);
                take.Home=homeA.Key;
                Check("房主允许卸载地区的本区材料",Chests.NetCraftTake(take,1,k=>false)&&cSlots[0].Count==beforeTake-ingredient.Count);
                cSlots[0]=ItemStack.Of(ingredient.Type,beforeTake);
                take.Keys=new[]{chestKey,chestKey};
                Check("房主拒绝重复箱子键",!Chests.NetCraftTake(take,1,k=>false)&&cSlots[0].Count==beforeTake);
                Shot("Town","home_remote_inventory");yield return new WaitForSeconds(.25f);
                InventoryGui.OpenHomeSettings(homeA.Key);yield return new WaitForSeconds(.5f);
                Check("配置显示64色且占用色不可点击",InventoryGui.DevRectOf("HomeColor",63).HasValue&&!InventoryGui.DevRectOf("HomeColor",1).HasValue);
                Check("配置色块点击真正改色",InventoryGui.DevHomeClick("HomeColor",62)&&Stations.Home(homeA.Key).Color==62);
                InventoryGui.DevHomeClick("HomeColor",63);
                Check("重命名按钮开始输入",InventoryGui.DevHomeClick("HomeAction",2)&&InventoryGui.TypingName);
                yield return null;
                InputSystem.QueueTextEvent(Keyboard.current,'甲');yield return new WaitForSeconds(.15f);
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Enter));yield return null;yield return null;
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return new WaitForSeconds(.15f);
                Check("中文输入并回车保存名称",Stations.Home(homeA.Key)?.Name=="家·甲 远程仓库甲"&&!InventoryGui.TypingName);
                Stations.ChangeHome(homeA.Key,63,"家·甲 远程仓库");
                Shot("Town","home_palette");yield return new WaitForSeconds(.25f);
                int slot=Save.ModSave.DevStateSlot;
                Save.ModSave.Write(slot,mod);
                Check("私人存档写入成功",Save.ModSave.LastWriteSucceeded);
                InventoryGui.Open=false;Stations.Clear();Chests.Clear();
                Save.ModSave.Read(slot,mod);yield return new WaitForSeconds(.25f);
                Check("读档保留颜色名称和原箱库存",Stations.Home(homeA.Key)?.Name=="家·甲 远程仓库"&&Stations.Home(homeA.Key)?.Color==63
                    &&Chests.HomeStorages(homeA.Key).Any(x=>x.Bank<0&&x.Items.Any(s=>s.Type==ingredient.Type&&s.Count==ingredient.Count*2)));
                var records=Stations.NetRecords().Where(r=>r.Kind==(int)StationKind.HomeEverywhere).ToList();
                var json=JsonConvert.SerializeObject(records);Stations.Clear();
                foreach(var record in JsonConvert.DeserializeObject<List<Net.WorldSync.Record>>(json))Stations.NetApply(record);
                Check("联机记录带全颜色顺序和名称",Stations.Home(homeA.Key)?.Color==63&&Stations.Home(homeA.Key)?.Name=="家·甲 远程仓库"&&Stations.Home(homeB.Key)?.Color==1);
                InventoryGui.DevHomeSelect(homeA.Key);Chests.HomeOpenStorage(0);
                bool mined=Stations.NetMine(new Net.WorldSync.Action{Key=homeA.Key,Power=100});
                yield return new WaitForSeconds(.25f);
                Check("拆除后远程连接失效且释放颜色",mined&&Stations.Home(homeA.Key)==null&&!Stations.HomeActive&&!Chests.HomeRemoteOpen&&Stations.ColorAvailable(63,null));
                var full=new List<Stations.Saved>();
                for(int i=0;i<64;i++)full.Add(new Stations.Saved{Scene="HomeCapacityTest",Local=new Vector2(i*4,0),Kind=StationKind.HomeEverywhere,HomeColor=i,HomeOrder=i+1,HomeName="家"+i});
                Stations.Load(full);InventoryGui.Open=false;
                inv.Selected=0;inv.Override=-1;inv.Slots[0]=ItemStack.Of(ItemType.HomeEverywhere,2);
                mod.DevUseSelected(hero.transform.position);
                Check("第65个无法放置且不吞物品",Stations.Homes().Count==64&&Stations.FreeHomeColor()==-1&&inv.Slots[0].Count==2);
                Stations.NetRemove(Net.WorldSync.Key("s","HomeCapacityTest",Vector2.zero));
                Check("回收后优先复用空闲颜色",Stations.FreeHomeColor()==0);
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"home-everywhere-result.json"),JsonConvert.SerializeObject(new{checks,failures,results},Formatting.Indented));
            }
            finally
            {
                InventoryGui.Open=false;Stations.Clear();Chests.Clear();
                Shop.DevStage=stageWas;TerraMod.DevAllInfinite=infiniteWas;PointerOff();
            }
        }
    }
}
