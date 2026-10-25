using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator QuestFishTest()
        {
            int all = 0, ok = 0;
            void Check(string name, bool pass, string detail = "")
            {
                all++; if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 任务鱼 {name} {(pass ? "通过" : "不通过")} {detail}");
            }
            var inv = mod.Inventory;
            int stageWas = Shop.DevStage;
            bool infiniteWas = inv.Infinite;
            inv.Infinite = false;
            PlayerData.instance.isInvincible = true;
            void ClearBag() { System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default; }
            void Ask(ItemType fish)
            {
                Angler.Saved = true; Angler.Pending = 1; Angler.QuestDay = -1;
                Angler.QuestDone = false; Angler.Quest = fish;
            }

            Check("36条、12地区各3条", QuestFishCatalog.All.Length == 36 && QuestFishCatalog.All.GroupBy(q => q.Area).Count() == 12
                && QuestFishCatalog.All.GroupBy(q => q.Area).All(g => g.Count() == 3));
            int[] expected = { 6, 12, 21, 27, 33, 36 };
            for (int stage = 0; stage <= 5; stage++)
                Check("阶段池" + stage, Fishing.QuestList(stage).Count() == expected[stage]
                    && Fishing.QuestList(stage).All(t => QuestFishCatalog.Find(t).Stage <= stage));

            foreach (var q in QuestFishCatalog.All)
            {
                ClearBag(); Shop.DevStage = q.Stage; Ask(q.Fish);
                var d = ItemDb.Get(q.Fish);
                Check(q.Fish + "物品", d.QuestFish && !d.Placeholder && d.MaxStack == 1 && d.Value == 0
                    && d.Stage == q.Stage && Shop.SellValue(ItemStack.Of(q.Fish, 1)) == 0);
                var tex = TerrariaAssets.Texture(d.Texture);
                var px = tex.GetPixels32();
                Check(q.Fish + "内嵌图标", tex.width == 34 && tex.height == 34 && tex.filterMode == FilterMode.Point
                    && px.Any(p => p.a == 0) && px.Any(p => p.a == 255));
                Lang.DevForce("zh-Hans"); ItemDb.Relocalize();
                var text = Angler.QuestButton(inv, "渔夫");
                Check(q.Fish + "中文", d.Name == q.Name && d.Tooltip.StartsWith(q.Tip + "\n")
                    && d.Tooltip.Contains(Fishing.WhereText(q.Fish)) && text.Contains(q.Chat)
                    && text.Contains("抓捕位置") && text.Contains("待办任务：1/3") && text.Contains(q.Name)
                    && Angler.Chat("渔夫") == text);
                Lang.DevForce("en-US"); ItemDb.Relocalize();
                text = Angler.QuestButton(inv, "Angler");
                Check(q.Fish + "英文", d.Name == q.EnglishName && d.Tooltip.StartsWith(q.EnglishTip + "\n")
                    && text.Contains(q.EnglishChat) && text.Contains("Pending quests: 1/3")
                    && text.Contains(q.EnglishName) && Angler.Chat("Angler") == text);
                inv.Slots[4] = ItemStack.Of(q.Fish, 1);
                Check(q.Fish + "背包去重", Angler.QuestFishToCatch(inv) == ItemType.None);
                inv.Slots[4] = default; inv.Mouse = ItemStack.Of(q.Fish, 1);
                Check(q.Fish + "鼠标去重", Angler.QuestFishToCatch(inv) == ItemType.None);
                inv.Mouse = default;
                bool acid = new[] { "苍绿之径", "真菌荒地", "雾之峡谷", "王后花园", "王国边缘" }.Contains(q.Area);
                var own = Fishing.DevSimulate(q.Area, acid, 100, 3600, 1800);
                string other = q.Area == "遗忘十字路" ? "泪水之城" : "遗忘十字路";
                var wrong = Fishing.DevSimulate(other, false, 100, 3600, 800);
                Check(q.Fish + "本地区可钓、异地不可钓", own.ContainsKey(q.Fish) && !wrong.ContainsKey(q.Fish)
                    && own.Keys.All(t => t == ItemType.None || !ItemDb.Get(t).QuestFish || t == q.Fish), q.Area);
                if (q.Stage > 0)
                {
                    Shop.DevStage = q.Stage - 1;
                    var future = Fishing.DevSimulate(q.Area, acid, 100, 3600, 400);
                    Check(q.Fish + "不可越阶段", !future.ContainsKey(q.Fish));
                    Shop.DevStage = q.Stage;
                }
                Ask(q.Fish);
                int done = Angler.QuestsDone, rewards = Angler.DevRewards;
                inv.Slots[4] = ItemStack.Of(q.Fish, 1);
                string turnedIn = Angler.QuestButton(inv, "Angler");
                Check(q.Fish + "新交付与无任务台词", turnedIn.Contains("you get the reward")
                    && turnedIn.Contains("Pending quests: 0/3") && Angler.Chat("Angler").Contains("Come back tomorrow"));
                Check(q.Fish + "交付奖励与当日结束", Angler.QuestsDone == done + 1 && Angler.DevRewards == rewards + 1
                    && Angler.QuestDone && Angler.Pending == 0 && !inv.Slots.Any(s => !s.IsEmpty && s.Type == q.Fish));
                Check(q.Fish + "交付后不可钓", Angler.QuestFishToCatch(inv) == ItemType.None);
                yield return null;
            }

            ClearBag(); Shop.DevStage = 0; Ask(ItemType.Batfish);
            Check("旧任务迁移不增单", QuestFishCatalog.Find(Angler.QuestFishToCatch(inv)) != null && Angler.Pending == 1);
            Angler.RefreshAt(Angler.QuestDay + 10);
            Check("每日累积封顶3", Angler.Pending == 3);
            Angler.RefreshAt(Angler.QuestDay - 5);
            Check("回退时钟不增单", Angler.Pending == 3);
            var old = Angler.Quest;
            inv.Slots[4] = ItemStack.Of(old, 1);
            Angler.QuestButton(inv, "Angler");
            Check("多单连续交付", Angler.Pending == 2 && !Angler.QuestDone && Angler.Quest != old);

            // Real native ponds and the normal bobber, bite timer, roll and reel paths.
            foreach (bool acid in new[] { false, true })
            {
                string scene = acid ? "Fungus1_01" : "Crossroads_50";
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                ClearBag(); System.Array.Clear(inv.Accessories, 0, inv.Accessories.Length);
                Shop.DevStage = 0;
                var target = acid ? ItemType.HkQuestFish04 : ItemType.HkQuestFish01;
                Ask(target);
                var ponds = Fishing.DevPonds().Where(p => p.Scene == scene && p.Acid == acid).OrderByDescending(p => p.Tiles).ToList();
                Check(scene + "原生水池", ponds.Count > 0);
                if (ponds.Count == 0) continue;
                var bounds = ponds[0].Area;
                Vector2 stand = new Vector2(bounds.center.x, bounds.max.y + 3f);
                Vector2 aim = new Vector2(bounds.center.x + 1f, bounds.max.y + 0.2f);
                hero.transform.position = Clear(hero, stand);
                inv.Slots[0] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
                inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MasterBait, 999);
                inv.Selected = 0; inv.RefreshEffective(); Equipment.Update(inv);
                if (acid)
                {
                    Check("酸水无钓具拒绝", !Fishing.Use(hero, aim, inv.Current.Def));
                    inv.Slots[0] = ItemStack.Of(ItemType.HotlineFishingHook, 1);
                    Check("酸水竿放行", Fishing.Use(hero, aim, inv.Current.Def)); Fishing.Cancel();
                    inv.Slots[0] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
                    inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.HellButterfly, 999);
                    Check("酸水饵放行", Fishing.Use(hero, aim, inv.Current.Def)); Fishing.Cancel();
                    inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MasterBait, 999);
                    inv.Accessories[0] = ItemStack.Of(ItemType.LavaproofTackleBag, 1);
                }
                else inv.Accessories[0] = ItemStack.Of(ItemType.AnglerTackleBag, 1);
                inv.RefreshEffective(); Equipment.Update(inv);
                float until = Time.time + 100f;
                while (!inv.Slots.Any(s => !s.IsEmpty && s.Type == target) && Time.time < until)
                {
                    hero.transform.position = Clear(hero, stand);
                    if (!Fishing.Active || Fishing.Biting) Fishing.Use(hero, aim, inv.Current.Def);
                    yield return null;
                }
                Check(scene + "自然咬钩收线任务鱼", inv.Slots.Any(s => !s.IsEmpty && s.Type == target), Fishing.DevLast);
                Fishing.Cancel();
                int before = Angler.QuestsDone;
                yield return GoTo("Town", "left1"); yield return new WaitForSeconds(2f);
                var npc = TownNpcs.DevPos("Angler");
                Check("渔夫在城镇", npc.HasValue);
                if (npc.HasValue)
                {
                    hero = HeroController.instance;
                    hero.transform.position = Clear(hero, npc.Value + Vector2.right * 2f + Vector2.up);
                    yield return new WaitForSeconds(0.3f);
                    TownNpcs.TryRightClick(npc.Value); TownNpcs.AnglerQuest();
                    Check(scene + "NPC交付", Angler.QuestsDone == before + 1 && Angler.QuestDone);
                    Shot("Town", acid ? "questfish_acid_turnin" : "questfish_water_turnin");
                    yield return null; yield return null;
                }
            }

            ClearBag();
            foreach (var q in QuestFishCatalog.All) inv.Slots[10 + System.Array.IndexOf(QuestFishCatalog.All, q)] = ItemStack.Of(q.Fish, 1);
            InventoryGui.Open = true;
            foreach (string culture in new[] { "zh-Hans", "en-US" })
            {
                Lang.DevForce(culture); ItemDb.Relocalize();
                yield return new WaitForSeconds(0.5f);
                Shot("Town", "questfish_inventory_" + culture); yield return null; yield return null;
            }
            InventoryGui.Open = false;
            Lang.DevForce(null); Shop.DevStage = stageWas; inv.Infinite = infiniteWas;
            Plugin.Log.LogInfo($"[AutoTest] 任务鱼合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
