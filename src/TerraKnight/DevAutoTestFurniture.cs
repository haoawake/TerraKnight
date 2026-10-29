using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "furniture"): platforms stood on with the inventory and its crafting column open; a
    /// Work Bench stood on, dropped through with down held, an Iron Anvil and a Bottle set on benches;
    /// the ground dug from under a bench (bench and anvil fall back into items); the ground under a
    /// chest kept; no block placed inside a station.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FurnitureTest()
        {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            const float c = CellGrid.CellSize;
            // A fixed interior test pad avoids native exit triggers and uneven generated terrain.
            for(int x=54;x<=96;x++)for(int y=20;y<=43;y++)TerraWorld.Set(x,y,y==20?CellKind.Stone:CellKind.Empty);
            yield return new WaitForSeconds(.5f);
            hero.transform.position=Clear(hero,CellGrid.CellCenter(60,24));
            rb.linearVelocity=Vector2.zero;
            yield return new WaitForSeconds(2f);
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 家具 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            IEnumerator Land(float seconds = 3f)
            {
                float t = Time.time;
                while (Time.time - t < 0.2f) yield return null;
                while (Time.time - t < seconds && !hero.cState.onGround) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            float Feet() => hero.GetComponent<BoxCollider2D>().bounds.min.y;
            float Mid() => hero.GetComponent<BoxCollider2D>().bounds.center.x;
            for (int i = 0; i < mod.Inventory.Slots.Length; i++)
                if (mod.Inventory.Slots[i].Type == ItemType.WorkBench || mod.Inventory.Slots[i].Type == ItemType.IronAnvil) mod.Inventory.Slots[i] = default;
            yield return Land();
            // The floor where the hero stands, and a stretch of it to the right with room above.
            var foot = CellGrid.WorldToCell(new Vector2(Mid(), Feet() - 0.05f));
            int gx = foot.x + 6;
            int floor = foot.y;
            while (!TerraWorld.IsSolid(gx, floor) && floor > foot.y - 6) floor--;
            while (TerraWorld.IsSolid(gx, floor + 1) && floor < foot.y + 6) floor++;

            // 1. A row of platforms 4 cells up: stood on, the inventory and crafting open, still stood on.
            int py = floor + 5;
            for (int x = gx; x < gx + 6; x++) TerraWorld.Set(x, py, CellKind.Platform);
            yield return null;
            hero.transform.position = Clear(hero, CellGrid.CellCenter(gx + 3, py + 3));
            rb.linearVelocity = Vector2.zero;
            yield return Land();
            float onPlat = Feet();
            InventoryGui.Open = true;
            yield return new WaitForSeconds(2f);
            float withInv = Feet();
            bool stillGround = hero.cState.onGround;
            InventoryGui.Open = false;
            Check("平台上打开背包和制造栏", Mathf.Abs(onPlat - (py + 1) * c) < 0.1f && Mathf.Abs(withInv - onPlat) < 0.05f && stillGround,
                $"站上平台脚底 {onPlat:F2}（平台顶 {(py + 1) * c:F2}），打开 2 秒后 {withInv:F2}，在地上 {stillGround}");
            for (int x = gx; x < gx + 6; x++) TerraWorld.Set(x, py, CellKind.Empty);
            yield return Land();

            // 2. A Work Bench on the floor, stood on.
            int placed0 = Stations.DevPlaced;
            var benchAim = CellGrid.CellCenter(gx + 1, floor + 1);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(gx - 2, floor + 2));
            rb.linearVelocity = Vector2.zero;
            yield return Land();
            bool bench = Stations.TryPlace(StationKind.WorkBench, hero, new Vector2(gx * c + c, (floor + 1) * c + 0.2f));
            yield return null;
            var bb = Stations.DevBoxNear(benchAim);
            Check("放工作台", bench, $"放在 {bb.min:F2}..{bb.max:F2}");
            if (!bench) yield break;
            yield return new WaitForSeconds(0.2f);
            hero.transform.position = new Vector3(bb.center.x, bb.yMax + 1.5f, hero.transform.position.z);
            rb.linearVelocity = Vector2.zero;
            yield return Land();
            var tops = new System.Text.StringBuilder();
            foreach (var col in Object.FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (col.name == "Terra Station Top") tops.Append($" [{PathOf(col.transform)} 激活 {col.gameObject.activeInHierarchy} 启用 {col.enabled} 层 {col.gameObject.layer} 框 {(Vector2)col.bounds.min:F2}..{(Vector2)col.bounds.max:F2} 效果器 {col.usedByEffector}]");
            var ray = Physics2D.Raycast(new Vector2(bb.center.x, bb.yMax + 0.5f), Vector2.down, 2f, 1 << ChunkColliders.TerrainLayer);
            Check("站在工作台上", hero.cState.onGround && Mathf.Abs(Feet() - bb.yMax) < 0.1f, $"脚底 {Feet():F2}，台面 {bb.yMax:F2}，在地上 {hero.cState.onGround}；台面碰撞体{tops}；从上往下射线打到 {(ray.collider != null ? PathOf(ray.collider.transform) + " " + ray.point.ToString("F2") : "无")}");

            // 3. Down held on it: through.
            int drops0 = Stations.DevDrops;
            Player.TerraMovement.DevDown = true;
            float lowest = Feet();
            float td = Time.time;
            while (Time.time - td < 0.5f) { lowest = Mathf.Min(lowest, Feet()); yield return null; }
            Player.TerraMovement.DevDown = false;
            yield return Land();
            Check("按住下穿过工作台", Feet() < bb.yMax - 0.2f, $"脚底 {Feet():F2}（最低 {lowest:F2}），台面 {bb.yMax:F2}，台面让开了 {Stations.DevDrops - drops0} 次，在地上 {hero.cState.onGround}");
            hero.transform.position = Clear(hero, CellGrid.CellCenter(gx - 2, floor + 2));
            rb.linearVelocity = Vector2.zero;
            yield return Land();

            // 4. An Iron Anvil on the bench.
            bool anvil = Stations.TryPlace(StationKind.IronAnvil, hero, new Vector2(bb.center.x, bb.yMax + 0.2f));
            yield return null;
            var ab = Stations.DevBoxNear(new Vector2(bb.center.x, bb.yMax + 0.3f));
            Check("铁砧放在工作台上", anvil && Mathf.Abs(ab.yMin - bb.yMax) < 0.05f, $"放上了 {anvil}（{Stations.DevWhy}），铁砧底 {ab.yMin:F2}，台面 {bb.yMax:F2}");

            // 5. A Bottle on a second bench beside it.
            bool bench2 = Stations.TryPlace(StationKind.WorkBench, hero, new Vector2(bb.xMax + c, (floor + 1) * c + 0.2f));
            yield return null;
            var b2 = Stations.DevBoxNear(new Vector2(bb.xMax + c, (floor + 1) * c + 0.1f));
            bool bottle = bench2 && Stations.TryPlace(StationKind.Bottle, hero, new Vector2(b2.center.x + c * 0.4f, b2.yMax + 0.2f));
            Check("瓶子放在工作台上", bottle, $"第二个工作台 {bench2} {b2.min:F2}..{b2.max:F2}（{Stations.DevWhy}）");

            // 6. No block inside a station.
            var inBench = CellGrid.WorldToCell(bb.center);
            Check("工作台里放不进方块", !TerraMod.CanPlace(inBench, CellKind.Stone), $"格子 {inBench}");

            // 7. The ground under the first bench dug: bench and anvil come down as items.
            int fell0 = Stations.DevFell;
            var under = CellGrid.WorldToCell(new Vector2(bb.xMin + c * 0.5f, bb.yMin - 0.05f));
            TerraWorld.Set(under.x, under.y, CellKind.Empty);
            yield return new WaitForSeconds(0.6f);
            int fell = Stations.DevFell - fell0;
            bool Got(ItemType t) => WorldItems.Lying(t) || SlotOf(t) >= 0;
            Check("挖掉工作台下面那格", fell == 2 && Got(ItemType.WorkBench) && Got(ItemType.IronAnvil),
                $"掉下来的家具 {fell} 个（应为工作台和铁砧 2 个），地上或背包里有工作台 {Got(ItemType.WorkBench)}、铁砧 {Got(ItemType.IronAnvil)}");

            // 8. A chest: the cell under it can't be dug.
            int cx = gx + 10;
            int cf = floor;
            while (!TerraWorld.IsSolid(cx, cf) && cf > floor - 6) cf--;
            while (TerraWorld.IsSolid(cx, cf + 1) && cf < floor + 6) cf++;
            hero.transform.position = Clear(hero, CellGrid.CellCenter(cx - 3, cf + 2));
            rb.linearVelocity = Vector2.zero;
            yield return Land();
            bool chest = Chests.TryPlace(hero, new Vector2(cx * c, (cf + 1) * c + 0.2f));
            yield return null;
            var belowChest = new Vector2Int(cx, cf);
            Check("箱子下面那格挖不掉", chest && !TerraWorld.CanDig(belowChest.x, belowChest.y) && TerraWorld.CanDig(cx + 3, cf),
                $"放箱子 {chest}，箱子下 {belowChest} 能挖 {TerraWorld.CanDig(belowChest.x, belowChest.y)}，旁边第三格能挖 {TerraWorld.CanDig(cx + 3, cf)}");
            Shot("Crossroads_01", "furniture");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 家具 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
