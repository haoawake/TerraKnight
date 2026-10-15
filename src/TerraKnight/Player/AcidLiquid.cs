using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Player
{
    /// <summary>
    /// Hallownest's acid and Terraria's liquid gear. Each acid pool is a pair the scene's FSMs run (nothing in code): the
    /// Acid Box, a trigger that hurts (a hazard respawn), and over it the Surface Water Region the Knight swims in (the
    /// hero's own Surface Water FSM). Both ask "has Isma's Tear?" (hasAcidArmour) once, a frame after the room loads,
    /// and only ever switch on: the box's damage to 0, the region's collider on. Here the pools follow the gear as it
    /// is worn: the flippers (the shells, the diving gear, the Flipper potion: accFlipper) swim in acid as Isma's Tear
    /// does, put on or taken off in the room (taken off, the acid hurts again once the Knight is out of it). Water
    /// walking (the boots, the Water Walking potion: Player.waterWalk) walks on it: a one-way top over each pool, gone
    /// while down is held (Terraria's way in).
    /// </summary>
    internal static class AcidLiquid
    {
        sealed class Pool
        {
            public DamageHero Box;
            public int Damage = 1;
            public BoxCollider2D Region;
            public GameObject Top;
        }

        static readonly List<Pool> pools = new List<Pool>();
        static int sceneSignature;
        static float rescanAt = -1f;
        static PlayMakerFSM surfaceFsm;
        static HeroController surfaceHero;

        public static int DevPools => pools.Count;
        public static bool DevSwimOn { get; private set; }
        public static bool DevWalkOn { get; private set; }

        /// <summary>The Knight floating or swimming in a pool: the hero's own Surface Water FSM out of its dry state (cState.swimming lasts a frame).</summary>
        public static bool Swimming(HeroController hero)
        {
            if (hero == null) return false;
            if (surfaceHero != hero || surfaceFsm == null)
            {
                surfaceHero = hero;
                surfaceFsm = PlayMakerFSM.FindFsmOnGameObject(hero.gameObject, "Surface Water");
            }
            if (surfaceFsm == null) return false;
            string s = surfaceFsm.ActiveStateName;
            return s == "Idle" || s.StartsWith("Swim");
        }

        /// <summary>Dev: the hero's Surface Water FSM state.</summary>
        static string SurfaceState()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return "-";
            Swimming(hero);
            return surfaceFsm != null ? surfaceFsm.ActiveStateName : "无";
        }

        /// <summary>Call every frame in gameplay.</summary>
        public static void Tick()
        {
            var hero = HeroController.SilentInstance;
            var pd = PlayerData.instance;
            if (hero == null || pd == null) return;
            // The rooms loaded (a neighbour joining the seamless world adds one): their pools, found again, and once
            // more a moment later, after their own check has run.
            int sig = 17;
            for (int i = 0; i < USM.sceneCount; i++) sig = sig * 31 + USM.GetSceneAt(i).handle;
            if (sig != sceneSignature)
            {
                sceneSignature = sig;
                Scan();
                rescanAt = Time.unscaledTime + 1f;
            }
            else if (rescanAt > 0f && Time.unscaledTime >= rescanAt)
            {
                rescanAt = -1f;
                Scan();
            }
            if (pools.Count == 0) return;
            var gear = Items.Equipment.Current;
            bool swim = pd.hasAcidArmour || gear.Swim;
            // Out of the pool before the acid hurts again (the region turned off under a floating Knight drops him in).
            if (!swim && Swimming(hero)) swim = true;
            bool walk = gear.WaterWalk > 0 && !TerraMovement.DownHeld;
            DevSwimOn = swim;
            DevWalkOn = walk;
            for (int i = pools.Count - 1; i >= 0; i--)
            {
                var p = pools[i];
                if (p.Box == null && p.Region == null)
                {
                    if (p.Top != null) Object.Destroy(p.Top);
                    pools.RemoveAt(i);
                    continue;
                }
                if (p.Box != null) p.Box.damageDealt = swim ? 0 : p.Damage;
                if (p.Region != null && p.Region.enabled != swim) p.Region.enabled = swim;
                if (p.Top != null && p.Top.activeSelf != walk) p.Top.SetActive(walk);
            }
        }

        /// <summary>The acid pools of the rooms loaded: each Acid Box with the swim region beside it under its control.</summary>
        static void Scan()
        {
            var boxes = new Dictionary<Transform, Pool>();
            var have = new HashSet<Object>();
            foreach (var p in pools)
            {
                if (p.Box != null) have.Add(p.Box);
                if (p.Region != null) have.Add(p.Region);
            }
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fsm == null || fsm.FsmName != "Acid Armour Check") continue;
                var go = fsm.gameObject;
                var parent = go.transform.parent != null ? go.transform.parent : go.transform;
                var dh = go.GetComponent<DamageHero>();
                var region = PlayMakerFSM.FindFsmOnGameObject(go, "Surface Water Region") != null ? go.GetComponent<BoxCollider2D>() : null;
                if (dh != null && have.Contains(dh) || region != null && have.Contains(region)) continue;
                if (dh == null && region == null) continue;
                if (!boxes.TryGetValue(parent, out var pool))
                {
                    pool = new Pool();
                    boxes[parent] = pool;
                }
                if (dh != null)
                {
                    pool.Box = dh;
                    // The check sets the damage to 0 once it finds the Tear; every pool hurts for a mask.
                    pool.Damage = dh.damageDealt > 0 ? dh.damageDealt : 1;
                }
                if (region != null) pool.Region = region;
            }
            foreach (var pool in boxes.Values)
            {
                pool.Top = MakeTop(pool);
                pools.Add(pool);
            }
        }

        /// <summary>A one-way top over the pool, on the terrain layer as the grid's platforms are (the Knight's ground rays meet it).</summary>
        static GameObject MakeTop(Pool p)
        {
            Bounds? span = null;
            void Add(Collider2D c)
            {
                if (c == null) return;
                // A collider switched off has no bounds: its box by hand.
                Bounds b = c.enabled && c.gameObject.activeInHierarchy ? c.bounds : BoxBounds(c as BoxCollider2D);
                if (b.size == Vector3.zero) return;
                if (span == null) span = b;
                else { var s = span.Value; s.Encapsulate(b); span = s; }
            }
            Add(p.Box != null ? p.Box.GetComponent<Collider2D>() : null);
            Add(p.Region);
            if (span == null) return null;
            var area = span.Value;
            const float thick = 0.25f;
            var parent = (p.Box != null ? p.Box.transform : p.Region.transform).parent;
            var go = new GameObject("Terra Water Walk Top") { layer = ChunkColliders.TerrainLayer };
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(area.center.x, area.max.y + 0.03f - thick / 2f, 0f);
            go.AddComponent<TerraOwned>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.usedByEffector = true;
            box.size = new Vector2(area.size.x, thick);
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            go.SetActive(false);
            return go;
        }

        static Bounds BoxBounds(BoxCollider2D box)
        {
            if (box == null) return default;
            var t = box.transform;
            Vector2 c = t.TransformPoint(box.offset), s = Vector2.Scale(box.size, t.lossyScale);
            return new Bounds(c, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 0f));
        }

        /// <summary>Dev: the pools found, each box's damage, region and top as they are now.</summary>
        public static string DevReport()
        {
            var sb = new System.Text.StringBuilder($"{pools.Count} 个酸池，游泳 {DevSwimOn}，水上行走 {DevWalkOn}，在池里 {Swimming(HeroController.SilentInstance)}，状态机 {SurfaceState()}");
            foreach (var p in pools)
                sb.Append($"；{(p.Box != null ? p.Box.name : "-")} 伤害 {(p.Box != null ? p.Box.damageDealt : -1)} 游泳区 {(p.Region != null ? p.Region.enabled.ToString() : "-")} 顶 {(p.Top != null ? p.Top.activeSelf + " y" + p.Top.transform.position.y.ToString("F2") : "-")}");
            return sb.ToString();
        }

        /// <summary>Dev: a pool's surface (the top's height), for a test to stand on.</summary>
        public static bool DevSurface(out Vector2 at)
        {
            at = default;
            foreach (var p in pools)
            {
                if (p.Top == null) continue;
                at = p.Top.transform.position;
                at.y += 0.125f;
                // A spot open from above, where a fall meets the top first (not a ledge over the pool).
                var box = p.Top.GetComponent<BoxCollider2D>();
                if (p.Top.activeInHierarchy && box != null)
                    for (int i = 1; i < 10; i++)
                    {
                        float x = box.bounds.min.x + box.bounds.size.x * i / 10f;
                        var hit = Physics2D.Raycast(new Vector2(x, at.y + 3f), Vector2.down, 4f, 1 << ChunkColliders.TerrainLayer);
                        if (hit.collider == box) { at = new Vector2(x, at.y); break; }
                    }
                return true;
            }
            return false;
        }
    }
}
