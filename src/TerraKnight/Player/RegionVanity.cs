using System.Collections.Generic;

namespace TerraKnight.Player
{
    /// <summary>
    /// The areas' costumes drawn (Items.ItemDb's region pieces, ItemDef.Region): a worn piece's sheets, and the sheets
    /// its body brings (its capes, its coat tails, a woman's legs), in its area's colours where the mod has that sheet
    /// (Resources/hk_RegionVanity_&lt;Key&gt;_&lt;vanilla sheet&gt;), else the vanilla one.
    /// </summary>
    internal static class RegionVanity
    {
        static HashSet<string> have;
        static readonly Dictionary<(int, string), string> names = new Dictionary<(int, string), string>();

        /// <summary>The sheet drawn for a vanilla one ("Armor_Head_268", "Armor/Armor_237", "Acc_Back_17") worn in an area's colours (1 + its index; 0 none).</summary>
        internal static string Sheet(int region, string vanilla)
        {
            if (region <= 0 || region > World.AreaBlocks.All.Length || vanilla == null) return vanilla;
            if (names.TryGetValue((region, vanilla), out var n)) return n;
            if (have == null)
            {
                have = new HashSet<string>();
                const string prefix = "TerraKnight.hk_", suffix = ".png";
                foreach (var r in typeof(RegionVanity).Assembly.GetManifestResourceNames())
                    if (r.StartsWith(prefix + "RegionVanity_") && r.EndsWith(suffix)) have.Add(r.Substring(prefix.Length, r.Length - prefix.Length - suffix.Length));
            }
            string mine = "RegionVanity_" + World.AreaBlocks.All[region - 1].Key + "_" + vanilla.Replace("Armor/", "");
            n = have.Contains(mine) ? mine : vanilla;
            names[(region, vanilla)] = n;
            return n;
        }
    }
}
