using System.Collections.Generic;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Creative: the monster book's spawn button on a boss. The player is taken to the boss's own
    /// arena and the fight happens as the game has it, its entrance, its cutscenes and its words
    /// included. What the game keeps of having beaten it (a PlayerData flag, a scene's saved state:
    /// what bossgates found deciding each fight) is set back to unbeaten for the visit, with what the
    /// fight needs (the Dream Nail for a warrior's ghost, the Grimmchild for the Troupe Master);
    /// leaving the arena puts all of it back as it was, but a win the game wrote down stays. A dream's
    /// or Godhome's fight, which ends by sending the player back, sends them back where they were.
    /// </summary>
    [HarmonyPatch]
    internal static class BossArena
    {
        enum Kind { World, Dream, Godhome }

        sealed class Arena
        {
            public string Scene, Gate;
            public Kind Kind;
            /// <summary>The game's record of the boss beaten, set to unbeaten for the fight (a win sets it again).</summary>
            public (string Name, bool Value)[] Beaten = new (string, bool)[0];
            public (string Name, int Value)[] BeatenInts = new (string, int)[0];
            /// <summary>Scene states ("scene/id") the fight is skipped by once set: unset for it.</summary>
            public string[] States = new string[0];
            /// <summary>What the fight needs of the player, given for the visit only.</summary>
            public (string Name, bool Value)[] Needs = new (string, bool)[0];
            public (string Name, int Value)[] NeedInts = new (string, int)[0];
        }

        static Arena W(string scene, string gate) => new Arena { Scene = scene, Gate = gate, Kind = Kind.World };
        static Arena D(string scene) => new Arena { Scene = scene, Gate = "door1", Kind = Kind.Dream };
        static Arena G(string scene) => new Arena { Scene = scene, Gate = "door_dreamEnter", Kind = Kind.Godhome };
        static (string, bool) F(string name) => (name, false);
        static (string, bool) T(string name) => (name, true);

        /// <summary>By the book's key. The warriors' ghosts count 2 once absorbed; the Dream Nail shows them.</summary>
        static readonly Dictionary<string, Arena> arenas = new Dictionary<string, Arena>
        {
            ["FalseKnight"] = Set(W("Crossroads_10", "left1"), beaten: new[] { F("falseKnightDefeated") }, states: new[] { "Crossroads_10/Battle Scene" }),
            ["BigFly"] = Set(W("Crossroads_04", "right1"), states: new[] { "Crossroads_04/Battle Scene" }),
            ["Mawlek"] = Set(W("Crossroads_09", "right1"), states: new[] { "Crossroads_09/Battle Scene", "Crossroads_09/Mawlek Body" }),
            ["Hornet"] = Set(W("Fungus1_04", "left1"), beaten: new[] { F("hornet1Defeated") }),
            ["MegaMossCharger"] = Set(W("Fungus1_29", "left1"), beaten: new[] { F("megaMossChargerDefeated"), F("megaMossChargerEncountered") }),
            ["GhostAladar"] = Set(W("Cliffs_02", "left1"), beatenInts: new[] { ("aladarSlugDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            ["GhostNoEyes"] = Set(W("Fungus1_35", "left1"), beatenInts: new[] { ("noEyesDefeated", 0) }, needs: new[] { T("hasDreamNail"), T("hasLantern") }),
            ["DungDefender"] = Set(W("Waterways_05", "bot1"), beaten: new[] { F("defeatedDungDefender") }),
            ["TraitorLord"] = Set(W("Fungus3_23", "left1"), beaten: new[] { F("killedTraitorLord") }, needs: new[] { T("hasShadowDash") }),
            ["MantisLord"] = Set(W("Fungus2_15", "top1"), beaten: new[] { F("defeatedMantisLords") }),
            ["MageLord"] = Set(W("Ruins1_24", "left1"), beaten: new[] { F("mageLordDefeated"), F("mageLordEncountered") }),
            ["MegaJellyfish"] = Set(W("Fungus3_archive_02", "top1"), beaten: new[] { F("defeatedMegaJelly") }),
            ["FlukeMother"] = Set(W("Waterways_12", "right1"), beaten: new[] { F("flukeMotherDefeated") }),
            ["MegaBeamMiner"] = Set(W("Mines_18", "left1"), beaten: new[] { F("killedMegaBeamMiner") }, states: new[] { "Mines_18/Battle Scene" }),
            ["GhostHu"] = Set(W("Fungus2_32", "left1"), beatenInts: new[] { ("elderHuDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            ["GhostMarmu"] = Set(W("Fungus3_40", "right1"), beatenInts: new[] { ("mumCaterpillarDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            // The Watcher Knights wake only for one who comes down from the Watcher's room (doorTrigger top1).
            ["BlackKnight"] = Set(W("Ruins2_03", "top1"), states: new[] { "Ruins2_03/Battle Control" }),
            ["HiveKnight"] = Set(W("Hive_05", "left1"), beaten: new[] { F("killedHiveKnight") }),
            ["JarCollector"] = Set(W("Ruins2_11", "right1"), beaten: new[] { F("collectorDefeated") }),
            ["MimicSpider"] = Set(W("Deepnest_32", "left1"), beaten: new[] { F("encounteredMimicSpider") }, states: new[] { "Deepnest_32/Battle Scene" }),
            ["GhostGalien"] = Set(W("Deepnest_40", "right1"), beatenInts: new[] { ("galienDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            ["GhostXero"] = Set(W("RestingGrounds_02", "left1"), beatenInts: new[] { ("xeroDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            ["Hornet@4"] = Set(W("Deepnest_East_Hornet", "left1"), beaten: new[] { F("hornetOutskirtsDefeated") }),
            ["GhostMarkoth"] = Set(W("Deepnest_East_10", "left1"), beatenInts: new[] { ("markothDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            ["InfectedKnight"] = Set(W("Abyss_19", "right1"), beaten: new[] { F("killedInfectedKnight"), F("infectedKnightEncountered") }, states: new[] { "Abyss_19/Camera Locks Boss" }),
            // The Troupe Master is in his tent for one who carries the Grimmchild with the flames gathered (Grimm_Main_Tent's BossLoader).
            ["Grimm"] = Set(W("Grimm_Main_Tent", "left1"), beaten: new[] { F("killedGrimm") }, needs: new[] { T("metGrimm"), T("equippedCharm_40") },
                            needInts: new[] { ("flamesCollected", 3), ("flamesRequired", 3), ("grimmChildLevel", 2) }),
            ["HollowKnight"] = Set(W("Room_Final_Boss_Core", "left1"), beaten: new[] { F("unchainedHollowKnight") }),
            ["FalseKnight@5"] = Set(D("Dream_01_False_Knight"), states: new[] { "Dream_01_False_Knight/Battle Scene" }),
            ["MageLord@5"] = D("Dream_02_Mage_Lord"),
            ["InfectedKnight@5"] = Set(D("Dream_03_Infected_Knight"), states: new[] { "Dream_03_Infected_Knight/Camera Locks Boss" }),
            ["WhiteDefender"] = D("Dream_04_White_Defender"),
            ["GreyPrince"] = D("Dream_Mighty_Zote"),
            ["NightmareGrimm"] = D("Grimm_Nightmare"),
            ["FinalBoss"] = D("Dream_Final_Boss"),
            ["HollowKnightPrime"] = G("GG_Hollow_Knight"),
            ["NailBros"] = G("GG_Nailmasters"),
            ["Paintmaster"] = G("GG_Painter"),
            ["Nailsage"] = G("GG_Sly"),
            ["Oblobble"] = G("GG_Oblobbles"),
            ["LobsterLancer"] = G("GG_God_Tamer"),
        };

        static Arena Set(Arena a, (string, bool)[] beaten = null, (string, int)[] beatenInts = null, string[] states = null,
                         (string, bool)[] needs = null, (string, int)[] needInts = null)
        {
            if (beaten != null) a.Beaten = beaten;
            if (beatenInts != null) a.BeatenInts = beatenInts;
            if (states != null) a.States = states;
            if (needs != null) a.Needs = needs;
            if (needInts != null) a.NeedInts = needInts;
            return a;
        }

        /// <summary>Dev: every world boss beaten, as the game records it (each arena's beaten flags the other way from a visit's).</summary>
        internal static void DevAllBeaten()
        {
            var pd = PlayerData.instance;
            foreach (var a in arenas.Values)
                if (a.Kind == Kind.World)
                    foreach (var (name, v) in a.Beaten) pd.SetBool(name, !v);
        }

        /// <summary>Dev: a world arena's way in by the book's key ("Hornet@4"), and the flags its fight needs of the player.</summary>
        internal static bool DevWorldArena(string key, out string gate, out (string Name, bool Value)[] needs)
        {
            gate = null;
            needs = null;
            if (!arenas.TryGetValue(key, out var a) || a.Kind != Kind.World) return false;
            gate = a.Gate;
            needs = a.Needs;
            return true;
        }

        /// <summary>The fight being visited: what was there before, to put back.</summary>
        sealed class Visit
        {
            public string Key;
            public Arena Arena;
            public readonly Dictionary<string, bool> Bools = new Dictionary<string, bool>();
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public readonly Dictionary<string, bool> States = new Dictionary<string, bool>();
            /// <summary>The values during the fight, held while a save writes the ones from before.</summary>
            public readonly Dictionary<string, bool> Now = new Dictionary<string, bool>(), NowStates = new Dictionary<string, bool>();
            public readonly Dictionary<string, int> NowInts = new Dictionary<string, int>();
            public bool Arrived;
        }

        static Visit visit;

        /// <summary>A creative challenge is on: its records are being set back and forth (SpecialDeaths leaves them be).</summary>
        public static bool Visiting => visit != null;
        /// <summary>Where a dream's or Godhome's fight sends the player back to: the scene and the spot they left from.</summary>
        static string returnScene;
        static Vector2 returnSpot;

        public static string DevLast = "";
        public static bool DevActive => visit != null;

        public static bool Has(string key) => key != null && arenas.ContainsKey(key);

        /// <summary>Takes the player to the boss's arena for its fight; false when this boss has none (a copy is spawned then).</summary>
        public static bool Fight(string key)
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            var pd = PlayerData.instance;
            if (!Has(key) || gm == null || hero == null || pd == null || gm.IsInSceneTransition) return false;
            if (visit != null) Restore(keepWins: true);
            var a = arenas[key];
            var v = new Visit { Key = key, Arena = a };
            foreach (var (n, val) in a.Beaten) { v.Bools[n] = pd.GetBool(n); pd.SetBool(n, val); }
            foreach (var (n, val) in a.Needs) { if (!v.Bools.ContainsKey(n)) v.Bools[n] = pd.GetBool(n); pd.SetBool(n, val); }
            foreach (var (n, val) in a.BeatenInts) { v.Ints[n] = pd.GetInt(n); pd.SetInt(n, val); }
            foreach (var (n, val) in a.NeedInts) { if (!v.Ints.ContainsKey(n)) v.Ints[n] = pd.GetInt(n); pd.SetInt(n, val); }
            foreach (var s in a.States)
            {
                var data = StateOf(s);
                var found = SceneData.instance.FindMyState(data);
                v.States[s] = found != null && found.activated;
                data.activated = false;
                SceneData.instance.SaveMyState(data);
            }
            visit = v;
            if (a.Kind != Kind.World)
            {
                // Sent back by the dream's own ending to where the player stands now.
                var cell = World.CellGrid.WorldToCell(hero.transform.position);
                var region = World.TerraWorld.RegionAt(cell.x, cell.y);
                returnScene = region?.SceneName ?? gm.sceneName;
                returnSpot = (Vector2)hero.transform.position - (region != null ? (Vector2)region.CellOffset * World.CellGrid.CellSize : Vector2.zero);
                pd.dreamReturnScene = returnScene;
                pd.bossReturnEntryGate = "";
            }
            if (a.Kind == Kind.Godhome)
                BossSceneController.SetupEvent = self =>
                {
                    self.BossLevel = 0;
                    self.DreamReturnEvent = "DREAM RETURN";
                };
            InventoryGui.Open = false;
            DevLast = $"{key} → {a.Scene}（{a.Gate}）";
            Plugin.Log.LogInfo($"[图鉴] 挑战 {DevLast}：开打前的记录 {string.Join("，", Describe(v))}");
            // An arena laid over its room only for one who came in by its door (the Watcher Knights':
            // SceneAdditiveLoadConditional.doorTrigger against the last gate walked through) has it walked.
            if (a.Kind == Kind.World) TransitionPoint.lastEntered = a.Gate;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = a.Scene, EntryGateName = a.Gate, HeroLeaveDirection = GatePosition.unknown, EntryDelay = 0f,
                WaitForSceneTransitionCameraFade = true,
                Visualization = a.Kind == Kind.World ? GameManager.SceneLoadVisualizations.Default : GameManager.SceneLoadVisualizations.Dream,
            });
            return true;
        }

        static PersistentBoolData StateOf(string s)
        {
            int cut = s.IndexOf('/');
            return new PersistentBoolData { sceneName = s.Substring(0, cut), id = s.Substring(cut + 1), activated = false, semiPersistent = false };
        }

        static IEnumerable<string> Describe(Visit v)
        {
            foreach (var kv in v.Bools) yield return $"{kv.Key}={kv.Value}";
            foreach (var kv in v.Ints) yield return $"{kv.Key}={kv.Value}";
            foreach (var kv in v.States) yield return $"{kv.Key}={kv.Value}";
        }

        /// <summary>The scenes the fight goes on in: the arena, the scene it adds for the boss, the dream.</summary>
        static bool InFight(string scene) =>
            visit != null && scene != null && (scene == visit.Arena.Scene || scene.StartsWith(visit.Arena.Scene + "_boss"));

        /// <summary>A scene entered: the arena (the fight is on), or away from it (all put back), or back from a dream (to the spot left).</summary>
        public static void OnSceneReady(GameManager gm)
        {
            if (gm == null) return;
            if (visit != null)
            {
                if (InFight(gm.sceneName))
                {
                    if (!visit.Arrived && visit.Arena.Kind != Kind.World) TerraMod.Instance?.StartCoroutine(FadeIn());
                    visit.Arrived = true;
                }
                else if (visit.Arrived || !gm.IsInSceneTransition)
                {
                    Restore(keepWins: true);
                    // Left some other way (a death, a door): no spot to go back to later.
                    if (gm.sceneName != returnScene) returnScene = null;
                }
            }
            if (returnScene != null && gm.sceneName == returnScene && visit == null)
            {
                var hero = HeroController.SilentInstance;
                if (hero != null)
                {
                    hero.transform.position = new Vector3(returnSpot.x, returnSpot.y, hero.transform.position.z);
                    var rb = hero.GetComponent<Rigidbody2D>();
                    if (rb != null) { rb.position = returnSpot; rb.linearVelocity = Vector2.zero; }
                    Plugin.Log.LogInfo($"[图鉴] 挑战结束，回到 {returnScene} {returnSpot:F1}");
                }
                returnScene = null;
            }
        }

        public static int DevFadeKicks;

        /// <summary>
        /// A dream's and Godhome's way in fades the screen in itself (their gates' customFade: the
        /// dream nail's or the statue's own sequence does it), which isn't run when the book takes
        /// the player there: the screen would stay black. Faded in here when it is still dark.
        /// </summary>
        static System.Collections.IEnumerator FadeIn()
        {
            float until = Time.time + 0.6f;
            while (Time.time < until) yield return null;
            var gm = GameManager.UnsafeInstance;
            var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
            if (gm == null || fade == null) yield break;
            string state = fade.ActiveStateName;
            if (state == "Normal" || state == "FadeIn") yield break;
            gm.FadeSceneIn();
            DevFadeKicks++;
            Plugin.Log.LogInfo($"[图鉴] 挑战：{gm.sceneName} 的入口不自己淡入（镜头停在 {state}），补一次淡入");
        }

        /// <summary>Every frame: walked out of the arena into the next room (the seamless world enters no scene for it), all put back.</summary>
        public static void Tick()
        {
            if (visit == null || !visit.Arrived) return;
            var gm = GameManager.UnsafeInstance;
            var live = World.TerraWorld.Live?.SceneName;
            if (gm == null || gm.IsInSceneTransition || live == null || InFight(live)) return;
            Restore(keepWins: true);
        }

        /// <summary>
        /// Everything set for the visit put back. A flag or scene state the game changed since (the
        /// boss beaten: it wrote the win down) stays as the game left it when <paramref name="keepWins"/>.
        /// What the fight only needed always goes back.
        /// </summary>
        static void Restore(bool keepWins)
        {
            var v = visit;
            if (v == null) return;
            var pd = PlayerData.instance;
            var a = v.Arena;
            var won = new List<string>();
            if (pd != null)
            {
                foreach (var (n, val) in a.Beaten)
                    if (!keepWins || pd.GetBool(n) == val) pd.SetBool(n, v.Bools[n]);
                    else won.Add(n);
                foreach (var (n, val) in a.BeatenInts)
                    if (!keepWins || pd.GetInt(n) == val) pd.SetInt(n, v.Ints[n]);
                    else won.Add(n);
                foreach (var (n, _) in a.Needs)
                    if (!Contains(a.Beaten, n)) pd.SetBool(n, v.Bools[n]);
                foreach (var (n, _) in a.NeedInts)
                    if (!Contains(a.BeatenInts, n)) pd.SetInt(n, v.Ints[n]);
            }
            if (SceneData.instance != null)
                foreach (var s in a.States)
                {
                    var data = StateOf(s);
                    var now = SceneData.instance.FindMyState(data);
                    bool set = now != null && now.activated;
                    if (keepWins && set) { won.Add(s); continue; }
                    data.activated = v.States[s];
                    SceneData.instance.SaveMyState(data);
                }
            Plugin.Log.LogInfo($"[图鉴] 挑战 {v.Key} 结束，记录放回原样{(won.Count > 0 ? "，赢下的留着：" + string.Join("、", won) : "")}");
            visit = null;
        }

        static bool Contains((string Name, bool Value)[] list, string n) { foreach (var e in list) if (e.Name == n) return true; return false; }
        static bool Contains((string Name, int Value)[] list, string n) { foreach (var e in list) if (e.Name == n) return true; return false; }

        // A save while the fight is on (quitting to the menu, a bench in the arena) keeps the save as it
        // was: the visit's changes come off for the save and go back on after it.
        static Visit saving;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), "SaveGame", new[] { typeof(int), typeof(System.Action<bool>) })]
        static void BeforeSave()
        {
            if (visit == null) return;
            saving = visit;
            var pd = PlayerData.instance;
            var a = saving.Arena;
            foreach (var kv in saving.Bools) saving.Now[kv.Key] = pd.GetBool(kv.Key);
            foreach (var kv in saving.Ints) saving.NowInts[kv.Key] = pd.GetInt(kv.Key);
            foreach (var (n, val) in a.Beaten) if (pd.GetBool(n) == val) pd.SetBool(n, saving.Bools[n]);
            foreach (var (n, val) in a.BeatenInts) if (pd.GetInt(n) == val) pd.SetInt(n, saving.Ints[n]);
            foreach (var (n, _) in a.Needs) pd.SetBool(n, saving.Bools[n]);
            foreach (var (n, _) in a.NeedInts) pd.SetInt(n, saving.Ints[n]);
            foreach (var st in a.States)
            {
                var data = StateOf(st);
                var now = SceneData.instance.FindMyState(data);
                bool set = now != null && now.activated;
                saving.NowStates[st] = set;
                if (set) continue;
                data.activated = saving.States[st];
                SceneData.instance.SaveMyState(data);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "SaveGame", new[] { typeof(int), typeof(System.Action<bool>) })]
        static void AfterSave()
        {
            var s = saving;
            saving = null;
            if (s == null) return;
            var pd = PlayerData.instance;
            foreach (var kv in s.Now) pd.SetBool(kv.Key, kv.Value);
            foreach (var kv in s.NowInts) pd.SetInt(kv.Key, kv.Value);
            foreach (var kv in s.NowStates)
            {
                var data = StateOf(kv.Key);
                data.activated = kv.Value;
                SceneData.instance.SaveMyState(data);
            }
            s.Now.Clear();
            s.NowInts.Clear();
            s.NowStates.Clear();
        }
    }
}
