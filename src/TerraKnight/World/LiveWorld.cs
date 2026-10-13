using System;
using System.Collections;
using System.Collections.Generic;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// One continuous world. Every scene joined to the one being played by a side, top or bottom
    /// gate is loaded in the background and kept running (enemies, props, everything), moved so
    /// its entry gate sits on our gate before any of its scripts start. Past the gate line the
    /// neighbour owns the space. Walking, falling or digging across the line simply makes the
    /// neighbour the scene being played: nothing loads, the player keeps moving, the camera never
    /// leaves them. The scene that was left stays loaded as a neighbour; scenes no longer next to
    /// the current one are unloaded, and new neighbours load in the background.
    ///
    /// A scene's own entry setup (music, colours, darkness, area title) waits until the player
    /// actually enters it. Ordinary transitions (doors, death, benches, gates that can't be
    /// joined) still go the game's way, which drops all neighbours and starts over at the new
    /// scene's own coordinates.
    /// </summary>
    [HarmonyPatch]
    internal sealed class LiveWorld : MonoBehaviour
    {
        public static bool Enabled = true;
        public static LiveWorld Instance { get; private set; }

        /// <summary>Dev: every scene's scenery over a world rectangle: shown or not, and which hiding has it.</summary>
        internal string DevArtAt(Rect world)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ls in scenes.Values)
            {
                if (ls.ArtCandidates == null) continue;
                foreach (var r in ls.ArtCandidates)
                {
                    if (r == null) continue;
                    var b = r.bounds;
                    if (b.max.x < world.xMin || b.min.x > world.xMax || b.max.y < world.yMin || b.min.y > world.yMax) continue;
                    string key = "";
                    foreach (var kv in ls.HiddenArt) if (kv.Value.Contains(r)) key = kv.Key;
                    string sprite = r is SpriteRenderer sr && sr.sprite != null ? sr.sprite.name : r.GetType().Name;
                    sb.Append($"\n  {ls.Name}/{r.name} [{sprite}] 显示 {r.enabled && r.gameObject.activeInHierarchy} z {b.center.z:F2} 范围 {b.min.x:F1},{b.min.y:F1}..{b.max.x:F1},{b.max.y:F1}{(key != "" ? " 藏于 " + key : "")}");
                }
            }
            return sb.ToString();
        }

        const float Far = 100000f;

        sealed class LiveScene
        {
            public string Name;
            public Scene Scene;
            public Vector2Int Offset;
            public TerrainSystem Terrain;
            public SceneManager Sm;
            public bool Entered;
            public readonly List<AudioSource> Muted = new List<AudioSource>();
            public readonly List<AreaTitleController> PendingTitles = new List<AreaTitleController>();
            public readonly Dictionary<string, List<Renderer>> HiddenArt = new Dictionary<string, List<Renderer>>();
            /// <summary>The scene's own scenery renderers (not enemies, not ours), gathered once.</summary>
            public List<Renderer> ArtCandidates;
            public Vector2 World => (Vector2)Offset * CellGrid.CellSize;
        }

        /// <summary>
        /// A gate of the current scene and the neighbour behind it. The seam is the gate line, in
        /// each grid's own cells along the gate's axis; cut and art areas are in each scene's own
        /// coordinates.
        /// </summary>
        sealed class Link
        {
            public string Key;
            public TransitionPoint Gate;
            public string Target, Entry;
            public GatePosition Side;
            public LiveScene Neighbour;
            public TransitionPoint EntryGate;
            public bool Placed;
            public RectInt OurCut, TheirCut;
            public Rect OurArt, TheirArt;
            /// <summary>Their entry gate's cell, in their own cells.</summary>
            public Vector2Int EntryCell;
            /// <summary>Parts of the neighbour that another neighbour already holds (the game's map isn't exact), in its own cells.</summary>
            public readonly List<RectInt> ExtraCuts = new List<RectInt>();
            /// <summary>
            /// Spans along the gate line (in this scene's cells) where another gate of this scene on
            /// the same side lies farther out, with that gate's line: see <see cref="SplitForDoorways"/>.
            /// </summary>
            public readonly List<(int from, int to, int line)> Doorways = new List<(int, int, int)>();
            public string DoorwaysLogged;
            public bool Vertical => Side == GatePosition.top || Side == GatePosition.bottom;
            public Vector2Int Direction => Side == GatePosition.right ? Vector2Int.right : Side == GatePosition.left ? Vector2Int.left
                : Side == GatePosition.top ? Vector2Int.up : Vector2Int.down;
        }

        readonly Dictionary<string, LiveScene> scenes = new Dictionary<string, LiveScene>();
        readonly List<Link> links = new List<Link>();
        readonly List<string> toLoad = new List<string>();
        readonly HashSet<string> refused = new HashSet<string>();
        /// <summary>"current>target": a neighbour that couldn't be joined from that scene, not to be tried again from it.</summary>
        readonly HashSet<string> skipHere = new HashSet<string>();
        /// <summary>
        /// Loaded scenes that no gate of the current scene joins where they are (the game's map
        /// doesn't add up around loops) but that are on screen: they stay as they were, rather
        /// than vanishing and coming back somewhere else in view, until they are out of sight.
        /// </summary>
        readonly HashSet<string> parked = new HashSet<string>();
        float nextParkCheck;
        readonly HashSet<TransitionPoint> joinedGates = new HashSet<TransitionPoint>();
        LiveScene current;
        string loading;
        bool loadingBusy;
        float nextLoadTime;
        GameManager.SceneLoadInfo deferred;

        // The last crossing, so the hero standing still can't be bounced straight back over it.
        LiveScene lastLeft;
        Vector2Int lastCrossCell;
        bool bounceLogged;

        public string CurrentName => current?.Name;
        /// <summary>A gate line is being crossed: the game's scene-entry listeners are running for a scene that was already there.</summary>
        public static bool Promoting { get; private set; }
        public int LoadedCount => scenes.Count;
        /// <summary>Whether this scene is the neighbour being loaded in the background right now (joined by Admit, not the game).</summary>
        public bool LoadingNeighbour(string name) => loadingBusy && name == loading;
        public bool IsLoading => loadingBusy || toLoad.Count > 0;
        public float LastPromoteMs { get; private set; }
        /// <summary>Dev: how many gate lines have been crossed so far.</summary>
        public int CrossCount { get; private set; }

        static readonly AccessTools.FieldRef<GameManager, bool> waitForManualLevelStart = AccessTools.FieldRefAccess<GameManager, bool>("waitForManualLevelStart");
        static readonly Action<HeroController, bool> sendHeroInPosition =
            AccessTools.MethodDelegate<Action<HeroController, bool>>(AccessTools.Method(typeof(HeroController), "SendHeroInPosition"));
        static readonly System.Reflection.MethodInfo sceneManagerStart = AccessTools.Method(typeof(SceneManager), "Start");
        static readonly System.Reflection.MethodInfo setSceneManager = AccessTools.PropertySetter(typeof(GameManager), nameof(GameManager.sm));
        /// <summary>SceneManager.AddSceneMapped: the scene into the visited list, for the map.</summary>
        static readonly System.Reflection.MethodInfo areaTitleStart = AccessTools.Method(typeof(AreaTitleController), "Start");
        static readonly AccessTools.FieldRef<GameMap, float> mapOriginX = AccessTools.FieldRefAccess<GameMap, float>("originOffsetX");
        static readonly AccessTools.FieldRef<GameMap, float> mapOriginY = AccessTools.FieldRefAccess<GameMap, float>("originOffsetY");

        public static LiveWorld Create(Transform host)
        {
            Instance = host.gameObject.AddComponent<LiveWorld>();
            return Instance;
        }

        void Awake() => USceneManager.sceneLoaded += OnSceneLoaded;
        void OnDestroy() => USceneManager.sceneLoaded -= OnSceneLoaded;

        /// <summary>Forget everything: a different save was loaded, or the game left gameplay.</summary>
        public void ClearAll()
        {
            foreach (var ls in scenes.Values)
                if (ls != current && ls.Scene.IsValid() && ls.Scene.isLoaded) USceneManager.UnloadSceneAsync(ls.Scene);
            scenes.Clear();
            links.Clear();
            toLoad.Clear();
            refused.Clear();
            skipHere.Clear();
            parked.Clear();
            joinedGates.Clear();
            current = null;
            lastLeft = null;
            gradeTarget = null;
        }

        /// <summary>The hero has finished entering a scene, the game's way or by crossing a gate line.</summary>
        public void OnSceneReady()
        {
            if (!Enabled) return;
            var active = USceneManager.GetActiveScene();
            var gm = GameManager.instance;
            if (current == null || current.Scene != active)
            {
                // Entered the game's way: this scene is the origin of a new world.
                var terrain = TerrainSystem.Current;
                if (terrain == null || terrain.Map.gameObject.scene != active || string.IsNullOrEmpty(terrain.SceneName)) return;
                scenes.Clear();
                skipHere.Clear();
                parked.Clear();
                current = new LiveScene { Name = terrain.SceneName, Scene = active, Offset = TerrainSystem.CellOffsetOf(terrain.Map), Terrain = terrain, Sm = gm.sm, Entered = true };
                scenes[current.Name] = current;
                lastLeft = null;
            }
            RebuildLinks();
        }

        void Update()
        {
            var gm = GameManager.UnsafeInstance;
            if (!Enabled || gm == null) return;
            if (gm.gameState == GameState.MAIN_MENU || gm.sceneName == "Menu_Title" || gm.sceneName == "Quit_To_Menu")
            {
                if (scenes.Count > 0 || refused.Count > 0) ClearAll();
                return;
            }
            if (loadingBusy) worstLoadFrame = Mathf.Max(worstLoadFrame, Time.unscaledDeltaTime);
            DeferredFsms.Pump();
            if (current == null || current.Terrain == null || waitingToTransition) return;
            PumpLoads(gm);
            CheckCrossing(gm);
            if (parked.Count > 0 && Time.unscaledTime >= nextParkCheck)
            {
                nextParkCheck = Time.unscaledTime + 0.25f;
                foreach (var name in parked)
                    if (!scenes.TryGetValue(name, out var ls) || !OnScreen(ls))
                    {
                        // Out of sight now: it can go, and come back where the current scene's gate puts it.
                        Plugin.Log.LogInfo($"[无缝] 搁置的 {name} 离开画面了，重新安排相邻场景");
                        RebuildLinks();
                        break;
                    }
            }
        }

        /// <summary>Whether any of a scene's grid is within (or near) the view.</summary>
        static bool OnScreen(LiveScene ls)
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            var region = ls.Terrain?.Region;
            if (cam == null || region == null) return false;
            const float c = CellGrid.CellSize, Margin = 6f;
            float dist = Mathf.Abs(cam.transform.position.z);
            float halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            var p = cam.transform.position;
            var view = new Rect(p.x - halfW - Margin, p.y - halfH - Margin, (halfW + Margin) * 2f, (halfH + Margin) * 2f);
            var r = region.CellRect;
            return view.Overlaps(new Rect(r.xMin * c, r.yMin * c, r.width * c, r.height * c));
        }

        string SkipKey(string target) => (current != null ? current.Name : "") + ">" + target;

        /// <summary>Whether two loaded scenes both claim some of the same cells.</summary>
        static bool Collide(LiveScene a, LiveScene b)
        {
            var ra = a.Terrain?.Region;
            var rb = b.Terrain?.Region;
            if (ra == null || rb == null) return false;
            var x = ra.CellRect;
            var y = rb.CellRect;
            int x0 = Mathf.Max(x.xMin, y.xMin), x1 = Mathf.Min(x.xMax, y.xMax), y0 = Mathf.Max(x.yMin, y.yMin), y1 = Mathf.Min(x.yMax, y.yMax);
            for (int gy = y0; gy < y1; gy += 2)
                for (int gx = x0; gx < x1; gx += 2)
                    if (ra.Contains(gx, gy) && rb.Contains(gx, gy)) return true;
            return false;
        }

        // --- Colour grade across a gate line. ---

        /// <summary>Least distance (units) either side of a joined gate line over which the two scenes' colours blend.</summary>
        const float GradeBlendDistance = 6f;
        SceneManager gradeTarget;

        /// <summary>
        /// The screen has a single colour grade (saturation, colour curves, ambient and hero light),
        /// the current scene's. Near a joined gate line it blends towards the neighbour's, half and
        /// half on the line itself, so the two sides meet halfway and crossing doesn't snap.
        /// </summary>
        void UpdateGradeBlend()
        {
            var gc = GameCameras.instance;
            var hero = HeroController.SilentInstance;
            if (gc == null || gc.sceneColorManager == null || hero == null || current == null) return;
            var scm = gc.sceneColorManager;
            Vector2 p = hero.transform.position;
            // The blend starts as the neighbour comes into view (half the view's width or height
            // from the line) and reaches half and half on the line, so the colours change little by
            // little as more of the neighbour fills the screen, not all at once near the line.
            var cam = gc.mainCamera;
            float halfH = cam != null ? Mathf.Abs(cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) : GradeBlendDistance;
            float halfW = cam != null ? halfH * cam.aspect : GradeBlendDistance;
            Link best = null;
            float bestK = 0f, bestD = float.MaxValue;
            foreach (var link in links)
            {
                if (!link.Placed || link.Neighbour?.Sm == null) continue;
                float d = DistanceToSeam(link, p);
                float reach = Mathf.Max(GradeBlendDistance, link.Vertical ? halfH : halfW);
                float k = Mathf.Clamp01(0.5f * (1f - d / reach));
                if (k > bestK) { best = link; bestK = k; bestD = d; }
            }
            if (best == null)
            {
                if (gradeTarget == null) return;
                gradeTarget = null;
                scm.SetFactor(0f);
                scm.UpdateScript(forceUpdate: true);
                return;
            }
            float factor = bestK;
            var sm = best.Neighbour.Sm;
            scm.SetFactor(factor);
            if (gradeTarget == sm)
            {
                scm.UpdateScript();
                return;
            }
            gradeTarget = sm;
            scm.SaturationB = sm.AdjustSaturation(sm.saturation);
            scm.RedB = sm.redChannel;
            scm.GreenB = sm.greenChannel;
            scm.BlueB = sm.blueChannel;
            scm.AmbientColorB = sm.defaultColor;
            scm.AmbientIntensityB = sm.defaultIntensity;
            scm.HeroLightColorB = sm.heroLightColor;
            // New B curves need pairing with A's keyframes again.
            scm.UpdateScript(forceUpdate: true);
        }

        /// <summary>
        /// How far a point is from a link's gate line, measured into the current scene (negative
        /// past it); infinite when the point is beside the line rather than in front of it.
        /// </summary>
        float DistanceToSeam(Link link, Vector2 p)
        {
            const float c = CellGrid.CellSize, margin = 4f;
            var cut = link.OurCut;
            var o = current.Offset;
            // Along another gate's doorway farther out this line isn't the edge: that gate's is.
            float along = (link.Vertical ? p.x : p.y) / c - (link.Vertical ? o.x : o.y);
            foreach (var d in link.Doorways)
                if (along >= d.from && along < d.to) return float.MaxValue;
            float x0 = (o.x + cut.xMin) * c, x1 = (o.x + cut.xMax) * c, y0 = (o.y + cut.yMin) * c, y1 = (o.y + cut.yMax) * c;
            switch (link.Side)
            {
                case GatePosition.right: return p.y >= y0 - margin && p.y <= y1 + margin ? x0 - p.x : float.MaxValue;
                case GatePosition.left: return p.y >= y0 - margin && p.y <= y1 + margin ? p.x - x1 : float.MaxValue;
                case GatePosition.top: return p.x >= x0 - margin && p.x <= x1 + margin ? y0 - p.y : float.MaxValue;
                default: return p.x >= x0 - margin && p.x <= x1 + margin ? p.y - y1 : float.MaxValue;
            }
        }

        float worstLoadFrame, loadStarted;
        /// <summary>Dev: the longest frame during the last background load, in milliseconds.</summary>
        public float LastLoadWorstFrameMs { get; private set; }

        // --- Which scenes are joined to the current one. ---

        void RebuildLinks()
        {
            links.Clear();
            var cur = current;
            foreach (var tp in TransitionPoint.TransitionPoints)
            {
                if (tp == null || tp.gameObject.scene != cur.Scene || !tp.isActiveAndEnabled || tp.isADoor) continue;
                if (string.IsNullOrEmpty(tp.targetScene) || string.IsNullOrEmpty(tp.entryPoint) || tp.targetScene == cur.Name) continue;
                var side = SideOf(tp.name);
                if (side == GatePosition.door || side == GatePosition.unknown) continue;
                // Only an edge gate facing back at this one continues the space: a side exit into a
                // door in the middle of a bigger scene (an interior like Sheo's hut) would lay that
                // whole scene over this one, with its hazards invisible behind this scene's art.
                if (SideOf(tp.entryPoint) != Opposite(side)) continue;
                if (KeptApart(cur.Name, tp.targetScene)) continue;
                links.Add(new Link { Key = cur.Name + ":" + tp.name, Gate = tp, Target = tp.targetScene, Entry = tp.entryPoint, Side = side });
            }

            // Scenes already loaded join where they already are, if that really is behind the gate
            // and clear of the other neighbours; anything else loaded goes, and missing ones load.
            var keep = new HashSet<string> { cur.Name };
            foreach (var link in links)
            {
                if (!scenes.TryGetValue(link.Target, out var ls) || ls == cur) continue;
                if (!Join(link, ls, null)) continue;
                if (YieldOverlaps(link, ls.Terrain.Grid.Width, ls.Terrain.Grid.Height)) keep.Add(link.Target);
                else link.Placed = false;
            }
            var targets = new HashSet<string>();
            foreach (var link in links) targets.Add(link.Target);
            // A neighbour of this scene loaded where this scene's gate doesn't put it, still on
            // screen: unloading it now would make it vanish and come back somewhere else in view.
            // It stays as it was (its gate is an ordinary one meanwhile), unless it overlaps this scene.
            var wasParked = new HashSet<string>(parked);
            parked.Clear();
            foreach (var ls in scenes.Values)
                if (!keep.Contains(ls.Name) && targets.Contains(ls.Name) && ls.Terrain != null && OnScreen(ls) && !Collide(ls, cur))
                {
                    parked.Add(ls.Name);
                    keep.Add(ls.Name);
                    if (!wasParked.Contains(ls.Name)) Plugin.Log.LogInfo($"[无缝] {ls.Name} 和这里的门对不上，但在画面里，先原样留着");
                }
            var gone = new List<LiveScene>();
            foreach (var ls in scenes.Values)
                if (!keep.Contains(ls.Name)) gone.Add(ls);
            foreach (var ls in gone) Unload(ls);
            foreach (var link in links)
            {
                if (keep.Contains(link.Target) || refused.Contains(link.Target) || skipHere.Contains(SkipKey(link.Target))) continue;
                if (!toLoad.Contains(link.Target) && loading != link.Target) toLoad.Add(link.Target);
            }
            toLoad.RemoveAll(n => !targets.Contains(n) || keep.Contains(n));
            ApplyLinks();

            var sb = new System.Text.StringBuilder();
            foreach (var l in links) sb.Append($" {l.Gate.name}->{l.Target}{(l.Placed ? "✓" : refused.Contains(l.Target) ? "(不能接)" : parked.Contains(l.Target) ? "(搁置)" : "")}");
            Plugin.Log.LogInfo($"[无缝] 当前 {cur.Name}（偏移 {cur.Offset}），门:{sb}，已载入 {scenes.Count} 个场景");
        }

        static void MarkVisited(SceneManager sm, GameManager gm)
        {
            var pd = PlayerData.instance;
            string name = gm.GetSceneNameString();
            if (pd?.scenesVisited == null || pd.scenesVisited.Contains(name) || sm.manualMapTrigger) return;
            if (sm.mapZone == MapZone.WHITE_PALACE || sm.mapZone == MapZone.GODS_GLORY) return;
            pd.scenesVisited.Add(name);
        }

        static GatePosition SideOf(string gateName)
        {
            // Same order as TransitionPoint.GetGatePosition.
            if (gateName.Contains("top")) return GatePosition.top;
            if (gateName.Contains("right")) return GatePosition.right;
            if (gateName.Contains("left")) return GatePosition.left;
            if (gateName.Contains("bot")) return GatePosition.bottom;
            return GatePosition.door;
        }

        static GatePosition Opposite(GatePosition side) =>
            side == GatePosition.left ? GatePosition.right : side == GatePosition.right ? GatePosition.left
            : side == GatePosition.top ? GatePosition.bottom : side == GatePosition.bottom ? GatePosition.top : GatePosition.unknown;

        /// <summary>
        /// Rooms hidden inside another's walls, laid over it where joined: Ruins1_31b, behind the City
        /// of Tears' toll bench, would take that bench's room (its floor cut away under the player).
        /// Their gates stay ordinary ones.
        /// </summary>
        static readonly string[][] apart = { new[] { "Ruins1_31", "Ruins1_31b" } };

        static bool KeptApart(string a, string b)
        {
            foreach (var p in apart)
                if ((p[0] == a && p[1] == b) || (p[0] == b && p[1] == a)) return true;
            return false;
        }

        static TransitionPoint FindGate(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                foreach (var tp in go.GetComponentsInChildren<TransitionPoint>(true))
                    if (tp.name == name) return tp;
            return null;
        }

        /// <summary>
        /// Works out where a neighbour goes relative to the current scene, from the two gates and
        /// (for side gates) the floors under them, and the areas each side gives up. With
        /// <paramref name="grid"/> set the neighbour isn't placed yet (its grid is at its own
        /// coordinates); otherwise it is already loaded, and has to be where this says.
        /// </summary>
        bool Join(Link link, LiveScene ls, CellGrid grid)
        {
            var cur = current;
            var entry = FindGate(ls.Scene, link.Entry);
            if (entry == null)
            {
                Plugin.Log.LogWarning($"[无缝] {ls.Name} 里没有入口 {link.Entry}，{link.Gate.name} 保持原版切换");
                return false;
            }
            if (entry.isADoor || SideOf(entry.name) != Opposite(link.Side))
            {
                Plugin.Log.LogInfo($"[无缝] {ls.Name} 的入口 {entry.name} 不在对面的边上，{link.Gate.name} 保持原版切换");
                return false;
            }
            var theirGrid = grid ?? ls.Terrain?.Grid;
            if (theirGrid == null) return false;
            const float c = CellGrid.CellSize;
            Vector2 ours = GateCenter(link.Gate) - cur.World;
            Vector2 theirs = GateCenter(entry) - (grid != null ? Vector2.zero : ls.World);
            var ourGrid = cur.Terrain.Grid;
            Vector2Int rel;
            int seam, theirSeam;
            if (!link.Vertical)
            {
                seam = Mathf.Clamp(Mathf.RoundToInt(ours.x / c), 0, ourGrid.Width);
                theirSeam = Mathf.Clamp(Mathf.RoundToInt(theirs.x / c), 0, theirGrid.Width);
                bool right = link.Side == GatePosition.right;
                int ourFloor = FloorBelow(ourGrid, right ? seam - 4 : seam + 3, ours.y);
                int theirFloor = FloorBelow(theirGrid, right ? theirSeam + 3 : theirSeam - 4, theirs.y);
                int oy = ourFloor >= 0 && theirFloor >= 0 ? ourFloor - theirFloor : Mathf.RoundToInt((ours.y - theirs.y) / c);
                rel = new Vector2Int(seam - theirSeam, oy);
            }
            else
            {
                seam = Mathf.Clamp(Mathf.RoundToInt(ours.y / c), 0, ourGrid.Height);
                theirSeam = Mathf.Clamp(Mathf.RoundToInt(theirs.y / c), 0, theirGrid.Height);
                rel = new Vector2Int(Mathf.RoundToInt((ours.x - theirs.x) / c), seam - theirSeam);
            }

            if (grid == null)
            {
                // Already loaded somewhere: only join if it really sits behind this gate. Along the
                // gate line allow some slack (floors may have been dug since it was placed).
                var actual = ls.Offset - cur.Offset;
                int acrossLine = link.Vertical ? Mathf.Abs(actual.y - rel.y) : Mathf.Abs(actual.x - rel.x);
                int alongLine = link.Vertical ? Mathf.Abs(actual.x - rel.x) : Mathf.Abs(actual.y - rel.y);
                if (acrossLine > 2 || alongLine > 8)
                {
                    Plugin.Log.LogInfo($"[无缝] {ls.Name} 已在别处（相对 {actual}，这扇门要 {rel}），{link.Gate.name} 保持原版切换");
                    return false;
                }
                rel = actual;
            }

            link.Neighbour = ls;
            link.EntryGate = entry;
            link.EntryCell = new Vector2Int(Mathf.FloorToInt(theirs.x / c), Mathf.FloorToInt(theirs.y / c));
            ComputeAreas(link, ourGrid, theirGrid, rel, seam, theirSeam);
            link.Placed = true;
            if (grid != null) ls.Offset = cur.Offset + rel;
            return true;
        }

        static Vector2 GateCenter(TransitionPoint tp)
        {
            var col = tp.GetComponent<Collider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)tp.transform.position;
        }

        static void ComputeAreas(Link link, CellGrid ourGrid, CellGrid theirGrid, Vector2Int rel, int seam, int theirSeam)
        {
            const float c = CellGrid.CellSize;
            int ourW = ourGrid.Width, ourH = ourGrid.Height, theirW = theirGrid.Width, theirH = theirGrid.Height;
            switch (link.Side)
            {
                case GatePosition.right:
                case GatePosition.left:
                {
                    // Rows both cover, in each grid's own cells.
                    int r0 = Mathf.Max(0, rel.y), r1 = Mathf.Min(ourH, rel.y + theirH);
                    int t0 = Mathf.Max(0, -rel.y), t1 = Mathf.Min(theirH, ourH - rel.y);
                    if (link.Side == GatePosition.right)
                    {
                        link.OurCut = new RectInt(seam, r0, ourW - seam, r1 - r0);
                        link.TheirCut = new RectInt(0, t0, theirSeam, t1 - t0);
                        link.OurArt = Rect.MinMaxRect(seam * c, r0 * c, Far, r1 * c);
                        link.TheirArt = Rect.MinMaxRect(-Far, t0 * c, theirSeam * c, t1 * c);
                    }
                    else
                    {
                        link.OurCut = new RectInt(0, r0, seam, r1 - r0);
                        link.TheirCut = new RectInt(theirSeam, t0, theirW - theirSeam, t1 - t0);
                        link.OurArt = Rect.MinMaxRect(-Far, r0 * c, seam * c, r1 * c);
                        link.TheirArt = Rect.MinMaxRect(theirSeam * c, t0 * c, Far, t1 * c);
                    }
                    break;
                }
                default:
                {
                    // Columns both cover.
                    int c0 = Mathf.Max(0, rel.x), c1 = Mathf.Min(ourW, rel.x + theirW);
                    int t0 = Mathf.Max(0, -rel.x), t1 = Mathf.Min(theirW, ourW - rel.x);
                    if (link.Side == GatePosition.top)
                    {
                        link.OurCut = new RectInt(c0, seam, c1 - c0, ourH - seam);
                        link.TheirCut = new RectInt(t0, 0, t1 - t0, theirSeam);
                        link.OurArt = Rect.MinMaxRect(c0 * c, seam * c, c1 * c, Far);
                        link.TheirArt = Rect.MinMaxRect(t0 * c, -Far, t1 * c, theirSeam * c);
                    }
                    else
                    {
                        link.OurCut = new RectInt(c0, 0, c1 - c0, seam);
                        link.TheirCut = new RectInt(t0, theirSeam, t1 - t0, theirH - theirSeam);
                        link.OurArt = Rect.MinMaxRect(c0 * c, -Far, c1 * c, seam * c);
                        link.TheirArt = Rect.MinMaxRect(t0 * c, theirSeam * c, t1 * c, Far);
                    }
                    break;
                }
            }
        }

        /// <summary>The first open cell above solid ground under a height, in one column; -1 if none.</summary>
        internal static int FloorBelow(CellGrid grid, int x, float localY)
        {
            int y = Mathf.Clamp(Mathf.FloorToInt(localY / CellGrid.CellSize), 0, grid.Height - 1);
            for (int k = 0; k < 12 && y < grid.Height - 1 && grid.IsSolid(x, y); k++) y++;
            for (int k = 0; k < 60 && y > 0; k++, y--)
                if (grid.IsSolid(x, y - 1)) return y;
            return -1;
        }

        /// <summary>The space a neighbour owns, in global cells: its own side of the gate line.</summary>
        static RectInt Owned(GatePosition side, Vector2Int offset, RectInt theirCut, int width, int height)
        {
            var c = theirCut;
            switch (side)
            {
                case GatePosition.right: return new RectInt(offset.x + c.xMax, offset.y, width - c.xMax, height);
                case GatePosition.left: return new RectInt(offset.x, offset.y, c.xMin, height);
                case GatePosition.top: return new RectInt(offset.x, offset.y + c.yMax, width, height - c.yMax);
                default: return new RectInt(offset.x, offset.y, width, c.yMin);
            }
        }

        static RectInt Owned(Link link)
        {
            var g = link.Neighbour.Terrain.Grid;
            return Owned(link.Side, link.Neighbour.Offset, link.TheirCut, g.Width, g.Height);
        }

        /// <summary>
        /// The game's map isn't geometrically exact, so a neighbour can land on top of another
        /// neighbour. The one already there keeps the overlap; the newcomer gives it up, unless the
        /// overlap reaches its entry gate (then it can't be walked into, and stays an ordinary transition).
        /// </summary>
        bool YieldOverlaps(Link link, int width, int height)
        {
            link.ExtraCuts.Clear();
            var ls = link.Neighbour;
            var owned = Owned(link.Side, ls.Offset, link.TheirCut, width, height);
            var nearGate = new RectInt(link.EntryCell.x - 6, link.EntryCell.y - 6, 13, 13);
            foreach (var other in links)
            {
                if (other == link || !other.Placed || other.Neighbour == null || other.Neighbour == ls || other.Neighbour.Terrain == null) continue;
                var o = Owned(other);
                int x0 = Mathf.Max(owned.xMin, o.xMin), x1 = Mathf.Min(owned.xMax, o.xMax);
                int y0 = Mathf.Max(owned.yMin, o.yMin), y1 = Mathf.Min(owned.yMax, o.yMax);
                if (x1 <= x0 || y1 <= y0) continue;
                var local = new RectInt(x0 - ls.Offset.x, y0 - ls.Offset.y, x1 - x0, y1 - y0);
                if (local.Overlaps(nearGate))
                {
                    Plugin.Log.LogInfo($"[无缝] {ls.Name} 的入口和 {other.Neighbour.Name} 重叠，{link.Gate.name} 保持原版切换");
                    return false;
                }
                link.ExtraCuts.Add(local);
                Plugin.Log.LogInfo($"[无缝] {ls.Name} 和 {other.Neighbour.Name} 有 {local.width}x{local.height} 格重叠，让给 {other.Neighbour.Name}");
            }
            return true;
        }

        /// <summary>
        /// A scene's edge isn't straight: two gates on one side can have their lines apart
        /// (Cliffs_02's left1 to Cliffs_01 at its very edge, left2 to Cliffs_03 three units in). The
        /// neighbour behind the nearer line reaches out to it along the whole of its height, and
        /// so walls up the other doorway. Along that doorway (the gate's span, two cells either
        /// side) this scene keeps its cells out to the farther line and the neighbour gives them
        /// up, and four cells past it, for the gate's trigger to be walked into when that gate is an
        /// ordinary one. The link's cut and hidden art are split up accordingly. A second gate to
        /// the same neighbour, or one beside the link's own (Crossroads_01's two triggers up the
        /// well to Dirtmouth), is no other doorway.
        /// </summary>
        void SplitForDoorways(Link link, List<RectInt> ourCuts, List<(string key, Rect area, Transform gate, ArtRule rule)> ourArt,
                              List<RectInt> theirCuts, List<(string key, Rect area, Transform gate, ArtRule rule)> theirArt)
        {
            const float c = CellGrid.CellSize;
            const int pad = 2, mouth = 4;
            link.Doorways.Clear();
            if (current.Terrain == null || link.Neighbour.Terrain == null)
            {
                ourCuts.Add(link.OurCut);
                ourArt.Add((link.Key, link.OurArt, link.Gate.transform, ArtRule.PastLine));
                return;
            }
            var ourGrid = current.Terrain.Grid;
            var theirGrid = link.Neighbour.Terrain.Grid;
            var cut = link.OurCut;
            bool vertical = link.Vertical;
            // Right and top: the lines grow outward with the coordinate.
            bool up = link.Side == GatePosition.right || link.Side == GatePosition.top;
            int size = vertical ? ourGrid.Height : ourGrid.Width;
            int seam = up ? (vertical ? cut.yMin : cut.xMin) : (vertical ? cut.yMax : cut.xMax);
            int a0 = vertical ? cut.xMin : cut.yMin, a1 = vertical ? cut.xMax : cut.yMax;
            var bands = new List<(int from, int to, int line)>();
            var own = link.Gate.GetComponent<Collider2D>();
            float own0 = own == null ? 0f : vertical ? own.bounds.min.x : own.bounds.min.y;
            float own1 = own == null ? 0f : vertical ? own.bounds.max.x : own.bounds.max.y;
            foreach (var other in links)
            {
                if (other == link || other.Side != link.Side || other.Gate == null || other.Target == link.Target) continue;
                var col = other.Gate.GetComponent<Collider2D>();
                if (col == null) continue;
                float o0 = vertical ? col.bounds.min.x : col.bounds.min.y, o1 = vertical ? col.bounds.max.x : col.bounds.max.y;
                if (own != null && o0 < own1 && o1 > own0) continue;
                Vector2 min = (Vector2)col.bounds.min - current.World, max = (Vector2)col.bounds.max - current.World;
                int line = Mathf.Clamp(Mathf.RoundToInt((vertical ? min.y + max.y : min.x + max.x) / 2f / c), 0, size);
                if (up ? line <= seam : line >= seam) continue;
                int from = Mathf.Max(a0, Mathf.FloorToInt((vertical ? min.x : min.y) / c) - pad);
                int to = Mathf.Min(a1, Mathf.CeilToInt((vertical ? max.x : max.y) / c) + pad);
                if (to > from) bands.Add((from, to, line));
            }
            if (bands.Count == 0)
            {
                ourCuts.Add(cut);
                ourArt.Add((link.Key, link.OurArt, link.Gate.transform, ArtRule.PastLine));
                return;
            }
            bands.Sort((p, q) => p.from.CompareTo(q.from));
            var pieces = new List<(int from, int to, int line)>();
            int at = a0;
            foreach (var b in bands)
            {
                int f = Mathf.Max(b.from, at);
                if (b.to <= f) continue;
                if (f > at) pieces.Add((at, f, seam));
                pieces.Add((f, b.to, b.line));
                link.Doorways.Add((f, b.to, b.line));
                at = b.to;
            }
            if (a1 > at) pieces.Add((at, a1, seam));
            // This scene's cells to the neighbour's.
            var off = current.Offset - link.Neighbour.Offset;
            int alongOff = vertical ? off.x : off.y, acrossOff = vertical ? off.y : off.x;
            int theirSize = vertical ? theirGrid.Height : theirGrid.Width;
            for (int i = 0; i < pieces.Count; i++)
            {
                var (from, to, line) = pieces[i];
                string key = i == 0 ? link.Key : link.Key + "#d" + i;
                RectInt ours;
                Rect oursArt;
                switch (link.Side)
                {
                    case GatePosition.right: ours = new RectInt(line, from, size - line, to - from); oursArt = Rect.MinMaxRect(line * c, from * c, Far, to * c); break;
                    case GatePosition.left: ours = new RectInt(0, from, line, to - from); oursArt = Rect.MinMaxRect(-Far, from * c, line * c, to * c); break;
                    case GatePosition.top: ours = new RectInt(from, line, to - from, size - line); oursArt = Rect.MinMaxRect(from * c, line * c, to * c, Far); break;
                    default: ours = new RectInt(from, 0, to - from, line); oursArt = Rect.MinMaxRect(from * c, -Far, to * c, line * c); break;
                }
                if (ours.width > 0 && ours.height > 0) ourCuts.Add(ours);
                ourArt.Add((key, oursArt, link.Gate.transform, ArtRule.PastLine));
                if (line == seam) continue;
                // The neighbour gives up the doorway, from a little past the farther line inward.
                int across0 = up ? 0 : line - mouth + acrossOff;
                int across1 = up ? line + mouth + acrossOff : theirSize;
                int along0 = from + alongOff, along1 = to + alongOff;
                var theirs = vertical ? new RectInt(along0, across0, along1 - along0, across1 - across0)
                                      : new RectInt(across0, along0, across1 - across0, along1 - along0);
                theirs = Clip(theirs, theirGrid.Width, theirGrid.Height);
                if (theirs.width <= 0 || theirs.height <= 0) continue;
                theirCuts.Add(theirs);
                theirArt.Add((link.Key + "#door" + i, new Rect(theirs.x * c, theirs.y * c, theirs.width * c, theirs.height * c), null, ArtRule.Overlap));
            }
            var sb = new System.Text.StringBuilder();
            foreach (var d in link.Doorways) sb.Append($" {d.from}..{d.to} 到线 {d.line}");
            string said = sb.ToString();
            if (said == link.DoorwaysLogged) return;
            link.DoorwaysLogged = said;
            Plugin.Log.LogInfo($"[无缝] {link.Gate.name} 的线（{seam}）旁边另有更靠外的门：{said}，这几段留给本场景");
        }

        static RectInt Clip(RectInt r, int width, int height)
        {
            int x0 = Mathf.Max(0, r.xMin), y0 = Mathf.Max(0, r.yMin), x1 = Mathf.Min(width, r.xMax), y1 = Mathf.Min(height, r.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>What a scene's scenery is hidden for.</summary>
        enum ArtRule
        {
            /// <summary>Past a joined gate line (the neighbour shows there), plus the gate's dressing.</summary>
            PastLine,
            /// <summary>An overlap given up to another neighbour: what lies mostly inside it.</summary>
            Overlap,
            /// <summary>Background lining the shaft above a joined bottom gate, which would cover the shaft below.</summary>
            ShaftLining,
        }

        /// <summary>Applies the cuts, hidden scenery and silenced gates of every placed link.</summary>
        void ApplyLinks()
        {
            joinedGates.Clear();
            var cuts = new Dictionary<LiveScene, List<RectInt>>();
            var art = new Dictionary<LiveScene, List<(string key, Rect area, Transform gate, ArtRule rule)>>();
            foreach (var ls in scenes.Values)
            {
                cuts[ls] = new List<RectInt>();
                art[ls] = new List<(string, Rect, Transform, ArtRule)>();
            }
            const float c = CellGrid.CellSize;
            foreach (var link in links)
            {
                if (!link.Placed || link.Neighbour == null || !cuts.ContainsKey(link.Neighbour)) continue;
                SplitForDoorways(link, cuts[current], art[current], cuts[link.Neighbour], art[link.Neighbour]);
                cuts[link.Neighbour].Add(link.TheirCut);
                cuts[link.Neighbour].AddRange(link.ExtraCuts);
                art[link.Neighbour].Add((link.Key, link.TheirArt, link.EntryGate != null ? link.EntryGate.transform : null, ArtRule.PastLine));
                for (int i = 0; i < link.ExtraCuts.Count; i++)
                {
                    var e = link.ExtraCuts[i];
                    art[link.Neighbour].Add((link.Key + "#" + i, new Rect(e.x * c, e.y * c, e.width * c, e.height * c), null, ArtRule.Overlap));
                }
                // A shaft between scenes stacked one above the other: the upper scene paints its end
                // of the shaft dark (it was never meant to be seen into), hiding the lower scene's lit shaft.
                if (link.Side == GatePosition.bottom)
                    art[current].Add((link.Key + "#shaft", ShaftArea(link.Gate, current, link.OurCut.yMax * c), null, ArtRule.ShaftLining));
                else if (link.Side == GatePosition.top && link.EntryGate != null)
                    art[link.Neighbour].Add((link.Key + "#shaft", ShaftArea(link.EntryGate, link.Neighbour, link.TheirCut.yMax * c), null, ArtRule.ShaftLining));
                joinedGates.Add(link.Gate);
                if (link.EntryGate != null) joinedGates.Add(link.EntryGate);
            }
            foreach (var ls in scenes.Values)
            {
                // A parked scene keeps the cuts and hidden art it had.
                if (parked.Contains(ls.Name)) continue;
                ls.Terrain?.Region.SetCuts(cuts[ls]);
                ApplyArt(ls, art[ls]);
            }
            ApplyShafts();
        }

        /// <summary>
        /// Scenes stacked one above the other meet in a shaft. The upper scene covers its end of
        /// the shaft with dark ground in front (it was never meant to be seen into); cut that open
        /// from the gate line up to where the shaft's walls end, so the lower scene's lit shaft
        /// shows through, and keep the lower scene's own shaft art out of any cutting.
        /// </summary>
        void ApplyShafts()
        {
            const float c = CellGrid.CellSize;
            var openings = new Dictionary<LiveScene, HashSet<string>>();
            var exempts = new Dictionary<LiveScene, HashSet<string>>();
            foreach (var ls in scenes.Values)
            {
                openings[ls] = new HashSet<string>();
                exempts[ls] = new HashSet<string>();
            }
            foreach (var link in links)
            {
                if (!link.Placed || !link.Vertical || link.Neighbour?.Terrain == null || current.Terrain == null || !openings.ContainsKey(link.Neighbour)) continue;
                bool weAreUpper = link.Side == GatePosition.bottom;
                var upper = weAreUpper ? current : link.Neighbour;
                var lower = weAreUpper ? link.Neighbour : current;
                var gate = weAreUpper ? link.Gate : link.EntryGate;
                if (gate == null) continue;
                int line = weAreUpper ? link.OurCut.yMax : link.TheirCut.yMax;
                var cells = ShaftCells(upper.Terrain.Grid, Mathf.FloorToInt((GateCenter(gate).x - upper.World.x) / c), line, out int x0, out int x1);
                if (cells.Count == 0) continue;
                // The lower scene's shaft has walls of its own drawn beside the open run (Dirtmouth's well:
                // the Crossroads' bricks): opened too, or the window shows the shaft's inside only, a light
                // box with straight sides, and the walls start at the upper scene's bottom line.
                const int wall = ShaftWallCells;
                var rows = new HashSet<int>();
                int top = line;
                foreach (var cell in cells)
                {
                    rows.Add(cell.y);
                    top = Mathf.Max(top, cell.y + 1);
                }
                foreach (int y in rows)
                    for (int k = 1; k <= wall; k++)
                    {
                        if (x0 - k >= 0) cells.Add(new Vector2Int(x0 - k, y));
                        if (x1 + k < upper.Terrain.Grid.Width) cells.Add(new Vector2Int(x1 + k, y));
                    }
                x0 = Mathf.Max(0, x0 - wall);
                x1 = Mathf.Min(upper.Terrain.Grid.Width - 1, x1 + wall);
                upper.Terrain.Holes.SetOpening(link.Key, cells);
                openings[upper].Add(link.Key);
                // The lower scene's art in the shaft above the line, in its own scene's coordinates.
                float wx0 = x0 * c + upper.World.x - 2f, wx1 = (x1 + 1) * c + upper.World.x + 2f, wy = line * c + upper.World.y;
                float wtop = top * c + upper.World.y + 1f;
                var keep = new List<SpriteRenderer>();
                if (lower.ArtCandidates != null)
                    foreach (var r in lower.ArtCandidates)
                    {
                        if (!(r is SpriteRenderer sr) || sr == null) continue;
                        var b = sr.bounds;
                        if (b.min.x >= wx0 && b.max.x <= wx1 && b.max.y > wy) keep.Add(sr);
                    }
                lower.Terrain.Holes.SetExempt(link.Key, keep);
                exempts[lower].Add(link.Key);
                // Its walls wholly above the line were hidden with the rest past it (the neighbour's
                // side): shown again through the window, those mostly no higher than the shaft goes
                // (Crossroads_01's left wall reaches a little over Dirtmouth's ground).
                int shown = 0;
                foreach (var kv in lower.HiddenArt)
                {
                    if (kv.Key != link.Key && !kv.Key.StartsWith(link.Key + "#d")) continue;
                    for (int i = kv.Value.Count - 1; i >= 0; i--)
                    {
                        var r = kv.Value[i];
                        if (!(r is SpriteRenderer sr) || sr == null || HandPlacedArt.Destroyed.Contains(r) || !keep.Contains(sr)) continue;
                        var bb = sr.bounds;
                        if (Mathf.Min(bb.max.y, wtop) - Mathf.Max(bb.min.y, wy) < 0.75f * bb.size.y) continue;
                        r.enabled = true;
                        kv.Value.RemoveAt(i);
                        shown++;
                    }
                }
                if (shown > 0) Plugin.Log.LogInfo($"[无缝] {lower.Name} 井道里 {shown} 块井壁贴图从 {upper.Name} 那边看得见（窗口 x {wx0:F1}..{wx1:F1}，y {wy:F1}..{wtop:F1}）");
                // Its light falling down the shaft (Crossroads_01's beam) reaches far up past the upper
                // scene's ground, never meant to be seen from there: a lit column with a hard top
                // standing in Dirtmouth's sky. Hidden with the rest past the line.
                if (lower.HiddenArt.TryGetValue(link.Key, out var past))
                    foreach (var sr in keep)
                    {
                        if (!sr.enabled || past.Contains(sr) || sr.bounds.min.y >= wy || sr.bounds.max.y < wtop + 2f) continue;
                        string n = sr.name.ToLowerInvariant();
                        if (!n.Contains("glow") && !n.Contains("beam") && !n.Contains("light")) continue;
                        sr.enabled = false;
                        past.Add(sr);
                        Plugin.Log.LogInfo($"[无缝] {lower.Name} 的 {sr.name} 从井道一直伸到 {upper.Name} 地面上方 {sr.bounds.max.y - wtop:F1}，藏起来");
                    }
            }
            foreach (var ls in scenes.Values)
            {
                var holes = ls.Terrain?.Holes;
                if (holes == null || parked.Contains(ls.Name)) continue;
                foreach (var key in new List<string>(holes.OpeningKeys)) if (!openings[ls].Contains(key)) holes.SetOpening(key, null);
                foreach (var key in new List<string>(holes.ExemptKeys)) if (!exempts[ls].Contains(key)) holes.SetExempt(key, null);
            }
        }

        /// <summary>
        /// The open cells of a shaft rising from a gate line: the open run of cells across the gate's
        /// column at the line, followed up while walls still stand at either side of it.
        /// </summary>
        /// <summary>How far past the open run a shaft's window reaches, for the walls beside it.</summary>
        const int ShaftWallCells = 3;

        static List<Vector2Int> ShaftCells(CellGrid grid, int column, int line, out int x0, out int x1)
        {
            var cells = new List<Vector2Int>();
            x0 = x1 = column;
            if (!grid.InBounds(column, line) || grid.IsSolid(column, line)) return cells;
            while (x0 - 1 >= 0 && column - x0 < 12 && !grid.IsSolid(x0 - 1, line)) x0--;
            while (x1 + 1 < grid.Width && x1 - column < 12 && !grid.IsSolid(x1 + 1, line)) x1++;
            for (int y = line; y < grid.Height && y < line + 60; y++)
            {
                if (!grid.IsSolid(x0 - 1, y) && !grid.IsSolid(x1 + 1, y)) break;
                for (int x = x0; x <= x1; x++) cells.Add(new Vector2Int(x, y));
            }
            return cells;
        }

        /// <summary>
        /// Scenery a scene lays past one of its joined gate lines: wholly past it (the neighbour
        /// shows there now), the black blocks and silhouettes that frame the walk-out, and the
        /// gate's own dressing. Restored when the link goes away.
        /// </summary>
        /// <summary>
        /// The shaft above a scene's bottom gate, in the scene's own coordinates: the gate's width
        /// and a little more, from just under the gate line to 12 units above it.
        /// </summary>
        static Rect ShaftArea(TransitionPoint gate, LiveScene ls, float lineY)
        {
            var col = gate.GetComponent<Collider2D>();
            var b = col != null ? col.bounds : new Bounds(gate.transform.position, new Vector3(4f, 1f, 0f));
            float x0 = b.min.x - ls.World.x - 3f, x1 = b.max.x - ls.World.x + 3f;
            return Rect.MinMaxRect(x0, lineY - 1f, x1, lineY + 12f);
        }

        static void ApplyArt(LiveScene ls, List<(string key, Rect area, Transform gate, ArtRule rule)> wanted)
        {
            var keys = new HashSet<string>();
            foreach (var w in wanted) keys.Add(w.key);
            var stale = new List<string>();
            foreach (var kv in ls.HiddenArt)
                if (!keys.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var key in stale)
            {
                foreach (var r in ls.HiddenArt[key]) if (r != null && !HandPlacedArt.Destroyed.Contains(r)) r.enabled = true;
                ls.HiddenArt.Remove(key);
            }
            if (!ls.Scene.IsValid() || !ls.Scene.isLoaded) return;
            if (ls.ArtCandidates == null)
            {
                ls.ArtCandidates = new List<Renderer>();
                foreach (var go in ls.Scene.GetRootGameObjects())
                    foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                        if ((r is SpriteRenderer || r is MeshRenderer) && r.GetComponentInParent<TerraOwned>() == null && r.GetComponentInParent<HealthManager>() == null)
                            ls.ArtCandidates.Add(r);
            }
            foreach (var (key, area, gate, rule) in wanted)
            {
                if (ls.HiddenArt.ContainsKey(key)) continue;
                var world = new Rect(area.position + ls.World, area.size);
                var hidden = new List<Renderer>();
                foreach (var r in ls.ArtCandidates)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var b = r.bounds;
                    bool hide;
                    switch (rule)
                    {
                        case ArtRule.Overlap:
                            // The other scene draws this space; whatever of this one mostly lies in it
                            // would be drawn over it (the two scenes' art piled up).
                            hide = MostlyInside(b, world);
                            break;
                        case ArtRule.ShaftLining:
                            // Behind the gameplay plane, no wider than the shaft, reaching down to the gate line.
                            hide = r is SpriteRenderer && b.center.z > 0.1f && b.min.x >= world.xMin && b.max.x <= world.xMax
                                   && b.min.y <= world.yMin + 3f && b.max.y > world.yMin;
                            break;
                        default:
                            hide = (gate != null && r.transform.IsChildOf(gate)) || WhollyPast(b, world, area) || (IsEdgeMask(r) && Crosses(b, world, area));
                            break;
                    }
                    if (hide)
                    {
                        r.enabled = false;
                        hidden.Add(r);
                    }
                }
                ls.HiddenArt[key] = hidden;
            }
        }

        static bool MostlyInside(Bounds b, Rect area)
        {
            float w = b.size.x, h = b.size.y;
            float ix = Mathf.Min(b.max.x, area.xMax) - Mathf.Max(b.min.x, area.xMin);
            float iy = Mathf.Min(b.max.y, area.yMax) - Mathf.Max(b.min.y, area.yMin);
            if (ix <= 0f || iy <= 0f) return false;
            if (w < 1e-3f || h < 1e-3f) return true;
            return ix * iy >= 0.5f * w * h;
        }

        static bool WhollyPast(Bounds b, Rect world, Rect local)
        {
            bool horizontal = Mathf.Abs(local.xMin) >= Far * 0.5f || Mathf.Abs(local.xMax) >= Far * 0.5f;
            if (horizontal)
                return b.min.x >= world.xMin && b.max.x <= world.xMax && b.max.y > world.yMin && b.min.y < world.yMax;
            return b.min.y >= world.yMin && b.max.y <= world.yMax && b.max.x > world.xMin && b.min.x < world.xMax;
        }

        /// <summary>Reaches across the gate line (and overlaps the shared span along it).</summary>
        static bool Crosses(Bounds b, Rect world, Rect local)
        {
            if (Mathf.Abs(local.xMin) >= Far * 0.5f || Mathf.Abs(local.xMax) >= Far * 0.5f)
            {
                float seam = local.xMin <= -Far * 0.5f ? world.xMax : world.xMin;
                return b.min.x < seam && b.max.x > seam && b.max.y > world.yMin && b.min.y < world.yMax;
            }
            float line = local.yMin <= -Far * 0.5f ? world.yMax : world.yMin;
            return b.min.y < line && b.max.y > line && b.max.x > world.xMin && b.min.x < world.xMax;
        }

        /// <summary>The game's plain black block, or scenery tinted solid black (silhouettes framing an exit).</summary>
        static bool IsEdgeMask(Renderer r)
        {
            if (!(r is SpriteRenderer sr) || sr.sprite == null) return false;
            if (sr.sprite.name.StartsWith("black_solid")) return true;
            var col = sr.color;
            return col.a > 0.5f && col.r < 0.05f && col.g < 0.05f && col.b < 0.05f;
        }

        // --- Loading neighbours, one at a time, in the background. ---

        void PumpLoads(GameManager gm)
        {
            if (loadingBusy || toLoad.Count == 0 || Time.unscaledTime < nextLoadTime) return;
            if (gm.gameState != GameState.PLAYING || gm.IsInSceneTransition) return;
            loading = toLoad[0];
            toLoad.RemoveAt(0);
            loadingBusy = true;
            worstLoadFrame = 0f;
            loadStarted = Time.realtimeSinceStartup;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            DevLoadProfiler.Recording = true;
            DeferredFsms.HoldScene = loading;
            Plugin.Log.LogInfo($"[无缝] 后台载入相邻场景 {loading}");
            if (USceneManager.LoadSceneAsync(loading, LoadSceneMode.Additive) == null)
            {
                refused.Add(loading);
                FinishLoad();
            }
        }

        // Where the frame a neighbour appears in goes: the game's own activation (every Awake at
        // once, which Unity can't spread over frames), our joining, and the scripts' Start.
        float lastFrameEnd, loadedAt, admitMs;
        string traced;

        void LateUpdate()
        {
            // After every Update: a scene's grade markers reset the blend each frame the player is
            // outside them.
            if (Enabled && current != null && current.Terrain != null && !waitingToTransition) UpdateGradeBlend();
            float now = Time.realtimeSinceStartup;
            if (traced != null)
                Plugin.Log.LogInfo($"[无缝] {traced} 出现那一帧：上一帧结束到场景出现 {(loadedAt - lastFrameEnd) * 1000f:F0} ms（激活、Awake），" +
                                   $"接入 {admitMs:F0} ms，接入后到这帧结束 {(now - loadedAt) * 1000f - admitMs:F0} ms（Start、Update）");
            traced = null;
            lastFrameEnd = now;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!loadingBusy || scene.name != loading || mode != LoadSceneMode.Additive) return;
            loadedAt = Time.realtimeSinceStartup;
            traced = scene.name;
            try
            {
                Admit(scene);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[无缝] 接入 {scene.name} 出错：{e}");
                Refuse(scene, true);
            }
            admitMs = (Time.realtimeSinceStartup - loadedAt) * 1000f;
            FinishLoad();
        }

        /// <summary>
        /// In the frame the neighbour appears, before any Start: move it behind its gate, give it
        /// a grid, quieten it, and hold its scene setup until the player walks in.
        /// </summary>
        void Admit(Scene scene)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var roots = scene.GetRootGameObjects();
            tk2dTileMap map = null;
            SceneManager sm = null;
            bool bossLoads = false;
            foreach (var go in roots)
            {
                if (map == null && go.CompareTag("TileMap")) map = go.GetComponent<tk2dTileMap>();
                if (sm == null) sm = go.GetComponentInChildren<SceneManager>(true);
                if (go.GetComponentInChildren<SceneAdditiveLoadConditional>(true) != null) bossLoads = true;
            }
            var link = links.Find(l => l.Target == scene.name && !l.Placed);
            if (link == null || current == null)
            {
                Refuse(scene, false);
                return;
            }
            // Scenes that pull in more scenes (boss arenas) or aren't ordinary gameplay stay behind ordinary transitions.
            if (map == null || sm == null || sm.sceneType != SceneType.GAMEPLAY || bossLoads || map.data == null)
            {
                Plugin.Log.LogInfo($"[无缝] {scene.name} 不能接（{(bossLoads ? "带额外场景" : "不是普通场景")}），{link.Gate.name} 保持原版切换");
                Refuse(scene, true);
                return;
            }
            var outlines = TerrainSystem.TerrainOutlines(map);
            if (outlines.Count == 0)
            {
                Refuse(scene, true);
                return;
            }

            // Where it goes: worked out at its own coordinates, then the whole scene is moved.
            var probe = TerrainSystem.BuildGrid(map, outlines, TerrainSystem.HandPlacedTerrain(map), scene.name, out _, out _);
            long probeMs = sw.ElapsedMilliseconds;
            var ls = new LiveScene { Name = scene.name, Scene = scene, Sm = sm };
            if (!Join(link, ls, probe))
            {
                Refuse(scene, false);
                skipHere.Add(SkipKey(scene.name));
                return;
            }
            if (!YieldOverlaps(link, probe.Width, probe.Height))
            {
                link.Placed = false;
                Refuse(scene, false);
                skipHere.Add(SkipKey(scene.name));
                return;
            }

            // Loaded at its own coordinates; move every root behind the gate before anything starts.
            var delta = (Vector3)((Vector2)ls.Offset * CellGrid.CellSize);
            foreach (var go in roots) go.transform.position += delta;
            // Its FSMs keep working in the scene's own coordinates.
            SceneFrames.Set(scene, delta);
            long t0 = sw.ElapsedMilliseconds;
            Physics2D.SyncTransforms();
            long syncMs = sw.ElapsedMilliseconds - t0;
            scenes[ls.Name] = ls;
            // The probe grid is in the scene's own cells, which moving the scene doesn't change.
            ls.Terrain = TerrainSystem.AttachTo(map, ls.Name, makeCurrent: false, prebuilt: probe);
            long attachMs = sw.ElapsedMilliseconds - t0 - syncMs;
            if (ls.Terrain == null)
            {
                scenes.Remove(ls.Name);
                link.Placed = false;
                Refuse(scene, true);
                return;
            }
            foreach (var go in roots)
                foreach (var a in go.GetComponentsInChildren<AudioSource>(true))
                    if (!a.mute)
                    {
                        a.mute = true;
                        ls.Muted.Add(a);
                    }
            // Other gates leading to it join too.
            foreach (var l in links)
                if (l != link && l.Target == ls.Name && !l.Placed) Join(l, ls, null);
            long t1 = sw.ElapsedMilliseconds;
            ApplyLinks();
            long applyMs = sw.ElapsedMilliseconds - t1, total = sw.ElapsedMilliseconds;
            int fsms = 0, behaviours = 0;
            foreach (var go in roots)
            {
                fsms += go.GetComponentsInChildren<PlayMakerFSM>(true).Length;
                behaviours += go.GetComponentsInChildren<MonoBehaviour>(true).Length;
            }
            Plugin.Log.LogInfo($"[无缝] 接入 {ls.Name}：在{SideName(link.Side)}，偏移 {ls.Offset}（格），{total} ms" +
                               $"（试放格子 {probeMs}，物理同步 {syncMs}，地形 {attachMs}，切边和藏美术 {applyMs}；场景里 {fsms} 个 FSM / {behaviours} 个脚本）");
            // Its enemies' templates (and their doubles), moved into place and before anything starts.
            EnemyRespawn.Instance?.Capture(scene);
        }

        static string SideName(GatePosition side) =>
            side == GatePosition.right ? "右边" : side == GatePosition.left ? "左边" : side == GatePosition.top ? "上面" : "下面";

        /// <summary>A neighbour that won't be joined: switch it off before anything starts, and unload it.</summary>
        void Refuse(Scene scene, bool forever)
        {
            if (forever) refused.Add(scene.name);
            foreach (var go in scene.GetRootGameObjects()) go.SetActive(false);
            USceneManager.UnloadSceneAsync(scene);
        }

        void FinishLoad()
        {
            if (loading != null)
                StartCoroutine(ReportLoad(loading, Time.realtimeSinceStartup - loadStarted));
            loadingBusy = false;
            loading = null;
            DeferredFsms.HoldScene = null;
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
            // Spread neighbour loads out a little so their start-up hitches don't stack.
            nextLoadTime = Time.unscaledTime + 0.3f;
            if (deferred == null) return;
            var info = deferred;
            deferred = null;
            Plugin.Log.LogInfo($"[无缝] 继续等待中的切换 -> {info.SceneName}");
            GameManager.instance.BeginSceneTransition(info);
        }

        /// <summary>The scene's Start methods run the frame after it appears; that frame counts too.</summary>
        IEnumerator ReportLoad(string name, float seconds)
        {
            yield return null;
            worstLoadFrame = Mathf.Max(worstLoadFrame, Time.unscaledDeltaTime);
            yield return null;
            worstLoadFrame = Mathf.Max(worstLoadFrame, Time.unscaledDeltaTime);
            LastLoadWorstFrameMs = worstLoadFrame * 1000f;
            Plugin.Log.LogInfo($"[无缝] {name} 后台载入 {seconds:F2}s，期间最长一帧 {LastLoadWorstFrameMs:F0} ms");
            if (DevLoadProfiler.Recording)
            {
                DevLoadProfiler.Recording = false;
                DevLoadProfiler.Report(name);
            }
        }

        void Unload(LiveScene ls)
        {
            scenes.Remove(ls.Name);
            ls.Terrain?.Detach();
            if (ls.Scene.IsValid() && ls.Scene.isLoaded) USceneManager.UnloadSceneAsync(ls.Scene);
            Plugin.Log.LogInfo($"[无缝] 卸载 {ls.Name}");
        }

        // --- Crossing a gate line. ---

        void CheckCrossing(GameManager gm)
        {
            if (gm.gameState != GameState.PLAYING || gm.IsInSceneTransition) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.transitioning) return;
            if (current?.Terrain?.Region == null) return;
            Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
            var cell = CellGrid.WorldToCell(center);
            var cur = current.Terrain.Region;
            if (cur.Contains(cell.x, cell.y)) return;
            foreach (var link in links)
            {
                if (!link.Placed || link.Neighbour?.Terrain == null) continue;
                var region = link.Neighbour.Terrain.Region;
                // A cell clear of the line, so standing on it doesn't flip back and forth.
                var back = cell - link.Direction;
                if (region.Contains(cell.x, cell.y) && region.Contains(back.x, back.y))
                {
                    // Straight back into the scene just left, without having moved: the two scenes
                    // disagree about who owns this spot. Stay put rather than flip every frame. (A
                    // real way back is at least three cells from where the line was crossed.)
                    if (link.Neighbour == lastLeft && (cell - lastCrossCell).sqrMagnitude <= 4)
                    {
                        if (!bounceLogged)
                        {
                            bounceLogged = true;
                            Plugin.Log.LogWarning($"[无缝] 刚从 {lastLeft.Name} 过来，原地又要回去，没切换。格 {cell}，各场景:{Claims(cell)}");
                        }
                        return;
                    }
                    Promote(link);
                    return;
                }
            }
        }

        /// <summary>Which loaded scenes own a cell, and for the rest why not (outside it, or the cut that took it away).</summary>
        string Claims(Vector2Int cell)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ls in scenes.Values)
            {
                var r = ls.Terrain?.Region;
                if (r == null) continue;
                sb.Append($" {ls.Name}{(ls == current ? "(当前)" : "")}=");
                if (!r.CellRect.Contains(cell)) sb.Append($"范围外{r.CellRect}");
                else if (r.Contains(cell.x, cell.y)) sb.Append("归它");
                else
                    foreach (var cut in r.Grid.Cuts)
                        if (cut.Contains(cell - r.CellOffset))
                        {
                            sb.Append($"被切掉{new RectInt(cut.position + r.CellOffset, cut.size)}");
                            break;
                        }
            }
            return sb.ToString();
        }

        /// <summary>The neighbour becomes the scene being played. Nothing loads; the player keeps moving.</summary>
        void Promote(Link link)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var gm = GameManager.instance;
            var hero = HeroController.instance;
            var from = current;
            var to = link.Neighbour;
            // Its scene setup talks to its FSMs: any still waiting to be set up must be ready first.
            DeferredFsms.Pump(all: true);
            // A drop through a gate between stacked scenes would have been cut short by the
            // transition; the scenes weren't built for the whole fall to hurt (Terraria fall damage).
            if (link.Vertical) TerraMod.Instance?.ResetFall();
            Vector2 heroCenter = hero.GetComponent<BoxCollider2D>().bounds.center;
            var cell = CellGrid.WorldToCell(heroCenter);
            string claims = Claims(cell);
            lastLeft = from;
            lastCrossCell = cell;
            bounceLogged = false;
            CrossCount++;
            gm.SaveLevelState();

            // The game's notion of the active scene, without its automatic level start.
            ref bool wait = ref waitForManualLevelStart(gm);
            bool saved = wait;
            wait = true;
            try
            {
                USceneManager.SetActiveScene(to.Scene);
            }
            finally
            {
                wait = saved;
            }
            current = to;
            TerrainSystem.Current = to.Terrain;
            gm.UpdateSceneName();
            setSceneManager.Invoke(gm, new object[] { to.Sm });
            gm.RefreshTilemapInfo(to.Name);
            // The scene's SceneManager marks the scene visited (the map draws visited rooms) when it
            // starts, by the game's current scene name: loaded behind the player that was still the
            // last one's (and its own fields unset), so it is marked now, as AddSceneMapped does.
            if (to.Sm != null) MarkVisited(to.Sm, gm);
            GameCameras.instance.SceneInit();
            hero.SceneInit();
            if (gm.gameMap != null)
            {
                var gameMap = gm.gameMap.GetComponent<GameMap>();
                gameMap.LevelReady();
                // The frame the scene set for the compass when it loaded behind the player (MapFrames).
                if (UI.MapFrames.TryGet(to.Name, out var frame)) gameMap.SetManualTilemap(frame.x, frame.y, frame.z, frame.w);
            }

            // What walking through the gate would have done, then (as when a scene loads after the
            // gate) the scene's own entry setup on top: the first time its real Start, afterwards
            // the same settings again.
            var gate = link.Gate;
            if (gate.atmosSnapshot != null) gate.atmosSnapshot.TransitionTo(1.5f);
            if (gate.enviroSnapshot != null) gate.enviroSnapshot.TransitionTo(1.5f);
            if (gate.actorSnapshot != null) gate.actorSnapshot.TransitionTo(1.5f);
            if (gate.musicSnapshot != null) gate.musicSnapshot.TransitionTo(1.5f);
            if (!to.Entered) RunEntryStart(to);
            else SceneMood.Apply(to.Sm);
            TransitionPoint.lastEntered = gate.name;
            var entry = link.EntryGate;
            if (entry != null && !entry.nonHazardGate && entry.respawnMarker != null) PlayerData.instance.SetHazardRespawn(entry.respawnMarker);

            foreach (var a in to.Muted) if (a != null) a.mute = false;
            to.Muted.Clear();
            foreach (var go in from.Scene.GetRootGameObjects())
                foreach (var a in go.GetComponentsInChildren<AudioSource>(true))
                    if (!a.mute)
                    {
                        a.mute = true;
                        from.Muted.Add(a);
                    }

            LastPromoteMs = sw.ElapsedMilliseconds;
            var scm = GameCameras.instance != null ? GameCameras.instance.sceneColorManager : null;
            Plugin.Log.LogInfo($"[无缝] 越过{SideName(link.Side)}门线：{from.Name} -> {to.Name}，{LastPromoteMs} ms，主角 {heroCenter:F1} 格 {cell}，" +
                               $"调色混合 {(scm != null ? scm.Factor : -1f):F2}，各场景:{claims}");
            // Hero-in-position listeners (area titles), then "finished entering": map, our own relinking.
            // The camera listens too, to snap onto the hero and fade the level in after a load;
            // nothing was loaded and the camera never left him, so it sits this one out.
            Promoting = true;
            try
            {
                sendHeroInPosition(hero, false);
                gm.FinishedEnteringScene();
            }
            finally
            {
                Promoting = false;
            }
            // The scene's own grade just replaced the blend's A side; blend towards the scene left behind now, not next frame.
            UpdateGradeBlend();
        }

        /// <summary>
        /// The scene's SceneManager.Start, held back while it was only a neighbour. It places the
        /// shade and dream gate at stored scene coordinates, so those are moved with the scene.
        /// </summary>
        static void RunEntryStart(LiveScene ls)
        {
            ls.Entered = true;
            var pd = PlayerData.instance;
            var o = ls.World;
            pd.shadePositionX += o.x;
            pd.shadePositionY += o.y;
            pd.dreamGateX += o.x;
            pd.dreamGateY += o.y;
            try
            {
                sceneManagerStart.Invoke(ls.Sm, null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[无缝] {ls.Name} 的场景设置出错：{e}");
            }
            finally
            {
                pd.shadePositionX -= o.x;
                pd.shadePositionY -= o.y;
                pd.dreamGateX -= o.x;
                pd.dreamGateY -= o.y;
            }
            foreach (var title in ls.PendingTitles)
                if (title != null) areaTitleStart.Invoke(title, null);
            ls.PendingTitles.Clear();
        }

        bool IsWaiting(Scene scene) =>
            scenes.TryGetValue(scene.name, out var ls) && ls.Scene == scene && ls != current && !ls.Entered;

        // --- Patches. ---

        /// <summary>A neighbour's scene setup (music, colours, darkness, shade) waits until the player enters it.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SceneManager), "Start")]
        static bool HoldSceneStart(SceneManager __instance) =>
            !Enabled || Instance == null || !Instance.IsWaiting(__instance.gameObject.scene);

        /// <summary>So does its area title.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AreaTitleController), "Start")]
        static bool HoldAreaTitle(AreaTitleController __instance)
        {
            if (!Enabled || Instance == null || !Instance.IsWaiting(__instance.gameObject.scene)) return true;
            Instance.scenes[__instance.gameObject.scene.name].PendingTitles.Add(__instance);
            return false;
        }

        /// <summary>
        /// The black walls the game puts around a scene's edges assume the camera stops there;
        /// past the edge there is either the neighbour or nothing, so no walls.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SceneManager), "OnCameraAspectChanged")]
        static bool NoSceneBorders() => !Enabled || TerraMod.Instance == null;

        /// <summary>Joined gates do nothing (crossing the line does), and nor do any gates of neighbours.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TransitionPoint), "TryDoTransition")]
        static bool TryDoTransition(TransitionPoint __instance)
        {
            var w = Instance;
            if (!Enabled || w == null || w.current == null) return true;
            return !w.joinedGates.Contains(__instance) && __instance.gameObject.scene == w.current.Scene;
        }

        /// <summary>
        /// An ordinary scene change: wait for a background load to finish (the game would find the
        /// wrong scene), then drop every neighbour; the new scene starts a new world at its own
        /// coordinates. A shade left by dying in a moved scene is put back in its scene's coordinates.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.BeginSceneTransition))]
        static bool BeforeSceneTransition(GameManager.SceneLoadInfo info)
        {
            var w = Instance;
            if (!Enabled || w == null) return true;
            if (w.loadingBusy)
            {
                w.deferred = info;
                Plugin.Log.LogInfo($"[无缝] 相邻场景载入中，切换到 {info.SceneName} 稍后进行");
                return false;
            }
            var cur = w.current;
            // The game finds the scene it has just loaded by name, so a neighbour of that name still
            // unloading would be taken for it and the transition would hang. That neighbour goes
            // first; then the same transition is made again.
            if (w.scenes.TryGetValue(info.SceneName, out var target) && target != cur && target.Scene.IsValid() && target.Scene.isLoaded)
            {
                Plugin.Log.LogInfo($"[无缝] 原版切换到已载入的相邻场景 {info.SceneName}（入口 {info.EntryGateName}）：先把它卸载，再切换。发起：{Caller()}");
                target.Terrain?.Detach();
                w.scenes.Remove(target.Name);
                w.waitingToTransition = true;
                w.StartCoroutine(w.TransitionWhenUnloaded(USceneManager.UnloadSceneAsync(target.Scene), info));
                return false;
            }
            var pd = PlayerData.instance;
            var hero = HeroController.SilentInstance;
            if (cur != null && cur.Offset != Vector2Int.zero && hero != null && hero.cState.dead && pd.shadeScene == cur.Name)
            {
                pd.shadePositionX -= cur.World.x;
                pd.shadePositionY -= cur.World.y;
            }
            foreach (var ls in w.scenes.Values)
                if (ls != cur && ls.Scene.IsValid() && ls.Scene.isLoaded)
                {
                    ls.Terrain?.Detach();
                    USceneManager.UnloadSceneAsync(ls.Scene);
                }
            cur?.Terrain?.Detach();
            w.scenes.Clear();
            w.links.Clear();
            w.toLoad.Clear();
            w.joinedGates.Clear();
            w.parked.Clear();
            w.current = null;
            w.lastLeft = null;
            // The game drops the blend (factor 0) when it unloads a level.
            w.gradeTarget = null;
            return true;
        }

        bool waitingToTransition;

        /// <summary>The game methods that led here, for the log.</summary>
        static string Caller()
        {
            var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
            if (frames == null) return "?";
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var f in frames)
            {
                var m = f.GetMethod();
                if (m?.DeclaringType == null || m.DeclaringType.Namespace?.StartsWith("TerraKnight") == true || m.DeclaringType.Namespace?.StartsWith("HarmonyLib") == true) continue;
                if (shown++ > 0) sb.Append(" <- ");
                sb.Append(m.DeclaringType.Name).Append('.').Append(m.Name);
                if (shown >= 5) break;
            }
            return sb.ToString();
        }

        IEnumerator TransitionWhenUnloaded(AsyncOperation unload, GameManager.SceneLoadInfo info)
        {
            if (unload != null)
                while (!unload.isDone) yield return null;
            waitingToTransition = false;
            GameManager.instance.BeginSceneTransition(info);
        }

        /// <summary>A scene loaded beside the current one must not pull in its boss scenes before it is refused.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SceneAdditiveLoadConditional), "OnEnable")]
        static bool AdditiveLoad(SceneAdditiveLoadConditional __instance) =>
            Instance == null || !Instance.loadingBusy || __instance.gameObject.scene.name != Instance.loading;

        /// <summary>The map's compass reads the hero's position within the scene; a moved scene's origin isn't zero.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMap), "GetTilemapDimensions")]
        static void MapOrigin(GameMap __instance)
        {
            var cur = Instance?.current;
            if (cur == null || cur.Offset == Vector2Int.zero) return;
            mapOriginX(__instance) = -cur.World.x;
            mapOriginY(__instance) = -cur.World.y;
        }

        // --- Dev. ---

        /// <summary>Dev: a joined neighbour on the given side, or any.</summary>
        public bool TryGetNeighbour(GatePosition preferred, out string scene, out GatePosition side, out Vector2 gate, out Vector2Int offset)
        {
            scene = null;
            side = GatePosition.unknown;
            gate = default;
            offset = default;
            Link found = null;
            foreach (var l in links)
                if (l.Placed && (found == null || (l.Side == preferred && found.Side != preferred))) found = l;
            if (found == null || (preferred != GatePosition.unknown && found.Side != preferred)) return false;
            scene = found.Target;
            side = found.Side;
            gate = GateCenter(found.Gate);
            offset = found.Neighbour.Offset;
            return true;
        }

        public bool IsLoaded(string scene) => scenes.ContainsKey(scene);

        /// <summary>Loaded beside the current scene, behind the player.</summary>
        public bool IsNeighbour(string scene) => scene != null && scenes.TryGetValue(scene, out var ls) && ls != current;

        public Scene SceneOf(string name) => scenes.TryGetValue(name, out var ls) ? ls.Scene : default;
    }
}
