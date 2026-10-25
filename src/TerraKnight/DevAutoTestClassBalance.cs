using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "classbalance"): research export for the user's 2026-10-21 review ("每个阶段既不会几下就杀boss，也不会一两下就
    /// 被秒…四职业都差不多强"). For every difficulty, stage and class: the stage's reference kit (ProgressionBalance's armor and
    /// accessory paths, newest first, as many as the slots hold) worn for real so Equipment.Update gives its numbers, the first
    /// kills of the bosses before the stage (and half of its own) as BossMastery adds them, a typical life total; each core
    /// weapon's damage against each of the stage's bosses (their defense, the weapon's cadence by WeaponBalance.OutputOf, crits),
    /// the boss's hit after defense and endurance. Nothing is saved: the kit is put back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The life a player of each stage is taken to have: crystals, extra masks' shards, fruits.</summary>
        static readonly int[] AssumedLife = { 140, 240, 360, 480, 590, 660 };

        IEnumerator ClassBalanceAudit()
        {
            yield return GoTo("Town", "left1");
            var inv = mod.Inventory;
            var keepArmor = (ItemStack[])inv.Armor.Clone();
            var keepAcc = (ItemStack[])inv.Accessories.Clone();
            int keepStage = Shop.DevStage;
            var keepDiff = GameDifficulty.Current;
            var rows = new List<object>();
            var bossRows = new List<object>();
            var tagField = typeof(SummonTags).GetField("effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var tagEffects = tagField?.GetValue(null) as System.Collections.IDictionary;
            try
            {
                foreach (var diff in new[] { DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Expert, DifficultyLevel.Master })
                {
                    GameDifficulty.Current = diff;
                    for (int stage = 0; stage <= 5; stage++)
                    {
                        Shop.DevStage = stage;
                        // The stage's bosses, as the world has them.
                        var bosses = new List<(string Key, int Life, int Defense, int Hit)>();
                        foreach (var kv in BossStats.DevAll)
                        {
                            var r = kv.Value;
                            if (!ProgressionBalance.BossRewards.TryGetValue(kv.Key, out var reward) || reward.Stage != stage || !ProgressionBalance.FormalBoss(kv.Key)) continue;
                            string scene = BestiaryHabitats.WorldScene(kv.Key) ?? BossArena.SceneOf(kv.Key) ?? EnemyDrops.SceneOf(kv.Key);
                            if (scene != null && Areas.GodhomeRoom(scene)) continue;
                            var tune = BossBalance.Of(r.Record, EnemyDrops.VariantLevel(scene));
                            int life = Mathf.RoundToInt(BossStats.LifeOf(r, scene, diff) * tune.Life * BossPhases.WholeFactor(r.Record, scene));
                            int def = EnemyStats.BossDefense(r, tune, scene);
                            int level = scene != null ? Areas.FightLevel(scene) : stage;
                            float perMask = Areas.MasterDamagePerMask(Areas.StatLevel(level, stage)) * Areas.CatchUp(level, stage) * GameDifficulty.FactorOf(diff);
                            int hit = Mathf.Max(1, Mathf.RoundToInt(perMask * Areas.BossDamage * tune.Damage));
                            bosses.Add((kv.Key, life, def, hit));
                            if (diff == DifficultyLevel.Master || diff == DifficultyLevel.Normal)
                                bossRows.Add(new { Difficulty = diff.ToString(), Stage = stage, Key = kv.Key, Scene = scene, Life = life, Defense = def, Hit = hit, TuneLife = tune.Life, TuneDamage = tune.Damage });
                        }
                        if (bosses.Count == 0) continue;
                        // Bosses beaten before (all of the earlier stages', half of this one's).
                        int before = ProgressionBalance.BossRewards.Count(b => ProgressionBalance.FormalBoss(b.Key) && b.Value.Stage < stage);
                        int own = ProgressionBalance.BossRewards.Count(b => ProgressionBalance.FormalBoss(b.Key) && b.Value.Stage == stage);
                        int mastered = before + own / 2;
                        for (int cls = 0; cls < 4; cls++)
                        {
                            // The kit: the stage's armor; the accessories of its path, the newest first.
                            for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = default;
                            for (int i = 0; i < inv.Accessories.Length; i++) inv.Accessories[i] = default;
                            var armor = ProgressionBalance.ArmorFor(stage, cls).ToArray();
                            foreach (var t in armor)
                            {
                                var d = ItemDb.Get(t);
                                int slot = d.Armor == ArmorSlot.Head ? 0 : d.Armor == ArmorSlot.Body ? 1 : 2;
                                inv.Armor[slot] = ItemStack.Of(t);
                            }
                            var accs = new List<ItemType>();
                            for (int s = stage; s >= 0 && accs.Count < Inventory.OpenAccessories; s--)
                                foreach (var t in ProgressionBalance.AccessoryPaths[s][cls])
                                    if (!accs.Contains(t) && accs.Count < Inventory.OpenAccessories) accs.Add(t);
                            for (int i = 0; i < accs.Count; i++) inv.Accessories[i] = ItemStack.Of(accs[i]);
                            Equipment.Update(inv);
                            var b = Equipment.Current;
                            // The test save's own first kills out, the stage's in.
                            float mastery = mastered * BossMastery.PerBoss - BossMastery.Bonus;
                            float endurance = b.Endurance + mastery;
                            int defense = b.Defense;
                            int life = AssumedLife[stage];
                            var weapons = new List<object>();
                            // Each core weapon's numbers; its damage against a defense worked out per boss below.
                            var kit = new List<(string Name, int Damage, float Unit, float Hits, float Crit, int Pen, bool Minion, bool Whip, int Tag)>();
                            foreach (var t in ProgressionBalance.Core[stage][cls])
                            {
                                var d = ItemDb.Get(t);
                                if (d == null || !d.IsWeapon) continue;
                                ItemDef ammo = null;
                                if (d.UseAmmo != AmmoKind.None)
                                    foreach (var a in ItemDb.All)
                                        if (a.Ammo == d.UseAmmo && a.Stage <= stage && a.Damage >= 0 && (ammo == null || a.Damage > ammo.Damage)) ammo = a;
                                int dmg = Equipment.Damage(d, ammo);
                                // The stage's first kills add to every class's damage.
                                float classDamage = d.Class == DamageClass.Melee ? b.MeleeDamage : d.Class == DamageClass.Ranged ? b.RangedDamage : d.Class == DamageClass.Magic ? b.MagicDamage : b.MinionDamage;
                                dmg = Mathf.RoundToInt(dmg * (1f + classDamage + mastery) / Mathf.Max(0.01f, 1f + classDamage));
                                float crit = Mathf.Clamp01(Equipment.Crit(d) / 100f);
                                var p = ProgressionBalance.OutputOf(d);
                                float unit = p.Volley > 0 ? p.Units / p.Volley : 1f;
                                float hits = p.Hits;
                                bool minion = d.IsSummon && !d.Sentry;
                                if (minion && d.Minion != MinionKind.StardustDragon)
                                {
                                    // The cadence counts the stage's usual slots; the kit's own count instead.
                                    int slots = 1 + b.MaxMinions;
                                    int assumed = new[] { 2, 3, 3, 4, 4, 5 }[Mathf.Clamp(d.Stage, 0, 5)];
                                    hits *= slots / (float)assumed;
                                }
                                int pen = d.Bonus.ArmorPen + b.ArmorPen;
                                int tag = 0;
                                if (d.IsWhip && tagEffects != null && tagEffects.Contains(d.Whip))
                                {
                                    var e = tagEffects[d.Whip];
                                    int raw = (int)e.GetType().GetField("TagDamage").GetValue(e);
                                    tag = ProgressionBalance.TagDamage(d.Whip, raw);
                                }
                                kit.Add((d.Name, dmg, unit, hits, crit, pen, minion, d.IsWhip, tag));
                                weapons.Add(new { Item = t.ToString(), d.Name, Damage = dmg, Ammo = ammo?.Type.ToString(), Crit = crit, Hits = hits, Unit = unit, Pen = pen, Tag = tag, Minion = minion, Whip = d.IsWhip, d.Sentry });
                            }
                            float Dps((string Name, int Damage, float Unit, float Hits, float Crit, int Pen, bool Minion, bool Whip, int Tag) w, int def, int tagOn = 0)
                                => w.Hits * Mathf.Max(1f, w.Damage * w.Unit + tagOn - Mathf.Max(0, def - w.Pen) * 0.5f) * (1f + w.Crit);
                            var fights = new List<object>();
                            float sumDps = 0f;
                            string bestName = "";
                            foreach (var boss in bosses)
                            {
                                float best = 0f;
                                string name = "";
                                foreach (var w in kit)
                                {
                                    float dps = Dps(w, boss.Defense);
                                    if (dps > best) { best = dps; name = w.Name; }
                                }
                                // A summoner fights with its minions and a whip at once: half the time swinging, the tag on every minion hit.
                                if (cls == 3)
                                    foreach (var m in kit.Where(x => x.Minion))
                                        foreach (var w in kit.Where(x => x.Whip))
                                        {
                                            float both = Dps(m, boss.Defense, w.Tag) + Dps(w, boss.Defense) * 0.5f;
                                            if (both > best) { best = both; name = m.Name + "+" + w.Name; }
                                        }
                                sumDps += best;
                                bestName = name;
                                float eff = GameDifficulty.DefenseEffectiveness;
                                int taken = Mathf.Max(1, (int)((1f - Mathf.Clamp(endurance, 0f, 0.9f)) * Mathf.Max(1f, boss.Hit - defense * eff)));
                                fights.Add(new { boss.Key, boss.Life, boss.Defense, boss.Hit, Dps = best, Weapon = name, Ttk = boss.Life / Mathf.Max(1f, best), Taken = taken, HitsToDie = Mathf.CeilToInt(life / (float)taken) });
                            }
                            float bestDps = sumDps / Mathf.Max(1, bosses.Count);
                            rows.Add(new
                            {
                                Difficulty = diff.ToString(), Stage = stage, Class = cls, Armor = armor.Select(x => x.ToString()).ToArray(), Accessories = accs.Select(x => x.ToString()).ToArray(),
                                Defense = defense, Endurance = endurance, Life = life, Mastered = mastered, MeleeDamage = b.MeleeDamage + mastery, RangedDamage = b.RangedDamage + mastery,
                                MagicDamage = b.MagicDamage + mastery, MinionDamage = b.MinionDamage + mastery, Slots = 1 + b.MaxMinions, BestDps = bestDps, Best = bestName,
                                Weapons = weapons, Fights = fights,
                            });
                        }
                        yield return null;
                    }
                }
                string path = Path.Combine(outDir, "classbalance.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(new { Rows = rows, Bosses = bossRows }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 职业平衡 导出 {rows.Count} 行 → {path}");
            }
            finally
            {
                for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = keepArmor[i];
                for (int i = 0; i < inv.Accessories.Length; i++) inv.Accessories[i] = keepAcc[i];
                Equipment.Update(inv);
                Shop.DevStage = keepStage;
                GameDifficulty.Current = keepDiff;
            }
        }

        static float Median(IEnumerable<float> xs)
        {
            var a = xs.OrderBy(x => x).ToArray();
            if (a.Length == 0) return 0f;
            return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) * 0.5f;
        }
    }
}
