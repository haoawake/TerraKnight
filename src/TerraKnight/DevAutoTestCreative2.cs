using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "creative2"): creative's world. The whole map from the world atlas, all of it shown;
    /// every bench's pylon unlocked, and a teleport to one placing it by its bench; the monster book's
    /// spawn button: an enemy of the scene played and two from far-off scenes, ten tiles in front.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Creative2Test(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 创造2 地图：{UI.WorldMap.DevReport()}，图集 {UI.WorldMap.DevAtlasCount} 个场景；晶塔 {World.Pylons.Unlocked.Count} 座");
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevZoom(0.45f);
            yield return new WaitForSeconds(0.8f);
            Shot(GameManager.instance.sceneName, "creative2_map");
            yield return null;
            UI.WorldMap.DevZoom(3f);
            yield return new WaitForSeconds(0.5f);
            Shot(GameManager.instance.sceneName, "creative2_map_near");
            yield return null;
            UI.WorldMap.DevSetOpen(false);

            // The spawn button: here, and from scenes far off.
            foreach (var rec in new[] { "Crawler", "AcidFlyer", "BeeStinger" })
            {
                int before = BookSpawner.DevSpawned;
                BookSpawner.Spawn(rec);
                float t0 = Time.time;
                while (BookSpawner.DevSpawned == before && Time.time - t0 < 10f) yield return null;
                yield return new WaitForSeconds(1.5f);
                Plugin.Log.LogInfo($"[AutoTest] 创造2 生成 {rec}：{BookSpawner.DevLast}（主角 {HeroController.instance.transform.position:F1}）");
            }
            int copies = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm.name.EndsWith("(图鉴)")) copies++;
            Plugin.Log.LogInfo($"[AutoTest] 创造2 场景里图鉴生成的怪 {copies} 只（应为 3）");
            Shot(GameManager.instance.sceneName, "creative2_spawned");
            yield return null;

            // A pylon far off: the teleport, and its spot chosen by its bench there.
            World.Pylons.Pylon target = null;
            foreach (var p in World.Pylons.Unlocked) if (p.Scene == "Crossroads_30") target = p;
            if (target != null)
            {
                World.Pylons.Teleport(target);
                float t0 = Time.time;
                while ((GameManager.instance.sceneName != "Crossroads_30" || !GameHooks.PlayerHasControl()) && Time.time - t0 < 25f) yield return null;
                yield return new WaitForSeconds(2f);
                Plugin.Log.LogInfo($"[AutoTest] 创造2 传送到 Crossroads_30：场景 {GameManager.instance.sceneName}，主角 {HeroController.instance.transform.position:F1}，晶塔 待定 {target.Provisional}，位置 {target.Local:F1}");
                Shot(GameManager.instance.sceneName, "creative2_pylon");
                yield return null;
            }
        }
    }
}
