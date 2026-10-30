using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade weapons' ways for the wiki only (the user's 10-29 "wiki里面写了所有武器的新版效果没？比如连发穿透什么的", then "只写进
    /// wiki": the game's tooltips keep their right-click line alone): what the left button does — a sword's combo, a spear's
    /// techniques, a yoyo's tricks, a flail's styles, a boomerang's throws, a shortsword's round, a lance, the Zenith, a whip,
    /// a gun's or bow's way of firing (RangedFire), a magic weapon's cast (MageLefts) — and its right-click art (BladeTips),
    /// a line each. Read from the tables the weapons run on, so the wiki says what they do. Asked with the remade weapons on
    /// (the wiki export sets them).
    /// </summary>
    internal static class WeaponWikiLines
    {
        const string Then = " → ";

        public static List<string> Lines(ItemDef d)
        {
            var lines = new List<string>();
            if (d == null || !d.IsWeapon) return lines;
            string left = Left(d);
            if (left != null) lines.Add(left.StartsWith("左键：") ? left : "左键：" + left);
            string right = BladeTips.RightClick(d);
            if (right != null) lines.Add(right);
            return lines;
        }

        static string Left(ItemDef d)
        {
            // Magic: the weapon's own cast, said as the game says it.
            var mage = MageLefts.Of(d);
            if (mage != null && !string.IsNullOrEmpty(mage.Tip)) return mage.Tip;
            if (d.Type == ItemType.Zenith)
                return "每次挥出三把剑，一次换一种飞行轨迹：原版环绕、从三面交叉穿过鼠标处的星芒、收拢的螺旋、天降剑雨（落地处一圈光）、身边旋轮后掷出";
            if (d.Type == ItemType.JoustingLance || d.Type == ItemType.ShadowJoustingLance || d.Type == ItemType.HallowJoustingLance)
                return "站着时短促抖枪，奔跑时枪尖破风，跑得越快伤害越高；枪长 ×1.28";
            if (Whips.WikiStyled(d)) return "八字抽鞭：正手、反手交替成一个循环，鞭梢抽到最远处炸响";
            var spear = TerraProjectiles.WikiSpearMoves(d.Type);
            if (spear != null)
                return $"枪法 {string.Join(Then, spear)} 轮换，接着按出下一式，停顿后从头开始；距离 ×{TerraProjectiles.SpearArtReach:0.##}，伤害 ×{TerraProjectiles.SpearArtBoost:0.##}";
            var stabs = SwordFx.WikiStabs(d.Type);
            if (stabs != null) return $"刺法 {string.Join(Then, stabs)} 轮换，接着按出下一式；距离 ×1.3";
            var tricks = TerraProjectiles.WikiYoyoTricks(d.Shoot);
            if (tricks != null) return $"花式 {string.Join(Then, tricks)} 轮流使出，在敌人身边会收紧；线长 +30%";
            var flail = TerraProjectiles.WikiFlailStyles(d.Shoot);
            if (flail != null) return $"甩法 {string.Join(Then, flail)} 轮换（按住旋转，松开抛出）；旋转范围和链长 ×1.3";
            var throws = TerraProjectiles.WikiThrows(d.Shoot);
            if (throws != null) return $"投掷轨迹 {string.Join(Then, throws)} 轮换";
            if (BladeArts.Is(d)) return Combo(d);
            if (d.Class == DamageClass.Ranged && !RangedArts.OwnWay(d.Type)) return Ranged(RangedArts.Of(d));
            return null;
        }

        /// <summary>A sword's (club's, axe's...) combo by its cuts' names, how much harder and further than the game's swing.</summary>
        static string Combo(ItemDef d)
        {
            var art = BladeArts.Of(d);
            if (art?.Combo == null || art.Combo.Length == 0) return null;
            var names = new List<string>();
            foreach (var m in art.Combo) names.Add(MoveName(m.Name));
            string combo = names.Count == 1 ? names[0] : string.Join(Then, names) + " 连招，按住或接着按出下一招";
            return $"{combo}；伤害 ×{art.Boost:0.##}，范围 ×{art.Reach:0.##}；空中朝下方攻击为空中下刺（命中后弹起）";
        }

        static string MoveName(string name)
        {
            switch (name)
            {
                case "Chop": return "竖劈";
                case "Rise": return "上撩";
                case "Diagonal": return "斜斩";
                case "Sweep": return "横劈";
                case "Backsweep": return "反手横劈";
                case "HeavySweep": return "重横扫";
                case "Reap": return "收割";
                case "Thrust": return "突刺";
                case "StabFlurry": return "连刺";
                case "Cleave": return "重劈";
                case "Smash": return "砸击";
                case "Iai": return "拔刀斩";
                case "Flurry": return "乱斩";
                case "Rake": return "三连爪";
                case "Snip": return "剪切";
                case "Twin": return "双斩";
                case "Cross": return "十字斩";
                case "Overhead": return "大上段劈";
                case "LowSweep": return "下段横扫";
                case "Crescent": return "月牙斩";
                case "Flourish": return "挽剑花";
                case "XCut": return "X 斩";
                case "Lunge": return "突进斩";
                case "IaiDash": return "一闪";
                case "Leap": return "跃斩";
                case "Uppercut": return "升龙斩";
                case "Drag": return "拖刀上撩";
                case "Plunge": return "空中下刺";
                default: return name;
            }
        }

        /// <summary>A gun's, bow's or throw's way of firing (RangedFire's), its numbers as they are dealt; null when it fires as the game's.</summary>
        static string Ranged(RangedArts.Look look)
        {
            int n = look.N;
            switch (look.Left)
            {
                case RangedArts.Left.Power:
                    return $"每 {Mathf.Max(2, n)} 发一发强化射击（伤害 ×{0.8f + Mathf.Max(2, n) / 4f:0.##}，多穿透 2 个敌人，弹速更快），其余每发 ×0.8";
                case RangedArts.Left.Twin: return "一次并排射出两发，只耗一份弹药（每发 ×0.55）";
                case RangedArts.Left.Burst: return $"{Mathf.Max(2, n)} 连发，每发各耗一份弹药";
                case RangedArts.Left.SpinUp: return "按住越射越快";
                case RangedArts.Left.Ricochet: return $"命中后分出一发弹向下一个敌人（×0.6），最多 {Mathf.Max(1, n)} 次";
                case RangedArts.Left.Split: return $"命中后分出 {Mathf.Max(1, n)} 片碎片穿过敌人（每片 ×0.3）";
                case RangedArts.Left.Blast: return "命中处爆开，波及周围敌人（45% 伤害）";
                case RangedArts.Left.Home: return "射出的弹幕追踪敌人";
                case RangedArts.Left.Pierce: return "多穿透 2 个敌人";
                case RangedArts.Left.PointBlank: return "弹丸 7 格内伤害 ×1.15，越远越低，20 格时 ×0.8";
                default: return null;
            }
        }
    }
}
