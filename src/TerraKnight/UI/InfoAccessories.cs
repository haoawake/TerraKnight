using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's information accessories (Main.DrawInfoAccs): carried anywhere in the bag or worn,
    /// each shows a line at the top right, in Terraria's order (watch, weather, moon, fishing, ore,
    /// rare creature, enemies, kill count, DPS, speed, compass, depth), its icon (UI/InfoIcon_N) then
    /// its text, 22 px apart: under the minimap when the minimap is in its frame (GetInfoAccIconPosition:
    /// Y + 261), else under the hearts. Drawn while the inventory is closed.
    /// In Hallownest: the watches tell Terraria's clock (TerraTime: 4:30 AM at daybreak, 7:30 PM at
    /// dusk; copper and tin to the hour, silver and tungsten to the half hour, gold and platinum to the
    /// minute); the weather is the area's (rain in the City of Tears, ash on Kingdom's Edge, wind on the
    /// Howling Cliffs, clear elsewhere); the moon is the Skeleton Merchant's; nothing is fished; the
    /// treasure is the nearest Life Crystal or geo rock; a rare creature is a Menderbug or an elite;
    /// the compass and the depth meter measure from Dirtmouth on the world map (a tile is two feet; the
    /// Abyss is the underworld). With the inventory open they are a row of their icons instead (DrawToggles), a click on
    /// one hiding its line or showing it again (Player.hideInfo, kept in the save).
    /// </summary>
    [HarmonyPatch]
    internal static class InfoAccessories
    {
        /// <summary>accThirdEye's radius: 2000 px.</summary>
        const float RadarRange = 2000f / 32f;
        /// <summary>accCritterGuide's: 1300 px.</summary>
        const float CritterRange = 1300f / 32f;
        const float OreRange = 50f;
        static int number, nextCount;
        static string rare, ore;
        static GUIStyle style;
        static float ticks;

        /// <summary>Dev: the last count.</summary>
        public static int DevEnemiesNear => number;
        /// <summary>Dev: the lines drawn last.</summary>
        public static readonly List<string> DevLines = new List<string>();

        /// <summary>What the carried accessories show (Player.UpdateEquips' acc* flags).</summary>
        struct Shows
        {
            public int Watch;
            public bool Weather, Calendar, Fish, Ore, Critter, ThirdEye, Jar, Dps, Stopwatch, Compass, Depth;
            public bool Any => Watch > 0 || Weather || Calendar || Fish || Ore || Critter || ThirdEye || Jar || Dps || Stopwatch || Compass || Depth;
        }

        static void Add(ref Shows s, ItemType t)
        {
            switch (t)
            {
                case ItemType.CopperWatch: case ItemType.TinWatch: s.Watch = Mathf.Max(s.Watch, 1); break;
                case ItemType.SilverWatch: case ItemType.TungstenWatch: s.Watch = Mathf.Max(s.Watch, 2); break;
                case ItemType.GoldWatch: case ItemType.PlatinumWatch: s.Watch = 3; break;
                case ItemType.DepthMeter: s.Depth = true; break;
                case ItemType.Compass: s.Compass = true; break;
                case ItemType.GPS: s.Watch = 3; s.Depth = s.Compass = true; break;
                case ItemType.FishermansGuide: s.Fish = true; break;
                case ItemType.WeatherRadio: s.Weather = true; break;
                case ItemType.Sextant: s.Calendar = true; break;
                case ItemType.FishFinder: s.Fish = s.Weather = s.Calendar = true; break;
                case ItemType.MetalDetector: s.Ore = true; break;
                case ItemType.LifeformAnalyzer: s.Critter = true; break;
                case ItemType.Radar: s.ThirdEye = true; break;
                case ItemType.TallyCounter: s.Jar = true; break;
                case ItemType.DPSMeter: s.Dps = true; break;
                case ItemType.Stopwatch: s.Stopwatch = true; break;
                case ItemType.GoblinTech: s.Dps = s.Ore = s.Stopwatch = true; break;
                case ItemType.REK: s.ThirdEye = s.Critter = s.Jar = true; break;
                case ItemType.PDA: case ItemType.CellPhone:
                    s.Watch = 3;
                    s.Depth = s.Compass = s.Fish = s.Weather = s.Calendar = s.Ore = s.Critter = s.ThirdEye = s.Jar = s.Dps = s.Stopwatch = true;
                    break;
            }
        }

        static Shows Gather(Inventory inv)
        {
            var s = new Shows();
            foreach (var x in inv.Slots) if (!x.IsEmpty) Add(ref s, x.Type);
            foreach (var x in inv.FunctionalAccessories) if (!x.IsEmpty) Add(ref s, x.Type);
            return s;
        }

        public static bool HasRadar(Inventory inv) => Gather(inv).ThirdEye;

        // ------------------------------------------------------------------ what is measured

        /// <summary>Player.addDPS / getDPS: the damage dealt since a first hit, per second; ended 3 s after the last.</summary>
        static float dpsStart = -1f, dpsLast, dpsEnd;
        static int dpsDamage;

        /// <summary>Damage the player dealt (the numbers over enemies).</summary>
        public static void Dealt(int damage)
        {
            float now = Time.time;
            if (dpsStart < 0f) { dpsStart = now; dpsDamage = 0; }
            dpsDamage += damage;
            dpsLast = dpsEnd = now;
        }

        static int Dps()
        {
            float now = Time.time;
            if (dpsStart < 0f) return 0;
            if (now - dpsLast >= 3f) { dpsStart = -1f; return 0; }
            float span = dpsEnd - dpsStart;
            if (span >= 3f)
            {
                dpsDamage = (int)(dpsDamage / span);
                dpsStart = now - 1f;
                span = dpsEnd - dpsStart;
            }
            return span <= 0f ? dpsDamage : (int)(dpsDamage / Mathf.Max(1f, span));
        }

        /// <summary>The last enemy the player struck (lastCreatureHit), for the Tally Counter.</summary>
        static string lastHit;

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        static void Struck(HealthManager __instance)
        {
            if (__instance == null || __instance.isDead) return;
            string rec = EnemyHover.RecordOf(__instance);
            if (rec != null) lastHit = rec;
        }

        /// <summary>Player.speedSlice: the speed averaged over a few ticks, px a tick.</summary>
        static readonly float[] slices = new float[60];

        /// <summary>Every frame in play: the counters, refreshed every 15 ticks as Terraria's.</summary>
        public static void Tick(Inventory inv, HeroController hero)
        {
            if (hero == null) return;
            var s = Gather(inv);
            if (!s.Any) return;
            ticks += Time.deltaTime * 60f;
            if (s.Stopwatch)
            {
                var rb = hero.GetComponent<Rigidbody2D>();
                float v = rb != null ? rb.linearVelocity.magnitude / TerraMovement.PxT : 0f;
                int n = Mathf.Clamp((int)(1f + v * 6f), 1, slices.Length);
                for (int k = n - 1; k > 0; k--) slices[k] = slices[k - 1];
                slices[0] = v;
            }
            if (ticks < 15f && nextCount > 0) return;
            ticks = 0f;
            nextCount = 1;
            Vector2 at = hero.transform.position;
            if (s.ThirdEye) number = Count(at);
            if (s.Critter) rare = Rarest(at);
            if (s.Ore) ore = Treasure(at);
        }

        public static int Count(Vector2 from)
        {
            int n = 0;
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || !info.Harmful || EnemyStats.LifeMax(info) <= 5) continue;
                if (Vector2.Distance(hm.transform.position, from) < RadarRange) n++;
            }
            return n;
        }

        /// <summary>The rarest creature near (npc.rarity): a Menderbug, else an elite or a boss.</summary>
        static string Rarest(Vector2 from)
        {
            string best = null;
            int bestRank = 0;
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || Vector2.Distance(hm.transform.position, from) >= CritterRange) continue;
                string rec = EnemyHover.RecordOf(hm);
                if (rec == null) continue;
                int rank = rec == "MenderBug" ? 3 : EnemyDrops.IsBossRecord(rec) ? 2 : EnemyDrops.IsEliteRecord(rec) ? 1 : 0;
                if (rank > bestRank) { bestRank = rank; best = rec; }
            }
            return best != null ? Lang.T(EnemyDrops.ZhName(best)) : null;
        }

        /// <summary>The most precious thing near (Player.accOreFinder): a Life Crystal lying, else a geo rock.</summary>
        static string Treasure(Vector2 from)
        {
            foreach (var p in LifeCrystals.Lying())
                if (Vector2.Distance(p, from) < OreRange) return ItemDb.Get(ItemType.LifeCrystal)?.Name;
            foreach (var r in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None))
                if (r != null && r.gameObject.activeInHierarchy && Vector2.Distance(r.transform.position, from) < OreRange) return Lang.T("吉欧石");
            return null;
        }

        // ------------------------------------------------------------------ the lines

        static string G(string key, string zh, params object[] args)
        {
            string s = TerrariaText.Get(Lang.Culture, "GameUI." + key) ?? Lang.T(zh);
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        /// <summary>The watch's line (Main.DrawInfoAccs, accWatch 1-3) from Terraria's clock.</summary>
        public static string ClockText() => WatchText(3);
        static string WatchText(int watch)
        {
            float now = TerraTime.Now;
            double t = (now < TerraTime.DaySeconds ? now : now - TerraTime.DaySeconds) * 60.0;
            if (!TerraTime.IsDay) t += 54000.0;
            double h = t / 86400.0 * 24.0 - 7.5 - 12.0;
            if (h < 0.0) h += 24.0;
            string ampm = h >= 12.0 ? G("TimePastMorning", "下午") : G("TimeAtMorning", "上午");
            int hour = (int)h;
            int min = (int)((h - hour) * 60.0);
            string mm = watch == 1 ? "00" : watch == 2 ? (min < 30 ? "00" : "30") : min.ToString("00");
            if (hour > 12) hour -= 12;
            if (hour == 0) hour = 12;
            return hour + ":" + mm + " " + ampm;
        }

        static string Weather(string scene)
        {
            if (scene != null && scene.StartsWith("Ruins")) return G("Rain", "雨");
            if (scene != null && scene.StartsWith("Deepnest_East")) return G("Sandstorm", "沙尘暴") + ", " + G("EastWind", "{0} mph 东风", 18);
            if (scene != null && scene.StartsWith("Cliffs")) return G("Clear", "晴") + ", " + G("WestWind", "{0} mph 西风", 31);
            return G("Clear", "晴");
        }

        static string Moon()
        {
            switch (TerraKnight.Town.TownNpcs.MoonPhase)
            {
                case 0: return G("FullMoon", "满月");
                case 1: return G("WaningGibbous", "亏凸月");
                case 2: return G("ThirdQuarter", "下弦月");
                case 3: return G("WaningCrescent", "残月");
                case 4: return G("NewMoon", "新月");
                case 5: return G("WaxingCrescent", "娥眉月");
                case 6: return G("FirstQuarter", "上弦月");
                default: return G("WaxingGibbous", "盈凸月");
            }
        }

        /// <summary>Where the player is on the world map, in tiles (cells), Dirtmouth's middle at 0.</summary>
        static bool MapTile(HeroController hero, out Vector2 tile)
        {
            tile = default;
            var region = TerrainSystem.Current?.Region;
            if (region == null || !WorldMap.TryLayout(region.SceneName, out var origin) || !WorldMap.TryLayout("Town", out var town)) return false;
            var cell = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.min);
            var local = cell - region.CellRect.min;
            tile = new Vector2(origin.x + local.x - (town.x + 263), origin.y + local.y - (town.y + 20));
            return true;
        }

        static string CompassText(HeroController hero)
        {
            if (!MapTile(hero, out var t)) return G("CompassCenter", "中心");
            int feet = Mathf.RoundToInt(t.x * 2f);
            return feet > 0 ? G("CompassEast", "{0}' 东", feet) : feet < 0 ? G("CompassWest", "{0}' 西", -feet) : G("CompassCenter", "中心");
        }

        static string DepthText(HeroController hero)
        {
            string scene = TerrainSystem.Current?.Region?.SceneName;
            if (!MapTile(hero, out var t)) return G("DepthLevel", "水平") + " " + G("LayerSurface", "地表");
            int feet = Mathf.RoundToInt(-t.y * 2f);
            string layer = scene != null && scene.StartsWith("Abyss") ? G("LayerUnderworld", "地狱")
                : feet > 1200 ? G("LayerCaverns", "洞穴") : feet > 60 ? G("LayerUnderground", "地下")
                : feet < -400 ? G("LayerSpace", "太空") : G("LayerSurface", "地表");
            string depth = feet != 0 ? G("Depth", "{0}'", Mathf.Abs(feet)) : G("DepthLevel", "水平");
            return depth + " " + layer;
        }

        static string Speed()
        {
            int n = slices.Length;
            float sum = 0f;
            for (int k = 0; k < n; k++) sum += slices[k];
            float avg = sum / n;
            // 216000 / 42240: px a tick into miles an hour.
            return G("Speed", "{0} mph", Mathf.RoundToInt(avg * 216000f / 42240f));
        }

        static readonly Color Grey = new Color(100 / 255f, 100 / 255f, 100 / 255f);

        static List<(int icon, string text, Color color)> Lines(Inventory inv, HeroController hero)
        {
            var s = Gather(inv);
            var lines = new List<(int, string, Color)>();
            if (!s.Any || hero == null) return lines;
            string scene = TerrainSystem.Current?.Region?.SceneName;
            if (s.Watch > 0) lines.Add((0, WatchText(s.Watch), Color.white));
            if (s.Weather) lines.Add((1, Weather(scene), Color.white));
            if (s.Calendar) lines.Add((7, Moon(), Color.white));
            if (s.Fish) lines.Add((2, Items.Fishing.InfoLine() ?? G("FishingPower", "{0} 渔力", 0), Grey));
            if (s.Ore) lines.Add((10, ore != null ? G("OreDetected", "附近有{0}！", ore) : G("NoTreasureNearby", "附近没有宝藏"), ore != null ? Color.white : Grey));
            if (s.Critter) lines.Add((11, rare ?? G("NoRareCreatures", "附近没有稀有生物"), rare != null ? Color.white : Grey));
            if (s.ThirdEye)
                lines.Add((5, number == 0 ? G("NoEnemiesNearby", "附近没有敌人") : number == 1 ? G("OneEnemyNearby", "附近有1个敌人！") : G("EnemiesNearby", "附近有{0}个敌人！", number),
                    number == 0 ? Grey : Color.white));
            if (s.Jar)
            {
                int kills = lastHit != null && Bestiary.Kills.TryGetValue(lastHit, out int k) ? k : -1;
                lines.Add((6, kills >= 0 ? Lang.T(EnemyDrops.ZhName(lastHit)) + ": " + kills : G("NoKillCount", "没有击杀数"), kills >= 0 ? Color.white : Grey));
            }
            if (s.Dps)
            {
                int dps = Dps();
                lines.Add((12, dps == 0 ? G("NoDPS", "N/A") : G("DPS", "每秒伤害：{0}", dps), dps == 0 ? Grey : Color.white));
            }
            if (s.Stopwatch) lines.Add((9, Speed(), Color.white));
            if (s.Compass) lines.Add((3, CompassText(hero), Color.white));
            if (s.Depth) lines.Add((4, DepthText(hero), Color.white));
            return lines;
        }

        // ------------------------------------------------------------------ hidden lines (Player.hideInfo)

        /// <summary>The save's flag for a hidden line (TerraMod.GrantedAbilities), by its icon (hideInfo's index: the calendar's 7 for both its icons).</summary>
        static string HideFlag(int icon) => "info.hide." + icon;

        public static bool Hidden(int icon)
        {
            var mod = TerraMod.Instance;
            return mod != null && mod.GrantedAbilities.Contains(HideFlag(icon));
        }

        public static void ToggleHidden(int icon)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            if (!mod.GrantedAbilities.Remove(HideFlag(icon))) mod.GrantedAbilities.Add(HideFlag(icon));
        }

        /// <summary>A line's name (Lang.inter 95-106), what its icon says when hovered.</summary>
        static string Name(int icon)
        {
            int key;
            string zh;
            switch (icon)
            {
                case 0: key = 95; zh = "时间"; break;
                case 1: key = 96; zh = "天气"; break;
                case 2: key = 97; zh = "钓鱼"; break;
                case 3: key = 98; zh = "位置"; break;
                case 4: key = 99; zh = "深度"; break;
                case 5: key = 100; zh = "生物数量"; break;
                case 6: key = 101; zh = "击杀数"; break;
                case 7: key = 102; zh = "月相"; break;
                case 9: key = 103; zh = "移动速度"; break;
                case 10: key = 104; zh = "宝藏"; break;
                case 11: key = 105; zh = "稀有生物"; break;
                default: key = 106; zh = "每秒伤害"; break;
            }
            return TerrariaText.Get(Lang.Culture, "LegacyInterface." + key) ?? Lang.T(zh);
        }

        /// <summary>Main.OurFavoriteColor: a hovered icon's highlight.</summary>
        static readonly Color Favorite = new Color(1f, 231f / 255f, 69f / 255f);
        static int hoverWas = -1;

        /// <summary>
        /// The inventory open (Main.DrawInfoAccs; GetInfoAccIconPosition's row): each carried one's icon, 20 px apart from
        /// the right edge - 290, under the minimap in its frame (Y 94 + 261) or under the hearts (94); a hidden one greyed
        /// (80, 80, 80, 70); hovered, Terraria's highlight (InfoIcon_13, 2 px out) and the line's name, a tick each time
        /// the pointer comes onto another. Not while a chest or a townsperson's talk is open (CanShowInfoAccs). Returns
        /// the icons' places with their indices, and the hovered one's name.
        /// </summary>
        public static List<(int icon, Rect rect)> DrawToggles(Inventory inv, Rect view, float ui, Vector2 mouse, out string hover)
        {
            hover = null;
            var rects = new List<(int, Rect)>();
            if (World.Chests.IsOpen || TerraKnight.Town.TownNpcs.ChatOpen) return rects;
            var lines = Lines(inv, HeroController.SilentInstance);
            float x0 = view.xMax - 290f * ui;
            float y = view.y + ((WorldMap.MinimapVisible ? 94f + 261f : 94f) + ResourceHud.ExtraHeartRows * ResourceHud.RowStep) * ui;
            int n = 0, over = -1;
            var prev = GUI.color;
            foreach (var (icon, _, _) in lines)
            {
                var tex = TerrariaAssets.Texture("UI/InfoIcon_" + icon);
                if (tex == null) continue;
                var r = new Rect(x0 + 20f * n * ui, y, tex.width * ui, tex.height * ui);
                n++;
                GUI.color = Hidden(icon) ? new Color(80f / 255f, 80f / 255f, 80f / 255f, 70f / 255f) : Color.white;
                GUI.DrawTexture(r, tex);
                GUI.color = prev;
                if (r.Contains(mouse))
                {
                    over = icon;
                    hover = Name(icon);
                    var hl = TerrariaAssets.Texture("UI/InfoIcon_13");
                    if (hl != null)
                    {
                        GUI.color = Favorite;
                        GUI.DrawTexture(new Rect(r.x - 2f * ui, r.y - 2f * ui, hl.width * ui, hl.height * ui), hl);
                        GUI.color = prev;
                    }
                }
                rects.Add((icon, r));
            }
            if (over >= 0 && over != hoverWas) TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            hoverWas = over;
            return rects;
        }

        public static void Draw(Inventory inv, TerraStats stats)
        {
            if (Event.current.type != EventType.Repaint || InventoryGui.Open) return;
            var hero = HeroController.SilentInstance;
            var lines = Lines(inv, hero);
            lines.RemoveAll(l => Hidden(l.icon));
            DevLines.Clear();
            foreach (var l in lines) DevLines.Add(l.text);
            if (lines.Count == 0) return;
            var view = GameView.Rect;
            float ui = GameView.Scale;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(18 * ui);
            // GetInfoAccIconPosition: X = screenWidth - 280, Y = -32 (+261 under the minimap in its frame); a line's icon at Y + 74 + 52 + 22 i, its text 4 px higher, 20 px right.
            float x = view.xMax - 280f * ui;
            float y0 = view.y + ((WorldMap.MinimapVisible ? -32f + 261f : -32f) + 74f + 52f + ResourceHud.ExtraHeartRows * ResourceHud.RowStep) * ui;
            float step = (Screen.height < 650 ? 20f : 22f) * ui;
            for (int i = 0; i < lines.Count; i++)
            {
                var (icon, text, color) = lines[i];
                float y = y0 + i * step;
                var tex = TerrariaAssets.Texture("UI/InfoIcon_" + icon);
                if (tex != null) GUI.DrawTexture(new Rect(x, y, tex.width * ui, tex.height * ui), tex);
                var r = new Rect(x + 20f * ui + (tex != null ? tex.width * ui - 14f * ui : 0f), y - 4f * ui, 260f * ui, 26f * ui);
                style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                        if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, style);
                style.normal.textColor = color;
                GUI.Label(r, text, style);
            }
        }
    }
}
