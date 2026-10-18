using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The Banner Case's and the Potion Box's storage in the chest panel (Items.Containers): opened where the player is as
    /// the Void Bag's vault is (it goes with him, the inventory shut shuts it), a page of 40 slots at a time (the panel's
    /// arrows turn them), taking only what the container is for; sorted, all its pages as one.
    /// </summary>
    internal sealed partial class Chests
    {
        static int containerPage;

        /// <summary>A Banner Case or a Potion Box is open.</summary>
        public static bool ContainerOpen => BankOpen && Containers.IsKind(open.BankKind);
        public static int ContainerPage => ContainerOpen ? containerPage : 0;
        public static int ContainerPages => ContainerOpen ? TerraMod.Instance.Inventory.ContainerOf(open.BankKind).Length : 0;

        public static int DevContainerOpened;

        /// <summary>The container's first page opened here, or the container shut if it is the one open.</summary>
        public static void ToggleContainer(int kind, ItemType with)
        {
            var inv = TerraMod.Instance?.Inventory;
            var hero = HeroController.SilentInstance;
            var region = TerrainSystem.Current?.Region;
            var pages = inv?.ContainerOf(kind);
            if (pages == null || hero == null || region == null) return;
            if (open != null && open.Bank && open.BankKind == kind) { Close(true); return; }
            if (open != null) Close(false);
            containerPage = 0;
            Open(new Chest(pages[0]) { Bank = true, BankKind = kind, Portable = true, OpenedWith = with, Scene = region.SceneName, Region = region,
                Local = (Vector2)hero.transform.position - (Vector2)region.Root.position });
            DevContainerOpened++;
        }

        /// <summary>The open container's next (or previous) page, round its pages.</summary>
        public static void TurnPage(int delta)
        {
            if (!ContainerOpen) return;
            var pages = TerraMod.Instance.Inventory.ContainerOf(open.BankKind);
            containerPage = ((containerPage + delta) % pages.Length + pages.Length) % pages.Length;
            var was = open;
            open = new Chest(pages[containerPage]) { Bank = true, BankKind = was.BankKind, Portable = true, OpenedWith = was.OpenedWith, Scene = was.Scene,
                Region = was.Region, Local = was.Local };
        }

        /// <summary>What the open storage won't take: a Banner Case anything but banners, a Potion Box anything but potions.</summary>
        public static bool OpenRefuses(ItemStack s) => IsOpen && Refuses(open, s);

        static bool Refuses(Chest c, ItemStack s) => c.Bank && Containers.IsKind(c.BankKind) && !s.IsEmpty && !Containers.Fits(c.BankKind, s.Def);

        /// <summary>The Auto-Sell Bag's list is the storage open.</summary>
        public static bool SellListOpen => ContainerOpen && open.BankKind == Containers.SellKind;
        public static bool PotionBoxOpen => ContainerOpen && open.BankKind == Containers.BoxKind;

        static bool IsSellList(Chest c) => c.Bank && c.BankKind == Containers.SellKind;

        /// <summary>A stack sent into the open Auto-Sell Bag (Shift+click, Deposit All): its item listed and the stack sold.</summary>
        static bool SellListPut(ref ItemStack s)
        {
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : Vector2.zero;
            return Containers.ListAndSell(TerraMod.Instance.Inventory, ref s, null, -1, at, out _);
        }

        /// <summary>ChestUI.Sort for a container: every page sorted as one, its favourites left where they are.</summary>
        static bool SortContainer()
        {
            if (!ContainerOpen) return false;
            var pages = TerraMod.Instance.Inventory.ContainerOf(open.BankKind);
            int per = Inventory.ContainerPageSlots;
            var flat = new ItemStack[pages.Length * per];
            for (int p = 0; p < pages.Length; p++) System.Array.Copy(pages[p], 0, flat, p * per, per);
            ItemSorting.Sort(flat, 0, flat.Length, true);
            for (int p = 0; p < pages.Length; p++) System.Array.Copy(flat, p * per, pages[p], 0, per);
            return true;
        }
    }
}
