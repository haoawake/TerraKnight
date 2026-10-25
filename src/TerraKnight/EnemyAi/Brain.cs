using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// One enemy's new AI (World.EnemyAiMode New): it sees, hears and remembers the player, leads its own script's aim,
    /// and adds its kind's moves (<see cref="Profiles"/>). A move that moves the enemy takes its body for the move's time:
    /// its PlayMaker FSMs wait (Fsm.ManualUpdate, and their fixed and late updates held by <see cref="EnemyAi"/>), its
    /// walking script waits, and everything is given back as it was. A move is started only while its own script is
    /// walking, flying about or waiting, never in one of its own attacks. Anything going wrong gives the enemy back to its
    /// own script for good (logged once).
    /// </summary>
    internal sealed class Brain : MonoBehaviour, IAim
    {
        const float Tile = 0.5f;
        enum Phase { None, Antic, Act, Recover }

        HealthManager hm;
        Items.EnemyStats.Info info;
        Profile prof;
        Rigidbody2D rb;
        Collider2D body;
        tk2dSpriteAnimator anim;
        SpriteFlash flash;
        PlayMakerFSM[] fsms, controls;
        /// <summary>Each move's script for its own attack (Kind.Own), by the move's index.</summary>
        PlayMakerFSM[] ownFsms;
        int animId;
        Behaviour[] movers;
        Climber climber;
        string scene;

        // The target.
        public bool HasTarget { get; private set; }
        public Vector2 TargetCenter { get; private set; }
        public Vector2 TargetVelocity { get; private set; }
        Vector2 lastPos;
        float lastPosAt = -1f;
        bool targetGrounded, targetHooked;
        public bool Alert { get; private set; }
        float lastSeenAt = -100f, wokeAt = -100f, lastAlarmAt = -100f;
        bool sees, knows;

        // Moves.
        float[] readyAt;
        float nextMoveAt;
        Move cur;
        int curIndex = -1;
        Phase phase;
        float phaseEnd, actStart, nextShotAt, nextDustAt;
        int shotsLeft, repeatsLeft, pillarIndex;
        Vector2 aimPoint, moveDir, launchAt;
        bool landed, hidden, approached;
        bool token;
        int lastHp;
        float struckAt = -10f;
        /// <summary>A FarStruck enemy hit until then still owes its answer (a knockback may have held it off).</summary>
        float chargeUntil = -10f;
        Items.KnockFlight knock;

        // What a takeover changes.
        bool seized;
        readonly List<Fsm> paused = new List<Fsm>();
        bool[] moverOn;
        float savedGravity;
        RigidbodyType2D savedType;
        Vector3 savedScale;
        Vector2 savedVelocity;
        string savedClip;
        Renderer[] renderers;
        bool[] rendererOn;
        Collider2D[] colliders;
        bool[] colliderOn;

        bool failed;
        GameObject aimProxy;
        PlayMakerFSM blockerControl;

        public static int DevBrains, DevMoves, DevFails, DevSeizes, DevOwnShots, DevOwnMoves;

        /// <summary>
        /// Its own shot (the user's, 2026-10-23: the enemies use only their own pictures, as the bosses do): the first
        /// thing its own scripts spawn or fling that hurts the player (a DamageHero on it), a state that fires, spits or
        /// throws first; null when it has none (its shots are then not used, nor the areas the mod drew).
        /// </summary>
        GameObject ownShot;
        bool ownShotFire;
        float ownShotGravity;

        static readonly HashSet<string> ownShotLogged = new HashSet<string>();

        static GameObject FindOwnShot(PlayMakerFSM[] fsms, GameObject self, out string where)
        {
            where = null;
            GameObject first = null;
            string firstWhere = null;
            foreach (var f in fsms)
            {
                if (f == null || f.Fsm == null || f.FsmName == "damages_hero") continue;
                // What its corpse lets out (an Aspid Mother's hatchlings, an Ooma's burst) is no shot of its own.
                if (f.FsmName.IndexOf("corpse", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || f.gameObject.name.IndexOf("corpse", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                foreach (var st in f.FsmStates)
                {
                    string sn = st.Name.ToLowerInvariant();
                    if (sn.Contains("death") || sn.Contains("die") || sn.Contains("dead") || sn.Contains("explo") || sn.Contains("birth")) continue;
                    var go = Bosses.OwnShots.Of(st);
                    if (go == null || go == self || go.GetComponentInChildren<DamageHero>(true) == null) continue;
                    string n = st.Name.ToLowerInvariant();
                    bool fires = n.Contains("fire") || n.Contains("shoot") || n.Contains("shot") || n.Contains("spit") || n.Contains("throw")
                                 || n.Contains("attack") || n.Contains("bullet");
                    if (fires) { where = f.FsmName + "." + st.Name; return go; }
                    if (first == null) { first = go; firstWhere = f.FsmName + "." + st.Name; }
                }
            }
            where = firstWhere;
            return first;
        }

        /// <summary>A shot of its own that waits to be sent (its script listens for FIRE, as the soul orbs do).</summary>
        static bool WaitsForFire(GameObject prefab)
        {
            foreach (var f in prefab.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f != null && f.Fsm != null)
                    foreach (var e in f.FsmEvents) if (e != null && e.Name == "FIRE") return true;
            return false;
        }

        /// <summary>A move the mod drew (its shots with none of the enemy's own, the areas): never used.</summary>
        bool Drawn(Move m)
        {
            switch (m.Kind)
            {
                case Kind.Shot: return ownShot == null;
                case Kind.Wave: return blockerControl == null;
                case Kind.Pulse: case Kind.Pillar: case Kind.Cloud: case Kind.Pull: case Kind.Beam: return true;
                default: return false;
            }
        }
        public string DevState => failed ? "failed" : cur == null ? (Alert ? "alert" : "calm") : cur.Kind + ":" + phase;

        public void Init(HealthManager h, Items.EnemyStats.Info i, Profile p)
        {
            hm = h;
            info = i;
            prof = p;
            scene = h.gameObject.scene.name;
            rb = h.GetComponent<Rigidbody2D>();
            body = h.GetComponent<Collider2D>();
            anim = h.GetComponent<tk2dSpriteAnimator>();
            flash = h.GetComponent<SpriteFlash>();
            climber = h.GetComponent<Climber>();
            fsms = h.GetComponentsInChildren<PlayMakerFSM>(true);
            var c = new List<PlayMakerFSM>();
            foreach (var f in fsms)
            {
                if (f == null || f.FsmName == "damages_hero") continue;
                string n = f.FsmName.ToLowerInvariant();
                if (f.gameObject == h.gameObject || n.Contains("control") || n.Contains("attack") || n.Contains("chase") || n.Contains("move") || n.Contains("behav") || n.Contains("ai"))
                    c.Add(f);
            }
            controls = c.ToArray();
            var m = new List<Behaviour>();
            foreach (var b in h.GetComponents<Behaviour>())
                if (b is Walker || b is Climber || b is Crawler) m.Add(b);
            movers = m.ToArray();
            moverOn = new bool[movers.Length];
            facesRight = p.FacesRight;
            var walker = h.GetComponent<Walker>();
            if (walker != null && walkerRight != null) facesRight = (float)walkerRight.GetValue(walker) > 0f;
            else if (SpriteFacesRight(fsms) is bool own) facesRight = own;
            // The Elder Baldur's own spit aimed at the player, its wave of that goop (BlockerSpit).
            if (p.GoopSpit)
            {
                blockerControl = BlockerSpit.Control(fsms);
                BlockerSpit.Hook(this, blockerControl);
            }
            ownShot = FindOwnShot(fsms, h.gameObject, out string ownWhere);
            if (ownShot != null)
            {
                ownShotFire = WaitsForFire(ownShot);
                var srb = ownShot.GetComponent<Rigidbody2D>();
                ownShotGravity = srb != null && srb.bodyType == RigidbodyType2D.Dynamic ? Mathf.Abs(Physics2D.gravity.y) * srb.gravityScale : 0f;
            }
            if (ownShotLogged.Add(i?.Record ?? h.gameObject.name))
            {
                int drawn = 0, left = 0;
                foreach (var mv in p.Moves) if (Drawn(mv)) drawn++; else left++;
                Plugin.Log.LogInfo($"[小怪招式] {i?.Record ?? h.gameObject.name}：自己的弹幕 {(ownShot != null ? ownShot.name + "（" + ownWhere + (ownShotFire ? "，等 FIRE" : "") + "）" : "无")}；mod 画的招式停用 {drawn} 个，剩 {left} 个新招");
            }
            ownFsms = new PlayMakerFSM[p.Moves.Count];
            for (int k = 0; k < p.Moves.Count; k++)
            {
                var mv = p.Moves[k];
                if (mv.Kind != Kind.Own) continue;
                foreach (var f in fsms)
                    if (f != null && (mv.OwnFsm == null || f.FsmName == mv.OwnFsm) && f.Fsm.GetState(mv.OwnState) != null) { ownFsms[k] = f; break; }
            }
            if (anim != null) { animId = anim.GetInstanceID(); EnemyAi.QuickAnimators.Add(animId); }
            readyAt = new float[p.Moves.Count];
            whyNot = new string[p.Moves.Count];
            // Not all at once: each move first comes a little after the enemy is met.
            for (int k = 0; k < readyAt.Length; k++) readyAt[k] = Time.time + Random.Range(0.6f, 1.6f);
            lastHp = h.hp;
            DevBrains++;
        }

        void Fail(System.Exception e)
        {
            if (failed) return;
            failed = true;
            DevFails++;
            try { EndShell(); } catch (System.Exception) { }
            try { if (hidden) Hide(false); if (dropping) DropEnd(); } catch (System.Exception) { }
            try { Release(); } catch (System.Exception) { }
            RestoreAim();
            if (token) { EnemyAi.ReturnToken(this); token = false; }
            Plugin.Log.LogWarning($"[敌人AI] {info?.Record} 新版AI出错，退回原版：{e}");
            enabled = false;
        }

        void OnDestroy()
        {
            if (animId != 0) EnemyAi.QuickAnimators.Remove(animId);
            if (token) EnemyAi.ReturnToken(this);
            try { if (seized) Release(); } catch (System.Exception) { }
            RestoreAim();
        }

        void OnDisable()
        {
            // Deactivated (a scene's enemies put away, a death): everything given back.
            if (failed) return;
            try { if (shelled) EndShell(); } catch (System.Exception) { }
            try { if (cur != null) End(false); } catch (System.Exception) { }
            RestoreAim();
            Leading = false;
            HasTarget = false;
            lastPosAt = -1f;
            TargetVelocity = Vector2.zero;
        }

        void Update()
        {
            if (failed) return;
            try { Think(); }
            catch (System.Exception e) { Fail(e); }
        }

        void FixedUpdate()
        {
            if (failed || cur == null) return;
            try { Step(Time.fixedDeltaTime); }
            catch (System.Exception e) { Fail(e); }
        }

        // ------------------------------------------------------------------ seeing the player

        bool Calm()
        {
            var pd = PlayerData.instance;
            if (pd == null) return false;
            if (prof.Mantis && pd.GetBool("defeatedMantisLords") && !World.EnemyAggro.IsAngry(hm)) return true;
            if (prof.Sibling && pd.GetBool("gotShadeCharm")) return true;
            return false;
        }

        /// <summary>A LAN game whose players share a scene's enemies (Net.Enemies): Net.EnemyTargets picks whom each goes for.</summary>
        static bool NetSync => Net.NetSession.Active && Net.Enemies.Enabled;

        /// <summary>The player it goes for is this game's own (else another LAN player).</summary>
        public bool TargetLocal { get; private set; } = true;
        public int TargetSlot { get; private set; } = -1;

        void Track()
        {
            HasTarget = false;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            Vector2 at = Vector2.zero;
            TargetLocal = true;
            if (NetSync)
            {
                if (!Net.EnemyTargets.Where(hm, out at, out int slot)) return;
                TargetSlot = slot;
                TargetLocal = slot == Net.NetSession.MySlot;
            }
            if (TargetLocal && hero.cState.dead) return;
            if (!TargetLocal)
            {
                targetGrounded = Physics2D.Raycast(at, Vector2.down, 1.4f, Hostiles.TerrainMask).collider != null;
                targetHooked = false;
            }
            else
            {
                var hc = hero.GetComponent<Collider2D>();
                at = hc != null ? (Vector2)hc.bounds.center : (Vector2)hero.transform.position;
                targetGrounded = hero.cState.onGround;
                targetHooked = Player.GrapplingHook.IsLatched;
            }
            HasTarget = true;
            float now = Time.time;
            if (lastPosAt > 0f && now - lastPosAt > 0.0001f)
            {
                var v = (at - lastPos) / (now - lastPosAt);
                // A teleport or a respawn is no speed.
                if (v.magnitude > 80f) v = Vector2.zero;
                TargetVelocity = Vector2.Lerp(TargetVelocity, v, 0.35f);
                // Another player's dash, seen by its speed (a dash is far quicker than any run or flight): when it ends.
                if (!TargetLocal)
                {
                    float speed = v.magnitude;
                    if (speed > 24f) remoteDashing = now;
                    else if (speed < 13f && now - remoteDashing < 0.3f && remoteDashing > remoteDashEnded) remoteDashEnded = now;
                }
            }
            lastPos = at;
            lastPosAt = now;
            TargetCenter = at;
        }

        float remoteDashing = -10f, remoteDashEnded = -10f;

        Vector2 Me => body != null && body.enabled ? (Vector2)body.bounds.center : (Vector2)transform.position;

        bool Sight(Vector2 to)
        {
            Vector2 from = Me;
            Vector2 d = to - from;
            return Physics2D.Raycast(from, d.normalized, d.magnitude, Hostiles.TerrainMask).collider == null;
        }

        void Think()
        {
            if (shelled && (hm == null || hm.isDead || !World.EnemyAiMode.New || Net.Enemies.IsPuppet(hm))) EndShell();
            if (hm == null || hm.isDead)
            {
                if (cur != null) End(false);
                RestoreAim();
                enabled = false;
                return;
            }
            if (!World.EnemyAiMode.New || Net.Enemies.IsPuppet(hm))
            {
                if (cur != null) End(false);
                RestoreAim();
                Leading = false;
                return;
            }
            Track();
            bool calm = Calm();
            if (!HasTarget || calm)
            {
                Alert = false;
                if (shelled) EndShell();
                if (cur != null) End(false);
                lastHp = hm.hp;
                lastPosAt = -1f;
                TargetVelocity = Vector2.zero;
                LeadAim(false);
                return;
            }
            float now = Time.time;
            float d = Vector2.Distance(Me, TargetCenter);
            bool struck = hm.hp < lastHp;
            lastHp = hm.hp;
            if (struck) struckAt = now;
            if (struck && prof.FarStruck) chargeUntil = now + 2.5f;
            // The Crystal Crawler's crystal turned to the blows (prof.Reflect): held, then given back.
            if (shelled)
            {
                ShellTick(now);
                return;
            }
            if (struck && prof.Reflect && cur == null && now >= shellReadyAt && !KnockedBack() && Random.value < EnemyAi.ReflectChance && ShellFits())
            {
                StartShell(now);
                return;
            }
            bool stealthy = Items.ArmorEffects.VortexStealth || Items.Equipment.Current.Invisible;
            float sight = prof.Sight * Tile * (stealthy ? 0.4f : 1f);
            sees = d <= sight && Sight(TargetCenter);
            bool hears = TerraMod.Instance != null && TerraMod.Instance.IsUsingItem && d <= prof.Hear * Tile && !stealthy;
            if (sees || struck || hears || World.EnemyAggro.IsAngry(hm))
            {
                if (!Alert) Wake(now);
                if (sees || struck) lastSeenAt = now;
                else if (hears) lastSeenAt = Mathf.Max(lastSeenAt, now - prof.Memory * 0.5f);
            }
            if (Alert && now - lastSeenAt > prof.Memory && !World.EnemyAggro.IsAngry(hm)) Alert = false;
            // Woken (struck, a cry, its own sight), it knows where the player is while nothing stands between them: a
            // kind that sees nothing (passive until struck) turns on the player then.
            knows = sees || Alert && !stealthy && Sight(TargetCenter);
            // Its own script woken while it hunts (EnemyAggro: its waking ranges and sight see the player).
            if (Alert && now - wokeAt > 2f && (sees || struck))
            {
                wokeAt = now;
                if (!prof.KeepWake) World.EnemyAggro.Struck(hm, info);
            }
            // A boss fighting in the room: its summons and the room's others aim as their own scripts do.
            LeadAim(Alert && !EnemyAi.BossNear(scene));
            if (cur != null)
            {
                Tick(now);
                return;
            }
            if (!Alert) return;
            Choose(now, struck || prof.FarStruck && now < chargeUntil);
        }

        void Wake(float now)
        {
            Alert = true;
            lastSeenAt = now;
            wokeAt = now;
            if (!prof.KeepWake) World.EnemyAggro.Struck(hm, info);
            if (prof.Alarm > 0f && now - lastAlarmAt > 4f)
            {
                lastAlarmAt = now;
                EnemyAi.Alarm(this, prof.Alarm * Tile);
            }
        }

        /// <summary>Another enemy's cry: it knows where the player is (a little late).</summary>
        public void Hear(Vector2 from)
        {
            if (Alert || failed || Calm()) return;
            Alert = true;
            lastSeenAt = Time.time;
            wokeAt = Time.time;
            nextMoveAt = Mathf.Max(nextMoveAt, Time.time + Random.Range(0.3f, 0.6f));
            if (!prof.KeepWake) World.EnemyAggro.Struck(hm, info);
        }

        public string Record => info?.Record;

        /// <summary>
        /// What its body's touch carries now: its running move's debuff, knock-off and hook cut, else its kind's touch
        /// debuff (sent with its hitbox to a LAN game's others: NetAi).
        /// </summary>
        public bool TouchFlags(out int debuff, out int ticks, out bool heavy, out bool cut)
        {
            debuff = 0; ticks = 0; heavy = false; cut = false;
            if (failed || prof == null || !isActiveAndEnabled || !World.EnemyAiMode.New || Net.Enemies.IsPuppet(hm)) return false;
            if (prof.TouchDebuff > 0 && (int)prof.TouchTier <= (int)EnemyAi.TierNow)
            {
                debuff = prof.TouchDebuff; ticks = prof.TouchTicks;
            }
            if (cur != null && phase == Phase.Act && Moves(cur.Kind))
            {
                if (cur.Debuff > 0) { debuff = cur.Debuff; ticks = cur.DebuffTicks; }
                heavy = cur.Heavy && EnemyAi.MasterRules;
                cut = cur.CutHook && EnemyAi.MasterRules;
            }
            return debuff > 0 || heavy || cut;
        }

        /// <summary>The enemy's own body (or one of its own attacks) just hurt the player: its touch's debuff, or its move's.</summary>
        public void HurtPlayer()
        {
            // A brain never set up (a copy's, before Init): nothing of its own to put on the player.
            if (TouchFlags(out int debuff, out int ticks, out _, out _) && debuff > 0) PlayerDebuffs.Apply(debuff, ticks);
        }
        public string Scene => scene;
        public Vector2 Position => Me;

        // ------------------------------------------------------------------ leading its own aim

        /// <summary>
        /// Single player: the enemy's own script, wherever it looks for the Knight, finds a point ahead of him by its lead
        /// (Net.EnemyTargets does the same for LAN games' other players): its spit, its leap and its chase go where the
        /// player is going.
        /// </summary>
        /// <summary>The lead its own script's aim takes now (Net.EnemyTargets puts its stand-in this far ahead of the player).</summary>
        public bool Leading { get; private set; }
        public Vector2 LeadOffset { get; private set; }

        void LeadAim(bool on)
        {
            Leading = false;
            if (!on || prof.AimLead <= 0f)
            {
                RestoreAim();
                return;
            }
            if (on)
            {
                var lead = TargetVelocity * prof.AimLead * EnemyAi.LeadWeight;
                if (lead.magnitude > 6f) lead = lead.normalized * 6f;
                var hit = Physics2D.Raycast(TargetCenter, lead.normalized, lead.magnitude, Hostiles.TerrainMask);
                LeadOffset = hit.collider != null ? lead.normalized * Mathf.Max(0f, hit.distance - 0.3f) : lead;
                Leading = true;
            }
            if (NetSync)
            {
                RestoreAim();
                return;
            }
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            if (aimProxy == null)
            {
                aimProxy = new GameObject("TK AI Aim");
                if (hm.gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(aimProxy, hm.gameObject.scene);
                EnemyAi.Proxies[hm] = aimProxy;
            }
            Vector2 at = TargetCenter + (on ? LeadOffset : Vector2.zero);
            aimProxy.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
            var heroGo = hero.gameObject;
            foreach (var f in fsms)
            {
                if (f == null) continue;
                foreach (var v in f.FsmVariables.GameObjectVariables)
                    if (v.Value == heroGo) v.Value = aimProxy;
            }
        }

        void RestoreAim()
        {
            if (aimProxy == null) return;
            var hero = HeroController.SilentInstance;
            if (fsms != null)
                foreach (var f in fsms)
                {
                    if (f == null) continue;
                    foreach (var v in f.FsmVariables.GameObjectVariables)
                        if (v.Value == aimProxy) v.Value = hero != null ? hero.gameObject : null;
                }
            if (hm != null) EnemyAi.Proxies.Remove(hm);
            Destroy(aimProxy);
            aimProxy = null;
        }

        // ------------------------------------------------------------------ choosing a move

        static readonly string[] busyWords =
        {
            "attack", "antic", "lunge", "shoot", "fire", "spit", "slash", "swipe", "charge", "dash", "leap", "jump", "land", "stun",
            "recoil", "death", "dead", "die", "hit", "block", "shell", "burrow", "emerge", "throw", "roar", "scream", "dig", "fall",
            "spawn", "hatch", "summon", "cast", "laser", "beam", "explode", "burst", "inflate", "bounce", "roll", "dive", "swoop",
            "strike", "stab", "pounce", "grab", "eat", "bite", "jab", "punch", "sweep", "spin", "teleport", "appear", "vanish",
            "wake", "sleep", "hide", "dormant", "inert", "init", "flip", "drop", "slam", "stomp", "shield", "guard", "parry", "counter",
        };

        /// <summary>Its own script is in one of its own attacks (or asleep, hidden, starting): nothing that takes its body now.</summary>
        bool Busy()
        {
            foreach (var f in controls)
            {
                if (f == null || !f.enabled) continue;
                string s = f.ActiveStateName;
                if (string.IsNullOrEmpty(s)) continue;
                if (!busyStates.TryGetValue(s, out bool busy))
                {
                    busy = false;
                    foreach (var w in busyWords)
                        if (s.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) { busy = true; break; }
                    busy &= !stances.Contains(s);
                    busyStates[s] = busy;
                }
                if (busy) { busyState = s; return true; }
            }
            return false;
        }

        string busyState;

        /// <summary>
        /// How they stand and go, though named like an attack: a shield held up as it walks (Husk Sentries, Moss Knights, the
        /// Shielded Fool; the Slash Spider's guarded sides), a Baldur's roll, a look round. Busy, they never took a move.
        /// </summary>
        static readonly HashSet<string> stances = new HashSet<string>
        {
            "Shield Left High", "Shield Left Low", "Shield Right High", "Shield Right Low",
            "L Shielded", "R Shielded", "L Unshielded", "R Unshielded", "Roll L", "Roll R", "Raycast",
        };

        /// <summary>Its own script standing in one of the stances (a shield up as it walks).</summary>
        bool Stanced()
        {
            foreach (var f in controls)
                if (f != null && f.enabled && stances.Contains(f.ActiveStateName ?? "")) return true;
            return false;
        }

        /// <summary>State names judged busy or not (no new strings each frame: the game runs with its collector off).</summary>
        static readonly Dictionary<string, bool> busyStates = new Dictionary<string, bool>();

        static bool Moves(Kind k) => k <= Kind.Drop;

        bool OnScreen()
        {
            // Another player's view: about a screen round him (Terraria's NPCs act within their player's range).
            if (!TargetLocal) return Mathf.Abs(Me.x - TargetCenter.x) < 15f && Mathf.Abs(Me.y - TargetCenter.y) < 9f;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(transform.position);
            return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        bool Allowed(Move m, int i, float now, float d, bool struck, bool postDash)
        {
            if ((int)m.Tier > (int)EnemyAi.TierNow) return No(i, "tier");
            if (Drawn(m)) return No(i, "drawn");
            if (now < readyAt[i]) return No(i, "cooldown");
            if (m.When == When.Struck) { if (!struck) return No(i, "when"); }
            else if (m.When == When.PostDash) { if (!postDash) return No(i, "when"); }
            else if (struck && now < nextMoveAt) return No(i, "gap");
            // The body's charges, leaps, dives and chases reach further on the hard difficulties (EnemyAi.ReachScale).
            float reach = m.Kind == Kind.Lunge || m.Kind == Kind.Leap || m.Kind == Kind.Dive || m.Kind == Kind.Chase ? EnemyAi.ReachScale : 1f;
            if (d < m.Min * Tile || d > m.Max * Tile * reach) return No(i, "range");
            if (m.Sight && !knows) return No(i, "sight");
            float dy = TargetCenter.y - Me.y;
            switch (m.Rel)
            {
                case Rel.Above: if (dy < 1.4f) return No(i, "rel"); break;
                case Rel.Below: if (dy > -1.4f) return No(i, "rel"); break;
                case Rel.Level: if (Mathf.Abs(dy) > 1.6f) return No(i, "rel"); break;
            }
            switch (m.When)
            {
                case When.Air: if (targetGrounded) return No(i, "when"); break;
                case When.Ground: if (!targetGrounded) return No(i, "when"); break;
                case When.Hooked: if (!targetHooked) return No(i, "when"); break;
                case When.Close: if (d > 3f * Tile) return No(i, "when"); break;
            }
            if (m.Kind == Kind.Own)
            {
                var f = ownFsms[i];
                if (f == null) return No(i, "noown");
                if (!f.isActiveAndEnabled || EnemyAi.Paused.Contains(f.Fsm)) return No(i, "off");
                string st = f.ActiveStateName;
                if (m.OwnFrom != null) { if (System.Array.IndexOf(m.OwnFrom, st) < 0) return No(i, "from"); }
                else if (Busy()) return No(i, busyState);
            }
            if (Moves(m.Kind))
            {
                // Invincible all round (dug in, hidden, waking) it waits; a shield's one side (InvincibleFromDirection) doesn't.
                if (rb == null) return No(i, "body");
                // Its body not there yet (a Mantis Traitor before its wave, a spawn still waking): a move would throw it
                // through the floor.
                if (body != null && !body.enabled) return No(i, "nobody");
                // A shield held up as it walks keeps it invincible all round (a Great Husk Sentry's): that is its stance, not a wait.
                if (hm.IsInvincible && hm.InvincibleFromDirection == 0 && !Stanced()) return No(i, "invincible");
                if (Busy()) return No(i, busyState);
                if (KnockedBack()) return No(i, "knocked");
                if (m.Kind == Kind.Drop && climber == null && !CrawlerOnCeiling() && !ScriptOnCeiling()) return No(i, "climber");
                // A drop falls straight: only with the player (where he'll be as it falls) under it.
                if (m.Kind == Kind.Drop && Mathf.Abs(TargetCenter.x + TargetVelocity.x * 0.3f - Me.x) > 1.2f) return No(i, "aside");
                if (climber != null && m.Kind != Kind.Drop) return No(i, "climber");
                // A crawler on a wall or a ceiling: a body move would take it off its surface into the air (or the
                // stone); only its shots and areas.
                if (m.Kind != Kind.Drop && CrawlerOffFloor()) return No(i, "surface");
            }
            whyNot[i] = null;
            return true;
        }

        /// <summary>Dev: why each move was last turned down (the autotests read it), and why none was looked at.</summary>
        string[] whyNot;
        string chooseWhy;

        bool No(int i, string why)
        {
            whyNot[i] = why;
            return false;
        }

        readonly List<int> picks = new List<int>();

        void Choose(float now, bool struck)
        {
            // The grace after a hurt is the local player's own (another player's is his game's: its hits wait on its immunity).
            if (TargetLocal && EnemyAi.Grace) { chooseWhy = "grace"; return; }
            if (!OnScreen() && !(struck && prof.FarStruck)) { chooseWhy = "offscreen"; return; }
            if (EnemyAi.BossNear(scene)) { chooseWhy = "boss"; return; }
            float dashEnded = TargetLocal ? EnemyAi.DashEndedAt : remoteDashEnded;
            bool postDash = EnemyAi.MasterRules && now - dashEnded < 0.35f && now - dashEnded > 0.12f;
            bool reactive = struck || postDash;
            if (!reactive && now < nextMoveAt) { chooseWhy = "gap"; return; }
            chooseWhy = null;
            float d = Vector2.Distance(Me, TargetCenter);
            picks.Clear();
            float total = 0f;
            for (int i = 0; i < prof.Moves.Count; i++)
            {
                var m = prof.Moves[i];
                if (!Allowed(m, i, now, d, struck, postDash)) continue;
                // A reaction goes first when it can.
                if (reactive && (m.When == When.Struck || m.When == When.PostDash)) { picks.Clear(); picks.Add(i); total = m.Weight; break; }
                if (reactive && now < nextMoveAt) continue;
                picks.Add(i);
                total += m.Weight;
            }
            if (picks.Count == 0) return;
            float r = Random.Range(0f, total);
            int pick = picks[picks.Count - 1];
            foreach (int i in picks)
            {
                r -= prof.Moves[i].Weight;
                if (r <= 0f) { pick = i; break; }
            }
            if (!EnemyAi.TryToken(this)) { chooseWhy = "token"; return; }
            token = true;
            Begin(pick, now);
        }

        // ------------------------------------------------------------------ running a move

        void Begin(int index, float now)
        {
            cur = prof.Moves[index];
            curIndex = index;
            if (cur.When == When.Struck) chargeUntil = -10f;
            repeatsLeft = cur.Repeat;
            DevMoves++;
            EnemyAi.DevMoveCounts[cur.Kind] = EnemyAi.DevMoveCounts.TryGetValue(cur.Kind, out int n) ? n + 1 : 1;
            StartAntic(now, 1f);
        }

        void StartAntic(float now, float scale)
        {
            phase = Phase.Antic;
            float antic = Mathf.Max(EnemyAi.MinAntic, cur.Antic * EnemyAi.AnticScale * scale);
            phaseEnd = now + antic;
            if (Moves(cur.Kind) && cur.Kind != Kind.Drop)
            {
                Seize();
                if (rb != null) rb.linearVelocity = prof.Flyer ? Vector2.zero : new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
                FaceTarget();
                Play(cur.AnticClip, anticWords);
            }
            // Its wind-up is its own animation only: no tint, dust or sound of the mod's tells a move is coming (the
            // user's, 2026-10-23).
        }

        void Tick(float now)
        {
            if (phase == Phase.Antic)
            {
                if (now >= phaseEnd) StartAct(now);
                return;
            }
            if (phase == Phase.Act)
            {
                ActTick(now);
                return;
            }
            if (phase == Phase.Recover && now >= phaseEnd)
            {
                if (repeatsLeft > 0)
                {
                    if (!(TargetLocal && EnemyAi.Grace) && EnemyAi.TryToken(this))
                    {
                        token = true;
                        repeatsLeft--;
                        StartAntic(now, 0.5f);
                        return;
                    }
                    // The repeat waits on the player's breath and a free token, not for ever: held still against him, its
                    // touch kept hurting him and renewing his grace, and both stood there (a mantis youth, 6.8 s).
                    if (now < phaseEnd + 0.6f) return;
                }
                End(true);
            }
        }

        Vector2 Lead(float seconds)
        {
            var v = TargetVelocity;
            var lead = v * seconds * EnemyAi.LeadWeight * cur.Lead;
            if (lead.magnitude > 8f) lead = lead.normalized * 8f;
            return TargetCenter + lead;
        }

        void StartAct(float now)
        {
            phase = Phase.Act;
            actStart = now;
            landed = false;
            approached = false;
            float d = Vector2.Distance(Me, TargetCenter);
            switch (cur.Kind)
            {
                case Kind.Lunge:
                case Kind.Chase:
                {
                    float speed = cur.Speed * Tile;
                    aimPoint = Lead(Mathf.Clamp(d / Mathf.Max(1f, speed), 0.1f, 0.8f));
                    moveDir = (aimPoint - Me).normalized;
                    if (!cur.Vertical && !prof.Flyer) moveDir = new Vector2(Mathf.Sign(aimPoint.x - Me.x), 0f);
                    Face(moveDir.x);
                    Play(cur.ActClip, actWords);
                    Pace(speed);
                    phaseEnd = now + cur.Time;
                    break;
                }
                case Kind.Leap:
                    Launch(now);
                    break;
                case Kind.Dive:
                    phaseEnd = now + 0.7f + cur.Time;
                    Play(cur.ActClip, flyWords);
                    break;
                case Kind.Stalk:
                    phaseEnd = now + cur.Time;
                    Play(cur.ActClip, walkWords);
                    Pace(cur.Speed * Tile);
                    break;
                case Kind.Blink:
                    Hide(true);
                    phaseEnd = now + 0.3f;
                    break;
                case Kind.Burrow:
                    Hide(true);
                    phaseEnd = now + cur.Time;
                    break;
                case Kind.Drop:
                    DropStart();
                    phaseEnd = now + 2.5f;
                    break;
                case Kind.Shot:
                    shotsLeft = Mathf.Max(1, cur.Count);
                    nextShotAt = now;
                    phaseEnd = now + cur.Interval * shotsLeft + 0.05f;
                    break;
                case Kind.Pillar:
                    pillarIndex = 0;
                    nextShotAt = now;
                    phaseEnd = now + cur.Interval * Mathf.Max(1, cur.Count) + 0.05f;
                    break;
                case Kind.Own:
                    // Its script goes from there by itself: its own wind-up, its own attack, back to its own loop.
                    var own = ownFsms[curIndex];
                    if (own != null && own.isActiveAndEnabled) { own.SetState(cur.OwnState); DevOwnMoves++; }
                    phaseEnd = now + cur.Time;
                    break;
                default:
                    Area(now);
                    phaseEnd = now + 0.05f;
                    break;
            }
        }

        void ActTick(float now)
        {
            switch (cur.Kind)
            {
                case Kind.Shot:
                    while (shotsLeft > 0 && now >= nextShotAt)
                    {
                        FireShot(cur.Count - shotsLeft);
                        shotsLeft--;
                        nextShotAt += Mathf.Max(0.01f, cur.Interval);
                    }
                    if (shotsLeft <= 0) Recover(now);
                    return;
                case Kind.Pillar:
                    while (pillarIndex < Mathf.Max(1, cur.Count) && now >= nextShotAt)
                    {
                        PillarAt(pillarIndex++);
                        nextShotAt += Mathf.Max(0.01f, cur.Interval);
                    }
                    if (pillarIndex >= Mathf.Max(1, cur.Count)) Recover(now);
                    return;
                case Kind.Blink:
                    if (now >= phaseEnd)
                    {
                        BlinkTo();
                        Hide(false);
                        Recover(now);
                    }
                    return;
                case Kind.Burrow:
                    if (now >= phaseEnd || landed)
                    {
                        Emerge();
                        Recover(now);
                    }
                    else if (now >= nextDustAt)
                    {
                        nextDustAt = now + 0.06f;
                    }
                    return;
                case Kind.Drop:
                    if (!landed)
                    {
                        if (now >= phaseEnd) dropReturning = true;
                        if (dropReturning) DropReturn(Time.deltaTime);
                        else DropFall(Time.deltaTime);
                    }
                    if (landed)
                    {
                        DropEnd();
                        Recover(now);
                    }
                    return;
            }
            if (Moves(cur.Kind))
            {
                if (now >= phaseEnd || landed) Recover(now);
                return;
            }
            if (now >= phaseEnd) Recover(now);
        }

        void Recover(float now)
        {
            phase = Phase.Recover;
            phaseEnd = now + cur.Recover;
            // The token is given back once the move has struck: the others may go while this one stands.
            if (token) { EnemyAi.ReturnToken(this); token = false; }
            if (seized && rb != null && (cur.Kind != Kind.Leap || landed)) rb.linearVelocity = prof.Flyer ? rb.linearVelocity * 0.2f : new Vector2(0f, rb.linearVelocity.y);
            if (seized) Play(null, idleWords);
        }

        void End(bool done)
        {
            if (cur != null && curIndex >= 0 && curIndex < readyAt.Length) readyAt[curIndex] = Time.time + cur.Cooldown * EnemyAi.CooldownScale;
            if (hidden) Hide(false);
            if (dropping) DropEnd();
            Release();
            if (token) { EnemyAi.ReturnToken(this); token = false; }
            cur = null;
            curIndex = -1;
            phase = Phase.None;
            nextMoveAt = Time.time + prof.Gap * EnemyAi.CooldownScale * Random.Range(0.8f, 1.3f);
        }

        // ------------------------------------------------------------------ the body's moves

        void Step(float dt)
        {
            if (!seized || rb == null) return;
            var hero = HeroController.SilentInstance;
            if (phase == Phase.Antic)
            {
                // Held still while winding up (a walker keeps falling).
                rb.linearVelocity = prof.Flyer ? Vector2.zero : new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
                return;
            }
            if (phase == Phase.Recover)
            {
                // Standing after the move: no slide on (a leap's speed kept on the ground slid it the length of its recovery).
                if (prof.Flyer) { rb.linearVelocity = Vector2.zero; return; }
                if (cur.Kind == Kind.Leap && !landed) { LeapStep(dt); return; }
                rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
                return;
            }
            if (phase != Phase.Act) return;
            float speed = cur.Speed * Tile;
            switch (cur.Kind)
            {
                case Kind.Lunge:
                {
                    if (Blocked(moveDir, speed * dt)) { landed = true; rb.linearVelocity = Vector2.zero; Bump(); break; }
                    if (!prof.Flyer && !cur.Vertical && !GroundAhead(Mathf.Sign(moveDir.x))) { landed = true; rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); break; }
                    rb.linearVelocity = cur.Vertical || prof.Flyer ? moveDir * speed : new Vector2(moveDir.x * speed, rb.linearVelocity.y);
                    break;
                }
                case Kind.Chase:
                {
                    var to = Lead(0.4f) - Me;
                    if (!prof.Flyer) to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) moveDir = Vector3.RotateTowards(moveDir, to.normalized, 4f * dt, 0f);
                    if (Blocked(moveDir, speed * dt) || (!prof.Flyer && !GroundAhead(Mathf.Sign(moveDir.x)))) { landed = true; break; }
                    rb.linearVelocity = prof.Flyer ? moveDir * speed : new Vector2(moveDir.x * speed, rb.linearVelocity.y);
                    Face(moveDir.x);
                    break;
                }
                case Kind.Leap:
                    LeapStep(dt);
                    break;
                case Kind.Dive:
                {
                    if (!approached)
                    {
                        var above = TargetCenter + Vector2.up * cur.Height * Tile;
                        var to = above - Me;
                        if (to.magnitude < 0.6f || Time.time - actStart > 0.7f)
                        {
                            approached = true;
                            aimPoint = Lead(0.3f);
                            moveDir = (aimPoint - Me).normalized;
                            Face(moveDir.x);
                            Play(cur.ActClip, actWords);
                            phaseEnd = Time.time + cur.Time;
                        }
                        else rb.linearVelocity = to.normalized * speed * 0.6f;
                        break;
                    }
                    if (Blocked(moveDir, speed * dt)) { landed = true; rb.linearVelocity = Vector2.zero; break; }
                    rb.linearVelocity = moveDir * speed;
                    break;
                }
                case Kind.Stalk:
                {
                    // Under where the player will come down: the walker waits there.
                    float x = Lead(0.5f).x;
                    float dx = x - Me.x;
                    if (Mathf.Abs(dx) < 0.4f || Blocked(new Vector2(Mathf.Sign(dx), 0f), speed * dt) || !GroundAhead(Mathf.Sign(dx))) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); landed = true; break; }
                    rb.linearVelocity = new Vector2(Mathf.Sign(dx) * speed, rb.linearVelocity.y);
                    Face(dx);
                    break;
                }
                case Kind.Burrow:
                {
                    float dx = Lead(0.4f).x - transform.position.x;
                    if (Mathf.Abs(dx) < 0.3f) { landed = true; break; }
                    float step = Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), speed * dt);
                    var p = transform.position + new Vector3(step, 0f, 0f);
                    // Under the ground only: never out past a pit's edge, nor into a wall (the column it would come up in
                    // stone), nor up or down a step it couldn't climb.
                    float g = SurfaceAt(p.x, burrowGround);
                    if (float.IsNaN(g) || Mathf.Abs(g - burrowGround) > 2.5f || !FreeAt(new Vector2(p.x + hiddenCenter.x, g + hiddenCenter.y + 0.05f), hiddenSize * 0.9f)) { landed = true; break; }
                    burrowGround = g;
                    transform.position = p;
                    break;
                }
            }
            if (cur.Heavy && EnemyAi.MasterRules && Player.Mounts.Riding && hero != null && Touching(hero)) Player.Mounts.Dismount();
            if (cur.CutHook && EnemyAi.MasterRules && body != null && Hostiles.CrossesRope(body)) EnemyAi.CutHooks(Me);
        }

        bool Touching(HeroController hero)
        {
            var hc = hero.GetComponent<Collider2D>();
            return hc != null && body != null && body.enabled && body.Distance(hc).isOverlapped;
        }

        bool Blocked(Vector2 dir, float dist)
        {
            if (body == null) return false;
            var b = body.bounds;
            var hit = Physics2D.BoxCast(b.center, b.size * 0.9f, 0f, dir, dist + 0.05f, Hostiles.TerrainMask);
            return hit.collider != null && !hit.collider.isTrigger && Vector2.Dot(hit.normal, dir) < -0.5f;
        }

        bool Grounded()
        {
            if (body == null) return true;
            var b = body.bounds;
            return Physics2D.BoxCast(b.center, new Vector2(b.size.x * 0.8f, 0.1f), 0f, Vector2.down, b.extents.y + 0.12f, Hostiles.TerrainMask).collider != null;
        }

        bool GroundAhead(float sign)
        {
            if (body == null) return true;
            var b = body.bounds;
            var from = new Vector2(b.center.x + sign * (b.extents.x + 0.3f), b.center.y);
            return Physics2D.Raycast(from, Vector2.down, b.extents.y + 1.5f, Hostiles.TerrainMask).collider != null;
        }

        static float GroundBelow(Vector3 p)
        {
            var hit = Physics2D.Raycast((Vector2)p + Vector2.up * 0.5f, Vector2.down, 6f, Hostiles.TerrainMask);
            return hit.collider != null ? hit.point.y : float.NaN;
        }

        /// <summary>A ballistic leap at where the player will be, no higher than the move's height.</summary>
        void Launch(float now)
        {
            float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.5f, rb.gravityScale > 0f ? rb.gravityScale : 1f);
            if (rb.gravityScale <= 0f) rb.gravityScale = 1f;
            var target = Lead(0.45f);
            Vector2 from = Me;
            float dx = target.x - from.x, dy = target.y - from.y;
            float maxH = cur.Height * Tile;
            float T = Mathf.Clamp(Mathf.Abs(dx) / Mathf.Max(1f, cur.Speed * Tile), 0.35f, 1.1f);
            float vy = (dy + 0.5f * g * T * T) / T;
            float vyMax = Mathf.Sqrt(2f * g * Mathf.Max(0.5f, maxH));
            if (vy > vyMax)
            {
                vy = vyMax;
                // The same height, later: the time to come down to the target's height from the top.
                float up = vy / g, top = vy * vy / (2f * g), down = Mathf.Sqrt(Mathf.Max(0f, 2f * (top - dy) / g));
                T = up + down;
            }
            float vx = dx / Mathf.Max(0.2f, T);
            float vxMax = cur.Speed * Tile * 1.4f;
            vx = Mathf.Clamp(vx, -vxMax, vxMax);
            rb.linearVelocity = new Vector2(vx, Mathf.Max(vy, 2f));
            Face(vx);
            Play(cur.ActClip, jumpWords);
            phaseEnd = now + T + 0.6f;
        }

        /// <summary>
        /// One step of a leap. A body the physics doesn't pull down falls by the leap's own gravity, and meets nothing by
        /// itself either: what it would pass through this step it lands on (or stops at, a wall or a ceiling). Landed, it
        /// stops dead (its own script's landings stop it too: LandingHold).
        /// </summary>
        void LeapStep(float dt)
        {
            if (rb.bodyType == RigidbodyType2D.Kinematic)
            {
                rb.linearVelocity += Vector2.down * Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(1f, savedGravity) * dt;
                var v = rb.linearVelocity;
                if (body != null && v.sqrMagnitude > 0.01f)
                {
                    var b = body.bounds;
                    var hit = SolidCast(b.center, (Vector2)b.size * 0.95f, v.normalized, v.magnitude * dt + 0.02f);
                    if (hit.collider != null)
                    {
                        transform.position += (Vector3)(v.normalized * Mathf.Max(0f, hit.distance - 0.02f));
                        if (hit.normal.y > 0.5f)
                        {
                            rb.linearVelocity = Vector2.zero;
                            landed = true;
                            Land();
                            return;
                        }
                        rb.linearVelocity = hit.normal.y < -0.5f ? new Vector2(v.x, 0f) : new Vector2(0f, v.y);
                    }
                }
            }
            if (Time.time - actStart > 0.15f && rb.linearVelocity.y <= 0.05f && Grounded())
            {
                landed = true;
                rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
                Land();
            }
        }

        void Land()
        {
            if (cur.LandWave) WaveFrom(new Vector2(Me.x, body != null ? body.bounds.min.y : Me.y));
            // A heavy landing shakes loose the hooks holding near it (Master).
            if (cur.CutHook && EnemyAi.MasterRules && Player.GrapplingHook.IsLatched)
            {
                var points = new List<Vector2>();
                Player.GrapplingHook.LatchedPoints(points);
                foreach (var p in points)
                    if (Vector2.Distance(p, Me) < 6f * Tile) { EnemyAi.CutHooks(p); break; }
            }
        }

        void Bump()
        {
            // A walker's charge into a wall: a longer stand (the counter's chance).
            if (!prof.Flyer) phaseEnd = Time.time;
        }

        void Hide(bool on)
        {
            if (on && !hidden)
            {
                // Its body's size and place against its pivot, read while its collider is still on (a hidden one has
                // none: the feet were taken as half a unit under every pivot, and the tall ones came up in the ground).
                footOffset = body != null && body.enabled ? transform.position.y - body.bounds.min.y : 0.5f;
                hiddenSize = body != null && body.enabled ? (Vector2)body.bounds.size : Vector2.one;
                hiddenCenter = body != null && body.enabled ? (Vector2)(body.bounds.center - new Vector3(transform.position.x, body.bounds.min.y, 0f)) : new Vector2(0f, 0.5f);
                hiddenFrom = transform.position;
                float g0 = GroundBelow(transform.position);
                burrowGround = float.IsNaN(g0) ? transform.position.y - footOffset : g0;
                renderers = GetComponentsInChildren<Renderer>(false);
                colliders = GetComponentsInChildren<Collider2D>(false);
                colliderOn = new bool[colliders.Length];
                for (int i = 0; i < colliders.Length; i++) { colliderOn[i] = colliders[i].enabled; colliders[i].enabled = false; }
                rendererOn = new bool[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) { rendererOn[i] = renderers[i].enabled; renderers[i].enabled = false; }
                if (rb != null) { savedGravityHide = rb.gravityScale; rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; }
                hidden = true;
            }
            else if (!on && hidden)
            {
                if (colliders != null) for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderOn[i];
                if (renderers != null) for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = rendererOn[i];
                if (rb != null) rb.gravityScale = savedGravityHide;
                hidden = false;
            }
        }

        float savedGravityHide, footOffset = 0.5f, burrowGround;
        Vector2 hiddenSize = Vector2.one, hiddenCenter = new Vector2(0f, 0.5f);
        Vector3 hiddenFrom;

        /// <summary>A blink's place: ahead of the player, at the move's range, in the open and in sight of him.</summary>
        void BlinkTo()
        {
            // Blink's ring round the player (tiles): Width to Length away.
            float r = Mathf.Lerp(cur.Width, cur.Length, Random.value) * Tile;
            var ahead = TargetVelocity.sqrMagnitude > 1f ? TargetVelocity.normalized : (Me - TargetCenter).normalized;
            for (int tries = 0; tries < 10; tries++)
            {
                float a = Mathf.Atan2(ahead.y, ahead.x) + Random.Range(-1.2f, 1.2f) * (tries < 5 ? 0.6f : 1.6f);
                var p = TargetCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!prof.Flyer)
                {
                    float g = SurfaceAt(p.x, p.y);
                    if (float.IsNaN(g)) continue;
                    p.y = g + hiddenCenter.y + 0.05f;
                }
                if (!FreeAt(p, hiddenSize * 0.9f)) continue;
                var d = TargetCenter - p;
                if (Physics2D.Raycast(p, d.normalized, d.magnitude, Hostiles.TerrainMask).collider != null) continue;
                if (World.TerraWorld.RegionAt(World.CellGrid.WorldToCell(p).x, World.CellGrid.WorldToCell(p).y)?.SceneName is string s && s != scene && !scene.StartsWith(s)) continue;
                // Hidden, its collider is off: its pivot placed by the body's own offset (Me is the pivot now).
                transform.position = new Vector3(p.x - hiddenCenter.x, p.y - hiddenCenter.y + footOffset, transform.position.z);
                break;
            }
            FaceTarget();
        }

        void Emerge()
        {
            float g = prof.Flyer ? float.NaN : SurfaceAt(transform.position.x, burrowGround);
            if (!prof.Flyer)
            {
                // Out where its body fits: back towards where it went down until it does; else there.
                float from = hiddenFrom.x, x = transform.position.x;
                for (int i = 0; i < 24; i++)
                {
                    if (!float.IsNaN(g) && FreeAt(new Vector2(x + hiddenCenter.x, g + hiddenCenter.y + 0.05f), hiddenSize * 0.9f)) break;
                    float back = Mathf.MoveTowards(x, from, 0.25f);
                    if (Mathf.Approximately(back, x)) { x = from; g = SurfaceAt(x, hiddenFrom.y - footOffset); break; }
                    x = back;
                    g = SurfaceAt(x, burrowGround);
                }
                if (float.IsNaN(g)) transform.position = hiddenFrom;
                else transform.position = new Vector3(x, g + footOffset + 0.05f, transform.position.z);
            }
            Hide(false);
            if (!float.IsNaN(g))
            {
                var m = cur.Clone();
                m.Width = Mathf.Max(2f, cur.Width);
                m.Height = Mathf.Max(3f, cur.Height);
                // Its own coming up shows it (the user's, 2026-10-23: nothing of the mod's drawn): the hurt unseen.
                var z = Hostiles.Area(hm, this, m, Hostiles.Zone.Shape.Pillar, new Vector2(transform.position.x, g), 0f, 0.3f);
                if (z != null) z.Unseen = true;
                if (rb != null) rb.linearVelocity = new Vector2(0f, prof.Flyer ? 0f : 9f);
            }
        }

        bool dropping;
        bool dropReturning;
        RigidbodyType2D dropType;
        float dropGravity, dropVy;
        Vector3 dropFromPos;
        Quaternion dropFromRot;
        Vector3 dropFromScale;
        /// <summary>A ceiling crawler's own walk, stopped for its drop (a Shardmite, a Shadow Creeper).</summary>
        Crawler dropCrawler;
        Vector2 dropFromCenter;

        /// <summary>A drop's fall (units a second, a second): a Tiktik's body is kinematic and meets nothing by itself.</summary>
        const float DropGravity = 40f, DropMaxSpeed = 24f;

        /// <summary>
        /// Off its surface: upright, its body where it hung (turned about its feet on a ceiling it stood up into the stone
        /// and the physics threw it out through it), still kinematic, to fall by <see cref="DropFall"/>.
        /// </summary>
        void DropStart()
        {
            dropping = true;
            dropReturning = false;
            dropVy = 0f;
            dropFromPos = transform.position;
            dropFromRot = transform.rotation;
            dropFromCenter = Me;
            if (rb != null) { dropType = rb.bodyType; dropGravity = rb.gravityScale; }
            dropFromScale = transform.localScale;
            if (climber == null && !HasCrawler && ScriptWalks() != null)
            {
                Seize();
                dropSeized = true;
                FlipUpright();
            }
            if (climber != null)
            {
                climber.StopAllCoroutines();
                climber.enabled = false;
            }
            else
                foreach (var mv in movers)
                    if (mv is Crawler c && c.enabled)
                    {
                        c.StopAllCoroutines();
                        c.enabled = false;
                        dropCrawler = c;
                        FlipUpright();
                        break;
                    }
            dropTurn = transform.eulerAngles.z;
            dropAge = 0f;
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
            }
            // Still in a corner of the stone: a little lower until clear.
            if (body != null)
                for (int i = 0; i < 8 && !FreeAt(Me, (Vector2)body.bounds.size * 0.9f); i++) transform.position += Vector3.down * 0.15f;
        }

        /// <summary>It comes upright as it lets go, over this long, turning about its middle.</summary>
        const float DropTurnTime = 0.2f;
        float dropTurn, dropAge;

        void DropTurn(float dt, bool all)
        {
            dropAge += dt;
            float k = all ? 1f : Mathf.Clamp01(dropAge / DropTurnTime);
            Vector2 before = Me;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(dropTurn, 0f, k));
            transform.position += (Vector3)(before - Me);
        }

        /// <summary>One step of the fall: down onto whatever lies under it, the player's blocks and platforms too, never through it.</summary>
        void DropFall(float dt)
        {
            DropTurn(dt, false);
            dropVy = Mathf.Max(dropVy - DropGravity * dt, -DropMaxSpeed);
            float step = -dropVy * dt;
            if (body == null) { transform.position += Vector3.down * step; return; }
            var b = body.bounds;
            var hit = SolidCast(b.center, new Vector2(b.size.x * 0.8f, b.size.y * 0.98f), Vector2.down, step + 0.02f);
            if (hit.collider != null)
            {
                // Upright by the time it lands (a short drop), then onto the ground.
                DropTurn(0f, true);
                b = body.bounds;
                hit = SolidCast(b.center, new Vector2(b.size.x * 0.8f, b.size.y * 0.98f), Vector2.down, step + 0.6f);
                if (hit.collider != null) transform.position += Vector3.down * Mathf.Max(0f, hit.distance - 0.02f);
                landed = true;
                return;
            }
            transform.position += Vector3.down * step;
        }

        void DropEnd()
        {
            if (!dropping) return;
            dropping = false;
            if (rb != null) { rb.bodyType = dropType; rb.gravityScale = dropGravity; rb.linearVelocity = Vector2.zero; }
            if (dropSeized)
            {
                // Its script started again where it stands: floor, wall or roof by how it stands now (Check Type).
                dropSeized = false;
                Release();
                var walk = ScriptWalks();
                if (walk != null) walk.Fsm.SetState(walk.Fsm.StartState);
                dropRestarted = true;
                return;
            }
            if (dropCrawler != null)
            {
                // Crawler.Start: floor, wall or roof by how it stands now, and its walk there.
                dropCrawler.enabled = true;
                crawlerStart?.Invoke(dropCrawler, null);
                dropCrawler = null;
                dropRestarted = true;
                return;
            }
            if (climber == null) return;
            // Climber.Start sticks it to the surface under it and starts its walk there.
            RestartClimber();
            dropRestarted = true;
        }

        // A floor removed during the fall must not snap a Tiktik back to its ceiling.
        // Return visibly along the descent, stopping at any newly built solid surface.
        void DropReturn(float dt)
        {
            Vector2 delta = dropFromCenter - Me;
            float step = Mathf.Min(delta.magnitude, DropMaxSpeed * dt);
            var hit = body != null ? SolidCast(Me, (Vector2)body.bounds.size * 0.8f, delta.normalized, step + 0.02f) : default;
            if (hit.collider != null)
            {
                transform.position += (Vector3)(delta.normalized * Mathf.Max(0f, hit.distance - 0.02f));
                landed = true;
                return;
            }
            transform.position += (Vector3)(delta.normalized * step);
            if (delta.magnitude <= step + 0.01f)
            {
                Vector2 center = Me;
                transform.rotation = dropFromRot;
                transform.localScale = dropFromScale;
                transform.position += (Vector3)(center - Me);
                landed = true;
            }
        }

        static readonly System.Reflection.MethodInfo climberStart = AccessTools.Method(typeof(Climber), "Start");

        /// <summary>
        /// The Tiktik's own walk again (Climber.Start), once: its Start adds its stun to the Recoil each time it runs, so
        /// the one it added before is taken off first.
        /// </summary>
        void RestartClimber()
        {
            if (climber == null) return;
            var recoil = GetComponent<Recoil>();
            if (recoil != null) recoil.OnHandleFreeze -= climber.Stun;
            climber.enabled = true;
            climberStart?.Invoke(climber, null);
        }

        // ------------------------------------------------------------------ the shell (Profile.Reflect)

        bool shelled;
        float shellUntil, shellReadyAt;
        bool shellInvincible;
        int shellFrom, blockedSeen;
        Vector3 shellPos;
        Quaternion shellRot;
        float nextShove;

        /// <summary>How long it holds its back to the blows, and the rest before it can again.</summary>
        const float ShellTime = 1.4f, ShellRest = 4f;

        /// <summary>
        /// On a floor, free (not in a move, a knockback or a stun of its own), its back up: a crawler on a wall or a ceiling
        /// already has it outward.
        /// </summary>
        bool ShellFits() => body != null && rb != null && !(hm.IsInvincible && hm.InvincibleFromDirection == 0) && !CrawlerOffFloor() && climber == null && !hidden;

        /// <summary>
        /// Struck: it rears its crystal back toward the player (tipped over his way), shielded from his side as the game's
        /// one-sided shields are (HealthManager.InvincibleFromDirection): his blows clank off it (MeleeHits.Blocks, the
        /// game's own crystal sparks, BLOCKED HIT), his reflectable shots come back (TerraProjectiles' reflection), and a
        /// swing that clanks on it up close throws him back. A pink flash, a crystal tinkle and dust first; it holds for
        /// <see cref="ShellTime"/>, then turns back as it was.
        /// </summary>
        void StartShell(float now)
        {
            shelled = true;
            shellUntil = now + ShellTime;
            shellReadyAt = shellUntil + ShellRest;
            Seize();
            shellInvincible = hm.IsInvincible;
            shellFrom = hm.InvincibleFromDirection;
            shellPos = transform.position;
            shellRot = transform.rotation;
            Vector2 to = TargetCenter - Me;
            bool fromAbove = to.y > Mathf.Abs(to.x) * 1.5f;
            float side = to.x < 0f ? -1f : 1f;
            // HealthManager.IsBlockingByDirection: 1 stops blows going right (from the left), 3 going left, 4 going down.
            hm.InvincibleFromDirection = fromAbove ? 4 : side < 0f ? 1 : 3;
            hm.IsInvincible = true;
            // A floor crawler's back is its top: tipped toward his side, its crystal faces him.
            if (!fromAbove) transform.RotateAround(Me, Vector3.forward, side < 0f ? 70f : -70f);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            blockedSeen = Items.MeleeHits.DevBlocked;
            TerraMod.Instance?.PlaySound("Item_27", 0.55f);
            EnemyAi.DevShells++;
        }

        void ShellTick(float now)
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
            // A blow that clanked on it (MeleeHits counts every blocked one) with the player up close: the crystal throws him back.
            int blocked = Items.MeleeHits.DevBlocked;
            if (blocked != blockedSeen)
            {
                blockedSeen = blocked;
                Vector2 away = TargetCenter - Me;
                if (now >= nextShove && away.magnitude < 2.6f)
                {
                    nextShove = now + 0.4f;
                    var to = TargetCenter + new Vector2(Mathf.Sign(away.x == 0f ? 1f : away.x) * 2.2f, 0.6f);
                    if (TargetLocal) PlayerDebuffs.Pull(to, 16f, 0.14f);
                    else NetAi.SendPull(scene, TargetSlot, to, 16f, 0.14f);
                }
            }
            if (now >= shellUntil) EndShell();
        }

        /// <summary>Back as it was: its own invincibility, its place and turn, its walk.</summary>
        void EndShell()
        {
            if (!shelled) return;
            shelled = false;
            if (hm != null)
            {
                hm.IsInvincible = shellInvincible;
                hm.InvincibleFromDirection = shellFrom;
            }
            Vector2 center = Me;
            transform.rotation = shellRot;
            transform.position += (Vector3)(center - Me);
            if (seized && cur == null) Release();
        }

        // ------------------------------------------------------------------ free room

        static readonly Collider2D[] overlap = new Collider2D[16];
        static readonly RaycastHit2D[] casts = new RaycastHit2D[16];

        /// <summary>A box clear of solid ground (triggers and one-way platforms don't count).</summary>
        static bool FreeAt(Vector2 center, Vector2 size)
        {
            int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlap, Hostiles.TerrainMask);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c != null && !c.isTrigger && !c.usedByEffector) return false;
            }
            return true;
        }

        /// <summary>
        /// The nearest solid thing a box would meet going a way (triggers never; a one-way platform only from above,
        /// going down onto it).
        /// </summary>
        static RaycastHit2D SolidCast(Vector2 center, Vector2 size, Vector2 dir, float distance)
        {
            int n = Physics2D.BoxCastNonAlloc(center, size, 0f, dir, casts, distance, Hostiles.TerrainMask);
            RaycastHit2D best = default;
            float d = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = casts[i];
                var c = h.collider;
                if (c == null || c.isTrigger || h.distance >= d) continue;
                if (c.usedByEffector && !(dir.y < -0.5f && h.normal.y > 0.5f)) continue;
                // Already inside it where it starts: not something it meets.
                if (h.distance <= 0f && !c.usedByEffector) continue;
                best = h;
                d = h.distance;
            }
            return best;
        }

        /// <summary>Whether a point is inside solid ground.</summary>
        static bool SolidAt(Vector2 p)
        {
            int n = Physics2D.OverlapPointNonAlloc(p, overlap, Hostiles.TerrainMask);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c != null && !c.isTrigger && !c.usedByEffector) return true;
            }
            return false;
        }

        /// <summary>The ground's top under a column near a height: none when the column is stone there (a wall) or nothing is below.</summary>
        static float SurfaceAt(float x, float nearY)
        {
            var from = new Vector2(x, nearY + 2.5f);
            if (SolidAt(from)) return float.NaN;
            var hit = Physics2D.Raycast(from, Vector2.down, 5.5f, Hostiles.TerrainMask);
            return hit.collider != null && !hit.collider.isTrigger ? hit.point.y : float.NaN;
        }

        /// <summary>A Crawler on a wall or a ceiling (Crawler.Start's own reading of its turn and its flip).</summary>
        bool dropSeized;

        /// <summary>On a ceiling by its flipped picture (scale y below 0): the same look turned half round, so the drop's turn rights it.</summary>
        void FlipUpright()
        {
            var sc = transform.localScale;
            if (sc.y >= 0f) return;
            transform.localScale = new Vector3(-sc.x, -sc.y, sc.z);
            transform.rotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z + 180f);
        }

        /// <summary>The script that walks it over floors, walls and roofs (a Shardmite's: its Check Type).</summary>
        PlayMakerFSM ScriptWalks()
        {
            foreach (var f in controls)
                if (f != null && f.Fsm != null && f.Fsm.GetState("Check Type") != null) return f;
            return null;
        }

        /// <summary>One its own script walks, upside down on a ceiling.</summary>
        bool ScriptOnCeiling()
        {
            if (climber != null || HasCrawler || ScriptWalks() == null) return false;
            float z = transform.eulerAngles.z % 360f;
            if (z < 0f) z += 360f;
            bool flipped = transform.localScale.y < 0f;
            return z > 135f && z < 225f ? !flipped : (z < 45f || z > 315f) && flipped;
        }

        /// <summary>A crawler upside down on a ceiling (its drop's place).</summary>
        bool CrawlerOnCeiling()
        {
            if (!HasCrawler) return false;
            float z = transform.eulerAngles.z % 360f;
            if (z < 0f) z += 360f;
            bool flipped = transform.localScale.y < 0f;
            return z > 135f && z < 225f ? !flipped : (z < 45f || z > 315f) && flipped;
        }

        bool CrawlerOffFloor()
        {
            if (!HasCrawler) return false;
            float z = transform.eulerAngles.z % 360f;
            if (z < 0f) z += 360f;
            bool flipped = transform.localScale.y < 0f;
            if (z >= 45f && z <= 135f || z >= 225f && z <= 315f) return true;
            return z > 135f && z < 225f ? !flipped : flipped;
        }

        bool dropRestarted;

        // ------------------------------------------------------------------ shots and areas

        void FireShot(int n)
        {
            var s = cur;
            float speed = s.ShotSpeed * Tile;
            Vector2 from = Me;
            Vector2 target;
            switch (s.Aim)
            {
                case Aim.Target: target = TargetCenter; break;
                case Aim.Bracket: target = (n % 2 == 0) ? TargetCenter : Lead(0.6f); break;
                default: target = Lead(Mathf.Clamp(Vector2.Distance(from, TargetCenter) / Mathf.Max(1f, speed), 0.1f, 0.9f)); break;
            }
            Vector2 dir;
            if (s.Aim == Aim.Down || s.Aim == Aim.Up)
            {
                // Down (a bomb) or up (a spray) at the player, at most 45° off straight (straight down beside him never landed).
                var straight = s.Aim == Aim.Down ? Vector2.down : Vector2.up;
                float off = Mathf.Clamp(Vector2.SignedAngle(straight, target - from), -45f, 45f);
                dir = Quaternion.Euler(0f, 0f, off) * straight;
            }
            else if (s.Aim == Aim.Radial)
            {
                // One ray at the player, the rest round it (a ring turned only by its count missed him between two rays).
                float at = Mathf.Atan2(TargetCenter.y - from.y, TargetCenter.x - from.x) * Mathf.Rad2Deg;
                dir = Quaternion.Euler(0f, 0f, at + 360f / Mathf.Max(1, s.Count) * n) * Vector2.right;
            }
            else dir = (target - from).normalized;
            Vector2 v;
            if (s.Gravity > 0f && s.Aim == Aim.Down)
            {
                // A bomb thrown down at its speed, across as much as it takes to come down on him.
                float g = s.Gravity * Tile, drop = Mathf.Max(0.1f, from.y - target.y);
                float t = (-speed + Mathf.Sqrt(speed * speed + 2f * g * drop)) / g;
                v = new Vector2(Mathf.Clamp((target.x - from.x) / Mathf.Max(0.1f, t), -speed, speed), -speed);
            }
            else if (s.Gravity > 0f && s.Aim != Aim.Radial) v = Lob(from, target, speed, s.Gravity * Tile);
            else v = dir * speed;
            // A fan: the shots of one volley spread about the aim (Count at once when Interval is 0).
            if (s.Spread > 0f && s.Aim != Aim.Radial && s.Count > 1 && s.Interval <= 0f)
            {
                float step = s.Spread / (s.Count - 1);
                float off = -s.Spread / 2f + step * n;
                // An even fan has no middle shot (two went either side of a player standing still): half a step over, one
                // at him and the rest the way he is heading.
                if (s.Count % 2 == 0)
                {
                    if (n == 0)
                    {
                        float across = v.x * TargetVelocity.y - v.y * TargetVelocity.x;
                        fanSide = Mathf.Abs(across) > 0.01f ? Mathf.Sign(across) : (Random.value < 0.5f ? -1f : 1f);
                    }
                    off += fanSide * step / 2f;
                }
                v = Quaternion.Euler(0f, 0f, off) * v;
            }
            if (ownShot == null) return;
            // Its own shot, sent as the move aims it: lobbed under its own gravity when it falls.
            if (ownShotGravity > 0.05f && s.Aim != Aim.Radial && s.Aim != Aim.Up && s.Aim != Aim.Down)
                v = Lob(from, target, Mathf.Max(speed, 8f), ownShotGravity);
            Bosses.OwnShots.Throw(ownShot, from, v, ownShotFire);
            DevOwnShots++;
        }

        float fanSide = 1f;

        /// <summary>A throw's velocity to land on the target under the shot's gravity (the flatter of the two arcs, or the farthest reach).</summary>
        internal static Vector2 Lob(Vector2 from, Vector2 to, float speed, float g)
        {
            float dx = to.x - from.x, dy = to.y - from.y;
            float x = Mathf.Abs(dx);
            float v2 = speed * speed;
            float root = v2 * v2 - g * (g * x * x + 2f * dy * v2);
            float angle;
            if (root < 0f || x < 0.01f) angle = dy > 0f ? 60f * Mathf.Deg2Rad : 45f * Mathf.Deg2Rad;
            else angle = Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * x));
            return new Vector2(Mathf.Cos(angle) * speed * Mathf.Sign(dx == 0f ? 1f : dx), Mathf.Sin(angle) * speed);
        }

        void PillarAt(int n)
        {
            // Drawn by the mod (none of the enemy's own): not made.
            if (Drawn(cur)) return;
            Vector2 at = n == 0 ? TargetCenter : n == 1 ? Lead(0.6f) : Lead(1.1f);
            float g = GroundBelow(new Vector3(at.x, at.y, 0f));
            if (float.IsNaN(g))
            {
                // Over a pit or in the air: a pillar standing where the player hangs.
                g = at.y - cur.Height * Tile * 0.5f;
            }
            Hostiles.Area(hm, this, cur, Hostiles.Zone.Shape.Pillar, new Vector2(at.x, g), cur.Delay, cur.Duration);
            SoundLive(cur.Look, cur.Delay);
        }

        /// <summary>Its sound as what it made goes live (not before: a sound ahead of it would be a tell).</summary>
        void SoundLive(Look look, float delay)
        {
            if (delay <= 0.01f) Sound(look);
            else StartCoroutine(SoundAfter(look, delay));
        }

        System.Collections.IEnumerator SoundAfter(Look look, float delay)
        {
            yield return new WaitForSeconds(delay);
            Sound(look);
        }

        void WaveFrom(Vector2 feet)
        {
            var m = cur.Clone();
            if (cur.LandWave)
            {
                m.Speed = cur.LandWaveSpeed;
                m.Width = 1.4f;
                m.Height = 2f;
            }
            m.Width = Mathf.Max(1.2f, m.Width);
            m.Height = Mathf.Max(1.5f, m.Height > 4f ? 2f : m.Height);
            foreach (float sign in new[] { -1f, 1f })
            {
                var z = Hostiles.Area(hm, this, m, Hostiles.Zone.Shape.Wave, feet, 0f, Mathf.Max(0.4f, m.Length * Tile / Mathf.Max(1f, m.Speed * Tile)));
                z.WaveVx = sign * Mathf.Max(6f, m.Speed * Tile);
            }
        }

        void Area(float now)
        {
            if (Drawn(cur)) return;
            switch (cur.Kind)
            {
                case Kind.Pulse:
                {
                    var z = Hostiles.Area(hm, this, cur, Hostiles.Zone.Shape.Circle, Me, 0f, cur.Duration);
                    z.Follow = transform;
                    z.FollowOffset = Me - (Vector2)transform.position;
                    Sound(cur.Look);
                    break;
                }
                case Kind.Wave:
                    if (blockerControl != null) StartCoroutine(BlockerSpit.Wave(this, blockerControl, body, Mathf.Sign(TargetCenter.x - Me.x), cur.Length * Tile));
                    else WaveFrom(new Vector2(Me.x, body != null ? body.bounds.min.y : Me.y));
                    break;
                case Kind.Cloud:
                {
                    Hostiles.Area(hm, this, cur, Hostiles.Zone.Shape.Circle, Me, 0.15f, cur.Duration);
                    break;
                }
                case Kind.Pull:
                    if (TargetLocal) PlayerDebuffs.Pull(Me, cur.Speed * Tile, cur.Duration);
                    else NetAi.SendPull(scene, TargetSlot, Me, cur.Speed * Tile, cur.Duration);
                    {
                        // The pull's line hurts where it shows (every attack seen hurts: the user's, 2026-10-23).
                        var line = cur.Clone();
                        var to = TargetCenter - Me;
                        line.Length = to.magnitude / Tile;
                        line.Width = 0.6f;
                        line.Sweep = 0f;
                        var z = Hostiles.Area(hm, this, line, Hostiles.Zone.Shape.Beam, Me, 0f, 0.25f);
                        z.SetAngle(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
                    }
                    break;
                case Kind.Beam:
                {
                    var aim = Lead(0.25f);
                    var dir = aim - Me;
                    // The sweep turns toward where the player was heading across the beam.
                    float across = Vector2.Dot(TargetVelocity, new Vector2(-dir.y, dir.x).normalized);
                    var beam = cur.Sweep != 0f && across < 0f ? SweepFlipped(cur) : cur;
                    var z = Hostiles.Area(hm, this, beam, Hostiles.Zone.Shape.Beam, Me, cur.Delay, cur.Duration);
                    z.SetAngle(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    z.Follow = transform;
                    z.FollowOffset = Me - (Vector2)transform.position;
                    SoundLive(cur.Look, cur.Delay);
                    break;
                }
            }
        }

        static Move SweepFlipped(Move m)
        {
            var c = m.Clone();
            c.Sweep = -m.Sweep;
            return c;
        }

        // ------------------------------------------------------------------ taking and giving back the body

        void Seize()
        {
            if (seized) return;
            seized = true;
            DevSeizes++;
            paused.Clear();
            foreach (var f in fsms)
            {
                if (f == null || f.Fsm == null || f.Fsm.ManualUpdate) continue;
                f.Fsm.ManualUpdate = true;
                paused.Add(f.Fsm);
                EnemyAi.Paused.Add(f.Fsm);
            }
            // Its own tweens move the transform by themselves (a Duranda's bobbing, a sibling's rise): stopped while the
            // body is held, their scripts started again from where it is (Release), else the tween dragged it back.
            tweened.Clear();
            foreach (var f in paused)
                if (Tweens(f.ActiveState) || f.Name == "Tween" || f.Name == "Bob") tweened.Add(f);
            if (GetComponent<iTween>() != null) iTween.Stop(gameObject);
            for (int i = 0; i < movers.Length; i++)
            {
                moverOn[i] = movers[i] != null && movers[i].enabled;
                if (movers[i] == null || movers[i] is Climber) continue;
                // A crawler walks in a coroutine, which a disabled script keeps running: stopped, and started again after.
                if (movers[i] is Crawler c) c.StopAllCoroutines();
                movers[i].enabled = false;
            }
            if (rb != null)
            {
                // A knockback's launch has the body's gravity off while it lasts: its own is the one to give back.
                var k = Knock();
                savedGravity = k != null && k.Active ? k.OwnGravity : rb.gravityScale;
                savedType = rb.bodyType;
                savedVelocity = rb.linearVelocity;
            }
            savedScale = transform.localScale;
            savedClip = anim != null && anim.CurrentClip != null ? anim.CurrentClip.name : null;
        }

        void Release()
        {
            if (!seized) return;
            seized = false;
            foreach (var f in paused)
            {
                if (f == null) continue;
                f.ManualUpdate = false;
                EnemyAi.Paused.Remove(f);
            }
            paused.Clear();
            // A motion script (its home read at its start: a Duranda's Tween) starts over here; a control re-enters the
            // state it tweened in, the tween going from here.
            foreach (var f in tweened)
                if (f != null && f.ActiveState != null) f.SetState(f.Name == "Tween" || f.Name == "Bob" ? f.StartState : f.ActiveStateName);
            tweened.Clear();
            // The body first: a crawler's own start (below) then sets the gravity of the surface it is on, last.
            if (rb != null)
            {
                if (!(cur != null && cur.Kind == Kind.Drop)) rb.gravityScale = savedGravity;
                rb.bodyType = savedType;
                if (!prof.Flyer && !HasCrawler) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            for (int i = 0; i < movers.Length; i++)
            {
                if (movers[i] == null || movers[i] is Climber) continue;
                movers[i].enabled = moverOn[i];
                // Crawler.Start: its way from its facing, its gravity from its surface, its walk again.
                if (movers[i] is Crawler c && moverOn[i]) crawlerStart?.Invoke(c, null);
            }
            // A walker knows its facing by the scale it had: given back as it was (a crawler takes its new one).
            if (HasWalker && !dropRestarted) transform.localScale = savedScale;
            dropRestarted = false;
            if (anim != null && savedClip != null && anim.GetClipByName(savedClip) != null) anim.Play(savedClip);
        }

        static readonly System.Reflection.MethodInfo crawlerStart = AccessTools.Method(typeof(Crawler), "Start");

        readonly List<Fsm> tweened = new List<Fsm>();

        static bool Tweens(FsmState s)
        {
            if (s == null) return false;
            foreach (var a in s.Actions)
                if (a is HutongGames.PlayMaker.Actions.iTweenFsmAction) return true;
            return false;
        }

        /// <summary>The Terraria knockback's launch on the body (Items.TerraKnockback), once it has had one.</summary>
        Items.KnockFlight Knock() => knock != null ? knock : (knock = GetComponent<Items.KnockFlight>());

        /// <summary>
        /// Still flying or braking from a Terraria knockback: no move of the body starts meanwhile (the launch owns its speed
        /// and has its gravity off; a move taken then gave the body back weightless, and a struck crawlid floated off).
        /// </summary>
        bool KnockedBack()
        {
            var k = Knock();
            return k != null && k.Active;
        }

        bool HasCrawler
        {
            get
            {
                foreach (var m in movers) if (m is Crawler) return true;
                return false;
            }
        }

        bool HasWalker
        {
            get
            {
                foreach (var m in movers) if (m is Walker) return true;
                return false;
            }
        }

        // ------------------------------------------------------------------ looks

        static readonly string[] anticWords = { "antic", "attack", "charge", "windup", "ready" };
        static readonly string[] actWords = { "lunge", "attack", "charge", "dash", "strike", "roll", "fly", "run", "walk" };
        static readonly string[] jumpWords = { "jump", "leap", "air", "fall", "lunge" };
        static readonly string[] flyWords = { "fly", "idle", "move" };
        static readonly string[] walkWords = { "run", "walk", "chase", "move" };
        static readonly string[] idleWords = { "idle", "fly", "walk" };

        void Play(string clip, string[] words)
        {
            if (anim == null || anim.Library == null) return;
            if (clip != null && anim.GetClipByName(clip) != null) { anim.Play(clip); return; }
            if (words == null) return;
            foreach (var w in words)
                foreach (var c in anim.Library.clips)
                    if (c != null && !string.IsNullOrEmpty(c.name) && c.name.ToLowerInvariant().Contains(w) && c.frames != null && c.frames.Length > 0)
                    {
                        anim.Play(c);
                        return;
                    }
        }

        public static int DevPaced;

        /// <summary>
        /// The clip a move that carries the body plays (a lunge, a chase, a stalk) sped up with it: as many times its own
        /// frame rate as the move outruns the game's own speed for it (its script's "… Speed" named like the clip: a
        /// Husk Hornhead's Lunge Speed for its Attack Lunge; else its walk's), at most three times, so its legs keep up
        /// instead of it gliding along in one stride.
        /// </summary>
        void Pace(float speed)
        {
            if (anim == null || anim.CurrentClip == null || speed <= 0f) return;
            float own = OwnSpeed(anim.CurrentClip.name);
            if (own <= 0f || speed <= own * 1.05f) return;
            anim.ClipFps = anim.CurrentClip.fps * Mathf.Min(3f, speed / own);
            DevPaced++;
        }

        float OwnSpeed(string clip)
        {
            string c = clip.ToLowerInvariant();
            float named = 0f;
            if (fsms != null)
                foreach (var f in fsms)
                {
                    if (f == null) continue;
                    foreach (var v in f.FsmVariables.FloatVariables)
                    {
                        if (v == null || string.IsNullOrEmpty(v.Name)) continue;
                        string n = v.Name.ToLowerInvariant();
                        int at = n.IndexOf("speed", System.StringComparison.Ordinal);
                        if (at <= 0 || Mathf.Abs(v.Value) < 1f) continue;
                        string word = n.Substring(0, at).Trim();
                        int space = word.LastIndexOf(' ');
                        if (space >= 0) word = word.Substring(space + 1);
                        if (word.Length > 2 && c.Contains(word)) named = Mathf.Max(named, Mathf.Abs(v.Value));
                    }
                }
            if (named > 0f) return named;
            var walker = GetComponent<Walker>();
            return walker != null ? Mathf.Max(Mathf.Abs(walker.walkSpeedL), Mathf.Abs(walker.walkSpeedR)) : 0f;
        }

        /// <summary>Its spit's sound (a ball of the Elder Baldur's goop thrown or popped up by the kit).</summary>
        internal void Spit() => Sound(Look.Infection);

        void Sound(Look? look)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !OnScreen()) return;
            string s = look switch
            {
                null => "Item_7",
                Look.Fire => "Item_20",
                Look.Electric => "Item_93",
                Look.Soul => "Item_8",
                Look.Web or Look.Poison or Look.Acid or Look.Infection or Look.Spore or Look.Honey or Look.Water => "Item_17",
                Look.Coin => "Coin_1",
                _ => "Item_5",
            };
            try { mod.PlaySound(s, look == null ? 0.25f : 0.35f); } catch (System.Exception) { }
        }

        static readonly System.Reflection.FieldInfo walkerRight = AccessTools.Field(typeof(Walker), "rightScale");
        bool facesRight;

        void FaceTarget() => Face(TargetCenter.x - Me.x);

        /// <summary>
        /// Which way its picture faces, as its own script's turns say (FaceObject, FaceDirection: most of their votes): one
        /// drawn facing right (a Mantis Youth, a Husk Miner, a Lesser Mawlek) was turned its back to the player for a move.
        /// </summary>
        static bool? SpriteFacesRight(PlayMakerFSM[] fsms)
        {
            int right = 0, left = 0;
            foreach (var f in fsms)
            {
                if (f == null || f.FsmStates == null) continue;
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        FsmBool says = a is HutongGames.PlayMaker.Actions.FaceObject fo ? fo.spriteFacesRight
                            : a is HutongGames.PlayMaker.Actions.FaceDirection fd ? fd.spriteFacesRight : null;
                        if (says == null || says.UseVariable) continue;
                        if (says.Value) right++;
                        else left++;
                    }
            }
            return right + left == 0 ? (bool?)null : right > left;
        }

        void Face(float dx)
        {
            if (Mathf.Abs(dx) < 0.01f || climber != null) return;
            var s = transform.localScale;
            bool right = dx > 0f;
            float sign = right != facesRight ? -1f : 1f;
            if (Mathf.Sign(s.x) != sign) transform.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
        }
    }
}
