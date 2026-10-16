using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator VanillaRecipesTest()
        {
            yield return GoTo("Town", "left1");
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            // Real crafting now requires real nearby stations; Dirtmouth is not a station substitute.
            // These fixtures belong only to the isolated suite and are never saved.
            if (!DevCopy.Verified) throw new System.InvalidOperationException("Isolated saves required");
            var hero = HeroController.instance;
            var region = TerrainSystem.Current.Region;
            Stations.Clear();
            foreach (StationKind kind in System.Enum.GetValues(typeof(StationKind)))
                if (kind != StationKind.None && !Relics.Looks.ContainsKey(kind))
                    Stations.PlaceFixed(kind, region.SceneName, (Vector2)hero.transform.position - (Vector2)region.Root.position + Vector2.right * 2f);
            yield return null;
            Shop.DevStage = 5;
            int oldEntry = Bestiary.KillsOf("InfectedKnight"), oldFinal = Bestiary.KillsOf("FinalBoss");
            Bestiary.Kills["InfectedKnight"] = Bestiary.Kills["FinalBoss"] = 1;
            var inv = mod.Inventory;
            inv.Override = -1;
            InventoryGui.Open = true;
            int passed = 0, total = 0;
            foreach (var r in Recipes.All.Where(r => !r.IsBought && r.Def.Texture.StartsWith("Item_")))
            {
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length);
                inv.Mouse = default;
                foreach (var ing in r.Ingredients) inv.Add(ing.Type, ing.Count);
                bool available = CraftEnvironments.Available(r.Environment);
                bool made = InventoryGui.DevCraft(r.Result);
                bool correct = available
                    ? made && inv.Mouse.Type == r.Result && inv.Mouse.Count == r.Count
                        && r.Ingredients.All(g => Recipes.Count(inv, g.Type) == 0)
                    : !made && inv.Mouse.IsEmpty
                        && r.Ingredients.All(g => Recipes.Count(inv, g.Type) == g.Count);
                total++;
                if (correct) passed++;
                else Plugin.Log.LogError($"[AutoTest] 原版配方 {r.Result} / {r.Station} / {r.Environment} 不通过");
            }
            Plugin.Log.LogInfo($"[AutoTest] 原版配方实际制作、材料扣除及环境限制 {passed}/{total} {(passed == total ? "通过" : "不通过")}");
            if (oldEntry == 0) Bestiary.Kills.Remove("InfectedKnight"); else Bestiary.Kills["InfectedKnight"] = oldEntry;
            if (oldFinal == 0) Bestiary.Kills.Remove("FinalBoss"); else Bestiary.Kills["FinalBoss"] = oldFinal;
            InventoryGui.Open = false;
        }
    }
}
