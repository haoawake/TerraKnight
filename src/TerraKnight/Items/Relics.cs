using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Master Mode relics (Terraria's MasterTrophy items: ItemDropRule.MasterModeCommonDrop, one every kill in Master
    /// Mode and never in another; placed as furniture on MasterTrophyBase; 5 gold to buy, so a gold to sell). Each of
    /// Hallownest's bosses has one (空洞骑士大师圣物素材包, RelicsGenerated.cs): its picture fitted to a relic's place,
    /// a three-cell-wide base and floating sculpture in a 3x4-cell space, split from a 64x96 texture. A fight the
    /// drop tables key as another (the Absolute Radiance, the
    /// Enraged Guardian, the Sisters of Battle, the Winged Nosk) gives its own, told apart by its scene or its boss's name.
    /// </summary>
    internal static partial class Relics
    {
        /// <summary>The complete replacement art is installed; expose relic items, Master drops and Bestiary entries.</summary>
        public static readonly bool Enabled = true;

        /// <summary>The prefix of a relic's texture name (TerrariaAssets.Texture loads it from the mod's resources).</summary>
        public const string TexturePrefix = "TKRelic:";

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        /// <summary>A relic's picture, from the mod's embedded resources (Resources/relic_*.png).</summary>
        public static Texture2D Texture(string name)
        {
            string file = name.StartsWith(TexturePrefix) ? name.Substring(TexturePrefix.Length) : name;
            if (textures.TryGetValue(file, out var tex) && tex != null) return tex;
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "TerraKnight/" + file, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            using (var s = typeof(Relics).Assembly.GetManifestResourceStream("TerraKnight." + file + ".png"))
            {
                if (s != null)
                {
                    var bytes = new byte[s.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = s.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    ImageConversion.LoadImage(tex, bytes, false);
                }
                else Plugin.Log.LogWarning("[圣物] 找不到贴图 " + file);
            }
            textures[file] = tex;
            return tex;
        }

        public static bool Is(ItemType t) => (int)t >= (int)ItemType.RelicAbsoluteRadiance && (int)t <= (int)ItemType.RelicZote;

        /// <summary>The relic a boss's fight gives (its drop table's key; its scene and its boss's name for the variants), or none.</summary>
        public static ItemType? Of(string key, string scene, string bossName)
        {
            if (key == null) return null;
            string record = key.Split('@')[0];
            foreach (var v in variants)
            {
                if (v.Key != record) continue;
                if (v.By == "scene" && scene == v.Value) return v.Relic;
                if (v.By == "name" && bossName != null && bossName.Contains(v.Value)) return v.Relic;
            }
            if (byKey.TryGetValue(key, out var t)) return t;
            return byKey.TryGetValue(record, out t) ? t : (ItemType?)null;
        }

        /// <summary>The fight being paid now (Bestiary.Credit): its scene and its boss's name, for a variant's relic.</summary>
        public static string ContextScene, ContextName;

        /// <summary>The relic a row of a boss's table drops in the fight being paid (its variant's, where it has one).</summary>
        public static ItemType Swap(ItemType relic, string key)
        {
            var v = Of(key, ContextScene, ContextName);
            return v ?? relic;
        }

        /// <summary>A relic's name in the language now: the mod's Chinese, else "X Relic".</summary>
        public static void Localize(ItemDef d)
        {
            if (Lang.IsSource || Lang.Culture.StartsWith("zh")) { d.Name = d.ZhName; d.Tooltip = Lang.T("大师模式"); return; }
            d.Name = English.TryGetValue(d.ZhName, out var en) ? en : d.ZhName;
            d.Tooltip = Lang.T("大师模式");
        }

        public static int DevDropped;
    }

    internal static partial class EnemyDrops
    {
        /// <summary>Every boss's table ends with its relic, in Master Mode only (ItemDropRule.MasterModeCommonDrop).</summary>
        static void AddRelicRows()
        {
            if (!Relics.Enabled) return;
            foreach (var key in new List<string>(table.Keys))
            {
                var e = table[key];
                if (!e.Boss) continue;
                var relic = Relics.Of(key, null, null);
                if (relic == null) continue;
                var rows = new List<Drop>(e.Drops ?? none) { new Drop { Type = relic.Value, Num = 1, Den = 1, Min = 1, Max = 1, Master = true } };
                e.Drops = rows.ToArray();
                table[key] = e;
                DevRelicRows++;
            }
        }

        public static int DevRelicRows;
    }
}
