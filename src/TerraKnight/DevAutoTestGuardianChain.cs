using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "guardianchain"): a save that has beaten both the Crystal Guardian and the Enraged Guardian rematches
    /// the Crystal Guardian (BossArena.Fight), beats it, and walks up into Mines_32: the room is entered again and the
    /// Enraged Guardian is there, its Battle Scene waiting for the fight.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GuardianChainTest()
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 暴怒守卫重打需要隔离存档，不通过"); yield break; }
            var pd = PlayerData.instance;
            Player.TerraStats.DevImmortal = true;
            pd.isInvincible = true;
            pd.SetBool("killedMegaBeamMiner", true);
            pd.SetBool("defeatedMegaBeamMiner", true);
            pd.SetInt("killsMegaBeamMiner", 0);
            foreach (var id in new[] { "Battle Scene", "Zombie Beam Miner Rematch" })
                SceneData.instance.SaveMyState(new PersistentBoolData { sceneName = "Mines_32", id = id, activated = true, semiPersistent = false });
            Bestiary.Kills["MegaBeamMiner"] = 2;
            int wakes = BossArena.DevGuardianWakes, reloads = BossArena.DevGuardianReloads;

            BossArena.Fight("MegaBeamMiner");
            float until = Time.realtimeSinceStartup + 30f;
            while (!BossArena.Arrived("MegaBeamMiner") && Time.realtimeSinceStartup < until) yield return null;
            if (!BossArena.Arrived("MegaBeamMiner")) { Plugin.Log.LogInfo("[AutoTest] 暴怒守卫重打 没进水晶守卫的房间 不通过"); yield break; }
            yield return new WaitForSeconds(3f);
            until = Time.realtimeSinceStartup + 15f;
            while ((World.LiveWorld.Instance == null || !World.LiveWorld.Instance.IsLoaded("Mines_32")) && Time.realtimeSinceStartup < until) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 暴怒守卫重打 Mines_32 在旁边载入：{World.LiveWorld.Instance != null && World.LiveWorld.Instance.IsLoaded("Mines_32")}");

            HealthManager guardian = null;
            foreach (var hm in Object.FindObjectsOfType<HealthManager>())
                if (hm != null && !hm.isDead && hm.gameObject.scene.name.StartsWith("Mines_18") && hm.name.StartsWith("Mega Zombie Beam Miner")) guardian = hm;
            if (guardian == null) { Plugin.Log.LogInfo("[AutoTest] 暴怒守卫重打 找不到水晶守卫 不通过"); yield break; }
            guardian.Die(null, AttackTypes.Generic, true);
            until = Time.realtimeSinceStartup + 10f;
            while (Bestiary.KillsOf("MegaBeamMiner") <= 2 && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 暴怒守卫重打 打倒水晶守卫：图鉴击杀 {Bestiary.KillsOf("MegaBeamMiner")}，defeatedMegaBeamMiner {pd.GetBool("defeatedMegaBeamMiner")}");

            // Up through Mines_18's top into the room above.
            TransitionPoint top = null;
            foreach (var tp in Object.FindObjectsOfType<TransitionPoint>())
                if (tp.name == "top1" && tp.gameObject.scene.name.StartsWith("Mines_18")) top = tp;
            var hero = HeroController.instance;
            if (top == null || hero == null) { Plugin.Log.LogInfo("[AutoTest] 暴怒守卫重打 找不到 Mines_18 的 top1 不通过"); yield break; }
            until = Time.realtimeSinceStartup + 25f;
            Vector3 up = top.transform.position + Vector3.up * 5f;
            while (Time.realtimeSinceStartup < until)
            {
                var gm = GameManager.instance;
                if (gm.sceneName == "Mines_32" && !gm.IsInSceneTransition && World.TerraWorld.Live?.SceneName == "Mines_32") break;
                if (!gm.IsInSceneTransition && World.TerraWorld.Live?.SceneName != "Mines_32")
                {
                    hero.transform.position = up;
                    var rb = hero.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.velocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                }
                yield return null;
            }
            yield return new WaitForSeconds(3f);

            GameObject enraged = null;
            PlayMakerFSM battle = null;
            foreach (var hm in Object.FindObjectsOfType<HealthManager>())
                if (hm != null && hm.gameObject.scene.name == "Mines_32" && hm.name.StartsWith("Zombie Beam Miner Rematch")) enraged = hm.gameObject;
            foreach (var f in Object.FindObjectsOfType<PlayMakerFSM>())
                if (f.gameObject.scene.name == "Mines_32" && f.FsmName == "Battle Scene") battle = f;
            string waiting = battle?.ActiveStateName ?? "无";
            // Walked up to him: he wakes and the fight is on.
            if (enraged != null && enraged.activeInHierarchy)
            {
                hero.transform.position = enraged.transform.position + Vector3.left * 5f;
                Physics2D.SyncTransforms();
                until = Time.realtimeSinceStartup + 8f;
                PlayMakerFSM miner = null;
                foreach (var f in enraged.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Beam Miner") miner = f;
                string trail = miner?.ActiveStateName ?? "无";
                while (Time.realtimeSinceStartup < until)
                {
                    string st = miner?.ActiveStateName;
                    if (st != null && !trail.EndsWith(st) && trail.Length < 300) trail += " → " + st;
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 暴怒守卫重打 走近后：暴怒守卫 {trail}；战斗场景 {battle?.ActiveStateName ?? "无"}");
            }
            Shot("Mines_32", "guardian_chain");
            // Entered again only when the room was loaded beside the arena before the win.
            bool pass = BossArena.DevGuardianWakes == wakes + 1 && BossArena.DevGuardianReloads <= reloads + 1 && enraged != null && enraged.activeInHierarchy
                && battle != null && waiting != "Activate" && pd.GetInt("killsMegaBeamMiner") >= 1;
            Plugin.Log.LogInfo($"[AutoTest] 暴怒守卫重打 场景 {GameManager.instance.sceneName}，醒来 {BossArena.DevGuardianWakes - wakes}，重新进入 {BossArena.DevGuardianReloads - reloads}，" +
                $"暴怒守卫 {(enraged != null ? enraged.activeInHierarchy.ToString() : "无")}，战斗场景 {waiting}，killsMegaBeamMiner {pd.GetInt("killsMegaBeamMiner")} {(pass ? "通过" : "不通过")}");
        }
    }
}
