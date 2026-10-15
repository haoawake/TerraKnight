using System.Collections;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "quirrel"): Quirrel's spot by the great window in the Teacher's Archives
    /// (Fungus3_archive_02): his state, every collider and renderer under his feet (active or not, any
    /// layer), the grid's cells there, and a photograph with the player beside him.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator QuirrelTest()
        {
            const string scene = "Fungus3_archive_02";
            yield return GoTo(scene, EntryGateOf(scene));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            PlayMakerFSM q = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene.name == scene && f.FsmName == "npc_control" && f.gameObject.name.Contains("Quirrel")) q = f;
            Vector2 at = q != null ? (Vector2)q.transform.position : new Vector2(61.4f, 86f);
            // Where he is drawn (his biggest renderer): his feet, not his object's origin.
            if (q != null)
            {
                float best = 0f;
                foreach (var r in q.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    float a2 = r.bounds.size.x * r.bounds.size.y;
                    if (a2 > best && a2 < 60f) { best = a2; at = new Vector2(r.bounds.center.x, r.bounds.min.y); }
                }
            }
            var sb = new StringBuilder();
            sb.Append($"奎若尔 {(q != null ? PathOf(q.transform) : "找不到")} {at:F1} 激活 {(q != null && q.gameObject.activeInHierarchy)}");
            var below = new Rect(at.x - 3f, at.y - 6f, 6f, 7f);
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.gameObject.scene.name != scene) continue;
                var b = c.bounds;
                if (b.max.x < below.xMin || b.min.x > below.xMax || b.max.y < below.yMin || b.min.y > below.yMax) continue;
                if (b.size.x > 60f) continue;
                sb.Append($"\n    碰撞 {PathOf(c.transform)} {c.GetType().Name} 层 {c.gameObject.layer} 触发 {c.isTrigger} 启用 {c.enabled}/{c.gameObject.activeInHierarchy} {(Vector2)b.min:F1}..{(Vector2)b.max:F1}");
            }
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.gameObject.scene.name != scene || r.sprite == null) continue;
                var b = r.bounds;
                if (b.max.x < below.xMin || b.min.x > below.xMax || b.max.y < below.yMin || b.min.y > at.y - 0.3f) continue;
                if (b.size.x > 30f || b.center.z > 2f || b.center.z < -2f) continue;
                sb.Append($"\n    贴图 {PathOf(r.transform)} [{r.sprite.name}] z {b.center.z:F2} 画出 {r.enabled && !r.forceRenderingOff}/{r.gameObject.activeInHierarchy} {(Vector2)b.min:F1}..{(Vector2)b.max:F1}");
            }
            for (float dy = 0.25f; dy <= 3f; dy += 0.5f)
            {
                var cell = World.CellGrid.WorldToCell(at - new Vector2(0f, dy));
                sb.Append($"\n    往下 {dy:F2}：格子实心 {World.TerraWorld.IsSolid(cell.x, cell.y)} 挖过 {World.TerraWorld.IsDug(cell.x, cell.y)} 贴图被抠 {World.TerraWorld.ArtCutAt(cell.x, cell.y)}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 奎若尔脚下 {sb}");
            // The tilemap's own outlines round there, point by point, and the grid's cells as made from them.
            var area = new Rect(at.x - 12f, at.y - 8f, 24f, 14f);
            var ol = new StringBuilder();
            foreach (var e in Object.FindObjectsByType<EdgeCollider2D>(FindObjectsSortMode.None))
            {
                if (e.gameObject.scene.name != scene || e.gameObject.layer != World.ChunkColliders.TerrainLayer) continue;
                var eb = e.bounds;
                if (eb.max.x < area.xMin || eb.min.x > area.xMax || eb.max.y < area.yMin || eb.min.y > area.yMax) continue;
                var pts = e.points;
                bool closed = pts.Length > 2 && (pts[0] - pts[pts.Length - 1]).sqrMagnitude < 0.01f;
                ol.Append($"\n    轮廓 {PathOf(e.transform)} 启用 {e.enabled} {pts.Length} 点 闭合 {closed}：");
                for (int i = 0; i < pts.Length && i < 80; i++)
                {
                    Vector2 w = e.transform.TransformPoint(pts[i] + e.offset);
                    ol.Append($" ({w.x:F1},{w.y:F1})");
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 奎若尔脚下 原版轮廓{ol}");
            var grid = new StringBuilder();
            const float cs = World.CellGrid.CellSize;
            for (float y = area.yMax; y >= area.yMin; y -= cs)
            {
                grid.Append($"\n    y {y,6:F1} ");
                for (float x = area.xMin; x <= area.xMax; x += cs)
                {
                    var cell = World.CellGrid.WorldToCell(new Vector2(x + cs / 2f, y - cs / 2f));
                    bool isQ = Mathf.Abs(x - at.x) < cs && Mathf.Abs(y - at.y) < cs;
                    grid.Append(isQ ? 'Q' : World.TerraWorld.IsSolid(cell.x, cell.y) ? '#' : '.');
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 奎若尔脚下 网格（x {area.xMin:F0}..{area.xMax:F0}，# 实心，Q 奎若尔）{grid}");
            var hero = HeroController.instance;
            // On the ledge left of him (the tilemap's, y 79..80, from x 57).
            hero.transform.position = Clear(hero, new Vector2(58.5f, 81.2f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            UI.WorldMap.DevStyle(0);
            yield return new WaitForSeconds(1.2f);
            Plugin.Log.LogInfo($"[AutoTest] 奎若尔脚下 主角放到 {(Vector2)hero.transform.position:F1}，站地 {hero.cState.onGround}");
            Player.TerraCamera.DevLook = at;
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "quirrel");
            yield return null;
            Player.TerraCamera.DevLook = null;
            yield return null;
            UI.WorldMap.DevStyle(1);
        }
    }
}
