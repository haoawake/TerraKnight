using System.Collections;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dissonance"): the Hook of Dissonance (the user: "失谐钩在人物和平台几乎平行的时候有概率平移过去而不是原版的
    /// 传送效果"). On Dirtmouth's street: a short row of platforms 7 tiles across at the height of the Knight's middle, half a
    /// tile over and under it, a column of dirt further off, and a block in the ground's top row far off. Each hooked, the
    /// Knight there in the frame it latches (most of the way at once, near the hook) rather than pulled over. Every case first with the old single cast (logged only), then as now.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DissonanceTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 失谐钩 {what} {(pass ? "通过" : "不通过")}");
            }
            var col = hero.GetComponent<BoxCollider2D>();
            var hook = Player.GrapplingHook.Instance;
            var hook0 = Player.GrapplingHook.DevHook;
            var placed = new List<Vector2Int>();
            float cs = CellGrid.CellSize;
            var start = (Vector2)hero.transform.position;
            var cases = new (string What, int Kind)[]
            {
                ("和人物齐平的平台", 0), ("高半格的平台", 1), ("低半格的平台", 2), ("远处的泥土", 3),
                ("站在地上钩远处地面那一层的方块", 5),
            };
            try
            {
                Player.GrapplingHook.DevHook = Items.ItemType.QueenSlimeHook;
                for (int pass = 0; pass < 2; pass++)
                {
                    Player.GrapplingHook.DevWholeCast = pass == 0;
                    foreach (var (what, kind) in cases)
                    {
                        hook.ReleaseAll();
                        hero.transform.position = Clear(hero, start);
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        yield return new WaitForSeconds(0.6f);
                        var row = new List<Vector2Int>();
                        void Put(int x, int y, CellKind k)
                        {
                            if (TerraWorld.Get(x, y) != CellKind.Empty) return;
                            TerraWorld.Set(x, y, k);
                            row.Add(new Vector2Int(x, y));
                            placed.Add(new Vector2Int(x, y));
                        }
                        Vector2 c = col.bounds.center;
                        var feet = CellGrid.WorldToCell(new Vector2(c.x, col.bounds.min.y - 0.05f));
                        Vector2Int end;
                        if (kind <= 2)
                        {
                            float dy = kind == 1 ? 0.5f : kind == 2 ? -0.5f : 0f;
                            end = CellGrid.WorldToCell(c + new Vector2(7f * cs, dy * cs));
                            for (int k = 0; k < 4; k++) Put(end.x + k, end.y, CellKind.Platform);
                        }
                        else if (kind == 3)
                        {
                            end = CellGrid.WorldToCell(c + new Vector2(11f * cs, 0f));
                            for (int h = -1; h < 3; h++) Put(end.x, end.y + h, CellKind.Dirt);
                        }
                        else if (kind == 4)
                        {
                            // A floor of platforms two tiles up, the Knight set on it.
                            int y = feet.y + 2;
                            for (int x = feet.x - 1; x <= feet.x + 9; x++) Put(x, y, CellKind.Platform);
                            yield return new WaitForSeconds(0.4f);
                            float bottom = col.bounds.min.y - hero.transform.position.y;
                            hero.transform.position = new Vector3(hero.transform.position.x, (y + 1) * cs - bottom + 0.02f, hero.transform.position.z);
                            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                            yield return new WaitForSeconds(0.6f);
                            end = new Vector2Int(feet.x + 9, y);
                        }
                        else
                        {
                            // A block in the ground's top row 9 tiles across (set there unless the ground's own is).
                            end = new Vector2Int(feet.x + 9, feet.y);
                            Put(end.x, end.y, CellKind.Dirt);
                        }
                        yield return new WaitForSeconds(0.4f);
                        var target = new Vector2((end.x + 0.5f) * cs, (end.y + 0.5f) * cs);
                        int tele0 = Player.GrapplingHook.DevTeleports;
                        Vector2 last = col.bounds.center;
                        hook.Throw(hero, target);
                        float toHook = 0f;
                        for (float t = 0f; t < 1.5f && Player.GrapplingHook.DevTeleports == tele0; t += Time.deltaTime)
                        {
                            last = col.bounds.center;
                            toHook = Vector2.Distance(last, hook.DevHookPosition);
                            yield return null;
                        }
                        yield return null;
                        Vector2 after = col.bounds.center;
                        float jump = (after - last).magnitude, left = Vector2.Distance(after, hook.DevHookPosition);
                        bool teleported = Player.GrapplingHook.DevTeleports > tele0;
                        string line = $"{what}：传送 {teleported}，钩住时一下移动 {jump:F2}（钩住前离钩点 {toHook:F2}），离钩点 {left:F2}，人在 {after:F1}，钩在 {hook.DevHookPosition:F1}（{hook.DevState}）";
                        if (pass == 0) Plugin.Log.LogInfo($"[AutoTest] 失谐钩 （旧写法）{line}");
                        else Check(teleported && jump > 0.7f * toHook && left < 1.2f, line);
                        hook.ReleaseAll();
                        foreach (var at in row) TerraWorld.Set(at.x, at.y, CellKind.Empty);
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }
            finally
            {
                Player.GrapplingHook.DevWholeCast = false;
                hook.ReleaseAll();
                foreach (var at in placed) if (TerraWorld.Get(at.x, at.y) != CellKind.Empty) TerraWorld.Set(at.x, at.y, CellKind.Empty);
                Player.GrapplingHook.DevHook = hook0;
                Plugin.Log.LogInfo($"[AutoTest] 失谐钩 结果 {ok}/{all}");
            }
        }
    }
}
