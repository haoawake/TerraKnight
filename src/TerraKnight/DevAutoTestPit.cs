using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pit"): the Dream Nail's trial over the void (Dream_Nailcollection): fallen to the
    /// bottom of the scene, the Knight is taken back to safe ground as by the game's pits, not left
    /// standing on the world's edge.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PitTest()
        {
            const string scene = "Dream_Nailcollection";
            PlayerData.instance.isInvincible = true;
            yield return GoTo(scene, EntryGateOf(scene));
            yield return new WaitForSeconds(2f);
            var h = HeroController.instance;
            Vector2 safe = h.transform.position;
            var region = World.TerraWorld.Live;
            if (region == null) { Plugin.Log.LogInfo("[AutoTest] 深坑 没有地形 不通过"); yield break; }
            const float c = World.CellGrid.CellSize;
            var cr = region.CellRect;
            // Somewhere near the middle, low, in the open: fall from there.
            Vector2 drop = new Vector2((cr.xMin + cr.width * 0.5f) * c, (cr.yMin + 6) * c);
            int pits0 = World.WorldBorder.DevPits;
            h.transform.position = new Vector3(drop.x, drop.y, h.transform.position.z);
            var path = new System.Text.StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSeconds(0.5f);
                path.Append($" {(Vector2)h.transform.position:F1}{(h.cState.hazardRespawning ? "回" : "")}");
            }
            bool back = World.WorldBorder.DevPits > pits0;
            Plugin.Log.LogInfo($"[AutoTest] 深坑 {scene}：场景底 {cr.yMin * c:F1}，从 {drop:F1} 掉下；掉出底部按深坑处理 {back}；位置{path}；进场时站在 {safe:F1} {(back ? "通过" : "不通过")}");
            Shot(GameManager.instance.sceneName, "pit_after");
            yield return null;
        }
    }
}
