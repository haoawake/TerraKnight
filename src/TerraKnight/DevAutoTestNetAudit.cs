using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suites "netaudit_host" and "netaudit_client": a LAN game between this session's two test copies (port 7795). The
    /// host goes to the False Knight's arena first (its game runs him), the client follows; the host fights him to the
    /// end as fkhead does. The client meanwhile counts what it draws: every head shown (the Head's sprite out of its
    /// parking spot up in the roof, and any renderer named for a head), the False Knight's parts as puppets or not,
    /// the host's character; screenshots when a head comes out and whenever more than one is drawn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NetAuditPort = 7795;
        const string FkArena = "Crossroads_10";

        IEnumerator NetAuditBoot()
        {
            if (suite == "netaudit_host")
            {
                bool ok = NetSession.Host(NetAuditPort, "", 8, out string error);
                NetCheck(ok, $"开服 {NetAuditPort} {error}");
                for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                NetCheck(NetSession.Active, "主机加入自己的服务器");
                yield break;
            }
            float until = Time.realtimeSinceStartup + 120f;
            while (!NetSession.Active && Time.realtimeSinceStartup < until)
            {
                NetSession.Join("127.0.0.1", NetAuditPort);
                for (float t = 0f; t < 6f && NetSession.State != NetSession.Phase.Off && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                if (!NetSession.Active) yield return new WaitForSecondsRealtime(3f);
            }
            NetCheck(NetSession.Active, $"客户端加入主机（槽位 {NetSession.MySlot}）");
        }

        IEnumerator NetKeepAlive(System.Func<bool> until)
        {
            while (!until())
            {
                mod.Stats.FullHeal();
                if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
                yield return null;
            }
        }

        static HealthManager FkBody()
        {
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm.name == "False Knight New" && hm.gameObject.activeInHierarchy) return hm;
            return null;
        }

        static HealthManager FkHead(HealthManager fk)
        {
            if (fk == null) return null;
            foreach (var hm in fk.GetComponentsInChildren<HealthManager>(true)) if (hm.name == "Head") return hm;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hm.name == "Head" && hm.gameObject.scene == fk.gameObject.scene) return hm;
            return null;
        }

        IEnumerator NetAuditHost(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            try
            {
                RemotePlayer other = null;
                for (float t = 0f; t < 180f && !(other != null && other.Visible); t += Time.unscaledDeltaTime) { other = Other(); yield return null; }
                NetCheck(other != null && other.Visible, $"看到客户端的人物：{(other != null ? other.Name : "没有")}");
                mod.DevNoFallDamage = true;
                yield return SealTravel(FkArena, "left1");
                for (float t = 0f; t < 10f && !(NetSession.Owners.TryGetValue(FkArena, out int o) && o == NetSession.MySlot); t += Time.unscaledDeltaTime) yield return null;
                NetCheck(NetSession.Owners.TryGetValue(FkArena, out int owner) && owner == NetSession.MySlot, $"假骑士的房间归主机（{owner}）");
                NetChat.Say("host: at fk");
                yield return WaitChat("client: at fk", 90f);
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                hero.transform.position = Clear(hero, new Vector2(38f, 28f));
                HealthManager fk = null;
                for (float wait = Time.time + 25f; Time.time < wait && fk == null;) { fk = FkBody(); yield return null; }
                NetCheck(fk != null, "主机这边假骑士出场");
                if (fk == null) yield break;
                var head = FkHead(fk);
                var control = new List<PlayMakerFSM>(fk.GetComponentsInChildren<PlayMakerFSM>(true)).Find(f => f.FsmName == "FalseyControl");
                var fkc = fk.GetComponent<Collider2D>();
                var pd = PlayerData.instance;
                string last = null;
                bool hostLooked = false;
                float t0 = Time.time, nextHit = 0f;
                while (Time.time - t0 < 200f && !pd.falseKnightDefeated)
                {
                    string cs = control != null ? control.ActiveStateName : "?";
                    var hc = head != null ? head.GetComponent<Collider2D>() : null;
                    Vector2 hp = hc != null ? (Vector2)hc.bounds.center : head != null ? (Vector2)head.transform.position : Vector2.zero;
                    bool headOut = head != null && head.gameObject.activeInHierarchy && hp.y < 150f;
                    if (cs != last)
                    {
                        last = cs;
                        Plugin.Log.LogInfo($"[AutoTest] 联机审计 主机 假骑士 {Time.time - t0:F1}s {cs} 身体 {(fkc != null ? (Vector2)fkc.bounds.center : Vector2.zero):F1} 头 {hp:F1} 露头 {headOut}{(cs == "Opened" && !hostLooked ? "；身子：" + FkLooks(fk) : "")}");
                        if (cs == "Opened") hostLooked = true;
                    }
                    bool raging = cs.StartsWith("Rage") || cs == "Particle Pause" || cs == "Anim End" || cs == "R Attack Antic";
                    if (Time.time >= nextHit && GameHooks.PlayerHasControl() && !raging)
                    {
                        nextHit = Time.time + 0.35f;
                        var target = headOut && hc != null && hc.enabled ? hc : fkc;
                        if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                        {
                            var tb = target.bounds;
                            hero.transform.position = Clear(hero, new Vector2(tb.min.x - 1.4f, tb.min.y + 0.3f));
                            mod.Melee.HitWith(target, headOut ? 120 : 60, 12f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center);
                        }
                    }
                    yield return null;
                }
                NetCheck(pd.falseKnightDefeated, $"主机打完假骑士（{Time.time - t0:F0}s），{Net.Enemies.DevReport()}");
                yield return new WaitForSeconds(4f);
                for (float t = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < t && GatesShut(out _) > 0;) yield return null;
                NetCheck(GatesShut(out string gates0) == 0, $"第一场打完后主机这边战斗门都开了：{gates0}");
                Shot(FkArena, "netaudit_host_fk_end");
                yield return null;
                NetChat.Say("host: fk done");
                yield return WaitChat("client: in town", 90f);

                // The rematch by the seal: the client is taken there too; the host fights, both look at the gates after.
                var seal = BossSummons.All[0];
                Bestiary.Kills[seal.Key] = Mathf.Max(1, KillsOf(seal.Key));
                Shop.DevStage = seal.Stage;
                mod.Inventory.Slots[0] = ItemStack.Of(seal.Type, 2);
                mod.Inventory.Selected = 0;
                mod.Inventory.Override = -1;
                yield return SealTravel("Town", "left1");
                yield return new WaitForSeconds(2f);
                string why = BossSummons.Refusal(seal);
                NetCheck(why == null, $"主机能用 {seal.Name}：{why ?? "能"}");
                int kills0 = KillsOf(seal.Key.Split('@')[0]);
                mod.DevUseSelected(HeroController.instance.transform.position);
                for (float there = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(seal.Key) && Time.realtimeSinceStartup < there;) yield return null;
                NetCheck(BossArena.Arrived(seal.Key), "主机用召唤物进了假骑士竞技场");
                yield return new WaitForSeconds(3f);
                Plugin.Log.LogInfo($"[AutoTest] 联机审计 主机 召唤后 房间归 {(NetSession.Owners.TryGetValue(FkArena, out int o2) ? o2 : -1)}（自己 {NetSession.MySlot}），{Net.Enemies.DevReport()}");
                hero = HeroController.instance;
                fk = FkBody();
                head = FkHead(fk);
                control = fk != null ? new List<PlayMakerFSM>(fk.GetComponentsInChildren<PlayMakerFSM>(true)).Find(f => f.FsmName == "FalseyControl") : null;
                fkc = fk != null ? fk.GetComponent<Collider2D>() : null;
                bool mine = NetSession.Owners.TryGetValue(FkArena, out int o3) && o3 == NetSession.MySlot;
                t0 = Time.time;
                last = null;
                while (Time.time - t0 < 200f && KillsOf(seal.Key.Split('@')[0]) <= kills0 && fk != null)
                {
                    string cs = control != null ? control.ActiveStateName : "?";
                    var hc = head != null ? head.GetComponent<Collider2D>() : null;
                    Vector2 hp = hc != null ? (Vector2)hc.bounds.center : head != null ? (Vector2)head.transform.position : Vector2.zero;
                    bool headOut = head != null && head.gameObject.activeInHierarchy && hp.y < 150f;
                    if (cs != last) { last = cs; Plugin.Log.LogInfo($"[AutoTest] 联机审计 主机 重打 {Time.time - t0:F1}s {cs} 头 {hp:F1} 露头 {headOut} 傀儡 {Net.Enemies.IsPuppet(fk)}"); }
                    bool raging = cs.StartsWith("Rage") || cs == "Particle Pause" || cs == "Anim End" || cs == "R Attack Antic";
                    if (Time.time >= nextHit && GameHooks.PlayerHasControl() && (!raging || Net.Enemies.IsPuppet(fk)))
                    {
                        nextHit = Time.time + 0.35f;
                        var target = headOut && hc != null && hc.enabled ? hc : fkc;
                        if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                        {
                            var tb = target.bounds;
                            hero.transform.position = Clear(hero, new Vector2(tb.min.x - 1.4f, tb.min.y + 0.3f));
                            mod.Melee.HitWith(target, headOut ? 120 : 60, 12f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center);
                        }
                    }
                    yield return null;
                }
                NetCheck(KillsOf(seal.Key.Split('@')[0]) > kills0, $"联机用召唤物重打假骑士打完（{Time.time - t0:F0}s，这边负责 {mine}），{Net.Enemies.DevReport()}");
                yield return new WaitForSeconds(12f);
                NetCheck(GatesShut(out string gates) == 0, $"主机这边打完后战斗门都开了：{gates}");
                NetChat.Say("host: rematch done");
                yield return WaitChat("client: bye", 60f);
                yield return new WaitForSeconds(1f);
            }
            finally { done = true; }
        }

        /// <summary>The False Knight's room's battle gates still solid.</summary>
        static int GatesShut(out string detail)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "BG Control" || !f.gameObject.scene.name.StartsWith(FkArena)) continue;
                var c = f.GetComponent<Collider2D>();
                bool shut = c != null && c.enabled;
                if (shut) n++;
                sb.Append($" [{f.name} {f.ActiveStateName}{(shut ? " 挡着" : "")}]");
            }
            detail = sb.ToString();
            return n;
        }

        /// <summary>How the False Knight's armour is drawn here: each renderer under him, on or off, seen, its clip and frame.</summary>
        static string FkLooks(HealthManager fk)
        {
            if (fk == null) return "无";
            var sb = new StringBuilder();
            var anim = fk.GetComponent<tk2dSpriteAnimator>();
            sb.Append($"开{(fk.gameObject.activeInHierarchy ? 1 : 0)} 动画 {anim?.CurrentClip?.name}#{anim?.CurrentFrame} 在 {(Vector2)fk.transform.position:F1} 缩放 {(Vector2)fk.transform.localScale:F2}");
            foreach (var r in fk.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.parent != null && r.transform.parent != fk.transform && r.transform != fk.transform) continue;
                var sp = r.GetComponent<tk2dSprite>();
                sb.Append($" [{r.name} 画{(r.enabled ? 1 : 0)} 开{(r.gameObject.activeInHierarchy ? 1 : 0)} 看得见{(r.isVisible ? 1 : 0)}{(sp != null ? $" 色a{sp.color.a:F2} 帧{sp.CurrentSprite?.name}" : "")}]");
            }
            return sb.ToString();
        }

        /// <summary>The False Knight's heads this game draws: his Head out of its parking spot, and every visible renderer named for a head.</summary>
        static int FkHeadsDrawn(out string detail)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm.name != "Head" || !hm.gameObject.scene.name.StartsWith(FkArena)) continue;
                var r = hm.GetComponent<Renderer>();
                bool drawn = hm.gameObject.activeInHierarchy && r != null && r.enabled && r.isVisible && hm.transform.position.y < 150f;
                if (drawn) n++;
                sb.Append($" [{PathOf(hm.transform)} 开{(hm.gameObject.activeInHierarchy ? 1 : 0)} 画{(r != null && r.enabled ? 1 : 0)} 看得见{(r != null && r.isVisible ? 1 : 0)} {(Vector2)hm.transform.position:F1} 傀儡{(Net.Enemies.IsPuppet(hm) ? 1 : 0)} 血{hm.hp}{(hm.isDead ? " 死" : "")}]");
            }
            detail = sb.ToString();
            return n;
        }

        IEnumerator NetAuditClient(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            try
            {
                mod.DevNoFallDamage = true;
                yield return WaitChat("host: at fk", 200f);
                yield return SealTravel(FkArena, "left1");
                hero = HeroController.instance;
                yield return new WaitForSeconds(1.5f);
                bool remote = NetSession.Owners.TryGetValue(FkArena, out int owner) && owner != NetSession.MySlot;
                NetCheck(remote, $"假骑士的房间在客户端这边归主机（{owner}，自己 {NetSession.MySlot}）");
                // Inside the arena, on its left: in the fight, out of the host's way.
                hero.transform.position = Clear(hero, new Vector2(24f, 28f));
                NetChat.Say("client: at fk");
                int most = 0, outs = 0, multiShots = 0, samples = 0;
                bool wasOut = false;
                string lastDetail = null;
                float next = 0f, nextShot = 0f, t0 = Time.time;
                while (!NetChat.DevHas("host: fk done") && Time.time - t0 < 240f)
                {
                    if (Time.time >= next)
                    {
                        next = Time.time + 0.2f;
                        samples++;
                        int n = FkHeadsDrawn(out string detail);
                        if (n > most) most = n;
                        bool isOut = n > 0;
                        if (isOut && !wasOut)
                        {
                            outs++;
                            Plugin.Log.LogInfo($"[AutoTest] 联机审计 客户端 第 {outs} 次露头 {Time.time - t0:F1}s：头 {n} 个{detail}；名字带头的画面：{HeadsShown()}；身子：{FkLooks(FkBody())}");
                            if (outs <= 3) { Shot(FkArena, $"netaudit_client_head{outs}"); yield return null; }
                        }
                        if (n > 1 && Time.time >= nextShot && multiShots < 4)
                        {
                            multiShots++;
                            nextShot = Time.time + 3f;
                            Plugin.Log.LogInfo($"[AutoTest] 联机审计 客户端 画了 {n} 个头 {Time.time - t0:F1}s：{detail}；名字带头的画面：{HeadsShown()}");
                            Shot(FkArena, $"netaudit_client_heads{multiShots}");
                            yield return null;
                        }
                        if (detail != lastDetail && samples % 10 == 0)
                        {
                            lastDetail = detail;
                            var body = FkBody();
                            var host = Other();
                            Plugin.Log.LogInfo($"[AutoTest] 联机审计 客户端 {Time.time - t0:F1}s 身体 {(body != null ? ((Vector2)body.transform.position).ToString("F1") + " 傀儡 " + Net.Enemies.IsPuppet(body) + " 血 " + body.hp : "无")}；头{detail}；主机 {(host != null && host.Visible ? host.Feet.ToString("F1") : "看不见")}；{Net.Enemies.DevReport()}");
                        }
                        wasOut = isOut;
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(3f);
                int after = FkHeadsDrawn(out string end);
                Plugin.Log.LogInfo($"[AutoTest] 联机审计 客户端 打完：头{end}；名字带头的画面：{HeadsShown()}；{Net.Enemies.DevReport()}");
                Shot(FkArena, "netaudit_client_fk_end");
                yield return null;
                NetCheck(outs > 0, $"客户端看到假骑士露头 {outs} 次");
                NetCheck(most <= 1, $"客户端同一时刻最多画 {most} 个假骑士的头");
                // His parts die at the end of his death scene, after the record of his defeat.
                for (float t = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < t && Net.Enemies.DevDeathsReceived == 0 && Net.Enemies.DevBeatenReceived == 0;) yield return null;
                NetCheck(Net.Enemies.DevDeathsReceived > 0 || Net.Enemies.DevBeatenReceived > 0, $"主机打倒的假骑士在客户端也算打倒了（他自己的死亡流程：首领打倒的消息）：{Net.Enemies.DevReport()}");
                NetCheck(PlayerData.instance.falseKnightDefeated, "客户端自己的存档也记下打倒了假骑士");
                yield return new WaitForSeconds(6f);
                NetCheck(GatesShut(out string gates1) == 0, $"第一场打完后客户端这边战斗门都开了：{gates1}");
                Shot(FkArena, "netaudit_client_fk_gates");
                yield return null;
                yield return SealTravel("Town", "left1");
                NetChat.Say("client: in town");

                // The host's seal takes this player along.
                int followed0 = BossSummons.DevFollowed;
                for (float t = 0f; t < 90f && BossSummons.DevFollowed == followed0; t += Time.unscaledDeltaTime) yield return null;
                NetCheck(BossSummons.DevFollowed > followed0, "主机用召唤物时客户端被一起带走");
                for (float t = Time.realtimeSinceStartup + 30f; Time.realtimeSinceStartup < t && !BossArena.Arrived(BossSummons.All[0].Key);) yield return null;
                yield return new WaitForSeconds(3f);
                Plugin.Log.LogInfo($"[AutoTest] 联机审计 客户端 召唤后 在 {GameManager.instance.sceneName}，房间归 {(NetSession.Owners.TryGetValue(FkArena, out int o2) ? o2 : -1)}（自己 {NetSession.MySlot}），{Net.Enemies.DevReport()}");
                most = 0;
                int bodyHidden = 0, looks = 0;
                t0 = Time.time;
                next = 0f;
                while (!NetChat.DevHas("host: rematch done") && Time.time - t0 < 240f)
                {
                    if (Time.time >= next)
                    {
                        next = Time.time + 0.25f;
                        int n = FkHeadsDrawn(out string detail);
                        if (n > most) most = n;
                        var body = FkBody();
                        var br = body != null ? body.GetComponent<Renderer>() : null;
                        looks++;
                        // The armour out in the fight but not drawn while its head isn't out either: half of him gone.
                        if (body != null && !body.isDead && n == 0 && (br == null || !br.enabled)) bodyHidden++;
                        if (looks % 20 == 0) Plugin.Log.LogInfo($"[AutoTest] 联机审计 客户端 重打 {Time.time - t0:F1}s 身体 {(body != null ? ((Vector2)body.transform.position).ToString("F1") + " 傀儡 " + Net.Enemies.IsPuppet(body) + " 画 " + (br != null && br.enabled) : "无")}；头 {n}{detail}；{Net.Enemies.DevReport()}");
                        if (n > 1 && multiShots < 6) { multiShots++; Shot(FkArena, $"netaudit_client_rematch_heads{multiShots}"); yield return null; }
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(12f);
                Shot(FkArena, "netaudit_client_rematch_end");
                yield return null;
                NetCheck(most <= 1, $"重打时客户端同一时刻最多画 {most} 个假骑士的头");
                NetCheck(bodyHidden <= 4, $"重打时假骑士的身子没画出来的次数 {bodyHidden}/{looks}");
                NetCheck(GatesShut(out string gates) == 0, $"客户端这边打完后战斗门都开了：{gates}");
                NetChat.Say("client: bye");
                yield return new WaitForSeconds(2f);
            }
            finally { done = true; }
        }
    }
}
