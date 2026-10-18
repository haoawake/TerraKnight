using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// An area's big title puts every talking thing to sleep while it shows: their "npc_control" machines go Inert on
    /// BIG TITLE START and wake on BIG TITLE END (the City of Tears' Waterways Machine by the manhole among them, which
    /// takes the Simple Key). A room loaded beside the one the player is in (the seamless world) can miss the end and
    /// stay asleep: no prompt, Up does nothing. Once a second, with no title showing, one asleep is woken.
    /// </summary>
    internal static class NpcTitleWake
    {
        static float next;
        public static int DevWoken;

        public static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            var title = AreaTitle.instance;
            if (title != null && title.gameObject.activeInHierarchy) return;
            var all = PlayMakerFSM.FsmList;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.FsmName != "npc_control" || !f.isActiveAndEnabled || f.ActiveStateName != "Inert") continue;
                f.SendEvent("BIG TITLE END");
                DevWoken++;
                Plugin.Log.LogInfo($"[对话] {f.gameObject.scene.name}/{f.name} 在区域标题后没醒，补上 BIG TITLE END → {f.ActiveStateName}");
            }
        }
    }
}
