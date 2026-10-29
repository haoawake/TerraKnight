using System.Collections;
using System.Linq;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator VesselEntryTest()
        {
            if (!DevCopy.Verified) throw new System.InvalidOperationException("Isolated saves required");
            int all = 0, ok = 0;
            void Check(string text, bool pass)
            {
                all++; if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 容器入口 {text}：{(pass ? "通过" : "不通过")}");
            }
            PlayMakerFSM Gate() => Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                .FirstOrDefault(f => f.gameObject.scene.name == "Room_Final_Boss_Core" && f.name == "Gate" && f.FsmName == "Control");
            GameObject contact = null;
            IEnumerator TouchAhead(PlayMakerFSM gate, bool child)
            {
                var hero = HeroController.instance;
                var entry = gate.GetComponent<Collider2D>().bounds;
                contact = new GameObject(child ? "Dev forward weapon collider" : "Dev foreign collider");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(contact, gate.gameObject.scene);
                contact.layer = hero.gameObject.layer;
                if (child) contact.transform.SetParent(hero.transform);
                contact.transform.position = new Vector3(entry.center.x, hero.transform.position.y, 0f);
                var box = contact.AddComponent<BoxCollider2D>(); box.size = Vector2.one * .5f;
                if (!child) { var rb = contact.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; }
                Physics2D.SyncTransforms();
                yield return new WaitForSeconds(.5f);
                Object.Destroy(contact); contact = null;
                yield return new WaitForSeconds(.1f);
            }
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            try
            {
                // Real native physical trigger, with the old guard and the body still at the entrance.
                ArenaTriggers.DevLegacyVesselGate = true;
                yield return GoTo("Room_Final_Boss_Core", "left1");
                yield return new WaitForSeconds(1f);
                var gate = Gate();
                Check("原生入口和关门触发器存在", gate != null && gate.GetComponent<Collider2D>() != null);
                if (gate == null) yield break;
                var hero = HeroController.instance;
                float outside = hero.transform.position.x;
                yield return TouchAhead(gate, false);
                Check("旧逻辑复现其他碰撞物关门、玩家还在门外", gate.ActiveStateName == "Activate" && Mathf.Abs(hero.transform.position.x - outside) < 1f);
                Shot("Room_Final_Boss_Core", "vessel_entry_legacy"); yield return new WaitForSeconds(.2f);
                yield return GoTo("Town", "left1");
                ArenaTriggers.DevLegacyVesselGate = false;
                yield return GoTo("Room_Final_Boss_Core", "left1");
                yield return new WaitForSeconds(1f);
                gate = Gate(); hero = HeroController.instance;
                Check("重新进房入口保持开启", gate != null && gate.ActiveStateName == "Idle");
                if (gate == null) yield break;
                yield return TouchAhead(gate, false);
                Check("其他碰撞物不能提前关门", gate.ActiveStateName == "Idle");
                yield return TouchAhead(gate, true);
                Check("向前伸出的武器碰撞体不能提前关门", gate.ActiveStateName == "Idle");
                Check("步行路径没有晶塔到达状态", !Pylons.LoadingByPortal("Room_Final_Boss_Core"));
                // Movement input, without teleporting over the seal or sending a synthetic ENTER.
                Player.TerraMovement.DevMove = 1;
                float until = Time.time + 12f;
                bool shutTooSoon = false;
                while (Time.time < until && hero.transform.position.x < 35f)
                {
                    var body = hero.GetComponent<BoxCollider2D>().bounds;
                    var entry = gate.GetComponent<Collider2D>().bounds;
                    if (gate.ActiveStateName == "Activate" && body.min.x <= entry.min.x + .05f) shutTooSoon = true;
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(.4f);
                Check("角色靠正常行走通过封门位置", hero.transform.position.x >= 35f);
                Check("整个身体进入前没有封门", !shutTooSoon);
                Check("走入后补发原生进入、门正常关闭", gate.ActiveStateName == "Activate" && gate.GetComponentsInChildren<Collider2D>().Any(c => !c.isTrigger && c.enabled));
                Check("走入后角色可以正常操作", GameHooks.PlayerHasControl());
                Shot("Room_Final_Boss_Core", "vessel_entry_fixed"); yield return new WaitForSeconds(.2f);
                yield return GoTo("Town", "left1");
                BossSceneController.SetupEvent = self => { self.BossLevel = 0; self.DreamReturnEvent = "DREAM RETURN"; };
                PlayerData.instance.dreamReturnScene = "Town";
                yield return GoTo("GG_Hollow_Knight", "door_dreamEnter");
                yield return new WaitForSeconds(1f);
                GameManager.instance.FadeSceneIn();
                yield return new WaitForSeconds(20f);
                var vessel = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                    .FirstOrDefault(f => f.gameObject.scene.name == "GG_Hollow_Knight" && f.name == "HK Prime" && f.FsmName == "Control");
                Check("神居纯粹容器原生开场继续进入战斗", vessel != null && !vessel.ActiveStateName.StartsWith("Intro") && vessel.ActiveStateName != "Pause" && vessel.ActiveStateName != "Init");
                Check("神居角色可以操作且没有晶塔状态", GameHooks.PlayerHasControl() && !Pylons.LoadingByPortal("GG_Hollow_Knight"));
                Shot("GG_Hollow_Knight", "vessel_entry_godhome"); yield return new WaitForSeconds(.2f);
            }
            finally
            {
                if (contact != null) Object.Destroy(contact);
                ArenaTriggers.DevLegacyVesselGate = false;
                Player.TerraMovement.DevMove = 0;
            }
            Plugin.Log.LogInfo($"[AutoTest] 容器入口 合计 {ok}/{all}");
        }
    }
}
