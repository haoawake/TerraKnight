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
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        sealed class EarlyMagicRow
        {
            public string Weapon, Name, Profile, Target, Armor, Source;
            public int Stage, Damage, ManaCost, ManaMax, ManaEnd, Hits, Casts, SlowCasts, Defense;
            public float Seconds, Dps, FirstDry = -1, BurstDps, TailDps;
            public int TotalDamage;
            public bool Killed;
            public int EnemyLife, PlayerHits;
        }

        readonly List<EarlyMagicRow> earlyMagicRows = new List<EarlyMagicRow>();
        object[] earlyMagicCatalog;
        object[] earlyMagicGear;
        bool earlyMagicComplete;
        bool earlyMagicRunning;

        IEnumerator EarlyMagicUnpause()
        {
            while (earlyMagicRunning) {
                var gm = GameManager.UnsafeInstance;
                if (gm != null && gm.isPaused) {
                    Plugin.Log.LogInfo("[EARLY_MAGIC] Resuming test copy after external/controller pause");
                    gm.StartCoroutine(gm.PauseGameToggleByMenu());
                    yield return new WaitForSecondsRealtime(2f);
                } else yield return new WaitForSecondsRealtime(.5f);
            }
        }

        void SaveEarlyMagic()
        {
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "early-magic-audit.json"), JsonConvert.SerializeObject(new {
                Complete = earlyMagicComplete,
                Scope = "Current runtime stages 0-2, unprefixed gear, no buffs/charms/mastery/banners/potions. Dummy motion is a synthetic fixture, not a boss clear. Finite-mana rows have no refill and stationary regeneration. Master difficulty; player immortality does not replenish mana. Unlimited fixtures use a 200+armor mana pool. Clouds are placed above targets and cast once, not continuously replaced.",
                ManaAssumption = "Reference pool: stage0 20, stage1 80, stage2 200, plus equipped armor. Sensitivity rows explicitly use 20 mana. These are scenarios, not mandatory progression caps.",
                Catalog = earlyMagicCatalog, Gear = earlyMagicGear, Rows = earlyMagicRows
            }, Formatting.Indented));
        }

        void EarlyMagicReset()
        {
            mod.CancelUse();
            mod.DevHold = false; mod.DevAim = null; mod.SetUseCooldown(0);
            Minions.Instance.Clear(); PlayerBuffs.Clear(); SummonTags.Clear(); EnemyDebuffs.Clear();
            TerraProjectiles.Instance.DevClear(); Whips.Instance.Clear(); SwordFx.Instance?.Clear(); ZenithSwords.Instance?.DevClear();
            mod.Inventory.Mouse = default; mod.Inventory.Override = -1; mod.Inventory.UseMouse = false;
        }

        void EarlyMagicEquip(ItemDef d, string profile, ItemType robe = ItemType.None, ItemType hat = ItemType.None)
        {
            EarlyMagicReset();
            var inv = mod.Inventory;
            Array.Clear(inv.Slots, 0, inv.Slots.Length);
            Equip(Array.Empty<ItemType>(), Array.Empty<ItemType>());
            // Clear shared equipment in other loadouts as well as the active one.
            foreach (var loadout in inv.Loadouts) { Array.Clear(loadout.Armor, 0, loadout.Armor.Length); Array.Clear(loadout.Accessories, 0, loadout.Accessories.Length); }
            if (profile != "bare" && profile != "lowmana") Equip(ProgressionBalance.ArmorFor(d.Stage, 2).ToArray(), Array.Empty<ItemType>());
            if (robe != ItemType.None) {
                Equip(new[] { hat, robe, ItemType.None }, Array.Empty<ItemType>());
                // A robe-only measurement isolates its feature from hats and accessories.
            }
            Equipment.Update(inv);
            mod.Stats.ManaCrystals = profile == "bare" || profile == "lowmana" ? 0 : d.Stage == 0 ? 0 : d.Stage == 1 ? 3 : 9;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            mod.Stats.Mana = mod.Stats.ManaMax;
            inv.Selected = 0; inv.Slots[0] = ItemStack.Of(d.Type);
            if (d.UseAmmo != AmmoKind.None) {
                var ammo = ItemDb.All.Where(x => x != null && x.IsAmmo && x.Ammo == d.UseAmmo && x.Stage <= d.Stage).OrderBy(x => x.Damage).FirstOrDefault();
                if (ammo != null) inv.Slots[Inventory.Size - 1] = ItemStack.Of(ammo.Type, 9999);
            }
            // Shade Wraith is held via its custom handler despite ItemDef.Channel being false.
            mod.DevHold = d.Channel || d.Type == ItemType.ShadeSoulFocus ? true : (bool?)null;
        }

        IEnumerator EarlyMagicDummy(ItemDef d, string profile, float duration, bool refill, bool moving, ItemType robe = ItemType.None, ItemType hat = ItemType.None)
        {
            var hero = HeroController.instance;
            var dummy = TargetDummy.DevHm;
            var dummyAt = dummy.transform.position;
            var col = dummy.GetComponent<Collider2D>();
            float distance = d.Class == DamageClass.Melee && !d.IsWhip ? 1.6f : 5f;
            var stand = Clear(hero, TargetDummy.DevAt.Value + Vector2.left * distance + Vector2.up * .5f);
            EarlyMagicEquip(d, profile, robe, hat);
            if (refill) {
                // Unlimited-output fixtures must fit a 30-mana spell even in the bare profile.
                mod.Stats.ManaCrystals = TerraStats.MaxManaCrystals;
                mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
                mod.Stats.Mana = mod.Stats.ManaMax;
            }
            Shop.DevStage = d.Stage;
            EnemyStats.DevDummyDefense = StageBossDefense(d.Stage);
            hero.transform.position = stand;
            var body = hero.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(.3f);
            var row = new EarlyMagicRow { Weapon = d.Type.ToString(), Name = d.Name, Stage = d.Stage, Damage = d.Damage,
                Profile = robe == ItemType.None ? profile : "robe/" + robe + (hat == ItemType.None ? "" : "+" + hat), Target = moving ? "moving-dummy-250ms-aim" : "static-dummy",
                ManaMax = mod.Stats.ManaMax, ManaCost = Equipment.ManaCost(d), Defense = EnemyStats.DevDummyDefense,
                Armor = string.Join("+", mod.Inventory.EffectiveArmor.Where(x => !x.IsEmpty).Select(x => x.Type)), Source = d.Source.ToString() };
            int damage0 = TargetDummy.DevDamage, hit0 = TargetDummy.DevHits, slow0 = TerraMod.DevSlowCasts, burst = 0, tail0 = 0;
            float started = Time.time, nextAim = 0;
            Vector2 aim = col.bounds.center;
            if (d.Type == ItemType.CrimsonRod) aim += Vector2.up * 3f;
            UnityEngine.Random.InitState(725031 + (int)d.Type * 7 + (int)robe);
            while (Time.time - started < duration)
            {
                float t = Time.time - started;
                hero.transform.position = stand; body.linearVelocity = Vector2.zero;
                if (moving) dummy.transform.position = dummyAt + new Vector3(Mathf.Sin(t * 1.7f) * 3, 1.3f + Mathf.Sin(t * 2.1f), 0);
                if (Time.time >= nextAim) { nextAim = Time.time + .25f; aim = col.bounds.center; if (d.Type == ItemType.CrimsonRod) aim += Vector2.up * 3f; }
                if (refill) mod.Stats.Mana = mod.Stats.ManaMax;
                mod.DevAim = aim;
                if (mod.DevReady && !mod.IsUsingItem && (d.Type != ItemType.CrimsonRod || row.Casts == 0)) { mod.DevUseSelected(aim); row.Casts++; }
                if (row.FirstDry < 0 && TerraMod.DevSlowCasts > slow0) row.FirstDry = t;
                if (t < 4f) burst = TargetDummy.DevDamage - damage0;
                if (t < duration - 4f) tail0 = TargetDummy.DevDamage - damage0;
                yield return null;
            }
            // No post-window damage is attributed to this row; retained clouds are cleared before the next one.
            row.Seconds = Time.time - started;
            row.TotalDamage = TargetDummy.DevDamage - damage0; row.Hits = TargetDummy.DevHits - hit0;
            row.Dps = row.TotalDamage / row.Seconds; row.BurstDps = burst / Mathf.Min(4, duration);
            row.TailDps = (row.TotalDamage - tail0) / Mathf.Min(4, duration);
            row.SlowCasts = TerraMod.DevSlowCasts - slow0; row.ManaEnd = mod.Stats.Mana;
            earlyMagicRows.Add(row); SaveEarlyMagic();
            Plugin.Log.LogInfo($"[EARLY_MAGIC] {earlyMagicRows.Count} {row.Weapon} {row.Profile} {row.Target} {row.Dps:F1}/s hits={row.Hits} mana={row.ManaEnd}/{row.ManaMax} slow={row.SlowCasts}");
            dummy.transform.position = dummyAt;
            EarlyMagicReset();
            yield return null;
        }

        IEnumerator EarlyMagicAudit()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Independent saves required");
            Application.targetFrameRate = 120;
            mod.ApplyCreative(false); TerraMod.DevAllInfinite = false; mod.Inventory.Infinite = false;
            mod.DevNoFallDamage = true; TerraStats.DevImmortal = true; PlayerData.instance.isInvincible = true;
            InventoryGui.Open = false; Equipment.AutoReuseAll = true;
            GameDifficulty.Current = DifficultyLevel.Master;
            var weapons = ItemDb.All.Where(d => d != null && d.IsWeapon && d.Class == DamageClass.Magic && d.Stage >= 0 && d.Stage <= 2).OrderBy(d => d.Stage).ThenBy(d => d.Type).ToArray();
            earlyMagicCatalog = weapons.Select(d => (object)new { Type = d.Type.ToString(), d.Name, d.Stage, d.Damage, d.Mana, d.UseTime, d.UseAnimation, d.Channel,
                Source = d.Source.ToString(), Native = ProgressionBalance.OriginalWeapon(d, out int native, out _) ? native : 0,
                BossRewardOf = ProgressionBalance.BossRewards.Where(p => p.Value.Weapons.Contains(d.Type)).Select(p => p.Key).ToArray()
            }).ToArray();
            earlyMagicGear = ItemDb.All.Where(d => d != null && (d.Set == ArmorSet.Robe || d.Type == ItemType.Robe || d.Type == ItemType.WizardHat || d.Type == ItemType.MagicHat)).Select(d => (object)new {
                Type = d.Type.ToString(), d.Name, d.Stage, Source = d.Source.ToString(), d.Defense,
                d.Bonus.ManaMax, d.Bonus.ManaCost, d.Bonus.MagicDamage, d.Bonus.MagicCrit
            }).ToArray();
            SaveEarlyMagic();
            Plugin.Log.LogInfo("[EARLY_MAGIC] Catalog=" + weapons.Length + " " + string.Join(",", weapons.Select(d => d.Type)));
            earlyMagicRunning = true;
            mod.StartCoroutine(EarlyMagicUnpause());
            try
            {
                yield return GoTo("Town", "left1");
                for (float until = Time.realtimeSinceStartup + 12; TargetDummy.DevHm == null && Time.realtimeSinceStartup < until;) yield return null;
                if (TargetDummy.DevHm == null) throw new InvalidOperationException("No target dummy");
                if (suite == "earlymagicsmoke") {
                    foreach (var type in new[] { ItemType.AmethystStaff, ItemType.MagicMissile, ItemType.CrimsonRod, ItemType.HowlingWraithsFocus })
                        yield return EarlyMagicDummy(ItemDb.Get(type), "bare", 4f, true, false);
                } else if (suite == "earlymagicboss") {
                    yield return EarlyMagicBosses(weapons);
                } else if (suite == "earlymagicencounters") {
                    yield return EarlyMagicEncounters(weapons);
                } else if (suite == "earlymagicencounterslate") {
                    yield return EarlyMagicEncounters(weapons.Where(d => d.Stage > 0).ToArray());
                } else if (suite == "earlymagicencountercity") {
                    yield return EarlyMagicEncounters(weapons.Where(d => d.Stage == 2).ToArray());
                } else if (suite == "earlymagictracking") {
                    yield return EarlyMagicTracking();
                } else if (suite == "earlymagicrepair") {
                    var d = ItemDb.Get(ItemType.ShadeSoulFocus);
                    yield return EarlyMagicDummy(d, "bare", 6f, true, false);
                    yield return EarlyMagicDummy(d, "reference", 6f, true, false);
                    yield return EarlyMagicDummy(d, "finite", 20f, false, false);
                    yield return EarlyMagicDummy(d, "reference", 8f, true, true);
                    yield return EarlyMagicBosses(new[] { d });
                } else if (suite == "earlymagicbalance") {
                    var gems = weapons.Where(d => TerraProjectiles.GemOf(d.Shoot) != TerraProjectiles.GemFx.None).ToArray();
                    foreach (var d in gems) {
                        yield return EarlyMagicDummy(d, "bare", 6f, true, false);
                        yield return EarlyMagicDummy(d, "reference", 6f, true, false);
                        yield return EarlyMagicDummy(d, "finite", 20f, false, false);
                        yield return EarlyMagicDummy(d, "reference", 8f, true, true);
                        foreach (var robe in ItemDb.All.Where(x => x != null && TerraProjectiles.RobeGem(x.Type) != TerraProjectiles.GemFx.None && x.Stage <= 2))
                            yield return EarlyMagicDummy(d, "reference", 6f, true, false, robe.Type);
                    }
                    for (int trial = 0; trial < 3; trial++)
                        foreach (var robe in new[] { ItemType.None, ItemType.AmethystRobe, ItemType.EmeraldRobe, ItemType.DiamondRobe, ItemType.AmberRobe }) {
                            yield return EarlyMagicDummy(ItemDb.Get(ItemType.AmethystStaff), "bare", 8f, true, false, robe);
                            earlyMagicRows.Last().Profile += "/validated-repeat" + trial;
                            SaveEarlyMagic();
                        }
                    yield return EarlyMagicTracking();
                    yield return EarlyMagicSingleHit();
                } else if (suite == "earlymagicbalanceencounters") {
                    yield return EarlyMagicEncounters(weapons.Where(d => d.Stage == 0).ToArray());
                } else if (suite == "earlymagiccontrols") {
                    for (int stage = 0; stage <= 2; stage++)
                        foreach (var type in ProgressionBalance.Core[stage][0].Concat(ProgressionBalance.Core[stage][1]))
                            yield return EarlyMagicDummy(ItemDb.Get(type), "bare", 6f, true, false);
                    var d = ItemDb.Get(ItemType.AmethystStaff);
                    for (int trial = 0; trial < 3; trial++)
                        foreach (var robe in new[] { ItemType.None, ItemType.AmethystRobe, ItemType.EmeraldRobe, ItemType.DiamondRobe, ItemType.AmberRobe }) {
                            yield return EarlyMagicDummy(d, "bare", 8f, true, false, robe);
                            earlyMagicRows.Last().Profile += "/validated-repeat" + trial;
                            SaveEarlyMagic();
                        }
                } else if (suite == "earlymagicfocus") {
                    var d = ItemDb.Get(ItemType.AmethystStaff);
                    yield return EarlyMagicDummy(d, "lowmana", 20f, false, false);
                    foreach (var robe in ItemDb.All.Where(x => x != null && (x.Set == ArmorSet.Robe || x.Type == ItemType.Robe) && x.Stage <= 2)) {
                        yield return EarlyMagicDummy(d, "reference", 6f, true, false, robe.Type);
                        yield return EarlyMagicDummy(d, "finite", 20f, false, false, robe.Type);
                        earlyMagicRows.Last().Profile += "/finite";
                        SaveEarlyMagic();
                        yield return EarlyMagicDummy(d, "reference", 8f, true, true, robe.Type);
                        foreach (var hat in new[] { ItemType.WizardHat, ItemType.MagicHat })
                            yield return EarlyMagicDummy(d, "reference", 8f, true, false, robe.Type, hat);
                    }
                    foreach (var staff in weapons.Where(x => TerraProjectiles.GemOf(x.Shoot) != TerraProjectiles.GemFx.None))
                        yield return EarlyMagicDummy(staff, "reference", 6f, true, false, ItemType.GypsyRobe);
                    // A small repeat set establishes variance for the reported combination.
                    for (int trial = 0; trial < 3; trial++) {
                        yield return EarlyMagicDummy(d, "reference", 8f, true, false, ItemType.AmethystRobe);
                        earlyMagicRows.Last().Profile += "/repeat" + trial;
                        SaveEarlyMagic();
                    }
                } else {
                    foreach (var d in weapons)
                    {
                        yield return EarlyMagicDummy(d, "bare", 6f, true, false);
                        yield return EarlyMagicDummy(d, "reference", 6f, true, false);
                        yield return EarlyMagicDummy(d, "finite", 20f, false, false);
                        yield return EarlyMagicDummy(d, "reference", 8f, true, true);
                    }
                    // Gem staff/robe cross product, filtered to robes available by that weapon's stage.
                    // Higher-stage robe combinations are sensitivity cases, labelled separately by the report.
                    var robes = ItemDb.All.Where(d => d != null && TerraProjectiles.RobeGem(d.Type) != TerraProjectiles.GemFx.None && d.Stage <= 2).ToArray();
                    foreach (var d in weapons.Where(d => TerraProjectiles.GemOf(d.Shoot) != TerraProjectiles.GemFx.None))
                        foreach (var robe in robes) yield return EarlyMagicDummy(d, "reference", 6f, true, false, robe.Type);
                    foreach (var type in new[] { ItemType.AmethystStaff, ItemType.TopazStaff, ItemType.SapphireStaff, ItemType.RubyStaff, ItemType.DiamondStaff })
                        yield return EarlyMagicDummy(ItemDb.Get(type), "lowmana", 20f, false, false);
                    // Same fixture and defense, naked melee/ranged baselines for context.
                    for (int stage = 0; stage <= 2; stage++)
                        foreach (var type in ProgressionBalance.Core[stage][0].Concat(ProgressionBalance.Core[stage][1]))
                            yield return EarlyMagicDummy(ItemDb.Get(type), "bare", 6f, true, false);
                }
                earlyMagicComplete = true; SaveEarlyMagic();
            }
            finally {
                earlyMagicRunning = false;
                MeleeHits.DevAcceptedDamage = null;
                EarlyMagicReset(); EnemyStats.DevDummyDefense = 0; TerraStats.DevImmortal = false;
                mod.DevNoFallDamage = false; mod.DevHold = null; mod.DevAim = null;
            }
        }

        IEnumerator EarlyMagicBosses(ItemDef[] weapons)
        {
            EnemyAiMode.Current = EnemyAiKind.New;
            // Natural AI and native invulnerability remain active. Health is topped up between
            // frames only to keep a comparable damage window; these are not kill-time runs.
            foreach (var entry in new[] { (0, "FalseKnight"), (1, "Hornet"), (2, "MageLord") })
            {
                if (!weapons.Any(d => d.Stage == entry.Item1)) continue;
                Shop.DevStage = entry.Item1;
                bool entered = BossArena.Fight(entry.Item2, true);
                if (!entered) throw new InvalidOperationException("Boss entry failed: " + entry.Item2);
                TerraKnight.EnemyAi.Bosses.BossKit kit = null;
                for (float until = Time.realtimeSinceStartup + 75; Time.realtimeSinceStartup < until;) {
                    kit = KitOf(entry.Item2);
                    if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                    yield return null;
                }
                if (kit == null) throw new InvalidOperationException("Missing boss kit: " + entry.Item2);
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                var hc = hero.GetComponent<Collider2D>();
                for (float until = Time.time + 8; !kit.Engaged && Time.time < until;) {
                    if (!BossWake.Rouse(kit.Hm)) kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Multiplier = 1, MagnitudeMultiplier = 0 });
                    yield return new WaitForSeconds(.5f);
                }
                yield return new WaitForSeconds(3);
                foreach (var d in weapons.Where(d => d.Stage == entry.Item1))
                {
                    EarlyMagicEquip(d, "finite");
                    Shop.DevStage = d.Stage;
                    var row = new EarlyMagicRow { Weapon = d.Type.ToString(), Name = d.Name, Stage = d.Stage, Damage = d.Damage,
                        Profile = "reference-finite-immortal", Target = "native-ai-window/" + entry.Item2, Source = d.Source.ToString(),
                        ManaMax = mod.Stats.ManaMax, ManaCost = Equipment.ManaCost(d), Defense = kit.Info.Defense,
                        Armor = string.Join("+", mod.Inventory.EffectiveArmor.Where(x => !x.IsEmpty).Select(x => x.Type)) };
                    string record = entry.Item2;
                    MeleeHits.DevAcceptedDamage = (hm, damage) => {
                        if (hm != null && EnemyStats.Of(hm)?.Record == record) { row.TotalDamage += damage; row.Hits++; }
                    };
                    var stateNames = new HashSet<string>();
                    Vector2 origin = kit.Me;
                    float traveled = 0, start = Time.time, nextAim = 0;
                    int slow0 = TerraMod.DevSlowCasts, burst = 0, tail = 0;
                    Vector2 aim = kit.Me;
                    Vector2 safe = kit.ClampToArena(kit.Me, 3);
                    safe.x = Mathf.Clamp(safe.x - 6, kit.Arena.xMin + 2, kit.Arena.xMax - 2);
                    safe.y = kit.GroundBelow(new Vector2(safe.x, kit.Arena.center.y)) + 1.5f;
                    while (Time.time - start < 12 && kit != null && kit.Hm != null && !kit.Hm.isDead)
                    {
                        // Hold the player in a reproducible position; immortality avoids deaths,
                        // but does not restore mana. This does not measure player survival or dodging.
                        hero.transform.position = new Vector3(safe.x, safe.y, hero.transform.position.z);
                        rb.linearVelocity = Vector2.zero;
                        float t = Time.time - start;
                        traveled += Vector2.Distance(origin, kit.Me); origin = kit.Me;
                        stateNames.Add(kit.StateNow ?? "?");
                        if (Time.time >= nextAim) { nextAim = Time.time + .25f; aim = kit.Hm.GetComponent<Collider2D>()?.bounds.center ?? kit.Me; if (d.Type == ItemType.CrimsonRod) aim += Vector2.up * 3f; }
                        mod.DevAim = aim;
                        if (mod.DevReady && !mod.IsUsingItem && hero.CanInput() && (d.Type != ItemType.CrimsonRod || row.Casts == 0)) { mod.DevUseSelected(aim); row.Casts++; }
                        if (TerraMod.DevSlowCasts > slow0 && row.FirstDry < 0) row.FirstDry = t;
                        if (t < 4) burst = row.TotalDamage;
                        if (t < 8) tail = row.TotalDamage;
                        // Keep the AI in the same early phase, without disabling its shields.
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (hm != null && !hm.isDead && EnemyStats.Of(hm)?.Record == record) hm.hp = Mathf.Max(hm.hp, Mathf.RoundToInt(EnemyStats.Of(hm).MaxHp * .8f));
                        yield return null;
                    }
                    row.Seconds = Time.time - start; row.Dps = row.TotalDamage / Mathf.Max(.01f, row.Seconds);
                    row.BurstDps = burst / 4f; row.TailDps = (row.TotalDamage - tail) / 4f;
                    row.ManaEnd = mod.Stats.Mana; row.SlowCasts = TerraMod.DevSlowCasts - slow0;
                    row.Target += "/states=" + stateNames.Count + "/travel=" + traveled.ToString("F1");
                    earlyMagicRows.Add(row); SaveEarlyMagic();
                    Plugin.Log.LogInfo($"[EARLY_MAGIC] {earlyMagicRows.Count} {row.Weapon} {row.Target} {row.Dps:F1}/s hits={row.Hits}");
                    MeleeHits.DevAcceptedDamage = null;
                    EarlyMagicReset();
                }
                MeleeHits.DevAcceptedDamage = null;
                yield return GoTo("Town", "left1");
            }
        }
    }
}
