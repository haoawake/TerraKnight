using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Whether this save hears the voice: asked when it is created (UI.CreativeMenu, after its difficulty), changed any time
    /// in the Terraria settings' guidance tab. A save from before the question was asked doesn't hear it until it is
    /// turned on. Off, nothing is said or kept to be said; on again, the voice picks up with the way on from the wins by
    /// then (or the opening, in a save with none).
    /// </summary>
    internal static partial class BossGuide
    {
        const string AskedFlag = "boss.guide.asked.v1", OnFlag = "boss.guide.on.v1";

        public static string Title => Pick("神的指引", "Guidance");
        public static string Question => Pick(
            "穿过旧日军团的传送门之后，一个不肯透露名字的声音会一路跟着你：每场首领战第一次打赢后，它评价这场胜利，并用地标暗示下一个对手在哪里。\n\n要听从它的指引吗？",
            "Past the Old One's Army's portal, a voice that won't give its name will follow you: after each boss fight's first win it speaks of the win and hints, by landmarks, where the next opponent is.\n\nWill you follow its guidance?");
        public static string QuestionNote => Pick("之后可以在“泰拉瑞亚设置 → 神的指引”里随时开启或关闭。", "It can be turned on or off any time in Terraria settings → Guidance.");
        public static string FollowLabel => Pick("听从", "Follow");
        public static string RefuseLabel => Pick("拒绝", "Refuse");

        const string RejoinZh = "你终于肯听了。", RejoinEn = "So you will listen at last.";

        /// <summary>This save hears the voice.</summary>
        public static bool On
        {
            get
            {
                var mod = TerraMod.Instance;
                return mod != null && mod.GrantedAbilities.Contains(OnFlag);
            }
        }

        /// <summary>A new game (GameHooks' StartNewGame, after the save's reset): the answer given when it was created.</summary>
        public static void NewGame(bool follow)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            Clear();
            mod.GrantedAbilities.Add(AskedFlag);
            if (follow) mod.GrantedAbilities.Add(OnFlag);
            else mod.GrantedAbilities.Remove(OnFlag);
        }

        /// <summary>The settings' switch: off, the voice stops and drops what it had to say; on, it picks up from here.</summary>
        public static void SetOn(bool on)
        {
            var mod = TerraMod.Instance;
            if (mod == null || on == On) return;
            mod.GrantedAbilities.Add(AskedFlag);
            if (!on)
            {
                mod.GrantedAbilities.Remove(OnFlag);
                mod.GrantedAbilities.RemoveWhere(f => f.StartsWith(PendingPrefix, System.StringComparison.Ordinal));
                Clear();
                Plugin.Log.LogInfo("[神的指引] 关闭");
                return;
            }
            mod.GrantedAbilities.Add(OnFlag);
            Clear();
            var won = Won(mod);
            int count = 0;
            foreach (var e in Route) if (won.Contains(e.Relic)) count++;
            // No win yet and the opening not heard: the opening, as at a new game's start.
            if (count == 0 && !mod.GrantedAbilities.Contains(OpeningFlag))
            {
                Plugin.Log.LogInfo("[神的指引] 开启：还没有首胜，播开场对白");
                return;
            }
            mod.GrantedAbilities.Add(OpeningFlag);
            var pages = new List<Page> { Voice(RejoinZh, RejoinEn) };
            var next = Next(won, 0, out _, out _);
            if (next != null) pages.Add(Voice(next.HintZh, next.HintEn));
            else if (mod.GrantedAbilities.Add(ClosingFlag))
                foreach (var line in Closing) pages.Add(new Page { Voice = line.Voice, Text = Pick(line.Zh, line.En) });
            waiting.Enqueue(new Talk { Lines = pages });
            queuedAt = Time.unscaledTime - AfterWin;
            Plugin.Log.LogInfo($"[神的指引] 开启：已打赢 {count} 场，指向 {(next != null ? next.Relic.ToString() : "收束对白")}");
        }

        /// <summary>The settings' guidance tab: the switch, what it does, and how far this save has come.</summary>
        public static void DrawSettings(Rect r, float ui)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            bool on = On;
            var button = new Rect(r.x, r.y, 380f * ui, 44f * ui);
            if (UI.CreativeMenu.Button(button, on ? Pick("神的指引：开", "Guidance: On") : Pick("神的指引：关", "Guidance: Off"), ui, on ? VoiceColor : (Color?)null)) SetOn(!on);
            float y = button.yMax + 14f * ui;
            UI.CreativeMenu.Shadowed(new Rect(r.x, y, r.width, 90f * ui), Pick(
                "开：每场首领战第一次打赢后，「门外之声」评价这场胜利，再指向下一个对手——先是同一阶段还没打过的，然后才是下一阶段；回头补打前面阶段的首领时，它会说起你的越级。字幕在屏幕上方，也会写进聊天框。\n关：它不再说话。",
                "On: after each boss fight's first win, the Voice Beyond the Door speaks of the win and points to the next opponent: first those of the same stage not yet beaten, then the next stage; a boss of an earlier stage beaten afterwards, it remarks on your coming back for it. Its lines show at the top of the screen and in the chat.\nOff: it says nothing."),
                UI.CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.9f));
            y += 100f * ui;
            UI.CreativeMenu.Shadowed(new Rect(r.x, y, r.width, 30f * ui), Pick("只对这个存档生效；开启前的存档默认关闭。", "This save only; saves from before the guidance start with it off."),
                UI.CreativeMenu.text, new Color(0.75f, 0.75f, 0.82f, 0.9f));
            y += 40f * ui;
            var won = Won(mod);
            int count = 0;
            foreach (var e in Route) if (won.Contains(e.Relic)) count++;
            UI.CreativeMenu.Shadowed(new Rect(r.x, y, r.width, 30f * ui), Pick($"这个存档已打赢 {count} / {Route.Length} 场首领战。", $"This save has won {count} of {Route.Length} boss fights."),
                UI.CreativeMenu.text, UI.CreativeMenu.Gold);
        }
    }
}
