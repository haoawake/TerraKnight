using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's fullscreen map (M) showing Hollow Knight's own map. The game's map rooms are
    /// copied, never touched, onto a layer no game camera draws, and a private orthographic
    /// camera renders them for the map screen: every room the player has been in or has had
    /// mapped, in its detailed form, on Terraria's parchment, with the player's head where he is
    /// and Terraria's death marker on the shade. The wheel zooms, dragging pans, as in Terraria;
    /// the player stands still while it's open.
    /// </summary>
    internal static class FullMap
    {
        static bool open;
        /// <summary>The map is open (Terraria's world map, WorldMap, when that one is in use).</summary>
        public static bool Open { get => WorldMap.Enabled ? WorldMap.Open : open; private set => open = value; }

        sealed class Room
        {
            public string Name, Area;
            public SpriteRenderer Sr;
            public Vector2 Pos, Size;
        }

        static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        static readonly List<Room> roomList = new List<Room>();
        static GameObject root;
        static Camera cam;
        static RenderTexture rt;
        static int layer = -1;
        static Rect extent;
        static Vector2 pan;
        static float zoom = 156f; // screen pixels per map unit
        static Vector2 lastMouse;
        static bool dragging;
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
            sb.Append("；gameMap 下的区域：");
            if (gm.gameMap != null)
                foreach (Transform area in gm.gameMap.transform)
                {
                    int kids = area.childCount;
                    string sample = kids > 0 ? area.GetChild(0).name + (kids > 1 ? "," + area.GetChild(1).name : "") : "";
                    sb.Append($"{area.name}({kids}{(areaNames.ContainsKey(area.name) ? "" : " 未收录")}:{sample}) ");
                }
            sb.Append($"；到过的场景 {pd.scenesVisited?.Count ?? 0} 个，其中悬崖：");
            if (pd.scenesVisited != null) foreach (var s in pd.scenesVisited) if (s.StartsWith("Cliffs")) sb.Append(s).Append(' ');
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

        /// <summary>Dev: where the head goes on the map (map units).</summary>
        public static Vector2 DevHead(out bool found) => HeadPosition(out found);

        /// <summary>Dev: the bench pins of a scene's rooms on the map, and how far each is from a point (map units, screen pixels).</summary>
        public static string DevBenchPins(string scene, Vector2 from)
        {
            if (root == null) return "地图还没建";
            var sb = new System.Text.StringBuilder();
            foreach (var r in roomList)
            {
                if (r.Name != scene && !r.Name.StartsWith(scene + "_")) continue;
                foreach (Transform pin in r.Sr.transform)
                {
                    if (pin.name != "bench") continue;
                    Vector2 at = pin.position - root.transform.position;
                    float d = Vector2.Distance(at, from);
                    sb.Append($" {r.Name} 的长椅 {at.x:F2},{at.y:F2}（离 {d:F2}，屏幕上 {d * zoom:F0} 像素）");
                }
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
                    sb.Append($" {r.Name}{(r.Sr.gameObject.activeSelf ? "[显示]" : "")}@{r.Pos.x:F2},{r.Pos.y:F2} 大小 {r.Size.x:F2}x{r.Size.y:F2} α{r.Sr.color.a:F2} 图 {sp?.name}" +
                              $"（轴心 {(sp != null ? sp.pivot / sp.rect.size : Vector2.zero):F2}，实际范围 {b.min.x - o.x:F2},{b.min.y - o.y:F2}..{b.max.x - o.x:F2},{b.max.y - o.y:F2}，缩放 {r.Sr.transform.localScale:F2}）；");
                }
            return sb.ToString();
        }

        /// <summary>Dev: where the view is and where the head is on the screen (the centre is half the screen).</summary>
        public static string DevView()
        {
            var head = HeadPosition(out bool found);
            var s = ToScreen(head);
            return $"头像 {(found ? "找到" : "没找到")}，在屏幕 ({s.x:F0},{s.y:F0})，屏幕中心 ({Screen.width / 2f:F0},{Screen.height / 2f:F0})";
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

        static void TryOpen()
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.gameMap == null) return;
            if (root == null && !Build(gm.gameMap)) return;
            Open = true;
            Refresh();
            // Terraria opens the map centred on the player.
            var head = HeadPosition(out bool found);
            pan = found || haveHead ? head : extent.center;
            hero.RelinquishControl();
            TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
        }

        public static void Close()
        {
            if (WorldMap.Enabled) { WorldMap.Close(); return; }
            if (!Open) return;
            Open = false;
            dragging = false;
            var hero = HeroController.SilentInstance;
            if (hero != null) hero.RegainControl();
            TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>Copies the game map's rooms (and their bench pins) onto a layer of their own.</summary>
        static bool Build(GameObject gameMap)
        {
            layer = FreeLayer();
            if (layer < 0)
            {
                Plugin.Log.LogWarning("[地图] 没有空闲的图层");
                return false;
            }
            root = new GameObject("Terra Map");
            Object.DontDestroyOnLoad(root);
            root.transform.position = new Vector3(0f, RootY, 0f);
            var roughType = HarmonyLib.AccessTools.TypeByName("RoughMapRoom");
            var fullSprite = roughType != null ? HarmonyLib.AccessTools.Field(roughType, "fullSprite") : null;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Transform area in gameMap.transform)
            {
                if (!areaNames.ContainsKey(area.name)) continue;
                foreach (Transform t in area)
                {
                    var src = t.GetComponent<SpriteRenderer>();
                    if (src == null || src.sprite == null || t.name.Contains("Area Name") || t.name.Contains("Grub Pins")) continue;
                    // A Cornifer sketch room carries its detailed sprite separately.
                    var sprite = src.sprite;
                    if (fullSprite != null)
                    {
                        var rough = t.GetComponent(roughType);
                        if (rough != null && fullSprite.GetValue(rough) is Sprite full && full != null) sprite = full;
                    }
                    var go = new GameObject(t.name) { layer = layer };
                    go.transform.SetParent(root.transform, false);
                    Vector2 pos = (Vector2)(area.localPosition + Vector3.Scale(area.localScale, t.localPosition));
                    var scale = Vector3.Scale(area.localScale, t.localScale);
                    go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                    go.transform.localRotation = t.localRotation;
                    go.transform.localScale = scale;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = src.color;
                    sr.sortingOrder = 0;
                    var size = Vector2.Scale(sprite.bounds.size, scale);
                    var room = new Room { Name = t.name, Area = area.name, Sr = sr, Pos = pos, Size = size };
                    if (!rooms.ContainsKey(t.name)) rooms[t.name] = room;
                    roomList.Add(room);
                    x0 = Mathf.Min(x0, pos.x - size.x / 2f); x1 = Mathf.Max(x1, pos.x + size.x / 2f);
                    y0 = Mathf.Min(y0, pos.y - size.y / 2f); y1 = Mathf.Max(y1, pos.y + size.y / 2f);
                    // Benches: where to shop and rest.
                    foreach (Transform pin in t)
                    {
                        if (pin.name != "pin_bench") continue;
                        var psr = pin.GetComponent<SpriteRenderer>();
                        if (psr == null || psr.sprite == null) continue;
                        var pg = new GameObject("bench") { layer = layer };
                        pg.transform.SetParent(go.transform, false);
                        pg.transform.localPosition = pin.localPosition - Vector3.forward * 0.1f;
                        pg.transform.localScale = pin.localScale;
                        var p2 = pg.AddComponent<SpriteRenderer>();
                        p2.sprite = psr.sprite;
                        p2.sortingOrder = 1;
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
            Plugin.Log.LogInfo($"[地图] 复制了 {roomList.Count} 个房间，图层 {layer}，范围 {extent}");
            return true;
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

        /// <summary>Which rooms show: those the player has been in or has mapped.</summary>
        static void Refresh()
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            var known = new HashSet<string>();
            if (pd.scenesVisited != null) known.UnionWith(pd.scenesVisited);
            if (pd.scenesMapped != null) known.UnionWith(pd.scenesMapped);
            var gm = GameManager.UnsafeInstance;
            if (gm != null) known.Add(gm.sceneName);
            foreach (var r in roomList) r.Sr.gameObject.SetActive(known.Contains(r.Name) || pd.mapAllRooms);
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

        static Vector2 ShadePosition(out bool found)
        {
            found = false;
            var pd = PlayerData.instance;
            if (pd == null || string.IsNullOrEmpty(pd.shadeScene) || pd.shadeScene == "None" || !rooms.TryGetValue(pd.shadeScene, out var room)) return default;
            // The shade's scene may not be loaded: about 63 tiles of scene to a unit of map.
            var size = room.Size / 0.0158f;
            var region = World.TerrainSystem.Find(pd.shadeScene);
            if (region != null) size = new Vector2(region.Grid.Width, region.Grid.Height) * World.CellGrid.CellSize;
            found = true;
            return OnRoom(room, pd.shadeScene, new Vector2(pd.shadePositionX, pd.shadePositionY), size);
        }

        /// <summary>Input and rendering while open; call every frame.</summary>
        public static void Update()
        {
            if (WorldMap.Enabled) { WorldMap.Update(); return; }
            var kb = Net.NetChat.BlocksKeys ? null : Keyboard.current;
            if (kb != null && Player.TerraKeys.Pressed(Player.TerraKeys.Action.FullMap)) Toggle();
            if (!Open) return;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.IsInSceneTransition || hero.cState.dead || gm.isPaused || kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                // Player.cs: the wheel zooms 1.3 times a notch about the view centre; dragging grabs the map.
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) zoom *= 1.3f;
                else if (wheel < -0.1f) zoom /= 1.3f;
                float min = Screen.width * 0.6f / Mathf.Max(1f, extent.width);
                zoom = Mathf.Clamp(zoom, min, 900f);
                Vector2 m = mouse.position.ReadValue();
                var guiMouse = new Vector2(m.x, Screen.height - m.y);
                // TeleportPylonsMapLayer: clicking a pylon on the map goes there.
                var pylon = mouse.leftButton.wasPressedThisFrame ? PylonAt(guiMouse) : null;
                if (pylon != null)
                {
                    Close();
                    World.Pylons.Teleport(pylon);
                    return;
                }
                if (mouse.leftButton.wasPressedThisFrame && !CloseButton().Contains(guiMouse)) dragging = true;
                if (!mouse.leftButton.isPressed) dragging = false;
                if (dragging) pan -= (m - lastMouse) / zoom;
                lastMouse = m;
                if (mouse.leftButton.wasPressedThisFrame && CloseButton().Contains(new Vector2(m.x, Screen.height - m.y))) Close();
            }
            pan.x = Mathf.Clamp(pan.x, extent.xMin, extent.xMax);
            pan.y = Mathf.Clamp(pan.y, extent.yMin, extent.yMax);
            Render();
        }

        static void Render()
        {
            if (rt == null || rt.width != Screen.width || rt.height != Screen.height)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32) { name = "Terra Map" };
            }
            cam.targetTexture = rt;
            cam.orthographicSize = Screen.height / 2f / zoom;
            cam.transform.position = new Vector3(pan.x, RootY + pan.y, -10f);
            cam.Render();
        }

        internal static Rect CloseButton() => new Rect(10f, Screen.height - 40f * GameView.Scale - 10f, 32f * GameView.Scale, 30f * GameView.Scale);

        static Vector2 ToScreen(Vector2 p) => new Vector2(Screen.width / 2f + (p.x - pan.x) * zoom, Screen.height / 2f - (p.y - pan.y) * zoom);

        static GUIStyle style;

        public static void Draw()
        {
            if (WorldMap.Enabled) { WorldMap.Draw(); return; }
            if (!Open || Event.current.type != EventType.Repaint || rt == null) return;
            float ui = GameView.Scale;
            // The map's background: dark, then Terraria's parchment over the map's extent.
            var prev = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.96f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
            var a = ToScreen(new Vector2(extent.xMin - 1f, extent.yMax + 1f));
            var b = ToScreen(new Vector2(extent.xMax + 1f, extent.yMin - 1f));
            GUI.DrawTexture(Rect.MinMaxRect(a.x, a.y, b.x, b.y), TerrariaAssets.Texture("Map"));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt);

            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            style.fontSize = Mathf.RoundToInt(18 * ui);

            // Terraria's death marker on the shade.
            var shade = ShadePosition(out bool hasShade);
            if (hasShade)
            {
                var tex = TerrariaAssets.Texture("MapDeath");
                var s = ToScreen(shade);
                GUI.DrawTexture(new Rect(s.x - tex.width * ui / 2f, s.y - tex.height * ui / 2f, tex.width * ui, tex.height * ui), tex);
            }
            // Pylons found so far (Extra_182, one frame per style), bigger under the mouse.
            var mouseNow = Event.current.mousePosition;
            World.Pylons.Pylon hoveredPylon = null;
            var icons = TerrariaAssets.Texture("Extra_182");
            foreach (var p in World.Pylons.Unlocked)
            {
                if (!PylonPosition(p, out var at)) continue;
                var r = PylonIconRect(ToScreen(at));
                bool over = r.Contains(mouseNow);
                if (over)
                {
                    hoveredPylon = p;
                    r = new Rect(r.center.x - r.width * 0.65f, r.center.y - r.height * 0.65f, r.width * 1.3f, r.height * 1.3f);
                }
                GUI.DrawTextureWithTexCoords(r, icons, new Rect(p.Style * 30f / icons.width, 0f, 30f / icons.width, 1f));
            }
            // The player's head.
            var head = ToScreen(HeadPosition(out _));
            DrawHead(head, ui);

            // Hovered room: its area's name, as Terraria names what's under the cursor.
            var mp = Event.current.mousePosition;
            if (hoveredPylon != null)
            {
                string where = rooms.TryGetValue(hoveredPylon.Scene, out var pr) && areaNames.TryGetValue(pr.Area, out var an) ? an : hoveredPylon.Scene;
                Outlined(new Rect(mp.x + 26f * ui, mp.y - 10f * ui, 500f * ui, 24f * ui), Lang.T(hoveredPylon.Name) + "（" + World.Areas.Display(where) + "）　" + Lang.T("点击传送"), new Color(1f, 0.9f, 0.5f));
            }
            var mw = new Vector2(pan.x + (mp.x - Screen.width / 2f) / zoom, pan.y - (mp.y - Screen.height / 2f) / zoom);
            foreach (var r in roomList)
            {
                if (hoveredPylon != null) break;
                if (!r.Sr.gameObject.activeSelf) continue;
                if (Mathf.Abs(mw.x - r.Pos.x) > r.Size.x / 2f || Mathf.Abs(mw.y - r.Pos.y) > r.Size.y / 2f) continue;
                Outlined(new Rect(mp.x + 26f * ui, mp.y - 10f * ui, 400f * ui, 24f * ui), areaNames.TryGetValue(r.Area, out var n) ? World.Areas.Display(n) : r.Area, Color.white);
                break;
            }
            // Bottom-left close button (Map_0, Map_4 under the mouse).
            var close = CloseButton();
            GUI.DrawTexture(close, TerrariaAssets.Texture(close.Contains(mp) ? "Map_4" : "Map_0"));
            Outlined(new Rect(close.xMax + 10f * ui, close.y + 4f * ui, 600f * ui, 24f * ui), Lang.T("滚轮缩放，拖动平移，M 关闭"), new Color(1f, 1f, 1f, 0.7f));
        }

        // Terraria's skin, eye and hair colours for the default player.
        static readonly Color Skin = new Color(1f, 125 / 255f, 90 / 255f), Eye = new Color(105 / 255f, 90 / 255f, 75 / 255f), Hair = new Color(215 / 255f, 90 / 255f, 55 / 255f);

        /// <summary>The player's head (Terraria draws the real head layers, outlined, at a constant size).</summary>
        internal static void DrawHead(Vector2 at, float ui)
        {
            var inv = TerraMod.Instance?.Inventory;
            var head = inv != null ? inv.EffectiveArmor[0] : default;
            int headArmor = !head.IsEmpty ? head.Def.ArmorSheet : 0;
            bool hairShows = headArmor == 0 || head.Type == Items.ItemType.MiningHelmet || head.Type == Items.ItemType.JungleHat;
            // In a LAN game the player's chosen look, as the others see them.
            var look = Net.NetSession.State != Net.NetSession.Phase.Off ? Player.TerraPlayerSprite.Looks[Mathf.Clamp(Net.NetSettings.Look.Value, 0, Player.TerraPlayerSprite.Looks.Length - 1)] : default;
            bool own = Net.NetSession.State != Net.NetSession.Phase.Off;
            DrawHead(at, ui, headArmor, hairShows, own ? look.Skin : Skin, own ? look.Hair : Hair, own ? look.Eye : Eye, Color.black);
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
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
                if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        /// <summary>Dev: multiply the zoom.</summary>
        public static void DevZoom(float factor) => zoom *= factor;

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
    }
}
