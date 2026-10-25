using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "charmcd", "unnfix", "steadyfix", "heartfix", "eaterlook", "quickslash2", "artextras", "focustoggle",
    /// "quickkeys", "charmtexts", "nailfront"): the user's 2026-10-21 third list of charm changes, each tried as it should
    /// work and where it could go wrong, by the Target Dummy in Dirtmouth.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CharmRound3(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            yield return CharmPlace();
            switch (suite)
            {
                case "charmcd": yield return CooldownTextTest(); break;
                case "unnfix": yield return UnnFixTest(); break;
                case "steadyfix": yield return SteadyFixTest(); break;
                case "heartfix": yield return HeartFixTest(); break;
                case "eaterlook": yield return EaterLookTest(); break;
                case "quickslash2": yield return QuickRecoveryTest(); break;
                case "artextras": yield return ArtExtrasTest(); break;
                case "focustoggle": yield return FocusToggleTest(); break;
                case "quickkeys": yield return QuickKeysTest(); break;
                case "charmtexts": yield return CharmTextsTest(); break;
                case "nailfront": yield return NailFrontProbe(); break;
            }
            EndNailTest();
            WearOnly();
            mod.Inventory.Slots[0] = default;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        // ------------------------------------------------------------------ the cooldown's words, the press kept, the shell

        IEnumerator CooldownTextTest()
        {
            var hero = HeroController.instance;
            var st = mod.Stats;
            const int Id = CharmEffects.SteadyBody;
            WearOnly(Id);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + Id);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("护符技开启", CharmEffects.DashPassActive, "");
            int said = CharmSkills.DevCoolingSaid;
            yield return new WaitForSeconds(0.4f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmShot("cooling");
            CharmCheck("冷却中再按：人物旁边飘字“护符技还剩 N 秒”", CharmSkills.DevCoolingSaid == said + 1 && CharmSkills.DevCoolingText == Lang.F("护符技还剩 {0} 秒", 10),
                $"{CharmSkills.DevCoolingText}（{CharmSkills.DevCoolingSaid - said} 次）");
            // Pressed at once again: not a pile of words.
            CharmSkills.DevPress = true;
            yield return null;
            CharmCheck("连按不会刷屏", CharmSkills.DevCoolingSaid == said + 1, $"{CharmSkills.DevCoolingSaid - said} 次");
            yield return new WaitForSeconds(4f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("剩下的秒数跟着变", CharmSkills.DevCoolingText == Lang.F("护符技还剩 {0} 秒", 6), CharmSkills.DevCoolingText);

            // The Gathering Swarm cooling: the same words.
            WearOnly(CharmSkills.GatheringSwarm);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmSkills.GatheringSwarm);
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.35f);
            said = CharmSkills.DevCoolingSaid;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("蜂群集结冷却中也飘字", CharmSkills.DevCoolingSaid == said + 1, CharmSkills.DevCoolingText);

            // A press while the game holds the player: kept a moment, used as control comes back.
            WearOnly(CharmEffects.Stalwart);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Stalwart);
            yield return new WaitForSeconds(0.3f);
            int waits = CharmSkills.DevPressWaits;
            hero.RelinquishControl();
            CharmSkills.DevPress = true;
            yield return null;
            yield return new WaitForSeconds(0.15f);
            bool upWhileHeld = Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.Stalwart);
            hero.RegainControl();
            yield return null;
            yield return null;
            bool upAfter = Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.Stalwart);
            CharmCheck("失去控制时按下，控制回来后照样张开外壳", CharmSkills.DevPressWaits > waits && !upWhileHeld && upAfter, $"等了 {CharmSkills.DevPressWaits - waits} 帧，等的时候 {upWhileHeld}，之后 {upAfter}");
            CharmShot("shell");
            // A second press with nothing taken: the shell stays up.
            int kept = CharmEffects.DevShellKept;
            yield return new WaitForSeconds(0.5f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("没吸收时再按一次，外壳不会被关掉", CharmEffects.DevShellKept == kept + 1 && Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.Stalwart)
                && !Player.PlayerBuffs.Has(CharmSkills.CooldownBase + CharmEffects.Stalwart), "");
            // Something taken: the second press lets the wave go.
            Player.TerraStats.DevImmortal = true;
            pd0().isInvincible = false;
            int life = st.Life, waves = CharmEffects.DevWaves;
            HitFromRight(10);
            yield return new WaitForSeconds(0.3f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("吸收后再按放出光波，进入冷却", CharmEffects.DevWaves == waves + 1 && st.Life == life && Player.PlayerBuffs.Has(CharmSkills.CooldownBase + CharmEffects.Stalwart), $"生命 {life} → {st.Life}");
            said = CharmSkills.DevCoolingSaid;
            yield return new WaitForSeconds(0.4f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("冷却中按坚硬外壳会提示剩余秒数", CharmSkills.DevCoolingSaid == said + 1, CharmSkills.DevCoolingText);
            pd0().isInvincible = true;
            Player.TerraStats.DevImmortal = false;
        }

        static PlayerData pd0() => PlayerData.instance;

        // ------------------------------------------------------------------ Shape of Unn

        IEnumerator UnnFixTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            WearOnly(CharmEffects.ShapeOfUnn);
            yield return ToDummy(9f);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.ShapeOfUnn);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmEffects.DevUnnKeys = new Vector2(0f, 1f);
            yield return new WaitForSeconds(0.25f);
            CharmEffects.DevUnnKeys = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.4f);
            float speed = rb.linearVelocity.magnitude;
            CharmCheck("乌恩之形飞行速度翻倍（18）", Mathf.Abs(speed - 18f) < 1.5f, $"{speed:F1}");
            CharmEffects.DevUnnKeys = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            // Striking as the light: the nail, and a Terraria sword.
            int sw0 = NailItem.DevSwings, locked0 = CharmEffects.DevLocked;
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            NailAim.DevAim = DummyCentre();
            NailItem.DevHold = true;
            yield return new WaitForSeconds(0.8f);
            NailItem.DevHold = false;
            CharmShot("unnnail");
            CharmCheck("乌恩之形下能挥骨钉", NailItem.DevSwings > sw0, $"{NailItem.DevSwings - sw0} 刀");
            InHand(ItemType.CopperBroadsword);
            yield return UseAt(DummyCentre(), 0.4f);
            CharmCheck("乌恩之形下能用武器", CharmEffects.DevLocked == locked0 && mod.SwingItem != null || CharmEffects.DevLocked == locked0, $"拦下 {CharmEffects.DevLocked - locked0}");
            CharmCheck("还是乌恩的光芒（看不见人物）", CharmEffects.HidePlayer, "");
            // The spores still forbid it.
            CharmEffects.DevUnnKeys = null;
            for (float w = Time.time + 6f; CharmEffects.HidePlayer && Time.time < w;) yield return null;
            CharmCheck("变回来后武器照常能用", !CharmEffects.WeaponLocked(ItemDb.Get(ItemType.CopperBroadsword)), "");
        }

        // ------------------------------------------------------------------ Steady Body

        IEnumerator SteadyFixTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var st = mod.Stats;
            const int Id = CharmEffects.SteadyBody;
            WearOnly(Id);
            yield return ToDummy(14f);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 6f);
            yield return new WaitForSeconds(0.4f);
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(0.3f);
            float run = rb.linearVelocity.x;
            // Faster than the run (a dash's carry, the boots): a turn keeps it.
            rb.linearVelocity = new Vector2(run * 1.6f, rb.linearVelocity.y);
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float turned = rb.linearVelocity.x;
            Player.TerraMovement.DevMove = 0;
            CharmCheck("转身保留原来的速度", turned < -run * 1.4f, $"跑速 {run:F2}，以 {run * 1.6f:F2} 转身后 {turned:F2}");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            CharmCheck("松开照样立刻停下", Mathf.Abs(rb.linearVelocity.x) < 0.01f, $"{rb.linearVelocity.x:F3}");
            // The skill: gold all over.
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + Id);
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.4f);
            CharmCheck("开启护符技后全身变金色", Player.TerraPlayerSprite.GoldTint > 0.9f, $"{Player.TerraPlayerSprite.GoldTint:F2}");
            CharmShot("gold");
            // Dashing through a hit: "免疫".
            int said = CharmEffects.DevImmuneSaid, life = st.Life;
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = false;
            for (float w = Time.time + 3f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < w;) yield return null;
            Player.TerraMovement.ForceDash(1);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            HitFrom(1, 20);
            yield return null;
            CharmShot("immune");
            CharmCheck("冲刺撞到敌人弹出“免疫”", CharmEffects.DevImmuneSaid == said + 1 && st.Life == life, $"{CharmEffects.DevImmuneSaid - said} 次，生命 {life} → {st.Life}");
            HitFrom(1, 20);
            CharmCheck("一直贴着不会刷屏", CharmEffects.DevImmuneSaid == said + 1, "");
            PlayerData.instance.isInvincible = true;
            for (float w = Time.time + 4f; CharmEffects.DashPassActive && Time.time < w;) yield return null;
            yield return new WaitForSeconds(0.4f);
            CharmCheck("3 秒后金色褪去", Player.TerraPlayerSprite.GoldTint < 0.02f, $"{Player.TerraPlayerSprite.GoldTint:F2}");
            Player.TerraStats.DevImmortal = false;
        }

        // ------------------------------------------------------------------ the Fragile Heart

        IEnumerator HeartFixTest()
        {
            var pd = PlayerData.instance;
            var st = mod.Stats;
            int keep = pd.maxHealthBase;
            pd.maxHealthBase = 9;
            WearOnly();
            yield return null;
            yield return null;
            int mask0 = st.MaskLife, max0 = st.LifeMax;
            st.Life = st.LifeMax;
            // Put on seated at a bench: three times the cyan life, and filled.
            pd.atBench = true;
            WearOnly(CharmEffects.FragileHeart);
            yield return null;
            yield return null;
            CharmCheck("青色心变为 3 倍", mask0 > 0 && st.MaskLife == mask0 * 3 && st.LifeMax == max0 + mask0 * 2, $"面具生命 {mask0} → {st.MaskLife}，上限 {max0} → {st.LifeMax}");
            CharmCheck("在长椅上戴上，多出的心是满的", st.Life == st.LifeMax, $"生命 {st.Life}/{st.LifeMax}");
            HudShot("hud3");
            // Away from a bench, more life comes empty (not a free heal).
            WearOnly();
            yield return null;
            pd.atBench = false;
            st.Life = st.LifeMax;
            Charms.DevAnywhere = true;
            WearOnly(CharmEffects.FragileHeart);
            yield return null;
            yield return null;
            CharmCheck("不在长椅上不白给生命", st.Life == max0, $"生命 {st.Life}/{st.LifeMax}");
            pd.fragileHealth_unbreakable = true;
            yield return null;
            CharmCheck("坚固心脏一样 3 倍", st.MaskLife == mask0 * 3, $"{st.MaskLife}");
            pd.fragileHealth_unbreakable = false;
            WearOnly();
            pd.maxHealthBase = keep;
            yield return null;
        }

        // ------------------------------------------------------------------ Soul Eater's looks

        IEnumerator EaterLookTest()
        {
            WearOnly(CharmEffects.SoulEater);
            yield return ToDummy(3f);
            CharmEffects.DevEaterMode = 1;
            InHand(ItemType.LeatherWhip);
            int g0 = CharmEffects.DevWhipGlows;
            mod.DevUseSelected(DummyCentre());
            yield return new WaitForSeconds(0.25f);
            CharmShot("whipglow");
            yield return new WaitForSeconds(0.4f);
            CharmCheck("鞭子模式：整条鞭子外放微光", CharmEffects.DevWhipGlows > g0, $"{CharmEffects.DevWhipGlows - g0} 帧");
            // A minion out, then the minion mode: a halo on it.
            InHand(ItemType.SlimeStaff);
            yield return UseAt(DummyCentre() + Vector2.up * 2f, 0.8f);
            int h0 = CharmEffects.DevMinionHalos;
            yield return new WaitForSeconds(0.3f);
            CharmCheck("鞭子模式下召唤物没有光晕", CharmEffects.DevMinionHalos == h0, $"{CharmEffects.DevMinionHalos - h0}");
            CharmEffects.DevEaterMode = 2;
            yield return new WaitForSeconds(0.5f);
            CharmShot("minionhalo");
            CharmCheck("召唤物模式：召唤物染上光晕", CharmEffects.DevMinionHalos > h0, $"{CharmEffects.DevMinionHalos - h0} 次");
            int w0 = CharmEffects.DevWhipGlows;
            yield return new WaitForSeconds(0.3f);
            CharmCheck("召唤物模式下鞭子不发光", CharmEffects.DevWhipGlows == w0, "");
            CharmEffects.DevEaterMode = 1;
            Minions.Instance?.Clear();
        }

        // ------------------------------------------------------------------ Quick Slash: two notches, no recovery

        IEnumerator QuickRecoveryTest()
        {
            var hero = HeroController.instance;
            WearOnly();
            yield return null;
            CharmCheck("快速劈砍 2 槽", Charms.Cost(CharmEffects.QuickSlash) == 2, $"{Charms.Cost(CharmEffects.QuickSlash)}");
            yield return ToDummy(3f);
            float plain = 0f, quick = 0f;
            bool plainHeld = false, quickHeld = true;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.QuickSlash);
                yield return new WaitForSeconds(0.5f);
                InHand(ItemType.KnightNail);
                PlayerData.instance.hasNailArt = false;
                NailAim.DevAim = DummyCentre();
                int s0 = NailItem.DevSwings;
                NailItem.DevHold = true;
                while (NailItem.DevSwings == s0) yield return null;
                NailItem.DevHold = false;
                float at = Time.time;
                while (Time.time - at < 0.16f) yield return null;
                if (pass == 0) { plain = NailItem.DevLastDuration; plainHeld = hero.cState.attacking; }
                else { quick = NailItem.DevLastDuration; quickHeld = hero.cState.attacking; }
                yield return new WaitForSeconds(0.5f);
            }
            CharmCheck("不戴时挥砍照常有后摇", plain > 0.3f && plainHeld, $"挥砍时长 {plain:F2}，0.16 秒时还在挥 {plainHeld}");
            CharmCheck("戴着快速劈砍挥砍没有后摇", quick <= 0.101f && !quickHeld, $"挥砍时长 {quick:F2}，0.16 秒时还在挥 {quickHeld}");
            // The swing rate as before (the cooldown), the hits as before.
            int h0 = 0, h1 = 0;
            WearOnly();
            yield return SwingNail(2f, (h, d) => h0 = h);
            WearOnly(CharmEffects.QuickSlash);
            int q0 = CharmEffects.DevQuickHits;
            yield return SwingNail(2f, (h, d) => h1 = h);
            int extra = CharmEffects.DevQuickHits - q0;
            CharmCheck("挥砍频率不变，追斩照常", Mathf.Abs((h1 - extra) - h0) <= 1 && extra > 0, $"不戴 {h0} 下，戴着 {h1} 下（追斩 {extra}）");
            CharmShot("silvercross");
        }

        // ------------------------------------------------------------------ the arts' extra cut and beams

        IEnumerator ArtExtrasTest()
        {
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, true);
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            TerraMod.DevNoRealMouse = false;
            bool aimWas = NailAim.On;
            NailAim.Set(false);
            // One art: the K held to the charge, let go (with W held: the Cyclone).
            IEnumerator Art(bool cyclone)
            {
                InHand(ItemType.KnightNail);
                float until = Time.time + 1.25f;
                while (Time.time < until) { if (cyclone) KeysDown(Key.K, Key.W); else KeysDown(Key.K); yield return null; }
                if (cyclone) KeysDown(Key.W); else KeysDown();
                yield return null;
            }
            // Quick Slash: the Great Slash's silver echo.
            WearOnly(CharmEffects.QuickSlash);
            yield return ToDummy(2.6f);
            int e0 = CharmEffects.DevEchoes, q0 = CharmEffects.DevQuickHits;
            Transform great = null;
            foreach (var t in hero.GetComponentsInChildren<Transform>(true)) if (t.name == "Great Slash") { great = t; break; }
            yield return Art(false);
            // Frame by frame: the art's picture and the echo (when its picture is full, when the copy shows).
            int shots = 0, echoSeen = 0;
            bool greatDrawn = false;
            for (float w = Time.time + 0.6f; Time.time < w;)
            {
                yield return null;
                echoSeen = Mathf.Max(echoSeen, CharmEffects.DevEchoesShown);
                if (great == null || !great.gameObject.activeInHierarchy) continue;
                Bounds b = default;
                int on = 0;
                foreach (var r in great.GetComponentsInChildren<Renderer>()) if (r.enabled) { if (on++ == 0) b = r.bounds; else b.Encapsulate(r.bounds); if (r.isVisible && r.bounds.size.x > 1f) greatDrawn = true; }
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 强力斩 {Time.time:F2} 贴图 {on} 个 大小 {(Vector2)b.size:F1}，残影 {CharmEffects.DevEchoesShown}");
                if (CharmEffects.DevEchoesShown > 0 && shots < 3 && Time.frameCount % 3 == 0) CharmShot("greatecho" + shots++);
            }
            CharmShot("greatecho");
            CharmCheck("强力斩画得出来（没被镜头剔除）", greatDrawn, "");
            CharmCheck("银色残影画得出来", CharmEffects.DevEchoDrawn > 0, $"{CharmEffects.DevEchoDrawn} 帧");
            CharmCheck("快速劈砍：强力斩再劈一道银色的", CharmEffects.DevEchoes > e0 && echoSeen > 0, $"{CharmEffects.DevEchoes - e0} 道（{CharmEffects.DevEchoParts} 块，{CharmEffects.DevLastEcho}）");
            yield return new WaitForSeconds(0.2f);
            CharmCheck("强力斩命中后追斩", CharmEffects.DevQuickHits > q0, $"{CharmEffects.DevQuickHits - q0} 刀");
            CharmCheck("银色残影按时消失", CharmEffects.DevEchoesShown == 0, $"{CharmEffects.DevEchoesShown}");
            KeysDown();
            yield return new WaitForSeconds(0.8f);
            // The Cyclone's echoes, each sweep.
            yield return ToDummy(1.6f);
            e0 = CharmEffects.DevEchoes;
            yield return Art(true);
            yield return new WaitForSeconds(0.45f);
            CharmShot("cycloneecho");
            for (float w = Time.time + 2f; NailArts.Striking && Time.time < w;) yield return null;
            KeysDown();
            CharmCheck("快速劈砍：旋风斩每一圈都有银色残影", CharmEffects.DevEchoes - e0 >= 2, $"{CharmEffects.DevEchoes - e0} 道");
            yield return new WaitForSeconds(0.8f);

            // Grubberfly's Elegy: the Great Slash's big beam.
            WearOnly(CharmEffects.GrubberflysElegy);
            yield return ToDummy(7f);
            int b0 = CharmEffects.DevArtBeams, bh0 = CharmEffects.DevBeamHits;
            yield return Art(false);
            for (float w = Time.time + 0.6f; CharmEffects.DevArtBeams == b0 && Time.time < w;) yield return null;
            yield return new WaitForSeconds(0.08f);
            CharmShot("greatbeam");
            var nail = ItemDb.Get(ItemType.KnightNail);
            nail.Damage = NailItem.DamageNow();
            float want = Equipment.Damage(nail) * NailArts.GreatSlash * 0.5f;
            CharmCheck("蜕变挽歌：强力斩射出一道大剑气（技艺一半的伤害）", CharmEffects.DevArtBeams == b0 + 1 && Mathf.Abs(CharmEffects.DevLastBeamScale - 1.6f) < 0.01f
                && Mathf.Abs(CharmEffects.DevLastBeamDamage - want) <= 1.5f, $"{CharmEffects.DevArtBeams - b0} 道，大小 {CharmEffects.DevLastBeamScale}，伤害 {CharmEffects.DevLastBeamDamage}（应为 {want:F1}）");
            yield return new WaitForSeconds(0.6f);
            CharmCheck("大剑气打到远处的木偶", CharmEffects.DevBeamHits > bh0, $"{CharmEffects.DevBeamHits - bh0} 下");
            KeysDown();
            yield return new WaitForSeconds(0.8f);
            // The Cyclone: beams to both sides in turn.
            yield return ToDummy(5f);
            b0 = CharmEffects.DevArtBeams;
            yield return Art(true);
            yield return new WaitForSeconds(0.5f);
            CharmShot("cyclonebeams");
            for (float w = Time.time + 2f; NailArts.Striking && Time.time < w;) yield return null;
            KeysDown();
            CharmCheck("蜕变挽歌：旋风斩转着射出剑气", CharmEffects.DevArtBeams - b0 >= 2, $"{CharmEffects.DevArtBeams - b0} 道");
            yield return new WaitForSeconds(0.8f);
            // Without the charms: none of it.
            WearOnly();
            yield return ToDummy(2.6f);
            e0 = CharmEffects.DevEchoes;
            b0 = CharmEffects.DevArtBeams;
            yield return Art(false);
            yield return new WaitForSeconds(0.8f);
            KeysDown();
            CharmCheck("不戴时技艺没有残影和剑气", CharmEffects.DevEchoes == e0 && CharmEffects.DevArtBeams == b0, "");
            NailAim.Set(aimWas);
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, false);
            TerraMod.DevNoRealMouse = true;
        }

        // ------------------------------------------------------------------ Deep Focus switched

        // A hidden independent game still simulates but may skip automatic camera rendering.
        // Exercise the real camera callbacks and image effects explicitly, including the focus overlay.
        void RenderFocusTest(string name)
        {
            var main = GameCameras.instance.mainCamera;
            var overlay = (Camera)typeof(CharmEffects).GetField("focusCam", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var before = main.targetTexture;
            var overlayBefore = overlay != null ? overlay.targetTexture : null;
            var active = RenderTexture.active;
            Texture2D capture = null;
            try
            {
                main.targetTexture = target;
                main.Render();
                if (overlay != null && overlay.enabled) overlay.Render();
                RenderTexture.active = target;
                capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); capture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, "focus-render-" + name + ".png"), capture.EncodeToPNG());
            }
            finally
            {
                main.targetTexture = before;
                if (overlay != null) overlay.targetTexture = overlayBefore;
                RenderTexture.active = active;
                if (capture != null) Object.Destroy(capture);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        IEnumerator FocusToggleTest()
        {
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            const int Id = CharmEffects.DeepFocus;
            // The Crossroads' crawlers about: not killed on the way (the mod's hits pass isInvincible).
            Player.TerraStats.DevImmortal = true;
            WearOnly(Id);
            if (CharmEffects.DevFocusOn) { CharmSkills.DevPress = true; yield return new WaitForSeconds(1f); }
            yield return ToDummy(3f);
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.8f);
            RenderFocusTest("on");
            CharmCheck("按下开启专心模式", CharmEffects.DevFocusOn && CharmEffects.Focusing && CharmEffects.DevSaturation < 0.05f, $"饱和度 {CharmEffects.DevSaturation:F2}");
            CharmCheck("没有冷却", !Player.PlayerBuffs.Has(CharmSkills.CooldownBase + Id), "");
            CharmCheck("增益栏显示专心（不计时）", Player.PlayerBuffs.Has(CharmSkills.ActiveBase + Id), "");
            // A shot whose hitbox is a child of its picture (a sprite with no DamageHero of its own), and one with no picture at all.
            var shot = new GameObject("Focus test shot");
            shot.transform.position = (Vector2)hero.transform.position + new Vector2(2.5f, 1.2f);
            var pic = shot.AddComponent<SpriteRenderer>();
            pic.sprite = FxBatch_Dot();
            pic.color = Color.white;
            // Big and in front of the town's scenery, so the screenshot shows it and its copy.
            shot.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            pic.sortingOrder = Effects.FxBatch.OnTopOrder;
            var hit = new GameObject("Hitbox");
            hit.transform.SetParent(shot.transform, false);
            var c1 = hit.AddComponent<CircleCollider2D>();
            c1.isTrigger = true;
            c1.radius = 0.3f;
            var dh1 = hit.AddComponent<DamageHero>();
            dh1.damageDealt = 1;
            dh1.hazardType = 1;
            var bare = new GameObject("Focus test particle shot");
            bare.transform.position = (Vector2)hero.transform.position + new Vector2(-2.5f, 1.2f);
            var c2 = bare.AddComponent<CircleCollider2D>();
            c2.isTrigger = true;
            c2.radius = 0.4f;
            var dh2 = bare.AddComponent<DamageHero>();
            dh2.damageDealt = 1;
            dh2.hazardType = 1;
            yield return new WaitForSeconds(0.5f);
            bool redPic = CharmEffects.DevCopied(pic, out bool picSelf) && !picSelf;
            CharmCheck("敌人弹幕（判定框在子物体上）高亮成红色", redPic, "");
            CharmCheck("没有图的弹幕（粒子）画一团红光", CharmEffects.DevFocusMarks > 0, $"{CharmEffects.DevFocusMarks} 个");
            CharmShot("enemyshots");
            yield return null;
            yield return null;
            Object.Destroy(shot);
            Object.Destroy(bare);
            // The player's own: the nail's slash, a Terraria bow's arrows, a sword's swing.
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            NailAim.DevAim = DummyCentre();
            NailItem.DevHold = true;
            yield return new WaitForSeconds(0.5f);
            NailItem.DevHold = false;
            bool slashBlue = false;
            foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
                if (NailArts.IsAttackPicture(r, hero.transform) && CharmEffects.DevCopied(r, out bool self) && self) { slashBlue = true; break; }
            CharmCheck("骨钉的挥砍高亮成蓝色", slashBlue, "");
            int fd0 = FxBatchInstance().DevFocusDraws;
            mod.Inventory.Slots[1] = ItemStack.Of(ItemType.WoodenArrow, 50);
            InHand(ItemType.WoodenBow);
            yield return ToDummy(9f);
            yield return UseAt(DummyCentre(), 0.08f);
            CharmShot("bluearrows");
            yield return new WaitForSeconds(0.3f);
            CharmCheck("玩家的弹幕也变成蓝色", FxBatchInstance().DevFocusDraws > fd0, $"{FxBatchInstance().DevFocusDraws - fd0} 次");
            mod.Inventory.Slots[1] = default;
            // On for good: 15 seconds on, still on.
            yield return new WaitForSeconds(12f);
            RenderFocusTest("persistent");
            CharmCheck("开启后一直生效（12 秒后还在）", CharmEffects.Focusing && CharmEffects.DevSaturation < 0.05f, $"饱和度 {CharmEffects.DevSaturation:F2}");
            // Into another room: on there too.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            for (float w = Time.time + 6f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            CharmShot("newroom");
            RenderFocusTest("newroom");
            CharmCheck("换房间后还在专心", CharmEffects.Focusing && CharmEffects.DevSaturation < 0.05f && CharmEffects.DevFocusEnemyCopies > 0, $"饱和度 {CharmEffects.DevSaturation:F2}，敌人 {CharmEffects.DevFocusEnemyCopies}");
            // Pressed again: off, faded back.
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(1f);
            CharmCheck("再按一次关闭，颜色恢复", !CharmEffects.DevFocusOn && !CharmEffects.Focusing && CharmEffects.DevSaturation > 0.5f && CharmEffects.DevFocusCopies == 0,
                $"饱和度 {CharmEffects.DevSaturation:F2}，高亮 {CharmEffects.DevFocusCopies}");
            CharmCheck("关掉后增益栏不再显示", !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + Id), "");
            // On, then the charm taken off: off at once; put back on: on again.
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.8f);
            WearOnly();
            yield return null;
            yield return null;
            bool offWithCharm = !CharmEffects.Focusing && CharmEffects.DevSaturation > 0.5f;
            WearOnly(Id);
            yield return new WaitForSeconds(0.8f);
            CharmCheck("摘下护符立刻恢复，戴回来自动继续专心", offWithCharm && CharmEffects.Focusing, $"摘下时恢复 {offWithCharm}，戴回 {CharmEffects.Focusing}");
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(1f);
            CharmCheck("最后关掉", !CharmEffects.DevFocusOn && !CharmEffects.Focusing, "");
            Player.TerraStats.DevImmortal = false;
            yield return CharmPlace();
        }

        static Sprite testDot;
        static Sprite FxBatch_Dot()
        {
            if (testDot != null) return testDot;
            var tex = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return testDot = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 32f);
        }

        static Effects.FxBatch FxBatchInstance() => Effects.FxBatch.Instance;

        // ------------------------------------------------------------------ the recall and Rod of Discord keys

        IEnumerator QuickKeysTest()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            CharmCheck("回城键默认不绑定", Player.TerraKeys.Default(Player.TerraKeys.Action.Recall) == Key.None && Player.TerraKeys.Get(Player.TerraKeys.Action.Recall) == Key.None, "");
            CharmCheck("混沌传送键默认不绑定", Player.TerraKeys.Default(Player.TerraKeys.Action.DiscordTeleport) == Key.None, "");
            CharmCheck("设置的按键里有这两个", System.Array.IndexOf(Player.TerraKeys.Ordered, Player.TerraKeys.Action.Recall) >= 0 && System.Array.IndexOf(Player.TerraKeys.Ordered, Player.TerraKeys.Action.DiscordTeleport) >= 0,
                $"{Player.TerraKeys.Name(Player.TerraKeys.Action.Recall)} / {Player.TerraKeys.Name(Player.TerraKeys.Action.DiscordTeleport)}");
            string unbound = TerraMod.QuickKeyLine(ItemDb.Get(ItemType.MagicMirror)) ?? "";
            CharmCheck("没绑定时魔镜写着去哪里绑定", unbound.Contains("绑定") && unbound.Contains(Player.TerraKeys.Name(Player.TerraKeys.Action.Recall)), unbound);
            CharmCheck("回忆药水、手机也有提示，混沌传送杖写着混沌传送键", TerraMod.QuickKeyLine(ItemDb.Get(ItemType.RecallPotion)) != null && TerraMod.QuickKeyLine(ItemDb.Get(ItemType.CellPhone)) != null
                && (TerraMod.QuickKeyLine(ItemDb.Get(ItemType.RodofDiscord)) ?? "").Contains(Player.TerraKeys.Name(Player.TerraKeys.Action.DiscordTeleport)), TerraMod.QuickKeyLine(ItemDb.Get(ItemType.RodofDiscord)));
            CharmCheck("别的物品没有这行", TerraMod.QuickKeyLine(ItemDb.Get(ItemType.CopperBroadsword)) == null, "");
            Player.TerraKeys.Set(Player.TerraKeys.Action.DiscordTeleport, Key.F10);
            Player.TerraKeys.Set(Player.TerraKeys.Action.Recall, Key.F9);
            string bound = TerraMod.QuickKeyLine(ItemDb.Get(ItemType.MagicMirror)) ?? "";
            CharmCheck("绑定后写着按键", bound.Contains("F9"), bound);
            // The Rod of Discord's key: to the cursor from the bag (not in hand).
            yield return ToDummy(6f);
            inv.Slots[0] = ItemStack.Of(ItemType.CopperBroadsword);
            inv.Selected = 0;
            inv.Slots[20] = ItemStack.Of(ItemType.RodofDiscord);
            var from = (Vector2)hero.transform.position;
            mod.DevAim = from + new Vector2(3f, 1.5f);
            yield return Tap(Key.F10);
            yield return new WaitForSeconds(0.2f);
            float moved = Vector2.Distance(from, hero.transform.position);
            CharmCheck("按混沌传送键传到光标（杖在背包里）", moved > 2f && Player.PlayerBuffs.Has(Player.TeleportRods.ChaosState) && inv.Slots[0].Type == ItemType.CopperBroadsword, $"移动 {moved:F1}，{TerraMod.DevLastQuickUse}");
            mod.DevAim = null;
            inv.Slots[20] = default;
            yield return Tap(Key.F10);
            CharmCheck("没有杖时提示", TerraMod.DevLastQuickUse.Contains("没找到"), TerraMod.DevLastQuickUse);
            // The recall key: a Recall Potion used up, home (the autotests' endless items off for it).
            bool endless = inv.Infinite;
            inv.Infinite = false;
            yield return new WaitForSeconds(1f);
            inv.Slots[21] = ItemStack.Of(ItemType.RecallPotion, 3);
            string scene0 = GameManager.instance.sceneName;
            yield return Tap(Key.F9);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("按回城键用掉一瓶回忆药水回家", TerraMod.DevLastQuickUse.Contains("用了") && inv.Slots[21].Count == 2, $"{TerraMod.DevLastQuickUse}，剩 {inv.Slots[21].Count}");
            for (float w = Time.time + 12f; (!GameHooks.PlayerHasControl() || GameManager.instance.IsInSceneTransition) && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1.5f);
            // A mirror carried: it goes first, nothing used up.
            inv.Slots[22] = ItemStack.Of(ItemType.MagicMirror);
            yield return Tap(Key.F9);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("有魔镜时先用魔镜，药水不少", TerraMod.DevLastQuickUse.Contains(ItemDb.Get(ItemType.MagicMirror).Name) && inv.Slots[21].Count == 2, TerraMod.DevLastQuickUse);
            for (float w = Time.time + 12f; (!GameHooks.PlayerHasControl() || GameManager.instance.IsInSceneTransition) && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            inv.Slots[21] = default;
            inv.Slots[22] = default;
            inv.Infinite = endless;
            Player.TerraKeys.Set(Player.TerraKeys.Action.Recall, Key.None);
            Player.TerraKeys.Set(Player.TerraKeys.Action.DiscordTeleport, Key.None);
            Player.PlayerBuffs.Remove(Player.TeleportRods.ChaosState);
            KeysDown();
        }

        // ------------------------------------------------------------------ the charms' descriptions

        IEnumerator CharmTextsTest()
        {
            var pd = PlayerData.instance;
            int empty = 0, vanilla = 0, untranslated = 0;
            var bad = new List<string>();
            for (int id = 1; id <= Charms.Count; id++)
            {
                string d = Charms.Desc(id);
                if (string.IsNullOrEmpty(d)) { empty++; bad.Add(id + " 空"); continue; }
                string v = Charms.VanillaDesc(id);
                if (!string.IsNullOrEmpty(v) && v == d) { vanilla++; bad.Add(id + " 原版"); }
                if (Lang.Line(CharmTexts.Zh(id, pd), "en-US") == null) { untranslated++; bad.Add(id + " 没有英文"); }
            }
            CharmCheck("40 个护符都有新的描述", empty == 0 && vanilla == 0, string.Join("，", bad));
            CharmCheck("都有英文", untranslated == 0, string.Join("，", bad));
            bool u0 = pd.fragileHealth_unbreakable;
            pd.fragileHealth_unbreakable = true;
            string whole = Charms.Desc(23);
            pd.fragileHealth_unbreakable = false;
            string fragile = Charms.Desc(23);
            pd.fragileHealth_unbreakable = u0;
            CharmCheck("易碎和坚固心脏说法不同", whole != fragile && whole.Contains("不会碎") && fragile.Contains("碎掉"), "");
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 示例 16：{Charms.Desc(16).Replace("\n", " / ")}");
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 示例 34：{Charms.Desc(34).Replace("\n", " / ")}");
            // The tooltip shows it (white, under the name and the cost).
            pd.SetBool("gotCharm_16", true);
            InventoryGui.Open = true;
            InventoryGui.DevEquipPage = 3;
            yield return new WaitForSeconds(0.8f);
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 背包开着 {InventoryGui.Open}，页 {InventoryGui.DevEquipPage}，场景 {GameManager.instance.sceneName}");
            InventoryGui.DevCharmTip = 16;
            for (float w = Time.time + 0.5f; Time.time < w;) { InventoryGui.Open = true; InventoryGui.DevEquipPage = 3; yield return null; }
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 截图时背包开着 {InventoryGui.Open}");
            HudShot("tip16");
            InventoryGui.DevCharmTip = 0;
            InventoryGui.Open = false;
            yield return null;
        }

        // ------------------------------------------------------------------ probe: what of the nail's is drawn behind the blocks

        IEnumerator NailFrontProbe()
        {
            var hero = HeroController.instance;
            int def = SortingLayer.NameToID("Default");
            string Sort(Renderer r) => $"{SortingLayer.IDToName(r.sortingLayerID)}({SortingLayer.GetLayerValueFromID(r.sortingLayerID)})/{r.sortingOrder} 队列 {(r.sharedMaterial != null ? r.sharedMaterial.renderQueue : -1)} {(r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?")} z {r.transform.position.z:F2} 层 {r.gameObject.layer}";
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} Default 层值 {SortingLayer.GetLayerValueFromID(def)}；全部排序层：{string.Join("，", System.Array.ConvertAll(SortingLayer.layers, l => l.name + "=" + l.value))}");
            yield return ToDummy(1.6f);
            // A stone wall right in front, between the player and the dummy.
            var dc = DummyCentre();
            var cell = World.CellGrid.WorldToCell(new Vector2(dc.x - 0.9f, dc.y));
            var placed = new List<Vector2Int>();
            for (int x = cell.x; x <= cell.x; x++)
                for (int y = cell.y - 1; y < cell.y + 4; y++)
                    if (World.TerraWorld.Get(x, y) == World.CellKind.Empty) { World.TerraWorld.Set(x, y, World.CellKind.Stone); placed.Add(new Vector2Int(x, y)); }
            yield return new WaitForSeconds(0.4f);
            yield return ToDummy(1.6f);
            // The cells' renderers near.
            var seen = new HashSet<Renderer>();
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (r.enabled && r.gameObject.activeInHierarchy && Vector2.Distance(r.bounds.center, dc) < 1.6f && !r.transform.IsChildOf(hero.transform) && seen.Count < 12 && seen.Add(r))
                    Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 墙边 {PathOf(r.transform)} {Sort(r)}");
            var before = new HashSet<Renderer>(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None));
            // A swing into the wall: what is drawn meanwhile near the player.
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            NailAim.DevAim = dc;
            NailItem.DevHold = true;
            var logged = new HashSet<Renderer>();
            bool slashDrawn = false, dreamDrawn = false;
            for (float t = Time.time + 0.8f; Time.time < t;)
            {
                yield return null;
                foreach (var r in hero.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.isVisible && r.name.Contains("Slash") && PathOf(r.transform).Contains("Attacks")) slashDrawn = true;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || !r.isVisible || logged.Contains(r)) continue;
                    if (Vector2.Distance(r.bounds.center, hero.transform.position) > 3.5f) continue;
                    bool mine = r.transform.IsChildOf(hero.transform);
                    if (!mine && before.Contains(r) && !(r.name.Contains("Slash") || r.name.Contains("Hit") || r.name.Contains("Nail"))) continue;
                    if (r.gameObject.layer == CharmEffects.FocusLayer || r.name == "Fx") continue;
                    if (mine && r is SpriteRenderer && r.transform.IsChildOf(mod.PlayerLook != null ? mod.PlayerLook.transform : hero.transform)) continue;
                    logged.Add(r);
                    Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 挥砍时 {(mine ? "骑士的" : "场景的")} {PathOf(r.transform)} {r.GetType().Name} {Sort(r)}");
                }
                if (Time.time > t - 0.55f && Time.time < t - 0.5f) CharmShot("intowall");
            }
            NailItem.DevHold = false;
            yield return new WaitForSeconds(0.5f);
            // The Dream Nail at the wall.
            PlayerData.instance.hasDreamNail = true;
            InHand(ItemType.DreamNail);
            DreamNailItem.DevHold = true;
            bool shot = false;
            var dn = DreamNailItem.DevFsm(hero);
            for (float t = Time.time + 2.6f; Time.time < t;)
            {
                if (Time.time > t - 1.3f) DreamNailItem.DevHold = false;
                yield return null;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || logged.Contains(r)) continue;
                    if (!r.transform.IsChildOf(hero.transform) && Vector2.Distance(r.bounds.center, hero.transform.position) > 3.5f) continue;
                    if (r.transform.IsChildOf(hero.transform) && !(PathOf(r.transform).Contains("Dream") || r.name.Contains("Dream"))) continue;
                    logged.Add(r);
                    Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 梦之钉 {PathOf(r.transform)} {r.GetType().Name} {Sort(r)}");
                }
                foreach (var r in hero.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.isVisible && r is MeshRenderer && PathOf(r.transform).Contains("Dream Effects/Slash")) dreamDrawn = true;
                if (!shot && dn != null && dn.ActiveStateName == "Slash") { yield return null; CharmShot("dreamwall"); shot = true; }
            }
            DreamNailItem.DevHold = false;
            foreach (var c in placed) World.TerraWorld.Set(c.x, c.y, World.CellKind.Empty);
            int front = Effects.FxBatch.OnTopOrder + 2, behindDream = 0, dreamSeen = 0, behindSpark = 0, sparkSeen = 0;
            foreach (var r in logged)
            {
                if (r == null) continue;
                bool over = SortingLayer.GetLayerValueFromID(r.sortingLayerID) > SortingLayer.GetLayerValueFromID(def) || r.sortingOrder >= front;
                string path = PathOf(r.transform);
                if (path.Contains("Dream Effects")) { dreamSeen++; if (!over) behindDream++; }
                if (path.Contains("Nail Terrain Hit Effect")) { sparkSeen++; if (!over) behindSpark++; }
            }
            CharmCheck("梦之钉的挥砍和特效都在物块前面", dreamSeen > 0 && behindDream == 0, $"{dreamSeen} 个，在后面的 {behindDream}");
            CharmCheck("骨钉砍到墙的火花在物块前面", sparkSeen > 0 && behindSpark == 0, $"{sparkSeen} 个，在后面的 {behindSpark}");
            CharmCheck("骨钉的挥砍在物块前面", NailArts.DevFronted > 0, $"放到前面的 {NailArts.DevFronted} 个");
            CharmCheck("骨钉的挥砍画得出来（没被镜头剔除）", slashDrawn, "");
            CharmCheck("梦之钉的挥砍画得出来（没被镜头剔除）", dreamDrawn, $"醒来后补回顺序 {NailArts.DevFrontedAgain} 次");
        }
    }
}
