using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hollow Knight hides secret rooms under masks (msk_* sprites) that fade away when the Knight
    /// walks in through the room's entrance. Digging into a secret room from anywhere else never
    /// crosses that entrance, and the player would be inside it, unseen. A mask the player is
    /// standing inside fades out the same way.
    /// </summary>
    internal static class SceneMasks
    {
        static readonly List<SpriteRenderer> masks = new List<SpriteRenderer>();
        static readonly Dictionary<SpriteRenderer, float> fading = new Dictionary<SpriteRenderer, float>();
        static float nextScan, nextCheck;
        static int sceneCount = -1;

        public static void Tick(HeroController hero)
        {
            if (hero == null) return;
            // The masks of every loaded scene, refreshed when scenes come and go.
            int loaded = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (loaded != sceneCount || Time.time > nextScan)
            {
                sceneCount = loaded;
                nextScan = Time.time + 5f;
                masks.Clear();
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (sr.sprite != null && sr.sprite.name.StartsWith("msk_")) masks.Add(sr);
            }
            if (Time.time > nextCheck)
            {
                nextCheck = Time.time + 0.2f;
                Vector2 p = hero.GetComponent<BoxCollider2D>().bounds.center;
                foreach (var sr in masks)
                {
                    if (sr == null || !sr.enabled || sr.forceRenderingOff || !sr.gameObject.activeInHierarchy || sr.color.a < 0.05f || fading.ContainsKey(sr)) continue;
                    var b = sr.bounds;
                    // Inset a little: the soft edge of the mask spills over the room's walls.
                    if (p.x > b.min.x + 0.5f && p.x < b.max.x - 0.5f && p.y > b.min.y + 0.5f && p.y < b.max.y - 0.5f)
                        fading[sr] = sr.color.a;
                }
            }
            if (fading.Count == 0) return;
            var done = new List<SpriteRenderer>();
            foreach (var kv in fading)
            {
                var sr = kv.Key;
                if (sr == null) { done.Add(sr); continue; }
                var c = sr.color;
                c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime / 0.5f);
                sr.color = c;
                if (c.a <= 0f) done.Add(sr);
            }
            foreach (var sr in done)
            {
                fading.Remove(sr);
                if (sr != null) Plugin.Log.LogInfo($"[遮罩] 玩家进了被遮住的区域，揭开 {sr.name}（{sr.gameObject.scene.name}）");
            }
        }
    }
}
