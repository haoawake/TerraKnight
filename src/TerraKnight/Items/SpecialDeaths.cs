using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Bosses whose death is their FSM's and not HealthManager.Die: the False Knight falls when
    /// his head's last stun ends (FalseyControl's "Death Anim Start", falseKnightDefeated) with his
    /// armor's health manager still at its life. Its kill is counted there (the book, the drops),
    /// and the body counts as dead (no health bar over the corpse, the boss room open). Any other
    /// boss the game records as killed (a "killed..." journal flag turning on, or the flag of a fight
    /// won without a death: Hornet's at Kingdom's Edge) without the mod having counted it is counted
    /// then, from its health manager in a loaded scene. The False Knight's armor also holds through
    /// his rage, so his third stun comes where his FSM looks for it.
    /// </summary>
    [HarmonyPatch]
    internal static class SpecialDeaths
    {
        public static int DevCredited;
        public static string DevLast = "";
        /// <summary>Dev: every state the False Knight's FSM enters, with his armor's and his head's health.</summary>
        public static bool DevTrace;

        /// <summary>Out of the rage: the armor given back before the next state's own actions (which may set it themselves).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnEnter))]
        static void Entering(HutongGames.PlayMaker.FsmState __instance)
        {
            if (armored.Count == 0 || Rage(__instance.Name)) return;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != "FalseyControl" || fsm.GameObject == null) return;
            var hm = fsm.GameObject.GetComponent<HealthManager>();
            if (hm != null && armored.Remove(hm.GetInstanceID())) hm.IsInvincible = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnEnter))]
        static void Entered(HutongGames.PlayMaker.FsmState __instance)
        {
            if (TerraMod.Instance == null) return;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != "FalseyControl" || fsm.GameObject == null) return;
            var hm = fsm.GameObject.GetComponent<HealthManager>();
            if (hm == null) return;
            if (DevTrace)
            {
                HealthManager head = null;
                foreach (var c in hm.GetComponentsInChildren<HealthManager>(true)) if (c != hm) head = c;
                Plugin.Log.LogInfo($"[假骑士] {Time.time:F1} 进入 {__instance.Name}：盔甲 血 {hm.hp} 无敌 {hm.IsInvincible}{(armored.Contains(hm.GetInstanceID()) ? "（mod 加的）" : "")}，" +
                    (head != null ? $"头 血 {head.hp} 在场 {head.gameObject.activeInHierarchy} 无敌 {head.IsInvincible} 位置 {(Vector2)head.transform.position:F1}" : "没有头"));
            }
            if (__instance.Name == "Death Anim Start") Credit(hm, "FalseyControl/Death Anim Start");
            // The False Knight's rage after each stun (the leap, the slams) can't be cut short: his
            // armor holds through it. His third stun breaks the floor only where the rage ends (Rage
            // Check); a Terraria weapon empties his armor long before a nail could, stunning him again
            // mid-rage, every time, so the fight never ended.
            if (Rage(__instance.Name) && !hm.IsInvincible)
            {
                hm.IsInvincible = true;
                armored.Add(hm.GetInstanceID());
            }
        }

        static bool Rage(string state) => state == "Rage Jump Antic" || state == "Rise 2" || state == "Fall 2" || state == "State 2" || state == "R Attack Antic"
            || state == "Rage Begin" || state == "Rage" || state == "Particle Pause" || state == "Anim End";

        static readonly System.Collections.Generic.HashSet<int> armored = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// Bosses beaten without a journal kill of their own, by the flag their FSM sets: Hornet at
        /// Kingdom's Edge kneels and leaves, and the three dream bosses (the Failed Champion, the Soul
        /// Tyrant, the Lost Kin) share the journal kill their waking selves wrote already, so it never
        /// turns on again.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, string> beatenFlags = new System.Collections.Generic.Dictionary<string, string>
        {
            ["hornet1Defeated"] = "Hornet", ["hornetOutskirtsDefeated"] = "Hornet",
            ["falseKnightDreamDefeated"] = "FalseKnight", ["mageLordDreamDefeated"] = "MageLord", ["infectedKnightDreamDefeated"] = "InfectedKnight",
        };

        /// <summary>The journal's kill flag set (EnemyDeathEffects.RecordKillForJournal, or a boss FSM's SetPlayerDataBool), or a beaten flag.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetBool))]
        static void JournalKill(PlayerData __instance, string boolName, bool value)
        {
            if (!value || TerraMod.Instance == null || boolName == null) return;
            string record = boolName.StartsWith("killed") && boolName.Length > 6 ? boolName.Substring(6) : beatenFlags.TryGetValue(boolName, out var r) ? r : null;
            // A rematch's win counts as any (its loot, its book entry); the visit's own writing of the records doesn't.
            if (record == null || BossArena.Writing || __instance.GetBool(boolName)) return;
            if (!EnemyDrops.IsBoss(record)) return;
            // Set within a death (EnemyDeathEffects in HealthManager.Die): that death counts it; looked at again after it.
            if (Bestiary.DyingNow != null)
            {
                deferred = (boolName, record);
                return;
            }
            CreditByFlag(boolName, record);
        }

        static (string Flag, string Record)? deferred;

        /// <summary>After a HealthManager.Die: a flag it set, counted if the death itself didn't count the boss.</summary>
        public static void AfterDeath(HealthManager dying)
        {
            if (deferred == null) return;
            var (flag, record) = deferred.Value;
            deferred = null;
            // Each Oblobble writes killedOblobble from its own Die, including the first
            // partner's death. Bestiary.AfterDie already waited for the surviving partner;
            // do not turn that individual journal flag into a whole-fight win. Other bosses'
            // terminal flags (e.g. the Soul Master's second phase) still finish their fight.
            if (record == "Oblobble" && dying != null && UI.EnemyHover.RecordOf(dying) == record) return;
            CreditByFlag(flag, record);
        }

        static void CreditByFlag(string boolName, string record)
        {
            // Its health manager: the nearest one of that record in a loaded scene, switched off or not (a dream's
            // second phase is put away by the time its flag is set); one up and about first.
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            HealthManager best = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm == null || !hm.gameObject.scene.isLoaded || UI.EnemyHover.RecordOf(hm) != record) continue;
                // Its death counted already (a phase of it went down by HealthManager.Die): not twice.
                if (Bestiary.Credited(hm)) return;
                bool up = hm.gameObject.activeInHierarchy, bestUp = best != null && best.gameObject.activeInHierarchy;
                if (best == null || (up && !bestUp) || (up == bestUp && Vector2.Distance(hm.transform.position, at) < Vector2.Distance(best.transform.position, at))) best = hm;
            }
            if (best != null) Credit(best, boolName);
            else Plugin.Log.LogWarning($"[首领] {boolName} 已设置，但找不到 {record} 的首领对象，没能记入图鉴");
        }

        static void Credit(HealthManager hm, string why)
        {
            if (hm != null && Bestiary.Credited(hm)) Plugin.Log.LogInfo($"[首领] {hm.gameObject.scene.name}/{hm.name}（{why}）已经记过");
            if (hm == null || Bestiary.Credited(hm)) return;
            EnemyStats.MarkDefeated(hm);
            Bestiary.Credit(hm, whole: true);
            DevCredited++;
            DevLast = $"{hm.gameObject.scene.name}/{hm.name}（{why}）";
            Plugin.Log.LogInfo($"[首领] {DevLast} 由它自己的死亡流程打倒：记入图鉴、掉落");
        }
    }
}
