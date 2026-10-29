using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Minions and sentries fight the room's enemies as Terraria's do, from the start; a boss alone waits for the player's own
    /// blow on it (the user's 10-25 "boss必须等玩家出手召唤物才攻击", 10-27 "召唤物应该锁小怪才对，只是不去攻击首领"). Grimmchild's,
    /// the weavers' and the hatchlings' blows are theirs, not his, and start no boss fight.
    /// </summary>
    [HarmonyPatch]
    internal static class MinionEngage
    {
        /// <summary>Dev: minions fight bosses from the start (the summon suites).</summary>
        public static bool DevAlways;

        static string SceneNow => GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;

        static string bossScene;

        /// <summary>
        /// The player's own blow has landed on a boss in this room: from then its fight is the minions' too. Before it they
        /// leave every boss be, asleep or awake.
        /// </summary>
        public static bool BossFought => DevAlways || (bossScene != null && bossScene == SceneNow);

        /// <summary>A blow of the player's own on a boss (his weapons, shots, nail, spells; not a minion's).</summary>
        public static void StruckBoss(HealthManager hm)
        {
            var info = hm != null ? EnemyStats.Of(hm) : null;
            if (info == null || !info.Boss) return;
            string s = SceneNow;
            if (s == null || s == bossScene) return;
            bossScene = s;
            Plugin.Log.LogInfo($"[召唤物] 玩家打中首领 {info.Record}，召唤物开始在 {s} 打首领");
        }

        /// <summary>The Knight's own blows on an enemy: the nail, its arts and beams, the spells (not a companion's).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void Struck(HealthManager __instance, HitInstance hitInstance)
        {
            var t = hitInstance.AttackType;
            if (t != AttackTypes.Nail && t != AttackTypes.NailBeam && t != AttackTypes.SharpShadow && t != AttackTypes.Spell) return;
            if (MeleeHits.Autonomous || FromCompanion(hitInstance.Source)) return;
            StruckBoss(__instance);
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
