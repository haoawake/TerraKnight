using System.Collections;
using System.Linq;
using TerraKnight.Effects;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shade"): the Shade Soul's weapons (Items/Shade.cs). Their sounds mixed from Terraria's; the reward
    /// (fireballLevel 2: the four in the bag, once); then in a room with an enemy held in front: the sword (chop, sweep,
    /// throw; the throw striking again and again and caught; no cut while it is out; let go, the combo back to the chop
    /// and a thrown sword gone), the rifle (the ray past the enemy to the wall, a mark a ray up to five, the right button
    /// bursting them, no round spent), the staff (the ghost at the cursor with shocks closing on it and striking, mana
    /// paid as it goes, no second ghost while it is out, back and caught when let go; the right click to it), the little
    /// shade (its charge turning into the wave that strikes). Close-ups shade_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Dev (suite "dustpeek"): a row of clusters of some of Terraria's dusts above the player, still, for a close-up of their colours.</summary>
        IEnumerator DustPeekTest(string scene, HeroController hero)
        {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            var c = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            int[] types = { 54, 27, 14, 109, 191, 240, 37, 65, 21, 62 };
            for (int f = 0; f < 3; f++)
            {
                for (int i = 0; i < types.Length; i++)
                    for (int k = 0; k < 6; k++)
                    {
                        var at = c + new Vector2(-180f + i * 40f, -70f) + Random.insideUnitCircle * 8f;
                        int d = TerraParticles.Instance.NewDust(at, 4, 4, types[i], 0f, 0f, 0, 1.5f);
                        if (d >= 0) { TerraParticles.Instance[d].NoGravity = true; TerraParticles.Instance[d].Velocity = Vector2.zero; }
                    }
                yield return null;
            }
            Plugin.Log.LogInfo("[AutoTest] 尘埃 从左到右 " + string.Join(", ", types));
            yield return CloseUp(scene, "dustpeek", hero, 220, 2);
        }

        IEnumerator ShadeTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 黑波 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            try
            {
                int sounds = 0;
                string[] names =
                {
                    "tk_shade_swing_0", "tk_shade_swing_1", "tk_shade_throw", "tk_shade_spin", "tk_shade_hit_0", "tk_shade_catch", "tk_rift_ray_0", "tk_rift_burst",
                    "tk_shade_cast", "tk_shade_shock", "tk_shade_return", "tk_shade_blink", "tk_shade_blink_charge", "tk_shade_summon", "tk_shade_dash", "tk_shade_wave",
                };
                foreach (var n in names)
                {
                    var clip = Terraria.TerrariaAssets.Sound(n);
                    if (clip != null && clip.length > 0.1f) sounds++;
                }
                Check(sounds == names.Length, $"混音音效 {sounds}/{names.Length}");
                Terraria.SoundMix.DevDump(System.IO.Path.Combine(DevCopy.SaveDir, "sounds"));

                foreach (var t in Shade.Weapons)
                    for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) inv.Slots[i] = default;
                int given0 = Shade.DevGiven;
                PlayerData.instance.fireballLevel = 2;
                for (float t = Time.realtimeSinceStartup + 4f; Shade.DevGiven == given0 && Time.realtimeSinceStartup < t;) yield return null;
                int have = Shade.Weapons.Count(w => inv.Slots.Any(s => !s.IsEmpty && s.Type == w));
                Check(Shade.DevGiven == given0 + 1 && have == 4, $"吸收暗影之魂得到四把武器 {have}/4");
                foreach (var w in Shade.Weapons) Plugin.Log.LogInfo($"[AutoTest] 黑波 {ItemDb.Get(w).Name}：伤害 {ItemDb.Get(w).Damage}，阶段 {ItemDb.Get(w).Stage}，使用 {ItemDb.Get(w).UseAnimation * 60f:F0} 帧");

                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                var col = hero.GetComponent<BoxCollider2D>();
                Vector2 C() => col.bounds.center;
                const int terrain = 1 << World.ChunkColliders.TerrainLayer;
                float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
                float side = Room(1f) >= Room(-1f) ? 1f : -1f;
                HealthManager target = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (!hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == "Crossroads_01" && (target == null || Vector2.Distance(hm.transform.position, C()) < Vector2.Distance(target.transform.position, C()))) target = hm;
                var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
                Check(target != null, $"找到靶子 {(target != null ? target.name : "没有")}");
                var start = C();
                var near = start + Vector2.right * side * 1.6f;
                var mid = start + Vector2.right * side * 4.5f;
                void Hold(Vector2 at)
                {
                    if (target == null) return;
                    target.hp = Mathf.Max(target.hp, 3000);
                    Pin(target, trb, at);
                }
                mod.SetInfiniteItems(false);
                const int slot = Inventory.HotbarSize - 1;
                void Take(ItemType t) { inv.Slots[slot] = ItemStack.Of(t); inv.Selected = slot; }
                var tp = TerraProjectiles.Instance;

                // ---- the sword: chop, sweep, fling (its thrown shadow).
                Take(ItemType.ShadeEdge);
                mod.DevAim = near;
                mod.DevHold = true;
                Shade.Combo = 0;
                int cuts0 = TerraProjectiles.DevShadeCuts, throws0 = TerraProjectiles.DevShadeThrows, fades0 = TerraProjectiles.DevShadeFades;
                int swingHits0 = TerraProjectiles.DevHit(ProjKind.ShadeSwing), spinHits0 = TerraProjectiles.DevHit(ProjKind.ShadeThrow);
                var cutsSeen = new System.Collections.Generic.List<int>();
                for (int c = 0; c < 3; c++)
                {
                    while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                    cutsSeen.Add(Shade.Combo);
                    mod.DevUseSelected(near);
                    for (int k = 0; k < 7; k++) { Hold(near); yield return null; }
                    yield return CloseUp(scene, "shade_edge_cut" + c, hero, 110, 2);
                }
                for (float w = 0f; w < 1f && (TerraProjectiles.DevShadeThrows == throws0 || mod.IsUsingItem); w += Time.deltaTime) { Hold(near); yield return null; }
                Check(string.Join(",", cutsSeen) == "0,1,2" && TerraProjectiles.DevShadeCuts - cuts0 == 3 && TerraProjectiles.DevShadeThrows == throws0 + 1,
                    $"连斩三刀 {string.Join(",", cutsSeen)}，第三刀掷出剑影 {TerraProjectiles.DevShadeThrows - throws0}");
                Check(TerraProjectiles.DevHit(ProjKind.ShadeSwing) - swingHits0 >= 3, $"三刀都打中靶子 {TerraProjectiles.DevHit(ProjKind.ShadeSwing) - swingHits0} 次");
                // The shadow out: the sword swings on, the next cut the chop.
                while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                bool stillOut = tp != null && tp.DevPos(ProjKind.ShadeThrow) != null;
                int nextCut = Shade.Combo, cutsBefore = TerraProjectiles.DevShadeCuts;
                mod.DevUseSelected(near);
                int cutWhileOut = TerraProjectiles.DevShadeCuts - cutsBefore;
                Check(stillOut && nextCut == 0 && cutWhileOut == 1, $"剑影在外时照样挥剑（剑影在外 {stillOut}，下一刀是第 {nextCut + 1} 刀，砍出 {cutWhileOut}）");
                bool shotSpin = false;
                for (float t = 0f; t < 2f && TerraProjectiles.DevShadeFades == fades0; t += Time.deltaTime)
                {
                    Hold(near);
                    if (!shotSpin) { shotSpin = true; Shot(scene, "shade_edge_throw"); }
                    yield return null;
                }
                Check(TerraProjectiles.DevHit(ProjKind.ShadeThrow) - spinHits0 >= 2 && TerraProjectiles.DevShadeFades == fades0 + 1 && tp.DevPos(ProjKind.ShadeThrow) == null,
                    $"剑影旋转打中 {TerraProjectiles.DevHit(ProjKind.ShadeThrow) - spinHits0} 次后消散（消散 {TerraProjectiles.DevShadeFades - fades0}），不飞回");

                // Let go: the next cut is the chop; a thrown shadow flies on.
                while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                Shade.Combo = 1;
                mod.DevHold = false;
                for (int k = 0; k < 3; k++) yield return null;
                int afterLetGo = Shade.Combo;
                mod.DevHold = true;
                for (int c = 0; c < 3; c++)
                {
                    while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                    mod.DevUseSelected(near);
                }
                for (float w = 0f; w < 1f && tp.DevPos(ProjKind.ShadeThrow) == null; w += Time.deltaTime) { Hold(near); yield return null; }
                mod.DevHold = false;
                for (int k = 0; k < 3; k++) yield return null;
                bool flewOn = tp != null && tp.DevPos(ProjKind.ShadeThrow) != null;
                Check(afterLetGo == 0 && flewOn && Shade.Combo == 0, $"松开左键回到第一刀（{afterLetGo}），已掷出的剑影照样飞完（{flewOn}）");
                mod.DevHold = null;
                for (float t = 0f; t < 1.5f && tp.DevPos(ProjKind.ShadeThrow) != null; t += Time.deltaTime) yield return null;

                // ---- the rifle: the ray through to the wall, marks, the burst.
                Take(ItemType.ShadeRiftRifle);
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 30);
                int Rounds() => inv.Slots[Inventory.Size - 1].IsEmpty ? 0 : inv.Slots[Inventory.Size - 1].Count;
                mod.DevAim = mid;
                int rays0 = TerraProjectiles.DevRays, rayHits0 = TerraProjectiles.DevHit(ProjKind.ShadeRayHit);
                Vector2? rayEnd = null;
                for (int r = 0; r < 3; r++)
                {
                    while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                    mod.DevUseSelected(mid);
                    if (r == 0) { yield return null; rayEnd = tp?.DevPos(ProjKind.ShadeRay); yield return CloseUp(scene, "shade_rift_ray", hero, 260, 1); }
                }
                for (int k = 0; k < 4; k++) { Hold(mid); yield return null; }
                int marks3 = tp != null && target != null ? tp.DevMarksOn(target) : -1;
                float targetAt = target != null ? Vector2.Distance(target.transform.position, start) : 0f;
                Check(TerraProjectiles.DevRays - rays0 == 3 && TerraProjectiles.DevHit(ProjKind.ShadeRayHit) - rayHits0 >= 3 && Rounds() == 27,
                    $"三道裂光，打中 {TerraProjectiles.DevHit(ProjKind.ShadeRayHit) - rayHits0} 次，子弹 30→{Rounds()}");
                Check(rayEnd != null && Vector2.Distance(rayEnd.Value, start) > targetAt + 0.5f,
                    $"裂光穿过敌人直到墙（终点离玩家 {(rayEnd != null ? Vector2.Distance(rayEnd.Value, start) : -1f):F1}，敌人 {targetAt:F1}）");
                Check(marks3 == 3, $"敌人身上 {marks3} 层印记（应为 3）");
                Shot(scene, "shade_rift_marks");
                for (int r = 0; r < 4; r++)
                {
                    while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                    mod.DevUseSelected(mid);
                }
                for (int k = 0; k < 4; k++) { Hold(mid); yield return null; }
                int marks7 = tp != null && target != null ? tp.DevMarksOn(target) : -1;
                Check(marks7 == Shade.MaxMarks, $"七道之后 {marks7} 层（最多 {Shade.MaxMarks}）");
                int blasts0 = TerraProjectiles.DevShadeBlasts, blastHits0 = TerraProjectiles.DevShadeBlastHits, rounds0 = Rounds();
                while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                mod.DevUseSelected(mid, alt: true);
                for (int k = 0; k < 3; k++) { Hold(mid); yield return null; }
                Shot(scene, "shade_rift_burst");
                for (int k = 0; k < 6; k++) { Hold(mid); yield return null; }
                Check(TerraProjectiles.DevShadeBlasts == blasts0 + 1 && TerraProjectiles.DevShadeBlastHits > blastHits0 && tp.DevMarksOn(target) == 0 && Rounds() == rounds0,
                    $"右键引爆 {TerraProjectiles.DevShadeBlasts - blasts0} 处，炸中 {TerraProjectiles.DevShadeBlastHits - blastHits0}，印记清空 {tp.DevMarksOn(target)}，不耗子弹 {rounds0}→{Rounds()}");

                // ---- the staff: the ghost at the cursor, shocks closing on it, mana as it goes.
                Take(ItemType.ShadeSoulFocus);
                mod.Stats.Mana = mod.Stats.ManaMax;
                mod.DevAim = mid;
                mod.DevHold = true;
                int ghosts0 = TerraProjectiles.DevShadeGhosts, shocks0 = TerraProjectiles.DevShocks, shockHits0 = TerraProjectiles.DevHit(ProjKind.ShadeShock), caught0 = TerraProjectiles.DevGhostCaught;
                int paid0 = TerraProjectiles.DevGhostPaid;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(mid); yield return null; }
                mod.DevUseSelected(mid);
                bool shotGhost = false;
                int secondGhost = -1;
                float ghostOff = -1f;
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    Hold(mid + Vector2.up * 0.3f);
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    if (!shotGhost && t > 0.9f) { shotGhost = true; var g = tp?.DevPos(ProjKind.ShadeGhost); if (g != null) ghostOff = Vector2.Distance(g.Value, mid); Shot(scene, "shade_ghost"); }
                    if (secondGhost < 0 && t > 1f)
                    {
                        int before = TerraProjectiles.DevShadeGhosts;
                        mod.DevUseSelected(mid);
                        secondGhost = TerraProjectiles.DevShadeGhosts - before;
                    }
                    yield return null;
                }
                int paid = TerraProjectiles.DevGhostPaid - paid0;
                Check(TerraProjectiles.DevShadeGhosts == ghosts0 + 1 && secondGhost == 0 && ghostOff >= 0f && ghostOff < 0.8f,
                    $"放出幽灵到光标处（离光标 {ghostOff:F2}），幽灵在外时不能再放（又放 {secondGhost}）");
                Check(TerraProjectiles.DevShocks - shocks0 >= 15 && TerraProjectiles.DevHit(ProjKind.ShadeShock) - shockHits0 >= 6,
                    $"冲击波 {TerraProjectiles.DevShocks - shocks0} 道从四周汇聚，打中 {TerraProjectiles.DevHit(ProjKind.ShadeShock) - shockHits0} 次");
                Check(paid >= 4, $"持续耗蓝：2 秒付了 {paid} 次（每 {Shade.ManaEvery} 帧一次）");
                mod.DevHold = false;
                int returns0 = TerraProjectiles.DevGhostReturns;
                float backIn = -1f;
                for (float t = 0f; t < 5f; t += Time.deltaTime)
                {
                    if (TerraProjectiles.DevGhostCaught > caught0) { backIn = t; break; }
                    yield return null;
                }
                Check(TerraProjectiles.DevGhostReturns == returns0 + 1 && backIn > 0.3f, $"松开后幽灵慢慢飞回，{backIn:F2} 秒回到手中");

                // Out of mana: back by itself.
                mod.Stats.Mana = mod.Stats.ManaMax;
                mod.DevHold = true;
                int noMana0 = TerraProjectiles.DevGhostNoMana, returns1 = TerraProjectiles.DevGhostReturns;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(mid);
                for (int k = 0; k < 10; k++) yield return null;
                mod.Stats.Mana = 0;
                for (float t = 0f; t < 1f && TerraProjectiles.DevGhostReturns == returns1; t += Time.deltaTime) yield return null;
                Check(TerraProjectiles.DevGhostNoMana == noMana0 + 1 && TerraProjectiles.DevGhostReturns == returns1 + 1, "魔力用完幽灵自己飞回");
                mod.DevHold = false;
                for (float t = 0f; t < 5f && tp != null && tp.DevPos(ProjKind.ShadeGhost) != null; t += Time.deltaTime) yield return null;

                // The right click with the ghost out: a short wait with its sound, then to it, all the mana spent.
                mod.Stats.Mana = mod.Stats.ManaMax;
                var spot = start + Vector2.right * side * 3f + Vector2.up * 1.2f;
                mod.DevAim = spot;
                mod.DevHold = true;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(spot);
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) yield return null;
                int blinked0 = TerraProjectiles.DevShadeBlinked;
                var ghostAt = tp?.DevPos(ProjKind.ShadeGhost);
                var from = C();
                float pressed = Time.time;
                mod.DevAltPress = true;
                for (float t = 0f; t < 0.15f; t += Time.deltaTime) yield return null;
                bool waited = TerraProjectiles.DevShadeBlinked == blinked0 && Vector2.Distance(C(), from) < 0.3f;
                for (float t = 0f; t < 1.5f && TerraProjectiles.DevShadeBlinked == blinked0; t += Time.deltaTime) yield return null;
                float delay = Time.time - pressed;
                yield return null;
                mod.DevHold = false;
                Check(waited && TerraProjectiles.DevShadeBlinked == blinked0 + 1 && delay > 0.25f && delay < 0.8f && ghostAt != null && Vector2.Distance(C(), ghostAt.Value) < 1.6f
                      && tp.DevPos(ProjKind.ShadeGhost) == null && mod.Stats.Mana <= 2,
                    $"右键后等 {delay:F2} 秒传送到幽灵身边（离幽灵 {(ghostAt != null ? Vector2.Distance(C(), ghostAt.Value) : -1f):F2}），魔力耗尽（剩 {mod.Stats.Mana}）");
                // Straight again: the ghost sent, the right click refused while the blink cools down (6 s).
                mod.Stats.Mana = mod.Stats.ManaMax;
                mod.DevHold = true;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(spot);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                int cooling0 = TerraProjectiles.DevShadeBlinkCooling, blinked1 = TerraProjectiles.DevShadeBlinked;
                mod.DevAltPress = true;
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) yield return null;
                mod.DevHold = false;
                Check(TerraProjectiles.DevShadeBlinkCooling == cooling0 + 1 && TerraProjectiles.DevShadeBlinked == blinked1,
                    $"传送冷却中再按右键不传送（拒绝 {TerraProjectiles.DevShadeBlinkCooling - cooling0}，传送 {TerraProjectiles.DevShadeBlinked - blinked1}，还剩 {Shade.BlinkReady - Time.time:F1} 秒）");
                for (float t = 0f; t < 4f && tp.DevPos(ProjKind.ShadeGhost) != null; t += Time.deltaTime) yield return null;
                Shade.BlinkReady = 0f;
                mod.DevHold = null;
                hero.transform.position = Clear(hero, start);
                for (int k = 0; k < 10; k++) yield return null;

                // ---- the little shade: the charge, the wave, the strike.
                Take(ItemType.ShadeSoulStaff);
                MinionEngage.DevAlways = true;
                int waves0 = Minions.DevShadeWaves, strikes0 = Minions.DevShadeStrikes;
                // Another enemy held on the way from behind the player's shoulder to the target.
                HealthManager other = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm != target && !hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == "Crossroads_01") { other = hm; break; }
                var orb = other != null ? other.GetComponent<Rigidbody2D>() : null;
                var onWay = start + Vector2.right * side * 1.8f + Vector2.up * 0.8f;
                if (other != null) other.hp = 3000;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(C() + Vector2.up * 1.5f);
                bool shotWave = false;
                for (float t = 0f; t < 6f; t += Time.deltaTime)
                {
                    Hold(mid);
                    if (other != null) Pin(other, orb, onWay);
                    if (!shotWave && Minions.Instance?.DevShade()?.state == 3) { shotWave = true; Shot(scene, "shade_minion_wave"); }
                    yield return null;
                }
                Check(Minions.Instance != null && Minions.Instance.DevCount(Minions.Kind.ShadeSoul) >= 1, $"召唤小暗魂 {Minions.Instance?.DevCount(Minions.Kind.ShadeSoul)}");
                Check(Minions.DevShadeWaves - waves0 >= 2 && Minions.DevShadeStrikes - strikes0 >= 2,
                    $"小暗魂冲刺后化作冲击波 {Minions.DevShadeWaves - waves0} 次，打中 {Minions.DevShadeStrikes - strikes0} 次");
                Check(other == null || other.hp < 3000, $"冲刺路上的另一个敌人也受伤（{(other != null ? other.name + " 剩 " + other.hp : "没有第二个敌人")}）");
            }
            finally
            {
                mod.DevAim = null;
                mod.DevHold = null;
                mod.DevAltPress = false;
                MinionEngage.DevAlways = false;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 黑波 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
