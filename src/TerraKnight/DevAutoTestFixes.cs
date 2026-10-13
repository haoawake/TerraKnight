using System.Collections;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fixes"): hand-placed terrain whose art goes with it, spikes dug out of
    /// Greenpath's walls no longer hurting, and Sheo's hut standing alone (its side exit leads to
    /// a door in the middle of Greenpath, which mustn't be laid over the hut).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FixesTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;

            // --- Floating terrain: its art goes once all of it is dug. The smallest pieces near the player. ---
            var terrain = TerrainSystem.Current;
            var pieces = new System.Collections.Generic.List<(string Name, Bounds Bounds)>(terrain.HandPlaced);
            pieces.Sort((a, b) => (a.Bounds.size.x * a.Bounds.size.y).CompareTo(b.Bounds.size.x * b.Bounds.size.y));
            int done = 0;
            foreach (var (name, b) in pieces)
            {
                if (done >= 3 || b.size.x > 8f || b.size.y > 8f || b.size.x < 0.4f) continue;
                var lo = CellGrid.WorldToCell(b.min);
                var hi = CellGrid.WorldToCell(b.max);
                int dug = 0;
                var near = new System.Collections.Generic.List<Renderer>();
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.enabled && r.gameObject.scene.name == scene && Mathf.Abs(r.bounds.center.z) < 2.5f && b.Contains(new Vector3(r.bounds.center.x, r.bounds.center.y, b.center.z)) && r.GetComponentInParent<TerraOwned>() == null)
                        near.Add(r);
                for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                        if (TerraWorld.CanDig(x, y)) { TerraWorld.Set(x, y, CellKind.Empty); dug++; }
                yield return new WaitForSeconds(0.2f);
                int off = 0;
                foreach (var r in near) if (r != null && !r.enabled) off++;
                Plugin.Log.LogInfo($"[AutoTest] 手放地形 '{name}'（{b.min:F1}..{b.max:F1}）：挖了 {dug} 格，上面的贴图 {near.Count} 个，关掉了 {off} 个");
                if (done == 0)
                {
                    hero.transform.position = Clear(hero, new Vector2(b.center.x, b.max.y + 1f));
                    yield return new WaitForSeconds(0.4f);
                    Shot(scene, "x1_hand_placed_dug");
                    yield return new WaitForSeconds(0.2f);
                }
                done++;
            }
            if (done == 0) Plugin.Log.LogInfo("[AutoTest] 这里没有小块的手放地形");

            // --- Spikes in Greenpath. ---
            yield return GoTo("Fungus1_09", "right1");
            hero = HeroController.instance;
            int tested = 0;
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
            {
                if (tested >= 2 || dh.hazardType != 2 || !dh.isActiveAndEnabled) continue;
                var col = dh.GetComponent<Collider2D>();
                if (col == null || !col.enabled) continue;
                var b = col.bounds;
                var lo = CellGrid.WorldToCell(b.min);
                var hi = CellGrid.WorldToCell(b.max);
                int solid = 0, empty = 0, dug = 0;
                for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                    {
                        if (TerraWorld.IsSolid(x, y)) solid++; else empty++;
                        if (TerraWorld.CanDig(x, y)) { TerraWorld.Set(x, y, CellKind.Empty); dug++; }
                    }
                // Thorns hanging in the open have no terrain to dig: they keep hurting.
                if (dug == 0) continue;
                tested++;
                Plugin.Log.LogInfo($"[AutoTest] 尖刺 '{dh.name}'（{col.GetType().Name}，{b.min:F1}..{b.max:F1}）：范围内实心格 {solid}、空格 {empty}，挖掉 {dug} 格");
                yield return new WaitForSeconds(0.3f);
                // The hero held still where it was dug, then touching it once (as the hero's box
                // reports a hazard): nothing else nearby can get in the way.
                var hrb = hero.GetComponent<Rigidbody2D>();
                bool wasKinematic = hrb.isKinematic;
                hrb.isKinematic = true;
                hrb.linearVelocity = Vector2.zero;
                Vector2 spot = b.center;
                for (float t = 0f; t <= 1f; t += 0.1f)
                {
                    var p = new Vector2(Mathf.Lerp(b.min.x, b.max.x, t), b.center.y);
                    var c = CellGrid.WorldToCell(p);
                    if (col.OverlapPoint(p) && TerraWorld.ArtCutAt(c.x, c.y)) { spot = p; break; }
                }
                hero.transform.position = new Vector3(spot.x, spot.y + 0.3f, Constants.HERO_Z);
                Physics2D.SyncTransforms();
                int life = mod.Stats.Life;
                hero.TakeDamage(dh.gameObject, GlobalEnums.CollisionSide.left, 1, 2);
                yield return new WaitForSeconds(0.2f);
                Plugin.Log.LogInfo($"[AutoTest] 在挖空的尖刺处碰它一下：生命 {life} -> {mod.Stats.Life}，尖刺死亡={hero.cState.hazardDeath}（应为 False）");
                if (tested == 1) Shot("Fungus1_09", "x2_dug_spikes");
                hrb.isKinematic = wasKinematic;
                // Let a hazard respawn finish before the next one.
                float wait = Time.time + 6f;
                while ((hero.cState.hazardDeath || hero.cState.hazardRespawning || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
                mod.Stats.FullHeal();
            }
            if (tested == 0) Plugin.Log.LogInfo("[AutoTest] Fungus1_09 里没找到尖刺");

            // --- Sheo's hut: nothing laid over it. ---
            yield return GoTo("Room_nailmaster_02", "left1");
            hero = HeroController.instance;
            yield return new WaitForSeconds(3f);
            var world = LiveWorld.Instance;
            Plugin.Log.LogInfo($"[AutoTest] 席奥的小屋：已载入 {world.LoadedCount} 个场景，苍绿之径 Fungus1_15 载入={world.IsLoaded("Fungus1_15")}（应为 False）");
            Shot("Room_nailmaster_02", "x3_sheo_hut");
            yield return new WaitForSeconds(0.2f);
            mod.DevNoFallDamage = false;
        }
    }
}
