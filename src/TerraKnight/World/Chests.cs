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
        const float Z = 0.004f;

        sealed class Chest
        {
            public string Scene;
            /// <summary>Bottom middle, in the scene's own coordinates.</summary>
            public Vector2 Local;
            public readonly ItemStack[] Items;
            /// <summary>The Piggy Bank's (the player's bank, opened at one), not a chest of the world.</summary>
            public bool Bank;
            /// <summary>Chest.name: what the player called it (ChestUI's Rename), "" for none.</summary>
            public string Name = "";
            public Chest() : this(new ItemStack[Capacity]) { }
            public Chest(ItemStack[] items) { Items = items; }
            public int FrameCounter, Damage;
            public SpriteRenderer Renderer;
            TerrainRegion region;
            public TerrainRegion Region
            {
                get => region != null && region.Root != null ? region : null;
                set => region = value;
            }
            public Vector2 World => Region != null ? (Vector2)Region.Root.position + Local : Local;
            public Rect Box => new Rect(World.x - Width / 2f, World.y, Width, Height);
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

        // ------------------------------------------------------------------ placing

        /// <summary>
        /// Chest placed where the cursor points (TileObject.CanPlace for a 2×2 with an anchor on
        /// solid ground below both cells): the spot snapped to the 16 px grid, ground right under
        /// it (blocks or the scene's own floor), room for 32×32 px and no other chest there,
        /// within the player's reach.
        /// </summary>
        public static bool TryPlace(HeroController hero, Vector2 aim)
        {
            if (!Net.WorldSync.CanInteract) return false;
            if (hero == null) return false;
            if (!FindSpot(hero, aim, out var bottom)) return false;
            if (BossRooms.Locked(bottom + new Vector2(0f, 0.1f))) { BossRooms.Notice(bottom); return false; }
            var region = TerraWorld.RegionAt(CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f)).x, CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f)).y)
                         ?? TerrainSystem.Current?.Region;
            if (region == null) return false;
            if (Net.WorldSync.Guest)
            {
                var local = bottom - (Vector2)region.Root.position;
                Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "chest", Scene = region.SceneName, X = local.x, Y = local.y, Count = TerraMod.Instance.Inventory.Infinite ? 0 : 1 });
                return true;
            }
            chests.Add(new Chest { Region = region, Scene = region.SceneName, Local = bottom - (Vector2)region.Root.position });
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            DevPlaced++;
            return true;
        }

        public static int DevPlaced, DevBroken, DevOpened;

        /// <summary>Where a chest would stand for this aim (its bottom middle), if it can.</summary>
        public static bool FindSpot(HeroController hero, Vector2 aim, out Vector2 bottom)
        {
            bottom = default;
            const int mask = 1 << ChunkColliders.TerrainLayer;
            const float cell = CellGrid.CellSize;
            // The cursor is on the chest's lower right tile (TileObjectData origin (0,1)).
            float x = Mathf.Round(aim.x / cell) * cell;
            var down = Physics2D.Raycast(new Vector2(x, aim.y + 0.25f), Vector2.down, 1.5f, mask);
            if (down.collider == null) return false;
            float y = down.point.y;
            // Both cells need ground under them, at the same height.
            foreach (float dx in new[] { -cell / 2f, cell / 2f })
            {
                var side = Physics2D.Raycast(new Vector2(x + dx, y + 0.1f), Vector2.down, 0.2f, mask);
                if (side.collider == null || Mathf.Abs(side.point.y - y) > 0.06f) return false;
            }
            var centre = new Vector2(x, y + Height / 2f + 0.02f);
            if (Physics2D.OverlapBox(centre, new Vector2(Width, Height) * 0.9f, 0f, mask) != null) return false;
            var box = new Rect(x - Width / 2f, y, Width, Height);
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

        /// <summary>A Piggy Bank's right click (Stations): the player's bank opens there, or shuts if it was open at that one.</summary>
        public static void ToggleBank(string scene, TerrainRegion region, Vector2 local)
        {
            var inv = TerraMod.Instance?.Inventory;
            if (inv == null) return;
            if (open != null && open.Bank && open.Scene == scene && open.Local == local) { Close(true); return; }
            Open(new Chest(inv.Bank) { Bank = true, Scene = scene, Local = local, Region = region });
        }

        /// <summary>The open storage is the Piggy Bank (the panel's name).</summary>
        public static bool BankOpen => IsOpen && open.Bank;

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

        /// <summary>ChestUI.DepositAll: the Geo first (MoveCoins), then the inventory below the hotbar, but its favourites, into the chest.</summary>
        public static void DepositAll(Inventory inv)
        {
            if (!IsOpen) return;
            MoveGeo(inv, open);
            for (int i = Inventory.Size - 1; i >= Inventory.HotbarSize; i--)
            {
                ref var s = ref inv.Slots[i];
                if (s.IsEmpty || s.Favorite) continue;
                Put(ref s, false);
            }
        }

        /// <summary>
        /// ChestUI.QuickStack: the inventory's items (below the hotbar, not favourites) that the chest already holds go on its
        /// stacks; the Geo (MoveCoins, the coin slot's too) into the Piggy Bank, or onto a chest's Geo.
        /// </summary>
        public static void QuickStack(Inventory inv)
        {
            if (!IsOpen) return;
            if (open.Bank || System.Array.Exists(open.Items, it => !it.IsEmpty && it.Type == ItemType.Geo)) MoveGeo(inv, open);
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
        /// in a loaded scene: crafting counts and uses their items after the inventory's.
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
                if (c.Region != null && !c.Bank && Net.WorldSync.OwnsChest(c.Scene, c.Local) && around.Overlaps(c.Box)) list.Add(c.Items);
            return list;
        }

        /// <summary>
        /// Quick Stack to Nearby Chests (QuickStacking.QuickStackToNearbyChests): the bag below the
        /// hotbar, not its favourites, onto every chest within 600 px (NearbyChests.GetChestsInRangeOf)
        /// that already holds that item, a Piggy Bank in range among them (its storage is the player's
        /// bank). The geo goes too, from anywhere in the bag and the coin slot: into the Piggy Bank when
        /// one stands in range, else onto the chests in range that hold geo, else into the nearest of
        /// them. How many stacks moved.
        /// </summary>
        public static int QuickStackNearby(Inventory inv, Vector2 from)
        {
            int moved = 0;
            float range = 600f * Effects.TerraParticles.Px;
            var near = new List<Chest>();
            if (Stations.PiggyBankWithin(from, range)) near.Add(new Chest(inv.Bank) { Bank = true });
            foreach (var c in chests)
                if (c.Region != null && !c.Bank && Net.WorldSync.OwnsChest(c.Scene, c.Local) && Vector2.Distance(c.Box.center, from) <= range) near.Add(c);
            near.Sort((a, b) => a.Bank != b.Bank ? (a.Bank ? -1 : 1) : (a.Bank ? 0 : Vector2.Distance(a.Box.center, from).CompareTo(Vector2.Distance(b.Box.center, from))));
            // The geo: the bank first, else a chest that has some, else the nearest chest.
            if (near.Count > 0)
            {
                Chest to = near[0].Bank ? near[0] : near.Find(c => System.Array.Exists(c.Items, it => !it.IsEmpty && it.Type == ItemType.Geo)) ?? near[0];
                moved += MoveGeo(inv, to);
            }
            foreach (var c in near)
            {
                for (int i = Inventory.HotbarSize; i < Inventory.Size; i++)
                {
                    ref var s = ref inv.Slots[i];
                    if (s.IsEmpty || s.Favorite) continue;
                    bool held = false;
                    foreach (var it in c.Items) held |= !it.IsEmpty && it.Type == s.Type;
                    if (!held) continue;
                    int before = s.Count;
                    PutIn(c, ref s);
                    if (s.IsEmpty || s.Count != before) moved++;
                }
            }
            return moved;
        }

        /// <summary>
        /// ChestUI.MoveCoins: the Geo of the whole inventory (the hotbar, the bag, the coin slot; not favourites) onto the
        /// chest's Geo, else into its first empty slot. How many stacks moved.
        /// </summary>
        static int MoveGeo(Inventory inv, Chest to)
        {
            int moved = 0;
            for (int i = 0; i < Inventory.Total; i++)
            {
                ref var s = ref inv.Slots[i];
                if (s.IsEmpty || s.Favorite || s.Type != ItemType.Geo) continue;
                if (PutIn(to, ref s)) moved++;
            }
            return moved;
        }

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
                WorldItems.Drop(ItemStack.Of(ItemType.Chest), c.Box.center);
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
            foreach (var c in chests) c.Region = RegionNamed(c.Scene);
            if (open != null && open.Bank) open.Region = RegionNamed(open.Scene);
            // Shut when out of reach, its scene gone, the inventory closed, or a townsperson talked to.
            if (open != null && (open.Region == null || !InventoryGui.Open || hero.cState.dead || !InReach(hero, open.Box) || Town.TownNpcs.ChatOpen))
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
                c.Renderer.sprite = FrameSprite(Frame(c));
                // The bottom tile is 18 px tall: its last 2 px sink into the ground.
                c.Renderer.transform.localPosition = new Vector3(c.Local.x, c.Local.y - 2f * Px, Z - c.Region.Root.position.z);
            }
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
            foreach (var c in chests) list.Add(new Saved { Scene = c.Scene, Local = c.Local, Items = (ItemStack[])c.Items.Clone(), Name = c.Name });
            return list;
        }

        public static void Load(IEnumerable<Saved> saved)
        {
            Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                var c = new Chest { Scene = s.Scene, Local = s.Local, Name = s.Name ?? "" };
                if (s.Items != null)
                    for (int i = 0; i < Capacity && i < s.Items.Length; i++) c.Items[i] = s.Items[i];
                chests.Add(c);
            }
        }
    }
}
