using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossmoves", the user's 10-26 "敌人的招式好像越打越少了，比如二见大黄蜂后期就只会一直扔飞针", then "新版ai的国王边境那一场，
    /// 打到二阶段的时候就只用飞针了"): a boss with the new AI fought out (TK_MOVES "key#hall", the Hornet Sentinel's GG_Hornet_2 by
    /// default; "Hornet@4#world" her fight at Kingdom's Edge) over TK_MOVES_SECONDS (150), its life
    /// brought down from 95% to 5% as it goes, against an unhurt player who takes turns, six seconds each, standing by it,
    /// up in the air beside it, standing off (TK_MOVES_STANCE near/air/far: only that). Every 30 s: the moves it began (its script's wind-up states), how often its move
    /// against the air was chosen, and the share of the time it took the player for camping in the air. Lines "[AutoTest] 首领招式 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossMovesTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string entry = Environment.GetEnvironmentVariable("TK_MOVES");
            if (string.IsNullOrWhiteSpace(entry)) entry = "Hornet#GG_Hornet_2";
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_MOVES_SECONDS"), out var s) ? s : 150f;
            // TK_MOVES_STANCE: the player only by it (near), only up in the air (air), only off (far); else in turns.
            int stance = Array.IndexOf(new[] { "near", "air", "far" }, Environment.GetEnvironmentVariable("TK_MOVES_STANCE") ?? "");
            string key = entry.Split('#')[0], record = key.Split('@')[0];
            string hall = entry.Contains("#") ? entry.Split('#')[1] : null;
            // "key#world": its fight where the world has it (Items.BossArena's arena: its room, its gate, the flags that put it there).
            if (hall == "world" && Items.BossArena.DevWorldArena(key, out var gate, out var needs))
            {
                foreach (var (flag, v) in needs) PlayerData.instance.SetBool(flag, v);
                yield return GoTo(record == "Hornet" ? "Deepnest_East_Hornet" : key, gate);
            }
            else if (hall != null) yield return GoTo(hall, "door_dreamEnter");
            BossKit kit = null;
            for (float until = Time.realtimeSinceStartup + 60f; Time.realtimeSinceStartup < until; )
            {
                kit = KitOf(record);
                if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                yield return null;
            }
            if (kit == null) { Plugin.Log.LogInfo($"[AutoTest] 首领招式 {entry} 没有首领"); yield break; }
            var hero = HeroController.instance;
            var hbody = hero.GetComponent<Rigidbody2D>();
            for (float wake = Time.time + 6f; !kit.Engaged && Time.time < wake;)
            {
                if (!BossWake.Rouse(kit.Hm))
                {
                    kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                    FSMUtility.SendEventToGameObject(kit.gameObject, "TAKE DAMAGE");
                }
                yield return new WaitForSeconds(1f);
                kit = KitOf(record) ?? kit;
            }
            // A world fight that waits on its intro (the Sentinel at Kingdom's Edge stands Inert till the Knight walks up and she has
            // spoken): its waiting state's ways out logged, then taken, a dialogue clicked through.
            for (float until = Time.time + 20f; kit.StateNow == "Inert" && Time.time < until;)
            {
                var control = kit.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Control");
                var inert = control != null ? control.Fsm.GetState("Inert") : null;
                if (inert != null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 首领招式 {record} 在 Inert 等开场：出口 {string.Join("，", inert.Transitions.Select(tr => tr.EventName + "→" + tr.ToState))}");
                    foreach (var tr in inert.Transitions) control.SendEvent(tr.EventName);
                }
                for (int k = 0; k < 10; k++) { yield return Tap(UnityEngine.InputSystem.Key.Space); yield return new WaitForSeconds(0.2f); }
                kit = KitOf(record) ?? kit;
            }
            int max = kit.Info != null ? kit.Info.MaxHp : kit.Hm.hp;
            var counts = new Dictionary<string, int>();
            string last = null;
            int antiAir0 = BossAi.DevAntiAir, window = 0, camping = 0, forced = 0, frames = 0;
            float started = Time.time, windowAt = Time.time, nextBlow = Time.time;
            while (Time.time - started < seconds && kit != null && kit.Hm != null && !kit.Hm.isDead)
            {
                float t = Time.time - started;
                // Its life down as the fight would bring it.
                int want = Mathf.Max(1, Mathf.RoundToInt(max * Mathf.Lerp(0.95f, 0.05f, t / seconds)));
                if (kit.Hm.hp > want) kit.Hm.hp = want;
                // A real blow now and then, as a fight has (its flinches and stuns are counted off hits).
                if (Time.time >= nextBlow)
                {
                    nextBlow = Time.time + 0.7f;
                    kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                }
                // The player's turn: by it on the floor, up in the air by it, off on the floor.
                int turn = stance >= 0 ? stance : (int)(t / 6f) % 3;
                var floor = kit.ClampToArena(kit.Me, 3f);
                float side = floor.x - kit.Arena.center.x > 0f ? -1f : 1f;
                floor.x += side * (turn == 2 ? 8f : 3.5f);
                floor.x = Mathf.Clamp(floor.x, kit.Arena.xMin + 1f, kit.Arena.xMax - 1f);
                floor.y = kit.GroundBelow(new Vector2(floor.x, kit.Arena.center.y)) + 1.2f;
                if (turn == 1) floor.y += 3.5f;
                if (turn == 1 || Vector2.Distance(hero.transform.position, floor) > 3f)
                {
                    hero.transform.position = new Vector3(floor.x, floor.y, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                }
                mod.Stats.FullHeal();
                string state = kit.StateNow;
                if (!string.IsNullOrEmpty(state) && state != last)
                {
                    last = state;
                    counts[state] = counts.TryGetValue(state, out int c) ? c + 1 : 1;
                }
                frames++;
                if (kit.AirCamping) camping++;
                if (kit.AirForced) forced++;
                if (Time.time - windowAt >= 30f)
                {
                    Report(window++, t);
                    windowAt = Time.time;
                }
                yield return null;
                kit = KitOf(record) ?? kit;
            }
            Report(window, Time.time - started);

            void Report(int w, float t)
            {
                // The wind-ups: where each move begins.
                var moves = counts.Where(kv => kv.Key.IndexOf("Antic", StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.StartsWith("TK ") || kv.Key.Contains("Barb"))
                    .OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}");
                var all = counts.OrderByDescending(kv => kv.Value).Take(14).Select(kv => $"{kv.Key}×{kv.Value}");
                Plugin.Log.LogInfo($"[AutoTest] 首领招式 {record} 窗口 {w}（到 {t:F0}s，血 {(kit != null && kit.Hm != null ? kit.Hm.hp * 100 / Mathf.Max(1, max) : 0)}%）：起手 {string.Join("，", moves)}｜对空 {BossAi.DevAntiAir - antiAir0} 次，判定空中蹲守 {camping * 100 / Mathf.Max(1, frames)}%、强制对空 {forced * 100 / Mathf.Max(1, frames)}%");
                Plugin.Log.LogInfo($"[AutoTest] 首领招式 {record} 窗口 {w} 全部状态：{string.Join("，", all)}");
                counts.Clear();
                antiAir0 = BossAi.DevAntiAir;
                camping = forced = frames = 0;
            }
        }
    }
}
