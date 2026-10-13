using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R5b's spells (Terraria's space, px y down, per update): the Crystal Serpent's unseen
    /// charge (521, AI_029) bursting into shards (522), the Soul Drain's unseen box (476, AI_185)
    /// laid as far toward the cursor as can be seen, the Zapinators' wayward laser (876), Betsy's
    /// Wrath's three fireballs (711) and the Blood Thorn's spikes (756, AI_157) out of the ground.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevCrystalShards, DevZapJumps, DevSharpTears;

        static void RegisterMagic6()
        {
            // P:5910-5930: the charge (unseen, dust only; one more update a tick) and its shards.
            specs[ProjKind.CrystalCharge] = new Spec
            {
                Ai = Ai.Custom, W = 14, H = 14, Time = 3600, Extra = 1, Penetrate = 1, AlphaStart = 1f, Look = Look.Invisible,
                Update = (t, p) => StepCrystalCharge(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => t.KillCrystalCharge(p),
            };
            specs[ProjKind.CrystalChargeShard] = new Spec
            {
                Ai = Ai.Custom, W = 8, H = 8, Time = 3600, Penetrate = 1, AlphaStart = 1f, Look = Look.Invisible,
                Update = (t, p) => t.StepCrystalChargeShard(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => KillCrystalChargeShard(p),
                TileHit = (t, p, hx, hy, ov) =>
                {
                    if (hx) p.Vel.x = -ov.x;
                    if (hy) p.Vel.y = -ov.y;
                },
            };
            // P:5507-5518: the Soul Drain's box, 200 px, there 3 ticks; it soul-drains what it touches (P:11481) and knocks it from the player (P:13232).
            specs[ProjKind.SoulDrainBox] = new Spec
            {
                Ai = Ai.Custom, W = 200, H = 200, Time = 3, Penetrate = -1, TileCollide = false, OwnMove = true, Look = Look.Invisible, PushFromPlayer = true,
                Debuffs = new[] { (EnemyDebuffs.SoulDrain, 30, 1) },
                Update = (t, p) => { }, Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { }, OnHit = (t, p, hm) => PaySoulDrain(p),
            };
            // P:8418-8430: the Zapinator's laser (through eight, 1.4 big, three more updates a tick); P:16303 its light; P:81458 added in full under alpha 200.
            specs[ProjKind.ZapinatorLaser] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_876", W = 4, H = 4, Scale = 1.4f, Time = 3600, Extra = 3, Penetrate = 8, NoGravity = true, PivotBack = 38f,
                Look = Look.Additive, AlphaStart = 1f, AlphaFade = 10f / 255f, Light = new Color(0.075f, 0.525f, 0.75f),
                ModifyHit = (t, p, hm, dealt, crit) => ZapinatorHit(p, dealt, crit), OnKill = (t, p) => KillZapinatorLaser(p),
            };
            // P:7538-7550: Betsy's Wrath's fireball (30 px, 0.7 big, one more update a tick, each enemy once); it hits along its whole trail
            // (P:16171-16185, TrailCacheLength 20) and curses (Betsy's Curse 10 s, P:11373).
            specs[ProjKind.BetsysWrathBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_711", W = 30, H = 30, Scale = 0.7f, Time = 3600, Extra = 1, Penetrate = 1, AlphaStart = 1f, OncePerNpc = true,
                TrailLength = 20, Debuffs = new[] { (EnemyDebuffs.BetsysCurse, 600, 1) },
                Beam = p => (CenterPx(p), p.Trail.Count > 0 ? TerraParticles.ToPx(p.Trail[p.Trail.Count - 1]) : CenterPx(p), 30f * p.Scale),
                Update = (t, p) => StepBetsysWrath(p), Draw = (t, fx, p, px) => DrawBetsysWrath(fx, p, px), OnKill = (t, p) => KillBetsysWrath(p),
            };
            // P:7795-7806, 15933-15950: the blood thorn, 200 px long and 22 wide from its foot; three hits in all, each enemy every 25 ticks (P:13733).
            specs[ProjKind.SharpTear] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_756", W = 32, H = 32, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, AlphaStart = 1f,
                LocalCooldown = 25, CanDamage = p => p.Ai0 >= 0f && p.Hits < 3,
                Beam = p =>
                {
                    var v = TV(p);
                    var up = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down;
                    return (CenterPx(p), CenterPx(p) + up * 200f * p.Scale, 22f * p.Scale);
                },
                Update = (t, p) => StepSharpTear(p), Draw = (t, fx, p, px) => DrawSharpTear(fx, p, px), OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ 521/522: Crystal Serpent (AI_029)

        /// <summary>P:38392-38427: once fully in, pink motes (254) about it and two specks (255) along it each update.</summary>
        static void StepCrystalCharge(Proj p)
        {
            if (p.Alpha <= 0f)
            {
                var c = CenterPx(p);
                var tl = c - new Vector2(7f, 7f);
                var v = TV(p);
                for (int k = 0; k < 3; k++)
                {
                    int i = BoxDust(tl, 14, 14, 254, v.x, v.y, 50, 1.2f);
                    if (i < 0) continue;
                    D(i).Position = (D(i).Position + c) / 2f;
                    D(i).NoGravity = true;
                    D(i).Velocity *= 0.5f;
                }
                for (int k = 0; k < 2; k++)
                {
                    int i = BoxDust(tl, 14, 14, 255, v.x, v.y, 50, 0.4f);
                    if (i < 0) continue;
                    D(i).Position = (D(i).Position + (k == 0 ? c : c + v / 2f) * 5f) / 6f;
                    D(i).Velocity *= 0.1f;
                    D(i).NoGravity = true;
                    D(i).FadeIn = 1f;
                }
            }
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
        }

        /// <summary>P:74663-74702: Item110, a pink burst, and 3-5 shards flung 7-10 px a tick any way at 80%.</summary>
        void KillCrystalCharge(Proj p)
        {
            Sound("Item_110");
            var v = TV(p);
            CrystalBurst(p, 20, v, 1.1f, 0.35f, 1.2f, 2.5f, 0.01f);
            int n = Random.Range(3, 6);
            for (int k = 0; k < n; k++)
            {
                var dir = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                while (dir == Vector2.zero) dir = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                dir = dir.normalized * (Random.Range(70, 101) * 0.1f);
                if (Spawn(ProjKind.CrystalChargeShard, p.Pos - p.Vel * Px, Flip(dir), (int)(p.Damage * 0.8f), p.Knockback * 0.8f, p.Crit) != null) DevCrystalShards++;
            }
        }

        /// <summary>The crystal bursts' motes: some small fading specks (the next dust), the rest pink, flung out and back along the flight.</summary>
        static void CrystalBurst(Proj p, int count, Vector2 v, float fadeIn, float smallScale, float bigScale, float speed, float smallStep)
        {
            var c = CenterPx(p);
            int w = p.Spec.W;
            for (int k = 0; k < count; k++)
            {
                int i = BoxDust(c - new Vector2(w / 2f, w / 2f), w, w, 254, v.x * 0.1f, v.y * 0.1f, 0, 0.5f);
                if (i < 0) continue;
                if (Random.Range(0, 3) == 0)
                {
                    D(i).FadeIn = fadeIn + Random.Range(-10, 11) * 0.01f;
                    D(i).Scale = smallScale + Random.Range(-10, 11) * smallStep;
                    D(i).Type++;
                }
                else D(i).Scale = bigScale + Random.Range(-10, 11) * 0.01f;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * speed - v / 10f;
            }
        }

        /// <summary>P:38429-38459: dying after 40 updates, falling 0.2 (to 18), dragging 2% sideways, shedding smaller motes as it goes.</summary>
        void StepCrystalChargeShard(Proj p)
        {
            p.Ai1++;
            float k = (60f - p.Ai1) / 60f;
            if (p.Ai1 > 40f)
            {
                Kill(p);
                return;
            }
            var v = TV(p);
            v.y = Mathf.Min(v.y + 0.2f, 18f);
            v.x *= 0.98f;
            p.Vel = Flip(v);
            var c = CenterPx(p);
            var tl = c - new Vector2(4f, 4f);
            for (int n = 0; n < 2; n++)
            {
                int i = BoxDust(tl, 8, 8, 254, v.x, v.y, 50, 1.1f);
                if (i < 0) continue;
                D(i).Position = (D(i).Position + c) / 2f;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.3f;
                D(i).Scale *= k;
            }
            int j = BoxDust(tl, 8, 8, 255, v.x, v.y, 50, 0.6f);
            if (j < 0) return;
            D(j).Position = (D(j).Position + c * 5f) / 6f;
            D(j).Velocity *= 0.1f;
            D(j).NoGravity = true;
            D(j).FadeIn = 0.9f * k;
            D(j).Scale *= k;
        }

        /// <summary>P:74638-74662: Item118 and a small pink burst.</summary>
        static void KillCrystalChargeShard(Proj p)
        {
            Sound("Item_118");
            CrystalBurst(p, 10, TV(p), 0.75f, 0.25f, 1f, 1.25f, 0.005f);
        }

        // ------------------------------------------------------------------ 476: Soul Drain (AI_185)

        /// <summary>
        /// Pl:49076-49080, 50063-50088 (GetFarthestSpawnPositionOnLine): stepped out along the aim from
        /// the player's centre while the player (16 px ahead) can see it, to the cursor at most.
        /// </summary>
        public void ShootSoulDrain(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, Vector2 cursor, float face)
        {
            var start = TerraParticles.ToPx(from);
            var target = TerraParticles.ToPx(cursor);
            var v = Flip(velocity);
            var eye = start + new Vector2(face * 16f, 0f);
            float speed = v.magnitude, dist = (target - start).magnitude;
            var at = start;
            for (int n = 1; speed > 1e-4f && n < 200 && InSight(eye, at); n++)
            {
                at += v;
                if ((at - target).magnitude < 20f + Mathf.Abs(v.x) + Mathf.Abs(v.y) || speed * n >= dist)
                {
                    at = target;
                    break;
                }
            }
            Spawn(ProjKind.SoulDrainBox, TerraParticles.ToWorld(at), Vector2.zero, damage, knockback, crit);
        }

        /// <summary>AI_185: the mana is paid only once the box finds someone (ItemCheck_PayMana_ShouldPayManaAfterCheck).</summary>
        static void PaySoulDrain(Proj p)
        {
            if (p.LocalAi0 != 0f) return;
            p.LocalAi0 = 1f;
            TerraMod.Instance?.CheckMana(ItemDb.Get(ItemType.SoulDrain));
        }

        // ------------------------------------------------------------------ 876: Zapinators

        /// <summary>
        /// P:12691-12740: as it strikes, it may jump up to 256 px either way (1 in 20 each axis), and
        /// two times in three back up to 39 steps along its way (unless into blocks); it slows to 60%,
        /// may veer, and loses a tenth of its damage and knockback each hit, though one hit in 20
        /// knocks ten times as far and one in 50 strikes ten times as hard.
        /// </summary>
        static (int, float) ZapinatorHit(Proj p, int dealt, float crit)
        {
            var c = CenterPx(p);
            if (Random.Range(0, 20) == 0) { c.x += Random.Range(-256, 257); DevZapJumps++; }
            if (Random.Range(0, 20) == 0) { c.y += Random.Range(-256, 257); DevZapJumps++; }
            var v = TV(p);
            if (Random.Range(0, 3) != 0)
            {
                var keep = c;
                c -= v * Random.Range(0, 40);
                if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(c)))
                {
                    c = keep - v * Random.Range(0, 40);
                    if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(c))) c = keep;
                }
            }
            p.Pos = TerraParticles.ToWorld(c);
            v *= 0.6f;
            if (Random.Range(0, 7) == 0) v.x += 0.3f;
            if (Random.Range(0, 7) == 0) v.y += 0.3f;
            p.Vel = Flip(v);
            p.Damage = (int)(p.Damage * 0.9f);
            dealt = (int)(dealt * 0.9f);
            p.Knockback *= 0.9f;
            if (Random.Range(0, 20) == 0) p.Knockback *= 10f;
            if (Random.Range(0, 50) == 0)
            {
                p.Damage *= 10;
                dealt *= 10;
            }
            return (dealt, crit);
        }

        /// <summary>P:77650-77654: chips of what it hit and Item10.</summary>
        static void KillZapinatorLaser(Proj p)
        {
            if (p.HitTile) HitTileDust(p);
            Sound("Item_10");
        }

        // ------------------------------------------------------------------ 711: Betsy's Wrath

        /// <summary>
        /// Pl:49265-49280: three fireballs from 40 px out along the aim (if open), at 80% speed, each
        /// 1 px faster than the last and turned a degree further (toward the player's back).
        /// </summary>
        public void ShootBetsysWrath(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float face)
        {
            var start = TerraParticles.ToPx(from);
            var v = Flip(velocity);
            var out40 = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right) * 40f;
            if (InSight(start, start + out40)) start += out40;
            var slow = v * 0.8f;
            var unit = slow.sqrMagnitude > 1e-6f ? slow.normalized : Vector2.down;
            float step = Mathf.Deg2Rad * -face;
            for (int k = 0; k <= 2; k++)
                Spawn(ProjKind.BetsysWrathBolt, TerraParticles.ToWorld(start), Flip(RotateBy(slow + unit * k, k * step)), damage, knockback, crit);
        }

        /// <summary>
        /// AI_001 for 711 (P:57576-57612, 58505-58522): purple sparks (55) as it starts; it fades in
        /// from its third update, pulled down 0.1 from its 10th and 0.2 from its 20th, dragging 1%
        /// sideways (to 32 down); sparks off it and its trail.
        /// </summary>
        static void StepBetsysWrath(Proj p)
        {
            var c = CenterPx(p);
            var tl = c - new Vector2(10.5f, 10.5f);
            var v = TV(p);
            p.Ai0++;
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                for (int k = 0; k < 10; k++)
                {
                    int i = BoxDust(tl, 21, 21, 55, 0f, 0f, 200);
                    if (i < 0) continue;
                    D(i).Scale *= 0.65f;
                    D(i).Velocity = D(i).Velocity * 1.5f + v * 0.3f;
                    D(i).FadeIn = 0.7f;
                }
            }
            if (p.Ai0 >= 2f) p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            if (Random.Range(0, 4) == 0)
            {
                int i = BoxDust(tl, 21, 21, 55, 0f, 0f, 200);
                if (i >= 0)
                {
                    D(i).Scale *= 0.7f;
                    D(i).Velocity += v;
                }
            }
            if (Random.Range(0, 3) == 0 && p.Trail.Count > 9)
            {
                int i = BoxDust(TerraParticles.ToPx(p.Trail[9]) - new Vector2(10.5f, 10.5f), 21, 21, 55, 0f, 0f, 50);
                if (i >= 0)
                {
                    D(i).Scale *= 0.85f;
                    D(i).Velocity += v * 0.85f;
                    D(i).Color = new Color(0.5f, 0f, 0.5f);
                }
            }
            if (p.Ai0 >= 10f) v.y += 0.1f;
            if (p.Ai0 >= 20f) v.y += 0.1f;
            if (p.Ai0 > 20f) p.Ai0 = 20f;
            v.x *= 0.99f;
            v.y = Mathf.Min(v.y, 32f);
            p.Vel = Flip(v);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
        }

        /// <summary>
        /// M:30687-30690, 30862-30868, 31294-31301, P:81100: seven copies at its old places 19, 16 ...
        /// 1, from its own half-added white through purple (128 0 255 180) to black, fainter and
        /// shrinking toward 0.5 further back; then itself, 70 px up its art from the head.
        /// </summary>
        static void DrawBetsysWrath(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_711", 0, 0, 34, 90, 17f, 20f);
            if (spr == null) return;
            float o = 1f - p.Alpha;
            var self = new Color(o, o, o, 0.5f * o);
            var purple = new Color(128f / 255f, 0f, 1f, 180f / 255f);
            for (int i = 19; i > 0; i -= 3)
            {
                if (i >= p.Trail.Count) continue;
                float t = i / 19f;
                var col = t < 0.5f ? Color.Lerp(self, purple, t / 0.5f) : Color.Lerp(purple, Color.black, (t - 0.5f) / 0.5f);
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), col * ((19 - i) / 30f), p.TRot, Mathf.LerpUnclamped(p.Scale, 0.5f, i / 15f));
            }
            fx.Draw(spr, px, self, p.TRot, p.Scale);
        }

        /// <summary>P:72845-72936: DD2_BetsysWrathImpact (0.4), and fire, purple and gold sparks all down its trail.</summary>
        static void KillBetsysWrath(Proj p)
        {
            Sound("Custom/dd2_betsy_fireball_impact_*", 0.4f);
            float dir = p.Vel.x < 0f ? -1f : 1f;
            var pts = new List<Vector2> { CenterPx(p) };
            foreach (var q in p.Trail) pts.Add(TerraParticles.ToPx(q));
            void Spark(Vector2 at, int type, int alpha, float extraScale, Color tint)
            {
                int i = BoxDust(at - new Vector2(10.5f, 10.5f), 21, 21, type, dir, -2.5f, alpha);
                if (i < 0) return;
                D(i).Velocity *= 2.4f;
                D(i).Scale += Random.value * extraScale - 0.5f;
                if (type == 55) D(i).Color = tint;
            }
            for (int k = 0; k < pts.Count / 2; k += 2)
                for (int n = 0; n < 2; n++)
                    Spark(pts[k], new[] { 6, 55, 158 }[Random.Range(0, 3)], 200, 1f, Color.Lerp(new Color(0.5f, 0f, 0.7f, 0.5f), new Color(1f, 0.84f, 0f), Random.value));
            for (int k = 10; k < pts.Count; k += 2)
                for (int n = 0; n < 2; n++)
                    if (Random.Range(0, 3) != 0) Spark(pts[k], 55, 120, 0.7f, Color.Lerp(new Color(0.5f, 0f, 0.5f), Color.black, Random.value));
        }

        // ------------------------------------------------------------------ 756: Blood Thorn (AI_157)

        /// <summary>
        /// Pl:48436-48444: a thorn out of the ground near the cursor (FindSharpTearsSpot), pointing at
        /// the cursor, 16 px a tick its way, 0.8-1.3 big.
        /// </summary>
        public void ShootSharpTears(Vector2 from, int damage, float knockback, float crit, Vector2 cursor)
        {
            var c = TerraParticles.ToPx(from);
            var target = TerraParticles.ToPx(cursor) + Random.insideUnitCircle * 8f;
            var tile = FindSharpTearsSpot(c, target);
            var spot = new Vector2(tile.x * 16f + Random.Range(0, 17), tile.y * 16f + Random.Range(0, 17));
            var d = target - spot;
            var v = (d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down) * 16f;
            var p = Spawn(ProjKind.SharpTear, TerraParticles.ToWorld(spot), Flip(v), damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = Random.value * 0.5f + 0.8f;
            DevSharpTears++;
        }

        static bool SolidPx(int i, int j) => TerraWorld.IsSolid(i, -j - 1);

        /// <summary>
        /// Pl:50123-50199: the first block on the way to the cursor (or the cursor), then a solid block
        /// within 200 px of it (6 across, 16 up or down) with an open side facing back that way, at
        /// random; else any solid one there; else about the player.
        /// </summary>
        static Vector2Int FindSharpTearsSpot(Vector2 from, Vector2 target)
        {
            var d = target - from;
            float len = d.magnitude, reach = len;
            var dir = len > 1e-4f ? d / len : Vector2.zero;
            for (float s = 0f; s < len; s += 4f)
            {
                var at = from + dir * s;
                if (SolidPx(Mathf.FloorToInt(at.x / 16f), Mathf.FloorToInt(at.y / 16f)))
                {
                    reach = s;
                    break;
                }
            }
            var spot = from + dir * reach;
            int px = Mathf.FloorToInt(spot.x / 16f), py = Mathf.FloorToInt(spot.y / 16f);
            var open = new List<Vector2Int>();
            var shut = new List<Vector2Int>();
            for (int i = px - 6; i <= px + 6; i++)
                for (int j = py - 16; j <= py + 16; j++)
                {
                    if (!SolidPx(i, j)) continue;
                    if (Vector2.Distance(spot, new Vector2(i * 16 + 8, j * 16 + 8)) > 200f) continue;
                    bool opening = (i > px && !SolidPx(i - 1, j)) || (i < px && !SolidPx(i + 1, j)) || (j > py && !SolidPx(i, j - 1)) || (j < py && !SolidPx(i, j + 1));
                    (opening ? open : shut).Add(new Vector2Int(i, j));
                }
            var pick = open.Count > 0 ? open : shut;
            if (pick.Count == 0)
                return new Vector2Int(Mathf.FloorToInt(from.x / 16f) + Random.Range(-2, 3), Mathf.FloorToInt(from.y / 16f) + Random.Range(-2, 3));
            return pick[Random.Range(0, pick.Count)];
        }

        /// <summary>
        /// AI_157 for 756 (P:52268-52395): as it shows, a frame of six at random, 30 blood motes (5)
        /// along it and 30 more about it, and Item60; it grows in over 20 updates, fades from its 30th
        /// and is gone at 35, with two motes an update either way; a dim red light.
        /// </summary>
        static void StepSharpTear(Proj p)
        {
            bool growing = p.Ai0 < 20f && p.Ai0 >= 0f, fading = p.Ai0 >= 30f, done = p.Ai0 >= 35f;
            p.Ai0++;
            if (p.Ai0 < 0f)
            {
                p.Alpha = 1f;
                p.Scale = 0.1f * p.Ai1;
            }
            var v = TV(p);
            var c = CenterPx(p);
            if (p.LocalAi0 == 0f && p.Ai0 >= 0f)
            {
                p.LocalAi0 = 1f;
                p.TRot = ToRot(v);
                p.Frame = Random.Range(0, 6);
                for (int i = 0; i < 30; i++)
                {
                    int d = PointDust(c + Random.insideUnitCircle * 24f, 5, v * Mathf.Lerp(0.2f, 0.7f, Random.value));
                    if (d < 0) continue;
                    D(d).Velocity += Random.insideUnitCircle * 0.5f;
                    D(d).Scale = 0.8f + Random.value * 0.5f;
                }
                for (int j = 0; j < 30; j++)
                {
                    int d = PointDust(c + Random.insideUnitCircle * 24f, 5, Random.insideUnitCircle * 2f + v * Mathf.Lerp(0.2f, 0.5f, Random.value));
                    if (d < 0) continue;
                    D(d).Velocity += Random.insideUnitCircle * 0.5f;
                    D(d).Scale = 0.8f + Random.value * 0.5f;
                    D(d).FadeIn = 1f;
                }
                Sound("Item_60");
            }
            float opacity = 1f - p.Alpha;
            if (growing || fading)
            {
                if (growing)
                {
                    opacity = Mathf.Min(1f, opacity + 0.1f);
                    p.Scale = opacity * p.Ai1;
                }
                else opacity = Mathf.Max(0f, opacity - 0.2f);
                for (int k = 0; k < 2; k++)
                {
                    int d = PointDust(c + Random.insideUnitCircle * 16f, 5, v * Mathf.Lerp(0.2f, 0.5f, Random.value));
                    if (d < 0) continue;
                    D(d).Velocity = (D(d).Velocity + Random.insideUnitCircle * 0.5f) * 0.5f;
                    D(d).Scale = 0.8f + Random.value * 0.5f;
                }
            }
            p.Alpha = 1f - opacity;
            if (done) TerraProjectiles.Instance?.Kill(p);
        }

        /// <summary>
        /// M:31807-31822, P:80758: a dark red glow (Extra 98) behind, then its frame of six pivoting
        /// 16 px from its foot, three quarters lit; flattening over its last five updates.
        /// </summary>
        static void DrawSharpTear(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Ai0 < 0f) return;
            fx.Light(px, new Color(0.5f, 0.1f, 0.1f) * p.Scale, 2.5f);
            var lit = new Color(L * 0.75f, L * 0.75f, L * 0.75f, 1f);
            float squash = Mathf.Clamp01(Mathf.InverseLerp(35f, 30f, p.Ai0));
            var back = TV(p) * p.Scale * 0.5f;
            var glow = FxBatch.Whole("Extra_98");
            if (glow != null)
                fx.Draw(glow, px - back, new Color(67f / 255f * lit.r, 17f / 255f * lit.r, 17f / 255f * lit.r, 1f), p.TRot + Mathf.PI / 2f, p.Scale * 0.9f);
            int frame = Mathf.Clamp(p.Frame, 0, 5);
            fx.Draw(FxBatch.Frame("Projectile_756", 0, frame * 32, 200, 32, 16f, 16f), px, lit, p.TRot, new Vector2(p.Scale, p.Scale * squash));
        }
    }
}
