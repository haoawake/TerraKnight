using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Accessory layers drawn through the effect batch, as the wings are: the Portable Stool under
    /// the feet (DrawPlayer_03_PortableStool), the Floating Tube round the waist (its back half
    /// behind the player, its front half over), the Shimmer Cloak's glitter strips and the
    /// Magiluminescence glow.
    /// </summary>
    internal static class AccessoryLayers
    {
        const int FrameW = 40, FrameH = 56;

        public static void Draw(FxBatch fx, in TerraPlayerSprite.Equips equips, in Items.Bonuses b, Vector2 frameCentrePx, Vector2 feetPx, int dir, float opacity,
            int bodyFrame, float bobPx)
        {
            if (fx == null || opacity <= 0f) return;
            bool flip = dir < 0;
            // Extra_102 standing on the feet, its bottom 2 px below them (PDL:246-258).
            if (TerraMovement.StoolActive)
                fx.Draw(FxBatch.Frame("Extra_102", 0, 0, 28, 28, 14f, 28f), feetPx + new Vector2(0f, 2f), Color.white * opacity, 0f, 1f, flipX: flip);
            // Extra_105's two 40x56 halves at the torso: the back one before the body, the front one after it (PDL:1273-1279, 2037-2043).
            if (b.FloatInLiquid)
            {
                var at = frameCentrePx + new Vector2(0f, bobPx);
                fx.Draw(FxBatch.Frame("Extra_105", 0, 0, FrameW, FrameH, 20f, 28f), at, Color.white * opacity, 0f, 1f, flipX: flip);
                fx.Draw(FxBatch.Frame("Extra_105", 0, FrameH, FrameW, FrameH, 20f, 28f), at, Color.white * opacity, 0f, 1f, flipX: flip, overPlayer: true);
            }
            if (!equips.HideCapes)
            {
                if (equips.Back == 36) Glitter(fx, "Glow_332", frameCentrePx, bodyFrame, flip, opacity, false);
                if (equips.Front == 12) Glitter(fx, "Glow_331", frameCentrePx, bodyFrame, flip, opacity, true);
            }
            // Glow_310 after the head (PDL:2673-2687), strongest in the dark: lerp((140,140,35,12), clear, light).
            // Hallownest is mostly dark, so most of it shows.
            if (equips.Neck == 11)
            {
                const float dark = 0.7f;
                var c = new Color(140f / 255f, 140f / 255f, 35f / 255f, 12f / 255f) * dark * opacity;
                fx.Draw(FxBatch.Frame("Glow_310", 0, bodyFrame * FrameH, FrameW, FrameH, 20f, 28f), frameCentrePx, c, 0f, 1f, flipX: flip, overPlayer: true);
            }
            if (equips.Shield > 0 && Items.MeleeSpecials.ShieldRaised) RaisedShield(fx, equips.Shield, frameCentrePx, bodyFrame, flip, opacity);
        }

        /// <summary>
        /// DrawPlayer_25_Shield raised (PDL:3060-3125): 4 px up, four added copies wobbling round it
        /// (2.5 ± 1.5 px, once a second), and while the parry is open one swelling away from the
        /// player's centre to 2.5 times, fading as it goes.
        /// </summary>
        static void RaisedShield(FxBatch fx, int sheet, Vector2 frameCentrePx, int bodyFrame, bool flip, float opacity)
        {
            var spr = FxBatch.Frame("Acc_Shield_" + sheet, 0, bodyFrame * FrameH, FrameW, FrameH, 20f, 28f);
            var at = frameCentrePx + new Vector2(0f, -4f);
            float sn = Mathf.Sin(Time.time * Mathf.PI * 2f);
            float r = 2.5f + 1.5f * sn;
            var glow = new Color(1f, 1f, 1f, 0f) * (0.45f - sn * 0.15f) * opacity;
            for (int k = 0; k < 4; k++)
            {
                float a = k / 4f * Mathf.PI * 2f;
                fx.Draw(spr, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, glow, 0f, 1f, flipX: flip, overPlayer: true);
            }
            int parry = Items.MeleeSpecials.ParryLeft;
            if (parry <= 0) return;
            float t = parry / 20f, grow = 1.5f * t;
            // The hitbox centre is 3 px below the frame's.
            var away = at - (frameCentrePx + new Vector2(0f, 3f));
            fx.Draw(spr, at + away * grow, Color.white * (1f - t) * opacity, 0f, 1f + grow, flipX: flip, overPlayer: true);
        }

        /// <summary>
        /// The Shimmer Cloak's glowmasks in twenty 2 px strips, each its own hue running round with
        /// time: GetShimmerGlitterColor(top: true, i / 16, 0) = hsl((i / 16 + t / 30) / 6, 1, 0.5)
        /// at half strength, added (PDL:630-648, 3926-3989).
        /// </summary>
        static void Glitter(FxBatch fx, string tex, Vector2 at, int frame, bool flip, float opacity, bool over)
        {
            float t = Time.time * 60f;
            for (int i = 0; i < FrameW / 2; i++)
            {
                var c = FxParticles.HslToRgb((i / 16f + t / 30f) / 6f % 1f, 1f, 0.5f) * 0.5f * opacity;
                c.a = 0f;
                fx.Draw(FxBatch.Frame(tex, i * 2, frame * FrameH, 2, FrameH, 20f - i * 2, 28f), at, c, 0f, 1f, flipX: flip, overPlayer: over);
            }
        }
    }
}
