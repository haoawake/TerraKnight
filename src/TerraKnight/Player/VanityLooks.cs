using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// What the player wears looks as Terraria draws it (the user's 10-25 "穿戴外观全按原版来"): Player.PlayerFrame's
    /// SetMatch (a body's own legs, the man's or woman's sheet of a piece), the body's own capes, the pieces that take others
    /// off (a body without hand accessories or belts, legs without shoes), the face accessories' layers, and PlayerDrawSet's
    /// glowmasks with their colours; the ArmorIDs sets it reads are VanityLooksData's.
    /// </summary>
    internal static partial class VanityLooks
    {
        /// <summary>The worn head, body and legs, the accessories already in (Player.UpdateVisibleAccessories), dressed as PlayerFrame goes on.</summary>
        internal static void Dress(ref TerraPlayerSprite.Equips e, bool male, bool werewolf, bool merfolk)
        {
            // SetMatch: the body's own legs (a robe's) over the worn ones, the legs' and the head's sheets by sex.
            int legs = MatchBodyLegs(e.Body, e.Legs, male, out e.Robe);
            if (legs >= 0) e.Legs = legs;
            legs = MatchLegs(e.Legs, male);
            if (legs >= 0) e.Legs = legs;
            if (e.Head == 201 && !male) e.Head = 202;
            if (e.Shoe > 0 && !male && ShoeFemale.TryGetValue(e.Shoe, out int shoe)) e.Shoe = shoe;
            if (e.Body == 93) { e.Shield = 0; e.HandOff = 0; }
            if (e.Body > 0 && BodyNoHands.Contains(e.Body)) { e.HandOn = 0; e.HandOff = 0; }
            if (e.Body > 0 && e.Waist > 0 && BodyNoBelt.Contains(e.Body) && WaistBelt.Contains(e.Waist)) e.Waist = 0;
            // The body's own capes: its back (a man's or a woman's) unless a cape is worn, its front unless one is; both halves
            // of a cloak when neither is.
            bool bodyFront = false, bodyBack = false;
            if (e.Body > 0)
            {
                if ((male ? BodyCapeBack : BodyCapeBackFemale).TryGetValue(e.Body, out int b))
                {
                    if (BackBackpack.Contains(b)) e.Backpack = b;
                    else if (BackTail.Contains(b)) e.Tail = b;
                    else if (e.Back <= 0) e.Back = b;
                }
                if (BodyCapeFront.TryGetValue(e.Body, out int f) && e.Front <= 0)
                {
                    bodyFront = true;
                    e.Front = f;
                }
                if (BodyCapeBoth.TryGetValue(e.Body, out var both) && e.Back <= 0 && e.Front <= 0)
                {
                    bodyFront = true;
                    e.Front = both.Front;
                    if (BackBackpack.Contains(both.Back)) e.Backpack = both.Back;
                    else if (BackTail.Contains(both.Back)) e.Tail = both.Back;
                    else { bodyBack = true; e.Back = both.Back; }
                }
            }
            if (e.Legs == 67 || e.Legs == 140) e.Shoe = 0;
            if (werewolf || merfolk)
            {
                e.Head = werewolf ? 38 : 39;
                e.Body = werewolf ? 21 : 22;
                e.Legs = werewolf ? 20 : 21;
                if (bodyFront) e.Front = 0;
                if (bodyBack) e.Back = 0;
                e.Face = e.FaceHead = e.FaceFlower = e.FaceMask = 0;
                e.Robe = false;
            }
            // A face accessory under a head piece: one that takes the helmet's place hides it; any other but the blindfold
            // (drawn under the hair) is hidden (the Chippy's headband goes with its own head).
            if (e.Head > 0 && e.Face > 0 && !(e.Head == 287 && e.Face == 23))
            {
                if (FaceOverrideHelmet.Contains(e.Face)) { e.Head = 0; e.FaceHead = 0; e.HelmetHidden = true; }
                else if (!FaceUnderHair.Contains(e.Face)) e.Face = 0;
            }
            if (e.Head > 0 && e.FaceHead > 0 && HeadAltFaceHead.Contains(e.Head) && FaceAltHead.TryGetValue(e.FaceHead, out int alt) && alt > 0) e.FaceHead = alt;
            if (e.FaceFlower > 0 && e.Head > 0 && HeadPreventFaceFlower.Contains(e.Head)) e.FaceFlower = 0;
            if (e.FaceMask > 0 && e.Head > 0)
            {
                if (HeadPreventFaceMask.Contains(e.Head)) e.FaceMask = 0;
                else e.FaceMaskUnder = HeadFaceMaskUnder.Contains(e.Head);
            }
            if (e.Head > 0 && HeadPreventBeard.Contains(e.Head)) e.Beard = 0;
            e.BeardHair = e.Beard > 0 && BeardHairColor.Contains(e.Beard);
            // The rest of the head: drawn again behind (a hood's back), in the skin's colour, the head's skin under it or not.
            e.BackHead = e.Head > 0 && HeadFrontToBack.TryGetValue(e.Head, out int backHead) ? backHead : 0;
            e.HeadSkin = e.Head > 0 && HeadUseSkinColor.Contains(e.Head);
            e.HideHeadSkin = e.Head > 0 && HeadHidesHead.Contains(e.Head);
            e.HideTopSkin = e.Body == 82 || e.Body == 83 || e.Body == 93 || e.Body == 21 || e.Body == 22;
            e.HideBottomSkin = e.Body == 93 || e.Legs == 20 || e.Legs == 21 || e.Legs == 216 || e.Legs == 214 || e.Legs == 215;
            // The long coats' tails over the legs (GetMatchingBodyExtension), the Chicken Bones robe's both ways.
            e.BodyExt = BodyExtension(e.Body, male);
            if (e.Coat > 0)
            {
                e.CoatExt = BodyExtension(e.Coat, male);
                e.CoatBack = e.Coat == 251 ? 239 : 0;
            }
            e.HideShoulders = e.Coat > 0 || e.Front > 0 && FrontHidesShoulders.Contains(e.Front);
            Glows(ref e, male);
        }

        /// <summary>SetMatch for armorSlotRequested 1: the legs a body brings (-1 none); a robe of them (wearsRobe).</summary>
        static int MatchBodyLegs(int body, int legs, bool male, out bool robe)
        {
            robe = false;
            int n = -1;
            bool flag = true;
            switch (body)
            {
                case 15: n = 88; break;
                case 36: n = 89; break;
                case 41: n = 97; break;
                case 42: n = 90; break;
                case 58: n = 91; break;
                case 59: n = 92; break;
                case 60: n = 93; break;
                case 61: n = 94; break;
                case 62: n = 95; break;
                case 63: n = 96; break;
                case 77: n = 121; break;
                case 165: n = male ? 118 : 99; break;
                case 166: flag = false; n = male ? 119 : 100; break;
                case 167: n = male ? 101 : 102; break;
                case 180: n = 115; break;
                case 181: n = 116; break;
                case 183: n = male ? 136 : 123; break;
                case 191: n = 131; break;
                case 93: n = 165; break;
                case 90: n = 166; break;
                case 88: n = 168; break;
                case 81: if (legs <= 0) n = 169; break;
                case 213: n = 187; break;
                case 215: n = 189; break;
                case 219: n = 196; break;
                case 221: n = 199; break;
                case 223: n = 204; break;
                case 231: n = 214; break;
                case 232: n = 215; break;
                case 233: n = 216; break;
                case 241: n = 229; break;
                case 256: n = 244; break;
            }
            if (n != -1) robe = flag;
            return n;
        }

        /// <summary>SetMatch for armorSlotRequested 2: the man's or woman's sheet of a pair of legs (-1: as it is).</summary>
        static int MatchLegs(int legs, bool male)
        {
            switch (legs)
            {
                case 83: return male ? 117 : -1;
                case 84: return male ? 120 : -1;
                case 132: return male ? 135 : -1;
                case 57: return male ? 137 : -1;
                case 180: return male ? -1 : 179;
                case 184: return male ? -1 : 183;
                case 146: return male ? 146 : 147;
                case 154: return male ? 155 : 154;
                case 158: return male ? 157 : -1;
                case 191: return male ? -1 : 192;
                case 193: return male ? -1 : 194;
                case 197: return male ? -1 : 198;
                case 203: return male ? -1 : 202;
                case 208: return male ? -1 : 207;
                case 219: return male ? -1 : 220;
                case 232: return male ? -1 : 233;
                case 236: return male ? -1 : 248;
                case 249: return male ? -1 : 250;
                default: return -1;
            }
        }

        /// <summary>PlayerDrawLayers.GetMatchingBodyExtension: a body's coat tails, a legs sheet over the legs (0 none).</summary>
        internal static int BodyExtension(int body, bool male)
        {
            switch (body)
            {
                case 200: return 149;
                case 202: return 151;
                case 201: return 150;
                case 209: return 160;
                case 207: return 161;
                case 198: return 162;
                case 182: return 163;
                case 168: return 164;
                case 73: return 170;
                case 52: return male ? 171 : 172;
                case 187: return 173;
                case 205: return 174;
                case 53: return male ? 175 : 176;
                case 210: return male ? 178 : 177;
                case 211: return male ? 182 : 181;
                case 218: return 195;
                case 222: return male ? 201 : 200;
                case 225: return 206;
                case 236: return 221;
                case 237: return 223;
                case 89: return 186;
                case 81: return 169;
                case 251: return 238;
                default: return 0;
            }
        }

        static Color32 Glow(int v, int a) => new Color32((byte)v, (byte)v, (byte)v, (byte)a);

        /// <summary>
        /// PlayerDrawSet's glowmasks (num2..num5 are always 3 there: the lunar sets' full 250): the head's and legs' Glow_N,
        /// the body's own sheet's lower rows (the composite pieces' glow, 224 down), and their colours.
        /// </summary>
        static void Glows(ref TerraPlayerSprite.Equips e, bool male)
        {
            e.HeadGlow = null;
            e.LegsGlow = null;
            e.BodyGlow = 0;
            e.BodyGlowArms = false;
            e.HeadGlowColor = e.BodyGlowColor = e.ArmGlowColor = e.LegsGlowColor = new Color32(0, 0, 0, 0);
            var full = Glow(250, 0);
            var arkhalis = e.UnderShirt;
            arkhalis.a = 180;
            // Main.mouseTextColor runs between 190 and 255; its square, the glow's strength.
            float m = (190f + 65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f))) / 255f, pulse = m * m;
            Color32 mouse = Glow(Mathf.RoundToInt(255f * pulse), 255);
            switch (e.Head)
            {
                case 169: e.HeadGlow = "Glow_15"; e.HeadGlowColor = full; break;
                case 216: e.HeadGlow = "Glow_256"; e.HeadGlowColor = Glow(127, 0); break;
                case 210: e.HeadGlow = "Glow_242"; e.HeadGlowColor = Glow(127, 0); break;
                case 214: e.HeadGlow = "Glow_245"; e.HeadGlowColor = arkhalis; break;
                case 240: e.HeadGlow = "Glow_273"; e.HeadGlowColor = Glow(230, 60); break;
                case 267: e.HeadGlow = "Glow_301"; e.HeadGlowColor = Glow(230, 60); break;
                case 268: e.HeadGlow = "Glow_302"; e.HeadGlowColor = mouse; break;
                case 269: e.HeadGlow = "Glow_304"; e.HeadGlowColor = Glow(200, 255); break;
                case 270: e.HeadGlow = "Glow_305"; e.HeadGlowColor = Glow(200, 150); break;
                case 170: e.HeadGlow = "Glow_16"; e.HeadGlowColor = full; break;
                case 189: e.HeadGlow = "Glow_184"; e.HeadGlowColor = full; break;
                case 175: e.HeadGlow = "Glow_41"; e.HeadGlowColor = Glow(255, 0); break;
                case 193: e.HeadGlow = "Glow_209"; e.HeadGlowColor = Glow(255, 127); break;
                case 109: e.HeadGlow = "Glow_208"; e.HeadGlowColor = Glow(255, 0); break;
                case 178: e.HeadGlow = "Glow_96"; e.HeadGlowColor = Glow(255, 0); break;
                case 282: e.HeadGlow = "Glow_357"; e.HeadGlowColor = Glow(Mathf.RoundToInt(255f * pulse), 0); break;
                case 285: e.HeadGlow = "Glow_367"; e.HeadGlowColor = Glow(255, 0); break;
                case 291: e.HeadGlow = "Glow_375"; e.HeadGlowColor = Glow(255, 255); break;
            }
            switch (e.Body)
            {
                case 175: case 176: case 190: e.BodyGlowColor = full; e.ArmGlowColor = e.Body == 175 ? new Color32(0, 0, 0, 0) : full; break;
                case 208: e.BodyGlowColor = e.ArmGlowColor = arkhalis; break;
                case 227: e.BodyGlowColor = e.ArmGlowColor = Glow(230, 60); break;
                case 237: e.BodyGlowColor = mouse; break;
                case 238: case 260: e.BodyGlowColor = e.ArmGlowColor = Glow(255, 255); break;
                case 239: e.BodyGlowColor = e.ArmGlowColor = Glow(200, 150); break;
                case 194: e.BodyGlowColor = e.ArmGlowColor = Glow(255, 127); break;
                case 179: e.BodyGlowColor = e.ArmGlowColor = Glow(255, 0); break;
            }
            if (e.BodyGlowColor.a != 0 || e.BodyGlowColor.r != 0)
            {
                e.BodyGlow = e.Body;
                e.BodyGlowArms = e.ArmGlowColor.a != 0 || e.ArmGlowColor.r != 0;
            }
            switch (e.Legs)
            {
                case 111: e.LegsGlow = "Glow_17"; e.LegsGlowColor = full; break;
                case 157: e.LegsGlow = "Glow_249"; e.LegsGlowColor = arkhalis; break;
                case 158: e.LegsGlow = "Glow_250"; e.LegsGlowColor = arkhalis; break;
                case 210: e.LegsGlow = "Glow_274"; e.LegsGlowColor = Glow(230, 60); break;
                case 222: e.LegsGlow = "Glow_303"; e.LegsGlowColor = mouse; break;
                case 225: e.LegsGlow = "Glow_306"; e.LegsGlowColor = Glow(200, 150); break;
                case 226: e.LegsGlow = "Glow_307"; e.LegsGlowColor = Glow(200, 150); break;
                case 110: e.LegsGlow = "Glow_199"; e.LegsGlowColor = full; break;
                case 134: e.LegsGlow = "Glow_212"; e.LegsGlowColor = Glow(255, 127); break;
                case 130: e.LegsGlow = "Glow_187"; e.LegsGlowColor = Glow(254, 0); break;
            }
        }

        /// <summary>Face.Sets.DrawInFaceUnderHairLayer: under the hair (the blindfold).</summary>
        internal static bool UnderHair(int face) => face > 0 && FaceUnderHair.Contains(face);

        /// <summary>Face.Sets.PreventHairDraw: the hair not drawn with it on (the diving gears, the Bone Helm).</summary>
        internal static bool TakesHair(int face) => face > 0 && FacePreventHair.Contains(face);

        /// <summary>Player.UpdateVisibleAccessory's face slots: a face sheet into its layer (faceHead, faceMask, faceFlower or face).</summary>
        internal static void WearFace(ref TerraPlayerSprite.Equips e, int face)
        {
            if (face <= 0) return;
            if (FaceHeadLayer.Contains(face)) e.FaceHead = face;
            else if (FaceMaskLayer.Contains(face)) e.FaceMask = face;
            else if (FaceFlowerLayer.Contains(face)) e.FaceFlower = face;
            else e.Face = face;
        }

        /// <summary>Its back sheet: a backpack, a tail, or a cape (which takes off a front worn before it).</summary>
        internal static void WearBack(ref TerraPlayerSprite.Equips e, int back)
        {
            if (back <= 0) return;
            if (BackBackpack.Contains(back)) e.Backpack = back;
            else if (BackTail.Contains(back)) e.Tail = back;
            else
            {
                e.Back = back;
                e.Front = 0;
            }
        }

        /// <summary>Body.Sets.showsShouldersWhileJumping: a body's shoulders on the jump frame (no body: the skin's are).</summary>
        internal static bool ShouldersOnJump(int body) => body <= 0 || BodyShowsShouldersJumping.Contains(body);

        /// <summary>Body.Sets.shouldersAreAlwaysInTheBack: its front shoulder behind the front arm on every frame.</summary>
        internal static bool ShouldersBehind(int body) => body > 0 && BodyShouldersBack.Contains(body);

        /// <summary>GetBeardOffsetAddonFromHelmet: a beard moved for some helmets (px, facing right).</summary>
        internal static float BeardShift(int head) => head == 165 ? 8f : head == 146 || head == 150 || head == 152 || head == 148 ? 2f : 0f;
    }
}
