using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's knockback on an enemy, delivered through the enemy's own recoil (the way the
    /// nail pushes it). NPC.StrikeNPC gives the NPC a speed of the weapon's knockback times its
    /// knockback resistance, squeezed above 8 and capped at 16, 1.4 times on a crit; a hit worth
    /// less than a fifteenth of its life (Master Mode) loses the resistance a second time, so light
    /// hits barely move it. Hollow Knight's recoil instead slides the enemy at a set speed for a set
    /// time; the multiplier here makes that slide as long as the Terraria push would carry it.
    /// Five knockbacks within three seconds make an enemy steady: no knockback for the next three.
    /// </summary>
    internal static class TerraKnockback
    {
        static readonly AccessTools.FieldRef<HealthManager, Recoil> recoilOf = AccessTools.FieldRefAccess<HealthManager, Recoil>("recoil");
        static readonly AccessTools.FieldRef<Recoil, float> speedBase = AccessTools.FieldRefAccess<Recoil, float>("recoilSpeedBase");
        static readonly AccessTools.FieldRef<Recoil, float> duration = AccessTools.FieldRefAccess<Recoil, float>("recoilDuration");

        /// <summary>The speed in px/tick a hit gives an NPC sideways (NPC.StrikeNPC, NPC.cs:82216-82306).</summary>
        public static float Speed(float knockBack, float resist, int damage, bool crit, int lifeMax)
        {
            if (knockBack <= 0f || resist <= 0f) return 0f;
            float k = knockBack * resist;
            if (k > 8f) k = 8f + (k - 8f) * 0.9f;
            if (k > 10f) k = 10f + (k - 10f) * 0.8f;
            if (k > 12f) k = 12f + (k - 12f) * 0.7f;
            if (k > 14f) k = 14f + (k - 14f) * 0.6f;
            if (k > 16f) k = 16f;
            if (crit) k *= 1.4f;
            return damage * 15 > lifeMax ? k : k * resist;
        }

        /// <summary>
        /// How far that speed carries a walking NPC, in px: it is thrown up at three quarters of it
        /// and keeps the sideways speed through the hop (gravity 0.3 px/tick², so 5v ticks in the
        /// air), then slides to a stop on the ground losing a fifth a tick (the fighter AI's
        /// overspeed brake, about 4v more).
        /// </summary>
        public static float Distance(float speed) => 5f * speed * speed + 4f * speed;

        /// <summary>Dev: the enemy's own recoil (speed × time = how far the nail pushes it), or null.</summary>
        public static string DevRecoil(HealthManager hm)
        {
            var recoil = hm != null ? recoilOf(hm) : null;
            if (recoil == null) return null;
            return $"{speedBase(recoil):F1}×{duration(recoil):F2}s={speedBase(recoil) * duration(recoil):F2}";
        }

        /// <summary>The recoil multiplier that slides this enemy that far (0: no push at all).</summary>
        public static float Magnitude(HealthManager hm, float speed)
        {
            if (speed <= 0f || hm == null) return 0f;
            var recoil = recoilOf(hm);
            if (recoil == null) return 0f;
            // The nail (multiplier 1) slides it recoilSpeedBase for recoilDuration.
            float perOne = speedBase(recoil) * duration(recoil);
            if (perOne <= 0f) return 0f;
            return Distance(speed) * Effects.TerraParticles.Px / perOne;
        }

        /// <summary>
        /// NPC.StrikeNPC's push speed in px/tick: knockBack × knockBackResist, squeezed above 8 and
        /// capped at 16, 1.4 times on a crit. Every hit counts as a full one (damage × 15 above
        /// lifeMax): Hallownest's enemies carry many times a Terraria enemy's life for the game's
        /// longer fights, and the light-hit rule would take the resistance off a second time on
        /// nearly every hit, where in Terraria (Classic) a common enemy takes the whole push from
        /// the first weapons on.
        /// </summary>
        public static float Launch(float knockBack, float resist, bool crit)
        {
            if (knockBack <= 0f || resist <= 0f) return 0f;
            float k = knockBack * resist;
            if (k > 8f) k = 8f + (k - 8f) * 0.9f;
            if (k > 10f) k = 10f + (k - 10f) * 0.8f;
            if (k > 12f) k = 12f + (k - 12f) * 0.7f;
            if (k > 14f) k = 14f + (k - 14f) * 0.6f;
            if (k > 16f) k = 16f;
            return crit ? k * 1.4f : k;
        }

        public static int DevOwnPushes, DevLaunches, DevPushCalls, DevImmune, DevPushUps;

        /// <summary>
        /// The Nail's upward slash: the struck enemy knocked up by Terraria's launch speed (its level's resistance; bosses and
        /// elites stand), a body that falls given that speed upward, the rest the game's own upward recoil.
        /// </summary>
        public static void PushUp(HealthManager hm, float knockBack)
        {
            if (hm == null || hm.isDead || Immune(hm)) return;
            var info = EnemyStats.Of(hm);
            if (info == null || EnemyStats.NoKnockback(info)) return;
            float speed = Launch(knockBack, World.Areas.KnockbackTaken(info.Level), false);
            if (speed <= 0f || !CountPush(hm)) return;
            DevPushUps++;
            var body = hm.GetComponent<Rigidbody2D>();
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic && body.gravityScale > 0.01f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, speed * Effects.TerraParticles.Px * 60f));
                return;
            }
            var recoil = hm.GetComponent<Recoil>();
            if (recoil != null) recoil.RecoilByDirection(1, speed / 8f);
        }

        /// <summary>
        /// Enemies that go round the terrain on a track of their own, not after the player, take no
        /// knockback: the tiktiks circling a ledge (the game's Climber), and the crawlers walking a
        /// wall or a roof (the game freezes them in place when hit, their body without gravity).
        /// Pushed, they would be put into the rock or off their surface and stick there.
        /// </summary>
        public static bool Immune(HealthManager hm)
        {
            if (hm == null) return false;
            if (hm.GetComponent<Climber>() != null) return true;
            // A part of another enemy goes where its owner puts it: the False Knight's head, pushed on its own
            // while he lay stunned, stayed where the blows left it, and the game brought it out from there the
            // next times, beyond the arena's gates, away from the head drawn on his body.
            var up = hm.transform.parent;
            if (up != null && up.GetComponentInParent<HealthManager>() != null) return true;
            var recoil = recoilOf(hm);
            var body = hm.GetComponent<Rigidbody2D>();
            // What the game itself never moves on a hit (the user's: those that can't move stay put): no Recoil, or one
            // with no speed (Recoil.RecoilByDirection: recoilSpeedBase × the hit's magnitude), as the turrets, the Fool
            // Eaters, the egg sacs have it; a static body.
            if (recoil == null || speedBase(recoil) <= 0f) return true;
            if (body != null && body.bodyType == RigidbodyType2D.Static) return true;
            return recoil.freezeInPlace && body != null && body.gravityScale == 0f;
        }

        /// <summary>Dev: whether an enemy takes knockback, and what decides it.</summary>
        public static string DevWhy(HealthManager hm)
        {
            if (hm == null) return "-";
            // A census loads scenes without waking their objects: the component itself, the field filled at Awake.
            var recoil = recoilOf(hm) ?? hm.GetComponent<Recoil>();
            var body = hm.GetComponent<Rigidbody2D>();
            string how = body == null ? "无刚体" : body.bodyType + (body.gravityScale == 0f ? "无重力" : "");
            string why = hm.GetComponent<Climber>() != null ? "绕地形爬" : hm.transform.parent != null && hm.transform.parent.GetComponentInParent<HealthManager>() != null ? "别的敌人的部件"
                : recoil == null ? "无Recoil" : speedBase(recoil) <= 0f ? "Recoil速度0" : body != null && body.bodyType == RigidbodyType2D.Static ? "静态刚体"
                : recoil.freezeInPlace && body != null && body.gravityScale == 0f ? "受击定住" : null;
            return (why != null ? "免疫(" + why + ")" : "可击退") + " " + how + (recoil != null ? $" recoil {speedBase(recoil):F1}" : "");
        }

        public static float DevPushSpeed;
        public static float DevLastOwnPush, DevLastLaunch;

        /// <summary>Knockbacks within this many seconds (on the game's clock) that make an enemy steady.</summary>
        public const float SteadyWindow = 3f;
        /// <summary>How many knockbacks in that window: the last of them still moves it.</summary>
        public const int SteadyAfter = 5;
        /// <summary>How long it then takes no knockback at all (s); after that the count starts again.</summary>
        public const float SteadyFor = 3f;

        sealed class Recent
        {
            public readonly float[] At = new float[SteadyAfter];
            public int Count, Next;
            public float SteadyUntil = -1f;
        }

        static readonly System.Collections.Generic.Dictionary<int, Recent> recent = new System.Collections.Generic.Dictionary<int, Recent>();

        public static int DevSteadied, DevHeldBySteady;

        /// <summary>Whether an enemy knocked back and launched too often just now takes no more knockback (for <see cref="SteadyFor"/>).</summary>
        public static bool Steady(HealthManager hm) =>
            hm != null && recent.TryGetValue(hm.GetInstanceID(), out var r) && Time.time < r.SteadyUntil;

        /// <summary>
        /// A knockback about to move an enemy: false (and nothing moves) while it is steady. Five within three seconds
        /// make it steady for three, so a stream of hits can't pin it to a wall or juggle it in the air.
        /// </summary>
        static bool CountPush(HealthManager hm)
        {
            int id = hm.GetInstanceID();
            if (!recent.TryGetValue(id, out var r))
            {
                if (recent.Count > 2048) recent.Clear();
                recent[id] = r = new Recent();
            }
            float now = Time.time;
            if (now < r.SteadyUntil)
            {
                DevHeldBySteady++;
                return false;
            }
            r.At[r.Next] = now;
            r.Next = (r.Next + 1) % SteadyAfter;
            r.Count = Mathf.Min(r.Count + 1, SteadyAfter);
            // The oldest of the last five, within the window: steady from now on.
            if (r.Count == SteadyAfter && now - r.At[r.Next] <= SteadyWindow)
            {
                r.SteadyUntil = now + SteadyFor;
                r.Count = 0;
                DevSteadied++;
            }
            return true;
        }

        /// <summary>
        /// Terraria's knockback on an enemy. One with a body of its own is launched: thrown back at
        /// the push speed and up at three quarters of it (half for a flyer), carried by Terraria's
        /// NPC gravity (0.3 px/tick², falling at most 10) and braked on landing (×0.8 a tick, the
        /// fighter AI's), its own moves held off meanwhile (<see cref="KnockFlight"/>). One without
        /// a body slides along the ground (<see cref="KnockSlide"/>). Those that go round the
        /// terrain on their own track, not after the player (<see cref="Immune"/>), aren't moved.
        /// </summary>
        /// <param name="bigHit">
        /// StrikeNPC's test: the hit's damage ×10 (×15 in Expert and Master) over the enemy's life.
        /// A big hit adds the push to the speed the enemy has, no further than the push itself (one
        /// already flying back and up that fast goes on as it was); a small one sets the speed, the
        /// push taken by the enemy's resistance a second time.
        /// </param>
        public static void Push(HealthManager hm, float speed, float facing, bool bigHit = true, float resist = 1f)
        {
            DevPushCalls++;
            DevPushSpeed = speed;
            if (hm == null || speed <= 0f || hm.isDead) return;
            var col = BodyCollider(hm);
            if (col == null) return;
            var body = hm.GetComponent<Rigidbody2D>();
            if (Immune(hm))
            {
                DevImmune++;
                return;
            }
            if (!CountPush(hm)) return;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            {
                var flight = hm.GetComponent<KnockFlight>() ?? hm.gameObject.AddComponent<KnockFlight>();
                flight.Begin(body, col, speed, facing >= 0f ? 1f : -1f, body.gravityScale <= 0.01f, bigHit, resist);
                DevLaunches++;
                DevLastLaunch = speed;
                return;
            }
            float distance = Distance(bigHit ? speed : speed * resist) * Effects.TerraParticles.Px;
            if (distance <= 0.01f) return;
            int cardinal = facing >= 0f ? 0 : 2;
            var slide = hm.GetComponent<KnockSlide>() ?? hm.gameObject.AddComponent<KnockSlide>();
            slide.Begin(col, cardinal, distance);
            DevOwnPushes++;
            DevLastOwnPush = distance;
        }

        /// <summary>The enemy's solid collider (not a trigger), its own first.</summary>
        static Collider2D BodyCollider(HealthManager hm)
        {
            foreach (var c in hm.GetComponents<Collider2D>())
                if (c.enabled && !c.isTrigger) return c;
            return hm.GetComponent<Collider2D>();
        }
    }

    /// <summary>
    /// A launched enemy (NPC.StrikeNPC's velocity, then NPC.UpdateNPC's gravity): its body's
    /// velocity set every physics step, after every other script so its own moves can't undo it,
    /// the physics keeping it out of the walls. Into a roof the rise stops, into a wall the push;
    /// on the ground it brakes to a stop and is handed back the pace it had before (a crawlid
    /// walks on). A flyer drifts back slowing (×0.92 a tick).
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class KnockFlight : MonoBehaviour
    {
        const float PxT = 60f / 32f;
        Rigidbody2D body;
        Collider2D col;
        Vector2 v, before;
        float gravity;
        bool flying, grounded, active, first;
        float age;

        public bool Active => active;

        public void Begin(Rigidbody2D rb, Collider2D c, float speed, float dir, bool fly, bool bigHit = true, float resist = 1f)
        {
            body = rb;
            col = c;
            if (!active)
            {
                before = rb.linearVelocity;
                gravity = rb.gravityScale;
            }
            // Terraria's gravity alone carries it: the body's own would pull it down a second time each step.
            rb.gravityScale = 0f;
            // The speed it has now (Terraria's px a tick, up positive): its own, or the last launch's.
            var cur = active ? v : rb.linearVelocity / PxT;
            float lift = speed * (fly ? 0.5f : 0.75f);
            if (bigHit)
            {
                // NPC.StrikeNPC: the push added toward the hit (twice against a speed the other way),
                // no faster than the push; the lift added to a fall or a slower rise, no faster than
                // the lift. An enemy already thrown back and up is thrown no further by more hits.
                if (dir < 0f && cur.x > -speed)
                {
                    if (cur.x > 0f) cur.x -= speed;
                    cur.x = Mathf.Max(cur.x - speed, -speed);
                }
                else if (dir > 0f && cur.x < speed)
                {
                    if (cur.x < 0f) cur.x += speed;
                    cur.x = Mathf.Min(cur.x + speed, speed);
                }
                if (cur.y < lift) cur.y = Mathf.Min(cur.y + lift, lift);
                v = cur;
            }
            // A small hit for its life: the speed set, the resistance taken a second time.
            else v = new Vector2(dir * speed * resist, lift * resist);
            flying = fly;
            grounded = false;
            active = true;
            first = true;
            age = 0f;
            DevPeak = DevBack = 0f;
            DevAir = airTicks = 0;
        }

        /// <summary>Where the body was when its velocity was last set, and that velocity (world units).</summary>
        Vector2 lastPos, lastCmd, startPos;
        int airTicks;

        /// <summary>Dev: the last launch's highest point above where it was hit, its time in the air (ticks) and how far back it went.</summary>
        public static float DevPeak, DevBack;
        public static int DevAir;

        void FixedUpdate()
        {
            if (!active) return;
            if (body == null || col == null)
            {
                active = false;
                return;
            }
            float ticks = Time.fixedDeltaTime * 60f;
            age += Time.fixedDeltaTime;
            if (first) startPos = body.position;
            else
            {
                // What the last physics step did with the velocity given: a roof stopped the rise,
                // a wall the push, the ground the fall (the collider's own overlap with the floor
                // it walks on would fool a cast, the movement doesn't).
                var moved = body.position - lastPos;
                var expected = lastCmd * Time.fixedDeltaTime;
                if (lastCmd.y > 0.05f && moved.y < expected.y * 0.3f) v.y = 0f;
                if (Mathf.Abs(lastCmd.x) > 0.05f && Mathf.Abs(moved.x) < Mathf.Abs(expected.x) * 0.3f) v.x = 0f;
                if (!flying && lastCmd.y < -0.05f && moved.y > expected.y * 0.3f) grounded = true;
                DevPeak = Mathf.Max(DevPeak, body.position.y - startPos.y);
                DevBack = Mathf.Max(DevBack, Mathf.Abs(body.position.x - startPos.x));
                if (!grounded) DevAir = ++airTicks;
            }
            first = false;
            if (flying)
            {
                v *= Mathf.Pow(0.92f, ticks);
                if (v.magnitude < 0.5f || age > 0.9f)
                {
                    Finish(false);
                    return;
                }
            }
            else if (!grounded)
            {
                v.y = Mathf.Max(v.y - 0.3f * ticks, -10f);
                if (age > 2f)
                {
                    Finish(true);
                    return;
                }
            }
            else
            {
                v.y = Mathf.Min(0f, body.linearVelocity.y / PxT);
                v.x *= Mathf.Pow(0.8f, ticks);
                if (Mathf.Abs(v.x) < 0.4f || age > 2f)
                {
                    Finish(true);
                    return;
                }
            }
            body.linearVelocity = v * PxT;
            lastCmd = body.linearVelocity;
            lastPos = body.position;
        }

        void OnDisable()
        {
            if (active && body != null) body.gravityScale = gravity;
            active = false;
        }

        void Finish(bool walker)
        {
            active = false;
            if (body == null) return;
            body.gravityScale = gravity;
            // A walker goes on as it went (the crawlid only sets its pace when it starts or turns).
            body.linearVelocity = walker ? new Vector2(before.x, body.linearVelocity.y) : body.linearVelocity * 0.3f;
        }
    }

    /// <summary>
    /// The push itself: the distance covered easing out over 0.12-0.35 s (longer for a longer push),
    /// moved in FixedUpdate as the game's Recoil moves an enemy, through its Sweep so it never goes
    /// into the ground or a wall.
    /// </summary>
    internal sealed class KnockSlide : MonoBehaviour
    {
        Sweep sweep;
        float total, done, time, duration;
        bool active;

        public void Begin(Collider2D col, int cardinal, float distance)
        {
            sweep = new Sweep(col, cardinal, 3);
            total = distance;
            done = 0f;
            time = 0f;
            duration = Mathf.Clamp(0.1f + distance * 0.08f, 0.12f, 0.35f);
            active = true;
        }

        void FixedUpdate()
        {
            if (!active) return;
            time += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(time / duration);
            float target = total * (1f - (1f - k) * (1f - k));
            float step = target - done;
            if (step > 0f)
            {
                bool blocked = sweep.Check(transform.position, step, 1 << World.ChunkColliders.TerrainLayer, out float clipped);
                if (clipped > Mathf.Epsilon) transform.Translate(sweep.Direction * clipped, Space.World);
                done = target;
                if (blocked) active = false;
            }
            if (k >= 1f) active = false;
        }
    }
}
