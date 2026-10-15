using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dropvisible"): items dropped along the floor of three rooms (the Crossroads, Greenpath's
    /// first room with its near scenery, Dirtmouth): for each, whether a renderer of the game's scenery is
    /// drawn over it (in front of it and later in the draw order) and how lit its spot is; photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DropVisibleTest()
        {
            int covered = 0, total = 0, foreground = 0;
            foreach (var room in new[] { "Crossroads_01", "Fungus1_01", "Town" })
            {
                yield return GoTo(room, room == "Town" ? "left1" : EntryGateOf(room));
                yield return new WaitForSeconds(1.5f);
                PlayerData.instance.isInvincible = true;
                var hero = HeroController.instance;
                Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
                // A row of drops across the view.
                for (int k = -5; k <= 5; k++) WorldItems.Drop(ItemStack.Of(ItemType.Gel, 1), at + new Vector2(k * 2.5f, 2f));
                yield return new WaitForSeconds(1.5f);
                var lines = new System.Text.StringBuilder();
                foreach (var sr in WorldItems.DevRenderers)
                {
                    if (sr == null || !sr.enabled || sr.gameObject.scene.name != room && sr.transform.root.gameObject.scene.name != room) continue;
                    total++;
                    Vector2 c = sr.bounds.center;
                    string over = null;
                    bool overLayer = false;
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.isVisible || r == sr || r is ParticleSystemRenderer) continue;
                        if (r.name.StartsWith("Terra") || r.transform.root.name.StartsWith("Terra") || r.gameObject.layer == 9) continue;
                        // A room's masks (a secret area not yet entered, the inside of its walls) hide the knight there too.
                        if (r.name.StartsWith("Masker")) continue;
                        // Lights and glows (the knight's light donut) brighten, they don't hide.
                        string lower = r.name.ToLowerInvariant();
                        if (lower.Contains("light") || lower.Contains("glow")) continue;
                        var b = r.bounds;
                        if (c.x < b.min.x || c.x > b.max.x || c.y < b.min.y || c.y > b.max.y) continue;
                        if (b.size.x > 60f || b.size.y > 40f) continue;
                        bool later = r.sortingLayerID == sr.sortingLayerID ? (r.sortingOrder > sr.sortingOrder || r.sortingOrder == sr.sortingOrder && r.transform.position.z < sr.transform.position.z - 0.01f)
                                                                            : SortingLayer.GetLayerValueFromID(r.sortingLayerID) > SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
                        if (later) { over = $"{r.name}（层 {SortingLayer.IDToName(r.sortingLayerID)}/{r.sortingOrder}，z {r.transform.position.z:F2}）"; overLayer = r.sortingLayerID != sr.sortingLayerID; break; }
                    }
                    float light = World.LightMap.DevLightAt(c);
                    // The "Over" layer's foreground (Greenpath's leaves) is over the knight as well: counted apart.
                    if (over != null && overLayer) foreground++;
                    else if (over != null) covered++;
                    if (lines.Length < 1500) lines.Append($" {c:F1}{(over != null ? " 被 " + over + " 挡住" : "")}，亮度 {light:F2}；");
                }
                Shot(room, "dropvisible");
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 掉落物可见 {room}：{lines}");
                WorldItems.DevClear();
            }
            Plugin.Log.LogInfo($"[AutoTest] 掉落物可见 合计 {total} 个，被场景挡住 {covered} 个，在 Over 层的前景后面（主角也在它后面）{foreground} 个 {(covered == 0 && total > 0 ? "通过" : "不通过")}");
        }
    }
}
