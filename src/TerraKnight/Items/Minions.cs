using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's minions (spec-summoner A, spec-minions-ground, spec-minions-flying): what the
    /// summon staves call, run on Terraria's 60 Hz tick in Terraria's own space (pixels, y down,
    /// top-left positions) so the AIs port line for line, and converted only to collide with the
    /// grid and the scene, to hit enemies (through <see cref="MeleeHits.HitWith"/>) and to draw.
    /// Each tick, in spawn order: summon damage is refreshed from the gear, minionPos counted and
    /// any minion past the slot cap killed; a minion lives while its buff's flag holds and dies
    /// two ticks after it drops. A staff use frees slots first (the cheapest, oldest minions go).
    /// Also the owner's minion target (right click with a staff, or a whip strike), the target
    /// reticle, the Cool Whip's snowflake and the Firecracker's blast.
    /// </summary>
    internal sealed partial class Minions : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        /// <summary>The environment's light (Hallownest has no light map: the dust's constant).</summary>
        const float L = 0.874f;

        public static Minions Instance { get; private set; }

        internal enum Kind : byte
        {
            BabySlime, VenomSpider, JumperSpider, DangerousSpider, OneEyedPirate, SoulscourgePirate, PirateCaptain, VampireFrog, Flinx,
            Finch, Hornet, Imp, AbigailCounter, Abigail, Retanimini, Spazmamini, Bat, DragonHead, DragonBody1, DragonBody2, DragonTail,
            Snowflake, FireBlast, CobSpider, DeadlySphere, Sharknado, UFO, StardustCell, Raven, Terraprisma, TigerGem, Tiger1, Tiger2, Tiger3, BladeStaff,
            Pygmy1, Pygmy2, Pygmy3, Pygmy4,
            Flameburst1, Flameburst2, Flameburst3, Ballista1, Ballista2, Ballista3, Trap1, Trap2, Trap3, TrapBlast1, TrapBlast2, TrapBlast3, Aura1, Aura2, Aura3,
            SpiderHiver, SpiderEgg, BabySpider, FrostHydra, Houndius, LunarPortal, PortalLaser, RainbowCrystal, CrystalBlast, Barnacle,
            MushroomBoi, Cattiva, TrustyCattiva, Foxsparks, TrustyFoxsparks, ClayPot, ForbiddenRelic,
            WanderingSoul,
            ChoirSoul,
            ShadeSoul,
            DirgeSoul,
        }

        /// <summary>A minion type's SetDefaults: size, slots, its buff, texture and frames, NPC immunity.</summary>
        sealed class KindSpec
        {
            public int Id, W, H, Frames = 1, Buff;
            public float Slots;
            public string Texture;
            /// <summary>usesIDStaticNPCImmunity / usesLocalNPCImmunity cooldowns (ticks); OncePerNpc = local −1.</summary>
            public int TypeCooldown, LocalCooldown;
            public bool OncePerNpc, Minion = true;
            /// <summary>Projectile.sentry: no slot, 36000 ticks of life, counted against maxTurrets.</summary>
            public bool Sentry;
            /// <summary>ProjectileID.Sets.SentryShot / MinionShot: a sentry's or minion's child, its hits take the whip tags.</summary>
            public bool Shot;
            /// <summary>The staff that summons it (for the death respawn) and whether that record is kept (TrackMinionSpawnFromItemUse).</summary>
            public bool Tracked = true;
        }

        static readonly Dictionary<Kind, KindSpec> specs = new Dictionary<Kind, KindSpec>
        {
            [Kind.BabySlime] = new KindSpec { Id = 266, W = 24, H = 16, Slots = 1, Buff = PlayerBuffs.BabySlime, Texture = "Projectile_266", Frames = 12, TypeCooldown = 12 },
            [Kind.VenomSpider] = new KindSpec { Id = 390, W = 30, H = 30, Slots = 1, Buff = PlayerBuffs.SpiderMinion, Texture = "Projectile_390", Frames = 11, TypeCooldown = 15 },
            [Kind.JumperSpider] = new KindSpec { Id = 391, W = 30, H = 30, Slots = 1, Buff = PlayerBuffs.SpiderMinion, Texture = "Projectile_391", Frames = 11, TypeCooldown = 15 },
            [Kind.DangerousSpider] = new KindSpec { Id = 392, W = 30, H = 30, Slots = 1, Buff = PlayerBuffs.SpiderMinion, Texture = "Projectile_392", Frames = 11, TypeCooldown = 15 },
            [Kind.OneEyedPirate] = new KindSpec { Id = 393, W = 20, H = 30, Slots = 1, Buff = PlayerBuffs.PirateMinion, Texture = "Projectile_393", Frames = 15, LocalCooldown = 18 },
            [Kind.SoulscourgePirate] = new KindSpec { Id = 394, W = 20, H = 30, Slots = 1, Buff = PlayerBuffs.PirateMinion, Texture = "Projectile_394", Frames = 15, LocalCooldown = 18 },
            [Kind.PirateCaptain] = new KindSpec { Id = 395, W = 20, H = 30, Slots = 1, Buff = PlayerBuffs.PirateMinion, Texture = "Projectile_395", Frames = 15, LocalCooldown = 18 },
            [Kind.VampireFrog] = new KindSpec { Id = 758, W = 20, H = 30, Slots = 1, Buff = PlayerBuffs.VampireFrog, Texture = "Projectile_758", Frames = 24, TypeCooldown = 10 },
            [Kind.Flinx] = new KindSpec { Id = 951, W = 26, H = 26, Slots = 1, Buff = PlayerBuffs.FlinxMinion, Texture = "Projectile_951", Frames = 12, TypeCooldown = 10 },
            [Kind.Finch] = new KindSpec { Id = 759, W = 10, H = 10, Slots = 1, Buff = PlayerBuffs.BabyBird, Texture = "Projectile_759", Frames = 5, LocalCooldown = 15 },
            [Kind.Hornet] = new KindSpec { Id = 373, W = 24, H = 26, Slots = 1, Buff = PlayerBuffs.HornetMinion, Texture = "Projectile_373", Frames = 3 },
            [Kind.Imp] = new KindSpec { Id = 375, W = 34, H = 26, Slots = 1, Buff = PlayerBuffs.ImpMinion, Texture = "Projectile_375", Frames = 8 },
            [Kind.AbigailCounter] = new KindSpec { Id = 970, W = 10, H = 10, Slots = 1, Buff = PlayerBuffs.AbigailMinion, Frames = 6 },
            [Kind.Abigail] = new KindSpec { Id = 963, W = 30, H = 48, Slots = 0, Buff = PlayerBuffs.AbigailMinion, Texture = "Projectile_963", Frames = 13, LocalCooldown = 20, Tracked = false },
            [Kind.Retanimini] = new KindSpec { Id = 387, W = 40, H = 20, Slots = 0.5f, Buff = PlayerBuffs.TwinEyesMinion, Texture = "Projectile_387", Frames = 3, TypeCooldown = 16 },
            [Kind.Spazmamini] = new KindSpec { Id = 388, W = 40, H = 20, Slots = 0.5f, Buff = PlayerBuffs.TwinEyesMinion, Texture = "Projectile_388", Frames = 3, TypeCooldown = 12, Tracked = false },
            [Kind.Bat] = new KindSpec { Id = 755, W = 10, H = 10, Slots = 1, Buff = PlayerBuffs.BatOfLight, Texture = "Projectile_755", Frames = 5, OncePerNpc = true },
            [Kind.DragonHead] = new KindSpec { Id = 625, W = 24, H = 24, Slots = 0, Buff = PlayerBuffs.StardustDragonMinion, Texture = "Projectile_625", LocalCooldown = 7, Tracked = false },
            [Kind.DragonBody1] = new KindSpec { Id = 626, W = 24, H = 24, Slots = 0.5f, Buff = PlayerBuffs.StardustDragonMinion, Texture = "Projectile_626", LocalCooldown = 7 },
            [Kind.DragonBody2] = new KindSpec { Id = 627, W = 24, H = 24, Slots = 0.5f, Buff = PlayerBuffs.StardustDragonMinion, Texture = "Projectile_627", LocalCooldown = 7, Tracked = false },
            [Kind.DragonTail] = new KindSpec { Id = 628, W = 24, H = 24, Slots = 0, Buff = PlayerBuffs.StardustDragonMinion, Texture = "Projectile_628", LocalCooldown = 7, Tracked = false },
            [Kind.Snowflake] = new KindSpec { Id = 917, W = 30, H = 30, Texture = "Projectile_917", LocalCooldown = 20, Minion = false, Tracked = false },
            [Kind.FireBlast] = new KindSpec { Id = 918, W = 96, H = 96, OncePerNpc = true, Minion = false, Tracked = false },
            // The Cob Whip's spider (1036): not a minion, its 2 damage fixed; it lives on the whip's buff.
            [Kind.CobSpider] = new KindSpec { Id = 1036, W = 18, H = 10, Frames = 4, Buff = PlayerBuffs.CobWhip, Texture = "Projectile_1036", LocalCooldown = 20, Minion = false, Tracked = false },
            [Kind.DeadlySphere] = new KindSpec { Id = 533, W = 20, H = 20, Slots = 1, Buff = PlayerBuffs.DeadlySphere, Texture = "Projectile_533", Frames = 21, LocalCooldown = 12 },
            [Kind.Sharknado] = new KindSpec { Id = 407, W = 28, H = 40, Slots = 1, Buff = PlayerBuffs.SharknadoMinion, Texture = "Projectile_407", Frames = 6, TypeCooldown = 10 },
            [Kind.UFO] = new KindSpec { Id = 423, W = 28, H = 28, Slots = 1, Buff = PlayerBuffs.UFOMinion, Texture = "Projectile_423", Frames = 4 },
            [Kind.StardustCell] = new KindSpec { Id = 613, W = 24, H = 24, Slots = 1, Buff = PlayerBuffs.StardustMinion, Texture = "Projectile_613", Frames = 4 },
            [Kind.Raven] = new KindSpec { Id = 317, W = 28, H = 28, Slots = 1, Buff = PlayerBuffs.Ravens, Texture = "Projectile_317", Frames = 8, LocalCooldown = 10 },
            [Kind.Terraprisma] = new KindSpec { Id = 946, W = 10, H = 10, Slots = 1, Buff = PlayerBuffs.EmpressBlade, Texture = "Projectile_946", OncePerNpc = true },
            [Kind.BladeStaff] = new KindSpec { Id = 864, W = 10, H = 10, Slots = 1, Buff = PlayerBuffs.Smolstar, Texture = "Projectile_864", Frames = 2, LocalCooldown = 10 },
            [Kind.TigerGem] = new KindSpec { Id = 831, W = 10, H = 10, Slots = 1, Buff = PlayerBuffs.StormTiger, Texture = "Projectile_831", Frames = 6 },
            [Kind.Tiger1] = new KindSpec { Id = 833, W = 26, H = 20, Slots = 0, Buff = PlayerBuffs.StormTiger, Texture = "Projectile_833", Frames = 10, LocalCooldown = 10, Tracked = false },
            [Kind.Tiger2] = new KindSpec { Id = 834, W = 20, H = 30, Slots = 0, Buff = PlayerBuffs.StormTiger, Texture = "Projectile_834", Frames = 12, LocalCooldown = 10, Tracked = false },
            [Kind.Tiger3] = new KindSpec { Id = 835, W = 20, H = 30, Slots = 0, Buff = PlayerBuffs.StormTiger, Texture = "Projectile_835", Frames = 12, LocalCooldown = 10, Tracked = false },
            [Kind.Pygmy1] = new KindSpec { Id = 191, W = 18, H = 18, Slots = 1, Buff = PlayerBuffs.Pygmies, Texture = "Projectile_191", Frames = 18 },
            [Kind.Pygmy2] = new KindSpec { Id = 192, W = 18, H = 18, Slots = 1, Buff = PlayerBuffs.Pygmies, Texture = "Projectile_192", Frames = 18 },
            [Kind.Pygmy3] = new KindSpec { Id = 193, W = 18, H = 18, Slots = 1, Buff = PlayerBuffs.Pygmies, Texture = "Projectile_193", Frames = 18 },
            [Kind.Pygmy4] = new KindSpec { Id = 194, W = 18, H = 18, Slots = 1, Buff = PlayerBuffs.Pygmies, Texture = "Projectile_194", Frames = 18 },
            // The Old One's Army sentries (MinionsSentries.cs): no slot or buff; the trap's blast is neither minion nor sentry.
            [Kind.Flameburst1] = new KindSpec { Id = 663, W = 30, H = 54, Frames = 7, Texture = "Projectile_663", Minion = false, Sentry = true, Tracked = false },
            [Kind.Flameburst2] = new KindSpec { Id = 665, W = 28, H = 58, Frames = 9, Texture = "Projectile_665", Minion = false, Sentry = true, Tracked = false },
            [Kind.Flameburst3] = new KindSpec { Id = 667, W = 28, H = 60, Frames = 9, Texture = "Projectile_667", Minion = false, Sentry = true, Tracked = false },
            [Kind.Ballista1] = new KindSpec { Id = 677, W = 26, H = 54, Frames = 6, Texture = "Projectile_677", Minion = false, Sentry = true, Tracked = false },
            [Kind.Ballista2] = new KindSpec { Id = 678, W = 26, H = 54, Frames = 6, Texture = "Projectile_678", Minion = false, Sentry = true, Tracked = false },
            [Kind.Ballista3] = new KindSpec { Id = 679, W = 26, H = 54, Frames = 6, Texture = "Projectile_679", Minion = false, Sentry = true, Tracked = false },
            [Kind.Trap1] = new KindSpec { Id = 691, W = 16, H = 16, Frames = 4, Texture = "Projectile_691", Minion = false, Sentry = true, Tracked = false },
            [Kind.Trap2] = new KindSpec { Id = 692, W = 16, H = 16, Frames = 4, Texture = "Projectile_692", Minion = false, Sentry = true, Tracked = false },
            [Kind.Trap3] = new KindSpec { Id = 693, W = 16, H = 16, Frames = 4, Texture = "Projectile_693", Minion = false, Sentry = true, Tracked = false },
            [Kind.TrapBlast1] = new KindSpec { Id = 694, W = 144, H = 144, Frames = 4, Texture = "Projectile_694", Minion = false, Shot = true, LocalCooldown = 30, Tracked = false },
            [Kind.TrapBlast2] = new KindSpec { Id = 695, W = 144, H = 144, Frames = 4, Texture = "Projectile_695", Minion = false, Shot = true, LocalCooldown = 30, Tracked = false },
            [Kind.TrapBlast3] = new KindSpec { Id = 696, W = 144, H = 144, Frames = 5, Texture = "Projectile_696", Minion = false, Shot = true, LocalCooldown = 30, Tracked = false },
            [Kind.Aura1] = new KindSpec { Id = 688, W = 16, H = 16, Frames = 6, Texture = "Projectile_688", Minion = false, Sentry = true, LocalCooldown = 3, Tracked = false },
            [Kind.Aura2] = new KindSpec { Id = 689, W = 16, H = 16, Frames = 6, Texture = "Projectile_689", Minion = false, Sentry = true, LocalCooldown = 3, Tracked = false },
            [Kind.Aura3] = new KindSpec { Id = 690, W = 16, H = 16, Frames = 8, Texture = "Projectile_690", Minion = false, Sentry = true, LocalCooldown = 3, Tracked = false },
            [Kind.SpiderHiver] = new KindSpec { Id = 377, W = 66, H = 50, Frames = 9, Texture = "Projectile_377", Minion = false, Sentry = true, Tracked = false },
            [Kind.SpiderEgg] = new KindSpec { Id = 378, W = 16, H = 16, Texture = "Projectile_378", Minion = false, Tracked = false },
            [Kind.BabySpider] = new KindSpec { Id = 379, W = 14, H = 10, Frames = 4, Texture = "Projectile_379", Minion = false, Shot = true, Tracked = false },
            [Kind.FrostHydra] = new KindSpec { Id = 308, W = 80, H = 74, Frames = 10, Texture = "Projectile_308", Minion = false, Sentry = true, Tracked = false },
            [Kind.Houndius] = new KindSpec { Id = 966, W = 18, H = 60, Frames = 3, Texture = "Projectile_966", Minion = false, Sentry = true, Tracked = false },
            [Kind.LunarPortal] = new KindSpec { Id = 641, W = 32, H = 32, Texture = "Projectile_641", Minion = false, Sentry = true, Tracked = false },
            [Kind.PortalLaser] = new KindSpec { Id = 642, W = 18, H = 18, Texture = "Projectile_642", Minion = false, Shot = true, LocalCooldown = 10, Tracked = false },
            [Kind.RainbowCrystal] = new KindSpec { Id = 643, W = 32, H = 32, Frames = 8, Texture = "Projectile_643", Minion = false, Sentry = true, Tracked = false },
            [Kind.CrystalBlast] = new KindSpec { Id = 644, W = 14, H = 14, Texture = "Projectile_644", Minion = false, Shot = true, Tracked = false },
            [Kind.Barnacle] = new KindSpec { Id = 1025, W = 42, H = 38, Frames = 4, Texture = "Projectile_1025", Minion = false, Sentry = true, Tracked = false },
            // The 1.4.5 summons (P:9669-9680, 10197-10480; MinionsPal.cs): the Pals and the Clay Bud each enemy every 9 / 30 ticks, the Foxsparks and the relic harmless themselves.
            [Kind.MushroomBoi] = new KindSpec { Id = 1022, W = 20, H = 30, Slots = 1, Buff = PlayerBuffs.MushroomBoiMinion, Texture = "Projectile_1022", Frames = 16, LocalCooldown = 18 },
            [Kind.Cattiva] = new KindSpec { Id = 1093, W = 14, H = 22, Slots = 1, Buff = PlayerBuffs.CattivaMinion, Texture = "Projectile_1093", Frames = 28, LocalCooldown = 9 },
            [Kind.TrustyCattiva] = new KindSpec { Id = 1112, W = 14, H = 22, Slots = 1, Buff = PlayerBuffs.TrustyCattivaMinion, Texture = "Projectile_1112", Frames = 28, LocalCooldown = 9 },
            [Kind.Foxsparks] = new KindSpec { Id = 1094, W = 18, H = 18, Slots = 1, Buff = PlayerBuffs.FoxsparksMinion, Texture = "Projectile_1094", Frames = 54 },
            [Kind.TrustyFoxsparks] = new KindSpec { Id = 1113, W = 18, H = 18, Slots = 1, Buff = PlayerBuffs.TrustyFoxsparksMinion, Texture = "Projectile_1113", Frames = 54 },
            [Kind.ClayPot] = new KindSpec { Id = 1118, W = 14, H = 22, Slots = 1, Buff = PlayerBuffs.ClayPotMinion, Texture = "Projectile_1118", Frames = 20, LocalCooldown = 30 },
            [Kind.ForbiddenRelic] = new KindSpec { Id = 1119, W = 14, H = 22, Slots = 1, Buff = PlayerBuffs.ForbiddenMinion, Texture = "Projectile_1119", Frames = 9, LocalCooldown = 30 },
            // The Wandering Soul Staff's little soul (Items/MinionsSoulWave.cs): the pack's frames, no Terraria projectile of its own.
            [Kind.WanderingSoul] = new KindSpec { Id = 0, W = 24, H = 24, Slots = 1, Buff = PlayerBuffs.WanderingSoul, Texture = SoulWave.Tex("minion"), Frames = 8, LocalCooldown = 20 },
            [Kind.ChoirSoul] = new KindSpec { Id = 0, W = 26, H = 26, Slots = 1, Buff = PlayerBuffs.ChoirSoul, Texture = Howl.Tex("choir"), Frames = 8, LocalCooldown = 20 },
            [Kind.ShadeSoul] = new KindSpec { Id = 0, W = 24, H = 24, Slots = 1, Buff = PlayerBuffs.ShadeSoul, Texture = Shade.Tex("minion"), Frames = 8, LocalCooldown = 20 },
            [Kind.DirgeSoul] = new KindSpec { Id = 0, W = 26, H = 26, Slots = 1, Buff = PlayerBuffs.DirgeSoul, Texture = Abyss.Tex("dirge"), Frames = 8, LocalCooldown = 20 },
        };

        /// <summary>One minion (a Terraria Projectile with its AI state), in Terraria px, y down.</summary>
        sealed class M
        {
            public Kind Kind;
            public KindSpec S;
            public ItemType Staff;
            /// <summary>Top-left of the hitbox, velocity per update, last update's position.</summary>
            public Vector2 Pos, Vel, OldPos;
            public int W, H;
            public float Ai0, Ai1, Ai2, LocalAi0, LocalAi1, LocalAi2;
            /// <summary>Projectile.soundDelay (the Pals' calls count it down from 0).</summary>
            public int SoundDelay;
            /// <summary>The Pals' petting timer (localAI[0], the Foxsparks' localAI[2]): 60 when a pat starts.</summary>
            public int PetJoy;
            public int Frame, FrameCounter;
            public float Rot;
            public int Dir = 1, SpriteDir = 1;
            public bool Friendly, TileCollide = true, FallThrough;
            public float GfxOffY, StepSpeed = 1f;
            public int OriginalDamage, Damage;
            public float Knockback;
            public int MinionPos, TimeLeft = 18000, Extra, Serial;
            public bool Dead;
            public readonly Dictionary<int, int> Immune = new Dictionary<int, int>();
            public bool Latched, HasTarget;
            public Vector2 ToTarget;
            /// <summary>A sentry carried by the sentry sling (ai[2] = 1): on the player's back for good.</summary>
            public bool Carried;
            /// <summary>oldPos / oldRot / oldSpriteDirection, newest first (the twins' and the bat's trails).</summary>
            public readonly Vector2[] OldPosRing = new Vector2[20];
            public readonly float[] OldRot = new float[20];
            public readonly int[] OldSpriteDir = new int[20];
            /// <summary>The dragon's link: the segment it follows (ai[0]).</summary>
            public M Leader;
            public float Alpha, Scale = 1f;
            /// <summary>The bat's prey and the snowflake's (by enemy id), and the blast's spared enemy.</summary>
            public int TargetId = -1;
            public Vector2 Start;
            /// <summary>The summoning weapon's prefix armour penetration and tag damage (Projectile.ApplyStatsFromSource).</summary>
            public int ItemPen, ItemTag;
            /// <summary>A pet's projectile (MinionsPets.cs).</summary>
            public PetProj Pet;

            public Vector2 Center
            {
                get => Pos + new Vector2(W, H) * 0.5f;
                set => Pos = value - new Vector2(W, H) * 0.5f;
            }
            public float Bottom => Pos.y + H;
            public Rect Box => new Rect(Pos.x, Pos.y, W, H);
        }

        /// <summary>An enemy the minions may chase (NPC.CanBeChasedBy), its hitbox in px.</summary>
        sealed class Npc
        {
            public HealthManager Hm;
            public Collider2D Col;
            public Rect Box;
            public Vector2 Center;
            public bool Boss;
            public int Id;
        }

        /// <summary>The player's minions (swapped for another player's ghosts while those are drawn).</summary>
        List<M> live = new List<M>();
        readonly List<Npc> npcs = new List<Npc>();
        readonly Dictionary<int, Npc> npcById = new Dictionary<int, Npc>();
        /// <summary>perIDStaticNPCImmunity: an enemy immune to a whole minion type for a while.</summary>
        readonly Dictionary<(Kind, int), int> typeImmune = new Dictionary<(Kind, int), int>();
        readonly List<(Kind, int)> typeKeys = new List<(Kind, int)>();
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        readonly List<int> immuneKeys = new List<int>();
        float tickAccumulator;
        int serial;
        /// <summary>Player.numMinions / slotsMinions, counted again each tick.</summary>
        int numMinions;
        /// <summary>How many minions the player has out (the dragon's head and tail not counted).</summary>
        internal int MinionCount => numMinions;
        float slotsMinions;
        int nextSpider;

        // The owner, refreshed each tick (Terraria px, y down).
        bool hasOwner, ownerDead, ownerGrounded, wasDead;
        Vector2 pCenter, pVel;
        Rect pBox;
        int pDir = 1;

        /// <summary>Player.MinionAttackTargetNPC: the enemy the minions go for first (none = null).</summary>
        public HealthManager Target { get; private set; }

        /// <summary>The minions to call back after a death (MinionRespawner): one staff per tracked minion.</summary>
        readonly List<ItemType> respawn = new List<ItemType>();

        public static Minions Create(Transform parent)
        {
            var go = new GameObject("Terra Minions");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<Minions>();
            return Instance;
        }

        public int Count => live.Count;

        // ------------------------------------------------------------------ queries

        /// <summary>ownedProjectileCounts for a minion buff: whether any of its minions lives.</summary>
        public bool Owns(int buff)
        {
            foreach (var m in live)
            {
                if (m.Dead || m.S.Buff != buff) continue;
                // The dragon buff counts the head; the Abigail buff the counters; the twins either.
                if (buff == PlayerBuffs.StardustDragonMinion && m.Kind != Kind.DragonHead) continue;
                if (buff == PlayerBuffs.AbigailMinion && m.Kind != Kind.AbigailCounter) continue;
                if (buff == PlayerBuffs.StormTiger && m.Kind != Kind.TigerGem) continue;
                return true;
            }
            return false;
        }

        /// <summary>The buff icon's count (BuffTextHandlers): slimes, spiders, Retanimini only, dragon bodies, counters…</summary>
        public int BuffCount(int buff)
        {
            int n = 0;
            foreach (var m in live)
            {
                if (m.Dead || m.S.Buff != buff) continue;
                if (buff == PlayerBuffs.TwinEyesMinion && m.Kind != Kind.Retanimini) continue;
                if (buff == PlayerBuffs.StardustDragonMinion && m.Kind != Kind.DragonBody1) continue;
                if (buff == PlayerBuffs.AbigailMinion && m.Kind != Kind.AbigailCounter) continue;
                if (buff == PlayerBuffs.StormTiger && m.Kind != Kind.TigerGem) continue;
                n++;
            }
            return n;
        }

        /// <summary>Slots in use (slotsMinions) as of the last tick.</summary>
        public float SlotsUsed => slotsMinions;

        /// <summary>Dev: how many of a kind live.</summary>
        public int DevCount(Kind kind)
        {
            int n = 0;
            foreach (var m in live) if (!m.Dead && m.Kind == kind) n++;
            return n;
        }

        /// <summary>Dev: every live minion, briefly (kind, centre in world units, state).</summary>
        public string DevDescribe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in live)
            {
                if (m.Dead || !(m.S.Minion || m.S.Sentry)) continue;
                if (sb.Length > 0) sb.Append("；");
                sb.Append($"{m.Kind}#{m.MinionPos} {TerraParticles.ToWorld(m.Center):F1} ai0={m.Ai0:F0} 伤害{m.Damage}");
            }
            return sb.Length == 0 ? "无" : sb.ToString();
        }

        /// <summary>Dev: where the first live minion of a kind is (world), if any.</summary>
        public Vector2? DevWhere(Kind kind)
        {
            foreach (var m in live) if (!m.Dead && m.Kind == kind) return TerraParticles.ToWorld(m.Center);
            return null;
        }

        /// <summary>Dev: the damage the first minion of a kind has now (after the per-tick refresh).</summary>
        public int DevDamage(Kind kind)
        {
            foreach (var m in live) if (!m.Dead && m.Kind == kind) return m.Damage;
            return 0;
        }

        int GrowthDamage(M m) => m.Kind == Kind.Abigail
            ? MinionGrowth.Abigail(m.Damage, DevCount(Kind.AbigailCounter), Shop.UnlockedStage() >= 4)
            : IsTiger(m.Kind) ? MinionGrowth.Tiger(m.Damage, TigerGems()) : m.Damage;

        public int DevContactDamage(Kind kind)
        {
            foreach (var m in live) if (!m.Dead && m.Kind == kind) return GrowthDamage(m);
            return 0;
        }

        public int DevSerial(Kind kind)
        {
            foreach (var m in live) if (!m.Dead && m.Kind == kind) return m.Serial;
            return -1;
        }

        /// <summary>Dev: hits dealt by minions (and their shots) so far, and their total damage before variance.</summary>
        public static int DevHits, DevHitDamage;
        /// <summary>Dev: the biggest single minion hit (before variance) and the Firecracker blasts so far.</summary>
        public static int DevMaxHit, DevBlasts;

        // ------------------------------------------------------------------ the tick

        void Update()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm != null && !gm.isPaused)
            {
                tickAccumulator += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
                while (tickAccumulator >= 1f)
                {
                    tickAccumulator -= 1f;
                    Tick();
                    TickGhosts();
                }
            }
            Draw();
        }

        void Tick()
        {
            RefreshOwner();
            if (!hasOwner) return;
            FindNpcs();
            UpdateTarget();
            Respawner();
            PlayerBuffs.Tick(ownerDead);
            SummonTags.Tick();
            StepMouseTextColor();
            if (typeImmune.Count > 0)
            {
                typeKeys.Clear();
                typeKeys.AddRange(typeImmune.Keys);
                foreach (var k in typeKeys) if (--typeImmune[k] <= 0) typeImmune.Remove(k);
            }
            // Update_SentryBackpack (Pl:28638-28644).
            if (slingCooldown > 0) slingCooldown--;
            numMinions = 0;
            slotsMinions = 0f;
            for (int i = 0; i < live.Count; i++)
            {
                var m = live[i];
                if (m.Dead) continue;
                // DecrementLocalImmuneTimeCounters, once a tick.
                if (m.Immune.Count > 0)
                {
                    immuneKeys.Clear();
                    immuneKeys.AddRange(m.Immune.Keys);
                    foreach (var k in immuneKeys)
                        if (m.Immune[k] > 0 && --m.Immune[k] <= 0) m.Immune.Remove(k);
                }
                // Projectile.Update (once a tick): summon damage follows the gear, and minionPos and
                // the slot cap are counted (the dragon's head and tail aside); past the cap it dies.
                if (m.S.Minion || m.S.Sentry) m.Damage = (int)(m.OriginalDamage * (1f + Equipment.Current.MinionDamage) * CharmEffects.MinionScale + 5e-6f);
                if (m.S.Minion)
                {
                    if (m.Kind != Kind.DragonHead && m.Kind != Kind.DragonTail)
                    {
                        m.MinionPos = numMinions;
                        if (slotsMinions + m.S.Slots > Equipment.MaxMinions)
                        {
                            if (m.Kind == Kind.DragonBody1 || m.Kind == Kind.DragonBody2) Unlink(m);
                            Kill(m);
                            continue;
                        }
                        numMinions++;
                        slotsMinions += m.S.Slots;
                    }
                }
                for (int u = 0; u <= m.Extra && !m.Dead; u++) Step(m);
            }
            SnapDragons();
            PetTick();
            PetsTick();
            UpdateAbigailStatus();
            UpdateStormTigerStatus();
            // UpdateMaxTurrets also runs when maxTurrets changes (Pl:25904-25908): gear taken off drops the extra sentries.
            if (Equipment.MaxTurrets != lastMaxTurrets)
            {
                lastMaxTurrets = Equipment.MaxTurrets;
                UpdateMaxTurrets();
            }
            discoTick++;
            miscCounter = (miscCounter + 1) % 300;
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].Dead) live.RemoveAt(i);
        }

        void RefreshOwner()
        {
            var hero = HeroController.SilentInstance;
            hasOwner = hero != null && TerrainSystem.Current != null;
            if (!hasOwner) return;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var b = col.bounds;
            pBox = new Rect(b.min.x / Px, -b.max.y / Px, b.size.x / Px, b.size.y / Px);
            pCenter = pBox.center;
            pVel = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Px / 60f : Vector2.zero;
            var mod = TerraMod.Instance;
            pDir = mod != null && mod.IsUsingItem ? (mod.UseDirection < 0f ? -1 : 1) : hero.cState.facingRight ? 1 : -1;
            ownerGrounded = hero.cState.onGround;
            ownerDead = hero.cState.dead;
        }

        /// <summary>
        /// MinionRespawner: on death the tracked minions' staves are noted; on respawn each one in
        /// the inventory gives its buff and summons again, silently, at the player.
        /// </summary>
        void Respawner()
        {
            if (ownerDead && !wasDead)
            {
                respawn.Clear();
                foreach (var m in live)
                    if (!m.Dead && m.S.Minion && m.S.Tracked && m.Staff != ItemType.None) respawn.Add(m.Staff);
                Target = null;
            }
            else if (!ownerDead && wasDead && respawn.Count > 0)
            {
                var inv = TerraMod.Instance?.Inventory;
                foreach (var t in respawn)
                {
                    if (inv == null || !InInventory(inv, t)) continue;
                    var def = ItemDb.Get(t);
                    PlayerBuffs.Add(def.MinionBuff, 3600);
                    Shoot(def, TerraParticles.ToWorld(pCenter));
                }
                respawn.Clear();
            }
            wasDead = ownerDead;
        }

        static bool InInventory(Inventory inv, ItemType t)
        {
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) return true;
            return false;
        }

        /// <summary>The room the minions were last set down in (OnSceneReady).</summary>
        string readyScene;
        public static int DevSameRoomReady;

        /// <summary>
        /// A new room: the minions come along, set down on the player; the sentries stay where they stood (gone). The game
        /// says it has finished entering the scene again after a hazard's respawn (spikes, acid, a pit) and a respawn in
        /// the same room: nothing is taken away then, the sentries stand and the minions fly on.
        /// </summary>
        public void OnSceneReady()
        {
            RefreshOwner();
            Target = null;
            if (!hasOwner) return;
            string scene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
            if (scene != null && scene == readyScene) { DevSameRoomReady++; return; }
            readyScene = scene;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var m = live[i];
                if (m.Kind == Kind.FireBlast || m.S.Sentry || m.S.Shot || m.Kind == Kind.SpiderEgg)
                {
                    live.RemoveAt(i);
                    continue;
                }
                m.Center = pCenter;
                m.Vel = Vector2.zero;
                m.OldPos = m.Pos;
                for (int k = 0; k < m.OldPosRing.Length; k++) m.OldPosRing[k] = m.Pos;
                // The sphere keeps its form; nothing else keeps its state (bar a dashing Spazmamini).
                if (m.Kind == Kind.DeadlySphere)
                {
                    m.Ai0 = (int)m.Ai0 % 3;
                    m.Ai1 = 0f;
                    m.Extra = 0;
                }
                else if (m.Kind != Kind.Spazmamini || m.Ai0 != 2f) m.Ai0 = 0f;
                m.Immune.Clear();
            }
            typeImmune.Clear();
        }

        public void Clear()
        {
            readyScene = null;
            live.Clear();
            typeImmune.Clear();
            respawn.Clear();
            Target = null;
        }

        // ------------------------------------------------------------------ one update of one minion

        void Step(M m)
        {
            // gfxOffY eases back to 0 after a step up.
            if (m.GfxOffY != 0f)
            {
                float k = (1f + Mathf.Abs(m.Vel.x) / 3f) * m.StepSpeed;
                m.GfxOffY = m.GfxOffY > 0f ? Mathf.Max(0f, m.GfxOffY - k) : Mathf.Min(0f, m.GfxOffY + k);
                m.GfxOffY = Mathf.Clamp(m.GfxOffY, -16f, 16f);
            }
            switch (m.Kind)
            {
                case Kind.BabySlime:
                case Kind.VenomSpider:
                case Kind.JumperSpider:
                case Kind.DangerousSpider:
                case Kind.Pygmy1:
                case Kind.Pygmy2:
                case Kind.Pygmy3:
                case Kind.Pygmy4:
                    AI026(m);
                    break;
                case Kind.OneEyedPirate:
                case Kind.SoulscourgePirate:
                case Kind.PirateCaptain:
                case Kind.VampireFrog:
                case Kind.Flinx:
                case Kind.CobSpider:
                    AI067(m);
                    break;
                case Kind.Hornet:
                case Kind.Imp:
                case Kind.Abigail:
                    AI062(m);
                    break;
                case Kind.AbigailCounter:
                    AI164(m);
                    break;
                case Kind.Retanimini:
                case Kind.Spazmamini:
                    AI066(m);
                    break;
                case Kind.Bat:
                    AI156(m);
                    break;
                case Kind.Finch:
                    AI158(m);
                    break;
                case Kind.DragonHead:
                case Kind.DragonBody1:
                case Kind.DragonBody2:
                case Kind.DragonTail:
                    AI121(m);
                    break;
                case Kind.Snowflake:
                    AI177(m);
                    break;
                case Kind.FireBlast:
                    AI178(m);
                    break;
                case Kind.DeadlySphere:
                    AI533(m);
                    break;
                case Kind.Sharknado:
                case Kind.UFO:
                case Kind.StardustCell:
                    AI062b(m);
                    break;
                case Kind.Raven:
                    AI054(m);
                    break;
                case Kind.Terraprisma:
                    AI156b(m);
                    break;
                case Kind.TigerGem:
                    AITigerGem(m);
                    break;
                case Kind.BladeStaff:
                    AI169(m);
                    break;
                case Kind.Tiger1:
                case Kind.Tiger2:
                case Kind.Tiger3:
                    AITiger(m);
                    break;
                case Kind.Flameburst1:
                case Kind.Flameburst2:
                case Kind.Flameburst3:
                    AI130(m);
                    break;
                case Kind.Ballista1:
                case Kind.Ballista2:
                case Kind.Ballista3:
                    AI134(m);
                    break;
                case Kind.Trap1:
                case Kind.Trap2:
                case Kind.Trap3:
                    AI138(m);
                    break;
                case Kind.TrapBlast1:
                case Kind.TrapBlast2:
                case Kind.TrapBlast3:
                    AI139(m);
                    break;
                case Kind.Aura1:
                case Kind.Aura2:
                case Kind.Aura3:
                    AI137(m);
                    break;
                case Kind.SpiderHiver:
                case Kind.FrostHydra:
                case Kind.Houndius:
                    AI053(m);
                    break;
                case Kind.SpiderEgg:
                    AISpiderEgg(m);
                    break;
                case Kind.BabySpider:
                    AI063(m);
                    break;
                case Kind.LunarPortal:
                case Kind.RainbowCrystal:
                    AI123(m);
                    break;
                case Kind.PortalLaser:
                    AIPortalLaser(m);
                    break;
                case Kind.CrystalBlast:
                    AICrystalBlast(m);
                    break;
                case Kind.Barnacle:
                    AI197(m);
                    break;
                case Kind.MushroomBoi:
                case Kind.Cattiva:
                case Kind.TrustyCattiva:
                case Kind.ClayPot:
                    if (petted == m) HoldForPat(m);
                    else AI067Pal(m);
                    break;
                case Kind.Foxsparks:
                case Kind.TrustyFoxsparks:
                    if (petted == m) HoldForPat(m);
                    else AI026Fox(m);
                    break;
                case Kind.ForbiddenRelic:
                    AI206(m);
                    break;
                case Kind.WanderingSoul:
                    AISoul(m);
                    break;
                case Kind.ChoirSoul:
                    AIChoir(m);
                    break;
                case Kind.ShadeSoul:
                    AIShade(m);
                    break;
                case Kind.DirgeSoul:
                    AIDirge(m);
                    break;
            }
            if (m.Dead) return;
            if (m.S.Sentry) Sling(m);
            if (m.Dead) return;
            // HandleMovement.
            m.OldPos = m.Pos;
            if (m.TileCollide && m.Kind == Kind.DeadlySphere)
            {
                // The sphere meets blocks with a 26 px box while dashing, and bounces off them.
                var before = m.Vel;
                var centre = m.Center;
                int grow = m.Ai0 >= 6f ? 6 : 0;
                m.W += grow;
                m.H += grow;
                m.Center = centre;
                TileCollision(m, true);
                centre = m.Center;
                m.W -= grow;
                m.H -= grow;
                m.Center = centre;
                BounceSphere(m, before);
            }
            else if (m.TileCollide && m.Kind == Kind.Raven)
            {
                var before = m.Vel;
                TileCollision(m, true);
                BounceRaven(m, before);
            }
            else if (m.TileCollide && (IsDD2(m.Kind) || m.Kind == Kind.FrostHydra)) SentryCollision(m);
            else if (m.TileCollide && (m.Kind == Kind.SpiderEgg || m.Kind == Kind.BabySpider))
            {
                var before = m.Vel;
                TileCollision(m, m.FallThrough);
                BounceSpiders(m, before);
            }
            else if (m.TileCollide && m.Kind == Kind.Barnacle)
            {
                var before = m.Vel;
                TileCollision(m, true);
                if (m.Ai0 == 0f && (m.Vel.x != before.x || m.Vel.y != before.y)) BarnacleAttach(m);
            }
            else if (m.TileCollide) TileCollision(m, m.FallThrough);
            else m.Pos += m.Vel;
            // AutomaticallyChangesDirection: every flying AI here faces its motion (aiStyle 26 and 67 don't, nor the sentries and their shots).
            if (!IsGround(m.Kind) && !m.S.Sentry && !m.S.Shot && m.Kind != Kind.DragonBody1 && m.Kind != Kind.DragonBody2 && m.Kind != Kind.DragonTail)
                m.Dir = m.Vel.x < 0f ? -1 : 1;
            else if (m.Kind == Kind.DragonBody1 || m.Kind == Kind.DragonBody2 || m.Kind == Kind.DragonTail) m.Dir = 1;
            Damage(m);
            // The trail cache, newest first.
            for (int k = m.OldPosRing.Length - 1; k > 0; k--)
            {
                m.OldPosRing[k] = m.OldPosRing[k - 1];
                m.OldRot[k] = m.OldRot[k - 1];
                m.OldSpriteDir[k] = m.OldSpriteDir[k - 1];
            }
            m.OldPosRing[0] = m.Pos;
            m.OldRot[0] = m.Rot;
            m.OldSpriteDir[0] = m.SpriteDir;
            if (--m.TimeLeft <= 0) Kill(m);
        }

        static bool IsGround(Kind k) => k <= Kind.Flinx || k == Kind.CobSpider || IsTiger(k) || IsPygmy(k);
        static bool IsSpider(Kind k) => k == Kind.VenomSpider || k == Kind.JumperSpider || k == Kind.DangerousSpider;
        static bool IsPygmy(Kind k) => k >= Kind.Pygmy1 && k <= Kind.Pygmy4;
        static bool IsPirate(Kind k) => k == Kind.OneEyedPirate || k == Kind.SoulscourgePirate || k == Kind.PirateCaptain;
        static bool IsDragon(Kind k) => k >= Kind.DragonHead && k <= Kind.DragonTail;
        static bool IsTwin(Kind k) => k == Kind.Retanimini || k == Kind.Spazmamini;

        void Kill(M m)
        {
            if (m.Dead) return;
            m.Dead = true;
            SentryDeath(m);
            if (IsDragon(m.Kind))
            {
                // Kill: six star dusts.
                for (int i = 0; i < 6; i++)
                {
                    int d = DustBox(m, 135, Vector2.zero, 100, 2f);
                    if (d >= 0) TerraParticles.Instance[d].NoGravity = true;
                }
            }
        }

        // ------------------------------------------------------------------ summoning

        /// <summary>
        /// A staff's left-click use (ItemCheck_ApplyPetBuffs, FreeUpPetsAndMinions, ItemCheck_Shoot):
        /// its buff for 3600 ticks, slots freed, and the minion at the cursor (kept inside the
        /// 1920×1200 box round the player).
        /// </summary>
        public bool Summon(ItemDef staff, Vector2 aimWorld)
        {
            RefreshOwner();
            if (!hasOwner) return false;
            if (staff.Sentry)
            {
                // A sentry staff (ItemCheck_CheckCanUse_Inner, FindSentryRestingSpot): no buff or slot;
                // only onto ground under the cursor with room above it; then UpdateMaxTurrets.
                if (!IsDD2Staff(staff.Minion))
                {
                    // The other staves (PickSentryLandingSpot): at the raw cursor, never refused.
                    SpawnLandingSentry(staff, TerraParticles.ToPx(aimWorld));
                    UpdateMaxTurrets();
                    return true;
                }
                if (!SentrySpot(ClampToReach(TerraParticles.ToPx(aimWorld)), out var ground)) return false;
                // The aura also needs three free tiles over the ground (Collision.SolidTiles, Pl:52741).
                bool aura = staff.Minion == MinionKind.Aura1 || staff.Minion == MinionKind.Aura2 || staff.Minion == MinionKind.Aura3;
                if (aura && SolidBox(ground.x - 7f, ground.y - 47f, 14f, 46f)) return false;
                SpawnSentry(staff, ground);
                UpdateMaxTurrets();
                return true;
            }
            PlayerBuffs.Add(staff.MinionBuff, 3600);
            // Pl:43103: a Foxsparks staff takes up one already out to breathe fire rather than summoning another.
            if ((staff.Minion == MinionKind.Foxsparks || staff.Minion == MinionKind.TrustyFoxsparks) && TryFoxsparksAbility(staff)) return true;
            FreeUp();
            Shoot(staff, aimWorld);
            return true;
        }

        /// <summary>
        /// FindSentryRestingSpot: straight down from a point (at most 1200 px) to the first row where
        /// the tile or one beside it is solid or a platform; the spot is that row's top under the
        /// point. False if there is none, or the tile over the spot is solid (the item can't be used).
        /// </summary>
        bool SentrySpot(Vector2 at, out Vector2 ground)
        {
            int tx = TileOf(at.x), ty = TileOf(at.y);
            for (int j = ty; j < ty + 75; j++)
            {
                if (!GroundTile(tx - 1, j) && !GroundTile(tx, j) && !GroundTile(tx + 1, j)) continue;
                ground = new Vector2(tx * 16f + 8f, j * 16f);
                return !SolidBox(ground.x - 7f, ground.y - 15f, 14f, 14f);
            }
            ground = at;
            return false;
        }

        /// <summary>WorldGen.SolidTile2: a block (or one of the scene's own solid colliders) or a platform.</summary>
        static bool GroundTile(int i, int j) => PlatformTile(i, j) || SolidBox(i * 16f, j * 16f, 16f, 16f);

        /// <summary>UpdateMaxTurrets: past maxTurrets, the own sentry with the least time left goes, one at a time.</summary>
        void UpdateMaxTurrets()
        {
            while (true)
            {
                int n = 0;
                M least = null;
                foreach (var m in live)
                {
                    if (m.Dead || !m.S.Sentry) continue;
                    n++;
                    if (least == null || m.TimeLeft < least.TimeLeft) least = m;
                }
                if (n <= Equipment.MaxTurrets || least == null) return;
                Kill(least);
            }
        }

        /// <summary>Dev: how many sentries stand.</summary>
        public int DevSentries()
        {
            int n = 0;
            foreach (var m in live) if (!m.Dead && m.S.Sentry) n++;
            return n;
        }

        /// <summary>ItemCheck_Shoot for a staff: what it summons, where.</summary>
        void Shoot(ItemDef staff, Vector2 aimWorld)
        {
            var at = ClampToReach(TerraParticles.ToPx(aimWorld));
            float kb = Equipment.Knockback(staff);
            int dmg = staff.Damage;
            switch (staff.Minion)
            {
                case MinionKind.BabySlime: Spawn(Kind.BabySlime, staff, at, dmg, kb); break;
                case MinionKind.Flinx: Spawn(Kind.Flinx, staff, at, dmg, kb); break;
                case MinionKind.VampireFrog: Spawn(Kind.VampireFrog, staff, at, dmg, kb); break;
                case MinionKind.Spider:
                    // The spiders come in turn: Venom, Jumper, Dangerous.
                    Spawn((Kind)((int)Kind.VenomSpider + nextSpider), staff, at, dmg, kb);
                    nextSpider = (nextSpider + 1) % 3;
                    break;
                case MinionKind.Pirate: Spawn((Kind)((int)Kind.OneEyedPirate + Random.Range(0, 3)), staff, at, dmg, kb); break;
                case MinionKind.BabyBird: Spawn(Kind.Finch, staff, at, dmg, kb); break;
                case MinionKind.Hornet: Spawn(Kind.Hornet, staff, at, dmg, kb).Ai1 = 1f; break;
                case MinionKind.Imp: Spawn(Kind.Imp, staff, at, dmg, kb).Ai1 = 1f; break;
                case MinionKind.Abigail:
                {
                    var c = Spawn(Kind.AbigailCounter, staff, at, dmg, kb);
                    c.TileCollide = false;
                    break;
                }
                case MinionKind.Twins:
                    Spawn(Kind.Retanimini, staff, at, dmg, kb).Ai1 = 1f;
                    Spawn(Kind.Spazmamini, staff, at, dmg, kb).Ai1 = 1f;
                    break;
                case MinionKind.Sanguine:
                {
                    var bat = Spawn(Kind.Bat, staff, at, dmg, kb);
                    bat.TileCollide = false;
                    break;
                }
                case MinionKind.StardustDragon: SummonDragon(staff, TerraParticles.ToPx(aimWorld), dmg, kb); break;
                case MinionKind.DeadlySphere: Spawn(Kind.DeadlySphere, staff, at, dmg, kb); break;
                case MinionKind.Sharknado: Spawn(Kind.Sharknado, staff, at, dmg, kb).Alpha = 1f; break;
                case MinionKind.UFO: Spawn(Kind.UFO, staff, at, dmg, kb).Ai1 = 1f; break;
                case MinionKind.StardustCell: Spawn(Kind.StardustCell, staff, at, dmg, kb).Ai1 = 1f; break;
                case MinionKind.Raven: Spawn(Kind.Raven, staff, at, dmg, kb); break;
                case MinionKind.Terraprisma: Spawn(Kind.Terraprisma, staff, at, dmg, kb).TileCollide = false; break;
                case MinionKind.BladeStaff: Spawn(Kind.BladeStaff, staff, at, dmg, kb).TileCollide = false; break;
                case MinionKind.DesertTiger: Spawn(Kind.TigerGem, staff, at, dmg, kb).TileCollide = false; break;
                case MinionKind.Pygmy:
                {
                    // 191-194 at random, each a little bigger.
                    int n = Random.Range(0, 4);
                    Spawn((Kind)((int)Kind.Pygmy1 + n), staff, at, dmg, kb).Scale = 1f + 0.025f * n;
                    break;
                }
                // The 1.4.5 summons (Pl:48540, 48929-48943, 49960, 50113): at the cursor, the Pals fading in, the Foxsparks shy of using
                // its breath for 30 ticks, the relic 45 ticks from its first shot; the Clay Bud from the player at 10 px a tick.
                case MinionKind.MushroomBoi: Spawn(Kind.MushroomBoi, staff, at, dmg, kb); break;
                case MinionKind.Cattiva: Spawn(Kind.Cattiva, staff, at, dmg, kb).Alpha = 1f; break;
                case MinionKind.TrustyCattiva: Spawn(Kind.TrustyCattiva, staff, at, dmg, kb).Alpha = 1f; break;
                case MinionKind.Foxsparks:
                case MinionKind.TrustyFoxsparks:
                {
                    var fox = Spawn(staff.Minion == MinionKind.Foxsparks ? Kind.Foxsparks : Kind.TrustyFoxsparks, staff, at, dmg, kb);
                    fox.Alpha = 1f;
                    fox.LocalAi0 = 30f;
                    break;
                }
                case MinionKind.ClayPot:
                {
                    var dir = TerraParticles.ToPx(aimWorld) - pCenter;
                    var pot = Spawn(Kind.ClayPot, staff, pCenter, dmg, kb);
                    pot.Vel = (dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(pDir, 0f)) * 10f;
                    break;
                }
                case MinionKind.ForbiddenRelic:
                {
                    var relic = Spawn(Kind.ForbiddenRelic, staff, at, dmg, kb);
                    relic.Ai0 = 45f;
                    relic.TileCollide = false;
                    break;
                }
                case MinionKind.WanderingSoul:
                {
                    var soul = Spawn(Kind.WanderingSoul, staff, at, dmg, kb);
                    soul.TileCollide = false;
                    SoulAppears(soul);
                    break;
                }
                case MinionKind.ChoirSoul:
                {
                    var choir = Spawn(Kind.ChoirSoul, staff, at, dmg, kb);
                    choir.TileCollide = false;
                    // Several take turns: each one's first rest a little longer than the one before.
                    choir.Ai1 = 15f * DevCount(Kind.ChoirSoul);
                    ChoirAppears(choir);
                    break;
                }
                case MinionKind.ShadeSoul:
                {
                    var shade = Spawn(Kind.ShadeSoul, staff, at, dmg, kb);
                    shade.TileCollide = false;
                    shade.TargetId = 0;
                    // Several take turns: each one's first rest a little longer than the one before.
                    shade.Ai1 = 12f * DevCount(Kind.ShadeSoul);
                    ShadeAppears(shade);
                    break;
                }
                case MinionKind.DirgeSoul:
                {
                    var dirge = Spawn(Kind.DirgeSoul, staff, at, dmg, kb);
                    dirge.TileCollide = false;
                    // Several take turns: each one's first rest a little longer than the one before.
                    dirge.Ai1 = 18f * DevCount(Kind.DirgeSoul);
                    DirgeAppears(dirge);
                    break;
                }
            }
        }

        M Spawn(Kind kind, ItemDef staff, Vector2 centerPx, int damage, float knockback)
        {
            var s = specs[kind];
            // Minions and sentries knock nothing back: their own hits, their shots and their children's (all take this).
            var m = new M
            {
                Kind = kind, S = s, Staff = staff != null ? staff.Type : ItemType.None, W = s.W, H = s.H,
                OriginalDamage = damage, Damage = damage, Knockback = 0f, Serial = serial++,
                ItemPen = staff != null ? staff.PrefixArmorPen : 0, ItemTag = staff != null ? staff.PrefixTagDamage : 0,
            };
            m.Center = centerPx;
            m.OldPos = m.Pos;
            for (int k = 0; k < m.OldPosRing.Length; k++) m.OldPosRing[k] = m.Pos;
            m.Damage = (int)(damage * (1f + Equipment.Current.MinionDamage) * CharmEffects.MinionScale + 5e-6f);
            if (s.Sentry) m.TimeLeft = 36000;
            live.Add(m);
            return m;
        }

        /// <summary>LimitPointToPlayerReachableArea: the cursor's offset scaled down to fit a 1920×1200 box round the player.</summary>
        Vector2 ClampToReach(Vector2 p)
        {
            var off = p - pCenter;
            float k = 1f;
            if (Mathf.Abs(off.x) > 960f) k = Mathf.Min(k, 960f / Mathf.Abs(off.x));
            if (Mathf.Abs(off.y) > 600f) k = Mathf.Min(k, 600f / Mathf.Abs(off.y));
            return pCenter + off * k;
        }

        /// <summary>
        /// FreeUpPetsAndMinions: all own minions by slot cost (equal ones oldest first) die until
        /// the new one fits; the dragon's head and tail never, only the body pair before its tail;
        /// the twins in pairs.
        /// </summary>
        void FreeUp()
        {
            var list = new List<M>();
            float total = 0f;
            foreach (var m in live)
            {
                if (m.Dead || !m.S.Minion) continue;
                int at = list.FindIndex(o => o.S.Slots > m.S.Slots);
                if (at < 0) list.Add(m);
                else list.Insert(at, m);
                total += m.S.Slots;
            }
            const float need = 1f;
            // Only the dragon's last pair (the 627 before the tail and its 626) can go, last.
            M tail = list.Find(o => o.Kind == Kind.DragonTail);
            list.RemoveAll(o => o.Kind == Kind.DragonBody1 || o.Kind == Kind.DragonBody2);
            if (tail != null && tail.Leader != null && tail.Leader.Kind == Kind.DragonBody2)
            {
                list.Add(tail.Leader);
                if (tail.Leader.Leader != null) list.Add(tail.Leader.Leader);
            }
            float killed = 0f;
            var twinSkip = Kind.Spazmamini;
            foreach (var m in list)
            {
                if (!(total - killed > Equipment.MaxMinions - need)) break;
                if (m.S.Slots <= 0f || m.Kind == twinSkip || m.Kind == Kind.DragonHead || m.Kind == Kind.DragonTail) continue;
                if (m.Kind == Kind.Spazmamini && twinSkip == Kind.Retanimini) twinSkip = Kind.Spazmamini;
                if (m.Kind == Kind.Retanimini && twinSkip == Kind.Spazmamini) twinSkip = Kind.Retanimini;
                killed += m.S.Slots;
                if (m.Kind == Kind.DragonBody1 || m.Kind == Kind.DragonBody2) Unlink(m);
                Kill(m);
            }
        }

        // ------------------------------------------------------------------ the owner's target

        /// <summary>
        /// MinionNPCTargetAim (a right click with a staff): the enemy whose hitbox is nearest the
        /// mouse, anywhere; the current one again clears it.
        /// </summary>
        public void AimTarget(Vector2 mouseWorld)
        {
            RefreshOwner();
            if (!hasOwner) return;
            MinionEngage.Engage("指定目标");
            FindNpcs();
            var m = TerraParticles.ToPx(mouseWorld);
            Npc best = null;
            float bestD = float.MaxValue;
            foreach (var n in npcs)
            {
                float dx = Mathf.Max(n.Box.xMin - m.x, 0f, m.x - n.Box.xMax);
                float dy = Mathf.Max(n.Box.yMin - m.y, 0f, m.y - n.Box.yMax);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < bestD)
                {
                    bestD = d;
                    best = n;
                }
            }
            if (best == null) return;
            Target = Target == best.Hm ? null : best.Hm;
        }

        /// <summary>A whip strike makes its enemy the minions' target.</summary>
        public void SetTarget(HealthManager hm)
        {
            if (hm == null || hm.isDead) return;
            MinionEngage.Engage("鞭子");
            Target = hm;
        }

        /// <summary>UpdateMinionTarget: gone, unchaseable or over 3000 px from the player, it's dropped.</summary>
        void UpdateTarget()
        {
            if (Target == null) return;
            if (!npcById.TryGetValue(Target.GetInstanceID(), out var n))
            {
                Target = null;
                return;
            }
            float dx = Mathf.Max(n.Box.xMin - pCenter.x, 0f, pCenter.x - n.Box.xMax);
            float dy = Mathf.Max(n.Box.yMin - pCenter.y, 0f, pCenter.y - n.Box.yMax);
            if (dx * dx + dy * dy > 3000f * 3000f) Target = null;
        }

        /// <summary>OwnerMinionAttackTargetNPC as an enemy record, when chaseable.</summary>
        Npc OwnerTarget => Target != null && npcById.TryGetValue(Target.GetInstanceID(), out var n) ? n : null;

        // ------------------------------------------------------------------ enemies and sight

        static readonly ContactFilter2D enemyFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = 1 << 11 };

        /// <summary>Every chaseable enemy near the player: alive, active, with more than 5 Terraria life.</summary>
        void FindNpcs()
        {
            npcs.Clear();
            npcById.Clear();
            overlaps.Clear();
            // Nothing to fight until the player attacks in the room (MinionEngage).
            if (!MinionEngage.Engaged) return;
            Physics2D.OverlapBox(TerraParticles.ToWorld(pCenter), new Vector2(7000f, 5000f) * Px, 0f, enemyFilter, overlaps);
            foreach (var col in overlaps)
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                int id = hm.GetInstanceID();
                if (npcById.ContainsKey(id)) continue;
                var info = EnemyStats.Of(hm);
                if (info == null || EnemyStats.LifeMax(info) <= 5) continue;
                // CanBeChasedBy (dontTakeDamage): not while nothing can hurt it (burrowed, asleep in the moss); a
                // shell only spells get through (a curled-up Elder Baldur) is fair game, Terraria's hits get through it.
                if ((EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm))) continue;
                // A boss behind its arena's gates from the player: not fought from outside (World.ArenaGuard).
                if (World.ArenaGuard.Outside(hm) || World.ArenaGuard.OffMap(hm)) continue;
                var main = hm.GetComponent<Collider2D>();
                var use = main != null && main.enabled ? main : col;
                var b = use.bounds;
                // Buried in a wall (dug in, or stuck): no minion can get at it.
                if (Buried(b)) continue;
                var n = new Npc
                {
                    Hm = hm, Col = use, Id = id, Boss = info.Boss,
                    Box = new Rect(b.min.x / Px, -b.max.y / Px, b.size.x / Px, b.size.y / Px),
                };
                n.Center = n.Box.center;
                npcs.Add(n);
                npcById[id] = n;
            }
        }

        static bool Buried(Bounds b) =>
            TerraWorld.IsSolidAt(b.center) && TerraWorld.IsSolidAt(new Vector2(b.center.x, b.max.y - 0.05f)) && TerraWorld.IsSolidAt(new Vector2(b.center.x, b.min.y + 0.05f));

        /// <summary>Minions that fly through blocks while they fight (Abigail, the bats, the finch, the Stardust Dragon, the Cool Whip's snowflake, Terraprisma's blades, the Blade Staff's daggers).</summary>
        static bool Phases(Kind k) => k == Kind.Abigail || k == Kind.Bat || k == Kind.Finch || k == Kind.DragonHead || k == Kind.Snowflake || k == Kind.Terraprisma || k == Kind.BladeStaff;

        /// <summary>Minions that walk and jump (the slime, the spiders, the pirates, the frog, the Flinx).</summary>
        static bool OnFoot(Kind k) => k <= Kind.Flinx || k == Kind.CobSpider || IsTiger(k) || IsPygmy(k);

        /// <summary>How far over its feet one on foot goes after an enemy (px): a few jumps, more for the wall-climbing spiders and for the pygmies' spears.</summary>
        static float Reach(Kind k) => IsSpider(k) ? 640f : IsPygmy(k) ? 450f : 240f;

        static bool Sees(Vector2 from, Npc n) => ClearLine(from, n.Center) || ClearLine(from, n.Center + new Vector2(0f, -n.Box.height / 3f));

        /// <summary>
        /// Whether a minion can get at an enemy, on top of Terraria's own range and sight rules,
        /// for Hallownest's walls: one that collides with blocks needs a clear line from itself
        /// (else it pushes against the wall), and one on foot leaves alone an enemy far over its
        /// head; one that flies through blocks goes for whatever it or the player can see (not
        /// for what is behind a wall from both).
        /// </summary>
        bool CanReach(M m, Npc n)
        {
            if (Phases(m.Kind)) return Sees(m.Center, n) || Sees(pCenter, n);
            if (OnFoot(m.Kind) && n.Box.yMax < m.Bottom - Reach(m.Kind)) return false;
            return Sees(m.Center, n);
        }

        static readonly RaycastHit2D[] rayHits = new RaycastHit2D[8];
        static readonly ContactFilter2D terrainFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << ChunkColliders.TerrainLayer };
        static readonly Dictionary<int, bool> ownedCache = new Dictionary<int, bool>();

        /// <summary>
        /// What doesn't block a minion's sight: triggers, effectors, the mod's own blocks (walked on the grid instead) and an
        /// enemy's own solid parts (an Elder Baldur is a wall across its tunnel on the terrain layer): Terraria's
        /// Collision.CanHitLine looks at tiles only, never at NPCs.
        /// </summary>
        static bool Ignored(Collider2D col)
        {
            if (col.isTrigger || col.usedByEffector) return true;
            int id = col.GetInstanceID();
            if (!ownedCache.TryGetValue(id, out bool owned))
            {
                owned = col.GetComponentInParent<TerraOwned>() != null || col.GetComponentInParent<HealthManager>() != null;
                if (ownedCache.Count > 4000) ownedCache.Clear();
                ownedCache[id] = owned;
            }
            return owned;
        }

        /// <summary>Collision.CanHitLine: nothing solid between two points (px); platforms don't block.</summary>
        static bool ClearLine(Vector2 a, Vector2 b)
        {
            // The blocks on the grid (the terrain's colliders are the mod's own, skipped below), then
            // the scene's own solid scenery that isn't on the grid.
            if (!TerraWorld.LineClearPx(a, b)) return false;
            int n = Physics2D.Linecast(TerraParticles.ToWorld(a), TerraParticles.ToWorld(b), terrainFilter, rayHits);
            for (int i = 0; i < n; i++)
                if (!Ignored(rayHits[i].collider)) return false;
            return true;
        }

        /// <summary>CanHitWithOwnBody: within 1000 px and in sight of this minion.</summary>
        static bool CanHitWithOwnBody(M m, Npc n)
        {
            if (Vector2.Distance(m.Center, n.Center) > 1000f) return false;
            if (ClearLine(m.Center, n.Center)) return true;
            var front = m.Center + new Vector2(m.Dir * m.W * 0.5f, -m.H / 3f);
            return ClearLine(front, n.Center + new Vector2(0f, -n.Box.height / 3f)) || ClearLine(front, n.Center);
        }

        /// <summary>CanHitWithMeleeWeapon: within 1000 px of the minion and in sight of the player.</summary>
        bool CanHitWithMeleeWeapon(M m, Npc n)
        {
            if (Vector2.Distance(m.Center, n.Center) > 1000f) return false;
            if (ClearLine(pCenter, n.Center)) return true;
            var hand = pCenter + new Vector2(pDir * 10f, -14f);
            return ClearLine(hand, n.Center + new Vector2(0f, -n.Box.height / 3f)) || ClearLine(hand, n.Center);
        }

        /// <summary>IsInRangeOfMeOrMyOwner: the nearer of the minion and the player, each needing sight of it.</summary>
        bool InRangeOfMeOrOwner(M m, Npc n, float max, out float my, out float pl)
        {
            my = Vector2.Distance(n.Center, m.Center);
            if (my < max && !CanHitWithOwnBody(m, n)) my = float.PositiveInfinity;
            pl = Vector2.Distance(n.Center, pCenter);
            if (pl < max && !CanHitWithMeleeWeapon(m, n)) pl = float.PositiveInfinity;
            return Mathf.Min(my, pl) <= max;
        }

        /// <summary>
        /// Minion_FindTargetInRange: the owner's target if in range; else the enemy nearest this
        /// minion or the player (each by its own sight), on screen (a 1920×1200 box round the
        /// player) or a boss.
        /// </summary>
        Npc FindTargetInRange(M m, float range, bool needOwnBody, bool respectOwnerTarget = true)
        {
            var t = respectOwnerTarget ? OwnerTarget : null;
            if (t != null && InRangeOfMeOrOwner(m, t, range, out _, out _)) return t;
            var view = new Rect(pCenter.x - 960f, pCenter.y - 600f, 1920f, 1200f);
            float best = range, bestMy = range, bestPl = range;
            Npc found = null;
            foreach (var n in npcs)
            {
                if (!CanReach(m, n)) continue;
                if (!InRangeOfMeOrOwner(m, n, best, out float my, out float pl)) continue;
                if (needOwnBody && !CanHitWithOwnBody(m, n)) continue;
                if (!n.Boss && !view.Overlaps(n.Box)) continue;
                found = n;
                bestMy = Mathf.Min(bestMy, my);
                bestPl = Mathf.Min(bestPl, pl);
                best = Mathf.Min(bestMy, bestPl);
            }
            return found;
        }

        // ------------------------------------------------------------------ tiles

        static bool SolidTile(int i, int j) => Cells.Blocks(TerraWorld.Get(i, -j - 1));
        static bool PlatformTile(int i, int j) => TerraKnight.World.Cells.IsPlatform(TerraWorld.Get(i, -j - 1));
        /// <summary>A background wall: the mod's only walls are the dug-out cells.</summary>
        static bool WallTile(int i, int j) => TerraWorld.IsDug(i, -j - 1);
        static int TileOf(float px) => Mathf.FloorToInt(px / 16f);

        static readonly Collider2D[] solidHits = new Collider2D[8];

        /// <summary>Whether a box (px, top-left, y down) overlaps a block or one of the scene's own solid colliders.</summary>
        static bool SolidBox(float x, float y, float w, float h)
        {
            int i0 = TileOf(x + 0.01f), i1 = TileOf(x + w - 0.01f), j0 = TileOf(y + 0.01f), j1 = TileOf(y + h - 0.01f);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (SolidTile(i, j)) return true;
            var c = TerraParticles.ToWorld(new Vector2(x + w * 0.5f, y + h * 0.5f));
            int n = Physics2D.OverlapBoxNonAlloc(c, new Vector2(Mathf.Max(w - 0.5f, 0.1f), Mathf.Max(h - 0.5f, 0.1f)) * Px, 0f, solidHits, 1 << ChunkColliders.TerrainLayer);
            for (int k = 0; k < n; k++)
                if (!Ignored(solidHits[k])) return true;
            return false;
        }

        static bool SolidBox(Rect r) => SolidBox(r.x, r.y, r.width, r.height);

        /// <summary>A platform's top crossed by a box's bottom moving down from bottom to bottom + dy.</summary>
        static bool PlatformCrossed(float x, float w, float bottom, float dy)
        {
            int i0 = TileOf(x + 0.01f), i1 = TileOf(x + w - 0.01f);
            int j0 = Mathf.CeilToInt(bottom / 16f - 0.001f), j1 = TileOf(bottom + dy);
            for (int j = j0; j <= j1; j++)
            {
                if (bottom > j * 16f + 0.01f) continue;
                for (int i = i0; i <= i1; i++)
                    if (PlatformTile(i, j)) return true;
            }
            return false;
        }

        /// <summary>
        /// Collision.TileCollision for a minion: swept in small steps, a blocked axis stops dead
        /// (vx or vy exactly 0, as the AIs test for "on the ground"), platforms hold it up unless
        /// it falls through, and it never dies on a tile.
        /// </summary>
        static void TileCollision(M m, bool fallThrough)
        {
            var v = m.Vel;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y)) / 8f));
            float dx = v.x / steps, dy = v.y / steps;
            bool hitX = false, hitY = false;
            for (int s = 0; s < steps; s++)
            {
                if (!hitX && dx != 0f)
                {
                    if (!SolidBox(m.Pos.x + dx, m.Pos.y, m.W, m.H)) m.Pos.x += dx;
                    else
                    {
                        m.Pos.x += Flush(t => SolidBox(m.Pos.x + dx * t, m.Pos.y, m.W, m.H)) * dx;
                        m.Pos.x = SnapEdge(m.Pos.x, m.W);
                        hitX = true;
                    }
                }
                if (!hitY && dy != 0f)
                {
                    bool Blocked(float t) => SolidBox(m.Pos.x, m.Pos.y + dy * t, m.W, m.H) || (dy > 0f && !fallThrough && PlatformCrossed(m.Pos.x, m.W, m.Bottom, dy * t));
                    if (!Blocked(1f)) m.Pos.y += dy;
                    else
                    {
                        m.Pos.y += Flush(Blocked) * dy;
                        m.Pos.y = SnapEdge(m.Pos.y, m.H);
                        hitY = true;
                    }
                }
            }
            if (hitX) m.Vel.x = 0f;
            if (hitY) m.Vel.y = 0f;
        }

        /// <summary>How far along a blocked step (0..1) the box can still go, by bisection.</summary>
        static float Flush(System.Func<float, bool> blocked)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 8; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (blocked(mid)) hi = mid;
                else lo = mid;
            }
            return lo;
        }

        /// <summary>Right against a tile edge (within a tenth of a pixel), it's put exactly on it.</summary>
        static float SnapEdge(float p, int size)
        {
            float a = Mathf.Round(p / 16f) * 16f, b = Mathf.Round((p + size) / 16f) * 16f - size;
            if (Mathf.Abs(p - a) < 0.1f) return a;
            if (Mathf.Abs(p - b) < 0.1f) return b;
            return p;
        }

        /// <summary>Whether something holds the box up from below (a block, a platform when not falling through).</summary>
        static bool Supported(M m, bool fallThrough) =>
            SolidBox(m.Pos.x, m.Pos.y + 1f, m.W, m.H) || (!fallThrough && PlatformCrossed(m.Pos.x, m.W, m.Bottom, 1f));

        /// <summary>
        /// Collision.StepUp: walking into a ledge up to a block high with room above, the minion is
        /// lifted onto it, its sprite easing up after it (gfxOffY).
        /// </summary>
        static void StepUp(M m, bool fallThrough)
        {
            if (m.Vel.y < 0f || m.Vel.x == 0f) return;
            float probe = Mathf.Sign(m.Vel.x) * Mathf.Max(Mathf.Abs(m.Vel.x), 1f);
            if (!SolidBox(m.Pos.x + probe, m.Pos.y, m.W, m.H)) return;
            if (!Supported(m, fallThrough)) return;
            for (float rise = 2f; rise <= 16.1f; rise += 2f)
            {
                if (SolidBox(m.Pos.x, m.Pos.y - rise, m.W, m.H)) return;
                if (SolidBox(m.Pos.x + probe, m.Pos.y - rise, m.W, m.H)) continue;
                m.Pos.y -= rise;
                m.GfxOffY += rise;
                m.StepSpeed = rise < 9f ? 1f : 2f;
                return;
            }
        }

        // ------------------------------------------------------------------ hitting

        /// <summary>Damage_CanDealDamage and friendly for these types.</summary>
        static bool CanDamage(M m)
        {
            switch (m.Kind)
            {
                case Kind.Hornet:
                case Kind.Imp:
                case Kind.Retanimini:
                case Kind.AbigailCounter:
                    return false;
                case Kind.Spazmamini: return m.Ai0 == 2f;
                case Kind.DeadlySphere: return m.Ai0 >= 6f && m.Ai0 <= 8f;
                // Damage_CanDealDamage: a projPet minion hurts only by the listed exceptions (not the UFO or the cell).
                case Kind.Sharknado: return true;
                case Kind.UFO:
                case Kind.StardustCell: return false;
                case Kind.Terraprisma: return m.Ai0 != 0f;
                case Kind.TigerGem: return false;
                case Kind.BladeStaff: return true;
                // Damage_CanDealDamage: the towers and the trap never touch; the trap's blast does; the aura on its own terms (AuraDamage).
                case Kind.Flameburst1:
                case Kind.Flameburst2:
                case Kind.Flameburst3:
                case Kind.Ballista1:
                case Kind.Ballista2:
                case Kind.Ballista3:
                case Kind.Trap1:
                case Kind.Trap2:
                case Kind.Trap3:
                case Kind.Aura1:
                case Kind.Aura2:
                case Kind.Aura3:
                    return false;
                case Kind.TrapBlast1:
                case Kind.TrapBlast2:
                case Kind.TrapBlast3:
                case Kind.BabySpider:
                    return true;
                case Kind.SpiderHiver:
                case Kind.SpiderEgg:
                case Kind.FrostHydra:
                case Kind.Houndius:
                case Kind.LunarPortal:
                case Kind.PortalLaser:
                case Kind.RainbowCrystal:
                case Kind.CrystalBlast:
                case Kind.Barnacle:
                case Kind.Foxsparks:
                case Kind.TrustyFoxsparks:
                case Kind.ForbiddenRelic:
                    return false;
                // P:12463: not while flung.
                case Kind.MushroomBoi: return m.Ai2 <= 0f;
                case Kind.Pygmy1:
                case Kind.Pygmy2:
                case Kind.Pygmy3:
                case Kind.Pygmy4:
                    return false;
                case Kind.Tiger1:
                case Kind.Tiger2:
                case Kind.Tiger3:
                    return m.Ai0 != 4f;
                case Kind.Bat: return m.Ai0 != 0f;
                case Kind.Finch: return m.Frame != 4;
                case Kind.VampireFrog:
                case Kind.Flinx:
                case Kind.Abigail:
                case Kind.Snowflake:
                case Kind.FireBlast:
                    return true;
                default:
                    if (IsDragon(m.Kind)) return true;
                    return m.Friendly;
            }
        }

        readonly HashSet<int> hitNow = new HashSet<int>();
        static readonly ContactFilter2D hitFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };

        /// <summary>
        /// Projectile.Damage: every enemy the minion's box overlaps that its immunity allows takes a
        /// hit: tags added (minions only), Terraria's ±15%, no crit unless a tag rolls one.
        /// </summary>
        void Damage(M m)
        {
            if (!MinionEngage.Engaged) return;
            if (IsAura(m.Kind))
            {
                if (m.Ai0 == 0f && m.LocalAi0 != 0f) AuraDamage(m);
                return;
            }
            if (m.Kind == Kind.PortalLaser)
            {
                LaserDamage(m);
                return;
            }
            if (!CanDamage(m)) return;
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            var box = m.Box;
            // Abigail's attack reaches 30 px past her box on every side.
            if (m.Kind == Kind.Abigail && m.Ai0 >= 2f) box = new Rect(box.x - 30f, box.y - 30f, box.width + 60f, box.height + 60f);
            overlaps.Clear();
            var filter = hitFilter;
            filter.layerMask = MeleeHits.HitMask;
            Physics2D.OverlapBox(TerraParticles.ToWorld(box.center), box.size * Px, 0f, filter, overlaps);
            // The Vampire Frog's tongue, while it strikes.
            // The Pals' and the Clay Bud's punch: a 20 px box 8 px before them (P:15845-15866).
            if ((m.Kind == Kind.Cattiva || m.Kind == Kind.TrustyCattiva || m.Kind == Kind.ClayPot) && m.Ai0 == 2f)
            {
                extra.Clear();
                Physics2D.OverlapBox(TerraParticles.ToWorld(m.Center + new Vector2(m.SpriteDir * 8f, 0f)), new Vector2(20f, 20f) * Px, 0f, filter, extra);
                foreach (var c in extra) if (!overlaps.Contains(c)) overlaps.Add(c);
            }
            if (m.Kind == Kind.VampireFrog && m.Ai0 == 2f)
            {
                extra.Clear();
                Physics2D.OverlapBox(TerraParticles.ToWorld(m.Center + new Vector2(m.SpriteDir * 30f, 0f)), new Vector2(50f, 20f) * Px, 0f, filter, extra);
                foreach (var c in extra) if (!overlaps.Contains(c)) overlaps.Add(c);
            }
            // The dragon's segments share the head's immunity.
            var immune = m.Immune;
            if (IsDragon(m.Kind))
            {
                var head = DragonHeadOf(m);
                if (head != null) immune = head.Immune;
            }
            int abigailCap = m.Kind == Kind.Abigail ? 3 + AbigailExtra() / 2 : int.MaxValue;
            hitNow.Clear();
            foreach (var col in overlaps)
            {
                if (m.Dead) return;
                var hm = col.GetComponentInParent<HealthManager>();
                // A minion fights, it doesn't pull levers or break pots (a City lift called away by a slime); Revek is
                // fought, and what a boss sends out at the player (barbs, barrels, dung balls) is hit as by any weapon.
                if (hm == null && m.Damage > 0)
                {
                    if (PropHits.Hostile(col)) PropHits.Pulse(col, TerraParticles.ToWorld(m.Center), m.Dir, m.Damage);
                    else PropHits.Revek(col);
                }
                if (hm == null || hm.isDead) continue;
                int id = hm.GetInstanceID();
                if (!hitNow.Add(id)) continue;
                if (m.Kind == Kind.FireBlast && id == m.TargetId) continue;
                // The little shade (Items/MinionsShade.cs): its wave's one strike for its own enemy, half a blow for others on its way.
                if (m.Kind == Kind.ShadeSoul && !ShadeMayHit(m, id)) continue;
                if (immune.ContainsKey(id)) continue;
                if (m.S.TypeCooldown > 0 && typeImmune.ContainsKey((m.Kind, id))) continue;
                // Abigail stops once she holds immunity on as many enemies as she may hit at once.
                if (immune.Count >= abigailCap) break;
                int num7 = GrowthDamage(m);
                if (m.Kind == Kind.ShadeSoul && id != m.TargetId) num7 = Mathf.Max(1, Mathf.RoundToInt(num7 * Shade.PathShare));
                // P:14619: a hot sphere (two bounces close together) hits for half.
                if (m.Kind == Kind.DeadlySphere && m.LocalAi2 >= 40f) num7 = (int)(num7 * 0.5f);
                // The dragon hits harder the longer it is: by its scale, (scale − 1)·100 capped at 50.
                if (IsDragon(m.Kind)) num7 = (int)(num7 * (1f + 0.23f * Mathf.Clamp((m.Scale - 1f) * 100f, 0f, 50f)));
                float kb = m.Knockback;
                if (m.Kind == Kind.Abigail && m.Ai0 < 2f) kb = 0f;
                float facing = m.Dir;
                // The Baby Finch knocks enemies away from the player.
                if (m.Kind == Kind.Finch) facing = Npcs(id, out var nn) && nn.Center.x < pCenter.x ? -1f : 1f;
                // P:13660: the Clay Bud knocks enemies away from the player.
                if (m.Kind == Kind.ClayPot) facing = Npcs(id, out var np) && np.Center.x < pCenter.x ? -1f : 1f;
                if (m.Kind == Kind.FireBlast || m.Kind == Kind.Snowflake) facing = col.bounds.center.x >= TerraParticles.ToWorld(m.Center).x ? 1f : -1f;
                bool crit = false;
                int dmg = num7;
                // SummonTagDamageMultiplier: the Blade Staff's daggers take two thirds of the tag (0.669).
                if (m.S.Minion || m.S.Sentry || m.S.Shot) dmg = SummonTags.Modify(hm, num7, out crit, m.Kind == Kind.BladeStaff ? 0.669f : 1f, m.S.Sentry || IsSentryChild(m.Kind), m.Center, m.ItemTag);
                // StatusNPC: the spiders' Venom (and the Queen Spider's babies', P:11497).
                if (IsSpider(m.Kind) || m.Kind == Kind.BabySpider) EnemyDebuffs.Add(hm, EnemyDebuffs.Venom, 60 * Random.Range(2, 5));
                // The Huntress's sets (P:11440-11443): the trap's blast oils what it hits for 4-8.5 s.
                if (IsTrapBlast(m.Kind) && (Equipment.Current.Ooa & OoaSet.HuntressT2) != 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Oiled, Random.Range(8, 18) * 30);
                int pen = Equipment.Current.ArmorPen + m.ItemPen + (m.Kind == Kind.Snowflake ? 30 : m.Kind == Kind.BladeStaff ? 25 : 0);
                melee.HitWith(col, dmg, kb, crit ? 100f : 0f, facing, TerraParticles.ToWorld(m.Center), pen, autonomous: true);
                // P:14492-14499: the Cattiva's punch glitters.
                if (m.Kind == Kind.Cattiva || m.Kind == Kind.TrustyCattiva) FxParticles.CattivaHit(m.Center + new Vector2(m.Dir * 8f, 0f) + Random.insideUnitCircle * 4f);
                DevHits++;
                DevHitDamage += dmg;
                DevMaxHit = Mathf.Max(DevMaxHit, dmg);
                if (m.S.Minion || m.S.Sentry || m.S.Shot) SummonTags.OnHit(hm, num7, m.Center, m.Knockback);
                // A baby spider bites once (penetrate 1).
                if (m.Kind == Kind.BabySpider)
                {
                    Kill(m);
                    return;
                }
                // P:13647: a raven that hits turns away for a while.
                if (m.Kind == Kind.Raven) m.Ai1 = -1f;
                // The little soul's pounce that strikes pulls up (Items/MinionsSoulWave.cs).
                if (m.Kind == Kind.WanderingSoul) SoulStruck(m);
                if (m.Kind == Kind.ShadeSoul && id == m.TargetId) ShadeStruck(m);
                // P:14131: a dagger that hits is knocked off its dart.
                if (m.Kind == Kind.BladeStaff && m.Ai0 > 0f)
                {
                    m.Ai0 = -1f;
                    m.Ai1 = 0f;
                }
                // The Cob Whip's spider re-tags what it bites with its whip.
                if (m.Kind == Kind.CobSpider) SummonTags.Apply(WhipKind.CobWhip, hm);
                // The snowflake carries the Cool Whip's tag.
                if (m.Kind == Kind.Snowflake) SummonTags.Apply(WhipKind.CoolWhip, hm);
                if (m.S.OncePerNpc) immune[id] = int.MaxValue;
                else if (m.S.LocalCooldown > 0) immune[id] = m.S.LocalCooldown;
                else if (m.S.TypeCooldown > 0) typeImmune[(m.Kind, id)] = m.S.TypeCooldown;
                // The spider runs on for 20 ticks after a bite; the Flinx pops up off whatever it hit.
                if (IsSpider(m.Kind)) m.LocalAi1 = 20f;
                if (m.Kind == Kind.Flinx)
                {
                    var to = (TerraParticles.ToPx(col.bounds.center) - m.Center).normalized;
                    m.Vel = new Vector2(to.x + (Random.value - 0.5f) * 13f, -5f);
                }
            }
        }

        readonly List<Collider2D> extra = new List<Collider2D>();

        bool Npcs(int id, out Npc n) => npcById.TryGetValue(id, out n);

        /// <summary>The Cob Whip's strike: its little spider (1036) where the enemy was, 2 damage, alive while the whip's buff is.</summary>
        public void SpawnCobSpider(Vector2 atPx)
        {
            RefreshOwner();
            if (!hasOwner) return;
            var m = Spawn(Kind.CobSpider, null, atPx, 2, 0f);
            m.Damage = 2;
            m.TimeLeft = 2;
        }

        /// <summary>The Firecracker's proc: a 96 px blast (918) at the enemy, hurting everyone else in it once.</summary>
        public void FireBlast(HealthManager hm, int damage)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col == null) return;
            DevBlasts++;
            var m = Spawn(Kind.FireBlast, null, TerraParticles.ToPx(col.bounds.center), damage, 0f);
            m.Damage = damage;
            m.TargetId = hm.GetInstanceID();
            m.TileCollide = false;
            m.TimeLeft = 10;
        }

        // ------------------------------------------------------------------ dust

        /// <summary>Dust.NewDust over the minion's box (Terraria's y-down speed).</summary>
        static int DustBox(M m, int type, Vector2 speed, int alpha, float scale)
        {
            var p = TerraParticles.Instance;
            if (p == null) return -1;
            return p.NewDust(m.Pos, m.W, m.H, type, speed.x, speed.y, alpha, scale);
        }

        static int DustAt(Vector2 topLeft, int w, int h, int type, Vector2 speed, int alpha, float scale)
        {
            var p = TerraParticles.Instance;
            if (p == null) return -1;
            return p.NewDust(topLeft, w, h, type, speed.x, speed.y, alpha, scale);
        }

        static void Sound(string name, float volume = 1f) => TerraMod.Instance?.PlaySound(name, volume);
    }
}
