using System.Collections.Generic;
using System.IO;
using TerraKnight.Terraria.Xnb;
using UnityEngine;

namespace TerraKnight.Terraria
{
    /// <summary>
    /// The mod's own sounds ("tk_" names: the Vengeful Spirit's weapons'), made when first played from the player's own
    /// Terraria sounds, as Terraria makes a new weapon's: a few of its sounds layered, each resampled to a pitch, cut and
    /// faded, low-passed where it is bright, delayed. Terraria's are quiet and mellow (its item sounds: loudness about
    /// 0.02-0.08 RMS, peaks 0.15-0.5, the spectrum's centre 200-1000 Hz, hardly anything over 6 kHz), and so are these:
    /// measured beside them by scratchpad sfx/mix_sfx.py (the same table) before they went in. The little soul's pounce
    /// (tk_soul_dash_0..2) is not mixed: its own WAVs, embedded (Resources), the user's choice.
    /// </summary>
    internal static class SoundMix
    {
        const int Rate = 44100;

        /// <summary>One of Terraria's sounds in a mix: its pitch (as a speed), gain, delay, where it starts and how long it plays (seconds, 0 all), a low-pass (Hz, 0 none), a fade in.</summary>
        readonly struct Layer
        {
            public readonly string Src;
            public readonly float Pitch, Gain, Delay, Start, Cut, Lowpass, FadeIn;

            public Layer(string src, float pitch, float gain, float delay, float start, float cut, float lowpass, float fadeIn)
            {
                Src = src; Pitch = pitch; Gain = gain; Delay = delay; Start = start; Cut = cut; Lowpass = lowpass; FadeIn = fadeIn;
            }
        }

        static Layer L(string src, float pitch = 1f, float gain = 1f, float delay = 0f, float start = 0f, float cut = 0f, float lowpass = 0f, float fadeIn = 0f)
            => new Layer(src, pitch, gain, delay, start, cut, lowpass, fadeIn);

        static readonly Dictionary<string, Layer[]> recipes = Recipes();

