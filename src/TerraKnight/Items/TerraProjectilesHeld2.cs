using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The held blades (sweep S1.D; AI_075, P:67685-67825, 68380-68428; Terraria's space, px y
    /// down): the Arkhalis (595) and the Terragrim (735), a whirl of 28 frames 15 px out along
    /// the aim while the button is held, each enemy cut every 5 ticks; and the Piercing Starlight
    /// (927), a flurry of starlit thrusts reaching 200 px, each enemy struck every 4 ticks.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterHeld2()
        {
            // P:6439-6451, 7738-7749: 68×64, through everything, the Arkhalis 20 armour penetration.
            foreach (var (k, id, pen) in new[] { (ProjKind.ArkhalisSlash, 595, 20), (ProjKind.TerragrimSlash, 735, 0) })
                specs[k] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = 68, H = 64, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                    ArmorPen = pen, Frames = 28, Update = (t, p) => t.StepHeldBlade(p), Draw = (t, fx, p, px) => DrawHeldBlade(fx, p, px), OnKill = (t, p) => { },
                    LocalCooldown = 5,
                };
            // P:8783-8794: 40×40; it strikes along twenty boxes reaching five of its widths out (P:15662-15676).
            specs[ProjKind.PiercingStarlight] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_927", W = 40, H = 40, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                LocalCooldown = 4, Update = (t, p) => t.StepStarlight(p), Draw = (t, fx, p, px) => DrawStarlight(fx, p, px), OnKill = (t, p) => { },
                Beam = p =>
                {
                    var n = TV(p).sqrMagnitude > 1e-6f ? TV(p).normalized : Vector2.right;
                    var c = CenterPx(p);
                    return (c, c + n * 40f * 5f * p.Scale, 40f * p.Scale);
                },
            };
        }

        /// <summary>A held blade at the player's centre with ai[1] set (the Piercing Starlight's size).</summary>
        public void ShootHeld(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float ai1)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.Ai1 = ai1;
        }

        /// <summary>The held blade's item (the Arkhalis's, the Terragrim's).</summary>
        static ItemType HeldBladeItem(ProjKind k) => k == ProjKind.ArkhalisSlash ? ItemType.Arkhalis : k == ProjKind.TerragrimSlash ? ItemType.Terragrim : ItemType.PiercingStarlight;

        /// <summary>
        /// AI_075 for 595 and 735: the 28 frames over and over, Item_1 every 12 ticks; while the
        /// button is held it points at the cursor at shootSpeed × scale (15), else it's gone;
        /// a white light and now and then a mote (63, the Terragrim's 302) three of its steps out.
        /// UpdatePosition then moves it its velocity off the player's centre.
        /// </summary>
        void StepHeldBlade(Proj p)
        {
            var item = HeldBladeItem(p.Kind);
            var def = ItemDb.Get(item);
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || !Channel(item))
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            if (++p.Frame >= 28) p.Frame = 0;
            if (--p.SoundDelay <= 0)
            {
                Sound("Item_1");
                p.SoundDelay = 12;
            }
            var aim = AimPx() - centre;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(mod.UseDirection, 0f);
            var v = aim * def.ShootSpeed * Equipment.ItemScale(def);
            p.Vel = Flip(v);
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = ToRot(v) + (p.Dir < 0f ? Mathf.PI : 0f);
            p.Pos = TerraParticles.ToWorld(centre + v);
            mod.KeepUsing(def, Flip(v));
            var at = centre + v + v * 3f;
            FxBatch.Instance?.Light(at, new Color(0.8f, 0.8f, 0.8f), 3f);
            if (Random.Range(0, 3) == 0)
            {
                int i = BoxDust(at - new Vector2(34f, 32f), 68, 64, p.Kind == ProjKind.ArkhalisSlash ? 63 : 302, v.x, v.y, 100, 2f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Position -= v;
                }
            }
        }

        /// <summary>Main.DrawProj's framed branch: its frame, mirrored with the player, white-lit and half added (GetAlpha: 85% to white, alpha 128).</summary>
        static void DrawHeldBlade(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 28;
            var c = Color.Lerp(new Color(L, L, L, 1f), Color.white, 0.85f);
            c.a = 128f / 255f;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * fh, w, fh - 1, w / 2f, 32f), px, c, p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        /// <summary>
        /// AI_075 for 927: scaled by ai[1] (the item's scale, up to twice with melee speed), a count of
        /// 8 round and round, Item_1 every 6 ticks; while held it points at the cursor at 15 px,
        /// else it's gone; light along its 80 px; a new seed for the drawing each tick.
        /// </summary>
        void StepStarlight(Proj p)
        {
            var def = ItemDb.Get(ItemType.PiercingStarlight);
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || !Channel(ItemType.PiercingStarlight))
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            p.Scale = p.Ai1 > 0f ? p.Ai1 : 1f;
            if (++p.Ai0 >= 8f) p.Ai0 = 0f;
            if (--p.SoundDelay <= 0)
            {
                Sound("Item_1");
                p.SoundDelay = 6;
            }
            var aim = AimPx() - centre;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(mod.UseDirection, 0f);
            var v = aim * def.ShootSpeed;
            p.Vel = Flip(v);
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = ToRot(v);
            p.Pos = TerraParticles.ToWorld(centre + v);
            mod.KeepUsing(def, Flip(v));
            for (float d = 0f; d <= 80f; d += 32f) FxBatch.Instance?.Light(centre + aim * d, new Color(0.5f, 0.5f, 0.5f), 2f);
            p.LocalAi0 = Random.Range(0, int.MaxValue);
        }

        /// <summary>
        /// Main.DrawProj_PiercingStarlight, drawn twice from this tick's seed: the sword itself a
        /// few px ahead of the hand, scattered a little and fading, then 3×ai[1] star streaks of
        /// the Empress's colours (Projectile_927, added) stabbing 30-80 px ahead, the later ones
        /// 56 px further, with a white core.
        /// </summary>
        static void DrawStarlight(FxBatch fx, Proj p, Vector2 px)
        {
            var rng = new System.Random((int)p.LocalAi0);
            float R() => (float)rng.NextDouble();
            float RD() => R() * 2f - 1f;
            Vector2 Circ(float r) { float a = R() * Mathf.PI * 2f, l = Mathf.Sqrt(R()) * r; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * l; }
            float Fade(float x) => Mathf.Clamp01(Mathf.InverseLerp(0f, 0.3f, x)) * Mathf.Clamp01(Mathf.InverseLerp(1f, 0.5f, x));
            float num = p.Ai1 == 0f ? 1f : p.Ai1;
            int streaks = Mathf.CeilToInt(3f * num);
            var dir = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
            var basePos = px - dir * 2f;
            var sword = FxBatch.Whole("Item_4923");
            var star = FxBatch.Whole("Projectile_927");
            bool left = p.TRot < -Mathf.PI / 2f || p.TRot > Mathf.PI / 2f;
            for (int pass = 0; pass < 2; pass++)
            {
                float r5 = R();
                float a9 = p.TRot + RD() * Mathf.PI * 2f * 0.04f;
                float dist = 8f + Mathf.Lerp(0f, 20f, r5) + R() * 6f;
                float rot = a9 + Mathf.PI / 4f + (left ? Mathf.PI / 2f : 0f);
                var at = basePos + new Vector2(Mathf.Cos(a9), Mathf.Sin(a9)) * dist + Circ(8f);
                if (sword != null) fx.Draw(sword, at, new Color(L, L, L, 1f) * Fade(r5), rot, 1f, flipX: left);
                for (int k = 0; k < streaks; k++)
                {
                    float r11 = R();
                    float f12 = Fade(r11);
                    float n13 = Mathf.Lerp(0.6f, 1f, f12);
                    var fairy = FxParticles.HslToRgb(((R() * 0.33f + Time.time) % 1f + 0.5f) % 1f, 1f, 0.5f);
                    fairy.a = 0.25f;
                    var c2 = fairy * (f12 * 0.5f);
                    var c3 = Color.white * f12;
                    c3.a *= 0.5f;
                    var c4 = c3 * 0.5f;
                    float n15 = (num - 1f) / 2f;
                    float n16 = R() * 2f * num + n15;
                    float n17 = RD();
                    var scale = new Vector2(2.8f + n16 * (1f + n15), 1f) * n13;
                    var ahead = dir * (k >= 1 ? 56f : 0f);
                    float tilt = (0.03f - k * 0.012f) / num;
                    float reach = 30f + Mathf.Lerp(0f, 50f * num, r11) + n16 * 16f;
                    float a20 = p.TRot + n17 * Mathf.PI * 2f * tilt;
                    var sp = basePos + new Vector2(Mathf.Cos(a20), Mathf.Sin(a20)) * reach + Circ(20f) + ahead;
                    if (star == null) continue;
                    fx.Draw(star, sp, c2, a20, scale, overPlayer: true);
                    fx.Draw(star, sp, c4, a20, scale * 0.6f, overPlayer: true);
                }
            }
        }
    }
}
