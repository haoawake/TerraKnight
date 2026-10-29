using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The right-click arts' looks (BladeSkills), particles and Terraria's own pictures, no blocks of colour (the user's
    /// 10-26 "残留动画还是一大个矩形太丑了，不能做成粒子动画吗"): the weapon held in its charging pose, brightening; the
    /// katana's tip scoring the ground; a run's cut a soft line of light (Terraria's sparkle streak, Extra_98, tapering at
    /// its ends) that opens bright and shrinks to its middle, white sparks and the weapon's motes thrown off along it, a
    /// cross of light on each enemy cut, the blade's afterimages where the player went; what is thrown turning, its glow
    /// round after it; the shock running along the ground a streak of light over flying rubble, the dragon fire a wall of
    /// flame motes; pillars of fire flame motes over a soft glow; ice the Deerclops' own ice spikes (Projectile_961) shooting
    /// up out of the ground, frost at their feet, shattering into ice; thorns the Vilethorn's own vine (Projectile_7, 8)
    /// growing out of the ground a piece at a time and drawing back into it, leaves falling; the terra run's trail motes of its light and twinkles; portals
    /// sparks wheeling round; the Psycho Knife's going a puff of smoke.
    /// </summary>
    internal sealed partial class SwordFx
    {
        static Sprite streak;

        /// <summary>A soft line of light from a to b (px), about thick px across: Terraria's Extra_98, its length along the line.</summary>
        void Streak(FxBatch fx, Vector2 a, Vector2 b, Color c, float thick, float alpha, bool over = true)
        {
            if (alpha <= 0.01f) return;
            if (streak == null) streak = FxBatch.Whole("Extra_98");
            var d = b - a;
            float len = d.magnitude;
            if (len < 1f || streak == null) return;
            float rot = Mathf.Atan2(d.y, d.x) - Pi / 2f;
            fx.Draw(streak, (a + b) * 0.5f, Glow(c, alpha), rot, new Vector2(thick / streak.rect.width * 2f, len / streak.rect.height), overPlayer: over);
        }

        /// <summary>The twinkles over the terra run's trail (Terraria px), each a moment.</summary>
        readonly List<(Vector2 At, float Age, float Spin)> twinkles = new List<(Vector2, float, float)>();

        static void RunColours(BladeSkills.Strike s, out Color tint, out Color light)
        {
            var art = BladeArts.Of(s.Item);
            switch (s.Style)
            {
                case BladeSkills.RunStyle.Dive: tint = new Color(0.85f, 0.85f, 0.8f); light = Color.white; break;
                case BladeSkills.RunStyle.Shadow: tint = new Color(0.5f, 0.25f, 0.8f); light = new Color(0.8f, 0.6f, 1f); break;
                case BladeSkills.RunStyle.Terra: tint = new Color(0.25f, 0.9f, 0.4f); light = new Color(0.75f, 1f, 0.7f); break;
                case BladeSkills.RunStyle.Execute: tint = new Color(1f, 0.82f, 0.3f); light = new Color(1f, 0.97f, 0.75f); break;
                default: tint = BladeArts.Tint(art, art.Main, 0f); light = BladeArts.Tint(art, art.Light, 0.2f); break;
            }
        }

        static BladeArts.Theme RunTheme(BladeSkills.Strike s) =>
            s.Style == BladeSkills.RunStyle.Dive ? BladeArts.Theme.Feather : s.Style == BladeSkills.RunStyle.Shadow ? BladeArts.Theme.Shadow
            : s.Style == BladeSkills.RunStyle.Terra ? BladeArts.Theme.Terra : s.Style == BladeSkills.RunStyle.Execute ? BladeArts.Theme.Holy : BladeArts.Of(s.Item).Theme;

        void TickSkills(HeroController hero)
        {
            TickCharge(hero);
            for (int i = twinkles.Count - 1; i >= 0; i--)
            {
                var w = twinkles[i];
                if (w.Age + 1f / 60f > 0.5f) twinkles.RemoveAt(i);
                else twinkles[i] = (w.At, w.Age + 1f / 60f, w.Spin);
            }
            foreach (var s in BladeSkills.lines)
            {
                var a = TerraParticles.ToPx(s.From);
                var b = TerraParticles.ToPx(s.To);
                var art = BladeArts.Of(s.Item);
                RunColours(s, out var tint, out var light);
                // Speed lines where the player passes.
                if (s.Age <= BladeSkills.DashTime)
                {
                    var at = Vector2.Lerp(a, b, Mathf.Clamp01(s.Age / BladeSkills.DashTime));
                    for (int k = 0; k < 4; k++)
                    {
                        int m = Dust(at + new Vector2(Random.Range(-10f, 10f), Random.Range(-18f, 6f)), 278, (a - b).normalized * (2f + Random.value * 3f), 60, 0.6f, true, Color.Lerp(light, Color.white, 0.5f));
                        FadeIn(m, 0.5f);
                    }
                }
                if (!s.Cut || s.Shed) continue;
                s.Shed = true;
                var theme = RunTheme(s);
                var along = (b - a).sqrMagnitude > 1f ? (b - a).normalized : new Vector2(s.Facing, 0f);
                var across = new Vector2(-along.y, along.x);
                for (int k = 0; k < 16 + 3 * art.Level; k++)
                    Mote(theme, art, Vector2.Lerp(a, b, Random.value), along * (0.5f + Random.value * 1.5f) + across * (Random.value - 0.5f) * 3f, s.Facing, true);
                // The cut breaking into fine white sparks thrown off to both sides.
                for (int k = 0; k < 40; k++)
                {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    int m = Dust(Vector2.Lerp(a, b, Random.value), 278, across * side * (0.6f + Random.value * 2.2f) + along * Random.value * 1.5f, 30, 0.35f + Random.value * 0.35f, true, Random.value < 0.6f ? Color.white : light);
                    FadeIn(m, 0.6f);
                }
            }
            var t = BladeSkills.thrown;
            if (t != null && !BladeSkills.SaberCutting && Random.value < 0.5f)
            {
                var art = BladeArts.Of(t.Item);
                float r = BladeLength(t.Item, Equipment.ItemScale(t.Item)) * 0.5f;
                float rot = t.Spin * Mathf.Deg2Rad;
                var at = TerraParticles.ToPx(t.At) + Along(rot) * r * (Random.value * 2f - 1f);
                Mote(art.Theme, art, at, Along(rot + Pi / 2f) * 1.5f, 1f, false);
            }
            foreach (var w in BladeSkills.waves)
            {
                var art = BladeArts.Of(w.Item);
                var foot = TerraParticles.ToPx(new Vector2(w.X, w.Y));
                if (w.Kind == BladeSkills.WaveKind.Shock)
                {
                    for (int k = 0; k < 3; k++) Dust(foot + new Vector2(Random.Range(-8f, 8f), -2f), 1, new Vector2(w.Dir * Random.value * 2f, -2.5f - Random.value * 3f), 0, 0.9f + Random.value * 0.4f, false);
                    if (Random.value < 0.6f) { int m = Dust(foot + new Vector2(0f, -4f), 31, new Vector2(w.Dir * 0.5f, -0.6f), 120, 1.2f, true); FadeIn(m, 0.8f); }
                    Mote(art.Theme, art, foot + new Vector2(0f, -8f), new Vector2(w.Dir * 2f, -1.5f), w.Dir, true);
                }
                else
                {
                    float h = w.Height / TerraParticles.Px;
                    for (int k = 0; k < 7; k++)
                    {
                        var at = foot + new Vector2(Random.Range(-10f, 10f), -Random.value * h);
                        int m = Dust(at, 6, new Vector2(w.Dir * (1f + Random.value * 2.5f), -1.5f - Random.value * 2.5f), 100, 1.4f + Random.value * 0.9f, true);
                        FadeIn(m, 1.3f);
                    }
                    if (Random.value < 0.6f) Dust(foot + new Vector2(0f, -Random.value * h), 278, new Vector2(w.Dir * 2f, -2f), 0, 0.5f, false, new Color(1f, 0.7f, 0.3f));
                }
            }
            foreach (var p in BladeSkills.posts)
            {
                float age = p.Age - p.Delay;
                if (age < 0f) continue;
                var foot = TerraParticles.ToPx(p.Foot);
                float h = p.Height / TerraParticles.Px;
                // Along the post (its way out, px y down) and across it: up and sideways for one out of the ground.
                var u = PostWay(p);
                var x = new Vector2(-u.y, u.x);
                switch (p.Kind)
                {
                    case BladeSkills.PostKind.Fire:
                        if (age < BladeSkills.PostLife - 0.15f)
                            for (int k = 0; k < 6; k++)
                            {
                                float up = Random.value;
                                int m = Dust(foot + x * (Random.Range(-9f, 9f) * (1f - up * 0.6f)) + u * (h * up * Mathf.Clamp01(age / 0.08f)), 6,
                                    x * Random.Range(-0.6f, 0.6f) + u * (2.5f + Random.value * 3f), 100, 1.6f + Random.value, true);
                                FadeIn(m, 1.4f);
                            }
                        if (Random.value < 0.5f) Dust(foot + x * Random.Range(-6f, 6f) + u * (h * Random.value), 278, x * Random.Range(-1f, 1f) + u * 3f, 0, 0.45f, false, new Color(1f, 0.75f, 0.3f));
                        break;
                    case BladeSkills.PostKind.Ice:
                        if (age < 0.15f) { int m = Dust(foot + x * Random.Range(-14f, 14f) + u * 2f, 135, x * Random.Range(-1.2f, 1.2f) + u * 0.4f, 100, 1.1f, true); FadeIn(m, 0.9f); }
                        if (Random.value < 0.25f) Dust(foot + x * Random.Range(-10f, 10f) + u * (Random.value * h), 76, new Vector2(Random.Range(-0.4f, 0.4f), 0.3f), 100, 0.8f, false);
                        if (!p.Burst && age >= BladeSkills.PostLife - 0.12f)
                        {
                            p.Burst = true;
                            for (int k = 0; k < 18; k++)
                            {
                                var at = foot + x * Random.Range(-10f, 10f) + u * (Random.value * h);
                                int m = Dust(at, Random.value < 0.5f ? 135 : 278, x * Random.Range(-2.5f, 2.5f) + u * (1f + Random.value * 2f), 40, 0.8f, false, new Color(0.75f, 0.92f, 1f));
                                FadeIn(m, 0.6f);
                            }
                        }
                        break;
                    default:
                        if (Random.value < 0.35f)
                        {
                            int m = Dust(foot + x * Random.Range(-10f, 10f) + u * (h * Random.value), 278, new Vector2(Random.Range(-1.2f, 1.2f), 0.2f), 40, 0.8f, false,
                                Color.Lerp(new Color(0.35f, 0.7f, 0.2f), new Color(0.6f, 0.85f, 0.3f), Random.value));
                            FadeIn(m, 0.6f);
                        }
                        if (age < 0.1f) Dust(foot + x * Random.Range(-8f, 8f) + u * 2f, 0, x * Random.Range(-1.5f, 1.5f) + u * 1.5f, 0, 0.9f, false);
                        break;
                }
            }
            foreach (var f in BladeSkills.fields)
            {
                if (f.Kind == BladeSkills.FieldKind.Flies)
                    for (int k = 0; k < 3; k++)
                    {
                        var at = TerraParticles.ToPx(f.From + Random.insideUnitCircle * f.Radius);
                        int m = Dust(at, 278, Random.insideUnitCircle * 2.5f, 60, 0.6f, true, new Color(0.12f, 0.12f, 0.1f));
                        FadeIn(m, 0.8f);
                    }
                else
                {
                    float k0 = 1f - Mathf.Clamp01((f.Age - (f.Life - 0.5f)) / 0.5f);
                    for (int k = 0; k < 4; k++)
                    {
                        if (Random.value > k0) continue;
                        var at = TerraParticles.ToPx(Vector2.Lerp(f.From, f.To, Random.value) + new Vector2(0f, Random.Range(-0.7f, 0.2f)));
                        int m = Dust(at, 107, new Vector2(Random.Range(-0.3f, 0.3f), -0.4f - Random.value * 0.6f), 100, 0.9f + Random.value * 0.5f, true);
                        FadeIn(m, 1.1f);
                    }
                    if (Random.value < 0.35f * k0)
                        twinkles.Add((TerraParticles.ToPx(Vector2.Lerp(f.From, f.To, Random.value) + new Vector2(0f, Random.Range(-0.8f, 0.3f))), 0f, Random.value * Pi));
                }
            }
            foreach (var p in BladeSkills.portals)
            {
                var at = TerraParticles.ToPx(p.At);
                if (p.Smoke)
                {
                    if (p.Age < 0.02f)
                        for (int k = 0; k < 16; k++) { int m = Dust(at + Random.insideUnitCircle * 10f, 31, Random.insideUnitCircle * 2f, 100, 1.4f, true, new Color(0.25f, 0.2f, 0.25f)); FadeIn(m, 1f); }
                    continue;
                }
                float open = Mathf.Clamp01(p.Age / 0.12f) * (1f - Mathf.Clamp01((p.Age - 0.25f) / 0.2f));
                for (int k = 0; k < 5; k++)
                {
                    float a = Random.value * Pi * 2f;
                    var on = new Vector2(Mathf.Cos(a) * 14f, Mathf.Sin(a) * 26f) * open;
                    var tangent = new Vector2(-Mathf.Sin(a) * 14f, Mathf.Cos(a) * 26f).normalized;
                    int m = Dust(at + on, 226, tangent * 2.5f, 100, 0.7f, true);
                    FadeIn(m, 0.7f);
                }
            }
            var feet = FeetPx(hero);
            var head = feet + new Vector2(0f, -40f);
            if (BladeSkills.eaten > 0.9f)
                for (int k = 0; k < 8; k++) Dust(head + new Vector2(hero.cState.facingRight ? 6f : -6f, 0f), 278, new Vector2(Random.Range(-2f, 2f), -1f - Random.value * 2f), 40, 0.8f, false, new Color(0.85f, 0.45f, 0.4f));
            if (BladeSkills.bled > 0.9f)
                for (int k = 0; k < 12; k++) Dust(feet + new Vector2(Random.Range(-10f, 10f), -Random.value * 40f), 5, Random.insideUnitCircle * 3f, 0, 1.2f, false);
            if (BladeSkills.BloodRiteOn && Random.value < 0.3f)
                Dust(feet + new Vector2(Random.Range(-10f, 10f), -Random.value * 40f), 5, new Vector2(0f, -0.5f), 0, 0.8f, false);
            if (BladeSkills.parried > 0.9f)
            {
                bool holy = BladeSkills.GuardItem != null && BladeSkills.GuardItem.Type != ItemType.Cutlass;
                var at = ShoulderPx(feet, hero.cState.facingRight ? 1f : -1f) + new Vector2(hero.cState.facingRight ? 18f : -18f, -6f);
                for (int k = 0; k < 10; k++)
                {
                    int m = Dust(at, holy ? 57 : 278, Random.insideUnitCircle * 4f, 0, holy ? 1.1f : 0.6f, !holy, holy ? default : new Color(1f, 0.85f, 0.5f));
                    FadeIn(m, 0.5f);
                }
            }
            if (BladeSkills.coins > 0.9f)
            {
                var at = TerraParticles.ToPx(BladeSkills.coinsAt);
                for (int k = 0; k < 7; k++) Dust(at, 10, new Vector2(Random.Range(-2f, 2f), -2f - Random.value * 2f), 80, 1.1f, Random.value < 0.6f);
            }
        }

        void DrawSkills(FxBatch fx, HeroController hero, Vector2 feet)
        {
            float facing = hero.cState.facingRight ? 1f : -1f;
            if (BladeSkills.Charging)
            {
                var item = ItemDb.Get(BladeSkills.ChargeItem);
                if (BladeSkills.ChargeAimed) HeldGun(fx, item, feet, facing, BladeSkills.ChargeAngle(hero, facing), BladeSkills.Charge);
                else HeldBlade(fx, item, feet, facing, BladeSkills.ChargeSkill == BladeSkills.Skill.Draw ? DragAngle(item) : BladeSkills.ChargePose, BladeSkills.Charge, true);
            }
            // The Flint held charged over the head: its embers' motes off its outline.
            if (BladeArts.Enabled && Time.time - TerraProjectiles.FlintChargedAt < 0.1f)
                ChargedInHand(ItemDb.Get(ItemType.DeadCellsFlint), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.4f));
            // A ranged art's weapon out while its shots go (BladeSkillsRanged); a thrown one is flung from the bare hand.
            else if (BladeSkills.RangedShown(out var shown) && !RangedArts.Flung(shown)) HeldGun(fx, shown, feet, facing, BladeSkills.ShownAngle(hero, facing), BladeSkills.ShownGlow);
            DrawScars(fx);
            if (BladeSkills.Guarding) HeldBlade(fx, BladeSkills.GuardItem, feet, facing, 70f, BladeSkills.Parrying ? 0.6f : 0.2f, false);
            foreach (var s in BladeSkills.lines) DrawRun(fx, s);
            foreach (var m in BladeSkills.marks)
            {
                var proxy = new BladeSkills.Strike { Item = m.Item, Style = m.Style };
                RunColours(proxy, out var tint, out var light);
                fx.Sparkle(1f, TerraParticles.ToPx(m.At), new Color(1f, 1f, 1f, 0f), light, m.Age / 0.35f, 0f, 0.12f, 0.45f, 1f, m.Spin, new Vector2(2.4f, 2.4f), new Vector2(1.4f, 1.4f), true);
            }
            // What is thrown, turning, its glow round after it.
            var t = BladeSkills.thrown;
            if (t != null && !BladeSkills.SaberCutting)
            {
                var blade = FlatBlade(t.Item);
                if (blade != null)
                {
                    var art = BladeArts.Of(t.Item);
                    float scale = Equipment.ItemScale(t.Item) * (1f + 0.5f * (art.Reach - 1f));
                    float r = BladeLength(t.Item, scale) * 0.5f;
                    var centre = TerraParticles.ToPx(t.At);
                    float rot = t.Spin * Mathf.Deg2Rad;
                    var tint = BladeArts.Tint(art, art.Main, 0f);
                    for (int k = 3; k >= 1; k--)
                    {
                        float rk = rot - k * 0.38f;
                        fx.Draw(blade, centre - Along(rk) * r, Glow(tint, 0.3f / k), rk, new Vector2(scale, scale * 1.4f), overPlayer: true);
                    }
                    fx.Draw(blade, centre - Along(rot) * r, new Color(L, L, L, 1f), rot, new Vector2(scale, scale), overPlayer: true);
                    if (art.Kind == BladeArts.Kind.Light)
                    {
                        fx.Draw(blade, centre - Along(rot) * r, Glow(BladeArts.Tint(art, art.Light, 0.1f), 0.35f), rot, new Vector2(scale * 1.03f, scale * 1.35f), overPlayer: true);
                        Fx?.Light(centre, tint * 0.45f, 3.5f, true);
                    }
                }
            }
            // The shock's leading edge along the ground; the dragon fire's heat.
            foreach (var w in BladeSkills.waves)
            {
                var foot = TerraParticles.ToPx(new Vector2(w.X, w.Y));
                var art = BladeArts.Of(w.Item);
                if (w.Kind == BladeSkills.WaveKind.Shock)
                {
                    var tint = BladeArts.Tint(art, art.Main, 0f);
                    Streak(fx, foot + new Vector2(-w.Dir * 34f, -2f), foot + new Vector2(w.Dir * 6f, -2f), tint, 10f, 0.55f);
                    Streak(fx, foot + new Vector2(-w.Dir * 26f, -2f), foot + new Vector2(w.Dir * 4f, -2f), Color.white, 4f, 0.6f);
                }
                else
                {
                    float h = w.Height / TerraParticles.Px;
                    float flick = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 10f, w.X);
                    Streak(fx, foot, foot + new Vector2(0f, -h), new Color(1f, 0.45f, 0.1f), 26f, 0.35f * flick);
                    Fx?.Light(foot + new Vector2(0f, -h * 0.5f), new Color(1f, 0.5f, 0.15f) * 0.6f, 4f, true);
                }
            }
            foreach (var p in BladeSkills.posts) DrawPost(fx, p);
            foreach (var w in twinkles)
                fx.Sparkle(1f, w.At, new Color(1f, 1f, 1f, 0f), new Color(0.5f, 1f, 0.6f), w.Age / 0.5f, 0f, 0.2f, 0.5f, 1f, w.Spin, new Vector2(1.1f, 1.1f), Vector2.one, true);
            // The hooks' lines.
            var h2 = BladeSkills.hook;
            if (h2 != null)
            {
                var from = ShoulderPx(feet, facing);
                var to = TerraParticles.ToPx(h2.At);
                var d = to - from;
                float rot = Mathf.Atan2(d.y, d.sqrMagnitude < 1e-4f ? 1f : d.x);
                int segs = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 6f));
                for (int k = 0; k < segs; k++)
                {
                    var c = h2.Self ? (k % 2 == 0 ? new Color(0.45f, 0.15f, 0.3f) : new Color(0.6f, 0.25f, 0.4f)) : (k % 2 == 0 ? new Color(0.95f, 0.15f, 0.2f) : Color.white);
                    fx.Draw(Block, from + Along(rot) * (k * d.magnitude / segs), new Color(c.r, c.g, c.b, 1f), rot, new Vector2(d.magnitude / segs / 4f, 3f / 4f), overPlayer: true);
                }
            }
            foreach (var p in BladeSkills.portals)
            {
                if (p.Smoke) continue;
                float open = Mathf.Clamp01(p.Age / 0.12f) * (1f - Mathf.Clamp01((p.Age - 0.25f) / 0.2f));
                var at = TerraParticles.ToPx(p.At);
                fx.Sparkle(1f, at, new Color(1f, 1f, 1f, 0f), new Color(0.3f, 0.95f, 1f), open, 0f, 0.3f, 0.8f, 1.01f, 0f, new Vector2(2.6f, 3.6f), new Vector2(1.6f, 1.6f), true);
                Fx?.Light(at, new Color(0.2f, 0.9f, 1f) * 0.5f * open, 3f, true);
            }
        }

        /// <summary>One run's cut: the blade's afterimages, then a soft line of light opening along the way and shrinking to its middle.</summary>
        void DrawRun(FxBatch fx, BladeSkills.Strike s)
        {
            var art = BladeArts.Of(s.Item);
            var a = TerraParticles.ToPx(s.From);
            var b = TerraParticles.ToPx(s.To);
            RunColours(s, out var tint, out var light);
            float dash = BladeSkills.DashTime, open = dash + s.Delay;
            var blade = FlatBlade(s.Item);
            var d = b - a;
            float rot = Mathf.Atan2(d.y, d.sqrMagnitude < 1e-4f ? s.Facing : d.x);
            if (blade != null && s.Age < open + 0.12f)
            {
                float scale = Equipment.ItemScale(s.Item) * (1f + 0.5f * (art.Reach - 1f));
                float gone = Mathf.Clamp01(s.Age / dash);
                for (int k = 1; k <= 4; k++)
                {
                    float alpha = 0.4f * (1f - Mathf.Clamp01((s.Age - dash) / 0.12f)) * (0.35f + 0.15f * k);
                    if (alpha <= 0f) continue;
                    fx.Draw(blade, Vector2.Lerp(a, b, gone * k / 4f) + new Vector2(0f, -4f), Glow(tint, alpha), rot, new Vector2(scale, scale * 1.2f), flipY: s.Facing < 0f, overPlayer: true);
                }
            }
            if (s.Age < open)
            {
                Streak(fx, a, Vector2.Lerp(a, b, Mathf.Clamp01(s.Age / dash)), light, 3f, 0.3f);
                return;
            }
            float t = Mathf.Clamp01((s.Age - open) / 0.3f);
            if (t >= 1f) return;
            float k1 = 1f - t;
            float ease = k1 * k1;
            var dir = d.sqrMagnitude > 1f ? d.normalized : new Vector2(s.Facing, 0f);
            var a2 = a - dir * 30f;
            var b2 = b + dir * 30f;
            var mid = (a2 + b2) * 0.5f;
            var half = (b2 - a2) * 0.5f * (0.3f + 0.7f * k1);
            Streak(fx, mid - half, mid + half, tint, 20f + 12f * s.Charge, 0.5f * ease);
            Streak(fx, mid - half, mid + half, light, 9f, 0.85f * ease);
            Streak(fx, mid - half * 0.95f, mid + half * 0.95f, Color.white, 3.5f, ease);
        }

        /// <summary>A post out of the ground: fire's soft glow under its motes, the Blizzard's ice shards, the Vilethorn's vines.</summary>
        void DrawPost(FxBatch fx, BladeSkills.Post p)
        {
            float age = p.Age - p.Delay;
            if (age < 0f) return;
            var foot = TerraParticles.ToPx(p.Foot);
            float h = p.Height / TerraParticles.Px;
            float rise = Mathf.Clamp01(age / 0.09f);
            float end = Mathf.Clamp01((age - (BladeSkills.PostLife - 0.15f)) / 0.15f);
            float seed = Mathf.Repeat(p.Foot.x * 7.31f, 1f);
            // Its way out (px y down) and how far that is turned from straight up.
            var u = PostWay(p);
            float turn = Mathf.Atan2(u.y, u.x) + Pi / 2f;
            switch (p.Kind)
            {
                case BladeSkills.PostKind.Fire:
                {
                    float flick = 0.8f + 0.25f * Mathf.PerlinNoise(Time.time * 14f, seed * 10f);
                    float k = rise * (1f - end);
                    Streak(fx, foot, foot + u * (h * rise), new Color(1f, 0.4f, 0.08f), 24f, 0.45f * k * flick);
                    Streak(fx, foot, foot + u * (h * 0.75f * rise), new Color(1f, 0.85f, 0.4f), 10f, 0.5f * k * flick);
                    Fx?.Light(foot + u * (h * 0.5f), new Color(1f, 0.5f, 0.15f) * 0.55f * k, 3.5f, true);
                    break;
                }
                case BladeSkills.PostKind.Ice:
                {
                    // The Deerclops' own ice spikes (Projectile_961, five 200×48 frames, the cluster's root at the left), stood up
                    // out of the ground at half size (a texel to a picture pixel), shooting up to their length, leaning in turn.
                    int frame = Mathf.FloorToInt(seed * 5f) % 5;
                    var tex = FxBatch.Frame("Projectile_961", 0, 48 * frame, 200, 48, 0f, 24f);
                    if (tex == null) break;
                    float alpha = 1f - end;
                    float shoot = 1f - (1f - rise) * (1f - rise) * (1f - rise);
                    float tilt = (Mathf.Repeat(p.Foot.x, 2f) < 1f ? -1f : 1f) * (0.08f + 0.12f * seed);
                    float rot = tilt + turn - Pi / 2f;
                    var root = foot - u * 4f;
                    var sc = new Vector2(0.5f * Mathf.Lerp(0.15f, 1f, shoot) * (1f - 0.25f * end), 0.5f);
                    fx.Draw(tex, root, Glow(new Color(0.45f, 0.8f, 1f), 0.35f * alpha), rot, sc * 1.08f, overPlayer: true);
                    fx.Draw(tex, root, new Color(1f, 1f, 1f, alpha), rot, sc, overPlayer: true);
                    var tip = root + Turn(u, tilt) * 190f * sc.x;
                    if (age < 0.3f) fx.Sparkle(1f, tip, new Color(1f, 1f, 1f, 0f), new Color(0.6f, 0.9f, 1f), age / 0.3f, 0f, 0.3f, 0.6f, 1f, seed * Pi, new Vector2(1.4f, 1.4f), Vector2.one, true);
                    Fx?.Light(foot + u * (h * 0.5f), new Color(0.4f, 0.75f, 1f) * 0.4f * alpha, 3f, true);
                    break;
                }
                default:
                {
                    // One Vilethorn vine (Projectile_7, the tip Projectile_8) to a post, leaning in turn, growing out of the
                    // ground a piece at a time as the Vilethorn does, a jungle-green light over it, drawn back in at the end.
                    var baseTex = FxBatch.Whole("Projectile_7");
                    var tipTex = FxBatch.Whole("Projectile_8");
                    if (baseTex == null || tipTex == null) break;
                    float grow = rise * (1f - end);
                    float tilt = (Mathf.Repeat(p.Foot.x, 2f) < 1f ? -1f : 1f) * (0.12f + 0.16f * seed);
                    var up = Turn(u, tilt);
                    float seg = 28f;
                    int pieces = Mathf.Max(1, Mathf.RoundToInt(h / seg));
                    float shown = grow * pieces;
                    var start = foot - u * 4f;
                    for (int i = 0; i < pieces; i++)
                    {
                        float a = Mathf.Clamp01(shown - i);
                        if (a <= 0f) break;
                        var tex = i == pieces - 1 ? tipTex : baseTex;
                        var at = start + up * seg * (i + 0.5f);
                        // The Vilethorn's olive turned the Blade of Grass's jungle green: tinted, then green light added on its own pixels.
                        fx.Draw(tex, at, new Color(0.78f, 1f, 0.62f, a), tilt + turn, 1f, overPlayer: true);
                        fx.Draw(tex, at, Glow(new Color(0.3f, 0.8f, 0.12f), 0.55f * a), tilt + turn, 1f, overPlayer: true);
                    }
                    break;
                }
            }
        }

        /// <summary>A post's way out from its foot in Terraria px (y down): up for one out of the ground.</summary>
        static Vector2 PostWay(BladeSkills.Post p) => new Vector2(p.Way.x, -p.Way.y).normalized;

        /// <summary>A way (px y down) turned by an angle, clockwise on the screen for a positive one.</summary>
        static Vector2 Turn(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        /// <summary>The draw's charge: the katana hanging behind, its tip on the ground (degrees from level for a player facing right).</summary>
        static float DragAngle(ItemDef item)
        {
            if (item == null) return -150f;
            var art = BladeArts.Of(item);
            float len = 9f + BladeLength(item, Equipment.ItemScale(item) * (1f + 0.5f * (art.Reach - 1f)));
            return -180f + Mathf.Asin(Mathf.Clamp01(24f / Mathf.Max(1f, len))) * Mathf.Rad2Deg;
        }

        /// <summary>A scratch the dragged katana's tip left on the ground (Terraria px): bright as it is cut, darkening, gone in three seconds.</summary>
        sealed class Scar
        {
            public Vector2 A, B;
            public float Age;
        }

        readonly List<Scar> scars = new List<Scar>();
        Vector2? scarTip;
        float scrapedAt;

        /// <summary>Each tick of a draw's charge: the tip scoring the ground as the player moves, sparks off it; motes drawn in to the blade.</summary>
        void TickCharge(HeroController hero)
        {
            for (int i = scars.Count - 1; i >= 0; i--) if ((scars[i].Age += 1f / 60f) > 3f) scars.RemoveAt(i);
            if (!BladeSkills.Charging || BladeSkills.ChargeSkill != BladeSkills.Skill.Draw) { scarTip = null; return; }
            var item = ItemDb.Get(BladeSkills.ChargeItem);
            if (item == null) return;
            var art = BladeArts.Of(item);
            float facing = hero.cState.facingRight ? 1f : -1f;
            var feet = FeetPx(hero);
            float scale = Equipment.ItemScale(item) * (1f + 0.5f * (art.Reach - 1f));
            float screen = ScreenAngle(DragAngle(item), facing);
            var hand = ShoulderPx(feet, facing) + Along(screen) * 9f;
            var tip = hand + Along(screen) * BladeLength(item, scale);
            var ground = World.TerraWorld.IsSolidAt(TerraParticles.ToWorld(tip + new Vector2(0f, 6f)));
            if (ground)
            {
                if (scarTip.HasValue && (tip - scarTip.Value).sqrMagnitude > 9f)
                {
                    scars.Add(new Scar { A = scarTip.Value, B = tip });
                    if (Time.time - scrapedAt > 0.14f) { scrapedAt = Time.time; Sound("tk_iai_scrape", 0.5f); }
                    for (int k = 0; k < 2; k++)
                    {
                        int m = Dust(tip, 278, new Vector2(-facing * (1f + Random.value * 2f), -1f - Random.value * 2f), 0, 0.5f, false, new Color(1f, 0.8f, 0.45f));
                        FadeIn(m, 0.3f);
                    }
                    scarTip = tip;
                }
                else if (!scarTip.HasValue) scarTip = tip;
            }
            else scarTip = null;
            // Motes drawn in to the blade, more as the charge fills.
            float c = BladeSkills.Charge;
            if (Random.value < 0.35f + 0.6f * c)
            {
                var along = Vector2.Lerp(hand, tip, 0.3f + Random.value * 0.7f);
                var from = along + Random.insideUnitCircle.normalized * (34f + Random.value * 22f);
                int m;
                if (art.Theme == BladeArts.Theme.Spirit) m = Dust(from, 278, (along - from) / 14f, 60, 1f, true, Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(0.6f, 0.95f, 1f), Random.value));
                else m = Dust(from, 278, (along - from) / 14f, 60, 0.8f, true, Color.Lerp(new Color(1f, 0.72f, 0.82f), Color.white, Random.value * 0.4f));
                FadeIn(m, 0.9f);
            }
        }

        void DrawScars(FxBatch fx)
        {
            foreach (var s in scars)
            {
                float fresh = Mathf.Clamp01(1f - s.Age / 0.6f);
                float k = 1f - Mathf.Clamp01((s.Age - 2.2f) / 0.8f);
                var d = s.B - s.A;
                float rot = Mathf.Atan2(d.y, d.sqrMagnitude < 1e-4f ? 1f : d.x);
                fx.Draw(Block, s.A, new Color(0.12f, 0.1f, 0.12f, 0.7f * k), rot, new Vector2((d.magnitude + 1f) / 4f, 1.5f / 4f), overPlayer: true);
                if (fresh > 0f) Streak(fx, s.A, s.B, new Color(1f, 0.85f, 0.6f), 4f, 0.8f * fresh);
            }
        }

        /// <summary>A weapon held in the hand at a way (degrees from level) by SwordFx, brightening to glow (a chime's worth at full).</summary>
        void HeldBlade(FxBatch fx, ItemDef item, Vector2 feet, float facing, float deg, float glow, bool charge)
        {
            var blade = FlatBlade(item);
            if (item == null || blade == null) return;
            var art = BladeArts.Of(item);
            float scale = Equipment.ItemScale(item) * (1f + 0.5f * (art.Reach - 1f));
            float screen = ScreenAngle(deg, facing);
            var hand = ShoulderPx(feet, facing) + Along(screen) * 9f;
            fx.Draw(blade, hand, new Color(L, L, L, 1f), screen, new Vector2(scale, scale), flipY: facing < 0f, overPlayer: true);
            float g = charge ? (glow >= 1f ? 0.75f + 0.1f * Mathf.Sin(Time.time * 6f) : 0.15f + 0.45f * glow) : glow;
            fx.Draw(blade, hand, Glow(BladeArts.Tint(art, art.Light, 0f), g), screen, new Vector2(scale * 1.04f, scale * 1.3f), flipY: facing < 0f, overPlayer: true);
            if (charge && glow > 0f) Fx?.Light(hand + Along(screen) * BladeLength(item, scale) * 0.6f, BladeArts.Tint(art, art.Main, 0f) * (0.2f + 0.4f * glow), 2f + 2f * glow, true);
            // Full: its colours' motes off its outline (SwordFxCharge).
            if (charge && glow >= 1f) ChargedOutline(blade, item, hand, screen, new Vector2(scale, scale), facing < 0f, BladeArts.Tint(art, art.Main, 0f), BladeArts.Tint(art, art.Light, 0f));
        }

        static readonly System.Collections.Generic.Dictionary<ItemType, Sprite> heldGuns = new System.Collections.Generic.Dictionary<ItemType, Sprite>();

        /// <summary>
        /// A launcher held out at the cursor while its art charges (BladeSkills.ChargeAimed): its own picture as a gun is held,
        /// the grip at the hand and the muzzle on the aim, brightening as a charging blade does.
        /// </summary>
        void HeldGun(FxBatch fx, ItemDef item, Vector2 feet, float facing, float deg, float glow)
        {
            if (item == null) return;
            if (!heldGuns.TryGetValue(item.Type, out var gun))
            {
                var held = TerraMod.ItemSprite(item.Type);
                gun = held == null ? null : Sprite.Create(held.texture, held.rect, new Vector2(0f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                heldGuns[item.Type] = gun;
            }
            if (gun == null) return;
            var art = BladeArts.Of(item);
            // A ranged weapon in its own colours (RangedArts).
            bool ranged = RangedArts.Active(item);
            var light = ranged ? RangedArts.Of(item).B : BladeArts.Tint(art, art.Light, 0f);
            var main = ranged ? RangedArts.Of(item).A : BladeArts.Tint(art, art.Main, 0f);
            float scale = Equipment.ItemScale(item);
            float screen = ScreenAngle(deg, facing);
            var hand = ShoulderPx(feet, facing) + Along(screen) * 4f;
            fx.Draw(gun, hand, new Color(L, L, L, 1f), screen, new Vector2(scale, scale), flipY: facing < 0f, overPlayer: true);
            float g = glow >= 1f ? 0.75f + 0.1f * Mathf.Sin(Time.time * 6f) : 0.15f + 0.45f * glow;
            fx.Draw(gun, hand, Glow(light, g), screen, new Vector2(scale * 1.04f, scale * 1.2f), flipY: facing < 0f, overPlayer: true);
            if (glow > 0f) Fx?.Light(hand + Along(screen) * gun.rect.width * scale, main * (0.2f + 0.4f * glow), 2f + 2f * glow, true);
            if (glow >= 1f) ChargedOutline(gun, item, hand, screen, new Vector2(scale, scale), facing < 0f, main, light);
            DevHeldGunDeg = deg;
        }

        /// <summary>Dev: the way (degrees from level for the way he faces) a launcher was last drawn held out charging.</summary>
        public static float DevHeldGunDeg = float.NaN;
    }
}
