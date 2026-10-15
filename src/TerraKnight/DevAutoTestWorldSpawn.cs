using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "worldspawn"): a new game's player dying before resting on any bench comes back
    /// at the world's spawn, where the opening's portal set it down in King's Pass, not on
    /// Dirtmouth's bench (not unlocked yet): once dying right at the start, once in Dirtmouth.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WorldSpawnTest()
        {
            float until = Time.time + 30f;
            while ((Player.OpeningPortal.Active || HeroController.instance == null || !HeroController.instance.acceptingInput) && Time.time < until) yield return null;
            yield return new WaitForSeconds(1f);
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            Vector2 start = Player.OpeningPortal.DevFeet;
            Plugin.Log.LogInfo($"[AutoTest] 出生点 开局 {gm.sceneName} 脚下 {start:F1}，复活点 {pd.respawnScene}/{pd.respawnMarkerName}/{pd.respawnType}");
            int ok = 0, all = 0;
            foreach (var where in new[] { "开局的地方", "德特茅斯" })
            {
                if (where == "德特茅斯")
                {
                    yield return GoTo("Town", "left1");
                    yield return new WaitForSeconds(1.5f);
                }
                var hero = HeroController.instance;
                string diedIn = gm.sceneName;
                Vector2 diedAt = hero.transform.position;
                pd.isInvincible = false;
                mod.DevNoFallDamage = false;
                mod.Stats.TakeDirect(999999);
                hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
                until = Time.time + 10f;
                while (!hero.cState.dead && Time.time < until) yield return null;
                bool died = hero.cState.dead;
                until = Time.time + 30f;
                while ((hero.cState.dead || gm.IsInSceneTransition || !hero.acceptingInput || hero.controlReqlinquished) && Time.time < until)
                {
                    yield return null;
                    hero = HeroController.instance;
                }
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                Vector2 back = hero.transform.position;
                Shot(gm.sceneName, "worldspawn_" + (where == "德特茅斯" ? "town" : "start"));
                yield return null;
                all++;
                bool pass = died && gm.sceneName == "Tutorial_01" && Mathf.Abs(back.x - start.x) < 3f && Mathf.Abs(back.y - start.y) < 3f;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 出生点 在{where}（{diedIn} {diedAt:F1}）{(died ? "死了" : "没死成")}，复活在 {gm.sceneName} {back:F1}（开局脚下 {start:F1}），复活点 {pd.respawnScene}/{pd.respawnMarkerName}/{pd.respawnType}，" +
                                   $"能动 {hero.acceptingInput} {(pass ? "通过" : "不通过")}");
            }
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            Plugin.Log.LogInfo($"[AutoTest] 出生点 合计 {ok}/{all}");
        }
    }
}
