using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The shortswords remade (the user's 10-26 "继续升级剩下的这些…依旧是原来的要求，特效，动画，粒子效果，新颖，多样", the spears'
    /// "可以多几种…法，也有一些右键特殊能力…范围也搞大一点"), with the remade swings on (BladeArts.Enabled): each use is one technique
    /// of the weapon's own round (a straight jab, a rising stab, a fan of three, a cross, a long thrust, a star of five), the
    /// blade out about 30% further than the game's stab, its streak of light along it, afterimages and its metal's motes;
    /// the rapiers keep their stabs a use, laid out in their own pattern (the Gladius's phalanx, the Ruler's marching
    /// marks, the Chlorophyte Saber's spiral). The right-click arts' blades, lines, clouds and the thrown umbrella are drawn
    /// and struck here too (BladeSkillsClose). Damage stays the game's: a technique's blows share one stab's.
    /// </summary>
    internal sealed partial class SwordFx
    {
        enum CloseWhat : byte { Stab, Dagger, Line, Spore, Umbrella, Cloud }

        /// <summary>One of the close weapons' pieces, carried by a CloseArt projectile (its Damage, Crit, Hit, Recent).</summary>
        sealed class Close
        {
            public CloseWhat What;
            public ItemDef Item;
            public BladeArts.Art Art;
            public string Move;
            /// <summary>Updates (two a tick) before it starts, since it started, its whole life.</summary>
            public int Delay, Age, Life;
            /// <summary>A stab's tip reach (px), its turn from the aim at the start and the end (radians, toward its side), how thick its streak.</summary>
            public float Reach, A0, A1, Thick = 15f, Spin, Side = 1f;
            public bool Pierce, Finisher, Aimed, Returning, Star, Peaked;
            /// <summary>The thrown umbrella's updates hanging at the far end.</summary>
            public int Delay2;
            /// <summary>Its aim (unit, px y down), from (px), to (px), velocity (px an update).</summary>
            public Vector2 Aim, From, To, Vel;
            public readonly List<Vector2> Trail = new List<Vector2>();
        }

        readonly Dictionary<Proj, Close> closes = new Dictionary<Proj, Close>();

        /// <summary>A stab's streak or blade left where it reached, fading over its life (s).</summary>
        sealed class CloseGhost
        {
            public Sprite Blade;
            public Vector2 A, B;
            public Color Tint;
            public float Thick, Rot, Scale = 1f, Age, Life;
            public bool Flip;
        }

        readonly List<CloseGhost> closeGhosts = new List<CloseGhost>();

        /// <summary>The stabs' lingering streaks and blades, aging (each frame, with the swings' effects).</summary>
        void DrawCloseGhosts(FxBatch fx)
        {
            float dt = Time.deltaTime;
            for (int i = closeGhosts.Count - 1; i >= 0; i--)
            {
                var g = closeGhosts[i];
                g.Age += dt;
                if (g.Age >= g.Life) { closeGhosts.RemoveAt(i); continue; }
                float f = 1f - g.Age / g.Life;
                if (g.Blade != null) fx.Draw(g.Blade, g.A, Glow(g.Tint, 0.5f * f), g.Rot, g.Scale * (1f + 0.15f * (1f - f)), flipX: g.Flip);
                else Streak(fx, g.A, g.B, g.Tint, g.Thick * (0.5f + 0.5f * f), 0.7f * f);
            }
        }

        /// <summary>Dev: techniques begun (by name, the last), close pieces' enemy hits, the farthest stab tip (px from the player's centre).</summary>
        public static string DevCloseMove = "";
        public static int DevCloseMoves, DevCloseHits;
        public static float DevCloseFar;
        /// <summary>Dev: the last thrown piece drawn (its picture, size, place).</summary>
        public static string DevCloseSprite = "";

        /// <summary>The basic shortswords' rounds of techniques, by metal (the next use within half a second goes on to the next).</summary>
        static readonly Dictionary<ItemType, string[]> stabCombos = new Dictionary<ItemType, string[]>
        {
            [ItemType.CopperShortsword] = new[] { "直刺", "上挑刺", "三连扇刺", "长刺" },
            [ItemType.TinShortsword] = new[] { "直刺", "上挑刺", "三连扇刺", "长刺" },
            [ItemType.IronShortsword] = new[] { "直刺", "三连扇刺", "上挑刺", "长刺" },
            [ItemType.LeadShortsword] = new[] { "直刺", "三连扇刺", "上挑刺", "长刺" },
            [ItemType.SilverShortsword] = new[] { "上挑刺", "直刺", "交叉刺", "长刺" },
            [ItemType.TungstenShortsword] = new[] { "上挑刺", "直刺", "交叉刺", "长刺" },
            [ItemType.GoldShortsword] = new[] { "直刺", "三连扇刺", "交叉刺", "星芒刺" },
            [ItemType.PlatinumShortsword] = new[] { "直刺", "三连扇刺", "交叉刺", "星芒刺" },
        };

        /// <summary>The game's stab tip reach (px from the player's centre): AI_161's centre 15 updates out plus the 6 its line looks ahead.</summary>
        const float StabTip = 2.1f * 21f, RapierTip = 2.4f * 21f;
        /// <summary>How much further the remade stabs reach.</summary>
        internal const float CloseReach = 1.3f;

        static ItemType closeLastItem;
        static int closeLastMove = -1;
        static float closeLastEnd = -10f;

        /// <summary>The weapon's look for the remade pieces: its colours (BladeArts' read of its picture) and the theme fitting it.</summary>
        static readonly Dictionary<ItemType, BladeArts.Art> closeArts = new Dictionary<ItemType, BladeArts.Art>();

        internal static BladeArts.Art CloseArtOf(ItemDef d)
        {
            if (closeArts.TryGetValue(d.Type, out var a)) return a;
            var src = BladeArts.Of(d);
            BladeArts.Theme theme;
            switch (d.Type)
            {
                case ItemType.ChlorophyteSaber: theme = BladeArts.Theme.Spore; break;
                case ItemType.Gladius: theme = BladeArts.Theme.Coin; break;
                case ItemType.Ruler: theme = BladeArts.Theme.Wood; break;
                case ItemType.Umbrella: case ItemType.TragicUmbrella: theme = BladeArts.Theme.Water; break;
                case ItemType.ShadowJoustingLance: theme = BladeArts.Theme.Shadow; break;
                case ItemType.HallowJoustingLance: theme = BladeArts.Theme.Holy; break;
                default: theme = BladeArts.Theme.Steel; break;
            }
            a = new BladeArts.Art { Type = d.Type, Kind = BladeArts.Kind.Knife, Dark = src.Dark, Main = src.Main, Light = src.Light, Level = Mathf.Clamp(d.Rare, 0, 5), Theme = theme };
            closeArts[d.Type] = a;
            return a;
        }

        // ------------------------------------------------------------------ the uses

        /// <summary>A shortsword's use with the remade swings on: its technique (or its rapier's pattern) instead of the game's one stab; false when off.</summary>
        bool CloseStabUse(ItemDef item, Vector2 playerPx, Vector2 aim, int damage, float crit)
        {
            if (!WeaponRefitting.EnabledFor(item) || item == null) return false;
            int type = StabType(item.Type);
            float scale = Mathf.Clamp(Equipment.UseScale(item), 0.4f, 1f);
            int useTicks = Mathf.Max(1, Mathf.RoundToInt(item.UseTime * 60f * Equipment.UseScale(item)));
            int animTicks = Mathf.Max(useTicks, Mathf.RoundToInt(item.UseAnimation * 60f * Equipment.UseScale(item)));
            float kb = Equipment.Knockback(item);
            float side = aim.x < 0f ? -1f : 1f;
            bool rapier = type == 802 || type == 842 || type == 1127;
            if (rapier)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(animTicks / (float)useTicks));
                int round = NextCloseMove(item, 3, animTicks);
                float tip = RapierTip * CloseReach;
                for (int k = 0; k < n; k++)
                {
                    float a = 0f, reach = tip, share = 1f;
                    string move;
                    bool fin = false;
                    switch (item.Type)
                    {
                        case ItemType.Gladius:
                            // The phalanx: high and low in turn, a wall of points; every third use the line breaks through.
                            if (round == 2) { move = "破阵刺"; reach = tip * (k == n - 1 ? 1.25f : 1.1f); fin = k == n - 1; }
                            else { move = "方阵刺"; a = (k % 2 == 0 ? -0.2f : 0.2f) * (round == 1 ? -1f : 1f) * (k == n - 1 ? 0.3f : 1f); }
                            break;
                        case ItemType.Ruler:
                            // Marching marks: each stab a tick further, straight down the aim.
                            move = "刻度刺";
                            reach = tip * (0.9f + 0.12f * k);
                            fin = k == n - 1;
                            break;
                        default:
                            // The spiral: the points wind round the aim; the saber's 70% and its spores as the game's.
                            move = "螺旋刺";
                            a = 0.3f * Mathf.Sin(k * 1.4f + round * 2.1f);
                            share = 0.7f;
                            break;
                    }
                    // The last stab of a use is the round's flourish: drawn bigger (its blow as any other).
                    fin |= k == n - 1;
                    var c = NewStab(item, type, move, damage * share, crit, kb * (fin ? 1.4f : 1f), aim, side, a, a, reach, 16, k * useTicks * 2);
                    c.Finisher = fin;
                    c.Aimed = k > 0;
                }
                DevCloseMove = item.Type == ItemType.Gladius ? (round == 2 ? "破阵刺" : "方阵刺") : item.Type == ItemType.Ruler ? "刻度刺" : "螺旋刺";
            }
            else
            {
                var combo = stabCombos.TryGetValue(item.Type, out var cmb) ? cmb : stabCombos[ItemType.CopperShortsword];
                int m = NextCloseMove(item, combo.Length, animTicks);
                string move = combo[m];
                float tip = StabTip * CloseReach;
                int life = Mathf.RoundToInt(16 * Mathf.Max(0.6f, scale));
                switch (move)
                {
                    case "直刺":
                        NewStab(item, type, move, damage, crit, kb, aim, side, 0f, 0f, tip, life, 0);
                        break;
                    case "上挑刺":
                        // From under the aim up past it: the point rising through what stands there.
                        NewStab(item, type, move, damage * 1.05f, crit, kb, aim, side, 0.55f, -0.35f, tip * 0.95f, life + 2, 0);
                        break;
                    case "三连扇刺":
                        NewStab(item, type, move, damage * 0.42f, crit, kb * 0.6f, aim, side, -0.24f, -0.24f, tip * 0.9f, life - 4, 0);
                        NewStab(item, type, move, damage * 0.42f, crit, kb * 0.6f, aim, side, 0.24f, 0.24f, tip * 0.9f, life - 4, 4);
                        NewStab(item, type, move, damage * 0.42f, crit, kb, aim, side, 0f, 0f, tip, life - 4, 8);
                        break;
                    case "交叉刺":
                        NewStab(item, type, move, damage * 0.58f, crit, kb * 0.7f, aim, side, -0.45f, 0.25f, tip, life - 2, 0);
                        NewStab(item, type, move, damage * 0.58f, crit, kb, aim, side, 0.45f, -0.25f, tip, life - 2, 6);
                        break;
                    case "长刺":
                    {
                        var c = NewStab(item, type, move, damage * 1.15f, crit, kb * 1.5f, aim, side, 0f, 0f, tip * 1.35f, life + 6, 0);
                        c.Finisher = true;
                        c.Thick = 22f;
                        break;
                    }
                    case "星芒刺":
                    {
                        // A point of light and four short rays about it: a star struck at the tip.
                        var c = NewStab(item, type, move, damage * 0.7f, crit, kb * 1.4f, aim, side, 0f, 0f, tip * 1.2f, life + 4, 0);
                        c.Finisher = true;
                        c.Star = true;
                        c.Thick = 20f;
                        foreach (float a in new[] { -0.7f, -0.35f, 0.35f, 0.7f })
                            NewStab(item, type, move, damage * 0.12f, crit, kb * 0.4f, aim, side, a, a, tip * 0.75f, life - 4, 4);
                        break;
                    }
                }
                DevCloseMove = move;
            }
            DevCloseMoves++;
            return true;
        }

        /// <summary>The technique's index this use: the next of the round within half a second of the last use's end, else the first.</summary>
        static int NextCloseMove(ItemDef item, int count, int animTicks)
        {
            bool goOn = closeLastItem == item.Type && Time.time - closeLastEnd < 0.5f;
            closeLastMove = goOn ? (closeLastMove + 1) % count : 0;
            closeLastItem = item.Type;
            closeLastEnd = Time.time + animTicks / 60f;
            return closeLastMove;
        }

        /// <summary>A stab of a technique: tip reach (px), turn from the aim at its start and end (toward the aim's side), life and delay in updates.</summary>
        Close NewStab(ItemDef item, int type, string move, float damage, float crit, float kb, Vector2 aim, float side, float a0, float a1, float reach, int life, int delay)
        {
            var hero = HeroController.SilentInstance;
            var at = hero != null ? PlayerCenterPx(hero) : Vector2.zero;
            var p = Add(Kind.CloseArt, type, at, aim * 2f, Mathf.Max(1, Mathf.RoundToInt(damage)), kb, crit, 0f, 1f, -1);
            p.Opacity = 0f;
            var c = new Close { What = CloseWhat.Stab, Item = item, Art = CloseArtOf(item), Move = move, Delay = delay, Life = Mathf.Max(6, life), Reach = reach, A0 = a0, A1 = a1, Side = side, Aim = aim };
            closes[p] = c;
            return c;
        }

        // ------------------------------------------------------------------ the arts' pieces (BladeSkillsClose)

        /// <summary>A charged long stab that runs through every enemy on its line (stopped by a wall).</summary>
        internal void ClosePierce(ItemDef item, Vector2 aimWorld, float damageScale, float reachScale, string move)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var from = PlayerCenterPx(hero);
            var aim = TerraParticles.ToPx(aimWorld) - from;
            if (aim.sqrMagnitude < 1e-3f) aim = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            aim.Normalize();
            float reach = StabTip * CloseReach * reachScale;
            reach = WallReach(from, aim, reach);
            int type = StabType(item.Type);
            var c = NewStab(item, type, move, Equipment.Damage(item) * damageScale, Equipment.Crit(item), Equipment.Knockback(item) * 1.6f, aim, aim.x < 0f ? -1f : 1f, 0f, 0f, reach, 30, 0);
            c.Pierce = true;
            c.Finisher = true;
            c.Thick = 26f;
            if (item.Type == ItemType.ChlorophyteSaber)
                for (int k = 1; k <= 4; k++) Spore(item, from + aim * reach * (k / 4.5f), 0.25f, 18 + k * 4);
            Sound("Item_1");
            DevCloseMove = move;
            DevCloseMoves++;
        }

        /// <summary>A fan of stabs over a moment (the Gladius's flurry): count, spread (radians), each blow's share.</summary>
        internal void CloseFlurry(ItemDef item, Vector2 aimWorld, int count, float spread, float share, string move)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var from = PlayerCenterPx(hero);
            var aim = TerraParticles.ToPx(aimWorld) - from;
            if (aim.sqrMagnitude < 1e-3f) aim = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            aim.Normalize();
            float side = aim.x < 0f ? -1f : 1f;
            int type = StabType(item.Type);
            for (int k = 0; k < count; k++)
            {
                float a = spread * ((k % 2 == 0 ? 1f : -1f) * (0.25f + 0.75f * Random.value));
                var c = NewStab(item, type, move, Equipment.Damage(item) * share, Equipment.Crit(item), Equipment.Knockback(item) * 0.5f, aim, side, a, a * 0.6f, RapierTip * CloseReach * (0.85f + 0.3f * Random.value), 12, k * 3);
                c.Aimed = true;
            }
            DevCloseMove = move;
            DevCloseMoves++;
        }

        /// <summary>Blades thrown at the cursor in a fan (count, spread, each blow's share): they fly, drop a little, stick in a wall.</summary>
        internal void CloseDaggers(ItemDef item, Vector2 aimWorld, int count, float spread, float share)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var from = PlayerCenterPx(hero);
            var aim = TerraParticles.ToPx(aimWorld) - from;
            if (aim.sqrMagnitude < 1e-3f) aim = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            aim.Normalize();
            for (int k = 0; k < count; k++)
            {
                float a = count == 1 ? 0f : Mathf.Lerp(-spread, spread, k / (float)(count - 1));
                var v = Rotate(aim, a) * 5.5f;
                var p = Add(Kind.CloseArt, 0, from + v * 2f, v, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(item) * share)), Equipment.Knockback(item) * 0.6f, Equipment.Crit(item), 0f, 1f, 1);
                closes[p] = new Close { What = CloseWhat.Dagger, Item = item, Art = CloseArtOf(item), Move = "飞刃", Life = 70, Vel = v, Delay = k * 2, Aim = aim };
            }
            Sound("Item_1");
            DevCloseMove = "飞刃";
            DevCloseMoves++;
        }

        /// <summary>The Ruler's measure: a line drawn out to the cursor (up to a wall), its ticks laid along it, then all on it struck.</summary>
        internal void CloseMeasure(ItemDef item, Vector2 aimWorld, float damageScale)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var from = PlayerCenterPx(hero);
            var aim = TerraParticles.ToPx(aimWorld) - from;
            float len = Mathf.Clamp(aim.magnitude, 48f, 14f * 16f);
            if (aim.sqrMagnitude < 1e-3f) aim = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            aim.Normalize();
            len = WallReach(from, aim, len);
            var p = Add(Kind.CloseArt, 842, from, aim, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(item) * damageScale)), Equipment.Knockback(item), Equipment.Crit(item), 0f, 1f, -1);
            closes[p] = new Close { What = CloseWhat.Line, Item = item, Art = CloseArtOf(item), Move = "量天尺", Life = 44, From = from, To = from + aim * len, Aim = aim };
            Sound("Item_7");
            DevCloseMove = "量天尺";
            DevCloseMoves++;
        }

        /// <summary>A spore cloud at a point: its share of the weapon's damage every 24 updates for its life.</summary>
        void Spore(ItemDef item, Vector2 at, float share, int delay)
        {
            var p = Add(Kind.CloseArt, 0, at, Vector2.zero, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(item) * share)), 0.5f, Equipment.Crit(item), 0f, 1f, -1);
            p.Cooldown = 24;
            closes[p] = new Close { What = CloseWhat.Spore, Item = item, Art = CloseArtOf(item), Move = "孢子", Life = 240, Delay = delay, From = at };
        }

        /// <summary>The umbrella thrown spinning out to the cursor (7 tiles at most, a wall nearer) and back to the hand. False with one out.</summary>
        internal bool CloseUmbrellaThrow(ItemDef item, Vector2 aimWorld)
        {
            foreach (var kv in closes) if (kv.Value.What == CloseWhat.Umbrella && !kv.Key.Dead) return false;
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            var from = PlayerCenterPx(hero) + new Vector2(0f, -10f);
            var aim = TerraParticles.ToPx(aimWorld) - from;
            if (aim.sqrMagnitude < 1e-3f) aim = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            float len = WallReach(from, aim.normalized, Mathf.Clamp(aim.magnitude, 64f, 7f * 16f));
            var p = Add(Kind.CloseArt, item.Type == ItemType.TragicUmbrella ? 4707 : 946, from, aim.normalized * 9f, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(item) * 1.1f)),
                Equipment.Knockback(item), Equipment.Crit(item), 0f, 1f, -1);
            closes[p] = new Close { What = CloseWhat.Umbrella, Item = item, Art = CloseArtOf(item), Move = "回旋伞", Life = 400, From = from, To = from + aim.normalized * len, Vel = aim.normalized * 9f };
            Sound("Item_7");
            DevCloseMove = "回旋伞";
            DevCloseMoves++;
            return true;
        }

        /// <summary>The Tragic Umbrella's rain cloud over the cursor: three seconds of rain, all under it struck every 0.35 s for the share.</summary>
        internal void CloseRainCloud(ItemDef item, Vector2 aimWorld, float share)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var at = TerraParticles.ToPx(aimWorld);
            var me = PlayerCenterPx(hero);
            var d = at - me;
            if (d.magnitude > 20f * 16f) at = me + d.normalized * 20f * 16f;
            at.y -= 72f;
            var p = Add(Kind.CloseArt, 0, at, Vector2.zero, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(item) * share)), 0.5f, Equipment.Crit(item), 0f, 1f, -1);
            p.Cooldown = 21;
            closes[p] = new Close { What = CloseWhat.Cloud, Item = item, Art = CloseArtOf(item), Move = "悲雨", Life = 360, From = at };
            Sound("Item_66");
            DevCloseMove = "悲雨";
            DevCloseMoves++;
        }

        /// <summary>How far along a way from a point (px) before a wall, at most that far.</summary>
        static float WallReach(Vector2 from, Vector2 way, float far)
        {
            var a = TerraParticles.ToWorld(from);
            var b = TerraParticles.ToWorld(from + way * far);
            var hit = Physics2D.Linecast(a, b, (1 << 8) | (1 << ChunkColliders.TerrainLayer));
            if (hit.collider != null) far = Mathf.Max(12f, Vector2.Distance(a, hit.point) / Px - 4f);
            for (float s = 8f; s < far; s += 8f)
                if (BlockedPx(from + way * s, 0f)) return Mathf.Max(12f, s - 4f);
            return far;
        }

        // ------------------------------------------------------------------ the pieces' updates (two a tick)

        void TickClose(Proj p, HeroController hero)
        {
            if (!closes.TryGetValue(p, out var c)) { p.Dead = true; return; }
            for (int u = 0; u < 2 && !p.Dead; u++) StepClose(p, c, hero);
            if (p.Dead) closes.Remove(p);
        }

        void StepClose(Proj p, Close c, HeroController hero)
        {
            if (c.Delay > 0)
            {
                c.Delay--;
                // A rapier's later stab aimed where the cursor is when it comes, only while the same use goes on (the game's).
                if (c.Delay == 0 && c.Aimed && c.What == CloseWhat.Stab)
                {
                    var mod = TerraMod.Instance;
                    if (c.Move != "罗马连刺" && (mod == null || mod.SwingItem != c.Item)) { p.Dead = true; return; }
                    if (mod != null)
                    {
                        var aimPx = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint) - PlayerCenterPx(hero);
                        if (aimPx.sqrMagnitude > 1e-3f) { c.Aim = aimPx.normalized; c.Side = c.Aim.x < 0f ? -1f : 1f; }
                    }
                }
                if (c.Delay > 0) return;
            }
            c.Age++;
            switch (c.What)
            {
                case CloseWhat.Stab: StepCloseStab(p, c, hero); break;
                case CloseWhat.Dagger: StepDagger(p, c); break;
                case CloseWhat.Line: StepLine(p, c); break;
                case CloseWhat.Spore: StepSpore(p, c); break;
                case CloseWhat.Umbrella: StepUmbrella(p, c, hero); break;
                case CloseWhat.Cloud: StepCloud(p, c); break;
            }
            if (c.Age >= c.Life) p.Dead = true;
        }

        /// <summary>How far out a stab's centre is (0..1) at its time: quickly out, held, a little back.</summary>
        static float StabOut(float u) => u < 0.5f ? 1f - Mathf.Pow(1f - u / 0.5f, 3f) : u < 0.75f ? 1f : Mathf.Lerp(1f, 0.55f, (u - 0.75f) / 0.25f);

        void StepCloseStab(Proj p, Close c, HeroController hero)
        {
            float u = c.Age / (float)c.Life;
            float ease = u < 0.6f ? Mathf.SmoothStep(0f, 1f, u / 0.6f) : 1f;
            float turn = Mathf.Lerp(c.A0, c.A1, ease) * c.Side;
            var dir = Rotate(c.Aim, turn);
            var centre = PlayerCenterPx(hero);
            float tipNow = c.Reach * StabOut(u);
            var tip = centre + dir * tipNow;
            p.Velocity = dir * 2f;
            p.Rotation = Mathf.Atan2(dir.y, dir.x) + Pi / 2f;
            p.Center = tip - dir * 12f;
            p.Opacity = Mathf.Clamp01(c.Age / 3f) * Mathf.Clamp01((c.Life - c.Age) / 4f);
            c.Trail.Add(tip);
            if (c.Trail.Count > 7) c.Trail.RemoveAt(0);
            DevCloseFar = Mathf.Max(DevCloseFar, tipNow);
            // Motes off the point and along the blade, more as it goes out (a finisher's twice as many); a metal blade's
            // sparks flying off the point.
            int motes = c.Finisher ? 3 : 2;
            for (int k = 0; k < motes; k++)
                if (Random.value < 0.75f) Mote(c.Art.Theme, c.Art, tip - dir * Random.value * (c.Finisher ? 26f : 14f), dir * (1.2f + Random.value) + new Vector2(-dir.y, dir.x) * Random.Range(-0.8f, 0.8f), c.Side, false);
            if ((c.Art.Theme == BladeArts.Theme.Steel || c.Art.Theme == BladeArts.Theme.Coin || c.Art.Theme == BladeArts.Theme.Wood) && u < 0.6f && Random.value < (c.Finisher ? 0.9f : 0.55f))
            {
                int i = Dust(tip, 278, dir * Random.Range(1.5f, 3.5f) + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-1.6f, -0.2f)), 0, c.Finisher ? 0.7f : 0.55f, false, new Color(1f, 0.85f, 0.5f));
                FadeIn(i, 0.5f);
            }
            // At its farthest: its streak and its blade left there a moment, fading.
            if (!c.Peaked && u >= 0.5f)
            {
                c.Peaked = true;
                var art = c.Art;
                var glow = Color.Lerp(BladeArts.Tint(art, art.Main, 0f), BladeArts.Tint(art, art.Light, 0.1f), 0.5f);
                closeGhosts.Add(new CloseGhost { A = centre + dir * 8f, B = tip + dir * (c.Finisher ? 14f : 8f), Tint = glow, Thick = c.Thick * 0.9f, Life = c.Finisher ? 0.22f : 0.17f });
                if (c.Finisher) closeGhosts.Add(new CloseGhost { A = centre + dir * 14f, B = tip + dir * 10f, Tint = Color.white, Thick = c.Thick * 0.35f, Life = 0.2f });
                bool diag = (p.Type >= 938 && p.Type <= 945) || p.Type == 1127;
                float sd = dir.x < 0f ? -1f : 1f;
                closeGhosts.Add(new CloseGhost { Blade = FxBatch.Whole("Projectile_" + p.Type), A = p.Center, Rot = p.Rotation - (diag ? Pi / 4f * sd : 0f), Flip = sd < 0f,
                    Scale = c.Finisher ? 1.35f : 1.12f, Tint = BladeArts.Tint(art, art.Light, 0.1f), Life = c.Finisher ? 0.2f : 0.15f });
                // A finisher's motes carried on past the point, the way it thrust.
                if (c.Finisher)
                    for (int k = 0; k < 6; k++) Mote(c.Art.Theme, c.Art, tip + dir * Random.value * 10f, dir * Random.Range(2f, 4f) + new Vector2(-dir.y, dir.x) * Random.Range(-0.6f, 0.6f), c.Side, false);
            }
            if (c.Age >= c.Life - 1 && c.Trail.Count > 1)
                for (int k = 1; k < c.Trail.Count; k++)
                    closeGhosts.Add(new CloseGhost { A = c.Trail[k - 1], B = c.Trail[k], Tint = BladeArts.Tint(c.Art, c.Art.Light, 0.1f), Thick = (5f + 6f * k / c.Trail.Count) * (c.Finisher ? 1.5f : 1f), Life = 0.15f });
            if (p.Type == 1127)
            {
                Fx.Light(p.Center, FxBatch.C(32, 108, 0));
                if (Random.value < 0.6f) { int i = Dust(tip - dir * Random.value * 16f, 157, dir * Random.value * 2f, 100, 1f + Random.value * 0.4f, true); FadeIn(i, 1f); }
            }
            if (p.Type == 842 && c.Age == 4)
                for (float s = 10f; s < tipNow; s += 8f) { int i = Dust(centre + dir * s, 278, Vector2.zero, 60, 0.45f, true, new Color(1f, 0.9f, 0.45f)); FadeIn(i, 0.5f); }
            if (c.Age > c.Life * 0.85f) return;
            HitCloseLine(p, c, centre + dir * 8f, tip, c.Pierce ? 18f : 12f, true);
        }

        /// <summary>Every enemy along a line struck once (by its piece), only those the player sees when it is a blade in his hand.</summary>
        void HitCloseLine(Proj p, Close c, Vector2 a, Vector2 b, float width, bool needsSight)
        {
            var wa = TerraParticles.ToWorld(a);
            var wb = TerraParticles.ToWorld(b);
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            float len = Vector2.Distance(wa, wb);
            if (len < 1e-3f) return;
            Physics2D.OverlapBox((wa + wb) / 2f, new Vector2(len, width * Px), Mathf.Atan2(wb.y - wa.y, wb.x - wa.x) * Mathf.Rad2Deg, filter, overlaps);
            for (int k = 0; k < overlaps.Count; k++)
            {
                if (p.Penetrate == 0) return;
                var col = overlaps[k];
                if (needsSight && !CharmEffects.MeleeReaches(col)) continue;
                CloseStrike(p, c, col);
            }
        }

        void HitCloseBox(Proj p, Close c, Vector2 at, Vector2 size)
        {
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapBox(TerraParticles.ToWorld(at), size * Px, 0f, filter, overlaps);
            for (int k = 0; k < overlaps.Count; k++)
            {
                if (p.Penetrate == 0) return;
                CloseStrike(p, c, overlaps[k]);
            }
        }

        void CloseStrike(Proj p, Close c, Collider2D col)
        {
            var hm = Strike(p, col);
            if (hm == null) return;
            DevCloseHits++;
            var at = TerraParticles.ToPx(col.bounds.ClosestPoint(TerraParticles.ToWorld(p.Center)));
            float facing = c.Side;
            // The theme's burst thrown the way of the blow; a metal blade's sparks.
            int n = c.Finisher ? 12 : 8;
            for (int k = 0; k < n; k++) Mote(c.Art.Theme, c.Art, at, new Vector2(facing * (1f + Random.value * 3f), (Random.value - 0.65f) * 4f) * 0.7f, facing, true);
            if (c.Art.Theme == BladeArts.Theme.Steel || c.Art.Theme == BladeArts.Theme.Coin || c.Art.Theme == BladeArts.Theme.Wood)
                for (int k = 0; k < (c.Finisher ? 9 : 6); k++) { int i = Dust(at, 278, new Vector2(facing * Random.Range(1f, 4f), Random.Range(-3.5f, -0.5f)), 0, 0.6f, false, new Color(1f, 0.85f, 0.5f)); FadeIn(i, 0.5f); }
        }

        void StepDagger(Proj p, Close c)
        {
            if (c.Returning)
            {
                // Stuck in a wall: a moment there, fading.
                p.Opacity = Mathf.Clamp01((c.Life - c.Age) / 20f);
                return;
            }
            if (c.Age > 14) c.Vel.y += 0.18f;
            c.Vel *= 0.995f;
            p.Center += c.Vel;
            p.Velocity = c.Vel;
            p.Rotation = Mathf.Atan2(c.Vel.y, c.Vel.x) + Pi / 4f;
            p.Opacity = 1f;
            c.Trail.Add(p.Center);
            if (c.Trail.Count > 6) c.Trail.RemoveAt(0);
            if (Random.value < 0.35f) Mote(c.Art.Theme, c.Art, p.Center, c.Vel * 0.1f, c.Vel.x < 0f ? -1f : 1f, false);
            if (BlockedPx(p.Center + c.Vel.normalized * 6f, 0f))
            {
                c.Returning = true;
                c.Life = c.Age + 24;
                p.Penetrate = 0;
                for (int k = 0; k < 4; k++) { int i = Dust(p.Center, 278, new Vector2(-c.Vel.x * 0.2f + Random.Range(-1f, 1f), Random.Range(-2.5f, -0.5f)), 0, 0.5f, false, new Color(1f, 0.85f, 0.5f)); FadeIn(i, 0.4f); }
                Sound("Tink_0", 0.5f);
                return;
            }
            c.Side = c.Vel.x < 0f ? -1f : 1f;
            HitCloseBox(p, c, p.Center, new Vector2(16f, 16f));
            if (p.Penetrate == 0) { c.Returning = true; c.Life = c.Age + 10; }
        }

        void StepLine(Proj p, Close c)
        {
            float len = Vector2.Distance(c.From, c.To);
            float drawn = Mathf.Clamp01(c.Age / 14f) * len;
            // Its ticks laid as it goes out: one a tile, a longer one every fifth.
            int had = Mathf.FloorToInt(Mathf.Clamp01((c.Age - 1) / 14f) * len / 16f), now = Mathf.FloorToInt(drawn / 16f);
            var across = new Vector2(-c.Aim.y, c.Aim.x);
            for (int k = had + 1; k <= now; k++)
            {
                var at = c.From + c.Aim * (k * 16f);
                float tall = k % 5 == 0 ? 7f : 4f;
                for (float s = -tall; s <= tall; s += 2.5f) { int i = Dust(at + across * s, 278, Vector2.zero, 40, 0.42f, true, new Color(1f, 0.92f, 0.5f)); FadeIn(i, 0.45f); }
            }
            if (c.Age == 16)
            {
                c.Side = c.Aim.x < 0f ? -1f : 1f;
                c.Finisher = true;
                HitCloseLine(p, c, c.From, c.To, 22f, false);
                Sound("Item_4", 0.6f);
            }
        }

        void StepSpore(Proj p, Close c)
        {
            p.Opacity = Mathf.Clamp01(c.Age / 10f) * Mathf.Clamp01((c.Life - c.Age) / 30f);
            if (Random.value < 0.5f) { int i = Dust(c.From + Random.insideUnitCircle * 18f, 157, Random.insideUnitCircle * 0.5f + new Vector2(0f, -0.3f), 100, 1f + Random.value * 0.6f, true); FadeIn(i, 1.2f); }
            c.Side = 1f;
            HitCloseBox(p, c, c.From, new Vector2(36f, 36f));
        }

        void StepUmbrella(Proj p, Close c, HeroController hero)
        {
            c.Spin += 0.35f;
            var home = PlayerCenterPx(hero) + new Vector2(0f, -10f);
            if (!c.Returning)
            {
                p.Center = Vector2.MoveTowards(p.Center, c.To, 5.5f);
                // At the far end it hangs a moment, turning, before it comes back.
                if ((p.Center - c.To).sqrMagnitude < 1f && ++c.Delay2 > 12) { c.Returning = true; p.Hit.Clear(); }
            }
            else
            {
                p.Center = Vector2.MoveTowards(p.Center, home, 6.5f);
                if ((p.Center - home).sqrMagnitude < 144f) { p.Dead = true; Sound("Grab", 0.5f); return; }
            }
            c.Side = (c.Returning ? home.x - p.Center.x : c.To.x - c.From.x) < 0f ? -1f : 1f;
            p.Opacity = 1f;
            bool tragic = c.Item.Type == ItemType.TragicUmbrella;
            // Water flung off the turning canopy.
            if (Random.value < 0.8f)
            {
                float a = c.Spin + Random.value * Pi * 2f;
                var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f;
                int i = Dust(p.Center + off, tragic ? 278 : 33, off.normalized * 1.6f + new Vector2(0f, 0.4f), tragic ? 60 : 0, tragic ? 0.8f : 1f, false, tragic ? new Color(0.35f, 0.4f, 0.55f) : default);
                FadeIn(i, 0.7f);
            }
            HitCloseBox(p, c, p.Center, new Vector2(34f, 34f));
            if (c.Age >= c.Life - 1) c.Life++;
        }

        void StepCloud(Proj p, Close c)
        {
            p.Opacity = Mathf.Clamp01(c.Age / 20f) * Mathf.Clamp01((c.Life - c.Age) / 30f);
            // The rain: grey-blue drops from the cloud's underside.
            for (int k = 0; k < 3; k++)
            {
                if (Random.value > 0.75f) continue;
                var at = c.From + new Vector2(Random.Range(-48f, 48f), 14f);
                int i = Dust(at, 33, new Vector2(0f, 5f + Random.value * 2f), 0, 0.9f + Random.value * 0.3f, true);
                if (i >= 0 && TerraParticles.Instance != null) TerraParticles.Instance[i].Color = new Color(0.55f, 0.6f, 0.75f);
            }
            if (Random.value < 0.15f) { int i = Dust(c.From + new Vector2(Random.Range(-36f, 36f), Random.Range(-8f, 6f)), 31, Vector2.zero, 120, 1.4f, true, new Color(0.3f, 0.32f, 0.4f)); FadeIn(i, 1f); }
            c.Side = 1f;
            if (c.Age > 20 && c.Age < c.Life - 20) HitCloseBox(p, c, c.From + new Vector2(0f, 110f), new Vector2(84f, 220f));
        }

        // ------------------------------------------------------------------ drawing

        void DrawClose(FxBatch fx, Proj p)
        {
            if (!closes.TryGetValue(p, out var c) || c.Delay > 0) return;
            var art = c.Art;
            var main = BladeArts.Tint(art, art.Main, 0f);
            var light = BladeArts.Tint(art, art.Light, 0.1f);
            switch (c.What)
            {
                case CloseWhat.Stab:
                {
                    var hero = HeroController.SilentInstance;
                    if (hero == null) return;
                    var dir = p.Velocity.sqrMagnitude > 1e-4f ? p.Velocity.normalized : c.Aim;
                    var tip = c.Trail.Count > 0 ? c.Trail[c.Trail.Count - 1] : p.Center;
                    var root = PlayerCenterPx(hero) + dir * 8f;
                    float f = p.Opacity;
                    // The streak along the blade, a white core for a finisher; the point's path a moment behind it.
                    var reachOut = tip + dir * (c.Finisher ? 14f : 8f);
                    Streak(fx, root, reachOut, Color.Lerp(main, light, 0.5f), c.Thick, 0.8f * f);
                    Streak(fx, root + dir * 6f, reachOut, Color.white, c.Thick * (c.Finisher ? 0.4f : 0.25f), (c.Finisher ? 0.7f : 0.45f) * f);
                    for (int k = 1; k < c.Trail.Count; k++)
                    {
                        float g = k / (float)c.Trail.Count;
                        Streak(fx, c.Trail[k - 1], c.Trail[k], light, (6f + 8f * g) * (c.Finisher ? 1.5f : 1f), 0.65f * g * f);
                    }
                    // The blade, its afterimages behind.
                    float sd = dir.x < 0f ? -1f : 1f;
                    bool diagonal = (p.Type >= 938 && p.Type <= 945) || p.Type == 1127;
                    var sprite = FxBatch.Whole("Projectile_" + p.Type);
                    float rot = p.Rotation - (diagonal ? Pi / 4f * sd : 0f);
                    float bs = c.Finisher ? 1.3f : 1.08f;
                    for (int k = 0; k < c.Trail.Count - 1; k++)
                    {
                        float g = (k + 1f) / c.Trail.Count;
                        fx.Draw(sprite, c.Trail[k] - dir * 12f, Glow(light, 0.4f * g * f), rot, bs, flipX: sd < 0f);
                    }
                    fx.Draw(sprite, p.Center, Glow(light, 0.45f * f), rot, bs * 1.12f, flipX: sd < 0f);
                    fx.Draw(sprite, p.Center, new Color(L, L, L, 1f) * f, rot, bs, flipX: sd < 0f);
                    if (c.Star && c.Age > c.Life * 0.3f)
                        fx.Sparkle(0.7f, tip, new Color(1f, 1f, 1f, 0f) * (f * 0.6f), light, Mathf.Clamp01((c.Age - c.Life * 0.3f) / (c.Life * 0.7f)), 0f, 0.2f, 0.5f, 1f, Pi / 4f, new Vector2(0.55f, 0.55f), new Vector2(0.6f, 0.6f));
                    break;
                }
                case CloseWhat.Dagger:
                {
                    var sprite = FxBatch.Whole(c.Item.Texture);
                    DevCloseSprite = $"{c.Item.Texture} {sprite?.rect.size} 在 {p.Center:F0} 不透明 {p.Opacity:F2}";
                    for (int k = 1; k < c.Trail.Count; k++)
                    {
                        float g = k / (float)c.Trail.Count;
                        Streak(fx, c.Trail[k - 1], c.Trail[k], Color.Lerp(main, light, g), 6f * g, 0.6f * g * p.Opacity);
                    }
                    fx.Draw(sprite, p.Center, new Color(L, L, L, 1f) * p.Opacity, p.Rotation, 1f);
                    break;
                }
                case CloseWhat.Line:
                {
                    float len = Vector2.Distance(c.From, c.To);
                    float drawn = Mathf.Clamp01(c.Age / 14f) * len;
                    float fade = c.Age < 16 ? 1f : Mathf.Clamp01(1f - (c.Age - 16) / (float)(c.Life - 16));
                    var end = c.From + c.Aim * drawn;
                    var gold = new Color(1f, 0.85f, 0.4f);
                    Streak(fx, c.From, end, gold, c.Age >= 16 ? 16f * fade + 4f : 6f, (c.Age >= 16 ? 0.8f : 0.5f) * fade);
                    if (c.Age >= 16) Streak(fx, c.From, end, Color.white, 4f * fade, 0.6f * fade);
                    break;
                }
                case CloseWhat.Umbrella:
                {
                    var sprite = FxBatch.Whole(c.Item.Texture);
                    DevCloseSprite = $"{c.Item.Texture} {sprite?.rect.size} 在 {p.Center:F0}";
                    for (int k = 2; k >= 1; k--)
                        fx.Draw(sprite, p.Center, Glow(main, 0.2f / k), c.Spin - k * 0.35f, 1f, overPlayer: true);
                    fx.Draw(sprite, p.Center, new Color(L, L, L, 1f), c.Spin, 1f, overPlayer: true);
                    break;
                }
                case CloseWhat.Cloud:
                {
                    // The Nimbus Rod's raining cloud (238, six frames), greyer for the tragic one.
                    var whole = FxBatch.Whole("Projectile_238");
                    if (whole == null) break;
                    int w = (int)whole.rect.width, fh = (int)whole.rect.height / 6;
                    int frame = (c.Age / 8) % 6;
                    var spr = FxBatch.Frame("Projectile_238", 0, frame * fh, w, fh, w / 2f, fh / 2f);
                    fx.Draw(spr, c.From, new Color(0.5f, 0.52f, 0.62f, 1f) * p.Opacity, 0f, 1.7f);
                    break;
                }
            }
        }

        /// <summary>The thrown umbrella out: the arm's way toward it (degrees from level for the way he faces, + up), for BladeSkills.Pose.</summary>
        internal bool CloseUmbrellaAngle(float facing, out float deg)
        {
            deg = 0f;
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            foreach (var kv in closes)
            {
                if (kv.Key.Dead || kv.Value.What != CloseWhat.Umbrella) continue;
                var d = kv.Key.Center - (PlayerCenterPx(hero) + new Vector2(-facing, -10f));
                deg = Mathf.Atan2(-d.y, Mathf.Abs(d.x) < 1e-3f ? 1e-3f : d.x * facing) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        /// <summary>A close piece of the shortsword in the hand now (a stab of a technique): its way, for the player's arm; zero when none.</summary>
        Vector2 CloseStabDirection()
        {
            foreach (var kv in closes)
                if (!kv.Key.Dead && kv.Value.What == CloseWhat.Stab && kv.Value.Delay == 0) return kv.Key.Velocity;
            return Vector2.zero;
        }
    }
}
