using System.Collections;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TerraKnight.Player;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E, suite "p4_v5E_menu_zh"): the Terraria start menu. Back at the title from the test game
    /// (four more saves made first, each with its own character, mode, difficulty and enemy AI, so the list has rows to
    /// show), 开始游戏 clicked: the world list, its rows hovered, one favourited (it goes to the top); 新建: the character
    /// creation, the name typed, the play style, a clothing style, a hair style, the colours dragged on their sliders; 创建:
    /// the world creation, a random name rolled, the five difficulties hovered and Master picked, the enemy AI's two hovered
    /// and 新版 chosen, the guidance followed; 创建 starts the game: King's Pass, the portal, the Voice's opening words.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly System.Reflection.MethodInfo e5Typed = AccessTools.Method(typeof(CharacterCreation), "Typed");
        static readonly System.Reflection.FieldInfo e5HairView = AccessTools.Field(typeof(CharCreationEditor), "hairView");

        /// <summary>A saved look: a clothing style, a hair, the seven colours.</summary>
        static Appearance E5Look(int variant, int hair, int hairColor, int skin, int eye, int shirt, int under, int pants, int shoes) => new Appearance
        {
            Variant = variant, Hair = hair, HairColor = hairColor, SkinColor = skin, EyeColor = eye, ShirtColor = shirt,
            UnderShirtColor = under, PantsColor = pants, ShoeColor = shoes,
        };

        /// <summary>
        /// Another slot made a save of its own: the game's save written with its profile's look changed (area, geo, masks,
        /// time, completion, Steel Soul), and a mod save of its world, character, mode, difficulty and enemy AI beside it.
        /// </summary>
        IEnumerator E5FakeSlot(int slot, GlobalEnums.MapZone zone, int geo, int masks, float hours, float percent, bool steel,
            JObject template, bool creative, DifficultyLevel difficulty, EnemyAiKind ai, string world, string player, Appearance look)
        {
            var pd = PlayerData.instance;
            var gm = GameManager.instance;
            var keep = (pd.mapZone, pd.geo, pd.maxHealth, pd.maxHealthBase, pd.playTime, pd.profileID, pd.completionPercentage, pd.unlockedCompletionRate, pd.permadeathMode);
            pd.mapZone = zone;
            pd.geo = geo;
            pd.maxHealth = masks;
            pd.maxHealthBase = masks;
            pd.playTime = hours * 3600f;
            pd.completionPercentage = percent;
            pd.unlockedCompletionRate = false;
            pd.permadeathMode = steel ? 1 : 0;
            bool done = false;
            d4SaveSlot.Invoke(gm, new object[] { slot, (System.Action<bool>)(ok => done = true) });
            float until = Time.realtimeSinceStartup + 10f;
            while (!done && Time.realtimeSinceStartup < until) yield return null;
            (pd.mapZone, pd.geo, pd.maxHealth, pd.maxHealthBase, pd.playTime, pd.profileID, pd.completionPercentage, pd.unlockedCompletionRate, pd.permadeathMode) = keep;
            var j = (JObject)template.DeepClone();
            j["Creative"] = creative;
            j["Difficulty"] = (int)difficulty;
            j["EnemyAi"] = (int)ai;
            j["WorldName"] = world;
            j["PlayerName"] = player;
            j["Look"] = JObject.FromObject(look);
            File.WriteAllText(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json"), j.ToString(Newtonsoft.Json.Formatting.None));
            E5Log($"菜单：存档 {slot}「{world}」{player}，{zone}，{GameDifficulty.NameOf(difficulty)}，{EnemyAiMode.NameOf(ai)}{(creative ? "，创造" : "")}{(steel ? "，钢铁之魂" : "")}，保存 {done}");
        }

        /// <summary>The character creation's and world list's pictures read now (each drawn the first time held a frame up).</summary>
        static void E5WarmLooks()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (int v in Appearance.ClothStyles)
                for (int p = 0; p < 16; p++) { try { Terraria.TerrariaAssets.Texture(Appearance.Sheet(v, p)); } catch (System.Exception) { } }
            foreach (int h in Appearance.CreationHairs) { try { Terraria.TerrariaAssets.Texture(Appearance.HairSheet(h)); } catch (System.Exception) { } }
            Plugin.Log.LogInfo($"[AutoTest] 录制E 人物贴图预读 {sw.ElapsedMilliseconds} ms");
        }

        /// <summary>A rect of the start menu's screens as they lay it out (Terraria's 1080-high layout, scaled to the screen).</summary>
        static Rect E5M(float x, float y, float w, float h)
        {
            float s = TerraUi.S;
            return new Rect(x * s, y * s, w * s, h * s);
        }

        /// <summary>The character creation's panel (UICharacterCreation: 500 x 230, 270 down, in the middle).</summary>
        static Rect E5CharPanel => new Rect(Mathf.Round((Screen.width - 500f * TerraUi.S) / 2f), 270f * TerraUi.S, 500f * TerraUi.S, 230f * TerraUi.S);

        static Rect E5InPanel(float x, float y, float w, float h)
        {
            var p = E5CharPanel;
            float s = TerraUi.S;
            return new Rect(p.x + x * s, p.y + y * s, w * s, h * s);
        }

        /// <summary>A colour slider dragged: pressed at a point, moved along while held, let go.</summary>
        IEnumerator E5DragSlider(CharCreationEditor ed, int slider, float from, float to, float seconds)
        {
            var area = ed.DevSlider(slider);
            if (area.width < 20f) { E5Log($"滑条 {slider} 还没画出来"); yield break; }
            Vector2 At(float k) => new Vector2(area.x + 6f + (area.width - 12f) * k, area.center.y);
            yield return E5Glide(At(from), 0.45f);
            yield return E5Wait(0.12f);
            TerraCursor.DevHeld = true;
            TerraCursor.DevClick();
            yield return null;
            yield return null;
            int n = E5Frames(seconds);
            for (int i = 1; i <= n; i++)
            {
                float u = Mathf.SmoothStep(0f, 1f, i / (float)n);
                TerraCursor.DevAt = At(Mathf.Lerp(from, to, u));
                yield return null;
            }
            yield return E5Wait(0.1f);
            TerraCursor.DevHeld = false;
            yield return null;
            yield return null;
            E5Log($"滑条 {slider} 拖到 {ed.DevHsl}");
        }

        IEnumerator P4_v5E_menu(HeroController hero)
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            E5NoInfinite();
            int mySlot = gm.profileID;
            // The test game's own row: an ordinary Master world of the new AI, in Dirtmouth.
            Save.ModSave.WorldName = "苍白之王的回声";
            Save.ModSave.PlayerName = "无名旅者";
            Appearance.Current = E5Look(0, 15, 0x3C2A1E, 0xE8B48C, 0x4B6EAF, 0x5A2E8C, 0xD2D2DC, 0x2A2A3C, 0x3C2814);
            EnemyAiMode.Current = EnemyAiKind.New;
            GameDifficulty.Current = DifficultyLevel.Master;
            Save.ModSave.Write(mySlot, mod);
            var template = JObject.Parse(File.ReadAllText(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{mySlot}.json")));
            yield return E5FakeSlot(2, GlobalEnums.MapZone.GREEN_PATH, 3480, 7, 12.4f, 0f, false, template, true, DifficultyLevel.Normal, EnemyAiKind.New,
                "翠绿苔原之境", "苔衣", E5Look(5, 26, 0x6B8E23, 0xF0C8A0, 0x2E8B57, 0x2E8B57, 0xF5F5DC, 0x556B2F, 0x8B4513));
            yield return E5FakeSlot(3, GlobalEnums.MapZone.CITY, 12650, 9, 31.7f, 52f, false, template, false, DifficultyLevel.Expert, EnemyAiKind.Vanilla,
                "雨落不息的王城", "雨城守夜人", E5Look(2, 41, 0xE6E6FA, 0xC89678, 0x1E90FF, 0x1C3A6E, 0xB0C4DE, 0x1A1A2E, 0x2F2F4F));
            yield return E5FakeSlot(4, GlobalEnums.MapZone.DEEPNEST, 7820, 8, 18.2f, 37f, true, template, false, DifficultyLevel.Master, EnemyAiKind.New,
                "深巢的低语", "钢魂", E5Look(8, 63, 0xB22222, 0xD2A27C, 0xDC143C, 0x3A0A0A, 0x8B0000, 0x1C1C1C, 0x2B1B17));
            yield return E5FakeSlot(5, GlobalEnums.MapZone.ROYAL_GARDENS, 25890, 9, 63.5f, 98f, false, template, false, DifficultyLevel.Hollow, EnemyAiKind.New,
                "螳螂王后的花园", "花园守望者", E5Look(6, 88, 0xFFD700, 0xFFDAB9, 0x9370DB, 0xFFB6C1, 0xFFFFFF, 0xDA70D6, 0xF5F5F5));
            pd.mapZone = GlobalEnums.MapZone.TOWN;
            pd.geo = 4210;
            pd.playTime = 41.6f * 3600f;
            pd.unlockedCompletionRate = false;
            Items.InventoryGui.Open = false;
            E5WarmLooks();
            gm.StartCoroutine(gm.ReturnToMainMenu(GameManager.ReturnToMainMenuSaveModes.SaveAndContinueOnFail));
            float wait = Time.realtimeSinceStartup + 60f;
            while (((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Menu_Title" || gm.gameState != GlobalEnums.GameState.MAIN_MENU || UIManager.instance == null) && Time.realtimeSinceStartup < wait) yield return null;
            var ui = UIManager.instance;
            wait = Time.realtimeSinceStartup + 20f;
            while (ui.menuState != GlobalEnums.MainMenuState.MAIN_MENU && Time.realtimeSinceStartup < wait) yield return null;
            E5Log($"菜单：回到标题 {gm.sceneName} {ui.menuState}");
            D4RealMouse(false);
            yield return new WaitForSeconds(1.5f);

            // ---- The title, 开始游戏, the world list.
            var start = ui.mainMenuButtons.startButton;
            var startRect = D4UiRect(start);
            E5PointerOn(new Vector2(Screen.width * 0.72f, Screen.height * 0.84f));
            yield return E5Rec("menu_list");
            yield return E5Wait(1.0f);
            if (startRect != null)
            {
                yield return E5Glide(startRect.Value.center + new Vector2(0f, 2f), 1.0f);
                var es = EventSystem.current;
                if (es != null) es.SetSelectedGameObject(start.gameObject);
                yield return E5Wait(0.45f);
            }
            D4Submit(start);
            wait = Time.realtimeSinceStartup + 10f;
            while (TerraMenu.DevStep != "List" && Time.realtimeSinceStartup < wait) yield return null;
            E5Log($"菜单：世界列表 {TerraMenu.DevStep}");
            yield return E5Wait(0.6f);
            // The rows, top to bottom (the latest played first), then one favourited and the play button's label.
            var rowsAt = new System.Collections.Generic.List<(int Slot, Rect R)>();
            for (int s = 1; s <= 8; s++)
            {
                var r = TerraMenu.DevRect("slot", s);
                if (r.width > 10f) rowsAt.Add((s, r));
            }
            rowsAt.Sort((a, b) => a.R.y.CompareTo(b.R.y));
            E5Log($"菜单：{rowsAt.Count} 行 {string.Join(" ", rowsAt.ConvertAll(x => x.Slot + "@" + x.R.y.ToString("F0")))}");
            for (int i = 0; i < rowsAt.Count && i < 4; i++)
            {
                var r = rowsAt[i].R;
                yield return E5Glide(new Vector2(r.x + r.width * 0.62f, r.y + r.height * 0.42f), i == 0 ? 0.7f : 0.45f);
                yield return E5Wait(0.6f);
            }
            // The fourth row favourited: it goes to the top of the list.
            if (rowsAt.Count >= 4)
            {
                var r = rowsAt[3].R;
                var fav = new Vector2(r.x + 6f * TerraUi.S + 4f * TerraUi.S + 24f * TerraUi.S + 11f * TerraUi.S, r.yMax - 6f * TerraUi.S - 11f * TerraUi.S);
                yield return E5GlidePress(fav, 0.55f, 0.5f);
                yield return E5Wait(0.9f);
                var top = TerraMenu.DevRect("slot", rowsAt[3].Slot);
                E5Log($"菜单：收藏了存档 {rowsAt[3].Slot}，它的行现在在 y={top.y:F0}");
                // Its play button: "开始游戏" beside it.
                var play = new Vector2(top.x + 6f * TerraUi.S + 4f * TerraUi.S + 11f * TerraUi.S, top.yMax - 6f * TerraUi.S - 11f * TerraUi.S);
                yield return E5Glide(play, 0.6f);
                yield return E5Wait(0.8f);
            }
            var create = TerraMenu.DevRect("creative");
            yield return E5GlidePress(create.center, 0.7f, 0.35f);
            wait = Time.realtimeSinceStartup + 5f;
            while (TerraMenu.DevStep != "Character" && Time.realtimeSinceStartup < wait) yield return null;
            yield return E5Wait(0.3f);
            StopRec();

            // ---- The character creation.
            var ed = CharacterCreation.DevEditor;
            if (ed == null) { E5Log("菜单：没打开人物创建"); D4RealMouse(true); E5PointerOff(); yield break; }
            yield return E5Rec("menu_char");
            yield return E5Wait(0.5f);
            // The name: the plate clicked, typed a character at a time, kept.
            var nameRect = E5InPanel(10f, 59f, 480f, 40f);
            yield return E5GlidePress(nameRect.center, 0.6f, 0.2f);
            yield return E5Wait(0.35f);
            const string playerName = "泰拉旅人";
            foreach (char c in playerName)
            {
                e5Typed.Invoke(null, new object[] { c });
                mod.PlaySound("Menu_Tick", 0.4f);
                yield return E5Wait(0.28f);
            }
            yield return E5Wait(0.3f);
            // A press beside it (the description) keeps the name.
            int styles = 2 + (TerraMenu.SteelSoulUnlocked ? 1 : 0) + (TerraMenu.GodseekerUnlocked ? 1 : 0);
            var style0 = E5InPanel(10f, 109f, 480f * 0.4f - 4f, 107f / Mathf.Max(3, styles));
            var desc = E5InPanel(10f + 480f * 0.4f + 4f + 40f, 140f, 200f, 40f);
            yield return E5GlidePress(desc.center, 0.45f, 0.1f);
            E5Log($"菜单：名字「{CharacterCreation.Name}」");
            // The play styles: 创造模式 (its description), then back to 经典.
            yield return E5GlidePress(style0.center, 0.5f, 0.2f);
            yield return E5Wait(1.1f);
            var classic = new Rect(style0.x, style0.y + style0.height, style0.width, style0.height);
            yield return E5GlidePress(classic.center, 0.4f, 0.15f);
            yield return E5Wait(1.0f);
            E5Log($"菜单：模式 {CharacterCreation.Style}");
            // The clothing styles: a few hovered (they walk), one picked.
            yield return E5GlidePress(ed.DevCategory(CharCreationEditor.Category.Clothing).center, 0.6f, 0.2f);
            yield return E5Wait(0.5f);
            Rect Cloth(int i) => E5InPanel(i * 46f + (i < 5 ? 11f : 29f), 59f, 44f, 80f);
            foreach (int i in new[] { 1, 2, 6 })
            {
                yield return E5Glide(Cloth(i).center, 0.35f);
                yield return E5Wait(0.45f);
            }
            yield return E5GlidePress(Cloth(7).center, 0.35f, 0.25f);
            yield return E5Wait(0.6f);
            // The hair styles: hovered across the grid, scrolled down a little, one picked.
            yield return E5GlidePress(ed.DevCategory(CharCreationEditor.Category.HairStyle).center, 0.6f, 0.2f);
            yield return E5Wait(0.45f);
            var list = E5InPanel(9f, 66f, 464f, 146f);
            Vector2 Hair(int i, float view) => new Vector2(list.x + ((i % 10) * 46f + 6f + 22f) * TerraUi.S, list.y + ((i / 10) * 48f + 1f + 22f) * TerraUi.S - view);
            foreach (int i in new[] { 2, 5, 13 })
            {
                yield return E5Glide(Hair(i, 0f), 0.35f);
                yield return E5Wait(0.35f);
            }
            float view = 0f, goal = 2f * 48f * TerraUi.S;
            for (int k = 1; k <= E5Frames(0.8f); k++)
            {
                view = goal * Mathf.SmoothStep(0f, 1f, k / (float)E5Frames(0.8f));
                e5HairView?.SetValue(ed, view);
                yield return null;
            }
            yield return E5Wait(0.3f);
            yield return E5Glide(Hair(37, view), 0.45f);
            yield return E5Wait(0.3f);
            yield return E5GlidePress(Hair(34, view), 0.3f, 0.2f);
            yield return E5Wait(0.6f);
            E5Log($"菜单：发型 {CharacterCreation.Look.Hair}，服装 {CharacterCreation.Look.Variant}");
            // The colours: the hair's hue and lightness dragged, the shirt's and the pants' hue.
            yield return E5GlidePress(ed.DevCategory(CharCreationEditor.Category.HairColor).center, 0.6f, 0.2f);
            yield return E5Wait(0.35f);
            yield return E5DragSlider(ed, 0, ed.DevHsl.x, 0.58f, 1.1f);
            yield return E5DragSlider(ed, 2, ed.DevHsl.z, 0.66f, 0.7f);
            yield return E5GlidePress(ed.DevCategory(CharCreationEditor.Category.Shirt).center, 0.6f, 0.2f);
            yield return E5Wait(0.35f);
            yield return E5DragSlider(ed, 0, ed.DevHsl.x, 0.75f, 1.0f);
            yield return E5DragSlider(ed, 1, ed.DevHsl.y, 0.9f, 0.6f);
            yield return E5GlidePress(ed.DevCategory(CharCreationEditor.Category.Pants).center, 0.6f, 0.2f);
            yield return E5Wait(0.35f);
            yield return E5DragSlider(ed, 0, ed.DevHsl.x, 0.62f, 0.9f);
            yield return E5DragSlider(ed, 2, ed.DevHsl.z, 0.38f, 0.6f);
            yield return E5Wait(0.5f);
            // Back to the info page, the whole character over the panel; then 创建.
            yield return E5GlidePress(ed.DevCategory(CharCreationEditor.Category.CharInfo).center, 0.6f, 0.2f);
            yield return E5Wait(0.8f);
            var cc = new Rect(E5CharPanel.x + 500f * TerraUi.S - 240f * TerraUi.S, 509f * TerraUi.S, 240f * TerraUi.S, 50f * TerraUi.S);
            yield return E5GlidePress(cc.center, 0.6f, 0.3f);
            wait = Time.realtimeSinceStartup + 5f;
            while (TerraMenu.DevStep != "Difficulty" && Time.realtimeSinceStartup < wait) yield return null;
            yield return E5Wait(0.3f);
            StopRec();
            E5Log($"菜单：人物 {CharacterCreation.Name}，{CharacterCreation.Style}，外观 发 {CharacterCreation.Look.HairColor:X6} 衣 {CharacterCreation.Look.ShirtColor:X6} 裤 {CharacterCreation.Look.PantsColor:X6}");

            // ---- The world creation.
            yield return E5Rec("menu_world");
            yield return E5Wait(0.6f);
            var dice = E5M(720f, 210f, 40f, 40f);
            yield return E5GlidePress(dice.center, 0.6f, 0.3f);
            yield return E5Wait(0.6f);
            yield return E5Press(dice.center);
            yield return E5Wait(0.8f);
            var levels = new[] { DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Expert, DifficultyLevel.Master, DifficultyLevel.Hollow };
            foreach (var d in levels)
            {
                var r = TerraMenu.DevRect("difficulty", (int)d);
                if (r.width < 10f) r = E5M(720f + 97f * ((int)d - 1), 304f, 92f, 34f);
                yield return E5Glide(r.center, d == DifficultyLevel.Normal ? 0.6f : 0.4f);
                yield return E5Wait(d == DifficultyLevel.Hollow ? 1.3f : 0.9f);
            }
            var master = TerraMenu.DevRect("difficulty", (int)DifficultyLevel.Master);
            if (master.width < 10f) master = E5M(720f + 97f * 3f, 304f, 92f, 34f);
            yield return E5GlidePress(master.center, 0.4f, 0.2f);
            yield return E5Wait(0.8f);
            // The enemy AI: Hollow Knight's own hovered, then the new one chosen.
            var aiOld = E5M(720f, 352f, 236f, 34f);
            var aiNew = E5M(964f, 352f, 236f, 34f);
            yield return E5Glide(aiOld.center, 0.5f);
            yield return E5Wait(1.2f);
            yield return E5GlidePress(aiNew.center, 0.5f, 0.3f);
            yield return E5Wait(1.6f);
            // The guidance: followed (its description read).
            var follow = E5M(720f, 400f, 236f, 34f);
            yield return E5GlidePress(follow.center, 0.5f, 0.3f);
            yield return E5Wait(1.6f);
            var make = E5M(970f, 509f, 240f, 50f);
            yield return E5Glide(make.center, 0.6f);
            yield return E5Wait(0.45f);
            var worldName = (string)AccessTools.Field(typeof(TerraMenu), "worldName").GetValue(null);
            E5Log($"菜单：世界「{worldName}」，难度 {AccessTools.Field(typeof(TerraMenu), "difficulty").GetValue(null)}，AI {AccessTools.Field(typeof(TerraMenu), "ai").GetValue(null)}，指引 {AccessTools.Field(typeof(TerraMenu), "guide").GetValue(null)}");
            yield return E5Press(make.center);
            yield return E5Wait(1.2f);
            StopRec();
            E5PointerOff();
            D4RealMouse(true);
            E5PlayerName(playerName);

            // ---- King's Pass: out of the portal, the Voice's first words.
            wait = Time.realtimeSinceStartup + 120f;
            GameManager g;
            while (Time.realtimeSinceStartup < wait && ((g = GameManager.UnsafeInstance) == null || g.sceneName != "Tutorial_01" || HeroController.SilentInstance == null))
            {
                UI.PopupText.Clear();
                yield return null;
            }
            E5Log($"菜单：进了 {GameManager.instance.sceneName}，新版AI {EnemyAiMode.Current}，难度 {GameDifficulty.NameOf(GameDifficulty.Current)}，指引 {Items.BossGuide.On}，创造 {mod.Creative}");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            E5NoInfinite();
            int said0 = Items.BossGuide.DevSaid;
            yield return E5Rec("guide_opening");
            float t0 = Time.time;
            var hcol = hero.GetComponent<BoxCollider2D>();
            float startX = hcol.bounds.center.x;
            E5PointerOn(GuiOf((Vector2)hcol.bounds.center + new Vector2(3f, 1.5f)));
            UI.TerraCursor.DevDrives = false;
            while (Time.time - t0 < 50f)
            {
                PlayerData.instance.isInvincible = true;
                UI.PopupText.Clear();
                // A few steps on now and then, as one looks about while listening (not far: the first crawlid waits ahead).
                float since = Time.time - t0;
                // A step or two now and then, back and forth by the portal (further on, the hero goes behind the foreground rocks).
                int step = 0;
                if (since > 4f && (since % 6f) < 0.7f)
                {
                    bool right = ((int)(since / 6f)) % 2 == 0;
                    step = right ? (hcol.bounds.center.x < startX + 1.5f ? 1 : 0) : (hcol.bounds.center.x > startX - 1.5f ? -1 : 0);
                }
                Player.TerraMovement.DevMove = step;
                UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt ?? GuiOf(hcol.bounds.center), GuiOf((Vector2)hcol.bounds.center + new Vector2(3f, 1.5f + Mathf.Sin(since * 0.7f) * 0.6f)), 1f - Mathf.Exp(-3f * Time.deltaTime));
                if (Time.time - t0 > 6f && !Items.BossGuide.DevTalking && Items.BossGuide.DevSaid - said0 >= 7) break;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(1.0f);
            StopRec();
            E5PointerOff();
            E5Log($"菜单：门外之声说了 {Items.BossGuide.DevSaid} 句，最后「{Items.BossGuide.DevLast}」");
            E5PlayerName(null);
        }
    }
}
