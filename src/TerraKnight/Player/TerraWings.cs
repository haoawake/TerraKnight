using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>How a pair of wings animates (Player.WingFrame's per-slot branches, accarmor-hm sec-G1 G1.1.8).</summary>
    internal enum WingAnim : byte { Generic, Jetpack, Steampunk, Beetle, Board, Clock, Vortex, Six, Grox, Empress, Starboard, Cycle11, Kazzy, Luna, Chippy, Arkhalis, Ghostar, Still }

    /// <summary>
    /// One wing slot (wingSlot N, texture Wings_N): WingStats (flight ticks, air speed and
    /// acceleration, the down-hover's speed), WingMovement's constants and rules, and the draw.
    /// </summary>
    internal sealed class WingDef
    {
        public int Slot, FlyTime;
        public float Speed, Accel = 1f, HoverSpeed, HoverAccel;
        /// <summary>WingMovement: num (lift a tick), num2 (more while falling), num3 (cap × jump speed), num4/num5 (more below num4 × jump speed).</summary>
        public float Num = 0.1f, Num2 = 0.5f, Num3 = 1.5f, Num4 = 0.5f, Num5 = 0.1f;
        /// <summary>Down + jump flies even from a jump's lift or the ground; damping while hovering down (0 none); half the time hovering still; the jetpack's and the Empress / Starboard's UP boosts.</summary>
        public bool HoverStart, HalfCost, JetUp, UpBoost;
        public float HoverDamp, HoverDamp2;
        /// <summary>The frame whose flap plays Item_32 (−1 silent), the glide's frame, ticks a frame, frames on the sheet.</summary>
        public int FlapFrame = 3, GlideFrame = 2, Ticks = 5, Frames = 4, AirFrame = 1;
        public WingAnim Anim;
        /// <summary>Where the frame's centre is from the hitbox centre (facing right, px y down).</summary>
        public Vector2 Off = new Vector2(-9f, 2f);
        /// <summary>Drawn full-bright (250,250,250,A), its alpha (lit ones: their opacity), only in the air, the player standing on it.</summary>
        public bool Bright, OnlyInAir, Standing, Invisible;
        public float Alpha = 1f;
        /// <summary>A glowmask over it (same frame) and its colour (Terraria bytes /255, premultiplied), a second copy's scale.</summary>
        public string Glow;
        public Color GlowColor;
        public float GlowScale = 1f;
    }

    /// <summary>
    /// Every pair of wings Terraria has (WingStatsInitializer, Player.WingMovement / WingFrame /
    /// WingAirVisuals, PlayerDrawLayers.DrawPlayer_09_Wings; sec-G1 G1.1): their numbers for
    /// <see cref="TerraMovement"/>, their frame each tick, and their draw behind the player.
    /// </summary>
    internal static class TerraWings
    {
        static readonly Dictionary<int, WingDef> defs = Build();

        public static WingDef Get(int slot) => slot > 0 && defs.TryGetValue(slot, out var d) ? d : null;

        static Dictionary<int, WingDef> Build()
        {
            var d = new Dictionary<int, WingDef>();
            WingDef W(int slot, int fly, float speed, float accel = 1f)
            {
                var w = new WingDef { Slot = slot, FlyTime = fly, Speed = speed, Accel = accel };
                d[slot] = w;
                return w;
            }
            // WSI:28-61 (G1.1.1); the thrust families (G1.1.3); the draws (G1.1.9).
            W(46, 25, 3f);
            W(1, 100, 6.25f);
            W(2, 100, 6.25f);
            foreach (int s in new[] { 25, 7 }) W(s, 130, 6.75f);
            var fairy = W(6, 130, 6.75f); fairy.Alpha = 160f / 255f * 0.9f;
            var frozen = W(10, 130, 6.75f); frozen.Bright = true; frozen.Alpha = 175f / 255f;
            var jet = W(4, 150, 6.5f); jet.JetUp = true; jet.Anim = WingAnim.Jetpack; jet.FlapFrame = -1;
            var butterfly = W(5, 160, 7.5f); butterfly.Off = new Vector2(-5f, -2f);
            foreach (int s in new[] { 14, 13, 15 }) W(s, 160, 7.5f);
            var flame = W(9, 160, 7.5f); flame.Bright = true; flame.Alpha = 100f / 255f;
            foreach (int s in new[] { 8, 24 }) W(s, 170, 7.5f).Num3 = 1.66f;
            d[24].Anim = WingAnim.Beetle;
            d[24].FlapFrame = -1;
            var ghost = W(11, 170, 7.5f); ghost.Num3 = 1.66f;
            var mothron = W(27, 170, 7.5f); mothron.Num3 = 1.66f; mothron.Off = new Vector2(-6f, 2f); mothron.Glow = "Glow_92"; mothron.GlowColor = FxBatch.C(255, 255, 255, 127);
            var board = W(22, 170, 6.5f); board.HoverSpeed = 10f; board.HoverAccel = 10f; board.Num3 = 1.66f; board.HoverStart = true; board.HoverDamp = 0.9f; board.HalfCost = true;
            board.Frames = 7; board.Off = new Vector2(-9f, 26f); board.OnlyInAir = true; board.Standing = true; board.Anim = WingAnim.Board; board.FlapFrame = -1;
            foreach (int s in new[] { 21, 20 }) W(s, 180, 7.5f).Num3 = 1.805f;
            var steam = W(12, 180, 7.5f); steam.Num3 = 1.805f; steam.Off = new Vector2(-10f, 1f); steam.Anim = WingAnim.Steampunk; steam.GlideFrame = 3;
            var festive = W(23, 180, 7.5f); festive.Num3 = 1.805f; festive.Glow = "Flame_8"; festive.GlowColor = FxBatch.C(200, 200, 200, 200);
            var fishron = W(26, 180, 8f, 2f); fishron.Num = 0.125f; fishron.Num2 = 0.75f; fishron.Num3 = 2.5f; fishron.Num4 = 1f; fishron.Num5 = 0.15f;
            var betsy = W(37, 150, 7f, 2.5f); betsy.HoverSpeed = 12f; betsy.HoverAccel = 12f; betsy.Num = 0.125f; betsy.Num2 = 0.75f; betsy.Num3 = 2.5f; betsy.Num4 = 1f; betsy.Num5 = 0.15f;
            betsy.HoverStart = true; betsy.HoverDamp = 0.9f; betsy.HoverDamp2 = 0.92f; betsy.HalfCost = true;
            var empress = W(44, 150, 8f, 2f); empress.Num = 0.125f; empress.Num2 = 0.85f; empress.Num3 = 2.75f; empress.Num4 = 1f; empress.Num5 = 0.15f; empress.UpBoost = true;
            empress.Frames = 7; empress.Anim = WingAnim.Empress; empress.FlapFrame = 4;
            var star = W(45, 180, 8f, 4.5f); star.HoverSpeed = 16f; star.HoverAccel = 16f; star.Num = 0.1f; star.Num2 = 0.95f; star.Num3 = 4.5f; star.Num4 = 1f; star.Num5 = 0.15f;
            star.UpBoost = true; star.HoverStart = true; star.HoverDamp = 0.8f; star.HalfCost = true; star.Frames = 6; star.Off = new Vector2(0f, 22f); star.OnlyInAir = true; star.Standing = true;
            star.Bright = true; star.Alpha = 1f; star.Anim = WingAnim.Starboard; star.FlapFrame = -1;
            var solar = W(29, 180, 9f, 2.5f); solar.Num = 0.135f; solar.Num2 = 0.85f; solar.Num3 = 3f; solar.Num4 = 1f; solar.Num5 = 0.15f; solar.HoverStart = true;
            solar.Bright = true; solar.Alpha = 100f / 255f; solar.Glow = "Wings_29"; solar.GlowColor = new Color(0.5f, 0.5f, 0.5f, 0f); solar.GlowScale = 1.06f;
            var stardust = W(32, 180, 9f, 2.5f); stardust.Num = 0.135f; stardust.Num2 = 0.85f; stardust.Num3 = 3f; stardust.Num4 = 1f; stardust.Num5 = 0.15f; stardust.HoverStart = true;
            stardust.Alpha = 127f / 255f; stardust.Glow = "Glow_183"; stardust.GlowColor = new Color(1f, 1f, 1f, 0f); stardust.GlowScale = 1.06f; stardust.Ticks = 4; stardust.AirFrame = 3;
            foreach (int s in new[] { 30, 31 })
            {
                var w = W(s, 180, 6.5f, 1.5f);
                w.HoverSpeed = 12f; w.HoverAccel = 12f; w.Num = 0.15f; w.Num3 = 2.45f; w.Num4 = 1f; w.Num5 = 0.1f; w.HoverDamp = 0.9f; w.HalfCost = true;
            }
            d[30].HoverStart = true; d[30].Glow = "Glow_181"; d[30].GlowColor = FxBatch.C(255, 255, 255, 127); d[30].Anim = WingAnim.Vortex; d[30].FlapFrame = -1;
            d[31].Alpha = 220f / 255f;
            // The developers' wings (150 / 7 / ×1).
            foreach (int s in new[] { 3, 16, 17, 18, 19, 28, 33, 34, 35, 36, 38, 39, 40, 41, 42, 43, 47, 48, 49, 50, 51 }) W(s, 150, 7f);
            var jew = d[28]; jew.HoverStart = true; jew.HoverDamp = 0.9f; jew.HalfCost = true; jew.Off = new Vector2(0f, 19f); jew.OnlyInAir = true; jew.Standing = true;
            jew.Bright = true; jew.Alpha = 1f; jew.Glow = "Extra_38"; jew.GlowColor = new Color(0.94f, 0.94f, 0.94f, 1f); jew.Anim = WingAnim.Clock; jew.FlapFrame = -1;
            var yor = d[33]; yor.HoverStart = true; yor.HoverDamp = 0.9f; yor.Invisible = true; yor.Anim = WingAnim.Still; yor.FlapFrame = -1;
            var jim = d[34]; jim.Frames = 6; jim.Off = new Vector2(-4f, 0f); jim.OnlyInAir = true; jim.Bright = true; jim.Alpha = 100f / 255f; jim.Anim = WingAnim.Six;
            d[35].HoverStart = true; d[35].HoverDamp = 0.9f;
            d[36].Glow = "Glow_213"; d[36].GlowColor = new Color(1f, 1f, 1f, 0f); d[36].GlowScale = 1.06f;
            var ark = d[38]; ark.Glow = "Glow_251"; ark.GlowColor = FxBatch.C(160, 180, 215, 0); ark.Anim = WingAnim.Arkhalis;
            var lein = d[39]; lein.Frames = 6; lein.Off = new Vector2(-6f, -7f); lein.OnlyInAir = true; lein.Anim = WingAnim.Six;
            var gho = d[40]; gho.Frames = 14; gho.Off = new Vector2(-1f, 0f); gho.Bright = true; gho.Alpha = 100f / 255f; gho.Anim = WingAnim.Ghostar;
            d[41].Off = new Vector2(-10f, 2f);
            var grox = d[43]; grox.Frames = 7; grox.Off = new Vector2(-14f, -5f); grox.Glow = "Glow_272"; grox.GlowColor = FxBatch.C(230, 230, 230, 60); grox.Anim = WingAnim.Grox; grox.FlapFrame = 4; grox.GlideFrame = 1;
            var chick = d[47]; chick.Frames = 11; chick.Off = new Vector2(-3f, -1f); chick.Glow = "Glow_366"; chick.GlowColor = FxBatch.C(255, 255, 255, 180); chick.Anim = WingAnim.Cycle11;
            var chip = d[48]; chip.Frames = 8; chip.Off = Vector2.zero; chip.OnlyInAir = true; chip.Anim = WingAnim.Chippy; chip.Ticks = 3; chip.GlideFrame = 3;
            var hero = d[49]; hero.Frames = 11; hero.Off = new Vector2(-3f, -1f); hero.Anim = WingAnim.Cycle11; hero.FlapFrame = 5;
            var kaz = d[50]; kaz.Frames = 11; kaz.Off = new Vector2(-4f, 0f); kaz.Anim = WingAnim.Kazzy; kaz.Glow = "Wings_50"; kaz.GlowColor = new Color(1f, 1f, 1f, 1f);
            var luna = d[51]; luna.Frames = 8; luna.Off = new Vector2(-4f, -1f); luna.Bright = true; luna.Alpha = 100f / 255f; luna.Anim = WingAnim.Luna;
            return d;
        }

        // ------------------------------------------------------------------ the frame (WingFrame)

        public static int Frame;
        static float counter;
        static int cycleStep;

        /// <summary>
        /// One physics step of WingFrame (t ticks): flapping is flying or a jump's lift; air is
        /// off the ground; the glide (holding jump while falling) sets its frame after.
        /// </summary>
        public static void Animate(WingDef w, bool flap, bool air, bool jumpHeld, bool gliding, float wingTime, float vx, bool hoverDown, float t)
        {
            switch (w.Anim)
            {
                case WingAnim.Jetpack:
                    if (flap) Loop(t, 5f, 0, 2);
                    else Frame = 3;
                    if (!jumpHeld || !air) Frame = 3;
                    return;
                case WingAnim.Steampunk:
                    if (flap) Seq(t, 5f, 1, 2, 3, 2);
                    else Frame = air ? (gliding ? 3 : 2) : 0;
                    return;
                case WingAnim.Beetle:
                    if (flap) Seq(t, 1f, 1, 2, 3, 2);
                    else if (air && jumpHeld) Seq(t, 3f, 1, 2, 3, 2);
                    else Frame = air ? (wingTime <= 0f ? 0 : 1) : 0;
                    return;
                case WingAnim.Board:
                    if (!jumpHeld) Frame = 0;
                    else if (wingTime > 0f && hoverDown)
                    {
                        if (Mathf.Abs(vx) > 0.01f) Seq(t, 2f, 1, 2, 3, 2);
                        else Seq(t, 6f, 4, 5, 4);
                    }
                    else if (wingTime > 0f) Seq(t, 2f, 4, 5, 6, 5);
                    else Seq(t, 6f, 4, 5, 4);
                    return;
                case WingAnim.Clock:
                    // M:… the board's own clock: miscCounter/5 % 4, whatever the flight does.
                    Frame = (int)(Time.time * 60f / 5f) % 4;
                    return;
                case WingAnim.Vortex:
                    if (flap || (air && jumpHeld))
                    {
                        counter += t;
                        if (counter >= 6f) counter -= 6f;
                        Frame = 1 + (int)(counter / 2f);
                    }
                    else Frame = 0;
                    return;
                case WingAnim.Six:
                    if (!air) { Frame = 0; return; }
                    counter += t;
                    Frame = (int)(counter / (flap ? 4f : jumpHeld ? 9f : 6f)) % 6;
                    return;
                case WingAnim.Grox:
                    if (flap) Loop(t, 4f, 1, 6);
                    else Frame = air ? (gliding ? 1 : 2) : 0;
                    return;
                case WingAnim.Empress:
                    if (flap)
                    {
                        counter += t;
                        if (counter >= 30f) counter -= 30f;
                        Frame = 1 + (int)(counter / 5f);
                    }
                    else Frame = air ? (jumpHeld ? 2 : 3) : 0;
                    return;
                case WingAnim.Starboard:
                    if (!air) { Frame = 0; return; }
                    counter += t;
                    Frame = (int)(counter / (wingTime <= 0f ? 5f : 3f)) % 6;
                    return;
                case WingAnim.Chippy:
                    if (flap) Loop(t, 3f, 0, 7);
                    else Frame = air ? (gliding ? 3 : 1) : 0;
                    return;
                case WingAnim.Luna:
                    if (!air) { Frame = 0; return; }
                    counter += t;
                    float step = flap ? 4f : jumpHeld ? 6f : 8f;
                    while (counter >= step)
                    {
                        counter -= step;
                        Frame = Frame >= 7 ? 2 : Frame + 1;
                    }
                    return;
                case WingAnim.Cycle11:
                case WingAnim.Kazzy:
                {
                    // The 11-frame wings (Pl:30182-30394): a flap loop of 2..10 (2..9), a glide pose of 1, at rest in the air on 2.
                    bool kazzy = w.Anim == WingAnim.Kazzy;
                    int last = kazzy ? 9 : 10;
                    if (!air)
                    {
                        Frame = 0;
                        counter = 0f;
                        return;
                    }
                    float s = flap ? (hoverDown ? 6f : 4f) : (kazzy ? 5f : 6f);
                    if (!flap && gliding) { Frame = 1; return; }
                    if (!flap && Frame == 2) return;
                    if (Frame < 2) Frame = 2;
                    counter += t;
                    while (counter >= s)
                    {
                        counter -= s;
                        Frame = Frame >= last ? 2 : Frame + 1;
                    }
                    return;
                }
                case WingAnim.Arkhalis:
                    if (flap || (air && jumpHeld))
                    {
                        counter += t;
                        if (counter >= 32f) counter -= 32f;
                        int f = 1 + (int)(counter / 8f);
                        Frame = f == 4 ? 2 : f;
                    }
                    else Frame = 0;
                    return;
                case WingAnim.Ghostar:
                    counter += t;
                    Frame = air ? (int)(counter / 5f) % 8 : 8 + (int)(counter / 8f) % 6;
                    return;
                case WingAnim.Still:
                    if (gliding) Frame = 2;
                    return;
                default:
                    // The generic rule (Pl:30678-30756): flapping advances a frame every Ticks ticks (0-3), in the air 1, on the ground 0.
                    if (flap) Loop(t, w.Ticks, 0, w.Frames - 1);
                    else Frame = air ? (gliding ? w.GlideFrame : w.AirFrame) : 0;
                    return;
            }
        }

        static void Loop(float t, float ticks, int first, int last)
        {
            if (Frame < first || Frame > last) Frame = first;
            counter += t;
            while (counter >= ticks)
            {
                counter -= ticks;
                Frame = Frame >= last ? first : Frame + 1;
            }
        }

        static readonly int[] seqBuf = new int[8];

        static void Seq(float t, float ticks, params int[] frames)
        {
            counter += t;
            while (counter >= ticks)
            {
                counter -= ticks;
                cycleStep = (cycleStep + 1) % frames.Length;
            }
            if (cycleStep >= frames.Length) cycleStep = 0;
            Frame = frames[cycleStep];
        }

        // ------------------------------------------------------------------ dust and light (WingAirVisuals, the glide's extras)

        /// <summary>The flight dust of a tick (Pl:30980-31254); topLeft is the hitbox's (Terraria px), dir the facing, v the player's px/tick (y down).</summary>
        public static void FlightDust(WingDef w, Vector2 topLeft, int dir, Vector2 v, bool up)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            const int W = 20, H = 42;
            var back = new Vector2(topLeft.x + W / 2f + (dir == 1 ? -40f : 4f), topLeft.y + H / 2f - 15f);
            int d;
            switch (w.Slot)
            {
                case 10:
                case 34:
                case 51:
                    if (Random.Range(0, 2) != 0) return;
                    d = p.NewDust(back, 30, 30, w.Slot == 10 ? 76 : 261, 0f, 0f, 50, 0.6f);
                    if (d < 0) return;
                    p[d].FadeIn = 1.1f;
                    p[d].NoGravity = true;
                    p[d].Velocity *= 0.3f;
                    if (w.Slot == 51) p[d].Color = FxBatch.C(230, 130, 55);
                    return;
                case 9:
                    if (Random.Range(0, 2) != 0) return;
                    d = p.NewDust(back, 30, 30, 6, 0f, 0f, 200, 2f);
                    if (d < 0) return;
                    p[d].NoGravity = true;
                    p[d].Velocity *= 0.3f;
                    return;
                case 6:
                    if (Random.Range(0, 4) != 0) return;
                    d = p.NewDust(back, 30, 30, 55, 0f, 0f, 200);
                    if (d >= 0) p[d].Velocity *= 0.3f;
                    return;
                case 5:
                    if (Random.Range(0, 3) != 0) return;
                    d = p.NewDust(new Vector2(topLeft.x + W / 2f + (dir == 1 ? -30f : 6f), topLeft.y), 18, H, 58, 0f, 0f, 255, 1.2f);
                    if (d >= 0) p[d].Velocity *= 0.3f;
                    return;
                case 26:
                case 37:
                {
                    bool fish = w.Slot == 26;
                    int wide = fish ? 18 : 24;
                    d = p.NewDust(new Vector2(topLeft.x + W / 2f + (dir == 1 ? -wide - 2f : 2f), topLeft.y), wide, H, fish ? 217 : 31, 0f, 0f, 100, fish ? 1.4f : 1f);
                    if (d >= 0)
                    {
                        p[d].NoGravity = fish;
                        p[d].Velocity = p[d].Velocity / 4f - v * (fish ? 1f : 0.5f);
                    }
                    if (Random.Range(0, fish ? 2 : 3) != 0) return;
                    d = p.NewDust(new Vector2(topLeft.x + W / 2f + (dir == 1 ? 12f : -24f), topLeft.y), 12, H / 2, fish ? 217 : 31, 0f, 0f, 100, fish ? 1.4f : 1f);
                    if (d >= 0)
                    {
                        p[d].NoGravity = fish;
                        p[d].Velocity = p[d].Velocity / 4f - v * (fish ? 1f : 0.5f);
                    }
                    return;
                }
                case 29:
                    if (Random.Range(0, 3) != 0) return;
                    d = p.NewDust(back, 30, 30, 6, 0f, 0f, 100, 2.4f);
                    if (d < 0) return;
                    p[d].NoGravity = true;
                    p[d].Velocity *= 0.3f;
                    if (Random.Range(0, 10) == 0) p[d].FadeIn = 2f;
                    return;
                case 31:
                    for (int k = 0; k < (Random.Range(0, 2) == 0 ? 2 : 1); k++)
                    {
                        var at = k == 0 ? back : new Vector2(topLeft.x + W / 2f + (dir == 1 ? 12f : -24f), topLeft.y);
                        if (Random.Range(0, 6) == 0 && (d = p.NewDust(at, k == 0 ? 30 : 12, k == 0 ? 30 : H / 2, 86, 0f, 0f, 0, 1f)) >= 0)
                        {
                            p[d].FadeIn = 1.2f;
                            p[d].Velocity *= 0.2f;
                            p[d].NoGravity = true;
                        }
                        if (Random.Range(0, 3) == 0 && (d = p.NewDust(at, k == 0 ? 30 : 12, k == 0 ? 30 : H / 2, 240, 0f, 0f, 200, 1.2f)) >= 0) p[d].Velocity *= 0.2f;
                    }
                    return;
                case 4:
                    JetDust(topLeft, dir, v, up);
                    return;
                case 30:
                    // Dust 229 along two arcs round the player, thrown outward.
                    var c = topLeft + new Vector2(W / 2f, H / 2f);
                    for (int j = 0; j < 4; j++)
                    {
                        if (Random.Range(0, 4) != 0) continue;
                        float a = -0.745f + Mathf.PI / 8f * j + 0.03f * j;
                        var off = new Vector2(Mathf.Cos(a) * -dir * 20f, Mathf.Sin(a) * 20f);
                        d = p.NewDust(c + off - new Vector2(4f, 4f), 0, 0, 229, 0f, 0f, 0, 0.8f);
                        if (d < 0) continue;
                        p[d].Position = c + off;
                        p[d].Velocity = off.normalized * 2f;
                        p[d].NoGravity = true;
                    }
                    return;
            }
        }

        /// <summary>The jetpack's flames (Pl:29713-29756): two a tick from the nozzles behind the back, four holding UP, thrown down.</summary>
        static void JetDust(Vector2 topLeft, int dir, Vector2 v, bool up)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < (up ? 4 : 2); k++)
            {
                float x = topLeft.x + 10f + (k % 2 == 0 ? (dir > 0 ? -26f : 16f) : (dir > 0 ? -20f : 8f));
                float y = topLeft.y + 42f - 18f + (k % 2 == 1 ? 6f : 0f) + (k >= 2 ? v.y : 0f);
                int d = p.NewDust(new Vector2(x, y), 8, 8, 6, 0f, 0f, 100, 1.75f);
                if (d < 0) continue;
                p[d].NoGravity = true;
                p[d].Velocity = new Vector2(p[d].Velocity.x * 0.1f, p[d].Velocity.y + 2f - v.y * 0.3f + (k >= 2 ? 6f : 0f));
            }
        }

        /// <summary>The glide's per-wing dust (Pl:27234-27546).</summary>
        public static void GlideDust(WingDef w, Vector2 topLeft, int dir)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            int d;
            var back = new Vector2(topLeft.x + 10f + (dir == 1 ? -40f : 4f), topLeft.y + 21f - 15f);
            switch (w.Slot)
            {
                case 10:
                    if (Random.Range(0, 3) == 0 && (d = p.NewDust(back, 30, 30, 76, 0f, 0f, 50, 0.6f)) >= 0) { p[d].NoGravity = true; p[d].Velocity *= 0.3f; }
                    return;
                case 9:
                    if (Random.Range(0, 3) == 0 && (d = p.NewDust(new Vector2(topLeft.x + (dir == 1 ? -32f : 8f) + 10f, topLeft.y + 6f), 30, 30, 6, 0f, 0f, 200, 2f)) >= 0) { p[d].NoGravity = true; p[d].Velocity *= 0.3f; }
                    return;
                case 6:
                    if (Random.Range(0, 10) == 0 && (d = p.NewDust(new Vector2(topLeft.x + 10f - 15f, topLeft.y + 21f - 12f), 30, 20, 55, 0f, 0f, 200)) >= 0) p[d].Velocity *= 0.3f;
                    return;
                case 5:
                    if (Random.Range(0, 6) == 0 && (d = p.NewDust(new Vector2(topLeft.x + 10f + (dir == 1 ? -30f : 6f), topLeft.y), 18, 42, 58, 0f, 0f, 255, 1.2f)) >= 0) p[d].Velocity *= 0.3f;
                    return;
                case 29:
                    if (Random.Range(0, 3) == 0 && (d = p.NewDust(back, 30, 30, 6, 0f, 0f, 100, 2.4f)) >= 0) { p[d].NoGravity = true; p[d].Velocity *= 0.3f; }
                    return;
                case 26:
                case 37:
                    FlightDust(w, topLeft, dir, Vector2.zero, false);
                    return;
            }
        }

        // ------------------------------------------------------------------ the draw (DrawPlayer_09_Wings)

        static readonly Dictionary<int, Sprite[]> frames = new Dictionary<int, Sprite[]>();

        static readonly HashSet<string> missing = new HashSet<string>();

        static Sprite FrameSprite(string tex, int frames_, int frame)
        {
            if (missing.Contains(tex)) return null;
            Sprite whole;
            try { whole = FxBatch.Whole(tex); }
            catch (System.Exception e)
            {
                missing.Add(tex);
                Plugin.Log.LogWarning($"翅膀贴图 {tex} 读不到：{e.Message}");
                return null;
            }
            if (whole == null) return null;
            int w = (int)whole.rect.width, h = (int)whole.rect.height / Mathf.Max(1, frames_);
            frame = Mathf.Clamp(frame, 0, frames_ - 1);
            return FxBatch.Frame(tex, 0, frame * h, w, h, w * 0.5f, h * 0.5f);
        }

        /// <summary>
        /// The wings behind the player: their frame at the slot's place off the hitbox centre
        /// (frameCentrePx is the drawn player's frame centre, 3 px above it), mirrored with the player,
        /// with the slot's colour and glowmask; the hoverboard's flames, Nebula's and Loki's copies.
        /// </summary>
        public static void Draw(FxBatch fx, WingDef w, Vector2 frameCentrePx, int dir, float opacity, bool air)
        {
            if (fx == null || w == null || w.Invisible || opacity <= 0f) return;
            if (w.OnlyInAir && !air) return;
            var at = frameCentrePx + new Vector2(w.Off.x * dir, w.Off.y + 3f);
            bool flip = dir < 0;
            string tex = "Wings_" + w.Slot;
            var spr = FrameSprite(tex, w.Frames, Frame);
            if (spr == null) return;
            if (w.Slot == 22)
            {
                // The hoverboard's seven jittered exhaust flames behind it.
                var flame = FrameSprite("ItemFlame_1866", 7, Frame);
                for (int i = 0; i < 7; i++)
                {
                    var j = new Vector2(Mathf.PerlinNoise(Time.time * 12f, i) - 0.5f, Mathf.PerlinNoise(i, Time.time * 12f)) * new Vector2(1.5f, 1.75f) * 0.5f;
                    float k = (250f - 10f * i) / 255f;
                    fx.Draw(flame, at + j, new Color(k, k, k, (150f - 10f * i) / 255f) * opacity, 0f, 1f, flipX: flip);
                }
            }
            if (w.Slot == 40)
            {
                // Ghostar's: the whole group of frames at once (0-7 in the air, 8-13 standing),
                // each circling its own way and trailing the velocity a little (PDL:849-895).
                var hero = HeroController.SilentInstance;
                var rb = hero != null ? hero.GetComponent<Rigidbody2D>() : null;
                var vel = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / TerraMovement.PxT : Vector2.zero;
                int first = air ? 0 : 8, last = air ? 8 : 14;
                float group = air ? 4f : 3f, mcN = Time.time * 60f % 300f / 300f;
                var gc = new Color(250f / 255f, 250f / 255f, 250f / 255f, 100f / 255f) * opacity;
                for (int k = first; k < last; k++)
                {
                    int i = (k - first) % (int)group;
                    float a = (mcN * (2f + i) + i * 0.5f) * Mathf.PI * 2f;
                    var swirl = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 0.5f * (i + 1);
                    var layer = FrameSprite(tex, w.Frames, k);
                    if (layer != null) fx.Draw(layer, at + swirl - vel * 1.5f * (i / group), gc, 0f, 1f, flipX: flip);
                }
                return;
            }
            float alpha = w.Alpha;
            if (w.Slot == 11) alpha = Mathf.Lerp(100f, 150f, Mathf.PingPong(Time.time * 6f, 1f)) / 255f;
            var c = w.Bright ? new Color(250f / 255f, 250f / 255f, 250f / 255f, alpha) : new Color(1f, 1f, 1f, alpha);
            if (w.Slot == 28 || w.Slot == 45) c = Color.white;
            if (w.Slot == 51)
            {
                float pulse = Mathf.Lerp(0.85f, 1f, Mathf.PingPong(Time.time * 1.2f, 1f));
                c = new Color(pulse, pulse, pulse, 100f / 255f * pulse);
            }
            fx.Draw(spr, at, c * opacity, 0f, 1f, flipX: flip);
            if (w.Slot == 31)
            {
                // Nebula: pink-to-crimson added copies breathing a pixel out each way, and a full one.
                float mc = Time.time * 60f;
                var pink = Color.Lerp(FxBatch.C(255, 105, 180), FxBatch.C(220, 20, 60), Mathf.Cos(Mathf.PI * 2f * mc / 100f) * 0.4f + 0.5f);
                pink.a = 0f;
                float o = Mathf.Cos(Mathf.PI * 2f * mc / 60f) * 0.5f + 0.5f;
                for (int k = 0; k < 4; k++)
                    fx.Draw(spr, at + new Vector2(k == 0 ? o : k == 1 ? -o : 0f, k == 2 ? o : k == 3 ? -o : 0f), pink * 0.5f * opacity, 0f, 1f, flipX: flip);
                fx.Draw(spr, at, pink * opacity, 0f, 1f, flipX: flip);
            }
            if (w.Slot == 45)
            {
                float pp = Mathf.Cos(Mathf.PI * 2f * Time.time * 60f / 75f) * 4f;
                var g = FxBatch.C(70, 70, 70, 0) * ((pp / 8f + 0.5f) * 0.4f) * opacity;
                for (int k = 0; k < 4; k++)
                    fx.Draw(spr, at + new Vector2(k == 0 ? pp : k == 1 ? -pp : 0f, k == 2 ? pp : k == 3 ? -pp : 0f), g, 0f, 1f, flipX: flip);
            }
            if (w.Slot == 44)
            {
                // The Empress's wings: Extra_171's grey veins through the HallowBoss rainbow
                // (PlayerRainbowWingsTextureContent), opaque over them; here a hue running down
                // the frame in bands and round with time.
                var veins = FxBatch.Whole("Extra_171");
                int fw = (int)veins.rect.width, fh = (int)veins.rect.height / 7;
                for (int y0 = 0; y0 < fh; y0 += 8)
                {
                    var band = FxBatch.Frame("Extra_171", 0, Frame * fh + y0, fw, Mathf.Min(8, fh - y0), fw * 0.5f, fh * 0.5f - y0);
                    var rc = FxParticles.HslToRgb((Time.time * 0.5f + y0 / (float)fh) % 1f, 1f, 0.75f);
                    fx.Draw(band, at, new Color(rc.r * opacity, rc.g * opacity, rc.b * opacity, opacity), 0f, 1f, flipX: flip);
                }
            }
            if (w.Glow == null) return;
            var gs = w.Glow.StartsWith("Wings_") || w.Glow.StartsWith("Glow_") || w.Glow.StartsWith("Flame_") ? FrameSprite(w.Glow, w.Frames, Frame) : FrameSprite(w.Glow, 4, Frame);
            if (gs == null) return;
            if (w.Slot == 43)
            {
                for (int k = 0; k < 2; k++)
                    fx.Draw(gs, at + new Vector2(Random.Range(-1.25f, 1.25f), Random.Range(-1.25f, 1.25f)), w.GlowColor * opacity, 0f, 1f, flipX: flip);
                return;
            }
            fx.Draw(gs, at, w.GlowColor * opacity, 0f, w.GlowScale, flipX: flip);
            if (w.Slot == 36)
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI / 2f;
                    fx.Draw(gs, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f, FxBatch.C(127, 127, 127, 127) * opacity, 0f, 1f, flipX: flip);
                }
        }
    }
}
