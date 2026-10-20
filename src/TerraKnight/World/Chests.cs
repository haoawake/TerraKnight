using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Chests (tile 21 style 0, Chest.maxItems 40) bought from the Merchant and set on the ground:
    /// 2×2 tiles on two cells of ground, opened by a right click within the player's reach
    /// (Player.IsInTileInteractionRange: 5 tiles across, 4 up and down), which opens the
    /// inventory with the chest's 40 slots under it. The lid opens over ten ticks and shuts
    /// again (Chest.UpdateChestFrames: frame 0 shut, 1 half, 2 open, 38 px apart in Tiles_21).
    /// Going out of reach or closing the inventory shuts it (Player.chest = -1). A pickaxe
    /// breaks an empty chest at 100 pick power (Chest.CanDestroyChest: never one with items
    /// in it), dropping it back as an item. A chest belongs to the scene it stands in, in that
    /// scene's own coordinates, and is kept in the mod's save with what it holds.
    /// </summary>
    internal sealed partial class Chests : MonoBehaviour
    {
        public const int Capacity = 40;
        const float Px = Effects.TerraParticles.Px;
        const float Width = 32f * Px, Height = 32f * Px;
        static float WidthOf(int style) => RegionalFurniture.IsDresser(style) ? 48f * Px : Width;
        // Behind the minions, pets and shots (FxBatch.BehindZ 0.0047), the townsfolk and the player, as the furniture (Stations.Z).
        const float Z = 0.00475f;

        sealed class Chest
        {
            public string Scene;
            /// <summary>Bottom middle, in the scene's own coordinates.</summary>
            public Vector2 Local;
            public readonly ItemStack[] Items;
            /// <summary>The Piggy Bank's (the player's bank, opened at one), not a chest of the world.</summary>
            public bool Bank;
            /// <summary>A LAN guest's: another player has it open (the host says so with its record).</summary>
            public bool NetBusy;
            /// <summary>Which of the player's storages (Inventory.BankOf): 0 the Piggy Bank's, 1 the Safe's, 2 the Defender's Forge's, 3 the Void Vault's.</summary>
            public int BankKind;
            /// <summary>Opened by an item where the player is (the Money Trough, the Void Bag): it goes with him, no reach.</summary>
            public bool Portable;
            /// <summary>The item or furniture it was opened with, for the panel's name.</summary>
            public Items.ItemType OpenedWith;
            /// <summary>Chest.name: what the player called it (ChestUI's Rename), "" for none.</summary>
            public string Name = "";
            /// <summary>Its look: 0 Terraria's chest, 1.. an area's (RegionalChests).</summary>
            public int Style;
            public Chest() : this(new ItemStack[Capacity]) { }
            public Chest(ItemStack[] items) { Items = items; }
            public int FrameCounter, Damage;
            public SpriteRenderer Renderer;
            /// <summary>The smart cursor's outline over it (Images/Misc/TileOutlines/Tiles_21).</summary>
            public SpriteRenderer Outline;
            TerrainRegion region;
            public TerrainRegion Region
            {
                get => region != null && region.Root != null ? region : null;
                set => region = value;
            }
            public Vector2 World => Region != null ? (Vector2)Region.Root.position + Local : Local;
            public Rect Box => new Rect(World.x - WidthOf(Style) / 2f, World.y, WidthOf(Style), Height);
            public bool Empty
            {
                get
                {
                    foreach (var s in Items) if (!s.IsEmpty) return false;
                    return true;
                }
            }
        }

        /// <summary>What the save keeps of a chest.</summary>
        public struct Saved
        {
            public string Scene;
            public Vector2 Local;
            public ItemStack[] Items;
            public string Name;
            public int Style;
        }

        public static Chests Instance { get; private set; }
        static readonly List<Chest> chests = new List<Chest>();
        static readonly Sprite[] frames = new Sprite[3];
        static Chest open;
        float ticks;

        public static Chests Create(Transform parent)
        {
            var go = new GameObject("Terra Chests");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<Chests>();
            return Instance;
        }

        public static int Count => chests.Count;
        public static bool IsOpen => open != null && open.Region != null;
        /// <summary>The open chest's slots (null when none is open).</summary>
        public static ItemStack[] OpenItems => IsOpen ? open.Items : null;

        /// <summary>A loaded chest stands on top of this box (its bottom on the box's top, over it).</summary>
        public static bool RestsOn(Rect below)
        {
            foreach (var c in chests)
            {
                if (c.Region == null) continue;
                var b = c.Box;
                if (b.xMax > below.xMin + 0.01f && b.xMin < below.xMax - 0.01f && Mathf.Abs(b.yMin - below.yMax) < 0.06f) return true;
            }
            return false;
        }

        /// <summary>A loaded chest covers this cell, or stands right on it (WorldGen.CanKillTile: the ground under a chest stays).</summary>
        public static bool Covers(Vector2Int cell, bool orUnder = false)
        {
            var c = CellGrid.CellCenter(cell.x, cell.y);
            const float cs = CellGrid.CellSize;
            foreach (var ch in chests)
            {
                if (ch.Region == null) continue;
                var b = ch.Box;
                if (b.Contains(c)) return true;
                if (orUnder && c.x > b.xMin && c.x < b.xMax && c.y < b.yMin && c.y > b.yMin - cs) return true;
            }
            return false;
        }

        /// <summary>Where each chest in a loaded scene stands (its middle).</summary>
        public static IEnumerable<Vector2> Positions()
        {
            foreach (var c in chests)
                if (c.Region != null) yield return c.Box.center;
        }

        /// <summary>The box each chest in a loaded scene takes up (for what is put down beside them).</summary>
        public static IEnumerable<Rect> Boxes()
        {
            foreach (var c in chests)
                if (c.Region != null) yield return c.Box;
        }

        // ------------------------------------------------------------------ placing

        /// <summary>
        /// Chest placed where the cursor points (TileObject.CanPlace for a 2×2 with an anchor on
        /// solid ground below both cells): the spot snapped to the 16 px grid, ground right under
        /// it (blocks or the scene's own floor), room for 32×32 px and no other chest there,
        /// within the player's reach.
        /// </summary>
        public static bool TryPlace(HeroController hero, Vector2 aim, int style = 0)
        {
            if (!Net.WorldSync.CanInteract) return false;
            if (hero == null) return false;
            if (!FindSpot(hero, aim, out var bottom, style)) return false;
            if (BossRooms.Locked(bottom + new Vector2(0f, 0.1f))) { BossRooms.Notice(bottom); return false; }
            var region = TerraWorld.RegionAt(CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f)).x, CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f)).y)
                         ?? TerrainSystem.Current?.Region;
            if (region == null) return false;
            if (Net.WorldSync.Guest)
            {
                var local = bottom - (Vector2)region.Root.position;
                Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "chest", Scene = region.SceneName, X = local.x, Y = local.y, Kind = style, Count = TerraMod.Instance.Inventory.Infinite ? 0 : 1 });
                return true;
            }
            chests.Add(new Chest { Region = region, Scene = region.SceneName, Local = bottom - (Vector2)region.Root.position, Style = style });
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            DevPlaced++;
            return true;
        }

        public static int DevPlaced, DevBroken, DevOpened;

        /// <summary>Where a chest would stand for this aim (its bottom middle), if it can.</summary>
        public static bool FindSpot(HeroController hero, Vector2 aim, out Vector2 bottom, int style = 0)
        {
            bottom = default;
            const int mask = 1 << ChunkColliders.TerrainLayer;
            const float cell = CellGrid.CellSize;
            // The cursor is on the chest's lower right tile (TileObjectData origin (0,1)).
            float w = WidthOf(style);
            float x = RegionalFurniture.IsDresser(style) ? (Mathf.Floor(aim.x/cell)+.5f)*cell : Mathf.Round(aim.x / cell) * cell;
            var down = Physics2D.Raycast(new Vector2(x, aim.y + 0.25f), Vector2.down, 1.5f, mask);
            if (down.collider == null) return false;
            float y = down.point.y;
            // Both cells need ground under them, at the same height.
            for (int col=0;col<Mathf.RoundToInt(w/cell);col++)
            {
                var side = Physics2D.Raycast(new Vector2(x - w/2f + (col+.5f)*cell, y + 0.1f), Vector2.down, 0.2f, mask);
                if (side.collider == null || Mathf.Abs(side.point.y - y) > 0.06f) return false;
            }
            var centre = new Vector2(x, y + Height / 2f + 0.02f);
            if (Physics2D.OverlapBox(centre, new Vector2(w, Height) * 0.9f, 0f, mask) != null) return false;
            var box = new Rect(x - w / 2f, y, w, Height);
            foreach (var c in chests)
                if (c.Region != null && c.Box.Overlaps(box)) return false;
            if (Stations.Overlaps(box)) return false;
            if (!InReach(hero, box)) return false;
            bottom = new Vector2(x, y);
            return true;
        }

        /// <summary>Player.IsInTileInteractionRange: within 5 tiles across and 4 up or down of the player's hitbox.</summary>
        static bool InReach(HeroController hero, Rect box)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            const float cell = CellGrid.CellSize;
            var reach = Rect.MinMaxRect(b.min.x - 5f * cell, b.min.y - 4f * cell, b.max.x + 5f * cell, b.max.y + 4f * cell);
            return reach.Overlaps(box);
        }

        // ------------------------------------------------------------------ opening

        /// <summary>A right click on a chest in reach: it opens (with the inventory), or shuts if it was the open one.</summary>
        public static bool TryRightClick(Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            foreach (var c in chests)
            {
                if (c.Region == null || !c.Box.Contains(mouse) || !InReach(hero, c.Box)) continue;
                if (open == c) Close(true);
                else Open(c);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the smart cursor's choice (SmartInteractSystem)

        /// <summary>The chest the smart cursor has picked (nearest the cursor of those in reach), or none.</summary>
        static Chest smart;
        public static int DevSmartOpened;

        /// <summary>
        /// SmartInteractSystem: with the smart cursor on and the pointer off the interface, of the chests within the
        /// player's reach (the world's, an area's, his own) the one nearest the cursor is picked: a gold outline on it,
        /// a grey one on the others in reach (TileDrawing's highlight), and a right click anywhere opens it.
        /// </summary>
        static void PickSmart(HeroController hero)
        {
            smart = null;
            var mod = TerraMod.Instance;
            if (mod == null || !mod.SmartCursor || InventoryGui.MouseOverUi || !Net.WorldSync.CanInteract) return;
            Vector2 aim = mod.AimPoint;
            float best = float.MaxValue;
            foreach (var c in chests)
            {
                if (c.Region == null || !InReach(hero, c.Box)) continue;
                var b = c.Box;
                float dx = Mathf.Max(b.xMin - aim.x, 0f, aim.x - b.xMax), dy = Mathf.Max(b.yMin - aim.y, 0f, aim.y - b.yMax);
                float d = dx * dx + dy * dy;
                if (d < best) { best = d; smart = c; }
            }
        }

        /// <summary>A right click that nothing under the cursor took: the smart cursor's chest opens (or shuts, the open one).</summary>
        public static bool TrySmartOpen()
        {
            if (smart == null || smart.Region == null) return false;
            var hero = HeroController.SilentInstance;
            if (hero == null || !InReach(hero, smart.Box)) return false;
            if (open == smart) Close(true);
            else Open(smart);
            DevSmartOpened++;
            return true;
        }

        /// <summary>A Piggy Bank's right click (Stations): the player's bank opens there, or shuts if it was open at that one.</summary>
        public static void ToggleBank(string scene, TerrainRegion region, Vector2 local, int kind = 0, Items.ItemType with = Items.ItemType.PiggyBank)
        {
            var inv = TerraMod.Instance?.Inventory;
            if (inv == null) return;
            if (open != null && open.Bank && open.Scene == scene && open.Local == local) { Close(true); return; }
            Open(new Chest(inv.BankOf(kind)) { Bank = true, BankKind = kind, OpenedWith = with, Scene = scene, Local = local, Region = region });
        }

        /// <summary>
        /// The Money Trough's flying piggy bank, the Void Bag's: the storage opens where the player is and goes with him
        /// (no reach to keep), and the item used again shuts it.
        /// </summary>
        public static void TogglePortable(int kind, Items.ItemType with)
        {
            var inv = TerraMod.Instance?.Inventory;
            var hero = HeroController.SilentInstance;
            var region = TerrainSystem.Current?.Region;
            if (inv == null || hero == null || region == null) return;
            if (open != null && open.Portable && open.BankKind == kind) { Close(true); return; }
            if (open != null) Close(false);
            Open(new Chest(inv.BankOf(kind)) { Bank = true, BankKind = kind, Portable = true, OpenedWith = with, Scene = region.SceneName, Region = region,
                Local = (Vector2)hero.transform.position - (Vector2)region.Root.position });
            DevPortableOpened++;
        }

        public static int DevPortableOpened, DevVoidStored;

        /// <summary>The open storage is one of the player's (the panel's name).</summary>
        public static bool BankOpen => IsOpen && open.Bank;

        /// <summary>The open storage's name: the furniture's or item's it was opened with.</summary>
        public static string BankTitle => !BankOpen ? null : Items.ItemDb.Get(open.OpenedWith == Items.ItemType.None ? Items.ItemType.PiggyBank : open.OpenedWith).Name;

        /// <summary>Which storage is open (-1 none or a chest).</summary>
        public static int OpenBankKind => BankOpen ? open.BankKind : -1;

        /// <summary>The item or furniture the open storage was opened with (None: none open, or a chest); Chester's Eye Bone for his.</summary>
        public static Items.ItemType OpenWith => BankOpen ? open.OpenedWith : Items.ItemType.None;

        /// <summary>The Void Bag carried (the bag or the hotbar): what the bag can't take goes into the Void Vault's slots.</summary>
        public static bool VoidBagCarried(Inventory inv)
        {
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == Items.ItemType.VoidBag) return true;
            return false;
        }

        /// <summary>A stack into the Void Vault's slots (its own stacks first, then empty ones). Returns how many didn't fit.</summary>
        public static int StoreInVoid(Inventory inv, ItemStack s, int count)
        {
            if (s.IsEmpty || count <= 0) return count;
            var def = s.Def;
            var slots = inv.Void;
            bool plain = s.Prefix == 0 && s.Forge == 0;
            for (int i = 0; i < slots.Length && count > 0 && plain; i++)
            {
                if (slots[i].IsEmpty || slots[i].Type != s.Type || slots[i].Prefix != 0 || slots[i].Forge != 0 || slots[i].Count >= def.MaxStack) continue;
                int m = System.Math.Min(count, def.MaxStack - slots[i].Count);
                slots[i].Count += m;
                count -= m;
                DevVoidStored += m;
            }
            for (int i = 0; i < slots.Length && count > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                var put = s;
                put.Count = plain ? System.Math.Min(count, def.MaxStack) : count;
                put.Favorite = false;
                slots[i] = put;
                count -= put.Count;
                DevVoidStored += put.Count;
            }
            return count;
        }

        /// <summary>Room in the Void Vault's slots for some of an item.</summary>
        public static bool VoidHasRoom(Inventory inv, ItemType type)
        {
            foreach (var v in inv.Void)
                if (v.IsEmpty || (v.Type == type && v.Prefix == 0 && v.Forge == 0 && v.Count < v.Def.MaxStack)) return true;
            return false;
        }

        /// <summary>Chest.MaxNameLength.</summary>
        public const int MaxNameLength = 20;

        /// <summary>The open chest's name ("" none; null for the bank or nothing open).</summary>
        public static string OpenName => IsOpen && !open.Bank ? open.Name ?? "" : null;

        /// <summary>ChestUI.RenameChestSubmit: the open chest's new name (trimmed, 20 characters at most).</summary>
        public static void SetOpenName(string name)
        {
            if (!IsOpen || open.Bank) return;
            name = (name ?? "").Trim();
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            open.Name = name;
        }

        /// <summary>The name of the chest under a world point (Main.DrawInterface_ChestNameHover's), null for none or an unnamed one.</summary>
        /// <summary>
        /// Player.TileInteractionsMouseOver_Containers: the chest under the cursor and in reach (the smart cursor's
        /// pick while it has one) shows its name beside the cursor, or, with none given, its item's icon
        /// (WorldGen.GetChestIcon: a chest named as its kind is shown the same way).
        /// </summary>
        public static bool Hover(Vector2 mouse, out string name, out Items.ItemType icon)
        {
            name = null;
            icon = Items.ItemType.None;
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            var hit = smart != null && smart.Region != null ? smart : null;
            if (hit == null)
                foreach (var c in chests)
                    if (c.Region != null && !c.Bank && c.Box.Contains(mouse)) { hit = c; break; }
            if (hit == null || !InReach(hero, hit.Box)) return false;
            icon = RegionalChests.ItemOf(hit.Style);
            var def = Items.ItemDb.Get(icon);
            if (!string.IsNullOrEmpty(hit.Name) && (def == null || hit.Name != def.Name)) { name = hit.Name; icon = Items.ItemType.None; }
            return true;
        }

        public static string NameAt(Vector2 world)
        {
            foreach (var c in chests)
                if (c.Region != null && !c.Bank && !string.IsNullOrEmpty(c.Name) && c.Box.Contains(world)) return c.Name;
            return null;
        }

        static void Open(Chest c)
        {
            if (!c.Bank && Net.NetSession.Active && !Net.WorldSync.OwnsChest(c.Scene, c.Local))
            {
                if (Net.WorldSync.CanInteract) Net.WorldSync.OpenChest(c.Scene, c.Local);
                return;
            }
            if (Town.TownNpcs.ChatOpen) Town.TownNpcs.Close(false);
            open = c;
            InventoryGui.Open = true;
            TerraMod.Instance?.PlaySound(open != null ? "Menu_Open" : "Menu_Close", 0.7f);
            DevOpened++;
        }

        /// <summary>Player.chest = -1 (sound Menu_Close).</summary>
        public static void Close(bool sound)
        {
            if (open == null) return;
            if (!open.Bank && Net.NetSession.Active)
                Net.WorldSync.FlushChest(Net.WorldSync.Key("c", open.Scene, open.Local), NetSlots(open), true);
            open = null;
            if (sound) TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>Dev: opens the chest nearest a point (no reach check).</summary>
        /// <summary>Dev: every chest set down: its scene, whether its scene is loaded, where it stands, how far from the player.</summary>
        public static string DevReport()
        {
            var hero = HeroController.SilentInstance;
            Vector2 from = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            var sb = new System.Text.StringBuilder($"{chests.Count} 个箱子");
            foreach (var c in chests)
                sb.Append($"；{c.Scene} 已加载 {c.Region != null} 位置 {c.Box.center:F1} 距离 {Vector2.Distance(c.Box.center, from):F1}");
            return sb.ToString();
        }

        public static bool DevOpenNear(Vector2 at)
        {
            Chest best = null;
            float bd = float.MaxValue;
            foreach (var c in chests)
            {
                if (c.Region == null) continue;
                float d = Vector2.Distance(c.Box.center, at);
                if (d < bd) { bd = d; best = c; }
            }
            if (best == null) return false;
            Open(best);
            return true;
        }

        /// <summary>Dev: the chest nearest a point, its middle.</summary>
        public static Vector2? DevNearest(Vector2 at)
        {
            Vector2? best = null;
            foreach (var c in chests)
                if (c.Region != null && (best == null || Vector2.Distance(c.Box.center, at) < Vector2.Distance(best.Value, at))) best = c.Box.center;
            return best;
        }

        public static int DevOpenFrame => open != null ? Frame(open) : -1;

        // ------------------------------------------------------------------ moving items

        /// <summary>ChestUI.LootAll: everything into the inventory, as much as fits.</summary>
        public static void LootAll(Inventory inv)
        {
            if (!IsOpen) return;
            for (int i = 0; i < Capacity; i++)
            {
                ref var s = ref open.Items[i];
                if (s.IsEmpty) continue;
                int left = inv.Pickup(s);
                if (left <= 0) s = default;
                else s.Count = left;
            }
        }

        /// <summary>ChestUI.DepositAll: the currencies first (MoveCoins), then the inventory below the hotbar, but its favourites, into the chest.</summary>
        public static void DepositAll(Inventory inv)
        {
            if (!IsOpen) return;
            foreach (var t in Currencies.All) MoveCoins(inv, open, t);
            for (int i = Inventory.Size - 1; i >= Inventory.HotbarSize; i--)
            {
                ref var s = ref inv.Slots[i];
                if (s.IsEmpty || s.Favorite) continue;
                Put(ref s, false);
            }
        }

        /// <summary>
        /// ChestUI.QuickStack: the inventory's items (below the hotbar, not favourites) that the chest already holds go on its
        /// stacks; each currency (MoveCoins, the coin slots' too) into the Piggy Bank, or onto a chest's stack of it.
        /// </summary>
        public static void QuickStack(Inventory inv)
        {
            if (!IsOpen) return;
            foreach (var t in Currencies.All)
                if (open.Bank || Holds(open, t)) MoveCoins(inv, open, t);
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++)
            {
                ref var s = ref inv.Slots[i];
                if (s.IsEmpty || s.Favorite) continue;
                bool held = false;
                foreach (var c in open.Items) held |= !c.IsEmpty && c.Type == s.Type;
                if (held) Put(ref s, false);
            }
        }

        /// <summary>ChestUI.Sort (ItemSorting.SortChest): the open chest's slots sorted and their stacks merged.</summary>
        public static void SortOpen()
        {
            if (SortContainer()) return;
            if (IsOpen) ItemSorting.Sort(open.Items, 0, Capacity, false);
        }

        /// <summary>ChestUI.Restock: the inventory's stacks not yet full topped up from the chest's of the same item.</summary>
        public static void Restock(Inventory inv)
        {
            if (!IsOpen) return;
            for (int i = 0; i < inv.Slots.Length; i++)
            {
                ref var s = ref inv.Slots[i];
                if (s.IsEmpty || s.Count >= s.Def.MaxStack) continue;
                for (int k = 0; k < Capacity && s.Count < s.Def.MaxStack; k++)
                {
                    ref var c = ref open.Items[k];
                    if (c.IsEmpty || c.Type != s.Type) continue;
                    int moved = Mathf.Min(c.Count, s.Def.MaxStack - s.Count);
                    s.Count += moved;
                    c.Count -= moved;
                    if (c.Count <= 0) c = default;
                }
            }
        }

        /// <summary>How far around the player's hitbox the chests' items count for crafting (8 tiles).</summary>
        public const int CraftTiles = 8;

        /// <summary>
        /// The slots of the chests standing within <see cref="CraftTiles"/> tiles of the player's hitbox
        /// in a loaded scene: crafting counts and uses their items after the inventory's. In a LAN game
        /// every such chest but one another player has open (the host's knowledge of who holds which, passed
        /// on with the chests); what is taken from them goes to the host (<see cref="NetCraftTook"/>).
        /// </summary>
        public static List<ItemStack[]> ForCrafting(HeroController hero)
        {
            var list = new List<ItemStack[]>();
            if (hero == null) return list;
            var col = hero.GetComponent<BoxCollider2D>();
            if (col == null) return list;
            var b = col.bounds;
            float reach = CraftTiles * CellGrid.CellSize;
            var around = Rect.MinMaxRect(b.min.x - reach, b.min.y - reach, b.max.x + reach, b.max.y + reach);
            foreach (var c in chests)
                if (c.Region != null && !c.Bank && CraftsFrom(c) && around.Overlaps(c.Box)) list.Add(c.Items);
            return list;
        }

        static bool CraftsFrom(Chest c) => !Net.NetSession.Active || Net.WorldSync.OwnsChest(c.Scene, c.Local)
            || (Net.NetSession.Hosting ? !Net.WorldSync.ChestBusy(c.Scene, c.Local) : !c.NetBusy);

        /// <summary>
        /// Quick Stack to Nearby Chests (QuickStacking.QuickStackToNearbyChests): the bag below the
        /// hotbar, not its favourites, onto every chest within 600 px (NearbyChests.GetChestsInRangeOf)
        /// that already holds that item, a Piggy Bank in range among them (its storage is the player's
        /// bank). Each currency goes too, from anywhere in the bag and the coin slots: into the Piggy Bank
        /// when one stands in range, else onto the chests in range that hold it, else into the nearest of
        /// them. How many stacks moved.
        /// A LAN game's chests are the host's (Net.WorldSync): the ones this player hasn't open go there, in one request
        /// (QuickStacking's packet 85, Chest.ServerPlaceItem), and what they had no room for comes back to its slot.
        /// </summary>
        public static int QuickStackNearby(Inventory inv, Vector2 from)
        {
            int moved = 0;
            float range = 600f * Effects.TerraParticles.Px;
            var near = new List<Chest>();
            if (Stations.PiggyBankWithin(from, range)) near.Add(new Chest(inv.Bank) { Bank = true });
            foreach (var c in chests)
                if (c.Region != null && !c.Bank && Vector2.Distance(c.Box.center, from) <= range) near.Add(c);
            near.Sort((a, b) => a.Bank != b.Bank ? (a.Bank ? -1 : 1) : (a.Bank ? 0 : Vector2.Distance(a.Box.center, from).CompareTo(Vector2.Distance(b.Box.center, from))));
            // Here: the bank and a chest this player has open (alone, every chest); the host's: the rest.
            bool Here(Chest c) => c.Bank || Net.WorldSync.OwnsChest(c.Scene, c.Local);
            var shared = near.FindAll(c => !Here(c));
            var sent = new List<(int Slot, ItemStack Stack, int Target)>();
            bool Sent(int slot) => sent.Exists(e => e.Slot == slot);
            // Each currency: the bank first, else a chest that has some, else the nearest chest.
            if (near.Count > 0)
                foreach (var t in Currencies.All)
                {
                    Chest to = near[0].Bank ? near[0] : near.Find(c => Holds(c, t)) ?? near[0];
                    if (Here(to)) { moved += MoveCoins(inv, to, t); continue; }
                    for (int i = 0; i < Inventory.Total; i++)
                    {
                        var s = inv.Slots[i];
                        if (!s.IsEmpty && !s.Favorite && s.Type == t) sent.Add((i, s, shared.IndexOf(to)));
                    }
                }
            foreach (var c in near)
            {
                if (!Here(c)) continue;
                for (int i = Inventory.HotbarSize; i < Inventory.Size; i++)
                {
                    ref var s = ref inv.Slots[i];
                    if (s.IsEmpty || s.Favorite || Sent(i) || !Holds(c, s.Type)) continue;
                    int before = s.Count;
                    PutIn(c, ref s);
                    if (s.IsEmpty || s.Count != before) moved++;
                }
            }
            if (shared.Count == 0) return moved;
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++)
            {
                var s = inv.Slots[i];
                if (!s.IsEmpty && !s.Favorite && !Sent(i) && shared.Exists(c => Holds(c, s.Type))) sent.Add((i, s, -1));
            }
            return moved + Net.WorldSync.QuickStack(inv, shared.ConvertAll(c => Net.WorldSync.Key("c", c.Scene, c.Local)).ToArray(), sent);
        }

        /// <summary>
        /// ChestUI.MoveCoins: a currency of the whole inventory (the hotbar, the bag, the coin slots; not favourites) onto the
        /// chest's stack of it, else into its first empty slot. How many stacks moved.
        /// </summary>
        static int MoveCoins(Inventory inv, Chest to, ItemType currency)
        {
            int moved = 0;
            for (int i = 0; i < Inventory.Total; i++)
            {
                ref var s = ref inv.Slots[i];
                if (s.IsEmpty || s.Favorite || s.Type != currency) continue;
                if (PutIn(to, ref s)) moved++;
            }
            return moved;
        }

        /// <summary>The chest holds some of this item.</summary>
        static bool Holds(Chest c, ItemType t) => System.Array.Exists(c.Items, it => !it.IsEmpty && it.Type == t);

        /// <summary>
        /// ItemSlot.ShiftClick from the inventory (Chest.PutItemInChest... TryPlacingInChest): onto
        /// the chest's stacks of it first, then its first empty slot. False when nothing moved.
        /// </summary>
        public static bool Put(ref ItemStack s, bool sound = true)
        {
            if (!IsOpen) return false;
            bool moved = PutIn(open, ref s);
            if (moved && sound) TerraMod.Instance?.PlaySound("Grab");
            return moved;
        }

        /// <summary>A stack into a chest: onto its stacks of the item first, then its first empty slot. False when nothing moved.</summary>
        static bool PutIn(Chest chest, ref ItemStack s)
        {
            if (s.IsEmpty) return false;
            // A Banner Case takes banners, a Potion Box potions; the Auto-Sell Bag lists and sells (ChestsContainers.cs).
            if (IsSellList(chest)) return SellListPut(ref s);
            if (Refuses(chest, s)) return false;
            int before = s.Count;
            int max = s.Def.MaxStack;
            for (int i = 0; i < Capacity && s.Count > 0; i++)
            {
                ref var c = ref chest.Items[i];
                if (c.IsEmpty || c.Type != s.Type || c.Count >= max) continue;
                int moved = Mathf.Min(s.Count, max - c.Count);
                c.Count += moved;
                s.Count -= moved;
            }
            for (int i = 0; i < Capacity && s.Count > 0; i++)
            {
                ref var c = ref chest.Items[i];
                if (!c.IsEmpty) continue;
                c = s;
                c.Favorite = false;
                s.Count = 0;
            }
            bool moved2 = s.Count != before;
            if (s.Count <= 0) s = default;
            return moved2;
        }

        // ------------------------------------------------------------------ breaking

        /// <summary>
        /// A pickaxe swung at a chest in reach: pick power until 100 breaks it (an empty one only:
        /// one with items just takes the hit), wood dust each hit, the chest item where it stood.
        /// True when the swing was at a chest.
        /// </summary>
        public static bool TryMine(ItemDef pick, Vector2 mouse, HeroController hero)
        {
            if (!Net.WorldSync.CanInteract) return false;
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            for (int i = 0; i < chests.Count; i++)
            {
                var c = chests[i];
                if (c.Region == null || !c.Box.Contains(mouse) || !InReach(hero, c.Box)) continue;
                if (Net.NetSession.Active && Net.WorldSync.Guest)
                {
                    Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "mineChest", Key = Net.WorldSync.Key("c", c.Scene, c.Local), Power = pick.PickPower });
                    return true;
                }
                var px = Effects.TerraParticles.ToPx(c.Box.center);
                if (!c.Empty || open == c || Net.WorldSync.ChestBusy(c.Scene, c.Local))
                {
                    TerraMod.Instance?.PlaySound("Tink_" + Random.Range(0, 3));
                    WoodDust(px, 1);
                    return true;
                }
                c.Damage += pick.PickPower;
                TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                if (c.Damage < 100)
                {
                    WoodDust(px, 3);
                    return true;
                }
                WoodDust(px, 10);
                WorldItems.Drop(ItemStack.Of(RegionalChests.ItemOf(c.Style)), c.Box.center);
                Remove(i);
                DevBroken++;
                return true;
            }
            return false;
        }

        static void WoodDust(Vector2 px, int n)
        {
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < n; k++) p.NewDust(px - new Vector2(16f, 16f), 32, 32, 7);
        }

        // ------------------------------------------------------------------ each frame

        void Update()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || TerrainSystem.Current == null) return;
            // The save's own chests of the areas, set down as their scenes first come in.
            RegionalChests.Tick();
            foreach (var c in chests) c.Region = RegionNamed(c.Scene);
            if (open != null && open.Bank && !open.Portable) open.Region = RegionNamed(open.Scene);
            // One the player carries goes with him, into whatever scene he walks.
            if (open != null && open.Portable)
            {
                var here = TerrainSystem.Current?.Region;
                if (here != null)
                {
                    open.Region = here;
                    open.Scene = here.SceneName;
                    open.Local = (Vector2)hero.transform.position - (Vector2)here.Root.position;
                }
            }
            // Shut when out of reach, its scene gone, the inventory closed, or a townsperson talked to.
            if (open != null && (open.Region == null || !InventoryGui.Open || hero.cState.dead || (!open.Portable && !InReach(hero, open.Box)) || Town.TownNpcs.ChatOpen))
                Close(open.Region != null);
            ticks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (ticks >= 1f)
            {
                ticks -= 1f;
                foreach (var c in chests)
                {
                    c.FrameCounter = Mathf.Clamp(c.FrameCounter + (c == open ? 1 : -1), 0, 10);
                    // Hits mend once the swinging stops (HitTile's damage fades).
                    if (c.Damage > 0 && Random.Range(0, 60) == 0) c.Damage = 0;
                }
            }
            PickSmart(hero);
            Draw();
        }

        static int Frame(Chest c) => c.FrameCounter == 0 ? 0 : c.FrameCounter == 10 ? 2 : 1;

        static void Draw()
        {
            foreach (var c in chests)
            {
                if (c.Region == null)
                {
                    if (c.Renderer != null) c.Renderer.enabled = false;
                    continue;
                }
                if (c.Renderer == null)
                {
                    var go = new GameObject("Terra Chest");
                    go.transform.SetParent(c.Region.Props ?? c.Region.Root, false);
                    c.Renderer = go.AddComponent<SpriteRenderer>();
                }
                c.Renderer.enabled = true;
                var area = c.Style > 0 ? RegionalChests.FrameSprite(c.Style, Frame(c)) : null;
                c.Renderer.sprite = area ?? FrameSprite(Frame(c));
                if (RegionalFurniture.IsDresser(c.Style) && c.Renderer.GetComponent<StorageTop>() == null) c.Renderer.gameObject.AddComponent<StorageTop>().Init(c.Box, c.Region.Material);
                // Terraria's bottom tile is 18 px tall: its last 2 px sink into the ground (an area's chest its last one, its feet on it).
                c.Renderer.transform.localPosition = new Vector3(c.Local.x, c.Local.y - (RegionalFurniture.IsDresser(c.Style) ? 0f : area != null ? 1f : 2f) * Px, Z - c.Region.Root.position.z);
                DrawOutline(c);
            }
        }

        /// <summary>
        /// TileDrawing's smart cursor highlight: the chest's outline (Images/Misc/TileOutlines/Tiles_21, its lid's frame)
        /// over it, gold and breathing with the mouse text's pulse on the one picked ((n, n, n/3, n), n = (mouseTextColor × 2
        /// + 255) / 3), grey on the others in reach; none with the smart cursor off.
        /// </summary>
        static void DrawOutline(Chest c)
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            bool show = hero != null && mod != null && mod.SmartCursor && !InventoryGui.MouseOverUi && InReach(hero, c.Box);
            var sprite = show && !RegionalFurniture.IsDresser(c.Style) ? OutlineSprite(Frame(c)) : null;
            if (sprite == null)
            {
                if (c.Outline != null) c.Outline.enabled = false;
                return;
            }
            if (c.Outline == null)
            {
                var go = new GameObject("Terra Chest Outline");
                go.transform.SetParent(c.Renderer.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.0001f);
                c.Outline = go.AddComponent<SpriteRenderer>();
            }
            c.Outline.enabled = true;
            c.Outline.sprite = sprite;
            c.Outline.sortingLayerID = c.Renderer.sortingLayerID;
            c.Outline.sortingOrder = c.Renderer.sortingOrder + 1;
            // Main.mouseTextColor: 190..255 and back, 1 a tick.
            float pulse = 190f + 65f * Mathf.PingPong(Time.unscaledTime * 60f / 65f, 1f);
            float n = (pulse * 2f + 255f) / 3f / 255f;
            c.Outline.color = c == smart ? new Color(n, n, n / 3f, n) : new Color(n / 2f, n / 2f, n / 2f, n);
        }

        static readonly Sprite[] outlines = new Sprite[3];
        static bool outlineMissing;

        /// <summary>The outline of one lid frame, put together from TileOutlines/Tiles_21 as the chest's own frames are (none without the file).</summary>
        static Sprite OutlineSprite(int f)
        {
            if (outlines[f] != null || outlineMissing) return outlines[f];
            Color32[] src;
            int sw, sh;
            try { src = TerrariaAssets.Pixels("Misc/TileOutlines/Tiles_21", out sw, out sh); }
            catch (System.Exception)
            {
                outlineMissing = true;
                Plugin.Log.LogWarning("[箱子] 没有找到泰拉瑞亚的箱子描边贴图 Images/Misc/TileOutlines/Tiles_21，智能光标不画描边");
                return null;
            }
            if (src == null) { outlineMissing = true; return null; }
            // The highlight mask is laid out as the tile sheet itself (the same frames at the same places); one that isn't
            // would draw a frame out of place.
            TerrariaAssets.Pixels("Tiles_21", out int tw, out int th0);
            if (tw != sw || th0 != sh)
            {
                outlineMissing = true;
                Plugin.Log.LogWarning($"[箱子] 箱子描边贴图 {sw}x{sh} 和箱子贴图 {tw}x{th0} 排布不同，智能光标不画描边");
                return null;
            }
            const int w = 32, h = 34;
            var dst = new Color32[w * h];
            for (int r = 0; r < 2; r++)
            for (int col = 0; col < 2; col++)
            {
                int th = r == 0 ? 16 : 18;
                for (int y = 0; y < th; y++)
                for (int x = 0; x < 16; x++)
                {
                    int sx = col * 18 + x, sy = r * 18 + f * 38 + y;
                    int dx = col * 16 + x, dy = r * 16 + y;
                    if (sx >= sw || sy >= sh) continue;
                    dst[(h - 1 - dy) * w + dx] = src[(sh - 1 - sy) * sw + sx];
                }
            }
            var tex = TerrariaAssets.Make("Tiles_21#outline" + f, dst, w, h);
            outlines[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            return outlines[f];
        }

        /// <summary>One of the three lid frames, put together from Tiles_21's four tiles (16 px apart, 18 in the sheet; the bottom row 18 tall).</summary>
        static Sprite FrameSprite(int f)
        {
            if (frames[f] != null) return frames[f];
            var src = TerrariaAssets.Pixels("Tiles_21", out int sw, out int sh);
            const int w = 32, h = 34;
            var dst = new Color32[w * h];
            for (int r = 0; r < 2; r++)
            for (int col = 0; col < 2; col++)
            {
                int th = r == 0 ? 16 : 18;
                for (int y = 0; y < th; y++)
                for (int x = 0; x < 16; x++)
                {
                    int sx = col * 18 + x, sy = r * 18 + f * 38 + y;
                    int dx = col * 16 + x, dy = r * 16 + y;
                    if (sx >= sw || sy >= sh) continue;
                    dst[(h - 1 - dy) * w + dx] = src[(sh - 1 - sy) * sw + sx];
                }
            }
            var tex = TerrariaAssets.Make("Tiles_21#chest" + f, dst, w, h);
            frames[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            return frames[f];
        }

        static TerrainRegion RegionNamed(string scene)
        {
            foreach (var t in TerrainSystem.All)
                if (t != null && t.Region != null && t.Region.SceneName == scene) return t.Region;
            return null;
        }

        static void Remove(int i)
        {
            var c = chests[i];
            if (open == c) open = null;
            if (c.Renderer != null) Object.Destroy(c.Renderer.gameObject);
            chests.RemoveAt(i);
        }

        public static void Clear()
        {
            for (int i = chests.Count - 1; i >= 0; i--) Remove(i);
            open = null;
        }

        /// <summary>Whether any chest set down holds an item of this kind.</summary>
        /// <summary>Every chest's slots, in every scene (the Piggy Bank's are the player's, not here).</summary>
        public static IEnumerable<ItemStack[]> AllSlots()
        {
            foreach (var c in chests) if (!c.Bank && Net.WorldSync.OwnsChest(c.Scene, c.Local)) yield return c.Items;
        }

        public static bool Holds(ItemType type)
        {
            foreach (var c in chests)
                foreach (var s in c.Items) if (!s.IsEmpty && s.Type == type) return true;
            return false;
        }

        public static List<Saved> Save()
        {
            var list = new List<Saved>(chests.Count);
            foreach (var c in chests) list.Add(new Saved { Scene = c.Scene, Local = c.Local, Items = (ItemStack[])c.Items.Clone(), Name = c.Name, Style = c.Style });
            return list;
        }

        /// <summary>A chest of the world set down with what it holds (RegionalChests: an area's own, as Terraria's world gen leaves them).</summary>
        internal static void AddWorld(string scene, Vector2 local, int style, ItemStack[] items)
        {
            var c = new Chest { Scene = scene, Local = local, Style = style, Region = RegionNamed(scene) };
            for (int i = 0; i < Capacity && items != null && i < items.Length; i++) c.Items[i] = items[i];
            chests.Add(c);
        }

        /// <summary>A chest of a scene this close to a spot (its bottom middle, the scene's coordinates).</summary>
        internal static bool AnyNear(string scene, Vector2 local, float distance)
        {
            foreach (var c in chests)
                if (c.Scene == scene && Vector2.Distance(c.Local, local) < distance) return true;
            return false;
        }

        public static void Load(IEnumerable<Saved> saved)
        {
            Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                var c = new Chest { Scene = s.Scene, Local = s.Local, Name = s.Name ?? "", Style = s.Style };
                if (s.Items != null)
                    for (int i = 0; i < Capacity && i < s.Items.Length; i++) c.Items[i] = s.Items[i];
                chests.Add(c);
            }
        }
    }
}
