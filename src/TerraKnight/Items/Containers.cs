using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class Inventory
    {
        /// <summary>
        /// The Banner Case's slots and the Potion Box's (Containers): pages of a chest's 40, four and three of them; the
        /// Auto-Sell Bag's list, two pages of entries (one of each item listed, no real stack).
        /// </summary>
        public const int ContainerPageSlots = 40, CasePages = 4, BoxPages = 3, SellPages = 2;
        public readonly ItemStack[][] BannerCase = NewPages(CasePages), PotionBox = NewPages(BoxPages), SellList = NewPages(SellPages);

        static ItemStack[][] NewPages(int n)
        {
            var pages = new ItemStack[n][];
            for (int i = 0; i < n; i++) pages[i] = new ItemStack[ContainerPageSlots];
            return pages;
        }

        /// <summary>A container's pages by its storage number (Containers.CaseKind, BoxKind), null for any other.</summary>
        public ItemStack[][] ContainerOf(int kind) =>
            kind == Containers.CaseKind ? BannerCase : kind == Containers.BoxKind ? PotionBox : kind == Containers.SellKind ? SellList : null;

        void ClearContainers()
        {
            DropBlacklist.Clear();
            foreach (var page in BannerCase) System.Array.Clear(page, 0, page.Length);
            foreach (var page in PotionBox) System.Array.Clear(page, 0, page.Length);
            foreach (var page in SellList) System.Array.Clear(page, 0, page.Length);
        }
    }

    /// <summary>
    /// Two carried storages after the Void Bag's pattern (Player.GetItem's void bag, bank4): the Banner Case holds banners
    /// (Banners) and the Potion Box potions, each its own slots, opened where the player is by using the item or a right
    /// click on it in the bag (the chest panel, a page of 40 at a time). Carried, a banner or a potion picked up goes into
    /// it first; the Banner Case's banners work as the bag's do; the Potion Box's potions serve Quick Heal, Quick Mana and
    /// Quick Buff after the bag's, as the Void Bag's do (Player.QuickHeal_GetItemToUse Pl:5549-5555, QuickMana_GetItemToUse
    /// Pl:5630-5636, QuickBuff Pl:5814-5832). A buff potion's stack of 20 or more in the Potion Box, favourited (Alt+click),
    /// keeps its buff on without being drunk, shown with no time as the furniture's buffs are, for as long as it stays so
    /// and the box is carried or lies in the Piggy Bank (the Banner Case's banners too: InEffect); a flask holds one imbue
    /// at a time (Player.AddBuff's imbues), the luck potions their own luck.
    /// The Auto-Sell Bag (not Terraria's) opens the same way to a list: a stack put into it lists its item and is sold
    /// there and then, an entry clicked comes off the list; carried, whatever on the list is picked up is sold as it is,
    /// for what a shop pays (Shop.SellValue, a prefix's and a forge's worth in it, the quick sell's price) into the wallet.
    /// </summary>
    internal static class Containers
    {
        /// <summary>The storages' numbers beside the Piggy Bank's 0 to the Void Vault's 3 (Chests' BankKind); the sell list's.</summary>
        public const int CaseKind = 4, BoxKind = 5, SellKind = 6;
        /// <summary>Potions of a kind in the box before it can be favourited to keep its buff on.</summary>
        public const int FavoriteAt = 20;

        public static bool IsKind(int kind) => kind == CaseKind || kind == BoxKind || kind == SellKind;
        public static bool IsContainer(ItemType t) => t == ItemType.BannerCase || t == ItemType.PotionBox || t == ItemType.AutoSellBag;
        public static int KindOf(ItemType t) => t == ItemType.BannerCase ? CaseKind : t == ItemType.PotionBox ? BoxKind : t == ItemType.AutoSellBag ? SellKind : 0;
        public static ItemType ItemOf(int kind) => kind == CaseKind ? ItemType.BannerCase : kind == BoxKind ? ItemType.PotionBox : kind == SellKind ? ItemType.AutoSellBag : ItemType.None;

        /// <summary>The item is in the bag, the hotbar or the ammo slots (useVoidBag's inventory[0..57]).</summary>
        public static bool Carried(Inventory inv, ItemType t)
        {
            if (inv == null) return false;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) return true;
            return false;
        }

        /// <summary>
        /// The container works for its effects (the Banner Case's banners, the Potion Box's held buffs): carried, or lying in
        /// the Piggy Bank (Inventory.Bank). Picking up into it, quick use from it and selling on pickup want it carried.
        /// </summary>
        public static bool InEffect(Inventory inv, ItemType t)
        {
            if (Carried(inv, t)) return true;
            if (inv == null) return false;
            foreach (var s in inv.Bank) if (!s.IsEmpty && s.Type == t) return true;
            return false;
        }

        /// <summary>ItemID.Sets.IsFood (ItemID.cs:264): the food and drinks, Ale and Sake among them, which are no potions.</summary>
        static readonly HashSet<int> food = new HashSet<int>
        {
            353, 357, 1787, 1911, 1912, 1919, 1920, 2266, 2267, 2268, 2425, 2426, 2427, 3195, 3532, 4009, 4010, 4011, 4012, 4013, 4014, 4015,
            4016, 4017, 4018, 4019, 4020, 4021, 4022, 4023, 4024, 4025, 4026, 4027, 4028, 4029, 4030, 4031, 4032, 4033, 4034, 4035, 4036, 4037,
            967, 969, 4282, 4283, 4284, 4285, 4286, 4287, 4288, 4289, 4290, 4291, 4292, 4293, 4294, 4295, 4296, 4297, 4403, 4411, 4614, 4615,
            4616, 4617, 4618, 4619, 4620, 4621, 4622, 4623, 4624, 4625, 5009, 5042, 5041, 5092, 5093, 5275, 5277, 5278, 5537, 5645,
        };

        static int TerrariaId(ItemDef d) => d.Texture != null && d.Texture.StartsWith("Item_") && int.TryParse(d.Texture.Substring(5), out int id) ? id : 0;

        /// <summary>
        /// A potion: drunk and used up for life, mana or a buff (not food, not a fed state), or one of the teleporting potions.
        /// The Mushroom (a crafting material first) and what is taken on touch (hearts, stars) are not.
        /// </summary>
        public static bool IsPotion(ItemDef d)
        {
            if (d == null || !d.Consumable || d.MaxStack <= 1 || d.IsWeapon || d.LifeCrystal || d.ManaCrystal || d.Boost != PermanentBoost.None || d.PetBuff > 0) return false;
            if (d.Type == ItemType.Heart || d.Type == ItemType.Star || d.Type == ItemType.Mushroom) return false;
            if (food.Contains(TerrariaId(d)) || d.BuffType == Player.PotionBuffs.WellFed || d.BuffType == Player.PotionBuffs.PlentySatisfied || d.BuffType == Player.PotionBuffs.ExquisitelyStuffed) return false;
            return d.HealLife > 0 || d.HealMana > 0 || d.BuffType > 0 || d.Type == ItemType.RecallPotion || d.Type == ItemType.TeleportationPotion
                || d.Type == ItemType.WormholePotion || d.Type == ItemType.PotionOfReturn;
        }

        /// <summary>A buff potion (a flask too): its buff and nothing else, no healing or mana, and no debuff.</summary>
        public static bool IsBuffPotion(ItemDef d) => IsPotion(d) && d.BuffType > 0 && d.HealLife <= 0 && d.HealMana <= 0 && !Player.PlayerBuffs.IsDebuff(d.BuffType);

        /// <summary>What a container takes: the Banner Case banners, the Potion Box potions.</summary>
        public static bool Fits(int kind, ItemDef d) => d != null && (kind == CaseKind ? Banners.Is(d.Type) : kind == BoxKind ? IsPotion(d) : kind == SellKind && Listable(ItemStack.Of(d.Type), out _));

        /// <summary>The container a picked-up item goes into, 0 none.</summary>
        static int KindFor(ItemDef d) => Banners.Is(d.Type) ? CaseKind : IsPotion(d) ? BoxKind : 0;

        public static int DevStored;

        /// <summary>A stack picked up: into its container when that is carried (its stacks of it first, then the first empty slot). Returns how many are left.</summary>
        public static int Store(Inventory inv, ItemStack s, int count)
        {
            if (inv == null || s.IsEmpty || count <= 0 || s.Prefix != 0 || s.Forge != 0) return count;
            var def = s.Def;
            int kind = KindFor(def);
            if (kind == 0 || !Carried(inv, ItemOf(kind))) return count;
            var pages = inv.ContainerOf(kind);
            int max = def.MaxStack;
            foreach (var page in pages)
                for (int i = 0; i < page.Length && count > 0; i++)
                {
                    if (page[i].IsEmpty || page[i].Type != s.Type || page[i].Prefix != 0 || page[i].Forge != 0 || page[i].Count >= max) continue;
                    int m = System.Math.Min(count, max - page[i].Count);
                    page[i].Count += m;
                    count -= m;
                    DevStored += m;
                }
            foreach (var page in pages)
                for (int i = 0; i < page.Length && count > 0; i++)
                {
                    if (!page[i].IsEmpty) continue;
                    int m = System.Math.Min(count, max);
                    page[i] = ItemStack.Of(s.Type, m);
                    count -= m;
                    DevStored += m;
                }
            return count;
        }

        /// <summary>Room for some of an item in its carried container, or it is sold on pickup (the magnet pulls it then, a full bag or not).</summary>
        public static bool HasRoom(Inventory inv, ItemType type)
        {
            if (Sells(inv, type)) return true;
            var def = ItemDb.Get(type);
            int kind = KindFor(def);
            if (kind == 0 || !Carried(inv, ItemOf(kind))) return false;
            foreach (var page in inv.ContainerOf(kind))
                foreach (var v in page)
                    if (v.IsEmpty || (v.Type == type && v.Prefix == 0 && v.Forge == 0 && v.Count < def.MaxStack)) return true;
            return false;
        }

        /// <summary>The container's panel opened (or shut, if it is the one open), where the player is.</summary>
        public static bool Toggle(ItemType t)
        {
            int kind = KindOf(t);
            if (kind == 0) return false;
            World.Chests.ToggleContainer(kind, t);
            return true;
        }

        /// <summary>A container's count of an item, over all its pages.</summary>
        public static int CountIn(Inventory inv, int kind, ItemType type)
        {
            int n = 0;
            var pages = inv?.ContainerOf(kind);
            if (pages == null) return 0;
            foreach (var page in pages) foreach (var s in page) if (!s.IsEmpty && s.Type == type) n += s.Count;
            return n;
        }

        /// <summary>Alt+click on a container's slot: a Potion Box's buff potion of 20 or more favourited, any favourite let go.</summary>
        public static bool CanFavorite(int kind, ItemStack s) => kind == BoxKind && !s.IsEmpty && (s.Favorite || (IsBuffPotion(s.Def) && s.Count >= FavoriteAt));

        // ------------------------------------------------------------------ the Auto-Sell Bag

        /// <summary>
        /// Whether an item may go on the list: a shop pays something for it (Shop.SellValue) and quick sell would sell it (no
        /// favourite, nothing permanent: the nails, the Dream Nail, Hallownest's key items; no currency, Geo or Dream Essence); nor a quest fish, a
        /// heart or a star (taken on touch), or the bag itself. Why not otherwise.
        /// </summary>
        public static bool Listable(ItemStack s, out string why, bool requireValue = true)
        {
            why = null;
            if (s.IsEmpty) return false;
            var d = s.Def;
            if (s.Favorite) why = "收藏的物品不会卖出";
            else if (d.Permanent || d.HkItem || d.GameTool || d.QuestFish || Currencies.Is(s.Type) || s.Type == ItemType.Heart || s.Type == ItemType.Star
                || s.Type == ItemType.AutoSellBag || requireValue && Shop.SellValue(ItemStack.Of(s.Type)) <= 0) why = "这件不能加入自动出售名单";
            return why == null;
        }

        /// <summary>The item is on the Auto-Sell Bag's list.</summary>
        public static bool Listed(Inventory inv, ItemType t)
        {
            if (inv == null || t == ItemType.None) return false;
            foreach (var page in inv.SellList) foreach (var e in page) if (!e.IsEmpty && e.Type == t) return true;
            return false;
        }

        /// <summary>A pickup of the item is sold: it is listed and the bag carried.</summary>
        public static bool Sells(Inventory inv, ItemType t) => inv != null &&
            (inv.DropBlacklist.Contains(t) || Listed(inv, t) && Carried(inv, ItemType.AutoSellBag));

        public static int DevAutoSold, DevListedSold;
        public static long DevAutoSoldCopper;

        /// <summary>
        /// A stack being picked up, sold instead when it is listed and the bag carried: what a shop pays for it
        /// (Shop.SellValue, its prefix's and forge's worth in it) earned as a sale does (Shop.Earn: whole geo into the
        /// wallet), the geo shown rising from the player. True when sold.
        /// </summary>
        public static bool AutoSell(Inventory inv, ItemStack s, Vector2 at)
        {
            if (!Sells(inv, s.Type) || !Listable(s, out _, requireValue: !inv.DropBlacklist.Contains(s.Type))) return false;
            long money = Shop.SellValue(s);
            inv.RecordCollection(s.Type);
            Pay(money, at);
            DevAutoSold += s.Count;
            DevAutoSoldCopper += money;
            return true;
        }

        /// <summary>A sale's money into the wallet: the whole geo it came to shown as a pickup (PopupText), a fraction as a number.</summary>
        static void Pay(long money, Vector2 at)
        {
            long before = Shop.Purse;
            Shop.Earn(money);
            long geo = Shop.Purse / 100 - before / 100;
            if (geo > 0) UI.PopupText.Show(ItemDb.Get(ItemType.Geo), (int)System.Math.Min(geo, int.MaxValue), at);
            else if (money > 0) UI.CombatText.Show(at + Vector2.up * 0.9f, "+" + Shop.Money(money), UI.CombatText.Pickup);
            TerraMod.Instance?.PlaySound(money > 0 ? "Coins" : "Grab", 0.7f);
        }

        /// <summary>
        /// A stack put into the list (into the clicked entry when it is free, else the first free one): its item listed,
        /// once, and the stack sold there and then. False with why when it can't be listed or the list is full.
        /// </summary>
        public static bool ListAndSell(Inventory inv, ref ItemStack s, ItemStack[] page, int slot, Vector2 at, out string why)
        {
            if (!Listable(s, out why)) return false;
            if (!Listed(inv, s.Type))
            {
                if (page != null && slot >= 0 && slot < page.Length && page[slot].IsEmpty) page[slot] = ItemStack.Of(s.Type);
                else if (!AddEntry(inv, s.Type))
                {
                    why = "自动出售名单已满";
                    return false;
                }
            }
            Pay(Shop.SellValue(s), at);
            DevListedSold += s.Count;
            s = default;
            return true;
        }

        static bool AddEntry(Inventory inv, ItemType t)
        {
            foreach (var page in inv.SellList)
                for (int i = 0; i < page.Length; i++)
                    if (page[i].IsEmpty)
                    {
                        page[i] = ItemStack.Of(t);
                        return true;
                    }
            return false;
        }

        /// <summary>An item off the list (nothing comes back).</summary>
        public static bool Unlist(Inventory inv, ItemType t)
        {
            bool any = false;
            foreach (var page in inv.SellList)
                for (int i = 0; i < page.Length; i++)
                    if (!page[i].IsEmpty && page[i].Type == t)
                    {
                        page[i] = default;
                        any = true;
                    }
            return any;
        }

        // ------------------------------------------------------------------ the Potion Box's buffs

        /// <summary>The flasks' buffs (WeaponImbue*: Player.AddBuff keeps one of them).</summary>
        static readonly int[] imbues = { 71, 73, 74, 75, 76, 77, 78, 79 };

        /// <summary>The ticks a held buff is kept at: as the carried furniture's, past a frame's ticks.</summary>
        const int HoldTicks = 10;

        static readonly Dictionary<int, int> held = new Dictionary<int, int>();
        static readonly Dictionary<int, (int Ticks, float Since)> drunk = new Dictionary<int, (int, float)>();
        static readonly Dictionary<int, int> now = new Dictionary<int, int>();
        static readonly List<int> order = new List<int>();

        /// <summary>The buff is kept on by a favourited stack in the Potion Box (shown with no time).</summary>
        public static bool Holds(int buff) => held.ContainsKey(buff);
        public static int HeldCount => held.Count;

        /// <summary>
        /// Each frame: the buffs of the Potion Box's favourited stacks of 20 or more kept on (the box carried); one let go
        /// goes, but for what a drink of it still had left. The Lucky buff is held at its potion's own time, the luck it gives
        /// (PotionBuffs: by the time left); the others a few ticks over and over.
        /// </summary>
        public static void Tick(TerraMod mod)
        {
            var inv = mod?.Inventory;
            if (inv == null) return;
            now.Clear();
            order.Clear();
            int imbue = 0;
            if (InEffect(inv, ItemType.PotionBox))
                foreach (var page in inv.PotionBox)
                    foreach (var s in page)
                    {
                        if (s.IsEmpty || !s.Favorite || s.Count < FavoriteAt) continue;
                        var d = s.Def;
                        if (!IsBuffPotion(d)) continue;
                        int b = d.BuffType;
                        if (System.Array.IndexOf(imbues, b) >= 0)
                        {
                            if (imbue != 0 && imbue != b) continue;
                            imbue = b;
                        }
                        int ticks = b == Player.PotionBuffs.Lucky ? Mathf.Max(HoldTicks, d.BuffTime) : HoldTicks;
                        if (now.TryGetValue(b, out int have)) { if (ticks > have) now[b] = ticks; }
                        else
                        {
                            now[b] = ticks;
                            order.Add(b);
                        }
                    }
            List<int> gone = null;
            foreach (var kv in held) if (!now.ContainsKey(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null)
                foreach (int b in gone)
                {
                    int was = held[b];
                    held.Remove(b);
                    if (was <= HoldTicks) { drunk.Remove(b); continue; }
                    int left = drunk.TryGetValue(b, out var dr) ? dr.Ticks - Mathf.FloorToInt((Time.time - dr.Since) * 60f) : 0;
                    drunk.Remove(b);
                    Player.PlayerBuffs.Remove(b);
                    if (left > 0) Player.PlayerBuffs.Add(b, left);
                }
            foreach (int b in order)
            {
                int ticks = now[b];
                if (!held.ContainsKey(b)) drunk[b] = (Player.PlayerBuffs.TimeLeft(b), Time.time);
                held[b] = ticks;
                if (b == imbue)
                    foreach (int other in imbues) if (other != b && Player.PlayerBuffs.Has(other)) Player.PlayerBuffs.Remove(other);
                Player.PlayerBuffs.Add(b, ticks);
            }
        }

        // ------------------------------------------------------------------ quick use

        static int Strength(ItemDef d) => d.HealLife + d.HealMana;

        /// <summary>
        /// Quick Heal or Quick Mana with the Potion Box carried: its strongest potion of the kind, when it beats the bag's
        /// best (or the bag has none), drunk and used up. True when the box's was the one tried.
        /// </summary>
        public static bool QuickUse(Inventory inv, System.Func<ItemDef, bool> kind, ItemDef bagBest, System.Func<ItemDef, bool> drink)
        {
            if (!Carried(inv, ItemType.PotionBox)) return false;
            int bp = -1, bi = -1;
            for (int p = 0; p < inv.PotionBox.Length; p++)
                for (int i = 0; i < inv.PotionBox[p].Length; i++)
                {
                    var s = inv.PotionBox[p][i];
                    if (s.IsEmpty || !kind(s.Def)) continue;
                    if (bp < 0 || Strength(s.Def) > Strength(inv.PotionBox[bp][bi].Def)) { bp = p; bi = i; }
                }
            if (bp < 0) return false;
            var def = inv.PotionBox[bp][bi].Def;
            if (bagBest != null && Strength(bagBest) >= Strength(def)) return false;
            bool done = drink(def);
            if (done) Consume(inv, ref inv.PotionBox[bp][bi]);
            TerraMod.DevLastQuickUse = $"药水箱第 {bp + 1} 页第 {bi} 格 {def.Name}，喝下 {done}";
            return true;
        }

        static void Consume(Inventory inv, ref ItemStack s)
        {
            if (inv.Infinite) return;
            s.Count--;
            if (s.Count <= 0) s = default;
        }

        // ------------------------------------------------------------------ text

        static readonly string[] caseTip =
        {
            "存放旗帜：拾取的旗帜会自动收进旗帜盒",
            "带在身上或放进猪猪存钱罐时，盒里的旗帜和背包里的一样生效",
            "使用或在背包里右键点击打开",
        };

        static readonly string[] boxTip =
        {
            "存放药水：拾取的药水会自动收进药水箱",
            "快速治疗、快速魔力和快速增益也会用箱里的药水",
            "同种增益药水满 20 瓶后可收藏（Alt+左键）：收藏后这种增益永久生效，不消耗药水，取消收藏即失效",
            "取消收藏、少于 20 瓶，或药水箱既不在身上也不在猪猪存钱罐里时失效",
            "使用或在背包里右键点击打开",
        };

        static readonly string[] sellTip =
        {
            "把物品放进袋里：加入自动出售名单，放进去的这组当场按商店价卖掉",
            "带在身上时，捡到名单上的物品会自动卖掉，吉欧直接进钱包",
            "点名单里的物品把它移出名单",
            "打开时：Shift+左键加入名单并出售，Ctrl+左键仅出售，可在快捷商店买回",
            "使用或在背包里右键点击打开",
        };

        static string[] TipOf(ItemType t) => t == ItemType.BannerCase ? caseTip : t == ItemType.PotionBox ? boxTip : sellTip;

        internal static string ZhTooltip(ItemType t) => string.Join("\n", TipOf(t));

        /// <summary>A banner's or a container's name and tooltip in the language now (ItemDb.Relocalize); false for anything else.</summary>
        public static bool Localize(ItemDef d)
        {
            if (Banners.Is(d.Type))
            {
                Banners.Localize(d);
                return true;
            }
            if (!IsContainer(d.Type)) return false;
            d.Name = Lang.IsSource ? d.ZhName : Lang.T(d.ZhName);
            var lines = TipOf(d.Type);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Lang.IsSource ? lines[i] : Lang.T(lines[i]));
            }
            d.Tooltip = sb.ToString();
            return true;
        }
    }

    internal static partial class ItemDb
    {
        /// <summary>
        /// The Banner Case, the Potion Box and the Auto-Sell Bag (Containers: the Void Bag's use, useStyle 1 for 20 ticks),
        /// and every record's banner (Banners). Their pictures from 空洞骑士怪物旗帜素材包's FlagCase.png and
        /// 空洞骑士药水箱素材包 (32x32); the bag's Terraria's Herb Bag.
        /// </summary>
        static void AddContainers(List<ItemDef> list)
        {
            ItemDef Box(ItemType t, string name, string texture) => new ItemDef
            {
                Type = t, Name = name, ZhName = name, Texture = Relics.TexturePrefix + texture, Rare = 1, UseAnimation = 20 * Tick, UseTime = 20 * Tick,
                AutoReuse = false, NoMelee = true, UseSound = null, Tooltip = Containers.ZhTooltip(t),
                Stage = 0, Source = ItemSource.Craft, Value = 10000, Price = 100,
            };
            list.Add(Box(ItemType.BannerCase, "旗帜盒", "banner_case"));
            list.Add(Box(ItemType.PotionBox, "药水箱", "potion_box"));
            // An original sale-pouch sprite; leave Terraria's Herb Bag artwork intact.
            var sell = Box(ItemType.AutoSellBag, "自动出售袋", null);
            sell.Texture = Relics.TexturePrefix + "auto_sell_bag";
            list.Add(sell);
            Banners.AddItems(list);
        }

        /// <summary>
        /// Their recipes: a chest lined with Silk at a Loom (Terraria's cloth banners are Silk at a Loom: the Red Banner, 3
        /// Silk at tile 86, Recipe.cs:8851-8854), a chest fitted with Bottles at a placed bottle.
        /// </summary>
        internal static IEnumerable<Recipe> ContainerRecipes()
        {
            yield return new Recipe(ItemType.BannerCase, 1, "织布机", 0, (ItemType.Chest, 1), (ItemType.Silk, 10));
            yield return new Recipe(ItemType.PotionBox, 1, "玻璃瓶", 0, (ItemType.Chest, 1), (ItemType.Bottle, 10));
            // The Auto-Sell Bag: a Silk bag sewn round a Piggy Bank at a Loom.
            yield return new Recipe(ItemType.AutoSellBag, 1, "织布机", 0, (ItemType.Silk, 10), (ItemType.PiggyBank, 1));
        }
    }
}
