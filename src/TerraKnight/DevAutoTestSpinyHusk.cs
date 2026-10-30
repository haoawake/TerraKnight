using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spinyhusk", the user's 10-28 "多刺躯壳死了还在放刺" and 10-30 "改完之后好像不会爆刺了，死不了了"): a Spiny
    /// Husk (GardenZombie) in each enemy AI (TK_SPINY_AI: vanilla, new or both, TK_SPINY_SCENE the room) struck down; its
    /// corpse must burst (Spine Hit on), the ring go off after SpinyCorpse.Hurts, the corpse turn vulnerable and break under
    /// a Terraria blow without a second kill counted, with no new-AI brain or shots of its own. Lines "[AutoTest] 多刺躯壳 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpinyHuskTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            string which = System.Environment.GetEnvironmentVariable("TK_SPINY_AI") ?? "both";
            string room = System.Environment.GetEnvironmentVariable("TK_SPINY_SCENE") ?? "Fungus3_22";
            var modes = new List<World.EnemyAiKind>();
            if (which != "new") modes.Add(World.EnemyAiKind.Vanilla);
            if (which != "vanilla") modes.Add(World.EnemyAiKind.New);
            var keepMode = World.EnemyAiMode.Current;
            int ok = 0, all = 0;
            foreach (var mode in modes)
            {
                World.EnemyAiMode.Current = mode;
                string tag = mode == World.EnemyAiKind.New ? "新版" : "原版";
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                var husks = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == "GardenZombie")
                    .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).ToList();
                all++;
                if (husks.Count == 0) { Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：{room} 里没有多刺躯壳 不通过"); continue; }
                var hm = husks.FirstOrDefault(h => h.GetComponents<PlayMakerFSM>().All(f => f.ActiveStateName != "Spawn Idle")) ?? husks[0];
                var col = hm.GetComponent<Collider2D>();
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：{room} 有 {husks.Count} 只，打 {hm.name} 在 {(Vector2)hm.transform.position:F1}");
                hero.transform.position = Clear(hero, (Vector2)hm.transform.position + new Vector2(-4f, 0.5f));
                // Beside it a few seconds, so the new AI has it in hand; it may die by itself meanwhile (lunging into the thorns).
                var diedAt = (Vector2)hm.transform.position;
                for (float w0 = Time.time; Time.time - w0 < 4f && hm != null && !hm.isDead;)
                {
                    diedAt = hm.transform.position;
                    yield return null;
                }
                if (hm != null && !hm.isDead)
                {
                    diedAt = hm.transform.position;
                    hm.hp = 1;
                    if (col != null && col.enabled) mod.Melee.HitWith(col, 9999, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left);
                    yield return new WaitForSeconds(0.3f);
                }
                if (hm != null && !hm.isDead) HarmonyLib.Traverse.Create(hm).Method("Die", new[] { typeof(float?), typeof(AttackTypes), typeof(bool) }).GetValue(null, AttackTypes.Generic, true);
                if (hm != null && !hm.isDead) { Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：打不死 不通过"); continue; }
                int shots0 = EnemyAi.EnemyAi.DevShots, bursts0 = SpinyCorpse.DevBursts;

                // Its corpse: when the ring comes on and goes off, when its invincibility ends.
                CorpseSpineBurst corpse = null;
                HealthManager corpseHm = null;
                float ringOn = -1f, ringOff = -1f, open = -1f;
                for (float t0 = Time.time; Time.time - t0 < 6f;)
                {
                    float at = Time.time - t0;
                    if (corpse == null)
                    {
                        foreach (var c in Object.FindObjectsByType<CorpseSpineBurst>(FindObjectsSortMode.None))
                            if (c != null && c.gameObject.activeInHierarchy && Vector2.Distance(c.transform.position, diedAt) < 12f) { corpse = c; break; }
                        if (corpse != null)
                        {
                            corpseHm = corpse.GetComponent<HealthManager>();
                            Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 死后 {at:F2}s 尸体 {PathOf(corpse.transform)} 在 {(Vector2)corpse.transform.position:F1}，无敌 {corpseHm?.IsInvincible}，血 {corpseHm?.hp}");
                        }
                    }
                    if (corpse != null)
                    {
                        bool on = corpse.spineHit != null && corpse.spineHit.activeInHierarchy;
                        if (on && ringOn < 0f) { ringOn = at; Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 死后 {at:F2}s 炸刺（伤害圈开）"); }
                        if (!on && ringOn >= 0f && ringOff < 0f) { ringOff = at; Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 死后 {at:F2}s 伤害圈关"); }
                        if (corpseHm != null && !corpseHm.IsInvincible && open < 0f) { open = at; Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 死后 {at:F2}s 尸体可以打了"); }
                    }
                    yield return null;
                }
                if (corpse == null) { Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：没找到尸体 不通过"); continue; }
                bool brained = corpse.GetComponent<EnemyAi.Brain>() != null;
                int shots = EnemyAi.EnemyAi.DevShots - shots0;

                // Struck apart by an early weapon's blows (30 each, at most three) from beside it.
                bool broken = false, credited = false;
                int blows = 0;
                var ccol = corpse.GetComponent<Collider2D>();
                if (corpseHm != null && ccol != null)
                {
                    int kills0 = Bestiary.KillsOf("GardenZombie");
                    hero.transform.position = Clear(hero, (Vector2)ccol.bounds.center + new Vector2(-2f, 0.5f));
                    yield return new WaitForSeconds(0.2f);
                    var cinfo = EnemyStats.Of(corpseHm);
                    Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：打尸体之前 血 {corpseHm.hp}，泰拉生命 {EnemyStats.Life(corpseHm, cinfo)}/{EnemyStats.LifeMax(cinfo)} 防御 {cinfo.Defense}，碰撞 {ccol.enabled}，层 {corpse.gameObject.layer}，记录 {UI.EnemyHover.RecordOf(corpseHm) ?? "无"}");
                    while (blows < 3 && !broken)
                    {
                        if (ccol != null && ccol.enabled) mod.Melee.HitWith(ccol, 30, 0f, 0f, 1f, (Vector2)ccol.bounds.center + Vector2.left);
                        blows++;
                        for (float h0 = Time.time; Time.time - h0 < 0.5f && !broken;)
                        {
                            yield return null;
                            broken = corpseHm == null || corpseHm.isDead || !corpseHm.gameObject.activeInHierarchy;
                        }
                    }
                    yield return new WaitForSeconds(0.3f);
                    credited = Bestiary.KillsOf("GardenZombie") != kills0 || corpseHm != null && Bestiary.Credited(corpseHm);
                    Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：打尸体 {blows} 下 碎了 {broken}（血 {(corpseHm != null ? corpseHm.hp : -1)}，挡下 {MeleeHits.DevBlocked}），记成一次击杀 {credited}");
                }
                bool burst = ringOn >= 0f && SpinyCorpse.DevBursts > bursts0;
                bool brief = ringOff >= 0f && ringOff - ringOn <= SpinyCorpse.Hurts + 0.3f;
                bool pass = burst && brief && open >= 0f && broken && !credited && !brained && shots == 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：炸刺 {burst}（{ringOn:F2}s），伤害圈 {(ringOff >= 0f ? $"{ringOff - ringOn:F2}s 后关" : "没关")}，可以打 {(open >= 0f ? $"{open:F2}s" : "从没")}，打碎 {broken}，多算击杀 {credited}，尸体挂了新版 AI {brained}，射击 {shots} 次 {(pass ? "通过" : "不通过")}");
            }
            World.EnemyAiMode.Current = keepMode;
            Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 合计 {ok}/{all}");
        }
    }
}
