using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "liftfix"): the City's lifts (the user's, 2026-10-20). In Ruins1_02: a blow on its own (a minion's) on
    /// a Lift Call Lever works nothing, the player's own works it (its FSM's state before and after); the Knight dropped
    /// down the lift's shaft over the room's bottom gate (bot1); and the Knight put under the room, falling: taken back
    /// as from a pit within a couple of seconds (WorldBorder.DevFellOut), not falling for ever.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LiftFixTest()
        {
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 电梯 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            const string room = "Ruins1_02";
            try
            {
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2f);
                PlayerData.instance.isInvincible = true;
                var hero = HeroController.instance;
                var lever = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f.FsmName == "Call Lever" && f.gameObject.scene.name == room);
                var col = lever != null ? lever.GetComponent<Collider2D>() : null;
                if (col == null) Check("找到叫电梯的拉杆", false, "没有");
                else
                {
                    string s0 = lever.ActiveStateName;
                    int spared = PropHits.DevLiftsSpared, pulses = PropHits.DevPulses;
                    PropHits.Pulse(col, (Vector2)col.bounds.center + Vector2.left, 1f, 20, onItsOwn: true);
                    yield return new WaitForSeconds(1f);
                    string s1 = lever.ActiveStateName;
                    Check("召唤物打电梯拉杆", PropHits.DevLiftsSpared == spared + 1 && PropHits.DevPulses == pulses && s1 == s0, $"拦下 {PropHits.DevLiftsSpared - spared}，状态 {s0}→{s1}");
                    PropHits.Pulse(col, (Vector2)col.bounds.center + Vector2.left, 1f, 20);
                    string s2 = s1;
                    for (float t = Time.time + 2f; Time.time < t && s2 == s1; ) { yield return null; s2 = lever.ActiveStateName; }
                    Check("自己打电梯拉杆", PropHits.DevPulses == pulses + 1, $"补刀 {PropHits.DevPulses - pulses}，状态 {s1}→{s2}");
                }
                // Down the shaft over bot1 (7, -0.5).
                yield return new WaitForSeconds(3f);
                hero.transform.position = new Vector3(7f, 6f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                for (float t = Time.time + 4f; Time.time < t && GameManager.instance.sceneName == room;) yield return null;
                yield return new WaitForSeconds(2f);
                Plugin.Log.LogInfo($"[AutoTest] 电梯 从电梯井往下掉：现在 {GameManager.instance.sceneName} {(Vector2)HeroController.instance.transform.position:F1}");
                // Under the room, falling: a pit.
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2f);
                hero = HeroController.instance;
                var region = TerraWorld.Live;
                float bottom = region != null ? region.CellRect.yMin * CellGrid.CellSize : 0f;
                int fell = WorldBorder.DevFellOut;
                Plugin.Log.LogInfo($"[AutoTest] 电梯 掉之前：最近站过的地面 {WorldBorder.DevSafeGround}，原版复活点 {PlayerData.instance.hazardRespawnLocation}，站着 {hero.cState.onGround}，主角 {(Vector2)hero.transform.position:F1}");
                // Into the lift's shaft, then out under the room's edge, where no gate is.
                hero.transform.position = new Vector3(30f, bottom + 1f, hero.transform.position.z);
                yield return null;
                hero.transform.position = new Vector3(30f, bottom - 4f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(0f, -10f);
                float until = Time.time + 6f;
                while (Time.time < until && WorldBorder.DevFellOut == fell) yield return null;
                yield return new WaitForSeconds(2.5f);
                var at = (Vector2)HeroController.instance.transform.position;
                Plugin.Log.LogInfo($"[AutoTest] 电梯 换落脚点的判断：{WorldBorder.DevLastCheck}；之后原版复活点 {PlayerData.instance.hazardRespawnLocation}");
                Check("掉出房间底部", WorldBorder.DevFellOut == fell + 1 && at.y > bottom, $"送回 {WorldBorder.DevFellOut - fell} 次，现在 {GameManager.instance.sceneName} {at:F1}（房间底 {bottom:F1}）");
            }
            finally { Player.TerraStats.DevImmortal = false; }
            Plugin.Log.LogInfo($"[AutoTest] 电梯 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
