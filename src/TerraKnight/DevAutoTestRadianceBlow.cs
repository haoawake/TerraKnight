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
            control.SetState("Final Impact");
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
                if (state != last)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 辐光一击 {state}（可操作 {GameHooks.PlayerHasControl()}）");
                    if (state == "Slash") slashes++;
                    last = state;
                }
                if (state == "Kill Hit" || state == "Tendrils 3" || state == "Yank Down" || state == "Drag") { killed = true; break; }
                if (state == "First Idle" || state == "Knight Idle")
                {
                    waited = true;
                    controlWhileWaiting |= GameHooks.PlayerHasControl();
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
        }
    }
}
