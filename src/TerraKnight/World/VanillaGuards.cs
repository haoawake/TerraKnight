using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A vanilla FSM action that keeps a component from its state's start and sets it every frame
    /// after it is gone: SetParticleEmissionSpeed (everyFrame) whose particle system was destroyed
    /// before its FSM (a room let go mid-effect). Nothing is left to set: the frame's set is skipped
    /// instead of an exception each time.
    /// </summary>
    [HarmonyPatch]
    internal static class VanillaGuards
    {
        static readonly AccessTools.FieldRef<SetParticleEmissionSpeed, ParticleSystem> emitter =
            AccessTools.FieldRefAccess<SetParticleEmissionSpeed, ParticleSystem>("emitter");
        static readonly HashSet<string> told = new HashSet<string>();
        public static int DevSkipped;

        /// <summary>Dev diagnosis: a scene's SceneManager start that throws, with what it found missing.</summary>
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(SceneManager), "Start")]
        static System.Exception SceneStartFailed(SceneManager __instance, System.Exception __exception)
        {
            if (__exception == null || told.Count >= 20) return __exception;
            var gc = GameCameras.instance;
            string who = $"{(__instance != null ? __instance.gameObject.scene.name : "?")}：GameManager {GameManager.UnsafeInstance != null} 镜头 {gc != null} 调色 {(gc != null && gc.colorCorrectionCurves != null)} 场景色 {(gc != null && gc.sceneColorManager != null)} PlayerData {PlayerData.instance != null} 主角 {HeroController.SilentInstance != null}";
            if (told.Add("start " + who)) Plugin.Log.LogInfo($"[原版] SceneManager.Start 出错 {__exception.GetType().Name}，{who}");
            return __exception;
        }

        /// <summary>Dev: every BG event a battle gate takes, with who sent it (the FSM and state running, or the calling code).</summary>
        public static bool DevGateTrace;
        /// <summary>Dev: the FSMs that shut a battle gate while traced.</summary>
        public static readonly List<PlayMakerFSM> DevGateClosers = new List<PlayMakerFSM>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Fsm), nameof(HutongGames.PlayMaker.Fsm.ProcessEvent))]
        static void GateEvent(HutongGames.PlayMaker.Fsm __instance, HutongGames.PlayMaker.FsmEvent fsmEvent)
        {
            if (!DevGateTrace || fsmEvent == null || __instance.Name != "BG Control" || fsmEvent.Name == null || !fsmEvent.Name.StartsWith("BG ")) return;
            var from = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
            string who = from != null && from != __instance
                ? $"{(from.GameObject != null ? from.GameObject.scene.name + "/" + from.GameObject.name : "?")} 的 {from.Name}:{from.ActiveStateName}"
                : "代码：" + string.Join(" <- ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(new System.Diagnostics.StackTrace(2, false).GetFrames() ?? new System.Diagnostics.StackFrame[0], 4), f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name));
            Plugin.Log.LogInfo($"[铁门] {Time.time:F1} {__instance.GameObject?.name} 收到 {fsmEvent.Name}（在 {__instance.ActiveStateName}），来自 {who}");
            if (fsmEvent.Name.Contains("CLOSE") && from != null && from != __instance && from.Owner is PlayMakerFSM pf && !DevGateClosers.Contains(pf)) DevGateClosers.Add(pf);
        }

        static readonly AccessTools.FieldRef<HealthManager, GameObject> battleOf = AccessTools.FieldRefAccess<HealthManager, GameObject>("battleScene");
        static readonly AccessTools.FieldRef<HealthManager, bool> battleNotified = AccessTools.FieldRefAccess<HealthManager, bool>("notifiedBattleScene");

        /// <summary>Dev (with the gate trace): an enemy given to a battle.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.SetBattleScene))]
        static void BattleGiven(HealthManager __instance, GameObject newBattleScene)
        {
            if (DevGateTrace) Plugin.Log.LogInfo($"[战斗] {Time.time:F1} {__instance.gameObject.scene.name}/{__instance.name} 分给了 {(newBattleScene != null ? newBattleScene.name : "无")}");
        }

        /// <summary>Dev (with the gate trace): an enemy's death through the game's Die, and what its battle has left to wait for.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), "Die")]
        static void Died(HealthManager __instance)
        {
            if (!DevGateTrace) return;
            var b = battleOf(__instance);
            var f = b != null ? FSMUtility.LocateFSM(b, "Battle Control") : null;
            var left = f != null ? f.FsmVariables.GetFsmInt("Battle Enemies") : null;
            Plugin.Log.LogInfo($"[战斗] {Time.time:F1} {__instance.gameObject.scene.name}/{__instance.name} 死了（isDead {__instance.isDead}）：战斗 {(b != null ? b.name : "无")}，报数 {battleNotified(__instance)}，还剩 {(left != null ? left.Value : -99)}");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SetParticleEmissionSpeed), "DoSetEmitSpeed")]
        static bool EmissionSpeed(SetParticleEmissionSpeed __instance)
        {
            if (emitter(__instance) != null) return true;
            DevSkipped++;
            var fsm = __instance.Fsm;
            string who = fsm != null ? $"{(fsm.GameObject != null ? fsm.GameObject.scene.name + "/" + fsm.GameObject.name : "?")} 的 {fsm.Name}:{__instance.State?.Name}" : "?";
            if (told.Count < 20 && told.Add(who)) Plugin.Log.LogInfo($"[原版] {who} 要设的粒子已经没了：跳过");
            return false;
        }

        public static int DevDiggerKept;

        /// <summary>
        /// Deepnest's Dirtcarver (BabyCentipede) sets its health back to what it started with each time it burrows (its
        /// Centipede FSM's Dig state: SetHP to Init Health), a full heal mid-fight with Terraria's life. It keeps what it
        /// has left; a set that lowers it still goes through.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SetHP), nameof(SetHP.OnEnter))]
        static bool KeepDiggerHealth(SetHP __instance)
        {
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != "Centipede" || __instance.hp == null || __instance.hp.IsNone) return true;
            var go = fsm.GetOwnerDefaultTarget(__instance.target);
            var hm = go != null ? go.GetComponent<HealthManager>() : null;
            if (hm == null || hm.isDead || hm.hp <= 0 || __instance.hp.Value <= hm.hp) return true;
            DevDiggerKept++;
            __instance.Finish();
            return false;
        }
    }

    /// <summary>
    /// A bench's "Resting" state gets the Knight up on jump, up, down, left or right through its
    /// ListenFor actions, which read the input handler they found on entering the state and only
    /// while the game isn't paused. Now and then (after a save loaded from the menu) a sitting Knight
    /// didn't get up for any key and sat for good. A key of those pressed in play with the bench
    /// still resting a moment later sends the bench its own get-up event; its "Game Paused?" state
    /// still turns it back when the game is paused or the inventory open.
    /// </summary>
    internal static class BenchGetUp
    {
        static PlayMakerFSM bench;
        static float nextFind, pressedAt = -1f;
        static float detachedSince = -1f;
        static string pending, key;
        static int told;
        public static int DevNudged;

        public static void Tick(HeroController hero)
        {
            var pd = PlayerData.instance;
            var gm = GameManager.UnsafeInstance;
            if (hero == null || pd == null || gm == null || !pd.atBench || gm.isPaused || gm.gameState != GlobalEnums.GameState.PLAYING
                || gm.IsInSceneTransition || Time.timeScale == 0f || Items.InventoryGui.Open)
            {
                pressedAt = -1f;
                detachedSince = -1f;
                return;
            }
            bool farFromCachedBench = bench != null && ((Vector2)bench.transform.position - (Vector2)hero.transform.position).sqrMagnitude > 4f;
            if ((bench == null || !bench.gameObject.activeInHierarchy || bench.ActiveStateName != "Resting" || farFromCachedBench)
                && (Time.unscaledTime >= nextFind || farFromCachedBench))
            {
                nextFind = Time.unscaledTime + 0.5f;
                bench = null;
                float nearest = float.MaxValue;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (f.FsmName == "Bench Control" && f.ActiveStateName == "Resting")
                    {
                        float distance = ((Vector2)f.transform.position - (Vector2)hero.transform.position).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; bench = f; }
                    }
            }
            // The native bench can remain in another scene, or no longer be Resting, after
            // dream/death return while TerraMovement already lets the player walk away.
            bool offBench = bench == null || !bench.gameObject.activeInHierarchy || bench.ActiveStateName != "Resting"
                || ((Vector2)bench.transform.position - (Vector2)hero.transform.position).sqrMagnitude > 4f;
            if (offBench && !hero.cState.dead && !hero.cState.transitioning && GameHooks.PlayerHasControl())
            {
                if (detachedSince < 0f) detachedSince = Time.unscaledTime;
                if (Time.unscaledTime - detachedSince >= 0.3f)
                {
                    if (bench != null && bench.ActiveStateName == "Resting") bench.SendEvent("GET UP");
                    pd.atBench = false;
                    pressedAt = detachedSince = -1f;
                    Plugin.Log.LogInfo("[长椅] 已离开长椅：清除残留的坐姿标记");
                    return;
                }
            }
            else detachedSince = -1f;
            if (bench == null || bench.ActiveStateName != "Resting") { pressedAt = -1f; return; }

            if (pressedAt < 0f)
            {
                var a = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
                if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.Jump) || a != null && a.jump.WasPressed) { pending = "GET UP"; key = "跳跃"; }
                else if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.Up) || a != null && a.up.WasPressed) { pending = "GET UP"; key = "上"; }
                else if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.Down) || a != null && a.down.WasPressed) { pending = "GET UP"; key = "下"; }
                else if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.Left) || a != null && a.left.WasPressed) { pending = "GET LEFT"; key = "左"; }
                else if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.Right) || a != null && a.right.WasPressed) { pending = "GET RIGHT"; key = "右"; }
                else return;
                pressedAt = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - pressedAt < 0.3f) return;
            pressedAt = -1f;
            DevNudged++;
            if (told++ < 10) Plugin.Log.LogInfo($"[长椅] 按了{key}，{bench.gameObject.scene.name}/{bench.name} 的长椅没有起身（{Why(gm)}）：替它起身");
            bench.SendEvent(pending);
        }

        /// <summary>What the bench's own listening found: its input handler and the action set it reads.</summary>
        static string Why(GameManager gm)
        {
            var ih = GameHooks.Input;
            var a = ih != null ? ih.inputActions : null;
            string mine = "没找到";
            var st = System.Array.Find(bench.FsmStates, s => s.Name == "Resting");
            var listen = st != null ? System.Array.Find(st.Actions, x => x is ListenForJump) as ListenForJump : null;
            if (listen != null)
            {
                var its = Traverse.Create(listen).Field("inputHandler").GetValue<InputHandler>();
                var itsGm = Traverse.Create(listen).Field("gm").GetValue<GameManager>();
                mine = its == null ? "空" : its == ih ? "同一个" : "另一个（" + (its.inputActions == null ? "无动作集" : $"启用 {its.inputActions.Enabled}/{its.inputActions.jump.Enabled}") + "）";
                mine += itsGm == gm ? "，GameManager 同一个" : itsGm == null ? "，GameManager 已销毁" : "，GameManager 另一个";
            }
            return $"长椅读的输入 {mine}；当前动作集 {(a == null ? "无" : $"启用 {a.Enabled}，跳跃 {a.jump.Enabled}")}";
        }
    }
}
