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
    /// while down is held (Terraria's way in). Obsidian Skin (lavaImmune) goes into the acid unharmed as into lava, the
    /// flippers' way here; the Lava Charm and its kin (lavaMax 420) for seven seconds at a time, filled again out of it.
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

        /// <summary>The Lava Charm's time left in the acid, seconds (Terraria's lavaTime, a tick each tick in and out); full to begin with, as on spawning.</summary>
        static float lavaTime = float.MaxValue;

        public static int DevPools => pools.Count;
        public static bool DevSwimOn { get; private set; }
        public static bool DevWalkOn { get; private set; }

        /// <summary>The Knight floating or swimming in a pool: the hero's own Surface Water FSM out of its dry state (cState.swimming lasts a frame).</summary>
        public static bool Swimming(HeroController hero)
        {
            if (hero == null) return false;
            if (PouredSwimming.For(hero)) return true;
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

        /// <summary>The rooms loaded (a neighbour joining the seamless world adds one): their pools, found again, and once more a moment later, after their own check has run.</summary>
        static void Refresh()
        {
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
        }

        /// <summary>Call every frame in gameplay.</summary>
        public static void Tick()
        {
            var hero = HeroController.SilentInstance;
            var pd = PlayerData.instance;
            if (hero == null || pd == null) return;
            Refresh();
            var gear = Items.Equipment.Current;
            float lavaMax = gear.LavaImmuneTicks / 60f;
            lavaTime = Mathf.Min(lavaTime, lavaMax);
            if (pools.Count == 0)
            {
                lavaTime = Mathf.Min(lavaMax, lavaTime + Time.deltaTime);
                return;
            }
            // Obsidian Skin, or the Ash Wood set with the Lava Rose (Pl:27883), as the flippers.
            // Defender's Crest too: the acid harms nothing, swum in anywhere (CharmEffects.AcidSwim).
            bool swim = Mounts.LavaImmune || pd.hasAcidArmour || gear.Swim || gear.LavaImmune || gear.AshWood && gear.LavaRose || Items.CharmEffects.AcidSwim;
            bool spent = false;
            if (!swim && lavaMax > 0f)
            {
                // The charm's seconds go while the Knight is in the acid and come back out of it (Pl:27889-27931).
                bool inAcid = PouredSwimming.InAcid || !PouredSwimming.Wet && Swimming(hero);
                lavaTime = inAcid ? Mathf.Max(0f, lavaTime - Time.deltaTime) : Mathf.Min(lavaMax, lavaTime + Time.deltaTime);
                if (lavaTime > 0f) swim = true;
                else spent = inAcid;
            }
            else lavaTime = Mathf.Min(lavaMax, lavaTime + Time.deltaTime);
            // Out of the pool before the acid hurts again (the region turned off under a floating Knight drops him in);
            // the charm's time run out in it, the acid hurts at once, as the lava does.
            if (!swim && !spent && (PouredSwimming.Active ? PouredSwimming.InAcid : Swimming(hero))) swim = true;
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
                if (p.Region != null && p.Region.GetComponent<PouredLiquidRegion>() != null) p.Region.enabled = false;
                else if (p.Region != null && p.Region.enabled != swim) p.Region.enabled = swim;
                if (p.Top != null && p.Top.activeSelf != walk)
                {
                    // Switched on again: its passes through are made afresh (a collider switched off may forget them).
                    if (walk) passing.Clear();
                    p.Top.SetActive(walk);
                }
                if (walk && p.Top != null) KeepEnemiesOff(p.Top);
            }
            if (swim) Refloat(hero);
            else sunkSince = -1f;
        }

        static float sunkSince = -1f;
        public static int DevRefloats;

        /// <summary>
        /// A hit while floating takes the hero's Surface Water FSM out of the water (the game's knockback throws him clear;
        /// Terraria's hardly moves him), and he sinks to the pool's floor inside the swim region, whose trigger only told
        /// of his coming in: stuck under the acid. Out of the water in a pool he may swim in for a third of a second, not
        /// recoiling or dying: lifted into the region if he sank under it, and the region switched off for a frame (the
        /// pools' tick switches it on again), so that it tells of him coming in again and the game's own swim floats him up.
        /// </summary>
        static void Refloat(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            if (col == null || Swimming(hero) || hero.cState.recoiling || hero.cState.hazardDeath || hero.cState.dead || hero.cState.transitioning
                || hero.cState.hazardRespawning)
            {
                sunkSince = -1f;
                return;
            }
            Vector2 c = col.bounds.center;
            Pool sunk = null;
            Bounds region = default, acid = default;
            foreach (var p in pools)
            {
                if (p.Region != null && p.Region.GetComponent<PouredLiquidRegion>() != null) continue;
                if (p.Region == null || !p.Region.enabled || !p.Region.gameObject.activeInHierarchy) continue;
                region = p.Region.bounds;
                acid = p.Box != null ? AreaOf(p.Box.GetComponent<Collider2D>()) : region;
                bool inRegion = region.min.x < c.x && c.x < region.max.x && region.min.y < c.y && c.y < region.max.y;
                // Under it, in the acid (the hurting box reaches the floor; the region may stop above it).
                bool under = acid.size != Vector3.zero && acid.min.x < c.x && c.x < acid.max.x && acid.min.y - 0.5f < c.y && c.y < region.min.y;
                if (inRegion || under) { sunk = p; break; }
            }
            if (sunk == null) { sunkSince = -1f; return; }
            if (!PouredSwimming.ClearRise(hero, region.max.y)) { sunkSince = -1f; return; }
            if (sunkSince < 0f) { sunkSince = Time.time; return; }
            if (Time.time - sunkSince < 0.35f) return;
            sunkSince = -1f;
            if (c.y < region.min.y + 0.1f)
            {
                var t = hero.transform;
                t.position += new Vector3(0f, region.min.y + 0.1f + col.bounds.extents.y * 0.5f - c.y, 0f);
            }
            sunk.Region.enabled = false;
            DevRefloats++;
            Plugin.Log.LogInfo($"[酸液] 骑士漂着时挨打沉到了池底：重新浮起（游泳区 y {region.min.y:F1}~{region.max.y:F1}，酸 y {acid.min.y:F1}~{acid.max.y:F1}）");
        }

        /// <summary>Enemy bodies already told to pass through a pool's top (the top's id and the collider's).</summary>
        static readonly HashSet<long> passing = new HashSet<long>();
        public static int DevEnemiesThrough;

        /// <summary>
        /// The walking top holds the Knight only (Player.waterWalk is his): every enemy body near the pool passes through it
        /// as through the water, the top being on the terrain layer the enemies stand on too.
        /// </summary>
        static void KeepEnemiesOff(GameObject top)
        {
            var box = top.GetComponent<BoxCollider2D>();
            if (box == null || !box.enabled) return;
            var span = box.bounds;
            span.Expand(new Vector3(6f, 12f, 0f));
            if (passing.Count > 8192) passing.Clear();
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || !hm.gameObject.activeInHierarchy) continue;
                if (!span.Contains(new Vector3(hm.transform.position.x, hm.transform.position.y, span.center.z))) continue;
                long key = ((long)box.GetInstanceID() << 32) ^ (uint)hm.GetInstanceID();
                if (!passing.Add(key)) continue;
                // Each of its bodies once (those switched on later too: the pair is kept while both live).
                foreach (var c in hm.GetComponentsInChildren<Collider2D>(true))
                    if (c != null && !c.isTrigger) Physics2D.IgnoreCollision(box, c, true);
                DevEnemiesThrough++;
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
                // The mod's own acid (World.LiquidView) comes in through Add; its templates are no pools.
                if (go.GetComponentInParent<TerraOwned>(true) != null) continue;
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

        /// <summary>
        /// Acid poured by the player (World.LiquidView): its box and the swim region over it (none for shallow acid, or
        /// before a scene's acid pool was met) follow the gear as the scene's own pools do. Dropped once both are gone.
        /// </summary>
        public static void Add(DamageHero box, BoxCollider2D region)
        {
            if (box == null && region == null) return;
            var pool = new Pool { Box = box, Region = region, Damage = 1 };
            pool.Top = MakeTop(pool);
            pools.Add(pool);
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

        /// <summary>Whether an area (world units) reaches into one of the loaded rooms' acid pools: its hurting box or the swim region over it.</summary>
        public static bool InAcid(Bounds area)
        {
            Refresh();
            foreach (var p in pools)
            {
                if (p.Box != null && Overlaps(AreaOf(p.Box.GetComponent<Collider2D>()), area)) return true;
                if (p.Region != null && Overlaps(AreaOf(p.Region), area)) return true;
            }
            return false;
        }

        /// <summary>A collider's area; one switched off has no bounds, a box's worked out by hand.</summary>
        static Bounds AreaOf(Collider2D c) =>
            c == null ? default : c.enabled && c.gameObject.activeInHierarchy ? c.bounds : BoxBounds(c as BoxCollider2D);

        static bool Overlaps(Bounds a, Bounds b) =>
            a.size != Vector3.zero && a.min.x < b.max.x && b.min.x < a.max.x && a.min.y < b.max.y && b.min.y < a.max.y;

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
