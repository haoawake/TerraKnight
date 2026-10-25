using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The new AI's bosses (a save's World.EnemyAiMode New; 空洞骑士首领新版AI设计.md): every boss with a kit (<see cref="Kits"/>)
    /// gets one when it starts, on the game that runs it (not a LAN puppet). Here: the difficulty's numbers (§1.1-1.3), the
    /// rooms with a fight on (whose scripts and thrown things go through the player's blocks: BossQueries, World.BossPlatforms),
    /// and the waits of the bosses' own scripts made shorter (<see cref="Tempo"/>).
    /// </summary>
    [HarmonyPatch]
    internal static class BossAi
    {
        public static int DevKits, DevFails, DevLocks, DevMarks, DevAntiAir, DevUnstuck, DevMoves;
        /// <summary>Dev: a kit's steps run in its boss's states, its own new states entered, its branches taken.</summary>
        public static int DevHooks, DevNewStates, DevBranches;

        // Hollow challenges the player; enemy timing and skills follow Master.
        static DifficultyLevel D => GameDifficulty.Current == DifficultyLevel.Hollow ? DifficultyLevel.Master : GameDifficulty.Current;

        /// <summary>The waits between moves (§1.3): the difficulty's share, before the boss's own pace.</summary>
        public static float TempoScale => D switch
        {
            DifficultyLevel.Normal => 0.9f,
            DifficultyLevel.Hard => 0.84f,
            DifficultyLevel.Expert => 0.78f,
            DifficultyLevel.Master => 0.72f,
            _ => 0.66f,
        };

        /// <summary>How fast a boss's own animations run, its wind-ups and recoveries with them (§1.3).</summary>
        public static float AnimScale => D switch
        {
            DifficultyLevel.Normal => 1.1f,
            DifficultyLevel.Hard => 1.15f,
            DifficultyLevel.Expert => 1.2f,
            DifficultyLevel.Master => 1.25f,
            _ => 1.3f,
        };

        /// <summary>The rank a boss starts the fight at (§1.4): its second rank's moves from the start on Expert and up.</summary>
        public static int RankFloor => D >= DifficultyLevel.Expert ? 1 : 0;

        /// <summary>A warning's length over its base (§1.2), as the new moves' wind-ups: shorter than the common enemies'.</summary>
        public static float WarnScale => EnemyAi.AnticScale * 0.85f;

        /// <summary>No warning of a locked or ranged move is shorter; a close wind-up, <see cref="MinAntic"/>.</summary>
        public const float MinWarn = 0.4f, MinAntic = 0.3f;

        /// <summary>How much of the player's coming move a lead takes in (§1.1, as the common enemies').</summary>
        public static float LeadWeight => EnemyAi.LeadWeight;

        /// <summary>A "sometimes" lead's chance (§1.1).</summary>
        public static float LeadChance => D switch
        {
            DifficultyLevel.Normal => 0.3f,
            DifficultyLevel.Hard => 0.4f,
            DifficultyLevel.Expert => 0.5f,
            DifficultyLevel.Master => 0.6f,
            _ => 0.7f,
        };

        /// <summary>How many locked or led strikes may be live at once (§1.6).</summary>
        public static int MaxStrikes => D >= DifficultyLevel.Master ? 4 : 3;

        /// <summary>Master and Hollow (the hooks let go and so on, as the common enemies').</summary>
        public static bool MasterRules => EnemyAi.MasterRules;

        // ------------------------------------------------------------------ the kits

        /// <summary>Each boss's kit by its record (UI.EnemyHover.RecordOf); its variants (a dream's, Godhome's) tell themselves apart by scene.</summary>
        static readonly Dictionary<string, System.Type> kits = new Dictionary<string, System.Type>();

        /// <summary>A kit for a record (the kit files register theirs: Kits.Register).</summary>
        internal static void Register(string record, System.Type kit) => kits[record] = kit;

        static bool registered;

        /// <summary>
        /// A boss starting (EnemyAi.Consider, before the common enemies' brains): its kit, if it has one. True when it is a
        /// boss of the new AI (nothing else of the new AI goes on it).
        /// </summary>
        internal static bool Consider(HealthManager hm, Items.EnemyStats.Info info)
        {
            if (!registered) { registered = true; Kits.RegisterAll(); BossQueries.Install(); }
            if (info == null || !kits.TryGetValue(info.Record ?? "", out var type)) return false;
            if (Net.Enemies.IsPuppet(hm)) return true;
            if (hm.GetComponent<BossKit>() != null) return true;
            try
            {
                var kit = (BossKit)hm.gameObject.AddComponent(type);
                kit.Begin(hm, info);
                DevKits++;
            }
            catch (System.Exception e)
            {
                DevFails++;
                Plugin.Log.LogWarning($"[首领AI] {info.Record} 挂载失败，按原版打：{e}");
            }
            return true;
        }

        // ------------------------------------------------------------------ fights

        static readonly Dictionary<int, int> fights = new Dictionary<int, int>();
        static readonly Dictionary<string, int> fightNames = new Dictionary<string, int>();

        /// <summary>A kit is up in some room.</summary>
        public static bool AnyFight => fights.Count > 0;

        /// <summary>A kit is up in this scene (by its handle: no name read, no garbage).</summary>
        public static bool FightIn(UnityEngine.SceneManagement.Scene scene) => fights.Count > 0 && fights.ContainsKey(scene.handle);

        public static bool FightIn(string scene) => fightNames.Count > 0 && scene != null && fightNames.ContainsKey(scene);

        /// <summary>
        /// A thing of a fight's: in a scene with a kit up, or in the room its boss scene is loaded into (what a boss's script
        /// creates goes into the room's scene; both count while it is up), or one of the game's pooled things out now (its
        /// waves, spurts and shots live in the pool's own scene) that isn't the player's (a spell hurts enemies only). By
        /// scene handle: no name read, no garbage.
        /// </summary>
        public static bool OfFight(GameObject go)
        {
            if (fights.Count == 0 || go == null) return false;
            if (fights.ContainsKey(go.scene.handle)) return true;
            var root = go.transform.root.gameObject;
            if (!ObjectPool.IsSpawned(root)) return false;
            return root.GetComponentInChildren<DamageEnemies>(true) == null || root.GetComponentInChildren<DamageHero>(true) != null;
        }

        /// <summary>The room scene each kit's boss scene is loaded into ("X_boss" in "X"), counted with it.</summary>
        static readonly Dictionary<BossKit, (int Handle, string Name)> roomOf = new Dictionary<BossKit, (int, string)>();

        internal static void Up(BossKit k)
        {
            var s = k.gameObject.scene;
            Count(s.handle, s.name, 1);
            if (s.name != null && s.name.EndsWith("_boss", System.StringComparison.OrdinalIgnoreCase))
            {
                var room = UnityEngine.SceneManagement.SceneManager.GetSceneByName(s.name.Substring(0, s.name.Length - 5));
                if (room.IsValid() && room.handle != s.handle)
                {
                    roomOf[k] = (room.handle, room.name);
                    Count(room.handle, room.name, 1);
                }
            }
        }

        internal static void Down(BossKit k, UnityEngine.SceneManagement.Scene s, string name)
        {
            Count(s.handle, name, -1);
            if (roomOf.TryGetValue(k, out var room))
            {
                roomOf.Remove(k);
                Count(room.Handle, room.Name, -1);
            }
        }

        static void Count(int handle, string name, int by)
        {
            int n = (fights.TryGetValue(handle, out int was) ? was : 0) + by;
            if (n <= 0) fights.Remove(handle); else fights[handle] = n;
            if (name == null) return;
            int m = (fightNames.TryGetValue(name, out int wasNamed) ? wasNamed : 0) + by;
            if (m <= 0) fightNames.Remove(name); else fightNames[name] = m;
        }

        // ------------------------------------------------------------------ the bosses' own waits, shorter

        /// <summary>
        /// The waits a kit times (its idle and recovery states'), with the kit whose pace they keep, or a factor of their own
        /// (over 0: that, a longer wait too).
        /// </summary>
        static readonly Dictionary<FsmStateAction, (BossKit Kit, float Factor)> tempo = new Dictionary<FsmStateAction, (BossKit, float)>();

        /// <summary>A wait of a boss's script, now its kit's pace (BossKit.Tempo), or a fixed factor of it.</summary>
        internal static void Tempo(FsmStateAction a, BossKit kit, float factor = 0f)
        {
            if (a is Wait || a is WaitRandom) tempo[a] = (kit, factor);
        }

        static float FactorOf((BossKit Kit, float Factor) t) => Mathf.Clamp(t.Factor > 0f ? t.Factor : t.Kit.TempoNow, 0.2f, 3f);

        internal static void Forget(BossKit kit)
        {
            gone.Clear();
            foreach (var kv in tempo) if (kv.Value.Kit == kit || kv.Value.Kit == null) gone.Add(kv.Key);
            foreach (var a in gone) tempo.Remove(a);
            goneAnims.Clear();
            foreach (var kv in quick) if (kv.Value == kit || kv.Value == null) goneAnims.Add(kv.Key);
            foreach (var id in goneAnims) quick.Remove(id);
        }

        /// <summary>The boss's own animators (by instance id), run at <see cref="AnimScale"/> while its kit is active.</summary>
        static readonly Dictionary<int, BossKit> quick = new Dictionary<int, BossKit>();
        static readonly List<int> goneAnims = new List<int>();

        internal static void Quicken(BossKit kit, GameObject go)
        {
            var a = go != null ? go.GetComponent<tk2dSpriteAnimator>() : null;
            if (a != null) quick[a.GetInstanceID()] = kit;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.UpdateAnimation))]
        static void AnimationStep(tk2dSpriteAnimator __instance, ref float deltaTime)
        {
            if (quick.Count == 0 || !quick.TryGetValue(__instance.GetInstanceID(), out var kit) || kit == null || !kit.Active) return;
            deltaTime *= AnimScale * kit.AnimPace;
        }

        static readonly List<FsmStateAction> gone = new List<FsmStateAction>();
        static readonly AccessTools.FieldRef<Wait, float> waitTimer = AccessTools.FieldRefAccess<Wait, float>("timer");
        static readonly AccessTools.FieldRef<Wait, float> waitStart = AccessTools.FieldRefAccess<Wait, float>("startTime");
        static readonly AccessTools.FieldRef<WaitRandom, float> randomTime = AccessTools.FieldRefAccess<WaitRandom, float>("time");

        [HarmonyPostfix, HarmonyPatch(typeof(Wait), nameof(Wait.OnEnter))]
        static void WaitStarts(Wait __instance)
        {
            if (tempo.Count == 0 || !tempo.TryGetValue(__instance, out var t) || t.Kit == null || !t.Kit.Active || __instance.Finished) return;
            // Started part of the way through (a shorter wait), or before its start (a longer one).
            float skip = __instance.time.Value * (1f - FactorOf(t));
            waitTimer(__instance) = skip;
            waitStart(__instance) -= skip;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(WaitRandom), nameof(WaitRandom.OnEnter))]
        static void RandomStarts(WaitRandom __instance)
        {
            if (tempo.Count == 0 || !tempo.TryGetValue(__instance, out var t) || t.Kit == null || !t.Kit.Active || __instance.Finished) return;
            randomTime(__instance) *= FactorOf(t);
        }

        // ------------------------------------------------------------------ the log

        static float nextLog;
        static int loggedMoves = -1;

        /// <summary>Every half minute of fighting, what the bosses' kits did (LogOutput.log: [首领AI]).</summary>
        internal static void Log()
        {
            if (Time.unscaledTime < nextLog || DevMoves == loggedMoves) return;
            nextLog = Time.unscaledTime + 30f;
            loggedMoves = DevMoves;
            Plugin.Log.LogInfo($"[首领AI] 挂载 {DevKits} 个（失败 {DevFails}），新招 {DevMoves} 次，锁定 {DevLocks}、预警 {DevMarks}、对空 {DevAntiAir}、脱困 {DevUnstuck}，"
                + $"穿过玩家方块的射线 {BossQueries.DevFiltered}、碰撞体 {BossPlatforms.DevThrown}");
        }
    }
}
