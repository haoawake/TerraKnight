using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossguard", the user's 10-25 "进入boss房后才能攻击到敌人、boss必须等玩家出手召唤物才攻击"): in a few
    /// boss rooms, a boss struck from where the player comes in (outside its arena) by his own blow and by a minion's, then
    /// from beside it by a minion's before his own, his own, and a minion's after. TK_GUARD_ROOMS "scene#gate" (';').
    /// Lines "[AutoTest] 首领出手 …：通过 / 不通过".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossGuardTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            string rooms = Environment.GetEnvironmentVariable("TK_GUARD_ROOMS") ?? "Crossroads_04;Fungus1_20_v02;GG_Vengefly#door_dreamEnter;GG_False_Knight#door_dreamEnter";
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 首领出手 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            foreach (var entry in rooms.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('#');
                yield return GoTo(parts[0], parts.Length > 1 ? parts[1] : EntryGateOf(parts[0]));
                yield return new WaitForSecondsRealtime(2f);
                var hero = HeroController.instance;
                var hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h)?.Boss == true)
                    .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).FirstOrDefault();
                if (hm == null) { Plugin.Log.LogInfo($"[AutoTest] 首领出手 {parts[0]}：没有首领"); continue; }
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                string room = $"{parts[0]} {EnemyStats.Of(hm).Record}";
                Vector2 heroAt = hero.transform.position;
                float far = Vector2.Distance(heroAt, col.bounds.center);
                int hp0 = hm.hp;
                bool own = mod.Melee.HitWith(col, 40, 0f, 0f, 1f, heroAt);
                bool minion = mod.Melee.HitWith(col, 40, 0f, 0f, 1f, heroAt, autonomous: true);
                yield return null;
                Check($"{room} 门口（离首领 {far:F1}）：玩家打不到、召唤物打不到", !own && !minion && hm.hp == hp0, $"玩家 {own}，召唤物 {minion}，血 {hp0}→{hm.hp}；{World.ArenaGuard.DevArena(hm)}");
                // Beside it (a clear place a little to its side), in its arena.
                foreach (float side in new[] { 1f, -1f })
                {
                    hero.transform.position = Clear(hero, (Vector2)col.bounds.center + new Vector2(side * (col.bounds.extents.x + 2f), 0f));
                    yield return new WaitForSecondsRealtime(0.6f);
                    if (!World.ArenaGuard.Outside(hm)) break;
                }
                heroAt = hero.transform.position;
                bool minionFirst = !MinionEngage.BossFought && mod.Melee.HitWith(col, 40, 0f, 0f, 1f, heroAt, autonomous: true);
                Check($"{room} 场地里，玩家出手前：召唤物打不到", !minionFirst, $"召唤物 {minionFirst}，已出手 {MinionEngage.BossFought}；{World.ArenaGuard.DevArena(hm)}");
                bool ownNow = mod.Melee.HitWith(col, 40, 0f, 0f, 1f, heroAt);
                yield return new WaitForSecondsRealtime(3f);
                bool minionAfter = false;
                for (int k = 0; k < 5 && !minionAfter; k++)
                {
                    minionAfter = hm != null && !hm.isDead && mod.Melee.HitWith(col, 40, 0f, 0f, 1f, heroAt, autonomous: true);
                    yield return new WaitForSecondsRealtime(0.4f);
                }
                Check($"{room} 场地里：玩家打得到，之后召唤物也打得到", ownNow && minionAfter, $"玩家 {ownNow}，召唤物 {minionAfter}，首领出手 {MinionEngage.BossFought}；{World.ArenaGuard.DevArena(hm)}；挡下 {World.ArenaGuard.DevBlocked}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 首领出手 合计 {ok}/{all}");
        }
    }
}
