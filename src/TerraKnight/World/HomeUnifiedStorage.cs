using System;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.World
{
    internal sealed partial class Chests
    {
        public sealed class HomeSlot
        {
            public HomeStorage Storage;
            public int Index;
            public ItemStack Item;
            public string Home;
        }
        public static bool HomeTransferPending { get; private set; }
        static void SortHomeArrays(List<HomeStorage> stores)
        {
            int count = 0; foreach (var s in stores) count += s.Items.Length;
            var all = new ItemStack[count]; int offset = 0;
            foreach (var s in stores) { Array.Copy(s.Items, 0, all, offset, s.Items.Length); offset += s.Items.Length; }
            ItemSorting.Sort(all, 0, all.Length, false); offset = 0;
            foreach (var s in stores) { Array.Copy(all, offset, s.Items, 0, s.Items.Length); offset += s.Items.Length; }
        }
        public static bool SortHome()
        {
            if (!Stations.HomeActive || HomeTransferPending || !WorldSync.CanInteract) return false;
            var stores = HomeStorages(Stations.SelectedHome);
            if (NetSession.Active)
            {
                // Personal banks stay on their owner; world containers are sorted atomically by the host.
                SortHomeArrays(stores.FindAll(s => s.Bank >= 0));
                HomeTransferPending = true;
                WorldSync.Submit(new WorldSync.Action { Op = "homeSort", Home = Stations.SelectedHome });
            }
            else SortHomeArrays(stores);
            return true;
        }
        internal static bool NetSortHome(string home, Func<string, bool> busy)
        {
            if (Stations.Home(home) == null) return false;
            var stores = HomeStorages(home).FindAll(s => s.Bank < 0);
            if (stores.Exists(s => busy(s.Key))) return false;
            SortHomeArrays(stores); return true;
        }
        internal static void CompleteHomeSort() { HomeTransferPending = false; }
        static bool SameStack(ItemStack a, ItemStack b) => a.IsEmpty && b.IsEmpty || a.Type == b.Type
            && a.Count == b.Count && a.Prefix == b.Prefix && a.Forge == b.Forge && a.WeaponVersion == b.WeaponVersion;
        static bool Mergeable(ItemStack a, ItemStack b) => !a.IsEmpty && !b.IsEmpty && a.Type == b.Type
            && a.Prefix == b.Prefix && a.Forge == b.Forge && a.WeaponVersion == b.WeaponVersion && a.Def.MaxStack > 1;

        public static List<HomeSlot> UnifiedHomeSlots(string home, string search)
        {
            var filled = new List<HomeSlot>(); var empty = new List<HomeSlot>();
            var groups = new Dictionary<(ItemType, int, int, byte), HomeSlot>();
            search = (search ?? "").Trim();
            foreach (var storage in HomeStorages(home))
            for (int i = 0; i < storage.Items.Length; i++)
            {
                var item = storage.Items[i];
                var entry = new HomeSlot { Storage = storage, Index = i, Item = item, Home = home };
                if (item.IsEmpty) { if (search.Length == 0) empty.Add(entry); continue; }
                var d = item.Def;
                if (search.Length == 0 || d.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || (d.ZhName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || d.Type.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var key = (item.Type, (int)item.Prefix, (int)item.Forge, item.WeaponVersion);
                    if (groups.TryGetValue(key, out var group)) group.Item.Count += item.Count;
                    else { groups.Add(key, entry); filled.Add(entry); }
                }
            }
            filled.AddRange(empty);
            return filled;
        }

        static bool PutHomeArray(ItemStack[] items, ref ItemStack held, bool existingOnly = false)
        {
            if (held.IsEmpty) return false;
            int before = held.Count;
            for (int i = 0; i < items.Length && !held.IsEmpty; i++)
                if (Mergeable(items[i], held))
                {
                    int n = Mathf.Min(held.Count, items[i].Def.MaxStack - items[i].Count);
                    items[i].Count += n; held.Count -= n; if (held.Count <= 0) held = default;
                }
            if (!existingOnly)
                for (int i = 0; i < items.Length && !held.IsEmpty; i++) if (items[i].IsEmpty)
                {
                    int n = Mathf.Min(held.Count, held.Def.MaxStack);
                    items[i] = held; items[i].Count = n; items[i].Favorite = false;
                    held.Count -= n; if (held.Count <= 0) held = default;
                }
            return held.Count != before;
        }

        static bool EditHomeSlot(ItemStack[] items, int i, int mode, ref ItemStack held, ItemStack expected, int take)
        {
            if (i < 0 || i >= items.Length || !SameStack(items[i], expected)) return false;
            ref var slot = ref items[i];
            if (mode == 2)
            {
                if (!held.IsEmpty || slot.IsEmpty || take <= 0) return false;
                held = slot; held.Count = Mathf.Min(take, slot.Count);
                slot.Count -= held.Count; if (slot.Count <= 0) slot = default;
            }
            else if (mode == 1)
            {
                if (slot.IsEmpty || !(held.IsEmpty || Mergeable(held, slot) && held.Count < held.Def.MaxStack)) return false;
                if (held.IsEmpty) { held = slot; held.Count = 0; }
                held.Count++; slot.Count--; if (slot.Count <= 0) slot = default;
            }
            else if (Mergeable(slot, held))
            {
                int n = Mathf.Min(held.Count, slot.Def.MaxStack - slot.Count);
                slot.Count += n; held.Count -= n; if (held.Count <= 0) held = default;
            }
            else (held, slot) = (slot, held);
            if (!slot.IsEmpty) slot.Favorite = false;
            return true;
        }

        static int HomeBagRoom(ItemStack item)
        {
            if (item.IsEmpty) return 0;
            int room = 0; var inv = TerraMod.Instance.Inventory;
            for (int i = 0; i < Inventory.Size; i++)
                room += inv.Slots[i].IsEmpty ? item.Def.MaxStack : Mergeable(inv.Slots[i], item) ? Mathf.Max(0, item.Def.MaxStack - inv.Slots[i].Count) : 0;
            return Mathf.Min(room, item.Count);
        }

        static bool SameHomeKind(ItemStack a, ItemStack b) => !a.IsEmpty && !b.IsEmpty && a.Type == b.Type
            && a.Prefix == b.Prefix && a.Forge == b.Forge && a.WeaponVersion == b.WeaponVersion;
        static void TakeHomeArrays(List<HomeStorage> stores, ItemStack kind, int limit, ref ItemStack held)
        {
            if (!held.IsEmpty && !SameHomeKind(held, kind)) return;
            int room = Mathf.Max(0, kind.Def.MaxStack - (held.IsEmpty ? 0 : held.Count));
            int remaining = Mathf.Min(limit, room);
            foreach (var s in stores)
            for (int i = 0; i < s.Items.Length && remaining > 0; i++)
            {
                if (!SameHomeKind(s.Items[i], kind)) continue;
                int n = Mathf.Min(remaining, s.Items[i].Count);
                if (held.IsEmpty) { held = s.Items[i]; held.Count = 0; held.Favorite = false; }
                held.Count += n; remaining -= n; s.Items[i].Count -= n;
                if (s.Items[i].Count <= 0) s.Items[i] = default;
            }
        }

        public static bool MoveHomeSlot(HomeSlot entry, int mode) => HallownestStorage.Move(TerraMod.Instance.Inventory, () =>
        {
            if (entry == null || entry.Storage == null || entry.Index < 0 || entry.Index >= entry.Storage.Items.Length
                || !Stations.HomeActive || HomeTransferPending || !WorldSync.CanInteract) return false;
            if (entry.Home != null)
            {
                if (entry.Home != Stations.SelectedHome) return false;
                var inventory = TerraMod.Instance.Inventory;
                if (!inventory.Mouse.IsEmpty && mode == 0) return PutHome();
                if (entry.Item.IsEmpty || mode == 2 && !inventory.Mouse.IsEmpty) return false;
                int limit = mode == 1 ? 1 : mode == 2 ? HomeBagRoom(entry.Item) : entry.Item.Def.MaxStack;
                if (limit <= 0 || !inventory.Mouse.IsEmpty && !SameHomeKind(inventory.Mouse, entry.Item)) return false;
                var heldGroup = inventory.Mouse;
                int before = heldGroup.IsEmpty ? 0 : heldGroup.Count;
                var stores = HomeStorages(entry.Home);
                if (NetSession.Active)
                {
                    TakeHomeArrays(stores.FindAll(s => s.Bank >= 0), entry.Item, limit, ref heldGroup);
                    int localTaken = (heldGroup.IsEmpty ? 0 : heldGroup.Count) - before;
                    var action = new WorldSync.Action { Op = "homeSlot", Home = entry.Home, Power = 4, Kind = mode,
                        Targets = new[] { Mathf.Max(0, limit - localTaken) },
                        Slots = new[] { WorldSync.Stack.From(heldGroup), WorldSync.Stack.From(entry.Item) } };
                    inventory.Mouse = default; HomeTransferPending = true; WorldSync.Submit(action); return true;
                }
                TakeHomeArrays(stores, entry.Item, limit, ref heldGroup);
                if ((heldGroup.IsEmpty ? 0 : heldGroup.Count) == before) return false;
                if (mode == 2) { int left = inventory.Pickup(heldGroup); heldGroup.Count = left; if (left <= 0) heldGroup = default; }
                inventory.Mouse = heldGroup;
                if (!heldGroup.IsEmpty) inventory.RecordCollection(heldGroup.Type);
                return true;
            }
            // Legacy physical-slot requests retain snapshot validation.
            var live = HomeStorages(Stations.SelectedHome).Find(s => s.Key == entry.Storage.Key && s.Items == entry.Storage.Items);
            if (live == null || !SameStack(live.Items[entry.Index], entry.Item)) return false;
            var inv = TerraMod.Instance.Inventory;
            int take = mode == 2 ? HomeBagRoom(entry.Item) : 1;
            if (mode == 2 && (!inv.Mouse.IsEmpty || take <= 0)) return false;
            if (NetSession.Active && live.Bank < 0)
            {
                var a = new WorldSync.Action { Op = "homeSlot", Home = Stations.SelectedHome, Key = live.Key,
                    Kind = entry.Index, Power = mode, Targets = new[] { take },
                    Slots = new[] { WorldSync.Stack.From(inv.Mouse), WorldSync.Stack.From(entry.Item) } };
                inv.Mouse = default; HomeTransferPending = true; WorldSync.Submit(a); return true;
            }
            var held = inv.Mouse;
            if (!EditHomeSlot(live.Items, entry.Index, mode, ref held, entry.Item, take)) return false;
            if (mode == 2) { int left = inv.Pickup(held); held.Count = left; if (left <= 0) held = default; }
            inv.Mouse = held;
            if (!held.IsEmpty) inv.RecordCollection(held.Type);
            return true;
        });

        public static bool PutHome(int source = -1) => HallownestStorage.Move(TerraMod.Instance.Inventory, () =>
        {
            if (!Stations.HomeActive || HomeTransferPending || !WorldSync.CanInteract) return false;
            var inv = TerraMod.Instance.Inventory;
            var held = source >= 0 ? inv.Slots[source] : inv.Mouse;
            if (held.IsEmpty || held.Favorite) return false;
            int before = held.Count;
            var stores = HomeStorages(Stations.SelectedHome);
            if (NetSession.Active)
            {
                foreach (var s in stores) if (s.Bank >= 0) PutHomeArray(s.Items, ref held);
                if (source >= 0) inv.Slots[source] = default; else inv.Mouse = default;
                var a = new WorldSync.Action { Op = "homeSlot", Home = Stations.SelectedHome, Power = 3,
                    Count = source + 1, Slots = new[] { WorldSync.Stack.From(held) } };
                HomeTransferPending = true; WorldSync.Submit(a); return true;
            }
            foreach (var s in stores) PutHomeArray(s.Items, ref held, true);
            foreach (var s in stores) PutHomeArray(s.Items, ref held);
            if (source >= 0) inv.Slots[source] = held; else inv.Mouse = held;
            return held.Count != before;
        });

        /// <summary>
        /// Terraria's Deposit All (ChestUI.DepositAll) into the open home: the bag below the hotbar, favourites kept. In a
        /// session one stack after another, each once the last has come back from the host.
        /// </summary>
        public static bool DepositAllHome()
        {
            if (!Stations.HomeActive || HomeTransferPending || !WorldSync.CanInteract) return false;
            var inv = TerraMod.Instance.Inventory;
            bool any = false;
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++) any |= !inv.Slots[i].IsEmpty && !inv.Slots[i].Favorite;
            if (!any) return false;
            if (NetSession.Active) { TerraMod.Instance.StartCoroutine(DepositAllHomeNet(Stations.SelectedHome)); return true; }
            any = false;
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++) any |= PutHome(i);
            return any;
        }

        static System.Collections.IEnumerator DepositAllHomeNet(string home)
        {
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++)
            {
                float wait = Time.unscaledTime + 5f;
                while (HomeTransferPending && Time.unscaledTime < wait) yield return null;
                if (Stations.SelectedHome != home || HomeTransferPending) yield break;
                PutHome(i);
            }
        }

        internal static WorldSync.Stack[] NetHomeMove(WorldSync.Action a, Func<string, bool> busy)
        {
            if (Stations.Home(a.Home) == null || a.Slots == null || a.Slots.Length < 1 || a.Slots.Length > 2 || a.Power < 0 || a.Power > 4) return null;
            var held = a.Slots[0]?.Item() ?? default;
            if (a.Power == 4)
            {
                if (a.Slots.Length != 2 || a.Kind < 0 || a.Kind > 2) return null;
                var kind = a.Slots[1]?.Item() ?? default;
                if (kind.IsEmpty || !held.IsEmpty && !SameHomeKind(held, kind)) return null;
                int limit = a.Targets != null && a.Targets.Length == 1 ? Mathf.Clamp(a.Targets[0], 0, a.Kind == 1 ? 1 : kind.Def.MaxStack) : 0;
                TakeHomeArrays(HomeStorages(a.Home).FindAll(s => s.Bank < 0 && !busy(s.Key)), kind, limit, ref held);
            }
            else if (a.Power == 3)
            {
                var stores = HomeStorages(a.Home).FindAll(s => s.Bank < 0 && !busy(s.Key));
                foreach (var s in stores) PutHomeArray(s.Items, ref held, true);
                foreach (var s in stores) PutHomeArray(s.Items, ref held);
            }
            else
            {
                if (a.Slots.Length != 2 || busy(a.Key) || !HomeCanUseChest(a.Home, a.Key)) return null;
                var c = chests.Find(x => WorldSync.Key("c", x.Scene, x.Local) == a.Key);
                int take = a.Targets != null && a.Targets.Length == 1 ? Mathf.Clamp(a.Targets[0], 0, 9999) : 1;
                if (c == null || !EditHomeSlot(c.Items, a.Kind, a.Power, ref held, a.Slots[1]?.Item() ?? default, take)) return null;
            }
            return new[] { WorldSync.Stack.From(held) };
        }

        internal static void CompleteHomeMove(WorldSync.Action a, bool ok, WorldSync.Stack[] result)
        {
            HomeTransferPending = false;
            var inv = TerraMod.Instance?.Inventory; if (inv == null) return;
            HallownestStorage.Move(inv, () =>
            {
                var back = ok && result != null && result.Length == 1 ? result[0]?.Item() ?? default : a.Slots[0]?.Item() ?? default;
                if (back.IsEmpty) return;
                if (a.Count > 0 && a.Count <= inv.Slots.Length && inv.Slots[a.Count - 1].IsEmpty) { inv.Slots[a.Count - 1] = back; return; }
                if (a.Power != 2 && !(a.Power == 4 && a.Kind == 2) && inv.Mouse.IsEmpty) { inv.Mouse = back; inv.RecordCollection(back.Type); return; }
                int left = inv.Pickup(back);
                if (left > 0) { back.Count = left; if (inv.Mouse.IsEmpty) inv.Mouse = back; else WorldItems.Drop(back, HeroController.instance.transform.position); }
            });
        }
    }
}
