using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Each boss's own measure on top of its Terraria stand-in (<see cref="BossStats"/>): life, defense
    /// and damage by how it fights. A big, slow or armoured boss has more life and defense and hits no
    /// harder; a quick, agile one less life and defense and harder hits; one that fights through
    /// summons or hazards hits softer itself. Whether its attacks can be broken off: the bosses the game
    /// itself staggers (its Stun Control: Hornet, the Soul Master, the Traitor Lord, the Hive Knight, the
    /// Broken Vessel, Grimm, the Hollow Knight...) are staggered again, once for every 2.2% of their life
    /// taken (about as often as the nail's slashes stagger them in the game), not by every hit of a fast
    /// weapon; the rest keep their super armour. All of it on every difficulty and stage alike: those
    /// set the stand-in's life and damage, this only shapes it.
    ///
    /// A boss in several pools of health (<see cref="Pool"/>): the stand-in's life is shared out over
    /// all of them, refills included, instead of each pool having the whole: the False Knight's armour
    /// (three rounds) and his head (three times out), the Soul Master's two phases.
    /// </summary>
    internal static class BossBalance
    {
        internal sealed class Tune
        {
            public float Life = 1f, Defense = 1f, Damage = 1f;
            /// <summary>Its attacks can be broken off (the game's own stagger, paced by damage).</summary>
            public bool Stun;
            public string Note;
        }

        static Tune T(float life, float defense, float damage, bool stun, string note) =>
            new Tune { Life = life, Defense = defense, Damage = damage, Stun = stun, Note = note };

        static readonly Tune Plain = new Tune();

        /// <summary>By the bestiary's record ("record@stage" for a boss met again elsewhere).</summary>
        static readonly Dictionary<string, Tune> table = new Dictionary<string, Tune>
        {
            // Stage 0
            ["FalseKnight"] = T(1.15f, 1.5f, 1.0f, false, "身躯庞大、盔甲厚重，动作慢"),
            ["BigFly"] = T(1.1f, 1.0f, 0.9f, false, "体型大、飞得慢，撞击为主"),
            ["Mawlek"] = T(1.2f, 1.2f, 1.0f, false, "体型大，酸液和跳压"),
            // Stage 1
            ["Hornet"] = T(0.8f, 0.6f, 1.2f, true, "灵活快速，原版能被打出硬直"),
            ["MegaMossCharger"] = T(1.1f, 1.3f, 0.9f, false, "厚重冲撞，招式单一"),
            ["GhostAladar"] = T(0.85f, 0.8f, 1.0f, false, "游荡的战士之梦"),
            ["GhostNoEyes"] = T(0.85f, 0.8f, 0.9f, false, "靠召唤的魂魄攻击"),
            // Stage 2
            ["DungDefender"] = T(1.1f, 1.2f, 1.0f, false, "体型壮，滚动和钻地"),
            ["TraitorLord"] = T(0.95f, 0.9f, 1.15f, true, "攻势凶猛，原版能被打出硬直"),
            ["MantisLord"] = T(0.85f, 0.7f, 1.2f, false, "身法灵活的三位领主"),
            ["MageLord"] = T(0.85f, 0.6f, 1.2f, true, "瞬移施法、身体脆弱，原版能被打出硬直"),
            ["MegaJellyfish"] = T(0.8f, 0.8f, 1.0f, false, "只在短暂的空隙能受伤"),
            ["FlukeMother"] = T(1.0f, 1.0f, 0.7f, false, "不动，靠孵出的吸虫攻击"),
            ["MegaBeamMiner"] = T(1.0f, 1.3f, 1.1f, false, "缓慢厚重，激光伤害高"),
            ["GhostHu"] = T(1.0f, 1.0f, 1.1f, false, "大范围的光环"),
            ["GhostMarmu"] = T(0.8f, 0.6f, 1.1f, false, "快速翻滚的小个子"),
            // Stage 3
            ["BlackKnight"] = T(1.1f, 1.3f, 1.0f, false, "身披重甲的守望者骑士"),
            ["HiveKnight"] = T(1.0f, 1.0f, 1.15f, true, "高大但迅猛，原版能被打出硬直"),
            ["JarCollector"] = T(0.85f, 0.8f, 1.1f, false, "上蹿下跳，靠罐子召唤"),
            ["MimicSpider"] = T(1.1f, 1.1f, 1.0f, false, "体型大的伪装者"),
            ["GhostGalien"] = T(1.0f, 1.0f, 1.0f, false, "靠飞舞的镰刀攻击"),
            ["GhostXero"] = T(0.9f, 0.8f, 1.1f, false, "飞剑齐射"),
            ["FalseKnight@5"] = T(1.15f, 1.5f, 1.1f, false, "失败冠军：更狂暴的假骑士"),
            ["MageLord@5"] = T(0.85f, 0.6f, 1.25f, true, "灵魂暴君：更快的灵魂大师"),
            // Stage 4
            ["Oblobble"] = T(0.9f, 0.8f, 1.0f, false, "空中的两只"),
            ["LobsterLancer"] = T(1.0f, 1.0f, 1.1f, false, "驯兽师和它的野兽"),
            ["Zote"] = T(0.6f, 0.5f, 0.7f, false, "没什么威胁"),
            ["Hornet@4"] = T(0.8f, 0.6f, 1.25f, true, "王国边缘的大黄蜂，原版能被打出硬直"),
            ["GhostMarkoth"] = T(0.9f, 0.9f, 1.1f, false, "飞剑和盾"),
            ["InfectedKnight"] = T(0.95f, 0.9f, 1.15f, true, "残破容器：迅猛，原版能被打出硬直"),
            ["InfectedKnight@5"] = T(0.95f, 0.9f, 1.2f, true, "失落近亲：更快的残破容器"),
            // Stage 5
            ["Grimm"] = T(0.85f, 0.7f, 1.25f, true, "舞者般灵活，原版能被打出硬直"),
            ["WhiteDefender"] = T(1.1f, 1.2f, 1.0f, false, "壮硕的白色守卫者"),
            ["GreyPrince"] = T(1.1f, 1.2f, 1.1f, false, "高大的灰色王子"),
            ["NightmareGrimm"] = T(0.85f, 0.7f, 1.3f, true, "梦魇之王：极快，原版能被打出硬直"),
            ["HollowKnight"] = T(1.0f, 1.0f, 1.15f, true, "空洞骑士，原版能被打出硬直"),
            // Godhome
            ["Nailsage"] = T(0.8f, 0.7f, 1.25f, true, "骨钉贤者斯莱：极快"),
            ["NailBros"] = T(0.9f, 0.8f, 1.15f, false, "两位骨钉大师"),
            ["Paintmaster"] = T(1.0f, 0.9f, 1.1f, false, "绘画大师席奥"),
            ["HollowKnightPrime"] = T(0.9f, 0.9f, 1.25f, true, "纯粹容器：极快"),
            ["FinalBoss"] = T(1.0f, 1.0f, 1.2f, false, "辐光：远程的光，不会被打断"),
        };

        /// <summary>A boss's measure where it is met: its own at that stage ("record@stage"), else its usual one.</summary>
        public static Tune Of(string record, int stage)
        {
            if (record == null) return Plain;
            if (table.TryGetValue(record + "@" + stage, out var t)) return t;
            return table.TryGetValue(record, out t) ? t : Plain;
        }

        /// <summary>
        /// The share of the boss's life one of its health pools carries, and how many times the game fills
        /// it in a fight: the False Knight's (and the Failed Champion's) armour three rounds, his head out
        /// three times; the Soul Master's (and the Soul Tyrant's) first phase and second; one pool, once, else.
        /// </summary>
        public static float Pool(string record, HealthManager hm, out int fills)
        {
            fills = 1;
            if (hm == null || record == null) return 1f;
            string n = hm.gameObject.name;
            if (record == "FalseKnight")
            {
                fills = 3;
                return n.Contains("Head") ? 0.35f : 0.65f;
            }
            if (record == "MageLord") return n.Contains("Phase2") ? 0.4f : 0.6f;
            return 1f;
        }

        /// <summary>A boss whose attacks can be broken off takes a stagger for every this much of its life.</summary>
        public const float StunEvery = 0.022f;

        /// <summary>
        /// Whether this hit counts towards the boss's own stagger (its Stun Control): for a boss that can
        /// be broken off, once each <see cref="StunEvery"/> of its whole life dealt; never for one in super
        /// armour or an elite.
        /// </summary>
        public static bool StunCounts(EnemyStats.Info info, int terraDamage)
        {
            if (info == null || !info.Boss || info.Ref == null || !Of(info.Record, EnemyDrops.VariantLevel(info.Scene)).Stun) return false;
            float every = Mathf.Max(1f, EnemyStats.LifeWhole(info) * StunEvery);
            info.StunCarry += terraDamage;
            if (info.StunCarry < every) return false;
            info.StunCarry -= every;
            DevStuns++;
            return true;
        }

        public static int DevStuns;

        /// <summary>Dev: every entry, for the balance report.</summary>
        public static IEnumerable<KeyValuePair<string, Tune>> DevAll => table;
    }
}
