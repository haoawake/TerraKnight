using System.Collections.Generic;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// Every common enemy's and elite's new AI by its record (空洞骑士小怪精英新版AI设计.md §8). Distances in tiles, speeds in
    /// tiles a second. Tiers: All (【全】), Expert (【专】), Master (【师】). Debuffs are Terraria's buff ids (PlayerDebuffs).
    /// </summary>
    internal static partial class Profiles
    {
        static Dictionary<string, Profile> all;

        public static Profile For(string record)
        {
            if (record == null) return null;
            if (all == null) Build();
            return all.TryGetValue(record, out var p) ? p : null;
        }

        public static int Count { get { if (all == null) Build(); return all.Count; } }

        static void Build()
        {
            all = new Dictionary<string, Profile>();
            Crossroads();
            Greenpath();
            FogAndFungus();
            City();
            WaterwaysAndPeak();
            Deepnest();
            EdgeAndGardens();
            Colosseum();
            Others();
        }

        static Profile P(string key, float sight = 14f, bool flyer = false, float memory = 4.5f, float alarm = 0f, float aimLead = 0.2f, float gap = 0.9f, float hear = 12f)
        {
            var p = new Profile { Key = key, Sight = sight, Flyer = flyer, Memory = memory, Alarm = alarm, AimLead = aimLead, Gap = gap, Hear = hear };
            all[key] = p;
            return p;
        }

        /// <summary>Another kind with the same AI (the Colosseum's stronger copies), its moves copied.</summary>
        static Profile Like(string key, string from)
        {
            var f = all[from];
            var p = new Profile
            {
                Key = key, Sight = f.Sight, Flyer = f.Flyer, Memory = f.Memory + 2f, Alarm = f.Alarm, AimLead = f.AimLead, Gap = f.Gap,
                Hear = f.Hear, FacesRight = f.FacesRight, Mantis = f.Mantis, Sibling = f.Sibling, TouchDebuff = f.TouchDebuff,
                TouchTicks = f.TouchTicks, TouchTier = f.TouchTier,
            };
            foreach (var m in f.Moves) p.Moves.Add(m.Clone());
            all[key] = p;
            return p;
        }

        static Profile Add(this Profile p, params Move[] moves)
        {
            p.Moves.AddRange(moves);
            return p;
        }

        // ------------------------------------------------------------------ the building blocks, with the usual numbers

        /// <summary>A straight charge (a walker's stays level).</summary>
        static Move Lunge(float max, float speed, float time, float antic = 0.4f, float recover = 0.5f, float cd = 2.5f, Tier tier = Tier.All, float min = 0f, bool vertical = false, Rel rel = Rel.Any, When when = When.Any) =>
            new Move { Kind = Kind.Lunge, Max = max, Min = min, Speed = speed, Time = time, Antic = antic, Recover = recover, Cooldown = cd, Tier = tier, Vertical = vertical, Rel = rel, When = when };

        /// <summary>A flyer's burst of chasing the meeting point.</summary>
        static Move Chase(float min, float max, float speed, float time, float cd = 3f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Chase, Min = min, Max = max, Speed = speed, Time = time, Antic = 0.3f, Recover = 0.2f, Cooldown = cd, Tier = tier, Sight = false };

        /// <summary>A leap at where the player will be.</summary>
        static Move Leap(float max, float height, float speed = 16f, float antic = 0.4f, float recover = 0.5f, float cd = 3f, Tier tier = Tier.All, float min = 0f, Rel rel = Rel.Any, When when = When.Any) =>
            new Move { Kind = Kind.Leap, Max = max, Min = min, Height = height, Speed = speed, Antic = antic, Recover = recover, Cooldown = cd, Tier = tier, Rel = rel, When = when };

        static Move Dive(float max, float height, float speed, float time = 0.4f, float antic = 0.35f, float cd = 3f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Dive, Max = max, Height = height, Speed = speed, Time = time, Antic = antic, Recover = 0.5f, Cooldown = cd, Tier = tier, Vertical = true };

        /// <summary>A walker waiting under where a flying player must come down.</summary>
        static Move Stalk(float speed, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Stalk, Min = 0f, Max = 18f, Speed = speed, Time = 2f, Antic = 0.3f, Recover = 0.1f, Cooldown = 4f, Tier = tier, Rel = Rel.Above, When = When.Air, Sight = false };

        static Move Blink(float min, float max, float antic = 0.45f, float cd = 3.5f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Blink, Min = 0f, Max = 30f, Antic = antic, Recover = 0.4f, Cooldown = cd, Tier = tier, Sight = false, Width = min, Length = max, Look = Look.Soul };

        static Move Burrow(float max, float speed, float antic = 0.45f, float cd = 5f, Tier tier = Tier.All, Look look = Look.Rock) =>
            new Move { Kind = Kind.Burrow, Max = max, Min = 3f, Speed = speed, Time = 1.6f, Antic = antic, Recover = 0.6f, Cooldown = cd, Tier = tier, Sight = false, Width = 2.5f, Height = 3.5f, Look = look };

        static Move Drop(float max, float antic = 0.3f, float cd = 4f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Drop, Max = max, Antic = antic, Recover = 0.6f, Cooldown = cd, Tier = tier, Rel = Rel.Below, Sight = true };

        /// <summary>Shots: straight (gravity 0) or thrown; Count with Interval 0 is a fan of Spread degrees.</summary>
        static Move Shot(Look look, float max, float speed, int count = 1, float interval = 0f, float spread = 0f, float gravity = 0f, float antic = 0.45f, float recover = 0.5f, float cd = 2.6f, Tier tier = Tier.All, float min = 0f, Aim aim = Aim.Lead, Land land = Land.None, float size = 0.7f, float homing = 0f, float life = 3f) =>
            new Move { Kind = Kind.Shot, Look = look, Max = max, Min = min, ShotSpeed = speed, Count = count, Interval = interval, Spread = spread, Gravity = gravity, Antic = antic, Recover = recover, Cooldown = cd, Tier = tier, Aim = aim, Land = land, Size = size, Homing = homing, Life = life };

        static Move Pulse(Look look, float radius, float max, float antic = 0.6f, float duration = 0.35f, float cd = 3f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Pulse, Look = look, Radius = radius, Max = max, Antic = antic, Duration = duration, Recover = 0.4f, Cooldown = cd, Tier = tier, Sight = false };

        static Move Wave(float length, float speed, float max = 10f, float antic = 0.5f, float cd = 3.5f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Wave, Look = Look.Rock, Length = length, Speed = speed, Max = max, Antic = antic, Recover = 0.5f, Cooldown = cd, Tier = tier, Width = 1.4f, Height = 2f, Sight = false };

        static Move Pillar(Look look, int count, float max, float delay = 0.6f, float interval = 0.35f, float height = 7f, float antic = 0.5f, float cd = 4f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Pillar, Look = look, Count = count, Max = max, Delay = delay, Interval = interval, Height = height, Width = 2f, Duration = 0.4f, Antic = antic, Recover = 0.5f, Cooldown = cd, Tier = tier };

        static Move Cloud(Look look, float radius, float max, float duration = 2f, float drift = 1f, float antic = 0.5f, float cd = 4f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Cloud, Look = look, Radius = radius, Max = max, Duration = duration, Drift = drift, Antic = antic, Recover = 0.4f, Cooldown = cd, Tier = tier, Sight = false };

        static Move Pull(float max, float speed, float duration = 0.8f, float antic = 0.5f, float cd = 6f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Pull, Max = max, Min = 3f, Speed = speed, Duration = duration, Antic = antic, Recover = 0.3f, Cooldown = cd, Tier = tier, Look = Look.Water };

        static Move Beam(Look look, float max, float length, float delay = 0.5f, float duration = 0.35f, float sweep = 0f, float antic = 0.4f, float cd = 4f, Tier tier = Tier.All) =>
            new Move { Kind = Kind.Beam, Look = look, Max = max, Length = length, Width = 1.2f, Delay = delay, Duration = duration, Sweep = sweep, Antic = antic, Recover = 0.5f, Cooldown = cd, Tier = tier };

        /// <summary>
        /// Its own attack again, sooner and from further than its script would (its script put at <paramref name="state"/>,
        /// from one of <paramref name="from"/>): held for <paramref name="hold"/> seconds while its script plays it out.
        /// </summary>
        static Move Own(string state, float max, float hold = 1.2f, float cd = 3f, Tier tier = Tier.All, float min = 0f, string fsm = null, params string[] from) =>
            new Move { Kind = Kind.Own, OwnState = state, OwnFsm = fsm, OwnFrom = from != null && from.Length > 0 ? from : null, Max = max, Min = min, Time = hold, Antic = 0f, Recover = 0.2f, Cooldown = cd, Tier = tier };

        static Move With(this Move m, int debuff, float seconds)
        {
            m.Debuff = debuff;
            m.DebuffTicks = UnityEngine.Mathf.RoundToInt(seconds * 60f);
            return m;
        }

        static Move Cut(this Move m) { m.CutHook = true; return m; }
        static Move Heavy(this Move m) { m.Heavy = true; return m; }
        static Move Waves(this Move m, float length = 6f, float speed = 18f) { m.LandWave = true; m.Length = length; m.LandWaveSpeed = speed; return m; }
        static Move Times(this Move m, int repeat) { m.Repeat = repeat; return m; }
        static Move Masks(this Move m, int masks) { m.Masks = masks; return m; }
        static Move Clips(this Move m, string antic, string act = null) { m.AnticClip = antic; m.ActClip = act; return m; }
        static Move NoSight(this Move m) { m.Sight = false; return m; }
        static Move Weight(this Move m, float w) { m.Weight = w; return m; }

        const int OnFire = PlayerDebuffs.OnFire, Bleeding = PlayerDebuffs.Bleeding, Confused = PlayerDebuffs.Confused, Slow = PlayerDebuffs.Slow,
            Silenced = PlayerDebuffs.Silenced, BrokenArmor = PlayerDebuffs.BrokenArmor, Chilled = PlayerDebuffs.Chilled, Darkness = PlayerDebuffs.Darkness,
            Electrified = PlayerDebuffs.Electrified, Webbed = PlayerDebuffs.Webbed, Poisoned = PlayerDebuffs.Poisoned, Venom = PlayerDebuffs.Venom;
    }
}
