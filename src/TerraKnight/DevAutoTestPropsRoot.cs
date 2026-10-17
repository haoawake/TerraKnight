using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_props"): a Work Bench and a chest set down in Dirtmouth, then the room's terrain root switched off
    /// as a boss room that keeps the game's own ground switches it off (TerrainSystem): both still drawn (they hang under
    /// the room's props, TerrainRegion.Props). Then F8's report taken with platforms under the player's feet: it has
    /// the platform part.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PropsRootTest()
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var region = TerrainSystem.Current?.Region;
            if (region == null) { FxCheck("地形", false, "德特茅斯没有地形"); yield break; }
            var col = hero.GetComponent<BoxCollider2D>();
            var feetAt = (Vector2)col.bounds.min + new Vector2(0f, 0.05f);
            bool bench = Stations.TryPlace(StationKind.WorkBench, hero, feetAt + new Vector2(3f, 0.3f));
            bool chest = false;
            for (float dx = 1.5f; dx >= -2.5f && !chest; dx -= 0.5f) chest = Chests.TryPlace(hero, feetAt + new Vector2(dx, 0.3f));
            yield return new WaitForSeconds(0.5f);
            SpriteRenderer Find(string name)
            {
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (sr.name == name && Vector2.Distance(sr.transform.position, feetAt) < 16f) return sr;
                return null;
            }
            var benchSr = Find("Terra Station WorkBench");
            var chestSr = Find("Terra Chest");
            string Where(SpriteRenderer sr) => sr == null ? "没找到" : $"挂在 {sr.transform.parent?.name}，画着 {sr.enabled && sr.gameObject.activeInHierarchy}，位置 {(Vector2)sr.transform.position:F2}";
            FxCheck("工作台和箱子挂在房间的摆件下", benchSr != null && chestSr != null && benchSr.transform.parent == region.Props && chestSr.transform.parent == region.Props,
                $"放下 工作台 {bench} 箱子 {chest}；工作台 {Where(benchSr)}；箱子 {Where(chestSr)}");
            region.Root.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            Shot("Town", "fx_props_rootoff");
            yield return null;
            bool drawn = benchSr != null && chestSr != null && benchSr.enabled && benchSr.gameObject.activeInHierarchy && chestSr.enabled && chestSr.gameObject.activeInHierarchy;
            FxCheck("地形根关掉（首领房）后工作台和箱子还画着", drawn, $"工作台 {Where(benchSr)}；箱子 {Where(chestSr)}");
            region.Root.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);

            // F8 over a row of platforms the player stands on.
            var start = CellGrid.WorldToCell(feetAt);
            int y = start.y + 3;
            for (int x = start.x - 2; x <= start.x + 2; x++) TerraWorld.Set(x, y, CellKind.Platform);
            yield return new WaitForSeconds(0.3f);
            float offset = hero.transform.position.y - col.bounds.min.y;
            hero.transform.position = new Vector3((start.x + 0.5f) * CellGrid.CellSize, (y + 1) * CellGrid.CellSize + offset + 0.3f, hero.transform.position.z);
            yield return new WaitForSeconds(1f);
            int before = Player.SpotReport.DevReports;
            HarmonyLib.AccessTools.Method(typeof(Player.SpotReport), "Report").Invoke(null, new object[] { hero, (Vector2)hero.transform.position });
            FxCheck("F8 报告写出平台部分", Player.SpotReport.DevReports == before + 1, "看日志里 [现场] 的“平台：”和“主角周围的格子”");
            for (int x = start.x - 2; x <= start.x + 2; x++) TerraWorld.Set(x, y, CellKind.Empty);
        }
    }
}
