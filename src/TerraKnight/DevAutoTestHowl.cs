using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "howl"): the Howling Wraiths' weapons (Items/Howl.cs). Their sounds mixed from Terraria's; the reward
    /// (screamLevel 1: the four in the bag, once); then in a room with an enemy held in front: the bell (out, bouncing with
    /// shocks, back and caught; no second throw while it is out; caught on the way when the player meets it), the organ
    /// (three roars for 1 + 2 + 3 rounds; the third's pieces live on through the enemy; turned back off a wall; cut short
    /// without the rounds), the wraiths (called short of the cursor, their beams striking), the choir soul (shouting on the
    /// enemy; following its owner off along Dirtmouth's street). Close-ups howl_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HowlTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 白吼 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            try
            {
                int sounds = 0;
                string[] names = { "tk_bell_throw", "tk_bell_toll_0", "tk_bell_catch", "tk_organ_roar_0", "tk_organ_roar_2", "tk_organ_charge", "tk_wraiths_appear", "tk_wraiths_shriek", "tk_wraith_beam", "tk_choir_summon", "tk_choir_shout" };
                foreach (var n in names)
                {
                    var clip = Terraria.TerrariaAssets.Sound(n);
                    if (clip != null && clip.length > 0.1f) sounds++;
                }
                Check(sounds == names.Length, $"混音音效 {sounds}/{names.Length}");

                foreach (var t in Howl.Weapons)
                    for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) inv.Slots[i] = default;
                int given0 = Howl.DevGiven;
                PlayerData.instance.screamLevel = 1;
                for (float t = Time.realtimeSinceStartup + 4f; Howl.DevGiven == given0 && Time.realtimeSinceStartup < t;) yield return null;
                int have = Howl.Weapons.Count(w => inv.Slots.Any(s => !s.IsEmpty && s.Type == w));
                Check(Howl.DevGiven == given0 + 1 && have == 4, $"吸收嚎叫幽灵得到四把武器 {have}/4");
                foreach (var w in Howl.Weapons) Plugin.Log.LogInfo($"[AutoTest] 白吼 {ItemDb.Get(w).Name}：伤害 {ItemDb.Get(w).Damage}，阶段 {ItemDb.Get(w).Stage}，使用 {ItemDb.Get(w).UseAnimation * 60f:F0} 帧");

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
                var near = start + Vector2.right * side * 2.6f;
                var far = start + Vector2.right * side * 6f;
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

                // ---- the bell: out, three bounces with shocks, back, caught.
                Take(ItemType.WailingDeathbell);
                int throws0 = TerraProjectiles.DevBellThrows, impacts0 = TerraProjectiles.DevBellImpacts, catches0 = TerraProjectiles.DevBellCatches, bellHit0 = TerraProjectiles.DevHit(ProjKind.DeathBell);
                mod.DevAim = near;
                mod.DevHold = true;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                mod.DevUseSelected(near);
                bool shotOut = false, shotShock = false, secondTried = false, thirdTried = false;
                int throwsWhileOut = 0, thirdWhileOut = -1;
                for (float t = 0f; t < 6f && TerraProjectiles.DevBellCatches < catches0 + 2; t += Time.deltaTime)
                {
                    Hold(near);
                    if (!shotOut && t > 0.08f) { shotOut = true; yield return CloseUp(scene, "howl_bell_out", hero, 120, 2); }
                    if (!shotShock && TerraProjectiles.DevBellImpacts > impacts0) { shotShock = true; yield return null; yield return CloseUp(scene, "howl_bell_shock", hero, 130, 2); }
                    if (!secondTried && t > 0.3f)
                    {
                        secondTried = true;
                        int before = TerraProjectiles.DevBellThrows;
                        mod.DevUseSelected(near);
                        throwsWhileOut = TerraProjectiles.DevBellThrows - before;
                    }
                    if (!thirdTried && t > 0.36f)
                    {
                        thirdTried = true;
                        int before = TerraProjectiles.DevBellThrows;
                        mod.DevUseSelected(near);
                        thirdWhileOut = TerraProjectiles.DevBellThrows - before;
                    }
                    yield return null;
                }
                Check(TerraProjectiles.DevBellThrows == throws0 + 2 && throwsWhileOut == 1 && thirdWhileOut == 0,
                    $"最多同时扔出两口丧钟（第二口 {throwsWhileOut}，第三口 {thirdWhileOut}）");
                Check(TerraProjectiles.DevBellImpacts - impacts0 >= Howl.BellBounces && TerraProjectiles.DevHit(ProjKind.DeathBell) > bellHit0,
                    $"弹跳 {TerraProjectiles.DevBellImpacts - impacts0} 次（每口 {Howl.BellBounces}），钟打中靶子 {TerraProjectiles.DevHit(ProjKind.DeathBell) - bellHit0} 次，震击打中 {TerraProjectiles.DevBellShockHits}");
                Check(TerraProjectiles.DevBellCatches == catches0 + 2, $"两口都弹完飞回手中（接住 {TerraProjectiles.DevBellCatches - catches0}）");

                // Caught on the way: the player meets it after its first bounce, and throws again at once.
                int early0 = TerraProjectiles.DevBellEarlyCatches;
                impacts0 = TerraProjectiles.DevBellImpacts;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(near); yield return null; }
                mod.DevUseSelected(near);
                for (float t = 0f; t < 3f && TerraProjectiles.DevBellImpacts == impacts0; t += Time.deltaTime) { Hold(near); yield return null; }
                for (int k = 0; k < 3; k++) yield return null;
                var bellAt = tp?.DevPos(ProjKind.DeathBell);
                // The player's middle onto the bell.
                if (bellAt != null) hero.transform.position += (Vector3)(bellAt.Value - C());
                for (int k = 0; k < 4; k++) yield return null;
                int throws1 = TerraProjectiles.DevBellThrows;
                mod.DevUseSelected(near);
                Check(TerraProjectiles.DevBellEarlyCatches == early0 + 1 && TerraProjectiles.DevBellThrows == throws1 + 1,
                    $"弹跳途中回到身上就接住（中途接住 {TerraProjectiles.DevBellEarlyCatches - early0}），马上又扔出 {TerraProjectiles.DevBellThrows - throws1}");
                for (float t = 0f; t < 5f && tp != null && tp.DevPos(ProjKind.DeathBell) != null; t += Time.deltaTime) { Hold(near); yield return null; }
                hero.transform.position = Clear(hero, start);
                for (int k = 0; k < 10; k++) yield return null;

                // Let go of the button: back at once, before its bounces are done.
                int recalls0 = TerraProjectiles.DevBellRecalls, impacts1 = TerraProjectiles.DevBellImpacts, catches1 = TerraProjectiles.DevBellCatches;
                var upward = start - Vector2.right * side * 0.2f + Vector2.up * 3f;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(far); yield return null; }
                mod.DevUseSelected(upward);
                for (float t = 0f; t < 0.12f; t += Time.deltaTime) { Hold(far); yield return null; }
                mod.DevHold = false;
                for (float t = 0f; t < 2f && TerraProjectiles.DevBellCatches == catches1; t += Time.deltaTime) { Hold(far); yield return null; }
                Check(TerraProjectiles.DevBellRecalls == recalls0 + 1 && TerraProjectiles.DevBellCatches == catches1 + 1 && TerraProjectiles.DevBellImpacts - impacts1 < Howl.BellBounces,
                    $"松开左键钟立刻飞回（收回 {TerraProjectiles.DevBellRecalls - recalls0}，飞回前弹了 {TerraProjectiles.DevBellImpacts - impacts1} 次，接住 {TerraProjectiles.DevBellCatches - catches1}）");
                mod.DevHold = null;

                // ---- the organ: three roars, 1 + 2 + 3 rounds; the third pierces.
                Take(ItemType.GhostPipeOrgan);
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 30);
                int Rounds() => inv.Slots[Inventory.Size - 1].IsEmpty ? 0 : inv.Slots[Inventory.Size - 1].Count;
                int roars0 = TerraProjectiles.DevRoars, pieces0 = TerraProjectiles.DevMade(ProjKind.ShriekWave), waveHits0 = TerraProjectiles.DevHit(ProjKind.ShriekWave);
                mod.DevAim = far;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(far); yield return null; }
                mod.DevUseSelected(far);
                bool shot1 = false, shot3 = false;
                int thirdAlive = -1;
                float thirdAt = -1f;
                for (float t = 0f; t < 1.6f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (!shot1 && t > 0.1f) { shot1 = true; yield return CloseUp(scene, "howl_organ_roar", hero, 150, 2); }
                    if (thirdAt < 0f && TerraProjectiles.DevRoars - roars0 >= 3) thirdAt = t;
                    if (!shot3 && thirdAt >= 0f && t > thirdAt + 0.12f) { shot3 = true; Shot(scene, "howl_organ_third_full"); }
                    if (thirdAlive < 0 && thirdAt >= 0f && t > thirdAt + 0.3f && tp != null) thirdAlive = tp.DevRoarAlive(2);
                    yield return null;
                }
                Check(TerraProjectiles.DevRoars - roars0 == 3 && Rounds() == 30 - 6, $"一次吼三声：{TerraProjectiles.DevRoars - roars0} 声，子弹 30→{Rounds()}（1+2+3）");
                Check(TerraProjectiles.DevMade(ProjKind.ShriekWave) - pieces0 == 23 && TerraProjectiles.DevHit(ProjKind.ShriekWave) - waveHits0 >= 3,
                    $"声浪 {TerraProjectiles.DevMade(ProjKind.ShriekWave) - pieces0} 段，打中靶子 {TerraProjectiles.DevHit(ProjKind.ShriekWave) - waveHits0} 次（每声一次）");
                Check(thirdAlive == 9, $"第三声穿过敌人：0.3 秒后还剩 {thirdAlive}/9 段");

                // Off a wall: the short side of the room.
                int reflect0 = TerraProjectiles.DevReflections;
                var wallward = start - Vector2.right * side * 5f;
                mod.DevAim = wallward;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(far); yield return null; }
                mod.DevUseSelected(wallward);
                for (float t = 0f; t < 1.4f; t += Time.deltaTime) { Hold(far); yield return null; }
                Check(TerraProjectiles.DevReflections > reflect0, $"声浪撞墙反弹 {TerraProjectiles.DevReflections - reflect0} 段次（离墙 {Room(-side):F1}）");

                // Short of rounds: the burst cut off at the roar it can't pay for.
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 2);
                int short0 = TerraProjectiles.DevRoarShort;
                roars0 = TerraProjectiles.DevRoars;
                mod.DevAim = far;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(far); yield return null; }
                mod.DevUseSelected(far);
                for (float t = 0f; t < 1.2f; t += Time.deltaTime) { Hold(far); yield return null; }
                Check(TerraProjectiles.DevRoarShort == short0 + 1 && TerraProjectiles.DevRoars - roars0 == 1 && Rounds() == 1,
                    $"子弹不够时停下：吼了 {TerraProjectiles.DevRoars - roars0} 声，子弹 2→{Rounds()}");

                // ---- the wraiths: short of the cursor, beams that strike.
                Take(ItemType.HowlingWraithsFocus);
                mod.Stats.Mana = mod.Stats.ManaMax;
                int calls0 = TerraProjectiles.DevWraithCalls, beams0 = TerraProjectiles.DevWraithBeams, beamHits0 = TerraProjectiles.DevHit(ProjKind.WraithBeam);
                mod.DevAim = far;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(far); yield return null; }
                mod.DevUseSelected(far);
                bool shotTriad = false;
                Vector2? triadAt = null;
                for (float t = 0f; t < 2.2f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (triadAt == null && tp != null) triadAt = tp.DevPos(ProjKind.WraithTriad);
                    if (!shotTriad && t > 0.4f) { shotTriad = true; Shot(scene, "howl_wraiths_full"); }
                    yield return null;
                }
                Check(TerraProjectiles.DevWraithCalls == calls0 + 1 && TerraProjectiles.DevWraithBeams - beams0 == Howl.WraithBeams && TerraProjectiles.DevHit(ProjKind.WraithBeam) - beamHits0 >= 3,
                    $"唤出三魂（离玩家 {(triadAt != null ? Vector2.Distance(triadAt.Value, start) : -1f):F1}），魂束 {TerraProjectiles.DevWraithBeams - beams0} 道，打中 {TerraProjectiles.DevHit(ProjKind.WraithBeam) - beamHits0} 次");
                // The cursor inside the wall behind: called right there.
                mod.Stats.Mana = mod.Stats.ManaMax;
                var through = start - Vector2.right * side * 12f;
                Vector2? shortAt = null;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(far); yield return null; }
                mod.DevUseSelected(through);
                for (int k = 0; k < 4 && shortAt == null; k++) { if (tp != null) shortAt = tp.DevPos(ProjKind.WraithTriad); yield return null; }
                float wallAt = Room(-side);
                Check(shortAt != null && Vector2.Distance(shortAt.Value, through) < 0.3f,
                    $"光标在墙里时三魂就出现在光标处（离光标 {(shortAt != null ? Vector2.Distance(shortAt.Value, through) : -1f):F2}，墙在 {wallAt:F1} 外）");
                for (float t = 0f; t < 1.5f; t += Time.deltaTime) { Hold(far); yield return null; }

                // ---- the choir soul: it shouts on the enemy, its pulses striking.
                Take(ItemType.ElegyMask);
                MinionEngage.DevAlways = true;
                int shouts0 = Minions.DevChoirShouts, pulses0 = TerraProjectiles.DevShouts, coneHits0 = TerraProjectiles.DevHit(ProjKind.ChoirShout);
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(C() + Vector2.up * 1.5f);
                bool shotChoir = false;
                for (float t = 0f; t < 6f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (!shotChoir && Minions.DevChoirShouts > shouts0) { shotChoir = true; for (int k = 0; k < 20; k++) { Hold(far); yield return null; } Shot(scene, "howl_choir_shout_full"); }
                    yield return null;
                }
                Check(Minions.Instance != null && Minions.Instance.DevCount(Minions.Kind.ChoirSoul) >= 1, $"召唤小唱魂 {Minions.Instance?.DevCount(Minions.Kind.ChoirSoul)}");
                Check(Minions.DevChoirShouts > shouts0 && TerraProjectiles.DevHit(ProjKind.ChoirShout) > coneHits0,
                    $"小唱魂发出声浪 {Minions.DevChoirShouts - shouts0} 次（{TerraProjectiles.DevShouts - pulses0} 下），打中 {TerraProjectiles.DevHit(ProjKind.ChoirShout) - coneHits0} 次");

                // Following its owner off along Dirtmouth's street.
                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
                hero = HeroController.instance;
                col = hero.GetComponent<BoxCollider2D>();
                if (World.TargetDummy.DevAt != null)
                {
                    var dummy = World.TargetDummy.DevAt.Value;
                    hero.transform.position = Clear(hero, dummy + Vector2.left * 5f + Vector2.up * 0.5f);
                    int townShouts = Minions.DevChoirShouts;
                    for (float t = 0f; t < 3.5f; t += Time.deltaTime) yield return null;
                    bool shouted = Minions.DevChoirShouts > townShouts;
                    var away = Clear(hero, dummy + Vector2.right * 35f + Vector2.up * 0.5f);
                    hero.transform.position = away;
                    float atEnd = -1f;
                    for (float t = 0f; t < 4f; t += Time.deltaTime)
                    {
                        var at = Minions.Instance?.DevChoir();
                        atEnd = at != null ? Vector2.Distance(at.Value.at, C()) : -1f;
                        if (atEnd >= 0f && atEnd < 3f && t > 1f) break;
                        yield return null;
                    }
                    Check(shouted && atEnd >= 0f && atEnd < 3f, $"小唱魂对假人发声，玩家走远后跟回来（最后离玩家 {atEnd:F1}）");
                }
                else Check(false, "德特茅斯没有训练假人");
            }
            finally
            {
                mod.DevAim = null;
                mod.DevHold = null;
                MinionEngage.DevAlways = false;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 白吼 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
