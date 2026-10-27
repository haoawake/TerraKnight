using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The False Knight and the Failed Champion (FalseyControl; 空洞骑士首领新版AI设计.md §2.1):
    /// <list type="bullet">
    /// <item>the mace's ground wave itself (the game's own: its rock spurts, their look and their hurt) turns up the arena's
    /// wall at the end of the floor, runs along the ceiling and goes at the far wall, straight on with no pause; the
    /// Champion sends a second one the other way;</item>
    /// <item>the jump slam's landing (marked while he falls) shakes the player's hooks loose and drops him through the
    /// platforms;</item>
    /// <item>every barrel that falls comes down where the player is as it drops (marked under him), the rage's half as
    /// many;</item>
    /// <item>his jumps sometimes land where the player is going, never past the arena's sides;</item>
    /// <item>new, the high leap: a player up off the floor (on a platform, hanging under the ceiling) gets him leaping
    /// straight up at him, mace raised (his own jump attack), and down again into his jump slam;</item>
    /// <item>Crossroads_10's left wall takes no hits while he fights (the game opens it at his first rage's end, and its
    /// break takes the left gate away); broken before, he keeps to the arena's side of its gate.</item>
    /// </list>
    /// </summary>
    [Kit("FalseKnight")]
    internal sealed class FalseKnightKit : BossKit
    {
        protected override string ControlName => "FalseyControl";
        protected override Look Theme => Look.Rock;

        bool Champion => Dream || Scene == "GG_Failed_Champion";

        /// <summary>His stun and his end (the shell down, the head out; through the floor and after): his own script moves him.</summary>
        static readonly System.Collections.Generic.HashSet<string> DownStates = new System.Collections.Generic.HashSet<string>
        {
            "Stun Start", "Check Direction", "Stun Turn L", "Stun Turn R", "Stun In Air", "Stun Land", "Roll End",
            "Pause Short", "Pause Long", "Check If GG", "Open Uuup", "Head Reset", "Opened", "Hit", "Recover", "Stun Fail",
            "Death Open", "Head Frame", "Opened 2", "Hit 2", "Death Anim Start",
        };

        bool Down => beaten || DownStates.Contains(StateNow ?? "");

        // Down, the watchdog leaves him be (he lies on the block of ground his shell puts up; he falls through the floor at
        // his end, out of the arena below).
        protected override bool InGroundNow => Down;
        protected override bool Roams => beaten;

        protected override void Setup()
        {
            OnEnter("Floor Break", () => beaten = true);
            Tempo("Idle", "Rage End");
            // His jumps: the hero's x read in, then led (sometimes) before the jump's speed is worked out from it.
            At("Towards", 1, () => LeadHeroX(0f));
            At("JA Right", 1, () => LeadHeroX(0f));
            At("JA Left", 1, () => LeadHeroX(0f));
            OnEnter("S Attack Recover", () => Run(Climb()), first: false);
            OnEnter("JA Fall", () => MarkGround(Me, 10f, Warn(0.45f)));
            OnEnter("JA Slam", Shake);
            if (AddState("TK High Leap", HighLeap, "JA Fall") != null)
                Branch("Move Choice", "TK High Leap", () => HasTarget && TargetHeight > 4f && !Grace && (WantAntiAir() || Random.value < 0.3f));
            Run(Barrels());
            HoldWall();
        }

        protected override void Tick() => KeepIn();

        void LeadHeroX(float extra)
        {
            var x = Float("Hero X");
            if (x != null && RollLead()) x.Value = Mathf.Clamp(Lead(1f).x + extra, Arena.xMin + 2f, Arena.xMax - 2f);
        }

        // ------------------------------------------------------------------ the high leap

        /// <summary>
        /// Crouched a moment, then up at the player (where he will be) mace raised, his upper body through him (kept under
        /// the ceiling), a breath there, then his jump attack's fall and slam.
        /// </summary>
        IEnumerator HighLeap()
        {
            // His script's own body (what its jumps move), whichever part his kit sits on.
            var go = Control != null ? Control.gameObject : gameObject;
            var mover = go.GetComponent<Rigidbody2D>();
            if (mover == null) mover = rb;
            Hostiles.Zone escort = null;
            try
            {
                if (mover != null) mover.linearVelocity = Vector2.zero;
                Batch2.Play(go, "Jump Antic");
                yield return new WaitForSeconds(Antic(0.4f));
                Vector2 pivot = (mover != null ? mover.position : (Vector2)go.transform.position) - Me;
                float half = body != null ? body.bounds.extents.y : 2.5f;
                var t = Lead(0.35f);
                float ceil = CeilingAbove(t + Vector2.up * 0.3f, 30f);
                float x = Mathf.Clamp(t.x, Arena.xMin + 2f, Arena.xMax - 2f);
                float y = Mathf.Min(t.y - half * 0.3f, ceil - half - 0.2f);
                var end = new Vector2(x, y);
                // His whole body along the way, short of the room's rock: the roof over the player isn't always the one over
                // his path (lower at his sides; the player up on the roof), and the room's ground is thin outlines a body
                // driven into gets through, to stand on their inside (the Godhome False Knight under the roof, 10-25).
                if (body != null)
                {
                    var from = Me + Vector2.up * 0.05f;
                    var path = end - from;
                    var hit = Hostiles.NativeBox(from, (Vector2)body.bounds.size * 0.95f, path.normalized, path.magnitude);
                    if (hit.collider != null) end = from + path.normalized * Mathf.Max(0f, hit.distance - 0.2f);
                }
                bool right = x > Me.x;
                var sc = go.transform.localScale;
                // Facing as his Turn R and Turn L leave him (a positive x scale faces right).
                go.transform.localScale = new Vector3(right ? Mathf.Abs(sc.x) : -Mathf.Abs(sc.x), sc.y, sc.z);
                var facing = Bool("Facing Right");
                if (facing != null) facing.Value = right;
                var recoil = Float("Recoil Speed");
                if (recoil != null) recoil.Value = right ? -3f : 3f;
                Batch2.Play(go, "Jump Attack Up");
                if (mover != null) mover.gravityScale = 0f;
                const float speed = 40f;
                float time = Vector2.Distance(Me, end) / speed;
                escort = Batch2.Escort(this, go.transform, -pivot, 4f, time + 0.4f, Look.Rock);
                // Driven on his body's own position (its transform lags a fixed step behind when interpolated), a physics step
                // at a time; stopped where something holds him (pressed on, he would be through it).
                Vector2 was = mover != null ? mover.position : Vector2.zero;
                int held = 0;
                for (float until = Time.time + time + 0.25f; mover != null && Time.time < until;)
                {
                    var d = end + pivot - mover.position;
                    if (d.sqrMagnitude < 0.04f) break;
                    float step = Mathf.Min(speed, d.magnitude / Time.fixedDeltaTime);
                    mover.linearVelocity = d.normalized * step;
                    yield return new WaitForFixedUpdate();
                    if (mover == null) break;
                    if ((mover.position - was).magnitude < step * Time.fixedDeltaTime * 0.2f) { if (++held >= 2) break; }
                    else held = 0;
                    was = mover.position;
                }
                if (mover != null) mover.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.12f);
            }
            finally
            {
                if (escort != null) Destroy(escort.gameObject);
                // His jump attack's fall takes it from here (its own gravity and speeding fall).
                if (mover != null)
                {
                    mover.gravityScale = 0.12f;
                    mover.linearVelocity = new Vector2(0f, -2f);
                }
            }
        }

        // ------------------------------------------------------------------ the wave up the wall

        /// <summary>
        /// The game's own wave out to the wall, then up it and over the ceiling (the Champion's a copy the other way). The
        /// wave itself is unseen: what shows and hurts are the rock spurts it leaves as it goes (its "Shockwave Object",
        /// one a frame from the pool); driven, it leaves them up the wall and along the ceiling, turned out of the rock.
        /// </summary>
        IEnumerator Climb()
        {
            yield return null;
            var sw = Go("Shockwave")?.Value;
            if (sw == null || !sw.activeInHierarchy) yield break;
            var spurt = Spurt(sw);
            if (spurt == null) yield break;
            bool right = Bool("Shockwave Going Right")?.Value ?? true;
            float dir = right ? 1f : -1f;
            float floorY = GroundBelow((Vector2)sw.transform.position + Vector2.up * 0.5f);
            if (Champion)
            {
                // A second wave the other way: a copy of the game's, driven from his feet.
                var twin = Object.Instantiate(sw, sw.transform.position, sw.transform.rotation);
                Run(Drive(twin, spurt, -dir, floorY, true));
            }
            yield return Drive(sw, spurt, dir, floorY, false);
        }

        /// <summary>The wave's spurt: its right-hand one (the left one's own script turns it to face left; the drive turns each itself).</summary>
        static GameObject Spurt(GameObject wave)
        {
            foreach (var f in wave.GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName != "shockwave") continue;
                var right = f.Fsm.GetState("Right");
                if (right != null)
                    foreach (var a in right.Actions)
                        if (a is HutongGames.PlayMaker.Actions.SetGameObject s && s.gameObject?.Value != null) return s.gameObject.Value;
                return f.FsmVariables.FindFsmGameObject("Shockwave Object")?.Value;
            }
            return null;
        }

        /// <summary>The wave's own script still running it along the floor (not stopped at the wall, nor ended).</summary>
        static string WaveState(PlayMakerFSM[] fsms)
        {
            foreach (var f in fsms) if (f != null && f.FsmName == "shockwave") return f.ActiveStateName;
            return null;
        }

        /// <summary>
        /// A ground wave on to the wall, up it, along the ceiling to the far wall, where it goes. Its own script stops while
        /// it is driven; the game's wave goes back to its pool after.
        /// </summary>
        IEnumerator Drive(GameObject wave, GameObject spurt, float dir, float floorY, bool copy)
        {
            var fsms = wave.GetComponents<PlayMakerFSM>();
            var body = wave.GetComponent<Rigidbody2D>();
            float lift = Mathf.Clamp(wave.transform.position.y - floorY, -0.5f, 1.5f);
            float speed = Champion ? 24f : 22f;
            // Along the floor the game's own wave runs itself until close to the wall or stopped at it (its WALL); a wave
            // ended another way (off the floor's edge) is left to end; a copy is driven from the start.
            if (!copy)
            {
                float wallX = WallTowards(new Vector2(wave.transform.position.x, floorY + 0.6f), dir, 80f);
                float until = Time.time + 4f;
                while (wave != null && wave.activeInHierarchy && Time.time < until && Mathf.Abs(wave.transform.position.x - wallX) > 1.2f)
                {
                    string s = WaveState(fsms);
                    if (s != null && s != "Pause" && s != "Start" && s != "Right" && s != "Left" && s != "Start Move" && s != "Move") break;
                    if (body != null && Mathf.Abs(body.linearVelocity.x) > 5f) speed = Mathf.Abs(body.linearVelocity.x);
                    yield return null;
                }
                if (wave == null || !wave.activeInHierarchy) yield break;
                // Stopped by its own script: at a wall (a battle gate, the left wall), or at the arena's side, it is taken
                // on up from there; ended anywhere else (off the floor's edge) it is left to end.
                string end = WaveState(fsms);
                if (end == "Recycle") yield break;
                if (end == "End Particle")
                {
                    Vector2 p = new Vector2(wave.transform.position.x, floorY + 0.6f);
                    bool atWall = Hostiles.NativeRay(p, new Vector2(dir, 0f), 3f).collider != null
                        || (p.x - (dir < 0f ? Arena.xMin + 3f : Arena.xMax - 3f)) * dir >= 0f;
                    if (!atWall) yield break;
                }
            }
            var baseScale = wave.transform.localScale;
            var baseRotation = wave.transform.rotation;
            var bodyType = body != null ? body.bodyType : RigidbodyType2D.Dynamic;
            var wasOn = new bool[fsms.Length];
            for (int i = 0; i < fsms.Length; i++) wasOn[i] = fsms[i] != null && fsms[i].enabled;
            if (!copy) driven[fsms] = wasOn;
            foreach (var f in fsms) if (f != null) f.enabled = false;
            if (body != null) { body.linearVelocity = Vector2.zero; body.bodyType = RigidbodyType2D.Kinematic; }
            // However it ends (recycled by something else on the way, the boss gone), the pooled wave gets its script back.
            try
            {
                yield return Climbing(wave, spurt, dir, speed, floorY, lift);
            }
            finally
            {
                if (!copy) driven.Remove(fsms);
                if (wave != null && !copy)
                {
                    wave.transform.rotation = baseRotation;
                    wave.transform.localScale = baseScale;
                    if (body != null) body.bodyType = bodyType;
                    for (int i = 0; i < fsms.Length; i++) if (fsms[i] != null) fsms[i].enabled = wasOn[i];
                    if (wave.activeInHierarchy) ObjectPool.Recycle(wave);
                }
                else if (wave != null) Object.Destroy(wave);
            }
        }

        /// <summary>
        /// The drive: along the rock's face (its base on the rock, the spurts out of it), round each inside corner onto the
        /// next face (the wall, then the ceiling), gone at the third (the far wall). Up the wall and along the ceiling it
        /// goes on over a gap in the face (a battle gate shorter than the wall, the hole of a broken wall, a hole in the
        /// ceiling), unseen there; with no wall at all at the floor's end (the left wall broken and its gate open) the arena's
        /// side is its wall.
        /// </summary>
        IEnumerator Climbing(GameObject wave, GameObject spurt, float dir, float speed, float floorY, float lift)
        {
            var t = wave.transform;
            float z = t.position.z;
            Vector2 along = new Vector2(dir, 0f), up = Vector2.up;
            Vector2 at = new Vector2(t.position.x, floorY);
            const float Gap = 0.35f;
            float sinceSpurt = Gap;
            int turns = 0;
            float until = Time.time + 6f;
            var room = new Rect(Arena.xMin - 2f, Arena.yMin - 2f, Arena.width + 4f, Arena.height + 4f);
            float side = dir < 0f ? Arena.xMin + 1f : Arena.xMax - 1f;
            while (wave != null && wave.activeInHierarchy && Time.time < until)
            {
                float step = speed * Time.deltaTime;
                // A face ahead (the wall at the floor's end, the ceiling at the wall's top): round onto it. Along the floor,
                // the arena's side counts as the wall where the room has none.
                var ahead = Hostiles.NativeRay(at + up * 0.5f, along, step + 0.5f);
                bool edge = ahead.collider == null && turns == 0 && (at.x - side) * dir >= 0f;
                bool onFace = true;
                if (ahead.collider != null || edge)
                {
                    if (ahead.collider != null) sinceSpurt += Mathf.Max(0f, ahead.distance - 0.5f);
                    if (++turns > 2) break;
                    if (ahead.collider != null) at = ahead.point;
                    var was = along;
                    along = up;
                    up = -was;
                }
                else
                {
                    at += along * step;
                    sinceSpurt += step;
                    // Kept on the face (a bump, a step in it). Along the floor a gap ends it (the floor's edge); up the wall
                    // and along the ceiling it goes on straight over one, unseen, while it is in the room.
                    var face = Hostiles.NativeRay(at + up * 1.5f, -up, 3f);
                    if (face.collider != null) at = face.point;
                    else if (turns == 0 || !room.Contains(at)) break;
                    else onFace = false;
                }
                var turn = Turn(along, up);
                t.position = new Vector3(at.x + up.x * lift, at.y + up.y * lift, z);
                t.rotation = Quaternion.FromToRotation(Vector3.up, up);
                if (sinceSpurt >= Gap && onFace)
                {
                    sinceSpurt = 0f;
                    ObjectPool.Spawn(spurt, t.position, turn);
                }
                yield return null;
            }
            DevWaveEnds[dir < 0f ? 0 : 1, Mathf.Min(turns, 3)]++;
            // Gone at the far wall (Drive's finally: the copy destroyed, the game's own put back and returned to its pool).
        }

        /// <summary>Dev: how far each driven wave got, by its way (left, right) and the faces it turned onto (3: the far wall).</summary>
        internal static readonly int[,] DevWaveEnds = new int[2, 4];

        /// <summary>
        /// A spurt's turn for a face: its top out of the rock, its front the way the wave runs (the art faces right; a
        /// left-running wave's spurt is turned over about its upright, as the game's left one is).
        /// </summary>
        static Quaternion Turn(Vector2 along, Vector2 up)
        {
            if (along.x * up.y - along.y * up.x > 0f) return Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            return Quaternion.Euler(0f, 180f, Mathf.Atan2(along.y, -along.x) * Mathf.Rad2Deg);
        }

        /// <summary>The scripts of the game's waves being driven now (given back if he goes mid-way: they are the pool's).</summary>
        readonly System.Collections.Generic.Dictionary<PlayMakerFSM[], bool[]> driven = new System.Collections.Generic.Dictionary<PlayMakerFSM[], bool[]>();

        protected override void Teardown()
        {
            foreach (var entry in driven)
                for (int i = 0; i < entry.Key.Length; i++)
                    if (entry.Key[i] != null) entry.Key[i].enabled = entry.Value[i];
            driven.Clear();
        }

        // ------------------------------------------------------------------ the slam's shake

        /// <summary>The jump slam's landing: hooks shaken loose, down through the platforms.</summary>
        void Shake()
        {
            if (!TargetLocal) return;
            ShakeOffHooks();
            Player.TerraMovement.ShakeOffPlatforms(0.35f);
        }

        // ------------------------------------------------------------------ the barrels

        /// <summary>The barrel summoner (the battle scene's, found by his script): every barrel dropped on the player, the rage's halved.</summary>
        IEnumerator Barrels()
        {
            PlayMakerFSM summon = null;
            for (int i = 0; i < 1200 && summon == null; i++)
            {
                var go = Go("Barrel Summoner")?.Value;
                if (go != null) foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == "summon") summon = f;
                if (summon == null) yield return null;
            }
            if (summon == null) yield break;
            // The rage asks for 8 (R Attack Antic), a slam for 2 to 4: the rage's halved.
            OnEnter("Determine Spawns", () =>
            {
                var n = Int("Spawns", summon);
                if (n != null && n.Value >= 6) n.Value = Mathf.CeilToInt(n.Value / 2f);
            }, true, summon);
            At("Spawn", 1, () =>
            {
                var x = Float("Spawn X", summon);
                if (x == null) return;
                float min = Float("Summon Min", summon)?.Value ?? Arena.xMin, max = Float("Summon Max", summon)?.Value ?? Arena.xMax;
                x.Value = Mathf.Clamp(Lock().x, min, max);
                MarkGround(new Vector2(x.Value, TargetFeet.y + 0.3f), 3f, 0.8f);
            }, summon);
            // A little more room between them, now they all come at the player.
            TempoFixed(summon, "Spawn", 1.5f);
        }

        // ------------------------------------------------------------------ the left wall

        PlayMakerFSM wall;
        Transform gate;
        bool beaten;

        /// <summary>
        /// Crossroads_10's left wall ("Fk Break Wall"): the game puts up its breakable part at his rage's end (FK RAGE
        /// COMPLETE), and that part's break takes the left battle gate away; he went through the hole after the player and
        /// stuck in it. While he fights it takes no hits (the game's own way after his fall through the floor).
        /// </summary>
        void HoldWall()
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "Control" || f.gameObject.name != "Fk Break Wall") continue;
                wall = f;
                foreach (var t in f.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Battle Gate 1") gate = t;
                break;
            }
            if (wall == null) return;
            Run(GuardBreakable());
        }

        /// <summary>The breakable part, once up and its script started: a hit on it goes back to waiting while he fights.</summary>
        IEnumerator GuardBreakable()
        {
            while (true)
            {
                var b = Go("Breakable", wall)?.Value;
                if (b != null && b.activeInHierarchy)
                {
                    yield return null;
                    foreach (var f in b.GetComponents<PlayMakerFSM>())
                        if (f.FsmName == "Break") Branch("Hit Left", "Idle", () => !beaten, f);
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary>The wall broken before (its gate gone or open): he keeps to the arena's side of where the gate stands.</summary>
        void KeepIn()
        {
            if (gate == null || Down || !Engaged || body == null) return;
            var col = gate.GetComponent<Collider2D>();
            if (gate.gameObject.activeInHierarchy && col != null && col.enabled) return;
            float inside = Mathf.Sign(Arena.center.x - gate.position.x);
            float edge = gate.position.x + inside * (body.bounds.extents.x + 0.5f);
            float over = (edge - Me.x) * inside;
            if (over <= 0f) return;
            // A missing gate must not turn a charge past its edge into a jump back across the room.
            float step = Mathf.Min(over, 12f * Mathf.Clamp(Time.deltaTime, 0f, 0.05f));
            transform.position += Vector3.right * (step * inside);
            if (rb != null && rb.linearVelocity.x * inside < 0f) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }
}
