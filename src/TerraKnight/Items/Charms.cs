using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hollow Knight's charms, kept as the game keeps them (PlayerData: gotCharm_N, charmCost_N, equippedCharm_N, the
    /// equipped list, charmSlots and charmSlotsFilled, overcharmed and canOvercharm, the broken fragile charms) and changed
    /// as its charm menu (UI Charms) changes them, only while resting on a bench. Putting one on needs a free notch; one
    /// costing more than the notches left overcharms, which the first time takes three tries (Overcharm Check: two
    /// cracks, then the break; canOvercharm after that) and doubles the damage taken (HeroController.TakeDamage, and the
    /// falls and blasts in TerraStats.TakeDirect). The White Fragment can't be worn and the Void Heart can't be taken off.
    /// Drawn on the inventory's charm page (InventoryGuiCharms).
    /// </summary>
    internal static class Charms
    {
        public const int Count = 40, Royal = 36, Grimmchild = 40;
        /// <summary>The overcharmed debuff (the mod's own, past Terraria's).</summary>
        public const int OverchargeBuff = 9003;
        /// <summary>Tries before the first overcharm goes through (UI Charms: OVERCHARM CRACK 1, CRACK 2, BREAK).</summary>
        const int OverchargeTries = 3;
        const string MigratedKey = "charms.page.v1";

        static readonly System.Reflection.FieldInfo[] got = new System.Reflection.FieldInfo[Count + 1];
        static readonly System.Reflection.FieldInfo[] worn = new System.Reflection.FieldInfo[Count + 1];
        static readonly System.Reflection.FieldInfo[] cost = new System.Reflection.FieldInfo[Count + 1];

        static Charms()
        {
            for (int id = 1; id <= Count; id++)
            {
                got[id] = AccessTools.Field(typeof(PlayerData), "gotCharm_" + id);
                worn[id] = AccessTools.Field(typeof(PlayerData), "equippedCharm_" + id);
                cost[id] = AccessTools.Field(typeof(PlayerData), "charmCost_" + id);
            }
        }

        static PlayerData Pd => PlayerData.instance;

        static BepInEx.Configuration.ConfigEntry<bool> pageSetting;

        public static void Bind(BepInEx.Configuration.ConfigFile cfg) =>
            pageSetting = cfg.Bind("Dev", "CharmPage", false,
                "护符页（还在制作中，默认关闭）：开启后装备栏上方出现护符页，国王之魂、虚空之心、格林之子改在护符页里装。\nThe charm page (still being made, off by default): on, the Charms page shows above the equipment column and the Kingsoul, the Void Heart and the Grimmchild are worn there.");

        /// <summary>
        /// The charm page is on (the CharmPage setting; still being made). Off, nothing of it shows or works: the Kingsoul,
        /// the Void Heart and the Grimmchild keep their items' ways (HkItems), and a save that had the page is put back (RollBack).
        /// </summary>
        public static bool Enabled => pageSetting != null && pageSetting.Value;

        public static bool Got(int id) => Pd != null && got[id] != null && (bool)got[id].GetValue(Pd);
        public static bool Worn(int id) => Pd != null && worn[id] != null && (bool)worn[id].GetValue(Pd);
        /// <summary>The notches it takes (the Void Heart none, whatever its record says).</summary>
        public static int Cost(int id) => Pd == null || cost[id] == null ? 0 : id == Royal && Pd.royalCharmState >= 4 ? 0 : (int)cost[id].GetValue(Pd);

        public static int Notches => Pd != null ? Pd.charmSlots : 0;
        public static int Filled => Pd != null ? Pd.charmSlotsFilled : 0;
        public static bool Overcharmed => Pd != null && Pd.overcharmed;
        /// <summary>The charms worn, in the order they were put on.</summary>
        public static List<int> WornList => Pd?.equippedCharms;
        public static int Owned
        {
            get
            {
                int n = 0;
                for (int id = 1; id <= Count; id++) if (Got(id)) n++;
                return n;
            }
        }

        /// <summary>Charms are changed only while resting on a bench, as in the game.</summary>
        public static bool CanChange => Pd != null && Pd.atBench;

        /// <summary>A fragile charm broken by a death (the Leg Eater mends it).</summary>
        public static bool Broken(int id) => Pd != null && (id == 23 ? Pd.brokenCharm_23 : id == 24 ? Pd.brokenCharm_24 : id == 25 && Pd.brokenCharm_25);
        static bool Unbreakable(int id) => Pd != null && (id == 23 ? Pd.fragileHealth_unbreakable : id == 24 ? Pd.fragileGreed_unbreakable : id == 25 && Pd.fragileStrength_unbreakable);

        public enum Answer { None, On, Off, Overcharmed, NotFound, NotAtBench, NoNotch, Crack, CantWear, CantRemove }

        static int triesFor, tries;

        /// <summary>The overcharm's tries start over (the page shut, another charm tried).</summary>
        public static void ForgetTries() => tries = 0;

        /// <summary>The tries left before the first overcharm goes through.</summary>
        public static int TriesLeft => OverchargeTries - tries;

        /// <summary>A click on a charm: on, or off if it is worn (UI Charms' Equip / Unequip, Open Slot?, Overcharm Check).</summary>
        public static Answer Click(int id)
        {
            var pd = Pd;
            if (pd == null || id < 1 || id > Count) return Answer.None;
            if (!Got(id)) return Answer.NotFound;
            if (!CanChange) return Answer.NotAtBench;
            if (triesFor != id) { triesFor = id; tries = 0; }
            if (Worn(id))
            {
                if (id == Royal && pd.royalCharmState >= 4) return Answer.CantRemove;
                Set(id, false);
                Apply();
                return Answer.Off;
            }
            // CHARM_DESC_36_A: the White Fragment "can't be equipped".
            if (id == Royal && pd.royalCharmState < 3) return Answer.CantWear;
            int c = Cost(id);
            if (c > 0 && pd.charmSlotsFilled >= pd.charmSlots) return Answer.NoNotch;
            if (pd.charmSlotsFilled + c > pd.charmSlots && !pd.canOvercharm)
            {
                if (++tries < OverchargeTries) return Answer.Crack;
                pd.canOvercharm = true;
            }
            tries = 0;
            Set(id, true);
            Apply();
            return pd.overcharmed ? Answer.Overcharmed : Answer.On;
        }

        static void Set(int id, bool on)
        {
            var pd = Pd;
            worn[id].SetValue(pd, on);
            if (on) { if (!pd.equippedCharms.Contains(id)) pd.EquipCharm(id); }
            else while (pd.equippedCharms.Contains(id)) pd.UnequipCharm(id);
        }

        /// <summary>
        /// After a change, what the game does after its charm menu's (BossSequenceController.ApplyBindings): the notches
        /// counted, overcharmed or not, the Knight's charm effects updated, every machine told CHARM EQUIP CHECK (the
        /// Grimmchild, the Weaverlings, the Dreamshield come or go), the lifeblood and the HUD's indicators refreshed.
        /// </summary>
        public static void Apply()
        {
            var pd = Pd;
            if (pd == null) return;
            if (pd.royalCharmState >= 4) pd.charmCost_36 = 0;
            pd.CalculateNotchesUsed();
            pd.overcharmed = pd.charmSlotsFilled > pd.charmSlots;
            var hero = HeroController.SilentInstance;
            if (hero != null) hero.CharmUpdate();
            PlayMakerFSM.BroadcastEvent("CHARM EQUIP CHECK");
            EventRegister.SendEvent("UPDATE BLUE HEALTH");
            PlayMakerFSM.BroadcastEvent("CHARM INDICATOR CHECK");
            DevApplied++;
        }

        public static int DevApplied;

        /// <summary>
        /// Every frame in play, before the Hallownest items take theirs back: an older save's charms moved onto the page once
        /// (the Grimmchild out as a pet, the Kingsoul carried, the Void Heart: worn now if the notches hold them), and the
        /// overcharmed debuff shown while it holds.
        /// </summary>
        public static void Tick(TerraMod mod)
        {
            var pd = Pd;
            if (mod == null || pd == null) return;
            if (!Enabled)
            {
                if (mod.GrantedAbilities.Contains(MigratedKey)) RollBack(mod, pd);
                if (Player.PlayerBuffs.Has(OverchargeBuff)) Player.PlayerBuffs.Remove(OverchargeBuff);
                return;
            }
            if (!mod.GrantedAbilities.Contains(MigratedKey)) Migrate(mod, pd);
            Player.PlayerBuffs.Define(OverchargeBuff, "护符过载", "受到的伤害翻倍", debuff: true, noTime: true);
            if (pd.overcharmed) { if (!Player.PlayerBuffs.Has(OverchargeBuff)) Player.PlayerBuffs.Add(OverchargeBuff, 18000); }
            else if (Player.PlayerBuffs.Has(OverchargeBuff)) Player.PlayerBuffs.Remove(OverchargeBuff);
        }

        /// <summary>
        /// A save that had the charm page, the page off: what it put on taken off (the Void Heart aside, which the game never
        /// takes off), the notches emptied and overcharm gone, so nothing works unseen; the Kingsoul and the Grimmchild come
        /// back as their items (HkItems). The page on again moves the save onto it afresh.
        /// </summary>
        static void RollBack(TerraMod mod, PlayerData pd)
        {
            mod.GrantedAbilities.Remove(MigratedKey);
            string was = string.Join(",", pd.equippedCharms);
            bool voidHeart = pd.royalCharmState >= 4;
            for (int id = 1; id <= Count; id++)
                if (Worn(id) && !(id == Royal && voidHeart)) worn[id].SetValue(pd, false);
            pd.equippedCharms.RemoveAll(id => !(id == Royal && voidHeart));
            Apply();
            Plugin.Log.LogInfo($"[护符] 护符页关闭：卸下 {was}，凹槽 {pd.charmSlotsFilled}/{pd.charmSlots}");
        }

        /// <summary>
        /// A creative save, each time it is loaded, with the charm page: every charm (the royal one is HkItems', the Void
        /// Heart), all eleven notches, the three fragile charms unbreakable and whole, and overcharming open at once, so any
        /// set can be tried.
        /// </summary>
        public static void GiveAllForCreative(PlayerData pd)
        {
            if (pd == null || !Enabled) return;
            int raised = 0;
            for (int id = 1; id <= Count; id++)
            {
                if (id == Royal || pd.GetBool("gotCharm_" + id)) continue;
                pd.SetBool("gotCharm_" + id, true);
                raised++;
            }
            pd.fragileHealth_unbreakable = pd.fragileGreed_unbreakable = pd.fragileStrength_unbreakable = true;
            pd.brokenCharm_23 = pd.brokenCharm_24 = pd.brokenCharm_25 = false;
            if (pd.grimmChildLevel < 1) pd.grimmChildLevel = 1;
            if (pd.charmSlots < MaxNotches)
            {
                pd.charmSlots = MaxNotches;
                raised++;
            }
            pd.canOvercharm = true;
            if (raised > 0) Plugin.Log.LogInfo($"[护符] 创造模式：补齐护符和凹槽（{raised} 项），凹槽 {pd.charmSlots} 个");
        }

        const int MaxNotches = 11;

        static void Migrate(TerraMod mod, PlayerData pd)
        {
            mod.GrantedAbilities.Add(MigratedKey);
            var inv = mod.Inventory;
            bool grimmOut = inv.Misc[Pets.PetSlot].Type == ItemType.HkGrimmchild && !inv.Misc[Pets.PetSlot].IsEmpty;
            bool kingsoulCarried = HkItems.Carried(inv, ItemType.HkKingsoul);
            var changes = new List<string>();
            // The list as the records have it: only what is had and marked worn, in its order.
            var list = new List<int>();
            foreach (int id in pd.equippedCharms) if (id >= 1 && id <= Count && Worn(id) && Got(id) && !list.Contains(id)) list.Add(id);
            for (int id = 1; id <= Count; id++)
            {
                if (Worn(id) && !Got(id)) { worn[id].SetValue(pd, false); changes.Add("卸下未获得的 " + id); }
                else if (Worn(id) && !list.Contains(id)) list.Add(id);
            }
            if (list.Count != pd.equippedCharms.Count || !list.TrueForAll(pd.equippedCharms.Contains)) changes.Add("整理已装备列表");
            pd.equippedCharms.Clear();
            pd.equippedCharms.AddRange(list);
            if (pd.royalCharmState >= 4) pd.charmCost_36 = 0;
            pd.CalculateNotchesUsed();
            bool Fits(int id) => pd.charmSlotsFilled + Cost(id) <= pd.charmSlots;
            if (Got(Royal) && pd.royalCharmState >= 4 && !Worn(Royal)) { Set(Royal, true); changes.Add("虚空之心"); }
            else if (Got(Royal) && pd.royalCharmState == 3 && kingsoulCarried && !Worn(Royal) && Fits(Royal)) { Set(Royal, true); changes.Add("国王之魂"); }
            pd.CalculateNotchesUsed();
            if (Got(Grimmchild) && grimmOut && !Worn(Grimmchild) && Fits(Grimmchild)) { Set(Grimmchild, true); changes.Add("格林之子"); }
            Apply();
            Plugin.Log.LogInfo($"[护符] 护符页迁移：已获得 {Owned} 个，装备 {string.Join(",", pd.equippedCharms)}，凹槽 {pd.charmSlotsFilled}/{pd.charmSlots}"
                + (changes.Count > 0 ? "；" + string.Join("、", changes) : ""));
        }

        // ------------------------------------------------------------------ names

        static readonly string[] zh =
        {
            "", "蜂群集结", "任性的指南针", "幼虫之歌", "坚硬外壳", "巴德尔之壳", "亡者之怒", "快速聚集", "生命血之心", "生命血核心", "防御者纹章",
            "吸虫之巢", "苦痛荆棘", "骄傲印记", "稳定之体", "沉重之击", "锋利之影", "蘑菇孢子", "修长之钉", "萨满之石", "灵魂捕手",
            "噬魂者", "发光子宫", "易碎心脏", "易碎贪婪", "易碎力量", "骨钉大师的荣耀", "乔尼的祝福", "乌恩之形", "蜂巢之血", "舞梦者",
            "冲刺大师", "快速劈砍", "法术扭曲者", "深度聚集", "蜕变挽歌", "国王之魂", "飞毛腿", "梦之盾", "编织者之歌", "格林之子",
        };

        /// <summary>The game's key for the charm as it is now: the White Fragment, Kingsoul or Void Heart; Carefree Melody; the unbreakable ones.</summary>
        static string Suffix(int id, bool desc)
        {
            var pd = Pd;
            if (pd == null) return "";
            if (id == Royal) return pd.royalCharmState >= 4 ? "_C" : pd.royalCharmState == 3 ? "_B" : "_A";
            if (id == Grimmchild) return pd.grimmChildLevel >= 5 ? "_N" : desc && pd.grimmChildLevel == 4 ? "_F" : "";
            if (id >= 23 && id <= 25) return Unbreakable(id) ? "_G" : desc && Broken(id) ? "_BROKEN" : "";
            return "";
        }

        static string Zh(int id)
        {
            var pd = Pd;
            if (id == Royal && pd != null) return pd.royalCharmState >= 4 ? "虚空之心" : pd.royalCharmState == 3 ? "国王之魂" : "白色碎片";
            if (id == Grimmchild && pd != null && pd.grimmChildLevel >= 5) return "无忧旋律";
            if (id >= 23 && id <= 25 && Unbreakable(id)) return id == 23 ? "坚固心脏" : id == 24 ? "坚固贪婪" : "坚固力量";
            return zh[id];
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

        /// <summary>The charm's name as the game names it (the mod's own Chinese names in Chinese).</summary>
        public static string Name(int id) => Lang.IsSource ? Zh(id) : Text("CHARM_NAME_" + id + Suffix(id, false), Zh(id));

        /// <summary>The game's own description, in the game's language, one line per paragraph.</summary>
        public static string Desc(int id)
        {
            string s = Text("CHARM_DESC_" + id + Suffix(id, true), "");
            if (s == "") s = Text("CHARM_DESC_" + id, "");
            return s.Replace("<br><br>", "\n").Replace("<br>", "\n").Trim();
        }

        /// <summary>The game's own word for the notches and for overcharmed (UI sheet).</summary>
        public static string NotchesWord => Lang.IsSource ? "凹槽" : Text("CHARM_TXT_NOTCHES", Lang.T("凹槽"));
        public static string OvercharmedWord => Lang.IsSource ? "护符过载" : Text("CHARM_TXT_OVERCHARMED", Lang.T("护符过载"));

        // ------------------------------------------------------------------ pictures

        static readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> retry = new Dictionary<string, float>();
        static Dictionary<string, Sprite> sprites;

        static Sprite Named(string name)
        {
            if (sprites == null || !sprites.ContainsKey(name))
            {
                sprites = new Dictionary<string, Sprite>();
                foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>()) if (sp != null && !sprites.ContainsKey(sp.name)) sprites[sp.name] = sp;
            }
            return sprites.TryGetValue(name, out var s) ? s : null;
        }

        static Texture2D Picture(string key, System.Func<Sprite> find, int height)
        {
            if (pictures.TryGetValue(key, out var tex) && !retry.ContainsKey(key)) return tex;
            if (retry.TryGetValue(key, out float at) && Time.unscaledTime < at) return tex;
            Sprite s = null;
            try { s = find(); } catch { }
            var made = s != null ? DreamNailItem.Capture(s, height, "TerraKnight/" + key) : null;
            if (made == null)
            {
                // Not loaded yet: tried again shortly.
                retry[key] = Time.unscaledTime + 2f;
                return tex;
            }
            retry.Remove(key);
            pictures[key] = made;
            return made;
        }

        /// <summary>The charm's picture as the game's charm menu shows it now (broken, unbreakable, the Grimmchild's level, the royal charm's state).</summary>
        public static Texture2D Icon(int id)
        {
            var pd = Pd;
            if (pd == null) return null;
            if (id == Royal)
            {
                int st = pd.royalCharmState;
                string name = st >= 4 ? "charm_black" : st == 3 ? "charm_white_full" : st == 2 ? "charm_white_right" : "charm_white_left";
                return Picture("charm_" + name, () => Named(name), 64);
            }
            if (id >= 23 && id <= 25 && Broken(id) && !Unbreakable(id))
            {
                string name = id == 23 ? "_0002_charm_glass_heal_broken" : id == 24 ? "_0003_charm_glass_geo_broken" : "_0002_charm_glass_attack_up_broken";
                return Picture("charm_broken_" + id, () => Named(name), 64);
            }
            string key = "charm_" + id + Suffix(id, false) + (id == Grimmchild ? "_" + pd.grimmChildLevel : "");
            return Picture(key, () => CharmIconList.Instance != null ? CharmIconList.Instance.GetSprite(id) : null, 64);
        }

        /// <summary>The notch item's picture (the page's button), a notch lit and one open.</summary>
        public static Texture2D NotchIcon => Picture("charm_notch_item", () => Named("Charm_Notch"), 48);
        public static Texture2D NotchLit => Picture("charm_notch_lit", () => Named("charm_UI__0000_charm_cost_02_lit"), 46);
        public static Texture2D NotchOpen => Picture("charm_notch_open", () => Named("charm_UI__0001_charm_cost_02_unlit"), 28);

        static Texture2D overchargeIcon;

        /// <summary>The overcharmed debuff's icon: a lit notch in the charm menu's overcharmed pink.</summary>
        public static Texture2D OverchargeIcon
        {
            get
            {
                if (overchargeIcon != null) return overchargeIcon;
                var lit = NotchLit;
                if (lit == null) return Terraria.TerrariaAssets.Texture("Item_327");
                var px = lit.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32((byte)Mathf.Min(255, px[i].r * 1.0f), (byte)(px[i].g * 0.45f), (byte)(px[i].b * 0.8f), px[i].a);
                overchargeIcon = new Texture2D(lit.width, lit.height, TextureFormat.RGBA32, false) { name = "TerraKnight/charm_overcharged", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                overchargeIcon.SetPixels32(px);
                overchargeIcon.Apply(false, false);
                return overchargeIcon;
            }
        }
    }
}
