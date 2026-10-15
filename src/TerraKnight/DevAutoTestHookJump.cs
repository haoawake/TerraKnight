using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hookjump"): a jump, the hook thrown at a ledge up and to the right while in the
    /// air, then jump pressed (Space, through the input system as the keyboard would) while the
    /// hook pulls: the player jumps off it (Terraria's GrappleMovement), not just lets go and falls.
    /// Holding down while pressing jump only lets go.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HookJumpTest()
        {
            var hero = HeroController.instance;
            var hook = Player.GrapplingHook.Instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            float pxt = Player.TerraMovement.PxT;
            Vector2 home = hero.transform.position;
            int ok = 0, all = 0;
            foreach (bool down in new[] { false, true })
            {
                hero.transform.position = Clear(hero, home);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.8f);
                var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
                var placed = new System.Collections.Generic.List<Vector2Int>();
                for (int x = feet.x + 8; x <= feet.x + 12; x++)
                {
                    var c = new Vector2Int(x, feet.y + 16);
                    if (TerraWorld.Get(c.x, c.y) == CellKind.Empty) { TerraWorld.Set(c.x, c.y, CellKind.Stone); placed.Add(c); }
                }
                yield return new WaitForSeconds(0.2f);
                // Jump, and the hook thrown on the way up.
                mod.StartCoroutine(Tap(Key.Space, 0.2f));
                yield return new WaitForSeconds(0.25f);
                bool airborne = !hero.cState.onGround;
                hook.Throw(hero, CellGrid.CellCenter(feet.x + 10, feet.y + 16));
                float t0 = Time.time;
                while (hook.DevState != "Latched" && Time.time - t0 < 1.5f) yield return null;
                while (rb.linearVelocity.magnitude < 6f * pxt && hook.DevState == "Latched" && Time.time - t0 < 2.5f) yield return null;
                string state = hook.DevState;
                Vector2 pulled = rb.linearVelocity / pxt;
                // Jump pressed while it pulls (down held too, the second time).
                if (down) tapHeld.Add(Key.S);
                tapHeld.Add(Key.Space);
                KeysDown(tapHeld.ToArray());
                bool jumped = false;
                float maxUp = -99f;
                var trace = new System.Text.StringBuilder();
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    yield return null;
                    jumped |= hero.cState.jumping;
                    maxUp = Mathf.Max(maxUp, rb.linearVelocity.y / pxt);
                    if (trace.Length < 120) trace.Append($" {rb.linearVelocity.y / pxt:F1}");
                }
                tapHeld.Remove(Key.Space);
                tapHeld.Remove(Key.S);
                KeysDown(tapHeld.ToArray());
                string after = hook.DevState;
                bool letGo = hook.DevHolding == 0;
                all++;
                bool pass = airborne && state == "Latched" && letGo && (down ? !jumped : jumped);
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 抓钩跳 {(down ? "按住下再按跳" : "按跳")}：起跳后{(airborne ? "在空中" : "还在地上")}扔钩，{state}，被拉着 ({pulled.x:F1},{pulled.y:F1}) px/tick；按下后抓钩 {after}，" +
                                   $"{(jumped ? "起跳了" : "没起跳")}，之后竖直速度{trace}（最高 {maxUp:F1}） {(pass ? "通过" : "不通过")}");
                foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
                yield return new WaitForSeconds(1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 抓钩跳 合计 {ok}/{all}");
        }
    }
}
