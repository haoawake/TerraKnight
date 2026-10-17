using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "v270a"): the first part of 2.7. The information lines under the minimap (watches, depth meter,
    /// compass and the rest) and their photograph; the version in the settings; the Nail and the Dream Nail kept to
    /// one; the Magic Mirror home; the Rod of Discord to the cursor and its Chaos State; every new crafting station set
    /// down and counting as its station; the buff stations' buffs; the altars in the Temple of the Black Egg; nothing in
    /// any drop table that can be crafted.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator V270aTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 2.7a {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            mod.DevNoFallDamage = true;

            // 1. Nothing craftable drops.
            var bad = new List<string>();
            foreach (var key in EnemyDrops.DevRecords)
                foreach (var d in EnemyDrops.Of(key))
                    if (Recipes.For(d.Type) != null && !bad.Contains(ItemDb.Get(d.Type)?.Name)) bad.Add(ItemDb.Get(d.Type)?.Name);
            Check("能造的不掉落", bad.Count == 0, bad.Count == 0 ? "所有掉落表里没有能合成的物品" : "能合成却在掉落表里：" + string.Join("、", bad));

            // 2. The information lines in Dirtmouth, under the minimap.
            yield return GoTo("Town", "left1");
            pd.isInvincible = true;
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(1f);
            UI.WorldMap.DevStyle(1);
            int slot = Inventory.HotbarSize + 1;
            var infos = new[] { ItemType.GoldWatch, ItemType.WeatherRadio, ItemType.Sextant, ItemType.FishermansGuide, ItemType.MetalDetector, ItemType.LifeformAnalyzer,
                ItemType.Radar, ItemType.TallyCounter, ItemType.DPSMeter, ItemType.Stopwatch, ItemType.Compass, ItemType.DepthMeter };
            for (int i = 0; i < infos.Length; i++) inv.Slots[slot + i] = ItemStack.Of(infos[i]);
            yield return new WaitForSeconds(1.5f);
            Shot("Town", "v270_info");
            yield return null;
            var lines = new List<string>(UI.InfoAccessories.DevLines);
            Check("信息栏 12 行", lines.Count == 12, string.Join(" | ", lines));
            for (int i = 0; i < infos.Length; i++) inv.Slots[slot + i] = default;
            inv.Slots[slot] = ItemStack.Of(ItemType.CopperWatch);
            yield return new WaitForSeconds(0.3f);
            Check("铜表只到整点", UI.InfoAccessories.DevLines.Count == 1 && UI.InfoAccessories.DevLines[0].Contains(":00"), string.Join(" | ", UI.InfoAccessories.DevLines));
            inv.Slots[slot] = default;

            // 3. The version in the settings.
            UI.IngameOptions.Show();
            // The settings stop the game (Terraria's ingameOptionsWindow pause): real time.
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Town", "v270_settings");
            yield return null;
            UI.IngameOptions.Hide();

            // 4. One Nail, one Dream Nail.
            pd.hasDreamNail = true;
            inv.Slots[slot] = ItemStack.Of(ItemType.KnightNail);
            inv.Slots[slot + 1] = ItemStack.Of(ItemType.DreamNail);
            inv.Bank[0] = ItemStack.Of(ItemType.KnightNail);
            inv.Loadouts[1].Bag[3] = ItemStack.Of(ItemType.KnightNail);
            inv.Bank[1] = ItemStack.Of(ItemType.DreamNail);
            yield return new WaitForSeconds(2.5f);
            int Count(ItemType t) => inv.Slots.Concat(inv.Bank).Concat(inv.Loadouts.SelectMany(l => l.Bag)).Count(x => !x.IsEmpty && x.Type == t) + (inv.Mouse.Type == t && !inv.Mouse.IsEmpty ? 1 : 0);
            Check("骨钉、梦之钉各只留一个", Count(ItemType.KnightNail) == 1 && Count(ItemType.DreamNail) == 1, $"骨钉 {Count(ItemType.KnightNail)}，梦之钉 {Count(ItemType.DreamNail)}，收走 {UniqueItems.DevRemoved}");

            // 5. The Rod of Discord: to a point a few tiles up and right, Chaos State; again: a seventh of the life.
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(0.8f);
            Vector2 before = hero.transform.position;
            int life0 = mod.Stats.Life;
            mod.DevUse(ItemType.RodofDiscord, before + new Vector2(6f, 4f));
            yield return new WaitForSeconds(0.6f);
            Vector2 after = hero.transform.position;
            bool chaos = PlayerBuffs.Has(TeleportRods.ChaosState);
            mod.DevUse(ItemType.RodofDiscord, (Vector2)hero.transform.position + new Vector2(-6f, 2f));
            yield return new WaitForSeconds(0.6f);
            Check("混沌传送杖", TeleportRods.DevTeleports >= 2 && (after - before).magnitude > 3f && chaos && mod.Stats.Life <= life0 - mod.Stats.LifeMax / 7 + 1,
                $"从 {before:F1} 到 {after:F1}，混沌状态 {chaos}，第二次后生命 {life0} → {mod.Stats.Life}（最大 {mod.Stats.LifeMax}）");
            mod.Stats.Life = mod.Stats.LifeMax;
            PlayerBuffs.Remove(TeleportRods.ChaosState);

            // 6. Every new station down on the street, each counted as its station; the buff ones give their buff.
            var kinds = new[] { StationKind.Hellforge, StationKind.AdamantiteForge, StationKind.TitaniumForge, StationKind.CrystalBall, StationKind.Bookcase, StationKind.ImbuingStation,
                StationKind.Autohammer, StationKind.AncientManipulator, StationKind.Solidifier, StationKind.Keg, StationKind.CookingPot, StationKind.AlchemyTable, StationKind.HeavyWorkBench,
                StationKind.BoneWelder, StationKind.GlassKiln, StationKind.LivingLoom, StationKind.SkyMill, StationKind.IceMachine, StationKind.LihzahrdFurnace, StationKind.SteampunkBoiler,
                StationKind.FleshCloningVat, StationKind.HoneyDispenser, StationKind.BlendOMatic, StationKind.MeatGrinder, StationKind.AmmoBox, StationKind.BewitchingTable,
                StationKind.SharpeningStation, StationKind.WarTable, StationKind.SliceOfCake };
            float x = 70f;
            int placed = 0;
            var failed = new List<string>();
            foreach (var k in kinds)
            {
                hero.transform.position = new Vector2(x, 12f);
                yield return new WaitForSeconds(0.25f);
                var feet = hero.GetComponent<BoxCollider2D>().bounds;
                bool done = false;
                foreach (float dx in new[] { 2.5f, 3.5f, 4.5f })
                    if (Stations.TryPlace(k, hero, new Vector2(feet.center.x + dx, feet.min.y + 0.3f))) { done = true; break; }
                if (done) placed++; else failed.Add($"{k}（{Stations.DevWhy}）");
                x += 3.2f;
                if ((placed + failed.Count) % 10 == 0)
                {
                    hero.transform.position = new Vector2(x - 16f, 12f);
                    yield return new WaitForSeconds(0.5f);
                    Shot("Town", "v270_stations_" + (placed + failed.Count));
                    yield return null;
                }
            }
            Check("新工作站都能放下", failed.Count == 0, $"放下 {placed}/{kinds.Length}" + (failed.Count > 0 ? "；放不下：" + string.Join("、", failed) : ""));
            // Standing by the Hellforge: it counts as a Hellforge and a Furnace.
            hero.transform.position = new Vector2(Stations.DevBoxOf(StationKind.Hellforge)?.center.x - 1.8f ?? 72f, 12f);
            yield return new WaitForSeconds(0.4f);
            Check("地狱熔炉算地狱熔炉和熔炉", Stations.Near("地狱熔炉", hero) && Stations.Near("熔炉", hero), $"旁边的工作站：{Stations.DevNearNames(hero)}");
            int buffs0 = Stations.DevBuffsTaken;
            foreach (var (k, buff) in new[] { (StationKind.AmmoBox, PotionBuffs.AmmoBox), (StationKind.BewitchingTable, PotionBuffs.Bewitched), (StationKind.SharpeningStation, PotionBuffs.Sharpened),
                (StationKind.WarTable, PotionBuffs.Strategist), (StationKind.SliceOfCake, PotionBuffs.SugarRush), (StationKind.CrystalBall, PotionBuffs.Clairvoyance) })
            {
                var box = Stations.DevBoxOf(k);
                if (box == null) continue;
                hero.transform.position = new Vector2(box.Value.center.x - 1.5f, 12f);
                yield return new WaitForSeconds(0.3f);
                Stations.TryRightClick(box.Value.center);
                yield return null;
                Check($"右键 {k}", PlayerBuffs.Has(buff), $"增益 {buff}：{PlayerBuffs.Has(buff)}");
            }
            Check("增益工作站共给了 6 个", Stations.DevBuffsTaken - buffs0 == 6, $"{Stations.DevBuffsTaken - buffs0}");

            // 7. The Magic Mirror: home.
            int recalls = Pylons.DevRecalls;
            mod.DevUse(ItemType.MagicMirror, hero.transform.position);
            yield return new WaitForSeconds(4f);
            Check("魔镜回家", Pylons.DevRecalls == recalls + 1, $"回家 {Pylons.DevRecalls - recalls} 次，现在在 {GameManager.instance.sceneName}");

            // 8. The altars beside Dirtmouth's bench.
            yield return GoTo("Town", EntryGateOf("Town"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            var demon = FixedStations.DevDemon;
            if (demon != default)
            {
                hero.transform.position = demon + new Vector2(-2f, 1f);
                yield return new WaitForSeconds(1f);
            }
            Check("德特茅斯长椅旁的祭坛", demon != default && Stations.Near("恶魔祭坛 / 猩红祭坛", hero), $"恶魔祭坛 {FixedStations.DevDemon:F1}，猩红祭坛 {FixedStations.DevCrimson:F1}，旁边 {Stations.DevNearNames(hero)}");
            Shot("Town", "v270_altars");
            yield return null;
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 2.7a 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
