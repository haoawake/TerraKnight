using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_radiance"): the Radiance's last blow. The fight is entered as the creative challenge does, the
    /// Radiance's "Control" FSM put on its final impact, and once the void-born Knight waits for the game's attack
    /// (First Idle / Knight Idle: ListenForAttack → SLASH) the left mouse button is clicked: each click must slash.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RadianceBlowTest()
        {
            PlayerData.instance.isInvincible = true;
            PlayerData.instance.hasDreamNail = true;
            yield return SealTravel("Town", "left1");
            Bestiary.Kills["FinalBoss"] = 1;
            // The Radiance's seal used as a player uses it (straight into the arena, the challenge taken up).
            var inv = mod.Inventory;
            BossSummons.Seal seal = null;
            foreach (var s in BossSummons.All) if (s.Key == "FinalBoss") seal = s;
            if (seal == null) { FxCheck("辐光最后一击", false, "没有辐光的召唤物"); yield break; }
            int stage = Shop.DevStage;
            Shop.DevStage = seal.Stage;
            mod.Stats.LifeCrystals = TerraKnight.Player.TerraStats.MaxLifeCrystals;
            inv.Slots[0] = ItemStack.Of(seal.Type, 3);
            inv.Selected = 0;
            inv.Override = -1;
            float ready = Time.realtimeSinceStartup + 15f;
            while (!GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready) yield return null;
            mod.DevUseSelected(HeroController.instance.transform.position);
            PlayMakerFSM control = null;
            float until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until && control == null)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                PlayerData.instance.isInvincible = true;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (f != null && f.FsmName == "Control" && f.gameObject.name == "Radiance" && f.transform.parent != null && f.transform.parent.name == "Boss Control") control = f;
                if (control == null && Time.realtimeSinceStartup > until - 50f)
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (f != null && f.gameObject.name.Contains("Radiance")) Plugin.Log.LogInfo($"[AutoTest] 辐光一击 找到 {PathOf(f.transform)} '{f.FsmName}' 场景 {f.gameObject.scene.name} 状态 {f.ActiveStateName} 启用 {f.gameObject.activeInHierarchy}");
            }
            if (control == null) { FxCheck("辐光最后一击", false, $"没进到辐光战（场景 {GameManager.instance.sceneName}）"); yield break; }
            Plugin.Log.LogInfo($"[AutoTest] 辐光一击 进场 {GameManager.instance.sceneName}，状态 {control.ActiveStateName}");
            // The fight under way first (its intro done), then straight to the final impact.
            until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until && control.ActiveStateName != "Arena 1 Idle") { PlayerData.instance.isInvincible = true; yield return null; }
            // Past the seal's own hand-back after the start (BossSummonStart.Free, 20 s), as a real fight's end is.
            for (float t = Time.realtimeSinceStartup + 22f; Time.realtimeSinceStartup < t;) { PlayerData.instance.isInvincible = true; mod.Stats.FullHeal(); yield return null; }
            Plugin.Log.LogInfo($"[AutoTest] 辐光一击 打了一会儿：{control.ActiveStateName}，可操作 {GameHooks.PlayerHasControl()}");
            bool auditFall = suite == "fx_radiancefall";
            if (auditFall)
            {
                var h = HeroController.instance;
                Player.TerraMovement.HkShadowDash(h, 1);
                yield return new WaitForSeconds(0.6f);
                FxCheck("普通冲刺结束仍恢复重力", !Player.TerraMovement.HkDashing && h.GetComponent<Rigidbody2D>().gravityScale > 0.01f, $"gravity={h.GetComponent<Rigidbody2D>().gravityScale:F3}");
            }
            if (auditFall) Player.TerraMovement.HkShadowDash(HeroController.instance, 1);
            control.SetState("Final Impact");
            var samples = new System.Collections.Generic.List<object>();
            float nextSample = 0f;
            float idleSince = -1f;
            float idleY = 0f, maxIdleDrop = 0f;
            float maxIdleGravity = 0f;
            string idleState = null;
            bool endingReached = false;
            int slashes = 0, clicks = 0, presses0 = Player.CutsceneAttack.DevPresses;
            bool waited = false, controlWhileWaiting = false, killed = false;
            string last = "";
            until = Time.realtimeSinceStartup + 90f;
            float nextShot = 0f;
            int shot = 0;
            while (Time.realtimeSinceStartup < until && control != null)
            {
                PlayerData.instance.isInvincible = true;
                string state = control.ActiveStateName;
                if (state == "Ending Scene") endingReached = true;
                var currentHero = HeroController.instance;
                var body = currentHero.GetComponent<Rigidbody2D>();
                if (auditFall && Time.realtimeSinceStartup >= nextSample)
                {
                    nextSample = Time.realtimeSinceStartup + 0.05f;
                    samples.Add(new { t = Time.realtimeSinceStartup, state, scene = GameManager.instance.sceneName,
                        y = currentHero.transform.position.y, vy = body.linearVelocity.y, gravity = body.gravityScale,
                        held = currentHero.controlReqlinquished, input = currentHero.acceptingInput, hidden = Player.CutsceneAttack.KnightHidden(), dash = Player.TerraMovement.HkDashing,
                        border = World.WorldBorder.DevStops });
                }
                if (state != last)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 辐光一击 {state}（可操作 {GameHooks.PlayerHasControl()}，y={currentHero.transform.position.y:F2} vy={body.linearVelocity.y:F2} gravity={body.gravityScale:F2} hidden={Player.CutsceneAttack.KnightHidden()}）");
                    if (state == "Slash") slashes++;
                    last = state;
                }
                if (state == "Kill Hit" || state == "Tendrils 3" || state == "Yank Down" || state == "Drag") { killed = true; if (!auditFall) break; }
                if (state == "First Idle" || state == "Knight Idle")
                {
                    waited = true;
                    controlWhileWaiting |= GameHooks.PlayerHasControl();
                    if (auditFall)
                    {
                        if (idleState != state) { idleState = state; idleSince = Time.realtimeSinceStartup; idleY = currentHero.transform.position.y; }
                        maxIdleDrop = Mathf.Max(maxIdleDrop, idleY - currentHero.transform.position.y);
                        maxIdleGravity = Mathf.Max(maxIdleGravity, Mathf.Abs(body.gravityScale));
                        if (Time.realtimeSinceStartup - idleSince < 2f) { yield return null; continue; }
                    }
                    clicks++;
                    yield return ClickLeft();
                    continue;
                }
                if (Time.realtimeSinceStartup > nextShot && shot < 8) { nextShot = Time.realtimeSinceStartup + 3f; Shot(GameManager.instance.sceneName, $"fx_radiance_{shot++:D2}"); }
                yield return null;
            }
            // Set Anim, Antic and Slash go by within a click's wait: the kill (reached only through a slash) is what shows it.
            FxCheck("辐光最后一击：左键当原版攻击", waited && killed && Player.CutsceneAttack.DevPresses > presses0,
                $"等攻击 {waited}（那时可操作 {controlWhileWaiting}），左键 {clicks} 下（当作攻击 {Player.CutsceneAttack.DevPresses - presses0} 次），看到挥砍 {slashes} 次，击杀 {killed}，最后状态 {(control != null ? control.ActiveStateName : "没了")}");
            if (auditFall)
            {
                FxCheck("辐光动画等待时不被强制下落", waited && maxIdleDrop < 0.2f, $"最大下落 {maxIdleDrop:F3}");
                FxCheck("辐光动画等待时保持原版无重力", waited && maxIdleGravity < 0.01f, $"最大重力 {maxIdleGravity:F3}");
                FxCheck("辐光终结动画完整进入结局转场", endingReached && GameManager.instance.sceneName.StartsWith("Cinematic_Ending"), GameManager.instance.sceneName);
                System.IO.File.WriteAllText(System.IO.Path.Combine(DevCopy.SaveDir, "radiance-fall.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { waited, killed, endingReached, finalScene = GameManager.instance.sceneName, maxIdleDrop, maxIdleGravity, samples }, Newtonsoft.Json.Formatting.Indented));
            }
        }
    }
}
