using System.Collections;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossConstructionTest()
        {
            int checks = 0, failed = 0;
            void Check(string label, bool pass)
            {
                checks++; if (!pass) failed++;
                Plugin.Log.LogInfo($"[AutoTest] BossBuild {label} {(pass ? "通过" : "不通过")}");
            }
            bool smart = mod.SmartCursor;
            mod.DevSetSmartCursor(false);
            Player.TerraStats.DevImmortal = true;
            try
            {
                foreach (var scene in new[] { "Fungus1_04", "Fungus2_15" })
                {
                    yield return SealTravel(scene, scene == "Fungus1_04" ? "right1" : "top1");
                    var r = TerraWorld.Live;
                    var hero = HeroController.instance;
                    var rb = hero.GetComponent<Rigidbody2D>();
                    var oldConstraints = rb.constraints;
                    rb.constraints = RigidbodyConstraints2D.FreezeAll;
                    try
                    {
                        Check(scene + " native arena and active building root", r != null && r.SceneOwnGround && r.Root.gameObject.activeInHierarchy);
                        if (r == null) continue;
                        Vector2Int? native = null, spot = null;
                        for (int y = 5; y < r.Grid.Height - 9; y++)
                        for (int x = 5; x < r.Grid.Width - 9; x++)
                        {
                            var c = new Vector2Int(x, y) + r.CellOffset;
                            if (!BossRooms.Locked(c.x, c.y)) continue;
                            if (!native.HasValue && r.Grid.Get(x, y) == CellKind.Original) native = c;
                            if (spot.HasValue || r.Grid.Get(x, y) != CellKind.Empty) continue;
                            bool free = true;
                            for (int dx = 0; dx < 7 && free; dx++)
                            for (int dy = 0; dy < 6; dy++)
                                if (r.Grid.Get(x + dx, y + dy) != CellKind.Empty || !BossRooms.Locked(c.x + dx, c.y + dy)) { free = false; break; }
                            if (free && Physics2D.OverlapBox(CellGrid.CellCenter(c.x + 3, c.y + 2), new Vector2(3.3f, 2.8f), 0, 1 << ChunkColliders.TerrainLayer) == null) spot = c;
                        }
                        Check(scene + " locked native terrain found", native.HasValue);
                        if (r.OwnZone.HasValue)
                        {
                            Vector2Int? outside = null;
                            for (int y = 0; y < r.Grid.Height && !outside.HasValue; y++)
                            for (int x = 0; x < r.Grid.Width; x++)
                            {
                                var c = new Vector2Int(x, y) + r.CellOffset;
                                if (r.Grid.Get(x, y) == CellKind.Original && !BossRooms.Locked(c.x, c.y)) { outside = c; break; }
                            }
                            Check(scene + " native room outside combat zone also protected", outside.HasValue && BossRooms.ProtectsTerrain(outside.Value.x, outside.Value.y));
                        }
                        Check(scene + " open space in locked arena found", spot.HasValue);
                        if (!native.HasValue || !spot.HasValue) continue;
                        var n = native.Value;
                        Check(scene + " native mining refused", !TerraWorld.CanDig(n.x, n.y));
                        var pick = ItemDb.Get(ItemType.Picksaw);
                        for (int i = 0; i < 10; i++) mod.Mine(pick, n);
                        Check(scene + " repeated pickaxe preserves native ground", TerraWorld.Get(n.x, n.y) == CellKind.Original);
                        AccessTools.Method(typeof(TerraProjectiles), "BlastTiles").Invoke(null, new object[] { CellGrid.CellCenter(n.x, n.y), 2 });
                        Check(scene + " explosion preserves native ground", TerraWorld.Get(n.x, n.y) == CellKind.Original);

                        var at = spot.Value + new Vector2Int(2, 1);
                        for (int dx = -1; dx <= 4; dx++) TerraWorld.Set(at.x + dx, at.y - 1, CellKind.Wood);
                        hero.transform.position = CellGrid.CellCenter(at.x - 2, at.y + 1);
                        rb.position = hero.transform.position;
                        rb.linearVelocity = Vector2.zero;
                        Physics2D.SyncTransforms();
                        Check(scene + " block placement allowed", TerraMod.CanPlace(at, CellKind.Stone));
                        mod.DevUse(ItemType.StoneBlock, CellGrid.CellCenter(at.x, at.y));
                        yield return null;
                        r.Tick(); Physics2D.SyncTransforms();
                        Check(scene + " real item places block", TerraWorld.Get(at.x, at.y) == CellKind.Stone);
                        var point = CellGrid.CellCenter(at.x, at.y);
                        Check(scene + " placed block rendered", r.Root.GetComponentsInChildren<SpriteRenderer>().Any(s => s.enabled && s.sprite != null && Vector2.Distance(s.transform.position, point) < 0.02f));
                        Check(scene + " placed block has physical collision", Physics2D.RaycastAll(point + Vector2.up, Vector2.down, 1.5f, 1 << ChunkColliders.TerrainLayer).Any(h => h.collider.transform.IsChildOf(r.Root)));
                        rb.constraints = oldConstraints;
                        hero.RegainControl();
                        hero.transform.position = point + Vector2.up * 1.5f;
                        rb.position = hero.transform.position;
                        rb.linearVelocity = Vector2.down;
                        yield return new WaitForSeconds(1f);
                        Check(scene + " player actually stands on built block", Mathf.Abs(hero.GetComponent<BoxCollider2D>().bounds.min.y - (point.y + CellGrid.CellSize * 0.5f)) < 0.12f);
                        rb.constraints = RigidbodyConstraints2D.FreezeAll;
                        hero.transform.position = CellGrid.CellCenter(at.x - 2, at.y + 1);
                        rb.position = hero.transform.position;
                        Physics2D.SyncTransforms();
                        Check(scene + " player block removable", TerraWorld.CanDig(at.x, at.y));
                        for (int i = 0; i < 10; i++) mod.Mine(pick, at);
                        Check(scene + " player block reclaimed", TerraWorld.Get(at.x, at.y) == CellKind.Empty);
                        yield return null;
                        r.Tick(); Physics2D.SyncTransforms();

                        Check(scene + " wall placement allowed", BuildingTools.CanPlaceWall(at));
                        mod.DevUse(ItemType.WoodWall, point);
                        Check(scene + " player wall placed and removable", TerraWorld.GetWall(at.x, at.y) > 0 && BuildingTools.CanHammerWall(at));
                        var hammer = ItemDb.Get(ItemType.WoodenHammer);
                        for (int i = 0; i < 10; i++) BuildingTools.Hammer(hammer, at, true);
                        Check(scene + " player wall reclaimed", !TerraWorld.HasWall(at.x, at.y));

                        int station0 = Stations.DevPlaced, chest0 = Chests.DevPlaced;
                        bool placedBench = Stations.TryPlace(StationKind.WorkBench, hero, point);
                        Check(scene + " workbench placement " + Stations.DevWhy, placedBench);
                        Check(scene + " workbench counted", Stations.DevPlaced > station0);
                        yield return null;
                        var station = Stations.DevBoxNear(point);
                        for (int i = 0; i < 10; i++) Stations.TryMine(pick, station.center, hero);
                        Check(scene + " chest placement", Chests.TryPlace(hero, point));
                        Check(scene + " chest counted", Chests.DevPlaced > chest0);
                        Check(scene + " native terrain still protected after construction", TerraWorld.Get(n.x, n.y) == CellKind.Original && !TerraWorld.CanDig(n.x, n.y));
                        Plugin.Log.LogInfo($"[AutoTest] BossBuild {scene} zone={(r.OwnZone.HasValue ? "partial" : "whole")} boxes={r.BoxCount}");
                    }
                    finally { rb.constraints = oldConstraints; }
                }
            }
            finally
            {
                mod.DevSetSmartCursor(smart);
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] BossBuild total={checks} failed={failed} {(failed == 0 ? "通过" : "不通过")}");
        }
    }
}
