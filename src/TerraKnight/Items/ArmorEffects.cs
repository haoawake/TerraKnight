using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The hardmode armor sets' bonuses that act rather than add (accarmor-hm sec-H2, Terraria
    /// px y down, 60 Hz): Player.OnHit's hub (Palladium's Rapid Healing, Orichalcum's petals,
    /// Titanium's shards, Hallowed's Holy Protection, Chlorophyte's extra leaf), the Frost set's
    /// frostburn, the Spectre hood's and mask's bolts, the Nebula boosters, the Beetle orbs, the
    /// Solar shields, the Shroomite and Vortex stealth, the Stardust Guardian, the Forbidden
    /// storm, and the sets' part in getting hurt (the dodge, the Solar flare, Beetle Endurance,
    /// the Turtle's thorns).
    /// </summary>
    internal static class ArmorEffects
    {
        public const int RapidHealing = 58, HolyProtection = 59, LeafCrystal = 60, BeetleEndurance = 95, BeetleMight = 98, SolarShield = 170,
            NebulaLife = 173, NebulaMana = 176, NebulaDamage = 179, StardustGuardian = 187, TitaniumStorm = 306;

        /// <summary>What dealt the hit <see cref="MeleeHits"/> is about to report: its class, a swing, a projectile's damage and hits so far, a set's own shot.</summary>
        public struct Hit
        {
            public DamageClass Class;
            public bool TrueMelee, FromProjectile, Suppress, NoGhost, Whip;
            public int ProjDamage, ProjHits;
        }

        /// <summary>Set by whoever deals the next hit; read and cleared by <see cref="OnHitNPC"/>.</summary>
        public static Hit Context;

        static ArmorSet Set => Equipment.FullSet;
        static bool Hallowed => Set == ArmorSet.Hallowed || Set == ArmorSet.HallowedSummoner;
        public static bool Chlorophyte => Set == ArmorSet.Chlorophyte || Set == ArmorSet.ChlorophyteMelee || Set == ArmorSet.ChlorophyteSummon;
        static bool Spectre => Set == ArmorSet.SpectreHealing || Set == ArmorSet.SpectreDamage;

        // Player.petalTimer (Orichalcum and the leaf share it), titaniumStormCooldown, shadowDodgeTimer, crystalLeafCooldown, nebulaCD.
        static int petalTimer, titaniumCooldown, shadowDodgeTimer, nebulaCD;
        public static int CrystalLeafCooldown;
        // Player.lifeSteal (Master: +0.5 a tick up to 70) and ghostDmg (−6.667 a tick).
        static float lifeSteal = 70f, ghostDmg;

        /// <summary>The Beetle orbs (beetleOrbs) and their counters; their places and speeds round the player (px).</summary>
        public static int BeetleOrbs;
        static int beetleCounter, beetleCountdown;
        public static readonly Vector2[] BeetlePos = new Vector2[3], BeetleVel = new Vector2[3];
        public static int BeetleFrame;

        /// <summary>The Solar shields (solarShields) and their counter; shield places off the player (px); the dash's flare spent.</summary>
        public static int SolarShields;
        static int solarCounter;
        public static readonly Vector2[] SolarPos = new Vector2[3];
        static readonly Vector2[] solarVel = new Vector2[3];
        public static bool SolarDashing;
        static bool consumedFlare;

        /// <summary>Player.stealth (1 seen .. 0 hidden) for the Shroomite and Vortex sets; vortexStealthActive.</summary>
        public static float Stealth = 1f;
        public static bool VortexStealth;

        /// <summary>The Nebula boosters' levels (0-3): life, mana, damage.</summary>
        public static int NebulaLifeLevel => Level(NebulaLife);
        public static int NebulaManaLevel => Level(NebulaMana);
        public static int NebulaDamageLevel => Level(NebulaDamage);

        /// <summary>Dev counters.</summary>
        public static int DevPetals, DevShards, DevLeafShots, DevHolyDodges, DevHeals, DevWraths, DevBoosters, DevThorns, DevFlares, DevStorms, DevGuardianHits;

        static int miscCounter;

        // ------------------------------------------------------------------ the hit hub (Player.OnHit, Pl:6912-7033)

        /// <summary>
        /// After any hit of the player's lands on an enemy (MeleeHits.Deal): the sets' on-hit
        /// effects. dealt is the hit's damage after defense; centre the enemy's (world).
        /// </summary>
        public static void OnHitNPC(HealthManager hm, Vector2 centre, int dealt, Hit ctx)
        {
            if (hm == null) return;
            // The flask (meleeEnchant, StatusToNPC / Projectile.StatusNPC): melee swings and shots, and whips.
            if (ctx.Class == DamageClass.Melee || ctx.Whip) Player.PotionBuffs.Imbue(hm, centre);
            var info = EnemyStats.Of(hm);
            // npc.life > 5, and nothing for the sets' own shots.
            if (info == null || EnemyStats.LifeMax(info) <= 5) return;
            var set = Set;
            if (set == ArmorSet.None) return;
            var at = TerraParticles.ToPx(centre);
            var tp = TerraProjectiles.Instance;
            var hero = HeroController.SilentInstance;
            var player = hero != null ? TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center) : at;

            // The Frost set: frostburn from melee and ranged (StatusToNPC Pl:6547-6550, P:11525-11528).
            if (set == ArmorSet.Frost && (ctx.Class == DamageClass.Melee || ctx.Class == DamageClass.Ranged))
                EnemyDebuffs.Add(hm, EnemyDebuffs.Frostbite, 60 * Random.Range(5, 15));
            // Beetle Might's gains (Pl:44824-44828, P:13919-13933): a swing all it dealt, a melee shot 3/2/1 times by the orbs.
            if (set == ArmorSet.BeetleDamage && ctx.Class == DamageClass.Melee)
            {
                beetleCounter += ctx.TrueMelee ? dealt : dealt * (BeetleOrbs == 0 ? 3 : BeetleOrbs == 1 ? 2 : 1);
                beetleCountdown = 0;
            }
            // The Spectre and Nebula sets: a magic projectile's hit (flag10 and canGhostHeal, P:13870-13891).
            if (ctx.FromProjectile && ctx.Class == DamageClass.Magic && !ctx.NoGhost && dealt > 0)
            {
                if (set == ArmorSet.SpectreHealing) GhostHeal(at, dealt, ctx.ProjHits);
                if (set == ArmorSet.SpectreDamage) GhostHurt(hm, at, dealt, ctx.ProjDamage);
                if (set == ArmorSet.Nebula && nebulaCD == 0 && Random.Range(0, 3) == 0)
                {
                    nebulaCD = 30;
                    int dir = at.x >= player.x ? 1 : -1;
                    tp?.ArmorNebulaBooster(at, new Vector2(Random.Range(10, 31) * 0.2f * dir, Random.Range(-20, 1) * 0.2f), Random.Range(0, 3));
                    DevBoosters++;
                }
            }
            if (ctx.Suppress || tp == null) return;

            // Titanium (Pl:6918-6936): a shard on the player, at most seven, every 10 ticks; Titanium Barrier 10 s.
            if (set == ArmorSet.Titanium && titaniumCooldown == 0)
            {
                titaniumCooldown = 10;
                PlayerBuffs.Add(TitaniumStorm, 600);
                if (tp.ArmorCount(ProjKind.TitaniumShard) < 7)
                {
                    tp.ArmorSpawn(ProjKind.TitaniumShard, player, Vector2.zero, 50, 15f);
                    DevShards++;
                }
            }
            // Hallowed (Pl:6937-6940): Holy Protection for 30 s, unless one was spent less than 30 s ago.
            if (Hallowed && shadowDodgeTimer == 0) PlayerBuffs.Add(HolyProtection, 1800);
            // Palladium (Pl:6941-6944): Rapid Healing 5 s.
            if (set == ArmorSet.Palladium) PlayerBuffs.Add(RapidHealing, 300);
            // Orichalcum (Pl:6963-6985): a petal from the screen's edge behind the facing, at most one every 20 ticks.
            if (set == ArmorSet.Orichalcum && petalTimer == 0 && ScreenPx(out var view))
            {
                petalTimer = 20;
                bool facingLeft = TerraMod.Instance != null && TerraMod.Instance.UseDirection < 0f;
                float sx = view.xMin + (facingLeft ? view.width : 0f), sy = view.yMin + Random.value * view.height;
                var d = new Vector2(at.x + Random.Range(-50, 51) * 0.1f - sx, at.y + Random.Range(-50, 51) * 0.1f - sy);
                tp.ArmorSpawn(ProjKind.OrichalcumPetal, new Vector2(sx, sy), d.normalized * 24f, 36, 0f);
                DevPetals++;
            }
            // Chlorophyte (Pl:6986-7031): the crystal shoots at what was hit too, within 1200 px, at most every 50 ticks.
            if (Chlorophyte && petalTimer == 0 && tp.ArmorCrystal(out var crystal) && Vector2.Distance(crystal, at) <= 1200f)
            {
                petalTimer = 50;
                tp.ArmorLeafShot(crystal - new Vector2(4f, 0f), hm);
                DevLeafShots++;
            }
        }

        /// <summary>Takes a heal out of Player.lifeSteal (vampireHeal, P:12111-12120): returns the amount actually reserved, never more than the remaining budget.</summary>
        public static int TakeLifeSteal(float heal)
        {
            int amount = Mathf.Min(Mathf.Max(0, (int)heal), Mathf.Max(0, (int)lifeSteal));
            lifeSteal -= amount;
            return amount;
        }

        /// <summary>ghostHeal (P:12077-12110): a share of the damage (less for each later hit of a piercing shot) out of the life-steal budget, carried back by a spirit.</summary>
        static void GhostHeal(Vector2 at, int dealt, int hits)
        {
            float f = 0.2f - 0.05f * hits;
            if (f <= 0f) return;
            float heal = dealt * f;
            if ((int)heal <= 0 || lifeSteal <= 0f) return;
            lifeSteal -= heal;
            TerraProjectiles.Instance?.ArmorSpiritHeal(at, (int)heal);
        }

        /// <summary>ghostHurt (P:12021-12075): a wrath bolt at another enemy near the one hit, worth the shot's damage, within 1500 of a budget falling 400 a second.</summary>
        static void GhostHurt(HealthManager hit, Vector2 at, int dealt, int projDamage)
        {
            if (projDamage <= 0 || dealt <= 1 || ghostDmg > 1500f) return;
            ghostDmg += projDamage;
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var a = Random.value * Mathf.PI * 2f;
            tp.ArmorWrath(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f, projDamage, hit);
            DevWraths++;
        }

        /// <summary>The camera's view at the gameplay plane, in Terraria px (y down).</summary>
        static bool ScreenPx(out Rect view)
        {
            view = default;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return false;
            float d = -cam.transform.position.z;
            var bl = TerraParticles.ToPx(cam.ViewportToWorldPoint(new Vector3(0f, 0f, d)));
            var tr = TerraParticles.ToPx(cam.ViewportToWorldPoint(new Vector3(1f, 1f, d)));
            view = Rect.MinMaxRect(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y), Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
            return true;
        }

        // ------------------------------------------------------------------ once a tick

        /// <summary>One 60 Hz tick (from TerraProjectiles.Tick): the timers, the sets' buffs and entities, the orbs and shields, stealth.</summary>
        public static void Tick()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || TerraMod.Instance == null) return;
            miscCounter = (miscCounter + 1) % 300;
            if (petalTimer > 0) petalTimer--;
            if (titaniumCooldown > 0) titaniumCooldown--;
            if (shadowDodgeTimer > 0) shadowDodgeTimer--;
            if (CrystalLeafCooldown > 0) CrystalLeafCooldown--;
            lifeSteal = Mathf.Min(70f, lifeSteal + 0.5f);
            ghostDmg = Mathf.Max(0f, ghostDmg - 6.667f);
            var set = Set;
            var tp = TerraProjectiles.Instance;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var centre = TerraParticles.ToPx(box.center);
            var rb = hero.GetComponent<Rigidbody2D>();
            var v = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / TerraMovement.PxT : Vector2.zero;

            // Taking the set off clears what it keeps up (Pl:25900-25903, 16428-16450).
            if (!Hallowed) PlayerBuffs.Remove(HolyProtection);
            if (!Chlorophyte) PlayerBuffs.Remove(LeafCrystal);
            else PlayerBuffs.Add(LeafCrystal, 18000);
            if (set != ArmorSet.Stardust) PlayerBuffs.Remove(StardustGuardian);
            else
            {
                PlayerBuffs.Add(StardustGuardian, 18000);
                if (tp != null && tp.ArmorCount(ProjKind.StardustGuardian) < 1) tp.ArmorSpawn(ProjKind.StardustGuardian, centre, new Vector2(0f, -1f), 30, 10f);
            }
            if (Chlorophyte && tp != null && tp.ArmorCount(ProjKind.CrystalLeaf) < 1) tp.ArmorSpawn(ProjKind.CrystalLeaf, centre + new Vector2(0f, -60f), Vector2.zero, 0, 0f);
            if (set == ArmorSet.Nebula && nebulaCD > 0) nebulaCD--;
            NebulaDecay();

            BeetleTick(set, v);
            SolarTick(set, hero, centre);
            NebulaDust(centre);
            StealthTick(set, v, centre);
            // The Forbidden set's light (ASB:61-66).
            if (set == ArmorSet.Forbidden) FxBatch.Instance?.Light(centre, new Color(0.8f, 0.7f, 0.2f), 3f);
        }

        /// <summary>Beetle Endurance's orbs every 3 s up to three; Beetle Might's from what melee deals, draining (Pl:16613-16695); the orbs' wander (Pl:16554-16611).</summary>
        static void BeetleTick(ArmorSet set, Vector2 v)
        {
            if (set == ArmorSet.BeetleDefense)
            {
                if (++beetleCounter >= 180)
                {
                    if (BeetleOrbs < 3)
                    {
                        BeetleOrbs++;
                        beetleCounter = 0;
                    }
                    else beetleCounter = 180;
                }
                SetLevelBuff(BeetleEndurance, BeetleOrbs);
                PlayerBuffs.Remove(BeetleMight); PlayerBuffs.Remove(BeetleMight + 1); PlayerBuffs.Remove(BeetleMight + 2);
            }
            else if (set == ArmorSet.BeetleDamage)
            {
                beetleCounter -= 3 + beetleCountdown / 20;
                beetleCountdown++;
                beetleCounter = Mathf.Clamp(beetleCounter, 0, 6400);
                int level = beetleCounter > 5200 ? 3 : beetleCounter > 1600 ? 2 : beetleCounter > 400 ? 1 : 0;
                if (level < BeetleOrbs) beetleCountdown = 0;
                if (level > BeetleOrbs) beetleCounter += 200;
                BeetleOrbs = level;
                SetLevelBuff(BeetleMight, BeetleOrbs);
                PlayerBuffs.Remove(BeetleEndurance); PlayerBuffs.Remove(BeetleEndurance + 1); PlayerBuffs.Remove(BeetleEndurance + 2);
            }
            else
            {
                BeetleOrbs = 0;
                beetleCounter = 0;
                for (int i = 0; i < 3; i++)
                {
                    PlayerBuffs.Remove(BeetleEndurance + i);
                    PlayerBuffs.Remove(BeetleMight + i);
                }
                return;
            }
            for (int i = 0; i < BeetleOrbs; i++)
            {
                BeetlePos[i] += BeetleVel[i];
                BeetleVel[i] += new Vector2(Random.Range(-100, 101) * 0.005f, Random.Range(-100, 101) * 0.005f);
                float d = BeetlePos[i].magnitude;
                if (d > 100f) BeetleVel[i] = (BeetleVel[i] * 9f - BeetlePos[i] / d * 20f) / 10f;
                else if (d > 30f) BeetleVel[i] = (BeetleVel[i] * 19f - BeetlePos[i] / d * 10f) / 20f;
                if (BeetleVel[i].magnitude > 2f) BeetleVel[i] *= 0.9f;
                BeetlePos[i] -= v * 0.25f;
            }
            BeetleFrame = (BeetleFrame + 1) % 3;
        }

        /// <summary>The buff of a 1-3 level family (Beetle orbs) held at 5 ticks; the others of the family off.</summary>
        static void SetLevelBuff(int first, int level)
        {
            for (int i = 0; i < 3; i++)
                if (i != level - 1) PlayerBuffs.Remove(first + i);
            if (level > 0) PlayerBuffs.Add(first + level - 1, 5);
        }

        /// <summary>ApplySetBonus_Solar (Pl:16479-16552): a shield every 3 s up to three, bobbing in front of the player.</summary>
        static void SolarTick(ArmorSet set, HeroController hero, Vector2 centre)
        {
            if (set != ArmorSet.Solar)
            {
                SolarShields = 0;
                solarCounter = 0;
                SolarDashing = false;
                for (int i = 0; i < 3; i++) PlayerBuffs.Remove(SolarShield + i);
                return;
            }
            if (++solarCounter >= 180)
            {
                if (SolarShields < 3)
                {
                    SolarShields++;
                    var p = TerraParticles.Instance;
                    var topLeft = TerraMovement.TopLeft(hero);
                    for (int k = 0; k < 16 && p != null; k++)
                    {
                        int d = p.NewDust(topLeft, 20, 42, 6, 0f, 0f, 100, 1.7f);
                        if (d < 0) continue;
                        p[d].NoGravity = true;
                        p[d].FadeIn = 0.5f;
                        p[d].Velocity *= 5f;
                    }
                    solarCounter = 0;
                }
                else solarCounter = 180;
            }
            SetLevelBuff(SolarShield, SolarShields);
            float dir = SolarFacing(hero);
            for (int l = 0; l < SolarShields; l++)
            {
                SolarPos[l] += solarVel[l];
                var goal = new Vector2(dir * 20f, Mathf.Sin(miscCounter / 100f * Mathf.PI * 2f + l * Mathf.PI * 2f / SolarShields) * 6f);
                solarVel[l] = (goal - SolarPos[l]) * 0.2f;
            }
            if (!TerraMovement.IsDashing)
            {
                SolarDashing = false;
                consumedFlare = false;
            }
        }

        internal static int SolarFacing(HeroController hero)
        {
            var mod = TerraMod.Instance;
            return mod != null && mod.IsUsingItem ? (mod.UseDirection < 0f ? -1 : 1) : (hero.cState.facingRight ? 1 : -1);
        }

        /// <summary>ConsumeSolarFlare (Pl:57416-57436): a shield spent.</summary>
        static bool ConsumeSolarFlare()
        {
            if (Set != ArmorSet.Solar || SolarShields <= 0) return false;
            SolarShields--;
            solarCounter = 0;
            SetLevelBuff(SolarShield, SolarShields);
            DevFlares++;
            return true;
        }

        /// <summary>Shroomite (Pl:25980-26021): hidden standing or moving slowly; Vortex (Pl:26022-26091): hidden while toggled on.</summary>
        static void StealthTick(ArmorSet set, Vector2 v, Vector2 centre)
        {
            if (set != ArmorSet.Vortex) VortexStealth = false;
            // Holding the Psycho Knife, its stealth runs instead (Pl:25928: the Shroomite one is the else).
            if (MeleeSpecials.PsychoHeld && set != ArmorSet.Vortex) Stealth = 1f;
            else if (set == ArmorSet.Shroomite)
            {
                const float thr = 3f - 0.1f;
                if (Mathf.Abs(v.x) < thr && Mathf.Abs(v.y) < thr) Stealth = Mathf.Max(0f, Stealth - 0.015f);
                else Stealth = Mathf.Min(1f, Stealth + (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.0075f);
            }
            else if (set == ArmorSet.Vortex)
            {
                float before = Stealth;
                Stealth = VortexStealth ? Mathf.Max(0f, Stealth - 0.04f) : Mathf.Min(1f, Stealth + 0.04f);
                if (Stealth != before) VortexDust(centre);
            }
            else Stealth = 1f;
        }

        /// <summary>The Vortex stealth's swirl while it changes: dust 229 and 240 spinning off opposite ways.</summary>
        static void VortexDust(Vector2 centre)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            float a = Random.value * Mathf.PI * 2f;
            var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            if (Random.Range(0, 2) == 0)
            {
                int d = p.NewDust(centre, 0, 0, 229);
                if (d >= 0)
                {
                    p[d].Position = centre - u * Random.Range(5f, 10f);
                    p[d].Velocity = new Vector2(-u.y, u.x) * 4f;
                    p[d].Scale = 0.5f + Random.value;
                    p[d].FadeIn = 0.5f;
                    p[d].NoGravity = true;
                }
            }
            if (Random.Range(0, 2) == 0)
            {
                int d = p.NewDust(centre, 0, 0, 240);
                if (d >= 0)
                {
                    p[d].Position = centre - u * 12f;
                    p[d].Velocity = new Vector2(u.y, -u.x) * 2f;
                    p[d].Scale = 0.5f + Random.value;
                    p[d].FadeIn = 0.5f;
                    p[d].NoGravity = true;
                }
            }
        }

        /// <summary>The Shroomite stealth's share (1 − k): full at 0.75 or less (Remap(stealth, 1 → 0.75, 0 → 1)).</summary>
        static float ShroomiteShare => Mathf.Clamp01((1f - Stealth) / 0.25f);

        /// <summary>The armor key (double-tapping down, Pl:57438-57461): the Vortex stealth toggle; the Forbidden storm at the cursor.</summary>
        /// <summary>The worn set does something on a double tap down (the Vortex's stealth, the Forbidden storm).</summary>
        public static bool DownTapAbility => Set == ArmorSet.Vortex || Set == ArmorSet.Forbidden;

        public static void KeyDoubleTapDown()
        {
            if (Set == ArmorSet.Vortex)
            {
                VortexStealth = !VortexStealth;
                return;
            }
            if (Set == ArmorSet.Forbidden) TerraProjectiles.Instance?.ArmorForbiddenStorm();
        }

        // ------------------------------------------------------------------ the Nebula boosters

        static int Level(int first)
        {
            for (int i = 2; i >= 0; i--) if (PlayerBuffs.Has(first + i)) return i + 1;
            return 0;
        }

        /// <summary>NebulaLevelup (Pl:57707-57736): the family's level up by one (at most three), 8 s.</summary>
        public static void NebulaLevelUp(int kind)
        {
            int first = kind == 0 ? NebulaDamage : kind == 1 ? NebulaLife : NebulaMana;
            int level = Mathf.Min(3, Level(first) + 1);
            for (int i = 0; i < 3; i++) PlayerBuffs.Remove(first + i);
            PlayerBuffs.Add(first + level - 1, 480);
        }

        /// <summary>Pl:12157-12167: a level a buff's end, 8 s each.</summary>
        static void NebulaDecay()
        {
            foreach (int first in new[] { NebulaLife, NebulaMana, NebulaDamage })
            {
                int level = Level(first);
                if (level <= 1) continue;
                int id = first + level - 1;
                if (PlayerBuffs.TimeLeft(id) > 2) continue;
                PlayerBuffs.Remove(id);
                PlayerBuffs.Add(id - 1, 480);
            }
        }

        // ------------------------------------------------------------------ what the sets add this frame

        /// <summary>The changing bonuses (Beetle Might, the stealth's ranged share, the Nebula damage and life boosters), added after the set's own.</summary>
        public static Bonuses DynamicBonuses()
        {
            var b = new Bonuses();
            if (Set == ArmorSet.BeetleDamage && BeetleOrbs > 0)
            {
                b.MeleeDamage = 0.1f * BeetleOrbs;
                b.MeleeSpeed = 0.1f * BeetleOrbs;
            }
            if (Set == ArmorSet.Shroomite)
            {
                float s = ShroomiteShare;
                b.RangedDamage = s * 0.5f;
                b.RangedCrit = (int)(s * 10f) / 100f;
            }
            if (Set == ArmorSet.Vortex && VortexStealth)
            {
                b.RangedDamage = (1f - Stealth) * 0.8f;
                b.RangedCrit = (int)((1f - Stealth) * 20f) / 100f;
            }
            int dmg = NebulaDamageLevel;
            if (dmg > 0)
            {
                b.MeleeDamage += 0.15f * dmg;
                b.RangedDamage += 0.15f * dmg;
                b.MagicDamage += 0.15f * dmg;
                b.MinionDamage += 0.15f * dmg;
            }
            b.LifeRegen += 6 * NebulaLifeLevel;
            return b;
        }

        /// <summary>lifeRegen the sets add outside the gate of time since a hit: Rapid Healing's 4 (2 HP a second, Pl:19382-19386).</summary>
        public static int RegenBonus => PlayerBuffs.Has(RapidHealing) ? 4 : 0;

        /// <summary>The Vortex stealth's run speeds ×0.3 (Pl:26022-26091).</summary>
        public static float RunSpeedMult => Set == ArmorSet.Vortex && VortexStealth ? 0.3f : 1f;

        /// <summary>dashType after the set (sets apply after accessories): the Crystal Assassin's 5, the Solar set's 3 while shielded or dashing.</summary>
        public static int DashKind(int fromAccessories)
        {
            if (Set == ArmorSet.CrystalAssassin) return 5;
            if (Set == ArmorSet.Solar && (SolarShields > 0 || (SolarDashing && TerraMovement.IsDashing))) return 3;
            return fromAccessories;
        }

        /// <summary>A dash starts: the Solar one is the solar dash; any dash ends the Vortex stealth (Pl:21554).</summary>
        public static void OnDash(int kind)
        {
            VortexStealth = false;
            if (kind == 3) SolarDashing = true;
        }

        // ------------------------------------------------------------------ getting hurt

        /// <summary>Holy Protection (Pl:38514-38518, 38306-38327): the next hit missed, then 30 s before another.</summary>
        public static bool TryHolyDodge()
        {
            if (!PlayerBuffs.Has(HolyProtection)) return false;
            PlayerBuffs.Remove(HolyProtection);
            shadowDodgeTimer = 1800;
            DevHolyDodges++;
            return true;
        }

        /// <summary>The next hit is an enemy's or a projectile's (ImmunityCooldownID General), which the Solar flare answers.</summary>
        public static bool CounterHit;

        /// <summary>
        /// After defense and endurance (Pl:38589-38623): a Solar shield spent for 20% off and a
        /// flare; each Beetle Endurance orb 15% off, one spent.
        /// </summary>
        /// <summary>Read-only preview; observing the next hit must never spend shields or orbs.</summary>
        public static float PreviewHurtMultiplier(bool enemyHit) =>
            (enemyHit && Set == ArmorSet.Solar && SolarShields > 0 ? 0.8f : 1f)
            * (Set == ArmorSet.BeetleDefense && BeetleOrbs > 0 ? 1f - 0.15f * BeetleOrbs : 1f);

        public static int ModifyHurt(int damage)
        {
            bool counter = CounterHit;
            CounterHit = false;
            if (counter && ConsumeSolarFlare())
            {
                damage = (int)(0.8f * damage);
                TerraProjectiles.Instance?.ArmorSolarBurst(MeleeFlareDamage());
            }
            if (Set == ArmorSet.BeetleDefense && BeetleOrbs > 0)
            {
                damage = (int)((1f - 0.15f * BeetleOrbs) * damage);
                BeetleOrbs--;
                beetleCounter = 0;
                SetLevelBuff(BeetleEndurance, BeetleOrbs);
            }
            return Mathf.Max(1, damage);
        }

        /// <summary>The flare's and the solar dash's 150 × meleeDamage.</summary>
        public static int MeleeFlareDamage() => (int)(150f * (1f + Equipment.Current.MeleeDamage));

        /// <summary>Any real hit: the stealth is gone (Pl:38528).</summary>
        public static void AfterHurt() => Stealth = 1f;

        /// <summary>
        /// The Turtle's thorns (Pl:31586-31641): an enemy touching the player takes twice its
        /// contact damage (±15%, at most 1000) back, knocked away, dodged or not.
        /// </summary>
        public static void OnContact(HealthManager attacker, int raw)
        {
            // The Thorns potion sends back a third as much (thorns 1 against the Turtle set's 2).
            float thorns = Mathf.Max(Set == ArmorSet.Turtle ? 2f : 0f, Equipment.Current.Thorns);
            bool cactus = Equipment.Current.CactusThorns;
            if ((thorns <= 0f && !cactus) || attacker == null || attacker.isDead || raw <= 0) return;
            var col = attacker.GetComponent<Collider2D>() ?? attacker.GetComponentInChildren<Collider2D>();
            var hero = HeroController.SilentInstance;
            var melee = TerraMod.Instance?.Melee;
            if (col == null || hero == null || melee == null) return;
            Vector2 c = hero.GetComponent<BoxCollider2D>().bounds.center;
            float dir = col.bounds.center.x >= c.x ? 1f : -1f;
            if (thorns > 0f)
            {
                int dmg = Mathf.Min(1000, (int)(raw * Random.Range(0.85f, 1.15f) * thorns));
                melee.HitWith(col, dmg, 10f, 0f, dir, c);
                DevThorns++;
            }
            // The Cactus set (cactusThorns, Pl:31642-31654): 15 back on every touch, 30 in Expert, 45 in Master.
            if (cactus && !attacker.isDead)
            {
                var d = World.GameDifficulty.Current;
                melee.HitWith(col, d >= World.DifficultyLevel.Master ? 45 : d == World.DifficultyLevel.Expert ? 30 : 15, 10f, 0f, dir, c);
                DevCactus++;
            }
        }

        /// <summary>Dev: the Cactus set's touches answered.</summary>
        public static int DevCactus;

        /// <summary>
        /// The solar dash's touch (Pl:21291-21348), each tick of the dash: the first enemy met
        /// spends a shield; each takes 150 × meleeDamage (melee crit) with knockback 9 and a flare,
        /// and is left alone 6 ticks; the player gets 4 ticks of immunity.
        /// </summary>
        public static bool SolarDashTouch(HeroController hero, float vx)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return false;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var rb = hero.GetComponent<Rigidbody2D>();
            Vector2 c = (Vector2)b.center + (rb != null ? rb.linearVelocity / 60f * 0.5f : Vector2.zero);
            bool any = false;
            foreach (var h in Physics2D.OverlapBoxAll(c, (Vector2)b.size + new Vector2(8f, 8f) * TerraParticles.Px, 0f, 1 << 11))
            {
                var hm = h.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead) continue;
                int id = hm.GetInstanceID();
                if (solarImmune.TryGetValue(id, out float until) && Time.time < until) continue;
                if (!consumedFlare)
                {
                    consumedFlare = true;
                    ConsumeSolarFlare();
                }
                float crit = 4f + (Equipment.Current.AllCrit + Equipment.Current.MeleeCrit) * 100f;
                melee.HitWith(h, MeleeFlareDamage(), 9f, crit, vx >= 0f ? 1f : -1f, b.center);
                TerraProjectiles.Instance?.ArmorSolarBurst(MeleeFlareDamage());
                solarImmune[id] = Time.time + 6f / 60f;
                any = true;
            }
            return any;
        }

        static readonly Dictionary<int, float> solarImmune = new Dictionary<int, float>();

        /// <summary>
        /// The boosters' rings (PDS:1380-1431): a dust per level round the player, life (90) at
        /// 10 px, mana (88) at 20 the other way, damage (86) at 30, a turn every 180 ticks.
        /// </summary>
        static void NebulaDust(Vector2 centre)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            float turn = miscCounter / 180f * Mathf.PI * 2f;
            foreach (var (level, dust, r, sign) in new[] { (NebulaLifeLevel, 90, 10f, 1f), (NebulaManaLevel, 88, 20f, -1f), (NebulaDamageLevel, 86, 30f, 1f) })
                for (int i = 0; i < level; i++)
                {
                    float a = sign * turn + i * Mathf.PI * 2f / level;
                    int d = p.NewDust(centre, 0, 0, dust, 0f, 0f, 100, 1.5f);
                    if (d < 0) continue;
                    p[d].Position = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    p[d].Velocity = Vector2.zero;
                    p[d].NoGravity = true;
                }
        }

        /// <summary>
        /// The Solar shield (PDL:3130-3146): shield 0's place only, Extra_61/62/63 by how many,
        /// (255,255,255,127), tipped toward where it bobs; the Beetle orbs (PDL:4130-4156): their
        /// frame of three with five fading copies trailing back, over everything.
        /// </summary>
        public static void Draw(FxBatch fx, Vector2 centrePx, int dir, float opacity)
        {
            if (fx == null) return;
            bool previous=fx.WeaponEffect; fx.WeaponEffect=true;
            try { DrawEffects(fx,centrePx,dir,opacity); }
            finally { fx.WeaponEffect=previous; }
        }

        static void DrawEffects(FxBatch fx, Vector2 centrePx, int dir, float opacity)
        {
            if (fx == null || opacity <= 0f) return;
            if (SolarShields > 0)
            {
                string tex = "Extra_" + (60 + SolarShields);
                var whole = FxBatch.Whole(tex);
                var p0 = SolarPos[0];
                float rot = Mathf.Atan2(p0.y * 0.5f, p0.x) + (dir < 0 ? Mathf.PI : 0f) + Mathf.PI / 50f * dir;
                if (whole != null) fx.Draw(whole, centrePx + p0, new Color(1f, 1f, 1f, 127f / 255f) * opacity, rot, 1f, flipX: dir < 0, overPlayer: true);
            }
            for (int i = 0; i < BeetleOrbs; i++)
            {
                var spr = FxBatch.Frame("BeetleOrb", 0, 22 * BeetleFrame + 1, 26, 20, 13f, 11f);
                for (int j = 5; j >= 0; j--)
                {
                    float k = j == 0 ? 1f : 0.5f - 0.1f * j;
                    if (k <= 0f) continue;
                    fx.Draw(spr, centrePx + BeetlePos[i] - BeetleVel[i] * j, new Color(k, k, k, k) * opacity, 0f, 1f, overPlayer: true);
                }
            }
        }

        /// <summary>Leaving a scene: the sets' entities and counters start over.</summary>
        public static void Clear()
        {
            BeetleOrbs = 0;
            SolarShields = 0;
            Stealth = 1f;
            VortexStealth = false;
            solarImmune.Clear();
        }
    }
}
