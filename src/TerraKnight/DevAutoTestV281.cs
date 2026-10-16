using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "v281"): 2.8.0's later fixes at a glance. The buff stations' buffs last until death; a phase is half a
    /// stage boss's life; enemies' weapons and accessories come with Terraria's natural prefixes (the nails never); the
    /// stages before the unlocked one brought up to it; the Terraria settings open; King's Pass's spikes dug out; the
    /// nail's up-slash knocks an enemy up; a dead enemy's corpse stays.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator V281Test()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 2.8.0补充 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Tutorial_01", "right1");
            yield return new WaitForSeconds(2f);

            // The buff stations' buffs: on until death, shown without a time; a death ends them.
            Player.PlayerBuffs.Add(159, Player.PlayerBuffs.Forever);
            yield return new WaitForSeconds(1f);
            int left = Player.PlayerBuffs.TimeLeft(159);
            bool noTime = Player.PlayerBuffs.NoTime(159);
            Player.PlayerBuffs.Tick(true);
            Check("增益站的增益持续到死亡", left > 100000000 && noTime && !Player.PlayerBuffs.Has(159), $"剩 {left} 帧，不显示时间 {noTime}，死后还在 {Player.PlayerBuffs.Has(159)}");

            Check("多阶段首领每阶段一半", Mathf.Abs(BossPhases.Mult(0) - 0.5f) < 0.001f && Mathf.Abs(BossPhases.Mult(1) - 0.6f) < 0.001f, $"{BossPhases.Mult(0):F2} / {BossPhases.Mult(1):F2}");

            int none = 0, n = 4000, nailPrefixed = 0;
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int p = Prefixes.RollNatural(ItemDb.Get(ItemType.IronBroadsword));
                if (p == 0) none++;
                seen.Add(p);
                if (Prefixes.RollNatural(ItemDb.Get(ItemType.KnightNail)) != 0) nailPrefixed++;
            }
            float rate = 1f - none / (float)n;
            Check("掉落的武器带原版随机词缀", rate > 0.55f && rate < 0.85f && seen.Count > 10 && nailPrefixed == 0, $"带词缀 {rate:P0}，{seen.Count} 种，骨钉带词缀 {nailPrefixed} 次");

            int unlocked = Shop.UnlockedStage();
            int lifted = Areas.StatLevel(0);
            Check("前面阶段的怪追平当前阶段", lifted == Mathf.Max(0, unlocked) && Mathf.Abs(Areas.CatchUp(0) - (unlocked > 0 ? 0.9f : 1f)) < 0.001f,
                $"解锁 {unlocked}，阶段 0 的怪按 {lifted} 级，再乘 {Areas.CatchUp(0):F2}");

            UI.IngameOptions.Show();
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            bool open = UI.IngameOptions.Open;
            UI.IngameOptions.Hide();
            Check("泰拉瑞亚设置打得开", open);

            // King's Pass's spikes: one dug out, its hazard and picture gone with it.
            var hero = HeroController.instance;
            var at = CellGrid.WorldToCell(hero.transform.position);
            Vector2Int spike = new Vector2Int(int.MinValue, 0);
            for (int dx = -150; dx <= 150 && spike.x == int.MinValue; dx++)
                for (int dy = -60; dy <= 60; dy++)
                {
                    var c = new Vector2Int(at.x + dx, at.y + dy);
                    if (DugHazards.IsSpikeCell(c)) { spike = c; break; }
                }
            if (spike.x == int.MinValue) Check("尖刺能挖", false, "没找到尖刺格");
            else
            {
                int cut0 = DugHazards.DevSpikesCut;
                DugHazards.CutSpike(spike);
                yield return new WaitForSeconds(0.5f);
                Check("尖刺能挖", DugHazards.DevSpikesCut > cut0 && !DugHazards.IsSpikeCell(spike), $"格 {spike}，挖掉 {DugHazards.DevSpikesCut - cut0}");
            }

            // An enemy of the room: knocked up by an up-slash, then killed; its corpse stays.
            HealthManager crawler = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != "Tutorial_01") continue;
                var info = EnemyStats.Of(hm);
                if (info == null || info.Boss) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; crawler = hm; }
            }
            if (crawler == null) Check("上劈击退、留下尸体", false, "没有敌人");
            else
            {
                int ups = TerraKnockback.DevPushUps;
                var rb = crawler.GetComponent<Rigidbody2D>();
                TerraKnockback.PushUp(crawler, 6f);
                yield return new WaitForFixedUpdate();
                float vy = rb != null ? rb.velocity.y : 0f;
                Check("骨钉上劈有击退", TerraKnockback.DevPushUps > ups, $"{crawler.name}，上推 {TerraKnockback.DevPushUps - ups} 次，竖直速度 {vy:F1}");
                Vector2 where = crawler.transform.position;
                int removed0 = World.CorpseCleanup.DevRemoved;
                var col = crawler.GetComponent<Collider2D>();
                if (col != null) mod.Melee.HitWith(col, 99999, 0f, 0f, 1f, where);
                yield return new WaitForSeconds(6f);
                int corpses = 0;
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (go.activeInHierarchy && go.name.Contains("Corpse") && Vector2.Distance(go.transform.position, where) < 6f) corpses++;
                Check("怪死了留下尸体", crawler.isDead && corpses > 0 && World.CorpseCleanup.DevRemoved == removed0, $"死了 {crawler.isDead}，附近尸体 {corpses}，清掉 {World.CorpseCleanup.DevRemoved - removed0}");
            }
            PlayerData.instance.isInvincible = false;
            Plugin.Log.LogInfo($"[AutoTest] 2.8.0补充 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
