using System.Collections;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "huprobe", the user's 10-28 "新版ai依旧有boss打不到高处，比如胡长老"): Elder Hu in his Godhome hall (TK_HU_SCENE,
    /// GG_Ghost_Hu by default) with the new AI, his scripts and his rings' dumped once, then the player held high (TK_HU_Y,
    /// 16.3) while every live ring box ("Box …" with a collider) is written down with how high its collider goes. Lines
    /// "[AutoTest] 胡长老探测 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HuProbeTest()
        {
            var pd = PlayerData.instance;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string scene = System.Environment.GetEnvironmentVariable("TK_HU_SCENE") ?? "GG_Ghost_Hu";
            float y = float.TryParse(System.Environment.GetEnvironmentVariable("TK_HU_Y"), out var yy) ? yy : 16.3f;
            if (scene.StartsWith("GG_"))
            {
                BossSceneController.SetupEvent = self => { self.BossLevel = 0; self.DreamReturnEvent = "DREAM RETURN"; };
                pd.dreamReturnScene = "Town";
                yield return GoTo(scene, "door_dreamEnter");
                GameManager.instance.FadeSceneIn();
            }
            else yield return GoTo(scene, EntryGateOf(scene));
            HealthManager hm = null;
            for (float until = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < until && (hm == null || !hm.gameObject.activeInHierarchy);)
            {
                foreach (var h in Resources.FindObjectsOfTypeAll<HealthManager>())
                    if (h != null && h.gameObject.scene.IsValid() && h.name.StartsWith("Ghost Warrior Hu")) hm = h;
                yield return null;
            }
            if (hm == null) { Plugin.Log.LogInfo($"[AutoTest] 胡长老探测 没找到胡长老（{GameManager.instance.sceneName}）"); yield break; }
            yield return new WaitForSeconds(4f);
            foreach (var root in hm.gameObject.scene.GetRootGameObjects())
                if (root.GetComponentsInChildren<PlayMakerFSM>(true).Length > 0 && (root.name.Contains("Hu") || root.name.Contains("Ring") || root.name.Contains("Warrior") || root.name.Contains("Box")))
                    DumpFsms(root.transform);
            var hero = HeroController.instance;
            float x = hm.transform.position.x - 6f;
            float maxTop = float.MinValue;
            for (float end = Time.time + 20f; Time.time < end;)
            {
                hero.transform.position = new Vector3(x, y, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                {
                    if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || !c.name.StartsWith("Box")) continue;
                    if (c.gameObject.scene != hm.gameObject.scene && !c.transform.root.name.Contains("Ring")) continue;
                    var b = c.bounds;
                    if (b.max.y > maxTop) maxTop = b.max.y;
                }
                yield return new WaitForSeconds(0.1f);
            }
            var boxes = Resources.FindObjectsOfTypeAll<Collider2D>().Where(c => c != null && c.gameObject.scene == hm.gameObject.scene && c.name.StartsWith("Box"))
                .Select(c => $"{PathOf(c.transform)}@{(Vector2)c.transform.position:F1}") .Distinct().Take(40);
            Plugin.Log.LogInfo($"[AutoTest] 胡长老探测 {GameManager.instance.sceneName}：主角高 {y:F1}，环碰撞体最高到 {maxTop:F1}；环 {string.Join("；", boxes)}");
            BossSceneController.SetupEvent = null;
        }
    }
}
