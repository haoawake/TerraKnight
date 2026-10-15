using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bosszones"): every world boss's room in turn. The part the boss fights in (BossArenaZone) and how
    /// much of the room it is; inside it nothing can be changed, outside it the ground digs like any room's; a
    /// photograph with the zone outlined in red, and one from its ground.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossZonesTest()
        {
            int ok = 0, all = 0, zoned = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 首领范围 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            mod.DevNoFallDamage = true;
            var seen = new HashSet<string>();
            foreach (var s in BossSummons.All)
            {
                if (!BossArena.DevWorldArena(s.Key, out string gate, out _)) continue;
                string scene = BossArena.SceneOf(s.Key);
                // The warriors' ghosts haunt ordinary rooms: locked only round the ghost while it is up.
                if (!seen.Add(scene) || !BossRooms.IsRoom(scene)) continue;
                yield return GoTo(scene, gate);
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(2f);
                var ts = TerrainSystem.Current;
                var r = ts?.Region;
                if (r == null || ts.SceneName != scene)
                {
                    Check(scene, false, $"没有地形（现在 {ts?.SceneName}）");
                    continue;
                }
                var room = new Rect(r.Root.position, new Vector2(r.Grid.Width, r.Grid.Height) * CellGrid.CellSize);
                var zone = r.OwnZone;
                if (zone == null)
                {
                    var c = CellGrid.WorldToCell(room.center);
                    Check(scene, BossRooms.Locked(c.x, c.y), $"整间保留（{ts.OwnZoneHow}）");
                    Shot(scene, "bz_" + scene);
                    yield return null;
                    continue;
                }
                zoned++;
                // Solid cells inside the zone (none may be dug) and ground outside it that can be.
                Vector2Int? inside = null, outside = null;
                int solidIn = 0, diggableIn = 0;
                for (int y = 0; y < r.Grid.Height; y++)
                for (int x = 0; x < r.Grid.Width; x++)
                {
                    if (r.Grid.Get(x, y) != CellKind.Original) continue;
                    int gx = x + r.CellOffset.x, gy = y + r.CellOffset.y;
                    var at = new Vector2((gx + 0.5f) * CellGrid.CellSize, (gy + 0.5f) * CellGrid.CellSize);
                    if (zone.Value.Contains(at))
                    {
                        solidIn++;
                        if (TerraWorld.CanDig(gx, gy)) diggableIn++;
                        if (inside == null && TerraWorld.Get(gx, gy + 1) == CellKind.Empty && TerraWorld.Get(gx, gy + 4) == CellKind.Empty) inside = new Vector2Int(gx, gy);
                    }
                    else if (outside == null && TerraWorld.CanDig(gx, gy) && TerraWorld.Get(gx, gy + 1) == CellKind.Empty) outside = new Vector2Int(gx, gy);
                }
                float share = zone.Value.width * zone.Value.height / (room.width * room.height);
                var mid = CellGrid.WorldToCell(zone.Value.center);
                bool inLocked = BossRooms.Locked(mid.x, mid.y) && solidIn > 0 && diggableIn == 0;
                // A zone over most of the room's ground may leave none outside it to dig.
                Check(scene, inLocked && (outside != null || share > 0.5f),
                      $"战斗范围 {zone.Value.xMin:F0}~{zone.Value.xMax:F0} × {zone.Value.yMin:F0}~{zone.Value.yMax:F0}（{ts.OwnZoneHow}），房间 {room.width:F0}×{room.height:F0}，占 {share:P0}；" +
                      $"范围内实心 {solidIn} 格、可挖 {diggableIn} 格，地面 {inside}；范围外可挖的地面 {outside}");
                // The zone outlined in red for the photographs.
                var line = new GameObject("dev zone").AddComponent<LineRenderer>();
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = Color.red;
                line.startWidth = line.endWidth = 0.15f;
                line.positionCount = 5;
                line.sortingOrder = 32000;
                var zr = zone.Value;
                line.SetPositions(new[] { new Vector3(zr.xMin, zr.yMin, -1f), new Vector3(zr.xMax, zr.yMin, -1f), new Vector3(zr.xMax, zr.yMax, -1f), new Vector3(zr.xMin, zr.yMax, -1f), new Vector3(zr.xMin, zr.yMin, -1f) });
                Shot(scene, "bz_" + scene);
                yield return null;
                if (inside != null)
                {
                    var hero = HeroController.instance;
                    hero.transform.position = new Vector3((inside.Value.x + 0.5f) * CellGrid.CellSize, (inside.Value.y + 3f) * CellGrid.CellSize, hero.transform.position.z);
                    yield return new WaitForSeconds(0.8f);
                    Shot(scene, "bz_" + scene + "_in");
                    yield return null;
                }
                Object.Destroy(line.gameObject);
            }
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 首领范围 合计 {ok}/{all}，划出战斗范围的 {zoned} 间，裁出原版轮廓 {TerrainSystem.DevClipped} 段 {(ok == all ? "通过" : "不通过")}");
        }
    }
}
