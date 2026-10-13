using System.Collections;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "route"): the player's way through Howling Cliffs, to check the map and the
    /// light. The map: the head with the player standing at a bench against the bench pin of its
    /// room (the game's own marker of where the bench is). The light: the same spot of Cliffs_02
    /// after each round of crossings (seamless into Cliffs_01 and back, the game's own scene
    /// change, through a darkness region), with the darkness, ambient and hero light state logged
    /// and a picture taken, so a drift shows as a trend.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly Vector2 lightSpot = new Vector2(20f, 17.4f);

        static string LightState()
        {
            var sb = new StringBuilder();
            var vignette = GameObject.FindGameObjectWithTag("Vignette");
            var dc = vignette != null ? FSMUtility.LocateFSM(vignette, "Darkness Control") : null;
            if (dc != null) sb.Append($"暗度状态机 {dc.ActiveStateName} 等级 {dc.FsmVariables.GetFsmInt("Darkness Level").Value}");
            else sb.Append("没有暗度状态机");
            if (vignette != null)
            {
                var vsr = vignette.GetComponent<SpriteRenderer>();
                sb.Append($"，暗角 {(vignette.activeInHierarchy ? "开" : "关")} 缩放 {vignette.transform.localScale.x:F2}{(vsr != null ? " α" + vsr.color.a.ToString("F2") : "")}");
            }
            var gm = GameManager.instance;
            if (gm.sm != null) sb.Append($"，场景暗度 {gm.sm.darknessLevel}{(gm.sm.noLantern ? " 无灯笼" : "")}");
            var scm = GameCameras.instance.sceneColorManager;
            sb.Append($"，环境光 A {scm.AmbientColorA} {scm.AmbientIntensityA:F2} B {scm.AmbientIntensityB:F2} 混合 {scm.Factor:F2}，RenderSettings {RenderSettings.ambientLight}");
            var hero = HeroController.instance;
            if (hero != null && hero.heroLight != null) sb.Append($"，主角光 {hero.heroLight.color} 缩放 {hero.heroLight.transform.localScale.x:F2}");
            int regions = 0;
            var inside = new StringBuilder();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "Darkness Region") continue;
                regions++;
                var c = f.GetComponent<Collider2D>();
                if (c != null && hero != null && c.OverlapPoint(hero.transform.position)) inside.Append($" {f.gameObject.scene.name}/{f.name}@{f.ActiveStateName}");
            }
            sb.Append($"，暗区 {regions} 个，人在其中：{(inside.Length > 0 ? inside.ToString() : "无")}");
            return sb.ToString();
        }

        IEnumerator AtLightSpot(HeroController hero, string label)
        {
            hero.transform.position = Clear(hero, lightSpot);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(2.5f);
            PlayerData.instance.isInvincible = true;
            Plugin.Log.LogInfo($"[AutoTest] 光照 {label}：场景 {GameManager.instance.sceneName} 主角 {hero.transform.position:F1}；{LightState()}");
            Shot(GameManager.instance.sceneName, "light_" + label);
            yield return null;
        }

        IEnumerator MapAt(HeroController hero, Vector2 at, string label)
        {
            hero.transform.position = Clear(hero, at);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            UI.FullMap.Toggle();
            yield return new WaitForSeconds(0.6f);
            var head = UI.FullMap.DevHead(out bool found);
            var pd = PlayerData.instance;
            var visited = new StringBuilder();
            foreach (var s in pd.scenesVisited) if (s.StartsWith("Cliffs") || s.StartsWith("Tutorial")) visited.Append(s).Append(' ');
            Plugin.Log.LogInfo($"[AutoTest] 隧道 {label}：场景 {GameManager.instance.sceneName} 主角 {hero.transform.position:F1}，头像 {head:F2}（{(found ? "定位到" : "没定位")}）；" +
                               $"{UI.FullMap.DevUnderHead()}；到过：{visited}");
            Shot(GameManager.instance.sceneName, "tunnel_" + label);
            yield return null;
            UI.FullMap.Close();
            yield return null;
        }

        /// <summary>
        /// Dev (suite "tunnel"): the way from the start to Howling Cliffs (King's Pass's left shaft,
        /// then Cliffs_02's dark tunnel to the one-way wall): whether the map shows anything under the head.
        /// </summary>
        IEnumerator TunnelTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            // King's Pass sets a 310-wide frame for its 201-wide scene: its middle and right end show whether that fits the drawing.
            yield return MapAt(hero, new Vector2(100f, 11.5f), "0a_山道中段");
            yield return MapAt(hero, new Vector2(190f, 11.5f), "0b_山道右端");
            yield return MapAt(hero, new Vector2(30f, 11.5f), "1_山道出生点左边");
            yield return MapAt(hero, new Vector2(5f, 30f), "2_山道左边竖井下段");
            yield return MapAt(hero, new Vector2(5f, 55f), "3_山道左边竖井上段");
            string gate = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.targetScene == "Cliffs_02") gate = tp.entryPoint;
            if (gate == null) yield break;
            yield return GoTo("Cliffs_02", gate);
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            yield return MapAt(hero, new Vector2(195f, 6f), "4_悬崖入口底部");
            yield return MapAt(hero, new Vector2(190f, 20f), "5_黑暗隧道右段");
            yield return MapAt(hero, new Vector2(175f, 13.5f), "6_黑暗隧道左段");
            yield return MapAt(hero, new Vector2(166f, 13.5f), "7_石墙跟前");
            mod.DevNoFallDamage = false;
        }

        IEnumerator RouteTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            string gate = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.targetScene == "Cliffs_02") gate = tp.entryPoint;
            if (gate == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 路线 国王山道里没有去 Cliffs_02 的门");
                yield break;
            }
            yield return GoTo("Cliffs_02", gate);
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 光照 刚进 Cliffs_02：{LightState()}");
            // What the game's own compass uses for this scene (GetTilemapDimensions, or a scene's SetManualTilemap).
            var gmap = GameManager.instance.gameMap != null ? GameManager.instance.gameMap.GetComponent<GameMap>() : null;
            if (gmap != null)
            {
                float F(string n) => (float)HarmonyLib.AccessTools.Field(typeof(GameMap), n).GetValue(gmap);
                var live = World.TerraWorld.Live;
                Plugin.Log.LogInfo($"[AutoTest] 地图 原版指南针用的场景框：原点偏移 {F("originOffsetX"):F2},{F("originOffsetY"):F2} 宽高 {F("sceneWidth"):F2}x{F("sceneHeight"):F2}；" +
                                   $"mod 用的：格子偏移 {live?.CellOffset} 宽高 {live?.Grid.Width * World.CellGrid.CellSize:F2}x{live?.Grid.Height * World.CellGrid.CellSize:F2}；tilemap {GameManager.instance.tilemap?.width}x{GameManager.instance.tilemap?.height}");
            }

            // The map: the head at each bench of the scene against its room's bench pin.
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "Bench Control" || f.gameObject.scene.name != GameManager.instance.sceneName) continue;
                hero.transform.position = Clear(hero, (Vector2)f.transform.position + Vector2.up);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                UI.FullMap.Toggle();
                yield return new WaitForSeconds(0.6f);
                var head = UI.FullMap.DevHead(out bool found);
                Plugin.Log.LogInfo($"[AutoTest] 地图 长椅 {f.name} 在 {f.transform.position:F1}，主角 {hero.transform.position:F1}，头像 {head:F2}（{(found ? "定位到" : "没定位")}），" +
                                   $"{GameManager.instance.sceneName} 房间的长椅标记：{UI.FullMap.DevBenchPins(GameManager.instance.sceneName, head)}；{UI.FullMap.DevView()}");
                Shot(GameManager.instance.sceneName, "route_map_bench");
                yield return null;
                UI.FullMap.Close();
            }
            // The map at a few more places, for the pictures.
            foreach (var at in new[] { new Vector2(20f, 17.4f), new Vector2(95f, 6.5f), new Vector2(162f, 13.5f), new Vector2(190f, 30.5f) })
            {
                hero.transform.position = Clear(hero, at);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                UI.FullMap.Toggle();
                yield return new WaitForSeconds(0.6f);
                var head = UI.FullMap.DevHead(out bool found);
                Plugin.Log.LogInfo($"[AutoTest] 地图 在 {hero.transform.position:F1}：头像 {head:F2}（{(found ? "定位到" : "没定位")}）；{UI.FullMap.DevView()}");
                if (at.x < 30f) Plugin.Log.LogInfo($"[AutoTest] 地图 悬崖的房间：{UI.FullMap.DevRooms("Cliffs")}；城镇和山道：{UI.FullMap.DevRooms("Town_Tutorial")}");
                Shot(GameManager.instance.sceneName, $"route_map_{at.x:F0}");
                yield return null;
                UI.FullMap.Close();
            }

            // The light, after rounds of crossings.
            yield return AtLightSpot(hero, "0_起点");
            for (int round = 1; round <= 3; round++)
            {
                hero.transform.position = Clear(hero, new Vector2(2.5f, 24.2f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(3f);
                PlayerData.instance.isInvincible = true;
                yield return WalkOn(hero, -1, 2.5f, $"第{round}轮 走进 Cliffs_01");
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 光照 第{round}轮 在 {GameManager.instance.sceneName}：{LightState()}");
                Shot(GameManager.instance.sceneName, $"light_{round}a_cliffs01");
                yield return null;
                yield return WalkOn(hero, 1, 2.5f, $"第{round}轮 走回 Cliffs_02");
                yield return new WaitForSeconds(1f);
                yield return AtLightSpot(hero, $"{round}_无缝来回");
            }
            // Through the darkness region by the one-way wall and out again.
            hero.transform.position = Clear(hero, new Vector2(180f, 13.5f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 光照 暗区里：{LightState()}");
            // Cliffs_01's dark region by the seam (level 1 before): now kept at 0.
            hero.transform.position = Clear(hero, new Vector2(-4f, 24.4f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 光照 Cliffs_01 的暗区边上：{LightState()}；压回去 {World.SceneMood.DevDarknessLowered} 次");
            Shot(GameManager.instance.sceneName, "light_4b_cliffs01_dark");
            yield return null;
            Shot(GameManager.instance.sceneName, "light_4a_dark");
            yield return null;
            yield return AtLightSpot(hero, "4_进出暗区");
            // The game's own scene change, twice there and back.
            for (int round = 1; round <= 2; round++)
            {
                yield return GoTo("Cliffs_01", "right1");
                hero = HeroController.instance;
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(2f);
                Plugin.Log.LogInfo($"[AutoTest] 光照 原版切换第{round}次 到 {GameManager.instance.sceneName}：{LightState()}");
                yield return GoTo("Cliffs_02", "left1");
                hero = HeroController.instance;
                PlayerData.instance.isInvincible = true;
                yield return AtLightSpot(hero, $"{4 + round}_原版切换");
            }
            mod.DevNoFallDamage = false;
        }
    }
}
