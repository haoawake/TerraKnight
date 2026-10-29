using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "charm3", "charm4" and on): the charm bag's charm effects (CharmEffects), each tried as it should work and
    /// then where it could go wrong. Shared: the charm bag on, changing anywhere, the charm worn alone, screenshots round the
    /// player named charmN_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        int charmPass, charmAll;
        string charmTag = "";

        void CharmCheck(string name, bool ok, string detail = "")
        {
            charmAll++;
            if (ok) charmPass++;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {name} {(ok ? "通过" : "不通过")} {detail}");
        }

        /// <summary>Only this charm worn (the Void Heart aside), the bag on, changing anywhere.</summary>
        void WearOnly(params int[] ids)
        {
            var pd = PlayerData.instance;
            Charms.DevSetPage(true);
            Charms.DevAnywhere = true;
            foreach (int id in new List<int>(pd.equippedCharms)) if (System.Array.IndexOf(ids, id) < 0 && Charms.Worn(id)) Charms.Click(id);
            pd.charmSlots = 11;
            foreach (int id in ids)
            {
                pd.SetBool("gotCharm_" + id, true);
                if (!Charms.Worn(id)) Charms.Click(id);
            }
            // The first of them with a skill favourited: its skill the key's.
            int chosen = 0;
            foreach (int id in ids) if (chosen == 0 && CharmSkills.HasSkill(id)) chosen = id;
            CharmSkills.Choose(chosen);
        }

        IEnumerator CharmPlace()
        {
            yield return GoTo("Town", "left1");
            // A preceding death test may still redirect the first transition to the respawn scene.
            if (GameManager.instance.sceneName != "Town")
            {
                yield return new WaitForSeconds(1f);
                yield return GoTo("Town", "left1");
            }
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 6f);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>A screenshot whose crop is the HUD's top right (the hearts), not the player.</summary>
        void HudShot(string name)
        {
            Shot(GameManager.instance.sceneName, $"charm{charmTag}_{name}");
            Plugin.Log.LogInfo($"[AutoTest] 护符图 截图 charm{charmTag}_{name} 主角屏幕 1300,110");
        }

        void CharmShot(string name)
        {
            var hero = HeroController.SilentInstance;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : Camera.main;
            var p = hero != null && cam != null ? cam.WorldToScreenPoint(hero.GetComponent<BoxCollider2D>().bounds.center) : Vector3.zero;
            Shot(GameManager.instance.sceneName, $"charm{charmTag}_{name}");
            Plugin.Log.LogInfo($"[AutoTest] 护符图 截图 charm{charmTag}_{name} 主角屏幕 {p.x:F0},{Screen.height - p.y:F0}");
        }

        IEnumerator CharmsTest(int charm)
        {
            charmPass = charmAll = 0;
            charmTag = charm.ToString();
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return CharmPlace();
            switch (charm)
            {
                case CharmEffects.Grubsong: yield return GrubsongTest(); break;
                case CharmEffects.Stalwart: yield return StalwartTest(); break;
                case CharmEffects.Baldur: yield return CharmBaldurTest(); break;
                case CharmEffects.Fury: yield return FuryTest(); break;
                case CharmEffects.QuickFocus: yield return QuickFocusTest(); break;
                case CharmEffects.LifebloodHeart: yield return LifebloodHeartTest(); break;
                case CharmEffects.LifebloodCore: yield return LifebloodCoreTest(); break;
                case CharmEffects.Defender: yield return DefenderTest(); break;
                case CharmEffects.SteadyBody: yield return SteadyTest(); break;
                case CharmEffects.DeepFocus: yield return FocusModeTest(); break;
                case 0: yield return CharmComboTest(); break;
                default: yield return CharmsTest2(charm); break;
            }
            Charms.DevAnywhere = false;
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        IEnumerator GrubsongTest()
        {
            var st = mod.Stats;
            var hero = HeroController.instance;
            // Pictures this charm work might use: their sizes, if the Terraria install has them.
            foreach (var name in new[] { "NPC_356", "NPC_605", "NPC_661", "Projectile_511", "Projectile_512", "Projectile_513", "Extra_98", "Item_938", "Extra_174" })
            {
                string size;
                try { var tex = Terraria.TerrariaAssets.Texture(name); size = tex != null ? $"{tex.width}x{tex.height}" : "无"; }
                catch (System.Exception e) { size = "读不到 " + e.GetType().Name; }
                Plugin.Log.LogInfo($"[AutoTest] 护符图 {name} {size}");
            }
            WearOnly(CharmEffects.Grubsong);
            CharmCheck("戴上", Charms.Worn(CharmEffects.Grubsong));
            st.Life = st.LifeMax;
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Grubsong);
            int heals = CharmEffects.DevGrubHeals;
            yield return new WaitForSeconds(1.5f);
            CharmCheck("满血不触发", CharmEffects.DevGrubHeals == heals, $"生命 {st.Life}/{st.LifeMax}");
            CharmShot("ready");

            // Hurt: back a tenth after half a second, with the cooldown.
            st.Life = st.LifeMax;
            st.TakeShare(40);
            int before = st.Life;
            yield return new WaitForSeconds(0.3f);
            CharmCheck("受伤后先等半秒", CharmEffects.DevGrubHeals == heals && st.Life == before, $"生命 {st.Life}");
            yield return new WaitForSeconds(0.35f);
            CharmShot("halo");
            yield return new WaitForSeconds(0.2f);
            CharmShot("halo2");
            int tenth = Mathf.CeilToInt(st.LifeMax * 0.1f);
            CharmCheck("回复 10% 生命", CharmEffects.DevGrubHeals == heals + 1 && st.Life == before + tenth, $"{before} → {st.Life}（10% = {tenth}）");
            CharmCheck("冷却 10 秒", Player.PlayerBuffs.Has(CharmSkills.CooldownBase + CharmEffects.Grubsong) && Mathf.Abs(Player.PlayerBuffs.TimeLeft(CharmSkills.CooldownBase + CharmEffects.Grubsong) / 60f - 9.3f) < 0.6f,
                $"剩 {Player.PlayerBuffs.TimeLeft(CharmSkills.CooldownBase + CharmEffects.Grubsong) / 60f:F1} 秒");
            yield return new WaitForSeconds(0.5f);
            CharmShot("cooling");

            // Hurt again while it cools: nothing until the cooldown is over, then once.
            st.TakeShare(30);
            before = st.Life;
            yield return new WaitForSeconds(3f);
            CharmCheck("冷却中不再回复", CharmEffects.DevGrubHeals == heals + 1 && st.Life == before, $"生命 {st.Life}");
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Grubsong);
            yield return new WaitForSeconds(0.7f);
            CharmCheck("冷却结束后再回复一次", CharmEffects.DevGrubHeals == heals + 2 && st.Life == before + tenth, $"{before} → {st.Life}");

            // Just short of full: only up to full.
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Grubsong);
            st.Life = st.LifeMax;
            st.TakeShare(3);
            yield return new WaitForSeconds(0.8f);
            CharmCheck("快满时只补到满", st.Life == st.LifeMax, $"生命 {st.Life}/{st.LifeMax}");

            // Taken off while hurt: nothing.
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Grubsong);
            Charms.Click(CharmEffects.Grubsong);
            st.Life = st.LifeMax;
            st.TakeShare(50);
            before = st.Life;
            int h0 = CharmEffects.DevGrubHeals;
            yield return new WaitForSeconds(1f);
            CharmCheck("摘下后不回复", !Charms.Worn(CharmEffects.Grubsong) && CharmEffects.DevGrubHeals == h0 && st.Life == before, $"生命 {st.Life}");
            CharmShot("off");

            // Dying with it on: nothing while dead (a heal at 0 life would stand a dead player up); after, all is well.
            Charms.Click(CharmEffects.Grubsong);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Grubsong);
            int deadHeals = -1;
            bool sawDead = false;
            h0 = CharmEffects.DevGrubHeals;
            var watch = mod.StartCoroutine(WatchDeadHeals(() => sawDead = true, n => deadHeals = n, h0));
            Vector2 back = default;
            yield return DieAndComeBack((at, scene) => back = at);
            mod.StopCoroutine(watch);
            CharmCheck("死着的时候不回复", sawDead && deadHeals == 0, $"死亡期间回复 {deadHeals} 次");
            hero = HeroController.instance;
            yield return new WaitForSeconds(1f);
            CharmCheck("复活后生命正常", st.Life > 0 && st.Life <= st.LifeMax, $"生命 {st.Life}/{st.LifeMax}");

            // A new room: the butterflies come along (no trail from the old place).
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(0.1f);
            CharmShot("newroom");
            yield return new WaitForSeconds(1.5f);
            CharmShot("newroom2");
            PlayerData.instance.isInvincible = true;
        }

        /// <summary>A hit from an enemy's touch on the right (the game's own way in, as PlayerHooks.HurtPlayer gives it).</summary>
        GameObject hitter;
        void HitFromRight(int damage)
        {
            var hero = HeroController.instance;
            if (hitter == null) hitter = new GameObject("Charm test hitter");
            hitter.transform.position = (Vector2)hero.transform.position + Vector2.right * 1.5f;
            Player.PlayerHooks.HurtPlayer(hero, damage, -1, hitter);
        }

        /// <summary>How long the hero stays immune after a hit (seconds, at most five).</summary>
        IEnumerator MeasureImmunity(System.Action<float> done)
        {
            var hero = HeroController.instance;
            float t0 = Time.time;
            while (!hero.cState.invulnerable && Time.time - t0 < 0.2f) yield return null;
            float start = Time.time;
            while (hero.cState.invulnerable && Time.time - start < 5f) yield return null;
            done(Time.time - start);
        }

        IEnumerator StalwartTest()
        {
            var st = mod.Stats;
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.Stalwart;
            int cd = CharmSkills.CooldownBase + Id;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            // The dummy to aim the waves at.
            var dummy = World.TargetDummy.DevHm;
            CharmCheck("训练假人在", dummy != null, dummy != null ? $"{(Vector2)dummy.transform.position:F1}" : "");
            if (dummy != null) { hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 9f); yield return new WaitForSeconds(0.6f); }

            // Without it, then with it: the hit's immunity 0.2 s longer.
            WearOnly();
            st.Life = st.LifeMax;
            float plain = 0f, longer = 0f;
            HitFromRight(10);
            yield return MeasureImmunity(v => plain = v);
            yield return new WaitForSeconds(0.3f);
            WearOnly(Id);
            HitFromRight(10);
            yield return MeasureImmunity(v => longer = v);
            CharmCheck("受伤无敌多 0.2 秒", Mathf.Abs(longer - plain - 0.2f) < 0.05f, $"不戴 {plain:F2} 秒，戴着 {longer:F2} 秒");
            yield return new WaitForSeconds(0.3f);

            // The shell: every hit taken whole, no knockback; the spikes' way too.
            Player.PlayerBuffs.Remove(cd);
            st.Life = st.LifeMax;
            int absorbed0 = CharmEffects.DevShellAbsorbed;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("按下张开外壳", Player.PlayerBuffs.Has(CharmSkills.ActiveBase + Id), $"增益 {Player.PlayerBuffs.TimeLeft(CharmSkills.ActiveBase + Id) / 60f:F1} 秒");
            CharmShot("shell");
            HitFromRight(40);
            yield return null;
            float vy = hero.GetComponent<Rigidbody2D>().linearVelocity.y;
            yield return new WaitForSeconds(1f);
            HitFromRight(25);
            yield return new WaitForSeconds(0.2f);
            st.TakeShare(15);
            CharmShot("shell_hit");
            int took = CharmEffects.DevShellAbsorbed - absorbed0;
            CharmCheck("外壳期间不掉血（敌人和尖刺都吸收）", st.Life == st.LifeMax && took > 0, $"生命 {st.Life}/{st.LifeMax}，吸收 {took}");
            CharmCheck("外壳期间没有击退", vy < 1f, $"挨打后竖直速度 {vy:F2}");

            // Again: the wave at the dummy, bursting for the whole amount.
            int hits0 = CharmEffects.DevWaveHits, waves0 = CharmEffects.DevWaves;
            if (dummy != null) CharmEffects.DevAim = dummy.GetComponent<Collider2D>().bounds.center;
            int dummyHits0 = World.TargetDummy.DevHits;
            CharmSkills.DevPress = true;
            yield return null;
            yield return new WaitForSeconds(0.12f);
            CharmShot("wave");
            yield return new WaitForSeconds(0.25f);
            CharmShot("burst");
            yield return new WaitForSeconds(0.6f);
            CharmCheck("再按放出光波打中假人，伤害等于吸收总量", CharmEffects.DevWaves == waves0 + 1 && CharmEffects.DevWaveHits > hits0 && CharmEffects.DevWaveDamage == took,
                $"光波 {CharmEffects.DevWaves - waves0}，打中 {CharmEffects.DevWaveHits - hits0}，伤害 {CharmEffects.DevWaveDamage}（吸收 {took}），假人记到 {World.TargetDummy.DevHits - dummyHits0} 下");
            CharmCheck("放完冷却 20 秒", Player.PlayerBuffs.Has(cd) && Player.PlayerBuffs.TimeLeft(cd) / 60f > 18f, $"剩 {Player.PlayerBuffs.TimeLeft(cd) / 60f:F1} 秒");

            // In the cooldown: a press does nothing, a hit hurts.
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("冷却中按下没有外壳", !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + Id));
            int lifeBefore = st.Life;
            HitFromRight(20);
            yield return null;
            CharmCheck("冷却中挨打照常掉血", st.Life < lifeBefore, $"{lifeBefore} → {st.Life}");
            yield return new WaitForSeconds(1f);

            // Up and nothing taken: it ends at three seconds with the cooldown, no wave.
            Player.PlayerBuffs.Remove(cd);
            waves0 = CharmEffects.DevWaves;
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(3.3f);
            CharmCheck("什么都没吸收就安静结束", !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + Id) && Player.PlayerBuffs.Has(cd) && CharmEffects.DevWaves == waves0);

            // Taken and kept past ten seconds: gone, cooldown, no wave.
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            HitFromRight(30);
            yield return new WaitForSeconds(3.5f);
            CharmShot("held");
            yield return new WaitForSeconds(10f);
            CharmCheck("存着不放 10 秒后散掉", Player.PlayerBuffs.Has(cd) && CharmEffects.DevWaves == waves0);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("散掉之后再按不出光波", CharmEffects.DevWaves == waves0);

            // A wave in flight when the room changes: gone with the room, nothing bursts in the next.
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            HitFromRight(30);
            yield return new WaitForSeconds(0.5f);
            CharmEffects.DevAim = (Vector2)hero.transform.position + Vector2.left * 20f;
            hits0 = CharmEffects.DevWaveHits;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            CharmCheck("换房间后光波不会在新房间炸", CharmEffects.DevWaveHits == hits0 && CharmEffects.DevWavesInFlight == 0, $"在飞 {CharmEffects.DevWavesInFlight}");

            // Taken off while charged: nothing kept.
            hero = HeroController.instance;
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            HitFromRight(30);
            yield return new WaitForSeconds(0.3f);
            Charms.Click(Id);
            yield return null;
            Charms.Click(Id);
            yield return new WaitForSeconds(1f);
            int lifeNow = st.Life;
            HitFromRight(30);
            yield return null;
            CharmCheck("摘下再戴，外壳不在了", st.Life < lifeNow && !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + Id), $"{lifeNow} → {st.Life}");
            CharmEffects.DevAim = null;
            pd.isInvincible = true;
        }

        void HitFrom(int side, int damage, GameObject source = null)
        {
            var hero = HeroController.instance;
            if (source == null)
            {
                if (hitter == null) hitter = new GameObject("Charm test hitter");
                source = hitter;
                hitter.transform.position = (Vector2)hero.transform.position + Vector2.right * (1.5f * side);
            }
            Player.PlayerHooks.HurtPlayer(hero, damage, -side, source);
        }

        IEnumerator CharmBaldurTest()
        {
            var st = mod.Stats;
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.Baldur;
            int cd = CharmSkills.CooldownBase + Id;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            var dummy = World.TargetDummy.DevHm;
            if (dummy != null) { hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 8f); yield return new WaitForSeconds(0.6f); }
            WearOnly(Id);
            // A plain hit from the right, for the amounts.
            st.Life = st.LifeMax;
            HitFrom(1, 40);
            int plain = st.LifeMax - st.Life;
            yield return new WaitForSeconds(1f);
            CharmEffects.DevAim = (Vector2)hero.transform.position + Vector2.right * 5f;

            // Raised and hit at once: a perfect guard, nothing lands, 3 s cooldown.
            st.Life = st.LifeMax;
            Player.PlayerBuffs.Remove(cd);
            int perfect0 = CharmEffects.DevPerfects;
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmShot("raise");
            HitFrom(1, 40);
            yield return null;
            CharmShot("perfect");
            CharmCheck("精准格挡：完全挡下", st.Life == st.LifeMax && CharmEffects.DevPerfects == perfect0 + 1, $"生命 {st.Life}/{st.LifeMax}");
            CharmSkills.DevHeld = false;
            yield return null;
            yield return null;
            CharmCheck("精准格挡后冷却 3 秒", Player.PlayerBuffs.Has(cd) && Mathf.Abs(Player.PlayerBuffs.TimeLeft(cd) / 60f - 3f) < 0.3f, $"剩 {Player.PlayerBuffs.TimeLeft(cd) / 60f:F1} 秒");
            yield return new WaitForSeconds(1f);

            // Raised a while, hit in front: 30%; from behind: all of it.
            Player.PlayerBuffs.Remove(cd);
            st.Life = st.LifeMax;
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.3f);
            HitFrom(1, 40);
            int front = st.LifeMax - st.Life;
            CharmShot("guard");
            CharmCheck("正面挨打只受 30%", front == Mathf.Max(1, Mathf.RoundToInt(plain * 0.3f)), $"平时 {plain}，举盾 {front}");
            yield return new WaitForSeconds(0.7f);
            CharmCheck("一直按着，0.8 秒后盾自己放下，冷却 5 秒", Player.PlayerBuffs.Has(cd) && Mathf.Abs(Player.PlayerBuffs.TimeLeft(cd) / 60f - 4.8f) < 0.5f, $"剩 {Player.PlayerBuffs.TimeLeft(cd) / 60f:F1} 秒");
            CharmSkills.DevHeld = false;
            Player.PlayerBuffs.Remove(cd);
            yield return new WaitForSeconds(0.8f);
            st.Life = st.LifeMax;
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.3f);
            HitFrom(-1, 40);
            int behind = st.LifeMax - st.Life;
            CharmCheck("背后挨打照常", behind == plain, $"平时 {plain}，背后 {behind}");
            CharmSkills.DevHeld = false;
            yield return new WaitForSeconds(0.8f);

            // In the cooldown: no shell.
            int guards0 = CharmEffects.DevGuards;
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            st.Life = st.LifeMax;
            HitFrom(1, 40);
            CharmCheck("冷却中按不出盾", CharmEffects.DevGuards == guards0 && st.LifeMax - st.Life == plain, $"受到 {st.LifeMax - st.Life}");
            CharmSkills.DevHeld = false;
            yield return new WaitForSeconds(0.8f);

            // Moving with it up: 60%.
            Player.PlayerBuffs.Remove(cd);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 2f);
            yield return new WaitForSeconds(0.3f);
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForSeconds(0.4f);
            float free = Mathf.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.x);
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.6f);
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return null;
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForSeconds(0.4f);
            float slowed = Mathf.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.x);
            Player.TerraMovement.DevMove = 0;
            CharmSkills.DevHeld = false;
            CharmCheck("举盾时速度六成", free > 0.5f && Mathf.Abs(slowed / free - 0.6f) < 0.08f, $"平时 {free:F2}，举盾 {slowed:F2}（{slowed / Mathf.Max(0.01f, free):F2}）");
            yield return new WaitForSeconds(0.8f);

            // A shot met perfectly: sent back at the dummy, harmless to the player; put away by the game, it is as it was.
            Player.PlayerBuffs.Remove(cd);
            hero = HeroController.instance;
            if (dummy != null) CharmEffects.DevAim = dummy.GetComponent<Collider2D>().bounds.center;
            var shot = new GameObject("Charm test shot");
            shot.layer = 11;
            shot.transform.position = (Vector2)hero.transform.position + Vector2.right * 1.2f;
            var rb = shot.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.left * 10f;
            var sc = shot.AddComponent<CircleCollider2D>();
            sc.isTrigger = true;
            sc.radius = 0.2f;
            var dh = shot.AddComponent<DamageHero>();
            dh.damageDealt = 1;
            dh.hazardType = 1;
            int dummyHits0 = World.TargetDummy.DevHits, reflects0 = CharmEffects.DevReflects;
            st.Life = st.LifeMax;
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            HitFrom(1, 40, shot);
            bool harmless = dh.damageDealt == 0;
            var way = rb.linearVelocity;
            yield return new WaitForSeconds(0.1f);
            CharmShot("reflect");
            CharmSkills.DevHeld = false;
            yield return new WaitForSeconds(1.2f);
            CharmCheck("弹幕被反弹：不伤自己，飞向瞄准方向", CharmEffects.DevReflects == reflects0 + 1 && harmless && Vector2.Dot(way.normalized, ((Vector2)CharmEffects.DevAim - (Vector2)hero.transform.position).normalized) > 0.9f,
                $"反弹 {CharmEffects.DevReflects - reflects0}，速度 {way:F1}，伤害玩家 {dh.damageDealt}");
            CharmCheck("反弹的弹幕打中假人", World.TargetDummy.DevHits > dummyHits0, $"假人记到 {World.TargetDummy.DevHits - dummyHits0} 下");
            shot.SetActive(false);
            yield return null;
            CharmCheck("弹幕被游戏收回后恢复原样", dh.damageDealt == 1 && shot.GetComponent<CharmEffects.ReflectedShot>() == null, $"伤害 {dh.damageDealt}");
            Object.Destroy(shot);

            // Taken off with the shell up: gone at once.
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevHeld = true;
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(0.3f);
            Charms.Click(Id);
            yield return null;
            st.Life = st.LifeMax;
            HitFrom(1, 40);
            CharmCheck("举盾时摘下，盾立刻消失", st.LifeMax - st.Life == plain, $"受到 {st.LifeMax - st.Life}");
            CharmSkills.DevHeld = false;
            CharmEffects.DevAim = null;
            pd.isInvincible = true;
        }

        /// <summary>Ten hits of a damage on the dummy, what it took in all.</summary>
        int HitDummy(int damage, int times = 10)
        {
            var dummy = World.TargetDummy.DevHm;
            if (dummy == null) return 0;
            var col = dummy.GetComponent<Collider2D>();
            int before = World.TargetDummy.DevDamage;
            for (int i = 0; i < times; i++) mod.Melee.HitWith(col, damage, 0f, 0f, 1f, (Vector2)dummy.transform.position + Vector2.left);
            return World.TargetDummy.DevDamage - before;
        }

        IEnumerator FuryTest()
        {
            var st = mod.Stats;
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.Fury;
            int cd = CharmSkills.CooldownBase + Id;
            Player.TerraStats.DevImmortal = true;
            var dummy = World.TargetDummy.DevHm;
            if (dummy != null) { hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 6f); yield return new WaitForSeconds(0.6f); }

            // The low line: +25% to every class, nothing above it.
            WearOnly();
            st.Life = st.LifeMax;
            yield return null;
            var b0 = Equipment.Current;
            WearOnly(Id);
            yield return null;
            var bFull = Equipment.Current;
            st.Life = Mathf.FloorToInt(st.LifeMax * 0.3f);
            yield return null;
            yield return null;
            var bLow = Equipment.Current;
            CharmCheck("满血时没有加成", Mathf.Approximately(bFull.MeleeDamage, b0.MeleeDamage) && Mathf.Approximately(bFull.MinionDamage, b0.MinionDamage), $"近战 {b0.MeleeDamage:F2} → {bFull.MeleeDamage:F2}");
            CharmCheck("35% 以下四职业各加 25%", Mathf.Abs(bLow.MeleeDamage - b0.MeleeDamage - 0.25f) < 0.001f && Mathf.Abs(bLow.RangedDamage - b0.RangedDamage - 0.25f) < 0.001f
                && Mathf.Abs(bLow.MagicDamage - b0.MagicDamage - 0.25f) < 0.001f && Mathf.Abs(bLow.MinionDamage - b0.MinionDamage - 0.25f) < 0.001f,
                $"近战 {bLow.MeleeDamage - b0.MeleeDamage:F2} 远程 {bLow.RangedDamage - b0.RangedDamage:F2} 魔法 {bLow.MagicDamage - b0.MagicDamage:F2} 召唤 {bLow.MinionDamage - b0.MinionDamage:F2}");
            yield return new WaitForSeconds(1.2f);
            CharmShot("low");
            st.Life = Mathf.CeilToInt(st.LifeMax * 0.36f) + 1;
            yield return null;
            yield return null;
            CharmCheck("回到 35% 以上加成消失", Mathf.Approximately(Equipment.Current.MeleeDamage, b0.MeleeDamage), $"生命 {st.Life}");

            // The skill: 30% of the most life given up, the next two seconds doubled.
            st.Life = st.LifeMax;
            Player.PlayerBuffs.Remove(cd);
            int plain = HitDummy(100);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmShot("frenzy");
            CharmCheck("舍去 30% 最大生命", st.Life == st.LifeMax - Mathf.RoundToInt(st.LifeMax * 0.3f), $"生命 {st.Life}/{st.LifeMax}，舍去 {CharmEffects.DevFrenzyCost}");
            int doubled = HitDummy(100);
            CharmCheck("2 秒内伤害翻倍", plain > 0 && Mathf.Abs(doubled / (float)plain - 2f) < 0.25f, $"平时 10 下 {plain}，翻倍时 {doubled}（{doubled / Mathf.Max(1f, plain):F2} 倍）");
            yield return new WaitForSeconds(2.1f);
            int after = HitDummy(100);
            CharmCheck("2 秒后恢复", Mathf.Abs(after / (float)plain - 1f) < 0.2f, $"{after}（{after / Mathf.Max(1f, plain):F2} 倍）");

            // In its cooldown: nothing given up; past it, again.
            int lifeNow = st.Life, frenzies = CharmEffects.DevFrenzies;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("冷却中按下不扣血", st.Life == lifeNow && CharmEffects.DevFrenzies == frenzies, $"生命 {st.Life}");
            yield return new WaitForSeconds(8.2f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("10 秒后又能用", CharmEffects.DevFrenzies == frenzies + 1, $"生命 {lifeNow} → {st.Life}");

            // At the last of the life: costs nothing, works, never kills.
            Player.PlayerBuffs.Remove(cd);
            Player.TerraStats.DevImmortal = false;
            st.Life = 1;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("1 滴血时照样能用，不会自杀", st.Life == 1 && !HeroController.instance.cState.dead && CharmEffects.DevFrenzies == frenzies + 2, $"生命 {st.Life}");
            CharmShot("last");
            st.Life = 4;
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("4 滴血时最多扣到只剩 1 点", st.Life == 1 && !HeroController.instance.cState.dead, $"生命 {st.Life}");
            Player.TerraStats.DevImmortal = true;
            st.Life = st.LifeMax;
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Seconds from a hit to the first life coming back of itself (at most ten).</summary>
        IEnumerator RegenDelay(System.Action<float> done)
        {
            var st = mod.Stats;
            st.Life = st.LifeMax;
            st.TakeShare(30);
            int at = st.Life;
            float t0 = Time.time;
            while (st.Life <= at && Time.time - t0 < 10f) yield return null;
            done(Time.time - t0);
        }

        IEnumerator QuickFocusTest()
        {
            var st = mod.Stats;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.QuickFocus;
            Player.TerraStats.DevImmortal = true;
            WearOnly(Id);
            yield return null;
            CharmCheck("快速聚集 2 个凹槽、稳定之体 4 个", Charms.Cost(Id) == 2 && Charms.Cost(CharmEffects.SteadyBody) == 4, $"{Charms.Cost(Id)}、{Charms.Cost(CharmEffects.SteadyBody)}");
            CharmCheck("药水病系数：单戴 0.85，同戴炼金石 0.70", Mathf.Approximately(CharmEffects.SicknessFactor(false), 0.85f) && Mathf.Approximately(CharmEffects.SicknessFactor(true), 0.7f));

            // Sickness left rescaled as it goes on and off.
            st.PotionSickness = 0f;
            WearOnly();
            yield return null;
            st.PotionSickness = 60f;
            WearOnly(Id);
            yield return null;
            yield return null;
            float on = st.PotionSickness;
            WearOnly();
            yield return null;
            yield return null;
            float off = st.PotionSickness;
            CharmCheck("戴上摘下时剩余药水病按比例换算", Mathf.Abs(on - 60f * 0.85f) < 0.3f && Mathf.Abs(off - 60f) < 0.5f, $"60 → 戴上 {on:F1} → 摘下 {off:F1}");
            st.PotionSickness = 0f;

            // The natural regeneration: 4 s after a hit without it, 3 s with it.
            float plain = 0f, quick = 0f;
            yield return RegenDelay(v => plain = v);
            WearOnly(Id);
            yield return RegenDelay(v => quick = v);
            // The first whole life comes about a second after the regeneration starts, either way: the start is a second sooner.
            CharmCheck("受伤后自然回血早 1 秒开始", Mathf.Abs(plain - quick - 1f) < 0.2f, $"第一点生命：不戴 {plain:F2} 秒，戴着 {quick:F2} 秒");
            yield return new WaitForSeconds(0.6f);
            CharmShot("motes");
            CharmEffects.OnDrink(null);
            yield return new WaitForSeconds(0.15f);
            CharmShot("drink");

            // A full set with the Steady Body when the bag comes on: it comes off rather than overcharm.
            Charms.DevSetPage(false);
            yield return null;
            yield return null;
            CharmCheck("护符背包关掉时凹槽回到原版", Charms.Cost(Id) == 3 && Charms.Cost(CharmEffects.SteadyBody) == 1, $"{Charms.Cost(Id)}、{Charms.Cost(CharmEffects.SteadyBody)}");
            pd.charmSlots = 3;
            pd.SetBool("gotCharm_14", true);
            pd.SetBool("gotCharm_6", true);
            foreach (int id in new List<int>(pd.equippedCharms)) if (Charms.Worn(id)) Charms.Click(id);
            Charms.Click(CharmEffects.SteadyBody);
            Charms.Click(CharmEffects.Fury);
            CharmCheck("关着时装满 3 槽", Charms.Worn(CharmEffects.SteadyBody) && Charms.Worn(CharmEffects.Fury) && pd.charmSlotsFilled == 3 && !pd.overcharmed, $"{pd.charmSlotsFilled}/{pd.charmSlots}");
            Charms.DevSetPage(true);
            yield return null;
            yield return null;
            yield return null;
            CharmCheck("打开后稳定之体放不下，自动摘下，不会过载", !Charms.Worn(CharmEffects.SteadyBody) && Charms.Worn(CharmEffects.Fury) && !pd.overcharmed,
                $"凹槽 {pd.charmSlotsFilled}/{pd.charmSlots}，过载 {pd.overcharmed}");
            pd.charmSlots = 11;
            WearOnly();
        }

        IEnumerator LifebloodHeartTest()
        {
            var st = mod.Stats;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.LifebloodHeart;
            int cd = CharmSkills.CooldownBase + Id;
            Player.TerraStats.DevImmortal = true;
            WearOnly(Id);
            st.Life = st.LifeMax;
            st.Shield = 0;
            int quarter = Mathf.CeilToInt(st.LifeMax * 0.25f);
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("按下获得 25% 生命的护盾，冷却 60 秒", CharmEffects.DevHeart == quarter && Player.PlayerBuffs.TimeLeft(cd) / 60f > 59f, $"护盾 {CharmEffects.DevHeart}，冷却 {Player.PlayerBuffs.TimeLeft(cd) / 60f:F0} 秒");
            CharmCheck("生命栏的银心算上护盾", st.ShieldShown == quarter, $"显示 {st.ShieldShown}");
            yield return new WaitForSeconds(0.4f);
            CharmShot("shield");
            st.TakeShare(10);
            CharmCheck("小伤害只扣护盾", st.Life == st.LifeMax && CharmEffects.DevHeart == quarter - 10, $"生命 {st.Life}，护盾 {CharmEffects.DevHeart}");
            int breaks = CharmEffects.DevHeartBreaks;
            st.TakeShare(500);
            yield return null;
            CharmShot("break");
            CharmCheck("大伤害只能打碎护盾，伤不到生命", st.Life == st.LifeMax && CharmEffects.DevHeart == 0 && CharmEffects.DevHeartBreaks == breaks + 1, $"生命 {st.Life}，护盾 {CharmEffects.DevHeart}");
            st.TakeShare(20);
            CharmCheck("碎了之后照常掉血", st.Life == st.LifeMax - 20, $"生命 {st.Life}");

            // In its cooldown: nothing. Past it with some left: filled up, not stacked.
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("冷却中按不出护盾", CharmEffects.DevHeart == 0);
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            st.TakeShare(15);
            Player.PlayerBuffs.Remove(cd);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("再按只补满、不叠加", CharmEffects.DevHeart == quarter, $"护盾 {CharmEffects.DevHeart}");

            // The game's own lifeblood of the charm (at a bench): none with the bag.
            pd.UpdateBlueHealth();
            yield return null;
            yield return null;
            CharmCheck("原版坐长椅给的生命血没了", pd.healthBlue == 0 && st.Shield == 0, $"蓝面具 {pd.healthBlue}，生命血护盾 {st.Shield}");

            // Taken off: gone.
            Charms.Click(Id);
            yield return null;
            CharmCheck("摘下后护盾消失", CharmEffects.DevHeart == 0 && st.ShieldShown == 0, $"护盾 {CharmEffects.DevHeart}");
            st.Life = st.LifeMax;
        }

        IEnumerator LifebloodCoreTest()
        {
            var st = mod.Stats;
            const int Id = CharmEffects.LifebloodCore;
            Player.TerraStats.DevImmortal = true;
            WearOnly(Id);
            st.Life = st.LifeMax;
            st.Shield = 0;
            int layer = Mathf.CeilToInt(st.LifeMax * 0.1f);
            // Always filling (the user's 10-28 "持续回"): a layer every 5 s, a quarter as fast within 5 s of life lost.
            st.TakeShare(5);
            yield return new WaitForSeconds(4f);
            float hurt = CharmEffects.DevCore / (float)layer;
            CharmCheck("掉血后 5 秒内也在回，速度四分之一", hurt > 0.1f && hurt < 0.3f, $"4 秒回了 {hurt:0.00} 层（应约 0.2）");
            yield return new WaitForSeconds(1.2f);
            float at5 = CharmEffects.DevCore / (float)layer;
            yield return new WaitForSeconds(5f);
            float quiet = CharmEffects.DevCore / (float)layer - at5;
            CharmCheck("没掉血时 5 秒一层", quiet > 0.85f && quiet < 1.15f, $"5 秒回了 {quiet:0.00} 层");
            yield return new WaitForSeconds(10f);
            CharmCheck("最多 3 层", CharmEffects.DevCoreCount == 3 && CharmEffects.DevCore == 3 * layer, $"{CharmEffects.DevCoreCount} 层，{CharmEffects.DevCore}");
            CharmShot("three");

            // A hit on the shield is no life lost: it fills again at the whole speed.
            int life = st.Life;
            st.TakeShare(layer + 2);
            CharmCheck("打在护盾上不掉血", st.Life == life && CharmEffects.DevCore == 2 * layer - 2, $"生命 {st.Life}，护盾 {CharmEffects.DevCore}（{CharmEffects.DevCoreCount} 层）");
            yield return new WaitForSeconds(5.3f);
            CharmCheck("护盾被打不算掉血，5 秒补回一层", CharmEffects.DevCore >= 3 * layer - 2, $"护盾 {CharmEffects.DevCore}");

            // Life lost: a quarter as fast for 5 s.
            st.TakeShare(st.ShieldShown + 10);
            int core = CharmEffects.DevCore;
            CharmCheck("打穿护盾后掉血", st.Life < life && core == 0, $"生命 {st.Life}，护盾 {core}");
            yield return new WaitForSeconds(3f);
            float slow = CharmEffects.DevCore / (float)layer;
            CharmCheck("掉血后 3 秒按四分之一回", slow > 0.05f && slow < 0.22f, $"{slow:0.00} 层（应约 0.15）");
            yield return new WaitForSeconds(2.3f);
            CharmCheck("掉血 5 秒后恢复全速", CharmEffects.DevCore > 0);

            // With the Heart: stacked, the Core's outside.
            WearOnly(Id, CharmEffects.LifebloodHeart);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.LifebloodHeart);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            int heart = CharmEffects.DevHeart;
            int coreNow = CharmEffects.DevCore;
            CharmCheck("和生命血之心叠加", heart > 0 && coreNow > 0 && st.ShieldShown == heart + coreNow, $"心 {heart} + 核心 {coreNow} = 显示 {st.ShieldShown}");
            yield return new WaitForSeconds(0.5f);
            CharmShot("stacked");
            coreNow = CharmEffects.DevCore;
            st.TakeShare(coreNow + 5);
            CharmCheck("先扣核心再扣心", CharmEffects.DevCore <= 1 && CharmEffects.DevHeart == heart - 5 && st.Life == st.LifeMax, $"核心 {CharmEffects.DevCore}，心 {CharmEffects.DevHeart}，生命 {st.Life}");
            Charms.Click(Id);
            yield return null;
            CharmCheck("摘下核心，核心护盾消失、心还在", CharmEffects.DevCore == 0 && CharmEffects.DevHeart == heart - 5);
            WearOnly();
        }

        /// <summary>The ground's harm of a kind (2 spikes, 3 acid, 5 a pit) from an object of a name, and the game's own respawn after it.</summary>
        IEnumerator Hazard(int type, string name)
        {
            var hero = HeroController.instance;
            // The last hit's (or respawn's) immunity over first, or the game refuses this one.
            for (float w = Time.time + 4f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < w;) yield return null;
            int life0 = mod.Stats.Life, shelters0 = CharmEffects.DevShelters;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 地形伤害 {type} {name} 之前：可受伤 {CanBeHit(hero)}，无敌 {hero.cState.invulnerable}，危险复活中 {hero.cState.hazardRespawning}，玩家无敌 {PlayerData.instance.isInvincible}");
            var go = new GameObject(name);
            go.transform.position = (Vector2)hero.transform.position + Vector2.down * 0.5f;
            hero.TakeDamage(go, GlobalEnums.CollisionSide.bottom, 1, type);
            Object.Destroy(go);
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 地形伤害 {type} {name} 之后：生命 {life0} → {mod.Stats.Life}，庇护 +{CharmEffects.DevShelters - shelters0}");
            float t0 = Time.time;
            yield return new WaitForSeconds(0.3f);
            // A respawn under way (the fade, then the move to the safe spot) all done before going on.
            while ((!GameHooks.PlayerHasControl() || hero.cState.hazardDeath || hero.cState.hazardRespawning) && Time.time - t0 < 8f) yield return null;
            yield return new WaitForSeconds(0.6f);
        }

        static readonly System.Func<HeroController, bool> canTakeDamage =
            HarmonyLib.AccessTools.MethodDelegate<System.Func<HeroController, bool>>(HarmonyLib.AccessTools.Method(typeof(HeroController), "CanTakeDamage"));
        static bool CanBeHit(HeroController hero) => canTakeDamage(hero);

        IEnumerator DefenderTest()
        {
            var st = mod.Stats;
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.Defender;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            var dummy = World.TargetDummy.DevHm;
            if (dummy != null) { hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 3f); yield return new WaitForSeconds(0.8f); }
            hero.SetHazardRespawn(hero.transform.position, true);

            // Without it: the spikes hurt.
            WearOnly();
            st.Life = st.LifeMax;
            yield return Hazard(2, "Spikes test");
            CharmCheck("不戴时尖刺掉血", st.Life < st.LifeMax, $"生命 {st.Life}");
            st.Life = st.LifeMax;
            WearOnly(Id);
            yield return null;
            var dung = new List<string>();
            foreach (var t in HeroController.instance.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf("Dung", System.StringComparison.OrdinalIgnoreCase) >= 0) dung.Add($"{t.name}({t.gameObject.activeInHierarchy})");
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 原版粪雾物体：{(dung.Count > 0 ? string.Join("、", dung) : "无")}");

            // With it: spikes, thorns (no poison), acid: nothing, not even the putting back (the safe spot set far off shows it);
            // a pit: unharmed, put back all the same.
            CharmEffects.DevTrace = true;
            yield return new WaitForSeconds(1.5f);
            int walked = CharmEffects.DevWalked;
            hero = HeroController.instance;
            var from = (Vector2)hero.transform.position;
            var safe = Clear(hero, from + Vector2.left * 8f);
            hero.SetHazardRespawn(safe, true);
            yield return Hazard(2, "Spikes test");
            hero = HeroController.instance;
            CharmCheck("尖刺不掉血，也不会被送回去（能站在上面）", st.Life == st.LifeMax && CharmEffects.DevWalked > walked && Vector2.Distance(hero.transform.position, from) < 1.5f,
                $"生命 {st.Life}，位置 {from:F1} → {(Vector2)hero.transform.position:F1}");
            Player.PlayerBuffs.Remove(Player.Poison.Buff);
            yield return Hazard(2, "Thorn test");
            CharmCheck("荆棘不掉血也不中毒", st.Life == st.LifeMax && !Player.PlayerBuffs.Has(Player.Poison.Buff), $"生命 {st.Life}，中毒 {Player.PlayerBuffs.Has(Player.Poison.Buff)}");
            yield return Hazard(3, "Acid test");
            hero = HeroController.instance;
            CharmCheck("酸水不掉血，也不会被送回去", st.Life == st.LifeMax && Vector2.Distance(hero.transform.position, from) < 1.5f, $"生命 {st.Life}，位置 {(Vector2)hero.transform.position:F1}");
            CharmCheck("能在酸水里游", CharmEffects.AcidSwim);
            yield return Hazard(5, "Pit test");
            hero = HeroController.instance;
            CharmCheck("深坑不掉血，但照常被送回", st.Life == st.LifeMax && Vector2.Distance(hero.transform.position, safe) < 1.5f, $"生命 {st.Life}，位置 {(Vector2)hero.transform.position:F1}（安全点 {safe:F1}）");
            hero.transform.position = Clear(hero, from);
            yield return new WaitForSeconds(0.5f);

            // An enemy's hit: as ever.
            for (float w = Time.time + 4f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < w;) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 敌人攻击之前：可受伤 {CanBeHit(hero)}，无敌 {hero.cState.invulnerable}");
            HitFrom(1, 30);
            CharmCheck("敌人的攻击照常掉血", st.Life < st.LifeMax, $"生命 {st.Life}");
            st.Life = st.LifeMax;
            yield return new WaitForSeconds(1f);

            // By the dummy (as an enemy close by would be): a cloud within eight seconds, the dummy poisoned in it.
            if (dummy != null) { hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 1.1f); yield return new WaitForSeconds(0.3f); }
            int dungOn = 0;
            foreach (var t in HeroController.instance.GetComponentsInChildren<Transform>(true))
                if ((t.name == "Dung" || t.name == "Dung Recharge") && t.gameObject.activeSelf) dungOn++;
            CharmCheck("原版的粪雾关掉了", dungOn == 0, $"还开着 {dungOn} 个");
            int clouds = CharmEffects.DevClouds;
            float t1 = Time.time;
            while (CharmEffects.DevClouds == clouds && Time.time - t1 < 9f) yield return null;
            float waited = Time.time - t1;
            yield return new WaitForSeconds(0.6f);
            CharmShot("cloud");
            yield return new WaitForSeconds(0.4f);
            CharmCheck("敌人在附近时 4～8 秒冒一团毒雾", CharmEffects.DevClouds == clouds + 1 && waited <= 8.2f, $"等了 {waited:F1} 秒");
            CharmCheck("毒雾里的敌人中毒", dummy != null && EnemyDebuffs.Has(dummy, EnemyDebuffs.Poisoned), $"中毒 {dummy != null && EnemyDebuffs.Has(dummy, EnemyDebuffs.Poisoned)}");

            // Far from every enemy: no cloud.
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 40f);
            yield return new WaitForSeconds(0.5f);
            clouds = CharmEffects.DevClouds;
            yield return new WaitForSeconds(9f);
            CharmCheck("附近没有敌人时不冒毒雾", CharmEffects.DevClouds == clouds, $"冒了 {CharmEffects.DevClouds - clouds} 团");
            CharmEffects.DevPuffHere();
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            for (float w = Time.time + 6f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(0.5f);
            CharmCheck("换房间后旧房间的毒雾消失", CharmEffects.DevCloudsElsewhere == 0, $"旧房间还有 {CharmEffects.DevCloudsElsewhere} 团（新房间 {CharmEffects.DevCloudsUp - CharmEffects.DevCloudsElsewhere} 团）");
            pd.isInvincible = true;
        }

        IEnumerator SteadyTest()
        {
            var st = mod.Stats;
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            const int Id = CharmEffects.SteadyBody;
            int cd = CharmSkills.CooldownBase + Id;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            var rb = hero.GetComponent<Rigidbody2D>();
            var dummy = World.TargetDummy.DevHm;
            if (dummy != null) { hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 14f); yield return new WaitForSeconds(0.8f); }

            // Without it: a run builds up; with it too (only the inertia goes): stopped at once, turned at once at its speed.
            WearOnly();
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float plainStart = Mathf.Abs(rb.linearVelocity.x);
            yield return new WaitForSeconds(0.8f);
            float top = Mathf.Abs(rb.linearVelocity.x);
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(1f);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 6f);
            yield return new WaitForSeconds(0.4f);
            WearOnly(Id);
            yield return null;
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float steadyStart = rb.linearVelocity.x;
            CharmCheck("起步照常加速（不是一下满速）", Mathf.Abs(steadyStart - plainStart) < top * 0.15f, $"不戴两帧 {plainStart:F2}，戴着两帧 {steadyStart:F2}，满速 {top:F2}");
            yield return new WaitForSeconds(0.3f);
            float pre = rb.linearVelocity.x;
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float turned = rb.linearVelocity.x;
            CharmCheck("转身立刻转身，速度是转身前的", pre > 0.5f && turned < -pre * 0.9f, $"转身前 {pre:F2}，两帧后 {turned:F2}");
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float stopped = rb.linearVelocity.x;
            CharmCheck("松开立刻停下", Mathf.Abs(stopped) < 0.01f, $"两帧后 {stopped:F3}");
            CharmCheck("急停扬起一点灰", CharmEffects.DevSteadyDust > 0, $"{CharmEffects.DevSteadyDust} 次");

            // In the air: the same.
            hero.transform.position = (Vector2)hero.transform.position + Vector2.up * 3f;
            yield return new WaitForFixedUpdate();
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(0.25f);
            float airPre = rb.linearVelocity.x;
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float air = rb.linearVelocity.x;
            Player.TerraMovement.DevMove = 0;
            CharmCheck("空中转身也没有惯性", airPre > 0.5f && air < -airPre * 0.9f, $"转身前 {airPre:F2}，两帧后 {air:F2}");
            yield return new WaitForSeconds(1f);

            // A hit's knockback is kept a moment.
            for (float w = Time.time + 3f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < w;) yield return null;
            HitFrom(1, 10);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float thrown = rb.linearVelocity.x;
            CharmCheck("被打时的击退保留", thrown < -1f, $"两帧后 {thrown:F2}");
            yield return new WaitForSeconds(1.2f);

            // Outside the skill: a dash meets the hits.
            st.Life = st.LifeMax;
            Player.PlayerBuffs.Remove(cd);
            for (float w = Time.time + 3f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < w;) yield return null;
            Player.TerraMovement.ForceDash(1);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            bool dashing = Player.TerraMovement.IsDashing;
            HitFrom(1, 20);
            CharmCheck("平时冲刺照样挨打", dashing && st.Life < st.LifeMax, $"冲刺中 {dashing}，生命 {st.Life}");
            yield return new WaitForSeconds(1.2f);

            // The skill: three seconds of dashes going through.
            st.Life = st.LifeMax;
            for (float w = Time.time + 3f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < w;) yield return null;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("按下开启 3 秒，冷却 10 秒", CharmEffects.DashPassActive && Player.PlayerBuffs.TimeLeft(cd) / 60f > 9.5f, $"冷却 {Player.PlayerBuffs.TimeLeft(cd) / 60f:F1}");
            int passed = CharmEffects.DevPassed;
            Player.TerraMovement.ForceDash(1);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            HitFrom(1, 20);
            CharmShot("pass");
            CharmCheck("开启后冲刺中不挨打", st.Life == st.LifeMax && CharmEffects.DevPassed == passed + 1, $"生命 {st.Life}，穿过 {CharmEffects.DevPassed - passed}");
            yield return new WaitForSeconds(0.5f);
            HitFrom(1, 20);
            CharmCheck("开启后不冲刺照样挨打", st.Life < st.LifeMax, $"生命 {st.Life}");

            // Through the dummy without a bonk, while it lasts.
            if (dummy != null)
            {
                Player.PlayerBuffs.Remove(cd);
                CharmSkills.DevPress = true;
                yield return null;
                hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 2.2f);
                yield return new WaitForSeconds(0.2f);
                Player.TerraMovement.ForceDash(1);
                yield return new WaitForSeconds(0.25f);
                float through = rb.linearVelocity.x;
                float past = hero.transform.position.x - dummy.transform.position.x;
                CharmCheck("开启后冲刺穿过敌人，不会被撞回", through >= 0f, $"速度 {through:F2}，在假人右边 {past:F1}");
                yield return new WaitForSeconds(3.2f);
                hero.transform.position = Clear(hero, (Vector2)dummy.transform.position + Vector2.left * 2.2f);
                yield return new WaitForSeconds(0.3f);
                Player.TerraMovement.ForceDash(1);
                // The bonk throws the player back for a sixth of a second (then the Steady Body stops him at once): its lowest speed.
                float bonked = 0f;
                float until = Time.time + 0.3f;
                while (Time.time < until)
                {
                    bonked = Mathf.Min(bonked, rb.linearVelocity.x);
                    yield return new WaitForFixedUpdate();
                }
                CharmCheck("3 秒后冲刺又会撞上敌人", bonked < -1f, $"最低速度 {bonked:F2}");
            }
            WearOnly();
            pd.isInvincible = true;
        }

        /// <summary>Deep Focus: switched on and off by its skill (DevAutoTestCharmRound3).</summary>
        IEnumerator FocusModeTest()
        {
            yield return CharmPlace();
            yield return FocusToggleTest();
        }

        /// <summary>Several skill charms worn at once: one press each, the shell's press its own, the order of what takes a hit.</summary>
        IEnumerator CharmComboTest()
        {
            var st = mod.Stats;
            var pd = PlayerData.instance;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            WearOnly(CharmEffects.Stalwart, CharmEffects.Baldur, CharmEffects.Fury, CharmEffects.LifebloodHeart);
            foreach (int id in new[] { 4, 5, 6, 8 }) Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + id);
            CharmCheck("四个一起戴上", Charms.Worn(4) && Charms.Worn(5) && Charms.Worn(6) && Charms.Worn(8) && !pd.overcharmed, $"凹槽 {pd.charmSlotsFilled}/{pd.charmSlots}");
            st.Life = st.LifeMax;
            st.Shield = 0;
            int frenzies = CharmEffects.DevFrenzies;

            // The favourite's rules: worn with a skill only, one at a time.
            CharmSkills.Choose(0);
            pd.SetBool("gotCharm_7", true);
            InventoryGui.DevFavoriteCharm(7);
            CharmCheck("没装上的护符不能收藏", CharmSkills.Chosen == 0);
            pd.SetBool("gotCharm_3", true);
            Charms.Click(3);
            InventoryGui.DevFavoriteCharm(3);
            CharmCheck("没有护符技的护符不能收藏", Charms.Worn(3) && CharmSkills.Chosen == 0);
            Charms.Click(3);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("没有收藏时按下什么都不发动", CharmEffects.DevFrenzies == frenzies && !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + 4) && CharmEffects.DevHeart == 0);
            InventoryGui.DevFavoriteCharm(CharmEffects.Fury);
            CharmCheck("收藏亡者之怒", CharmSkills.Chosen == CharmEffects.Fury);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("按下只发动收藏的那一个", CharmEffects.DevFrenzies == frenzies + 1 && !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + 4) && CharmEffects.DevHeart == 0
                && !Player.PlayerBuffs.Has(CharmSkills.CooldownBase + 5), $"舍血 {CharmEffects.DevFrenzies - frenzies}，外壳 {Player.PlayerBuffs.Has(CharmSkills.ActiveBase + 4)}，心 {CharmEffects.DevHeart}");
            InventoryGui.DevFavoriteCharm(CharmEffects.LifebloodHeart);
            CharmCheck("改收藏生命血之心，原来的收藏取消", CharmSkills.Chosen == CharmEffects.LifebloodHeart);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("现在按下发动生命血之心", CharmEffects.DevHeart > 0 && CharmEffects.DevFrenzies == frenzies + 1, $"心 {CharmEffects.DevHeart}");
            InventoryGui.DevFavoriteCharm(CharmEffects.LifebloodHeart);
            CharmCheck("再点一次取消收藏", CharmSkills.Chosen == 0);
            InventoryGui.DevFavoriteCharm(CharmEffects.Stalwart);
            int heart = CharmEffects.DevHeart, life = st.Life;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("收藏坚硬外壳后按下张开外壳", Player.PlayerBuffs.Has(CharmSkills.ActiveBase + 4));
            for (float w = Time.time + 2f; (HeroController.instance.cState.invulnerable || !CanBeHit(HeroController.instance)) && Time.time < w;) yield return null;
            HitFrom(1, 40);
            CharmCheck("外壳先吸收，心的护盾和生命不动", CharmEffects.DevHeart == heart && st.Life == life, $"心 {CharmEffects.DevHeart}/{heart}，生命 {st.Life}/{life}");
            // Fury ready again, the shell holding: the press is the shell's alone.
            yield return new WaitForSeconds(3.2f);
            frenzies = CharmEffects.DevFrenzies;
            int waves = CharmEffects.DevWaves;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("再按一下放出光波", CharmEffects.DevWaves == waves + 1 && CharmEffects.DevFrenzies == frenzies, $"光波 {CharmEffects.DevWaves - waves}，舍血 {CharmEffects.DevFrenzies - frenzies}");
            Charms.Click(CharmEffects.Stalwart);
            yield return null;
            CharmCheck("摘下收藏的护符，收藏一起清掉", CharmSkills.Chosen == 0);
            WearOnly();
            st.Life = st.LifeMax;
            pd.isInvincible = true;
        }

        IEnumerator WatchDeadHeals(System.Action dead, System.Action<int> heals, int from)
        {
            while (true)
            {
                var hero = HeroController.SilentInstance;
                if (hero != null && hero.cState.dead) { dead(); heals(CharmEffects.DevGrubHeals - from); }
                yield return null;
            }
        }
    }
}
