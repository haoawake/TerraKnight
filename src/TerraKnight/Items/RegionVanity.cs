using System.Collections.Generic;
using TerraKnight.World;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 24 Hallownest areas' costumes (the user's 10-25: the areas' colourings of vanilla vanity sets, made from the
    /// area's blocks, never dropped). Each piece is its vanilla piece's def (slot, sheet: the look's rules all go by that
    /// sheet) with its own name, picture and Region, sewn at a Loom from the area's block: a head 15, a body 25, legs 20.
    /// The sheets in the area's colours are Resources/hk_RegionVanity_&lt;Key&gt;_&lt;vanilla sheet&gt;
    /// (tools/vanity/import_regional_vanity.py), drawn in place of the vanilla ones (Player.RegionVanity).
    /// </summary>
    internal static partial class ItemDb
    {
        static void AddRegionVanity(List<ItemDef> list)
        {
            var vanity = new Dictionary<ItemType, ItemDef>();
            foreach (var d in list)
                if (d != null && d.Vanity) vanity[d.Type] = d;
            foreach (var (area, made, from) in regionVanity)
            {
                var a = AreaBlocks.All[area];
                var d = vanity[from].Clone();
                d.Type = made;
                d.Name = a.Name + "·" + d.Name;
                d.Tooltip = "用" + a.Block + "缝制的" + a.Name + "时装";
                d.Texture = "RegionVanity_" + a.Key + "_" + d.Texture;
                d.Region = area + 1;
                list.Add(d);
            }
        }

        /// <summary>Their stage (their area's block's), made, and the vanilla piece's value.</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> RegionVanitySources()
        {
            var value = new Dictionary<ItemType, int>();
            foreach (var (t, _, _, v) in VanitySources()) value[t] = v;
            foreach (var (area, made, from) in regionVanity)
                yield return (made, AreaBlocks.AcquisitionStage(area), ItemSource.Craft, value.TryGetValue(from, out int v) ? v : 0);
        }

        internal static IEnumerable<Recipe> RegionVanityRecipes()
        {
            // Recipes are also enumerated while ItemDb.Build is running, before defs exists.
            var native = new List<ItemDef>();
            AddVanity(native);
            var slots = new Dictionary<ItemType, ArmorSlot>();
            foreach (var item in native) slots[item.Type] = item.Armor;
            foreach (var (area, made, from) in regionVanity)
            {
                var slot = slots[from];
                int blocks = slot == ArmorSlot.Head ? 15 : slot == ArmorSlot.Body ? 25 : 20;
                yield return new Recipe(made, 1, "织布机", 0, (AreaBlocks.BlockItem(area), blocks));
            }
        }

        /// <summary>The areas' costume pieces (the audits).</summary>
        internal static IEnumerable<ItemType> RegionVanityItems()
        {
            foreach (var (_, made, _) in regionVanity) yield return made;
        }
    }
}
