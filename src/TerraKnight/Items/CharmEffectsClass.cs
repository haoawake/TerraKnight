using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The class and gear charms (护符设计记录.md):
    /// Shaman Stone (19): magic +15%, every magic shot 30% bigger, its hit and its picture (a shot drawn its own way through
    /// FxBatch.Grow, a beam thicker, an area and a blast wider), a faint violet glow round it.
    /// Soul Catcher (20): geo +20%; melee and ranged hits steal back 3% of their damage (at most 4 life a second), each
    /// heal carried in as a pale wisp. Soul Eater (21): geo +30%; its skill swaps two modes (3 s): the whip's (minions
    /// −40%, whips ×2) and the minions' (minions +40%, no weapon can be used), a small mote above the head in its colour.
    /// Fragile / Unbreakable Heart (23): the masks' cyan life twice over (a broken one nothing). Fragile Greed (24): the
    /// game's own, its 20% more geo glinting. Joni's Blessing (27): the run halved, every class +35%, a pool of pale blue
    /// light underfoot. Spell Twister (33): mana −10%, magic uses 30% quicker, a third of the ammo kept, and ranged hits
    /// throw a small seeking orb of magic (at most four a second, a quarter of the hit). Kingsoul / Void Heart (36): magic
    /// costs half the mana, white motes off each cast. Sprintmaster (37): running boots at their top speed at once (none
    /// without boots), faint wind streaks behind at that speed.
    /// The game's own +2 masks of the Fragile Heart and Joni's single mask are left out while the charm bag is on.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int ShamanStone = 19, SoulCatcher = 20, SoulEater = 21, FragileHeart = 23, FragileGreed = 24, JonisBlessing = 27,
            SpellTwister = 33, Kingsoul = 36, Sprintmaster = 37;
        const float ShamanMagic = 0.15f, ShamanSize = 1.3f, CatcherGeo = 0.2f, EaterGeo = 0.3f, CatcherSteal = 0.03f, CatcherPerSecond = 4f;
        const float JoniDamage = 0.35f, JoniSpeed = 0.5f, TwisterMana = 0.9f, TwisterSpeed = 1.3f, TwisterAmmo = 0.33f, KingsoulMana = 0.5f;
        const float EaterCooldown = 3f, EaterWeak = 0.6f, EaterStrong = 1.4f, EaterWhip = 2f, OrbShare = 0.25f, OrbGap = 0.25f, OrbLife = 1.6f;
        static readonly Color ShamanColor = new Color(0.66f, 0.45f, 1f), SoulColor = new Color(0.8f, 0.92f, 1f), EaterWhipColor = new Color(1f, 0.6f, 0.25f),
            EaterMinionColor = new Color(0.35f, 0.9f, 1f), JoniColor = new Color(0.55f, 0.78f, 1f), OrbColor = new Color(0.55f, 0.55f, 1f);
        public static int DevStolen, DevOrbs, DevOrbHits, DevLocked;

        /// <summary>A fragile charm broken gives nothing (the game's own rule); every other worn charm works.</summary>
        static bool Whole(int id)
        {
            if (!On(id)) return false;
            var pd = PlayerData.instance;
            if (pd == null) return true;
            return id == 23 ? !pd.brokenCharm_23 : id == 24 ? !pd.brokenCharm_24 : id == 25 ? !pd.brokenCharm_25 : true;
        }

        static void AddClassBonuses(ref Bonuses b)
        {
            if (On(JonisBlessing)) { b.MeleeDamage += JoniDamage; b.RangedDamage += JoniDamage; b.MagicDamage += JoniDamage; b.MinionDamage += JoniDamage; }
            if (On(ShamanStone)) b.MagicDamage += ShamanMagic;
            if (On(SpellTwister)) b.AmmoSave = 1f - (1f - b.AmmoSave) * (1f - TwisterAmmo);
        }

        // ------------------------------------------------------------------ the numbers asked for

        /// <summary>A magic shot's size (TerraProjectiles.Spawn): Shaman Stone's 30% more.</summary>
        public static float SpellSize => On(ShamanStone) ? ShamanSize : 1f;

        /// <summary>The mana a use costs (Equipment.ManaCost): Spell Twister's −10%, Kingsoul's half for magic.</summary>
        public static float ManaFactor(ItemDef item) => (On(SpellTwister) ? TwisterMana : 1f) * (On(Kingsoul) && item != null && item.Class == DamageClass.Magic ? KingsoulMana : 1f);

        /// <summary>A magic weapon's use time (Equipment.UseScale): Spell Twister's 30% quicker.</summary>
        public static float MagicUseScale => On(SpellTwister) ? 1f / TwisterSpeed : 1f;

        /// <summary>The run (TerraMovement): Joni's Blessing halves both its speeds, the boots' too.</summary>
        public static float RunSpeedMult => On(JonisBlessing) ? JoniSpeed : 1f;

        /// <summary>Sprintmaster: running boots go at their top speed at once.</summary>
        public static bool InstantSprint => On(Sprintmaster);

        /// <summary>A death's geo (EnemyDrops): Soul Catcher's +20%, Soul Eater's +30%.</summary>
        public static int ScaleGeo(int geo)
        {
            float k = 1f + (On(SoulCatcher) ? CatcherGeo : 0f) + (On(SoulEater) ? EaterGeo : 0f);
            return geo <= 0 || k == 1f ? geo : Mathf.RoundToInt(geo * k);
        }

        /// <summary>The HUD's cyan hearts: the Fragile Heart's wave passing through them (0..1 for the one at this place).</summary>
        public static float MaskWave(int index, int count)
        {
            if (count <= 0 || !Whole(FragileHeart)) return 0f;
            float p = Mathf.Repeat(Time.unscaledTime / 2.8f, 1f) * (count + 4f) - 2f;
            float k = Mathf.Clamp01(1f - Mathf.Abs(index - p) / 1.2f);
            return k * k * (3f - 2f * k);
        }

        /// <summary>TerraStats.UpdateMaximums: the Fragile Heart's masks' life twice more, three times in all (beyond the cap: the charm's own).</summary>
        public static int MaskLifeBonus(int maskLife) => Whole(FragileHeart) ? maskLife * 2 : 0;

        // ------------------------------------------------------------------ Soul Eater's modes

        /// <summary>1: the whip's (minions −40%, whips ×2); 2: the minions' (minions +40%, no weapon).</summary>
        static int eaterMode = 1, eaterShown;
        static float eaterSwitchAt = -10f;

        /// <summary>A minion's or sentry's damage (Minions): Soul Eater's mode on it.</summary>
        public static float MinionScale => !On(SoulEater) ? 1f : eaterMode == 2 ? EaterStrong : EaterWeak;

        /// <summary>A weapon's damage (Equipment.Damage): a whip doubled in Soul Eater's whip mode.</summary>
        public static int WhipDamage(ItemDef item, int damage) => item != null && item.IsWhip && On(SoulEater) && eaterMode == 1 ? damage * (int)EaterWhip : damage;

        /// <summary>In Soul Eater's minion mode no weapon can be used (tools, summoning staffs and sentries can).</summary>
        public static bool WeaponLocked(ItemDef def)
        {
            // In the spores: no weapon at all (Unn's light can strike).
            if (def != null && def.IsWeapon && Spored) return true;
            if (!On(SoulEater) || eaterMode != 2 || def == null || !def.IsWeapon || def.IsSummon || def.Sentry) return false;
            return def.PickPower <= 0 && def.HammerPower <= 0 && def.AxePower <= 0;
        }

        static float lockedNoticeAt = -10f;

        /// <summary>A use refused by the minion mode: said once in a while.</summary>
        public static void SayLocked()
        {
            DevLocked++;
            if (Time.unscaledTime - lockedNoticeAt < 2f) return;
            lockedNoticeAt = Time.unscaledTime;
            UI.CenterNotice.Show(Spored ? Lang.T("这个形态下不能攻击") : Lang.T("噬魂者：召唤物模式下不能用武器攻击"));
        }

        static void SwitchEater(HeroController hero)
        {
            eaterMode = eaterMode == 1 ? 2 : 1;
            eaterSwitchAt = Time.time;
            CharmSkills.Cool(SoulEater, EaterCooldown);
            Mod.PlaySound("Item_" + (eaterMode == 2 ? "44" : "152"), 0.45f);
            Plugin.Log.LogInfo($"[护符] 噬魂者切到{(eaterMode == 2 ? "召唤物模式（召唤物 +40%，不能用武器）" : "鞭子模式（召唤物 −40%，鞭子 ×2）")}");
        }

        public static int DevEaterMode { get => eaterMode; set => eaterMode = value; }
        public static int DevWhipGlows, DevMinionHalos;

        /// <summary>The whip mode: the whips (Whips.DrawOnTop) give off a faint glow along their length.</summary>
        public static bool EaterWhipGlow => On(SoulEater) && eaterMode == 1;

        /// <summary>The minion mode: the minions (Minions.DrawMinions) wear a halo.</summary>
        public static bool EaterMinionHalo => On(SoulEater) && eaterMode == 2;

        /// <summary>A whip drawn in the whip mode: soft amber light along its chain (Terraria pixels), brighter toward the tip.</summary>
        public static void EaterGlowWhip(FxBatch fx, List<Vector2> points, bool over)
        {
            int n = points.Count;
            if (fx == null || n < 2) return;
            float breath = 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
            for (int i = 1; i < n; i += 2)
            {
                float along = i / (float)(n - 1);
                fx.Draw(Dot, points[i], Glow(EaterWhipColor, (0.16f + 0.14f * along) * breath), 0f, 0.55f + 0.25f * along, overPlayer: over);
            }
            fx.Draw(Dot, points[n - 1], Glow(EaterWhipColor, 0.35f * breath), 0f, 0.9f, overPlayer: over);
            DevWhipGlows++;
        }

        /// <summary>A minion drawn in the minion mode: a cyan halo round it (its middle, its size in Terraria pixels).</summary>
        public static void EaterHalo(FxBatch fx, Vector2 centre, float size, int seed)
        {
            if (fx == null) return;
            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 3.2f + seed * 1.7f);
            float r = Mathf.Clamp(size, 14f, 90f);
            fx.Draw(Dot, centre, Glow(EaterMinionColor, 0.3f * breath), 0f, r * 1.7f / 32f);
            fx.Draw(Ring, centre, Glow(EaterMinionColor, 0.32f * breath), 0f, r * 1.25f / 64f);
            DevMinionHalos++;
        }

        // ------------------------------------------------------------------ the player's hits

        static float catcherPool, catcherPayAt;
        // A rolling one-second limit: idle time must not bank an eight-point opening heal.
        static readonly Queue<(float At, int Amount)> catcherPayments = new Queue<(float, int)>();
        static int catcherWindowUsed;
        static Vector2 catcherFrom;
        static float orbAt = -10f;

        /// <summary>
        /// Every hit the player deals that an enemy took (MeleeHits.Deal, after its number): Soul Catcher's theft from melee
        /// and ranged hits, Spell Twister's orb from ranged ones. Not a minion's or sentry's.
        /// </summary>
        public static void OnPlayerHit(HealthManager hm, Vector2 at, int damage, ArmorEffects.Hit ctx, bool autonomous)
        {
            if (!Charms.Enabled || autonomous || hm == null || damage <= 0) return;
            bool melee = ctx.Class == DamageClass.Melee, ranged = ctx.Class == DamageClass.Ranged;
            if (On(SoulCatcher) && (melee || ranged))
            {
                var st = Stats;
                if (st != null && st.Life < st.LifeMax)
                {
                    catcherPool = Mathf.Min(10f, catcherPool + damage * CatcherSteal);
                    catcherFrom = at;
                }
            }
            if (On(SpellTwister) && ranged && Time.time - orbAt >= OrbGap)
            {
                orbAt = Time.time;
                Orb(at, Mathf.Max(1, Mathf.RoundToInt(damage * OrbShare)), hm);
            }
        }

        static void TickCatcher(HeroController hero)
        {
            while (catcherPayments.Count > 0 && Time.time - catcherPayments.Peek().At >= 1f)
                catcherWindowUsed -= catcherPayments.Dequeue().Amount;
            var st = Stats;
            if (!On(SoulCatcher) || st == null || !Playing(hero) || st.Life >= st.LifeMax) { catcherPool = 0f; return; }
            int available = (int)CatcherPerSecond - catcherWindowUsed;
            if (Time.time < catcherPayAt || catcherPool < 1f || available <= 0) return;
            catcherPayAt = Time.time + 0.25f;
            int heal = Mathf.Min(Mathf.FloorToInt(catcherPool), available);
            catcherPool -= heal;
            int healed = st.Heal(heal, recovery: true);
            if (healed <= 0) return;
            catcherPayments.Enqueue((Time.time, healed));
            catcherWindowUsed += healed;
            DevStolen += healed;
            UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, healed.ToString(), UI.CombatText.Heal);
            wisps.Add((catcherFrom, Time.time, SceneNow));
        }

        /// <summary>Soul Catcher's wisps flying in: from where, since when.</summary>
        static readonly List<(Vector2 From, float Time, string Scene)> wisps = new List<(Vector2, float, string)>();

        // ------------------------------------------------------------------ Spell Twister's orbs

        sealed class Orb_
        {
            public Vector2 At, Vel;
            public float Born;
            public int Damage;
            public HealthManager Target;
            public string Scene;
            public readonly Vector2[] Trail = new Vector2[5];
        }
        static readonly List<Orb_> orbs = new List<Orb_>();
        static readonly List<HealthManager> orbFound = new List<HealthManager>();

        static void Orb(Vector2 at, int damage, HealthManager struck)
        {
            // Another enemy near the one hit if there is one, else that one again.
            HealthManager target = struck;
            float best = float.MaxValue;
            foreach (var hm in AccessoryEffects.Enemies(at, 400f, orbFound))
            {
                if (hm == struck) continue;
                float d = ((Vector2)hm.transform.position - at).sqrMagnitude;
                if (d < best && !WallBetween(at, hm.transform.position, out _)) { best = d; target = hm; }
            }
            float a = Random.Range(0f, Mathf.PI * 2f);
            var o = new Orb_ { At = at, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, Born = Time.time, Damage = damage, Target = target, Scene = SceneNow };
            for (int k = 0; k < o.Trail.Length; k++) o.Trail[k] = at;
            orbs.Add(o);
            DevOrbs++;
        }

        static void TickOrbs()
        {
            string scene = SceneNow;
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                var o = orbs[i];
                if (o.Scene != scene || Time.time - o.Born > OrbLife) { orbs.RemoveAt(i); continue; }
                if (o.Target == null || o.Target.isDead)
                {
                    o.Target = null;
                    foreach (var hm in AccessoryEffects.Enemies(o.At, 400f, orbFound)) { o.Target = hm; break; }
                }
                // Seeking: turned toward the target harder as it goes, faster as it goes.
                if (o.Target != null)
                {
                    var c = TargetCentre(o.Target);
                    var want = (c - o.At).normalized * Mathf.Lerp(10f, 22f, (Time.time - o.Born) / OrbLife);
                    o.Vel = Vector2.Lerp(o.Vel, want, Mathf.Clamp01(Time.deltaTime * 7f));
                }
                var next = o.At + o.Vel * Time.deltaTime;
                if (WallBetween(o.At, next, out _)) { orbs.RemoveAt(i); Pop(o.At, OrbColor); continue; }
                for (int k = o.Trail.Length - 1; k > 0; k--) o.Trail[k] = o.Trail[k - 1];
                o.Trail[0] = o.At;
                o.At = next;
                foreach (var hm in AccessoryEffects.Enemies(o.At, 10f, orbFound))
                {
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Magic, FromProjectile = true };
                    if (Mod.Melee.HitWith(col, o.Damage, 0f, Equipment.Current.MagicCrit * 100f + 4f, o.Vel.x >= 0f ? 1f : -1f, o.At)) DevOrbHits++;
                    Pop(o.At, OrbColor);
                    orbs.RemoveAt(i);
                    break;
                }
            }
        }

        static Vector2 TargetCentre(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
        }

        /// <summary>A small burst of a colour's sparks (an orb or a creature spent).</summary>
        static void Pop(Vector2 at, Color c)
        {
            pops.Add((at, c, Time.time, SceneNow));
        }

        static readonly List<(Vector2 At, Color C, float Time, string Scene)> pops = new List<(Vector2, Color, float, string)>();

        // ------------------------------------------------------------------ casting

        static readonly List<(Vector2 At, Vector2 Vel, float Time, string Scene)> motes = new List<(Vector2, Vector2, float, string)>();

        /// <summary>A magic weapon used (TerraMod.Use): Kingsoul's white motes off the hand.</summary>
        public static void OnMagicUse(HeroController hero, ItemDef def, Vector2 aim)
        {
            if (!On(Kingsoul) || hero == null || def == null || def.Class != DamageClass.Magic) return;
            var from = Centre(hero);
            var dir = (aim - from).sqrMagnitude > 0.01f ? (aim - from).normalized : Vector2.right;
            for (int k = 0; k < 3; k++)
            {
                var v = (dir + Random.insideUnitCircle * 0.6f).normalized * Random.Range(1.2f, 2.2f) + Vector2.up * 0.8f;
                motes.Add((from + dir * 0.5f, v, Time.time, SceneNow));
            }
        }

        static void TickClass(HeroController hero)
        {
            TickCatcher(hero);
            TickOrbs();
            string scene = SceneNow;
            for (int i = wisps.Count - 1; i >= 0; i--) if (Time.time - wisps[i].Time > 0.35f || wisps[i].Scene != scene) wisps.RemoveAt(i);
            for (int i = pops.Count - 1; i >= 0; i--) if (Time.time - pops[i].Time > 0.3f || pops[i].Scene != scene) pops.RemoveAt(i);
            for (int i = motes.Count - 1; i >= 0; i--) if (Time.time - motes[i].Time > 0.7f || motes[i].Scene != scene) motes.RemoveAt(i);
            // The mode in the buff bar (made again only as it changes or goes missing: no strings a frame).
            if (On(SoulEater))
            {
                if (eaterShown != eaterMode || !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + SoulEater))
                {
                    eaterShown = eaterMode;
                    CharmSkills.Hold(SoulEater, Charms.Name(SoulEater) + Lang.T(eaterMode == 2 ? "：召唤物模式" : "：鞭子模式"),
                        Lang.T(eaterMode == 2 ? "召唤物伤害提高 40%，不能用武器攻击" : "召唤物伤害降低 40%，鞭子伤害翻倍"));
                }
            }
            else if (eaterShown != 0)
            {
                eaterShown = 0;
                CharmSkills.EndActive(SoulEater);
            }
        }

        // ------------------------------------------------------------------ the looks

        static Rigidbody2D heroBody;

        static void DrawClass(FxBatch fx, Vector2 c, float opacity)
        {
            float t = Time.time;
            var hero = HeroController.SilentInstance;
            if (On(JonisBlessing))
            {
                // A pool of pale blue light underfoot, breathing slowly.
                fx.OnTop = true;
                float k = (0.42f + 0.1f * Mathf.Sin(t * 1.6f)) * opacity;
                fx.Draw(Dot, c + new Vector2(0f, 20f), Glow(JoniColor, k), 0f, new Vector2(1.5f, 0.32f), overPlayer: false);
                fx.Draw(Dot, c + new Vector2(0f, 20f), Glow(Color.white, 0.4f * k), 0f, new Vector2(0.7f, 0.12f), overPlayer: false);
            }
            if (On(SoulEater))
            {
                // A small mote circling above the head in the mode's colour; a ring as it changes.
                var col = eaterMode == 2 ? EaterMinionColor : EaterWhipColor;
                var at = c + new Vector2(Mathf.Cos(t * 2.2f) * 9f, -30f + Mathf.Sin(t * 4.4f) * 2f);
                fx.OnTop = true;
                fx.Draw(Dot, at, Glow(col, 0.55f * opacity), 0f, 0.42f, overPlayer: true);
                fx.Draw(Dot, at, Glow(Color.white, 0.5f * opacity), 0f, 0.14f, overPlayer: true);
                float sw = Mathf.Clamp01(1f - (t - eaterSwitchAt) / 0.4f);
                if (sw > 0f) fx.Draw(Ring, c, Glow(col, 0.8f * sw * opacity), 0f, Mathf.Lerp(20f, 64f, 1f - sw) / 64f, overPlayer: true);
            }
            if (On(Sprintmaster) && hero != null)
            {
                if (heroBody == null || heroBody.gameObject != hero.gameObject) heroBody = hero.GetComponent<Rigidbody2D>();
                float top = Player.TerraMovement.AccRunSpeed * Player.TerraMovement.PxT;
                if (heroBody != null && hero.cState.onGround && top > 3.1f * Player.TerraMovement.PxT && Mathf.Abs(heroBody.linearVelocity.x) >= top * 0.95f)
                {
                    // Faint wind streaks behind the runner.
                    fx.OnTop = true;
                    float dir = Mathf.Sign(heroBody.linearVelocity.x);
                    for (int i = 0; i < 3; i++)
                    {
                        float ph = Mathf.Repeat(t * 3f + i * 0.33f, 1f);
                        var at = c + new Vector2(-dir * (14f + ph * 26f), -12f + i * 10f);
                        fx.Draw(Dot, at, Glow(Color.white, 0.4f * (1f - ph) * opacity), 0f, new Vector2(1.3f, 0.08f), overPlayer: false);
                    }
                }
            }
        }

        static void DrawClassWorld(FxBatch fx)
        {
            if (orbs.Count == 0 && wisps.Count == 0 && pops.Count == 0 && motes.Count == 0) return;
            bool top = fx.OnTop;
            fx.OnTop = true;
            float t = Time.time;
            foreach (var o in orbs)
            {
                var px = TerraParticles.ToPx(o.At);
                for (int k = 1; k < o.Trail.Length; k++)
                    fx.Draw(Dot, TerraParticles.ToPx(o.Trail[k]), Glow(OrbColor, 0.35f * (1f - k / (float)o.Trail.Length)), 0f, 0.3f);
                fx.Draw(Dot, px, Glow(OrbColor, 0.8f), 0f, 0.55f);
                fx.Draw(Dot, px, Glow(Color.white, 0.7f), 0f, 0.2f);
                fx.Light(px, OrbColor * 0.6f, 2.5f);
            }
            var hero = HeroController.SilentInstance;
            if (hero != null)
            {
                var to = TerraParticles.ToPx(Centre(hero));
                foreach (var w in wisps)
                {
                    // A pale wisp from the struck enemy into the player.
                    float k = Mathf.Clamp01((t - w.Time) / 0.35f);
                    var from = TerraParticles.ToPx(w.From);
                    var at = Vector2.Lerp(from, to, k * k) + new Vector2(0f, -Mathf.Sin(k * Mathf.PI) * 18f);
                    fx.Draw(Dot, at, Glow(SoulColor, 0.8f * (1f - k * 0.5f)), 0f, 0.45f);
                    fx.Draw(Dot, at, Glow(Color.white, 0.6f), 0f, 0.15f);
                }
            }
            foreach (var p in pops)
            {
                float k = Mathf.Clamp01((t - p.Time) / 0.3f);
                var px = TerraParticles.ToPx(p.At);
                fx.Draw(Ring, px, Glow(p.C, 0.8f * (1f - k)), 0f, Mathf.Lerp(6f, 26f, k) / 64f);
                fx.Draw(Dot, px, Glow(Color.white, 0.6f * (1f - k) * (1f - k)), 0f, 0.5f);
            }
            foreach (var m in motes)
            {
                float age = t - m.Time, k = Mathf.Clamp01(age / 0.7f);
                var px = TerraParticles.ToPx(m.At + m.Vel * age);
                fx.Draw(Dot, px, Glow(Color.white, 0.85f * (1f - k)), 0f, 0.36f);
                fx.Draw(Dot, px, Glow(new Color(0.9f, 0.95f, 1f), 0.3f * (1f - k)), 0f, 0.8f);
            }
            fx.OnTop = top;
        }

        /// <summary>A magic shot Shaman Stone made bigger (TerraProjectiles.DrawOne): a faint violet glow round it.</summary>
        public static void DrawSpellAura(FxBatch fx, Vector2 px, float sizePx)
        {
            if (fx == null) return;
            fx.Draw(Dot, px, Glow(ShamanColor, 0.22f), 0f, Mathf.Clamp(sizePx * 1.6f / 32f, 0.5f, 4f));
        }
    }
}
