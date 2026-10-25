using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// The new enemy AI (a save's World.EnemyAiMode New; 空洞骑士小怪精英新版AI设计.md): every common enemy and elite with a
    /// <see cref="Profiles"/> entry gets a <see cref="Brain"/> when it starts. Bosses, a boss's own summons (while a boss
    /// lives in the room), LAN puppets and harmless creatures keep their own scripts. Here: the difficulty's numbers, the
    /// attack tokens (how many new moves may wind up or strike at the player at once), the grace after he is hurt, his
    /// dashes (Master's dash punish), the cut hooks, and the FSMs a brain holds still.
    /// </summary>
    [HarmonyPatch]
    internal static class EnemyAi
    {
        public static int DevShots, DevAreas, DevHits, DevCuts, DevShells;
        public static readonly Dictionary<Kind, int> DevMoveCounts = new Dictionary<Kind, int>();

        // Hollow challenges the player; enemy timing and skills follow Master.
        static DifficultyLevel D => GameDifficulty.Current == DifficultyLevel.Hollow ? DifficultyLevel.Master : GameDifficulty.Current;

        /// <summary>The moves this difficulty uses: 【全】 on Normal and Hard, 【专】 from Expert, 【师】 on Master and Hollow.</summary>
        public static Tier TierNow => D >= DifficultyLevel.Master ? Tier.Master : D >= DifficultyLevel.Expert ? Tier.Expert : Tier.All;

        /// <summary>Hook cutting, dash punishing and knocking off mounts: Master and Hollow.</summary>
        public static bool MasterRules => D >= DifficultyLevel.Master;

        /// <summary>How much of the player's coming move an aim takes in (§6).</summary>
        public static float LeadWeight => D switch
        {
            DifficultyLevel.Normal => 0.25f,
            DifficultyLevel.Hard => 0.45f,
            DifficultyLevel.Expert => 0.65f,
            DifficultyLevel.Master => 0.8f,
            _ => 0.9f,
        };

        /// <summary>The new moves' wind-ups, longer on the easy ones.</summary>
        public static float AnticScale => D switch
        {
            DifficultyLevel.Normal => 1.25f,
            DifficultyLevel.Hard => 1.1f,
            DifficultyLevel.Expert => 1f,
            DifficultyLevel.Master => 0.9f,
            _ => 0.85f,
        };

        /// <summary>The new moves' cooldowns and the gap between two of them: sooner on the hard ones.</summary>
        public static float CooldownScale => D switch
        {
            DifficultyLevel.Expert => 0.85f,
            DifficultyLevel.Master => 0.75f,
            DifficultyLevel.Hollow => 0.65f,
            _ => 1f,
        };

        /// <summary>How much further an enemy's body moves (charges, leaps, dives, chases) are taken from.</summary>
        public static float ReachScale => D switch
        {
            DifficultyLevel.Expert => 1.25f,
            DifficultyLevel.Master => 1.4f,
            DifficultyLevel.Hollow => 1.5f,
            _ => 1f,
        };

        /// <summary>How much faster an enemy's own animations run (its own attacks with them): Expert and up, as the bosses' (BossAi.AnimScale).</summary>
        public static float AnimScale => D switch
        {
            DifficultyLevel.Expert => 1.1f,
            DifficultyLevel.Master => 1.15f,
            DifficultyLevel.Hollow => 1.2f,
            _ => 1f,
        };

        /// <summary>The animators of the enemies with a new brain, by instance id (<see cref="AnimationStep"/>).</summary>
        internal static readonly HashSet<int> QuickAnimators = new HashSet<int>();

        [HarmonyPrefix, HarmonyPatch(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.UpdateAnimation))]
        static void AnimationStep(tk2dSpriteAnimator __instance, ref float deltaTime)
        {
            if (QuickAnimators.Count == 0 || !QuickAnimators.Contains(__instance.GetInstanceID())) return;
            deltaTime *= AnimScale;
        }

        /// <summary>The chance a struck enemy with a hard back turns it to the blows (Profile.Reflect).</summary>
        public static float ReflectChance => D switch
        {
            DifficultyLevel.Normal => 0.25f,
            DifficultyLevel.Hard => 0.3f,
            DifficultyLevel.Expert => 0.35f,
            DifficultyLevel.Master => 0.4f,
            _ => 0.45f,
        };

        /// <summary>No wind-up is ever shorter.</summary>
        public const float MinAntic = 0.3f;

        public static int Tokens => D switch
        {
            DifficultyLevel.Normal => 2,
            DifficultyLevel.Hard => 2,
            DifficultyLevel.Expert => 3,
            DifficultyLevel.Master => 4,
            _ => 5,
        };

        static readonly HashSet<Brain> holding = new HashSet<Brain>();
        static readonly List<Brain> gone = new List<Brain>();

        public static bool TryToken(Brain b)
        {
            gone.Clear();
            foreach (var h in holding) if (h == null || !h.isActiveAndEnabled) gone.Add(h);
            foreach (var h in gone) holding.Remove(h);
            if (holding.Contains(b)) return true;
            if (holding.Count >= Tokens) return false;
            holding.Add(b);
            return true;
        }

        public static void ReturnToken(Brain b) => holding.Remove(b);

        /// <summary>The player was just hurt: through his immunity and a fifth of a second after it, no new move starts.</summary>
        public static bool Grace
        {
            get
            {
                var hero = HeroController.SilentInstance;
                if (hero != null && hero.cState.invulnerable) return true;
                return Time.time - Player.TerraHurt.LastHurtAt < 0.87f;
            }
        }

        /// <summary>When the player's last dash ended (Master's dash punish waits 0.12 s, the time to see it).</summary>
        public static float DashEndedAt = -10f;
        static bool wasDashing;

        /// <summary>The FSMs held still by a brain's takeover: their fixed and late updates wait too.</summary>
        internal static readonly HashSet<Fsm> Paused = new HashSet<Fsm>();

        /// <summary>The single player's aim points, by enemy (its own script finds them in place of the Knight).</summary>
        internal static readonly Dictionary<HealthManager, GameObject> Proxies = new Dictionary<HealthManager, GameObject>();

        [HarmonyPrefix, HarmonyPatch(typeof(Fsm), nameof(Fsm.FixedUpdate))]
        static bool HeldFixed(Fsm __instance) => Paused.Count == 0 || !Paused.Contains(__instance);

        [HarmonyPrefix, HarmonyPatch(typeof(Fsm), nameof(Fsm.LateUpdate))]
        static bool HeldLate(Fsm __instance) => Paused.Count == 0 || !Paused.Contains(__instance);

        /// <summary>GetHero in an enemy's script: its aim point (single player; LAN games have Net.EnemyTargets).</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(GetHero), nameof(GetHero.OnEnter))]
        static void Hero(GetHero __instance)
        {
            if (Proxies.Count == 0 || Net.NetSession.Active || __instance.Fsm?.GameObject == null) return;
            var hm = __instance.Fsm.GameObject.GetComponentInParent<HealthManager>();
            if (hm != null && Proxies.TryGetValue(hm, out var p) && p != null) __instance.storeResult.Value = p;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Fsm), nameof(Fsm.GetOwnerDefaultTarget))]
        static void Resolve(Fsm __instance, ref GameObject __result)
        {
            if (Proxies.Count == 0 || Net.NetSession.Active || __instance.GameObject == null) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || __result != hero.gameObject) return;
            var hm = __instance.GameObject.GetComponentInParent<HealthManager>();
            if (hm != null && Proxies.TryGetValue(hm, out var p) && p != null) __result = p;
        }

        /// <summary>The player hurt by an enemy's body or its own attack: that enemy's touch debuff (or its running move's).</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static void Hurt(GameObject go, int hazardType)
        {
            if (hazardType != 1 || go == null || Player.TerraHurt.LastHurtAt != Time.time) return;
            // Another player's enemy, as his game sends it (a LAN game): what its touch carries.
            var ghost = go.GetComponent<NetAi.GhostView>();
            if (ghost != null) { ghost.HurtPlayer(); return; }
            var brain = go.GetComponentInParent<Brain>();
            if (brain != null) brain.HurtPlayer();
        }

        // ------------------------------------------------------------------ who gets a brain

        static readonly HashSet<string> skipScenes = new HashSet<string>();

        [HarmonyPostfix, HarmonyPatch(typeof(HealthManager), "Start")]
        static void Attach(HealthManager __instance)
        {
            // Every enemy's own leaps stop dead on landing, whichever AI the save has (LandingHold).
            try { LandingHold.Attach(__instance); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[敌人AI] 落地定住挂载失败：{e.Message}"); }
            try { Consider(__instance); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[敌人AI] 挂载失败：{e.Message}"); }
        }

        static void Consider(HealthManager hm)
        {
            if (!EnemyAiMode.New || hm == null || TerraMod.Instance == null || hm.isDead) return;
            if (hm.GetComponent<Brain>() != null) return;
            if (TargetDummy.Is(hm) || Net.Enemies.IsPuppet(hm)) return;
            var info = Items.EnemyStats.Of(hm);
            if (info == null || info.Dummy) return;
            // A boss of the new AI: its kit (EnemyAi.Bosses), nothing of the common enemies'.
            if (Bosses.BossAi.Consider(hm, info)) { Runner(); return; }
            if (info.Boss) return;
            var prof = Profiles.For(info.Record);
            if (prof == null) return;
            string scene = hm.gameObject.scene.name;
            // Dreams (the warriors', the Grey Prince's) and the bosses' own rooms keep their fights.
            if (scene.StartsWith("Dream_") || scene.EndsWith("_boss")) return;
            Runner();
            hm.gameObject.AddComponent<Brain>().Init(hm, info, prof);
        }

        /// <summary>A boss is fighting in the room: its summons and the room's others keep their own scripts meanwhile.</summary>
        public static bool BossNear(string scene)
        {
            if (bossCheckAt.TryGetValue(scene, out var c) && Time.time - c.At < 0.5f) return c.Yes;
            bool yes = false;
            foreach (var info in Items.EnemyStats.All)
            {
                if (!info.Boss || info.Hm == null || info.Hm.isDead || !info.Hm.gameObject.activeInHierarchy) continue;
                // A boss by a record only: a part with health of its own and no record (a Durandoo's shell) is none.
                if (string.IsNullOrEmpty(info.Record)) continue;
                if (info.Hm.gameObject.scene.name == scene) { yes = true; DevBossNear = info.Record + "/" + info.Hm.gameObject.name; break; }
            }
            bossCheckAt[scene] = (Time.time, yes);
            return yes;
        }

        /// <summary>Dev: the last boss found by <see cref="BossNear"/> (record/object).</summary>
        public static string DevBossNear;

        static readonly Dictionary<string, (float At, bool Yes)> bossCheckAt = new Dictionary<string, (float, bool)>();

        /// <summary>A cry: the brains of the same room within reach hear where the player is.</summary>
        public static void Alarm(Brain from, float reach)
        {
            foreach (var b in Object.FindObjectsByType<Brain>(FindObjectsSortMode.None))
            {
                if (b == null || b == from || b.Scene != from.Scene || b.Alert) continue;
                if (Vector2.Distance(b.Position, from.Position) <= reach) b.Hear(from.Position);
            }
        }

        /// <summary>A blade, a bite or a web across a hook's rope: the hooks let go (Master).</summary>
        public static void CutHooks(Vector2 at)
        {
            var hook = Player.GrapplingHook.Instance;
            if (hook == null || !Player.GrapplingHook.IsLatched) return;
            var points = new List<Vector2>();
            Player.GrapplingHook.LatchedPoints(points);
            hook.ReleaseAll();
            DevCuts++;
            TerraMod.Instance?.PlaySound("Item_1", 0.4f);
            Plugin.Log.LogInfo("[敌人AI] 钩爪被敌人切断");
        }

        // ------------------------------------------------------------------ each frame

        static GameObject runner;

        static void Runner()
        {
            if (runner != null) return;
            runner = new GameObject("TK Enemy AI");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<Ticker>();
        }

        sealed class Ticker : MonoBehaviour
        {
            void Update()
            {
                var hero = HeroController.SilentInstance;
                var mod = TerraMod.Instance;
                if (hero == null || mod == null) return;
                PlayerDebuffs.Tick(hero, mod);
                // A LAN scene that became this game's: its enemies, started as the others' copies, get their brains now.
                if (Net.NetSession.Active && Time.unscaledTime >= nextAdopt)
                {
                    nextAdopt = Time.unscaledTime + 1f;
                    adopt.Clear();
                    foreach (var info in Items.EnemyStats.All)
                        if (info.Hm != null && !info.Hm.isDead && info.Hm.gameObject.activeInHierarchy && info.Hm.GetComponent<Brain>() == null) adopt.Add(info.Hm);
                    foreach (var hm in adopt) Consider(hm);
                }
                bool dashing = Player.TerraMovement.IsDashing || hero.cState.shadowDashing;
                if (wasDashing && !dashing) DashEndedAt = Time.time;
                wasDashing = dashing;
                // Every half minute of fighting, what the new AI did (LogOutput.log: [敌人AI]).
                if (Time.unscaledTime >= nextLog && Brain.DevMoves != loggedMoves)
                {
                    nextLog = Time.unscaledTime + 30f;
                    loggedMoves = Brain.DevMoves;
                    Plugin.Log.LogInfo($"[敌人AI] 新版AI 挂载 {Brain.DevBrains} 只，出招 {Brain.DevMoves} 次（接管 {Brain.DevSeizes}），出错退回 {Brain.DevFails}，"
                        + $"弹幕 {DevShots}、区域 {DevAreas}、命中 {DevHits}、断钩 {DevCuts}、减益 {PlayerDebuffs.DevApplied}、晶壳反弹 {DevShells}、落地定住 {LandingHold.DevHeld}");
                }
            }

            float nextLog, nextAdopt;
            readonly List<HealthManager> adopt = new List<HealthManager>();
            int loggedMoves;
        }
    }
}
