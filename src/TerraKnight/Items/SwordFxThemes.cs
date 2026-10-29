using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade swings' themes (BladeArts.Theme), each from what the weapon is: what its cut sheds along the blade every
    /// frame it cuts, what flies off an enemy it strikes, and how its trail is coloured and shaped. Terraria's own dusts where
    /// it has one (fire 6, the ice torch 135, shadowflame 27, blood 5, the magic stars 15, 57, 58, the electric arc 226, the
    /// Terra Blade's 107, water 33, sand 32, snow 76, wood 7, bone 26, smoke 31, cloud 16, the gems 86-91, the rainbow torch
    /// 66, the chlorophyte's 157), else its tinted mote (278) in the theme's colours. Nothing flashes.
    /// </summary>
    internal sealed partial class SwordFx
    {
        /// <summary>One frame's motes off a cutting blade: along it from halfway out to the tip, flung the way it swings.</summary>
        void ThemeMotes(ArtCut a, Vector2 shoulder, float screen, float r, Vector2 tang, int level)
        {
            var theme = a.Art.Theme;
            float rate = Rate(theme) * (0.7f + 0.12f * level);
            for (; rate > 0f; rate -= 1f)
            {
                if (rate < 1f && Random.value > rate) break;
                var at = shoulder + Along(screen) * r * (0.55f + Random.value * 0.45f);
                Mote(theme, a.Art, at, tang, a.Facing, false);
            }
        }

        /// <summary>A hit's burst: the theme's motes thrown the way of the blow.</summary>
        void ThemeHit(BladeArts.Art art, Vector2 at, float facing)
        {
            int n = 4 + art.Level;
            for (int k = 0; k < n; k++)
            {
                var push = new Vector2(facing * (1f + Random.value * 3.5f), (Random.value - 0.65f) * 4.5f);
                Mote(art.Theme, art, at, push * 0.6f, facing, true);
            }
        }

        /// <summary>A remade spear's mote off its head (TerraProjectilesSpearArts), carried by its push.</summary>
        internal static void SpearMote(BladeArts.Art art, Vector2 at, Vector2 push, float facing) => Mote(art.Theme, art, at, push, facing, false);

        /// <summary>A remade spear's blow landing: its theme's burst, thrown the way it struck.</summary>
        internal static void SpearHit(BladeArts.Art art, Vector2 at, float facing)
        {
            int n = 4 + art.Level;
            for (int k = 0; k < n; k++)
            {
                var push = new Vector2(facing * (1f + Random.value * 3.5f), (Random.value - 0.65f) * 4.5f);
                Mote(art.Theme, art, at, push * 0.6f, facing, true);
            }
        }

        static float Rate(BladeArts.Theme t)
        {
            switch (t)
            {
                case BladeArts.Theme.Fire: case BladeArts.Theme.Electric: case BladeArts.Theme.Terra: case BladeArts.Theme.Rainbow:
                case BladeArts.Theme.Spirit: case BladeArts.Theme.Spore: case BladeArts.Theme.Water: case BladeArts.Theme.Star: return 1.1f;
                case BladeArts.Theme.Ice: case BladeArts.Theme.Shadow: case BladeArts.Theme.Dye: case BladeArts.Theme.Sakura: case BladeArts.Theme.Petal:
                case BladeArts.Theme.Festive: case BladeArts.Theme.Sand: case BladeArts.Theme.Ichor: case BladeArts.Theme.Honey: case BladeArts.Theme.Saber: return 0.8f;
                case BladeArts.Theme.Steel: case BladeArts.Theme.Wood: case BladeArts.Theme.Bone: return 0.3f;
                default: return 0.55f;
            }
        }

        static Color Hsv(float h, float s = 0.85f, float v = 1f) => Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);

        /// <summary>The Christmas tree sword's lights.</summary>
        static readonly Color[] Lights = { new Color(1f, 0.2f, 0.2f), new Color(0.2f, 0.9f, 0.3f), new Color(1f, 0.85f, 0.2f), new Color(0.35f, 0.6f, 1f) };

        /// <summary>One mote of a theme at a point (Terraria px), carried by the blade's push.</summary>
        static void Mote(BladeArts.Theme theme, BladeArts.Art art, Vector2 at, Vector2 push, float facing, bool hit)
        {
            var main = BladeArts.Tint(art, art.Main, Random.value);
            var light = BladeArts.Tint(art, art.Light, Random.value);
            float sway = (Random.value - 0.5f) * 1.6f;
            int m;
            switch (theme)
            {
                case BladeArts.Theme.Fire:
                    m = Dust(at, 6, push * 0.6f + new Vector2(0f, -1.2f - Random.value), 100, 1.2f + Random.value * 0.6f, true);
                    FadeIn(m, 1.1f);
                    if (Random.value < 0.35f) Dust(at, 278, push * 0.9f + new Vector2(0f, -0.5f), 100, 0.45f, false, new Color(1f, 0.6f + Random.value * 0.3f, 0.2f));
                    break;
                case BladeArts.Theme.Ice:
                    m = Dust(at, 135, push * 0.5f, 100, 1.1f + Random.value * 0.4f, true);
                    FadeIn(m, 1f);
                    if (Random.value < 0.3f) Dust(at, 76, new Vector2(sway, 0.4f), 100, 0.9f, false);
                    break;
                case BladeArts.Theme.Snow:
                    Dust(at, 76, new Vector2(sway * 0.8f, 0.2f) + push * 0.2f, 100, 0.8f + Random.value * 0.4f, false);
                    break;
                case BladeArts.Theme.Leaf:
                    m = Dust(at, 278, new Vector2(sway * 1.5f, -0.6f) + push * 0.3f, 100, 0.7f + Random.value * 0.3f, false, Color.Lerp(new Color(0.35f, 0.75f, 0.2f), new Color(0.6f, 0.9f, 0.3f), Random.value));
                    FadeIn(m, 0.6f);
                    break;
                case BladeArts.Theme.Shadow:
                    m = Dust(at, 27, push * 0.4f + new Vector2(0f, -0.4f), 100, 1.1f + Random.value * 0.5f, true);
                    FadeIn(m, 1.2f);
                    if (Random.value < 0.35f) { m = Dust(at, 31, push * 0.2f, 140, 1f, true, new Color(0.25f, 0.12f, 0.35f)); FadeIn(m, 0.8f); }
                    break;
                case BladeArts.Theme.Holy:
                    m = Dust(at, Random.value < 0.5f ? 57 : 278, push * 0.3f + new Vector2(0f, -0.6f), 100, 0.9f, true, new Color(1f, 0.95f, 0.7f));
                    FadeIn(m, 0.8f);
                    break;
                case BladeArts.Theme.Star:
                    m = Dust(at, Random.value < 0.5f ? 15 : (art.Type == ItemType.StarWrath ? 58 : 57), push * 0.4f, 100, 1f + Random.value * 0.4f, true);
                    FadeIn(m, 1f);
                    break;
                case BladeArts.Theme.Blood:
                    Dust(at, 5, push * 0.6f + new Vector2(0f, -0.5f), 0, 1f + Random.value * 0.4f, false);
                    break;
                case BladeArts.Theme.Honey:
                    m = Dust(at, 278, push * 0.25f + new Vector2(0f, 0.3f), 60, 0.8f + Random.value * 0.3f, false, new Color(1f, 0.72f, 0.15f));
                    FadeIn(m, 0.7f);
                    break;
                case BladeArts.Theme.Ichor:
                    m = Dust(at, 278, push * 0.5f + new Vector2(0f, -0.4f), 60, 0.8f + Random.value * 0.3f, false, new Color(1f, 0.88f, 0.25f));
                    FadeIn(m, 0.6f);
                    break;
                case BladeArts.Theme.Fly:
                    m = Dust(at, 278, new Vector2(sway * 3f, (Random.value - 0.5f) * 3f), 80, 0.5f, true, new Color(0.15f, 0.15f, 0.12f));
                    FadeIn(m, 0.9f);
                    break;
                case BladeArts.Theme.Candy:
                    m = Dust(at, 278, push * 0.4f + new Vector2(0f, -0.3f), 60, 0.8f, false, Random.value < 0.5f ? new Color(0.95f, 0.15f, 0.2f) : Color.white);
                    FadeIn(m, 0.6f);
                    break;
                case BladeArts.Theme.Coin:
                    m = Dust(at, 278, push * 0.5f + new Vector2(0f, -1.5f - Random.value), 0, 0.9f, false, new Color(1f, 0.82f, 0.25f));
                    FadeIn(m, 0.5f);
                    if (Random.value < 0.4f) Dust(at, 43, push * 0.1f, 100, 0.6f, true, new Color(1f, 0.9f, 0.4f));
                    break;
                case BladeArts.Theme.Dye:
                    m = Dust(at, 278, push * 0.6f + new Vector2(0f, -0.6f), 40, 0.9f + Random.value * 0.3f, false, Hsv(Random.value));
                    FadeIn(m, 0.6f);
                    break;
                case BladeArts.Theme.Saber:
                    m = Dust(at, Gem(art.Type), push * 0.3f, 100, 0.9f + Random.value * 0.3f, true);
                    FadeIn(m, 0.9f);
                    break;
                case BladeArts.Theme.Electric:
                    m = Dust(at, 226, push * 0.5f + Random.insideUnitCircle * 1.5f, 100, 0.7f + Random.value * 0.3f, true);
                    FadeIn(m, 0.8f);
                    break;
                case BladeArts.Theme.Terra:
                    m = Dust(at, 107, push * 0.4f, 100, 1f + Random.value * 0.3f, true);
                    FadeIn(m, 1f);
                    break;
                case BladeArts.Theme.Rainbow:
                    m = Dust(at, 66, push * 0.4f, 0, 1.1f, true, Hsv(Time.time * 0.6f + Random.value * 0.3f));
                    FadeIn(m, 1f);
                    break;
                case BladeArts.Theme.Sand:
                    Dust(at, 32, push * 0.5f, 60, 0.9f + Random.value * 0.3f, false);
                    break;
                case BladeArts.Theme.Water:
                    Dust(at, 33, push * 0.6f + new Vector2(0f, -1f), 0, 1f + Random.value * 0.3f, false);
                    break;
                case BladeArts.Theme.Bone:
                    Dust(at, 26, push * 0.5f, 0, 0.9f, false);
                    break;
                case BladeArts.Theme.Festive:
                {
                    m = Dust(at, 278, push * 0.3f, 60, 0.8f, true, Lights[Random.Range(0, Lights.Length)]);
                    FadeIn(m, 0.9f);
                    if (Random.value < 0.3f) Dust(at, 76, new Vector2(sway, 0.3f), 100, 0.8f, false);
                    break;
                }
                case BladeArts.Theme.Spore:
                    m = Dust(at, 157, push * 0.15f + new Vector2(sway * 0.3f, -0.2f), 100, 1f + Random.value * 0.4f, true);
                    FadeIn(m, 1.3f);
                    break;
                case BladeArts.Theme.Sakura:
                    m = Dust(at, 278, new Vector2(sway * 1.8f, 0.2f) + push * 0.35f, 60, 0.8f + Random.value * 0.3f, false, Color.Lerp(new Color(1f, 0.72f, 0.82f), Color.white, Random.value * 0.4f));
                    FadeIn(m, 0.7f);
                    break;
                case BladeArts.Theme.Spirit:
                    m = Dust(at, 278, push * 0.3f + new Vector2(0f, -1.4f - Random.value), 60, 1f + Random.value * 0.4f, true, Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(0.6f, 0.95f, 1f), Random.value));
                    FadeIn(m, 1.1f);
                    break;
                case BladeArts.Theme.Hay:
                    Dust(at, 278, push * 0.4f + new Vector2(sway, -0.3f), 40, 0.7f, false, new Color(0.95f, 0.85f, 0.4f));
                    break;
                case BladeArts.Theme.Heart:
                    m = Dust(at, 278, new Vector2(sway * 0.6f, -1f), 40, 0.9f, true, Random.value < 0.5f ? new Color(1f, 0.35f, 0.5f) : new Color(1f, 0.6f, 0.75f));
                    FadeIn(m, 0.9f);
                    break;
                case BladeArts.Theme.Feather:
                    m = Dust(at, 278, new Vector2(sway * 1.4f, 0.15f) + push * 0.3f, 40, 0.9f, false, new Color(0.95f, 0.93f, 0.85f));
                    FadeIn(m, 0.8f);
                    break;
                case BladeArts.Theme.Petal:
                    m = Dust(at, 278, new Vector2(sway * 1.8f, 0.2f) + push * 0.35f, 60, 0.85f, false, Color.Lerp(new Color(1f, 0.4f, 0.75f), new Color(1f, 0.7f, 0.9f), Random.value));
                    FadeIn(m, 0.7f);
                    break;
                case BladeArts.Theme.Wind:
                    m = Dust(at, 16, push * 0.8f, 120, 0.9f + Random.value * 0.4f, true);
                    FadeIn(m, 0.7f);
                    break;
                case BladeArts.Theme.Rot:
                    Dust(at, 278, push * 0.4f + new Vector2(0f, 0.2f), 80, 0.7f, false, new Color(0.35f, 0.45f, 0.2f));
                    break;
                case BladeArts.Theme.Wood:
                    Dust(at, 7, push * 0.6f, 0, 0.9f, false);
                    break;
                default:
                    // Steel: a spark struck off the edge, falling.
                    m = Dust(at, 278, push * (hit ? 1.2f : 1.6f) + new Vector2(0f, -1f), 0, 0.5f, false, Color.Lerp(new Color(1f, 0.85f, 0.55f), main, 0.3f));
                    FadeIn(m, 0.4f);
                    break;
            }
        }

        /// <summary>A phase blade's gem dust (the amethyst, topaz, sapphire, emerald, ruby, diamond and amber ones; the rainbow torch's).</summary>
        static int Gem(ItemType t)
        {
            switch (t)
            {
                case ItemType.PurplePhaseblade: case ItemType.PurplePhasesaber: return 86;
                case ItemType.YellowPhaseblade: case ItemType.YellowPhasesaber: return 87;
                case ItemType.BluePhaseblade: case ItemType.BluePhasesaber: return 88;
                case ItemType.GreenPhaseblade: case ItemType.GreenPhasesaber: return 89;
                case ItemType.RedPhaseblade: case ItemType.RedPhasesaber: case ItemType.PinkPhaseblade: case ItemType.PinkPhasesaber: return 90;
                case ItemType.OrangePhaseblade: case ItemType.OrangePhasesaber: return 87;
                case ItemType.RainbowPhaseblade: case ItemType.RainbowPhasesaber: return 66;
                default: return 91;
            }
        }

        /// <summary>
        /// A trail piece's colour and width by the theme (k: the sample, newest 0; f: how new, 0..1): a candy cane's red and
        /// white bands, a fire's flicker, a phase blade's white core, the rainbow along the trail, an arc's jitter.
        /// </summary>
        static Color ThemeTrail(ArtCut a, int k, float f, Color c, ref float width, ref float inner)
        {
            switch (a.Art.Theme)
            {
                case BladeArts.Theme.Candy:
                    return (k / 2) % 2 == 0 ? new Color(0.95f, 0.18f, 0.22f) : new Color(1f, 0.96f, 0.96f);
                case BladeArts.Theme.Fire:
                    width *= 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, k * 0.37f);
                    return Color.Lerp(new Color(0.85f, 0.2f, 0.05f), new Color(1f, 0.85f, 0.3f), f * f);
                case BladeArts.Theme.Rainbow:
                    return Hsv(Time.time * 0.5f + k * 0.045f, 0.75f);
                case BladeArts.Theme.Electric:
                    inner *= 0.9f + 0.2f * Random.value;
                    return Color.Lerp(c, new Color(0.6f, 1f, 1f), Random.value * 0.5f);
                case BladeArts.Theme.Ice:
                    return Color.Lerp(new Color(0.55f, 0.85f, 1f), Color.white, f * 0.6f);
                case BladeArts.Theme.Spirit:
                    return Color.Lerp(new Color(0.25f, 0.5f, 1f), new Color(0.7f, 0.95f, 1f), f);
                case BladeArts.Theme.Sakura:
                    return Color.Lerp(c, new Color(1f, 0.75f, 0.85f), 0.35f);
                default:
                    return c;
            }
        }
    }
}
