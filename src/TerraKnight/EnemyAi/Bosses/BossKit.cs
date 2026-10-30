using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>A kit's boss (its journal record; several for the same kit, or one kit a record's variants by scene).</summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class KitAttribute : System.Attribute
    {
        public readonly string Record;
        public KitAttribute(string record) { Record = record; }
    }

    /// <summary>Every kit with a <see cref="KitAttribute"/>, found once.</summary>
    internal static class Kits
    {
        internal static void RegisterAll()
        {
            System.Type[] types;
            try { types = typeof(BossKit).Assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || !typeof(BossKit).IsAssignableFrom(t)) continue;
                foreach (KitAttribute a in t.GetCustomAttributes(typeof(KitAttribute), false)) BossAi.Register(a.Record, t);
            }
        }
    }

    /// <summary>
    /// One boss's new AI (空洞骑士首领新版AI设计.md): its own script (PlayMaker) stays and leads the fight; a kit operates on it
    /// once (<see cref="Setup"/>: shorter waits, a locked or led aim, new states for new moves) and adds what it throws itself
    /// through Hostiles (shots and areas, warned first, sent to the LAN game's others). Common to all of them:
    /// <list type="bullet">
    /// <item>whom it goes for (this game's player or another LAN player, Net.EnemyTargets), where he is and how he moves;</item>
    /// <item>a lock (where he is at a warning's start) and a lead (where he will be), both taken once and shown (§1.1);</item>
    /// <item>the warnings (§1.2): a mark on the ground, a line, a ring; and the difficulty's warning length;</item>
    /// <item>the pace (§1.3): its script's waits in the states a kit names, shorter by the difficulty and the boss's own;</item>
    /// <item>the rank (§1.3): 0, 1 below two thirds of its life, 2 below one third;</item>
    /// <item>the air (§1.4): the player kept up off the ground (camping), long enough to force the boss's move against it
    /// (its own moves reach him: nothing strikes a player for where he stands);</item>
    /// <item>stuck (§1.5): in the room's own ground or out of its arena too long, put back where it can stand.</item>
    /// </list>
    /// Distances in the game's units unless a name says tiles (Move's sizes are tiles, as the common enemies').
    /// </summary>
    internal abstract class BossKit : MonoBehaviour, IAim
    {
        public HealthManager Hm { get; private set; }
        public Items.EnemyStats.Info Info { get; private set; }
        public string Record => Info?.Record;
        public string Scene { get; private set; }
        UnityEngine.SceneManagement.Scene sceneRef;
        protected Collider2D body;
        protected Rigidbody2D rb;

        /// <summary>The boss's main script (<see cref="ControlName"/>), or null.</summary>
        protected PlayMakerFSM Control;

        /// <summary>The name of the boss's main script.</summary>
        protected virtual string ControlName => "Control";

        /// <summary>The boss's own pace (§1.3: "节奏 ×0.85"), over the difficulty's.</summary>
        protected virtual float Pace => 1f;

        /// <summary>Its animations' own speed over the difficulty's (<see cref="BossAi.AnimScale"/>); 1 by default.</summary>
        public virtual float AnimPace => 1f;

        /// <summary>It goes into the ground (a burrower): the watchdog leaves it there.</summary>
        protected virtual bool Burrows => false;

        /// <summary>It goes out of its arena on its own (up into a ceiling, off the screen): the watchdog lets it.</summary>
        protected virtual bool Roams => false;

        /// <summary>
        /// It is in the ground on purpose now (a burrower always; a kit may name moves, a charge through a wall, a bounce off
        /// one): the watchdog leaves it there, and still brings it back from out of the arena.
        /// </summary>
        protected virtual bool InGroundNow => Burrows;

        /// <summary>Its colour (§1.2): its warnings and the strike on a player out of its arena.</summary>
        protected virtual Look Theme => Look.Infection;

        /// <summary>The kit's work on the boss's script, once, as it starts.</summary>
        protected abstract void Setup();

        /// <summary>Each frame while it lives, has someone to go for and runs here (not a LAN puppet).</summary>
        protected virtual void Tick() { }

        /// <summary>
        /// Set by a kit's Setup that leaves the work to another (a second body of one script, the Mantis Lords' sub,
        /// the Soul Master's second): it then strikes no one out of the arena, nor is watched, nor ticks.
        /// </summary>
        protected bool Dormant;

        /// <summary>A Dream_ scene's (the dream return: the Failed Champion, the Soul Tyrant, the Lost Kin...).</summary>
        protected bool Dream => Scene != null && Scene.StartsWith("Dream_");
        /// <summary>Godhome's (the Hall of Gods, the pantheons).</summary>
        protected bool Godhome => Scene != null && Scene.StartsWith("GG_");

        bool begun, down;

        internal void Begin(HealthManager hm, Items.EnemyStats.Info info)
        {
            Hm = hm;
            Info = info;
            sceneRef = hm.gameObject.scene;
            Scene = sceneRef.name;
            body = hm.GetComponent<Collider2D>();
            rb = hm.GetComponent<Rigidbody2D>();
            Control = FindFsm(ControlName);
            startedAt = Me;
            ArenaFrom(Me);
            BossAi.Up(this);
            begun = true;
            // One kit works a script: a boss of two health managers on one script (the False Knight's body and head) gets
            // its work done once, by the kit on the script's own object if there is one (decided at its first frame).
            if (Control != null)
            {
                int id = Control.GetInstanceID();
                if (!owners.TryGetValue(id, out var was) || was == null || !was.begun || was.Hm == null
                    || gameObject == Control.gameObject && was.gameObject != Control.gameObject)
                    owners[id] = this;
            }
            setupPending = true;
        }

        static readonly Dictionary<int, BossKit> owners = new Dictionary<int, BossKit>();
        bool setupPending;

        /// <summary>Its first frame: its work on the script, unless another kit of the same script does it.</summary>
        void SetupNow()
        {
            setupPending = false;
            if (Control != null && owners.TryGetValue(Control.GetInstanceID(), out var owner) && owner != null && owner != this)
            {
                Dormant = true;
                Plugin.Log.LogInfo($"[首领AI] {Record}（{Scene}）的 {gameObject.name} 和 {owner.gameObject.name} 共用状态机 {ControlName}，交给后者");
                return;
            }
            Setup();
            if (!Dormant)
            {
                BossAi.Quicken(this, gameObject);
                if (Control != null && Control.gameObject != gameObject) BossAi.Quicken(this, Control.gameObject);
            }
            Plugin.Log.LogInfo($"[首领AI] {Record}（{Scene}）新版招式就位{(Control == null ? $"，没找到状态机 {ControlName}" : "")}");
        }

        void OnDestroy()
        {
            StopRoutines();
            if (!begun) return;
            if (!down) { down = true; BossAi.Down(this, sceneRef, Scene); }
            BossAi.Forget(this);
            try { Teardown(); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[首领AI] {Record} 收尾出错：{e.Message}"); }
        }

        /// <summary>A kit's own clearing up as the boss goes (what it made that outlives it, a time scale it holds).</summary>
        protected virtual void Teardown() { }

        protected virtual void OnDisable() => StopRoutines();

        readonly Dictionary<IEnumerator, IEnumerator> routines = new Dictionary<IEnumerator, IEnumerator>();

        void StopRoutines()
        {
            StopAllCoroutines();
            var pending = new List<IEnumerator>(routines.Values);
            routines.Clear();
            foreach (var routine in pending)
            {
                try { (routine as System.IDisposable)?.Dispose(); }
                catch (System.Exception ex) { Plugin.Log.LogWarning($"[首领AI] {Record} 停用收尾出错：{ex.Message}"); }
            }
        }

        // ------------------------------------------------------------------ whom it goes for

        public bool HasTarget { get; private set; }
        public Vector2 TargetCenter { get; private set; }
        public Vector2 TargetVelocity { get; private set; }
        /// <summary>The player it goes for is this game's own (else another LAN player's, <see cref="TargetSlot"/>).</summary>
        public bool TargetLocal { get; private set; } = true;
        public int TargetSlot { get; private set; } = -1;
        /// <summary>His feet (the bottom of his body).</summary>
        public Vector2 TargetFeet { get; private set; }
        /// <summary>He stands on the room's own ground now.</summary>
        public bool TargetOnGround { get; private set; }

        Vector2 lastPos;
        float lastPosAt;

        /// <summary>The middle of the boss's body.</summary>
        public Vector2 Me => body != null && body.enabled ? (Vector2)body.bounds.center : (Vector2)transform.position;

        static bool NetSync => Net.NetSession.Active && Net.Enemies.Enabled;

        void Track()
        {
            HasTarget = false;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            Vector2 at;
            TargetLocal = true;
            if (NetSync)
            {
                if (!Net.EnemyTargets.Where(Hm, out at, out int slot)) return;
                TargetSlot = slot;
                TargetLocal = slot == Net.NetSession.MySlot;
            }
            else at = hero.transform.position;
            if (TargetLocal)
            {
                if (hero.cState.dead) return;
                var hc = hero.GetComponent<Collider2D>();
                at = hc != null ? (Vector2)hc.bounds.center : (Vector2)hero.transform.position;
                TargetFeet = hc != null ? new Vector2(at.x, hc.bounds.min.y) : at + Vector2.down * 0.6f;
                TargetOnGround = hero.cState.onGround && Hostiles.NativeRay(TargetFeet + Vector2.up * 0.1f, Vector2.down, 0.4f).collider != null;
            }
            else
            {
                TargetFeet = at + Vector2.down * 0.6f;
                TargetOnGround = Hostiles.NativeRay(at, Vector2.down, 0.9f).collider != null;
            }
            HasTarget = true;
            float now = Time.time;
            if (lastPosAt > 0f && now - lastPosAt > 0.0001f)
            {
                var v = (at - lastPos) / (now - lastPosAt);
                if (v.magnitude > 80f) v = Vector2.zero;
                TargetVelocity = Vector2.Lerp(TargetVelocity, v, 0.35f);
            }
            lastPos = at;
            lastPosAt = now;
            TargetCenter = at;
        }

        // ------------------------------------------------------------------ the numbers

        /// <summary>Its script's waits now (§1.3): the difficulty's share times the boss's pace.</summary>
        public float TempoNow => Mathf.Max(1f, BossAi.TempoScale * Pace);

        /// <summary>A warning's length (§1.2): its base (0.6 s by default) times the difficulty's, never under 0.4 s.</summary>
        public float Warn(float seconds = 0.6f) => Mathf.Max(BossAi.MinWarn, seconds * BossAi.WarnScale);

        /// <summary>A close wind-up's length, never under 0.3 s.</summary>
        public float Antic(float seconds) => Mathf.Max(BossAi.MinAntic, seconds * BossAi.WarnScale);

        /// <summary>0, 1 below two thirds of its life (or its second phase), 2 below one third (or its third phase); never under the difficulty's <see cref="BossAi.RankFloor"/>.</summary>
        public int Rank
        {
            get
            {
                int max = Info != null ? Mathf.Max(1, Info.MaxHp) : 1;
                float share = Hm != null ? Hm.hp / (float)max : 1f;
                int r = share < 0.33f ? 2 : share < 0.66f ? 1 : 0;
                if (Info != null && Items.BossPhases.Of(Info, out int phase, out _)) r = Mathf.Max(r, Mathf.Min(2, phase));
                return Mathf.Max(r, BossAi.RankFloor);
            }
        }

        /// <summary>The player was just hurt (this game's own): no new locked move starts on him (§1.6).</summary>
        public bool Grace => TargetLocal && EnemyAi.Grace;

        /// <summary>How many of this boss's strikes are warned or live now (§1.6 caps them: <see cref="RoomForStrike"/>).</summary>
        public int Strikes
        {
            get
            {
                int n = 0;
                foreach (var a in Hostiles.Attack.All)
                    if (a != null && a.Owner == Hm && a is Hostiles.Zone z && !z.MarkOnly) n++;
                return n;
            }
        }

        /// <summary>Another strike may go now.</summary>
        public bool RoomForStrike => Strikes < BossAi.MaxStrikes;

        // ------------------------------------------------------------------ locks and leads (§1.1)

        /// <summary>Where the player is now (taken once: he can walk out of what it aims at).</summary>
        public Vector2 Lock()
        {
            BossAi.DevLocks++;
            return TargetCenter;
        }

        /// <summary>
        /// Where he will be in some seconds, by the difficulty's weight (and a kit's): in the arena, unless he is out of it
        /// himself (dug in under its floor, up over it), then where he goes.
        /// </summary>
        public Vector2 Lead(float seconds, float weight = 1f)
        {
            BossAi.DevLocks++;
            var p = TargetCenter + TargetVelocity * seconds * BossAi.LeadWeight * weight;
            return Arena.Contains(TargetCenter) ? ClampToArena(p, 0.5f) : p;
        }

        /// <summary>Sometimes led (the difficulty's chance, §1.1), else where he is.</summary>
        public Vector2 MaybeLead(float seconds, float weight = 1f) => RollLead() ? Lead(seconds, weight) : Lock();

        /// <summary>A "sometimes" lead comes up.</summary>
        public bool RollLead() => Random.value < BossAi.LeadChance;

        /// <summary>The room's own ground under a point (its y), or the point's own y with none within reach.</summary>
        public float GroundBelow(Vector2 p, float reach = 30f)
        {
            var hit = Hostiles.NativeRay(p, Vector2.down, reach);
            return hit.collider != null ? hit.point.y : p.y;
        }

        /// <summary>A point brought down to the room's own ground under it.</summary>
        public Vector2 OnGround(Vector2 p) => new Vector2(p.x, GroundBelow(p + Vector2.up * 0.3f));

        /// <summary>
        /// The player standing on something up off the room's floor (a one-way platform of the room's or one he built,
        /// a ledge): its ends, as far as it goes on either side of him (within a reach) without a wall in the way.
        /// </summary>
        public bool FootingSpan(out float left, out float right, float reach = 20f)
        {
            left = right = TargetFeet.x;
            if (!HasTarget || Mathf.Abs(TargetVelocity.y) > 1f || !Hostiles.FootingBelow(TargetFeet + Vector2.up * 0.1f, 0.4f)) return false;
            float top = TargetFeet.y;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = TargetFeet.x;
                for (float d = 0.5f; d <= reach; d += 0.5f)
                {
                    float nx = TargetFeet.x + side * d;
                    if (!Hostiles.FootingBelow(new Vector2(nx, top + 0.25f), 0.6f) || Hostiles.NativeSolidAt(new Vector2(nx, top + 1f))) break;
                    x = nx;
                }
                if (side < 0) left = x; else right = x;
            }
            return true;
        }

        /// <summary>The room's own ceiling over a point (its y), or the arena's top.</summary>
        public float CeilingAbove(Vector2 p, float reach = 30f)
        {
            var hit = Hostiles.NativeRay(p, Vector2.up, reach);
            return hit.collider != null ? hit.point.y : Arena.yMax;
        }

        /// <summary>The room's own wall a way from a point (its x), or the arena's side.</summary>
        public float WallTowards(Vector2 p, float dir, float reach = 40f)
        {
            var hit = Hostiles.NativeRay(p, new Vector2(Mathf.Sign(dir), 0f), reach);
            return hit.collider != null ? hit.point.x : dir > 0 ? Arena.xMax : Arena.xMin;
        }

        // ------------------------------------------------------------------ the arena

        /// <summary>What the fight's camera shows (World.BossArenaZone), the boss's reach.</summary>
        public Rect Arena { get; private set; }

        void ArenaFrom(Vector2 at) => Arena = ArenaAt(at);

        Rect ArenaAt(Vector2 at)
        {
            Rect arena;
            var region = World.TerrainSystem.Find(Scene);
            UnityEngine.SceneManagement.Scene? mainScene = null;
            if (Scene != null && Scene.EndsWith("_boss", System.StringComparison.OrdinalIgnoreCase))
            {
                string main = Scene.Substring(0, Scene.Length - 5);
                if (region == null) region = World.TerrainSystem.Find(main);
                var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByName(main);
                if (loaded.IsValid() && loaded.isLoaded) mainScene = loaded;
            }
            if (region != null && region.Root != null)
            {
                var room = new Rect(region.Root.position, new Vector2(region.Grid.Width, region.Grid.Height) * World.CellGrid.CellSize);
                arena = World.BossArenaZone.Around(sceneRef, room, at, mainScene);
            }
            else arena = new Rect(at.x - 20f, at.y - 10f, 40f, 24f);
            return DownToFloor(UpToCeiling(arena));
        }

        /// <summary>
        /// The view's bottom down to the room's own floor under it when that floor is lower (at most 12): a lock's view can
        /// stand well over the floor the boss fights on (Godhome's False Knight hall: the view from 31.8, the floor at 26),
        /// and a boss on its own floor was then "out of the arena" to the watchdog, which put it back up in the roof (the
        /// user's 10-25). The middle of five looks down across it, from points in the open.
        /// </summary>
        static Rect DownToFloor(Rect a)
        {
            var floors = new List<float>(5);
            for (int i = 1; i <= 5; i++)
            {
                var from = new Vector2(Mathf.Lerp(a.xMin, a.xMax, i / 6f), a.center.y);
                if (Hostiles.NativeSolidAt(from) || RoomRockAt(from)) continue;
                var hit = Hostiles.NativeRay(from, Vector2.down, 40f);
                if (hit.collider != null) floors.Add(hit.point.y);
            }
            if (floors.Count == 0) return a;
            floors.Sort();
            float floor = floors[floors.Count / 2];
            if (floor >= a.yMin) return a;
            return Rect.MinMaxRect(a.xMin, Mathf.Max(floor - 1f, a.yMin - 12f), a.xMax, a.yMax);
        }

        /// <summary>
        /// The view's top short of the room's own ceiling (the game's camera lock ends below it; ours, drawn wider, shows it):
        /// the arena up to the ceiling over its middle (the middle of five looks across it, a ledge at one side not taken for
        /// it; at most 8 more), so a boss going for a player up there (a ledge, his own platforms) isn't stopped short of him
        /// as at a wall. Open above (no ceiling in reach): as it was.
        /// </summary>
        static Rect UpToCeiling(Rect a)
        {
            var tops = new List<float>(5);
            for (int i = 1; i <= 5; i++)
            {
                var from = new Vector2(Mathf.Lerp(a.xMin, a.xMax, i / 6f), a.center.y);
                if (Hostiles.NativeSolidAt(from) || RoomRockAt(from)) continue;
                var hit = Hostiles.NativeRay(from, Vector2.up, 40f);
                if (hit.collider != null) tops.Add(hit.point.y);
            }
            if (tops.Count == 0) return a;
            tops.Sort();
            float roof = tops[tops.Count / 2];
            if (roof <= a.yMax) return a;
            return Rect.MinMaxRect(a.xMin, a.yMin, a.xMax, Mathf.Min(roof, a.yMax + 8f));
        }

        /// <summary>The arena taken again where the boss is now (a fight that moves on: the Radiance's second arena, her climb).</summary>
        protected void Rearena() => ArenaFrom(Me);

        public Vector2 ClampToArena(Vector2 p, float margin = 1f) =>
            new Vector2(Mathf.Clamp(p.x, Arena.xMin + margin, Arena.xMax - margin), Mathf.Clamp(p.y, Arena.yMin + margin, Arena.yMax - margin));

        // ------------------------------------------------------------------ warnings (§1.2)

        Move Mark(Look look) => new Move { Look = look, Masks = 1 };

        /// <summary>A line on the ground where something will land (its width in tiles), for some seconds.</summary>
        public Hostiles.Zone MarkGround(Vector2 at, float widthTiles, float seconds, Look? look = null)
        {
            var m = Mark(look ?? Theme);
            m.Width = widthTiles;
            m.Height = 1f;
            var z = Hostiles.Area(Hm, this, m, Hostiles.Zone.Shape.Pillar, OnGround(at), seconds, 0f);
            z.MarkOnly = true;
            BossAi.DevMarks++;
            return z;
        }

        /// <summary>A thin line from one point to another (a dash's, a throw's, a beam's way), for some seconds.</summary>
        public Hostiles.Zone MarkLine(Vector2 from, Vector2 to, float seconds, Look? look = null)
        {
            var m = Mark(look ?? Theme);
            var d = to - from;
            m.Length = d.magnitude / 0.5f;
            m.Width = 0.3f;
            var z = Hostiles.Area(Hm, this, m, Hostiles.Zone.Shape.Beam, from, seconds, 0f);
            z.SetAngle(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            z.MarkOnly = true;
            BossAi.DevMarks++;
            return z;
        }

        /// <summary>A ring round a point (its radius in tiles), for some seconds.</summary>
        public Hostiles.Zone MarkCircle(Vector2 at, float radiusTiles, float seconds, Look? look = null)
        {
            var m = Mark(look ?? Theme);
            m.Radius = radiusTiles;
            var z = Hostiles.Area(Hm, this, m, Hostiles.Zone.Shape.Circle, at, seconds, 0f);
            z.MarkOnly = true;
            BossAi.DevMarks++;
            return z;
        }

        // ------------------------------------------------------------------ its own attacks

        /// <summary>A move for its own attacks, in its colour: its masks (1 light, 2 heavy).</summary>
        public Move NewMove(int masks = 1, Look? look = null) => new Move { Look = look ?? Theme, Masks = masks };

        /// <summary>A strike in a place after its wait (unseen till then), live for a while: a hitbox for a move of the boss's own (Unseen on its body), never a picture of the mod's.</summary>
        public Hostiles.Zone Strike(Hostiles.Zone.Shape shape, Vector2 at, float warn, float active, Move m)
        {
            var z = Hostiles.Area(Hm, this, m, shape, at, warn, active);
            z.NativeOnly = true;
            BossAi.DevMoves++;
            return z;
        }

        /// <summary>The player's hooks let go (a slam's shake, a cut): this game's own player only.</summary>
        public void ShakeOffHooks()
        {
            if (TargetLocal) EnemyAi.CutHooks(TargetCenter);
        }

        // ------------------------------------------------------------------ its script

        /// <summary>One of the boss's scripts by name: on it, its children or its parents.</summary>
        public PlayMakerFSM FindFsm(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var f in GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return f;
            foreach (var f in GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == name) return f;
            foreach (var f in GetComponentsInParent<PlayMakerFSM>(true)) if (f.FsmName == name) return f;
            return null;
        }

        static readonly HashSet<string> warned = new HashSet<string>();

        void Missing(string what)
        {
            string key = Record + "|" + what;
            if (warned.Add(key)) Plugin.Log.LogWarning($"[首领AI] {Record}（{Scene}）找不到 {what}，这部分按原版");
        }

        /// <summary>A state of a script (the main one by default), or null (and a warning in the log once).</summary>
        public FsmState State(string name, PlayMakerFSM f = null)
        {
            f = f ?? Control;
            var s = f != null ? f.Fsm.GetState(name) : null;
            if (s == null) Missing((f != null ? f.FsmName : ControlName) + "." + name);
            return s;
        }

        /// <summary>Its script's waits in these states, at the kit's pace (§1.3; its idle and recovery ones, not its wind-ups).</summary>
        public void Tempo(params string[] states) => TempoIn(Control, states);

        public void TempoIn(PlayMakerFSM f, params string[] states)
        {
            foreach (var name in states)
            {
                var s = State(name, f);
                if (s == null) continue;
                foreach (var a in s.Actions) BossAi.Tempo(a, this);
            }
        }

        /// <summary>A state's waits at a fixed factor (under 1 shorter, over 1 longer), whatever the difficulty.</summary>
        public void TempoFixed(PlayMakerFSM f, string state, float factor)
        {
            var s = State(state, f);
            if (s == null) return;
            foreach (var a in s.Actions) BossAi.Tempo(a, this, factor);
        }

        /// <summary>Something done as a state starts (first, or after its own actions), as long as the boss runs here.</summary>
        public void OnEnter(string state, System.Action act, bool first = true, PlayMakerFSM f = null)
        {
            var s = State(state, f);
            if (s == null) return;
            Insert(s, new KitAction { Kit = this, Enter = act }, first ? 0 : s.Actions.Length);
        }

        /// <summary>Something done at a place among a state's actions (index 1: after its first, say, a GetPosition of the hero).</summary>
        public void At(string state, int index, System.Action act, PlayMakerFSM f = null)
        {
            var s = State(state, f);
            if (s == null) return;
            Insert(s, new KitAction { Kit = this, Enter = act }, index);
        }

        /// <summary>Something done as a state ends.</summary>
        public void OnExit(string state, System.Action act, PlayMakerFSM f = null)
        {
            var s = State(state, f);
            if (s == null) return;
            Insert(s, new KitAction { Kit = this, Exit = act }, s.Actions.Length);
        }

        static void Insert(FsmState s, FsmStateAction a, int index)
        {
            var list = new List<FsmStateAction>(s.Actions);
            list.Insert(Mathf.Clamp(index, 0, list.Count), a);
            s.Actions = list.ToArray();
            a.Init(s);
        }

        /// <summary>
        /// A new state of the boss's script running a kit's routine, then going on to another state (FINISHED). Its
        /// routine runs on the boss (stopped if the script leaves the state first).
        /// </summary>
        public FsmState AddState(string name, System.Func<IEnumerator> routine, string next, PlayMakerFSM f = null)
        {
            f = f ?? Control;
            if (f == null) return null;
            var fsm = f.Fsm;
            var old = fsm.GetState(name);
            if (old != null) return old;
            var to = fsm.GetState(next);
            if (to == null) { Missing(f.FsmName + "." + next); return null; }
            var s = new FsmState(fsm) { Name = name };
            var a = new RoutineAction { Kit = this, Body = routine };
            s.Actions = new FsmStateAction[] { a };
            a.Init(s);
            // Out on its own event, not FINISHED: the game's Tk2dWatchAnimationEvents leaves its callbacks on the animator,
            // and a clip this move plays would send FINISHED as it ends (another of the boss's scripts may be waiting on
            // that same animator, so the callbacks are left alone).
            s.Transitions = new[] { new FsmTransition { FsmEvent = RoutineAction.Done, ToState = next, ToFsmState = to } };
            var all = new List<FsmState>(fsm.States) { s };
            fsm.States = all.ToArray();
            return s;
        }

        /// <summary>A state's way out on an event, led elsewhere.</summary>
        public void Divert(string state, string evt, string to, PlayMakerFSM f = null)
        {
            f = f ?? Control;
            var s = State(state, f);
            var target = f != null ? f.Fsm.GetState(to) : null;
            if (s == null || target == null) { if (target == null) Missing((f != null ? f.FsmName : "") + "." + to); return; }
            foreach (var t in s.Transitions)
                if (t.EventName == evt) { t.ToState = to; t.ToFsmState = target; return; }
            Missing(state + " 的 " + evt);
        }

        /// <summary>
        /// A state may go off to another now and then: as it starts, when the test says so, its script goes to that state
        /// (a new move, an anti-air move) instead of doing its own.
        /// </summary>
        public void Branch(string state, string to, System.Func<bool> when, PlayMakerFSM f = null)
        {
            f = f ?? Control;
            var s = State(state, f);
            var target = f != null ? f.Fsm.GetState(to) : null;
            if (s == null || target == null) { if (target == null) Missing((f != null ? f.FsmName : "") + "." + to); return; }
            var ev = FsmEvent.GetFsmEvent("TK " + to);
            var list = new List<FsmTransition>(s.Transitions) { new FsmTransition { FsmEvent = ev, ToState = to, ToFsmState = target } };
            s.Transitions = list.ToArray();
            var fsm = f.Fsm;
            // A branch taken again and again in one frame is a loop with the script's own way back (the game stops a script
            // for good at a thousand moves in a frame): from the twentieth time in a frame, the script's own way is left.
            int frame = -1, times = 0;
            Insert(s, new KitAction
            {
                Kit = this,
                Enter = () =>
                {
                    if (Time.frameCount != frame) { frame = Time.frameCount; times = 0; }
                    if (++times > 20)
                    {
                        if (warned.Add(Record + "|loop|" + state)) Plugin.Log.LogWarning($"[首领AI] {Record} {state} → {to} 一帧里来回 20 次，这一帧按原版走");
                        return;
                    }
                    if (!when()) return;
                    BossAi.DevBranches++;
                    fsm.Event(ev);
                },
            }, 0);
        }

        public FsmFloat Float(string name, PlayMakerFSM f = null) => (f ?? Control)?.FsmVariables.FindFsmFloat(name);
        public FsmInt Int(string name, PlayMakerFSM f = null) => (f ?? Control)?.FsmVariables.FindFsmInt(name);
        public FsmBool Bool(string name, PlayMakerFSM f = null) => (f ?? Control)?.FsmVariables.FindFsmBool(name);
        public FsmVector2 Vec2(string name, PlayMakerFSM f = null) => (f ?? Control)?.FsmVariables.FindFsmVector2(name);
        public FsmVector3 Vec3(string name, PlayMakerFSM f = null) => (f ?? Control)?.FsmVariables.FindFsmVector3(name);
        public FsmGameObject Go(string name, PlayMakerFSM f = null) => (f ?? Control)?.FsmVariables.FindFsmGameObject(name);

        /// <summary>A float of a script set (no-op if it has none by that name).</summary>
        public void SetFloat(string name, float value, PlayMakerFSM f = null)
        {
            var v = Float(name, f);
            if (v != null) v.Value = value;
            else Missing("变量 " + name);
        }

        /// <summary>A float of a script multiplied (a speed, a count), once.</summary>
        public void ScaleFloat(string name, float by, PlayMakerFSM f = null)
        {
            var v = Float(name, f);
            if (v != null) v.Value *= by;
            else Missing("变量 " + name);
        }

        /// <summary>The state its main script is in.</summary>
        public string StateNow => Control != null ? Control.ActiveStateName : null;

        // ------------------------------------------------------------------ the air (§1.4)

        float lastOnGround;

        /// <summary>He has kept off the room's own ground, six tiles up or more, for 1.5 s (his height over the ground under him).</summary>
        public bool AirCamping { get; private set; }
        /// <summary>And for 3 s: the boss's next move is its move against the air.</summary>
        public bool AirForced { get; private set; }

        /// <summary>The player's height over the room's own ground under him (units).</summary>
        public float TargetHeight { get; private set; }

        /// <summary>
        /// A kit's test before a move against the air: seven times in ten for a player long up there, a little over half
        /// for one camping; never a third in a row, so its own moves (and what they lead to) keep coming between.
        /// </summary>
        public bool WantAntiAir()
        {
            bool yes = AirForced ? Random.value < 0.7f : AirCamping && Random.value < 0.55f;
            if (yes && airStreak >= 2) yes = false;
            airStreak = yes ? airStreak + 1 : 0;
            if (yes) BossAi.DevAntiAir++;
            return yes;
        }

        int airStreak;

        /// <summary>
        /// Its fight is on: a second after it first moved from where it waited, or was hurt (not still up in a ceiling or
        /// under the floor before the fight). Only then does the watchdog look at it.
        /// </summary>
        public bool Engaged { get; private set; }
        Vector2 startedAt;
        float movedAt = -1f, arenaAgainAt;

        void Engage()
        {
            float now = Time.time;
            // The arena again a little after the fight starts (where it waited may have been above it).
            if (arenaAgainAt > 0f && now >= arenaAgainAt) { arenaAgainAt = 0f; ArenaFrom(Me); }
            if (Engaged) return;
            if (movedAt < 0f)
            {
                if ((Me - startedAt).sqrMagnitude < 1f && (Info == null || Hm.hp >= Info.MaxHp)) return;
                movedAt = now;
            }
            if (now - movedAt < 1f) return;
            Engaged = true;
            ArenaFrom(Me);
            arenaAgainAt = now + 3f;
        }

        void Air()
        {
            float now = Time.time;
            // A tall room's floor can be far under him (no ground within reach: he is well up all the same).
            var under = Hostiles.NativeRay(TargetFeet + Vector2.up * 0.1f, Vector2.down, 80f);
            TargetHeight = under.collider != null ? TargetFeet.y - under.point.y : 80f;
            if (TargetOnGround || TargetHeight < 3f) lastOnGround = now;
            // Terraria's wings keep a player up most of a fight (the user's, 10-25: "我在空中的时候……根本打不到我"): the
            // answer comes after a second and a half up there, every move after three.
            AirCamping = now - lastOnGround >= 1.5f;
            AirForced = now - lastOnGround >= 3f;
        }

        // ------------------------------------------------------------------ stuck (§1.5)

        float inGroundSince = -1f, outOfArenaSince = -1f, inRockSince = -1f, nextLook, lastUnstick = -100f, sunkSince = -1f;
        Vector2 lastGood;
        bool haveGood;

        /// <summary>Dev: bodies sunk into the room's floor and brought back up onto it.</summary>
        public static int DevSurfaced;

        /// <summary>The room's own ground at a point by its grid (a cell of the scene's terrain, untouched).</summary>
        protected static bool RoomRockAt(Vector2 p)
        {
            var c = World.CellGrid.WorldToCell(p);
            var r = World.TerraWorld.RegionAt(c.x, c.y);
            return r != null && r.Grid.Get(c.x - r.CellOffset.x, c.y - r.CellOffset.y) == World.CellKind.Original;
        }

        readonly List<Collider2D> attachedNow = new List<Collider2D>();

        /// <summary>
        /// What its body stands on now: its own collider, or, a boss whose own is off while it swaps colliders for its poses
        /// (the Pure Vessel's Idle, Slash Antic, Counter...), the largest of the solid ones on its body that are on.
        /// </summary>
        Collider2D SolidNow()
        {
            if (body != null && body.enabled && !body.isTrigger) return body;
            if (rb == null) return null;
            rb.GetAttachedColliders(attachedNow);
            Collider2D best = null;
            float area = 0f;
            foreach (var c in attachedNow)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                var size = c.bounds.size;
                if (size.x * size.y > area) { area = size.x * size.y; best = c; }
            }
            return best;
        }

        /// <summary>
        /// Sunk into the floor: its body (falling, not moved by its script) inside the room's own ground by the room's grid,
        /// its middle, its upper half and under its feet, for three quarters of a second (that ground is mostly hollow
        /// outlines, which no point inside answers: a body pressed through one falls on inside it, the Pure Vessel's slash
        /// against a wall). Brought back up onto the ground over it. A body up in a ceiling has open air under it (the Hive
        /// Knight hanging after his teleport slash); a burrower going under is its own (InGroundNow).
        /// </summary>
        void WatchSunk(float now)
        {
            if (InGroundNow) { sunkSince = -1f; return; }
            var solid = SolidNow();
            // Its script moving it (a teleport's moment, the Pure Vessel's every second or so) says nothing either way: the
            // time it has been down runs on (a teleport moves it along, not up).
            if (solid == null || rb == null || rb.bodyType != RigidbodyType2D.Dynamic || !rb.simulated) return;
            var b = solid.bounds;
            // Under its feet the room may end (sunk to the bottom of its map): past the map is rock too.
            var under = new Vector2(b.center.x, b.min.y - 0.3f);
            var uc = World.CellGrid.WorldToCell(under);
            bool sunk = RoomRockAt(b.center) && RoomRockAt(new Vector2(b.center.x, b.center.y + b.extents.y * 0.5f))
                && (RoomRockAt(under) || World.TerraWorld.RegionAt(uc.x, uc.y) == null);
            sunkSince = sunk ? (sunkSince < 0f ? now : sunkSince) : -1f;
            if (!sunk || now - sunkSince < 0.75f || now - lastUnstick < 5f) return;
            sunkSince = -1f;
            // Straight up to the first place its body fits clear of the room's rock, inside the arena.
            Vector2 from = ClampToArena(b.center, b.extents.x + 0.5f);
            for (float dy = 0f; dy <= 14f; dy += 0.25f)
            {
                var c = new Vector2(from.x, b.center.y + dy);
                if (c.y > Arena.yMax || !ClearOfRock(c, b.extents)) continue;
                lastUnstick = now;
                var recovery = GetComponent<BodyRecovery>() ?? gameObject.AddComponent<BodyRecovery>();
                recovery.Begin(this, solid, rb, Control, c);
                BossAi.DevUnstuck++;
                DevSurfaced++;
                Plugin.Log.LogInfo($"[首领AI] {Record} 沉进地面（{StateNow}，{solid.name} 在 {b.center:F1}），浮回地面 {c:F1}");
                return;
            }
        }

        /// <summary>A body this size centred here clear of the room's rock (its grid and its colliders), corners and middle.</summary>
        bool ClearOfRock(Vector2 c, Vector2 ext)
        {
            foreach (var o in new[] { Vector2.zero, new Vector2(-ext.x, -ext.y), new Vector2(ext.x, -ext.y), new Vector2(-ext.x, ext.y), new Vector2(ext.x, ext.y) })
            {
                var q = c + o * 0.9f;
                if (RoomRockAt(q) || Hostiles.NativeSolidAt(q, transform)) return false;
            }
            return true;
        }

        void Watch()
        {
            float now = Time.time;
            if (now < nextLook) return;
            nextLook = now + 0.25f;
            if (!Engaged || Hm.isDead) return;
            WatchSunk(now);
            if (body == null || !body.enabled) return;
            var b = body.bounds;
            // Buried: its middle and its upper half both in the room's own rock (a body lying on the floor, stunned or
            // fallen, has its upper half clear). Rock of its own is not the room's: the False Knight's shell, stunned,
            // puts up a block of ground to stand on round him.
            var upper = new Vector2(b.center.x, b.center.y + b.extents.y * 0.5f);
            bool inGround = !InGroundNow && Hostiles.NativeSolidAt(b.center, transform) && Hostiles.NativeSolidAt(upper, transform);
            inGroundSince = inGround ? (inGroundSince < 0f ? now : inGroundSince) : -1f;
            // Inside the room's rock by its grid: a boss room's ground is hollow outlines, which no point inside answers, and
            // a body through one stands on its inside (the Godhome False Knight under the roof, 10-25). A boss its script
            // holds up there (the Hive Knight, Nosk: no gravity, or not moved by physics) is at a move of its own; one
            // falling as ever, three seconds in rock, is stuck.
            bool falls = rb != null && rb.bodyType == RigidbodyType2D.Dynamic && rb.simulated && rb.gravityScale > 0f;
            bool inRock = falls && !InGroundNow && !inGround && RoomRockAt(b.center) && RoomRockAt(upper);
            inRockSince = inRock ? (inRockSince < 0f ? now : inRockSince) : -1f;
            // Out of the arena: well past its sides or floor, or far over it (its jumps and its entrance fall go above the view).
            // A burrower tunnels deep under its floor (the White Defender's dream arena): that far down is still in it.
            float below = InGroundNow ? 10f : 3f;
            var outer = new Rect(Arena.xMin - 3f, Arena.yMin - below, Arena.width + 6f, Arena.height + 10f + below);
            bool outOfArena = !Roams && !outer.Contains(b.center);
            outOfArenaSince = outOfArena ? (outOfArenaSince < 0f ? now : outOfArenaSince) : -1f;
            // Where it last stood well: in the arena, free, the room's ground under it (a flyer anywhere free in it).
            if (!inGround && Arena.Contains(b.center) && !Hostiles.NativeSolidAt(b.center, transform) && !RoomRockAt(b.center)
                && (Roams || Hostiles.NativeRay(new Vector2(b.center.x, b.min.y + 0.1f), Vector2.down, 1.5f).collider != null))
            {
                lastGood = b.center;
                haveGood = true;
            }
            // Out of an arena taken where it waited off-stage (the God Tamer's, past the room's end, 10-25): the arena where
            // the player fights it; in that, it is in the fight, and that is its arena now.
            if (outOfArenaSince >= 0f && now - outOfArenaSince >= 2f && HasTarget)
            {
                var there = ArenaAt(TargetCenter);
                var outerThere = new Rect(there.xMin - 3f, there.yMin - below, there.width + 6f, there.height + 10f + below);
                if (there != Arena && outerThere.Contains(b.center))
                {
                    Plugin.Log.LogInfo($"[首领AI] {Record} 场地改为玩家所在的 {there.xMin:F1}~{there.xMax:F1} × {there.yMin:F1}~{there.yMax:F1}（原 {Arena.xMin:F1}~{Arena.xMax:F1}）");
                    Arena = there;
                    outOfArenaSince = -1f;
                }
            }
            if (inGroundSince >= 0f && now - inGroundSince >= 0.75f || outOfArenaSince >= 0f && now - outOfArenaSince >= 2f
                || inRockSince >= 0f && now - inRockSince >= 3f)
            {
                inGroundSince = outOfArenaSince = inRockSince = -1f;
                // Rate-limit recovery without permanently abandoning a boss that gets trapped again.
                if (now - lastUnstick < 5f) return;
                lastUnstick = now;
                Unstick(inGround ? "卡在地形里" : inRock ? "卡在岩壁里" : "出了场地");
            }
        }

        /// <summary>
        /// Put where it can stand, clear of the room's rock by its colliders and its grid (a boss room's ground is hollow
        /// outlines: inside one is no free place): where it last stood well in the arena; with none known, the nearest clear
        /// place to it in the arena, down or up, brought down onto the ground under it; else the arena's middle on its ground.
        /// </summary>
        public void Unstick(string why)
        {
            if (body == null || !body.enabled) return;
            var b = body.bounds;
            Vector2 from = ClampToArena(b.center, b.extents.x + 0.5f);
            Vector2 to = Vector2.zero;
            bool found = false;
            if (haveGood && ClearOfRock(lastGood, b.extents)) { to = lastGood; found = true; }
            for (float d = 0f; d <= 12f && !found; d += 0.5f)
                for (int way = 0; way < 2 && !found; way++)
                    for (int side = 0; side < 5 && !found; side++)
                    {
                        float dx = side == 0 ? 0f : (side % 2 == 1 ? 1f : -1f) * (side + 1) / 2 * 1.5f;
                        var p = ClampToArena(from + new Vector2(dx, way == 0 ? -d : d), b.extents.x + 0.5f);
                        if (!ClearOfRock(p, b.extents)) continue;
                        to = p;
                        found = true;
                        if (Roams) continue;
                        var stand = OnGround(p) + Vector2.up * (b.extents.y + 0.1f);
                        if (stand.y < p.y && ClearOfRock(stand, b.extents)) to = stand;
                    }
            if (!found)
            {
                to = OnGround(Arena.center) + Vector2.up * (b.extents.y + 0.1f);
                if (!ClearOfRock(to, b.extents)) return;
            }
            var recovery = GetComponent<BodyRecovery>() ?? gameObject.AddComponent<BodyRecovery>();
            recovery.Begin(this, body, rb, Control, to);
            BossAi.DevUnstuck++;
            Plugin.Log.LogInfo($"[首领AI] {Record} {why}（{StateNow}，在 {(Vector2)b.center:F1}，场地 {Arena.xMin:F1}~{Arena.xMax:F1} × {Arena.yMin:F1}~{Arena.yMax:F1}），连续脱困至 {to:F1}");
        }

        // ------------------------------------------------------------------ each frame

        /// <summary>The boss runs here and is up (not dead, not another LAN game's).</summary>
        public bool Active => begun && isActiveAndEnabled && Hm != null && !Hm.isDead && World.EnemyAiMode.New && !Net.Enemies.IsPuppet(Hm);

        void Update()
        {
            if (!begun) return;
            if (Hm == null || Hm.isDead)
            {
                if (!down) { down = true; BossAi.Down(this, sceneRef, Scene); }
                return;
            }
            if (!Active) return;
            if (GetComponent<BodyRecovery>() is BodyRecovery recovery && recovery.Running) return;
            if (setupPending) SetupNow();
            if (Dormant) return;
            Track();
            if (!HasTarget) return;
            Engage();
            Air();
            Watch();
            try { Tick(); }
            catch (System.Exception e)
            {
                if (warned.Add(Record + "|tick")) Plugin.Log.LogWarning($"[首领AI] {Record} 出错：{e}");
            }
            BossAi.Log();
        }

        /// <summary>A kit's routine on the boss (stopped with it).</summary>
        public Coroutine Run(IEnumerator routine) => StartCoroutine(Guard(routine));

        protected IEnumerator WithCleanup(IEnumerator routine, System.Action cleanup)
        {
            try { yield return routine; }
            finally { cleanup?.Invoke(); }
        }

        internal IEnumerator Guard(IEnumerator e)
        {
            if (e == null) return GuardSteps(null);
            var guarded = GuardSteps(e);
            routines[e] = guarded;
            return guarded;
        }

        IEnumerator GuardSteps(IEnumerator e)
        {
            var stack = new Stack<IEnumerator>();
            if (e != null) stack.Push(e);
            try
            {
                while (stack.Count > 0)
                {
                    if (Active && GetComponent<BodyRecovery>() is BodyRecovery recovery && recovery.Running)
                    {
                        yield return null;
                        continue;
                    }
                    object cur;
                    try
                    {
                        if (!Active) yield break;
                        var top = stack.Peek();
                        if (!top.MoveNext())
                        {
                            stack.Pop();
                            (top as System.IDisposable)?.Dispose();
                            continue;
                        }
                        cur = top.Current;
                    }
                    catch (System.Exception ex)
                    {
                        if (warned.Add(Record + "|run")) Plugin.Log.LogWarning($"[首领AI] {Record} 招式出错：{ex}");
                        yield break;
                    }
                    // Drive nested routines here too, so an interrupted parent owns their cleanup.
                    // CustomYieldInstruction must remain with Unity (e.g. WaitUntil).
                    if (cur is IEnumerator nested && !(cur is CustomYieldInstruction)) stack.Push(nested);
                    else yield return cur;
                }
            }
            finally
            {
                if (e != null) routines.Remove(e);
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as System.IDisposable)?.Dispose(); }
                    catch (System.Exception ex) { Plugin.Log.LogWarning($"[首领AI] {Record} 招式收尾出错：{ex.Message}"); }
                }
            }
        }
    }

    /// <summary>A kit's step in a state of the boss's script: done as it starts or ends.</summary>
    internal sealed class KitAction : FsmStateAction
    {
        public BossKit Kit;
        public System.Action Enter, Exit;

        public override void OnEnter()
        {
            if (Enter != null && Kit != null && Kit.Active)
            {
                BossAi.DevHooks++;
                try { Enter(); }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[首领AI] {Kit.Record} 状态 {State?.Name} 出错：{e.Message}"); }
                // Sent elsewhere (Branch): this state is over.
                if (Fsm != null && Fsm.ActiveState != State) return;
            }
            Finish();
        }

        public override void OnExit()
        {
            if (Exit == null || Kit == null || !Kit.Active) return;
            try { Exit(); }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[首领AI] {Kit.Record} 状态 {State?.Name} 出错：{e.Message}"); }
        }
    }

    /// <summary>A kit's new state's whole work: a routine on the boss; the state ends when it does.</summary>
    internal sealed class RoutineAction : FsmStateAction
    {
        public BossKit Kit;
        public System.Func<IEnumerator> Body;
        Coroutine run;
        IEnumerator execution;
        bool done;

        public override void OnEnter()
        {
            done = false;
            if (Kit == null || !Kit.Active || Body == null) { End(); return; }
            BossAi.DevNewStates++;
            run = Kit.StartCoroutine(Wrap());
        }

        /// <summary>The way out of a kit's own state (its only transition).</summary>
        public static readonly FsmEvent Done = FsmEvent.GetFsmEvent("TK DONE");

        void End()
        {
            Finish();
            Fsm.Event(Done);
        }

        IEnumerator Wrap()
        {
            try { execution = Kit.Guard(Body()); }
            catch (System.Exception ex) { Plugin.Log.LogWarning($"[首领AI] {Kit.Record} 新招出错：{ex.Message}"); }
            try
            {
                while (execution != null)
                {
                    object cur;
                    try
                    {
                        if (!Kit.Active || !execution.MoveNext()) break;
                        cur = execution.Current;
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log.LogWarning($"[首领AI] {Kit.Record} 新招出错：{ex.Message}");
                        break;
                    }
                    yield return cur;
                }
            }
            finally { DisposeExecution(); }
            done = true;
            End();
        }

        public override void OnExit()
        {
            if (!done && run != null && Kit != null) Kit.StopCoroutine(run);
            run = null;
            // StopCoroutine does not guarantee disposal of a separately driven iterator.
            DisposeExecution();
        }

        void DisposeExecution()
        {
            var e = execution;
            execution = null;
            (e as System.IDisposable)?.Dispose();
        }
    }
}
