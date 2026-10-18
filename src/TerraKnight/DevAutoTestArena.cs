using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "arena"): the creative book's spawn button on a boss (Items/BossArena). Bosses
    /// marked beaten are fought again in their arenas, their scenes and flags as for a first fight;
    /// leaving puts the marks back. A warrior's ghost gets the Dream Nail for the visit only. A
    /// dream's fight sends the player back to where they pressed the button.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArenaTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            var pd = PlayerData.instance;
            // The book's button, creative: "传送并召唤" on a boss (the longest name), "生成" on any other.
            bool wasCreative = mod.Creative;
            mod.ApplyCreative(true);
            InventoryGui.Open = true;
            foreach (var rec in new[] { "NailBros", "Crawler" })
            {
                InventoryGui.DevBestiaryShow(rec);
                yield return new WaitForSeconds(0.6f);
                Plugin.Log.LogInfo($"[AutoTest] 挑战 图鉴按钮 {Bestiary.NameOf(rec)}：{(BossArena.Has(rec) ? "传送并召唤" : "生成")}，按钮 {InventoryGui.DevSpawnRect}");
                Shot(GameManager.instance.sceneName, "arena_0_button_" + rec);
                yield return null;
            }
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            mod.ApplyCreative(wasCreative);
            // Beaten already, as in a save that has been through them.
            pd.SetBool("falseKnightDefeated", true);
            SceneData.instance.SaveMyState(new PersistentBoolData { sceneName = "Crossroads_10", id = "Battle Scene", activated = true });
            pd.SetBool("hornet1Defeated", true);
            pd.SetInt("noEyesDefeated", 2);
            bool dreamNail = pd.GetBool("hasDreamNail"), lantern = pd.GetBool("hasLantern");

            yield return ArenaVisit("FalseKnight", "Crossroads_10", "FalseKnight", () => $"falseKnightDefeated={pd.GetBool("falseKnightDefeated")}（应为 False）");
            Plugin.Log.LogInfo($"[AutoTest] 挑战 离开假骑士后：falseKnightDefeated={pd.GetBool("falseKnightDefeated")}（应放回 True），战斗场景状态={SceneData.instance.FindMyState(new PersistentBoolData { sceneName = "Crossroads_10", id = "Battle Scene" })?.activated}（应放回 True）");

            yield return ArenaVisit("Hornet", "Fungus1_04", "Hornet", () => $"hornet1Defeated={pd.GetBool("hornet1Defeated")}（应为 False）");
            Plugin.Log.LogInfo($"[AutoTest] 挑战 离开大黄蜂后：hornet1Defeated={pd.GetBool("hornet1Defeated")}（应放回 True）");

            yield return ArenaVisit("GhostNoEyes", "Fungus1_35", "GhostNoEyes", () => $"noEyesDefeated={pd.GetInt("noEyesDefeated")}（应为 0），梦之钉 {pd.GetBool("hasDreamNail")}，灯笼 {pd.GetBool("hasLantern")}");
            Plugin.Log.LogInfo($"[AutoTest] 挑战 离开无眼后：noEyesDefeated={pd.GetInt("noEyesDefeated")}（应放回 2），梦之钉 {pd.GetBool("hasDreamNail")}（应放回 {dreamNail}），灯笼 {pd.GetBool("hasLantern")}（应放回 {lantern}）");

            // A dream's fight won: back to the spot left.
            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            yield return new WaitForSeconds(1.5f);
            Vector2 spot = hero.transform.position;
            yield return ArenaVisit("WhiteDefender", "Dream_04_White_Defender", "WhiteDefender", () => $"梦醒回到 {pd.dreamReturnScene}", leave: false);
            HealthManager wd = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!hm.isDead && UI.EnemyHover.RecordOf(hm) == "WhiteDefender") wd = hm;
            // He sleeps until the player comes near: walked up to.
            if (wd != null)
            {
                var h = HeroController.instance;
                h.transform.position = Clear(h, (Vector2)wd.transform.position + new Vector2(-5f, 0f));
                Physics2D.SyncTransforms();
                var fsm = PlayMakerFSM.FindFsmOnGameObject(wd.gameObject, "Dung Defender");
                float wake = Time.time + 20f;
                while (fsm != null && fsm.ActiveStateName == "Sleep" && Time.time < wake)
                {
                    Player.TerraMovement.DevMove = 1;
                    yield return new WaitForSeconds(0.2f);
                }
                Player.TerraMovement.DevMove = 0;
                Plugin.Log.LogInfo($"[AutoTest] 挑战 白色防御者 醒了：{fsm?.ActiveStateName}");
            }
            float until = Time.time + 60f;
            while (wd != null && !wd.isDead && Time.time < until)
            {
                PlayerData.instance.isInvincible = true;
                if (!wd.IsInvincible && GameHooks.PlayerHasControl())
                {
                    wd.hp = 1;
                    wd.Hit(new HitInstance { Source = HeroController.instance.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 100, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                }
                yield return new WaitForSeconds(0.4f);
            }
            until = Time.time + 40f;
            var gm = GameManager.instance;
            while ((gm.sceneName != "Town" || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 挑战 白色防御者打完：在 {gm.sceneName}（应为 Town），位置 {(Vector2)hero.transform.position:F1}，离按按钮时 {Vector2.Distance(hero.transform.position, spot):F1}（应接近 0）");
            Shot(gm.sceneName, "arena_5_back_from_dream");
            yield return null;

            // Godhome's fight ends with the same dream return a dream's does: from Dirtmouth and back.
            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            yield return new WaitForSeconds(1.5f);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(20f, 0f));
            yield return new WaitForSeconds(1f);
            spot = hero.transform.position;
            yield return ArenaVisit("Nailsage", "GG_Sly", "Nailsage", () => $"梦醒回到 {pd.dreamReturnScene}", leave: false);
            if (BossSceneController.Instance != null) BossSceneController.Instance.DoDreamReturn();
            until = Time.time + 40f;
            while ((gm.sceneName != "Town" || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 挑战 斯莱 梦境返回后：在 {gm.sceneName}（应为 Town），位置 {(Vector2)hero.transform.position:F1}，离按按钮时 {Vector2.Distance(hero.transform.position, spot):F1}（应接近 0），进行中 {BossArena.DevActive}（应为 False）");
            Shot(gm.sceneName, "arena_6_back_from_godhome");
            yield return null;
            mod.DevNoFallDamage = false;
        }

        IEnumerator ArenaVisit(string key, string scene, string record, System.Func<string> during, bool leave = true)
        {
            var gm = GameManager.instance;
            bool started = BossArena.Fight(key);
            float until = Time.time + 40f;
            while ((gm.sceneName != scene || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
            yield return new WaitForSeconds(3f);
            HealthManager boss = null;
            int found = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (UI.EnemyHover.RecordOf(hm) == record) { found++; if (hm.gameObject.activeInHierarchy) boss = hm; }
            var loaded = new System.Text.StringBuilder();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) loaded.Append(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name).Append(' ');
            var fadeFsm = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
            var fsms = new System.Text.StringBuilder();
            if (boss != null) foreach (var f in boss.GetComponents<PlayMakerFSM>()) fsms.Append($" {f.FsmName}:{f.ActiveStateName}");
            Plugin.Log.LogInfo($"[AutoTest] 挑战 {key}：镜头 {fadeFsm?.ActiveStateName ?? "无"}，补淡入 {BossArena.DevFadeKicks} 次，首领状态机{fsms}");
            Plugin.Log.LogInfo($"[AutoTest] 挑战 {key}：开始 {started}，到了 {gm.sceneName}（应为 {scene}），可操作 {GameHooks.PlayerHasControl()}，首领 {found} 个（启用的 {(boss != null ? boss.name : "无")}），{during()}，进行中 {BossArena.DevActive}，载入的场景 {loaded}");
            Shot(gm.sceneName, "arena_" + key);
            yield return null;
            if (!leave) yield break;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 挑战 {key} 离开后：进行中 {BossArena.DevActive}（应为 False）");
        }
    }
}
