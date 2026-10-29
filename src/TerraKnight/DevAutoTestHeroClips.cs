using System.Collections;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "heroclips", the user's 10-28 "吸收法术…又变成无限循环张手了"): every hero clip the cutscene sheets stand in
    /// for (Player.HeroSpecialAnimations.Clips), with how the game plays it: wrap mode, frames, fps, loop start, and the
    /// sheet range given it. Lines "[AutoTest] 主角片段 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HeroClipsTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var anim = HeroController.instance.GetComponent<tk2dSpriteAnimator>();
            var lib = anim != null ? anim.Library : null;
            if (lib == null) { Plugin.Log.LogInfo("[AutoTest] 主角片段 没有动画库"); yield break; }
            foreach (var kv in Player.HeroSpecialAnimations.Clips.OrderBy(k => k.Value.Kind).ThenBy(k => k.Value.First))
            {
                var c = lib.GetClipByName(kv.Key);
                string how = c == null ? "库里没有" : $"{c.wrapMode} 帧 {c.frames.Length} fps {c.fps:F0} 循环起点 {c.loopStart}";
                Plugin.Log.LogInfo($"[AutoTest] 主角片段 {kv.Key}：{how}；表 {kv.Value.Kind} {kv.Value.First}-{kv.Value.Last}{(kv.Value.Once ? " 只播一次" : "")}");
            }
            // The other clips whose names speak of getting something (a pickup's), not in the table.
            foreach (var c in lib.clips.Where(c => c != null && !string.IsNullOrEmpty(c.name) && !Player.HeroSpecialAnimations.Clips.ContainsKey(c.name)
                         && (c.name.Contains("Get") || c.name.Contains("Collect") || c.name.Contains("Scream") || c.name.Contains("Quake") || c.name.Contains("DN ") || c.name.Contains("Dream"))))
                Plugin.Log.LogInfo($"[AutoTest] 主角片段 表外 {c.name}：{c.wrapMode} 帧 {c.frames.Length} fps {c.fps:F0}");
        }
    }
}
