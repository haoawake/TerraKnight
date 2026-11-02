using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hornet2", a player's 11-01 report via the user: "有人用中士联盾的追踪效果把二见大黄蜂打飞了，然后传送走再离开就出现了空气墙导致死档"):
    /// Hornet at Kingdom's Edge fought with the Sergeant United Shield thrown at her (TK_H2_WEAPONS new/vanilla, TK_H2_AI
    /// vanilla/new, TK_H2_ENTRY world/seal), her life brought down over TK_H2_SECONDS. Watched each frame: where she is, how
    /// fast she goes, her state; then her corpse (Corpse Hornet Outskirts: launched, landed, waiting for the talk, her leave by
    /// her thread) with the shield still thrown, the battle scene, its gates and walls after her leave, and the way out by each
    /// gate. TK_H2_PARTS "fight" (the whole fight), "teleport" (away mid-fight and back, then out). Lines "[AutoTest] 黄蜂二 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Hornet2Test()
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 黄蜂二 需要隔离存档，不通过"); yield break; }
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 {what} {(pass ? "通过" : "不通过")}");
            }
            var parts = (Environment.GetEnvironmentVariable("TK_H2_PARTS") ?? "fight,teleport").Split(',');
            string entry = Environment.GetEnvironmentVariable("TK_H2_ENTRY") ?? "world";
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_H2_SECONDS"), out var sec) ? sec : 40f;
            var keepWeapons = WeaponMode.Current;
            var keepAi = EnemyAiMode.Current;
            BladeArts.Set(Environment.GetEnvironmentVariable("TK_H2_WEAPONS") != "vanilla");
            EnemyAiMode.Current = Environment.GetEnvironmentVariable("TK_H2_AI") == "new" ? EnemyAiKind.New : EnemyAiKind.Vanilla;
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            var inv = mod.Inventory;
            int stage = Shop.DevStage;
            const string arena = "Deepnest_East_Hornet";
            bool alive = true;
            IEnumerator Alive()
            {
                while (alive)
                {
                    mod.Stats.FullHeal();
                    if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
                    yield return null;
                }
            }
            mod.StartCoroutine(Alive());

            HealthManager Hornet() => Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name.StartsWith(arena) && UI.EnemyHover.RecordOf(h) == "Hornet");
            PlayMakerFSM Fsm(GameObject go, string name) => go == null ? null : go.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == name);
            GameObject Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t != null && t.name == name && t.gameObject.scene.name != null && t.gameObject.scene.name.StartsWith(arena)).Select(t => t.gameObject).FirstOrDefault();

            // Into her fight: by the world's own door (her refight: she drops in when the Knight walks into her range), or by her seal.
            IEnumerator Enter()
            {
                PlayerData.instance.SetBool("hornetOutskirtsDefeated", false);
                if (entry == "seal")
                {
                    yield return SealTravel("Town", "left1");
                    var seal = BossSummons.All.FirstOrDefault(x => x.Key == "Hornet@4");
                    if (seal == null) { Plugin.Log.LogInfo("[AutoTest] 黄蜂二 没有战令"); yield break; }
                    Bestiary.Kills["Hornet@4"] = Mathf.Max(1, Bestiary.KillsOf("Hornet@4"));
                    Shop.DevStage = seal.Stage;
                    inv.Slots[0] = ItemStack.Of(seal.Type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    for (float t = Time.realtimeSinceStartup + 30f; !BossArena.Arrived("Hornet@4") && Time.realtimeSinceStartup < t;) yield return null;
                    inv.Slots[0] = default;
                }
                else
                {
                    PlayerData.instance.SetBool("encounteredHornet", true);
                    if (BossArena.DevWorldArena("Hornet@4", out var gate, out var needs))
                        foreach (var (flag, v) in needs) PlayerData.instance.SetBool(flag, v);
                    yield return GoTo(arena, "left1");
                }
                for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                yield return new WaitForSeconds(1f);
                // Her refight waits for the Knight in her range ("Refight Range"): stood in it.
                var hero = HeroController.instance;
                var range = Find("Refight Range");
                var rc = range != null ? range.GetComponent<Collider2D>() : null;
                if (rc != null && entry != "seal")
                {
                    var c = rc.bounds.center;
                    hero.transform.position = new Vector3(c.x, c.y, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                }
                for (float t = Time.time + 10f; Time.time < t;)
                {
                    var h = Hornet();
                    var control = h != null ? Fsm(h.gameObject, "Control") : null;
                    if (control != null && control.ActiveStateName != "Refight Ready" && control.ActiveStateName != "Inert" && control.ActiveStateName != "Dormant") break;
                    yield return null;
                }
                var hh = Hornet();
                var bs = Find("Battle Scene");
                Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 进场（{entry}）：{(hh != null ? $"{hh.name} {Fsm(hh.gameObject, "Control")?.ActiveStateName} 在 {(Vector2)hh.transform.position:F1}" : "没找到她")}，战斗场景 {Fsm(bs, "FSM")?.ActiveStateName}，主角 {(Vector2)hero.transform.position:F1}");
            }

            // The battle scene, its gates and its walls (the battle scene's children), now.
            string Room()
            {
                var sb = new StringBuilder();
                var bs = Find("Battle Scene");
                sb.Append($"战斗场景 {Fsm(bs, "FSM")?.ActiveStateName ?? "无"}");
                if (bs != null)
                    foreach (Transform ch in bs.transform)
                    {
                        var c = ch.GetComponent<Collider2D>();
                        sb.Append($"；子物体 {ch.name} 开着 {ch.gameObject.activeInHierarchy}{(c != null ? $" 碰撞 {c.enabled && ch.gameObject.activeInHierarchy} {c.bounds.min:F1}~{c.bounds.max:F1}" : "")}");
                    }
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.FsmName != "BG Control" || !f.gameObject.scene.name.StartsWith(arena)) continue;
                    var c = f.GetComponent<Collider2D>();
                    sb.Append($"；战斗门 {f.name} {f.ActiveStateName} 碰撞 {(c != null && c.enabled && f.gameObject.activeInHierarchy)} 在 {(Vector2)f.transform.position:F1}");
                }
                return sb.ToString();
            }

            // Out by each side gate: stood 5 inside it, what the Knight's box meets on the way (and the world's edge); then walked
            // out of the nearest one. True when he left the room.
            var outMethod = HarmonyLib.AccessTools.Method(typeof(WorldBorder), "Out");
            IEnumerator WayOut(string label)
            {
                var h = HeroController.instance;
                var hcol = h.GetComponent<BoxCollider2D>();
                int heroMask = Physics2D.GetLayerCollisionMask(h.gameObject.layer);
                var sb = new StringBuilder();
                TransitionPoint best = null;
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (tp == null || !tp.gameObject.scene.name.StartsWith(arena) || !(tp.name.StartsWith("left") || tp.name.StartsWith("right"))) continue;
                    var tc = tp.GetComponent<Collider2D>();
                    bool live = tp.gameObject.activeInHierarchy && tc != null && tc.enabled;
                    Vector2 g = tc != null && tc.enabled ? (Vector2)tc.bounds.center : (Vector2)tp.transform.position;
                    float dirX = tp.name.StartsWith("left") ? -1f : 1f;
                    var from = new Vector2(g.x - dirX * 5f, g.y);
                    var walls = new List<string>();
                    foreach (var hit in Physics2D.BoxCastAll(from, hcol.bounds.size * 0.9f, 0f, new Vector2(dirX, 0f), 5f, heroMask))
                    {
                        if (hit.collider == null || hit.collider.isTrigger || hit.collider == hcol || hit.collider.usedByEffector || hit.collider.GetComponentInParent<HeroController>() != null) continue;
                        walls.Add($"{PathOf(hit.collider.transform)}（层{hit.collider.gameObject.layer} {hit.point:F1}{FsmStates(hit.collider.gameObject, false)}）");
                    }
                    var edge = new List<string>();
                    for (float t = 0f; t <= 5f; t += 0.5f)
                    {
                        var p = from + new Vector2(dirX * t, 0f);
                        if ((bool)outMethod.Invoke(null, new object[] { p })) edge.Add($"{p.x:F1}");
                    }
                    sb.Append($"\n  门 {tp.name} 在 {g:F1} 启用 {live}：挡着的 {(walls.Count == 0 ? "无" : string.Join("、", walls))}；世界边缘 {(edge.Count == 0 ? "无" : string.Join(",", edge))}");
                    if (live && (best == null || tp.name == "left2")) best = tp;
                }
                Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 {label} 出口：{Room()}{sb}");
                bool left = false;
                if (best != null)
                {
                    var ntc = best.GetComponent<Collider2D>();
                    float dir = best.name.StartsWith("left") ? -1f : 1f;
                    Vector2 gc = ntc.bounds.center;
                    float offset = h.transform.position.y - hcol.bounds.min.y;
                    h.transform.position = new Vector3(gc.x - dir * 4f, ntc.bounds.min.y + offset + 0.3f, h.transform.position.z);
                    h.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.8f);
                    string scene = GameManager.instance.sceneName;
                    int stops0 = WorldBorder.DevStops;
                    float minDist = float.MaxValue;
                    Player.TerraMovement.DevMove = (int)dir;
                    for (float walk = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < walk && GameManager.instance.sceneName == scene && !GameManager.instance.IsInSceneTransition;)
                    {
                        minDist = Mathf.Min(minDist, Mathf.Abs(h.transform.position.x - gc.x));
                        yield return null;
                    }
                    Player.TerraMovement.DevMove = 0;
                    left = GameManager.instance.sceneName != scene || GameManager.instance.IsInSceneTransition;
                    Check(left, $"{label} 从 {best.name} 走得出去（离门最近 {minDist:F1}，现在 {(Vector2)h.transform.position:F1}，世界边缘挡了 {WorldBorder.DevStops - stops0} 次 {WorldBorder.DevLast}）");
                    for (float t = Time.realtimeSinceStartup + 10f; Time.realtimeSinceStartup < t && GameManager.instance.IsInSceneTransition;) yield return null;
                }
                else Check(false, $"{label} 没有能走的侧门");
            }

            // The shield thrown at her from where the Knight stands, for `span` seconds or till `stop`; each frame where she is.
            int shieldHits0 = 0;
            IEnumerator Throw(Func<Vector2?> aim, float span, Func<bool> stop)
            {
                inv.Slots[0] = ItemStack.Of(ItemType.BouncingShield);
                inv.Selected = 0;
                inv.Override = -1;
                mod.DevHold = true;
                for (float end = Time.time + span; Time.time < end && !stop();)
                {
                    var at = aim();
                    if (at != null)
                    {
                        mod.DevAim = at.Value;
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at.Value);
                    }
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
            }

            // The room as cells (1 unit a character, '#' solid, '.' open, 'W' a battle-scene wall collider on, 'w' off, 'G' a gate
            // collider on), x -2..70, y 0..100, top row first.
            string Map()
            {
                var walls = new List<(Bounds B, bool On)>();
                var bs = Find("Battle Scene");
                if (bs != null)
                    foreach (var c in bs.GetComponentsInChildren<Collider2D>(true))
                        if (!c.isTrigger) walls.Add((c.bounds, c.enabled && c.gameObject.activeInHierarchy));
                var gateCols = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f => f.FsmName == "BG Control" && f.gameObject.scene.name.StartsWith(arena))
                    .Select(f => f.GetComponent<Collider2D>()).Where(c => c != null && c.enabled).Select(c => c.bounds).ToList();
                var sb = new StringBuilder();
                for (int y = 100; y >= 0; y--)
                {
                    sb.Append($"\n{y,3} ");
                    for (int x = -2; x <= 70; x++)
                    {
                        var p = new Vector3(x + 0.5f, y + 0.5f, 0f);
                        char ch = TerraWorld.IsSolidAt(p) ? '#' : '.';
                        foreach (var w in walls) if (w.B.Contains(new Vector3(p.x, p.y, w.B.center.z))) ch = w.On ? 'W' : (ch == '#' ? '#' : 'w');
                        foreach (var g in gateCols) if (g.Contains(new Vector3(p.x, p.y, g.center.z))) ch = 'G';
                        sb.Append(ch);
                    }
                }
                return sb.ToString();
            }

            try
            {
                if (parts.Contains("map"))
                {
                    foreach (bool beaten in new[] { false, true })
                    {
                        PlayerData.instance.SetBool("hornetOutskirtsDefeated", beaten);
                        PlayerData.instance.SetBool("encounteredHornet", true);
                        yield return GoTo(arena, "left1");
                        for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                        yield return new WaitForSeconds(1.5f);
                        var bs = Find("Battle Scene");
                        var kids = new StringBuilder();
                        if (bs != null)
                            foreach (var c in bs.GetComponentsInChildren<Collider2D>(true))
                                kids.Append($"\n  {PathOf(c.transform)} {c.GetType().Name} 触发 {c.isTrigger} 开着 {c.enabled && c.gameObject.activeInHierarchy}（自身 {c.gameObject.activeSelf}） {c.bounds.min:F1}~{c.bounds.max:F1}");
                        Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 地图（存档里{(beaten ? "已打败" : "没打败")}）：{Room()}{kids}{Map()}");
                    }
                    yield return SealTravel("Town", "left1");
                }

                if (parts.Contains("fight"))
                {
                    yield return Enter();
                    var hm = Hornet();
                    if (hm == null) { Check(false, "进场后没找到大黄蜂"); }
                    else
                    {
                        var hero = HeroController.instance;
                        Vector2 home = hero.transform.position;
                        var control = Fsm(hm.gameObject, "Control");
                        var body = hm.GetComponent<Rigidbody2D>();
                        int max = hm.hp;
                        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, top = 0f;
                        var fast = new List<string>();
                        Vector2 lastPos = hm.transform.position;
                        float started = Time.time;
                        shieldHits0 = TerraProjectiles.DevHit(ProjKind.BouncingShieldProj);
                        bool dead = false;
                        Vector2 deathAt = default;
                        var fightLog = mod.StartCoroutine(Throw(() => hm != null && !hm.isDead ? TerraProjectiles.EnemyCentre(hm) : (Vector2?)null, seconds + 20f, () => hm == null || hm.isDead));
                        for (bool first = true; Time.time - started < seconds + 20f; first = false)
                        {
                            if (!first) yield return null;
                            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) { dead = true; break; }
                            float t = Time.time - started;
                            int want = Mathf.Max(1, Mathf.RoundToInt(max * Mathf.Lerp(1f, 0.02f, t / seconds)));
                            if (hm.hp > want) hm.hp = want;
                            Vector2 p = hm.transform.position;
                            deathAt = p;
                            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                            float v = body != null ? body.linearVelocity.magnitude : 0f;
                            float jump = Vector2.Distance(p, lastPos);
                            lastPos = p;
                            top = Mathf.Max(top, v);
                            if ((v > 28f || jump > 1.2f) && fast.Count < 25)
                                fast.Add($"{t:F2}s {control?.ActiveStateName} 速度 {(body != null ? body.linearVelocity : Vector2.zero):F1} 一帧 {jump:F2} 在 {p:F1}");
                        }
                        int hits = TerraProjectiles.DevHit(ProjKind.BouncingShieldProj) - shieldHits0;
                        Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 战斗 {Time.time - started:F1} 秒，联盾打中 {hits} 次，她 x {minX:F1}~{maxX:F1} y {minY:F1}~{maxY:F1}，最快 {top:F1}，倒下 {dead} 在 {deathAt:F1}；快速/跳动帧 {fast.Count}：{string.Join("；", fast)}");
                        Check(dead, "打得倒");
                        Check(minX > 12f && maxX < 42f, $"战斗中没被打出场地（x {minX:F1}~{maxX:F1}）");

                        // Her corpse: launched, landed, its talk; the shield still thrown at whatever it finds.
                        GameObject corpse = null;
                        for (float t = Time.time + 3f; corpse == null && Time.time < t;)
                        {
                            corpse = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f => f.FsmName == "Control" && f.gameObject.name.StartsWith("Corpse Hornet Outskirts")).Select(f => f.gameObject).FirstOrDefault();
                            if (corpse == null) yield return null;
                        }
                        if (corpse == null) Check(false, "没有尸体（Corpse Hornet Outskirts）");
                        else
                        {
                            var cc = Fsm(corpse, "Control");
                            var cb = corpse.GetComponent<Rigidbody2D>();
                            Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 尸体 {PathOf(corpse.transform)}，血量组件 {corpse.GetComponent<HealthManager>() != null}，刚体 {cb?.bodyType}");
                            var trail = new List<string>();
                            string lastState = null;
                            int hitsBefore = TerraProjectiles.DevHit(ProjKind.BouncingShieldProj);
                            var keepThrowing = mod.StartCoroutine(Throw(() => corpse != null ? (Vector2)corpse.transform.position : (Vector2?)null, 12f, () => cc == null || cc.ActiveStateName == "Can Talk"));
                            float cMinX = float.MaxValue, cMaxX = float.MinValue, cMinY = float.MaxValue, cMaxY = float.MinValue;
                            for (float t = Time.time + 12f; Time.time < t && corpse != null;)
                            {
                                yield return null;
                                if (corpse == null) break;
                                Vector2 p = corpse.transform.position;
                                cMinX = Mathf.Min(cMinX, p.x); cMaxX = Mathf.Max(cMaxX, p.x); cMinY = Mathf.Min(cMinY, p.y); cMaxY = Mathf.Max(cMaxY, p.y);
                                if (cc.ActiveStateName != lastState)
                                {
                                    lastState = cc.ActiveStateName;
                                    trail.Add($"{lastState}@{p:F1}{(cb != null ? $" v{cb.linearVelocity:F1}" : "")}");
                                }
                                if (lastState == "Can Talk") break;
                            }
                            Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 尸体经过 {string.Join(" → ", trail)}；x {cMinX:F1}~{cMaxX:F1} y {cMinY:F1}~{cMaxY:F1}；尸体期间联盾又打中 {TerraProjectiles.DevHit(ProjKind.BouncingShieldProj) - hitsBefore} 次");
                            Check(lastState == "Can Talk", $"尸体落地等对话（停在 {lastState}）");
                            // The talk: walked up to her (the NPC by her) and its end sent as her conversation's end sends it.
                            if (lastState == "Can Talk")
                            {
                                var npc = corpse.transform.Find("NPC");
                                Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 对话对象 {(npc != null ? $"{npc.name} 开着 {npc.gameObject.activeInHierarchy} 在 {(Vector2)npc.position:F1}" : "无")}");
                                hero.transform.position = new Vector3(corpse.transform.position.x - 2f, hero.transform.position.y, hero.transform.position.z);
                                yield return new WaitForSeconds(0.5f);
                                cc.SendEvent("HORNET END CONVO");
                            }
                            var leave = new List<string>();
                            lastState = cc != null ? cc.ActiveStateName : null;
                            for (float t = Time.time + 6f; Time.time < t && cc != null;)
                            {
                                if (cc.ActiveStateName != lastState) { lastState = cc.ActiveStateName; leave.Add($"{lastState}@{(Vector2)corpse.transform.position:F1}"); }
                                yield return null;
                            }
                            Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 离场 {string.Join(" → ", leave)}");
                        }
                        yield return new WaitForSeconds(2f);
                        var bs = Find("Battle Scene");
                        string bsState = Fsm(bs, "FSM")?.ActiveStateName;
                        Check(bsState == "Open", $"她离场后战斗场景打开（{bsState}）");
                        yield return WayOut("打完");
                    }
                    yield return SealTravel("Town", "left1");
                }

                if (parts.Contains("teleport"))
                {
                    yield return Enter();
                    var hm = Hornet();
                    if (hm == null) Check(false, "传送：进场后没找到大黄蜂");
                    else
                    {
                        yield return Throw(() => hm != null && !hm.isDead ? TerraProjectiles.EnemyCentre(hm) : (Vector2?)null, 8f, () => hm == null || hm.isDead);
                        Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 传送前：{Room()}");
                        // Away (a pylon's or a mirror's way: a scene change), then back by the door and out.
                        yield return SealTravel("Town", "left1");
                        yield return new WaitForSeconds(1f);
                        yield return GoTo(arena, "left1");
                        for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                        yield return new WaitForSeconds(2f);
                        yield return WayOut("传送走再回来");
                    }
                    yield return SealTravel("Town", "left1");
                }
            }
            finally
            {
                alive = false;
                mod.DevHold = null;
                mod.DevAim = null;
                Player.TerraMovement.DevMove = 0;
                inv.Slots[0] = default;
                Shop.DevStage = stage;
                WeaponMode.Current = keepWeapons;
                EnemyAiMode.Current = keepAi;
                Player.TerraStats.DevImmortal = false;
                Plugin.Log.LogInfo($"[AutoTest] 黄蜂二 合计 {ok}/{all}");
            }
        }
    }
}
