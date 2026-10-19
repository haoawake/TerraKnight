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

        public static long Held(Inventory inv) => Currencies.Held(inv, ItemType.Geo);

        /// <summary>The geo in the player's storages: the Piggy Bank's, the Safe's, the Defender's Forge's, the Void Vault's (all spent in shops).</summary>
        public static long Banked(Inventory inv) => Currencies.Banked(inv, ItemType.Geo);

        /// <summary>Geo taken from the storages in their order (Player.BuyItem: bank, bank2, bank3, bank4). Returns how much.</summary>
        static long TakeBanked(Inventory inv, long geo) => Currencies.TakeBanked(inv, ItemType.Geo, geo);

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
                inv.GatherCoins();
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
                if (left > 0 && !(hero != null && hero.cState.dead)) TakeBanked(inv, left);
            }
            long total = Held(inv) + Banked(inv);
            if (pd.geo != Clamp(total)) SetGameGeo(pd, total);
            lastPd = pd.geo;
        }

        /// <summary>A geo piece being picked up (GeoControl's trigger, its AddGeo inside).</summary>
        static int collecting;
        public static int DevPopped;

        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(GeoControl), "OnTriggerEnter2D")]
        static void PickupStart() => collecting++;

        [HarmonyLib.HarmonyFinalizer]
        [HarmonyLib.HarmonyPatch(typeof(GeoControl), "OnTriggerEnter2D")]
        static void PickupEnd() => collecting = Mathf.Max(0, collecting - 1);

        static readonly HarmonyLib.AccessTools.FieldRef<GeoControl, bool> drawnIn = HarmonyLib.AccessTools.FieldRefAccess<GeoControl, bool>("attracted");

        /// <summary>A geo piece drawn in (a money accessory's pull, the Gatherer's) passes over the acid on its way to the player instead of dissolving.</summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(GeoControl), "OnTriggerEnter2D")]
        static bool NotInAcid(GeoControl __instance, Collider2D collision) => collision == null || !collision.CompareTag("Acid") || !drawnIn(__instance);

        /// <summary>A geo piece picked up (small, medium or large): how much, over the player, as Terraria pops a coin's (PopupText, merged with the last).</summary>
        [HarmonyLib.HarmonyPostfix]
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.AddGeo))]
        static void Picked(HeroController __instance, int amount)
        {
            if (collecting <= 0 || amount <= 0 || __instance == null) return;
            UI.PopupText.Show(ItemDb.Get(ItemType.Geo), amount, __instance.GetComponent<BoxCollider2D>().bounds.center);
            DevPopped++;
        }

        /// <summary>
        /// The shade's geo at a death: what the bag carried, not the Piggy Bank's share of the counter. On Master (and
        /// Hollow) the shade keeps half of it and the other half is gone with the death; struck, the shade gives back what
        /// it keeps, and a second death before that leaves the old shade's geo lost (the game's own way).
        /// </summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetInt))]
        static void ShadeGeo(string intName, ref int value)
        {
            var mod = TerraMod.Instance;
            if (intName != "geoPool" || value <= 0 || mod == null || mod.Creative) return;
            value = Clamp(System.Math.Min(value, Held(mod.Inventory)));
            if (!World.GameDifficulty.MasterRules) return;
            int kept = value / 2;
            Plugin.Log.LogInfo($"[吉欧] 大师规则下死亡：阴影保管 {kept}，另外 {value - kept} 吉欧丢失");
            value = kept;
        }

        static int Clamp(long n) => Currencies.Clamp(n);

        static void SetGameGeo(PlayerData pd, long held)
        {
            pd.geo = Clamp(held);
            var hero = HeroController.SilentInstance;
            if (hero != null && hero.geoCounter != null) hero.geoCounter.NewSceneRefresh();
        }

        /// <summary>Geo into the bag; what doesn't fit falls at the player's feet.</summary>
        public static void Give(TerraMod mod, long n) => Currencies.Give(mod, ItemType.Geo, n);

        /// <summary>Geo out of some slots (and the cursor), the smallest stacks first; how much was taken.</summary>
        static long Take(ItemStack[] slots, ref ItemStack mouse, long n) => Currencies.Take(slots, ref mouse, ItemType.Geo, n);

        /// <summary>Pays geo from the bag, then the Piggy Bank; false (nothing taken) when there isn't enough.</summary>
        public static bool Pay(Inventory inv, long geo)
        {
            if (geo <= 0) return true;
            if (Held(inv) + Banked(inv) < geo) return false;
            geo -= Take(inv.Slots, ref inv.Mouse, geo);
            if (geo > 0) TakeBanked(inv, geo);
            return true;
        }
    }
}
