using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hollow Knight's bosses and elites were made for the floors of their arenas, and the platforms
    /// a player builds there (Terraria's arena) catch them: a boss that lands on one, or is wedged
    /// under one, can't get at the player any more. Terraria's bosses mostly go through blocks; here
    /// bosses and elites go through the player's platforms, and still stand on the game's own floors.
    /// </summary>
    internal static class BossPlatforms
    {
        static float next;
        /// <summary>Dev: boss colliders told to go through the platforms at the last look.</summary>
        public static int DevColliders;

        /// <summary>A few times a second: every live boss's and elite's solid colliders against every platform built.</summary>
        public static void Tick()
        {
            if (Time.time < next) return;
            next = Time.time + 0.5f;
            var platforms = ChunkColliders.AllPlatforms;
            platforms.RemoveAll(p => p == null);
            if (platforms.Count == 0) return;
            int n = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead || !Items.EnemyStats.Unstoppable(Items.EnemyStats.Of(hm))) continue;
                foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                {
                    if (c.isTrigger) continue;
                    n++;
                    // Again each time: an ignore is lost when either collider is switched off and on.
                    foreach (var p in platforms) Physics2D.IgnoreCollision(c, p, true);
                }
            }
            DevColliders = n;
        }
    }
}
