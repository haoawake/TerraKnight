using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wetfire", the user's 10-28 "水下不能用补全"): Terraria's weapons that can't be used in a liquid (Pl:52750) — the
    /// Flamethrower, Elf Melter, Flower of Fire, Flamelash and Arc Surge — each tried for a second standing in a poured pool
    /// in Dirtmouth (none of their shots made) and on the dry floor beside it (shots made). The Space Gun, used the same way,
    /// fires in both. The pool is drained after. Lines "[AutoTest] 水下 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WetFireTest()
        {
            TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1f);
            var r = TerrainSystem.Current.Region;
            var inv = mod.Inventory;
            bool savedInfinite = inv.Infinite;
            inv.Infinite = true;
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            int savedSel = inv.Selected;
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 水下 {what} {(pass ? "通过" : "不通过")}"); }

            // A pool walled in stone with a dry floor of its own to one side.
            const int cx = 200, floor = 40, top = 52;
            void Cell(int x, int y, CellKind kind) => TerraWorld.Set(x + r.CellOffset.x, y + r.CellOffset.y, kind);
            Vector3 At(int x, float y) => r.Root.position + new Vector3((x + 0.5f) * CellGrid.CellSize, y * CellGrid.CellSize, 0f);
            for (int y = floor - 1; y <= top + 8; y++)
                for (int x = cx - 30; x <= cx + 12; x++)
                    Cell(x, y, x == cx - 30 || x == cx + 12 || x == cx - 12 || y == floor - 1 ? CellKind.Stone : CellKind.Empty);
            for (int y = floor; y < top; y++)
                for (int x = cx - 11; x <= cx + 11; x++)
                    r.Liquid.Set(x, y, 255, LiquidKind.Water);
            yield return new WaitForSeconds(0.8f);
            var hero = HeroController.instance;
            var body = hero.GetComponent<Rigidbody2D>();

            var weapons = new[] { ItemType.Flamethrower, ItemType.ElfMelter, ItemType.FlowerofFire, ItemType.Flamelash, ItemType.ArcSurge, ItemType.SpaceGun };
            foreach (var type in weapons)
            {
                var def = ItemDb.Get(type);
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[0] = ItemStack.Of(type);
                if (def.UseAmmo == AmmoKind.Gel) inv.Slots[1] = ItemStack.Of(ItemType.Gel, 999);
                inv.Selected = 0;
                var made = new int[2];
                for (int pass = 0; pass < 2; pass++)
                {
                    bool wet = pass == 0;
                    hero.transform.position = wet ? At(cx, floor + 3) : At(cx - 21, floor + 1) + Vector3.up * 1.2f;
                    body.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(wet ? 1.5f : 1f);
                    bool inside = PouredSwimming.Wet;
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    int before = TerraProjectiles.DevMade(def.Shoot);
                    var aim = (Vector2)hero.transform.position + Vector2.right * 4f;
                    for (float end = Time.time + 1f; Time.time < end;)
                    {
                        if (wet) { hero.transform.position = At(cx, floor + 3); body.linearVelocity = Vector2.zero; }
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    made[pass] = TerraProjectiles.DevMade(def.Shoot) - before;
                    if (wet && !inside) Plugin.Log.LogInfo($"[AutoTest] 水下 {def.Name}：角色没在水里");
                    TerraProjectiles.Instance?.Clear();
                }
                bool drowns = type != ItemType.SpaceGun;
                Check(drowns ? made[0] == 0 && made[1] > 0 : made[0] > 0 && made[1] > 0,
                    $"{def.Name}：水里射出 {made[0]}，岸上射出 {made[1]}（{(drowns ? "水里不能用" : "水里照常")}）");
            }

            for (int y = 0; y < r.Grid.Height; y++)
                for (int x = 0; x < r.Grid.Width; x++)
                    if (r.Grid.GetLiquid(x, y) > 0) r.Liquid.Set(x, y, 0, LiquidKind.Water);
            for (int i = 0; i < inv.Slots.Length && i < savedSlots.Length; i++) inv.Slots[i] = savedSlots[i];
            inv.Selected = savedSel;
            inv.Infinite = savedInfinite;
            Plugin.Log.LogInfo($"[AutoTest] 水下 合计 {ok}/{all}");
        }
    }
}
