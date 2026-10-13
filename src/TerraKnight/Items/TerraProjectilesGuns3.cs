using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R6a, the deferred ranged and the Dead Cells pair (Terraria's space, px y down, per
    /// update): the Paintball Gun's paint (587, AI_001: unseen, a streak of coloured dust), the
    /// Barrel Launcher's barrel (1023, AI_001: bouncing harder off blocks, a cyan blast) and the
    /// Killing Deck's cards (1024, AI_113: curling, sticking in what they meet, called back).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevPaintballs, DevBarrelBlasts, DevBarrelBlastHits, DevBarrelBounces, DevCardsThrown, DevCardsStuck, DevCardsRecalled;

        /// <summary>Player.killingCardFireType: one card, two back to back, a fan of six, all called back.</summary>
        static int killingCardFireType;

        public static int KillingCardFireType => killingCardFireType;

        static void RegisterGuns3()
        {
            // P:6369-6377: the paint (10 px, one hit), never drawn (alpha 255 throughout).
            specs[ProjKind.Paintball] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_587", W = 10, H = 10, Time = 3600, Penetrate = 1, AlphaStart = 1f, Look = Look.Invisible,
                Update = (t, p) => t.StepPaintball(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => KillPaintball(p),
            };
            // P:9683-9694, 15071, 19998: the barrel (26 px, hitting with 8 more round it, a 14 px box against blocks; each enemy once,
            // gone at the first), 180 updates; a third harder once it has bounced (P:14627).
            specs[ProjKind.DeadCellsBarrel] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1023", W = 26, H = 26, Time = 180, Penetrate = 1, OncePerNpc = true, AlphaStart = 1f, TileBox = new Vector2(14f, 14f),
                HitArea = p => (p.Pos, new Vector2(42f, 42f) * Px),
                ModifyHit = (t, p, hm, dealt, crit) => (p.LocalAi1 > 0f ? (int)(dealt * 1.33f) : dealt, crit),
                Update = (t, p) => StepBarrel(p), Draw = (t, fx, p, px) => DrawBarrel(fx, p, px), OnKill = (t, p) => t.KillBarrel(p),
                TileHit = (t, p, hx, hy, ov) => BarrelTile(p, hx, hy, ov),
            };
            // P:9695-9707, 15067, 20078: the card (16 px, hitting with 6 more round it, a 4 px box against blocks; through all, each
            // enemy once until called back; one more update a tick); 8 frames of 18; harmless while stuck (P:12463), half again as
            // hard coming back (P:14623). Its trail is 40 places long (TrailCacheLength).
            specs[ProjKind.KillingDeckCard] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1024", W = 16, H = 16, Time = 3600, Extra = 1, Penetrate = -1, OncePerNpc = true, AlphaStart = 1f, OwnMove = true,
                TileBox = new Vector2(4f, 4f), Frames = 8, TrailLength = 40,
                CanDamage = p => p.Ai0 != 1f,
                HitArea = p => (p.Pos, new Vector2(28f, 28f) * Px),
                ModifyHit = (t, p, hm, dealt, crit) => (p.Ai0 == 2f ? (int)(dealt * 1.5f) : dealt, crit),
                OnHit = (t, p, hm) => CardHit(p, hm),
                TileHit = (t, p, hx, hy, ov) => CardTile(p, ov),
                Update = (t, p) => t.StepCard(p), Draw = (t, fx, p, px) => DrawCard(fx, p, px), OnKill = (t, p) => KillCard(p),
            };
        }

        // ------------------------------------------------------------------ 587: the Paintball Gun

        /// <summary>
        /// Pl:49542-49553: each shot jittered by up to half a pixel each way, taken a step forward
        /// when the way is clear, 4 px up, its colour one of twelve.
        /// </summary>
        public void ShootPaintball(Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            var v = Flip(velocity) + new Vector2(Random.Range(-1, 2) * 0.5f, Random.Range(-1, 2) * 0.5f);
            var at = TerraParticles.ToPx(from);
            if (TerraWorld.LineClearPx(at, at + v * 2f)) at += v;
            at.y -= 4f;
            var p = Spawn(ProjKind.Paintball, TerraParticles.ToWorld(at), Flip(v), damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = Random.Range(0, 12) / 6f;
            DevPaintballs++;
        }

        static Color PaintColour(Proj p)
        {
            var c = Hsl(p.Ai1, 1f, 0.5f);
            c.a = 200f / 255f;
            return c;
        }

        /// <summary>
        /// AI_001 for 587 (P:57165-57196): the shot's twang (Item5) on its second update with four
        /// motes of its paint (76) thrown out, then one an update along its path; falling 0.1 after
        /// 15 updates.
        /// </summary>
        void StepPaintball(Proj p)
        {
            var c = CenterPx(p);
            var v = TV(p);
            var paint = PaintColour(p);
            if (++p.LocalAi0 == 2f)
            {
                Sound("Item_5");
                for (int k = 0; k < 4; k++)
                {
                    int i = BoxDust(c - new Vector2(5f, 5f), 10, 10, 76, v.x, v.y, 0, 1.1f);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Color = paint;
                    var out_ = c - D(i).Position;
                    D(i).Velocity = (out_.sqrMagnitude > 1e-6f ? out_.normalized : Vector2.zero) * -3f + v / 2f;
                }
            }
            else if (p.LocalAi0 > 2f)
            {
                if (++p.Frame > 2) p.Frame = 0;
                int i = BoxDust(c - new Vector2(1f, 1f), 2, 2, 76, v.x * 0.2f, v.y * 0.2f, 0, 0.9f);
                if (i >= 0)
                {
                    D(i).Position = c;
                    D(i).NoGravity = true;
                    D(i).Color = paint;
                    D(i).Velocity = v * 0.5f;
                }
            }
            if (++p.Ai0 >= 15f)
            {
                p.Ai0 = 15f;
                v.y += 0.1f;
            }
            if (v.y > 16f) v.y = 16f;
            p.Vel = Flip(v);
        }

        /// <summary>P:73950-73978: a thud (Dig) and a splash of its paint, half of it flung back along its way.</summary>
        static void KillPaintball(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var c = CenterPx(p);
            var v = TV(p);
            var paint = PaintColour(p);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(c - new Vector2(5f, 5f), 10, 10, 76);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Color = paint;
                    D(i).Velocity = D(i).Velocity * 1.2f - v * 0.3f;
                    D(i).Scale = 0.9f;
                }
                i = BoxDust(c - new Vector2(1f, 1f), 2, 2, 76, 0f, 0f, 0, 1.1f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Color = paint;
                D(i).Velocity *= 2f;
            }
        }

        // ------------------------------------------------------------------ 1023: the Barrel Launcher

        /// <summary>
        /// AI_001 for 1023 (P:56176-56196, 58144-58155, 58603-58606): a puff of smoke 54 px ahead as
        /// it leaves the barrel (thrown on with the player's own speed); seen from its sixth update;
        /// falling 0.25 after 20; always tumbling forward, faster the faster it goes sideways.
        /// </summary>
        static void StepBarrel(Proj p)
        {
            var v = TV(p);
            if (p.Ai1 == 0f)
            {
                p.Ai1 = 1f;
                var hero = HeroController.SilentInstance;
                var pv = Vector2.zero;
                if (hero != null)
                {
                    var rb = hero.GetComponent<Rigidbody2D>();
                    if (rb != null) pv = Flip(rb.linearVelocity) / Px / 60f;
                }
                float push = Mathf.Lerp(0f, 0.5f, pv.magnitude / 3f);
                var dir = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right;
                var at = CenterPx(p) + dir * 54f + new Vector2(0f, -4f);
                float a = ToRot(v);
                for (int k = 0; k < 25; k++)
                {
                    int i = BoxDust(at, 16, 8, 31, v.x, v.y, 50, 1f + Random.value * 0.3f);
                    if (i < 0) continue;
                    D(i).Position = at + RotateBy(new Vector2(0f, (Random.value * 2f - 1f) * 10f), a);
                    D(i).Velocity = (RotateBy(v * (0.2f + push), (Random.value - 0.5f) * Mathf.PI / 16f) + pv * 0.25f) * 3f * Random.value;
                    D(i).FadeIn = 0.1f;
                    D(i).Position -= D(i).Velocity;
                    D(i).NoGravity = true;
                }
            }
            if (++p.Ai0 >= 6f) p.Alpha = 0f;
            if (p.Ai0 >= 20f)
            {
                p.Ai0 = 30f;
                v.y += 0.25f;
            }
            if (v.y > 16f) v.y = 16f;
            p.TRot += Mathf.Clamp(v.x * 0.025f, Mathf.PI / 16f, Mathf.PI / 6f);
            p.Vel = Flip(v);
        }

        /// <summary>
        /// P:18865-18894: off any block straight back (at 5 px at least sideways; sent up at 5 off
        /// the floor when it came down slowly), and from then on it hits a third harder.
        /// </summary>
        static void BarrelTile(Proj p, bool hx, bool hy, Vector2 before)
        {
            var o = Flip(before);
            var v = TV(p);
            bool bounced = false;
            if (hx)
            {
                v.x = -o.x;
                if (Mathf.Abs(o.x) < 5f) v.x = v.x < 0f ? -5f : 5f;
                bounced = true;
            }
            if (hy)
            {
                v.y = -o.y;
                if (o.y > 0f && o.y < 5f) v.y = -5f;
                bounced = true;
            }
            if (bounced)
            {
                p.LocalAi1 = 1f;
                DevBarrelBounces++;
                HitTileDust(p);
                Sound("Dig_" + Random.Range(0, 3));
            }
            p.Vel = Flip(v);
        }

        static void DrawBarrel(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Alpha >= 1f) return;
            fx.Draw(FxBatch.Frame("Projectile_1023", 0, 0, 28, 22, 14f, 11f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        /// <summary>
        /// P:78559-78580: its bang, a blow over 192 px (the one it struck already had its), the cyan
        /// burst (PO DeadCellsBarrelExplosion with its seven sparks), eight smoke gores and forty puffs
        /// thrown out from the middle.
        /// </summary>
        void KillBarrel(Proj p)
        {
            Sound("Custom/deadcells_barrel_launcher_explode", 0.6f);
            int damage = p.LocalAi1 > 0f ? (int)(p.Damage * 1.33f) : p.Damage;
            DevBarrelBlastHits += Blast(p, 192f, damage, p.Knockback).Count;
            DevBarrelBlasts++;
            var c = CenterPx(p);
            FxParticles.DeadCellsBarrelExplosion(c);
            for (float f = 0f; f < 1f; f += 1f / 7f)
            {
                int i = PointDust(c, 267, RotateBy(new Vector2(0f, 12f), Mathf.PI * 2f * f) * Random.value, 0, 2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Color = new Color(100f / 255f, 200f / 255f, 1f, 127f / 255f);
            }
            var pp = TerraParticles.Instance;
            if (pp != null)
                for (int k = 0; k < 8; k++)
                {
                    float a = Mathf.PI * 2f * k / 8f;
                    var at = c + Random.insideUnitCircle * 24f;
                    pp.NewGore(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.value * 8f, 61 + Random.Range(0, 3), exact: true);
                }
            for (int k = 0; k < 40; k++)
            {
                int i = BoxDust(c - new Vector2(96f, 96f), 192, 192, 31);
                if (i < 0) continue;
                var out_ = D(i).Position - c;
                D(i).Velocity = (out_.sqrMagnitude > 1e-6f ? out_.normalized : Vector2.zero) * Random.value * 8f;
            }
            var light = Glow.Create(transform, new Color(0.3f, 0.8f, 1f, 1f), 192f * Px * 0.6f);
            light.transform.position = new Vector3(p.Pos.x, p.Pos.y, Glow.Z);
            light.gameObject.AddComponent<FadeOut>().Begin(0.4f);
        }

        // ------------------------------------------------------------------ 1024: the Killing Deck

        /// <summary>
        /// Pl:49704-49775: the deck's four throws in turn, one a use: a card; two back to back; six
        /// fanned over 45 degrees, each curling one way or (at random) the other; then every card out
        /// called back, at once harmful again. The fourth turns back to the first when every card is
        /// already coming home.
        /// </summary>
        public void ShootKillingDeck(Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            if (killingCardFireType == 3)
            {
                bool allHome = true;
                foreach (var q in live)
                    if (!q.Dead && q.Kind == ProjKind.KillingDeckCard && q.Ai0 != 2f) allHome = false;
                if (allHome) killingCardFireType = 0;
            }
            float Curl() => (Random.Range(0, 2) * 2 - 1) * 0.4f;
            var v = Flip(velocity);
            switch (killingCardFireType)
            {
                default:
                    Card(from, v, damage, knockback, crit, Curl());
                    break;
                case 1:
                    Card(from, v, damage, knockback, crit, Curl());
                    Card(from, -v, damage, knockback, crit, Curl());
                    break;
                case 2:
                {
                    const float n = 6f;
                    float step = Mathf.PI / 4f / (n - 1f);
                    float curl = -1f;
                    for (int k = 0; k < (int)n; k++)
                    {
                        Card(from, RotateBy(v, step * (-(n - 1f) * 0.5f + k)), damage, knockback, crit, curl * 0.4f);
                        if (Random.Range(0, 2) == 0) curl = -curl;
                    }
                    break;
                }
                case 3:
                    foreach (var q in live)
                    {
                        if (q.Dead || q.Kind != ProjKind.KillingDeckCard) continue;
                        q.Vel = Vector2.zero;
                        q.Ai0 = 2f;
                        q.Immune.Clear();
                        DevCardsRecalled++;
                    }
                    break;
            }
            if (++killingCardFireType > 3) killingCardFireType = 0;
        }

        void Card(Vector2 from, Vector2 velDown, int damage, float knockback, float crit, float curl)
        {
            var p = Spawn(ProjKind.KillingDeckCard, from, Flip(velDown), damage, knockback, crit);
            if (p == null) return;
            p.Timer2 = curl;
            DevCardsThrown++;
        }

        static void CardDust(Vector2 c, Vector2 vel, int k)
        {
            int i = BoxDust(c - new Vector2(8f, 8f), 16, 16, 306, 0f, 0f, 0, 0.8f + Random.value * 1.7f);
            if (i < 0) return;
            D(i).Velocity = vel;
            D(i).Color = Color.Lerp(Color.white, new Color(140f / 255f, 234f / 255f, 180f / 255f), k / 10f);
            D(i).NoGravity = true;
        }

        static Vector2 SafeNormal(Vector2 v) => v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;

        /// <summary>
        /// AI_113 for 1024 (P:41290-41630). Thrown: shuffling through its faces, a flurry of green
        /// as it leaves, curling on a sine (40 × ai2 over a 60-update wave), dropping 0.35 and
        /// dragging 2% from its 60th update (12 at most). Stuck: in an enemy it rides its middle,
        /// in a block it holds while one is within 7 px; after 600 updates, or once loose, it comes
        /// back. Coming back: straight at the player at 16 px through everything, gone on touching
        /// them.
        /// </summary>
        void StepCard(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var c = CenterPx(p);
            var v = TV(p);
            if (p.Ai0 == 0f)
            {
                CardFrames(p);
                p.Ai1++;
                if (p.Ai1 >= 60f)
                {
                    v.x *= 0.98f;
                    v.y += 0.35f;
                }
                if (v.y > 12f) v.y = 12f;
                if (p.Ai1 == 1f)
                {
                    p.Frame = Random.Range(0, 8);
                    for (int k = 0; k < 6; k++) CardDust(c, RotateBy(SafeNormal(v), p.Timer2 * Mathf.PI * 0.1f) * 8f * (0.1f + Random.value * 1.2f), k);
                }
                p.TRot = ToRot(v);
                const float wave = Mathf.PI / 30f;
                float amp = 40f * p.Timer2;
                float curl = amp * wave * wave * -Mathf.Sin(p.Ai1 * wave);
                if (p.Ai1 == 1f) curl += amp * wave;
                v += RotateBy(new Vector2(0f, 1f), p.TRot) * curl;
                p.Vel = Flip(v);
                Move(p);
                if (!p.Dead && p.Ai0 == 0f && v.sqrMagnitude > 1e-6f) p.TRot = ToRot(TV(p));
                return;
            }
            if (p.Ai0 == 1f)
            {
                p.Frame = 0;
                p.Collide = false;
                if (p.LocalAi0 == 0f)
                {
                    for (int k = 0; k < 12; k++) CardDust(c, RotateBy(SafeNormal(v), Mathf.PI * 2f * k / 12f) * 8f * (0.1f + Random.value * 1.2f), k);
                    DevCardsStuck++;
                }
                p.LocalAi0++;
                bool loose = p.LocalAi0 >= 600f;
                if (p.Ai1 < 0f)
                {
                    // In a block: held while a solid block is within 7 px of it.
                    if (Blocked(p.Pos, new Vector2(15f, 15f) * Px, false, p)) p.Vel = Vector2.zero;
                    else loose = true;
                }
                else if (p.Target == null || p.Target.isDead || !p.Target.gameObject.activeInHierarchy) loose = true;
                else p.Pos = TerraParticles.ToWorld(EnemyCenterPx(p.Target) - v);
                if (loose)
                {
                    p.Vel = Vector2.zero;
                    p.Ai0 = 2f;
                    p.Immune.Clear();
                    p.Target = null;
                }
                return;
            }
            CardFrames(p);
            p.Collide = false;
            if (!Holder(out var owner))
            {
                Kill(p);
                return;
            }
            var hb = HeroController.SilentInstance.GetComponent<BoxCollider2D>().bounds;
            if (new Rect(hb.min, hb.size).Overlaps(new Rect(p.Pos - new Vector2(8f, 8f) * Px, new Vector2(16f, 16f) * Px)))
            {
                Kill(p);
                return;
            }
            v = SafeNormal(owner - c) * 16f;
            p.TRot = ToRot(v);
            if (p.LocalAi1 == 0f)
            {
                p.LocalAi1 = 1f;
                for (int k = 0; k < 6; k++)
                {
                    int i = PointDust(c, 306, SafeNormal(v) * 3f * (-1f + Random.value * 2f) + Random.insideUnitCircle, 0, 0.8f + Random.value * 1.7f);
                    if (i < 0) continue;
                    D(i).Color = Color.Lerp(Color.white, new Color(140f / 255f, 234f / 255f, 180f / 255f), k / 10f);
                    D(i).NoGravity = true;
                }
            }
            p.Vel = Flip(v);
            Move(p);
        }

        static void CardFrames(Proj p)
        {
            if (++p.FrameCounter > 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 8) p.Frame = 0;
            }
        }

        /// <summary>P:13244-13250: on an enemy (not on the way home) it sticks there, set three quarters of the way in.</summary>
        static void CardHit(Proj p, HealthManager hm)
        {
            if (p.Ai0 >= 2f) return;
            p.Ai0 = 1f;
            p.Ai1 = 1f;
            p.Target = hm;
            p.Vel = Flip((EnemyCenterPx(hm) - CenterPx(p)) * 0.75f);
        }

        /// <summary>P:18399-18413: on a block, flying, it chips it (Item10) and sticks in it, still facing the way it flew.</summary>
        static void CardTile(Proj p, Vector2 before)
        {
            if (p.Ai0 == 0f)
            {
                HitTileDust(p);
                Sound("Item_10");
            }
            if (p.Ai0 < 2f)
            {
                p.Ai0 = 1f;
                p.Ai1 = -1f;
                p.Vel = SafeNormal(before);
                p.Collide = false;
            }
        }

        /// <summary>P:76332-76341: Item8 and a ring of green.</summary>
        static void KillCard(Proj p)
        {
            Sound("Item_8");
            var c = CenterPx(p);
            var dir = SafeNormal(TV(p));
            if (dir == Vector2.zero) dir = Vector2.right;
            for (int k = 0; k < 12; k++) CardDust(c, RotateBy(dir, Mathf.PI * 2f * k / 12f) * 8f * (0.1f + Random.value * 1.2f), k);
        }

        /// <summary>
        /// M:28351-28369 (DrawTrail, 140/234/180): a 4 px green line along its last forty places,
        /// fading toward the tail; then the card, its own white (GetAlpha P:80992).
        /// </summary>
        static void DrawCard(FxBatch fx, Proj p, Vector2 px)
        {
            var pixel = FxBatch.Frame("MagicPixel", 0, 0, 1, 1, 0.5f, 1f);
            int n = 40;
            for (int k = Mathf.Min(n - 1, p.Trail.Count - 1); k >= 1; k--)
            {
                var a = TerraParticles.ToPx(p.Trail[k]);
                var b = TerraParticles.ToPx(p.Trail[k - 1]);
                var seg = b - a;
                if (seg.sqrMagnitude < 0.01f) continue;
                var col = new Color(140f / 255f, 234f / 255f, 180f / 255f, 1f) * (1f - (float)k / n);
                fx.Draw(pixel, a, col, Mathf.Atan2(seg.y, seg.x) + Mathf.PI / 2f, new Vector2(4f, seg.magnitude));
            }
            float o = 1f - p.Alpha;
            fx.Draw(FxBatch.Frame("Projectile_1024", 0, 18 * p.Frame, 18, 18, 9f, 9f), px, new Color(o, o, o, o), p.TRot, p.Scale, overPlayer: p.Ai0 == 1f);
        }
    }
}
