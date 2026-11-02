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
    /// Dev (suite "bubble", the user's 11-01 "泡泡枪会把所有敌人的状态机卡住，包括boss，让它们动不了"): the new Bubble Gun fired
    /// at bosses (TK_BUBBLE_BOSSES, by their seals) and at the Forgotten Crossroads' enemies, its bubbles as before 11-01
    /// (MageKits.DevOldBubble) and as now. Watched each frame: the enemy's main FSM (its state changes, the longest it stayed in
    /// one), how far it went, how much of the time a bubble had it, its picture paused. A boss is watched unshot first, its
    /// own pace; "slow" (TK_BUBBLE_PARTS) adds a window of MageMech.Slow alone on it (any magic slow; as before 11-01 with
    /// MageMech.DevSlowBosses). Lines "[AutoTest] 泡泡 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BubbleTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 泡泡 {what} {(pass ? "通过" : "不通过")}");
            }
            var parts = (Environment.GetEnvironmentVariable("TK_BUBBLE_PARTS") ?? "boss,foes,bubble,slow").Split(',');
            var bosses = (Environment.GetEnvironmentVariable("TK_BUBBLE_BOSSES") ?? "Hornet;MantisLord").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            var keepWeapons = World.WeaponMode.Current;
            BladeArts.Set(true);
            var inv = mod.Inventory;
            var st = mod.Stats;
            st.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            st.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);

            PlayMakerFSM Main(HealthManager hm)
            {
                var fsms = hm.GetComponents<PlayMakerFSM>();
                return fsms.FirstOrDefault(f => f.FsmName == "Control") ?? fsms.OrderByDescending(f => f.FsmStates != null ? f.FsmStates.Length : 0).FirstOrDefault();
            }

            // One window: `seconds` long, the gun fired at the enemy (or Slow alone on it, or nothing), the hero kept within a
            // bubble's reach of it. Its state changes a second, the longest stay in one state (s), how far it went sideways
            // and up, the share of frames a bubble had it, the share its picture stood paused.
            (float rate, float dwell, float wentX, float wentY, float wrapped, float paused, int changes, string states) result = default;
            // Its life kept as found (a boss's phases and staggers go by it).
            var keepHp = new Dictionary<HealthManager, int>();
            // A wait with its life kept (bubbles still on it pop meanwhile).
            IEnumerator Keep(HealthManager hm, float seconds)
            {
                for (float end = Time.time + seconds; Time.time < end;)
                {
                    if (hm != null && !hm.isDead && keepHp.TryGetValue(hm, out int floor)) hm.hp = Mathf.Max(hm.hp, floor);
                    yield return null;
                }
            }
            // The hero 3.5 units to one side of an enemy on its floor (feet on its feet), the side it sees it from.
            Vector2 Beside(HealthManager hm)
            {
                var hero = HeroController.instance;
                var box = hero.GetComponent<BoxCollider2D>();
                float feet = hero.transform.position.y - box.bounds.min.y;
                var col = hm.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled && !c.isTrigger) ?? hm.GetComponent<Collider2D>();
                Vector2 c = TerraProjectiles.EnemyCentre(hm);
                float floor = col != null ? col.bounds.min.y : c.y;
                Vector2 best = new Vector2(c.x - 3.5f, floor + feet + 0.05f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var at = new Vector2(c.x + side * 3.5f, floor + feet + 0.05f);
                    bool open = !World.TerraWorld.IsSolidAt(at) && !World.TerraWorld.IsSolidAt(at + Vector2.up)
                        && !Physics2D.Linecast(at + Vector2.up * 0.3f, c, 1 << World.ChunkColliders.TerrainLayer);
                    if (open) return at;
                }
                return best;
            }
            IEnumerator Watch(HealthManager hm, float seconds, string how, Vector2? stand = null)
            {
                result = default;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) yield break;
                if (!keepHp.ContainsKey(hm)) keepHp[hm] = hm.hp;
                var hero = HeroController.instance;
                // A boss's fight: the hero where he came in (chasing it, he was put outside the arena). An enemy: beside it.
                var spot = stand ?? Beside(hm);
                if (how == "fire")
                {
                    hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                }
                var fsm = Main(hm);
                var anim = hm.GetComponent<tk2dSpriteAnimator>();
                string last = fsm != null ? fsm.ActiveStateName : "";
                float since = Time.time, dwell = 0f;
                int changes = 0, frames = 0, wrapped = 0, paused = 0;
                Vector2 p0 = hm.transform.position;
                float minX = p0.x, maxX = p0.x, minY = p0.y, maxY = p0.y;
                var seen = new List<string>();
                if (how == "fire")
                {
                    inv.Slots[0] = ItemStack.Of(ItemType.BubbleGun);
                    inv.Selected = 0;
                    inv.Override = -1;
                    mod.DevHold = true;
                }
                float end = Time.time + seconds;
                while (Time.time < end && hm != null && !hm.isDead && hm.gameObject.activeInHierarchy)
                {
                    mod.Stats.FullHeal();
                    st.Mana = st.ManaMax;
                    PlayerData.instance.isInvincible = true;
                    hm.hp = Mathf.Max(hm.hp, keepHp[hm]);
                    Vector2 c = TerraProjectiles.EnemyCentre(hm);
                    if (how == "fire")
                    {
                        mod.DevAim = c;
                        if (mod.DevReady && !mod.IsUsingItem && !BladeSkills.HoldsLeft) mod.DevUseSelected(c);
                    }
                    else if (how == "slow") MageMech.Slow(hm, 0.3f, 0.1f);
                    string s = fsm != null ? fsm.ActiveStateName : "";
                    if (s != last)
                    {
                        dwell = Mathf.Max(dwell, Time.time - since);
                        since = Time.time;
                        changes++;
                        if (seen.Count < 30) seen.Add(s);
                        last = s;
                    }
                    Vector2 p = hm.transform.position;
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    frames++;
                    if (MageKits.DevBubbled(hm)) wrapped++;
                    if (anim != null && anim.Paused) paused++;
                    yield return null;
                }
                dwell = Mathf.Max(dwell, Time.time - since);
                if (how == "fire")
                {
                    mod.DevHold = null;
                    mod.DevAim = null;
                    inv.Slots[0] = default;
                }
                result = (changes / seconds, dwell, maxX - minX, maxY - p0.y, frames > 0 ? (float)wrapped / frames : 0f, frames > 0 ? (float)paused / frames : 0f, changes, string.Join("、", seen));
            }
            string Line((float rate, float dwell, float wentX, float wentY, float wrapped, float paused, int changes, string states) r) =>
                $"状态切换 {r.changes} 次（{r.rate:0.0}/秒），同一状态最长 {r.dwell:0.0} 秒，横走 {r.wentX:0.0}，升起 {r.wentY:0.0}，泡泡包着 {r.wrapped:P0}，动画停着 {r.paused:P0}；[{r.states}]";

            try
            {
                if (parts.Contains("boss"))
                {
                    foreach (var key in bosses)
                    {
                        // Into the arena by its seal, as a player goes: the fight begun there (BossSummonStart).
                        yield return SealTravel("Town", "left1");
                        var seal = BossSummons.All.FirstOrDefault(x => x.Key == key);
                        if (seal == null) { Check(false, $"{key} 没有战令"); continue; }
                        string record = key.Split('@')[0];
                        Bestiary.Kills[record] = Mathf.Max(1, Bestiary.KillsOf(record));
                        Shop.DevStage = seal.Stage;
                        inv.Slots[0] = ItemStack.Of(seal.Type, 3);
                        inv.Selected = 0;
                        inv.Override = -1;
                        for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                        int started = BossSummonStart.DevStarted;
                        mod.DevUseSelected(HeroController.instance.transform.position);
                        for (float t = Time.realtimeSinceStartup + 30f; (!BossArena.Arrived(key) || BossSummonStart.DevStarted == started) && Time.realtimeSinceStartup < t;) { mod.Stats.FullHeal(); yield return null; }
                        for (float t = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) { mod.Stats.FullHeal(); yield return null; }
                        inv.Slots[0] = default;
                        yield return new WaitForSeconds(2f);
                        PlayerData.instance.isInvincible = true;
                        var boss = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                            .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == record)
                            .OrderBy(h => Vector2.Distance(h.transform.position, HeroController.instance.transform.position)).FirstOrDefault();
                        if (boss == null) { Check(false, $"{key} 没找到首领"); continue; }
                        var fsm = Main(boss);
                        Vector2 home = HeroController.instance.transform.position;
                        Plugin.Log.LogInfo($"[AutoTest] 泡泡 {key}：{boss.name}，看状态机 {(fsm != null ? fsm.FsmName : "无")}，主角站在 {home:F1}");

                        yield return Watch(boss, 8f, "none", home);
                        var calm = result;
                        Plugin.Log.LogInfo($"[AutoTest] 泡泡 {key} 不打 8 秒：{Line(calm)}");
                        // The new bubbles first: a boss the old ones (or a slow) left hanging in a move stays so.
                        // As lively as unshot: half its pace of state changes at least, no state held more than twice its own
                        // longest (3 s at least), going about the arena at least half as far.
                        bool Lively((float rate, float dwell, float wentX, float wentY, float wrapped, float paused, int changes, string states) r) =>
                            r.rate >= 0.5f * calm.rate && r.dwell <= Mathf.Max(3f, 2f * calm.dwell) && r.wentX + r.wentY >= 0.5f * (calm.wentX + calm.wentY);
                        string Calm() => $"不打时 {calm.rate:0.0}/秒、最长 {calm.dwell:0.0} 秒、走 {calm.wentX + calm.wentY:0.0}";
                        // The gun ("bubble") and any magic slow alone ("slow": MageMech.Slow each frame), as now; then one of them as
                        // before 11-01, last, as a boss it left hanging in a move stays so.
                        var windows = new List<(string How, bool Old)>();
                        if (parts.Contains("slow")) windows.Add(("slow", false));
                        if (parts.Contains("bubble")) windows.Add(("fire", false));
                        if (parts.Contains("bubble")) windows.Add(("fire", true));
                        else if (parts.Contains("slow")) windows.Add(("slow", true));
                        foreach (var w in windows)
                        {
                            if (boss == null || boss.isDead) break;
                            MageKits.DevOldBubble = w.Old;
                            MageMech.DevSlowBosses = w.Old;
                            float span = w.How == "fire" ? 10f : 8f;
                            yield return Watch(boss, span, w.How, home);
                            var shot = result;
                            MageKits.DevOldBubble = false;
                            MageMech.DevSlowBosses = false;
                            yield return Keep(boss, 2f);
                            yield return Watch(boss, 6f, "none", home);
                            var after = result;
                            string what = (w.Old ? "旧" : "新") + (w.How == "fire" ? "泡泡连射" : "减速 0.3");
                            Plugin.Log.LogInfo($"[AutoTest] 泡泡 {key} {what} {span:0} 秒：{Line(shot)}");
                            Plugin.Log.LogInfo($"[AutoTest] 泡泡 {key} {what}停后 6 秒：{Line(after)}");
                            bool lively = Lively(shot) && Lively(after);
                            if (!w.Old) Check(lively, $"{key} {what}时和停后照常行动（{Calm()}）");
                            else Plugin.Log.LogInfo($"[AutoTest] 泡泡 {key} {what} {(lively ? "照常" : "卡住或走不动（复现）")}");
                        }
                    }
                    yield return SealTravel("Town", "left1");
                    yield return new WaitForSeconds(1f);
                }

                if (parts.Contains("foes"))
                {
                    const string room = "Crossroads_01";
                    yield return GoTo(room, EntryGateOf(room));
                    yield return new WaitForSeconds(2f);
                    var hero = HeroController.instance;
                    PlayerData.instance.isInvincible = true;
                    var foes = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name == room
                            && !EnemyStats.Unstoppable(EnemyStats.Of(h)) && Main(h) != null && UI.EnemyHover.RecordOf(h) != null)
                        .GroupBy(h => UI.EnemyHover.RecordOf(h)).Select(g => g.First()).Take(3).ToList();
                    Plugin.Log.LogInfo($"[AutoTest] 泡泡 十字路 测 {string.Join("、", foes.Select(f => $"{f.name}({UI.EnemyHover.RecordOf(f)})"))}");
                    foreach (var hm in foes)
                    {
                        foreach (bool old in new[] { false, true })
                        {
                            if (hm == null || hm.isDead) break;
                            MageKits.DevOldBubble = old;
                            yield return Watch(hm, 6f, "fire");
                            var shot = result;
                            MageKits.DevOldBubble = false;
                            yield return Keep(hm, 0.5f);
                            if (hm == null || hm.isDead) break;
                            // The hero stands by: it goes on as it would with him there.
                            yield return Watch(hm, 5f, "none", hero.transform.position);
                            var after = result;
                            string what = old ? "旧泡泡" : "新泡泡";
                            Plugin.Log.LogInfo($"[AutoTest] 泡泡 {hm.name} {what}连射 6 秒：{Line(shot)}");
                            Plugin.Log.LogInfo($"[AutoTest] 泡泡 {hm.name} {what}停手后 5 秒：{Line(after)}");
                            // Wrapped a while, never for good: free part of every rest, acting (a crawlid has no states to change:
                            // walking) while shot, and after.
                            if (!old) Check(shot.wrapped > 0.1f && shot.wrapped <= 0.7f && (shot.changes > 0 || shot.wentX > 0.5f) && (after.changes > 0 || after.wentX > 0.5f),
                                $"{hm.name} 新泡泡：包着 {shot.wrapped:P0}，连射时切换 {shot.changes} 次、走 {shot.wentX:0.0}，停手后切换 {after.changes} 次、走 {after.wentX:0.0}");
                            yield return Keep(hm, 1.5f);
                        }
                    }
                }
            }
            finally
            {
                MageKits.DevOldBubble = false;
                MageMech.DevSlowBosses = false;
                mod.DevHold = null;
                mod.DevAim = null;
                World.WeaponMode.Current = keepWeapons;
                Player.TerraStats.DevImmortal = false;
                Shop.DevStage = -1;
                Plugin.Log.LogInfo($"[AutoTest] 泡泡 合计 {ok}/{all}");
            }
        }
    }
}
