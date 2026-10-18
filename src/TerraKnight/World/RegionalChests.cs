using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The areas' chests, as Terraria's world gen leaves chests about its caves: a save begun since there were any gets them
    /// by the areas' size (the open cells of their scenes on the world atlas): five in the largest, the rest in proportion,
    /// one at the least, none in Godhome; each in its area's own chest (the crate pack's pictures, World.AreaBlocks.All's
    /// order), spread over the area's scenes in the save's own order. Each is set down the first time its scene comes in, on two
    /// cells of even ground with three free over them, reachable from the scene's gates, four cells clear of spikes, acid,
    /// lava and water, away from other chests, benches, gates and a boss's fighting ground; a scene with no such spot hands
    /// its chest to another of the area. Then it is an ordinary chest (opened, emptied, mined for its item, on the map once
    /// seen). What an area's hold: one a weapon its area's enemies drop, one an accessory of theirs (both of the stage of
    /// the area or the stage reached, the lower; an area of one chest one or the other), the rest the area's materials,
    /// Terraria's chest potions, healing potions, the area's torches and geo; any of them, now and then (1 in 40), a pet.
    /// A save from before has none.
    /// </summary>
    internal static class RegionalChests
    {
        /// <summary>The largest area's chests; the others' in proportion to their size.</summary>
        public const int PerArea = 5;
        /// <summary>The areas with none: Godhome (its halls are a boss rush's).</summary>
        static readonly HashSet<string> without = new HashSet<string> { "神居" };
        const float Cell = CellGrid.CellSize;
        const float Px = Effects.TerraParticles.Px;

        /// <summary>A chest's part: what it holds (0 a weapon, 1 an accessory, 2.. supplies).</summary>
        public const int WeaponPart = 0, AccessoryPart = 1;

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
        public static ItemType ItemOf(int style) => style >= 1 && style <= AreaBlocks.All.Length ? ItemType.ChestKingsPass + (style - 1) : ItemType.Chest;

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

        /// <summary>A new game: each area's five, over its scenes in the save's own order, their parts shuffled.</summary>
        public static void NewWorld(int seed = 0)
        {
            Clear();
            Seed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            var rng = new System.Random(Seed);
            int none = 0;
            foreach (var a in AreaBlocks.All)
            {
                var scenes = Candidates(a.Name);
                if (scenes.Count == 0) { none++; continue; }
                for (int i = scenes.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (scenes[i], scenes[j]) = (scenes[j], scenes[i]);
                }
                // The weapon's and the accessory's first (one of them for an area of one), then supplies; shuffled.
                int count = CountOf(a.Name);
                var parts = new List<int>();
                if (count == 1) parts.Add(rng.Next(2));
                else for (int k = 0; k < count; k++) parts.Add(k);
                for (int i = parts.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (parts[i], parts[j]) = (parts[j], parts[i]);
                }
                for (int k = 0; k < count; k++) plans.Add(new Plan { Area = a.Name, Scene = scenes[k % scenes.Count], Part = parts[k] });
            }
            Plugin.Log.LogInfo($"[地区储物箱] 这个存档的 {plans.Count} 个箱子分在 {AreaBlocks.All.Length - none} 个地区");
        }

        public static void Clear()
        {
            plans.Clear();
            Seed = 0;
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

        /// <summary>An area's chests: five for the largest, the rest in proportion (rounded, one at the least), none for Godhome.</summary>
        public static int CountOf(string area)
        {
            if (without.Contains(area)) return 0;
            var all = Sizes();
            int largest = 0;
            foreach (var a in AreaBlocks.All)
                if (!without.Contains(a.Name) && all.TryGetValue(a.Name, out int v) && v > largest) largest = v;
            if (largest <= 0 || !all.TryGetValue(area, out int size)) return 1;
            return Mathf.Clamp(Mathf.RoundToInt(PerArea * (float)size / largest), 1, PerArea);
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
                Plugin.Log.LogInfo($"[地区储物箱] {p.Scene}（{p.Area}）放下一个{(p.Part == WeaponPart ? "武器" : p.Part == AccessoryPart ? "饰品" : "补给")}箱 @{local:F1}：{Describe(items)}");
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

        /// <summary>WorldGen's chest potions (the cave chests' second roll).</summary>
        static readonly ItemType[] chestPotions =
        {
            ItemType.IronskinPotion, ItemType.SwiftnessPotion, ItemType.RegenerationPotion, ItemType.ShinePotion, ItemType.NightOwlPotion, ItemType.SpelunkerPotion,
            ItemType.MiningPotion, ItemType.HunterPotion, ItemType.ArcheryPotion, ItemType.GravitationPotion, ItemType.ThornsPotion, ItemType.WaterWalkingPotion,
            ItemType.InvisibilityPotion, ItemType.FeatherfallPotion, ItemType.BattlePotion, ItemType.BuilderPotion, ItemType.ObsidianSkinPotion,
            ItemType.HeartreachPotion, ItemType.TrapsightPotion, ItemType.RecallPotion,
        };

        static bool Unlocked(ItemType t) => t != ItemType.None && ProgressionBalance.Unlocked(t);

        static ItemType Any(List<ItemType> from) => from.Count == 0 ? ItemType.None : from[Random.Range(0, from.Count)];

        /// <summary>The area's weapons or accessories at a stage: its enemies' (its neighbours' when it has none), else any found item of the stage.</summary>
        static ItemType Piece(string area, int stage, bool weapon)
        {
            bool Fits(ItemDef d) => d != null && d.MaxStack == 1 && (weapon ? d.IsWeapon && !d.IsAmmo && !d.GameTool : d.IsAccessory) && !d.HkItem;
            var pool = new List<ItemType>();
            void From(string a)
            {
                foreach (var t in Fishing.AreaLootAt(a, stage).Gear) if (Fits(ItemDb.Get(t))) pool.Add(t);
            }
            From(area);
            if (pool.Count == 0) foreach (var near in Fishing.NearAreas(area)) From(near);
            var piece = Fishing.PickUnlocked(pool.ToArray());
            if (piece != ItemType.None) return piece;
            var found = new List<ItemType>();
            foreach (var d in ItemDb.All)
                if (Fits(d) && d.Source == ItemSource.Drop && d.Stage <= stage && d.Stage >= stage - 1) found.Add(d.Type);
            return Fishing.PickUnlocked(found.ToArray());
        }

        static void Add(List<ItemStack> got, ItemType t, int min, int max, bool gate = true)
        {
            if (t == ItemType.None || gate && !Unlocked(t)) return;
            got.Add(ItemStack.Of(t, Random.Range(min, max + 1)));
        }

        /// <summary>A chest's contents (part: 0 a weapon, 1 an accessory, else supplies), into its 40 slots.</summary>
        internal static ItemStack[] Contents(string area, int part, int stage)
        {
            var got = new List<ItemStack>();
            if (part == WeaponPart || part == AccessoryPart)
            {
                var t = Piece(area, stage, part == WeaponPart);
                if (t != ItemType.None)
                {
                    var s = ItemStack.Of(t);
                    s.Prefix = (byte)Prefixes.RollNatural(ItemDb.Get(t));
                    got.Add(s);
                }
            }
            int key = StyleOf(area) - 1;
            var (_, stuff) = Fishing.AreaLootAt(area, stage);
            var materials = new List<ItemType>(stuff);
            if (materials.Count == 0) foreach (var near in Fishing.NearAreas(area)) materials.AddRange(Fishing.AreaLootAt(near, stage).Stuff);
            materials.RemoveAll(t => !Unlocked(t));
            var potions = new List<ItemType>(chestPotions);
            potions.RemoveAll(t => !Unlocked(t));
            ItemType heal = stage >= 4 && Unlocked(ItemType.GreaterHealingPotion) ? ItemType.GreaterHealingPotion
                : stage >= 2 && Unlocked(ItemType.HealingPotion) ? ItemType.HealingPotion : ItemType.LesserHealingPotion;
            bool supplies = part != WeaponPart && part != AccessoryPart;
            // The area's materials.
            int kinds = supplies ? Random.Range(1, 3) : (Random.Range(0, 2) == 0 ? 1 : 0);
            for (int k = 0; k < kinds && materials.Count > 0; k++)
            {
                var m = Any(materials);
                materials.Remove(m);
                Add(got, m, 3, 10);
            }
            // Terraria's chest potions, one or two kinds of one or two (one kind in a weapon's or accessory's chest).
            int potionKinds = supplies ? Random.Range(1, 3) : 1;
            for (int k = 0; k < potionKinds && potions.Count > 0; k++)
            {
                var p = Any(potions);
                potions.Remove(p);
                Add(got, p, 1, 2);
            }
            Add(got, heal, supplies ? 3 : 2, supplies ? 5 : 3, false);
            // The area's torches (as the cave chests' torches).
            if (key >= 0)
                for (int s = 1; s <= RegionalTorches.Count; s++)
                    if (RegionalTorches.All[s - 1].Key == AreaBlocks.All[key].Key && (supplies || Random.Range(0, 2) == 0)) Add(got, RegionalTorches.ItemOf(s), 8, 15, false);
            // Geo, more in the later areas.
            int geoMin = supplies ? 20 : 10, geoMax = supplies ? 60 : 30;
            Add(got, ItemType.Geo, geoMin * (stage + 1), geoMax * (stage + 1), false);
            // Now and then a pet.
            var eggs = Pets.Eggs;
            if (eggs.Count > 0 && Random.Range(0, 40) == 0) Add(got, eggs[Random.Range(0, eggs.Count)], 1, 1);
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