        static Dictionary<string, Layer[]> Recipes()
        {
            var r = new Dictionary<string, Layer[]>();
            // The sword: Terraria's three swings, a touch lower, with the Death Sickle's ghostly whoosh under them.
            string[] swings = { "Item_1", "Item_18", "Item_19" };
            float[] swingPitch = { 0.94f, 0.97f, 0.92f };
            for (int i = 0; i < 3; i++)
                r["tk_soul_swing_" + i] = new[] { L(swings[i], swingPitch[i], 1.15f), L("Item_71", 1.12f + 0.04f * i, 0.32f, 0.02f, cut: 0.34f, lowpass: 5000f) };
            // A soul shot landing: a soft thud and a little of the ghost's cry. The spell's and the laser's: a lighter thud and the Zapinator's tail.
            for (int i = 0; i < 3; i++)
            {
                float p = 1.25f + 0.1f * i;
                r["tk_soul_hit_" + i] = new[] { L("Item_10", p, 0.45f, cut: 0.22f), L("NPC_Hit_54", p - 0.1f, 0.22f, cut: 0.24f, lowpass: 4000f) };
                float q = 1.45f + 0.1f * i;
                r["tk_spirit_hit_" + i] = new[] { L("Item_10", q, 0.4f, cut: 0.2f), L("Item_158", q - 0.35f, 0.3f, cut: 0.18f, lowpass: 4000f) };
            }
            // The wraiths: rising off an enemy as the Shadowflame Hex Doll's flame, striking as a ghost's cry and a thud.
            r["tk_wraith_strike"] = new[] { L("NPC_Hit_54", 1.3f, 0.3f, cut: 0.3f, lowpass: 4000f), L("Item_10", 1.6f, 0.25f, cut: 0.18f) };
            r["tk_wraith_rise"] = new[] { L("Item_103", 1.25f, 0.75f, cut: 0.62f), L("Item_24", 1.2f, 0.35f) };
            // A wave fading: the Spirit Flame's tail, lower. A soul burst: a small bomb (higher, quieter) under the hex doll's flame.
            r["tk_spirit_fade"] = new[] { L("Item_117", 0.8f, 0.55f, start: 0.12f, cut: 0.6f, fadeIn: 0.04f) };
            r["tk_soul_burst"] = new[] { L("Item_14", 1.35f, 0.5f), L("Item_103", 0.95f, 0.45f, cut: 0.7f) };
            // The rifle: the Space Gun's shot over a little of the Pulse Bow; the charge as the Last Prism's hum, three times
            // rising, the Deadly Sphere's swell under it and a sparkle as it fills; let go, the hum dropping away.
            for (int i = 0; i < 3; i++)
                r["tk_pulse_laser_" + i] = new[] { L("Item_157", 1.14f + 0.06f * i, 0.62f), L("Item_75", 1f, 0.25f, cut: 0.2f, lowpass: 5000f) };
            r["tk_pulse_charge"] = new[]
            {
                L("Item_15", 0.85f, 0.7f), L("Item_15", 1f, 0.7f, 0.66f), L("Item_15", 1.15f, 0.7f, 1.32f),
                L("Item_113", 0.9f, 0.35f, cut: 1.4f), L("Item_46", 1.2f, 0.3f, 1.45f),
            };
            r["tk_pulse_cancel"] = new[] { L("Item_15", 0.7f, 0.55f, cut: 0.28f), L("Item_158", 0.6f, 0.3f, cut: 0.22f) };
            r["tk_pulse_wave"] = new[]
            {
                L("Item_75", 0.8f, 0.55f, lowpass: 5000f), L("Item_72", 0.85f, 0.35f, lowpass: 5000f),
                L("Item_14", 1.6f, 0.32f), L("Item_103", 1f, 0.4f, 0.05f),
            };
            // The spell: the Spectre Staff's cast with the hex doll's flame; the staff: the summoning staves' sound with it.
            r["tk_spirit_cast"] = new[] { L("Item_43", 1f, 0.85f), L("Item_103", 1.3f, 0.38f, cut: 0.6f) };
            r["tk_soul_summon"] = new[] { L("Item_44", 0.9f, 0.8f), L("Item_103", 1.1f, 0.38f) };
            // The little soul gathering itself: the Spectre Boots' hover, higher.
            r["tk_soul_gather"] = new[] { L("Item_24", 1.3f, 0.7f) };

            // The Howling Wraiths' weapons. The bell: a low swing with a faint ring as it leaves; its toll at each shock (Terraria's
            // Bell, a fifth lower and more, a small blast under it, the hex doll's flame after); caught, a soft ring and the grab.
            r["tk_bell_throw"] = new[] { L("Item_1", 0.75f, 1.1f), L("Item_35", 0.55f, 0.22f, 0.03f, cut: 0.45f) };
            for (int i = 0; i < 3; i++)
                r["tk_bell_toll_" + i] = new[] { L("Item_35", 0.6f + 0.05f * i, 0.42f), L("Item_14", 1.7f, 0.24f, cut: 0.35f), L("Item_103", 1.2f, 0.2f, 0.02f, cut: 0.5f) };
            r["tk_bell_catch"] = new[] { L("Item_35", 1.1f, 0.18f, cut: 0.3f), L("Grab", 0.85f, 0.45f, lowpass: 4000f) };
            // The organ's roars: Terraria's scream (Roar_2) from its body, low-passed, over a boss roar's (Roar_0) and the hex doll's
            // flame; the third lower and longer with a blast; the charge as the Last Prism's hum twice rising with a sparkle.
            r["tk_organ_roar_0"] = new[] { L("Roar_2", 1.05f, 0.33f, start: 0.3f, cut: 0.42f, lowpass: 3000f), L("Roar_0", 1.6f, 0.26f, start: 0.8f, cut: 0.38f, lowpass: 3000f), L("Item_103", 1.3f, 0.3f, cut: 0.4f) };
            r["tk_organ_roar_1"] = new[] { L("Roar_2", 0.95f, 0.38f, start: 0.3f, cut: 0.5f, lowpass: 3000f), L("Roar_0", 1.45f, 0.3f, start: 0.8f, cut: 0.45f, lowpass: 3000f), L("Item_103", 1.15f, 0.34f, cut: 0.45f) };
            r["tk_organ_roar_2"] = new[] { L("Roar_2", 0.8f, 0.42f, start: 0.25f, cut: 0.9f, lowpass: 3000f), L("Roar_0", 1.25f, 0.38f, start: 0.8f, cut: 0.75f, lowpass: 3000f), L("Item_14", 1.2f, 0.24f) };
            r["tk_organ_charge"] = new[] { L("Item_15", 0.95f, 0.6f, cut: 0.4f), L("Item_15", 1.25f, 0.45f, 0.12f, cut: 0.3f), L("Item_46", 1.3f, 0.22f, 0.15f, cut: 0.3f) };
            // The wraiths: the hex doll's flame and the Deadly Sphere's swell as they appear; their shriek (the scream over the
            // Spectre Staff); a beam as the Spirit Flame's shot, quiet.
            r["tk_wraiths_appear"] = new[] { L("Item_103", 0.85f, 0.6f, cut: 0.7f), L("Item_113", 1.1f, 0.3f, cut: 0.6f) };
            r["tk_wraiths_shriek"] = new[] { L("Roar_2", 0.9f, 0.36f, start: 0.3f, cut: 0.75f, lowpass: 3000f), L("Roar_0", 1.4f, 0.24f, start: 0.8f, cut: 0.6f, lowpass: 3000f), L("Item_43", 0.8f, 0.4f) };
            r["tk_wraith_beam"] = new[] { L("Item_117", 1.5f, 0.22f, cut: 0.25f) };
            // The choir soul: summoned with the staves' sound and the hex doll's; its shout the scream, higher, over the hum.
            r["tk_choir_summon"] = new[] { L("Item_44", 1.1f, 0.75f), L("Item_103", 0.95f, 0.32f) };
            r["tk_choir_shout"] = new[] { L("Roar_2", 1.15f, 0.22f, start: 0.3f, cut: 0.9f, lowpass: 3000f), L("Item_15", 1.2f, 0.35f, cut: 0.9f) };

            // The Shade Soul's weapons, darker than the white ones'. The sword: Terraria's swings lower with the Death Sickle's
            // whoosh, lower still and dulled; its throw the sickle's alone, its spin a soft whoosh, a blow the thud and a ghost's
            // cry, low; caught, the grab.
            for (int i = 0; i < 3; i++)
                r["tk_shade_swing_" + i] = new[] { L(swings[i], 0.82f + 0.03f * i, 1.1f, lowpass: 4500f), L("Item_71", 0.88f + 0.04f * i, 0.36f, 0.02f, cut: 0.36f, lowpass: 4000f) };
            r["tk_shade_throw"] = new[] { L("Item_71", 0.78f, 0.7f, lowpass: 4000f), L("Item_1", 0.7f, 0.5f) };
            r["tk_shade_spin"] = new[] { L("Item_7", 0.8f, 2.2f, lowpass: 3500f) };
            for (int i = 0; i < 3; i++)
                r["tk_shade_hit_" + i] = new[] { L("Item_10", 0.95f + 0.08f * i, 0.45f, cut: 0.22f), L("NPC_Hit_54", 0.8f + 0.06f * i, 0.22f, cut: 0.26f, lowpass: 3500f) };
            r["tk_shade_catch"] = new[] { L("Grab", 0.7f, 0.7f, lowpass: 2500f), L("Item_103", 1.2f, 0.22f, cut: 0.25f) };
            // The rifle: the Shadowbeam Staff's beam over a little of the Space Gun, both low; the marks bursting, a bomb and the
            // hex doll's flame under the beam, low.
            for (int i = 0; i < 3; i++)
                r["tk_rift_ray_" + i] = new[] { L("Item_72", 0.95f + 0.05f * i, 0.55f, lowpass: 4500f), L("Item_157", 0.8f + 0.04f * i, 0.3f, cut: 0.25f, lowpass: 4000f) };
            r["tk_rift_burst"] = new[] { L("Item_14", 0.85f, 0.44f), L("Item_103", 0.75f, 0.36f, cut: 0.7f), L("Item_72", 0.7f, 0.2f, lowpass: 3500f) };
            // The staff: the hex doll's flame low with Terraria's spell sound (Item_8) as the ghost goes; its shocks the Spirit
            // Flame's shot, low and quiet; coming back, the Spectre Boots' hover, low; the blink the spell sound lower, with the flame.
            r["tk_shade_cast"] = new[] { L("Item_103", 0.8f, 0.6f, cut: 0.7f), L("Item_8", 0.75f, 0.3f, lowpass: 4000f) };
            r["tk_shade_shock"] = new[] { L("Item_117", 0.85f, 0.3f, cut: 0.25f, lowpass: 3500f) };
            r["tk_shade_return"] = new[] { L("Item_24", 0.8f, 0.6f) };
            r["tk_shade_blink"] = new[] { L("Item_8", 0.7f, 0.55f, lowpass: 3000f), L("Item_103", 1.0f, 0.4f, cut: 0.5f) };
            // The blink's wait: the Last Prism's hum low under the flame, swelling.
            r["tk_shade_blink_charge"] = new[] { L("Item_15", 0.75f, 0.55f, cut: 0.45f, lowpass: 3500f), L("Item_103", 0.7f, 0.32f, cut: 0.45f, fadeIn: 0.1f) };
            // The little shade: the summoning staves' sound with the flame, low; its charge the Death Sickle's whoosh over the
            // boots' hover; turning into the wave, the beam's low hum and a small blast.
            r["tk_shade_summon"] = new[] { L("Item_44", 0.8f, 0.8f), L("Item_103", 0.8f, 0.4f) };
            r["tk_shade_dash"] = new[] { L("Item_71", 1.05f, 0.45f, lowpass: 4000f), L("Item_24", 0.9f, 0.3f, cut: 0.3f) };
            r["tk_shade_wave"] = new[] { L("Item_72", 0.8f, 0.5f, lowpass: 4000f), L("Item_14", 1.4f, 0.25f) };

            // The Abyss Shriek's weapons. The yoyo: its ripple a low wail (the scream, low-passed) under the hex doll's flame; its
            // rend the Death Sickle's whoosh, quick and high, over a ghost's cry.
            r["tk_abyss_ripple"] = new[] { L("Item_103", 0.7f, 0.45f, cut: 0.5f), L("Roar_2", 0.75f, 0.16f, start: 0.3f, cut: 0.35f, lowpass: 2500f) };
            r["tk_abyss_rend"] = new[] { L("Item_71", 1.25f, 0.5f, cut: 0.25f, lowpass: 4000f), L("NPC_Hit_54", 0.7f, 0.25f, cut: 0.2f, lowpass: 3000f) };
            // The sniper: Terraria's Sniper Rifle low with the Shadowbeam Staff's tail; a hit the thud and the cry, low; the aim
            // a soft hum; a mark a low bell; the strike's launch the flame and a small blast; each bomb's burst a bomb low.
            for (int i = 0; i < 3; i++)
                r["tk_void_snipe_" + i] = new[] { L("Item_40", 0.82f + 0.04f * i, 0.75f, lowpass: 4500f), L("Item_72", 0.7f + 0.03f * i, 0.28f, cut: 0.3f, lowpass: 4000f) };
            r["tk_void_hit"] = new[] { L("Item_10", 0.85f, 0.45f, cut: 0.22f), L("NPC_Hit_54", 0.65f, 0.25f, cut: 0.26f, lowpass: 3000f) };
            r["tk_void_aim"] = new[] { L("Item_15", 1.1f, 0.3f, cut: 0.22f, lowpass: 3500f) };
            r["tk_void_mark"] = new[] { L("Item_35", 0.5f, 0.6f, cut: 0.45f, lowpass: 2500f), L("Item_103", 1.1f, 0.26f, cut: 0.3f, lowpass: 3500f) };
            r["tk_void_missile"] = new[] { L("Item_103", 1.3f, 0.45f, cut: 0.45f), L("Item_14", 1.8f, 0.2f) };
            r["tk_void_blast"] = new[] { L("Item_14", 0.9f, 0.45f), L("Item_103", 0.7f, 0.25f, cut: 0.5f) };
            // The staff: the flame and the Spectre Staff's cast, low; its bursts the scream lower each time over a small blast;
            // the threads the Spirit Flame's shot, low.
            r["tk_abyss_cast"] = new[] { L("Item_103", 0.7f, 0.55f, cut: 0.6f), L("Item_43", 0.7f, 0.35f) };
            for (int i = 0; i < 3; i++)
                r["tk_abyss_pulse_" + i] = new[] { L("Roar_2", 0.7f - 0.06f * i, 0.3f, start: 0.3f, cut: 0.35f, lowpass: 2200f), L("Item_14", 1.1f - 0.1f * i, 0.26f) };
            r["tk_abyss_threads"] = new[] { L("Item_117", 0.9f, 0.35f, cut: 0.4f, lowpass: 3500f) };
            // The mask: the summoning staves' sound low with the flame; the laser the Last Prism's hum under the scream, low.
            r["tk_dirge_summon"] = new[] { L("Item_44", 0.75f, 0.8f), L("Item_103", 0.7f, 0.4f) };
            r["tk_dirge_laser"] = new[] { L("Item_15", 0.8f, 0.5f, cut: 0.6f, lowpass: 4000f), L("Roar_2", 1.2f, 0.15f, start: 0.3f, cut: 0.5f, lowpass: 3000f) };
            Casino(r);
            // The remade blades' charges (Items.BladeSkills, the user's 10-26 "蓄力还要有音效"): the phase blade's hum three
            // times rising over the Deadly Sphere's swell, as the pulse rifle's charge but steel-bright for the katana's draw,
            // low for the greatswords' leap, with a star's chime for the star swords'; the dragged tip's scrape a dig's scratch
            // and a faint tink, high and short.
            r["tk_iai_charge"] = new[]
            {
                L("Item_15", 0.8f, 0.5f), L("Item_15", 0.95f, 0.5f, 0.33f), L("Item_15", 1.1f, 0.5f, 0.66f),
                L("Item_113", 1f, 0.28f, cut: 1f, fadeIn: 0.25f), L("Item_35", 1.7f, 0.12f, 0.9f, cut: 0.25f),
            };
            r["tk_heavy_charge"] = new[]
            {
                L("Item_113", 0.7f, 0.45f, cut: 1.3f, fadeIn: 0.2f, lowpass: 3000f), L("Item_15", 0.55f, 0.45f), L("Item_15", 0.65f, 0.45f, 0.4f), L("Item_15", 0.75f, 0.45f, 0.8f),
            };
            r["tk_blade_charge"] = new[]
            {
                L("Item_15", 0.75f, 0.45f), L("Item_15", 0.9f, 0.45f, 0.3f), L("Item_15", 1.05f, 0.45f, 0.6f), L("Item_113", 0.9f, 0.25f, cut: 0.9f, fadeIn: 0.2f),
            };
            r["tk_star_charge"] = new[]
            {
                L("Item_15", 1f, 0.4f), L("Item_15", 1.15f, 0.4f, 0.37f), L("Item_15", 1.3f, 0.4f, 0.74f), L("Item_9", 1.4f, 0.22f, 0.5f), L("Item_25", 1.2f, 0.2f, 1f, cut: 0.4f),
            };
            // A bow drawn: its string's creak (the bow's own twang, low and cut short) over the charge's rising tones, softer;
            // a gun's charge: the hammer's clicks (the gunshot pitched high and cut to its first crack), the tones under them.
            r["tk_bow_draw"] = new[]
            {
                L("Item_5", 0.45f, 0.3f, cut: 0.3f, lowpass: 2500f), L("Item_5", 0.5f, 0.2f, 0.35f, cut: 0.25f, lowpass: 2500f),
                L("Item_15", 0.7f, 0.3f, 0.1f), L("Item_15", 0.85f, 0.3f, 0.5f),
            };
            r["tk_gun_charge"] = new[]
            {
                L("Item_11", 2.2f, 0.3f, cut: 0.04f), L("Item_11", 2.5f, 0.3f, 0.2f, cut: 0.04f),
                L("Item_15", 0.8f, 0.35f, 0.1f), L("Item_15", 0.95f, 0.35f, 0.5f),
            };
            r["tk_iai_scrape"] = new[] { L("Dig_1", 1.7f, 0.35f, cut: 0.12f, lowpass: 5000f), L("Tink_0", 1.5f, 0.12f, cut: 0.1f) };
            return r;
        }

