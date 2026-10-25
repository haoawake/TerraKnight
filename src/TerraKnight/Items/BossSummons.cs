using System;
using System.Collections;
using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Consumable rematch seals. Terraria's conditional use/one successful summon per item,
    /// adapted to Hollow Knight's complete native arenas, not detached boss prefab clones.
    /// Portraits remain the game's own art; the seal/runes are drawn procedurally at runtime.
    /// </summary>
    internal static class BossSummons
    {
        public const string IconPrefix = "HK_BossSeal_";
        internal sealed class Seal
        {
            public ItemType Type;
            public string Key, Name, En;
            public int Stage;
            public ItemType Material;
            public int Amount;
            public ItemType SupportMaterial;
            public int SupportAmount;
            /// <summary>Repeatable nearby enemies whose actual stage drops supply each ingredient.</summary>
            public string MaterialSource, SupportSource, Locality;
            public bool Dream;
        }

        static Seal S(ItemType t, string key, string zh, string en, int stage, ItemType mat, int amount, string source,
            ItemType support, int supportAmount, string supportSource, string locality, bool dream = false) =>
            new Seal { Type = t, Key = key, Name = zh, En = en, Stage = stage, Material = mat, Amount = amount,
                MaterialSource = source, SupportMaterial = support, SupportAmount = supportAmount,
                SupportSource = supportSource, Locality = locality, Dream = dream };

        // Each entry has a supported full arena in BossArena; the enum IDs are append-only.
        internal static readonly Seal[] All =
        {
            S(ItemType.BossSigilFalseKnight, "FalseKnight", "空甲战令", "Hollow Armour Writ", 0, ItemType.IronOre, 12, "ZombieShield", ItemType.Gel, 6, "ZombieRunner", "遗忘十字路·假骑士周边"),
            S(ItemType.BossSigilGruzMother, "BigFly", "沉睡母巢", "Slumbering Brood Seal", 0, ItemType.Lens, 4, "Hatcher", ItemType.Daybloom, 4, "ZombieShield", "遗忘十字路·格鲁兹之母周边"),
            S(ItemType.BossSigilMawlek, "Mawlek", "躁动爪印", "Restless Claw Seal", 0, ItemType.IronOre, 10, "ZombieShield", ItemType.Deathweed, 4, "ZombieRunner", "遗忘十字路·毛里克周边"),
            S(ItemType.BossSigilHornet, "Hornet", "守护丝结", "Protector's Silken Knot", 1, ItemType.Vine, 6, "MossCharger", ItemType.Cobweb, 8, "MossmanRunner", "苍绿之径·大黄蜂初战周边"),
            S(ItemType.BossSigilMossCharger, "MegaMossCharger", "巨苔诱饵", "Great Moss Lure", 1, ItemType.JungleSpores, 6, "MossCharger", ItemType.Moonglow, 4, "MossmanRunner", "苍绿之径·巨型苔藓冲锋者周边"),
            S(ItemType.BossSigilGorb, "GhostAladar", "升华梦珠", "Ascending Dream Bead", 1, ItemType.Lens, 4, "Buzzer", ItemType.FlinxFur, 6, "Climber", "呼啸悬崖·戈布周边", true),
            S(ItemType.BossSigilNoEyes, "GhostNoEyes", "无目安魂铃", "Sightless Requiem Bell", 1, ItemType.Moonglow, 4, "PlantShooter", ItemType.JungleSpores, 6, "MossKnight", "苍绿之径·石之庇护所外", true),
            S(ItemType.BossSigilDungDefender, "DungDefender", "荣耀土球", "Glorious Earth Orb", 2, ItemType.Bone, 10, "Flukeman", ItemType.Obsidian, 6, "Inflater", "皇家水道·粪虫防御者周边"),
            S(ItemType.BossSigilTraitorLord, "TraitorLord", "叛离刃契", "Exile's Blade Pact", 5, ItemType.Stinger, 10, "HeavyMantis", ItemType.Vine, 8, "HeavyMantis", "王后花园·叛徒领主周边"),
            S(ItemType.BossSigilMantisLords, "MantisLord", "三刃挑战书", "Three-Blade Challenge", 2, ItemType.Stinger, 8, "Mantis", ItemType.JungleSpores, 6, "MantisFlyerChild", "真菌荒地·螳螂村"),
            S(ItemType.BossSigilSoulMaster, "MageLord", "聚魂法印", "Soul Convergence Seal", 2, ItemType.Bone, 10, "Mage", ItemType.Book, 2, "MageBlob", "泪水之城·灵魂圣所"),
            S(ItemType.BossSigilUumuu, "MegaJellyfish", "档案电核", "Archive Storm Core", 2, ItemType.Coral, 4, "Jellyfish", ItemType.Waterleaf, 4, "Jellyfish", "雾之峡谷·教师档案馆外"),
            S(ItemType.BossSigilFlukemarm, "FlukeMother", "吸虫母卵", "Fluke Brood Egg", 2, ItemType.Gel, 10, "Flukeman", ItemType.Bone, 8, "Flukeman", "皇家水道·吸虫之母周边"),
            S(ItemType.BossSigilCrystalGuardian, "MegaBeamMiner", "守卫晶镜", "Guardian's Crystal Lens", 2, ItemType.Diamond, 4, "BeamMiner", ItemType.Ruby, 4, "CrystalCrawler", "水晶山峰·水晶守卫周边"),
            S(ItemType.BossSigilElderHu, "GhostHu", "长老梦环", "Elder's Dream Ring", 2, ItemType.JungleSpores, 8, "MushroomRoller", ItemType.SturdyFossil, 6, "MushroomRoller", "真菌荒地·胡长老周边", true),
            S(ItemType.BossSigilMarmu, "GhostMarmu", "翠梦弹珠", "Verdant Dream Marble", 3, ItemType.Stinger, 6, "HeavyMantis", ItemType.Vine, 6, "HeavyMantis", "王后花园·马尔穆周边", true),
            S(ItemType.BossSigilWatcherKnights, "BlackKnight", "守望六印", "Six Watchers' Seal", 3, ItemType.DemoniteOre, 12, "RoyalDandy", ItemType.Bone, 10, "RoyalPlumper", "泪水之城·守望者尖塔周边"),
            S(ItemType.BossSigilHiveKnight, "HiveKnight", "蜂卫蜡章", "Hiveguard Wax Seal", 4, ItemType.BeeWax, 8, "BeeStinger", ItemType.Stinger, 6, "BeeStinger", "蜂巢·蜂巢骑士周边"),
            S(ItemType.BossSigilCollector, "JarCollector", "空罐收藏契", "Empty Jar Covenant", 3, ItemType.TissueSample, 6, "RoyalCoward", ItemType.Bone, 8, "RoyalDandy", "泪水之城·爱之塔周边"),
            S(ItemType.BossSigilNosk, "MimicSpider", "拟形蛛蜕", "Mimic's Spider Husk", 3, ItemType.Cobweb, 12, "MiniSpider", ItemType.SpiderFang, 6, "ShootSpider", "深巢·诺斯克周边"),
            S(ItemType.BossSigilGalien, "GhostGalien", "旋镰梦符", "Dream Scythe Charm", 3, ItemType.SpiderFang, 8, "SlashSpider", ItemType.Cobweb, 10, "MiniSpider", "深巢·加利安周边", true),
            S(ItemType.BossSigilXero, "GhostXero", "双钉残念", "Twin-Nail Reverie", 2, ItemType.Waterleaf, 4, "Mummy", ItemType.Fireblossom, 4, "Mummy", "安息之地·泽若与墓地周边", true),
            S(ItemType.BossSigilHornetSentinel, "Hornet@4", "哨兵棘丝", "Sentinel's Barbed Thread", 4, ItemType.Ichor, 8, "SuperSpitter", ItemType.PixieDust, 8, "Hopper", "王国边缘·大黄蜂哨兵周边"),
            S(ItemType.BossSigilMarkoth, "GhostMarkoth", "梦盾残片", "Dreamshield Fragment", 4, ItemType.PixieDust, 10, "BlowFly", ItemType.SoulofFlight, 6, "Hopper", "王国边缘·马科斯周边", true),
            S(ItemType.BossSigilBrokenVessel, "InfectedKnight", "破碎容器印", "Broken Vessel Seal", 3, ItemType.CursedFlame, 8, "LesserMawlek", ItemType.Obsidian, 8, "MawlekTurret", "远古盆地·破碎容器周边"),
            S(ItemType.BossSigilGrimm, "Grimm", "猩红剧票", "Scarlet Troupe Ticket", 5, ItemType.Lens, 6, "Buzzer", ItemType.Daybloom, 4, "Climber", "德特茅斯剧团帐篷外·呼啸悬崖入口"),
            S(ItemType.BossSigilHollowKnight, "HollowKnight", "黑卵封契", "Black Egg Covenant", 5, ItemType.IronOre, 16, "ZombieShield", ItemType.Deathweed, 6, "ZombieRunner", "黑卵神殿外·遗忘十字路"),
            S(ItemType.BossSigilFailedChampion, "FalseKnight@5", "失败者王徽", "Fallen Champion's Crest", 2, ItemType.IronOre, 16, "ZombieGuard", ItemType.Gel, 8, "ZombieRunner", "梦境本体·遗忘十字路假骑士周边", true),
            S(ItemType.BossSigilSoulTyrant, "MageLord@5", "暴君魂匣", "Tyrant's Soul Casket", 3, ItemType.ShadowScale, 8, "Mage", ItemType.Bone, 12, "MageBlob", "梦境本体·泪水之城灵魂圣所", true),
            S(ItemType.BossSigilLostKin, "InfectedKnight@5", "失亲追忆", "Lost Kin Memento", 4, ItemType.SoulofNight, 8, "LesserMawlek", ItemType.CursedFlame, 10, "LesserMawlek", "梦境本体·远古盆地破碎容器周边", true),
            S(ItemType.BossSigilWhiteDefender, "WhiteDefender", "白卫誓章", "White Defender's Oath", 5, ItemType.Obsidian, 10, "Inflater", ItemType.Bone, 12, "Flukeman", "梦境本体·皇家水道粪虫防御者周边", true),
            S(ItemType.BossSigilGreyPrince, "GreyPrince", "灰王妄想册", "Grey Prince's Fantasy", 5, ItemType.Lens, 6, "Hatcher", ItemType.Gel, 8, "ZombieRunner", "德特茅斯布蕾塔屋外·十字路井口", true),
            S(ItemType.BossSigilNightmareGrimm, "NightmareGrimm", "梦魇心烛", "Nightmare Heart Candle", 5, ItemType.FlinxFur, 8, "Climber", ItemType.Lens, 8, "Buzzer", "德特茅斯剧团帐篷外·呼啸悬崖入口", true),
            S(ItemType.BossSigilRadiance, "FinalBoss", "曙光梦盘", "Dawn Dream Disc", 5, ItemType.SilverOre, 18, "BurstingZombie", ItemType.Daybloom, 6, "SpittingZombie", "黑卵神殿外·感染十字路", true),
            S(ItemType.BossSigilPureVessel, "HollowKnightPrime", "无瑕容器印", "Pure Vessel Sigil", 5, ItemType.Bone, 14, "Flukeman", ItemType.Obsidian, 10, "Inflater", "神居入口·皇家水道垃圾坑", true),
            S(ItemType.BossSigilOroMato, "NailBros", "兄弟双钉契", "Brothers' Nail Pact", 5, ItemType.IronOre, 12, "Buzzer", ItemType.PixieDust, 8, "Hopper", "马托住所·呼啸悬崖／奥罗住所·王国边缘", true),
            S(ItemType.BossSigilSheo, "Paintmaster", "绘圣调色印", "Paintmaster's Palette Seal", 5, ItemType.Moonglow, 6, "MossCharger", ItemType.Vine, 8, "MossKnight", "席奥住所·苍绿之径", true),
            S(ItemType.BossSigilSly, "Nailsage", "贤者试剑令", "Nailsage's Trial Writ", 5, ItemType.IronOre, 14, "ZombieGuard", ItemType.Gel, 10, "ZombieRunner", "斯莱住所·德特茅斯十字路井口", true),
        };

        internal static Seal Find(ItemType type) { foreach (var s in All) if (s.Type == type) return s; return null; }
        static Seal Find(string key) { foreach (var s in All) if (s.Key == key) return s; return null; }
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> retryIcons = new Dictionary<string, float>();
        internal static int DevPortraitIcons, DevAccepted, DevConsumed, DevRefunded;
        internal static string DevLast = "";
        static bool pending;
        static int generation;
        static float nextNotice;
        /// <summary>The seal whose portal is to open on arrival in its world arena.</summary>
        static Seal arriving;
        internal static int DevArrivals;

        internal static void Reset() { generation++; pending = false; arriving = null; }

        internal static int DevFollowed;

        /// <summary>
        /// Another player of a LAN game used a seal: this player goes to its arena too, by the same portal, the story
        /// skipped (as for the one who used it), nothing spent. Once able to (out of a scene change, a talk, the bench);
        /// not out of a dream or Godhome, whose own ending decides where the player goes.
        /// </summary>
        internal static void Follow(string key, string who, int slot = -1)
        {
            var s = Find(key);
            if (s == null || !BossArena.Has(s.Key)) return;
            TerraMod.Instance?.StartCoroutine(FollowWhenAble(s, who, slot));
        }

        internal static int DevFollowWaits;

        static IEnumerator FollowWhenAble(Seal s, string who, int slot)
        {
            // The one who used it goes in first: their game runs the fight. The server makes the first game to have a
            // scene its owner, and a follower's could load the arena sooner: then the summoner came into a copy of a fight
            // already under way (its gates shut on the way in). Up to 15 s for the summoner's game to have it.
            string arena = BossArena.SceneOf(s.Key);
            float first = Time.realtimeSinceStartup + 15f;
            bool waited = false;
            while (slot >= 0 && arena != null && Net.NetSession.Active && Time.realtimeSinceStartup < first
                   && !(Net.NetSession.Owners.TryGetValue(arena, out int owner) && owner == slot))
            {
                waited = true;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (waited) DevFollowWaits++;
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                var gm = GameManager.UnsafeInstance;
                var hero = HeroController.SilentInstance;
                if (gm != null && hero != null && !gm.IsInSceneTransition && !hero.cState.dead && GameHooks.IsGameplay(gm) && !GameHooks.HeldByTheGame())
                {
                    if (gm.sceneName.StartsWith("GG_") || gm.sceneName.StartsWith("Dream_"))
                    {
                        Notice(Text($"{who} 召唤了首领，但梦境和神居里不能传送过去", $"{who} summoned a boss; you can't be taken there from a dream or Godhome"));
                        yield break;
                    }
                    // Already there (two seals at once): nothing to do.
                    if (BossArena.Arrived(s.Key) || pending) yield break;
                    if (BossArena.Visiting) BossArena.AbortEntry(s.Key);
                    if (!BossArena.Fight(s.Key, true)) yield break;
                    DevFollowed++;
                    DevLast = s.Key + " followed";
                    Plugin.Log.LogInfo($"[首领召唤物] 跟随 {who} 的召唤去 {s.Key}");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        /// <summary>
        /// The arena reached (HeroController.SendHeroInPosition): its portal opens once the game has brought the player
        /// in by the arena's door (Player.OpeningPortal.SummonAfterEntry). False when no seal is arriving here.
        /// </summary>

        internal static bool Arrive(HeroController hero)
        {
            var s = arriving;
            var gm = GameManager.UnsafeInstance;
            if (s == null || hero == null || gm == null) return false;
            if (gm.sceneName != BossArena.ArrivalScene(s.Key)) return false;
            arriving = null;
            DevArrivals++;
            Player.OpeningPortal.SummonAfterEntry(hero, Areas.AreaName(gm.sceneName));
            return true;
        }

        internal static void AddItems(List<ItemDef> list)
        {
            foreach (var s in All)
                list.Add(new ItemDef { Type = s.Type, Name = s.Name, Texture = IconPrefix + s.Key,
                    BossSummonKey = s.Key, Consumable = true, MaxStack = 9999, UseTime = 0.75f,
                    UseAnimation = 0.75f, AutoReuse = false, NoMelee = true, NoUseGraphic = true,
                    Rare = Mathf.Min(8, s.Stage + 1), Stage = s.Stage, Source = ItemSource.Craft,
                    // EnemyDrops initializes some loot values through ItemDb.Get. Do not read its
                    // roster while ItemDb itself is being constructed; Relocalize fills the text later.
                    Value = 0, Price = 0, Tooltip = "首领召唤物\n首次击败对应首领后使用" });
            // The colosseum's bosses are its trials': one summon to its door instead of their seals (ColosseumTicket).
            list.Add(ColosseumTicket.Def());
        }

        internal static IEnumerable<Recipe> Recipes()
        {
            foreach (var s in All)
                yield return new Recipe(s.Type, 1, "砧", s.Stage,
                    (s.Material, s.Amount), (s.SupportMaterial, s.SupportAmount));
            yield return ColosseumTicket.Recipe();
        }

        static string Tooltip(Seal s, bool zh)
        {
            string boss = zh ? EnemyDrops.ZhName(s.Key) : EnemyDrops.EnglishName(s.Key);
            // Where its arena is: a record fought twice (Hornet) has one scene in the book, its last.
            string area = Areas.AreaName(BossArena.WorldSceneOf(s.Key) ?? EnemyDrops.SceneOf(s.Key));
            return zh
                ? $"首领召唤物：{boss}（{area}）\n首次击败该首领后可在任意地点使用\n直接传送进它的竞技场，立刻开战，跳过剧情\n联机时所有玩家一起传送过去\n到达竞技场时消耗1个；条件不符不消耗\n梦境或神居战后返回出发处；现世战后留在竞技场"
                    + (s.Dream ? "\n需要梦之钉" : "")
                    + $"\n材料来源：{s.Locality}（{EnemyDrops.ZhName(s.MaterialSource)}、{EnemyDrops.ZhName(s.SupportSource)}）"
                : $"Boss summon: {boss} ({area})\nUsable anywhere once this boss is defeated\nTakes you straight into its arena, the fight on at once, the story skipped\nIn a LAN game every player goes too\nConsumes one on reaching the arena; invalid use costs nothing\nDream/Godhome fights return you; world fights end in the arena"
                    + (s.Dream ? "\nRequires the Dream Nail" : "")
                    + $"\nIngredients drop from {EnemyDrops.EnglishName(s.MaterialSource)} / {EnemyDrops.EnglishName(s.SupportSource)}";
        }

        internal static void Localize(ItemDef def)
        {
            var s = Find(def.Type);
            if (s == null) return;
            def.Name = Lang.IsSource ? s.Name : s.En;
            def.Tooltip = Tooltip(s, Lang.IsSource);
        }

        internal static string Refusal(Seal s)
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (s == null || !BossArena.Has(s.Key)) return Text("这个召唤物没有战斗入口", "No battle entrance for this seal");
            if (pending || BossArena.Visiting) return Text("已有首领挑战正在进行", "A boss challenge is already in progress");
            if (!GameHooks.PlayerHasControl() || gm == null || hero == null) return Text("现在无法召唤首领", "You cannot summon a boss now");
            if (Shop.UnlockedStage() < s.Stage) return Text("当前探索进度不足", "Explore further before using this seal");
            if (Bestiary.KillsOf(s.Key) <= 0) return Text("请先在旅途中击败该首领一次", "Defeat this boss in your journey first");
            if (s.Dream && (PlayerData.instance == null || !PlayerData.instance.hasDreamNail)) return Text("需要梦之钉", "Requires the Dream Nail");
            // Anywhere in the world; not from inside a dream or Godhome, whose own ending decides where the player goes.
            if (gm.sceneName.StartsWith("GG_") || gm.sceneName.StartsWith("Dream_")) return Text("梦境和神居里不能使用", "Cannot be used in a dream or Godhome");
            // Check the actual loaded world, including adjacent seamless regions, not just a cached book.
            foreach (var hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || EnemyStats.Defeated(hm)) continue;
                if (EnemyStats.Of(hm).Boss && Vector2.Distance(hm.transform.position, hero.transform.position) < 50f)
                    return Text("附近已有首领，不能重复召唤", "A boss is already nearby");
            }
            return null;
        }

        internal static bool TryUse(ItemDef def, Inventory inv)
        {
            var s = def == null ? null : Find(def.Type);
            string why = Refusal(s);
            if (inv == null || inv.Current.IsEmpty || inv.Current.Type != def?.Type)
                why = Text("请手持该召唤物", "Hold the summoning seal first");
            if (why != null) { Notice(why); return false; }
            pending = true;
            int slot = inv.Held;
            bool mouse = inv.UsingMouse;
            var hero = HeroController.SilentInstance;
            try
            {
                // Straight into the arena by its own way in; the fight begins there at once (BossSummonStart).
                if (!BossArena.Fight(s.Key, false)) { pending = false; Notice(Text("战斗入口暂不可用", "Battle entrance unavailable")); return false; }
                if (hero != null) PylonObject.TeleportEffect(hero);
            }
            catch (Exception e)
            {
                BossArena.AbortEntry(s.Key);
                pending = false;
                arriving = null;
                Plugin.Log.LogError("[首领召唤物] 入口失败 " + s.Key + ": " + e);
                Notice(Text("战斗入口失败，召唤物已保留", "Battle entrance failed; seal retained"));
                return false;
            }
            DevAccepted++;
            DevLast = s.Key + " requested";
            // A LAN game: everyone goes with the player.
            Net.NetSession.SendBossSummon(s.Key);
            // One spent once the arena is reached (the slot captured: no other stack charged); kept if it never is.
            TerraMod.Instance.StartCoroutine(Complete(s, inv, slot, mouse));
            return true;
        }

        static IEnumerator Complete(Seal s, Inventory inv, int slot, bool mouse)
        {
            int run = generation;
            try
            {
            // The scene change: up to thirty seconds to be in the arena.
            float until = Time.realtimeSinceStartup + 30f;
            bool found = false;
            while (Time.realtimeSinceStartup < until)
            {
                if (run != generation) yield break;
                if (BossArena.Arrived(s.Key)) { found = true; break; }
                if (!BossArena.Visiting) break;
                yield return new WaitForSecondsRealtime(0.1f);
            }
            if (found)
            {
                if (!inv.Infinite)
                {
                    if (mouse && inv.Mouse.Type == s.Type && inv.Mouse.Count > 0)
                    { inv.Mouse.Count--; if (inv.Mouse.Count == 0) inv.Mouse = default; DevConsumed++; }
                    else if (!mouse && slot >= 0 && slot < inv.Slots.Length && inv.Slots[slot].Type == s.Type && inv.Slots[slot].Count > 0)
                    { inv.ConsumeAt(slot); DevConsumed++; }
                    else Plugin.Log.LogWarning("[首领召唤物] 入场时原物品栏已变化，未扣除其他物品：" + s.Key);
                }
                DevLast = s.Key + " entered";
                Plugin.Log.LogInfo("[首领召唤物] 到达竞技场：" + s.Key + ", creative=" + inv.Infinite);
            }
            else
            {
                DevRefunded++;
                DevLast = s.Key + " failed; retained";
                Plugin.Log.LogWarning("[首领召唤物] 没有到达竞技场，物品保留：" + s.Key);
                Notice(Text("没能进入竞技场，召唤物已保留", "The arena wasn't reached; seal retained"));
            }
            }
            finally { if (run == generation) pending = false; }
        }

        static string Text(string zh, string en) => Lang.IsSource ? zh : en;
        static void Notice(string text)
        {
            DevLast = text;
            if (Time.unscaledTime < nextNotice) return;
            nextNotice = Time.unscaledTime + 1.5f;
            var hero = HeroController.SilentInstance;
            if (hero != null) CombatText.Show(hero.transform.position + Vector3.up, text, CombatText.Notice);
        }

        internal static bool Pending => pending;

        /// <summary>One cache for inventory, held item, world drop and Wiki export. No packaged HK art.</summary>
        internal static Texture2D Icon(string texture)
        {
            string key = texture.Substring(IconPrefix.Length);
            bool cached = icons.TryGetValue(key, out var previous);
            if (cached && (!retryIcons.TryGetValue(key, out float next) || Time.unscaledTime < next)) return previous;
            // The colosseum's summon has no seal of its own: its picture is drawn as one (ColosseumTicket.IconSeal).
            var s = Find(key) ?? (key == ColosseumTicket.IconSeal.Key ? ColosseumTicket.IconSeal : null);
            if (s == null) return Texture2D.whiteTexture;
            var portrait = EnemyHover.PortraitOf(key);
            if (portrait == null && cached) { retryIcons[key] = Time.unscaledTime + 3f; return previous; }
            Texture2D head = portrait != null ? DreamNailItem.Capture(portrait, 32, texture + "_portrait") : null;
            var pixels = new Color32[48 * 48];
            Color32 rim = s.Dream ? new Color32(173, 141, 238, 255) : new Color32(199, 162, 98, 255);
            // A dark inset with a distinct metallic rim; the source portrait is not recoloured.
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f));
                if (radius < 22f) pixels[y * 48 + x] = radius > 19.5f ? rim : new Color32(29, 27, 42, 255);
            }
            if (head != null)
            {
                var hp = head.GetPixels32();
                float scale = Mathf.Min(32f / head.width, 31f / head.height);
                int w = Mathf.Max(1, Mathf.RoundToInt(head.width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(head.height * scale));
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    var p = hp[Mathf.Min(head.height - 1, (int)(y / scale)) * head.width + Mathf.Min(head.width - 1, (int)(x / scale))];
                    int at = (y + 9) * 48 + x + (48 - w) / 2;
                    float a = p.a / 255f;
                    pixels[at] = Color.Lerp(pixels[at], p, a);
                }
                UnityEngine.Object.Destroy(head);
                retryIcons.Remove(key);
                DevPortraitIcons++;
            }
            else retryIcons[key] = Time.unscaledTime + 3f;
            // Five progression runes and a pair for rematch variants, visible even at inventory scale.
            for (int i = 0; i <= s.Stage; i++)
                for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++) pixels[(5 + dy) * 48 + 12 + i * 4 + dx] = rim;
            if (key.Contains("@")) for (int y = 19; y <= 27; y += 6) for (int x = 42; x <= 44; x++) pixels[y * 48 + x] = rim;
            var result = TerrariaAssets.Make(texture, pixels, 48, 48);
            icons[key] = result;
            // Keep a previously displayed fallback alive: a dropped-item sprite may still reference it.
            return result;
        }
    }
}
