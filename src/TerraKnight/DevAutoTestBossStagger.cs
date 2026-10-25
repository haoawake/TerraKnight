using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossstagger"): bosses use the same stagger threshold on Master and Hollow. Hornet in Greenpath woken, then struck every frame for eight seconds (a fast weapon's hail) on Master
    /// and again on Hollow: the hits counted towards her stagger (BossBalance.DevStuns), how often her FSM went into a
    /// stun state, her health.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossStaggerTest()
        {
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            var was = World.GameDifficulty.Current;
            try
            {
                foreach (var difficulty in new[] { World.DifficultyLevel.Master, World.DifficultyLevel.Hollow })
                {
                    World.GameDifficulty.Current = difficulty;
                    // Into the arena by Hornet's seal, as a player goes: the fight begun there (BossSummonStart).
                    yield return SealTravel("Town", "left1");
                    var inv = mod.Inventory;
                    var seal = BossSummons.All.First(x => x.Key == "Hornet");
                    Bestiary.Kills["Hornet"] = Mathf.Max(1, Bestiary.KillsOf("Hornet"));
                    Shop.DevStage = seal.Stage;
                    inv.Slots[0] = ItemStack.Of(seal.Type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                    int started = BossSummonStart.DevStarted;
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    for (float t = Time.realtimeSinceStartup + 30f; (!BossArena.Arrived("Hornet") || BossSummonStart.DevStarted == started) && Time.realtimeSinceStartup < t;) { mod.Stats.FullHeal(); yield return null; }
                    for (float t = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) { mod.Stats.FullHeal(); yield return null; }
                    yield return new WaitForSeconds(1f);
                    PlayerData.instance.isInvincible = true;
                    var hero = HeroController.instance;
                    var boss = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.isDead && UI.EnemyHover.RecordOf(h) == "Hornet");
                    if (boss == null) { Plugin.Log.LogInfo($"[AutoTest] 首领硬直 {difficulty} 没找到大黄蜂 不通过"); continue; }
                    var fsm = boss.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Control");
                    var info = EnemyStats.Of(boss);
                    int max = info.MaxHp;
                    int initialHp = boss.hp;
                    if (difficulty == World.DifficultyLevel.Master)
                    {
                        info.MaxHp = 100;
                        foreach (World.DifficultyLevel d in System.Enum.GetValues(typeof(World.DifficultyLevel)))
                        {
                            World.GameDifficulty.Current = d;
                            foreach (int hp in new[] { 100, 41, 40, 39, 1 })
                            {
                                boss.hp = hp;
                                bool expected = hp <= 40;
                                Plugin.Log.LogInfo($"[AutoTest] 首领霸体边界 {d} {hp}% {(BossBalance.SuperArmour(info) == expected ? "通过" : "不通过")}");
                            }
                            boss.hp = 41;
                            Plugin.Log.LogInfo($"[AutoTest] 首领霸体跨线 {d} 41→39% {(BossBalance.SuperArmour(info, 2) ? "通过" : "不通过")}");
                        }
                        World.GameDifficulty.Current = difficulty;
                        info.MaxHp = max;
                        boss.hp = initialHp;
                    }
                    foreach (int percent in new[] { 80, 40 })
                    {
                    for (float settle = Time.time + 3f; Time.time < settle;) { mod.Stats.FullHeal(); yield return null; }
                    int stuns0 = BossBalance.DevStuns, hp0 = boss.hp, stunStates = 0;
                    int nativeHeld0 = BossStunArmour.DevHeld;
                    float nextNative = Time.time;
                    string last = fsm != null ? fsm.ActiveStateName : "";
                    var states = new System.Collections.Generic.HashSet<string>();
                    float end = Time.time + 8f;
                    while (Time.time < end && boss != null && !boss.isDead)
                    {
                        mod.Stats.FullHeal();
                        boss.hp = Mathf.Max(1, max * percent / 100);
                        var col = boss.GetComponent<Collider2D>();
                        if (col != null && col.enabled)
                            mod.Melee.HitWith(col, 150, 0f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center);
                        if (Time.time >= nextNative)
                        {
                            nextNative = Time.time + 0.35f;
                            HarmonyLib.AccessTools.Method(typeof(HealthManager), "TakeDamage").Invoke(boss, new object[] { new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Multiplier = 1f, MagnitudeMultiplier = 0f, IgnoreInvulnerable = true } });
                        }
                        string s = fsm != null ? fsm.ActiveStateName : "";
                        if (s != last)
                        {
                            if (s.IndexOf("Stun", System.StringComparison.OrdinalIgnoreCase) >= 0) stunStates++;
                            states.Add(s);
                            last = s;
                        }
                        yield return null;
                    }
                    int counted = BossBalance.DevStuns - stuns0;
                    bool pass = percent <= 40 ? counted == 0 && stunStates == 0 : counted > 0 && stunStates > 0;
                    Plugin.Log.LogInfo($"[AutoTest] 首领硬直 {difficulty} {percent}%：8 秒每帧挨打，记硬直 {counted} 下，进眩晕 {stunStates} 次，血 {hp0}→{(boss != null ? boss.hp : -1)}，状态 {string.Join("、", states.Take(25))} {(pass ? "通过" : "不通过")}");
                    bool nativePass = percent <= 40 ? BossStunArmour.DevHeld > nativeHeld0 : BossStunArmour.DevHeld == nativeHeld0;
                    Plugin.Log.LogInfo($"[AutoTest] 原生伤害霸体 {difficulty} {percent}% held={BossStunArmour.DevHeld - nativeHeld0} {(nativePass ? "通过" : "不通过")}");
                    }
                    yield return SealTravel("Town", "left1");
                    yield return new WaitForSeconds(1f);
                }
            }
            finally
            {
                World.GameDifficulty.Current = was;
                Player.TerraStats.DevImmortal = false;
                Shop.DevStage = -1;
            }
        }
    }
}
