using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossLifecycleTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            yield return GoTo("Town", "left1");
            var evidence = new List<object>();
            int checks = 0, failures = 0;
            void Check(string name, bool ok)
            {
                checks++; evidence.Add(new { Name = name, Passed = ok });
                if (!ok) { failures++; Plugin.Log.LogError("[AutoTest] 首领生命周期/" + name + " 不通过"); }
            }
            int Kills(string record) => Bestiary.Kills.TryGetValue(record, out int n) ? n : 0;
            HealthManager Body(string name, int hp = 100)
            {
                var go = new GameObject(name);
                var hm = go.AddComponent<HealthManager>(); hm.hp = hp; hm.isDead = false;
                return hm;
            }
            foreach (var group in new[] {
                (Record: "MantisLord", Names: new[] { "Mantis Lord", "Mantis Lord S1", "Mantis Lord S2" }),
                (Record: "MageLord", Names: new[] { "Mage Lord", "Mage Lord Phase2" }),
                (Record: "BlackKnight", Names: new[] { "Black Knight 1", "Black Knight 2", "Black Knight 3", "Black Knight 4", "Black Knight 5", "Black Knight 6" }),
                (Record: "NailBros", Names: new[] { "Oro", "Mato" }),
                (Record: "Oblobble", Names: new[] { "Oblobble", "Oblobble (1)" }) })
            {
                var bodies = new List<HealthManager>();
                int start = Kills(group.Record);
                foreach (var name in group.Names) { var hm = Body(name); hm.gameObject.SetActive(false); bodies.Add(hm); }
                try
                {
                    for (int i = 0; i < bodies.Count; i++)
                    {
                        var hm = bodies[i]; hm.gameObject.SetActive(true); hm.hp = 0; hm.isDead = true;
                        Check(group.Record + "/识别身体" + i, UI.EnemyHover.RecordOf(hm) == group.Record);
                        Bestiary.Credit(hm);
                        Check(group.Record + "/阶段死亡" + i, Kills(group.Record) == start + (i == bodies.Count - 1 ? 1 : 0));
                        // Explicitly exercise FSM-written individual journal kills, outside HealthManager.Die.
                        AccessTools.Method(typeof(SpecialDeaths), "CreditByFlag").Invoke(null, new object[] { "killed" + group.Record, group.Record });
                        Check(group.Record + "/图鉴标记不提前或重复结算" + i, Kills(group.Record) == start + (i == bodies.Count - 1 ? 1 : 0));
                        hm.gameObject.SetActive(false);
                    }
                    foreach (var hm in bodies) Bestiary.Credit(hm);
                    Check(group.Record + "/重复回调只有一次掉落任务", Kills(group.Record) == start + 1 && BossLootTiming.DevPending == 1);
                }
                finally
                {
                    BossLootTiming.Clear();
                    foreach (var hm in bodies) UnityEngine.Object.Destroy(hm.gameObject);
                }
            }
            var armour = Body("False Knight New", 65);
            var head = Body("Head", 40); head.transform.SetParent(armour.transform);
            var ai = EnemyStats.Of(armour); var hi = EnemyStats.Of(head);
            Check("假骑士头部继承首领身份", hi.Boss && hi.Record == "FalseKnight" && hi.Ref == ai.Ref);
            Check("假骑士铠甲配额大于头部", ai.MaxHp * ai.Scale > hi.MaxHp * hi.Scale);
            Check("假骑士铠甲头部配额比10比3", Mathf.Abs((ai.MaxHp * ai.Scale) / (hi.MaxHp * hi.Scale) - 10f / 3f) < 0.001f);
            UnityEngine.Object.Destroy(armour.gameObject);
            var crawler = Body("Baby Centipede");
            Check("刻尘者识别正确", UI.EnemyHover.RecordOf(crawler) == "BabyCentipede");
            Check("刻尘者沿表面移动不被抛离轨道", TerraKnockback.Immune(crawler));
            UnityEngine.Object.Destroy(crawler.gameObject);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "boss-lifecycle-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Evidence = evidence }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 首领生命周期 {checks} 项，失败 {failures}");
        }
    }
}
