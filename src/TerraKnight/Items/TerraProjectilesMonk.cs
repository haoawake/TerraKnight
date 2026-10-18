using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Old One's Army monk staffs (sweep S1.E; Terraria's space, px y down): the Sleepy
    /// Octopod (697, AI_140) whirled twice round the player, speeding up, hitting all within 73 px,
    /// and slammed down at the end (698, AI_141: three times the damage in a tall box, a green
    /// burst and the ground thrown up); the Ghastly Glaive (699, AI_142) thrust out and back along
    /// an ellipse, calling a ghast (700, AI_143) through an enemy on its first hit; and the Sky
    /// Dragon's Fury: whirled like the Octopod (707, reaching 110 px), or, with a right click,
    /// thrust (708) loosing three dragon bolts (709) that burst into crackling fields (1110).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMonk()
        {
            // P:7368-7385, 7476-7492: whirled at the player, through everything; each enemy every 12 (6) ticks, only if the player could see it.
            foreach (var (k, id, scale, cd, reach) in new[] { (ProjKind.MonkStaffT1Spin, 697, 1.35f, 12, 73f), (ProjKind.MonkStaffT3Spin, 707, 1f, 6, 110f) })
                specs[k] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = 16, H = 16, Scale = scale, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                    AlphaStart = 1f, LocalCooldown = cd, OwnerHitCheck = true,
                    // P:15992-16013: what's hit is whatever comes within its reach of the player's centre.
                    HitArea = p => (Holder(out var c) ? TerraParticles.ToWorld(c) : p.Pos, Vector2.one * reach * 2f * Px),
                    HitTest = (p, col) => Holder(out var c) && WithinOfBox(c, col, reach),
                    Update = (t, p) => t.StepMonkSpin(p), Draw = (t, fx, p, px) => DrawMonkSpin(fx, p, px), OnKill = (t, p) => { },
                };
            // P:7386-7403: the slam, unseen, 75×200, each enemy once, for 5 ticks.
            specs[ProjKind.MonkStaffT1Slam] = new Spec
            {
                Ai = Ai.Custom, W = 75, H = 200, Time = 5, Penetrate = -1, OncePerNpc = true, OwnerHitCheck = true, TileCollide = false, OwnMove = true,
                Look = Look.Invisible, Update = (t, p) => t.StepMonkSlam(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:7404-7420, 7493-7509: thrust along an ellipse; the Glaive any enemy every 10 ticks for all of them, the Fury's each every 6.
            specs[ProjKind.MonkStaffT2] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_699", W = 24, H = 24, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                AlphaStart = 1f, TypeCooldown = 10, OwnerHitCheck = true,
                // P:16015-16028: a line 95 px back from its point, 23 wide.
                Beam = p => (CenterPx(p), CenterPx(p) + GlaiveAxis(p) * -95f, 23f * p.Scale),
                Update = (t, p) => t.StepMonkThrust(p), Draw = (t, fx, p, px) => DrawMonkThrust(fx, p, px), OnHit = (t, p, hm) => t.SummonMonkGhast(p),
                OnKill = (t, p) => { },
            };
            specs[ProjKind.MonkStaffT3Thrust] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_708", W = 24, H = 24, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                AlphaStart = 1f, LocalCooldown = 6, OwnerHitCheck = true,
                Update = (t, p) => t.StepMonkThrust(p), Draw = (t, fx, p, px) => DrawMonkThrust(fx, p, px), OnKill = (t, p) => { },
            };
            // P:7421-7434: the ghast, 40×40, through everything, each enemy once.
            specs[ProjKind.MonkStaffT2Ghast] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_700", W = 40, H = 40, Time = 3600, Penetrate = -1, OncePerNpc = true, TileCollide = false, AlphaStart = 1f,
                Frames = 4, TrailLength = 10, Update = (t, p) => StepMonkGhast(p), Draw = (t, fx, p, px) => DrawMonkGhast(fx, p, px), OnKill = (t, p) => { },
            };
            // P:7510-7521: the dragon bolt, 12×12 at 0.6, one more update a tick, one enemy.
            specs[ProjKind.SkyDragonBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_709", W = 12, H = 12, Scale = 0.6f, Extra = 1, Time = 3600, Penetrate = 1, AlphaStart = 1f, Frames = 3,
                Update = (t, p) => StepSkyDragonBolt(p), Draw = (t, fx, p, px) => DrawSkyDragonBolt(fx, p, px), OnKill = (t, p) => t.KillSkyDragonBolt(p),
            };
            // P:10342-10353, 14073: the field, 80×80 breathing, through everything; an enemy it hurts is safe from the player for 8 ticks.
            specs[ProjKind.SkyDragonField] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1110", W = 80, H = 80, Time = 120, Penetrate = -1, TileCollide = false, AlphaStart = 1f, OwnerImmune = 8, Frames = 4,
                CanDamage = p => p.Ai1 != 1f, Update = (t, p) => t.StepSkyDragonField(p), Draw = (t, fx, p, px) => DrawSkyDragonField(fx, p, px), OnKill = (t, p) => { },
            };
        }

        /// <summary>Whether the closest point of an enemy's box is within a distance (Terraria px) of a point.</summary>
        static bool WithinOfBox(Vector2 px, Collider2D col, float reach)
        {
            var b = col.bounds;
            var tl = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
            var br = TerraParticles.ToPx(new Vector2(b.max.x, b.min.y));
            var closest = new Vector2(Mathf.Clamp(px.x, tl.x, br.x), Mathf.Clamp(px.y, tl.y, br.y));
            return (closest - px).magnitude <= reach;
        }

        /// <summary>A monk staff from the player: velocity y up (its length is the thrust's reach), ai[0], and the use's length in ticks.</summary>
        public void ShootMonk(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float ai0, int useTicks)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.Ai0 = ai0;
            p.Ai1 = Mathf.Max(3, useTicks);
            p.Dir = velocity.x < 0f ? -1f : 1f;
        }

        static ItemType MonkItem(ProjKind k) =>
            k == ProjKind.MonkStaffT1Spin ? ItemType.MonkStaffT1 : k == ProjKind.MonkStaffT2 ? ItemType.MonkStaffT2 : ItemType.MonkStaffT3;

        // ------------------------------------------------------------------ 697, 707: whirled (AI_140)

        /// <summary>
        /// AI_140 (P:70493-70683): two turns over 50 counts, starting overhead behind; the Octopod's
        /// count speeds up from 1 to 1.5 a tick. At half way, with the button up it stops (else the
        /// player turns to the cursor); at the end it stops, reuse 10 (2) ticks later. It rides 0-20
        /// px out along itself from the player's centre. The Octopod sheds smoke (31), green motes
        /// (55) at its end once 85% through, a burst of them near the end and slams down at count 47
        /// (698 at three times the damage, 25 more crit); the Fury four blue streams (226) and light.
        /// </summary>
        void StepMonkSpin(Proj p)
        {
            var item = MonkItem(p.Kind);
            var def = ItemDb.Get(item);
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || mod.Inventory.Current.Type != item)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            const float num = 50f, turns = 2f, reach = 20f;
            bool octopod = p.Kind == ProjKind.MonkStaffT1Spin;
            int dir = p.Vel.x < 0f ? -1 : 1;
            if (p.Ai0 == 0f)
            {
                p.TRot = Mathf.Atan2(-1f, dir) - Mathf.PI / 4f + Mathf.PI;
                if (dir < 0) p.TRot -= Mathf.PI / 2f;
            }
            p.Alpha = Mathf.Max(0f, p.Alpha - 128f / 255f);
            float frac = p.Ai0 / num;
            float step = octopod ? 1f + p.Ai0 / num * 0.5f : 1f;
            bool before = p.Ai0 < (int)(num / 2f);
            p.Ai0 += step;
            p.TRot += Mathf.PI * 2f * turns / num * dir * step;
            bool half = octopod ? before && p.Ai0 >= (int)(num / 2f) : p.Ai0 == (int)(num / 2f);
            if (p.Ai0 >= num || (half && !mod.UseHeld))
            {
                Kill(p);
                mod.ReuseDelay(octopod ? 10 : 2);
                return;
            }
            if (half)
            {
                int face = AimPx().x > centre.x ? 1 : -1;
                if (face != dir)
                {
                    dir = face;
                    p.TRot -= Mathf.PI;
                }
            }
            p.Vel = new Vector2(dir, 0f);
            float axis = p.TRot - Mathf.PI / 4f * dir + (dir == -1 ? Mathf.PI : 0f);
            var unit = new Vector2(Mathf.Cos(axis), Mathf.Sin(axis));
            var end = CenterPx(p) + unit * 30f;
            var pp = TerraParticles.Instance;
            var hero = HeroController.SilentInstance;
            var heroV = hero != null ? Flip(hero.GetComponent<Rigidbody2D>().linearVelocity / Px / 60f) : Vector2.zero;
            var c = CenterPx(p);
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(end - new Vector2(5f, 5f), 10, 10, 31, heroV.x, heroV.y, 150);
                if (i >= 0) D(i).Velocity = (D(i).Position - c).normalized * 0.1f + D(i).Velocity * 0.1f;
            }
            if (octopod)
            {
                var green = FxBatch.C(20, 255, 100, 160);
                if (frac >= 0.85f)
                {
                    int i = BoxDust(end - new Vector2(5f, 5f), 10, 10, 55, heroV.x, heroV.y, 50);
                    if (i >= 0)
                    {
                        D(i).Velocity = (D(i).Position - c).normalized * 0.1f + D(i).Velocity * 0.1f;
                        D(i).NoGravity = true;
                        D(i).Color = green;
                    }
                }
                if (p.Ai0 >= num - 8f && p.Ai0 < num - 2f)
                    for (int k = 0; k < 5; k++)
                    {
                        int i = BoxDust(end - new Vector2(5f, 5f), 10, 10, 55, heroV.x, heroV.y, 50);
                        if (i < 0) continue;
                        D(i).Velocity *= 1.2f;
                        D(i).NoGravity = true;
                        D(i).Scale += 0.1f;
                        D(i).Color = green;
                    }
                if (p.Ai0 >= num - 3f && p.Ai0 - step < num - 3f)
                {
                    var slam = MeleeChild(ProjKind.MonkStaffT1Slam, end + new Vector2(dir * -15f, -60f), new Vector2(dir, 0f), p.Damage * 3, 0f, p.Crit + 25f);
                    if (slam != null) slam.Vel = new Vector2(dir, 0f);
                    Sound("Custom/dd2_monk_staff_ground_impact_" + Random.Range(0, 3));
                    DevMonkSlams++;
                }
            }
            else
            {
                FxBatch.Instance?.Light(centre, new Color(0.75f, 0.9f, 1.15f), 3f);
                var a5 = new Vector2(Mathf.Cos(axis - (dir == -1 ? Mathf.PI : 0f)), Mathf.Sin(axis - (dir == -1 ? Mathf.PI : 0f)));
                var a6 = RotateBy(a5, Mathf.PI / 2f * dir);
                for (int j = 0; j < 4; j++)
                {
                    float m16 = j >= 2 ? 0.5f : 1f, m17 = j == 0 ? 1f : j == 1 ? -1f : j == 2 ? 1.25f : -1.25f;
                    if (Random.Range(0, 6) == 0) continue;
                    int i = BoxDust(c, 0, 0, 226, 0f, 0f, 100);
                    if (i < 0) continue;
                    ref var d = ref D(i);
                    d.Position = c + a5 * (60f + Random.value * 20f) * m17;
                    d.Velocity = a6 * (4f + 4f * Random.value) * m17 * m16;
                    d.NoGravity = Random.Range(0, 4) != 0;
                    d.Scale = 0.5f;
                }
            }
            p.Pos = TerraParticles.ToWorld(centre + unit * (p.Ai0 / num) * reach);
            p.Dir = dir;
            // itemRotation is the staff's own turn; the arm follows it as Terraria's useStyle 5 frames do.
            float r = Mathf.Repeat(p.TRot + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            mod.KeepUsing(def, new Vector2(dir * Mathf.Cos(r * dir), -Mathf.Sin(r * dir)), face: dir);
        }

        /// <summary>
        /// Main.DrawProj's held branch: the whole staff about its middle, turned, mirrored with the
        /// player; the Octopod lit, the Fury full bright with its glowing runes (Glow_232, a frame
        /// every 3 counts) four times round it at half and once on top.
        /// </summary>
        static void DrawMonkSpin(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Whole(p.Spec.Texture);
            if (spr == null) return;
            float a = 1f - p.Alpha;
            bool flip = p.Dir < 0f;
            bool octopod = p.Kind == ProjKind.MonkStaffT1Spin;
            fx.Draw(spr, px, octopod ? new Color(L, L, L, 1f) * a : new Color(a, a, a, a), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            if (!octopod) DrawFuryGlow(fx, p, px, ((int)p.Ai0 % 9) / 3, 0.5f);
        }

        /// <summary>Glow_232's frame (of three) over the Fury, four copies 2 px round at half and one on top; strength 0 draws nothing.</summary>
        static void DrawFuryGlow(FxBatch fx, Proj p, Vector2 px, int frame, float strength, float pivotX = -1f, float pivotY = -1f)
        {
            if (strength <= 0f) return;
            var whole = FxBatch.Whole("Glow_232");
            var body = FxBatch.Whole(p.Spec.Texture);
            if (whole == null || body == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 3;
            float ox = pivotX >= 0f ? pivotX : body.rect.width / 2f, oy = pivotY >= 0f ? pivotY : body.rect.height / 2f;
            var spr = FxBatch.Frame("Glow_232", 0, frame * fh, w, fh, ox, oy);
            var c = new Color(1f, 1f, 1f, 127f / 255f) * strength;
            var off = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot)) * 2f;
            bool flip = p.Dir < 0f;
            for (int k = 0; k < 4; k++) fx.Draw(spr, px + RotateBy(off, Mathf.PI / 2f * k), c * 0.5f, p.TRot, p.Scale, flipX: flip, overPlayer: true);
            fx.Draw(spr, px, c, p.TRot, p.Scale, flipX: flip, overPlayer: true);
        }

        // ------------------------------------------------------------------ 698: the Octopod's slam (AI_141)

        /// <summary>
        /// AI_141 (P:70685-70703): on its first tick the ground under its foot is thrown up (each open
        /// block top within 75 px: 47 of its motes flung up and its way) and a green ring (55) bursts
        /// there, twice as much of it when there's no ground.
        /// </summary>
        void StepMonkSlam(Proj p)
        {
            if (++p.Ai0 != 1f) return;
            var c = CenterPx(p);
            var bottom = c + new Vector2(0f, 100f);
            float dir = p.Vel.x < 0f ? -1f : 1f;
            bool shook = false;
            var pp = TerraParticles.Instance;
            int i0 = Mathf.FloorToInt((c.x - 37.5f) / 16f), i1 = Mathf.FloorToInt((c.x + 37.5f) / 16f);
            int j0 = Mathf.FloorToInt((c.y - 100f) / 16f), j1 = Mathf.FloorToInt((c.y + 100f) / 16f) + 1;
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (Vector2.Distance(bottom, new Vector2(i * 16f, j * 16f)) > 75f) continue;
                    var cell = new Vector2Int(i, -j - 1);
                    if (!TerraWorld.IsSolid(cell.x, cell.y) || TerraWorld.IsSolid(i, -j)) continue;
                    shook = true;
                    if (pp == null) continue;
                    int type = TerraMod.TileDustType(TerraWorld.Get(cell.x, cell.y));
                    var tl = new Vector2(i * 16f, j * 16f);
                    for (int k = 0; k < 24; k++)
                    {
                        int d = pp.NewDust(tl, 16, 16, type);
                        if (d < 0) continue;
                        pp[d].Velocity.y -= 9f;
                        pp[d].Velocity.y *= Random.value;
                        pp[d].Velocity.x += dir * 3f * (0.3f + 0.7f * Random.value);
                        pp[d].Scale *= 0.75f;
                        pp[d].Scale += 0.12f;
                    }
                    for (int k = 0; k < 23; k++)
                    {
                        int d = pp.NewDust(tl, 16, 16, type);
                        if (d < 0) continue;
                        pp[d].Velocity.y -= 5f;
                        pp[d].Velocity.y *= Random.value;
                        pp[d].Velocity.x += dir * 3f;
                        pp[d].Scale *= 0.75f;
                    }
                }
            // CreateImpactExplosion2_SleepyOctopod: a flat ring of green motes 10 px behind its foot, thrown up.
            var origin = bottom - new Vector2(dir, 0f) * 10f;
            var green = FxBatch.C(20, 255, 100, 200);
            for (int pass = 0; pass < (shook ? 1 : 2); pass++)
                for (int k = 0; k < 25; k++)
                {
                    float a = k * Mathf.PI * 2f / 25f;
                    int d = BoxDust(c - new Vector2(37.5f, 100f), 75, 200, 55);
                    if (d < 0) continue;
                    ref var dd = ref D(d);
                    dd.Color = green;
                    dd.Position = origin + new Vector2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f * 0.7f);
                    if (pass == 0)
                    {
                        dd.Velocity.y -= 3f;
                        dd.Velocity.x = dd.Velocity.x * 0.5f + dir * 3f * (0.3f + 0.7f * Random.value);
                    }
                    else
                    {
                        dd.Velocity.y -= 5f;
                        dd.Velocity.x = dd.Velocity.x * 0.8f + dir * 3f;
                    }
                    dd.FadeIn = 0.5f + Random.value * 0.5f;
                }
        }

        // ------------------------------------------------------------------ 699, 708: thrust (AI_142)

        /// <summary>The Glaive's shaft, from its point back toward the hands (Colliding's f5).</summary>
        static Vector2 GlaiveAxis(Proj p)
        {
            var v = TV(p);
            float f5 = p.TRot - Mathf.PI / 4f * Mathf.Sign(v.x == 0f ? 1f : v.x) + (p.Dir < 0f ? Mathf.PI : 0f);
            return new Vector2(Mathf.Cos(f5), Mathf.Sin(f5));
        }

        /// <summary>
        /// AI_142 (P:70900-71062): over the use, out and back along an ellipse its speed long and
        /// ai[0] wide, 22 px beyond the hands; it points at a spot 40 px further on the same path.
        /// The Glaive trails ghostly wisps (228) round its point and back along the shaft; the
        /// Fury's thrust looses a dragon bolt at 10, 6 and 2 ticks left (straight, then 22° either
        /// side, 10-13 px an update) and sheds blue streams (226). Gone with 2 ticks left.
        /// </summary>
        void StepMonkThrust(Proj p)
        {
            var mod = TerraMod.Instance;
            var item = MonkItem(p.Kind);
            if (mod == null || !Holder(out var centre) || mod.Inventory.Current.Type != item)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            int n = (int)p.Ai1;
            int left = n - (int)++p.LocalAi1;
            float dir = mod.UseDirection;
            p.Dir = dir;
            p.Alpha = Mathf.Max(0f, p.Alpha - 127f / 255f);
            float t = 1f - left / (float)n;
            var v = TV(p);
            float rot = ToRot(v), len = v.magnitude;
            float a = Mathf.PI + t * Mathf.PI * 2f;
            var swing = RotateBy(new Vector2(Mathf.Cos(a) * len, Mathf.Sin(a) * p.Ai0), rot);
            var at = centre + swing + RotateBy(new Vector2(len + 22f, 0f), rot);
            var target = centre + swing + RotateBy(new Vector2(len + 22f + 40f, 0f), rot);
            p.Pos = TerraParticles.ToWorld(at);
            p.TRot = ToRot(target - centre) + Mathf.PI / 4f * dir + (dir < 0f ? Mathf.PI : 0f);
            var vn = len > 1e-4f ? v / len : Vector2.up;
            if (p.Kind == ProjKind.MonkStaffT2)
            {
                var toTarget = (target - centre).normalized;
                for (int i = 0; i < 2; i++)
                {
                    int d = BoxDust(at, 14, 14, 228, 0f, 0f, 110);
                    if (d < 0) continue;
                    ref var dd = ref D(d);
                    dd.Velocity = (dd.Position - centre).normalized * 2f;
                    dd.Position = at + RotateBy(vn, t * Mathf.PI * 2f * 2f + i / 2f * Mathf.PI * 2f) * 10f;
                    dd.Scale = 1f + 0.6f * Random.value;
                    dd.Velocity += vn * 3f;
                    dd.NoGravity = true;
                }
                if (Random.Range(0, 3) == 0)
                {
                    int d = BoxDust(at, 20, 20, 228, 0f, 0f, 110);
                    if (d >= 0)
                    {
                        ref var dd = ref D(d);
                        dd.Velocity = (dd.Position - centre).normalized * 2f;
                        dd.Position = at + toTarget * -110f;
                        dd.Scale = 0.45f + 0.4f * Random.value;
                        dd.FadeIn = 0.7f + 0.4f * Random.value;
                        dd.NoGravity = true;
                    }
                }
            }
            else
            {
                FxBatch.Instance?.Light(centre, new Color(0.75f, 0.9f, 1.15f), 3f);
                if (left == 2 || left == 6 || left == 10)
                {
                    float turn = left == 2 ? 0.3839724659919739f : left == 6 ? -0.3839724659919739f : 0f;
                    var bolt = RotateBy(vn, turn) * (10f + Random.Range(0, 4));
                    MeleeChild(ProjKind.SkyDragonBolt, at, bolt, p.Damage, 0f, p.Crit);
                    DevSkyBolts++;
                }
                for (int k = 0; k < 3; k += 2)
                {
                    float m12 = k == 2 ? 0.5f : 1f, m13 = (k == 2 ? 1.25f : 1f) * 1.2f;
                    if (Random.Range(0, 6) == 0) continue;
                    int d = BoxDust(at - new Vector2(12f, 12f), 24, 24, 226, 0f, 0f, 100);
                    if (d < 0) continue;
                    ref var dd = ref D(d);
                    dd.Velocity = vn * (4f + 4f * Random.value) * m13 * m12;
                    dd.NoGravity = true;
                    dd.Scale = 0.75f;
                    dd.FadeIn = 0.8f;
                    if (Random.Range(0, 3) == 0)
                    {
                        dd.NoGravity = false;
                        dd.FadeIn = 0f;
                    }
                }
            }
            if (left <= 2)
            {
                Kill(p);
                mod.ReuseDelay(2);
            }
        }

        /// <summary>
        /// Main.DrawProj's held branch for 699/708: the staff pivoting 8 px out past its top corner
        /// (the far one from the player), lit; the Glaive's ghostly edge (Glow_231) half added over
        /// it, the Fury's runes (Glow_232) at three quarters, a frame every 3 ticks of the use.
        /// </summary>
        static void DrawMonkThrust(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int w = (int)whole.rect.width, h = (int)whole.rect.height;
            // XNA keeps the origin where it is as it mirrors the picture; Unity mirrors about the pivot: the pivot is the mirrored origin.
            var spr = FxBatch.Frame(p.Spec.Texture, 0, 0, w, h, w + 8f, -8f);
            bool flip = p.Dir < 0f;
            float a = 1f - p.Alpha;
            fx.Draw(spr, px, new Color(L, L, L, 1f) * a, p.TRot, p.Scale, flipX: flip, overPlayer: true);
            if (p.Kind == ProjKind.MonkStaffT2)
            {
                var glow = FxBatch.Whole("Glow_231");
                if (glow != null)
                    fx.Draw(FxBatch.Frame("Glow_231", 0, 0, (int)glow.rect.width, (int)glow.rect.height, w + 8f, -8f), px, new Color(1f, 1f, 1f, 127f / 255f), p.TRot, p.Scale,
                        flipX: flip, overPlayer: true);
                return;
            }
            int left = (int)p.Ai1 - (int)p.LocalAi1;
            DrawFuryGlow(fx, p, px, (Mathf.Max(0, left) % 9) / 3, 0.75f, w + 8f, -8f);
        }

        /// <summary>
        /// Projectile.SummonMonkGhast (P:15238-15266): once a thrust, a ghast 120 px to one side of
        /// a random enemy within 800 px (else of the glaive), drifting 4-6 px a tick across it and
        /// 2 more toward where the enemy will be in 15 ticks; its damage, no knockback.
        /// </summary>
        void SummonMonkGhast(Proj p)
        {
            if (p.Spent) return;
            p.Spent = true;
            var c = CenterPx(p);
            var near = EnemiesNear(c, 800f);
            var centre = c;
            var vel = Vector2.zero;
            if (near.Count > 0)
            {
                var hm = near[Random.Range(0, near.Count)];
                centre = EnemyCenterPx(hm);
                var rb = hm.GetComponent<Rigidbody2D>();
                if (rb != null) vel = Flip(rb.linearVelocity / Px / 60f);
            }
            int side = Random.Range(0, 2) * 2 - 1;
            var from = centre + new Vector2(-side * 120f, 0f);
            var v = new Vector2(side * (4f + Random.Range(0, 3)), 0f) + (centre + vel * 15f - from).normalized * 2f;
            MeleeChild(ProjKind.MonkStaffT2Ghast, from, v, p.Damage, 0f, p.Crit);
            DevGhasts++;
        }

        // ------------------------------------------------------------------ 700: the ghast (AI_143)

        /// <summary>
        /// AI_143 (P:71062-71128): its wail as it comes; showing to alpha 100 for 50 ticks then
        /// fading; slowing 2% a tick; four frames of 5 ticks; gone at 60; facing and turned along its
        /// drift; ghostly wisps (228) swirling round it from 10 to 34 ticks.
        /// </summary>
        static void StepMonkGhast(Proj p)
        {
            if (p.Ai0 == 0f) Sound("Custom/dd2_ghastly_glaive_impact_ghost_" + Random.Range(0, 3));
            p.Ai0++;
            p.Alpha = p.Ai0 > 50f ? Mathf.Min(1f, p.Alpha + 25f / 255f) : Mathf.Max(100f / 255f, p.Alpha - 25f / 255f);
            p.Vel *= 0.98f;
            if (++p.FrameCounter >= 5)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 4) p.Frame = 0;
            }
            if (p.Ai0 >= 60f)
            {
                TerraProjectiles.Instance?.Kill(p);
                return;
            }
            var v = TV(p);
            p.Dir = v.x > 0f ? 1f : -1f;
            p.TRot = ToRot(v) + (p.Dir < 0f ? Mathf.PI : 0f);
            if (p.Ai0 < 10f || p.Ai0 >= 34f) return;
            var vn = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.up;
            var c = CenterPx(p);
            for (int i = 0; i < 2; i++)
            {
                int d = BoxDust(c, 14, 14, 228, 0f, 0f, 110);
                if (d < 0) continue;
                ref var dd = ref D(d);
                dd.Velocity = vn * 2f;
                dd.Position = c + RotateBy(vn, p.Ai0 / 60f * Mathf.PI * 2f * 2f + i / 2f * Mathf.PI * 2f) * 7f;
                dd.Scale = 1f + 0.6f * Random.value;
                dd.Velocity += vn * 3f;
                dd.NoGravity = true;
            }
        }

        /// <summary>
        /// M:30651-31427 for 700: four lit copies at its old places 1-4, fainter (4 to 1 fifteenths)
        /// and larger toward the back; then itself, added white as it shows; pivoting 20 px from its
        /// head end.
        /// </summary>
        static void DrawMonkGhast(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_700");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 4;
            var spr = FxBatch.Frame("Projectile_700", 0, p.Frame * fh, w, fh, w - 20f, fh / 2f);
            bool flip = p.Dir < 0f;
            for (int i = 1; i < 5 && i < p.Trail.Count; i++)
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), new Color(L, L, L, 1f) * ((5 - i) / 15f), p.TRot, Mathf.Lerp(p.Scale, 2.6f, i / 15f), flipX: flip);
            float a = 1f - p.Alpha;
            fx.Draw(spr, px, new Color(a, a, a, 0f), p.TRot, p.Scale, flipX: flip);
        }

        // ------------------------------------------------------------------ 709: the dragon bolt

        /// <summary>P:55660-55690, 58625-58631: showing 30 an update, three frames of 3 updates, a cyan light, its roar as it leaves; turned along its flight; gone after 30 updates.</summary>
        static void StepSkyDragonBolt(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 30f / 255f);
            if (++p.FrameCounter >= 3)
            {
                p.Frame++;
                p.FrameCounter = 0;
                if (p.Frame >= 3) p.Frame = 0;
            }
            FxBatch.Instance?.Light(CenterPx(p), new Color(0.4f, 0.85f, 0.9f), 3f);
            if (p.Ai1 == 0f) Sound("Custom/dd2_sky_dragons_fury_shot_" + Random.Range(0, 3));
            var v = TV(p);
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = ToRot(v) + (p.Dir < 0f ? Mathf.PI : 0f);
            if (++p.Ai1 >= 30f) TerraProjectiles.Instance?.Kill(p);
        }

        /// <summary>M:31120-31127, P:81064-81067: its frame about its middle, mirrored as it faces, white at alpha 200 (TrailingMode none: no copies).</summary>
        static void DrawSkyDragonBolt(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_709");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 3;
            fx.Draw(FxBatch.Frame("Projectile_709", 0, p.Frame * fh, w, fh, w / 2f, fh / 2f), px, new Color(1f, 1f, 1f, 200f / 255f), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        /// <summary>
        /// P:74714-74770: smoke (31) and blue sparks (226) through its little box; the fields already
        /// within 40 px are drawn into this one and fade, and a new one crackles here with its
        /// damage for 1-2.5 s.
        /// </summary>
        void KillSkyDragonBolt(Proj p)
        {
            var c = CenterPx(p);
            int size = (int)(12 * p.Scale);
            var tl = c - new Vector2(size / 2f, size / 2f);
            for (int k = 0; k < 3; k++)
            {
                int d = BoxDust(tl, size, size, 31, 0f, 0f, 100, 1.5f);
                if (d >= 0) D(d).Position = RotateBy(new Vector2(size / 2f, 0f), Random.value * Mathf.PI * 2f) * Random.value + c;
            }
            for (int k = 0; k < 10; k++)
            {
                int d = BoxDust(tl, size, size, 226, 0f, 0f, 0, 1.5f);
                if (d < 0) continue;
                D(d).Position = RotateBy(new Vector2(size / 2f, 0f), Random.value * Mathf.PI * 2f) * Random.value + c;
                D(d).NoGravity = true;
            }
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != ProjKind.SkyDragonField) continue;
                var qc = CenterPx(q);
                var half = q.HitSize != Vector2.zero ? q.HitSize / 2f : new Vector2(40f, 40f);
                if (Mathf.Abs(qc.x - c.x) >= 40f + half.x || Mathf.Abs(qc.y - c.y) >= 40f + half.y) continue;
                q.Ai1 = 1f;
                q.Vel = Flip((c - qc) / 5f);
            }
            var field = MeleeChild(ProjKind.SkyDragonField, c, Vector2.zero, p.Damage, 0f, p.Crit);
            if (field == null) return;
            field.TimeLeft = 30 * Random.Range(2, 6);
            Sound("Custom/dd2_sky_dragons_fury_circle_" + Random.Range(0, 3));
            DevSkyFields++;
        }

        // ------------------------------------------------------------------ 1110: the field (AI_077)

        /// <summary>
        /// AI_077 (P:31193-31292): showing 50 an update, or, drawn into a newer one, fading 51 an
        /// update and harmless; breathing between 80% and full size over 30 updates; four frames of
        /// 3; blue sparks (226) flung out from its middle.
        /// </summary>
        void StepSkyDragonField(Proj p)
        {
            if (p.Ai1 == 1f)
            {
                p.Alpha = Mathf.Min(1f, p.Alpha + 51f / 255f);
                if (p.Alpha >= 1f)
                {
                    Kill(p);
                    return;
                }
            }
            else p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (++p.Ai0 > 120f) p.Ai0 = 0f;
            float y = -Mathf.Cos(Mathf.PI * 2f * p.Ai0 / 30f);
            float k = Mathf.Max(0.75f + y * 0.25f, 0.8f - y * 0.2f);
            int size = (int)(80f * k);
            p.HitSize = new Vector2(size, size);
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 4) p.Frame = 0;
            }
            var c = CenterPx(p);
            var tl = c - new Vector2(size / 2f, size / 2f);
            void Spark(float scale, float mul)
            {
                int d = BoxDust(tl, size, size, 226, 0f, 0f, 100, scale);
                if (d < 0) return;
                ref var dd = ref D(d);
                dd.NoGravity = true;
                dd.Velocity *= mul;
                float a = Random.value * Mathf.PI * 2f;
                dd.Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (11f * k + 0.5f * Random.value * 55f * k) + c;
                dd.Velocity = dd.Velocity / 2f + (dd.Position - c).normalized;
            }
            Spark(0.5f, 2f);
            if (Random.Range(0, 2) == 0) Spark(0.9f, 1.2f);
            if (Random.Range(0, 4) == 0) Spark(0.7f, 1.2f);
        }

        /// <summary>M:33054-33067, P:81100-81102: its frame twice, turning slowly opposite ways and breathing out of step; white, half added, as it shows.</summary>
        static void DrawSkyDragonField(FxBatch fx, Proj p, Vector2 px)
        {
            // (Also the Electrosphere's, 443.)
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 4;
            var spr = FxBatch.Frame(p.Spec.Texture, 0, p.Frame * fh, w, fh, w / 2f, fh / 2f);
            float spin = Mathf.PI * 2f * p.Ai0 / 120f;
            float y = -Mathf.Cos(Mathf.PI * 2f * p.Ai0 / 30f);
            float a = 1f - p.Alpha;
            var c = new Color(a, a, a, 0.5f * a);
            fx.Draw(spr, px, c, spin, 0.75f + y * 0.25f);
            fx.Draw(spr, px, c, Mathf.PI * 2f - spin, 0.8f - y * 0.2f);
        }

        public static int DevMonkSlams, DevGhasts, DevSkyBolts, DevSkyFields;
    }
}
