using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>
    /// What gets the achievements (docs/成就列表-*.md): the game's own records looked at once a second (the journey's
    /// abilities, spells, charms, keys, the Colosseum), the bosses as they go down (Bestiary's count: first defeats, no hit
    /// since the player came near, quick, at a sliver of life, on Master or Hollow, under the new AI), Hollow Knight's own
    /// achievements as it awards them (its endings and pantheons), and the player's deaths by what last hurt him.
    /// </summary>
    [HarmonyPatch]
    internal static partial class AchievementTracker
    {
        static float nextPoll, nextFight, nextSlowPoll;
        static bool goalsSet;

        public static void Tick()
        {
            if (!goalsSet)
            {
                goalsSet = true;
                Goals();
            }
            if (!Achievements.Counting) return;
            RunLater();
            Odometer();
            float now = Time.unscaledTime;
            if (now >= nextFight)
            {
                nextFight = now + 0.25f;
                WatchFights();
            }
            if (now < nextPoll) return;
            nextPoll = now + 1f;
            PollJourney();
        }

        // ------------------------------------------------------------------ the journey (Hollow Knight's records)

        static void PollJourney()
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            Check("J01", pd.hasDash);
            Check("J02", pd.hasWalljump);
            Check("J03", pd.hasSuperDash);
            Check("J04", pd.hasDoubleJump);
            Check("J05", pd.hasAcidArmour);
            Check("J06", pd.hasShadowDash);
            Check("J07", pd.hasDreamNail);
            Check("J08", pd.dreamNailUpgraded);
            Check("J09", pd.hasKingsBrand);
            Check("J10", pd.fireballLevel >= 1);
            Check("J11", pd.fireballLevel >= 2);
            Check("J12", pd.quakeLevel >= 1);
            Check("J13", pd.quakeLevel >= 2);
            Check("J14", pd.screamLevel >= 1);
            Check("J15", pd.screamLevel >= 2);
            Check("J16", pd.fireballLevel >= 2 && pd.quakeLevel >= 2 && pd.screamLevel >= 2);
            // Charms are worn only with the charm bag on (Charms.Enabled, still being made).
            Check("J17", Charms.Enabled && pd.equippedCharms != null && pd.equippedCharms.Count > 0);
            Check("J19", pd.charmSlots >= 11);
            Check("J20", pd.royalCharmState >= 4);
            Check("J21", pd.royalCharmState >= 3);
            Check("J22", pd.destroyedNightmareLantern);
            Check("J23", pd.grimmChildLevel >= 4);
            Check("J24", pd.xunFlowerGiven);
            Check("J25", pd.grubsCollected >= 46);
            Check("J26", pd.maxHealthBase >= 9);
            Check("J27", pd.MPReserveMax >= 99);
            Check("J28", pd.nailSmithUpgrades >= 4);
            Check("J29", pd.hasCyclone && pd.hasDashSlash && pd.hasUpwardSlash);
            Check("J30", pd.mothDeparted || pd.dreamOrbs + pd.dreamOrbsSpent >= 2400);
            // The four doors a Simple Key opens: the Pleasure House, Jiji's, the Waterways' manhole, the Junk Pit's.
            Check("J31", pd.bathHouseOpened && pd.jijiDoorUnlocked && pd.openedWaterwaysManhole && pd.godseekerUnlocked);
            Check("J32", pd.gotKingFragment);
            Check("J37", pd.slyRescued && pd.brettaRescued && pd.zoteRescuedDeepnest);
            Check("B20", pd.colosseumBronzeCompleted);
            Check("B21", pd.colosseumSilverCompleted);
            Check("B22", pd.colosseumGoldCompleted);
            // The Path of Pain's end is its secret room.
            Check("C21", pd.whitePalaceSecretRoomVisited);
            Check("X05", pd.rancidEggs >= 10);
            Check("H08", Player.PlayerBuffs.Has(87) && Player.PlayerBuffs.Has(89) && Player.PlayerBuffs.Has(158));
            Check("H13", Player.PlayerBuffs.Has(207));
            Check("W07", Items.Minions.Instance != null && Items.Minions.Instance.MinionCount >= 10);
            if (Time.unscaledTime >= nextSlowPoll)
            {
                // Once every ten seconds: the full map and the book (they count over everything).
                nextSlowPoll = Time.unscaledTime + 10f;
                Check("E02", UI.WorldMap.SeenFraction >= 0.95f);
                float book = Items.InventoryGui.BookCompletion;
                Check("I12", book >= 0.5f);
                Check("I13", book >= 0.999f);
            }
            PollWorld();
            var mod = TerraMod.Instance;
            if (mod != null)
            {
                long geo = Currencies.Held(mod.Inventory, ItemType.Geo);
                Check("I16", geo >= 1000000);
                Check("X10", geo == 1);
            }
        }

        static void Check(string id, bool done)
        {
            if (done && !Achievements.Has(id)) Achievements.Unlock(id);
        }

        /// <summary>Hollow Knight's own achievements as it awards them (whether or not the platform has them already).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(AchievementHandler), nameof(AchievementHandler.AwardAchievementToPlayer))]
        static void HollowKnightAward(string key)
        {
            switch (key)
            {
                case "ENDINGA": Achievements.Unlock("J33"); break;
                case "ENDINGB": Achievements.Unlock("J34"); break;
                case "ENDINGC": Achievements.Unlock("J35"); break;
                case "ENDINGD": Achievements.Unlock("J36"); Achievements.Unlock("C19"); break;
                case "PANTHEON1": Achievements.Unlock("C15"); break;
                case "PANTHEON2": Achievements.Unlock("C16"); break;
                case "PANTHEON3": Achievements.Unlock("C17"); break;
                case "PANTHEON4": Achievements.Unlock("C18"); break;
            }
        }

        // ------------------------------------------------------------------ boss fights

        sealed class Fight
        {
            public HealthManager Hm;
            public string Record;
            public string Scene;
            public float NearAt = -1f;
            public int MaxHp;
            /// <summary>The classes of the player's hits on it (AchievementTracker.Hit's flags).</summary>
            public int Classes;
            /// <summary>The player stood a whole minute without moving in this fight.</summary>
            public bool Rooted;
        }

        static readonly Dictionary<int, Fight> fights = new Dictionary<int, Fight>();
        static readonly List<int> gone = new List<int>();

        /// <summary>The bosses up in the player's scene: when he first came near each (its fight's start, as he sees it).</summary>
        static Vector2 stillAt;
        static float stillFor;

        static void WatchFights()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            Vector2 at = hero.transform.position;
            // Rooted: not a step (nor a jump, nor a dash) for a minute in a fight.
            var now = Current();
            if (now != null && (at - stillAt).sqrMagnitude < 0.0025f) stillFor += 0.25f;
            else stillFor = 0f;
            stillAt = at;
            if (now != null && stillFor >= 60f) now.Rooted = true;
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !info.Boss || !hm.gameObject.activeInHierarchy) continue;
                int id = hm.GetInstanceID();
                if (!fights.TryGetValue(id, out var f))
                {
                    f = new Fight { Hm = hm, Record = info.Record, Scene = hm.gameObject.scene.name, MaxHp = Mathf.Max(1, hm.hp) };
                    fights[id] = f;
                }
                f.MaxHp = Mathf.Max(f.MaxHp, hm.hp);
                if (f.NearAt < 0f && Vector2.Distance(at, hm.transform.position) < 22f) f.NearAt = Time.time;
            }
            gone.Clear();
            foreach (var kv in fights) if (kv.Value.Hm == null) gone.Add(kv.Key);
            foreach (var k in gone) fights.Remove(k);
        }

        /// <summary>The fight on now (a boss near the player, alive), or null.</summary>
        static Fight Current()
        {
            Fight best = null;
            foreach (var f in fights.Values)
                if (f.Hm != null && !f.Hm.isDead && f.NearAt >= 0f && (best == null || f.NearAt > best.NearAt)) best = f;
            return best;
        }

        static bool InDreamOrGodhome(string scene) =>
            scene != null && (scene.StartsWith("GG_") || scene.StartsWith("Dream_") || scene.StartsWith("Room_Colosseum"));

        /// <summary>A boss's (whole fight's) defeat, as the book counts it (Bestiary's kill, after its multi-body gate).</summary>
        public static void OnBossDown(HealthManager hm, string baseRecord, string record, string scene)
        {
            try { BossDown(hm, baseRecord, record, scene); }
            catch (System.Exception e) { Quiet(e); }
        }

        static void BossDown(HealthManager hm, string baseRecord, string record, string scene)
        {
            if (!Achievements.Counting) return;
            fights.TryGetValue(hm != null ? hm.GetInstanceID() : 0, out var fight);
            float since = fight != null && fight.NearAt >= 0f ? fight.NearAt : -1f;
            bool noHit = since >= 0f && Player.TerraHurt.LastHurtAt < since;
            var mod = TerraMod.Instance;
            var pd = PlayerData.instance;

            string first = FirstOf(baseRecord, scene);
            if (first != null) Achievements.Unlock(first);
            if (Godhome(scene) && baseRecord == "HollowKnightPrime") Achievements.Unlock("B35");

            // The seven warriors.
            if (baseRecord.StartsWith("Ghost"))
            {
                Achievements.Set("ghost:" + baseRecord, 1);
                int seven = 0;
                foreach (var g in Ghosts) if (Achievements.Count("ghost:" + g) > 0) seven++;
                if (seven >= Ghosts.Length) Achievements.Unlock("B34");
            }

            // The new AI's bosses: ten, then all of them (their waking versions).
            if (World.EnemyAiMode.New && !Godhome(scene))
            {
                if (Achievements.Count("newai:" + baseRecord) == 0)
                {
                    Achievements.Set("newai:" + baseRecord, 1);
                    Achievements.Add("newai bosses");
                }
                if (Achievements.Count("newai bosses") >= 10) Achievements.Unlock("B39");
                bool all = true;
                foreach (var r in WorldBosses) if (Achievements.Count("newai:" + r) == 0) { all = false; break; }
                if (all) Achievements.Unlock("B40");
            }

            if (since >= 0f && Time.time - since <= 30f && !InDreamOrGodhome(scene)) Achievements.Unlock("B41");
            if (mod != null && mod.Stats.LifeMax > 0 && mod.Stats.Life <= mod.Stats.LifeMax * 0.05f) Achievements.Unlock("B42");
            var level = World.GameDifficulty.Current;
            if (baseRecord == "HollowKnight" && level >= World.DifficultyLevel.Master) Achievements.Unlock("B43");
            if (baseRecord == "FinalBoss" && level == World.DifficultyLevel.Hollow) Achievements.Unlock("B44");
            if (baseRecord == "HollowKnight" && pd != null && pd.permadeathMode == 1) Achievements.Unlock("C23");
            if (baseRecord == "HollowKnight" && pd != null && pd.playTime <= 5f * 3600f) Achievements.Unlock("C29");
            if (fight != null && fight.Rooted) Achievements.Unlock("C30");
            if (pd != null && pd.overcharmed && Charms.Enabled) Achievements.Unlock("J18");
            ByClass(baseRecord, fight != null ? fight.Classes : 0);
            if (baseRecord == "HollowKnight" && mod != null && Bare(mod.Inventory) && (pd == null || pd.equippedCharms == null || pd.equippedCharms.Count == 0))
                Achievements.Unlock("C27");
            Others(out int others, out int othersDead);
            if (others >= 1) Achievements.Unlock("M01");
            if (others >= 3) Achievements.Unlock("M02");
            if (others >= 1 && othersDead == others) Achievements.Unlock("M04");

            if (noHit)
            {
                string flawless = NoHitOf(baseRecord, scene);
                if (flawless != null) Achievements.Unlock(flawless);
                if (Achievements.Count("nohit:" + baseRecord) == 0)
                {
                    Achievements.Set("nohit:" + baseRecord, 1);
                    Achievements.Add("nohit bosses");
                }
                if (Achievements.Count("nohit bosses") >= 10) Achievements.Unlock("C13");
                if (World.EnemyAiMode.New && level == World.DifficultyLevel.Hollow) Achievements.Unlock("C14");
            }
            Plugin.Log.LogInfo($"[成就] 首领倒下 {record}（{scene}）：靠近后 {(since >= 0f ? (Time.time - since).ToString("F1") + " 秒" : "未记录")}，无伤 {noHit}");
        }

        static bool Godhome(string scene) => scene != null && scene.StartsWith("GG_");

        static bool Dream(string scene, params string[] rooms)
        {
            foreach (var r in rooms) if (scene == r) return true;
            return false;
        }

        /// <summary>A boss's first-defeat achievement by its record and room (its dream and Godhome versions apart).</summary>
        static string FirstOf(string rec, string scene)
        {
            switch (rec)
            {
                case "FalseKnight": return Dream(scene, "Dream_01_False_Knight", "GG_Failed_Champion") ? "B31" : "B01";
                case "BigFly": return "B02";
                case "BigBuzzer": return "B03";
                case "Mawlek": return "B04";
                case "Hornet": return Dream(scene, "Deepnest_East_Hornet", "GG_Hornet_2") ? null : "B05";
                case "MegaMossCharger": return "B06";
                case "MageKnight": return "B07";
                case "MantisLord": return "B08";
                case "MageLord": return Dream(scene, "Dream_02_Mage_Lord", "GG_Soul_Tyrant") ? "B32" : "B09";
                case "DungDefender": return "B10";
                case "FlukeMother": return "B11";
                case "MegaBeamMiner": return Dream(scene, "Mines_32", "GG_Crystal_Guardian_2") ? "B13" : "B12";
                case "MimicSpider": return "B14";
                case "BlackKnight": return "B15";
                case "InfectedKnight": return Dream(scene, "Dream_03_Infected_Knight", "GG_Lost_Kin") ? "B33" : "B16";
                case "JarCollector": return "B17";
                case "HiveKnight": return "B18";
                case "TraitorLord": return "B19";
                case "MegaJellyfish": return "B23";
                case "NailBros": return "B24";
                case "Nailsage": return "B25";
                case "Paintmaster": return "B26";
                case "Grimm": return "B27";
                case "NightmareGrimm": return "B28";
                case "GreyPrince": return "B29";
                case "WhiteDefender": return "B30";
                case "HollowKnight": return "B36";
                case "FinalBoss": return scene == "GG_Radiance" ? "B38" : "B37";
                default: return null;
            }
        }

        static string NoHitOf(string rec, string scene)
        {
            switch (rec)
            {
                case "FalseKnight": return "C01";
                case "Hornet": return "C02";
                case "MantisLord": return "C03";
                case "MageLord": return "C04";
                case "BlackKnight": return "C05";
                case "InfectedKnight": return "C06";
                case "TraitorLord": return "C07";
                case "Grimm": return "C08";
                case "NightmareGrimm": return "C09";
                case "Nailsage": return "C10";
                case "HollowKnightPrime": return "C11";
                case "FinalBoss": return scene == "GG_Radiance" ? "C12" : null;
                default: return null;
            }
        }

        static readonly string[] Ghosts = { "GhostXero", "GhostAladar", "GhostHu", "GhostMarmu", "GhostNoEyes", "GhostGalien", "GhostMarkoth" };

        /// <summary>The bosses of the world (Godhome's own left out) for "every boss under the new AI".</summary>
        static readonly string[] WorldBosses =
        {
            "FalseKnight", "BigFly", "Mawlek", "Hornet", "MageKnight", "BigBuzzer", "MegaMossCharger", "MantisLord", "MageLord",
            "DungDefender", "FlukeMother", "MegaBeamMiner", "MimicSpider", "BlackKnight", "InfectedKnight", "JarCollector",
            "Oblobble", "HiveKnight", "LobsterLancer", "TraitorLord", "Grimm", "NightmareGrimm", "GreyPrince", "WhiteDefender",
            "MegaJellyfish", "HollowKnight", "FinalBoss", "GhostXero", "GhostAladar", "GhostHu", "GhostMarmu", "GhostNoEyes",
            "GhostGalien", "GhostMarkoth",
        };

        /// <summary>Any enemy's kill, as the book counts it.</summary>
        public static void OnKill(string baseRecord)
        {
            try { KillNow(baseRecord); }
            catch (System.Exception e) { Quiet(e); }
        }

        static void KillNow(string baseRecord)
        {
            if (!Achievements.Counting) return;
            long kills = Achievements.Add("kills");
            if (kills >= 1000) Achievements.Unlock("W01");
            if (kills >= 10000) Achievements.Unlock("W02");
            if (baseRecord == "SuperSpitter") Achievements.AddTo("W12", "primal aspids", 100);
            if (EnemyDrops.IsEliteRecord(baseRecord)) Achievements.AddTo("W13", "elites", 50);
            // Vengeance: the kind that last killed the player, within a minute.
            if (killer != null && baseRecord == killer && Time.time - diedAt <= 60f) Achievements.Unlock("W10");
        }

        // ------------------------------------------------------------------ deaths

        /// <summary>A bench (its script, "Bench Control") within five tiles.</summary>
        static bool NearBench(Vector2 at)
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f != null && f.FsmName == "Bench Control" && Vector2.Distance(f.transform.position, at) < 2.5f) return true;
            return false;
        }

        /// <summary>No armor and no accessories on (what counts: the loadout worn).</summary>
        static bool Bare(Inventory inv)
        {
            foreach (var s in inv.EffectiveArmor) if (!s.IsEmpty) return false;
            foreach (var s in inv.EffectiveAccessories) if (!s.IsEmpty) return false;
            return true;
        }

        static GameObject lastHit;
        static int lastHazard;
        static bool lastFall;
        static float poisonAt = -10f;
        static float lastHitAt = -10f;
        static string killer;
        static float diedAt = -100f;

        /// <summary>Something about to hurt the player (HeroController.TakeDamage): what it was, for his death.</summary>
        public static void Touched(GameObject go, int hazardType)
        {
            lastHit = go;
            lastHazard = hazardType;
            lastFall = Player.TerraHurt.FallHurt;
            lastHitAt = Time.time;
        }

        /// <summary>Poison's damage over time took the last of his life.</summary>
        public static void PoisonKill() => poisonAt = Time.time;

        public static void Died()
        {
            try { DiedNow(); }
            catch (System.Exception e) { Quiet(e); }
        }

        static void DiedNow()
        {
            if (!Achievements.Counting) return;
            diedAt = Time.time;
            long deaths = Achievements.Add("deaths");
            if (deaths >= 100) Achievements.Unlock("D19");
            if (deaths >= 1000) Achievements.Unlock("D20");
            bool fresh = Time.time - lastHitAt < 1.5f;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            string scene = hero != null ? hero.gameObject.scene.name : "";

            bool poisoned = Time.time - poisonAt < 1.5f;
            if (poisoned) Achievements.Unlock("D01");
            if (fresh && lastFall) Achievements.Unlock("D03");
            if (fresh && lastHazard == 2) Achievements.Unlock("D04");
            if (fresh && lastHazard == 3) Achievements.Unlock("D05");
            if (mod != null && Currencies.Held(mod.Inventory, ItemType.Geo) >= 100000) Achievements.Unlock("D14");
            if (Casino.IsOpen) Achievements.Unlock("D21");
            if (Player.GrapplingHook.IsLatched) Achievements.Unlock("D22");

            // What hurt him last: an enemy (its record), or a part of one, or a thing of a room.
            string by = null, name = "";
            HealthManager owner = null;
            if (fresh && lastHit != null && (hero == null || lastHit != hero.gameObject))
            {
                name = lastHit.name;
                owner = lastHit.GetComponentInParent<HealthManager>();
                var info = owner != null ? EnemyStats.Of(owner) : null;
                by = info?.Record;
            }
            killer = by;
            Others(out int others, out int othersDead);
            if (others >= 1 && othersDead == others && Current() != null) Achievements.Unlock("M03");
            if (by == "Crawler") Achievements.Unlock("D11");
            if (by == "SuperSpitter") Achievements.AddTo("D12", "aspid deaths", 10);
            if (by == "CeilingDropper" || name.Contains("Ceiling Dropper")) Achievements.Unlock("D25");
            if (by == "GreyPrince" || name.Contains("Zote")) Achievements.Unlock("D13");
            if (by == "MegaJellyfish" || name.Contains("Jelly") && name.Contains("Zap")) Achievements.Unlock("D28");
            if (name.Contains("Dung Ball") || name.Contains("Dung Shot")) Achievements.Unlock("D08");
            if (scene.StartsWith("Hive") && (name.Contains("Bee") || by != null && by.Contains("Bee"))) Achievements.Unlock("D24");
            if (scene.StartsWith("White_Palace") && (name.Contains("saw") || name.Contains("Saw"))) Achievements.AddTo("D06", "saw deaths", 50);
            // The bosses' own things: the False Knight's falling rocks, the Mawlek's vomit, the infection's blobs, the
            // Radiance's beams; a boss's body coming down on him.
            if (scene.StartsWith("Crossroads_10") || scene.Contains("False_Knight") || scene.Contains("Failed_Champion"))
                if (name.Contains("Barrel") || name.Contains("Rock") || name.Contains("Debris")) Achievements.Unlock("D02");
            if (by == "Mawlek" && owner != null && lastHit != owner.gameObject) Achievements.Unlock("D09");
            if (name.Contains("Blob") || name.Contains("Infect") || name.Contains("Balloon") || name.Contains("Orange")) Achievements.Unlock("D10");
            if ((by == "FinalBoss" || scene.Contains("Radiance") || scene.StartsWith("Dream_Final")) && name.Contains("Beam")) Achievements.Unlock("D29");
            if (owner != null && lastHit == owner.gameObject && by != null && EnemyDrops.IsBoss(by))
            {
                var body = owner.GetComponent<Rigidbody2D>();
                if (body != null && body.linearVelocity.y < -4f) Achievements.Unlock("D27");
            }
            if (hero != null && NearBench(hero.transform.position)) Achievements.Unlock("D30");

            // A boss's fight: deaths at its hands, the fight just begun, the boss nearly gone, the player bare.
            var fight = Current();
            if (fight != null)
            {
                Achievements.AddTo("D15", "boss deaths:" + fight.Record, 10);
                if (Achievements.Count("boss deaths:" + fight.Record) >= 50) Achievements.Unlock("D16");
                if (Time.time - fight.NearAt <= 3f) Achievements.Unlock("D17");
                if (fight.Hm != null && fight.Hm.hp > 0 && fight.Hm.hp < fight.MaxHp * 0.01f) Achievements.Unlock("D18");
                if (mod != null && Bare(mod.Inventory)) Achievements.Unlock("D23");
            }
            Plugin.Log.LogInfo($"[成就] 死亡来源：{(fresh ? $"{name}（{by ?? "非敌人"}），伤害类型 {lastHazard}，摔落 {lastFall}，中毒 {poisoned}" : "无近期受伤")}，场景 {scene}");
        }
    }
}
