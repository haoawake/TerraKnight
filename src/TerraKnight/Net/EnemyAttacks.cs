using System.Collections.Generic;
using System.IO;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>Authoritative enemy contact and projectile hitboxes, evaluated against each local player.</summary>
    [HarmonyPatch]
    internal static class EnemyAttacks
    {
        sealed class Ghost
        {
            public GameObject Go;
            public Collider2D Collider;
            public DamageHero Damage;
            public float Seen;
            public tk2dSprite Sprite;
            public float Factor = 1f;
        }
        static readonly Dictionary<(string Scene, int Id), Ghost> ghosts = new Dictionary<(string, int), Ghost>();
        static readonly Dictionary<string, tk2dSpriteCollectionData> collections = new Dictionary<string, tk2dSpriteCollectionData>();
        static readonly Dictionary<string, HashSet<int>> received = new Dictionary<string, HashSet<int>>();
        public static int DevHits, DevReceived, DevOverlaps, DevTickSkips;
        public static int DevGhosts => ghosts.Count;
        public static int DevLiveGhosts
        {
            get { int count = 0; foreach (var g in ghosts.Values) if (g.Go != null) count++; return count; }
        }
        public static void DevDestroyGhostObjects()
        { foreach (var g in ghosts.Values) if (g.Go != null) Object.Destroy(g.Go); }

        static DamageHero[] hurting = new DamageHero[0];
        static int hurtingFrame = -1;
        static readonly List<PlayMakerFSM> hurtingFsms = new List<PlayMakerFSM>();
        static float hurtingFsmsAt = -10f;

        public static void Send(string scene, Vector2 shift)
        {
            // These objects include dynamically spawned shots with no HealthManager. Their
            // local replicas' FSMs cannot create the owner's attack timing or positions.
            // Looked up once a round for all the owned scenes; the FSMs that hurt (a slow search) four times a second.
            if (hurtingFrame != Time.frameCount)
            {
                hurtingFrame = Time.frameCount;
                hurting = Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None);
            }
            if (Time.unscaledTime - hurtingFsmsAt > 0.25f)
            {
                hurtingFsmsAt = Time.unscaledTime;
                hurtingFsms.Clear();
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (fsm.FsmName == "damages_hero" && fsm.GetComponent<DamageHero>() == null) hurtingFsms.Add(fsm);
            }
            foreach (var damage in hurting)
            {
                if (damage == null) continue;
                if (!Belongs(damage.gameObject, scene) || damage.hazardType != 1 || damage.damageDealt <= 0) continue;
                foreach (var collider in damage.GetComponents<Collider2D>())
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    SendCollider(scene, shift, collider, damage.damageDealt, damage.shadowDashHazard);
                }
            }
            foreach (var fsm in hurtingFsms)
            {
                if (fsm == null || !fsm.isActiveAndEnabled || !Belongs(fsm.gameObject, scene)) continue;
                int damage = FSMUtility.GetInt(fsm, "damageDealt");
                if (damage <= 0 || FSMUtility.GetInt(fsm, "hazardType") != 1) continue;
                foreach (var collider in fsm.GetComponents<Collider2D>())
                    if (collider.enabled) SendCollider(scene, shift, collider, damage, false);
            }
            // The new enemy AI's warnings and webs: no live hitbox, but the others must see them (EnemyAi.NetAi).
            foreach (var collider in EnemyAi.NetAi.Unarmed(scene)) SendCollider(scene, shift, collider, 0, false);
            var end = NetSession.Mine(Msg.EnemyAttacks);
            end.W.Write(scene); end.W.Write(0);
            NetSession.Send(end.Done());
        }

        /// <summary>
        /// Whether a hurting object is one of the scene's attacks (sent with its round): the scene's own, or one of the game's
        /// global pool in the scene's room. The pool keeps its objects in DontDestroyOnLoad (Nosk's vomit globs, the Soul
        /// Master's orbs), so they went to no one and hurt only the scene's owner (the user's 10-27 "boss对联机队友依旧不造成伤害，
        /// 比如诺斯克的弹幕").
        /// </summary>
        static bool Belongs(GameObject go, string scene)
        {
            string own = go.scene.name;
            if (own == scene) return true;
            if (own != "DontDestroyOnLoad" || DevSceneOnly) return false;
            var cell = World.CellGrid.WorldToCell(go.transform.position);
            return World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName == scene;
        }

        /// <summary>Dev: only a scene's own objects (as before), to compare.</summary>
        internal static bool DevSceneOnly;

        /// <summary>Dev (suite bossshots): whether a hitbox would go to the other players with a room's (or its boss scene's) round.</summary>
        internal static bool DevWouldSend(DamageHero d, string room) => d != null && (Belongs(d.gameObject, room) || Belongs(d.gameObject, room + "_boss"));

        static void SendCollider(string scene, Vector2 shift, Collider2D collider, int amount, bool shadow)
        {
            var p = NetSession.Mine(Msg.EnemyAttacks);
            var w = p.W;
            w.Write(scene); w.Write(collider.GetInstanceID());
            var tr = collider.transform;
            Vector2 at = (Vector2)tr.position - shift;
            w.Write(at.x); w.Write(at.y); w.Write(tr.eulerAngles.z);
            w.Write(tr.lossyScale.x); w.Write(tr.lossyScale.y);
            w.Write(collider.offset.x); w.Write(collider.offset.y);
            w.Write(amount); w.Write(shadow);
            // The new enemy AI's shots and areas: their own enemy's factor.
            var source = collider.GetComponentInParent<HealthManager>() ?? EnemyAi.Hostiles.OwnerOf(collider.gameObject);
            float factor = source != null ? Items.EnemyStats.DamageFactor(Items.EnemyStats.Of(source)) : 1f;
            if (source == null)
            {
                float best = 30f;
                foreach (var info in Items.EnemyStats.All)
                {
                    if (info.Hm == null || info.Hm.isDead || !info.Hm.gameObject.activeInHierarchy || !Items.EnemyStats.Unstoppable(info)) continue;
                    float distance = Vector2.Distance(info.Hm.transform.position, collider.transform.position);
                    if (distance < best) { best = distance; factor = Items.EnemyStats.DamageFactor(info); }
                }
            }
            w.Write(factor);
            if (collider is CircleCollider2D circle) { w.Write((byte)1); w.Write(circle.radius); }
            else if (collider is PolygonCollider2D polygon)
            {
                w.Write((byte)2); w.Write((ushort)polygon.pathCount);
                for (int i = 0; i < polygon.pathCount; i++)
                {
                    var points = polygon.GetPath(i);
                    w.Write((ushort)points.Length);
                    foreach (var point in points) { w.Write(point.x); w.Write(point.y); }
                }
            }
            else
            {
                w.Write((byte)0);
                Vector2 size = collider is BoxCollider2D box ? box.size : new Vector2(
                    collider.bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(tr.lossyScale.x)),
                    collider.bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(tr.lossyScale.y)));
                w.Write(size.x); w.Write(size.y);
            }
            // Standalone attacks need a picture as well as a hitbox. Enemy child sprites
            // already come through EnemyParts, so they must not be drawn twice.
            var sprite = collider.GetComponent<tk2dBaseSprite>();
            bool visual = collider.GetComponentInParent<HealthManager>() == null && sprite != null && sprite.Collection != null && !SceneMirror.Covered(collider.transform);
            w.Write(visual ? sprite.Collection.name : "");
            if (visual)
            {
                w.Write(sprite.spriteId); w.Write(sprite.color.r); w.Write(sprite.color.g); w.Write(sprite.color.b); w.Write(sprite.color.a);
                var rend = sprite.GetComponent<Renderer>();
                w.Write(rend != null && rend.enabled);
                w.Write(rend != null ? rend.sortingOrder : 0);
                w.Write(tr.position.z);
            }
            // The new enemy AI's part: a shot's look and flight, an area's warning, what its touch carries.
            EnemyAi.NetAi.WriteExtra(w, collider);
            NetSession.Send(p.Done());
        }

        public static void Receive(int owner, BinaryReader r)
        {
            string scene = r.ReadString();
            if (!Enemies.RemoteOwned(scene) || !NetSession.Owners.TryGetValue(scene, out int current) || current != owner
                || !NetScenes.Shift(scene, out var shift)) return;
            int id = r.ReadInt32();
            if (!received.TryGetValue(scene, out var seen)) received[scene] = seen = new HashSet<int>();
            if (id == 0)
            {
                foreach (var kv in new List<KeyValuePair<(string Scene, int Id), Ghost>>(ghosts))
                    if (kv.Key.Scene == scene && !seen.Contains(kv.Key.Id))
                    { if (kv.Value.Go != null) Object.Destroy(kv.Value.Go); ghosts.Remove(kv.Key); }
                seen.Clear();
                return;
            }
            seen.Add(id);
            Vector2 at = new Vector2(r.ReadSingle(), r.ReadSingle()) + shift;
            float angle = r.ReadSingle();
            var scale = new Vector3(r.ReadSingle(), r.ReadSingle(), 1f);
            var offset = new Vector2(r.ReadSingle(), r.ReadSingle());
            int amount = r.ReadInt32(); bool shadow = r.ReadBoolean();
            float factor = r.ReadSingle();
            byte shape = r.ReadByte();
            if (!ghosts.TryGetValue((scene, id), out var ghost) || ghost.Go == null)
            {
                var go = new GameObject("TK Enemy Attack");
                go.AddComponent<World.TerraOwned>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, NetScenes.FindScene(scene));
                ghost = new Ghost { Go = go, Damage = go.AddComponent<DamageHero>() };
                ghosts[(scene, id)] = ghost;
            }
            var g = ghost;
            g.Seen = Time.unscaledTime;
            g.Factor = factor;
            g.Go.transform.position = new Vector3(at.x, at.y, 0f);
            g.Go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            g.Go.transform.localScale = scale;
            if (shape == 1)
            {
                if (!(g.Collider is CircleCollider2D)) Replace(g, g.Go.AddComponent<CircleCollider2D>());
                ((CircleCollider2D)g.Collider).radius = r.ReadSingle();
            }
            else if (shape == 2)
            {
                if (!(g.Collider is PolygonCollider2D)) Replace(g, g.Go.AddComponent<PolygonCollider2D>());
                var polygon = (PolygonCollider2D)g.Collider;
                polygon.pathCount = r.ReadUInt16();
                for (int i = 0; i < polygon.pathCount; i++)
                {
                    var points = new Vector2[r.ReadUInt16()];
                    for (int j = 0; j < points.Length; j++) points[j] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    polygon.SetPath(i, points);
                }
            }
            else
            {
                if (!(g.Collider is BoxCollider2D)) Replace(g, g.Go.AddComponent<BoxCollider2D>());
                ((BoxCollider2D)g.Collider).size = new Vector2(r.ReadSingle(), r.ReadSingle());
            }
            g.Collider.offset = offset;
            g.Collider.isTrigger = true;
            g.Damage.damageDealt = amount; g.Damage.hazardType = 1; g.Damage.shadowDashHazard = shadow;
            string collection = r.ReadString();
            if (collection.Length > 0)
            {
                int spriteId = r.ReadInt32();
                var color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                bool visible = r.ReadBoolean(); int order = r.ReadInt32(); float z = r.ReadSingle();
                if (!collections.TryGetValue(collection, out var data) || data == null)
                    foreach (var c in Resources.FindObjectsOfTypeAll<tk2dSpriteCollectionData>())
                        if (c.name == collection) { collections[collection] = data = c; break; }
                if (data != null && spriteId >= 0 && spriteId < data.spriteDefinitions.Length)
                {
                    if (g.Sprite == null) g.Sprite = g.Go.AddComponent<tk2dSprite>();
                    g.Sprite.SetSprite(data, spriteId); g.Sprite.color = color;
                    var rend = g.Sprite.GetComponent<Renderer>();
                    rend.enabled = visible; rend.sortingOrder = order;
                    g.Go.transform.position = new Vector3(at.x, at.y, z);
                }
            }
            else if (g.Sprite != null) g.Sprite.GetComponent<Renderer>().enabled = false;
            EnemyAi.NetAi.ReadExtra(r, g.Go, g.Collider, at);
            DevReceived++;
        }

        static void Replace(Ghost g, Collider2D collider)
        {
            if (g.Collider != null) { g.Collider.enabled = false; Object.Destroy(g.Collider); }
            g.Collider = collider;
        }

        public static void Tick()
        {
            var hero = HeroController.SilentInstance;
            var heroCollider = hero != null ? hero.GetComponent<Collider2D>() : null;
            foreach (var kv in new List<KeyValuePair<(string Scene, int Id), Ghost>>(ghosts))
            {
                var g = kv.Value;
                if (g.Go == null || Time.unscaledTime - g.Seen > 2f || !Enemies.RemoteOwned(kv.Key.Scene))
                { if (g.Go != null) Object.Destroy(g.Go); ghosts.Remove(kv.Key); continue; }
                if (heroCollider == null || !heroCollider.enabled || hero.cState.dead || HeroBox.inactive
                    || (hero.cState.shadowDashing && g.Damage.shadowDashHazard)) { DevTickSkips++; continue; }
                // Both bodies can be kinematic in TerraMovement. Evaluate the mirrored shape
                // explicitly, rather than relying on Unity generating a trigger callback.
                if (g.Damage.damageDealt > 0 && g.Collider != null && g.Collider.enabled && Physics2D.Distance(heroCollider, g.Collider).isOverlapped)
                {
                    DevOverlaps++;
                    int before = TerraMod.Instance?.Stats.Life ?? 0;
                    hero.TakeDamage(g.Go, g.Go.transform.position.x > hero.transform.position.x ? CollisionSide.right : CollisionSide.left, g.Damage.damageDealt, 1);
                    if ((TerraMod.Instance?.Stats.Life ?? before) < before) DevHits++;
                }
            }
        }

        // A remote scene's own attack FSMs can still leave local hitboxes enabled; only
        // the owner's mirrored attack is allowed to hurt, at the owner's timing/position.
        [HarmonyPrefix, HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static bool Damage(GameObject go, int hazardType)
        {
            if (go == null || hazardType != 1 || !Enemies.RemoteOwned(go.scene.name)) return true;
            foreach (var g in ghosts.Values) if (g.Go == go) return true;
            return go.GetComponent<DamageHero>() == null && !FSMUtility.ContainsFSM(go, "damages_hero");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player.PlayerHooks), "SourceFactor")]
        static void Factor(GameObject go, ref float __result)
        {
            foreach (var g in ghosts.Values) if (g.Go == go) { __result = g.Factor; return; }
        }

        public static void OwnerChanged(string scene)
        {
            received.Remove(scene);
            foreach (var kv in new List<KeyValuePair<(string Scene, int Id), Ghost>>(ghosts))
                if (kv.Key.Scene == scene) { if (kv.Value.Go != null) Object.Destroy(kv.Value.Go); ghosts.Remove(kv.Key); }
        }
        public static void Reset()
        {
            foreach (var g in ghosts.Values) if (g.Go != null) Object.Destroy(g.Go);
            ghosts.Clear(); collections.Clear(); received.Clear();
        }
    }
}
