using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossliquid"): in a Mawlek rematch, a row of platforms built over the Knight; every thrown thing of the
    /// fight (its spit too; not the room's scenery there before, its ceiling's water drips) goes through them (Physics2D
    /// ignores each pair), most of them from the moment they are spawned (BossPlatforms.Spawned).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossLiquidTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            // The boss rooms' pass-through is the new AI's (its fights).
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            const string key = "Mawlek";
            Bestiary.Kills[key] = Mathf.Max(1, Bestiary.KillsOf(key));
            int started = BossSummonStart.DevStarted;
            BossArena.Fight(key, true);
            float until = Time.realtimeSinceStartup + 25f;
            while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
            until = Time.realtimeSinceStartup + 15f;
            while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            var head = CellGrid.WorldToCell((Vector2)hero.transform.position + Vector2.up * 2f);
            for (int x = head.x - 10; x <= head.x + 10; x++)
                if (!TerraWorld.IsSolid(x, head.y)) TerraWorld.Set(x, head.y, CellKind.Platform);
            yield return new WaitForSeconds(0.5f);
            int spawned0 = BossPlatforms.DevSpawned;
            // The room's own scenery that was there before (the ceiling's water drips falling over and over) isn't thrown.
            var scenery = new HashSet<int>();
            foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None)) if (rb != null) scenery.Add(rb.gameObject.GetInstanceID());
            var seen = new HashSet<int>();
            var blocked = new HashSet<int>();
            var names = new HashSet<string>();
            var blockedNames = new HashSet<string>();
            var parts = new List<Collider2D>();
            for (float t = 0f; t < 20f; t += Time.deltaTime)
            {
                var platforms = ChunkColliders.AllPlatforms;
                foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                {
                    if (rb == null || !rb.simulated || rb.bodyType == RigidbodyType2D.Static || rb.gameObject == hero.gameObject) continue;
                    var go = rb.gameObject;
                    if (go.layer == ChunkColliders.TerrainLayer || go.GetComponent<HealthManager>() != null || !EnemyAi.Bosses.BossAi.OfFight(go)) continue;
                    if (scenery.Contains(go.GetInstanceID())) continue;
                    rb.GetAttachedColliders(parts);
                    if (parts.Count == 0) continue;
                    seen.Add(go.GetInstanceID());
                    names.Add(go.name);
                    foreach (var c in parts)
                        foreach (var p in platforms)
                            if (c != null && p != null && !Physics2D.GetIgnoreCollision(c, p)) { blocked.Add(go.GetInstanceID()); blockedNames.Add($"{go.name}（{go.scene.name}，{(c.isTrigger ? "触发器" : "实体")}，层 {go.layer}）"); }
                }
                yield return null;
            }
            int atSpawn = BossPlatforms.DevSpawned - spawned0;
            bool pass = seen.Count > 0 && blocked.Count == 0 && atSpawn > 0;
            Plugin.Log.LogInfo($"[AutoTest] 首领液体穿平台 看到 {seen.Count} 个投出物（{string.Join("、", names)}），被平台挡的 {blocked.Count} 个（{string.Join("、", blockedNames)}），生成时就穿过的 {atSpawn} 个 {(pass ? "通过" : "不通过")}");
        }
    }
}
