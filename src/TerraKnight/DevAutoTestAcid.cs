using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "acid"): Hallownest's acid and Terraria's liquid gear. The rooms with acid pools found (each loaded on
    /// its own), the first one entered: without gear the acid hurts and can't be swum; the Flipper potion drunk there
    /// swims in it at once (the box harmless, the swim region on); the Water Walking potion stands on its surface, and
    /// down held goes in; both gone, the acid hurts again.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AcidTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 酸液 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            // The rooms with acid, and a side gate into each.
            var found = new List<(string Scene, string Gate)>();
            foreach (var prefix in new[] { "Fungus1_", "Fungus3_", "Waterways_", "Crossroads_", "Fungus2_" })
                for (int i = 0; i < USM.sceneCountInBuildSettings && found.Count < 12; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                    if (!name.StartsWith(prefix) || name == GameManager.instance.sceneName) continue;
                    var op = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (op != null && !op.isDone && Time.realtimeSinceStartup < until) yield return null;
                    var scene = USM.GetSceneByName(name);
                    if (!scene.isLoaded) continue;
                    int acid = 0;
                    string gate = null;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                            if (fsm.FsmName == "Acid Armour Check" && fsm.GetComponent<DamageHero>() != null) acid++;
                        foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                            if (gate == null && !tp.isADoor && (tp.name.StartsWith("left") || tp.name.StartsWith("right"))) gate = tp.name;
                    }
                    var un = USM.UnloadSceneAsync(scene);
                    until = Time.realtimeSinceStartup + 20f;
                    while (un != null && !un.isDone && Time.realtimeSinceStartup < until) yield return null;
                    if (acid > 0 && gate != null) found.Add((name, gate));
                    if (acid > 0) Plugin.Log.LogInfo($"[AutoTest] 酸液 场景 {name}：{acid} 个酸池，入口 {gate ?? "无"}");
                }
            if (found.Count == 0) { Check("找到有酸的房间", false, "没找到"); yield break; }
            var (room, way) = found[0];
            yield return GoTo(room, way);
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var dry = hero.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 酸液 进房：{AcidLiquid.DevReport()}");
            Check("找到酸池", AcidLiquid.DevPools > 0, AcidLiquid.DevReport());
            bool tear = PlayerData.instance.hasAcidArmour;
            PlayerData.instance.hasAcidArmour = false;
            PlayerBuffs.Remove(109);
            PlayerBuffs.Remove(15);
            yield return new WaitForSeconds(0.5f);
            Check("没装备：酸伤人、不能游", !AcidLiquid.DevSwimOn && !AcidLiquid.DevWalkOn, AcidLiquid.DevReport());
            Shot(room, "acid_0_plain");

            PotionBuffs.Give(15, 3600);
            yield return new WaitForSeconds(0.5f);
            Check("喝水上漂药水：水面有落脚处", AcidLiquid.DevWalkOn && Items.Equipment.Current.WaterWalk > 0, AcidLiquid.DevReport());
            if (AcidLiquid.DevSurface(out var surface))
            {
                PlayerData.instance.isInvincible = false;
                int masks = PlayerData.instance.health;
                hero.transform.position = new Vector3(surface.x, surface.y + 2f, hero.transform.position.z);
                yield return new WaitForSeconds(2f);
                var feet = hero.GetComponent<BoxCollider2D>().bounds.min.y;
                Check("站在酸面上", hero.cState.onGround && Mathf.Abs(feet - surface.y) < 0.3f && PlayerData.instance.health >= masks,
                    $"着地 {hero.cState.onGround}，脚 {feet:F2}，水面 {surface.y:F2}，血 {masks}→{PlayerData.instance.health}");
                Shot(room, "acid_2_walk");
                PlayerData.instance.isInvincible = true;
                TerraMovement.DevDown = true;
                yield return new WaitForSeconds(0.6f);
                Check("按住下进入酸里", !AcidLiquid.DevWalkOn && hero.GetComponent<BoxCollider2D>().bounds.min.y < surface.y - 0.2f,
                    $"脚 {hero.GetComponent<BoxCollider2D>().bounds.min.y:F2}，{AcidLiquid.DevReport()}");
                TerraMovement.DevDown = false;
                hero.transform.position = dry;
                yield return new WaitForSeconds(1f);
            }
            PlayerBuffs.Remove(15);
            PotionBuffs.Give(109, 3600);
            yield return new WaitForSeconds(0.5f);
            Check("房间里喝鳍状肢药水：马上能游", AcidLiquid.DevSwimOn && Items.Equipment.Current.Swim, AcidLiquid.DevReport());
            // Into the pool: floating, swimming, not hurt.
            if (AcidLiquid.DevSurface(out surface))
            {
                PlayerData.instance.isInvincible = false;
                int masks = PlayerData.instance.health;
                hero.transform.position = new Vector3(surface.x, surface.y + 1.5f, hero.transform.position.z);
                yield return new WaitForSeconds(2.5f);
                bool swimming = AcidLiquid.Swimming(hero);
                Check("在酸里游泳不掉血", swimming && PlayerData.instance.health >= masks && !hero.cState.hazardDeath,
                    $"游泳 {swimming}，血 {masks}→{PlayerData.instance.health}，位置 {(Vector2)hero.transform.position:F2}，水面 {surface:F2}");
                Shot(room, "acid_1_swim");
                // Out again before the next part: on dry land, the swim ended as leaving the pool ends it.
                yield return Tap(UnityEngine.InputSystem.Key.Space);
                yield return new WaitForSeconds(0.25f);
                hero.transform.position = dry;
                yield return new WaitForSeconds(1f);
                PlayerData.instance.isInvincible = true;
            }
            PlayerBuffs.Remove(109);
            yield return new WaitForSeconds(0.5f);
            Check("药效没了：还漂着时先不收回游泳区", AcidLiquid.DevSwimOn == AcidLiquid.Swimming(HeroController.instance) && !AcidLiquid.DevWalkOn, AcidLiquid.DevReport());
            PlayerData.instance.hasAcidArmour = tear;
            Plugin.Log.LogInfo($"[AutoTest] 酸液 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
