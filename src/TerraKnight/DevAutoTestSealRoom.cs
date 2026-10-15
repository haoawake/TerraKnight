using System.Collections;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "sealroom"): the Hallownest Seal hidden at the top of Crossroads_01, under Dirtmouth's
    /// well: seen from Dirtmouth (Crossroads_01 loaded beside it) and from Crossroads_01 itself. At the
    /// seal: which scene's grid holds the cells round it and whether they are solid, the scenes' art
    /// there (shown, or hidden by the seamless world), the masks over it; the player put by it and
    /// photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static PlayMakerFSM FindSeal()
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.FsmName != "Shiny Control" || f.gameObject.scene.name != "Crossroads_01") continue;
                var n = f.FsmVariables.FindFsmInt("Trinket Num");
                if (n != null && n.Value == 2) return f;
            }
            return null;
        }

        static string AroundSeal(Vector2 at)
        {
            var sb = new StringBuilder();
            const float cs = World.CellGrid.CellSize;
            var area = new Rect(at.x - 8f, at.y - 5f, 16f, 10f);
            sb.Append($"\n    网格（x {area.xMin:F0}..{area.xMax:F0}，# 实心，S 印章；行尾是这一行中间格子属于哪个场景）");
            for (float y = area.yMax; y >= area.yMin; y -= cs)
            {
                sb.Append($"\n    y {y,6:F1} ");
                for (float x = area.xMin; x <= area.xMax; x += cs)
                {
                    var cell = World.CellGrid.WorldToCell(new Vector2(x + cs / 2f, y - cs / 2f));
                    bool isS = Mathf.Abs(x - at.x) < cs && Mathf.Abs(y - at.y) < cs;
                    sb.Append(isS ? 'S' : World.TerraWorld.IsSolid(cell.x, cell.y) ? '#' : '.');
                }
                var mid = World.CellGrid.WorldToCell(new Vector2(at.x, y - cs / 2f));
                sb.Append(" ").Append(World.TerraWorld.RegionAt(mid.x, mid.y)?.SceneName ?? "无");
            }
            sb.Append("\n    这一带的贴图：").Append(World.LiveWorld.Instance != null ? World.LiveWorld.Instance.DevArtAt(new Rect(at.x - 3f, at.y - 2f, 6f, 4f)) : "无");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("mask")) continue;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                var b = r.bounds;
                if (b.max.x < at.x - 3f || b.min.x > at.x + 3f || b.max.y < at.y - 3f || b.min.y > at.y + 3f) continue;
                var c = r is SpriteRenderer sr ? sr.color : Color.white;
                sb.Append($"\n    遮罩 {t.gameObject.scene.name}/{PathOf(t)} 画出 {r.enabled && !r.forceRenderingOff}/{t.gameObject.activeInHierarchy} 不透明 {c.a:F2} {(Vector2)b.min:F1}..{(Vector2)b.max:F1}");
            }
            return sb.ToString();
        }

        IEnumerator SealRoomTest()
        {
            foreach (var from in new[] { "Town", "Crossroads_01" })
            {
                yield return GoTo(from, from == "Town" ? "left1" : EntryGateOf(from));
                yield return new WaitForSeconds(2f);
                PlayerData.instance.isInvincible = true;
                var hero = HeroController.instance;
                if (from == "Town")
                {
                    // To the well, so Crossroads_01 loads underneath.
                    foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                        if (tp.gameObject.scene.name == "Town" && tp.targetScene == "Crossroads_01")
                            hero.transform.position = Clear(hero, (Vector2)tp.transform.position + new Vector2(-3f, 2f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(4f);
                }
                var seal = FindSeal();
                if (seal == null) { Plugin.Log.LogInfo($"[AutoTest] 印章房间 从 {from}：Crossroads_01 的印章不在（场景没载入）"); continue; }
                Vector2 at = seal.transform.position;
                var r = seal.GetComponentInChildren<Renderer>(true);
                Plugin.Log.LogInfo($"[AutoTest] 印章房间 从 {from}（现在在 {GameManager.instance.sceneName}）：印章 {at:F1} 激活 {seal.gameObject.activeInHierarchy} 画出 {(r != null && r.enabled && !r.forceRenderingOff)} 状态 {seal.ActiveStateName}{AroundSeal(at)}");
                hero.transform.position = Clear(hero, at + new Vector2(-1.5f, 0.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                UI.WorldMap.DevStyle(0);
                yield return new WaitForSeconds(1.2f);
                Plugin.Log.LogInfo($"[AutoTest] 印章房间 从 {from}：主角放到 {(Vector2)hero.transform.position:F1}（{GameManager.instance.sceneName}），站地 {hero.cState.onGround}");
                Shot(GameManager.instance.sceneName, "sealroom_" + from);
                yield return null;
                UI.WorldMap.DevStyle(1);
            }
        }
    }
}
