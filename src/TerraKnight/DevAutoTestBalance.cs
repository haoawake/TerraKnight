using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "balance"): the numbers a fight comes to, stage by stage and difficulty by
    /// difficulty, from the game's own data and formulas. Enemies: the roster's own health times
    /// Areas.HealthScale, the stage's defense, a mask's damage (elites x1.5, bosses x2). The
    /// player: the weapons on offer by then (ItemDef.Stage) at their volley/continuous hit rate against that
    /// defense (StrikeNPC: damage less half the defense; crit chance on top), the best armor by
    /// then, life from the masks and crystals a player has around then. Then live: King's Pass's
    /// crawlids and vengeflies fought with the starting weapons at each difficulty. Lines start
    /// "[平衡]" so the report can be built from them.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>A typical player's masks beyond the first five and life crystals by stage (assumed; the report says so).</summary>
        static readonly (int Masks, int Crystals, int Fruits)[] lifeByStage = { (0, 1, 0), (1, 3, 0), (2, 6, 0), (3, 10, 0), (4, 15, 4), (4, 15, 20) };

        static float RateOf(ItemDef d) => ProgressionBalance.OutputOf(d).Hits;

        /// <summary>Single-target output estimate including volleys, delayed bursts and sustained hit cadence.</summary>
        static float DpsOf(ItemDef d, int defense, int stage)
        {
            // A bow or a gun hits for its own damage and its ammo's: the best ammo of the kind by then.
            int ammo = d.UseAmmo == AmmoKind.None ? 0 : ItemDb.All.Where(a => a.Ammo == d.UseAmmo && a.Stage <= stage).Select(a => a.Damage).DefaultIfEmpty(0).Max();
            return ProgressionBalance.WeaponDps(d,defense,ammo);
        }

        IEnumerator BalanceTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            var saved = GameDifficulty.Current;
            int savedStage = Shop.DevStage;
            // The shared output model includes held weapons and autonomous summon attacks.
            var weapons = ItemDb.All.Where(ProgressionBalance.BalancedWeapon).ToList();
            var records = EnemyDrops.DevRecords.Where(r =>
            {
                var k = EnemyDrops.KindOf(r);
                return k != "non_enemy" && k != "player_shade" && k != "passive" && k != "invulnerable" && EnemyDrops.HpOf(r) > 0;
            }).ToList();
            var levels = new[] { DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Expert, DifficultyLevel.Master };
            string[] cats = { "小怪", "精英", "首领" };

            for (int s = 0; s <= 5; s++)
            {
                Shop.DevStage = s;
                int def = Areas.Defense(s);
                // The weapons by then: the best of each class, and the typical one (the 70th percentile of all of them).
                var have = weapons.Where(d => d.Stage <= s).OrderByDescending(d => DpsOf(d, def, s)).ToList();
                var best = new List<string>();
                foreach (var c in new[] { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic })
                {
                    var b = have.FirstOrDefault(d => d.Class == c);
                    if (b != null) best.Add($"{b.Name}（伤害 {b.Damage} 用时 {b.UseTime * 60f:F0}/{b.UseAnimation * 60f:F0} 帧{(b.UseAmmo != AmmoKind.None ? " 用弹药" : "")}）{DpsOf(b, def, s):F0}/秒");
                }
                float bestDps = have.Count > 0 ? DpsOf(have[0], def, s) : 1f;
                // Typical: the median of the weapons first on offer at this stage.
                var fresh = have.Where(d => d.Stage == s).Select(d => DpsOf(d, def, s)).OrderBy(x => x).ToList();
                float typicalDps = fresh.Count > 0 ? fresh[fresh.Count / 2] : have.Count > 0 ? DpsOf(have[have.Count / 2], def, s) : 1f;
                // The best armor by then.
                int armor = 0;
                foreach (var slot in new[] { ArmorSlot.Head, ArmorSlot.Body, ArmorSlot.Legs })
                    armor += ItemDb.All.Where(d => d.Armor == slot && d.Stage <= s).Select(d => d.Defense + d.Bonus.Defense).DefaultIfEmpty(0).Max();
                // Original metal/wood set defence is additional to the pieces. Compare
                // complete sets with the strongest mixed outfit instead of dropping it.
                var obtainableArmor=ItemDb.All.Where(d=>d.IsArmor && d.Stage<=s).ToArray();
                foreach(var head in obtainableArmor.Where(d=>d.Armor==ArmorSlot.Head && d.Set!=ArmorSet.None))
                foreach(var body in obtainableArmor.Where(d=>d.Armor==ArmorSlot.Body && d.Set==head.Set))
                foreach(var legs in obtainableArmor.Where(d=>d.Armor==ArmorSlot.Legs && d.Set==head.Set))
                {
                    var set=head.Set==ArmorSet.Beetle ? body.SetBonus : head.SetBonus!=ArmorSet.None ? head.SetBonus : head.Set;
                    armor=Mathf.Max(armor,head.Defense+head.Bonus.Defense+body.Defense+body.Bonus.Defense+legs.Defense+legs.Bonus.Defense+Equipment.SetBonus(set).Defense);
                }
                var (masks, crystals, fruits) = lifeByStage[s];
                int life = Mathf.Min(700, Player.TerraStats.BaseLife + crystals * 20 + fruits * 5 + masks * 4 * Player.TerraStats.LifePerShard);
                Plugin.Log.LogInfo($"[平衡] 阶段 {s}：敌人防御 {def}；可用武器 {have.Count} 把，最强 {string.Join("、", best)}，典型 {typicalDps:F0}/秒；最好的护甲合计防御 {armor}；假设生命 {life}（额外面具 {masks}、生命水晶 {crystals}、生命果 {fruits}）");
                foreach (var lv in levels)
                {
                    GameDifficulty.Current = lv;
                    var line = new System.Text.StringBuilder();
                    for (int c = 0; c < 3; c++)
                    {
                        var lives = records.Where(r => EnemyDrops.StageOf(r) == s && Bestiary.CategoryOf(r) == c)
                            .Select(r => Bestiary.StatsOf(r).Life).OrderBy(x => x).ToList();
                        if (lives.Count == 0) continue;
                        int median = lives[lives.Count / 2], max = lives[lives.Count - 1];
                        float factor = c == 2 ? Areas.BossDamage : c == 1 ? Areas.EliteDamage : 1f;
                        int hit = Mathf.RoundToInt(Areas.DamagePerMask(s) * factor);
                        int taken = Mathf.Max(1, Mathf.RoundToInt((Mathf.RoundToInt(Areas.MasterDamagePerMask(s) * factor) - armor / 2) * GameDifficulty.EnemyFactor));
                        line.Append($"{cats[c]} {lives.Count} 种 生命中位 {median}（最高 {max}）击杀 {median / bestDps:F1}秒/{median / typicalDps:F1}秒（最强/本阶段典型武器），一下伤害 {hit}→实受 {taken}，{Mathf.CeilToInt(life / (float)taken)} 下死；");
                    }
                    Plugin.Log.LogInfo($"[平衡] 阶段 {s} {GameDifficulty.NameOf(lv)}：{line}");
                }
            }

            // Each boss as its Terraria stand-in: life on each difficulty, defense, how long the best weapon by then takes on Master.
            foreach (var kv in BossStats.DevAll.OrderBy(k => k.Value.Stage))
            {
                var r = kv.Value;
                // Its own measure on top (BossBalance): the record is the key less any "@stage".
                string rec = kv.Key.Contains("@") ? kv.Key.Substring(0, kv.Key.IndexOf('@')) : kv.Key;
                var tune = BossBalance.Of(rec, kv.Key.Contains("@") ? int.Parse(kv.Key.Substring(kv.Key.IndexOf('@') + 1)) : r.Stage);
                Shop.DevStage = r.Stage;
                int defense = EnemyStats.BossDefense(r, tune);
                var have = weapons.Where(d => d.Stage <= r.Stage).OrderByDescending(d => DpsOf(d, defense, r.Stage)).ToList();
                float dps = have.Count > 0 ? DpsOf(have[0], defense, r.Stage) : 1f;
                string bossScene = EnemyDrops.SceneOf(kv.Key);
                var lives = levels.Select(lv => Mathf.CeilToInt(BossStats.LifeOf(r, bossScene, lv) * tune.Life * BossPhases.WholeFactor(rec, bossScene))).ToList();
                Plugin.Log.LogInfo($"[平衡] 首领 {Bestiary.NameOf(rec)}（{kv.Key}，阶段 {r.Stage}）= 泰拉 {r.Boss.Name}，{tune.Note ?? "按原样"}：生命 ×{tune.Life:F2} {string.Join("/", lives)}（普通/困难/专家/大师），防御 {defense}（×{tune.Defense:F2}），" +
                                   $"伤害 ×{World.Areas.BossDamage * tune.Damage:F2}（普通敌人的倍数），{(tune.Stun ? "能被打出硬直" : "霸体")}；最强武器 {(have.Count > 0 ? have[0].Name : "无")} {dps:F0}/秒，大师 {lives[3] / dps:F0} 秒；神居重打 大师 {Mathf.RoundToInt(BossStats.LifeOf(r, "GG_", DifficultyLevel.Master) * tune.Life)}");
            }

            // Things on offer earlier than their Terraria tier (rarity 4+ is hardmode, 7+ after Plantera, 10+ lunar):
            // stages 0-2 are before hardmode, 3-4 hardmode, 5 the end.
            foreach (var d in ItemDb.All.OrderBy(x => x.Stage).ThenByDescending(x => x.Rare))
            {
                if (d.Material || d.Rare < 4) continue;
                bool early = (d.Stage <= 2 && d.Rare >= 4) || (d.Stage <= 3 && d.Rare >= 7) || (d.Stage <= 4 && d.Rare >= 10);
                if (!early) continue;
                Plugin.Log.LogInfo($"[平衡] 太早：{d.Name}（{d.Type}）阶段 {d.Stage}，稀有度 {d.Rare}，来源 {d.Source}，伤害 {d.Damage} 防御 {d.Defense}");
            }

            // Live: the starting weapons on King's Pass's crawlids and vengeflies.
            Shop.DevStage = 0;
            bool wasInvincible = PlayerData.instance.isInvincible;
            PlayerData.instance.isInvincible = false;
            Equip(new ItemType[0], new ItemType[0]);
            Equipment.Update(mod.Inventory);
            Player.PlayerBuffs.Clear();
            foreach (var lv in levels)
            {
                GameDifficulty.Current = lv;
                foreach (var (rec, weapon) in new[] { ("Crawler", ItemType.CopperShortsword), ("Buzzer", ItemType.CopperShortsword), ("Crawler", ItemType.WoodenBow) })
                {
                    HealthManager target = null;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (!hm.isDead && hm.gameObject.activeInHierarchy && Bestiary.KeyOf(hm) == rec) { target = hm; break; }
                    if (target == null) continue;
                    var info = EnemyStats.Of(target);
                    var def2 = ItemDb.Get(weapon);
                    int ammo = weapon == ItemType.WoodenBow ? ItemDb.Get(ItemType.WoodenArrow).Damage : 0;
                    int perHit = EnemyStats.Strike(info, def2.Damage + ammo, false);
                    int lifeMax = EnemyStats.LifeMax(info);
                    int hits = Mathf.CeilToInt(lifeMax / (float)perHit);
                    Plugin.Log.LogInfo($"[平衡] 实测 {GameDifficulty.NameOf(lv)}：{Bestiary.NameOf(rec)} 生命 {lifeMax}，{def2.Name}{(ammo > 0 ? "+木箭" : "")} 每下 {perHit}（不暴击），{hits} 下打死，约 {hits / RateOf(def2):F1} 秒");
                }
                // A real hit taken from a crawlid.
                HealthManager crawler = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (!hm.isDead && hm.gameObject.activeInHierarchy && Bestiary.KeyOf(hm) == "Crawler") { crawler = hm; break; }
                if (crawler != null)
                {
                    mod.Stats.Life = mod.Stats.LifeMax;
                    int before = mod.Stats.Life;
                    hero.TakeDamage(crawler.gameObject, GlobalEnums.CollisionSide.left, 1, 1);
                    yield return new WaitForSeconds(0.2f);
                    Plugin.Log.LogInfo($"[平衡] 实测 {GameDifficulty.NameOf(lv)}：被小爬虫碰一下掉 {before - mod.Stats.Life} / {mod.Stats.LifeMax}");
                    if (before == mod.Stats.Life) Plugin.Log.LogError($"[AutoTest] 平衡实测接触伤害 {lv} 不通过");
                    yield return new WaitForSeconds(2.2f);
                    mod.Stats.Life = mod.Stats.LifeMax;
                }
            }
            GameDifficulty.Current = saved;
            Shop.DevStage = savedStage;
            PlayerData.instance.isInvincible = wasInvincible;
            mod.DevNoFallDamage = false;
        }
    }
}
