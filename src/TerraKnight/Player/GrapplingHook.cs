using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria's grappling hooks (AI_007, Player.QuickGrapple, GrappleMovement): E throws the
    /// first hook in the inventory from the player towards the cursor. Each kind has its own reach,
    /// speeds and how many may be out or holding at once (P:53484-53796, Pl:6291-6477, 22793-23040):
    /// a hook latches onto the first solid block it touches within reach (on the block's centre),
    /// otherwise comes back; past the most that may hold, the oldest lets go. Hooked, the player is
    /// pulled at the kind's speed toward the middle of the holding hooks (the Anti-Gravity Hook
    /// lets them float about its hook, the Static Hook holds them still, reeled in and out along
    /// the rope; the Squirrel Hook hangs them a little off it; the Queen Slime's hook takes them
    /// there at once). Jump lets go of them all with a jump (down held: only lets go) and gives back the
    /// double jumps; hanging on refreshes rocket boots, wings and the flying carpet. The hooks go
    /// when the player dies or the game moves him. Chains are drawn link by link as Main.DrawProj
    /// draws each kind's.
    /// </summary>
    [HarmonyPatch]
    internal sealed class GrapplingHook : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        const float KillRange = 2500f, ReturnedDist = 24f, LatchBox = 10f;
        const float L = 0.874f;

        enum State { Flying, Retracting, Latched }

        /// <summary>A hook kind (its projectile): reach, return and pull speed, how many may hold, how many fly, how many in all, and its chain.</summary>
        sealed class Spec
        {
            public int Proj;
            public float Range = 300f, Retract = 11f, Pull = 11f;
            public int LatchMax = 3;
            public int Updates = 1;
            public string Chain = "Chain";
            public float Step = 12f, End = 25f, Back;
            public int ChainFrames = 1;
            public bool CycleLinks, Unlit;
            public string Glow;
            public Color GlowColor;
            public float Alpha = 1f;
            public int HeadFrames = 1;
            public Color Light;
        }

        static readonly Dictionary<int, Spec> specs = new Dictionary<int, Spec>();

        /// <summary>Hook items: the projectile thrown and its shootSpeed (Item.SetDefaults).</summary>
        public static readonly Dictionary<ItemType, (int Proj, float Speed)> Hooks = new Dictionary<ItemType, (int, float)>
        {
            [ItemType.GrapplingHook] = (13, 11.5f), [ItemType.IvyWhip] = (32, 13f), [ItemType.DualHook] = (73, 14.5f), [ItemType.WebSlinger] = (165, 10f),
            [ItemType.AmethystHook] = (230, 10f), [ItemType.TopazHook] = (231, 10.5f), [ItemType.SapphireHook] = (232, 11f), [ItemType.EmeraldHook] = (233, 11.5f),
            [ItemType.RubyHook] = (234, 12f), [ItemType.DiamondHook] = (235, 12.5f), [ItemType.SkeletronHand] = (256, 8f), [ItemType.BatHook] = (315, 13.5f),
            [ItemType.SpookyHook] = (322, 15.5f), [ItemType.CandyCaneHook] = (331, 11.5f), [ItemType.ChristmasHook] = (332, 15.5f), [ItemType.FishHook] = (372, 13f),
            [ItemType.SlimeHook] = (396, 13f), [ItemType.AntiGravityHook] = (446, 14f), [ItemType.TendonHook] = (486, 15f), [ItemType.ThornHook] = (487, 16f),
            [ItemType.IlluminantHook] = (488, 15f), [ItemType.WormHook] = (489, 15f), [ItemType.LunarHook] = (646, 18f), [ItemType.StaticHook] = (652, 16f),
            [ItemType.AmberHook] = (753, 12.5f), [ItemType.SquirrelHook] = (865, 11.5f), [ItemType.QueenSlimeHook] = (935, 4f),
        };

        static GrapplingHook()
        {
            void S(Spec s) => specs[s.Proj] = s;
            S(new Spec { Proj = 13 });
            S(new Spec { Proj = 32, Range = 400f, Retract = 15f, Chain = "Chain5", Back = 8f, Step = 28f, End = 28f });
            S(new Spec { Proj = 73, Range = 440f, Retract = 22f, Pull = 12.5f, LatchMax = 1, Chain = "Chain8" });
            S(new Spec { Proj = 74, Range = 440f, Retract = 22f, Pull = 12.5f, LatchMax = 1, Chain = "Chain9" });
            S(new Spec { Proj = 165, Range = 375f, LatchMax = 8, Chain = "Chain15", Step = 24f });
            for (int k = 0; k < 6; k++) S(new Spec { Proj = 230 + k, Range = 300f + 30f * k, Retract = 11f + 0.75f * k, Chain = "GemChain_" + (k + 1), Step = 0f });
            S(new Spec { Proj = 256, Range = 425f, LatchMax = 2, Chain = "Chain20", Step = 26f, End = 26f });
            S(new Spec { Proj = 315, Range = 500f, Retract = 20f, Pull = 14f, Chain = "Chain28", Step = 40f, End = 50f });
            S(new Spec { Proj = 322, Range = 550f, Retract = 22f, Chain = "Chain29", Step = 22f, End = 22f });
            S(new Spec { Proj = 331, Range = 400f, Chain = "Chain30", Step = 24f, End = 30f });
            S(new Spec { Proj = 332, Range = 550f, Retract = 17f, Chain = "Chain31", ChainFrames = 4, CycleLinks = true, Step = 16f, End = 30f, Glow = "Chain32", GlowColor = new Color(200f / 255f, 200f / 255f, 200f / 255f, 0f) });
            S(new Spec { Proj = 372, Range = 400f, LatchMax = 2, Chain = "Chain33", Step = 0f });
            S(new Spec { Proj = 396, Range = 300f, Chain = "Chain35", Step = 0f, Alpha = 155f / 255f });
            S(new Spec { Proj = 446, Range = 500f, Retract = 20f, Chain = "Extra_3", ChainFrames = 4, Step = 0f, Back = 20f, Light = new Color(0f, 0.4f, 0.3f) });
            S(new Spec { Proj = 486, Range = 480f, Retract = 18f, Chain = "Chains_0", Step = 0f });
            S(new Spec { Proj = 487, Range = 480f, Retract = 18f, Pull = 12f, Chain = "Chains_1", Step = 0f, Back = 8f });
            S(new Spec { Proj = 488, Range = 480f, Retract = 18f, Chain = "Chains_2", Step = 0f, Unlit = true, Light = new Color(0.2f, 0f, 0.175f) });
            S(new Spec { Proj = 489, Range = 480f, Retract = 18f, Chain = "Chains_3", Step = 0f, Back = 10f });
            Color[] lunar = { new Color(0.7f, 0.5f, 0.1f), new Color(0f, 0.6f, 0.7f), new Color(0.6f, 0.2f, 0.6f), new Color(0.6f, 0.6f, 0.9f) };
            for (int k = 0; k < 4; k++)
                S(new Spec { Proj = 646 + k, Range = 550f, Retract = 24f, Pull = 16f, LatchMax = 4, Chain = "Chains_" + (8 + k), Step = 0f, Unlit = true,
                    Glow = "Chains_" + (12 + k), GlowColor = new Color(1f, 1f, 1f, 0.5f), Light = lunar[k] });
            S(new Spec { Proj = 652, Range = 600f, Retract = 24f, LatchMax = 1, Chain = "Chains_16", Step = 0f, HeadFrames = 6 });
            S(new Spec { Proj = 753, Range = 420f, Retract = 15f, Chain = "Extra_95", Step = 0f });
            S(new Spec { Proj = 865, Range = 300f, Chain = "Extra_154", Step = 0f });
            S(new Spec { Proj = 935, Range = 500f, Chain = "Extra_208", Step = 0f, End = 8f, Updates = 4 });
        }

        sealed class Hook
        {
            public Spec S;
            public State State;
            public Vector2 Pos, Vel;        // world units; velocity in px per update
            public Vector2Int Cell;
            public Collider2D Col;
            public bool IsCell;
            public int Age;
            public int Frame, FrameCounter;
            public float LocalAi0;
        }

        public static GrapplingHook Instance { get; private set; }
        public static bool IsLatched => Instance != null && Instance.hooks.Exists(h => h.State == State.Latched);

        readonly List<Hook> hooks = new List<Hook>();
        float tickAccumulator;
        bool slow;

        static readonly System.Action<HeroController> heroJump =
            AccessTools.MethodDelegate<System.Action<HeroController>>(AccessTools.Method(typeof(HeroController), "HeroJump"));
        static readonly AccessTools.FieldRef<HeroController, bool> doubleJumped = AccessTools.FieldRefAccess<HeroController, bool>("doubleJumped");

        public static GrapplingHook Create(Transform parent)
        {
            var go = new GameObject("Terra Grappling Hook");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<GrapplingHook>();
            return Instance;
        }

        static bool Playing(HeroController hero) =>
            hero != null && !hero.cState.dead && TerrainSystem.Current != null && GameHooks.PlayerHasControl();

        /// <summary>Player.QuickGrapple_GetItemToUse: the first hook in the inventory (there is no hook slot here).</summary>
        public static ItemType HookItem(Inventory inv)
        {
            if (inv == null) return ItemType.None;
            // Player.QuickGrapple: the hook slot's first, else the first hook in the inventory.
            var worn = inv.Misc[Inventory.HookSlot];
            if (!worn.IsEmpty && Hooks.ContainsKey(worn.Type)) return worn.Type;
            foreach (var s in inv.Slots)
                if (!s.IsEmpty && Hooks.ContainsKey(s.Type)) return s.Type;
            return ItemType.None;
        }

        /// <summary>Dev: the hook kind thrown when the inventory holds none (the plain Grappling Hook).</summary>
        public static ItemType DevHook = ItemType.GrapplingHook;

        /// <summary>Throws the plain Grappling Hook (or DevHook) towards a point.</summary>
        public void Throw(HeroController hero, Vector2 target) => Throw(hero, DevHook, target);

        /// <summary>
        /// Player.QuickGrapple (Pl:6291-6477): how many may fly at once (less those holding) and
        /// in all for this kind; past the total the oldest goes; the Dual Hook's second is the other
        /// colour, the Lunar Hook's next takes the next pillar's colour.
        /// </summary>
        public void Throw(HeroController hero, ItemType item, Vector2 target)
        {
            if (!Hooks.TryGetValue(item, out var h)) return;
            int proj = h.Proj;
            int latched = 0, flying = 0;
            foreach (var k in hooks)
                if (k.State == State.Latched) latched++;
                else flying++;
            int fly = 3, total = 999;
            if (proj == 73) fly = 2;
            else if (proj == 372) fly = 3;
            else if (proj == 165) fly = 8;
            else if (proj == 652) fly = 2;
            else if (proj == 646) fly = 4;
            else if (proj == 13 || proj == 315 || (proj >= 230 && proj <= 235) || proj == 331 || proj == 753 || proj == 865 || proj == 935) fly = total = 1;
            else if (proj == 256) fly = total = 2;
            fly = Mathf.Max(1, fly - latched);
            if (flying >= fly) return;
            if (latched + flying >= total) KillOldest(hooks);
            if (proj == 73 && hooks.Exists(k => k.S.Proj == 73)) proj = 74;
            if (proj == 646)
            {
                Hook newest = null;
                foreach (var k in hooks) if (k.S.Proj >= 646 && k.S.Proj <= 649 && (newest == null || k.Age < newest.Age)) newest = k;
                proj = newest == null || newest.S.Proj == 649 ? 646 : newest.S.Proj + 1;
            }
            Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
            var dir = target - center;
            dir = dir.sqrMagnitude < 1e-6f ? new Vector2(hero.cState.facingRight ? 1f : -1f, 0f) : dir.normalized;
            hooks.Add(new Hook { S = specs[proj], State = State.Flying, Pos = center, Vel = dir * h.Speed });
            DevThrown++;
            TerraMod.Instance?.PlaySound("Item_1", 0.6f);
        }

        static void KillOldest(List<Hook> list)
        {
            Hook oldest = null;
            foreach (var k in list) if (oldest == null || k.Age > oldest.Age) oldest = k;
            if (oldest != null) list.Remove(oldest);
        }

        void Update()
        {
            var hero = HeroController.SilentInstance;
            if (hooks.Count > 0 && Gone(hero))
            {
                Note("主角死了、被传送或失去控制");
                hooks.Clear();
            }
            if (hooks.Count > 0)
            {
                tickAccumulator += Time.deltaTime * 60f;
                int ticks = Mathf.Min(Mathf.FloorToInt(tickAccumulator), 4);
                tickAccumulator -= Mathf.FloorToInt(tickAccumulator);
                for (int t = 0; t < ticks && hooks.Count > 0; t++)
                    for (int i = hooks.Count - 1; i >= 0; i--)
                    {
                        var k = hooks[i];
                        k.Age++;
                        for (int u = 0; u < k.S.Updates && hooks.Contains(k); u++) Tick(hero, k);
                    }
            }
            else tickAccumulator = 0f;
            Draw(hero);
        }

        /// <summary>
        /// Terraria removes a player's hooks when he dies or is teleported. Here: death, the
        /// game putting him back after spikes, any scene change it makes, and cutscenes or benches
        /// taking control away.
        /// </summary>
        static bool Gone(HeroController hero)
        {
            if (hero == null || TerrainSystem.Current == null) return true;
            var cs = hero.cState;
            if (cs.dead || cs.hazardDeath || cs.hazardRespawning || cs.transitioning) return true;
            var gm = GameManager.UnsafeInstance;
            return gm == null || gm.IsInSceneTransition || hero.controlReqlinquished;
        }

        /// <summary>AI_007_GrapplingHooks, one update of one hook.</summary>
        void Tick(HeroController hero, Hook k)
        {
            Vector2 player = hero.GetComponent<BoxCollider2D>().bounds.center;
            var d = player - k.Pos;
            float dist = d.magnitude / Px;
            if (dist > KillRange)
            {
                Note("离太远");
                hooks.Remove(k);
                return;
            }
            if (k.S.HeadFrames > 1 && ++k.FrameCounter >= 7)
            {
                k.FrameCounter = 0;
                if (++k.Frame >= k.S.HeadFrames) k.Frame = 0;
            }
            if (k.S.Proj == 446 && ++k.LocalAi0 >= 28f) k.LocalAi0 = 0f;
            switch (k.State)
            {
                case State.Flying:
                    if (dist > k.S.Range) k.State = State.Retracting;
                    // The latch test still runs on the update it turns back.
                    if (TryLatch(hero, k)) return;
                    k.Pos += k.Vel * Px;
                    break;
                case State.Retracting:
                    if (dist < ReturnedDist)
                    {
                        hooks.Remove(k);
                        return;
                    }
                    k.Vel = d.normalized * k.S.Retract;
                    k.Pos += k.Vel * Px;
                    break;
                case State.Latched:
                    // The block it holds was dug away: it lets go and comes back.
                    bool held = k.IsCell ? Holds(k.Cell.x, k.Cell.y) : k.Col != null && k.Col.enabled && k.Col.gameObject.activeInHierarchy;
                    if (!held)
                    {
                        Note($"钩住的格 {k.Cell} 没了");
                        k.State = State.Retracting;
                    }
                    break;
            }
        }

        /// <summary>A cell a hook takes hold of: a solid block, or a platform (Main.tileSolidTop, as Projectile.AI_007_GrapplingHooks lets it).</summary>
        static bool Holds(int x, int y) => TerraWorld.IsSolid(x, y) || TerraWorld.IsPlatform(x, y);

        /// <summary>A 10 px box round the hook against solid blocks and platforms, then the game's own solid terrain.</summary>
        bool TryLatch(HeroController hero, Hook k)
        {
            float half = LatchBox / 2f * Px;
            var lo = CellGrid.WorldToCell(k.Pos - new Vector2(half, half));
            var hi = CellGrid.WorldToCell(k.Pos + new Vector2(half, half));
            for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                {
                    if (!Holds(x, y)) continue;
                    k.IsCell = true;
                    k.Cell = new Vector2Int(x, y);
                    Latch(hero, k, CellGrid.CellCenter(x, y), TerraWorld.Get(x, y), new Vector2Int(x, y));
                    return true;
                }
            // Solid scenery the grid doesn't hold (moving platforms, doors): hold where it touched.
            foreach (var col in Physics2D.OverlapBoxAll(k.Pos, new Vector2(half * 2f, half * 2f), 0f, 1 << ChunkColliders.TerrainLayer))
            {
                if (col.isTrigger || col.GetComponentInParent<TerraOwned>() != null) continue;
                k.IsCell = false;
                k.Col = col;
                Latch(hero, k, k.Pos, CellKind.Original, CellGrid.WorldToCell(k.Pos));
                return true;
            }
            return false;
        }

        void Latch(HeroController hero, Hook k, Vector2 at, CellKind kind, Vector2Int cell)
        {
            k.Pos = at;
            k.Vel = Vector2.zero;
            k.State = State.Latched;
            DevLatches++;
            // Past the most that may hold (counting this one), the oldest of them lets go.
            int holding = 0;
            foreach (var o in hooks) if (o.State == State.Latched) holding++;
            if (holding > k.S.LatchMax)
            {
                Hook oldest = null;
                foreach (var o in hooks) if (o.State == State.Latched && (oldest == null || o.Age > oldest.Age)) oldest = o;
                if (oldest != null) hooks.Remove(oldest);
            }
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            // WorldGen.KillTile(fail, effectOnly): three dust of the block's kind.
            var p = TerraParticles.Instance;
            if (p != null)
            {
                int type = kind == CellKind.Dirt ? 0 : kind == CellKind.Wood ? 7 : 1;
                var corner = TerraParticles.ToPx(new Vector2(cell.x * CellGrid.CellSize, (cell.y + 1) * CellGrid.CellSize));
                for (int i = 0; i < 3; i++) p.NewDust(corner, 16, 16, type);
            }
            doubleJumped(hero) = false;
            TerraMovement.RefreshFlight();
            if (k.S.Proj == 935) QueenSlimeTeleport(hero, k.Pos);
        }

        /// <summary>
        /// Player.DoQueenSlimeHookTeleport (Pl:22922-22936): up to 150 steps of 12 px toward the
        /// hook, stopped by blocks, and there at once (a puff of gel at both ends).
        /// </summary>
        static void QueenSlimeTeleport(HeroController hero, Vector2 target)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var from = (Vector2)col.bounds.center;
            var to = target - from;
            float dist = Mathf.Min(to.magnitude, 150f * 12f * Px);
            if (dist < 1e-3f) return;
            var hit = Physics2D.BoxCast(from, col.bounds.size * 0.98f, 0f, to.normalized, dist, 1 << ChunkColliders.TerrainLayer);
            float go = hit.collider != null ? Mathf.Max(0f, hit.distance - 0.02f) : dist;
            Puff(from);
            hero.transform.position += (Vector3)(to.normalized * go);
            Puff(from + to.normalized * go);
            TerraMod.Instance?.ResetFall();
            DevTeleports++;
        }

        static void Puff(Vector2 at)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(at);
            for (int i = 0; i < 20; i++)
            {
                int d = p.NewDust(px - new Vector2(10f, 21f), 20, 42, 4, 0f, 0f, 100, 1.5f);
                if (d < 0) continue;
                p[d].Color = new Color(1f, 0.6f, 0.9f, 1f);
                p[d].NoGravity = true;
            }
        }

        /// <summary>The held direction keys as Terraria's control vector (y down), turned with gravity.</summary>
        static Vector2 InputVector()
        {
            var a = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
            if (a == null) return Vector2.zero;
            float x = (a.right.IsPressed ? 1f : 0f) - (a.left.IsPressed ? 1f : 0f);
            float y = (a.down.IsPressed ? 1f : 0f) - (a.up.IsPressed ? 1f : 0f);
            if (GravityGlobe.Flipped) y = -y;
            return new Vector2(x, y);
        }

        /// <summary>
        /// GetGrapplingForces (Pl:22938-23030), in Terraria px: the middle of the holding hooks
        /// (the Anti-Gravity Hook's point 200 px off it toward the held keys, the Static Hook's the
        /// player's own place moved along the rope by them at 6, the Squirrel Hook's 28 px short of
        /// it), approached at the first one's pull speed.
        /// </summary>
        Vector2 Force(Vector2 playerPx)
        {
            var sum = Vector2.zero;
            int n = 0;
            Hook first = null;
            var input = InputVector();
            foreach (var k in hooks)
            {
                if (k.State != State.Latched) continue;
                if (first == null || k.Age > first.Age) first = k;
                var hook = TerraParticles.ToPx(k.Pos);
                var c = hook;
                if (k.S.Proj == 446)
                {
                    var v = input.sqrMagnitude > 0f ? input.normalized * 100f : Vector2.zero;
                    var away = playerPx - hook + v;
                    c += (away.sqrMagnitude > 1e-6f ? away.normalized : new Vector2(0f, -1f)) * 200f;
                }
                else if (k.S.Proj == 652)
                {
                    var rope = hook - playerPx;
                    var along = rope.sqrMagnitude > 1e-6f ? rope.normalized : Vector2.zero;
                    var move = input.sqrMagnitude > 0f ? along * Vector2.Dot(along, input.normalized) : Vector2.zero;
                    float speed = Vector2.Dot(move, rope) < 0f && rope.magnitude >= 600f ? 0f : 6f;
                    c = playerPx + move * speed;
                }
                else if (k.S.Proj == 865)
                {
                    var toPlayer = playerPx - hook;
                    var axis = Mathf.Abs(toPlayer.x) >= Mathf.Abs(toPlayer.y) ? new Vector2(Mathf.Sign(toPlayer.x), 0f) : new Vector2(0f, Mathf.Sign(toPlayer.y));
                    c += axis * 28f;
                }
                sum += c;
                n++;
            }
            if (n == 0) return Vector2.zero;
            var vel = sum / n - playerPx;
            float max = first.S.Pull;
            if (vel.magnitude > max) vel = vel.normalized * max;
            return vel;
        }

        /// <summary>
        /// GrappleMovement, after the hero's own FixedUpdate: the pull's velocity (px per tick as a
        /// speed). Terraria's grapple ignores gravity; the coming physics step's pull of gravity is
        /// cancelled in the velocity (the hero's gravity scale is left alone).
        /// </summary>
        void Pull(HeroController hero, Rigidbody2D rb)
        {
            slow = rb.linearVelocity.magnitude < 2f * TerraMovement.PxT;
            Vector2 player = hero.GetComponent<BoxCollider2D>().bounds.center;
            // The force is px a tick: as a speed, 60 ticks a second; never past the spot within one physics step.
            var pp = TerraParticles.ToPx(player);
            var f = Force(pp);
            var vel = new Vector2(f.x, -f.y) * Px * 60f;
            float dt = Mathf.Max(Time.fixedDeltaTime, 1e-4f);
            float room = f.magnitude * Px;
            if (vel.magnitude * dt > room && room > 0f) vel = vel.normalized * room / dt;
            pullingDown = vel.y < 0f;
            standingPullDown = pullingDown && Mathf.Abs(rb.linearVelocity.y) < 0.01f;
            rb.linearVelocity = vel - Physics2D.gravity * rb.gravityScale * Time.fixedDeltaTime;
            if (TerraMod.Instance != null && !TerraMod.Instance.IsUsingItem && Mathf.Abs(vel.x) > 0.01f)
            {
                if (vel.x > 0f) hero.FaceRight();
                else hero.FaceLeft();
            }
            TerraMod.Instance?.ResetFall();
            doubleJumped(hero) = false;
            TerraMovement.RefreshFlight();
        }

        bool pullingDown, standingPullDown;

        void LetGoWithJump(HeroController hero)
        {
            var input = GameHooks.Input.inputActions;
            // Off a hook always with a jump, moving or not (Terraria's GrappleMovement jumps only from a hook it
            // hangs still on, and a jump pressed while still pulled only let go and fell); down held only lets go.
            bool jump = !input.down.IsPressed && !(standingPullDown && !input.up.IsPressed);
            Note("跳跃松开");
            hooks.Clear();
            doubleJumped(hero) = false;
            if (jump) heroJump(hero);
        }

        /// <summary>Why the hooks let go, for the log.</summary>
        void Note(string why)
        {
            var hero = HeroController.SilentInstance;
            string detail = hero == null ? "" : $"（dead={hero.cState.dead} hazard={hero.cState.hazardDeath}/{hero.cState.hazardRespawning} transitioning={hero.cState.transitioning} " +
                                                $"relinquished={hero.controlReqlinquished} 场景切换={GameManager.UnsafeInstance?.IsInSceneTransition}）";
            Plugin.Log.LogInfo($"[抓钩] {DevState} -> 松开：{why}{detail}");
        }

        // --- Drawing (Main.DrawProj for aiStyle 7): each hook's chain from it to the player, then the hook. ---

        void Draw(HeroController hero)
        {
            var fx = FxBatch.Instance;
            if (fx == null || hero == null || hooks.Count == 0) return;
            var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            foreach (var k in hooks)
            {
                var s = k.S;
                var hook = TerraParticles.ToPx(k.Pos);
                var toPlayer = player - hook;
                float rot = Mathf.Atan2(toPlayer.y, toPlayer.x) - 1.57f;
                if (s.Proj == 865 && k.State == State.Latched) rot = Mathf.Round(rot / (Mathf.PI / 2f)) * (Mathf.PI / 2f);
                DrawChain(fx, k, hook, player);
                var whole = FxBatch.Whole("Projectile_" + s.Proj);
                if (whole != null)
                {
                    int w = (int)whole.rect.width, fh = (int)whole.rect.height / s.HeadFrames;
                    float headRot = s.Proj == 256 ? Mathf.Atan2(toPlayer.y, toPlayer.x) + 3.925f : rot;
                    float lit = s.Unlit ? 1f : L;
                    fx.Draw(FxBatch.Frame("Projectile_" + s.Proj, 0, fh * k.Frame, w, fh, w / 2f, fh / 2f), hook, new Color(lit, lit, lit, 1f) * s.Alpha, headRot, 1f);
                }
                if (s.Light.r + s.Light.g + s.Light.b > 0f)
                {
                    fx.Light(hook, s.Light, 3f);
                    fx.Light(player, s.Light, 3f);
                }
            }
        }

        /// <summary>The links: from the hook (set back along it for some kinds) toward the player, a step at a time while farther than the end distance.</summary>
        static void DrawChain(FxBatch fx, Hook k, Vector2 hook, Vector2 player)
        {
            var s = k.S;
            var whole = FxBatch.Whole(s.Chain);
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / s.ChainFrames;
            float step = s.Step > 0f ? s.Step : fh;
            float end = s.Step > 0f ? s.End : (s.Proj == 935 ? s.End : fh + 1f);
            var at = hook;
            var dir = player - at;
            if (dir.sqrMagnitude < 1e-4f) return;
            if (s.Back > 0f) at -= dir.normalized * s.Back;
            float rot = Mathf.Atan2(dir.y, dir.x) - 1.57f;
            int frame = s.Proj == 446 ? (int)(k.LocalAi0 / 7f) % 4 : 0;
            float lit = s.Unlit ? 1f : L;
            var col = new Color(lit, lit, lit, 1f) * s.Alpha;
            for (int link = 0; link < 400; link++)
            {
                var rest = player - at;
                if (rest.magnitude < end) break;
                at += rest.normalized * step;
                int f = s.CycleLinks ? link % s.ChainFrames : frame;
                var spr = FxBatch.Frame(s.Chain, 0, fh * f, w, fh, w / 2f, fh / 2f);
                fx.Draw(spr, at, col, rot, 1f);
                if (s.Glow != null)
                {
                    var g = FxBatch.Whole(s.Glow);
                    if (g != null)
                    {
                        int gw = (int)g.rect.width, gh = (int)g.rect.height / s.ChainFrames;
                        fx.Draw(FxBatch.Frame(s.Glow, 0, gh * f, gw, gh, gw / 2f, gh / 2f), at, s.GlowColor, rot, 1f);
                    }
                }
                if (s.Proj == 488) fx.Light(at, s.Light, 1.5f);
            }
        }

        // --- The hero while hooked: pulled, and no jumping off its own way. ---

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (IsLatched && Playing(__instance)) Instance.Pull(__instance, ___rb2d);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "CanJump")]
        [HarmonyPatch(typeof(HeroController), "CanDoubleJump")]
        [HarmonyPatch(typeof(HeroController), "CanWallJump")]
        [HarmonyPatch(typeof(HeroController), "CanWallSlide")]
        static bool NotWhileHooked(ref bool __result)
        {
            if (!IsLatched && consumedJumpFrame != Time.frameCount) return true;
            __result = false;
            return false;
        }

        static int consumedJumpFrame = -1;
        static readonly AccessTools.FieldRef<HeroController, bool> jumpQueuing = AccessTools.FieldRefAccess<HeroController, bool>("jumpQueuing");
        static readonly AccessTools.FieldRef<HeroController, bool> doubleJumpQueuing = AccessTools.FieldRefAccess<HeroController, bool>("doubleJumpQueuing");

        /// <summary>
        /// Jump while hooked lets go (GrappleMovement), before the hero reads the press itself:
        /// otherwise it would keep the press queued and spend it the next frame as a double jump,
        /// or as a wall jump against the wall it was pulled to. Letting go is an ordinary jump.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "LookForQueueInput")]
        static void BeforeQueueInput(HeroController __instance)
        {
            if (!IsLatched || !Playing(__instance) || GameHooks.Input == null || !GameHooks.Input.inputActions.jump.WasPressed) return;
            consumedJumpFrame = Time.frameCount;
            Instance.LetGoWithJump(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "LookForQueueInput")]
        static void AfterQueueInput(HeroController __instance)
        {
            if (consumedJumpFrame != Time.frameCount) return;
            jumpQueuing(__instance) = false;
            doubleJumpQueuing(__instance) = false;
        }

        /// <summary>Dev: the hooks' state (Latched if any holds), where the oldest holding (or first) one is, how many, and letting go.</summary>
        public string DevState => hooks.Count == 0 ? "None" : hooks.Exists(h => h.State == State.Latched) ? "Latched" : hooks[0].State.ToString();
        public Vector2 DevHookPosition
        {
            get
            {
                Hook best = null;
                foreach (var k in hooks) if (k.State == State.Latched && (best == null || k.Age > best.Age)) best = k;
                return (best ?? (hooks.Count > 0 ? hooks[0] : null))?.Pos ?? Vector2.zero;
            }
        }
        public int DevOut => hooks.Count;
        public int DevHolding => hooks.FindAll(h => h.State == State.Latched).Count;
        public static int DevThrown, DevLatches, DevTeleports;
        public void DevRelease(HeroController hero) => hooks.Clear();
        /// <summary>Player.RemoveAllGrapplingHooks (a pat, a teleport).</summary>
        public void ReleaseAll() => hooks.Clear();
        /// <summary>Dev: what pressing jump while hooked does.</summary>
        public void DevJumpRelease(HeroController hero)
        {
            consumedJumpFrame = Time.frameCount;
            LetGoWithJump(hero);
        }

        /// <summary>E throws the first hook in the inventory (Terraria's Grapple key).</summary>
        public void ReadKey(HeroController hero, ItemType hook, Vector2 mouse)
        {
            var kb = Keyboard.current;
            if (kb == null || hook == ItemType.None || !Playing(hero) || Net.NetChat.BlocksKeys) return;
            if (TerraKeys.Pressed(TerraKeys.Action.Grapple)) Throw(hero, hook, mouse);
        }
    }
}
