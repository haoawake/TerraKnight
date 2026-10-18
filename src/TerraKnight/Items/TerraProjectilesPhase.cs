using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 1.4.5 thrown phaseblades (sweep S1.B; AI_003_Boomerang's IsAPhaseblade branches,
    /// P:39693-40355; Terraria's space, px y down): every use of a phaseblade, phasesaber or the
    /// Keybrand also throws the blade. It spins out for 30 updates, then drops, leaning over as
    /// it falls; whatever block it meets holds it (it stands there lit, glittering, and still
    /// cuts at 60% (sabers 50%) with a narrower edge); holding the use button calls it back at
    /// 40 px an update, faster the farther it is. While it is out the item can't be swung.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static readonly ProjKind[] phaseKinds =
        {
            ProjKind.PhaseBlue, ProjKind.PhaseRed, ProjKind.PhaseGreen, ProjKind.PhasePurple, ProjKind.PhaseWhite, ProjKind.PhaseYellow, ProjKind.PhaseOrange, ProjKind.PhasePink, ProjKind.PhaseRainbow,
            ProjKind.SaberBlue, ProjKind.SaberRed, ProjKind.SaberGreen, ProjKind.SaberPurple, ProjKind.SaberWhite, ProjKind.SaberYellow, ProjKind.SaberOrange, ProjKind.SaberPink, ProjKind.SaberRainbow,
            ProjKind.KeybrandThrown,
        };
        static readonly int[] phaseIds = { 1059, 1060, 1061, 1062, 1063, 1064, 1071, 1075, 1101, 1065, 1066, 1067, 1068, 1069, 1070, 1072, 1076, 1102, 1074 };

        /// <summary>Dev: the newest of a kind's ai[0] (a phaseblade: 0 out, 1 coming back, 2 stuck), -1 if none.</summary>
        public float DevState(ProjKind kind)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].Kind == kind && !live[i].Dead) return live[i].Ai0;
            return -1f;
        }

        /// <summary>ProjectileID.Sets.IsAPhaseblade.</summary>
        public static bool IsPhaseblade(ProjKind k) => k >= ProjKind.PhaseBlue && k <= ProjKind.KeybrandThrown;
        static bool IsSaber(ProjKind k) => k >= ProjKind.SaberBlue && k <= ProjKind.SaberRainbow;

        static void RegisterPhase()
        {
            for (int i = 0; i < phaseKinds.Length; i++)
            {
                var k = phaseKinds[i];
                int id = phaseIds[i];
                bool big = IsSaber(k) || k == ProjKind.KeybrandThrown;
                // P:10008-10060: 38×38 (the sabers and the Keybrand 46×46), aiStyle 3, through everything, 10 ticks between hits on one enemy.
                specs[k] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = big ? 46 : 38, H = big ? 46 : 38, Time = 3600, Penetrate = -1, LocalCooldown = 10, Held = true,
                    // Collision.TileCollision ignores blocks a projectile already overlaps; thrown from the player's middle, a 46 px blade
                    // starts 2-3 px into the floor under him, which here would stop it on the spot: meet blocks with 40 px of it.
                    TileBox = big ? new Vector2(46f, 40f) : Vector2.zero,
                    Update = (t, p) => t.StepPhaseblade(p), Draw = (t, fx, p, px) => DrawPhaseblade(fx, p, px), OnKill = (t, p) => { },
                    TileHit = (t, p, hx, hy, before) => LandPhaseblade(p, hx, hy, before),
                    // Stuck in a block (P:13340-13360, 15041-15044): 60% damage (the sabers 50%), 60% knockback, 14 px narrower each side.
                    ModifyHit = (t, p, hm, dmg, crit) =>
                    {
                        if (p.Kind == ProjKind.KeybrandThrown) dmg = KeyBrandStrike(hm, dmg, CenterPx(p));
                        if (p.Ai0 == 2f) dmg = (int)(dmg * (IsSaber(p.Kind) ? 0.5f : 0.6f));
                        return (dmg, crit);
                    },
                    KnockbackScale = p => p.Ai0 == 2f ? 0.6f : 1f,
                    HitArea = p => (p.Pos, new Vector2(p.Ai0 == 2f ? p.Spec.W - 28 : p.Spec.W, p.Spec.H) * Px),
                };
            }
        }

        /// <summary>Item.GetPhaseColorDirect (I:49114-49172): each blade's light, r g b each at most 1.</summary>
        static Color PhaseColorDirect(ProjKind k)
        {
            switch (k)
            {
                case ProjKind.PhaseBlue: case ProjKind.SaberBlue: return new Color(0.1f, 0.5f, 1f);
                case ProjKind.PhaseRed: case ProjKind.SaberRed: return new Color(1f, 0.2f, 0.1f);
                case ProjKind.PhaseGreen: case ProjKind.SaberGreen: return new Color(0.1f, 1f, 0.2f);
                case ProjKind.PhasePurple: case ProjKind.SaberPurple: return new Color(0.8f, 0.1f, 1f);
                case ProjKind.PhaseWhite: case ProjKind.SaberWhite: return new Color(0.8f, 0.9f, 1f);
                case ProjKind.PhaseYellow: case ProjKind.SaberYellow: return new Color(0.8f, 0.8f, 0f);
                case ProjKind.PhaseOrange: case ProjKind.SaberOrange: return new Color(0.9f, 0.5f, 0f);
                case ProjKind.PhasePink: case ProjKind.SaberPink: return new Color(1f, 0.1f, 0.4f);
                default: return Color.white;
            }
        }

        static readonly ProjKind[] rainbowOrder =
            { ProjKind.PhaseBlue, ProjKind.PhaseRed, ProjKind.PhaseGreen, ProjKind.PhasePurple, ProjKind.PhaseWhite, ProjKind.PhaseYellow, ProjKind.PhaseOrange, ProjKind.PhasePink, ProjKind.PhaseBlue };

        /// <summary>
        /// Item.GetPhaseColor: the blade's colour; the rainbow ones run through all eight (and back to
        /// blue) once every 300 ticks (Utils.MultiLerp over miscCounterNormalized), drawColor at half alpha.
        /// </summary>
        public static Color PhaseColor(ProjKind k, bool drawColor = false)
        {
            if (k != ProjKind.PhaseRainbow && k != ProjKind.SaberRainbow) return PhaseColorDirect(k);
            float t = Mathf.Repeat(Time.time * 60f, 300f) / 300f;
            float step = 1f / (rainbowOrder.Length - 1);
            int n = Mathf.Min(rainbowOrder.Length - 2, (int)(t / step));
            var c = Color.Lerp(PhaseColorDirect(rainbowOrder[n]), PhaseColorDirect(rainbowOrder[n + 1]), (t - step * n) / step);
            if (drawColor) c.a = 0.5f;
            return c;
        }

        /// <summary>NPC.KeyBrandStrike: up to twice the damage as the enemy's life runs from full to a tenth, and the Keybrand's sparkles where the blow lands.</summary>
        public static int KeyBrandStrike(HealthManager hm, int dmg, Vector2 fromPx)
        {
            var info = EnemyStats.Of(hm);
            if (info == null) return dmg;
            float life = (float)EnemyStats.Life(hm, info) / Mathf.Max(1, EnemyStats.LifeMax(info));
            float extra = Mathf.Clamp01(Mathf.InverseLerp(1f, 0.1f, life));
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                var b = col.bounds;
                var tl = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
                var br = TerraParticles.ToPx(new Vector2(b.max.x, b.min.y));
                FxParticles.Keybrand(new Vector2(Mathf.Clamp(fromPx.x, tl.x, br.x), Mathf.Clamp(fromPx.y, tl.y, br.y)));
            }
            return (int)(dmg * (1f + extra));
        }

        /// <summary>The thrown blade's damage and knockback (Pl:47488-47506): ×1.25 (the sabers ×1.5), knockback ×1.25; not the Keybrand's.</summary>
        public static (int, float) PhaseThrow(ProjKind k, int damage, float knockback)
        {
            if (k == ProjKind.KeybrandThrown) return (damage, knockback);
            return ((int)(damage * (IsSaber(k) ? 1.5f : 1.25f)), knockback * 1.25f);
        }

        void StepPhaseblade(Proj p)
        {
            bool key = p.Kind == ProjKind.KeybrandThrown;
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (p.SoundDelay > 0) p.SoundDelay--;
            // Out of time with the owner about: it stays, coming back (P:39700-39708).
            if (p.TimeLeft < 5)
            {
                p.TimeLeft = 5;
                p.Ai0 = 1f;
            }
            if (p.TRot < -Mathf.PI * 2f) p.TRot += Mathf.PI * 2f;
            if (p.TRot > Mathf.PI * 2f) p.TRot -= Mathf.PI * 2f;
            // The use button held with a phaseblade in hand calls it back.
            if (mod != null && p.Ai0 != 1f && mod.UseHeld && IsPhaseblade(mod.Inventory.Current.Def.Shoot)) p.Ai0 = 1f;
            var px = CenterPx(p);
            if (!key) FxBatch.Instance?.Light(px + new Vector2(0f, -8f), PhaseColor(p.Kind) * 0.5f, 3f);
            var v = TV(p);
            if (p.Ai0 == 0f)
            {
                p.Collide = true;
                p.Ai1++;
                if (p.Ai1 >= 30f)
                {
                    v.y = Mathf.Min(16f, v.y + 1f);
                    v.x *= 0.96f;
                    if (v.y > 0f)
                    {
                        // Leaning over to stand point down as it drops.
                        if (p.Dir > 0f)
                            p.TRot = p.TRot <= 2.335f ? (p.TRot * 4f + 2.335f) / 5f : (p.TRot * 9f + 2.335f + Mathf.PI * 2f) / 10f;
                        else
                            p.TRot = p.TRot >= -3.948f ? (p.TRot * 4f - 3.948f) / 5f : (p.TRot * 9f - 3.948f - Mathf.PI * 2f) / 10f;
                    }
                    else Spin(p, key, false);
                }
                else Spin(p, key, true);
                p.Vel = Flip(v);
                return;
            }
            if (p.Ai0 == 2f)
            {
                // Stuck: upright, only falling (P:40053-40075).
                p.Collide = true;
                if (v.y < 0f) v.y *= 0.5f;
                p.TRot = 2.335f;
                v.x = 0f;
                v.y += 1f;
                p.Vel = Flip(v);
                if (p.LocalAi1 == 0f)
                {
                    p.LocalAi1 = 1f;
                    if (key)
                    {
                        p.LocalAi0 = -1f;
                        Sound("Item_182");
                    }
                    else Sound("Item_15");
                }
                return;
            }
            // Coming back (P:40076-40237): 40 px an update, 3 a turn, no blocks; more updates the farther it is.
            p.Collide = false;
            Vector2 heroPx = hero != null ? TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center) : px;
            p.Dir = px.x < heroPx.x ? -1f : 1f;
            p.TRot = ToRot(v) - 2.335f;
            if (p.LocalAi0 <= 0f)
            {
                Sound(key ? (p.LocalAi0 < 0f ? "Item_182" : "Item_7") : "Item_15");
                p.LocalAi0 = 1f;
            }
            var to = heroPx - px;
            float dist = to.magnitude;
            p.Extra = Mathf.Min(20, (int)(dist / 3000f) + (key ? 1 : 0));
            if (dist > 0f) to *= 40f / dist;
            v.x = Approach(v.x, to.x, 3f);
            v.y = Approach(v.y, to.y, 3f);
            p.Vel = Flip(v);
            if (hero != null && hero.GetComponent<BoxCollider2D>().bounds.Intersects(new Bounds(p.Pos, new Vector3(p.Spec.W * Px, p.Spec.H * Px, 10f)))) Kill(p);
        }

        /// <summary>The spin while it flies (0.4 a tick) and its hum: Item_15 every 14 updates, the Keybrand's Item_7 every 11 (9 once dropping).</summary>
        static void Spin(Proj p, bool key, bool early)
        {
            p.TRot += 0.4f * p.Dir;
            if (p.SoundDelay != 0) return;
            if (key)
            {
                Sound("Item_7");
                p.SoundDelay = early ? 11 : 9;
            }
            else
            {
                Sound("Item_15");
                p.SoundDelay = 14;
            }
        }

        /// <summary>
        /// OnTileCollide (P:19490-19620): any block stops it (ai[0] = 2); landing on one from above
        /// (every update it rests there too) sends up five motes of its colour (the Keybrand
        /// fifteen gold ones), most floating up, now and then one thrown higher.
        /// </summary>
        static void LandPhaseblade(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (p.Ai0 == 1f) return;
            p.Ai0 = 2f;
            if (hitX) p.Vel.x = 0f;
            if (hitY) p.Vel.y = 0f;
            if (hitX || before.y >= 0f) return;
            var c = CenterPx(p);
            if (p.Kind == ProjKind.KeybrandThrown)
            {
                for (int k = 0; k < 15; k++)
                {
                    int type = Random.Range(0, 2) == 0 ? 43 : 57;
                    int i = PointDust(c, type, Random.insideUnitCircle * 2f, 255);
                    if (i < 0) continue;
                    ref var d = ref D(i);
                    if (Random.Range(0, 10) == 0)
                    {
                        d.Color = new Color(1f, 1f, 1f, 0f);
                        d.Scale *= 0.5f;
                        d.Velocity *= 0.7f;
                        d.Velocity.y -= 3f;
                    }
                    else
                    {
                        d.Color = FxBatch.C(200, 200, 200, 0);
                        if (Random.Range(0, 4) == 0) d.NoGravity = true;
                        d.Scale *= 0.5f;
                        d.Velocity.y = d.Velocity.y * 0.15f - 0.6f;
                        d.Velocity.x *= 0.6f;
                    }
                    d.Position.x += Random.Range(-4, 6);
                    if (Random.Range(0, 10) != 0) d.Velocity.x = d.Position.x < c.x ? -Mathf.Abs(d.Velocity.x) : Mathf.Abs(d.Velocity.x);
                    d.Position.y += 6f + Random.Range(0, 300) * 0.1f;
                }
                return;
            }
            int dust;
            Color tint = Color.white;
            switch (p.Kind)
            {
                case ProjKind.PhaseBlue: case ProjKind.SaberBlue: dust = 88; break;
                case ProjKind.PhaseRed: case ProjKind.SaberRed: dust = 90; break;
                case ProjKind.PhaseGreen: case ProjKind.SaberGreen: dust = 89; break;
                case ProjKind.PhasePurple: case ProjKind.SaberPurple: dust = 86; break;
                case ProjKind.PhaseYellow: case ProjKind.SaberYellow: dust = 87; break;
                case ProjKind.PhaseWhite: case ProjKind.SaberWhite: dust = 91; break;
                case ProjKind.PhaseOrange: case ProjKind.SaberOrange: dust = 138; break;
                case ProjKind.PhasePink: case ProjKind.SaberPink: dust = 325; break;
                default: dust = 267; tint = PhaseColor(p.Kind); break;
            }
            for (int k = 0; k < 5; k++)
            {
                int i = PointDust(c, dust, Random.insideUnitCircle * 2f);
                if (i < 0) continue;
                ref var d = ref D(i);
                d.Color = tint;
                if (Random.Range(0, 10) == 0)
                {
                    d.Scale *= 0.45f;
                    d.Velocity *= 0.7f;
                    d.Velocity.x *= 1.2f;
                    d.Velocity.y -= 3.5f;
                    if (dust == 267)
                    {
                        d.Velocity *= 2f;
                        d.NoGravity = true;
                    }
                }
                else
                {
                    d.Velocity *= 0.3f;
                    d.Scale *= 0.7f;
                    d.NoGravity = true;
                    d.Velocity.y -= 0.5f;
                }
                d.Position.x += Random.Range(-2, 4) + (dust != 138 ? 1 : 0);
                d.Position.y += 6f + Random.Range(0, p.Spec.H < 42 ? 240 : 320) * 0.1f;
            }
        }

        /// <summary>
        /// Main.DrawProj's generic branch: the whole blade about its centre, turned (never mirrored:
        /// spriteDirection stays 1, and the drop leans either way onto the same 2.335), full bright
        /// (GetAlpha: white); the rainbow ones' glowmask (Glow_372, Glow_373) over it in their
        /// running colour, once whole and once at half alpha.
        /// </summary>
        static void DrawPhaseblade(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Whole(p.Spec.Texture), px, Color.white, p.TRot, p.Scale);
            if (p.Kind != ProjKind.PhaseRainbow && p.Kind != ProjKind.SaberRainbow) return;
            var glow = FxBatch.Whole(p.Kind == ProjKind.PhaseRainbow ? "Glow_372" : "Glow_373");
            fx.Draw(glow, px, PhaseColor(p.Kind), p.TRot, p.Scale);
            fx.Draw(glow, px, PhaseColor(p.Kind, true), p.TRot, p.Scale);
        }
    }
}
