using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Voice Beyond the Door: the one who pulled the player through the Old One's Army's portal, speaking once
    /// each of the 48 fights is first won (a new claim in <see cref="BossMastery"/>: a fight already won, a rematch,
    /// one body of several, says nothing), and once at the start of a save no fight has been won in. What it says is
    /// the win's (or, the fight won after a later stage's, the late win's), then the way on: the first fight not yet won,
    /// in the route's order, of the won fight's stage or a later one, so the stage's others come first and the next stage
    /// after them; nothing left from there on, the first left behind. After the last fight, the closing words.
    /// It waits for the fight to be over: the boss's loot paid (<see cref="BossLootTiming"/>), its own talk done (the
    /// game's dialogue box down, the told lines of <see cref="UI.DialogueBroadcast"/> out), the player in control, all
    /// of it quiet for a few seconds; a talk of the game's that starts meanwhile holds it until it's over. A win not yet
    /// spoken of is kept in the save (an ending's cinematic and the menu come before the quiet: the Hollow Knight, the
    /// Radiance) and spoken of after the next load; its way on is worked out as it is said, from the wins by then. Each
    /// line shows as a subtitle in the upper middle of the screen, and goes into the chat to be read again. Each player
    /// hears it for their own wins, in a save that follows the guidance (<see cref="On"/>).
    /// </summary>
    internal static partial class BossGuide
    {
        struct Page
        {
            public bool Voice;
            public string Text;
        }

        const string OpeningFlag = "boss.guide.opening.v1", ClosingFlag = "boss.guide.closing.v1";
        /// <summary>A win to be spoken of: this, its relic, and ".late" when won after a later stage's.</summary>
        const string PendingPrefix = "boss.guide.pending.v1.";
        /// <summary>Quiet this long before the voice speaks, and this long after a fight's first win.</summary>
        const float QuietBefore = 3f, AfterWin = 4f, QuietAgain = 1.5f;
        static readonly Color VoiceColor = new Color(0.82f, 0.7f, 1f), NameColor = new Color(0.66f, 0.46f, 1f), PlayerNameColor = new Color(1f, 0.86f, 0.5f);

        /// <summary>What is to be said: a won fight's words (worked out when said), or fixed lines (the opening).</summary>
        sealed class Talk
        {
            public Entry Fight;
            public bool Late;
            public List<Page> Lines;
        }

        static readonly Queue<Talk> waiting = new Queue<Talk>();
        static List<Page> talk;
        static Talk saying;
        static bool pendingRead;
        static int page;
        static float pageTime, pageLength, queuedAt, quietFor, lookAt;
        static bool held, boxUp;
        static GUIStyle nameStyle, textStyle;

        public static int DevSaid, DevQueued;
        public static string DevLast = "";
        public static bool DevTalking => talk != null;

        static bool Chinese => Lang.IsSource || Lang.Culture.StartsWith("zh");
        static string Pick(string zh, string en) => Chinese || string.IsNullOrEmpty(en) ? zh : en;
        static Page Voice(string zh, string en) => new Page { Voice = true, Text = Pick(zh, en) };

        static Entry Find(ItemType relic)
        {
            foreach (var e in Route) if (e.Relic == relic) return e;
            return null;
        }

        /// <summary>The fights this save has won (BossMastery's claims, its old saves' kills among them).</summary>
        static HashSet<ItemType> Won(TerraMod mod)
        {
            BossMastery.Initialize(mod);
            var won = new HashSet<ItemType>();
            foreach (string flag in mod.GrantedAbilities)
                if (flag.StartsWith(BossMastery.Prefix, System.StringComparison.Ordinal)
                    && System.Enum.TryParse(flag.Substring(BossMastery.Prefix.Length), out ItemType relic)) won.Add(relic);
            return won;
        }

        /// <summary>The way on from a stage: the first fight not won of it or a later one; else the first not won at all (behind).</summary>
        static Entry Next(HashSet<ItemType> won, int stage, out bool onward, out bool behind)
        {
            onward = behind = false;
            foreach (var e in Route)
                if (!won.Contains(e.Relic) && e.Stage >= stage)
                {
                    onward = e.Stage > stage;
                    return e;
                }
            foreach (var e in Route)
                if (!won.Contains(e.Relic))
                {
                    behind = true;
                    return e;
                }
            return null;
        }

        /// <summary>A fight first won (Bestiary.Credit, as BossMastery claims it): its words, said once it has played out.</summary>
        public static void FirstWin(string record, string scene, string bossName)
        {
            var mod = TerraMod.Instance;
            var relic = Relics.Of(record, scene, bossName);
            var fight = relic.HasValue ? Find(relic.Value) : null;
            if (mod == null || fight == null || !On) return;
            var won = Won(mod);
            int furthest = -1;
            foreach (var e in Route)
                if (e != fight && won.Contains(e.Relic)) furthest = Mathf.Max(furthest, e.Stage);
            bool late = fight.Stage < furthest && fight.LateZh != null;
            // Heard the first words already, or past them: a save's first win means the start is behind it.
            mod.GrantedAbilities.Add(OpeningFlag);
            mod.GrantedAbilities.Add(PendingPrefix + fight.Relic + (late ? ".late" : ""));
            waiting.Enqueue(new Talk { Fight = fight, Late = late });
            queuedAt = Time.unscaledTime;
            DevQueued++;
            Plugin.Log.LogInfo($"[神的指引] 首胜 {fight.Relic}（阶段 {fight.Stage}{(late ? $"，已打过阶段 {furthest}，回头补打" : "")}），等战斗演完再说");
        }

        /// <summary>A won fight's words as they are said: the win (or the late win), then the way on from the wins by now.</summary>
        static List<Page> Compose(TerraMod mod, Talk t)
        {
            var fight = t.Fight;
            var won = Won(mod);
            won.Add(fight.Relic);
            var pages = new List<Page> { t.Late ? Voice(fight.LateZh, fight.LateEn) : Voice(fight.WinZh, fight.WinEn) };
            var next = Next(won, fight.Stage, out bool onward, out bool behind);
            if (next != null)
            {
                if (onward) pages.Add(Voice(OnwardZh, OnwardEn));
                if (behind) pages.Add(Voice(BehindZh, BehindEn));
                pages.Add(Hint(next));
            }
            else if (mod.GrantedAbilities.Add(ClosingFlag))
                foreach (var line in Closing) pages.Add(new Page { Voice = line.Voice, Text = Pick(line.Zh, line.En) });
            Plugin.Log.LogInfo($"[神的指引] {fight.Relic}{(t.Late ? "（越级后回头）" : "")}：" +
                               $"指向 {(next != null ? $"{next.Relic}（阶段 {next.Stage}{(onward ? "，下一阶段" : behind ? "，身后" : "，同阶段")}）" : "收束对白")}");
            return pages;
        }

        /// <summary>Once a save is in play: the wins it kept unspoken of, in the route's order.</summary>
        static void ReadPending(TerraMod mod)
        {
            pendingRead = true;
            var queued = new HashSet<ItemType>();
            foreach (var t in waiting) if (t.Fight != null) queued.Add(t.Fight.Relic);
            var late = new HashSet<ItemType>();
            var found = new HashSet<ItemType>();
            foreach (string flag in mod.GrantedAbilities)
            {
                if (!flag.StartsWith(PendingPrefix, System.StringComparison.Ordinal)) continue;
                string rest = flag.Substring(PendingPrefix.Length);
                bool isLate = rest.EndsWith(".late");
                if (isLate) rest = rest.Substring(0, rest.Length - 5);
                if (!System.Enum.TryParse(rest, out ItemType relic) || queued.Contains(relic)) continue;
                found.Add(relic);
                if (isLate) late.Add(relic);
            }
            foreach (var e in Route)
                if (found.Contains(e.Relic)) waiting.Enqueue(new Talk { Fight = e, Late = late.Contains(e.Relic) && e.LateZh != null });
            if (found.Count > 0) Plugin.Log.LogInfo($"[神的指引] 存档里还有 {found.Count} 场首胜没说");
        }

        static void Said(TerraMod mod, Talk t)
        {
            if (mod == null || t?.Fight == null) return;
            mod.GrantedAbilities.Remove(PendingPrefix + t.Fight.Relic);
            mod.GrantedAbilities.Remove(PendingPrefix + t.Fight.Relic + ".late");
        }

        /// <summary>The start of a save no fight has been won in: the first words, ending with the way to the first fight.</summary>
        static void TryOpening()
        {
            var mod = TerraMod.Instance;
            if (mod == null || mod.GrantedAbilities.Contains(OpeningFlag) || Player.OpeningPortal.Active || quietFor < QuietBefore) return;
            var won = Won(mod);
            mod.GrantedAbilities.Add(OpeningFlag);
            if (won.Count > 0) return;
            var pages = new List<Page>();
            foreach (var line in Opening) pages.Add(new Page { Voice = line.Voice, Text = Pick(line.Zh, line.En) });
            var first = Next(won, 0, out _, out _);
            if (first != null) pages.Add(Voice(HintZhOf(first) + OpeningLastZh, HintEnOf(first) + " " + OpeningLastEn));
            waiting.Enqueue(new Talk { Lines = pages });
            queuedAt = Time.unscaledTime - AfterWin;
            Plugin.Log.LogInfo("[神的指引] 开场对白");
        }

        /// <summary>Nothing else going on: no boss's loot waiting, no talk of the game's, the player in control.</summary>
        internal static bool Quiet()
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.gameState != GlobalEnums.GameState.PLAYING || gm.IsInSceneTransition) return false;
            if (!hero.acceptingInput || hero.controlReqlinquished || hero.cState.dead || hero.cState.transitioning) return false;
            if (BossLootTiming.DevPending > 0 || UI.DialogueBroadcast.Telling) return false;
            if (Time.unscaledTime >= lookAt)
            {
                lookAt = Time.unscaledTime + 0.25f;
                boxUp = UI.HudDialogue.DevOpen;
            }
            return !boxUp;
        }

        /// <summary>Each frame: in play (not paused), the voice's lines one after another; at the menu, whatever was to be said goes.</summary>
        public static void Tick(bool playing)
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.gameState == GlobalEnums.GameState.MAIN_MENU)
            {
                Clear();
                return;
            }
            // Paused, or time stopped (the inventory, the map): the line waits where it is.
            if (!playing || Time.timeScale == 0f) return;
            // A save that doesn't follow the guidance hears nothing.
            if (!On)
            {
                if (talk != null || waiting.Count > 0) Clear();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            bool quiet = Quiet();
            quietFor = quiet ? quietFor + dt : 0f;
            if (talk == null)
            {
                var mod = TerraMod.Instance;
                if (mod == null) return;
                if (!pendingRead) ReadPending(mod);
                if (waiting.Count == 0)
                {
                    TryOpening();
                    return;
                }
                if (quietFor < QuietBefore || Time.unscaledTime - queuedAt < AfterWin) return;
                // The player's own words (StoryBeats) said first.
                if (StoryBeats.Talking) return;
                saying = waiting.Dequeue();
                talk = saying.Lines ?? Compose(mod, saying);
                page = -1;
                held = false;
                NextPage();
                return;
            }
            // The game's own talk (a boss's words after its fight, a cutscene) holds the voice; the line starts over after.
            if (!quiet)
            {
                held = true;
                return;
            }
            if (held)
            {
                if (quietFor < QuietAgain) return;
                held = false;
                pageTime = 0f;
            }
            pageTime += dt;
            if (pageTime >= pageLength) NextPage();
        }

        static void NextPage()
        {
            page++;
            if (talk == null || page >= talk.Count)
            {
                Said(TerraMod.Instance, saying);
                talk = null;
                saying = null;
                return;
            }
            var p = talk[page];
            pageTime = 0f;
            pageLength = Mathf.Clamp(1.6f + p.Text.Length * (Chinese ? 0.12f : 0.05f), 3f, 9f);
            string who = Speaker(p);
            bool first = true;
            foreach (var part in ChatParts(p.Text))
            {
                Net.NetChat.Add(first ? $"<{who}> {part}" : "    " + part, p.Voice ? VoiceColor : Color.white);
                first = false;
            }
            DevSaid++;
            DevLast = p.Text;
        }

        static string Speaker(Page p) => p.Voice ? Pick("门外之声", "Voice Beyond the Door") : PlayerSpeaker;

        /// <summary>The player's name over their own lines (the multiplayer name: the one set, else Steam's).</summary>
        internal static string PlayerSpeaker
        {
            get
            {
                string name = Net.NetSettings.PlayerName;
                return string.IsNullOrEmpty(name) ? Pick("你", "You") : name;
            }
        }

        /// <summary>A long line cut in two for the chat (it doesn't wrap): at the semicolon or comma nearest its middle.</summary>
        internal static IEnumerable<string> ChatParts(string text)
        {
            int limit = Chinese ? 34 : 90;
            if (text.Length <= limit)
            {
                yield return text;
                yield break;
            }
            int best = -1;
            for (int i = 1; i < text.Length - 1; i++)
            {
                char c = text[i];
                if (c != '；' && c != '，' && c != ';' && c != ',' && c != '。' && c != '.') continue;
                if (best < 0 || Mathf.Abs(i - text.Length / 2) < Mathf.Abs(best - text.Length / 2)) best = i;
            }
            if (best < 0)
            {
                yield return text;
                yield break;
            }
            yield return text.Substring(0, best + 1).TrimEnd();
            yield return text.Substring(best + 1).TrimStart();
        }

        public static void Clear()
        {
            waiting.Clear();
            pendingRead = false;
            saying = null;
            talk = null;
            held = false;
            quietFor = 0f;
        }

        /// <summary>The line being said: the speaker's name over it, in the upper middle of the screen, fading in and out.</summary>
        public static void Draw()
        {
            if (talk == null || held || page < 0 || page >= talk.Count || InventoryGui.Open || Event.current.type != EventType.Repaint) return;
            var p = talk[page];
            float a = Mathf.Clamp01(pageTime / 0.3f) * Mathf.Clamp01((pageLength - pageTime) / 0.45f);
            if (a <= 0f) return;
            DrawSubtitle(Speaker(p), p.Text, p.Voice ? NameColor : PlayerNameColor, p.Voice ? VoiceColor : Color.white, a);
        }

        /// <summary>A subtitle in the upper middle of the screen: the speaker's name over the line (the Voice's, the player's).</summary>
        internal static void DrawSubtitle(string who, string text, Color nameColor, Color textColor, float a)
        {
            float ui = UI.GameView.Scale;
            var view = UI.GameView.Rect;
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, wordWrap = false, clipping = TextClipping.Overflow };
                textStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true, clipping = TextClipping.Overflow };
            }
            nameStyle.fontSize = Mathf.RoundToInt(19 * ui);
            textStyle.fontSize = Mathf.RoundToInt(25 * ui);
            float w = Mathf.Min(860f * ui, view.width - 80f * ui);
            var name = new Rect(view.center.x - w / 2f, view.y + view.height * 0.13f, w, 26f * ui);
            var body = new Rect(name.x, name.yMax + 4f * ui, w, 100f * ui);
            Outlined(name, who, nameStyle, nameColor, a, ui);
            Outlined(body, text, textStyle, textColor, a, ui);
        }

        static void Outlined(Rect r, string text, GUIStyle style, Color color, float a, float ui)
        {
            var prev = style.normal.textColor;
            UI.BorderText.Draw(r, text, style, new Color(0f, 0f, 0f, 0.85f * a), new Color(color.r, color.g, color.b, a), 2f * ui);
            style.normal.textColor = prev;
        }
    }
}
