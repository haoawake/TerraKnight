using System.Collections.Generic;
using BepInEx.Configuration;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.UI
{
    /// <summary>
    /// Hollow Knight's own map as the fullscreen map (M), the user's 10-30 "原版的地图能不能应用回来，不过是按m打开，然后晶塔，
    /// npc，虫子，长椅什么的依旧显示". The game's map rooms are copied, never touched, onto a layer no game camera draws, and a
    /// private orthographic camera renders them for the map screen: every room the player has been in or has had mapped in its
    /// detailed form, and the rest of an area whose map was bought as Cornifer sketched it (as the game's map has them); the
    /// game's own pins by the game's rules (stag stations, shops, springs, trams, graves, cocoons, roots, once bought from
    /// Iselda), the areas' names. Over them, what Terraria's map marks (WorldMap.DrawMarksOn): the pylons (clicked to go there),
    /// the townsfolk, grubs and benches, Grimm flames, the compass's way, the shade, the players' heads. The wheel zooms about
    /// the cursor, dragging and the movement keys pan, as on Terraria's map; the player stands still while it's open.
    /// With Terraria's map in use (WorldMap.Enabled) this is the view WorldMap opens when Hollow is on.
    /// </summary>
    internal static class FullMap
    {
        static bool open;
        /// <summary>The map is open (Terraria's world map, WorldMap, when that one is in use, in either view).</summary>
        public static bool Open { get => WorldMap.Enabled ? WorldMap.Open : open; private set => open = value; }

        /// <summary>M opens Hollow Knight's map rather than Terraria's (cfg Gameplay.HollowKnightMap, the options' 全屏地图).</summary>
        public static ConfigEntry<bool> HollowSetting;
        /// <summary>Dev: which map a test opens (tests open Terraria's unless they say).</summary>
        public static bool? DevHollow;
        public static bool Hollow => DevHollow ?? (TerraMod.Instance?.AutoTest == null && (HollowSetting == null || HollowSetting.Value));

        public static void SetHollow(bool on)
        {
            if (HollowSetting != null) HollowSetting.Value = on;
        }

        sealed class Room
        {
            public string Name, Area;
            public SpriteRenderer Sr;
            public Vector2 Pos, Size;
            /// <summary>The detailed drawing, and the one shown while only the area's map is bought (null: none, the room unseen till visited).</summary>
            public Sprite Full, Sketch;
            public readonly List<Pin> Pins = new List<Pin>();
        }

        /// <summary>One of the game's pins on a room: shown by the player's flags, as its DeactivateIfPlayerdata components have it.</summary>
        sealed class Pin
        {
            public string Name;
            public Sprite Sprite;
            public Vector2 Pos;
            public bool Shown;
            public readonly List<string> NeedTrue = new List<string>(), NeedFalse = new List<string>();
            /// <summary>Dev: its components and state machines, as found.</summary>
            public string Parts;
        }

        static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        static readonly List<Room> roomList = new List<Room>();
        /// <summary>Where each area with anything shown has its name: the middle of its rooms shown, weighted by their size.</summary>
        static readonly Dictionary<string, Vector2> areaLabels = new Dictionary<string, Vector2>();
        static readonly Dictionary<string, Vector3> areaSums = new Dictionary<string, Vector3>();
        static GameObject root, builtFrom;
        static Camera cam;
        static RenderTexture rt;
        static int layer = -1;
        static Rect extent;
        static Vector2 pan;
        static float zoom = -1f; // screen pixels per map unit (unset: about nine map units tall, an area round the player)
        static Vector2 lastMouse;
        static bool dragging;
        static int openedFrame = -1;
        static Vector2 lastHead;
        static bool haveHead;
        const float RootY = -5000f;

        static readonly Dictionary<string, string> areaNames = new Dictionary<string, string>
        {
            ["Crossroads"] = "遗忘十字路", ["Waterways"] = "皇家水道", ["Cliffs"] = "呼啸悬崖", ["Kingdoms_Edge"] = "王国边缘",
            ["Green_Path"] = "苍绿之径", ["Fog_Canyon"] = "雾之峡谷", ["Fungal Wastes"] = "真菌荒地", ["Queens_Gardens"] = "王后花园",
            ["Deepnest"] = "深巢", ["Town_Tutorial"] = "德特茅斯与国王山道", ["Resting_Grounds"] = "安息之地", ["Crystal Peak"] = "水晶山峰",
            ["Ancient Basin"] = "古老盆地", ["City of Tears"] = "泪水之城",
        };

        /// <summary>Each area's map from Cornifer (GameMap.WorldMap's flags); Dirtmouth's is always shown.</summary>
        static readonly Dictionary<string, string> areaMaps = new Dictionary<string, string>
        {
            ["Ancient Basin"] = "mapAbyss", ["City of Tears"] = "mapCity", ["Cliffs"] = "mapCliffs", ["Crossroads"] = "mapCrossroads",
            ["Crystal Peak"] = "mapMines", ["Deepnest"] = "mapDeepnest", ["Fog_Canyon"] = "mapFogCanyon", ["Fungal Wastes"] = "mapFungalWastes",
            ["Green_Path"] = "mapGreenpath", ["Kingdoms_Edge"] = "mapOutskirts", ["Queens_Gardens"] = "mapRoyalGardens",
            ["Resting_Grounds"] = "mapRestingGrounds", ["Waterways"] = "mapWaterways",
        };

        /// <summary>What a pin marks, by its name on the game's map (a numbered copy as its first, but the Godseeker's).</summary>
        static readonly Dictionary<string, string> pinNames = new Dictionary<string, string>
        {
            ["pin_stag_station"] = "鹿角虫车站", ["pin_spa"] = "温泉", ["pin_tram"] = "电车站", ["pin_blue_health"] = "生命血茧", ["pin_dream_tree"] = "低语之根",
            ["pin_sly"] = "斯莱", ["pin_sly (1)"] = "寻神者", ["pin_mapper"] = "伊塞尔达", ["pin_charm_slug"] = "萨鲁巴", ["pin_leg eater"] = "食腿者",
            ["pin_nailsmith"] = "骨钉匠", ["pin_banker"] = "米莉贝儿", ["pin_relic_dealer"] = "遗物寻求者莱姆", ["pin_jiji"] = "忏悔者吉吉",
            ["pin_grub_king"] = "幼虫之父", ["pin_hunter"] = "猎人", ["pin_dream moth"] = "先知", ["pin_colosseum"] = "愚人斗兽场",
        };

        /// <summary>Dev: the map's rooms by area, whether the current scene is one and has been visited, where the head goes.</summary>
        public static string DevReport()
        {
            var gm = GameManager.UnsafeInstance;
            var pd = PlayerData.instance;
            if (gm == null || pd == null) return "无";
            if (root == null && gm.gameMap != null) Build(gm.gameMap);
            var sb = new System.Text.StringBuilder();
            string scene = gm.sceneName;
            sb.Append($"场景 {scene}：地图里有这个房间 {rooms.ContainsKey(scene)}，到过 {pd.scenesVisited != null && pd.scenesVisited.Contains(scene)}，画过 {pd.scenesMapped != null && pd.scenesMapped.Contains(scene)}，");
            HeadPosition(out bool found);
            sb.Append($"头像定位 {found}；各区域房间数：");
            var byArea = new Dictionary<string, int>();
            foreach (var r in roomList) byArea[r.Area] = byArea.TryGetValue(r.Area, out int n) ? n + 1 : 1;
            foreach (var kv in byArea) sb.Append($"{kv.Key} {kv.Value} ");
            return sb.ToString();
        }

        /// <summary>
        /// Dev: what the map shows under the head: the most opaque pixel of the rendered rooms in a
        /// small square round it (0 = nothing drawn there), and the shown rooms whose box holds it.
        /// </summary>
        public static string DevUnderHead()
        {
            if (!Open || rt == null) return "地图没开";
            var head = HeadPosition(out _);
            var s = ToScreen(head);
            int half = 6;
            int x0 = Mathf.Clamp((int)s.x - half, 0, rt.width - 1), y0 = Mathf.Clamp(rt.height - (int)s.y - half, 0, rt.height - 1);
            int w = Mathf.Min(half * 2, rt.width - x0), h = Mathf.Min(half * 2, rt.height - y0);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            float a = 0f, lum = 0f;
            foreach (var c in tex.GetPixels()) { a = Mathf.Max(a, c.a); lum = Mathf.Max(lum, c.grayscale * c.a); }
            Object.Destroy(tex);
            var sb = new System.Text.StringBuilder();
            foreach (var r in roomList)
            {
                if (!r.Sr.gameObject.activeSelf) continue;
                var b = r.Sr.bounds;
                var o = root.transform.position;
                if (head.x >= b.min.x - o.x && head.x <= b.max.x - o.x && head.y >= b.min.y - o.y && head.y <= b.max.y - o.y) sb.Append(' ').Append(r.Name);
            }
            return $"头像下面画出的地图：不透明度 {a:F2}，亮度 {lum:F2}；框住头像的已显示房间：{(sb.Length > 0 ? sb.ToString() : "无")}";
        }

        /// <summary>Dev: the head's placing worked through: the scene, the player's point in it, the scene's size as the grid and the game's tilemap have it, the room.</summary>
        public static string DevHeadMath()
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            var live = World.TerraWorld.Live;
            if (gm == null || hero == null || live == null || !rooms.TryGetValue(live.SceneName ?? "", out var room)) return "无";
            var local = (Vector2)hero.transform.position - (Vector2)live.CellOffset * World.CellGrid.CellSize;
            var grid = new Vector2(live.Grid.Width, live.Grid.Height) * World.CellGrid.CellSize;
            string tile = gm.tilemap != null ? $"{gm.tilemap.width}x{gm.tilemap.height}" : "无";
            string frame = MapFrames.TryGet(live.SceneName, out var f) ? f.ToString() : "无";
            return $"{live.SceneName} 主角 {local:F1}（世界 {(Vector2)hero.transform.position:F1}，格偏移 {live.CellOffset}），网格 {grid.x:F0}x{grid.y:F0}，原版瓦片图 {tile}，地图框 {frame}，" +
                   $"房间 {room.Pos:F2} 大小 {room.Size:F2}（图 {room.Full.bounds.size:F2}，轴心 {room.Full.pivot / room.Full.rect.size:F2}） → {HeadPosition(out _):F2}";
        }

        /// <summary>Dev: where the head goes on the map (map units).</summary>
        public static Vector2 DevHead(out bool found) => HeadPosition(out found);

        /// <summary>Dev: a scene's bench pins on the map (WorldMap.BenchPins), and how far each is from a point (map units, screen pixels).</summary>
        public static string DevBenchPins(string scene, Vector2 from)
        {
            if (root == null) return "地图还没建";
            var sb = new System.Text.StringBuilder();
            foreach (var b in WorldMap.BenchPins)
            {
                var at = b.Scene == scene ? Place(b.Scene, b.Local) : null;
                if (!at.HasValue) continue;
                float d = Vector2.Distance(at.Value, from);
                sb.Append($" 长椅 {at.Value.x:F2},{at.Value.y:F2}（离 {d:F2}，屏幕上 {d * zoom:F0} 像素）");
            }
            return sb.Length > 0 ? sb.ToString() : "没有";
        }

        /// <summary>Dev: an area's rooms, whether each shows, where it is and its sprite.</summary>
        public static string DevRooms(string area)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in roomList)
                if (r.Area == area)
                {
                    var sp = r.Sr.sprite;
                    var b = r.Sr.bounds;
                    var o = root.transform.position;
                    sb.Append($" {r.Name}{(r.Sr.gameObject.activeSelf ? sp == r.Full ? "[显示]" : "[草图]" : "")}@{r.Pos.x:F2},{r.Pos.y:F2} 大小 {r.Size.x:F2}x{r.Size.y:F2} α{r.Sr.color.a:F2} 图 {sp?.name}" +
                              $"（轴心 {(sp != null ? sp.pivot / sp.rect.size : Vector2.zero):F2}，实际范围 {b.min.x - o.x:F2},{b.min.y - o.y:F2}..{b.max.x - o.x:F2},{b.max.y - o.y:F2}，缩放 {r.Sr.transform.localScale:F2}）；");
                }
            return sb.ToString();
        }

        /// <summary>Dev: the game's pins on the map by kind: how many, the flags they need, how many show now.</summary>
        public static string DevPins()
        {
            var kinds = new SortedDictionary<string, (int all, int shown, string need)>();
            foreach (var r in roomList)
                foreach (var p in r.Pins)
                {
                    string need = string.Join("+", p.NeedTrue) + (p.NeedFalse.Count > 0 ? " 非" + string.Join("+非", p.NeedFalse) : "");
                    kinds.TryGetValue(p.Name, out var k);
                    kinds[p.Name] = (k.all + 1, k.shown + (p.Shown ? 1 : 0), k.all > 0 ? k.need : (need.Length > 0 ? need : "无条件") + "，在 " + r.Name + (!string.IsNullOrEmpty(p.Parts) ? "；" + p.Parts : ""));
                }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in kinds) sb.Append($" {kv.Key}×{kv.Value.all}（显示 {kv.Value.shown}，需要 {kv.Value.need}）");
            return sb.ToString();
        }

        /// <summary>Dev: the areas named on the map and where.</summary>
        public static string DevLabels()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in areaLabels) sb.Append($" {kv.Key}@{kv.Value.x:F2},{kv.Value.y:F2}");
            return sb.ToString();
        }

        /// <summary>Dev: where the view is and where the head is on the screen (the centre is half the screen).</summary>
        public static string DevView()
        {
            var head = HeadPosition(out bool found);
            var s = ToScreen(head);
            return $"头像 {(found ? "找到" : "没找到")}，在屏幕 ({s.x:F0},{s.y:F0})，屏幕中心 ({Screen.width / 2f:F0},{Screen.height / 2f:F0})，缩放 {zoom:F0}";
        }

        /// <summary>Dev: the view centred on a point of a scene (its own units).</summary>
        public static bool DevCentre(string scene, Vector2 local)
        {
            var at = Place(scene, local);
            if (at.HasValue) pan = at.Value;
            return at.HasValue;
        }

        public static void Toggle()
        {
            if (WorldMap.Enabled) { WorldMap.Toggle(); return; }
            if (Open) Close();
            else TryOpen();
        }

        /// <summary>
        /// Right-clicking a pylon (Player.TryOpeningFullscreenMap): the map opens as it always does,
        /// centred on the player (resetMapFull puts the view on the screen's centre), the pylons on it
        /// to click.
        /// </summary>
        public static void OpenForPylons()
        {
            if (WorldMap.Enabled) { if (!WorldMap.Open) WorldMap.TryOpen(); return; }
            if (!Open) TryOpen();
        }

        /// <summary>Where a pylon goes on the map: its scene's room, stretched like the player's head.</summary>
        static bool PylonPosition(World.Pylons.Pylon p, out Vector2 at)
        {
            at = default;
            if (!rooms.TryGetValue(p.Scene, out var room) || p.SceneSize.x <= 0f || p.SceneSize.y <= 0f) return false;
            at = OnRoom(room, p.Scene, p.Local + new Vector2(0.75f, 1f), p.SceneSize);
            return true;
        }

        /// <summary>
        /// Where a point of a scene (in its own coordinates) goes on its room: as GameMap's compass
        /// puts it, over the scene's frame (its tilemap, or the frame the scene sets, MapFrames).
        /// </summary>
        static Vector2 OnRoom(Room room, string scene, Vector2 local, Vector2 sceneSize)
        {
            float ox = 0f, oy = 0f;
            if (MapFrames.TryGet(scene, out var f))
            {
                ox = f.x;
                oy = f.y;
                sceneSize = new Vector2(f.z, f.w);
            }
            return room.Pos - room.Size / 2f + Vector2.Scale(new Vector2((local.x + ox) / sceneSize.x, (local.y + oy) / sceneSize.y), room.Size);
        }

        /// <summary>
        /// Where a point of a scene (its own units) goes on this map, as the compass puts the player: on its room over the scene's
        /// size (loaded, or as Terraria's map or the world atlas has it); null for a scene the map has no room for (Godhome, the
        /// White Palace, the rooms behind doors).
        /// </summary>
        static Vector2? Place(string scene, Vector2 local)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var room)) return null;
            var region = World.TerrainSystem.Find(scene);
            Vector2 size;
            if (region != null) size = new Vector2(region.Grid.Width, region.Grid.Height) * World.CellGrid.CellSize;
            // About 63 tiles of scene to a unit of map.
            else if (!WorldMap.SceneSize(scene, out size)) size = room.Size / 0.0158f;
            return OnRoom(room, scene, local, size);
        }

        /// <summary>The pylon icon under the mouse (screen position, y down), if any.</summary>
        static World.Pylons.Pylon PylonAt(Vector2 guiMouse)
        {
            foreach (var p in World.Pylons.Unlocked)
            {
                if (!PylonPosition(p, out var at)) continue;
                if (PylonIconRect(ToScreen(at)).Contains(guiMouse)) return p;
            }
            return null;
        }

        internal static Rect PylonIconRect(Vector2 center)
        {
            float ui = GameView.Scale;
            return new Rect(center.x - 15f * ui, center.y - 19f * ui, 30f * ui, 38f * ui);
        }

        /// <summary>
        /// The map made ready to show: built from the game's map (again for another game's), its rooms and pins as the player has
        /// them, the view on the player; false: no game map to show.
        /// </summary>
        internal static bool Begin()
        {
            if (!Ensure()) return false;
            Refresh();
            // Terraria opens the map centred on the player.
            var head = HeadPosition(out bool found);
            pan = found || haveHead ? head : extent.center;
            zoom = ClampZoom(zoom > 0f ? zoom : Screen.height / 9f);
            dragging = false;
            openedFrame = Time.frameCount;
            return true;
        }

        /// <summary>The rooms copied from the game's map (again for another game's); false: no game map.</summary>
        static bool Ensure()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.gameMap == null) return false;
            if (root != null && builtFrom != gm.gameMap) Clear();
            return root != null || Build(gm.gameMap);
        }

        static void TryOpen()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || GameHooks.HeldByTheGame() || !Begin()) return;
            Open = true;
            MapHold.Take(hero);
            TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
        }

        public static void Close()
        {
            if (WorldMap.Enabled) { WorldMap.Close(); return; }
            if (!Open) return;
            Open = false;
            dragging = false;
            MapHold.Give(HeroController.SilentInstance);
            TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        static void Clear()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            cam = null;
            rooms.Clear();
            roomList.Clear();
            areaLabels.Clear();
        }

        /// <summary>Copies the game map's rooms, their pins and the areas' names onto a layer of their own.</summary>
        static bool Build(GameObject gameMap)
        {
            layer = FreeLayer();
            if (layer < 0)
            {
                Plugin.Log.LogWarning("[地图] 没有空闲的图层");
                return false;
            }
            builtFrom = gameMap;
            root = new GameObject("Terra Map");
            Object.DontDestroyOnLoad(root);
            root.transform.position = new Vector3(0f, RootY, 0f);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Transform area in gameMap.transform)
            {
                if (!areaNames.ContainsKey(area.name)) continue;
                foreach (Transform t in area)
                {
                    Vector2 pos = (Vector2)(area.localPosition + Vector3.Scale(area.localScale, t.localPosition));
                    var src = t.GetComponent<SpriteRenderer>();
                    if (src == null || src.sprite == null || t.name.Contains("Area Name") || t.name.Contains("Grub Pins")) continue;
                    // A room Cornifer sketched carries its detailed drawing separately, swapped in once the room is mapped; a room
                    // the game's map shows from the start (the area's map bought) is shown so here too, the rest only once visited.
                    Sprite full = src.sprite, sketch = t.gameObject.activeSelf ? src.sprite : null;
                    var rough = t.GetComponent<RoughMapRoom>();
                    if (rough != null && rough.fullSprite != null)
                    {
                        full = rough.fullSprite;
                        if (rough.fullSpriteDisplayed) sketch = sketch != null ? full : null;
                    }
                    var go = new GameObject(t.name) { layer = layer };
                    go.transform.SetParent(root.transform, false);
                    var scale = Vector3.Scale(area.localScale, t.localScale);
                    // The game's own order where rooms overlap: their depth and sorting, as its map draws them.
                    go.transform.localPosition = new Vector3(pos.x, pos.y, Mathf.Clamp(area.localPosition.z + t.localPosition.z, -5f, 5f));
                    go.transform.localRotation = t.localRotation;
                    go.transform.localScale = scale;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = full;
                    sr.color = src.color;
                    sr.sortingLayerID = src.sortingLayerID;
                    sr.sortingOrder = src.sortingOrder;
                    var size = Vector2.Scale(full.bounds.size, scale);
                    var room = new Room { Name = t.name, Area = area.name, Sr = sr, Pos = pos, Size = size, Full = full, Sketch = sketch };
                    if (!rooms.ContainsKey(t.name)) rooms[t.name] = room;
                    roomList.Add(room);
                    var drawn = sr.bounds;
                    var o = root.transform.position;
                    x0 = Mathf.Min(x0, drawn.min.x - o.x); x1 = Mathf.Max(x1, drawn.max.x - o.x);
                    y0 = Mathf.Min(y0, drawn.min.y - o.y); y1 = Mathf.Max(y1, drawn.max.y - o.y);
                    // The game's pins (the benches are Terraria's map's, WorldMap.DrawMapPins, named on hover).
                    foreach (Transform pin in t)
                    {
                        if (!pin.name.StartsWith("pin_") || pin.name == "pin_bench") continue;
                        var psr = pin.GetComponent<SpriteRenderer>();
                        if (psr == null || psr.sprite == null) continue;
                        var p = new Pin { Name = pin.name, Sprite = psr.sprite, Pos = pos + (Vector2)Vector3.Scale(scale, pin.localPosition) };
                        PinFlags(pin, p);
                        room.Pins.Add(p);
                    }
                }
            }
            extent = Rect.MinMaxRect(x0, y0, x1, y1);
            var camGo = new GameObject("Terra Map Camera");
            camGo.transform.SetParent(root.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            Plugin.Log.LogInfo($"[地图] 复制了 {roomList.Count} 个房间，图层 {layer}，范围 {extent}；原版图钉：{DevPins()}；区域名：{DevLabels()}");
            return true;
        }

        /// <summary>
        /// The flags a pin of the game's map needs: its DeactivateIfPlayerdata components' and the bools its state machines test
        /// (a vendor's: Iselda's Vendor Pin and having met him), else Iselda's pin for its kind (stag stations, springs, trams,
        /// cocoons, roots; the rest are vendors and services).
        /// </summary>
        static void PinFlags(Transform pin, Pin p)
        {
            var parts = new List<string>();
            foreach (var c in pin.GetComponents<Component>()) if (c != null && !(c is Transform) && !(c is SpriteRenderer)) parts.Add(c.GetType().Name);
            foreach (var c in pin.GetComponents<DeactivateIfPlayerdataFalse>()) if (!string.IsNullOrEmpty(c.boolName)) p.NeedTrue.Add(c.boolName);
            foreach (var c in pin.GetComponents<DeactivateIfPlayerdataTrue>()) if (!string.IsNullOrEmpty(c.boolName)) p.NeedFalse.Add(c.boolName);
            foreach (var f in pin.GetComponents<PlayMakerFSM>())
            {
                parts.Add("FSM " + f.FsmName);
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        if (a == null || a.GetType().Name != "PlayerDataBoolTest") continue;
                        var v = a.GetType().GetField("boolName")?.GetValue(a) as HutongGames.PlayMaker.FsmString;
                        if (v == null || string.IsNullOrEmpty(v.Value)) continue;
                        parts.Add(st.Name + ":" + v.Value);
                        if (!p.NeedTrue.Contains(v.Value)) p.NeedTrue.Add(v.Value);
                    }
            }
            p.Parts = string.Join(",", parts);
            if (p.NeedTrue.Count > 0) return;
            string n = p.Name;
            p.NeedTrue.Add(n.StartsWith("pin_stag") ? "hasPinStag" : n.StartsWith("pin_spa") ? "hasPinSpa" : n.StartsWith("pin_tram") ? "hasPinTram"
                : n.StartsWith("pin_blue_health") ? "hasPinCocoon" : n.StartsWith("pin_dream_tree") ? "hasPinDreamPlant" : "hasPinShop");
        }

        /// <summary>A layer no camera of the game draws.</summary>
        static int FreeLayer()
        {
            int used = 0;
            foreach (var c in Camera.allCameras) used |= c.cullingMask;
            foreach (int l in new[] { 6, 7, 3, 24, 25, 28 })
                if ((used & (1 << l)) == 0) return l;
            return -1;
        }

        static bool Flag(PlayerData pd, string name)
        {
            try { return pd.GetBool(name); }
            catch (System.Exception) { return false; }
        }

        static readonly HashSet<string> known = new HashSet<string>();

        /// <summary>
        /// Which rooms show and how: in its detailed drawing a room the player has been in or has mapped (every room in a creative
        /// save); as Cornifer drew it a room the area's bought map shows; a pin where its room shows and the player has its flags (GameMap.SetupMap's own
        /// rules for the cocoons and the roots: met, and a root not yet drained).
        /// </summary>
        static void Refresh()
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            known.Clear();
            if (pd.scenesVisited != null) known.UnionWith(pd.scenesVisited);
            if (pd.scenesMapped != null) known.UnionWith(pd.scenesMapped);
            var gm = GameManager.UnsafeInstance;
            if (gm != null) known.Add(gm.sceneName);
            areaSums.Clear();
            // Dirtmouth's sketch comes with the first map bought (hasMap), as on Terraria's map; a creative save has every room.
            bool firstMap = Flag(pd, "hasMap"), everything = pd.mapAllRooms || TerraMod.Instance != null && TerraMod.Instance.Creative;
            foreach (var r in roomList)
            {
                bool bought = areaMaps.TryGetValue(r.Area, out var flag) ? Flag(pd, flag) : firstMap;
                var sprite = everything || known.Contains(r.Name) ? r.Full : bought ? r.Sketch : null;
                r.Sr.gameObject.SetActive(sprite != null);
                if (sprite == null)
                {
                    foreach (var p in r.Pins) p.Shown = false;
                    continue;
                }
                r.Sr.sprite = sprite;
                float weight = r.Size.x * r.Size.y;
                areaSums.TryGetValue(r.Area, out var sum);
                areaSums[r.Area] = sum + new Vector3(r.Pos.x * weight, r.Pos.y * weight, weight);
                foreach (var p in r.Pins)
                {
                    bool on = true;
                    foreach (var b in p.NeedTrue) on &= Flag(pd, b);
                    foreach (var b in p.NeedFalse) on &= !Flag(pd, b);
                    if (p.Name == "pin_blue_health") on &= pd.hasPinCocoon && pd.scenesEncounteredCocoon != null && pd.scenesEncounteredCocoon.Contains(r.Name);
                    if (p.Name == "pin_dream_tree")
                        on &= pd.hasPinDreamPlant && pd.scenesEncounteredDreamPlant != null && pd.scenesEncounteredDreamPlant.Contains(r.Name)
                              && (pd.scenesEncounteredDreamPlantC == null || !pd.scenesEncounteredDreamPlantC.Contains(r.Name));
                    p.Shown = on;
                }
            }
            areaLabels.Clear();
            foreach (var kv in areaSums)
                if (kv.Value.z > 0f) areaLabels[kv.Key] = new Vector2(kv.Value.x / kv.Value.z, kv.Value.y / kv.Value.z);
        }

        /// <summary>
        /// GameMap's compass: the scene stretched onto its room's sprite, the player at his place
        /// within the scene (in the scene's own coordinates, its tilemap's size).
        /// </summary>
        static Vector2 HeadPosition(out bool found)
        {
            found = false;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            var live = World.TerraWorld.Live;
            if (gm == null || hero == null || live == null) return haveHead ? lastHead : extent.center;
            Vector2 at = hero.transform.position;
            // A scene the map has no room for (the Stag Nest): from the nearest scene round it
            // that has one, the player's place carried on past that room's edge.
            var region = live;
            if (!rooms.TryGetValue(live.SceneName ?? gm.sceneName, out var room))
            {
                region = null;
                float best = float.MaxValue;
                foreach (var t in World.TerrainSystem.All)
                {
                    var r = t.Region;
                    if (r == null || r.SceneName == null || !rooms.ContainsKey(r.SceneName)) continue;
                    var min = (Vector2)r.CellOffset * World.CellGrid.CellSize;
                    var max = min + new Vector2(r.Grid.Width, r.Grid.Height) * World.CellGrid.CellSize;
                    float d = (at - Vector2.Max(min, Vector2.Min(max, at))).sqrMagnitude;
                    if (d < best) { best = d; region = r; }
                }
                if (region == null) return haveHead ? lastHead : extent.center;
                room = rooms[region.SceneName];
            }
            var local = at - (Vector2)region.CellOffset * World.CellGrid.CellSize;
            var sceneSize = new Vector2(region.Grid.Width, region.Grid.Height) * World.CellGrid.CellSize;
            var p = OnRoom(room, region.SceneName, local, sceneSize);
            found = true;
            lastHead = p;
            haveHead = true;
            return p;
        }

        /// <summary>Input and rendering while open without Terraria's map in use (WorldMap hands its input to Input); call every frame.</summary>
        public static void Update()
        {
            if (WorldMap.Enabled) { WorldMap.Update(); return; }
            var kb = Net.NetChat.BlocksKeys ? null : Keyboard.current;
            if (kb != null && Player.TerraKeys.Pressed(Player.TerraKeys.Action.FullMap) && TerraMod.Instance?.AutoTest == null) Toggle();
            if (!Open) return;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.IsInSceneTransition || hero.cState.dead || gm.isPaused || kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            Input(Time.frameCount == openedFrame);
        }

        static float ClampZoom(float z)
        {
            float min = Mathf.Min(Screen.width / Mathf.Max(1f, extent.width), Screen.height / Mathf.Max(1f, extent.height)) * 0.8f;
            return Mathf.Clamp(z, min, 900f);
        }

        /// <summary>
        /// Input while open: the map keys zoom about the middle and the wheel about the cursor (the point under it stays put), the
        /// mouse drags and the movement keys pan, as on Terraria's map; a pylon clicked goes there, a teammate's head with a
        /// Wormhole Potion carried goes to him, the corner button closes. Then the rooms rendered.
        /// </summary>
        internal static void Input(bool justOpened)
        {
            var kb = Net.NetChat.BlocksKeys ? null : Keyboard.current;
            var mouse = Mouse.current;
            float keyZoom = (Player.TerraKeys.Held(Player.TerraKeys.Action.MapZoomIn) ? 1f : 0f) - (Player.TerraKeys.Held(Player.TerraKeys.Action.MapZoomOut) ? 1f : 0f);
            if (keyZoom != 0f) zoom = ClampZoom(zoom * Mathf.Pow(keyZoom > 0f ? 1.05f : 0.95f, Time.unscaledDeltaTime * 60f));
            if (mouse != null)
            {
                Vector2 m = TerraCursor.ScreenMouse(mouse);
                var gui = new Vector2(m.x, Screen.height - m.y);
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.1f)
                {
                    var before = ToMap(gui);
                    zoom = ClampZoom(zoom * (wheel > 0f ? 1.25f : 0.8f));
                    pan += before - ToMap(gui);
                }
                bool pressed = TerraCursor.LeftPressed(mouse) && !justOpened;
                // TeleportPylonsMapLayer: clicking a pylon on the map goes there.
                var pylon = pressed ? PylonAt(gui) : null;
                if (pylon != null)
                {
                    Close();
                    World.Pylons.Teleport(pylon);
                    return;
                }
                var mate = pressed ? WorldMap.MateOn(place, toScreen, gui, 16f * GameView.Scale) : null;
                if (mate != null)
                {
                    Close();
                    World.Wormhole.Go(mate);
                    return;
                }
                if (pressed && CloseButton().Contains(gui))
                {
                    Close();
                    return;
                }
                if (pressed) dragging = true;
                if (!TerraCursor.LeftHeld(mouse)) dragging = false;
                if (dragging) pan -= (m - lastMouse) / zoom;
                lastMouse = m;
            }
            // Player.cs: the movement keys pan the fullscreen map, 16 screen pixels a tick.
            if (kb != null)
            {
                var dir = Vector2.zero;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Up)) dir.y += 1f;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Down)) dir.y -= 1f;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Left)) dir.x -= 1f;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Right)) dir.x += 1f;
                pan += dir * 16f * Time.unscaledDeltaTime * 60f / zoom;
            }
            pan.x = Mathf.Clamp(pan.x, extent.xMin, extent.xMax);
            pan.y = Mathf.Clamp(pan.y, extent.yMin, extent.yMax);
            Render();
        }

        static void Render()
        {
            if (cam == null) return;
            if (rt == null || rt.width != Screen.width || rt.height != Screen.height)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32) { name = "Terra Map" };
            }
            RenderTo(rt, pan, zoom);
        }

        /// <summary>The rooms round a point of the map at a zoom (screen pixels a map unit) into a picture.</summary>
        static void RenderTo(RenderTexture target, Vector2 centre, float z)
        {
            cam.targetTexture = target;
            cam.orthographicSize = target.height / 2f / z;
            cam.transform.position = new Vector3(centre.x, RootY + centre.y, -10f);
            cam.Render();
        }

        // ---------------------------------------------------------------- the corner maps

        /// <summary>
        /// About how many of the world's cells a unit of the game's map spans (its rooms are drawn some 66 scene units wide and
        /// 45 high to a unit, two cells a unit): the corner maps' zoom, kept in Terraria's terms (pixels a cell), in its own.
        /// </summary>
        internal const float CellsPerUnit = 110f;
        static RenderTexture cornerRt;
        static Rect cornerArea;
        static Vector2 cornerCentre;
        static float cornerZoom, nextRefresh;
        static string refreshedIn;
        static bool cornerReady;

        /// <summary>
        /// The minimap's or the overlay's picture for this frame (WorldMap's corner styles with this map chosen): the rooms round
        /// the player at a zoom (screen pixels a map unit), of a size; false: no game map to show. The rooms shown kept up as
        /// the player goes: on a new scene, and every two seconds.
        /// </summary>
        internal static bool RenderCorner(Vector2 size, float pixels)
        {
            cornerReady = false;
            var gm = GameManager.UnsafeInstance;
            if (size.x < 2f || size.y < 2f || !Ensure()) return false;
            if (gm.sceneName != refreshedIn || Time.unscaledTime >= nextRefresh)
            {
                refreshedIn = gm.sceneName;
                nextRefresh = Time.unscaledTime + 2f;
                Refresh();
            }
            int w = Mathf.CeilToInt(size.x), h = Mathf.CeilToInt(size.y);
            if (cornerRt == null || cornerRt.width != w || cornerRt.height != h)
            {
                if (cornerRt != null) cornerRt.Release();
                cornerRt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "Terra Corner Map" };
            }
            cornerCentre = HeadPosition(out _);
            cornerZoom = pixels;
            RenderTo(cornerRt, cornerCentre, pixels);
            cornerReady = true;
            return true;
        }

        public static bool CornerReady => cornerReady;

        static Vector2 CornerPoint(Vector2 p) =>
            new Vector2(cornerArea.x + cornerArea.width / 2f + (p.x - cornerCentre.x) * cornerZoom, cornerArea.y + cornerArea.height / 2f - (p.y - cornerCentre.y) * cornerZoom);

        /// <summary>
        /// The corner map drawn into an area (the minimap's group, or the screen for the overlay): the rooms at an opacity, the
        /// game's pins and Terraria's marks at another, the players' heads at a scale, the player's in the middle.
        /// </summary>
        internal static void DrawCorner(Rect area, float alpha, float markAlpha, float marks, float heads)
        {
            if (!cornerReady || cornerRt == null) return;
            cornerArea = area;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(area, cornerRt);
            GUI.color = new Color(1f, 1f, 1f, markAlpha);
            DrawPins(toCorner, 22f * marks, area, null);
            WorldMap.DrawMarksOn(place, toCorner, marks, null, area, false, markAlpha);
            GUI.color = prev;
            WorldMap.DrawOthersOn(place, toCorner, heads, area, false);
            DrawHead(CornerPoint(cornerCentre), heads);
        }

        /// <summary>Dev: the corner map's picture, centre and zoom.</summary>
        public static string DevCorner() => cornerReady && cornerRt != null ? $"{cornerRt.width}x{cornerRt.height}，中心 {cornerCentre:F2}，缩放 {cornerZoom:F0}" : "没有";

        internal static Rect CloseButton() => new Rect(10f, Screen.height - 40f * GameView.Scale - 10f, 32f * GameView.Scale, 30f * GameView.Scale);

        static Vector2 ToScreen(Vector2 p) => new Vector2(Screen.width / 2f + (p.x - pan.x) * zoom, Screen.height / 2f - (p.y - pan.y) * zoom);
        static Vector2 ToMap(Vector2 gui) => new Vector2(pan.x + (gui.x - Screen.width / 2f) / zoom, pan.y - (gui.y - Screen.height / 2f) / zoom);

        static GUIStyle style, labelStyle;

        // The marks' placing and the views' screens, made once: no garbage a frame (the game's collector is off in play).
        static readonly System.Func<string, Vector2, Vector2?> place = Place;
        static readonly System.Func<Vector2, Vector2> toScreen = ToScreen, toCorner = CornerPoint;

        public static void Draw()
        {
            if (WorldMap.Enabled) { WorldMap.Draw(); return; }
            if (Open) DrawView();
        }

        /// <summary>The map on the screen: the rooms, the areas' names, the pins and marks, the player, the name under the mouse.</summary>
        internal static void DrawView()
        {
            if (Event.current.type != EventType.Repaint || rt == null) return;
            float ui = GameView.Scale;
            // The map's background: dark, as the game's map screen.
            var prev = GUI.color;
            GUI.color = new Color(0.02f, 0.025f, 0.04f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
            if (DevCells) DevCellsDrawn = WorldMap.DrawCellsOn(place, toScreen);
            else GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt);
            var mp = TerraCursor.GuiMouse();
            // Zoomed far out the icons shrink a little, so the whole map isn't buried under them.
            float icon = ui * Mathf.Clamp(zoom / (Screen.height / 9f), 0.6f, 1f);

            // The areas' names, in the middle of what is shown of them.
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelStyle.fontSize = Mathf.RoundToInt(22 * ui);
            foreach (var kv in areaLabels)
            {
                if (!areaNames.TryGetValue(kv.Key, out var name)) continue;
                var at = ToScreen(kv.Value);
                BorderText.Draw(new Rect(at.x - 200f * ui, at.y - 16f * ui, 400f * ui, 32f * ui), World.Areas.Display(name), labelStyle, new Color(0f, 0f, 0f, 0.85f), new Color(0.93f, 0.89f, 0.78f), 1.5f * ui);
            }

            // The game's own pins, at a constant size.
            string hovered = DrawPins(toScreen, 22f * icon, null, mp);

            // The shade, with Terraria's death marker.
            var pd = PlayerData.instance;
            if (pd != null && !string.IsNullOrEmpty(pd.shadeScene) && pd.shadeScene != "None")
            {
                var shade = Place(pd.shadeScene, new Vector2(pd.shadePositionX, pd.shadePositionY));
                if (shade.HasValue)
                {
                    var tex = TerrariaAssets.Texture("MapDeath");
                    var s = ToScreen(shade.Value);
                    GUI.DrawTexture(new Rect(s.x - tex.width * ui / 2f, s.y - tex.height * ui / 2f, tex.width * ui, tex.height * ui), tex);
                }
            }
            // Pylons found so far (Extra_182, one frame per style), bigger under the mouse.
            World.Pylons.Pylon hoveredPylon = null;
            var icons = TerrariaAssets.Texture("Extra_182");
            foreach (var p in World.Pylons.Unlocked)
            {
                if (!PylonPosition(p, out var at)) continue;
                var r = PylonIconRect(ToScreen(at));
                if (r.Contains(mp))
                {
                    hoveredPylon = p;
                    r = new Rect(r.center.x - r.width * 0.65f, r.center.y - r.height * 0.65f, r.width * 1.3f, r.height * 1.3f);
                }
                GUI.DrawTextureWithTexCoords(r, icons, new Rect(p.Style * 30f / icons.width, 0f, 30f / icons.width, 1f));
            }
            // Terraria's map's marks: townsfolk, Grimm flames, grubs and benches, the compass's way.
            string marked = WorldMap.DrawMarksOn(place, toScreen, icon, mp);
            // The player's head, then the other players'.
            DrawHead(ToScreen(HeadPosition(out _)), ui);
            WorldMap.DrawOthersOn(place, toScreen, ui);

            // What is under the cursor: a pylon, a mark, a pin, else the room's area.
            string label = hoveredPylon != null ? Lang.T(hoveredPylon.Name) + "（" + World.Areas.Display(World.Areas.AreaName(hoveredPylon.Scene)) + "）　" + Lang.T("点击传送")
                : marked ?? hovered;
            if (label == null)
            {
                var mw = ToMap(mp);
                foreach (var r in roomList)
                {
                    if (!r.Sr.gameObject.activeSelf) continue;
                    if (Mathf.Abs(mw.x - r.Pos.x) > r.Size.x / 2f || Mathf.Abs(mw.y - r.Pos.y) > r.Size.y / 2f) continue;
                    label = areaNames.TryGetValue(r.Area, out var n) ? World.Areas.Display(n) : r.Area;
                    break;
                }
            }
            WorldMap.DevLabel = label;
            if (!string.IsNullOrEmpty(label))
                Outlined(new Rect(mp.x + 26f * ui, mp.y - 10f * ui, 600f * ui, 24f * ui), label, hoveredPylon != null ? new Color(1f, 0.9f, 0.5f) : Color.white);
            // Bottom-left close button (Map_0, Map_4 under the mouse).
            var close = CloseButton();
            GUI.DrawTexture(close, TerrariaAssets.Texture(close.Contains(mp) ? "Map_4" : "Map_0"));
            Outlined(new Rect(close.xMax + 10f * ui, close.y + 4f * ui, 700f * ui, 24f * ui), Lang.T("滚轮缩放，拖动平移，鼠标指着看是什么，M 关闭"), new Color(1f, 1f, 1f, 0.7f));
        }

        /// <summary>The game's pins shown, at a size, within a clip; returns the name of the one under the mouse.</summary>
        static string DrawPins(System.Func<Vector2, Vector2> at, float size, Rect? clip, Vector2? mouse)
        {
            string hovered = null;
            foreach (var r in roomList)
                foreach (var p in r.Pins)
                {
                    if (!p.Shown) continue;
                    var c = at(p.Pos);
                    if (clip.HasValue && !clip.Value.Contains(c)) continue;
                    var tr = p.Sprite.textureRect;
                    float k = size / Mathf.Max(tr.width, tr.height);
                    var rect = new Rect(c.x - tr.width * k / 2f, c.y - tr.height * k / 2f, tr.width * k, tr.height * k);
                    WorldMap.DrawSprite(p.Sprite, rect);
                    if (mouse.HasValue && rect.Contains(mouse.Value)) hovered = PinLabel(p, r);
                }
            return hovered;
        }

        /// <summary>A pin's name on hover and its area.</summary>
        static string PinLabel(Pin p, Room r)
        {
            int copy = p.Name.IndexOf(" (");
            if (!pinNames.TryGetValue(p.Name, out var what) && (copy < 0 || !pinNames.TryGetValue(p.Name.Substring(0, copy), out what))) return null;
            return Lang.T(what) + "（" + World.Areas.Display(areaNames.TryGetValue(r.Area, out var n) ? n : r.Area) + "）";
        }

        // Terraria's skin, eye and hair colours for the default player.
        static readonly Color Skin = new Color(1f, 125 / 255f, 90 / 255f), Eye = new Color(105 / 255f, 90 / 255f, 75 / 255f), Hair = new Color(215 / 255f, 90 / 255f, 55 / 255f);

        /// <summary>The player's head (Terraria draws the real head layers, outlined, at a constant size).</summary>
        internal static void DrawHead(Vector2 at, float ui)
        {
            var inv = TerraMod.Instance?.Inventory;
            var head = inv == null ? default : inv.VanityArmor[0].IsEmpty ? inv.EffectiveArmor[0] : inv.VanityArmor[0];
            int headArmor = !head.IsEmpty ? head.Def.ArmorSheet : 0;
            bool hairShows = headArmor == 0 || head.Type == Items.ItemType.MiningHelmet || head.Type == Items.ItemType.JungleHat;
            // The save's character (Player.Appearance): its clothing style's head, its hair style and colours.
            DrawHead(at, ui, headArmor, hairShows, Player.Appearance.Current, Color.black);
        }

        /// <summary>A player's head on the map in their own look (Main.DrawPlayerHead): its rim, skin, eyes, hair style, helmet.</summary>
        internal static void DrawHead(Vector2 at, float ui, int headArmor, bool hairShows, Player.Appearance look, Color rim)
        {
            float s = 1.6f * ui;
            CharacterPortrait.DrawHead(new Rect(at.x - 20f * s, at.y - 14f * s, 40f * s, 28f * s), look, rim, hairShows, headArmor);
        }

        /// <summary>A player's head on the map (Main.DrawPlayerHead): its rim (a team's colour for another player), skin, eyes, hair, helmet.</summary>
        internal static void DrawHead(Vector2 at, float ui, int headArmor, bool hairShows, Color skin, Color hair, Color eye, Color rim)
        {
            float s = 1.6f * ui;
            var rect = new Rect(at.x - 20f * s, at.y - 14f * s, 40f * s, 28f * s);
            // A dark rim first, then the parts.
            foreach (var off in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) })
                Part(new Rect(rect.x + off.x * ui, rect.y + off.y * ui, rect.width, rect.height), "Player_0_0", rim);
            Part(rect, "Player_0_0", skin);
            Part(rect, "Player_0_1", Color.white);
            Part(rect, "Player_0_2", eye);
            if (hairShows) Part(rect, "Player_Hair_1", hair, hair: true);
            if (headArmor > 0) Part(rect, "Armor_Head_" + headArmor, Color.white);
        }

        /// <summary>The top 28 pixels (the head) of frame 0 of a 40x56 player sheet.</summary>
        static void Part(Rect r, string texture, Color tint, bool hair = false)
        {
            var tex = TerrariaAssets.Texture(texture);
            float frameH = hair ? 54f : 56f;
            var uv = new Rect(0f, 1f - 28f / tex.height, 40f / tex.width, 28f / tex.height);
            var prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = prev;
            _ = frameH;
        }

        internal static void Outlined(Rect r, string text, Color color)
        {
            float ui = GameView.Scale;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            style.fontSize = Mathf.RoundToInt(18 * ui);
            BorderText.Draw(r, text, style, new Color(0f, 0f, 0f, 0.85f), color, 1.5f * ui);
        }

        /// <summary>Dev: multiply the zoom.</summary>
        public static void DevZoom(float factor) => zoom = ClampZoom(zoom * factor);

        /// <summary>Dev: the zoom set (screen pixels per map unit; 0: the first view's).</summary>
        public static void DevZoomTo(float pixels) => zoom = ClampZoom(pixels > 0f ? pixels : Screen.height / 9f);

        /// <summary>Dev: open or close without the key.</summary>
        public static void DevSetOpen(bool open)
        {
            if (WorldMap.Enabled) { if (open && !WorldMap.Open) WorldMap.TryOpen(); else if (!open && WorldMap.Open) WorldMap.Close(); return; }
            if (open && !Open) TryOpen();
            else if (!open && Open) Close();
        }

        public static int RoomCount => roomList.Count;
        public static int ShownRooms
        {
            get { int n = 0; foreach (var r in roomList) if (r.Sr.gameObject.activeSelf) n++; return n; }
        }
        public static int SketchedRooms
        {
            get { int n = 0; foreach (var r in roomList) if (r.Sr.gameObject.activeSelf && r.Sr.sprite != r.Full) n++; return n; }
        }
        /// <summary>Dev: Terraria's map's cells drawn into the game's rooms instead of its drawings (WorldMap.DrawCellsOn), and how many rooms were.</summary>
        public static bool DevCells;
        public static int DevCellsDrawn;

        /// <summary>
        /// Dev: how the game's map draws each room against the scene's real size: scene units a map unit across and up (the frame
        /// a scene sets for the compass, else its cells), one line a room: name, area, across, up.
        /// </summary>
        public static string DevScales()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in roomList)
            {
                Vector2 size;
                if (MapFrames.TryGet(r.Name, out var f)) size = new Vector2(f.z, f.w);
                else if (!WorldMap.SceneSize(r.Name, out size)) continue;
                sb.Append($"{r.Name}\t{r.Area}\t{size.x / r.Size.x:F1}\t{size.y / r.Size.y:F1}\t{size.x:F0}\t{size.y:F0}\n");
            }
            return sb.ToString();
        }

        public static int ShownPins
        {
            get { int n = 0; foreach (var r in roomList) foreach (var p in r.Pins) if (p.Shown) n++; return n; }
        }
    }
}
