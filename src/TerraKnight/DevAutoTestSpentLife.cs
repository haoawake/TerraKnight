using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spentlife", the user's 10-26 "扣了480血但我没死…也不回血"): a life spent must end in death. (1) Dirtmouth
    /// entered from King's Pass and the ledge walked off, as the player came in: the fall's damage, then the hero dead.
    /// (2) The life gone while the hero can't be hurt (invulnerable a second): dead once it wears off (Player.TerraHurt.Tick
    /// sends the kill again). Lines "[AutoTest] 生命耗尽 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpentLifeTest()
        {
            mod.DevNoFallDamage = false;
            Player.TerraStats.DevImmortal = false;
            PlayerData.instance.isInvincible = false;
            int ok = 0, all = 0;

            // (1) The ledge from King's Pass.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            mod.Stats.Life = mod.Stats.LifeMax;
            TerraMod.DevLastFall = null;
            Player.TerraMovement.DevMove = 1;
            for (float t = Time.time + 8f; Time.time < t && hero.cState.onGround;) yield return null;
            Player.TerraMovement.DevMove = 0;
            for (float t = Time.time + 10f; Time.time < t && !hero.cState.onGround && !hero.cState.dead;) yield return null;
            bool dead = false;
            for (float t = Time.time + 4f; Time.time < t && !(dead = hero.cState.dead);) yield return null;
            all++;
            bool fell = TerraMod.DevLastFall != null && mod.Stats.IsDead;
            if (fell == dead) ok++;
            Plugin.Log.LogInfo($"[AutoTest] 生命耗尽 从国王山道进德特茅斯走下崖：{TerraMod.DevLastFall ?? "没摔伤"}，死了 {dead}，补发 {Player.TerraHurt.DevFinished} {(fell == dead ? "通过" : "不通过")}");
            if (dead) yield return new WaitForSeconds(9f);

            // (2) Spent while it can't be hurt.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            int finished0 = Player.TerraHurt.DevFinished;
            Player.TerraHurt.Invulnerable(hero, 60);
            yield return null;
            mod.Stats.Life = 0;
            yield return new WaitForSeconds(0.4f);
            bool heldOff = !hero.cState.dead;
            dead = false;
            for (float t = Time.time + 4f; Time.time < t && !(dead = hero.cState.dead);) yield return null;
            all++;
            bool pass = heldOff && dead && Player.TerraHurt.DevFinished > finished0;
            if (pass) ok++;
            Plugin.Log.LogInfo($"[AutoTest] 生命耗尽 无敌时生命归零：无敌期间没死 {heldOff}，过后死了 {dead}，补发 {Player.TerraHurt.DevFinished - finished0} {(pass ? "通过" : "不通过")}");
            if (dead) yield return new WaitForSeconds(9f);
            Plugin.Log.LogInfo($"[AutoTest] 生命耗尽 合计 {ok}/{all}");
        }
    }
}
