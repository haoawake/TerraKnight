using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade swings' looks (BladeArts), each use's cut drawn every frame from where its blade is: the weapon's own
    /// picture turned flat along the blade (FlatBlade) from the hand, shortened as a level cut points toward or away from the
    /// eye; behind its edge a trail traced through the blade's last positions, wide by the blade, narrowing and fading back
    /// along the way it came (a glow, a body, a bright rim), its shape by the weapon's kind (a crisp crescent for a sword, a
    /// heavy band for a club, an axe or a greatsword, a wide glowing fan for a phase blade, thin rakes for claws and knives)
    /// and its colours the sprite's; motes off the tip (and the blade's own: fire, ice, spores, shadow, glints, blood, stars).
    /// Stage by stage: afterimages of the blade, a light at the tip. A smash strikes the ground (dust both ways and a shock
    /// running along it), a dragged blade throws sparks off it, a hit throws motes the way of the blow. No flashes or glints
    /// besides (the user's 10-26 "莫名其妙的不必要的闪光", the finisher's burst "散射出去一些针").
    /// </summary>
    internal sealed partial class SwordFx
    {
        sealed class ArtCut
        {
            public ItemDef Item;
            public BladeArts.Art Art;
            public BladeArts.Move Move;
            public float Aim, Facing, Age, Duration, BladePx, Scale;
            public bool Finisher, Impacted, Finished;
            public Sprite Blade;
            /// <summary>Where the cut turns when it isn't at the player's shoulder (a thrown phase blade's), Terraria px.</summary>
            public Vector2? Remote;
        }

        sealed class Shock
        {
            public Vector2 At;
            public Color Tint;
            public float Age, Size;
        }

        readonly List<ArtCut> arts = new List<ArtCut>();
        readonly List<Shock> shocks = new List<Shock>();

        /// <summary>Dev: cuts drawn, finishers, smashes on the ground and hits flashed so far.</summary>
        public static int DevArtCuts, DevArtFinishers, DevArtImpacts, DevArtFlashes;

        /// <summary>A use of a remade weapon begins: its cut drawn for duration ticks, aimed at aim (degrees from level).</summary>
        public void ArtUse(ItemDef item, BladeArts.Art art, BladeArts.Move move, float aim, float facing, float duration, float scale, bool finisher, Vector2? remote = null)
        {
            var blade = FlatBlade(item);
            arts.Add(new ArtCut
            {
                Item = item, Art = art, Move = move, Aim = aim, Facing = facing < 0f ? -1f : 1f, Duration = Mathf.Max(3f, duration),
                Scale = scale, Blade = blade, BladePx = BladeLength(item, scale), Finisher = finisher,
                Remote = remote.HasValue ? TerraParticles.ToPx(remote.Value) : (Vector2?)null,
            });
            DevArtCuts++;
            DevLastMove = move.Name;
        }

        /// <summary>Dev: the move the last use made.</summary>
        public static string DevLastMove;

        /// <summary>Dev: the item's flat blade could be made from its picture.</summary>
        public static bool DevHasFlat(ItemDef item) => FlatBlade(item) != null;

        /// <summary>The blade's length on the screen (px) for an item, as ArtUse draws it.</summary>
        public static float BladeLength(ItemDef item, float scale)
        {
            var blade = FlatBlade(item);
            return blade != null ? blade.rect.width * scale : 40f * scale;
        }

        // ------------------------------------------------------------------ the flat blade

        static readonly Dictionary<ItemType, Sprite> flats = new Dictionary<ItemType, Sprite>();

        /// <summary>
        /// The item's picture (its first frame) turned from Terraria's diagonal (the handle at the bottom left, the point at
        /// the top right) to lie along +x, the handle at the left: each pixel taken from the source pixel under it (no
        /// blending: pixel art), the empty rows cut away; it pivots on the grip.
        /// </summary>
        /// <summary>The flat blade of an item, for the held blades' dance (TerraProjectilesHeldArts).</summary>
        internal static Sprite BladeSprite(ItemDef item) => FlatBlade(item);

        static Sprite FlatBlade(ItemDef item)
        {
            if (item == null) return null;
            if (flats.TryGetValue(item.Type, out var s)) return s;
            s = null;
            try
            {
                var held = TerraMod.ItemSprite(item.Type);
                if (held != null && BladeArts.Pixels(item.Texture, out var px, out int w, out int h))
                {
                    var r = held.rect;
                    int fx = (int)r.x, fw = (int)r.width, fh = (int)r.height, fy = h - (int)r.y - fh;
                    float len = Mathf.Sqrt(fw * fw + fh * fh);
                    int L = Mathf.CeilToInt(len), T = L;
                    var ux = new Vector2(fw / len, -fh / len);
                    var vy = new Vector2(fh / len, fw / len);
                    var src = new Vector2(0f, fh);
                    var outPx = new Color32[L * T];
                    int top = T, bottom = -1;
                    for (int oy = 0; oy < T; oy++)
                        for (int ox = 0; ox < L; ox++)
                        {
                            var p = src + ux * (ox + 0.5f) + vy * (oy + 0.5f - T * 0.5f);
                            int sx = Mathf.FloorToInt(p.x), sy = Mathf.FloorToInt(p.y);
                            if (sx < 0 || sy < 0 || sx >= fw || sy >= fh) continue;
                            var c = px[(fy + sy) * w + fx + sx];
                            if (c.a < 8) continue;
                            outPx[oy * L + ox] = c;
                            top = Mathf.Min(top, oy);
                            bottom = Mathf.Max(bottom, oy);
                        }
                    if (bottom >= top)
                    {
                        int H = bottom - top + 1;
                        var tex = new Texture2D(L, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "TerraKnight/flat_" + item.Type };
                        var rows = new Color32[L * H];
                        // Unity's rows go up from the bottom.
                        for (int y = 0; y < H; y++) System.Array.Copy(outPx, (top + y) * L, rows, (H - 1 - y) * L, L);
                        tex.SetPixels32(rows);
                        tex.Apply(false, false);
                        float gripRow = T * 0.5f - top;
                        s = Sprite.Create(tex, new Rect(0, 0, L, H), new Vector2(0f, Mathf.Clamp01(1f - gripRow / H)), 1f / Px, 0, SpriteMeshType.FullRect);
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[新挥砍] {item.Type} 的平放剑身做不出来：{e.Message}");
            }
            flats[item.Type] = s;
            return s;
        }

        static Sprite block;

        /// <summary>A white block pivoting on its left middle (the trail's pieces), made once.</summary>
        static Sprite Block
        {
            get
            {
                if (block != null) return block;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "TerraKnight/art_block" };
                var c = new Color32[16];
                for (int i = 0; i < 16; i++) c[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(c);
                tex.Apply(false, true);
                block = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                return block;
            }
        }

        static Sprite soft;

        /// <summary>
        /// A white strip pivoting on its left middle, soft across (a bell over its 16 px, a texel a px): the trail's pieces laid
        /// side by side at half its width add up to an even sheet, with no bright seam where two meet (the user's 10-26 ladder of
        /// lines across a cut). Drawn at y scale gap / 8 its bell spans twice the gap; together they come to about 1.23 times one.
        /// </summary>
        static Sprite Soft
        {
            get
            {
                if (soft != null) return soft;
                var tex = new Texture2D(4, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "TerraKnight/art_soft" };
                var c = new Color32[64];
                for (int y = 0; y < 16; y++)
                {
                    float u = (y - 7.5f) / 5.5f;
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Exp(-u * u));
                    for (int x = 0; x < 4; x++) c[y * 4 + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(c);
                tex.Apply(false, true);
                soft = Sprite.Create(tex, new Rect(0, 0, 4, 16), new Vector2(0f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                return soft;
            }
        }

        /// <summary>A piece's share of the sheet the soft pieces make together.</summary>
        const float SoftShare = 0.81f;

        // ------------------------------------------------------------------ geometry

        static Vector2 FeetPx(HeroController hero) => TerraParticles.ToPx(BladeArts.Feet(hero));

        /// <summary>The shoulder the blade turns on (the hitbox's (9, 17), Terraria px y down).</summary>
        static Vector2 ShoulderPx(Vector2 feet, float facing) => feet + new Vector2(-facing, -25f);

        /// <summary>Where a cut turns: the player's shoulder, or the thrown blade's place.</summary>
        static Vector2 Pivot(ArtCut a, Vector2 feet) => a.Remote ?? ShoulderPx(feet, a.Facing);

        /// <summary>A way (degrees from level for a player facing right, up positive) as Terraria's screen angle (radians, y down).</summary>
        static float ScreenAngle(float deg, float facing)
        {
            float r = deg * Mathf.Deg2Rad;
            return Mathf.Atan2(-Mathf.Sin(r), facing * Mathf.Cos(r));
        }

        static Vector2 Along(float screen) => new Vector2(Mathf.Cos(screen), Mathf.Sin(screen));

        static Color Glow(Color c, float k)
        {
            var g = c * k;
            g.a = 0f;
            return g;
        }

        /// <summary>The blade at a point of the use (0..1): its way, length share, hand thrust, cutting or not, and the stroke it is in.</summary>
        static float BladeAt(ArtCut a, float u, out float len, out float ext, out bool live, out int stroke)
        {
            stroke = BladeArts.StrokeAt(a.Move, Mathf.Clamp01(u), out float su);
            float floor = a.Remote.HasValue ? -90f : BladeArts.FloorAngle(TipRadius(a, a.Move.Strokes[stroke].Reach, 0f));
            return BladeArts.Blade(a.Move.Strokes[stroke], su, a.Aim, out len, out ext, out live, floor);
        }

        /// <summary>From the shoulder to the tip (px) for a blade that long (its share), the hand thrust out ext: the reach the cuts hit to.</summary>
        static float TipRadius(ArtCut a, float len, float ext) => 9f + ext + a.BladePx * len * a.Art.Reach;

        // ------------------------------------------------------------------ ticks

        void TickArts(HeroController hero)
        {
            for (int i = shocks.Count - 1; i >= 0; i--) if (++shocks[i].Age > 16f) shocks.RemoveAt(i);
            if (hero != null) TickSkills(hero);
            if (hero != null) TickMage(hero);
            if (hero != null) TickRanged(hero);
            if (arts.Count == 0) return;
            for (int i = arts.Count - 1; i >= 0; i--)
            {
                var a = arts[i];
                a.Age++;
                if (hero == null || a.Age > a.Duration + 10f) { arts.RemoveAt(i); continue; }
                if (a.Age > a.Duration)
                {
                    if (!a.Finished) Finish(a, hero);
                    continue;
                }
                float u = a.Age / a.Duration;
                float deg = BladeAt(a, u, out float len, out float ext, out bool live, out int stroke);
                var shoulder = Pivot(a, FeetPx(hero));
                float screen = ScreenAngle(deg, a.Facing);
                var dir = Along(screen);
                var tip = shoulder + dir * TipRadius(a, len, ext);
                var s = a.Move.Strokes[stroke];
                BladeArts.StrokeAt(a.Move, u, out float su);
                // A smash meets the ground as it comes down.
                if (s.Shape == BladeArts.Shape.Smash && !a.Impacted && su >= 0.6f)
                {
                    a.Impacted = true;
                    Impact(a, tip);
                }
                if (!live) continue;
                int level = a.Art.Level;
                var main = BladeArts.Tint(a.Art, a.Art.Main, 0f);
                // Motes off the edge, more stage by stage, flung the way of the swing.
                float prev = BladeAt(a, Mathf.Max(0f, u - 1f / a.Duration), out _, out _, out _, out _);
                var tang = Along(ScreenAngle(deg + (deg >= prev ? 90f : -90f), a.Facing));
                if (Random.value < 0.25f + 0.05f * level)
                {
                    var at = shoulder + dir * TipRadius(a, len, ext) * (0.6f + Random.value * 0.4f);
                    int m = Dust(at, 278, tang * (1.5f + Random.value * 2.5f), 100, 0.45f + 0.08f * level, true, Color.Lerp(main, Color.white, Random.value * 0.3f));
                    FadeIn(m, 0.4f + Random.value * 0.2f);
                }
                // The weapon's own: flame, frost, leaves, smoke, stars, honey, coins... along the blade (SwordFxThemes).
                ThemeMotes(a, shoulder, screen, TipRadius(a, len, ext), tang * (1.5f + Random.value * 1.5f), level);
                // Sparks off a blade dragged along the ground, falling back the way it came.
                if (s.Shape == BladeArts.Shape.Drag && su < 0.55f && Random.value < 0.75f)
                {
                    int m = Dust(tip, 278, new Vector2(-a.Facing * (1f + Random.value * 2.5f), -1.5f - Random.value * 2.5f), 100, 0.55f, false, new Color(1f, 0.78f, 0.4f));
                    FadeIn(m, 0.3f);
                }
                if (level >= 2) Fx?.Light(tip, main * (0.3f + 0.08f * level), 2.5f + 0.4f * level, true);
            }
        }

        /// <summary>The use's end (a combo's last cut counted; its harder knock is MeleeHits').</summary>
        void Finish(ArtCut a, HeroController hero)
        {
            a.Finished = true;
            if (a.Finisher) DevArtFinishers++;
        }

        /// <summary>A smash on (or just over) the ground: dust sprayed both ways along it, a shock running out.</summary>
        void Impact(ArtCut a, Vector2 tip)
        {
            // The ground under where the blade comes down: looked for from the shoulder's height down, so a tip a little into
            // it lands on its top.
            var world = TerraParticles.ToWorld(tip);
            var hero = HeroController.SilentInstance;
            float from = hero != null ? BladeArts.Feet(hero).y + 0.8f : world.y + 1f;
            float ground = float.NaN;
            for (float y = from; y >= from - 4f; y -= 0.1f)
                if (World.TerraWorld.IsSolidAt(new Vector2(world.x, y))) { ground = Mathf.Ceil(y / World.CellGrid.CellSize) * World.CellGrid.CellSize; break; }
            if (float.IsNaN(ground)) return;
            DevArtImpacts++;
            var at = TerraParticles.ToPx(new Vector2(world.x, ground + 0.05f));
            int level = a.Art.Level;
            var main = BladeArts.Tint(a.Art, a.Art.Main, 0f);
            var light = BladeArts.Tint(a.Art, a.Art.Light, 0.4f);
            for (int k = 0; k < 10 + 3 * level; k++)
            {
                float side = k % 2 == 0 ? 1f : -1f;
                var v = new Vector2(side * (2f + Random.value * 5f), -1f - Random.value * 2.5f);
                int m = Dust(at, 278, v, 100, 0.6f + 0.05f * level, false, Color.Lerp(main, light, Random.value * 0.5f));
                FadeIn(m, 0.5f);
            }
            if (a.Art.Mote != 0) for (int k = 0; k < 6; k++) Dust(at, a.Art.Mote, new Vector2((Random.value - 0.5f) * 8f, -2f - Random.value * 3f), 100, 1.1f, false);
            shocks.Add(new Shock { At = at, Tint = main, Size = 1f + 0.15f * level });
            // A heavy weapon's blow on the ground shakes the view a little.
            var kind = a.Art.Kind;
            if (kind == BladeArts.Kind.Great || kind == BladeArts.Kind.Blunt || kind == BladeArts.Kind.Axe) Player.TerraCamera.Punch(0.05f + 0.015f * level, 0.18f);
            Sound("Item_14", 0.2f + 0.03f * level);
        }

        /// <summary>A remade cut struck an enemy: motes in the weapon's colours flung the way of the blow (DevArtFlashes counts the hits).</summary>
        public void ArtHit(BladeArts.Art art, Vector3 world, float facing)
        {
            var at = TerraParticles.ToPx(world);
            int level = art.Level;
            var main = BladeArts.Tint(art, art.Main, 0.3f);
            var light = BladeArts.Tint(art, art.Light, 0.45f);
            ThemeHit(art, at, facing);
            for (int k = 0; k < 2 + level; k++)
            {
                var v = new Vector2(facing * (1f + Random.value * 4f), (Random.value - 0.6f) * 5f);
                int m = Dust(at, 278, v, 100, 0.5f + 0.06f * level, true, Color.Lerp(main, light, Random.value));
                FadeIn(m, 0.45f);
            }
            DevArtFlashes++;
        }

        /// <summary>The Horseman's Blade's cut struck: a flaming jack-o'-lantern called on the enemy, as its slash calls one.</summary>
        public void ArtPumpkin(HealthManager hm, int damage, float knockback) => SpawnPumpkin(hm, damage, knockback);

        // ------------------------------------------------------------------ drawing

        void DrawArts(FxBatch fx)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var feet = FeetPx(hero);
            foreach (var s in shocks) DrawShock(fx, s);
            foreach (var a in arts) DrawArt(fx, a, feet);
            DrawSkills(fx, hero, feet);
            DrawMage(fx, hero, feet);
            DrawRanged(fx, hero, feet);
        }

        /// <summary>The ground shock: soft lines of light running out along the floor both ways, thinning and fading.</summary>
        void DrawShock(FxBatch fx, Shock s)
        {
            float t = (s.Age + tickAccumulator) / 16f;
            float k = 1f - t;
            if (k <= 0f) return;
            float reach = 18f + 70f * s.Size * (1f - (1f - t) * (1f - t));
            for (int side = -1; side <= 1; side += 2)
            {
                var from = s.At + new Vector2(side * reach * 0.1f, 0f);
                var to = s.At + new Vector2(side * reach, 0f);
                Streak(fx, from, to, s.Tint, 6f + 8f * k, 0.6f * k);
                Streak(fx, from, Vector2.Lerp(from, to, 0.85f), Color.white, 2f + 2f * k, 0.6f * k);
            }
        }

        /// <summary>The trail's look by the weapon's kind: how far back it reaches (a share of the use), how close to the shoulder its inner edge comes at its newest and oldest, how much of a dark body it has.</summary>
        static void TrailStyle(BladeArts.Art art, out float span, out float innerNew, out float innerOld, out float body)
        {
            switch (art.Kind)
            {
                case BladeArts.Kind.Light: span = 0.42f; innerNew = 0.42f; innerOld = 0.7f; body = 0f; break;
                case BladeArts.Kind.Great: case BladeArts.Kind.Blunt: case BladeArts.Kind.Axe: span = 0.34f; innerNew = 0.5f; innerOld = 0.8f; body = 0.42f; break;
                case BladeArts.Kind.Claw: case BladeArts.Kind.Knife: case BladeArts.Kind.Scissors: span = 0.22f; innerNew = 0.84f; innerOld = 0.95f; body = 0.15f; break;
                case BladeArts.Kind.Sickle: span = 0.38f; innerNew = 0.66f; innerOld = 0.9f; body = 0.25f; break;
                case BladeArts.Kind.Katana: span = 0.3f; innerNew = 0.78f; innerOld = 0.96f; body = 0.2f; break;
                default: span = 0.32f; innerNew = 0.62f; innerOld = 0.9f; body = 0.28f; break;
            }
            span *= 1f + 0.08f * art.Level;
        }

        void DrawArt(FxBatch fx, ArtCut a, Vector2 feet)
        {
            float age = a.Age + tickAccumulator;
            float u = Mathf.Clamp01(age / a.Duration);
            float fade = age <= a.Duration ? 1f : Mathf.Clamp01(1f - (age - a.Duration) / 10f);
            if (fade <= 0f) return;
            int level = a.Art.Level;
            var shoulder = Pivot(a, feet);
            var main = BladeArts.Tint(a.Art, a.Art.Main, 0f);
            var light = BladeArts.Tint(a.Art, a.Art.Light, 0.15f);
            var dark = BladeArts.Tint(a.Art, a.Art.Dark, 0.3f);
            TrailStyle(a.Art, out float span, out float innerNew, out float innerOld, out float body);
            // The trail: samples back along the use, each a piece from the inner edge to the tip, as wide as the way to the
            // next; only between samples of one stroke, while it cuts.
            // Fine enough that the pieces' ends at the tip make a smooth curve, not steps (the user's 10-26 "一大个矩形").
            int n = 40 + 6 * level;
            float stepU = span / n;
            float lastA = 0f, lastR = 0f;
            int lastStroke = -1;
            bool lastLive = false;
            for (int k = n; k >= 0; k--)
            {
                float uk = u - k * stepU;
                if (uk < 0f) { lastStroke = -1; continue; }
                float deg = BladeAt(a, uk, out float len, out float ext, out bool live, out int stroke);
                float screen = ScreenAngle(deg, a.Facing);
                float r = TipRadius(a, len, ext);
                if (live && lastLive && stroke == lastStroke)
                {
                    float f = 1f - k / (float)(n + 1);
                    float gap = Mathf.Abs(Mathf.DeltaAngle(screen * Mathf.Rad2Deg, lastA * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r + Mathf.Abs(r - lastR) + 1.5f;
                    // Where the blade turns slowly (its wind-up, the end of its stroke) the pieces lie on each other and their glows
                    // added made a bright blot (the user's 10-26 "开始和结尾的挥舞位置格外亮眼"): each as bright as it is apart.
                    float o = fade * f * Mathf.Clamp01((gap - 1.5f) / 3f);
                    if (o <= 0.01f) { lastA = screen; lastR = r; lastStroke = stroke; lastLive = live; continue; }
                    float inner = r * Mathf.Lerp(innerOld, innerNew, f);
                    var c = Color.Lerp(main, light, f * f);
                    float wide = 1f;
                    c = ThemeTrail(a, k, f, c, ref wide, ref inner);
                    float l = (r - inner) * wide;
                    inner = r - l;
                    var at = shoulder + Along(screen) * inner;
                    // A soft glow wider than the trail, its body, and the bright rim along its outer edge, drawn from the last
                    // sample's tip to this one's so it runs on as one curve.
                    fx.Draw(Soft, shoulder + Along(screen) * (inner - 3f), Glow(c, 0.22f * o * SoftShare), screen, new Vector2((l + 6f) / 4f, (gap + 3f) / 8f), overPlayer: true);
                    if (body > 0f) fx.Draw(Soft, at, dark * (body * o * 0.8f * SoftShare), screen, new Vector2(l / 4f, gap / 8f), overPlayer: true);
                    fx.Draw(Soft, at, Glow(c, (a.Art.Kind == BladeArts.Kind.Light ? 0.75f : 0.55f) * o * SoftShare), screen, new Vector2(l / 4f, gap / 8f), overPlayer: true);
                    float rim = Mathf.Max(2f, l * 0.14f);
                    var r0 = shoulder + Along(lastA) * (lastR - rim * 0.5f);
                    var r1 = shoulder + Along(screen) * (r - rim * 0.5f);
                    var run = r1 - r0;
                    float runLen = run.magnitude;
                    if (runLen > 0.2f)
                        fx.Draw(Block, r0, Glow(Color.Lerp(light, Color.white, 0.5f), 0.9f * fade * f * Mathf.Clamp01(runLen / 1.5f)), Mathf.Atan2(run.y, run.x), new Vector2((runLen + 0.3f) / 4f, rim / 4f), overPlayer: true);
                }
                lastA = screen;
                lastR = r;
                lastStroke = stroke;
                lastLive = live;
            }
            if (age > a.Duration || a.Blade == null) return;
            // Afterimages of the blade a little back along its way, then the blade itself from the hand.
            float bdeg = BladeAt(a, u, out float blen, out float bext, out bool blive, out int bstroke);
            if (level >= 2 && blive)
                for (int k = Mathf.Min(3, level - 1); k >= 1; k--)
                {
                    float uk = u - k * 0.06f;
                    if (uk < 0f) continue;
                    float d = BladeAt(a, uk, out float l2, out float e2, out bool live2, out int s2);
                    if (!live2 || s2 != bstroke) continue;
                    // An afterimage as clear as it is apart from the blade (none piled on it where it turns slowly).
                    float apart = Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(d, bdeg)) / 18f);
                    if (apart > 0.05f) DrawBlade(fx, a, shoulder, d, l2, e2, Glow(light, 0.38f / k * apart));
                }
            DrawBlade(fx, a, shoulder, bdeg, blen, bext, new Color(L, L, L, 1f));
        }

        /// <summary>The flat blade from the hand along a way, shortened to a share (a level cut), mirrored for the left.</summary>
        void DrawBlade(FxBatch fx, ArtCut a, Vector2 shoulder, float deg, float len, float ext, Color c)
        {
            float screen = ScreenAngle(deg, a.Facing);
            var hand = shoulder + Along(screen) * (9f + ext);
            // Drawn larger as its reach is (half of what it gains), the trail going on to the rest.
            float grow = 1f + 0.5f * (a.Art.Reach - 1f);
            fx.Draw(a.Blade, hand, c, screen, new Vector2(a.Scale * grow * Mathf.Clamp(len, 0.25f, 1.2f), a.Scale * grow), flipY: a.Facing < 0f, overPlayer: true);
        }

        /// <summary>The blade's way now for the player's pose (degrees from level), false when none of the remade swings is drawn.</summary>
        public bool ArtAngle(out float deg)
        {
            deg = 0f;
            if (arts.Count == 0) return false;
            var a = arts[arts.Count - 1];
            float age = a.Age + tickAccumulator;
            if (age > a.Duration) return false;
            deg = BladeAt(a, Mathf.Clamp01(age / a.Duration), out _, out _, out _, out _);
            return true;
        }
    }
}
