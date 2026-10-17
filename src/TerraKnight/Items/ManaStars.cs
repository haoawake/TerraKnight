using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>Terraria's star (item 184): picked up, it gives 100 mana at once and is never carried (WorldItems).</summary>
        static void AddManaStar(List<ItemDef> list) =>
            list.Add(new ItemDef { Type = ItemType.Star, Name = "星星", Texture = "Item_184", MaxStack = 9999, Tooltip = "拾取后恢复100点魔力", UseSound = null });
    }

    /// <summary>
    /// NPC.NPCLoot_DropCommonLifeAndMana: every enemy that can hurt the player (damage &gt; 0, lifeMax &gt; 1) may leave
    /// stars when it dies, for the nearest player while his mana is short of full: one roll in 6 (RollLuck) gives a
    /// star half the time, and a second roll in 2 (RollLuck) gives another. The hearts of that roll are in the enemies'
    /// drop tables. A kill in a scene the game leaves by itself (a dream, Godhome) gives the mana straight away.
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal static class ManaStars
    {
        public const int ManaPerStar = 100;

        /// <summary>Dev: stars dropped so far (and given straight away); off: none.</summary>
        public static int DevDropped, DevGiven;
        public static bool Enabled = true;

        /// <summary>Luck.RollLuck: good luck may roll in a shorter range, bad luck in a longer one.</summary>
        static int RollLuck(int range)
        {
            float luck = Equipment.Current.Luck;
            if (luck > 0f && Random.value < luck) return Random.Range(0, Random.Range(range / 2, range));
            if (luck < 0f && Random.value < -luck) return Random.Range(0, Random.Range(range, range * 2));
            return Random.Range(0, range);
        }

        static bool CanHurt(HealthManager hm)
        {
            foreach (var dh in hm.GetComponentsInChildren<DamageHero>(true))
                if (dh.damageDealt > 0) return true;
            return false;
        }

        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void OnDeath(HealthManager __instance)
        {
            var mod = TerraMod.Instance;
            if (!Enabled || __instance == null || __instance.isDead || mod == null || HeroController.SilentInstance == null) return;
            string rec = UI.EnemyHover.RecordOf(__instance);
            if (rec == null) return;
            // A LAN game: the stars are the world's, dropped by the game that runs the enemy, not by a puppet's copy of it.
            if (Net.NetSession.Active && Net.Enemies.RemoteOwned(__instance.gameObject.scene.name)) return;
            string kind = EnemyDrops.KindOf(rec);
            if (kind == "non_enemy" || kind == "invulnerable") return;
            var info = EnemyStats.Of(__instance);
            if (info == null || EnemyStats.LifeMax(info) <= 1 || !CanHurt(__instance)) return;
            int stars = Roll(mod.Stats);
            if (stars == 0) return;
            var col = __instance.GetComponent<Collider2D>();
            Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)__instance.transform.position;
            if (EnemyDrops.LeavesOnItsOwn(__instance.gameObject.scene.name))
            {
                Give(stars);
                return;
            }
            for (int i = 0; i < stars; i++) WorldItems.Drop(ItemStack.Of(ItemType.Star, 1), at);
            DevDropped += stars;
        }

        /// <summary>The stars one kill leaves (0 to 2).</summary>
        static int Roll(Player.TerraStats stats)
        {
            int stars = 0;
            if (RollLuck(6) == 0 && Random.Range(0, 2) == 0 && stats.Mana < stats.ManaMax) stars++;
            if (RollLuck(2) == 0 && stats.Mana < stats.ManaMax) stars++;
            return stars;
        }

        /// <summary>Dev: the stars of so many kills' rolls with the player's mana as it is now.</summary>
        public static int DevRoll(int kills)
        {
            int n = 0;
            for (int i = 0; i < kills; i++) n += Roll(TerraMod.Instance.Stats);
            return n;
        }

        /// <summary>Player.PickupItem's star: 100 mana each (ManaEffect shows the full amount), up to the most there is.</summary>
        public static void Give(int stars)
        {
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || stars <= 0) return;
            mod.Stats.RestoreMana(ManaPerStar * stars);
            if (hero != null) UI.CombatText.Show(hero.GetComponent<BoxCollider2D>().bounds.center + Vector3.up * 0.9f, (ManaPerStar * stars).ToString(), UI.CombatText.Mana);
            mod.PlaySound("Grab", 0.7f);
            DevGiven += stars;
        }
    }
}
