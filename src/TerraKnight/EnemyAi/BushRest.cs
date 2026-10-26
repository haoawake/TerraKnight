using HarmonyLib;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// A Moss Walker gone back into its bush (its Moss Walker machine's Rest) is held where it lies: its body kinematic and
    /// still until its Wake makes it dynamic again (SetIsKinematic2d false). Left dynamic, resting with part of it over a
    /// ledge where its walk ended, the terrain's rounded corner eased it off a little a second, and it
    /// fell through the pit below and out of the room (Fungus1_10's acid, on 5.0.0 too).
    /// </summary>
    [HarmonyPatch]
    internal sealed class BushRest : MonoBehaviour
    {
        PlayMakerFSM fsm;
        Rigidbody2D body;
        public static int DevHeld;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), "Start")]
        static void Attach(HealthManager __instance)
        {
            if (__instance == null || __instance.GetComponent<BushRest>() != null || Net.Enemies.IsPuppet(__instance)) return;
            var body = __instance.GetComponent<Rigidbody2D>();
            if (body == null) return;
            foreach (var f in __instance.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Moss Walker")
                {
                    var rest = __instance.gameObject.AddComponent<BushRest>();
                    rest.fsm = f;
                    rest.body = body;
                    return;
                }
        }

        void FixedUpdate()
        {
            if (fsm == null || body == null || body.bodyType != RigidbodyType2D.Dynamic || fsm.ActiveStateName != "Rest") return;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            DevHeld++;
        }
    }
}
