using System.Collections;
using System.IO;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.0, recorder D): the menus. The title screen with the mod's 空洞泰拉 logo, "开始游戏" clicked,
    /// the save profiles with their mode and difficulty labels, the creative mode button, an empty slot chosen,
    /// Terraria's four difficulty cards hovered and Master picked, on into the portal of King's Pass. Every
    /// press is the drawn pointer gliding onto the button and the button's own submit (the game's real mouse
    /// is kept out of the menu meanwhile).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// A clip begun a couple of frames on: a recording loop still waiting out the frame its clip ended in would see
        /// recording on again and carry on beside the new clip's own, writing every frame twice (the clip at half speed).
        /// </summary>
        /// <summary>Where the clip being recorded is now (its seconds, 50 frames a second), for the catalog's times.</summary>
        string D4At => recording ? $"@{recFrame / 50f:F2}s" : "@-";

        IEnumerator D4Rec(string clip)
        {
            yield return null;
            yield return null;
            // Pickup texts left over from what went before (a kit given, a reforge) aren't the new clip's.
            UI.PopupText.Clear();
            StartRec(clip);
        }

        /// <summary>The scene's area title triggers off (the big "德特茅斯" title would come up over a clip).</summary>
        static void D4NoAreaTitle()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f != null && f.FsmName != null && f.FsmName.Contains("Area Title") && f.gameObject.scene.name == GameManager.instance.sceneName)
                {
                    names.Add(f.gameObject.name);
                    f.gameObject.SetActive(false);
                }
            Plugin.Log.LogInfo($"[AutoTest] 录制 关掉区域标题：{string.Join(",", names)}");
        }

        /// <summary>Where a uGUI element is drawn, as a GUI rect (top-left origin).</summary>
        static Rect? D4UiRect(Component c)
        {
            if (c == null) return null;
            var rt = c.transform as RectTransform;
            if (rt == null) return null;
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? D4CanvasCamera(canvas.rootCanvas) : null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var w in corners)
            {
                var s = RectTransformUtility.WorldToScreenPoint(cam, w);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
            return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>
        /// The camera a canvas is seen through: none for an overlay; else the one that maps the root canvas onto the
        /// whole screen (the menu's canvas names none of its own, and the main camera isn't it).
        /// </summary>
        static Camera D4CanvasCamera(Canvas root)
        {
            if (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            var rt = root.transform as RectTransform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Camera best = root.worldCamera;
            float bestErr = float.MaxValue;
            foreach (var cam in Camera.allCameras)
            {
                var a = cam.WorldToScreenPoint(corners[0]);
                var b = cam.WorldToScreenPoint(corners[2]);
                float err = Mathf.Abs(a.x) + Mathf.Abs(a.y) + Mathf.Abs(b.x - Screen.width) + Mathf.Abs(b.y - Screen.height);
                if (err < bestErr) { bestErr = err; best = cam; }
            }
            return best;
        }

        /// <summary>Dev: how a uGUI element's canvas is drawn, and every camera, for finding where it is on screen.</summary>
        static string D4CanvasInfo(Component c)
        {
            var sb = new System.Text.StringBuilder();
            var canvas = c != null ? c.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                var root = canvas.rootCanvas;
                var rt = root.transform as RectTransform;
                sb.Append($"画布 {canvas.name}（根 {root.name}）{root.renderMode} 相机 {(root.worldCamera != null ? root.worldCamera.name : "无")} 缩放 {root.scaleFactor:F2} 根大小 {rt.rect.size} 位置 {rt.position} 层 {root.gameObject.layer}；");
                var elt = c.transform as RectTransform;
                if (elt != null) sb.Append($"元素 {elt.position} 大小 {elt.rect.size} 世界缩放 {elt.lossyScale}；");
            }
            foreach (var cam in Camera.allCameras)
                sb.Append($" [{cam.name} 正交 {cam.orthographic} 尺寸 {cam.orthographicSize:F2} fov {cam.fieldOfView:F0} 位置 {cam.transform.position} 遮罩 {cam.cullingMask} 像素 {cam.pixelRect} 深度 {cam.depth}]");
            return sb.ToString();
        }

        /// <summary>The game's menu takes no real mouse (the pointer is ours) while a clip runs.</summary>
        static void D4RealMouse(bool on)
        {
            var es = EventSystem.current;
            if (es != null && es.currentInputModule is InControl.HollowKnightInputModule hk) hk.allowMouseInput = on;
        }

        /// <summary>The pointer glides onto a menu button, the button lights up (selected, its sound), a beat.</summary>
        IEnumerator D4Hover(Component button, float glide, float hold, Vector2? offset = null)
        {
            var r = D4UiRect(button);
            if (r == null) yield break;
            var to = r.Value.center + (offset ?? Vector2.zero);
            Plugin.Log.LogInfo($"[AutoTest] 录制 悬停 {button.name}：{r.Value}");
            yield return Glide(to, glide);
            var es = EventSystem.current;
            if (es != null && button != null) es.SetSelectedGameObject(button.gameObject);
            yield return new WaitForSeconds(hold);
        }

        /// <summary>The button pressed as the game's input would (its submit: flash, sound, action).</summary>
        static void D4Submit(Component button)
        {
            var es = EventSystem.current;
            if (button == null || es == null) return;
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
        }

        static readonly System.Reflection.MethodInfo d4SaveSlot =
            HarmonyLib.AccessTools.Method(typeof(GameManager), "SaveGame", new[] { typeof(int), typeof(System.Action<bool>) });

        /// <summary>The game saved into another slot with its profile's look changed (area, geo, masks, time), and a mod save of that mode beside it.</summary>
        IEnumerator D4FakeSlot(int slot, GlobalEnums.MapZone zone, int geo, int masks, float hours, bool creative, DifficultyLevel difficulty, string modText)
        {
            var pd = PlayerData.instance;
            var gm = GameManager.instance;
            var keep = (pd.mapZone, pd.geo, pd.maxHealth, pd.maxHealthBase, pd.playTime, pd.profileID);
            pd.mapZone = zone;
            pd.geo = geo;
            pd.maxHealth = masks;
            pd.maxHealthBase = masks;
            pd.playTime = hours * 3600f;
            bool done2 = false;
            d4SaveSlot.Invoke(gm, new object[] { slot, (System.Action<bool>)(ok => done2 = true) });
            float until = Time.realtimeSinceStartup + 10f;
            while (!done2 && Time.realtimeSinceStartup < until) yield return null;
            (pd.mapZone, pd.geo, pd.maxHealth, pd.maxHealthBase, pd.playTime, pd.profileID) = keep;
            if (modText != null)
            {
                string t = System.Text.RegularExpressions.Regex.Replace(modText, "\"Creative\":(true|false)", "\"Creative\":" + (creative ? "true" : "false"));
                t = System.Text.RegularExpressions.Regex.Replace(t, "\"Difficulty\":\\d+", "\"Difficulty\":" + (int)difficulty);
                File.WriteAllText(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json"), t);
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 菜单：存档 {slot} 做成 {zone} {GameDifficulty.NameOf(difficulty)}{(creative ? " 创造" : "")}，保存 {done2}");
        }

        /// <summary>
        /// The opening: from the test game back to the title (a few save slots filled first so the profiles
        /// show their labels), then the menus clicked through into a new creative game.
        /// </summary>
        IEnumerator P4_Dmenu(HeroController hero)
        {
            yield return P4Kit();
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            int mySlot = gm.profileID;
            // The test game's own slot: creative Master, in Dirtmouth.
            Save.ModSave.Write(mySlot, mod);
            string modText = File.ReadAllText(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{mySlot}.json"));
            yield return D4FakeSlot(2, GlobalEnums.MapZone.GREEN_PATH, 3480, 7, 21.4f, false, DifficultyLevel.Expert, modText);
            yield return D4FakeSlot(3, GlobalEnums.MapZone.CITY, 12650, 9, 47.8f, false, DifficultyLevel.Master, modText);
            pd.mapZone = GlobalEnums.MapZone.TOWN;
            pd.geo = 9999;
            pd.maxHealth = pd.maxHealthBase = 9;
            pd.playTime = 63.2f * 3600f;
            InventoryGui.Open = false;
            gm.StartCoroutine(gm.ReturnToMainMenu(GameManager.ReturnToMainMenuSaveModes.SaveAndContinueOnFail));
            float wait = Time.realtimeSinceStartup + 60f;
            while (((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Menu_Title" || gm.gameState != GlobalEnums.GameState.MAIN_MENU || UIManager.instance == null) && Time.realtimeSinceStartup < wait) yield return null;
            var ui = UIManager.instance;
            wait = Time.realtimeSinceStartup + 20f;
            while (ui.menuState != GlobalEnums.MainMenuState.MAIN_MENU && Time.realtimeSinceStartup < wait) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 菜单：回到标题 {gm.sceneName} {ui.menuState}，语言 {TeamCherry.Localization.Language.CurrentLanguage()}");
            D4RealMouse(false);
            var buttons = ui.mainMenuButtons;
            Plugin.Log.LogInfo($"[AutoTest] 录制 菜单：开始游戏按钮 {D4UiRect(buttons.startButton)}，标题 {D4UiRect(ui.mainMenuScreen)}；{D4CanvasInfo(buttons.startButton)}");
            yield return new WaitForSeconds(1.5f);

            PointerOn(new Vector2(Screen.width * 0.74f, Screen.height * 0.86f));
            StartRec("menu_title");
            // The title a while (the logo and the menu under it), the pointer drifting in.
            yield return new WaitForSeconds(1.2f);
            yield return Glide(new Vector2(Screen.width * 0.62f, Screen.height * 0.74f), 1.1f);
            yield return new WaitForSeconds(0.3f);
            yield return D4Hover(buttons.startButton, 0.8f, 0.55f);
            // The profiles open on the first page (the slot played last decides it; the run puts the player's own back after).
            global::Platform.Current.LocalSharedData.SetInt("lastProfileIndex", 1);
            UI.SaveSlots.DevSetPage(0);
            D4Submit(buttons.startButton);
            // The profiles come in, slot by slot.
            wait = Time.time + 6f;
            while (ui.menuState != GlobalEnums.MainMenuState.SAVE_PROFILES && Time.time < wait) yield return null;
            yield return new WaitForSeconds(0.4f);
            var slots = new UnityEngine.UI.SaveSlotButton[] { ui.slotOne, ui.slotTwo, ui.slotThree, ui.slotFour };
            var sb = new System.Text.StringBuilder();
            foreach (var b in slots) sb.Append($" {UI.SaveSlots.IndexOf(b)}:{b.saveFileState} “{(b.locationText != null ? b.locationText.text : "")}” {D4UiRect(b)}");
            Plugin.Log.LogInfo($"[AutoTest] 录制 菜单：存档界面{sb}");
            for (int i = 0; i < 3; i++)
                yield return D4Hover(slots[i], i == 0 ? 0.7f : 0.45f, 0.75f, new Vector2(-180f * Screen.height / 1080f, 0f));
            // The creative mode button in the corner, its panel.
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
            yield return Glide(UI.CreativeMenu.DevRect("creative").center, 0.8f);
            yield return new WaitForSeconds(0.55f);
            UI.CreativeMenu.DevOpen();
            mod.PlaySound("Menu_Tick", 0.7f);
            yield return new WaitForSeconds(1.3f);
            int pick = 4;
            yield return Glide(UI.CreativeMenu.DevRect("slot", pick).center, 0.7f);
            yield return new WaitForSeconds(0.45f);
            UI.CreativeMenu.DevChoose(pick);
            mod.PlaySound("Menu_Tick", 0.7f);
            yield return new WaitForSeconds(0.6f);
            if (UI.CreativeMenu.DevStep == "Confirm")
            {
                yield return Glide(UI.CreativeMenu.DevRect("confirm").center, 0.5f);
                yield return new WaitForSeconds(0.3f);
                UI.CreativeMenu.DevConfirm();
                mod.PlaySound("Menu_Tick", 0.7f);
                yield return new WaitForSeconds(0.5f);
            }
            // Terraria's four difficulty cards, one after another, then Master.
            for (int i = 1; i <= 4; i++)
            {
                var r = UI.CreativeMenu.DevRect("difficulty", i);
                yield return Glide(new Vector2(r.x + r.width * 0.36f, r.center.y), i == 1 ? 0.6f : 0.42f);
                yield return new WaitForSeconds(i == 4 ? 0.9f : 0.6f);
            }
            UI.CreativeMenu.DevPick(DifficultyLevel.Master);
            mod.PlaySound("Menu_Tick", 0.7f);
            yield return new WaitForSeconds(0.35f);
            PointerOff();
            D4RealMouse(true);
            // On through the load into King's Pass: the hero out of the portal.
            float until = Time.realtimeSinceStartup + 90f;
            GameManager g;
            while (Time.realtimeSinceStartup < until && ((g = GameManager.UnsafeInstance) == null || g.sceneName != "Tutorial_01" || !GameHooks.PlayerHasControl()))
            {
                // The creative game's Hallownest items arrive with a column of pickup texts over the portal: kept out of the shot.
                UI.PopupText.Clear();
                yield return null;
            }
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                UI.PopupText.Clear();
                yield return null;
            }
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 菜单：进了 {GameManager.instance.sceneName}，创造 {mod.Creative}，难度 {GameDifficulty.NameOf(GameDifficulty.Current)}");
        }
    }
}
