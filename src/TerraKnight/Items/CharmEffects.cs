using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The charm bag's own charm effects (护符设计记录.md), each charm in its part (CharmEffects*.cs): a clock every frame
    /// (<see cref="Tick"/>, from CharmSkills), a look drawn round the posed player (<see cref="Draw"/>, from TerraMod.PosePlayer:
    /// each charm its own, quiet one), and the charm skill key (<see cref="Press"/>, <see cref="PressAll"/>). Only with the
    /// charm bag on and the charm worn.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int Grubsong = 3, Stalwart = 4, Baldur = 5, Fury = 6, QuickFocus = 7, LifebloodHeart = 8, LifebloodCore = 9,
            Defender = 10, SteadyBody = 14, DeepFocus = 34;

        static bool On(int id) => !PlayerChallenges.AccessoriesLocked && Charms.Enabled && Charms.Worn(id);
        static TerraMod Mod => TerraMod.Instance;
        static Player.TerraStats Stats => TerraMod.Instance != null ? TerraMod.Instance.Stats : null;

        /// <summary>Playing: in a scene of the world with the player alive.</summary>
        static bool Playing(HeroController hero) =>
            hero != null && !hero.cState.dead && Mod != null && World.TerrainSystem.Current != null && GameHooks.IsGameplay(GameManager.UnsafeInstance);

        /// <summary>The player's middle now (world units).</summary>
        static Vector2 Centre(HeroController hero)
        {
            var box = hero != null ? hero.GetComponent<BoxCollider2D>() : null;
            return box != null ? (Vector2)box.bounds.center : hero != null ? (Vector2)hero.transform.position : Vector2.zero;
        }

        /// <summary>Every frame in play: each worn charm's clock.</summary>
        public static void Tick(HeroController hero)
        {
            TrackLife();
            TickGrubsong(hero);
            TickFury(hero);
            TickQuickFocus(hero);
            TickLifeblood(hero);
            TickStalwart(hero);
            TickBaldur(hero);
            TickDefender(hero);
            TickSteady(hero);
            TickFocus(hero);
            TickNail(hero);
            TickClass(hero);
            TickFlukes(hero);
            TickWomb(hero);
            TickSpores(hero);
            TickUnn(hero);
            TickHive(hero);
            TickCompanions(hero);
            TickDream(hero);
            var fx = FxBatch.Instance;
            if (fx != null) { DrawStalwartWorld(fx); DrawDefenderWorld(fx); DrawNailWorld(fx); DrawClassWorld(fx); DrawSwarmWorld(fx); DrawFormsWorld(fx); DrawDashWorld(fx); DrawFocusWorld(fx); }
        }

        /// <summary>The charm skill key pressed: the chosen charm's skill (CharmSkills.Chosen), when it is ready.</summary>
        public static void Press(HeroController hero, int chosen)
        {
            if (!On(chosen)) return;
            // Cooling (the shell's second press aside): how long is left, by the player.
            if (CharmSkills.Cooling(chosen) && !(chosen == Stalwart && (ShellUp || ShellCharged))) { CharmSkills.SayCooling(hero, chosen); return; }
            switch (chosen)
            {
                // The shell's second press lets its wave go.
                case Stalwart: if (!PressStalwart(hero)) StartShell(hero); break;
                case Baldur: if (!guarding) StartGuard(hero); break;
                case Fury: Frenzy(hero); break;
                case LifebloodHeart: GiveHeart(hero); break;
                case SteadyBody: StartSteady(hero); break;
                case DeepFocus: ToggleFocus(hero); break;
                case SoulEater: SwitchEater(hero); break;
                case SporeShroom: if (!Spored) StartSpores(hero); break;
                case ShapeOfUnn: if (!UnnFlying) StartUnn(hero); break;
            }
        }

        // ------------------------------------------------------------------ the life watched

        /// <summary>When the life last went down (a hit, a burn, a sacrifice): the Lifeblood Core's quiet seconds count from it.</summary>
        static float lifeLostAt;
        static int lastLife = -1;

        static void TrackLife()
        {
            var st = Stats;
            if (st == null) return;
            if (lastLife >= 0 && st.Life < lastLife) lifeLostAt = Time.time;
            lastLife = st.Life;
        }

        // ------------------------------------------------------------------ a hit taken softly

        /// <summary>A hit a charm took (the shell, a perfect guard): its own number, colour and sound, no knockback or blood.</summary>
        static int softHitFrame = -1;
        static string softHitText, softHitSound;
        static Color softHitColor;

        /// <summary>The hit being taken now was a charm's (TerraHurt.Hurt: immunity only).</summary>
        public static bool SoftHitNow => softHitFrame == Time.frameCount;

        /// <summary>PlayerHooks.Hurt: shows a hit a charm took its own way; true when it did.</summary>
        public static bool ShowSoftHit(HeroController hero)
        {
            if (!SoftHitNow || hero == null) return false;
            if (softHitText != null) UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, softHitText, softHitColor);
            if (softHitSound != null) Mod.PlaySound(softHitSound, 0.6f);
            return true;
        }

        /// <summary>After the player is posed: each worn charm's look round it (Terraria pixels, y down).</summary>
        public static void Draw(FxBatch fx, Vector2 centrePx, int facing, float opacity)
        {
            if (fx == null || !Charms.Enabled) return;
            bool top = fx.OnTop;
            try
            {
                DrawFury(fx, centrePx, opacity);
                DrawQuickFocus(fx, centrePx, opacity);
                DrawLifeblood(fx, centrePx, opacity);
                DrawDefender(fx, centrePx, opacity);
                DrawSteady(fx, centrePx, opacity);
                DrawGrubsong(fx, centrePx, opacity);
                DrawStalwart(fx, centrePx, opacity);
                DrawBaldur(fx, centrePx, opacity);
                DrawNail(fx, centrePx, opacity);
                DrawClass(fx, centrePx, opacity);
                DrawSwarm(fx, centrePx, opacity);
                DrawForms(fx, centrePx, opacity);
                DrawDream(fx, centrePx, opacity);
            }
            finally { fx.OnTop = top; }
        }

        /// <summary>The charms' skill and passive lines for their tooltips (null: none here).</summary>
        public static string Describe(int charm, string key)
        {
            switch (charm)
            {
                case Grubsong: return Lang.T("戴着时：三只光蝶绕着你飞；生命不满时，它们洒下一圈光环，回复 10% 生命。冷却 10 秒");
                case DeepFocus: return Lang.F("护符技（{0}）：开启或关闭专心模式。开启后一直生效，直到再按一次关闭：整个场景变成灰色，敌人和它们的攻击、弹幕高亮成红色，你自己和你的弹幕、武器攻击、召唤物高亮成蓝色。没有冷却", key);
                case SteadyBody: return Lang.F("戴着时：移动没有惯性，松开立刻停下，转身立刻转身，转身后的速度就是转身前的速度；松开方向键 0.5 秒内再按，按原来的速度出发。护符技（{0}）：3 秒内全身变成金色，每次冲刺都无敌，直接穿过敌人。冷却 10 秒", key);
                case Defender: return Lang.T("戴着时：尖刺、荆棘和酸水都伤不到你，可以直接站在上面、穿过去、在酸水里游，哪里都能去；深坑和陨石也不伤你（掉进深坑照常被送回）。附近有敌人时，不时在脚下冒出一团毒雾，让雾里的敌人中毒");
                case LifebloodHeart: return Lang.F("护符技（{0}）：获得一层 25% 生命的生命血护盾（不叠加）。护盾在时，敌人的一击最多只能把它打碎，伤害不会穿过它。冷却 60 秒", key);
                case LifebloodCore: return Lang.T("戴着时：5 秒没有掉血，获得一层 10% 生命的护盾，最多 3 层；和生命血之心同戴时护盾叠加");
                case QuickFocus: return Lang.T("戴着时：药水病时间缩短 15%（和炼金石、神话护身符同戴时缩短 30%）；受伤后自然回血早 1 秒开始（3 秒，不戴是 4 秒）");
                case Fury: return Lang.F("戴着时：生命不高于 35% 时，四个职业的伤害都加 25%。护符技（{0}）：舍去 30% 最大生命（最低留 1 点），身边爆出一圈火焰，之后 2 秒内造成的伤害翻倍。冷却 10 秒", key);
                case Baldur: return Lang.F("护符技（{0}，按住）：朝鼠标方向举盾，最多 0.8 秒，正面受到的伤害减少 70%；刚举起的 0.15 秒内（盾发白光）挨的那一下完全挡下，弹幕会被反弹回去打敌人。举盾时移动变慢。冷却 5 秒，精准格挡后只要 3 秒", key);
                case Stalwart: return Lang.F("戴着时：受伤后的无敌时间多 0.2 秒。护符技（{0}）：3 秒内吸收受到的所有伤害，再按一次朝鼠标射出光波，碰到敌人或墙就炸开，伤害等于吸收的总量。冷却 20 秒", key);
                case MarkOfPride: return Lang.T("戴着时：近战武器和骨钉的攻击范围变为 1.5 倍");
                case Flukenest: return Lang.T("戴着时：身边显示一圈范围（和狱火药水一样大）；范围里有敌人时，每 2 秒飞出 3 只吸虫追击敌人，每只命中回复 1% 生命");
                case Thorns: return Lang.T("戴着时：被敌人打中时荆棘向四周爆开，120 像素内的敌人受到骨钉 1.5 倍的伤害");
                case SporeShroom: return Lang.F("护符技（{0}）：遁入孢子毒雾 2 秒，看不见、免疫所有伤害，雾里的敌人中毒并持续受到伤害；期间不能攻击。冷却 30 秒", key);
                case GlowingWomb: return Lang.T("戴着时：战斗中每 4 秒消耗 20 魔力孵出一只幼崽（最多 4 只），它们扑向附近的敌人自爆，伤害是手上武器的 1.5 倍");
                case ShapeOfUnn: return Lang.F("戴着时：骑在坐骑上，小怪和精英不会主动攻击你，除非你打了它们（首领照常）。护符技（{0}）：变成一团乌恩的光芒 5 秒，用 W A S D 上下左右飞行，照常会受伤，也能攻击。冷却 15 秒", key);
                case Hiveblood: return Lang.T("戴着时：受伤后下一次喝的回血药效果翻倍（之后 60 秒内不会再翻倍）；3 秒没受伤就获得蜂蜜的回血效果，直到受伤");
                case Longnail: return Lang.T("戴着时：近战武器和骨钉的攻击范围变为 1.15 倍，并且能隔着墙打到敌人（不戴时打不到墙后面的敌人）");
                case HeavyBlow: return Lang.T("戴着时：骨钉技艺蓄满后还能继续蓄力 1.5 秒，分三段，最多伤害 +60%、范围 +35%、击退翻倍");
                case UnbreakableStrength: return Lang.T("戴着时：近战伤害提高 10%，骨钉伤害提高 30%（碎了就没有效果）");
                case NailmastersGlory: return Lang.T("戴着时：骨钉技艺蓄力快 30%，骨钉挥砍的间隔缩短 30%");
                case QuickSlash: return Lang.T("戴着时：骨钉挥砍没有后摇；骨钉和骨钉技艺每次命中，紧接着再劈一刀银色的，造成这一击 30% 的伤害，必定暴击");
                case GrubberflysElegy: return Lang.T("戴着时：骨钉每次挥砍都朝挥砍方向射出一道银色剑气，造成一半伤害，能穿过敌人，碰墙消失；强力斩和冲刺斩射出一道大剑气，旋风斩转着向两边射出剑气");
                case SharpShadow: return Lang.T("戴着时：暗影冲刺冲得更快更远，穿过的敌人受到骨钉 1.5 倍的伤害");
                case ShamanStone: return Lang.T("戴着时：魔法伤害提高 15%，所有魔法弹幕变大 30%");
                case SoulCatcher: return Lang.T("戴着时：吉欧掉落增加 20%；近战和远程命中时吸取伤害的 3% 回复生命（每秒最多 4 点）");
                case SoulEater: return Lang.F("戴着时：吉欧掉落增加 30%。护符技（{0}）：在两种模式之间切换。鞭子模式：召唤物伤害降低 40%，鞭子伤害翻倍；召唤物模式：召唤物伤害提高 40%，但不能用任何武器攻击。冷却 3 秒", key);
                case FragileHeart: return Lang.T("戴着时：面具带来的青色心变为 3 倍（碎了就没有效果）");
                case FragileGreed: return Lang.T("戴着时：敌人掉落的吉欧增加 20%，多出来的会闪光（碎了就没有效果）");
                case JonisBlessing: return Lang.T("戴着时：移动速度减半，所有职业的伤害提高 35%");
                case SpellTwister: return Lang.T("戴着时：魔力消耗降低 10%，魔法武器的施法速度和间隔快 30%，弹药有 33% 的几率不消耗；远程武器命中时飞出一颗小法球追击敌人");
                case Kingsoul: return Lang.T("戴着时：魔法武器的魔力消耗减半；能打开通往出生地的门");
                case Dashmaster: return Lang.T("戴着时：冲刺键的冲刺（克苏鲁之盾、黑冲白冲都算）冲向鼠标的方向；连按两下 W A S D 分别向上、左、下、右冲刺（盔甲有双击下的能力时，双击下照旧给盔甲）");
                case Dreamshield: return Lang.T("戴着时：原版的梦之盾绕着你转，而且是两面，在两侧各一面；挡下弹幕，碰到敌人造成骨钉 80% 的伤害，之后碎开一会儿再长回来");
                case Weaversong: return Lang.T("戴着时：召唤原版的编织者幼体攻击敌人；它们算作召唤物，吃召唤伤害加成和鞭子的标记");
                case Grimmchild:
                    if (PlayerData.instance != null && PlayerData.instance.grimmChildLevel >= 5)
                        return Lang.T("戴着时：保留无忧旋律的守护之歌，有时挡下敌人的一次伤害；不会召唤格林之子");
                    return Lang.T("戴着时：格林之子（2 级起）攻击更强（随等级）、更频繁（每 0.9 秒一发）、更准（不散射、火球更快并追踪目标）；算作召唤物");
                case DreamWielder: return Lang.T("戴着时：梦之钉在空中也能蓄力，蓄力和挥出时都不会定住你，可以照常移动；蓄力更快，更容易掉落精华（原版）");
                case Sprintmaster: return Lang.T("戴着时：穿着跑鞋在地面上奔跑直接就是最高速度，不用加速（没穿跑鞋没有效果）");
                default: return null;
            }
        }

        // ------------------------------------------------------------------ shared pictures

        static Sprite dot, ring;

        /// <summary>A soft round glow, 32 px across (Terraria pixels at the batch's scale 1).</summary>
        static Sprite Dot => dot != null ? dot : dot = Make(32, (x, y) =>
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f);
            return (1f - d) * (1f - d);
        });

        /// <summary>A thin soft ring, 64 px across, its brightest line at the edge.</summary>
        static Sprite Ring => ring != null ? ring : ring = Make(64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
            float k = 1f - Mathf.Abs(d - 0.9f) / 0.1f;
            return Mathf.Clamp01(k) * Mathf.Clamp01(k);
        });

        static Sprite Make(int size, System.Func<int, int, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TerraKnight/Charm art", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f / TerraParticles.Px, 0, SpriteMeshType.FullRect);
        }

        /// <summary>An added glow of a colour at a strength (FxBatch: alpha 0 means added).</summary>
        static Color Glow(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 0f);
    }
}
