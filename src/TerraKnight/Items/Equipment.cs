using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>What the worn armor and accessories add up to, recomputed every frame.</summary>
    internal static class Equipment
    {
        public static Bonuses Current;
        /// <summary>The armor set all three worn pieces belong to, if they do.</summary>
        public static ArmorSet FullSet { get; private set; }

        public static void Update(Inventory inv)
        {
            // Worn items plus those shared in from other loadouts (GetEffectiveArmor) count alike.
            inv.RefreshEffective();
            var armor = inv.EffectiveArmor;
            var b = new Bonuses();
            foreach (var a in armor)
            {
                if (a.IsEmpty) continue;
                b.Defense += a.Def.Defense;
                b.Add(a.Def.Bonus);
            }
            foreach (var a in inv.EffectiveAccessories)
            {
                if (a.IsEmpty) continue;
                if (PlayerChallenges.AccessoriesLocked)
                {
                    // Preserve only the dash and the extra jumps themselves (wings stay off), not this accessory's
                    // defense, prefix, stats or other effects.
                    var kept = a.Def.Bonus;
                    if (kept.DashKind != 0) b.DashKind = kept.DashKind;
                    b.JumpCloud |= kept.JumpCloud || a.Def.Accessory == AccessoryEffect.CloudInABottle;
                    b.JumpSandstorm |= kept.JumpSandstorm;
                    b.JumpBlizzard |= kept.JumpBlizzard;
                    b.JumpFart |= kept.JumpFart;
                    b.JumpSail |= kept.JumpSail;
                    continue;
                }
                b.Defense += a.Def.Defense;
                b.Add(a.Def.Bonus);
            }
            // UpdateEquips' post-pass (Pl:13053-13106): what several items share counts once.
            if (b.ManaFlower) b.ManaCost += 0.08f;
            if (b.AmmoCyclerDamage) b.RangedDamage += 0.08f;
            if (b.SharpBarb) b.RangedCrit += 0.08f;
            // The whip-tag bracelets (Pl:13067-13082): a tag more each; the Silver Bracer's tags last three times as long.
            if (b.SilverBracer)
            {
                b.TagDuration += 2f;
                b.MaxTagEffects++;
            }
            if (b.MobiusStrip)
            {
                b.MaxTagEffects++;
                b.WhipRange += 0.1f;
            }
            if (b.WickedArmlet) b.MaxTagEffects++;
            if (b.DD2Accessory)
            {
                b.MinionDamage += 0.1f;
                b.MaxTurrets++;
            }
            // The Mana Cloak's star override comes after the Bee Cloak's, before the Druidic cloak's (Pl:38727-38749).
            if (b.ManaCloak && b.StarKind < CloakStar.Mana) b.StarKind = CloakStar.Mana;
            // skyStoneEffects (Pl:13087-13101): once, while the stone's time of day holds.
            if ((b.SkyStone & (World.TerraTime.IsDay ? SkyStoneTime.Day : SkyStoneTime.Night)) != 0)
            {
                b.LifeRegen += 2;
                b.Defense += 4;
                b.MeleeSpeed += 0.1f;
                b.MeleeDamage += 0.1f;
                b.RangedDamage += 0.1f;
                b.MagicDamage += 0.1f;
                b.MinionDamage += 0.1f;
                b.MeleeCrit += 0.02f;
                b.RangedCrit += 0.02f;
                b.MagicCrit += 0.02f;
                b.PickSpeed -= 0.15f;
                b.MinionKB += 0.5f;
            }
            // accMerman in liquid (Pl:25772-25779; Hallownest's liquid is acid): the merfolk, buff 34. The
            // werewolf at night unless the merfolk is on (buff 28, Pl:11980-11996).
            var hero = HeroController.SilentInstance;
            MerfolkNow = b.Merfolk && Player.AcidLiquid.Swimming(hero);
            WerewolfNow = b.Werewolf && !MerfolkNow && !World.TerraTime.IsDay;
            if (WerewolfNow)
            {
                b.LifeRegen += 1;
                b.MeleeCrit += 0.02f;
                b.MeleeDamage += 0.051f;
                b.MeleeSpeed += 0.051f;
                b.Defense += 3;
                b.MoveSpeed += 0.05f;
            }
            // The Frozen Turtle Shell's ice barrier (buff 62, Pl:10549-10572): a quarter of the damage off at half life or less.
            var stats = TerraMod.Instance?.Stats;
            IceBarrierNow = b.IceBarrier && stats != null && stats.Life > 0 && stats.Life <= stats.LifeMax * 0.5f;
            if (IceBarrierNow) b.Endurance += 0.25f;
            // The Brain of Confusion's buff after a dodge (321, Pl:10027-10034): crit +10, minion damage +10%.
            if (Player.PlayerBuffs.Has(Player.PlayerBuffs.BrainOfConfusion))
            {
                b.MeleeCrit += 0.1f;
                b.RangedCrit += 0.1f;
                b.MagicCrit += 0.1f;
                b.MinionDamage += 0.1f;
            }
            // ArmorSetBonuses.GetCompleteSet: one family on all three pieces; the bonus is the head's pick
            // (Cobalt, Mythril, Adamantite, Hallowed, Chlorophyte, Spectre), the Beetle body's, or the family's.
            var head = armor[0].IsEmpty ? ArmorSet.None : armor[0].Def.Set;
            bool full = head != ArmorSet.None && !armor[1].IsEmpty && !armor[2].IsEmpty && armor[1].Def.Set == head && armor[2].Def.Set == head;
            // The Wizard Hat's and the Magic Hat's sets are the hat and a robe, whatever the legs (Set(238, 1282, 0): no legs needed).
            bool hatAndRobe = (head == ArmorSet.Wizard || head == ArmorSet.MagicHat) && !armor[1].IsEmpty && armor[1].Def.Set == ArmorSet.Robe;
            FullSet = hatAndRobe ? head
                    : !full ? ArmorSet.None
                    : head == ArmorSet.Beetle ? armor[1].Def.SetBonus
                    : armor[0].Def.SetBonus != ArmorSet.None ? armor[0].Def.SetBonus : head;
            b.Add(SetBonus(FullSet));
            b.Add(ArmorEffects.DynamicBonuses());
            // The charm bag's charms (Fury of the Fallen at its low line).
            b.Add(CharmEffects.DynamicBonuses());
            b.Add(MeleeSpecials.DynamicBonuses());
            b.Add(Player.PotionBuffs.Bonuses());
            b.MagicDamage -= Player.PlayerBuffs.ManaSicknessPenalty;
            BossMastery.Apply(ref b);
            Current = b;
        }

        /// <summary>
        /// Player.SetMatch (Pl:37476-37560, male): the legs sheet a robe is drawn with in place of the worn legs (the Robe
        /// 15→88, the gem robes 58-63→91-96, the Gi 165→118, the Mysterious Robe 167→101, the Amber Robe 213→187, the
        /// Flinx Fur Coat 241→229); 0 for a body piece that isn't one.
        /// </summary>
        public static int RobeLegs(int bodySheet)
        {
            switch (bodySheet)
            {
                case 15: return 88;
                case 58: return 91;
                case 59: return 92;
                case 60: return 93;
                case 61: return 94;
                case 62: return 95;
                case 63: return 96;
                case 165: return 118;
                case 167: return 101;
                case 213: return 187;
                case 241: return 229;
                default: return 0;
            }
        }

        /// <summary>The werewolf (night), the merfolk (in acid) and the ice barrier (half life or less) this frame.</summary>
        public static bool WerewolfNow, MerfolkNow, IceBarrierNow;

        /// <summary>Player.IsConsideredStandingStill, set each frame.</summary>
        public static bool StandingStill;

        /// <summary>pickSpeed: a pickaxe's use time multiplied, never below 0.3 (Pl:26170-26173).</summary>
        public static float PickTime => Mathf.Max(0.3f, 1f + Current.PickSpeed);

        /// <summary>Mana Heat (buff 396, Pl:10415-10428): magic damage up to 20% more, fading with the buff's 120 ticks.</summary>
        public static float ManaHeat => 1f + 0.2f * Player.PlayerBuffs.TimeLeft(Player.PlayerBuffs.ManaHeat) / 120f;

        /// <summary>Terraria 1.4's set bonuses for these sets.</summary>
        public static Bonuses SetBonus(ArmorSet set) => OriginalSetBonus(set);

        static Bonuses OriginalSetBonus(ArmorSet set)
        {
            switch (set)
            {
                case ArmorSet.Copper: return new Bonuses { Defense = 2 };
                case ArmorSet.Iron: return new Bonuses { Defense = 2 };
                case ArmorSet.Gold: return new Bonuses { Defense = 3 };
                // ArmorSetBonuses: MetalTier1 (copper, tin, iron) +2, MetalTier2 (silver, gold, lead, tungsten) +3, platinum +4.
                case ArmorSet.Tin: return new Bonuses { Defense = 2 };
                case ArmorSet.Lead: case ArmorSet.Silver: case ArmorSet.Tungsten: return new Bonuses { Defense = 3 };
                case ArmorSet.Platinum: return new Bonuses { Defense = 4 };
                case ArmorSet.Shadow: return default; // Run-speed/acceleration multipliers are applied in TerraMovement.
                case ArmorSet.Necro: return new Bonuses { RangedCrit = 0.1f };
                case ArmorSet.Jungle: return new Bonuses { ManaCost = 0.16f };
                // 1.4.5: +10% (ArmorSetBonuses:283-291).
                case ArmorSet.Molten: return new Bonuses { MeleeDamage = 0.1f, FireWalk = true, DebuffImmune = DebuffImmunity.OnFire };
                case ArmorSet.Bee: return new Bonuses { MinionDamage = 0.1f };
                case ArmorSet.Spider: return new Bonuses { MinionDamage = 0.12f };
                case ArmorSet.Wood: return new Bonuses { Defense = 1 };
                case ArmorSet.Obsidian: return new Bonuses { MinionDamage = 0.15f, WhipRange = 0.3f, WhipFaster = 0.15f };
                // ArmorSetBonuses (accarmor-hm sec-H2); what acts rather than adds is in ArmorEffects.
                case ArmorSet.CobaltCaster: return new Bonuses { ManaCost = 0.14f };
                case ArmorSet.CobaltMelee: return new Bonuses { MeleeSpeed = 0.15f };
                case ArmorSet.CobaltRanged: return new Bonuses { AmmoSave = 0.2f };
                case ArmorSet.MythrilCaster: return new Bonuses { ManaCost = 0.17f };
                case ArmorSet.MythrilMelee: return new Bonuses { MeleeCrit = 0.1f };
                case ArmorSet.MythrilRanged: return new Bonuses { AmmoSave = 0.2f };
                case ArmorSet.AdamantiteCaster: return new Bonuses { ManaCost = 0.19f };
                case ArmorSet.AdamantiteMelee: return new Bonuses { MeleeSpeed = 0.2f, MoveSpeed = 0.2f };
                case ArmorSet.AdamantiteRanged: return new Bonuses { AmmoSave = 0.25f };
                case ArmorSet.Frost: return new Bonuses { MeleeDamage = 0.1f, RangedDamage = 0.1f };
                case ArmorSet.HallowedSummoner: return new Bonuses { MaxMinions = 2 };
                case ArmorSet.CrystalAssassin: return new Bonuses { MeleeDamage = 0.1f, RangedDamage = 0.1f, MagicDamage = 0.1f, MinionDamage = 0.1f, AllCrit = 0.1f };
                case ArmorSet.ChlorophyteMelee: return new Bonuses { Endurance = 0.05f };
                case ArmorSet.ChlorophyteSummon: return new Bonuses { MaxMinions = 2 };
                case ArmorSet.Turtle: return new Bonuses { Endurance = 0.15f };
                case ArmorSet.Tiki: return new Bonuses { MaxMinions = 1, WhipRange = 0.2f };
                case ArmorSet.Spooky: return new Bonuses { MinionDamage = 0.25f };
                case ArmorSet.SpectreHealing: return new Bonuses { MagicDamage = -0.4f };
                case ArmorSet.Solar: return new Bonuses { Endurance = 0.12f };
                // ArmorSetBonuses.Benefits (1.4.5) of the sets in ItemsArmor2.cs.
                case ArmorSet.Ebonwood: case ArmorSet.RichMahogany: case ArmorSet.Pearlwood: case ArmorSet.Shadewood: case ArmorSet.BorealWood: case ArmorSet.PalmWood:
                    return new Bonuses { Defense = 1 };
                case ArmorSet.AshWood: return new Bonuses { AshWood = true };
                case ArmorSet.Meteor: return new Bonuses { SpaceGunFree = true };
                case ArmorSet.Ninja: return new Bonuses { MoveSpeed = 0.2f };
                case ArmorSet.Mining: return new Bonuses { PickSpeed = -0.1f };
                case ArmorSet.Crimson: return new Bonuses { CrimsonRegen = true };
                case ArmorSet.Snow: return new Bonuses { DebuffImmune = DebuffImmunity.Chilled | DebuffImmunity.Frozen };
                case ArmorSet.Cactus: return new Bonuses { CactusThorns = true };
                case ArmorSet.Wizard: return new Bonuses { MagicCrit = 0.1f };
                case ArmorSet.MagicHat: return new Bonuses { ManaMax = 60 };
                case ArmorSet.Pumpkin: return new Bonuses { MeleeDamage = 0.1f, RangedDamage = 0.1f, MagicDamage = 0.1f, MinionDamage = 0.1f };
                case ArmorSet.Angler: return new Bonuses { AnglerSet = true };
                case ArmorSet.Gladiator: return new Bonuses { NoKnockback = true };
                case ArmorSet.Fossil: return new Bonuses { AmmoSave = 0.2f };
                case ArmorSet.SquireT2: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.SquireT2 };
                case ArmorSet.ApprenticeT2: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.ApprenticeT2 };
                case ArmorSet.HuntressT2: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.HuntressT2 };
                case ArmorSet.MonkT2: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.MonkT2 };
                case ArmorSet.SquireT3: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.SquireT2 | OoaSet.SquireT3 };
                case ArmorSet.ApprenticeT3: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.ApprenticeT2 | OoaSet.ApprenticeT3 };
                case ArmorSet.HuntressT3: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.HuntressT2 | OoaSet.HuntressT3 };
                case ArmorSet.MonkT3: return new Bonuses { MaxTurrets = 1, Ooa = OoaSet.MonkT2 | OoaSet.MonkT3 };
                default: return default;
            }
        }

        /// <summary>The family a set bonus belongs to (the tooltip shows it on that family's pieces).</summary>
        public static ArmorSet FamilyOf(ArmorSet set)
        {
            switch (set)
            {
                case ArmorSet.CobaltCaster: case ArmorSet.CobaltMelee: case ArmorSet.CobaltRanged: return ArmorSet.Cobalt;
                case ArmorSet.MythrilCaster: case ArmorSet.MythrilMelee: case ArmorSet.MythrilRanged: return ArmorSet.Mythril;
                case ArmorSet.AdamantiteCaster: case ArmorSet.AdamantiteMelee: case ArmorSet.AdamantiteRanged: return ArmorSet.Adamantite;
                case ArmorSet.HallowedSummoner: return ArmorSet.Hallowed;
                case ArmorSet.ChlorophyteMelee: case ArmorSet.ChlorophyteSummon: return ArmorSet.Chlorophyte;
                case ArmorSet.SpectreHealing: case ArmorSet.SpectreDamage: return ArmorSet.Spectre;
                case ArmorSet.BeetleDamage: case ArmorSet.BeetleDefense: return ArmorSet.Beetle;
                default: return set;
            }
        }

        public static string SetBonusText(ArmorSet set) => OriginalSetBonusText(set);

        static string OriginalSetBonusText(ArmorSet set)
        {
            switch (set)
            {
                case ArmorSet.Copper: return "防御力 +2";
                case ArmorSet.Iron: return "防御力 +2";
                case ArmorSet.Gold: return "防御力 +3";
                case ArmorSet.Tin: return "防御力 +2";
                case ArmorSet.Lead: case ArmorSet.Silver: case ArmorSet.Tungsten: return "防御力 +3";
                case ArmorSet.Platinum: return "防御力 +4";
                case ArmorSet.Shadow: return "最高跑速提高15%，加速度和减速能力提高75%";
                case ArmorSet.Necro: return "远程暴击率提高10%";
                case ArmorSet.Jungle: return "魔力消耗降低16%";
                case ArmorSet.Molten: return "近战伤害提高10%\n免疫火块和着火";
                case ArmorSet.Bee: return "召唤伤害提高10%";
                case ArmorSet.Spider: return "召唤伤害提高12%";
                case ArmorSet.Wood: return "防御力 +1";
                case ArmorSet.Obsidian: return "鞭子攻击范围扩大30%，速度提高15%，\n召唤伤害提高15%";
                // Lang ArmorSetBonus (zh-Hans Game.json 1351-1418); the armor key is double-tapping down.
                case ArmorSet.CobaltCaster: return "魔力值消耗降低14%";
                case ArmorSet.CobaltMelee: return "近战速度提高15%";
                case ArmorSet.CobaltRanged: return "20%几率省下弹药";
                case ArmorSet.MythrilCaster: return "魔力值消耗降低17%";
                case ArmorSet.MythrilMelee: return "近战暴击率提高10%";
                case ArmorSet.MythrilRanged: return "20%几率省下弹药";
                case ArmorSet.AdamantiteCaster: return "魔力值消耗降低19%";
                case ArmorSet.AdamantiteMelee: return "近战速度和移动速度各提高20%";
                case ArmorSet.AdamantiteRanged: return "25%几率省下弹药";
                case ArmorSet.Palladium: return "攻击敌人后大大提高生命再生速度";
                case ArmorSet.Orichalcum: return "花瓣将落到目标身上造成额外伤害";
                case ArmorSet.Titanium: return "攻击生成钛金碎片防御屏障";
                case ArmorSet.Frost: return "近战和远程攻击会造成霜冻\n近战和远程伤害各提高10%";
                case ArmorSet.Forbidden: return "双击下可在光标位置召唤远古风暴";
                case ArmorSet.Hallowed: return "攻击敌人后进入免疫状态";
                case ArmorSet.HallowedSummoner: return "攻击敌人后进入免疫状态，\n仆从数量上限增加2";
                case ArmorSet.CrystalAssassin: return "可猛冲\n伤害和暴击率各提高10%";
                case ArmorSet.Chlorophyte: return "召唤强大的水晶叶瓣来射击附近的敌人";
                case ArmorSet.ChlorophyteMelee: return "召唤强大的水晶叶瓣来射击附近的敌人\n所受伤害降低5%";
                case ArmorSet.ChlorophyteSummon: return "召唤强大的叶状水晶来射击附近的敌人\n仆从数量上限增加2个";
                case ArmorSet.Turtle: return "攻击者也会承受双倍伤害\n所受伤害降低15%";
                case ArmorSet.Tiki: return "仆从数量上限增加1\n鞭子攻击范围扩大20%";
                case ArmorSet.Spooky: return "召唤伤害提高25%";
                case ArmorSet.Shroomite: return "静止不动时进入隐形状态，\n提高远程能力并降低敌人以你为目标的几率";
                case ArmorSet.SpectreHealing: return "魔法伤害降低40%并将其转化为治愈力\n对敌人造成的魔法伤害将治疗生命值最低的玩家";
                case ArmorSet.SpectreDamage: return "所造成的魔法伤害还能伤及附近的其他敌人";
                case ArmorSet.BeetleDamage: return "甲虫增加你的近战伤害和速度";
                case ArmorSet.BeetleDefense: return "甲虫保护你不受伤害";
                case ArmorSet.Solar: return "所受伤害减少12%\n日耀护盾可以随着时间推移补充能量，保护你，让你能猛冲\n能量可以用来伤害你触碰到的敌人\n消耗的能量会爆炸并伤害敌人";
                case ArmorSet.Vortex: return "双击下切换隐形，\n提高远程能力并降低敌人以你为目标的几率，但会减慢移动速度";
                case ArmorSet.Nebula: return "魔法伤害有几率生成增益强化道具\n拾取强化道具可获得堆叠增益";
                case ArmorSet.Stardust: return "星尘守卫将保护你不受附近敌人的伤害";
                case ArmorSet.Ebonwood: case ArmorSet.RichMahogany: case ArmorSet.Pearlwood: case ArmorSet.Shadewood: case ArmorSet.BorealWood: case ArmorSet.PalmWood:
                    return "防御力 +1";
                case ArmorSet.AshWood: return "降低因触碰熔岩而受到的伤害";
                case ArmorSet.Meteor: return "太空枪不消耗魔力";
                case ArmorSet.Ninja: return "移动速度提高20%";
                // zh-Hans has the Snow set's line here by mistake; the bonus is the pieces' own mining speed.
                case ArmorSet.Mining: return "挖矿速度提高10%";
                case ArmorSet.Crimson: return "大大提高生命再生速度";
                case ArmorSet.Snow: return "不会被冷冻或冰冻";
                case ArmorSet.Cactus: return "攻击者因为仙人掌尖刺受到伤害";
                case ArmorSet.Wizard: return "魔法暴击率提高10%";
                case ArmorSet.MagicHat: return "最大魔力增加60";
                case ArmorSet.Pumpkin: return "伤害提高10%";
                case ArmorSet.Angler: return "敌人生成速度降低";
                case ArmorSet.Gladiator: return "对击退免疫";
                case ArmorSet.Fossil: return "20%几率省下弹药";
                case ArmorSet.SquireT2: return "哨兵数量上限增加1\n弩车可以刺穿更多目标，你受到伤害时会得到弩车恐慌增益";
                case ArmorSet.ApprenticeT2: return "哨兵数量上限增加1\n焰爆的视野和范围大幅增加";
                case ArmorSet.HuntressT2: return "哨兵数量上限增加1\n爆炸机关充能更快，并会给敌人涂油\n点燃涂油的敌人可造成额外伤害";
                case ArmorSet.MonkT2: return "哨兵数量上限增加1\n闪电光环现在可以暴击，并且打击得更快";
                case ArmorSet.SquireT3: return "哨兵数量上限增加1\n大幅提升弩车效力";
                case ArmorSet.ApprenticeT3: return "哨兵数量上限增加1\n极大提升焰爆效力";
                case ArmorSet.HuntressT3: return "哨兵数量上限增加1\n极大提升爆炸机关效力";
                case ArmorSet.MonkT3: return "哨兵数量上限增加1\n极大提升闪电光环效力";
                default: return null;
            }
        }

        /// <summary>Damage of an item with the worn bonuses (Terraria rounds the multiplied damage).</summary>
        /// <summary>
        /// Player.GetWeaponDamage (truncated, with its 5e-6 nudge) plus, for a bow or gun, the
        /// ammo's damage under the same bonus, truncated on its own (PickAmmo).
        /// </summary>
        public static int Damage(ItemDef item, ItemDef ammo = null)
        {
            var b = Current;
            float mult = 1f;
            switch (item.Class)
            {
                case DamageClass.Melee: mult += b.MeleeDamage + CharmEffects.NailBonus(item); break;
                case DamageClass.Ranged:
                    mult += b.RangedDamage + (ammo != null && ammo.Ammo == AmmoKind.Arrow ? b.ArrowDamage : 0f);
                    // GetWeaponDamageMultiplier (Pl:54028-54060): the Shroomite heads' arrowDamage / bulletDamage / specialistDamage ×1.12 on the whole.
                    if (ammo != null && ammo.Ammo == AmmoKind.Arrow) mult *= 1f + b.ArrowMult;
                    else if (ammo != null && ammo.Ammo == AmmoKind.Bullet) mult *= 1f + b.BulletMult;
                    else if (IsSpecialist(item)) mult *= 1f + b.SpecialistMult;
                    break;
                case DamageClass.Magic: mult += b.MagicDamage; break;
                case DamageClass.Summon: mult += b.MinionDamage; break;
            }
            if (item.Class == DamageClass.Magic) mult *= ManaHeat;
            int dmg = (int)(item.Damage * mult + 5e-6f) + (ammo != null ? (int)(ammo.Damage * mult) : 0);
            // Soul Eater's whip mode: a whip doubled.
            dmg = CharmEffects.WhipDamage(item, dmg);
            return UnityEngine.Mathf.Max(1, dmg);
        }

        /// <summary>
        /// AmmoID.Sets.IsSpecialist (rockets, Stynger bolts, Jack 'O Lanterns, nails, flares, darts,
        /// snowballs, Fallen Stars, gel) and ItemID.Sets.IsRangedSpecialistWeapon (ID:1311).
        /// </summary>
        static bool IsSpecialist(ItemDef item)
        {
            var kind = item.UseAmmo != AmmoKind.None ? item.UseAmmo : item.Ammo;
            switch (kind)
            {
                case AmmoKind.Rocket: case AmmoKind.StyngerBolt: case AmmoKind.JackOLantern: case AmmoKind.Nail: case AmmoKind.Flare:
                case AmmoKind.Dart: case AmmoKind.Snowball: case AmmoKind.FallenStar: case AmmoKind.Gel:
                    return true;
            }
            switch (item.Type)
            {
                case ItemType.PiranhaGun: case ItemType.PaintballGun: case ItemType.Toxikarp: case ItemType.Harpoon: case ItemType.Sandgun:
                case ItemType.AcornSlingshot: case ItemType.AleTosser: case ItemType.BarrelLauncher:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Terraria's "Autoswing" setting (Main.SettingsEnabled_AutoReuseAllItems, Player.autoReuseAllWeapons): every weapon
        /// goes on while the button is held (TryAllowingItemReuse).
        /// </summary>
        public static bool AutoReuseAll;

        /// <summary>Whether an item goes on being used while the button is held: its own autoReuse, or melee with the auto-reuse glove.</summary>
        public static bool AutoReuses(ItemDef item) => item.AutoReuse || (Current.AutoReuseGlove && (item.Class == DamageClass.Melee || item.IsWhip))
            // The Stress Ball (Pl:14447-14466, 53546-53558): standing still, with the inventory shut, any weapon goes on.
            || (Current.StressBall && StandingStill && !InventoryGui.Open && item.Damage > 0 && !item.Channel)
            // TryAllowingItemReuse (Pl:53553): (stressBall || autoReuseAllWeapons) && damage > 0 && !channel.
            || (AutoReuseAll && item.Damage > 0 && !item.Channel);

        /// <summary>Crit chance in percent: base 4, the item's own, the worn bonuses.</summary>
        public static float Crit(ItemDef item)
        {
            // Summon items never crit (GetWeaponCrit returns 0); only a whip tag's chance does, on a minion hit.
            if (item.Class == DamageClass.Summon) return 0f;
            var b = Current;
            float c = 4f + item.Crit + b.AllCrit * 100f;
            // The Revolver's timing bonus (Item.cs:641, P:11134).
            if (item.Type == ItemType.Revolver && TerraMod.RevolverCrit > 0) c += TerraMod.RevolverCrit;
            // A forged ranged weapon: half a point a level (Forging.RangedCrit).
            c += Forging.RangedCrit(Forging.RangedLevel(item));
            switch (item.Class)
            {
                case DamageClass.Melee: c += b.MeleeCrit * 100f; break;
                case DamageClass.Ranged: c += b.RangedCrit * 100f; break;
                case DamageClass.Magic: c += b.MagicCrit * 100f; break;
            }
            return c;
        }

        /// <summary>GetWeaponKnockback: the Power Glove doubles a melee weapon's knockback.</summary>
        /// <summary>The item's knockback, with the minion bonus, the Titan Glove's ×2 for melee and the Titan potion's ×1.5 (kbBuff, GetWeaponKnockback).</summary>
        public static float Knockback(ItemDef item) => (item.Knockback + (item.Class == DamageClass.Summon ? Current.MinionKB : 0f)) * (item.Class == DamageClass.Melee && Current.KbGlove ? 2f : 1f) * (Current.KbBuff ? 1.5f : 1f);

        /// <summary>maxMinions: one, and what the gear and buffs add.</summary>
        public static int MaxMinions => 1 + Current.MaxMinions;

        /// <summary>maxTurrets: one sentry, and what the gear adds.</summary>
        public static int MaxTurrets => 1 + Current.MaxTurrets;

        /// <summary>
        /// A whip's animation scale (ApplyItemAnimation): 1 / min(3, melee speed + the whip buffs
        /// (Durendal +0.25, Snapthorn +0.12)), times the Obsidian set's 1/1.15.
        /// </summary>
        public static float WhipScale()
        {
            float speed = 1f + Current.MeleeSpeed;
            if (Player.PlayerBuffs.Has(Player.PlayerBuffs.SwordWhip)) speed += 0.25f;
            if (Player.PlayerBuffs.Has(Player.PlayerBuffs.ThornWhip)) speed += 0.12f;
            if (Player.PlayerBuffs.Has(Player.PlayerBuffs.ScytheWhip)) speed += 0.35f;
            return 1f / Mathf.Min(3f, speed) / (1f + Current.WhipFaster);
        }

        /// <summary>ApplyMeleeScale: the Power Glove makes melee weapons a tenth bigger.</summary>
        public static float ItemScale(ItemDef item) => item.Scale * (item.Class == DamageClass.Melee && Current.MeleeScaleGlove ? 1.1f : 1f)
            // Mark of Pride and Longnail (CharmEffects.MeleeReach).
            * (item.Class == DamageClass.Melee ? CharmEffects.MeleeReach : 1f);

        /// <summary>A use's length with melee speed: Terraria divides melee useAnimation by the melee speed.</summary>
        /// <summary>
        /// The use-time multiplier: whips by their own speed; melee by meleeSpeed after CapAttackSpeeds
        /// (Pl:29251-29269: attack speed at most ×3, so never below a third).
        /// </summary>
        public static float UseScale(ItemDef item) =>
            item.IsWhip ? WhipScale() : item.Class == DamageClass.Melee && !item.ShootsFinalFractal ? 1f / UnityEngine.Mathf.Min(3f, 1f + Current.MeleeSpeed)
            // Spell Twister: magic 30% quicker.
            : item.Class == DamageClass.Magic ? CharmEffects.MagicUseScale
            // A remade ranged weapon's burst, a machine gun's quickening (RangedArts.UseScale).
            : item.Class == DamageClass.Ranged ? RangedArts.UseScale(item) : 1f;

        /// <summary>The mana a use costs; the Tome of Infinite Wisdom's twister twice its own (Pl:53317-53325).</summary>
        public static int ManaCost(ItemDef item)
        {
            if (item.Mana <= 0) return 0;
            // The Meteor set (spaceGun, Pl:51200, 53333): the Space Gun, the Zapinators and the Laser Rifle cost nothing.
            if (Current.SpaceGunFree && (item.Type == ItemType.SpaceGun || item.Type == ItemType.ZapinatorGray || item.Type == ItemType.ZapinatorOrange || item.Type == ItemType.LaserRifle)) return 0;
            int mana = item.Type == ItemType.BookStaff && TerraMod.Instance != null && TerraMod.Instance.AltFunction ? item.Mana * 2 : item.Mana;
            // Spell Twister's tenth and Kingsoul's half for magic (CharmEffects.ManaFactor) on top of the gear's.
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.FloorToInt(mana * (1f - UnityEngine.Mathf.Clamp01(Current.ManaCost)) * CharmEffects.ManaFactor(item)));
        }
    }
}
