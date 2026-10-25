using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "classdps"): every stage's four classes' reference kits (ProgressionBalance's armor and accessory paths,
    /// newest first, the slots full) worn for real, each of the class's core weapons used on Dirtmouth's Target Dummy for six
    /// seconds with the dummy given the median defense of the stage's bosses (EnemyStats.DevDummyDefense): the Terraria damage it took a
    /// second. A summoner's best minion staff is then measured again with its best whip swung over the minions, and the
    /// Knight's nail beside the warrior's. Research export only (classdps.json): the saves and the kit are put back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The median defense of a stage's world bosses (EnemyStats.BossDefense): what its weapons are measured against.</summary>
        static int StageBossDefense(int stage)
        {
            var defs = new List<int>();
            foreach (var kv in BossStats.DevAll)
            {
                if (!ProgressionBalance.BossRewards.TryGetValue(kv.Key, out var reward) || reward.Stage != stage || !ProgressionBalance.FormalBoss(kv.Key)) continue;
                string scene = BestiaryHabitats.WorldScene(kv.Key) ?? BossArena.SceneOf(kv.Key) ?? EnemyDrops.SceneOf(kv.Key);
                if (scene == null || Areas.GodhomeRoom(scene)) continue;
                defs.Add(EnemyStats.BossDefense(kv.Value, BossBalance.Of(kv.Value.Record, EnemyDrops.VariantLevel(scene)), scene));
            }
            if (defs.Count == 0) return Areas.Defense(stage);
            defs.Sort();
            return defs[defs.Count / 2];
        }

        IEnumerator ClassDpsAudit()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            TerraStats.DevImmortal = true;
            yield return GoTo("Town", "left1");
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            if (TargetDummy.DevAt == null) { Plugin.Log.LogInfo("[AutoTest] 职业实测 没有训练假人"); TerraStats.DevImmortal = false; yield break; }
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var difficultyWas = GameDifficulty.Current;
            int stageWas = Shop.DevStage;
            bool infiniteWas = inv.Infinite;
            var saved = (ItemStack[])inv.Slots.Clone();
            var savedArmor = (ItemStack[])inv.Armor.Clone();
            var savedAccessories = (ItemStack[])inv.Accessories.Clone();
            int crystalsWas = mod.Stats.ManaCrystals;
            bool autoWas = Equipment.AutoReuseAll;
            GameDifficulty.Current = DifficultyLevel.Master;
            inv.Infinite = true;
            InventoryGui.Open = false;
            Equipment.AutoReuseAll = true;
            MinionEngage.DevAlways = true;
            var dummy = TargetDummy.DevAt.Value;
            var aim = DummyCentre();
            var heroBody = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            const int slot = Inventory.HotbarSize - 1;
            var rows = new List<object>();

            void Reset()
            {
                heroBody.linearVelocity = Vector2.zero;
                Minions.Instance.Clear(); PlayerBuffs.Clear(); SummonTags.Clear(); EnemyDebuffs.Clear();
                TerraProjectiles.Instance.DevClear(); Whips.Instance.Clear(); SwordFx.Instance?.Clear(); ZenithSwords.Instance?.DevClear();
            }

            // One weapon (and, for a summoner's pair, its minions first) at the dummy for some seconds: Terraria damage a second.
            IEnumerator Measure(ItemType weapon, ItemType minions, int stage, float seconds, Action<float, int, int> done)
            {
                Reset();
                var d = ItemDb.Get(weapon);
                for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
                inv.Mouse = default; inv.Override = -1; inv.UseMouse = false;
                // The best ammo of the stage for it.
                ItemDef ammo = null;
                if (d.UseAmmo != AmmoKind.None)
                    foreach (var a in ItemDb.All)
                        if (a.Ammo == d.UseAmmo && a.Stage <= stage && a.Damage >= 0 && !a.IsWeapon && (ammo == null || a.Damage > ammo.Damage)) ammo = a;
                if (ammo != null) inv.Slots[Inventory.Size - 1] = ItemStack.Of(ammo.Type, 9999);
                mod.Stats.ManaCrystals = TerraStats.MaxManaCrystals;
                mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
                bool melee = (d.Class == DamageClass.Melee && !d.IsWhip) || weapon == ItemType.KnightNail;
                Vector3 stand = Clear(hero, dummy + Vector2.left * (melee ? 1.6f : 5f) + Vector2.up * 0.5f);
                hero.transform.position = stand; heroBody.linearVelocity = Vector2.zero; hero.FaceRight();
                EnemyStats.DevDummyDefense = StageBossDefense(stage);
                yield return new WaitForSeconds(0.4f);
                // The minions first, a full set of them.
                var staff = minions != ItemType.None ? minions : d.IsSummon ? weapon : ItemType.None;
                if (staff != ItemType.None)
                {
                    var sd = ItemDb.Get(staff);
                    inv.Selected = slot; inv.Slots[slot] = ItemStack.Of(staff);
                    int n = sd.Sentry ? Equipment.MaxTurrets : Equipment.MaxMinions;
                    for (int i = 0; i < n; i++)
                    {
                        for (float w = Time.time + 2f; (!mod.DevReady || mod.IsUsingItem) && Time.time < w;) yield return null;
                        mod.DevUseSelected((Vector2)col.bounds.center + (sd.Sentry ? Vector2.right * 2f : Vector2.up));
                        yield return null;
                    }
                    yield return new WaitForSeconds(1.2f);
                }
                inv.Selected = slot; inv.Slots[slot] = ItemStack.Of(weapon, d.MaxStack > 1 ? Mathf.Min(999, d.MaxStack) : 1);
                if (weapon == ItemType.KnightNail) { PlayerData.instance.hasNailArt = false; NailAim.DevAim = aim; NailItem.DevHold = true; }
                mod.DevHold = d.Channel ? true : (bool?)null; mod.DevAim = aim;
                yield return null;
                int d0 = TargetDummy.DevDamage, h0 = TargetDummy.DevHits;
                float start = Time.time;
                bool usesItem = !(d.IsSummon && staff == weapon) && weapon != ItemType.KnightNail;
                while (Time.time - start < seconds)
                {
                    // Pushed off by a weapon's own move: back where it stood.
                    if (((Vector2)hero.transform.position - (Vector2)stand).sqrMagnitude > 0.5f * 0.5f) { hero.transform.position = stand; heroBody.linearVelocity = Vector2.zero; }
                    mod.DevAim = aim; mod.Stats.Mana = mod.Stats.ManaMax;
                    if (usesItem && mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                    yield return null;
                }
                NailItem.DevHold = false; NailAim.DevAim = null;
                mod.DevHold = null;
                // Shots still on their way count; the time they take doesn't.
                yield return new WaitForSeconds(0.6f);
                float took = Time.time - start - 0.6f;
                int damage = TargetDummy.DevDamage - d0, hits = TargetDummy.DevHits - h0;
                mod.DevAim = null;
                done(damage / Mathf.Max(0.1f, took), hits, EnemyStats.DevDummyDefense);
            }

            try
            {
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    for (int cls = 0; cls < 4; cls++)
                    {
                        var armor = ProgressionBalance.ArmorFor(stage, cls).ToArray();
                        var accs = new List<ItemType>();
                        for (int s = stage; s >= 0 && accs.Count < Inventory.OpenAccessories; s--)
                            foreach (var t in ProgressionBalance.AccessoryPaths[s][cls])
                                if (!accs.Contains(t) && accs.Count < Inventory.OpenAccessories) accs.Add(t);
                        Equip(armor, accs.ToArray());
                        Equipment.Update(inv);
                        var weapons = new List<object>();
                        float bestMinion = 0f, bestWhip = 0f;
                        ItemType minionStaff = ItemType.None, whip = ItemType.None;
                        var list = ProgressionBalance.Core[stage][cls].ToList();
                        if (cls == 0) list.Add(ItemType.KnightNail);
                        foreach (var t in list)
                        {
                            var d = ItemDb.Get(t);
                            if (d == null || (!d.IsWeapon && t != ItemType.KnightNail)) continue;
                            float dps = 0f; int hits = 0, def = 0;
                            yield return Measure(t, ItemType.None, stage, 6f, (x, h, y) => { dps = x; hits = h; def = y; });
                            weapons.Add(new { Item = t.ToString(), d.Name, d.Damage, Dps = dps, Hits = hits, Defense = def, d.IsWhip, Minion = d.IsSummon && !d.Sentry, d.Sentry });
                            Plugin.Log.LogInfo($"[AutoTest] 职业实测 阶段{stage} 职业{cls} {d.Name}：{dps:F0}/秒，{hits} 下（防 {def}）");
                            if (d.IsSummon && !d.Sentry && dps > bestMinion) { bestMinion = dps; minionStaff = t; }
                            if (d.IsWhip && dps > bestWhip) { bestWhip = dps; whip = t; }
                        }
                        float combined = 0f;
                        if (cls == 3 && minionStaff != ItemType.None && whip != ItemType.None)
                        {
                            yield return Measure(whip, minionStaff, stage, 6f, (x, h, y) => combined = x);
                            Plugin.Log.LogInfo($"[AutoTest] 职业实测 阶段{stage} 召唤 {ItemDb.Get(minionStaff).Name}+{ItemDb.Get(whip).Name}：{combined:F0}/秒");
                        }
                        var b = Equipment.Current;
                        rows.Add(new
                        {
                            Stage = stage, Class = cls, Armor = armor.Select(x => x.ToString()).ToArray(), Accessories = accs.Select(x => x.ToString()).ToArray(),
                            b.Defense, b.Endurance, b.MeleeDamage, b.RangedDamage, b.MagicDamage, b.MinionDamage, Slots = Equipment.MaxMinions, Mastery = BossMastery.Bonus,
                            Weapons = weapons, Combined = combined, CombinedOf = minionStaff + "+" + whip,
                        });
                    }
                }
                string path = Path.Combine(outDir, "classdps.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(rows, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 职业实测 导出 {rows.Count} 套 → {path}");
            }
            finally
            {
                Reset();
                EnemyStats.DevDummyDefense = 0;
                MinionEngage.DevAlways = false;
                TerraStats.DevImmortal = false;
                mod.DevHold = null; mod.DevAim = null; mod.DevNoFallDamage = false; NailItem.DevHold = false; NailAim.DevAim = null;
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = saved[i];
                for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = savedArmor[i];
                for (int i = 0; i < inv.Accessories.Length; i++) inv.Accessories[i] = savedAccessories[i];
                mod.Stats.ManaCrystals = crystalsWas;
                Equipment.Update(inv);
                inv.Infinite = infiniteWas;
                Equipment.AutoReuseAll = autoWas;
                GameDifficulty.Current = difficultyWas;
                Shop.DevStage = stageWas;
            }
        }
    }
}
