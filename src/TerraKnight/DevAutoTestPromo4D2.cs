using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.0, recorder D): the map. Dirtmouth's street walked with the Grimmchild along and the minimap in its
    /// corner (then the overlay map); the full map opened over the town (the townsfolk's heads hovered), drawn back to the
    /// whole new world layout and dragged about; a gate's arrow hovered (its dashed line and "通往：…") and clicked; the
    /// "神居" button; the Grimm flames to find. The map's own view (pan, zoom) is eased by the clip where a person would
    /// use the wheel; every press is the drawn pointer's.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly System.Type d4Map = typeof(UI.WorldMap);

        static Vector2 D4MapPan
        {
            get => (Vector2)AccessTools.Field(d4Map, "pan").GetValue(null);
            set => AccessTools.Field(d4Map, "pan").SetValue(null, value);
        }

        static float D4MapZoom
        {
            get => (float)AccessTools.Field(d4Map, "zoom").GetValue(null);
            set => UI.WorldMap.DevZoom(value);
        }

        static Vector2 D4MapScreen(Vector2 cell) => (Vector2)AccessTools.Method(d4Map, "ToScreen").Invoke(null, new object[] { cell });

        static bool D4ScenePoint(string scene, Vector2 local, out Vector2 cell)
        {
            var args = new object[] { scene, local, null };
            bool ok = (bool)AccessTools.Method(d4Map, "ScenePoint").Invoke(null, args);
            cell = ok ? (Vector2)args[2] : default;
            return ok;
        }

        /// <summary>Nothing of the map's that a press would act on under a point (an arrow, a pylon, a tab).</summary>
        static bool D4MapFree(Vector2 gui)
        {
            if (AccessTools.Method(d4Map, "GateAt").Invoke(null, new object[] { gui }) != null) return false;
            if (AccessTools.Method(d4Map, "PylonAt").Invoke(null, new object[] { gui }) != null) return false;
            if (UI.FullMap.CloseButton().Contains(gui)) return false;
            for (int i = 0; i < 3; i++) if (UI.WorldMap.DevLayerTab(i).Contains(gui)) return false;
            var tab = (Rect)AccessTools.Field(d4Map, "godhomeTab").GetValue(null);
            return !tab.Contains(gui);
        }

        /// <summary>The shallow world's rooms on the map: their bounds in global cells.</summary>
        static Rect D4WorldBounds()
        {
            var rooms = (System.Collections.IDictionary)AccessTools.Field(d4Map, "rooms").GetValue(null);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            int n = 0;
            foreach (System.Collections.DictionaryEntry e in rooms)
            {
                var room = e.Value;
                var t = room.GetType();
                string name = (string)t.GetField("Name").GetValue(room);
                if (!(bool)t.GetField("Captured").GetValue(room) || UI.WorldMap.DevDeep(name)) continue;
                var at = (Vector2Int)t.GetField("At").GetValue(room);
                int w = (int)t.GetField("W").GetValue(room), h = (int)t.GetField("H").GetValue(room);
                x0 = Mathf.Min(x0, at.x); y0 = Mathf.Min(y0, at.y);
                x1 = Mathf.Max(x1, at.x + w); y1 = Mathf.Max(y1, at.y + h);
                n++;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 地图：浅层 {n} 个房间，范围 ({x0},{y0})-({x1},{y1})");
            return n == 0 ? new Rect(0, 0, 1000, 1000) : Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>The map's view eased to a centre and a zoom (as the wheel and a drag would take it there).</summary>
        IEnumerator D4MapMove(Vector2 toPan, float toZoom, float seconds)
        {
            Vector2 p0 = D4MapPan;
            float z0 = D4MapZoom;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / seconds);
                float z = Mathf.Exp(Mathf.Lerp(Mathf.Log(z0), Mathf.Log(toZoom), u));
                // The centre moves at a pace the zoom keeps steady on screen.
                float k = Mathf.Abs(z0 - toZoom) < 1e-3f ? u : (1f / z0 - 1f / z) / (1f / z0 - 1f / toZoom);
                if (float.IsNaN(k)) k = u;
                D4MapZoom = z;
                D4MapPan = Vector2.Lerp(p0, toPan, Mathf.Clamp01(k));
                yield return null;
            }
            D4MapZoom = toZoom;
            D4MapPan = toPan;
        }

        /// <summary>The map grabbed at a point and dragged to another (the pointer held down on it).</summary>
        IEnumerator D4MapDrag(Vector2 from, Vector2 to, float seconds)
        {
            yield return Glide(from, 0.45f);
            if (!D4MapFree(from)) Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：拖动起点 {from} 下有东西");
            UI.TerraCursor.DevHeld = true;
            UI.TerraCursor.DevClick();
            yield return null;
            yield return null;
            yield return Glide(to, seconds);
            yield return new WaitForSeconds(0.08f);
            UI.TerraCursor.DevHeld = false;
            yield return null;
        }

        /// <summary>The nearest point to one the map's press would take as a grab (no arrow, pylon or tab under it).</summary>
        static Vector2 D4FreeNear(Vector2 p)
        {
            for (int r = 0; r < 12; r++)
                for (int k = 0; k < 8; k++)
                {
                    var q = p + new Vector2(Mathf.Cos(k * Mathf.PI / 4f), Mathf.Sin(k * Mathf.PI / 4f)) * (r * 24f);
                    if (D4MapFree(q)) return q;
                }
            return p;
        }

        /// <summary>An area's rooms on the map: their bounds in global cells.</summary>
        static Rect D4AreaBounds(string area)
        {
            var rooms = (System.Collections.IDictionary)AccessTools.Field(d4Map, "rooms").GetValue(null);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            int n = 0;
            foreach (System.Collections.DictionaryEntry e in rooms)
            {
                var room = e.Value;
                var t = room.GetType();
                if ((string)t.GetField("Area").GetValue(room) != area || !(bool)t.GetField("Captured").GetValue(room)) continue;
                var at = (Vector2Int)t.GetField("At").GetValue(room);
                int w = (int)t.GetField("W").GetValue(room), h = (int)t.GetField("H").GetValue(room);
                x0 = Mathf.Min(x0, at.x); y0 = Mathf.Min(y0, at.y);
                x1 = Mathf.Max(x1, at.x + w); y1 = Mathf.Max(y1, at.y + h);
                n++;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 地图：{area} {n} 个房间，范围 ({x0},{y0})-({x1},{y1})");
            return n == 0 ? default : Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>A marked gate whose other end is on the same layer, a fair way off: (scene, name, its cell, the other end's).</summary>
        static (string Scene, string Gate, Vector2 A, Vector2 B)? D4PickGate()
        {
            var gates = (System.Collections.IDictionary)AccessTools.Property(d4Map, "Gates").GetValue(null);
            var layout = (Dictionary<string, Vector2Int>)AccessTools.Field(d4Map, "layout").GetValue(null);
            var cellOf = AccessTools.Method(d4Map, "GateCell");
            var otherEnd = AccessTools.Method(d4Map, "OtherEnd");
            (string, string, Vector2, Vector2)? best = null;
            float bestScore = float.MinValue;
            var sb = new System.Text.StringBuilder();
            foreach (var (scene, gate, to, same) in UI.WorldMap.DevMarkedGates())
            {
                if (!same || UI.WorldMap.DevGateDeep(scene, gate) || !layout.TryGetValue(scene, out var at)) continue;
                object g = null;
                foreach (var o in (System.Collections.IList)gates[scene])
                    if ((string)o.GetType().GetField("Name").GetValue(o) == gate) g = o;
                if (g == null) continue;
                var a = (Vector2)cellOf.Invoke(null, new object[] { g, at });
                var args = new object[] { g, null, null };
                if (!(bool)otherEnd.Invoke(null, args) || (bool)args[2]) continue;
                var b = (Vector2)args[1];
                float d = Vector2.Distance(a, b);
                // A clear line across a good part of the screen; a well-known area first.
                float score = -Mathf.Abs(d - 300f);
                if (Areas.AreaName(scene) != Areas.AreaName(to)) score += 150f;
                if (scene.StartsWith("Crossroads") || scene.StartsWith("Fungus1") || scene.StartsWith("Ruins") || scene.StartsWith("Fungus2") || scene.StartsWith("Town") || scene.StartsWith("Cliffs")) score += 40f;
                if (Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x) * 1.5f) score -= 60f;
                if (d < 80f || d > 520f) continue;
                sb.Append($" {scene}:{gate}→{to}({d:F0})");
                if (score > bestScore) { bestScore = score; best = (scene, gate, a, b); }
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 地图：可选的门{sb}");
            return best;
        }

        IEnumerator P4_Dmap(HeroController hero)
        {
            yield return P4Kit();
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            // The Grimmchild out (its charm got, its first flames to find).
            pd.SetBool("gotCharm_40", true);
            pd.SetInt("grimmChildLevel", 1);
            pd.SetInt("flamesCollected", 0);
            pd.SetInt("flamesRequired", 3);
            HkItems.Tick(mod);
            for (int i = 0; i < inv.HkBag.Length; i++)
                if (!inv.HkBag[i].IsEmpty && inv.HkBag[i].Type == ItemType.HkGrimmchild && inv.Misc[Pets.PetSlot].IsEmpty)
                {
                    inv.Misc[Pets.PetSlot] = inv.HkBag[i];
                    inv.HkBag[i] = default;
                }
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            D4NoAreaTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var street = TownNpcs.DevStreet;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：德特茅斯街道 {street}，居民 {TownNpcs.Count}：{TownNpcs.DevDescribe()}，格林之子 {(GameObject.FindWithTag("Grimmchild") != null ? "在" : "不在")}");
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(street.z + 4f, street.y - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            // The area's title passes; the map's rooms are made once ahead, off the record (they're made a few a frame).
            var world = D4WorldBounds();
            UI.WorldMap.DevSetOpen(true);
            yield return D4MapMove(world.center, Mathf.Clamp(Mathf.Min(Screen.width / (world.width + 60f), Screen.height / (world.height + 60f)), 0.3f, 4f), 0.5f);
            yield return new WaitForSeconds(3f);
            UI.WorldMap.DevSetOpen(false);
            yield return new WaitForSeconds(3.5f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：{UI.WorldMap.DevMini()}，格林之子 {(GameObject.FindWithTag("Grimmchild") != null ? "在" : "不在")}");

            // --- Dirtmouth's street with the minimap, then the overlay.
            int styleWas = UI.WorldMap.Style;
            UI.WorldMap.DevStyle(1);
            UI.WorldMap.DevMiniZoom(1.5f);
            aimInit = false;
            yield return D4Rec("map_minimap");
            float t0 = Time.time;
            bool overlay = false;
            Player.TerraMovement.DevMove = 1;
            while (Time.time - t0 < 8f && col.bounds.center.x < street.w - 6f)
            {
                PlayerData.instance.isInvincible = true;
                SmoothAim((Vector2)col.bounds.center + new Vector2(4f, 1.2f), 4f);
                if (!overlay && Time.time - t0 > 4f)
                {
                    overlay = true;
                    UI.WorldMap.DevStyle(2);
                    mod.PlaySound("Menu_Tick", 0.7f);
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.8f);
            StopRec();
            UI.WorldMap.DevStyle(1);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：走到 {col.bounds.center.x:F1}（街道到 {street.w:F1}），{Time.time - t0:F1}s");

            // --- The full map over the town: the townsfolk's heads, then the whole world.
            var me = GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.5f));
            PointerOn(me);
            UI.WorldMap.DevZoom(4.5f);
            yield return D4Rec("map_full");
            yield return new WaitForSeconds(0.4f);
            UI.WorldMap.DevSetOpen(true);
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：头像 {UI.WorldMap.DevNpcHeads()}");
            // Two heads near the middle, their names.
            var heads = new List<Vector2>();
            for (int i = 0; i < UI.WorldMap.DevNpcHeadCount; i++)
            {
                var h = UI.WorldMap.DevNpcHeadScreen(i);
                if (h != null && Mathf.Abs(h.Value.x - Screen.width / 2f) < 500f && Mathf.Abs(h.Value.y - Screen.height / 2f) < 300f) heads.Add(h.Value);
            }
            heads.Sort((a, b) => a.x.CompareTo(b.x));
            if (heads.Count > 0)
            {
                yield return Glide(heads[heads.Count / 3], 0.6f);
                yield return new WaitForSeconds(0.7f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：悬停头像 {UI.WorldMap.DevLabel}");
                if (heads.Count > 2)
                {
                    yield return Glide(heads[heads.Count * 2 / 3], 0.45f);
                    yield return new WaitForSeconds(0.6f);
                    Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：悬停头像 {UI.WorldMap.DevLabel}");
                }
            }
            yield return Glide(new Vector2(Screen.width * 0.56f, Screen.height * 0.45f), 0.5f);
            // Back to the whole of it.
            float worldZoom = Mathf.Clamp(Mathf.Min(Screen.width / (world.width + 60f), Screen.height / (world.height + 60f)), 0.3f, 4f);
            yield return D4MapMove(world.center, worldZoom, 2.6f);
            yield return new WaitForSeconds(1.4f);
            // Dragged about: across to the west, then back over the middle.
            var g1 = D4FreeNear(new Vector2(Screen.width * 0.42f, Screen.height * 0.5f));
            yield return D4MapDrag(g1, g1 + new Vector2(Screen.width * 0.24f, Screen.height * 0.06f), 1.2f);
            yield return new WaitForSeconds(0.3f);
            var g2 = D4FreeNear(new Vector2(Screen.width * 0.7f, Screen.height * 0.4f));
            yield return D4MapDrag(g2, g2 - new Vector2(Screen.width * 0.34f, -Screen.height * 0.06f), 1.4f);
            yield return new WaitForSeconds(0.6f);
            StopRec();

            // --- A gate's arrow: its line to the other end, "通往：…", a click across.
            var pick = D4PickGate();
            if (pick != null)
            {
                var (gs, gn, a, b) = pick.Value;
                float d = Vector2.Distance(a, b);
                float z = Mathf.Clamp(820f / Mathf.Max(1f, d), 1.2f, 6f);
                UI.WorldMap.DevCentreGate(gs, gn);
                D4MapZoom = z * 0.7f;
                D4MapPan = Vector2.Lerp(a, b, 0.5f) + new Vector2(0f, 20f / z);
                yield return null;
                yield return null;
                yield return D4Rec("map_gate");
                yield return D4MapMove(Vector2.Lerp(a, b, 0.5f), z, 1.0f);
                var at = UI.WorldMap.DevGateScreen(gs, gn);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：门 {gs} {gn}（{d:F0} 格，缩放 {z:F1}）在屏幕 {at}，提示 {UI.WorldMap.DevGateLabel(gs, gn)}");
                if (at != null)
                {
                    yield return Glide(at.Value, 0.7f);
                    yield return new WaitForSeconds(1.8f);
                    Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：悬停门 {UI.WorldMap.DevLabel}");
                    UI.TerraCursor.DevClick();
                    yield return null;
                    yield return null;
                    yield return new WaitForSeconds(0.5f);
                    yield return Glide(new Vector2(Screen.width * 0.58f, Screen.height * 0.6f), 0.6f);
                    yield return new WaitForSeconds(1.2f);
                }
                StopRec();
            }

            // --- The "神居" button: Godhome's rooms.
            yield return D4MapMove(world.center + new Vector2(world.width * 0.1f, 0f), worldZoom * 1.6f, 0.01f);
            yield return null;
            yield return null;
            var tab = (Rect)AccessTools.Field(d4Map, "godhomeTab").GetValue(null);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：神居按钮 {tab}");
            if (tab.width > 0f)
            {
                yield return D4Rec("map_godhome");
                yield return new WaitForSeconds(0.5f);
                yield return Glide(tab.center, 1.0f);
                yield return new WaitForSeconds(0.6f);
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.6f);
                // Out over Godhome's rooms, their names.
                var gh = D4AreaBounds("神居");
                yield return Glide(new Vector2(Screen.width * 0.45f, Screen.height * 0.5f), 0.8f);
                if (gh.width > 0f)
                    yield return D4MapMove(gh.center, Mathf.Clamp(Mathf.Min(Screen.width * 0.9f / gh.width, Screen.height * 0.85f / gh.height), 0.3f, 6f), 1.8f);
                yield return new WaitForSeconds(0.5f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：神居 {UI.WorldMap.DevLabel}，深层 {UI.WorldMap.DevViewDeep}");
                yield return Glide(new Vector2(Screen.width * 0.6f, Screen.height * 0.42f), 0.9f);
                yield return new WaitForSeconds(0.8f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：神居 {UI.WorldMap.DevLabel}");
                StopRec();
            }

            // --- The Grimm flames still to find, with the Grimmchild worn.
            UI.WorldMap.DevPickLayer(1);
            var flames = new List<(Vector2 Cell, string Scene)>();
            foreach (var f in UI.WorldMap.GrimmFlames)
                if (UI.WorldMap.FlameAvailable(f, pd) && D4ScenePoint(f.Scene, f.Local, out var c)) flames.Add((c, f.Scene));
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：格林火焰 {flames.Count} 个 {string.Join(",", flames.ConvertAll(x => x.Scene))}");
            if (flames.Count > 0)
            {
                var first = flames[0].Cell;
                D4MapZoom = 3.2f;
                D4MapPan = first + new Vector2(25f, -10f);
                yield return null;
                yield return null;
                yield return D4Rec("map_grimm");
                yield return D4MapMove(first + new Vector2(12f, -4f), 4f, 1.0f);
                var tip = new Vector2(7f, 7f) * UI.GameView.Scale;
                yield return Glide(D4MapScreen(first) + tip, 0.8f);
                yield return new WaitForSeconds(1.4f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：悬停火焰 {UI.WorldMap.DevLabel}");
                // On to the next one (the City's), the view drawn across at a lower zoom.
                if (flames.Count > 1)
                {
                    var next = flames[flames.Count - 1].Cell;
                    yield return Glide(new Vector2(Screen.width * 0.62f, Screen.height * 0.66f), 0.5f);
                    yield return D4MapMove(Vector2.Lerp(first, next, 0.5f), 1.6f, 1.2f);
                    yield return D4MapMove(next + new Vector2(10f, -4f), 3.2f, 1.2f);
                    yield return Glide(D4MapScreen(next) + tip, 0.7f);
                    yield return new WaitForSeconds(1.0f);
                    Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 地图：悬停火焰 {UI.WorldMap.DevLabel}");
                }
                StopRec();
            }
            UI.WorldMap.DevSetOpen(false);
            PointerOff();
            UI.WorldMap.DevStyle(styleWas);
            UI.WorldMap.DevMiniZoom(1.05f);
        }
    }
}
