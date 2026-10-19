using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Net
{
    /// <summary>
    /// What the game running a scene's enemies shows there besides the enemies themselves, the same in the others' games:
    /// <list type="bullet">
    /// <item>What its fight spawns: a boss's shots, spikes, shockwaves, roars and slashes, a summoner's effects (a FSM of
    /// an enemy or of its room making an object, from the global pool or by CreateObject). Each one sent with its look and
    /// its drawn children fifteen times a second; the others make their own copy from the same prefab, stripped to its
    /// pictures (no scripts, no colliders: Net.EnemyAttacks carries what it hurts), moved and animated as the owner's, gone
    /// when the owner's is. An enemy so spawned is a puppet instead (Enemies.Adopt). What a hit or a death makes (blood,
    /// corpses, geo) each game makes for itself (Enemies: the hit's effects, the puppet's own death).</item>
    /// <item>A boss room's own objects outside its enemies (the boss scene laid over the room, and what is under the room's
    /// battle gates' roots): switched on and off, moved, animated, their colliders on and off as in the owner's game (the iron
    /// gates falling, the floor spikes coming up). Only what changed, and a share of the rest each time round.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch]
    internal static class SceneMirror
    {
        const byte KSpawns = 1, KStatic = 2;
        public static int DevNoted, DevSpawnsSent, DevCopies, DevCopiesMissing, DevStaticSent, DevStaticApplied;
        public static string DevMissing = "";

        // ================================================================== the owner's side: what the fight spawns

        sealed class Spawn
        {
            public GameObject Go;
            public int Id, Sends;
            public string Scene, Prefab;
            public List<(Transform Tr, string Path)> Parts;
        }

        static readonly Dictionary<int, Spawn> spawns = new Dictionary<int, Spawn>();
        /// <summary>What this player's Knight spawned (its FSMs: spells, nail arts), sent to the others whatever scene owner.</summary>
        static readonly Dictionary<int, Spawn> mine = new Dictionary<int, Spawn>();
        public static int DevMineSent;
        static int nextId = 1;
        /// <summary>
        /// A HealthManager's death or hit in progress, with the FSM running when it began (none: the mod's own blow): what
        /// the game's code makes for them (a corpse, geo, the hit's blood) is each game's own (Enemies makes it there); what
        /// another FSM makes meanwhile (the boss's own FSM answering ZERO HP or HIT with its death's or its anger's effects) is sent.
        /// </summary>
        static readonly Stack<HutongGames.PlayMaker.Fsm> dying = new Stack<HutongGames.PlayMaker.Fsm>(), hurting = new Stack<HutongGames.PlayMaker.Fsm>();
        /// <summary>A copy being made here: its own spawns are nobody's.</summary>
        static bool copying;

        [HarmonyPrefix, HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void DieIn() => dying.Push(HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm);

        [HarmonyFinalizer, HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void DieOut() { if (dying.Count > 0) dying.Pop(); }

        [HarmonyPrefix, HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void HurtIn() => hurting.Push(HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm);

        [HarmonyFinalizer, HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void HurtOut() { if (hurting.Count > 0) hurting.Pop(); }

        /// <summary>Made by the game's own code for a death or a hit in progress (not by another FSM answering it).</summary>
        static bool OwnEffect(HutongGames.PlayMaker.Fsm now) =>
            dying.Count > 0 && dying.Peek() == now || hurting.Count > 0 && hurting.Peek() == now;

        [HarmonyPostfix, HarmonyPatch(typeof(ObjectPool), nameof(ObjectPool.Spawn), new[] { typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion) })]
        static void Pooled(GameObject prefab, GameObject __result) => Noted(prefab, __result);

        [HarmonyPostfix, HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.CreateObject), nameof(HutongGames.PlayMaker.Actions.CreateObject.OnEnter))]
        static void Created(HutongGames.PlayMaker.Actions.CreateObject __instance)
        {
            if (__instance.storeObject != null && __instance.gameObject != null) Noted(__instance.gameObject.Value, __instance.storeObject.Value);
        }

        /// <summary>An object just spawned: the owner's fight's (an enemy's or its room's FSM made it in a scene this game runs) is sent on.</summary>
        static void Noted(GameObject prefab, GameObject go)
        {
            if (go == null || prefab == null || copying || !NetSession.Active || !Enemies.Enabled) return;
            var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
            if (OwnEffect(fsm)) return;
            var src = fsm?.GameObject;
            if (src == null) return;
            var hero = HeroController.SilentInstance;
            if (hero != null && (src == hero.gameObject || src.transform.IsChildOf(hero.transform)))
            {
                // The Knight's own (a spell, a nail art's flash): to the others with this scene, drawn where this player is.
                if (go.GetComponent<HealthManager>() == null && go.GetComponentInChildren<Renderer>(true) != null && !prefab.name.StartsWith("Geo ")
                    && Enemies.Watched(NetScenes.Current))
                    mine[go.GetInstanceID()] = new Spawn { Go = go, Id = nextId++, Scene = NetScenes.Current, Prefab = prefab.name };
                return;
            }
            string scene = src.scene.name;
            // A spawn's own spawn (a shot bursting into sparks): the scene the first was made for.
            for (var t = src.transform; t != null; t = t.parent)
                if (spawns.TryGetValue(t.gameObject.GetInstanceID(), out var first)) { scene = first.Scene; break; }
            if (!Enemies.Owns(scene) || !Enemies.Watched(scene) || prefab.name.StartsWith("Geo ")) return;
            var hm = go.GetComponent<HealthManager>();
            if (hm != null)
            {
                // An enemy: a puppet in the others' games (one made from a pool lives outside every scene).
                if (go.scene.name == "DontDestroyOnLoad") Enemies.Adopt(hm, scene);
                return;
            }
            if (go.GetComponentInChildren<Renderer>(true) == null) return;
            spawns[go.GetInstanceID()] = new Spawn { Go = go, Id = nextId++, Scene = scene, Prefab = prefab.name };
            DevNoted++;
        }

        /// <summary>Whether this object is, or is under, one of the fight's spawns this game sends (not an enemy's own part then).</summary>
        public static bool IsSpawned(Transform t)
        {
            if (spawns.Count == 0) return false;
            for (; t != null; t = t.parent) if (spawns.ContainsKey(t.gameObject.GetInstanceID())) return true;
            return false;
        }

        static readonly List<int> gone = new List<int>();

        /// <summary>Each snapshot round: the spawns put away or destroyed leave the table (whatever scene they were for).</summary>
        public static void Prune()
        {
            if (spawns.Count == 0) return;
            gone.Clear();
            foreach (var kv in spawns) if (kv.Value.Go == null || !kv.Value.Go.activeInHierarchy || !Enemies.Owns(kv.Value.Scene)) gone.Add(kv.Key);
            foreach (int k in gone) spawns.Remove(k);
        }

        /// <summary>Each snapshot of an owned scene others have: its spawns' looks, then its room's own objects.</summary>
        public static void Send(string scene, Vector2 shift)
        {
            var p = NetSession.Mine(Msg.SceneMirror);
            p.W.Write(scene);
            p.W.Write(KSpawns);
            long countAt = p.Length;
            p.W.Write((ushort)0);
            int n = 0;
            gone.Clear();
            foreach (var kv in spawns)
            {
                var s = kv.Value;
                if (s.Go == null || !s.Go.activeInHierarchy) { gone.Add(kv.Key); continue; }
                if (s.Scene != scene || p.Length > 56000) continue;
                WriteSpawn(p.W, s, shift);
                n++;
            }
            foreach (int k in gone) spawns.Remove(k);
            var data = p.Done();
            data[countAt] = (byte)n;
            data[countAt + 1] = (byte)(n >> 8);
            NetSession.Send(data);
            DevSpawnsSent += n;
            SendStatic(scene, shift);
        }

        static void WriteSpawn(BinaryWriter w, Spawn s, Vector2 shift)
        {
            w.Write(s.Id);
            bool named = s.Sends++ % 8 == 0;
            w.Write(named);
            if (named) w.Write(s.Prefab);
            var tr = s.Go.transform;
            var look = Looks.Of(tr, tr.position - (Vector3)shift, tr.eulerAngles.z);
            look.Scale = tr.lossyScale;
            Looks.Write(w, look);
            if (s.Parts == null)
            {
                s.Parts = new List<(Transform, string)>();
                foreach (var c in tr.GetComponentsInChildren<Transform>(true))
                {
                    if (c == tr || c.GetComponent<Renderer>() == null || s.Parts.Count >= 24) continue;
                    var path = new List<string>();
                    for (var q = c; q != tr; q = q.parent) path.Add(q.GetSiblingIndex().ToString());
                    path.Reverse();
                    s.Parts.Add((c, string.Join("/", path)));
                }
            }
            int k = 0;
            foreach (var (c, _) in s.Parts) if (c != null) k++;
            w.Write((byte)k);
            foreach (var (c, path) in s.Parts)
            {
                if (c == null) continue;
                w.Write(path);
                w.Write(Looks.Hash(c.name));
                Looks.Write(w, Looks.Of(c, c.localPosition, c.localEulerAngles.z));
            }
        }

        /// <summary>Twenty times a second: this player's Knight's spawns, each in its scene's own frame (Net.NetFx carries them, to everyone).</summary>
        public static void SendMine()
        {
            if (mine.Count == 0) return;
            gone.Clear();
            var p = NetSession.Mine(Msg.NetFx);
            p.W.Write(NetFx.KSpawns);
            long countAt = p.Length;
            p.W.Write((ushort)0);
            int n = 0;
            foreach (var kv in mine)
            {
                var s = kv.Value;
                if (s.Go == null || !s.Go.activeInHierarchy) { gone.Add(kv.Key); continue; }
                if (p.Length > 56000 || !NetScenes.Shift(s.Scene, out var shift)) continue;
                p.W.Write(s.Scene);
                WriteSpawn(p.W, s, shift);
                n++;
            }
            foreach (int k in gone) mine.Remove(k);
            var data = p.Done();
            data[countAt] = (byte)n;
            data[countAt + 1] = (byte)(n >> 8);
            NetSession.Send(data);
            DevMineSent += n;
        }

        /// <summary>Another player's Knight's spawns: copies drawn here where they are, gone with theirs.</summary>
        public static void ReceiveMine(int from, BinaryReader r)
        {
            string stream = "p" + from;
            int n = r.ReadUInt16();
            seen.Clear();
            for (int i = 0; i < n; i++)
            {
                string scene = r.ReadString();
                bool here = NetScenes.Shift(scene, out var shift);
                ReadEntry(r, stream, scene, shift, here);
            }
            DropUnseen(stream);
        }

        // ================================================================== the owner's side: a boss room's own objects

        sealed class Room
        {
            public float At;
            public readonly List<(Transform Tr, string Path, ushort Name)> List = new List<(Transform, string, ushort)>();
            public readonly Dictionary<Transform, Looks.State> Sent = new Dictionary<Transform, Looks.State>();
            public readonly HashSet<Transform> Set = new HashSet<Transform>();
            public int Cursor;
        }

        /// <summary>Whether this object's look goes to the others already (a spawn's, or a boss room's own object): Net.EnemyAttacks draws no picture of its own for it.</summary>
        public static bool Covered(Transform t)
        {
            if (IsSpawned(t)) return true;
            return t != null && rooms.TryGetValue(t.gameObject.scene.name, out var room) && room.Set.Contains(t);
        }

        static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();

        /// <summary>
        /// The objects of a boss room kept the same: all of its boss scene ("_boss", laid over the room), and in the room
        /// itself what is under a root holding a battle gate; drawn ones or ones with a collider, not an enemy's (its puppet
        /// has them) nor the mod's own. Listed again every three seconds.
        /// </summary>
        static Room RoomOf(string scene)
        {
            if (!rooms.TryGetValue(scene, out var room)) rooms[scene] = room = new Room { At = -10f };
            if (Time.unscaledTime - room.At < 3f) return room;
            room.At = Time.unscaledTime;
            room.List.Clear();
            room.Set.Clear();
            if (!World.BossRooms.IsRoom(scene) || scene.StartsWith("GG_") || scene.StartsWith("Dream_")) return room;
            var s = NetScenes.FindScene(scene);
            if (!s.IsValid()) return room;
            bool bossScene = scene.EndsWith("_boss");
            var roots = s.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (Ours(root)) continue;
                if (!bossScene && !HasGate(root)) continue;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.GetComponentInParent<HealthManager>() != null || IsSpawned(tr) || tr.name.StartsWith("TK ")) continue;
                    if (tr.GetComponent<Renderer>() == null && tr.GetComponent<Collider2D>() == null) continue;
                    room.List.Add((tr, PathIn(tr, i), Looks.Hash(tr.name)));
                    room.Set.Add(tr);
                }
            }
            var stale = new List<Transform>();
            foreach (var t in room.Sent.Keys) if (t == null || !room.Set.Contains(t)) stale.Add(t);
            foreach (var t in stale) room.Sent.Remove(t);
            return room;
        }

        static bool Ours(GameObject root) => root.name.StartsWith("TK ") || root.name.StartsWith("TerraKnight") || root.name.StartsWith("Terra ")
            || root.GetComponent<World.TerraOwned>() != null;

        static bool HasGate(GameObject root)
        {
            foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "BG Control") return true;
            return false;
        }

        /// <summary>Its root's place among the scene's roots, then its places among its siblings.</summary>
        static string PathIn(Transform tr, int rootIndex)
        {
            var path = new List<string>();
            for (var p = tr; p.parent != null; p = p.parent) path.Add(p.GetSiblingIndex().ToString());
            path.Add(rootIndex.ToString());
            path.Reverse();
            return string.Join("/", path);
        }

        static void SendStatic(string scene, Vector2 shift)
        {
            var room = RoomOf(scene);
            if (room.List.Count == 0) return;
            var p = NetSession.Mine(Msg.SceneMirror);
            p.W.Write(scene);
            p.W.Write(KStatic);
            long countAt = p.Length;
            p.W.Write((ushort)0);
            int n = 0, again = 0;
            // What changed, then a share of the rest (a copy that went its own way comes back within a few seconds).
            for (int k = 0; k < room.List.Count && p.Length < 56000; k++)
            {
                int i = (room.Cursor + k) % room.List.Count;
                var (tr, path, name) = room.List[i];
                if (tr == null) continue;
                var look = Looks.Of(tr, tr.position - (Vector3)shift, tr.eulerAngles.z);
                bool changed = !room.Sent.TryGetValue(tr, out var was) || Looks.Differs(look, was);
                if (!changed && again >= 24) continue;
                if (!changed) again++;
                room.Sent[tr] = look;
                p.W.Write(path);
                p.W.Write(name);
                Looks.Write(p.W, look);
                n++;
            }
            room.Cursor = (room.Cursor + 24) % Mathf.Max(1, room.List.Count);
            if (n == 0) return;
            var data = p.Done();
            data[countAt] = (byte)n;
            data[countAt + 1] = (byte)(n >> 8);
            NetSession.Send(data);
            DevStaticSent += n;
        }

        // ================================================================== the others' side

        sealed class Copy
        {
            public GameObject Go;
            /// <summary>Whose: a scene (its owner's fight) or "p" and a player's slot (what that player's Knight made).</summary>
            public string Stream;
            public string Scene, Prefab;
            public int Id;
            public float Seen;
            public readonly List<(float T, Vector3 P)> Samples = new List<(float, Vector3)>();
        }

        static readonly Dictionary<(string, int), Copy> copies = new Dictionary<(string, int), Copy>();
        static readonly Dictionary<(string, int), string> names = new Dictionary<(string, int), string>();
        static readonly HashSet<(string, int)> seen = new HashSet<(string, int)>();
        static readonly Dictionary<string, Transform> statics = new Dictionary<string, Transform>();

        public static void Receive(int owner, BinaryReader r)
        {
            string scene = r.ReadString();
            byte kind = r.ReadByte();
            int n = r.ReadUInt16();
            bool ours = Enemies.RemoteOwned(scene) && NetSession.Owners.TryGetValue(scene, out int o) && o == owner && NetScenes.Shift(scene, out _);
            NetScenes.Shift(scene, out var shift);
            if (kind == KSpawns)
            {
                seen.Clear();
                for (int i = 0; i < n; i++) ReadEntry(r, scene, scene, shift, ours);
                if (!ours) return;
                // The owner's gone: so is the copy.
                DropUnseen(scene);
            }
            else if (kind == KStatic)
            {
                var s = ours ? NetScenes.FindScene(scene) : default;
                for (int i = 0; i < n; i++)
                {
                    string path = r.ReadString();
                    ushort name = r.ReadUInt16();
                    var look = Looks.Read(r);
                    if (!ours) continue;
                    var tr = Static(s, scene, path);
                    if (tr == null || Looks.Hash(tr.name) != name) continue;
                    look.Pos += (Vector3)shift;
                    Looks.Apply(tr, look, true, false, true);
                    DevStaticApplied++;
                }
            }
        }

        /// <summary>One spawn's look (its copy made when there is none yet, from its prefab's name), into the stream's copies.</summary>
        static void ReadEntry(BinaryReader r, string stream, string scene, Vector2 shift, bool apply)
        {
            int id = r.ReadInt32();
            string prefab = r.ReadBoolean() ? r.ReadString() : null;
            var look = Looks.Read(r);
            int parts = r.ReadByte();
            var key = (stream, id);
            if (prefab != null) names[key] = prefab;
            Copy c = null;
            if (apply)
            {
                seen.Add(key);
                if (!copies.TryGetValue(key, out c) || c.Go == null)
                {
                    c = names.TryGetValue(key, out var pn) ? Make(scene, id, pn) : null;
                    if (c != null) { c.Stream = stream; copies[key] = c; }
                }
                if (c != null)
                {
                    c.Seen = Time.unscaledTime;
                    var world = look.Pos + (Vector3)shift;
                    if (c.Samples.Count > 0 && (c.Samples[c.Samples.Count - 1].P - world).sqrMagnitude > 64f) c.Samples.Clear();
                    c.Samples.Add((Time.unscaledTime, world));
                    while (c.Samples.Count > 8) c.Samples.RemoveAt(0);
                    look.Pos = world;
                    Looks.Apply(c.Go.transform, look, c.Samples.Count == 1, false, false);
                    if (c.Samples.Count > 1)
                    {
                        var e = c.Go.transform.eulerAngles;
                        c.Go.transform.eulerAngles = new Vector3(e.x, e.y, look.Rot);
                    }
                }
            }
            for (int k = 0; k < parts; k++)
            {
                string path = r.ReadString();
                ushort name = r.ReadUInt16();
                var pl = Looks.Read(r);
                if (c == null || c.Go == null) continue;
                var t = Find(c.Go.transform, path);
                if (t != null && Looks.Hash(t.name) == name) Looks.Apply(t, pl, true, true, false);
            }
        }

        static void DropUnseen(string stream)
        {
            var drop = new List<(string, int)>();
            foreach (var kv in copies) if (kv.Key.Item1 == stream && !seen.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var key in drop) { if (copies[key].Go != null) Object.Destroy(copies[key].Go); copies.Remove(key); names.Remove(key); }
            // Names of spawns never copied here (no prefab of that name): gone with the owner's too.
            drop.Clear();
            foreach (var key in names.Keys) if (key.Item1 == stream && !seen.Contains(key)) drop.Add(key);
            foreach (var key in drop) names.Remove(key);
        }

        static Transform Find(Transform root, string path)
        {
            var t = root;
            foreach (var seg in path.Split('/'))
            {
                if (!int.TryParse(seg, out int i) || i < 0 || i >= t.childCount) return null;
                t = t.GetChild(i);
            }
            return t;
        }

        /// <summary>A boss room's own object here by its path (cached; the scene's roots by their order, then the children's).</summary>
        static Transform Static(UnityEngine.SceneManagement.Scene s, string scene, string path)
        {
            string key = scene + "|" + path;
            if (statics.TryGetValue(key, out var t) && t != null) return t;
            if (!s.IsValid()) return null;
            int cut = path.IndexOf('/');
            if (!int.TryParse(cut < 0 ? path : path.Substring(0, cut), out int ri)) return null;
            var roots = s.GetRootGameObjects();
            if (ri < 0 || ri >= roots.Length) return null;
            t = cut < 0 ? roots[ri].transform : Find(roots[ri].transform, path.Substring(cut + 1));
            if (t != null) statics[key] = t;
            return t;
        }

        // ------------------------------------------------------------------ copies

        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, GameObject> lying = new Dictionary<string, GameObject>();
        static readonly HashSet<string> unknown = new HashSet<string>();
        static float prefabsAt = -10f, unknownAt;

        /// <summary>The prefab of that name loaded in this game (else an object of that name lying in a scene or a pool, to copy).</summary>
        static GameObject Prefab(string name)
        {
            if (prefabs.TryGetValue(name, out var p) && p != null) return p;
            if (lying.TryGetValue(name, out p) && p != null) return p;
            if (Time.unscaledTime - unknownAt > 30f) { unknown.Clear(); unknownAt = Time.unscaledTime; }
            if (unknown.Contains(name) || Time.unscaledTime - prefabsAt < 2f) return null;
            prefabsAt = Time.unscaledTime;
            prefabs.Clear();
            lying.Clear();
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (!go.scene.IsValid())
                {
                    if (go.transform.parent == null && !prefabs.ContainsKey(go.name)) prefabs[go.name] = go;
                    continue;
                }
                string bare = go.name.EndsWith("(Clone)") ? go.name.Substring(0, go.name.Length - 7) : go.name;
                if (!lying.TryGetValue(bare, out var had) || (had.activeInHierarchy && !go.activeInHierarchy)) lying[bare] = go;
            }
            if (prefabs.TryGetValue(name, out p) || lying.TryGetValue(name, out p)) return p;
            unknown.Add(name);
            DevMissing = name;
            return null;
        }

        /// <summary>This game's copy of one of the owner's spawns: the same prefab, its scripts, colliders and bodies gone, in the same scene.</summary>
        static Copy Make(string scene, int id, string prefabName)
        {
            var prefab = Prefab(prefabName);
            if (prefab == null) { DevCopiesMissing++; return null; }
            var s = NetScenes.FindScene(scene);
            if (!s.IsValid()) return null;
            bool asset = !prefab.scene.IsValid(), was = prefab.activeSelf;
            GameObject go;
            copying = true;
            try
            {
                if (asset && was) prefab.SetActive(false);
                try { go = Object.Instantiate(prefab); }
                finally { if (asset && was) prefab.SetActive(true); }
                go.SetActive(false);
                go.name = prefabName + " (TK copy)";
                Strip(go);
                if (go.transform.parent != null) go.transform.SetParent(null, true);
                USM.MoveGameObjectToScene(go, s);
                go.AddComponent<World.TerraOwned>();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[联机] 复制 {prefabName} 出错：{e.Message}");
                return null;
            }
            finally { copying = false; }
            DevCopies++;
            return new Copy { Go = go, Scene = scene, Id = id, Prefab = prefabName };
        }

        /// <summary>Only its pictures left: every script but tk2d's sprites and animators, its colliders, joints and bodies taken off.</summary>
        static void Strip(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name.StartsWith("tk2d")) continue;
                try { Object.DestroyImmediate(mb); }
                catch { mb.enabled = false; }
            }
            foreach (var j in go.GetComponentsInChildren<Joint2D>(true)) { try { Object.DestroyImmediate(j); } catch { j.enabled = false; } }
            foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) { try { Object.DestroyImmediate(c); } catch { c.enabled = false; } }
            foreach (var b in go.GetComponentsInChildren<Rigidbody2D>(true)) { try { Object.DestroyImmediate(b); } catch { b.simulated = false; } }
        }

        /// <summary>Each frame: the copies eased along the owner's track a tenth of a second behind; one not heard of for a second goes.</summary>
        public static void Tick()
        {
            if (copies.Count == 0) return;
            float t = Time.unscaledTime - 0.1f;
            List<(string, int)> stale = null;
            foreach (var kv in copies)
            {
                var c = kv.Value;
                if (c.Go == null || Time.unscaledTime - c.Seen > 1f || (c.Stream == c.Scene && !Enemies.RemoteOwned(c.Scene)) || !NetScenes.IsLoaded(c.Scene)) { (stale ?? (stale = new List<(string, int)>())).Add(kv.Key); continue; }
                if (c.Samples.Count < 2) continue;
                Vector3 at = c.Samples[c.Samples.Count - 1].P;
                for (int i = c.Samples.Count - 1; i > 0; i--)
                {
                    var a = c.Samples[i - 1];
                    var b = c.Samples[i];
                    if (t < a.T) continue;
                    if (t > b.T) break;
                    at = Vector3.Lerp(a.P, b.P, b.T - a.T > 1e-4f ? (t - a.T) / (b.T - a.T) : 1f);
                    break;
                }
                if (t < c.Samples[0].T) at = c.Samples[0].P;
                c.Go.transform.position = at;
            }
            if (stale == null) return;
            foreach (var key in stale)
            {
                if (copies[key].Go != null) Object.Destroy(copies[key].Go);
                copies.Remove(key);
                names.Remove(key);
            }
        }

        /// <summary>A scene's enemies changed hands: its copies go (the new owner's spawns come with its snapshots), its sent looks are forgotten.</summary>
        public static void OwnerChanged(string scene)
        {
            var drop = new List<(string, int)>();
            foreach (var kv in copies) if (kv.Key.Item1 == scene) drop.Add(kv.Key);
            foreach (var key in drop) { if (copies[key].Go != null) Object.Destroy(copies[key].Go); copies.Remove(key); names.Remove(key); }
            rooms.Remove(scene);
            var forget = new List<int>();
            foreach (var kv in spawns) if (kv.Value.Scene == scene) forget.Add(kv.Key);
            foreach (int k in forget) spawns.Remove(k);
        }

        public static void Reset()
        {
            foreach (var c in copies.Values) if (c.Go != null) Object.Destroy(c.Go);
            copies.Clear();
            names.Clear();
            spawns.Clear();
            mine.Clear();
            rooms.Clear();
            statics.Clear();
            prefabs.Clear();
            lying.Clear();
            unknown.Clear();
            dying.Clear();
            hurting.Clear();
        }

        public static string DevReport() =>
            $"记下生成 {DevNoted}，发出生成 {DevSpawnsSent}，副本 {copies.Count}（做过 {DevCopies}，找不到预制体 {DevCopiesMissing}{(DevMissing.Length > 0 ? " " + DevMissing : "")}），房间物体 发出 {DevStaticSent} 套上 {DevStaticApplied}";
    }
}
