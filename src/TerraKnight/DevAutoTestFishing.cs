using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fishing"): Terraria's fishing in Hallownest. The data (rules, quests, crates, the renamed acid gear); the
    /// catch rules rolled many times in each area's water at a stage and a fishing power (what comes up, nothing above the
    /// stage); a real cast into the Blue Lake, the bite and the reel; acid refused without the acid hook and fished with it
    /// in Greenpath; every crate opened; the Sleeping Angler woken by the lake, moved into Dirtmouth, his quest fish handed
    /// in for the reward.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FishingTest()
        {
            if (suite == "questfish") { yield return QuestFishTest(); yield break; }
            if (suite == "fishingangler") { yield return AnglerVisibilityTest(); yield break; }
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 钓鱼 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            int stageWas = Shop.DevStage;
            inv.Infinite = false;
            PlayerData.instance.isInvincible = true;


            // ---- data
            Check("规则与宝匣", Fishing.DevRules > 100 && Fishing.DevCrates == 57, $"规则 {Fishing.DevRules} 条，宝匣 {Fishing.DevCrates} 种");
            Check("任务鱼", Fishing.QuestList(5).Count() == 36 && Fishing.QuestList(0).Count() == 6, $"第5阶段 {Fishing.QuestList(5).Count()} 条，第0阶段 {Fishing.QuestList(0).Count()} 条");
            Check("改名", ItemDb.Get(ItemType.LavaFishingHook).Name == Lang.T("酸水钩") && ItemDb.Get(ItemType.HotlineFishingHook).AcidPole && ItemDb.Get(ItemType.HellButterfly).AcidBait,
                  $"{ItemDb.Get(ItemType.LavaFishingHook).Name}、{ItemDb.Get(ItemType.HotlineFishingHook).Name}、{ItemDb.Get(ItemType.LavaCrate).Name}");
            Check("钓竿和鱼饵", ItemDb.Get(ItemType.GoldenFishingRod).FishingPower == 50 && ItemDb.Get(ItemType.MasterBait).BaitPower == 50 && ItemDb.Get(ItemType.WoodFishingPole).Bobber == 360,
                  $"金钓竿 {ItemDb.Get(ItemType.GoldenFishingRod).FishingPower}，大师诱饵 {ItemDb.Get(ItemType.MasterBait).BaitPower}");
            string qtip = ItemDb.Get(Fishing.QuestList(5).First()).Tooltip ?? "";
            Check("任务鱼写圣巢地名", qtip.Contains("抓捕位置") || qtip.Contains("Caught"), qtip);

            // ---- the rules, rolled
            string Top(Dictionary<ItemType, int> d, int n) => string.Join("，", d.OrderByDescending(kv => kv.Value).Take(n)
                .Select(kv => (kv.Key == ItemType.None ? "无" : ItemDb.Get(kv.Key).Name) + " " + (100f * kv.Value / d.Values.Sum()).ToString("F1") + "%"));
            bool AllAtOrBelow(Dictionary<ItemType, int> d, int stage) => d.Keys.All(t => t == ItemType.None || ItemDb.Get(t).QuestFish || ItemDb.Get(t).Rare < 0 || ItemDb.Get(t).Stage <= stage);
            Shop.DevStage = 0;
            var lake = Fishing.DevSimulate("遗忘十字路", false, 30, 3600, 20000);
            Check("十字路蓝湖 渔力30 第0阶段", lake.TryGetValue(ItemType.Bass, out int bass) && bass == lake.Where(kv => kv.Key != ItemType.None).Max(kv => kv.Value) && AllAtOrBelow(lake, 0), Top(lake, 8));
            Shop.DevStage = 2;
            var fog = Fishing.DevSimulate("雾之峡谷", true, 50, 1656, 20000);
            Check("雾之峡谷酸池 渔力50", fog.ContainsKey(ItemType.Obsidifish) && (fog.ContainsKey(ItemType.Tuna) || fog.ContainsKey(ItemType.RedSnapper) || fog.ContainsKey(ItemType.Trout)), Top(fog, 10));
            var city = Fishing.DevSimulate("泪水之城", false, 50, 2600, 20000, crateBuff: true);
            Check("泪水之城 宝匣药水", city.ContainsKey(ItemType.DungeonFishingCrate), Top(city, 10));
            var ways = Fishing.DevSimulate("皇家水道", false, 50, 2600, 20000);
            Check("皇家水道 腐化渔获", ways.ContainsKey(ItemType.Ebonkoi) && AllAtOrBelow(ways, 2), Top(ways, 10));
            var small = Fishing.DevSimulate("泪水之城", false, 50, 125, 20000);
            // Any water of any size is fished as a big one: no small-pool penalty, its catches mostly fish.
            int junk = new[] { ItemType.OldShoe, ItemType.TinCan, ItemType.FishingSeaweed }.Sum(t => small.TryGetValue(t, out int c) ? c : 0);
            Check("温泉小水池照常能钓", small.Keys.Any(t => t != ItemType.None && ItemDb.Get(t).Rare >= 0 && !ItemDb.Get(t).QuestFish) && junk < small.Values.Sum() / 10, Top(small, 8));
            Shop.DevStage = 5;
            var god = Fishing.DevSimulate("神居", false, 80, 1664, 20000, crateBuff: true);
            Check("神居 太空和神圣", god.ContainsKey(ItemType.Damselfish) && (god.ContainsKey(ItemType.HallowedFishingCrateHard) || god.ContainsKey(ItemType.FloatingIslandFishingCrateHard)), Top(god, 10));

            // ---- bait from kills (a Crossroads crawler: the Crossroads' bait, about 1 in 6)
            {
                var got = new Dictionary<ItemType, int>();
                EnemyDrops.DevSink = (t, n) => { got.TryGetValue(t, out int c); got[t] = c + n; };
                Shop.DevStage = 0;
                // One in 200 (FishingCrates.KillDrops): about seven or eight in 1500.
                for (int i = 0; i < 1500; i++) Fishing.KillDrops("Crawler", Vector2.zero, false, "Crossroads_01");
                EnemyDrops.DevSink = null;
                int bait = got.Where(kv => ItemDb.Get(kv.Key).BaitPower > 0).Sum(kv => kv.Value);
                bool home = got.Keys.Where(t => ItemDb.Get(t).BaitPower > 0).All(t => t == ItemType.Worm || t == ItemType.Snail || t == ItemType.Grasshopper || t == ItemType.Firefly
                    || t.ToString().StartsWith("Gold"));
                Check("爬虫掉鱼饵", bait >= 1 && bait < 20 && home, $"1500 只掉鱼饵 {bait} 个：{string.Join("，", got.Where(kv => ItemDb.Get(kv.Key).BaitPower > 0).Select(kv => ItemDb.Get(kv.Key).Name + "×" + kv.Value))}");
            }

            // ---- crates
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Mouse = default;
            var crateTypes = new[] { ItemType.WoodenCrate, ItemType.IronCrate, ItemType.GoldenCrate, ItemType.WoodenCrateHard, ItemType.IronCrateHard, ItemType.GoldenCrateHard,
                ItemType.DungeonFishingCrate, ItemType.OceanCrate, ItemType.CorruptFishingCrate, ItemType.CrimsonFishingCrate, ItemType.HallowedFishingCrate, ItemType.FloatingIslandFishingCrate,
                ItemType.JungleFishingCrate, ItemType.FrozenCrate, ItemType.OasisCrate, ItemType.LavaCrate, ItemType.LavaCrateHard, ItemType.Oyster };
            int opened = 0, empty = 0;
            foreach (var c in crateTypes)
            {
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[10] = ItemStack.Of(c, 3);
                InventoryGui.DevRightClick(10);
                int filled = inv.Slots.Count(s => !s.IsEmpty && s.Type != c);
                bool one = inv.Slots[10].Count == 2;
                if (one) opened++;
                if (filled == 0 && c != ItemType.Oyster) empty++;
                Plugin.Log.LogInfo($"[AutoTest] 钓鱼 打开 {ItemDb.Get(c).Name}：{string.Join("，", inv.Slots.Where(s => !s.IsEmpty && s.Type != c).Select(s => s.Def.Name + "×" + s.Count))}");
            }
            Check("宝匣右键打开", opened == crateTypes.Length, $"打开 {opened}/{crateTypes.Length}，只给了吉欧的 {empty} 个");

            // ---- the Blue Lake: the sleeper, a real cast
            Angler.Clear();
            yield return GoTo("Crossroads_50", EntryGateOf("Crossroads_50"));
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            Check("沉睡的渔夫在蓝湖", Angler.DevSleeperShown, $"在 {Angler.DevSleeperAt}");
            var ponds = Fishing.DevPonds().Where(p => p.Scene == "Crossroads_50" && !p.Acid).OrderByDescending(p => p.Tiles).ToList();
            Check("蓝湖水池", ponds.Count > 0 && ponds[0].Tiles >= 300, ponds.Count > 0 ? $"{ponds[0].Area.min:F1}..{ponds[0].Area.max:F1}，{ponds[0].Tiles} 格" : "没找到");
            if (Angler.DevSleeperShown)
            {
                hero.transform.position = Clear(hero, Angler.DevSleeperAt + Vector2.right * 1.5f + Vector2.up * 0.5f);
                yield return new WaitForSeconds(1.5f);
                Shot("Crossroads_50", "fishing_sleeper");
                yield return null;
                yield return null;
                bool woke = Angler.TryRightClick(Angler.DevSleeperAt);
                Check("叫醒渔夫", woke && Angler.Saved && !Angler.DevSleeperShown);
            }
            if (ponds.Count > 0)
            {
                var b = ponds[0].Area;
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[0] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
                inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MasterBait, 99);
                inv.Accessories[0] = ItemStack.Of(ItemType.AnglerTackleBag, 1);
                inv.RefreshEffective();
                Equipment.Update(inv);
                inv.Selected = 0;
                hero.transform.position = Clear(hero, new Vector2(b.min.x - 1f, b.max.y + 1f));
                yield return new WaitForSeconds(0.5f);
                Vector2 aim = new Vector2(b.min.x + 4f, b.max.y + 0.3f);
                bool cast = Fishing.Use(hero, aim, inv.Current.Def);
                Check("蓝湖抛竿", cast && Fishing.Active, Fishing.DevLast);
                float until = Time.time + 40f;
                while (Fishing.Active && !Fishing.Biting && Time.time < until) yield return null;
                Shot("Crossroads_50", "fishing_bite");
                yield return null;
                yield return null;
                Check("咬钩", Fishing.Biting, Fishing.DevLast + "，" + Fishing.InfoLine());
                int catches = Fishing.Catches;
                Fishing.Use(hero, aim, inv.Current.Def);
                Check("收线", Fishing.Catches == catches + 1 && !Fishing.Active, Fishing.DevLast);
            }

            // ---- acid in Greenpath
            yield return GoTo("Fungus1_01", EntryGateOf("Fungus1_01"));
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            var acid = Fishing.DevPonds().Where(p => p.Acid).OrderByDescending(p => p.Tiles).ToList();
            Check("苍绿之径酸池", acid.Count > 0, acid.Count > 0 ? $"{acid[0].Scene} {acid[0].Tiles} 格" : "没找到");
            if (acid.Count > 0)
            {
                var b = acid[0].Area;
                inv.Accessories[0] = default;
                inv.RefreshEffective();
                Equipment.Update(inv);
                inv.Slots[0] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
                inv.Selected = 0;
                hero.transform.position = Clear(hero, new Vector2(b.center.x, b.max.y + 3f));
                yield return new WaitForSeconds(0.3f);
                Vector2 aim = new Vector2(b.center.x + 2f, b.max.y + 0.3f);
                Check("酸池没有酸水钩不能抛", !Fishing.Use(hero, aim, inv.Current.Def));
                inv.Accessories[0] = ItemStack.Of(ItemType.LavaproofTackleBag, 1);
                inv.RefreshEffective();
                Equipment.Update(inv);
                bool cast = Fishing.Use(hero, aim, inv.Current.Def);
                Check("酸池戴酸水钓具包能抛", cast && Fishing.Active, Fishing.DevLast);
                float until = Time.time + 40f;
                while (Fishing.Active && !Fishing.Biting && Time.time < until)
                {
                    hero.transform.position = Clear(hero, new Vector2(b.center.x, b.max.y + 3f));
                    yield return null;
                }
                int catches = Fishing.Catches;
                if (Fishing.Biting) Fishing.Use(hero, aim, inv.Current.Def);
                Check("酸池钓到", Fishing.Catches == catches + 1, Fishing.DevLast);
                Fishing.Cancel();
            }

            // ---- the Angler in Dirtmouth
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2.5f);
            hero = HeroController.instance;
            var at = TownNpcs.DevPos("Angler");
            Check("渔夫住进德特茅斯", at.HasValue, TownNpcs.DevDescribe());
            if (at.HasValue)
            {
                var fish = Fishing.QuestList(5).First();
                Angler.Quest = fish;
                Angler.QuestDay = (int)(PlayerData.instance.playTime / World.TerraTime.FullDaySeconds);
                Angler.QuestDone = false;
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                Check("任务鱼可钓", Angler.QuestFishToCatch(inv) == fish);
                inv.Slots[5] = ItemStack.Of(fish, 1);
                Check("身上有就不钓", Angler.QuestFishToCatch(inv) == ItemType.None);
                hero.transform.position = Clear(hero, at.Value + Vector2.left * 1f);
                yield return new WaitForSeconds(1.5f);
                var now = TownNpcs.DevPos("Angler").Value;
                hero.transform.position = Clear(hero, now + Vector2.left * 0.8f);
                yield return null;
                bool talked = TownNpcs.TryRightClick(TownNpcs.DevPos("Angler").Value);
                Check("和渔夫说话", talked && TownNpcs.TalkingIsAngler, TownNpcs.ChatText);
                yield return null;
                Shot("Town", "fishing_angler_chat");
                yield return null;
                yield return null;
                long purse = Shop.Purse;
                TownNpcs.AnglerQuest();
                Check("交任务鱼", Angler.QuestsDone == 1 && Angler.QuestDone && inv.Slots[5].IsEmpty && Angler.DevRewards == 1 && Shop.Purse > purse,
                      $"{TownNpcs.ChatText}；钱 {purse}→{Shop.Purse}");
                Shot("Town", "fishing_angler_turnin");
                TownNpcs.AnglerQuest();
                Check("今天不再要鱼", Angler.QuestsDone == 1, TownNpcs.ChatText);
                TownNpcs.Close(false);
            }
            Shop.DevStage = stageWas;
            inv.Infinite = infinite;
            Plugin.Log.LogInfo($"[AutoTest] 钓鱼 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        IEnumerator AnglerVisibilityTest()
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Angler.Clear();
            yield return GoTo("Crossroads_50", "left1");
            yield return new WaitForSeconds(3f);
            var sleeper = GameObject.Find("Terra Sleeping Angler")?.GetComponent<SpriteRenderer>();
            var hero = HeroController.instance;
            bool shown = sleeper != null && sleeper.enabled && sleeper.sprite != null;
            Plugin.Log.LogInfo($"[AutoTest] 渔夫生成 {(shown ? "通过" : "不通过")}：{Angler.DevSleeperAt}");
            if (!shown) yield break;
            hero.transform.position = Clear(hero, Angler.DevSleeperAt + Vector2.right * 2f + Vector2.up);
            yield return new WaitForSeconds(3f);
            var camera = Camera.main;
            Plugin.Log.LogInfo($"[AutoTest] 渔夫渲染：{sleeper.sortingLayerName}/{sleeper.sortingOrder} z={sleeper.transform.position.z}；相机 {camera?.name}");
            LogRenderersAt(Angler.DevSleeperAt, "沉睡渔夫遮挡", 100f);
            void Capture(string name)
            {
                if (camera == null) return;
                var previousTarget = camera.targetTexture;
                var previousActive = RenderTexture.active;
                var target = RenderTexture.GetTemporary(1280, 720, 24);
                var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                var captureObject = new GameObject("Angler diagnostic camera");
                var captureCamera = captureObject.AddComponent<Camera>();
                captureCamera.CopyFrom(camera);
                captureCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                captureCamera.enabled = false;
                captureCamera.cullingMask = 1; // Default-layer world art, without HUD/darkness overlays.
                try
                {
                    captureCamera.targetTexture = target;
                    captureCamera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    pixels.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, name + ".png"), pixels.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(target);
                    Object.Destroy(pixels);
                    Object.Destroy(captureObject);
                }
            }
            bool correctLayer = sleeper.sortingLayerName == "Default" && sleeper.sortingOrder == 0
                && Mathf.Abs(sleeper.transform.position.z - Effects.FxBatch.TownZ) < 0.00001f;
            Plugin.Log.LogInfo($"[AutoTest] 渔夫和城镇居民使用相同绘制层 {(correctLayer ? "通过" : "不通过")}");
            // Isolate world rendering from the game's full-screen darkness mask in this diagnostic capture.
            var blackout = GameObject.Find("Masker Blackout")?.GetComponent<Renderer>();
            bool blackoutEnabled = blackout != null && blackout.enabled;
            if (blackout != null) blackout.enabled = false;
            try
            {
                Capture("angler-fixed-world");
                sleeper.sortingOrder = -1;
                Capture("angler-old-order-world");
            }
            finally
            {
                sleeper.sortingOrder = 0;
                if (blackout != null) blackout.enabled = blackoutEnabled;
            }
            bool woke = Angler.TryRightClick(Angler.DevSleeperAt);
            Plugin.Log.LogInfo($"[AutoTest] 渔夫右键救醒 {(woke && Angler.Saved && !Angler.DevSleeperShown ? "通过" : "不通过")}");
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            bool moved = TownNpcs.DevPos("Angler").HasValue;
            Plugin.Log.LogInfo($"[AutoTest] 渔夫救醒后入住 {(moved ? "通过" : "不通过")}");
        }
    }
}
