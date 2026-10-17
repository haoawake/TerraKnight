using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// The player's buffs (Player.buffType / buffTime, 44 slots, spec-summoner B.1): AddBuff only
    /// lengthens a running buff and otherwise takes the first free slot from the first non-debuff
    /// on (evicting the oldest buff when full), DelBuff compacts the slots. Each 60 Hz tick the
    /// times count down; a minion buff instead holds itself at 18000 while its minions live and
    /// deletes itself when none are left (UpdateBuffs), and its flag keeps those minions alive.
    /// Potion Sickness, Honey and Panic keep their own timers in the mod and are mirrored here so
    /// the buff bar shows them.
    /// </summary>
    internal static class PlayerBuffs
    {
        public const int MaxBuffs = 44;
        public const int PotionSickness = 21, Honey = 48, Panic = 63, BabySlime = 64, HornetMinion = 125, ImpMinion = 126, SpiderMinion = 133,
            TwinEyesMinion = 134, PirateMinion = 135, StardustDragonMinion = 188, BatOfLight = 213, VampireFrog = 214, BabyBird = 216,
            Ravens = 83, Pygmies = 49, SharknadoMinion = 139, UFOMinion = 140, DeadlySphere = 161, StardustMinion = 182, StormTiger = 263, Smolstar = 271, SwordWhip = 308, ScytheWhip = 311, EmpressBlade = 322, CoolWhip = 312, ThornWhip = 314, FlinxMinion = 325, AbigailMinion = 335, CobWhip = 365,
            Werewolf = 28, Merfolk = 34, IceBarrier = 62, BrainOfConfusion = 321, ManaHeat = 396,
            MushroomBoiMinion = 355, CattivaMinion = 385, FoxsparksMinion = 386, TrustyCattivaMinion = 389, TrustyFoxsparksMinion = 390, ClayPotMinion = 393, ForbiddenMinion = 394,
            // The buff furniture's (SceneMetrics, here carried: Items.CarriedFurniture) and the Water Candle's.
            WaterCandle = 86, CozyFire = 87, HeartLamp = 89, Happy = 146, StarInBottle = 158, BastDefense = 215;

        public static readonly int[] Type = new int[MaxBuffs];
        public static readonly int[] Time = new int[MaxBuffs];

        /// <summary>Lang.GetBuffName / GetBuffTooltip (zh-Hans), Main.debuff (can't be right-clicked away), buffNoTimeDisplay.</summary>
        sealed class Info
        {
            public string Name, Desc;
            public bool Debuff, NoTime, Minion;
        }

        static readonly Dictionary<int, Info> info = new Dictionary<int, Info>
        {
            [PotionSickness] = new Info { Name = "耐药性", Desc = "不能再消耗治疗物品", Debuff = true },
            [WaterCandle] = new Info { Name = "水蜡烛", Desc = "提高怪物生成速度", NoTime = true },
            [CozyFire] = new Info { Name = "温馨之火", Desc = "生命再生速度略有提高", NoTime = true },
            [HeartLamp] = new Info { Name = "心灯", Desc = "生命再生速度提高", NoTime = true },
            [Happy] = new Info { Name = "快乐！", Desc = "移动速度提高，怪物生成减少", NoTime = true },
            [StarInBottle] = new Info { Name = "星星瓶", Desc = "提高魔力再生速度", NoTime = true },
            [BastDefense] = new Info { Name = "巴斯特防御", Desc = "防御力增加5点", NoTime = true },
            [Honey] = new Info { Name = "蜂蜜", Desc = "生命再生速度提高" },
            [Panic] = new Info { Name = "恐慌！", Desc = "移动速度提高" },
            [Werewolf] = new Info { Name = "狼人", Desc = "体能提高", NoTime = true },
            [Merfolk] = new Info { Name = "人鱼", Desc = "可在水下顺畅呼吸和移动", NoTime = true },
            [IceBarrier] = new Info { Name = "冰障", Desc = "所受伤害降低25%", NoTime = true },
            [BrainOfConfusion] = new Info { Name = "控脑术", Desc = "提高暴击率和仆从伤害" },
            [ManaHeat] = new Info { Name = "魔力涌动", Desc = "魔法伤害提高" },
            [BabySlime] = new Info { Name = "史莱姆宝宝", Desc = "史莱姆宝宝将为你而战", NoTime = true, Minion = true },
            [FlinxMinion] = new Info { Name = "小雪怪", Desc = "小雪怪将为你而战", NoTime = true, Minion = true },
            [MushroomBoiMinion] = new Info { Name = "蘑菇小伙伴！", Desc = "蘑菇小伙伴将为你而战", NoTime = true, Minion = true },
            [CattivaMinion] = new Info { Name = "捣蛋猫", Desc = "捣蛋猫将为你而战", NoTime = true, Minion = true },
            [FoxsparksMinion] = new Info { Name = "火绒狐", Desc = "火绒狐将为你而战", NoTime = true, Minion = true },
            [TrustyCattivaMinion] = new Info { Name = "可靠的捣蛋猫", Desc = "捣蛋猫将为你而战", NoTime = true, Minion = true },
            [TrustyFoxsparksMinion] = new Info { Name = "可靠的火绒狐", Desc = "火绒狐将为你而战", NoTime = true, Minion = true },
            [ClayPotMinion] = new Info { Name = "黏土蓓蕾", Desc = "黏土蓓蕾将为你而战", NoTime = true, Minion = true },
            [ForbiddenMinion] = new Info { Name = "禁戒遗迹", Desc = "禁戒遗迹将为你而战", NoTime = true, Minion = true },
            [VampireFrog] = new Info { Name = "吸血鬼青蛙", Desc = "吸血鬼青蛙将为你而战", NoTime = true, Minion = true },
            [SpiderMinion] = new Info { Name = "蜘蛛", Desc = "蜘蛛将为你而战", NoTime = true, Minion = true },
            [PirateMinion] = new Info { Name = "海盗", Desc = "海盗将为你而战", NoTime = true, Minion = true },
            [BabyBird] = new Info { Name = "雀宝宝", Desc = "雀宝宝将为你而战", NoTime = true, Minion = true },
            [HornetMinion] = new Info { Name = "黄蜂", Desc = "黄蜂将为你而战", NoTime = true, Minion = true },
            [ImpMinion] = new Info { Name = "小鬼", Desc = "小鬼将为你而战", NoTime = true, Minion = true },
            [AbigailMinion] = new Info { Name = "阿比盖尔", Desc = "阿比盖尔会为了保护你而战", NoTime = true, Minion = true },
            [TwinEyesMinion] = new Info { Name = "双子魔眼", Desc = "双子魔眼将为你而战", NoTime = true, Minion = true },
            [BatOfLight] = new Info { Name = "血红蝙蝠", Desc = "血红蝙蝠将为你而战", NoTime = true, Minion = true },
            [Ravens] = new Info { Name = "乌鸦", Desc = "乌鸦将攻击你的敌人", NoTime = true, Minion = true },
            [Pygmies] = new Info { Name = "矮人", Desc = "矮人将为你而战", NoTime = true, Minion = true },
            [SharknadoMinion] = new Info { Name = "鲨鱼旋风", Desc = "鲨鱼旋风将为你而战", NoTime = true, Minion = true },
            [UFOMinion] = new Info { Name = "UFO", Desc = "UFO将为你而战", NoTime = true, Minion = true },
            [StardustMinion] = new Info { Name = "星尘细胞", Desc = "星尘细胞将为你而战", NoTime = true, Minion = true },
            [Smolstar] = new Info { Name = "附魔飞刀", Desc = "千刀万剐", NoTime = true, Minion = true },
            [StormTiger] = new Info { Name = "沙漠虎", Desc = "沙漠虎将与你并肩作战", NoTime = true, Minion = true },
            [EmpressBlade] = new Info { Name = "泰拉棱镜", Desc = "女皇之刃将为你而战", NoTime = true, Minion = true },
            [DeadlySphere] = new Info { Name = "致命球", Desc = "致命球将为你而战", NoTime = true, Minion = true },
            [StardustDragonMinion] = new Info { Name = "星尘之龙", Desc = "星尘之龙会保护你", NoTime = true, Minion = true },
            [SwordWhip] = new Info { Name = "迪朗达尔祝福", Desc = "鞭的速度提高" },
            [CoolWhip] = new Info { Name = "一种不错的增益", Desc = "召唤雪花来为你战斗", NoTime = true },
            [ThornWhip] = new Info { Name = "丛林之怒", Desc = "鞭的速度提高" },
            [ScytheWhip] = new Info { Name = "收割时刻", Desc = "鞭的速度提高" },
            [CobWhip] = new Info { Name = "鞭蜘蛛", Desc = "召唤一只蜘蛛来为你战斗", NoTime = true },
            // The armor sets' (accarmor-hm sec-H2).
            [58] = new Info { Name = "快速治疗", Desc = "生命再生速度大幅提高" },
            [59] = new Info { Name = "神圣防护", Desc = "你将闪避下一次攻击" },
            [60] = new Info { Name = "叶状水晶", Desc = "朝附近的敌人射出水晶叶", NoTime = true },
            [95] = new Info { Name = "甲虫耐力", Desc = "吸收15%所受伤害", NoTime = true },
            [96] = new Info { Name = "甲虫耐力", Desc = "吸收30%所受伤害", NoTime = true },
            [97] = new Info { Name = "甲虫耐力", Desc = "吸收45%所受伤害", NoTime = true },
            [98] = new Info { Name = "甲虫力量", Desc = "近战伤害和近战速度各提高10%", NoTime = true },
            [99] = new Info { Name = "甲虫力量", Desc = "近战伤害和近战速度各提高20%", NoTime = true },
            [100] = new Info { Name = "甲虫力量", Desc = "近战伤害和近战速度各提高30%", NoTime = true },
            [170] = new Info { Name = "日耀烈焰", Desc = "吸收20%所受伤害，受到伤害时推开敌人", NoTime = true },
            [171] = new Info { Name = "日耀烈焰", Desc = "吸收20%所受伤害，受到伤害时推开敌人", NoTime = true },
            [172] = new Info { Name = "日耀烈焰", Desc = "吸收20%所受伤害，受到伤害时推开敌人", NoTime = true },
            [173] = new Info { Name = "生命星云", Desc = "提高生命再生速度" },
            [174] = new Info { Name = "生命星云", Desc = "提高生命再生速度" },
            [175] = new Info { Name = "生命星云", Desc = "提高生命再生速度" },
            [176] = new Info { Name = "魔力星云", Desc = "提高魔力再生速度" },
            [177] = new Info { Name = "魔力星云", Desc = "提高魔力再生速度" },
            [178] = new Info { Name = "魔力星云", Desc = "提高魔力再生速度" },
            [179] = new Info { Name = "伤害星云", Desc = "伤害提高15%" },
            [180] = new Info { Name = "伤害星云", Desc = "伤害提高30%" },
            [181] = new Info { Name = "伤害星云", Desc = "伤害提高45%" },
            [187] = new Info { Name = "星尘守卫", Desc = "星尘守卫会保护你", NoTime = true },
            [306] = new Info { Name = "钛金屏障", Desc = "防御碎片环绕着你" },
            // The plain swords' (sweep S1.A).
            [198] = new Info { Name = "惊人一刻", Desc = "下次近战攻击伤害提高400%" },
            [336] = new Info { Name = "丰盛大餐", Desc = "提高生命再生速度" },
        };

        /// <summary>A buff's name and tooltip defined from elsewhere (the potions', Player/PotionBuffs.cs).</summary>
        public static void Define(int type, string name, string desc, bool debuff = false, bool noTime = false)
        {
            if (!info.ContainsKey(type)) info[type] = new Info { Name = name, Desc = desc, Debuff = debuff, NoTime = noTime };
        }

        /// <summary>The minion flags (slime, hornetMinion…), set each tick while the buff holds.</summary>
        static readonly HashSet<int> flags = new HashSet<int>();

        public static string Name(int type) => info.TryGetValue(type, out var i) ? Localized(type, i.Name, true) : "";
        public static string Desc(int type) => info.TryGetValue(type, out var i) ? Localized(type, i.Desc, false) : "";

        static readonly Dictionary<long, string> localized = new Dictionary<long, string>();
        static string localizedFor;

        /// <summary>A buff's name or description in the language now: Terraria's own (BuffName / BuffDescription) where the mod's Chinese is Terraria's, else the mod's table (Lang.Pick).</summary>
        static string Localized(int type, string zh, bool name)
        {
            if (Lang.IsSource || string.IsNullOrEmpty(zh)) return zh;
            if (localizedFor != Lang.Culture)
            {
                localized.Clear();
                localizedFor = Lang.Culture;
            }
            long key = ((long)type << 1) | (name ? 1L : 0L);
            if (localized.TryGetValue(key, out var s)) return s;
            string c = Lang.Culture;
            s = name ? Lang.Pick(zh, Terraria.TerrariaText.BuffName(Lang.Source, type), Terraria.TerrariaText.BuffName(c, type))
                     : Lang.Pick(zh, Terraria.TerrariaText.BuffDescription(Lang.Source, type), Terraria.TerrariaText.BuffDescription(c, type));
            localized[key] = s;
            return s;
        }

        /// <summary>Dev: the buffs whose text is the mod's own with no line in the table yet.</summary>
        public static List<string> DevUntranslated()
        {
            var list = new List<string>();
            foreach (var kv in info)
            {
                string vn = Terraria.TerrariaText.BuffName(Lang.Source, kv.Key), vd = Terraria.TerrariaText.BuffDescription(Lang.Source, kv.Key);
                if (!string.IsNullOrEmpty(kv.Value.Name) && (vn == null || !Lang.Same(vn, kv.Value.Name)) && !Lang.Has(kv.Value.Name)) list.Add(kv.Value.Name);
                if (!string.IsNullOrEmpty(kv.Value.Desc) && (vd == null || !Lang.Same(vd, kv.Value.Desc)) && !Lang.Has(kv.Value.Desc)) list.Add(kv.Value.Desc);
            }
            return list;
        }
        public static bool IsDebuff(int type) => info.TryGetValue(type, out var i) && i.Debuff;
        public static bool NoTime(int type) => (info.TryGetValue(type, out var i) && i.NoTime) || UntilDeath(type);

        /// <summary>The buff stations' buffs (Clairvoyance, Ammo Box, Bewitched, Sharpened, Strategist): on until death, shown without a time.</summary>
        public static bool UntilDeath(int type) => type == 29 || type == 93 || type == 150 || type == 159 || type == 348;

        /// <summary>A time no play outlasts (about 200 days of ticks): the buff goes only with death.</summary>
        public const int Forever = 1 << 30;
        public static bool IsMinionBuff(int type) => info.TryGetValue(type, out var i) && i.Minion;

        public static bool Has(int type)
        {
            for (int i = 0; i < MaxBuffs; i++) if (Type[i] == type && Time[i] > 0) return true;
            return false;
        }

        /// <summary>The ticks a buff has left (0 when it isn't on).</summary>
        public static int TimeLeft(int type)
        {
            for (int i = 0; i < MaxBuffs; i++) if (Type[i] == type) return Time[i];
            return 0;
        }

        /// <summary>The player flag a minion buff sets (slime, spiderMinion…): its minions live while it's on.</summary>
        public static bool Flag(int type) => flags.Contains(type);

        /// <summary>Player.AddBuff: a running buff only lengthens; a new one takes the first free slot after the first non-debuff.</summary>
        public static void Add(int type, int ticks)
        {
            if (type <= 0 || ticks <= 0 || Immune(type)) return;
            for (int i = 0; i < MaxBuffs; i++)
                if (Type[i] == type)
                {
                    if (Time[i] < ticks) Time[i] = ticks;
                    return;
                }
            for (int tries = 0; tries < MaxBuffs; tries++)
            {
                int first = -1;
                for (int i = 0; i < MaxBuffs; i++)
                    if (!IsDebuff(Type[i]) || Type[i] == 0) { first = i; break; }
                if (first < 0) return;
                for (int i = first; i < MaxBuffs; i++)
                    if (Type[i] == 0)
                    {
                        Type[i] = type;
                        Time[i] = ticks;
                        return;
                    }
                // Full: the oldest buff goes.
                DelAt(first);
            }
        }

        /// <summary>Player.DelBuff: the slot empties and the later ones move up.</summary>
        public static void DelAt(int slot)
        {
            if (slot < 0 || slot >= MaxBuffs) return;
            for (int i = slot; i < MaxBuffs - 1; i++)
            {
                Type[i] = Type[i + 1];
                Time[i] = Time[i + 1];
            }
            Type[MaxBuffs - 1] = 0;
            Time[MaxBuffs - 1] = 0;
        }

        /// <summary>Player.ClearBuff: every slot of a type.</summary>
        public static void Remove(int type)
        {
            for (int i = MaxBuffs - 1; i >= 0; i--) if (Type[i] == type) DelAt(i);
            flags.Remove(type);
        }

        /// <summary>
        /// Main.TryRemovingBuff (a right click on the icon): not a debuff; Menu_Tick and DelBuff.
        /// A mirrored buff stops its own timer too.
        /// </summary>
        public static bool TryRemoveAt(int slot)
        {
            int type = Type[slot];
            if (type == 0 || IsDebuff(type)) return false;
            var mod = TerraMod.Instance;
            if (type == Panic && mod != null) mod.PanicTicks = 0f;
            if (type == Honey && mod != null) mod.Stats.HoneyTicks = 0f;
            mod?.PlaySound("Menu_Tick", 0.7f);
            DelAt(slot);
            if (IsMinionBuff(type)) flags.Remove(type);
            return true;
        }

        /// <summary>
        /// Player.UpdateBuffs, once per 60 Hz tick: the mirrored timers, the minion buffs held at
        /// 18000 while their minions live (else gone), the rest counting down and leaving at 0.
        /// Dead, every buff is cleared (UpdateDead).
        /// </summary>
        public static void Tick(bool dead)
        {
            flags.Clear();
            if (dead)
            {
                Clear();
                return;
            }
            var mod = TerraMod.Instance;
            if (mod != null)
            {
                Mirror(PotionSickness, Mathf.CeilToInt(mod.Stats.PotionSickness * 60f));
                Mirror(Honey, Mathf.CeilToInt(mod.Stats.HoneyTicks));
                Mirror(Panic, Mathf.CeilToInt(mod.PanicTicks));
                // The werewolf, the merfolk and the ice barrier hold while their conditions do (Pl:25796, 25779, 14993).
                Mirror(Werewolf, Equipment.WerewolfNow ? 60 : 0);
                Mirror(Merfolk, Equipment.MerfolkNow ? 2 : 0);
                Mirror(IceBarrier, Equipment.IceBarrierNow ? 5 : 0);
            }
            var minions = Minions.Instance;
            for (int i = 0; i < MaxBuffs; i++)
            {
                int type = Type[i];
                if (type == 0) continue;
                if (type == PotionSickness || type == Honey || type == Panic || type == Werewolf || type == Merfolk || type == IceBarrier) continue;
                if (IsMinionBuff(type))
                {
                    // The Finch buff calls a bird back from a Finch Staff when it has none and a slot is free.
                    if (type == BabyBird && minions != null) minions.ReviveFinch();
                    if (minions != null && minions.Owns(type))
                    {
                        flags.Add(type);
                        Time[i] = 18000;
                    }
                    else
                    {
                        DelAt(i);
                        i--;
                    }
                    continue;
                }
                if (--Time[i] <= 0)
                {
                    DelAt(i);
                    i--;
                }
            }
        }

        /// <summary>buffImmune (Pl:5255-5260): the debuffs the gear keeps off.</summary>
        static bool Immune(int type)
        {
            var bits = Equipment.Current.DebuffImmune;
            if (bits == DebuffImmunity.None) return false;
            DebuffImmunity b;
            switch (type)
            {
                case 20: b = DebuffImmunity.Poisoned; break;
                case 22: b = DebuffImmunity.Darkness; break;
                case 23: b = DebuffImmunity.Cursed; break;
                case 24: b = DebuffImmunity.OnFire; break;
                case 30: b = DebuffImmunity.Bleeding; break;
                case 31: b = DebuffImmunity.Confused; break;
                case 32: b = DebuffImmunity.Slow; break;
                case 33: b = DebuffImmunity.Weak; break;
                case 35: b = DebuffImmunity.Silenced; break;
                case 36: b = DebuffImmunity.BrokenArmor; break;
                case 46: b = DebuffImmunity.Chilled; break;
                case 47: b = DebuffImmunity.Frozen; break;
                case 156: b = DebuffImmunity.Stoned; break;
                default: return false;
            }
            return (bits & b) != 0;
        }

        static void Mirror(int type, int ticks)
        {
            int slot = System.Array.IndexOf(Type, type);
            if (ticks <= 0)
            {
                if (slot >= 0) DelAt(slot);
                return;
            }
            if (slot >= 0) Time[slot] = ticks;
            else Add(type, ticks);
        }

        public static void Clear()
        {
            for (int i = 0; i < MaxBuffs; i++)
            {
                Type[i] = 0;
                Time[i] = 0;
            }
            flags.Clear();
        }

        /// <summary>
        /// Lang.LocalizedDuration, abbreviated as every non-English language is: only the largest
        /// unit, rounded up when over it (3600 ticks "1 分", 3601 "2 分"), else seconds.
        /// </summary>
        public static string Duration(int ticks)
        {
            double s = ticks / 60.0;
            if (s >= 86400) return $"{(int)(s / 86400) + (s % 86400 > 0 ? 1 : 0)} 天";
            if (s >= 3600) return $"{(int)(s / 3600) + (s % 3600 > 0 ? 1 : 0)} 时";
            if (s >= 60) return $"{(int)(s / 60) + (s % 60 > 0 ? 1 : 0)} 分";
            return $"{(int)s} 秒";
        }
    }
}
