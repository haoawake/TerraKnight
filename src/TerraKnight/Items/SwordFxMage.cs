using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The magic weapons' casts drawn (MageArts; nothing here hits): as each use begins a flash and a ring of its motes at the
    /// weapon's head (TerraPlayerSprite.HeldTip for one held out); through the use magic drawn in to a staff's head, runes
    /// round a book and a circle opening before it, sparks off a gun's muzzle, notes off a harp or guitar, motes round what is
    /// held, a column rising from a sky staff; a wand drawn here, swept round in its flourish, its tip trailing light.
    /// </summary>
    internal sealed partial class SwordFx
    {
        ItemDef mageItem;
        float mageAt, mageDur, mageFlashAt = -1f;
        bool mageBurst;
        readonly List<(Vector2 Pos, float At)> wandTrail = new List<(Vector2, float)>();

        /// <summary>Dev: casts begun, their bursts made, the wand flourishes drawn.</summary>
        public static int DevMageCasts, DevMageBursts, DevWandDraws;

        /// <summary>A magic weapon's use begins (TerraMod.BeginUse).</summary>
        public void MageBegin(ItemDef def)
        {
            if (!MageArts.Active(def)) return;
            mageItem = def;
            mageAt = Time.time;
            mageDur = Mathf.Max(0.1f, def.UseAnimation * Equipment.UseScale(def));
            mageBurst = false;
            wandTrail.Clear();
            DevMageCasts++;
            MageCircles(def);
        }

        /// <summary>The cast under way (its weapon still the one in use), and how far through (by the clock).</summary>
        bool MageNow(out ItemDef def, out float p)
        {
            def = null;
            p = 0f;
            var mod = TerraMod.Instance;
            if (mageItem == null || mod == null || !mod.IsUsingItem || mod.SwingItem != mageItem || !MageArts.Active(mageItem)) return false;
            def = mageItem;
            p = Mathf.Clamp01((Time.time - mageAt) / mageDur);
            return true;
        }

        /// <summary>The aim's way from the shoulder (degrees from level for the way he faces, + up).</summary>
        static float AimDeg(HeroController hero, float facing)
        {
            var mod = TerraMod.Instance;
            var aim = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint);
            var d = aim - ShoulderPx(FeetPx(hero), facing);
            return Mathf.Atan2(-d.y, Mathf.Max(0.01f, d.x * facing)) * Mathf.Rad2Deg;
        }

        /// <summary>A wand's way now through its flourish, for the arm (TerraMod) and the drawing.</summary>
        public bool MageWandAngle(out float deg)
        {
            deg = 0f;
            var hero = HeroController.SilentInstance;
            if (hero == null || !MageNow(out var def, out float p) || MageArts.Of(def).Kind != MageArts.Kind.Wand) return false;
            float facing = TerraMod.Instance.UseDirection;
            deg = MageArts.WandAngle(p, AimDeg(hero, facing), def.UseAnimation * 60f <= 14f);
            return true;
        }

        /// <summary>The cast's head (Terraria px): the wand's tip, the held-out weapon's far end, else the hand.</summary>
        bool MageTip(HeroController hero, ItemDef def, MageArts.Look look, float facing, out Vector2 tip, out Vector2 way)
        {
            var feet = FeetPx(hero);
            float aimDeg = AimDeg(hero, facing);
            way = Along(ScreenAngle(aimDeg, facing));
            if (look.Kind == MageArts.Kind.Wand)
            {
                if (!MageWandAngle(out float deg)) { tip = default; return false; }
                float screen = ScreenAngle(deg, facing);
                tip = ShoulderPx(feet, facing) + Along(screen) * (9f + BladeLength(def, Equipment.ItemScale(def)));
                return true;
            }
            var held = TerraMod.Instance.PlayerLook != null ? TerraMod.Instance.PlayerLook.HeldTip : null;
            if (held.HasValue && look.Kind != MageArts.Kind.Hand && look.Kind != MageArts.Kind.Raise)
            {
                tip = TerraParticles.ToPx(held.Value);
                return true;
            }
            tip = ShoulderPx(feet, facing) + way * 12f;
            return true;
        }

        void TickMage(HeroController hero)
        {
            TickCircles(hero);
            for (int i = wandTrail.Count - 1; i >= 0; i--) if (Time.time - wandTrail[i].At > 0.14f) wandTrail.RemoveAt(i);
            if (!MageNow(out var def, out float p)) { mageItem = null; return; }
            var look = MageArts.Of(def);
            float facing = TerraMod.Instance.UseDirection;
            if (!MageTip(hero, def, look, facing, out var tip, out var way)) return;
            var side = new Vector2(-way.y, way.x);
            Color Tint() => Color.Lerp(look.A, look.B, Random.value * 0.6f);
            int Mote(Vector2 at, Vector2 vel, float scale, bool tinted = true)
            {
                int m = Dust(at, look.Dust, vel, look.Dust == 278 ? 40 : 90, scale, true, look.Dust == 278 || tinted ? Tint() : default);
                FadeIn(m, scale * 0.8f);
                return m;
            }
            if (!mageBurst)
            {
                mageBurst = true;
                mageFlashAt = Time.time;
                DevMageBursts++;
                // From the second grandeur on, a rune opens at the head as it casts.
                int g = MageArts.Grand(def);
                if (g >= 2) OpenCircle(look, tip + way * 4f, 6f + 2f * g, 1f, 0.28f, 6, false, 4f);
                // The burst: a ring of its motes at the head, more by kind.
                for (int k = 0; k < 12; k++)
                {
                    float a = k / 12f * Mathf.PI * 2f;
                    Mote(tip, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.4f, 2.6f) + way * 0.8f, Random.Range(0.6f, 0.9f));
                }
                switch (look.Kind)
                {
                    case MageArts.Kind.Tome:
                        // A circle opening before the book, turning as it widens.
                        var centre = tip + way * 10f;
                        for (int k = 0; k < 18; k++)
                        {
                            float a = k / 18f * Mathf.PI * 2f;
                            var r = side * Mathf.Cos(a) * 14f + new Vector2(0f, Mathf.Sin(a) * 14f);
                            var tangent = new Vector2(-r.y, r.x).normalized;
                            Mote(centre + r, tangent * 1.6f + r.normalized * 0.6f, 0.7f);
                        }
                        break;
                    case MageArts.Kind.Gun:
                        for (int k = 0; k < 8; k++) Mote(tip, way * Random.Range(2f, 4.5f) + side * Random.Range(-1.2f, 1.2f), Random.Range(0.5f, 0.85f));
                        break;
                    case MageArts.Kind.SkyStaff:
                        for (int k = 0; k < 10; k++) Mote(tip + new Vector2(Random.Range(-6f, 6f), 0f), new Vector2(Random.Range(-0.6f, 0.6f), -2.5f - Random.value * 3f), Random.Range(0.6f, 1f));
                        break;
                    case MageArts.Kind.Raise:
                        var ground = FeetPx(hero);
                        for (int k = 0; k < 14; k++) Mote(ground + new Vector2(facing * Random.Range(4f, 40f), -2f), new Vector2(Random.Range(-0.5f, 0.5f), -1.5f - Random.value * 2.5f), Random.Range(0.6f, 1f));
                        break;
                }
            }
            bool quick = def.UseAnimation * 60f <= 14f;
            switch (look.Kind)
            {
                case MageArts.Kind.Staff:
                case MageArts.Kind.SkyStaff:
                    // Magic drawn in to the head, turning as it comes.
                    if (Random.value < (quick ? 0.4f : 0.65f))
                    {
                        var off = Random.insideUnitCircle.normalized * Random.Range(16f, 26f);
                        Mote(tip + off, -off * 0.11f + new Vector2(-off.y, off.x) * 0.05f, Random.Range(0.45f, 0.75f));
                    }
                    if (Random.value < 0.2f) Mote(tip, way * 0.6f + Random.insideUnitCircle * 0.5f, 0.5f);
                    break;
                case MageArts.Kind.Tome:
                    // Runes round the book.
                    if (Random.value < 0.45f)
                    {
                        float a = Time.time * 5f + Random.value * Mathf.PI * 2f;
                        var r = new Vector2(Mathf.Cos(a) * 13f, Mathf.Sin(a) * 9f);
                        Mote(tip - way * 6f + r, new Vector2(-r.y, r.x).normalized * 1.1f, 0.55f);
                    }
                    break;
                case MageArts.Kind.Gun:
                    if (Random.value < (MageArts.Continuous(def) ? 0.7f : 0.25f)) Mote(tip, way * Random.Range(0.8f, 2f) + side * Random.Range(-0.5f, 0.5f), 0.5f);
                    break;
                case MageArts.Kind.Harp:
                case MageArts.Kind.Guitar:
                    // Notes and stars off the strings, rising.
                    if (Random.value < 0.3f) Mote(tip - way * 8f + Random.insideUnitCircle * 6f, new Vector2(Random.Range(-0.6f, 0.6f), -1.1f - Random.value), Random.Range(0.6f, 0.9f));
                    break;
                case MageArts.Kind.Held:
                    if (Random.value < 0.45f)
                    {
                        float a = Time.time * 4f + Random.value * Mathf.PI * 2f;
                        var r = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12f;
                        Mote(tip - way * 4f + r, new Vector2(-r.y, r.x).normalized * 0.9f + new Vector2(0f, -0.3f), 0.55f);
                    }
                    break;
                case MageArts.Kind.Wand:
                    // The tip's trail: motes along its way since the last tick, the ribbon in the drawing.
                    wandTrail.Add((tip, Time.time));
                    if (wandTrail.Count >= 2)
                    {
                        var from = wandTrail[wandTrail.Count - 2].Pos;
                        int n = Mathf.Clamp(Mathf.CeilToInt((tip - from).magnitude / 7f), 1, 5);
                        for (int k = 0; k < n; k++) Mote(Vector2.Lerp(from, tip, (k + 1f) / n), side * Random.Range(-0.6f, 0.6f) + Random.insideUnitCircle * 0.3f, Random.Range(0.5f, 0.8f));
                    }
                    break;
            }
            Fx?.Light(tip, look.A * 0.5f, 3f, true);
        }

        void DrawMage(FxBatch fx, HeroController hero, Vector2 feet)
        {
            DrawCircles(fx, hero);
            if (!MageNow(out var def, out float p)) return;
            var look = MageArts.Of(def);
            float facing = TerraMod.Instance.UseDirection;
            if (look.Kind == MageArts.Kind.Wand && MageWandAngle(out float deg))
            {
                // The wand in the hand along its flourish, a glow of its colour on it.
                var blade = FlatBlade(def);
                if (blade != null)
                {
                    float scale = Equipment.ItemScale(def);
                    float screen = ScreenAngle(deg, facing);
                    var hand = ShoulderPx(feet, facing) + Along(screen) * 9f;
                    fx.Draw(blade, hand, new Color(L, L, L, 1f), screen, new Vector2(scale, scale), flipY: facing < 0f, overPlayer: true);
                    fx.Draw(blade, hand, Glow(look.A, 0.22f), screen, new Vector2(scale * 1.04f, scale * 1.3f), flipY: facing < 0f, overPlayer: true);
                    DevWandDraws++;
                }
                // The ribbon after the tip.
                float now = Time.time;
                for (int k = 1; k < wandTrail.Count; k++)
                {
                    float fresh = 1f - (now - wandTrail[k].At) / 0.14f;
                    Streak(fx, wandTrail[k - 1].Pos, wandTrail[k].Pos, Color.Lerp(look.A, look.B, 0.3f), 4f + 6f * fresh, 0.55f * fresh);
                }
            }
            if (!MageTip(hero, def, look, facing, out var tip, out _)) return;
            // A short flash in its own colours as it casts (none for the quick-firing ones: no flicker of stars, the user's 10-26
            // "莫名其妙的不必要的闪光"), then a faint light breathing at the head of a staff, book or held thing while it lasts.
            float ticks = def.UseAnimation * 60f;
            if (ticks > 10f && mageFlashAt >= 0f && Time.time - mageFlashAt < 0.14f)
            {
                var core = Color.Lerp(look.B, Color.white, 0.3f) * 0.7f;
                core.a = 0f;
                float size = ticks <= 16f ? 0.8f : 1.2f;
                fx.Sparkle(1f, tip, core, look.A, (Time.time - mageFlashAt) / 0.14f, 0f, 0.2f, 0.45f, 1f, 0.3f, new Vector2(size, size), new Vector2(1f, 1f), true);
            }
            if (look.Kind == MageArts.Kind.Staff || look.Kind == MageArts.Kind.SkyStaff || look.Kind == MageArts.Kind.Tome || look.Kind == MageArts.Kind.Held)
            {
                float breathe = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                var core = look.B * 0.35f;
                core.a = 0f;
                fx.Sparkle(0.6f, tip, core, look.A, 0.5f, 0f, 0.1f, 0.9f, 1f, Time.time * 1.5f, new Vector2(0.45f, 0.45f) * breathe, new Vector2(0.7f, 0.7f), true);
            }
        }
    }
}
