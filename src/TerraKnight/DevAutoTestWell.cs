using System.Collections;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "well"): Dirtmouth's well down to the Crossroads and back up, as the player went:
    /// pictures down the shaft (the seam between the scenes, the shaft's art), flying straight back
    /// up from the bottom, then twice the bottom's own way up (door1's Door Control: up pressed, the
    /// game's scene change to Dirtmouth's bot1), the hero's state each second after it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string HeroState(HeroController hero)
        {
            var gm = GameManager.instance;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            return $"场景 {gm.sceneName}，主角 {hero.transform.position:F1}，速度 {hero.GetComponent<Rigidbody2D>().linearVelocity:F1}，" +
                   $"状态 {hero.hero_state}，切换中 {gm.IsInSceneTransition}/{hero.cState.transitioning}，接受输入 {hero.acceptingInput}，交出控制 {hero.controlReqlinquished}，" +
                   $"可操作 {GameHooks.PlayerHasControl()}，镜头 {(cam != null ? cam.transform.position.ToString("F1") : "无")}";
        }

        IEnumerator FallDownWell(HeroController hero, string tag)
        {
            // Dirtmouth's well: bot1 is at x 185 in Dirtmouth's own coordinates (wherever Dirtmouth is now).
            var town = World.TerrainSystem.Find("Town");
            var origin = town != null ? (Vector2)town.CellOffset * World.CellGrid.CellSize : Vector2.zero;
            hero.transform.position = Clear(hero, origin + new Vector2(185f, 9f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            for (int i = 0; i < 10; i++)
            {
                PlayerData.instance.isInvincible = true;
                Plugin.Log.LogInfo($"[AutoTest] 井 {tag} 下落 {i}：{HeroState(hero)}");
                Shot(GameManager.instance.sceneName, $"well_{tag}_fall_{i}");
                if (tag == "1" && i == 1 && World.LiveWorld.Instance != null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 井 左壁接缝下：{World.LiveWorld.Instance.DevArtAt(new Rect(origin + new Vector2(181.2f, 4.3f), new Vector2(1.2f, 2.5f)))}");
                    Plugin.Log.LogInfo($"[AutoTest] 井 右壁接缝下：{World.LiveWorld.Instance.DevArtAt(new Rect(origin + new Vector2(187.6f, 4.3f), new Vector2(1.3f, 2.5f)))}");
                }
                yield return null;
                yield return new WaitForSeconds(0.25f);
                if (hero.cState.onGround && i > 2) break;
            }
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 井 {tag} 到底：{HeroState(hero)}");
            Shot(GameManager.instance.sceneName, $"well_{tag}_bottom");
            yield return null;
        }

        IEnumerator WellTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(3f);
            yield return FallDownWell(hero, "1");

            // Straight back up, flying (a steady climb), without the door.
            var rb = hero.GetComponent<Rigidbody2D>();
            float t0 = Time.time;
            int shot = 0;
            while (Time.time - t0 < 4f)
            {
                yield return new WaitForFixedUpdate();
                rb.linearVelocity = new Vector2(0f, 10f);
                PlayerData.instance.isInvincible = true;
                if (Time.time - t0 > shot * 0.5f)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 井 往上飞 {shot}：{HeroState(hero)}");
                    Shot(GameManager.instance.sceneName, $"well_fly_{shot}");
                    shot++;
                }
            }
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 井 飞上去以后：{HeroState(hero)}");

            // Twice down and up by the bottom's own way: up pressed at door1.
            for (int round = 1; round <= 2; round++)
            {
                hero = HeroController.instance;
                if (World.TerrainSystem.Find("Town") == null)
                {
                    yield return GoTo("Town", "left1");
                    hero = HeroController.instance;
                    yield return new WaitForSeconds(3f);
                }
                yield return FallDownWell(hero, $"door{round}");
                PlayMakerFSM door = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (f.FsmName == "Door Control" && f.gameObject.name == "door1" && f.gameObject.scene.name == "Crossroads_01") door = f;
                Plugin.Log.LogInfo($"[AutoTest] 井 第{round}次 井底的门：{(door != null ? door.ActiveStateName : "没找到")}，{HeroState(hero)}");
                if (door == null) break;
                door.SendEvent("UP PRESSED");
                for (int s = 0; s < 12; s++)
                {
                    yield return new WaitForSeconds(1f);
                    hero = HeroController.instance;
                    PlayerData.instance.isInvincible = true;
                    Plugin.Log.LogInfo($"[AutoTest] 井 第{round}次 按上 {s + 1}s：门 {(door != null ? door.ActiveStateName : "没了")}，{HeroState(hero)}");
                    if (s == 2 || s == 8) Shot(GameManager.instance.sceneName, $"well_door{round}_{s + 1}s");
                }
                // Walk a little: does the player have the hero back?
                yield return WalkOn(hero, -1, 1.5f, $"第{round}次上来以后走");
            }
            mod.DevNoFallDamage = false;
        }
    }
}
