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
    /// for what a player's coming or blow does: a state named for sleep (or the Vengefly King's Hanging), or one shielded until the
    /// Knight comes near (the Gruz Mother's "Invincible", out of its HERO ENTER), with a way out on
    /// HERO ENTER, TAKE DAMAGE or BLOCKED HIT. Bosses a battle wakes in its own order (the Watcher
    /// Knights' WAKE) are left to it. While the Knight has no control (a roar, a scene) no Terraria
    /// blow counts on a boss either.
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal static class BossWake
    {
        public enum Result { Normal, Woke, Held }

        static readonly string[] wakeEvents = { "HERO ENTER", "TAKE DAMAGE", "BLOCKED HIT" };
        static readonly Dictionary<int, float> waking = new Dictionary<int, float>();
        public static int DevWoken, DevHeld;
        static readonly Dictionary<int, PlayMakerFSM> introOwners = new Dictionary<int, PlayMakerFSM>();
        static readonly HashSet<int> ready = new HashSet<int>();

        internal static bool Primary(string name) => name == "Control" || name == "Mantis" ||
            name == "FalseyControl" || name == "Movement" || name == "Hornet" || name == "Mage Lord" ||
            name == "Big Fly Control" || name == "Mawlek Control" || name == "Black Knight" ||
            name == "Mossy Control" || name == "Mantis Throne Main" || name == "Beam Miner" ||
            name == "Mimic Spider" || name == "IK Control" || name == "Dung Defender" || name == "Fluke Mother";
        internal static bool Intro(string name) => name != null && (name.Contains("Wake") ||
            name.Contains("Roar") || name.Contains("Startle") || name.Contains("Intro") || name.Contains("Title"));
        internal static bool Combat(string name) => name != null &&
            (name.Contains("Idle") || name == "Active" || name == "Hover");

        static PlayMakerFSM IntroOf(HealthManager hm)
        {
            if (ready.Contains(hm.GetInstanceID())) return null;
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
                if (Primary(f.FsmName) && Intro(f.ActiveStateName)) return f;
            return null;
        }

        // The native dream-entry interaction must remain in charge of this window.
        internal static bool DreamWindow(HealthManager hm)
        {
            if (hm == null || hm.gameObject.scene.name != "Room_Final_Boss_Core") return false;
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Control" && (f.ActiveStateName == "H Collapsed" ||
                    (f.ActiveStateName ?? "").StartsWith("H Scene") || f.ActiveStateName == "H Collapse")) return true;
            return false;
        }

        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static bool NativeHit(HealthManager __instance) => Hit(__instance) == Result.Normal;


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
                // The Vengefly King hangs from the roof until struck (Big Buzzer's Hanging): a minion's quiet blow, telling it
                // nothing, chipped it to death up there.
                bool asleep = n.Contains("Sleep") || n.Contains("Dormant") || n == "Hanging" || (hm.IsInvincible && Has(st, "HERO ENTER"));
                if (!asleep) continue;
                foreach (var ev in wakeEvents) if (Has(st, ev)) return f;
            }
            return null;
        }

        /// <summary>
        /// A boss this game runs. Another player's (a LAN game's puppet: its machines frozen where they stood when it was
        /// taken over, often asleep or in its intro) wakes in its owner's game: blows on it go there as they are, and the
        /// owner's BossWake decides (players' 10-26 "其中一个人打不了": every blow a guest landed was its 'one point' or held).
        /// </summary>
        static bool IsBoss(HealthManager hm)
        {
            var info = EnemyStats.Of(hm);
            return info != null && info.Boss && !hm.isDead && !EnemyStats.Defeated(hm) && (DevOldNet || !Net.Enemies.IsPuppet(hm));
        }

        /// <summary>Dev (suites nbhit_*, TK_NH_BEFORE=1): puppets and teammates' blows as before 10-26.</summary>
        internal static bool DevOldNet;

        /// <summary>
        /// A teammate's blow being dealt here (MeleeHits.ApplyNetHit): the boss's state decides, not whether this game's
        /// Knight is in control (dead and waiting to rise, in a cutscene, on a bench), which held every blow of theirs.
        /// </summary>
        internal static bool ForOther;

        static bool InControl => (ForOther && !DevOldNet) || GameHooks.PlayerHasControl();

        /// <summary>A boss asleep, waiting to be woken by a blow: fair game for minions and homing shots although shielded.</summary>
        public static bool Asleep(HealthManager hm) => hm != null && IsBoss(hm) && Sleeper(hm) != null;

        /// <summary>A boss asleep woken as the Knight's coming and his blow would (a seal's fight begins at once); false when it wasn't asleep.</summary>
        public static bool Rouse(HealthManager hm) => Asleep(hm) && Hit(hm) == Result.Woke;

        /// <summary>
        /// A Terraria blow about to land on an enemy: Woke (it woke a boss: one point and no more),
        /// Held (a boss still waking, or in a scene: nothing), or Normal.
        /// </summary>
        public static Result Hit(HealthManager hm)
        {
            if (hm == null || !IsBoss(hm)) return Result.Normal;
            int id = hm.GetInstanceID();
            var intro = IntroOf(hm);
            if (intro != null && !waking.ContainsKey(id))
            {
                waking[id] = Time.time;
                introOwners[id] = intro;
            }
            if (waking.TryGetValue(id, out float since))
            {
                if (StillWaking(hm, since)) { DevHeld++; return Result.Held; }
                waking.Remove(id);
                introOwners.Remove(id);
                if (Sleeper(hm) == null) ready.Add(id);
            }
            var f = Sleeper(hm);
            if (f == null)
            {
                // A roar or a scene with the Knight held: the fight hasn't begun for him.
                // A nail art takes the Knight's control while it plays (NailArts.Striking): that isn't a scene, its hits count.
                if (!InControl && !NailArts.Striking) { DevHeld++; return Result.Held; }
                return Result.Normal;
            }
            string from = f.ActiveStateName;
            // A boss that wakes only when struck with the Knight beside it (the Crystal Guardian's Hero Range,
            // tested after its BLOCKED HIT) is struck from across the room as from beside it.
            var near = f.FsmVariables.FindFsmBool("Hero Range");
            if (near != null) near.Value = true;
            // As the Knight's coming and his blow would: near (a shield dropped), then struck.
            if (Has(f.Fsm.ActiveState, "HERO ENTER")) f.SendEvent("HERO ENTER");
            var st = f.Fsm.ActiveState;
            if (st != null && Has(st, "TAKE DAMAGE")) f.SendEvent("TAKE DAMAGE");
            else if (st != null && Has(st, "BLOCKED HIT")) f.SendEvent("BLOCKED HIT");
            else if (st != null && Has(st, "TOOK DAMAGE")) f.SendEvent("TOOK DAMAGE");
            waking[id] = Time.time;
            introOwners[id] = f;
            ready.Remove(id);
            DevWoken++;
            Plugin.Log.LogInfo($"[叫醒] {hm.gameObject.scene.name}/{hm.name} 的 {f.FsmName} 在 {from} 被泰拉攻击叫醒（现在 {f.ActiveStateName}）：唤醒和出场期间不掉血");
            return Result.Woke;
        }

        /// <summary>
        /// Its waking is over once its own FSM stands idle (First Idle, Idle): a roar after that is one of its attacks (the
        /// Crystal Guardian's crystal rain), not its waking, and blows count through it.
        /// </summary>
        [HarmonyLib.HarmonyPostfix]
        [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnEnter))]
        static void Entered(HutongGames.PlayMaker.FsmState __instance)
        {
            DevEntered?.Invoke(__instance);
            if (waking.Count == 0 && !Intro(__instance.Name)) return;
            var go = __instance.Fsm != null ? __instance.Fsm.GameObject : null;
            var hm = go != null ? go.GetComponent<HealthManager>() : null;
            if (hm == null || !IsBoss(hm)) return;
            int id = hm.GetInstanceID();
            PlayMakerFSM owner = null;
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
                if (f.Fsm == __instance.Fsm) { owner = f; break; }
            if (owner == null) return;
            if (!ready.Contains(id) && Primary(owner.FsmName) && Intro(__instance.Name))
            {
                if (!waking.ContainsKey(id)) waking[id] = Time.time;
                introOwners[id] = owner;
            }
            if (!waking.ContainsKey(id) || !Combat(__instance.Name) ||
                !introOwners.TryGetValue(id, out var primary) || primary != owner) return;
            waking.Remove(id);
            introOwners.Remove(id);
            ready.Add(id);
            DevAwake++;
        }

        public static int DevAwake;

        /// <summary>Dev: every FSM state entered, as it is (a test watching a prop's FSM run through a hit in one frame).</summary>
        public static System.Action<HutongGames.PlayMaker.FsmState> DevEntered;

        /// <summary>
        /// Its waking not over: under half a second, the Knight held, its shield up, or its FSM in a wake or a roar (20 s at
        /// most). One gone back to sleep (the blow didn't wake it) isn't waking: the next blow tries again.
        /// </summary>
        static bool StillWaking(HealthManager hm, float since)
        {
            float t = Time.time - since;
            if (t > 20f && IntroOf(hm) == null) return false;
            if (Sleeper(hm) != null) return false;
            if (t < 0.5f || !InControl || hm.IsInvincible) return true;
            if (introOwners.TryGetValue(hm.GetInstanceID(), out var owner) && owner != null && Intro(owner.ActiveStateName)) return true;
            return false;
        }
    }
}
