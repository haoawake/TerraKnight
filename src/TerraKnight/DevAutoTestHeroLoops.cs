using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "heroloops", the user's 10-28 "角色的动画有些在抽搐…俯卧撑…无限循环张手"): each hero clip of the cutscene
    /// sheets that the game loops, played on the Knight for three seconds (his animation controller held off): the sheet
    /// frames shown, and how often the frame changes once the clip's first pass is over (none: the pose holds). Lines
    /// "[AutoTest] 过场循环 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HeroLoopsTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var anim = hero.GetComponent<tk2dSpriteAnimator>();
            var control = hero.GetComponent<HeroAnimationController>();
            hero.RelinquishControl();
            control.StopControl();
            int ok = 0, all = 0;
            foreach (var kv in Player.HeroSpecialAnimations.Clips)
            {
                var clip = anim.Library.GetClipByName(kv.Key);
                if (clip == null || kv.Value.Last == kv.Value.First || clip.frames.Length <= 1) continue;
                if (clip.wrapMode != tk2dSpriteAnimationClip.WrapMode.Loop && clip.wrapMode != tk2dSpriteAnimationClip.WrapMode.LoopSection) continue;
                anim.Play(clip);
                float start = Time.time;
                float settled = (clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.LoopSection ? Mathf.Max(1, clip.loopStart) : clip.frames.Length) / Mathf.Max(1f, clip.fps) + 0.2f;
                var seen = new List<int>();
                int last = int.MinValue, late = 0;
                while (Time.time - start < 3f)
                {
                    if (anim.CurrentClip != clip) anim.Play(clip);
                    int code = Player.HeroSpecialAnimations.Code(hero);
                    int f = Player.HeroSpecialAnimations.Decode(code, out _, out int fr) ? fr : -1;
                    if (f != last)
                    {
                        if (Time.time - start > settled && last != int.MinValue) late++;
                        seen.Add(f);
                        last = f;
                    }
                    yield return null;
                }
                all++;
                bool pass = late == 0 && last == kv.Value.Last;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 过场循环 {kv.Key}（{clip.wrapMode}，起点 {clip.loopStart}/{clip.frames.Length}）：帧 {string.Join(",", seen)}，第一遍后变 {late} 次，停在 {last}（应 {kv.Value.Last}） {(pass ? "通过" : "不通过")}");
            }
            control.StartControl();
            hero.RegainControl();
            Plugin.Log.LogInfo($"[AutoTest] 过场循环 合计 {ok}/{all}");
        }
    }
}
