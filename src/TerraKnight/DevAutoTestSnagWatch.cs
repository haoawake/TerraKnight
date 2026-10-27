using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "snagwatch", Player.SnagWatch): in Dirtmouth (1) a lip 0.15 high and solid laid on the floor in the Knight's
    /// way: walking into it, he is lifted over it and goes on; (2) a wall as tall as he is: he stops there, not lifted; (3)
    /// his state set to "no input" with control his: walking puts it back and he goes. Lines "[AutoTest] 卡脚看门狗 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SnagWatchTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 卡脚看门狗 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            Player.TerraStats.DevImmortal = true;
            var box = hero.GetComponent<BoxCollider2D>();
            IEnumerator Settle(Vector2 at)
            {
                hero.transform.position = Clear(hero, at);
                hero.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                for (float t = Time.time + 3f; Time.time < t && !hero.cState.onGround;) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            GameObject Block(Vector2 bottomLeft, Vector2 size)
            {
                var go = new GameObject("snag test block") { layer = World.ChunkColliders.TerrainLayer };
                var c = go.AddComponent<BoxCollider2D>();
                c.size = size;
                go.transform.position = bottomLeft + size * 0.5f;
                return go;
            }
            var start = (Vector2)hero.transform.position + Vector2.right * 3f;
            // (1) A low lip.
            yield return Settle(start);
            var b = box.bounds;
            var lip = Block(new Vector2(b.max.x + 0.5f, b.min.y - 0.02f), new Vector2(0.6f, 0.17f));
            int lifts0 = Player.SnagWatch.DevLifts;
            float x0 = hero.transform.position.x;
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(2f);
            Player.TerraMovement.DevMove = 0;
            float went = hero.transform.position.x - x0;
            Check(went > 1.5f, $"脚下的矮坎：走过去 {went:F2}（抬起 {Player.SnagWatch.DevLifts - lifts0} 次）");
            Object.Destroy(lip);
            // (2) A wall.
            yield return Settle(start);
            b = box.bounds;
            var wall = Block(new Vector2(b.max.x + 0.5f, b.min.y - 0.02f), new Vector2(0.6f, 3f));
            lifts0 = Player.SnagWatch.DevLifts;
            x0 = hero.transform.position.x;
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(1.5f);
            Player.TerraMovement.DevMove = 0;
            went = hero.transform.position.x - x0;
            Check(Player.SnagWatch.DevLifts == lifts0 && went < 0.8f && hero.transform.position.y < b.max.y + 1f, $"真墙前停住、不往上抬（走了 {went:F2}，抬起 {Player.SnagWatch.DevLifts - lifts0} 次）");
            Object.Destroy(wall);
            // (3) A state that takes no steps.
            yield return Settle(start);
            int states0 = Player.SnagWatch.DevStates;
            HarmonyLib.AccessTools.FieldRefAccess<HeroController, GlobalEnums.ActorStates>("hero_state")(hero) = GlobalEnums.ActorStates.no_input;
            x0 = hero.transform.position.x;
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(1.5f);
            Player.TerraMovement.DevMove = 0;
            went = hero.transform.position.x - x0;
            Check(Player.SnagWatch.DevStates > states0 && went > 0.8f, $"卡在不可输入状态被复位后能走（复位 {Player.SnagWatch.DevStates - states0} 次，走了 {went:F2}）");
            Plugin.Log.LogInfo($"[AutoTest] 卡脚看门狗 合计 {ok}/{all}");
        }
    }
}
