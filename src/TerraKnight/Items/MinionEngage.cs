using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Minions and sentries wait for the player (the user's): in a room, until he attacks — a weapon swung or fired, the
    /// nail, a spell landing, a blow of his own on an enemy, a target picked with a staff or struck with a whip — they
    /// follow him and fight nothing, nor hurt what they brush past. From then until he leaves the room they fight as
    /// Terraria's do. Grimmchild's, the weavers' and the hatchlings' blows are theirs, not his, and wake nothing.
    /// </summary>
    [HarmonyPatch]
    internal static class MinionEngage
    {
        static string engagedScene;
        public static int DevEngaged;
        /// <summary>Dev: minions fight from the start (the summon suites).</summary>
        public static bool DevAlways;

        static string SceneNow => GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;

        /// <summary>The player has attacked in this room: the minions may fight.</summary>
        public static bool Engaged => DevAlways || (engagedScene != null && engagedScene == SceneNow);

        public static void Engage(string why)
        {
            string s = SceneNow;
            if (s == null || s == engagedScene) return;
            engagedScene = s;
            DevEngaged++;
            Plugin.Log.LogInfo($"[召唤物] 玩家出手（{why}），召唤物开始在 {s} 战斗");
        }

        /// <summary>The start of a use of an item: a weapon of his own (not a tool, a summoning staff or a sentry's).</summary>
        public static void Used(ItemDef def)
        {
            if (def == null || !def.IsWeapon || def.IsSummon || def.Sentry) return;
            if (def.PickPower > 0 || def.HammerPower > 0 || def.AxePower > 0) return;
            Engage(def.Name);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "Attack")]
        static void Nail() => Engage("骨钉");

        /// <summary>The Knight's own blows on an enemy: the nail, its arts and beams, the spells (not a companion's).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void Struck(HitInstance hitInstance)
        {
            var t = hitInstance.AttackType;
            if (t != AttackTypes.Nail && t != AttackTypes.NailBeam && t != AttackTypes.SharpShadow && t != AttackTypes.Spell) return;
            if (MeleeHits.Autonomous || FromCompanion(hitInstance.Source)) return;
            Engage(t.ToString());
        }

        static bool FromCompanion(GameObject go)
        {
            var tr = go != null ? go.transform : null;
            for (int k = 0; k < 5 && tr != null; k++, tr = tr.parent)
            {
                string n = tr.name;
                if (n.Contains("Grimmchild") || n.Contains("Weaverling") || n.Contains("Knight Hatchling") || n.Contains("Grimm Ball")) return true;
            }
            return false;
        }
    }
}
