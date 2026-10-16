using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hksprites"): the game's sprites in memory whose names look like the inventory's key items and charms
    /// (name, texture, size), written to hksprites.txt, and the inventory pane's own item objects with their sprites.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HkSpritesScan()
        {
            yield return new WaitForSeconds(1f);
            var lines = new List<string>();
            string[] words = { "lantern", "key", "crest", "tram", "brand", "king", "void", "heart", "charm", "flower", "egg", "ore", "seal", "idol", "journal", "quill", "map", "tuner", "shade", "grimm", "collector", "love", "white", "sly", "pass", "inv" };
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                string n = s.name.ToLowerInvariant();
                bool hit = false;
                foreach (var w in words) if (n.Contains(w)) { hit = true; break; }
                if (!hit) continue;
                lines.Add($"sprite\t{s.name}\t{(s.texture != null ? s.texture.name : "-")}\t{s.rect.width}x{s.rect.height}");
            }
            // The inventory pane's items (Inv > Inv_Items): each object's name and its sprite.
            foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                var t = sr.transform;
                string path = t.name;
                for (var p = t.parent; p != null && path.Length < 200; p = p.parent) path = p.name + "/" + path;
                if (!path.Contains("Inv")) continue;
                lines.Add($"renderer\t{path}\t{(sr.sprite != null ? sr.sprite.name : "-")}");
            }
            // The UI sheet's inventory and charm texts.
            foreach (var sheet in new[] { "UI", "Prompts" })
            {
                System.Collections.Generic.IEnumerable<string> keys = null;
                try { keys = TeamCherry.Localization.Language.GetKeys(sheet); } catch (System.Exception e) { lines.Add("keys error " + e.Message); }
                if (keys == null) continue;
                foreach (var k in keys)
                    if (k.StartsWith("INV_") || k.StartsWith("CHARM_NAME_36") || k.StartsWith("CHARM_DESC_36") || k.StartsWith("CHARM_NAME_40"))
                        lines.Add(string.Join("|", "text", sheet, k, TeamCherry.Localization.Language.Get(k, sheet).Replace((char)10, '/')));
            }
            File.WriteAllLines(Path.Combine(DevCopy.SaveDir, "hksprites.txt"), lines);
            Plugin.Log.LogInfo($"[AutoTest] 空洞骑士精灵图 {lines.Count} 条");
        }
    }
}
