using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Items
{
    /// <summary>
    /// Creative: the monster book's spawn button. A boss is fought in its own arena, all of it
    /// (<see cref="BossArena"/>). Any other enemy: a fresh copy of it is put ten tiles in front of the player, as the
    /// game itself would have it. Enemies live in their scenes, so one is copied from a scene that has it, the freshest
    /// one first (<see cref="Rank"/>): the mod's own untouched template of it (World.EnemyRespawn), a prefab its scene
    /// makes it from (the Colosseum's cages), one that has never started; else its scene (the roster's) is loaded beside
    /// the one played and switched off the moment it loads, before anything in it starts (no music, colours or lights of
    /// its own), the enemy copied out, the scene let go; only for an enemy of the scene being played is a live one copied.
    /// The copy is made whole (<see cref="Sanitize"/>): its full health, alive, out of any arena's count, no tie to the
    /// original's saved death or to whom the original tells it died, no state the mod gave the original (its new AI,
    /// knockback, fading: the copy gets its own as it starts), each of its scripts on or off as its scene had it. Then
    /// <see cref="BookCopy"/> does what the enemy's spawner or waker does in its scene (a hatchling's SPAWN, a sleeper's
    /// WAKE...), and when it dies takes it away (the pooled ones, which their own script would put back in their cage or
    /// pool and keep, too).
    /// </summary>
    internal static class BookSpawner
    {
        static bool busy;
        /// <summary>When the spawn under way began: one stuck past <see cref="BusyMost"/> (a load that never ends) frees the book again.</summary>
        static float busySince;
        const float BusyMost = 12f;
        static string switchOff;
        public static string DevLast = "";
        public static int DevSpawned;

        public static void Spawn(string key)
        {
            var mod = TerraMod.Instance;
            if (busy && Time.unscaledTime - busySince < BusyMost || mod == null || string.IsNullOrEmpty(key) || HeroController.SilentInstance == null) return;
            if (busy) Plugin.Log.LogWarning("[图鉴] 上一次生成过了 12 秒还没结束，放弃它");
            if (BossArena.Fight(key)) return;
            string record = key.Split('@')[0];
            // Use a combat miner, not Myla's initially friendly quest body in Crossroads_45.
            // Death Loodles belong to the Trial of the Fool, not the Silver trial.
            string source = record == "ZombieMiner" ? "Mines_04" : record == "ColHopper" ? "Room_Colosseum_Gold" : EnemyDrops.SceneOf(record);
            mod.StartCoroutine(Run(record, source));
        }

        /// <summary>How fresh a source is: 0 the mod's template, 1 a prefab, 2 never started, 3 alive and running, 4 dead.</summary>
        static int Rank(HealthManager hm)
        {
            if (hm == null) return 9;
            var t = hm.transform.parent;
            if (t != null && t.name == "TK Enemy Templates") return 0;
            if (!hm.gameObject.scene.IsValid()) return 1;
            if (!Started(hm)) return 2;
            return hm.isDead ? 4 : 3;
        }

        /// <summary>The mod saw it start (Items.EnemyStats registers each enemy as it starts).</summary>
        static bool Started(HealthManager hm) => known != null && known.Contains(hm.GetInstanceID());

        static readonly IDictionary known = AccessTools.Field(typeof(EnemyStats), "known")?.GetValue(null) as IDictionary;

        /// <summary>The freshest of a record in a scene: its enemies, the mod's templates, and the prefabs its scripts make enemies from.</summary>
        static HealthManager Find(string record, Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            HealthManager best = null;
            int bestRank = 9;
            void Offer(HealthManager hm)
            {
                if (hm == null || UI.EnemyHover.RecordOf(hm) != record) return;
                int r = Rank(hm);
                if (r < bestRank) { best = hm; bestRank = r; }
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var hm in root.GetComponentsInChildren<HealthManager>(true)) Offer(hm);
                // A prefab a script makes the enemy from (a Colosseum cage's Enemy Type): the freshest of all but a template.
                if (bestRank <= 1) continue;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm == null || fsm.FsmVariables == null) continue;
                    foreach (var v in fsm.FsmVariables.GameObjectVariables)
                    {
                        var go = v?.Value;
                        if (go != null && !go.scene.IsValid()) Offer(go.GetComponent<HealthManager>());
                    }
                }
            }
            // Some cages/quest spawners keep their enemy in an action or component field,
            // not a GameObjectVariable. Loading that scene still loads the untouched prefab.
            if (bestRank > 1)
                foreach (var hm in Resources.FindObjectsOfTypeAll<HealthManager>())
                    if (hm != null && !hm.gameObject.scene.IsValid()) Offer(hm);
            return best;
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != switchOff) return;
            // Before any Start in it: nothing of it may run beside the scene played.
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        }

        static IEnumerator Run(string record, string sceneName)
        {
            busy = true;
            busySince = Time.unscaledTime;
            // Whatever happens on the way (an error in a copy, the routine stopped), the book is free again after.
            try
            {
                var steps = Steps(record, sceneName);
                while (true)
                {
                    object cur;
                    try
                    {
                        if (!steps.MoveNext()) break;
                        cur = steps.Current;
                    }
                    catch (System.Exception e)
                    {
                        Plugin.Log.LogWarning($"[图鉴] 生成 {record} 出错：{e}");
                        TerraMod.Instance?.PlaySound("Menu_Cancel", 0.7f);
                        break;
                    }
                    yield return cur;
                }
            }
            finally
            {
                if (switchOff != null) { USM.sceneLoaded -= OnLoaded; switchOff = null; }
                RestorePhysicsBridge();
                busy = false;
            }
        }

        static IEnumerator Steps(string record, string sceneName)
        {
            HealthManager source = null;
            for (int i = 0; i < USM.sceneCount; i++)
            {
                var s = USM.GetSceneAt(i);
                if (World.TerrainSystem.Find(s.name) == null) continue;
                var found = Find(record, s);
                if (found != null && (source == null || Rank(found) < Rank(source))) source = found;
            }
            Scene borrowed = default;
            // Only a live one here (or none): a fresh one from its own scene, when that isn't loaded.
            if ((source == null || Rank(source) >= 3) && !string.IsNullOrEmpty(sceneName) && !USM.GetSceneByName(sceneName).isLoaded)
            {
                switchOff = sceneName;
                USM.sceneLoaded += OnLoaded;
                var load = USM.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (load != null) while (!load.isDone) yield return null;
                USM.sceneLoaded -= OnLoaded;
                switchOff = null;
                borrowed = USM.GetSceneByName(sceneName);
                var fresh = Find(record, borrowed);
                if (fresh != null && (source == null || Rank(fresh) < Rank(source))) source = fresh;
            }
            var hero = HeroController.SilentInstance;
            var gm = GameManager.UnsafeInstance;
            if (source != null && hero != null && gm != null)
            {
                int rank = Rank(source);
                var copy = Copy(source, out var copyHm);
                copy.name = source.gameObject.name.Replace("(TK)", "") + "(图鉴)";
                var here = USM.GetSceneByName(gm.sceneName);
                if (here.IsValid()) USM.MoveGameObjectToScene(copy, here);
                copy.transform.position = Spot(hero, source.transform.position.z);
                var book = copy.AddComponent<BookCopy>();
                // A room's own (not a prefab's): its places in that room brought here.
                if (source.gameObject.scene.IsValid()) book.Rehome(source.transform.position, copy.transform.position, here);
                RestorePhysicsBridge();
                copy.SetActive(true);
                DevSpawned++;
                string from = rank == 0 ? "模板" : rank == 1 ? "预制体" : rank == 2 ? "未启动的原版" : "场景里的活体";
                DevLast = $"{record}：从 {(source.gameObject.scene.IsValid() ? source.gameObject.scene.name : "预制体")} 的{from}复制，放在 {copy.transform.position:F1}";
                Plugin.Log.LogInfo($"[图鉴] 生成 {DevLast}");
            }
            else
            {
                DevLast = $"{record}：在 {sceneName ?? "（没有场景）"} 里没找到";
                Plugin.Log.LogWarning($"[图鉴] 生成失败 {DevLast}");
                TerraMod.Instance?.PlaySound("Menu_Cancel", 0.7f);
            }
            if (borrowed.IsValid() && borrowed.isLoaded)
            {
                var unload = USM.UnloadSceneAsync(borrowed);
                while (unload != null && !unload.isDone) yield return null;
            }
        }

        // Every borrowed room's Awake overwrites this native static pointer. Its unload otherwise
        // leaves it pointing at a destroyed FSM, and subsequent proxies disable themselves in Start.
        static readonly System.Reflection.FieldInfo physicsBridge = AccessTools.Field(typeof(PlayMakerUnity2d), "fsmProxy");

        static void RestorePhysicsBridge()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null) return;
            foreach (var bridge in Object.FindObjectsByType<PlayMakerUnity2d>(FindObjectsSortMode.None))
            {
                if (bridge.gameObject.scene.name != gm.sceneName || !bridge.gameObject.activeInHierarchy) continue;
                var fsm = bridge.GetComponent<PlayMakerFSM>();
                if (fsm != null) physicsBridge?.SetValue(null, fsm);
                return;
            }
        }

        static readonly AccessTools.FieldRef<EnemyDeathEffects, GameObject> corpse = AccessTools.FieldRefAccess<EnemyDeathEffects, GameObject>("corpse");
        static readonly AccessTools.FieldRef<HealthManager, GameObject> sendKilledTo = AccessTools.FieldRefAccess<HealthManager, GameObject>("sendKilledTo");
        static readonly AccessTools.FieldRef<HealthManager, GameObject> sendKilledToObject = AccessTools.FieldRefAccess<HealthManager, GameObject>("sendKilledToObject");
        static readonly AccessTools.FieldRef<HealthManager, string> sendKilledToName = AccessTools.FieldRefAccess<HealthManager, string>("sendKilledToName");

        /// <summary>An inactive copy of the source, made whole (<see cref="Sanitize"/>).</summary>
        static GameObject Copy(HealthManager source, out HealthManager copyHm)
        {
            var src = source.gameObject;
            // Its own colours, not half see-through from a steady second.
            if (src.scene.IsValid()) SteadyFade.End(src);
            // Its health in full: what it started with as the mod saw it, else its own (it hasn't started).
            int hp = Started(source) ? Mathf.Max(source.hp, EnemyStats.Of(source).MaxHp) : source.hp;
            var own = src.GetComponentsInChildren<PlayMakerFSM>(true);
            var ede = src.GetComponent<EnemyDeathEffects>();
            var premade = ede != null ? corpse(ede) : null;
            int premadeIndex = premade != null && premade.transform.parent == src.transform ? premade.transform.GetSiblingIndex() : -1;
            GameObject copy;
            if (src.scene.IsValid())
            {
                bool was = src.activeSelf;
                src.SetActive(false);
                copy = Object.Instantiate(src);
                src.SetActive(was);
            }
            else
            {
                // A prefab is never switched off itself: its copy is, as soon as it is made (as the cages do).
                copy = Object.Instantiate(src);
                copy.SetActive(false);
            }
            copyHm = copy.GetComponent<HealthManager>();
            Sanitize(copy, copyHm, own, premadeIndex, hp);
            return copy;
        }

        /// <summary>
        /// A copy made as the game makes the enemy: alive with its full health, counted in no arena, telling no one of its
        /// death (the original's gate or cage), not tied to the original's saved death (its PersistentBoolItem: the copy
        /// would vanish with a dead original, or kill it with its own death), without the original's premade corpse (its
        /// own is made as it starts), none of the mod's state on the original (re-made as the copy starts), and each
        /// script on or off as its scene had it on the original.
        /// </summary>
        static void Sanitize(GameObject copy, HealthManager hm, PlayMakerFSM[] own, int premadeIndex, int hp)
        {
            var copies = copy.GetComponentsInChildren<PlayMakerFSM>(true);
            for (int i = 0; i < copies.Length && copies.Length == own.Length; i++)
                copies[i].enabled = World.DeferredFsms.WasOn(own[i]) ?? own[i].enabled;
            if (premadeIndex >= 0 && premadeIndex < copy.transform.childCount) Object.DestroyImmediate(copy.transform.GetChild(premadeIndex).gameObject);
            foreach (var p in copy.GetComponentsInChildren<PersistentBoolItem>(true)) Object.DestroyImmediate(p);
            foreach (var p in copy.GetComponentsInChildren<PersistentIntItem>(true)) Object.DestroyImmediate(p);
            // The mod's own (a brain, a boss's kit, knockback, a fade...): the copy's start brings its own.
            foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && b.GetType().Assembly == typeof(BookSpawner).Assembly) Object.DestroyImmediate(b);
            if (hm != null)
            {
                hm.SetIsDead(false);
                hm.SetBattleScene(null);
                if (hp > 0) hm.hp = hp;
                sendKilledTo(hm) = null;
                sendKilledToObject(hm) = null;
                sendKilledToName(hm) = null;
            }
        }

        /// <summary>Ten tiles in front of the player, up out of anything solid there.</summary>
        static Vector3 Spot(HeroController hero, float z)
        {
            float facing = hero.cState.facingRight ? 1f : -1f;
            Vector2 at = (Vector2)hero.transform.position + new Vector2(facing * 10f * World.CellGrid.CellSize, 0.5f);
            for (int k = 0; k < 20 && World.TerraWorld.IsSolidAt(at); k++) at.y += World.CellGrid.CellSize;
            return new Vector3(at.x, at.y, z);
        }
    }

    /// <summary>
    /// A book copy's own spawner and undertaker. As it starts, its scripts that wait for what their spawner, waker or
    /// arena sends them in their scene (an Aspid Hatchling or a Zoteling waiting in its cage for SPAWN, a Volatile Zoteling
    /// for BALLOON SPAWN, a Shrumal Ogre or
    /// an Ooma asleep for WAKE, an Obble for START...) get it, once each. When it dies it goes: its death (the game's
    /// own), or its script putting it back to wait (a pooled one: the hatchlings reset in their cage, the Zotelings in
    /// their pool), or its body switched off.
    /// </summary>
    internal sealed class BookCopy : MonoBehaviour
    {
        /// <summary>The events a spawner or waker sends, in the order tried (a wall sentry waits for WAKE or IDLE).</summary>
        static readonly string[] wakes = { "SPAWN", "BALLOON SPAWN", "WAKE", "START", "BATTLE START", "GO", "APPEAR", "IDLE" };

        HealthManager hm;
        readonly List<(PlayMakerFSM Fsm, string State)> woken = new List<(PlayMakerFSM, string)>();
        readonly List<GameObject> points = new List<GameObject>();
        float born;
        bool dying;

        /// <summary>Its room's bounds its script keeps as numbers (the Pale Lurker's Left/Right Pos, Mid Y), by axis.</summary>
        static readonly string[] boundsX = { "Left Pos", "Right Pos", "Min X", "Max X", "X Min", "X Max" };
        static readonly string[] boundsY = { "Mid Y", "Min Y", "Max Y", "Y Min", "Y Max", "Floor Y", "Ground Y" };
        /// <summary>A place it goes to by number (a Mantis Traitor's other spawn spot), by axis.</summary>
        static readonly string[] spotsX = { "Alt Spawn X", "Spawn X" };
        static readonly string[] spotsY = { "Alt Spawn Y", "Spawn Y" };

        /// <summary>
        /// Its script's places in its own room, brought to where it is: a set of points it picks from (the Pale Lurker's
        /// Tele High and Tele Low, where it digs to) made again here at the same offsets, each moved out of the rock; its
        /// room's bounds moved by as much. Else it went to its own room's spots, here inside the walls.
        /// </summary>
        public void Rehome(Vector3 was, Vector3 now, UnityEngine.SceneManagement.Scene here)
        {
            Vector2 by = now - was;
            int moved = 0;
            foreach (var fsm in GetComponentsInChildren<PlayMakerFSM>(true))
            {
                var vars = fsm.FsmVariables;
                foreach (var v in vars.GameObjectVariables)
                {
                    var set = v.Value;
                    // A room's object (not a prefab's, as a projectile's), not this one's own, of bare points only.
                    if (set == null || set.transform.childCount == 0 || !set.scene.IsValid() || set.scene == here || set.scene == gameObject.scene) continue;
                    if (set.transform.IsChildOf(transform) || set.scene.name == "DontDestroyOnLoad" || !BarePoints(set.transform)) continue;
                    var holder = new GameObject(set.name + "(图鉴)");
                    if (here.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, here);
                    holder.transform.position = now;
                    // Its points about it as they lay about it there, drawn in to eight units at most (its own room's
                    // spread put them thirty away, past this room's walls).
                    float far = 0f;
                    for (int i = 0; i < set.transform.childCount; i++) far = Mathf.Max(far, ((Vector2)(set.transform.GetChild(i).position - was)).magnitude);
                    float k8 = far > 8f ? 8f / far : 1f;
                    for (int i = 0; i < set.transform.childCount; i++)
                    {
                        // Across as it was there; up and down, on this room's ground under it (a point under the floor here
                        // was a dig into the rock).
                        Vector2 p = new Vector2(now.x + (set.transform.GetChild(i).position.x - was.x) * k8, now.y);
                        var ground = Physics2D.Raycast(p + Vector2.up * 4f, Vector2.down, 14f, 1 << 8);
                        if (ground.collider != null && !ground.collider.isTrigger) p.y = ground.point.y + 1f;
                        var point = new GameObject(set.transform.GetChild(i).name);
                        point.transform.SetParent(holder.transform, false);
                        point.transform.position = new Vector3(p.x, p.y, set.transform.GetChild(i).position.z);
                    }
                    v.Value = holder;
                    points.Add(holder);
                    moved++;
                }
                foreach (var f in vars.FloatVariables)
                {
                    if (System.Array.IndexOf(boundsX, f.Name) >= 0) { f.Value += by.x; moved++; }
                    else if (System.Array.IndexOf(boundsY, f.Name) >= 0) { f.Value += by.y; moved++; }
                    else if (System.Array.IndexOf(spotsX, f.Name) >= 0)
                    {
                        // Its other spot on its side as there, eight units at most and short of a wall.
                        float d = Mathf.Clamp(f.Value - was.x, -8f, 8f);
                        var wall = Physics2D.Raycast((Vector2)now + Vector2.up * 0.5f, new Vector2(Mathf.Sign(d), 0f), Mathf.Abs(d), 1 << 8);
                        if (wall.collider != null && !wall.collider.isTrigger) d = Mathf.Sign(d) * Mathf.Max(0f, wall.distance - 1f);
                        f.Value = now.x + d;
                        moved++;
                    }
                    else if (System.Array.IndexOf(spotsY, f.Name) >= 0) { f.Value = now.y + Mathf.Clamp(f.Value - was.y, -1f, 3f); moved++; }
                }
            }
            // A Soul Twister's teleports pick a spot in a "Teleplane" of its room: a room without one, one round it here.
            if (Teleports(this) && !HasTeleplane(here))
            {
                try
                {
                    var plane = new GameObject("Teleplane(图鉴)") { tag = "Teleplane", layer = 2 };
                    if (here.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(plane, here);
                    plane.transform.position = now + Vector3.up * 2f;
                    var box = plane.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.size = new Vector2(18f, 6f);
                    points.Add(plane);
                    moved++;
                }
                catch (UnityException) { }
            }
            if (moved > 0) Plugin.Log.LogInfo($"[图鉴] {name} 原房间里的点位和边界搬到这里：{moved} 处");
        }

        static bool Teleports(BookCopy c)
        {
            foreach (var fsm in c.GetComponentsInChildren<PlayMakerFSM>(true))
                foreach (var st in fsm.FsmStates)
                    foreach (var a in st.Actions)
                        if (a is HutongGames.PlayMaker.Actions.GetRandomObject r && r.withTag != null && r.withTag.Value == "Teleplane") return true;
            return false;
        }

        static bool HasTeleplane(UnityEngine.SceneManagement.Scene here)
        {
            try
            {
                foreach (var g in GameObject.FindGameObjectsWithTag("Teleplane")) if (g.scene == here) return true;
            }
            catch (UnityException) { }
            return false;
        }

        static bool BarePoints(Transform set)
        {
            for (int i = 0; i < set.childCount; i++)
                if (set.GetChild(i).GetComponents<Component>().Length > 1 || set.GetChild(i).childCount > 0) return false;
            return true;
        }

        void Start()
        {
            born = Time.time;
            hm = GetComponent<HealthManager>();
            if (hm != null) hm.OnDeath += Died;
            StartCoroutine(Wake());
        }

        void OnDestroy()
        {
            if (hm != null) hm.OnDeath -= Died;
            foreach (var p in points) if (p != null) Destroy(p);
        }

        IEnumerator Wake()
        {
            // Over its first second and a half: a script may pass a few states (or a wait) before it settles to wait.
            var asked = new HashSet<PlayMakerFSM>();
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                foreach (var fsm in GetComponentsInChildren<PlayMakerFSM>())
                {
                    if (fsm == null || !fsm.enabled || asked.Contains(fsm)) continue;
                    var state = fsm.Fsm?.ActiveState;
                    if (state == null) continue;
                    foreach (var ev in wakes)
                    {
                        bool waits = false;
                        foreach (var tr in state.Transitions) if (tr.EventName == ev) { waits = true; break; }
                        if (!waits) continue;
                        asked.Add(fsm);
                        // A spawned one (SPAWN) goes back to this state only when its script recycles it on death.
                        if (ev == "SPAWN" || ev == "BALLOON SPAWN") woken.Add((fsm, state.Name));
                        Plugin.Log.LogInfo($"[图鉴] {name} 的 {fsm.FsmName} 停在 {state.Name}，照它在场景里那样发 {ev}");
                        fsm.SendEvent(ev);
                        break;
                    }
                }
                yield return null;
            }
        }

        void Died()
        {
            if (!dying) StartCoroutine(Away(0.2f));
        }

        void Update()
        {
            if (dying) return;
            // Its script put it back to wait for SPAWN where it was spawned from (a pooled one's death): gone.
            foreach (var (fsm, state) in woken)
                if (fsm != null && Time.time - born > 1f && fsm.ActiveStateName == state) { StartCoroutine(Away(0f)); return; }
            if (hm != null && hm.isDead) StartCoroutine(Away(0.2f));
        }

        void OnDisable()
        {
            // Switched off by its death (the game's recycling): gone for good.
            if (!dying && hm != null && (hm.isDead || hm.hp <= 0) && gameObject != null) Destroy(gameObject);
        }

        IEnumerator Away(float after)
        {
            dying = true;
            if (after > 0f) yield return new WaitForSeconds(after);
            if (this != null && gameObject != null) Destroy(gameObject);
        }
    }
}
