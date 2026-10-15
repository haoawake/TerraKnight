using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// How the sweep R4a guns and bows shoot (Player.ItemCheck_Shoot): the shotguns' pellets, the
    /// Onyx Blaster's blast, the Xenopopper's bubbles, Aerial Bane's fan, the arrows the Bee's Knees
    /// and the Hellwing make their own, the Toxikarp's bubbles, the Pew-matic Horn's things and the
    /// Super Star Shooter pulled back from a wall. The asymmetric spreads are worked in Terraria's
    /// y-down space, as there.
    /// </summary>
    internal static partial class ShootPatterns
    {
        static readonly bool guns =
            Add(ItemType.Shotgun, Shotgun) & Add(ItemType.QuadBarrelShotgun, QuadBarrel) & Add(ItemType.OnyxBlaster, OnyxBlaster) &
            Add(ItemType.Xenopopper, Xenopopper) & Add(ItemType.AerialBane, AerialBane) & Add(ItemType.BeesKnees, BeesKnees) &
            Add(ItemType.HellwingBow, Hellwing) & Add(ItemType.Toxikarp, Toxikarp) & Add(ItemType.PewMaticHorn, PewMatic) & Add(ItemType.SuperStarCannon, SuperStar) &
            Add(ItemType.VortexBeater, HeldBow) & Add(ItemType.Phantasm, HeldBow) & Add(ItemType.PhantomPhoenix, HeldBow) &
            Add(ItemType.Eventide, Eventide) & Add(ItemType.PiranhaGun, PiranhaGun) &
            Add(ItemType.GrenadeLauncher, Launcher) & Add(ItemType.RocketLauncher, Launcher) & Add(ItemType.ProximityMineLauncher, Launcher) & Add(ItemType.SnowmanCannon, Launcher) &
            Add(ItemType.Celebration, Celebration) & Add(ItemType.CelebrationMk2, HeldBow) & Add(ItemType.ElectrosphereLauncher, Electrosphere) &
            Add(ItemType.Sandgun, Sandgun) & Add(ItemType.Flamethrower, Flames) & Add(ItemType.ElfMelter, Flames) &
            Add(ItemType.SapphireStaff, GemStaff) & Add(ItemType.EmeraldStaff, GemStaff) & Add(ItemType.AmberStaff, GemStaff) &
            Add(ItemType.AmethystStaff, GemStaff) & Add(ItemType.TopazStaff, GemStaff) & Add(ItemType.RubyStaff, GemStaff) & Add(ItemType.DiamondStaff, GemStaff) &
            Add(ItemType.PaintballGun, Paintball);

        /// <summary>Pl:49542-49553: the paint jittered and stepped forward (TerraProjectiles.ShootPaintball).</summary>
        static void Paintball(in Shot s) => TerraProjectiles.Instance?.ShootPaintball(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);

        /// <summary>
        /// Pl:49779-49890, 49988-50007 (GemStaffFeatures): the bolt's features are its gem's and the worn robe's
        /// (GetEffectiveArmor(1)); the Ruby's (ArPenSpread) fires again halfway through the use (Pl:43030-43037).
        /// </summary>
        static void GemStaff(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var own = TerraProjectiles.GemOf(s.Kind);
            var body = TerraMod.Instance != null ? TerraMod.Instance.Inventory.EffectiveArmor[1] : default;
            var robe = body.IsEmpty ? TerraProjectiles.GemFx.None : TerraProjectiles.RobeGem(body.Type);
            var fx = own | robe;
            if (robe != TerraProjectiles.GemFx.None && robe == own) fx |= TerraProjectiles.GemFx.Repeats;
            FireGem(tp, s, fx, s.From, s.Vel);
            if ((fx & TerraProjectiles.GemFx.ArPen) != 0 && s.UseTicks > 1)
            {
                var copy = s;
                tp.Later(s.UseTicks / 2, (from, dir) => { if (TerraProjectiles.Instance != null) FireGem(TerraProjectiles.Instance, copy, fx, from, dir * copy.Speed); });
            }
        }

        /// <summary>
        /// One use's bolts: FastThenSlow five times as fast, CanBounce 25% faster, ArPenSpread from a point 12 px off; the
        /// damage at the least of SwirlTwins' 60%, CanBounce's 80% and ArPenSpread's 50%; RepeatsGem 4 more damage and 1 more
        /// knockback; SwirlTwins a twin.
        /// </summary>
        static void FireGem(TerraProjectiles tp, in Shot s, TerraProjectiles.GemFx fx, Vector2 from, Vector2 vel)
        {
            bool Has(TerraProjectiles.GemFx f) => (fx & f) != 0;
            if (Has(TerraProjectiles.GemFx.FastSlow)) vel *= 5f;
            if (Has(TerraProjectiles.GemFx.Bounce)) vel *= 1.25f;
            if (Has(TerraProjectiles.GemFx.ArPen)) from += Random.insideUnitCircle.normalized * 12f * TerraParticles.Px;
            float mult = 1f;
            if (Has(TerraProjectiles.GemFx.Swirl)) mult = Mathf.Min(mult, 0.6f);
            if (Has(TerraProjectiles.GemFx.Bounce)) mult = Mathf.Min(mult, 0.8f);
            if (Has(TerraProjectiles.GemFx.ArPen)) mult = Mathf.Min(mult, 0.5f);
            int damage = (int)(s.Damage * mult);
            float kb = s.Knockback;
            if (Has(TerraProjectiles.GemFx.Repeats))
            {
                damage += 4;
                kb += 1f;
            }
            tp.ShootGem(s.Kind, from, vel, damage, kb, s.Crit, 0f, fx);
            if (Has(TerraProjectiles.GemFx.Swirl)) tp.ShootGem(s.Kind, from, vel, damage, kb, s.Crit, 1f, fx);
        }

        /// <summary>Five bursts 6 ticks apart (useTime 6 of a 30 tick use), each aimed where the player then aims, with the use sound (Item34) each.</summary>
        static void Flames(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var copy = s;
            bool elf = s.Def.Type == ItemType.ElfMelter;
            float Facing(Vector2 dir) => dir.x < 0f ? -1f : 1f;
            tp.ShootFlame(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, elf, Facing(s.Vel));
            for (int k = 1; k < 5; k++)
                tp.Later(6 * k, (from, dir) =>
                {
                    TerraProjectiles.Instance?.FlameSound();
                    TerraProjectiles.Instance?.ShootFlame(from, dir * copy.Speed, copy.Damage, copy.Knockback, copy.Crit, elf, Facing(dir));
                });
        }

        /// <summary>Pl:54156-54172: the Sandgun's ball by the sand it uses, the corrupt, hallowed and crimson ones 5 harder.</summary>
        static void Sandgun(in Shot s)
        {
            var sand = s.Ammo != null ? s.Ammo.Type : ItemType.SandBlock;
            var kind = sand switch { ItemType.EbonsandBlock => ProjKind.EbonsandBall, ItemType.PearlsandBlock => ProjKind.PearlsandBall, ItemType.CrimsandBlock => ProjKind.CrimsandBall, _ => ProjKind.SandBall };
            int bonus = kind == ProjKind.SandBall ? 0 : (int)(5 * (1f + Equipment.Current.RangedDamage));
            One(s, kind, s.From, s.Vel, s.Damage + bonus, s.Knockback);
        }

        /// <summary>Pl:49529-49541: two fireworks.</summary>
        static void Celebration(in Shot s) => TerraProjectiles.Instance?.ShootCelebration(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse.x < s.From.x ? -1f : 1f);

        /// <summary>Pl:47574-47577, 49906-49910: the missile, ending at the cursor's block.</summary>
        static void Electrosphere(in Shot s) => TerraProjectiles.Instance?.ShootElectrosphere(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, TerraParticles.ToPx(s.Mouse));

        /// <summary>PickAmmo (Pl:54092-54119): the launcher's own shot for the rocket used.</summary>
        static void Launcher(in Shot s)
        {
            var kind = TerraProjectiles.LauncherShot(s.Def.Type, s.Ammo != null ? s.Ammo.Type : ItemType.RocketI);
            if (kind != ProjKind.None) One(s, kind, s.From, s.Vel, s.Damage, s.Knockback);
        }

        /// <summary>Pl:43836-43843, 48352-48401: Eventide's five shots, 2 ticks apart, each fanned and aimed where the player then aims.</summary>
        static void Eventide(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var copy = s;
            Vector2 Mouse() => TerraMod.Instance != null ? TerraMod.Instance.DevAim ?? TerraMod.Instance.AimPoint : copy.Mouse;
            tp.ShootEventide(s.Kind, s.From, s.Vel, s.Mouse, s.Speed, s.Damage, s.Knockback, s.Crit, 0);
            for (int k = 1; k < 5; k++)
            {
                int n = k;
                tp.Later(2 * k, (from, dir) => tp.ShootEventide(copy.Kind, from, dir, Mouse(), copy.Speed, copy.Damage, copy.Knockback, copy.Crit, n));
            }
        }

        /// <summary>Pl:48845-48862: piranhas to make up three.</summary>
        static void PiranhaGun(in Shot s) => TerraProjectiles.Instance?.ShootPiranhas(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);

        /// <summary>Pl:49496-49528: the held weapon itself (the Vortex Beater's and the Mk2's count started at 5 × 0-19, so their rockets come at random).</summary>
        static void HeldBow(in Shot s) =>
            TerraProjectiles.Instance?.ShootHeldBow(s.Def.Shoot, s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Def.Type == ItemType.VortexBeater || s.Def.Type == ItemType.CelebrationMk2 ? 5f * Next(0, 20) : 0f);

        /// <summary>y up and y down swap.</summary>
        static Vector2 YFlip(Vector2 v) => new Vector2(v.x, -v.y);

        static Vector2 Turn(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Main.rand.NextFloatDirection: -1 or 1.</summary>
        static float Sign() => Next(0, 2) == 0 ? -1f : 1f;

        /// <summary>Pl:48405-48415: four or five pellets, each up to 2 px off on either axis.</summary>
        static void Shotgun(in Shot s)
        {
            for (int k = Next(4, 6); k > 0; k--) Spray(s, s.Kind, s.Vel + Jitter(40, 0.05f));
        }

        /// <summary>Pl:48418-48433: one straight, seven more bent up to a quarter turn off by as much as 5 px, then up to 2 px off.</summary>
        static void QuadBarrel(in Shot s)
        {
            Spray(s, s.Kind, s.Vel);
            var v0 = YFlip(s.Vel);
            float len = v0.magnitude;
            var n = len > 1e-6f ? v0 / len : Vector2.zero;
            for (int k = 0; k < 7; k++)
            {
                var v = v0 + Turn(n, Mathf.PI / 2f * Random.value) * Sign() * 5f;
                v = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero) * len;
                Spray(s, s.Kind, YFlip(v) + Jitter(40, 0.05f));
            }
        }

        /// <summary>Pl:49334-49345: four rounds bent an eighth to a quarter turn either way by up to 2 px, and the blast (661) 30% faster at twice the damage.</summary>
        static void OnyxBlaster(in Shot s)
        {
            var v = YFlip(s.Vel);
            var n = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
            const float eighth = Mathf.PI / 4f;
            for (int k = 0; k < 2; k++)
            {
                Spray(s, s.Kind, YFlip(v + Turn(n, eighth * (Random.value * 0.5f + 0.5f)) * Sign() * 2f));
                Spray(s, s.Kind, YFlip(v + Turn(n, -eighth * (Random.value * 0.5f + 0.5f)) * Sign() * 2f));
            }
            var dir = s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : new Vector2(s.Mouse.x < s.From.x ? -1f : 1f, 0f);
            One(s, ProjKind.OnyxBlast, s.From, dir * s.Speed * 1.3f, s.Damage * 2, s.Knockback);
        }

        /// <summary>
        /// Pl:48022-48044: from 40 px out along the aim (if open), four bubbles (five, one time in
        /// four) anywhere within a third of a turn of it at 5-25% speed, each to turn back to the
        /// aim and carry the round on.
        /// </summary>
        static void Xenopopper(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var from = s.From;
            var reach = (s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : Vector2.right) * 40f * s.Def.Scale;
            if (!Blocked(from, from + reach * Px)) from += reach * Px;
            var v = YFlip(s.Vel);
            float aim = Mathf.Atan2(v.y, v.x), third = Mathf.PI * 2f / 3f;
            int n = 4 + (Next(0, 4) == 0 ? 1 : 0);
            for (int k = 0; k < n; k++)
            {
                float f = Random.value * 0.2f + 0.05f;
                var b = Turn(v, third * Random.value - third / 2f) * f;
                tp.ShootXenoBubble(from, YFlip(b), s.Damage, s.Knockback, s.Crit, aim, s.Kind, s.Speed);
            }
        }

        /// <summary>
        /// Pl:49252-49262: six arrows (710) at 80% speed, side by side across the aim half a px
        /// apart and each a degree further turned against the way the player faces.
        /// </summary>
        static void AerialBane(in Shot s)
        {
            var v = YFlip(s.Vel) * 0.8f;
            var n = v.sqrMagnitude > 1e-6f ? v.normalized : new Vector2(0f, -1f);
            float dir = s.Mouse.x < s.From.x ? -1f : 1f, deg = Mathf.Deg2Rad * -dir;
            for (float k = -2.5f; k < 3f; k++) Spray(s, ProjKind.BetsyArrow, YFlip(Turn(v + n * k * 0.5f, k * deg)));
        }

        /// <summary>Pl:54174-54177: wooden arrows go as bee arrows (469).</summary>
        static void BeesKnees(in Shot s) => Spray(s, s.Kind == ProjKind.WoodenArrow ? ProjKind.BeeArrow : s.Kind, s.Vel);

        /// <summary>
        /// Pl:47990-47997, 54136-54139: wooden arrows go as bats (485); every shot a sixteenth of a
        /// turn off at most, bending toward a way up to an eighth off.
        /// </summary>
        static void Hellwing(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var v = YFlip(s.Vel);
            var bend = Turn(v, Random.value * 0.3926991f - 0.19634955f);
            var go = Turn(v, Random.value * 0.19634955f - 0.09817477f);
            var kind = s.Kind == ProjKind.WoodenArrow ? ProjKind.HellwingBat : s.Kind;
            tp.ShootHellwing(kind, s.From, YFlip(go), s.Damage, s.Knockback, s.Crit, bend);
        }

        /// <summary>Pl:48889-48900: up to 1.2 px (and 0.9 up or down) off, set to 7-9 px a tick, off again; rising after 10-30 updates.</summary>
        static void Toxikarp(in Shot s)
        {
            var v = YFlip(s.Vel);
            v.x += Next(-30, 31) * 0.04f;
            v.y += Next(-30, 31) * 0.03f;
            v = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right) * (Next(70, 91) * 0.1f);
            v.x += Next(-30, 31) * 0.04f;
            v.y += Next(-30, 31) * 0.03f;
            var tp = TerraProjectiles.Instance;
            tp?.ShootToxicBubble(s.From, YFlip(v), s.Damage, s.Knockback, s.Crit, Next(0, 20));
        }

        /// <summary>
        /// Pl:47582, 47637, 48058-48066: any round goes as one of the horn's 24 things (968), from a
        /// third of the way down the player, up to 1.1 px off.
        /// </summary>
        static void PewMatic(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var from = s.From + Vector2.up * 7f * Px;
            var v = s.Vel + new Vector2(Next(-15, 16) * 0.075f, Next(-15, 16) * 0.075f);
            tp.ShootPewMatic(from, v, s.Damage, s.Knockback, s.Crit, Next(0, 24));
        }

        /// <summary>Pl:47730-47734: from just behind the player when two steps ahead is in a wall.</summary>
        static void SuperStar(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var from = s.From;
            if (Blocked(s.From, s.From + s.Vel * 2f * Px)) from = s.From - s.Vel * 0.25f * Px;
            One(s, s.Kind, from, s.Vel, s.Damage, s.Knockback);
        }
    }
}
