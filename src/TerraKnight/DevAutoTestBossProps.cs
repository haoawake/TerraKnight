using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossprops"): what a boss sends out that the nail breaks or knocks away (Hornet Sentinel's barbs,
    /// False Knight's falling barrels, Dung Defender's dung balls, Hive Knight's droppers) answers each class's weapon:
    /// a sword, a bow, a wand, a whip, and a minion running into it. Each is struck while it waits for the nail, and its
    /// FSM must take the hit's road (the transition its hit event leads to).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string Colliders(GameObject go)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in go.GetComponentsInChildren<Collider2D>())
                sb.Append($"{c.name}[层{c.gameObject.layer}{(c.isTrigger ? " 触发" : "")}{(c.enabled ? "" : " 关")} {c.bounds.size.x:F1}]");
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) sb.Append($" 刚体 {rb.bodyType} 速度 {rb.velocity.magnitude:F1}");
            return sb.ToString();
        }

        IEnumerator BossPropsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 首领召唤物 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            mod.DevNoFallDamage = true;
            bool wasInfinite = mod.Inventory.Infinite;
            mod.SetInfiniteItems(true);
            var hitEvents = new HashSet<string> { "BLOCKED HIT", "NAIL HIT", "SMACKED", "TAKE DAMAGE", "HIT" };
            // Hornet Sentinel and the False Knight in their Godhome halls (the fight at once, no walk-in or words first).
            var bosses = new (string key, string gg, string prefix, string fsm, string ready)[]
            {
                ("Hornet@4", "GG_Hornet_2", "Hornet Barb", "Control", "Barb"),
                ("FalseKnight", "GG_False_Knight", "Falling Barrel", "Fall Barrel Control", "Idle"),
                ("DungDefender", null, "Dung Ball", "Nail Hit", "Detecting"),
                ("HiveKnight", null, "Bee Dropper", "Recoil", "Idle"),
            };
            var ways = new (string label, ItemType item)[]
            {
                ("铁阔剑", ItemType.IronBroadsword), ("木弓", ItemType.WoodenBow), ("火花魔杖", ItemType.WandofSparking),
                ("皮鞭", ItemType.LeatherWhip), ("召唤物撞上", ItemType.None),
            };
            foreach (var (key, gg, prefix, fsmName, ready) in bosses)
            {
                yield return SealTravel("Town", "left1");
                PlayerData.instance.isInvincible = true;
                if (gg != null)
                {
                    // As BossArena does for a Godhome fight: the hall's boss level and the dream's way back.
                    BossSceneController.SetupEvent = self => { self.BossLevel = 0; self.DreamReturnEvent = "DREAM RETURN"; };
                    PlayerData.instance.dreamReturnScene = "Town";
                    PlayerData.instance.bossReturnEntryGate = "";
                    yield return SealTravel(gg, "door_dreamEnter");
                }
                else
                {
                    if (!BossArena.Fight(key)) { Check(key + " 进场", false); continue; }
                    float until = Time.realtimeSinceStartup + 30f;
                    while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                }
                yield return new WaitForSeconds(2f);
                string scene = GameManager.instance.sceneName;
                bool logged = false;
                int way = 0, tries = 0;
                float stop = Time.realtimeSinceStartup + 90f;
                var tried = new HashSet<int>();
                float nextNear = 0f, nextLog = 0f;
                string bossLine = "";
                while (way < ways.Length && Time.realtimeSinceStartup < stop)
                {
                    PlayerData.instance.isInvincible = true;
                    var hero = HeroController.SilentInstance;
                    // The boss comes at the player: the hero goes in (its battle starts) and stays near it so it keeps attacking.
                    if (Time.realtimeSinceStartup >= nextNear && hero != null)
                    {
                        nextNear = Time.realtimeSinceStartup + 4f;
                        HealthManager boss = null;
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        {
                            var info = EnemyStats.Of(hm);
                            if (hm.isDead || !hm.gameObject.activeInHierarchy || info == null || !info.Boss) continue;
                            if (boss == null || hm.hp > boss.hp) boss = hm;
                        }
                        if (boss != null)
                        {
                            var bc = boss.GetComponent<Collider2D>();
                            Vector2 bp = bc != null ? (Vector2)bc.bounds.center : (Vector2)boss.transform.position;
                            // In a hall the fight comes to the player; in the world the player goes to it (its battle starts).
                            if (gg == null) hero.transform.position = Clear(hero, bp + new Vector2(bp.x > hero.transform.position.x ? -5f : 5f, 0f));
                            // A sleeping one (the False Knight in his hall) woken, an idle one stirred: a light blow.
                            if (bc != null) mod.Melee.HitWith(bc, 1, 0f, 0f, 1f, (Vector2)hero.transform.position);
                            var sb = new System.Text.StringBuilder($"{boss.name} 血 {boss.hp} 无敌 {boss.IsInvincible} 位置 {bp:F1}");
                            foreach (var f in boss.GetComponents<PlayMakerFSM>()) sb.Append($" {f.FsmName}:{f.ActiveStateName}");
                            bossLine = sb.ToString();
                        }
                        else bossLine = "场上没有首领";
                    }
                    PlayMakerFSM found = null;
                    var states = new List<string>();
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    {
                        if (f == null || f.FsmName != fsmName || !f.gameObject.activeInHierarchy || !f.gameObject.name.StartsWith(prefix)) continue;
                        var rend = f.GetComponent<Renderer>();
                        var c0 = f.GetComponent<Collider2D>() ?? f.GetComponentInChildren<Collider2D>();
                        states.Add($"{f.ActiveStateName}{(rend != null && !rend.enabled ? "(隐)" : "")}{(c0 != null && !c0.enabled ? "(无碰撞)" : "")}");
                        // Out and to be struck: shown, its collider on, waiting for the nail.
                        if (rend != null && !rend.enabled || c0 == null || !c0.enabled) continue;
                        if (f.ActiveStateName != ready || tried.Contains(f.GetInstanceID())) continue;
                        if (found == null) found = f;
                    }
                    if (Time.realtimeSinceStartup >= nextLog)
                    {
                        nextLog = Time.realtimeSinceStartup + 10f;
                        Plugin.Log.LogInfo($"[AutoTest] 首领召唤物 {key} 等 {prefix}：场上 {states.Count} 个 [{string.Join("，", states)}]，主角 {hero?.transform.position}，{bossLine}");
                    }
                    if (found == null) { yield return new WaitForSeconds(0.1f); continue; }
                    tried.Add(found.GetInstanceID());
                    var targets = new HashSet<string>();
                    foreach (var st in found.FsmStates)
                        if (st.Name == ready)
                            foreach (var tr in st.Transitions)
                                if (hitEvents.Contains(tr.EventName)) targets.Add(tr.ToState);
                    if (!logged)
                    {
                        logged = true;
                        var sb = new System.Text.StringBuilder();
                        foreach (var st in found.FsmStates)
                            if (st.Name == ready)
                                foreach (var tr in st.Transitions) sb.Append($"{tr.EventName}->{tr.ToState}；");
                        Plugin.Log.LogInfo($"[AutoTest] 首领召唤物 {key} {found.gameObject.name} 层 {found.gameObject.layer}，{ready} 的转移：{sb}");
                    }
                    var col = found.GetComponent<Collider2D>() ?? found.GetComponentInChildren<Collider2D>();
                    var (label, item) = ways[way];
                    var seen = new List<string>();
                    bool hit = false;
                    var watched = found.Fsm;
                    System.Action<HutongGames.PlayMaker.FsmState> onEnter = st => { if (st.Fsm == watched && targets.Contains(st.Name)) hit = true; };
                    BossWake.DevEntered += onEnter;
                    int pulses0 = PropHits.DevPulses, tinks0 = PropHits.DevTinkEvents, touch0 = NailAttackProxy.DevReceivedTouches;
                    for (float t = 0f; t < 1.2f && !hit; t += Time.deltaTime)
                    {
                        if (found == null || !found.gameObject.activeInHierarchy) break;
                        var at = (Vector2)(col != null ? col.bounds.center : found.transform.position);
                        hero.transform.position = Clear(hero, at + new Vector2(at.x > hero.transform.position.x ? -2.2f : 2.2f, -0.5f));
                        hero.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                        if (item == ItemType.None)
                        {
                            // Minions.Damage's road for something it runs into that isn't an enemy.
                            if (col != null && PropHits.Hostile(col)) PropHits.Pulse(col, at + Vector2.left * 0.5f, 1f, 20);
                        }
                        else if (mod.DevReady) mod.DevUse(item, at);
                        yield return null;
                        string now = found != null ? found.ActiveStateName : "无";
                        if (seen.Count == 0 || seen[seen.Count - 1] != now) seen.Add(now);
                        hit |= targets.Contains(now);
                    }
                    BossWake.DevEntered -= onEnter;
                    if (!hit && tries < 2 && (found == null || !found.gameObject.activeInHierarchy || found.ActiveStateName != ready && !targets.Contains(found.ActiveStateName)))
                    {
                        // Gone its own way before the blow landed (a barrel that hit the floor): another one.
                        tries++;
                        continue;
                    }
                    Check($"{key} {prefix} 被{label}打中", hit, $"状态 {string.Join(">", seen)}，目标 {string.Join("/", targets)}，替身斩击 +{PropHits.DevPulses - pulses0}，叮 +{PropHits.DevTinkEvents - tinks0}，受击检测碰到 +{NailAttackProxy.DevReceivedTouches - touch0}，碰撞体 {Colliders(found.gameObject)}");
                    if (way == 0) Shot(scene, "bossprops_" + key.Replace("@", "_"));
                    way++;
                    tries = 0;
                }
                if (way < ways.Length) Check($"{key} 等到 {prefix}", false, $"只试了 {way}/{ways.Length} 种");
                if (gg != null) BossSceneController.SetupEvent = null;
            }
            mod.SetInfiniteItems(wasInfinite);
            PlayerData.instance.isInvincible = false;
            yield return SealTravel("Town", "left1");
            Plugin.Log.LogInfo($"[AutoTest] 首领召唤物 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
