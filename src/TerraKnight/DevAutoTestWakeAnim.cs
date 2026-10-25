using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wakeanim"): the getting-up after a death, frame by frame (the user: "复活站起来的动画把头顶剪掉了，再跑一遍给我看看").
    /// The Knight killed; while the character-only cutscene pose shows (Player.HeroSpecialAnimations), a screenshot every
    /// other frame with, logged, the sheet and frame shown and the pose's sprite on the screen (its rectangle, in the
    /// screenshot's pixels) for cropping; until control is back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WakeAnimTest()
        {
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            pd.isInvincible = false;
            mod.DevNoFallDamage = false;
            mod.Stats.TakeDirect(999999);
            hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
            float until = Time.time + 10f;
            while (!hero.cState.dead && Time.time < until) yield return null;
            Plugin.Log.LogInfo("[AutoTest] 起身动画 倒下");
            int shots = 0, frame = 0;
            float controlAt = -1f;
            until = Time.time + 45f;
            while (Time.time < until && shots < 140)
            {
                yield return null;
                frame++;
                hero = HeroController.instance;
                if (hero == null) continue;
                if (controlAt < 0f && !hero.cState.dead && GameHooks.PlayerHasControl() && !GameManager.instance.IsInSceneTransition) controlAt = Time.time;
                if (controlAt >= 0f && Time.time - controlAt > 0.6f) break;
                int code = Player.HeroSpecialAnimations.Code(hero);
                bool special = Player.HeroSpecialAnimations.Decode(code, out int kind, out int f);
                SpriteRenderer body = null;
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (sr.name == "Special cutscene body" && sr.enabled) body = sr;
                if (!special && body == null && controlAt < 0f) continue;
                if (frame % 2 != 0) continue;
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : Camera.main;
                string rect = "-";
                if (body != null && cam != null)
                {
                    var b = body.bounds;
                    var lo = cam.WorldToScreenPoint(b.min);
                    var hi = cam.WorldToScreenPoint(b.max);
                    rect = $"{lo.x:F0},{Screen.height - hi.y:F0},{hi.x:F0},{Screen.height - lo.y:F0}";
                }
                var anim = hero.GetComponent<tk2dSpriteAnimator>();
                string clip = anim != null && anim.CurrentClip != null ? anim.CurrentClip.name + "#" + anim.CurrentFrame : "-";
                string name = $"wake_{shots:000}";
                Shot(GameManager.instance.sceneName, name);
                Plugin.Log.LogInfo($"[AutoTest] 起身动画 帧 {name} 原版 {clip} 表 {(special ? kind + "/" + f : "无")} 画着 {(body != null ? body.sprite?.name ?? "?" : "否")} 屏幕 {rect} 屏幕大小 {Screen.width}x{Screen.height}");
                shots++;
            }
            pd.isInvincible = true;
            Plugin.Log.LogInfo($"[AutoTest] 起身动画 结果 截了 {shots} 张");
        }
    }
}
