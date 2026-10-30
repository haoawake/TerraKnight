using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "flukelag", the user's 10-29 "吸虫之母门口还是很卡"): the room before the Flukemarm's door (Waterways_08: 85
    /// Fluke larvae, 14 Flukemen...) crossed by the player, a stop every 1.5 s across its open floor, with the new enemy AI
    /// and with the game's (TK_FLUKELAG_ROOMS for others): frame times and the profiler's top methods for each. Lines
    /// "[AutoTest] 卡顿 …" and "[剖析] …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FlukeLagTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            var pd = PlayerData.instance;
            Player.TerraStats.DevImmortal = true; mod.DevNoFallDamage = true;
            var rooms = (Environment.GetEnvironmentVariable("TK_FLUKELAG_ROOMS") ?? "Waterways_08").Split(';');
            foreach (string room in rooms)
                foreach (var kind in new[] { World.EnemyAiKind.New, World.EnemyAiKind.Vanilla })
                {
                    World.EnemyAiMode.Current = kind;
                    string label = $"{room}（{(kind == World.EnemyAiKind.New ? "新版AI" : "原版AI")}）穿过房间";
                    yield return GoTo("Town", "left1");
                    yield return GoTo(room, EntryGateOf(room));
                    pd.isInvincible = true;
                    yield return new WaitForSeconds(1f);
                    var region = TerrainSystem.Find(room);
                    if (region == null) { Plugin.Log.LogInfo($"[AutoTest] 卡顿 {room} 没有网格"); continue; }
                    // Open floor across the room: a cell open two tiles high with the room's rock right under it, every 6 tiles.
                    var stops = new List<Vector2>();
                    var cells = region.CellRect;
                    const float cs = CellGrid.CellSize;
                    for (int x = cells.xMin + 4; x < cells.xMax - 4; x += 12)
                        for (int y = cells.yMax - 6; y > cells.yMin + 1; y--)
                        {
                            if (TerraWorld.Get(x, y) != CellKind.Empty || TerraWorld.Get(x, y + 1) != CellKind.Empty || TerraWorld.Get(x, y + 3) != CellKind.Empty) continue;
                            if (TerraWorld.Get(x, y - 1) != CellKind.Original) continue;
                            stops.Add(new Vector2((x + 0.5f) * cs, (y + 2.4f) * cs));
                            break;
                        }
                    var hero = HeroController.instance;
                    var frames = new List<float>();
                    int gc0 = GC.CollectionCount(0);
                    DevFrameProfiler.Begin();
                    float until = Time.realtimeSinceStartup + 18f, next = 0f;
                    int at = 0;
                    while (Time.realtimeSinceStartup < until)
                    {
                        if (Time.realtimeSinceStartup >= next && stops.Count > 0)
                        {
                            next = Time.realtimeSinceStartup + 1.5f;
                            hero.transform.position = new Vector3(stops[at % stops.Count].x, stops[at % stops.Count].y, hero.transform.position.z);
                            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                            at++;
                        }
                        pd.isInvincible = true;
                        mod.Stats.FullHeal();
                        yield return null;
                        frames.Add(Time.unscaledDeltaTime);
                    }
                    string report = DevFrameProfiler.Report(label, frames.Count);
                    Plugin.Log.LogInfo($"[AutoTest] 卡顿 {label}（{stops.Count} 处停点）：{FrameStats(frames)}，GC {GC.CollectionCount(0) - gc0} 次");
                    Plugin.Log.LogInfo(report);
                    Plugin.Log.LogInfo($"[AutoTest] 卡顿 {label} 之后：{Census()}");
                }
        }
    }
}
