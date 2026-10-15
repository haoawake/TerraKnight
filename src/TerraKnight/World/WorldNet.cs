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
                if (!s.Fixed) yield return new WorldSync.Record { Key = WorldSync.Key("s", s.Scene, s.Local), Scene = s.Scene, X = s.Local.x, Y = s.Local.y, Kind = (int)s.Kind };
        }

        internal static void NetApply(WorldSync.Record r)
        {
            if (!specs.ContainsKey((StationKind)r.Kind)) return;
            var at = new Vector2(r.X, r.Y);
            foreach (var s in stations)
                if (!s.Fixed && WorldSync.Key("s", s.Scene, s.Local) == r.Key)
                {
                    if (s.Kind != (StationKind)r.Kind)
                    {
                        if (s.Renderer != null) Object.Destroy(s.Renderer.gameObject);
                        s.Renderer = null; s.Top = null; s.SupportedIn = null;
                        s.Kind = (StationKind)r.Kind;
                    }
                    return;
                }
            stations.Add(new Station { Scene = r.Scene, Local = at, Kind = (StationKind)r.Kind, Region = RegionNamed(r.Scene) });
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
            if (!specs.TryGetValue((StationKind)a.Kind, out var spec) || spec.Item == ItemType.None || string.IsNullOrEmpty(a.Scene)) return false;
            var box = new Rect(a.X - spec.W * 8f * Px, a.Y, spec.W * 16f * Px, spec.H * 16f * Px);
            if (NetOverlaps(a.Scene, box) || Chests.NetOverlaps(a.Scene, box)) return false;
            NetApply(new WorldSync.Record { Key = WorldSync.Key("s", a.Scene, new Vector2(a.X, a.Y)), Scene = a.Scene, X = a.X, Y = a.Y, Kind = a.Kind });
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
                yield return new WorldSync.Record { Key = WorldSync.Key("c", c.Scene, c.Local), Scene = c.Scene, X = c.Local.x, Y = c.Local.y, Slots = NetSlots(c) };
        }

        internal static void NetApply(WorldSync.Record r, bool keepContents)
        {
            var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == r.Key);
            if (c == null)
            {
                c = new Chest { Scene = r.Scene, Local = new Vector2(r.X, r.Y), Region = RegionNamed(r.Scene) };
                chests.Add(c);
            }
            if (!keepContents) NetSet(c, r.Slots);
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

        internal static void NetOpen(string key, WorldSync.Stack[] slots)
        {
            var c = chests.Find(ch => WorldSync.Key("c", ch.Scene, ch.Local) == key);
            if (c == null) return;
            NetSet(c, slots);
            c.Region = RegionNamed(c.Scene);
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
                if (c.Scene == scene && new Rect(c.Local.x - Width / 2f, c.Local.y, Width, Height).Overlaps(box)) return true;
            return false;
        }

        internal static bool NetRestsOn(string scene, Rect box)
        {
            foreach (var c in chests)
                if (c.Scene == scene && c.Local.x + Width / 2f > box.xMin + 0.01f && c.Local.x - Width / 2f < box.xMax - 0.01f && Mathf.Abs(c.Local.y - box.yMax) < 0.06f) return true;
            return false;
        }

        internal static bool NetPlace(WorldSync.Action a)
        {
            if (string.IsNullOrEmpty(a.Scene)) return false;
            var box = new Rect(a.X - Width / 2f, a.Y, Width, Height);
            if (NetOverlaps(a.Scene, box) || Stations.NetOverlaps(a.Scene, box)) return false;
            var at = new Vector2(a.X, a.Y);
            chests.Add(new Chest { Scene = a.Scene, Local = at, Region = RegionNamed(a.Scene) });
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
                WorldItems.NetDrop(ItemStack.Of(ItemType.Chest), c.Scene, c.Local + Vector2.up * Height / 2f);
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
