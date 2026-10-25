using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The stations Terraria animates, animated as it does. Main.AnimateTiles moves each animated tile's frame on by a
    /// counter of its own every tick, every tile of the kind in step, and GetTileDrawData draws the picture that many
    /// frames down its sheet; DrawSingleTile_Flames draws a flame sheet's same frame over a few of them, added to what is
    /// behind (the campfire, the Bone Welder, the Glass Kiln, the Lihzahrd Furnace, the Ice Machine). The Ancient
    /// Manipulator's glow fades from one colour into the next, and the Void Vault's lens turns over its pedestal
    /// (DrawVoidLenses). As the tiles are drawn they throw off dust (DrawTiles_EmitParticles): the campfire's smoke, the
    /// furnaces' sparks. All of it moves only while the station is on screen.
    /// </summary>
    internal sealed partial class Stations
    {
        /// <summary>Game ticks counted (Main.AnimateTiles runs once a tick), and the tick the stations were last drawn at.</summary>
        static int animTicks, drawnTick;

        /// <summary>
        /// The tiles' render target is drawn again every fourth tick (LightingEngine.ProcessArea's four passes, Main.RenderToTargets
        /// on renderCount 3: Main.cs:58212), and DrawTiles_EmitParticles runs for a tile as it is drawn.
        /// </summary>
        const int RedrawTicks = 4;

        /// <summary>
        /// Main.AnimateTiles (Main.cs:18741-19528): how many frames, how many ticks each; GetTileDrawData's addFrY
        /// (TileDrawing.cs:4573, 5378-5629, 6132-6143): how far down the sheet each frame is. The flame sheets are
        /// DrawSingleTile_Flames' (TileDrawing.cs:2729-2848), drawn at (255, 255, 255, 0) for the campfire and (200, 200, 200, 0)
        /// for the others.
        /// </summary>
        static void AddAnimations()
        {
            // 215 (Main.cs:18844): a frame every 4 ticks, 8 of them; lit, addFrY = tileFrame × 36; Flames[15] over it.
            Animated(StationKind.Campfire, 8, 4, 36, "Flame_15", 1f);
            // 17, 77 (18785, 18814): 6 ticks, 12 frames; 133 (18795): 4 ticks, 6 frames; the default addFrY, × 38.
            Animated(StationKind.Furnace, 12, 6, 38);
            Animated(StationKind.Hellforge, 12, 6, 38);
            Animated(StationKind.AdamantiteForge, 6, 4, 38);
            Animated(StationKind.TitaniumForge, 6, 4, 38);
            // 106 (18824): 5 ticks, 2 frames, × 54.
            Animated(StationKind.Sawmill, 2, 5, 54);
            // 96 (18974) steps too, 4 frames × 36 down Tiles_96, but only the Cauldron's (style 1) bubble: the Cooking Pot's
            // four are alike, so it stays still.
            // 217, 218 (18864, 18874): 5 ticks, 5 and 2 frames, × 36; 220 (18904): 5 ticks, 4 frames, × 54.
            Animated(StationKind.BlendOMatic, 5, 5, 36);
            Animated(StationKind.MeatGrinder, 2, 5, 36);
            Animated(StationKind.Solidifier, 4, 5, 54);
            // 228 (19278): 5 ticks, 3 frames; 243, 247 (18944, 18964): 5 ticks, 6 and 8 frames; all × 54.
            Animated(StationKind.DyeVat, 3, 5, 54);
            Animated(StationKind.ImbuingStation, 6, 5, 54);
            Animated(StationKind.Autohammer, 8, 5, 54);
            // 300-308 (19027-19105): 5 ticks; 7, 8, 4, 5 frames, the Living Loom (304) still, 12, 12, 2, 8; × 54. Flames[9] to
            // [12] over the Bone Welder, the Glass Kiln, the Lihzahrd Furnace and the Ice Machine.
            Animated(StationKind.BoneWelder, 7, 5, 54, "Flame_9", 200f / 255f);
            Animated(StationKind.FleshCloningVat, 8, 5, 54);
            Animated(StationKind.GlassKiln, 4, 5, 54, "Flame_10", 200f / 255f);
            Animated(StationKind.LihzahrdFurnace, 5, 5, 54, "Flame_11", 200f / 255f);
            Animated(StationKind.SkyMill, 12, 5, 54);
            Animated(StationKind.IceMachine, 12, 5, 54, "Flame_12", 200f / 255f);
            Animated(StationKind.SteampunkBoiler, 2, 5, 54);
            Animated(StationKind.HoneyDispenser, 8, 5, 54);
            // 354 (19239): 5 ticks, 8 frames, × 54; the Alchemy Table (355) shows the Bewitching Table's frame.
            Animated(StationKind.BewitchingTable, 8, 5, 54);
            Animated(StationKind.AlchemyTable, 8, 5, 54);
            // 377 (19250): 5 ticks, 4 frames, × 38; 463, 464 (19432, 19440): 10 ticks, 6 frames and 5 ticks, 23 frames, × 72.
            Animated(StationKind.SharpeningStation, 4, 5, 38);
            Animated(StationKind.DefendersForge, 6, 10, 72);
            Animated(StationKind.WarTable, 23, 5, 72);
            // 491: drawn from the pedestal 54 px across Tiles_491 (GetTileDrawData's addFrX, TileDrawing.cs:5405), the lens apart.
            specs[StationKind.VoidVault].PlacedX = 54;
        }

        static void Animated(StationKind kind, int frames, int ticks, int step, string flame = null, float strength = 0f)
        {
            var spec = specs[kind];
            spec.Frames = frames;
            spec.FrameTicks = ticks;
            spec.FrameStep = step;
            spec.Flame = flame;
            spec.FlameStrength = strength;
        }

        /// <summary>Main.tileFrame of the kind's tile now: every one of a kind shows the same frame.</summary>
        static int FrameOf(Spec spec) => spec.Frames > 1 ? animTicks / spec.FrameTicks % spec.Frames : 0;

        /// <summary>
        /// A station's tiles cut from a sheet laid out as its tile's (Terraria's tile sheet, its outline, its flame or glow
        /// sheet) and put together, 16 px a cell, rows of Spec.Heights; frame after frame, each step px further down the
        /// sheet, one under the other in a single texture (frame 0 on top).
        /// </summary>
        static Color32[] ComposePixels(Color32[] src, int sw, int sh, Spec spec, int x0, int frames, int step, out int w, out int h)
        {
            w = spec.W * 16;
            h = 0;
            foreach (var th in spec.Heights) h += th;
            int all = h * frames;
            var dst = new Color32[w * all];
            for (int f = 0; f < frames; f++)
            {
                int sy0 = f * step, dy0 = f * h;
                for (int r = 0; r < spec.H; r++)
                {
                    int th = spec.Heights[Mathf.Min(r, spec.Heights.Length - 1)];
                    for (int col = 0; col < spec.W; col++)
                        for (int y = 0; y < th; y++)
                            for (int x = 0; x < 16; x++)
                            {
                                int sx = x0 + col * 18 + x, sy = sy0 + y;
                                int dx = col * 16 + x, dy = dy0 + y;
                                if (sx >= sw || sy >= sh) continue;
                                dst[(all - 1 - dy) * w + dx] = src[(sh - 1 - sy) * sw + sx];
                            }
                    sy0 += th + 2;
                    dy0 += th;
                }
            }
            return dst;
        }

        /// <summary>The frames of <see cref="ComposePixels"/> as sprites, each standing on its bottom middle.</summary>
        static Sprite[] FrameSprites(Color32[] src, int sw, int sh, Spec spec, int x0, int frames, int step, string name)
        {
            var px = ComposePixels(src, sw, sh, spec, x0, frames, step, out int w, out int h);
            var tex = TerrariaAssets.Make(name, px, w, h * frames);
            var list = new Sprite[frames];
            for (int f = 0; f < frames; f++)
                list[f] = Sprite.Create(tex, new Rect(0, (frames - 1 - f) * h, w, h), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            return list;
        }

        static Sprite[] FrameSprites(string sheet, Spec spec, int x0, int frames, int step, string tag)
        {
            var src = TerrariaAssets.Pixels(sheet, out int sw, out int sh);
            return FrameSprites(src, sw, sh, spec, x0, frames, step, sheet + tag);
        }

        // ------------------------------------------------------------------ what is drawn over them

        /// <summary>VoidLensHelper.DrawToDrawData's copies of the lens drawn round it (num 0, 0.34, 0.68).</summary>
        const int LensCopies = 3;

        static readonly Dictionary<int, Sprite[]> flameSprites = new Dictionary<int, Sprite[]>();
        static Sprite[] glowSprites, lensSprites;

        /// <summary>
        /// What Terraria draws over the station's tile now, on children of its picture: its flame sheet's frame, the
        /// Ancient Manipulator's glow, the Void Vault's lens; and its dust when the tiles are drawn again
        /// (<paramref name="redraw"/>). Off screen it is left as it was.
        /// </summary>
        static void Animate(Station s, float sink, bool redraw)
        {
            var spec = s.Spec;
            bool lens = s.Kind == StationKind.VoidVault, glow = s.Kind == StationKind.AncientManipulator;
            bool seen = s.Renderer.isVisible;
            if (redraw && seen) Emit(s);
            if (spec.Flame == null && !lens && !glow) return;
            if (s.Fx == null || s.Fx[0] == null) s.Fx = MakeFx(s, lens, glow);
            if (!seen && s.Fx[0].sprite != null) return;
            if (spec.Flame != null)
            {
                s.Fx[0].sprite = FlameFrames(s.Kind, spec)[FrameOf(spec)];
            }
            else if (glow)
            {
                // Main.tileFrame[412] counts 0..239 a tick at a time (Main.cs:19339); its glow (GlowMask 202, 54 px a colour
                // down the sheet) is frame tileFrame / 60 fading out while the next fades in (TileDrawing.cs:1363-1373).
                if (glowSprites == null || glowSprites[0] == null) glowSprites = FrameSprites("Glow_202", spec, spec.StyleX, 4, 54, "#glow");
                int t = animTicks % 240, n = t / 60;
                float k = t % 60 / 60f;
                s.Fx[0].sprite = glowSprites[n];
                s.Fx[0].color = new Color(1f, 1f, 1f, 1f - k);
                s.Fx[1].sprite = glowSprites[(n + 1) % 4];
                s.Fx[1].color = new Color(1f, 1f, 1f, k);
            }
            else
            {
                Lens(s, sink);
            }
        }

        /// <summary>The children an animated station's overlays are drawn on: just in front of its picture, behind what Terraria draws over its tiles.</summary>
        static SpriteRenderer[] MakeFx(Station s, bool lens, bool glow)
        {
            int n = lens ? 1 + LensCopies : glow ? 2 : 1;
            var fx = new SpriteRenderer[n];
            for (int k = 0; k < n; k++)
            {
                var go = new GameObject("Terra Station Fx " + k);
                go.transform.SetParent(s.Renderer.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.00001f * (k + 1));
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerID = s.Renderer.sortingLayerID;
                sr.sortingOrder = s.Renderer.sortingOrder;
                // A flame sheet and the lens's copies are added to what is behind (drawn in a colour with alpha 0).
                if (s.Spec.Flame != null || (lens && k > 0)) sr.sharedMaterial = RegionalTorchFlames.AdditiveMaterial();
                if (lens && k > 0) go.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
                fx[k] = sr;
            }
            if (s.Spec.Flame != null) fx[0].color = RegionalTorchFlames.Added(s.Spec.FlameStrength, s.Spec.FlameStrength, s.Spec.FlameStrength);
            return fx;
        }

        static Sprite[] FlameFrames(StationKind kind, Spec spec)
        {
            if (flameSprites.TryGetValue((int)kind, out var frames) && frames[0] != null) return frames;
            frames = FrameSprites(spec.Flame, spec, spec.StyleX, spec.Frames, spec.FrameStep, "#flame" + spec.StyleX);
            flameSprites[(int)kind] = frames;
            return frames;
        }

        /// <summary>
        /// VoidLensHelper (DrawVoidLenses): Projectile_734's eight frames, (Main.tileFrameCounter[491], 0..39 a tick at a time,
        /// + the lens's X + Y) % 40 / 5, centred on the middle tile 2 px up, half lit (here at full); three copies 1.1 × as
        /// big, (127, 50, 127, 0) × (x × 0.1 + 0.3) added, going round it (x + 2) px out, x = cos(time / 5 turns).
        /// </summary>
        static void Lens(Station s, float sink)
        {
            if (lensSprites == null || lensSprites[0] == null)
            {
                lensSprites = new Sprite[8];
                for (int f = 0; f < 8; f++) lensSprites[f] = TerrariaAssets.Sprite("Projectile_734", 0, f * 48, 34, 48, 1f / Px, new Vector2(0.5f, 0.5f));
            }
            // The middle tile's middle (24 px over the ground) and 2 px higher, over the picture that stands sink px down.
            var centre = new Vector3(0f, (26f + sink) * Px, 0f);
            var lens = s.Fx[0];
            lens.transform.localPosition = centre + new Vector3(0f, 0f, -0.00001f);
            var px = TerraParticles.ToPx(lens.transform.position);
            int frame = (int)Mathf.Repeat(animTicks % 40 + px.x + px.y, 40f) / 5;
            lens.sprite = lensSprites[Mathf.Clamp(frame, 0, 7)];
            float time = Time.time / 5f;
            float x = Mathf.Cos(time * 2f * Mathf.PI), strength = x * 0.1f + 0.3f;
            var tint = RegionalTorchFlames.Added(127f / 255f * strength, 50f / 255f * strength, 127f / 255f * strength);
            for (int k = 0; k < LensCopies; k++)
            {
                var copy = s.Fx[1 + k];
                float a = (time + k * 0.34f) * 2f * Mathf.PI, r = (x + 2f) * Px;
                // Terraria's y runs down the screen.
                copy.transform.localPosition = centre + new Vector3(Mathf.Cos(a) * r, -Mathf.Sin(a) * r, -0.00001f * (k + 2));
                copy.sprite = lens.sprite;
                copy.color = tint;
            }
        }

        // ------------------------------------------------------------------ dust

        /// <summary>
        /// DrawTiles_EmitParticles as the station's tiles are drawn: the lit campfire's smoke off each of its top tiles, 1 in
        /// 3 (TileDrawing.cs:6901); a Furnace's, Hellforge's or forge's spark off its middle tile, 1 in 40 (7490).
        /// </summary>
        static void Emit(Station s)
        {
            var kind = s.Kind;
            bool furnace = kind == StationKind.Furnace || kind == StationKind.Hellforge || kind == StationKind.AdamantiteForge || kind == StationKind.TitaniumForge;
            if (kind != StationKind.Campfire && !furnace) return;
            var p = TerraParticles.Instance;
            if (p == null) return;
            var box = s.Box;
            var top = TerraParticles.ToPx(new Vector2(box.xMin, box.yMax));
            if (kind == StationKind.Campfire)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (Random.Range(0, 3) != 0) continue;
                    int d = TileDust(top + new Vector2(col * 16 + 2, -4f), 4, 8, 31, 100);
                    if (d < 0) continue;
                    ref var dust = ref p[d];
                    if (col == 0) dust.Position.x += Random.Range(0, 8);
                    if (col == 2) dust.Position.x -= Random.Range(0, 8);
                    dust.Alpha += Random.Range(0, 100);
                    dust.Velocity *= 0.2f;
                    dust.Velocity.y -= 0.5f + Random.Range(0, 10) * 0.1f;
                    dust.FadeIn = 0.5f + Random.Range(0, 10) * 0.1f;
                    DevSmoke++;
                }
                return;
            }
            if (Random.Range(0, 40) != 0) return;
            // The middle tile's corner (18, 18 in its frame: one tile across, one down), 4 px left and 6 up.
            int spark = TileDust(top + new Vector2(16f - 4f, 16f - 6f), 8, 6, 6, 100);
            if (spark >= 0 && Random.Range(0, 3) != 0) p[spark].NoGravity = true;
        }

        /// <summary>
        /// Dust a placed tile throws off as it is drawn (Dust.NewDust, Terraria pixels): every game drawing the tile makes
        /// its own, so none of it goes to the other players (Net.NetFx). -1 when there is none to make.
        /// </summary>
        internal static int TileDust(Vector2 topLeft, int width, int height, int type, int alpha)
        {
            var p = TerraParticles.Instance;
            if (p == null) return -1;
            bool was = Net.NetFx.Applying;
            Net.NetFx.Applying = true;
            try { return p.NewDust(topLeft, width, height, type, 0f, 0f, alpha); }
            finally { Net.NetFx.Applying = was; }
        }

        // ------------------------------------------------------------------ dev

        public static int DevSmoke;

        /// <summary>Dev: the frame the kind shows now (-1 for none of that kind).</summary>
        public static int DevFrame(StationKind kind) => specs.TryGetValue(kind, out var spec) ? FrameOf(spec) : -1;

        /// <summary>Dev: the last loaded station of the kind: its picture, and what is drawn over it (null for none).</summary>
        public static SpriteRenderer DevRenderer(StationKind kind, out SpriteRenderer[] fx)
        {
            fx = null;
            for (int i = stations.Count - 1; i >= 0; i--)
                if (stations[i].Kind == kind && stations[i].Region != null)
                {
                    fx = stations[i].Fx;
                    return stations[i].Renderer;
                }
            return null;
        }

        /// <summary>Dev: the stations set down after the first <paramref name="count"/> taken away again.</summary>
        public static void DevTrim(int count)
        {
            for (int i = stations.Count - 1; i >= count && i >= 0; i--) Remove(i);
        }

        /// <summary>
        /// Dev: every animated kind's frames checked against its sheet: the last frame inside the sheet, no frame empty, the
        /// frames not all alike; the flame sheets as well. One line a kind, false in <paramref name="ok"/> for any fault.
        /// </summary>
        public static string DevAnimAudit(out bool ok)
        {
            ok = true;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in specs)
            {
                var spec = kv.Value;
                if (spec.Frames <= 1) continue;
                foreach (var sheet in spec.Flame != null ? new[] { spec.Texture, spec.Flame } : new[] { spec.Texture })
                {
                    var src = TerrariaAssets.Pixels(sheet, out int sw, out int sh);
                    int span = -2;
                    foreach (var th in spec.Heights) span += th + 2;
                    int end = (spec.Frames - 1) * spec.FrameStep + span;
                    var px = ComposePixels(src, sw, sh, spec, spec.StyleX, spec.Frames, spec.FrameStep, out int w, out int h);
                    int empty = 0, distinct = 1;
                    for (int f = 0; f < spec.Frames; f++)
                    {
                        int solid = 0;
                        bool same = f > 0;
                        int a0 = (spec.Frames - 1 - f) * h * w, b0 = (spec.Frames - f) * h * w;
                        for (int i = 0; i < h * w; i++)
                        {
                            Color32 c = px[a0 + i], d = same ? px[b0 + i] : default;
                            if (c.a > 0) solid++;
                            if (same && (c.r != d.r || c.g != d.g || c.b != d.b || c.a != d.a)) same = false;
                        }
                        if (solid == 0) empty++;
                        if (f > 0 && !same) distinct++;
                    }
                    bool good = end <= sh && empty == 0 && distinct > 1;
                    ok &= good;
                    sb.Append($"{kv.Key}/{sheet} {spec.Frames} 帧×{spec.FrameTicks} tick 步 {spec.FrameStep}：末帧到 {end}/{sh}，空帧 {empty}，相邻不同 {distinct - 1}{(good ? "" : " ←")}；");
                }
            }
            return sb.ToString();
        }
    }
}
