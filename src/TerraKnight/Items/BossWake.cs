using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A sleeping boss woken by any blow from inside its arena, as the nail wakes it from beside it
    /// (World.ArenaGuard keeps blows from outside the arena off it): a gun, a minion or the Zenith's
    /// swords from across the room too. The blow that wakes it takes one point off it and no more,
    /// and nothing it takes counts until its waking is over (its wake and roar played out, the
    /// Knight free again, its shield down): a wake-up isn't a free opening.
    /// A boss asleep is one whose own FSM (on its HealthManager's object) waits in a sleeping state
    /// for what a player's coming or blow does: a state named for sleep, or one shielded until the
    /// Knight comes near (the Gruz Mother's "Invincible", out of its HERO ENTER), with a way out on
    /// HERO ENTER, TAKE DAMAGE or BLOCKED HIT. Bosses a battle wakes in its own order (the Watcher
    /// Knights' WAKE) are left to it. While the Knight has no control (a roar, a scene) no Terraria
    /// blow counts on a boss either.
    /// </summary>
    internal static class BossWake
    {
        public enum Result { Normal, Woke, Held }

        static readonly string[] wakeEvents = { "HERO ENTER", "TAKE DAMAGE", "BLOCKED HIT" };
        static readonly Dictionary<int, float> waking = new Dictionary<int, float>();
        public static int DevWoken, DevHeld;

        static bool Has(HutongGames.PlayMaker.FsmState st, string ev)
        {
            foreach (var t in st.Transitions) if (t.EventName == ev) return true;
            return false;
        }

        /// <summary>The boss's own FSM that has it asleep, if one has.</summary>
        static PlayMakerFSM Sleeper(HealthManager hm)
        {
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                var st = f.Fsm != null ? f.Fsm.ActiveState : null;
                if (st == null) continue;
                string n = st.Name ?? "";
                bool asleep = n.Contains("Sleep") || n.Contains("Dormant") || (hm.IsInvincible && Has(st, "HERO ENTER"));
                if (!asleep) continue;
                foreach (var ev in wakeEvents) if (Has(st, ev)) return f;
            }
            return null;
        }

        static bool IsBoss(HealthManager hm)
        {
            var info = EnemyStats.Of(hm);
            return info != null && info.Boss && !hm.isDead && !EnemyStats.Defeated(hm);
        }

        /// <summary>A boss asleep, waiting to be woken by a blow: fair game for minions and homing shots although shielded.</summary>
        public static bool Asleep(HealthManager hm) => hm != null && IsBoss(hm) && Sleeper(hm) != null;

        /// <summary>
        /// A Terraria blow about to land on an enemy: Woke (it woke a boss: one point and no more),
        /// Held (a boss still waking, or in a scene: nothing), or Normal.
        /// </summary>
        public static Result Hit(HealthManager hm)
        {
            if (hm == null || !IsBoss(hm)) return Result.Normal;
            int id = hm.GetInstanceID();
            if (waking.TryGetValue(id, out float since))
            {
                if (StillWaking(hm, since)) { DevHeld++; return Result.Held; }
                waking.Remove(id);
            }
            var f = Sleeper(hm);
            if (f == null)
            {
                // A roar or a scene with the Knight held: the fight hasn't begun for him.
                if (!GameHooks.PlayerHasControl()) { DevHeld++; return Result.Held; }
                return Result.Normal;
            }
            string from = f.ActiveStateName;
            // As the Knight's coming and his blow would: near (a shield dropped), then struck.
            if (Has(f.Fsm.ActiveState, "HERO ENTER")) f.SendEvent("HERO ENTER");
            var st = f.Fsm.ActiveState;
            if (st != null && Has(st, "TAKE DAMAGE")) f.SendEvent("TAKE DAMAGE");
            else if (st != null && Has(st, "BLOCKED HIT")) f.SendEvent("BLOCKED HIT");
            waking[id] = Time.time;
            DevWoken++;
            Plugin.Log.LogInfo($"[叫醒] {hm.gameObject.scene.name}/{hm.name} 的 {f.FsmName} 在 {from} 被泰拉攻击叫醒（现在 {f.ActiveStateName}）：这一下只掉 1 点，醒完之前不掉血");
            return Result.Woke;
        }

        /// <summary>Its waking not over: under half a second, the Knight held, its shield up, or its FSM in a wake or a roar (20 s at most).</summary>
        static bool StillWaking(HealthManager hm, float since)
        {
            float t = Time.time - since;
            if (t > 20f) return false;
            if (t < 0.5f || !GameHooks.PlayerHasControl() || hm.IsInvincible) return true;
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                string n = f.ActiveStateName ?? "";
                if (n.Contains("Wake") || n.Contains("Roar") || n.Contains("Startle") || n.Contains("Intro") || n.Contains("Title")) return true;
            }
            return false;
        }
    }
}
