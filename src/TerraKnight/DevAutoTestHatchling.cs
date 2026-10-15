using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hatchling"): the Aspid Mother in Crossroads_19 and the hatchlings she lets out, three
    /// broods: each hatchling's state (dead flag, invincibility, collider, layer, whether the Terraria
    /// weapons, the homing shots and the minions would take it as a target) and a 1-damage Terraria hit
    /// on it; then the brood killed and the next one (the same pooled hatchlings) checked again.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string HatchlingState(HealthManager hm, out bool targetable)
        {
            var col = hm.GetComponent<Collider2D>();
            bool untouchable = EnemyStats.Untouchable(hm);
            bool offMap = World.ArenaGuard.OffMap(hm), outside = World.ArenaGuard.Outside(hm);
            bool onEnemyLayer = col != null && col.gameObject.layer == 11;
            targetable = !hm.isDead && hm.hp > 0 && col != null && col.enabled && !untouchable && !offMap && !outside && onEnemyLayer;
            return $"{PathOf(hm.transform)} {(Vector2)hm.transform.position:F1} 血 {hm.hp} 死 {hm.isDead} 无敌 {hm.IsInvincible}/{hm.InvincibleFromDirection} " +
                   $"碰撞 {(col != null ? $"{col.GetType().Name} 启用 {col.enabled} 触发 {col.isTrigger} 层 {col.gameObject.layer}" : "无")} 碰不到 {untouchable} 地图外 {offMap} 场外 {outside} 能选中 {targetable}";
        }

        IEnumerator HatchlingTest()
        {
            yield return GoTo("Crossroads_19", EntryGateOf("Crossroads_19"));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            HealthManager mother = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm.gameObject.scene.name == "Crossroads_19" && hm.name.StartsWith("Hatcher") && !hm.name.Contains("Baby")) mother = hm;
            if (mother == null) { Plugin.Log.LogInfo("[AutoTest] 阿斯匹德幼体 Crossroads_19 没有阿斯匹德之母 不通过"); yield break; }
            var melee = mod.Melee;
            int problems = 0;
            for (int brood = 1; brood <= 3; brood++)
            {
                // Near her, so she lets her young out.
                hero.transform.position = Clear(hero, (Vector2)mother.transform.position + new Vector2(-5f, -1f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                var babies = new List<HealthManager>();
                float t0 = Time.time;
                while (Time.time - t0 < 20f)
                {
                    PlayerData.instance.isInvincible = true;
                    babies.Clear();
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (hm.name.Contains("Hatcher Baby") && hm.gameObject.activeInHierarchy && !hm.isDead && hm.hp > 0) babies.Add(hm);
                    if (babies.Count >= 2 && Time.time - t0 > 3f) break;
                    yield return new WaitForSeconds(0.5f);
                }
                yield return new WaitForSeconds(1.5f);
                // The ones out of the cage (the pool waits off the map: not taken as targets, rightly).
                babies.RemoveAll(x => x == null || World.ArenaGuard.OffMap(x));
                var sb = new StringBuilder();
                int targetable = 0, stuck = 0;
                foreach (var b in babies) { HatchlingState(b, out bool can0); if (can0) targetable++; }
                // The first one hit down a quarter of its life at a time, its state after each blow, until it is gone.
                if (babies.Count > 0)
                {
                    var b = babies[0];
                    var info = EnemyStats.Of(b);
                    int quarter = Mathf.Max(1, EnemyStats.LifeMax(info) / 4 + 1);
                    for (int k = 0; k < 10 && b != null && b.gameObject.activeInHierarchy && !World.ArenaGuard.OffMap(b); k++)
                    {
                        int hp0 = b.hp;
                        var col = b.GetComponent<Collider2D>();
                        bool canBefore;
                        HatchlingState(b, out canBefore);
                        if (col != null && canBefore) melee.HitWith(col, quarter, 0f, 0f, 1f, col.bounds.center);
                        yield return new WaitForSeconds(0.35f);
                        if (b == null) break;
                        string state = HatchlingState(b, out bool can);
                        bool alive = b.gameObject.activeInHierarchy && !World.ArenaGuard.OffMap(b);
                        // Still flying about, yet nothing would hit it any more.
                        if (alive && !can) stuck++;
                        sb.Append($"\n    第 {k + 1} 下（记录 {UI.EnemyHover.RecordOf(b)}，记过击杀 {Bestiary.Credited(b)}，{quarter} 点，生命 {EnemyStats.Life(b, info)}/{EnemyStats.LifeMax(info)}）原版血 {hp0}→{b.hp}：{state}{(alive && !can ? " ← 还在飞却选不中" : "")}");
                    }
                }
                if (stuck > 0 || targetable < babies.Count) problems++;
                Plugin.Log.LogInfo($"[AutoTest] 阿斯匹德幼体 第 {brood} 窝 放出来 {babies.Count} 只，能选中 {targetable}，图鉴击杀 {Bestiary.KillsOf("Hatchling")}（被池当场收回的 {Bestiary.DevPooledDeaths}，复用记新 {Bestiary.DevRevived}）；一只一下下打：{sb}");
                if (brood == 1) { Shot("Crossroads_19", "hatchling"); yield return null; }
                // The brood killed: its hatchlings go back to the pool for the next.
                foreach (var b in babies)
                    if (b != null && !b.isDead)
                    {
                        var col = b.GetComponent<Collider2D>();
                        if (col != null) melee.HitWith(col, 9999, 0f, 0f, 1f, col.bounds.center);
                    }
                yield return new WaitForSeconds(2f);
            }
            int kills = Bestiary.KillsOf("Hatchling");
            if (kills < 3) problems++;
            Plugin.Log.LogInfo($"[AutoTest] 阿斯匹德幼体 三窝里有问题的 {problems} 窝（图鉴击杀 {kills}）{(problems == 0 ? "通过" : "不通过")}");
        }
    }
}
