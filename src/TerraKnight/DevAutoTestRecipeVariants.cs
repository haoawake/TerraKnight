using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator RecipeVariantsTest()
        {
            yield return VanillaRecipesTest();
            // Keep each crafted output for the next step: verify upgrade chains,
            // not just independently seeded final recipes.
            InventoryGui.Open = true;
            var inv = mod.Inventory;
            bool Step(ItemType result, ItemType lower)
            {
                var recipe = Recipes.All.FirstOrDefault(r => r.Result == result
                    && r.Ingredients.Any(i => i.Type == lower));
                if (recipe == null || Recipes.Count(inv, lower) != 1) return false;
                foreach (var ingredient in recipe.Ingredients.Where(i => i.Type != lower))
                    inv.Add(ingredient.Type, ingredient.Count);
                bool made = InventoryGui.DevCraft(result);
                bool pass = made && inv.Mouse.Type == result && inv.Mouse.Count == recipe.Count
                    && recipe.Ingredients.All(i => Recipes.Count(inv, i.Type) == 0);
                if (pass) { inv.Add(inv.Mouse.Type, inv.Mouse.Count); inv.Mouse = default; }
                return pass;
            }
            foreach (var boots in new[] { ItemType.HermesBoots, ItemType.FlurryBoots, ItemType.SailfishBoots, ItemType.SandBoots })
            {
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default;
                inv.Add(boots, 1);
                bool pass = Recipes.Using(boots).Any(r => r.Result == ItemType.SpectreBoots)
                    && Step(ItemType.SpectreBoots, boots)
                    && Step(ItemType.LightningBoots, ItemType.SpectreBoots)
                    && Step(ItemType.FrostsparkBoots, ItemType.LightningBoots);
                Plugin.Log.LogInfo($"[AutoTest] 跑鞋连续升级 {boots} -> SpectreBoots -> LightningBoots -> FrostsparkBoots {(pass ? "通过" : "不通过")}");
            }
            foreach (var emblem in new[] { ItemType.WarriorEmblem, ItemType.RangerEmblem, ItemType.SorcererEmblem, ItemType.SummonerEmblem })
            {
                bool visible = Recipes.Using(emblem).Any(r => r.Result == ItemType.AvengerEmblem);
                bool material = Recipes.IsMaterial(emblem);
                Plugin.Log.LogInfo($"[AutoTest] 徽章向导制作入口 {emblem} {(visible && material ? "通过" : "不通过")}");
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default;
                inv.Add(emblem, 1);
                bool pass = Step(ItemType.AvengerEmblem, emblem)
                    && Step(ItemType.DestroyerEmblem, ItemType.AvengerEmblem);
                Plugin.Log.LogInfo($"[AutoTest] 纹章连续升级 {emblem} -> AvengerEmblem -> DestroyerEmblem {(pass ? "通过" : "不通过")}");
            }
            InventoryGui.Open = false;
            var items = ItemDb.All.ToDictionary(d => d.Type.ToString(), d => new { d.Texture });
            var recipes = Recipes.All.Select(r => new { Result=r.Result.ToString(), r.Count, r.Station,
                Environment=r.Environment.ToString(), Ingredients=r.Ingredients.Select(i => new { Type=i.Type.ToString(), i.Count }).ToArray() }).ToArray();
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"recipe-variants-runtime.json"), JsonConvert.SerializeObject(new {items,recipes},Formatting.Indented));
        }
    }
}
