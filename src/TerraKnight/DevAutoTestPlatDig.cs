using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "platdig", players' 10-25 report "在平台上挖方块会从平台上掉下来"): the hero stands on a platform row in
    /// the air and blocks round him are placed and mined with a pickaxe swung as clicks do (beside the row, over it, under
    /// it, one set on it); after each change he must still stand on the row. Lines "[AutoTest] 平台挖方块 …：通过 / 不通过".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PlatDigTest()
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var inv = mod.Inventory;
            var start = CellGrid.WorldToCell((Vector2)col.bounds.min + new Vector2(7f, 0.05f));
            int gy = start.y + 4;
            for (int k = 0; k < 40 && !TerraWorld.IsSolid(start.x, gy - 1); k++) gy--;
            int y = gy + 5, x0 = start.x - 4, x1 = start.x + 4;
            float top = (y + 1) * CellGrid.CellSize, mid = (start.x + 0.5f) * CellGrid.CellSize;
            float offset = hero.transform.position.y - col.bounds.min.y;
            var row = new List<Vector2Int>();
            for (int x = x0; x <= x1; x++) Put(row, x, y, CellKind.Platform);
            yield return new WaitForSeconds(0.4f);
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 平台挖方块 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            IEnumerator Stand()
            {
                hero.transform.position = new Vector3(mid, top + 1f + offset, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
            }
            // Watches the feet for a while after a change: the lowest they got, under the row's top.
            float lowest = 0f;
            IEnumerator Watch(float seconds)
            {
                lowest = float.MaxValue;
                for (float t = Time.time + seconds; Time.time < t;)
                {
                    lowest = Mathf.Min(lowest, col.bounds.min.y);
                    yield return new WaitForFixedUpdate();
                }
            }
            IEnumerator Mine(Vector2Int c, float seconds)
            {
                inv.Slots[5] = ItemStack.Of(ItemType.CopperPickaxe);
                inv.Selected = 5;
                inv.Override = -1;
                var at = CellGrid.CellCenter(c.x, c.y);
                lowest = float.MaxValue;
                for (float t = Time.time + seconds; Time.time < t && TerraWorld.Get(c.x, c.y) != CellKind.Empty;)
                {
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at, target: true);
                    lowest = Mathf.Min(lowest, col.bounds.min.y);
                    yield return new WaitForFixedUpdate();
                }
                float before = lowest;
                yield return Watch(0.8f);
                lowest = Mathf.Min(lowest, before);
            }
            var cases = new (string what, Vector2Int cell)[]
            {
                ("平台同一行旁边 2 格", new Vector2Int(x1 + 2, y)),
                ("平台上方、脚边 2 格", new Vector2Int(start.x + 2, y + 1)),
                ("平台上方、头顶", new Vector2Int(start.x, y + 4)),
                ("平台正下方", new Vector2Int(start.x + 1, y - 1)),
                ("平台下方远一点", new Vector2Int(start.x - 3, y - 3)),
            };
            foreach (var (what, cell) in cases)
            {
                yield return Stand();
                bool stood = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f;
                int letBy = Player.TerraMovement.DevPlatformsLetBy;
                // Set by hand (another chunk's rebuild, a block placed by anything).
                TerraWorld.Set(cell.x, cell.y, CellKind.Dirt);
                yield return Watch(0.8f);
                bool kept = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f && lowest > top - 0.15f;
                Check($"{what}：放下泥土后还站在平台上", stood && kept,
                    $"放前站住 {stood}，最低脚 {lowest - top:F3}，放行 {Player.TerraMovement.DevPlatformsLetBy - letBy}；{Contacts(hero)}");
                if (!kept) { Shot("Town", "platdig_place"); yield return null; yield return Stand(); }
                letBy = Player.TerraMovement.DevPlatformsLetBy;
                yield return Mine(cell, 6f);
                bool mined = TerraWorld.Get(cell.x, cell.y) == CellKind.Empty;
                kept = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f && lowest > top - 0.15f;
                Check($"{what}：拿铜镐挖掉以后还站在平台上", mined && kept,
                    $"挖掉 {mined}，最低脚 {lowest - top:F3}，放行 {Player.TerraMovement.DevPlatformsLetBy - letBy}；{Contacts(hero)}");
                if (!kept) { Shot("Town", "platdig_mine"); yield return null; }
                TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
            }
            // Standing at the row's end: the blocks against its end (beside it, over and under that) mined with the cursor
            // on each one, smart cursor off and on.
            foreach (bool smart in new[] { false, true })
            {
                mod.DevSetSmartCursor(smart);
                foreach (var cell in new[] { new Vector2Int(x1 + 1, y), new Vector2Int(x1 + 1, y + 1), new Vector2Int(x1 + 1, y - 1), new Vector2Int(x1, y - 1) })
                {
                    float endX = (x1 + 0.5f) * CellGrid.CellSize - 0.1f;
                    hero.transform.position = new Vector3(endX, top + 1f + offset, hero.transform.position.z);
                    rb.linearVelocity = Vector2.zero;
                    TerraWorld.Set(cell.x, cell.y, CellKind.Dirt);
                    yield return new WaitForSeconds(1f);
                    bool stood = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f;
                    int letBy = Player.TerraMovement.DevPlatformsLetBy;
                    yield return Mine(cell, 6f);
                    bool mined = TerraWorld.Get(cell.x, cell.y) == CellKind.Empty;
                    bool rowWhole = true;
                    foreach (var c in row) rowWhole &= Cells.IsPlatform(TerraWorld.Get(c.x, c.y));
                    bool kept = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f && lowest > top - 0.15f;
                    Check($"站在平台一端，挖紧挨端头的 ({cell.x - x1},{cell.y - y})（智能光标{(smart ? "开" : "关")}）",
                        stood && kept && rowWhole,
                        $"挖前站住 {stood}，挖掉 {mined}，平台完整 {rowWhole}，最低脚 {lowest - top:F3}，放行 {Player.TerraMovement.DevPlatformsLetBy - letBy}；{Contacts(hero)}");
                    if (!kept || !rowWhole) { Shot("Town", $"platdig_end_{(smart ? 1 : 0)}_{cell.x - x1}_{cell.y - y}"); yield return null; }
                    TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
                    foreach (var c in row) if (!Cells.IsPlatform(TerraWorld.Get(c.x, c.y))) TerraWorld.Set(c.x, c.y, CellKind.Platform);
                    yield return new WaitForSeconds(0.3f);
                }
            }
            mod.DevSetSmartCursor(false);
            // A block set on the row by the hand, as clicks do.
            {
                yield return Stand();
                var cell = new Vector2Int(start.x + 3, y + 1);
                inv.Slots[6] = ItemStack.Of(ItemType.DirtBlock, 10);
                int letBy = Player.TerraMovement.DevPlatformsLetBy;
                mod.DevUse(ItemType.DirtBlock, CellGrid.CellCenter(cell.x, cell.y));
                yield return Watch(0.8f);
                bool kept = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f && lowest > top - 0.15f;
                Check("平台上放一块泥土后还站着", TerraWorld.IsSolid(cell.x, cell.y) && kept,
                    $"放下 {TerraWorld.IsSolid(cell.x, cell.y)}，最低脚 {lowest - top:F3}，放行 {Player.TerraMovement.DevPlatformsLetBy - letBy}；{Contacts(hero)}");
                TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
            }
            // The row itself changed under him: a platform added at its end, one taken off its far end.
            {
                yield return Stand();
                int letBy = Player.TerraMovement.DevPlatformsLetBy;
                TerraWorld.Set(x1 + 1, y, CellKind.Platform);
                yield return Watch(0.8f);
                TerraWorld.Set(x1 + 1, y, CellKind.Empty);
                yield return Watch(0.8f);
                bool kept = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.2f && lowest > top - 0.15f;
                Check("平台这一行自己加长又缩短后还站着", kept,
                    $"最低脚 {lowest - top:F3}，放行 {Player.TerraMovement.DevPlatformsLetBy - letBy}；{Contacts(hero)}");
            }
            foreach (var c in row) TerraWorld.Set(c.x, c.y, CellKind.Empty);
            Plugin.Log.LogInfo($"[AutoTest] 平台挖方块 合计 {ok}/{all}");
        }
    }
}
