using System.Collections;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_&lt;name&gt;", "p4_&lt;name&gt;_zh" for the Chinese cut): the 4.0 trailer's clips. Each is a method
    /// P4_&lt;name&gt;(HeroController) in a DevAutoTestPromo4*.cs file of its own, found here by name, so the clips can be
    /// written apart. A clip records with StartRec/StopRec (DevAutoTestPromo: frame by frame at 50 a second into
    /// dev/&lt;copy&gt;/rec/&lt;clip&gt;/).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Promo4(string name)
        {
            var m = GetType().GetMethod("P4_" + name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null || m.ReturnType != typeof(IEnumerator))
            {
                Plugin.Log.LogError("[AutoTest] 录制 没有 P4_" + name + "，不通过");
                yield break;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 p4 {name}{(zh ? "（中文）" : "")}");
            yield return (IEnumerator)m.Invoke(this, new object[] { HeroController.instance });
            if (recording) StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 p4 {name} 完成");
        }

        /// <summary>The showcase kit on a creative save, the hero kept alive: the state most clips start from (as promoB).</summary>
        IEnumerator P4Kit(DifficultyLevel difficulty = DifficultyLevel.Master)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            GameDifficulty.Current = difficulty;
            yield return new WaitForSeconds(1f);
        }

        /// <summary>The pipeline checked: three seconds of the start, recorded.</summary>
        IEnumerator P4_hello(HeroController hero)
        {
            yield return P4Kit();
            StartRec("p4_hello");
            yield return new WaitForSeconds(3f);
            StopRec();
        }
    }
}
