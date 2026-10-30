using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A shield held one way (MeleeHits.IsShield: Husk Sentries, Husk Warriors, Shielded Fools...) gives way for a while once
    /// it has stopped as much Terraria damage as its bearer's whole life (the user's 10-29: "射手完全打不了带盾的敌人，当盾受到1倍
    /// 敌人血量的伤害后能够暂时碎裂"): for <see cref="BrokenFor"/> seconds every hit gets through from every side (the game's
    /// own block check answers no), then the shield holds again and counts from nothing. All-round invincibility (an enemy
    /// curled up or hiding) isn't a shield and never breaks.
    /// </summary>
    internal static class ShieldBreak
    {
        public const float BrokenFor = 6f;

        /// <summary>The damage each shield has stopped since it last broke, and until when a broken one stays down.</summary>
        static readonly Dictionary<int, float> taken = new Dictionary<int, float>();
        static readonly Dictionary<int, float> brokenUntil = new Dictionary<int, float>();
        static readonly List<int> expired = new List<int>();

        /// <summary>Dev: shields broken.</summary>
        public static int DevBroken;
        /// <summary>Dev: the damage a shield has stopped since it last broke.</summary>
        public static float DevTaken(HealthManager hm) => hm != null && taken.TryGetValue(hm.GetInstanceID(), out float t) ? t : 0f;

        public static bool Broken(HealthManager hm) =>
            hm != null && brokenUntil.TryGetValue(hm.GetInstanceID(), out float until) && Time.time < until;

        /// <summary>A blocked hit's Terraria damage counted on the enemy's shield; true when this one broke it.</summary>
        public static bool Take(HealthManager hm, EnemyStats.Info info, int damage)
        {
            if (info == null || !MeleeHits.IsShield(hm)) return false;
            int id = hm.GetInstanceID();
            float sum = (taken.TryGetValue(id, out float before) ? before : 0f) + damage;
            if (sum < EnemyStats.LifeMax(info))
            {
                taken[id] = sum;
                return false;
            }
            taken.Remove(id);
            if (brokenUntil.Count > 64)
            {
                expired.Clear();
                foreach (var kv in brokenUntil) if (kv.Value < Time.time) expired.Add(kv.Key);
                foreach (int k in expired) brokenUntil.Remove(k);
            }
            brokenUntil[id] = Time.time + BrokenFor;
            DevBroken++;
            return true;
        }

        /// <summary>A broken shield blocks nothing, the game's own checks (the nail, its spells, every Terraria hit) included.</summary>
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.IsBlockingByDirection))]
        static class Down
        {
            static bool Prefix(HealthManager __instance, ref bool __result)
            {
                if (!Broken(__instance)) return true;
                __result = false;
                return false;
            }
        }
    }
}
