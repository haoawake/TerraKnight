using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hallownest's currencies as items, the ones the coin column takes (Inventory.FitsCoinSlot: any of them in any of its
    /// four slots): the Geo, the money (GeoWallet), and the Dream Essence (EssenceWallet). Each is one stack up to
    /// int.MaxValue; chests, the Piggy Bank and the other storages take them, and Deposit All and Quick Stack put them onto
    /// the stacks of the same already there (Chests.MoveCoins). Only the Geo is spent in shops; neither is sold.
    /// </summary>
    internal static class Currencies
    {
        /// <summary>Every currency, in the order of their coin slots in a coin column that started empty.</summary>
        public static readonly ItemType[] All = { ItemType.Geo, ItemType.DreamEssence };

        public static bool Is(ItemType t) => t == ItemType.Geo || t == ItemType.DreamEssence;

        /// <summary>How much of a currency the player holds: the bag, the ammo and coin slots, the cursor.</summary>
        public static long Held(Inventory inv, ItemType t)
        {
            long n = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
            if (!inv.Mouse.IsEmpty && inv.Mouse.Type == t) n += inv.Mouse.Count;
            return n;
        }

        /// <summary>How much of a currency is in the player's storages: the Piggy Bank's, the Safe's, the Defender's Forge's, the Void Vault's.</summary>
        public static long Banked(Inventory inv, ItemType t)
        {
            long n = 0;
            for (int k = 0; k < 4; k++)
                foreach (var s in inv.BankOf(k)) if (!s.IsEmpty && s.Type == t) n += s.Count;
            return n;
        }

        /// <summary>A currency taken from the storages in their order (Player.BuyItem: bank, bank2, bank3, bank4). Returns how much.</summary>
        public static long TakeBanked(Inventory inv, ItemType t, long n)
        {
            long taken = 0;
            var none = default(ItemStack);
            for (int k = 0; k < 4 && taken < n; k++) taken += Take(inv.BankOf(k), ref none, t, n - taken);
            return taken;
        }

        /// <summary>A currency out of some slots (and the cursor), the smallest stacks first; how much was taken.</summary>
        public static long Take(ItemStack[] slots, ref ItemStack mouse, ItemType t, long n)
        {
            long taken = 0;
            while (n > 0)
            {
                int best = -1;
                for (int i = 0; i < slots.Length; i++)
                    if (!slots[i].IsEmpty && slots[i].Type == t && (best < 0 || slots[i].Count < slots[best].Count)) best = i;
                if (best < 0) break;
                int part = (int)Mathf.Min(n, slots[best].Count);
                slots[best].Count -= part;
                if (slots[best].Count <= 0) slots[best] = default;
                n -= part;
                taken += part;
            }
            if (n > 0 && !mouse.IsEmpty && mouse.Type == t)
            {
                int part = (int)Mathf.Min(n, mouse.Count);
                mouse.Count -= part;
                if (mouse.Count <= 0) mouse = default;
                taken += part;
            }
            return taken;
        }

        /// <summary>A currency into the coin slots and the bag (Inventory.Pickup); what doesn't fit falls at the player's feet.</summary>
        public static void Give(TerraMod mod, ItemType t, long n)
        {
            var hero = HeroController.SilentInstance;
            while (n > 0)
            {
                int part = (int)Mathf.Min(n, int.MaxValue);
                n -= part;
                int left = mod.Inventory.Pickup(t, part);
                if (left > 0 && hero != null) WorldItems.Drop(ItemStack.Of(t, left), hero.GetComponent<BoxCollider2D>().bounds.center);
            }
        }

        public static int Clamp(long n) => n > int.MaxValue ? int.MaxValue : n < 0 ? 0 : (int)n;
    }

    /// <summary>
    /// The Dream Essence as an item, as the Geo is (GeoWallet): the essence the player holds (the bag, the coin slots, the
    /// cursor) and has put in the Piggy Bank, the Safe, the Defender's Forge or the Void Vault is the game's own count
    /// (PlayerData.dreamOrbs). Essence the game adds (a Whispering Root's orbs, an enemy's, a warrior's dream, a ghost
    /// dream-nailed, a dream boss's orbs) comes into the coin slots as the item; essence it takes (the one a Dream Gate warp
    /// costs, Dream Return's "DG Warp In") leaves the bag first and then the storages; what it reads (the Seer's rewards,
    /// the Dream Gate's check, the HUD's and the inventory's count, the Attunement achievement) is all of that. Essence in a
    /// chest or on the ground stays the player's as an item and counts again once it is back in the bag or a storage. A death
    /// leaves it as it is, as the game does. A save from before this has its essence turned into the item the first time it is
    /// played. The picture is the game's own: the essence the HUD shows while it is collected.
    /// </summary>
    internal static class EssenceWallet
    {
        public const string Icon = "HK_DreamEssence";
        static bool primed;
        static int lastPd;

        /// <summary>The save's essence already lives in the bag as the item (from the mod's save).</summary>
        public static bool Migrated;

        public static int DevGiven, DevTaken;

        public static void Reset(bool migrated)
        {
            primed = false;
            Migrated = migrated;
        }

        public static long Held(Inventory inv) => Currencies.Held(inv, ItemType.DreamEssence);
        public static long Banked(Inventory inv) => Currencies.Banked(inv, ItemType.DreamEssence);

        /// <summary>Each frame in play: the game's essence and the essence held and banked equal.</summary>
        public static void Tick(TerraMod mod)
        {
            EnsureIcon();
            var pd = PlayerData.instance;
            if (pd == null || mod == null || mod.Creative) return;
            var inv = mod.Inventory;
            if (!primed)
            {
                if (!Migrated)
                {
                    if (pd.dreamOrbs > 0) Currencies.Give(mod, ItemType.DreamEssence, pd.dreamOrbs);
                    Migrated = true;
                    Plugin.Log.LogInfo($"[梦境精华] 老存档的 {pd.dreamOrbs} 精华换成背包里的梦境精华");
                }
                inv.GatherCoins();
                pd.dreamOrbs = Currencies.Clamp(Held(inv) + Banked(inv));
                lastPd = pd.dreamOrbs;
                primed = true;
                return;
            }
            int d = pd.dreamOrbs - lastPd;
            if (d > 0)
            {
                Currencies.Give(mod, ItemType.DreamEssence, d);
                DevGiven += d;
            }
            else if (d < 0)
            {
                long left = -d - Currencies.Take(inv.Slots, ref inv.Mouse, ItemType.DreamEssence, -d);
                if (left > 0) Currencies.TakeBanked(inv, ItemType.DreamEssence, left);
                DevTaken -= d;
            }
            int total = Currencies.Clamp(Held(inv) + Banked(inv));
            if (pd.dreamOrbs != total) pd.dreamOrbs = total;
            lastPd = pd.dreamOrbs;
        }

        // ------------------------------------------------------------------ its picture

        static bool iconFromGame;
        static float nextLook;
        static int looks;
        public static string DevIconFrom = "";

        /// <summary>
        /// The game's essence made a small texture under <see cref="Icon"/>: the HUD's (Hud Canvas/Extras/Dream Nail/Sprite,
        /// the essence beside the count as it is collected, as its animation leaves it), else a Whispering Root's orb;
        /// Terraria's Fallen Star until then.
        /// </summary>
        static void EnsureIcon()
        {
            if (iconFromGame) return;
            if (!TerrariaAssets.Has(Icon)) TerrariaAssets.Put(Icon, TerrariaAssets.Texture("Item_75"));
            // A look every five seconds for two minutes; the stand-in stays if the game's isn't loaded by then.
            if (Time.unscaledTime < nextLook || looks >= 24) return;
            nextLook = Time.unscaledTime + 5f;
            looks++;
            Texture2D got = null;
            var cams = GameCameras.instance;
            var hud = cams != null ? cams.hudCanvas : null;
            var at = hud != null ? hud.transform.Find("Extras/Dream Nail/Sprite") : null;
            var tk = at != null ? at.GetComponent<tk2dBaseSprite>() : null;
            if (tk != null)
            {
                // The counter's resting look: the last frame of its "in" animation (the frame shown now while it is hidden).
                var def = tk.GetCurrentSpriteDef();
                var clip = at.GetComponent<tk2dSpriteAnimator>()?.DefaultClip;
                var last = clip != null && clip.frames != null && clip.frames.Length > 0 ? clip.frames[clip.frames.Length - 1] : null;
                var defs = last?.spriteCollection?.spriteDefinitions;
                if (defs != null && last.spriteId >= 0 && last.spriteId < defs.Length && defs[last.spriteId] != null) def = defs[last.spriteId];
                got = GeoItem.CaptureTk2d(def, 28, Icon);
                if (got != null) DevIconFrom = $"HUD {def?.name}（{clip?.name}）";
            }
            if (got == null)
                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s == null || s.name != "Dream_Orb_smaller0000") continue;
                    got = DreamNailItem.Capture(s, 28, Icon);
                    if (got != null) DevIconFrom = s.name;
                    break;
                }
            if (got == null) return;
            TerrariaAssets.Put(Icon, got);
            iconFromGame = true;
            Plugin.Log.LogInfo($"[梦境精华] 图标用原版的 {DevIconFrom}（{got.width}x{got.height}）");
        }
    }
}
