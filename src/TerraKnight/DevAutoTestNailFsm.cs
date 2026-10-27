using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>Dev (suite "nailfsm"): the Knight's Nail Arts FSM laid out (how a let-go charge picks its art). Lines "[AutoTest] 骨钉技艺FSM …".</summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailFsmTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            foreach (var f in hero.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Nail Arts") DumpFsm(f);
            Plugin.Log.LogInfo("[AutoTest] 骨钉技艺FSM 完");
        }
    }
}
