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
    /// one is made from the template when its time is up, where the original stood. The time goes
    /// by how tough the enemy is (its own health in the game, elites longest), and for its last
    /// seconds a faint ghost of the enemy shows on the spot, so the player sees it coming; none
    /// comes back with the player standing right there, and no ghost shows over him meanwhile
    /// (it shows again once he has stepped away, a second before the enemy comes).
    /// Bosses, arena and gauntlet waves, scripted and one-time enemies, the Path of Pain's and
    /// other pogo platforms, pooled spawner children, the Dirtcarvers (they dig away and come
    /// up again whole by themselves, the game sending more out of its pool) and everything in
    /// Godhome, dreams, the Colosseum and the Path of Pain are left alone. The White Palace's come
    /// back but one for one (more only with the Water Candle).
    /// </summary>
    internal sealed class EnemyRespawn : MonoBehaviour
    {
        public static EnemyRespawn Instance { get; private set; }
        /// <summary>Gameplay settings: enemies per original (2 = doubled); how many times its own wait (<see cref="DelayFor"/>) a dead one takes to return (0: never).</summary>
        public static int Multiplier = 2;
        public static float Scale = 1f;
        /// <summary>Dev: every slot's wait in seconds instead of its own (negative: off).</summary>
        public static float DevFixedSeconds = -1f;
        /// <summary>How long before a respawn its spot shows the enemy's ghost.</summary>
        const float TelegraphSeconds = 3f;
        /// <summary>None comes back with the player nearer its spot than this (units): the ghost waits.</summary>
        const float KeepClear = 4f;
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
            /// <summary>Its own wait before coming back, seconds; and the ghost on its spot while that runs out.</summary>
            public float Delay;
            public GameObject Ghost;
            public Renderer GhostRenderer;
            /// <summary>When the ghost last came up (it shows a second at least before the enemy).</summary>
            public float GhostSince;
            /// <summary>This time's way from its spot to where it comes back (blocks or platforms the player put on the spot: the nearest clear place).</summary>
            public Vector2 Shift;
            /// <summary>Since when its live enemy has been out of the world (Time.time; negative: it isn't).</summary>
            public float LostSince = -1f;
        }

        /// <summary>Every enemy the slots have put down (and the originals): whose corpses go (<see cref="CorpseCleanup"/>).</summary>
        readonly HashSet<int> ours = new HashSet<int>();

        public bool Owns(GameObject go) => go != null && ours.Contains(go.GetInstanceID());

        readonly List<Slot> slots = new List<Slot>();
        readonly Dictionary<string, GameObject> holders = new Dictionary<string, GameObject>();
        float nextCheck;

        /// <summary>Dev: captured, extra copies placed, respawned so far; and why the last scene's enemies were left alone.</summary>
        public static int DevCaptured, DevDoubled, DevRespawned, DevOffScreen, DevNoRoom, DevMoved, DevLost;
        public static Vector2 DevLastSpawn;
        /// <summary>Dev: each extra copy's distance at its placing from the nearest of its original and the copies before it.</summary>
        public static readonly List<float> DevGaps = new List<float>();
        public static Rect DevView => ViewRect();
        public static readonly Dictionary<string, int> DevRejected = new Dictionary<string, int>();
        /// <summary>Dev: the copies' FSMs left off as the originals have them (enemy/FSM).</summary>
        public static readonly HashSet<string> DevKeptOff = new HashSet<string>();

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
            if (Multiplier < 1 && Scale <= 0f && DevFixedSeconds < 0f) return;
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
            var eligible = new List<HealthManager>();
            foreach (var hm in found)
            {
                if (eligible.Count >= MaxSlotsPerScene) break;
                string why = Eligible(hm);
                if (why != null) Reject(why);
                else eligible.Add(hm);
            }
            // The copies shared out evenly under the cap (a full room with the Water Candle: each enemy a few, not the
            // first ones all theirs and the rest none).
            int share = eligible.Count > 0 ? Mathf.Max(1, MaxSlotsPerScene / eligible.Count) : 1;
            foreach (var hm in eligible)
            {
                if (count >= MaxSlotsPerScene) break;
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
                float delay = DelayFor(hm);
                var template = MakeTemplate(hm, holder.transform);
                var tr = hm.transform;
                Body(hm, out var size, out var offset);
                var slot = new Slot
                {
                    Scene = name, Template = template, Parent = tr.parent, HadParent = tr.parent != null,
                    LocalPos = tr.localPosition, LocalRot = tr.localRotation, LocalScale = tr.localScale, Live = hm.gameObject,
                    Size = size, Offset = offset, Delay = delay,
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
                // The Water Candle in the hotbar (Items.CarriedFurniture) sets how many times over. The White Palace's
                // come back one for one: more only with the candle.
                int copies = (Single(name) || singleKinds.Contains(UI.EnemyHover.RecordOf(hm) ?? "") ? 1 : Multiplier) * Items.CarriedFurniture.CandleFactor;
                copies = Mathf.Min(copies, share);
                var taken = new List<Vector3>();
                for (int k = 1; k < copies && count < MaxSlotsPerScene; k++)
                {
                    var shift = Vector3.zero;
                    if (gridReady && !Beside(slot, k, out shift, taken))
                    {
                        Reject("旁边放不下第二只");
                        continue;
                    }
                    var extra = new Slot
                    {
                        Scene = name, Template = template, Parent = slot.Parent, HadParent = slot.HadParent,
                        LocalPos = slot.LocalPos + shift, LocalRot = slot.LocalRot, LocalScale = slot.LocalScale, Pending = true,
                        Size = size, Offset = offset, BesideOf = gridReady ? null : slot, Nth = k, Delay = delay,
                    };
                    slots.Add(extra);
                    taken.Add(shift);
                    count++;
                    if (gridReady && ParentActive(extra))
                    {
                        Spawn(extra);
                        DevDoubled++;
                    }
                }
            }
            if (count > 0) Plugin.Log.LogInfo($"[刷怪] {name}：{count} 个刷新点（模板 {found.Count} 个敌人中合格的），排除：{DescribeRejects()}{(DevKeptOff.Count > 0 ? "；复制体里照原版关着的状态机：" + string.Join("、", DevKeptOff) : "")}");
            DevKeptOff.Clear();
        }

        static void Reject(string why) => DevRejected[why] = DevRejected.TryGetValue(why, out int n) ? n + 1 : 1;

        /// <summary>
        /// How long a dead enemy of this kind takes to come back while the player stays in its room,
        /// seconds: by its own health in the game (a crawlid or a vengefly 8, a husk 15-20, a husk guard
        /// 65), the big ones three minutes and the elites five (a husk guard came back too soon).
        /// </summary>
        static float DelayFor(HealthManager hm)
        {
            if (Items.EnemyStats.Unstoppable(Items.EnemyStats.Of(hm))) return 300f;
            int hp = hm.hp;
            return hp <= 10 ? 30f : hp <= 25 ? 45f : hp <= 50 ? 90f : 180f;
        }

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
        /// it; for a tiktik or a crawlid the game's ground within the reach it feels for it
        /// (<see cref="Crawls"/>). None where there isn't one (a moss creeper pressed into a corner): never in a wall.
        /// </summary>
        static bool Beside(Slot origin, int k, out Vector3 local, List<Vector3> taken = null)
        {
            Vector2 pos = SpawnPoint(origin);
            var rot = origin.HadParent && origin.Parent != null ? origin.Parent.rotation * origin.LocalRot : origin.LocalRot;
            Vector2 along = ((Vector2)(rot * Vector3.right)).normalized;
            Vector2 down = -((Vector2)(rot * Vector3.up)).normalized;
            bool upright = Mathf.Abs(Mathf.DeltaAngle(0f, rot.eulerAngles.z)) <= 10f;
            var size = origin.Size;
            var offset = origin.Offset;
            float? floor0 = upright ? FloorBelow(pos + offset, size.y * 0.5f + 1.5f) : null;
            // Well apart (the user's 10-25 "不要聚在一起"): a couple of bodies between them, a little uneven, the same
            // each time for this copy; a flyer's also a little above or below.
            float jitter = 0.85f + 0.3f * Mathf.Repeat(k * 0.618f + Mathf.Abs(pos.x) * 0.37f + Mathf.Abs(pos.y) * 0.11f, 1f);
            float step = Mathf.Max(2.4f, size.x * 1.6f + 0.8f) * ((k + 1) / 2) * jitter;
            float first = k % 2 == 1 ? 1f : -1f;
            bool crawls = Crawls(origin, out var toGround, out float reach);
            bool flies = upright && !floor0.HasValue && !crawls;
            float lift = flies ? (k % 3 - 1) * Mathf.Max(0.8f, size.y * 0.6f) : 0f;
            float apart = Mathf.Max(1f, Mathf.Max(size.x, size.y) * 0.9f);
            foreach (float side in new[] { first, -first })
                foreach (float scale in new[] { 1f, 0.7f, 1.4f, 0.45f, 1.9f })
                    foreach (float up in lift != 0f ? new[] { lift, 0f } : new[] { 0f })
                    {
                        var p = pos + along * (side * step * scale) - down * up;
                        Vector3 world = p - pos;
                        if (Near(world, taken, origin, apart)) continue;
                        if (!Fits(p, size, offset, upright, floor0, down)) continue;
                        if (crawls && !Felt(p + offset, toGround, reach)) continue;
                        local = origin.HadParent && origin.Parent != null ? origin.Parent.InverseTransformVector(world) : world;
                        float gap = ((Vector2)world).magnitude;
                        if (taken != null)
                            foreach (var tk in taken)
                                gap = Mathf.Min(gap, ((Vector2)((origin.HadParent && origin.Parent != null ? origin.Parent.TransformVector(tk) : tk) - world)).magnitude);
                        if (DevGaps.Count < 2000) DevGaps.Add(gap);
                        return true;
                    }
            local = default;
            return false;
        }

        /// <summary>A place within a body of the original or of another copy of it (their shifts, in the parent's space).</summary>
        static bool Near(Vector3 world, List<Vector3> taken, Slot origin, float apart)
        {
            if (((Vector2)world).sqrMagnitude < apart * apart) return true;
            if (taken == null) return false;
            foreach (var t in taken)
            {
                Vector3 w = origin.HadParent && origin.Parent != null ? origin.Parent.TransformVector(t) : t;
                if (((Vector2)(w - world)).sqrMagnitude < apart * apart) return true;
            }
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

        /// <summary>
        /// A surface crawler: a tiktik (the game's Climber) or a crawlid (its Crawler). They walk by
        /// the game's own colliders, not by the grid: a tiktik sticks to the ground it finds within
        /// two units under it as it starts and turns at once where it feels none within one, so one
        /// set off its ledge turns round and round the empty air. The way to its ground (its own down,
        /// flipped for one hung upside down by its scale) and how far it reaches for it.
        /// </summary>
        static bool Crawls(Slot s, out Vector2 toGround, out float reach)
        {
            toGround = Vector2.zero;
            reach = 0f;
            if (s.Template == null) return false;
            bool climber = s.Template.GetComponent<Climber>() != null;
            if (!climber && s.Template.GetComponent<Crawler>() == null) return false;
            Upright(s, out _, out var down);
            float sy = Mathf.Sign(s.LocalScale.y) * (s.HadParent && s.Parent != null ? Mathf.Sign(s.Parent.lossyScale.y) : 1f);
            toGround = down * sy;
            reach = climber ? 2f : s.Size.y * 0.5f + 1f;
            return true;
        }

        /// <summary>The game's ground (its terrain layer, the blocks put in it too) within reach of a body's centre that way.</summary>
        static bool Felt(Vector2 body, Vector2 toGround, float reach) => ClimberSurfaces.Surface(body, toGround, reach).collider != null;

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
        /// would tie its deaths to the original's), no premade corpse, alive, in no arena, each FSM
        /// on or off as the scene has it on the original (a neighbour's held ones as the scene had
        /// them), marked "(TK)". An FSM the scene keeps off stays off: the Husk Sentry's Fall Check,
        /// on in a copy, flipped it between falling and landing every frame, frozen in place and
        /// starting its startle and patrol sounds over and over.
        /// </summary>
        static GameObject MakeTemplate(HealthManager hm, Transform holder)
        {
            var own = hm.GetComponentsInChildren<PlayMakerFSM>(true);
            // Copied in its own colours, not half see-through from a steady second (Items.SteadyFade).
            Items.SteadyFade.End(hm.gameObject);
            var t = Instantiate(hm.gameObject, holder, true);
            t.name = hm.gameObject.name + TK;
            var fade = t.GetComponent<Items.SteadyFade>();
            if (fade != null) DestroyImmediate(fade);
            // Paired one for one while the copy is still whole (its premade corpse, taken out below, has FSMs too).
            var copies = t.GetComponentsInChildren<PlayMakerFSM>(true);
            for (int i = 0; i < copies.Length; i++)
            {
                copies[i].enabled = copies.Length == own.Length ? DeferredFsms.WasOn(own[i]) ?? own[i].enabled : true;
                if (!copies[i].enabled) DevKeptOff.Add($"{BaseName(hm.name)}/{copies[i].FsmName}");
            }
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
            if (!t.activeSelf) t.SetActive(true);
            return t;
        }

        // ------------------------------------------------------------------ the rules (spec-hkenemies 2.2)

        static readonly string[] scenePrefixes = { "GG_", "Dream_", "Grimm_", "Room_Colosseum", "Room_Final_Boss", "Cutscene_" };
        static readonly string[] sceneAllowedGG = { "GG_Pipeway", "GG_Waterways" };
        static readonly HashSet<string> sceneExact = new HashSet<string>
        {
            "Abyss_09", "Abyss_15", "Room_Fungus_Shaman",
            // The Path of Pain.
            "White_Palace_17", "White_Palace_18", "White_Palace_19", "White_Palace_20",
            // World battle scenes (a "Battle Control" FSM; some enlist enemies outside it).
            "Crossroads_04", "Crossroads_08", "Crossroads_09", "Crossroads_22", "Ruins1_05", "Ruins1_09", "Ruins1_23", "Ruins1_31b", "Ruins2_01", "Ruins2_01_b",
            // (Fungus3_39 isn't one: its battle counts only the two Mantis Warriors under its Battle Scene, which
            // Eligible leaves alone; its Mossy Vagabonds and Mossflies stand outside it, ordinary enemies.)
            "Ruins2_03", "Ruins2_03b", "Ruins2_09", "Fungus1_21", "Fungus1_32", "Fungus2_05", "Fungus3_05", "Fungus3_10", "Deepnest_33",
            "Abyss_17", "Waterways_09", "Waterways_13",
            // Boss rooms that are ordinary scenes.
            "Fungus1_20_v02", "Fungus1_29", "Deepnest_32", "Hive_05", "Abyss_19", "Mines_32", "Ruins_House_02", "Deepnest_40", "Fungus1_35", "Fungus2_32",
            "Deepnest_East_10", "Deepnest_East_16", "Crossroads_11_alt", "Fungus1_28", "Crossroads_45",
            // (The Ancestral Mound, Crossroads_ShamanTemple, isn't one: its Elder Baldur is left alone by its kind, its
            // Baldurs and Vengeflies are ordinary enemies.)
        };

        /// <summary>A scene whose enemies come back but don't come in twos: the White Palace (the King's dream).</summary>
        static bool Single(string scene) => scene.StartsWith("White_Palace");

        /// <summary>Kinds that come back but one for one, whatever the room: the Flukefey (the Waterways' rooms swarm with them already).</summary>
        static readonly HashSet<string> singleKinds = new HashSet<string> { "Flukefly" };

        static bool SceneAllowed(string s)
        {
            if (sceneExact.Contains(s) || s.EndsWith("_boss") || s.EndsWith("_boss_defeated")) return false;
            foreach (var a in sceneAllowedGG) if (s.StartsWith(a)) return true;
            foreach (var p in scenePrefixes) if (s.StartsWith(p)) return false;
            return !BossSceneController.IsBossScene;
        }

        static readonly HashSet<string> recordNever = new HashSet<string>
        {
            "AbyssTendril", "AcidFlyer", "AcidWalker", "BabyCentipede", "BigBuzzer", "BigCentipede", "BigFly", "BindingSeal", "BlackKnight", "Blobble", "Blocker", "ColFlyingSentry",
            "ColHopper", "ColMiner", "ColMosquito", "ColRoller", "ColShield", "ColWorm", "CrystalCrawler", "DungDefender", "EggSac", "ElectricMage", "FalseKnight",
            "FinalBoss", "FlameBearerLarge", "FlameBearerMed", "FlameBearerSmall", "FlukeMother", "GhostAladar", "GhostGalien", "GhostHu", "GhostMarkoth", "GhostMarmu",
            "GhostNoEyes", "GhostXero", "GodseekerMask", "GorgeousHusk", "GreyPrince", "Grimm", "GrubMimic", "Hatchling", "HealthScuttler", "HiveKnight", "HollowKnight",
            "HollowKnightPrime", "Hornet", "HunterMark", "InfectedKnight", "JarCollector", "LazyFlyer", "LobsterLancer", "MageKnight", "MageLord", "MantisLord", "Mawlek",
            "MegaBeamMiner", "MegaJellyfish", "MegaMossCharger", "MenderBug", "MimicSpider", "NailBros", "Nailsage", "NightmareGrimm", "Oblobble", "OrangeScuttler",
            "Paintmaster", "PaleLurker", "Pigeon", "PrayerSlug", "Sibling", "TraitorLord", "VoidIdol_1", "VoidIdol_2", "VoidIdol_3",
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
                {
                    if (f.FsmName == "Battle Control") return "竞技场";
                    if (GroupSwitchedOff(f)) return "原版关掉的一组";
                }
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

        /// <summary>
        /// A group the game shows or hides by a PlayerData record (the Forgotten Crossroads' "Activate Infected", and its
        /// kind in Dirtmouth, the Black Egg Temple, Crystal Peak: "PD Bool Name", "Active If Bool True"), switching all its
        /// children on or off a frame after the scene starts: true for the group this save doesn't show (the uninfected
        /// husks once the Crossroads is infected, the infected ones before). Its enemies are never copied: a copy set down
        /// after the switch (the extra ones wait for the scene's grid) would be left on, and come back each time it died.
        /// </summary>
        static bool GroupSwitchedOff(PlayMakerFSM f)
        {
            var vars = f.FsmVariables;
            if (vars == null) return false;
            string record = null;
            bool? whenTrue = null;
            foreach (var s in vars.StringVariables)
                if (s.Name == "PD Bool Name") record = s.Value;
            foreach (var b in vars.BoolVariables)
                if (b.Name == "Active If Bool True") whenTrue = b.Value;
            var pd = PlayerData.instance;
            if (string.IsNullOrEmpty(record) || whenTrue == null || pd == null) return false;
            return pd.GetBool(record) != whenTrue.Value;
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
            AnimateGhosts();
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.isPaused || !GameHooks.IsGameplay(gm)) return;
            foreach (var s in slots)
                if (s.Ghost != null && (s.Template == null || s.NoRoom || (s.HadParent && s.Parent == null))) HideGhost(s);
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
                    var taken = new List<Vector3>();
                    foreach (var o in slots)
                        if (o != s && o.Template == s.BesideOf.Template && o != s.BesideOf && o.BesideOf == null && o.Nth > 0 && !o.NoRoom)
                            taken.Add(o.LocalPos - s.BesideOf.LocalPos);
                    if (!Beside(s.BesideOf, s.Nth, out var shift, taken))
                    {
                        s.NoRoom = true;
                        DevNoRoom++;
                        return;
                    }
                    s.LocalPos = s.BesideOf.LocalPos + shift;
                    s.BesideOf = null;
                }
                // An extra copy whose group has just switched on comes at once (beside blocks put on its place).
                if (!FindClear(s, SpawnPoint(s), out s.Shift)) s.Shift = Vector2.zero;
                Spawn(s, s.Shift == Vector2.zero ? (Vector2?)null : SpawnPoint(s) + s.Shift);
                DevDoubled++;
                return;
            }
            bool gone = s.Live == null || (s.Live.GetComponent<HealthManager>() is HealthManager lhm && lhm.isDead) || !s.Live.activeInHierarchy && Dead(s.Live);
            // One lost out of the world for a while (dropped through the ground, flung off the map): it won't
            // be back by itself and it holds its spot, so it goes and the spot comes back as for a kill.
            if (!gone && Lost(s))
            {
                Plugin.Log.LogInfo($"[刷怪] {BaseName(s.Live.name)} 掉出了地图（{(Vector2)s.Live.transform.position:F1}），按死了重新等");
                Destroy(s.Live);
                s.Live = null;
                gone = true;
                DevLost++;
            }
            if (!gone)
            {
                s.FreeSince = -1f;
                if (s.Ghost != null) HideGhost(s);
                return;
            }
            if (s.FreeSince < 0f) s.FreeSince = Time.time;
            if (DevFixedSeconds < 0f && Scale <= 0f) { s.Why = "不刷新"; return; }
            // The Battle potion halves the wait, Calming and Invisibility lengthen it (spawn rates ×0.5, ×1.3, ×1.2).
            float wait = (DevFixedSeconds >= 0f ? DevFixedSeconds : s.Delay * Scale) * Player.PotionBuffs.RespawnFactor;
            float left = wait - (Time.time - s.FreeSince);
            if (left > TelegraphSeconds) { s.Why = "等时间"; return; }
            // Its last seconds: its ghost where it will stand, then the enemy there (not on top of the player).
            // Blocks or platforms the player has put on its spot: the nearest clear place round it, found
            // afresh whenever the place the ghost shows on is built over.
            var home = SpawnPoint(s);
            // The player on the spot: neither the enemy nor its see-through ghost over him; both wait for him to step away.
            if (Vector2.Distance(home + s.Shift + s.Offset, heroPos) < KeepClear)
            {
                HideGhost(s);
                s.Why = "主角站在出生点旁";
                return;
            }
            if (s.Ghost == null || Built(s, home + s.Shift, s.Shift == Vector2.zero))
            {
                HideGhost(s);
                if (!FindClear(s, home, out s.Shift)) { s.Why = "出生点被方块堵住，附近也没空位"; return; }
            }
            var spot = home + s.Shift;
            if (Vector2.Distance(spot + s.Offset, heroPos) < KeepClear) { s.Why = "主角站在出生点旁"; return; }
            if (s.Ghost == null) s.GhostSince = Time.time;
            ShowGhost(s, spot);
            if (left > 0f || Time.time - s.GhostSince < 1f) { s.Why = "快刷出来了"; return; }
            HideGhost(s);
            if (s.Shift != Vector2.zero) DevMoved++;
            Spawn(s, s.Shift == Vector2.zero ? (Vector2?)null : spot);
            DevRespawned++;
        }

        // ------------------------------------------------------------------ room to come back in

        /// <summary>How it stood: upright (rotated 10° at most), and which way is down for it.</summary>
        static bool Upright(Slot s, out Vector2 along, out Vector2 down)
        {
            var rot = s.HadParent && s.Parent != null ? s.Parent.rotation * s.LocalRot : s.LocalRot;
            along = ((Vector2)(rot * Vector3.right)).normalized;
            down = -((Vector2)(rot * Vector3.up)).normalized;
            return Mathf.Abs(Mathf.DeltaAngle(0f, rot.eulerAngles.z)) <= 10f;
        }

        /// <summary>
        /// Something in the way of its body at a position: a block or a platform (its feet may touch
        /// a floor). On its own spot only what the player put there counts: the room's own ground
        /// is where the game set it, even where it presses into it.
        /// </summary>
        static bool Built(Slot s, Vector2 p, bool placedOnly)
        {
            bool upright = Upright(s, out _, out _);
            var c = p + s.Offset;
            var centre = upright ? new Vector2(c.x, c.y + s.Size.y * 0.125f) : c;
            var size = upright ? new Vector2(s.Size.x * 0.8f, s.Size.y * 0.75f) : s.Size * 0.7f;
            var lo = CellGrid.WorldToCell(centre - size * 0.5f);
            var hi = CellGrid.WorldToCell(centre + size * 0.5f);
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    var k = TerraWorld.Get(x, y);
                    if (TerraKnight.World.Cells.IsPlatform(k) || Cells.Blocks(k) && !(placedOnly && k == CellKind.Original)) return true;
                }
            return false;
        }

        /// <summary>
        /// Where it comes back: its own spot, or, with blocks or platforms the player has put on it,
        /// the nearest clear place round it, as Terraria's enemies never come in inside tiles: room
        /// for its body; for one that stood on the room's ground a floor under it (a block or a
        /// platform, on top of what was built or beside it); for one clinging to a wall or ceiling
        /// that surface going on under it; in its own room. False when there is none near.
        /// </summary>
        static bool FindClear(Slot s, Vector2 home, out Vector2 shift)
        {
            shift = Vector2.zero;
            if (!Built(s, home, true)) return true;
            bool upright = Upright(s, out var along, out var down);
            bool walker = upright && GroundBelow(home + s.Offset, s.Size.y * 0.5f + 1.5f);
            bool crawls = Crawls(s, out var toGround, out float reach);
            const float c = CellGrid.CellSize;
            float best = float.MaxValue;
            for (int j = -4; j <= 20; j++)
                for (int i = -20; i <= 20; i++)
                {
                    var off = along * (i * c) - down * (j * c);
                    float d = off.sqrMagnitude;
                    if (d >= best) continue;
                    var p = home + off;
                    var cell = CellGrid.WorldToCell(p + s.Offset);
                    var region = TerraWorld.RegionAt(cell.x, cell.y);
                    if (region == null || region.SceneName != s.Scene) continue;
                    if (Built(s, p, false)) continue;
                    var body = p + s.Offset;
                    if (walker && !FloorUnder(body, s.Size.y * 0.5f + 1.5f)) continue;
                    if (!upright && !TerraWorld.IsSolidAt(body + down * (s.Size.y * 0.5f + 0.3f))) continue;
                    if (crawls && !Felt(body, toGround, reach)) continue;
                    best = d;
                    shift = off;
                }
            return best < float.MaxValue;
        }

        /// <summary>The room's own ground below a point, within range (what the enemy stood on, whatever is built on it now).</summary>
        static bool GroundBelow(Vector2 p, float range)
        {
            var cell = CellGrid.WorldToCell(p);
            int steps = Mathf.CeilToInt(range / CellGrid.CellSize);
            for (int i = 1; i <= steps; i++)
                if (TerraWorld.Get(cell.x, cell.y - i) == CellKind.Original) return true;
            return false;
        }

        /// <summary>Something to stand on below an open point, within range: a block or a platform.</summary>
        static bool FloorUnder(Vector2 p, float range)
        {
            var cell = CellGrid.WorldToCell(p);
            int steps = Mathf.CeilToInt(range / CellGrid.CellSize);
            for (int i = 1; i <= steps; i++)
            {
                var k = TerraWorld.Get(cell.x, cell.y - i);
                if (Cells.Blocks(k) || TerraKnight.World.Cells.IsPlatform(k)) return true;
            }
            return false;
        }

        /// <summary>Its live enemy has been well out of every part of the world for 3 seconds.</summary>
        static bool Lost(Slot s)
        {
            if (s.Live == null || !s.Live.activeInHierarchy || !SceneBuilt(s.Scene)) { s.LostSince = -1f; return false; }
            Vector2 at = s.Live.transform.position;
            const float margin = 4f;
            bool inWorld = false;
            foreach (var p in new[] { at, at + new Vector2(0f, margin), at - new Vector2(0f, margin), at + new Vector2(margin, 0f), at - new Vector2(margin, 0f) })
            {
                var cell = CellGrid.WorldToCell(p);
                if (TerraWorld.RegionAt(cell.x, cell.y) != null) { inWorld = true; break; }
            }
            if (inWorld) { s.LostSince = -1f; return false; }
            if (s.LostSince < 0f) s.LostSince = Time.time;
            return Time.time - s.LostSince >= 3f;
        }

        /// <summary>The scene's grid is up (before it is, nothing of it is "out of the world").</summary>
        static bool SceneBuilt(string scene)
        {
            foreach (var t in TerrainSystem.All)
                if (t != null && t.Region != null && t.Region.SceneName == scene) return true;
            return false;
        }

        // ------------------------------------------------------------------ the ghost before a respawn

        /// <summary>
        /// The enemy's own sprite, faint and pulsing, on the spot it will come back at: the
        /// template's tk2d sprite (or plain sprite) as it stands, in its scene (so it goes with it).
        /// </summary>
        void ShowGhost(Slot s, Vector2 home)
        {
            if (s.Ghost != null) return;
            var go = new GameObject("TK Respawn Ghost");
            try
            {
                USceneManager.MoveGameObjectToScene(go, s.Template.scene);
            }
            catch (System.ArgumentException)
            {
                Destroy(go);
                return;
            }
            var rootT = s.Template.transform;
            var parentRot = s.HadParent && s.Parent != null ? s.Parent.rotation : Quaternion.identity;
            var parentScale = s.HadParent && s.Parent != null ? s.Parent.lossyScale : Vector3.one;
            var rootScale = Vector3.Scale(parentScale, s.LocalScale);
            Renderer made = null;
            var tk = s.Template.GetComponentInChildren<tk2dSprite>(true);
            if (tk != null && tk.Collection != null)
            {
                Place(go.transform, tk.transform, rootT, home, parentRot * s.LocalRot, rootScale);
                var g = go.AddComponent<tk2dSprite>();
                g.SetSprite(tk.Collection, tk.spriteId);
                made = g.GetComponent<Renderer>();
                CopySorting(tk.GetComponent<Renderer>(), made);
            }
            else if (s.Template.GetComponentInChildren<SpriteRenderer>(true) is SpriteRenderer sr && sr.sprite != null)
            {
                Place(go.transform, sr.transform, rootT, home, parentRot * s.LocalRot, rootScale);
                var g = go.AddComponent<SpriteRenderer>();
                g.sprite = sr.sprite;
                g.flipX = sr.flipX;
                made = g;
                CopySorting(sr, made);
            }
            else go.transform.position = new Vector3(home.x, home.y, rootT.position.z);
            // tk2d gives a sprite the collider its definition has (tk2dBaseSprite.UpdateCollider): the
            // ghost is only to be seen, nothing stands on it, bumps it or hits it.
            foreach (var col in go.GetComponents<Collider2D>()) DestroyImmediate(col);
            foreach (var col in go.GetComponents<Collider>()) DestroyImmediate(col);
            s.Ghost = go;
            s.GhostRenderer = made;
            SetGhostAlpha(s, 0.3f);
        }

        /// <summary>The ghost where the sprite will be: the sprite's place under the template, put at the spawn point.</summary>
        static void Place(Transform ghost, Transform sprite, Transform root, Vector2 home, Quaternion rot, Vector3 rootScale)
        {
            var local = sprite == root ? Vector3.zero : root.InverseTransformPoint(sprite.position);
            var spriteScale = sprite == root ? Vector3.one : sprite.localScale;
            ghost.position = new Vector3(home.x, home.y, root.position.z) + rot * Vector3.Scale(local, rootScale);
            ghost.rotation = rot * (sprite == root ? Quaternion.identity : sprite.localRotation);
            ghost.localScale = Vector3.Scale(rootScale, spriteScale);
        }

        static void CopySorting(Renderer from, Renderer to)
        {
            if (from == null || to == null) return;
            to.sortingLayerID = from.sortingLayerID;
            to.sortingOrder = from.sortingOrder;
        }

        static void SetGhostAlpha(Slot s, float a)
        {
            if (s.GhostRenderer is SpriteRenderer sr) sr.color = new Color(1f, 1f, 1f, a);
            else if (s.Ghost != null && s.Ghost.GetComponent<tk2dSprite>() is tk2dSprite tk) tk.color = new Color(1f, 1f, 1f, a);
        }

        float nextDust;

        /// <summary>Every frame: the ghosts pulse between faint and half seen, with a sparkle now and then.</summary>
        void AnimateGhosts()
        {
            bool dust = Time.time >= nextDust;
            if (dust) nextDust = Time.time + 0.15f;
            float a = 0.2f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7f));
            foreach (var s in slots)
            {
                if (s.Ghost == null) continue;
                SetGhostAlpha(s, a);
                if (!dust || Effects.TerraParticles.Instance == null) continue;
                var c = (Vector2)s.Ghost.transform.position + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)) * Mathf.Max(1f, s.Size.x);
                Effects.TerraParticles.Instance.NewDust(Effects.TerraParticles.ToPx(c) - new Vector2(4f, 4f), 8, 8, 15, 0f, -0.5f, 150, 0.8f);
            }
        }

        static void HideGhost(Slot s)
        {
            if (s.Ghost != null) Destroy(s.Ghost);
            s.Ghost = null;
            s.GhostRenderer = null;
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

        /// <summary>Dev: ghosts showing now; each slot of a scene with its own wait.</summary>
        public int DevGhosts
        {
            get
            {
                int n = 0;
                foreach (var s in slots) if (s.Ghost != null) n++;
                return n;
            }
        }

        /// <summary>Dev: colliders on the ghosts showing now (none should be).</summary>
        public int DevGhostColliders
        {
            get
            {
                int n = 0;
                foreach (var s in slots)
                    if (s.Ghost != null) n += s.Ghost.GetComponentsInChildren<Collider2D>(true).Length + s.Ghost.GetComponentsInChildren<Collider>(true).Length;
                return n;
            }
        }

        /// <summary>Dev: a live enemy's slot: its spot (the enemy's position), its body's size and centre from there, whether it stood upright on the room's ground.</summary>
        public bool DevBody(HealthManager hm, out Vector2 home, out Vector2 size, out Vector2 offset, out bool walker)
        {
            foreach (var s in slots)
                if (s.Live == hm.gameObject)
                {
                    home = SpawnPoint(s);
                    size = s.Size;
                    offset = s.Offset;
                    walker = Upright(s, out _, out _) && GroundBelow(home + s.Offset, s.Size.y * 0.5f + 1.5f);
                    return true;
                }
            home = size = offset = Vector2.zero;
            walker = false;
            return false;
        }

        /// <summary>Dev: a live enemy's slot as the lost check sees it.</summary>
        public string DevLostState(HealthManager hm)
        {
            foreach (var s in slots)
                if (s.Live == hm.gameObject)
                {
                    Vector2 at = s.Live.transform.position;
                    var cell = CellGrid.WorldToCell(at);
                    return $"位置 {at:F1}，所在区域 {TerraWorld.RegionAt(cell.x, cell.y)?.SceneName ?? "无"}，激活 {s.Live.activeInHierarchy}，地形建好 {SceneBuilt(s.Scene)}，出界起 {s.LostSince:F1}（现在 {Time.time:F1}），{s.Why ?? "活着"}";
                }
            return "不是哪个刷新点的";
        }

        /// <summary>Dev: where the ghosts show now.</summary>
        public List<Vector2> DevGhostsAt()
        {
            var list = new List<Vector2>();
            foreach (var s in slots) if (s.Ghost != null) list.Add(SpawnPoint(s) + s.Shift + s.Offset);
            return list;
        }

        /// <summary>Dev: the slot whose spot is nearest a point: its state, its spot and its ghost.</summary>
        public string DevSlotAt(Vector2 p)
        {
            Slot best = null;
            float bestD = float.MaxValue;
            foreach (var s in slots)
            {
                float d = Vector2.Distance(SpawnPoint(s) + s.Offset, p);
                if (d < bestD) { best = s; bestD = d; }
            }
            if (best == null) return "无";
            bool live = best.Live != null && best.Live.activeInHierarchy && !(best.Live.GetComponent<HealthManager>() is HealthManager h && h.isDead);
            return $"{BaseName(best.Template != null ? best.Template.name : "?")} 出生点 {SpawnPoint(best) + best.Offset:F1}（离那里 {bestD:F1}），{(live ? "活着" : best.Why ?? "刚死")}，空了 {(best.FreeSince < 0f ? -1f : Time.time - best.FreeSince):F1} 秒，虚影 {best.Ghost != null}";
        }

        /// <summary>Dev: where the slot of a live enemy puts it when it comes back (its body's middle).</summary>
        public Vector2? DevHome(HealthManager hm)
        {
            foreach (var s in slots)
                if (s.Live == hm.gameObject) return SpawnPoint(s) + s.Offset;
            return null;
        }

        public string DevDelays(string scene)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in slots)
                if (s.Scene == scene && s.Template != null) sb.Append($" {BaseName(s.Template.name)}={s.Delay:F0}s");
            return sb.Length == 0 ? " 无" : sb.ToString();
        }

        /// <summary>Dev: the live enemies the slots of a scene put down (originals and copies).</summary>
        public List<HealthManager> DevLive(string scene)
        {
            var list = new List<HealthManager>();
            foreach (var s in slots)
                if (s.Scene == scene && s.Live != null && s.Live.activeInHierarchy && s.Live.GetComponent<HealthManager>() is HealthManager h && !h.isDead) list.Add(h);
            return list;
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
            HideGhost(s);
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
            foreach (var s in slots) HideGhost(s);
            slots.Clear();
            holders.Clear();
            ours.Clear();
        }
    }
}
