using TerraKnight.Terraria;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Every item's name and tooltip in the language now (Lang): kept as written (Simplified
        /// Chinese) in the mod, otherwise Terraria's own for the item (by its id, Item_&lt;id&gt;) where the
        /// mod's text is Terraria's, and the mod's table where the mod wrote its own. The Chinese is
        /// kept aside to come back to.
        /// </summary>
        public static void Relocalize()
        {
            string c = Lang.Culture;
            int done = 0, vanilla = 0, own = 0, missing = 0;
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (d.ZhName == null)
                {
                    // An item without a real definition (named after its enum) stays as it is.
                    if (d.Name == d.Type.ToString()) continue;
                    d.ZhName = d.Name;
                    d.ZhTooltip = d.Tooltip ?? "";
                }
                if (d.BossSummonKey != null) { BossSummons.Localize(d); done++; continue; }
                if (Relics.Is(d.Type)) { Relics.Localize(d); done++; continue; }
                if (TreasureBags.Is(d.Type)) { TreasureBags.Localize(d); done++; continue; }
                if (Containers.Localize(d)) { done++; continue; }
                if (d.HkItem) { HkItems.Localize(d); done++; continue; }
                // A quest fish's "Caught in ...": Hallownest's areas where it is.
                if (d.QuestFish)
                {
                    if (QuestFishCatalog.Localize(d)) { done++; continue; }
                    int qid = TerrariaId(d);
                    d.Name = Lang.IsSource ? d.ZhName : Lang.Pick(d.ZhName, TerrariaText.ItemName(Lang.Source, qid), TerrariaText.ItemName(c, qid));
                    d.Tooltip = Fishing.QuestTooltip(d.Type);
                    done++;
                    continue;
                }
                // An area's costume: Hollow Knight's own name for the area, Terraria's for the piece it recolours.
                if (d.Region > 0) { RegionVanityName(d, c); done++; continue; }
                int id = TerrariaId(d);
                if (Lang.IsSource || id <= 0)
                {
                    d.Name = Lang.IsSource ? d.ZhName : Lang.T(d.ZhName);
                    d.Tooltip = Lang.IsSource ? d.ZhTooltip : Lang.T(d.ZhTooltip);
                    done++;
                    continue;
                }
                d.Name = Lang.Pick(d.ZhName, TerrariaText.ItemName(Lang.Source, id), TerrariaText.ItemName(c, id));
                // Mount acquisition and controls are mod-specific; keep them when no full translation exists.
                if (d.IsMount) { d.Tooltip=Lang.IsSource ? d.ZhTooltip : (Lang.Lookup(d.ZhTooltip,c) ?? (c.StartsWith("zh") ? d.ZhTooltip : Player.Mounts.EnglishTooltip(d))); done++; continue; }
                if (string.IsNullOrEmpty(d.ZhTooltip)) d.Tooltip = d.ZhTooltip;
                else
                {
                    string vz = TerrariaText.ItemTooltip(Lang.Source, id);
                    bool same = vz != null && Lang.Same(d.ZhTooltip, vz);
                    if (same) vanilla++;
                    else if (Lang.Has(d.ZhTooltip)) own++;
                    else missing++;
                    d.Tooltip = same ? Lang.Pick(d.ZhTooltip, vz, TerrariaText.ItemTooltip(c, id)) : Tooltip(d.ZhTooltip, vz, TerrariaText.ItemTooltip(c, id), c);
                }
                done++;
            }
            if (!Lang.IsSource) Plugin.Log.LogInfo($"[语言] 物品 {done} 件换成 {c}：说明照泰拉瑞亚 {vanilla}，mod 自己翻译 {own}，还没翻译 {missing}");
        }

        /// <summary>
        /// A tooltip the mod wrote itself, line by line: a line that is a line of the item's own Terraria
        /// tooltip takes Terraria's line in the language; any other the table's (Lang.Line). A line
        /// still without one leaves the whole to Terraria's tooltip, or the Chinese.
        /// </summary>
        static string Tooltip(string zh, string vanillaZh, string vanillaLocal, string culture)
        {
            var whole = Lang.Lookup(zh, culture);
            if (whole != null) return whole;
            string[] vz = vanillaZh != null ? vanillaZh.Split('\n') : new string[0];
            string[] vl = vanillaLocal != null ? vanillaLocal.Split('\n') : new string[0];
            var lines = zh.Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i], done = null;
                int at = System.Array.FindIndex(vz, v => Lang.Same(v, line));
                if (at >= 0 && vz.Length == vl.Length) done = vl[at];
                done = done ?? Lang.Line(line, culture);
                if (done == null)
                {
                    DevMissingLines.Add(line);
                    return string.IsNullOrEmpty(vanillaLocal) ? zh : vanillaLocal;
                }
                if (i > 0) sb.Append('\n');
                sb.Append(done);
            }
            return sb.ToString();
        }

        /// <summary>Dev: tooltip lines that had no translation at the last change of language.</summary>
        public static readonly System.Collections.Generic.HashSet<string> DevMissingLines = new System.Collections.Generic.HashSet<string>();

        /// <summary>The Terraria item an item is (its texture Item_&lt;id&gt;), or 0.</summary>
        /// <summary>An area's costume piece named in a language: "国王山道·永恒旅人兜帽", "King's Pass Timeless Traveler's Hood".</summary>
        static void RegionVanityName(ItemDef d, string c)
        {
            if (Lang.IsSource) { d.Name = d.ZhName; d.Tooltip = d.ZhTooltip; return; }
            var a = World.AreaBlocks.All[d.Region - 1];
            int cut = d.Texture.LastIndexOf("_Item_", System.StringComparison.Ordinal);
            int id = cut >= 0 && int.TryParse(d.Texture.Substring(cut + 6), out int v) ? v : 0;
            string area = World.Areas.Display(a.Name);
            string piece = (id > 0 ? TerrariaText.ItemName(c, id) : null) ?? d.ZhName;
            d.Name = c.StartsWith("zh") ? area + "·" + piece : area + " " + piece;
            d.Tooltip = Lang.F("用{0}缝制的{1}时装", Lang.T(a.Block), area);
        }

        static int TerrariaId(ItemDef d)
        {
            var t = d.Texture;
            if (t == null || !t.StartsWith("Item_")) return 0;
            return int.TryParse(t.Substring(5), out int id) ? id : 0;
        }

        /// <summary>Dev: items whose tooltip is the mod's own (not Terraria's Chinese), with no line in the table yet.</summary>
        public static System.Collections.Generic.List<string> DevUntranslated()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var d in defs)
            {
                if (d == null || d.ZhName == null) continue;
                int id = TerrariaId(d);
                string vn = id > 0 ? TerrariaText.ItemName(Lang.Source, id) : null;
                if ((vn == null || !Lang.Same(vn, d.ZhName)) && !Lang.Has(d.ZhName)) list.Add(d.ZhName);
                if (string.IsNullOrEmpty(d.ZhTooltip)) continue;
                string vt = id > 0 ? TerrariaText.ItemTooltip(Lang.Source, id) : null;
                if ((vt == null || !Lang.Same(vt, d.ZhTooltip)) && !Lang.Has(d.ZhTooltip)) list.Add(d.ZhTooltip);
            }
            return list;
        }
    }
}
