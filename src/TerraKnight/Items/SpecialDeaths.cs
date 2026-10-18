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
    /// then, from its health manager in a loaded scene. Through the False Knight's rage his armor takes every
    /// blow but keeps one point of health (<see cref="Raging"/>), so his next stun comes where his FSM looks for it.
    /// </summary>
    [HarmonyPatch]
    internal static class SpecialDeaths
    {
        public static int DevCredited;
        public static string DevLast = "";
        /// <summary>Dev: every state the False Knight's FSM enters, with his armor's and his head's health.</summary>
        public static bool DevTrace;

        /// <summary>Out of the rage: his armor's last point can go again.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnEnter))]
        static void Entering(HutongGames.PlayMaker.FsmState __instance)
        {
            if (raging.Count == 0 || Rage(__instance.Name)) return;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != "FalseyControl" || fsm.GameObject == null) return;
            var hm = fsm.GameObject.GetComponent<HealthManager>();
            if (hm != null) raging.Remove(hm.GetInstanceID());
        }

        /// <summary>Whether the False Knight is in his rage now: his armor keeps its last point of health.</summary>
        public static bool Raging(HealthManager hm) => hm != null && raging.Count > 0 && raging.Contains(hm.GetInstanceID());

        public static int DevHeldAtOne;

        /// <summary>Every blow on the raging armor (a weapon's, a minion's, a burn's) leaves it one point at least.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void KeepLastPoint(HealthManager __instance, ref HitInstance hitInstance)
        {
            if (!Raging(__instance) || hitInstance.DamageDealt < __instance.hp) return;
            hitInstance.DamageDealt = Mathf.Max(0, __instance.hp - 1);
            DevHeldAtOne++;
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
                Plugin.Log.LogInfo($"[假骑士] {Time.time:F1} 进入 {__instance.Name}：盔甲 血 {hm.hp} 无敌 {hm.IsInvincible}{(raging.Contains(hm.GetInstanceID()) ? "（暴怒，留一点血）" : "")}，" +
                    (head != null ? $"头 血 {head.hp} 在场 {head.gameObject.activeInHierarchy} 无敌 {head.IsInvincible} 位置 {(Vector2)head.transform.position:F1}" : "没有头"));
            }
            if (__instance.Name == "Death Anim Start") Credit(hm, "FalseyControl/Death Anim Start");
            // The False Knight's rage after each stun (the leap, the slams) can't be cut short: his third stun breaks the
            // floor only where the rage ends (Rage Check), and a Terraria weapon empties his armor long before a nail could,
            // stunning him again mid-rage, every time, so the fight never ended. He takes the blows meanwhile (their
            // numbers show), his armor down to its last point; the stun comes with the first blow after the rage.
            if (Rage(__instance.Name)) raging.Add(hm.GetInstanceID());
        }

        static bool Rage(string state) => state == "Rage Jump Antic" || state == "Rise 2" || state == "Fall 2" || state == "State 2" || state == "R Attack Antic"
            || state == "Rage Begin" || state == "Rage" || state == "Particle Pause" || state == "Anim End";

        static readonly System.Collections.Generic.HashSet<int> raging = new System.Collections.Generic.HashSet<int>();

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
            ["defeatedMantisLords"] = "MantisLord",
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

        // In these fights, an individual phase/partner can write the shared journal flag.
        // Other fights (e.g. God Tamer's rider yielding when the beast dies) use it as their
        // terminal signal even though another health-bearing object remains alive.
        internal static bool IndividualJournal(string record) => record == "MantisLord" || record == "MageLord"
            || record == "BlackKnight" || record == "NailBros" || record == "Oblobble" || record == "BigBuzzer";

        /// <summary>After a HealthManager.Die: a flag it set, counted if the death itself didn't count the boss.</summary>
        public static void AfterDeath(HealthManager dying)
        {
            if (deferred == null) return;
            var (flag, record) = deferred.Value;
            deferred = null;
            // These multi-body journal kills describe individual bodies, not a whole-fight win.
            // AfterDie already credited it (or waited for the remaining, possibly inactive phases).
            if (flag.StartsWith("killed") && IndividualJournal(record) && dying != null && UI.EnemyHover.RecordOf(dying) == record) return;
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
            if (best != null)
            {
                // FSM-written journal kills also occur between phases. Only explicit beaten
                // flags may bypass the remaining-body check.
                Credit(best, boolName, whole: !boolName.StartsWith("killed") || !IndividualJournal(record));
            }
            else Plugin.Log.LogWarning($"[首领] {boolName} 已设置，但找不到 {record} 的首领对象，没能记入图鉴");
        }

        static void Credit(HealthManager hm, string why, bool whole = true)
        {
            if (hm != null && Bestiary.Credited(hm)) Plugin.Log.LogInfo($"[首领] {hm.gameObject.scene.name}/{hm.name}（{why}）已经记过");
            if (hm == null || Bestiary.Credited(hm)) return;
            Bestiary.Credit(hm, whole: whole);
            if (!Bestiary.Credited(hm)) return;
            EnemyStats.MarkDefeated(hm);
            DevCredited++;
            DevLast = $"{hm.gameObject.scene.name}/{hm.name}（{why}）";
            Plugin.Log.LogInfo($"[首领] {DevLast} 由它自己的死亡流程打倒：记入图鉴、掉落");
            // A LAN game whose fight ran here: the others in the room count it too (their copy is a puppet, Net.Enemies).
            Net.Enemies.SendBeaten(hm.gameObject.scene.name, UI.EnemyHover.RecordOf(hm), why);
        }

        /// <summary>
        /// A LAN game: the boss beaten in the game that ran its fight (Net.Enemies.ReceiveBeaten). Its copy here, a puppet
        /// whose scripts stand still, counted the same way (the book, the treasure bag, a rematch's win); the arena's own
        /// records of the win written in this save (BossArena.WriteBeaten).
        /// </summary>
        internal static void CreditFromNet(string record, string scene, string why)
        {
            HealthManager best = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm == null || hm.gameObject.scene.name != scene || UI.EnemyHover.RecordOf(hm) != record) continue;
                if (best == null || (hm.gameObject.activeInHierarchy && !best.gameObject.activeInHierarchy)) best = hm;
            }
            if (best != null) Credit(best, "联机 " + why);
            BossArena.WriteBeaten(record, scene);
        }
    }
}
