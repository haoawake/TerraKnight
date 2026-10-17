using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_platmix"): a row of platforms in the open in Dirtmouth with something else beside or on it each time
    /// (a block over it, a block at its end, a torch on it, a rope through it, a Work Bench on it, a chest on it, its end
    /// hammered into a stair), and each time the same trials: standing on it from above, jumping up through it from
    /// below, placing one more platform. Everything set is taken away after each case.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PlatformMixTest()
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            // On the ground the player stands on, a little way along.
            var feetAt = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.min + new Vector2(6f, 0.05f);
            var start = CellGrid.WorldToCell(feetAt);
            int gy = start.y + 4;
            for (int k = 0; k < 40 && !TerraWorld.IsSolid(start.x, gy - 1); k++) gy--;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 平台混放 地面格 {start.x},{gy}（主角脚 {feetAt:F1}）");
            int y = gy + 3, x0 = start.x - 3, x1 = start.x + 3;
            var cases = new (string name, System.Action<List<Vector2Int>> setup)[]
            {
                ("只有平台", set => { }),
                ("平台中间上方两格有石块", set => { Put(set, start.x, y + 4, CellKind.Stone); }),
                ("平台一端挨着石块", set => { Put(set, x1 + 2, y, CellKind.Stone); }),
                ("平台上放火把", set => { Put(set, x0, y + 1, CellKind.Torch); }),
                ("绳子穿过平台", set => { for (int k = -3; k <= 3; k++) if (k != 0) Put(set, x0 + 1, y + k, CellKind.Rope); }),
                ("平台一端锤成台阶", set => { TerraWorld.SetSlope(x0, y, 1); }),
            };
            foreach (var (name, setup) in cases)
            {
                var set = new List<Vector2Int>();
                for (int x = x0; x <= x1; x++) Put(set, x, y, CellKind.Platform);
                setup(set);
                yield return new WaitForSeconds(0.4f);
                yield return PlatformTrials(name, hero, x0 + 1, x1 - 1, y, name.Contains("上方") ? 0.4f : 2.5f);
                foreach (var c in set) { TerraWorld.SetSlope(c.x, c.y, 0); TerraWorld.Set(c.x, c.y, CellKind.Empty); }
                yield return new WaitForSeconds(0.3f);
            }
            // Furniture set on the row: a Work Bench, then a chest (left there: the test save goes).
            foreach (var what in new[] { "工作台", "箱子" })
            {
                var set = new List<Vector2Int>();
                for (int x = x0; x <= x1; x++) Put(set, x, y, CellKind.Platform);
                yield return new WaitForSeconds(0.3f);
                var col = hero.GetComponent<BoxCollider2D>();
                float offset = hero.transform.position.y - col.bounds.min.y;
                hero.transform.position = new Vector3((x0 + 4.5f) * CellGrid.CellSize, (y + 1) * CellGrid.CellSize + offset + 0.05f, hero.transform.position.z);
                yield return new WaitForSeconds(0.6f);
                var aim = CellGrid.CellCenter(x0 + 1, y + 1);
                bool placed = what == "工作台" ? Stations.TryPlace(StationKind.WorkBench, hero, aim) : Chests.TryPlace(hero, aim);
                yield return new WaitForSeconds(0.4f);
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 平台混放 {what} 放在平台上 {placed}");
                yield return PlatformTrials($"平台上放{what}（放上 {placed}）", hero, x0 + 3, x1 - 1, y);
                foreach (var c in set) TerraWorld.Set(c.x, c.y, CellKind.Empty);
                yield return new WaitForSeconds(0.3f);
            }
        }

        static void Put(List<Vector2Int> set, int x, int y, CellKind kind)
        {
            TerraWorld.Set(x, y, kind);
            set.Add(new Vector2Int(x, y));
        }
    }
}
