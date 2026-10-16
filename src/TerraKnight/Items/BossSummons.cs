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
            public bool Dream;
        }

        static Seal S(ItemType t, string key, string zh, string en, int stage, ItemType mat, int amount, bool dream = false) =>
            new Seal { Type = t, Key = key, Name = zh, En = en, Stage = stage, Material = mat, Amount = amount, Dream = dream };

        // Each entry has a supported full arena in BossArena; the enum IDs are append-only.
        internal static readonly Seal[] All =
        {
            S(ItemType.BossSigilFalseKnight, "FalseKnight", "空甲战令", "Hollow Armour Writ", 0, ItemType.IronBar, 4),
            S(ItemType.BossSigilGruzMother, "BigFly", "沉睡母巢", "Slumbering Brood Seal", 0, ItemType.Lens, 6),
            S(ItemType.BossSigilMawlek, "Mawlek", "躁动爪印", "Restless Claw Seal", 0, ItemType.RottenChunk, 6),
            S(ItemType.BossSigilHornet, "Hornet", "守护丝结", "Protector's Silken Knot", 1, ItemType.Cobweb, 15),
            S(ItemType.BossSigilMossCharger, "MegaMossCharger", "巨苔诱饵", "Great Moss Lure", 1, ItemType.Moonglow, 5),
            S(ItemType.BossSigilGorb, "GhostAladar", "升华梦珠", "Ascending Dream Bead", 1, ItemType.FallenStar, 3, true),
            S(ItemType.BossSigilNoEyes, "GhostNoEyes", "无目安魂铃", "Sightless Requiem Bell", 1, ItemType.Deathweed, 5, true),
            S(ItemType.BossSigilDungDefender, "DungDefender", "荣耀土球", "Glorious Earth Orb", 2, ItemType.ClayBlock, 20),
            S(ItemType.BossSigilTraitorLord, "TraitorLord", "叛离刃契", "Exile's Blade Pact", 5, ItemType.Stinger, 8),
            S(ItemType.BossSigilMantisLords, "MantisLord", "三刃挑战书", "Three-Blade Challenge", 2, ItemType.Stinger, 6),
            S(ItemType.BossSigilSoulMaster, "MageLord", "聚魂法印", "Soul Convergence Seal", 2, ItemType.Amethyst, 5),
            S(ItemType.BossSigilUumuu, "MegaJellyfish", "档案电核", "Archive Storm Core", 2, ItemType.Coral, 8),
            S(ItemType.BossSigilFlukemarm, "FlukeMother", "吸虫母卵", "Fluke Brood Egg", 2, ItemType.RottenChunk, 10),
            S(ItemType.BossSigilCrystalGuardian, "MegaBeamMiner", "守卫晶镜", "Guardian's Crystal Lens", 2, ItemType.Diamond, 3),
            S(ItemType.BossSigilElderHu, "GhostHu", "长老梦环", "Elder's Dream Ring", 2, ItemType.FallenStar, 4, true),
            S(ItemType.BossSigilMarmu, "GhostMarmu", "翠梦弹珠", "Verdant Dream Marble", 2, ItemType.Moonglow, 6, true),
            S(ItemType.BossSigilWatcherKnights, "BlackKnight", "守望六印", "Six Watchers' Seal", 3, ItemType.IronBar, 12),
            S(ItemType.BossSigilHiveKnight, "HiveKnight", "蜂卫蜡章", "Hiveguard Wax Seal", 3, ItemType.BeeWax, 8),
            S(ItemType.BossSigilCollector, "JarCollector", "空罐收藏契", "Empty Jar Covenant", 3, ItemType.Bottle, 5),
            S(ItemType.BossSigilNosk, "MimicSpider", "拟形蛛蜕", "Mimic's Spider Husk", 3, ItemType.Cobweb, 30),
            S(ItemType.BossSigilGalien, "GhostGalien", "旋镰梦符", "Dream Scythe Charm", 3, ItemType.Bone, 12, true),
            S(ItemType.BossSigilXero, "GhostXero", "双钉残念", "Twin-Nail Reverie", 3, ItemType.IronBar, 8, true),
            S(ItemType.BossSigilHornetSentinel, "Hornet@4", "哨兵棘丝", "Sentinel's Barbed Thread", 4, ItemType.Silk, 6),
            S(ItemType.BossSigilMarkoth, "GhostMarkoth", "梦盾残片", "Dreamshield Fragment", 4, ItemType.FallenStar, 6, true),
            S(ItemType.BossSigilBrokenVessel, "InfectedKnight", "破碎容器印", "Broken Vessel Seal", 4, ItemType.RottenChunk, 15),
            S(ItemType.BossSigilGrimm, "Grimm", "猩红剧票", "Scarlet Troupe Ticket", 5, ItemType.Fireblossom, 8),
            S(ItemType.BossSigilHollowKnight, "HollowKnight", "黑卵封契", "Black Egg Covenant", 5, ItemType.Obsidian, 20),
            S(ItemType.BossSigilFailedChampion, "FalseKnight@5", "失败者王徽", "Fallen Champion's Crest", 2, ItemType.IronBar, 10, true),
            S(ItemType.BossSigilSoulTyrant, "MageLord@5", "暴君魂匣", "Tyrant's Soul Casket", 3, ItemType.Amethyst, 8, true),
            S(ItemType.BossSigilLostKin, "InfectedKnight@5", "失亲追忆", "Lost Kin Memento", 4, ItemType.Bone, 15, true),
            S(ItemType.BossSigilWhiteDefender, "WhiteDefender", "白卫誓章", "White Defender's Oath", 5, ItemType.Pearlwood, 20, true),
            S(ItemType.BossSigilGreyPrince, "GreyPrince", "灰王妄想册", "Grey Prince's Fantasy", 5, ItemType.Silk, 8, true),
            S(ItemType.BossSigilNightmareGrimm, "NightmareGrimm", "梦魇心烛", "Nightmare Heart Candle", 5, ItemType.Fireblossom, 12, true),
            S(ItemType.BossSigilRadiance, "FinalBoss", "曙光梦盘", "Dawn Dream Disc", 5, ItemType.FallenStar, 12, true),
            S(ItemType.BossSigilPureVessel, "HollowKnightPrime", "无瑕容器印", "Pure Vessel Sigil", 5, ItemType.HallowedBar, 8, true),
            S(ItemType.BossSigilOroMato, "NailBros", "兄弟双钉契", "Brothers' Nail Pact", 5, ItemType.HallowedBar, 6, true),
            S(ItemType.BossSigilSheo, "Paintmaster", "绘圣调色印", "Paintmaster's Palette Seal", 5, ItemType.Ruby, 8, true),
            S(ItemType.BossSigilSly, "Nailsage", "贤者试剑令", "Nailsage's Trial Writ", 5, ItemType.HallowedBar, 8, true),
            S(ItemType.BossSigilOblobbles, "Oblobble", "双生酸囊", "Twin Acid Sacs", 4, ItemType.Lens, 15, true),
            S(ItemType.BossSigilGodTamer, "LobsterLancer", "驯神兽笛", "God Tamer's Beast Whistle", 4, ItemType.Bone, 15, true),
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
        internal static void Follow(string key, string who)
        {
            var s = Find(key);
            if (s == null || !BossArena.Has(s.Key)) return;
            TerraMod.Instance?.StartCoroutine(FollowWhenAble(s, who));
        }

        static IEnumerator FollowWhenAble(Seal s, string who)
        {
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                var gm = GameManager.UnsafeInstance;
                var hero = HeroController.SilentInstance;
                if (gm != null && hero != null && !gm.IsInSceneTransition && !hero.cState.dead && GameHooks.IsGameplay(gm))
                {
                    if (gm.sceneName.StartsWith("GG_") || gm.sceneName.StartsWith("Dream_"))
                    {
                        Notice(Text($"{who} 召唤了首领，但梦境和神居里不能传送过去", $"{who} summoned a boss; you can't be taken there from a dream or Godhome"));
                        yield break;
                    }
                    // Already there (two seals at once): nothing to do.
                    if (BossArena.Arrived(s.Key) || pending) yield break;
                    if (BossArena.Visiting) BossArena.AbortEntry(s.Key);
                    arriving = BossArena.DevWorldArena(s.Key, out _, out _) ? s : null;
                    if (!BossArena.Fight(s.Key, true, atDoor: true)) { arriving = null; yield break; }
                    PylonObject.TeleportEffect(hero);
                    TerraMod.Instance?.PlaySound("Custom/dd2_etherian_portal_open", 0.8f);
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
        }

        internal static IEnumerable<Recipe> Recipes()
        {
            foreach (var s in All)
                yield return new Recipe(s.Type, 1, "砧", s.Stage,
                    (ItemType.Glass, 2), (ItemType.StoneBlock, 5), (s.Material, s.Amount));
        }

        static string Tooltip(Seal s, bool zh)
        {
            string boss = zh ? EnemyDrops.ZhName(s.Key) : EnemyDrops.EnglishName(s.Key);
            string area = Areas.AreaName(EnemyDrops.SceneOf(s.Key));
            return zh
                ? $"首领召唤物：{boss}（{area}）\n首次击败该首领后可在任意地点使用\n打开传送门，把你送到它竞技场的正门外，走进去即开战，跳过剧情\n联机时所有玩家一起传送过去\n成功进入战斗消耗1个；条件不符不消耗\n梦境或神居战后返回出发处；现世战后留在竞技场；走开则召唤物保留"
                    + (s.Dream ? "\n需要梦之钉" : "")
                : $"Boss summon: {boss} ({area})\nUsable anywhere once this boss is defeated\nA portal takes you to its arena's front door; walk in to fight, the story skipped\nIn a LAN game every player goes too\nConsumes one on battle entry; invalid use costs nothing\nDream/Godhome fights return you; world fights end in the arena; walk away and the seal is kept"
                    + (s.Dream ? "\nRequires the Dream Nail" : "");
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
                // The portal: the teleport's light and the portal's sound where the player stands; out of it in the arena.
                arriving = BossArena.DevWorldArena(s.Key, out _, out _) ? s : null;
                if (!BossArena.Fight(s.Key, false, atDoor: true)) { pending = false; arriving = null; Notice(Text("战斗入口暂不可用", "Battle entrance unavailable")); return false; }
                if (hero != null) PylonObject.TeleportEffect(hero);
                TerraMod.Instance?.PlaySound("Custom/dd2_etherian_portal_open", 0.8f);
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
            // The stack is not spent until an actual living boss is present. No inventory/UI mutation
            // is possible during transition; checking the captured slot also prevents charging another item.
            TerraMod.Instance.StartCoroutine(Complete(s, inv, slot, mouse));
            return true;
        }

        static IEnumerator Complete(Seal s, Inventory inv, int slot, bool mouse)
        {
            int run = generation;
            try
            {
            // From the front door the player walks in when ready: up to five minutes.
            float until = Time.realtimeSinceStartup + 300f;
            bool found = false;
            string scene = BossArena.SceneOf(s.Key), record = s.Key.Split('@')[0];
            while (Time.realtimeSinceStartup < until)
            {
                if (run != generation) yield break;
                if (BossArena.Arrived(s.Key))
                {
                    foreach (var hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        string room = hm.gameObject.scene.name;
                        if (hm.isDead || hm.hp <= 0 || (room != scene && !room.StartsWith(scene + "_boss"))) continue;
                        if (EnemyHover.RecordOf(hm) == record) { found = true; break; }
                    }
                }
                if (found) break;
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
                Plugin.Log.LogInfo("[首领召唤物] 已进入完整战斗：" + s.Key + ", creative=" + inv.Infinite);
            }
            else
            {
                DevRefunded++;
                DevLast = s.Key + " failed; retained";
                Plugin.Log.LogWarning("[首领召唤物] 未确认活体首领，物品保留：" + s.Key);
                Notice(Text("未能确认首领入场，召唤物已保留", "Boss entry unconfirmed; seal retained"));
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
            var s = Find(key);
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
