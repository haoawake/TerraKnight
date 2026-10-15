using System.Collections;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fxtop"): the weapons' effects over the game's near scenery. In Greenpath, by the
    /// nearest big foreground piece (nearer the camera than the hero), the Zenith, the Star Wrath and
    /// a Minishark used through it; the effects' sorting orders logged and the view photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FxTopTest()
        {
            const string room = "Fungus1_01";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(1f);
            var h = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            SpriteRenderer fg = null;
            float best = float.MaxValue;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.gameObject.scene.name != room || !sr.enabled || !sr.isVisible || sr.transform.position.z > -0.5f) continue;
                var b = sr.bounds;
                if (b.size.x < 2f || b.size.y < 2f || b.size.x > 20f) continue;
                float d = Vector2.Distance(b.center, h.transform.position);
                if (d < best) { best = d; fg = sr; }
            }
            if (fg == null) { Plugin.Log.LogInfo("[AutoTest] 特效层 找不到近景装饰 不通过"); yield break; }
            var fb = fg.bounds;
            Plugin.Log.LogInfo($"[AutoTest] 特效层 近景 {PathOf(fg.transform)} z {fg.transform.position.z:F1} 层 {fg.sortingLayerName}/{fg.sortingOrder} 范围 {fb.center:F1} {fb.size:F1}");
            var inv = mod.Inventory;
            inv.Infinite = true;
            int ok = 0, all = 0;
            foreach (var (weapon, ammo) in new[] { (ItemType.Zenith, ItemType.None), (ItemType.StarWrath, ItemType.None), (ItemType.Minishark, ItemType.MusketBall) })
            {
                inv.Slots[0] = ItemStack.Of(weapon);
                if (ammo != ItemType.None) inv.Slots[Inventory.Size] = ItemStack.Of(ammo, 999);
                inv.Selected = 0;
                // Standing beside it, using the weapon through it.
                var probe = new Vector2(fb.min.x - 3f, fb.center.y + 2f);
                var down = Physics2D.Raycast(probe, Vector2.down, 20f, 1 << World.ChunkColliders.TerrainLayer);
                h.transform.position = Clear(h, down.collider != null ? down.point + Vector2.up * 0.7f : probe);
                yield return new WaitForSeconds(0.4f);
                Vector2 aim = fb.center;
                Effects.FxBatch.DevLowCallers.Clear();
                Effects.FxBatch.DevTraceLow = true;
                int onTop = 0, low = 0;
                var lowNames = new System.Collections.Generic.HashSet<string>();
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    mod.DevAim = aim;
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                    yield return null;
                }
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (!sr.enabled || sr.sprite == null) continue;
                    var p = sr.transform.parent;
                    bool fx = p != null && (p.name == "Terra Fx" || p.name.StartsWith("Zenith Sword") || p.GetComponent<Effects.TerraParticles>() != null);
                    // Lights left from before the weapon (a torch's, a lamp's) aren't its: only those it lit count, by their order.
                    if (fx && sr.name == "Terra Glow" && sr.sortingOrder == 0 && !sr.isVisible) fx = false;
                    if (!fx) continue;
                    if (sr.sortingOrder >= Effects.FxBatch.OnTopOrder) onTop++;
                    else { low++; if (lowNames.Count < 8) lowNames.Add(sr.name + "/" + sr.sortingOrder); }
                }
                Effects.FxBatch.DevTraceLow = false;
                Plugin.Log.LogInfo($"[AutoTest] 特效层 {ItemDb.Get(weapon).Name} 不在最上层的是谁画的：{string.Join("，", System.Linq.Enumerable.Select(Effects.FxBatch.DevLowCallers, kv => $"{kv.Key}×{kv.Value}"))}");
                Shot(room, $"fxtop_{weapon}");
                yield return null;
                yield return null;
                mod.DevAim = null;
                all++;
                bool pass = onTop > 0 && low == 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 特效层 {ItemDb.Get(weapon).Name}：特效在最上层 {onTop} 个，不在 {low} 个（{string.Join(" ", lowNames)}） {(pass ? "通过" : "不通过")}");
                yield return new WaitForSeconds(0.8f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 特效层 合计 {ok}/{all}");
        }
    }
}
