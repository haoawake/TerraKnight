using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shadowbeam", the user's 10-28 "暗影束法杖的射线有问题，乱飘"): the Shadowbeam Staff fired a few times in a
    /// stone box in Dirtmouth (Terraria blocks) and at Dirtmouth's own ground (the game's colliders), each beam's bounces
    /// logged (TerraProjectiles.DevBeamBounces): how many, and how many left the beam inside a wall. Screenshots of the
    /// lines. Lines "[AutoTest] 暗影束 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShadowbeamTest()
        {
            TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var r = TerrainSystem.Current.Region;
            var inv = mod.Inventory;
            bool savedInfinite = inv.Infinite;
            inv.Infinite = true;
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            int savedSel = inv.Selected;
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(ItemType.ShadowbeamStaff);
            inv.Selected = 0;
            var hero = HeroController.instance;
            var body = hero.GetComponent<Rigidbody2D>();
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 暗影束 {what} {(pass ? "通过" : "不通过")}"); }

            IEnumerator Fire(string where, Vector2 aim, string shot)
            {
                TerraProjectiles.DevBeamBounces = new System.Collections.Generic.List<(Vector2 at, bool inWall)>();
                mod.Stats.Mana = mod.Stats.ManaMax;
                for (int k = 0; k < 3; k++)
                {
                    for (float t = Time.time + 1f; Time.time < t && !(mod.DevReady && !mod.IsUsingItem);) yield return null;
                    mod.DevUseSelected(aim + new Vector2(0f, k * 0.6f));
                    yield return new WaitForSeconds(0.12f);
                    if (k == 0) Shot("Town", shot);
                    yield return new WaitForSeconds(0.5f);
                }
                var b = TerraProjectiles.DevBeamBounces;
                int inWall = b.Count(x => x.inWall);
                string sample = string.Join(" ", b.Take(8).Select(x => $"({x.at.x:0.0},{x.at.y:0.0}{(x.inWall ? "墙内" : "")})"));
                Check(b.Count > 0 && inWall == 0, $"{where}：反弹 {b.Count} 次，停在墙里 {inWall} 次；{sample}");
                TerraProjectiles.DevBeamBounces = null;
                TerraProjectiles.Instance?.Clear();
            }

            // A stone box of Terraria blocks.
            const int cx = 200, floor = 40, top = 56;
            void Cell(int x, int y, CellKind kind) => TerraWorld.Set(x + r.CellOffset.x, y + r.CellOffset.y, kind);
            Vector3 At(int x, float y) => r.Root.position + new Vector3((x + 0.5f) * CellGrid.CellSize, y * CellGrid.CellSize, 0f);
            for (int y = floor - 1; y <= top + 1; y++)
                for (int x = cx - 14; x <= cx + 14; x++)
                    Cell(x, y, x <= cx - 13 || x >= cx + 13 || y <= floor - 1 || y >= top ? CellKind.Stone : CellKind.Empty);
            yield return new WaitForSeconds(0.5f);
            hero.transform.position = At(cx - 6, floor + 1) + Vector3.up * 1.2f;
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            yield return Fire("石块盒子", (Vector2)At(cx + 12, floor + 9), "beam_box");

            // Dirtmouth's own ground, out of the box.
            for (int y = floor - 1; y <= top + 1; y++)
                for (int x = cx - 14; x <= cx + 14; x++)
                    Cell(x, y, CellKind.Empty);
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var feet = (Vector2)HeroController.instance.transform.position;
            yield return Fire("德特茅斯地面", feet + new Vector2(6f, -3f), "beam_ground");

            // Open air, straight ahead and a little up, each weapon mode (the user's 10-28 "依旧没修好，甚至还受到重力"):
            // every update's place and velocity until its first turn; it must keep its line.
            foreach (bool remade in new[] { false, true })
            {
                BladeArts.Set(remade);
                hero = HeroController.instance;
                // Out on Dirtmouth's open street, room all round.
                hero.transform.position = Clear(hero, new Vector2(100f, 11.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.8f);
                feet = hero.transform.position;
                var aim = feet + new Vector2(12f, 2.5f);
                mod.DevAim = aim;
                TerraProjectiles.DevBeamPath = new System.Collections.Generic.List<(Vector2 at, Vector2 vel)>();
                mod.Stats.Mana = mod.Stats.ManaMax;
                for (float t = Time.time + 1f; Time.time < t && !(mod.DevReady && !mod.IsUsingItem);) yield return null;
                mod.DevUseSelected(aim);
                // The line it leaves, as it is drawn and after: whether its dust stays where the beam went.
                foreach (float at in new[] { 0.05f, 0.2f, 0.4f, 0.8f })
                {
                    yield return new WaitForSeconds(at - (at == 0.05f ? 0f : at == 0.2f ? 0.05f : at == 0.4f ? 0.2f : 0.4f));
                    Shot("Town", $"beam_air_{(remade ? "new" : "old")}_{at * 100:000}");
                }
                var path = TerraProjectiles.DevBeamPath;
                TerraProjectiles.DevBeamPath = null;
                mod.DevAim = null;
                if (path.Count < 3) { Check(false, $"空中直射（{(remade ? "新版" : "原版")}）：只记到 {path.Count} 步"); continue; }
                var v0 = path[0].vel;
                // Until the first change of heading of more than 5°: how far it came and how much its velocity's down part grew.
                int n = 1;
                while (n < path.Count && Vector2.Angle(path[n].vel, v0) < 5f) n++;
                float straight = Vector2.Distance(path[0].at, path[n - 1].at);
                float bend = n < path.Count ? Vector2.SignedAngle(v0, path[n].vel) : 0f;
                var line = path[0].at + v0.normalized * Vector2.Dot(path[n - 1].at - path[0].at, v0.normalized);
                float off = Vector2.Distance(line, path[n - 1].at);
                Check(off < 0.3f && straight > 4f, $"空中直射（{(remade ? "新版" : "原版")}）：{path.Count} 步，直线走了 {straight:F1}，偏离直线 {off:F2}，第一次转向 {bend:F0}°（在 {(n < path.Count ? path[n].at.ToString("F1") : "没有")}），起速 {v0}");
            }

            for (int i = 0; i < inv.Slots.Length && i < savedSlots.Length; i++) inv.Slots[i] = savedSlots[i];
            inv.Selected = savedSel;
            inv.Infinite = savedInfinite;
            Plugin.Log.LogInfo($"[AutoTest] 暗影束 合计 {ok}/{all}");
        }
    }
}
