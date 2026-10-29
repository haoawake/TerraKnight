using System.Collections;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "lostkinentry", the user's 10-29 "打梦境残破容器的时候，刚进来直接从平台往下跳，就触发了boss战"): into the Lost
    /// Kin's dream by its door as the Dream Nail brings the player, both AIs (TK_LKE_AI old/new, default both). Logged: where
    /// the player stands on arrival, the room's battle FSMs and their trigger boxes, the boss's main state; then 6 s standing
    /// still and 6 s walking right off the ledge, the states as they change and where the player was then. Pictures on
    /// arrival and as the fight starts. Lines "[AutoTest] LostKin …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LostKinEntryTest()
        {
            string ai = System.Environment.GetEnvironmentVariable("TK_LKE_AI") ?? "";
            var kinds = ai == "old" ? new[] { World.EnemyAiKind.Vanilla } : ai == "new" ? new[] { World.EnemyAiKind.New } : new[] { World.EnemyAiKind.Vanilla, World.EnemyAiKind.New };
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            foreach (var kind in kinds)
            {
                World.EnemyAiMode.Current = kind;
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                yield return GoTo("Dream_03_Infected_Knight", "door1");
                var hero = HeroController.instance;
                Plugin.Log.LogInfo($"[AutoTest] LostKin {kind}：到达 {(Vector2)hero.transform.position:F2}，着地 {hero.cState.onGround}");
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                {
                    if (f == null || f.gameObject.scene.name != "Dream_03_Infected_Knight") continue;
                    string n = f.gameObject.name + "/" + f.FsmName;
                    if (!(n.Contains("Battle") || n.Contains("IK") || n.Contains("Infected") || n.Contains("Control") || n.Contains("Trigger") || n.Contains("Camera"))) continue;
                    var boxes = string.Join(" ", f.GetComponents<Collider2D>().Select(c => $"{(c.isTrigger ? "触发" : "实体")}{c.bounds.min:F1}-{c.bounds.max:F1}"));
                    Plugin.Log.LogInfo($"[AutoTest] LostKin {kind} FSM {n}：{f.ActiveStateName}，位置 {(Vector2)f.transform.position:F1} {boxes}");
                }
                Shot("Dream_03_Infected_Knight", $"lostkin_{kind}_arrive");
                var ik = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f != null && f.FsmName == "IK Control");
                var battle = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f => f != null && f.gameObject.scene.name == "Dream_03_Infected_Knight" && (f.gameObject.name.Contains("Battle") || f.FsmName.Contains("Battle"))).ToArray();
                string Last() => (ik != null ? "IK " + ik.ActiveStateName : "无IK") + " | " + string.Join("，", battle.Select(b => b.gameObject.name + "/" + b.FsmName + " " + b.ActiveStateName));
                string last = Last();
                Plugin.Log.LogInfo($"[AutoTest] LostKin {kind} 0.0s {last}");
                bool shot = false, dropped = false;
                float t0 = Time.time;
                while (Time.time - t0 < 12f)
                {
                    yield return null;
                    bool walk = Time.time - t0 >= 6f;
                    // Then dropped from over the arena's middle, as one who jumps down the shaft (the trigger's top is 43.6).
                    if (walk && !dropped)
                    {
                        dropped = true;
                        hero.transform.position = new Vector3(26f, 60f, hero.transform.position.z);
                        var body = hero.GetComponent<Rigidbody2D>();
                        if (body != null) body.linearVelocity = Vector2.zero;
                        Plugin.Log.LogInfo($"[AutoTest] LostKin {kind} 从 (26, 60) 往下落");
                    }
                    string now = Last();
                    if (now == last) continue;
                    last = now;
                    Plugin.Log.LogInfo($"[AutoTest] LostKin {kind} {Time.time - t0:F1}s {(walk ? "落" : "站")} 玩家 {(Vector2)hero.transform.position:F2} 着地 {hero.cState.onGround}：{now}");
                    if (!shot && ik != null && !ik.ActiveStateName.Contains("Dormant") && !ik.ActiveStateName.Contains("Init"))
                    {
                        shot = true;
                        Shot("Dream_03_Infected_Knight", $"lostkin_{kind}_start");
                    }
                }
            }
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            Plugin.Log.LogInfo("[AutoTest] LostKin 结束");
        }
    }
}
