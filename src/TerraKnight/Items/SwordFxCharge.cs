using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A weapon fully charged (the user's 10-27 "所有武器蓄力结束后应该焕发些许光辉…需要蓄力的都加相应颜色的光辉，只需要在最外层
    /// 加粒子效果就行"): a few motes of its own colours (Terraria's dust 278, tinted) rising off the outline of its picture as it
    /// is drawn, an outermost layer over the weapon and its brightening: every right-click art held at its full charge
    /// (HeldBlade, HeldGun) and the Flint held charged. The outline is the picture's edge pixels (solid, beside a clear one),
    /// found once a picture.
    /// </summary>
    internal sealed partial class SwordFx
    {
        static readonly Dictionary<Sprite, Vector2[]> outlines = new Dictionary<Sprite, Vector2[]>();

        /// <summary>Dev: the motes made off a charged weapon's outline.</summary>
        public static int DevChargeMotes;

        /// <summary>
        /// A picture's outline: its edge pixels' middles from its pivot (its own pixels, y up); its texture's pixels if it can
        /// be read, else the item's (BladeArts.Pixels); empty when neither.
        /// </summary>
        static Vector2[] OutlineOf(Sprite s, ItemDef item)
        {
            if (s == null) return System.Array.Empty<Vector2>();
            if (outlines.TryGetValue(s, out var known)) return known;
            var r = s.rect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var solid = new bool[w, h];
            bool read = false;
            try
            {
                if (s.texture != null && s.texture.isReadable)
                {
                    var px = s.texture.GetPixels32();
                    int tw = s.texture.width;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            solid[x, y] = px[((int)r.y + y) * tw + (int)r.x + x].a >= 40;
                    read = true;
                }
                else if (item != null && BladeArts.Pixels(item.Texture, out var px, out int tw, out int th))
                {
                    // Its rows top-down: the picture's bottom row is the texture's row (th - r.y - 1) from its top.
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int row = th - 1 - ((int)r.y + y);
                            if (row >= 0 && row < th) solid[x, y] = px[row * tw + (int)r.x + x].a >= 40;
                        }
                    read = true;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[蓄力光辉] {item?.Type} 的轮廓读不出：{e.Message}");
            }
            var edge = new List<Vector2>();
            if (read)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (!solid[x, y]) continue;
                        bool rim = x == 0 || y == 0 || x == w - 1 || y == h - 1 || !solid[x - 1, y] || !solid[x + 1, y] || !solid[x, y - 1] || !solid[x, y + 1];
                        if (rim) edge.Add(new Vector2(x + 0.5f, y + 0.5f) - s.pivot);
                    }
            var list = edge.ToArray();
            outlines[s] = list;
            return list;
        }

        /// <summary>One frame's motes (none most frames: a soft shimmer) off an outline point given in the world.</summary>
        static void ChargeMote(Vector2 worldAt, Vector2 worldMiddle, Color a, Color b)
        {
            var at = TerraParticles.ToPx(worldAt);
            var mid = TerraParticles.ToPx(worldMiddle);
            var out_ = at - mid;
            out_ = out_.sqrMagnitude > 1e-4f ? out_.normalized : Vector2.up;
            // Its colours lifted toward white: a glow, even off a wooden bow or a gun's steel.
            var tint = Color.Lerp(Color.Lerp(a, b, Random.value), Color.white, Random.Range(0.3f, 0.5f));
            int m = Dust(at, 278, out_ * Random.Range(0.25f, 0.7f) + new Vector2(0f, -Random.Range(0.15f, 0.45f)), 40, Random.Range(0.65f, 0.95f), true, tint);
            FadeIn(m, 0.4f);
            DevChargeMotes++;
        }

        /// <summary>How many motes this frame: about thirty a second, whatever the frame rate.</summary>
        static int MotesNow()
        {
            float want = 30f * Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            int n = Mathf.FloorToInt(want);
            if (Random.value < want - n) n++;
            return n;
        }

        /// <summary>
        /// The outline motes of a picture drawn by FxBatch.Draw (at px, turned by the screen angle, scaled, flipped about its
        /// pivot as the batch does: one picture pixel a Terraria px).
        /// </summary>
        void ChargedOutline(Sprite s, ItemDef item, Vector2 posPx, float screen, Vector2 scale, bool flipY, Color a, Color b)
        {
            int n = MotesNow();
            if (n == 0) return;
            var edge = OutlineOf(s, item);
            if (edge.Length == 0) return;
            var origin = TerraParticles.ToWorld(posPx);
            var turn = Quaternion.Euler(0f, 0f, -screen * Mathf.Rad2Deg);
            Vector2 Place(Vector2 o)
            {
                if (flipY) o.y = -o.y;
                return origin + (Vector2)(turn * new Vector2(o.x * scale.x, o.y * scale.y)) * TerraParticles.Px;
            }
            var middle = Place(new Vector2(s.rect.width * 0.5f, s.rect.height * 0.5f) - s.pivot);
            for (int k = 0; k < n; k++) ChargeMote(Place(edge[Random.Range(0, edge.Length)]), middle, a, b);
        }

        /// <summary>
        /// The outline motes of the item in the player's hand as the player's picture holds it (the Flint charged over the
        /// head), in its colours.
        /// </summary>
        public void ChargedInHand(ItemDef item, Color a, Color b)
        {
            var sr = TerraMod.Instance?.PlayerLook?.HeldRenderer;
            if (sr == null || !sr.gameObject.activeInHierarchy || sr.sprite == null) return;
            int n = MotesNow();
            if (n == 0) return;
            var s = sr.sprite;
            var edge = OutlineOf(s, item);
            if (edge.Length == 0) return;
            var t = sr.transform;
            float ppu = s.pixelsPerUnit;
            Vector2 Place(Vector2 o) => t.TransformPoint(new Vector3((sr.flipX ? -o.x : o.x) / ppu, (sr.flipY ? -o.y : o.y) / ppu, 0f));
            var middle = Place(new Vector2(s.rect.width * 0.5f, s.rect.height * 0.5f) - s.pivot);
            for (int k = 0; k < n; k++) ChargeMote(Place(edge[Random.Range(0, edge.Length)]), middle, a, b);
        }
    }
}
