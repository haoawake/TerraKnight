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
            public static Stack From(ItemStack s) => new Stack { Type = (int)s.Type, Count = s.Count, Favorite = s.Favorite };
            public ItemStack Item()
            {
                if (!ItemDb.Has((ItemType)Type) || Count <= 0) return default;
                var s = ItemStack.Of((ItemType)Type, Count);
                s.Favorite = Favorite;
                return s;
            }
        }

        internal sealed class Record
        {
            public string Key, Scene;
            public float X, Y, Vx, Vy;
            public int Kind, Seed, Age, Delay, Dropper = -1, Owner = -1;
            public bool Placed, Taken, Sky;
            public Stack[] Slots;
            public long[] Cells;
            public long[] Walls;
        }

        internal sealed class Action
        {
            public int Id, Kind, Power, Count;
            public string Op, Key, Scene;
            public float X, Y, Vx, Vy;
            public bool Placed, Sky;
            public Stack[] Slots;
            public Record[] Motion;
        }

        sealed class Result
        {
            public int Id, Type, Count;
            public string Op, Key;
            public bool Ok;
            public Stack[] Slots;
        }

        public static bool Ready { get; private set; }
        public static bool Applying { get; private set; }
        public static bool Guest => NetSession.Active && !NetSession.Hosting;
        public static bool CanInteract => !NetSession.Active || Ready;
        public static int DevSnapshots, DevActions, DevRejected, DevPickups;
        static float nextSnapshot, nextRequest;
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
            list.AddRange(Stations.NetRecords());
            list.AddRange(Chests.NetRecords());
            list.AddRange(LifeCrystals.NetRecords());
            list.AddRange(WorldItems.NetRecords());
            foreach (var scene in WorldEdits.All)
            {
                var chunks = new Dictionary<int, List<long>>();
                foreach (var cell in scene.Value)
                {
                    int chunk = cell.Key / 512;
                    if (!chunks.TryGetValue(chunk, out var cells)) chunks[chunk] = cells = new List<long>();
                    cells.Add(((long)cell.Key << 8) | cell.Value);
                }
                foreach (var chunk in chunks)
                {
                    chunk.Value.Sort();
                    list.Add(new Record { Key = "t:" + scene.Key + ":" + chunk.Key, Scene = scene.Key, Cells = chunk.Value.ToArray() });
                }
            }
            foreach (var scene in WorldWalls.All)
            {
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
            return list;
        }

        public static void Tick()
        {
            if (!NetSession.Active || TerrainSystem.Current == null) return;
            if (NetSession.Hosting)
            {
                Ready = true;
                if (Time.unscaledTime < nextSnapshot && waiting.Count == 0) return;
                nextSnapshot = Time.unscaledTime + 0.1f;
                var current = new Dictionary<string, string>();
                foreach (var record in Capture()) current[record.Key] = JsonConvert.SerializeObject(record);
                foreach (var kv in current)
                    if (!published.TryGetValue(kv.Key, out var before) || before != kv.Value) SendState(255, 1, kv.Key, kv.Value);
                foreach (var key in published.Keys)
                    if (!current.ContainsKey(key)) SendState(255, 2, key, "");
                published.Clear();
                foreach (var kv in current) published[kv.Key] = kv.Value;
                foreach (int slot in waiting)
                {
                    SendState(slot, 0, "", "");
                    foreach (var kv in current) SendState(slot, 1, kv.Key, kv.Value);
                    SendState(slot, 3, "", "");
                }
                waiting.Clear();
            }
            else if (!Ready && Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + 2f;
                NetSession.Send(NetSession.Mine(Msg.WorldRequest).Done());
            }
            if (Ready)
            {
                Chests.NetFlush();
                WorldItems.NetFlushMotion();
            }
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
                foreach (var t in TerrainSystem.All) { t?.Region?.Grid.ResetNetworkEdits(); t?.Region?.Grid.ResetWalls(); }
                WorldWalls.ClearInPlace();
                WorldEdits.ClearInPlace();
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
                    int index = (int)(packed >> 8);
                    byte value = (byte)(packed & 255);
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
        }

        static void Remove(string key)
        {
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
            // All actions are processed sequentially on the host main thread. A request's result
            // is cached before responding, so a repeated request never spends or awards twice.
            switch (a.Op)
            {
                case "station": result.Ok = Stations.NetPlace(a); break;
                case "chest": result.Ok = Chests.NetPlace(a); break;
                case "mineStation": result.Ok = Stations.NetMine(a); break;
                case "mineChest": result.Ok = Chests.NetMine(a); break;
                case "mineCrystal": result.Ok = LifeCrystals.NetMine(a); break;
                case "locateCrystal": result.Ok = LifeCrystals.NetLocate(a); break;
                case "drop":
                    result.Ok = WorldItems.NetSpawn(a, slot);
                    break;
                case "motion":
                    result.Ok = WorldItems.NetMotion(a.Motion, slot);
                    break;
                case "claim":
                    WorldItems.NetClaim(a, slot, out result.Type, out result.Count);
                    result.Ok = result.Count > 0;
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
                if (result.Ok) { Give((ItemType)result.Type, result.Count); DevPickups++; }
            }
            else if (!result.Ok && action.Count > 0 && (result.Op == "station" || result.Op == "chest"))
            {
                // The caller consumed one item on submitting placement; rejection refunds it.
                Give(result.Op == "chest" ? ItemType.Chest : Stations.ItemOf((StationKind)action.Kind), 1);
                Notice("放置位置已被占用，物品已退回");
            }
        }

        static void Give(ItemType type, int count)
        {
            if (count <= 0 || !ItemDb.Has(type) || TerraMod.Instance == null) return;
            int left = TerraMod.Instance.Inventory.Pickup(type, count);
            var hero = HeroController.SilentInstance;
            if (hero != null)
            {
                if (left < count) UI.PopupText.Show(ItemDb.Get(type), count - left, hero.transform.position);
                if (left > 0) WorldItems.Drop(ItemStack.Of(type, left), hero.transform.position);
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
            if (personal != null && TerrainSystem.Current != null) Install(personal);
            personal = null;
            Ready = receiving = leasePending = false;
            ownLease = lastChest = null;
            incoming.Clear(); published.Clear(); waiting.Clear(); results.Clear(); pending.Clear(); leases.Clear(); throttled.Clear();
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
            foreach (var r in personal) if (r.Key.StartsWith("s:")) list.Add(new Stations.Saved { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Kind = (StationKind)r.Kind });
            return list;
        }
        public static List<Chests.Saved> SaveChests()
        {
            if (personal == null) return Chests.Save();
            var list = new List<Chests.Saved>();
            foreach (var r in personal)
                if (r.Key.StartsWith("c:")) list.Add(new Chests.Saved { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Items = Array.ConvertAll(r.Slots, s => s.Item()) });
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
        public static IEnumerable<KeyValuePair<string, Dictionary<int, byte>>> SaveEdits()
        {
            if (personal == null) return WorldEdits.All;
            var scenes = new Dictionary<string, Dictionary<int, byte>>();
            foreach (var r in personal)
                if (r.Cells != null)
                {
                    if (!scenes.TryGetValue(r.Scene, out var cells)) scenes[r.Scene] = cells = new Dictionary<int, byte>();
                    foreach (long cell in r.Cells) cells[(int)(cell >> 8)] = (byte)cell;
                }
            return scenes;
        }
    }
}
