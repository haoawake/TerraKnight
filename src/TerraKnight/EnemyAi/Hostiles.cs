using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>Whoever aims a shot or an area (an enemy's Brain, a boss's Bosses.BossKit): whom it turns after.</summary>
    internal interface IAim
    {
        bool HasTarget { get; }
        Vector2 TargetCenter { get; }
    }

    /// <summary>
    /// The new AI's own attacks: shots and areas that hurt the player as the enemies' own do (a DamageHero on a trigger,
    /// so the hit goes through the mod's Terraria damage, the Hollow difficulty's masks, dodges and the shadow dash), placed
    /// in the enemy's scene (gone with it, and sent to the other players by Net.EnemyAttacks like any enemy attack). Drawn
    /// with Terraria's own projectile sheets and dusts, tinted for Hallownest.
    /// </summary>
    internal static class Hostiles
    {
        const float Px = TerraParticles.Px;
        internal const int TerrainMask = 1 << 8;

        /// <summary>A look: Terraria's sheet (its frame count), the tint, the dust it leaves, whether it turns along its way.</summary>
        internal sealed class Style
        {
            public string Sheet;
            public int Frames = 1;
            public Color Tint = Color.white;
            public int Dust;
            public Color DustColor = Color.white;
            public bool Turns, Spins;
        }

        static readonly Dictionary<Look, Style> styles = new Dictionary<Look, Style>
        {
            [Look.Infection] = new Style { Sheet = "Projectile_258", Tint = new Color(1f, 0.62f, 0.25f), Dust = 6 },
            [Look.Acid] = new Style { Sheet = "Projectile_410", Tint = new Color(0.55f, 0.95f, 0.45f), Dust = 273 },
            [Look.Spike] = new Style { Sheet = "Projectile_176", Tint = new Color(0.9f, 0.9f, 0.85f), Dust = 0, Turns = true },
            [Look.Thorn] = new Style { Sheet = "Projectile_277", Tint = Color.white, Dust = 157, Spins = true },
            [Look.Crystal] = new Style { Sheet = "Projectile_90", Tint = new Color(1f, 0.7f, 0.92f), Dust = 70, Turns = true },
            [Look.Web] = new Style { Sheet = "Projectile_472", Tint = Color.white, Dust = 30, Spins = true },
            [Look.Poison] = new Style { Sheet = "Projectile_265", Tint = new Color(0.75f, 1f, 0.6f), Dust = 157, Turns = true },
            [Look.Soul] = new Style { Sheet = "Projectile_258", Tint = new Color(0.85f, 0.92f, 1f), Dust = 15 },
            [Look.Fire] = new Style { Sheet = "Projectile_376", Tint = new Color(1f, 0.45f, 0.4f), Dust = 6 },
            [Look.Stinger] = new Style { Sheet = "Projectile_55", Tint = Color.white, Dust = 152, Turns = true },
            [Look.Honey] = new Style { Sheet = "Projectile_410", Tint = new Color(1f, 0.8f, 0.25f), Dust = 152 },
            [Look.Rock] = new Style { Sheet = "Projectile_31", Tint = new Color(0.62f, 0.6f, 0.58f), Dust = 0, Spins = true },
            [Look.Electric] = new Style { Sheet = "Projectile_443", Frames = 4, Tint = new Color(0.8f, 0.9f, 1f), Dust = 226 },
            [Look.Coin] = new Style { Sheet = "Projectile_160", Tint = Color.white, Dust = 228, Spins = true },
            [Look.Spore] = new Style { Sheet = "Projectile_228", Frames = 5, Tint = new Color(0.85f, 0.95f, 0.6f), Dust = 16, DustColor = new Color(0.8f, 0.95f, 0.55f) },
            [Look.Void] = new Style { Sheet = "Projectile_258", Tint = new Color(0.22f, 0.15f, 0.3f), Dust = 27 },
            [Look.Blade] = new Style { Sheet = "Projectile_44", Tint = new Color(0.92f, 0.95f, 0.88f), Dust = 15, Spins = true },
            [Look.Water] = new Style { Sheet = "Projectile_410", Tint = new Color(0.6f, 0.75f, 1f), Dust = 33 },
        };

        internal static Style StyleOf(Look l) => styles.TryGetValue(l, out var s) ? s : styles[Look.Infection];

        static readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>A style's sheet cut into its frames (Terraria stacks them top to bottom); null when the sheet won't load.</summary>
        internal static Sprite Frame(Style s, int frame)
        {
            if (s.Sheet == null || missing.Contains(s.Sheet)) return null;
            if (!frames.TryGetValue(s.Sheet, out var list))
            {
                try
                {
                    var tex = Terraria.TerrariaAssets.Texture(s.Sheet);
                    int h = tex.height / Mathf.Max(1, s.Frames);
                    list = new Sprite[Mathf.Max(1, s.Frames)];
                    for (int i = 0; i < list.Length; i++) list[i] = FxBatch.Frame(s.Sheet, 0, i * h, tex.width, h, tex.width / 2f, h / 2f);
                    frames[s.Sheet] = list;
                }
                catch (System.Exception e)
                {
                    missing.Add(s.Sheet);
                    Plugin.Log.LogWarning($"[敌人AI] 贴图 {s.Sheet} 读不到：{e.Message}");
                    return null;
                }
            }
            return list[Mathf.Abs(frame) % list.Length];
        }

        static Sprite white;

        /// <summary>A 4x4 white square, one Terraria pixel a texel: stretched for warning lines, beams, pools.</summary>
        internal static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    var px = new Color[16];
                    for (int i = 0; i < 16; i++) px[i] = Color.white;
                    tex.SetPixels(px);
                    tex.Apply();
                    white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                }
                return white;
            }
        }

        /// <summary>Dust round a point (world units) in a box w×h (world units), as Terraria's Dust.NewDust.</summary>
        internal static void Dust(Vector2 at, float w, float h, int type, Vector2 velocityPx, float scale = 1.1f, Color? tint = null, bool noGravity = true)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var topLeft = TerraParticles.ToPx(at + new Vector2(-w / 2f, h / 2f));
            int d = p.NewDust(topLeft, Mathf.Max(5, Mathf.RoundToInt(w / Px)), Mathf.Max(5, Mathf.RoundToInt(h / Px)), type, velocityPx.x, velocityPx.y, 60, scale);
            if (d < 0) return;
            p[d].NoGravity = noGravity;
            if (tint.HasValue) p[d].Color = tint.Value;
        }

        /// <summary>Who threw it: its enemy's damage factor (an elite's shots hit as hard as its touch, PlayerHooks.SourceFactor).</summary>
        internal static HealthManager OwnerOf(GameObject go)
        {
            if (go == null) return null;
            var a = go.GetComponent<Attack>();
            return a != null ? a.Owner : null;
        }

        /// <summary>The game's Enemy Attack layer: the hero's HeroBox meets it (it never meets Default, so nothing there hurts).</summary>
        const int EnemyAttackLayer = 22;

        static GameObject Make(string name, HealthManager owner, Vector2 at)
        {
            var go = new GameObject(name) { layer = EnemyAttackLayer };
            go.AddComponent<World.TerraOwned>();
            if (owner != null && owner.gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            // Awake while it lasts: a still cloud or pool and a player standing in it both slept, and the hero's box heard
            // no more of it once his immunity after the first touch was over.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            return go;
        }

        internal static Shot Fire(HealthManager owner, IAim brain, Move m, Vector2 at, Vector2 velocity, float sizeScale = 1f)
        {
            var go = Make("TK AI Shot", owner, at);
            var s = go.AddComponent<Shot>();
            s.Init(owner, brain, m, velocity, sizeScale);
            EnemyAi.DevShots++;
            return s;
        }

        internal static Zone Area(HealthManager owner, IAim brain, Move m, Zone.Shape shape, Vector2 at, float warn, float active)
        {
            var go = Make("TK AI Area", owner, at);
            var z = go.AddComponent<Zone>();
            z.Init(owner, brain, m, shape, warn, active);
            EnemyAi.DevAreas++;
            return z;
        }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[24];

        /// <summary>The nearest of some hits that is the room's own ground: not what the player built, not a trigger or a one-way platform.</summary>
        static RaycastHit2D Nearest(int n)
        {
            RaycastHit2D best = default;
            float d = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger || c.usedByEffector || World.ChunkColliders.IsBuilt(c) || hits[i].distance >= d) continue;
                best = hits[i];
                d = hits[i].distance;
            }
            return best;
        }

        /// <summary>A ray against the room's own ground only (the new AI's bosses go through what the player built).</summary>
        internal static RaycastHit2D NativeRay(Vector2 from, Vector2 dir, float distance) =>
            Nearest(Physics2D.RaycastNonAlloc(from, dir, hits, distance, TerrainMask));

        /// <summary>Anything to stand on under a point within a reach: the room's ground, its one-way platforms or what the player built.</summary>
        internal static bool FootingBelow(Vector2 from, float reach)
        {
            int n = Physics2D.RaycastNonAlloc(from, Vector2.down, hits, reach, TerrainMask);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c != null && !c.isTrigger) return true;
            }
            return false;
        }

        /// <summary>A circle cast against the room's own ground only.</summary>
        internal static RaycastHit2D NativeCircle(Vector2 from, float radius, Vector2 dir, float distance) =>
            Nearest(Physics2D.CircleCastNonAlloc(from, radius, dir, hits, distance, TerrainMask));

        /// <summary>A box cast against the room's own ground only.</summary>
        internal static RaycastHit2D NativeBox(Vector2 from, Vector2 size, Vector2 dir, float distance) =>
            Nearest(Physics2D.BoxCastNonAlloc(from, size, 0f, dir, hits, distance, TerrainMask));

        static readonly Collider2D[] overlaps = new Collider2D[16];

        /// <summary>Whether a point is inside the room's own ground (not counting ground of something's own: <paramref name="except"/> and what it carries).</summary>
        internal static bool NativeSolidAt(Vector2 at, Transform except = null)
        {
            int n = Physics2D.OverlapPointNonAlloc(at, overlaps, TerrainMask);
            for (int i = 0; i < n; i++)
            {
                var c = overlaps[i];
                if (c != null && !c.isTrigger && !c.usedByEffector && !World.ChunkColliders.IsBuilt(c) && (except == null || !c.transform.IsChildOf(except))) return true;
            }
            return false;
        }

        /// <summary>Whether a collider lies across the rope of one of the player's latched hooks.</summary>
        internal static bool CrossesRope(Collider2D col)
        {
            if (col == null || !Player.GrapplingHook.IsLatched) return false;
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            Vector2 from = hero.transform.position;
            ropes.Clear();
            Player.GrapplingHook.LatchedPoints(ropes);
            foreach (var to in ropes)
            {
                float len = Vector2.Distance(from, to);
                int steps = Mathf.Clamp(Mathf.CeilToInt(len / 0.25f), 1, 80);
                for (int i = 1; i <= steps; i++)
                    if (col.OverlapPoint(Vector2.Lerp(from, to, i / (float)steps))) return true;
            }
            return false;
        }

        static readonly List<Vector2> ropes = new List<Vector2>();

        /// <summary>Common to shots and areas: the hurting trigger, the debuff once it lands, the hook it cuts.</summary>
        internal abstract class Attack : MonoBehaviour
        {
            public HealthManager Owner;
            protected IAim brain;
            protected Move move;
            protected Collider2D col;
            protected DamageHero damage;
            float touchedAt = -10f;
            bool landedDebuff;
            protected bool hitPlayer;

            /// <summary>Every shot and area there is now (the LAN game sends the warnings and webs, which have no live DamageHero).</summary>
            internal static readonly List<Attack> All = new List<Attack>();

            void OnEnable() => All.Add(this);
            void OnDisable() => All.Remove(this);

            public Move Data => move;
            public Collider2D Col => col;
            /// <summary>Hurts now (live, not a web).</summary>
            public bool Hurts => damage != null && col != null && col.enabled;
            /// <summary>Cuts hooks (its move does, on its owner's Master or Hollow).</summary>
            public bool Cuts => move != null && move.CutHook && EnemyAi.MasterRules;

            protected void Arm(bool on)
            {
                if (damage != null) damage.enabled = on;
                if (col != null) col.enabled = on;
            }

            protected void SetUp(HealthManager owner, IAim b, Move m, Collider2D c)
            {
                Owner = owner;
                brain = b;
                move = m;
                col = c;
                col.isTrigger = true;
                damage = gameObject.AddComponent<DamageHero>();
                damage.damageDealt = Mathf.Max(1, m.Masks);
                damage.hazardType = 1;
                // The shadow dash goes through it, as through the enemies' own attacks.
                damage.shadowDashHazard = true;
            }

            void OnTriggerEnter2D(Collider2D other)
            {
                if (other != null && other.GetComponent<HeroBox>() != null) touchedAt = Time.time;
            }

            void OnTriggerStay2D(Collider2D other)
            {
                if (other != null && touchedAt < 0f && other.GetComponent<HeroBox>() != null) touchedAt = Time.time;
            }

            /// <summary>The player hurt by it just now (his last hurt came as it touched him).</summary>
            protected bool CheckHit()
            {
                if (touchedAt < 0f) return false;
                bool hurt = Player.TerraHurt.LastHurtAt >= touchedAt - 0.03f && Time.time - touchedAt < 0.25f;
                if (Time.time - touchedAt > 0.25f) touchedAt = -10f;
                if (!hurt) return false;
                touchedAt = -10f;
                hitPlayer = true;
                EnemyAi.DevHits++;
                if (!landedDebuff && move.Debuff > 0 && move.DebuffTicks > 0)
                {
                    landedDebuff = true;
                    PlayerDebuffs.Apply(move.Debuff, move.DebuffTicks);
                }
                return true;
            }

            protected void TryCutHook()
            {
                if (!move.CutHook || !EnemyAi.MasterRules || col == null || !col.enabled) return;
                if (CrossesRope(col)) EnemyAi.CutHooks(transform.position);
            }
        }

        /// <summary>A thrown thing: straight, falling, or turning after the player; lands, bounces, or bursts.</summary>
        internal sealed class Shot : Attack
        {
            Vector2 vel;
            float gravity, homing, life, born, radius, spin;
            int bounces, frame;
            Style style;
            float nextDust;

            public Vector2 Velocity => vel;
            public float Gravity => gravity;
            public float Radius => radius;
            /// <summary>Goes through what the player built (a boss's thrown things but the liquids: Bosses.BossKit).</summary>
            public bool PassBuilt;
            /// <summary>Moved by its thrower each step instead (a needle on its thread): no flight of its own.</summary>
            public bool Steered;

            /// <summary>Its way now (a steered one's thrower sets it, for its picture and the LAN game's).</summary>
            public void SetVelocity(Vector2 v) => vel = v;

            public void Init(HealthManager owner, IAim b, Move m, Vector2 velocity, float sizeScale)
            {
                radius = Mathf.Max(0.12f, m.Size * 0.5f * Tile * sizeScale);
                var c = gameObject.AddComponent<CircleCollider2D>();
                c.radius = radius;
                SetUp(owner, b, m, c);
                vel = velocity;
                gravity = m.Gravity * Tile;
                homing = m.Homing;
                life = m.Life;
                bounces = m.Bounces;
                born = Time.time;
                style = StyleOf(m.Look);
                spin = Random.Range(0f, 6.28f);
            }

            const float Tile = 0.5f;

            void FixedUpdate()
            {
                float dt = Time.fixedDeltaTime;
                if (Steered) { TryCutHook(); return; }
                if (homing > 0f && brain != null && brain.HasTarget)
                {
                    Vector2 want = brain.TargetCenter - (Vector2)transform.position;
                    float speed = vel.magnitude;
                    if (want.sqrMagnitude > 0.01f && speed > 0.01f)
                        vel = Vector3.RotateTowards(vel, want.normalized * speed, homing * Mathf.Deg2Rad * dt, 0f);
                }
                vel.y -= gravity * dt;
                Vector2 from = transform.position, to = from + vel * dt;
                var hit = PassBuilt ? NativeCircle(from, radius * 0.6f, vel.normalized, (to - from).magnitude) : Physics2D.CircleCast(from, radius * 0.6f, vel.normalized, (to - from).magnitude, TerrainMask);
                if (hit.collider != null && !hit.collider.isTrigger && !OneWay(hit))
                {
                    if (bounces > 0)
                    {
                        bounces--;
                        vel = Vector2.Reflect(vel, hit.normal) * 0.85f;
                        transform.position = hit.centroid + hit.normal * 0.02f;
                        Dust(hit.point, 0.3f, 0.3f, style.Dust, Vector2.zero);
                        return;
                    }
                    transform.position = hit.centroid;
                    LandOn(hit.point, hit.normal);
                    return;
                }
                transform.position = to;
                TryCutHook();
            }

            /// <summary>A one-way platform the shot passes, as Terraria's hostile shots pass platforms.</summary>
            static bool OneWay(RaycastHit2D hit) => hit.collider.GetComponent<PlatformEffector2D>() != null || hit.collider.usedByEffector;

            void LandOn(Vector2 point, Vector2 normal)
            {
                for (int i = 0; i < 6; i++) Dust(point, 0.4f, 0.4f, style.Dust, normal * 2f + Random.insideUnitCircle * 2f, 1.2f, style.DustColor);
                if (move.Land != Land.None && Owner != null) Landed(point, normal);
                Destroy(gameObject);
            }

            void Landed(Vector2 point, Vector2 normal)
            {
                switch (move.Land)
                {
                    case Land.Pool:
                    case Land.Honey:
                    case Land.Web:
                    {
                        // Only on a floor (a wall or ceiling hit leaves a splash).
                        if (normal.y < 0.5f && move.Land != Land.Web) return;
                        var m = move.Clone();
                        m.Width = move.Land == Land.Web ? 3f : Mathf.Max(2.5f, move.Width);
                        m.Duration = move.Land == Land.Pool ? 2.5f : 4f;
                        m.Look = move.Land == Land.Honey ? Look.Honey : move.Land == Land.Web ? Look.Web : move.Look;
                        // A web or honey hurts as a pool does, and slows on its hit.
                        if (move.Land != Land.Pool) { m.Debuff = 32; m.DebuffTicks = 120; m.Masks = 1; }
                        Area(Owner, brain, m, normal.y >= 0.5f ? Zone.Shape.Pool : Zone.Shape.Circle, point + normal * 0.1f, 0.1f, m.Duration);
                        break;
                    }
                    case Land.Cluster:
                    {
                        var m = move.Clone();
                        m.Width = 1.5f; m.Height = 1.5f; m.Duration = 3f; m.Look = Look.Crystal;
                        Area(Owner, brain, m, Zone.Shape.Box, point + normal * 0.35f, 0.15f, m.Duration);
                        break;
                    }
                    case Land.Cloud:
                    {
                        var m = move.Clone();
                        m.Radius = Mathf.Max(2f, move.Radius); m.Duration = 1.8f; m.Drift = 1f;
                        Area(Owner, brain, m, Zone.Shape.Circle, point + normal * 0.5f, 0.2f, m.Duration);
                        break;
                    }
                    case Land.Burst:
                    {
                        var m = move.Clone();
                        m.Land = Land.None; m.Homing = 0f; m.Gravity = 0f; m.Size = move.Size * 0.6f; m.Life = 0.8f;
                        for (int i = 0; i < 4; i++)
                        {
                            float a = (45f + i * 90f) * Mathf.Deg2Rad;
                            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            if (Vector2.Dot(dir, normal) < -0.2f) dir = Vector2.Reflect(dir, normal);
                            Fire(Owner, brain, m, point + normal * 0.25f, dir * move.ShotSpeed * 0.5f * Tile);
                        }
                        break;
                    }
                }
            }

            /// <summary>Bursts where it is (a shot that shatters near the player: the Gulka's spike ball, a crystal cluster).</summary>
            public void Burst()
            {
                var m = move.Clone();
                m.Land = Land.None; m.Homing = 0f; m.Gravity = 0f; m.Size = move.Size * 0.55f; m.Life = 0.9f; m.Look = move.Look;
                Vector2 dir = vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.right;
                for (int i = -1; i <= 1; i++)
                    Fire(Owner, brain, m, transform.position, (Vector2)(Quaternion.Euler(0f, 0f, i * 35f) * dir) * vel.magnitude * 0.9f);
                Destroy(gameObject);
            }

            bool burstDone;

            void Update()
            {
                if (CheckHit())
                {
                    // Spent on the player, as Terraria's hostile shots are (penetrate 1).
                    Dust(transform.position, 0.4f, 0.4f, style.Dust, Vector2.zero, 1.3f, style.DustColor);
                    Destroy(gameObject);
                    return;
                }
                if (Time.time - born > life)
                {
                    Dust(transform.position, 0.4f, 0.4f, style.Dust, Vector2.zero, 1f, style.DustColor);
                    Destroy(gameObject);
                    return;
                }
                // The burst kind opens near where the player was going (Gulka, Crystal Hunter).
                if (!burstDone && move.Land == Land.Burst && brain != null && brain.HasTarget
                    && Vector2.Distance(transform.position, brain.TargetCenter) < 2.2f && Time.time - born > 0.15f)
                {
                    burstDone = true;
                    Burst();
                    return;
                }
                Draw();
            }

            Visual look;

            void Draw()
            {
                look.Spin = spin;
                DrawShot(style, transform.position, vel, radius, ref look);
                spin = look.Spin;
            }
        }

        /// <summary>What a drawing keeps from frame to frame (its turn, its sheet's frame, its next dust).</summary>
        internal struct Visual
        {
            public float Spin, NextDust;
            public int Frame, FrameIndex;
        }

        /// <summary>A shot drawn where it is (here, or as the scene's owner sends it to a LAN game's others).</summary>
        internal static void DrawShot(Style style, Vector2 pos, Vector2 vel, float radius, ref Visual v)
        {
            var fx = FxBatch.Instance;
            if (fx == null) return;
            if (++v.Frame % 5 == 0) v.FrameIndex++;
            var sprite = Frame(style, v.FrameIndex);
            float rot = 0f;
            if (style.Turns && vel.sqrMagnitude > 0.01f) rot = -Mathf.Atan2(vel.y, vel.x) + Mathf.PI / 2f;
            else if (style.Spins) { v.Spin += Time.deltaTime * 8f * Mathf.Sign(vel.x == 0 ? 1 : vel.x); rot = v.Spin; }
            float k = radius * 2f / Px / Mathf.Max(8f, sprite != null ? Mathf.Max(sprite.rect.width, sprite.rect.height) : 16f);
            var at = TerraParticles.ToPx(pos);
            if (sprite != null) fx.Draw(sprite, at, style.Tint, rot, Mathf.Clamp(k, 0.5f, 2.5f), overPlayer: true);
            else fx.Draw(White, at, style.Tint, 0f, radius * 2f / Px / 4f, overPlayer: true);
            if (Time.time >= v.NextDust)
            {
                v.NextDust = Time.time + 0.05f;
                Dust(pos, radius * 1.4f, radius * 1.4f, style.Dust, -vel.normalized * 0.6f, 0.9f, style.DustColor);
            }
        }

        /// <summary>
        /// A place that hurts for a while after its wait: a pulse round the enemy, a pillar, a ground wave, a cloud, a
        /// pool on the floor, a beam. Unseen and harmless while it waits, then live.
        /// </summary>
        internal sealed class Zone : Attack
        {
            internal enum Shape { Circle, Box, Pillar, Wave, Pool, Beam }

            Shape shape;
            float warnUntil, until, born;
            Style style;
            Vector2 drift;
            float angle, sweep;
            bool live;
            /// <summary>
            /// Never drawn: a hitbox on a boss's own body through a move of its own (its own animation is what shows: the
            /// user's, 2026-10-23, nothing of the mod's drawn in a boss fight).
            /// </summary>
            public bool Unseen;
            /// <summary>Only a warning: drawn while it warns, then gone (a boss's own move shown where it will land).</summary>
            public bool MarkOnly;
            /// <summary>A wave that keeps to the room's own ground, through what the player built (a boss's).</summary>
            public bool NativeOnly;
            /// <summary>Follows its enemy (a pulse round it, a beam from it).</summary>
            public Transform Follow;
            public Vector2 FollowOffset;
            float nextDust;

            internal float Width, Height, Radius, Length;
            /// <summary>A wave's way and speed (world units a second).</summary>
            internal float WaveVx;

            public Shape Form => shape;
            public bool Live => live;
            public float Angle => angle;
            public Look LookOf => move.Look;

            public void Init(HealthManager owner, IAim b, Move m, Shape s, float warn, float active)
            {
                shape = s;
                style = StyleOf(m.Look);
                born = Time.time;
                warnUntil = born + warn;
                until = warnUntil + active;
                const float tile = 0.5f;
                Radius = m.Radius * tile;
                Width = m.Width * tile;
                Height = m.Height * tile;
                Length = m.Length * tile;
                sweep = m.Sweep;
                Collider2D c;
                switch (s)
                {
                    case Shape.Circle:
                        var cc = gameObject.AddComponent<CircleCollider2D>();
                        cc.radius = Radius;
                        c = cc;
                        break;
                    case Shape.Beam:
                        var bc = gameObject.AddComponent<BoxCollider2D>();
                        bc.size = new Vector2(Length, Mathf.Max(0.2f, Width));
                        bc.offset = new Vector2(Length / 2f, 0f);
                        c = bc;
                        break;
                    case Shape.Pool:
                        var pc = gameObject.AddComponent<BoxCollider2D>();
                        pc.size = new Vector2(Mathf.Max(0.5f, Width), 0.45f);
                        pc.offset = new Vector2(0f, 0.2f);
                        c = pc;
                        break;
                    default:
                        var box = gameObject.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(Mathf.Max(0.3f, Width), Mathf.Max(0.3f, s == Shape.Pillar || s == Shape.Wave ? Height : Height));
                        box.offset = s == Shape.Pillar || s == Shape.Wave ? new Vector2(0f, box.size.y / 2f) : Vector2.zero;
                        c = box;
                        break;
                }
                SetUp(owner, b, m, c);
                Arm(false);
                if (m.Drift > 0f) drift = Vector2.one * (m.Drift * tile);
            }

            public void SetAngle(float degrees)
            {
                angle = degrees;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            void FixedUpdate()
            {
                float dt = Time.fixedDeltaTime;
                if (Follow != null) transform.position = (Vector2)Follow.position + FollowOffset;
                if (live && sweep != 0f && shape == Shape.Beam) SetAngle(angle + sweep / Mathf.Max(0.05f, until - warnUntil) * dt);
                if (shape == Shape.Wave && live)
                {
                    // Along the ground: stepping up and down with it, ending at a wall or a drop.
                    Vector2 p = transform.position;
                    p.x += WaveVx * dt;
                    var down = NativeOnly ? NativeRay(p + Vector2.up * 1.2f, Vector2.down, 3f) : Physics2D.Raycast(p + Vector2.up * 1.2f, Vector2.down, 3f, TerrainMask);
                    var wall = NativeOnly ? NativeRay(p + Vector2.up * 0.4f, new Vector2(Mathf.Sign(WaveVx), 0f), 0.3f) : Physics2D.Raycast(p + Vector2.up * 0.4f, new Vector2(Mathf.Sign(WaveVx), 0f), 0.3f, TerrainMask);
                    if (down.collider == null || wall.collider != null)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    p.y = down.point.y;
                    transform.position = p;
                }
                if (drift != Vector2.zero && brain != null && brain.HasTarget)
                {
                    Vector2 to = brain.TargetCenter - (Vector2)transform.position;
                    transform.position += (Vector3)(to.normalized * drift.x * dt);
                }
                if (live) TryCutHook();
            }

            void Update()
            {
                float now = Time.time;
                if (!live && now >= warnUntil)
                {
                    if (MarkOnly)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    live = true;
                    Arm(true);
                }
                if (now >= until)
                {
                    Destroy(gameObject);
                    return;
                }
                if (live) CheckHit();
                if (!Unseen) Draw(now);
            }

            Visual look;

            void Draw(float now)
            {
                // A mark is the kit's own bookkeeping (the move it stands for shows itself): never drawn.
                if (MarkOnly) return;
                look.NextDust = nextDust;
                DrawZone(style, shape, live, transform.position, Width, Height, Radius, Length, angle, now, ref look);
                nextDust = look.NextDust;
            }
        }

        /// <summary>
        /// An area drawn (here, or as the scene's owner sends it) once it is live: nothing shows while it waits (the user's,
        /// 2026-10-23: no warning marks, the fights learnt by playing them).
        /// </summary>
        internal static void DrawZone(Style style, Zone.Shape shape, bool live, Vector2 c, float Width, float Height, float Radius, float Length, float angle, float now, ref Visual v)
        {
                if (!live) return;
                var fx = FxBatch.Instance;
                bool dusty = now >= v.NextDust;
                if (dusty) v.NextDust = now + (live ? 0.04f : 0.08f);
                Color tint = style.Tint;
                switch (shape)
                {
                    case Zone.Shape.Circle:
                        // Filled, a cloud of pixel rows (no ring round it: a ring reads as a warning), seen when the
                        // particle budget is full too.
                        if (fx != null)
                        {
                            const int rows = 10;
                            float row = 2f * Radius / rows;
                            for (int i = 0; i < rows; i++)
                            {
                                float y = -Radius + (i + 0.5f) * row;
                                float half = Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - y * y));
                                if (half < Px) continue;
                                fx.Draw(White, TerraParticles.ToPx(c + new Vector2(0f, y)), new Color(tint.r, tint.g, tint.b, 0.2f), 0f,
                                    new Vector2(2f * half / Px / 4f, row / Px / 4f), overPlayer: true);
                            }
                        }
                        if (dusty)
                        {
                            int n = live ? 3 : 2;
                            for (int i = 0; i < n; i++)
                            {
                                float a = Random.Range(0f, Mathf.PI * 2f);
                                float r = live ? Random.Range(0f, Radius) : Radius;
                                Dust(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0.2f, 0.2f, style.Dust, live ? Random.insideUnitCircle : Vector2.zero, live ? 1.3f : 0.9f, style.DustColor);
                            }
                        }
                        if (live && fx != null && style.Frames > 1)
                        {
                            var sp = Frame(style, (int)(now * 10f));
                            if (sp != null) fx.Draw(sp, TerraParticles.ToPx(c), new Color(tint.r, tint.g, tint.b, 0.55f), 0f, Radius * 2f / Px / Mathf.Max(8f, sp.rect.width));
                        }
                        break;
                    case Zone.Shape.Pool:
                        if (fx != null) fx.Draw(White, TerraParticles.ToPx(c + Vector2.up * 0.08f), new Color(tint.r, tint.g, tint.b, live ? 0.6f : 0.3f), 0f, new Vector2(Width / Px / 4f, 0.16f / Px / 4f));
                        if (dusty) Dust(c + new Vector2(Random.Range(-Width / 2f, Width / 2f), 0.15f), 0.2f, 0.2f, style.Dust, new Vector2(0f, -1.2f), 0.9f, style.DustColor);
                        break;
                    case Zone.Shape.Beam:
                        if (fx != null)
                        {
                            float w = live ? Mathf.Max(0.2f, Width) : 0.06f;
                            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                            var mid = c + dir * Length / 2f;
                            var col2 = live ? new Color(tint.r, tint.g, tint.b, 0.85f) : new Color(1f, 1f, 1f, 0.45f + 0.3f * Mathf.Sin(now * 30f));
                            fx.Draw(White, TerraParticles.ToPx(mid), col2, -angle * Mathf.Deg2Rad, new Vector2(Length / Px / 4f, w / Px / 4f), overPlayer: true);
                            if (live && dusty) Dust(c + dir * Random.Range(0f, Length), 0.25f, 0.25f, style.Dust, Vector2.zero, 1.2f, style.DustColor);
                        }
                        break;
                    default:
                    {
                        // Pillars, waves, boxes: the style's own dust filling the shape, as much as it is big (no flat
                        // box behind it: a box reads as a warning frame).
                        float w = Mathf.Max(0.3f, Width), h = Mathf.Max(0.3f, Height);
                        bool rises = shape == Zone.Shape.Pillar || shape == Zone.Shape.Wave;
                        Vector2 mid = rises ? c + Vector2.up * h / 2f : c;
                        if (dusty)
                        {
                            int n = Mathf.Clamp(Mathf.CeilToInt(w * h), 4, 8);
                            for (int i = 0; i < n; i++)
                                Dust(mid + new Vector2(Random.Range(-w / 2f, w / 2f), Random.Range(-h / 2f, h / 2f)), 0.25f, 0.25f, style.Dust,
                                    rises ? new Vector2(0f, -3f) : Random.insideUnitCircle, 1.4f, style.DustColor);
                        }
                        break;
                    }
                }
        }
    }
}
