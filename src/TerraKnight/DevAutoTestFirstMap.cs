using System.Collections;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "firstmap"): Cornifer in Kingdom's Edge sells the first map (his Conversation Control put at "Get F
    /// Map" with no map yet): its prompt is one line in the chat, and "Map Input", which waited for the game's own
    /// buttons, goes on by itself.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FirstMapTest()
        {
            yield return GoTo("Deepnest_East_03", EntryGateOf("Deepnest_East_03"));
            yield return new WaitForSeconds(2f);
            PlayMakerFSM convo = null;
            foreach (var f in Object.FindObjectsOfType<PlayMakerFSM>())
                if (f.FsmName == "Conversation Control" && f.gameObject.name == "Cornifer") convo = f;
            if (convo == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 第一张地图 找不到科尼法 不通过");
                yield break;
            }
            PlayerData.instance.SetBool("hasMap", false);
            int notes = TutorialNotes.DevNotes, advanced = TutorialNotes.DevMapAdvanced;
            convo.SetState("Get F Map");
            string path = convo.ActiveStateName;
            for (float t = 0f; t < 6f; t += 0.1f)
            {
                yield return new WaitForSeconds(0.1f);
                string s = convo.ActiveStateName;
                if (s != null && !path.EndsWith(s)) path += " → " + s;
            }
            bool through = path.Contains("Map Input") && path.Contains("Prompt Down");
            int lines = TutorialNotes.DevNotes - notes;
            bool pass = through && lines == 1 && TutorialNotes.DevMapAdvanced == advanced + 1;
            Plugin.Log.LogInfo($"[AutoTest] 第一张地图 状态 {path}；聊天 {lines} 行，自己继续 {TutorialNotes.DevMapAdvanced - advanced} 次 {(pass ? "通过" : "不通过")}");
        }
    }
}
