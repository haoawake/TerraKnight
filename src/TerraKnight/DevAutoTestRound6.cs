using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "steadygold", "dashgravity", "palacefly"): the user's 2026-10-21 sixth list. Steady Body's skill laying a
    /// gold copy over the whole player; every pairing of the dashes (shadow dash, the Shield's, Dashmaster's any-way one, a
    /// hit in the middle, the skill pressed) with Steady Body worn giving the gravity back; the White Palace's Wingmoulds
    /// (White Palace Fly) and what kills them.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Round6(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            switch (suite)
            {
                case "steadygold": yield return SteadyGoldTest(); break;
                case "dashgravity": yield return DashGravityTest(); break;
                case "palacefly": yield return PalaceFlyProbe(); break;
                case "shadechase": yield return ShadeChaseProbe(); break;
            }
            EndNailTest();
            WearOnly();
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        // ------------------------------------------------------------------ Steady Body's gold

        IEnumerator SteadyGoldTest()
        {
            yield return CharmPlace();
            var hero = HeroController.instance;
            const int Id = CharmEffects.SteadyBody;
            WearOnly(Id);
            yield return new WaitForSeconds(0.4f);
            var gold = GameObject.Find("Terra Player Gold");
            CharmCheck("没开护符技时没有金色层", gold == null || !gold.activeInHierarchy, "");
            CharmShot("plain");
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + Id);
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.5f);
            gold = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Terra Player Gold" && t.gameObject.activeInHierarchy) gold = t.gameObject;
            int shown = 0;
            float alpha = 0f;
            if (gold != null)
                foreach (var sr in gold.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.enabled && sr.sprite != null) { shown++; alpha = Mathf.Max(alpha, sr.color.a); }
            CharmCheck("开护符技后全身盖上金色剪影", gold != null && shown >= 6 && alpha > 0.4f && Player.TerraPlayerSprite.DevGoldShown >= Time.frameCount - 2,
                $"金色层 {(gold != null)}，{shown} 层，最浓 {alpha:F2}");
            CharmShot("gold");
            for (float w = Time.time + 4f; CharmEffects.DashPassActive && Time.time < w;) yield return null;
            yield return new WaitForSeconds(0.5f);
            CharmCheck("3 秒后金色层收起", gold == null || !gold.activeInHierarchy, "");
            CharmShot("after");
        }

        // ------------------------------------------------------------------ the gravity after the dashes

        IEnumerator DashGravityTest()
        {
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            pd.hasShadowDash = true;
            pd.hasDash = true;
            Wear(ItemType.ShadeCloak, ItemType.ShieldOfCthulhu);
            yield return CharmPlace();
            hero = HeroController.instance;
            rb = hero.GetComponent<Rigidbody2D>();
            // Dirtmouth's middle, by the dummy: far from its gates, so no dash or knockback takes the player out of the scene.
            yield return ToDummy(14f);
            var home = (Vector2)hero.transform.position;
            string scene = GameManager.instance.sceneName;
            float normal = Mathf.Abs(rb.gravityScale);
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 平时重力 {rb.gravityScale:F2}，原版 {hero.DEFAULT_GRAVITY:F2}");
            const int Id = CharmEffects.SteadyBody;
            float[] delays = { 0.02f, 0.08f, 0.15f, 0.25f };
            var combos = new (string Name, bool Aim, System.Func<float, IEnumerator> Run)[]
            {
                ("黑冲", false, d => Steps(d, Shadow)),
                ("黑冲接克盾冲", false, d => Steps(d, Shadow, () => Player.TerraMovement.ForceDash(1))),
                ("克盾冲接黑冲", false, d => Steps(d, () => Player.TerraMovement.ForceDash(1), Shadow)),
                ("斜冲接黑冲", true, d => Steps(d, () => Player.TerraMovement.RequestDashToward(new Vector2(1f, 0.8f)), Shadow)),
                ("黑冲接斜冲", true, d => Steps(d, Shadow, () => Player.TerraMovement.RequestDashToward(new Vector2(1f, -0.6f)))),
                ("黑冲中挨打", false, d => Steps(d, Shadow, () => HitFrom(1, 5))),
                ("黑冲中按护符技", false, d => Steps(d, Shadow, Skill)),
                ("黑冲接黑冲", false, d => Steps(d, Shadow, Shadow, Shadow)),
                ("空中黑冲", false, d => Air(d)),
            };
            void Shadow() { AbilityAccessories.DevReset(); AbilityAccessories.DevShadowKey = true; }
            void Skill() { Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + Id); CharmSkills.DevPress = true; }
            IEnumerator Steps(float d, params System.Action[] acts)
            {
                for (int i = 0; i < acts.Length; i++)
                {
                    acts[i]();
                    float until = Time.time + d;
                    while (Time.time < until) yield return null;
                }
            }
            IEnumerator Air(float d)
            {
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.up * 3f);
                yield return new WaitForSeconds(d);
                Shadow();
                yield return new WaitForSeconds(0.1f);
                Player.TerraMovement.ForceDash(-1);
            }
            string State()
            {
                var cs = hero.cState;
                return $"HK 状态 {hero.hero_state} 着地 {cs.onGround} 冲刺 {cs.dashing}/黑冲 {cs.shadowDashing}/超冲 {cs.superDashing} 后退 {cs.recoiling} 受伤 {cs.invulnerable} " +
                    $"接受输入 {hero.acceptingInput} 交出控制 {hero.controlReqlinquished} 速度 {rb.linearVelocity.x:F2},{rb.linearVelocity.y:F2} 刚体 {rb.bodyType}/{rb.simulated} 重力 {rb.gravityScale:F2} " +
                    $"泰拉冲刺 {Player.TerraMovement.IsDashing}(延迟 {Player.TerraMovement.DashDelay:F1} 克盾 {Player.TerraMovement.EocDash:F1}) HK冲 {Player.TerraMovement.HkDashing} 黑冲标记 {AbilityAccessories.ShadowDashing} " +
                    $"下冲 {Player.TerraMovement.DownDashing} 绳 {Player.TerraMovement.OnRope} 骑 {Player.Mounts.Riding}";
            }
            // One pairing, once: whether the player falls again after it, and how he stands when he doesn't.
            IEnumerator Once((string Name, bool Aim, System.Func<float, IEnumerator> Run) combo, bool skill, float d, bool steady, List<string> bad, System.Action<bool> done)
            {
                if (steady) { if (combo.Aim) WearOnly(Id, CharmEffects.Dashmaster); else WearOnly(Id); }
                else if (combo.Aim) WearOnly(CharmEffects.Dashmaster); else WearOnly();
                if (GameManager.instance.sceneName != scene)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 出了场景（{GameManager.instance.sceneName}），回 {scene}");
                    yield return GoTo(scene, "left1");
                    yield return ToDummy(14f);
                }
                hero.transform.position = Clear(hero, home);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                if (skill) { Skill(); yield return null; yield return null; }
                Player.TerraMovement.DevMove = d < 0.1f ? 1 : -1;
                yield return combo.Run(d);
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(1.1f);
                // Up in the air a moment: it falls again.
                float g = rb.gravityScale;
                string before = State();
                hero.transform.position = Clear(hero, home + Vector2.up * 2.5f);
                rb.linearVelocity = Vector2.zero;
                float y0 = hero.transform.position.y;
                var trace = new List<string>();
                for (float t = Time.time + 0.35f; Time.time < t;)
                {
                    yield return new WaitForFixedUpdate();
                    if (trace.Count < 6) trace.Add($"{rb.linearVelocity.y:F2}");
                }
                float fell = y0 - hero.transform.position.y;
                bool ok = Mathf.Abs(Mathf.Abs(g) - normal) < 0.05f && fell > 0.3f;
                if (GameManager.instance.sceneName != scene) Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 这次出了场景：{GameManager.instance.sceneName}");
                if (!ok)
                {
                    bad.Add($"{(skill ? "技能中" : "平时")} 间隔 {d:F2}：重力 {g:F2}，0.35 秒落下 {fell:F2}");
                    Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 卡住 {combo.Name} {(steady ? "戴稳定之体" : "不戴")} {(skill ? "技能中" : "平时")} 间隔 {d:F2}\n  放上去之前 {before}\n  放上去之后 {State()}\n  竖直速度 {string.Join(" ", trace)}");
                }
                done(ok);
            }
            foreach (var combo in combos)
            {
                int ok = 0, all = 0;
                var bad = new List<string>();
                (bool Skill, float D)? firstBad = null;
                foreach (bool skill in new[] { false, true })
                    foreach (float d in delays)
                    {
                        bool good = false;
                        yield return Once(combo, skill, d, true, bad, r => good = r);
                        all++;
                        if (good) ok++;
                        else if (firstBad == null) firstBad = (skill, d);
                    }
                CharmCheck($"{combo.Name}之后重力正常", ok == all, $"{ok}/{all} {string.Join("；", bad)}");
                // The same without Steady Body: whether the charm has anything to do with it.
                if (firstBad != null && !firstBad.Value.Skill)
                {
                    var bad2 = new List<string>();
                    bool good = false;
                    yield return Once(combo, false, firstBad.Value.D, false, bad2, r => good = r);
                    Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {combo.Name} 不戴稳定之体同样的间隔 {firstBad.Value.D:F2}：{(good ? "正常" : "也卡住")}");
                }
                yield return new WaitForSeconds(0.3f);
            }
            // The old way (the gravity a shadow dash found given back, none included): Dashmaster's dash, a shadow dash
            // started in it. What the user saw: left floating.
            Player.TerraMovement.DevRawGravity = true;
            int stuck = 0, tries = 0;
            foreach (float d in delays)
            {
                var bad = new List<string>();
                bool good = false;
                yield return Once(combos[3], false, d, true, bad, r => good = r);
                tries++;
                if (!good) stuck++;
            }
            Player.TerraMovement.DevRawGravity = false;
            CharmCheck("旧的还原方式下，斜冲中接黑冲会把重力留成 0（复现）", stuck > 0, $"{stuck}/{tries} 次卡住");
            Wear();
        }

        // ------------------------------------------------------------------ the White Palace's Wingmoulds

        // ------------------------------------------------------------------ the shade coming after the player

        /// <summary>
        /// The user's F8 of 10-21: a shade in Crossroads_21 standing still beside the player. Left there, the player walks up;
        /// then struck once (EnemyAggro's ten seconds) with the player staying in its range until they are over.
        /// </summary>
        IEnumerator ShadeChaseProbe()
        {
            var pd = PlayerData.instance;
            pd.soulLimited = true;
            pd.shadeScene = "Crossroads_21";
            pd.shadePositionX = 65.1f;
            pd.shadePositionY = 4.3f;
            yield return GoTo("Crossroads_21", EntryGateOf("Crossroads_21"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            HealthManager shade = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && hm.name.StartsWith("Hollow Shade")) shade = hm;
            CharmCheck("阴影在场景里", shade != null, "");
            if (shade == null) yield break;
            var heroBox = hero.GetComponent<Collider2D>();
            string Mind()
            {
                if (shade == null) return "没了";
                var sb = new System.Text.StringBuilder($"位置 {(Vector2)shade.transform.position:F1} 血 {shade.hp}");
                foreach (var f in shade.GetComponents<PlayMakerFSM>()) sb.Append($" {f.FsmName}={f.ActiveStateName}");
                sb.Append($" 追人 {World.EnemyAggro.IsAngry(shade)}");
                foreach (var r in shade.GetComponentsInChildren<AlertRange>(true))
                {
                    bool touching = false, overlap = false;
                    foreach (var c in r.GetComponents<Collider2D>())
                    {
                        if (c.enabled && c.IsTouching(heroBox)) touching = true;
                        if (c is CircleCollider2D cc && Physics2D.OverlapCircle(r.transform.TransformPoint(cc.offset), cc.radius * Mathf.Max(r.transform.localScale.x, r.transform.localScale.y), 512) != null) overlap = true;
                    }
                    sb.Append($"；{r.gameObject.name} 看到 {r.IsHeroInRange}（贴着 {touching}，原版判断 {overlap}）");
                }
                return sb.ToString();
            }
            foreach (var f in shade.GetComponents<PlayMakerFSM>())
            {
                var sb = new System.Text.StringBuilder($"[AutoTest] 护符{charmTag} 阴影状态机 {f.FsmName}：");
                foreach (var e in f.FsmGlobalTransitions) sb.Append($"\n    全局 {e.EventName} → {e.ToState}");
                foreach (var s in f.FsmStates)
                {
                    sb.Append($"\n    {s.Name}：");
                    if (s.Name == "Init" || s.Name == "Idle" || s.Name == "Pause" || s.Name == "Friendly?" || s.Name == "Friendly Idle" || s.Name == "Special Type" || s.Name == "Startle")
                        foreach (var act in s.Actions) sb.Append(act.GetType().Name).Append(DescribeAction(act)).Append(' ');
                    foreach (var tr in s.Transitions) sb.Append($" [{tr.EventName} → {tr.ToState}]");
                }
                Plugin.Log.LogInfo(sb.ToString());
            }
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 存档：虚空之心 got {pd.gotShadeCharm} 戴 {pd.GetBool("equippedCharm_36")}，王魂 {pd.GetBool("equippedCharm_40")}");
            // Walked up to it, not striking.
            Vector2 at = shade.transform.position;
            IEnumerator Watch(string what, float seconds, System.Action<float> moved)
            {
                string last = "";
                float far = 0f;
                Vector2 start = shade != null ? (Vector2)shade.transform.position : Vector2.zero;
                for (float t = Time.time + seconds; Time.time < t && shade != null;)
                {
                    string now = Mind();
                    if (now != last) { Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {what} {Time.time:F2} {now}"); last = now; }
                    far = Mathf.Max(far, Vector2.Distance(start, shade.transform.position));
                    yield return null;
                }
                moved(far);
            }
            hero.transform.position = Clear(hero, at + new Vector2(4.7f, -1.5f));
            float moved0 = 0f;
            yield return Watch("走近", 5f, m => moved0 = m);
            CharmCheck("走到阴影旁边，它会动起来追人", moved0 > 1f, $"动了 {moved0:F1}");
            // Away, then back: its range entered again.
            hero.transform.position = Clear(hero, at + new Vector2(18f, -1.5f));
            yield return new WaitForSeconds(2f);
            // Struck once, then the player stays by it while the anger runs out.
            var s0 = shade != null ? (Vector2)shade.transform.position : at;
            hero.transform.position = Clear(hero, s0 + new Vector2(2.5f, -1f));
            yield return null;
            InHand(ItemType.CopperShortsword);
            yield return UseAt(s0, 0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 打了一下：{Mind()}");
            for (float t = Time.time + 11.5f; Time.time < t && shade != null;)
            {
                // Kept within its range: back beside it when it drifts off.
                if (Vector2.Distance(hero.transform.position, shade.transform.position) > 5f)
                    hero.transform.position = Clear(hero, (Vector2)shade.transform.position + new Vector2(3f, -1f));
                yield return new WaitForSeconds(0.5f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 气消了：{Mind()}");
            float moved1 = 0f;
            yield return Watch("气消后站在旁边", 5f, m => moved1 = m);
            bool sees = false;
            if (shade != null) foreach (var r in shade.GetComponentsInChildren<AlertRange>(true)) if (r.IsHeroInRange) sees = true;
            CharmCheck("打过它 10 秒以后站在旁边，它还看得到玩家", sees, Mind());
            CharmCheck("打过它 10 秒以后站在旁边，它还在追", moved1 > 1f, $"动了 {moved1:F1}");
            CharmShot("shade");
            mod.Inventory.Slots[0] = default;
            // The Void Heart had (royalCharmState 4, as the user's save): the Shade Control FSM's "Friendly?" sets $Friendly and
            // no contact damage; Idle goes to Friendly Idle and stays there.
            bool hadShadeCharm = pd.gotShadeCharm;
            int royal = pd.royalCharmState;
            pd.gotShadeCharm = true;
            pd.royalCharmState = 4;
            WearOnly(36);
            pd.soulLimited = true;
            pd.shadeScene = "Crossroads_21";
            pd.shadePositionX = 65.1f;
            pd.shadePositionY = 4.3f;
            yield return GoTo("Crossroads_21", EntryGateOf("Crossroads_21"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            heroBox = hero.GetComponent<Collider2D>();
            shade = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && hm.name.StartsWith("Hollow Shade")) shade = hm;
            if (shade != null)
            {
                at = shade.transform.position;
                hero.transform.position = Clear(hero, at + new Vector2(4.7f, -1.5f));
                float moved2 = 0f;
                yield return Watch("戴虚空之心走近", 5f, m => moved2 = m);
                string state = "";
                foreach (var f in shade.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Shade Control") state = f.ActiveStateName;
                CharmCheck("有虚空之心时阴影是友好的，不追人（原版 royalCharmState 4）", state == "Friendly Idle" && moved2 < 0.5f, $"{state}，动了 {moved2:F1}");
                CharmShot("friendly");
            }
            else CharmCheck("戴虚空之心再进场景，阴影还在", false, "");
            pd.gotShadeCharm = hadShadeCharm;
            pd.royalCharmState = royal;
            WearOnly();
            pd.soulLimited = false;
            pd.shadeScene = "None";
        }

        /// <summary>An FSM action's own settings, short: the events it sends or waits for, the numbers it counts.</summary>
        static string DescribeAction(HutongGames.PlayMaker.FsmStateAction act)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var fi in act.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                object v = fi.GetValue(act);
                string s = v switch
                {
                    HutongGames.PlayMaker.FsmEvent e => e?.Name,
                    HutongGames.PlayMaker.FsmInt i => i.UseVariable ? "$" + i.Name : i.Value.ToString(),
                    HutongGames.PlayMaker.FsmFloat f => f.UseVariable ? "$" + f.Name : f.Value.ToString("F2"),
                    HutongGames.PlayMaker.FsmString str => str.UseVariable ? "$" + str.Name : str.Value,
                    HutongGames.PlayMaker.FsmBool bo => bo.UseVariable ? "$" + bo.Name : bo.Value.ToString(),
                    HutongGames.PlayMaker.FsmGameObject go => go.UseVariable ? "$" + go.Name : go.Value != null ? go.Value.name : null,
                    _ => null,
                };
                if (!string.IsNullOrEmpty(s)) sb.Append(sb.Length == 0 ? "(" : ",").Append(fi.Name).Append('=').Append(s);
            }
            return sb.Length > 0 ? sb.Append(')').ToString() : "";
        }

        IEnumerator PalaceFlyProbe()
        {
            var hero = HeroController.instance;
            Shop.DevStage = 5;
            HealthManager fly = null;
            foreach (var (scene, gate) in new[] { ("White_Palace_01", "right1"), ("White_Palace_02", "left1"), ("White_Palace_03_hub", "left1") })
            {
                yield return GoTo(scene, gate);
                for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
                yield return new WaitForSeconds(1.5f);
                hero = HeroController.instance;
                var names = new List<string>();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (hm == null) continue;
                    names.Add(hm.name + (hm.gameObject.activeInHierarchy ? "" : "(未激活)"));
                    if (fly == null && !hm.isDead && hm.gameObject.activeInHierarchy && hm.name.StartsWith("White Palace Fly")) fly = hm;
                }
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {scene} 的敌人：{string.Join("、", names)}");
                if (fly != null) break;
            }
            CharmCheck("找到翅膀傀儡", fly != null, "");
            if (fly == null) yield break;
            string Describe()
            {
                if (fly == null) return "没了";
                string s = $"血 {fly.hp} 死了 {fly.isDead} 无敌 {fly.IsInvincible}/{fly.InvincibleFromDirection} 开 {fly.gameObject.activeInHierarchy}";
                foreach (var f in fly.GetComponents<PlayMakerFSM>()) s += $" {f.FsmName}={f.ActiveStateName}";
                return s;
            }
            var comps = new List<string>();
            foreach (var c in fly.GetComponents<Component>()) if (c != null) comps.Add(c.GetType().Name);
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 翅膀傀儡 {PathOf(fly.transform)} 位置 {(Vector2)fly.transform.position:F1} {Describe()}；组件 {string.Join("、", comps)}");
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 只怕法术 {EnemyStats.ShellOnlySpells(fly)}，碰不到 {EnemyStats.Untouchable(fly)}，睡着 {BossWake.Asleep(fly)}，标签 {fly.gameObject.tag}，层 {fly.gameObject.layer}");
            foreach (var c in fly.GetComponentsInChildren<Collider2D>(true)) Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag}   碰撞体 {DescribeCol(c)}");
            // Its FSMs as the game made them: how one dies.
            foreach (var f in fly.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                var sb = new System.Text.StringBuilder($"[AutoTest] 护符{charmTag} 状态机 {PathOf(f.transform)} {f.FsmName}（开始 {f.Fsm.StartState}）");
                foreach (var v in f.FsmVariables.IntVariables) sb.Append($" int {v.Name}={v.Value}");
                foreach (var v in f.FsmVariables.FloatVariables) sb.Append($" float {v.Name}={v.Value:F2}");
                foreach (var v in f.FsmVariables.BoolVariables) sb.Append($" bool {v.Name}={v.Value}");
                foreach (var e in f.FsmGlobalTransitions) sb.Append($"\n    全局 {e.EventName} → {e.ToState}");
                foreach (var s in f.FsmStates)
                {
                    sb.Append($"\n    {s.Name}：");
                    foreach (var act in s.Actions) sb.Append(act.GetType().Name).Append(DescribeAction(act)).Append(' ');
                    foreach (var tr in s.Transitions) sb.Append($" [{tr.EventName} → {tr.ToState}]");
                }
                Plugin.Log.LogInfo(sb.ToString());
            }
            // Watched a while, untouched.
            string last = "";
            for (float t = Time.time + 3f; Time.time < t;)
            {
                string now = Describe();
                if (now != last) { Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 状态 {Time.time:F2} {now}"); last = now; }
                yield return null;
            }
            var col = fly.GetComponent<Collider2D>() ?? fly.GetComponentInChildren<Collider2D>();
            Vector2 At() => fly != null && col != null ? (Vector2)col.bounds.center : Vector2.zero;
            var deaths = new List<string>();
            int hp0 = fly.hp;
            // The nail, close by it.
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            int h0 = NailItem.DevHits, b0 = MeleeHits.DevBlocked;
            for (float t = Time.time + 2.5f; Time.time < t && fly != null && !fly.isDead;)
            {
                hero.transform.position = new Vector3(At().x - 1.4f, At().y, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                NailAim.DevAim = At();
                NailItem.DevHold = true;
                string now = Describe();
                if (now != last) { Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 骨钉中 {Time.time:F2} {now}"); last = now; }
                yield return null;
            }
            NailItem.DevHold = false;
            NailAim.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 骨钉：命中 {NailItem.DevHits - h0}，被挡 {MeleeHits.DevBlocked - b0}，血 {hp0} → {Describe()}");
            CharmShot("nail");
            bool nailKilled = fly == null || fly.isDead || !fly.gameObject.activeInHierarchy;
            // Watched after: does it come back?
            for (float t = Time.time + 4f; Time.time < t;)
            {
                string now = Describe();
                if (now != last) { Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 之后 {Time.time:F2} {now}"); last = now; }
                yield return null;
            }
            CharmCheck("骨钉能打死翅膀傀儡", nailKilled, Describe());
            // Terraria weapons on one still about.
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && hm.name.StartsWith("White Palace Fly")) { fly = hm; break; }
            col = fly != null ? fly.GetComponent<Collider2D>() ?? fly.GetComponentInChildren<Collider2D>() : null;
            foreach (var weapon in new[] { ItemType.NightsEdge, ItemType.WandofSparking })
            {
                if (fly == null || fly.isDead || !fly.gameObject.activeInHierarchy) break;
                InHand(weapon);
                int before = fly.hp;
                for (int k = 0; k < 6 && fly != null && !fly.isDead; k++)
                {
                    hero.transform.position = new Vector3(At().x - 2f, At().y, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    yield return UseAt(At(), 0.45f);
                }
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {weapon}：血 {before} → {Describe()}");
                CharmCheck($"{weapon} 打得动翅膀傀儡", fly == null || fly.isDead || fly.hp < before, Describe());
            }
            mod.Inventory.Slots[0] = default;
            Shop.DevStage = 0;
        }
    }
}
