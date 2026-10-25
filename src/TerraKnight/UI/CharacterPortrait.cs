using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// A character drawn standing in a GUI rect, as Terraria's menus draw one (the character list's portraits, the
    /// creation screen's preview, UICharacter): its body-part sheets tinted in its colours, in the order PlayerDrawLayers
    /// draws a player without armor, on the same 40x56 frames as <see cref="TerraPlayerSprite"/>, scaled to fit.
    /// </summary>
    internal static class CharacterPortrait
    {
        const int FrameW = 40, FrameH = 56;

        /// <summary>The character standing, facing right, as big as the rect holds it.</summary>
        public static void Draw(Rect r, Appearance a) => Draw(r, a, 0, true, 0f);

        /// <summary>UICharacter's animated preview: walking in place, a frame every 0.07 s through frames 6-19.</summary>
        public static void DrawWalking(Rect r, Appearance a, bool faceRight = true, float scale = 0f, int variant = -1, int hair = -1)
        {
            int frame = (int)(Time.unscaledTime / 0.07f) % 14 + 6;
            Draw(r, a, frame, faceRight, scale, variant, hair);
        }

        /// <summary>
        /// One frame (body and legs alike: 0 standing, 6-19 walking) centred in the rect, facing right or left, at
        /// <paramref name="scale"/> GUI pixels per Terraria pixel (0: the largest whole number that fits, at least 1);
        /// <paramref name="variant"/> and <paramref name="hair"/> other than the look's when not -1 (the style buttons' previews).
        /// </summary>
        public static void Draw(Rect r, Appearance a, int frame, bool faceRight, float scale, int variant = -1, int hair = -1)
        {
            if (Event.current.type != EventType.Repaint || a == null) return;
            frame = Mathf.Clamp(frame, 0, 19);
            if (scale <= 0f) scale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(r.width / FrameW, r.height / FrameH)));
            var box = new Rect(Mathf.Round(r.center.x - FrameW * scale / 2f), Mathf.Round(r.center.y - FrameH * scale / 2f), FrameW * scale, FrameH * scale);
            var pal = a.ToPalette();
            int v = variant >= 0 ? variant : a.Variant, fem = Appearance.IsMale(v) ? 0 : 2;
            int hairId = hair >= 0 ? hair : a.Hair;
            bool bob = frame == 7 || frame == 8 || frame == 9 || frame == 14 || frame == 15 || frame == 16;
            float bobY = bob ? -2f : 0f;
            var torsoCell = frame == 5 ? new Vector2Int(1, fem) : new Vector2Int(0, fem);
            var arm = TerraPlayerSprite.frontArm[frame];
            var backArm = new Vector2Int(arm.x, arm.y + 2);
            int hairRow = Mathf.Max(0, frame - 6);
            bool longHair = Appearance.BackHair(hairId);
            var hairOff = Appearance.HairOffset(hairId, false);
            string hairSheet = Appearance.HairSheet(hairId);

            // DrawPlayer_01_BackHair, then the skin, the back arm, the legs, the coat's tails, the shoulders and torso.
            if (longHair) Part(box, scale, faceRight, hairSheet, 0, hairRow, FrameH - 2, pal.Hair, hairOff);
            Part(box, scale, faceRight, Appearance.Sheet(v, 3), torsoCell.x, torsoCell.y, FrameH, pal.Skin, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 10), 0, frame, FrameH, pal.Skin, Vector2.zero);
            Part(box, scale, faceRight, Appearance.Sheet(v, 7), backArm.x, backArm.y, FrameH, pal.Skin, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 5), backArm.x, backArm.y, FrameH, pal.Skin, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 8), backArm.x, backArm.y, FrameH, pal.UnderShirt, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 13), backArm.x, backArm.y, FrameH, pal.Shirt, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 11), 0, frame, FrameH, pal.Pants, Vector2.zero);
            Part(box, scale, faceRight, Appearance.Sheet(v, 12), 0, frame, FrameH, pal.Shoe, Vector2.zero);
            if (Appearance.HasCoat(v)) Part(box, scale, faceRight, Appearance.Sheet(v, 14), 0, frame, FrameH, pal.Shirt, Vector2.zero);
            Part(box, scale, faceRight, Appearance.Sheet(v, 4), 1, 1 + fem, FrameH, pal.UnderShirt, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 6), 1, 1 + fem, FrameH, pal.Shirt, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 4), torsoCell.x, torsoCell.y, FrameH, pal.UnderShirt, new Vector2(0f, bobY));
            Part(box, scale, faceRight, Appearance.Sheet(v, 6), torsoCell.x, torsoCell.y, FrameH, pal.Shirt, new Vector2(0f, bobY));
            // The head: skin, eye whites, eyes, the hair (only its top over the head when it is long).
            Part(box, scale, faceRight, Appearance.Sheet(v, 0), 0, frame, FrameH, pal.Skin, Vector2.zero);
            Part(box, scale, faceRight, Appearance.Sheet(v, 1), 0, frame, FrameH, Color.white, Vector2.zero);
            Part(box, scale, faceRight, Appearance.Sheet(v, 2), 0, frame, FrameH, pal.Eye, Vector2.zero);
            Part(box, scale, faceRight, hairSheet, 0, hairRow, longHair ? 26 : FrameH - 2, pal.Hair, hairOff);
            // The front arm and shoulder, whichever the frame puts on top last.
            bool shoulderOver = TerraPlayerSprite.shoulderOverFrontArm[frame];
            for (int pass = 0; pass < 2; pass++)
            {
                bool shoulder = shoulderOver ? pass == 1 : pass == 0;
                var cell = shoulder ? new Vector2Int(0, 1 + fem) : arm;
                Part(box, scale, faceRight, Appearance.Sheet(v, 7), cell.x, cell.y, FrameH, pal.Skin, new Vector2(0f, bobY));
                Part(box, scale, faceRight, Appearance.Sheet(v, 8), cell.x, cell.y, FrameH, pal.UnderShirt, new Vector2(0f, bobY));
                Part(box, scale, faceRight, Appearance.Sheet(v, 13), cell.x, cell.y, FrameH, pal.Shirt, new Vector2(0f, bobY));
                Part(box, scale, faceRight, Appearance.Sheet(v, 6), cell.x, cell.y, FrameH, pal.Shirt, new Vector2(0f, bobY));
            }
        }

        /// <summary>
        /// The top <paramref name="height"/> rows of a 40x56 cell (column, row) of a sheet, laid on the frame box, moved by
        /// <paramref name="offset"/> Terraria pixels (y down, x as facing right), mirrored when facing left.
        /// </summary>
        static void Part(Rect box, float scale, bool faceRight, string sheet, int col, int row, int height, Color tint, Vector2 offset)
        {
            if (sheet == null) return;
            Texture2D tex;
            try { tex = TerrariaAssets.Texture(sheet); }
            catch (System.Exception) { return; }
            if (tex == null) return;
            int x = col * FrameW, yTop = row * FrameH;
            if (x + FrameW > tex.width || yTop >= tex.height) return;
            int h = Mathf.Min(height, tex.height - yTop);
            var uv = new Rect((float)x / tex.width, 1f - (float)(yTop + h) / tex.height, (float)FrameW / tex.width, (float)h / tex.height);
            if (!faceRight) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            float ox = faceRight ? offset.x : -offset.x;
            var dest = new Rect(box.x + ox * scale, box.y + offset.y * scale, box.width, h * scale);
            var prev = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, prev.a);
            GUI.DrawTextureWithTexCoords(dest, tex, uv);
            GUI.color = prev;
        }

        /// <summary>
        /// The head alone (Main.DrawPlayerHead on the map, the hair style buttons): the top 28 rows of frame 0, the hair in
        /// its style, an outline round it in <paramref name="rim"/> (clear for none).
        /// </summary>
        public static void DrawHead(Rect r, Appearance a, Color rim, bool hairShows = true, int helmet = 0, int hair = -1)
        {
            if (Event.current.type != EventType.Repaint || a == null) return;
            var pal = a.ToPalette();
            if (rim.a > 0f)
            {
                float t = Mathf.Max(1f, r.width / 40f);
                string skin = Appearance.Sheet(a.Variant, 0);
                HeadPart(new Rect(r.x - t, r.y, r.width, r.height), skin, rim);
                HeadPart(new Rect(r.x + t, r.y, r.width, r.height), skin, rim);
                HeadPart(new Rect(r.x, r.y - t, r.width, r.height), skin, rim);
                HeadPart(new Rect(r.x, r.y + t, r.width, r.height), skin, rim);
            }
            HeadPart(r, Appearance.Sheet(a.Variant, 0), pal.Skin);
            HeadPart(r, Appearance.Sheet(a.Variant, 1), Color.white);
            HeadPart(r, Appearance.Sheet(a.Variant, 2), pal.Eye);
            if (hairShows) HeadPart(r, Appearance.HairSheet(hair >= 0 ? hair : a.Hair), pal.Hair);
            if (helmet > 0) HeadPart(r, "Armor_Head_" + helmet, Color.white);
        }

        /// <summary>The top 28 pixels (the head) of frame 0 of a 40-wide sheet over the rect.</summary>
        static void HeadPart(Rect r, string sheet, Color tint)
        {
            if (sheet == null) return;
            Texture2D tex;
            try { tex = TerrariaAssets.Texture(sheet); }
            catch (System.Exception) { return; }
            if (tex == null) return;
            var uv = new Rect(0f, 1f - 28f / tex.height, 40f / tex.width, 28f / tex.height);
            var prev = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, prev.a * tint.a);
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = prev;
        }
    }
}
