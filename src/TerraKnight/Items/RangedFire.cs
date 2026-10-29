using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade ranged weapons' left button (RangedArts.Left): the shot the game would make, fired its new way, each kept
    /// near the weapon's own damage a second (suite rangedarts measures them against the game's):
    ///   Power: every Nth shot of a run a full-power one (×(0.8 + N/4): 1.55 for the third, 2.3 for the sixth; through 2 more,
    ///   faster, trailing its motes), the rest ×0.8;
    ///   Twin: two side by side for one ammo, ×0.55 each;
    ///   Burst: N in a quick run for the use (its use 0.9 × N long), every one its ammo;
    ///   SpinUp: a machine gun quickening as it is held (RangedArts.UseScale);
    ///   Ricochet: a copy sent on to the next enemy (×0.6) N times;
    ///   Split: N shards on through the struck (×0.3 each);
    ///   Blast: a burst of 45% of its damage round the struck;
    ///   Home: turning after enemies;
    ///   Pierce: through 2 more;
    /// (what these add is for crowds: on one enemy they deal the game's damage).
    ///   PointBlank: a shotgun's pellets ×1.15 within 7 blocks, down to ×0.8 at 20.
    /// </summary>
    internal static class RangedFire
    {
        static ItemType runItem;
        static int run;
        static float lastShotAt = -10f, runStart;

        /// <summary>When the last shot of a remade ranged weapon left (the hold's kick, SwordFxRanged's muzzle).</summary>
        public static float LastShotAt => lastShotAt;
        public static ItemType LastItem => runItem;

        /// <summary>Dev: the shots fired the new way, the power shots among them, the bursts' later shots.</summary>
        public static int DevShots, DevPower, DevBurstShots;
        public static RangedArts.Left DevLastLeft;

        /// <summary>A machine gun's heat: 0 as it starts firing, 1 after 2.5 s of it.</summary>
        public static float Heat(ItemDef d)
        {
            if (d == null || runItem != d.Type || Time.time - lastShotAt > Gap(d)) return 0f;
            return Mathf.Clamp01((Time.time - runStart) / 2.5f);
        }

        /// <summary>The longest pause that keeps a run going: twice the use (a burst's whole run), at least 0.6 s.</summary>
        static float Gap(ItemDef d)
        {
            var look = RangedArts.Of(d);
            return Mathf.Max(0.6f, d.UseAnimation * 2.2f * (look.Left == RangedArts.Left.Burst ? Mathf.Max(1, look.N) : 1.3f));
        }

        /// <summary>The use's shot its new way (TerraMod.Fire); false to leave it to the game's.</summary>
        public static bool Fire(in ShootPatterns.Shot s)
        {
            var def = s.Def;
            if (!RangedArts.Active(def)) return false;
            var look = RangedArts.Of(def);
            if (runItem != def.Type || Time.time - lastShotAt > Gap(def))
            {
                run = 0;
                runStart = Time.time;
            }
            runItem = def.Type;
            run++;
            Shot(def);
            DevShots++;
            DevLastLeft = look.Left;
            // A gun's right made this shot stronger (an aim, a loaded round, a stance): its own shot.
            if (BladeSkills.GunFire(in s)) return true;
            var r = new RangedShot { Item = def };
            RangedArts.Sign(def, r, Heat(def));
            var t = s;
            switch (look.Left)
            {
                case RangedArts.Left.Power:
                    int every = Mathf.Max(2, look.N);
                    if (run % every == 0)
                    {
                        r.Pierce = 2;
                        r.Speed = 1.25f;
                        r.Trail = 1f;
                        r.TrailScale = 1f;
                        r.Local = true;
                        t.Damage = Scale(s.Damage, 0.8f + 0.25f * every);
                        t.Knockback *= 1.5f;
                        DevPower++;
                        SwordFx.Instance?.RangedPower(def);
                        TerraMod.Instance?.PlaySound("Item_5", 0.35f, 1.25f);
                    }
                    else t.Damage = Scale(s.Damage, 0.8f);
                    With(r, t);
                    return true;
                case RangedArts.Left.Twin:
                {
                    // Side by side, 3 px either way of the line.
                    var dir = Dir(s);
                    var side = new Vector2(-dir.y, dir.x) * 3f * Effects.TerraParticles.Px;
                    t.Damage = Scale(s.Damage, 0.55f);
                    r.Local = true;
                    var a = t;
                    a.From = s.From + side;
                    With(r, a);
                    var b = t;
                    b.From = s.From - side;
                    b.Vel = Rotate(s.Vel, 1.5f);
                    With(r, b);
                    return true;
                }
                case RangedArts.Left.Burst:
                {
                    r.Local = true;
                    With(r, t);
                    var pr = TerraProjectiles.Instance;
                    if (pr == null) return true;
                    int gap = Mathf.Clamp(Mathf.RoundToInt(def.UseTime * 60f * 0.3f), 3, 6);
                    for (int k = 1; k < Mathf.Max(2, look.N); k++)
                        pr.Later(gap * k, (from, dir) => Later(def, from, dir, r));
                    return true;
                }
                case RangedArts.Left.Ricochet:
                    r.Bounces = Mathf.Max(1, look.N);
                    With(r, t);
                    return true;
                case RangedArts.Left.Split:
                    r.Shards = Mathf.Max(2, look.N);
                    With(r, t);
                    return true;
                case RangedArts.Left.Blast:
                    r.Burst = 72f;
                    r.BurstDamage = 0.45f;
                    With(r, t);
                    return true;
                case RangedArts.Left.Home:
                    r.Home = 3.5f;
                    r.Trail = 0.25f;
                    With(r, t);
                    return true;
                case RangedArts.Left.Pierce:
                    r.Pierce = 2;
                    r.Local = true;
                    r.Trail = 0.2f;
                    With(r, t);
                    return true;
                case RangedArts.Left.PointBlank:
                    r.Near = 1.15f;
                    r.Far = 0.8f;
                    With(r, t);
                    return true;
                default:
                    With(r, t);
                    return true;
            }
        }

        /// <summary>A burst's later shot: its ammo picked now (none left ends it), from the player at the aim then.</summary>
        static void Later(ItemDef def, Vector2 from, Vector2 dir, RangedShot r)
        {
            var mod = TerraMod.Instance;
            if (mod == null || mod.Inventory.Current.Def != def) return;
            var kind = def.Shoot;
            float speed = def.ShootSpeed;
            int damage;
            float knockback;
            if (def.UseAmmo != AmmoKind.None)
            {
                if (!mod.PickAmmo(def, ref kind, ref speed, out damage, out knockback)) return;
            }
            else
            {
                damage = Equipment.Damage(def, null);
                knockback = Equipment.Knockback(def);
            }
            var shot = new ShootPatterns.Shot
            {
                Def = def, Kind = kind, From = from, Mouse = mod.DevAim ?? mod.AimPoint, Vel = dir * speed, Speed = speed, Knockback = knockback,
                Crit = Equipment.Crit(def), Damage = damage, UseTicks = Mathf.RoundToInt(def.UseAnimation * 60f), Hero = HeroController.SilentInstance,
            };
            TerraProjectiles.SpawnClass = def.Class;
            TerraProjectiles.SpawnForge = Forging.RangedLevel(def);
            With(r, shot);
            TerraProjectiles.SpawnClass = DamageClass.None;
            TerraProjectiles.SpawnForge = 0;
            if (def.UseSound != null) mod.PlaySound(def.UseSound, 0.5f * def.UseSoundVolume);
            Shot(def);
            DevBurstShots++;
        }

        /// <summary>A shot left the weapon: the hold's kick and the muzzle's smoke.</summary>
        public static void Shot(ItemDef def)
        {
            lastShotAt = Time.time;
            runItem = def.Type;
            SwordFx.Instance?.RangedShot(def);
        }

        /// <summary>The shot fired the game's way with what r carries.</summary>
        public static void With(RangedShot r, in ShootPatterns.Shot s)
        {
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { Vanilla(s); }
            finally { TerraProjectiles.SpawnRanged = keep; }
        }

        /// <summary>The game's shot (TerraMod.Fire's): the weapon's own pattern, the Boomstick's pellets, else the one projectile.</summary>
        public static void Vanilla(in ShootPatterns.Shot s)
        {
            if (ShootPatterns.Try(s)) return;
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            if (s.Def.Pellets > 0)
            {
                for (int n = Random.Range(3, 5); n > 0; n--)
                    tp.Shoot(s.Kind, s.From, s.Vel + new Vector2(Random.Range(-35, 36) * 0.04f, Random.Range(-35, 36) * 0.04f), s.Damage, s.Knockback, s.Crit, 0, s.UseTicks);
            }
            else tp.Shoot(s.Kind, s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Extra, s.UseTicks, s.Melee);
        }

        static int Scale(int damage, float k) => Mathf.Max(1, Mathf.RoundToInt(damage * k));

        static Vector2 Dir(in ShootPatterns.Shot s) => s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : Vector2.right;

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * sn, v.x * sn + v.y * c);
        }
    }
}
