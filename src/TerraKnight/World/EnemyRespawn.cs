using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// Hallownest's ordinary enemies come back and come in twos (spec-hkenemies 2-3). When a
    /// scene appears, before any enemy starts, each eligible enemy gets an inactive template
    /// copy kept in that scene (so it never wakes), and a second one is set down beside it at
    /// once. Whenever one of them is gone (killed, or kept dead by the game until a bench), a new
    /// one is made from the template as soon as its time is up, out of sight: where the original
    /// stood when that is off screen, else just past the edge of the view along the same row.
    /// Bosses, arena and gauntlet waves, scripted and one-time enemies, the Path of Pain's and
    /// other pogo platforms, pooled spawner children and everything in Godhome, dreams, the
    /// Colosseum and the White Palace are left alone.
    /// </summary>
    internal sealed class EnemyRespawn : MonoBehaviour
    {
        public static EnemyRespawn Instance { get; private set; }
        /// <summary>Gameplay settings: enemies per original (2 = doubled), seconds before a dead one returns.</summary>
        public static int Multiplier = 2;
        public static float RespawnSeconds = 5f;
        /// <summary>At most this many slots per scene (each copy brings its own pools and FSMs).</summary>
        const int MaxSlotsPerScene = 70;
        /// <summary>The name mark of templates and the enemies made from them.</summary>
        const string TK = "(TK)";

        sealed class Slot
        {
            public string Scene;
            public GameObject Template;
            public Transform Parent;
            public bool HadParent;
            public Vector3 LocalPos;
            public Quaternion LocalRot;
            public Vector3 LocalScale;
            public GameObject Live;
            public float FreeSince = -1f;
            /// <summary>An extra copy not yet set down (its group isn't active yet).</summary>
            public bool Pending;
            /// <summary>The body (main collider): size, and its centre from the enemy's position, world units.</summary>
            public Vector2 Size, Offset;
            /// <summary>Dev: why it is waiting, if it is.</summary>
            public string Why;
            /// <summary>An extra copy whose place beside this original is still to be found (the scene's grid wasn't built when it was captured), and which one.</summary>
            public Slot BesideOf;
            public int Nth;
            /// <summary>No room was found for this extra copy: it goes.</summary>
            public bool NoRoom;
        }

        /// <summary>Every enemy the slots have put down (and the originals): whose corpses go (<see cref="CorpseCleanup"/>).</summary>
        readonly HashSet<int> ours = new HashSet<int>();

        public bool Owns(GameObject go) => go != null && ours.Contains(go.GetInstanceID());

        readonly List<Slot> slots = new List<Slot>();
        readonly Dictionary<string, GameObject> holders = new Dictionary<string, GameObject>();
        float nextCheck;

        /// <summary>Dev: captured, extra copies placed, respawned so far; and why the last scene's enemies were left alone.</summary>
        public static int DevCaptured, DevDoubled, DevRespawned, DevOffScreen, DevNoRoom;
        public static Vector2 DevLastSpawn;
        public static Rect DevView => ViewRect();
        public static readonly Dictionary<string, int> DevRejected = new Dictionary<string, int>();

        static readonly AccessTools.FieldRef<HealthManager, GameObject> battleScene = AccessTools.FieldRefAccess<HealthManager, GameObject>("battleScene");
        static readonly AccessTools.FieldRef<HealthManager, GameObject> killedObj = AccessTools.FieldRefAccess<HealthManager, GameObject>("sendKilledToObject");
        static readonly AccessTools.FieldRef<HealthManager, string> killedName = AccessTools.FieldRefAccess<HealthManager, string>("sendKilledToName");
        static readonly AccessTools.FieldRef<HealthManager, bool> godfinder = AccessTools.FieldRefAccess<HealthManager, bool>("showGodfinderIcon");
        static readonly System.Reflection.FieldInfo unlockBoss = AccessTools.Field(typeof(HealthManager), "unlockBossScene");
        static readonly System.Reflection.FieldInfo hpScale = AccessTools.Field(typeof(HealthManager), "hpScale");
        static System.Reflection.FieldInfo hpLevel1;
        static readonly AccessTools.FieldRef<EnemyDeathEffects, GameObject> corpse = AccessTools.FieldRefAccess<EnemyDeathEffects, GameObject>("corpse");

        public static EnemyRespawn Create(Transform parent)
        {
            var go = new GameObject("Terra Enemy Respawn");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<EnemyRespawn>();
            return Instance;
        }

        void Awake() => USceneManager.sceneLoaded += OnSceneLoaded;
        void OnDestroy() => USceneManager.sceneLoaded -= OnSceneLoaded;

        /// <summary>A scene the game itself loaded (a transition): captured before anything starts. Neighbours come through <see cref="Capture"/> from LiveWorld.</summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (TerraMod.Instance == null) return;
            if (LiveWorld.Instance != null && LiveWorld.Instance.LoadingNeighbour(scene.name)) return;
            Capture(scene);
        }

        public int SlotCount => slots.Count;

        /// <summary>Dev: captures given up because the scene was being unloaded as it was admitted.</summary>
        public static int DevAbandoned;

        // ------------------------------------------------------------------ capture

        /// <summary>Templates (and the extra copies) for every eligible enemy of a scene that has just appeared.</summary>
        public void Capture(Scene scene)
        {
            if (Multiplier < 1 && RespawnSeconds <= 0f) return;
            string name = scene.name;
            DevRejected.Clear();
            if (!SceneAllowed(name))
            {
                Reject("场景");
                return;
            }
            // The same scene still loaded and admitted again (a neighbour that became the room, or
            // back again): its slots, templates and copies are all still there.
            if (holders.TryGetValue(name, out var kept) && kept != null && kept.scene == scene) return;
            slots.RemoveAll(s => s.Scene == name);
            GameObject holder = null;
            int count = 0;
            var found = new List<HealthManager>();
            foreach (var root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<HealthManager>(true));
            foreach (var hm in found)
            {
                if (count >= MaxSlotsPerScene) break;
                string why = Eligible(hm);
                if (why != null)
                {
                    Reject(why);
                    continue;
                }
                if (holder == null)
                {
                    holder = new GameObject("TK Enemy Templates");
                    holder.SetActive(false);
                    try
                    {
                        USceneManager.MoveGameObjectToScene(holder, scene);
                    }
                    catch (System.ArgumentException)
                    {
                        // The scene is already on its way out again (a neighbour unloaded as it came in):
                        // nothing of it to keep, and no slot of it made yet.
                        Object.Destroy(holder);
                        DevAbandoned++;
                        return;
                    }
                    holders[name] = holder;
                }
                var template = MakeTemplate(hm, holder.transform);
                var tr = hm.transform;
                Body(hm, out var size, out var offset);
                var slot = new Slot
                {
                    Scene = name, Template = template, Parent = tr.parent, HadParent = tr.parent != null,
                    LocalPos = tr.localPosition, LocalRot = tr.localRotation, LocalScale = tr.localScale, Live = hm.gameObject,
                    Size = size, Offset = offset,
                };
                slots.Add(slot);
                ours.Add(hm.gameObject.GetInstanceID());
                count++;
                DevCaptured++;
                // The extra copies, beside the original where there is room for one. A scene the
                // game itself loads is captured before its grid exists: their places are found
                // once it does.
                var at = CellGrid.WorldToCell(tr.position);
                bool gridReady = TerraWorld.RegionAt(at.x, at.y) != null;
                for (int k = 1; k < Multiplier && count < MaxSlotsPerScene; k++)
                {
                    var shift = Vector3.zero;
                    if (gridReady && !Beside(slot, k, out shift))
                    {
                        Reject("旁边放不下第二只");
                        continue;
                    }
                    var extra = new Slot
                    {
                        Scene = name, Template = template, Parent = slot.Parent, HadParent = slot.HadParent,
                        LocalPos = slot.LocalPos + shift, LocalRot = slot.LocalRot, LocalScale = slot.LocalScale, Pending = true,
                        Size = size, Offset = offset, BesideOf = gridReady ? null : slot, Nth = k,
                    };
                    slots.Add(extra);
                    count++;
                    if (gridReady && ParentActive(extra))
                    {
                        Spawn(extra);
                        DevDoubled++;
                    }
                }
            }
            if (count > 0) Plugin.Log.LogInfo($"[刷怪] {name}：{count} 个刷新点（模板 {found.Count} 个敌人中合格的），排除：{DescribeRejects()}");
        }

        static void Reject(string why) => DevRejected[why] = DevRejected.TryGetValue(why, out int n) ? n + 1 : 1;

        static string DescribeRejects()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in DevRejected) sb.Append(kv.Key).Append('×').Append(kv.Value).Append(' ');
            return sb.Length == 0 ? "无" : sb.ToString().Trim();
        }

        /// <summary>The enemy's body: its main collider's size, and that box's centre from the enemy's position (world units).</summary>
        static void Body(HealthManager hm, out Vector2 size, out Vector2 offset)
        {
            var col = hm.GetComponent<Collider2D>();
            var tr = hm.transform;
            var scale = new Vector2(Mathf.Abs(tr.lossyScale.x), Mathf.Abs(tr.lossyScale.y));
            size = new Vector2(1f, 1f);
            offset = Vector2.zero;
            if (col is BoxCollider2D box)
            {
                size = Vector2.Scale(box.size, scale);
                offset = (Vector2)tr.TransformVector(box.offset);
            }
            else if (col is CircleCollider2D circle)
            {
                size = Vector2.one * circle.radius * 2f * Mathf.Max(scale.x, scale.y);
                offset = (Vector2)tr.TransformVector(circle.offset);
            }
            else if (col != null && col.bounds.size.sqrMagnitude > 0.01f)
            {
                size = col.bounds.size;
                offset = (Vector2)col.bounds.center - (Vector2)tr.position;
            }
            size = new Vector2(Mathf.Clamp(size.x, 0.4f, 8f), Mathf.Clamp(size.y, 0.4f, 8f));
        }

        /// <summary>
        /// A place beside the enemy for an extra copy (in the parent's space), so the pair doesn't
        /// stand in one spot: along the surface it stands or clings on (a wall crawler's wall),
        /// with room for its body there and, for one on a floor, a floor at the same height under
        /// it. None where there isn't one (a moss creeper pressed into a corner): never in a wall.
        /// </summary>
        static bool Beside(Slot origin, int k, out Vector3 local)
        {
            Vector2 pos = SpawnPoint(origin);
            var rot = origin.HadParent && origin.Parent != null ? origin.Parent.rotation * origin.LocalRot : origin.LocalRot;
            Vector2 along = ((Vector2)(rot * Vector3.right)).normalized;
            Vector2 down = -((Vector2)(rot * Vector3.up)).normalized;
            bool upright = Mathf.Abs(Mathf.DeltaAngle(0f, rot.eulerAngles.z)) <= 10f;
            var size = origin.Size;
            var offset = origin.Offset;
            float? floor0 = upright ? FloorBelow(pos + offset, size.y * 0.5f + 1.5f) : null;
            float step = Mathf.Max(1.2f, size.x + 0.3f) * ((k + 1) / 2);
            float first = k % 2 == 1 ? 1f : -1f;
            foreach (float side in new[] { first, -first })
                foreach (float scale in new[] { 1f, 1.6f, 0.6f })
                {
                    var p = pos + along * (side * step * scale);
                    if (!Fits(p, size, offset, upright, floor0, down)) continue;
                    Vector3 world = p - pos;
                    local = origin.HadParent && origin.Parent != null ? origin.Parent.InverseTransformVector(world) : world;
                    return true;
                }
            local = default;
            return false;
        }

        /// <summary>Room for the body at a position (its upper three quarters clear of blocks: the feet may touch the floor), standing as it stood.</summary>
        static bool Fits(Vector2 p, Vector2 size, Vector2 offset, bool upright, float? floor0, Vector2 down)
        {
            var c = p + offset;
            if (BoxSolid(new Vector2(c.x, c.y + size.y * 0.125f), new Vector2(size.x * 0.8f, size.y * 0.75f))) return false;
            if (upright && floor0.HasValue)
            {
                float? f = FloorBelow(c, size.y * 0.5f + 1.5f);
                return f.HasValue && Mathf.Abs(f.Value - floor0.Value) <= 0.6f;
            }
            // Clinging to a wall or ceiling: that surface goes on under it.
            return upright || TerraWorld.IsSolidAt(c + down * (size.y * 0.5f + 0.3f));
        }

        static bool BoxSolid(Vector2 centre, Vector2 size)
        {
            var lo = CellGrid.WorldToCell(centre - size * 0.5f);
            var hi = CellGrid.WorldToCell(centre + size * 0.5f);
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                    if (TerraWorld.IsSolid(x, y)) return true;
            return false;
        }

        /// <summary>
        /// The inactive copy every later enemy of the slot is made from: no persistence record (it
        /// would tie its deaths to the original's), no premade corpse, alive, in no arena, its
        /// FSMs switched on again (a neighbour's were held), marked "(TK)".
        /// </summary>
        static GameObject MakeTemplate(HealthManager hm, Transform holder)
        {
            var t = Instantiate(hm.gameObject, holder, true);
            t.name = hm.gameObject.name + TK;
            var pbi = t.GetComponent<PersistentBoolItem>();
            if (pbi != null) DestroyImmediate(pbi);
            var ede = hm.GetComponent<EnemyDeathEffects>();
            var oc = ede != null ? corpse(ede) : null;
            if (oc != null && oc.transform.parent == hm.transform)
            {
                int i = oc.transform.GetSiblingIndex();
                if (i < t.transform.childCount) DestroyImmediate(t.transform.GetChild(i).gameObject);
            }
            var thm = t.GetComponent<HealthManager>();
            thm.SetIsDead(false);
            thm.SetBattleScene(null);
            foreach (var f in t.GetComponentsInChildren<PlayMakerFSM>(true)) f.enabled = true;
            if (!t.activeSelf) t.SetActive(true);
            return t;
        }

        // ------------------------------------------------------------------ the rules (spec-hkenemies 2.2)

        static readonly string[] scenePrefixes = { "GG_", "Dream_", "Grimm_", "Room_Colosseum", "White_Palace", "Room_Final_Boss", "Cutscene_" };
        static readonly string[] sceneAllowedGG = { "GG_Pipeway", "GG_Waterways" };
        static readonly HashSet<string> sceneExact = new HashSet<string>
        {
            "Abyss_09", "Abyss_15", "Room_Fungus_Shaman",
            // World battle scenes (a "Battle Control" FSM; some enlist enemies outside it).
            "Crossroads_04", "Crossroads_08", "Crossroads_09", "Crossroads_22", "Ruins1_05", "Ruins1_09", "Ruins1_23", "Ruins1_31b", "Ruins2_01", "Ruins2_01_b",
            "Ruins2_03", "Ruins2_03b", "Ruins2_09", "Fungus1_21", "Fungus1_32", "Fungus2_05", "Fungus3_05", "Fungus3_10", "Fungus3_39", "Deepnest_33",
            "Abyss_17", "Waterways_09", "Waterways_13",
            // Boss rooms that are ordinary scenes.
            "Fungus1_20_v02", "Fungus1_29", "Deepnest_32", "Hive_05", "Abyss_19", "Mines_32", "Ruins_House_02", "Deepnest_40", "Fungus1_35", "Fungus2_32",
            "Deepnest_East_10", "Deepnest_East_16", "Crossroads_11_alt", "Crossroads_ShamanTemple", "Fungus1_28", "Crossroads_45",
        };

        static bool SceneAllowed(string s)
        {
            if (sceneExact.Contains(s) || s.EndsWith("_boss") || s.EndsWith("_boss_defeated")) return false;
            foreach (var a in sceneAllowedGG) if (s.StartsWith(a)) return true;
            foreach (var p in scenePrefixes) if (s.StartsWith(p)) return false;
            return !BossSceneController.IsBossScene;
        }

        static readonly HashSet<string> recordNever = new HashSet<string>
        {
            "AbyssTendril", "AcidFlyer", "AcidWalker", "BigBuzzer", "BigCentipede", "BigFly", "BindingSeal", "BlackKnight", "Blobble", "Blocker", "ColFlyingSentry",
            "ColHopper", "ColMiner", "ColMosquito", "ColRoller", "ColShield", "ColWorm", "CrystalCrawler", "DungDefender", "EggSac", "ElectricMage", "FalseKnight",
            "FinalBoss", "FlameBearerLarge", "FlameBearerMed", "FlameBearerSmall", "FlukeMother", "GhostAladar", "GhostGalien", "GhostHu", "GhostMarkoth", "GhostMarmu",
            "GhostNoEyes", "GhostXero", "GodseekerMask", "GorgeousHusk", "GreyPrince", "Grimm", "GrubMimic", "Hatchling", "HealthScuttler", "HiveKnight", "HollowKnight",
            "HollowKnightPrime", "Hornet", "HunterMark", "InfectedKnight", "JarCollector", "LazyFlyer", "LobsterLancer", "MageKnight", "MageLord", "MantisLord", "Mawlek",
            "MegaBeamMiner", "MegaJellyfish", "MegaMossCharger", "MenderBug", "MimicSpider", "NailBros", "Nailsage", "NightmareGrimm", "Oblobble", "OrangeScuttler",
            "Paintmaster", "PalaceFly", "PaleLurker", "Pigeon", "PrayerSlug", "RoyalGuard", "Sibling", "TraitorLord", "VoidIdol_1", "VoidIdol_2", "VoidIdol_3",
            "WhiteDefender", "WhiteRoyal", "Worm", "ZapBug", "Zote", "ZotelingBalloon", "ZotelingBuzzer", "ZotelingHopper", "Dummy",
        };

        static readonly string[] namesNever =
        {
            "Hollow Shade", "Shade Sibling", "Flamebearer ", "Ghost Warrior ", "Zote", "White Palace Fly", "Royal Gaurd", "Mender Bug", "Lazy Flyer Enemy", "Prayer Slug",
            "Egg Sac", "Grub Mimic", "Gorgeous Husk", "Blocker", "Zombie Myla", "Mega ", "Giant Fly", "Giant Buzzer", "Mawlek Body", "Mawlek Col", "Hornet Boss ",
            "Hornet Nosk", "Mantis Lord", "Mantis Traitor Lord", "Black Knight ", "Mage Lord", "Dream Mage Lord", "Mage Knight", "Jar Collector", "Dung Defender",
            "White Defender", "Fluke Mother", "Mimic Spider", "Hive Knight", "Infected Knight", "Lost Kin", "Radiance", "Absolute Radiance", "Hollow Knight Boss",
            "HK Prime", "Grimm Boss", "Nightmare Grimm Boss", "Real Bat", "Lobster", "Lancer", "Pale Lurker", "Oro", "Mato", "Sly Boss", "Sheo Boss", "False Knight",
            "Head", "Tinger", "Acid Walker", "Acid Flyer", "Crystal Crawler", "Shell", "Cap Hit", "Hatcher Baby Spawner", "Hiveling Spawner", "Baby Centipede Spawner",
            "Fluke Fly Spawner", "Mage Balloon Spawner", "fluke_baby_", "Zombie Beam Miner Rematch", "Colosseum_", "Parasite Balloon Spawner", "Lil Jellyfish", "Corpse ",
        };

        static readonly HashSet<string> parentsNever = new HashSet<string>
        {
            "Battle Scene", "Battle Scene v2", "Battle Control", "Battle Main", "Battle Sub", "Battle Music", "Battle Scene Ore", "Mantis Battle", "Colosseum Manager",
            "Waves", "Pre Battle Enemies", "Completed", "Uncompleted", "Activated", "Activate on contact Trigger", "Activate on Hero contact Trigger", "Spitter Summon v2",
            "Hatcher Summon", "Dragonfly Summon", "Giant Hopper Summon", "Hopper Spawn", "Spawn", "Spawn 2", "Spawner", "Fly Spawn", "Shade Spawn", "Scream Spawn Mosquitos",
            "Shiny Spawner", "Hatcher Cage", "Centipede Cage", "Cloth Enemies", "Cloth Entry", "Lords Defeated Group", "HeavyMantisEnabler", "Zombie Spider Wake",
            "First Crawler", "Prayer Room", "Siblings", "Balloons", "Balloon Spawner", "Entry Object", "Boss Control", "Boss Holder", "Grimm Control", "Grimm Scene",
            "Grimm Bats", "Lurker Control", "Warrior", "Buzzer Dummy", "False Knight New", "False Knight Dream", "Audience", "Grub Scene",
        };

        /// <summary>Null when the enemy may be copied and respawned; else why not.</summary>
        static string Eligible(HealthManager hm)
        {
            var go = hm.gameObject;
            // Our own templates and copies.
            if (go.name.EndsWith(TK)) return "复制体";
            if (!go.activeSelf) return "未激活";
            if (hm.hasSpecialDeath) return "特殊死亡";
            if (hm.deathReset) return "对象池";
            if (hm.isDead) return "已死";
            if (godfinder(hm) || (unlockBoss != null && unlockBoss.GetValue(hm) != null)) return "首领";
            if (BossScaled(hm)) return "神居首领";
            if (battleScene(hm) != null) return "竞技场";
            if (killedObj(hm) != null) return "击杀通知";
            string kn = killedName(hm);
            if (!string.IsNullOrEmpty(kn) && kn != BaseName(go.name)) return "击杀通知";
            var pbi = go.GetComponent<PersistentBoolItem>();
            if (pbi != null && !pbi.semiPersistent) return "一次性";
            if (go.GetComponent<EnemyDeathEffects>() == null) return "无死亡效果";
            if (go.GetComponent<BigBouncer>() != null || (hm.IsInvincible && (hm.InvincibleFromDirection == 4 || hm.InvincibleFromDirection == 7))) return "踩跳平台";
            if (go.GetComponent<DeactivateIfPlayerdataTrue>() != null || go.GetComponent<DeactivateIfPlayerdataFalse>() != null) return "剧情";
            for (var p = go.transform.parent; p != null; p = p.parent)
            {
                if (p.GetComponent<HealthManager>() != null) return "敌人的一部分";
                foreach (var f in p.GetComponents<PlayMakerFSM>())
                    if (f.FsmName == "Battle Control") return "竞技场";
                string pn = BaseName(p.name);
                if (parentsNever.Contains(pn) || pn.StartsWith("Wave ") || pn.Contains("Summon") || pn.StartsWith("Mushroom Brawler") || pn.StartsWith("Grub Mimic Top")) return "剧本父物体";
            }
            string rec = UI.EnemyHover.RecordOf(hm);
            if (rec == null || recordNever.Contains(rec)) return "名单排除";
            foreach (var n in namesNever)
                if (go.name.StartsWith(n)) return "名单排除";
            if (go.name.EndsWith(" Col") || go.name.EndsWith(" R")) return "名单排除";
            return null;
        }

        static bool BossScaled(HealthManager hm)
        {
            if (hpScale == null) return false;
            var scale = hpScale.GetValue(hm);
            if (scale == null) return false;
            if (hpLevel1 == null) hpLevel1 = AccessTools.Field(scale.GetType(), "level1");
            return hpLevel1 != null && (int)hpLevel1.GetValue(scale) > 0;
        }

        static string BaseName(string n)
        {
            int cut = n.IndexOf(" (");
            return (cut > 0 ? n.Substring(0, cut) : n).Trim();
        }

        // ------------------------------------------------------------------ respawning

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.isPaused || !GameHooks.IsGameplay(gm)) return;
            slots.RemoveAll(s => s.Template == null || s.NoRoom || (s.HadParent && s.Parent == null));
            var view = ViewRect();
            Vector2 heroPos = hero.transform.position;
            foreach (var s in slots)
            {
                try
                {
                    Step(s, view, heroPos);
                }
                catch (System.Exception e)
                {
                    s.Why = "出错";
                    if (!loggedError)
                    {
                        loggedError = true;
                        Plugin.Log.LogError($"[刷怪] {s.Scene} 的刷新点出错：{e}");
                    }
                }
            }
        }

        bool loggedError;

        void Step(Slot s, Rect view, Vector2 heroPos)
        {
            s.Why = null;
            if (DeferredFsms.HoldScene == s.Scene) { s.Why = "场景还在初始化"; return; }
            // A LAN game where another player's game runs this scene's enemies: that game respawns them (Net.Enemies).
            if (Net.Enemies.RemoteOwned(s.Scene)) { s.Why = "联机：由别的玩家负责"; return; }
            if (!ParentActive(s)) { s.Why = "所在的组没激活"; return; }
            if (s.Pending)
            {
                if (s.BesideOf != null)
                {
                    var at = CellGrid.WorldToCell(SpawnPoint(s.BesideOf));
                    if (TerraWorld.RegionAt(at.x, at.y) == null) { s.Why = "地形还没建好"; return; }
                    if (!Beside(s.BesideOf, s.Nth, out var shift))
                    {
                        s.NoRoom = true;
                        DevNoRoom++;
                        return;
                    }
                    s.LocalPos = s.BesideOf.LocalPos + shift;
                    s.BesideOf = null;
                }
                // An extra copy whose group has just switched on comes at once.
                Spawn(s);
                DevDoubled++;
                return;
            }
            bool gone = s.Live == null || (s.Live.GetComponent<HealthManager>() is HealthManager lhm && lhm.isDead) || !s.Live.activeInHierarchy && Dead(s.Live);
            if (!gone)
            {
                s.FreeSince = -1f;
                return;
            }
            if (s.FreeSince < 0f) s.FreeSince = Time.time;
            // The Battle potion halves the wait, Calming and Invisibility lengthen it (spawn rates ×0.5, ×1.3, ×1.2).
            if (RespawnSeconds <= 0f || Time.time - s.FreeSince < RespawnSeconds * Player.PotionBuffs.RespawnFactor) { s.Why = "等时间"; return; }
            // Due: back at once, out of sight. Where it stood when that is off screen; else
            // just past the edge of the view along its own row, so it walks or flies in.
            var home = SpawnPoint(s);
            if (!view.Contains(home) && Vector2.Distance(home, heroPos) >= 8f)
            {
                Spawn(s);
                DevRespawned++;
                return;
            }
            var off = OffScreen(s, home, view);
            if (off.HasValue)
            {
                Spawn(s, off.Value);
                DevRespawned++;
                DevOffScreen++;
                return;
            }
            // The room is all in view (or nowhere past the edge to stand): where it stood, unless on top of the player.
            if (Vector2.Distance(home, heroPos) < 5f) { s.Why = "离主角太近、屏幕外又没地方"; return; }
            Spawn(s);
            DevRespawned++;
        }

        /// <summary>Dev: the slots of a scene by state ("活着", "等时间", ...), and how many of the live ones stand in a block.</summary>
        public string DevReport(string scene, out int alive, out int waiting, out int inWall)
        {
            alive = waiting = inWall = 0;
            var by = new Dictionary<string, int>();
            foreach (var s in slots)
            {
                if (s.Scene != scene) continue;
                bool live = s.Live != null && s.Live.activeInHierarchy && !(s.Live.GetComponent<HealthManager>() is HealthManager h && h.isDead);
                string k = live ? "活着" : s.Pending ? "待放" : s.Why ?? "刚死";
                by[k] = by.TryGetValue(k, out int n) ? n + 1 : 1;
                if (live)
                {
                    alive++;
                    var c = (Vector2)s.Live.transform.position + s.Offset;
                    if (BoxSolid(new Vector2(c.x, c.y + s.Size.y * 0.125f), new Vector2(s.Size.x * 0.6f, s.Size.y * 0.5f))) inWall++;
                }
                else waiting++;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in by) sb.Append(kv.Key).Append('×').Append(kv.Value).Append(' ');
            return sb.ToString().Trim();
        }

        /// <summary>Dev: the live enemies the slots of a scene put down (originals and copies).</summary>
        public List<HealthManager> DevLive(string scene)
        {
            var list = new List<HealthManager>();
            foreach (var s in slots)
                if (s.Scene == scene && s.Live != null && s.Live.activeInHierarchy && s.Live.GetComponent<HealthManager>() is HealthManager h && !h.isDead) list.Add(h);
            return list;
        }

        /// <summary>
        /// A place just out of view on the enemy's own row, in its own scene, the nearer side
        /// first: one that stood on a floor gets a floor at about the same height with room
        /// above it, one in the air open air. Not for one clinging to a wall or ceiling.
        /// </summary>
        static Vector2? OffScreen(Slot s, Vector2 home, Rect view)
        {
            var rot = s.HadParent && s.Parent != null ? s.Parent.rotation * s.LocalRot : s.LocalRot;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, rot.eulerAngles.z)) > 10f) return null;
            float? floor = FloorBelow(home + s.Offset, s.Size.y * 0.5f + 1.5f);
            bool walker = floor.HasValue || TerraWorld.IsSolidAt(home + s.Offset);
            float lift = floor.HasValue ? home.y - floor.Value : 0.3f;
            bool leftFirst = home.x - view.xMin < view.xMax - home.x;
            for (int k = 0; k < 24; k++)
                for (int side = 0; side < 2; side++)
                {
                    bool left = (side == 0) == leftFirst;
                    var at = new Vector2(left ? view.xMin - 1f - k : view.xMax + 1f + k, home.y);
                    var p = Place(s, at, walker, lift);
                    if (p.HasValue) return p;
                }
            return null;
        }

        static Vector2? Place(Slot s, Vector2 at, bool walker, float lift)
        {
            var c = at + s.Offset;
            if (!walker) return Room(s.Scene, c, c.y - s.Size.y * 0.5f, c.y + s.Size.y * 0.5f, s.Size.x) ? at : (Vector2?)null;
            // A floor a step or two up or down from its own.
            float? floor = FloorBelow(c + Vector2.up * 1.5f, 4f + s.Size.y * 0.5f);
            if (!floor.HasValue) return null;
            var p = new Vector2(at.x, floor.Value + lift);
            float top = p.y + s.Offset.y + s.Size.y * 0.5f;
            return Room(s.Scene, p + s.Offset, floor.Value + s.Size.y * 0.25f, top, s.Size.x) ? p : (Vector2?)null;
        }

        /// <summary>The top of the first block below an open point, within range.</summary>
        static float? FloorBelow(Vector2 p, float range)
        {
            const float c = CellGrid.CellSize;
            var cell = CellGrid.WorldToCell(p);
            if (TerraWorld.IsSolid(cell.x, cell.y)) return null;
            int steps = Mathf.CeilToInt(range / c);
            for (int i = 1; i <= steps; i++)
                if (TerraWorld.IsSolid(cell.x, cell.y - i)) return (cell.y - i + 1) * c;
            return null;
        }

        /// <summary>The point lies in the scene's own ground, and a body's width is clear of blocks between the heights.</summary>
        static bool Room(string scene, Vector2 p, float bottom, float top, float width = 1f)
        {
            var cell = CellGrid.WorldToCell(p);
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            if (region == null || region.SceneName != scene) return false;
            float half = Mathf.Max(0.5f, width * 0.4f);
            var lo = CellGrid.WorldToCell(new Vector2(p.x - half, bottom + 0.05f));
            var hi = CellGrid.WorldToCell(new Vector2(p.x + half, top));
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                    if (TerraWorld.IsSolid(x, y)) return false;
            return true;
        }

        static bool Dead(GameObject go)
        {
            var hm = go.GetComponent<HealthManager>();
            return hm == null || hm.isDead;
        }

        static bool ParentActive(Slot s) => !s.HadParent || (s.Parent != null && s.Parent.gameObject.activeInHierarchy);

        static Vector2 SpawnPoint(Slot s) => s.HadParent && s.Parent != null ? (Vector2)s.Parent.TransformPoint(s.LocalPos) : (Vector2)s.LocalPos;

        /// <summary>A new enemy from the slot's template where the slot stands (in the template's scene, so its FSMs keep that scene's frame).</summary>
        void Spawn(Slot s, Vector2? at = null)
        {
            var go = Instantiate(s.Template, s.Template.transform.parent, false);
            go.name = s.Template.name;
            go.transform.SetParent(s.HadParent ? s.Parent : null, false);
            go.transform.localPosition = s.LocalPos;
            go.transform.localRotation = s.LocalRot;
            go.transform.localScale = s.LocalScale;
            if (at.HasValue) go.transform.position = new Vector3(at.Value.x, at.Value.y, go.transform.position.z);
            DevLastSpawn = go.transform.position;
            ours.Add(go.GetInstanceID());
            s.Live = go;
            s.Pending = false;
            s.FreeSince = -1f;
        }

        /// <summary>The camera's view on the gameplay plane (z = 0), a little enlarged.</summary>
        static Rect ViewRect()
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return new Rect();
            Vector2 Corner(float x, float y)
            {
                var ray = cam.ViewportPointToRay(new Vector3(x, y, 0f));
                float t = -ray.origin.z / ray.direction.z;
                return ray.origin + ray.direction * t;
            }
            var a = Corner(0f, 0f);
            var b = Corner(1f, 1f);
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f);
        }

        /// <summary>
        /// A LAN game: a copy the scene's owner has (made from its template there) that this game hasn't made: one
        /// from this game's template of that name, in a free slot of it if there is one, where the owner's is.
        /// </summary>
        public GameObject NetSpawn(string scene, string name, Transform parent, Vector2 at)
        {
            Slot free = null, any = null;
            foreach (var s in slots)
            {
                if (s.Scene != scene || s.Template == null || s.Template.name != name) continue;
                any = any ?? s;
                bool gone = s.Live == null || (s.Live.GetComponent<HealthManager>() is HealthManager h && h.isDead) || (!s.Live.activeInHierarchy && Dead(s.Live));
                if (gone && !s.Pending)
                {
                    free = s;
                    break;
                }
            }
            if (free != null)
            {
                Spawn(free, at);
                return free.Live;
            }
            if (any == null) return null;
            var go = Instantiate(any.Template, any.Template.transform.parent, false);
            go.name = any.Template.name;
            go.transform.SetParent(parent != null ? parent : any.HadParent ? any.Parent : null, false);
            go.transform.localPosition = any.LocalPos;
            go.transform.localRotation = any.LocalRot;
            go.transform.localScale = any.LocalScale;
            go.transform.position = new Vector3(at.x, at.y, go.transform.position.z);
            ours.Add(go.GetInstanceID());
            return go;
        }

        public void Clear()
        {
            slots.Clear();
            holders.Clear();
            ours.Clear();
        }
    }
}
