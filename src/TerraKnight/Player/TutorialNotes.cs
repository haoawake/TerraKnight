using System.Collections;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Player
{
    /// <summary>
    /// The game's tutorials name its own keys (C to dash, Tab for the map, I for the inventory), most of them not the
    /// mod's, and point at its own inventory, which the Terraria one stands in for here (the user's): none of them is
    /// shown, and a line in the chat in the bottom-left corner says it instead, Terraria's way, with the keys as bound now
    /// (the Dash key's, or where to bind it when it has none). They are the HUD's Control Reminder (its Display FSM:
    /// REMINDER JUMP, ATTACK, DASH… sent to it), the first map's prompt (First Map's Control: FIRST MAP UP), the first
    /// charm's message (Charm Tutorial Msg) and the item messages of the journal and the abilities (ObsoletePrompts).
    /// The first charm's pickup and Cornifer's first map go on by themselves after their messages (they waited for one
    /// of the game's own buttons), and a
    /// bench's sitting shows neither the map being updated (the hero unrolling it, its note) nor the first charm's
    /// equip message: the map is the mod's, and charms aren't worn from the game's inventory here.
    /// </summary>
    [HarmonyPatch]
    internal static class TutorialNotes
    {
        /// <summary>Terraria's yellow of a status line (Main.NewText's announcements).</summary>
        static readonly Color Yellow = new Color(1f, 0.94f, 0.08f);

        public static int DevSwallowed, DevNotes;
        public static string DevLast;

        static string K(TerraKeys.Action a) => TerraKeys.KeyName(TerraKeys.Get(a));

        static bool Bound(TerraKeys.Action a) => TerraKeys.Get(a) != Key.None;

        /// <summary>The dash, by the Dash key bound now; unbound, where to bind it. The double tap when it is on.</summary>
        static string DashLine(string what)
        {
            string tap = TerraKeys.DoubleTapDash ? Lang.T("（也可以双击方向键）") : "";
            return Bound(TerraKeys.Action.Dash)
                ? Lang.F("{0}：按 {1} 冲刺", what, K(TerraKeys.Action.Dash)) + tap
                : Lang.F("{0}：冲刺键还没有绑定，在“泰拉瑞亚设置”的按键里绑定冲刺", what) + tap;
        }

        /// <summary>What a tutorial says here, by its name (a Control Reminder's event, an item message's item); null: nothing.</summary>
        public static string For(string what)
        {
            switch (what)
            {
                case "REMINDER JUMP": return Lang.F("按 {0} 跳跃，按住跳得更高", K(TerraKeys.Action.Jump));
                case "REMINDER ATTACK": return Lang.F("按 {0} 使用手里的武器", K(TerraKeys.Action.UseItem));
                case "REMINDER FOCUS": return Lang.F("按 {0} 快速使用治疗药水", K(TerraKeys.Action.QuickHeal));
                case "REMINDER INTERACT": return Lang.F("走到它面前按 {0} 交互", K(TerraKeys.Action.Up));
                case "REMINDER LOOK DOWN": return Lang.F("按住 {0} 往下看", K(TerraKeys.Action.Down));
                case "REMINDER DASH":
                case "Dash": return DashLine(Lang.T("冲刺（装备克苏鲁护盾后）"));
                case "REMINDER SUPERDASH":
                case "Super Dash":
                    if (Items.AbilityAccessories.SuperKeyApart) return Lang.F("装备水晶之心后，按住 {0} 蓄力，松开超级冲刺", K(TerraKeys.Action.SuperDash));
                    return Bound(TerraKeys.Action.Dash)
                        ? Lang.F("装备水晶之心后，长按 {0} 蓄力超级冲刺", K(TerraKeys.Action.Dash))
                        : Lang.T("装备水晶之心后，长按冲刺键蓄力超级冲刺；冲刺键还没有绑定，在“泰拉瑞亚设置”的按键里绑定冲刺");
                case "Shadow Dash": return Lang.F("装备暗影披风后，按 {0} 暗影冲刺，穿过敌人和攻击", K(TerraKeys.Action.ShadowDash))
                    + (TerraKeys.DoubleTapDash ? Lang.T("（也可以双击方向键）") : "");
                case "REMINDER DREAM NAIL":
                case "Dream Nail": return Lang.T("把梦之钉拿在手上，按住左键或右键蓄力，松开挥出");
                case "NA GSlash": return Lang.F("学会了强力斩：拿着骨钉按住 {0} 蓄力，蓄满后松开（不按上下）", K(TerraKeys.Action.UseItem));
                case "NA Cyclone": return Lang.F("学会了旋风斩：拿着骨钉按住 {0} 蓄力，蓄满后按住上或下松开，转圈时连按还能转得更久", K(TerraKeys.Action.UseItem));
                case "NA Dash": return Lang.F("学会了冲刺斩：拿着骨钉按住 {0} 蓄力，蓄满后冲刺时松开", K(TerraKeys.Action.UseItem));
                case "FIRST MAP UP": return Lang.F("获得了地图：按 {0} 打开完整地图", K(TerraKeys.Action.FullMap));
                case "Journal": return Lang.T("获得了猎人日志：打败过的敌人可以在背包里的“怪物图鉴”查看");
                case "CHARM": return Items.Charms.Enabled
                    ? Lang.T("获得了护符：它放进了护符背包（打开背包，点装备栏上方的“护符背包”按钮；坐在长椅上时才能装备）")
                    : Lang.T("获得了护符：它放进了护符背包");
                default: return null;
            }
        }

        /// <summary>A line in the chat for a tutorial, if it has one.</summary>
        public static void Note(string what)
        {
            string line = For(what);
            if (line == null) return;
            DevNotes++;
            DevLast = line;
            Net.NetChat.Add(line, Yellow);
        }

        /// <summary>
        /// The Control Reminder's REMINDER … and the first map's FIRST MAP UP never reach them: each FSM stays as it was
        /// (Idle, Init), so nothing shows and nothing waits on it; the line goes to the chat instead.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Fsm), nameof(Fsm.ProcessEvent))]
        static bool Swallow(Fsm __instance, FsmEvent fsmEvent)
        {
            if (TerraMod.Instance == null || fsmEvent == null) return true;
            string n = fsmEvent.Name;
            if (n == null || n.Length < 12 || (n[0] != 'R' && n[0] != 'F')) return true;
            bool reminder = n.StartsWith("REMINDER ") && __instance.Name == "Display" && __instance.GameObjectName == "Control Reminder";
            // The first map's prompt, whatever holds it (only it listens for that event).
            bool map = n == "FIRST MAP UP";
            if (!reminder && !map) return true;
            // A map seller broadcasts it to every FSM there is: one line for it.
            if (map && Time.frameCount == mapNoteFrame) return false;
            if (map) mapNoteFrame = Time.frameCount;
            DevSwallowed++;
            Plugin.Log.LogInfo($"[提示] 原版教程 {n} 改成聊天里的一行");
            Note(n);
            return false;
        }

        static int mapNoteFrame = -1;

        public static int DevMapAdvanced;

        /// <summary>
        /// A map seller's first map (Cornifer's Conversation Control): after its prompt, not shown here, it waited in "Map
        /// Input" for a jump, attack, cast or quick map press read through the game's own input, which may never come; it
        /// goes on by itself.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        static void WatchMapSeller(PlayMakerFSM __instance)
        {
            if (TerraMod.Instance == null || __instance == null || __instance.FsmName != "Conversation Control" || __instance.Fsm.GetState("Map Input") == null) return;
            TerraMod.Instance.StartCoroutine(MapSeller(__instance));
        }

        static IEnumerator MapSeller(PlayMakerFSM f)
        {
            float since = -1f;
            while (f != null)
            {
                yield return quarter;
                if (f == null) yield break;
                if (!f.isActiveAndEnabled || f.ActiveStateName != "Map Input") { since = -1f; continue; }
                if (since < 0f) { since = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < 0.5f) continue;
                since = -1f;
                DevMapAdvanced++;
                Plugin.Log.LogInfo($"[提示] 第一张地图（{f.gameObject.name}）在 Map Input 等原版按键，自己继续");
                f.SendEvent("CONVO END");
            }
        }

        /// <summary>The first charm's message (its FSM Charm Tute Msg): hidden as it starts, ended its own way, and told in the chat.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        static void CharmMessage(PlayMakerFSM __instance)
        {
            if (TerraMod.Instance == null || __instance == null || __instance.FsmName != "Charm Tute Msg") return;
            foreach (var r in __instance.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            DevSwallowed++;
            Plugin.Log.LogInfo("[提示] 原版的护符教程改成聊天里的一行");
            Note("CHARM");
            TerraMod.Instance.StartCoroutine(EndCharm(__instance));
        }

        /// <summary>A frame later, past its Init (its texts set): down, giving control back if it took it, and gone.</summary>
        static IEnumerator EndCharm(PlayMakerFSM msg)
        {
            yield return null;
            if (msg == null) yield break;
            foreach (var r in msg.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            msg.SetState("Down");
        }

        public static int DevCharmAdvanced, DevBenchQuiet;

        /// <summary>
        /// Every pickup (Shiny Control) watched while it lives: a charm's goes on by itself where it would wait. The first
        /// charm's held the hero kneeling in "Tute" for 4.5 s behind a message that isn't shown here, then in "Wait for
        /// Input" for a jump, attack, cast or inventory press read through the game's own input, which may never come; and
        /// "Hero Up" waits for the hero's stand-up clip to finish, which a cutscene pose may keep from ending.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        static void WatchPickup(PlayMakerFSM __instance)
        {
            if (TerraMod.Instance == null || __instance == null || __instance.FsmName != "Shiny Control") return;
            TerraMod.Instance.StartCoroutine(Pickup(__instance));
        }

        static readonly WaitForSecondsRealtime quarter = new WaitForSecondsRealtime(0.25f);

        static IEnumerator Pickup(PlayMakerFSM f)
        {
            string last = null;
            float since = 0f;
            while (f != null)
            {
                yield return quarter;
                if (f == null || !f.isActiveAndEnabled) { if (f == null) yield break; continue; }
                string state = f.ActiveStateName;
                if (state != last) { last = state; since = Time.unscaledTime; continue; }
                float held = Time.unscaledTime - since;
                if (state == "Finish" || state == "Destroy") yield break;
                if (state == "Tute" && held >= 1.25f) f.SetState("Hero Up");
                else if (state == "Wait for Input" && held >= 0.5f) f.SendEvent("BUTTON DOWN");
                else if (state == "Hero Up" && held >= 3f) f.SetState("Finish");
                else continue;
                DevCharmAdvanced++;
                Plugin.Log.LogInfo($"[提示] 拾取（{f.gameObject.name}）在 {state} 停了 {held:F1} 秒，自己继续");
            }
        }

        /// <summary>
        /// A bench (Bench Control): sitting down shows no map update (the hero unrolling the map, its "map updated" note)
        /// and no first charm equip message (Charm Prompt? skips it once the charm bench message counts as seen). The map
        /// is still updated as the game does, without the show.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        static void BenchQuiet(PlayMakerFSM __instance)
        {
            if (TerraMod.Instance == null || __instance == null || __instance.FsmName != "Bench Control") return;
            int off = 0;
            foreach (var name in new[] { "Update Map", "Update Map Silently" })
            {
                var st = __instance.Fsm.GetState(name);
                if (st == null) continue;
                foreach (var a in st.Actions)
                {
                    bool show = a is HutongGames.PlayMaker.Actions.CreateObject
                        || a is HutongGames.PlayMaker.Actions.Tk2dPlayAnimationWithEvents anim && anim.clipName != null && anim.clipName.Value == "Map Update";
                    if (!show || !a.Enabled) continue;
                    a.Enabled = false;
                    off++;
                }
            }
            var pd = PlayerData.instance;
            if (pd != null && !pd.GetBool("charmBenchMsg")) pd.SetBool("charmBenchMsg", true);
            if (off > 0) DevBenchQuiet++;
        }
    }
}
