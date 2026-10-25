using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// A character's look, as Terraria's character creation makes it (Player.hair, skinVariant and the seven colours):
    /// chosen when a save is created (UI.CharacterCreation), kept in the save (Save.ModSave), drawn by
    /// <see cref="TerraPlayerSprite"/> and shown to the other players in a LAN game. A save from before has Terraria's
    /// default character.
    /// </summary>
    [System.Serializable]
    internal sealed class Appearance
    {
        /// <summary>Player.hair: the hair style, Player_Hair_(Hair + 1).</summary>
        public int Hair;
        /// <summary>Player.skinVariant: the clothing style, Player_(Variant)_N.</summary>
        public int Variant;
        /// <summary>The colours as 0xRRGGBB.</summary>
        public int HairColor = 0xD75A37, SkinColor = 0xFF7D5A, EyeColor = 0x695A4B, ShirtColor = 0xAFA58C,
            UnderShirtColor = 0xA0B4D7, PantsColor = 0xFFE6AF, ShoeColor = 0xA0693C;

        static Appearance current = new Appearance();

        /// <summary>
        /// The look of the save being played (Terraria's default character until one is read or made). Setting it
        /// restyles the player's sprite and, in a LAN game, tells the others (<see cref="Version"/>).
        /// </summary>
        public static Appearance Current
        {
            get => current;
            set
            {
                current = (value ?? new Appearance()).Sanitized();
                Version++;
            }
        }

        /// <summary>Counts every change of <see cref="Current"/>: the sprite and the LAN game follow it.</summary>
        public static int Version { get; private set; }

        /// <summary>Current changed in place (an edit in game): restyle and resend.</summary>
        public static void Touch() => Version++;

        public static Appearance Default() => new Appearance();

        public Appearance Clone() => (Appearance)MemberwiseClone();

        public static Color ToColor(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static int ToRgb(Color c)
        {
            Color32 k = c;
            return (k.r << 16) | (k.g << 8) | k.b;
        }

        public bool SameAs(Appearance o) => o != null && Hair == o.Hair && Variant == o.Variant && HairColor == o.HairColor && SkinColor == o.SkinColor
            && EyeColor == o.EyeColor && ShirtColor == o.ShirtColor && UnderShirtColor == o.UnderShirtColor && PantsColor == o.PantsColor && ShoeColor == o.ShoeColor;

        /// <summary>The look with every number in range: a hair style that exists, a clothing style Terraria has, colours in 24 bits.</summary>
        public Appearance Sanitized()
        {
            var a = Clone();
            a.Hair = Mathf.Clamp(a.Hair, 0, HairCount - 1);
            a.Variant = Mathf.Clamp(a.Variant, 0, VariantCount - 1);
            a.HairColor &= 0xFFFFFF; a.SkinColor &= 0xFFFFFF; a.EyeColor &= 0xFFFFFF; a.ShirtColor &= 0xFFFFFF;
            a.UnderShirtColor &= 0xFFFFFF; a.PantsColor &= 0xFFFFFF; a.ShoeColor &= 0xFFFFFF;
            return a;
        }

        public TerraPlayerSprite.Palette ToPalette() => new TerraPlayerSprite.Palette
        {
            Hair = ToColor(HairColor), Skin = ToColor(SkinColor), Eye = ToColor(EyeColor), Shirt = ToColor(ShirtColor),
            UnderShirt = ToColor(UnderShirtColor), Pants = ToColor(PantsColor), Shoe = ToColor(ShoeColor),
        };

        [Newtonsoft.Json.JsonIgnore]
        public bool Male => IsMale(Variant);

        // ------------------------------------------------------------------ Terraria's ranges

        /// <summary>Player_Hair_1 .. Player_Hair_228 (Player.hair 0-227).</summary>
        public const int HairCount = 228;
        /// <summary>PlayerVariantID.Count: 0-9 the clothing styles, 10 and 11 the display dolls'.</summary>
        public const int VariantCount = 12;

        /// <summary>UICharacterCreation._validClothStyles: the clothing styles offered, in their buttons' order (the men's five, then the women's).</summary>
        public static readonly int[] ClothStyles = { 0, 2, 1, 3, 8, 9, 7, 5, 6, 4 };

        /// <summary>PlayerVariantID.Sets.Male.</summary>
        public static bool IsMale(int variant) => variant == 0 || variant == 1 || variant == 2 || variant == 3 || variant == 8 || variant == 10;

        /// <summary>PlayerVariantID.Sets.AltGenderReference: the same clothes on the other body.</summary>
        static readonly int[] otherGender = { 4, 5, 6, 7, 0, 1, 2, 3, 9, 8, 11, 10 };
        public static int OtherGender(int variant) => otherGender[Mathf.Clamp(variant, 0, VariantCount - 1)];

        /// <summary>Player.Male's setter: the clothing style moved to the asked body.</summary>
        public void SetMale(bool male)
        {
            if (male != Male) Variant = OtherGender(Variant);
        }

        /// <summary>UICharacterCreation._defaultHairstylesForClothStyle: the hair a clothing style comes with until one is picked.</summary>
        public static readonly Dictionary<int, int> DefaultHairFor = new Dictionary<int, int>
        {
            { 0, 0 }, { 2, 1 }, { 1, 12 }, { 3, 2 }, { 8, 28 }, { 9, 68 }, { 7, 18 }, { 5, 22 }, { 6, 81 }, { 4, 5 },
        };

        static List<int> creationHairs;

        /// <summary>
        /// HairstyleUnlocksHelper at character creation (and at the Stylist's): the hair styles offered, in their grid's
        /// order. The Martian and Moon Lord ones (123-133) and Plantera's (145, 162-164) only the Stylist sells.
        /// </summary>
        public static List<int> CreationHairs
        {
            get
            {
                if (creationHairs != null) return creationHairs;
                var l = new List<int>();
                for (int i = 0; i < 51; i++) l.Add(i);
                l.AddRange(new[] { 136, 137, 138, 139, 140, 141, 142, 143, 144, 147, 148, 149, 150, 151, 154, 155, 157, 158, 161 });
                for (int i = 51; i < 123; i++) l.Add(i);
                l.AddRange(new[] { 134, 135, 146, 152, 153, 156, 159, 165, 160 });
                for (int i = 166; i < 228; i++) l.Add(i);
                return creationHairs = l;
            }
        }

        /// <summary>The Stylist's list: the creation ones, with Plantera's after Plantera and the Martians' after the Martian Madness (here: the stages they come at).</summary>
        public static List<int> StylistHairs(bool plantera, bool martians, bool moonLord)
        {
            var l = new List<int>(CreationHairs);
            if (plantera) l.AddRange(new[] { 162, 164, 163, 145 });
            if (martians) l.AddRange(new[] { 132, 131, 130, 129, 128, 127, 126, 125, 124, 123 });
            if (martians && moonLord) l.Add(133);
            return l;
        }

        // ------------------------------------------------------------------ the sheets

        static readonly string[,] sheetNames = new string[VariantCount, 16];
        static readonly string[] hairNames = new string[HairCount], hairAltNames = new string[HairCount];

        /// <summary>
        /// TextureAssets.Players[variant, piece] as PlayerDataInitializer fills it: each style's own sheets, the rest the
        /// starter's of its body (the men's 0, the women's 4); the long coats' lower half (piece 14) only styles 3, 7 and 8
        /// have; the display dolls' blank pieces their sheet 2. Null for none.
        /// </summary>
        public static string Sheet(int variant, int piece)
        {
            variant = Mathf.Clamp(variant, 0, VariantCount - 1);
            if (piece < 0 || piece > 15) return null;
            var s = sheetNames[variant, piece];
            if (s != null) return s.Length == 0 ? null : s;
            int owner = Owner(variant, piece);
            s = owner == -2 ? "Player_10_2" : owner < 0 ? "" : "Player_" + owner + "_" + piece;
            sheetNames[variant, piece] = s;
            return s.Length == 0 ? null : s;
        }

        static bool In(int p, params int[] set) => System.Array.IndexOf(set, p) >= 0;

        static int Owner(int v, int p)
        {
            switch (v)
            {
                case 0: return p == 14 ? -1 : 0;
                case 1:
                case 2: return In(p, 4, 6, 8, 11, 12, 13) ? v : Owner(0, p);
                case 3:
                case 8: return In(p, 4, 6, 8, 11, 12, 13, 14) ? v : Owner(0, p);
                case 4: return p >= 3 && p <= 13 ? 4 : Owner(0, p);
                case 5:
                case 6:
                case 9: return In(p, 4, 6, 8, 11, 12, 13) ? v : Owner(4, p);
                case 7: return In(p, 4, 6, 8, 11, 12, 13, 14) ? 7 : Owner(4, p);
                case 10: return In(p, 0, 2, 3, 5, 7, 9, 10) ? 10 : In(p, 1, 4, 6, 8, 11, 12, 13, 15) ? -2 : Owner(0, p);
                default: return In(p, 3, 5, 7, 9, 10) ? 11 : In(p, 1, 2, 4, 6, 8, 11, 12, 13, 15) ? -2 : Owner(10, p);
            }
        }

        /// <summary>DrawPlayer_15_SkinLongCoat: the coat's tails over the legs, in the shirt's colour.</summary>
        public static bool HasCoat(int variant) => variant == 3 || variant == 7 || variant == 8;

        public static string HairSheet(int hair)
        {
            hair = Mathf.Clamp(hair, 0, HairCount - 1);
            return hairNames[hair] ??= "Player_Hair_" + (hair + 1);
        }

        /// <summary>TextureAssets.PlayerHairAlt: the hair cut short under a hat.</summary>
        public static string HairAltSheet(int hair)
        {
            hair = Mathf.Clamp(hair, 0, HairCount - 1);
            return hairAltNames[hair] ??= "Player_HairAlt_" + (hair + 1);
        }

        /// <summary>
        /// Player.GetHairSettings' backHairDraw: the long styles whose lower half hangs behind the body (drawn first,
        /// DrawPlayer_01_BackHair), only their top 26 rows over the head.
        /// </summary>
        public static bool BackHair(int hair)
        {
            int n = hair;
            bool back = n > 50 && (n < 56 || n > 63) && (n < 74 || n > 77) && (n < 88 || n > 89) && n != 94 && n != 100 && n != 104 && n != 112 && n < 116;
            return back || n == 133 || n == 134 || n == 146 || n == 162 || n == 6;
        }

        /// <summary>Player.GetHairDrawOffset, Terraria pixels facing right, y down.</summary>
        public static Vector2 HairOffset(int hair, bool hatHair) =>
            !hatHair && hair == 163 ? new Vector2(0f, -2f) : hair == 164 ? new Vector2(-2f, 0f) : Vector2.zero;

        static readonly HashSet<int> fullHairHeads = new HashSet<int>
        {
            10, 12, 28, 42, 62, 97, 106, 113, 116, 119, 133, 138, 139, 163, 178, 181, 191, 198, 217, 218, 220, 222, 224, 225, 228, 229, 230, 232,
            235, 238, 242, 243, 244, 245, 272, 273, 274, 277, 284, 290,
        };
        static readonly HashSet<int> hatHairHeads = new HashSet<int>
        {
            13, 14, 15, 16, 18, 21, 24, 25, 26, 29, 40, 44, 51, 56, 59, 60, 63, 64, 65, 67, 68, 69, 81, 92, 94, 95, 100, 114, 121, 126, 130, 136, 140,
            143, 145, 158, 159, 161, 182, 184, 190, 195, 215, 216, 219, 223, 226, 227, 231, 233, 234, 262, 263, 264, 265, 267, 275, 279, 280, 281, 286,
            289, 292,
        };

        /// <summary>
        /// Player.GetHairSettings for a helmet (its head sheet; 0 none): fullHair (the hair drawn over the helmet), hatHair
        /// (the short cut, Player_HairAlt, under it), and drawsBackHairWithoutHeadgear (no helmet, or 259).
        /// </summary>
        public static void HairSettings(int head, out bool fullHair, out bool hatHair, out bool backWithoutHeadgear)
        {
            fullHair = head > 0 && fullHairHeads.Contains(head);
            hatHair = head > 0 && hatHairHeads.Contains(head);
            backWithoutHeadgear = head <= 0 || head == 259;
        }

        // ------------------------------------------------------------------ colours

        /// <summary>UICharacterCreation.ScaledHslToRgb: Main.hslToRgb with the luminosity kept above 0.15.</summary>
        public static Color ScaledHslToRgb(float hue, float saturation, float luminosity) => HslToRgb(hue, saturation, luminosity * 0.85f + 0.15f);

        /// <summary>UICharacterCreation.RgbToScaledHsl.</summary>
        public static Vector3 RgbToScaledHsl(Color c)
        {
            var v = RgbToHsl(c);
            v.z = (v.z - 0.15f) / 0.85f;
            return new Vector3(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z));
        }

        /// <summary>Main.hslToRgb.</summary>
        public static Color HslToRgb(float h, float s, float l)
        {
            byte r, g, b;
            if (s == 0f)
            {
                r = g = b = (byte)System.Math.Round(l * 255.0);
            }
            else
            {
                double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
                double p = 2.0 * l - q;
                r = (byte)System.Math.Round(Hue2Rgb(h + 1.0 / 3.0, p, q) * 255.0);
                g = (byte)System.Math.Round(Hue2Rgb(h, p, q) * 255.0);
                b = (byte)System.Math.Round(Hue2Rgb(h - 1.0 / 3.0, p, q) * 255.0);
            }
            return new Color32(r, g, b, 255);
        }

        static double Hue2Rgb(double c, double t1, double t2)
        {
            if (c < 0.0) c++;
            if (c > 1.0) c--;
            if (6.0 * c < 1.0) return t1 + (t2 - t1) * 6.0 * c;
            if (2.0 * c < 1.0) return t2;
            if (3.0 * c < 2.0) return t1 + (t2 - t1) * (2.0 / 3.0 - c) * 6.0;
            return t1;
        }

        /// <summary>Main.rgbToHsl.</summary>
        public static Vector3 RgbToHsl(Color c)
        {
            Color32 k = c;
            float r = k.r / 255f, g = k.g / 255f, b = k.b / 255f;
            float max = Mathf.Max(r, Mathf.Max(g, b)), min = Mathf.Min(r, Mathf.Min(g, b));
            float h = 0f, s, l = (max + min) / 2f;
            if (max == min) h = s = 0f;
            else
            {
                float d = max - min;
                s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                if (max == g) h = (b - r) / d + 2f;
                if (max == b) h = (r - g) / d + 4f;
                h /= 6f;
            }
            return new Vector3(h, s, l);
        }

        /// <summary>"#RRGGBB" (UICharacterCreation.GetHexText).</summary>
        public static string Hex(int rgb) => "#" + (rgb & 0xFFFFFF).ToString("X6");

        /// <summary>UICharacterCreation.GetHexColor: "#RRGGBB" or "RRGGBB" (six hex digits at most).</summary>
        public static bool ParseHex(string text, out int rgb)
        {
            rgb = 0;
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (text.StartsWith("#")) text = text.Substring(1);
            return text.Length > 0 && text.Length <= 6
                && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb);
        }

        static Color RandomColor() => ScaledHslToRgb(Random.value, Random.value, Random.value);

        /// <summary>
        /// UICharacterCreation.Click_RandomizePlayer: a hair style of the creation list, a dark enough eye colour, a skin
        /// tone, any colours for the hair and clothes, any clothing style, then the body the hair style goes with.
        /// </summary>
        public void Randomize()
        {
            var hairs = CreationHairs;
            Hair = hairs[Random.Range(0, hairs.Count)];
            Color32 eye = RandomColor();
            while (eye.r + eye.g + eye.b > 300) eye = RandomColor();
            EyeColor = ToRgb(eye);
            float tone = Mathf.Min(1f, Random.Range(60, 120) * 0.01f);
            SkinColor = ((int)(Random.Range(240, 255) * tone) << 16) | ((int)(Random.Range(110, 140) * tone) << 8) | (int)(Random.Range(75, 110) * tone);
            HairColor = ToRgb(RandomColor());
            ShirtColor = ToRgb(RandomColor());
            UnderShirtColor = ToRgb(RandomColor());
            PantsColor = ToRgb(RandomColor());
            ShoeColor = ToRgb(RandomColor());
            Variant = ClothStyles[Random.Range(0, ClothStyles.Length)];
            SetMale(!femaleHairs.Contains(Hair + 1));
        }

        /// <summary>The randomizer's hair styles (Player.hair + 1) that make a woman.</summary>
        static readonly HashSet<int> femaleHairs = new HashSet<int>
        {
            5, 6, 7, 10, 12, 19, 22, 23, 26, 27, 30, 33, 34, 35, 37, 38, 39, 40, 41, 44, 45, 46, 47, 48, 49, 51, 56, 65, 66, 67, 68, 69, 70, 71, 72,
            73, 74, 79, 80, 81, 82, 84, 85, 86, 87, 88, 90, 91, 92, 93, 95, 96, 98, 100, 102, 104, 107, 108, 113, 124, 126, 133, 134, 135, 144, 146,
            147, 163, 165,
        };

        // ------------------------------------------------------------------ Terraria's player template (copy and paste)

        /// <summary>UICharacterCreation.GetPlayerTemplateValues: the look as Terraria copies it to the clipboard.</summary>
        public string ToTemplate()
        {
            var o = new Newtonsoft.Json.Linq.JObject
            {
                ["version"] = 1,
                ["hairStyle"] = Hair,
                ["clothingStyle"] = Variant,
                ["voiceStyle"] = Male ? 1 : 2,
                ["voicePitch"] = 0.0,
                ["hairColor"] = Hex(HairColor),
                ["eyeColor"] = Hex(EyeColor),
                ["skinColor"] = Hex(SkinColor),
                ["shirtColor"] = Hex(ShirtColor),
                ["underShirtColor"] = Hex(UnderShirtColor),
                ["pantsColor"] = Hex(PantsColor),
                ["shoeColor"] = Hex(ShoeColor),
            };
            return o.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>UICharacterCreation.Click_PastePlayerTemplate: what of a template is valid taken over; false when the text holds none.</summary>
        public bool FromTemplate(string text, IList<int> hairsAllowed)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int a = text.IndexOf('{'), b = text.LastIndexOf('}');
            if (a < 0 || b <= a) return false;
            Newtonsoft.Json.Linq.JObject o;
            try { o = Newtonsoft.Json.Linq.JObject.Parse(text.Substring(a, b - a + 1)); }
            catch (System.Exception) { return false; }
            var keys = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();
            foreach (var p in o.Properties()) keys[p.Name.ToLowerInvariant()] = p.Value;
            bool any = false;
            if (keys.TryGetValue("hairstyle", out var t) && t.Type == Newtonsoft.Json.Linq.JTokenType.Integer && hairsAllowed.Contains((int)t)) { Hair = (int)t; any = true; }
            if (keys.TryGetValue("clothingstyle", out t) && t.Type == Newtonsoft.Json.Linq.JTokenType.Integer && System.Array.IndexOf(ClothStyles, (int)t) >= 0) { Variant = (int)t; any = true; }
            void Col(string key, ref int field)
            {
                if (keys.TryGetValue(key, out var v) && v.Type == Newtonsoft.Json.Linq.JTokenType.String && ParseHex((string)v, out int rgb))
                {
                    // Through Terraria's scaled HSL, as its paste does.
                    var hsl = RgbToScaledHsl(ToColor(rgb));
                    field = ToRgb(ScaledHslToRgb(hsl.x, hsl.y, hsl.z));
                    any = true;
                }
            }
            Col("haircolor", ref HairColor);
            Col("eyecolor", ref EyeColor);
            Col("skincolor", ref SkinColor);
            Col("shirtcolor", ref ShirtColor);
            Col("undershirtcolor", ref UnderShirtColor);
            Col("pantscolor", ref PantsColor);
            Col("shoecolor", ref ShoeColor);
            return any;
        }

        // ------------------------------------------------------------------ the wire (a LAN game's PlayerInfo)

        public void Write(BinaryWriter w)
        {
            w.Write((byte)1);
            w.Write((ushort)Hair);
            w.Write((byte)Variant);
            foreach (int c in new[] { HairColor, SkinColor, EyeColor, ShirtColor, UnderShirtColor, PantsColor, ShoeColor })
            {
                w.Write((byte)(c >> 16));
                w.Write((byte)(c >> 8));
                w.Write((byte)c);
            }
        }

        public static Appearance Read(BinaryReader r)
        {
            int version = r.ReadByte();
            var a = new Appearance { Hair = r.ReadUInt16(), Variant = r.ReadByte() };
            int C() => (r.ReadByte() << 16) | (r.ReadByte() << 8) | r.ReadByte();
            a.HairColor = C(); a.SkinColor = C(); a.EyeColor = C(); a.ShirtColor = C();
            a.UnderShirtColor = C(); a.PantsColor = C(); a.ShoeColor = C();
            _ = version;
            return a.Sanitized();
        }
    }
}
