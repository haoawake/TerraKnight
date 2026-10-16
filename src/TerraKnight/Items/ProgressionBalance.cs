using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Fixed item tiers and budgets. No item scales with the owner's progress.</summary>
    internal static partial class ProgressionBalance
    {
        internal sealed class ArmorPath
        {
            public readonly int Stage;
            public readonly Kit Class;
            public readonly ItemType[] Items;
            public ArmorPath(int stage, Kit cls, ItemType[] items) { Stage = stage; Class = cls; Items = items; }
        }
        internal sealed class BossReward
        {
            public readonly int Stage;
            public readonly ItemType[] Weapons;
            public readonly ItemType Accessory;
            public BossReward(int stage, ItemType[] weapons, ItemType accessory) { Stage = stage; Weapons = weapons; Accessory = accessory; }
        }
        static readonly int[,] ArmorDefense = { {10,9,8,8}, {18,16,14,14}, {28,25,22,22}, {40,36,32,32}, {56,50,44,44}, {74,66,58,58} };
        static readonly float[] AttackBudget = { .08f, .12f, .16f, .20f, .24f, .30f };
        static readonly float[] HitBudget = { 17, 29, 46, 69, 102, 151 };
        static readonly int[] Minions = { 2,3,3,4,4,5 };
        static readonly Dictionary<ArmorSet, (int Stage, Kit Class)> setBudgets = new Dictionary<ArmorSet, (int, Kit)>();

        internal static int ClassIndex(DamageClass c) => c == DamageClass.Melee ? 0 : c == DamageClass.Ranged ? 1 : c == DamageClass.Magic ? 2 : c == DamageClass.Summon ? 3 : -1;
        internal static int ClassIndex(Kit c) => c == Kit.Melee ? 0 : c == Kit.Ranged ? 1 : c == Kit.Magic ? 2 : c == Kit.Summon ? 3 : -1;
        internal static bool EliteReward(string key) => key == "BigBuzzer" || key == "MageKnight" || key == "PaleLurker";
        internal static bool FormalBoss(string key) => key != "Zote" && !EliteReward(key) && BossRewards.ContainsKey(key);

        internal static int AbilityStage(Func<string, bool> has)
        {
            int stage = 0;
            foreach (var flag in new[] {"hasDash","hasWalljump","hasSuperDash","hasDoubleJump","hasShadowDash"})
            { if (!has(flag)) break; stage++; }
            return stage;
        }
        internal static bool EntryClear
        {
            get
            {
                if (Bestiary.KillsOf("InfectedKnight") > 0) return true;
                foreach (var b in BossRewards)
                    if (b.Value.Stage == 4 && FormalBoss(b.Key) && Bestiary.KillsOf(b.Key) > 0) return true;
                return false;
            }
        }
        internal static bool FinalClear => Bestiary.KillsOf("FinalBoss") > 0;
        internal static int ExtraGate(ItemType t)
        {
            if (t == ItemType.LunarOre || t == ItemType.LunarBar) return 2;
            switch (t)
            {
                case ItemType.MythrilOre: case ItemType.MythrilBar: case ItemType.OrichalcumOre: case ItemType.OrichalcumBar:
                case ItemType.AdamantiteOre: case ItemType.AdamantiteBar: case ItemType.TitaniumOre: case ItemType.TitaniumBar:
                case ItemType.ChlorophyteOre: case ItemType.ChlorophyteBar: return 1;
                default:
                    var d=ItemDb.Get(t);
                    if(d.Set==ArmorSet.Titanium || d.Set==ArmorSet.Chlorophyte || d.Set==ArmorSet.Shroomite) return 1;
                    if(d.Set==ArmorSet.Solar || d.Set==ArmorSet.Vortex || d.Set==ArmorSet.Nebula || d.Set==ArmorSet.Stardust) return 2;
                    switch(t)
                    {
                        case ItemType.Meowmere: case ItemType.StarWrath: case ItemType.Terrarian: case ItemType.SDMG:
                        case ItemType.LastPrism: case ItemType.LunarFlareBook: case ItemType.StardustDragonStaff:
                        case ItemType.StardustCellStaff: case ItemType.LunarPortalStaff: case ItemType.RainbowCrystalStaff: return 2;
                    }
                    return 0;
            }
        }
        internal static bool Unlocked(ItemType t, int stage, bool entry, bool final)
        {
            var d = ItemDb.Get(t);
            return d != null && d.Stage <= stage && (ExtraGate(t) == 0 || (ExtraGate(t) == 1 ? entry : final));
        }
        internal static bool Unlocked(ItemType t) => Shop.Free || Unlocked(t, Shop.UnlockedStage(), EntryClear, FinalClear);

        static void DamageBonus(ref Bonuses b, Kit cls, float value)
        {
            b.MeleeDamage = b.RangedDamage = b.MagicDamage = b.MinionDamage = 0;
            b.MeleeCrit = b.RangedCrit = b.MagicCrit = b.AllCrit = 0;
            b.MeleeSpeed = b.ArrowDamage = 0;
            if (cls == Kit.Melee) b.MeleeDamage = value;
            else if (cls == Kit.Ranged) b.RangedDamage = value;
            else if (cls == Kit.Magic) b.MagicDamage = value;
            else if (cls == Kit.Summon) b.MinionDamage = value;
        }
        internal static Bonuses SetBonus(ArmorSet set, Bonuses b)
        {
            // Defence is allocated to the actual three pieces, including common starter armour.
            b.Defense = 0;
            if (setBudgets.TryGetValue(set, out var budget))
            {
                DamageBonus(ref b, budget.Class, AttackBudget[budget.Stage] * .4f);
                b.MaxMinions = 0;
            }
            if (set == ArmorSet.Bee) return new Bonuses { MinionDamage = .06f, MaxMinions = 1 };
            return b;
        }

        internal static void ApplyItems(ItemDef[] items)
        {
            foreach (var d in items)
                if (d != null && d.IsAccessory) OriginalAccessories[d.Type] = (d.Bonus, d.Defense, d.Tooltip, d.Damage);
            var tiers = new Dictionary<ItemType, int>(ResourceStages);
            for (int s = 0; s < Core.Length; s++)
                foreach (var cls in Core[s]) foreach (var t in cls) tiers[t] = s;
            for (int s = 0; s < AccessoryPaths.Length; s++)
                foreach (var cls in AccessoryPaths[s]) foreach (var t in cls)
                    if (!tiers.TryGetValue(t, out int old) || s < old) tiers[t] = s;
            var armourTiers = new Dictionary<ItemType, int>();
            foreach (var p in ArmorPaths) foreach (var t in p.Items)
                if (!armourTiers.TryGetValue(t, out int old) || p.Stage < old) armourTiers[t] = p.Stage;
            foreach (var p in armourTiers) tiers[p.Key] = p.Value;
            tiers[ItemType.PygmyNecklace] = 0;
            tiers[ItemType.IronBow] = tiers[ItemType.FlintlockPistol] = 0;
            tiers[ItemType.FeralClaws] = tiers[ItemType.PoisonBarb] = tiers[ItemType.BandofStarpower] = 0;
            tiers[ItemType.Gel] = tiers[ItemType.Feather] = tiers[ItemType.FallenStar] = tiers[ItemType.Amethyst] = 0;
            tiers[ItemType.WarriorEmblem] = tiers[ItemType.RangerEmblem] = tiers[ItemType.SorcererEmblem] = tiers[ItemType.SummonerEmblem] = 3;
            tiers[ItemType.MagicQuiver] = tiers[ItemType.MagicCuffs] = 2;
            tiers[ItemType.Hellforge] = 3;
            tiers[ItemType.MythrilAnvil] = tiers[ItemType.AdamantiteForge] = 4;
            // Actual world/merchant sources, rather than the old generic material tier.
            tiers[ItemType.MetalDetector] = 2;
            tiers[ItemType.ArgonMoss] = 2;
            tiers[ItemType.VioletMoss] = 3;
            tiers[ItemType.RainbowMoss] = 4;
            tiers[ItemType.Honeyfin] = 4;
            // Hive Knight and its nearby hive enemies are reached at stage 3. Wax must be
            // obtainable there before stage 4, including for its locally sourced rematch seal.
            tiers[ItemType.BeeWax] = 3;
            tiers[ItemType.FishermansGuide] = tiers[ItemType.WeatherRadio] = tiers[ItemType.Sextant] = 2;
            tiers[ItemType.AnglerHat] = tiers[ItemType.AnglerVest] = tiers[ItemType.AnglerPants] = 2;
            foreach (var d in items)
            {
                if (d == null || d.Type == ItemType.None) continue;
                if (tiers.TryGetValue(d.Type, out int stage)) d.Stage = stage;
                if (d.Stage < 0 || d.Stage > 5) continue;
                if (d.IsWeapon && !d.IsAccessory && !d.GameTool && !d.IsAmmo && d.PickPower == 0 && d.AxePower == 0 && d.HammerPower == 0 && !d.HkItem)
                    TuneWeapon(d);
                // Accessories retain their original stats; no stage-based caps.
                if (d.IsArmor)
                {
                    int cls = Math.Max(0, ClassIndex(d.Kit));
                    float share = d.Armor == ArmorSlot.Body ? .4f : .3f;
                    d.Defense = Mathf.Max(1, Mathf.RoundToInt(ArmorDefense[d.Stage,cls] * share));
                    d.Bonus.Defense = 0;
                    DamageBonus(ref d.Bonus, d.Kit, AttackBudget[d.Stage] * .2f);
                    d.Bonus.MaxMinions = 0;
                }
            }
            // Shared bodies and legs keep one fixed defence; the head carries the branch difference.
            var allocated = new HashSet<ItemType>();
            foreach (var p in ArmorPaths)
            {
                var head = items[(int)p.Items[0]];
                var body = items[(int)p.Items[1]];
                var legs = items[(int)p.Items[2]];
                int total = ArmorDefense[p.Stage,ClassIndex(p.Class)];
                bool mixed = p.Stage == 1 && p.Class == Kit.Summon;
                if (mixed) body.Defense = total - head.Defense - legs.Defense;
                else
                {
                    if (allocated.Add(body.Type)) body.Defense = Mathf.RoundToInt(total * .4f);
                    if (allocated.Add(legs.Type)) legs.Defense = Mathf.RoundToInt(total * .3f);
                    if (allocated.Add(head.Type)) head.Defense = total - body.Defense - legs.Defense;
                }
                foreach (var d in new[] {head,body,legs})
                {
                    // Common wood and metal pieces remain general, not class-locked.
                    if (p.Stage > 0 && d.Stage == p.Stage) DamageBonus(ref d.Bonus, p.Class, AttackBudget[p.Stage] * .2f);
                    if (d.Set != ArmorSet.None && p.Stage > 0 && d.Stage == p.Stage)
                        setBudgets[d.Set] = (p.Stage,p.Class);
                    if (d.SetBonus != ArmorSet.None && p.Stage > 0 && d.Stage == p.Stage)
                        setBudgets[d.SetBonus] = (p.Stage,p.Class);
                    // Keep the original acquisition method (some sets are drops or purchases).
                }
            }
            // Hallowed bodies/legs are shared by ranger and caster heads. Give both branches
            // the same piece budget instead of letting the last path overwrite the first.
            var roles=new Dictionary<ItemType,HashSet<Kit>>();
            foreach(var p in ArmorPaths) foreach(var t in p.Items)
            {
                if(items[(int)t].Stage!=p.Stage || p.Stage==0) continue;
                if(!roles.TryGetValue(t,out var kits)) roles[t]=kits=new HashSet<Kit>();
                kits.Add(p.Class);
            }
            foreach(var pair in roles)
            {
                if(pair.Value.Count<2) continue;
                var d=items[(int)pair.Key];
                float amount=AttackBudget[d.Stage]*.2f;
                DamageBonus(ref d.Bonus,Kit.General,0);
                foreach(var cls in pair.Value)
                {
                    if(cls==Kit.Melee) d.Bonus.MeleeDamage=amount;
                    if(cls==Kit.Ranged) d.Bonus.RangedDamage=amount;
                    if(cls==Kit.Magic) d.Bonus.MagicDamage=amount;
                    if(cls==Kit.Summon) d.Bonus.MinionDamage=amount;
                }
            }
            // The Flinx mixed outfit has no complete-set damage bonus.
            items[(int)ItemType.FlinxFurCoat].Bonus = new Bonuses { MinionDamage = .12f, MaxMinions = 1 };
            items[(int)ItemType.ObsidianOutlawHat].Bonus.MaxMinions = 1;
            items[(int)ItemType.SpiderMask].Bonus.MaxMinions = 1;
            items[(int)ItemType.SpiderBreastplate].Bonus.MaxMinions = 1;
            items[(int)ItemType.SpookyHelmet].Bonus.MaxMinions = 1;
            items[(int)ItemType.SpookyBreastplate].Bonus.MaxMinions = 1;
            items[(int)ItemType.SpookyLeggings].Bonus.MaxMinions = 1;
            foreach (var t in new[] {ItemType.BeeHeadgear,ItemType.BeeBreastplate,ItemType.BeeGreaves})
            {
                var d = items[(int)t];
                d.Stage = 4;
                d.Bonus = new Bonuses { MinionDamage = .06f, MaxMinions = t == ItemType.BeeGreaves ? 0 : 1, MoveSpeed = t == ItemType.BeeGreaves ? .05f : 0 };
                d.Tooltip = "召唤伤害提高6%" + (t == ItemType.BeeGreaves ? "\n移动速度提高5%" : "\n仆从数量上限增加1");
            }
            foreach (var t in new[] {ItemType.WoodenSword,ItemType.WoodenBow,ItemType.WandofSparking,ItemType.FinchStaff})
            { items[(int)t].Value = 3000; items[(int)t].Price = 30; }
            foreach (var t in new[] {ItemType.WoodenArrow,ItemType.Seed,ItemType.MusketBall})
            { items[(int)t].Stage = 0; items[(int)t].Value = 5; }
            items[(int)ItemType.LesserManaPotion].Value = 100;
            foreach (var d in items)
                if (d != null && d.IsArmor) d.Tooltip = BonusTooltip(d);
        }

        internal static readonly Dictionary<ItemType, (Bonuses Bonus, int Defense, string Tooltip, int Damage)> OriginalAccessories
            = new Dictionary<ItemType, (Bonuses, int, string, int)>();

        static void TuneWeapon(ItemDef d)
        {
            int s = d.Stage;
            float dps = HitBudget[s] * 2;
            float defense = World.Areas.Defense(s) * .5f;
            float seconds = Mathf.Max(1f/60f, d.Class == DamageClass.Melee && d.Shoot == ProjKind.None ? d.UseAnimation : d.UseTime);
            int projectiles = Mathf.Max(1,d.Pellets);
            switch (d.Type)
            {
                case ItemType.Tsunami: projectiles = 5; break;
                case ItemType.AerialBane: projectiles = 6; break;
                case ItemType.QuadBarrelShotgun: projectiles = 8; break;
                case ItemType.DaedalusStormbow: projectiles = 3; break;
                case ItemType.ClockworkAssaultRifle: projectiles = 3; break;
                case ItemType.PoisonStaff: projectiles = 3; break;
                case ItemType.VenomStaff: projectiles = 7; break;
                case ItemType.SkyFracture: projectiles = 3; break;
                case ItemType.BlizzardStaff: projectiles = 4; break;
                case ItemType.BatScepter: projectiles = 3; break;
            }
            float target;
            if (d.IsSummon) target = dps * .65f / (Minions[s] * 2f) + defense;
            else if (d.Sentry) target = dps * .25f / 2f + defense;
            else if (d.IsWhip) target = dps * .25f * seconds + defense;
            else if (d.Channel) target = HitBudget[s] * .7f + defense; // Held projectiles have their own hit cadence.
            else target = dps * seconds / projectiles + defense;
            if (d.Class == DamageClass.Ranged && d.UseAmmo != AmmoKind.None)
                target -= s <= 1 ? 4 : s <= 3 ? 8 : 12;
            if (d.Class == DamageClass.Melee && d.Shoot == ProjKind.None) target *= 1.12f;
            // Bee child shots are part of the same budget as the primary hit.
            if (d.Type == ItemType.BeeKeeper) target *= .42f;
            if (d.Type == ItemType.BeesKnees) target *= 1.4f;
            if (d.Type == ItemType.BladeofGrass) target *= .8f;
            if (d.Type == ItemType.Muramasa) target *= .65f;
            if (d.Type == ItemType.FieryGreatsword) target *= .7f;
            if (d.Type == ItemType.TerraBlade) target *= .8f;
            if (d.Type == ItemType.GoldBow) target *= 1.2f;
            if (d.Type == ItemType.ClockworkAssaultRifle) target *= 1.35f;
            if (d.Type == ItemType.Tsunami) target *= 1.15f;
            if (d.Type == ItemType.RazorbladeTyphoon) target *= .44f;
            if (d.Type == ItemType.AquaScepter) target *= 1.2f;
            if (s==0 && d.IsSummon) target *= 1.8f;
            if (s==5 && d.IsSummon) target *= 1.4f;
            d.Damage = Mathf.Max(3,Mathf.RoundToInt(target));
            if (d.Class == DamageClass.Magic && !d.NoUseMana)
                d.Mana = s == 0 ? 2 : Mathf.Clamp(Mathf.RoundToInt(seconds*(3+s*2)),1,3+s);
        }
        static string BonusTooltip(ItemDef d)
        {
            var lines = new List<string>();
            // Keep descriptions of behaviour; numeric lines are regenerated from the actual bonuses.
            foreach (var line in (d.Tooltip ?? "").Split('\n'))
                if (!string.IsNullOrWhiteSpace(line) && (!System.Text.RegularExpressions.Regex.IsMatch(line, @"\d") || line.Contains("每") || line.Contains("第"))) lines.Add(line);
            var b = d.Bonus;
            void Percent(string label, float n) { if (Mathf.Abs(n) > .0001f) lines.Add(label + (n*100).ToString("0.#") + "%"); }
            Percent("近战伤害提高",b.MeleeDamage); Percent("远程伤害提高",b.RangedDamage);
            Percent("魔法伤害提高",b.MagicDamage); Percent("召唤伤害提高",b.MinionDamage);
            Percent("近战速度提高",b.MeleeSpeed); Percent("全职业暴击提高",b.AllCrit);
            Percent("近战暴击提高",b.MeleeCrit); Percent("远程暴击提高",b.RangedCrit); Percent("魔法暴击提高",b.MagicCrit);
            Percent("魔力消耗降低",b.ManaCost); Percent("不消耗弹药几率",b.AmmoSave);
            Percent("伤害减免",b.Endurance); Percent("移动速度提高",b.MoveSpeed);
            if (b.ManaMax != 0) lines.Add("魔力上限增加" + b.ManaMax);
            if (b.MaxMinions != 0) lines.Add("仆从数量上限增加" + b.MaxMinions);
            if (b.MaxTurrets != 0) lines.Add("哨兵数量上限增加" + b.MaxTurrets);
            if (b.LifeRegen != 0) lines.Add("生命再生提高" + b.LifeRegen);
            if(Upgrades.ContainsKey(d.Type)) lines.Add("与本系列升级品同时装备时，仅生效升级品");
            return string.Join("\n",lines);
        }
        internal static string SetDescription(ArmorSet set,string original) => BonusTooltip(new ItemDef { Bonus=Equipment.SetBonus(set),Tooltip=original });
        internal static int TagDamage(WhipKind whip,int original)
        {
            if(original==0) return 0;
            foreach(var row in Core) foreach(var cls in row) foreach(var t in cls)
            {
                var d=ItemDb.Get(t);
                if(d.Whip==whip) return Math.Min(original,new[] {2,3,4,6,8,12}[d.Stage]);
            }
            return Math.Min(original,12);
        }
        internal static int WhipProcDamage => new[] {3,5,8,12,18,26}[Mathf.Clamp(ItemDb.Get(ItemType.Durendal).Stage,0,5)];
        internal static int ProcDamage(ItemType source,float share,float seconds=1,int hits=1)
        {
            int stage=Mathf.Clamp(ItemDb.Get(source).Stage,0,5);
            return Mathf.Max(1,Mathf.RoundToInt(HitBudget[stage]*2*share*seconds/Math.Max(1,hits)+World.Areas.Defense(stage)*.5f));
        }
        internal static int EquippedProcDamage(Func<Bonuses,bool> matches,float share,int hits=1)
        {
            ItemType source=ItemType.None;
            int stage=-1;
            var inv=TerraMod.Instance?.Inventory;
            if(inv!=null) foreach(var stack in inv.EffectiveAccessories)
                if(!stack.IsEmpty && matches(stack.Def.Bonus) && stack.Def.Stage>stage) {source=stack.Type;stage=stack.Def.Stage;}
            return source==ItemType.None ? 1 : ProcDamage(source,share,1,hits);
        }
        internal static ItemType[] FunctionPool(int stage)
        {
            var paths = AccessoryPaths[Mathf.Clamp(stage,0,5)];
            return new[] {paths[0][0],paths[1][0],paths[2][0],paths[3][0]};
        }
        internal static IEnumerable<ItemType> ArmorFor(int stage, int cls)
        {
            foreach (var p in ArmorPaths)
                if (p.Stage == stage && ClassIndex(p.Class) == cls)
                    foreach (var t in p.Items) yield return t;
        }
    }
}