        /// <summary>A note n semitones from the sample's own pitch, as a speed.</summary>
        static float Semi(float n) => Mathf.Pow(2f, n / 12f);

        /// <summary>
        /// The casino's sounds, all of Terraria's own: the Magical Harp (Item_26) and the Bell (Item_35) as instruments
        /// (arpeggios, chords, a fanfare), the coins (Coin_0-4) as a payout's cascade, the wire switch (Mech_0), the doors,
        /// the tinks and the digs as the machines' parts.
        /// </summary>
        static void Casino(Dictionary<string, Layer[]> r)
        {
            Layer[] Arp(string src, float gain, float step, float at, float lowpass, params float[] semis)
            {
                var a = new Layer[semis.Length];
                for (int i = 0; i < semis.Length; i++) a[i] = L(src, Semi(semis[i]), gain, at + step * i, lowpass: lowpass);
                return a;
            }
            Layer[] Chord(string src, float gain, float at, params float[] semis)
            {
                var a = new Layer[semis.Length];
                for (int i = 0; i < semis.Length; i++) a[i] = L(src, Semi(semis[i]), gain, at);
                return a;
            }
            Layer[] Coins(int count, float from, float every, float gain)
            {
                var a = new Layer[count];
                for (int i = 0; i < count; i++) a[i] = L("Coin_" + (i * 3 % 5), 0.95f + 0.04f * (i % 4), gain, from + every * i);
                return a;
            }
            Layer[] Ticks(int count, float from, float first, float last, float pitch, float gain)
            {
                var a = new Layer[count];
                float at = from;
                for (int i = 0; i < count; i++)
                {
                    a[i] = L("Tink_" + i % 3, pitch + 0.05f * (i % 3), gain, at, cut: 0.08f);
                    at += Mathf.Lerp(first, last, i / (float)Mathf.Max(1, count - 1));
                }
                return a;
            }
            Layer[] Join(params Layer[][] parts)
            {
                var list = new List<Layer>();
                foreach (var p in parts) list.AddRange(p);
                return list.ToArray();
            }

            // Opening each machine: its own little tune.
            r["tk_casino_open_slots"] = Join(new[] { L("Coin_1", 1f, 0.45f) }, Arp("Item_26", 0.5f, 0.07f, 0.02f, 0f, 0, 4, 7, 12), new[] { L("Item_35", Semi(12), 0.32f, 0.3f) });
            r["tk_casino_open_wheel"] = Join(new[] { L("Item_35", 0.75f, 0.42f) }, Arp("Item_26", 0.45f, 0.11f, 0.12f, 0f, 7, 12, 16), new[] { L("Item_4", 1.2f, 0.28f, 0.22f) });
            r["tk_casino_open_pinball"] = Join(new[] { L("Mech_0", 1f, 0.55f), L("Door_Opened", 1.8f, 0.22f, cut: 0.2f) }, Arp("Item_26", 0.42f, 0.05f, 0.08f, 0f, 0, 7, 12, 19));
            // The slot: the lever down (a clank, the spring), back up; the reels rolling (ticks slowing); each reel's stop.
            r["tk_casino_lever_pull"] = new[] { L("Mech_0", 0.7f, 0.8f), L("Door_Closed", 1.6f, 0.35f, 0.05f, cut: 0.25f), L("Grab", 0.6f, 0.4f) };
            r["tk_casino_lever_back"] = new[] { L("Door_Opened", 2f, 0.25f, cut: 0.2f), L("Mech_0", 1.2f, 0.45f, 0.06f) };
            r["tk_casino_spin"] = Ticks(34, 0f, 0.045f, 0.075f, 1.6f, 0.13f);
            for (int i = 0; i < 3; i++)
                r["tk_casino_reel_" + i] = new[] { L("Mech_0", 0.85f - 0.05f * i, 0.7f), L("Dig_1", 1.4f, 0.32f, cut: 0.15f), L("Tink_" + i, 1.2f, 0.22f) };
            r["tk_casino_bet"] = new[] { L("Coin_4", 1.1f, 0.5f), L("Coin_2", 0.9f, 0.25f, 0.05f) };
            r["tk_casino_button"] = new[] { L("Mech_0", 1.6f, 0.35f) };
            r["tk_casino_collect"] = Join(new[] { L("Coins", 1f, 0.6f) }, Coins(8, 0.05f, 0.06f, 0.35f));
            // Results, louder as they get bigger. A small win: two coins and a little rise. A win: the harp up two octaves,
            // a bell chord over it, the mana chime, coins pouring. A jackpot: a fanfare of bell chords (I, IV, V, I) over a
            // harp run and the Life Crystal's rise, coins pouring for two seconds, a sparkle at the top. Nothing back: the harp
            // going down in a sad little fall, muffled, a dull thud at the end.
            r["tk_casino_win_small"] = Join(Coins(2, 0f, 0.08f, 0.6f), Arp("Item_26", 0.5f, 0.06f, 0.02f, 0f, 2, 6, 9));
            r["tk_casino_win_big"] = Join(Arp("Item_26", 0.48f, 0.06f, 0f, 0f, 0, 4, 7, 12, 16, 19, 24), Chord("Item_35", 0.28f, 0.45f, 0, 4, 7),
                new[] { L("MaxMana", 1f, 0.5f, 0.4f) }, Coins(12, 0.1f, 0.09f, 0.42f));
            r["tk_casino_jackpot"] = Join(Chord("Item_35", 0.26f, 0f, 0, 4, 7), Chord("Item_35", 0.26f, 0.35f, 5, 9, 12), Chord("Item_35", 0.26f, 0.7f, 7, 11, 14),
                Chord("Item_35", 0.3f, 1.05f, 12, 16, 19), Arp("Item_26", 0.36f, 0.045f, 0f, 0f, 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19, 21, 23, 24),
                new[] { L("Item_29", 1f, 0.45f, 0.1f), L("Item_4", 1.1f, 0.55f, 1.05f) }, Coins(30, 0.15f, 0.07f, 0.38f));
            r["tk_casino_lose"] = Join(Arp("Item_26", 0.55f, 0.24f, 0f, 2500f, -5, -6, -7), new[] { L("Item_26", Semi(-9), 0.6f, 0.72f, lowpass: 2000f), L("Dig_0", 0.7f, 0.45f, 0.78f) });
            // The wheel: a whoosh as it is thrown, the frets ticking ever slower; the ball dropping into its pocket.
            r["tk_casino_wheel_spin"] = Join(new[] { L("Item_7", 0.6f, 0.22f, cut: 0.8f) }, Ticks(40, 0.05f, 0.04f, 0.16f, 1.8f, 0.12f));
            r["tk_casino_ball_land"] = new[] { L("Tink_1", 1.4f, 0.4f), L("Tink_2", 1.6f, 0.25f, 0.07f), L("Tink_0", 1.8f, 0.15f, 0.13f) };
            r["tk_casino_chip"] = new[] { L("Coin_3", 0.8f, 0.6f), L("Coin_1", 0.9f, 0.3f, 0.04f) };
            // The pinball: the plunger drawn back, let go (a spring and a whoosh); the pins; each channel's score.
            r["tk_casino_plunger"] = new[] { L("Door_Opened", 1.3f, 0.3f, cut: 0.3f), L("Mech_0", 0.6f, 0.4f) };
            r["tk_casino_launch"] = new[] { L("Mech_0", 1.1f, 0.55f), L("Item_1", 1.4f, 0.3f, cut: 0.2f), L("Door_Closed", 2f, 0.18f, cut: 0.15f) };
            for (int i = 0; i < 3; i++) r["tk_casino_pin_" + i] = new[] { L("Tink_" + i, 1.7f + 0.15f * i, 0.18f, cut: 0.1f) };
            r["tk_casino_score_lo"] = new[] { L("Tink_0", 0.9f, 0.35f), L("Dig_1", 1.2f, 0.2f, cut: 0.12f) };
            r["tk_casino_score_mid"] = new[] { L("Coin_0", 1f, 0.5f), L("Item_26", Semi(7), 0.3f, 0.03f) };
            r["tk_casino_score_hi"] = Join(Coins(3, 0f, 0.06f, 0.5f), Arp("Item_26", 0.42f, 0.05f, 0.03f, 0f, 0, 4, 7, 12));
            r["tk_casino_score_top"] = Join(Chord("Item_35", 0.3f, 0f, 0, 4, 7, 12), Coins(10, 0.05f, 0.06f, 0.4f), new[] { L("Item_4", 1.2f, 0.5f, 0.1f) });
        }

