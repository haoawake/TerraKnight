using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_ggstatues", or "fx_ggstatues_Scene1.Scene2"; "fx_ggstatuesold_..." the same with Godhome's fights
    /// not counted as boss rooms, as before): the Hall of Gods' fights of the world's bosses, entered as a statue's
    /// challenge enters them (its boss scene set up at level 0, in by the dream door). Each room: a boss room or not
    /// (the game's own ground), then for 20 s, twice a second, each boss: inside the room's solid ground, or above
    /// its top. Screenshots of the first of each.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] GgStatueRooms =
        {
            "GG_Gruz_Mother", "GG_Vengefly", "GG_Brooding_Mawlek", "GG_False_Knight", "GG_Hornet_1", "GG_Mega_Moss_Charger",
            "GG_Flukemarm", "GG_Mantis_Lords", "GG_Hive_Knight", "GG_Broken_Vessel", "GG_Nosk", "GG_Collector", "GG_Crystal_Guardian",
            "GG_Crystal_Guardian_2", "GG_Uumuu", "GG_Traitor_Lord", "GG_Grey_Prince_Zote", "GG_Soul_Master", "GG_Mage_Knight",
            "GG_Watcher_Knights", "GG_Dung_Defender", "GG_White_Defender", "GG_Hornet_2", "GG_Grimm", "GG_Grimm_Nightmare",
            "GG_Failed_Champion", "GG_Soul_Tyrant", "GG_Lost_Kin", "GG_Ghost_Xero", "GG_Ghost_Marmu", "GG_Ghost_Galien",
            "GG_Ghost_Hu", "GG_Ghost_Gorb", "GG_Ghost_No_Eyes", "GG_Ghost_Markoth", "GG_Radiance",
        };

        IEnumerator GgStatuesTest(string only)
        {
            var rooms = only != null ? new List<string>(only.Split('.')) : new List<string>(GgStatueRooms);
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            mod.DevNoFallDamage = true;
            PlayerData.instance.hasDreamNail = true;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var gm = GameManager.instance;
            var bad = new List<string>();
            foreach (var room in rooms)
            {
                yield return SealTravel("Town", "left1");
                BossSceneController.SetupEvent = self => { self.BossLevel = 0; self.DreamReturnEvent = "DREAM RETURN"; };
                PlayerData.instance.dreamReturnScene = "Town";
                gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                {
                    SceneName = room, EntryGateName = "door_dreamEnter", HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                    EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.GodsAndGlory,
                });
                for (float until = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < until && (gm.sceneName != room || gm.IsInSceneTransition);) yield return null;
                if (gm.sceneName != room) { FxCheck($"{room} 进得去", false, $"在 {gm.sceneName}"); bad.Add(room + " 进不去"); continue; }
                yield return new WaitForSeconds(0.8f);
                gm.FadeSceneIn();
                yield return new WaitForSeconds(2.5f);
                var ts = TerrainSystem.Current;
                bool own = BossRooms.IsRoom(room) && ts != null && ts.SceneOwnGround;
                var region = ts?.Region;
                float top = region != null ? region.Root.position.y + region.Grid.Height * CellGrid.CellSize : float.MaxValue;
                int samples = 0, inWall = 0, inSky = 0;
                var sb = new StringBuilder();
                bool shot = false;
                for (float t = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < t && gm.sceneName == room;)
                {
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || !EnemyStats.Of(hm).Boss) continue;
                        var col = hm.GetComponent<Collider2D>();
                        if (col == null || !col.enabled) continue;
                        samples++;
                        var b = col.bounds;
                        bool wall = false;
                        foreach (var c in Physics2D.OverlapBoxAll(b.center, new Vector2(b.size.x / 3f, b.size.y / 3f), 0f, terrain))
                            if (c != null && !c.isTrigger && c.GetComponentInParent<HealthManager>() == null && !c.usedByEffector) { wall = true; break; }
                        bool sky = b.min.y > top + 2f;
                        if (wall) { inWall++; if (sb.Length < 400) sb.Append($" 墙里 {hm.name}@{(Vector2)b.center:F1}"); }
                        if (sky) { inSky++; if (sb.Length < 400) sb.Append($" 天上 {hm.name}@{(Vector2)b.center:F1}"); }
                        if ((wall || sky) && !shot) { shot = true; Player.TerraCamera.DevLook = b.center; yield return null; Shot(room, "fx_ggstatues_" + room); yield return null; Player.TerraCamera.DevLook = null; }
                    }
                    yield return new WaitForSeconds(0.5f);
                }
                bool ok = samples > 0 && inWall <= 2 && inSky == 0;
                if (!ok) bad.Add($"{room}（墙里 {inWall} 天上 {inSky}/{samples}）");
                FxCheck($"{room} 首领没卡进墙里也没飞到天上", ok, $"原版地面 {own}（{ts?.OwnZoneHow}），看了 {samples} 次，墙里 {inWall}，天上 {inSky}，房顶 {top:F1}：{sb}");
            }
            BossSceneController.SetupEvent = null;
            done = true;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 神居雕像 有问题的：{(bad.Count == 0 ? "无" : string.Join("、", bad))}");
            yield return SealTravel("Town", "left1");
        }
    }
}
