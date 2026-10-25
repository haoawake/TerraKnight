using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Town
{
    /// <summary>
    /// The generated townsfolk's words (Terraria's own: NPC.GetChat's LegacyDialog lines and the Chatter families, the names
    /// of the others in town put in), the Nurse's healing (Main.NPCChatText_Nurse: the life missing, dearer the further the
    /// world has gone) and the Tax Collector's taxes (Player.taxMoney: a silver a minute for each resident, up to ten gold;
    /// a geo is a silver here).
    /// </summary>
    internal static partial class TownNpcs
    {
        static string ChatOf(Npc n)
        {
            var keys = n.Def.Chat;
            string culture = Lang.IsSource ? Lang.Source : Lang.Culture;
            for (int tries = 0; tries < 6; tries++)
            {
                string key = keys[Random.Range(0, keys.Length)];
                string text = Terraria.TerrariaText.Get(culture, key) ?? Terraria.TerrariaText.Get("en-US", key);
                if (string.IsNullOrEmpty(text) || !Holds(ref text)) continue;
                return Fill(text);
            }
            return "……";
        }

        /// <summary>
        /// A line's own conditions ("{?Day}", "{?!Hardmode}", Terraria's LanguageSearchFilter): by day or night, hardmode
        /// (the Monarch Wings on); any other (a boss of Terraria's beaten, rain, a party) never holds here. True when the line
        /// may be said, with its conditions taken off.
        /// </summary>
        static bool Holds(ref string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"^\{\?(!?)(\w+)\}");
            while (m.Success)
            {
                bool not = m.Groups[1].Value == "!";
                bool value;
                switch (m.Groups[2].Value)
                {
                    case "Day": value = World.TerraTime.IsDay; break;
                    case "Night": value = !World.TerraTime.IsDay; break;
                    case "Hardmode": value = Shop.UnlockedStage() >= 4; break;
                    default: value = false; break;
                }
                if (value == not) return false;
                text = text.Substring(m.Length);
                m = System.Text.RegularExpressions.Regex.Match(text, @"^\{\?(!?)(\w+)\}");
            }
            return true;
        }

        /// <summary>The names in a line: the townsfolk's given names, the player's, the world's (Hallownest).</summary>
        static string Fill(string text)
        {
            foreach (var o in npcs)
            {
                if (string.IsNullOrEmpty(o.Given)) continue;
                text = text.Replace("{" + o.Def.Internal + "}", o.Given);
            }
            text = text.Replace("{PlayerName}", Lang.T("泰拉人")).Replace("{WorldName}", Lang.T("圣巢"));
            // A name of one not in town: the type's name.
            return System.Text.RegularExpressions.Regex.Replace(text, @"\{([A-Za-z]+)\}", m =>
            {
                foreach (var d in All)
                    if (d.Internal == m.Groups[1].Value) return Terraria.TerrariaText.NpcName(Lang.IsSource ? Lang.Source : Lang.Culture, d.Id) ?? d.Zh;
                return m.Value == "{Nurse}" || m.Value.Length < 3 ? m.Value : "";
            });
        }

        // ------------------------------------------------------------------ the Nurse

        /// <summary>The price of a full heal in copper (Main.NPCChatText_Nurse: the life missing, doubled for each stage reached past the first), or 0.</summary>
        public static long NursePrice()
        {
            var stats = TerraMod.Instance?.Stats;
            if (stats == null) return 0;
            int missing = Mathf.Max(0, stats.LifeMax - stats.Life);
            if (missing == 0) return 0;
            int stage = Shop.UnlockedStage();
            long price = missing * (1L << Mathf.Min(stage, 5));
            if (World.GameDifficulty.Current >= World.DifficultyLevel.Expert) price = (long)(price * 0.75f);
            return Mathf.Max(1, (int)price);
        }

        public static void NurseHeal()
        {
            if (!TalkingIsNurse) return;
            long price = NursePrice();
            if (price <= 0) { ChatText = Lang.T("你看起来很健康。"); return; }
            if (!Shop.Spend(price)) { ChatText = Lang.T("你没有足够的钱。"); return; }
            var stats = TerraMod.Instance.Stats;
            stats.Life = stats.LifeMax;
            TerraMod.Instance.PlaySound("Item_4");
            ChatText = Lang.T("好了，你的伤都治好了。");
        }

        // ------------------------------------------------------------------ the Tax Collector

        /// <summary>The taxes waiting, in copper (Player.taxMoney), kept with the character.</summary>
        public static long Taxes;
        public static float TaxClock;
        /// <summary>Player.CollectTaxes: 50 copper a townsperson a time, up to 25 gold.</summary>
        public const long TaxEach = 50, TaxMost = 250000;

        /// <summary>
        /// Main.UpdateTime / Player.CollectTaxes: while the Tax Collector lives in town, every 60 s of play (Player.taxRate,
        /// 3600 ticks), wherever the player is, 50 copper for each townsperson with a home (not the pets, not the Traveling
        /// Merchant), up to 25 gold waiting.
        /// </summary>
        static void CollectTaxTime()
        {
            int stage = Shop.UnlockedStage();
            var collector = System.Array.Find(All, d => d.Id == 441);
            if (collector == null || !InTown(collector, stage)) return;
            TaxClock += Time.deltaTime;
            if (TaxClock < 60f) return;
            TaxClock -= 60f;
            if (Taxes >= TaxMost) return;
            int housed = 0;
            foreach (var d in All)
                if (d.Kind != NpcKind.Visitor && d.Kind != NpcKind.Pet && InTown(d, stage)) housed++;
            Taxes = System.Math.Min(TaxMost, Taxes + TaxEach * housed);
        }

        public static void TaxCollect()
        {
            if (!TalkingIsTaxCollector) return;
            if (Taxes <= 0) { ChatText = Lang.T("现在还没有税金。"); return; }
            Shop.Earn(Taxes);
            ChatText = Lang.F("这是收来的税金：{0}。", Shop.Money(Taxes));
            Taxes = 0;
            TerraMod.Instance?.PlaySound("Coins");
        }
    }
}
