using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fkhidden", the user's 10-25 "底下的小虫子没死前把空中的假骑士打进二阶段，假骑士就会强行下来而且隐身"): the
    /// False Knight in Crossroads_10 waiting in the roof (Dormant, his picture off) struck by Terraria blows as hard as his
    /// armour (TK_FKH_BEFORE=1: hidden bosses struck as before); then his fight started (BATTLE START), and struck again
    /// once he shows. Lines "[AutoTest] 假骑士隐身 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FkHiddenTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.ArenaGuard.DevNoHidden = Environment.GetEnvironmentVariable("TK_FKH_BEFORE") == "1";
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 假骑士隐身 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Crossroads_10", EntryGateOf("Crossroads_10"));
            HealthManager fk = null;
            for (float until = Time.realtimeSinceStartup + 10f; fk == null && Time.realtimeSinceStartup < until;)
            {
                fk = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.isDead && UI.EnemyHover.RecordOf(h) == "FalseKnight" && h.name.StartsWith("False Knight"));
                yield return null;
            }
            if (fk == null) { Check("找到假骑士", false, "没有"); yield break; }
            var control = fk.GetComponents<PlayMakerFSM>().First(f => f.FsmName == "FalseyControl");
            var mr = fk.GetComponent<MeshRenderer>();
            var col = fk.GetComponent<Collider2D>();
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            string Now() => $"{control.ActiveStateName}，贴图 {(mr != null && mr.enabled ? "显示" : "隐藏")}，生命 {fk.hp}，在 {(Vector2)fk.transform.position:F1}";
            Plugin.Log.LogInfo($"[AutoTest] 假骑士隐身 到场：{Now()}，主角 {(Vector2)hero.transform.position:F1}");
            // On the arena's floor beside where he lands (y 31.6), held there; the room's battle trigger held off meanwhile
            // (it is sent below).
            var battle = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f => f.FsmName == "Battle Control" && f.gameObject.scene.name.StartsWith("Crossroads_10")).ToList();
            foreach (var b in battle) b.enabled = false;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            hero.transform.position = Clear(hero, new Vector2(fk.transform.position.x - 5f, 31.5f));
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 假骑士隐身 主角放到 {(Vector2)hero.transform.position:F1}：场外 {World.ArenaGuard.Outside(fk)}，隐藏 {World.ArenaGuard.Hidden(fk)}，地图外 {World.ArenaGuard.OffMap(fk)}，可操作 {GameHooks.PlayerHasControl()}，{Now()}");
            bool hiddenBefore = mr != null && !mr.enabled;
            string stateBefore = control.ActiveStateName;
            int hp0 = fk.hp;
            Vector2 from = hero.GetComponent<Collider2D>().bounds.center;
            for (int i = 0; i < 6; i++)
            {
                mod.Melee.HitWith(col, Mathf.Max(50, fk.hp), 0f, 0f, 1f, from, autonomous: false);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 假骑士隐身 打了藏着的他之后：{Now()}（打前 {stateBefore}，生命 {hp0}）");
            Check("藏在房顶时打不到", hiddenBefore && fk.hp == hp0 && control.ActiveStateName == stateBefore,
                $"打前藏着 {hiddenBefore}，生命 {hp0} → {fk.hp}，状态 {stateBefore} → {control.ActiveStateName}");
            // His fight begun: he falls in shown, and blows count.
            foreach (var b in battle) b.enabled = true;
            control.SendEvent("BATTLE START");
            for (float until = Time.realtimeSinceStartup + 8f; Time.realtimeSinceStartup < until && !(mr.enabled && control.ActiveStateName.Contains("Idle"));)
                yield return null;
            Check("开战后落地显示", mr.enabled, Now());
            int hp1 = fk.hp;
            from = hero.GetComponent<Collider2D>().bounds.center;
            mod.Melee.HitWith(col, 20, 0f, 0f, 1f, from, autonomous: false);
            yield return new WaitForSeconds(0.5f);
            Check("开战后打得到", fk.hp < hp1, $"生命 {hp1} → {fk.hp}，{Now()}");
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士隐身 合计 {ok}/{all}");
        }
    }
}
