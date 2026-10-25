using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hookmove"), on Dirtmouth's street: a ledge of solid scenery (the terrain layer, as a lift's or a moving
    /// platform's) above the player; the hook thrown up holds it; the ledge moved 3 across and 1.5 up: the hook goes with it
    /// and the player is pulled along; the ledge pushed down into the ground (as a Colosseum platform slides into the wall):
    /// the hook lets go; thrown again and the ledge gone: it lets go.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HookMoveTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 钩爪 {what} {(pass ? "通过" : "不通过")}");
            }
            GameObject ledge = null;
            try
            {
                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
                hero = HeroController.instance;
                var col = hero.GetComponent<BoxCollider2D>();
                var dummy = World.TargetDummy.DevAt ?? (Vector2)hero.transform.position;
                hero.transform.position = Clear(hero, dummy + Vector2.left * 8f + Vector2.up * 0.5f);
                for (float t = 0f; t < 0.6f; t += Time.deltaTime) yield return null;
                Vector2 c = col.bounds.center;
                ledge = new GameObject("Dev Moving Ledge") { layer = World.ChunkColliders.TerrainLayer };
                ledge.transform.position = c + Vector2.up * 3f;
                var box = ledge.AddComponent<BoxCollider2D>();
                box.size = new Vector2(2f, 0.4f);
                Physics2D.SyncTransforms();
                yield return null;
                var hook = Player.GrapplingHook.Instance;
                hook.ReleaseAll();
                hook.Throw(hero, ledge.transform.position);
                for (float t = 0f; t < 1.5f && hook.DevState != "Latched"; t += Time.deltaTime) yield return null;
                Check(hook.DevState == "Latched", $"钩住会动的地形（{hook.DevState}）");
                var offset = hook.DevHookPosition - (Vector2)ledge.transform.position;
                var from = (Vector2)ledge.transform.position;
                var move = new Vector2(3f, 1.5f);
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    ledge.transform.position = from + move * Mathf.Clamp01(t / 1.2f);
                    Physics2D.SyncTransforms();
                    yield return null;
                }
                for (float t = 0f; t < 0.6f; t += Time.deltaTime) yield return null;
                var expect = (Vector2)ledge.transform.position + offset;
                float off = Vector2.Distance(hook.DevHookPosition, expect);
                float pulled = Vector2.Distance(col.bounds.center, hook.DevHookPosition);
                Check(hook.DevState == "Latched" && off < 0.05f, $"地形移动后钩子跟着走（偏差 {off:F3}）");
                Check(pulled < 1.6f, $"人被拉到钩子那里（离钩子 {pulled:F2}）");
                // Into the ground: it lets go, the player left where they are.
                int carried0 = Player.GrapplingHook.DevCarriedOff;
                var down = Physics2D.Raycast(ledge.transform.position, Vector2.down, 20f, 1 << World.ChunkColliders.TerrainLayer);
                var ground = down.collider != null ? down.point : (Vector2)ledge.transform.position + Vector2.down * 5f;
                from = ledge.transform.position;
                for (float t = 0f; t < 1.2f && hook.DevState == "Latched"; t += Time.deltaTime)
                {
                    ledge.transform.position = Vector2.Lerp(from, ground + Vector2.down * 1.5f, Mathf.Clamp01(t / 0.8f));
                    Physics2D.SyncTransforms();
                    yield return null;
                }
                Check(hook.DevState != "Latched" && Player.GrapplingHook.DevCarriedOff > carried0,
                    $"钩住的地形缩进地里时钩子松开（{hook.DevState}，松开 {Player.GrapplingHook.DevCarriedOff - carried0}），人离地面 {col.bounds.min.y - ground.y:F2}");
                // Again, and the ledge gone.
                hook.ReleaseAll();
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                c = col.bounds.center;
                ledge.transform.position = c + Vector2.up * 3f;
                Physics2D.SyncTransforms();
                yield return null;
                hook.Throw(hero, ledge.transform.position);
                for (float t = 0f; t < 1.5f && hook.DevState != "Latched"; t += Time.deltaTime) yield return null;
                ledge.SetActive(false);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                Check(hook.DevState != "Latched", $"地形没了钩子松开（{hook.DevState}）");
            }
            finally
            {
                if (ledge != null) Object.Destroy(ledge);
                Player.GrapplingHook.Instance?.ReleaseAll();
            }
            Plugin.Log.LogInfo($"[AutoTest] 钩爪 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
