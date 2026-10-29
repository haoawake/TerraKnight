using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The spears' techniques (the user's 10-26 "长矛的戳刺类，可以多几种枪法…更好的粒子特效和戳刺特效，范围也搞大一点"): with the
    /// remade swings on (BladeArts.Enabled) a spear's thrust (AI_019) is one technique of its own a use, the next while the
    /// uses follow on (within half a second), the first again after a pause; early spears have one or two, the spears of the
    /// end four. 扎枪 a thrust drawn back first; 点枪 three quick jabs; 拦拿扎 the tip circling twice, then thrust; 挑枪 a low
    /// thrust lifted up; 劈枪 raised and brought down; 扫枪 swept across; 枪花 spun once round the hand, then thrust; 崩枪
    /// dropped low and snapped up; 螺旋刺 a long drilling thrust. None moves the player. The spear is its own picture (the
    /// projectile's), reaching 35% further than the game's thrust, its head 40% bigger and its shaft's upper half striking too;
    /// an enemy is struck at most every 10 updates as by the game's thrust, a blow at 10% more than its damage (by its weight). Its look: afterimages of
    /// the spear in its colours, a streak ahead of a thrust, a ribbon behind a sweep, motes of its theme off the head
    /// (BladeArts' themes: the tridents' water, the Dark Lance's shadowflame, Gungnir's holy gold, the North Pole's snow...)
    /// and a burst where it strikes. Its own shots (the mushrooms, the spore cloud, the ice spear, the bolt) as ever.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>
        /// A stretch of a technique: from T0 to T1 of the use, the head from E0 to E1 of the reach and the way from A0 to A1
        /// (degrees off the aim, + up), eased (Ease: 0 smooth, 1 fast then slowing, 2 slow then fast, 3 even); out to Peak and
        /// back if Peak > 0 (a jab); circling Turns times round its way (CircleA degrees, CircleE of the reach) if Turns > 0.
        /// Fresh: a new blow (each enemy struck once more), at Share of the spear's damage; Harmless: a wind-up.
        /// </summary>
        sealed class SpearPhase
        {
            public float T0, T1, E0, E1, A0, A1, Peak, CircleA, CircleE, Turns, Share = 1f;
            public int Ease;
            public bool Fresh, Harmless;
        }

        sealed class SpearMove
        {
            public string Name;
            public SpearPhase[] Phases;
            /// <summary>A drilling thrust's spiral of motes.</summary>
            public bool Spiral;
        }

        static SpearPhase Ph(float t0, float t1, float e0, float e1, float a0 = 0f, float a1 = 0f, int ease = 0, bool fresh = false, float share = 1f, bool harmless = false) =>
            new SpearPhase { T0 = t0, T1 = t1, E0 = e0, E1 = e1, A0 = a0, A1 = a1, Ease = ease, Fresh = fresh, Share = share, Harmless = harmless };

        static readonly Dictionary<string, SpearMove> spearMoves = new Dictionary<string, SpearMove>
        {
            ["扎枪"] = new SpearMove { Name = "扎枪", Phases = new[]
            {
                Ph(0f, 0.18f, 0.35f, 0.2f, harmless: true), Ph(0.18f, 0.45f, 0.2f, 1f, ease: 1, fresh: true, share: 1.05f),
                Ph(0.45f, 0.6f, 1f, 1f), Ph(0.6f, 1f, 1f, 0.3f),
            } },
            ["点枪"] = new SpearMove { Name = "点枪", Phases = new[]
            {
                Ph(0f, 0.1f, 0.35f, 0.25f, harmless: true),
                new SpearPhase { T0 = 0.1f, T1 = 0.37f, E0 = 0.25f, E1 = 0.25f, Peak = 0.82f, A0 = 8f, A1 = 8f, Fresh = true, Share = 0.85f },
                new SpearPhase { T0 = 0.37f, T1 = 0.64f, E0 = 0.25f, E1 = 0.25f, Peak = 0.82f, A0 = -6f, A1 = -6f, Fresh = true, Share = 0.85f },
                new SpearPhase { T0 = 0.64f, T1 = 1f, E0 = 0.25f, E1 = 0.3f, Peak = 0.98f, Fresh = true, Share = 0.95f },
            } },
            ["拦拿扎"] = new SpearMove { Name = "拦拿扎", Phases = new[]
            {
                new SpearPhase { T0 = 0f, T1 = 0.45f, E0 = 0.45f, E1 = 0.45f, CircleA = 10f, CircleE = 0.08f, Turns = 2f, Fresh = true, Share = 0.75f },
                Ph(0.45f, 0.72f, 0.45f, 1.05f, ease: 1, fresh: true, share: 1f), Ph(0.72f, 1f, 1.05f, 0.3f),
            } },
            ["挑枪"] = new SpearMove { Name = "挑枪", Phases = new[]
            {
                Ph(0f, 0.12f, 0.35f, 0.25f, -12f, -12f, harmless: true), Ph(0.12f, 0.38f, 0.25f, 0.95f, -12f, -12f, 1, true, 1f),
                Ph(0.38f, 0.7f, 0.95f, 0.95f, -12f, 55f, 0, true, 0.9f), Ph(0.7f, 1f, 0.95f, 0.35f, 55f, 20f),
            } },
            ["劈枪"] = new SpearMove { Name = "劈枪", Phases = new[]
            {
                Ph(0f, 0.3f, 0.35f, 0.75f, 0f, 75f, 2, harmless: true), Ph(0.3f, 0.55f, 0.75f, 1f, 75f, -35f, 1, true, 1.3f),
                Ph(0.55f, 1f, 1f, 0.35f, -35f, -10f),
            } },
            ["扫枪"] = new SpearMove { Name = "扫枪", Phases = new[]
            {
                Ph(0f, 0.2f, 0.4f, 0.7f, 0f, 40f, harmless: true), Ph(0.2f, 0.62f, 0.7f, 1f, 40f, -30f, 0, true, 1.2f),
                Ph(0.62f, 1f, 1f, 0.35f, -30f, 0f),
            } },
            ["枪花"] = new SpearMove { Name = "枪花", Phases = new[]
            {
                Ph(0f, 0.6f, 0.55f, 0.55f, 0f, 360f, 3, true, 0.85f), Ph(0.6f, 0.82f, 0.55f, 1f, 360f, 360f, 1, true, 1f),
                Ph(0.82f, 1f, 1f, 0.35f, 360f, 360f),
            } },
            ["崩枪"] = new SpearMove { Name = "崩枪", Phases = new[]
            {
                Ph(0f, 0.2f, 0.4f, 0.6f, 0f, -40f, harmless: true), Ph(0.2f, 0.42f, 0.6f, 0.95f, -40f, 30f, 1, true, 1.1f),
                Ph(0.42f, 1f, 0.95f, 0.35f, 30f, 5f),
            } },
            // The right-click arts' (BladeSkillsSpears): a charged piercing thrust, the trident's three, the whirlwind, held
            // out through a run.
            ["贯穿"] = new SpearMove { Name = "贯穿", Spiral = true, Phases = new[]
            {
                Ph(0f, 0.22f, 0.3f, 0.1f, harmless: true), Ph(0.22f, 0.45f, 0.1f, 1.7f, ease: 1, fresh: true), Ph(0.45f, 0.62f, 1.7f, 1.7f), Ph(0.62f, 1f, 1.7f, 0.3f),
            } },
            ["三叉刺"] = new SpearMove { Name = "三叉刺", Phases = new[]
            {
                new SpearPhase { T0 = 0f, T1 = 0.33f, E0 = 0.3f, E1 = 0.3f, Peak = 1.15f, A0 = 15f, A1 = 15f, Fresh = true },
                new SpearPhase { T0 = 0.33f, T1 = 0.66f, E0 = 0.3f, E1 = 0.3f, Peak = 1.15f, Fresh = true },
                new SpearPhase { T0 = 0.66f, T1 = 1f, E0 = 0.3f, E1 = 0.3f, Peak = 1.15f, A0 = -15f, A1 = -15f, Fresh = true },
            } },
            ["旋风"] = new SpearMove { Name = "旋风", Phases = new[]
            {
                Ph(0f, 0.18f, 0.45f, 0.75f, 0f, 180f, 2, true), Ph(0.18f, 0.36f, 0.75f, 0.75f, 180f, 360f, 3, true), Ph(0.36f, 0.54f, 0.75f, 0.75f, 360f, 540f, 3, true),
                Ph(0.54f, 0.72f, 0.75f, 0.75f, 540f, 720f, 3, true), Ph(0.72f, 0.9f, 0.75f, 0.75f, 720f, 900f, 1, true), Ph(0.9f, 1f, 0.75f, 0.35f, 900f, 900f),
            } },
            ["冲锋"] = new SpearMove { Name = "冲锋", Phases = new[] { Ph(0f, 0.15f, 0.5f, 0.95f, ease: 1, fresh: true), Ph(0.15f, 0.8f, 0.95f, 0.95f), Ph(0.8f, 1f, 0.95f, 0.4f) } },
            ["螺旋刺"] = new SpearMove { Name = "螺旋刺", Spiral = true, Phases = new[]
            {
                Ph(0f, 0.2f, 0.35f, 0.15f, harmless: true), Ph(0.2f, 0.5f, 0.15f, 1.15f, ease: 1, fresh: true, share: 1.05f),
                Ph(0.5f, 0.65f, 1.15f, 1.15f, fresh: true, share: 0.95f), Ph(0.65f, 1f, 1.15f, 0.3f),
            } },
        };

        /// <summary>Each spear's techniques in turn, a use each (more of them stage by stage).</summary>
        static readonly Dictionary<ItemType, string> spearCombos = new Dictionary<ItemType, string>
        {
            [ItemType.Spear] = "扎枪 崩枪", [ItemType.Trident] = "扎枪 点枪", [ItemType.TheRottedFork] = "点枪 挑枪",
            [ItemType.Swordfish] = "扎枪 点枪", [ItemType.ObsidianSwordfish] = "点枪 扎枪 挑枪", [ItemType.SlimeSpear] = "扎枪 崩枪",
            [ItemType.DarkLance] = "拦拿扎 扎枪 劈枪", [ItemType.ThunderSpear] = "扎枪 点枪 扫枪",
            [ItemType.CobaltNaginata] = "扫枪 扎枪 劈枪", [ItemType.PalladiumPike] = "扎枪 点枪 拦拿扎", [ItemType.MythrilHalberd] = "劈枪 扫枪 扎枪",
            [ItemType.OrichalcumHalberd] = "扫枪 挑枪 扎枪", [ItemType.AdamantiteGlaive] = "扫枪 劈枪 扎枪 枪花", [ItemType.TitaniumTrident] = "点枪 拦拿扎 扎枪",
            [ItemType.Gungnir] = "拦拿扎 扎枪 挑枪 螺旋刺", [ItemType.MushroomSpear] = "点枪 扫枪 扎枪 枪花",
            [ItemType.ChlorophytePartisan] = "枪花 扎枪 扫枪 螺旋刺", [ItemType.NorthPole] = "扫枪 扎枪 枪花 拦拿扎",
        };

        /// <summary>The remade spear's reach and head over the game's, and its damage a use.</summary>
        public const float SpearArtReach = 1.35f, SpearArtHead = 1.4f, SpearArtBoost = 1.1f;

        /// <summary>A spear under way with the remade swings: its technique, where it is in it, its aim and reach.</summary>
        sealed class SpearArt
        {
            public ItemType Item;
            public SpearMove Move;
            public BladeArts.Art Art;
            public int Phase = -1, BaseDamage;
            public Vector2 Aim;
            public float Side, Speed, ReachPx, Ext, DamageScale = 1f;
            public bool Harmless, Skill;
            /// <summary>The head's last places (world) and ways, newest last, for its afterimages and ribbon.</summary>
            public readonly List<Vector2> Tips = new List<Vector2>(), Ways = new List<Vector2>();
        }

        static readonly Dictionary<ProjKind, ItemType> spearItems = new Dictionary<ProjKind, ItemType>();

        static ItemType SpearItemOf(ProjKind kind)
        {
            if (spearItems.Count == 0)
                foreach (var d in ItemDb.All)
                    if (d != null && d.HoldOut && d.Shoot != ProjKind.None && specs.TryGetValue(d.Shoot, out var s) && s.Ai == Ai.Spear && !spearItems.ContainsKey(d.Shoot))
                        spearItems[d.Shoot] = d.Type;
            return spearItems.TryGetValue(kind, out var t) ? t : ItemType.None;
        }

        /// <summary>Dev (suite newmeleesave): every item held out as a spear.</summary>
        public static List<ItemType> DevSpearItems()
        {
            SpearItemOf(ProjKind.None);
            return new List<ItemType>(spearItems.Values);
        }

        /// <summary>The spears' techniques in turn: the last spear's, when it ended.</summary>
        static ItemType spearLastItem;
        static int spearLastMove = -1;
        static float spearLastEnd = -10f;

        /// <summary>Dev: the last technique begun, techniques begun, blows struck with a technique.</summary>
        public static string DevSpearMove = "";
        public static int DevSpearMoves, DevSpearBlows;
        /// <summary>Dev: the farthest the head has gone (px from the player's middle), and the game's own thrust's reach for that spear.</summary>
        public static float DevSpearFar, DevSpearVanilla;

        /// <summary>A spear's technique as its use begins (the remade swings on, a spear with techniques), else null.</summary>
        SpearArt BeginSpearArt(Proj p)
        {
            if (!BladeArts.Enabled) return null;
            var item = SpearItemOf(p.Kind);
            if (item == ItemType.None || !spearCombos.TryGetValue(item, out var combo)) return null;
            SpearMove move;
            bool forced = forcedMove != null;
            if (forced)
            {
                // A right-click art's (SkillSpear): its move, the combo where it was.
                if (!spearMoves.TryGetValue(forcedMove, out move)) return null;
            }
            else
            {
                var names = combo.Split(' ');
                int next = item == spearLastItem && spearLastMove >= 0 && Time.time - spearLastEnd < 0.5f ? (spearLastMove + 1) % names.Length : 0;
                spearLastItem = item;
                spearLastMove = next;
                spearLastEnd = Time.time + 999f;
                if (!spearMoves.TryGetValue(names[next], out move)) return null;
            }
            var off = p.Spec.SpearOffset == Vector3.zero ? new Vector3(4f, 0.9f, 1.2f) : p.Spec.SpearOffset;
            // An art's reach goes by the spear's own use, however long the art lasts.
            int N = forced ? forcedBasis : Mathf.Max(1, (int)p.Ai1), T = N / 3;
            float speed = Mathf.Max(0.5f, p.Vel.magnitude);
            var a = new SpearArt
            {
                Item = item, Move = move, Art = BladeArts.Of(ItemDb.Get(item)), Aim = p.Vel / speed, Speed = speed,
                Side = p.Vel.x < 0f ? -1f : p.Vel.x > 0f ? 1f : p.Dir, BaseDamage = p.Damage,
                ReachPx = (off.x + off.y * (N - T)) * speed * SpearArtReach,
            };
            // Its head bigger; an enemy struck at most every 10 updates through the use, as the game's thrust strikes what it
            // stays on, each blow at its weight of 10% more than the spear's damage (an art's: what it is given, shared).
            p.HitSize = new Vector2((int)(p.Spec.W * p.Spec.Scale), (int)(p.Spec.H * p.Spec.Scale)) * SpearArtHead;
            p.Cooldown = 10;
            if (forced)
            {
                float weights = 0f;
                foreach (var ph in move.Phases) if (ph.Fresh) weights += ph.Share;
                a.DamageScale = forcedDamage / Mathf.Max(0.1f, weights);
            }
            // The quick spears of the end barely stay on an enemy in the game's thrust: the remade one, held out and wider, would
            // strike them far more often; their blows lighter by how fast the game's thrust goes out (off.y px a tick a speed).
            else a.DamageScale = SpearArtBoost / (1f + Mathf.Max(0f, off.y - 1.2f) * 0.62f);
            a.Skill = forced;
            DevSpearMove = move.Name;
            DevSpearMoves++;
            DevSpearVanilla = a.ReachPx / SpearArtReach;
            return a;
        }

        static float SpearEase(int ease, float u)
        {
            switch (ease)
            {
                case 1: return 1f - (1f - u) * (1f - u);
                case 2: return u * u;
                case 3: return u;
                default: return u * u * (3f - 2f * u);
            }
        }

        /// <summary>
        /// One update of a spear's technique: its stretch now (a new blow: the enemies struck may be struck again, at its share
        /// of the damage, the use's sound again, softer), the head's place on the way off the aim it is at, the motes off it.
        /// </summary>
        void StepSpearArt(Proj p, HeroController hero, SpearArt a)
        {
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            int N = Mathf.Max(1, (int)p.Ai1);
            float t = Mathf.Clamp01((p.Ai0 + 1f) / N);
            var phases = a.Move.Phases;
            int i = 0;
            while (i < phases.Length - 1 && t > phases[i].T1) i++;
            var ph = phases[i];
            if (i != a.Phase)
            {
                a.Phase = i;
                a.Harmless = ph.Harmless;
                if (ph.Fresh)
                {
                    // An art's blows are its own (each strikes again); a technique's go at the game's rhythm.
                    if (a.Skill) p.Immune.Clear();
                    p.Damage = Mathf.Max(1, Mathf.RoundToInt(a.BaseDamage * ph.Share * a.DamageScale));
                    if (i > 0) Sound(SpearSound(a.Item), 0.35f);
                }
            }
            float u = Mathf.Clamp01((t - ph.T0) / Mathf.Max(1e-4f, ph.T1 - ph.T0));
            float k = SpearEase(ph.Ease, u);
            float e = ph.Peak > 0f ? ph.E0 + (ph.Peak - ph.E0) * Mathf.Sin(Mathf.PI * u) + (ph.E1 - ph.E0) * u : Mathf.Lerp(ph.E0, ph.E1, k);
            float ang = Mathf.Lerp(ph.A0, ph.A1, k);
            if (ph.Turns > 0f)
            {
                float w = 2f * Mathf.PI * ph.Turns * u;
                ang += ph.CircleA * Mathf.Sin(w);
                e += ph.CircleE * (Mathf.Cos(w) - 1f) * 0.5f;
            }
            var way = Rotate(a.Aim, ang * a.Side);
            a.Ext = e * a.ReachPx;
            DevSpearFar = Mathf.Max(DevSpearFar, a.Ext);
            var before = p.Pos;
            p.Vel = way * a.Speed;
            p.Pos = centre + way * a.Ext * Px;
            p.Anim = N - 1 - (int)p.Ai0;
            a.Tips.Add(p.Pos);
            a.Ways.Add(way);
            if (a.Tips.Count > 8)
            {
                a.Tips.RemoveAt(0);
                a.Ways.RemoveAt(0);
            }
            // Motes of its theme off the head as it moves; a drilling thrust's spiral.
            float moved = (p.Pos - before).magnitude / Px;
            if (p.Ai0 > 0f && moved > 2.5f)
            {
                var tip = TerraParticles.ToPx(p.Pos);
                var push = Flip((p.Pos - before) / Px) * 0.25f;
                int n = moved > 9f ? 2 : 1;
                for (int m = 0; m < n; m++) SwordFx.SpearMote(a.Art, tip - Flip(way) * Random.value * 14f, push, a.Side);
                if (a.Move.Spiral)
                {
                    float spin = p.Ai0 * 0.9f;
                    var across = new Vector2(-way.y, way.x);
                    for (int m = 0; m < 2; m++)
                    {
                        float s = Mathf.Sin(spin + m * Mathf.PI) * 9f;
                        SwordFx.SpearMote(a.Art, tip - Flip(way) * (6f + m * 10f) + Flip(across) * s, Flip(across) * Mathf.Cos(spin + m * Mathf.PI) * 1.2f, a.Side);
                    }
                }
            }
        }

        static string SpearSound(ItemType t) => ItemDb.Get(t)?.UseSound ?? "Item_1";

        /// <summary>A blow of a spear's technique landing: its theme's burst there.</summary>
        static void SpearArtHit(Proj p, SpearArt a)
        {
            DevSpearBlows++;
            SwordFx.SpearHit(a.Art, TerraParticles.ToPx(p.Pos), a.Side);
        }

        /// <summary>The shaft's upper half strikes too (boxes back from the head along it), the remade spear.</summary>
        void SpearShaftOverlaps(Proj p, SpearArt a, Vector2 head, ContactFilter2D filter)
        {
            var back = -a.Ways[a.Ways.Count - 1];
            var box = head * 0.8f;
            for (float d = 0.15f; d <= 0.5f; d += 0.12f)
                AddOverlaps(p.Pos + back * a.Ext * d * Px, box, filter);
        }

        /// <summary>
        /// A remade spear's look under its picture: afterimages of the spear back along its last places in its colours, a
        /// streak ahead of a thrust going out, a ribbon behind a head that sweeps.
        /// </summary>
        static void DrawSpearArt(FxBatch fx, Proj p, SpearArt a, Sprite spear, bool right)
        {
            int n = a.Tips.Count;
            if (n < 2) return;
            var main = BladeArts.Tint(a.Art, a.Art.Main, 0f);
            var light = BladeArts.Tint(a.Art, a.Art.Light, 0.3f);
            // Afterimages: the spear where it was, fainter the older.
            for (int k = n - 2; k >= Mathf.Max(0, n - 5); k--)
            {
                float age = (n - 1 - k) / 4f;
                var px = TerraParticles.ToPx(a.Tips[k]);
                var v = Flip(a.Ways[k]);
                float rot = Mathf.Atan2(v.y, v.x) + 2.355f;
                if (right) rot -= Mathf.PI / 2f;
                var c = Color.Lerp(light, main, age) * (0.32f * (1f - age));
                c.a = 0f;
                fx.Draw(spear, px, c, rot, p.Scale, flipX: right, overPlayer: true);
            }
            var tip = TerraParticles.ToPx(p.Pos);
            var prevTip = TerraParticles.ToPx(a.Tips[n - 2]);
            var way = Flip(a.Ways[n - 1]);
            float along = Vector2.Dot(tip - prevTip, way), across = Mathf.Abs(Vector2.Dot(tip - prevTip, new Vector2(-way.y, way.x)));
            // A thrust going out: a streak of light ahead of the head, longer the faster.
            if (along > 3f && !a.Harmless)
                StreakPx(fx, tip, tip + way * Mathf.Min(70f, along * 4f), light, 10f + a.Art.Level * 1.5f, Mathf.Clamp01(along / 12f) * 0.8f);
            // A sweep or a spin: a ribbon of light through the head's last places.
            if (across > 2.5f && !a.Harmless)
                for (int k = Mathf.Max(1, n - 6); k < n; k++)
                {
                    float f = (k - (n - 6f)) / 6f;
                    StreakPx(fx, TerraParticles.ToPx(a.Tips[k - 1]), TerraParticles.ToPx(a.Tips[k]), Color.Lerp(main, light, f), 6f + 6f * f, 0.55f * f);
                }
        }

        static Sprite streakSprite;

        /// <summary>A soft line of light from a to b (px), about thick px across (Terraria's Extra_98), added.</summary>
        static void StreakPx(FxBatch fx, Vector2 a, Vector2 b, Color c, float thick, float alpha)
        {
            if (alpha <= 0.01f) return;
            if (streakSprite == null) streakSprite = FxBatch.Whole("Extra_98");
            var d = b - a;
            float len = d.magnitude;
            if (len < 1f || streakSprite == null) return;
            float rot = Mathf.Atan2(d.y, d.x) - Mathf.PI / 2f;
            fx.Draw(streakSprite, (a + b) * 0.5f, new Color(c.r * alpha, c.g * alpha, c.b * alpha, 0f), rot,
                new Vector2(thick / streakSprite.rect.width * 2f, len / streakSprite.rect.height), overPlayer: true);
        }

        /// <summary>The technique's end: the next use goes on to the next technique if it comes within half a second.</summary>
        static void EndSpearArt(SpearArt a)
        {
            if (!a.Skill) spearLastEnd = Time.time;
        }

        // ------------------------------------------------------------------ the right-click arts' part (BladeSkillsSpears)

        static string forcedMove;
        static float forcedDamage = 1f;
        static int forcedBasis = 30;

        /// <summary>
        /// A right-click art's spear: the spear's own thrust (its picture, its shots) through the move given, that long, its
        /// blows sharing that many times the spear's damage; toward the cursor. The spear out already is put away first.
        /// </summary>
        public bool SkillSpear(ItemDef def, HeroController hero, Vector2 aim, string move, float damage, float seconds)
        {
            if (def == null || hero == null || def.Shoot == ProjKind.None || !spearMoves.ContainsKey(move)) return false;
            foreach (var q in live) if (!q.Dead && q.Kind == def.Shoot) Kill(q);
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            var dir = aim - centre;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right * (hero.cState.facingRight ? 1f : -1f);
            dir.Normalize();
            float scale = Equipment.UseScale(def);
            SpawnClass = DamageClass.Melee;
            var p = Spawn(def.Shoot, centre, dir * (def.ShootSpeed / scale), Equipment.Damage(def), Equipment.Knockback(def), Equipment.Crit(def), 0, Mathf.Max(6, Mathf.RoundToInt(seconds * 60f)));
            if (p == null) return false;
            forcedMove = move;
            forcedDamage = damage;
            forcedBasis = Mathf.Max(1, Mathf.RoundToInt(def.UseAnimation * scale * 60f));
            try
            {
                p.SpearArtTried = true;
                p.SpearArt = BeginSpearArt(p);
            }
            finally { forcedMove = null; }
            return true;
        }

        /// <summary>A right-click art's spear out now: its way from level for the player's arm (degrees, + up, his facing's).</summary>
        public bool SkillSpearAngle(float facing, out float deg)
        {
            deg = 0f;
            foreach (var p in live)
            {
                if (p.Dead || p.SpearArt == null || !p.SpearArt.Skill) continue;
                var v = p.Vel;
                deg = Mathf.Atan2(v.y, Mathf.Abs(v.x) < 1e-3f ? 1e-3f : v.x * facing) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        /// <summary>A melee shot of an art at a place (world), going that way (Terraria px an update, y up).</summary>
        Proj ArtShot(ProjKind kind, Vector2 at, Vector2 vel, int damage, float knockback, float crit)
        {
            SpawnClass = DamageClass.Melee;
            var c = Spawn(kind, at, vel, damage, knockback, crit);
            if (c != null) c.Melee = true;
            return c;
        }

        /// <summary>The North Pole's ice spears falling on the cursor from above, spread across it.</summary>
        public void IceRain(ItemDef def, Vector2 aim, int count)
        {
            int dmg = Mathf.RoundToInt(Equipment.Damage(def) * 0.8f);
            for (int k = 0; k < count; k++)
            {
                float x = (k - (count - 1) * 0.5f) * 34f + Random.Range(-8f, 8f);
                var from = aim + new Vector2(x, 230f + Random.Range(0f, 60f)) * Px;
                var c = ArtShot(ProjKind.NorthPoleSpear, from, new Vector2(Random.Range(-1f, 1f), -12f - Random.value * 3f), dmg, Equipment.Knockback(def) * 0.85f, Equipment.Crit(def));
                if (c != null) c.Ai0 = 30f;
            }
            Sound("Item_120", 0.6f);
        }

        /// <summary>The Chlorophyte Partisan's spore clouds blown out round the player.</summary>
        public void SporeRing(ItemDef def, Vector2 centre, int count)
        {
            int dmg = Mathf.RoundToInt(Equipment.Damage(def) * 0.6f);
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                ArtShot(ProjKind.SporeCloud, centre + way * 30f * Px, way * 3.2f, dmg, Equipment.Knockback(def) * 0.5f, Equipment.Crit(def));
            }
            Sound("Item_8", 0.6f);
        }

        /// <summary>The Mushroom Spear's mushrooms flung out in a ring round the player.</summary>
        public void MushroomRing(ItemDef def, Vector2 centre, int count)
        {
            int dmg = Mathf.Max(1, Equipment.Damage(def) / 3);
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                ArtShot(ProjKind.SpearMushroom, centre + way * 20f * Px, way * (8f + (k % 2) * 3f), dmg, 0f, Equipment.Crit(def));
            }
            Sound("Item_8", 0.6f);
        }

        /// <summary>The Storm Spear's bolts in a fan toward the cursor.</summary>
        public void ThunderVolley(ItemDef def, HeroController hero, Vector2 aim, int count, float damage)
        {
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            var dir = (aim - centre).normalized;
            if (dir == Vector2.zero) dir = Vector2.right;
            int dmg = Mathf.RoundToInt(Equipment.Damage(def) * damage);
            for (int k = 0; k < count; k++)
            {
                float off = (k - (count - 1) * 0.5f) * 9f;
                ArtShot(ProjKind.ThunderSpearShot, centre, Rotate(dir, off) * 14f, dmg, Equipment.Knockback(def) * 0.5f, Equipment.Crit(def));
            }
            Sound("Item_122", 0.7f);
        }

        /// <summary>Gungnir thrown: straight at the cursor, through five, falling a little after a while, gold behind it.</summary>
        public bool ThrowGungnir(ItemDef def, HeroController hero, Vector2 aim)
        {
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            var dir = (aim - centre).normalized;
            if (dir == Vector2.zero) dir = Vector2.right * (hero.cState.facingRight ? 1f : -1f);
            var c = ArtShot(ProjKind.ThrownGungnir, centre + dir * 20f * Px, dir * 19f, Mathf.RoundToInt(Equipment.Damage(def) * 1.8f), Equipment.Knockback(def), Equipment.Crit(def));
            Sound("Item_1", 0.8f);
            return c != null;
        }

        static void RegisterSpearArts()
        {
            specs[ProjKind.ThrownGungnir] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_105", W = 18, H = 18, Scale = 1.3f, Time = 90, Penetrate = 5, TileCollide = true, LocalCooldown = 15,
                Light = new Color(1f, 0.85f, 0.4f),
                Update = (t, p) => t.StepThrownGungnir(p), Draw = (t, fx, p, px) => DrawThrownGungnir(fx, p, px), OnKill = (t, p) => KillThrownGungnir(p),
            };
        }

        static BladeArts.Art gungnirArt;
        static BladeArts.Art GungnirArt => gungnirArt ?? (gungnirArt = BladeArts.Of(ItemDb.Get(ItemType.Gungnir)));

        void StepThrownGungnir(Proj p)
        {
            if (++p.Ai0 > 22f) p.Vel = new Vector2(p.Vel.x * 0.995f, Mathf.Max(p.Vel.y - 0.35f, -16f));
            p.Trail.Add(p.Pos);
            if (p.Trail.Count > 10) p.Trail.RemoveAt(0);
            var v = Flip(p.Vel);
            if (Random.value < 0.8f) SwordFx.SpearMote(GungnirArt, TerraParticles.ToPx(p.Pos) - v.normalized * Random.value * 20f, v * 0.1f, p.Vel.x < 0f ? -1f : 1f);
        }

        static void DrawThrownGungnir(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = Sprite(p.Spec.Texture).rect;
            var spear = FxBatch.Frame(p.Spec.Texture, 0, 0, (int)whole.width, (int)whole.height, 0f, 0f);
            var v = Flip(p.Vel);
            bool right = p.Vel.x > 0f;
            float rot = Mathf.Atan2(v.y, v.x) + 2.355f;
            if (right) rot -= Mathf.PI / 2f;
            for (int k = 1; k < p.Trail.Count; k++)
            {
                float f = k / (float)p.Trail.Count;
                StreakPx(fx, TerraParticles.ToPx(p.Trail[k - 1]), TerraParticles.ToPx(p.Trail[k]), new Color(1f, 0.85f, 0.35f), 6f + 8f * f, 0.6f * f);
            }
            StreakPx(fx, px, px + v.normalized * 60f, new Color(1f, 0.95f, 0.7f), 10f, 0.55f);
            fx.Draw(spear, px, new Color(1f, 1f, 1f, 1f), rot, p.Scale, flipX: right, overPlayer: true);
        }

        static void KillThrownGungnir(Proj p)
        {
            var at = TerraParticles.ToPx(p.Pos);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                SwordFx.SpearMote(GungnirArt, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f, 1f);
            }
            Sound("Item_10", 0.5f);
        }
    }
}
