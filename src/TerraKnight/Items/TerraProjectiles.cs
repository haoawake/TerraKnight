using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's projectiles: thrown explosives, arrows, bullets, magic bolts and the boomerang.
    /// They run on Terraria's 60 Hz tick in Terraria units (pixels per update; extraUpdates runs
    /// the whole update again within the tick, lifetimes count updates), collide with the grid's
    /// blocks, hit enemies through <see cref="MeleeHits.HitWith"/> (a piercing shot may hit the
    /// same enemy again once its immunity runs out), and are drawn as Main.DrawProj draws them,
    /// through <see cref="FxBatch"/>: their sprites pivoting near the head, lit, full-bright or
    /// added as Terraria's GetAlpha says, with their light, dust and kill bursts. Explosives blow
    /// up the terrain, hurt whatever is in the blast (the player too) and break the floors the
    /// Knight would have to dive through.
    /// </summary>
    internal sealed partial class TerraProjectiles : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        /// <summary>The environment's light (Hallownest has no light map: the dust's constant).</summary>
        const float L = 0.874f;

        public static TerraProjectiles Instance { get; private set; }

        enum Ai { Thrown, Arrow, Spark, GemBolt, WaterBolt, Fireball, Scythe, Boomerang, Star, Shard, LuminiteTrail, Starfury, Beam, Bee, Spear, Thrown2, Missile, Vine, Stream, CloakStar, Custom }
        /// <summary>
        /// Projectile.GetAlpha: Lit by the light; Bright full white; Additive white added; Invisible
        /// not drawn; NearlyAdded (200,200,200,25); AdditiveFade (255-α) grey added, hidden while
        /// α ≥ 200; WhiteA100 (255,255,255,100), hidden while α ≥ 140; HolyAdd (200,200,200,0);
        /// ShardAdd (205,205,205,0).
        /// </summary>
        enum Look { Lit, Bright, Additive, Invisible, NearlyAdded, AdditiveFade, WhiteA100, HolyAdd, ShardAdd }

        sealed class Spec
        {
            public Ai Ai;
            public string Texture;
            /// <summary>Its glowmask (Glow_N), added over it where its drawing uses one.</summary>
            public string Glow;
            public int W = 10, H = 10, Time = 600, Penetrate = 1, Extra, Dust = -1;
            public float Scale = 1f;
            /// <summary>How far behind the hitbox centre the sprite's centre is drawn, along the flight (Main.DrawProj's pivot).</summary>
            public float PivotBack;
            public Look Look = Look.Lit;
            public Color Light;
            /// <summary>usesLocalNPCImmunity's cooldown in ticks; 0 = the NPC's own 10 tick immunity.</summary>
            public int LocalCooldown;
            /// <summary>AI_001 types that never fall (Jester's arrow, bullets, the laser).</summary>
            public bool NoGravity;
            public bool TileCollide = true;
            /// <summary>AI_001: gravity of Gravity px per update once GravityDelay updates have passed (negative: falls up).</summary>
            public int GravityDelay = 15;
            public float Gravity = 0.1f;
            /// <summary>The Luminite arrow: gravity from this many updates of age on, with no counter (-1 = the usual rule).</summary>
            public int GravityAfterAge = -1;
            /// <summary>Alpha it starts at (0 visible .. 1), and how much it loses each update (-1: speed × 0.9, -2: speed × 0.3, in 255ths).</summary>
            public float AlphaStart, AlphaFade = 15f / 255f;
            /// <summary>Damage kept after each enemy hit (Unholy 0.95, Jester's 0.9, High Velocity 0.85, Luminite 0.96).</summary>
            public float Falloff = 1f;
            /// <summary>usesLocalNPCImmunity with cooldown -1: each projectile hits each enemy once, ever.</summary>
            public bool OncePerNpc;
            /// <summary>usesIDStaticNPCImmunity: an enemy hit by one is immune to all of this kind for this long (ticks).</summary>
            public int TypeCooldown;
            /// <summary>UseGroupStaticImmunity: the kind whose immunity it shares (the giant bee the small bee's); None = its own.</summary>
            public ProjKind TypeGroup;
            /// <summary>Dies on its first enemy (Hellfire's timeLeft = 1 on a hit).</summary>
            public bool DieOnHit;
            /// <summary>Debuffs it gives on a hit: (buff, ticks, one chance in N).</summary>
            public (int Buff, int Ticks, int Chance)[] Debuffs;
            /// <summary>Old positions drawn behind it (TrailCacheLength; 0 = none).</summary>
            public int TrailLength;
            /// <summary>Boomerangs (AI_003): the speed they come back at and how hard they turn to it; the Thorn Chakram bounces off walls instead of turning back.</summary>
            public float ReturnSpeed = 9f, ReturnAccel = 0.4f;
            /// <summary>Boomerangs: how many can be out at once (ItemCheck_CheckCanUse; 0 = one), the updates before turning back, the spin per update.</summary>
            public int MaxOut, OutTicks = 30;
            public float SpinRate = 0.4f;
            public bool WallBounce;
            /// <summary>Boomerangs that don't turn back off what they hit (P:13101: the Combat Wrench, the Sergeant United Shield).</summary>
            public bool NoHitTurn;
            /// <summary>Boomerangs that turn home as a whole vector, by at most ReturnAccel (P:40218-40227: the Anchor).</summary>
            public bool ReturnVector;
            /// <summary>Main.DrawProj's generic origin: the pivot in texture px from its top-left (negative: the centre), and where it sits off the hitbox centre (px, y down).</summary>
            public Vector2 Origin = new Vector2(-1f, -1f), DrawOffset;
            /// <summary>GetCollisionParams: the box it meets blocks with, when not its own size.</summary>
            public Vector2 TileBox;
            /// <summary>npc.immune[owner] it leaves even when it hits only once (the Magic Missile's 8).</summary>
            public int OwnerImmune;
            /// <summary>ownerHitCheck: it can't hit what the player has no line to (spears).</summary>
            public bool OwnerHitCheck;
            /// <summary>Frames stacked down its texture.</summary>
            public int Frames = 1;
            /// <summary>Its own armour penetration, on top of the player's (the Star Cloak's star: 25).</summary>
            public int ArmorPen;
            // Explosives.
            public int Fuse, BlastPx, BlastDamage, TileRadius;
            public float BlastKnockback;
            // Ai.Custom: a projectile with its own code (the partial TerraProjectiles*.cs files).
            /// <summary>Its AI each update, before the move and the hits (with another Ai: its own part, after the shared one).</summary>
            public System.Action<TerraProjectiles, Proj> Update;
            /// <summary>It moves itself (no generic move or block collision).</summary>
            public bool OwnMove;
            /// <summary>Drawn its own way instead of the generic sprite (the fx batch, the projectile, its centre in Terraria px).</summary>
            public System.Action<TerraProjectiles, FxBatch, Proj, Vector2> Draw;
            /// <summary>After each enemy it hits (on-hit specials, splits).</summary>
            public System.Action<TerraProjectiles, Proj, HealthManager> OnHit;
            /// <summary>Its death (dust, sound, children) instead of the generic one.</summary>
            public System.Action<TerraProjectiles, Proj> OnKill;
            /// <summary>Meeting a block (which axes, the velocity before); without one it dies.</summary>
            public System.Action<TerraProjectiles, Proj, bool, bool, Vector2> TileHit;
            /// <summary>Whether it can hurt right now (some only in certain states).</summary>
            public System.Func<Proj, bool> CanDamage;
            /// <summary>Stops after the first enemy it hits in an update (keepIterating = false).</summary>
            public bool OneHitPerUpdate;
            /// <summary>Leaves no immunity behind on what it hits (Projectile.Damage returns before setting npc.immune: the Influx Waver).</summary>
            public bool NoImmunity;
            /// <summary>Knocks what it hits away from its centre, not along its flight (the Inferno Fork's blaze).</summary>
            public bool PushFromCentre;
            /// <summary>A set's own shot: no Player.OnHit for its hits (P:15211-15224: 221, 227, 908); NoGhost: no Spectre or Nebula either (aiStyle 59).</summary>
            public bool NoSetOnHit, NoGhost;
            /// <summary>Whether this one keeps the item in use (Holds): a yoyo's Magic String copies don't.</summary>
            public System.Func<Proj, bool> HoldsWhile;
            /// <summary>Before its hit (after the crit chance is known): the damage and crit chance it lands with.</summary>
            public System.Func<TerraProjectiles, Proj, HealthManager, int, float, (int, float)> ModifyHit;
            /// <summary>Knocks what it hits away from the player (yoyos, flails), not along its flight.</summary>
            public bool PushFromPlayer;
            /// <summary>Its knockback by state (a spinning flail's ×0.35, a dragged one's ×0.5).</summary>
            public System.Func<Proj, float> KnockbackScale;
            /// <summary>Its own hit area (world centre and size) instead of its box, and the test each collider there must pass.</summary>
            public System.Func<Proj, (Vector2, Vector2)> HitArea;
            public System.Func<Proj, Collider2D, bool> HitTest;
            /// <summary>Lands on platforms rather than falling through them (a yoyo's loose copy, fallThrough false).</summary>
            public System.Func<Proj, bool> LandsOnPlatforms;
            // Spears (AI_019_Spears): the offset's three terms (start, out per tick, back per tick;
            // zero = the Trident's 4, 0.9, 1.2), and the reach past the tip while thrusting out
            // (AI_019_Spears_GetExtensionHitbox: length and size added at full stretch, the item whose
            // melee speed shortens it, the streaks' colour; 0 = none).
            public Vector3 SpearOffset;
            public float SpearReach, SpearReachSize;
            public ItemType SpearItem;
            public Color SpearGlint;
            /// <summary>Held out by the player while the button is held (AI_075, AI_100: the Last Prism, the Charged Blaster Cannon, the Medusa Head): the item makes no other while one is out.</summary>
            public bool Held;
            /// <summary>A beam (AI_084): it hits along a line instead of with its box (its two ends in Terraria px, y down, and its width; Colliding's CheckAABBvLineCollision).</summary>
            public System.Func<Proj, (Vector2 From, Vector2 To, float Width)> Beam;
        }

        static TerraProjectiles() => RegisterMore();

        /// <summary>The kinds the partial files add (TerraProjectilesMore.cs).</summary>
        static partial void RegisterMore();

        static readonly Dictionary<ProjKind, Spec> specs = new Dictionary<ProjKind, Spec>
        {
            [ProjKind.Bomb] = new Spec { Ai = Ai.Thrown, Texture = "Projectile_28", W = 22, H = 22, Fuse = 180, BlastPx = 128, BlastDamage = 100, BlastKnockback = 8f, TileRadius = 4, Penetrate = -1, PivotBack = -4f },
            [ProjKind.Dynamite] = new Spec { Ai = Ai.Thrown, Texture = "Projectile_29", W = 10, H = 10, Fuse = 300, BlastPx = 250, BlastDamage = 250, BlastKnockback = 10f, TileRadius = 7, Penetrate = -1 },
            [ProjKind.Grenade] = new Spec { Ai = Ai.Thrown, Texture = "Projectile_30", W = 14, H = 14, Fuse = 180, BlastPx = 128, BlastDamage = 0, BlastKnockback = 8f, TileRadius = 0, Penetrate = -1, PivotBack = 3f },
            // The Sticky, Bouncy and Happy Grenades (P:5320, 5940, 6330; fuses P:11031, 10999, 11051): the grenade's 128 px blast, 3 s.
            [ProjKind.StickyGrenadeProj] = new Spec { Ai = Ai.Thrown, Texture = "Projectile_397", W = 14, H = 14, Fuse = 180, BlastPx = 128, BlastKnockback = 8f, Penetrate = -1, PivotBack = 3f },
            [ProjKind.BouncyGrenadeProj] = new Spec { Ai = Ai.Thrown, Texture = "Projectile_517", W = 14, H = 14, Fuse = 180, BlastPx = 128, BlastKnockback = 8f, Penetrate = -1, PivotBack = 3f },
            [ProjKind.PartyGrenadeProj] = new Spec { Ai = Ai.Thrown, Texture = "Projectile_588", W = 14, H = 14, Fuse = 180, BlastPx = 128, BlastKnockback = 8f, Penetrate = -1, PivotBack = 3f },
            [ProjKind.WoodenArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_1", Time = 1200, PivotBack = 11f, Dust = 7 },
            [ProjKind.FlamingArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_2", Time = 1200, PivotBack = 11f, Dust = 6, Light = new Color(1f, 0.75f, 0.55f) },
            [ProjKind.UnholyArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_4", Time = 1200, PivotBack = 11f, Penetrate = 5, Dust = 14, Falloff = 0.95f, Light = new Color(0.175f, 0.035f, 0.35f) },
            [ProjKind.JesterArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_5", Time = 120, Extra = 1, PivotBack = 11f, Penetrate = -1, Falloff = 0.9f, NoGravity = true, Look = Look.Additive, Light = new Color(0.4f, 0.4f, 0.4f) },
            [ProjKind.Bullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_14", W = 4, H = 4, Time = 600, Extra = 1, Scale = 1.2f, PivotBack = 9.6f, NoGravity = true, Look = Look.Bright, Light = new Color(0.5f, 0.35f, 0.05f), AlphaStart = 1f },
            [ProjKind.MeteorShot] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_36", W = 4, H = 4, Time = 600, Extra = 1, Scale = 1.4f, PivotBack = 16.8f, NoGravity = true, Penetrate = 2, LocalCooldown = 7, Light = new Color(0.8f, 0.12f, 0.36f), AlphaStart = 1f },
            // Every other arrow (spec-ammo B.2; Light is ProjLight's colour).
            [ProjKind.HellfireArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_41", Time = 3600, PivotBack = 11f, Penetrate = -1, DieOnHit = true, Light = new Color(0.3f, 0.24f, 0.18f) },
            [ProjKind.HolyArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_91", Time = 1200, PivotBack = 11f, GravityDelay = 20, Gravity = 0.07f, Look = Look.HolyAdd },
            [ProjKind.HallowStar] = new Spec { Ai = Ai.Star, Texture = "Projectile_92", W = 24, H = 24, Scale = 0.8f, Time = 3600, PivotBack = 7f, TileCollide = false, Look = Look.Additive, Light = new Color(0.72f, 0.54f, 0.9f) },
            [ProjKind.CursedArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_103", Time = 1200, PivotBack = 11f, Light = new Color(0.35f, 1f, 0f), Debuffs = new[] { (EnemyDebuffs.CursedInferno, 420, 1) } },
            [ProjKind.FrostburnArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_172", Time = 1200, PivotBack = 11f, GravityDelay = 17, Gravity = 0.085f, Light = new Color(0.35f, 0.55f, 1f), Debuffs = new[] { (EnemyDebuffs.Frostburn, 180, 3) } },
            [ProjKind.ChlorophyteArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_225", Time = 1200, Extra = 1, PivotBack = 12f },
            [ProjKind.IchorArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_278", Time = 1200, Extra = 1, PivotBack = 14f, Light = new Color(1f, 1f, 0f), Debuffs = new[] { (EnemyDebuffs.Ichor, 600, 1) } },
            [ProjKind.VenomArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_282", Time = 1200, Extra = 1, PivotBack = 14f, Debuffs = new[] { (EnemyDebuffs.Venom, 600, 1), (EnemyDebuffs.PotentAcid, 120, 1) } },
            [ProjKind.BoneArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_474", Time = 1200, PivotBack = 13f, Penetrate = 2 },
            [ProjKind.LuminiteArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_639", Time = 90, Extra = 1, PivotBack = 12f, Penetrate = 4, OncePerNpc = true, Falloff = 0.96f, AlphaStart = 1f, AlphaFade = 25f / 255f, GravityAfterAge = 14, NoGravity = true, TrailLength = 10 },
            [ProjKind.LuminiteTrail] = new Spec { Ai = Ai.LuminiteTrail, Texture = "Projectile_640", Time = 90, Extra = 2, PivotBack = 18f, Penetrate = 4, OncePerNpc = true, Falloff = 0.96f, AlphaStart = 1f, AlphaFade = 25f / 255f, Look = Look.Invisible, TrailLength = 20 },
            [ProjKind.ShimmerArrow] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_1006", Time = 1200, PivotBack = 11f, Gravity = -0.1f, Look = Look.Bright },
            // Every other bullet.
            [ProjKind.SilverBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_981", W = 4, H = 4, Time = 600, Extra = 1, Scale = 1.2f, PivotBack = 9.6f, NoGravity = true, Look = Look.Bright, Light = new Color(0.5f, 0.35f, 0.35f) },
            [ProjKind.CrystalBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_89", W = 4, H = 4, Time = 600, Extra = 1, Scale = 1.2f, PivotBack = 14.4f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.025f, 0.1f, 0.5f) },
            [ProjKind.CrystalShard] = new Spec { Ai = Ai.Shard, Texture = "Projectile_90", W = 6, H = 6, Scale = 1.2f, Time = 600, TileCollide = false, Look = Look.ShardAdd, Light = new Color(0.05f, 0.2f, 1f) },
            [ProjKind.CursedBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_104", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.2f, PivotBack = 9.6f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.175f, 0.5f, 0f), Debuffs = new[] { (EnemyDebuffs.CursedInferno, 420, 1) } },
            [ProjKind.ChlorophyteBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_207", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.2f, PivotBack = 9.6f, NoGravity = true, AlphaStart = 1f, AlphaFade = 25f / 255f, Light = new Color(0.2f, 0.5f, 0.2f) },
            [ProjKind.HighVelocityBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_242", W = 4, H = 4, Time = 600, Extra = 7, Scale = 1.18f, PivotBack = 77.9f, NoGravity = true, Penetrate = 3, OncePerNpc = true, Falloff = 0.85f, AlphaStart = 1f, AlphaFade = -1f, Look = Look.WhiteA100, Light = new Color(0.5f, 0.35f, 0.05f) },
            [ProjKind.IchorBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_279", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.25f, PivotBack = 10f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.5f, 0.5f, 0f), Debuffs = new[] { (EnemyDebuffs.Ichor, 600, 1) } },
            [ProjKind.VenomBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_283", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.25f, PivotBack = 22.5f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.4f, 0.05f, 0.5f), Debuffs = new[] { (EnemyDebuffs.Venom, 600, 1), (EnemyDebuffs.PotentAcid, 120, 1) } },
            [ProjKind.PartyBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_284", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.3f, PivotBack = 23.4f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.5f, 0.05f, 0.4f) },
            [ProjKind.NanoBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_285", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.3f, PivotBack = 23.4f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.05f, 0.25f, 0.5f) },
            [ProjKind.ExplodingBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_286", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.3f, PivotBack = 23.4f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.5f, 0.25f, 0.05f), TypeCooldown = 60 },
            [ProjKind.GoldenBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_287", W = 4, H = 4, Time = 600, Extra = 2, Scale = 1.3f, PivotBack = 23.4f, NoGravity = true, AlphaStart = 1f, Look = Look.AdditiveFade, Light = new Color(0.45f, 0.5f, 0.2f), Debuffs = new[] { (EnemyDebuffs.Midas, 120, 1) } },
            [ProjKind.LuminiteBullet] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_638", W = 4, H = 4, Time = 600, Extra = 5, PivotBack = 58f, NoGravity = true, Penetrate = -1, OncePerNpc = true, Falloff = 0.96f, AlphaStart = 1f, AlphaFade = -2f, Look = Look.WhiteA100, TrailLength = 5 },
            [ProjKind.Laser] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_20", W = 4, H = 4, Time = 600, Extra = 2, Scale = 0.65f, PivotBack = 19f, NoGravity = true, Penetrate = 3, Look = Look.Additive, Light = new Color(0.075f, 0.75f, 0.225f) },
            [ProjKind.AmethystBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_121", Time = 300, Dust = 86, LocalCooldown = 60, Light = new Color(0.9f, 0f, 0.9f) },
            [ProjKind.RubyBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_125", Time = 300, Dust = 90, LocalCooldown = 60, Light = new Color(1f, 0.1f, 0.1f) },
            [ProjKind.DiamondBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_126", Time = 300, Dust = 91, LocalCooldown = 60, Penetrate = 2, Light = new Color(1.3f, 1.3f, 1.3f) },
            // P:2116-2134, 6463, 22261-22298 (FinalizeProjectile_GemStaves): the Topaz bursts; the Sapphire's twins; the Emerald goes twice as long and through
            // one more; the Amber through two more, off walls. Their light (P:38594-38640) by gem.
            [ProjKind.TopazBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_122", Time = 300, Dust = 87, LocalCooldown = 60, Light = new Color(0.9f, 0.9f, 0f) },
            [ProjKind.SapphireBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_123", Time = 300, Dust = 88, LocalCooldown = 60, Light = new Color(0f, 0.1f, 1.3f) },
            [ProjKind.EmeraldBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_124", Time = 600, Dust = 89, LocalCooldown = 60, Penetrate = 2, Light = new Color(0f, 1f, 0.1f) },
            [ProjKind.AmberBolt] = new Spec { Ai = Ai.GemBolt, Texture = "Projectile_597", Time = 300, Dust = 262, LocalCooldown = 60, Penetrate = 3, Light = new Color(0.9f, 0.6f, 0.2f) },
            [ProjKind.Spark] = new Spec { Ai = Ai.Spark, Look = Look.Invisible, Penetrate = 2, Time = 3600, Dust = 6 },
            [ProjKind.WaterBolt] = new Spec { Ai = Ai.WaterBolt, W = 16, H = 16, Time = 1800, Penetrate = 10, Look = Look.Invisible, Dust = 172 },
            [ProjKind.BallofFire] = new Spec { Ai = Ai.Fireball, Texture = "Projectile_15", W = 16, H = 16, Time = 3600, Penetrate = 1, Dust = 6, Look = Look.NearlyAdded, Light = new Color(1f, 0.32f, 0.08f) },
            [ProjKind.DemonScythe] = new Spec { Ai = Ai.Scythe, Texture = "Projectile_45", W = 48, H = 48, Time = 3600, Penetrate = 5, Scale = 0.9f, Dust = 27, Look = Look.Bright, Light = new Color(0.12f, 0.02f, 1f) },
            [ProjKind.TerraBeam] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_132", W = 32, H = 32, Time = 45, Penetrate = 3, NoGravity = true, Look = Look.Bright, Dust = 107, Light = new Color(0.4f, 1f, 0.4f) },
            [ProjKind.Boomerang] = new Spec { Ai = Ai.Boomerang, Texture = "Projectile_6", W = 22, H = 22, Time = 3600, Penetrate = -1, PivotBack = 0f, Light = new Color(0.4f, 0.4f, 0.4f) },
            // More weapons (spec-weapons2).
            [ProjKind.StarfuryStar] = new Spec { Ai = Ai.Starfury, Texture = "Projectile_9", W = 24, H = 24, Scale = 0.8f, Time = 3600, Penetrate = 2, TileCollide = false },
            [ProjKind.EnchantedBeam] = new Spec { Ai = Ai.Beam, Texture = "Projectile_173", W = 16, H = 16, Time = 3600, AlphaStart = 1f, TileBox = new Vector2(4f, 4f), Light = new Color(0.06f, 0.4f, 0.2f) },
            [ProjKind.IceBolt] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_118", W = 10, H = 10, Time = 3600, NoGravity = true, Look = Look.Invisible },
            [ProjKind.Bee] = new Spec { Ai = Ai.Bee, Texture = "Projectile_181", W = 8, H = 8, Time = 600, Penetrate = 3, Extra = 3, AlphaStart = 1f, TypeCooldown = 10, Frames = 4, Origin = new Vector2(5f, 4f), DrawOffset = new Vector2(1f, 0f) },
            [ProjKind.DarkLance] = new Spec { Ai = Ai.Spear, Texture = "Projectile_46", W = 20, H = 20, Scale = 1.1f, Time = 3600, Penetrate = -1, TileCollide = false, OwnerHitCheck = true,
                SpearOffset = new Vector3(3f, 1.4f, 1.6f), SpearReach = 50f, SpearReachSize = 15f, SpearItem = ItemType.DarkLance, SpearGlint = FxBatch.C(180, 80, 255, 0) },
            [ProjKind.TridentSpear] = new Spec { Ai = Ai.Spear, Texture = "Projectile_47", W = 18, H = 18, Scale = 1.1f, Time = 3600, Penetrate = -1, TileCollide = false, OwnerHitCheck = true },
            [ProjKind.WoodenBoomerang] = new Spec { Ai = Ai.Boomerang, Texture = "Projectile_52", W = 22, H = 22, Time = 3600, Penetrate = -1, TileBox = new Vector2(10f, 10f), Origin = new Vector2(9f, 11f), DrawOffset = new Vector2(-2f, 0f) },
            [ProjKind.Flamarang] = new Spec { Ai = Ai.Boomerang, Texture = "Projectile_19", W = 22, H = 22, Time = 3600, Penetrate = -1, TileBox = new Vector2(10f, 10f), Origin = new Vector2(9f, 11f), DrawOffset = new Vector2(-2f, 0f),
                ReturnSpeed = 20f, ReturnAccel = 1.5f, Look = Look.Bright, Light = new Color(1f, 0.5f, 0.1f), Debuffs = new[] { (EnemyDebuffs.OnFire, 360, 2) } },
            [ProjKind.ThornChakram] = new Spec { Ai = Ai.Boomerang, Texture = "Projectile_33", W = 38, H = 38, Scale = 0.9f, Time = 3600, Penetrate = -1, TileBox = new Vector2(14f, 14f), Origin = new Vector2(19f, 17f), DrawOffset = new Vector2(2f, 0f),
                ReturnSpeed = 18f, ReturnAccel = 1.2f, WallBounce = true, Debuffs = new[] { (EnemyDebuffs.Poisoned, 420, 2) } },
            [ProjKind.Shuriken] = new Spec { Ai = Ai.Thrown2, Texture = "Projectile_3", W = 22, H = 22, Time = 3600, Penetrate = 4, TileBox = new Vector2(6f, 6f) },
            [ProjKind.ThrowingKnife] = new Spec { Ai = Ai.Thrown2, Texture = "Projectile_48", W = 12, H = 12, Time = 3600, Penetrate = 2, Origin = new Vector2(5f, 6f), DrawOffset = new Vector2(-1f, 0f) },
            [ProjKind.MagicMissile] = new Spec { Ai = Ai.Missile, Texture = "Projectile_16", W = 32, H = 32, Time = 3600, OwnerImmune = 8, TileBox = new Vector2(4f, 4f), TrailLength = 30, Light = new Color(0.08f, 0.32f, 1f) },
            [ProjKind.Flamelash] = new Spec { Ai = Ai.Missile, Texture = "Projectile_34", W = 32, H = 32, Time = 3600, Penetrate = 2, LocalCooldown = 12, TileBox = new Vector2(4f, 4f), TrailLength = 30, Frames = 6, Light = new Color(0.8f, 0.08f, 0.08f) },
            [ProjKind.VilethornBase] = new Spec { Ai = Ai.Vine, Texture = "Projectile_7", W = 28, H = 28, Time = 3600, Penetrate = -1, TileCollide = false, AlphaStart = 1f, Origin = new Vector2(15f, 14f), DrawOffset = new Vector2(1f, 0f) },
            [ProjKind.VilethornTip] = new Spec { Ai = Ai.Vine, Texture = "Projectile_8", W = 28, H = 28, Time = 3600, Penetrate = -1, TileCollide = false, AlphaStart = 1f, Origin = new Vector2(15f, 14f), DrawOffset = new Vector2(1f, 0f) },
            [ProjKind.WaterStream] = new Spec { Ai = Ai.Stream, W = 18, H = 18, Time = 3600, Penetrate = 5, Extra = 2, Look = Look.Invisible },
            [ProjKind.StarCloakStar] = new Spec { Ai = Ai.CloakStar, Texture = "Projectile_726", W = 24, H = 24, Time = 3600, Penetrate = 2, LocalCooldown = 10, AlphaStart = 50f / 255f, TileCollide = false, ArmorPen = 25 },
            // Minion shots (spec-minions-flying §§4.2, 5.2, 7.4): the Hornet's stinger, the Imp's unseen fireball, Retanimini's laser.
            [ProjKind.HornetStinger] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_374", W = 10, H = 10, Scale = 0.9f, Time = 300, NoGravity = true, Dust = 18, Origin = new Vector2(5f, 4f), DrawOffset = new Vector2(0.5f, -0.5f) },
            [ProjKind.ImpFireball] = new Spec { Ai = Ai.Arrow, W = 12, H = 12, Time = 300, Extra = 1, Penetrate = 5, TypeCooldown = 6, NoGravity = true, Look = Look.Invisible, Dust = 6 },
            [ProjKind.MiniRetinaLaser] = new Spec { Ai = Ai.Arrow, Texture = "Projectile_389", W = 4, H = 4, Scale = 1.2f, Time = 300, Extra = 2, NoGravity = true, AlphaStart = 1f, Light = new Color(0.6f, 0f, 0.375f), Origin = new Vector2(1f, 2f), DrawOffset = new Vector2(-1f, 0f) },
        };

        sealed class Proj
        {
            public ProjKind Kind;
            public Spec Spec;
            public int Damage, Penetrate, TimeLeft, Ticks, Bounces, Extra;
            public float Knockback, Crit, Scale, Alpha, Ai0, Ai1;
            public Vector2 Pos, Vel; // world centre; Terraria px per update, y up
            public float Rot;        // degrees, Unity's (counter-clockwise)
            public bool Returning, Dead, HitTile;
            public int SoundDelay;
            /// <summary>Updates run so far; updates to wait before it starts (the Luminite trail).</summary>
            public int Updates, Delay;
            /// <summary>Dev: where it was first drawn.</summary>
            public Vector2? ShownAt;
            /// <summary>Whether it meets blocks now (the hallow star only from the impact's height down).</summary>
            public bool Collide, Stopped;
            public float LaunchSpeed;
            public Vector2 StartPos, StartVel;
            /// <summary>The Chlorophyte Bullet's quarry.</summary>
            public HealthManager Target;
            /// <summary>The enemy it last hit (left out of its blast).</summary>
            public int LastHit;
            public readonly Dictionary<int, int> Immune = new Dictionary<int, int>();
            public readonly List<Vector2> Trail = new List<Vector2>();
            public AudioSource Fuse;
            /// <summary>Projectile.direction: which way it faces (frozen while a boomerang comes back).</summary>
            public float Dir = 1f;
            /// <summary>Terraria's own rotation (clockwise radians), for the ones drawn by their own rules.</summary>
            public float TRot;
            public int Frame, FrameCounter;
            /// <summary>A spear's itemAnimation this tick.</summary>
            public int Anim;
            /// <summary>The magic missiles: still on the player's channel, let go, past their first hit; their dark transit (localAI[0]).</summary>
            public bool Channel, Free, Spent;
            public float LocalAi0, LocalAi1;
            /// <summary>Its own hitbox (Terraria px) when it differs from its kind's (a wall of a chosen height, a cloud of one of three sizes); zero = the spec's.</summary>
            public Vector2 HitSize;
            public Vector2 OldVel;
            /// <summary>Shot by a melee item (the Magma Stone's flames and Hellfire).</summary>
            public bool Melee;
            /// <summary>ProjectileID.Sets.MinionShot: a minion's shot, so whip tags add to its hits.</summary>
            public bool MinionShot;
            /// <summary>The projectile it belongs to (ProjectileKey in ai[1]: a beam's prism or cannon, an orbiting shard's sphere).</summary>
            public Proj Parent;
            /// <summary>The damage class of what shot it (its parent's for a projectile's own children).</summary>
            public DamageClass Class;
            /// <summary>Projectile.arrow.</summary>
            public bool Arrow;
            /// <summary>AI_001_Features (ai[2]): 1 charmed by the Harpy Charm, 2 spent, 4 Phoenix-quivered, 8 Seraph-necklaced.</summary>
            public byte Harpy;
            /// <summary>The Mana Cloak's star from a magic hit (ai[2] = 1): it lands and leaves its star to pick up.</summary>
            public bool ManaDrop;
            /// <summary>Projectile.numHits: enemies it has hit (the Spectre hood heals less for each).</summary>
            public int Hits;
            /// <summary>localNPCHitCooldown when it changes by state (a flail's); 0 = the spec's.</summary>
            public int Cooldown;
            /// <summary>ownerHitCheck by state (a spinning flail's).</summary>
            public bool OwnerCheck;
            /// <summary>The item it was thrown from (a yoyo's, a flail's).</summary>
            public ItemType Item;
            /// <summary>A point it goes for (a yoyo's spot by the cursor, Terraria px).</summary>
            public Vector2 Point;
            /// <summary>A gem bolt's features (its gem's and the robe's) and ArPenSpread's armour penetration.</summary>
            public GemFx Gem;
            /// <summary>A held spell's clock short of mana (<see cref="MagicTick"/>).</summary>
            public float SlowAcc;
            public int GemArmorPen;
            /// <summary>A flail wielded with its partner (Blue Moon and Sunfury), and the off-hand one of the pair; a second timer.</summary>
            public bool Dual, OffHand;
            public float Timer2;
            /// <summary>Its number in a LAN game's snapshots (0 until first sent).</summary>
            public int NetId;
        }

        sealed class Pending
        {
            public int Ticks;
            public ProjKind Kind;
            public float Speed;
            public int Damage;
            public float Knockback, Crit;
            /// <summary>Its own shot instead (the player's centre and the aim's direction, world units).</summary>
            public System.Action<Vector2, Vector2> Then;
            public DamageClass Class;
        }

        /// <summary>The damage class new projectiles get: the weapon firing, or the projectile whose update makes them.</summary>
        public static DamageClass SpawnClass;

        /// <summary>Something to fire a number of ticks from now, aimed where the player aims then (a burst's later shots).</summary>
        public void Later(int ticks, System.Action<Vector2, Vector2> fire) => pending.Add(new Pending { Ticks = ticks, Then = fire, Class = SpawnClass });

        readonly List<Proj> live = new List<Proj>();
        readonly List<Pending> pending = new List<Pending>();
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        /// <summary>NPC.immune[player]: an enemy hit by a non-local-immunity projectile can't be hit by another such shot for 10 ticks.</summary>
        readonly Dictionary<int, int> npcImmune = new Dictionary<int, int>();
        /// <summary>Projectile.perIDStaticNPCImmunity: an enemy immune to a whole kind of projectile for a while.</summary>
        readonly Dictionary<(ProjKind, int), int> typeImmune = new Dictionary<(ProjKind, int), int>();
        static readonly List<(ProjKind, int)> typeKeys = new List<(ProjKind, int)>();
        float tickAccumulator;
        NailAttackProxy blastProps;
        int blastPulses;
        Vector2 blastCenter, blastSize;
        float blastFacing;

        /// <summary>Dev: explosions so far, with what they did.</summary>
        public static string DevLastBlast = "";
        public int Count => live.Count;

        /// <summary>Dev: how many of each kind have been made so far.</summary>
        public static readonly Dictionary<ProjKind, int> DevSpawned = new Dictionary<ProjKind, int>();

        public static int DevMade(ProjKind kind) => DevSpawned.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>Dev: the kind of the newest projectile made, and where it started.</summary>
        public static ProjKind DevLastKind = ProjKind.None;
        public static Vector2 DevLastFrom;

        /// <summary>Dev: how many enemy hits each kind has landed.</summary>
        public static readonly Dictionary<ProjKind, int> DevHits = new Dictionary<ProjKind, int>();

        public static int DevHit(ProjKind kind) => DevHits.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>Dev: the newest of a kind, described (where, how fast, its state).</summary>
        public string DevDescribe(ProjKind kind)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var q = live[i];
                if (q.Kind != kind || q.Dead) continue;
                return $"位置 {q.Pos:F2} 速度 {q.Vel.magnitude:F1}px 穿透 {q.Penetrate} 返回={q.Returning} 放开={q.Free} 目标={(q.Target != null ? q.Target.name : "无")}";
            }
            return "无";
        }

        /// <summary>Dev: how many of a kind are alive (counting ones still waiting to start).</summary>
        /// <summary>Dev: every projectile gone at once, without its death (one test's shots out of the next's way).</summary>
        public void DevClear()
        {
            foreach (var q in live) q.Dead = true;
            pending.Clear();
        }

        public int DevCount(ProjKind kind)
        {
            int n = 0;
            foreach (var q in live) if (q.Kind == kind && !q.Dead) n++;
            return n;
        }

        /// <summary>Dev: where the newest drawn projectile of a kind was first drawn (world centre).</summary>
        public Vector2? DevFirstDrawn(ProjKind kind)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].Kind == kind && live[i].ShownAt != null) return live[i].ShownAt;
            return null;
        }

        /// <summary>Dev: where the newest projectile of a kind is (world centre), if one is alive.</summary>
        public Vector2? DevNewest(ProjKind kind)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].Kind == kind && !live[i].Dead) return live[i].Pos;
            return null;
        }

        public static TerraProjectiles Create(Transform parent)
        {
            var go = new GameObject("Terra Projectiles");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<TerraProjectiles>();
            Instance.blastProps = new NailAttackProxy(go.transform, isSwing: false);
            return Instance;
        }

        /// <summary>
        /// A projectile from the player: velocity in px per update (y up), damage already with
        /// bonuses. extraUpdates adds updates per tick (the Magic Quiver's arrows); useTicks is the
        /// weapon's use time, for the Ruby Staff's second bolt halfway through it.
        /// </summary>
        public void Shoot(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, int extraUpdates = 0, int useTicks = 0, bool melee = false)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, crit, extraUpdates, useTicks);
            if (p != null) p.Melee = melee;
        }

        /// <summary>Projectile.GemStaffFeatures (1.4.5, ai[0]): what a gem staff's bolt does, by its gem and the robe worn.</summary>
        [System.Flags]
        public enum GemFx : byte { None = 0, Homing = 1, Aoe = 2, Swirl = 4, FastSlow = 8, Bounce = 16, ArPen = 32, Bigger = 64, Repeats = 128 }

        /// <summary>Each gem's own feature (PackGemStaffFeatures: Amethyst homing, Topaz the burst, Sapphire the twins, Emerald fast then slow, Amber bouncing, Ruby the spread, Diamond the bigger hitbox).</summary>
        public static GemFx GemOf(ProjKind kind) => kind switch
        {
            ProjKind.AmethystBolt => GemFx.Homing, ProjKind.TopazBolt => GemFx.Aoe, ProjKind.SapphireBolt => GemFx.Swirl, ProjKind.EmeraldBolt => GemFx.FastSlow,
            ProjKind.AmberBolt => GemFx.Bounce, ProjKind.RubyBolt => GemFx.ArPen, ProjKind.DiamondBolt => GemFx.Bigger, _ => GemFx.None,
        };

        /// <summary>The gem robes' (1282-1287, 4256): their gem's feature on any gem staff's bolt.</summary>
        public static GemFx RobeGem(ItemType body) => body switch
        {
            ItemType.AmethystRobe => GemFx.Homing, ItemType.TopazRobe => GemFx.Aoe, ItemType.SapphireRobe => GemFx.Swirl, ItemType.EmeraldRobe => GemFx.FastSlow,
            ItemType.AmberRobe => GemFx.Bounce, ItemType.RubyRobe => GemFx.ArPen, ItemType.DiamondRobe => GemFx.Bigger, _ => GemFx.None,
        };

        /// <summary>
        /// A gem staff's bolt with its twin flag (ai1: the second of SwirlTwins weaves out of step) and its features;
        /// FinalizeProjectile_GemStaves (P:22261-22298): through one more for FastThenSlow or BiggerHitbox, two more for
        /// CanBounce (the most of them counts), twice as long for FastThenSlow, 4 armour penetration for ArPenSpread.
        /// </summary>
        /// <summary>
        /// Player.GetSlowMagicUseRate (1.4.5): short of mana a held spell's own clock goes 1/2.5 as fast (ai[0] +=
        /// 0.4): whether it moves on this tick (two in five then; every tick otherwise).
        /// </summary>
        static bool MagicTick(Proj p)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !mod.SlowMagicUse) return true;
            p.SlowAcc += 1f / TerraMod.SlowMagic;
            if (p.SlowAcc < 1f) return false;
            p.SlowAcc -= 1f;
            return true;
        }

        /// <summary>Dev: the features of the last gem bolt shot.</summary>
        public static GemFx DevLastGem;

        public void ShootGem(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float twin, GemFx fx)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = twin;
            p.Gem = fx;
            DevLastGem = fx;
            int more = Mathf.Max((fx & GemFx.Bounce) != 0 ? 2 : 0, (fx & (GemFx.FastSlow | GemFx.Bigger)) != 0 ? 1 : 0);
            p.Penetrate = 1 + more;
            p.TimeLeft = (fx & GemFx.FastSlow) != 0 ? 600 : 300;
            p.GemArmorPen = (fx & GemFx.ArPen) != 0 ? 4 : 0;
        }

        /// <summary>A minion's shot (stinger, fireball, laser): the minion's damage and knockback, never a crit of its own.</summary>
        public void ShootMinion(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, int penetrate = 0)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, 0f);
            if (p == null) return;
            p.MinionShot = true;
            // Its own pierce instead of the kind's (the Ballista's bolt through 7 with the Squire's sets).
            if (penetrate != 0) p.Penetrate = penetrate;
        }

        /// <summary>A Star Cloak star, through everything until below topY (the player's top when it was called down).</summary>
        public void ShootCloakStar(Vector2 from, Vector2 velocity, int damage, float topY) => ShootCloakStar(ProjKind.StarCloakStar, from, velocity, damage, topY);

        /// <summary>Starfury's star, falling through everything above floorY (the cursor's height, or the first open spot over it).</summary>
        public void ShootStar(Vector2 from, Vector2 velocity, int damage, float crit, float floorY)
        {
            var p = Spawn(ProjKind.StarfuryStar, from, velocity, damage, 0f, crit);
            if (p == null) return;
            p.Ai1 = floorY;
            p.Melee = true;
        }

        /// <summary>Player.ItemCheck_CheckCanUse: a boomerang can't be thrown while one of its own kind is out.</summary>
        public bool Holds(ProjKind kind)
        {
            if (!specs.TryGetValue(kind, out var spec) || (spec.Ai != Ai.Boomerang && !spec.Held)) return false;
            int n = 0, max = spec.MaxOut > 0 ? spec.MaxOut : 1;
            foreach (var q in live) if (q.Kind == kind && !q.Dead && (spec.HoldsWhile == null || spec.HoldsWhile(q)) && ++n >= max) return true;
            return false;
        }

        Proj Spawn(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, int extraUpdates = 0, int useTicks = 0)
        {
            if (!specs.TryGetValue(kind, out var spec)) return null;
            if (spec.Ai == Ai.Boomerang && Holds(kind)) return null; // one out at a time
            // AI_001's launch clamp: no faster than 16 px an update on either axis.
            if (spec.Ai == Ai.Arrow)
                for (int g = 0; g < 100 && (Mathf.Abs(velocity.x) >= 16f || Mathf.Abs(velocity.y) >= 16f); g++) velocity *= 0.97f;
            var p = new Proj
            {
                Kind = kind, Spec = spec, Damage = damage, Knockback = knockback, Crit = crit,
                Pos = from, Vel = velocity, Penetrate = spec.Penetrate,
                TimeLeft = spec.Ai == Ai.Thrown ? spec.Fuse : spec.Time, Scale = spec.Scale, Extra = Mathf.Max(spec.Extra, extraUpdates),
                Alpha = spec.AlphaStart, Collide = spec.TileCollide, LaunchSpeed = velocity.magnitude, StartPos = from, StartVel = velocity,
            };
            p.Rot = Angle(velocity);
            p.Class = SpawnClass;
            p.Arrow = IsArrow(kind);
            DevSpawned[kind] = DevMade(kind) + 1;
            DevLastKind = kind;
            DevLastFrom = from;
            p.Dir = velocity.x < 0f ? -1f : 1f;
            p.OldVel = velocity;
            // A spear lives the use's itemAnimation; a magic missile starts on the player's channel.
            if (spec.Ai == Ai.Spear) p.Ai1 = useTicks > 0 ? useTicks : 22;
            if (spec.Ai == Ai.Missile) p.Channel = true;
            live.Add(p);
            if (spec.Ai == Ai.GemBolt) Sound("Item_205");
            if (kind == ProjKind.Bomb || kind == ProjKind.Dynamite) StartFuse(p);
            return p;
        }

        static float Angle(Vector2 v) => v.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;

        void Update()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm != null && gm.isPaused)
            {
                Draw();
                return;
            }
            if (blastPulses > 0) PulseBlastProps();
            tickAccumulator += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                Tick();
            }
            Draw();
        }

        void Tick()
        {
            FxParticles.Tick();
            AccessoryEffects.Tick();
            ArmorEffects.Tick();
            FallingStars.Tick();
            MeleeSpecials.TickNow();
            armorTicks++;
            AccTick();
            if (PhantasmTime > 0) PhantasmTime--;
            if (npcImmune.Count > 0)
            {
                var keys = new List<int>(npcImmune.Keys);
                foreach (var k in keys) if (--npcImmune[k] <= 0) npcImmune.Remove(k);
            }
            if (typeImmune.Count > 0)
            {
                typeKeys.Clear();
                typeKeys.AddRange(typeImmune.Keys);
                foreach (var k in typeKeys) if (--typeImmune[k] <= 0) typeImmune.Remove(k);
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (--pending[i].Ticks > 0) continue;
                var s = pending[i];
                pending.RemoveAt(i);
                var hero = HeroController.SilentInstance;
                if (hero == null) continue;
                Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
                var dir = (TerraMod.Instance != null ? TerraMod.Instance.DevAim ?? TerraMod.Instance.AimPoint : center + Vector2.right) - center;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right;
                SpawnClass = s.Class;
                if (s.Then != null) s.Then(center, dir.normalized);
                else Shoot(s.Kind, center, dir.normalized * s.Speed, s.Damage, s.Knockback, s.Crit);
                SpawnClass = DamageClass.None;
            }
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i];
                p.Ticks++;
                var keys = new List<int>(p.Immune.Keys);
                foreach (var k in keys) if (--p.Immune[k] <= 0) p.Immune.Remove(k);
                int updates = p.Extra;
                // What it makes as it goes is of its class.
                SpawnClass = ClassOf(p);
                for (int u = 0; u <= updates && !p.Dead; u++) Step(p, u == 0);
            }
            SpawnClass = DamageClass.None;
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].Dead) Remove(i);
            TickGhosts();
        }

        void Remove(int i)
        {
            var p = live[i];
            if (p.Fuse != null) Destroy(p.Fuse);
            live.RemoveAt(i);
        }

        // --- One update of one projectile. ---

        void Step(Proj p, bool firstOfTick)
        {
            var s = p.Spec;
            // Projectile.Update: a delayed projectile only counts down, unseen, until it starts.
            if (p.Delay > 0)
            {
                p.Delay--;
                return;
            }
            p.Updates++;
            if (s.TrailLength > 0 && s.Ai != Ai.Missile)
            {
                p.Trail.Insert(0, p.Pos);
                if (p.Trail.Count > s.TrailLength) p.Trail.RemoveAt(p.Trail.Count - 1);
            }
            if (--p.TimeLeft <= 0 && s.Ai != Ai.Boomerang)
            {
                Kill(p);
                return;
            }
            var hero = HeroController.SilentInstance;
            switch (s.Ai)
            {
                case Ai.Thrown:
                    StepThrown(p);
                    break;
                case Ai.Arrow:
                    // AI_001: gravity after a few updates (falling up for the Shimmer Arrow, by age for
                    // the Luminite arrow), rotation along the flight, the fade-in.
                    if (s.GravityAfterAge >= 0)
                    {
                        if (p.Updates > s.GravityAfterAge) p.Vel.y -= 0.1f;
                    }
                    else if (!s.NoGravity)
                    {
                        p.Ai0++;
                        if (p.Ai0 >= s.GravityDelay)
                        {
                            p.Ai0 = s.GravityDelay;
                            p.Vel.y -= s.Gravity;
                        }
                    }
                    p.Vel.y = Mathf.Max(p.Vel.y, -16f);
                    if (s.Gravity < 0f) p.Vel.y = Mathf.Min(p.Vel.y, 16f);
                    if (p.Alpha > 0f)
                    {
                        float fade = s.AlphaFade >= 0f ? s.AlphaFade : (s.AlphaFade == -1f ? p.Vel.magnitude * 0.9f : p.Vel.magnitude * 0.3f) / 255f;
                        p.Alpha = Mathf.Max(0f, p.Alpha - fade);
                    }
                    StepAmmo(p, firstOfTick);
                    if (p.Kind == ProjKind.Laser && p.Scale < 3f)
                    {
                        // The laser grows in size, punch and pierce as it flies.
                        p.Scale += 0.0189f;
                        p.Knockback *= 1.0025f;
                        if (++p.Ai1 % 25 == 0)
                        {
                            p.Damage += Random.Range(1, 3);
                            p.Penetrate++;
                        }
                    }
                    p.Rot = Angle(p.Vel);
                    if (p.Kind == ProjKind.FlamingArrow) DustBox(p, 6, Vector2.zero, 100, 1f, false);
                    else if (p.Kind == ProjKind.UnholyArrow && Random.Range(0, 5) == 0) DustBox(p, 14, Vector2.zero, 150, 1.1f, false);
                    else if (p.Kind == ProjKind.JesterArrow) DustBox(p, new[] { 15, 57, 58 }[Random.Range(0, 3)], p.Vel * 0.5f, 150, 1.2f, false);
                    else if (p.Kind == ProjKind.TerraBeam && Random.value < 0.4f) DustBox(p, 107, Vector2.zero, 100, 0.9f, true);
                    else if (p.Kind == ProjKind.HornetStinger || p.Kind == ProjKind.ImpFireball || p.Kind == ProjKind.MiniRetinaLaser) MinionShotUpdate(p);
                    else if (p.Kind == ProjKind.PygmySpear) StepPygmySpear(p);
                    if (p.Harpy != 0) HarpyArrowUpdate(p);
                    break;
                case Ai.Spark:
                    StepSpark(p);
                    break;
                case Ai.GemBolt:
                    StepGem(p);
                    break;
                case Ai.WaterBolt:
                    p.Rot += 0.3f * Mathf.Rad2Deg * Mathf.Sign(p.Vel.x == 0f ? 1f : p.Vel.x);
                    for (int k = 0; k < 5; k++)
                    {
                        int d = DustAt(p.Pos - p.Vel * Px / 3f * k, 172, 8, Vector2.zero, 100, 1.2f, true);
                        if (d >= 0) TerraParticles.Instance[d].Velocity = TerraParticles.Instance[d].Velocity * 0.1f + Flip(p.Vel) * 0.1f;
                    }
                    if (Random.Range(0, 5) == 0)
                    {
                        int d = DustAt(p.Pos, 172, 8, Vector2.zero, 100, 0.6f, false);
                        if (d >= 0) TerraParticles.Instance[d].Velocity = TerraParticles.Instance[d].Velocity * 0.25f + Flip(p.Vel) * 0.5f;
                    }
                    break;
                case Ai.Fireball:
                    // AI_008: two flames a tick, falling only after 20 ticks.
                    if (firstOfTick)
                        for (int k = 0; k < 2; k++) DustBox(p, s.Dust, Flip(p.Vel) * 0.2f * 0.3f, 100, 2f, true);
                    if (++p.Ai1 >= 20f) p.Vel.y = Mathf.Max(p.Vel.y - 0.2f, -16f);
                    p.Rot -= 0.3f * Mathf.Rad2Deg * Mathf.Sign(p.Vel.x == 0f ? 1f : p.Vel.x);
                    break;
                case Ai.Scythe:
                    // AI_018: drifts for 29 ticks, then speeds up by 6% a tick for 70 more.
                    p.Rot -= 0.8f * Mathf.Rad2Deg * Mathf.Sign(p.Vel.x == 0f ? 1f : p.Vel.x);
                    p.Ai0++;
                    if (p.Ai0 >= 30f && p.Ai0 < 100f) p.Vel *= 1.06f;
                    else if (p.Ai0 >= 100f) p.Ai0 = 200f;
                    for (int k = 0; k < 2; k++) DustBox(p, 27, Vector2.zero, 100, 1f, true);
                    break;
                case Ai.Boomerang:
                    StepBoomerang(p, hero);
                    if (p.Dead) return;
                    break;
                case Ai.Star:
                    StepStar(p);
                    break;
                case Ai.Shard:
                    // AI_024: drags to a stop, spins, then shrinks away after 15 updates.
                    p.Rot -= p.Vel.x * 0.2f * Mathf.Rad2Deg;
                    if (++p.Ai1 > 15f)
                    {
                        p.Scale -= 0.05f;
                        if (p.Scale <= 0.2f)
                        {
                            p.Dead = true;
                            return;
                        }
                    }
                    p.Vel *= 0.96f;
                    break;
                case Ai.LuminiteTrail:
                    StepLuminiteTrail(p, firstOfTick);
                    if (p.Dead) return;
                    break;
                case Ai.Starfury:
                    StepStarfury(p);
                    break;
                case Ai.Beam:
                    StepBeam(p);
                    break;
                case Ai.Bee:
                    StepBee(p);
                    break;
                case Ai.Spear:
                    // Held by the player, not moved: it hits where the thrust puts it.
                    StepSpear(p, hero);
                    if (!p.Dead) HitEnemies(p);
                    return;
                case Ai.Thrown2:
                    StepThrown2(p);
                    break;
                case Ai.Missile:
                    StepMissile(p, hero);
                    if (p.Dead) return;
                    break;
                case Ai.Vine:
                    // UpdatePosition skips aiStyle 4: each thorn stays where it grew, hurting even while it fades in.
                    if (p.Kind == ProjKind.NettleStem || p.Kind == ProjKind.NettleStem2 || p.Kind == ProjKind.NettleTip) StepNettle(p);
                    else StepVine(p);
                    if (!p.Dead) HitEnemies(p);
                    return;
                case Ai.Stream:
                    StepStream(p);
                    if (p.Dead) return;
                    break;
                case Ai.CloakStar:
                    StepCloakStar(p);
                    break;
                case Ai.Custom:
                    s.Update?.Invoke(this, p);
                    if (p.Dead) return;
                    break;
            }
            // The others can add their own part to their AI (dust, spin, conversions).
            if (s.Ai != Ai.Custom && s.Update != null)
            {
                s.Update(this, p);
                if (p.Dead) return;
            }
            // A melee projectile with the Magma Stone sheds flames (Projectile.cs:17437).
            if (p.Melee && Equipment.Current.MagmaStone && Random.Range(0, 3) != 0)
            {
                var corner = TerraParticles.ToPx(p.Pos) - new Vector2(s.W * 0.5f + 4f, s.H * 0.5f + 4f);
                int fd = TerraParticles.Instance != null ? TerraParticles.Instance.NewDust(corner, s.W + 8, s.H + 8, 6, Flip(p.Vel).x * 0.2f, Flip(p.Vel).y * 0.2f, 100, Random.Range(0, 2) == 0 ? 1.5f : 2f) : -1;
                if (fd >= 0)
                {
                    TerraParticles.Instance[fd].NoGravity = true;
                    TerraParticles.Instance[fd].Velocity *= 2f;
                }
            }

            if (!s.OwnMove) Move(p);
            if (p.Dead) return;
            if (s.Ai == Ai.Missile) PushMissileTrail(p);
            HitEnemies(p);
        }

        /// <summary>AI_016: the fuse burns, gravity after a few ticks, rolling to a stop on the ground.</summary>
        void StepThrown(Proj p)
        {
            bool grenade = IsGrenade(p.Kind), dynamite = p.Kind == ProjKind.Dynamite;
            // The Sticky Grenade, once on a block, holds there (AI_016 for 397: P:47822-47880).
            if (p.Kind == ProjKind.StickyGrenadeProj && p.Stopped)
            {
                p.Vel = Vector2.zero;
                return;
            }
            // Resting on the ground (the last move ended on it): rolling friction.
            if (p.Vel.y == 0f)
            {
                p.Vel.x *= 0.97f;
                if (dynamite) p.Vel.x *= 0.99f;
                if (Mathf.Abs(p.Vel.x) < 0.01f) p.Vel.x = 0f;
            }
            p.Ai0++;
            if (p.Ai0 > (grenade ? 10f : 5f))
            {
                p.Ai0 = 10f;
                p.Vel.y -= 0.2f;
            }
            p.Rot -= p.Vel.x * 0.1f * Mathf.Rad2Deg;
            if (grenade) return;
            // The burning fuse at the tip: a puff of smoke and a spark.
            if (Random.value < 0.5f)
            {
                float h = p.Spec.H * Px;
                var up = (Vector2)(Quaternion.Euler(0f, 0f, p.Rot) * Vector2.up);
                int d = DustAt(p.Pos + up * (h / 2f) * 1.1f, 31, 2, Vector2.zero, 0, 0.1f + Random.Range(0, 5) * 0.1f, true);
                if (d >= 0) TerraParticles.Instance[d].FadeIn = 1.5f + Random.Range(0, 5) * 0.1f;
                DustAt(p.Pos + up * (h / 2f + 6f * Px) * 1.1f, 6, 2, Vector2.zero, 0, 1f + Random.Range(0, 5) * 0.1f, true);
            }
        }

        /// <summary>AI_002 for the Wand of Sparking: a spark shedding fire that sags after 20 ticks and is gone at 60.</summary>
        void StepSpark(Proj p)
        {
            p.Ai0++;
            if (p.Ai0 > 3f)
            {
                float chance = p.Ai0 <= 10f ? p.Ai0 * 10f : p.Ai0 <= 20f ? 100f : 100f * (1f - (p.Ai0 - 20f) / 40f);
                if (Random.value * 100f < chance)
                {
                    int d = DustAt(p.Pos, p.Spec.Dust, 10, Vector2.zero, 150, 1f, true);
                    if (d >= 0)
                    {
                        ref var dust = ref TerraParticles.Instance[d];
                        dust.Velocity = dust.Velocity * 2f + Flip(p.Vel);
                        dust.Scale *= 1.6f;
                    }
                }
            }
            if (p.Ai0 >= 20f)
            {
                p.Vel.x *= 0.99f;
                p.Vel.y = Mathf.Max(p.Vel.y - 0.1f, -32f);
            }
            if (p.Ai0 - 20f >= 40f) p.Dead = true;
        }

        /// <summary>AI_029 (1.4.5): gem dust, and the amethyst bolt's homing.</summary>
        void StepGem(Proj p)
        {
            // The Forbidden relic's gem sheds its sand every update (P:38527-38540).
            bool forbidden = p.Kind == ProjKind.ForbiddenGem;
            for (int k = 0; k < (forbidden ? 1 : 2); k++)
                if (forbidden || Random.Range(0, 5) == 0)
                {
                    int d = DustAt(p.Pos, p.Spec.Dust, 10, Vector2.zero, 50, 1.2f, true);
                    if (d >= 0) TerraParticles.Instance[d].Velocity = Flip(p.Vel) * 0.3f;
                }
            if ((p.Gem & GemFx.Homing) != 0)
            {
                // P:38563-38611: 500 px, twice as far with FastThenSlow or CanBounce; FastThenSlow up to 16 instead of 8 and
                // turning less from 30 to 40.
                bool fast = (p.Gem & GemFx.FastSlow) != 0;
                float range = (p.Gem & (GemFx.FastSlow | GemFx.Bounce)) != 0 ? 1000f : 500f, top = fast ? 16f : 8f;
                var target = NearestEnemy(p.Pos, range * Px);
                if (target.HasValue)
                {
                    float speed = p.Vel.magnitude;
                    if (speed < top)
                    {
                        p.Vel += p.Vel.normalized * 0.1f;
                        p.Vel *= 1.1f;
                    }
                    var to = (target.Value - p.Pos) / Px;
                    float dist = to.magnitude;
                    float t = Remap(dist, range, 0f, 0f, 1f) * 0.1f + 0.01f;
                    if (speed < top) t = (t + 0.05f) * Remap(speed, 1f, 3f, 10f, 1f);
                    if (fast) t *= Remap(speed, 30f, 40f, 1f, 0f);
                    float cur = Mathf.Atan2(p.Vel.y, p.Vel.x), want = Mathf.Atan2(to.y, to.x);
                    float a = Mathf.MoveTowardsAngle(cur * Mathf.Rad2Deg, want * Mathf.Rad2Deg, t * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    p.Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * p.Vel.magnitude;
                    p.Vel = Vector2.Lerp(p.Vel, Vector2.ClampMagnitude(to, top), Remap(dist, 150f, 0f, 0f, 0.2f));
                }
            }
            if ((p.Gem & GemFx.Swirl) != 0)
            {
                // SwirlTwins (P:38614-38626): weaving 6 px across its flight, the twin out of step.
                float phase = p.Ai1 * Mathf.PI, now = Mathf.Sin(p.LocalAi0 * Mathf.PI * 2f * 0.05f + phase), next = Mathf.Sin((p.LocalAi0 + 1f) * Mathf.PI * 2f * 0.05f + phase);
                p.LocalAi0++;
                var across = RotateBy(Vector2.right, Mathf.Atan2(-p.Vel.y, p.Vel.x) + Mathf.PI / 2f);
                p.Pos += Flip(across) * (next - now) * 6f * Px;
            }
            if ((p.Gem & GemFx.FastSlow) != 0 || forbidden)
            {
                // FastThenSlow (P:38628-38641): slowing 3% an update, then shrinking away once all but still.
                if (p.Vel.magnitude > 0.1f) p.Vel *= 0.97f;
                else
                {
                    p.Scale -= 0.005f;
                    if (p.Scale < 0.1f)
                    {
                        Kill(p);
                        return;
                    }
                }
            }
            p.Rot = Angle(p.Vel) + 90f;
            p.Trail.Insert(0, p.Pos);
            if (p.Trail.Count > 30) p.Trail.RemoveAt(p.Trail.Count - 1);
        }

        /// <summary>
        /// P:18105-18160: the Amber's off a wall at 80% (losing a pierce), the Emerald's sinks into
        /// it and stays (shrinking away); the others break.
        /// </summary>
        bool GemTile(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if ((p.Gem & GemFx.Bounce) != 0)
            {
                if (hitX) p.Vel.x = -oldVel.x * 0.8f;
                if (hitY) p.Vel.y = -oldVel.y * 0.8f;
                p.Trail.Clear();
                // Homing too: off the wall straight at the nearest enemy in sight within 2400 px (P:18126-18142).
                if ((p.Gem & GemFx.Homing) != 0)
                {
                    var target = NearestEnemy(p.Pos, 2400f * Px);
                    if (target.HasValue && (target.Value - p.Pos).sqrMagnitude > 0f) p.Vel = (target.Value - p.Pos).normalized * oldVel.magnitude * 0.8f;
                }
                if (--p.Penetrate > 0)
                {
                    for (int i = 0; i < 5; i++) DustBox(p, 0, Vector2.zero, 0, 1f, false);
                    Sound("Dig_" + Random.Range(0, 3));
                    return true;
                }
                return false;
            }
            if ((p.Gem & GemFx.FastSlow) != 0 || p.Kind == ProjKind.ForbiddenGem)
            {
                p.Pos += oldVel * 0.99f * Px;
                p.Vel = (oldVel.sqrMagnitude > 1e-8f ? oldVel.normalized : Vector2.zero) * 0.0001f;
                p.Collide = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// AI_003: out for 30 ticks, then home on the player (9 px a tick turning 0.4, the
        /// Flamarang 20 / 1.5, the Thorn Chakram 18 / 1.2), whooshing every 8 ticks.
        /// </summary>
        void StepBoomerang(Proj p, HeroController hero)
        {
            var s = p.Spec;
            // The whoosh every 8 ticks (soundDelay counts updates); Paladin's Hammer only on its way back.
            if (p.SoundDelay > 0) p.SoundDelay--;
            else if (p.Kind != ProjKind.PaladinsHammer || p.Returning)
            {
                p.SoundDelay = (p.Kind == ProjKind.PaladinsHammer ? 10 : 8) * (p.Extra + 1);
                Sound("Item_7");
            }
            var pp = TerraParticles.Instance;
            int d;
            switch (p.Kind)
            {
                case ProjKind.Boomerang:
                    if (Random.Range(0, 5) == 0 && (d = DustAt(p.Pos, new[] { 15, 57, 58 }[Random.Range(0, 3)], 22, Vector2.zero, 150, 0.7f, true)) >= 0) pp[d].Velocity += Flip(p.Vel) * 0.25f;
                    break;
                case ProjKind.Flamarang:
                    for (int k = 0; k < 2; k++)
                        if ((d = DustBox(p, 6, Flip(p.Vel) * 0.2f, 100, 2f, true)) >= 0) pp[d].Velocity *= 0.3f;
                    break;
                case ProjKind.ThornChakram:
                    DustBox(p, 40, Flip(p.Vel) * 0.25f, 0, 1.4f, true);
                    break;
                default:
                    MoreBoomerangDust(p);
                    break;
            }
            if (!p.Returning)
            {
                p.Dir = p.Vel.x < 0f ? -1f : 1f;
                p.Ai1++;
                // The hardmode ones have their own way out (TerraProjectilesBoomerangs.cs).
                if (!BoomerangOut(p, hero) && p.Ai1 >= s.OutTicks) p.Returning = true;
            }
            else if (hero != null)
            {
                Vector2 to = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center - p.Pos;
                if (to.magnitude > (p.Kind == ProjKind.PaladinsHammer ? 6000f : 3000f) * Px)
                {
                    p.Dead = true;
                    return;
                }
                // P:40167-40180: melee speed speeds the way home (toMax = 1/meleeSpeed, Remap(1.5, 0, 1, 1, toMax), 1 to 10).
                float boost = Mathf.Clamp(1f + 1.5f * (Mathf.Min(3f, 1f + Equipment.Current.MeleeSpeed) - 1f), 1f, 10f);
                var target = to.normalized * s.ReturnSpeed * boost;
                if (s.ReturnVector)
                {
                    var gap = target - p.Vel;
                    if (gap.sqrMagnitude > 0f) p.Vel += gap.normalized * Mathf.Min(s.ReturnAccel * boost, gap.magnitude);
                }
                else
                {
                    p.Vel.x = Approach(p.Vel.x, target.x, s.ReturnAccel * boost);
                    p.Vel.y = Approach(p.Vel.y, target.y, s.ReturnAccel * boost);
                }
                if (hero.GetComponent<BoxCollider2D>().bounds.Intersects(new Bounds(p.Pos, new Vector3(p.Spec.W * Px, p.Spec.H * Px, 10f))))
                {
                    p.Dead = true;
                    return;
                }
            }
            // Paladin's Hammer turns its own way while it is out.
            if (p.Kind != ProjKind.PaladinsHammer || p.Returning)
            {
                p.Rot -= s.SpinRate * Mathf.Rad2Deg * p.Dir;
                p.TRot += s.SpinRate * p.Dir;
            }
        }

        /// <summary>One axis turned towards a wanted speed; turning back across zero adds `reverse` times as much again.</summary>
        static float Approach(float v, float target, float acc = 0.4f, float reverse = 1f)
        {
            if (v < target)
            {
                v += acc;
                if (v < 0f && target > 0f) v += acc * reverse;
            }
            else if (v > target)
            {
                v -= acc;
                if (v > 0f && target < 0f) v -= acc * reverse;
            }
            return v;
        }

        void Move(Proj p)
        {
            var s = p.Spec;
            Vector2 half = TileBox(p) * 0.5f * Px;
            Vector2 total = p.Vel * Px;
            bool collide = p.Collide && !p.Stopped && !(s.Ai == Ai.Boomerang && p.Returning);
            if (!collide)
            {
                p.Pos += total;
                return;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(total.magnitude / (6f * Px)));
            Vector2 step = total / steps;
            bool hitX = false, hitY = false;
            var oldVel = p.Vel;
            for (int i = 0; i < steps && !hitX && !hitY; i++)
            {
                var nx = p.Pos + new Vector2(step.x, 0f);
                if (Blocked(nx, half, false, p)) hitX = true;
                else p.Pos = nx;
                var ny = p.Pos + new Vector2(0f, step.y);
                // Projectiles fall through platforms (bombs, dynamite and grenades too); only Starfury's star lands on them (flag6, P:17650).
                if (Blocked(ny, half, step.y < 0f && (s.Ai == Ai.Starfury || (s.LandsOnPlatforms != null && s.LandsOnPlatforms(p))), p)) hitY = true;
                else p.Pos = ny;
            }
            if (s.Ai == Ai.Thrown && hitY && oldVel.y < 0f) p.Vel.y = 0f;
            if (hitX || hitY) OnTile(p, hitX, hitY, oldVel);
        }

        /// <summary>Projectile.GetCollisionParams: some projectiles touch blocks with a smaller box than they hit with.</summary>
        static Vector2 TileBox(Proj p)
        {
            var s = p.Spec;
            if (p.Kind == ProjKind.Bomb) return new Vector2(14f, 14f);
            if (s.TileBox != Vector2.zero) return s.TileBox;
            if (s.Ai == Ai.GemBolt) return new Vector2(2f, 2f);
            if (s.Ai == Ai.Scythe) return new Vector2(7f, 7f); // 43 - 36
            return new Vector2(s.W, s.H);
        }

        static readonly Collider2D[] solidHits = new Collider2D[8];

        /// <summary>
        /// Whether a box at a point overlaps a block (and, for things falling onto them, a
        /// platform's top), or one of the game's own solid colliders on the terrain layer that
        /// aren't grid cells: dive floors, breakable walls, lifts, doors.
        /// </summary>
        static bool Blocked(Vector2 center, Vector2 half, bool platforms, Proj p)
        {
            int n = Physics2D.OverlapBoxNonAlloc(center, half * 2f, 0f, solidHits, 1 << ChunkColliders.TerrainLayer);
            for (int i = 0; i < n; i++)
            {
                var col = solidHits[i];
                if (col.isTrigger || col.usedByEffector || col.GetComponentInParent<TerraOwned>() != null) continue;
                // An enemy's own solid part (an Elder Baldur's shell, which bars the way like a wall): the
                // shot reaches it and hits it, it isn't stopped by it as by the ground.
                if (col.GetComponentInParent<HealthManager>() != null) continue;
                return true;
            }
            const float c = CellGrid.CellSize;
            int x0 = Mathf.FloorToInt((center.x - half.x) / c), x1 = Mathf.FloorToInt((center.x + half.x - 0.001f) / c);
            int y0 = Mathf.FloorToInt((center.y - half.y) / c), y1 = Mathf.FloorToInt((center.y + half.y - 0.001f) / c);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var k = TerraWorld.Get(x, y);
                    if (Cells.Blocks(k)) return true;
                    // A platform holds up what comes down onto its top from above it.
                    if (platforms && k == CellKind.Platform && p.Pos.y - half.y >= (y + 1) * c - 0.02f) return true;
                }
            return false;
        }

        void OnTile(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            var s = p.Spec;
            if (s.TileHit != null)
            {
                s.TileHit(this, p, hitX, hitY, oldVel);
                return;
            }
            if (s.Ai == Ai.Custom)
            {
                Kill(p);
                return;
            }
            if (s.Ai == Ai.GemBolt && GemTile(p, hitX, hitY, oldVel)) return;
            switch (s.Ai)
            {
                case Ai.Thrown:
                {
                    // The Sticky Grenade sticks to what it meets; the Bouncy one springs back 90% off anything met faster than 1 px (P:18592-18603).
                    if (p.Kind == ProjKind.StickyGrenadeProj)
                    {
                        p.Vel = Vector2.zero;
                        p.Stopped = true;
                        break;
                    }
                    if (p.Kind == ProjKind.BouncyGrenadeProj)
                    {
                        if (hitX && Mathf.Abs(oldVel.x) > 1f) p.Vel.x = -oldVel.x * 0.9f;
                        if (hitY && Mathf.Abs(oldVel.y) > 1f) p.Vel.y = -oldVel.y * 0.9f;
                        break;
                    }
                    // Bounce: 40% back off walls, and off the floor only when coming down hard.
                    float k = p.Kind == ProjKind.Dynamite ? 0.4f * 0.8f : 0.4f;
                    if (hitX) p.Vel.x = -oldVel.x * k;
                    if (hitY && oldVel.y < -0.7f) p.Vel.y = -oldVel.y * k;
                    break;
                }
                case Ai.WaterBolt:
                case Ai.Fireball:
                    Sound("Item_10");
                    p.Bounces++;
                    // The water bolt dies on its fifth wall, the ball of fire on its sixth, the frost ball on its eighth (P:19689-19721).
                    if (p.Bounces >= (s.Ai == Ai.WaterBolt ? 5 : p.Kind == ProjKind.FrostBall ? 8 : 6))
                    {
                        p.Pos += p.Vel * Px;
                        Kill(p);
                        return;
                    }
                    if (hitX) p.Vel.x = -oldVel.x;
                    if (hitY) p.Vel.y = p.Kind == ProjKind.BallofFire && oldVel.y < -4f ? -oldVel.y * 0.8f : -oldVel.y;
                    break;
                case Ai.Boomerang:
                    // Collision.HitTiles and a dig sound; the Thorn Chakram bounces off and keeps going out.
                    for (int i = 0; i < 5; i++) DustBox(p, 1, Vector2.zero, 0, 1f, false);
                    Sound("Dig_" + Random.Range(0, 3));
                    if (s.WallBounce)
                    {
                        if (p.Kind == ProjKind.LightDisc)
                        {
                            // The sparks off the wall, along where it slides, back from the face it met.
                            var o = Flip(oldVel);
                            var slide = new Vector2(hitX ? 0f : o.x, hitY ? 0f : o.y);
                            var at = TerraParticles.ToPx(p.Pos) + (slide.sqrMagnitude > 1e-6f ? slide.normalized : Vector2.right) * 8f * p.Scale;
                            LightDiscBounce(p, at, new Vector2(hitX ? -Mathf.Sign(o.x) : 0f, hitY ? -Mathf.Sign(o.y) : 0f));
                        }
                        if (hitX) p.Vel.x = -oldVel.x;
                        if (hitY) p.Vel.y = -oldVel.y;
                        break;
                    }
                    p.Returning = true;
                    p.Vel = -oldVel;
                    break;
                case Ai.Bee:
                    // A bee turns back off a wall and loses one of its three hits.
                    if (hitX) p.Vel.x = -oldVel.x;
                    if (hitY) p.Vel.y = -oldVel.y;
                    if (--p.Penetrate <= 0) Kill(p);
                    break;
                case Ai.Missile:
                    // Any block ends a magic missile where it is; the Rainbow Rod, while still steered, only slows against it (P:19443).
                    if (p.Kind == ProjKind.RainbowRod && !p.Free)
                    {
                        if (hitX) p.Vel.x *= 0.1f;
                        if (hitY) p.Vel.y *= 0.1f;
                        break;
                    }
                    Kill(p);
                    break;
                case Ai.LuminiteTrail:
                    // It stops where the arrow struck, and lingers till its trail has caught up.
                    p.Pos += oldVel * Px;
                    p.Vel = Vector2.zero;
                    p.Stopped = true;
                    break;
                default:
                    // HandleMovement: the Chlorophyte Arrow turns right round three times, the Nano Bullet
                    // bounces once; each loses a third of its damage on the first and looks for an enemy.
                    if (p.Kind == ProjKind.ChlorophyteArrow || p.Kind == ProjKind.NanoBullet)
                    {
                        bool arrow = p.Kind == ProjKind.ChlorophyteArrow;
                        if (++p.Bounces == 1) p.Damage = (int)(p.Damage * 0.66f);
                        if (p.Bounces >= (arrow ? 4 : 2))
                        {
                            p.HitTile = true;
                            p.Pos += p.Vel * Px;
                            Kill(p);
                            return;
                        }
                        if (arrow) p.Vel = -oldVel;
                        else
                        {
                            if (hitX) p.Vel.x = -oldVel.x;
                            if (hitY) p.Vel.y = -oldVel.y;
                        }
                        float speed = p.Vel.magnitude;
                        var aim = NearestEnemy(p.Pos, 800f * Px);
                        if (aim.HasValue) p.Vel = (aim.Value - p.Pos).normalized * speed;
                        break;
                    }
                    // Meteor shot: a bounce costs one of its two pierces.
                    if (p.Kind == ProjKind.MeteorShot && p.Penetrate > 1)
                    {
                        Sound("Item_10");
                        p.Penetrate--;
                        if (hitX) p.Vel.x = -oldVel.x;
                        if (hitY) p.Vel.y = -oldVel.y;
                        break;
                    }
                    p.HitTile = true;
                    p.Pos += p.Vel * Px;
                    Kill(p);
                    break;
            }
        }

        void HitEnemies(Proj p)
        {
            var s = p.Spec;
            // SetDefaults scales the hitbox once and truncates it (the laser keeps its 2×2 as it grows).
            var size = (p.HitSize != Vector2.zero ? p.HitSize : new Vector2((int)(s.W * s.Scale), (int)(s.H * s.Scale))) * Px;
            // BiggerHitbox (the Diamond's, P:15458-15474): it hits 30 px further all round.
            if ((p.Gem & GemFx.Bigger) != 0) size += Vector2.one * 60f * Px;
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            if (s.Beam != null)
            {
                // A beam: a box turned along it, as long as it and as wide (AABB against a thick line).
                var (from, to, width) = s.Beam(p);
                Vector2 a = TerraParticles.ToWorld(from), b = TerraParticles.ToWorld(to), d = b - a;
                if (width <= 0f || d.sqrMagnitude < 1e-6f) return;
                Physics2D.OverlapBox((a + b) * 0.5f, new Vector2(d.magnitude, width * Px), Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, filter, overlaps);
            }
            else if (s.HitArea != null)
            {
                var (areaCentre, areaSize) = s.HitArea(p);
                Physics2D.OverlapBox(areaCentre, areaSize, 0f, filter, overlaps);
            }
            else Physics2D.OverlapBox(p.Pos, size, 0f, filter, overlaps);
            // The Dark Lance and Gungnir reach on past their tips while thrusting out (Colliding, P:15902).
            if (s.SpearReach > 0f && LanceReach(p, out var ext, out float extSize, out _))
            {
                var box = Vector2.one * extSize * Px;
                AddOverlaps(ext, box, filter);
                float m = Mathf.Max(extSize, 12f) * Px, len = Vector2.Distance(p.Pos, ext);
                for (float t = m; t < len; t += m) AddOverlaps(Vector2.Lerp(p.Pos, ext, t / len), box, filter);
            }
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            if (s.CanDamage != null && !s.CanDamage(p)) return;
            var owner = HeroController.SilentInstance;
            Vector2 ownerCentre = owner != null ? (Vector2)owner.GetComponent<BoxCollider2D>().bounds.center : p.Pos;
            var hitNow = new HashSet<int>();
            // A copy: a shot that dies on the hit can go off (Blast, EnemiesIn), which fills the same list anew.
            foreach (var col in overlaps.ToArray())
            {
                if (p.Dead) return;
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null && p.Damage > 0) PropHits.Pulse(col, p.Pos - p.Vel.normalized * 0.3f, p.Vel.x >= 0f ? 1f : -1f, p.Damage);
                if (hm == null || hm.isDead) continue;
                int id = hm.GetInstanceID();
                if (!hitNow.Add(id)) continue;
                if ((s.OwnerHitCheck || p.OwnerCheck) && Physics2D.Linecast(ownerCentre, col.bounds.center, 1 << ChunkColliders.TerrainLayer)) continue;
                if (s.HitTest != null && !s.HitTest(p, col)) continue;
                if (s.Ai == Ai.Thrown)
                {
                    // Only the grenade goes off on contact; bombs and dynamite carry no damage in flight.
                    if (IsGrenade(p.Kind) && p.TimeLeft > 3) p.TimeLeft = 3;
                    continue;
                }
                // Immunity (Projectile.Damage): the shot's own cooldown, or the enemy's 10 ticks after
                // any such hit. A shot that hits once and dies ignores both and leaves none behind.
                int localCooldown = p.Cooldown > 0 ? p.Cooldown : s.LocalCooldown;
                bool local = localCooldown > 0 || s.OncePerNpc, typed = s.TypeCooldown > 0;
                bool single = s.Penetrate == 1 && !local && !typed && s.OwnerImmune == 0;
                var typeKey = (s.TypeGroup != ProjKind.None ? s.TypeGroup : p.Kind, id);
                if (p.Immune.ContainsKey(id)) continue;
                if (typed && typeImmune.ContainsKey(typeKey)) continue;
                if (!single && !local && !typed && npcImmune.ContainsKey(id)) continue;
                float facing = p.Vel.x >= 0f ? 1f : -1f;
                // The water stream knocks away from the player (P:13680).
                if (p.Kind == ProjKind.WaterStream) facing = col.bounds.center.x >= ownerCentre.x ? 1f : -1f;
                // A blast pushes away from its middle (Projectile.Damage for aiStyle 50).
                if (s.PushFromCentre) facing = col.bounds.center.x >= p.Pos.x ? 1f : -1f;
                // Yoyos and flails push away from the player (P:13304-13311, 13664-13679).
                if (s.PushFromPlayer) facing = col.bounds.center.x >= ownerCentre.x ? 1f : -1f;
                // StatusNPC runs before the damage: the debuffs of the shot.
                if (p.Kind == ProjKind.DarkLance) EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, Random.Range(240, 300));
                if (p.Kind == ProjKind.Flamelash && Random.Range(0, 2) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, Random.Range(240, 480));
                if (p.Melee) EnemyDebuffs.Magma(hm, true);
                if (s.Debuffs != null)
                    foreach (var (buff, ticks, chance) in s.Debuffs)
                        if (Random.Range(0, chance) == 0) EnemyDebuffs.Add(hm, buff, ticks);
                if (p.Kind == ProjKind.NanoBullet) EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, Random.Range(0, 3) == 0 ? 180 : 60);
                // The Hornet's stinger poisons, the Imp's fireball burns, always (P:11505-11512).
                if (p.Kind == ProjKind.HornetStinger) EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 60 * Random.Range(4, 7));
                if (p.Kind == ProjKind.ImpFireball) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 60 * Random.Range(3, 7));
                int dealt = p.Damage;
                float critChance = p.Crit;
                bool tagged = false;
                if (p.MinionShot)
                {
                    // A minion's shot takes the whip tags (and only a tag can make it crit).
                    dealt = SummonTags.Modify(hm, p.Damage, out bool tagCrit, 1f, sentryShots.Contains(p.Kind), TerraParticles.ToPx(p.Pos));
                    tagged = dealt != p.Damage || tagCrit;
                    critChance = tagCrit ? 100f : 0f;
                }
                if (s.ModifyHit != null) (dealt, critChance) = s.ModifyHit(this, p, hm, dealt, critChance);
                int snap = AccBeforeHit(p, hm, ref critChance, tagged);
                ArmorEffects.Context = new ArmorEffects.Hit
                {
                    Class = ClassOf(p), FromProjectile = true, ProjDamage = p.Damage, ProjHits = p.Hits, Suppress = s.NoSetOnHit, NoGhost = s.NoGhost || s.NoSetOnHit,
                };
                // A minion's or sentry's shot knocks nothing back.
                float kb = p.MinionShot ? 0f : p.Knockback * (s.KnockbackScale != null ? s.KnockbackScale(p) : 1f);
                // P:12855-12860: the Hive Pack's wasps go through 5 more defence.
                int pen = Equipment.Current.ArmorPen + s.ArmorPen + p.GemArmorPen + (p.Kind == ProjKind.Wasp && Equipment.Current.StrongBees ? 5 : 0);
                melee.HitWith(col, dealt, kb, critChance, facing, p.Pos - p.Vel.normalized * 0.3f, pen, snap);
                p.Hits++;
                AccAfterHit(p, hm, dealt);
                DevHits[p.Kind] = DevHit(p.Kind) + 1;
                if (p.MinionShot)
                {
                    SummonTags.OnHit(hm, p.Damage, TerraParticles.ToPx(p.Pos), p.Knockback);
                    Minions.DevHits++;
                    Minions.DevHitDamage += dealt;
                    Minions.DevMaxHit = Mathf.Max(Minions.DevMaxHit, dealt);
                }
                p.LastHit = id;
                if (s.OncePerNpc) p.Immune[id] = int.MaxValue;
                else if (localCooldown > 0) p.Immune[id] = localCooldown;
                else if (typed) typeImmune[typeKey] = s.TypeCooldown;
                else if (!single && !s.NoImmunity)
                {
                    int ticks = s.OwnerImmune > 0 ? s.OwnerImmune : 10;
                    npcImmune[id] = ticks;
                    p.Immune[id] = ticks;
                }
                // Piercing shots lose some of their damage with each hit (Projectile.cs:13451-13575, 14242).
                if (s.Falloff < 1f) p.Damage = (int)(p.Damage * s.Falloff);
                if (s.DieOnHit)
                {
                    Kill(p);
                    return;
                }

                OnHitEffects(p, hm);
                s.OnHit?.Invoke(this, p, hm);
                // A boomerang turns back from what it hits (not Paladin's Hammer); the Light Disc throws sparks.
                if (s.Ai == Ai.Boomerang && p.Kind != ProjKind.PaladinsHammer && !s.NoHitTurn)
                {
                    if (!p.Returning)
                    {
                        if (p.Kind == ProjKind.LightDisc)
                        {
                            var n = Flip(p.Vel).normalized;
                            LightDiscBounce(p, TerraParticles.ToPx(p.Pos) + n * 8f, -n);
                        }
                        p.Vel = -p.Vel;
                    }
                    p.Returning = true;
                }
                if (p.Kind == ProjKind.Spark || p.Kind == ProjKind.FrostSpark) p.Ai0 += (60f - p.Ai0) / 2f;
                if (p.Penetrate > 0 && --p.Penetrate == 0) Kill(p);
                if (s.OneHitPerUpdate) return;
            }
        }

        static void OnHitEffects(Proj p, HealthManager hm)
        {
            switch (p.Kind)
            {
                case ProjKind.FlamingArrow:
                    if (Random.Range(0, 3) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
                    break;
                case ProjKind.Spark:
                    if (Random.value < 0.5f) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, Random.Range(0, 3) == 0 ? Random.Range(60, 241) : Random.Range(60, 121));
                    break;
                case ProjKind.BallofFire:
                    if (Random.value < 0.5f) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 300);
                    break;
                case ProjKind.Flamelash:
                    // Let go, it looks for someone else after its first hit (P:14284).
                    if (p.Free) p.Target = null;
                    break;
            }
        }

        void AddOverlaps(Vector2 centre, Vector2 size, ContactFilter2D filter)
        {
            extraOverlaps.Clear();
            Physics2D.OverlapBox(centre, size, 0f, filter, extraOverlaps);
            foreach (var c in extraOverlaps) if (!overlaps.Contains(c)) overlaps.Add(c);
        }

        readonly List<Collider2D> extraOverlaps = new List<Collider2D>();

        void Kill(Proj p)
        {
            if (p.Dead) return;
            p.Dead = true;
            var s = p.Spec;
            if (s.OnKill != null)
            {
                s.OnKill(this, p);
                return;
            }
            if (KillAmmo(p)) return;
            switch (p.Kind)
            {
                case ProjKind.Bomb:
                case ProjKind.Dynamite:
                case ProjKind.Grenade:
                case ProjKind.StickyGrenadeProj:
                case ProjKind.BouncyGrenadeProj:
                case ProjKind.PartyGrenadeProj:
                    Explode(p);
                    return;
                case ProjKind.WoodenArrow:
                case ProjKind.FlamingArrow:
                case ProjKind.UnholyArrow:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 10; i++)
                        DustBox(p, s.Dust, Vector2.zero, p.Kind == ProjKind.WoodenArrow ? 0 : p.Kind == ProjKind.FlamingArrow ? 100 : 150, p.Kind == ProjKind.UnholyArrow ? 1.1f : 1f, false);
                    return;
                case ProjKind.JesterArrow:
                    Sound("Item_10");
                    for (int i = 0; i < 60; i++) DustBox(p, new[] { 15, 57, 58 }[Random.Range(0, 3)], p.Vel * 0.5f, 150, 1.5f, false);
                    return;
                case ProjKind.Bullet:
                case ProjKind.MeteorShot:
                case ProjKind.Laser:
                    Sound("Item_10");
                    if (p.HitTile) for (int i = 0; i < 5; i++) DustBox(p, 1, Vector2.zero, 0, 1f, false);
                    return;
                case ProjKind.AmethystBolt:
                case ProjKind.RubyBolt:
                case ProjKind.DiamondBolt:
                case ProjKind.TopazBolt:
                case ProjKind.SapphireBolt:
                case ProjKind.EmeraldBolt:
                case ProjKind.AmberBolt:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 15; i++)
                    {
                        int d = DustAt(p.Pos, s.Dust, 10, Vector2.zero, 50, 1.2f * 1.25f, true);
                        if (d < 0) continue;
                        ref var dust = ref TerraParticles.Instance[d];
                        var v = Flip(p.Vel).normalized;
                        dust.Velocity = (v * 2.2f + Random.insideUnitCircle * 1.5f) * Random.value;
                    }
                    if ((p.Gem & GemFx.Aoe) != 0)
                    {
                        // AoeExplosion (P:77012-77042): more gem dust flung out, and a 160 px burst at 35% damage and knockback.
                        for (int i = 0; i < 30; i++)
                        {
                            int d = DustAt(p.Pos, s.Dust, 10, Vector2.zero, 50, 1.2f, Random.Range(0, 5) != 0);
                            if (d < 0) continue;
                            ref var dust = ref TerraParticles.Instance[d];
                            dust.Scale *= 1f + Random.value;
                            dust.Velocity *= 4.2f * Random.value;
                            if (!dust.NoGravity) dust.Scale *= 0.6f;
                        }
                        Blast(p, 160f, (int)(p.Damage * 0.35f), p.Knockback * 0.35f);
                    }
                    return;
                case ProjKind.WaterBolt:
                    Sound("Item_10");
                    for (int i = 0; i < 30; i++) DustBox(p, 172, Flip(p.Vel) * 0.1f, 100, 1f, true);
                    for (int i = 0; i < 30; i++) DustBox(p, 172, Flip(p.Vel) * 0.1f, 100, 0.5f, false);
                    return;
                case ProjKind.BallofFire:
                    Sound("Item_10");
                    for (int i = 0; i < 20; i++) Scaled(DustBox(p, 6, -Flip(p.Vel) * 0.2f, 100, 2f, true), 2f);
                    for (int i = 0; i < 20; i++) Scaled(DustBox(p, 6, -Flip(p.Vel) * 0.2f, 100, 1f, false), 2f);
                    return;
                case ProjKind.DemonScythe:
                    Sound("Item_10");
                    for (int i = 0; i < 30; i++) DustBox(p, 27, Flip(p.Vel), 100, 1.7f, true);
                    for (int i = 0; i < 30; i++) DustBox(p, 27, Flip(p.Vel), 100, 1f, true);
                    return;
                case ProjKind.HornetStinger:
                case ProjKind.ImpFireball:
                    return;
                case ProjKind.MiniRetinaLaser:
                    // Any death: dust where it touches blocks, and Item10.
                    HitTiles(p);
                    Sound("Item_10");
                    return;
                case ProjKind.Spark:
                case ProjKind.Boomerang:
                case ProjKind.WoodenBoomerang:
                case ProjKind.Flamarang:
                case ProjKind.ThornChakram:
                case ProjKind.DarkLance:
                case ProjKind.TridentSpear:
                case ProjKind.VilethornBase:
                case ProjKind.VilethornTip:
                case ProjKind.WaterStream:
                    return;
                case ProjKind.StarfuryStar:
                case ProjKind.StarCloakStar:
                    Sound("Item_10");
                    for (int i = 0; i < 10; i++) DustBox(p, 58, Flip(p.Vel) * 0.1f, 150, 1.2f, false);
                    for (int i = 0; i < 3; i++) Gore(p, Random.Range(16, 18), 0.05f, 0f);
                    return;
                case ProjKind.EnchantedBeam:
                    // A streak of sparkles back along its path.
                    Sound("Item_10");
                    for (int i = 4; i < 24; i++)
                    {
                        int d = DustAt(p.Pos - p.Vel * (30f / i) * Px + new Vector2(-4f, 4f) * Px, new[] { 15, 57, 58 }[Random.Range(0, 3)], 8, Flip(p.Vel) * 0.2f, 100, 1.8f, true);
                        if (d >= 0) TerraParticles.Instance[d].Velocity *= 1.5f;
                    }
                    return;
                case ProjKind.IceBolt:
                    Sound("Item_27");
                    for (int i = 0; i < 10; i++)
                    {
                        int d = DustBox(p, 92, Vector2.zero, 0, 1f, false);
                        if (d < 0) continue;
                        ref var dust = ref TerraParticles.Instance[d];
                        if (Random.Range(0, 3) != 0)
                        {
                            dust.Velocity *= 2f;
                            dust.NoGravity = true;
                            dust.Scale *= 1.75f;
                        }
                        else dust.Scale *= 0.5f;
                    }
                    return;
                case ProjKind.Bee:
                    for (int i = 0; i < 6; i++)
                    {
                        int d = DustBox(p, 150, Flip(p.Vel), 50, 1f, true);
                        if (d >= 0) TerraParticles.Instance[d].Scale = 1f;
                    }
                    return;
                case ProjKind.Shuriken:
                case ProjKind.ThrowingKnife:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 10; i++) DustBox(p, 1, Flip(p.Vel) * 0.1f, 0, 0.75f, false);
                    return;
                case ProjKind.MagicMissile:
                case ProjKind.Flamelash:
                    KillMissile(p);
                    return;
                default:
                    for (int i = 0; i < 4; i++) DustBox(p, s.Dust >= 0 ? s.Dust : 1, Vector2.zero, 100, 1f, true);
                    return;
            }
        }

        static void Scaled(int d, float velocity)
        {
            if (d >= 0) TerraParticles.Instance[d].Velocity *= velocity;
        }

        // --- Explosives. ---

        void StartFuse(Proj p)
        {
            try
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.clip = Terraria.TerrariaAssets.Sound("Custom/fuse");
                a.loop = true;
                a.volume = 0.2f;
                a.spatialBlend = 0f;
                var mix = TerraMod.Instance != null ? TerraMod.Instance.SoundGroup : null;
                if (mix != null) a.outputAudioMixerGroup = mix;
                a.Play();
                p.Fuse = a;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("fuse sound: " + e.Message);
            }
        }

        void Explode(Proj p)
        {
            var s = p.Spec;
            if (p.Fuse != null)
            {
                Destroy(p.Fuse);
                p.Fuse = null;
            }
            Sound("Item_14");
            var particles = TerraParticles.Instance;
            float blast = s.BlastPx * Px;
            bool dynamite = p.Kind == ProjKind.Dynamite;
            if (particles != null && p.Kind == ProjKind.PartyGrenadeProj)
            {
                // P:80191-80230: the Happy Grenade bursts into confetti (219-223, 139-142) and party gores (276-282).
                var corner = TerraParticles.ToPx(p.Pos) - new Vector2(11f, 11f);
                for (int i = 0; i < 8; i++)
                {
                    int d = particles.NewDust(corner, 22, 22, 219 + Random.Range(0, 5));
                    if (d < 0) continue;
                    particles[d].Velocity *= 1.4f;
                    particles[d].FadeIn = 1f;
                    particles[d].NoGravity = true;
                }
                for (int i = 0; i < 15; i++)
                {
                    int d = particles.NewDust(corner, 22, 22, 139 + Random.Range(0, 4), 0f, 0f, 0, 1.6f);
                    if (d >= 0)
                    {
                        particles[d].NoGravity = true;
                        particles[d].Velocity *= 5f;
                    }
                    d = particles.NewDust(corner, 22, 22, 139 + Random.Range(0, 4), 0f, 0f, 0, 1.9f);
                    if (d >= 0) particles[d].Velocity *= 3f;
                }
                // Each gore's own toss slowed to 0.4, then pushed 1 px out along a diagonal (px, y down).
                foreach (var diag in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
                    if (Random.Range(0, 2) == 0) particles.NewGore(corner, Vector2.zero, Random.Range(276, 283), 1f, 0.4f, diag);
            }
            else if (particles != null)
            {
                // Projectile.Kill: the dust comes from a small box round the centre (200 px for dynamite).
                float box = dynamite ? 200f : 22f;
                var corner = TerraParticles.ToPx(p.Pos) - new Vector2(box, box) * 0.5f;
                int smoke = dynamite ? 50 : 20, fire = dynamite ? 80 : 10;
                for (int i = 0; i < smoke; i++)
                {
                    int d = particles.NewDust(corner, (int)box, (int)box, 31, 0f, 0f, 100, dynamite ? 2f : 1.5f);
                    if (d >= 0) particles[d].Velocity *= 1.4f;
                }
                for (int i = 0; i < fire; i++)
                {
                    int d = particles.NewDust(corner, (int)box, (int)box, 6, 0f, 0f, 100, dynamite ? 3f : 2.5f);
                    if (d >= 0)
                    {
                        particles[d].NoGravity = true;
                        particles[d].Velocity *= 5f;
                    }
                    d = particles.NewDust(corner, (int)box, (int)box, 6, 0f, 0f, 100, dynamite ? 2f : 1.5f);
                    if (d >= 0) particles[d].Velocity *= 3f;
                }
                var gorePos = dynamite ? TerraParticles.ToPx(p.Pos) - new Vector2(24f, 24f) : corner;
                int rounds = dynamite ? 2 : 1;
                for (int r = 0; r < rounds; r++)
                    foreach (var diag in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
                        particles.NewGore(gorePos, diag * (dynamite ? 1.5f : 1f), Random.Range(61, 64), dynamite ? 1.5f : 1f, 0.4f);
            }
            var light = World.Glow.Create(transform, new Color(1f, 0.75f, 0.4f, 1f), blast * 0.9f);
            light.transform.position = new Vector3(p.Pos.x, p.Pos.y, World.Glow.Z);
            light.gameObject.AddComponent<FadeOut>().Begin(0.4f);
            World.LightMap.Attach(light, new Color(1f, 0.75f, 0.4f), followSprite: true);

            var melee = TerraMod.Instance?.Melee;
            int hitEnemies = 0;
            // Enemies in the blast box.
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapBox(p.Pos, Vector2.one * blast, 0f, filter, overlaps);
            var done = new HashSet<int>();
            int damage = s.BlastDamage > 0 ? s.BlastDamage : p.Damage;
            if (melee != null)
                foreach (var col in overlaps)
                {
                    var hm = col.GetComponentInParent<HealthManager>();
                    if (hm == null || hm.isDead || !done.Add(hm.GetInstanceID())) continue;
                    float facing = col.bounds.center.x >= p.Pos.x ? 1f : -1f;
                    if (melee.HitWith(col, damage, p.MinionShot ? 0f : s.BlastKnockback, 4f, facing, p.Pos)) hitEnemies++;
                }

            // The player, if caught in it (Terraria's explosives hurt their owner).
            var hero = HeroController.SilentInstance;
            bool hurtSelf = false;
            if (hero != null && !hero.cState.dead && !hero.cState.invulnerable)
            {
                var hb = hero.GetComponent<BoxCollider2D>().bounds;
                if (new Rect(p.Pos - Vector2.one * blast * 0.5f, Vector2.one * blast).Overlaps(new Rect(hb.min, hb.size)))
                {
                    hurtSelf = true;
                    Player.PlayerHooks.HurtPlayer(hero, damage, hb.center.x >= p.Pos.x ? 1 : -1, gameObject);
                }
            }

            int cells = 0, floors = 0;
            if (s.TileRadius > 0)
            {
                cells = BlastTiles(p.Pos, s.TileRadius);
                floors = BreakSpecialFloors(p.Pos, s.TileRadius * CellGrid.CellSize);
                // Walls, pots, levers and other props that react to the nail.
                blastCenter = p.Pos;
                blastSize = Vector2.one * blast;
                blastFacing = hero != null && hero.transform.position.x > p.Pos.x ? -1f : 1f;
                blastPulses = 3;
                PulseBlastProps();
            }
            DevLastBlast = $"{p.Kind} @ {p.Pos:F2}：炸掉 {cells} 格，敌人 {hitEnemies}，下砸地板 {floors}，炸到自己={hurtSelf}";
            Plugin.Log.LogInfo("[爆炸] " + DevLastBlast);
        }

        static bool IsGrenade(ProjKind k) => k == ProjKind.Grenade || k == ProjKind.StickyGrenadeProj || k == ProjKind.BouncyGrenadeProj || k == ProjKind.PartyGrenadeProj;

        /// <summary>Projectile.ExplodeTiles: every diggable cell within the radius (in tiles) goes, as if mined.</summary>
        static int BlastTiles(Vector2 center, int radius)
        {
            const float c = CellGrid.CellSize;
            var cc = new Vector2(center.x / c, center.y / c);
            int n = 0;
            var mod = TerraMod.Instance;
            for (int x = Mathf.FloorToInt(cc.x - radius); x <= Mathf.CeilToInt(cc.x + radius); x++)
                for (int y = Mathf.FloorToInt(cc.y - radius); y <= Mathf.CeilToInt(cc.y + radius); y++)
                {
                    float dx = x + 0.5f - cc.x, dy = y + 0.5f - cc.y;
                    if (dx * dx + dy * dy >= radius * radius) continue;
                    if (!TerraWorld.CanDig(x, y)) continue;
                    var kind = TerraWorld.Get(x, y);
                    TerraWorld.Set(x, y, CellKind.Empty);
                    n++;
                    var drop = ItemDb.DropFor(kind);
                    if (drop != ItemType.None && mod != null && !mod.Inventory.Infinite) WorldItems.Drop(ItemStack.Of(drop), CellGrid.CellCenter(x, y));
                }
            return n;
        }

        /// <summary>The floors broken by a dive or by striking them, in a blast (World.DiveFloors).</summary>
        static int BreakSpecialFloors(Vector2 center, float radius) => World.DiveFloors.BreakAround(center, radius);

        /// <summary>The blast counts as a few quick strikes for props that count nail hits.</summary>
        void PulseBlastProps()
        {
            if (blastPulses % 2 == 1)
            {
                blastProps.Begin(blastFacing, 50);
                blastProps.Tick(blastCenter, blastSize);
            }
            else blastProps.End();
            blastPulses--;
            if (blastPulses <= 0) blastProps.End();
        }

        // --- Drawing (Main.DrawProj). ---

        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        static Sprite Sprite(string texture)
        {
            if (spriteCache.TryGetValue(texture, out var s)) return s;
            s = FxBatch.Whole(texture);
            spriteCache[texture] = s;
            return s;
        }

        void Draw()
        {
            var fx = FxBatch.Instance;
            if (fx == null) return;
            fx.OnTop = true;
            try
            {
                FxParticles.Draw(fx);
                foreach (var p in live) DrawOne(fx, p);
                DrawGhosts(fx);
            }
            finally
            {
                fx.OnTop = false;
            }
        }

        /// <summary>One projectile as Main.DrawProj draws it (the player's own, or another player's ghost).</summary>
        void DrawOne(FxBatch fx, Proj p)
        {
            // Main.DoUpdate updates projectiles after the players, so a shot is moved (all its updates
            // for the tick) before it is first drawn: never seen at the player's middle it starts from.
            if (p.Dead || p.Ticks == 0) return;
            if (p.ShownAt == null && p.Delay <= 0) p.ShownAt = p.Pos;
            var s = p.Spec;
            var px = TerraParticles.ToPx(p.Pos);
            var light = p.Kind == ProjKind.CrystalShard ? s.Light * (p.Scale * 0.5f) : s.Light;
            if (light.r + light.g + light.b > 0f && p.Delay <= 0) fx.Light(px, light, s.Ai == Ai.Thrown ? 3f : 2.5f);
            // A projectile drawn its own way draws its own trail.
            if (s.TrailLength > 0 && p.Delay <= 0 && s.Ai != Ai.Missile && s.Draw == null) DrawTrail(fx, p);
            if (s.Ai == Ai.GemBolt)
            {
                DrawGem(fx, p, px);
                return;
            }
            if (s.Draw != null)
            {
                if (p.Delay <= 0) s.Draw(this, fx, p, px);
                return;
            }
            switch (s.Ai)
            {
                case Ai.Starfury: DrawStarfury(fx, p, px); return;
                case Ai.Beam: DrawBeam(fx, p, px); return;
                case Ai.Spear: DrawSpear(fx, p, px); return;
                case Ai.Missile: DrawMissile(fx, p, px); return;
                case Ai.CloakStar: DrawCloakStar(fx, p, px); return;
            }
            if (s.Look == Look.Invisible || string.IsNullOrEmpty(s.Texture)) return;
            float o = 1f - p.Alpha;
            if (s.Look == Look.AdditiveFade && p.Alpha >= 200f / 255f) return;
            if (p.Kind == ProjKind.HighVelocityBullet && p.Alpha >= 140f / 255f) return;
            Color c = s.Look switch
            {
                Look.Bright => Color.white * o,
                Look.Additive => new Color(1f, 1f, 1f, 0f) * o,
                Look.NearlyAdded => FxBatch.C(200, 200, 200, 25) * o,
                Look.AdditiveFade => new Color(1f, 1f, 1f, 0f) * o,
                Look.WhiteA100 => FxBatch.C(255, 255, 255, 100) * (p.Kind == ProjKind.LuminiteBullet ? o : 1f),
                Look.HolyAdd => FxBatch.C(200, 200, 200, 0),
                Look.ShardAdd => FxBatch.C(205, 205, 205, 0),
                _ => new Color(L, L, L, 1f) * o,
            };
            // A Phoenix-quivered or Seraph-necklaced arrow is drawn white (P:80606-80612), the Phoenix's with its flames behind.
            if ((p.Harpy & 12) != 0)
            {
                c = Color.white * o;
                if ((p.Harpy & 4) != 0) DrawPhoenixFlames(fx, p, px);
            }
            if (s.Origin.x >= 0f)
            {
                // Main.DrawProj's generic rule: the texture's own origin, a little off the hitbox centre.
                var whole = Sprite(s.Texture).rect;
                int fh = (int)whole.height / s.Frames;
                var framed = FxBatch.Frame(s.Texture, 0, p.Frame * fh, (int)whole.width, fh, s.Origin.x, s.Origin.y);
                fx.Draw(framed, px + s.DrawOffset, c, -p.Rot * Mathf.Deg2Rad, p.Scale, flipX: s.Ai == Ai.Bee && p.Vel.x < 0f);
                return;
            }
            // The pivot sits near the head of the sprite: its centre trails behind along the flight.
            var rot = p.Rot * Mathf.Deg2Rad;
            var back = new Vector2(Mathf.Sin(rot), Mathf.Cos(rot)) * s.PivotBack * p.Scale;
            fx.Draw(Sprite(s.Texture), px + back, c, -rot, p.Scale);
        }

        /// <summary>The 1.4.5 gem bolts: a fading trail behind four added glows that breathe once a second.</summary>
        static void DrawGem(FxBatch fx, Proj p, Vector2 px)
        {
            Color c1, c2;
            switch (p.Kind)
            {
                case ProjKind.AmethystBolt: c1 = new Color(0.93f, 0.51f, 0.93f); c2 = new Color(0.5f, 0f, 0.5f); break;
                case ProjKind.RubyBolt: c1 = c2 = Color.red; break;
                // M:28665-28703: Topaz orange, Sapphire 60 90 255, Emerald lime to green, Amber orange-red to dark orange.
                case ProjKind.TopazBolt: c1 = c2 = new Color(1f, 165f / 255f, 0f); break;
                case ProjKind.SapphireBolt: c1 = c2 = new Color(60f / 255f, 90f / 255f, 1f); break;
                case ProjKind.EmeraldBolt: c1 = new Color(50f / 255f, 205f / 255f, 50f / 255f); c2 = new Color(0f, 0.5f, 0f); break;
                case ProjKind.AmberBolt: c1 = new Color(1f, 69f / 255f, 0f); c2 = new Color(1f, 140f / 255f, 0f); break;
                case ProjKind.ForbiddenGem: c1 = c2 = new Color(1f, 165f / 255f, 0f); break;
                default: c1 = c2 = new Color(0.75f, 0.75f, 0.75f); break;
            }
            var dot = FxBatch.Whole("Extra_98");
            for (int i = 1; i < p.Trail.Count; i++)
            {
                float t = i / 30f;
                var col = Color.Lerp(Color.white, Color.Lerp(c1, c2, t), Mathf.Clamp01(Mathf.InverseLerp(-0.2f, 0.5f, t))) * (1f - Mathf.Clamp01(t / 0.98f));
                col.a = 0f;
                var a = TerraParticles.ToPx(p.Trail[i - 1]);
                var b = TerraParticles.ToPx(p.Trail[i]);
                var d = b - a;
                if (d.sqrMagnitude < 0.01f) continue;
                fx.Draw(dot, (a + b) / 2f, col * 0.8f, Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f, new Vector2(16f / 72f * 3f, Mathf.Max(d.magnitude, 1f) / 72f * 1.6f));
            }
            var glow = Sprite(p.Spec.Texture);
            float breathe = 1f + 0.2f * Mathf.Cos(2f * Mathf.PI * Time.time);
            float rot = -p.Rot * Mathf.Deg2Rad + Mathf.PI / 2f;
            for (int k = 0; k < 4; k++) fx.Draw(glow, px, FxBatch.C(80, 80, 80, 0), rot, new Vector2(1.25f, 1f) * 0.6f * breathe);
            if ((p.Gem & GemFx.Bigger) != 0)
            {
                float b2 = 1f + 0.2f * Mathf.Cos(8f * Mathf.PI * Time.time);
                fx.Draw(glow, px, FxBatch.C(80, 80, 80, 0) * 0.5f, rot + Mathf.PI / 4f, new Vector2(1f, 2f) * b2);
                fx.Draw(glow, px, FxBatch.C(80, 80, 80, 0) * 0.5f, rot + Mathf.PI / 4f, new Vector2(2f, 1f) * b2);
            }
        }

        // --- Arrows and bullets (spec-ammo B.3). ---

        /// <summary>The per-update dust and steering of the arrows and bullets that have any.</summary>
        void StepAmmo(Proj p, bool firstOfTick)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            int d;
            switch (p.Kind)
            {
                case ProjKind.HellfireArrow:
                    DustBox(p, 31, Vector2.zero, 100, 1.6f, true);
                    DustBox(p, 6, Vector2.zero, 100, 2f, true);
                    break;
                case ProjKind.HolyArrow:
                    if (Random.value < 0.5f && (d = DustBox(p, Random.value < 0.5f ? 58 : 15, Flip(p.Vel) * 0.25f, 150, 0.9f, false)) >= 0) pp[d].Velocity *= 0.25f;
                    break;
                case ProjKind.CursedArrow:
                    if ((d = DustBox(p, 75, Vector2.zero, 100, 1f, false)) >= 0 && Random.value < 0.5f)
                    {
                        pp[d].NoGravity = true;
                        pp[d].Scale *= 2f;
                    }
                    break;
                case ProjKind.FrostburnArrow:
                    DustBox(p, 135, Vector2.zero, 100, 1f, false);
                    break;
                case ProjKind.ChlorophyteArrow:
                    if (Random.value < 0.5f && (d = DustBox(p, 40, Vector2.zero, 0, 1.3f, true)) >= 0) pp[d].Velocity *= 0.5f;
                    break;
                case ProjKind.IchorArrow:
                    if ((d = DustBox(p, 169, Vector2.zero, 100, 1f, false)) >= 0 && Random.value < 0.5f)
                    {
                        pp[d].NoGravity = true;
                        pp[d].Scale *= 1.5f;
                    }
                    break;
                case ProjKind.VenomArrow:
                    if ((d = DustBox(p, 171, Vector2.zero, 100, Random.Range(1, 10) * 0.1f, true)) >= 0)
                    {
                        pp[d].FadeIn = 1.5f;
                        pp[d].Velocity = pp[d].Velocity * 0.25f + Flip(p.Vel) * 0.25f;
                    }
                    break;
                case ProjKind.ShimmerArrow:
                    if (Random.Range(0, 8) == 0) ShimmerDust(p.Pos + Random.insideUnitCircle * 4f * Px, Flip(p.Vel) * 1.25f);
                    break;
                case ProjKind.SilverBullet:
                    if (Random.Range(0, 6) == 0 && (d = DustBox(p, 279, Flip(p.Vel), 100, 1.2f, true)) >= 0) pp[d].Velocity *= 0.2f;
                    break;
                case ProjKind.IceBolt:
                    // AI_028: the bolt is only its icy dust, and a chime as it leaves.
                    if (p.Updates == 1) Sound("Item_28");
                    for (int i = 0; i < 2; i++)
                        if ((d = DustBox(p, 92, Flip(p.Vel), 50, 1.2f, true)) >= 0) pp[d].Velocity *= 0.3f;
                    break;
                case ProjKind.ChlorophyteBullet:
                    // A line of green behind it, then a Manhattan 300 px search and a 7:1 turn towards the quarry.
                    if (p.Alpha < 170f / 255f)
                        for (int i = 0; i < 10; i++)
                            if ((d = DustAt(p.Pos - p.Vel * Px / 10f * i, 75, 1, Vector2.zero, (int)(p.Alpha * 255f), 1f, true)) >= 0) pp[d].Velocity = Vector2.zero;
                    if (p.Target != null && (p.Target.isDead || !p.Target.gameObject.activeInHierarchy || Manhattan(p.Target, p.Pos) >= 1000f)) p.Target = null;
                    if (p.Target == null) p.Target = Quarry(p.Pos, 300f);
                    if (p.Target != null)
                    {
                        var to = (Centre(p.Target) - p.Pos).normalized;
                        p.Vel = (p.Vel * 7f + to * p.LaunchSpeed) / 8f;
                    }
                    break;
                case ProjKind.LuminiteBullet:
                    // A burst of motes the moment it is about to strike an enemy it hasn't yet.
                    var ahead = Physics2D.OverlapBox(p.Pos + p.Vel * Px, new Vector2(4f, 4f) * Px, 0f, 1 << 11);
                    var hm = ahead != null ? ahead.GetComponentInParent<HealthManager>() : null;
                    if (hm != null && !hm.isDead && !p.Immune.ContainsKey(hm.GetInstanceID()))
                        for (int i = Random.Range(15, 31); i > 0; i--)
                            if ((d = DustAt(p.Pos, 229, 1, Vector2.zero, 100, 0.8f, true)) >= 0)
                            {
                                pp[d].Velocity = pp[d].Velocity * 1.6f + new Vector2(0f, -1f) + Flip(p.Vel);
                            }
                    break;
            }
        }

        /// <summary>Shimmer's dust 306: a rainbow mote and a white one just inside it.</summary>
        static void ShimmerDust(Vector2 at, Vector2 velocityYDown)
        {
            var pp = TerraParticles.Instance;
            float scale = 1f + Random.value * 0.4f;
            int d = DustAt(at, 306, 1, Vector2.zero, 0, scale, true);
            if (d < 0) return;
            pp[d].Velocity = velocityYDown;
            pp[d].Color = Color.HSVToRGB(Random.value, 1f, 1f);
            pp[d].FadeIn = scale + 0.05f;
            int w = DustAt(at, 306, 1, Vector2.zero, 0, scale - 0.3f, true);
            if (w >= 0)
            {
                pp[w].Velocity = velocityYDown;
                pp[w].Color = Color.white;
            }
        }

        /// <summary>AI_005 for the Hallow star: straight down at its speed, spinning, through blocks until below the arrow's impact.</summary>
        void StepStar(Proj p)
        {
            if (!p.Collide && p.Pos.y < p.Ai1) p.Collide = true;
            if (p.SoundDelay-- <= 0)
            {
                Sound("Item_9");
                p.SoundDelay = 20 + Random.Range(0, 40);
            }
            float dir = p.Vel.x >= 0f ? 1f : -1f;
            p.Rot -= (Mathf.Abs(p.Vel.x) + Mathf.Abs(p.Vel.y)) * 0.01f * dir * Mathf.Rad2Deg;
            int d;
            if (Random.Range(0, 10) == 0 && (d = DustBox(p, 58, Vector2.zero, 150, 1.2f, false)) >= 0) TerraParticles.Instance[d].Velocity = Flip(p.Vel) * 0.5f;
            if (Random.Range(0, 20) == 0) TerraParticles.Instance?.NewGore(TerraParticles.ToPx(p.Pos), Flip(p.Vel) * 0.2f, Random.Range(16, 18));
        }

        /// <summary>The Luminite arrow's afterimage, flying the arrow's path again once its delay is over.</summary>
        void StepLuminiteTrail(Proj p, bool firstOfTick)
        {
            var pp = TerraParticles.Instance;
            int d;
            if (!p.Stopped)
            {
                if (p.Alpha > 0f) p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
                if (++p.Ai0 >= 15f)
                {
                    p.Ai0 = 15f;
                    p.Vel.y = Mathf.Max(p.Vel.y - 0.1f, -16f);
                }
                if (firstOfTick && pp != null && (d = DustAt(p.Pos, 229, 1, Vector2.zero, 0, 1.2f, true)) >= 0)
                {
                    var side = new Vector2(-p.Vel.y, p.Vel.x).normalized * (Random.value < 0.5f ? 1f : -1f);
                    pp[d].Velocity = Flip(side) * Random.Range(0.25f, 0.5f);
                }
                p.Rot = Angle(p.Vel);
                return;
            }
            // Stopped: gone once every old position has caught up with it.
            bool collapsed = p.Trail.Count >= p.Spec.TrailLength;
            foreach (var t in p.Trail) if ((t - p.Pos).sqrMagnitude > 1e-8f) collapsed = false;
            if (collapsed)
            {
                Kill(p);
                return;
            }
            if (pp != null && Random.Range(0, Mathf.Max(1, p.Extra)) == 0 && Random.Range(0, 6) == 0)
                for (int i = 0; i < 2; i++)
                    if ((d = DustAt(p.Pos, 229, 1, Vector2.zero, 0, 1.2f, true)) >= 0) pp[d].Velocity = Random.insideUnitCircle.normalized * Random.Range(1f, 1.8f);
        }

        /// <summary>The kill bursts and what the arrows and bullets leave behind. True when handled.</summary>
        bool KillAmmo(Proj p)
        {
            var pp = TerraParticles.Instance;
            int d;
            switch (p.Kind)
            {
                case ProjKind.HellfireArrow:
                    Sound("Item_14");
                    for (int i = 0; i < 10; i++) DustBox(p, 31, Vector2.zero, 100, 1.5f, false);
                    for (int i = 0; i < 5; i++)
                    {
                        if ((d = DustBox(p, 6, Vector2.zero, 100, 2.5f, true)) >= 0) pp[d].Velocity *= 3f;
                        if ((d = DustBox(p, 6, Vector2.zero, 100, 1.5f, false)) >= 0) pp[d].Velocity *= 2f;
                    }
                    for (int i = 0; i < 2; i++) Gore(p, Random.Range(61, 64), 0.4f, 1f);
                    // A 64 px blast round it hits everyone else once (the one it struck is immune).
                    Blast(p, 64f, p.Damage, p.Knockback);
                    return true;
                case ProjKind.HolyArrow:
                case ProjKind.HallowStar:
                    Sound("Item_10");
                    for (int i = 0; i < 10; i++) if ((d = DustBox(p, 58, Vector2.zero, 150, 1.2f, false)) >= 0) pp[d].Velocity *= 0.1f;
                    for (int i = 0; i < 3; i++) Gore(p, Random.Range(16, 18), 0.05f, 0f);
                    // A star falls on where it struck: a third of the arrow's damage, and that star calls one more.
                    if (p.Kind == ProjKind.HolyArrow) Star(p.Pos, p.Damage / 3, p.Knockback, p.Crit, 1f);
                    else if (p.Ai0 > 0f) Star(p.Pos, p.Damage, p.Knockback, p.Crit, 0f);
                    return true;
                case ProjKind.CursedArrow:
                case ProjKind.IchorArrow:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 20; i++)
                    {
                        if ((d = DustBox(p, p.Kind == ProjKind.CursedArrow ? 75 : 169, Vector2.zero, 100, 1f, false)) < 0 || Random.value >= 0.5f) continue;
                        pp[d].Scale *= p.Kind == ProjKind.CursedArrow ? 2.5f : 1.5f;
                        pp[d].NoGravity = true;
                        pp[d].Velocity *= 5f;
                    }
                    return true;
                case ProjKind.FrostburnArrow:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 20; i++) DustBox(p, 135, Vector2.zero, 100, 1f, false);
                    return true;
                case ProjKind.ChlorophyteArrow:
                    Sound("Grass");
                    for (int i = 0; i < 5; i++) DustBox(p, 40, Vector2.zero, 0, 1f, false);
                    return true;
                case ProjKind.VenomArrow:
                    Sound("Item_10");
                    for (int i = 0; i < 10; i++) VenomMote(p, 0.75f, Vector2.zero);
                    return true;
                case ProjKind.BoneArrow:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 20; i++) DustBox(p, 26, Vector2.zero, 0, 0.9f, false);
                    return true;
                case ProjKind.LuminiteArrow:
                    LuminiteBurst(p);
                    // Its afterimage sets off from where it was shot, as fast, once the arrow's time would have run out.
                    var trail = Spawn(ProjKind.LuminiteTrail, p.StartPos, p.StartVel, p.Damage, p.Knockback, p.Crit);
                    if (trail != null) trail.Delay = Mathf.Max(0, p.TimeLeft) + 1;
                    return true;
                case ProjKind.LuminiteTrail:
                    LuminiteBurst(p);
                    return true;
                case ProjKind.ShimmerArrow:
                    HitTiles(p);
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 6; i++) ShimmerDust(p.Pos, Random.insideUnitCircle.normalized * 0.4f * 3f);
                    return true;
                case ProjKind.SilverBullet:
                    HitTiles(p);
                    Sound("Item_10");
                    for (int i = 0; i < 4; i++) if ((d = DustAt(p.Pos, 279, 1, Vector2.zero, 100, 1.2f, true)) >= 0) pp[d].Velocity *= 0.5f;
                    return true;
                case ProjKind.CrystalBullet:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int i = 0; i < 5; i++)
                        if ((d = DustBox(p, 68, Vector2.zero, 0, 1f, true)) >= 0)
                        {
                            pp[d].Velocity *= 1.5f;
                            pp[d].Scale *= 0.9f;
                        }
                    // Two shards fly back out of it at half its damage.
                    for (int i = 0; i < 2; i++)
                    {
                        var v = new Vector2(-p.Vel.x * Random.Range(40, 70) * 0.01f + Random.Range(-20, 21) * 0.4f,
                                            -p.Vel.y * Random.Range(40, 70) * 0.01f + Random.Range(-20, 21) * 0.4f);
                        Spawn(ProjKind.CrystalShard, p.Pos + v * Px, v, (int)(p.Damage * 0.5f), 0f, p.Crit);
                    }
                    return true;
                case ProjKind.CursedBullet:
                case ProjKind.IchorBullet:
                case ProjKind.GoldenBullet:
                case ProjKind.ChlorophyteBullet:
                case ProjKind.HighVelocityBullet:
                case ProjKind.NanoBullet:
                    HitTiles(p);
                    Sound("Item_10");
                    return true;
                case ProjKind.VenomBullet:
                    Sound("Item_10");
                    for (int i = 0; i < 10; i++) VenomMote(p, 0.75f, Vector2.zero);
                    for (int i = 0; i < 10; i++) VenomMote(p, 0.75f, Flip(p.Vel) * 0.5f * Random.value);
                    for (int i = 0; i < 10; i++) VenomMote(p, 1.5f, Flip(p.Vel) * 1.5f * Random.value);
                    // Three 80 px boxes along its flight give the venom without any damage.
                    var ahead = p.Vel.sqrMagnitude > 0f ? p.Vel.normalized : Vector2.right;
                    for (int k = 0; k < 3; k++)
                        foreach (var hm in EnemiesIn(p.Pos + ahead * (60f * k) * Px, 80f))
                        {
                            EnemyDebuffs.Add(hm, EnemyDebuffs.Venom, 600);
                            EnemyDebuffs.Add(hm, EnemyDebuffs.PotentAcid, 120);
                        }
                    return true;
                case ProjKind.PartyBullet:
                    // Silent confetti.
                    for (int i = 0; i < 10; i++)
                        if ((d = DustBox(p, Random.Range(139, 143), -Flip(p.Vel) * 0.3f, 0, 1.2f, false)) >= 0)
                        {
                            pp[d].Velocity = Jitter(pp[d].Velocity);
                            pp[d].Scale *= 1f + Random.Range(-30, 31) * 0.01f;
                        }
                    for (int i = 0; i < 5; i++)
                        pp?.NewGore(TerraParticles.ToPx(p.Pos), Jitter(-Flip(p.Vel) * 0.3f), Random.Range(276, 283), 1f + Random.Range(-20, 21) * 0.01f);
                    return true;
                case ProjKind.ExplodingBullet:
                    Sound("Item_14");
                    for (int i = 0; i < 7; i++) DustBox(p, 31, Vector2.zero, 100, 1.5f, false);
                    for (int i = 0; i < 3; i++)
                    {
                        if ((d = DustBox(p, 6, Vector2.zero, 100, 2.5f, true)) >= 0) pp[d].Velocity *= 3f;
                        if ((d = DustBox(p, 6, Vector2.zero, 100, 1.5f, false)) >= 0) pp[d].Velocity *= 2f;
                    }
                    pp?.NewGore(TerraParticles.ToPx(p.Pos) - new Vector2(10f, 10f), Flip(p.Vel) * 0.3f + Random.insideUnitCircle * 0.5f, Random.Range(61, 64));
                    // An 80 px blast; those it catches can't be caught by another for 5 ticks.
                    foreach (var hm in Blast(p, 80f, p.Damage, p.Knockback)) typeImmune[(p.Kind, hm)] = 5;
                    // ResetStaticImmunityMatching: the one it struck may take the next exploding bullet at once.
                    typeImmune.Remove((p.Kind, p.LastHit));
                    return true;
                case ProjKind.LuminiteBullet:
                    HitTiles(p);
                    Sound("Item_10");
                    for (int i = Random.Range(2, 5); i > 0; i--)
                        if ((d = DustAt(p.Pos, 229, 8, Vector2.zero, 0, 1f, true)) >= 0) pp[d].Velocity = pp[d].Velocity * 1.6f + new Vector2(0f, -1f);
                    return true;
                case ProjKind.CrystalShard:
                    return true;
            }
            return false;
        }

        void HitTiles(Proj p)
        {
            if (p.HitTile) for (int i = 0; i < 5; i++) DustBox(p, 1, Vector2.zero, 0, 1f, false);
        }

        static void LuminiteBurst(Proj p)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int i = Random.Range(5, 10); i > 0; i--)
            {
                int d = DustAt(p.Pos, 220, 8, Vector2.zero, 100, 0.5f, true);
                if (d >= 0) pp[d].Velocity = pp[d].Velocity * 1.6f + new Vector2(0f, -1f);
            }
        }

        static void VenomMote(Proj p, float speed, Vector2 add)
        {
            var pp = TerraParticles.Instance;
            int d = DustBox(p, 171, Vector2.zero, 100, Random.Range(1, 10) * 0.1f, true);
            if (d < 0) return;
            pp[d].FadeIn = 1.5f;
            pp[d].Velocity = pp[d].Velocity * speed + add;
        }

        static Vector2 Jitter(Vector2 v)
        {
            v += new Vector2(Random.Range(-50, 51), Random.Range(-50, 51)) * 0.01f;
            v = new Vector2(v.x * (1f + Random.Range(-50, 51) * 0.01f), v.y * (1f + Random.Range(-50, 51) * 0.01f));
            return v + new Vector2(Random.Range(-50, 51), Random.Range(-50, 51)) * 0.05f;
        }

        static void Gore(Proj p, int type, float velocityScale, float random)
        {
            var v = Flip(p.Vel) * velocityScale + new Vector2(Random.Range(-10, 11), Random.Range(-10, 11)) * 0.1f * random;
            TerraParticles.Instance?.NewGore(TerraParticles.ToPx(p.Pos) - new Vector2(p.Spec.W, p.Spec.H) * 0.5f, v, type);
        }

        /// <summary>The Holy Arrow's star: from somewhere 600-900 px above (400 px either side), at 22 px an update straight at the spot.</summary>
        void Star(Vector2 at, int damage, float knockback, float crit, float chain)
        {
            var from = at + new Vector2(Random.Range(-400, 400), Random.Range(600, 900)) * Px;
            var star = Spawn(ProjKind.HallowStar, from, (at - from).normalized * 22f, damage, knockback, crit);
            if (star == null) return;
            star.Ai0 = chain;
            star.Ai1 = at.y;
        }

        /// <summary>A projectile's blast box: everyone in it bar the one it just struck (and the NPC-immune) takes one hit. Returns who.</summary>
        List<int> Blast(Proj p, float sizePx, int damage, float knockback)
        {
            var hit = new List<int>();
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return hit;
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapBox(p.Pos, Vector2.one * sizePx * Px, 0f, filter, overlaps);
            var done = new HashSet<int>();
            foreach (var col in overlaps)
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null && damage > 0) PropHits.Pulse(col, p.Pos, col.bounds.center.x >= p.Pos.x ? 1f : -1f, damage);
                if (hm == null || hm.isDead) continue;
                int id = hm.GetInstanceID();
                if (id == p.LastHit || npcImmune.ContainsKey(id) || p.Immune.ContainsKey(id) || typeImmune.ContainsKey((p.Kind, id)) || !done.Add(id)) continue;
                float facing = col.bounds.center.x >= p.Pos.x ? 1f : -1f;
                if (melee.HitWith(col, damage, p.MinionShot ? 0f : knockback, p.Crit, facing, p.Pos, Equipment.Current.ArmorPen + p.Spec.ArmorPen)) hit.Add(id);
            }
            return hit;
        }

        /// <summary>
        /// One hit straight on an enemy, where it is (the Medusa Head's gaze: Projectile.Damage on a
        /// ray put at the enemy's centre), leaving it the usual 10 ticks of immunity.
        /// </summary>
        bool StrikeEnemy(Proj p, HealthManager hm, int damage, float knockback)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null || hm == null || hm.isDead) return false;
            var col = hm.GetComponent<Collider2D>();
            int id = hm.GetInstanceID();
            if (col == null || npcImmune.ContainsKey(id)) return false;
            float facing = col.bounds.center.x >= p.Pos.x ? 1f : -1f;
            if (!melee.HitWith(col, damage, p.MinionShot ? 0f : knockback, p.Crit, facing, col.bounds.center, Equipment.Current.ArmorPen + p.Spec.ArmorPen)) return false;
            npcImmune[id] = 10;
            DevHits[p.Kind] = DevHit(p.Kind) + 1;
            return true;
        }

        List<HealthManager> EnemiesIn(Vector2 centre, float sizePx)
        {
            var found = new List<HealthManager>();
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapBox(centre, Vector2.one * sizePx * Px, 0f, filter, overlaps);
            foreach (var col in overlaps)
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm != null && !hm.isDead && !found.Contains(hm)) found.Add(hm);
            }
            return found;
        }

        static Vector2 Centre(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
        }

        static float Manhattan(HealthManager hm, Vector2 at)
        {
            var d = Centre(hm) - at;
            return (Mathf.Abs(d.x) + Mathf.Abs(d.y)) / Px;
        }

        /// <summary>The nearest enemy within a Manhattan distance (px) in plain sight, as the Chlorophyte Bullet looks for one.</summary>
        static HealthManager Quarry(Vector2 at, float withinPx)
        {
            HealthManager best = null;
            float bestD = withinPx;
            foreach (var col in Physics2D.OverlapCircleAll(at, withinPx * Px, 1 << 11))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || (EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm)) || ArenaGuard.Outside(hm) || ArenaGuard.OffMap(hm)) continue;
                float d = Manhattan(hm, at);
                if (d >= bestD) continue;
                if (Physics2D.Linecast(at, Centre(hm), 1 << ChunkColliders.TerrainLayer)) continue;
                bestD = d;
                best = hm;
            }
            return best;
        }

        /// <summary>
        /// The trail branch of Main.DrawProj for the Luminite shots: copies at the last positions,
        /// fading towards the tail (the bullet's white, the arrow's lit ghost, the afterimage's
        /// white-to-cyan glow).
        /// </summary>
        static void DrawTrail(FxBatch fx, Proj p)
        {
            var s = p.Spec;
            var sprite = Sprite(s.Texture);
            var rot = p.Rot * Mathf.Deg2Rad;
            var back = new Vector2(Mathf.Sin(rot), Mathf.Cos(rot)) * s.PivotBack;
            float a = 1f - p.Alpha;
            for (int n = 1; n < p.Trail.Count; n++)
            {
                Color c;
                if (p.Kind == ProjKind.LuminiteBullet) c = FxBatch.C(255, 255, 255, 100) * a * ((5 - n) / 15f);
                else if (p.Kind == ProjKind.LuminiteArrow) c = new Color(L, L, L, 0.5f) * a * ((10 - n) / 15f);
                else c = new Color(a / n, a, a, 0f) * ((20 - n) / 30f);
                fx.Draw(sprite, TerraParticles.ToPx(p.Trail[n]) + back, c, -rot, 1f);
            }
        }

        // --- More weapons (spec-weapons2). ---

        /// <summary>AI_005 for Starfury's star: a straight fall at 25 px a tick, through everything until below the cursor's height.</summary>
        void StepStarfury(Proj p)
        {
            p.Collide = p.Pos.y - p.Spec.H * p.Spec.Scale * 0.5f * Px <= p.Ai1;
            if (p.SoundDelay > 0) p.SoundDelay--;
            else
            {
                p.SoundDelay = 20 + Random.Range(0, 40);
                Sound("Item_9");
            }
            p.TRot += (Mathf.Abs(p.Vel.x) + Mathf.Abs(p.Vel.y)) * 0.01f * p.Dir;
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            if (Random.Range(0, 20) == 0) pp.NewGore(TerraParticles.ToPx(p.Pos) - new Vector2(10f, 10f), Flip(p.Vel) * 0.2f, Random.Range(16, 18));
            int d;
            if (Random.Range(0, 4) == 0 && (d = DustBox(p, 86, Vector2.zero, 127, 1f, true)) >= 0)
            {
                pp[d].Velocity = pp[d].Velocity * 0.7f + Flip(p.Vel) * 0.3f;
                if (Random.Range(0, 2) == 0) pp[d].Position -= Flip(p.Vel) * 4f;
            }
        }

        /// <summary>
        /// Main.DrawProj for Starfury's star: a comet tail (Extra_91) and its pulsing glow behind a
        /// star that turns between gold and pink as it spins.
        /// </summary>
        static void DrawStarfury(FxBatch fx, Proj p, Vector2 px)
        {
            float c = Frac(p.TRot * 0.5f / (2f * Mathf.PI));
            float k = 1f - Remap(c, 0.15f, 0.5f, 0f, 1f) * Remap(c, 0.5f, 0.85f, 1f, 0f);
            var star = Color.Lerp(FxBatch.C(255, 215, 0, 180), FxBatch.C(194, 22, 134, 127), k);
            var tail = Color.Lerp(FxBatch.C(145, 16, 100, 95), FxBatch.C(40, 4, 57, 57), k);
            var glow = Color.Lerp(FxBatch.C(162, 149, 81, 40), FxBatch.C(38, 38, 38, 0), k);
            var v = Flip(p.Vel);
            var fwd = v.normalized * 8f;
            float ang = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
            var comet = FxBatch.Frame("Extra_91", 0, 0, 34, 90, 17f, 10f);
            fx.Draw(comet, px + fwd, tail, ang, 0.9f);
            foreach (float k2 in new[] { 0f, 0.5f })
            {
                float u = Frac(Time.time % 0.5f / 0.5f + k2);
                float tri = u * 2f;
                if (tri > 1f) tri = 2f - tri;
                fx.Draw(comet, px - v * 0.5f + fwd, glow * tri, ang, (0.5f + 0.5f * u) * 0.75f);
            }
            fx.Draw(Sprite(p.Spec.Texture), px, star, p.TRot, 0.9f + 0.2f * k);
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>
        /// AI_005 for the Star Cloak's star: a straight fall at 23 px a tick, through everything
        /// until below the player's top; fading in to 100 above it and fully below; Item_9 now and
        /// then, star gores and purple dust.
        /// </summary>
        void StepCloakStar(Proj p)
        {
            p.Collide = p.Pos.y < p.Ai1;
            // The Mana Cloak's star off a magic hit lands on what is under the player's top (P:24099-24104).
            if (p.ManaDrop)
            {
                var hero = HeroController.SilentInstance;
                p.Collide = hero != null && p.Pos.y - 12f * Px < hero.GetComponent<BoxCollider2D>().bounds.max.y;
            }
            if (p.SoundDelay > 0) p.SoundDelay--;
            else
            {
                p.SoundDelay = 20 + Random.Range(0, 40);
                Sound("Item_9");
            }
            p.Alpha = Mathf.Max(p.Alpha - 15f / 255f, p.Collide ? 0f : 100f / 255f);
            p.TRot += (Mathf.Abs(p.Vel.x) + Mathf.Abs(p.Vel.y)) * 0.01f * p.Dir;
            if (p.Kind != ProjKind.StarCloakStar)
            {
                CloakStarFlight(p);
                return;
            }
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            if (Random.Range(0, 24) == 0) pp.NewGore(TerraParticles.ToPx(p.Pos) - new Vector2(12f, 12f), Flip(p.Vel) * 0.2f, Random.Range(16, 18));
            int d;
            if (Random.Range(0, 3) == 0 && (d = DustBox(p, 86, Vector2.zero, 127, 1f, true)) >= 0) pp[d].Velocity *= 0.5f;
        }

        /// <summary>
        /// Main.DrawProj for the Star Cloak's star: three purple streaks (Extra_91) circling a step
        /// ahead of it, two pulsing glows half a step behind, and the star added on top.
        /// </summary>
        static void DrawCloakStar(FxBatch fx, Proj p, Vector2 px)
        {
            var v = Flip(p.Vel);
            float ang = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
            var comet = FxBatch.Frame("Extra_91", 0, 0, 34, 90, 17f, 10f);
            CloakStarLook(p.Kind, out var streak, out var glow, out var scales, out float back);
            var shift = v.sqrMagnitude > 1e-6f ? -v.normalized * back : Vector2.zero;
            float T = Time.time;
            for (int k = 0; k < 3; k++)
            {
                float a = 2f * Mathf.PI * T + k * 2f * Mathf.PI / 3f;
                var spin = new Vector2(5f * Mathf.Sin(a), -5f * Mathf.Cos(a));
                if (scales[k] > 0f) fx.Draw(comet, px + v + shift + spin, streak, ang, scales[k]);
            }
            for (float k2 = 0f; k2 < 1f; k2 += 0.5f)
            {
                float u = Frac(T % 0.5f / 0.5f + k2);
                float tri = u * 2f;
                if (tri > 1f) tri = 2f - tri;
                fx.Draw(comet, px - v * 0.5f + shift, glow * tri, ang, 0.5f + u * 0.5f);
            }
            fx.Draw(Sprite(p.Spec.Texture), px, new Color(1f, 1f, 1f, 0f) * (1f - p.Alpha), p.TRot, p.Scale + 0.1f);
        }

        /// <summary>AI_027 for the Enchanted Sword's beam: fades in, then pulses between solid and added light; sparkles behind it.</summary>
        void StepBeam(Proj p)
        {
            int d;
            if (p.Ai1 > 7f && (d = DustAt(p.Pos - p.Vel * 4f * Px + new Vector2(-2f, 2f) * Px, new[] { 15, 57, 58 }[Random.Range(0, 3)], 8, Vector2.zero, 100, 1.25f, false)) >= 0)
                TerraParticles.Instance[d].Velocity *= 0.1f;
            if (p.Ai1 < 15f) p.Ai1++;
            else if (p.Ai0 == 0f)
            {
                p.Scale -= 0.02f;
                p.Alpha += 30f / 255f;
                if (p.Alpha >= 250f / 255f)
                {
                    p.Alpha = 1f;
                    p.Ai0 = 1f;
                }
            }
            else
            {
                p.Scale += 0.02f;
                p.Alpha -= 30f / 255f;
                if (p.Alpha <= 0f)
                {
                    p.Alpha = 0f;
                    p.Ai0 = 0f;
                }
            }
            if (p.Updates == 1) Sound("Item_8");
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x) + 0.785f;
            p.Vel.y = Mathf.Max(p.Vel.y, -16f);
        }

        /// <summary>The beam's sword, tip on its centre (origin at the texture's top-right), white with its alpha as opacity.</summary>
        static void DrawBeam(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Ai1 < 5f) return;
            float n = (p.Ai1 - 5f) / 10f;
            var c = p.Ai1 < 15f ? new Color(n, n, n, n) : new Color(1f, 1f, 1f, p.Alpha);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, 28, 28, 28f, 0f), px, c, p.TRot, p.Scale);
        }

        /// <summary>AI_036: a bee fades in fast, then after 30 updates turns towards the nearest enemy in sight (Manhattan 800 px).</summary>
        void StepBee(Proj p)
        {
            if (p.Alpha > 0f) p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            else p.Extra = 0;
            p.Rot = -p.Vel.x * 0.1f * Mathf.Rad2Deg;
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 3) p.Frame = 0;
            }
            var target = p.Pos + p.Vel * 100f * Px;
            if (++p.Ai0 > 30f)
            {
                p.Ai0 = 30f;
                var q = Quarry(p.Pos, 800f);
                if (q != null) target = Centre(q);
            }
            // The giant bee is quicker on the turn (P:27819-27823: 6.8 and 0.14), the wasp quicker still (P:27795-27799: 9 and 0.2).
            bool giant = p.Kind == ProjKind.GiantBee, wasp = p.Kind == ProjKind.Wasp;
            float speed = giant ? 6.8f : wasp ? 9f : 6f, accel = giant ? 0.14f : wasp ? 0.2f : 0.1f;
            var want = (target - p.Pos).normalized * speed;
            p.Vel.x = Approach(p.Vel.x, want.x, accel, 2f);
            p.Vel.y = Approach(p.Vel.y, want.y, accel, 2f);
        }

        /// <summary>
        /// AI_019_Spears: the spear's tip goes out from the player's centre along the thrust for the
        /// first two thirds of the use and comes back over the last third; it lives the use.
        /// </summary>
        void StepSpear(Proj p, HeroController hero)
        {
            if (hero == null)
            {
                p.Dead = true;
                return;
            }
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            int N = Mathf.Max(1, (int)p.Ai1), a = N - 1 - (int)p.Ai0, T = N / 3;
            bool lance = p.Kind == ProjKind.DarkLance;
            var off = p.Spec.SpearOffset == Vector3.zero ? new Vector3(4f, 0.9f, 1.2f) : p.Spec.SpearOffset;
            float n6 = off.x, n7 = off.y, n8 = off.z;
            float f = n6 + n7 * ((N - T) - Mathf.Max(0, a - T)) - n8 * (T - Mathf.Min(a, T));
            p.Pos = centre + p.Vel * f * Px;
            p.Anim = a;
            if (lance)
            {
                var pp = TerraParticles.Instance;
                int d;
                if (Random.Range(0, 5) == 0) DustBox(p, 14, Vector2.zero, 150, 1.4f, false);
                if ((d = DustBox(p, 27, Flip(p.Vel) * 0.2f + new Vector2(p.Dir * 3f, 0f), 100, 1.2f, true)) >= 0) pp[d].Velocity /= 2f;
                if ((d = DustAt(p.Pos - p.Vel * 2f * Px, 27, (int)(p.Spec.W * p.Spec.Scale), Vector2.zero, 150, 1.4f, false)) >= 0) pp[d].Velocity /= 5f;
            }
            // What the other spears shed and leave behind (TerraProjectilesSpears.cs); a < T: drawing back.
            SpearExtras(p, centre, a < T);
            if (p.Dead) return;
            if (++p.Ai0 >= N) Kill(p);
        }

        /// <summary>A spear's reach past its tip while thrusting out (the Dark Lance, Gungnir): its far end, the size of its boxes and how far out it is (0..1).</summary>
        static bool LanceReach(Proj p, out Vector2 ext, out float size, out float t)
        {
            int N = Mathf.Max(1, (int)p.Ai1), a = p.Anim, T = N / 3;
            ext = p.Pos;
            size = 0f;
            t = 0f;
            if (p.Spec.SpearReach <= 0f || a < T || N == T) return false;
            t = (N - a) / (float)(N - T);
            float scale = Equipment.UseScale(ItemDb.Get(p.Spec.SpearItem));
            ext = p.Pos + p.Vel.normalized * (10f + p.Spec.SpearReach * t * scale) * Px;
            size = 10f + p.Spec.SpearReachSize * t;
            return true;
        }

        /// <summary>
        /// DrawProj_Spear: the diagonal spear art with its head (the texture's top-left) on the tip,
        /// mirrored when the player faces right; the streaks before a spear that reaches past its tip
        /// (the Dark Lance's purple, Gungnir's gold).
        /// </summary>
        static void DrawSpear(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = Sprite(p.Spec.Texture).rect;
            var spear = FxBatch.Frame(p.Spec.Texture, 0, 0, (int)whole.width, (int)whole.height, 0f, 0f);
            var v = Flip(p.Vel);
            bool right = p.Dir > 0f;
            float rot = Mathf.Atan2(v.y, v.x) + 2.355f;
            if (right) rot -= Mathf.PI / 2f;
            var hero = HeroController.SilentInstance;
            if (p.Spec.SpearReach > 0f && hero != null && LanceReach(p, out var extWorld, out float size, out _))
            {
                int N = Mathf.Max(1, (int)p.Ai1);
                float pr = Remap(p.Anim, N, N / 3f, 0f, 1f);
                float w = Remap(pr, 0f, 0.3f, 0f, 1f) * Remap(pr, 0.3f, 1f, 1f, 0f);
                w = 1f - (1f - w) * (1f - w);
                float q = size / 22f;
                var glint = FxBatch.Whole("Extra_98");
                var col = p.Spec.SpearGlint * w;
                float grot = rot - Mathf.PI / 4f * (right ? -1f : 1f);
                var ext = TerraParticles.ToPx(extWorld);
                var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
                var down = new Vector2(0f, 2f);
                fx.Draw(glint, Vector2.Lerp(ext, px, 0.5f), col, grot, new Vector2(w * q, q) * 1.1f * q, flipX: right, overPlayer: true);
                fx.Draw(glint, ext, col, grot, new Vector2(w * q, 1.5f * q) * 1.1f * q, flipX: right, overPlayer: true);
                var thin = new Vector2(w * w * q, 2f * w * q) * 1.1f * q;
                fx.Draw(glint, Vector2.LerpUnclamped(player, px, 1.5f * pr - 0.5f) + down, col, grot, thin, flipX: right, overPlayer: true);
                for (float k = 0.4f; k <= 1.001f; k += 0.1f)
                    fx.Draw(glint, Vector2.LerpUnclamped(player, ext, k + 0.2f) + down, col * (0.75f * k), grot, thin, flipX: right, overPlayer: true);
            }
            fx.Draw(spear, px, new Color(L, L, L, 1f), rot, p.Scale, flipX: right, overPlayer: true);
        }

        /// <summary>AI_002 for the shuriken and the throwing knife: straight for 19 updates, then a heavy arc; spinning (the knife point-first until it drops).</summary>
        void StepThrown2(Proj p)
        {
            float dir = p.Vel.x < 0f ? -1f : 1f;
            p.Rot -= (Mathf.Abs(p.Vel.x) + Mathf.Abs(p.Vel.y)) * 0.03f * dir * Mathf.Rad2Deg;
            if (++p.Ai0 >= 20f)
            {
                p.Vel.y -= 0.4f;
                p.Vel.x *= 0.97f;
            }
            else if (p.Kind == ProjKind.ThrowingKnife) p.Rot = Angle(p.Vel);
            p.Vel.y = Mathf.Max(p.Vel.y, -32f);
        }

        /// <summary>
        /// AI_009_MagicMissiles: while the button stays held it flies at the cursor (snapping to it,
        /// up to 32 px an update); let go, it homes on the nearest enemy in sight within 800 px or
        /// flies straight on at 32 px for at most 300 more updates. The Flamelash, once it has hit,
        /// burns darker and looks for another.
        /// </summary>
        void StepMissile(Proj p, HeroController hero)
        {
            bool lash = p.Kind == ProjKind.Flamelash;
            p.OldVel = p.Vel;
            bool free = p.Free;
            if (lash)
            {
                if (p.FrameCounter++ >= 4)
                {
                    p.FrameCounter = 0;
                    p.Frame = (p.Frame + 1) % 6;
                }
                if (p.Penetrate == 1) p.Spent = true;
                if (p.Spent) p.LocalAi0++;
            }
            var mod = TerraMod.Instance;
            Vector2 ownerCentre = hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : p.Pos;
            if (!p.Free)
            {
                bool held = p.Channel && mod != null && mod.UseHeld && mod.Inventory.Current.Def.Shoot == p.Kind;
                if (!held)
                {
                    p.Free = true;
                    p.Channel = false;
                    p.Target = Nearest(p, 800f);
                    if (p.Target == null)
                        p.Vel = p.Vel.magnitude < 2f ? (p.Pos - ownerCentre).normalized * 32f : p.Vel.normalized * 32f;
                }
            }
            else if (p.Target == null) p.Target = Nearest(p, 800f);

            Vector2? point = null;
            float amount = 1f;
            if (!p.Free && mod != null) point = mod.DevAim ?? mod.AimPoint;
            if (free && p.Target != null)
            {
                if (!p.Target.isDead && p.Target.gameObject.activeInHierarchy)
                {
                    point = Centre(p.Target);
                    amount = 0.2f;
                }
                else p.Target = null;
            }
            bool moving = free;
            if (point.HasValue)
            {
                var d = (point.Value - p.Pos) / Px;
                if (d.magnitude >= 64f)
                {
                    moving = true;
                    var want = d.normalized * Mathf.Min(32f, d.magnitude);
                    if (p.Vel.magnitude < 4f && p.Vel.sqrMagnitude > 0f) p.Vel += (Vector2)(Quaternion.Euler(0f, 0f, -45f) * p.Vel.normalized) * 4f;
                    p.Vel = Vector2.Lerp(p.Vel, want, amount);
                }
                else
                {
                    p.Vel = p.Vel * 0.3f + d * 0.3f;
                    moving = p.Vel.magnitude >= 2f;
                }
                p.TimeLeft = Mathf.Max(p.TimeLeft, 60);
            }
            if (free && p.Target == null)
            {
                if (p.Vel.magnitude != 32f) p.Vel = Vector2.MoveTowards(p.Vel, (p.Vel.sqrMagnitude > 0f ? p.Vel.normalized : Vector2.down) * 32f, 4f);
                p.TimeLeft = Mathf.Min(p.TimeLeft, 300);
            }
            float vAngle = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            p.TRot = moving && p.Vel != Vector2.zero
                ? Mathf.MoveTowardsAngle(p.TRot * Mathf.Rad2Deg, vAngle * Mathf.Rad2Deg, 45f) * Mathf.Deg2Rad
                : Mathf.LerpAngle(p.TRot * Mathf.Rad2Deg, 0f, 0.2f) * Mathf.Deg2Rad;
            bool sharp = p.Vel.magnitude > 0.1f && Vector2.Dot(p.OldVel.normalized, p.Vel.normalized) < 0.2f;

            var pp = TerraParticles.Instance;
            if (pp == null) return;
            int dd;
            // The Rainbow Rod sheds nothing: its rainbow is drawn (RainbowRodDrawer).
            if (p.Kind == ProjKind.RainbowRod) return;
            if (!lash)
            {
                if (p.SoundDelay > 0) p.SoundDelay--;
                else if (Mathf.Abs(p.Vel.x) + Mathf.Abs(p.Vel.y) > 2f)
                {
                    p.SoundDelay = 10;
                    Sound("Item_9");
                }
                if (Random.Range(0, 9) == 0 && (dd = DustBox(p, 15, Vector2.zero, 100, 2f, true)) >= 0)
                {
                    pp[dd].Velocity = pp[dd].Velocity * 0.3f + Random.insideUnitCircle * 2f;
                    pp[dd].Position = TerraParticles.ToPx(p.Pos) + new Vector2(4f + Random.Range(-4, 5), Random.Range(-4, 5));
                }
                if (sharp)
                    for (int n = Random.Range(2, 5); n > 0; n--)
                        if ((dd = DustBox(p, 15, Vector2.zero, 100, 1.5f, true)) >= 0)
                        {
                            pp[dd].Velocity = pp[dd].Velocity * 0.3f + Random.insideUnitCircle * 0.5f;
                            pp[dd].Position = TerraParticles.ToPx(p.Pos);
                            pp[dd].FadeIn = 2.2f;
                        }
                return;
            }
            float lv = Remap(p.LocalAi0, 0f, 10f, 0f, 1f);
            var tint = Color.Lerp(Color.clear, FxBatch.C(220, 20, 60), lv);
            if (Random.Range(0, 6) == 0 && (dd = DustAt(p.Pos, 6, 0, Flip(p.Vel) * 0.2f, 100, 3.5f, true)) >= 0)
            {
                pp[dd].Color = tint;
                pp[dd].Velocity = pp[dd].Velocity * 1.4f + Random.insideUnitCircle + Flip(p.Vel) * 0.15f;
            }
            if (Random.Range(0, 12) == 0 && (dd = DustAt(p.Pos, 6, 0, Flip(p.Vel) * 0.2f, 100, 1.5f, false)) >= 0)
            {
                pp[dd].Color = tint;
                pp[dd].Velocity += Random.insideUnitCircle + Flip(p.Vel) * 0.15f;
            }
            if (sharp)
                for (int n = Random.Range(2, 5 + (int)(lv * 4f)); n > 0; n--)
                    if ((dd = DustBox(p, 6, Vector2.zero, 100, 1.5f, true)) >= 0)
                    {
                        pp[dd].Color = tint;
                        pp[dd].Velocity = pp[dd].Velocity * 0.3f + Random.insideUnitCircle * 0.5f;
                        pp[dd].Position = TerraParticles.ToPx(p.Pos);
                        pp[dd].FadeIn = 2.2f;
                    }
        }

        /// <summary>Trail mode 3: the last 30 positions, each pulled 65% towards the one before it when more than 2 px apart.</summary>
        static void PushMissileTrail(Proj p)
        {
            p.Trail.Insert(0, p.Pos);
            if (p.Trail.Count > p.Spec.TrailLength) p.Trail.RemoveAt(p.Trail.Count - 1);
            for (int i = 1; i < p.Trail.Count; i++)
                if ((p.Trail[i] - p.Trail[i - 1]).sqrMagnitude > 4f * Px * Px) p.Trail[i] = Vector2.Lerp(p.Trail[i], p.Trail[i - 1], 0.65f);
        }

        /// <summary>FindTargetWithLineOfSight: the nearest enemy in sight within a distance (px) that this shot isn't immune to.</summary>
        static HealthManager Nearest(Proj p, float withinPx)
        {
            HealthManager best = null;
            float bestD = withinPx * Px;
            foreach (var col in Physics2D.OverlapCircleAll(p.Pos, withinPx * Px, 1 << 11))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || (EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm)) || ArenaGuard.Outside(hm) || ArenaGuard.OffMap(hm) || p.Immune.ContainsKey(hm.GetInstanceID())) continue;
                var c = Centre(hm);
                float d = Vector2.Distance(c, p.Pos);
                if (d >= bestD || Physics2D.Linecast(p.Pos, c, 1 << ChunkColliders.TerrainLayer)) continue;
                bestD = d;
                best = hm;
            }
            return best;
        }

        /// <summary>
        /// The 1.4.5 missiles' end: everyone else in a 128 px box (the Flamelash's 96) takes its
        /// damage once, with sparks scattered back along its trail and a burst where it ended.
        /// </summary>
        void KillMissile(Proj p)
        {
            bool lash = p.Kind == ProjKind.Flamelash;
            Blast(p, lash ? 96f : 128f, p.Damage, p.Knockback);
            Sound("Item_10");
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var towards = p.Pos;
            const int length = 30;
            for (int i = 0; i < p.Trail.Count; i++)
            {
                var at = p.Trail[i];
                var col = Hsl(lash ? Random.value / 9f : 4f / 9f + Random.value * 2f / 9f, 1f, lash ? 0.5f : 0.65f);
                int count = lash ? Random.Range(1, 5) : Random.Range(1, 4);
                float s = Mathf.Lerp(0.3f, 1f, Remap(i, length, 0f, 0f, 1f));
                if (i >= length * 0.3f) count--;
                if (i >= length * 0.75f) count -= 2;
                var dir = Flip((towards - at).normalized);
                towards = at;
                for (int k = 0; k < count; k++)
                {
                    int d;
                    if (Random.Range(0, 3) == 0)
                    {
                        if ((d = DustAt(at, 267, p.Spec.W, Vector2.zero, 0, 1f, true)) < 0) continue;
                        pp[d].Color = col;
                        pp[d].Velocity *= Random.value * 0.8f;
                        pp[d].Scale = Random.value * (lash ? 1f : 0.8f);
                        pp[d].FadeIn = Random.value * (lash ? 2f : 1.2f * s);
                        pp[d].Velocity += dir * (lash ? 8f : 6f);
                        pp[d].Scale *= s;
                        int e = pp.Clone(d);
                        if (e >= 0)
                        {
                            pp[e].Scale /= 2f;
                            pp[e].FadeIn /= 2f;
                            pp[e].Color = Color.white;
                        }
                    }
                    else if (!lash)
                    {
                        if ((d = DustAt(at, 15, p.Spec.W, -Flip(p.Vel) * 0.2f, 100, 1f, true)) < 0) continue;
                        pp[d].Velocity = pp[d].Velocity * 2f + dir * 9f;
                        pp[d].Scale *= s;
                        pp[d].FadeIn = (0.6f + Random.value * 0.4f) * s;
                    }
                    else
                    {
                        if ((d = DustAt(at, 6, p.Spec.W, -Flip(p.Vel) * 0.2f, 100, 1f, false)) < 0) continue;
                        if (Random.Range(0, 2) == 0)
                        {
                            pp[d].NoGravity = true;
                            pp[d].Scale *= 2.5f;
                        }
                        pp[d].Velocity = pp[d].Velocity * 2f + dir * 6f;
                        pp[d].Scale *= s;
                    }
                }
            }
            for (int i = 0; i < 20; i++)
            {
                int d;
                if (!lash)
                {
                    if ((d = DustBox(p, 15, Vector2.zero, 0, 1f, true)) >= 0)
                    {
                        pp[d].Velocity = Random.insideUnitCircle * 1.5f + Flip(p.Vel) * 0.01f;
                        pp[d].Scale = 1.2f + Random.value * 0.5f;
                        pp[d].Position += pp[d].Velocity * Random.Range(1, 16);
                    }
                    if ((d = DustBox(p, 15, Vector2.zero, 100, 1f, false)) >= 0)
                    {
                        pp[d].Velocity = pp[d].Velocity * 1.2f + Flip(p.Vel) * 0.01f;
                        pp[d].Scale *= 0.8f + Random.value * 0.2f;
                        pp[d].Position += pp[d].Velocity * Random.Range(1, 16);
                    }
                }
                else
                {
                    if ((d = DustBox(p, 6, -Flip(p.Vel) * 0.2f, 100, 1f, true)) >= 0)
                    {
                        pp[d].Velocity = Random.insideUnitCircle * 6f + Flip(p.Vel) * 0.1f;
                        pp[d].Scale = 1.6f;
                        pp[d].FadeIn = 1.3f + Random.value;
                    }
                    if ((d = DustBox(p, 6, -Flip(p.Vel) * 0.2f, 100, 1f, false)) >= 0)
                        pp[d].Velocity = pp[d].Velocity * 2f + Flip(p.Vel) * 0.1f;
                }
            }
        }

        /// <summary>Main.hslToRgb.</summary>
        static Color Hsl(float h, float s, float l)
        {
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s, p = 2f * l - q;
            float Hue(float t)
            {
                t = Frac(t);
                if (t < 1f / 6f) return p + (q - p) * 6f * t;
                if (t < 0.5f) return q;
                if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
                return p;
            }
            return new Color(Hue(h + 1f / 3f), Hue(h), Hue(h - 1f / 3f), 1f);
        }

        /// <summary>
        /// The missiles' look: a ribbon along the smoothed trail (MagicMissileDrawer /
        /// FlameLashDrawer; the shader is approximated with soft strokes, as the gem bolts' trail
        /// is), then the Magic Missile's stretched blue star with four breathing glows, or the
        /// Flamelash's flame trailing its tip, glowing once it has hit.
        /// </summary>
        static void DrawMissile(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Kind == ProjKind.RainbowRod)
            {
                DrawRainbowRod(fx, p, px);
                return;
            }
            bool lash = p.Kind == ProjKind.Flamelash;
            float dark = lash ? Remap(p.LocalAi0, 0f, 6f, 0f, 1f) : 0f;
            var dot = FxBatch.Whole("Extra_98");
            for (int i = 1; i < p.Trail.Count; i++)
            {
                float t = (i - 0.5f) / p.Spec.TrailLength;
                float width;
                Color col;
                if (!lash)
                {
                    width = Mathf.Lerp(26f, 32f, Remap(t, 0f, 0.2f, 0f, 1f)) * Remap(t, 0f, 0.07f, 0f, 1f);
                    col = Color.Lerp(Color.white, FxBatch.C(238, 130, 238), Remap(t, 0f, 0.7f, 0f, 1f)) * (1f - Remap(t, 0f, 0.98f, 0f, 1f));
                    col.a /= 2f;
                }
                else
                {
                    float k = Remap(t, 0f, 0.06f + 0.01f * dark, 0f, 1f);
                    k = 1f - (1f - k) * (1f - k);
                    width = Mathf.Lerp(24f + 16f * dark, 8f, t) * k;
                    col = Color.Lerp(Color.Lerp(Color.white, FxBatch.C(255, 165, 0), 0.5f * dark), Color.red, Remap(t, -0.1f * dark, 0.7f - 0.2f * dark, 0f, 1f)) * (1f - Remap(t, 0f, 0.98f, 0f, 1f));
                    col.a /= 8f;
                }
                if (width <= 0.01f) continue;
                var a = TerraParticles.ToPx(p.Trail[i - 1]);
                var b = TerraParticles.ToPx(p.Trail[i]);
                var seg = b - a;
                if (seg.sqrMagnitude < 0.01f) continue;
                fx.Draw(dot, (a + b) / 2f, col, Mathf.Atan2(seg.y, seg.x) + Mathf.PI / 2f, new Vector2(width / 72f * 3f, Mathf.Max(seg.magnitude, 1f) / 72f * 1.6f));
            }
            float l = Remap(p.Vel.magnitude, 0f, 8f, 0f, 1f);
            if (!lash)
            {
                var star = Sprite(p.Spec.Texture);
                float rot = p.TRot * l;
                var scale = 0.6f * new Vector2(Mathf.Lerp(1f, 1.5f, l), Mathf.Lerp(1f, 0.8f, l));
                var added = new Color(1f, 1f, 1f, 0f);
                fx.Draw(star, px, added, rot, scale);
                var S = scale * (1f + 0.4f * Mathf.Cos(2f * Mathf.PI * Time.time));
                for (int k = 0; k < 4; k++)
                {
                    float r = rot + k * Mathf.PI / 2f;
                    fx.Draw(star, px + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 2f * S.x, FxBatch.C(80, 80, 80, 0), rot, S);
                }
                fx.Draw(star, px, added, rot, scale);
                return;
            }
            var flame = FxBatch.Frame(p.Spec.Texture, 0, p.Frame * 64, 48, 64, 24f, 32f);
            var at = (p.Trail.Count > 0 ? TerraParticles.ToPx(p.Trail[0]) : px) - Flip(p.Vel) * 0.5f;
            float frot = p.TRot * l - Mathf.PI / 2f * l;
            fx.Draw(flame, at, Color.white, frot, new Vector2(Mathf.Lerp(1f, 0.8f, l), 1f));
            if (dark > 0f)
                for (int k = 0; k < 4; k++)
                {
                    float r = frot + k * Mathf.PI / 2f;
                    fx.Draw(flame, at + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 4f * dark, FxBatch.C(255, 255, 255, 127) * 0.75f, frot, dark);
                }
        }

        /// <summary>AI_004: a thorn grows in over 6 updates and then sprouts the next one 32 px on (six, then the tip), and fades away.</summary>
        void StepVine(Proj p)
        {
            if (p.Ai1 == 0f && !p.Spent)
            {
                p.Spent = true;
                Sound("Item_8");
            }
            p.Rot = Angle(p.Vel);
            if (p.Ai0 == 0f)
            {
                p.Alpha = (Mathf.Round(p.Alpha * 255f) - 50f) / 255f;
                if (p.Alpha > 0f) return;
                p.Alpha = 0f;
                p.Ai0 = 1f;
                if (p.Ai1 == 0f)
                {
                    p.Ai1 = 1f;
                    p.Pos += p.Vel * Px;
                }
                if (p.Kind == ProjKind.VilethornBase)
                {
                    var next = Spawn(p.Ai1 >= 6f ? ProjKind.VilethornTip : ProjKind.VilethornBase, p.Pos + p.Vel * Px, p.Vel, p.Damage, p.Knockback, p.Crit);
                    if (next != null)
                    {
                        next.Ai1 = p.Ai1 + 1f;
                        next.Spent = true;
                    }
                }
                return;
            }
            int alpha = Mathf.RoundToInt(p.Alpha * 255f);
            if (alpha < 170 && alpha + 5 >= 170)
            {
                for (int i = 0; i < 3; i++) DustBox(p, 18, Flip(p.Vel) * 0.025f, 170, 1.2f, false);
                DustBox(p, 14, Vector2.zero, 170, 1.1f, false);
            }
            alpha += 5;
            p.Alpha = alpha / 255f;
            if (alpha >= 255) Kill(p);
        }

        /// <summary>AI_012 for the Aqua Scepter: an unseen stream of water dust that falls after 4 updates and dries up over 100.</summary>
        void StepStream(Proj p)
        {
            p.Scale -= 0.01f;
            if (p.Scale <= 0f)
            {
                Kill(p);
                return;
            }
            if (p.Ai0 <= 3f)
            {
                p.Ai0++;
                return;
            }
            p.Vel.y -= 0.15f;
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            int d;
            for (int k = 0; k < 3; k++)
                if ((d = DustAt(p.Pos - p.Vel * Px / 3f * k, 172, 6, Vector2.zero, 100, 1.2f, true)) >= 0) pp[d].Velocity = pp[d].Velocity * 0.3f + Flip(p.Vel) * 0.5f;
            if (Random.Range(0, 8) == 0 && (d = DustAt(p.Pos, 172, 6, Vector2.zero, 100, 0.75f, false)) >= 0) pp[d].Velocity = pp[d].Velocity * 0.5f + Flip(p.Vel) * 0.5f;
        }

        /// <summary>
        /// The minion shots' per-update extras: the first update's sound (stinger Item17, fireball
        /// Item20, laser Item12); the stinger's green bits; the fireball's flames from its fourth
        /// update on (two from its sixth).
        /// </summary>
        void MinionShotUpdate(Proj p)
        {
            if (p.Updates == 1) Sound(p.Kind == ProjKind.HornetStinger ? "Item_17" : p.Kind == ProjKind.ImpFireball ? "Item_20" : "Item_12");
            if (p.Kind == ProjKind.HornetStinger && Random.Range(0, 2) == 0)
            {
                int d = DustBox(p, 18, Vector2.zero, 0, 0.9f, true);
                if (d >= 0) TerraParticles.Instance[d].Velocity *= 0.5f;
            }
            if (p.Kind == ProjKind.ImpFireball && ++p.LocalAi0 > 3f)
            {
                int n = p.LocalAi0 > 5f ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    int d = DustAt(p.Pos + new Vector2(0f, -2f) * Px, 6, 12, Flip(p.Vel) * 0.2f, 100, 2f, true);
                    if (d >= 0) TerraParticles.Instance[d].Velocity *= 0.3f;
                }
            }
        }

        // --- Helpers. ---

        /// <summary>World velocity (y up) to Terraria's (y down), for dust.</summary>
        static Vector2 Flip(Vector2 v) => new Vector2(v.x, -v.y);

        static float Remap(float v, float a, float b, float c, float d) => Mathf.Lerp(c, d, Mathf.Clamp01((v - a) / (b - a)));

        Vector2? NearestEnemy(Vector2 at, float within)
        {
            Vector2? best = null;
            float bestD = within;
            foreach (var col in Physics2D.OverlapCircleAll(at, within, 1 << 11))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || (EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm)) || ArenaGuard.Outside(hm) || ArenaGuard.OffMap(hm)) continue;
                float d = Vector2.Distance(col.bounds.center, at);
                if (d >= bestD) continue;
                if (Physics2D.Linecast(at, col.bounds.center, 1 << ChunkColliders.TerrainLayer)) continue;
                bestD = d;
                best = col.bounds.center;
            }
            return best;
        }

        /// <summary>Dust.NewDust over the projectile's hitbox, with Terraria's (y down) speed.</summary>
        static int DustBox(Proj p, int type, Vector2 speedYDown, int alpha, float scale, bool noGravity)
        {
            var particles = TerraParticles.Instance;
            if (particles == null || type < 0) return -1;
            var topLeft = TerraParticles.ToPx(p.Pos) - new Vector2(p.Spec.W, p.Spec.H) * 0.5f;
            int d = particles.NewDust(topLeft, p.Spec.W, p.Spec.H, type, speedYDown.x, speedYDown.y, alpha, scale);
            if (d >= 0) particles[d].NoGravity = noGravity;
            return d;
        }

        static int DustAt(Vector2 world, int type, int box, Vector2 speedYDown, int alpha, float scale, bool noGravity)
        {
            var particles = TerraParticles.Instance;
            if (particles == null) return -1;
            var topLeft = TerraParticles.ToPx(world) - new Vector2(box, box) * 0.5f;
            int d = particles.NewDust(topLeft, box, box, type, speedYDown.x, speedYDown.y, alpha, scale);
            if (d >= 0) particles[d].NoGravity = noGravity;
            return d;
        }

        static void Sound(string name, float volume = 1f) => TerraMod.Instance?.PlaySound(name, volume);

        public void Clear()
        {
            for (int i = live.Count - 1; i >= 0; i--) Remove(i);
            pending.Clear();
        }

        /// <summary>A short-lived light fading out (the flash of an explosion).</summary>
        sealed class FadeOut : MonoBehaviour
        {
            float time, length;
            SpriteRenderer sr;
            Color start;

            public void Begin(float seconds)
            {
                length = seconds;
                sr = GetComponent<SpriteRenderer>();
                start = sr.color;
            }

            void Update()
            {
                time += Time.deltaTime;
                float k = 1f - time / length;
                if (k <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                sr.color = start * k;
            }
        }
    }
}
