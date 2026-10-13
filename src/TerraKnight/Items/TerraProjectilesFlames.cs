using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Flamethrower's and the Elf Melter's flames (sweep R4b3; 85, AI_193 in Terraria's space,
    /// px y down, per update: a burst of flame that swells, slows and smokes away, the Elf Melter's
    /// blue and frostbiting) and the Flare Gun's flares (163, 310, 1008, 1009; AI_033: they stick
    /// where they land and burn there, lighting the place).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterFlames()
        {
            // P:1662, 12475, 13488, 15075, 15569, 18415: the flame, 72 updates at 3 a tick, four enemies (each once, 15% less a hit), 15 armour
            // ignored, its box growing 10-40 px each side; harmless after 54; against a block it slows and holds.
            specs[ProjKind.FlamethrowerFlame] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_85", W = 6, H = 6, Time = 3600, Extra = 2, Penetrate = 4, OncePerNpc = true, AlphaStart = 1f, ArmorPen = 15, Frames = 7,
                CanDamage = p => p.LocalAi0 < 54f, Update = (t, p) => t.StepFlame(p), Draw = (t, fx, p, px) => DrawFlame(fx, p, px), OnKill = (t, p) => { },
                OnHit = (t, p, hm) =>
                {
                    EnemyDebuffs.Add(hm, p.Ai0 == 1f ? EnemyDebuffs.Frostbite : EnemyDebuffs.Hellfire, 1200);
                    p.Damage = (int)(p.Damage * 0.85f);
                },
                TileHit = (t, p, hx, hy, before) =>
                {
                    p.Vel = before * 0.95f;
                    p.Pos -= p.Vel * Px;
                },
            };
            // P:2517, 4024, 9582-9625: the flares, 10 minutes, through everything; P:11687-11703: on fire (cursed fire for 1009).
            foreach (var (kind, id) in new[] { (ProjKind.FlareProj, 163), (ProjKind.BlueFlareProj, 310), (ProjKind.SpelunkerFlareProj, 1008), (ProjKind.CursedFlareProj, 1009),
                         (ProjKind.RainbowFlareProj, 1010), (ProjKind.ShimmerFlareProj, 1011) })
                specs[kind] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = 6, H = 6, Time = 36000, Penetrate = -1, AlphaStart = 1f,
                    Update = (t, p) => t.StepFlare(p), Draw = (t, fx, p, px) => DrawFlare(fx, p, px), OnKill = (t, p) => { },
                    CanDamage = p => p.LocalAi0 == 0f,
                    TileHit = (t, p, hx, hy, before) => FlareStick(p, before),
                    OnHit = (t, p, hm) =>
                    {
                        if (p.Kind == ProjKind.CursedFlareProj) EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 600);
                        else EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, Random.Range(0, 3) == 0 ? 600 : 300);
                    },
                };
        }

        // ------------------------------------------------------------------ 85: the flames (AI_193)

        /// <summary>The Flamethrower's roar for each burst after the use's first.</summary>
        public void FlameSound() => Sound("Item_34");

        /// <summary>
        /// The Flamethrower's and the Elf Melter's use (Pl:43836-ish, 47735-47741, 49625-49629,
        /// 54307): five bursts, one each 6 ticks, from 6 px off the aim's side and on 4 steps if
        /// open, only the first using gel; the Elf Melter's are its blue ones (ai0 1).
        /// </summary>
        public void ShootFlame(Vector2 fromWorld, Vector2 velocity, int damage, float knockback, float crit, bool elf, float facing)
        {
            var v = Flip(velocity);
            var from = TerraParticles.ToPx(fromWorld) + RotateBy(new Vector2(0f, -6f * facing), ToRot(v));
            if (!Physics2D.Linecast(TerraParticles.ToWorld(from), TerraParticles.ToWorld(from + v * 5f), 1 << World.ChunkColliders.TerrainLayer)) from += v * 4f;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            var p = Spawn(ProjKind.FlamethrowerFlame, TerraParticles.ToWorld(from), velocity, damage, knockback, crit);
            SpawnClass = keep;
            if (p != null) p.Ai0 = elf ? 1f : 0f;
        }

        /// <summary>
        /// AI_193 (P:42076-42144): 60 updates of flight then 12 slowing 5% each, gone; flames (6;
        /// the Elf Melter's blue, 135) a quarter of the time, swept along at first, up to 60 px out
        /// as it swells; the Flamethrower's smokes (31) back toward the player after 50 updates.
        /// Its box swells from 10 to 40 px each side.
        /// </summary>
        void StepFlame(Proj p)
        {
            p.LocalAi0++;
            if (p.LocalAi0 >= 72f)
            {
                Kill(p);
                return;
            }
            var v = TV(p);
            if (p.LocalAi0 >= 60f) v *= 0.95f;
            bool elf = p.Ai0 == 1f;
            // The Foxsparks' breath (1106, P:42102-42112): flames and smoke only 15 px out, half as often.
            bool fox = p.Kind == ProjKind.FoxFlame;
            float flameR = fox ? 15f : 60f, smokeR = fox ? 15f : 50f, flameOdds = fox ? 0.125f : 0.25f;
            int smokeFrom = elf ? 0 : 50, flameUntil = elf ? 60 : 50;
            var c = CenterPx(p);
            float t = p.LocalAi0;
            if (t < flameUntil && Random.value < flameOdds)
            {
                var at = c + Random.insideUnitCircle * flameR * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(t / 72f));
                int i = BoxDust(at, 4, 4, elf ? 135 : 6, v.x * 0.2f, v.y * 0.2f, 100);
                if (i >= 0)
                {
                    if (Random.Range(0, 4) == 0)
                    {
                        D(i).NoGravity = true;
                        D(i).Scale *= 3f;
                        D(i).Velocity *= 2f;
                    }
                    else D(i).Scale *= 1.5f;
                    D(i).Scale *= 1.5f;
                    D(i).Velocity *= 1.2f;
                    D(i).Velocity += v * Mathf.Lerp(1f, 0.1f, Mathf.Clamp01(t / 45f)) * Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(t / 6f));
                }
            }
            if (smokeFrom > 0 && t >= smokeFrom && Random.value < 0.5f && Holder(out var player))
            {
                var away = (c - player).sqrMagnitude > 1e-6f ? (c - player).normalized : Vector2.right;
                var sv = RotateBy(away, Random.value * 0.3926991f - 0.19634955f) * 7f;
                int i = BoxDust(c + Random.insideUnitCircle * smokeR - sv * 2f, 4, 4, 31, 0f, 0f, 150);
                if (i >= 0)
                {
                    D(i).Color = new Color(80f / 255f, 80f / 255f, 80f / 255f, 1f);
                    D(i).NoGravity = true;
                    D(i).Velocity = sv;
                    D(i).Scale *= 1.1f + Random.value * 0.2f;
                }
            }
            // P:15075-15090: its box 10-40 px out each side; the breath's 0-20 (doubled and 10 more when fast).
            int box = (int)Mathf.Lerp(10f, 40f, Mathf.Clamp01(t / 72f));
            if (fox)
            {
                box = (int)Mathf.Lerp(0f, 20f, Mathf.Clamp01(t / 72f));
                if (v.magnitude > 10f) box = box * 2 + 10;
            }
            p.HitSize = new Vector2(6 + box * 2, 6 + box * 2);
            p.TRot = ToRot(v);
            p.Vel = Flip(v);
            if (p.LocalAi0 < 5f && !fox) FxBatch.Instance?.Light(c, elf ? new Color(0.3f, 0.5f, 1f) * 0.6f : new Color(1f, 0.5f, 0.1f) * 0.6f, 3f);
        }

        /// <summary>
        /// DrawProj_Flamethrower (M:35695-35757): its flame frame (the 4th; the Elf Melter's 4th to
        /// 6th as it goes) drawn in rings of spinning copies back along its path, the colour turning
        /// from orange to yellow to smoke (blue to white-blue for the Elf Melter) as it ages, and a
        /// second, redder, smaller layer for the Flamethrower.
        /// </summary>
        static void DrawFlame(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            bool elf = p.Ai0 == 1f;
            const float life = 72f;
            float t = p.LocalAi0, age = Mathf.Clamp01(t / life);
            if (age >= 1f) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / 7;
            int frame = elf ? (int)Mathf.Lerp(3f, 5f, Mathf.Clamp01((age - 0.5f) / 0.5f)) : 3;
            var spr = FxBatch.Frame(p.Spec.Texture, 0, fh * frame, tw, fh, tw / 2f, fh / 2f);
            Color C(int r, int g, int b, int a) => FxBatch.C(r, g, b, a);
            Color c1 = elf ? C(95, 120, 255, 200) : C(255, 80, 20, 200);
            Color c2 = elf ? C(50, 180, 255, 70) : C(255, 255, 20, 70);
            Color c3 = elf ? C(95, 160, 255, 100) : Color.Lerp(C(255, 80, 20, 100), c2, 0.25f);
            Color c4 = elf ? C(33, 125, 202, 100) : C(80, 80, 80, 100);
            float step = t > 50f ? 0.175f : 0.2f;
            float fade = Mathf.Clamp01(Mathf.InverseLerp(60f, 72f, t));
            fade = 1f - fade;
            float back = Mathf.Min(t, 20f);
            float grow = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01((age - 0.2f) / 0.3f));
            Color tint = age < 0.1f ? Color.Lerp(Color.clear, c1, age / 0.1f) : age < 0.2f ? Color.Lerp(c1, c2, (age - 0.1f) / 0.1f) : age < 0.35f ? c2
                : age < 0.7f ? Color.Lerp(c2, c3, (age - 0.35f) / 0.35f) : age < 0.85f ? Color.Lerp(c3, c4, (age - 0.7f) / 0.15f) : Color.Lerp(c4, Color.clear, (age - 0.85f) / 0.15f);
            var v = TV(p);
            float T = Time.time;
            for (int layer = 0; layer < 2; layer++)
            {
                if (layer == 1 && elf) break;
                for (float k = 1f; k >= 0f; k -= step)
                {
                    float near = (1f - k) * Mathf.Clamp01(age / 0.2f);
                    var at = px - v * back * k;
                    var col = tint * near;
                    var col6 = col;
                    if (!elf)
                    {
                        col6.g /= 2f;
                        col6.b /= 2f;
                        col6.a = Mathf.Min(col.a + 80f / 255f * near, 1f);
                    }
                    float spinRate = 1f / step * (k + 1f);
                    float r1 = p.TRot + k * Mathf.PI / 2f + T * spinRate * 2f, r2 = p.TRot - k * Mathf.PI / 2f - T * spinRate * 2f;
                    if (layer == 0)
                    {
                        fx.Draw(spr, at - v * back * step * 0.5f, col6 * fade * 0.25f, r1 + Mathf.PI / 4f, grow);
                        fx.Draw(spr, at, col6 * fade, r2, grow);
                    }
                    else
                    {
                        fx.Draw(spr, at - v * back * step * 0.2f, col * fade * 0.25f, r1 + Mathf.PI / 2f, grow * 0.75f);
                        fx.Draw(spr, at, col * fade, r2 + Mathf.PI / 2f, grow * 0.75f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 163, 310, 1008, 1009: the flares (AI_033)

        /// <summary>
        /// AI_033 (P:27426-27522): showing 50 an update; in flight it keeps its heading and falls
        /// 0.09 after 30 updates (to 16); sparks of its colour (127 orange, 187 blue, 169 gold, 75
        /// cursed) streaming off its tail once seen; stuck in a block it burns there, harmless, and
        /// flies on if the block goes. It lights the place round it. The Rainbow Flare's sparks (66)
        /// take the colour of the moment (half the size, three quarters the speed), the Shimmer
        /// Flare's are the shimmer's (310) (P:27455-27484).
        /// </summary>
        void StepFlare(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            var v = TV(p);
            var head = new Vector2(p.Ai0, p.Ai1);
            if (head == Vector2.zero) head = new Vector2(1f, 0f);
            var dir = head.normalized * 4f;
            var c = CenterPx(p);
            int dust = p.Kind switch
            {
                ProjKind.BlueFlareProj => 187, ProjKind.SpelunkerFlareProj => 169, ProjKind.CursedFlareProj => 75, ProjKind.RainbowFlareProj => 66, ProjKind.ShimmerFlareProj => 310, _ => 127,
            };
            var rainbow = FxParticles.HslToRgb(Time.time * 0.6f % 1f, 1f, 0.5f);
            if (p.Alpha < 70f / 255f)
            {
                int i = BoxDust(c - new Vector2(3f, 5f), 6, 6, dust, v.x, v.y, 100, 1.6f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Position -= dir;
                    D(i).Velocity -= dir;
                    if (p.Kind == ProjKind.RainbowFlareProj)
                    {
                        D(i).Color = rainbow;
                        D(i).Scale *= 0.5f;
                        D(i).Velocity *= 0.75f;
                    }
                }
            }
            if (p.LocalAi0 == 0f)
            {
                p.Ai0 = v.x;
                p.Ai1 = v.y;
                if (++p.LocalAi1 >= 30f)
                {
                    v.y += 0.09f;
                    p.LocalAi1 = 30f;
                }
            }
            else if (!Blocked(p.Pos, TileBox(p) * 0.5f * Px, false, p))
            {
                p.LocalAi0 = 0f;
                p.LocalAi1 = 30f;
                p.Collide = true;
            }
            if (v.y > 16f) v.y = 16f;
            p.TRot = Mathf.Atan2(p.Ai1, p.Ai0) + 1.57f;
            p.Vel = Flip(v);
            var glow = p.Kind switch
            {
                ProjKind.BlueFlareProj => new Color(0.3f, 0.6f, 1f), ProjKind.SpelunkerFlareProj => new Color(1f, 0.85f, 0.3f), ProjKind.CursedFlareProj => new Color(0.5f, 1f, 0.2f),
                // TorchID.ShimmerTorchLight: (0.9, 0.55, 1.2) of a slow pulse.
                ProjKind.RainbowFlareProj => rainbow, ProjKind.ShimmerFlareProj => new Color(0.81f, 0.5f, 1f),
                _ => new Color(1f, 0.6f, 0.2f),
            };
            FxBatch.Instance?.Light(c, glow * 0.9f, 6f);
        }

        /// <summary>P:18732-18745: on a block it sinks in by its last step and holds there.</summary>
        static void FlareStick(Proj p, Vector2 before)
        {
            if (p.LocalAi0 != 0f) return;
            p.Pos += before * Px;
            p.Vel = Vector2.zero;
            p.LocalAi0 = 1f;
            p.Collide = false;
        }

        /// <summary>M:30336-30338: the flare drawn 10 px back along it, bright.</summary>
        static void DrawFlare(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            float tw = whole.rect.width, th = whole.rect.height;
            float a = 1f - p.Alpha;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, (int)tw, (int)th, tw / 2f, th / 2f + 10f - 3f), px, new Color(a, a, a, a), p.TRot, p.Scale);
        }
    }
}
