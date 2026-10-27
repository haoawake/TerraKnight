using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Save
{
    /// <summary>
    /// The mod's own save, one JSON file per save slot next to the game's saves. The game's
    /// save files are never touched. Written whenever the game saves (benches, quitting to the
    /// menu), read when a slot is loaded, so it follows the game's save semantics.
    /// </summary>
    internal static class ModSave
    {
        sealed class SaveFile
        {
            public int Version = 8;
            public List<string> HallownestObtained;
            public Dictionary<string, int> HallownestTradeOffsets;
            public Dictionary<string, int> HallownestNativeCounts;
            /// <summary>A creative-mode save (UI.CreativeMenu): items never run out, anything can be made.</summary>
            public bool Creative;
            /// <summary>World.DifficultyLevel (1 Normal to 5 Hollow); 0, from before the choice, is Master.</summary>
            public int Difficulty;
            /// <summary>World.EnemyAiKind: 0 Hollow Knight's own (every save from before the choice), 1 the new one.</summary>
            public int EnemyAi;
            public int LootStageSelection = -1;
            /// <summary>The world's and the character's names (UI.TerraMenu); none in a save from before.</summary>
            public string WorldName, PlayerName;
            /// <summary>The character's look (UI.CharacterCreation); none in a save from before: Terraria's default character.</summary>
            public Player.Appearance Look;
            public List<SlotItem> Inventory = new List<SlotItem>();
            /// <summary>Direct dream/ending rewards waiting for space or a safe room.</summary>
            public List<SlotItem> PendingEnemyLoot;
            /// <summary>The worn set (the current loadout's).</summary>
            public List<SlotItem> Accessories = new List<SlotItem>();
            public List<SlotItem> Armor = new List<SlotItem>();
            /// <summary>The social column (the current loadout's); none in a save from before it.</summary>
            public List<SlotItem> VanityArmor, VanityAccessories;
            /// <summary>The misc equipment: pet, light pet, minecart, mount, hook.</summary>
            public List<SlotItem> Misc;
            /// <summary>The Piggy Bank's slots; GeoItems: the save's geo is the Geo item already (Items.GeoWallet).</summary>
            public List<SlotItem> Bank;
            /// <summary>The Hallownest bag (Items.HkItems).</summary>
            public List<SlotItem> HkBag;
            /// <summary>The Safe's, the Defender's Forge's and the Void Vault's slots (Player.bank2 .. bank4).</summary>
            public List<SlotItem> Safe, Forge, Void;
            /// <summary>The Banner Case's and the Potion Box's slots, page after page (Items.Containers); Fav a favourited potion.</summary>
            public List<SlotItem> BannerCase, PotionBox;
            /// <summary>The Auto-Sell Bag's list, its entries page after page (an item each, no stack).</summary>
            public List<SlotItem> SellList;
            public List<string> DropBlacklist;
            public bool GeoItems;
            /// <summary>The save's Dream Essence is the item already (Items.EssenceWallet); false in a file from before, whose essence becomes the item once.</summary>
            public bool EssenceItems;
            /// <summary>Version 4: the worn loadout and the four loadouts' stored sets (the worn one's is empty). Fav marks a shared item.</summary>
            public int CurrentLoadout;
            public List<LoadoutSave> Loadouts;
            /// <summary>The Alt-to-share hint has been acted on once (Player.Settings.ShowLoadoutShareHint).</summary>
            public bool ShareHintSeen;
            public SlotItem Trash;
            /// <summary>Pylons unlocked by resting on their benches.</summary>
            public List<PylonSave> Pylons = new List<PylonSave>();
            /// <summary>Items lying in the world, by the scene they lie in (its own coordinates): no longer written or read (2.1.1), kept so old files still parse.</summary>
            public List<DroppedSave> Dropped = new List<DroppedSave>();
            /// <summary>Version 1 files only had the hotbar.</summary>
            public List<SlotItem> Hotbar;
            public int Selected;
            public int LifeMax = 100;
            public int ManaMax = 20;
            /// <summary>Version 3: life and mana crystals used (the maximums follow from them and the Knight's masks).</summary>
            public int LifeCrystals = -1, ManaCrystals = -1;
            /// <summary>Knight abilities already turned into accessories, so each is given once.</summary>
            public List<string> Granted = new List<string>();
            /// <summary>Version 5: copper beyond whole geo, the monster book (kills by record, first-kill order) and the townsfolk's names and arrivals.</summary>
            public int SpareCopper;
            public Casino.Saved Casino;
            public Dictionary<string, int> Kills;
            public List<string> KillOrder;
            /// <summary>The monster book's sightings: records in the order first seen, and the stats seen of each.</summary>
            public List<string> SeenOrder;
            public Dictionary<string, Bestiary.Seen> Seen;
            public Dictionary<string, string> NpcNames;
            public List<string> NpcArrived;
            /// <summary>The Tax Collector's taxes waiting (copper) and the seconds toward the next.</summary>
            public long Taxes;
            public float TaxClock;
            /// <summary>Townsfolk the player sent away with a right click in the panel: they stay away.</summary>
            public List<string> NpcDismissed;
            /// <summary>Townsfolk the player put down themselves: their internal name and the street x they were put at.</summary>
            public Dictionary<string, float> NpcHomes;
            /// <summary>The same spots' heights (so one on a roof stays on the roof).</summary>
            public Dictionary<string, float> NpcHomeY;
            /// <summary>The townsfolk with a house (Town.TownNpcsHousing): the scene and its cell of their home tile.</summary>
            public Dictionary<string, Town.TownNpcs.HouseSave> NpcHouses;
            /// <summary>The Angler: woken at the Blue Lake, today's fish and its day, handed in today, quests done.</summary>
            public bool AnglerSaved;
            public string AnglerQuest;
            public int AnglerDay = -1;
            public bool AnglerDone;
            public int AnglerQuests;
            public int? AnglerPending;
            public List<string> CollectedItems;
            /// <summary>A meteorite coming at midnight (World.Meteorites.Pending), and how many have come down.</summary>
            public bool MeteorPending;
            public int MeteorsLanded;
            /// <summary>Life Fruits eaten and the other permanent boosters used (Player.ConsumedLifeFruit, extraAccessory, usedAegisCrystal...).</summary>
            public int LifeFruits;
            public List<string> Boosts;
            /// <summary>The chests set down, by scene (their own coordinates), with what they hold.</summary>
            public List<ChestSave> Chests;
            /// <summary>The crafting stations set down (World.Stations).</summary>
            public List<StationSave> Stations;
            /// <summary>The Life Crystals lying in the early areas (World.LifeCrystals): the save's seed, then each one's scene, spot and whether it was taken.</summary>
            public int CrystalSeed;
            public List<CrystalSave> Crystals;
            /// <summary>The areas' chests of a save begun since there were any (World.RegionalChests): its seed, each chest's area, scene, part and whether set down; null for an older save (none).</summary>
            public int WorldChestSeed;
            public List<WorldChestSave> WorldChests;
            /// <summary>Scene name -> packed cell edits (index &lt;&lt; 8 | value, see <see cref="World.WorldEdits"/>).</summary>
            public Dictionary<string, Dictionary<int, int>> Walls = new Dictionary<string, Dictionary<int, int>>();
            public Dictionary<string, List<long>> Scenes = new Dictionary<string, List<long>>();
            /// <summary>Scene name -> the edits' high bytes where they have one (index &lt;&lt; 8 | high byte: the areas' blocks).</summary>
            public Dictionary<string, List<long>> ScenesHigh;
            /// <summary>Scene name -> the regional torches' styles on its torch cells (index &lt;&lt; 8 | style, World.RegionalTorches).</summary>
            public Dictionary<string, List<long>> TorchStyles;
            /// <summary>Scene name -> the liquid poured there (index &lt;&lt; 16 | kind &lt;&lt; 8 | amount, World.WorldLiquids).</summary>
            public Dictionary<string, List<long>> Liquids;
            /// <summary>The breakables that have given their drop (Items.BreakableDrops): scene, name and place.</summary>
            public List<string> LootedBreakables;
        }

        sealed class PylonSave
        {
            public string Scene, Bench;
            public float X, Y, W, H;
            public int Style;
            /// <summary>Set down by the player (World.PylonsMove), and its way in.</summary>
            public bool Placed;
            public string ArriveBench, Entry;
            /// <summary>Standing by its bench, not lit yet (World.PylonsDormant): its place kept.</summary>
            public bool Dormant;
        }

        sealed class DroppedSave
        {
            public string Scene, Type;
            public int Count;
            public float X, Y;
        }

        sealed class ChestSave
        {
            public string Scene;
            public float X, Y;
            /// <summary>Its name (ChestUI's Rename), "" for none.</summary>
            public string Name;
            /// <summary>Its look: 0 Terraria's chest, 1.. an area's (World.RegionalChests).</summary>
            public int Style;
            public List<SlotItem> Items = new List<SlotItem>();
        }

        sealed class StationSave
        {
            public string Scene, Kind;
            public float X, Y;
            /// <summary>Placed facing right (a chair, a bed, a relic).</summary>
            public bool Right;
        }

        sealed class CrystalSave
        {
            public string Scene;
            public bool Placed, Taken;
            public float X, Y;
        }

        sealed class WorldChestSave
        {
            public string Area, Scene;
            public int Part;
            public bool Placed;
        }

        sealed class LoadoutSave
        {
            public List<SlotItem> Armor = new List<SlotItem>();
            public List<SlotItem> Accessories = new List<SlotItem>();
            public List<SlotItem> VanityArmor, VanityAccessories;
            /// <summary>Version 6, creative: the loadout's own bag and hotbar pick (the worn one's is empty; its bag is Inventory).</summary>
            public List<SlotItem> Bag;
            public int Selected;
        }

        sealed class SlotItem
        {
            public string Type;
            public int Count;
            public bool Fav;
            /// <summary>A reforged item's prefix (PrefixID), 0 none.</summary>
            public int Prefix;
            /// <summary>A forged weapon's level (Items.Forging), 0 none.</summary>
            public int Forge;
        }

        static SlotItem Pack(ItemStack s) => new SlotItem { Type = s.Type.ToString(), Count = s.Count, Fav = s.Favorite, Prefix = s.Prefix, Forge = s.Forge };

        /// <summary>Dev: a stack through the save's packing and back.</summary>
        internal static ItemStack DevRoundTrip(ItemStack s) => Unpack(Pack(s));

        static ItemStack Unpack(SlotItem s)
        {
            if (s == null || !Enum.TryParse(s.Type, out ItemType type)) return default;
            // The God Tamer's seal is no more: one kept comes back as the colosseum's summon (Items.ColosseumTicket).
            if (type == ItemType.BossSigilGodTamer) type = Items.ColosseumTicket.Type;
            var stack = ItemStack.Of(type, s.Count);
            stack.Favorite = s.Fav;
            // A prefix the item can't have (or one past the list) is dropped.
            if (s.Prefix > 0 && Items.Prefixes.Valid(Items.ItemDb.Get(type), s.Prefix)) stack.Prefix = (byte)s.Prefix;
            // A weapon's forge level; a treasure bag's first-kill mark in the same byte.
            if (s.Forge > 0 && (Items.Forging.Forgeable(Items.ItemDb.Get(type)) || Items.TreasureBags.Is(type))) stack.Forge = (byte)Mathf.Min(s.Forge, 255);
            return stack;
        }

        /// <summary>The save's world and character names (chosen when it was created; empty in a save from before).</summary>
        public static string WorldName = "", PlayerName = "";

        /// <summary>Whether a slot's save is a creative one.</summary>
        public static bool IsCreative(int slot) => ModeOf(slot).Creative;

        /// <summary>
        /// A slot's mode: creative or not, and its difficulty (Known: the slot has a mod save at
        /// all). Read from its file's text, kept while the file is unchanged.
        /// </summary>
        public static (bool Known, bool Creative, World.DifficultyLevel Difficulty) ModeOf(int slot)
        {
            var h = HeaderOf(slot);
            return h == null ? (false, false, World.DifficultyLevel.Master) : (true, h.Creative, h.Difficulty);
        }

        /// <summary>What the start menu's world list shows of a save (its mod save's first fields).</summary>
        internal sealed class Header
        {
            public bool Creative;
            public World.DifficultyLevel Difficulty = World.DifficultyLevel.Master;
            public World.EnemyAiKind EnemyAi;
            public string WorldName, PlayerName;
            public Player.Appearance Look;
        }

        /// <summary>
        /// A slot's header (null: no mod save there): read from the start of its file up to the bag, which every field the
        /// menu wants comes before, and kept while the file is unchanged.
        /// </summary>
        public static Header HeaderOf(int slot)
        {
            string path = PathFor(slot);
            if (slot < 1 || !File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (headerCache.TryGetValue(slot, out var c) && c.Stamp == stamp) return c.Head;
            var head = new Header();
            try
            {
                using (var reader = new JsonTextReader(new StreamReader(path)))
                {
                    var serializer = new JsonSerializer();
                    if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                        {
                            string name = (string)reader.Value;
                            if (name == nameof(SaveFile.Inventory)) break;
                            reader.Read();
                            switch (name)
                            {
                                case nameof(SaveFile.Creative): head.Creative = reader.TokenType == JsonToken.Boolean && (bool)reader.Value; break;
                                case nameof(SaveFile.Difficulty): head.Difficulty = World.GameDifficulty.Parse(reader.TokenType == JsonToken.Integer ? Convert.ToInt32(reader.Value) : 0); break;
                                case nameof(SaveFile.EnemyAi): head.EnemyAi = World.EnemyAiMode.Parse(reader.TokenType == JsonToken.Integer ? Convert.ToInt32(reader.Value) : 0); break;
                                case nameof(SaveFile.WorldName): head.WorldName = reader.Value as string; break;
                                case nameof(SaveFile.PlayerName): head.PlayerName = reader.Value as string; break;
                                case nameof(SaveFile.Look): head.Look = reader.TokenType == JsonToken.StartObject ? serializer.Deserialize<Player.Appearance>(reader) : null; break;
                                default: reader.Skip(); break;
                            }
                        }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[存档] 读存档 {slot} 的列表信息失败：{e.Message}"); }
            headerCache[slot] = (stamp, head);
            return head;
        }

        static readonly Dictionary<int, (DateTime Stamp, Header Head)> headerCache = new Dictionary<int, (DateTime, Header)>();

        /// <summary>
        /// A save's world renamed from the start menu: the name changed in its mod save (written whole beside it, then
        /// swapped in, as a save is). False when the slot has no mod save or it couldn't be written.
        /// </summary>
        public static bool Rename(int slot, string name)
        {
            string path = PathFor(slot);
            if (slot < 1 || !File.Exists(path)) return false;
            try
            {
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                var prop = root.Property(nameof(SaveFile.WorldName));
                if (prop != null) prop.Value = name ?? "";
                else
                {
                    // Ahead of the bag, where the list's reader stops (a save from before has no name yet).
                    var named = new Newtonsoft.Json.Linq.JProperty(nameof(SaveFile.WorldName), name ?? "");
                    var bag = root.Property(nameof(SaveFile.Inventory));
                    if (bag != null) bag.AddBeforeSelf(named);
                    else root.Add(named);
                }
                string json = root.ToString(Formatting.None);
                File.WriteAllText(path + ".new", json);
                File.Replace(path + ".new", path, path + ".bak", true);
                headerCache.Remove(slot);
                if (stateOf == slot) WorldName = name ?? "";
                Plugin.Log.LogInfo($"[存档] 存档 {slot} 的世界改名为“{name}”");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[存档] 存档 {slot} 改名失败：{e.Message}");
                return false;
            }
        }

        /// <summary>The mod's save of a slot gone (the slot is being started over).</summary>
        public static void Delete(int slot)
        {
            string path = PathFor(slot);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                headerCache.Remove(slot);
                UI.WorldMap.Delete(slot);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"删除 mod 存档失败：{e.Message}"); }
        }

        static string PathFor(int slot) => Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json");

        /// <summary>
        /// The slot whose save the mod's state now is (read from it, or begun there as a new game);
        /// -1 before any. A save of another slot never gets this state written into it.
        /// </summary>
        static int stateOf = -1;

        /// <summary>Dev: the slot the mod's state belongs to.</summary>
        public static int DevStateSlot => stateOf;

        /// <summary>A new game begins in a slot: the mod's fresh state is that slot's.</summary>
        public static void BeganNewGame(int slot) => stateOf = slot;

        public static bool LastWriteSucceeded { get; private set; }
        internal static bool CommitCasino(){if(stateOf<0||TerraMod.Instance==null)return false;Write(stateOf,TerraMod.Instance);return LastWriteSucceeded;}
        public static void Write(int slot, TerraMod mod)
        {
            LastWriteSucceeded=false;
            if (slot < 0) return;
            if (slot != stateOf)
            {
                Plugin.Log.LogWarning($"[存档] 游戏在存存档 {slot}，但 mod 现在的内容属于存档 {stateOf}：不写，存档 {slot} 原来的 mod 存档保持不变。调用：{Caller()}");
                return;
            }
            var inv = mod.Inventory;
            mod.ReturnOrDropMouseItem();
            // The Guide's slot too: an item shown to him belongs to the bag.
            Town.TownNpcs.ReturnGuideItem();
            Town.TownNpcs.ReturnReforgeItem();
            var file = new SaveFile { LootStageSelection = LootStage.Selection, Creative = mod.Creative, Difficulty = (int)World.GameDifficulty.Current, EnemyAi = (int)World.EnemyAiMode.Current,
                WorldName = WorldName, PlayerName = PlayerName, Look = Player.Appearance.Current, Selected = inv.Selected, LifeMax = mod.Stats.LifeMax, ManaMax = mod.Stats.ManaMax, LifeCrystals = mod.Stats.LifeCrystals, ManaCrystals = mod.Stats.ManaCrystals };
            HallownestRecovery.Observe(inv);
            inv.ObserveCollections();
            file.CollectedItems = new List<string>();
            foreach (var t in inv.CollectedItems) file.CollectedItems.Add(t.ToString());
            file.HallownestObtained = new List<string>();
            foreach (var t in inv.HallownestObtained) file.HallownestObtained.Add(t.ToString());
            file.HallownestTradeOffsets = new Dictionary<string, int>();
            foreach (var kv in inv.HallownestTradeOffsets) file.HallownestTradeOffsets[kv.Key.ToString()] = kv.Value;
            file.HallownestNativeCounts = new Dictionary<string, int>();
            foreach (var kv in inv.HallownestNativeCounts) file.HallownestNativeCounts[kv.Key.ToString()] = kv.Value;
            foreach (var s in inv.Slots) file.Inventory.Add(Pack(s));
            foreach (var s in inv.Accessories) file.Accessories.Add(Pack(s));
            foreach (var s in inv.Armor) file.Armor.Add(Pack(s));
            file.VanityArmor = PackAll(inv.VanityArmor);
            file.VanityAccessories = PackAll(inv.VanityAccessories);
            file.Misc = new List<SlotItem>();
            foreach (var s in inv.Misc) file.Misc.Add(Pack(s));
            file.Bank = new List<SlotItem>();
            foreach (var s in inv.Bank) file.Bank.Add(Pack(s));
            file.HkBag = new List<SlotItem>();
            foreach (var s in inv.HkBag) file.HkBag.Add(Pack(s));
            file.Safe = new List<SlotItem>();
            foreach (var s in inv.Safe) file.Safe.Add(Pack(s));
            file.Forge = new List<SlotItem>();
            foreach (var s in inv.Forge) file.Forge.Add(Pack(s));
            file.Void = new List<SlotItem>();
            foreach (var s in inv.Void) file.Void.Add(Pack(s));
            file.BannerCase = new List<SlotItem>();
            foreach (var page in inv.BannerCase) foreach (var s in page) file.BannerCase.Add(Pack(s));
            file.PotionBox = new List<SlotItem>();
            foreach (var page in inv.PotionBox) foreach (var s in page) file.PotionBox.Add(Pack(s));
            file.SellList = new List<SlotItem>();
            foreach (var page in inv.SellList) foreach (var s in page) file.SellList.Add(Pack(s));
            file.DropBlacklist = LootBlacklist.Save(inv);
            file.GeoItems = GeoWallet.Migrated;
            file.EssenceItems = EssenceWallet.Migrated;
            file.CurrentLoadout = inv.CurrentLoadout;
            file.Loadouts = new List<LoadoutSave>();
            foreach (var l in inv.Loadouts)
            {
                var ls = new LoadoutSave();
                foreach (var s in l.Armor) ls.Armor.Add(Pack(s));
                foreach (var s in l.Accessories) ls.Accessories.Add(Pack(s));
                ls.VanityArmor = PackAll(l.VanityArmor);
                ls.VanityAccessories = PackAll(l.VanityAccessories);
                if (inv.BagPerLoadout)
                {
                    ls.Bag = new List<SlotItem>();
                    foreach (var s in l.Bag) ls.Bag.Add(Pack(s));
                    ls.Selected = l.Selected;
                }
                file.Loadouts.Add(ls);
            }
            file.ShareHintSeen = InventoryGui.ShareHintSeen;
            file.SpareCopper = Shop.SpareCopper;
            file.Casino = Items.Casino.Snapshot();
            file.Kills = new Dictionary<string, int>(Bestiary.Kills);
            file.KillOrder = new List<string>(Bestiary.Order);
            file.SeenOrder = new List<string>(Bestiary.SeenOrder);
            file.Seen = new Dictionary<string, Bestiary.Seen>(Bestiary.Stats);
            file.NpcNames = new Dictionary<string, string>(Town.TownNpcs.Names);
            file.NpcArrived = new List<string>(Town.TownNpcs.Arrived);
            file.Taxes = Town.TownNpcs.Taxes;
            file.TaxClock = Town.TownNpcs.TaxClock;
            file.NpcDismissed = new List<string>(Town.TownNpcs.Dismissed);
            file.NpcHomes = new Dictionary<string, float>(Town.TownNpcs.Homes);
            file.NpcHomeY = new Dictionary<string, float>(Town.TownNpcs.HomeY);
            file.NpcHouses = Town.TownNpcs.SaveHouses();
            file.AnglerSaved = Town.Angler.Saved;
            file.AnglerQuest = Town.Angler.Quest == ItemType.None ? null : Town.Angler.Quest.ToString();
            file.AnglerDay = Town.Angler.QuestDay;
            file.AnglerDone = Town.Angler.QuestDone;
            file.AnglerQuests = Town.Angler.QuestsDone;
            file.AnglerPending = Town.Angler.Pending;
            file.MeteorPending = World.Meteorites.Pending;
            file.MeteorsLanded = World.Meteorites.Landed;
            file.Trash = Pack(inv.Trash);
            file.Granted.AddRange(mod.GrantedAbilities);
            file.PendingEnemyLoot = new List<SlotItem>();
            foreach(var item in EnemyDrops.PendingLoot()) file.PendingEnemyLoot.Add(Pack(item));
            // Creative's pylons not yet placed by their bench are made again when the save is read.
            foreach (var p in World.Pylons.Unlocked)
                if (!p.Provisional) file.Pylons.Add(new PylonSave { Scene = p.Scene, Bench = p.Bench, X = p.Local.x, Y = p.Local.y, W = p.SceneSize.x, H = p.SceneSize.y, Style = p.Style,
                    Placed = p.Placed, ArriveBench = p.ArriveBench, Entry = p.Entry });
            foreach (var p in World.Pylons.Dormant)
                file.Pylons.Add(new PylonSave { Scene = p.Scene, Bench = p.Bench, X = p.Local.x, Y = p.Local.y, W = p.SceneSize.x, H = p.SceneSize.y, Style = p.Style, Dormant = true });
            file.LifeFruits = mod.Stats.LifeFruits;
            file.Boosts = new List<string>();
            foreach (var b in mod.Stats.Boosts) file.Boosts.Add(b.ToString());
            file.Stations = new List<StationSave>();
            foreach (var s in Net.WorldSync.SaveStations()) file.Stations.Add(new StationSave { Scene = s.Scene, Kind = s.Kind.ToString(), X = s.Local.x, Y = s.Local.y, Right = s.Right });
            file.Chests = new List<ChestSave>();
            foreach (var c in Net.WorldSync.SaveChests())
            {
                var cs = new ChestSave { Scene = c.Scene, X = c.Local.x, Y = c.Local.y, Name = c.Name, Style = c.Style };
                foreach (var it in c.Items) cs.Items.Add(Pack(it));
                file.Chests.Add(cs);
            }
            file.WorldChestSeed = World.RegionalChests.Seed;
            if (World.RegionalChests.Planned)
            {
                file.WorldChests = new List<WorldChestSave>();
                foreach (var c in World.RegionalChests.Save()) file.WorldChests.Add(new WorldChestSave { Area = c.Area, Scene = c.Scene, Part = c.Part, Placed = c.Placed });
            }
            file.CrystalSeed = Net.WorldSync.SaveSeed;
            file.Crystals = new List<CrystalSave>();
            foreach (var c in Net.WorldSync.SaveCrystals()) file.Crystals.Add(new CrystalSave { Scene = c.Scene, Placed = c.Placed, Taken = c.Taken, X = c.Local.x, Y = c.Local.y });
            // Items lying in the world aren't kept (Terraria saves none with the world): quitting clears them,
            // so they never pile up from one session to the next.
            foreach (var kv in Net.WorldSync.SaveWalls()) file.Walls[kv.Key] = new Dictionary<int, int>(kv.Value);
            foreach (var kv in Net.WorldSync.SaveEdits())
            {
                var cells = new List<long>(kv.Value.Count);
                List<long> high = null;
                foreach (var cell in kv.Value)
                {
                    cells.Add(((long)cell.Key << 8) | (cell.Value & 0xFFu));
                    if (cell.Value >> 8 == 0) continue;
                    if (high == null) high = new List<long>();
                    high.Add(((long)cell.Key << 8) | (uint)(cell.Value >> 8));
                }
                file.Scenes[kv.Key] = cells;
                if (high != null) (file.ScenesHigh ??= new Dictionary<string, List<long>>())[kv.Key] = high;
            }
            {
                // The styles of the torches the saved edits hold.
                var torchEdits = new Dictionary<string, Dictionary<int, ushort>>();
                foreach (var kv in Net.WorldSync.SaveEdits()) torchEdits[kv.Key] = kv.Value;
                file.TorchStyles = World.RegionalTorches.Pack(Net.WorldSync.SaveTorchStyles(), (scene, index) =>
                {
                    if (!torchEdits.TryGetValue(scene, out var edits) || !edits.TryGetValue(index, out ushort value)) return false;
                    World.WorldEdits.Unpack(value, out var kind, out _);
                    return kind == World.CellKind.Torch;
                });
            }
            World.WorldLiquids.CaptureLive();
            foreach (var kv in World.WorldLiquids.All)
            {
                if (kv.Value.Count == 0) continue;
                var cells = new List<long>(kv.Value.Count);
                foreach (var cell in kv.Value) cells.Add(((long)cell.Key << 16) | cell.Value);
                (file.Liquids ??= new Dictionary<string, List<long>>())[kv.Key] = cells;
            }
            file.LootedBreakables = BreakableDrops.Save();
            try
            {
                string path = PathFor(slot);
                string json = JsonConvert.SerializeObject(file);
                // Written whole to the side first, then swapped in, the last one kept as .bak: a
                // crash or a full disk mid-write leaves the old save, never half a new one.
                File.WriteAllText(path + ".new", json);
                if (File.ReadAllText(path + ".new").Length != json.Length) throw new IOException("写入后读回的长度不对");
                if (File.Exists(path)) File.Replace(path + ".new", path, path + ".bak", true);
                else File.Move(path + ".new", path);
                LastWriteSucceeded=true;
                UI.WorldMap.Save(slot);
                Plugin.Log.LogInfo($"已保存 mod 存档：{path}（{file.Scenes.Count} 个场景有改动），调用：{Caller()}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"mod 存档写入失败：{e}");
            }
        }

        /// <summary>
        /// The slot's save as written last: its file, else (unreadable or gone mid-swap) the one
        /// before it (.bak) or one fully written but not yet swapped in (.new). An unreadable file
        /// is kept aside (.broken-time) before anything can overwrite it.
        /// </summary>
        static SaveFile ParseSave(string path, out string from)
        {
            from = null;
            string broken = null;
            foreach (var p in new[] { path, path + ".new", path + ".bak" })
            {
                if (!File.Exists(p)) continue;
                try
                {
                    var file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(p));
                    if (file == null) throw new InvalidDataException("空文件");
                    from = p;
                    if (broken != null) Plugin.Log.LogWarning($"[存档] {Path.GetFileName(broken)} 读不了，改用 {Path.GetFileName(p)}");
                    return file;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[存档] {Path.GetFileName(p)} 读不了：{e.Message}");
                    if (p == path)
                    {
                        broken = p;
                        try { File.Copy(p, p + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); }
                        catch (Exception) { }
                    }
                }
            }
            return null;
        }

        /// <summary>The save's enemy AI, look and names as read (a save from before them: Hollow Knight's AI, Terraria's default character, no names).</summary>
        static void SetIdentity(SaveFile file)
        {
            World.EnemyAiMode.Current = World.EnemyAiMode.Parse(file?.EnemyAi ?? 0);
            Player.Appearance.Current = file?.Look != null ? file.Look.Clone() : Player.Appearance.Default();
            WorldName = file?.WorldName ?? "";
            PlayerName = file?.PlayerName ?? "";
        }

        public static void Read(int slot, TerraMod mod)
        {
            // The last world's building and changes all gone before this save's are read (World.WorldReset).
            World.WorldReset.All();
            mod.ResetPlayer();
            stateOf = slot;
            string path = PathFor(slot);
            bool any = slot >= 0 && (File.Exists(path) || File.Exists(path + ".new") || File.Exists(path + ".bak"));
            // The map belongs with the mod's save: a slot without one starts a fresh map.
            UI.WorldMap.Load(any ? slot : -1);
            var file = any ? ParseSave(path, out _) : null;
            if (file == null)
            {
                // No mod save of this slot yet: its geo becomes the item now.
                GeoWallet.Reset(false);
                EssenceWallet.Reset(false);
                if (any) Plugin.Log.LogError($"[存档] 存档 {slot} 的 mod 存档全都读不了，按新档开始（原文件留在 .broken-* 里）");
                mod.ApplyCreative(false);
                World.GameDifficulty.Current = World.DifficultyLevel.Master;
                SetIdentity(null);
                return;
            }
            try
            {
                var inv = mod.Inventory;
                inv.Clear();
                mod.ApplyCreative(file.Creative);
                World.GameDifficulty.Current = World.GameDifficulty.Parse(file.Difficulty);
                SetIdentity(file);
                LootStage.Load(file.LootStageSelection);
                // The boosters first: the Demon Heart's slot decides what is worn.
                mod.Stats.LifeFruits = Mathf.Clamp(file.LifeFruits, 0, Player.TerraStats.MaxLifeFruits);
                if (file.Boosts != null)
                    foreach (var b in file.Boosts)
                        if (Enum.TryParse(b, out PermanentBoost boost) && boost != PermanentBoost.None && boost != PermanentBoost.LifeFruit) mod.Stats.Boosts.Add(boost);
                Load(file.Inventory.Count > 0 ? file.Inventory : file.Hotbar, inv.Slots);
                Load(file.Accessories, inv.Accessories);
                Load(file.Armor, inv.Armor);
                Load(file.VanityArmor, inv.VanityArmor);
                Load(file.VanityAccessories, inv.VanityAccessories);
                Load(file.Misc, inv.Misc);
                Load(file.Bank, inv.Bank);
                if (file.HkBag != null) Load(file.HkBag, inv.HkBag);
                Load(file.Safe, inv.Safe);
                Load(file.Forge, inv.Forge);
                Load(file.Void, inv.Void);
                LoadPages(file.BannerCase, inv.BannerCase);
                LoadPages(file.PotionBox, inv.PotionBox);
                LoadPages(file.SellList, inv.SellList);
                LootBlacklist.Load(inv, file.DropBlacklist);
                GeoWallet.Reset(file.GeoItems);
                EssenceWallet.Reset(file.EssenceItems);
                if (file.Version >= 4 && file.Loadouts != null)
                {
                    bool bags = false;
                    for (int l = 0; l < file.Loadouts.Count && l < Inventory.LoadoutCount; l++)
                    {
                        Load(file.Loadouts[l].Armor, inv.Loadouts[l].Armor);
                        Load(file.Loadouts[l].Accessories, inv.Loadouts[l].Accessories);
                        Load(file.Loadouts[l].VanityArmor, inv.Loadouts[l].VanityArmor);
                        Load(file.Loadouts[l].VanityAccessories, inv.Loadouts[l].VanityAccessories);
                        if (inv.BagPerLoadout && file.Loadouts[l].Bag != null)
                        {
                            Load(file.Loadouts[l].Bag, inv.Loadouts[l].Bag);
                            inv.Loadouts[l].Selected = Mathf.Clamp(file.Loadouts[l].Selected, 0, Inventory.HotbarSize - 1);
                            bags = true;
                        }
                    }
                    inv.CurrentLoadout = Mathf.Clamp(file.CurrentLoadout, 0, Inventory.LoadoutCount - 1);
                    if (inv.BagPerLoadout)
                    {
                        // The worn loadout's bag is the one out; its stored one stays empty.
                        var worn = inv.Loadouts[inv.CurrentLoadout];
                        for (int i = 0; i < worn.Bag.Length; i++) worn.Bag[i] = default;
                        worn.Selected = 0;
                        // A creative save from before the four bags: the bag it had stays with the worn
                        // loadout, the other three get their class's showcase bag.
                        if (!bags)
                        {
                            for (int l = 0; l < Inventory.LoadoutCount; l++)
                                if (l != inv.CurrentLoadout) ShowcaseKit.FillBag(inv.Loadouts[l].Bag, l);
                            Plugin.Log.LogInfo($"[存档] 旧的创造存档：背包留给配装{inv.CurrentLoadout + 1}，另外三个配装各配上自己职业的展示背包");
                        }
                    }
                    // The worn loadout's storage is empty; anything there (a damaged file) goes back into the bag.
                    var own = inv.Loadouts[inv.CurrentLoadout];
                    for (int i = 0; i < own.Armor.Length; i++) { if (!own.Armor[i].IsEmpty) inv.Pickup(own.Armor[i]); own.Armor[i] = default; }
                    for (int i = 0; i < own.Accessories.Length; i++) { if (!own.Accessories[i].IsEmpty) inv.Pickup(own.Accessories[i]); own.Accessories[i] = default; }
                    for (int i = 0; i < own.VanityArmor.Length; i++) { if (!own.VanityArmor[i].IsEmpty) inv.Pickup(own.VanityArmor[i]); own.VanityArmor[i] = default; }
                    for (int i = 0; i < own.VanityAccessories.Length; i++) { if (!own.VanityAccessories[i].IsEmpty) inv.Pickup(own.VanityAccessories[i]); own.VanityAccessories[i] = default; }
                    InventoryGui.ShareHintSeen = file.ShareHintSeen;
                }
                else
                {
                    // Version 3 had one set: it is the melee loadout's. The accessories standing in for the
                    // Knight's abilities are shared, so switching to another loadout never takes them away.
                    inv.CurrentLoadout = 0;
                    for (int i = 0; i < inv.Accessories.Length; i++)
                        foreach (var (_, item) in Player.PlayerHooks.AbilityItems)
                            if (inv.Accessories[i].Type == item && !inv.Accessories[i].IsEmpty) inv.Accessories[i].Favorite = true;
                }
                inv.RefreshEffective();
                Shop.SpareCopper = Mathf.Clamp(file.SpareCopper, 0, 99);
                Items.Casino.Load(file.Casino);
                if (file.Kills != null) foreach (var kv in file.Kills) Bestiary.Kills[kv.Key] = kv.Value;
                if (file.KillOrder != null) foreach (var r in file.KillOrder) if (Bestiary.Kills.ContainsKey(r) && !Bestiary.Order.Contains(r)) Bestiary.Order.Add(r);
                if (file.Seen != null) foreach (var kv in file.Seen) if (kv.Value != null) Bestiary.Stats[kv.Key] = kv.Value;
                if (file.SeenOrder != null) foreach (var r in file.SeenOrder) if (!Bestiary.SeenOrder.Contains(r)) Bestiary.SeenOrder.Add(r);
                // Kills from before the book kept sightings count as seen (their stats from the roster until seen again).
                foreach (var r in Bestiary.Order) if (!Bestiary.SeenOrder.Contains(r)) Bestiary.SeenOrder.Add(r);
                if (file.NpcNames != null) foreach (var kv in file.NpcNames) Town.TownNpcs.Names[kv.Key] = kv.Value;
                if (file.NpcArrived != null) Town.TownNpcs.Arrived.UnionWith(file.NpcArrived);
                Town.TownNpcs.Taxes = System.Math.Max(0, System.Math.Min(Town.TownNpcs.TaxMost, file.Taxes));
                Town.TownNpcs.TaxClock = Mathf.Clamp(file.TaxClock, 0f, 60f);
                if (file.NpcDismissed != null) Town.TownNpcs.Dismissed.UnionWith(file.NpcDismissed);
                if (file.NpcHomes != null) foreach (var kv in file.NpcHomes) Town.TownNpcs.Homes[kv.Key] = kv.Value;
                if (file.NpcHomeY != null) foreach (var kv in file.NpcHomeY) Town.TownNpcs.HomeY[kv.Key] = kv.Value;
                Town.TownNpcs.LoadHouses(file.NpcHouses);
                Town.Angler.Saved = file.AnglerSaved;
                Town.Angler.Quest = file.AnglerQuest != null && System.Enum.TryParse(file.AnglerQuest, out ItemType aq) ? aq : ItemType.None;
                Town.Angler.QuestDay = file.AnglerDay;
                Town.Angler.QuestDone = file.AnglerDone;
                Town.Angler.QuestsDone = file.AnglerQuests;
                Town.Angler.Pending = Mathf.Clamp(file.AnglerPending ?? (file.AnglerDone ? 0 : file.AnglerQuest != null ? 1 : 0), 0, 3);
                if (file.CollectedItems != null)
                    foreach (var name in file.CollectedItems)
                        if (Enum.TryParse(name, out ItemType collected)) inv.RecordCollection(collected);
                inv.ObserveCollections();
                World.Meteorites.Pending = file.MeteorPending;
                World.Meteorites.Landed = file.MeteorsLanded;
                inv.Trash = Unpack(file.Trash);
                if (file.HallownestObtained != null)
                    foreach (var name in file.HallownestObtained)
                        if (Enum.TryParse(name, out ItemType t)) HallownestRecovery.Record(inv, t);
                if (file.HallownestTradeOffsets != null)
                    foreach (var kv in file.HallownestTradeOffsets)
                        if (Enum.TryParse(kv.Key, out ItemType t) && HallownestRecovery.IsUnique(t)) inv.HallownestTradeOffsets[t] = kv.Value;
                if (file.HallownestNativeCounts != null)
                    foreach (var kv in file.HallownestNativeCounts)
                        if (Enum.TryParse(kv.Key, out ItemType t) && HkItems.Is(t)) inv.HallownestNativeCounts[t] = kv.Value;
                HallownestRecovery.Observe(inv);
                inv.Selected = Mathf.Clamp(file.Selected, 0, Inventory.HotbarSize - 1);
                // Older files kept only the maximums; every 20 above the start was a crystal.
                mod.Stats.LifeCrystals = file.LifeCrystals >= 0 ? file.LifeCrystals : Mathf.Clamp((file.LifeMax - 100) / 20, 0, Player.TerraStats.MaxLifeCrystals);
                mod.Stats.ManaCrystals = file.ManaCrystals >= 0 ? file.ManaCrystals : Mathf.Clamp((file.ManaMax - 20) / 20, 0, Player.TerraStats.MaxManaCrystals);
                mod.Stats.UpdateMaximums(PlayerData.instance, 0);
                mod.Stats.FullHeal();
                mod.GrantedAbilities.Clear();
                if (file.Granted != null) mod.GrantedAbilities.UnionWith(file.Granted);
                var pendingLoot = new List<ItemStack>();
                if(file.PendingEnemyLoot!=null)foreach(var item in file.PendingEnemyLoot)pendingLoot.Add(Unpack(item));
                EnemyDrops.RestorePendingLoot(pendingLoot);
                // Before acquisition history existed, these flags were committed only after delivery.
                foreach (var reward in Player.PlayerHooks.AbilityItems)
                    if (mod.GrantedAbilities.Contains(reward.flag)) HallownestRecovery.Record(inv, reward.item);
                if (mod.GrantedAbilities.Contains("quakeLevel.dark.v1")) HallownestRecovery.Record(inv, ItemType.DescendingDarkRune);
                Net.BossLifeRewards.InitializeClaims(mod);
                VanillaBossProgression.Initialize();
                if (file.Pylons != null)
                    foreach (var p in file.Pylons)
                        (p.Dormant ? World.Pylons.Dormant : World.Pylons.Unlocked).Add(new World.Pylons.Pylon { Scene = p.Scene, Bench = p.Bench, Local = new Vector2(p.X, p.Y), SceneSize = new Vector2(p.W, p.H), Style = p.Style,
                            Placed = p.Placed, ArriveBench = p.ArriveBench, Entry = p.Entry, Dormant = p.Dormant });
                // Items a save from before 2.1.1 kept lying in the world are let go (see Write).
                if (file.Dropped != null && file.Dropped.Count > 0) Plugin.Log.LogInfo($"[存档] 地上的 {file.Dropped.Count} 堆掉落物不再读回（退出游戏就清掉）");
                if (file.Stations != null)
                {
                    var stations = new List<World.Stations.Saved>();
                    foreach (var s in file.Stations)
                        if (Enum.TryParse(s.Kind, out World.StationKind kind)) stations.Add(new World.Stations.Saved { Scene = s.Scene, Kind = kind, Local = new Vector2(s.X, s.Y), Right = s.Right });
                    World.Stations.Load(stations);
                }
                {
                    // A save from before the crystals gets its own set now.
                    var crystals = new List<World.LifeCrystals.Saved>();
                    if (file.Crystals != null)
                        foreach (var c in file.Crystals) crystals.Add(new World.LifeCrystals.Saved { Scene = c.Scene, Placed = c.Placed, Taken = c.Taken, Local = new Vector2(c.X, c.Y) });
                    World.LifeCrystals.Load(file.CrystalSeed, file.Crystals != null ? crystals : null);
                }
                if (file.Chests != null)
                {
                    var chests = new List<World.Chests.Saved>();
                    foreach (var c in file.Chests)
                    {
                        var items = new ItemStack[World.Chests.Capacity];
                        if (c.Items != null) Load(c.Items, items);
                        // Legacy saves have no collection history: seed stored items once.
                        if (file.CollectedItems == null) foreach (var item in items) if (!item.IsEmpty) inv.RecordCollection(item.Type);
                        chests.Add(new World.Chests.Saved { Scene = c.Scene, Local = new Vector2(c.X, c.Y), Items = items, Name = c.Name, Style = c.Style });
                    }
                    World.Chests.Load(chests);
                }
                {
                    // The areas' chests: a save from before them has none.
                    List<World.RegionalChests.Saved> plan = null;
                    if (file.WorldChests != null)
                    {
                        plan = new List<World.RegionalChests.Saved>();
                        foreach (var c in file.WorldChests) plan.Add(new World.RegionalChests.Saved { Area = c.Area, Scene = c.Scene, Part = c.Part, Placed = c.Placed });
                    }
                    World.RegionalChests.Load(file.WorldChestSeed, plan);
                }
                if (file.Walls != null)
                    foreach (var scene in file.Walls)
                        foreach (var wall in scene.Value) World.WorldWalls.For(scene.Key)[wall.Key] = wall.Value;
                foreach (var scene in file.Scenes)
                {
                    var edits = World.WorldEdits.For(scene.Key);
                    foreach (long packed in scene.Value) edits[(int)(packed >> 8)] = (ushort)(packed & 0xFF);
                }
                if (file.ScenesHigh != null)
                    foreach (var scene in file.ScenesHigh)
                    {
                        var edits = World.WorldEdits.For(scene.Key);
                        foreach (long packed in scene.Value)
                        {
                            int index = (int)(packed >> 8);
                            if (edits.TryGetValue(index, out ushort low)) edits[index] = (ushort)((low & 0xFF) | (int)(packed & 0xFF) << 8);
                        }
                    }
                World.RegionalTorches.Load(file.TorchStyles);
                if (file.Liquids != null)
                    foreach (var scene in file.Liquids)
                    {
                        var cells = World.WorldLiquids.For(scene.Key);
                        foreach (long v in scene.Value) cells[(int)(v >> 16)] = (ushort)(v & 0xFFFF);
                    }
                BreakableDrops.Load(file.LootedBreakables);
                Plugin.Log.LogInfo($"已读取 mod 存档：{path}（{file.Scenes.Count} 个场景有改动），调用：{Caller()}");
                mod.ApplyCreativeWorld();
            }
            catch (Exception e)
            {
                // Whatever came back so far stays; the file itself is kept aside, as the next save writes over it.
                Plugin.Log.LogError($"mod 存档读到一半出错，没读完的部分按新档处理（原文件另存为 .broken-*）：{e}");
                try { File.Copy(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); }
                catch (Exception) { }
            }
        }

        /// <summary>The game methods that led here, for the log: who saved or loaded.</summary>
        static string Caller()
        {
            var sb = new System.Text.StringBuilder();
            var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
            if (frames == null) return "?";
            int shown = 0;
            foreach (var f in frames)
            {
                var m = f.GetMethod();
                if (m == null || m.DeclaringType == null || m.DeclaringType.Namespace?.StartsWith("TerraKnight") == true || m.DeclaringType.Namespace?.StartsWith("HarmonyLib") == true) continue;
                if (shown++ > 0) sb.Append(" <- ");
                sb.Append(m.DeclaringType.Name).Append('.').Append(m.Name);
                if (shown >= 4) break;
            }
            return sb.ToString();
        }

        static List<SlotItem> PackAll(ItemStack[] from)
        {
            var list = new List<SlotItem>();
            foreach (var s in from) list.Add(Pack(s));
            return list;
        }

        static void Load(List<SlotItem> from, ItemStack[] to)
        {
            if (from == null) return;
            for (int i = 0; i < from.Count && i < to.Length; i++) to[i] = Unpack(from[i]);
        }

        /// <summary>A container's pages from its slots saved one after another.</summary>
        static void LoadPages(List<SlotItem> from, ItemStack[][] to)
        {
            if (from == null) return;
            int per = Inventory.ContainerPageSlots;
            for (int i = 0; i < from.Count && i / per < to.Length; i++) to[i / per][i % per] = Unpack(from[i]);
        }
    }
}
