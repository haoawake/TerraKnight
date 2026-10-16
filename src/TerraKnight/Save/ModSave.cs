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
            public int Version = 7;
            /// <summary>A creative-mode save (UI.CreativeMenu): items never run out, anything can be made.</summary>
            public bool Creative;
            /// <summary>World.DifficultyLevel (1 Normal to 4 Master); 0, from before the choice, is Master.</summary>
            public int Difficulty;
            public List<SlotItem> Inventory = new List<SlotItem>();
            /// <summary>The worn set (the current loadout's).</summary>
            public List<SlotItem> Accessories = new List<SlotItem>();
            public List<SlotItem> Armor = new List<SlotItem>();
            /// <summary>The misc equipment: pet, light pet, minecart, mount, hook.</summary>
            public List<SlotItem> Misc;
            /// <summary>The Piggy Bank's slots; GeoItems: the save's geo is the Geo item already (Items.GeoWallet).</summary>
            public List<SlotItem> Bank;
            /// <summary>The Hallownest bag (Items.HkItems).</summary>
            public List<SlotItem> HkBag;
            public bool GeoItems;
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
            public Dictionary<string, int> Kills;
            public List<string> KillOrder;
            /// <summary>The monster book's sightings: records in the order first seen, and the stats seen of each.</summary>
            public List<string> SeenOrder;
            public Dictionary<string, Bestiary.Seen> Seen;
            public Dictionary<string, string> NpcNames;
            public List<string> NpcArrived;
            /// <summary>The Angler: woken at the Blue Lake, today's fish and its day, handed in today, quests done.</summary>
            public bool AnglerSaved;
            public string AnglerQuest;
            public int AnglerDay = -1;
            public bool AnglerDone;
            public int AnglerQuests;
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
            /// <summary>Scene name -> packed cell edits (index &lt;&lt; 8 | value, see <see cref="World.WorldEdits"/>).</summary>
            public Dictionary<string, Dictionary<int, int>> Walls = new Dictionary<string, Dictionary<int, int>>();
            public Dictionary<string, List<long>> Scenes = new Dictionary<string, List<long>>();
        }

        sealed class PylonSave
        {
            public string Scene, Bench;
            public float X, Y, W, H;
            public int Style;
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
            public List<SlotItem> Items = new List<SlotItem>();
        }

        sealed class StationSave
        {
            public string Scene, Kind;
            public float X, Y;
        }

        sealed class CrystalSave
        {
            public string Scene;
            public bool Placed, Taken;
            public float X, Y;
        }

        sealed class LoadoutSave
        {
            public List<SlotItem> Armor = new List<SlotItem>();
            public List<SlotItem> Accessories = new List<SlotItem>();
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
        }

        static SlotItem Pack(ItemStack s) => new SlotItem { Type = s.Type.ToString(), Count = s.Count, Fav = s.Favorite, Prefix = s.Prefix };

        static ItemStack Unpack(SlotItem s)
        {
            if (s == null || !Enum.TryParse(s.Type, out ItemType type)) return default;
            var stack = ItemStack.Of(type, s.Count);
            stack.Favorite = s.Fav;
            // A prefix the item can't have (or one past the list) is dropped.
            if (s.Prefix > 0 && Items.Prefixes.Valid(Items.ItemDb.Get(type), s.Prefix)) stack.Prefix = (byte)s.Prefix;
            return stack;
        }

        /// <summary>Whether a slot's save is a creative one.</summary>
        public static bool IsCreative(int slot) => ModeOf(slot).Creative;

        /// <summary>
        /// A slot's mode: creative or not, and its difficulty (Known: the slot has a mod save at
        /// all). Read from its file's text, kept while the file is unchanged.
        /// </summary>
        public static (bool Known, bool Creative, World.DifficultyLevel Difficulty) ModeOf(int slot)
        {
            string path = PathFor(slot);
            if (slot < 1 || !File.Exists(path)) return (false, false, World.DifficultyLevel.Master);
            var stamp = File.GetLastWriteTimeUtc(path);
            if (modeCache.TryGetValue(slot, out var c) && c.Stamp == stamp) return c.Mode;
            var mode = (true, false, World.DifficultyLevel.Master);
            try
            {
                string text = File.ReadAllText(path);
                var m = System.Text.RegularExpressions.Regex.Match(text, "\"Difficulty\":(\\d+)");
                mode = (true, text.Contains("\"Creative\":true"), World.GameDifficulty.Parse(m.Success ? int.Parse(m.Groups[1].Value) : 0));
            }
            catch (Exception) { }
            modeCache[slot] = (stamp, mode);
            return mode;
        }

        static readonly Dictionary<int, (DateTime Stamp, (bool, bool, World.DifficultyLevel) Mode)> modeCache = new Dictionary<int, (DateTime, (bool, bool, World.DifficultyLevel))>();

        /// <summary>The mod's save of a slot gone (the slot is being started over).</summary>
        public static void Delete(int slot)
        {
            string path = PathFor(slot);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                modeCache.Remove(slot);
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

        public static void Write(int slot, TerraMod mod)
        {
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
            var file = new SaveFile { Creative = mod.Creative, Difficulty = (int)World.GameDifficulty.Current, Selected = inv.Selected, LifeMax = mod.Stats.LifeMax, ManaMax = mod.Stats.ManaMax, LifeCrystals = mod.Stats.LifeCrystals, ManaCrystals = mod.Stats.ManaCrystals };
            foreach (var s in inv.Slots) file.Inventory.Add(Pack(s));
            foreach (var s in inv.Accessories) file.Accessories.Add(Pack(s));
            foreach (var s in inv.Armor) file.Armor.Add(Pack(s));
            file.Misc = new List<SlotItem>();
            foreach (var s in inv.Misc) file.Misc.Add(Pack(s));
            file.Bank = new List<SlotItem>();
            foreach (var s in inv.Bank) file.Bank.Add(Pack(s));
            file.HkBag = new List<SlotItem>();
            foreach (var s in inv.HkBag) file.HkBag.Add(Pack(s));
            file.GeoItems = GeoWallet.Migrated;
            file.CurrentLoadout = inv.CurrentLoadout;
            file.Loadouts = new List<LoadoutSave>();
            foreach (var l in inv.Loadouts)
            {
                var ls = new LoadoutSave();
                foreach (var s in l.Armor) ls.Armor.Add(Pack(s));
                foreach (var s in l.Accessories) ls.Accessories.Add(Pack(s));
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
            file.Kills = new Dictionary<string, int>(Bestiary.Kills);
            file.KillOrder = new List<string>(Bestiary.Order);
            file.SeenOrder = new List<string>(Bestiary.SeenOrder);
            file.Seen = new Dictionary<string, Bestiary.Seen>(Bestiary.Stats);
            file.NpcNames = new Dictionary<string, string>(Town.TownNpcs.Names);
            file.NpcArrived = new List<string>(Town.TownNpcs.Arrived);
            file.AnglerSaved = Town.Angler.Saved;
            file.AnglerQuest = Town.Angler.Quest == ItemType.None ? null : Town.Angler.Quest.ToString();
            file.AnglerDay = Town.Angler.QuestDay;
            file.AnglerDone = Town.Angler.QuestDone;
            file.AnglerQuests = Town.Angler.QuestsDone;
            file.Trash = Pack(inv.Trash);
            file.Granted.AddRange(mod.GrantedAbilities);
            // Creative's pylons not yet placed by their bench are made again when the save is read.
            foreach (var p in World.Pylons.Unlocked)
                if (!p.Provisional) file.Pylons.Add(new PylonSave { Scene = p.Scene, Bench = p.Bench, X = p.Local.x, Y = p.Local.y, W = p.SceneSize.x, H = p.SceneSize.y, Style = p.Style });
            file.LifeFruits = mod.Stats.LifeFruits;
            file.Boosts = new List<string>();
            foreach (var b in mod.Stats.Boosts) file.Boosts.Add(b.ToString());
            file.Stations = new List<StationSave>();
            foreach (var s in Net.WorldSync.SaveStations()) file.Stations.Add(new StationSave { Scene = s.Scene, Kind = s.Kind.ToString(), X = s.Local.x, Y = s.Local.y });
            file.Chests = new List<ChestSave>();
            foreach (var c in Net.WorldSync.SaveChests())
            {
                var cs = new ChestSave { Scene = c.Scene, X = c.Local.x, Y = c.Local.y, Name = c.Name };
                foreach (var it in c.Items) cs.Items.Add(Pack(it));
                file.Chests.Add(cs);
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
                foreach (var cell in kv.Value) cells.Add(((long)cell.Key << 8) | cell.Value);
                file.Scenes[kv.Key] = cells;
            }
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

        public static void Read(int slot, TerraMod mod)
        {
            World.WorldEdits.Clear();
            World.WorldWalls.Clear();
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
                if (any) Plugin.Log.LogError($"[存档] 存档 {slot} 的 mod 存档全都读不了，按新档开始（原文件留在 .broken-* 里）");
                mod.ApplyCreative(false);
                World.GameDifficulty.Current = World.DifficultyLevel.Master;
                return;
            }
            try
            {
                var inv = mod.Inventory;
                inv.Clear();
                mod.ApplyCreative(file.Creative);
                World.GameDifficulty.Current = World.GameDifficulty.Parse(file.Difficulty);
                // The boosters first: the Demon Heart's slot decides what is worn.
                mod.Stats.LifeFruits = Mathf.Clamp(file.LifeFruits, 0, Player.TerraStats.MaxLifeFruits);
                if (file.Boosts != null)
                    foreach (var b in file.Boosts)
                        if (Enum.TryParse(b, out PermanentBoost boost) && boost != PermanentBoost.None && boost != PermanentBoost.LifeFruit) mod.Stats.Boosts.Add(boost);
                Load(file.Inventory.Count > 0 ? file.Inventory : file.Hotbar, inv.Slots);
                Load(file.Accessories, inv.Accessories);
                Load(file.Armor, inv.Armor);
                Load(file.Misc, inv.Misc);
                Load(file.Bank, inv.Bank);
                if (file.HkBag != null) Load(file.HkBag, inv.HkBag);
                GeoWallet.Reset(file.GeoItems);
                if (file.Version >= 4 && file.Loadouts != null)
                {
                    bool bags = false;
                    for (int l = 0; l < file.Loadouts.Count && l < Inventory.LoadoutCount; l++)
                    {
                        Load(file.Loadouts[l].Armor, inv.Loadouts[l].Armor);
                        Load(file.Loadouts[l].Accessories, inv.Loadouts[l].Accessories);
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
                if (file.Kills != null) foreach (var kv in file.Kills) Bestiary.Kills[kv.Key] = kv.Value;
                if (file.KillOrder != null) foreach (var r in file.KillOrder) if (Bestiary.Kills.ContainsKey(r) && !Bestiary.Order.Contains(r)) Bestiary.Order.Add(r);
                if (file.Seen != null) foreach (var kv in file.Seen) if (kv.Value != null) Bestiary.Stats[kv.Key] = kv.Value;
                if (file.SeenOrder != null) foreach (var r in file.SeenOrder) if (!Bestiary.SeenOrder.Contains(r)) Bestiary.SeenOrder.Add(r);
                // Kills from before the book kept sightings count as seen (their stats from the roster until seen again).
                foreach (var r in Bestiary.Order) if (!Bestiary.SeenOrder.Contains(r)) Bestiary.SeenOrder.Add(r);
                if (file.NpcNames != null) foreach (var kv in file.NpcNames) Town.TownNpcs.Names[kv.Key] = kv.Value;
                if (file.NpcArrived != null) Town.TownNpcs.Arrived.UnionWith(file.NpcArrived);
                Town.Angler.Saved = file.AnglerSaved;
                Town.Angler.Quest = file.AnglerQuest != null && System.Enum.TryParse(file.AnglerQuest, out ItemType aq) ? aq : ItemType.None;
                Town.Angler.QuestDay = file.AnglerDay;
                Town.Angler.QuestDone = file.AnglerDone;
                Town.Angler.QuestsDone = file.AnglerQuests;
                inv.Trash = Unpack(file.Trash);
                inv.Selected = Mathf.Clamp(file.Selected, 0, Inventory.HotbarSize - 1);
                // Older files kept only the maximums; every 20 above the start was a crystal.
                mod.Stats.LifeCrystals = file.LifeCrystals >= 0 ? file.LifeCrystals : Mathf.Clamp((file.LifeMax - 100) / 20, 0, Player.TerraStats.MaxLifeCrystals);
                mod.Stats.ManaCrystals = file.ManaCrystals >= 0 ? file.ManaCrystals : Mathf.Clamp((file.ManaMax - 20) / 20, 0, Player.TerraStats.MaxManaCrystals);
                mod.Stats.UpdateMaximums(PlayerData.instance, 0);
                mod.Stats.FullHeal();
                mod.GrantedAbilities.Clear();
                if (file.Granted != null) mod.GrantedAbilities.UnionWith(file.Granted);
                if (file.Pylons != null)
                    foreach (var p in file.Pylons)
                        World.Pylons.Unlocked.Add(new World.Pylons.Pylon { Scene = p.Scene, Bench = p.Bench, Local = new Vector2(p.X, p.Y), SceneSize = new Vector2(p.W, p.H), Style = p.Style });
                // Items a save from before 2.1.1 kept lying in the world are let go (see Write).
                if (file.Dropped != null && file.Dropped.Count > 0) Plugin.Log.LogInfo($"[存档] 地上的 {file.Dropped.Count} 堆掉落物不再读回（退出游戏就清掉）");
                if (file.Stations != null)
                {
                    var stations = new List<World.Stations.Saved>();
                    foreach (var s in file.Stations)
                        if (Enum.TryParse(s.Kind, out World.StationKind kind)) stations.Add(new World.Stations.Saved { Scene = s.Scene, Kind = kind, Local = new Vector2(s.X, s.Y) });
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
                        chests.Add(new World.Chests.Saved { Scene = c.Scene, Local = new Vector2(c.X, c.Y), Items = items, Name = c.Name });
                    }
                    World.Chests.Load(chests);
                }
                if (file.Walls != null)
                    foreach (var scene in file.Walls)
                        foreach (var wall in scene.Value) World.WorldWalls.For(scene.Key)[wall.Key] = wall.Value;
                foreach (var scene in file.Scenes)
                {
                    var edits = World.WorldEdits.For(scene.Key);
                    foreach (long packed in scene.Value) edits[(int)(packed >> 8)] = (byte)(packed & 0xFF);
                }
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

        static void Load(List<SlotItem> from, ItemStack[] to)
        {
            if (from == null) return;
            for (int i = 0; i < from.Count && i < to.Length; i++) to[i] = Unpack(from[i]);
        }
    }
}
