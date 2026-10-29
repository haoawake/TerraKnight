using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// A remade weapon's line in its tooltip: its right-click art, said the same way for all of them (the user's 10-26 "要把
    /// 武器描述也都统一改好", then "把描述里面的招式说明和特效说明去掉，只留右键说明，没有的就直接不加"); a weapon without one gets
    /// no line. Shown only with the remade swing on ("近战挥砍：新版").
    /// </summary>
    internal static class BladeTips
    {
        /// <summary>The weapon's right-click art, a line; null when it has none.</summary>
        public static string RightClick(ItemDef d)
        {
            // The Brand of the Inferno's own right button (Terraria's: MeleeSpecials' raised shield and parry).
            if (d != null && d.Type == ItemType.DD2SquireDemonSword && BladeArts.Active(d)) return ShieldLine;
            switch (BladeSkills.Of(d))
            {
                case BladeSkills.Skill.Draw: return "右键：按住蓄力，刀垂在身后拖地；松开朝面向冲出，斩中沿途所有敌人（2～5 倍伤害）";
                case BladeSkills.Skill.Throw: return "右键：把光剑抛向鼠标处悬着旋转，此时左键在那里挥砍；再按右键收回";
                case BladeSkills.Skill.Boomerang:
                    switch (d.Type)
                    {
                        case ItemType.LucyTheAxe: return "右键：把露西扔出去，她旋转着飞回来（还会说话）";
                        case ItemType.IceSickle: return "右键：掷出镰刀，旋转着飞回，沿途冻伤敌人";
                        case ItemType.DeathSickle: return "右键：掷出镰刀，旋转着飞回，沿途吸取生命";
                        default: return "右键：掷出镰刀，旋转着飞回";
                    }
                case BladeSkills.Skill.Slam:
                    switch (d.Type)
                    {
                        case ItemType.FieryGreatsword: return "右键：按住蓄力，松开跳起劈下，前方地面喷出一排火柱";
                        case ItemType.ChlorophyteClaymore: return "右键：按住蓄力，松开跳起劈下，落地炸开一片孢子云";
                        default: return "右键：按住蓄力，松开跳起劈下，冲击波沿地面推开（满血的敌人伤害加倍）";
                    }
                case BladeSkills.Skill.Wave: return "右键：按住蓄力，松开向前放出贴地的龙焰";
                case BladeSkills.Skill.SoulWave: return "右键：按住蓄力，松开挥出一道白波飞向鼠标处，碰到敌人爆炸；蓄满时一次三道";
                case BladeSkills.Skill.Execute: return "右键：按住蓄力，松开朝面向冲斩，敌人生命越少伤害越高";
                case BladeSkills.Skill.HomeRun: return "右键：按住蓄力，松开一棒把敌人打飞，命中回血";
                case BladeSkills.Skill.Slap: return "右键：按住蓄力，松开一记巨掌，击退极大";
                case BladeSkills.Skill.Clamp: return "右键：烤模张开再合拢，夹住前方的敌人";
                case BladeSkills.Skill.Dive: return "右键：朝鼠标方向俯冲斩";
                case BladeSkills.Skill.Portal: return "右键：穿过传送门到鼠标处，出来就是一斩";
                case BladeSkills.Skill.Assassin: return "右键：闪到离鼠标最近的敌人身后，刺出必定暴击的一刀";
                case BladeSkills.Skill.ShadowStep: return "右键：暗影步，朝面向穿过敌人，路过的被斩中";
                case BladeSkills.Skill.TerraDash: return "右键：按住蓄力，松开朝面向冲刺，身后留下一道持续伤敌的泰拉能量";
                case BladeSkills.Skill.Hook: return "右键：射出钉刺扎住敌人或墙，把自己拉过去";
                case BladeSkills.Skill.Pull: return "右键：用拐杖钩住敌人，拉到面前";
                case BladeSkills.Skill.Starfall: return d.Type == ItemType.StarWrath ? "右键：按住蓄力，松开在鼠标处降下一阵星雨" : "右键：按住蓄力，松开在鼠标处降下一串星星";
                case BladeSkills.Skill.Bees: return "右键：放出一群杀人蜂";
                case BladeSkills.Skill.Pumpkins: return "右键：召出南瓜头扑向鼠标附近的敌人";
                case BladeSkills.Skill.Flies: return "右键：在鼠标处放出一团苍蝇，里面的敌人陷入混乱";
                case BladeSkills.Skill.Ichor: return "右键：向前喷出灵液，降低敌人防御";
                case BladeSkills.Skill.Frost: return "右键：插剑入地，前方冒出一排冰刺，冻伤敌人";
                case BladeSkills.Skill.Thorns: return "右键：前方地面长出荆棘，敌人中毒";
                case BladeSkills.Skill.Parry: return d.Type == ItemType.Cutlass ? "右键：举刀招架，时机对上不受伤并反击" : "右键：举剑招架，时机对上不受伤，并以圣光反击";
                case BladeSkills.Skill.Eat: return "右键：啃一口火腿，回复生命并获得饱食（30 秒冷却）";
                case BladeSkills.Skill.Tax: return "右键：征税，敲中的敌人掉钱（同一个敌人 10 秒一次）";
                case BladeSkills.Skill.BloodRite: return "右键：血祭，花一点生命换 8 秒内伤害提高、攻击让敌人流血";
                case BladeSkills.Skill.Rush: return d.Type == ItemType.FetidBaghnakhs ? "右键：按住连续抓击，命中吸血" : "右键：按住连续出拳";
                case BladeSkills.Skill.SpearPierce: return "右键：按住蓄力，松开一记贯穿突刺，刺得更远（2～4 倍伤害）";
                case BladeSkills.Skill.TriThrust: return "右键：按住蓄力，松开朝上、中、下连刺三枪";
                case BladeSkills.Skill.SpearDash: return d.Type == ItemType.DarkLance ? "右键：按住蓄力，松开化作暗影朝面向突刺穿过敌人" : "右键：按住蓄力，松开朝面向突刺穿过敌人";
                case BladeSkills.Skill.ThunderVolley: return "右键：按住蓄力，松开朝鼠标方向掷出一排雷枪（蓄满五道）";
                case BladeSkills.Skill.Whirl: return "右键：把长柄抡圆，在身边旋转扫击两圈半";
                case BladeSkills.Skill.SpearThrow: return "右键：把永恒之枪朝鼠标方向投出，贯穿沿途的敌人";
                case BladeSkills.Skill.IceRain: return "右键：在鼠标处降下一排冰枪";
                case BladeSkills.Skill.SporeRing: return "右键：舞动枪花，向四周吹出孢子云";
                case BladeSkills.Skill.MushroomRing: return "右键：舞动枪花，向四周撒出一圈发光蘑菇";
                case BladeSkills.Skill.GaleDash: return "右键：按住蓄力，松开朝面向疾风突斩，沿途放出切割疾风飞向鼠标处";
                case BladeSkills.Skill.StarBurst: return "右键：流星连刺，一阵加倍的星光突刺";
                case BladeSkills.Skill.Kind: return BladeSkills.KindArtOf(d)?.Tip;
                default: return null;
            }
        }

        const string ShieldLine = "右键：按住举起护盾防护（需要装备盾牌），刚举起时挡下攻击可以招架反击";

        /// <summary>A line of the item's own tooltip that a line here says better (the Brand of the Inferno's shield).</summary>
        public static bool Supersedes(ItemDef d, string line) =>
            d != null && d.Type == ItemType.DD2SquireDemonSword && BladeArts.Active(d) && (line == "按住右键来用护盾防护" || line == Lang.T("按住右键来用护盾防护"));

        /// <summary>The weapon's lines (in the game's language): its right-click art's, or none; none without the remade swing.</summary>
        public static List<string> Lines(ItemDef d)
        {
            var lines = new List<string>();
            var kind = BladeSkills.KindArtOf(d);
            if (d == null || !BladeArts.Active(d) && !BladeSkills.HasSpearArt(d) && kind == null && MageLefts.Tip(d) == null) return lines;
            // A remade magic weapon's left (MageLefts), said first.
            string left = MageLefts.Tip(d);
            if (left != null) lines.Add(left);
            string right = RightClick(d);
            // A kind's art carries its own English.
            if (right != null) lines.Add(kind != null && kind.TipEn != null && !Lang.IsSource && Lang.Culture != "zh-Hant" ? kind.TipEn : Lang.T(right));
            return lines;
        }

        /// <summary>Every Chinese line this can show (for the language table's check).</summary>
        public static IEnumerable<string> AllChinese()
        {
            yield return ShieldLine;
            foreach (var d in ItemDb.All)
            {
                if (d == null || !BladeArts.Is(d) && !BladeSkills.HasSpearArt(d) || BladeSkills.KindArtOf(d) != null) continue;
                string r = RightClick(d);
                if (r != null) yield return r;
            }
        }
    }
}
