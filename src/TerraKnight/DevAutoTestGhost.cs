using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ghost"): the placement preview and Auto Select. A Work Bench in hand: on the ground in reach the
    /// preview shows and fits; in the air shows red; out of reach none. Shift held: on the bench set down, the pickaxe;
    /// on the ground, the pickaxe; on near air, a torch; on far air with flares, the Flare Gun.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GhostTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 放置虚影 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(1.5f);
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(ItemType.WorkBench, 5);
            inv.Slots[1] = ItemStack.Of(ItemType.CopperPickaxe, 1);
            inv.Slots[2] = ItemStack.Of(ItemType.Torch, 20);
            inv.Slots[3] = ItemStack.Of(ItemType.FlareGun, 1);
            inv.Slots[Inventory.HotbarSize + 2] = ItemStack.Of(ItemType.Flare, 50);
            inv.Selected = 0;
            var col = hero.GetComponent<BoxCollider2D>();
            var b = col.bounds;
            var ground = new Vector2(b.center.x + 1.6f, b.min.y + 0.2f);
            mod.DevAim = ground;
            yield return null;
            yield return null;
            Check("地上够得着：显示且能放", PlacementGhost.DevShown && PlacementGhost.DevValid, $"显示 {PlacementGhost.DevShown}，能放 {PlacementGhost.DevValid}，{Stations.DevWhy}");
            Shot("Town", "ghost_valid");
            mod.DevAim = new Vector2(b.center.x + 1.6f, b.max.y + 0.5f);
            yield return null;
            yield return null;
            Check("空中：红色", PlacementGhost.DevShown && !PlacementGhost.DevValid, $"显示 {PlacementGhost.DevShown}，能放 {PlacementGhost.DevValid}");
            Shot("Town", "ghost_invalid");
            mod.DevAim = new Vector2(b.center.x + 12f, b.min.y + 0.2f);
            yield return null;
            yield return null;
            Check("够不着：不显示", !PlacementGhost.DevShown, $"显示 {PlacementGhost.DevShown}");

            // A bench set down, then Shift.
            bool placed = Stations.TryPlace(StationKind.WorkBench, hero, ground);
            if (placed) inv.ConsumeSelected();
            yield return null;
            var benchBox = Stations.DevBoxOf(StationKind.WorkBench);
            TerraMod.DevAutoSelect = true;
            string Held() => inv.Current.IsEmpty ? "空" : inv.Current.Def.Name;
            mod.DevAim = benchBox.HasValue ? benchBox.Value.center : ground;
            yield return null;
            yield return null;
            Check("指着工作台：镐", placed && inv.Current.Type == ItemType.CopperPickaxe, $"放下 {placed}，手里 {Held()}");
            mod.DevAim = new Vector2(b.center.x - 0.8f, b.min.y - 0.3f);
            yield return null;
            yield return null;
            Check("指着脚下地面：镐", inv.Current.Type == ItemType.CopperPickaxe, $"手里 {Held()}，那格 {TerraWorld.Get(CellGrid.WorldToCell(mod.DevAim.Value).x, CellGrid.WorldToCell(mod.DevAim.Value).y)}");
            mod.DevAim = (Vector2)b.center + new Vector2(1.2f, 1.2f);
            yield return null;
            yield return null;
            Check("近处空气：火把", inv.Current.Type == ItemType.Torch, $"手里 {Held()}");
            mod.DevAim = (Vector2)b.center + new Vector2(9f, 2f);
            yield return null;
            yield return null;
            Check("远处空气：信号枪", inv.Current.Type == ItemType.FlareGun, $"手里 {Held()}");
            TerraMod.DevAutoSelect = false;
            mod.DevAim = null;
            yield return null;
            inv.Infinite = infinite;
            Plugin.Log.LogInfo($"[AutoTest] 放置虚影 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
