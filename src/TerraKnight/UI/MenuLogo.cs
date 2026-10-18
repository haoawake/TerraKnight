using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The title screen's big logo reads 空洞泰拉 (空洞泰拉 in traditional Chinese) instead of
    /// 空洞骑士. MenuStyleTitle puts the menu style's title picture for the language on screen;
    /// for Chinese this swaps in the mod's own picture, made from that very sprite with only the
    /// four characters redrawn (embedded title_*.png), the same size and in the same place, so
    /// the ornaments and the Void Heart line stay as they were. Other languages keep the game's.
    /// </summary>
    [HarmonyPatch]
    internal static class MenuLogo
    {
        /// <summary>The game's Chinese title sprites and the pictures that replace them.</summary>
        static readonly Dictionary<string, string> pictureFor = new Dictionary<string, string>
        {
            { "title_chinese", "title_zh.png" },
            { "title_traditional_chinese_regular", "title_zh_tw.png" },
            { "vheart_title_chinese", "title_voidheart_zh.png" },
            { "title_traditional_chinese_voidheart", "title_voidheart_zh_tw.png" },
        };

        static readonly Dictionary<Sprite, Sprite> made = new Dictionary<Sprite, Sprite>();
        static readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MenuStyleTitle), nameof(MenuStyleTitle.SetTitle))]
        static void AfterSetTitle(MenuStyleTitle __instance)
        {
            var title = __instance.Title;
            if (title == null || title.sprite == null) return;
            var lang = Language.CurrentLanguage();
            if (lang != LanguageCode.ZH && lang != LanguageCode.ZH_TW) return;
            var ours = Ours(title.sprite, lang);
            if (ours != null) title.sprite = ours;
        }

        static Sprite Ours(Sprite original, LanguageCode lang)
        {
            if (made.TryGetValue(original, out var s)) return s;
            if (made.ContainsValue(original)) return original;
            if (!pictureFor.TryGetValue(original.name, out var file))
                file = lang == LanguageCode.ZH_TW ? "title_zh_tw.png" : "title_zh.png";
            var tex = Picture(file);
            if (tex == null) return null;
            // The picture is the sprite's own (trimmed) texture area: the same size on screen and
            // the same pivot, wherever that area sits in the sprite.
            var area = original.textureRect;
            float ppu = original.pixelsPerUnit * tex.width / area.width;
            var pivot = (original.pivot - original.textureRectOffset) / area.size;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
            s.name = "TerraKnight " + Path.GetFileNameWithoutExtension(file);
            made[original] = s;
            return s;
        }

        static Texture2D Picture(string file)
        {
            if (pictures.TryGetValue(file, out var tex)) return tex;
            using (var stream = typeof(MenuLogo).Assembly.GetManifestResourceStream("TerraKnight." + file))
            {
                if (stream != null)
                {
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = file, wrapMode = TextureWrapMode.Clamp };
                    if (!tex.LoadImage(bytes)) tex = null;
                }
            }
            if (tex == null) Plugin.Log.LogWarning($"[标题] 找不到标题图 {file}，保留原版标题");
            pictures[file] = tex;
            return tex;
        }

        /// <summary>Dev: the title screen's logo, what it shows, and the game's Chinese title sprites saved as pictures.</summary>
        public static void DevDump(string dir)
        {
            int m = 0;
            foreach (var mt in Resources.FindObjectsOfTypeAll<MenuStyleTitle>())
            {
                if (!mt.gameObject.scene.IsValid()) continue;
                var t = mt.Title;
                Plugin.Log.LogInfo($"[AutoTest] 标题 {m}：{mt.gameObject.scene.name}:{mt.transform.name} 当前 {Describe(t ? t.sprite : null)} 范围 {(t ? t.bounds.size : Vector3.zero):F2} 位置 {(t ? t.transform.position : Vector3.zero):F2}；" +
                                   $"默认 中文 {Describe(mt.DefaultTitleSprite.Chinese)} 繁体 {Describe(mt.DefaultTitleSprite.ChineseTraditional)}；主题标题 {mt.TitleSprites?.Length ?? 0} 套");
                var seen = new HashSet<Sprite>();
                void Keep(Sprite s, string tag)
                {
                    if (s == null || !seen.Add(s)) return;
                    Save(s, Path.Combine(dir, $"title{m}_{tag}_{s.name}.png"));
                }
                Keep(mt.DefaultTitleSprite.Chinese, "zh");
                Keep(mt.DefaultTitleSprite.ChineseTraditional, "zhtw");
                if (mt.TitleSprites != null)
                    for (int i = 0; i < mt.TitleSprites.Length; i++)
                    {
                        Keep(mt.TitleSprites[i].Chinese, $"s{i}zh");
                        Keep(mt.TitleSprites[i].ChineseTraditional, $"s{i}zhtw");
                    }
                m++;
            }
        }

        static string Describe(Sprite s) => s == null ? "无" : $"{s.name} {s.rect.width}x{s.rect.height}（贴图区 {s.textureRect.width}x{s.textureRect.height} 偏移 {s.textureRectOffset}）ppu {s.pixelsPerUnit:F1} 轴心 {s.pivot}";

        /// <summary>Reads a sprite's texture area back through a render texture (the game's textures aren't readable).</summary>
        static void Save(Sprite s, string path)
        {
            var r = s.textureRect;
            var rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(s.texture, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
