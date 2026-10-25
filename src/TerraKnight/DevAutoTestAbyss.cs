using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "abyss"): the Abyss Shriek's weapons (Items/Abyss.cs). Their sounds mixed from Terraria's; the reward
    /// (screamLevel 2: the four in the bag, once); then in a room with an enemy held in front: the yoyo (its reach, its 12
    /// seconds, ripples in its first six at most every 0.5 s, rends in its last six, an enemy struck once in 0.2 s), the
    /// sniper (a plain shot through walls, the aim slowing the step and broken by a jump, the aimed shot's quicker wait,
    /// stopped at walls, marking; a marked enemy struck again calling the bombs), the staff (at the cursor within 22 tiles,
    /// short of a wall; three bursts and the threads, striking; 24 mana), the dirge soul (its laser from 8 tiles off).
    /// Close-ups abyss_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AbyssTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 黑吼 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            try
            {
                int sounds = 0;
                string[] names =
                {
                    "tk_abyss_ripple", "tk_abyss_rend", "tk_void_snipe_0", "tk_void_hit", "tk_void_aim", "tk_void_mark", "tk_void_missile", "tk_void_blast",
                    "tk_abyss_cast", "tk_abyss_pulse_0", "tk_abyss_pulse_2", "tk_abyss_threads", "tk_dirge_summon", "tk_dirge_laser",
                };
                foreach (var n in names)
                {
                    var clip = Terraria.TerrariaAssets.Sound(n);
                    if (clip != null && clip.length > 0.1f) sounds++;
                }
                Check(sounds == names.Length, $"混音音效 {sounds}/{names.Length}");

                foreach (var t in Abyss.Weapons)
                    for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) inv.Slots[i] = default;
                int given0 = Abyss.DevGiven;
                PlayerData.instance.screamLevel = 2;
                for (float t = Time.realtimeSinceStartup + 4f; Abyss.DevGiven == given0 && Time.realtimeSinceStartup < t;) yield return null;
                int have = Abyss.Weapons.Count(w => inv.Slots.Any(s => !s.IsEmpty && s.Type == w));
                Check(Abyss.DevGiven == given0 + 1 && have == 4, $"吸收深渊尖啸得到四把武器 {have}/4");
                foreach (var w in Abyss.Weapons) Plugin.Log.LogInfo($"[AutoTest] 黑吼 {ItemDb.Get(w).Name}：伤害 {ItemDb.Get(w).Damage}，阶段 {ItemDb.Get(w).Stage}，使用 {ItemDb.Get(w).UseAnimation * 60f:F0} 帧");

                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                var col = hero.GetComponent<BoxCollider2D>();
                Vector2 C() => col.bounds.center;
                const int terrain = 1 << World.ChunkColliders.TerrainLayer;
                float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 40f, terrain); return hit.collider != null ? hit.distance : 40f; }
                float side = Room(1f) >= Room(-1f) ? 1f : -1f;
                HealthManager target = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (!hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == "Crossroads_01" && (target == null || Vector2.Distance(hm.transform.position, C()) < Vector2.Distance(target.transform.position, C()))) target = hm;
                var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
                Check(target != null, $"找到靶子 {(target != null ? target.name : "没有")}");
                var start = C();
                var near = start + Vector2.right * side * 2.6f;
                var mid = start + Vector2.right * side * 4.5f;
                void Hold(Vector2 at)
                {
                    if (target == null) return;
                    target.hp = Mathf.Max(target.hp, 9000);
                    Pin(target, trb, at);
                }
                mod.SetInfiniteItems(false);
                const int slot = Inventory.HotbarSize - 1;
                void Take(ItemType t) { inv.Slots[slot] = ItemStack.Of(t); inv.Selected = slot; }
                var tp = TerraProjectiles.Instance;

                // ---- the yoyo: its reach first, then a whole life on the enemy.
                Take(ItemType.AbyssYoyo);
                var far = start + Vector2.right * side * 20f;
                mod.DevAim = far;
                mod.DevHold = true;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(near + Vector2.up * 6f); yield return null; }
                mod.DevUseSelected(far);
                float reach = 0f;
                for (float t = 0f; t < 1.6f; t += Time.deltaTime) { Hold(near + Vector2.up * 6f); reach = Mathf.Max(reach, tp.DevFarthest(ProjKind.AbyssYoyo, C())); yield return null; }
                mod.DevHold = false;
                for (float t = 0f; t < 2f && tp.DevPos(ProjKind.AbyssYoyo) != null; t += Time.deltaTime) yield return null;
                Check(reach > Abyss.YoyoRangePx * 0.85f && reach < Abyss.YoyoRangePx + 40f, $"悠悠球射程 {reach:F0} px（应约 {Abyss.YoyoRangePx:F0}）");

                int ripples0 = TerraProjectiles.DevAbyssRipples, rends0 = TerraProjectiles.DevAbyssRends, yoyoHits0 = TerraProjectiles.DevHit(ProjKind.AbyssYoyo);
                int rippleHits0 = TerraProjectiles.DevHit(ProjKind.AbyssRipple), rendHits0 = TerraProjectiles.DevHit(ProjKind.AbyssRend);
                mod.DevAim = near;
                mod.DevHold = true;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                mod.DevUseSelected(near);
                float thrown = Time.time, gone = -1f;
                int ripplesFirst = -1, rendsFirst = -1, hitsFirst = -1;
                bool shotRipple = false, shotRend = false;
                for (float t = 0f; t < 14f; t += Time.deltaTime)
                {
                    Hold(near);
                    if (!shotRipple && TerraProjectiles.DevAbyssRipples > ripples0) { shotRipple = true; yield return null; yield return CloseUp(scene, "abyss_yoyo_ripple", hero, 150, 2); }
                    if (!shotRend && TerraProjectiles.DevAbyssRends > rends0) { shotRend = true; yield return null; yield return CloseUp(scene, "abyss_yoyo_rend", hero, 150, 2); }
                    if (ripplesFirst < 0 && Time.time - thrown > 5.8f) { ripplesFirst = TerraProjectiles.DevAbyssRipples - ripples0; rendsFirst = TerraProjectiles.DevAbyssRends - rends0; hitsFirst = TerraProjectiles.DevHit(ProjKind.AbyssYoyo) - yoyoHits0; }
                    if (gone < 0f && Time.time - thrown > 1f && tp.DevPos(ProjKind.AbyssYoyo) == null) gone = Time.time - thrown;
                    if (gone > 0f) break;
                    yield return null;
                }
                mod.DevHold = null;
                int ripples = TerraProjectiles.DevAbyssRipples - ripples0, rends = TerraProjectiles.DevAbyssRends - rends0, yoyoHits = TerraProjectiles.DevHit(ProjKind.AbyssYoyo) - yoyoHits0;
                Check(gone > 11.5f && gone < 13.8f, $"悠悠球持续 {gone:F1} 秒（应约 12，命中不缩短）");
                Check(ripplesFirst >= 3 && ripplesFirst <= 13 && rendsFirst == 0 && TerraProjectiles.DevHit(ProjKind.AbyssRipple) > rippleHits0,
                    $"前 6 秒：波纹 {ripplesFirst} 次（每 0.5 秒最多一次），撕裂 {rendsFirst}，波纹打中 {TerraProjectiles.DevHit(ProjKind.AbyssRipple) - rippleHits0}");
                Check(rends - rendsFirst >= 5 && ripples - ripplesFirst <= 1 && TerraProjectiles.DevHit(ProjKind.AbyssRend) > rendHits0,
                    $"后 6 秒：撕裂 {rends - rendsFirst} 次，波纹 {ripples - ripplesFirst}，撕裂打中 {TerraProjectiles.DevHit(ProjKind.AbyssRend) - rendHits0}");
                Check(yoyoHits <= gone * 5f + 2f && hitsFirst > 0, $"同一敌人每 0.2 秒最多挨一下：{gone:F1} 秒打中 {yoyoHits} 下");

                // ---- the sniper.
                Take(ItemType.VoidSniper);
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 60);
                mod.DevAim = mid;
                mod.DevAltHold = false;
                for (int k = 0; k < 3; k++) yield return null;
                int snipeHits0 = TerraProjectiles.DevHit(ProjKind.VoidSnipe);
                while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                mod.DevUseSelected(mid);
                float shotAt = Time.time;
                for (int k = 0; k < 3; k++) { Hold(mid); yield return null; }
                bool markedPlain = tp.DevVoidMarked(target);
                while (!mod.DevReady) { Hold(mid); yield return null; }
                float plainWait = Time.time - shotAt;
                Check(TerraProjectiles.DevHit(ProjKind.VoidSnipe) == snipeHits0 + 1 && !markedPlain && plainWait > 1.0f && plainWait < 1.25f,
                    $"普通狙击打中 {TerraProjectiles.DevHit(ProjKind.VoidSnipe) - snipeHits0}，不留标记（{markedPlain}），间隔 {plainWait:F2} 秒");
                yield return CloseUp(scene, "abyss_sniper_plain", hero, 200, 1);

                // Aiming: the step halved; a jump breaks it off until the button comes up.
                mod.DevAltHold = true;
                for (int k = 0; k < 3; k++) { Hold(mid); yield return null; }
                bool aimingNow = Abyss.Aiming;
                float slow = Abyss.MoveSpeedMult;
                Player.TerraMovement.DevJump = true;
                for (int k = 0; k < 2; k++) yield return null;
                Player.TerraMovement.DevJump = false;
                for (int k = 0; k < 2; k++) yield return null;
                bool brokenByJump = !Abyss.Aiming;
                mod.DevAltHold = false;
                for (int k = 0; k < 2; k++) yield return null;
                mod.DevAltHold = true;
                for (int k = 0; k < 2; k++) yield return null;
                Check(aimingNow && slow == Abyss.AimSlow && brokenByJump && Abyss.Aiming, $"按住右键瞄准（移速 ×{slow}），跳跃打断（{brokenByJump}），松开再按又能瞄准（{Abyss.Aiming}）");
                hero.transform.position = Clear(hero, start);
                for (int k = 0; k < 20; k++) { Hold(mid); yield return null; }

                // The aimed shot: sooner, marking; the next aimed one calls the bombs.
                int aimed0 = TerraProjectiles.DevAimedSnipes, strikes0 = TerraProjectiles.DevVoidStrikes, bombs0 = TerraProjectiles.DevVoidBombs, blastHits0 = TerraProjectiles.DevShadeBlastHits;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                mod.DevUseSelected(mid);
                shotAt = Time.time;
                for (int k = 0; k < 3; k++) { Hold(mid); yield return null; }
                bool marked = tp.DevVoidMarked(target);
                yield return CloseUp(scene, "abyss_sniper_mark", hero, 200, 1);
                while (!mod.DevReady) { Hold(mid); yield return null; }
                float aimedWait = Time.time - shotAt;
                Check(TerraProjectiles.DevAimedSnipes == aimed0 + 1 && marked && aimedWait > 0.7f && aimedWait < 0.95f, $"瞄准射击打中并标记（{marked}），间隔 {aimedWait:F2} 秒");
                mod.DevUseSelected(mid);
                bool shotBombs = false;
                for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                {
                    Hold(mid);
                    if (!shotBombs && TerraProjectiles.DevVoidBombs > bombs0 && t > 0.3f) { shotBombs = true; Shot(scene, "abyss_sniper_bombs"); }
                    yield return null;
                }
                Check(TerraProjectiles.DevVoidStrikes == strikes0 + 1 && TerraProjectiles.DevVoidBombs - bombs0 == Abyss.Bombs && TerraProjectiles.DevShadeBlastHits > blastHits0 && !tp.DevVoidMarked(target),
                    $"再次射中标记敌人召来炸弹 {TerraProjectiles.DevVoidBombs - bombs0} 枚，炸中 {TerraProjectiles.DevShadeBlastHits - blastHits0}，标记用掉、这一发不再标记（{!tp.DevVoidMarked(target)}）");
                // The next aimed shot marks again; then a plain shot on the marked one: the bombs too, the mark spent.
                while (!mod.DevReady) { Hold(mid); yield return null; }
                mod.DevUseSelected(mid);
                for (int k = 0; k < 3; k++) { Hold(mid); yield return null; }
                Check(tp.DevVoidMarked(target), $"下一发瞄准弹重新标记（{tp.DevVoidMarked(target)}）");
                mod.DevAltHold = false;
                for (int k = 0; k < 3; k++) yield return null;
                strikes0 = TerraProjectiles.DevVoidStrikes;
                while (!mod.DevReady) { Hold(mid); yield return null; }
                mod.DevUseSelected(mid);
                for (int k = 0; k < 4; k++) { Hold(mid); yield return null; }
                Check(TerraProjectiles.DevVoidStrikes == strikes0 + 1 && !tp.DevVoidMarked(target), $"普通弹射中标记敌人也召来炸弹（{TerraProjectiles.DevVoidStrikes - strikes0}），标记用掉（{!tp.DevVoidMarked(target)}）");
                for (float t = 0f; t < 2f; t += Time.deltaTime) { Hold(mid); yield return null; }
                // Walls: the plain shot through the wall behind, the aimed one stopped at it.
                var behind = start - Vector2.right * side * 30f;
                float wallAt = Room(-side);
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(behind);
                yield return null;
                var plainEnd = tp.DevPos(ProjKind.VoidTracer);
                mod.DevAltHold = true;
                for (int k = 0; k < 3; k++) yield return null;
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(behind);
                yield return null;
                var aimedEnd = tp.DevPos(ProjKind.VoidTracer);
                mod.DevAltHold = false;
                float plainLen = plainEnd != null ? Vector2.Distance(plainEnd.Value, start) : -1f, aimedLen = aimedEnd != null ? Vector2.Distance(aimedEnd.Value, start) : -1f;
                Check(plainLen > wallAt + 2f && aimedLen > 0f && aimedLen < wallAt + 1f, $"普通弹穿墙（{plainLen:F1}），瞄准弹停在墙上（{aimedLen:F1}，墙在 {wallAt:F1}）");

                // ---- the staff.
                Take(ItemType.AbyssStaff);
                mod.Stats.Mana = mod.Stats.ManaMax;
                mod.DevAim = mid;
                int casts0 = TerraProjectiles.DevAbyssCasts, pulses0 = TerraProjectiles.DevAbyssPulses, threads0 = TerraProjectiles.DevAbyssThreads;
                int pulseHits0 = TerraProjectiles.DevHit(ProjKind.AbyssPulse), threadHits0 = TerraProjectiles.DevHit(ProjKind.AbyssThread);
                while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                int mana0 = mod.Stats.Mana;
                mod.DevUseSelected(mid);
                int manaSpent = mana0 - mod.Stats.Mana;
                yield return null;
                var castAt = tp.DevTriadAt();
                bool shotTriad = false;
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    Hold(mid);
                    if (!shotTriad && TerraProjectiles.DevAbyssPulses - pulses0 >= 2) { shotTriad = true; yield return CloseUp(scene, "abyss_staff_burst", hero, 200, 1); }
                    yield return null;
                }
                Check(TerraProjectiles.DevAbyssCasts == casts0 + 1 && castAt != null && Vector2.Distance(castAt.Value, mid) < 0.4f && manaSpent == Mathf.Min(mana0, Equipment.ManaCost(ItemDb.Get(ItemType.AbyssStaff))),
                    $"在光标处唤出三魂（离光标 {(castAt != null ? Vector2.Distance(castAt.Value, mid) : -1f):F2}），耗魔 {manaSpent}");
                Check(TerraProjectiles.DevAbyssPulses - pulses0 == Abyss.Pulses && TerraProjectiles.DevHit(ProjKind.AbyssPulse) - pulseHits0 == Abyss.Pulses,
                    $"爆发 {TerraProjectiles.DevAbyssPulses - pulses0} 次，打中 {TerraProjectiles.DevHit(ProjKind.AbyssPulse) - pulseHits0} 次");
                Check(TerraProjectiles.DevAbyssThreads - threads0 == Abyss.Threads && TerraProjectiles.DevHit(ProjKind.AbyssThread) - threadHits0 >= 3,
                    $"散成黑丝 {TerraProjectiles.DevAbyssThreads - threads0} 道，打中 {TerraProjectiles.DevHit(ProjKind.AbyssThread) - threadHits0} 次");
                // Too far: at 22 tiles; behind a wall: short of it.
                mod.Stats.Mana = mod.Stats.ManaMax;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(start + Vector2.right * side * 40f);
                yield return null;
                var farAt = tp.DevTriadAt();
                float farDist = farAt != null ? Vector2.Distance(farAt.Value, start) : -1f;
                for (float t = 0f; t < 1f; t += Time.deltaTime) yield return null;
                mod.Stats.Mana = mod.Stats.ManaMax;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(behind);
                yield return null;
                var wallCast = tp.DevTriadAt();
                float wallDist = wallCast != null ? Vector2.Distance(wallCast.Value, start) : -1f;
                Check(farDist > 0f && farDist <= Abyss.CastReachPx * Effects.TerraParticles.Px + 0.3f && wallDist > 0f && wallDist <= wallAt + 0.1f,
                    $"最远 22 格（{farDist:F1}，上限 {Abyss.CastReachPx * Effects.TerraParticles.Px:F1}），墙后的光标停在墙前（{wallDist:F1}，墙在 {wallAt:F1}）");
                for (float t = 0f; t < 1f; t += Time.deltaTime) yield return null;

                // ---- the dirge soul.
                Take(ItemType.DirgeMask);
                MinionEngage.DevAlways = true;
                int dShots0 = Minions.DevDirgeShots, lasers0 = TerraProjectiles.DevDirgeLasers;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(C() + Vector2.up * 1.5f);
                bool shotLaser = false;
                float shotFrom = -1f;
                for (float t = 0f; t < 7f; t += Time.deltaTime)
                {
                    Hold(mid);
                    var d = Minions.Instance?.DevDirge();
                    if (d != null && d.Value.state == 2 && shotFrom < 0f) shotFrom = Vector2.Distance(d.Value.at, target.transform.position);
                    if (!shotLaser && d != null && d.Value.state == 2) { shotLaser = true; for (int k = 0; k < 6; k++) { Hold(mid); yield return null; } Shot(scene, "abyss_dirge_laser"); }
                    yield return null;
                }
                Check(Minions.Instance != null && Minions.Instance.DevCount(Minions.Kind.DirgeSoul) >= 1, $"召唤小冥魂 {Minions.Instance?.DevCount(Minions.Kind.DirgeSoul)}");
                Check(Minions.DevDirgeShots > dShots0 && TerraProjectiles.DevDirgeLasers - lasers0 >= 3 && shotFrom > 2.5f && shotFrom < 6f,
                    $"小冥魂从 {shotFrom:F1} 单位外射激光 {Minions.DevDirgeShots - dShots0} 次，打中 {TerraProjectiles.DevDirgeLasers - lasers0} 次");
            }
            finally
            {
                mod.DevAim = null;
                mod.DevHold = null;
                mod.DevAltHold = null;
                Player.TerraMovement.DevJump = false;
                MinionEngage.DevAlways = false;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 黑吼 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
