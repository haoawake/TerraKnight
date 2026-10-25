using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hollow Knight's bosses and elites were made for the floors of their arenas, and the platforms
    /// a player builds there (Terraria's arena) catch them: a boss that lands on one, or is wedged
    /// under one, can't get at the player any more. Terraria's bosses mostly go through blocks; here
    /// bosses and elites go through the player's platforms, and still stand on the game's own floors.
    /// With the new enemy AI (EnemyAi.Bosses) a boss's room goes further: the bosses, their summons and what
    /// they throw go through every block and platform the player built there (ChunkColliders.Built, the
    /// platforms), their solid parts and their triggers alike (a shot that goes at a wall's touch never feels
    /// the player's), the liquids (a Mawlek's spit, infection, dung) too: they come down on the room's own floor. What
    /// comes out of the pool in a fight goes through them at once (Spawned), not at the next look.
    /// </summary>
    [HarmonyPatch]
    internal static class BossPlatforms
    {
        static float next;
        /// <summary>Dev: boss colliders told to go through the platforms at the last look.</summary>
        public static int DevColliders;
        /// <summary>Dev: a boss fight's other colliders (summons, thrown things) told to go through the player's blocks.</summary>
        public static int DevThrown;

        /// <summary>A few times a second: every live boss's and elite's solid colliders against every platform built.</summary>
        public static void Tick()
        {
            if (Time.time < next) return;
            bool fights = EnemyAi.Bosses.BossAi.AnyFight;
            next = Time.time + (fights ? 0.2f : 0.5f);
            var platforms = ChunkColliders.AllPlatforms;
            platforms.RemoveAll(p => p == null);
            var built = ChunkColliders.AllBuilt;
            built.RemoveAll(p => p == null);
            if (platforms.Count == 0 && (!fights || built.Count == 0)) return;
            int n = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead) continue;
                bool boss = Items.EnemyStats.Unstoppable(Items.EnemyStats.Of(hm));
                bool inFight = fights && EnemyAi.Bosses.BossAi.OfFight(hm.gameObject);
                if (!boss && !inFight) continue;
                foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                {
                    // Its triggers too in a fight (the sensors some bosses turn at a wall with).
                    if (c.isTrigger && !inFight) continue;
                    n++;
                    // Again each time: an ignore is lost when either collider is switched off and on.
                    if (boss) foreach (var p in platforms) Physics2D.IgnoreCollision(c, p, true);
                    if (inFight) foreach (var b in built) Physics2D.IgnoreCollision(c, b, true);
                }
            }
            DevColliders = n;
            if (fights && (built.Count > 0 || platforms.Count > 0)) Thrown(built, platforms);
        }

        /// <summary>
        /// A boss fight's thrown things (anything with a body there but the player's and the ground's): through the player's
        /// blocks and platforms, a trigger as much as a solid part (a shockwave, a spit that ends at a wall's touch).
        /// </summary>
        static void Thrown(List<CompositeCollider2D> built, List<CompositeCollider2D> platforms)
        {
            int n = 0;
            var hero = HeroController.SilentInstance;
            foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
            {
                if (rb == null || !rb.simulated || rb.bodyType == RigidbodyType2D.Static) continue;
                var go = rb.gameObject;
                if (go.layer == ChunkColliders.TerrainLayer || hero != null && go == hero.gameObject) continue;
                if (!EnemyAi.Bosses.BossAi.OfFight(go) || go.GetComponent<HealthManager>() != null) continue;
                n += Through(rb, built, platforms);
            }
            DevThrown = n;
        }

        /// <summary>A body's colliders told to go through the player's blocks and platforms; how many.</summary>
        static int Through(Rigidbody2D rb, List<CompositeCollider2D> built, List<CompositeCollider2D> platforms)
        {
            int n = 0;
            rb.GetAttachedColliders(attached);
            foreach (var c in attached)
            {
                if (c == null) continue;
                n++;
                foreach (var b in built) if (b != null) Physics2D.IgnoreCollision(c, b, true);
                foreach (var p in platforms) if (p != null) Physics2D.IgnoreCollision(c, p, true);
            }
            return n;
        }

        static readonly List<Collider2D> attached = new List<Collider2D>();
        public static int DevSpawned;

        /// <summary>A boss fight's thrown thing as it comes out of the pool (a Mawlek's spit, a shot): through the player's blocks before its first step.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectPool), nameof(ObjectPool.Spawn), typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion))]
        static void Spawned(GameObject __result)
        {
            try
            {
                if (__result == null || !EnemyAi.Bosses.BossAi.AnyFight) return;
                var built = ChunkColliders.AllBuilt;
                var platforms = ChunkColliders.AllPlatforms;
                if (built.Count == 0 && platforms.Count == 0) return;
                var rb = __result.GetComponent<Rigidbody2D>();
                if (rb == null || !rb.simulated || rb.bodyType == RigidbodyType2D.Static) return;
                if (__result.layer == ChunkColliders.TerrainLayer || __result.GetComponent<HealthManager>() != null || !EnemyAi.Bosses.BossAi.OfFight(__result)) return;
                if (Through(rb, built, platforms) > 0) DevSpawned++;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[首领平台] 生成时穿过玩家方块出错：{e.Message}");
            }
        }
    }
}
