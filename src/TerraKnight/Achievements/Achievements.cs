using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>The achievements' tabs (docs/成就列表-*.md's twelve kinds, in its order).</summary>
    internal enum Cat { Journey, Boss, Challenge, Collect, Explore, Casino, Fishing, Home, Combat, Death, Fun, Multi }

    /// <summary>One achievement: its number, its place in the table (and the icon atlas), its tab, stars, whether it hides.</summary>
    internal sealed class Def
    {
        public readonly string Id;
        public readonly int Index;
        public readonly Cat Cat;
        public readonly int Stars;
        public readonly bool Hidden;
        readonly string zh, zhCond, en, enCond;

        /// <summary>A count it is reached by (<see cref="Achievements.Count"/>) and how much, for its progress bar; null for none.</summary>
        public string Counter;
        public long Goal;

        public Def(string id, int index, Cat cat, int stars, bool hidden, string zh, string zhCond, string en, string enCond)
        {
            Id = id; Index = index; Cat = cat; Stars = stars; Hidden = hidden;
            this.zh = zh; this.zhCond = zhCond; this.en = en; this.enCond = enCond;
        }

        public string Name => Pick(zh, en);
        public string Condition => Pick(zhCond, enCond);

        /// <summary>Chinese as written (Traditional through the mod's table where it has the line), English otherwise.</summary>
        static string Pick(string zh, string en) => Lang.IsSource ? zh : Lang.Culture.StartsWith("zh") ? Lang.T(zh) : en;
    }

    internal static partial class AchievementDefs
    {
        static Dictionary<string, Def> byId;

        public static Def Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Def>();
                foreach (var d in All) byId[d.Id] = d;
            }
            return id != null && byId.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>A count-reached achievement's count and goal (its progress bar).</summary>
        public static void Goal(string id, string counter, long goal)
        {
            var d = Get(id);
            if (d == null) return;
            d.Counter = counter;
            d.Goal = goal;
        }
    }

    /// <summary>
    /// The achievements (docs/成就列表-*.md), as Terraria's: the player's own across all their saves, kept in
    /// BepInEx/TerraKnight/achievements.txt (the time each was got, and the counts some are reached by). None are got in a
    /// creative save. A new one shows Terraria's popup (<see cref="AchievementPopup"/>) with its sound.
    /// </summary>
    internal static class Achievements
    {
        static readonly Dictionary<string, long> got = new Dictionary<string, long>();
        static readonly Dictionary<string, long> counts = new Dictionary<string, long>();
        static bool loaded, dirty;
        static float saveAt;

        public static int DevUnlocked;

        static string FilePath => Path.Combine(BepInEx.Paths.BepInExRootPath, "TerraKnight", "achievements.txt");

        /// <summary>Got in this game: not in a creative save, nor while no save is being played.</summary>
        public static bool Counting
        {
            get
            {
                var mod = TerraMod.Instance;
                var gm = GameManager.UnsafeInstance;
                return mod != null && !mod.Creative && gm != null && GameHooks.IsGameplay(gm);
            }
        }

        public static int GotCount
        {
            get
            {
                Load();
                return got.Count;
            }
        }

        public static bool Has(string id)
        {
            Load();
            return got.ContainsKey(id);
        }

        /// <summary>When it was got (local time), or null.</summary>
        public static DateTime? GotAt(string id)
        {
            Load();
            return got.TryGetValue(id, out long t) ? DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime : (DateTime?)null;
        }

        /// <summary>Got now (once; the popup and its sound), when the game counts.</summary>
        public static bool Unlock(string id)
        {
            Load();
            var def = AchievementDefs.Get(id);
            if (def == null || got.ContainsKey(id) || !Counting) return false;
            got[id] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DevUnlocked++;
            Save();
            AchievementPopup.Show(def);
            Plugin.Log.LogInfo($"[成就] 达成 {id} {def.Name}");
            return true;
        }

        public static long Count(string counter)
        {
            Load();
            return counter != null && counts.TryGetValue(counter, out long v) ? v : 0;
        }

        /// <summary>A count raised (while the game counts); its new value.</summary>
        public static long Add(string counter, long n = 1)
        {
            Load();
            if (!Counting || n == 0) return Count(counter);
            counts.TryGetValue(counter, out long v);
            v += n;
            counts[counter] = v;
            Changed();
            return v;
        }

        /// <summary>A count kept at its highest.</summary>
        public static long Max(string counter, long value)
        {
            Load();
            counts.TryGetValue(counter, out long v);
            if (!Counting || value <= v) return v;
            counts[counter] = value;
            Changed();
            return value;
        }

        /// <summary>A count set (a streak back to nothing).</summary>
        public static void Set(string counter, long value)
        {
            Load();
            counts.TryGetValue(counter, out long v);
            if (!Counting || v == value) return;
            counts[counter] = value;
            Changed();
        }

        /// <summary>A count reached its goal: the achievement got.</summary>
        public static void AddTo(string id, string counter, long goal, long n = 1)
        {
            if (Add(counter, n) >= goal) Unlock(id);
        }

        static void Changed()
        {
            if (dirty) return;
            dirty = true;
            saveAt = Time.unscaledTime + 10f;
        }

        /// <summary>Every frame: the counts written now and then (and the trackers' checks, AchievementTracker).</summary>
        public static void Tick()
        {
            if (dirty && Time.unscaledTime >= saveAt) Save();
            AchievementTracker.Tick();
        }

        public static void Flush()
        {
            if (dirty) Save();
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            Application.quitting += Flush;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    var p = raw.Split('\t');
                    if (p.Length < 3 || !long.TryParse(p[2], out long v)) continue;
                    if (p[0] == "U") got[p[1]] = v;
                    else if (p[0] == "C") counts[p[1]] = v;
                }
                Plugin.Log.LogInfo($"[成就] 读入：已达成 {got.Count} 个，计数 {counts.Count} 项");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[成就] 读不了 {FilePath}：{e.Message}");
            }
        }

        static void Save()
        {
            dirty = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var lines = new List<string> { "# TerraKnight achievements: U <id> <unix time got>, C <count> <value>" };
                foreach (var kv in got) lines.Add("U\t" + kv.Key + "\t" + kv.Value);
                foreach (var kv in counts) lines.Add("C\t" + kv.Key + "\t" + kv.Value);
                string tmp = FilePath + ".tmp";
                File.WriteAllLines(tmp, lines);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[成就] 写不了 {FilePath}：{e.Message}");
            }
        }

        /// <summary>Dev: every achievement got (or none), as if over time; nothing popped up.</summary>
        public static void DevSetAll(bool on)
        {
            Load();
            got.Clear();
            if (on)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var d in AchievementDefs.All) got[d.Id] = now;
            }
            Save();
        }

        /// <summary>Dev: these got, these not (the rest as they were).</summary>
        public static void DevSet(string id, bool on)
        {
            Load();
            if (on) got[id] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            else got.Remove(id);
            Save();
        }

        // ------------------------------------------------------------------ icons

        static Texture2D atlas;
        static bool[] drawn;
        const int Icon = 64, Columns = 16;

        /// <summary>The icon atlas (tools/achievements_gen.py: 16 a row, 64x64, the table's order).</summary>
        public static Texture2D Atlas
        {
            get
            {
                if (atlas != null) return atlas;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerraKnight.AchievementIcons.png"))
                {
                    if (stream == null) return null;
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "AchievementIcons", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    ImageConversion.LoadImage(atlas, bytes, false);
                    atlas.filterMode = FilterMode.Point;
                }
                // An icon not drawn yet is clear: its middle pixel tells.
                drawn = new bool[AchievementDefs.All.Length];
                var px = atlas.GetPixels32();
                for (int i = 0; i < drawn.Length; i++)
                {
                    var c = IconRect(i);
                    int x = (int)c.x + Icon / 2, y = atlas.height - (int)c.y - Icon / 2;
                    drawn[i] = x < atlas.width && y >= 0 && px[y * atlas.width + x].a > 0;
                }
                return atlas;
            }
        }

        /// <summary>An icon's place in the atlas, in pixels from its top left.</summary>
        public static Rect IconRect(int index) => new Rect((index % Columns) * Icon, (index / Columns) * Icon, Icon, Icon);

        /// <summary>Its icon has been drawn (the atlas has it).</summary>
        public static bool IconDrawn(Def d) => Atlas != null && drawn != null && d.Index < drawn.Length && drawn[d.Index];

        /// <summary>Its icon into a rect (nothing when it hasn't been drawn).</summary>
        public static void DrawIcon(Rect dst, Def d, Color tint)
        {
            if (!IconDrawn(d)) return;
            var r = IconRect(d.Index);
            UI.TerraUi.Part(dst, Atlas, r.x, r.y, r.width, r.height, tint);
        }
    }
}
