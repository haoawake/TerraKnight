using System.Collections;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "torchfar"): a torch in a closed cave under Dirtmouth stays lit with the player beside it, 10 and 20
    /// units away, and after leaving the room and coming back (its light and glow both).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TorchFarTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 远处火把 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            hero.transform.position = Clear(hero, new Vector2(112f, 11.5f));
            yield return new WaitForSeconds(1f);
            PlayerData.instance.isInvincible = true;
            var top = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            for (int y = top.y - 14; y <= top.y - 4; y++)
                for (int x = top.x - 4; x <= top.x + 46; x++)
                    TerraWorld.Set(x, y, CellKind.Empty);
            var torch = new Vector2Int(top.x, top.y - 14);
            TerraWorld.Set(torch.x, torch.y, CellKind.Torch);
            var at = CellGrid.CellCenter(torch.x, torch.y);
            string Read(string label)
            {
                return $"{label} 亮度 {LightMap.DevLightAt(at):F2}，光晕 {LightMap.DevSpritesShown}";
            }
            var report = new System.Collections.Generic.List<string>();
            float near = 0f, far10 = 0f, far20 = 0f, back = 0f;
            foreach (var (dx, label) in new[] { (1f, "旁边"), (10f, "10 单位外"), (20f, "20 单位外") })
            {
                hero.transform.position = Clear(hero, at + new Vector2(dx, 1f));
                yield return new WaitForSeconds(1f);
                float v = LightMap.DevLightAt(at);
                if (dx < 2f) near = v; else if (dx < 15f) far10 = v; else far20 = v;
                report.Add(Read(label));
            }
            Shot("Town", "torchfar_far");
            yield return null;
            // Out of the room and back.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            hero.transform.position = Clear(hero, at + new Vector2(10f, 1f));
            yield return new WaitForSeconds(1.5f);
            back = LightMap.DevLightAt(at);
            report.Add(Read("回来后 10 单位外"));
            Shot("Town", "torchfar_back");
            yield return null;
            Check("离远了还亮", near > 0.8f && far10 > 0.8f && far20 > 0.8f && back > 0.8f && TerraWorld.Get(torch.x, torch.y) == CellKind.Torch, string.Join("；", report));
            Plugin.Log.LogInfo($"[AutoTest] 远处火把 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
