using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The held blades with the remade swings on (BladeArts.Enabled; the user's 10-26 "怎么没看见泰拉魔刃…优化其他近战武器"): held,
    /// the Arkhalis and the Terragrim dance through cuts of their own one after another toward the cursor (a cut down aslant,
    /// one up, one across, a wide turn...), the blade itself in the hand with a trail behind its edge and motes of its theme,
    /// reaching 35% further than the game's whirl and striking what it sweeps every 5 updates as the game's does; the Terragrim
    /// lets a cutting gale fly from every third cut (its "释放切割疾风"). The Piercing Starlight's flurry sweeps across a fan and
    /// reaches 35% further, a meteor thrust of twice the stars every so often. The right-click arts (BladeSkills): the gale
    /// run, the meteor flurry.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>A cut of the dance: from A0 to A1 degrees off the aim (+ up) over Ticks updates, its tip that share of the reach; Gale: a gale flies from it.</summary>
        struct DanceCut
        {
            public float A0, A1, Reach;
            public int Ticks;
            public bool Gale;
        }

        static readonly DanceCut[] dance =
        {
            new DanceCut { A0 = 70f, A1 = -60f, Ticks = 10, Reach = 1f },
            new DanceCut { A0 = -60f, A1 = 80f, Ticks = 10, Reach = 1f },
            new DanceCut { A0 = 40f, A1 = -40f, Ticks = 8, Reach = 1.1f, Gale = true },
            new DanceCut { A0 = 105f, A1 = -95f, Ticks = 13, Reach = 0.95f },
            new DanceCut { A0 = -35f, A1 = 60f, Ticks = 8, Reach = 1f },
            new DanceCut { A0 = 65f, A1 = -80f, Ticks = 9, Reach = 1.05f, Gale = true },
        };

        /// <summary>The game's whirl reaches about 50 px from the player's middle; the dance 35% further.</summary>
        const float HeldReachPx = 50f * 1.35f;

        sealed class HeldDance
        {
            public BladeArts.Art Art;
            public Sprite Blade;
            public int Cut, Tick;
            public float Angle, Side;
            public readonly List<Vector2> Tips = new List<Vector2>();
        }

        /// <summary>Dev: cuts danced, gales let fly, the farthest a dance's tip reached (px).</summary>
        public static int DevDanceCuts, DevGales;
        public static float DevDanceFar;

        /// <summary>A held blade's dance step (centre and aim in Terraria px, y down): its cut now, the blade's way, the hit along it.</summary>
        void StepHeldDance(Proj p, ItemDef def, Vector2 centre, Vector2 aim)
        {
            var d = p.HeldDance;
            if (d == null)
            {
                d = p.HeldDance = new HeldDance { Art = BladeArts.Of(def), Blade = SwordFx.BladeSprite(def) };
                DevDanceCuts++;
            }
            var cut = dance[d.Cut];
            if (++d.Tick > cut.Ticks)
            {
                if (cut.Gale && p.Kind == ProjKind.TerragrimSlash) Gale(def, centre, aim, p.Damage);
                d.Cut = (d.Cut + 1) % dance.Length;
                d.Tick = 1;
                cut = dance[d.Cut];
                DevDanceCuts++;
                Sound("Item_1", 0.45f);
            }
            d.Side = aim.x < 0f ? -1f : 1f;
            float u = d.Tick / (float)cut.Ticks;
            u = 1f - (1f - u) * (1f - u);
            float ang = Mathf.Lerp(cut.A0, cut.A1, u);
            // Off the aim, + up (y down here: up is negative).
            float baseAng = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            d.Angle = baseAng - ang * d.Side;
            var way = new Vector2(Mathf.Cos(d.Angle * Mathf.Deg2Rad), Mathf.Sin(d.Angle * Mathf.Deg2Rad));
            float reach = HeldReachPx * cut.Reach * Equipment.ItemScale(def);
            var tip = centre + way * reach;
            d.Tips.Add(TerraParticles.ToWorld(tip));
            if (d.Tips.Count > 7) d.Tips.RemoveAt(0);
            DevDanceFar = Mathf.Max(DevDanceFar, reach);
            // It strikes what the blade covers: a box round its middle as long as it is.
            p.Pos = TerraParticles.ToWorld(centre + way * reach * 0.55f);
            p.HitSize = new Vector2(reach, reach) * 0.95f;
            p.Dir = d.Side;
            var push = new Vector2(-way.y, way.x) * (cut.A1 < cut.A0 ? d.Side : -d.Side) * 2f;
            if (Random.value < 0.8f) SwordFx.SpearMote(d.Art, centre + way * reach * (0.5f + Random.value * 0.5f), push, d.Side);
        }

        /// <summary>The dance drawn: the blade in the hand along its way, a trail through the tip's last places behind it.</summary>
        static void DrawHeldDance(FxBatch fx, Proj p, Vector2 px)
        {
            var d = p.HeldDance;
            if (d == null || d.Blade == null) return;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var main = BladeArts.Tint(d.Art, d.Art.Main, 0f);
            var light = BladeArts.Tint(d.Art, d.Art.Light, 0.3f);
            for (int k = 1; k < d.Tips.Count; k++)
            {
                float f = k / (float)d.Tips.Count;
                var a = TerraParticles.ToPx(d.Tips[k - 1]);
                var b = TerraParticles.ToPx(d.Tips[k]);
                StreakPx(fx, a, b, Color.Lerp(main, light, f), 8f + 10f * f, 0.65f * f);
                StreakPx(fx, Vector2.Lerp(centre, a, 0.55f), Vector2.Lerp(centre, b, 0.55f), main, 5f + 6f * f, 0.3f * f);
            }
            float len = HeldReachPx * Equipment.ItemScale(ItemDb.Get(HeldBladeItem(p.Kind)));
            float scale = len / Mathf.Max(1f, d.Blade.rect.width);
            var hand = centre + new Vector2(Mathf.Cos(d.Angle * Mathf.Deg2Rad), Mathf.Sin(d.Angle * Mathf.Deg2Rad)) * 9f;
            fx.Draw(d.Blade, hand, new Color(L, L, L, 1f), d.Angle * Mathf.Deg2Rad, new Vector2(scale, scale), flipY: d.Side < 0f, overPlayer: true);
        }

        /// <summary>The Terragrim's cutting gale: a crescent of wind flying at the cursor, through what it meets.</summary>
        void Gale(ItemDef def, Vector2 centrePx, Vector2 aimPx, int damage)
        {
            var dir = aimPx.sqrMagnitude > 1e-6f ? aimPx.normalized : Vector2.right;
            SpawnClass = DamageClass.Melee;
            var g = Spawn(ProjKind.CuttingGale, TerraParticles.ToWorld(centrePx + dir * 30f), Flip(dir) * 11f, Mathf.Max(1, Mathf.RoundToInt(damage * 0.6f)), 2f, Equipment.Crit(def));
            if (g != null) g.Melee = true;
            DevGales++;
        }

        static void RegisterHeldArts()
        {
            specs[ProjKind.CuttingGale] = new Spec
            {
                Ai = Ai.Custom, Texture = "Extra_98", W = 40, H = 40, Time = 36, Penetrate = -1, TileCollide = true, LocalCooldown = 12,
                Update = (t, p) => t.StepGale(p), Draw = (t, fx, p, px) => DrawGale(fx, p, px), OnKill = (t, p) => { },
            };
        }

        static BladeArts.Art galeArt;
        static BladeArts.Art GaleArt => galeArt ?? (galeArt = BladeArts.Of(ItemDb.Get(ItemType.Terragrim)));

        void StepGale(Proj p)
        {
            p.Vel *= 0.985f;
            p.Alpha = Mathf.Clamp01((p.Ai0++ - 22f) / 14f);
            var v = Flip(p.Vel);
            if (Random.value < 0.7f) SwordFx.SpearMote(GaleArt, TerraParticles.ToPx(p.Pos) + new Vector2(-v.y, v.x).normalized * Random.Range(-16f, 16f), v * 0.2f, v.x < 0f ? -1f : 1f);
        }

        /// <summary>A crescent of three soft streaks across its way, bowed back.</summary>
        static void DrawGale(FxBatch fx, Proj p, Vector2 px)
        {
            var v = Flip(p.Vel);
            if (v.sqrMagnitude < 1e-6f) return;
            var f = v.normalized;
            var n = new Vector2(-f.y, f.x);
            float o = 1f - p.Alpha;
            var c = new Color(0.75f, 1f, 0.8f);
            for (int k = -2; k < 2; k++)
            {
                float t0 = k / 2f, t1 = (k + 1) / 2f;
                var a = px + n * (t0 * 22f) - f * (t0 * t0 * 10f);
                var b = px + n * (t1 * 22f) - f * (t1 * t1 * 10f);
                StreakPx(fx, a, b, c, 7f, 0.75f * o);
            }
        }

        // ------------------------------------------------------------------ the Piercing Starlight

        /// <summary>The flurry's way off the aim now (degrees): swept across a fan of 22 each way, round and round.</summary>
        static float StarlightSweep(Proj p) => 22f * Mathf.Sin(p.Updates * 2f * Mathf.PI / 40f);

        /// <summary>A meteor thrust every 45 updates: for 6 updates twice the stars.</summary>
        static bool StarlightMeteor(Proj p) => p.Updates % 45 < 6;

        /// <summary>The meteor flurry (the right-click art): the Piercing Starlight out on its own for 36 updates at twice its size.</summary>
        public void StarBurst(ItemDef def, HeroController hero)
        {
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(ProjKind.PiercingStarlight, centre, Vector2.right, Mathf.RoundToInt(Equipment.Damage(def) * 1.4f), Equipment.Knockback(def), Equipment.Crit(def));
            if (p == null) return;
            p.Melee = true;
            p.Ai1 = 2.2f;
            p.LocalAi1 = 36f;
            Sound("Item_105", 0.7f);
        }

        /// <summary>The gale run's gales (the right-click art): a gale from each of a few places along the run, at the cursor.</summary>
        public void GaleRun(ItemDef def, Vector2 fromWorld, Vector2 toWorld, int count)
        {
            var aim = AimPx();
            for (int k = 0; k < count; k++)
            {
                var at = TerraParticles.ToPx(Vector2.Lerp(fromWorld, toWorld, (k + 0.5f) / count));
                Gale(def, at, aim - at, Equipment.Damage(def));
            }
        }
    }
}
