using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Money is an item, as Terraria's coins are: the bag's Geo (one stack holds up to
    /// int.MaxValue) is what the player carries, and it can be dropped, put in a chest or in the
    /// Piggy Bank. The game's own geo (PlayerData.geo: the counter in the corner, the stag's and the
    /// benches' tolls, Sly's shop) is kept equal to all the money the player can spend, the bag's
    /// and the Piggy Bank's (Player.BuyItem spends both): geo the game adds (a pickup, the shade
    /// struck) comes into the bag as the item, geo it takes leaves the bag first and then the bank,
    /// and Geo moved by hand sets its count. A death's shade takes only what the bag carried (the
    /// game's geoPool), the bank's stays. A save from before this has its geo turned into the item
    /// the first time it is played.
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal static class GeoWallet
    {
        static bool primed;
        static int lastPd;

        /// <summary>The save's geo already lives in the bag as the item (from the mod's save).</summary>
        public static bool Migrated;

        public static void Reset(bool migrated)
        {
            primed = false;
            Migrated = migrated;
        }

        public static long Held(Inventory inv)
        {
            long n = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == ItemType.Geo) n += s.Count;
            if (!inv.Mouse.IsEmpty && inv.Mouse.Type == ItemType.Geo) n += inv.Mouse.Count;
            return n;
        }

        public static long Banked(Inventory inv)
        {
            long n = 0;
            foreach (var s in inv.Bank) if (!s.IsEmpty && s.Type == ItemType.Geo) n += s.Count;
            return n;
        }

        /// <summary>Each frame in play: the game's geo and the bag's held equal.</summary>
        public static void Tick(TerraMod mod)
        {
            var pd = PlayerData.instance;
            if (pd == null || mod == null || mod.Creative) return;
            var inv = mod.Inventory;
            if (!primed)
            {
                if (!Migrated)
                {
                    if (pd.geo > 0) Give(mod, pd.geo);
                    Migrated = true;
                    Plugin.Log.LogInfo($"[吉欧] 老存档的 {pd.geo} 吉欧换成背包里的吉欧");
                }
                SetGameGeo(pd, Held(inv) + Banked(inv));
                lastPd = pd.geo;
                primed = true;
                return;
            }
            int dPd = pd.geo - lastPd;
            if (dPd > 0) Give(mod, dPd);
            else if (dPd < 0)
            {
                long left = -dPd - Take(inv.Slots, ref inv.Mouse, -dPd);
                // A death takes only the bag's (its shade carries that much); a toll or a price the bank too.
                var hero = HeroController.SilentInstance;
                if (left > 0 && !(hero != null && hero.cState.dead))
                {
                    var none = default(ItemStack);
                    Take(inv.Bank, ref none, left);
                }
            }
            long total = Held(inv) + Banked(inv);
            if (pd.geo != Clamp(total)) SetGameGeo(pd, total);
            lastPd = pd.geo;
        }

        /// <summary>The shade's geo at a death: what the bag carried, not the Piggy Bank's share of the counter.</summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetInt))]
        static void ShadeGeo(string intName, ref int value)
        {
            var mod = TerraMod.Instance;
            if (intName != "geoPool" || value <= 0 || mod == null || mod.Creative) return;
            value = Clamp(System.Math.Min(value, Held(mod.Inventory)));
        }

        static int Clamp(long n) => n > int.MaxValue ? int.MaxValue : n < 0 ? 0 : (int)n;

        static void SetGameGeo(PlayerData pd, long held)
        {
            pd.geo = Clamp(held);
            var hero = HeroController.SilentInstance;
            if (hero != null && hero.geoCounter != null) hero.geoCounter.NewSceneRefresh();
        }

        /// <summary>Geo into the bag; what doesn't fit falls at the player's feet.</summary>
        public static void Give(TerraMod mod, long n)
        {
            var hero = HeroController.SilentInstance;
            while (n > 0)
            {
                int part = (int)Mathf.Min(n, int.MaxValue);
                n -= part;
                int left = mod.Inventory.Pickup(ItemType.Geo, part);
                if (left > 0 && hero != null) WorldItems.Drop(ItemStack.Of(ItemType.Geo, left), hero.GetComponent<BoxCollider2D>().bounds.center);
            }
        }

        /// <summary>Geo out of some slots (and the cursor), the smallest stacks first; how much was taken.</summary>
        static long Take(ItemStack[] slots, ref ItemStack mouse, long n)
        {
            long taken = 0;
            while (n > 0)
            {
                int best = -1;
                for (int i = 0; i < slots.Length; i++)
                    if (!slots[i].IsEmpty && slots[i].Type == ItemType.Geo && (best < 0 || slots[i].Count < slots[best].Count)) best = i;
                if (best < 0) break;
                int part = (int)Mathf.Min(n, slots[best].Count);
                slots[best].Count -= part;
                if (slots[best].Count <= 0) slots[best] = default;
                n -= part;
                taken += part;
            }
            if (n > 0 && !mouse.IsEmpty && mouse.Type == ItemType.Geo)
            {
                int part = (int)Mathf.Min(n, mouse.Count);
                mouse.Count -= part;
                if (mouse.Count <= 0) mouse = default;
                taken += part;
            }
            return taken;
        }

        /// <summary>Pays geo from the bag, then the Piggy Bank; false (nothing taken) when there isn't enough.</summary>
        public static bool Pay(Inventory inv, long geo)
        {
            if (geo <= 0) return true;
            if (Held(inv) + Banked(inv) < geo) return false;
            geo -= Take(inv.Slots, ref inv.Mouse, geo);
            var none = default(ItemStack);
            if (geo > 0) Take(inv.Bank, ref none, geo);
            return true;
        }
    }
}
