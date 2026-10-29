using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossshots", the user's 10-27 "boss对联机队友依旧不造成伤害，比如诺斯克的弹幕"): a boss fight begun by its seal
    /// (TK_BOSSSHOTS keys, default Nosk "MimicSpider" and Hornet), the Knight invincible in it for 20 s; every hurting hitbox up
    /// (DamageHero, hazard 1, damage over 0, on) sorted by the scene it lies in, and whether Net.EnemyAttacks would send it to
    /// the other players for the boss room (Net.EnemyAttacks.DevSends). Lines "[AutoTest] 首领弹幕 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossShotsTest()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            pd.hasDreamNail = true;
            string keys = System.Environment.GetEnvironmentVariable("TK_BOSSSHOTS") ?? "MimicSpider,Hornet";
            int ok = 0, all = 0;
            foreach (var key in keys.Split(','))
            {
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                var seal = BossSummons.All.FirstOrDefault(x => x.Key == key);
                if (seal == null) { Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 {key} 没有封印"); continue; }
                Bestiary.Kills[key] = 1;
                inv.Slots[1] = ItemStack.Of(seal.Type, 3);
                inv.Selected = 1;
                inv.Override = -1;
                for (float t = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                mod.DevUseSelected(HeroController.instance.transform.position);
                for (float t = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < t;) yield return null;
                string room = GameManager.instance.sceneName;
                var byScene = new Dictionary<string, HashSet<string>>();
                int seenOther = 0, sentOther = 0, seenRoom = 0, sentOld = 0;
                var unsent = new HashSet<string>();
                float seconds = float.TryParse(System.Environment.GetEnvironmentVariable("TK_BOSSSHOTS_SECONDS"), out float sx) ? sx : 20f;
                var heroT = HeroController.instance.transform;
                // The boss struck down to half its health, for the moves its later phases bring (Nosk's falling globs).
                HealthManager boss = null;
                int hp0 = 0;
                float nextHit = Time.time + 3f;
                for (float t = Time.time + seconds; Time.time < t;)
                {
                    mod.Stats.FullHeal();
                    if (boss == null && Time.time >= nextHit)
                        foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (h != null && !h.isDead && h.hp > 0 && Vector2.Distance(h.transform.position, heroT.position) < 30f && (boss == null || h.hp > boss.hp)) { boss = h; hp0 = h.hp; }
                    if (boss != null && !boss.isDead && Time.time >= nextHit && boss.hp > hp0 / 2)
                    {
                        nextHit = Time.time + 1.5f;
                        boss.Hit(new HitInstance { Source = HeroController.instance.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 30, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                    }
                    // The FSM-made hurters (damages_hero) too, noted by their name with a '*'.
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    {
                        if (f == null || f.FsmName != "damages_hero" || !f.isActiveAndEnabled || f.GetComponent<DamageHero>() != null) continue;
                        if (FSMUtility.GetInt(f, "damageDealt") <= 0 || Vector2.Distance(f.transform.position, heroT.position) > 40f) continue;
                        string fsc = f.gameObject.scene.name;
                        if (!byScene.TryGetValue(fsc, out var fn)) byScene[fsc] = fn = new HashSet<string>();
                        fn.Add("*" + f.gameObject.name);
                    }
                    foreach (var d in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                    {
                        if (d == null || d.hazardType != 1 || d.damageDealt <= 0 || !d.gameObject.activeInHierarchy) continue;
                        if (!d.GetComponents<Collider2D>().Any(c => c.enabled)) continue;
                        if (Vector2.Distance(d.transform.position, heroT.position) > 40f) continue;
                        string sc = d.gameObject.scene.name;
                        if (!byScene.TryGetValue(sc, out var names)) byScene[sc] = names = new HashSet<string>();
                        string n = d.gameObject.name;
                        int cut = n.IndexOf('(');
                        names.Add(cut > 0 ? n.Substring(0, cut).Trim() : n);
                        bool roomScene = sc == room || sc == room + "_boss";
                        if (roomScene) seenRoom++;
                        else if (sc == "DontDestroyOnLoad")
                        {
                            seenOther++;
                            Net.EnemyAttacks.DevSceneOnly = true;
                            if (Net.EnemyAttacks.DevWouldSend(d, room)) sentOld++;
                            Net.EnemyAttacks.DevSceneOnly = false;
                            if (Net.EnemyAttacks.DevWouldSend(d, room)) sentOther++;
                            else if (unsent.Count < 12)
                            {
                                var cell = World.CellGrid.WorldToCell(d.transform.position);
                                unsent.Add($"{n}@{(Vector2)d.transform.position} 格{cell} 区域 {World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName ?? "无"}");
                            }
                        }
                    }
                    yield return null;
                }
                foreach (var kv in byScene) Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 {key}（{room}）场景 {kv.Key}：{string.Join(",", kv.Value.Take(30))}");
                if (unsent.Count > 0) Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 {key} 没发出的：{string.Join("；", unsent)}");
                if (System.Environment.GetEnvironmentVariable("TK_BOSSSHOTS_DUMP") == "1")
                {
                    foreach (var d in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                        if (d != null && (d.gameObject.scene.name == room || d.gameObject.scene.name == room + "_boss"))
                        {
                            var root = d.transform.root;
                            Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 状态机 {PathOf(d.transform)}：HM {d.GetComponentInParent<HealthManager>()?.name}");
                            DumpFsms(root);
                            break;
                        }
                }
                all++;
                bool pass = seenOther > 0 && sentOther == seenOther && sentOld == 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 {key} 首领 {(boss != null ? $"{boss.name} 血 {hp0}→{boss.hp}" : "没找到")}");
                Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 {key}：房间里的判定 {seenRoom} 帧次，对象池（常驻场景）里的 {seenOther} 帧次，发给队友：修复前 {sentOld}，修复后 {sentOther} {(pass ? "通过" : "不通过")}");
                Shot(room, $"bossshots_{key}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 首领弹幕 合计 {ok}/{all}");
        }
    }
}
