using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class TerraMenu
    {
        // The world creation's choices (UIWorldCreation's options).
        static string worldName = "";
        static DifficultyLevel difficulty = DifficultyLevel.Normal;
        static EnemyAiKind ai = EnemyAiKind.New;
        static WeaponKind weapons = WeaponKind.New;
        static bool guide = true;

        static bool nameTyping;
        static readonly ImeTextEntry nameInput = new ImeTextEntry(NameTyped);

        /// <summary>Terraria's defaults for a new world (Classic: 普通 here), the new enemy AI, the remade weapons, the guidance followed, a random name.</summary>
        static void ResetWorldOptions()
        {
            EndNameTyping();
            worldName = RandomWorldName();
            difficulty = DifficultyLevel.Normal;
            ai = EnemyAiKind.New;
            weapons = WeaponKind.New;
            guide = true;
        }

        static void BeginNameTyping()
        {
            if (nameTyping) return;
            EndRename(true);
            nameTyping = true;
            nameInput.Begin();
        }

        static void EndNameTyping()
        {
            if (!nameTyping) return;
            nameTyping = false;
            nameInput.End();
            typingEnded = Time.frameCount;
        }

        static void NameTyped(char c)
        {
            if (!nameTyping || char.IsControl(c)) return;
            if (worldName.Length < MaxNameLength) worldName += c;
        }

        static readonly Dictionary<string, List<string>[]> nameWords = new Dictionary<string, List<string>[]>();

        /// <summary>
        /// UIWorldCreation's random name (WorldGen's RandomWorldName): one of Terraria's compositions with a random adjective,
        /// noun and place from its own lists, in the language being played.
        /// </summary>
        static string RandomWorldName()
        {
            string culture = Lang.Culture;
            if (!nameWords.TryGetValue(culture, out var words))
            {
                words = new[]
                {
                    TerrariaText.Category(culture, "RandomWorldName_Composition"), TerrariaText.Category(culture, "RandomWorldName_Adjective"),
                    TerrariaText.Category(culture, "RandomWorldName_Noun"), TerrariaText.Category(culture, "RandomWorldName_Location"),
                };
                nameWords[culture] = words;
            }
            string Pick(List<string> l) => l[Random.Range(0, l.Count)];
            if (words[0].Count == 0 || words[1].Count == 0 || words[2].Count == 0 || words[3].Count == 0)
                return Lang.F("世界 {0}", rows.Count + 1);
            string name = Pick(words[0]).Replace("{Adjective}", Pick(words[1])).Replace("{Noun}", Pick(words[2])).Replace("{Location}", Pick(words[3]));
            return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name;
        }

        /// <summary>
        /// UIWorldCreation: the name (with its random button), the character it is for in the seed's place, the preview,
        /// four rows of options (the five difficulties, the enemy AI, the weapons, the guidance), the hovered option's description,
        /// 返回 and 创建.
        /// </summary>
        static void DrawWorld()
        {
            float s = TerraUi.S;
            float w = 500f * s;
            var box = new Rect((Screen.width - w) / 2f, (170f - 18f) * s, w, 500f * s);
            var panel = new Rect(box.x, box.y + 50f * s, w, 346f * s);
            TerraUi.Panel(panel, TerraUi.ListBack, Color.black);
            var info = new Rect(panel.x + 10f * s, panel.y + 8f * s, panel.width - 20f * s, panel.height - 20f * s);
            string desc = null;
            var plate = new Rect(info.x + 44f * s, info.y, info.width - 132f * s, 40f * s);
            // A click anywhere but the name ends its typing.
            var e = Event.current;
            if (nameTyping && e.type == EventType.MouseDown && !plate.Contains(e.mousePosition)) EndNameTyping();

            // The name and its random button.
            var dice = new Rect(info.x, info.y, 40f * s, 40f * s);
            if (TerraUi.Option(dice, TerraUi.Tex("UI/WorldCreation/IconRandomName"), null, Color.white, false, "dice", false))
            {
                EndNameTyping();
                worldName = RandomWorldName();
            }
            if (TerraUi.Over(dice)) desc = TT("UI.WorldCreationRandomizeNameDescription", "随机世界名称。");
            string shown = nameTyping ? worldName + nameInput.Composition + Caret() : worldName;
            if (NamePlate(plate, TT("UI.WorldCreationName", "名字："), shown, nameTyping, "name"))
            {
                if (nameTyping) EndNameTyping();
                else BeginNameTyping();
            }
            if (nameTyping) nameInput.Anchor(new Vector2(plate.x + 20f * s + TerraUi.Measure(TT("UI.WorldCreationName", "名字："), TerraUi.Normal * s).x + TerraUi.Measure(shown, TerraUi.Normal * s).x, plate.yMax));
            if (TerraUi.Over(plate)) desc = TT("UI.WorldDescriptionName", "给你的世界起个名字吧！如有需要，您随时可以更改它！");

            // The character this world is made for, where Terraria has the seed: back to the character screen.
            var face = new Rect(info.x, info.y + 44f * s, 40f * s, 40f * s);
            var who = new Rect(plate.x, face.y, plate.width, 40f * s);
            bool backToCharacter = TerraUi.Option(face, null, null, Color.white, false, "face", false);
            CharacterPortrait.DrawHead(new Rect(face.x + 4f * s, face.y + 4f * s, face.width - 8f * s, face.height - 8f * s), CharacterCreation.Look ?? Player.Appearance.Default(), Color.clear);
            backToCharacter |= NamePlate(who, Lang.T("人物："), CharacterCreation.Name, false, "who");
            if (TerraUi.Over(face) || TerraUi.Over(who)) desc = Lang.T("这个世界的主角。点这里回到人物创建，修改外观、名字和模式。");

            DrawPreview(new Rect(info.xMax - 84f * s, info.y, 84f * s, 84f * s));

            // The options, a row each between Terraria's separators.
            float top = info.y + 94f * s;
            TerraUi.Separator(info.x, top - 6f * s, info.width);
            var levels = new[] { DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Expert, DifficultyLevel.Master, DifficultyLevel.Hollow };
            for (int i = 0; i < levels.Length; i++)
            {
                var d = levels[i];
                var r = OptionRect(info, top, i, levels.Length, 1f);
                devRects["difficulty" + (int)d] = r;
                bool tinted = d == DifficultyLevel.Hard || d == DifficultyLevel.Hollow;
                if (TerraUi.Option(r, TerraUi.Tex(GameDifficulty.IconOf(d)), GameDifficulty.NameOf(d), GameDifficulty.ColorOf(d), difficulty == d, "d" + i, true, tinted ? GameDifficulty.ColorOf(d) : (Color?)null))
                {
                    difficulty = d;
                    Sound("Menu_Tick");
                }
                if (TerraUi.Over(r)) desc = DifficultyText(d);
            }
            top += 48f * s;
            TerraUi.Separator(info.x, top - 8f * s, info.width);
            for (int i = 0; i < 2; i++)
            {
                var k = i == 0 ? EnemyAiKind.Vanilla : EnemyAiKind.New;
                var r = OptionRect(info, top, i, 2, 4f);
                string icon = k == EnemyAiKind.New ? "UI/WorldCreation/IconEvilCrimson" : "UI/WorldCreation/IconEvilCorruption";
                string title = k == EnemyAiKind.New ? Lang.T("新版敌人AI") : Lang.T("原版敌人AI");
                var color = k == EnemyAiKind.New ? TerraUi.C(205, 92, 92) : TerraUi.C(147, 112, 219);
                if (TerraUi.Option(r, TerraUi.Tex(icon), title, color, ai == k, "ai" + i))
                {
                    ai = k;
                    Sound("Menu_Tick");
                }
                if (TerraUi.Over(r)) desc = k == EnemyAiKind.New
                    ? Lang.T("小怪、精英和首领按泰拉人物的高机动重新设计的攻击与索敌。")
                    : Lang.T("空洞骑士原本的攻击和索敌。");
            }
            top += 48f * s;
            TerraUi.Separator(info.x, top - 8f * s, info.width);
            // The weapons (World.WeaponMode): the remade ones or Terraria's own, for good with this world.
            for (int i = 0; i < 2; i++)
            {
                var k = i == 0 ? WeaponKind.Vanilla : WeaponKind.New;
                var r = OptionRect(info, top, i, 2, 4f);
                devRects["weapons" + i] = r;
                if (TerraUi.Option(r, null, WeaponMode.NameOf(k), k == WeaponKind.New ? TerraUi.C(255, 214, 115) : Color.white, weapons == k, "wp" + i))
                {
                    weapons = k;
                    Sound("Menu_Tick");
                }
                if (TerraUi.Over(r)) desc = k == WeaponKind.New
                    ? Lang.T("重做的武器：近战连招和右键技艺，长枪、悠悠球、链枷、投掷武器、短剑和鞭子的新招式，魔法武器的施法动作。建好世界后不能更改。")
                    : Lang.T("泰拉瑞亚原本的武器用法和动作。建好世界后不能更改。");
            }
            top += 48f * s;
            TerraUi.Separator(info.x, top - 8f * s, info.width);
            for (int i = 0; i < 2; i++)
            {
                bool follow = i == 0;
                var r = OptionRect(info, top, i, 2, 4f);
                string title = follow ? Items.BossGuide.FollowLabel : Items.BossGuide.RefuseLabel;
                if (TerraUi.Option(r, null, Items.BossGuide.Title + "：" + title, follow ? new Color(0.82f, 0.7f, 1f) : Color.white, guide == follow, "g" + i))
                {
                    guide = follow;
                    Sound("Menu_Tick");
                }
                if (TerraUi.Over(r)) desc = follow
                    ? Lang.T("每场首领战第一次打赢后，门后的声音会评价这场胜利，并用地标暗示下一个对手在哪里。")
                    : Lang.T("不听那个声音的指引。") + Items.BossGuide.QuestionNote;
            }
            top += 48f * s;
            TerraUi.Separator(info.x, top - 8f * s, info.width);

            // The description of what the mouse is on (UI.WorldDescriptionDefault when nothing).
            var dr = new Rect(info.x, info.yMax - 40f * s + 2f * s, info.width, 40f * s);
            TerraUi.Spliced(dr, TerraUi.Tex("UI/CharCreation/CategoryPanelHighlight"), 10, new Color(0.83f, 0.83f, 0.83f, 0.7f));
            TerraUi.Text(new Rect(dr.x + 20f * s, dr.y + 3f * s, dr.width - 40f * s, dr.height - 6f * s), desc ?? TT("UI.WorldDescriptionDefault", "请选择上述选项来构建你的世界。"),
                TerraUi.Small * s, Color.white, TextAnchor.MiddleLeft, true);

            float bw = w / 2f - 10f * s, by = box.yMax - 95f * s;
            if (TerraUi.BigButton(new Rect(box.x, by, bw, 50f * s), TT("UI.Back", "返回"), "wback") || Escaped())
            {
                Sound("Menu_Close");
                BackToCharacter();
                return;
            }
            if (backToCharacter)
            {
                Sound("Menu_Tick");
                BackToCharacter();
                return;
            }
            if (TerraUi.BigButton(new Rect(box.xMax - bw, by, bw, 50f * s), TT("UI.Create", "创建"), "create")) Create();
        }

        /// <summary>GroupOptionButton's place in a row of <paramref name="n"/>: an equal share less the gaps, spread across (HAlign i/(n-1)).</summary>
        static Rect OptionRect(Rect info, float top, int i, int n, float gap)
        {
            float s = TerraUi.S;
            float w = info.width / n - gap * (n - 1) * s;
            float x = info.x + (n > 1 ? (info.width - w) * i / (n - 1) : 0f);
            return new Rect(x, top, w, 34f * s);
        }

        /// <summary>UICharacterNameButton: CategoryPanel at half white, its title, then the text (grey "<空>" when empty), a white border under the mouse.</summary>
        static bool NamePlate(Rect r, string title, string text, bool active, string id)
        {
            float s = TerraUi.S;
            bool over = TerraUi.Hover(r, id);
            TerraUi.Spliced(r, TerraUi.Tex("UI/CharCreation/CategoryPanel"), 10, new Color(1f, 1f, 1f, 0.5f));
            if (over || active) TerraUi.Spliced(r, TerraUi.Tex(active ? "UI/CharCreation/CategoryPanelHighlight" : "UI/CharCreation/CategoryPanelBorder"), 10, Color.white);
            TerraUi.Text(new Rect(r.x + 10f * s, r.y, r.width - 20f * s, r.height), title, TerraUi.Normal * s, Color.white);
            float tx = r.x + 10f * s + TerraUi.Measure(title, TerraUi.Normal * s).x + 20f * s;
            bool empty = string.IsNullOrEmpty(text) || text == "|" || text == " ";
            TerraUi.Text(new Rect(tx, r.y, r.xMax - tx - 8f * s, r.height), empty && !active ? TT("UI.WorldCreationNameEmpty", "<空>") : text, TerraUi.Normal * s, empty && !active ? Color.gray : Color.white);
            return TerraUi.Clicked(r);
        }

        /// <summary>UIWorldCreationPreview: the difficulty's sky, the world, the evil (red for the new enemies), the bunny, the frame.</summary>
        static void DrawPreview(Rect r)
        {
            float s = TerraUi.S;
            var at = new Rect(r.x + 4f * s, r.y + 4f * s, r.width - 8f * s, r.height - 8f * s);
            Texture2D T(string n) => TerraUi.Tex("UI/WorldCreation/" + n);
            void Layer(string n, Color c)
            {
                var t = T(n);
                if (t != null) TerraUi.Whole(TerraUi.At(t, at.x, at.y), t, c);
            }
            bool hard = difficulty == DifficultyLevel.Master || difficulty == DifficultyLevel.Hollow;
            string sky = hard ? "PreviewDifficultyMaster1" : difficulty == DifficultyLevel.Expert ? "PreviewDifficultyExpert1" : "PreviewDifficultyNormal1";
            Layer(sky, Color.white);
            var c2 = difficulty == DifficultyLevel.Expert || hard ? new Color(0.66f, 0.66f, 0.66f) : Color.white;
            Layer("PreviewSizeMedium", c2);
            Layer(ai == EnemyAiKind.New ? "PreviewEvilCrimson" : "PreviewEvilCorruption", c2);
            string bunny = CharacterCreation.Style == PlayStyle.Journey ? "PreviewDifficultyCreative2" : hard ? "PreviewDifficultyMaster2" : difficulty == DifficultyLevel.Expert ? "PreviewDifficultyExpert2" : "PreviewDifficultyNormal2";
            Layer(bunny, difficulty == DifficultyLevel.Hollow ? GameDifficulty.ColorOf(difficulty) : c2);
            var frame = T("PreviewBorder");
            if (frame != null) TerraUi.Whole(TerraUi.At(frame, r.x, r.y), frame, Color.white);
        }

        static string DifficultyText(DifficultyLevel d)
        {
            int pct = Mathf.RoundToInt(GameDifficulty.FactorOf(d) * 100f);
            switch (d)
            {
                case DifficultyLevel.Normal: return TT("UI.WorldDescriptionNormal", "你的冒险之旅即将开始……") + Lang.F("敌人血量与伤害为大师的 {0}%。", pct);
                case DifficultyLevel.Hard: return Lang.F("比经典更难的旅程。敌人血量与伤害为大师的 {0}%。", pct);
                case DifficultyLevel.Expert: return TT("UI.WorldDescriptionExpert", "为财富和荣誉而战，孩子。") + Lang.F("敌人血量与伤害为大师的 {0}%。", pct);
                case DifficultyLevel.Hollow: return Lang.T("保留大师数值与原生面具增伤。角色生命上限为85%，治疗药冷却延长20%；敌人掉落材料和吉欧额外增加40%。与生命锁同时开启时取70%，不重复叠罚。环境伤害每次固定为当前生命上限的50%，生命上限本身不变。");
                default: return TT("UI.WorldDescriptionMaster", "游戏结束了，伙计，游戏结束了！") + Lang.T("敌人是模组原本的满额数值。");
            }
        }
    }
}
