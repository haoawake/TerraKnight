using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The areas' chests, as Terraria's world gen leaves chests about its caves: a save begun since there were any gets them
    /// by the areas' size (the open cells of their scenes on the world atlas): ten in the largest, the rest in proportion,
    /// two at the least, none in Godhome (a save planned with fewer gets the rest of its areas' on loading); each in its
    /// area's own chest (the crate pack's pictures, World.AreaBlocks.All's order), spread over the area's scenes in the
    /// save's own order. Each is set down the first time its scene comes in, on two cells of even ground with three free over
    /// them, reachable from the scene's gates, four cells clear of spikes, acid, lava and water, away from other chests,
    /// benches, gates and a boss's fighting ground; a scene with no such spot hands its chest to another of the area. Then it
    /// is an ordinary chest (opened, emptied, mined for its item, on the map once seen). Each holds one piece, of the stage of
    /// the area or the stage reached, the lower: in turn a weapon its area's enemies drop, an accessory of theirs, and one of
    /// Terraria's chests' own pieces for the area (its Wooden, Gold, Ivy, Ice, Water, Sky, Shadow, Sandstone or Dungeon chests',
    /// sold in town or not; a boss's own stay its own); then, as WorldGen's chests, the area's materials, bars, ammunition,
    /// throwing weapons or explosives, Terraria's chest potions, healing and mana potions, a Recall Potion now and then, the
    /// area's torches and geo; now and then (1 in 30) a pet. A save from before has none.
    /// </summary>
    internal static class RegionalChests
    {
        /// <summary>The largest area's chests; the others' in proportion to their size, <see cref="Fewest"/> at the least.</summary>
        public const int PerArea = 10, Fewest = 2;
        /// <summary>The areas with none: Godhome (its halls are a boss rush's).</summary>
        static readonly HashSet<string> without = new HashSet<string> { "神居" };
        const float Cell = CellGrid.CellSize;
        const float Px = Effects.TerraParticles.Px;

        /// <summary>
        /// A chest's part: what its piece is (by the part's place in threes: 0 a weapon of the area's enemies, 1 an accessory of
        /// theirs, 2 one of Terraria's chests' pieces for the area).
        /// </summary>
        public const int WeaponPart = 0, AccessoryPart = 1, ChestPiecePart = 2;
        static int KindOf(int part) => part % 3;

        sealed class Plan
        {
            public string Area, Scene;
            public int Part;
            public bool Placed;
            public readonly HashSet<string> Tried = new HashSet<string>();
        }

        /// <summary>What the save keeps of a chest still to come (or come).</summary>
        public struct Saved
        {
            public string Area, Scene;
            public int Part;
            public bool Placed;
        }

        static readonly List<Plan> plans = new List<Plan>();
        public static int Seed { get; private set; }
        /// <summary>A save of the areas' chests (a new one since): its plan is kept.</summary>
        public static bool Planned => Seed != 0;

        public static int DevPlaced;

        // ------------------------------------------------------------------ the chests' items

        /// <summary>Each area's chest's stem name (the crate pack's), by World.AreaBlocks key.</summary>
        static readonly Dictionary<string, string> stems = new Dictionary<string, string>
        {
            ["KingsPass"] = "山道", ["Dirtmouth"] = "镇灯", ["Crossroads"] = "旧巢", ["Greenpath"] = "苍绿", ["FungalWastes"] = "菌孢", ["FogCanyon"] = "雾泡",
            ["HowlingCliffs"] = "啸风", ["CityOfTears"] = "泪城", ["RoyalWaterways"] = "锈渠", ["CrystalPeak"] = "晶簇", ["RestingGrounds"] = "安息", ["Deepnest"] = "蛛丝",
            ["BeastsDen"] = "兽巢", ["AncientBasin"] = "沉岩", ["QueensGardens"] = "荆花", ["KingdomsEdge"] = "白灰", ["Colosseum"] = "斗兽", ["Hive"] = "蜂蜡",
            ["Abyss"] = "虚空", ["WhitePalace"] = "白宫", ["Godhome"] = "神辉", ["BlackEggTemple"] = "黑卵", ["Dream"] = "梦光", ["GrimmTroupe"] = "梦魇",
        };

        /// <summary>The chest item of a look (0, or none known: Terraria's chest).</summary>
        public static ItemType ItemOf(int style) => RegionalFurniture.IsDresser(style) ? RegionalFurniture.Item(style-1001,RegionalFurniture.DresserIndex) : style >= 1 && style <= AreaBlocks.All.Length ? ItemType.ChestKingsPass + (style - 1) : ItemType.Chest;

        /// <summary>An area's chest look (1..24), 0 for a place not one of the areas.</summary>
        public static int StyleOf(string area)
        {
            for (int i = 0; i < AreaBlocks.All.Length; i++) if (AreaBlocks.All[i].Name == area) return i + 1;
            return 0;
        }

        /// <summary>The 24 chest items: placed as their area's chest, as Terraria's own chest is (World.Chests).</summary>
        internal static void AddItems(List<ItemDef> list)
        {
            for (int i = 0; i < AreaBlocks.All.Length; i++)
            {
                var a = AreaBlocks.All[i];
                list.Add(new ItemDef
                {
                    Type = ItemType.ChestKingsPass + i, Name = (stems.TryGetValue(a.Key, out var stem) ? stem : a.Name) + "储物箱", Texture = "HkChestItem_" + a.Key,
                    PlacesChest = true, ChestStyle = i + 1, Consumable = true, MaxStack = 9999, UseAnimation = 15f / 60f, UseTime = 10f / 60f, Value = 500, Rare = 1,
                    // Made of 30 of its area's blocks only (Recipes), never one of the basics a bench or Dirtmouth sells.
                    Source = ItemSource.Craft, Price = 5,
                    UseSound = null, Tooltip = "放在地面上，可存放 40 格物品\n右键打开；空箱子可用镐挖回",
                });
            }
        }

        /// <summary>Each area's chest made of its own block: 30 of them at a Work Bench.</summary>
        public static IEnumerable<Recipe> Recipes()
        {
            for (int i = 0; i < AreaBlocks.All.Length; i++)
                yield return new Recipe(ItemType.ChestKingsPass + i, 1, "工作台", 0, (AreaBlocks.BlockItem(i), 30));
        }

        static readonly Sprite[,] frames = new Sprite[32, 3];

        /// <summary>An area's chest's lid frame (0 shut, 1 half, 2 open): Resources/hk_HkChest_&lt;key&gt;_&lt;f&gt;.png, 32×40 px standing on its bottom.</summary>
        public static Sprite FrameSprite(int style, int f)
        {
            if (RegionalFurniture.IsDresser(style)) return RegionalFurniture.Sprite(RegionalFurniture.DresserKind(style));
            if (style < 1 || style > AreaBlocks.All.Length || f < 0 || f > 2) return null;
            var s = frames[style, f];
            if (s != null) return s;
            var tex = TerrariaAssets.Texture("HkChest_" + AreaBlocks.All[style - 1].Key + "_" + f);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            frames[style, f] = s;
            return s;
        }

        // ------------------------------------------------------------------ the save's plan

        /// <summary>
        /// An area's scenes a chest may stand in: not a boss's room, a fight's or Godhome's; houses and shops (Room_) only
        /// for an area of fewer than three other scenes (Dirtmouth's houses, the temple's).
        /// </summary>
        static List<string> Candidates(string area)
        {
            var open = new List<string>();
            var rooms = new List<string>();
            if (without.Contains(area)) return open;
            foreach (var s in UI.WorldMap.DevSceneNames())
            {
                if (Areas.AreaName(s) != area || BossRooms.IsRoom(s) || s.StartsWith("GG_")) continue;
                if (s.StartsWith("Room_")) rooms.Add(s);
                else open.Add(s);
            }
            if (open.Count < 3) open.AddRange(rooms);
            open.Sort(System.StringComparer.Ordinal);
            return open;
        }

        /// <summary>A new game: each area's chests, over its scenes in the save's own order, their parts shuffled.</summary>
        public static void NewWorld(int seed = 0)
        {
            Clear();
            Seed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            toppedUp = true;
            var rng = new System.Random(Seed);
            int none = 0;
            foreach (var a in AreaBlocks.All)
            {
                var scenes = Candidates(a.Name);
                if (scenes.Count == 0) { none++; continue; }
                Shuffle(scenes, rng);
                // A weapon's, an accessory's and a chest piece's in turn (one of the first two for an area of one); shuffled.
                int count = CountOf(a.Name);
                var parts = new List<int>();
                if (count == 1) parts.Add(rng.Next(2));
                else for (int k = 0; k < count; k++) parts.Add(k);
                Shuffle(parts, rng);
                for (int k = 0; k < count; k++) plans.Add(new Plan { Area = a.Name, Scene = scenes[k % scenes.Count], Part = parts[k] });
            }
            Plugin.Log.LogInfo($"[地区储物箱] 这个存档的 {plans.Count} 个箱子分在 {AreaBlocks.All.Length - none} 个地区");
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Whether a loaded save's plan has had its areas' chests made up to <see cref="CountOf"/> (TopUp).</summary>
        static bool toppedUp;

        /// <summary>
        /// A save planned when the areas had fewer chests: each area's made up to its count now, the new ones' parts going
        /// on from the area's last, over its scenes in an order of the save's own (the host's game, once a load).
        /// </summary>
        static void TopUp()
        {
            int added = 0;
            foreach (var a in AreaBlocks.All)
            {
                int have = 0;
                foreach (var p in plans) if (p.Area == a.Name) have++;
                int want = CountOf(a.Name);
                if (have >= want) continue;
                var scenes = Candidates(a.Name);
                if (scenes.Count == 0) continue;
                Shuffle(scenes, new System.Random(Seed ^ LifeCrystals.Hash(a.Name)));
                for (int k = have; k < want; k++, added++) plans.Add(new Plan { Area = a.Name, Scene = scenes[k % scenes.Count], Part = k });
            }
            if (added > 0) Plugin.Log.LogInfo($"[地区储物箱] 这个存档的箱子补到 {plans.Count} 个（新加 {added} 个，各在它们的场景第一次进来时放下）");
        }

        public static void Clear()
        {
            plans.Clear();
            Seed = 0;
            toppedUp = false;
        }

        static Dictionary<string, int> sizes;

        /// <summary>
        /// The areas' size: the open cells of their scenes on the world atlas (UI.WorldMap.OpenCells); without the atlas,
        /// how many scenes a chest may stand in.
        /// </summary>
        static Dictionary<string, int> Sizes()
        {
            if (sizes != null) return sizes;
            var map = new Dictionary<string, int>();
            foreach (var s in UI.WorldMap.DevSceneNames())
            {
                string a = Areas.AreaName(s);
                map.TryGetValue(a, out int n);
                map[a] = n + UI.WorldMap.OpenCells(s);
            }
            bool atlas = false;
            foreach (var v in map.Values) if (v > 0) { atlas = true; break; }
            if (!atlas)
                foreach (var a in AreaBlocks.All) map[a.Name] = Candidates(a.Name).Count;
            return sizes = map;
        }

        /// <summary>An area's chests: ten for the largest, the rest in proportion (rounded, two at the least), none for Godhome.</summary>
        public static int CountOf(string area)
        {
            if (without.Contains(area)) return 0;
            var all = Sizes();
            int largest = 0;
            foreach (var a in AreaBlocks.All)
                if (!without.Contains(a.Name) && all.TryGetValue(a.Name, out int v) && v > largest) largest = v;
            if (largest <= 0 || !all.TryGetValue(area, out int size)) return Fewest;
            return Mathf.Clamp(Mathf.RoundToInt(PerArea * (float)size / largest), Fewest, PerArea);
        }

        /// <summary>Dev: an area's size as counted.</summary>
        public static int DevSize(string area) => Sizes().TryGetValue(area, out int v) ? v : 0;

        public static List<Saved> Save()
        {
            var list = new List<Saved>(plans.Count);
            foreach (var p in plans) list.Add(new Saved { Area = p.Area, Scene = p.Scene, Part = p.Part, Placed = p.Placed });
            return list;
        }

        /// <summary>A save's plan; a save with none (from before the chests) gets no chests.</summary>
        public static void Load(int seed, IEnumerable<Saved> saved)
        {
            Clear();
            if (seed == 0 || saved == null) return;
            Seed = seed;
            foreach (var s in saved) plans.Add(new Plan { Area = s.Area, Scene = s.Scene, Part = s.Part, Placed = s.Placed });
        }

        // ------------------------------------------------------------------ setting them down

        /// <summary>Each frame: one chest still to come whose scene is in, set down (or handed on); the host's game only.</summary>
        public static void Tick()
        {
            if (!Planned || !Net.WorldSync.CanInteract || Net.WorldSync.Guest) return;
            if (!toppedUp)
            {
                toppedUp = true;
                TopUp();
            }
            foreach (var p in plans)
            {
                if (p.Placed) continue;
                var region = TerrainSystem.Find(p.Scene);
                if (region == null || region.Root == null || !region.Root.gameObject.scene.isLoaded) continue;
                Place(p, region);
                return;
            }
        }

        static void Place(Plan p, TerrainRegion region)
        {
            if (FindSpot(region, plans.IndexOf(p), out var local))
            {
                int stage = Mathf.Min(Areas.LevelOf(p.Scene), Shop.UnlockedStage());
                var items = Contents(p.Area, p.Part, stage);
                Chests.AddWorld(p.Scene, local, StyleOf(p.Area), items);
                p.Placed = true;
                DevPlaced++;
                UI.WorldMap.Refresh(p.Scene);
                Plugin.Log.LogInfo($"[地区储物箱] {p.Scene}（{p.Area}）放下一个{(KindOf(p.Part) == WeaponPart ? "武器" : KindOf(p.Part) == AccessoryPart ? "饰品" : "宝箱货")}箱（阶段 {stage}）@{local:F1}：{Describe(items)}");
                return;
            }
            // No room: another scene of the area, one not tried.
            p.Tried.Add(p.Scene);
            foreach (var s in Candidates(p.Area))
            {
                if (p.Tried.Contains(s)) continue;
                Plugin.Log.LogInfo($"[地区储物箱] {p.Scene} 里没有合适的地方，改放到 {s}");
                p.Scene = s;
                return;
            }
            Plugin.Log.LogWarning($"[地区储物箱] {p.Area} 找不到放箱子的地方，这个箱子不放了");
            p.Placed = true;
        }

        /// <summary>
        /// Where in a loaded scene: one of the ground spots (LifeCrystals.GroundSpots: three cells over, four clear of
        /// hazards), six units from another chest, three from a bench, a gate and a Life Crystal, out of a boss's fight;
        /// the same for the same save and chest.
        /// </summary>
        static bool FindSpot(TerrainRegion region, int index, out Vector2 local)
        {
            local = default;
            var spots = LifeCrystals.GroundSpots(region, 3, 4);
            if (spots.Count == 0) return false;
            var scene = region.Root.gameObject.scene;
            var away = new List<Vector2>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(false))
                    if (f.FsmName == "Bench Control") away.Add(f.transform.position);
                foreach (var g in root.GetComponentsInChildren<TransitionPoint>(true)) away.Add(g.transform.position);
            }
            foreach (var (s, at) in LifeCrystals.Standing()) if (s == region.SceneName) away.Add((Vector2)region.Root.position + at);
            var ok = new List<Vector2>();
            foreach (var w in spots)
            {
                var lw = w - (Vector2)region.Root.position;
                if (Chests.AnyNear(region.SceneName, lw, 6f)) continue;
                if (BossRooms.Locked(w + new Vector2(0f, 0.1f))) continue;
                bool near = false;
                foreach (var a in away) if (Vector2.Distance(a, w) < 3f) { near = true; break; }
                if (!near) ok.Add(w);
            }
            if (ok.Count == 0) return false;
            var rng = new System.Random(Seed ^ LifeCrystals.Hash(region.SceneName) ^ (index * 7919));
            local = ok[rng.Next(ok.Count)] - (Vector2)region.Root.position;
            return true;
        }

        // ------------------------------------------------------------------ what one holds

        // Terraria's chests' own pieces (WorldGen.AddBuriedChest's first roll and the Dungeon's), by the chest; those that
        // are a boss's own here (BossDropPlan.BossOnly) never come out of one.
        static readonly ItemType[] wooden =
        {
            ItemType.Spear, ItemType.Blowpipe, ItemType.WoodenBoomerang, ItemType.Aglet, ItemType.ClimbingClaws, ItemType.Umbrella, ItemType.WandofSparking,
            ItemType.Radar, ItemType.FinchStaff, ItemType.PortableStool,
        };
        static readonly ItemType[] gold =
        {
            ItemType.BandofRegeneration, ItemType.CloudinaBottle, ItemType.HermesBoots, ItemType.ShoeSpikes, ItemType.FlareGun, ItemType.EnchantedBoomerang,
            ItemType.Mace, ItemType.LavaCharm, ItemType.WaterWalkingBoots, ItemType.FlurryBoots,
        };
        static readonly ItemType[] ivy = { ItemType.FeralClaws, ItemType.AnkletoftheWind, ItemType.StaffofRegrowth, ItemType.Boomstick, ItemType.FlowerBoots };
        static readonly ItemType[] ice = { ItemType.IceBoomerang, ItemType.IceBlade, ItemType.IceSkates, ItemType.SnowballCannon, ItemType.BlizzardinaBottle, ItemType.FlurryBoots, ItemType.IceMirror };
        static readonly ItemType[] water = { ItemType.BreathingReed, ItemType.Flipper, ItemType.Trident, ItemType.FloatingTube, ItemType.WaterWalkingBoots };
        static readonly ItemType[] skyware = { ItemType.ShinyRedBalloon, ItemType.Starfury, ItemType.LuckyHorseshoe, ItemType.CelestialMagnet };
        static readonly ItemType[] shadow = { ItemType.DarkLance, ItemType.Flamelash, ItemType.FlowerofFire, ItemType.Sunfury, ItemType.HellwingBow, ItemType.TreasureMagnet };
        static readonly ItemType[] sandstone = { ItemType.SandstorminaBottle, ItemType.FlyingCarpet, ItemType.AncientChisel, ItemType.SandBoots, ItemType.ThunderSpear, ItemType.ThunderStaff };
        static readonly ItemType[] dungeon = { ItemType.Muramasa, ItemType.CobaltShield, ItemType.AquaScepter, ItemType.BlueMoon, ItemType.Handgun, ItemType.MagicMissile, ItemType.Valor };

        /// <summary>
        /// Each area's Terraria chests, as its fishing crate stands for a biome's (FishingAreaCrates); the Dungeon's six
        /// biome chests' pieces in the three last areas.
        /// </summary>
        static readonly Dictionary<string, ItemType[][]> chestPieces = new Dictionary<string, ItemType[][]>
        {
            ["国王山道"] = new[] { wooden }, ["德特茅斯"] = new[] { wooden }, ["遗忘十字路"] = new[] { wooden, gold }, ["苍绿之径"] = new[] { ivy },
            ["真菌荒地"] = new[] { gold }, ["雾之峡谷"] = new[] { water }, ["呼啸悬崖"] = new[] { skyware, wooden }, ["泪水之城"] = new[] { dungeon, water },
            ["皇家水道"] = new[] { water }, ["水晶山峰"] = new[] { gold }, ["安息之地"] = new[] { ice }, ["深巢"] = new[] { gold },
            ["野兽巢穴"] = new[] { gold }, ["远古盆地"] = new[] { shadow, gold }, ["王后花园"] = new[] { ivy }, ["王国边缘"] = new[] { sandstone },
            ["愚人斗兽场"] = new[] { sandstone }, ["蜂巢"] = new[] { ivy }, ["深渊"] = new[] { shadow }, ["黑卵神殿"] = new[] { gold },
            ["白色宫殿"] = new[] { skyware, new[] { ItemType.RainbowGun, ItemType.StaffoftheFrostHydra } },
            ["梦境"] = new[] { skyware, new[] { ItemType.PiranhaGun, ItemType.DesertTigerStaff } },
            ["格林剧团"] = new[] { shadow, new[] { ItemType.ScourgeoftheCorruptor, ItemType.VampireKnives } },
        };

        /// <summary>WorldGen's chest potions (the cave chests' second roll).</summary>
        static readonly ItemType[] chestPotions =
        {
            ItemType.IronskinPotion, ItemType.SwiftnessPotion, ItemType.RegenerationPotion, ItemType.ShinePotion, ItemType.NightOwlPotion, ItemType.SpelunkerPotion,
            ItemType.MiningPotion, ItemType.HunterPotion, ItemType.ArcheryPotion, ItemType.GravitationPotion, ItemType.ThornsPotion, ItemType.WaterWalkingPotion,
            ItemType.InvisibilityPotion, ItemType.FeatherfallPotion, ItemType.BattlePotion, ItemType.BuilderPotion, ItemType.ObsidianSkinPotion,
            ItemType.HeartreachPotion, ItemType.TrapsightPotion,
        };

        /// <summary>
        /// WorldGen's chests' bars by depth (the surface's copper to lead, the underground's iron to tungsten, the caverns'
        /// silver to platinum), then the hardmode crates' by tier: a chest's those of its stage.
        /// </summary>
        static readonly ItemType[][] bars =
        {
            new[] { ItemType.CopperBar, ItemType.TinBar, ItemType.IronBar, ItemType.LeadBar },
            new[] { ItemType.IronBar, ItemType.LeadBar, ItemType.SilverBar, ItemType.TungstenBar },
            new[] { ItemType.SilverBar, ItemType.TungstenBar, ItemType.GoldBar, ItemType.PlatinumBar },
            new[] { ItemType.CobaltBar, ItemType.PalladiumBar },
            new[] { ItemType.MythrilBar, ItemType.OrichalcumBar },
            new[] { ItemType.AdamantiteBar, ItemType.TitaniumBar },
        };

        /// <summary>A chest's bars: one of its stage's, or the stage before's where none of them is unlocked.</summary>
        static ItemType Bar(int stage)
        {
            for (int s = Mathf.Clamp(stage, 0, bars.Length - 1); s >= 0; s--)
            {
                var ok = new List<ItemType>();
                foreach (var t in bars[s]) if (Unlocked(t)) ok.Add(t);
                if (ok.Count > 0) return Any(ok);
            }
            return ItemType.None;
        }

        /// <summary>WorldGen's chests' arrows, bullets, throwing weapons, explosives and rope, with how many of them.</summary>
        static readonly (ItemType Type, int Min, int Max)[] rounds =
        {
            (ItemType.WoodenArrow, 25, 50), (ItemType.FlamingArrow, 25, 50), (ItemType.JestersArrow, 25, 50), (ItemType.MusketBall, 25, 50),
            (ItemType.MeteorShot, 25, 50), (ItemType.SilverBullet, 25, 50), (ItemType.Shuriken, 25, 50), (ItemType.ThrowingKnife, 25, 50),
            (ItemType.Grenade, 5, 10), (ItemType.Bomb, 5, 10), (ItemType.Dynamite, 1, 3), (ItemType.Rope, 50, 100),
        };

        /// <summary>A piece's kind past the parts': armour.</summary>
        const int ArmourKind = 3;

        static bool Unlocked(ItemType t) => t != ItemType.None && ProgressionBalance.Unlocked(t);

        static ItemType Any(List<ItemType> from) => from.Count == 0 ? ItemType.None : from[Random.Range(0, from.Count)];

        static bool Fits(ItemDef d, int kind) => d != null && !d.Placeholder && d.MaxStack == 1 && !d.HkItem
            && (kind == WeaponPart ? d.IsWeapon && !d.IsAmmo && !d.GameTool : kind == AccessoryPart ? d.IsAccessory : kind == ArmourKind ? d.IsArmor : false);

        /// <summary>Of a chest's pieces or supplies (all of its stage or before), those of the latest stage among them and the one before.</summary>
        static List<ItemType> Latest(List<ItemType> pool)
        {
            int top = -1;
            foreach (var t in pool) top = Mathf.Max(top, ItemDb.Get(t).Stage);
            return pool.FindAll(t => ItemDb.Get(t).Stage >= top - 1);
        }

        /// <summary>The latest of the area's enemies' pieces of a kind at a stage (its neighbours' when it has none).</summary>
        static List<ItemType> EnemyPieces(string area, int stage, int kind)
        {
            var pool = new List<ItemType>();
            void From(string a)
            {
                foreach (var t in Fishing.AreaLootAt(a, stage).Gear)
                    if (Fits(ItemDb.Get(t), kind) && ItemDb.Get(t).Stage <= stage && ProgressionLoot.Droppable(t) && !pool.Contains(t)) pool.Add(t);
            }
            From(area);
            if (pool.Count == 0) foreach (var near in Fishing.NearAreas(area)) From(near);
            return Latest(pool);
        }

        /// <summary>
        /// The area's Terraria chests' pieces at a stage: sold in town or made at a bench as well or not (the user's, 2026-10-22),
        /// never a boss's own, a lunar piece or one of the shops' alone (ProgressionBalance.ShopOnlyWeapons).
        /// </summary>
        static List<ItemType> ChestPieces(string area, int stage)
        {
            var pool = new List<ItemType>();
            if (!chestPieces.TryGetValue(area, out var sets)) return pool;
            foreach (var set in sets)
                foreach (var t in set)
                {
                    var d = ItemDb.Get(t);
                    if (d != null && !d.Placeholder && d.Stage <= stage && Unlocked(t) && !BossDropPlan.BossOnly(t) && !GodhomeRewards.LunarEquipment(t)
                        && !ProgressionBalance.ShopOnlyWeapons.Contains(t) && !pool.Contains(t)) pool.Add(t);
                }
            return pool;
        }

        /// <summary>
        /// A chest's piece (Fishing.PickCollectionWeighted: one not had yet twice as likely). A weapon's or an accessory's
        /// chest: the latest of the area's enemies' of its kind, else the area's Terraria chest pieces of the kind, else any of
        /// them. A chest piece's: one of the area's Terraria chest pieces not had yet, else the latest of the area's enemies'
        /// weapons, accessories and armour. None of any: a piece of the stage that enemies drop.
        /// </summary>
        static ItemType Piece(string area, int part, int stage)
        {
            var inv = TerraMod.Instance?.Inventory;
            inv?.ObserveCollections();
            int kind = KindOf(part);
            var chest = ChestPieces(area, stage);
            List<ItemType> pool;
            if (kind == ChestPiecePart)
            {
                pool = chest.FindAll(t => inv == null || !inv.CollectedItems.Contains(t));
                if (pool.Count == 0)
                {
                    foreach (int k in new[] { WeaponPart, AccessoryPart, ArmourKind }) pool.AddRange(EnemyPieces(area, stage, k));
                    pool = Latest(pool);
                }
            }
            else
            {
                pool = EnemyPieces(area, stage, kind);
                if (pool.Count == 0) pool = chest.FindAll(t => Fits(ItemDb.Get(t), kind));
                if (pool.Count == 0) pool = Found(kind, stage);
                if (pool.Count == 0) pool = chest;
            }
            var piece = Fishing.PickCollectionWeighted(pool, ItemType.None);
            return piece != ItemType.None ? piece : Fishing.PickCollectionWeighted(Found(kind == ChestPiecePart ? AccessoryPart : kind, stage), ItemType.None);
        }

        /// <summary>Any piece of a kind that enemies drop, of the stage or the one before.</summary>
        static List<ItemType> Found(int kind, int stage)
        {
            var found = new List<ItemType>();
            foreach (var d in ItemDb.All)
                if (Fits(d, kind) && d.Source == ItemSource.Drop && d.Stage <= stage && d.Stage >= stage - 1 && ProgressionLoot.Droppable(d.Type)) found.Add(d.Type);
            return found;
        }

        static void Add(List<ItemStack> got, ItemType t, int min, int max, bool gate = true)
        {
            if (t == ItemType.None || gate && !Unlocked(t)) return;
            got.Add(ItemStack.Of(t, Random.Range(min, max + 1)));
        }

        /// <summary>A chest's contents (its part: what its piece is, KindOf), into its 40 slots.</summary>
        internal static ItemStack[] Contents(string area, int part, int stage)
        {
            var got = new List<ItemStack>();
            var t = Piece(area, part, stage);
            if (t != ItemType.None)
            {
                var s = ItemStack.Of(t);
                s.Prefix = (byte)Prefixes.RollNatural(ItemDb.Get(t));
                got.Add(s);
            }
            int key = StyleOf(area) - 1;
            var (_, stuff) = Fishing.AreaLootAt(area, stage);
            var materials = new List<ItemType>(stuff);
            if (materials.Count == 0) foreach (var near in Fishing.NearAreas(area)) materials.AddRange(Fishing.AreaLootAt(near, stage).Stuff);
            materials.RemoveAll(m => !Unlocked(m));
            // The area's materials, one or two kinds.
            int kinds = Random.Range(1, 3);
            for (int k = 0; k < kinds && materials.Count > 0; k++)
            {
                var m = Any(materials);
                materials.Remove(m);
                Add(got, m, 3, 10);
            }
            // Bars of the stage, one time in two.
            if (Random.Range(0, 2) == 0) Add(got, Bar(stage), 4, 10);
            // Two of the arrows, bullets, throwing weapons, explosives and rope.
            var kits = new List<(ItemType Type, int Min, int Max)>();
            foreach (var r in rounds) if (ItemDb.Get(r.Type) is ItemDef d && d.Stage <= stage && Unlocked(r.Type)) kits.Add(r);
            for (int k = 0; k < 2 && kits.Count > 0; k++)
            {
                var r = kits[Random.Range(0, kits.Count)];
                kits.Remove(r);
                Add(got, r.Type, r.Min, r.Max);
            }
            // Terraria's chest potions, one or two kinds of one to three.
            var potions = new List<ItemType>(chestPotions);
            potions.RemoveAll(p => !Unlocked(p));
            int potionKinds = Random.Range(1, 3);
            for (int k = 0; k < potionKinds && potions.Count > 0; k++)
            {
                var p = Any(potions);
                potions.Remove(p);
                Add(got, p, 1, 3);
            }
            // Healing potions, mana potions one time in two, a Recall Potion or two one time in three.
            ItemType heal = stage >= 4 && Unlocked(ItemType.GreaterHealingPotion) ? ItemType.GreaterHealingPotion
                : stage >= 2 && Unlocked(ItemType.HealingPotion) ? ItemType.HealingPotion : ItemType.LesserHealingPotion;
            Add(got, heal, 3, 6, false);
            ItemType mana = stage >= 4 && Unlocked(ItemType.GreaterManaPotion) ? ItemType.GreaterManaPotion
                : stage >= 2 && Unlocked(ItemType.ManaPotion) ? ItemType.ManaPotion : ItemType.LesserManaPotion;
            if (Random.Range(0, 2) == 0) Add(got, mana, 3, 6, false);
            if (Random.Range(0, 3) == 0) Add(got, ItemType.RecallPotion, 1, 2);
            // The area's torches (as the cave chests' torches).
            if (key >= 0)
                for (int s = 1; s <= RegionalTorches.Count; s++)
                    if (RegionalTorches.All[s - 1].Key == AreaBlocks.All[key].Key) Add(got, RegionalTorches.ItemOf(s), 10, 20, false);
            // Geo, more in the later areas (60-150 at stage 0, two and a half times it at stage 5).
            Add(got, ItemType.Geo, Mathf.RoundToInt(60 * (1f + 0.3f * stage)), Mathf.RoundToInt(150 * (1f + 0.3f * stage)), false);
            // Now and then a pet.
            var eggs = Pets.Eggs;
            if (eggs.Count > 0 && Random.Range(0, 30) == 0) Add(got, eggs[Random.Range(0, eggs.Count)], 1, 1);
            var slots = new ItemStack[Chests.Capacity];
            for (int i = 0; i < got.Count && i < slots.Length; i++) slots[i] = got[i];
            return slots;
        }

        static string Describe(ItemStack[] items)
        {
            var parts = new List<string>();
            foreach (var s in items) if (!s.IsEmpty) parts.Add(s.Def.Name + "×" + s.Count);
            return string.Join("，", parts);
        }

        // ------------------------------------------------------------------ dev

        /// <summary>Dev: each area's chests (scene, part, set down or not).</summary>
        public static string DevDescribe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in plans) sb.Append($" {p.Area}:{p.Scene}#{p.Part}{(p.Placed ? "✓" : "")}");
            return sb.ToString();
        }

        public static int DevCount => plans.Count;
        public static int DevPlacedCount => plans.FindAll(p => p.Placed).Count;
        public static List<string> DevScenesOf(string area) => plans.FindAll(p => p.Area == area).ConvertAll(p => p.Scene);
        public static int DevCandidates(string area) => Candidates(area).Count;
    }
}
