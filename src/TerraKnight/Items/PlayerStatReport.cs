using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Read-only view of the same final values combat and movement use.</summary>
    internal static class PlayerStatReport
    {
        internal readonly struct Row
        {
            public readonly string Label, Value;
            public Row(string label, string value) { Label = label; Value = value; }
        }
        internal static string Percent(float value) => (value * 100f).ToString("0.##") + "%";
        internal static float EnemyHitReduction(float endurance) => 1f - (1f - endurance) * ArmorEffects.PreviewHurtMultiplier(true);
        static string Number(float value) => value.ToString("0.##");
        static string ClassName(DamageClass value) => value == DamageClass.Melee ? "近战" : value == DamageClass.Ranged ? "远程"
            : value == DamageClass.Magic ? "魔法" : value == DamageClass.Summon ? "召唤" : "通用";

        /// <summary>The permanent boosts used (the items' names), the life fruits eaten among them.</summary>
        static string Boosts(Player.TerraStats stats)
        {
            var names = new List<string>();
            if (stats.LifeFruits > 0) names.Add($"生命果 ×{stats.LifeFruits}");
            foreach (var def in ItemDb.All)
                if (def != null && ItemDb.Get(def.Type) == def && def.Boost != PermanentBoost.None && def.Boost != PermanentBoost.LifeFruit && stats.Boosts.Contains(def.Boost))
                    names.Add(def.Name);
            return names.Count == 0 ? "无" : string.Join("、", names);
        }

        internal static ItemDef CurrentWeapon(out ItemDef ammo)
        {
            ammo = null;
            var inv = TerraMod.Instance?.Inventory;
            if (inv == null || inv.Current.IsEmpty || inv.Current.Def.Damage <= 0) return null;
            var weapon = inv.Current.Def; // ItemStack.Def already applies its prefix and Forge.
            if (weapon.UseAmmo != AmmoKind.None)
            {
                int slot = inv.FindAmmo(weapon.UseAmmo, Equipment.Current.AmmoCycling, AccessoryEffects.AmmoOffset);
                if (slot >= 0) ammo = inv.Slots[slot].Def;
            }
            return weapon;
        }

        public static List<Row> Capture(int tab)
        {
            if (tab == 4) return PlayerCollectionReport.Capture();
            var rows = new List<Row>();
            var mod = TerraMod.Instance;
            if (mod == null) return rows;
            var stats = mod.Stats;
            var inv = mod.Inventory;
            var b = Equipment.Current;
            void Add(string label, object value) => rows.Add(new Row(label, value?.ToString() ?? "—"));
            var weapon = CurrentWeapon(out var ammo);
            Add("挑战伤害倍率", "×" + Number(PlayerChallenges.DamageShare));
            Add("挑战生命倍率", "×" + Number(PlayerChallenges.LifeShare));
            Add("恢复锁", PlayerChallenges.RecoveryLocked ? "战斗回血关闭，药水治疗70%" : "未生效");
            if (tab == 0)
            {
                Add("生命", $"{stats.Life} / {stats.LifeMax}");
                Add("额外护盾", stats.Shield);
                Add("魔力", $"{stats.Mana} / {stats.ManaMax}");
                Add("防御（含锻造）", stats.Defense);
                Add("防御抵消伤害", Number(stats.Defense * World.GameDifficulty.DefenseEffectiveness));
                Add("免伤（防御后生效）", Percent(stats.Endurance));
                Add("当前敌袭总比例免伤", Percent(EnemyHitReduction(stats.Endurance)));
                Add("护甲临时护盾 / 甲虫层数", $"{ArmorEffects.SolarShields} / {ArmorEffects.BeetleOrbs}");
                if (weapon != null) Add("当前武器伤害", Equipment.Damage(weapon, ammo));
                Add("近战增伤", Percent(b.MeleeDamage));
                Add("远程增伤", Percent(b.RangedDamage));
                Add("魔法增伤", Percent(b.MagicDamage));
                Add("召唤增伤", Percent(b.MinionDamage));
                Add("首杀 Boss 数量", BossMastery.Count);
                Add("首杀永久增伤 / 免伤", $"+{Percent(BossMastery.Bonus)} / +{Percent(BossMastery.Bonus)}");
                Add("召唤栏（已占 / 上限）", $"{Number(Minions.Instance?.SlotsUsed ?? 0f)} / {Equipment.MaxMinions}");
                Add("哨兵（已用 / 上限）", $"{Minions.Instance?.DevSentries() ?? 0} / {Equipment.MaxTurrets}");
                Add("装备生命再生加成", Number(stats.LifeRegenBonus / 2f) + " / 秒");
                Add("魔力再生加成", stats.ManaRegenBonus);
                Add("魔力再生（当前魔力下，移动 / 站立）", $"{Number(stats.ManaRegenRate(false))} / {Number(stats.ManaRegenRate(true))} 每秒");
                if (stats.Boosts.Contains(PermanentBoost.ArcaneCrystal))
                {
                    // The Arcane Crystal (Pl:19551-19577): the wait 5% shorter, manaMax / 50 more regeneration (none below 50 mana).
                    float more = stats.ManaRegenRate(false) - stats.ManaRegenRate(false, false);
                    Add("奥术水晶", "再生等待缩短 5%" + (more > 0f ? $"，回魔每秒多 {Number(more)}" : "；最大魔力 50 以上时回魔也更快"));
                }
                Add("已用永久增益", Boosts(stats));
                Add("魔力消耗减免", Percent(Mathf.Clamp01(b.ManaCost)));
                Add("弹药节省概率", Percent(b.AmmoSave));
                Add("药水病剩余", Number(stats.PotionSickness) + " 秒");
            }
            else if (tab == 1)
            {
                if (weapon == null) { Add("当前手持", "未手持可造成伤害的武器"); return rows; }
                Add("当前武器", weapon.Name);
                Add("伤害类型", ClassName(weapon.Class));
                Add("武器面板伤害（含锻造、词缀）", weapon.Damage);
                Add(ammo != null ? "实际武器 + 弹药伤害" : "实际武器伤害", Equipment.Damage(weapon, ammo));
                if (weapon.UseAmmo != AmmoKind.None) Add("当前弹药", ammo != null ? ammo.Name : "缺少弹药；伤害未计弹药");
                Add("伤害说明", "未计敌人防御、暴击与命中时的特殊效果");
                Add("锻造等级", "+" + weapon.Forge);
                if (weapon.Class == DamageClass.Ranged)
                {
                    Add("锻造盔甲穿透", Forging.ArmorPenOf(weapon));
                    Add("暴击伤害", Mathf.RoundToInt((2f + Forging.RangedCritDamage(Forging.RangedLevel(weapon))) * 100f) + "%");
                }
                Add("暴击率", Number(Equipment.Crit(weapon)) + "%");
                Add("击退", Number(Equipment.Knockback(weapon)));
                Add("使用间隔", Number(weapon.UseTime * Equipment.UseScale(weapon)) + " 秒");
                Add("动作时长", Number(weapon.UseAnimation * Equipment.UseScale(weapon)) + " 秒");
                Add("每次魔力消耗", Equipment.ManaCost(weapon));
                Add("自动挥舞", Equipment.AutoReuses(weapon) ? "开启" : "关闭");
                Add("武器尺寸倍率", "×" + Number(Equipment.ItemScale(weapon)));
                if (weapon.Class == DamageClass.Summon && (weapon.Minion != MinionKind.None || weapon.Sentry))
                {
                    // A minion's or sentry's blow: the accessories', the staff's prefix's and its own (MeleeHits.MinionArmorPen;
                    // the Blade Staff's daggers 25 more, as its tooltip says).
                    int own = MeleeHits.MinionArmorPen + (weapon.Minion == MinionKind.BladeStaff ? 25 : 0);
                    Add("护甲穿透（召唤物）", b.ArmorPen + weapon.PrefixArmorPen + own);
                    Add("其中召唤物自带", own);
                }
                else Add("护甲穿透", b.ArmorPen);
                Add("魔力热增伤倍率", "×" + Number(Equipment.ManaHeat));
            }
            else if (tab == 2)
            {
                for (int i = 0; i < inv.EffectiveArmor.Length; i++)
                {
                    var stack = inv.EffectiveArmor[i];
                    Add(i == 0 ? "头部" : i == 1 ? "上衣" : "腿部", stack.IsEmpty ? "未装备" : stack.Def.Name + $"（锻造 +{stack.Forge}，防御 {stack.Def.Defense}）");
                }
                for (int i = 0; i < inv.EffectiveAccessories.Length; i++)
                {
                    var stack = inv.EffectiveAccessories[i];
                    Add("生效饰品 " + (i + 1), stack.IsEmpty ? "未装备" : stack.Def.Name + (PlayerChallenges.AccessoriesLocked ? "（饰品锁：仅保留冲刺和多段跳）" : ""));
                }
                Add("套装奖励", Equipment.SetBonusText(Equipment.FullSet) ?? "无完整套装奖励");
                Add("装备范围", "当前配装及共享生效装备；时装不计入属性");
            }
            else
            {
                Add("近战暴击加成", Percent(b.AllCrit + b.MeleeCrit));
                Add("远程暴击加成", Percent(b.AllCrit + b.RangedCrit));
                Add("魔法暴击加成", Percent(b.AllCrit + b.MagicCrit));
                Add("近战攻速加成", Percent(b.MeleeSpeed));
                Add("箭类额外增伤", Percent(b.ArrowDamage));
                Add("箭 / 子弹 / 专家武器倍率", $"×{Number(1f + b.ArrowMult)} / ×{Number(1f + b.BulletMult)} / ×{Number(1f + b.SpecialistMult)}");
                Add("召唤击退加成", Number(b.MinionKB));
                Add("鞭子范围加成", Percent(b.WhipRange));
                Add("鞭子动作时间倍率", "×" + Number(Equipment.WhipScale()));
                Add("鞭标上限 / 持续时间倍率", $"{1 + b.MaxTagEffects} / ×{Number(1f + b.TagDuration)}");
                Add("移动速度倍率", "×" + Number(Player.TerraMovement.MoveSpeed));
                Add("靴子奔跑速度", Number(Player.TerraMovement.AccRunSpeed));
                Add("额外跳跃速度", Number(b.JumpSpeedBoost));
                Add("飞行时间（剩余 / 上限）", $"{Number(Player.TerraMovement.WingTimeLeft / 60f)} / {Number(Player.TerraMovement.WingTimeMax / 60f)} 秒");
                Add("挖掘用时倍率", "×" + Number(Equipment.PickTime));
                Add("放置速度加成", Percent(b.TileSpeed));
                Add("额外建造距离", b.BlockRange + " 格");
                Add("钓鱼技能加成", b.FishingSkill);
                Add("幸运加成", Number(b.Luck));
                Add("反伤比例", Percent(b.Thorns));
                Add("摔落伤害免疫", b.NoFallDamage ? "生效" : "无");
                Add("击退免疫", b.NoKnockback ? "生效" : "无");
                Add("延长受伤无敌时间", b.LongImmunity ? "生效" : "无");
                Add("冰障 / 狼人 / 人鱼", $"{(Equipment.IceBarrierNow ? "生效" : "未生效")} / {(Equipment.WerewolfNow ? "生效" : "未生效")} / {(Equipment.MerfolkNow ? "生效" : "未生效")}");
                for (int i = 0; i < Player.PlayerBuffs.MaxBuffs; i++)
                {
                    int type = Player.PlayerBuffs.Type[i];
                    if (type <= 0 || Player.PlayerBuffs.Time[i] <= 0) continue;
                    string remaining = Player.PlayerBuffs.NoTime(type) ? "持续生效" : Number(Player.PlayerBuffs.Time[i] / 60f) + " 秒";
                    Add("当前状态 · " + Player.PlayerBuffs.Name(type), remaining + " · " + Player.PlayerBuffs.Desc(type));
                }
                foreach (var stack in inv.EffectiveArmor) Effects(stack);
                foreach (var stack in inv.FunctionalAccessories) Effects(stack);
                void Effects(ItemStack stack)
                {
                    if (stack.IsEmpty || string.IsNullOrWhiteSpace(stack.Def.Tooltip)) return;
                    foreach (var line in stack.Def.Tooltip.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(line)) Add(stack.Def.Name, line.Trim());
                }
            }
            return rows;
        }
    }
}
