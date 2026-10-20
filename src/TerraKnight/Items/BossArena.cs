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
            ["TraitorLord"] = Set(W("Fungus3_23", "left1"), beaten: new[] { F("killedTraitorLord") }, needs: new[] { T("hasShadowDash") }, states: new[] { "Fungus3_23/Battle Scene" }),
            ["MantisLord"] = Set(W("Fungus2_15", "top1"), beaten: new[] { F("defeatedMantisLords") }),
            // left1 is the lower, second-phase exit (Y=19). Enter the first-phase
            // approach from right1 (Y=43), before reviewed placement on the upper floor.
            ["MageLord"] = Set(W("Ruins1_24", "right1"), beaten: new[] { F("mageLordDefeated"), F("mageLordEncountered") }),
            ["MegaJellyfish"] = Set(W("Fungus3_archive_02", "top1"), beaten: new[] { F("defeatedMegaJelly") }),
            ["FlukeMother"] = Set(W("Waterways_12", "right1"), beaten: new[] { F("flukeMotherDefeated") }),
            ["MegaBeamMiner"] = Set(W("Mines_18", "left1"), beaten: new[] { F("killedMegaBeamMiner"), F("defeatedMegaBeamMiner") }, states: new[] { "Mines_18/Battle Scene", "Mines_18/Mega Zombie Beam Miner (1)", "Mines_18/Mega Zombie Beam Miner" }),
            ["GhostHu"] = Set(W("Fungus2_32", "left1"), beatenInts: new[] { ("elderHuDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            ["GhostMarmu"] = Set(W("Fungus3_40", "right1"), beatenInts: new[] { ("mumCaterpillarDefeated", 0) }, needs: new[] { T("hasDreamNail") }),
            // The Watcher Knights wake only for one who comes down from the Watcher's room (doorTrigger top1).
            ["BlackKnight"] = Set(W("Ruins2_03", "top1"), states: new[] { "Ruins2_03/Battle Control" }),
            ["HiveKnight"] = Set(W("Hive_05", "left1"), beaten: new[] { F("killedHiveKnight") }),
            ["JarCollector"] = Set(W("Ruins2_11", "right1"), beaten: new[] { F("collectorDefeated") }, states: new[] { "Ruins2_11/Battle Scene" }),
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
            ["NightmareGrimm"] = Set(D("Grimm_Nightmare"), beaten: new[] { F("killedNightmareGrimm"), F("defeatedNightmareGrimm") }),
            ["FinalBoss"] = D("Dream_Final_Boss"),
            ["HollowKnightPrime"] = G("GG_Hollow_Knight"),
            ["NailBros"] = G("GG_Nailmasters"),
            ["Paintmaster"] = G("GG_Painter"),
            ["Nailsage"] = G("GG_Sly"),
            ["Oblobble"] = G("GG_Oblobbles"),
            ["LobsterLancer"] = G("GG_God_Tamer"),
        };

        /// <summary>
        /// Each world arena's front door from outside (arenadoors: the arena gate's targetScene and entryPoint): the room
        /// before the fight and its gate into the arena. A seal brings the player there, to walk in. The Troupe Master's
        /// tent has none: Dirtmouth's tent door is only there while the troupe is.
        /// </summary>
        static readonly Dictionary<string, (string Scene, string Gate)> frontDoors = new Dictionary<string, (string, string)>
        {
            ["FalseKnight"] = ("Crossroads_06", "right1"), ["BigFly"] = ("Crossroads_50", "left1"), ["Mawlek"] = ("Crossroads_33", "left1"),
            ["Hornet"] = ("Fungus1_25", "right1"), ["MegaMossCharger"] = ("Fungus1_12", "right1"), ["GhostAladar"] = ("Cliffs_01", "right1"),
            ["GhostNoEyes"] = ("Fungus1_34", "door1"), ["DungDefender"] = ("Waterways_02", "top2"), ["TraitorLord"] = ("Fungus3_48", "right1"),
            ["MantisLord"] = ("Fungus2_14", "bot1"), ["MageLord"] = ("Ruins1_30", "left1"), ["MegaJellyfish"] = ("Fungus3_archive", "bot1"),
            ["FlukeMother"] = ("Waterways_08", "left1"), ["MegaBeamMiner"] = ("Mines_11", "right1"), ["GhostHu"] = ("Fungus2_08", "right1"),
            ["GhostMarmu"] = ("Fungus3_13", "left2"), ["BlackKnight"] = ("Ruins2_Watcher_Room", "bot1"), ["HiveKnight"] = ("Hive_04", "right1"),
            ["JarCollector"] = ("Ruins2_11_b", "left1"), ["MimicSpider"] = ("Deepnest_31", "right2"), ["GhostGalien"] = ("Deepnest_35", "left1"),
            ["GhostXero"] = ("Crossroads_46b", "right1"), ["Hornet@4"] = ("Deepnest_East_12", "right1"), ["GhostMarkoth"] = ("Deepnest_East_18", "right2"),
            ["InfectedKnight"] = ("Abyss_18", "left1"), ["HollowKnight"] = ("Room_Final_Boss_atrium", "right1"),
        };

        /// <summary>Dev: the front door's gate in the room before the arena.</summary>
        internal static string DevDoorGate(string key) => frontDoors.TryGetValue(key, out var d) ? d.Gate : null;

        /// <summary>The room a seal brings the player to for this fight: the front door's, else the arena itself.</summary>
        internal static string ArrivalScene(string key) =>
            visit != null && visit.Key == key && visit.Door != null ? visit.Door : SceneOf(key);

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

        /// <summary>A boss of this record beaten in this scene's arena: its beaten records written as a win writes them (a LAN partner's fight).</summary>
        internal static void WriteBeaten(string record, string scene)
        {
            var pd = PlayerData.instance;
            if (pd == null || record == null || scene == null) return;
            string room = scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;
            foreach (var kv in arenas)
            {
                if (kv.Key.Split('@')[0] != record || kv.Value.Scene != room) continue;
                foreach (var (name, v) in kv.Value.Beaten) if (pd.GetBool(name) == v) pd.SetBool(name, !v);
                foreach (var (name, v) in kv.Value.BeatenInts) if (pd.GetInt(name) <= v) pd.SetInt(name, v + 1);
            }
        }

        /// <summary>Dev: every world boss beaten, as the game records it (each arena's beaten flags the other way from a visit's).</summary>
        internal static void DevAllBeaten()
        {
            var pd = PlayerData.instance;
            foreach (var a in arenas.Values)
                if (a.Kind == Kind.World)
                    foreach (var (name, v) in a.Beaten) pd.SetBool(name, !v);
        }

        /// <summary>Dev: a boss not beaten yet, as a visit sets it (its "encountered" records left be: the intro the test has seen).</summary>
        internal static void DevUnbeaten(string key)
        {
            if (!arenas.TryGetValue(key, out var a)) return;
            foreach (var (name, v) in a.Beaten) if (!Encountered(name)) PlayerData.instance.SetBool(name, v);
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
            /// <summary>Brought to the arena's front door (a seal): the room before it, until the player walks in.</summary>
            public string Door;
            public bool AtDoor;
            /// <summary>The boss's kills when the visit began; when it was beaten in this one; its gates opened since.</summary>
            public int KillsBefore;
            /// <summary>A boss beaten before: its first-win rewards (a warrior's Dream Essence) aren't given again.</summary>
            public bool Rematch;
            public float WonAt = -1f;
            public bool GatesOpened;
            /// <summary>The arena scenes' solid colliders (not their ground, not a gate) off when first seen in the visit: true
            /// when their own object was switched off then (not only the collider, or a parent).</summary>
            public readonly Dictionary<Collider2D, bool> OffAtStart = new Dictionary<Collider2D, bool>();
            public readonly HashSet<string> Looked = new HashSet<string>();
            public float NextLook;
        }

        static Visit visit;

        /// <summary>A creative challenge is on: its records are being set back and forth (SpecialDeaths leaves them be).</summary>
        public static bool Visiting => visit != null;
        internal static string VisitKey => visit?.Key;
        /// <summary>A visit to a boss beaten before is on (EssenceWallet keeps its essence out).</summary>
        internal static bool Rematching => visit != null && visit.Rematch;
        /// <summary>Where a dream's or Godhome's fight sends the player back to: the scene and the spot they left from.</summary>
        static string returnScene;
        /// <summary>
        /// The gate back into <see cref="returnScene"/> when it has no dream-return door of its own (a dream left from
        /// anywhere): its gate nearest the spot left from. Null: the room's own door_dreamReturn.
        /// </summary>
        static string returnGate;
        static bool ordinaryDreamReturn;
        public static int DevReturnGates;
        static Vector2 returnSpot;

        public static string DevLast = "";
        public static bool DevActive => visit != null;

        public static bool Has(string key) => key != null && arenas.ContainsKey(key);
        /// <summary>Dev: every arena's key.</summary>
        internal static IEnumerable<string> DevKeys() => arenas.Keys;
        internal static string SceneOf(string key) => key != null && arenas.TryGetValue(key, out var a) ? a.Scene : null;
        /// <summary>A world arena's scene by the book's key ("Hornet" Greenpath's, "Hornet@4" Kingdom's Edge's); null for a dream's or Godhome's.</summary>
        internal static string WorldSceneOf(string key) => key != null && arenas.TryGetValue(key, out var a) && a.Kind == Kind.World ? a.Scene : null;
        internal static bool Arrived(string key) => visit != null && visit.Key == key && visit.Arrived;
        internal static void AbortEntry(string key)
        {
            if (visit == null || visit.Key != key || visit.Arrived) return;
            if (visit.Arena.Kind == Kind.Godhome) BossSceneController.SetupEvent = null;
            Restore(keepWins: true);
            returnScene = null; returnGate = null;
        }

        /// <summary>Takes the player to the boss's arena for its fight; false when this boss has none (a copy is spawned then).</summary>
        public static bool Fight(string key) => Fight(key, false);

        /// <summary>
        /// The fight, as a rematch whatever this save has beaten (a LAN game's follower of another's seal). At the door
        /// (a seal's): a world arena is come to by its front door, from the room before it, and walked into.
        /// </summary>
        public static bool Fight(string key, bool rematch, bool atDoor = false)
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            var pd = PlayerData.instance;
            if (!Has(key) || gm == null || hero == null || pd == null || gm.IsInSceneTransition) return false;
            if (visit != null) Restore(keepWins: true);
            ordinaryDreamReturn = false;
            // A summon used while resting leaves the bench behind; its seated flag must not
            // follow the player into a fight or survive a later dream return.
            pd.atBench = false;
            var a = arenas[key];
            var v = new Visit { Key = key, Arena = a, KillsBefore = Bestiary.KillsOf(key) };
            string scene = a.Scene, gate = a.Gate;
            if (atDoor && a.Kind == Kind.World && frontDoors.TryGetValue(key, out var door))
            {
                v.Door = door.Scene;
                scene = door.Scene;
                gate = door.Gate;
            }
            // Beaten before (as a seal needs): the fight straight away, the story skipped, its "encountered" record kept on
            // (the game's own way back into a fight it has seen); never beaten (the Creative book's button): met as the
            // first time, intro and all.
            rematch |= Bestiary.KillsOf(key) > 0;
            v.Rematch = rematch;
            // These writes are the visit's own, not a fight's (SpecialDeaths leaves them be).
            Writing = true;
            foreach (var (n, val) in a.Beaten) { v.Bools[n] = pd.GetBool(n); pd.SetBool(n, rematch && Encountered(n) ? true : val); }
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
            Writing = false;
            visit = v;
            if (a.Kind != Kind.World)
            {
                // Sent back by the dream's own ending to where the player stands now.
                var cell = World.CellGrid.WorldToCell(hero.transform.position);
                var region = World.TerraWorld.RegionAt(cell.x, cell.y);
                returnScene = region?.SceneName ?? gm.sceneName;
                returnSpot = (Vector2)hero.transform.position - (region != null ? (Vector2)region.CellOffset * World.CellGrid.CellSize : Vector2.zero);
                pd.dreamReturnScene = returnScene;
                returnGate = ReturnGate(returnScene, hero.transform.position);
                pd.bossReturnEntryGate = "";
            }
            if (a.Kind == Kind.Godhome)
                BossSceneController.SetupEvent = self =>
                {
                    self.BossLevel = 0;
                    self.DreamReturnEvent = "DREAM RETURN";
                };
            InventoryGui.Open = false;
            UI.HudDialogue.Close();
            DevLast = $"{key} → {scene}（{gate}）" + (v.Door != null ? $"，正门外，首领房 {a.Scene}" : "");
            Plugin.Log.LogInfo($"[图鉴] 挑战 {DevLast}：开打前的记录 {string.Join("，", Describe(v))}");
            // An arena laid over its room only for one who came in by its door (the Watcher Knights':
            // SceneAdditiveLoadConditional.doorTrigger against the last gate walked through) has it walked.
            if (a.Kind == Kind.World && v.Door == null) TransitionPoint.lastEntered = a.Gate;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = scene, EntryGateName = gate, HeroLeaveDirection = GatePosition.unknown, EntryDelay = 0f,
                WaitForSceneTransitionCameraFade = true,
                Visualization = a.Kind == Kind.World ? GameManager.SceneLoadVisualizations.Default : GameManager.SceneLoadVisualizations.Dream,
            });
            return true;
        }

        // BeginSceneTransition saves every loaded persistent object before loading the target.
        // A parked room can otherwise write its old victory over the temporary arena reset.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveLevelState))]
        static void RearmAfterDepartureSave()
        {
            if (visit == null || visit.Arrived || saving != null || SceneData.instance == null) return;
            foreach (var key in visit.Arena.States)
            {
                var data = StateOf(key); data.activated = false;
                SceneData.instance.SaveMyState(data);
            }
        }

        /// <summary>
        /// The waking's way back into a room: its own door_dreamReturn (null), else its gate nearest the spot (a room the
        /// game never wakes the player into: Crossroads_01, Greenpath... a wake to a door the room hasn't left the player
        /// nowhere, a white screen with no control).
        /// </summary>
        static string ReturnGate(string scene, Vector2 near)
        {
            string best = null;
            float bestD = float.MaxValue;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tp == null || tp.gameObject.scene.name != scene) continue;
                if (tp.name == "door_dreamReturn") return null;
                if (tp.name.StartsWith("door_dream") || tp.name.StartsWith("dream")) continue;
                float d = Vector2.Distance(tp.transform.position, near);
                if (d < bestD) { bestD = d; best = tp.name; }
            }
            return best;
        }

        /// <summary>The waking sent into the room left from by its own gate when it has no dream-return door.</summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(GameManager), nameof(GameManager.BeginSceneTransition))]
        static void WakeByGate(GameManager.SceneLoadInfo info)
        {
            if (info == null || returnGate == null || returnScene == null) return;
            if (info.SceneName != returnScene || info.EntryGateName != "door_dreamReturn") return;
            info.EntryGateName = returnGate;
            // The old dream visualization reasserts IsEnteringDream inside GameManager,
            // even after the returning flags below were cleared. An ordinary gate has no
            // dream wake-up actor to remove its white fade or release the hero.
            info.Visualization = GameManager.SceneLoadVisualizations.Default;
            ordinaryDreamReturn = true;
            DevReturnGates++;
            // In by an ordinary gate, an ordinary entry: not the Knight lying where the dream's door would be, waiting for
            // jump (his "Dream Return" left in "Ready" while he walks, the next waking stuck in "Get Up").
            var hero = HeroController.SilentInstance;
            if (hero != null)
                foreach (var f in hero.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "Dream Return" || f.FsmName == "Dream Nail")
                    {
                        var returning = f.FsmVariables.FindFsmBool("Dream Returning");
                        if (returning != null) returning.Value = false;
                    }
            Plugin.Log.LogInfo($"[图鉴] 梦醒回到 {returnScene}：那里没有梦醒的门，从 {returnGate} 进去，再回到出发的地方");
        }

        /// <summary>A record of the boss met (its intro seen), not of it beaten (Nosk's "encountered" is his defeat).</summary>
        static bool Encountered(string flag) => flag.IndexOf("ncountered", System.StringComparison.Ordinal) >= 0 && flag != "encounteredMimicSpider";

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
            visit != null && scene != null && (scene == visit.Arena.Scene || scene.StartsWith(visit.Arena.Scene + "_boss")
                // GameManager.sceneName is the base name: the Radiance's Dream_Final_Boss goes by Dream_Final.
                || scene + "_Boss" == visit.Arena.Scene || scene + "_boss" == visit.Arena.Scene);

        /// <summary>A scene entered: the arena (the fight is on), or away from it (all put back), or back from a dream (to the spot left).</summary>
        public static void OnSceneReady(GameManager gm)
        {
            if (gm == null) return;
            if (visit != null)
            {
                if (InFight(gm.sceneName))
                {
                    BossEncounterCleanup.HideStoryActors();
                    bool firstArrival = !visit.Arrived;
                    if (firstArrival && visit.Arena.Kind != Kind.World) TerraMod.Instance?.StartCoroutine(FadeIn());
                    visit.Arrived = true;
                    // Seals, creative-book challenges and LAN followers share the same placement/start path.
                    if (firstArrival) TerraMod.Instance?.StartCoroutine(BossSummonStart.Begin(visit.Key));
                }
                else if (!visit.Arrived && visit.Door != null && gm.sceneName == visit.Door)
                {
                    // At the front door: the records stay set for the fight inside. An arena laid over its room only
                    // for one who comes in by its door (the Watcher Knights') takes the walk in as that.
                    visit.AtDoor = true;
                    TransitionPoint.lastEntered = visit.Arena.Gate;
                    Plugin.Log.LogInfo($"[图鉴] 挑战 {visit.Key}：到了正门外 {visit.Door}，首领房 {visit.Arena.Scene}");
                }
                else if (visit.Arrived || !gm.IsInSceneTransition)
                {
                    Restore(keepWins: true);
                    // Left some other way (a death, a door): no spot to go back to later.
                    if (gm.sceneName != returnScene) returnScene = null; returnGate = null;
                }
            }
            if (returnScene != null && gm.sceneName == returnScene && visit == null)
            {
                string arrivedScene = returnScene;
                var hero = HeroController.SilentInstance;
                if (hero != null)
                {
                    hero.transform.position = new Vector3(returnSpot.x, returnSpot.y, hero.transform.position.z);
                    var rb = hero.GetComponent<Rigidbody2D>();
                    if (rb != null) { rb.position = returnSpot; rb.linearVelocity = Vector2.zero; }
                    Plugin.Log.LogInfo($"[图鉴] 挑战结束，回到 {returnScene} {returnSpot:F1}");
                }
                returnScene = null; returnGate = null;
                if (ordinaryDreamReturn)
                {
                    ordinaryDreamReturn = false;
                    TerraMod.Instance?.StartCoroutine(FinishOrdinaryDreamReturn(arrivedScene));
                }
            }
        }

        // A dream/Godhome loss can return to any room a seal was used in. When that room
        // has no native dream-return gate, explicitly finish the wake-up we bypassed.
        static System.Collections.IEnumerator FinishOrdinaryDreamReturn(string scene)
        {
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                var loading = GameManager.UnsafeInstance;
                if (loading == null || loading.sceneName != scene || visit != null) yield break;
                if (!loading.IsInSceneTransition) break;
                yield return null;
            }
            yield return null;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.sceneName != scene || gm.IsInSceneTransition || visit != null) yield break;
            foreach (var f in hero.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Dream Return" || f.FsmName == "Dream Nail")
                {
                    var returning = f.FsmVariables.FindFsmBool("Dream Returning");
                    if (returning != null) returning.Value = false;
                    foreach (var state in f.FsmStates)
                        if (state.Name == "Idle") { if (f.ActiveStateName != "Idle") f.SetState("Idle"); break; }
                }
            hero.IsEnteringDream = false;
            hero.cState.dead = false;
            hero.RegainControl();
            hero.StartAnimationControl();
            hero.AffectedByGravity(true);
            PlayerData.instance.SetBool("disablePause", false);
            PlayerData.instance.atBench = false;
            TerraMod.Instance?.Stats.FullHeal();
            gm.FadeSceneIn();
            var whites = new List<PlayMakerFSM>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.gameObject.name == "Blanker White" || f.gameObject.name == "Blanker White(Clone)")
                { whites.Add(f); f.SendEvent("FADE OUT"); }
            yield return new WaitForSecondsRealtime(0.7f);
            if (gm == null || gm.sceneName != scene || gm.IsInSceneTransition || visit != null) yield break;
            var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
            if (fade != null && fade.ActiveStateName != "Normal")
                foreach (var state in fade.FsmStates)
                    if (state.Name == "Normal") { fade.SetState("Normal"); break; }
            // Only the known dream white overlay; keep it enabled for future native fades.
            foreach (var f in whites)
            {
                if (f == null) continue;
                foreach (var r in f.GetComponentsInChildren<SpriteRenderer>(true)) { var c = r.color; c.a = 0f; r.color = c; }
                foreach (var r in f.GetComponentsInChildren<tk2dBaseSprite>(true)) { var c = r.color; c.a = 0f; r.color = c; }
                foreach (var r in f.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) { var c = r.color; c.a = 0f; r.color = c; }
            }
            DevFadeKicks++;
            Plugin.Log.LogInfo($"[图鉴] 梦境/神居返回 {scene}：普通门入场完成，梦醒状态、白屏和操作已收尾");
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

        public static int DevReadyReset;
        static float readyFree;

        /// <summary>
        /// The Knight's "Dream Return" waiting for him to get up ("Ready") while he already walks about with control: a
        /// waking that ended some other way. Put back to its idle (a second of it), so the next waking isn't caught in its
        /// "Get Up" waiting on a lying Knight's clip.
        /// </summary>
        static void UnstickWaking()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || !GameHooks.PlayerHasControl()) { readyFree = 0f; return; }
            PlayMakerFSM ret = null;
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Dream Return") ret = f;
            if (ret == null || ret.ActiveStateName != "Ready") { readyFree = 0f; return; }
            if (readyFree <= 0f) { readyFree = Time.unscaledTime; return; }
            if (Time.unscaledTime - readyFree < 1f) return;
            ret.SetState("Idle");
            readyFree = 0f;
            DevReadyReset++;
            Plugin.Log.LogInfo("[图鉴] 骑士能动了，梦醒还停在等起身：复位");
        }

        /// <summary>Every frame: walked out of the arena into the next room (the seamless world enters no scene for it), all put back.</summary>
        public static void Tick()
        {
            UnstickWaking();
            if (visit == null) return;
            if (!visit.Arrived && visit.AtDoor)
            {
                // From the front door: walked into the arena (the seamless world's next room) is the fight on; walked
                // off another way, all put back.
                var g = GameManager.UnsafeInstance;
                var here = World.TerraWorld.Live?.SceneName;
                if (g == null || g.IsInSceneTransition || here == null) return;
                if (InFight(here))
                {
                    visit.Arrived = true;
                    Plugin.Log.LogInfo($"[图鉴] 挑战 {visit.Key}：走进首领房 {here}");
                }
                else if (here != visit.Door) Restore(keepWins: true);
                return;
            }
            if (!visit.Arrived) return;
            var gm = GameManager.UnsafeInstance;
            var live = World.TerraWorld.Live?.SceneName;
            if (gm == null || gm.IsInSceneTransition || live == null) return;
            if (InFight(live))
            {
                if (visit.WonAt < 0f && Time.time >= visit.NextLook) LookForWalls();
                OpenGatesAfterWin(gm);
                KeepGatesOpen();
                return;
            }
            Restore(keepWins: true);
        }

        /// <summary>
        /// A collider that can wall the player in: solid, on the terrain layer, not the room's ground (its tilemap), not
        /// a battle gate (opened by its event), not an enemy's body, not the mod's own.
        /// </summary>
        static bool WallLike(Collider2D c)
        {
            if (c == null || c.isTrigger || c.gameObject.layer != World.ChunkColliders.TerrainLayer) return false;
            if (c.GetComponentInParent<World.TerraOwned>() != null || c.GetComponentInParent<HealthManager>() != null) return false;
            for (var t = c.transform; t != null; t = t.parent)
                if (t.name == "TileMap Render Data" || t.GetComponent<tk2dTileMap>() != null) return false;
            foreach (var f in c.GetComponentsInParent<PlayMakerFSM>(true)) if (f.FsmName == "BG Control") return false;
            return true;
        }

        /// <summary>The arena's scenes looked at once each as they come in (a boss scene loads beside its room): their walls switched off then.</summary>
        static void LookForWalls()
        {
            visit.NextLook = Time.time + 0.5f;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!sc.isLoaded || !InFight(sc.name) || !visit.Looked.Add(sc.name)) continue;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                        if (!(c.enabled && c.gameObject.activeInHierarchy) && WallLike(c)) visit.OffAtStart[c] = !c.gameObject.activeSelf;
            }
        }

        public static int DevGatesReopened;

        /// <summary>
        /// For half a minute after a rematch's gates were opened, a gate shut again (Nosk's: his arena closes one behind
        /// the player once more) opened again once it can take it (its state listens for BG OPEN).
        /// </summary>
        static void KeepGatesOpen()
        {
            if (!visit.GatesOpened || Time.time - visit.WonAt > 40f || Time.time < visit.NextLook) return;
            visit.NextLook = Time.time + 1f;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f == null || f.FsmName != "BG Control" || !InFight(f.gameObject.scene.name)) continue;
                var c = f.GetComponent<Collider2D>();
                if (c == null || !c.enabled) continue;
                var st = f.Fsm.ActiveState;
                bool listens = false;
                if (st != null) foreach (var t in st.Transitions) if (t.EventName == "BG OPEN") listens = true;
                foreach (var t in f.FsmGlobalTransitions) if (t.EventName == "BG OPEN") listens = true;
                if (!listens) continue;
                f.SendEvent("BG OPEN");
                DevGatesReopened++;
                Plugin.Log.LogInfo($"[图鉴] 挑战 {visit.Key}：{f.gameObject.name} 又关上了（{st?.Name}），再打开");
            }
        }

        public static int DevWallsOff;
        public static string DevWallsOffNames = "";

        /// <summary>
        /// A rematch won: the walls its fight put up taken down, as its own ending would (Hornet's in Greenpath: her
        /// refight's "Hornet Saver" stands by both gates and nothing of the refight takes it away; Hornet's in Kingdom's
        /// Edge, its battle scene's). Off when the visit came in, on now: put back the way they were off (their object
        /// switched off, picture and all, or just the collider), as the room was before the rematch.
        /// </summary>
        static void TakeDownFightWalls()
        {
            var names = new List<string>();
            foreach (var kv in visit.OffAtStart)
            {
                var c = kv.Key;
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (kv.Value) c.gameObject.SetActive(false);
                else c.enabled = false;
                names.Add(c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name);
            }
            DevWallsOff += names.Count;
            if (names.Count == 0) return;
            DevWallsOffNames = string.Join("、", names);
            Plugin.Log.LogInfo($"[图鉴] 挑战 {visit.Key}：战斗竖起的 {names.Count} 道墙撤掉：{DevWallsOffNames}");
        }

        /// <summary>
        /// A rematch won: the arena's battle gates opened 5 s after the boss went down. Some open only once the reward is
        /// picked up (the ability's shiny), which a save that has it doesn't get again, so they stayed shut.
        /// </summary>
        static void OpenGatesAfterWin(GameManager gm)
        {
            if (visit.GatesOpened) return;
            if (visit.WonAt < 0f)
            {
                if (Bestiary.KillsOf(visit.Key) > visit.KillsBefore) visit.WonAt = Time.time;
                return;
            }
            if (Time.time - visit.WonAt < 5f) return;
            visit.GatesOpened = true;
            int n = 0;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f == null || f.FsmName != "BG Control" || !f.gameObject.scene.isLoaded) continue;
                f.SendEvent("BG OPEN");
                n++;
            }
            DevGatesOpened += n;
            Plugin.Log.LogInfo($"[图鉴] 挑战 {visit.Key}：赢了，打开 {n} 道战斗门");
            TakeDownFightWalls();
        }

        public static int DevGatesOpened;

        /// <summary>
        /// Everything set for the visit put back. A flag or scene state the game changed since (the
        /// boss beaten: it wrote the win down) stays as the game left it when <paramref name="keepWins"/>.
        /// What the fight only needed always goes back.
        /// </summary>
        /// <summary>The visit is writing the game's records itself (setting them up, putting them back, around a save): no fight's win.</summary>
        public static bool Writing { get; private set; }

        static void Restore(bool keepWins)
        {
            Writing = true;
            try { RestoreRecords(keepWins); }
            finally { Writing = false; }
        }

        static void RestoreRecords(bool keepWins)
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
                    else
                    {
                        // A warrior's 1 (beaten) after a rematch never undoes the 2 (absorbed) from before: no second absorbing.
                        pd.SetInt(n, Mathf.Max(v.Ints[n], pd.GetInt(n)));
                        won.Add(n);
                    }
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
            BossEncounterCleanup.RestoreStoryActors();
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
            Writing = true;
            try { SaveAsBefore(); }
            finally { Writing = false; }
        }

        static void SaveAsBefore()
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
            Writing = true;
            try { SaveDone(); }
            finally { Writing = false; }
        }

        static void SaveDone()
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
