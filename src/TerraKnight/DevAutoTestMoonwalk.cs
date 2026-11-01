using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "moonwalk", the user's 10-31 "用新版武器打怪有些怪会倒着走卡着出去，比如发光子宫那里的怪…还有尖刺躯壳，都会卡着后退"):
    /// in rooms of such enemies (TK_MOON_ROOMS), with the game's AI and the new one (TK_MOON_AI) and each weapon kind
    /// (TK_MOON_WEAPONS: new and original editions), the player beside an enemy hits it for four seconds, then watches three
    /// from further off. Every frame: its picture, its speed, the way its picture faces (its x scale), whether a knockback
    /// has it. "倒退" = moving sideways faster than a crawl the way its back is turned, a tenth of a second running or more,
    /// with no knockback on it. The first enemy of each kind is logged frame by frame (TK_MOON_FRAMES=0 for none).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MoonwalkTest()
        {
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var rooms = (Environment.GetEnvironmentVariable("TK_MOON_ROOMS") ?? "Fungus3_22;Crossroads_22;Crossroads_01").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            string ai = Environment.GetEnvironmentVariable("TK_MOON_AI") ?? "both";
            var modes = new List<World.EnemyAiKind>();
            if (ai != "new") modes.Add(World.EnemyAiKind.Vanilla);
            if (ai != "vanilla") modes.Add(World.EnemyAiKind.New);
            var weapons = (Environment.GetEnvironmentVariable("TK_MOON_WEAPONS") ?? "LightsBane:new;Minishark:new;LightsBane:old")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => (Type: (ItemType)Enum.Parse(typeof(ItemType), s.Split(':')[0]), New: !s.EndsWith(":old"))).ToList();
            bool frames = Environment.GetEnvironmentVariable("TK_MOON_FRAMES") != "0";
            var keepAi = World.EnemyAiMode.Current;
            var keepWeapons = World.WeaponMode.Current;
            var inv = mod.Inventory;
            int slot = Inventory.HotbarSize - 1;
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 999);
            var logged = new HashSet<string>();
            int total = 0, enemies = 0, totalPlaying = 0, totalStuck = 0;
            KnockFlight.DevNoHold = Environment.GetEnvironmentVariable("TK_MOON_HOLD") == "0";
            var summary = new List<string>();
            try
            {
                foreach (var mode in modes)
                {
                    World.EnemyAiMode.Current = mode;
                    foreach (var room in rooms)
                    {
                        yield return GoTo(room, EntryGateOf(room));
                        yield return new WaitForSeconds(1.5f);
                        var hero = HeroController.instance;
                        PlayerData.instance.isInvincible = true;
                        var all = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                            .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name == room
                                && UI.EnemyHover.RecordOf(h) != null && !(EnemyStats.Of(h)?.Boss ?? false) && h.GetComponent<tk2dSpriteAnimator>() != null)
                            .ToList();
                        // One of each kind, the nearest first.
                        var targets = all.GroupBy(h => UI.EnemyHover.RecordOf(h)).Select(g => g.OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).First()).Take(4).ToList();
                        Plugin.Log.LogInfo($"[AutoTest] 倒退 {mode} {room}：敌人 {all.Count} 个，测 {string.Join("、", targets.Select(t => $"{t.name}({UI.EnemyHover.RecordOf(t)})"))}");
                        foreach (var w in weapons)
                        foreach (var hm in targets)
                        {
                            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                            World.WeaponMode.Current = w.New ? World.WeaponKind.New : World.WeaponKind.Vanilla;
                            var rb = hm.GetComponent<Rigidbody2D>();
                            var anim = hm.GetComponent<tk2dSpriteAnimator>();
                            var col = hm.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled && !c.isTrigger) ?? hm.GetComponent<Collider2D>();
                            if (col == null) continue;
                            string kind = UI.EnemyHover.RecordOf(hm);
                            // Just to its side, on its floor (a flyer: under it).
                            Vector2 c0 = col.bounds.center;
                            float side = c0.x > Arena0(room) ? -1f : 1f;
                            var at = Clear(hero, new Vector2(c0.x + side * 2.5f, c0.y));
                            hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                            inv.Slots[slot] = ItemStack.Of(w.Type);
                            inv.Selected = slot;
                            int uses = 0, back = 0, backKnocked = 0, count = 0, run = 0, turns = 0, playing = 0, stuck = 0, stuckRun = 0;
                            float backPath = 0f, lastDir = 0f;
                            var clips = new HashSet<string>();
                            var first = new List<string>();
                            bool log = frames && logged.Add($"{mode}/{kind}/{w.Type}{(w.New ? "" : "原版")}");
                            float nextLog = 0f;
                            Vector2 last = hm.transform.position;
                            for (float t = 0f; t < 7f && hm != null && !hm.isDead && hm.gameObject.activeInHierarchy; t += Time.deltaTime)
                            {
                                hm.hp = Mathf.Max(hm.hp, 400);
                                PlayerData.instance.isInvincible = true;
                                if (t < 4f)
                                {
                                    if (mod.DevReady)
                                    {
                                        var aim = (Vector2)col.bounds.center;
                                        mod.DevAim = aim;
                                        mod.DevUseSelected(aim);
                                        mod.DevAim = null;
                                        uses++;
                                    }
                                }
                                else if (t - Time.deltaTime < 4f)
                                    hero.transform.position = new Vector3(hero.transform.position.x + side * 5f, hero.transform.position.y, hero.transform.position.z);
                                Vector2 now = hm.transform.position;
                                float vx = Time.deltaTime > 0f ? (now.x - last.x) / Time.deltaTime : 0f;
                                last = now;
                                float face = -Mathf.Sign(hm.transform.lossyScale.x);
                                var flight = hm.GetComponent<KnockFlight>();
                                var slide = hm.GetComponent<KnockSlide>();
                                bool knocked = flight != null && flight.Active || slide != null && slide.Active;
                                string clip = anim.CurrentClip != null ? anim.CurrentClip.name : "";
                                // Its picture running on under a push (moving backwards in a walk), or still held a while after one.
                                if (knocked && Mathf.Abs(vx) > 0.8f && anim.Playing && !anim.Paused) playing++;
                                if (!knocked && anim.Paused && !MageMech.IsHeld(hm)) { if (++stuckRun > 30) stuck++; }
                                else stuckRun = 0;
                                if (Mathf.Abs(vx) > 0.8f)
                                {
                                    if (lastDir != 0f && Mathf.Sign(vx) != lastDir) turns++;
                                    lastDir = Mathf.Sign(vx);
                                }
                                count++;
                                if (Mathf.Abs(vx) > 0.8f && Mathf.Sign(vx) != face)
                                {
                                    if (knocked) backKnocked++;
                                    else if (++run >= 6)
                                    {
                                        back++;
                                        backPath += Mathf.Abs(vx) * Time.deltaTime;
                                        clips.Add(clip);
                                        if (first.Count < 4) first.Add($"{t:F2}s {clip} vx {vx:F1}");
                                    }
                                }
                                else run = 0;
                                if (log && t >= nextLog && t < 6f)
                                {
                                    nextLog = t + 0.05f;
                                    var fsm = string.Join("/", hm.GetComponents<PlayMakerFSM>().Where(f => f.enabled).Select(f => $"{f.FsmName}:{f.ActiveStateName}"));
                                    Plugin.Log.LogInfo($"[AutoTest] 倒退 帧 {mode} {kind} {w.Type}{(w.New ? "" : "原版")} {t:F2}s {clip} 朝 {face:+0;-0} vx {vx:F1} rb {(rb != null ? rb.linearVelocity.ToString("F1") : "-")} 击退 {(flight != null && flight.Active ? (flight.Airborne ? "空中" : "落地") : slide != null && slide.Active ? "滑" : "-")} " +
                                                       $"稳住 {TerraKnockback.Steady(hm)} 人在 {(hero.transform.position.x > now.x ? "右" : "左")} 位置 {now:F2} {fsm}");
                                }
                                yield return null;
                            }
                            inv.Slots[slot] = default;
                            if (hm == null) continue;
                            enemies++;
                            total += back;
                            totalPlaying += playing;
                            totalStuck += stuck;
                            string line = $"{mode} {room} {kind} {w.Type}{(w.New ? "（新版）" : "（原版）")}：用 {uses} 下，7 秒 {count} 帧里没被击退却倒退 {back} 帧（{backPath:F2}），被击退时倒退 {backKnocked} 帧（其中动画还在播 {playing} 帧），停手后动画卡住 {stuck} 帧，掉头 {turns} 次；动画 [{string.Join("、", clips)}]{(first.Count > 0 ? "；如 " + string.Join("；", first) : "")}";
                            Plugin.Log.LogInfo($"[AutoTest] 倒退 {line}");
                            summary.Add(line);
                            yield return new WaitForSeconds(0.5f);
                        }
                    }
                }
            }
            finally
            {
                World.EnemyAiMode.Current = keepAi;
                World.WeaponMode.Current = keepWeapons;
                KnockFlight.DevNoHold = false;
                Plugin.Log.LogInfo($"[AutoTest] 倒退 合计：{enemies} 次，没被击退却倒退 {total} 帧，被击退时动画还在播 {totalPlaying} 帧，停手后动画卡住 {totalStuck} 帧 {(totalPlaying == 0 && totalStuck == 0 ? "通过" : "不通过")}");
            }
        }
    }
}
