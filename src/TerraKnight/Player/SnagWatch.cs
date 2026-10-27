using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// The Knight held to one side and going nowhere (the user's 10-26: "从天上掉下来或者走路的时候会突然卡住脚无法移动，要跳一下或者
    /// 勾爪才能恢复"): after a quarter second of no ground gained with a way held and the game handing him control, what is
    /// ahead is looked at. Nothing solid at body height (only something at the very feet: a seam between two collider
    /// outlines, a lip of a prop): he is lifted a little over it, as the jump that freed him did. His own state left at one
    /// that takes no steps (no input, a landing's hold) while the game hands him control: put back to standing. Each time a
    /// line "[卡脚]" in the log with what was there, so a stall that comes back can be told apart.
    /// </summary>
    [HarmonyPatch]
    internal static class SnagWatch
    {
        static float stillFor, lastX, saidAt = -10f;
        public static int DevLifts, DevStates;
        public static bool Enabled = true;

        static readonly AccessTools.FieldRef<HeroController, GlobalEnums.ActorStates> heroState =
            AccessTools.FieldRefAccess<HeroController, GlobalEnums.ActorStates>("hero_state");
        static readonly System.Reflection.MethodInfo setState = AccessTools.Method(typeof(HeroController), "SetState");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void Watch(HeroController __instance, Rigidbody2D ___rb2d)
        {
            var hero = __instance;
            if (!Enabled || hero == null || ___rb2d == null || TerraMod.Instance == null || World.TerrainSystem.Current == null) return;
            var cs = hero.cState;
            var input = GameHooks.Input;
            int way = TerraMovement.DevMove != 0 ? TerraMovement.DevMove
                : input != null && input.inputActions.right.IsPressed && !input.inputActions.left.IsPressed ? 1
                : input != null && input.inputActions.left.IsPressed && !input.inputActions.right.IsPressed ? -1 : 0;
            float x = ___rb2d.position.x;
            bool held = way != 0 && hero.acceptingInput && !hero.controlReqlinquished && !cs.dead && !cs.transitioning && !cs.wallSliding
                        && !Mounts.OwnMovement && !GrapplingHook.IsLatched && !TerraMovement.OnRope;
            if (!held || Mathf.Abs(x - lastX) > 0.004f)
            {
                stillFor = 0f;
                lastX = x;
                return;
            }
            stillFor += Time.fixedDeltaTime;
            if (stillFor < 0.25f) return;
            stillFor = 0f;
            var state = heroState(hero);
            // A state that takes no steps while the game hands him control.
            if (state == GlobalEnums.ActorStates.no_input || state == GlobalEnums.ActorStates.hard_landing || state == GlobalEnums.ActorStates.dash_landing)
            {
                setState?.Invoke(hero, new object[] { cs.onGround ? GlobalEnums.ActorStates.grounded : GlobalEnums.ActorStates.airborne });
                DevStates++;
                Say(hero, ___rb2d, way, $"状态 {state} 复位为 {heroState(hero)}");
                return;
            }
            // Something solid at body height ahead (a wall, a closed door, an enemy): a real stop.
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            const float foot = 0.3f;
            int mask = Physics2D.GetLayerCollisionMask(hero.gameObject.layer);
            var size = new Vector2(b.size.x * 0.9f, Mathf.Max(0.2f, b.size.y - foot - 0.05f));
            var centre = new Vector2(b.center.x, b.min.y + foot + size.y * 0.5f);
            foreach (var hit in Physics2D.BoxCastAll(centre, size, 0f, new Vector2(way, 0f), 0.12f, mask))
                if (hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<HeroController>() == null) return;
            // Only something at the feet: onto its top (a seam: a hair), a little ahead.
            float top = b.min.y;
            float frontX = way > 0 ? b.max.x + 0.08f : b.min.x - 0.08f;
            foreach (var hit in Physics2D.RaycastAll(new Vector2(frontX, b.min.y + foot), Vector2.down, foot + 0.05f, mask))
                if (hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<HeroController>() == null) top = Mathf.Max(top, hit.point.y);
            var lift = new Vector2(way * 0.12f, Mathf.Max(0.06f, top - b.min.y + 0.04f));
            ___rb2d.position += lift;
            hero.transform.position += (Vector3)lift;
            DevLifts++;
            Say(hero, ___rb2d, way, "脚下被绊住，抬起越过");
        }

        static void Say(HeroController hero, Rigidbody2D rb, int way, string what)
        {
            if (Time.unscaledTime - saidAt < 1f) return;
            saidAt = Time.unscaledTime;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var foot = new Vector2(way > 0 ? b.max.x : b.min.x, b.min.y);
            var near = new System.Collections.Generic.List<string>();
            foreach (var col in Physics2D.OverlapBoxAll(foot + new Vector2(way * 0.1f, 0.1f), new Vector2(0.3f, 0.3f), 0f))
                if (col != null && !col.isTrigger && col.GetComponentInParent<HeroController>() == null) near.Add($"{col.name}[{col.GetType().Name} 层{col.gameObject.layer}]");
            var cs = hero.cState;
            Plugin.Log.LogInfo($"[卡脚] {GameManager.instance?.sceneName} 脚 {foot:F2} 往{(way > 0 ? "右" : "左")}：{what}；速度 {rb.velocity:F2} 着地 {cs.onGround} 后坐 {cs.recoiling} 攻击 {cs.attacking} 冲刺 {TerraMovement.IsDashing} 下砸 {TerraMovement.DownDashing}，前脚碰到 {(near.Count == 0 ? "无" : string.Join("、", near))}");
        }
    }
}
