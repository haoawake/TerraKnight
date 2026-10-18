using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "water"): the Fluke Mother's pool, the way a player falls in: dropped above the
    /// surface, he floats (the hero's "Surface Water" FSM holds him) and jump takes him out. Once
    /// before the fight and once after the Fluke Mother is dead; each time the FSM's state, whether
    /// the jump key reached the game's jump action, and whether control came back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WaterTest()
        {
            const string room = "Waterways_12";
            yield return GoTo(room, "right1");
            yield return new WaitForSeconds(2f);
            var h = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            PlayMakerFSM region = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Surface Water Region") region = f;
            var water = PlayMakerFSM.FindFsmOnGameObject(h.gameObject, "Surface Water");
            if (region == null || water == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 水面 找不到水面（{region != null}）或主角的水面状态机（{water != null}） 不通过");
                yield break;
            }
            var box = region.GetComponent<Collider2D>().bounds;
            Plugin.Log.LogInfo($"[AutoTest] 水面 区域 {box.center:F1} {box.size:F1}");
            for (int round = 0; round < 2; round++)
            {
                string tag = round == 0 ? "开打前" : "吸虫之母死后";
                if (round == 1)
                {
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (UI.EnemyHover.RecordOf(hm) == "FlukeMother" && !hm.isDead)
                        {
                            hm.hp = 1;
                            hm.Hit(new HitInstance { Source = h.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 100, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                        }
                    yield return new WaitForSeconds(6f);
                }
                // Out of the water first, then dropped in from above its surface.
                float t0 = Time.time;
                while (!GameHooks.PlayerHasControl() && Time.time - t0 < 6f) yield return Tap(Key.Space);
                h.transform.position = new Vector3(box.center.x, box.max.y + 3f, h.transform.position.z);
                t0 = Time.time;
                while (water.ActiveStateName != "Idle" && Time.time - t0 < 6f) yield return null;
                yield return new WaitForSeconds(1f);
                string before = $"{water.ActiveStateName}，可操作 {GameHooks.PlayerHasControl()}，主角 {(Vector2)h.transform.position:F1}";
                var a = InputHandler.Instance.inputActions;
                KeysDown(Key.Space);
                yield return new WaitForSecondsRealtime(0.08f);
                bool held = a.jump.IsPressed;
                string mid = water.ActiveStateName;
                yield return new WaitForSecondsRealtime(0.1f);
                KeysDown();
                t0 = Time.time;
                while (!GameHooks.PlayerHasControl() && Time.time - t0 < 3f) yield return null;
                bool back = GameHooks.PlayerHasControl();
                Plugin.Log.LogInfo($"[AutoTest] 水面 {tag}：漂着 {before}；按跳跃时动作集启用 {a.Enabled}/{a.jump.Enabled}，跳跃按下 {held}，状态 {mid}；之后 {water.ActiveStateName}，控制回来 {back} {(back ? "通过" : "不通过")}");
                Shot(room, $"water_{round}");
                yield return null;
            }
        }
    }
}
