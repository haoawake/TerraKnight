using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.World
{
    internal sealed partial class Stations
    {
        internal static IEnumerable<WorldSync.Record> NetRecords()
        {
            foreach (var s in stations)
                // Placed: which way it faces (Station.Right).
                if (!s.Fixed) yield return new WorldSync.Record { Key = WorldSync.Key("s", s.Scene, s.Local), Scene = s.Scene, X = s.Local.x, Y = s.Local.y, Kind = (int)s.Kind, Placed = s.Right, HomeColor=s.HomeColor,HomeOrder=s.HomeOrder,HomeName=s.HomeName };
            // Dirtmouth's altars knocked off in the host's world: gone in every game.
            foreach (var kind in new[] { StationKind.DemonAltar, StationKind.CrimsonAltar })
                if (FixedStations.Taken(kind)) yield return new WorldSync.Record { Key = "s:altar:" + kind, Kind = (int)kind, Taken = true };
        }

        internal static void NetApply(WorldSync.Record r)
        {
            if (!specs.ContainsKey((StationKind)r.Kind)) return;
            if (r.Taken)
            {
                FixedStations.NetTaken((StationKind)r.Kind);
                for (int i = stations.Count - 1; i >= 0; i--) if (stations[i].Fixed && stations[i].Kind == (StationKind)r.Kind) Remove(i);
                return;
            }
            var at = new Vector2(r.X, r.Y);
            foreach (var s in stations)
                if (!s.Fixed && WorldSync.Key("s", s.Scene, s.Local) == r.Key)
                {
                    if (s.Kind != (StationKind)r.Kind)
                    {
                        if (s.Aura != null) Object.Destroy(s.Aura.sharedMaterial);
                        s.Aura = null;
                        if (s.Renderer != null) Object.Destroy(s.Renderer.gameObject);
                        s.Renderer = null; s.Top = null; s.SupportedIn = null;
                        s.Kind = (StationKind)r.Kind;
                    }
                    s.HomeColor=r.HomeColor;s.HomeOrder=r.HomeOrder;s.HomeName=r.HomeName;
                    s.Right = r.Placed && s.Spec.Faces;
                    return;
                }
            stations.Add(new Station { Scene = r.Scene, Local = at, Kind = (StationKind)r.Kind, Region = RegionNamed(r.Scene), Right = r.Placed && specs[(StationKind)r.Kind].Faces,HomeColor=r.HomeColor,HomeOrder=r.HomeOrder,HomeName=r.HomeName });
        }

        internal static void NetRemove(string key)
        {
            for (int i = stations.Count - 1; i >= 0; i--)
                if (!stations[i].Fixed && WorldSync.Key("s", stations[i].Scene, stations[i].Local) == key) Remove(i);
        }

        internal static bool NetOverlaps(string scene, Rect box)
        {
            foreach (var s in stations)
            {
                if (s.Scene != scene) continue;
                var b = new Rect(s.Local.x - s.Spec.W * 8f * Px, s.Local.y, s.Spec.W * 16f * Px, s.Spec.H * 16f * Px);
                if (b.Overlaps(box)) return true;
            }
            return false;
        }

        internal static bool NetPlace(WorldSync.Action a)
        {
            if((StationKind)a.Kind==StationKind.HomeEverywhere&&FreeHomeColor()<0)return false;
            if (!specs.TryGetValue((StationKind)a.Kind, out var spec) || spec.Item == ItemType.None || string.IsNullOrEmpty(a.Scene)) return false;
            var box = new Rect(a.X - spec.W * 8f * Px, a.Y, spec.W * 16f * Px, spec.H * 16f * Px);
            if (NetOverlaps(a.Scene, box) || Chests.NetOverlaps(a.Scene, box)) return false;
            NetApply(new WorldSync.Record { Key = WorldSync.Key("s", a.Scene, new Vector2(a.X, a.Y)), Scene = a.Scene, X = a.X, Y = a.Y, Kind = a.Kind, Placed = a.Placed, HomeColor=(StationKind)a.Kind==StationKind.HomeEverywhere?FreeHomeColor():-1 });
            RepairHomeColors();
            DevPlaced++;
            return true;
        }

        internal static bool NetMine(WorldSync.Action a)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                var s = stations[i];
                if (s.Fixed || WorldSync.Key("s", s.Scene, s.Local) != a.Key) continue;
                var box = new Rect(s.Local.x - s.Spec.W * 8f * Px, s.Local.y, s.Spec.W * 16f * Px, s.Spec.H * 16f * Px);
                if (Chests.NetRestsOn(s.Scene, box)) return false;
                s.Damage += Mathf.Clamp(a.Power, 0, 1000);
                if (s.Damage < 100) return true;
                WorldItems.NetDrop(ItemStack.Of(s.Spec.Item), s.Scene, s.Local + Vector2.up * (s.Spec.H * 8f * Px));
                if (RegionalFurniture.BaseKind(s.Kind) == StationKind.Bed) Beds.Gone(s.Scene,s.Local);
                Remove(i);
                DevBroken++;
                return true;
            }
            return false;
        }
    }

    internal sealed partial class Chests
    {
        static WorldSync.Stack[] NetSlots(Chest c) => System.Array.ConvertAll(c.Items, WorldSync.Stack.From);
        internal static IEnumerable<WorldSync.Record> NetRecords()
        {
            foreach (var c in chests)
                yield return new WorldSync.Record { Key = WorldSync.Key("c", c.Scene, c.Local), Scene = c.Scene, X = c.Local.x, Y = c.Local.y, Kind = c.Style, Slots = NetSlots(c),
                    Busy = WorldSync.ChestBusy(c.Scene, c.Local) };
        }

        internal static void NetApply(WorldSync.Record r, bool keepContents)
        {
            var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == r.Key);
            if (c == null)
            {
                c = new Chest { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Region = RegionNamed(r.Scene) };
                chests.Add(c);
            }
            c.Style = r.Kind;
            c.NetBusy = r.Busy;
            if (!keepContents) NetSet(c, r.Slots);
        }

        /// <summary>
        /// Crafting took from the chests near the player (Recipes.Consume): in a LAN game the host's chests are the ones
        /// that count. The host's own crafting took from them already (the others see it at the next sync); a guest's took
        /// from its copies, so the host is told and takes the same from its chests, all or nothing, and a refusal (a chest
        /// another player opened meanwhile, its items gone) takes the crafted item back. The chest the player has open goes
        /// with its contents as always.
        /// </summary>
        internal static void NetCraftTook(List<(ItemStack[] Slots, ItemType Type, int Count)> took, ItemStack made)
        {
            if (!NetSession.Active || took == null || took.Count == 0) return;
            var keys = new List<string>();
            var stacks = new List<WorldSync.Stack>();
            var targets = new List<int>();
            foreach (var (slots, type, count) in took)
            {
                var c = chests.Find(ch => ch.Items == slots);
                if (c == null || WorldSync.OwnsChest(c.Scene, c.Local)) continue;
                string key = WorldSync.Key("c", c.Scene, c.Local);
                int k = keys.IndexOf(key);
                if (k < 0) { keys.Add(key); k = keys.Count - 1; }
                stacks.Add(WorldSync.Stack.From(ItemStack.Of(type, count)));
                targets.Add(k);
            }
            if (stacks.Count > 0) WorldSync.CraftTook(keys.ToArray(), stacks.ToArray(), targets.ToArray(), made);
        }

        /// <summary>The host takes a guest's crafting's ingredients out of its chests: every one there, none in other hands, or nothing.</summary>
        internal static bool NetCraftTake(WorldSync.Action a, int slot, System.Func<string, bool> busy)
        {
            if (a.Keys == null || a.Slots == null || a.Targets == null || a.Keys.Length > 64 || a.Slots.Length > 256 || a.Targets.Length != a.Slots.Length) return false;
            var player = NetSession.Player(slot);
            var near = new Chest[a.Keys.Length];
            var uniqueKeys = new HashSet<string>();
            for (int k = 0; k < a.Keys.Length; k++)
            {
                if (!uniqueKeys.Add(a.Keys[k])) return false;
                var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == a.Keys[k]);
                if (c == null || busy(a.Keys[k])) return false;
                if(a.Home!=null) { if(!HomeContainsChest(a.Home,c))return false; }
                else if (player == null || !player.HasState || (c.Scene != player.Latest.Scene && !player.Loaded.Contains(c.Scene) && !player.In.Contains(c.Scene))) return false;
                near[k] = c;
            }
            var need = new Dictionary<(int, ItemType), int>();
            for (int i = 0; i < a.Slots.Length; i++)
            {
                var s = a.Slots[i]?.Item() ?? default;
                int k = a.Targets[i];
                if (s.IsEmpty || k < 0 || k >= near.Length) return false;
                need.TryGetValue((k, s.Type), out int n);
                need[(k, s.Type)] = n + s.Count;
            }
            foreach (var kv in need)
            {
                int have = 0;
                foreach (var it in near[kv.Key.Item1].Items) if (!it.IsEmpty && it.Type == kv.Key.Item2) have += it.Count;
                if (have < kv.Value) return false;
            }
            foreach (var kv in need)
            {
                var items = near[kv.Key.Item1].Items;
                int left = kv.Value;
                for (int i = items.Length - 1; i >= 0 && left > 0; i--)
                {
                    if (items[i].IsEmpty || items[i].Type != kv.Key.Item2) continue;
                    int take = Mathf.Min(left, items[i].Count);
                    items[i].Count -= take;
                    left -= take;
                    if (items[i].Count <= 0) items[i] = default;
                }
            }
            return true;
        }

        static void NetSet(Chest c, WorldSync.Stack[] slots)
        {
            if (slots == null || slots.Length != Capacity) return;
            for (int i = 0; i < Capacity; i++) c.Items[i] = slots[i]?.Item() ?? default;
        }

        internal static void NetRemove(string key)
        {
            for (int i = chests.Count - 1; i >= 0; i--)
                if (WorldSync.Key("c", chests[i].Scene, chests[i].Local) == key) Remove(i);
        }

        internal static WorldSync.Stack[] NetContents(string key)
        {
            var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == key);
            return c == null ? null : NetSlots(c);
        }

        internal static bool NetContents(string key, WorldSync.Stack[] slots)
        {
            var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == key);
            if (c == null || slots == null || slots.Length != Capacity) return false;
            NetSet(c, slots);
            return true;
        }

        /// <summary>
        /// A player's Quick Stack to Nearby Chests into the host's chests (Chest.ServerPlaceItem, PutItemInNearbyChest): each
        /// stack onto the named chests, nearest first, that hold that item; a currency into the one it is meant for (its
        /// target). None that another player has open, none in a scene that player isn't in. What is left of each stack,
        /// and how many moved; null for a request out of bounds.
        /// </summary>
        internal static WorldSync.Stack[] NetQuickStack(WorldSync.Action a, int slot, System.Func<string, bool> busy, out int moved)
        {
            moved = 0;
            if (a.Keys == null || a.Slots == null || a.Keys.Length > 64 || a.Slots.Length > Inventory.Total) return null;
            var player = slot == NetSession.MySlot ? null : NetSession.Player(slot);
            var near = new Chest[a.Keys.Length];
            for (int k = 0; k < a.Keys.Length; k++)
            {
                var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == a.Keys[k]);
                if (c == null || busy(a.Keys[k])) continue;
                // A chest a Home Everywhere holds is reached from anywhere (its quick stack to every home).
                bool homed = Stations.AnyHomeContains(c.Scene, new Rect(c.Local.x - WidthOf(c.Style) / 2, c.Local.y, WidthOf(c.Style), Height));
                if (slot != NetSession.MySlot && !homed && (player == null || !player.HasState
                    || (c.Scene != player.Latest.Scene && !player.Loaded.Contains(c.Scene) && !player.In.Contains(c.Scene)))) continue;
                near[k] = c;
            }
            var left = new WorldSync.Stack[a.Slots.Length];
            for (int i = 0; i < a.Slots.Length; i++)
            {
                var s = a.Slots[i]?.Item() ?? default;
                int target = a.Targets != null && i < a.Targets.Length ? a.Targets[i] : -1;
                if (!s.IsEmpty && target >= 0)
                {
                    if (target < near.Length && near[target] != null && PutIn(near[target], ref s)) moved++;
                }
                else if (!s.IsEmpty)
                    foreach (var c in near)
                    {
                        if (c == null || s.IsEmpty || !Holds(c, s.Type)) continue;
                        int before = s.Count;
                        PutIn(c, ref s);
                        if (s.IsEmpty || s.Count != before) moved++;
                    }
                left[i] = WorldSync.Stack.From(s);
            }
            return left;
        }

        internal static void NetOpen(string key, WorldSync.Stack[] slots)
        {
            var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == key);
            if (c == null) return;
            NetSet(c, slots);
            c.Region = RegionNamed(c.Scene);
            c.Remote=Stations.HomeActive&&HomeContainsChest(Stations.SelectedHome,c);
            Open(c);
        }

        internal static void NetFlush()
        {
            if (open != null && !open.Bank)
                WorldSync.FlushChest(WorldSync.Key("c", open.Scene, open.Local), NetSlots(open), false);
        }

        internal static bool NetOverlaps(string scene, Rect box)
        {
            foreach (var c in chests)
                if (c.Scene == scene && new Rect(c.Local.x - WidthOf(c.Style) / 2f, c.Local.y, WidthOf(c.Style), Height).Overlaps(box)) return true;
            return false;
        }

        internal static bool NetRestsOn(string scene, Rect box)
        {
            foreach (var c in chests)
                if (c.Scene == scene && c.Local.x + WidthOf(c.Style) / 2f > box.xMin + 0.01f && c.Local.x - WidthOf(c.Style) / 2f < box.xMax - 0.01f && Mathf.Abs(c.Local.y - box.yMax) < 0.06f) return true;
            return false;
        }

        internal static bool NetPlace(WorldSync.Action a)
        {
            if (string.IsNullOrEmpty(a.Scene)) return false;
            var box = new Rect(a.X - WidthOf(a.Kind) / 2f, a.Y, WidthOf(a.Kind), Height);
            if (NetOverlaps(a.Scene, box) || Stations.NetOverlaps(a.Scene, box)) return false;
            var at = new Vector2(a.X, a.Y);
            chests.Add(new Chest { Scene = a.Scene, Local = at, Region = RegionNamed(a.Scene), Style = a.Kind });
            DevPlaced++;
            return true;
        }

        internal static bool NetMine(WorldSync.Action a)
        {
            for (int i = 0; i < chests.Count; i++)
            {
                var c = chests[i];
                if (WorldSync.Key("c", c.Scene, c.Local) != a.Key) continue;
                if (!c.Empty || WorldSync.ChestBusy(c.Scene, c.Local)) return false;
                c.Damage += Mathf.Clamp(a.Power, 0, 1000);
                if (c.Damage < 100) return true;
                WorldItems.NetDrop(ItemStack.Of(RegionalChests.ItemOf(c.Style)), c.Scene, c.Local + Vector2.up * Height / 2f);
                Remove(i);
                DevBroken++;
                return true;
            }
            return false;
        }
    }

    internal sealed partial class LifeCrystals
    {
        internal static IEnumerable<WorldSync.Record> NetRecords()
        {
            yield return new WorldSync.Record { Key = "l:seed", Seed = Seed };
            foreach (var c in crystals)
                yield return new WorldSync.Record { Key = "l:" + c.Scene, Scene = c.Scene, Placed = c.Local.HasValue, X = c.Local?.x ?? 0f, Y = c.Local?.y ?? 0f, Taken = c.Taken };
        }

        internal static void NetApply(WorldSync.Record r)
        {
            if (r.Key == "l:seed") { Seed = r.Seed; return; }
            var c = crystals.Find(cr => cr.Scene == r.Scene);
            if (c == null) { c = new Crystal { Scene = r.Scene }; crystals.Add(c); }
            c.Local = r.Placed ? new Vector2(r.X, r.Y) : (Vector2?)null;
            c.Taken = r.Taken;
            if (c.Renderer != null && c.Taken) c.Renderer.enabled = false;
            UI.WorldMap.Refresh(c.Scene);
        }

        internal static void NetRemove(string key)
        {
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                var c = crystals[i];
                if ("l:" + c.Scene != key) continue;
                if (c.Renderer != null) Object.Destroy(c.Renderer.gameObject);
                crystals.RemoveAt(i);
            }
        }

        internal static bool NetLocate(WorldSync.Action a)
        {
            var c = crystals.Find(cr => cr.Scene == a.Scene && !cr.Taken && !cr.Local.HasValue);
            if (c == null) return false;
            if (!a.Placed) HandOn(c);
            else c.Local = new Vector2(a.X, a.Y);
            return true;
        }

        internal static bool NetMine(WorldSync.Action a)
        {
            var c = crystals.Find(cr => "l:" + cr.Scene == a.Key && !cr.Taken && cr.Local.HasValue);
            if (DevCopy.Name != null) Plugin.Log.LogInfo($"[联机测试] 挖水晶 {a.Key} power={a.Power} found={c != null} damage={c?.Damage}");
            if (c == null) return false;
            c.Damage += Mathf.Clamp(a.Power, 0, 1000);
            if (c.Damage < 100) return true;
            c.Taken = true;
            if (c.Renderer != null) c.Renderer.enabled = false;
            WorldItems.NetDrop(ItemStack.Of(ItemType.LifeCrystal), c.Scene, c.Local.Value + Vector2.up * Cell);
            UI.WorldMap.Refresh(c.Scene);
            DevBroken++;
            return true;
        }
    }
}
