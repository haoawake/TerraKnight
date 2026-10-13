using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The lunar and Old One's Army weapons held out while the button is held (AI_075, Terraria's
    /// space, px y down, per update): the Vortex Beater (615: a round every 5 ticks, a homing rocket,
    /// 616, every seventh), the Phantasm (630: four arrows at a time, faster the longer it's held,
    /// and phantom arrows, 631, off any arrow that lands meanwhile) and the Phantom Phoenix (705: two
    /// arrows, every third shot a burning phoenix, 706, that bursts when it hits a wall).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevVortexRockets, DevPhantasmArrows, DevPhoenixes;

        /// <summary>Player.phantasmTime: ticks the Phantasm's arrows still call up phantom ones.</summary>
        public static int PhantasmTime;

        /// <summary>Player.phantomPhoneixCounter: the Phantom Phoenix's shots toward the next phoenix.</summary>
        static int phoenixCounter;

        static void RegisterHeldBows()
        {
            Spec HeldSpec(string tex, int w, int h, int frames, System.Action<TerraProjectiles, Proj> step) => new Spec
            {
                Ai = Ai.Custom, Texture = tex, W = w, H = h, Frames = frames, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = step, Draw = (t, fx, p, px) => DrawHeldBow(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.VortexBeaterHeld] = HeldSpec("Projectile_615", 22, 22, 7, (t, p) => t.StepVortexBeater(p));
            specs[ProjKind.PhantasmHeld] = HeldSpec("Projectile_630", 22, 22, 1, (t, p) => t.StepPhantasm(p));
            specs[ProjKind.PhoenixBowHeld] = HeldSpec("Projectile_705", 22, 22, 1, (t, p) => t.StepPhoenixBow(p));
            // P:6634: the Vortex Beater's rocket, AI_001 without gravity, 90 ticks, homing after 20 updates; bursting over 80 px.
            specs[ProjKind.VortexRocket] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_616", W = 14, H = 14, Time = 270, Extra = 2, AlphaStart = 1f, Penetrate = 1,
                Update = (t, p) => t.StepVortexRocket(p), Draw = (t, fx, p, px) => DrawVortexRocket(fx, p, px), OnKill = (t, p) => t.KillVortexRocket(p),
            };
            // P:6781: the phantom arrow, AI_122, through blocks while it has its enemy, waiting 0, 15 or 30 updates first.
            specs[ProjKind.PhantasmArrow] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_631", W = 10, H = 10, Time = 1200, Extra = 1, AlphaStart = 1f, Penetrate = 1, TileCollide = false,
                Update = (t, p) => t.StepPhantasmArrow(p), Draw = (t, fx, p, px) => fx.Draw(FxBatch.Whole(p.Spec.Texture), px, new Color(1f - p.Alpha, 1f - p.Alpha, 1f - p.Alpha, 0f), p.TRot, p.Scale),
                OnKill = (t, p) => KillPhantasmArrow(p),
            };
            // P:7463, 11377, 14276, 20063: the phoenix, 66 px, AI_001 without gravity, 5 s, through everything (each every 10 ticks,
            // losing 5% a hit), Hellfire 8 s; a 10 px box against blocks; it bursts over 96 px.
            specs[ProjKind.Phoenix] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_706", W = 66, H = 66, TileBox = new Vector2(10f, 10f), Time = 300, AlphaStart = 1f, Penetrate = -1, LocalCooldown = 10,
                Frames = 8, TrailLength = 10, Light = new Color(1f, 0.6f, 0.2f), Debuffs = new[] { (EnemyDebuffs.Hellfire, 480, 1) },
                Update = (t, p) => StepPhoenix(p), Draw = (t, fx, p, px) => DrawPhoenix(fx, p, px), OnHit = (t, p, hm) => p.Damage = (int)(p.Damage * 0.95f),
                OnKill = (t, p) => t.KillPhoenix(p),
            };
        }

        // ------------------------------------------------------------------ the holders (AI_075)

        public void ShootHeldBow(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float ai0)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            if (p != null) p.Ai0 = ai0;
        }

        /// <summary>
        /// AI_075's end (P:67677-67688): on the player's middle (and on by its velocity, as
        /// UpdatePosition moves it after), turned along the aim (a half turn more facing left), the
        /// player facing and holding it.
        /// </summary>
        void HoldBow(Proj p, Vector2 centre, Vector2 v, ItemDef def, float drop)
        {
            float facing = v.x < 0f ? -1f : 1f;
            p.Pos = TerraParticles.ToWorld(centre + v + new Vector2(0f, drop));
            p.Vel = Flip(v);
            p.TRot = ToRot(v) + (facing < 0f ? Mathf.PI : 0f);
            p.Dir = facing;
            p.TimeLeft = 2;
            TerraMod.Instance?.KeepUsing(def, Flip(v));
        }

        /// <summary>The aim from the player's middle at the item's shot speed (times its scale), the way up if there's none.</summary>
        static Vector2 HeldAim(Vector2 centre, ItemDef def, float scale)
        {
            var aim = AimPx() - centre;
            return (aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(0f, -1f)) * def.ShootSpeed * scale;
        }

        /// <summary>
        /// AI_075 for 615 (P:67919-68012): every 5 ticks a round (14 px plus the ammo's, up to 5.6°
        /// off a heading itself up to 3.75° off the aim), and every seventh also a rocket (8 px, up
        /// to 11° off, 20 more damage, a quarter more knockback); seven frames, faster at 40, 80 and
        /// 120 ticks held; the shot sound (Item36) with each.
        /// </summary>
        void StepVortexBeater(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.VortexBeater);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.Ai0++;
            int stage = (p.Ai0 >= 40f ? 1 : 0) + (p.Ai0 >= 80f ? 1 : 0) + (p.Ai0 >= 120f ? 1 : 0);
            bool shoot = false, rocket = false;
            if (--p.Ai1 <= 0f)
            {
                p.Ai1 = 5f;
                shoot = true;
                rocket = (int)p.Ai0 / 5 % 7 == 0;
            }
            p.FrameCounter += 1 + stage;
            if (p.FrameCounter >= 4)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 7) p.Frame = 0;
            }
            if (p.SoundDelay > 0) p.SoundDelay--;
            if (p.SoundDelay <= 0 && mod.UseHeld)
            {
                p.SoundDelay = 5;
                Sound("Item_36");
            }
            var v = TV(p);
            if (shoot)
            {
                var kind = ProjKind.Bullet;
                float speed = 14f;
                if (!Channel(def.Type) || !mod.PickAmmo(def, ref kind, ref speed, out int dmg, out float kb))
                {
                    Kill(p);
                    return;
                }
                v = RotateBy(HeldAim(centre, def, p.Scale), Random.value * 0.1308997f - 0.06544985f);
                var keep = SpawnClass;
                SpawnClass = DamageClass.Ranged;
                float crit = Equipment.Crit(def);
                Spawn(kind, TerraParticles.ToWorld(centre), Flip(RotateBy(v.normalized * speed, Random.value * 0.19634955f - 0.09817477f)), dmg, kb, crit);
                if (rocket && Spawn(ProjKind.VortexRocket, TerraParticles.ToWorld(centre), Flip(RotateBy(v.normalized * 8f, Random.value * 0.3926991f - 0.19634955f)), dmg + 20, kb * 1.25f, crit) != null)
                    DevVortexRockets++;
                SpawnClass = keep;
            }
            HoldBow(p, centre, v, def, 2f);
        }

        /// <summary>
        /// AI_075 for 630 (P:68194-68285): a volley every 24 ticks (2 fewer at 40, 80 and 120 held):
        /// four arrows (14 px plus the ammo's, 60-140% of it) from up to 15 px about the player; the
        /// bowstring drawn (Item5) with each; meanwhile any arrow that lands calls up phantom ones.
        /// </summary>
        void StepPhantasm(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.Phantasm);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.Ai0++;
            int stage = (p.Ai0 >= 40f ? 1 : 0) + (p.Ai0 >= 80f ? 1 : 0) + (p.Ai0 >= 120f ? 1 : 0);
            int every = 24 - 2 * stage;
            bool shoot = false;
            if (--p.Ai1 <= 0f)
            {
                p.Ai1 = every;
                shoot = true;
            }
            bool can = Channel(def.Type) && mod.HasAmmo(def);
            if (p.LocalAi0 > 0f) p.LocalAi0--;
            if (p.SoundDelay > 0) p.SoundDelay--;
            if (p.SoundDelay <= 0 && can)
            {
                p.SoundDelay = every;
                if (p.Ai0 != 1f) Sound("Item_5");
                p.LocalAi0 = 12f;
            }
            PhantasmTime = 2;
            var v = TV(p);
            if (shoot)
            {
                var kind = ProjKind.WoodenArrow;
                float speed = 14f;
                if (!can || !mod.PickAmmo(def, ref kind, ref speed, out int dmg, out float kb))
                {
                    Kill(p);
                    return;
                }
                v = HeldAim(centre, def, p.Scale) * 0.55f;
                var keep = SpawnClass;
                SpawnClass = DamageClass.Ranged;
                float crit = Equipment.Crit(def);
                for (int k = 0; k < 4; k++)
                {
                    var at = centre + new Vector2(Random.Range(-15f, 15f), Random.Range(-15f, 15f));
                    Spawn(kind, TerraParticles.ToWorld(at), Flip(v.normalized * speed * (0.6f + Random.value * 0.8f)), dmg, kb, crit);
                }
                SpawnClass = keep;
            }
            HoldBow(p, centre, v, def, 0f);
        }

        /// <summary>
        /// AI_075 for 705 (P:68287-68383): a volley every use (wooden arrows lit): two arrows (12 px
        /// plus the ammo's, up to 1.5 px off), or every third time one phoenix (706) at 16 px and
        /// twice the damage, the next volley half again as late; the bowstring (Item5) with each.
        /// </summary>
        void StepPhoenixBow(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.PhantomPhoenix);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.Ai0++;
            int anim = Mathf.Max(1, Mathf.RoundToInt(def.UseAnimation * Equipment.UseScale(def) * 60f));
            bool shoot = false;
            if (--p.Ai1 <= 0f)
            {
                p.Ai1 = anim;
                shoot = true;
            }
            bool can = Channel(def.Type) && mod.HasAmmo(def);
            if (p.LocalAi0 > 0f) p.LocalAi0--;
            if (p.SoundDelay > 0) p.SoundDelay--;
            if (p.SoundDelay <= 0 && can)
            {
                p.SoundDelay = anim;
                if (p.Ai0 != 1f) Sound("Item_5");
                p.LocalAi0 = 12f;
            }
            var v = TV(p);
            if (shoot)
            {
                var kind = ProjKind.WoodenArrow;
                float speed = 12f;
                if (!can || !mod.PickAmmo(def, ref kind, ref speed, out int dmg, out float kb))
                {
                    Kill(p);
                    return;
                }
                if (kind == ProjKind.WoodenArrow) kind = ProjKind.FlamingArrow;
                int n = 2;
                float spread = 1.5f;
                if (++phoenixCounter >= 3)
                {
                    phoenixCounter = 0;
                    n = 1;
                    dmg *= 2;
                    spread = 0f;
                    p.Ai1 *= 1.5f;
                    kind = ProjKind.Phoenix;
                    speed = 16f;
                }
                v = HeldAim(centre, def, p.Scale) * 0.55f;
                var keep = SpawnClass;
                SpawnClass = DamageClass.Ranged;
                float crit = Equipment.Crit(def);
                for (int k = 0; k < n; k++)
                {
                    var go = v.normalized * speed + new Vector2(Random.Range(-spread, spread), Random.Range(-spread, spread));
                    if (Spawn(kind, TerraParticles.ToWorld(centre), Flip(go), dmg, kb, crit) != null && kind == ProjKind.Phoenix) DevPhoenixes++;
                }
                SpawnClass = keep;
            }
            HoldBow(p, centre, v, def, 0f);
        }

        /// <summary>
        /// M:32630-32716: the weapon about its middle, lit, flipped facing left; the Vortex Beater's
        /// and the Phantasm's glowmasks (192, 200) half added over it, and the Phantasm's drawn string
        /// (Extra_65, six frames as the shot goes).
        /// </summary>
        static void DrawHeldBow(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / p.Spec.Frames;
            var at = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y));
            bool flip = p.Dir < 0f;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f), at, new Color(L, L, L, 1f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            string glow = p.Kind == ProjKind.VortexBeaterHeld ? "Glow_192" : p.Kind == ProjKind.PhantasmHeld ? "Glow_200" : null;
            if (glow != null)
                fx.Draw(FxBatch.Frame(glow, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f), at, FxBatch.C(255, 255, 255, 127), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            if (p.Kind == ProjKind.PhantasmHeld && p.LocalAi0 >= 1f && p.LocalAi0 <= 6f)
            {
                var str = FxBatch.Whole("Extra_65");
                if (str == null) return;
                int sw = (int)str.rect.width, sh = (int)str.rect.height / 6, frame = 6 - (int)p.LocalAi0;
                var v = TV(p);
                fx.Draw(FxBatch.Frame("Extra_65", 0, sh * frame, sw, sh, flip ? sw : 0f, fh / 2f - 2f), at + (v.sqrMagnitude > 1e-6f ? v.normalized * 2f : Vector2.zero),
                    FxBatch.C(255, 255, 255, 127), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            }
        }

        // ------------------------------------------------------------------ 616: the Vortex Beater's rocket

        /// <summary>
        /// AI_001 for 616 (P:58356-58450): showing 25 an update, a thin teal trail (229) once
        /// half seen; after 20 updates it takes the nearest enemy it can see within 800 px
        /// (across plus down), and while that one is within 1000 bends an eighth of the way at
        /// its first speed toward it each update.
        /// </summary>
        void StepVortexRocket(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            if (p.Alpha < 170f / 255f)
                for (int k = 0; k < 3; k++)
                {
                    int i = BoxDust(c, 1, 1, 229);
                    if (i < 0) continue;
                    D(i).Position = c - v / 3f * k;
                    D(i).Velocity = Vector2.zero;
                    D(i).NoGravity = true;
                    D(i).Alpha = 200;
                    D(i).Scale = 0.5f;
                }
            if (p.LocalAi0 == 0f) p.LocalAi0 = v.magnitude;
            if (p.Alpha > 0f) p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            if (++p.Ai0 > 20f)
            {
                p.Ai0--;
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
                var t = p.Target;
                if (t != null && !t.isDead && t.gameObject.activeInHierarchy)
                {
                    var e = EnemyCenterPx(t);
                    if (Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y) < 1000f && (e - c).sqrMagnitude > 1e-6f)
                        v = (v * 7f + (e - c).normalized * p.LocalAi0) / 8f;
                }
            }
            p.TRot = ToRot(v) + 1.57f;
            p.Vel = Flip(v);
        }

        /// <summary>M:33209, 33642-33645: the rocket about its middle, lit, its glow (Glow_193, 127 127 127 0) added over.</summary>
        static void DrawVortexRocket(FxBatch fx, Proj p, Vector2 px)
        {
            float a = 1f - p.Alpha;
            fx.Draw(FxBatch.Whole(p.Spec.Texture), px, new Color(L, L, L, 1f) * a, p.TRot, p.Scale);
            fx.Draw(FxBatch.Whole("Glow_193"), px, FxBatch.C(127, 127, 127, 0) * a, p.TRot, p.Scale);
        }

        /// <summary>P:75045-75069: the boom (Item14), an 80 px blast, smoke (31), teal sparks (229) and a smoke gore.</summary>
        void KillVortexRocket(Proj p)
        {
            Sound("Item_14");
            Blast(p, 80f, p.Damage, p.Knockback);
            var tl = CenterPx(p) - new Vector2(40f, 40f);
            for (int k = 0; k < 4; k++) BoxDust(tl, 80, 80, 31, 0f, 0f, 100, 1.5f);
            for (int k = 0; k < 40; k++)
            {
                int i = BoxDust(tl, 80, 80, 229, 0f, 0f, 200, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2f;
                }
                i = BoxDust(tl, 80, 80, 229, 0f, 0f, 200, 1.5f);
                if (i < 0) continue;
                D(i).Velocity *= 1.2f;
                D(i).NoGravity = true;
            }
            var pp = TerraParticles.Instance;
            if (pp != null)
                pp.NewGore(tl + new Vector2(80f * Random.Range(0, 100) / 100f, 80f * Random.Range(0, 100) / 100f) - Vector2.one * 10f, Vector2.zero, Random.Range(61, 64), 1f, 0.3f,
                    new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
        }

        // ------------------------------------------------------------------ 631: the phantom arrow

        /// <summary>
        /// P:13935-13941: while the Phantasm is out, an arrow (not a phantom one) that lands calls up
        /// three phantom arrows from about the player, 15 updates apart, at its target with 30% of its
        /// damage and no knockback.
        /// </summary>
        void PhantasmArrows(Proj p, HealthManager hm)
        {
            if (!Holder(out _)) return;
            var hero = HeroController.SilentInstance;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            for (int k = 0; k < 3; k++)
            {
                var from = new Vector2(Random.Range(box.min.x, box.max.x), Random.Range(box.min.y, box.max.y));
                var to = EnemyCenterPx(hm) - TerraParticles.ToPx(from);
                var q = Spawn(ProjKind.PhantasmArrow, from, Flip((to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right) * 6f), (int)(p.Damage * 0.3f), 0f, p.Crit);
                if (q == null) continue;
                q.Target = hm;
                q.Ai1 = 15f * k;
                DevPhantasmArrows++;
            }
            SpawnClass = keep;
        }

        /// <summary>
        /// AI_122 for 631 (P:35544-35604): waiting out its delay where it was made; then at 12 px
        /// straight for its enemy through anything, showing 15 an update; with its enemy gone it
        /// drifts on, solid now, fading 10 an update.
        /// </summary>
        void StepPhantasmArrow(Proj p)
        {
            var t = p.Target;
            bool lost = t == null || t.isDead || !t.gameObject.activeInHierarchy;
            if (lost) p.Target = null;
            if (p.Ai1 > 0f)
            {
                p.Ai1--;
                p.Vel = Vector2.zero;
                return;
            }
            var v = TV(p);
            if (lost)
            {
                if (v == Vector2.zero)
                {
                    Kill(p);
                    return;
                }
                p.Collide = true;
                p.Alpha += 10f / 255f;
                if (p.Alpha > 1f)
                {
                    Kill(p);
                    return;
                }
            }
            else
            {
                var to = EnemyCenterPx(t) - CenterPx(p);
                v = (to.sqrMagnitude > 1e-6f ? to.normalized : v.normalized) * 12f;
                p.Alpha = Mathf.Max(0f, p.Alpha - 15f / 255f);
            }
            p.TRot = ToRot(v) - Mathf.PI / 2f;
            p.Vel = Flip(v);
        }

        /// <summary>P:73849-73861: a burst of teal motes (229), floating up.</summary>
        static void KillPhantasmArrow(Proj p)
        {
            var c = CenterPx(p);
            for (int k = Random.Range(5, 10); k > 0; k--)
            {
                int i = BoxDust(c, 0, 0, 229, 0f, 0f, 100);
                if (i < 0) continue;
                D(i).Velocity *= 1.6f;
                D(i).Velocity.y--;
                D(i).Position -= Vector2.one * 4f;
                D(i).Position = Vector2.Lerp(D(i).Position, c, 0.5f);
                D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 706: the phoenix

        /// <summary>
        /// P:56736-56842, 58580-58590: its cry (dd2_phantom_phoenix_shot) and a burst of flame and
        /// smoke as it goes; once seen, flames and smoke on it and two flames riding a wave down its
        /// wings; eight frames five updates each; showing 55 an update, then a stretched ring of
        /// flames; turned along its flight (mirrored going left).
        /// </summary>
        static void StepPhoenix(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            int w = p.Spec.W;
            var tl = c - new Vector2(w / 4f, w / 4f);
            int Puff() => BoxDust(tl, w / 2, w / 2, Random.Range(0, 3) == 0 ? 6 : 31);
            if (p.LocalAi1 == 0f)
            {
                p.LocalAi1 = 1f;
                Sound("Custom/dd2_phantom_phoenix_shot_" + Random.Range(0, 3));
                for (int k = 0; k < 15; k++)
                {
                    if (Random.Range(0, 4) == 0) continue;
                    int i = Puff();
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2.3f;
                    D(i).FadeIn = 1.5f;
                }
            }
            if (p.Alpha <= 0f)
            {
                for (int k = 0; k < 2; k++)
                {
                    if (Random.Range(0, 4) == 0) continue;
                    int i = Puff();
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2.3f;
                    D(i).FadeIn = 1.5f;
                }
                var wave = RotateBy(new Vector2(0f, Mathf.Cos(p.FrameCounter * Mathf.PI * 2f / 40f - Mathf.PI / 2f) * 16f), p.TRot);
                var n = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
                foreach (var back in new[] { 1.2f, 1.7f })
                {
                    int i = BoxDust(tl, w / 2, w / 2, 6);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Position = c + wave + v * back;
                    D(i).Velocity = n * 2f;
                    D(i).FadeIn = 1.4f;
                    D(i).Scale = 1.15f;
                }
            }
            if (++p.FrameCounter >= 40) p.FrameCounter = 0;
            p.Frame = p.FrameCounter / 5;
            if (p.Alpha > 0f)
            {
                p.Alpha -= 55f / 255f;
                if (p.Alpha <= 0f)
                {
                    p.Alpha = 0f;
                    float a = ToRot(v);
                    for (int k = 0; k < 16; k++)
                    {
                        var off = RotateBy(new Vector2(0f, -1f), k * Mathf.PI * 2f / 16f);
                        off = RotateBy(new Vector2(off.x, off.y * 4f), a);
                        int i = BoxDust(c, 0, 0, 6, 0f, 0f, 0, 1.5f);
                        if (i < 0) continue;
                        D(i).NoGravity = true;
                        D(i).Position = c + off;
                        D(i).Velocity = D(i).Velocity * 4f + v * 0.3f;
                    }
                }
            }
            if (v.x < 0f)
            {
                p.Dir = -1f;
                p.TRot = Mathf.Atan2(-v.y, -v.x);
            }
            else
            {
                p.Dir = 1f;
                p.TRot = Mathf.Atan2(v.y, v.x);
            }
        }

        /// <summary>
        /// M:31097-31102, 31315-31320, P:80831: four copies at its old places 7, 5, 3, 1, redder and
        /// fainter further back ((9 - i) / 15) and toward half size; then itself, mostly solid
        /// (alpha 180 less three quarters of its alpha), mirrored going left.
        /// </summary>
        static void DrawPhoenix(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / 8;
            var spr = FxBatch.Frame(p.Spec.Texture, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f);
            bool flip = p.Dir < 0f;
            for (int i = 7; i >= 1; i -= 2)
            {
                if (i >= p.Trail.Count) continue;
                float k = (9 - i) / 15f;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), new Color(L * k, L / i * k, L / i * k, k / i), p.TRot, Mathf.Lerp(p.Scale, 0.5f, i / 15f), flipX: flip);
            }
            float a = 1f - p.Alpha;
            fx.Draw(spr, px, new Color(a, a, a, (180f - p.Alpha * 255f * 0.75f) / 255f), p.TRot, p.Scale, flipX: flip);
        }

        /// <summary>
        /// P:73046-73137 (as the explosive traps'): a 96 px blast (Item100), smoke, flames flung up
        /// and out, a ring of flame and smoke along its flight, smoke gores.
        /// </summary>
        void KillPhoenix(Proj p)
        {
            Blast(p, 96f, p.Damage, p.Knockback);
            Sound("Item_100");
            var c = CenterPx(p);
            var tl = c - new Vector2(48f, 48f);
            float a = ToRot(TV(p));
            Vector2 Disc(float r) => c + RotateBy(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * r;
            Vector2 Ring() => c + RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)), a) * 48f;
            for (int k = 0; k < 4; k++)
            {
                int i = BoxDust(tl, 96, 96, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Position = Disc(48f);
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 96, 96, 6, 0f, 0f, 200, 2.5f);
                if (i < 0) continue;
                D(i).Position = Disc(9.6f);
                D(i).Velocity *= 16f;
                if (D(i).Velocity.y > -2f) D(i).Velocity.y *= -0.4f;
                D(i).NoGravity = true;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 96, 96, 6, 0f, 0f, 100, 1.5f);
                if (i < 0) continue;
                D(i).Position = Disc(48f);
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
                D(i).FadeIn = 1.5f;
            }
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 96, 96, 6, 0f, 0f, 0, 2.7f);
                if (i < 0) continue;
                D(i).Position = Ring();
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 96, 96, 31, 0f, 0f, 0, 1.5f);
                if (i < 0) continue;
                D(i).Position = Ring();
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int k = 0; k < 4; k++)
            {
                var at = Disc(48f) - Vector2.one * 16f;
                if (Random.Range(0, 2) == 0) at.y -= 30f;
                pp.NewGore(at, Vector2.zero, Random.Range(61, 64), 1f, 0.3f, new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
            }
        }
    }
}
