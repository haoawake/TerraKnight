using UnityEngine;
using Left = TerraKnight.Items.MageLefts.Left;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade magic weapons' left button (MageLefts' specs): the game's shot at the heart of each, fired its new way
    /// through RangedFire's With (what a RangedShot carries), counted in runs as the ranged weapons' are; a channelled one's
    /// held thing left as the game makes it, its pulses ticked while it is used (BladeSkills.Tick).
    /// </summary>
    internal static class MageFire
    {
        static ItemType runItem;
        static int run;
        static float lastCastAt = -10f, pulseAt;

        /// <summary>Dev: casts fired the new way, echoes, twins, great ones, pulses, chaos's picks.</summary>
        public static int DevCasts, DevEchoes, DevGreat, DevPulses, DevExtras, DevNovas;
        public static Left DevLastLeft;

        /// <summary>The longest pause that keeps a run going: twice the use, at least 0.8 s.</summary>
        static float Gap(ItemDef d) => Mathf.Max(0.8f, d.UseAnimation * 2.5f);

        /// <summary>The use's shot its new way (TerraMod.Fire); false to leave it to the game's.</summary>
        public static bool Fire(in ShootPatterns.Shot s)
        {
            var def = s.Def;
            if (def == null || def.Class != DamageClass.Magic || !MageArts.Active(def)) return false;
            var spec = MageLefts.Of(def);
            if (spec == null) return false;
            if (runItem != def.Type || Time.time - lastCastAt > Gap(def)) run = 0;
            runItem = def.Type;
            lastCastAt = Time.time;
            run++;
            DevCasts++;
            DevLastLeft = spec.Left;
            var r = new RangedShot { Item = def };
            spec.Sign?.Invoke(r);
            var t = s;
            var s0 = s;
            // The weapon's own cast (MageKits).
            if (spec.Left == Left.Custom && spec.Cast != null)
            {
                spec.Cast(s0, r, run);
                return true;
            }
            // A channelled one's held thing (the Last Prism, the Medusa Head...) as the game makes it, what its shot carries aside.
            if (def.Channel || spec.Left == Left.Pulse)
            {
                RangedFire.With(r, t);
                return true;
            }
            switch (spec.Left)
            {
                case Left.Echo:
                {
                    t.Damage = Scale(s.Damage, 0.9f);
                    RangedFire.With(r, t);
                    if (run % Mathf.Max(1, spec.N) != 0) return true;
                    var echo = r.Copy();
                    echo.Home = Mathf.Max(echo.Home, 3f);
                    echo.Trail = Mathf.Max(echo.Trail, 0.6f);
                    echo.Local = true;
                    var kind = def.Type == ItemType.BejeweledStaff ? MageLefts.Gems[Random.Range(0, MageLefts.Gems.Length)] : spec.Kind;
                    float echoK = 0.6f * spec.K;
                    TerraProjectiles.Instance?.Later(12, (from, dir) => Echo(def, from, dir, echo, kind, s0, echoK));
                    return true;
                }
                case Left.Twin:
                {
                    // From either shoulder, turned out a little, curving in on the cursor.
                    var dir = Dir(s);
                    var side = new Vector2(-dir.y, dir.x);
                    r.Point = s.Mouse;
                    r.Seek = 4f;
                    r.SeekFrom = 2;
                    r.Local = true;
                    t.Damage = Scale(s.Damage, 0.55f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var a = t;
                        a.From = s.From + side * k * 1.1f;
                        a.Vel = Rotate(s.Vel, k * 20f);
                        RangedFire.With(r, a);
                    }
                    MageCast(def, s.From + side * 1.1f, s.From - side * 1.1f);
                    return true;
                }
                case Left.Resonance:
                {
                    int every = Mathf.Max(2, spec.N);
                    if (run % every == 0)
                    {
                        r.Pierce += 2;
                        r.Size = 1.4f;
                        r.Trail = 1f;
                        r.TrailScale = 1.1f;
                        r.Local = true;
                        t.Damage = Scale(s.Damage, 0.8f + 0.25f * every);
                        DevGreat++;
                        SwordFx.Instance?.MageGreat(def);
                        TerraMod.Instance?.PlaySound("Item_9", 0.35f, 0.75f);
                    }
                    else t.Damage = Scale(s.Damage, 0.8f);
                    RangedFire.With(r, t);
                    return true;
                }
                case Left.Fan:
                {
                    int n = Mathf.Max(2, spec.N);
                    t.Damage = Scale(s.Damage, 1.2f * spec.K / n);
                    r.Local = true;
                    for (int i = 0; i < n; i++)
                    {
                        var a = t;
                        // Close together (5 degrees either side): all of them on one enemy near, spreading further out.
                        a.Vel = Rotate(s.Vel, Mathf.Lerp(-5f, 5f, i / (n - 1f)));
                        RangedFire.With(r, a);
                    }
                    return true;
                }
                case Left.Orbit:
                    r.Orbit = 24;
                    r.OrbitTurn = 15f;
                    r.OrbitRadius = 34f;
                    r.OrbitAngle = Random.Range(0f, 360f);
                    r.Local = true;
                    r.Trail = Mathf.Max(r.Trail, 0.4f);
                    t.Damage = Scale(s.Damage, 1.1f * spec.K);
                    RangedFire.With(r, t);
                    return true;
                case Left.Hover:
                {
                    // Held a little behind and above, gathering, then off faster.
                    var dir = Dir(s);
                    r.Hover = 14;
                    r.LaunchK = 1.5f;
                    r.Size = 1.15f;
                    r.Local = true;
                    r.Trail = Mathf.Max(r.Trail, 0.5f);
                    t.Damage = Scale(s.Damage, 0.95f * spec.K);
                    t.From = s.From - dir * Random.Range(0.6f, 1.4f) + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 1.6f));
                    RangedFire.With(r, t);
                    return true;
                }
                case Left.Volley:
                {
                    t.Damage = Scale(s.Damage, 0.8f);
                    RangedFire.With(r, t);
                    var second = r.Copy();
                    second.Local = true;
                    int dmg = Scale(s.Damage, 0.35f);
                    TerraProjectiles.Instance?.Later(6, (from, dir) =>
                    {
                        var b = s0;
                        b.From = from;
                        b.Vel = Rotate(dir * s0.Speed, Random.Range(-6f, 6f));
                        b.Damage = dmg;
                        RangedFire.With(second, b);
                    });
                    return true;
                }
                case Left.Rain:
                {
                    RangedFire.With(r, t);
                    if (run % Mathf.Max(1, spec.N) == 0) Rain(def, spec.Kind != ProjKind.None ? spec.Kind : s.Kind, s, spec.K, r.Copy());
                    return true;
                }
                case Left.Nova:
                {
                    t.Damage = Scale(s.Damage, 0.9f);
                    RangedFire.With(r, t);
                    if (run % Mathf.Max(1, spec.N) != 0) return true;
                    var tp = TerraProjectiles.Instance;
                    if (tp == null) return true;
                    var kind = spec.Kind != ProjKind.None ? spec.Kind : s.Kind;
                    int dmg = Scale(s.Damage, 0.2f);
                    var ring = new RangedShot { Item = def, Local = true, Trail = 0.4f };
                    var keep = TerraProjectiles.SpawnRanged;
                    TerraProjectiles.SpawnRanged = ring;
                    try
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k / 8f * Mathf.PI * 2f;
                            var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            tp.Shoot(kind, s.From + way * 0.6f, way * Mathf.Max(6f, s.Speed), dmg, 0f, s.Crit);
                        }
                    }
                    finally { TerraProjectiles.SpawnRanged = keep; }
                    DevNovas++;
                    SwordFx.Instance?.MageNova(def, s.From);
                    return true;
                }
                case Left.Extra:
                {
                    t.Damage = Scale(s.Damage, 0.9f);
                    RangedFire.With(r, t);
                    if (run % Mathf.Max(1, spec.N) != 0) return true;
                    var a = s;
                    a.Kind = spec.Kind != ProjKind.None ? spec.Kind : s.Kind;
                    a.Damage = Scale(s.Damage, spec.K);
                    var dir = Dir(s);
                    a.From = s.From + new Vector2(-dir.y, dir.x) * Random.Range(-0.8f, 0.8f);
                    a.Vel = Rotate(s.Vel, Random.Range(-10f, 10f));
                    a.Knockback = 0f;
                    var extra = new RangedShot { Item = def, Local = true, Trail = 0.4f };
                    var tp = TerraProjectiles.Instance;
                    if (a.Kind == s.Kind) RangedFire.With(extra, a);
                    else if (tp != null)
                    {
                        var keep = TerraProjectiles.SpawnRanged;
                        TerraProjectiles.SpawnRanged = extra;
                        try { tp.Shoot(a.Kind, a.From, a.Vel, a.Damage, a.Knockback, a.Crit); }
                        finally { TerraProjectiles.SpawnRanged = keep; }
                    }
                    DevExtras++;
                    return true;
                }
                case Left.Chaos:
                    switch (Random.Range(0, 5))
                    {
                        case 0: r.Shards = 3; break;
                        case 1: r.Chain = 2; r.ChainDamage = 0.35f; break;
                        case 2: r.Burst = 72f; r.BurstDamage = 0.45f; break;
                        case 3: r.Bounces = 2; r.BounceDamage = 0.6f; break;
                        default: r.Home = 4f; break;
                    }
                    r.Trail = 0.5f;
                    RangedFire.With(r, t);
                    return true;
                case Left.Gather:
                    r.Point = s.Mouse;
                    r.Seek = 9f;
                    r.SeekFrom = 6;
                    r.PointSpeed = 3f;
                    RangedFire.With(r, t);
                    return true;
                default:
                    RangedFire.With(r, t);
                    return true;
            }
        }

        /// <summary>An echo: from a rune behind and above the player, at the aim then, seeking.</summary>
        static void Echo(ItemDef def, Vector2 from, Vector2 dir, RangedShot r, ProjKind kind, ShootPatterns.Shot s, float k)
        {
            var mod = TerraMod.Instance;
            if (mod == null || mod.Inventory.Current.Def == null || mod.Inventory.Current.Def.Type != def.Type) return;
            var at = from - dir * 1.1f + new Vector2(0f, 1.2f);
            var aim = mod.DevAim ?? mod.AimPoint;
            var way = aim - at;
            var e = s;
            e.From = at;
            e.Vel = (way.sqrMagnitude > 1e-4f ? way.normalized : dir) * s.Speed;
            e.Damage = Scale(s.Damage, k);
            if (kind != ProjKind.None) e.Kind = kind;
            var tp = TerraProjectiles.Instance;
            TerraProjectiles.SpawnClass = def.Class;
            if (kind != ProjKind.None && tp != null)
            {
                var keep = TerraProjectiles.SpawnRanged;
                TerraProjectiles.SpawnRanged = r;
                try { tp.Shoot(kind, e.From, e.Vel, e.Damage, 0f, e.Crit); }
                finally { TerraProjectiles.SpawnRanged = keep; }
            }
            else RangedFire.With(r, e);
            TerraProjectiles.SpawnClass = DamageClass.None;
            DevEchoes++;
            SwordFx.Instance?.MageRune(def, at);
            if (def.UseSound != null) mod.PlaySound(def.UseSound, 0.35f * def.UseSoundVolume);
        }

        /// <summary>One more down from the sky onto the cursor (×k), a little bigger.</summary>
        static void Rain(ItemDef def, ProjKind kind, in ShootPatterns.Shot s, float k, RangedShot r)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            const float Block = 16f * Effects.TerraParticles.Px;
            var spot = s.Mouse + new Vector2(Random.Range(-2f, 2f) * Block, 0f);
            var from = spot + new Vector2(Random.Range(-3f, 3f) * Block, 20f * Block);
            var vel = (spot - from).normalized * Mathf.Max(10f, s.Speed * 1.2f);
            r.Local = true;
            r.Size = Mathf.Max(r.Size, k > 1f ? 1.3f : 1f);
            r.Trail = Mathf.Max(r.Trail, 0.6f);
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { tp.ShootFalling(kind, from, vel, Scale(s.Damage, k), 0f, s.Crit, null); }
            finally { TerraProjectiles.SpawnRanged = keep; }
            DevExtras++;
            SwordFx.Instance?.MageRune(def, from);
        }

        /// <summary>The magic weapon in use (BladeSkills.Tick): a channelled one's pulses.</summary>
        public static void Tick(HeroController hero)
        {
            MageMech.Tick();
            var mod = TerraMod.Instance;
            if (mod == null || hero == null || !mod.IsUsingItem) return;
            var def = mod.SwingItem;
            if (def == null || def.Class != DamageClass.Magic || !MageArts.Active(def)) return;
            var spec = MageLefts.Of(def);
            if (spec == null || spec.Left != Left.Pulse || spec.OnPulse == null) return;
            if (Time.time - pulseAt < spec.Every) return;
            pulseAt = Time.time;
            spec.OnPulse(hero, def);
            DevPulses++;
        }

        // ------------------------------------------------------------------ the channelled weapons' pulses

        static Vector2 Centre(HeroController hero) => (Vector2)hero.transform.position + new Vector2(0f, 0.2f);

        static Vector2 AimOf(HeroController hero)
        {
            var mod = TerraMod.Instance;
            return mod != null ? mod.DevAim ?? mod.AimPoint : Centre(hero) + Vector2.right;
        }

        /// <summary>Shoots one of a kind from a point at a target (world), damage of the weapon × k, its shot carrying r.</summary>
        static void ShootAt(ItemDef def, ProjKind kind, Vector2 from, Vector2 at, float speed, float k, RangedShot r)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var way = at - from;
            var vel = (way.sqrMagnitude > 1e-4f ? way.normalized : Vector2.right) * speed;
            r.Item = def;
            r.Local = true;
            TerraProjectiles.SpawnClass = def.Class;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { tp.Shoot(kind, from, vel, Scale(Equipment.Damage(def, null), k), 0f, Equipment.Crit(def)); }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
        }

        static Vector2 Target(HeroController hero, float withinPx = 640f)
        {
            var hm = TerraProjectiles.NearestEnemy(Centre(hero), withinPx);
            return hm != null ? TerraProjectiles.EnemyCentre(hm) : AimOf(hero);
        }

        /// <summary>The Glacier Fang: two ice bolts at an enemy near.</summary>
        internal static void GlacierShards(HeroController hero, ItemDef def)
        {
            var at = Target(hero);
            for (int k = -1; k <= 1; k += 2)
                ShootAt(def, ProjKind.IceBolt, Centre(hero) + new Vector2(0f, 0.5f) + new Vector2(k * 0.5f, 0f), at, 9f, 0.3f, new RangedShot { Trail = 0.5f, TrailDust = 135 });
        }

        /// <summary>The Medusa Head's gaze: the nearest enemy in sight confused a moment.</summary>
        internal static void MedusaGaze(HeroController hero, ItemDef def)
        {
            var hm = TerraProjectiles.NearestEnemy(Centre(hero), 560f);
            if (hm == null) return;
            EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 90);
            SwordFx.Instance?.MageRune(def, TerraProjectiles.EnemyCentre(hm));
        }

        /// <summary>The Laser Machinegun: two more lasers at an angle either side of the aim.</summary>
        internal static void MachinegunSide(HeroController hero, ItemDef def)
        {
            var from = Centre(hero);
            var aim = AimOf(hero);
            var way = aim - from;
            for (int k = -1; k <= 1; k += 2)
                ShootAt(def, ProjKind.MachinegunLaser, from, from + Rotate(way, k * 9f), 14f, 0.35f, new RangedShot { Trail = 0.3f });
        }

        /// <summary>The Charged Blaster Cannon: an arc to the nearest enemy.</summary>
        internal static void BlasterArc(HeroController hero, ItemDef def)
        {
            var hm = TerraProjectiles.NearestEnemy(Centre(hero), 560f);
            if (hm == null) return;
            ShootAt(def, ProjKind.PulseBolt, Centre(hero), TerraProjectiles.EnemyCentre(hm), 14f, 0.4f, new RangedShot { Trail = 1f, TrailDust = 226, Home = 6f });
        }

        /// <summary>The Last Prism: a hallowed star down on the nearest enemy (its floor there, or it falls through the world).</summary>
        internal static void PrismStar(HeroController hero, ItemDef def)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            const float Block = 16f * Effects.TerraParticles.Px;
            var at = Target(hero);
            var from = at + new Vector2(Random.Range(-3f, 3f) * Block, 14f * Block);
            var r = new RangedShot { Item = def, Local = true, Trail = 1f };
            TerraProjectiles.SpawnClass = def.Class;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { tp.ShootFalling(ProjKind.HallowStar, from, (at - from).normalized * 14f, Scale(Equipment.Damage(def, null), 0.35f), 1f, Equipment.Crit(def), at.y - 0.5f); }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
            SwordFx.Instance?.MageRune(def, from);
        }

        // ------------------------------------------------------------------ helpers

        static void MageCast(ItemDef def, Vector2 a, Vector2 b)
        {
            SwordFx.Instance?.MageRune(def, a);
            SwordFx.Instance?.MageRune(def, b);
        }

        static int Scale(int damage, float k) => Mathf.Max(1, Mathf.RoundToInt(damage * k));

        static Vector2 Dir(in ShootPatterns.Shot s) => s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : Vector2.right;

        internal static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * sn, v.x * sn + v.y * c);
        }
    }
}
