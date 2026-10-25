using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The nail's charms (护符设计记录.md):
    /// Mark of Pride (13): melee weapons and the nail's slashes half again as long, a faint violet crescent at each swing's reach.
    /// Longnail (18): ×1.15 as long, and through walls: melee and the nail reach only what the player can see (Terraria's
    /// Player.CanHit), Longnail anything, a silver glint where a blow went through the wall.
    /// Heavy Blow (15): the nail art's charge goes on past ready for 1.5 s more, its art up to +60% damage, +35% size and
    /// twice the knockback, a deepening ember glow while it builds and a ring as the art goes.
    /// Unbreakable Strength (25): melee +10%, the nail +30%; a spark of embers where the nail strikes.
    /// Nailmaster's Glory (26): the charge 30% quicker, the wait between slashes 30% shorter; gold motes drawn in as it charges.
    /// Quick Slash (32): no recovery after a slash (the swing over as its slash is out); each nail hit cuts again a moment
    /// later for 30% of it, always a crit, a silver cross where it cuts (the arts: CharmEffectsArts).
    /// Grubberfly's Elegy (35): every slash throws a silver crescent of light its way for half its damage, through enemies
    /// (the arts: CharmEffectsArts).
    /// Sharp Shadow (16): the shadow dash cuts what it goes through for one and a half nail blows (the Shield's bonk's 30
    /// without it) and runs at the game's DASH_SPEED_SHARP; a dark cut on each enemy it goes through.
    /// The game's own Quick Slash cooldown and Grubberfly beams are switched off while the charm bag is on.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int MarkOfPride = 13, HeavyBlow = 15, SharpShadow = 16, Longnail = 18, UnbreakableStrength = 25, NailmastersGlory = 26,
            QuickSlash = 32, GrubberflysElegy = 35;
        const float PrideReach = 1.5f, LongnailReach = 1.15f, StrengthMelee = 0.1f, StrengthNail = 0.3f, GloryCharge = 1.3f, GloryCooldown = 0.7f;
        const float QuickShare = 0.3f, QuickDelay = 0.07f, HeavyExtra = 1.5f, HeavyDamage = 0.6f, HeavyReach = 0.35f, HeavyKnock = 1f;
        const float BeamShare = 0.5f, BeamSpeed = 26f, BeamLife = 0.5f, BeamRadius = 0.7f, SharpShare = 1.5f;
        /// <summary>Quick Slash's and Grubberfly's silver: its light and its deep tone (drawn covering, not added).</summary>
        static readonly Color SilverLight = new Color(0.86f, 0.89f, 0.96f), SilverDeep = new Color(0.55f, 0.6f, 0.7f);
        static readonly Color PrideColor = new Color(0.72f, 0.5f, 1f), LongnailColor = new Color(0.82f, 0.9f, 1f), StrengthColor = new Color(1f, 0.6f, 0.2f),
            GloryColor = new Color(1f, 0.85f, 0.38f), HeavyColor = new Color(1f, 0.4f, 0.14f), SharpColor = new Color(0.5f, 0.3f, 0.85f);
        public static int DevWallStops, DevPierces, DevQuickHits, DevQuickDealt, DevQuickBase, DevBeams, DevBeamHits, DevSharpHits;
        public static float DevHeavyArt, DevLastArtScale = 1f;

        // ------------------------------------------------------------------ reach

        /// <summary>The worn charms' length on melee weapons (Equipment.ItemScale) and the nail's slashes (NailItem.SlashSize).</summary>
        public static float MeleeReach => (On(MarkOfPride) ? PrideReach : 1f) * (On(Longnail) ? LongnailReach : 1f);

        /// <summary>Equipment.Damage: Unbreakable Strength's nail, a fifth more over the tenth every melee weapon has.</summary>
        public static float NailBonus(ItemDef item) => item != null && item.Type == ItemType.KnightNail && Whole(UnbreakableStrength) ? StrengthNail - StrengthMelee : 0f;

        static void AddNailBonuses(ref Bonuses b)
        {
            if (Whole(UnbreakableStrength)) b.MeleeDamage += StrengthMelee;
        }

        static readonly RaycastHit2D[] sightHits = new RaycastHit2D[8];

        /// <summary>A wall (not a platform one jumps up through) between two points, and where it begins.</summary>
        static bool WallBetween(Vector2 a, Vector2 b, out Vector2 at)
        {
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << World.ChunkColliders.TerrainLayer };
            int n = Physics2D.Linecast(a, b, filter, sightHits);
            for (int i = 0; i < n; i++)
            {
                var c = sightHits[i].collider;
                if (c != null && !c.isTrigger && !c.usedByEffector) { at = sightHits[i].point; return true; }
            }
            at = b;
            return false;
        }

        /// <summary>
        /// Whether the player's melee blow (a swing, a sword's slash, the nail and its arts, the Dream Nail) reaches this
        /// enemy: in sight of the player as Terraria's melee must be (Player.ItemCheck_MeleeHitNPCs: noTileCollide ||
        /// CanHit; here its middle, its nearest edge, its top or its bottom), or one in the ground (a burrower, Terraria's
        /// noTileCollide). Longnail reaches through, a silver glint on the wall. Projectiles keep their own rules.
        /// </summary>
        public static bool MeleeReaches(Collider2D col)
        {
            if (col == null) return true;
            var hero = HeroController.SilentInstance;
            if (hero == null || col.GetComponentInParent<HealthManager>() == null) return true;
            Vector2 from = Centre(hero);
            var b = col.bounds;
            if (World.TerraWorld.IsSolidAt(b.center)) return true;
            if (!WallBetween(from, b.center, out var wall)) return true;
            Vector2 near = Vector2.MoveTowards(b.ClosestPoint(from), b.center, 0.12f);
            if (!WallBetween(from, near, out _)) return true;
            if (!WallBetween(from, new Vector2(b.center.x, b.max.y - 0.1f), out _) || !WallBetween(from, new Vector2(b.center.x, b.min.y + 0.1f), out _)) return true;
            if (!On(Longnail)) { DevWallStops++; return false; }
            DevPierces++;
            Glint(wall);
            return true;
        }

        /// <summary>Longnail's glints on the walls gone through: where, when, which room.</summary>
        static readonly List<(Vector2 At, float Time, string Scene)> glints = new List<(Vector2, float, string)>();

        static void Glint(Vector2 at)
        {
            string scene = SceneNow;
            foreach (var g in glints) if (g.Scene == scene && (g.At - at).sqrMagnitude < 0.25f && Time.time - g.Time < 0.2f) return;
            glints.Add((at, Time.time, scene));
        }

        static string SceneNow => GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;

        // ------------------------------------------------------------------ the swing

        /// <summary>The faint crescents of Mark of Pride: where (world), which way (radians, world), how far, when.</summary>
        static readonly List<(Vector2 At, float Angle, float Reach, float Time, string Scene)> crescents = new List<(Vector2, float, float, float, string)>();

        /// <summary>A Terraria melee swing begins (TerraMod.StartSwing): Mark of Pride's crescent at its reach.</summary>
        public static void OnMeleeSwing(HeroController hero, ItemDef def, float direction, float bladeLength)
        {
            if (!On(MarkOfPride) || hero == null || def == null || def.Class != DamageClass.Melee || bladeLength <= 0f) return;
            // A weapon's swing, not a tool's.
            if (!def.IsWeapon || def.PickPower > 0 || def.AxePower > 0 || def.HammerPower > 0) return;
            // The sword's arc goes from overhead to a little below the front: the strokes from 75° up to 25° down, its own length off.
            crescents.Add((Centre(hero), direction >= 0f ? 25f * Mathf.Deg2Rad : 155f * Mathf.Deg2Rad, bladeLength * 0.8f, Time.time, SceneNow));
        }

        /// <summary>The nail swung (NailItem.Tick, after the game's attack): the beam, the crescent.</summary>
        public static void OnNailSwing(HeroController hero, ItemDef def)
        {
            if (!Charms.Enabled || hero == null || def == null) return;
            Vector2 dir = hero.cState.upAttacking ? Vector2.up : hero.cState.downAttacking ? Vector2.down
                : hero.cState.wallSliding ? (hero.cState.facingRight ? Vector2.left : Vector2.right)
                : hero.cState.facingRight ? Vector2.right : Vector2.left;
            // The slash's own reach, and (as a negative angle span marker) a narrower arc: the nail's crescent is shallow.
            if (On(MarkOfPride)) crescents.Add((Centre(hero), Mathf.Atan2(dir.y, dir.x), -2.1f * MeleeReach, Time.time, SceneNow));
            if (On(GrubberflysElegy)) Beam(hero, def, dir);
            if (On(Dreamshield)) ShieldsSlash();
        }

        /// <summary>The wait before the next slash (NailItem.Tick): Nailmaster's Glory's, three tenths shorter.</summary>
        public static float NailCooldownScale => On(NailmastersGlory) ? GloryCooldown : 1f;

        /// <summary>
        /// Quick Slash: no recovery after a slash (NailItem.Tick). The Knight's swing (HeroController's attackDuration, which
        /// holds off turning, dashing, casting and the next swing) ends this long after the press, as its slash is out; the
        /// slash itself plays on, the wait before the next one (attack_cooldown) as ever.
        /// </summary>
        public static float NailRecovery(float plain) => On(QuickSlash) ? Mathf.Min(plain, QuickRecovery) : plain;
        const float QuickRecovery = 0.1f;

        // ------------------------------------------------------------------ the charge

        static readonly AccessTools.FieldRef<HeroController, float> chargeTimer = AccessTools.FieldRefAccess<HeroController, float>("nailChargeTimer");
        static readonly AccessTools.FieldRef<HeroController, float> chargeNeed = AccessTools.FieldRefAccess<HeroController, float>("nailChargeTime");

        /// <summary>The charge the nail art needs (NailArts.Tick, each frame with the charm bag on): Nailmaster's Glory's 30% quicker.</summary>
        public static float ChargeTime(float plain) => On(NailmastersGlory) ? plain / GloryCharge : plain;

        static float heavyHeld, heavyArt, heavyLevelAt = -10f, heavyShockAt = -10f, notChargingFor;
        static int heavyLevel;
        static bool wasStriking;
        static float gloryCharge, gloryReadyAt = -10f;
        static bool gloryWasReady;
        static readonly List<(Transform T, Vector3 Scale)> scaledArts = new List<(Transform, Vector3)>();

        /// <summary>A nail art's hit (NailItem.NailHit): Heavy Blow's overcharge on it.</summary>
        public static float ArtDamage(string art)
        {
            float k = art != null && heavyArt > 0f && On(HeavyBlow) ? 1f + HeavyDamage * heavyArt : 1f;
            if (art != null) DevLastArtFactor = k;
            return k;
        }

        /// <summary>Dev: the multiple the last nail art's hit had.</summary>
        public static float DevLastArtFactor = 1f;
        public static float ArtKnockback(string art) => art != null && heavyArt > 0f && On(HeavyBlow) ? 1f + HeavyKnock * heavyArt : 1f;
        /// <summary>The Cyclone Slash's sweep (NailArts.Tick) with Heavy Blow's size on it.</summary>
        public static float ArtReach => heavyArt > 0f && On(HeavyBlow) ? 1f + HeavyReach * heavyArt : 1f;

        static void TickNail(HeroController hero)
        {
            TickFollows();
            TickBeams();
            FadeEchoes();
            string scene = SceneNow;
            for (int i = glints.Count - 1; i >= 0; i--) if (Time.time - glints[i].Time > 0.35f || glints[i].Scene != scene) glints.RemoveAt(i);
            for (int i = crescents.Count - 1; i >= 0; i--) if (Time.time - crescents[i].Time > 0.25f || crescents[i].Scene != scene) crescents.RemoveAt(i);
            for (int i = cuts.Count - 1; i >= 0; i--) if (Time.time - cuts[i].Time > 0.3f || cuts[i].Scene != scene) cuts.RemoveAt(i);
            if (hero == null || !Charms.Enabled) return;
            bool charging = hero.cState.nailCharging && NailArts.Ready;
            float timer = charging ? chargeTimer(hero) : 0f, need = Mathf.Max(0.05f, chargeNeed(hero));
            // Nailmaster's Glory: how far the charge is, and the moment it is ready.
            gloryCharge = On(NailmastersGlory) && charging ? Mathf.Clamp01(timer / need) : 0f;
            bool ready = gloryCharge >= 1f;
            if (ready && !gloryWasReady) gloryReadyAt = Time.time;
            gloryWasReady = ready;
            // Heavy Blow: the charge held on past ready, in three steps.
            if (charging && On(HeavyBlow) && timer > need)
            {
                heavyHeld = Mathf.Clamp01((timer - need) / HeavyExtra);
                int level = Mathf.FloorToInt(heavyHeld * 3f + 0.001f);
                if (level > heavyLevel)
                {
                    heavyLevel = level;
                    heavyLevelAt = Time.time;
                    Mod.PlaySound("Item_" + (level >= 3 ? "4" : "35"), 0.35f, 0.8f + 0.15f * level);
                }
                notChargingFor = 0f;
            }
            else if (!charging)
            {
                notChargingFor += Time.deltaTime;
                if (notChargingFor > 0.25f && !NailArts.Striking) { heavyHeld = 0f; heavyLevel = 0; }
            }
            bool striking = NailArts.Striking;
            if (striking && !wasStriking)
            {
                heavyArt = On(HeavyBlow) ? heavyHeld : 0f;
                DevHeavyArt = heavyArt;
                if (heavyArt > 0.02f)
                {
                    ScaleArts(hero, 1f + HeavyReach * heavyArt);
                    heavyShockAt = Time.time;
                    Mod.PlaySound("Item_14", 0.4f, 1.2f - 0.3f * heavyArt);
                    Plugin.Log.LogInfo($"[护符] 沉重之击：骨钉技艺蓄力多了 {heavyArt:P0}，伤害 ×{ArtDamage("x"):F2}，大小 ×{ArtReach:F2}");
                }
                heavyHeld = 0f;
                heavyLevel = 0;
            }
            else if (!striking && wasStriking)
            {
                RestoreArts();
                heavyArt = 0f;
            }
            wasStriking = striking;
            TickArtCues(hero);
        }

        /// <summary>The arts' slash objects made bigger for the art (put back after).</summary>
        static void ScaleArts(HeroController hero, float k)
        {
            RestoreArts();
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n != "Great Slash" && n != "Dash Slash" && n != "Cyclone Slash") continue;
                scaledArts.Add((t, t.localScale));
                t.localScale = new Vector3(t.localScale.x * k, t.localScale.y * k, t.localScale.z);
            }
            DevLastArtScale = k;
        }

        static void RestoreArts()
        {
            foreach (var (t, s) in scaledArts) if (t != null) t.localScale = s;
            scaledArts.Clear();
        }

        // ------------------------------------------------------------------ Quick Slash's second cut

        struct Follow
        {
            public HealthManager Hm;
            public Collider2D Col;
            public int Damage;
            public float Facing, At, Direction;
            public Vector2 From;
            public string Scene;
        }
        static readonly List<Follow> follows = new List<Follow>();
        /// <summary>Quick Slash's crosses: where (world), the angle, when.</summary>
        static readonly List<(Vector2 At, float Angle, float Time, string Scene, bool Sharp)> cuts = new List<(Vector2, float, float, string, bool)>();

        /// <summary>A nail blow landed (NailItem.NailHit): Quick Slash's second cut to come, Unbreakable Strength's embers.</summary>
        public static void AfterNailHit(HealthManager hm, Collider2D col, int damage, float facing, Vector2 from, float direction = float.NaN)
        {
            if (hm == null || col == null || !Charms.Enabled) return;
            if (Whole(UnbreakableStrength)) Embers(col.bounds.center, facing);
            if (On(QuickSlash)) follows.Add(new Follow { Hm = hm, Col = col, Damage = damage, Facing = facing, From = from, Direction = direction, At = Time.time + QuickDelay, Scene = SceneNow });
        }

        static void TickFollows()
        {
            for (int i = follows.Count - 1; i >= 0; i--)
            {
                var f = follows[i];
                if (Time.time < f.At) continue;
                follows.RemoveAt(i);
                if (f.Hm == null || f.Hm.isDead || f.Col == null || f.Scene != SceneNow || Mod?.Melee == null) continue;
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Melee, TrueMelee = true };
                int dummyBefore = World.TargetDummy.DevDamage;
                // The second cut comes the way the first did (a shield judged as for it).
                MeleeHits.HitDirection = f.Direction;
                bool cut;
                try { cut = Mod.Melee.HitWith(f.Col, Mathf.Max(1, Mathf.RoundToInt(f.Damage * QuickShare)), 0f, 100f, f.Facing, f.From); }
                finally { MeleeHits.HitDirection = float.NaN; }
                if (!cut) continue;
                DevQuickHits++;
                DevQuickDealt += World.TargetDummy.DevDamage - dummyBefore;
                DevQuickBase += Mathf.Max(1, Mathf.RoundToInt(f.Damage * QuickShare));
                cuts.Add((f.Col.bounds.center, Random.Range(-0.5f, 0.5f), Time.time, f.Scene, false));
                Mod.PlaySound("Item_1", 0.25f, 1.5f);
            }
        }

        /// <summary>Unbreakable Strength's embers off a nail blow: a few short sparks thrown forward.</summary>
        static void Embers(Vector2 at, float facing)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < 8; k++)
            {
                float a = Random.Range(-0.8f, 0.8f) + (facing >= 0f ? 0f : Mathf.PI);
                int d = p.NewDust(px - new Vector2(3f, 3f), 6, 6, 6, Mathf.Cos(a) * 3.6f, Mathf.Sin(a) * 3.6f - 1f, 0, 1.25f);
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
            }
        }

        // ------------------------------------------------------------------ Sharp Shadow

        /// <summary>The shadow dash's speed (TerraMovement.HkShadowDash): the game's sharp one with Sharp Shadow.</summary>
        public static float ShadowDashSpeed(HeroController hero) => On(SharpShadow) && hero.DASH_SPEED_SHARP > 0f ? hero.DASH_SPEED_SHARP : hero.DASH_SPEED;

        /// <summary>What a shadow dash's strike deals (AbilityAccessories.DashStrikes): one and a half nail blows with Sharp Shadow.</summary>
        public static int ShadowDashDamage(int plain)
        {
            if (!On(SharpShadow)) return plain;
            var nail = ItemDb.Get(ItemType.KnightNail);
            nail.Damage = NailItem.DamageNow();
            return Mathf.Max(plain, Mathf.RoundToInt(Equipment.Damage(nail) * SharpShare));
        }

        /// <summary>A shadow dash struck an enemy: Sharp Shadow's dark cut on it.</summary>
        public static void ShadowCut(Collider2D col, float dir)
        {
            if (!On(SharpShadow) || col == null) return;
            DevSharpHits++;
            cuts.Add((col.bounds.center, dir >= 0f ? -0.25f : 0.25f, Time.time, SceneNow, true));
        }

        // ------------------------------------------------------------------ Grubberfly's beams

        sealed class Beam_
        {
            public Vector2 At, Dir;
            public float Born;
            public int Damage;
            public float Crit, Knockback, Scale = 1f;
            public string Scene;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        static readonly List<Beam_> beams = new List<Beam_>();
        static readonly List<HealthManager> beamFound = new List<HealthManager>();

        /// <summary>A beam thrown its way: half the slash's blow (an art's: what it is given), its size.</summary>
        static void Beam(HeroController hero, ItemDef def, Vector2 dir, int damage = -1, float scale = 1f)
        {
            if (damage < 0) damage = Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def) * BeamShare));
            beams.Add(new Beam_
            {
                At = Centre(hero) + dir * 0.6f, Dir = dir, Born = Time.time, Damage = damage, Crit = Equipment.Crit(def),
                Knockback = Equipment.Knockback(def) * 0.5f, Scale = scale, Scene = SceneNow,
            });
            DevBeams++;
            DevLastBeamDamage = damage;
            DevLastBeamScale = scale;
        }

        public static int DevLastBeamDamage;
        public static float DevLastBeamScale;

        static void TickBeams()
        {
            string scene = SceneNow;
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var b = beams[i];
                float age = Time.time - b.Born;
                if (b.Scene != scene || age > BeamLife) { beams.RemoveAt(i); continue; }
                float step = BeamSpeed * Time.deltaTime;
                if (WallBetween(b.At, b.At + b.Dir * step, out var wall))
                {
                    b.At = wall;
                    BeamStrike(b);
                    Glow2(b.At);
                    beams.RemoveAt(i);
                    continue;
                }
                b.At += b.Dir * step;
                BeamStrike(b);
            }
        }

        static void BeamStrike(Beam_ b)
        {
            var melee = Mod?.Melee;
            if (melee == null) return;
            foreach (var hm in AccessoryEffects.Enemies(b.At, BeamRadius * b.Scale / TerraParticles.Px, beamFound))
            {
                if (!b.Hit.Add(hm.GetInstanceID())) continue;
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                if (col == null) continue;
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Melee, FromProjectile = true };
                if (melee.HitWith(col, b.Damage, b.Knockback, b.Crit, b.Dir.x >= 0f ? 1f : -1f, b.At)) DevBeamHits++;
            }
        }

        /// <summary>A covering colour at an opacity (FxBatch's premultiplied: rgb times the opacity).</summary>
        static Color Silver(Color c, float a) => new Color(c.r * a, c.g * a, c.b * a, a);

        /// <summary>A beam spent on a wall: a small white flash there.</summary>
        static void Glow2(Vector2 at)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f;
                int d = p.NewDust(px - new Vector2(2f, 2f), 4, 4, 43, Mathf.Cos(a) * 2f, Mathf.Sin(a) * 2f, 100, 0.8f);
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
            }
        }

        // ------------------------------------------------------------------ the looks

        /// <summary>Round the player: Nailmaster's Glory's gold motes, Heavy Blow's ember glow and its steps.</summary>
        static void DrawNail(FxBatch fx, Vector2 c, float opacity)
        {
            float t = Time.time;
            if (On(NailmastersGlory))
            {
                if (gloryCharge > 0f && gloryCharge < 1f)
                {
                    fx.OnTop = true;
                    // Six gold motes drawn in as it charges.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f + t * 2f;
                        float r = Mathf.Lerp(42f, 6f, gloryCharge);
                        var at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        fx.Draw(Dot, at, Glow(GloryColor, (0.35f + 0.5f * gloryCharge) * opacity), 0f, 0.22f, overPlayer: true);
                    }
                }
                float ready = Mathf.Clamp01(1f - (t - gloryReadyAt) / 0.35f);
                if (ready > 0f)
                {
                    fx.OnTop = true;
                    fx.Draw(Ring, c, Glow(GloryColor, 0.8f * ready * opacity), 0f, Mathf.Lerp(20f, 56f, 1f - ready) / 64f, overPlayer: true);
                }
            }
            if (On(HeavyBlow))
            {
                if (heavyHeld > 0f)
                {
                    // A deepening ember glow behind the player, a beat at each step.
                    fx.OnTop = true;
                    float beat = Mathf.Clamp01(1f - (t - heavyLevelAt) / 0.3f);
                    float k = (0.25f + 0.45f * heavyHeld + 0.1f * Mathf.Sin(t * 10f)) * opacity;
                    fx.Draw(Dot, c, Glow(HeavyColor, k), 0f, 1.6f + 0.8f * heavyHeld, overPlayer: false);
                    if (beat > 0f)
                    {
                        fx.OnTop = true;
                        fx.Draw(Ring, c, Glow(HeavyColor, 0.7f * beat * opacity), 0f, Mathf.Lerp(52f, 28f, beat) / 64f, overPlayer: true);
                    }
                }
                float shock = Mathf.Clamp01(1f - (t - heavyShockAt) / 0.4f);
                if (shock > 0f)
                {
                    fx.OnTop = true;
                    float r = Mathf.Lerp(16f, 60f + 70f * DevHeavyArt, 1f - shock * shock);
                    fx.Draw(Ring, c, Glow(HeavyColor, 0.85f * shock * opacity), 0f, r * 2f / 64f, overPlayer: true);
                }
            }
        }

        /// <summary>In the world: the beams, the crescents, the crosses and the glints.</summary>
        static void DrawNailWorld(FxBatch fx)
        {
            if (beams.Count == 0 && crescents.Count == 0 && cuts.Count == 0 && glints.Count == 0) return;
            bool top = fx.OnTop;
            fx.OnTop = true;
            float t = Time.time;
            foreach (var b in beams)
            {
                float age = t - b.Born;
                float fade = Mathf.Clamp01((BeamLife - age) / 0.12f) * Mathf.Clamp01(age / 0.03f + 0.4f);
                var px = TerraParticles.ToPx(b.At);
                var d = new Vector2(b.Dir.x, -b.Dir.y);
                float rot = Mathf.Atan2(d.y, d.x);
                float sc = 0.42f * b.Scale;
                // The crescent's bright edge sits 79 px in front of its middle: the middle drawn behind the beam's place.
                var body = FxBatch.Frame("Projectile_985", 0, 0, 170, 170, 85f, 85f);
                var edge = FxBatch.Frame("Projectile_985", 0, 510, 170, 170, 85f, 85f);
                for (int k = 2; k >= 0; k--)
                {
                    var at = px - d * (79f * sc + k * 10f * b.Scale);
                    float a = fade * (k == 0 ? 1f : 0.35f / k);
                    // Silver: a deep grey-blue body, the bright silver edge over it, a thin white shine along the edge.
                    fx.Draw(body, at, Silver(SilverDeep, 0.4f * a), rot, sc);
                    fx.Draw(edge, at, Silver(SilverLight, 0.85f * a), rot, sc, overPlayer: true);
                    fx.Draw(edge, at, Glow(Color.white, 0.22f * a), rot, sc, overPlayer: true);
                }
                fx.Light(px, new Color(0.72f, 0.78f, 0.9f) * fade, 3f * b.Scale);
            }
            foreach (var cr in crescents)
            {
                // Eleven short strokes along an arc of 100° at the reach, faint at its ends, gone in a quarter second.
                float k = Mathf.Clamp01((t - cr.Time) / 0.25f);
                var px = TerraParticles.ToPx(cr.At);
                // A nail slash's arc (its reach kept negative) is narrower than a sword's sweep.
                float span = cr.Reach < 0f ? 0.6f : 0.87f;
                float r = Mathf.Abs(cr.Reach) / TerraParticles.Px * (0.94f + 0.06f * k);
                float a0 = -cr.Angle;
                float alpha = 0.6f * (1f - k) * Mathf.Clamp01(k * 6f + 0.2f);
                for (int s = -5; s <= 5; s++)
                {
                    float u = s / 5f;
                    float a = a0 + u * span;
                    var at = px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    float edgeFade = 1f - u * u;
                    fx.Draw(Dot, at, Glow(PrideColor, alpha * edgeFade), a + Mathf.PI / 2f, new Vector2(r * 0.17f / 32f * 2f, 0.12f));
                }
            }
            foreach (var cut in cuts)
            {
                float k = Mathf.Clamp01((t - cut.Time) / 0.3f);
                var px = TerraParticles.ToPx(cut.At);
                float grow = Mathf.Clamp01(k * 6f);
                if (cut.Sharp)
                {
                    // Sharp Shadow: one dark violet cut along the dash, a faint glow round it.
                    fx.Draw(Dot, px, Glow(SharpColor, 0.5f * (1f - k)), 0f, 1.1f);
                    fx.Draw(Dot, px, Glow(SharpColor, 1f * (1f - k)), cut.Angle, new Vector2(2.6f * grow, 0.14f));
                    fx.Draw(Dot, px, Glow(Color.white, 0.5f * (1f - k)), cut.Angle, new Vector2(1.8f * grow, 0.05f));
                }
                else
                {
                    // Quick Slash: a thin silver cross, a white shine down its middle.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        fx.Draw(Dot, px, Silver(SilverLight, 0.9f * (1f - k)), cut.Angle + s * Mathf.PI / 4f, new Vector2(2f * grow, 0.12f));
                        fx.Draw(Dot, px, Glow(Color.white, 0.35f * (1f - k)), cut.Angle + s * Mathf.PI / 4f, new Vector2(1.6f * grow, 0.05f), overPlayer: true);
                    }
                }
            }
            foreach (var g in glints)
            {
                float k = Mathf.Clamp01((t - g.Time) / 0.35f);
                var px = TerraParticles.ToPx(g.At);
                fx.Draw(Ring, px, Glow(LongnailColor, 0.6f * (1f - k)), 0f, Mathf.Lerp(8f, 26f, k) / 64f);
                fx.Draw(Dot, px, Glow(LongnailColor, 0.8f * (1f - k)), 0f, new Vector2(1.2f * (1f - k * 0.5f), 0.08f));
                fx.Draw(Dot, px, Glow(LongnailColor, 0.8f * (1f - k)), Mathf.PI / 2f, new Vector2(1.2f * (1f - k * 0.5f), 0.08f));
            }
            fx.OnTop = top;
        }

        /// <summary>
        /// The game's own Quick Slash cooldown and Grubberfly beams (HeroController.DoAttack reads equippedCharm_32 and _35)
        /// left out while the charm bag is on: these charms are the bag's own.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "DoAttack")]
        static class NoVanillaSlashCharms
        {
            static void Prefix(out (bool, bool) __state)
            {
                var pd = PlayerData.instance;
                __state = pd != null ? (pd.equippedCharm_32, pd.equippedCharm_35) : (false, false);
                if (!Charms.Enabled || pd == null) return;
                pd.equippedCharm_32 = false;
                pd.equippedCharm_35 = false;
            }

            static void Postfix((bool, bool) __state)
            {
                var pd = PlayerData.instance;
                if (!Charms.Enabled || pd == null) return;
                pd.equippedCharm_32 = __state.Item1;
                pd.equippedCharm_35 = __state.Item2;
            }
        }
    }
}