        public static bool Has(string name) => recipes.ContainsKey(name);

        /// <summary>The mix named, as one clip (mono, 44.1 kHz).</summary>
        public static AudioClip Build(string name)
        {
            var data = Mix(name);
            var clip = AudioClip.Create("TerraKnight/" + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Mix(string name)
        {
            if (!recipes.TryGetValue(name, out var layers)) throw new FileNotFoundException("mod sound " + name);
            var parts = new List<(int at, float[] y)>();
            int length = 1;
            foreach (var l in layers)
            {
                var y = Render(l);
                int at = Mathf.RoundToInt(l.Delay * Rate);
                parts.Add((at, y));
                length = Mathf.Max(length, at + y.Length);
            }
            var outp = new float[length];
            foreach (var (at, y) in parts)
                for (int i = 0; i < y.Length; i++) outp[at + i] += y[i];
            return outp;
        }

        static readonly Dictionary<string, XnbAudio> sources = new Dictionary<string, XnbAudio>();

        /// <summary>A layer: its source (mono) read at its pitch from its start, cut, faded at the ends, low-passed twice (12 dB an octave).</summary>
        static float[] Render(Layer l)
        {
            if (!sources.TryGetValue(l.Src, out var a))
                sources[l.Src] = a = XnbSound.Load(Path.Combine(TerrariaAssets.SoundsDir, l.Src + ".xnb"));
            int ch = Mathf.Max(1, a.Channels), frames = a.Samples.Length / ch;
            double step = l.Pitch * a.SampleRate / Rate, first = l.Start * a.SampleRate;
            int n = Mathf.Max(0, (int)((frames - 1 - first) / step));
            if (l.Cut > 0f) n = Mathf.Min(n, (int)(l.Cut * Rate));
            var y = new float[n];
            for (int i = 0; i < n; i++)
            {
                double pos = first + i * step;
                int j = (int)pos;
                float f = (float)(pos - j);
                int k = Mathf.Min(j + 1, frames - 1);
                float s0 = 0f, s1 = 0f;
                for (int c = 0; c < ch; c++)
                {
                    s0 += a.Samples[j * ch + c];
                    s1 += a.Samples[k * ch + c];
                }
                y[i] = (s0 + (s1 - s0) * f) / ch * l.Gain;
            }
            if (l.Lowpass > 0f)
            {
                float k = 1f - Mathf.Exp(-2f * Mathf.PI * l.Lowpass / Rate);
                for (int pass = 0; pass < 2; pass++)
                {
                    float acc = 0f;
                    for (int i = 0; i < n; i++) y[i] = acc += k * (y[i] - acc);
                }
            }
            int fadeIn = Mathf.Min(n, (int)(l.FadeIn * Rate));
            for (int i = 0; i < fadeIn; i++) y[i] *= i / (float)fadeIn;
            int fadeOut = Mathf.Min(n, (int)((l.Cut > 0f ? 0.04f : 0.01f) * Rate));
            for (int i = 0; i < fadeOut; i++) y[n - 1 - i] *= i / (float)fadeOut;
            return y;
        }

        /// <summary>Dev: every mix written as a 16-bit WAV into a folder (to measure beside Terraria's).</summary>
        public static int DevDump(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var name in recipes.Keys)
            {
                var x = Mix(name);
                using (var w = new BinaryWriter(File.Create(Path.Combine(dir, name + ".wav"))))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    w.Write(36 + x.Length * 2);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    w.Write(x.Length * 2);
                    foreach (var s in x) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
                }
            }
            return recipes.Count;
        }
    }
}
