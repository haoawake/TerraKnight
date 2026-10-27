using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Net
{
    /// <summary>
    /// Terraria's chat (Main.DrawPlayerChat, RemadeChatMonitor), alone or in a LAN game: Enter opens the line
    /// at the bottom left, Enter sends it, Escape lets it go; the last ten messages stand over it,
    /// each for ten seconds (all ten while typing). A player's message reads "&lt;name&gt; text" and
    /// shows over their head for a while (Player.chatOverhead); the server's lines (joins, PvP,
    /// deaths, commands) come in their own colours. Alone, a line and the commands (/me, /roll,
    /// /playing, /death, /p, /help) are answered here as Terraria's single player answers them. While
    /// typing, the keys type: nothing else in the game hears them.
    /// </summary>
    internal static class NetChat
    {
        struct Line
        {
            public string Text;
            public Color Color;
            public float Time;
        }

        static readonly List<Line> lines = new List<Line>();
        const int ShownLines = 10;
        const float LineSeconds = 10f;
        const int MaxLength = 200;

        /// <summary>The chat line is open: the keyboard types into it.</summary>
        public static bool Typing { get; private set; }

        /// <summary>The keys belong to the chat (typing, or the frame it closed on: its Enter or Escape is no one else's).</summary>
        public static bool BlocksKeys => Typing || Time.frameCount - closedFrame <= 1;

        static string input = "";
        static int openedFrame = -10, closedFrame = -10;
        static bool focus;
        static GUIStyle lineStyle, fieldStyle;

        public static string DevLastLine => lines.Count > 0 ? lines[lines.Count - 1].Text : "";
        public static int DevLines => lines.Count;

        /// <summary>Dev: the lines' texts, oldest first.</summary>
        public static IEnumerable<string> DevTexts() { foreach (var l in lines) yield return l.Text; }

        /// <summary>Dev: whether any line so far has this text in it.</summary>
        public static bool DevHas(string text)
        {
            foreach (var l in lines) if (l.Text.Contains(text)) return true;
            return false;
        }

        public static void Add(string text, Color color)
        {
            lines.Add(new Line { Text = text, Color = color, Time = Time.unscaledTime });
            if (lines.Count > 200) lines.RemoveAt(0);
            Plugin.Log.LogInfo($"[聊天] {text}");
        }

        /// <summary>A line from the server: a player's message (the author's slot), or the server's own (255).</summary>
        public static void Receive(int author, NetText line, Color32 color)
        {
            string text = line.ToString();
            if (author == 255)
            {
                Add(text, color);
                return;
            }
            string name = author == NetSession.MySlot ? NetSession.MyName : NetSession.Player(author)?.Name ?? "?";
            Add($"<{name}> {text}", color);
            // Player.chatOverhead: over the speaker's head, longer for a longer line.
            float seconds = Mathf.Clamp(text.Length / 7f, 4f, 10f);
            if (author == NetSession.MySlot)
            {
                ownOverhead = text;
                ownOverheadUntil = Time.unscaledTime + seconds;
            }
            else if (NetSession.Player(author) is RemotePlayer rp)
            {
                rp.Overhead = text;
                rp.OverheadUntil = Time.unscaledTime + seconds;
            }
        }

        static string ownOverhead;
        static float ownOverheadUntil;

        /// <summary>Alone: the deaths this play (Player.numberOfDeathsPVE, for /death).</summary>
        static int deaths;
        static bool wasDead;

        /// <summary>Each frame of play: Enter opens the line (not while the game holds the Knight: a talk, a cutscene).</summary>
        public static void Tick(bool playing)
        {
            var hero = HeroController.SilentInstance;
            bool dead = hero != null && hero.cState.dead;
            if (dead && !wasDead) deaths++;
            wasDead = dead;
            if (!playing)
            {
                if (Typing) Close();
                return;
            }
            if (Typing) return;
            // A statue's challenge or a Pantheon door's bindings: Enter is its confirm (UI.HkMenus).
            if (Items.InventoryGui.TypingName || Items.Casino.IsOpen || UI.HkMenus.Open) return;
            if (!NetSession.Active && !GameHooks.PlayerHasControl() && !Items.InventoryGui.Open) return;
            var kb = Keyboard.current;
            if (kb == null || Time.frameCount - closedFrame <= 1) return;
            if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.Chat) || (Player.TerraKeys.Get(Player.TerraKeys.Action.Chat) == Key.Enter && kb.numpadEnterKey.wasPressedThisFrame)) Open();
        }

        public static void Open()
        {
            Typing = true;
            input = "";
            openedFrame = Time.frameCount;
            focus = true;
        }

        static void Close()
        {
            Typing = false;
            closedFrame = Time.frameCount;
            unfocus = true;
        }

        /// <summary>Dev: the line let go, as Escape does.</summary>
        public static void DevClose() { if (Typing) Close(); }

        static bool unfocus;

        /// <summary>Sends a line (or a command) as if typed.</summary>
        public static void Say(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            if (!NetSession.Active)
            {
                SayAlone(text);
                return;
            }
            var p = new Packet(Msg.Chat);
            p.W.Write(text.Length > MaxLength ? text.Substring(0, MaxLength) : text);
            NetSession.Send(p.Done());
        }

        static readonly Color32 Yellow = new Color32(255, 240, 20, 255), EmoteOrange = new Color32(200, 100, 0, 255);

        /// <summary>Alone (Terraria's single player, ChatCommandProcessor): the line is the player's own, the commands answered here.</summary>
        static void SayAlone(string text)
        {
            string name = string.IsNullOrEmpty(NetSettings.PlayerName) ? "Knight" : NetSettings.PlayerName;
            if (text.StartsWith("/"))
            {
                string word = text.Split(' ')[0].ToLowerInvariant(), rest = text.Length > word.Length ? text.Substring(word.Length).Trim() : "";
                switch (word)
                {
                    case "/me":
                        Add("*" + name + " " + rest, EmoteOrange);
                        return;
                    case "/roll":
                        // LegacyMultiplayer.9: "rolls a"
                        Add(NetText.Mod("*{0} {1} {2}", name, NetText.Key("LegacyMultiplayer.9"), Random.Range(1, 101)).ToString(), Yellow);
                        return;
                    case "/playing":
                    case "/players":
                        // LegacyMultiplayer.7: "Current players:"
                        Add(NetText.Mod("{0} {1}", NetText.Key("LegacyMultiplayer.7"), name).ToString(), Yellow);
                        return;
                    case "/death":
                    case "/alldeath":
                        Add(NetText.Key("LegacyMultiplayer.23", name, deaths).ToString(), Yellow);
                        return;
                    case "/pvpdeath":
                    case "/allpvpdeath":
                        Add(NetText.Key("LegacyMultiplayer.24", name, 0).ToString(), Yellow);
                        return;
                    case "/p":
                        // LegacyMultiplayer.10: "You are not in a party!"
                        Add(NetText.Key("LegacyMultiplayer.10").ToString(), Yellow);
                        return;
                    case "/help":
                        foreach (var k in new[] { "Party", "Emote", "Playing", "Roll", "Death", "PVPDeath", "AllDeath", "AllPVPDeath", "Help" })
                            Add(NetText.Key("ChatCommandDescription." + k).ToString(), Yellow);
                        return;
                }
            }
            Add($"<{name}> {text}", Color.white);
            ownOverhead = text;
            ownOverheadUntil = Time.unscaledTime + Mathf.Clamp(text.Length / 7f, 4f, 10f);
        }

        public static void Draw()
        {
            // Alone the game's own lines (a townsperson come, Main.NewText) show all the same, while fresh.
            if (!Typing && NetSession.State == NetSession.Phase.Off && (lines.Count == 0 || Time.unscaledTime - lines[lines.Count - 1].Time > LineSeconds)) return;
            var e = Event.current;
            float ui = UI.GameView.Scale;
            var view = UI.GameView.Rect;
            if (lineStyle == null)
            {
                lineStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                fieldStyle = new GUIStyle(GUI.skin.textField) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                fieldStyle.normal.background = fieldStyle.focused.background = fieldStyle.hover.background = fieldStyle.active.background = null;
                fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = Color.white;
            }
            if (unfocus)
            {
                unfocus = false;
                GUI.FocusControl(null);
            }
            lineStyle.fontSize = Mathf.RoundToInt(19 * ui);
            fieldStyle.fontSize = Mathf.RoundToInt(19 * ui);

            // The typed line's keys before the field sees them: Enter sends, Escape lets go.
            if (Typing && e.type == EventType.KeyDown && Time.frameCount != openedFrame)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    e.Use();
                    Say(input);
                    Close();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    e.Use();
                    Close();
                }
            }

            float x = view.x + 78f * ui, bottom = view.yMax - 36f * ui;
            if (Typing)
            {
                // Main.DrawPlayerChat: the box along the bottom, the text in it.
                var box = new Rect(x, bottom, view.width - 300f * ui, 32f * ui);
                Fill(box, new Color(0.08f, 0.1f, 0.22f, 0.75f));
                Fill(new Rect(box.x, box.y, box.width, 1.5f * ui), new Color(0.5f, 0.55f, 0.8f, 0.9f));
                GUI.SetNextControlName("tk_chat");
                input = GUI.TextField(new Rect(box.x + 10f * ui, box.y, box.width - 20f * ui, box.height), input ?? "", MaxLength, fieldStyle);
                if (focus && e.type == EventType.Repaint)
                {
                    GUI.FocusControl("tk_chat");
                    focus = false;
                }
            }
            if (e.type != EventType.Repaint) return;
            // The messages over the box, newest at the bottom.
            int shown = 0;
            float y = bottom - 30f * ui;
            for (int i = lines.Count - 1; i >= 0 && shown < ShownLines; i--)
            {
                var l = lines[i];
                float age = Time.unscaledTime - l.Time;
                if (!Typing && age > LineSeconds) break;
                float a = Typing ? 1f : Mathf.Clamp01((LineSeconds - age) / 1f);
                var c = l.Color;
                c.a = a;
                RemotePlayer.Outlined(new Rect(x + 10f * ui, y, view.width - 320f * ui, 28f * ui), l.Text, lineStyle, c);
                y -= 28f * ui;
                shown++;
            }
            // The local player's own line over their head.
            if (ownOverhead != null && Time.unscaledTime < ownOverheadUntil) DrawOwnOverhead(ui);
        }

        static void DrawOwnOverhead(float ui)
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            var hero = HeroController.SilentInstance;
            if (cam == null || hero == null) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var top = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y + 0.25f, 0f));
            if (top.z < 0f) return;
            var st = new GUIStyle(lineStyle) { alignment = TextAnchor.LowerCenter, wordWrap = true, fontSize = Mathf.RoundToInt(16 * ui) };
            RemotePlayer.Outlined(new Rect(top.x - 180f * ui, Screen.height - top.y - 70f * ui, 360f * ui, 64f * ui), ownOverhead, st, Color.white);
        }

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
