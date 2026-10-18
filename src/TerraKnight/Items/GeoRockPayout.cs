using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The geo rocks lying about (GeoRock and its "Geo Rock" FSM) pay 150 geo each, shared out between their hits and their
    /// breaking (the game's own pay 15 to 26: Geo Per Hit small geo a hit, Final Payout more as it breaks). As a rock starts,
    /// its FSM's flings of geo are made medium geo (5 apiece, the enemies' own prefab), as many as make 150: a rock of five
    /// hits 25 a hit and 25 as it breaks, one of four 30 and 30. A rock hit before in an old save pays the 150 over the
    /// hits it has left. Without a medium geo to hand, small geo by the same sums.
    /// </summary>
    [HarmonyPatch]
    internal static class GeoRockPayout
    {
        public const int Total = 150;

        static readonly AccessTools.FieldRef<HealthManager, GameObject> mediumGeoPrefab = AccessTools.FieldRefAccess<HealthManager, GameObject>("mediumGeoPrefab");
        static GameObject medium;
        static int lookedFrame = -1;

        public static int DevPaid;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GeoRock), "Start")]
        static void Started(GeoRock __instance) => Pay(__instance);

        /// <summary>The enemies' medium geo (any HealthManager's), looked for once a frame until found.</summary>
        static GameObject Medium()
        {
            if (medium != null || lookedFrame == Time.frameCount) return medium;
            lookedFrame = Time.frameCount;
            foreach (var hm in Resources.FindObjectsOfTypeAll<HealthManager>())
            {
                var p = hm != null ? mediumGeoPrefab(hm) : null;
                if (p != null) { medium = p; break; }
            }
            return medium;
        }

        static PlayMakerFSM FsmOf(GeoRock rock)
        {
            foreach (var f in rock.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Geo Rock") return f;
            return null;
        }

        /// <summary>A rock's flings of geo set to pay <see cref="Total"/> over its hits left and its breaking.</summary>
        internal static bool Pay(GeoRock rock)
        {
            var fsm = rock != null ? FsmOf(rock) : null;
            if (fsm == null) return false;
            int hits = fsm.FsmVariables.GetFsmInt("Hits").Value;
            if (hits <= 0) return false;
            var prefab = Medium();
            int pieces = prefab != null ? Total / 5 : Total;
            int perHit = pieces / (hits + 1), last = pieces - perHit * hits;
            bool any = false;
            foreach (var st in fsm.FsmStates)
            {
                if (st.Name != "Hit" && st.Name != "Destroy") continue;
                foreach (var a in st.Actions)
                {
                    if (!(a is FlingObjectsFromGlobalPool f) || f.gameObject == null || f.gameObject.Value == null || !f.gameObject.Value.name.StartsWith("Geo")) continue;
                    int n = st.Name == "Hit" ? perHit : last;
                    f.spawnMin.Value = n;
                    f.spawnMax.Value = n;
                    if (prefab != null) f.gameObject.Value = prefab;
                    any = true;
                }
            }
            if (any) DevPaid++;
            return any;
        }

        /// <summary>Dev: what a rock pays as its FSM stands: hits left × a hit's + the breaking's, in geo.</summary>
        internal static int DevPayout(GeoRock rock)
        {
            var fsm = rock != null ? FsmOf(rock) : null;
            if (fsm == null) return -1;
            int hits = fsm.FsmVariables.GetFsmInt("Hits").Value, perHit = 0, last = 0;
            foreach (var st in fsm.FsmStates)
                foreach (var a in st.Actions)
                    if (a is FlingObjectsFromGlobalPool f && f.gameObject?.Value != null && f.gameObject.Value.name.StartsWith("Geo"))
                    {
                        var gc = f.gameObject.Value.GetComponent<GeoControl>();
                        int unit = gc != null && gc.sizes != null && gc.type >= 0 && gc.type < gc.sizes.Length ? gc.sizes[gc.type].value : 1;
                        if (st.Name == "Hit") perHit = f.spawnMax.Value * unit;
                        else if (st.Name == "Destroy") last = f.spawnMax.Value * unit;
                    }
            return hits * perHit + last;
        }
    }
}
