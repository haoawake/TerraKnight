using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ragdata", for the AstrBot knowledge base in kb-export/): what the item table, the armor sets, the bosses'
    /// vanilla nodes and reward pools, their fights' numbers and the summoning items are in this build, written as
    /// rag-details.json beside the saves. Reads only; the isolated copy's saves are never written.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>An item's English name: Terraria's own for its vanilla items, the mod's translation (Lang) for its own; null when neither has one.</summary>
        static string EnglishName(ItemDef d)
        {
            if (d.Texture != null && d.Texture.StartsWith("Item_") && int.TryParse(d.Texture.Substring(5), out int id))
            {
                var vanilla = Terraria.TerrariaText.ItemName("en-US", id);
                if (!string.IsNullOrEmpty(vanilla) && Terraria.TerrariaText.ItemName("zh-Hans", id) == d.Name) return vanilla;
            }
            return Lang.Lookup(d.Name, "en-US");
        }

        void RagExport()
        {
            if (!DevCopy.Verified) throw new Exception("RAG export requires isolated saves");
            var defs = new Dictionary<string, object>();
            foreach (var d in ItemDb.All)
                if (d.Type != ItemType.None)
                    defs[d.Type.ToString()] = new
                    {
                        d.Name, d.Tooltip, d.Bonus, Armor = d.Armor.ToString(), Set = d.Set.ToString(), SetBonus = d.SetBonus.ToString(), d.IsWeapon, d.IsArmor,
                        d.Knockback, d.Crit, d.HammerPower, d.AxePower, d.FishingPower, d.BaitPower, d.Consumable, d.AutoReuse, d.Scale, d.LifeCrystal, d.ManaCrystal,
                        d.Rare, d.IsSummon, d.IsWhip, d.Sentry, Measured = WeaponCalibration.Measured(d.Type), ActualBuyPrice = Shop.PriceOf(d),
                        En = EnglishName(d),
                    };
            var sets = new Dictionary<string, object>();
            foreach (ArmorSet s in Enum.GetValues(typeof(ArmorSet))) sets[s.ToString()] = new { Text = Equipment.SetBonusText(s), Bonus = Equipment.SetBonus(s) };
            var names = new Dictionary<string, string>();
            foreach (string k in EnemyDrops.DevRecords) names[k] = Bestiary.NameOf(k);
            var bosses = new Dictionary<string, object>();
            foreach (var pair in VanillaBossProgression.All)
            {
                var p = pair.Value;
                bool godhome = VanillaBossProgression.GodhomeFights.TryGetValue(pair.Key, out int fight);
                VanillaBossProgression.Combat.TryGetValue(pair.Key, out var c);
                bosses[pair.Key] = new
                {
                    Name = Bestiary.NameOf(pair.Key), p.Band, Step = p.Step.ToString(), p.Role, p.Upgrade, Signature = p.Signature.ToString(),
                    Gear = (p.Gear ?? new ItemType[0]).Select(t => t.ToString()).ToArray(),
                    Materials = (p.Materials ?? new VanillaBossProgression.Material[0]).Select(m => new { Type = m.Type.ToString(), m.Min, m.Max, m.Group }).ToArray(),
                    // A Godhome-only god: its stage-5 Godhome fight, its defense and damage Godhome's scaling (null here).
                    MasterLife = godhome ? fight : c.Life, Defense = godhome ? (int?)null : c.Defense, DamageFactor = godhome ? (float?)null : c.Damage,
                    GodhomeOnly = godhome,
                };
            }
            var seals = BossSummons.All.Select(s => new
            {
                Type = s.Type.ToString(), Boss = s.Key, s.Name, s.En, s.Stage, Material = s.Material.ToString(), s.Amount, s.MaterialSource,
                Support = s.SupportMaterial.ToString(), s.SupportAmount, s.SupportSource, s.Locality, s.Dream,
            }).ToArray();
            // The stage paths the balance is built on (ProgressionBalance): each stage's four classes' core weapons, armor
            // and accessories, and each formal boss's stage, weapons and accessory.
            var paths = new
            {
                Core = ProgressionBalance.Core.Select(s => s.Select(c => c.Select(t => t.ToString()).ToArray()).ToArray()).ToArray(),
                Accessories = ProgressionBalance.AccessoryPaths.Select(s => s.Select(c => c.Select(t => t.ToString()).ToArray()).ToArray()).ToArray(),
                Armor = ProgressionBalance.ArmorPaths.Select(p => new { p.Stage, Class = p.Class.ToString(), Items = p.Items.Select(t => t.ToString()).ToArray() }).ToArray(),
                Rewards = ProgressionBalance.BossRewards.ToDictionary(p => p.Key, p => (object)new
                {
                    p.Value.Stage, Weapons = p.Value.Weapons.Select(t => t.ToString()).ToArray(), Accessory = p.Value.Accessory.ToString(),
                    Formal = ProgressionBalance.FormalBoss(p.Key), Name = Bestiary.NameOf(p.Key),
                }),
            };
            var rules = new
            {
                Areas.BossDamage, Areas.EliteDamage, ForgeOdds = Forging.Odds, ForgeCaps = Enumerable.Range(0, 6).Select(Forging.StageCap).ToArray(),
                MasteryPerBoss = BossMastery.PerBoss,
                GodhomeShares = BossHealthBudgets.GodhomeStageShares,
                Godhome = GodhomeBalance.Hall.Concat(GodhomeBalance.Pantheons).Select(p => new { p.Key, p.Name, p.Life, p.Defense, p.FlatDefense, p.Damage }).ToArray(),
            };
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "rag-details.json"),
                JsonConvert.SerializeObject(new { items = defs, sets, names, bosses, seals, paths, rules }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 知识库导出 {defs.Count} 件物品，{bosses.Count} 个首领，{seals.Length} 个召唤物");
        }
    }
}
