using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "worldmap"): Terraria's world map. King's Pass walked through and Dirtmouth after
    /// it (the cells revealed round the player); the names under the cursor at Dirtmouth's bench, a
    /// door, a lamp and the ground; the map near and far, with the label shown; Howling Cliffs
    /// joined on; the map's save written and read back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Visit(HeroController hero, params Vector2[] spots)
        {
            foreach (var s in spots)
            {
                hero.transform.position = Clear(hero, s);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(0.7f);
            }
        }

        IEnumerator MapShot(string name, float zoom, Vector2? centre = null, Vector2? pointAt = null)
        {
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevZoom(zoom);
            if (centre != null) UI.WorldMap.DevCentre(centre.Value);
            UI.WorldMap.DevPointAt(pointAt);
            yield return new WaitForSeconds(0.5f);
            Shot(GameManager.instance.sceneName, "wm_" + name);
            yield return null;
            yield return null;
            UI.WorldMap.DevPointAt(null);
            UI.WorldMap.DevSetOpen(false);
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator WorldMapTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 刚进山道：{UI.WorldMap.DevReport()}");
            yield return Visit(hero, new Vector2(60f, 11.5f), new Vector2(100f, 11.5f), new Vector2(140f, 11.5f), new Vector2(180f, 21.5f), new Vector2(30f, 11.5f), new Vector2(5f, 30f), new Vector2(5f, 55f), new Vector2(37f, 11.5f));
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 走完山道：{UI.WorldMap.DevReport()}；山道里的东西：{UI.WorldMap.DevThings("Tutorial_01")}");
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 脚下：{UI.WorldMap.DevNameAtWorld((Vector2)hero.transform.position + Vector2.down * 1.8f)}，身边：{UI.WorldMap.DevNameAtWorld(hero.transform.position)}");
            yield return MapShot("kingspass", 6f);

            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2.5f);
            yield return Visit(hero, new Vector2(20f, 15f), new Vector2(60f, 15f), new Vector2(100f, 15f), new Vector2(140f, 15f), new Vector2(180f, 15f), new Vector2(220f, 15f), new Vector2(250f, 15f), new Vector2(120f, 15f));
            yield return new WaitForSeconds(1f);
            var camNow = GameCameras.instance.mainCamera.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 镜头 德特茅斯：镜头偏离主角 {Player.TerraCamera.DevOffset:F2}，镜头 y {camNow.y:F2}，主角 y {hero.transform.position.y:F2}");
            Shot("Town", "camera_town");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 走完德特茅斯：{UI.WorldMap.DevReport()}；德特茅斯的东西：{UI.WorldMap.DevThings("Town")}");
            Vector2? bench = null, door = null, lamp = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Bench Control" && f.gameObject.scene.name == "Town") bench = f.transform.position;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.name == "door_sly" && tp.gameObject.scene.name == "Town") door = tp.GetComponent<Collider2D>().bounds.center;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Lamp_Bug") && t.gameObject.scene.name == "Town") { lamp = t.position; break; }
            string Name(Vector2? p) => p == null ? "没找到" : $"{p.Value:F1} → {UI.WorldMap.DevNameAtWorld(p.Value)}";
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 指着：长椅 {Name(bench)}；斯莱的店门 {Name(door)}；路灯 {Name(lamp)}；主角脚下 {Name((Vector2)hero.transform.position + Vector2.down * 1.8f)}");
            yield return MapShot("town", 6f, null, bench);
            yield return MapShot("town_door", 10f, door, door);
            yield return MapShot("town_far", 1.2f);
            // The corner minimap and the overlay (Tab's styles), then the minimap zoomed in.
            UI.WorldMap.DevStyle(1);
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 小地图：{UI.WorldMap.DevMini()}；光标：{UI.TerraCursor.DevInfo()}");
            Shot("Town", "wm_mini");
            yield return null;
            UI.WorldMap.DevMiniZoom(2.5f);
            yield return new WaitForSeconds(0.6f);
            Shot("Town", "wm_mini_zoom");
            yield return null;
            UI.WorldMap.DevMiniZoom(1.05f);
            UI.WorldMap.DevStyle(2);
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 覆盖：{UI.WorldMap.DevMini()}");
            Shot("Town", "wm_overlay");
            yield return null;
            UI.WorldMap.DevStyle(1);

            // Howling Cliffs on, through King's Pass's top.
            yield return GoTo("Cliffs_02", "right1");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2.5f);
            yield return Visit(hero, new Vector2(190f, 30f), new Vector2(165f, 13.5f), new Vector2(120f, 31.4f), new Vector2(95f, 6.5f), new Vector2(60f, 17.4f), new Vector2(20f, 17.4f));
            Plugin.Log.LogInfo($"[AutoTest] 世界地图 走完悬崖入口：{UI.WorldMap.DevReport()}；悬崖入口的东西：{UI.WorldMap.DevThings("Cliffs_02")}");
            yield return MapShot("cliffs_far", 1.5f);
            yield return MapShot("cliffs", 5f);

            Plugin.Log.LogInfo($"[AutoTest] 世界地图 存档往返：{UI.WorldMap.DevRoundTrip(2)}");
            yield return MapShot("after_load", 1.5f);
            mod.DevNoFallDamage = false;
        }
    }
}
