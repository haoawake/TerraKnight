using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PlayerChallengesTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town", "left1");
            yield return new WaitForSecondsRealtime(1f);
            int checks = 0, failures = 0;
            void Check(string name, bool pass) { checks++; if (!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] PLAYER_CHALLENGES {name} {(pass ? "通过" : "不通过")}"); }
            var difficulty = GameDifficulty.Current;
            var bindings = PlayerChallenges.DevBindings;
            var resting = PlayerChallenges.DevResting;
            var random = UnityEngine.Random.state;
            bool invincible = PlayerData.instance.isInvincible, immortal = TerraStats.DevImmortal;
            var fields = typeof(TerraStats).GetFields(BindingFlags.Instance | BindingFlags.Public);
            var values = Array.ConvertAll(fields, f => f.GetValue(mod.Stats));
            var drink = typeof(TerraMod).GetMethod("Drink", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                PlayerChallenges.DevResting = false;
                Equipment.Current = new Bonuses();
                foreach (var d in new[] { DifficultyLevel.Master, DifficultyLevel.Hollow })
                for (int bits = 0; bits < 16; bits++)
                {
                    GameDifficulty.Current = d;
                    PlayerChallenges.DevBindings = (BossSequenceController.ChallengeBindings)bits;
                    var st = new TerraStats { LifeCrystals = 15, LifeFruits = 20, Life = 1 };
                    st.UpdateMaximums(null, 40);
                    int expected = (bits & 2) != 0 ? 350 : d == DifficultyLevel.Hollow ? 425 : 500;
                    Check($"{d}/{bits}/life", st.LifeMax == expected);
                    Check($"{d}/{bits}/mana", st.ManaMax == 60);
                    var saved = PlayerChallenges.DevBindings;
                    PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.None;
                    int damage = EnemyStats.Strike(new EnemyStats.Info(), 100, false);
                    PlayerChallenges.DevBindings = saved;
                    Check($"{d}/{bits}/damage", EnemyStats.Strike(new EnemyStats.Info(), 100, false) == ((bits & 1) != 0 ? Mathf.Max(1, Mathf.RoundToInt(damage * .85f)) : damage));
                    Check($"{d}/{bits}/lifesteal", st.Heal(20, recovery: true) == ((bits & 8) != 0 ? 0 : 20));
                    float cooldown = PlayerChallenges.PotionCooldown(60);
                    Check($"{d}/{bits}/cooldown", Mathf.Abs(cooldown - (d == DifficultyLevel.Hollow ? 72 : 60)) < .01f);
                    mod.Stats.LifeMax = 500; mod.Stats.Life = 1; mod.Stats.PotionSickness = 0;
                    Check($"{d}/{bits}/drink", (bool)drink.Invoke(mod, new object[] { ItemDb.Get(ItemType.HealingPotion) })
                        && mod.Stats.Life == ((bits & 8) != 0 ? 71 : 101)
                        && Mathf.Abs(mod.Stats.PotionSickness - cooldown) < .01f);
                    Check($"{d}/{bits}/accessory", PlayerChallenges.AccessoriesLocked == ((bits & 4) != 0));
                    PlayerChallenges.DevResting = true;
                    Check($"{d}/{bits}/rest", st.Heal(20, recovery: true) == 20);
                    PlayerChallenges.DevResting = false;
                }
                GameDifficulty.Current = DifficultyLevel.Master;
                var inv = new Inventory();
                inv.Armor[0] = ItemStack.Of(ItemType.CobaltHelmet, 1);
                inv.Armor[1] = ItemStack.Of(ItemType.CobaltBreastplate, 1);
                inv.Armor[2] = ItemStack.Of(ItemType.CobaltLeggings, 1);
                inv.Accessories[0] = ItemStack.Of(ItemType.WormScarf, 1);
                inv.Accessories[1] = ItemStack.Of(ItemType.ShadeCloak, 1);
                PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.None;
                Equipment.Update(inv); var normal = Equipment.Current; var set = Equipment.FullSet;
                PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.Charms;
                Equipment.Update(inv);
                Check("armor-and-set-retained", Equipment.Current.Defense == normal.Defense && Equipment.FullSet == set && set != ArmorSet.None);
                Check("accessory-attributes-removed", normal.Endurance > 0 && Equipment.Current.Endurance == 0 && !inv.HasEquipped(AccessoryEffect.Passive));
                Check("dash-exception", PlayerChallenges.DashComponent(ItemType.ShadeCloak) && PlayerChallenges.DashComponent(ItemType.CrystalHeart) && !PlayerChallenges.DashComponent(ItemType.WormScarf));
                PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.None;
                Equipment.Update(inv);
                Check("accessory-restored", Equipment.Current.Endurance == normal.Endurance);
                inv.Accessories[2] = ItemStack.Of(ItemType.ShieldOfCthulhu, 1);
                PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.None;
                Equipment.Update(inv); int withShield = Equipment.Current.Defense;
                PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.Charms;
                Equipment.Update(inv);
                Check("shield-dash-retained-without-defense", Equipment.Current.DashKind == 2 && Equipment.Current.Defense == withShield - ItemDb.Get(ItemType.ShieldOfCthulhu).Defense);
                PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.None;

                Check("all-bindings-reward", Mathf.Abs(PlayerChallenges.CompletionShare((BossSequenceController.ChallengeBindings)15) - 2.5f) < .001f);
                for (int stage = 0; stage <= 5; stage++) for (int door = 1; door <= 5; door++)
                {
                    UnityEngine.Random.InitState(71);
                    var plain = PantheonRewards.Contents(door, stage);
                    UnityEngine.Random.InitState(71);
                    var bound = PantheonRewards.Contents(door, stage, (BossSequenceController.ChallengeBindings)15);
                    Check($"reward/{stage}/{door}", plain.Count == bound.Count && plain.Count > 0);
                    for (int i = 0; i < plain.Count; i++)
                        Check($"reward/{stage}/{door}/{i}", bound[i].Type == plain[i].Type && bound[i].Count >= plain[i].Count * 2
                            && !CraftOnlyLoot.Blocked(bound[i].Type) && ProgressionBalance.Unlocked(bound[i].Type, stage, false, false));
                }
                GameDifficulty.Current = DifficultyLevel.Master;
                var ai = new[] { EnemyAi.EnemyAi.LeadWeight, EnemyAi.EnemyAi.AnticScale, EnemyAi.EnemyAi.CooldownScale, EnemyAi.EnemyAi.ReachScale, EnemyAi.EnemyAi.AnimScale, EnemyAi.Bosses.BossAi.TempoScale, EnemyAi.Bosses.BossAi.AnimScale };
                GameDifficulty.Current = DifficultyLevel.Hollow;
                var hollowAi = new[] { EnemyAi.EnemyAi.LeadWeight, EnemyAi.EnemyAi.AnticScale, EnemyAi.EnemyAi.CooldownScale, EnemyAi.EnemyAi.ReachScale, EnemyAi.EnemyAi.AnimScale, EnemyAi.Bosses.BossAi.TempoScale, EnemyAi.Bosses.BossAi.AnimScale };
                for (int i=0;i<ai.Length;i++) Check("enemy-timing-retained/"+i, ai[i] == hollowAi[i]);
                Check("hollow-stagger-retained", !BossBalance.SuperArmour(new EnemyStats.Info { Boss = true }));
                Check("hollow-all-bindings-reward", Mathf.Abs(PlayerChallenges.HollowReward * PlayerChallenges.CompletionShare((BossSequenceController.ChallengeBindings)15) - 3.5f) < .001f);
                // Exercise the actual native sequence flags, not only the combination overrides.
                PlayerChallenges.DevBindings = null;
                PlayerChallenges.DevResting = null;
                PlayerData.instance.isInvincible = true;
                TerraStats.DevImmortal = true;
                mod.Stats.FullHeal();
                var sequence = Resources.LoadAll<BossSequence>("GG").First(s => s.name == "Boss Sequence Tier 1");
                BossSequenceController.SetupNewSequence(sequence, (BossSequenceController.ChallengeBindings)15, "bossDoorStateTier1");
                Check("outside-godhome-no-stale-binding", PlayerChallenges.Bindings == BossSequenceController.ChallengeBindings.None);
                yield return GoTo(sequence.GetBossScene(0).sceneName, "door_dreamEnter");
                yield return new WaitForSecondsRealtime(2f);
                Check("native-sequence-four-bindings", GameManager.UnsafeInstance.sceneName == sequence.GetBossScene(0).sceneName && (int)PlayerChallenges.Bindings == 15 && PlayerChallenges.LifeShare == .7f && PlayerChallenges.AccessoriesLocked);
                int mana = mod.Stats.ManaMax;
                mod.Stats.Mana = 0;
                Check("native-sequence-mana-unrestricted", mod.Stats.RestoreMana(mana) == mana);
                yield return GoTo("GG_Spa", "door_dreamEnter");
                yield return new WaitForSecondsRealtime(1f);
                Check("native-spa-rest-recovery", GameManager.UnsafeInstance.sceneName == "GG_Spa" && !PlayerChallenges.RecoveryLocked);
                BossSequenceController.RestoreBindings();
                BossSequenceController.Reset();
                BossSceneController.SetupEvent = null;
                yield return GoTo("Town", "left1");
                yield return new WaitForSecondsRealtime(1f);
                Check("native-exit-cleared", PlayerChallenges.Bindings == BossSequenceController.ChallengeBindings.None && !PlayerChallenges.AccessoriesLocked && PlayerChallenges.LifeShare == .85f);
            }
            finally
            {
                PlayerData.instance.isInvincible = invincible; TerraStats.DevImmortal = immortal;
                BossSequenceController.Reset(); BossSceneController.SetupEvent = null;
                GameDifficulty.Current = difficulty; PlayerChallenges.DevBindings = bindings; PlayerChallenges.DevResting = resting;
                UnityEngine.Random.state = random;
                for (int i = 0; i < fields.Length; i++) if (!fields[i].IsInitOnly) fields[i].SetValue(mod.Stats, values[i]);
                Equipment.Update(mod.Inventory);
            }
            Plugin.Log.LogInfo($"[AutoTest] PLAYER_CHALLENGES checks={checks} failures={failures} {(failures == 0 ? "通过" : "不通过")}");
        }
    }
}
