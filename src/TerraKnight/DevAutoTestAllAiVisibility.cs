using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.EnemyAi;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        sealed class AiVisibilityRow
        {
            public string Key, Name, Category, Scene, Source, Status = "pending", Snapshot;
            public bool Attached, Seen, NativeWake, EarlyExit;
            public int Samples, InvisibleTakeovers, Failures, EngagedSamples;
            public float LongestHidden, LongestHiddenAfterShown, Observed;
            public List<string> States = new List<string>();
            public List<string> Diagnostics = new List<string>();
        }

        static tk2dBaseSprite[] AiBodySprites(HealthManager hm)
        {
            var root = hm.GetComponent<tk2dBaseSprite>();
            if (root != null) return new[] { root };
            return hm.GetComponentsInChildren<tk2dBaseSprite>(true)
                .Where(s => s.GetComponentInParent<HealthManager>() == hm && !s.name.Contains("Flash") && !s.name.Contains("Shadow"))
                .ToArray();
        }

        static bool AiBodyShown(HealthManager hm)
        {
            return AiBodySprites(hm).Any(s => s.gameObject.activeInHierarchy && s.GetComponent<Renderer>() is Renderer r && r.enabled
                && s.color.a > .05f && Mathf.Abs(s.transform.lossyScale.x) > .05f && Mathf.Abs(s.transform.lossyScale.y) > .05f);
        }

        static string AiVisibilityState(HealthManager hm)
        {
            return string.Join(";", hm.GetComponentsInChildren<PlayMakerFSM>(true)
                .Where(f => f.enabled).Select(f => f.FsmName + "=" + f.ActiveStateName + "/held=" + f.Fsm.ManualUpdate))
                + " | " + string.Join(";", AiBodySprites(hm).Select(s => s.name + "/on=" + s.gameObject.activeInHierarchy + "/render="
                    + s.GetComponent<Renderer>()?.enabled + "/alpha=" + s.color.a.ToString("F2") + "/scale=" + s.transform.lossyScale
                    + "/clip=" + s.GetComponent<tk2dSpriteAnimator>()?.CurrentClip?.name));
        }

        BossKit AiVisibleBossKit(string record)
        {
            // A hidden phase-two body can already have a kit while phase one is still alive.
            // Prefer the rendered fighting body, never the phase-two Init placeholder.
            return Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None)
                .Where(k => k.Record == record && k.Hm != null && !k.Hm.isDead && k.gameObject.activeInHierarchy && !(bool)dormant.GetValue(k))
                .OrderByDescending(k => AiBodyShown(k.Hm))
                .ThenBy(k => record == "MageLord" && k.gameObject.name.Contains("Mage Lord 2") && k.StateNow == "Init")
                .ThenBy(k => bossWaits.IsMatch(k.StateNow ?? "")).FirstOrDefault();
        }

        IEnumerator AllAiVisibilityTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            World.GameDifficulty.Current = World.DifficultyLevel.Master;
            _ = Profiles.Count;
            var profiles = (Dictionary<string, Profile>)AccessTools.Field(typeof(Profiles), "all").GetValue(null);
            var bossRecords = typeof(BossKit).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(BossKit).IsAssignableFrom(t))
                .SelectMany(t => t.GetCustomAttributes(typeof(KitAttribute), false).Cast<KitAttribute>()).Select(a => a.Record).Distinct().OrderBy(x => x).ToArray();
            var worldBossKeys = BossArena.DevKeys().Where(k => bossRecords.Contains(k.Split('@')[0]))
                .Concat(bossRecords.Where(k => !BossArena.DevKeys().Any(a => a.Split('@')[0] == k))).Distinct().OrderBy(k => k).ToArray();
            var hallRooms = new Dictionary<string, string>((Dictionary<string, string>)AccessTools.Field(typeof(BestiaryHabitats), "GodhomeRooms").GetValue(null));
            hallRooms["Hornet@4"] = "GG_Hornet_2";
            hallRooms["MegaBeamMiner@2"] = "GG_Crystal_Guardian_2";
            var halls = hallRooms.Where(kv => bossRecords.Contains(kv.Key.Split('@')[0]) && BossArena.SceneOf(kv.Key) != kv.Value)
                .Select(kv => kv.Key + "#" + kv.Value).ToList();
            foreach (var pair in new[] { ("BigBuzzer", "GG_Vengefly_V"), ("BigFly", "GG_Gruz_Mother_V"), ("Mawlek", "GG_Brooding_Mawlek_V"),
                ("MantisLord", "GG_Mantis_Lords_V"), ("MimicSpider", "GG_Nosk_Hornet"), ("MegaJellyfish", "GG_Uumuu_V"),
                ("GhostAladar", "GG_Ghost_Gorb_V"), ("GhostMarkoth", "GG_Ghost_Markoth_V"), ("GhostMarmu", "GG_Ghost_Marmu_V"),
                ("GhostNoEyes", "GG_Ghost_No_Eyes_V"), ("GhostXero", "GG_Ghost_Xero_V"), ("MageKnight", "GG_Mage_Knight_V"),
                ("JarCollector", "GG_Collector_V"), ("MimicSpider", "GG_Nosk_V") }) halls.Add(pair.Item1 + "#" + pair.Item2);
            var bossKeys = worldBossKeys.Concat(halls).Distinct().OrderBy(k => k).ToArray();
            string category = Environment.GetEnvironmentVariable("TK_VIS_CATEGORY") ?? "common";
            int part = int.TryParse(Environment.GetEnvironmentVariable("TK_VIS_PART"), out var p) ? p : 0;
            int parts = int.TryParse(Environment.GetEnvironmentVariable("TK_VIS_PARTS"), out var n) ? n : 1;
            string only = Environment.GetEnvironmentVariable("TK_VIS_ONLY");
            bool native = Environment.GetEnvironmentVariable("TK_VIS_NATIVE") == "1";
            bool rephoto = Environment.GetEnvironmentVariable("TK_VIS_REPHOTO") == "1";
            if (rephoto) PlayerData.instance.hasLantern = true;
            var keys = category == "boss" ? bossKeys : profiles.Keys.ToArray();
            if (Environment.GetEnvironmentVariable("TK_VIS_HALL") == "1") keys = keys.Where(k => k.Contains("#")).ToArray();
            keys = keys.Where((k, i) => string.IsNullOrEmpty(only) ? i % parts == part : only.Split(';').Contains(k)).ToArray();
            var rows = keys.Select(k => new AiVisibilityRow { Key = k, Name = EnemyDrops.EnglishName(k.Split('@')[0]), Category = category }).ToList();
            string resultPath = Path.Combine(DevCopy.SaveDir, "all-ai-visibility.json");
            void Save() => File.WriteAllText(resultPath, JsonConvert.SerializeObject(new { Category = category, Part = part, Parts = parts,
                CommonInventory = profiles.Keys.ToArray(), BossRecords = bossRecords, BossKeys = bossKeys, Rows = rows }, Formatting.Indented));
            Save();
            Plugin.Log.LogInfo($"[ALL_VIS] inventory common={profiles.Count} bossRecords={bossRecords.Length} bossVariants={bossKeys.Length} batch={category}/{part}/{parts} selected={keys.Length}");
            string fixture = Environment.GetEnvironmentVariable("TK_VIS_FIXTURE") ?? "Town";
            if (category != "boss" && !native)
            {
                yield return GoTo(fixture, "left1");
                if (fixture == "Crossroads_21") HeroController.instance.transform.position = Clear(HeroController.instance, new Vector2(20f, 10f));
                yield return new WaitForSeconds(1.5f);
            }
            var hero = HeroController.instance;
            Vector3 home = hero.transform.position;
            foreach (var row in rows)
            {
                row.Status = "running"; Save();
                Plugin.Log.LogInfo("[ALL_VIS_BEGIN] " + category + " " + row.Key);
                HealthManager hm = null;
                Brain brain = null;
                BossKit kit = null;
                var pools = AiSpawnedPoolRoots();
                if (category == "boss")
                {
                    string record = row.Key.Split('#')[0].Split('@')[0];
                    if (row.Key.Contains("#"))
                    {
                        BossSceneController.SetupEvent = self => { self.BossLevel = 0; self.DreamReturnEvent = "DREAM RETURN"; };
                        PlayerData.instance.dreamReturnScene = "Town";
                        yield return GoTo(row.Key.Split('#')[1], "door_dreamEnter");
                        GameManager.instance.FadeSceneIn();
                    }
                    else if (BossArena.Has(row.Key)) BossArena.Fight(row.Key, true);
                    else
                    {
                        string scene = EnemyDrops.SceneOf(row.Key);
                        if (!string.IsNullOrEmpty(scene)) yield return GoTo(scene, EntryGateOf(scene));
                    }
                    float clearWaveAt = Time.realtimeSinceStartup + 8f;
                    int clearedWave = 0;
                    for (float until = Time.realtimeSinceStartup + 55f; Time.realtimeSinceStartup < until;)
                    {
                        kit = AiVisibleBossKit(record);
                        if (record == "MageLord" && kit != null && kit.gameObject.name.Contains("Mage Lord 2") && kit.StateNow == "Init") kit = null;
                        if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                        // World arenas may require ordinary pre-boss enemies to die first (Traitor Lord).
                        // Use native deaths, as a fighter does; never jump the battle controller to a state.
                        if (!GameManager.instance.IsInSceneTransition && Time.realtimeSinceStartup >= clearWaveAt)
                        {
                            clearWaveAt = Time.realtimeSinceStartup + 2f;
                            foreach (var enemy in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            {
                                var info = EnemyStats.Of(enemy);
                                if (!enemy.isDead && enemy.gameObject.activeInHierarchy && info != null && !info.Boss && info.Record != record
                                    && enemy.gameObject.scene.name.StartsWith(GameManager.instance.sceneName))
                                { enemy.Die(null, AttackTypes.Generic, true); clearedWave++; }
                            }
                        }
                        yield return null;
                    }
                    row.Source = "arena=" + row.Key + "; native pre-boss wave deaths=" + clearedWave;
                    hm = kit?.Hm;
                }
                else if (native)
                {
                    string scene = EnemyDrops.SceneOf(row.Key);
                    if (!string.IsNullOrEmpty(scene)) yield return GoTo(scene, EntryGateOf(scene));
                    yield return new WaitForSeconds(1f);
                    hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Where(h => h.gameObject.scene.name == scene && h.gameObject.activeInHierarchy && !h.isDead && UI.EnemyHover.RecordOf(h) == row.Key)
                        .OrderBy(h => h.name.Contains("(TK)")).ThenBy(h => h.name).FirstOrDefault();
                    brain = hm != null ? hm.GetComponent<Brain>() : null;
                    row.Source = "native-scene:" + scene;
                }
                else
                {
                    hero.transform.position = home;
                    hero.FaceRight();
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    BookSpawner.Spawn(row.Key);
                    for (float until = Time.realtimeSinceStartup + 16f; Time.realtimeSinceStartup < until;)
                    {
                        brain = Object.FindObjectsByType<Brain>(FindObjectsSortMode.None).FirstOrDefault(b => b.Record == row.Key && b.name.Contains("图鉴"));
                        if (brain != null) break;
                        yield return null;
                    }
                    hm = brain != null ? brain.GetComponent<HealthManager>() : null;
                    row.Source = BookSpawner.DevLast;
                }
                if (hm == null)
                {
                    row.Status = "unavailable";
                    row.Diagnostics.Add("No active native body / new AI; requires native-scene retry");
                    if (category == "boss")
                        foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Where(h => h.gameObject.scene.name == GameManager.instance.sceneName))
                            row.Diagnostics.Add(h.name + " record=" + UI.EnemyHover.RecordOf(h) + " active=" + h.gameObject.activeInHierarchy
                                + " kit=" + h.GetComponent<BossKit>()?.GetType().Name);
                    Save(); Plugin.Log.LogWarning("[ALL_VIS_RESULT] " + row.Key + " unavailable");
                    continue;
                }
                row.Attached = category == "boss" ? kit != null : brain != null;
                if (category == "boss" && Environment.GetEnvironmentVariable("TK_VIS_PACING") == "1")
                {
                    FxCheck("Boss readable timing " + row.Key, kit.TempoNow >= 1f && BossAi.AnimScale == 1f
                        && kit.Warn(.1f) >= .65f && kit.Antic(.1f) >= .45f,
                        "tempo=" + kit.TempoNow + " animation=" + BossAi.AnimScale + " warning=" + kit.Warn(.1f));
                }
                row.Scene = hm.gameObject.scene.name;
                int failsBefore = category == "boss" ? BossAi.DevFails : Brain.DevFails;
                int hpMax = hm.hp;
                Vector3 bossEntry = hero.transform.position;
                float started = Time.realtimeSinceStartup, end = started + (category == "boss" ? 24f : 10f);
                float hiddenFor = 0, awakeAt = started + 3f, nextWake = started + 1f;
                var states = new HashSet<string>();
                while (Time.realtimeSinceStartup < end && hm != null && !hm.isDead && hm.gameObject.activeInHierarchy)
                {
                    float now = Time.realtimeSinceStartup;
                    row.Observed = now - started;
                    bool shown = AiBodyShown(hm);
                    bool seenBefore = row.Seen;
                    row.Seen |= shown;
                    hiddenFor = shown ? 0 : hiddenFor + .1f;
                    row.LongestHidden = Mathf.Max(row.LongestHidden, hiddenFor);
                    if (seenBefore) row.LongestHiddenAfterShown = Mathf.Max(row.LongestHiddenAfterShown, hiddenFor);
                    row.Samples++;
                    string state = AiVisibilityState(hm);
                    if (states.Add(state)) row.States.Add(state);
                    if (brain != null && !shown && AiGet<bool>(brain, "seized") && !AiGet<bool>(brain, "hidden")) row.InvisibleTakeovers++;
                    Vector2 at;
                    if (category == "boss")
                    {
                        if (kit.Engaged) row.EngagedSamples++;
                        at = kit.ClampToArena(kit.Me, 3f);
                        float side = at.x > kit.Arena.center.x ? -1f : 1f;
                        at.x = Mathf.Clamp(at.x + side * (row.Observed < 12f ? 4f : 7f), kit.Arena.xMin + 1f, kit.Arena.xMax - 1f);
                        at.y = kit.GroundBelow(at) + 1.2f;
                        // God Tamer waits far offstage. Moving the hero to that provisional arena
                        // prevents its native entrance and arena watchdog from observing the real fight.
                        if (row.Key.Split('#')[0].Split('@')[0] == "LobsterLancer") at = bossEntry;
                        if (!float.IsNaN(at.y) && !float.IsInfinity(at.y)) hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                        // Exercise three health bands without killing the body or skipping native states.
                        int want = Mathf.Max(2, Mathf.RoundToInt(hpMax * (row.Observed < 8f ? .95f : row.Observed < 16f ? .55f : .15f)));
                        if (hm.hp > want) hm.hp = want;
                        if (now >= nextWake && !kit.Engaged)
                        {
                            nextWake = now + 2f;
                            if (!BossWake.Rouse(hm))
                            {
                                hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1,
                                    Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                                FSMUtility.SendEventToGameObject(hm.gameObject, "TAKE DAMAGE");
                            }
                        }
                        kit = AiVisibleBossKit(row.Key.Split('#')[0].Split('@')[0]) ?? kit;
                        if (kit.Hm != hm) { hm = kit.Hm; hiddenFor = 0; }
                    }
                    else
                    {
                        at = hm.transform.position;
                        // Show both chase and distant spawn-safety paths. Keep the original Hero actor reference.
                        bool spawnWait = hm.GetComponents<PlayMakerFSM>().Any(f => f.ActiveStateName == "Check Saver");
                        float distance = !shown || spawnWait ? 9f : row.Observed % 6f < 3f ? 4f : 7f;
                        at = native || !shown ? at + Vector2.left * distance : (Vector2)home;
                        hero.transform.position = Clear(hero, at);
                        World.EnemyAggro.Struck(hm, EnemyStats.Of(hm));
                        if (!row.Seen && now >= awakeAt)
                        {
                            awakeAt = float.MaxValue;
                            // Same external events used by native spawners and BookCopy, never force rendering.
                            foreach (var f in hm.GetComponents<PlayMakerFSM>())
                                foreach (var ev in new[] { "SPAWN", "BALLOON SPAWN", "WAKE", "START", "BATTLE START", "GO", "APPEAR" })
                                    if (f.Fsm.ActiveState?.Transitions.Any(t => t.EventName == ev) == true)
                                    { f.SendEvent(ev); row.NativeWake = true; break; }
                        }
                    }
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    mod.Stats.FullHeal();
                    if (shown && row.Snapshot == null && row.Observed > (rephoto ? 12f : 1f)
                        && (!rephoto || GameCameras.instance.cameraFadeFSM.ActiveStateName == "Normal"))
                    {
                        row.Snapshot = "allvis_" + category + "_" + row.Key.Replace('@', '_').Replace('#', '_');
                        Player.TerraCamera.DevLook = hm.transform.position;
                        yield return new WaitForEndOfFrame();
                        row.Diagnostics.Add("snapshot actor=" + hm.transform.position + " camera=" + GameCameras.instance.mainCamera.transform.position
                            + " viewport=" + GameCameras.instance.mainCamera.WorldToViewportPoint(hm.transform.position)
                            + " fade=" + GameCameras.instance.cameraFadeFSM.ActiveStateName);
                        Shot(row.Scene, row.Snapshot);
                        yield return new WaitForEndOfFrame();
                        Player.TerraCamera.DevLook = null;
                    }
                    yield return new WaitForSecondsRealtime(.1f);
                }
                row.EarlyExit = Time.realtimeSinceStartup < end;
                row.Failures = (category == "boss" ? BossAi.DevFails : Brain.DevFails) - failsBefore;
                row.Status = !row.Attached ? "no-new-ai" : !row.Seen ? "hidden" : row.Failures > 0 || row.InvisibleTakeovers > 2 ? "failed"
                    : row.LongestHiddenAfterShown > (category == "boss" ? 8f : 3f) ? "hidden-after-spawn" : "visible";
                if (hm != null && row.Status != "visible")
                {
                    foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true))
                        if (f.Fsm?.ActiveState != null) row.Diagnostics.Add(f.FsmName + ":" + f.ActiveStateName + " exits="
                            + string.Join(",", f.Fsm.ActiveState.Transitions.Select(t => t.EventName + "->" + t.ToState))
                            + " actions=" + string.Join(",", f.Fsm.ActiveState.Actions.Select(a => a.GetType().Name)));
                }
                Save();
                Plugin.Log.LogInfo($"[ALL_VIS_RESULT] {row.Key} {row.Status} observed={row.Observed:F1} hidden={row.LongestHidden:F1} invisibleTakeovers={row.InvisibleTakeovers} failures={row.Failures}");
                if (category != "boss")
                {
                    if (hm != null) Object.Destroy(hm.gameObject);
                    // Death scripts may spawn their explosion a few frames after the body goes away.
                    // Let that native chain settle before returning its pools, not after the next enemy spawns.
                    if (row.EarlyExit) yield return new WaitForSecondsRealtime(2f);
                    foreach (var a in Hostiles.Attack.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
                    foreach (var root in AiSpawnedPoolRoots()) if (root != null && !pools.Contains(root)) ObjectPool.Recycle(root);
                    yield return new WaitForSecondsRealtime(.2f);
                }
            }
            Save();
            Plugin.Log.LogInfo($"[ALL_VIS_COMPLETE] {category}/{part} visible={rows.Count(r => r.Status == "visible")}/{rows.Count}; hidden/failed/unavailable remain explicit in JSON");
        }
    }
}
