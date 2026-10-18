using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_platdrop"): a row of platforms over Dirtmouth's ground at several heights (3 to 8 cells), and
    /// for each what the player does with it: stood on, dropped through with Down to the ground, then a real jump
    /// (Space held) from the ground under it, a drop onto it from above, a Work Bench and a block set on it. The
    /// hero's contacts with the platform (touching, the effector letting it through or not), its composite's state
    /// and the hero's collision ignores are logged at each step, and the hero's collision callbacks with platforms
    /// during each jump. Then, at one height: a cell elsewhere in the chunk changed while the platform lets the
    /// player through, and walking off its end to the ground and jumping back on from under it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        [HarmonyPatch]
        internal static class PlatTrace
        {
            public static bool On;
            public static readonly StringBuilder Log = new StringBuilder();

            [HarmonyPrefix]
            [HarmonyPatch(typeof(HeroController), "OnCollisionEnter2D")]
            static void Enter(HeroController __instance, Collision2D collision)
            {
                if (!On || !(collision.collider is CompositeCollider2D cc) || !ChunkColliders.AllPlatforms.Contains(cc) || Log.Length > 900) return;
                var n = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.zero;
                var b = __instance.GetComponent<BoxCollider2D>().bounds;
                Log.Append($"[进 帧{Time.frameCount} 开{(collision.enabled ? 1 : 0)} 法线{n.y:F2} 跳{(__instance.cState.jumping ? 1 : 0)} 脚{b.min.y:F2} vy{__instance.GetComponent<Rigidbody2D>().linearVelocity.y:F2}]");
            }
        }

        static string Contacts(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var sb = new StringBuilder();
            var pts = new ContactPoint2D[16];
            int n = col.GetContacts(pts);
            int plat = 0, platOff = 0;
            for (int i = 0; i < n; i++)
            {
                var other = pts[i].collider == col ? pts[i].otherCollider : pts[i].collider;
                if (other is CompositeCollider2D cc && ChunkColliders.AllPlatforms.Contains(cc)) { plat++; if (!pts[i].enabled) platOff++; }
            }
            foreach (var p in ChunkColliders.AllPlatforms)
            {
                if (p == null || Vector2.Distance(p.bounds.center, col.bounds.center) > 20f) continue;
                sb.Append($"[{p.transform.parent?.name} 开{(p.enabled && p.gameObject.activeInHierarchy ? 1 : 0)} 形状{p.shapeCount} 点{p.pointCount} 碰到{(col.IsTouching(p) ? 1 : 0)} 忽略{(Physics2D.GetIgnoreCollision(col, p) ? 1 : 0)}] ");
            }
            return $"平台接触 {plat}（被效应器放行 {platOff}）{sb}地上 {hero.cState.onGround} 跳 {hero.cState.jumping} 脚 {col.bounds.min.y:F2} 头 {col.bounds.max.y:F2}";
        }

        /// <summary>A real jump (Space held 0.45 s): how high the feet got and the platform callbacks on the way.</summary>
        IEnumerator PlatJump(HeroController hero, BoxCollider2D col, System.Action<float, string> done)
        {
            float before = col.bounds.min.y, highest = before;
            PlatTrace.Log.Clear();
            PlatTrace.On = true;
            KeysDown(Key.Space);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime) { highest = Mathf.Max(highest, col.bounds.min.y); yield return null; }
            KeysDown();
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { highest = Mathf.Max(highest, col.bounds.min.y); yield return null; }
            PlatTrace.On = false;
            done(highest - before, $"起跳脚 {before:F2}，最高 {highest:F2}；回调 {PlatTrace.Log}");
        }

        static string PlatRay(float x, float top)
        {
            var hit = Physics2D.Raycast(new Vector2(x, top + 0.25f), Vector2.down, 1.5f, 1 << ChunkColliders.TerrainLayer);
            return hit.collider != null ? $"{hit.collider.name}@{hit.point.y:F2}" : "无";
        }

        IEnumerator PlatDropTest()
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var feetAt = (Vector2)col.bounds.min + new Vector2(7f, 0.05f);
            var start = CellGrid.WorldToCell(feetAt);
            int gy = start.y + 4;
            for (int k = 0; k < 40 && !TerraWorld.IsSolid(start.x, gy - 1); k++) gy--;
            float ground = gy * CellGrid.CellSize;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 平台下落 地面格 {start.x},{gy}（顶 {ground:F2}），主角高 {col.bounds.size.y:F2}");
            float offset = hero.transform.position.y - col.bounds.min.y;
            float mid = (start.x + 0.5f) * CellGrid.CellSize;
            foreach (int h in new[] { 3, 4, 5, 6, 8 })
            {
                int y = gy + h - 1, x0 = start.x - 3, x1 = start.x + 3;
                float top = (y + 1) * CellGrid.CellSize;
                var set = new List<Vector2Int>();
                for (int x = x0; x <= x1; x++) Put(set, x, y, CellKind.Platform);
                yield return new WaitForSeconds(0.4f);
                string tag = $"平台离地 {h} 格";
                // 1. Onto it from above.
                hero.transform.position = new Vector3(mid, top + 2f + offset, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                bool on1 = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.25f;
                FxCheck($"{tag}：从上面落下站住", on1, Contacts(hero));
                // 2. Down held: through it to the ground.
                Player.TerraMovement.DevDown = true;
                yield return new WaitForSeconds(0.6f);
                Player.TerraMovement.DevDown = false;
                yield return new WaitForSeconds(1f);
                bool down = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - ground) < 0.3f;
                FxCheck($"{tag}：按下穿过平台落到地上", down, Contacts(hero));
                // 3. A real jump from the ground under it (Space held): up through it and onto it, when it's in reach.
                float rose = 0f;
                string how = "";
                yield return PlatJump(hero, col, (r, s) => { rose = r; how = s; });
                bool landedOn = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.25f;
                bool reach = top - ground < 2.8f;
                FxCheck($"{tag}：在地面和平台之间按空格跳得起来{(reach ? "，跳上平台" : "")}", rose > 1f && (!reach || landedOn), $"升 {rose:F2}，落在平台上 {landedOn}；{how}；{Contacts(hero)}");
                // 4. Onto it from above again.
                hero.transform.position = new Vector3(mid, top + 2f + offset, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                bool on2 = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.25f;
                FxCheck($"{tag}：穿下去以后再从上面落下站住", on2, Contacts(hero));
                // 5. Things set on it: a Work Bench, a block.
                bool bench = Stations.TryPlace(StationKind.WorkBench, hero, CellGrid.CellCenter(x0 + 1, y + 1));
                yield return new WaitForSeconds(0.3f);
                var cell = new Vector2Int(x1, y + 1);
                mod.DevUse(ItemType.DirtBlock, CellGrid.CellCenter(cell.x, cell.y));
                yield return new WaitForSeconds(0.3f);
                bool block = TerraWorld.IsSolid(cell.x, cell.y);
                FxCheck($"{tag}：平台上放得下工作台和方块", bench && block, $"工作台 {bench}（{Stations.DevWhy}），方块 {block}；{Contacts(hero)}");
                if (!on2 || !down || rose <= 1f) { Shot("Town", $"fx_platdrop_{h}"); yield return null; }
                TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
                foreach (var c in set) TerraWorld.Set(c.x, c.y, CellKind.Empty);
                yield return new WaitForSeconds(0.4f);
            }

            // A cell elsewhere in the chunk changes while the platform lets the player through.
            {
                int h = 5, y = gy + h - 1, x0 = start.x - 3, x1 = start.x + 3;
                float top = (y + 1) * CellGrid.CellSize;
                var set = new List<Vector2Int>();
                for (int x = x0; x <= x1; x++) Put(set, x, y, CellKind.Platform);
                yield return new WaitForSeconds(0.4f);
                hero.transform.position = new Vector3(mid, top + 2f + offset, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                Player.TerraMovement.DevDown = true;
                yield return new WaitForSeconds(0.1f);
                var far = new Vector2Int(x1 + 4, y + 4);
                TerraWorld.Set(far.x, far.y, CellKind.Dirt);
                yield return new WaitForSeconds(0.5f);
                Player.TerraMovement.DevDown = false;
                yield return new WaitForSeconds(1.2f);
                string ray = PlatRay(mid, top);
                hero.transform.position = new Vector3(mid, top + 2f + offset, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                bool on = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.25f;
                FxCheck("穿平台时同一区块别处改了格子：之后平台还站得住、射线打得到", on && ray != "无", $"射线 {ray}；{Contacts(hero)}");
                TerraWorld.Set(far.x, far.y, CellKind.Empty);

                // Off its end to the ground, back under it, a jump up onto it.
                Player.TerraMovement.DevMove = -1;
                for (float t = 0f; t < 3f && (!hero.cState.onGround || col.bounds.max.x > x0 * CellGrid.CellSize - 1.5f); t += Time.deltaTime) yield return null;
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.8f);
                bool offGround = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - ground) < 0.3f;
                // Back under it (Dirtmouth's ground falls away just past its far end, so not by walking).
                hero.transform.position = new Vector3(mid, ground + 0.05f + offset, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                float rose = 0f;
                string how = "";
                yield return PlatJump(hero, col, (r, s) => { rose = r; how = s; });
                bool landedOn = hero.cState.onGround && Mathf.Abs(col.bounds.min.y - top) < 0.25f;
                FxCheck("从平台一头走下到地上，回到下面跳上平台", offGround && landedOn, $"走下到地上 {offGround}，升 {rose:F2}，落在平台上 {landedOn}；{how}；{Contacts(hero)}");
                foreach (var c in set) TerraWorld.Set(c.x, c.y, CellKind.Empty);
                yield return new WaitForSeconds(0.4f);
            }
            done = true;
        }
    }
}
