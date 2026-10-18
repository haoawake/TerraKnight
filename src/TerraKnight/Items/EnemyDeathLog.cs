using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Every enemy death the player didn't cause goes to the log with what did it: the hit that
    /// brought it down (its attack type and source), or the game's own call to HealthManager.Die
    /// (an FSM's InstaDeath, a KillOnContact, a battle scene) and the methods that led there.
    /// Reports of enemies "killing themselves" can be read off a player's LogOutput.log this way.
    /// </summary>
    [HarmonyPatch]
    internal static class EnemyDeathLog
    {
        static HealthManager lastHm;
        static HitInstance lastHit;
        static int lastFrame = -1;

        /// <summary>Dev: deaths logged as not the player's, and the last one.</summary>
        public static int DevOthers;
        public static string DevLast = "";

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void Took(HealthManager __instance, HitInstance hitInstance)
        {
            lastHm = __instance;
            lastHit = hitInstance;
            lastFrame = Time.frameCount;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void Dying(HealthManager __instance, AttackTypes attackType)
        {
            if (__instance == null || __instance.isDead || TerraMod.Instance == null) return;
            bool hit = lastHm == __instance && lastFrame == Time.frameCount;
            var source = hit ? lastHit.Source : null;
            if (hit && ByPlayer(source)) return;
            // The mod's own kills (a hit through MeleeHits, a debuff, a LAN game's owner) are the player's or another's.
            string caller = Caller(out bool ours);
            if (ours) return;
            DevOthers++;
            var at = __instance.transform.position;
            DevLast = $"{__instance.gameObject.scene.name}/{__instance.name}";
            Plugin.Log.LogWarning($"[怪物死亡] {DevLast} 不是玩家打死的：{attackType}，{(hit ? $"最后一下来自 {Path(source)}（{lastHit.AttackType}，{lastHit.DamageDealt}）" : "没有挨打")}，位置 {at.x:F1},{at.y:F1}，剩血 {__instance.hp}，调用：{caller}");
        }

        static bool ByPlayer(GameObject source)
        {
            if (source == null) return false;
            if (source == TerraHitFx.Source || NailAttackProxy.IsProxy(source)) return true;
            var hero = HeroController.SilentInstance;
            return hero != null && source.transform.IsChildOf(hero.transform);
        }

        static string Path(GameObject go)
        {
            if (go == null) return "无";
            var t = go.transform;
            string p = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return $"{go.scene.name}:{p}";
        }

        /// <summary>The game methods that led to the death (the mod's own frames mark one of its kills).</summary>
        static string Caller(out bool ours)
        {
            ours = false;
            var sb = new System.Text.StringBuilder();
            var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
            if (frames == null) return "?";
            int shown = 0;
            foreach (var f in frames)
            {
                var m = f.GetMethod();
                var type = m?.DeclaringType;
                if (type == null) continue;
                string ns = type.Namespace ?? "";
                if (ns.StartsWith("HarmonyLib")) continue;
                // The mod's frames stay in: one of its own systems (not a hit) killing an enemy is what this is for.
                if (ns.StartsWith("TerraKnight") && (type.Name.StartsWith("MeleeHits") || type.Name.StartsWith("EnemyDebuffs") || ns.StartsWith("TerraKnight.Net"))) ours = true;
                // Every frame is looked at (a hit of the mod's may be deep down); six are shown.
                if (type.Name.StartsWith("<") || shown >= 6) continue;
                if (shown++ > 0) sb.Append(" <- ");
                sb.Append(type.Name).Append('.').Append(m.Name);
            }
            return sb.ToString();
        }
    }
}
