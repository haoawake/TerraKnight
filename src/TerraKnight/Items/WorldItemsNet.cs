using System;
using System.Collections.Generic;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class WorldItems
    {
        static float nextMotion;
        static bool NetSimulates(Item it)
        {
            if (it.NetId == null || !NetSession.Active) return true;
            if (WorldSync.Guest && it.PhysicsOwner >= 0) return it.PhysicsOwner == NetSession.MySlot;
            return NetSession.Owners.TryGetValue(it.Scene, out int owner) ? owner == NetSession.MySlot : NetSession.Hosting;
        }

        internal static void NetFlushMotion()
        {
            if (!WorldSync.Guest || Time.unscaledTime < nextMotion) return;
            nextMotion = Time.unscaledTime + 0.1f;
            var list = new List<WorldSync.Record>();
            foreach (var it in items)
                if (it.NetId != null && !it.Stack.IsEmpty && NetSimulates(it))
                {
                    list.Add(NetRecord(it));
                    if (list.Count == 32) { WorldSync.Submit(new WorldSync.Action { Op = "motion", Motion = list.ToArray() }); list.Clear(); }
                }
            if (list.Count > 0) WorldSync.Submit(new WorldSync.Action { Op = "motion", Motion = list.ToArray() });
        }

        internal static bool NetMotion(WorldSync.Record[] motion, int slot)
        {
            if (motion == null || motion.Length > 32) return false;
            foreach (var r in motion)
            {
                var it = items.Find(item => item.NetId == r.Key);
                if (it == null || !NetSession.Owners.TryGetValue(it.Scene, out int owner) || owner != slot) continue;
                if (r.Taken) { it.Stack = default; continue; }
                if (r.Scene != it.Scene && !NetSession.HaveScene(r.Scene) && !(NetSession.Player(slot)?.Loaded.Contains(r.Scene) ?? false)) continue;
                var region = RegionNamed(r.Scene);
                if (it.Region != region && it.Renderer != null) { UnityEngine.Object.Destroy(it.Renderer.gameObject); it.Renderer = null; }
                it.Scene = r.Scene;
                it.Local = new Vector2(r.X, r.Y);
                it.Vel = new Vector2(r.Vx, r.Vy);
                it.Region = region;
                it.Age = r.Age;
                it.GrabDelay = Mathf.Max(0, r.Delay);
            }
            return true;
        }

        internal static IEnumerable<WorldSync.Record> NetRecords()
        {
            foreach (var it in items)
                if (it.NetId != null && !it.Stack.IsEmpty)
                    yield return NetRecord(it);
        }

        static WorldSync.Record NetRecord(Item it) => new WorldSync.Record { Key = it.NetId, Scene = it.Scene, X = it.Local.x, Y = it.Local.y,
            Vx = it.Vel.x, Vy = it.Vel.y, Kind = (int)it.Stack.Type, Seed = it.Stack.Count, Prefix = it.Stack.Prefix, Forge = it.Stack.Forge,
            Age = it.Age, Delay = it.GrabDelay, Sky = it.FromSky, Dropper = it.Dropper,
            Owner = NetSession.Owners.TryGetValue(it.Scene, out int owner) ? owner : NetSession.Hosting ? NetSession.MySlot : it.PhysicsOwner };

        internal static void NetApply(WorldSync.Record r)
        {
            var it = items.Find(item => item.NetId == r.Key);
            bool created = it == null;
            if (created) { it = new Item { NetId = r.Key }; items.Add(it); }
            bool simulate = !created && NetSimulates(it);
            var region = RegionNamed(r.Scene);
            if (it.Region != region && it.Renderer != null) { UnityEngine.Object.Destroy(it.Renderer.gameObject); it.Renderer = null; }
            it.Scene = r.Scene;
            if (!simulate)
            {
                it.Local = new Vector2(r.X, r.Y);
                it.Vel = new Vector2(r.Vx, r.Vy);
                it.Age = r.Age;
                it.GrabDelay = r.Delay;
            }
            it.Region = region;
            it.Stack = ItemStack.Of((ItemType)r.Kind, r.Seed);
            it.Stack.Prefix = r.Prefix;
            it.Stack.Forge = r.Forge;
            it.FromSky = r.Sky;
            it.Dropper = r.Dropper;
            it.PhysicsOwner = r.Owner;
        }

        internal static void NetRemove(string key)
        {
            for (int i = items.Count - 1; i >= 0; i--) if (items[i].NetId == key) Remove(i);
        }

        internal static void NetClear()
        {
            for (int i = items.Count - 1; i >= 0; i--) if (items[i].NetId != null) Remove(i);
        }

        internal static void NetDetach()
        {
            foreach (var it in items)
            {
                it.NetId = null;
                it.PhysicsOwner = it.Dropper = -1;
                it.ClaimPending = false;
            }
        }

        internal static bool NetSpawn(WorldSync.Action a, int dropper)
        {
            if (!ItemDb.Has((ItemType)a.Kind) || a.Count <= 0 || string.IsNullOrEmpty(a.Scene)) return false;
            // Do not silently evict an item somebody has already paid for by throwing it.
            var stack = ItemStack.Of((ItemType)a.Kind, a.Count);
            stack.Prefix = a.Prefix;
            stack.Forge = a.Forge;
            items.Add(new Item { NetId = "i:" + Guid.NewGuid().ToString("N"), Stack = stack,
                Scene = a.Scene, Local = new Vector2(a.X, a.Y), Vel = new Vector2(a.Vx, a.Vy),
                Region = RegionNamed(a.Scene), Dropper = dropper, GrabDelay = Mathf.Max(0, a.Power), FromSky = a.Sky });
            return true;
        }

        internal static void NetDrop(ItemStack stack, string scene, Vector2 local)
        {
            NetSpawn(new WorldSync.Action { Kind = (int)stack.Type, Count = stack.Count, Prefix = stack.Prefix, Forge = stack.Forge, Scene = scene, X = local.x, Y = local.y,
                Vx = UnityEngine.Random.Range(-3f, 3f), Vy = UnityEngine.Random.Range(1.6f, 4f) }, -1);
        }

        internal static void NetClaim(WorldSync.Action a, int slot, out int type, out int count, out byte prefix, out byte forge)
        {
            type = count = 0;
            prefix = forge = 0;
            var it = items.Find(item => item.NetId == a.Key && !item.Stack.IsEmpty);
            if (it == null || a.Count <= 0 || (it.Dropper == slot && it.GrabDelay > 0)) return;
            var player = NetSession.Player(slot);
            if (slot != NetSession.MySlot)
            {
                if (player == null || !player.HasState || player.Latest.Dead || !player.Latest.Shown) return;
                if (it.Scene != player.Latest.Scene && !player.Loaded.Contains(it.Scene) && !player.In.Contains(it.Scene)) return;
                if (it.Region != null && NetScenes.ToWorld(player.Latest.Scene, player.Latest.BoxCenter, out var at))
                {
                    // Slight tolerance for TCP state age and the 10 Hz item snapshot; the client's
                    // local magnet still determines when to request a pickup.
                    float range = GrabRange + 150f * Px + 2f;
                    if ((it.World - at).sqrMagnitude > range * range) return;
                }
            }
            type = (int)it.Stack.Type;
            prefix = it.Stack.Prefix;
            forge = it.Stack.Forge;
            count = Mathf.Min(a.Count, it.Stack.Count);
            it.Stack.Count -= count;
            if (it.Stack.Count <= 0) it.Stack = default;
        }

        internal static void NetClaimDone(string key)
        {
            var it = items.Find(item => item.NetId == key);
            if (it != null) it.ClaimPending = false;
        }
    }
}
