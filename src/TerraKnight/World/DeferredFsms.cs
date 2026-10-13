using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace TerraKnight.World
{
    /// <summary>
    /// A neighbouring scene's PlayMaker FSMs are set up over the following frames instead of all in
    /// the frame the scene appears, a few milliseconds' worth a frame. Unity runs every Awake at
    /// once when a scene activates, and FSM set-up (PlayMakerFSM.Awake, then its Start entering
    /// the first state) was nearly all of the hitch a neighbour's background load caused. Held FSMs
    /// stay switched off (no Start or Update) and ignore events until set up (PlayMaker drops
    /// events for an FSM without an owner); then each is set up, switched on (its OnEnable), started,
    /// and told the level loaded if it listens for that, as Unity would have done.
    /// </summary>
    [HarmonyPatch]
    internal static class DeferredFsms
    {
        /// <summary>The scene whose FSMs are being held, while it loads; null otherwise.</summary>
        public static string HoldScene;
        /// <summary>Time to spend setting held FSMs up each frame.</summary>
        const double BudgetMs = 3.0;

        static readonly Queue<PlayMakerFSM> held = new Queue<PlayMakerFSM>();
        static readonly System.Reflection.MethodInfo awake = AccessTools.Method(typeof(PlayMakerFSM), "Awake");
        static bool waking;

        public static int Pending => held.Count;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Awake")]
        static bool Awake(PlayMakerFSM __instance)
        {
            if (waking || HoldScene == null || __instance.gameObject.scene.name != HoldScene) return true;
            __instance.enabled = false;
            held.Enqueue(__instance);
            return false;
        }

        /// <summary>Sets held FSMs up, oldest first, until this frame's time is used (or all, with no budget).</summary>
        public static void Pump(bool all = false)
        {
            if (held.Count == 0) return;
            var sw = Stopwatch.StartNew();
            while (held.Count > 0 && (all || sw.Elapsed.TotalMilliseconds < BudgetMs))
            {
                var fsm = held.Dequeue();
                if (fsm == null) continue;
                double t0 = sw.Elapsed.TotalMilliseconds;
                waking = true;
                try
                {
                    awake.Invoke(fsm, null);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"[无缝] 延后初始化状态机 {fsm.name}/{fsm.FsmName} 出错：{e.InnerException?.Message ?? e.Message}");
                }
                finally
                {
                    waking = false;
                }
                fsm.enabled = true;
                // Started now rather than with every other woken FSM next frame, so it counts against
                // this frame's time (Unity's own Start then finds it started).
                try
                {
                    if (fsm.Fsm != null && !fsm.Fsm.Started && fsm.isActiveAndEnabled) fsm.Fsm.Start();
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"[无缝] 延后启动状态机 {fsm.name}/{fsm.FsmName} 出错：{e.Message}");
                }
                // Missed while held: the load of its own scene.
                if (fsm.Fsm != null && fsm.Fsm.HandleLevelLoaded) fsm.Fsm.Event(FsmEvent.LevelLoaded);
                double took = sw.Elapsed.TotalMilliseconds - t0;
                if (took > 5.0)
                    Plugin.Log.LogInfo($"[无缝] 状态机 {fsm.gameObject.scene.name}/{fsm.name}/{fsm.FsmName} 设置用了 {took:F1} ms");
            }
        }

        /// <summary>Forget held FSMs (their scene is going).</summary>
        public static void Clear() => held.Clear();

        /// <summary>
        /// The "got an item" message an item pickup or chest shows is made up front, once for every
        /// kind of item it could give (CreateUIMsgGetItem.Awake), though only one is ever shown:
        /// some 80 ms for one pickup. The action makes its message on entering when none was made,
        /// so leave it to do that.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.CreateUIMsgGetItem), nameof(FsmStateAction.Awake))]
        static bool NoPremadeItemMessage() => TerraMod.Instance == null;
    }
}
