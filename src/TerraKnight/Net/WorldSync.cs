using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// Shared world authority runs on the host's Unity thread, never on Server's socket thread.
    /// Initial snapshots are installed atomically before world interaction is allowed. Furniture
    /// and loot have stable keys; chest writes require a lease, and pickups are serialized here.
    /// Each record fits in one framed packet; terrain is split into bounded chunks.
    /// </summary>
    internal static class WorldSync
    {
        internal sealed class Stack
        {
            public int Type, Count;
            public bool Favorite;
            /// <summary>Its reforge and its forge level (a reforged or forged item keeps them through a shared chest).</summary>
            public byte Prefix, Forge;
            public static Stack From(ItemStack s) => new Stack { Type = (int)s.Type, Count = s.Count, Favorite = s.Favorite, Prefix = s.Prefix, Forge = s.Forge };
            public ItemStack Item()
            {
                if (!ItemDb.Has((ItemType)Type) || Count <= 0) return default;
                var s = ItemStack.Of((ItemType)Type, Count);
                s.Favorite = Favorite;
                s.Prefix = Prefix;
                s.Forge = Forge;
                return s;
            }
        }

        internal sealed class Record
        {
            public string Key, Scene;
            public float X, Y, Vx, Vy;
            public int Kind, Seed, Age, Delay, Dropper = -1, Owner = -1;
            /// <summary>A dropped item's reforge and forge level.</summary>
            public byte Prefix, Forge;
            public bool Placed, Taken, Sky, Shimmered;
            /// <summary>A chest another player has open (the host's leases): not for others' crafting.</summary>
            public bool Busy;
            public Stack[] Slots;
            public long[] Cells;
            public long[] Walls;
            /// <summary>An "r:" record's regional torch styles (World.RegionalTorches): index &lt;&lt; 8 | style.</summary>
            public long[] Torches;
        }

        internal sealed class Action
        {
            public int Id, Kind, Power, Count;
            /// <summary>A dropped item's reforge and forge level.</summary>
            public byte Prefix, Forge;
            public string Op, Key, Scene;
            public float X, Y, Vx, Vy;
            public bool Placed, Sky;
            public Stack[] Slots;
            public Record[] Motion;
            /// <summary>A quick stack's chests (nearest first), and each stack's own chest among them (a currency's; -1: any that holds it).</summary>
            public string[] Keys;
            public int[] Targets;
            /// <summary>The sender's own: the bag slots a quick stack's stacks came out of, for what comes back.</summary>
            [JsonIgnore] public int[] FromSlots;
            /// <summary>The sender's own: what a crafting from nearby chests made, taken back when the host refuses.</summary>
            [JsonIgnore] public ItemStack Made;
        }

        sealed class Result
        {
            public int Id, Type, Count;
            /// <summary>A claimed item's reforge and forge level.</summary>
            public byte Prefix, Forge;
            public string Op, Key;
            public bool Ok;
            public Stack[] Slots;
        }

        public static bool Ready { get; private set; }
        public static bool Applying { get; private set; }
        public static bool Guest => NetSession.Active && !NetSession.Hosting;
        public static bool CanInteract => !NetSession.Active || Ready;
        public static int DevSnapshots, DevActions, DevRejected, DevPickups;
        static float nextSnapshot, nextRequest, nextChestFlush;
        static int serial;
        static readonly Dictionary<string, string> published = new Dictionary<string, string>();
        static readonly Dictionary<string, Record> incoming = new Dictionary<string, Record>();
        static readonly HashSet<int> waiting = new HashSet<int>();
        static readonly Dictionary<(int, int), Result> results = new Dictionary<(int, int), Result>();
        static readonly Queue<(int, int)> resultOrder = new Queue<(int, int)>();
        static readonly Dictionary<int, int> highestRequest = new Dictionary<int, int>();
        static readonly Dictionary<int, Action> pending = new Dictionary<int, Action>();
        static readonly Dictionary<string, int> leases = new Dictionary<string, int>();
        static readonly Dictionary<string, float> throttled = new Dictionary<string, float>();
        static List<Record> personal;
        static bool receiving, leasePending;
        static string ownLease;
        static string lastChest;

        public static string Key(string kind, string scene, Vector2 local) => kind + ":" + scene + ":"
            + local.x.ToString("R", CultureInfo.InvariantCulture) + ":" + local.y.ToString("R", CultureInfo.InvariantCulture);
        public static bool OwnsChest(string scene, Vector2 local) => !NetSession.Active || ownLease == Key("c", scene, local);
        public static bool ChestBusy(string scene, Vector2 local) => NetSession.Active && leases.ContainsKey(Key("c", scene, local));

        static List<Record> Capture()
        {
            var list = new List<Record>();
            list.AddRange(Furniture());
            list.AddRange(WorldItems.NetRecords());
            list.AddRange(Layers(null));
            return list;
        }

        static IEnumerable<Record> Furniture()
        {
            foreach (var r in Stations.NetRecords()) yield return r;
            foreach (var r in Chests.NetRecords()) yield return r;
            foreach (var r in LifeCrystals.NetRecords()) yield return r;
        }

        /// <summary>The terrain, walls and torch styles changed in a scene (all scenes: null), a record a chunk of 512 cells.</summary>
        static List<Record> Layers(string only)
        {
            var list = new List<Record>();
            foreach (var scene in WorldEdits.All)
            {
                if (only != null && scene.Key != only) continue;
                var chunks = new Dictionary<int, List<long>>();
                foreach (var cell in scene.Value)
                {
                    int chunk = cell.Key / 512;
                    if (!chunks.TryGetValue(chunk, out var cells)) chunks[chunk] = cells = new List<long>();
                    cells.Add(((long)cell.Key << 16) | cell.Value);
                }
                foreach (var chunk in chunks)
                {
                    chunk.Value.Sort();
                    list.Add(new Record { Key = "t:" + scene.Key + ":" + chunk.Key, Scene = scene.Key, Cells = chunk.Value.ToArray() });
                }
            }
            foreach (var scene in WorldWalls.All)
            {
                if (only != null && scene.Key != only) continue;
                var chunks = new Dictionary<int, List<long>>();
                foreach (var wall in scene.Value)
                {
                    int chunk = wall.Key / 512;
                    if (!chunks.TryGetValue(chunk, out var cells)) chunks[chunk] = cells = new List<long>();
                    cells.Add(((long)wall.Key << 16) | (ushort)(wall.Value + 1));
                }
                foreach (var chunk in chunks)
                {
                    chunk.Value.Sort();
                    list.Add(new Record { Key = "w:" + scene.Key + ":" + chunk.Key, Scene = scene.Key, Walls = chunk.Value.ToArray() });
                }
            }
            foreach (var (key, scene, torches) in RegionalTorches.NetRecords())
                if (only == null || scene == only) list.Add(new Record { Key = key, Scene = scene, Torches = torches });
            return list;
        }

        // ------------------------------------------------------------------ the host's changes, cheaply

        /// <summary>Each published record's signature (Sig): a record whose signature is unchanged isn't written out again.</summary>
        static readonly Dictionary<string, ulong> signed = new Dictionary<string, ulong>();
        /// <summary>The published keys by scope ("i:", "c:"..., and "t:scene", "w:scene", "r:scene" for the layers).</summary>
        static readonly Dictionary<string, HashSet<string>> scoped = new Dictionary<string, HashSet<string>>();
        /// <summary>Scenes whose terrain or walls changed since they were last looked at (TerrainRegion's edits).</summary>
        static readonly HashSet<string> dirtyScenes = new HashSet<string>();
        static bool furnitureDirty = true, editsHooked;
        static float nextFurniture;
        static int layerCursor;
        public static int DevSigned, DevWritten;

        static string ScopeOf(string key)
        {
            if (key.Length > 2 && (key[0] == 't' || key[0] == 'w' || key[0] == 'r') && key[1] == ':')
            {
                int cut = key.LastIndexOf(':');
                return cut > 2 ? key.Substring(0, cut) : key.Substring(0, 2);
            }
            return key.Length >= 2 ? key.Substring(0, 2) : key;
        }

        static void HookEdits()
        {
            if (editsHooked) return;
            editsHooked = true;
            TerrainRegion.Edited += (region, x, y) => { if (NetSession.Hosting && region != null && region.SceneName != null) dirtyScenes.Add(region.SceneName); };
            TerrainRegion.WallEdited += (region, x, y) => { if (NetSession.Hosting && region != null && region.SceneName != null) dirtyScenes.Add(region.SceneName); };
        }

        /// <summary>
        /// A record's signature: its fields hashed (a dropped item's place to a 64th of a unit, and its age left out, as it
        /// counts up every tick), so the host writes a record out as JSON only when something the others see of it changed.
        /// </summary>
        static ulong Sig(Record r)
        {
            ulong h = 14695981039346656037UL;
            void Mix(long v) { h ^= (ulong)v; h *= 1099511628211UL; }
            void Str(string t) { if (t == null) { Mix(-1); return; } foreach (char c in t) Mix(c); Mix(t.Length); }
            Str(r.Key);
            Str(r.Scene);
            bool item = r.Key.StartsWith("i:", StringComparison.Ordinal);
            Mix(Mathf.RoundToInt(r.X * 64f)); Mix(Mathf.RoundToInt(r.Y * 64f));
            Mix(Mathf.RoundToInt(r.Vx * 16f)); Mix(Mathf.RoundToInt(r.Vy * 16f));
            Mix(r.Kind); Mix(r.Seed); Mix(r.Dropper); Mix(r.Owner); Mix(r.Prefix); Mix(r.Forge);
            Mix(item ? (r.Delay > 0 ? 1 : 0) : r.Delay);
            if (!item) Mix(r.Age);
            Mix((r.Placed ? 1 : 0) | (r.Taken ? 2 : 0) | (r.Sky ? 4 : 0) | (r.Shimmered ? 8 : 0));
            if (r.Slots != null) foreach (var st in r.Slots) { if (st == null) Mix(-2); else { Mix(st.Type); Mix(st.Count); Mix(st.Favorite ? 1 : 0); Mix(st.Prefix); Mix(st.Forge); } }
            if (r.Cells != null) { Mix(r.Cells.Length); foreach (long c in r.Cells) Mix(c); }
            if (r.Walls != null) { Mix(r.Walls.Length); foreach (long c in r.Walls) Mix(c); }
            if (r.Torches != null) { Mix(r.Torches.Length); foreach (long c in r.Torches) Mix(c); }
            return h;
        }

        /// <summary>One scope's records now: the changed ones to everyone, the gone ones taken away.</summary>
        static void Publish(string scopePrefix, bool exact, IEnumerable<Record> records)
        {
            var now = new HashSet<string>();
            foreach (var r in records)
            {
                if (r?.Key == null) continue;
                now.Add(r.Key);
                ulong sig = Sig(r);
                DevSigned++;
                if (signed.TryGetValue(r.Key, out ulong was) && was == sig && published.ContainsKey(r.Key)) continue;
                signed[r.Key] = sig;
                string json = JsonConvert.SerializeObject(r);
                DevWritten++;
                if (published.TryGetValue(r.Key, out var before) && before == json) continue;
                published[r.Key] = json;
                string scope = ScopeOf(r.Key);
                if (!scoped.TryGetValue(scope, out var keys)) scoped[scope] = keys = new HashSet<string>();
                keys.Add(r.Key);
                SendState(255, 1, r.Key, json);
            }
            // What this scope held and holds no more.
            var gone = new List<string>();
            foreach (var kv in scoped)
            {
                if (exact ? kv.Key != scopePrefix : !kv.Key.StartsWith(scopePrefix, StringComparison.Ordinal)) continue;
                foreach (var key in kv.Value) if (!now.Contains(key)) gone.Add(key);
            }
            foreach (var key in gone)
            {
                published.Remove(key);
                signed.Remove(key);
                if (scoped.TryGetValue(ScopeOf(key), out var keys)) keys.Remove(key);
                SendState(255, 2, key, "");
            }
        }

        public static void Tick()
        {
            if (!NetSession.Active || TerrainSystem.Current == null) return;
            if (NetSession.Hosting)
            {
                Ready = true;
                HookEdits();
                if (Time.unscaledTime >= nextSnapshot || waiting.Count > 0) HostTick();
            }
            else if (!Ready && Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + 2f;
                NetSession.Send(NetSession.Mine(Msg.WorldRequest).Done());
            }
            if (Ready)
            {
                // The open chest's slots compared (as JSON) ten times a second, not every frame.
                if (Time.unscaledTime >= nextChestFlush) { nextChestFlush = Time.unscaledTime + 0.1f; Chests.NetFlush(); }
                WorldItems.NetFlushMotion();
            }
        }

        /// <summary>
        /// The host's world to the others, ten times a second, by what can have changed: the lying items each time; the
        /// furniture (stations, chests, crystals) after an action on it and every two seconds; the terrain, walls and torch
        /// styles of a scene edited since, and of one more scene each time round (so every scene is looked at again). A record
        /// is written out only when its signature changed. A player joining gets the whole world first.
        /// </summary>
        static void HostTick()
        {
            nextSnapshot = Time.unscaledTime + 0.1f;
            if (waiting.Count > 0)
            {
                // Everything, for the newcomers (and the others brought up to date with it).
                Publish("i:", true, WorldItems.NetRecords());
                Publish("s:", true, Stations.NetRecords());
                Publish("c:", true, Chests.NetRecords());
                Publish("l:", true, LifeCrystals.NetRecords());
                var layers = Layers(null);
                Publish("t:", false, layers.FindAll(r => r.Key.StartsWith("t:", StringComparison.Ordinal)));
                Publish("w:", false, layers.FindAll(r => r.Key.StartsWith("w:", StringComparison.Ordinal)));
                Publish("r:", false, layers.FindAll(r => r.Key.StartsWith("r:", StringComparison.Ordinal)));
                dirtyScenes.Clear();
                furnitureDirty = false;
                nextFurniture = Time.unscaledTime + 2f;
                foreach (int slot in waiting)
                {
                    SendState(slot, 0, "", "");
                    foreach (var kv in published) SendState(slot, 1, kv.Key, kv.Value);
                    SendState(slot, 3, "", "");
                }
                waiting.Clear();
                return;
            }
            Publish("i:", true, WorldItems.NetRecords());
            if (furnitureDirty || Time.unscaledTime >= nextFurniture)
            {
                furnitureDirty = false;
                nextFurniture = Time.unscaledTime + 2f;
                Publish("s:", true, Stations.NetRecords());
                Publish("c:", true, Chests.NetRecords());
                Publish("l:", true, LifeCrystals.NetRecords());
            }
            // One more scene round the edited ones, in turn.
            var scenes = new List<string>();
            foreach (var kv in WorldEdits.All) if (!scenes.Contains(kv.Key)) scenes.Add(kv.Key);
            foreach (var kv in WorldWalls.All) if (!scenes.Contains(kv.Key)) scenes.Add(kv.Key);
            foreach (var key in scoped.Keys)
                if (key.Length > 2 && key[1] == ':' && key[0] != 'i' && key[0] != 's' && key[0] != 'c' && key[0] != 'l' && !scenes.Contains(key.Substring(2))) scenes.Add(key.Substring(2));
            if (scenes.Count > 0) dirtyScenes.Add(scenes[layerCursor++ % scenes.Count]);
            foreach (var scene in new List<string>(dirtyScenes))
            {
                var layers = Layers(scene);
                Publish("t:" + scene, true, layers.FindAll(r => r.Key.StartsWith("t:", StringComparison.Ordinal)));
                Publish("w:" + scene, true, layers.FindAll(r => r.Key.StartsWith("w:", StringComparison.Ordinal)));
                Publish("r:" + scene, true, layers.FindAll(r => r.Key.StartsWith("r:", StringComparison.Ordinal)));
            }
            dirtyScenes.Clear();
        }

        public static void RequestSnapshot(int slot) => waiting.Add(slot);

        static void SendState(int slot, byte op, string key, string json)
        {
            var p = NetSession.Mine(Msg.WorldState);
            p.W.Write((byte)slot);
            p.W.Write(op);
            p.W.Write(key);
            p.W.Write(json);
            NetSession.Send(p.Done());
        }

        public static void ReceiveState(BinaryReader r)
        {
            int target = r.ReadByte();
            byte op = r.ReadByte();
            string key = r.ReadString(), json = r.ReadString();
            if (NetSession.Hosting || (target != 255 && target != NetSession.MySlot)) return;
            if (op == 0) { incoming.Clear(); receiving = true; Ready = false; return; }
            if (op == 3)
            {
                if (!receiving || TerrainSystem.Current == null) return;
                if (personal == null) personal = Capture();
                Install(incoming.Values);
                receiving = false;
                Ready = true;
                DevSnapshots++;
                return;
            }
            if (receiving)
            {
                if (op == 1) incoming[key] = JsonConvert.DeserializeObject<Record>(json);
                else incoming.Remove(key);
                return;
            }
            if (!Ready) return;
            Applying = true;
            try
            {
                if (op == 1) Apply(JsonConvert.DeserializeObject<Record>(json));
                else Remove(key);
            }
            finally { Applying = false; }
        }

        static void Install(IEnumerable<Record> records)
        {
            Applying = true;
            try
            {
                ownLease = null;
                Chests.Clear();
                Stations.Clear();
                LifeCrystals.Clear();
                WorldItems.NetClear();
                foreach (var t in TerrainSystem.All) { t?.Region?.Grid.ResetNetworkEdits(); t?.Region?.Grid.ResetWalls(); t?.Region?.Grid.ResetTorchStyles(); }
                WorldWalls.ClearInPlace();
                WorldEdits.ClearInPlace();
                RegionalTorches.ClearInPlace();
                foreach (var record in records) Apply(record);
            }
            finally { Applying = false; }
        }

        static void Apply(Record record)
        {
            if (record.Key.StartsWith("t:", StringComparison.Ordinal))
            {
                var region = TerrainSystem.Find(record.Scene);
                foreach (long packed in record.Cells)
                {
                    int index = (int)(packed >> 16);
                    ushort value = (ushort)(packed & 0xFFFF);
                    WorldEdits.For(record.Scene)[index] = value;
                    WorldEdits.Unpack(value, out var kind, out bool dug, out int slope);
                    region?.Grid.ApplyNetwork(index, kind, dug, slope);
                }
            }
            else if (record.Key.StartsWith("w:", StringComparison.Ordinal))
            {
                var region = TerrainSystem.Find(record.Scene);
                foreach (long packed in record.Walls)
                {
                    int index = (int)(packed >> 16), wall = (int)(packed & 65535) - 1;
                    WorldWalls.For(record.Scene)[index] = wall;
                    if (region != null) region.Grid.SetWall(index % region.Grid.Width, index / region.Grid.Width, wall);
                }
            }
            else if (record.Key.StartsWith("s:", StringComparison.Ordinal)) Stations.NetApply(record);
            else if (record.Key.StartsWith("c:", StringComparison.Ordinal)) Chests.NetApply(record, ownLease == record.Key);
            else if (record.Key.StartsWith("l:", StringComparison.Ordinal)) LifeCrystals.NetApply(record);
            else if (record.Key.StartsWith("i:", StringComparison.Ordinal)) WorldItems.NetApply(record);
            else if (record.Key.StartsWith("r:", StringComparison.Ordinal)) RegionalTorches.NetApply(record.Key, record.Scene, record.Torches);
        }

        static void Remove(string key)
        {
            RegionalTorches.NetRemove(key);
            Stations.NetRemove(key);
            Chests.NetRemove(key);
            LifeCrystals.NetRemove(key);
            WorldItems.NetRemove(key);
        }

        public static void Submit(Action action)
        {
            if (!CanInteract) return;
            action.Id = ++serial;
            pending[action.Id] = action;
            string json = JsonConvert.SerializeObject(action);
            if (NetSession.Hosting) ReceiveAction(NetSession.MySlot, json);
            else
            {
                var p = NetSession.Mine(Msg.WorldAction);
                p.W.Write(json);
                NetSession.Send(p.Done());
            }
        }

        public static void Suggest(Action action)
        {
            string key = action.Op + action.Key + action.Scene;
            if (throttled.TryGetValue(key, out float at) && Time.unscaledTime < at) return;
            throttled[key] = Time.unscaledTime + 1f;
            Submit(action);
        }

        public static void ReceiveAction(int slot, string json)
        {
            if (!Ready) return;
            var a = JsonConvert.DeserializeObject<Action>(json);
            if (a == null) return;
            if (results.TryGetValue((slot, a.Id), out var cached)) { SendResult(slot, cached); return; }
            var result = new Result { Id = a.Id, Op = a.Op, Key = a.Key };
            if (highestRequest.TryGetValue(slot, out int highest) && a.Id <= highest) { SendResult(slot, result); return; }
            highestRequest[slot] = a.Id;
            DevActions++;
            if (a.Op != "motion" && a.Op != "claim" && a.Op != "drop") furnitureDirty = true;
            // All actions are processed sequentially on the host main thread. A request's result
            // is cached before responding, so a repeated request never spends or awards twice.
            switch (a.Op)
            {
                case "station": result.Ok = Stations.NetPlace(a); break;
                case "prefabHouse": result.Ok = PrefabHouses.NetBuild(a); break;
                case "chest": result.Ok = Chests.NetPlace(a); break;
                case "mineStation": result.Ok = Stations.NetMine(a); break;
                case "smashAltar": result.Ok = Stations.NetSmash(a); break;
                case "door": result.Ok = Stations.NetToggleDoor(a); break;
                case "mineChest": result.Ok = Chests.NetMine(a); break;
                case "mineCrystal": result.Ok = LifeCrystals.NetMine(a); break;
                case "locateCrystal": result.Ok = LifeCrystals.NetLocate(a); break;
                case "drop":
                    result.Ok = WorldItems.NetSpawn(a, slot);
                    break;
                case "motion":
                    result.Ok = WorldItems.NetMotion(a.Motion, slot);
                    break;
                case "shimmer":
                    result.Count = WorldItems.NetShimmer(a.Key, slot);
                    result.Ok = result.Count > 0;
                    break;
                case "claim":
                    WorldItems.NetClaim(a, slot, out result.Type, out result.Count, out result.Prefix, out result.Forge);
                    result.Ok = result.Count > 0;
                    break;
                case "stack":
                    result.Slots = Chests.NetQuickStack(a, slot, key => leases.ContainsKey(key), out result.Count);
                    result.Ok = result.Slots != null;
                    break;
                case "craftTake":
                    // The host's own crafting took from its chests already.
                    result.Ok = slot == NetSession.MySlot || Chests.NetCraftTake(a, slot, key => leases.TryGetValue(key, out int holder) && holder != slot);
                    break;
                case "lease":
                    if (!leases.ContainsKey(a.Key) || leases[a.Key] == slot)
                    {
                        result.Slots = Chests.NetContents(a.Key);
                        if (result.Slots != null) { leases[a.Key] = slot; result.Ok = true; }
                    }
                    break;
                case "contents":
                case "release":
                    if (leases.TryGetValue(a.Key, out int holder) && holder == slot)
                    {
                        result.Ok = Chests.NetContents(a.Key, a.Slots);
                        if (a.Op == "release") leases.Remove(a.Key);
                    }
                    break;
            }
            if (!result.Ok) DevRejected++;
            results[(slot, a.Id)] = result;
            resultOrder.Enqueue((slot, a.Id));
            while (resultOrder.Count > 4096) results.Remove(resultOrder.Dequeue());
            SendResult(slot, result);
            nextSnapshot = 0f;
        }

        static void SendResult(int slot, Result result)
        {
            if (slot == NetSession.MySlot) { Complete(result); return; }
            var p = NetSession.Mine(Msg.WorldResult);
            p.W.Write((byte)slot);
            p.W.Write(JsonConvert.SerializeObject(result));
            NetSession.Send(p.Done());
        }

        public static void ReceiveResult(BinaryReader r)
        {
            int target = r.ReadByte();
            var result = JsonConvert.DeserializeObject<Result>(r.ReadString());
            if (target == NetSession.MySlot) Complete(result);
        }

        static void Complete(Result result)
        {
            if (result == null || !pending.TryGetValue(result.Id, out var action)) return;
            pending.Remove(result.Id);
            if (result.Op == "lease")
            {
                leasePending = false;
                if (result.Ok)
                {
                    ownLease = result.Key;
                    lastChest = null;
                    Chests.NetOpen(result.Key, result.Slots);
                }
                else Notice("箱子正在被其他玩家使用");
            }
            else if (result.Op == "claim")
            {
                WorldItems.NetClaimDone(result.Key);
                if (result.Ok)
                {
                    var got = ItemStack.Of((ItemType)result.Type, result.Count);
                    got.Prefix = result.Prefix;
                    got.Forge = result.Forge;
                    var hero = HeroController.SilentInstance;
                    Vector2 at = hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : Vector2.zero;
                    // A heart or a star heals or gives mana as it is taken, never into the bag.
                    if (WorldItems.TakeAtOnce(got, at)) { }
                    else if (TerraMod.Instance == null || !Containers.AutoSell(TerraMod.Instance.Inventory, got, at)) Give(got);
                    DevPickups++;
                }
            }
            else if (result.Op == "shimmer" && result.Ok) Achievements.AchievementTracker.Shimmered(result.Count);
            else if (result.Op == "stack") Restack(action, result);
            else if (result.Op == "craftTake" && !result.Ok) Uncraft(action);
            else if(result.Op=="prefabHouse"&&!result.Ok&&action.Count>0)
            {
                Give((ItemType)action.Kind,1);
                Notice("房屋位置已被占用，预制房已退回");
            }
            else if (!result.Ok && action.Count > 0 && (result.Op == "station" || result.Op == "chest"))
            {
                // The caller consumed one item on submitting placement; rejection refunds it.
                Give(result.Op == "chest" ? RegionalChests.ItemOf(action.Kind) : Stations.ItemOf((StationKind)action.Kind), 1);
                Notice("放置位置已被占用，物品已退回");
            }
        }

        public static int DevCraftTakes, DevUncrafted;

        /// <summary>A crafting from nearby chests: the host to take the same ingredients from its chests (Chests.NetCraftTook).</summary>
        public static void CraftTook(string[] keys, Stack[] slots, int[] targets, ItemStack made)
        {
            if (!CanInteract) return;
            DevCraftTakes++;
            Submit(new Action { Op = "craftTake", Keys = keys, Slots = slots, Targets = targets, Made = made });
        }

        /// <summary>The host couldn't take the ingredients: what was made goes back (off the cursor, else out of the bag).</summary>
        static void Uncraft(Action action)
        {
            var mod = TerraMod.Instance;
            var made = action.Made;
            if (mod == null || made.IsEmpty) return;
            var inv = mod.Inventory;
            int left = made.Count;
            if (!inv.Mouse.IsEmpty && inv.Mouse.Type == made.Type)
            {
                int take = Mathf.Min(left, inv.Mouse.Count);
                inv.Mouse.Count -= take;
                left -= take;
                if (inv.Mouse.Count <= 0) inv.Mouse = default;
            }
            for (int i = inv.Slots.Length - 1; i >= 0 && left > 0; i--)
            {
                if (inv.Slots[i].IsEmpty || inv.Slots[i].Type != made.Type) continue;
                int take = Mathf.Min(left, inv.Slots[i].Count);
                inv.Slots[i].Count -= take;
                left -= take;
                if (inv.Slots[i].Count <= 0) inv.Slots[i] = default;
            }
            DevUncrafted++;
            Notice("附近箱子里的材料已被其他玩家取走或正在使用，这次制作撤回了");
        }

        public static int DevStacked, DevStackReturned;

        /// <summary>
        /// Quick Stack to Nearby Chests onto the host's chests (Chests.QuickStackNearby): the stacks out of their bag slots
        /// now, sent with the chests' keys; what the host had no room for comes back (Restack). How many stacks went.
        /// </summary>
        public static int QuickStack(Inventory inv, string[] keys, List<(int Slot, ItemStack Stack, int Target)> stacks)
        {
            if (!CanInteract || stacks.Count == 0 || keys.Length == 0) return 0;
            var a = new Action { Op = "stack", Keys = keys, Slots = new Stack[stacks.Count], Targets = new int[stacks.Count], FromSlots = new int[stacks.Count] };
            for (int i = 0; i < stacks.Count; i++)
            {
                a.Slots[i] = Stack.From(stacks[i].Stack);
                a.Targets[i] = stacks[i].Target;
                a.FromSlots[i] = stacks[i].Slot;
                inv.Slots[stacks[i].Slot] = default;
            }
            Submit(a);
            return stacks.Count;
        }

        /// <summary>A quick stack's answer: what is left of each stack back into its slot (else the bag, else the floor); all of it with no answer.</summary>
        static void Restack(Action action, Result result) =>
            HallownestStorage.Move(TerraMod.Instance?.Inventory, () => RestackCore(action, result));

        static void RestackCore(Action action, Result result)
        {
            var mod = TerraMod.Instance;
            if (mod == null || action.Slots == null) return;
            var inv = mod.Inventory;
            for (int i = 0; i < action.Slots.Length; i++)
            {
                var back = result != null && result.Ok && result.Slots != null && i < result.Slots.Length ? result.Slots[i]?.Item() ?? default : action.Slots[i]?.Item() ?? default;
                var sent = action.Slots[i]?.Item() ?? default;
                if (result != null && result.Ok && sent.Count > back.Count) DevStacked++;
                if (back.IsEmpty) continue;
                DevStackReturned++;
                int at = action.FromSlots != null && i < action.FromSlots.Length ? action.FromSlots[i] : -1;
                if (at >= 0 && at < inv.Slots.Length && inv.Slots[at].IsEmpty) { inv.Slots[at] = back; continue; }
                int left = inv.Pickup(back);
                var hero = HeroController.SilentInstance;
                if (left > 0 && hero != null)
                {
                    back.Count = left;
                    WorldItems.Drop(back, hero.transform.position);
                }
            }
        }

        static void Give(ItemType type, int count) => Give(ItemStack.Of(type, count));

        /// <summary>A stack into the bag (its prefix and forge level kept); what doesn't fit falls at the player's feet.</summary>
        static void Give(ItemStack stack)
        {
            if (stack.Count <= 0 || !ItemDb.Has(stack.Type) || TerraMod.Instance == null) return;
            int count = stack.Count;
            int left = TerraMod.Instance.Inventory.Pickup(stack);
            var hero = HeroController.SilentInstance;
            if (hero != null)
            {
                if (left < count) UI.PopupText.Show(stack.Def, count - left, hero.transform.position);
                if (left > 0)
                {
                    var rest = stack;
                    rest.Count = left;
                    WorldItems.Drop(rest, hero.transform.position);
                }
            }
        }

        static void Notice(string line)
        {
            var hero = HeroController.SilentInstance;
            if (hero != null) UI.CombatText.Show(hero.transform.position + Vector3.up, Lang.T(line), UI.CombatText.Notice);
        }

        public static void OpenChest(string scene, Vector2 local)
        {
            if (leasePending) return;
            if (ownLease != null) Chests.Close(false);
            leasePending = true;
            Submit(new Action { Op = "lease", Key = Key("c", scene, local) });
        }

        public static void FlushChest(string key, Stack[] slots, bool release)
        {
            if (key != ownLease || Applying) return;
            string json = JsonConvert.SerializeObject(slots);
            if (!release && json == lastChest) return;
            lastChest = json;
            Submit(new Action { Op = release ? "release" : "contents", Key = key, Slots = slots });
            if (release) { ownLease = null; lastChest = null; }
        }

        public static void PlayerLeft(int slot)
        {
            var remove = new List<string>();
            foreach (var kv in leases) if (kv.Value == slot) remove.Add(kv.Key);
            foreach (string key in remove) leases.Remove(key);
            var old = new List<(int, int)>();
            foreach (var key in results.Keys) if (key.Item1 == slot) old.Add(key);
            foreach (var key in old) results.Remove(key);
            waiting.Remove(slot);
            highestRequest.Remove(slot);
        }

        public static void Reset()
        {
            bool wasGuestWorld = personal != null;
            // A quick stack the host never answered: the stacks back to the player.
            foreach (var a in pending.Values) if (a.Op == "stack") Restack(a, null);
            if (personal != null && TerrainSystem.Current != null) Install(personal);
            personal = null;
            Ready = receiving = leasePending = false;
            ownLease = lastChest = null;
            incoming.Clear(); published.Clear(); waiting.Clear(); results.Clear(); pending.Clear(); leases.Clear(); throttled.Clear();
            signed.Clear(); scoped.Clear(); dirtyScenes.Clear(); furnitureDirty = true;
            resultOrder.Clear(); highestRequest.Clear();
            nextSnapshot = nextRequest = 0f;
            if (wasGuestWorld) WorldItems.NetClear();
            else WorldItems.NetDetach();
        }

        // Guest world data never overwrites that character's single-player world on autosave.
        public static List<Stations.Saved> SaveStations()
        {
            if (personal == null) return Stations.Save();
            var list = new List<Stations.Saved>();
            foreach (var r in personal) if (r.Key.StartsWith("s:") && !r.Taken) list.Add(new Stations.Saved { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Kind = (StationKind)r.Kind, Right = r.Placed });
            return list;
        }
        public static List<Chests.Saved> SaveChests()
        {
            if (personal == null) return Chests.Save();
            var list = new List<Chests.Saved>();
            foreach (var r in personal)
                if (r.Key.StartsWith("c:")) list.Add(new Chests.Saved { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Items = Array.ConvertAll(r.Slots, s => s.Item()), Style = r.Kind });
            return list;
        }
        public static List<LifeCrystals.Saved> SaveCrystals()
        {
            if (personal == null) return LifeCrystals.Save();
            var list = new List<LifeCrystals.Saved>();
            foreach (var r in personal) if (r.Key.StartsWith("l:") && r.Key != "l:seed") list.Add(new LifeCrystals.Saved { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Placed = r.Placed, Taken = r.Taken });
            return list;
        }
        public static int SaveSeed => personal == null ? LifeCrystals.Seed : personal.Find(r => r.Key == "l:seed")?.Seed ?? 0;
        public static IEnumerable<KeyValuePair<string, Dictionary<int, int>>> SaveWalls()
        {
            if (personal == null) return WorldWalls.All;
            var scenes = new Dictionary<string, Dictionary<int, int>>();
            foreach (var r in personal)
                if (r.Walls != null)
                {
                    if (!scenes.TryGetValue(r.Scene, out var cells)) scenes[r.Scene] = cells = new Dictionary<int, int>();
                    foreach (long cell in r.Walls) cells[(int)(cell >> 16)] = (int)(cell & 65535) - 1;
                }
            return scenes;
        }
        public static IEnumerable<KeyValuePair<string, Dictionary<int, ushort>>> SaveEdits()
        {
            if (personal == null) return WorldEdits.All;
            var scenes = new Dictionary<string, Dictionary<int, ushort>>();
            foreach (var r in personal)
                if (r.Cells != null)
                {
                    if (!scenes.TryGetValue(r.Scene, out var cells)) scenes[r.Scene] = cells = new Dictionary<int, ushort>();
                    foreach (long cell in r.Cells) cells[(int)(cell >> 16)] = (ushort)cell;
                }
            return scenes;
        }
        public static IEnumerable<KeyValuePair<string, Dictionary<int, byte>>> SaveTorchStyles()
        {
            if (personal == null) return RegionalTorches.AllSaved;
            var scenes = new Dictionary<string, Dictionary<int, byte>>();
            foreach (var r in personal)
                if (r.Torches != null)
                {
                    if (!scenes.TryGetValue(r.Scene, out var cells)) scenes[r.Scene] = cells = new Dictionary<int, byte>();
                    foreach (long cell in r.Torches) cells[(int)(cell >> 8)] = (byte)cell;
                }
            return scenes;
        }
    }
}
