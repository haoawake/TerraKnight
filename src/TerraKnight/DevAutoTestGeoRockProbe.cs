using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>Dev (suite "fx_17georockprobe"): the geo rocks of a few rooms, their FSMs' spawning actions and fields, logged.</summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GeoRockProbe17()
        {
            foreach (var scene in new[] { "Tutorial_01", "Crossroads_02", "Crossroads_07", "Fungus1_02", "Mines_02", "Abyss_04" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
                var rocks = Object.FindObjectsByType<GeoRock>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Plugin.Log.LogInfo($"[AutoTest] 吉欧堆 {scene}：{rocks.Length} 块");
                int shown = 0;
                foreach (var rock in rocks)
                {
                    if (shown++ >= 2) break;
                    var sb = new System.Text.StringBuilder();
                    foreach (var f in rock.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        sb.Append(f.Name).Append('=').Append(Describe(f.GetValue(rock))).Append(' ');
                    Plugin.Log.LogInfo($"[AutoTest]   {rock.name}（{rock.gameObject.scene.name}）：{sb}");
                    foreach (var fsm in rock.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        var ints = new System.Text.StringBuilder();
                        foreach (var v in fsm.FsmVariables.IntVariables) ints.Append(v.Name).Append('=').Append(v.Value).Append(' ');
                        Plugin.Log.LogInfo($"[AutoTest]     FSM '{fsm.FsmName}' 整数 {ints}");
                        foreach (var st in fsm.FsmStates)
                            foreach (var a in st.Actions)
                            {
                                string n = a.GetType().Name;
                                if (!n.Contains("Fling") && !n.Contains("Spawn") && !n.Contains("Int")) continue;
                                var fields = new System.Text.StringBuilder();
                                foreach (var f in a.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                                    fields.Append(f.Name).Append('=').Append(Describe(f.GetValue(a))).Append(' ');
                                Plugin.Log.LogInfo($"[AutoTest]       {st.Name}/{n}：{fields}");
                            }
                    }
                }
            }
            FxCheck("吉欧堆探测", true, "");
        }

        static string Describe(object v)
        {
            switch (v)
            {
                case null: return "null";
                case HutongGames.PlayMaker.FsmGameObject g: return g.Value != null ? g.Value.name : (g.Name ?? "") + "(空)";
                case HutongGames.PlayMaker.FsmInt i: return i.Value.ToString();
                case HutongGames.PlayMaker.FsmFloat f: return f.Value.ToString("0.##");
                case HutongGames.PlayMaker.FsmBool b: return b.Value.ToString();
                case HutongGames.PlayMaker.FsmString s: return s.Value;
                case HutongGames.PlayMaker.FsmOwnerDefault o: return o.OwnerOption.ToString();
                case HutongGames.PlayMaker.NamedVariable nv: return nv.ToString();
                case Object uo: return uo != null ? uo.name : "null";
                default: return v.ToString();
            }
        }
    }
}
