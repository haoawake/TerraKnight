using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hallownest's key items as Terraria items: the Lumafly Lantern, the City Crest, the keys, the Tram Pass, the King's
    /// Brand, the Delicate Flower, the Rancid Eggs, the Pale Ore, the four relics, the Kingsoul and the Void Heart. Each
    /// stands for its PlayerData record (what the game gives, it gives; what the game takes, it takes: a key used, relics
    /// sold to Lemm), drawn with the game's own inventory picture and named as the game names it. They live only in the
    /// hotbar and in their own bag (the Hallownest bag, beside the monster book), and work only in hand (the item held):
    /// the City Gate opens to the crest held, the dark rooms are lit by the lantern held, the tram runs for the pass held,
    /// the Abyss's door to the Birthplace opens to the Kingsoul held, the Siblings and the Radiance know the Void Heart
    /// held. Elsewhere they are only kept.
    /// </summary>
    [HarmonyPatch]
    internal static class HkItems
    {
        public const string TexturePrefix = "HKInv:";
        public const int BagSlots = 20;

        enum Kind { Bool, Int, Royal }

        sealed class Def
        {
            public ItemType Type;
            public Kind Kind;
            /// <summary>The PlayerData record; the royal charm's state (3 the Kingsoul, 4 the Void Heart).</summary>
            public string Flag;
            public int Royal;
            /// <summary>The game's text keys (UI sheet) and the inventory picture's sprite name.</summary>
            public string NameKey, DescKey, Sprite;
            public string Zh;
        }

        static Def B(ItemType t, string flag, string key, string sprite, string zh) => new Def { Type = t, Kind = Kind.Bool, Flag = flag, NameKey = "INV_NAME_" + key, DescKey = "INV_DESC_" + key, Sprite = sprite, Zh = zh };
        static Def I(ItemType t, string flag, string key, string sprite, string zh) => new Def { Type = t, Kind = Kind.Int, Flag = flag, NameKey = "INV_NAME_" + key, DescKey = "INV_DESC_" + key, Sprite = sprite, Zh = zh };
        static Def R(ItemType t, int state, string suffix, string zh) => new Def { Type = t, Kind = Kind.Royal, Royal = state, Flag = "equippedCharm_36", NameKey = "CHARM_NAME_36_" + suffix, DescKey = "CHARM_DESC_36_" + suffix, Sprite = null, Zh = zh };

        static readonly Def[] all =
        {
            B(ItemType.HkLumaflyLantern, "hasLantern", "LANTERN", "Lumafly_Lantern0000", "光蝇灯笼"),
            B(ItemType.HkCityCrest, "hasCityKey", "CITYKEY", "inv_item_city_key", "城市纹章"),
            I(ItemType.HkSimpleKey, "simpleKeys", "SIMPLEKEY", "inv_item__00014_graveyard_key", "简单钥匙"),
            B(ItemType.HkElegantKey, "hasWhiteKey", "WHITEKEY", "Elegant_Key", "典雅的钥匙"),
            B(ItemType.HkLoveKey, "hasLoveKey", "LOVEKEY", "inv_Love_Key", "爱之钥"),
            B(ItemType.HkShopkeepersKey, "hasSlykey", "STOREKEY", "inv_item__0002_storeroom_key", "店主的钥匙"),
            B(ItemType.HkTramPass, "hasTramPass", "TRAM_PASS", "inv_item__0001_tram_pass", "电车通行证"),
            B(ItemType.HkKingsBrand, "hasKingsBrand", "KINGSBRAND", "inv_white_brand", "王之印记"),
            B(ItemType.HkDelicateFlower, "hasXunFlower", "FLOWER", "White_Flower_Full", "娇嫩的花"),
            I(ItemType.HkRancidEgg, "rancidEggs", "RANCIDEGG", "inv_rancid_egg", "腐臭蛋"),
            I(ItemType.HkPaleOre, "ore", "ORE", "inv_item__0009_ore", "苍白矿石"),
            I(ItemType.HkWanderersJournal, "trinket1", "TRINKET1", "inv_item__0013_wanderers-journal", "漫游者日记"),
            I(ItemType.HkHallownestSeal, "trinket2", "TRINKET2", "inv_item__0012_hallownest-seal", "圣巢印章"),
            I(ItemType.HkKingsIdol, "trinket3", "TRINKET3", "inv_item__0010_Kings_idol", "国王神像"),
            I(ItemType.HkArcaneEgg, "trinket4", "TRINKET4", "inv_item__0011_arcane-egg", "神秘蛋"),
            R(ItemType.HkKingsoul, 3, "B", "国王之魂"),
            R(ItemType.HkVoidHeart, 4, "C", "虚空之心"),
        };

        static readonly Dictionary<ItemType, Def> byType = new Dictionary<ItemType, Def>();
        static readonly Dictionary<string, Def> byFlag = new Dictionary<string, Def>();

        static HkItems()
        {
            foreach (var d in all)
            {
                byType[d.Type] = d;
                if (d.Kind != Kind.Royal) byFlag[d.Flag] = d;
            }
        }

        public static bool Is(ItemType t) => byType.ContainsKey(t);
        public static bool Is(ItemStack s) => !s.IsEmpty && byType.ContainsKey(s.Type);

        // ------------------------------------------------------------------ the items

        internal static void AddItems(List<ItemDef> list)
        {
            foreach (var d in all)
                list.Add(new ItemDef
                {
                    Type = d.Type, Name = d.Zh, Texture = TexturePrefix + d.Type, Permanent = true, HkItem = true, UseSound = null, Rare = -11,
                    MaxStack = d.Kind == Kind.Int ? 9999 : 1, Tooltip = "",
                });
        }

        static string Text(string key, string fallback)
        {
            try
            {
                if (TeamCherry.Localization.Language.Has(key, "UI")) return TeamCherry.Localization.Language.Get(key, "UI");
            }
            catch { }
            return fallback;
        }

        /// <summary>The game's own name and description (its UI sheet, in the game's language), and where it works.</summary>
        internal static void Localize(ItemDef def)
        {
            if (!byType.TryGetValue(def.Type, out var d)) return;
            def.Name = Lang.IsSource ? d.Zh : Text(d.NameKey, d.Zh);
            string desc = Text(d.DescKey, "").Replace("<br>", "\n").Trim();
            def.Tooltip = (desc.Length > 0 ? desc + "\n" : "") + Lang.T("拿在手里才生效");
        }

        // ------------------------------------------------------------------ pictures

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> retry = new Dictionary<string, float>();
        static Dictionary<string, Sprite> sprites;

        /// <summary>The item's picture: the game's inventory sprite (the charm list's for the Kingsoul and the Void Heart).</summary>
        internal static Texture2D Texture(string name)
        {
            if (textures.TryGetValue(name, out var tex) && (!retry.TryGetValue(name, out float at) || Time.unscaledTime < at)) return tex;
            string typeName = name.Substring(TexturePrefix.Length);
            Def d = null;
            foreach (var x in all) if (x.Type.ToString() == typeName) d = x;
            Sprite s = null;
            if (d != null)
            {
                if (d.Kind == Kind.Royal) s = RoyalSprite(d.Royal);
                else
                {
                    if (sprites == null || !sprites.ContainsKey(d.Sprite))
                    {
                        sprites = new Dictionary<string, Sprite>();
                        foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>()) if (sp != null && !sprites.ContainsKey(sp.name)) sprites[sp.name] = sp;
                    }
                    sprites.TryGetValue(d.Sprite, out s);
                }
            }
            Texture2D made = s != null ? DreamNailItem.Capture(s, 48, "TerraKnight/" + typeName) : null;
            if (made == null)
            {
                // Not loaded yet (the title menu): a plain stand-in, tried again shortly.
                retry[name] = Time.unscaledTime + 2f;
                if (tex != null) return tex;
                made = Terraria.TerrariaAssets.Texture("Item_327");
            }
            else retry.Remove(name);
            textures[name] = made;
            return made;
        }

        /// <summary>The charm list's picture for charm 36 in a royal state (it draws by PlayerData's state: set for the moment).</summary>
        static Sprite RoyalSprite(int state)
        {
            var pd = PlayerData.instance;
            var list = CharmIconList.Instance;
            if (pd == null || list == null) return null;
            int was = pd.royalCharmState;
            try
            {
                pd.royalCharmState = state;
                return list.GetSprite(36);
            }
            catch { return null; }
            finally { pd.royalCharmState = was; }
        }

        // ------------------------------------------------------------------ what the game has given

        static readonly Dictionary<string, System.Reflection.FieldInfo> fields = new Dictionary<string, System.Reflection.FieldInfo>();

        static object Field(PlayerData pd, string name)
        {
            if (!fields.TryGetValue(name, out var f)) fields[name] = f = AccessTools.Field(typeof(PlayerData), name);
            return f?.GetValue(pd);
        }

        /// <summary>How many of the item the game's record says the Knight has.</summary>
        static int Wanted(Def d, PlayerData pd)
        {
            switch (d.Kind)
            {
                case Kind.Royal:
                    return Field(pd, "gotCharm_36") is bool got && got && pd.royalCharmState == d.Royal ? 1 : 0;
                case Kind.Int:
                    return Field(pd, d.Flag) is int n ? Mathf.Max(0, n) : 0;
                default:
                    if (!(Field(pd, d.Flag) is bool b) || !b) return 0;
                    if (d.Type == ItemType.HkDelicateFlower && Field(pd, "xunFlowerBroken") is bool broken && broken) return 0;
                    return 1;
            }
        }

        static int Count(ItemStack[] xs, ItemType t, int from = 0, int to = -1)
        {
            if (to < 0) to = xs.Length;
            int n = 0;
            for (int i = from; i < to; i++) if (!xs[i].IsEmpty && xs[i].Type == t) n += xs[i].Count;
            return n;
        }

        /// <summary>In hand: the item held (the hotbar's selected slot, or the cursor's with the inventory open).</summary>
        public static bool Held(Inventory inv, ItemType t) => inv != null && !inv.Current.IsEmpty && inv.Current.Type == t;

        public static int DevGiven, DevTaken;

        /// <summary>
        /// Every frame in play: the items as the game's records have them (given into the bag, taken from the bag first);
        /// any that has wandered into the bag's main rows, the ammo slots or an open chest goes back to the Hallownest bag.
        /// </summary>
        public static void Tick(TerraMod mod)
        {
            var pd = PlayerData.instance;
            if (mod == null || pd == null) return;
            var inv = mod.Inventory;
            foreach (var d in all)
            {
                int want = Wanted(d, pd);
                int have = Count(inv.Slots, d.Type) + Count(inv.HkBag, d.Type) + (!inv.Mouse.IsEmpty && inv.Mouse.Type == d.Type ? inv.Mouse.Count : 0);
                if (have < want)
                {
                    int add = want - have;
                    int left = Put(inv.HkBag, d.Type, add);
                    if (left > 0) left = Put(inv.Slots, d.Type, left, Inventory.HotbarSize);
                    if (add - left > 0)
                    {
                        var hero = HeroController.SilentInstance;
                        if (hero != null) UI.PopupText.Show(ItemDb.Get(d.Type), add - left, hero.transform.position);
                        DevGiven += add - left;
                    }
                }
                else if (have > want)
                {
                    int take = have - want;
                    take = Take(inv.HkBag, d.Type, take, inv.HkBag.Length);
                    take = Take(inv.Slots, d.Type, take, inv.Slots.Length);
                    if (take > 0 && !inv.Mouse.IsEmpty && inv.Mouse.Type == d.Type)
                    {
                        int n = Mathf.Min(take, inv.Mouse.Count);
                        inv.Mouse.Count -= n;
                        if (inv.Mouse.Count <= 0) inv.Mouse = default;
                    }
                    DevTaken += have - want;
                }
            }
            // Only the hotbar and the bag: anything else goes back.
            for (int i = Inventory.HotbarSize; i < inv.Slots.Length; i++)
                if (Is(inv.Slots[i])) Return(inv, ref inv.Slots[i]);
            var chest = World.Chests.OpenItems;
            if (chest != null)
                for (int i = 0; i < chest.Length; i++)
                    if (Is(chest[i])) Return(inv, ref chest[i]);
        }

        /// <summary>A stray stack into the Hallownest bag (or an empty hotbar slot when the bag is full).</summary>
        static void Return(Inventory inv, ref ItemStack s)
        {
            int left = Put(inv.HkBag, s.Type, s.Count);
            if (left > 0) left = Put(inv.Slots, s.Type, left, Inventory.HotbarSize);
            if (left <= 0) s = default;
            else s.Count = left;
        }

        /// <summary>Into a row of slots: on its own stack first, then the first empty slot. Returns how many didn't fit.</summary>
        static int Put(ItemStack[] xs, ItemType t, int n, int to = -1)
        {
            if (to < 0) to = xs.Length;
            int max = ItemDb.Get(t).MaxStack;
            for (int i = 0; i < to && n > 0; i++)
            {
                if (xs[i].IsEmpty || xs[i].Type != t || xs[i].Count >= max) continue;
                int m = Mathf.Min(n, max - xs[i].Count);
                xs[i].Count += m;
                n -= m;
            }
            for (int i = 0; i < to && n > 0; i++)
            {
                if (!xs[i].IsEmpty) continue;
                int m = Mathf.Min(n, max);
                xs[i] = ItemStack.Of(t, m);
                n -= m;
            }
            return n;
        }

        static int Take(ItemStack[] xs, ItemType t, int n, int to)
        {
            for (int i = 0; i < to && n > 0; i++)
            {
                if (xs[i].IsEmpty || xs[i].Type != t) continue;
                int m = Mathf.Min(n, xs[i].Count);
                xs[i].Count -= m;
                n -= m;
                if (xs[i].Count <= 0) xs[i] = default;
            }
            return n;
        }

        // ------------------------------------------------------------------ the game asking

        /// <summary>The records read as they are (building a shop's stock, a boss visit setting what its fight needs).</summary>
        static int rawDepth;
        static bool Gating => rawDepth == 0 && TerraMod.Instance != null && !BossArena.Visiting;

        /// <summary>PlayerData.GetBool: a key item's record counts only while the item is held (and the charm 36 worn while either royal one is).</summary>
        internal static void GateBool(string name, ref bool result)
        {
            if (!Gating) return;
            var inv = TerraMod.Instance.Inventory;
            if (name == "equippedCharm_36")
            {
                var pd = PlayerData.instance;
                bool got = pd != null && Field(pd, "gotCharm_36") is bool g && g && pd.royalCharmState >= 3;
                result = got && (Held(inv, ItemType.HkKingsoul) || Held(inv, ItemType.HkVoidHeart));
                return;
            }
            if (byFlag.TryGetValue(name, out var d) && d.Kind == Kind.Bool && result) result = Held(inv, d.Type);
        }

        /// <summary>PlayerData.GetInt: a counted item (keys, eggs, ore, relics) counts in full while held, else not at all.</summary>
        internal static void GateInt(string name, ref int result)
        {
            if (!Gating || result <= 0) return;
            if (byFlag.TryGetValue(name, out var d) && d.Kind == Kind.Int && !Held(TerraMod.Instance.Inventory, d.Type)) result = 0;
        }

        /// <summary>The Knight holds the lantern up in a dark room (HeroController.SetDarkness reads the record itself): from the hotbar.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SetDarkness))]
        static bool SetDarkness(HeroController __instance, int darkness)
        {
            var pd = PlayerData.instance;
            __instance.wieldingLantern = darkness > 0 && pd != null && pd.GetBool("hasLantern");
            return false;
        }

        /// <summary>What a pitch-dark room hides from one without the lantern (it reads the record itself): from the hotbar.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DeactivateInDarknessWithoutLantern), "Start")]
        static bool DarknessStart(DeactivateInDarknessWithoutLantern __instance)
        {
            var gm = GameManager.instance;
            if (gm != null && gm.sm != null && gm.sm.darknessLevel == 2 && !gm.playerData.GetBool("hasLantern")) __instance.gameObject.SetActive(false);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.GetInt))]
        static void AfterGetInt(string intName, ref int __result) => GateInt(intName, ref __result);

        /// <summary>A shop's stock is built from the records as they are (Sly doesn't sell the lantern again when it's in the bag).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShopMenuStock), "BuildItemList")]
        static void ShopStart() => rawDepth++;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ShopMenuStock), "BuildItemList")]
        static void ShopEnd() => rawDepth = Mathf.Max(0, rawDepth - 1);
    }
}

namespace TerraKnight.Items
{
    internal static partial class HkItemsDev
    {
        /// <summary>Dev: the game's record set, the item given at once, put in the hotbar and held (as a player would, to use it).</summary>
        public static void ToHotbar(ItemType t)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            HkItems.Tick(mod);
            var inv = mod.Inventory;
            for (int i = 0; i < inv.HkBag.Length; i++)
            {
                if (inv.HkBag[i].IsEmpty || inv.HkBag[i].Type != t) continue;
                for (int k = 0; k < Inventory.HotbarSize; k++)
                    if (inv.Slots[k].IsEmpty) { inv.Slots[k] = inv.HkBag[i]; inv.HkBag[i] = default; inv.Selected = k; inv.Override = -1; return; }
            }
        }
    }
}
