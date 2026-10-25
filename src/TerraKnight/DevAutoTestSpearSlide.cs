using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spearslide"): the user's "躯壳被长矛连续击退后就开始平移了" (10-25). In rooms of walking husks, with the game's own
    /// AI and the new one, the player waits a way off until the enemy walks over, steps in front of it and jabs it with a
    /// Spear for five seconds (each jab a knockback, the first on a walk's pace, the next before the last has settled), then
    /// steps back; within a second of a push's end (the push's aftermath, not the enemy's own moves), a tenth of a second
    /// running or more of the enemy moving sideways faster than a crawl while its picture is not one that moves (an idle, a stand,
    /// an attack's wind-up) count as gliding.
    /// TK_SLIDE_ROOMS picks the rooms (';').
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly Regex movingClip = new Regex("walk|run|charge|dash|lunge|jump|leap|fly|roll|hop|rush|slash|turn|chase|move|swoop|fall|land|recoil|hurt|stun|knock|death|die|evade|shoot|throw|burrow",
            RegexOptions.IgnoreCase);

        IEnumerator SpearSlideTest()
        {
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            string pick = Environment.GetEnvironmentVariable("TK_SLIDE_ROOMS");
            var rooms = string.IsNullOrWhiteSpace(pick) ? new[] { "Crossroads_01", "Crossroads_15", "Crossroads_19" } : pick.Split(';');
            var inv = mod.Inventory;
            int slot = Inventory.HotbarSize - 1;
            int totalGlide = 0, enemies = 0;
            bool pictured = false;
            foreach (var mode in new[] { World.EnemyAiKind.Vanilla, World.EnemyAiKind.New })
            {
                World.EnemyAiMode.Current = mode;
                foreach (var room in rooms)
                {
                    yield return GoTo(room, EntryGateOf(room));
                    yield return new WaitForSeconds(1.5f);
                    var hero = HeroController.instance;
                    var targets = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name == room
                            && h.GetComponent<Rigidbody2D>() is Rigidbody2D r && r.bodyType == RigidbodyType2D.Dynamic && r.gravityScale > 0f
                            && h.GetComponent<tk2dSpriteAnimator>() != null && !(EnemyStats.Of(h)?.Boss ?? false))
                        .Take(4).ToList();
                    Plugin.Log.LogInfo($"[AutoTest] 长矛平移 {mode} {room}：地面敌人 {targets.Count} 个 {string.Join("、", targets.Select(t => t.name))}");
                    foreach (var hm in targets)
                    {
                        if (hm == null || hm.isDead) continue;
                        var rb = hm.GetComponent<Rigidbody2D>();
                        var anim = hm.GetComponent<tk2dSpriteAnimator>();
                        var col = hm.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled && !c.isTrigger);
                        if (rb == null || col == null) continue;
                        // Six units off on its floor until it walks over (three seconds at most), then just in front of it.
                        float side = hm.transform.position.x > Arena0(room) ? -1f : 1f;
                        var far = Clear(hero, new Vector2(col.bounds.center.x + side * 6f, col.bounds.min.y + 1.2f));
                        hero.transform.position = new Vector3(far.x, far.y, hero.transform.position.z);
                        for (float w = 0f; w < 3f && hm != null && !hm.isDead; w += Time.deltaTime)
                        {
                            if (Mathf.Abs(rb.linearVelocity.x) > 0.5f && anim.CurrentClip != null && Regex.IsMatch(anim.CurrentClip.name, "walk|run", RegexOptions.IgnoreCase)) break;
                            yield return null;
                        }
                        if (hm == null || hm.isDead) continue;
                        bool walking = Mathf.Abs(rb.linearVelocity.x) > 0.5f;
                        float toward = walking ? Mathf.Sign(rb.linearVelocity.x) : side;
                        Plugin.Log.LogInfo($"[AutoTest] 长矛平移 {hm.name} 走来 {walking} {anim.CurrentClip?.name} vx {rb.linearVelocity.x:F1}，原版后坐 {TerraKnockback.DevRecoil(hm) ?? "无"}");
                        side = toward;
                        var at = Clear(hero, new Vector2(col.bounds.center.x + toward * 2.2f, col.bounds.min.y + 1.2f));
                        hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                        // Five seconds of jabs, as fast as the Spear goes, then three of watching from further off.
                        inv.Slots[slot] = ItemStack.Of(ItemType.Spear);
                        inv.Selected = slot;
                        int jabs = 0, glide = 0, frames = 0, run = 0;
                        float fastest = 0f, glidePath = 0f;
                        var glideClips = new HashSet<string>();
                        var firstGlides = new List<string>();
                        Vector2 last = hm.transform.position;
                        bool backedOff = false;
                        float pushedAt = -10f;
                        bool shots = Environment.GetEnvironmentVariable("TK_SLIDE_SHOTS") == "1" && !pictured;
                        pictured |= shots;
                        float nextShot = 0f;
                        for (float t = 0f; t < 8f && hm != null && !hm.isDead; t += Time.deltaTime)
                        {
                            hm.hp = Mathf.Max(hm.hp, 400);
                            if (t < 5f)
                            {
                                if (mod.DevReady)
                                {
                                    var aim = (Vector2)col.bounds.center;
                                    mod.DevAim = aim;
                                    mod.DevUseSelected(aim);
                                    mod.DevAim = null;
                                    jabs++;
                                }
                            }
                            else if (!backedOff)
                            {
                                backedOff = true;
                                hero.transform.position = new Vector3(hero.transform.position.x - side * 5f, hero.transform.position.y, hero.transform.position.z);
                            }
                            // TK_SLIDE_SHOTS=1: the first enemy's first three seconds pictured (a frame each eighth of a second).
                            if (shots && t < 3f && t >= nextShot)
                            {
                                nextShot = t + 0.04f;
                                var fl = hm.GetComponent<KnockFlight>();
                                Plugin.Log.LogInfo($"[AutoTest] 长矛平移 画面 {t:F2}s {anim.CurrentClip?.name} vx {rb.linearVelocity.x:F1} vy {rb.linearVelocity.y:F1} 击退中 {(fl != null && fl.Active)} 在空中 {(fl != null && fl.Airborne)} 位置 ({hm.transform.position.x:F2},{hm.transform.position.y:F2}) 最高 {KnockFlight.DevPeak:F2} 空中 {KnockFlight.DevAir} 帧 重力 {rb.gravityScale:F2}");
                                Shot(room, $"spearslide_{mode}_{t:00.00}");
                            }
                            frames++;
                            var flight = hm.GetComponent<KnockFlight>();
                            bool knocked = flight != null && flight.Active;
                            if (knocked) pushedAt = t;
                            Vector2 now = hm.transform.position;
                            float vx = Time.deltaTime > 0f ? (now.x - last.x) / Time.deltaTime : 0f;
                            last = now;
                            string clip = anim.CurrentClip != null ? anim.CurrentClip.name : "";
                            if (!knocked && t - pushedAt < 1f && Mathf.Abs(vx) > 0.8f && !movingClip.IsMatch(clip))
                            {
                                run++;
                                // A tenth of a second or more (a chase's first frames still in the last move's picture are not a glide).
                                if (run >= 6)
                                {
                                    glide++;
                                    glidePath += Mathf.Abs(vx) * Time.deltaTime;
                                    fastest = Mathf.Max(fastest, Mathf.Abs(vx));
                                    glideClips.Add(clip);
                                    if (firstGlides.Count < 3) firstGlides.Add($"{t:F2}s {clip} vx {vx:F1} rb {rb.linearVelocity.x:F1}");
                                }
                            }
                            else run = 0;
                            yield return null;
                        }
                        inv.Slots[slot] = default;
                        if (hm == null || hm.isDead) continue;
                        enemies++;
                        totalGlide += glide;
                        Plugin.Log.LogInfo($"[AutoTest] 长矛平移 {mode} {room} {hm?.name}：戳 {jabs} 下，8 秒里持续平移 {glide}/{frames} 帧，滑了 {glidePath:F2}，最快 {fastest:F1}/秒，"
                            + $"当时的动画 [{string.Join("、", glideClips)}]{(firstGlides.Count > 0 ? "；如 " + string.Join("；", firstGlides) : "")}");
                    }
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 长矛平移 合计：{enemies} 个敌人，平移 {totalGlide} 帧 {(totalGlide == 0 ? "通过" : "不通过")}");
        }

        /// <summary>A room's middle (x) for which side to stand on.</summary>
        static float Arena0(string room)
        {
            var r = World.TerraWorld.Live;
            if (r == null) return 0f;
            var rect = r.CellRect;
            return (rect.xMin + rect.width * 0.5f) * World.CellGrid.CellSize;
        }
    }
}
