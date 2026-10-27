using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fkroom", the user's 10-26 "在boss房间外就不应该能打到或者触发boss，尤其是假骑士底下那个空房间"): Crossroads_10
    /// with the False Knight waiting: his battle's trigger (Battle Scene's colliders), his arena (ArenaGuard), the camera
    /// locks, and for a grid of clear places over the room whether a blow from there would count (ArenaGuard.Outside) and
    /// whether the place is inside the battle's trigger. Lines "[AutoTest] 假骑士房 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FkRoomProbe()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Crossroads_10", EntryGateOf("Crossroads_10"));
            yield return new WaitForSeconds(2f);
            var fk = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.isDead && UI.EnemyHover.RecordOf(h) == "FalseKnight" && h.name.StartsWith("False Knight"));
            if (fk == null) { Plugin.Log.LogInfo("[AutoTest] 假骑士房 没有假骑士"); yield break; }
            var battle = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f.FsmName == "Battle Control" && f.gameObject.scene.name.StartsWith("Crossroads_10"));
            foreach (var b in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f => f.FsmName == "Battle Control")) b.enabled = false;
            var triggers = battle != null ? battle.GetComponents<Collider2D>() : new Collider2D[0];
            foreach (var c in triggers) Plugin.Log.LogInfo($"[AutoTest] 假骑士房 战斗触发区 {c.GetType().Name} 触发 {c.isTrigger} 开 {c.enabled} 范围 x {c.bounds.min.x:F1}~{c.bounds.max.x:F1} y {c.bounds.min.y:F1}~{c.bounds.max.y:F1}");
            Plugin.Log.LogInfo($"[AutoTest] 假骑士房 假骑士 {(Vector2)fk.transform.position:F1}，{World.ArenaGuard.DevArena(fk)}");
            foreach (var l in Object.FindObjectsByType<CameraLockArea>(FindObjectsSortMode.None).Where(l => l.gameObject.scene.name.StartsWith("Crossroads_10")))
            {
                var c = l.GetComponent<Collider2D>();
                Plugin.Log.LogInfo($"[AutoTest] 假骑士房 镜头锁 {l.name}（{l.gameObject.scene.name}）x {c?.bounds.min.x:F1}~{c?.bounds.max.x:F1} y {c?.bounds.min.y:F1}~{c?.bounds.max.y:F1}，锁 x {l.cameraXMin:F1}~{l.cameraXMax:F1} y {l.cameraYMin:F1}~{l.cameraYMax:F1}");
            }
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            // TK_FKR_NOHIDDEN=1: the arena alone (his waiting hidden not counted).
            World.ArenaGuard.DevNoHidden = System.Environment.GetEnvironmentVariable("TK_FKR_NOHIDDEN") == "1";
            // The minions' choice from each place too (Minions.FindNpcs' boss rule: none before the player's own blow).
            var scene = World.TerraWorld.RegionAt(World.CellGrid.WorldToCell(hero.transform.position).x, World.CellGrid.WorldToCell(hero.transform.position).y);
            for (float y = 2f; y <= 44f; y += 3f)
            {
                var row = new System.Text.StringBuilder();
                for (float x = 2f; x <= 56f; x += 3f)
                {
                    var p = new Vector2(x, y);
                    bool solid = World.TerraWorld.IsSolidAt(p) || Physics2D.OverlapPoint(p, 1 << 8) != null;
                    if (solid) { row.Append('#'); continue; }
                    hero.transform.position = p;
                    Physics2D.SyncTransforms();
                    bool outside = World.ArenaGuard.Outside(fk);
                    bool inTrigger = triggers.Any(c => c.enabled && c.OverlapPoint(p));
                    row.Append(inTrigger ? (outside ? 'T' : 'B') : (outside ? '.' : 'h'));
                }
                Plugin.Log.LogInfo($"[AutoTest] 假骑士房 y={y,4:F0} {row}");
            }
            Plugin.Log.LogInfo("[AutoTest] 假骑士房 图例：# 实心，. 打不到，h 打得到，T 打不到但在战斗触发区，B 打得到且在触发区；x 从 2 起每 3 格");
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }
}
