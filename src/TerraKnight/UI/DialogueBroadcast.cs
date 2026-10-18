using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Hollow Knight's talk told the way Terraria tells its own news: a conversation put in the plain dialogue box
    /// (DialogueManager/Text: the townsfolk, the lore tablets, the Seer's dream talk; the yes/no box "Text YN" stays the
    /// game's) goes into the chat in the bottom-left a line a second, and the box ends the talk at once by its own Dialogue
    /// Page Control's CONVO CANCEL (End Conversation: the text hidden, CONVO_FINISH to the one talking), so what follows
    /// the talk goes on as usual (a shop, a prompt, a gift) and the player has control back. The box itself stays down
    /// (<see cref="BoxUp"/>): a talk begins straight in the chat, the HUD left up.
    /// </summary>
    [HarmonyPatch]
    internal static class DialogueBroadcast
    {
        public static BepInEx.Configuration.ConfigEntry<bool> Setting;
        public static bool Enabled => Setting == null || Setting.Value;

        /// <summary>The lines' colour in the chat: the dialogue box's pale text.</summary>
        static readonly Color LineColor = new Color(1f, 0.94f, 0.78f);
        const float LineSeconds = 1f;

        static readonly Queue<string> waiting = new Queue<string>();
        static float nextLine;
        static string lastConvo;

        public static int DevTold, DevCancelled;
        public static string DevLastConvo => lastConvo;
        public static int DevWaiting => waiting.Count;

        public static void Bind(BepInEx.Configuration.ConfigFile cfg) =>
            Setting = cfg.Bind("Gameplay", "BroadcastDialogue", true,
                "空洞骑士的对话和碑文按泰拉瑞亚的方式播报：台词每秒一句进左下角聊天框，不用翻页，说话时也能走动；是/否选择照常弹出。\nHollow Knight's talk and lore tablets told the Terraria way: a line a second in the chat at the bottom left, no pages to turn, free to move meanwhile; yes/no prompts show as usual.");

        static readonly Regex pageBreak = new Regex(@"<\s*/?\s*h?page\s*>", RegexOptions.IgnoreCase);
        static readonly Regex lineBreak = new Regex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase);
        static readonly Regex tag = new Regex(@"<[^>]*>");

        /// <summary>The conversation's text (Language.Get, as SetConversation reads it) cut into its pages' lines, tags dropped.</summary>
        public static List<string> Lines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var page in pageBreak.Split(text))
                foreach (var raw in lineBreak.Replace(page, "\n").Split('\n'))
                {
                    string line = tag.Replace(raw, "").Trim();
                    if (line.Length > 0) lines.Add(line);
                }
            return lines;
        }

        public static int DevBoxKept;

        /// <summary>
        /// The plain box raised for a talk (the Dialogue Manager's "Box Open" on BOX UP: the box faded and scaled in,
        /// the HUD sent out, the Orbit Shield down) left down, its talk going into the chat; the yes/no box (BOX UP YN)
        /// comes up as usual; the dream box is also suppressed. Its BOX DOWN at the talk's end finds it down already.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Fsm), nameof(HutongGames.PlayMaker.Fsm.ProcessEvent))]
        static bool BoxUp(HutongGames.PlayMaker.Fsm __instance, HutongGames.PlayMaker.FsmEvent fsmEvent)
        {
            if (!Enabled || fsmEvent == null || !SuppressBox(__instance.Name, fsmEvent.Name)) return true;
            DevBoxKept++;
            return false;
        }

        internal static bool SuppressBox(string fsm, string ev) =>
            (fsm == "Box Open" && ev == "BOX UP") || (fsm == "Box Open Dream" && ev == "BOX UP DREAM");

        static readonly System.Reflection.FieldInfo textMesh = AccessTools.Field(typeof(DialogueBox), "textMesh");

        /// <summary>
        /// A conversation's first page about to show (StartConversation: SetConversation, then ShowPage(1); the two-line
        /// StartConversation itself is inlined into its callers, past a patch): told instead, and that page never shown.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DialogueBox), nameof(DialogueBox.ShowPage))]
        static bool Told(DialogueBox __instance, int pageNum)
        {
            if (!Enabled || pageNum != 1 || __instance == null || __instance.gameObject.name != "Text") return true;
            PlayMakerFSM pages = null;
            foreach (var f in __instance.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Dialogue Page Control") pages = f;
            var mesh = textMesh?.GetValue(__instance);
            string text = mesh != null ? AccessTools.Property(mesh.GetType(), "text")?.GetValue(mesh, null) as string : null;
            if (pages == null || text == null) return true;
            string convo = __instance.currentConversation;
            // The same talk again while its lines are still coming: not told twice.
            if (!(convo == lastConvo && waiting.Count > 0))
                foreach (var line in Lines(text)) waiting.Enqueue(line);
            lastConvo = convo;
            DevTold++;
            HoldInRoom();
            __instance.HideText();
            __instance.StartCoroutine(EndTalk(pages));
            return false;
        }

        // ------------------------------------------------------------------ kept in the room while it is told

        /// <summary>The room the talk began in (world rect of its region, its scene): the player stays in it until the last line.</summary>
        static Rect? holdRect;
        static string holdScene;
        static float nextHoldNotice;
        public static int DevHeldBack, DevGatesRefused;

        /// <summary>Whether the player is kept in the talk's room now: lines still to come, or the last one's second not over.</summary>
        public static bool Holding => holdRect.HasValue && Telling;

        static bool Telling => waiting.Count > 0 || Time.unscaledTime < nextLine;

        static void HoldInRoom()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var cell = World.CellGrid.WorldToCell(hero.transform.position);
            var region = World.TerraWorld.RegionAt(cell.x, cell.y) ?? World.TerrainSystem.Current?.Region;
            if (region == null) return;
            var r = region.CellRect;
            const float k = World.CellGrid.CellSize;
            holdRect = new Rect(r.x * k, r.y * k, r.width * k, r.height * k);
            holdScene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
        }

        /// <summary>
        /// Each frame while lines are still to come: the player stopped at the edge of the room the talk began in (the
        /// seamless world's crossing into a neighbour as much as a gate), with a word about it. A scene changed some other
        /// way (a death, a teleport, a dream's end) lets go.
        /// </summary>
        static void KeepInRoom()
        {
            if (!holdRect.HasValue) return;
            var hero = HeroController.SilentInstance;
            var gm = GameManager.UnsafeInstance;
            if (!Telling || hero == null || gm == null || hero.cState.dead || gm.IsInSceneTransition || gm.sceneName != holdScene)
            {
                holdRect = null;
                return;
            }
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var room = holdRect.Value;
            const float inset = 0.6f;
            Vector3 p = hero.transform.position;
            float dx = 0f, dy = 0f;
            if (box.min.x < room.xMin + inset) dx = room.xMin + inset - box.min.x;
            else if (box.max.x > room.xMax - inset) dx = room.xMax - inset - box.max.x;
            if (box.min.y < room.yMin + inset) dy = room.yMin + inset - box.min.y;
            else if (box.max.y > room.yMax - inset) dy = room.yMax - inset - box.max.y;
            if (dx == 0f && dy == 0f) return;
            hero.transform.position = new Vector3(p.x + dx, p.y + dy, p.z);
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(dx != 0f ? 0f : rb.linearVelocity.x, dy != 0f ? 0f : rb.linearVelocity.y);
            DevHeldBack++;
            NoticeHeld();
        }

        static void NoticeHeld()
        {
            if (Time.unscaledTime < nextHoldNotice) return;
            nextHoldNotice = Time.unscaledTime + 2f;
            CenterNotice.Show(Lang.T("对话还没结束，不能离开这里"));
        }

        /// <summary>A gate of the talk's room walked into while lines are still to come: not taken.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TransitionPoint), "TryDoTransition")]
        static bool GateWhileTold(TransitionPoint __instance, Collider2D heroCollider)
        {
            if (!Holding || heroCollider == null || heroCollider.gameObject.layer != 9) return true;
            DevGatesRefused++;
            NoticeHeld();
            return false;
        }

        /// <summary>A frame on, once the one talking waits for the talk's end: the box's own way out.</summary>
        static IEnumerator EndTalk(PlayMakerFSM pages)
        {
            yield return null;
            if (pages == null) yield break;
            pages.SendEvent("CONVO CANCEL");
            DevCancelled++;
        }

        /// <summary>Each frame: the next line into the chat, a second after the last.</summary>
        public static void Tick()
        {
            KeepInRoom();
            if (waiting.Count == 0 || Time.unscaledTime < nextLine) return;
            Net.NetChat.Add(waiting.Dequeue(), LineColor);
            nextLine = Time.unscaledTime + LineSeconds;
        }

        /// <summary>A save left or another loaded: nothing still to be told.</summary>
        public static void Clear()
        {
            waiting.Clear();
            lastConvo = null;
            holdRect = null;
        }
    }
}
