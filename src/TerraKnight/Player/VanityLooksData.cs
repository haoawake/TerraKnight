// <generated> by tools/vanity/gen_vanity.py from Terraria's ArmorIDs.*.Sets (vanity-runtime.json). </generated>
using System.Collections.Generic;

namespace TerraKnight.Player
{
    internal static partial class VanityLooks
    {
        /// <summary>Head.Sets.FrontToBackID: a head drawn again behind the body (DrawPlayer_01_3_BackHead).</summary>
        static readonly Dictionary<int, int> HeadFrontToBack = new Dictionary<int, int> { [133] = 252, [224] = 253, [242] = 246, [243] = 247, [244] = 248, [245] = 249 };
        /// <summary>Head.Sets.UseSkinColor: drawn in the skin's colour.</summary>
        static readonly HashSet<int> HeadUseSkinColor = new HashSet<int> { 274, 277 };
        /// <summary>Head.Sets.HidesHead: the head's skin not drawn under it.</summary>
        static readonly HashSet<int> HeadHidesHead = new HashSet<int> { 38, 135, 269, 282, 288 };
        /// <summary>Head.Sets.PreventFaceFlowerDraw.</summary>
        static readonly HashSet<int> HeadPreventFaceFlower = new HashSet<int> { 92, 275 };
        /// <summary>Head.Sets.PreventFaceMaskDraw.</summary>
        static readonly HashSet<int> HeadPreventFaceMask = new HashSet<int> { 20, 22, 27, 58, 77, 107, 108, 118, 123, 125, 127, 136, 137, 142, 146, 147, 148, 150, 151, 152, 153, 154, 155, 162, 164, 165, 168, 169, 173, 175, 186, 187, 188, 192, 193, 196, 207, 208, 209, 210, 221, 236, 241, 260, 268, 270, 271, 276 };
        /// <summary>Head.Sets.DrawFaceMaskUnderHeadLayer.</summary>
        static readonly HashSet<int> HeadFaceMaskUnder = new HashSet<int> { 10, 12, 26, 51, 52, 53, 55, 71, 81, 92, 106, 160, 166, 167, 178, 198, 203, 267, 278, 279, 289, 291 };
        /// <summary>Head.Sets.PreventBeardDraw.</summary>
        static readonly HashSet<int> HeadPreventBeard = new HashSet<int> { 17, 22, 28, 31, 46, 47, 48, 102, 105, 107, 108, 118, 119, 120, 123, 125, 127, 129, 132, 135, 136, 137, 153, 154, 155, 162, 164, 168, 169, 172, 179, 185, 186, 187, 188, 199, 207, 208, 209, 210, 213, 241, 260, 261, 269, 271, 276 };
        /// <summary>Head.Sets.UseAltFaceHeadDraw: the face-head piece swapped for its alternative (Face.Sets.AltFaceHead).</summary>
        static readonly HashSet<int> HeadAltFaceHead = new HashSet<int> { 3, 4, 6, 7, 8, 9, 12, 17, 22, 27, 28, 30, 31, 32, 35, 36, 37, 38, 39, 40, 42, 43, 45, 46, 47, 48, 49, 50, 57, 58, 61, 66, 69, 70, 72, 73, 74, 75, 76, 77, 78, 79, 80, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 93, 98, 101, 102, 103, 104, 105, 107, 108, 109, 110, 111, 112, 115, 118, 120, 122, 123, 124, 127, 128, 129, 131, 132, 134, 135, 136, 137, 141, 142, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 160, 162, 164, 165, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 185, 186, 187, 188, 189, 192, 193, 194, 196, 200, 201, 202, 204, 205, 207, 208, 209, 210, 211, 213, 214, 230, 236, 237, 239, 240, 241, 251, 254, 255, 256, 257, 258, 260, 261, 266, 268, 269, 270, 271, 283, 287, 291, 292 };
        /// <summary>Body.Sets.IncludedCapeBack: a body's own cape (a back sheet), a man's.</summary>
        static readonly Dictionary<int, int> BodyCapeBack = new Dictionary<int, int> { [24] = 29, [80] = 21, [94] = 19, [96] = 18, [185] = 17, [205] = 11, [206] = 12, [207] = 13, [217] = 22, [238] = 32 };
        /// <summary>Body.Sets.IncludedCapeBackFemale: a woman's.</summary>
        static readonly Dictionary<int, int> BodyCapeBackFemale = new Dictionary<int, int> { [24] = 29, [80] = 21, [94] = 19, [96] = 18, [185] = 17, [205] = 11, [206] = 12, [207] = 13, [217] = 23, [238] = 32 };
        /// <summary>Body.Sets.IncludedCapeFront: its front (a front sheet).</summary>
        static readonly Dictionary<int, int> BodyCapeFront = new Dictionary<int, int> { [184] = 6, [248] = 13 };
        /// <summary>Body.Sets.IncludeCapeFrontAndBack: both halves, when no other cape is worn.</summary>
        static readonly Dictionary<int, (int Front, int Back)> BodyCapeBoth = new Dictionary<int, (int, int)> { [85] = (7, 20), [235] = (9, 30), [236] = (10, 31) };
        /// <summary>Body.Sets.showsShouldersWhileJumping: its shoulders drawn on the jump frame.</summary>
        static readonly HashSet<int> BodyShowsShouldersJumping = new HashSet<int> { 24, 46, 54, 55, 56, 65, 66, 67, 69, 70, 71, 73, 75, 92, 95, 105, 106, 175, 176, 177, 182, 183, 187, 190, 194, 197, 198, 201, 204, 226, 227, 229, 239, 259 };
        /// <summary>Body.Sets.shouldersAreAlwaysInTheBack.</summary>
        static readonly HashSet<int> BodyShouldersBack = new HashSet<int> { 190 };
        /// <summary>Body.Sets.DisableHandOnAndOffAccDraw.</summary>
        static readonly HashSet<int> BodyNoHands = new HashSet<int> { 83 };
        /// <summary>Body.Sets.DisableBeltAccDraw.</summary>
        static readonly HashSet<int> BodyNoBelt = new HashSet<int> { 82, 83 };
        /// <summary>Back.Sets.DrawInBackpackLayer.</summary>
        static readonly HashSet<int> BackBackpack = new HashSet<int> { 7, 8, 9, 10, 15, 16, 32, 33 };
        /// <summary>Back.Sets.DrawInTailLayer.</summary>
        static readonly HashSet<int> BackTail = new HashSet<int> { 18, 19, 21, 25, 26, 27, 28 };
        /// <summary>Front.Sets.HidesCompositeShoulders.</summary>
        static readonly HashSet<int> FrontHidesShoulders = new HashSet<int> { 8, 11, 15, 16, 17 };
        /// <summary>Waist.Sets.IsABelt.</summary>
        static readonly HashSet<int> WaistBelt = new HashSet<int> { 5, 10, 12, 17 };
        /// <summary>Face.Sets.PreventHairDraw.</summary>
        static readonly HashSet<int> FacePreventHair = new HashSet<int> { 2, 3, 4, 19 };
        /// <summary>Face.Sets.OverrideHelmet: the head piece hidden under it.</summary>
        static readonly HashSet<int> FaceOverrideHelmet = new HashSet<int> { 2, 3, 4, 19 };
        /// <summary>Face.Sets.DrawInFaceUnderHairLayer.</summary>
        static readonly HashSet<int> FaceUnderHair = new HashSet<int> { 5 };
        /// <summary>Face.Sets.DrawInFaceMaskLayer.</summary>
        static readonly HashSet<int> FaceMaskLayer = new HashSet<int> { 22 };
        /// <summary>Face.Sets.DrawInFaceFlowerLayer.</summary>
        static readonly HashSet<int> FaceFlowerLayer = new HashSet<int> { 1, 6, 8, 9 };
        /// <summary>Face.Sets.DrawInFaceHeadLayer.</summary>
        static readonly HashSet<int> FaceHeadLayer = new HashSet<int> { 10, 11, 12, 13 };
        /// <summary>Face.Sets.AltFaceHead.</summary>
        static readonly Dictionary<int, int> FaceAltHead = new Dictionary<int, int> { [10] = 15, [11] = 16, [12] = 17, [13] = 18 };
        /// <summary>Beard.Sets.UseHairColor.</summary>
        static readonly HashSet<int> BeardHairColor = new HashSet<int> { 2, 3, 4 };
        /// <summary>Shoe.Sets.MaleToFemaleID.</summary>
        static readonly Dictionary<int, int> ShoeFemale = new Dictionary<int, int> { [25] = 26 };
    }
}
