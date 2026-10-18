using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The party rocket launchers and the Electrosphere Launcher (sweep R4b; Terraria's space, px y
    /// down, per update): the Celebration's fireworks (167-170, AI_034, bursting into their four
    /// patterns), the Celebration Mk2 held out (714, AI_075) loosing its seven kinds of rocket in
    /// turn (715-718, AI_147, each with its own flight and burst), and the Electrosphere Launcher's
    /// missile (442) that leaves an electrosphere (443, AI_077) where it ends.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevFireworks, DevCeleb2Rockets, DevElectrospheres;

        static void RegisterCelebration()
        {
            // P:2560-2571: the fireworks, 45 updates, an enemy only once a tick for all of them.
            foreach (var (kind, id) in new[] { (ProjKind.Firework1, 167), (ProjKind.Firework2, 168), (ProjKind.Firework3, 169), (ProjKind.Firework4, 170) })
                specs[kind] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = 14, H = 14, Time = 45, Penetrate = 1, TypeCooldown = 1,
                    Update = (t, p) => StepFirework(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => KillFirework(p),
                };
            // P:7574: the Celebration Mk2 held out, seven frames of its rainbow overlay.
            specs[ProjKind.Celeb2Held] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_714", W = 22, H = 22, Frames = 7, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = (t, p) => t.StepCeleb2(p), Draw = (t, fx, p, px) => DrawCeleb2(fx, p, px), OnKill = (t, p) => { },
            };
            // P:7586-7604: its rockets, one enemy, 3 updates a tick, 6 s; the kind of flight in ai0.
            foreach (var (kind, id) in new[] { (ProjKind.Celeb2Rocket1, 715), (ProjKind.Celeb2Rocket2, 716), (ProjKind.Celeb2Rocket3, 717), (ProjKind.Celeb2Rocket4, 718) })
                specs[kind] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = 14, H = 14, Time = 1080, Extra = 2, Penetrate = 1, TrailLength = 20,
                    Update = (t, p) => t.StepCeleb2Rocket(p), Draw = (t, fx, p, px) => DrawCeleb2Rocket(fx, p, px), OnKill = (t, p) => t.KillCeleb2Rocket(p),
                };
            // P:5128: the Electrosphere missile, 10 s, gone at the cursor's block; P:5139: the electrosphere, 80 px breathing, an enemy every 8 ticks.
            specs[ProjKind.ElectrosphereMissile] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_442", W = 14, H = 14, Time = 600, AlphaStart = 1f, Penetrate = 1,
                Update = (t, p) => StepElectrosphereMissile(t, p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => t.KillElectrosphereMissile(p),
            };
            specs[ProjKind.Electrosphere] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_443", W = 80, H = 80, Time = 3600, Penetrate = -1, TileCollide = false, AlphaStart = 1f, OwnerImmune = 8, Frames = 4,
                CanDamage = p => p.Ai1 != 1f, Update = (t, p) => t.StepSkyDragonField(p), Draw = (t, fx, p, px) => DrawSkyDragonField(fx, p, px), OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ 167-170: the Celebration's fireworks

        /// <summary>The Celebration (Pl:49529-49541): two random fireworks a shot, each up to 2 px off, 6 px to the side.</summary>
        public void ShootCelebration(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float facing)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            for (int k = 0; k < 2; k++)
            {
                var v = Flip(velocity) + new Vector2(Random.Range(-40, 41) * 0.05f, Random.Range(-40, 41) * 0.05f);
                var side = RotateBy(v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right, -Mathf.PI / 2f * facing) * 6f;
                var kind = Random.Range(0, 4) switch { 0 => ProjKind.Firework1, 1 => ProjKind.Firework2, 2 => ProjKind.Firework3, _ => ProjKind.Firework4 };
                var p = Spawn(kind, TerraParticles.ToWorld(TerraParticles.ToPx(from) + side), Flip(v), damage, knockback, crit);
                if (p == null) continue;
                p.Ai1 = 1f;
                DevFireworks++;
            }
            SpawnClass = keep;
        }

        /// <summary>AI_034 with ai1 1 (P:27520-27560): turned along its flight; a puff of fire as it leaves, then three flames out of its tail each update.</summary>
        static void StepFirework(Proj p)
        {
            var v = TV(p);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            var c = CenterPx(p);
            var tl = c - new Vector2(7f, 7f);
            if (++p.Ai0 == 1f)
            {
                var hero = HeroController.SilentInstance;
                var pv = hero != null ? Flip(hero.GetComponent<Rigidbody2D>().linearVelocity / Px / 60f) : Vector2.zero;
                for (int k = 0; k < 8; k++)
                {
                    int i = BoxDust(tl, 14, 14, 6, 0f, 0f, 100, 1.8f);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity = D(i).Velocity * 3f + v / 4f + pv * 0.1f;
                    D(i).FadeIn = 0.5f;
                    D(i).Position += v / 2f;
                }
            }
            if (p.Ai0 <= 2f) return;
            foreach (float down in new[] { 20f, 15f, 10f })
            {
                int i = BoxDust(tl + new Vector2(2f, down), 8, 8, 6, v.x, v.y, 100, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.2f;
                D(i).Position = c + RotateBy(D(i).Position - c, p.TRot);
            }
        }

        /// <summary>
        /// Kill for aiStyle 34 (P:78130-78340): the bang (Item14) and its pattern: rings of red (130)
        /// at 4-16 px, a green burst and oval (131), a yellow star with a blue heart (133, 132), or
        /// crossed ovals of yellow and pink (133, 134).
        /// </summary>
        static void KillFirework(Proj p)
        {
            Sound("Item_14");
            var at = CenterPx(p) - new Vector2(3f, 3f);
            int Spark(int type, float speed, float sx = 1f, float sy = 1f)
            {
                int i = BoxDust(at, 6, 6, type, 0f, 0f, 100);
                if (i < 0) return -1;
                var d = D(i).Velocity;
                if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
                d = d.normalized * speed;
                D(i).Velocity = D(i).Velocity * 0.5f + new Vector2(d.x * sx, d.y * sy);
                return i;
            }
            switch (p.Kind)
            {
                case ProjKind.Firework1:
                    for (int k = 0; k < 400; k++)
                    {
                        int i = Spark(130, k < 100 ? 4f : k < 200 ? 8f : k < 300 ? 12f : 16f);
                        if (i < 0) continue;
                        D(i).Scale = 1.3f;
                        D(i).NoGravity = true;
                    }
                    break;
                case ProjKind.Firework2:
                    for (int k = 0; k < 400; k++)
                    {
                        float s = k > 250 ? 13f : k > 100 ? 10f : 2f * (k / 100f);
                        int i = k <= 200 ? Spark(131, s) : Spark(131, s, 1.25f, 0.75f);
                        if (i < 0 || k <= 100) continue;
                        D(i).Scale = 1.3f;
                        D(i).NoGravity = true;
                    }
                    break;
                case ProjKind.Firework3:
                {
                    var dir = RotateBy(Vector2.right, Random.value * Mathf.PI * 2f);
                    float points = Random.Range(5, 9), outer = Random.Range(12, 17), inner = Random.Range(3, 7);
                    for (int pass = 0; pass < 2; pass++)
                        for (float n = 0f; n < points; n++)
                        {
                            for (int side = 0; side < 2; side++)
                            {
                                var to = RotateBy(dir, (side == 0 ? 1f : -1f) * Mathf.PI * 2f / (points * 2f));
                                for (float j = 0f; j < 20f; j++)
                                {
                                    int i = BoxDust(at, 6, 6, 133, 0f, 0f, 100, 1.3f);
                                    if (i < 0) continue;
                                    D(i).Velocity *= 0.1f;
                                    D(i).NoGravity = true;
                                    D(i).Velocity += Vector2.Lerp(dir, to, j / 20f) * Mathf.Lerp(outer, inner, j / 20f) / (pass == 0 ? 1f : 2f);
                                }
                            }
                            dir = RotateBy(dir, Mathf.PI * 2f / points);
                        }
                    for (int k = 0; k < 100; k++)
                    {
                        int i = Spark(132, outer);
                        if (i < 0) continue;
                        D(i).Scale = 1.3f;
                        D(i).NoGravity = true;
                    }
                    break;
                }
                default:
                    for (int k = 0; k < 400; k++)
                    {
                        int type = k > 300 ? 134 : k > 200 ? 133 : k > 100 ? 134 : 133;
                        float s = k > 300 ? 5f : k > 200 ? 8f : k > 100 ? 11f : 16f;
                        bool wide = k > 300 || (k > 100 && k <= 200);
                        int i = Spark(type, s, wide ? 0.7f : 1f, wide ? 1f : 0.7f);
                        if (i < 0 || Random.Range(0, 3) == 0) continue;
                        D(i).Scale = 1.3f;
                        D(i).NoGravity = true;
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ 714: the Celebration Mk2

        /// <summary>GetCeleb2Color: each kind of rocket's colour.</summary>
        static Color Celeb2Color(int kind) => kind switch
        {
            0 => new Color(1f, 0f, 0f), 1 => new Color(1f, 140f / 255f, 0f), 2 => new Color(1f, 215f / 255f, 0f), 3 => new Color(50f / 255f, 205f / 255f, 50f / 255f),
            4 => new Color(65f / 255f, 105f / 255f, 225f / 255f), 5 => new Color(80f / 255f, 20f / 255f, 180f / 255f), 6 => new Color(1f, 105f / 255f, 180f / 255f), _ => Color.clear,
        };

        /// <summary>The Mk2's rocket for the ammo used (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[3930]).</summary>
        static ProjKind Celeb2RocketFor(ItemType ammo) => ammo switch
        {
            ItemType.RocketI => ProjKind.Celeb2Rocket1, ItemType.RocketII => ProjKind.Celeb2Rocket2, ItemType.RocketIV => ProjKind.Celeb2Rocket4,
            ItemType.ClusterRocketII => ProjKind.Celeb2Rocket4, ItemType.MiniNukeII => ProjKind.Celeb2Rocket4, _ => ProjKind.Celeb2Rocket3,
        };

        /// <summary>
        /// AI_075 for 714 (P:68030-68192): a rocket every 8 ticks while the button is held and there's
        /// ammo, the kinds going round 0-6 (the fourth a little faster; the sixth three at once, the
        /// fifth with a twin out of step), from the player's middle at up to 5.6° off a heading
        /// itself a little off the aim; seven frames; the launch sound (Item156) with each; rainbow
        /// motes (267) at the muzzle, streaming as it turns.
        /// </summary>
        void StepCeleb2(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.CelebrationMk2);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.Ai0++;
            bool shoot = false;
            int kind = -1;
            if (--p.Ai1 <= 0f)
            {
                p.Ai1 = 8f;
                shoot = true;
                kind = (int)p.Ai0 / 8 % 7;
            }
            var oldCentre = TerraParticles.ToPx(p.Pos);
            var oldV = TV(p);
            if (++p.FrameCounter >= 4)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 7) p.Frame = 0;
            }
            if (p.SoundDelay > 0) p.SoundDelay--;
            if (p.SoundDelay <= 0 && mod.UseHeld)
            {
                p.SoundDelay = 8;
                if (p.Ai0 != 1f) Sound("Item_156");
            }
            var v = oldV;
            if (shoot)
            {
                var rocket = ProjKind.Celeb2Rocket1;
                float speed = 8f + (kind == 3 ? 1f : 0f);
                int slot = mod.Inventory.FindAmmo(AmmoKind.Rocket);
                if (slot >= 0) rocket = Celeb2RocketFor(mod.Inventory.Slots[slot].Type);
                if (!Channel(def.Type) || !mod.PickAmmo(def, ref rocket, ref speed, out int dmg, out float kb))
                {
                    Kill(p);
                    return;
                }
                v = RotateBy(HeldAim(centre, def, p.Scale), (Random.value * 0.1308997f - 0.06544985f) * 0.5f);
                var go = RotateBy(v.normalized * speed, Random.value * 0.19634955f - 0.09817477f);
                var keep = SpawnClass;
                SpawnClass = DamageClass.Ranged;
                float crit = Equipment.Crit(def);
                var from = TerraParticles.ToWorld(centre);
                void Launch(Vector2 vel, float twin)
                {
                    var q = Spawn(rocket, from, Flip(vel), dmg, kb, crit);
                    if (q == null) return;
                    q.Ai0 = kind;
                    q.Ai1 = twin;
                    DevCeleb2Rockets++;
                }
                Launch(go, 0f);
                if (kind == 5)
                    for (float s = -1f; s <= 1f; s += 2f) Launch(RotateBy(go, Mathf.PI * 2f * s / 80f), 0f);
                if (kind == 4) Launch(go, 1f);
                SpawnClass = keep;
            }
            HoldBow(p, centre, v, def, 2f);
            // The muzzle's rainbow, 40 px out along the aim (and streaming between the old aim and the new).
            var tint = FxParticles.HslToRgb(p.Ai0 / 90f % 1f, 1f, 0.5f);
            tint.a = 120f / 255f;
            var muzzle = TerraParticles.ToPx(p.Pos) + RotateBy(Vector2.right, ToRot(v)) * 40f;
            var oldMuzzle = oldCentre + RotateBy(Vector2.right, ToRot(oldV)) * 40f;
            int Mote(Vector2 at, Vector2 vel, float fade)
            {
                int i = PointDust(at, 267, vel);
                if (i < 0) return -1;
                D(i).Color = tint;
                D(i).Scale = 0.6f;
                D(i).FadeIn = fade;
                D(i).NoGravity = true;
                return i;
            }
            Mote(muzzle, Vector2.zero, 0.9f);
            Mote(muzzle, -RotateBy(v, 2.0943952f) / 10f, 0.7f);
            Mote(muzzle, -RotateBy(v, -2.0943952f) / 10f, 0.7f);
            if (muzzle != oldMuzzle)
            {
                var back = -(muzzle - oldMuzzle).normalized * 2f;
                foreach (float k in new[] { 0.8f, 0.5f, 0.2f }) Mote(Vector2.Lerp(muzzle, oldMuzzle, k), back, 0.9f);
            }
        }

        /// <summary>M:32630-32716 for 714: the Mk2 itself (Item_3930) about its middle, lit, its rainbow overlay frame (alpha 120) over it, flipped facing left.</summary>
        static void DrawCeleb2(FxBatch fx, Proj p, Vector2 px)
        {
            var gun = FxBatch.Whole("Item_3930");
            var over = FxBatch.Whole(p.Spec.Texture);
            if (gun == null || over == null) return;
            var at = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y));
            bool flip = p.Dir < 0f;
            int gw = (int)gun.rect.width, gh = (int)gun.rect.height;
            int ow = (int)over.rect.width, oh = (int)over.rect.height / 7;
            fx.Draw(FxBatch.Frame("Item_3930", 0, 0, gw, gh, gw / 2f, gh / 2f), at, new Color(L, L, L, 1f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            var tint = FxParticles.HslToRgb(p.Ai0 / 90f % 1f, 1f, 0.5f);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, oh * p.Frame, ow, oh, ow / 2f, oh / 2f), at, new Color(tint.r, tint.g, tint.b, 120f / 255f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
        }

        // ------------------------------------------------------------------ 715-718: its rockets (AI_147)

        /// <summary>
        /// AI_147 (P:53811-54062), each by its kind: red falls after 20 updates, through everything
        /// then, bursting every 20; orange hops up at 10 and falls fast, swelling, shedding a ring of
        /// motes every 10; gold falls after 60; green homes (as the Vortex rocket) after 20 and falls
        /// with nothing to chase; blue weaves side to side at double size; purple falls after 40, pink
        /// after 30. Lit by its colour.
        /// </summary>
        void StepCeleb2Rocket(Proj p)
        {
            p.LocalAi0++;
            p.Alpha = 0f;
            var v = TV(p);
            int kind = (int)p.Ai0;
            float t = p.LocalAi0;
            switch (kind)
            {
                case 0:
                    if (t >= 20f)
                    {
                        v.y += 0.12f;
                        p.Penetrate = p.MaxPenetrate = -1;
                    }
                    if (t > 20f && t % 20f == 0f) Celeb2Burst(p);
                    break;
                case 1:
                    if (t == 10f) v.y -= 10f;
                    if (t >= 10f) v.y += 0.25f;
                    if (t >= 10f)
                    {
                        p.Scale = Mathf.Min(2.5f, p.Scale + 0.015f);
                        if (t % 10f == 0f)
                        {
                            var c = CenterPx(p);
                            var tint = Color.Lerp(Celeb2Color(1), Color.white, Random.value * 0.3f);
                            for (float k = 0f; k < 1f; k += 0.12f)
                            {
                                var sp = new Vector2(-8f, 0f) + Vector2.Scale(RotateBy(new Vector2(0f, -1f), k * Mathf.PI * 2f), new Vector2(2f, 4f));
                                sp = RotateBy(sp, p.TRot - Mathf.PI / 2f);
                                int i = PointDust(c + sp, 267, (-v * 0.35f + sp * 0.35f) * p.Scale);
                                if (i < 0) continue;
                                D(i).Color = tint;
                                D(i).Scale = 0.9f;
                                D(i).FadeIn = 1.1f;
                                D(i).NoGravity = true;
                                if (t == 10f) D(i).Velocity = RotateBy(sp, ToRot(v)) * 0.3f;
                            }
                        }
                    }
                    break;
                case 2:
                    if (t >= 60f) v.y += 0.15f;
                    break;
                case 3:
                {
                    if (p.LocalAi1 == 0f) p.LocalAi1 = v.magnitude;
                    if (t >= 25f && p.Target == null) v.y += 0.15f;
                    if (t >= 20f)
                    {
                        var c = CenterPx(p);
                        if (p.Target == null)
                        {
                            float best = 800f;
                            foreach (var hm in EnemiesNear(c, 1200f))
                            {
                                if (!Chaseable(hm)) continue;
                                var e = EnemyCenterPx(hm);
                                float d = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                                if (d < best && InSight(c, e))
                                {
                                    best = d;
                                    p.Target = hm;
                                }
                            }
                        }
                        var tg = p.Target;
                        if (tg != null && !tg.isDead && tg.gameObject.activeInHierarchy)
                        {
                            var e = EnemyCenterPx(tg);
                            if (Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y) < 1000f && (e - c).sqrMagnitude > 1e-6f) v = (v * 7f + (e - c).normalized * p.LocalAi1) / 8f;
                        }
                    }
                    break;
                }
                case 4:
                {
                    if (t == 1f && p.Ai1 == 1f) p.LocalAi0 = t += 45f;
                    var n = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
                    float x = RotateBy(n, t * (Mathf.PI / 45f)).x;
                    p.Pos += Flip(RotateBy(n, Mathf.PI / 2f) * x * 3f) * Px;
                    p.Scale = 2f;
                    if (t >= 10f) v.y += 0.04f;
                    break;
                }
                case 5:
                    if (t >= 40f) v.y += 0.08f;
                    break;
                case 6:
                    if (t >= 30f) v.y += 0.1f;
                    break;
            }
            if (v.y > 16f) v.y = 16f;
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            p.Vel = Flip(v);
        }

        /// <summary>
        /// Kill for 715-718 (P:75030-75036): the bang (Item14), a 128 px blast (240 for the big
        /// rockets), blocks broken (3 tiles for 716, 5 for 718), and its kind's burst.
        /// </summary>
        void KillCeleb2Rocket(Proj p)
        {
            Sound("Item_14");
            bool big = p.Kind == ProjKind.Celeb2Rocket3 || p.Kind == ProjKind.Celeb2Rocket4;
            Blast(p, big ? 240f : 128f, p.Damage, p.Knockback);
            int blocks = p.Kind == ProjKind.Celeb2Rocket2 ? 3 : p.Kind == ProjKind.Celeb2Rocket4 ? 5 : 0;
            if (blocks > 0)
            {
                DevRocketBlocks += BlastTiles(p.Pos, blocks);
                BreakSpecialFloors(p.Pos, blocks * CellGrid.CellSize);
            }
            Celeb2Burst(p, big ? 240f : 128f);
        }

        /// <summary>AI_147_Explosion (P:54064-54338): the burst of each kind of rocket, in its colour.</summary>
        void Celeb2Burst(Proj p, float size = 14f)
        {
            int kind = (int)p.Ai0;
            var c = CenterPx(p);
            int w = (int)size;
            var tl = c - new Vector2(w / 2f, w / 2f);
            var tint = Celeb2Color(kind);
            var pp = TerraParticles.Instance;
            void Smoke(int n)
            {
                for (int k = 0; k < n; k++) BoxDust(tl, w, w, 31, 0f, 0f, 100, 1.5f);
            }
            void Gore(int n)
            {
                if (pp == null) return;
                for (int k = 0; k < n; k++)
                    pp.NewGore(tl + new Vector2(w * Random.Range(0, 100) / 100f, w * Random.Range(0, 100) / 100f) - Vector2.one * 10f, Vector2.zero, Random.Range(61, 64), 1f, 0.3f,
                        new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
            }
            int Perfect(int type, Vector2 at, Vector2 vel, int alpha, Color col)
            {
                int i = PointDust(at, type, vel, alpha);
                if (i >= 0) D(i).Color = col;
                return i;
            }
            switch (kind)
            {
                case 0:
                    Smoke(4);
                    for (int k = 0; k < 20; k++)
                    {
                        int i = BoxDust(tl, w, w, 130, 0f, 0f, 200, 1.2f);
                        if (i >= 0)
                        {
                            D(i).NoGravity = true;
                            D(i).Velocity *= 3f;
                        }
                        i = BoxDust(tl, w, w, 130, 0f, 0f, 200, 0.5f);
                        if (i < 0) continue;
                        D(i).Velocity *= 1.2f;
                        D(i).NoGravity = true;
                    }
                    Gore(1);
                    break;
                case 1:
                    for (float k = 0f; k < 1f; k += 0.0125f)
                    {
                        var v = Random.insideUnitCircle * (Random.value * 2f + 2f);
                        if (v.y > 0f) v *= 0.3f;
                        int i = Perfect(278, c, v * 2f, 200, Color.Lerp(tint, Color.white, Random.value * 0.6f));
                        if (i < 0) continue;
                        D(i).Scale = 1.1f;
                        D(i).FadeIn = 1.3f;
                    }
                    Gore(3);
                    break;
                case 2:
                {
                    bool fat = Random.Range(0, 4) == 0;
                    float pull = fat ? 0.1f : 0.05f, step = fat ? 0.1f : 0.13f, spin = Random.Range(-1f, 1f);
                    for (float n = 0f; n < 5f; n++)
                    {
                        var a = c + RotateBy(new Vector2(0f, -100f), spin + n * Mathf.PI * 2f / 5f);
                        var b = c + RotateBy(new Vector2(0f, -100f), spin + (n + 1f) * Mathf.PI * 2f / 5f);
                        var m = c + RotateBy(new Vector2(0f, -100f), spin + (n + 0.5f) * Mathf.PI * 2f / 5f) * 0.4f;
                        for (int half = 0; half < 2; half++)
                        {
                            var from = half == 0 ? a : m;
                            var to = half == 0 ? m : b;
                            for (float k = 0f; k < 1f; k += step)
                            {
                                var on = Vector2.Lerp(from, to, k);
                                var start = Vector2.Lerp(on, c, 0.9f);
                                int i = Perfect(267, start, (on - start).normalized * Vector2.Distance(on, start) * pull, 0, tint);
                                if (i < 0) continue;
                                D(i).Scale = 0.5f;
                                D(i).FadeIn = 1.2f;
                                D(i).NoGravity = true;
                            }
                        }
                    }
                    Smoke(4);
                    for (int k = 0; k < (fat ? 60 : 40); k++)
                    {
                        int i = BoxDust(tl, w, w, 228, 0f, 0f, 200, 2.5f);
                        if (i >= 0)
                        {
                            D(i).NoGravity = true;
                            D(i).Velocity *= fat ? 8f : 4f;
                        }
                        i = BoxDust(tl, w, w, 228, 0f, 0f, 200, 1.5f);
                        if (i < 0) continue;
                        D(i).Velocity *= fat ? 4.2f : 2.2f;
                        D(i).NoGravity = true;
                    }
                    Gore(1);
                    break;
                }
                case 3:
                {
                    Smoke(4);
                    float spin = Random.value * Mathf.PI * 2f;
                    for (int k = 0; k < 40; k++)
                    {
                        var v = Random.insideUnitCircle * 2f;
                        int i = Perfect(278, c, v, 100, Color.Lerp(tint, Color.white, Random.value * 0.3f));
                        if (i < 0) continue;
                        D(i).Scale = 1.3f * (v.magnitude / 2f);
                        D(i).FadeIn = 1.5f * (v.magnitude / 2f);
                        D(i).NoGravity = true;
                        v *= 6f;
                        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) v.y *= 0.1f;
                        else v.x *= 0.1f;
                        D(i).Velocity = RotateBy(v, spin);
                    }
                    Gore(1);
                    break;
                }
                case 4:
                    for (float k = 0f; k < 1f; k += 0.025f)
                    {
                        var v = Random.insideUnitCircle * (Random.value * 2f + 2f);
                        if (v.y > 0f) v *= 0.3f;
                        int i = PointDust(c, 132, v * 2f, 200);
                        if (i < 0) continue;
                        D(i).Scale = 1.1f;
                        D(i).FadeIn = 1.5f;
                    }
                    Gore(3);
                    break;
                case 5:
                    for (int k = 0; k < 20; k++)
                    {
                        int i = BoxDust(tl, w, w, 278, 0f, 0f, 200, 2.5f);
                        if (i >= 0)
                        {
                            D(i).Color = Color.Lerp(tint, Color.white, Random.value * 0.4f);
                            D(i).FadeIn = 1.3f;
                            D(i).Scale = 1.1f;
                            D(i).Velocity *= 2f;
                        }
                        i = BoxDust(tl, w, w, 27, 0f, 0f, 200, 2.5f);
                        if (i < 0) continue;
                        D(i).Color = tint;
                        D(i).Velocity *= 1.2f;
                        D(i).NoGravity = true;
                    }
                    break;
                default:
                    Smoke(4);
                    foreach (var (step, type, jitter, speed, scale, fade) in new[] { (0.015f, 134, 0.1f, 5f, 1.4f, 0f), (0.025f, 267, 0.2f, 4f, 0.4f, 1.2f), (0.07f, 267, 0.3f, 7f, 0.4f, 1.3f) })
                        for (float k = 0f; k < 1f; k += step)
                        {
                            var r = RotateBy(Vector2.up, k * Mathf.PI * 2f) * (1f + Random.Range(-1f, 1f) * jitter);
                            var v = r * speed;
                            v.y -= 3f;
                            int i = Perfect(type, c + r, v * 2f, 0, tint);
                            if (i < 0) continue;
                            D(i).Scale = scale;
                            if (type == 267)
                            {
                                D(i).NoGravity = true;
                                D(i).FadeIn = fade;
                            }
                        }
                    Gore(1);
                    break;
            }
        }

        /// <summary>
        /// M:34045-34122, 34453-34458: its trail (the sheet's third column, stretched across its
        /// path between old places, fading back; the gold one rippling, the purple one thin), the
        /// rocket lit, then its colour over it.
        /// </summary>
        static void DrawCeleb2Rocket(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int fw = (int)whole.rect.width / 3, fh = (int)whole.rect.height;
            int kind = (int)p.Ai0;
            var tint = Celeb2Color(kind);
            var trailColor = new Color(tint.r, tint.g, tint.b, 127f / 255f) * 0.8f;
            var strip = FxBatch.Frame(p.Spec.Texture, fw * 2, 0, fw, fh, fw / 2f, 0f);
            int n = p.Trail.Count;
            for (int i = n - 1; i > 0; i--)
            {
                var at = TerraParticles.ToPx(p.Trail[i]);
                var next = TerraParticles.ToPx(p.Trail[i - 1]);
                var d = next - at;
                float rot = d.sqrMagnitude > 1e-6f ? Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f : p.TRot;
                var scale = new Vector2(d.magnitude / fw, 1f);
                if (kind == 2)
                {
                    int phase = ((i + p.TimeLeft) % 20 + 20) % 20;
                    float shift = phase switch { 1 => 1f, 2 => 2f, 3 => 3f, 4 => 2f, 5 => 1f, 7 => -1f, 8 => -2f, 9 => -3f, 10 => -2f, 11 => -1f, _ => 0f };
                    at += new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)) * shift * 4f;
                    scale *= 0.6f;
                }
                else if (kind == 5) scale *= 0.5f;
                fx.Draw(strip, at, trailColor * (1f - (float)i / n), rot, scale);
            }
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, fw, fh, fw / 2f, fh / 2f), px, new Color(L, L, L, 1f), p.TRot, p.Scale);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, fw, 0, fw, fh, fw / 2f, fh / 2f), px, new Color(tint.r, tint.g, tint.b, 80f / 255f), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 442 / 443: the Electrosphere Launcher

        public void ShootElectrosphere(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, Vector2 targetPx)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            var p = Spawn(ProjKind.ElectrosphereMissile, from, velocity, damage, knockback, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Ai0 = Mathf.Floor(targetPx.x / 16f);
            p.Ai1 = Mathf.Floor(targetPx.y / 16f);
        }

        /// <summary>
        /// AI_001 for 442 (P:55690-55741): showing 90 an update after four; gone at the cursor's
        /// block, or 120 updates after it shows; then lit blue, a ring of blue sparks (135) off its
        /// tail every three updates.
        /// </summary>
        static void StepElectrosphereMissile(TerraProjectiles t, Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (p.Alpha > 0f && ++p.LocalAi0 >= 4f)
            {
                p.Alpha -= 90f / 255f;
                if (p.Alpha <= 0f)
                {
                    p.Alpha = 0f;
                    p.LocalAi0 = 2f;
                }
            }
            if (Vector2.Distance(c, new Vector2(p.Ai0, p.Ai1) * 16f + Vector2.one * 8f) <= 16f)
            {
                t.Kill(p);
                return;
            }
            if (p.Alpha > 0f) return;
            if (++p.LocalAi1 >= 120f)
            {
                t.Kill(p);
                return;
            }
            if (++p.LocalAi0 == 3f)
            {
                p.LocalAi0 = 0f;
                for (int k = 0; k < 8; k++)
                {
                    var sp = new Vector2(-8f, 0f) + Vector2.Scale(RotateBy(new Vector2(0f, -1f), k * Mathf.PI / 4f), new Vector2(2f, 4f));
                    sp = RotateBy(sp, p.TRot - Mathf.PI / 2f);
                    int i = PointDust(c + sp, 135, v * 0.66f, 0, 1.5f);
                    if (i >= 0) D(i).NoGravity = true;
                }
            }
        }

        /// <summary>P:74920-74946: a crackle (Item94), blue sparks, older electrospheres about it drawn in and gone, and a new one here.</summary>
        void KillElectrosphereMissile(Proj p)
        {
            Sound("Item_94");
            var c = CenterPx(p);
            for (int k = Random.Range(3, 7); k > 0; k--)
            {
                int i = BoxDust(c - new Vector2(7f, 7f), 14, 14, 135, 0f, 0f, 100, 2.1f);
                if (i < 0) continue;
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
            }
            var near = new Rect(c - new Vector2(40f, 40f), new Vector2(80f, 80f));
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != ProjKind.Electrosphere) continue;
                var qc = CenterPx(q);
                var qs = q.HitSize != Vector2.zero ? q.HitSize : new Vector2(80f, 80f);
                if (!near.Overlaps(new Rect(qc - qs / 2f, qs))) continue;
                q.Ai1 = 1f;
                q.Vel = Flip((c - qc) / 5f);
            }
            var keep = SpawnClass;
            SpawnClass = p.Class;
            if (Spawn(ProjKind.Electrosphere, p.Pos, Vector2.zero, p.Damage, 0f, p.Crit) != null) DevElectrospheres++;
            SpawnClass = keep;
        }
    }
}
