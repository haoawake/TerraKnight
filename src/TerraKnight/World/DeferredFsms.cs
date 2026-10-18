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
    /// events for an FSM without an owner); then each is set up, switched back on if the scene had
    /// it on (its OnEnable), started, and told the level loaded if it listens for that, as Unity
    /// would have done. One the scene keeps switched off (Unity still runs its Awake) stays off:
    /// the stag stations' arrival door has a Door Control left switched off, still set to take the
    /// player to the Black Egg Temple, its trigger right where one stands to talk to the stag.
    /// </summary>
    [HarmonyPatch]
    internal static class DeferredFsms
    {
        /// <summary>The scene whose FSMs are being held, while it loads; null otherwise.</summary>
        public static string HoldScene;
        /// <summary>Time to spend setting held FSMs up each frame.</summary>
        const double BudgetMs = 3.0;

        static readonly Queue<(PlayMakerFSM fsm, bool wasOn)> held = new Queue<(PlayMakerFSM, bool)>();
        static readonly System.Reflection.MethodInfo awake = AccessTools.Method(typeof(PlayMakerFSM), "Awake");
        static bool waking;

        public static int Pending => held.Count;
        /// <summary>The FSMs the scene keeps switched off, kept off this load (for the log).</summary>
        static readonly List<string> keptOff = new List<string>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Awake")]
        static bool Awake(PlayMakerFSM __instance)
        {
            if (waking || HoldScene == null || __instance.gameObject.scene.name != HoldScene) return true;
            held.Enqueue((__instance, __instance.enabled));
            __instance.enabled = false;
            return false;
        }

        /// <summary>Sets held FSMs up, oldest first, until this frame's time is used (or all, with no budget).</summary>
        public static void Pump(bool all = false)
        {
            if (held.Count == 0) return;
            var sw = Stopwatch.StartNew();
            while (held.Count > 0 && (all || sw.Elapsed.TotalMilliseconds < BudgetMs))
            {
                var (fsm, wasOn) = held.Dequeue();
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
                // On as the scene had it (or as something has switched it since).
                fsm.enabled = wasOn || fsm.enabled;
                if (!fsm.enabled) keptOff.Add($"{fsm.name}/{fsm.FsmName}{(SetsRespawn(fsm) ? "（会改复活点）" : "")}");
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
                if (fsm.enabled && fsm.Fsm != null && fsm.Fsm.HandleLevelLoaded) fsm.Fsm.Event(FsmEvent.LevelLoaded);
                double took = sw.Elapsed.TotalMilliseconds - t0;
                if (took > 5.0)
                    Plugin.Log.LogInfo($"[无缝] 状态机 {fsm.gameObject.scene.name}/{fsm.name}/{fsm.FsmName} 设置用了 {took:F1} ms");
                if (held.Count == 0 && keptOff.Count > 0)
                {
                    Plugin.Log.LogInfo($"[无缝] {fsm.gameObject.scene.name} 里原版关着的 {keptOff.Count} 个状态机照旧关着：{string.Join("、", keptOff)}");
                    keptOff.Clear();
                }
            }
        }

        /// <summary>Whether an FSM has an action that moves the respawn point (the bench the player comes back to).</summary>
        static bool SetsRespawn(PlayMakerFSM fsm)
        {
            foreach (var state in fsm.FsmStates)
                foreach (var a in state.Actions)
                {
                    if (a == null) continue;
                    if (a.GetType().Name.IndexOf("Respawn", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    foreach (var f in a.GetType().GetFields())
                        if (f.GetValue(a) is FsmString str && str.Value != null && str.Value.StartsWith("respawn", System.StringComparison.OrdinalIgnoreCase)) return true;
                }
            return false;
        }

        /// <summary>A held FSM: whether its scene had it switched on (null: not held, its own enabled says).</summary>
        public static bool? WasOn(PlayMakerFSM fsm)
        {
            foreach (var (f, wasOn) in held)
                if (f == fsm) return wasOn;
            return null;
        }

        /// <summary>Forget held FSMs (their scene is going).</summary>
        public static void Clear()
        {
            held.Clear();
            keptOff.Clear();
        }

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
