namespace TerraKnight.Items
{
    /// <summary>The sweep's swords that shoot their own way (Player.ItemCheck_Shoot).</summary>
    internal static partial class ShootPatterns
    {
        static readonly bool melee2 = AddMelee2();

        static bool AddMelee2()
        {
            foreach (var t in new[]
            {
                ItemType.BluePhaseblade, ItemType.RedPhaseblade, ItemType.GreenPhaseblade, ItemType.PurplePhaseblade, ItemType.WhitePhaseblade, ItemType.YellowPhaseblade,
                ItemType.OrangePhaseblade, ItemType.PinkPhaseblade, ItemType.RainbowPhaseblade, ItemType.BluePhasesaber, ItemType.RedPhasesaber, ItemType.GreenPhasesaber,
                ItemType.PurplePhasesaber, ItemType.WhitePhasesaber, ItemType.YellowPhasesaber, ItemType.OrangePhasesaber, ItemType.PinkPhasesaber, ItemType.RainbowPhasesaber,
                ItemType.Keybrand,
            })
                Add(t, Phase);
            Add(ItemType.AntlionClaw, ThrownMelee);
            Add(ItemType.StylistKilLaKillScissorsIWish, ThrownMelee);
            Add(ItemType.IceSickle, IceSickle);
            Add(ItemType.DD2SquireBetsySword, FlyingDragon);
            Add(ItemType.PiercingStarlight, Starlight);
            Add(ItemType.DayBreak, Daybreak);
            Add(ItemType.Javelin, Daybreak);
            Add(ItemType.BoneJavelin, Daybreak);
            Add(ItemType.MonkStaffT1, MonkStaff);
            Add(ItemType.MonkStaffT2, MonkStaff);
            Add(ItemType.MonkStaffT3, MonkStaff);
            Add(ItemType.VampireKnives, VampireKnives);
            Add(ItemType.DeadCellsFlint, Flint);
            Add(ItemType.SolarEruption, SolarEruption);
            return true;
        }

        /// <summary>Pl:49141-49146: the Solar Eruption's loop flattened by ai[1], up to π/8 either way.</summary>
        static void SolarEruption(in Shot s) =>
            TerraProjectiles.Instance?.ShootHeld(s.Kind, s.From, s.Vel, s.Damage, s.Knockback, s.Crit, (UnityEngine.Random.value - 0.5f) * UnityEngine.Mathf.PI / 4f);

        /// <summary>Pl:48625-48647: four to eight vampire knives, more scattered the later.</summary>
        static void VampireKnives(in Shot s) => TerraProjectiles.Instance?.ThrowVampireKnives(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);

        /// <summary>Pl:49384-49390: the Flint's holder at the player, the way he faces; the swing itself is the holder's to drive.</summary>
        static void Flint(in Shot s) => TerraProjectiles.Instance?.HoldFlint(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse.x < s.From.x ? -1f : 1f);

        /// <summary>
        /// Pl:49230-49250: the monk staffs (NoMeleeSpeedVelocityScaling: Fire's melee speed undone).
        /// The Glaive's ai[0] is up to three quarters of its speed across, the way the player faces;
        /// the Fury on a right click thrusts (708) half again as far, 0.3-1 × 1.75 its speed across,
        /// at half damage and 4 more knockback.
        /// </summary>
        static void MonkStaff(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var v = s.Vel * Equipment.UseScale(s.Def);
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            if (s.Def.Type == ItemType.MonkStaffT2)
            {
                tp.ShootMonk(s.Kind, s.From, v, s.Damage, s.Knockback, s.Crit, UnityEngine.Random.value * s.Def.ShootSpeed * 0.75f * dir, s.UseTicks);
                return;
            }
            if (s.Def.Type == ItemType.MonkStaffT3 && TerraMod.Instance != null && TerraMod.Instance.AltFunction)
            {
                float ai0 = (0.3f + 0.7f * UnityEngine.Random.value) * s.Def.ShootSpeed * 1.75f * dir;
                tp.ShootMonk(ProjKind.MonkStaffT3Thrust, s.From, v * 1.5f, (int)(s.Damage * 0.5f), s.Knockback + 4f, s.Crit, ai0, s.UseTicks);
                return;
            }
            tp.ShootMonk(s.Kind, s.From, v, s.Damage, s.Knockback, s.Crit, 0f, s.UseTicks);
        }

        /// <summary>Pl:47633-47636: Daybreak (and the Javelins) leave from a third of the way down the player (7 px over his centre), aimed from there.</summary>
        static void Daybreak(in Shot s)
        {
            var from = s.From + new UnityEngine.Vector2(0f, 7f * Effects.TerraParticles.Px);
            var to = s.Mouse - from;
            var vel = to.sqrMagnitude > 1e-8f ? to.normalized * s.Vel.magnitude : s.Vel;
            One(s, s.Kind, from, vel, s.Damage, s.Knockback);
        }

        /// <summary>Pl:49619-49624: the Piercing Starlight carries its size in ai[1]: the item's scale, up to twice with melee speed.</summary>
        static void Starlight(in Shot s)
        {
            float ai1 = Equipment.ItemScale(s.Def) * UnityEngine.Mathf.Lerp(1f, 2f, UnityEngine.Mathf.InverseLerp(1f, 1f / 3f, Equipment.UseScale(s.Def)));
            TerraProjectiles.Instance?.ShootHeld(s.Kind, s.From, s.Vel, s.Damage, s.Knockback, s.Crit, ai1);
        }

        /// <summary>Pl:47544-47547: the Ice Sickle's scythe hits for two thirds of the sword.</summary>
        static void IceSickle(in Shot s) => One(s, s.Kind, s.From, s.Vel, (int)(s.Damage * 0.67f), s.Knockback);

        /// <summary>Pl:47655-47658: the Flying Dragon's sonic boom leaves 24 px to the side of the player's centre, towards his head as he faces.</summary>
        static void FlyingDragon(in Shot s)
        {
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            var down = new UnityEngine.Vector2(s.Vel.x, -s.Vel.y);
            if (down.sqrMagnitude < 1e-6f) down = new UnityEngine.Vector2(dir, 0f);
            down.Normalize();
            float a = dir * -UnityEngine.Mathf.PI / 2f;
            var off = new UnityEngine.Vector2(down.x * UnityEngine.Mathf.Cos(a) - down.y * UnityEngine.Mathf.Sin(a), down.x * UnityEngine.Mathf.Sin(a) + down.y * UnityEngine.Mathf.Cos(a)) * 24f;
            One(s, s.Kind, s.From + new UnityEngine.Vector2(off.x, -off.y) * Effects.TerraParticles.Px, s.Vel, s.Damage, s.Knockback);
        }

        /// <summary>Pl:47488-47506: the thrown blade hits a quarter harder (the sabers half as hard again), with a quarter more knockback; not the Keybrand.</summary>
        static void Phase(in Shot s)
        {
            var (damage, knockback) = TerraProjectiles.PhaseThrow(s.Kind, s.Damage, s.Knockback);
            One(s, s.Kind, s.From, s.Vel, damage, knockback);
        }

        /// <summary>
        /// The Mandible Blade's and Stylish Scissors' throw: NoMeleeSpeedVelocityScaling (1082,
        /// 1083), so not sped up by melee speed (Fire divided it: undone), and 0-5 updates nearer
        /// to dropping.
        /// </summary>
        static void ThrownMelee(in Shot s) =>
            TerraProjectiles.Instance?.ShootThrownMelee(s.Kind, s.From, s.Vel * Equipment.UseScale(s.Def), s.Damage, s.Knockback, s.Crit);
    }
}
