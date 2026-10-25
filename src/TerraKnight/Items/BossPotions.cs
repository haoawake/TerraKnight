using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>Terraria's heart (item 58): picked up, it heals 20 life at once and is never carried (WorldItems).</summary>
        static void AddHeart(List<ItemDef> list)
        {
            list.Add(new ItemDef { Type = ItemType.Heart, Name = "心", Texture = "Item_58", MaxStack = 9999, Tooltip = "拾取后恢复20点生命", UseSound = null });
            AddManaStar(list);
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> HeartSources()
        {
            yield return (ItemType.Heart, 0, ItemSource.Drop, 0);
            yield return (ItemType.Star, 0, ItemSource.Drop, 0);
        }
    }

    internal static partial class EnemyDrops
    {
        /// <summary>
        /// Every boss's table ends with its healing (NPC.DoDeathEvents_DropBossPotionsAndHearts): 5 to 15 of its stage's
        /// potion and 5 to 9 hearts, a stage 5 boss's 1 to 3 Ultimate Healing Potions too, sure, in the monster book with the
        /// rest of its drops.
        /// </summary>
        static void AddBossHealing()
        {
            foreach (var key in new List<string>(table.Keys))
            {
                var e = table[key];
                if (!e.Boss) continue;
                int at = key.IndexOf('@');
                string record = at > 0 ? key.Substring(0, at) : key;
                var stand = BossStats.Of(record, at > 0 && int.TryParse(key.Substring(at + 1), out int stage) ? stage : e.Stage);
                if (stand == null) continue;
                var rows = new List<Drop>(e.Drops ?? none)
                {
                    C(BossStats.PotionOf(stand), 1, 5, 15),
                    C(ItemType.Heart, 1, 5, 9),
                };
                if (BossStats.UltimatePotion(stand)) rows.Add(C(ItemType.UltimateHealingPotion, 1, 1, 3));
                e.Drops = rows.ToArray();
                table[key] = e;
                DevHealingRows++;
            }
        }

        /// <summary>Dev: the boss tables given their healing.</summary>
        public static int DevHealingRows;
    }

    /// <summary>
    /// A boss's healing. At its death, its table's (<see cref="EnemyDrops"/>: 5 to 15 of its potion, 5 to 9 hearts). At
    /// the end of each of its phases but the last (<see cref="BossPhases"/>) a third of that, for the next one. The
    /// potion by the boss's stage (<see cref="BossStats.PotionOf"/>). Each heart drops on its
    /// own (Item.NewItem one by one); where the game takes the player away after the fight (the dreams, Godhome) the
    /// potions go into the bag and the hearts heal at once.
    /// </summary>
    internal static class BossPotions
    {
        public static int DevPhases, DevHearts;
        public static ItemType DevLastPotion;

        /// <summary>At the end of a phase with another to come.</summary>
        public static void AtPhaseEnd(EnemyStats.Info info, Vector2 at)
        {
            if (info == null || info.Ref == null) return;
            DevPhases++;
            // Shared in a LAN game, from the game that runs the boss (the puppet's copy crosses the phase too).
            EnemyDrops.LootScene = info.Scene;
            bool direct = EnemyDrops.LeavesOnItsOwn(info.Scene);
            DevLastPotion = BossStats.PotionOf(info.Ref);
            if(!CraftOnlyLoot.Blocked(DevLastPotion)
                && (!ProgressionLoot.GodhomeScene(info.Scene) || ProgressionBalance.Unlocked(DevLastPotion)))
                EnemyDrops.Spill(ItemStack.Of(DevLastPotion, Random.Range(2, 6)), at, direct);
            Hearts(Random.Range(2, 4), at, direct);
        }

        /// <summary>Hearts from a boss: each its own in the world, or their healing at once where there's no time to pick them up.</summary>
        public static void Hearts(int n, Vector2 at, bool direct)
        {
            if (n <= 0) return;
            DevHearts += n;
            if (direct)
            {
                var mod = TerraMod.Instance;
                var hero = HeroController.SilentInstance;
                if (mod == null || hero == null) return;
                mod.Stats.Heal(20 * n);
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, (20 * n).ToString(), UI.CombatText.Heal);
                return;
            }
            for (int i = 0; i < n; i++) EnemyDrops.Spill(ItemStack.Of(ItemType.Heart, 1), at, false);
        }
    }
}
