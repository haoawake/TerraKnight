using System.Collections.Generic;

namespace TerraKnight.EnemyAi
{
    /// <summary>The lowest difficulty a move is used on: every one, Expert and up, Master and Hollow.</summary>
    internal enum Tier { All = 0, Expert = 1, Master = 2 }

    /// <summary>
    /// The building blocks of the new moves (空洞骑士小怪精英新版AI设计.md §4). The first group moves the enemy itself (its own
    /// script waits while it runs); the rest act beside whatever its own script is doing.
    /// </summary>
    internal enum Kind
    {
        // The enemy moves.
        Lunge, Chase, Leap, Dive, Stalk, Blink, Burrow, Drop,
        // Beside its own script.
        Shot, Pulse, Wave, Pillar, Cloud, Pull, Beam,
        // Its own attack again: its script put at the state that starts it (a Gulka's spit, a Kingsmould's throw).
        Own,
    }

    /// <summary>Where the player must be, against the enemy.</summary>
    internal enum Rel { Any, Above, Below, Level }

    /// <summary>What the player must be doing (only what can be seen).</summary>
    internal enum When { Any, Air, Ground, Hooked, Struck, PostDash, Close }

    /// <summary>Where a shot goes: at the player, at where he will be, both by turns, straight down, up, all round.</summary>
    internal enum Aim { Target, Lead, Bracket, Down, Up, Radial }

    /// <summary>How a shot or area looks: Terraria's own projectile sheets and dusts, tinted for Hallownest.</summary>
    internal enum Look { Infection, Acid, Spike, Thorn, Crystal, Web, Poison, Soul, Fire, Stinger, Honey, Rock, Electric, Coin, Spore, Void, Blade, Water }

    /// <summary>What a shot leaves where it lands.</summary>
    internal enum Land { None, Pool, Cluster, Burst, Cloud, Web, Honey }

    /// <summary>
    /// One new move of one enemy. Distances are in Terraria tiles (16 px, half a Unity unit), speeds in tiles a second,
    /// times in seconds; <see cref="Brain"/> turns them into the game's units.
    /// </summary>
    internal sealed class Move
    {
        public Kind Kind;
        public Tier Tier;
        public float Weight = 1f, Cooldown = 3f;
        /// <summary>The player's distance it is used at.</summary>
        public float Min, Max = 10f;
        public bool Sight = true;
        public Rel Rel;
        public When When;
        /// <summary>The wind-up (shown) and the stand after (open to a counter), before the difficulty's scale.</summary>
        public float Antic = 0.5f, Recover = 0.5f;
        /// <summary>How much of the difficulty's lead it aims with (0: where the player is).</summary>
        public float Lead = 1f;

        // Moving: a lunge's or chase's speed and time, a leap's top height, a dive's height above the player.
        public float Speed = 30f, Time = 0.4f, Height = 5f;
        /// <summary>A flyer's lunge goes any way; a walker's stays level.</summary>
        public bool Vertical;
        /// <summary>Knocks the player off a mount (Master).</summary>
        public bool Heavy;
        /// <summary>Cuts a hook's rope it crosses (Master).</summary>
        public bool CutHook;
        /// <summary>A leap or drop ends in a ground wave both ways.</summary>
        public bool LandWave;
        public float LandWaveSpeed = 18f;
        /// <summary>Repeats (a lunge again from where it stopped, a chain of leaps).</summary>
        public int Repeat;

        // Shots.
        public int Count = 1;
        public float Spread, Interval, Gravity, Homing, Life = 3f, Size = 0.6f, ShotSpeed = 24f;
        public Aim Aim = Aim.Lead;
        public Look Look = Look.Infection;
        public Land Land;
        public int Bounces;

        // Areas: a pulse's radius, a pillar's or wave's width and height, a beam's length, how long it hurts, its warning.
        public float Radius = 3f, Width = 2f, Length = 14f, Duration = 0.6f, Delay = 0.6f, Drift, Sweep;
        public int Masks = 1;
        /// <summary>A Terraria debuff (buff id) put on the player it hits, and for how long (ticks).</summary>
        public int Debuff, DebuffTicks;
        /// <summary>The enemy's own clips for the wind-up and the act (else found by name).</summary>
        public string AnticClip, ActClip;

        // Its own attack (Kind.Own): the state of its script that starts it, the script's name (null: the one that has
        // that state), and the states it may be sent from (null: any that isn't busy).
        public string OwnState, OwnFsm;
        public string[] OwnFrom;

        public Move Clone() => (Move)MemberwiseClone();
    }

    /// <summary>One enemy kind's new AI: how it finds the player and the moves it adds.</summary>
    internal sealed class Profile
    {
        public string Key;
        /// <summary>Sight and hearing in tiles; memory of the player once out of sight, in seconds.</summary>
        public float Sight = 14f, Hear = 12f, Memory = 4.5f;
        /// <summary>It flies (moves any way, no gravity).</summary>
        public bool Flyer;
        /// <summary>Its cry wakes the room's others of its kind and side (tiles).</summary>
        public float Alarm;
        /// <summary>How far ahead (seconds) its own script's aim and chase are led (single player).</summary>
        public float AimLead = 0.2f;
        /// <summary>The least time between two of its new moves.</summary>
        public float Gap = 0.9f;
        /// <summary>Its sprite faces right at a positive x scale (most face left).</summary>
        public bool FacesRight;
        /// <summary>A Mantis tribe member: calm after the Mantis Lords fall, unless struck.</summary>
        public bool Mantis;
        /// <summary>A Sibling: calm once the Void Heart is worn.</summary>
        public bool Sibling;
        /// <summary>Its own script's waking ranges are left as they are (its closing and opening go by them: the Elder Baldur's shell).</summary>
        public bool KeepWake;
        /// <summary>The Elder Baldur: its own goop spit aimed at the player, its wave made of that goop (BlockerSpit).</summary>
        public bool GoopSpit;
        /// <summary>
        /// Struck from any distance, it answers (the Crawlid's charge): its struck move goes even off the screen, and still
        /// a moment later if a knockback held it off when the hit came.
        /// </summary>
        public bool FarStruck;
        /// <summary>
        /// Struck, it may turn its hard back to the blows for a moment (Brain's shell: the Crystal Crawler's crystal), as
        /// a shield held their way: melee clanks off and throws the player back, Terraria's reflectable shots come back.
        /// </summary>
        public bool Reflect;
        /// <summary>A debuff its touch puts on the player (from this difficulty tier up).</summary>
        public int TouchDebuff, TouchTicks;
        public Tier TouchTier;
        public readonly List<Move> Moves = new List<Move>();
    }
}
