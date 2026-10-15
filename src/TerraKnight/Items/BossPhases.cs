using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A boss fought in phases one after another has its stand-in's whole life (<see cref="BossStats"/>, tuned) in every
    /// phase, each phase <see cref="Step"/> more than the one before; the bodies up together in a phase share it (the
    /// user's: a phase is a boss of its time, not a slice of one). The phases as the game's own fights run them (their
    /// scenes' FSMs):
    /// <list type="bullet">
    /// <item>The False Knight (the Failed Champion): a round is his armour broken (Check Health refills it each time) and
    /// his head beaten (Health Check); FalseyControl's Stunned Amount counts the heads beaten. Three rounds, and in the
    /// world and the dream a last head with the floor gone (Opened 2). The armour 10/13 of a round, the head 3/13.</item>
    /// <item>The Soul Master (the Soul Tyrant): Mage Lord, then Mage Lord Phase2 once the first's corpse breaks the floor.</item>
    /// <item>The Mantis Lords: Mantis Lord alone, then S1 and S2 together (the Sisters of Battle: S1 to S3).</item>
    /// <item>The Watcher Knights (Battle Control: the next wakes as one falls): Black Knight 1 alone, then two at a time,
    /// 2 and 3, 4 and 5, 6 beside the last of them.</item>
    /// <item>Oro and Mato: Oro alone; at no health he gets up again (P2 HP) as Mato comes in, the two together.</item>
    /// </list>
    /// Each phase but the last ends with healing dropped (<see cref="BossPotions.AtPhaseEnd"/>). A body with two
    /// HealthManagers (the Mantis Lords, the Soul Tyrant's second phase) is its first, the one the hits go to.
    /// </summary>
    internal static class BossPhases
    {
        /// <summary>Each phase's life over the one before's.</summary>
        public const float Step = 0.2f;
        const float Armour = 10f / 13f, Head = 3f / 13f;

        public static float Mult(int phase) => 1f + Step * phase;

        /// <summary>A body's phase and its share of the phase's life; false for a boss not fought in phases.</summary>
        public static bool Of(EnemyStats.Info info, out int phase, out float share)
        {
            phase = 0;
            share = 1f;
            var hm = info?.Hm;
            if (hm == null || info.Record == null) return false;
            string n = BaseName(hm.gameObject.name);
            switch (info.Record)
            {
                case "FalseKnight":
                    // The plate he blocks with (Tinger) takes nothing.
                    phase = Mathf.Clamp(HeadsBeaten(hm), 0, 3);
                    share = n == "Head" ? Head : Armour;
                    return true;
                case "MageLord":
                    phase = n.Contains("Phase2") ? 1 : 0;
                    return true;
                case "MantisLord":
                    if (n == "Mantis Lord") return true;
                    phase = 1;
                    share = 1f / Sisters(info.Scene);
                    return true;
                case "BlackKnight":
                    int k = Number(n);
                    phase = k <= 1 ? 0 : k <= 3 ? 1 : k <= 5 ? 2 : 3;
                    share = k <= 1 ? 1f : 0.5f;
                    return true;
                case "NailBros":
                    if (n == "Mato") { phase = 1; share = 0.5f; return true; }
                    phase = oroUp.Contains(hm.GetInstanceID()) ? 1 : 0;
                    share = phase == 0 ? 1f : 0.5f;
                    return true;
            }
            return false;
        }

        /// <summary>The share of the stand-in's life a body carries now: its phase's measure times its share (1 for a boss not in phases).</summary>
        public static float Factor(EnemyStats.Info info) => Of(info, out int phase, out float share) ? Mult(phase) * share : 1f;

        /// <summary>Its phase's measure (1 for a boss not in phases): how much its stagger is paced by.</summary>
        public static float PhaseFactor(EnemyStats.Info info) => Of(info, out int phase, out _) ? Mult(phase) : 1f;

        /// <summary>The whole fight over the stand-in's life: every phase's measure added up.</summary>
        public static float WholeFactor(string record, string scene)
        {
            switch (record)
            {
                case "FalseKnight":
                    // Godhome's third round ends with his head (Opened 2); the world's and the dream's have a fourth head.
                    return Mult(0) + Mult(1) + Mult(2) + (scene != null && scene.StartsWith("GG_") ? 0f : Head * Mult(3));
                case "MageLord":
                case "MantisLord":
                case "NailBros":
                    return Mult(0) + Mult(1);
                case "BlackKnight":
                    return Mult(0) + Mult(1) + Mult(2) + 0.5f * Mult(3);
            }
            return 1f;
        }

        /// <summary>How many bodies a phase has (a phase ends when they have all fallen).</summary>
        static int Bodies(string record, string scene, int phase)
        {
            switch (record)
            {
                case "MantisLord": return phase == 0 ? 1 : Sisters(scene);
                case "BlackKnight": return phase == 0 || phase == 3 ? 1 : 2;
                default: return 1;
            }
        }

        static int LastPhase(string record) => record == "BlackKnight" ? 3 : record == "FalseKnight" ? 3 : 1;

        static int Sisters(string scene) => scene == "GG_Mantis_Lords_V" ? 3 : 2;

        static string BaseName(string name)
        {
            int cut = name.IndexOf('(');
            return (cut > 0 ? name.Substring(0, cut) : name).Trim();
        }

        static int Number(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            return i < name.Length && int.TryParse(name.Substring(i), out int k) ? k : 1;
        }

        // ---------------------------------------------------------------- what the game counts

        static readonly Dictionary<int, PlayMakerFSM> falsey = new Dictionary<int, PlayMakerFSM>();

        /// <summary>The heads beaten so far (FalseyControl's Stunned Amount on the armour, the head's parent).</summary>
        static int HeadsBeaten(HealthManager hm)
        {
            var body = hm.transform;
            if (BaseName(body.name) == "Head" && body.parent != null) body = body.parent;
            int id = body.gameObject.GetInstanceID();
            if (!falsey.TryGetValue(id, out var fsm) || fsm == null)
            {
                fsm = PlayMakerFSM.FindFsmOnGameObject(body.gameObject, "FalseyControl");
                falsey[id] = fsm;
            }
            var v = fsm != null ? fsm.FsmVariables.GetFsmInt("Stunned Amount") : null;
            return v != null ? v.Value : 0;
        }

        /// <summary>Oro got up again (his health refilled after none: P2 HP): the second phase.</summary>
        static readonly HashSet<int> oroUp = new HashSet<int>(), oroDown = new HashSet<int>();

        // ---------------------------------------------------------------- the end of each phase

        sealed class Body
        {
            public EnemyStats.Info Info;
            public string Fight;
            public int Phase, LastHeads;
            public Vector2 At;
            public bool Down;
        }

        static readonly Dictionary<int, Body> bodies = new Dictionary<int, Body>();
        static readonly Dictionary<string, Dictionary<int, int>> fallen = new Dictionary<string, Dictionary<int, int>>();
        static readonly HashSet<string> ended = new HashSet<string>();
        static string lastScene;

        public static int DevPhaseEnds;
        public static string DevLast = "";

        /// <summary>Call every frame in gameplay: a phase whose bodies have all fallen (or the False Knight's head beaten) drops its healing.</summary>
        public static void Tick()
        {
            var gm = GameManager.UnsafeInstance;
            string scene = gm != null ? gm.sceneName : null;
            if (scene != lastScene)
            {
                lastScene = scene;
                bodies.Clear();
                fallen.Clear();
                ended.Clear();
                oroUp.Clear();
                oroDown.Clear();
                falsey.Clear();
            }
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || !info.Boss || info.Ref == null) continue;
                // A second HealthManager on the same body takes no hits.
                if (hm.GetComponent<HealthManager>() != hm) continue;
                int id = hm.GetInstanceID();
                if (info.Record == "NailBros" && BaseName(hm.gameObject.name) == "Oro")
                {
                    if (hm.hp <= 0) oroDown.Add(id);
                    else if (oroDown.Contains(id) && !oroUp.Contains(id))
                    {
                        oroUp.Add(id);
                        End(info, 0, hm);
                    }
                }
                if (!Of(info, out int phase, out _)) continue;
                if (!bodies.TryGetValue(id, out var b))
                {
                    if (hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy) continue;
                    b = new Body { Info = info, Fight = info.Scene + "/" + info.Record, Phase = phase };
                    bodies[id] = b;
                    if (info.Record == "FalseKnight") b.LastHeads = HeadsBeaten(hm);
                }
                var col = hm.GetComponent<Collider2D>();
                if (hm.gameObject.activeInHierarchy) b.At = col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
                // The False Knight: a head beaten with another round to come.
                if (info.Record == "FalseKnight" && BaseName(hm.gameObject.name) != "Head")
                {
                    int heads = HeadsBeaten(hm);
                    if (heads > b.LastHeads)
                    {
                        for (int k = b.LastHeads; k < heads; k++) End(info, k, hm, b.At);
                        b.LastHeads = heads;
                    }
                }
            }
            // The bodies fallen (dead, or gone with their room's objects).
            foreach (var kv in bodies)
            {
                var b = kv.Value;
                if (b.Down) continue;
                var hm = b.Info.Hm;
                bool down = hm == null || hm.isDead || hm.hp <= 0 && b.Info.Record != "NailBros" && b.Info.Record != "FalseKnight";
                if (!down) continue;
                b.Down = true;
                if (b.Info.Record == "FalseKnight" || b.Info.Record == "NailBros") continue;
                if (!fallen.TryGetValue(b.Fight, out var count)) fallen[b.Fight] = count = new Dictionary<int, int>();
                count.TryGetValue(b.Phase, out int n);
                count[b.Phase] = ++n;
                if (n >= Bodies(b.Info.Record, b.Info.Scene, b.Phase)) End(b.Info, b.Phase, null, b.At);
            }
        }

        static void End(EnemyStats.Info info, int phase, HealthManager hm, Vector2? at = null)
        {
            if (phase >= LastPhase(info.Record)) return;
            string key = info.Scene + "/" + info.Record + "#" + phase;
            if (!ended.Add(key)) return;
            DevPhaseEnds++;
            DevLast = $"{info.Record} 第 {phase + 1} 阶段结束";
            Vector2 spot = at ?? (hm != null ? (Vector2)hm.transform.position : Vector2.zero);
            BossPotions.AtPhaseEnd(info, spot);
        }
    }
}
