using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hallownest's key items as Terraria items: the Lumafly Lantern, the City Crest, the keys, the Tram Pass, the King's
    /// Brand, the Delicate Flower, the Rancid Eggs, the Pale Ore, the four relics, the Kingsoul and the Void Heart. Each
    /// stands for its PlayerData record (what the game gives, it gives; what the game takes, it takes: a key used, relics
    /// sold to Lemm), drawn with the game's own inventory picture and named as the game names it. They can be kept in
    /// chests and personal storages as well as their own bag, and work while carried (the
    /// Hallownest bag, the bag, the hotbar, a pet slot), as the game's own inventory has them: the City Gate opens to the
    /// crest, the dark rooms are lit by the lantern, the tram runs for the pass, the Abyss's door to the Birthplace opens to
    /// the Kingsoul, the Siblings and the Radiance know the Void Heart. The Grimmchild is worn while its summon is out.
    /// With the charm page on (Charms.Enabled, still being made) those three are charms worn there instead, their items
    /// taken back.
    /// </summary>
    [HarmonyPatch]
    internal static class HkItems
    {
        public const string TexturePrefix = "HKInv:";
        public const int KeyBagSlots = 20;
        /// <summary>A reserved, saved slot: a newly obtained Dream Nail never has to be left on the ground.</summary>
        public const int DreamNailSlot = KeyBagSlots;
        public const int AbilityBagSlots = 8;
        public const int AbilityBagStart = DreamNailSlot + 1;
        // Append only: old dream/ability slots retain their saved indices.
        public const int KnightNailSlot = AbilityBagStart + AbilityBagSlots;
        public const int HallownestShieldSlot = KnightNailSlot + 1;
        public const int DarkRuneSlot = HallownestShieldSlot + 1;
        public const int BagSlots = DarkRuneSlot + 1;

        enum Kind { Bool, Int, Royal, Charm }

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
        /// <summary>A charm the story needs (the Grimmchild): got by its gotCharm record; "worn" while its summon is out.</summary>
        static Def C(ItemType t, int charm, string zh) => new Def { Type = t, Kind = Kind.Charm, Royal = charm, Flag = "gotCharm_" + charm, NameKey = "CHARM_NAME_" + charm, DescKey = "CHARM_DESC_" + charm, Sprite = null, Zh = zh };
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
            C(ItemType.HkGrimmchild, 40, "格林之子"),
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

        public static int AbilityBagSlot(ItemType type)
        {
            if (type == ItemType.DescendingDarkRune) return DarkRuneSlot;
            for (int i = 0; i < Player.PlayerHooks.AbilityItems.Length && i < AbilityBagSlots; i++)
                if (Player.PlayerHooks.AbilityItems[i].item == type) return AbilityBagStart + i;
            return -1;
        }

        public static int EquipmentBagSlot(ItemType type) => type == ItemType.KnightNail ? KnightNailSlot
            : AbilityShields.Is(type) ? HallownestShieldSlot : -1;

        public static bool TryStoreEquipment(Inventory inv, ItemStack stack)
        {
            int slot = EquipmentBagSlot(stack.Type);
            if (stack.IsEmpty || slot < 0 || !inv.HkBag[slot].IsEmpty) return false;
            inv.HkBag[slot] = stack; HallownestRecovery.Record(inv, stack.Type);
            return true;
        }

        public static bool CanStoreInBag(ItemStack s) => Is(s) || (!s.IsEmpty && (s.Type == ItemType.DreamNail || AbilityBagSlot(s.Type) >= 0 || EquipmentBagSlot(s.Type) >= 0));
        public static bool BagAccepts(int slot, ItemStack s) => s.IsEmpty ||
            (slot == DarkRuneSlot ? s.Type == ItemType.DescendingDarkRune : slot == KnightNailSlot || slot == HallownestShieldSlot ? EquipmentBagSlot(s.Type) == slot
                : slot == DreamNailSlot ? s.Type == ItemType.DreamNail : slot >= AbilityBagStart
                ? AbilityBagSlot(s.Type) == slot : Is(s) || s.Type == ItemType.DreamNail);

        public static bool TryStoreAbility(Inventory inv, ItemStack stack)
        {
            int slot = AbilityBagSlot(stack.Type);
            if (stack.IsEmpty || slot < 0 || !inv.HkBag[slot].IsEmpty) return false;
            inv.HkBag[slot] = stack; HallownestRecovery.Record(inv, stack.Type); // Keep prefix, forge and favorite; never displace an existing stack.
            return true;
        }

        internal static string AbilityDeliveryKey(string flag) => "ability.hkbag.delivered.v1." + flag;

        internal static bool OwnsAbilityReward(Inventory inv, ItemType type)
        {
            bool Has(ItemType item)
            {
                if (inv.Owns(item)) return true;
                foreach (var shield in AbilityShields.All) if (AbilityShields.Includes(shield.Type, item) && inv.Owns(shield.Type)) return true;
                foreach (var xs in new[] { inv.Bank, inv.Safe, inv.Forge, inv.Void })
                    foreach (var stack in xs) if (!stack.IsEmpty && (stack.Type == item || AbilityShields.Includes(stack.Type, item))) return true;
                return false;
            }
            return Has(type);
        }

        public static string BagSlotHint(int slot) => slot == DarkRuneSlot ? Lang.T("暗渊符文专用格") : slot == KnightNailSlot ? Lang.T("骨钉专用格")
            : slot == HallownestShieldSlot ? Lang.T("圣巢之盾专用格") : slot == DreamNailSlot ? Lang.T("梦之钉专用格")
            : slot >= AbilityBagStart && slot < AbilityBagStart + AbilityBagSlots
                ? Lang.F("{0}专用格", ItemDb.Get(Player.PlayerHooks.AbilityItems[slot - AbilityBagStart].item).Name)
                : Lang.T("圣巢物品格");

        // ------------------------------------------------------------------ the items

        internal static void AddItems(List<ItemDef> list)
        {
            foreach (var d in all)
                list.Add(new ItemDef
                {
                    Type = d.Type, Name = d.Zh, Texture = TexturePrefix + d.Type, Permanent = true, HkItem = true, UseSound = null, Rare = -11,
                    MaxStack = d.Kind == Kind.Int ? 9999 : 1, Tooltip = "",
                    // The lantern is a light pet too: worn in the light pet slot, it floats by the player (Pets); the Grimmchild
                    // a pet: worn in the pet slot it is out, the game's own Grimmchild with it.
                    PetBuff = d.Type == ItemType.HkLumaflyLantern ? Pets.LanternBuff : d.Type == ItemType.HkGrimmchild ? Pets.GrimmBuff : 0, LightPet = d.Type == ItemType.HkLumaflyLantern,
                    // Used (in hand), it calls its pet as a light pet item does.
                    UseAnimation = Pet(d.Type) ? 20f / 60f : 0f, UseTime = Pet(d.Type) ? 20f / 60f : 0f, NoMelee = Pet(d.Type),
                });
        }

        static bool Pet(ItemType t) => t == ItemType.HkLumaflyLantern || t == ItemType.HkGrimmchild;

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
            def.Tooltip = (desc.Length > 0 ? desc + "\n" : "") + Lang.T(d.Type == ItemType.HkLumaflyLantern ? "放在圣巢背包里就生效：黑暗的房间会被照亮；放进照明宠物栏还会漂浮在身边照亮四周"
                : d.Type == ItemType.HkGrimmchild ? "放进宠物栏（或使用）召唤格林之子：算作佩戴了这个护符"
                : d.Kind == Kind.Royal ? "放在圣巢背包里就算佩戴着，不用拿在手上" : "放在圣巢背包里就算已获取，不用拿在手上");
        }

        // ------------------------------------------------------------------ pictures

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, string> abilitySprites = new Dictionary<string, string>
        {
            [nameof(ItemType.ShadeCloak)] = "items__0004_shade_cloak",
            [nameof(ItemType.CrystalHeart)] = "items__0000_crystal-heart",
        };
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
            // The ability accessories (AbilityAccessories) by the game's inventory pictures of them.
            if (d == null && abilitySprites.TryGetValue(typeName, out string spriteName))
            {
                if (sprites == null || !sprites.ContainsKey(spriteName))
                {
                    sprites = new Dictionary<string, Sprite>();
                    foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>()) if (sp != null && !sprites.ContainsKey(sp.name)) sprites[sp.name] = sp;
                }
                sprites.TryGetValue(spriteName, out s);
            }
            if (d != null)
            {
                if (d.Kind == Kind.Royal) s = RoyalSprite(d.Royal);
                else if (d.Kind == Kind.Charm) s = CharmIconList.Instance != null ? CharmIconList.Instance.GetSprite(d.Royal) : null;
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

        static readonly Dictionary<string, AccessTools.FieldRef<PlayerData, bool>> bools = new Dictionary<string, AccessTools.FieldRef<PlayerData, bool>>();
        static readonly Dictionary<string, AccessTools.FieldRef<PlayerData, int>> ints = new Dictionary<string, AccessTools.FieldRef<PlayerData, int>>();

        static System.Reflection.FieldInfo FieldOf(string name)
        {
            if (!fields.TryGetValue(name, out var f)) fields[name] = f = AccessTools.Field(typeof(PlayerData), name);
            return f;
        }

        /// <summary>A bool record of the game's (false when there is none by the name), read without boxing it.</summary>
        static bool TryBool(PlayerData pd, string name, out bool value)
        {
            if (!bools.TryGetValue(name, out var r))
            {
                var f = FieldOf(name);
                bools[name] = r = f != null && f.FieldType == typeof(bool) ? AccessTools.FieldRefAccess<PlayerData, bool>(f) : null;
            }
            value = r != null && r(pd);
            return r != null;
        }

        /// <summary>An int record of the game's (false when there is none by the name), read without boxing it.</summary>
        static bool TryInt(PlayerData pd, string name, out int value)
        {
            if (!ints.TryGetValue(name, out var r))
            {
                var f = FieldOf(name);
                ints[name] = r = f != null && f.FieldType == typeof(int) ? AccessTools.FieldRefAccess<PlayerData, int>(f) : null;
            }
            value = r != null ? r(pd) : 0;
            return r != null;
        }

        /// <summary>The counted ones as many as the whole world holds: 4 simple keys, 21 rancid eggs, 6 pale ore, the four relics' 14, 17, 8 and 4.</summary>
        static readonly Dictionary<string, int> worldTotals = new Dictionary<string, int>
        {
            ["simpleKeys"] = 4, ["rancidEggs"] = 21, ["ore"] = 6, ["trinket1"] = 14, ["trinket2"] = 17, ["trinket3"] = 8, ["trinket4"] = 4,
        };

        public static int DevCreativeGiven;

        /// <summary>
        /// A creative save has all of them (UI.CreativeMenu), each by its record as the game sets it on getting the item: the
        /// counted ones to the world's totals, the Void Heart for the royal charm (one of its two states), the Grimmchild at
        /// its first level, the Delicate Flower whole, and the Dream Nail, awoken. On every load of the save: a record is
        /// only ever raised, so what was used up (a key, relics sold) is there again, as nothing runs out in creative.
        /// </summary>
        public static void GiveAllForCreative(PlayerData pd)
        {
            if (pd == null) return;
            int raised = 0;
            foreach (var d in all)
                switch (d.Kind)
                {
                    case Kind.Bool:
                        if (!(TryBool(pd, d.Flag, out bool b) && b)) { pd.SetBool(d.Flag, true); raised++; }
                        if (d.Type == ItemType.HkDelicateFlower) pd.SetBool("xunFlowerBroken", false);
                        break;
                    case Kind.Int:
                        int want = worldTotals.TryGetValue(d.Flag, out int total) ? total : 1;
                        if (TryInt(pd, d.Flag, out int n) && n < want) { pd.SetInt(d.Flag, want); raised++; }
                        break;
                    case Kind.Charm:
                        if (!(TryBool(pd, d.Flag, out bool c) && c)) { pd.SetBool(d.Flag, true); raised++; }
                        if (d.Royal == 40 && pd.GetInt("grimmChildLevel") < 1) pd.SetInt("grimmChildLevel", 1);
                        break;
                    case Kind.Royal:
                        if (d.Royal != 4) break;
                        if (!(TryBool(pd, "gotCharm_36", out bool r) && r) || pd.royalCharmState < 4) raised++;
                        pd.SetBool("gotCharm_36", true);
                        pd.royalCharmState = Mathf.Max(pd.royalCharmState, 4);
                        pd.SetBool("gotShadeCharm", true);
                        break;
                }
            if (!pd.hasDreamNail) { pd.hasDreamNail = true; raised++; }
            pd.dreamNailUpgraded = true;
            DevCreativeGiven += raised;
            if (raised > 0) Plugin.Log.LogInfo($"[圣巢物品] 创造模式：补齐 {raised} 种圣巢物品");
        }

        /// <summary>How many of the item the game's record says the Knight has.</summary>
        static int Wanted(Def d, PlayerData pd)
        {
            switch (d.Kind)
            {
                // With the charm page on they are charms worn there: their items go.
                case Kind.Royal:
                    if (Charms.Enabled) return 0;
                    return TryBool(pd, "gotCharm_36", out bool got) && got && pd.royalCharmState == d.Royal ? 1 : 0;
                case Kind.Charm:
                    if (Charms.Enabled) return 0;
                    return TryBool(pd, d.Flag, out bool has) && has ? 1 : 0;
                case Kind.Int:
                    return TryInt(pd, d.Flag, out int n) ? Mathf.Max(0, n) : 0;
                default:
                    if (!(TryBool(pd, d.Flag, out bool b)) || !b) return 0;
                    if (d.Type == ItemType.HkDelicateFlower && TryBool(pd, "xunFlowerBroken", out bool broken) && broken) return 0;
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

        /// <summary>Carried: in the Hallownest bag, the bag or the hotbar, a pet slot, or on the cursor.</summary>
        public static bool Carried(Inventory inv, ItemType t) =>
            inv != null && (Count(inv.HkBag, t) + Count(inv.Slots, t) + Count(inv.Misc, t) > 0 || !inv.Mouse.IsEmpty && inv.Mouse.Type == t);

        /// <summary>The Lumafly Lantern works: carried (out as the light pet, too).</summary>
        public static bool LanternLit(Inventory inv) => Pets.LanternOut || Carried(inv, ItemType.HkLumaflyLantern);

        /// <summary>Carried (the lantern also out as the light pet).</summary>
        static bool Working(Inventory inv, ItemType t) => t == ItemType.HkLumaflyLantern ? LanternLit(inv) : Carried(inv, t);

        public static int DevGiven, DevTaken;

        /// <summary>
        /// Every frame in play: the items as the game's records have them (given into the bag, taken from the bag first);
        /// items in the bag's main rows or ammo slots move to the Hallownest bag; chest storage stays in place.
        /// </summary>
        public static void Tick(TerraMod mod)
        {
            var pd = PlayerData.instance;
            if (mod == null || pd == null) return;
            // The Hollow Knight's Phase Control tests gotShadeCharm, not royalCharmState.
            // Older creative saves raised the royal charm but missed that native ending flag.
            if (pd.royalCharmState >= 4 && TryBool(pd, "gotCharm_36", out bool royal) && royal && !pd.gotShadeCharm)
            {
                pd.SetBool("gotShadeCharm", true);
                Plugin.Log.LogInfo("[虚空之心] 补齐原版 gotShadeCharm 结局标记");
            }
            var inv = mod.Inventory;
            HallownestRecovery.Observe(inv);
            foreach (var d in all)
            {
                int want = Wanted(d, pd);
                // A fresh native acquisition (e.g. another Delicate Flower) is not an automatic replacement for a sold item.
                if (inv.HallownestNativeCounts.TryGetValue(d.Type, out int previous) && want > previous
                    && inv.HallownestTradeOffsets.TryGetValue(d.Type, out int soldOffset) && soldOffset < 0)
                    inv.HallownestTradeOffsets.Remove(d.Type);
                inv.HallownestNativeCounts[d.Type] = want;
                if (d.Kind != Kind.Int && inv.HallownestTradeOffsets.TryGetValue(d.Type, out int offset)) want = Mathf.Max(0, want + offset);
                int have = Count(inv.Slots, d.Type) + Count(inv.HkBag, d.Type) + Count(inv.Misc, d.Type) + (!inv.Mouse.IsEmpty && inv.Mouse.Type == d.Type ? inv.Mouse.Count : 0);
                if (have < want)
                {
                    int add = want - have;
                    int left = Put(inv.HkBag, d.Type, add, KeyBagSlots);
                    if (left > 0) left = Put(inv.Slots, d.Type, left, Inventory.HotbarSize);
                    if (add - left > 0)
                    {
                        var hero = HeroController.SilentInstance;
                        if (hero != null) UI.PopupText.Show(ItemDb.Get(d.Type), add - left, hero.transform.position);
                        DevGiven += add - left;
                        HallownestRecovery.Record(inv, d.Type);
                    }
                }
                else if (have > want)
                {
                    int take = have - want;
                    take = Take(inv.HkBag, d.Type, take, inv.HkBag.Length);
                    take = Take(inv.Slots, d.Type, take, inv.Slots.Length);
                    take = Take(inv.Misc, d.Type, take, inv.Misc.Length);
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
            HandChanged(inv);
        }

        /// <summary>A stray stack into the Hallownest bag (or an empty hotbar slot when the bag is full).</summary>
        static void Return(Inventory inv, ref ItemStack s)
        {
            int left = Put(inv.HkBag, s.Type, s.Count, KeyBagSlots);
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

        internal static void Trade(Inventory inv, ItemType type, int delta, bool alreadyMoved = false)
        {
            if (inv == null) return;
            if (type == ItemType.KnightNail || type == ItemType.DreamNail)
            {
                if (delta < 0) inv.HallownestTradeOffsets[type] = -1;
                else
                {
                    inv.HallownestTradeOffsets.Remove(type);
                    if (type == ItemType.DreamNail && PlayerData.instance != null) PlayerData.instance.hasDreamNail = true;
                }
                return;
            }
            if (!byType.TryGetValue(type, out var d) || PlayerData.instance == null) return;
            var pd = PlayerData.instance;
            if (d.Kind == Kind.Int)
            {
                TryInt(pd, d.Flag, out int n);
                pd.SetInt(d.Flag, Mathf.Max(0, n + delta));
                return;
            }
            int carried = Count(inv.Slots, type) + Count(inv.HkBag, type) + Count(inv.Misc, type)
                + (!inv.Mouse.IsEmpty && inv.Mouse.Type == type ? inv.Mouse.Count : 0);
            // Buy and storage moves run after placement; sell runs before clearing its source.
            int desired = Mathf.Max(0, carried + (delta < 0 && !alreadyMoved ? delta : 0));
            if (delta > 0 && d.Kind == Kind.Bool && Wanted(d, pd) == 0) pd.SetBool(d.Flag, true);
            if (delta > 0 && d.Kind == Kind.Charm)
            {
                pd.SetBool(d.Flag, true);
                if (d.Royal == 40) pd.grimmChildLevel = Mathf.Max(1, pd.grimmChildLevel);
            }
            if (delta > 0 && d.Kind == Kind.Royal)
            {
                pd.SetBool("gotCharm_36", true);
                pd.royalCharmState = Mathf.Max(pd.royalCharmState, d.Royal);
                if (d.Royal >= 4) pd.gotShadeCharm = true;
            }
            inv.HallownestTradeOffsets[type] = desired - Wanted(d, pd);
            inv.HallownestNativeCounts[type] = Wanted(d, pd);
        }

        // ------------------------------------------------------------------ the game asking

        /// <summary>The records read as they are (building a shop's stock, a boss visit setting what its fight needs).</summary>
        static int rawDepth;
        static bool Gating => rawDepth == 0 && TerraMod.Instance != null && !BossArena.Visiting;

        /// <summary>PlayerData.GetBool: a key item's record counts while the item is carried (and the charm 36 worn while either royal one is).</summary>
        internal static void GateBool(string name, ref bool result)
        {
            if (!Gating || PickupAsking()) return;
            var inv = TerraMod.Instance.Inventory;
            if (name == "equippedCharm_36" && !Charms.Enabled)
            {
                var pd = PlayerData.instance;
                bool got = pd != null && TryBool(pd, "gotCharm_36", out bool g) && g && pd.royalCharmState >= 3;
                result = got && (Carried(inv, ItemType.HkKingsoul) || Carried(inv, ItemType.HkVoidHeart));
                if (got && !result) Refused(pd.royalCharmState >= 4 ? ItemType.HkVoidHeart : ItemType.HkKingsoul);
                return;
            }
            // The Grimmchild worn: its summon out (Pets), whatever the charm menu says.
            if (name == "equippedCharm_40" && !Charms.Enabled)
            {
                var pd = PlayerData.instance;
                bool got = pd != null && TryBool(pd, "gotCharm_40", out bool g40) && g40;
                result = got && Pets.GrimmOut;
                return;
            }
            if (byFlag.TryGetValue(name, out var d) && d.Kind == Kind.Bool && result)
            {
                result = Working(inv, d.Type);
                if (!result) Refused(d.Type);
            }
        }

        /// <summary>PlayerData.GetInt: a counted item (keys, eggs, ore, relics) counts in full while carried, else not at all.</summary>
        internal static void GateInt(string name, ref int result)
        {
            if (!Gating || result <= 0 || PickupAsking()) return;
            if (byFlag.TryGetValue(name, out var d) && d.Kind == Kind.Int && !Carried(TerraMod.Instance.Inventory, d.Type))
            {
                result = 0;
                Refused(d.Type);
            }
        }

        /// <summary>
        /// A pickup asking whether its item was got already (Shiny Control's "PD Bool?": the King's Brand on its stand):
        /// the record as it is, or the item would lie there again for the taking whenever it isn't carried.
        /// </summary>
        static bool PickupAsking() => HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm?.Name == "Shiny Control";

        // ------------------------------------------------------------------ asked again once carried

        /// <summary>
        /// A state machine told no because its item wasn't carried (both bags full as it was got), that waits in the state
        /// that asked: the Abyss's gate asks for the King's Brand once as its room comes in (abyss_door's Check) and, told no,
        /// stays there; the Birthplace's floor asks for the Kingsoul so. Carried at last, the item is asked for again there.
        /// </summary>
        sealed class Asker
        {
            public HutongGames.PlayMaker.Fsm Fsm;
            public string State;
            public ItemType Item;
        }

        static readonly List<Asker> askers = new List<Asker>();
        static readonly HashSet<ItemType> carriedBefore = new HashSet<ItemType>();
        static bool lanternBefore;

        /// <summary>What a pitch-dark room hid from one without the lantern (DarknessStart): back once it is carried.</summary>
        static readonly List<GameObject> hiddenInDark = new List<GameObject>();

        public static int DevAskedAgain;
        public static string DevLastAskedAgain = "";

        static void Refused(ItemType t)
        {
            var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
            var state = fsm?.ActiveState;
            // A PlayerData action's question (not the mod's own code run from inside a state machine's step).
            if (state == null || fsm.Owner == null || state.ActiveAction == null || !state.ActiveAction.GetType().Name.Contains("PlayerData")) return;
            foreach (var a in askers)
                if (a.Fsm == fsm && a.State == state.Name && a.Item == t) return;
            if (askers.Count >= 64) askers.RemoveAll(a => a.Fsm.Owner == null || a.Fsm.ActiveStateName != a.State);
            if (askers.Count >= 64) askers.RemoveAt(0);
            askers.Add(new Asker { Fsm = fsm, State = state.Name, Item = t });
        }

        /// <summary>What is carried changed: a key item come in asked for again where it was refused; the lantern lights (or not) at once.</summary>
        static bool grimmBefore;
        static float grimmLook;
        public static int DevGrimmCalls;

        /// <summary>
        /// The Grimmchild's summon out or gone: the Knight's "Spawn Grimmchild" machine (Charm Effects) and a Grimmchild's own
        /// "Charm Unequip" told CHARM EQUIP CHECK, as the charm menu tells them, so the game's own Grimmchild comes or goes;
        /// once a second while it is out and none flies, called again (a new scene).
        /// </summary>
        static void GrimmCheck()
        {
            // With the charm page on: worn there (Carefree Melody, level 5, has no Grimmchild to call).
            bool now = Charms.Enabled ? Charms.Worn(Charms.Grimmchild) && PlayerData.instance != null && PlayerData.instance.grimmChildLevel < 5 : Pets.GrimmOut;
            bool changed = now != grimmBefore;
            grimmBefore = now;
            if (!changed && (!now || Time.unscaledTime < grimmLook)) return;
            grimmLook = Time.unscaledTime + 1f;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            if (!changed && GameObject.FindWithTag("Grimmchild") != null) return;
            var fx = hero.transform.Find("Charm Effects");
            if (fx != null)
                foreach (var f in fx.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "Spawn Grimmchild") f.SendEvent("CHARM EQUIP CHECK");
            if (!now)
                foreach (var f in PlayMakerFSM.FsmList)
                    if (f != null && f.FsmName == "Charm Unequip" && f.gameObject.name.StartsWith("Grimmchild")) f.SendEvent("CHARM EQUIP CHECK");
            DevGrimmCalls++;
        }

        static void HandChanged(Inventory inv)
        {
            GrimmCheck();
            // The lantern lit or put out (carried, or its light pet out): the dark room as it should be at once.
            bool lantern = LanternLit(inv);
            if (lantern != lanternBefore)
            {
                lanternBefore = lantern;
                var hero = HeroController.SilentInstance;
                if (hero != null) hero.SetDarkness(World.LightMap.Darkness);
                if (lantern)
                {
                    foreach (var go in hiddenInDark) if (go != null) go.SetActive(true);
                    hiddenInDark.Clear();
                    AskAgain(ItemType.HkLumaflyLantern);
                }
            }
            foreach (var d in all)
            {
                bool now = Carried(inv, d.Type);
                if (now == carriedBefore.Contains(d.Type)) continue;
                if (!now)
                {
                    carriedBefore.Remove(d.Type);
                    continue;
                }
                carriedBefore.Add(d.Type);
                AskAgain(d.Type);
            }
        }

        /// <summary>The state machines told no for an item, asked again where they asked (if they still wait there).</summary>
        static void AskAgain(ItemType item)
        {
            int held = (int)item;
            for (int i = askers.Count - 1; i >= 0; i--)
            {
                var a = askers[i];
                var owner = a.Fsm.Owner;
                if (owner == null)
                {
                    askers.RemoveAt(i);
                    continue;
                }
                if ((int)a.Item != held) continue;
                askers.RemoveAt(i);
                // Gone on elsewhere (a talk that went on, a spawner at work): left as it is.
                if (!owner.isActiveAndEnabled || a.Fsm.ActiveStateName != a.State) continue;
                a.Fsm.SetState(a.State);
                DevAskedAgain++;
                DevLastAskedAgain = $"{owner.gameObject.scene.name}/{owner.name} '{a.Fsm.Name}' {a.State} → {a.Fsm.ActiveStateName}";
                Plugin.Log.LogInfo($"[圣巢物品] 带上{ItemDb.Get((ItemType)held).Name}：{DevLastAskedAgain}");
            }
        }

        /// <summary>The Knight holds the lantern up in a dark room (HeroController.SetDarkness reads the record itself): the lantern carried.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SetDarkness))]
        static bool SetDarkness(HeroController __instance, int darkness)
        {
            var pd = PlayerData.instance;
            __instance.wieldingLantern = darkness > 0 && pd != null && pd.GetBool("hasLantern");
            return false;
        }

        /// <summary>What a pitch-dark room hides from one without the lantern (it reads the record itself): the lantern carried.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DeactivateInDarknessWithoutLantern), "Start")]
        static bool DarknessStart(DeactivateInDarknessWithoutLantern __instance)
        {
            var gm = GameManager.instance;
            if (gm != null && gm.sm != null && gm.sm.darknessLevel == 2 && !gm.playerData.GetBool("hasLantern"))
            {
                __instance.gameObject.SetActive(false);
                if (hiddenInDark.Count >= 256) hiddenInDark.RemoveAll(go => go == null);
                hiddenInDark.Add(__instance.gameObject);
            }
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
