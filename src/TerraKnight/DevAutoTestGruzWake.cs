using System.Collections;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "gruzwake"): the Gruz Mother woken by Terraria's weapons, the way a player comes
    /// to her. Three rounds, each from the room next door through her arena's left gate on foot:
    /// a broadsword from beside her, a Minishark and an Amethyst Staff from across her arena (out
    /// of her Battle Range, nothing between). Each: whether the blow woke her, that it took one
    /// point and no more (Items.BossWake), how many blows her waking held off, and that blows count
    /// again after it. The last round goes on: from outside the arena's gate nothing reaches her
    /// (World.ArenaGuard, its notice in the middle of the screen), then the fight to its end, her
    /// brood too, and whether every gate opens. Every change of her FSMs, the battle's and its
    /// gates is logged as it comes.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GruzWakeTest()
        {
            // From the left: the right gate is the way out, shut until she's beaten.
            const string room = "Crossroads_04", gate = "left1";
            var gm = GameManager.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            World.VanillaGuards.DevGateTrace = true;
            if (!GateLink(room, gate, out var link)) { Plugin.Log.LogInfo("[AutoTest] 叫醒 不知道门外是哪 不通过"); yield break; }
            string outSide = GateLink(link.To, link.Entry, out var back) ? back.Side : Opposite(link.Side);
            var inv = mod.Inventory;
            inv.Infinite = true;
            int passed = 0;
            var rounds = new[] { (ItemType.IronBroadsword, ItemType.None, true, "近战"), (ItemType.Minishark, ItemType.MusketBall, false, "远程"), (ItemType.AmethystStaff, ItemType.None, false, "法术") };
            for (int round = 0; round < rounds.Length; round++)
            {
                var (weapon, ammo, close, kind) = rounds[round];
                yield return GoTo(link.To, link.Entry);
                var res = new bool[1];
                yield return ThroughGate(link.To, link.Entry, outSide, room, res);
                var h = HeroController.instance;
                if (gm.sceneName != room) { Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind}：没走进 {room} 不通过"); continue; }
                HealthManager gruz = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (UI.EnemyHover.RecordOf(hm) == "BigFly") gruz = hm;
                if (gruz == null) { Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind}：找不到格鲁兹之母 不通过"); continue; }
                string States()
                {
                    var sb = new System.Text.StringBuilder();
                    if (gruz != null) foreach (var f in gruz.GetComponents<PlayMakerFSM>()) sb.Append($" {f.FsmName}:{f.ActiveStateName}");
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if (f.gameObject.scene.name == room && (f.name.Contains("Battle") || f.FsmName.Contains("Battle"))) sb.Append($" {f.name}/{f.FsmName}:{f.ActiveStateName}");
                    return sb.ToString();
                }
                string last = States();
                IEnumerator Watch(string what, float seconds, System.Func<bool> step)
                {
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        mod.Stats.FullHeal();
                        mod.Stats.Mana = mod.Stats.ManaMax;
                        PlayerData.instance.isInvincible = true;
                        string now = States();
                        if (now != last)
                        {
                            Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind} {what} {t:F1}s 主角 {(Vector2)h.transform.position:F1}：{now}");
                            last = now;
                        }
                        if (step != null && step()) yield break;
                        yield return null;
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind}（{ItemDb.Get(weapon).Name}）：从 {link.To} 走进来，她在 {(Vector2)gruz.transform.position:F1} 无敌 {gruz.IsInvincible}；{last}");

                // Where the blow comes from: beside her for the sword (walked to, jumping what's in the way),
                // across the arena for the gun and the staff.
                Vector2 Her() => gruz.GetComponent<Collider2D>() != null ? (Vector2)gruz.GetComponent<Collider2D>().bounds.center : (Vector2)gruz.transform.position;
                if (close)
                {
                    yield return Watch("走过去", 20f, () =>
                    {
                        if (gruz == null) return true;
                        float dx = gruz.transform.position.x - h.transform.position.x;
                        if (Mathf.Abs(dx) < 2.5f) { Player.TerraMovement.DevMove = 0; return true; }
                        Stride(h, (int)Mathf.Sign(dx));
                        return false;
                    });
                    Player.TerraMovement.DevMove = 0;
                    if (gruz != null && Mathf.Abs(gruz.transform.position.x - h.transform.position.x) > 4f)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind} 辅助：走不到她跟前（跳了 {strideJumps} 次），挡着的：{Blockers(h, gruz.GetComponent<Collider2D>().bounds)}");
                        var down = Physics2D.Raycast(Her() + new Vector2(-2f, 0f), Vector2.down, 30f, 1 << World.ChunkColliders.TerrainLayer);
                        h.transform.position = Clear(h, down.collider != null ? down.point + Vector2.up * 0.7f : Her() + new Vector2(-2f, 0f));
                    }
                }
                else
                {
                    Vector3? spot = null;
                    Vector2 her = Her();
                    foreach (float dx in new[] { -16f, -14f, -18f, -12f, -20f, 16f, 14f, 12f })
                    {
                        var floor = Physics2D.Raycast(her + new Vector2(dx, 4f), Vector2.down, 30f, 1 << World.ChunkColliders.TerrainLayer);
                        if (floor.collider == null) continue;
                        Vector2 stand = floor.point + Vector2.up * 1.2f;
                        if (Physics2D.Linecast(stand, her, 1 << World.ChunkColliders.TerrainLayer).collider != null) continue;
                        // Inside her arena (no battle gate between): a spot past a gate is outside the fight.
                        var keep = h.transform.position;
                        h.transform.position = Clear(h, floor.point + Vector2.up * 0.7f);
                        if (World.ArenaGuard.Outside(gruz)) { h.transform.position = keep; continue; }
                        spot = h.transform.position;
                        break;
                    }
                    if (spot == null) { Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind}：找不到战斗区里能直接打到她的位置 不通过"); continue; }
                    h.transform.position = spot.Value;
                    Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind}：站到 {(Vector2)spot.Value:F1}，离她 {Vector2.Distance(spot.Value, her):F0} 格");
                }
                if (gruz.transform.position.x > h.transform.position.x) h.FaceRight(); else h.FaceLeft();
                yield return new WaitForSeconds(0.6f);

                // The weapon at her until a while after she's up.
                inv.Slots[0] = ItemStack.Of(weapon);
                if (ammo != ItemType.None) inv.Slots[Inventory.Size] = ItemStack.Of(ammo, 999);
                inv.Selected = 0;
                var info = EnemyStats.Of(gruz);
                int life0 = EnemyStats.Life(gruz, info), woken0 = BossWake.DevWoken, held0 = BossWake.DevHeld;
                int lifeAtWake = -1, heldDuring = 0;
                float wokeAt = -1f, freeAt = -1f;
                var lives = new System.Text.StringBuilder();
                float t1 = Time.time;
                yield return Watch("攻击", 12f, () =>
                {
                    if (gruz == null) return true;
                    int life = EnemyStats.Life(gruz, info);
                    if (wokeAt < 0f && BossWake.DevWoken > woken0) { wokeAt = Time.time; lifeAtWake = life; }
                    if (wokeAt > 0f)
                    {
                        heldDuring = BossWake.DevHeld - held0;
                        if (freeAt < 0f && life < lifeAtWake) freeAt = Time.time;
                        if (freeAt > 0f && Time.time - freeAt > 2f) return true;
                    }
                    if (lives.Length < 500 && (int)((Time.time - t1) * 2f) != (int)((Time.time - t1 - Time.deltaTime) * 2f)) lives.Append($" {life}");
                    Vector2 aim = Her();
                    mod.DevAim = aim;
                    if ((gruz.transform.position.x > h.transform.position.x) != h.cState.facingRight) { if (gruz.transform.position.x > h.transform.position.x) h.FaceRight(); else h.FaceLeft(); }
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                    return false;
                });
                mod.DevAim = null;
                bool woke = wokeAt > 0f;
                bool one = woke && lifeAtWake == life0 - 1;
                bool resumed = freeAt > 0f;
                bool ok = woke && one && resumed;
                if (ok) passed++;
                Plugin.Log.LogInfo($"[AutoTest] 叫醒 {kind}（{ItemDb.Get(weapon).Name}）：门外挡下 {World.ArenaGuard.DevBlocked} 下（累计），叫醒 {woke}，醒时血 {life0}→{lifeAtWake}（应只少 1），醒的过程中挡下 {heldDuring} 下，{(resumed ? $"醒后 {freeAt - wokeAt:F1} 秒开始掉血" : "醒后一直没掉血")}；每半秒血量{lives} {(ok ? "通过" : "不通过")}");

                // The last round goes on: from outside the arena's gate, then to the end.
                if (round < rounds.Length - 1 || gruz == null) continue;
                Transform gate1 = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (f.FsmName == "BG Control" && f.gameObject.scene.name == room && f.name == "Battle Gate 1") gate1 = f.transform;
                if (gate1 != null)
                {
                    bool herRight = gruz.transform.position.x > gate1.position.x;
                    Vector2 outside = new Vector2(gate1.position.x + (herRight ? -4f : 4f), gate1.position.y + 1f);
                    var floor = Physics2D.Raycast(outside, Vector2.down, 12f, 1 << World.ChunkColliders.TerrainLayer);
                    h.transform.position = Clear(h, floor.collider != null ? floor.point + Vector2.up * 0.7f : outside);
                    int hp1 = gruz.hp, blocked0 = World.ArenaGuard.DevBlocked;
                    inv.Slots[0] = ItemStack.Of(ItemType.Zenith);
                    UI.CenterNotice.DevLast = null;
                    yield return Watch("门外", 3f, () =>
                    {
                        if (gruz == null) return true;
                        mod.DevAim = Her();
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(Her());
                        return false;
                    });
                    mod.DevAim = null;
                    bool held = gruz != null && gruz.hp == hp1;
                    if (held) passed++;
                    Plugin.Log.LogInfo($"[AutoTest] 叫醒 站在门外打：血 {hp1}→{(gruz != null ? gruz.hp : -1)}，挡下 {World.ArenaGuard.DevBlocked - blocked0} 下，屏幕中间提示「{UI.CenterNotice.DevLast}」 {(held ? "通过" : "不通过")}");
                }
                float until = Time.time + 90f;
                while (Time.time < until)
                {
                    mod.Stats.FullHeal();
                    PlayerData.instance.isInvincible = true;
                    HealthManager target = null;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm.gameObject.scene.name != room || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || EnemyStats.Defeated(hm) || World.ArenaGuard.OffMap(hm)) continue;
                        if (target == null || Vector2.Distance(hm.transform.position, h.transform.position) < Vector2.Distance(target.transform.position, h.transform.position)) target = hm;
                    }
                    bool anyShut = false;
                    foreach (var (gn, st) in BattleGates(room)) if (gn == "Battle Gate 1" && Shut(st)) anyShut = true;
                    if (target == null && !anyShut) break;
                    if (target == null) { yield return new WaitForSeconds(0.5f); continue; }
                    Vector2 at = target.transform.position;
                    if (Vector2.Distance(at, h.transform.position) > 10f)
                    {
                        var fl = Physics2D.Raycast(at + new Vector2(-4f, 2f), Vector2.down, 30f, 1 << World.ChunkColliders.TerrainLayer);
                        h.transform.position = Clear(h, fl.collider != null ? fl.point + Vector2.up * 0.7f : at + new Vector2(-4f, 0f));
                    }
                    mod.DevAim = at;
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at);
                    yield return null;
                }
                mod.DevAim = null;
                yield return new WaitForSeconds(3f);
                var after = BattleGates(room);
                bool open = true;
                foreach (var (gn, st) in after) if (Shut(st)) open = false;
                if (open) passed++;
                Plugin.Log.LogInfo($"[AutoTest] 叫醒 打完：铁门{string.Join("", System.Linq.Enumerable.Select(after, g => $" {g.Name}:{g.State}"))} {(open ? "全开 通过" : "还有关着的 不通过")}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 叫醒 合计 {passed}/{rounds.Length + 2}");
        }
    }
}
