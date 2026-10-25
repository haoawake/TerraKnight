using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "soulwave"): the Vengeful Spirit's weapons (Items/SoulWave.cs). Their sounds mixed from Terraria's
    /// (Terraria/SoundMix.cs; written as WAVs to the copy's save folder, sounds/, to measure); the reward (fireballLevel 1:
    /// the four in the bag, once); then in a room with enemies, one held in front: the sword's swings (the crescent; a wraith
    /// off a blow, nothing shot from the swing), the rifle's lasers (a round each), a charge let go early (nothing spent), a
    /// full one (one wave, five rounds, none more while held; a burst where it lands), the spell's wave (stopped by the
    /// enemy, bursting) and its split (fifteen little wraiths), the staff's soul pouncing and staying on its quarry while the
    /// player walks off; close-ups at each.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SoulWaveTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 白波 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            try
            {
                // The sounds, mixed from Terraria's.
                int sounds = 0;
                foreach (var n in new[] { "tk_soul_swing_0", "tk_pulse_charge", "tk_pulse_wave", "tk_spirit_cast", "tk_soul_summon", "tk_soul_burst", "tk_soul_dash_1" })
                {
                    var clip = Terraria.TerrariaAssets.Sound(n);
                    if (clip != null && clip.length > 0.1f) sounds++;
                }
                int dumped = Terraria.SoundMix.DevDump(System.IO.Path.Combine(DevCopy.SaveDir ?? TerraMod.SaveDir, "sounds"));
                Check(sounds == 7 && dumped >= 22, $"音效 {sounds}/7（扑击用内嵌原音效），混音写出 {dumped} 个（蓄力 {Terraria.TerrariaAssets.Sound("tk_pulse_charge").length:F2}s）");

                // The reward.
                foreach (var t in SoulWave.Weapons)
                    for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) inv.Slots[i] = default;
                int given0 = SoulWave.DevGiven;
                PlayerData.instance.fireballLevel = 1;
                for (float t = Time.realtimeSinceStartup + 4f; SoulWave.DevGiven == given0 && Time.realtimeSinceStartup < t;) yield return null;
                int have = SoulWave.Weapons.Count(w => inv.Slots.Any(s => !s.IsEmpty && s.Type == w));
                Check(SoulWave.DevGiven == given0 + 1 && have == 4, $"吸收复仇之魂得到四把武器 {have}/4");
                yield return new WaitForSeconds(2.5f);
                Check(SoulWave.DevGiven == given0 + 1, "只给一次");
                foreach (var w in SoulWave.Weapons) Plugin.Log.LogInfo($"[AutoTest] 白波 {ItemDb.Get(w).Name}：伤害 {ItemDb.Get(w).Damage}，阶段 {ItemDb.Get(w).Stage}，使用 {ItemDb.Get(w).UseAnimation * 60f:F0} 帧");

                // A room with enemies; the nearest held in front.
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
                var near = start + Vector2.right * side * 2.2f;
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

                // The sword: the crescent with each swing, nothing shot; a wraith off a blow.
                Take(ItemType.RevenantEdge);
                int slash0 = TerraProjectiles.DevSlashes, wraith0 = TerraProjectiles.DevWraiths, blade0 = TerraProjectiles.DevMade(ProjKind.SoulBlade);
                mod.DevAim = near;
                bool shot = false, wraithShot = false;
                int swings = 0;
                float swung = -1f, firstWraith = -1f, firstWraithHit = -1f;
                bool orbitShot = false;
                int wraithHit0 = TerraProjectiles.DevHit(ProjKind.SoulWraith);
                for (float t = 0f; t < 6f && (swung < 0f || t < swung + 1.8f); t += Time.deltaTime)
                {
                    Hold(near);
                    if (firstWraith < 0f && TerraProjectiles.DevWraiths > wraith0) firstWraith = t;
                    if (firstWraithHit < 0f && TerraProjectiles.DevHit(ProjKind.SoulWraith) > wraithHit0) firstWraithHit = t;
                    if (firstWraith >= 0f && !orbitShot && t - firstWraith > 0.5f) { orbitShot = true; Shot(scene, "sw_sword_wraith_orbit_full"); }
                    if (swings < 3 && mod.DevReady && !mod.IsUsingItem) { mod.DevUseSelected(near); swings++; if (swings == 3) swung = t; }
                    if (!shot && swings >= 1 && t > 0.18f) { shot = true; yield return CloseUp(scene, "sw_sword_swing", hero, 110, 2); }
                    if (swings >= 1 && TerraProjectiles.DevWraiths > wraith0 && !wraithShot) { wraithShot = true; yield return null; yield return null; yield return null; yield return null; Shot(scene, "sw_sword_wraith_full"); }
                    yield return null;
                }
                Check(TerraProjectiles.DevSlashes - slash0 >= 3 && TerraProjectiles.DevMade(ProjKind.SoulBlade) == blade0,
                    $"挥剑 {swings} 次：挥砍弧 {TerraProjectiles.DevSlashes - slash0}，挥剑射出 {TerraProjectiles.DevMade(ProjKind.SoulBlade) - blade0}（应为 0）");
                Check(TerraProjectiles.DevWraiths > wraith0, $"命中后从敌人身上飞出怨灵 {TerraProjectiles.DevWraiths - wraith0}（怨灵命中 {TerraProjectiles.DevHit(ProjKind.SoulWraith) - wraithHit0}）");
                // Out, round the enemy once, then the dive: the first strike nearly a second after the wraith appears.
                Check(firstWraith >= 0f && firstWraithHit - firstWraith > 0.8f,
                    $"怨灵先往外飞、绕一圈再打：出现后 {(firstWraithHit >= 0f ? firstWraithHit - firstWraith : -1f):F2} 秒才命中");

                // The rifle: lasers.
                Take(ItemType.RevenantPulseRifle);
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 30);
                int Rounds() => inv.Slots[Inventory.Size - 1].IsEmpty ? 0 : inv.Slots[Inventory.Size - 1].Count;
                int lasers0 = TerraProjectiles.DevLasers, beams0 = TerraProjectiles.DevMade(ProjKind.PulseBeam), blows0 = TerraProjectiles.DevHit(ProjKind.PulseLaser), struck0 = TerraProjectiles.DevBeamStruck;
                mod.DevAim = far;
                int fired = 0;
                shot = false;
                for (float t = 0f; t < 1.4f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (fired < 3 && mod.DevReady && !mod.IsUsingItem) { mod.DevUseSelected(far); fired++; }
                    if (!shot && fired >= 1 && t > 0.03f) { shot = true; yield return CloseUp(scene, "sw_rifle_laser", hero, 120, 2); }
                    yield return null;
                }
                Check(TerraProjectiles.DevLasers - lasers0 == 3 && Rounds() == 27, $"左键激光 {TerraProjectiles.DevLasers - lasers0} 发，子弹 30→{Rounds()}");
                Check(TerraProjectiles.DevMade(ProjKind.PulseBeam) - beams0 == 3 && TerraProjectiles.DevHit(ProjKind.PulseLaser) - blows0 >= 2,
                    $"每发一整条光束 {TerraProjectiles.DevMade(ProjKind.PulseBeam) - beams0} 道，射线碰到敌人 {TerraProjectiles.DevBeamStruck - struck0} 次，击中 {TerraProjectiles.DevHit(ProjKind.PulseLaser) - blows0} 次");

                // A charge let go early.
                int cancel0 = TerraProjectiles.DevCancels, waves0 = TerraProjectiles.DevWaves;
                mod.DevAltHold = true;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(far, alt: true);
                for (float t = 0f; t < 1.0f; t += Time.deltaTime) { Hold(far); if (t > 0.9f && t < 0.95f) yield return CloseUp(scene, "sw_rifle_charge", hero, 110, 3); yield return null; }
                mod.DevAltHold = false;
                for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;
                Check(TerraProjectiles.DevCancels == cancel0 + 1 && TerraProjectiles.DevWaves == waves0 && Rounds() == 27, $"未满 2 秒松开取消：取消 {TerraProjectiles.DevCancels - cancel0}，白波 {TerraProjectiles.DevWaves - waves0}，子弹 {Rounds()}");

                // A full charge, held on past it: one wave, bursting where it lands.
                int blast0 = TerraProjectiles.DevSoulBlasts;
                mod.DevAltHold = true;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(far, alt: true);
                bool near2 = false, after = false, boom = false;
                for (float t = 0f; t < 3.4f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (!near2 && t > 1.85f) { near2 = true; yield return CloseUp(scene, "sw_rifle_full", hero, 110, 3); }
                    if (!after && TerraProjectiles.DevWaves > waves0) { after = true; yield return null; yield return CloseUp(scene, "sw_rifle_wave", hero, 150, 2); }
                    if (!boom && TerraProjectiles.DevSoulBlasts > blast0) { boom = true; yield return null; yield return null; Shot(scene, "sw_rifle_burst_full"); }
                    yield return null;
                }
                int wavesHeld = TerraProjectiles.DevWaves - waves0;
                mod.DevAltHold = false;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                Check(wavesHeld == 1 && Rounds() == 27 - SoulWave.WaveAmmo, $"满 2 秒自动发射一次：白波 {wavesHeld}，子弹 27→{Rounds()}（每发 {SoulWave.WaveAmmo}），命中 {TerraProjectiles.DevHit(ProjKind.ChargedSoulWave)}");
                Check(TerraProjectiles.DevSoulBlasts > blast0, $"蓄力白波命中后爆炸 {TerraProjectiles.DevSoulBlasts - blast0} 次");

                // Not enough rounds: no wave.
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 3);
                int short0 = TerraProjectiles.DevShort;
                waves0 = TerraProjectiles.DevWaves;
                mod.DevAltHold = true;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(far, alt: true);
                for (float t = 0f; t < 2.4f; t += Time.deltaTime) { Hold(far); yield return null; }
                mod.DevAltHold = false;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;
                Check(TerraProjectiles.DevShort == short0 + 1 && TerraProjectiles.DevWaves == waves0 && Rounds() == 3, $"子弹不足不发射：白波 {TerraProjectiles.DevWaves - waves0}，子弹 {Rounds()}");

                // The spell: the wave stopped by the enemy, bursting.
                Take(ItemType.VengefulSpiritFocus);
                mod.Stats.Mana = mod.Stats.ManaMax;
                int spirit0 = TerraProjectiles.DevMade(ProjKind.SpiritWave), sHit0 = TerraProjectiles.DevHit(ProjKind.SpiritWave);
                blast0 = TerraProjectiles.DevSoulBlasts;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevAim = far;
                mod.DevUseSelected(far);
                shot = false;
                bool spellBoom = false, goneAfterHit = false;
                int frame = 0, hitFrame = -1;
                for (float t = 0f; t < 1.4f; t += Time.deltaTime)
                {
                    Hold(far);
                    frame++;
                    if (hitFrame < 0 && TerraProjectiles.DevHit(ProjKind.SpiritWave) > sHit0) hitFrame = frame;
                    if (hitFrame >= 0 && frame == hitFrame + 3) goneAfterHit = TerraProjectiles.Instance != null && TerraProjectiles.Instance.DevCount(ProjKind.SpiritWave) == 0;
                    if (!shot && t > 0.12f) { shot = true; yield return CloseUp(scene, "sw_spirit_cast", hero, 130, 2); }
                    if (!spellBoom && TerraProjectiles.DevSoulBlasts > blast0) { spellBoom = true; yield return null; yield return null; Shot(scene, "sw_spirit_burst_full"); }
                    yield return null;
                }
                Check(TerraProjectiles.DevMade(ProjKind.SpiritWave) - spirit0 == 1 && TerraProjectiles.DevHit(ProjKind.SpiritWave) - sHit0 == 1 && TerraProjectiles.DevSoulBlasts > blast0 && goneAfterHit,
                    $"复仇之魂碰到敌人爆炸：白波 {TerraProjectiles.DevMade(ProjKind.SpiritWave) - spirit0}，命中 {TerraProjectiles.DevHit(ProjKind.SpiritWave) - sHit0}，命中后消失 {goneAfterHit}（不穿透），爆炸 {TerraProjectiles.DevSoulBlasts - blast0}");

                // The split: right-clicked before it lands, fifteen little wraiths that home.
                mod.Stats.Mana = mod.Stats.ManaMax;
                int split0 = TerraProjectiles.DevSplits, little0 = TerraProjectiles.DevSplitWraiths, wHit0 = TerraProjectiles.DevHit(ProjKind.SoulWraith);
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(far);
                shot = false;
                int manaBefore = -1, manaAfter = -1, cost = Equipment.ManaCost(inv.Current.Def);
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (t > 0.1f && TerraProjectiles.DevSplits == split0)
                    {
                        if (manaBefore < 0) manaBefore = (int)mod.Stats.Mana;
                        mod.DevAltPress = true;
                    }
                    if (manaAfter < 0 && TerraProjectiles.DevSplits > split0) manaAfter = (int)mod.Stats.Mana;
                    if (!shot && TerraProjectiles.DevSplits > split0 && t > 0.3f) { shot = true; yield return CloseUp(scene, "sw_spirit_split", hero, 150, 2); }
                    yield return null;
                }
                Check(TerraProjectiles.DevSplits == split0 + 1 && TerraProjectiles.DevSplitWraiths - little0 == SoulWave.SplitCount && TerraProjectiles.DevHit(ProjKind.SoulWraith) > wHit0,
                    $"右键分散：{TerraProjectiles.DevSplitWraiths - little0} 只小怨灵，命中 {TerraProjectiles.DevHit(ProjKind.SoulWraith) - wHit0} 次");
                Check(manaBefore >= 0 && manaAfter >= 0 && manaBefore - manaAfter >= cost - 1, $"右键分散再扣一次魔力：{manaBefore} → {manaAfter}（每次 {cost}）");

                // No mana for it: no split.
                int noMana0 = SoulWave.DevSplitNoMana;
                split0 = TerraProjectiles.DevSplits;
                mod.Stats.Mana = mod.Stats.ManaMax;
                // The enemy off the wave's line, so the wave is still out when the button goes down.
                var aside = start + Vector2.up * 6f;
                while (!mod.DevReady || mod.IsUsingItem) { Hold(aside); yield return null; }
                mod.DevUseSelected(far);
                for (float t = 0f; t < 1f && (TerraProjectiles.Instance == null || TerraProjectiles.Instance.DevCount(ProjKind.SpiritWave) == 0); t += Time.deltaTime) { Hold(aside); yield return null; }
                int wavesOut = TerraProjectiles.Instance != null ? TerraProjectiles.Instance.DevCount(ProjKind.SpiritWave) : 0;
                mod.Stats.Mana = 0;
                mod.DevAltPress = true;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime) { Hold(aside); mod.Stats.Mana = 0; yield return null; }
                Check(wavesOut > 0 && TerraProjectiles.DevSplits == split0 && SoulWave.DevSplitNoMana > noMana0,
                    $"魔力不够时右键不分散（白波 {wavesOut} 道，没分散 {SoulWave.DevSplitNoMana - noMana0} 次）");
                mod.Stats.Mana = mod.Stats.ManaMax;

                // The staff: the soul pounces, and stays on its quarry when the player walks off.
                Take(ItemType.WanderingSoulStaff);
                MinionEngage.DevAlways = true;
                int pounce0 = Minions.DevSoulPounces, strike0 = Minions.DevSoulStrikes;
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(C() + Vector2.up * 1.5f);
                bool pic = false;
                for (float t = 0f; t < 5f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (!pic && Minions.DevSoulPounces > pounce0) { pic = true; yield return null; yield return null; Shot(scene, "sw_soul_pounce_full"); }
                    if (t > 0.4f && t < 0.43f) Shot(scene, "sw_soul_idle_full");
                    yield return null;
                }
                Check(Minions.Instance != null && Minions.Instance.DevCount(Minions.Kind.WanderingSoul) >= 1, $"召唤小怨魂 {Minions.Instance?.DevCount(Minions.Kind.WanderingSoul)}");
                Check(Minions.DevSoulPounces > pounce0 && Minions.DevSoulStrikes > strike0, $"小怨魂扑击 {Minions.DevSoulPounces - pounce0} 次，命中 {Minions.DevSoulStrikes - strike0} 次");
                // On Dirtmouth's long street: the soul at the training dummy, then the owner walks 35 units off; it breaks off and comes back.
                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
                hero = HeroController.instance;
                col = hero.GetComponent<BoxCollider2D>();
                if (World.TargetDummy.DevAt != null)
                {
                    var dummy = World.TargetDummy.DevAt.Value;
                    hero.transform.position = Clear(hero, dummy + Vector2.left * 4f + Vector2.up * 0.5f);
                    int pounceTown = Minions.DevSoulPounces, recalls0 = Minions.DevSoulRecalls;
                    for (float t = 0f; t < 3f; t += Time.deltaTime) yield return null;
                    bool hunted = Minions.DevSoulPounces > pounceTown;
                    var away = Clear(hero, dummy + Vector2.right * 35f + Vector2.up * 0.5f);
                    for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                    {
                        hero.transform.position = Vector3.Lerp(hero.transform.position, away, Mathf.Clamp01(Time.deltaTime * 4f));
                        yield return null;
                    }
                    hero.transform.position = away;
                    float atEnd = -1f;
                    for (float t = 0f; t < 4f; t += Time.deltaTime)
                    {
                        var at = Minions.Instance?.DevSoulAt();
                        atEnd = at != null ? Vector2.Distance(at.Value, C()) : -1f;
                        if (atEnd >= 0f && atEnd < 3f && t > 1f) break;
                        yield return null;
                    }
                    Shot(scene, "sw_soul_back_full");
                    Check(hunted && atEnd >= 0f && atEnd < 3f, $"玩家走远后小怨魂丢下假人跟回来：先扑了假人 {hunted}，最后离玩家 {atEnd:F1}，脱离 {Minions.DevSoulRecalls - recalls0} 次");
                }
                else Check(false, "德特茅斯没有训练假人");
            }
            finally
            {
                mod.DevAltHold = null;
                mod.DevAltPress = false;
                mod.DevAim = null;
                MinionEngage.DevAlways = false;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 白波 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
