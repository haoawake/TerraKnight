using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// One set of enemies per scene for everyone, as Terraria's server runs one set of NPCs
    /// (SyncNPC 23, DamageNPC 28). Hallownest's enemies are the game's own, run by their scripts
    /// in each game, so one player's game runs a scene's enemies: its owner, the first player in
    /// it (<see cref="Server"/> decides). The owner sends where each of them is, how it is turned,
    /// what it is playing and its health fifteen times a second; in everyone else's game the same
    /// enemies become puppets: their scripts and AI switched off, their bodies kinematic, moved and
    /// animated as the owner's are. They still touch: a puppet's body hurts the player it touches.
    /// A hit on a puppet goes to the owner, whose game deals it (knockback and all); a death there
    /// kills the puppets too, so everyone gets the drops (instanced loot, as Terraria's boss bags).
    /// When the owner leaves, the next player in the scene takes over from where things are.
    /// What doesn't carry over: the shots and spawned attacks of an enemy's scripts (only the
    /// owner's game has them), and enemies go after their owner, who is the only one they see.
    /// </summary>
    [HarmonyPatch]
    internal static class Enemies
    {
        public static bool Enabled => NetSettings.SyncEnemies == null || NetSettings.SyncEnemies.Value;

        /// <summary>Whose game runs this scene's enemies: true when another player's does.</summary>
        public static bool RemoteOwned(string scene) =>
            NetSession.Active && Enabled && scene != null && NetSession.Owners.TryGetValue(scene, out int o) && o != NetSession.MySlot;

        static bool Owned(string scene) => NetSession.Active && Enabled && scene != null && NetSession.Owners.TryGetValue(scene, out int o) && o == NetSession.MySlot;

        public static bool IsPuppet(HealthManager hm) => hm != null && puppetOf.ContainsKey(hm);

        /// <summary>The owner's side of one enemy: its number and how it is found in the others' games.</summary>
        sealed class Tracked
        {
            public HealthManager Hm;
            public int Id;
            public string Scene, Path;
            public int Sends;
            public tk2dSpriteAnimator Anim;
            public Renderer Rend;
            public bool DeathSent;
        }

        /// <summary>The others' side: a local enemy moved and animated as the owner's.</summary>
        sealed class Puppet
        {
            public HealthManager Hm;
            public string Scene;
            public int Id, Owner;
            public PlayMakerFSM[] Fsms;
            public bool[] FsmOn;
            public Behaviour[] Parts;
            public bool[] PartOn;
            public Rigidbody2D Body;
            public RigidbodyType2D BodyType;
            public tk2dSpriteAnimator Anim;
            public Renderer Rend;
            public float Seen;
            public readonly List<(float T, Vector2 P)> Samples = new List<(float, Vector2)>();
            public int Clip = -1, Frame;
            public bool Active, Visible;
        }

        static readonly Dictionary<HealthManager, Tracked> tracked = new Dictionary<HealthManager, Tracked>();
        static readonly Dictionary<(string, int), Tracked> trackedById = new Dictionary<(string, int), Tracked>();
        static readonly Dictionary<(string, int), Puppet> puppets = new Dictionary<(string, int), Puppet>();
        static readonly Dictionary<HealthManager, Puppet> puppetOf = new Dictionary<HealthManager, Puppet>();
        /// <summary>Local enemies of a scene another game runs that its owner doesn't have: hidden while it does.</summary>
        static readonly Dictionary<HealthManager, string> hidden = new Dictionary<HealthManager, string>();
        static readonly Dictionary<string, List<HealthManager>> sceneEnemies = new Dictionary<string, List<HealthManager>>();
        static readonly Dictionary<string, float> sceneScanned = new Dictionary<string, float>();
        static int nextId = 1;

        public static int DevPuppets => puppets.Count;
        public static int DevTracked => tracked.Count;
        public static int DevForwarded, DevApplied, DevDeathsSent, DevDeathsReceived, DevHidden;

        public static void Reset()
        {
            foreach (var p in new List<Puppet>(puppets.Values)) Release(p);
            puppets.Clear();
            puppetOf.Clear();
            Unhide(null);
            tracked.Clear();
            trackedById.Clear();
            sceneEnemies.Clear();
            sceneScanned.Clear();
        }

        // ------------------------------------------------------------------ the scenes' enemies

        const string TemplateHolder = "TK Enemy Templates";

        /// <summary>The enemies of a scene (inactive ones too; not the respawn templates), looked up again twice a second.</summary>
        static List<HealthManager> Of(string scene)
        {
            if (sceneEnemies.TryGetValue(scene, out var list) && sceneScanned.TryGetValue(scene, out float at) && Time.unscaledTime - at < 0.5f)
            {
                list.RemoveAll(h => h == null);
                return list;
            }
            if (list == null) sceneEnemies[scene] = list = new List<HealthManager>();
            list.Clear();
            sceneScanned[scene] = Time.unscaledTime;
            var s = NetScenes.FindScene(scene);
            if (!s.IsValid()) return list;
            foreach (var root in s.GetRootGameObjects())
            {
                if (root.name == TemplateHolder) continue;
                foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                    if (!UnderTemplates(hm.transform)) list.Add(hm);
            }
            return list;
        }

        static bool UnderTemplates(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == TemplateHolder) return true;
            return false;
        }

        /// <summary>
        /// How an enemy is found in another game's copy of its scene: the names down from its root,
        /// each with its place among its siblings. A copy the mod made (name ending "(TK)") is
        /// "name@*" under its parent: the copies differ from game to game, the nearest one is taken.
        /// </summary>
        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            bool copy = t.name.EndsWith("(TK)");
            parts.Add(t.name + "@" + (copy ? "*" : t.GetSiblingIndex().ToString()));
            for (var p = t.parent; p != null; p = p.parent) parts.Add(p.name + "@" + p.GetSiblingIndex());
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Transform FindPath(string scene, string path, Vector2 near)
        {
            var s = NetScenes.FindScene(scene);
            if (!s.IsValid() || string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('/');
            Transform at = null;
            var roots = s.GetRootGameObjects();
            for (int i = 0; i < parts.Length; i++)
            {
                string seg = parts[i];
                int cut = seg.LastIndexOf('@');
                string name = cut >= 0 ? seg.Substring(0, cut) : seg;
                string idx = cut >= 0 ? seg.Substring(cut + 1) : "";
                bool last = i == parts.Length - 1;
                int count = at == null ? roots.Length : at.childCount;
                Transform Child(int k) => at == null ? roots[k].transform : at.GetChild(k);
                Transform found = null;
                if (idx == "*")
                {
                    // A copy: the nearest of that name not already following another of the owner's.
                    float best = float.MaxValue;
                    for (int k = 0; k < count; k++)
                    {
                        var c = Child(k);
                        if (c.name != name) continue;
                        var hm = c.GetComponent<HealthManager>();
                        if (hm != null && (puppetOf.ContainsKey(hm) || hm.isDead)) continue;
                        float d = ((Vector2)c.position - near).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            found = c;
                        }
                    }
                }
                else
                {
                    if (int.TryParse(idx, out int k) && k >= 0 && k < count && Child(k).name == name) found = Child(k);
                    // Not at its place (something was added before it): the first of that name.
                    for (int j = 0; found == null && j < count; j++)
                        if (Child(j).name == name) found = Child(j);
                }
                if (found == null)
                {
                    if (last && idx == "*") return SpawnCopy(scene, at, name, near);
                    return null;
                }
                at = found;
            }
            return at;
        }

        /// <summary>A copy the owner has and this game hasn't made yet: made from this game's template of it.</summary>
        static Transform SpawnCopy(string scene, Transform parent, string name, Vector2 at)
        {
            var go = World.EnemyRespawn.Instance != null ? World.EnemyRespawn.Instance.NetSpawn(scene, name, parent, at) : null;
            return go != null ? go.transform : null;
        }

        // ------------------------------------------------------------------ the owner

        static void Prune()
        {
            var deadKeys = new List<HealthManager>();
            foreach (var kv in tracked) if (kv.Key == null) deadKeys.Add(kv.Key);
            foreach (var k in deadKeys)
            {
                trackedById.Remove((tracked[k].Scene, tracked[k].Id));
                tracked.Remove(k);
            }
            deadKeys.Clear();
            foreach (var kv in hidden) if (kv.Key == null) deadKeys.Add(kv.Key);
            foreach (var k in deadKeys) hidden.Remove(k);
        }

        static Tracked Track(HealthManager hm, string scene)
        {
            if (tracked.TryGetValue(hm, out var t) && t.Scene == scene) return t;
            t = new Tracked { Hm = hm, Id = nextId++, Scene = scene, Path = PathOf(hm.transform), Anim = hm.GetComponent<tk2dSpriteAnimator>(), Rend = hm.GetComponent<Renderer>() };
            tracked[hm] = t;
            trackedById[(scene, t.Id)] = t;
            return t;
        }

        static bool Watched(string scene)
        {
            foreach (var p in NetSession.Players.Values)
                if (p.In.Contains(scene) || p.Loaded.Contains(scene)) return true;
            return false;
        }

        static readonly List<string> ownedScenes = new List<string>();

        /// <summary>Fifteen times a second (every third state tick), the owned scenes others have loaded.</summary>
        static int snapshotTick;

        public static void SendSnapshots()
        {
            if (!NetSession.Active || !Enabled) return;
            if (++snapshotTick % 4 == 3) return;
            // Every ten seconds: enemies destroyed with their scenes leave the tables.
            if (snapshotTick % 200 == 0) Prune();
            ownedScenes.Clear();
            foreach (var kv in NetSession.Owners)
                if (kv.Value == NetSession.MySlot && NetSession.HaveScene(kv.Key) && Watched(kv.Key)) ownedScenes.Add(kv.Key);
            foreach (var scene in ownedScenes)
            {
                if (!NetScenes.Shift(scene, out var shift)) continue;
                var list = Of(scene);
                var p = NetSession.Mine(Msg.EnemySnapshot);
                p.W.Write(scene);
                long countAt = p.Length;
                p.W.Write((ushort)0);
                int n = 0;
                foreach (var hm in list)
                {
                    if (hm == null || p.Length > 60000) continue;
                    var t = Track(hm, scene);
                    WriteEnemy(p.W, t, shift);
                    n++;
                }
                var data = p.Done();
                data[countAt + 0] = (byte)n;
                data[countAt + 1] = (byte)(n >> 8);
                NetSession.Send(data);
            }
        }

        const byte FActive = 1, FRend = 2, FDead = 4, FPath = 8;

        static void WriteEnemy(BinaryWriter w, Tracked t, Vector2 shift)
        {
            var hm = t.Hm;
            var tr = hm.transform;
            bool path = t.Sends++ % 15 == 0;
            byte f = (byte)((hm.gameObject.activeInHierarchy ? FActive : 0) | (t.Rend != null && t.Rend.enabled ? FRend : 0) | (hm.isDead ? FDead : 0) | (path ? FPath : 0));
            w.Write(t.Id);
            w.Write(f);
            if (path) w.Write(t.Path);
            Vector2 local = (Vector2)tr.position - shift;
            w.Write(local.x);
            w.Write(local.y);
            w.Write(tr.localScale.x);
            w.Write(tr.localScale.y);
            w.Write(tr.eulerAngles.z);
            int clip = -1, frame = 0;
            if (t.Anim != null && t.Anim.CurrentClip != null)
            {
                clip = t.Anim.GetClipIdByName(t.Anim.CurrentClip.name);
                frame = t.Anim.CurrentFrame;
            }
            w.Write((short)clip);
            w.Write((byte)Mathf.Clamp(frame, 0, 255));
            w.Write(hm.hp);
        }

        /// <summary>HealthManager.Die in the owner's game: everyone's copy dies with it.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void AfterDie(HealthManager __instance, float? attackDirection, AttackTypes attackType)
        {
            if (__instance == null || !__instance.isDead || !NetSession.Active || !Enabled) return;
            string scene = __instance.gameObject.scene.name;
            if (!Owned(scene) || puppetOf.ContainsKey(__instance)) return;
            var t = Track(__instance, scene);
            if (t.DeathSent) return;
            t.DeathSent = true;
            DevDeathsSent++;
            var p = NetSession.Mine(Msg.EnemyDeath);
            p.W.Write(scene);
            p.W.Write(t.Id);
            p.W.Write(attackDirection ?? 0f);
            p.W.Write((byte)attackType);
            NetSession.Send(p.Done());
        }

        /// <summary>DamageNPC from another player: their hit dealt here, by the game that runs the enemy.</summary>
        public static void ReceiveDamage(int from, BinaryReader r)
        {
            string scene = r.ReadString();
            int id = r.ReadInt32();
            int hk = r.ReadInt32();
            float dir = r.ReadSingle();
            var type = (AttackTypes)r.ReadByte();
            float push = r.ReadSingle();
            int terra = r.ReadInt32();
            byte flags = r.ReadByte();
            var fromLocal = new Vector2(r.ReadSingle(), r.ReadSingle());
            if (!Owned(scene) || !trackedById.TryGetValue((scene, id), out var t) || t.Hm == null || t.Hm.isDead) return;
            NetScenes.ToWorld(scene, fromLocal, out var fromWorld);
            DevApplied++;
            MeleeHits.ApplyNetHit(t.Hm, hk, dir, type, push, terra, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, fromWorld);
        }

        // ------------------------------------------------------------------ the others

        /// <summary>What a hit on a puppet carries to the owner beyond the game's hit (MeleeHits.Deal sets it just before).</summary>
        public struct HitContext
        {
            public float Push;
            public int TerraDamage;
            public bool Crit, BigHit, Set;
        }

        public static HitContext Context;

        /// <summary>A hit on a puppet: not dealt here (its owner's game does it), sent there.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        static bool BeforeHit(HealthManager __instance, HitInstance hitInstance)
        {
            if (!puppetOf.TryGetValue(__instance, out var pup)) return true;
            var ctx = Context;
            Context = default;
            if (__instance.isDead || hitInstance.DamageDealt <= 0) return false;
            int hk = Mathf.RoundToInt(hitInstance.DamageDealt * hitInstance.Multiplier);
            Vector2 from = hitInstance.Source != null ? (Vector2)hitInstance.Source.transform.position : (Vector2)__instance.transform.position;
            Forward(pup, hk, hitInstance.Direction, hitInstance.AttackType, ctx.Set ? ctx.Push : 0f, ctx.Set ? ctx.TerraDamage : 0, ctx.Crit, ctx.BigHit, false, from);
            return false;
        }

        /// <summary>A debuff's damage on a puppet (EnemyDebuffs): to the owner, who takes it off.</summary>
        public static bool ForwardDot(HealthManager hm, int hk, int terra)
        {
            if (!puppetOf.TryGetValue(hm, out var pup)) return false;
            Forward(pup, hk, 0f, AttackTypes.Generic, 0f, terra, false, false, true, hm.transform.position);
            return true;
        }

        static void Forward(Puppet pup, int hk, float dir, AttackTypes type, float push, int terra, bool crit, bool big, bool dot, Vector2 from)
        {
            DevForwarded++;
            var p = NetSession.Mine(Msg.DamageNpc);
            p.W.Write(pup.Scene);
            p.W.Write(pup.Id);
            p.W.Write(hk);
            p.W.Write(dir);
            p.W.Write((byte)type);
            p.W.Write(push);
            p.W.Write(terra);
            p.W.Write((byte)((crit ? 1 : 0) | (big ? 2 : 0) | (dot ? 4 : 0)));
            var local = NetScenes.ToLocal(pup.Scene, from);
            p.W.Write(local.x);
            p.W.Write(local.y);
            NetSession.Send(p.Done());
        }

        static readonly HashSet<HealthManager> seenThisSnapshot = new HashSet<HealthManager>();

        /// <summary>SyncNPC from a scene's owner: its enemies' places and looks, the local ones made to follow.</summary>
        public static void ReceiveSnapshot(int owner, BinaryReader r)
        {
            string scene = r.ReadString();
            int n = r.ReadUInt16();
            bool ours = Enabled && NetSession.Owners.TryGetValue(scene, out int o) && o == owner && owner != NetSession.MySlot && NetScenes.Shift(scene, out _);
            NetScenes.Shift(scene, out var shift);
            seenThisSnapshot.Clear();
            bool complete = true;
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                byte f = r.ReadByte();
                string path = (f & FPath) != 0 ? r.ReadString() : null;
                var local = new Vector2(r.ReadSingle(), r.ReadSingle());
                var scale = new Vector2(r.ReadSingle(), r.ReadSingle());
                float rot = r.ReadSingle();
                int clip = r.ReadInt16();
                int frame = r.ReadByte();
                int hp = r.ReadInt32();
                if (!ours) continue;
                var world = local + shift;
                if (!puppets.TryGetValue((scene, id), out var pup) || pup.Hm == null)
                {
                    if (path == null || (f & FDead) != 0)
                    {
                        complete = false;
                        continue;
                    }
                    var t = FindPath(scene, path, world);
                    var hm = t != null ? t.GetComponent<HealthManager>() : null;
                    if (hm == null || puppetOf.ContainsKey(hm) || hm.isDead) continue;
                    pup = MakePuppet(hm, scene, id, owner);
                }
                seenThisSnapshot.Add(pup.Hm);
                Apply(pup, f, world, scale, rot, clip, frame, hp);
            }
            if (!ours) return;
            // Once every one of the owner's has been named (its path came), the local ones that aren't among them go.
            if (complete) HideStrays(scene);
        }

        static void HideStrays(string scene)
        {
            foreach (var hm in Of(scene))
            {
                if (hm == null || seenThisSnapshot.Contains(hm) || puppetOf.ContainsKey(hm) || hidden.ContainsKey(hm) || hm.isDead) continue;
                if (!hm.gameObject.activeSelf) continue;
                hidden[hm] = scene;
                hm.gameObject.SetActive(false);
                DevHidden++;
            }
        }

        static void Unhide(string scene)
        {
            var back = new List<HealthManager>();
            foreach (var kv in hidden)
                if (scene == null || kv.Value == scene) back.Add(kv.Key);
            foreach (var hm in back)
            {
                hidden.Remove(hm);
                if (hm != null && !hm.isDead) hm.gameObject.SetActive(true);
            }
        }

        /// <summary>Behaviours a puppet keeps: its life, its sprite and its animation, what it does to the hero, its death and hit effects.</summary>
        static bool Keep(Behaviour b)
        {
            string n = b.GetType().Name;
            return n == "HealthManager" || n.StartsWith("tk2d") || n == "SpriteFlash" || n == "DamageHero" || n.StartsWith("EnemyDeathEffects")
                   || n.StartsWith("EnemyHitEffects") || n == "InfectedEnemyEffects" || n == "ExtraDamageable" || n == "PersistentBoolItem"
                   || n.StartsWith("PlayMakerUnity2D") || n == "PlayMakerFixedUpdate";
        }

        static Puppet MakePuppet(HealthManager hm, string scene, int id, int owner)
        {
            var p = new Puppet { Hm = hm, Scene = scene, Id = id, Owner = owner, Anim = hm.GetComponent<tk2dSpriteAnimator>(), Rend = hm.GetComponent<Renderer>() };
            p.Fsms = hm.GetComponentsInChildren<PlayMakerFSM>(true);
            p.FsmOn = new bool[p.Fsms.Length];
            for (int i = 0; i < p.Fsms.Length; i++)
            {
                p.FsmOn[i] = p.Fsms[i].enabled;
                p.Fsms[i].enabled = false;
            }
            var parts = new List<Behaviour>();
            foreach (var b in hm.GetComponents<MonoBehaviour>())
                if (b != null && !(b is PlayMakerFSM) && !Keep(b)) parts.Add(b);
            p.Parts = parts.ToArray();
            p.PartOn = new bool[p.Parts.Length];
            for (int i = 0; i < p.Parts.Length; i++)
            {
                p.PartOn[i] = p.Parts[i].enabled;
                p.Parts[i].enabled = false;
            }
            p.Body = hm.GetComponent<Rigidbody2D>();
            if (p.Body != null)
            {
                p.BodyType = p.Body.bodyType;
                p.Body.linearVelocity = Vector2.zero;
                p.Body.bodyType = RigidbodyType2D.Kinematic;
            }
            p.Active = hm.gameObject.activeSelf;
            hidden.Remove(hm);
            puppets[(scene, id)] = p;
            puppetOf[hm] = p;
            return p;
        }

        static void Release(Puppet p)
        {
            puppets.Remove((p.Scene, p.Id));
            // By reference: a destroyed enemy (its scene unloaded) goes from the table too.
            puppetOf.Remove(p.Hm);
            if (p.Hm == null) return;
            for (int i = 0; i < p.Fsms.Length; i++)
                if (p.Fsms[i] != null) p.Fsms[i].enabled = p.FsmOn[i];
            for (int i = 0; i < p.Parts.Length; i++)
                if (p.Parts[i] != null) p.Parts[i].enabled = p.PartOn[i];
            if (p.Body != null) p.Body.bodyType = p.BodyType;
        }

        static void Apply(Puppet p, byte f, Vector2 world, Vector2 scale, float rot, int clip, int frame, int hp)
        {
            var go = p.Hm.gameObject;
            p.Seen = Time.unscaledTime;
            bool active = (f & FActive) != 0;
            if (active && !go.activeSelf) go.SetActive(true);
            else if (!active && go.activeSelf && (f & FDead) == 0) go.SetActive(false);
            if ((f & FDead) != 0) return;
            if (p.Samples.Count > 0 && (p.Samples[p.Samples.Count - 1].P - world).sqrMagnitude > 64f) p.Samples.Clear();
            p.Samples.Add((Time.unscaledTime, world));
            while (p.Samples.Count > 12) p.Samples.RemoveAt(0);
            var tr = p.Hm.transform;
            tr.localScale = new Vector3(scale.x, scale.y, tr.localScale.z);
            var e = tr.eulerAngles;
            tr.eulerAngles = new Vector3(e.x, e.y, rot);
            if (p.Rend != null) p.Rend.enabled = (f & FRend) != 0;
            if (hp > 0) p.Hm.hp = hp;
            p.Clip = clip;
            p.Frame = frame;
            if (p.Anim != null && clip >= 0 && go.activeInHierarchy)
            {
                var c = p.Anim.GetClipById(clip);
                if (c != null)
                {
                    if (p.Anim.CurrentClip != c) p.Anim.PlayFromFrame(c, Mathf.Clamp(frame, 0, Mathf.Max(0, c.frames.Length - 1)));
                    else if (Mathf.Abs(p.Anim.CurrentFrame - frame) > 1 && frame < c.frames.Length) p.Anim.SetFrame(frame);
                }
            }
        }

        /// <summary>EnemyDeath from a scene's owner: the puppet given its scripts back and killed the game's way.</summary>
        public static void ReceiveDeath(int owner, BinaryReader r)
        {
            string scene = r.ReadString();
            int id = r.ReadInt32();
            float dir = r.ReadSingle();
            var type = (AttackTypes)r.ReadByte();
            if (!puppets.TryGetValue((scene, id), out var p) || p.Hm == null) return;
            DevDeathsReceived++;
            var hm = p.Hm;
            Release(p);
            if (!hm.gameObject.activeSelf) hm.gameObject.SetActive(true);
            if (hm.isDead) return;
            // A splatter would destroy it at once; a plain death leaves the corpse.
            hm.hp = 0;
            hm.Die(dir, type == AttackTypes.Splatter ? AttackTypes.Generic : type, true);
        }

        public static void ReceiveHit(int from, BinaryReader r) { }

        /// <summary>Ownership moved: the scene's puppets and hidden ones are this game's again (the new owner's numbers come with its snapshots).</summary>
        public static void OwnerChanged(string scene)
        {
            foreach (var p in new List<Puppet>(puppets.Values))
                if (p.Scene == scene) Release(p);
            Unhide(scene);
            var gone = new List<HealthManager>();
            foreach (var kv in tracked)
                if (kv.Value.Scene == scene) gone.Add(kv.Key);
            foreach (var hm in gone)
            {
                trackedById.Remove((scene, tracked[hm].Id));
                tracked.Remove(hm);
            }
            Plugin.Log.LogInfo($"[联机] {scene} 的敌人现在由{(NetSession.Owners.TryGetValue(scene, out int o) ? (o == NetSession.MySlot ? "自己" : NetSession.Player(o)?.Name ?? "?") : "没人")}负责");
        }

        /// <summary>Each frame: the puppets eased along the owner's track (a tenth of a second behind); one the owner has stopped sending goes.</summary>
        public static void Tick()
        {
            if (puppets.Count == 0) return;
            float t = Time.unscaledTime - 0.1f;
            List<Puppet> stale = null;
            foreach (var p in puppets.Values)
            {
                if (p.Hm == null || Time.unscaledTime - p.Seen > 2f)
                {
                    (stale ?? (stale = new List<Puppet>())).Add(p);
                    continue;
                }
                if (p.Samples.Count == 0) continue;
                Vector2 at = p.Samples[p.Samples.Count - 1].P;
                for (int i = p.Samples.Count - 1; i > 0; i--)
                {
                    var a = p.Samples[i - 1];
                    var b = p.Samples[i];
                    if (t < a.T) continue;
                    if (t > b.T) break;
                    at = Vector2.Lerp(a.P, b.P, b.T - a.T > 1e-4f ? (t - a.T) / (b.T - a.T) : 1f);
                    break;
                }
                if (t < p.Samples[0].T) at = p.Samples[0].P;
                var tr = p.Hm.transform;
                tr.position = new Vector3(at.x, at.y, tr.position.z);
                if (p.Body != null) p.Body.position = at;
            }
            if (stale == null) return;
            foreach (var p in stale)
            {
                // Gone from the owner's game (or this scene's owner changed without a word): hidden until it comes back.
                bool own = p.Hm != null && RemoteOwned(p.Scene);
                var hm = p.Hm;
                Release(p);
                if (own && hm != null && !hm.isDead && hm.gameObject.activeSelf)
                {
                    hidden[hm] = p.Scene;
                    hm.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Dev: how an enemy is named in the snapshots.</summary>
        public static string DevPathOf(HealthManager hm) => PathOf(hm.transform);

        /// <summary>Dev: an enemy's number in the owner's snapshots (this game the owner), -1 if it has none yet.</summary>
        public static int DevIdOf(HealthManager hm) => tracked.TryGetValue(hm, out var t) ? t.Id : -1;

        /// <summary>Dev: the puppet's number, -1 if the enemy isn't one.</summary>
        public static int DevPuppetId(HealthManager hm) => puppetOf.TryGetValue(hm, out var p) ? p.Id : -1;

        /// <summary>Dev: whether the enemy's scripts are switched off (it is a puppet now).</summary>
        public static bool DevScriptsOff(HealthManager hm)
        {
            foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.enabled) return false;
            return true;
        }

        /// <summary>Dev: what the follower side is doing now.</summary>
        public static string DevReport()
        {
            int vis = 0;
            foreach (var p in puppets.Values) if (p.Hm != null && p.Hm.gameObject.activeInHierarchy) vis++;
            return $"傀儡 {puppets.Count}（显示 {vis}），藏起 {hidden.Count}，自己负责的 {tracked.Count}，转交伤害 {DevForwarded}，结算别人的伤害 {DevApplied}，发出死亡 {DevDeathsSent}，收到死亡 {DevDeathsReceived}";
        }
    }
}
