using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "recipegroups"): every recipe made from exactly its own ingredients, its station taken as in reach: the
    /// crafting column offers it and a click makes it (the vanilla variants among them: Rich Mahogany for wood, lead for
    /// iron, the Platinum Watch for the Gold Watch...). Recipes that need a liquid or a biome around are left out.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RecipeGroupsTest()
        {
            if (!DevCopy.Verified) yield break;
            mod.ApplyCreative(false);
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            var savedBag = (ItemStack[])inv.HkBag.Clone();
            var savedMouse = inv.Mouse;
            // A creative save makes everything for free from a list of its own: the real crafting is the one under test.
            bool freeWas = inv.FreeMaking;
            inv.FreeMaking = false;
            InventoryGui.DevAnyStation = true;
            int tried = 0, made = 0;
            var failed = new List<string>();
            var failedVariants = new List<string>();
            var named = new[] { ItemType.Sawmill, ItemType.GPS, ItemType.CellPhone, ItemType.IronPickaxe, ItemType.WorkBench };
            try
            {
                foreach (var r in Recipes.All.ToList())
                {
                    if (r.IsBought || r.Environment != CraftEnvironment.None || r.Ingredients.Length == 0) continue;
                    System.Array.Clear(inv.Slots, 0, inv.Slots.Length);
                    System.Array.Clear(inv.HkBag, 0, inv.HkBag.Length);
                    inv.Mouse = default;
                    int slot = 0;
                    foreach (var (t, n) in r.Ingredients)
                    {
                        int left = n, max = Mathf.Max(1, ItemDb.Get(t).MaxStack);
                        while (left > 0 && slot < 40)
                        {
                            int put = Mathf.Min(left, max);
                            inv.Slots[slot++] = ItemStack.Of(t, put);
                            left -= put;
                        }
                    }
                    tried++;
                    string why = InventoryGui.DevCraftRecipe(r);
                    if (why == null) { made++; continue; }
                    string line = $"{ItemDb.Get(r.Result).Name}←{string.Join("+", r.Ingredients.Select(g => ItemDb.Get(g.Type).Name + "×" + g.Count))}（{r.Station}）{why}";
                    failed.Add(line);
                    if (r.IngredientAlternative) failedVariants.Add(line);
                    if (yieldEvery++ % 200 == 0) yield return null;
                }
                // The user's own examples, reported one by one.
                foreach (var t in named)
                    foreach (var r in Recipes.All.Where(x => x.Result == t))
                        Plugin.Log.LogInfo($"[AutoTest] 配方变体 {ItemDb.Get(t).Name} ← {string.Join("+", r.Ingredients.Select(g => ItemDb.Get(g.Type).Name + "×" + g.Count))}：{(failed.Any(f => f.StartsWith(ItemDb.Get(t).Name + "←" + string.Join("+", r.Ingredients.Select(g => ItemDb.Get(g.Type).Name + "×" + g.Count)))) ? "做不出" : "能做")}");
            }
            finally
            {
                InventoryGui.DevAnyStation = false;
                inv.FreeMaking = freeWas;
                System.Array.Copy(savedSlots, inv.Slots, inv.Slots.Length);
                System.Array.Copy(savedBag, inv.HkBag, inv.HkBag.Length);
                inv.Mouse = savedMouse;
            }
            Plugin.Log.LogInfo($"[AutoTest] 配方变体 做不出的 {failed.Count} 条（其中同类替代 {failedVariants.Count} 条）：{string.Join("；", failed.Take(80))}");
            Plugin.Log.LogInfo($"[AutoTest] 配方变体 合计 {made}/{tried} {(made == tried ? "通过" : "不通过")}");
        }

        int yieldEvery;
    }
}
