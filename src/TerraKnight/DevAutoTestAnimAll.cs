using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "animall"): the character-only cutscene poses besides the getting-up (the user: "别的所有动画都录一遍给我看看").
    /// Standing in Dirtmouth with control held and the game's own animation control off, the Knight's own clips played in
    /// the order the game plays them for each sheet (Player.HeroSpecialAnimations: the spell, focus, ability, map, roar and
    /// Dreamgate ones); a screenshot every other frame, logged with the sheet, the clip and its frame, the sheet frame shown
    /// and the pose's rectangle on the screen, named anim_&lt;sheet&gt;_NNN for the strips and GIFs.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AnimAllTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            var anim = hero.GetComponent<tk2dSpriteAnimator>();
            var sets = new (string Sheet, string[] Clips)[]
            {
                ("absorb_magic", new[] { "Collect Magical 1", "Collect Magical 2", "Collect Magical 3", "Collect Magical Fall", "Collect Magical Land" }),
                ("focus", new[] { "Focus", "Focus Get Once", "Focus End" }),
                ("collect_ability", new[] { "Collect Normal 1", "Collect Normal 2", "Collect Normal 3", "Collect Heart Piece", "Collect Heart Piece End" }),
                ("read_map", new[] { "Map Open", "Map Idle", "Map Away" }),
                ("roar_stagger", new[] { "Roar Lock", "Stun" }),
                ("dream_warp", new[] { "DG Warp Charge", "DG Warp", "DG Warp In" }),
            };
            hero.RelinquishControl();
            hero.StopAnimationControl();
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : Camera.main;
            try
            {
                foreach (var (sheet, clips) in sets)
                {
                    int shots = 0, frame = 0;
                    foreach (var name in clips)
                    {
                        var clip = anim.GetClipByName(name);
                        if (clip == null) { Plugin.Log.LogInfo($"[AutoTest] 动画录制 {sheet} 没有片段 {name}"); continue; }
                        anim.Play(clip);
                        float fps = clip.fps > 0f ? clip.fps : 12f;
                        bool loops = clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.Loop || clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.LoopSection;
                        float length = loops ? Mathf.Max(1.2f, clip.frames.Length / fps) : clip.frames.Length / fps + 0.15f;
                        for (float t = 0f; t < length && shots < 120; t += Time.deltaTime)
                        {
                            yield return null;
                            if (++frame % 2 != 0) continue;
                            int code = Player.HeroSpecialAnimations.Code(hero);
                            bool special = Player.HeroSpecialAnimations.Decode(code, out int kind, out int f);
                            SpriteRenderer body = null;
                            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                                if (sr.name == "Special cutscene body" && sr.enabled) body = sr;
                            string rect = "-";
                            if (body != null && cam != null)
                            {
                                var b = body.bounds;
                                var lo = cam.WorldToScreenPoint(b.min);
                                var hi = cam.WorldToScreenPoint(b.max);
                                rect = $"{lo.x:F0},{Screen.height - hi.y:F0},{hi.x:F0},{Screen.height - lo.y:F0}";
                            }
                            string shot = $"anim_{sheet}_{shots:000}";
                            Shot("Town", shot);
                            Plugin.Log.LogInfo($"[AutoTest] 动画录制 帧 {shot} 片段 {name}#{anim.CurrentFrame} 表 {(special ? kind + "/" + f : "无")} 屏幕 {rect}");
                            shots++;
                        }
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 动画录制 {sheet} 截了 {shots} 张");
                    anim.Play("Idle");
                    yield return new WaitForSeconds(0.4f);
                }
            }
            finally
            {
                hero.StartAnimationControl();
                hero.RegainControl();
                Plugin.Log.LogInfo("[AutoTest] 动画录制 结果 完成");
            }
        }
    }
}
