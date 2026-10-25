using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.EnemyAi;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        static object AiCall(Brain b, string name, params object[] args) => AccessTools.Method(typeof(Brain), name).Invoke(b, args);
        static void AiSet(Brain b, string name, object value) => AccessTools.Field(typeof(Brain), name).SetValue(b, value);
        static T AiGet<T>(Brain b, string name) => (T)AccessTools.Field(typeof(Brain), name).GetValue(b);

        // Real Unity components, without a native enemy prefab's Start. Explicitly driven probes
        // distinguish lifecycle guarantees from natural attack selection and complete fights.
        void AiLifecycleChecks()
        {
            var go = new GameObject("TK AI lifecycle probe");
            go.SetActive(false);
            var hm = go.AddComponent<HealthManager>();
            hm.hp = 100;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            go.AddComponent<BoxCollider2D>();
            var visible = go.AddComponent<MeshRenderer>();
            var child = new GameObject("hidden renderer");
            child.transform.SetParent(go.transform);
            var invisible = child.AddComponent<MeshRenderer>();
            invisible.enabled = false;
            var p = new Profile { Key = "probe", AimLead = 0.2f };
            p.Moves.Add(new Move { Kind = Kind.Lunge, Antic = 0.3f, Repeat = 1 });
            var b = go.AddComponent<Brain>();
            b.Init(hm, new EnemyStats.Info { Hm = hm, Record = "probe", MaxHp = 100 }, p);
            var held = new List<Brain>();
            var holders = new List<GameObject>();
            try
            {
                AiCall(b, "Begin", 0, Time.time);
                AiCall(b, "Think"); // Calm is unconditional for a Sibling with the Void Heart.
                // An explicit loss of target via the hero's death cancels even a seized body.
                var hero = HeroController.SilentInstance;
                bool wasDead = hero.cState.dead;
                try { hero.cState.dead = true; AiCall(b, "Think"); }
                finally { hero.cState.dead = wasDead; }
                FxCheck("NewAI target loss releases body", !AiGet<bool>(b, "seized") && AiGet<Move>(b, "cur") == null && rb.gravityScale == 2f, b.DevState);

                AiCall(b, "Begin", 0, Time.time);
                AiCall(b, "Recover", Time.time);
                var graceUntil = Player.TerraHurt.LastHurtAt;
                try
                {
                    Player.TerraHurt.LastHurtAt = Time.time;
                    AiCall(b, "Tick", Time.time + 1f);
                    FxCheck("NewAI repeat respects hurt grace", b.DevState.EndsWith(":Recover"), b.DevState);
                }
                finally { Player.TerraHurt.LastHurtAt = graceUntil; }
                // Active token holders, with Update disabled; no native Start or combat runs.
                for (int i = 0; i < EnemyAi.EnemyAi.Tokens; i++)
                {
                    var h = new GameObject("TK token holder");
                    holders.Add(h);
                    var hb = h.AddComponent<Brain>();
                    held.Add(hb);
                    EnemyAi.EnemyAi.TryToken(hb);
                }
                AiCall(b, "Tick", Time.time + 1f);
                FxCheck("NewAI repeat waits for token", b.DevState.EndsWith(":Recover"), b.DevState);
                foreach (var hb in held) EnemyAi.EnemyAi.ReturnToken(hb);
                AiCall(b, "Tick", Time.time + 1f);
                FxCheck("NewAI repeat reacquires token and full antic", AiGet<bool>(b, "token") && b.DevState.EndsWith(":Antic") && AiGet<float>(b, "phaseEnd") >= Time.time + 1f + EnemyAi.EnemyAi.MinAntic - 0.001f, b.DevState);
                AiCall(b, "End", false);

                // Use an active object for GetComponentsInChildren(false), keeping its native HM off.
                hm.enabled = false;
                b.enabled = false;
                go.SetActive(true);
                AiCall(b, "Hide", true);
                AiCall(b, "Hide", false);
                FxCheck("NewAI hide preserves renderer states", visible.enabled && !invisible.enabled, $"visible={visible.enabled} hidden={invisible.enabled}");
                AiCall(b, "LeadAim", true);
                AiCall(b, "LeadAim", false);
                FxCheck("NewAI calm restores native aim", !EnemyAi.EnemyAi.Proxies.ContainsKey(hm) && !b.Leading, "proxy restored");
                AiCall(b, "StartShell", Time.time);
                go.transform.position += new Vector3(3f, 2f, 0f);
                Vector2 shellCenter = b.Position;
                AiCall(b, "EndShell");
                FxCheck("NewAI shell recovery never snaps back", Vector2.Distance(shellCenter, b.Position) < 0.01f && !AiGet<bool>(b, "seized"), $"before={shellCenter} after={b.Position}");
                p.TouchDebuff = PlayerDebuffs.Darkness; p.TouchTicks = 180;
                b.enabled = true;
                AiCall(b, "Begin", 0, Time.time);
                AiCall(b, "StartAct", Time.time);
                FxCheck("NewAI body move preserves species touch debuff", b.TouchFlags(out int touch, out int ticks, out _, out _) && touch == PlayerDebuffs.Darkness && ticks == 180, "a move without its own debuff retains the species effect");
                p.Moves[0].Debuff = PlayerDebuffs.Slow; p.Moves[0].DebuffTicks = 60;
                FxCheck("NewAI move debuff overrides species debuff", b.TouchFlags(out touch, out ticks, out _, out _) && touch == PlayerDebuffs.Slow && ticks == 60, "explicit move debuff wins");
                AiCall(b, "End", false);
                b.enabled = false;

                foreach (var key in new[] { "ZombieGuard", "MossKnightFat", "SentryFat", "RoyalPlumper", "GiantHopper" })
                {
                    var profile = Profiles.For(key);
                    foreach (var m in profile.Moves.Where(m => m.LandWave))
                        FxCheck("NewAI independent leap/wave " + key, m.Height > 2f && m.LandWaveSpeed > 0f, $"jumpHeight={m.Height} jumpSpeed={m.Speed} waveSpeed={m.LandWaveSpeed}");
                }

                var beamProfile = new Profile { Key = "beam" };
                beamProfile.Moves.Add(new Move { Kind = Kind.Beam, Sweep = 20f, Delay = 0.5f });
                AiSet(b, "prof", beamProfile);
                AiSet(b, "cur", beamProfile.Moves[0]);
                AccessTools.Property(typeof(Brain), "TargetCenter").SetValue(b, (Vector2)go.transform.position + Vector2.right * 3f);
                AccessTools.Property(typeof(Brain), "TargetVelocity").SetValue(b, Vector2.down);
                AiCall(b, "Area", Time.time);
                var zone = Hostiles.Attack.All.OfType<Hostiles.Zone>().Last(z => z.Owner == hm);
                FxCheck("NewAI beam sweep follows crossing direction", zone.Data.Sweep < 0f && beamProfile.Moves[0].Sweep == 20f, $"actual={zone.Data.Sweep} profile={beamProfile.Moves[0].Sweep}");
                Object.Destroy(zone.gameObject);
                AiSet(b, "cur", null);
            }
            finally
            {
                foreach (var hb in held) EnemyAi.EnemyAi.ReturnToken(hb);
                foreach (var h in holders) Object.Destroy(h);
                Object.Destroy(go);
            }
        }

        IEnumerator NewAiAudit()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            AiLifecycleChecks();
            AiBossLifecycleChecks();
            yield return AiContinuousMoveChecks();
            yield return AiEffectChecks();
            if (suite == "newaivis") { Plugin.Log.LogInfo("[NEWAI_DONE]"); yield break; }
            // Dictionary insertion order preserves the region-by-region review order.
            _ = Profiles.Count;
            var profiles = (Dictionary<string, Profile>)AccessTools.Field(typeof(Profiles), "all").GetValue(null);
            var selected = (Environment.GetEnvironmentVariable("TK_NEWAI_PROFILES") ?? "").Split(';');
            var entries = profiles.Where(p => selected.Length == 1 && selected[0] == "" || selected.Contains(p.Key)).ToArray();
            int ordinal = 0;
            foreach (var pair in entries)
            {
                // Native pooled explosions/poison from the previous enemy must not kill the next case.
                var poolsBefore = AiSpawnedPoolRoots();
                var p = pair.Value;
                ordinal++;
                FxCheck("NewAI profile " + p.Key, p.Key == pair.Key && (p.Moves.Count > 0 || p.Key == "CeilingDropper") && p.Moves.All(m => m.Min >= 0f && m.Max >= m.Min && m.Weight > 0f && m.Cooldown >= 0f && m.Recover >= 0f && m.Antic > 0f && m.Count > 0), $"#{ordinal}/{profiles.Count} moves={p.Moves.Count}");
                BookSpawner.Spawn(pair.Key);
                Brain brain = null;
                float end = Time.realtimeSinceStartup + 16f;
                while (Time.realtimeSinceStartup < end)
                {
                    brain = Object.FindObjectsByType<Brain>(FindObjectsSortMode.None).FirstOrDefault(b => b.Record == pair.Key && b.name.Contains("图鉴"));
                    if (brain != null) break;
                    yield return null;
                }
                if (brain == null)
                {
                    Plugin.Log.LogWarning($"[NEWAI_UNAVAILABLE] {pair.Key}: {BookSpawner.DevLast}; source did not produce an active Brain; not runtime verified");
                    continue;
                }
                FxCheck("NewAI real prefab attached " + pair.Key, true, BookSpawner.DevLast);
                FxCheck("NewAI native physics bridge " + pair.Key, PlayMakerUnity2d.isAvailable(), "borrowed scene must not destroy the active collision/trigger bridge");
                int failures = Brain.DevFails;
                yield return new WaitForSeconds(1.8f);
                if (brain == null) Plugin.Log.LogWarning($"[NEWAI_UNAVAILABLE] {pair.Key}: native prefab died before observation finished; needs own-scene validation");
                else FxCheck("NewAI real prefab update " + pair.Key, brain.DevState != "failed" && Brain.DevFails == failures, brain.DevState);
                if (brain != null)
                {
                    // Force only the move lifecycle, not eligibility, timing or whole trajectories.
                    for (int i = 0; i < p.Moves.Count; i++)
                    {
                        bool passed = true;
                        string detail = p.Moves[i].Kind.ToString();
                        try
                        {
                            AiCall(brain, "End", false);
                            if (p.Moves[i].Kind == Kind.Drop && brain.GetComponent<Climber>() == null) { detail += " no Climber; eligibility excludes"; }
                            else
                            {
                                AiCall(brain, "Begin", i, Time.time);
                                AiCall(brain, "StartAct", Time.time);
                                AiCall(brain, "Step", 0.02f);
                                AiCall(brain, "ActTick", Time.time);
                                AiCall(brain, "End", false);
                                passed = !AiGet<bool>(brain, "seized") && !AiGet<bool>(brain, "hidden") && !AiGet<bool>(brain, "token");
                            }
                        }
                        catch (Exception e) { passed = false; detail += " " + (e.InnerException ?? e); }
                        FxCheck($"NewAI forced lifecycle {pair.Key}/{i}", passed, detail);
                    }
                    Object.Destroy(brain.gameObject);
                }
                foreach (var a in Hostiles.Attack.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
                foreach (var root in AiSpawnedPoolRoots())
                    if (root != null && !poolsBefore.Contains(root)) ObjectPool.Recycle(root);
                yield return new WaitForSeconds(0.2f);
            }
            FxCheck("NewAI selected profile inventory", entries.Length > 0 && ordinal == entries.Length, $"profiles={ordinal}/{profiles.Count}");
            Plugin.Log.LogInfo("[NEWAI_DONE]");
        }

        static HashSet<GameObject> AiSpawnedPoolRoots()
        {
            var roots = new HashSet<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var root = t.root.gameObject;
                if (ObjectPool.IsSpawned(root)) roots.Add(root);
            }
            return roots;
        }

        IEnumerator AiCleanupProbe(System.Action cleanup, IEnumerator nested = null)
        {
            try
            {
                if (nested != null) yield return nested;
                while (true) yield return null;
            }
            finally { cleanup(); }
        }

        void AiBossLifecycleChecks()
        {
            var go = new GameObject("TK boss lifecycle probe");
            go.SetActive(false);
            var hm = go.AddComponent<HealthManager>(); hm.enabled = false; hm.hp = 100;
            var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 2f;
            var body = go.AddComponent<BoxCollider2D>(); body.offset = new Vector2(0.3f, 0.2f);
            var kit = go.AddComponent<EnemyAi.Bosses.GruzMotherKit>();
            kit.Begin(hm, new EnemyStats.Info { Hm = hm, Record = "BigFly", MaxHp = 100 });
            go.SetActive(true);
            try
            {
                int parent = 0, child = 0;
                var e = kit.Guard(AiCleanupProbe(() => parent++, AiCleanupProbe(() => child++)));
                FxCheck("NewAI boss nested routine starts", e.MoveNext(), "nested iterator driven by kit");
                ((IDisposable)e).Dispose();
                FxCheck("NewAI boss nested interruption cleans all levels", parent == 1 && child == 1, $"parent={parent} child={child}");
                int busyCleanup = 0;
                var withCleanup = (IEnumerator)AccessTools.Method(typeof(EnemyAi.Bosses.BossKit), "WithCleanup").Invoke(kit, new object[] { AiCleanupProbe(() => { }), (Action)(() => busyCleanup++) });
                e = kit.Guard(withCleanup); e.MoveNext(); ((IDisposable)e).Dispose();
                FxCheck("NewAI boss interrupted effect releases busy flag", busyCleanup == 1, $"cleanup={busyCleanup}");

                int exited = 0;
                var action = new EnemyAi.Bosses.RoutineAction { Kit = kit, Body = () => AiCleanupProbe(() => exited++) };
                var wrap = (IEnumerator)AccessTools.Method(typeof(EnemyAi.Bosses.RoutineAction), "Wrap").Invoke(action, null);
                wrap.MoveNext();
                action.OnExit(); action.OnExit();
                ((IDisposable)wrap).Dispose();
                FxCheck("NewAI boss state exit disposes once", exited == 1, $"cleanup={exited}");

                int deadCleanup = 0;
                e = kit.Guard(AiCleanupProbe(() => deadCleanup++)); e.MoveNext();
                hm.isDead = true;
                bool stopped = !e.MoveNext(); hm.isDead = false;
                FxCheck("NewAI boss death cleans routine", stopped && deadCleanup == 1, $"stopped={stopped} cleanup={deadCleanup}");

                int disabledCleanup = 0;
                e = kit.Guard(AiCleanupProbe(() => disabledCleanup++)); e.MoveNext();
                kit.enabled = false;
                FxCheck("NewAI boss disable immediately cleans routine", disabledCleanup == 1, $"cleanup={disabledCleanup}");
                kit.enabled = true;

                var recovery = go.AddComponent<EnemyAi.BodyRecovery>();
                Physics2D.SyncTransforms();
                Vector2 from = body.bounds.center, to = from + Vector2.right * 4f;
                recovery.Begin(kit, body, rb, null, to);
                FxCheck("NewAI boss recovery starts without teleport", Vector2.Distance(from, body.bounds.center) < 0.001f, $"center={body.bounds.center}");
                recovery.Step(0.02f);
                FxCheck("NewAI boss recovery bounded movement", Vector2.Distance(from, body.bounds.center) <= 0.161f && Vector2.Distance(from, body.bounds.center) > 0.1f, $"step={Vector2.Distance(from, body.bounds.center)}");
                Vector2 beforeHitch = body.bounds.center;
                recovery.Step(3f);
                FxCheck("NewAI boss recovery frame hitch cannot snap", Vector2.Distance(beforeHitch, body.bounds.center) <= 0.401f, $"step={Vector2.Distance(beforeHitch, body.bounds.center)}");
                for (int i = 0; i < 40 && recovery.Running; i++) recovery.Step(0.02f);
                FxCheck("NewAI boss recovery arrives and restores physics", !recovery.Running && Vector2.Distance(to, body.bounds.center) < 0.01f && !rb.isKinematic && rb.gravityScale == 2f, $"center={body.bounds.center} gravity={rb.gravityScale}");
                recovery.Begin(kit, body, rb, null, to + Vector2.up * 4f);
                recovery.enabled = false;
                FxCheck("NewAI boss recovery disable restores physics", !recovery.Running && !rb.isKinematic && rb.gravityScale == 2f, "disabled halfway");
                rb.bodyType = RigidbodyType2D.Static;
                recovery.Begin(hm, null, body, rb, null, (Vector2)body.bounds.center + Vector2.up);
                recovery.Restore();
                FxCheck("NewAI shared recovery preserves exact body type", rb.bodyType == RigidbodyType2D.Static && rb.gravityScale == 2f && recovery.enabled, "static body and disabled recovery reused");
                rb.bodyType = RigidbodyType2D.Dynamic;

                foreach (var probe in new[] {
                    (typeof(EnemyAi.Bosses.HornetKit), "Rise", "Hornet"),
                    (typeof(EnemyAi.Bosses.MimicSpiderKit), "Dive", "MimicSpider"),
                    (typeof(EnemyAi.Bosses.InfectedKnightKit), "WallDash", "InfectedKnight"),
                    (typeof(EnemyAi.Bosses.TraitorLordKit), "SkyDash", "TraitorLord"),
                    (typeof(EnemyAi.Bosses.MantisLordKit), "CeilingDrop", "MantisLord"),
                    (typeof(EnemyAi.Bosses.MantisLordKit), "WallSlash", "MantisLord"),
                    (typeof(EnemyAi.Bosses.SoulWarriorKit), "AirDash", "MageKnight") })
                {
                    var specific = (EnemyAi.Bosses.BossKit)go.AddComponent(probe.Item1);
                    specific.Begin(hm, new EnemyStats.Info { Hm = hm, Record = probe.Item3, MaxHp = 100 });
                    Quaternion rotation = go.transform.rotation;
                    rb.gravityScale = 2f;
                    var damage = go.GetComponent<DamageHero>() ?? go.AddComponent<DamageHero>();
                    damage.damageDealt = 1;
                    e = specific.Guard((IEnumerator)AccessTools.Method(probe.Item1, probe.Item2).Invoke(specific, null));
                    e.MoveNext();
                    ((IDisposable)e).Dispose();
                    FxCheck("NewAI boss interrupted move restores body " + probe.Item3 + "/" + probe.Item2, rb.gravityScale == 2f && rb.linearVelocity.sqrMagnitude < 0.001f && Quaternion.Angle(rotation, go.transform.rotation) < 0.1f && damage.damageDealt == 1, $"gravity={rb.gravityScale} velocity={rb.linearVelocity} touchDamage={damage.damageDealt}");
                }
                Vector3 blockedAt = go.transform.position;
                e = EnemyAi.Bosses.Batch2.Travel(kit, rb, (Vector2)blockedAt + Vector2.right * 10f, 50f, 1f);
                int attempts = 0;
                while (e.MoveNext() && attempts++ < 100) { }
                rb.linearVelocity = Vector2.zero;
                FxCheck("NewAI boss blocked travel never snaps to endpoint", attempts < 100 && Vector3.Distance(blockedAt, go.transform.position) < 0.001f, $"attempts={attempts} position={go.transform.position}");
                var mantis = go.GetComponent<EnemyAi.Bosses.MantisLordKit>();
                float originalLandingY = go.transform.position.y - 8f;
                var nativeY = new HutongGames.PlayMaker.FsmFloat { Value = originalLandingY };
                var landing = new HutongGames.PlayMaker.Actions.SetPosition { y = nativeY };
                AccessTools.Field(typeof(EnemyAi.Bosses.MantisLordKit), "landPosition").SetValue(mantis, landing);
                AccessTools.Field(typeof(EnemyAi.Bosses.MantisLordKit), "ceilingLanding").SetValue(mantis, true);
                AccessTools.Method(typeof(EnemyAi.Bosses.MantisLordKit), "PrepareLanding").Invoke(mantis, null);
                FxCheck("NewAI mantis custom landing keeps actual height", Mathf.Abs(landing.y.Value - go.transform.position.y) < 0.001f && Mathf.Abs(nativeY.Value - originalLandingY) < 0.001f, $"actual={go.transform.position.y} parameter={landing.y.Value} original={nativeY.Value} flag={AccessTools.Field(typeof(EnemyAi.Bosses.MantisLordKit), "ceilingLanding").GetValue(mantis)}");
                mantis.enabled = false;
                FxCheck("NewAI mantis landing restores native parameter on disable", ReferenceEquals(landing.y, nativeY), "original variable reference restored");
                var sly = go.AddComponent<EnemyAi.Bosses.NailsageKit>();
                sly.Begin(hm, new EnemyStats.Info { Hm = hm, Record = "Nailsage", MaxHp = 100 });
                AccessTools.Property(typeof(EnemyAi.Bosses.BossKit), "TargetHeight").SetValue(sly, 3f);
                rb.gravityScale = 2f;
                AccessTools.Method(typeof(EnemyAi.Bosses.NailsageKit), "HoldCyclone").Invoke(sly, null);
                FxCheck("NewAI sly cyclone holds gravity only during move", rb.gravityScale == 0f, "air cyclone");
                sly.enabled = false;
                FxCheck("NewAI sly cyclone disable restores gravity", rb.gravityScale == 2f, "interrupted cyclone");

                var aimFsm = go.AddComponent<PlayMakerFSM>();
                var originalAim = new HutongGames.PlayMaker.FsmGameObject { UseVariable = true, Name = "Hero" };
                var aimAction = new AiAimProbe { Target = originalAim };
                var aimState = new HutongGames.PlayMaker.FsmState(aimFsm.Fsm) { Name = "Aim", Actions = new HutongGames.PlayMaker.FsmStateAction[] { aimAction } };
                aimAction.Init(aimState);
                aimFsm.Fsm.States = new[] { aimState };
                var proxy = EnemyAi.Bosses.Batch4.Proxy(kit, "pool aim regression");
                int retargeted = EnemyAi.Bosses.Batch4.Retarget(aimFsm, proxy);
                FxCheck("NewAI pooled aim replaces hero reference", retargeted == 1 && aimAction.Target.Value == proxy, $"retargeted={retargeted}");
                proxy.GetComponent<EnemyAi.Bosses.KitAimProxy>().Restore();
                FxCheck("NewAI pooled aim restores original reference", ReferenceEquals(aimAction.Target, originalAim), "pooled action must not retain destroyed scene proxy");
                Object.Destroy(proxy);

                var markoth = go.AddComponent<EnemyAi.Bosses.GhostMarkothKit>();
                markoth.Begin(hm, new EnemyStats.Info { Hm = hm, Record = "GhostMarkoth", MaxHp = 100 });
                var shield = new GameObject("Shield scale regression"); shield.transform.SetParent(go.transform); shield.transform.localScale = Vector3.one * 2f;
                AccessTools.Method(typeof(EnemyAi.Bosses.GhostMarkothKit), "Grow").Invoke(markoth, null);
                AccessTools.Method(typeof(EnemyAi.Bosses.GhostMarkothKit), "Grow").Invoke(markoth, null);
                FxCheck("NewAI markoth shield growth never compounds", Vector3.Distance(shield.transform.localScale, Vector3.one * 2.6f) < 0.001f, $"scale={shield.transform.localScale}");
                markoth.enabled = false;
                FxCheck("NewAI markoth shield interruption restores scale", Vector3.Distance(shield.transform.localScale, Vector3.one * 2f) < 0.001f, "original shield scale");
            }
            finally { Object.Destroy(go); }
        }

        IEnumerator AiContinuousMoveChecks()
        {
            var go = new GameObject("TK continuous move probe");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, 1000f, 0f);
            var hm = go.AddComponent<HealthManager>();
            hm.enabled = false;
            hm.hp = 100;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.4f, 0.4f);
            var b = go.AddComponent<Brain>();
            b.enabled = false;
            var p = new Profile { Key = "continuous", Flyer = true };
            p.Moves.Add(new Move { Kind = Kind.Lunge, Speed = 20f, Time = 0.3f, Vertical = true });
            b.Init(hm, new EnemyStats.Info { Hm = hm, Record = p.Key, MaxHp = 100 }, p);
            go.SetActive(true);
            var wall = new GameObject("TK continuous probe wall") { layer = 8 };
            wall.transform.position = go.transform.position + Vector3.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 4f);
            try
            {
                Vector2 start = rb.position;
                AccessTools.Property(typeof(Brain), "TargetCenter").SetValue(b, start + Vector2.right * 3f);
                AiCall(b, "Begin", 0, Time.time);
                AiCall(b, "StartAct", Time.time);
                FxCheck("NewAI lunge never snaps or hides", Vector2.Distance(start, rb.position) < 0.001f && !AiGet<bool>(b, "hidden") && box.enabled, $"start={start} actual={rb.position}");
                AiSet(b, "aimPoint", start + Vector2.right * 2f);
                float maxStep = 0f;
                for (int i = 0; i < 14; i++)
                {
                    Vector2 before = rb.position;
                    AiCall(b, "Step", Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                    maxStep = Mathf.Max(maxStep, Vector2.Distance(before, rb.position));
                }
                FxCheck("NewAI lunge continuous bounded motion", maxStep > 0f && maxStep <= 10f * Time.fixedDeltaTime + 0.03f, $"maxStep={maxStep}");
                FxCheck("NewAI lunge stops before wall", box.bounds.max.x <= wall.GetComponent<BoxCollider2D>().bounds.min.x + 0.05f && AiGet<bool>(b, "landed"), $"x={rb.position.x}");
                AiCall(b, "End", false);
                FxCheck("NewAI blocked lunge releases body", !AiGet<bool>(b, "seized") && rb.gravityScale == 0f, b.DevState);
                Object.Destroy(wall);
                yield return null;
                p.Moves[0] = new Move { Kind = Kind.Drop };
                AiCall(b, "Begin", 0, Time.time);
                AiCall(b, "StartAct", Time.time);
                go.transform.position += Vector3.down * 2f;
                Vector2 fallen = b.Position;
                AiCall(b, "ActTick", Time.time + 3f);
                FxCheck("NewAI pit timeout never snaps to ceiling", Vector2.Distance(fallen, b.Position) <= 24f * Time.deltaTime + 0.03f && AiGet<bool>(b, "dropping"), $"before={fallen} after={b.Position}");
                for (int i = 0; i < 10 && !AiGet<bool>(b, "landed"); i++) AiCall(b, "DropReturn", 0.02f);
                FxCheck("NewAI pit return terminates", AiGet<bool>(b, "landed"), b.Position.ToString());
                AiCall(b, "End", false);
                FxCheck("NewAI pit return releases physics", !AiGet<bool>(b, "dropping") && rb.bodyType == RigidbodyType2D.Dynamic && rb.gravityScale == 0f, b.DevState);
            }
            finally { Object.Destroy(go); Object.Destroy(wall); }
        }

        IEnumerator AiEffectChecks()
        {
            yield return new WaitForSeconds(2f);
            var hero = HeroController.SilentInstance;
            Vector2 origin = hero.transform.position;
            var attacks = new List<Hostiles.Attack>();
            int index = 0;
            foreach (Look look in Enum.GetValues(typeof(Look)))
            {
                var style = Hostiles.StyleOf(look);
                bool loaded = true;
                for (int f = 0; f < style.Frames; f++) loaded &= Hostiles.Frame(style, f) != null;
                FxCheck("NewAI effect texture " + look, loaded, style.Sheet);
                var m = new Move { Look = look, Size = 2f, Life = 3f };
                var shot = Hostiles.Fire(null, null, m, origin + new Vector2(-4f + index % 6 * 1.6f, 2f + index / 6 * 1.2f), Vector2.zero);
                shot.Steered = true;
                attacks.Add(shot);
                index++;
            }
            yield return new WaitForSeconds(0.2f);
            Shot(GameManager.instance.sceneName, "newai_effects_gallery");
            AiRenderCapture("newai_effects_camera.png");
            FxCheck("NewAI effect renderer available", Effects.FxBatch.Instance != null && Effects.FxBatch.Instance.DevDrawCount > 0, "draw calls recorded; screenshot requires visual inspection");
            foreach (var a in attacks) if (a != null) Object.Destroy(a.gameObject);
            // Nothing drawn while an area waits (no warning box, line or ring: the user's, 2026-10-23).
            foreach (Hostiles.Zone.Shape shape in Enum.GetValues(typeof(Hostiles.Zone.Shape)))
            {
                var visual = new Hostiles.Visual();
                int before = Effects.FxBatch.Instance.DevDrawCount;
                Hostiles.DrawZone(Hostiles.StyleOf(Look.Crystal), shape, false, origin, 1f, 1f, 1f, 1f, 0f, Time.time, ref visual);
                FxCheck($"NewAI no warning drawn {shape}", Effects.FxBatch.Instance.DevDrawCount == before, "an area waiting draws nothing");
            }
            index = 0;
            GameObject waveFloor = null;
            foreach (Hostiles.Zone.Shape shape in Enum.GetValues(typeof(Hostiles.Zone.Shape)))
            {
                var m = new Move { Look = Look.Crystal, Width = 2f, Height = 3f, Radius = 1.5f, Length = 2f };
                attacks.Add(Hostiles.Area(null, null, m, shape, origin + new Vector2(-5f + index * 2f, 2f), 1f, 1f));
                attacks.Add(Hostiles.Area(null, null, m, shape, origin + new Vector2(-5f + index * 2f, 4.5f), 0f, 1f));
                if (shape == Hostiles.Zone.Shape.Wave)
                {
                    waveFloor = new GameObject("TK wave gallery support") { layer = 8 };
                    waveFloor.transform.position = origin + new Vector2(-5f + index * 2f, 4.35f);
                    waveFloor.AddComponent<BoxCollider2D>().size = new Vector2(1.5f, 0.2f);
                }
                index++;
            }
            yield return new WaitForSeconds(0.2f);
            Shot(GameManager.instance.sceneName, "newai_zone_gallery");
            AiRenderCapture("newai_zones_camera.png");
            yield return new WaitForSeconds(0.15f);
            foreach (var a in attacks) if (a != null) Object.Destroy(a.gameObject);
            if (waveFloor != null) Object.Destroy(waveFloor);
            yield return null;
        }

        void AiRenderCapture(string name)
        {
            if (Environment.GetEnvironmentVariable("TK_NEWAI_SKIP_CAPTURE") == "1")
            {
                Plugin.Log.LogInfo("[NEWAI_CAPTURE_SKIPPED] " + name + "; no visual assertion");
                return;
            }
            // ScreenCapture is black when the independent test window is hidden/minimized.
            // Render the actual game camera and its live effect renderers to a texture instead.
            var camera = GameCameras.instance.mainCamera;
            var priorTarget = camera.targetTexture;
            var priorRect = camera.rect;
            float priorAspect = camera.aspect;
            var scaler = camera.GetComponent<CameraRenderScaled>();
            bool scaled = scaler != null && scaler.enabled;
            var priorActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                Plugin.Log.LogInfo($"[NEWAI_CAMERA] {camera.name} enabled={camera.enabled} mask={camera.cullingMask} pixel={camera.pixelWidth}x{camera.pixelHeight} aspect={camera.aspect} rect={camera.rect}");
                target.Create();
                AiCaptureRedirectGuard.Active = camera;
                // CameraRenderScaled.OnPreCull overwrites targetTexture (including clearing
                // it at full resolution). Hold only that redirect while rendering our target.
                camera.targetTexture = target;
                // A minimized window may leave a zero-sized camera viewport. The offscreen
                // image has its own fixed dimensions, independent of that window state.
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.aspect = 1280f / 720f;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, name), texture.EncodeToPNG());
                bool visible = texture.GetPixels32().Count(c => c.r > 10 || c.g > 10 || c.b > 10) > 1000;
                FxCheck("NewAI camera image " + name, visible, "actual camera render; visual inspection still required");
                if (!visible)
                {
                    Plugin.Log.LogInfo($"[NEWAI_CAMERA_DIAG] position={camera.transform.position} rotation={camera.transform.eulerAngles} projection={camera.projectionMatrix} components={string.Join(",", camera.GetComponents<MonoBehaviour>().Select(c => c.GetType().Name + ":" + c.enabled))}");
                    var diagnostic = new GameObject("TK AI diagnostic camera");
                    var copy = diagnostic.AddComponent<Camera>();
                    copy.CopyFrom(camera);
                    copy.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                    copy.enabled = false;
                    copy.targetTexture = target;
                    copy.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                    texture.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, "diagnostic_" + name), texture.EncodeToPNG());
                    Plugin.Log.LogInfo($"[NEWAI_CAMERA_DIAG] without-postfx-visible={texture.GetPixels32().Count(c => c.r > 10 || c.g > 10 || c.b > 10)}");
                    Object.Destroy(diagnostic);
                }
            }
            finally
            {
                camera.targetTexture = priorTarget;
                AiCaptureRedirectGuard.Active = null;
                camera.rect = priorRect;
                camera.aspect = priorAspect;
                if (scaler != null) scaler.enabled = scaled;
                RenderTexture.active = priorActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
            }
        }
    }

    internal sealed class AiAimProbe : HutongGames.PlayMaker.FsmStateAction
    {
        public HutongGames.PlayMaker.FsmGameObject Target;
    }

    // Unity may invoke camera messages independently of a MonoBehaviour's enabled flag.
    // This guard is active only for a development probe's synchronous offscreen Render.
    [HarmonyPatch]
    internal static class AiCaptureRedirectGuard
    {
        internal static Camera Active;
        [HarmonyPrefix, HarmonyPatch(typeof(CameraRenderScaled), "OnPreCull")]
        static bool BeforeCull(CameraRenderScaled __instance)
        {
            if (Active == null || __instance.GetComponent<Camera>() != Active) return true;
            Plugin.Log.LogInfo("[NEWAI_CAMERA] held native target redirection for offscreen capture");
            return false;
        }
    }
}
