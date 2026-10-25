using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// Target Dummies, as Terraria's (tile 378 and its NPC 488): furniture set down from the Target Dummy item (the
    /// Merchant's from the start) and knocked back into it with a pickaxe (World.Stations, StationKind.TargetDummy), and the
    /// one Dirtmouth starts with, ten widths left of Elderbug (the user's): set there once a save, the first time Dirtmouth
    /// is in (mined, it is gone for good). Every weapon hits each, the nail, shots, minions and debuffs too, with no defense
    /// and no knockback, and none ever dies. Their Terraria numbers show as any enemy's, and once a second while one is
    /// being hit, above it, its damage per second of the last three seconds; struck, it rocks on its base. Each is a
    /// HealthManager on the enemies' layer over its furniture so every hit finds it, kept in the mod's own root (not the
    /// scene's, so the respawns and LAN games never take it for one of the scene's enemies); the game's own damage path
    /// (its hit effects, which a dummy made here has no pieces for) is left out: its hits are taken off here and the
    /// health filled again each frame.
    /// </summary>
    [HarmonyPatch]
    internal static class TargetDummy
    {
        const string Scene = "Town";
        const string PlacedKey = "dummy.town.v1";
        const int Hp = 60;
        /// <summary>Its feet this far to Elderbug's left (the user's: ten of the Knight's widths).</summary>
        const float Beside = 10f;
        const float Window = 3f;

        sealed class Dummy
        {
            public GameObject Go;
            public HealthManager Hm;
            public Rect Box;
            public SpriteRenderer Look;
            public float Shake, NextDps;
            public readonly Queue<(float At, int Damage)> Hits = new Queue<(float, int)>();
        }

        static readonly List<Dummy> dummies = new List<Dummy>();
        static readonly List<(Rect Box, SpriteRenderer Renderer)> standing = new List<(Rect, SpriteRenderer)>();
        static float nextTry;
        static readonly System.Reflection.FieldInfo hpScale = AccessTools.Field(typeof(HealthManager), "hpScale");
        public static int DevHits, DevDamage;
        /// <summary>Dev: the first dummy standing (Dirtmouth's, where the tests stand by it): its feet, and its body.</summary>
        public static Vector2? DevAt => dummies.Count > 0 && dummies[0].Go != null ? (Vector2?)dummies[0].Go.transform.position : null;
        public static HealthManager DevHm => dummies.Count > 0 ? dummies[0].Hm : null;
        public static int DevCount => dummies.Count;

        public static bool Is(HealthManager h) => Of(h) != null;

        static Dummy Of(HealthManager h)
        {
            if (h == null) return null;
            foreach (var d in dummies) if (d.Hm == h) return d;
            return null;
        }

        /// <summary>Each frame in play: Dirtmouth's set down once; a body over every dummy standing, gone with it; their health filled, their rocking and their DPS lines.</summary>
        public static void Tick()
        {
            PlaceTownOne();
            Stations.StandingOf(StationKind.TargetDummy, standing);
            // The bodies whose furniture went (mined, its scene gone).
            for (int i = dummies.Count - 1; i >= 0; i--)
            {
                var d = dummies[i];
                bool still = false;
                foreach (var s in standing) if (Same(s.Box, d.Box)) { still = true; d.Look = s.Renderer; break; }
                if (still && d.Go != null) continue;
                if (d.Go != null) Object.Destroy(d.Go);
                if (d.Look != null) d.Look.transform.localRotation = Quaternion.identity;
                dummies.RemoveAt(i);
            }
            foreach (var s in standing)
            {
                bool had = false;
                foreach (var d in dummies) if (Same(s.Box, d.Box)) { had = true; break; }
                if (!had) dummies.Add(Make(s.Box, s.Renderer));
            }
            foreach (var d in dummies)
            {
                d.Hm.hp = Hp;
                d.Hm.isDead = false;
                d.Shake = Mathf.Max(0f, d.Shake - Time.deltaTime);
                if (d.Look != null)
                    d.Look.transform.localRotation = Quaternion.Euler(0f, 0f, d.Shake > 0f ? Mathf.Sin(d.Shake * 60f) * 6f * (d.Shake / 0.3f) : 0f);
                while (d.Hits.Count > 0 && Time.time - d.Hits.Peek().At > Window) d.Hits.Dequeue();
                if (d.Hits.Count > 0 && Time.time >= d.NextDps)
                {
                    d.NextDps = Time.time + 1f;
                    long sum = 0;
                    foreach (var h in d.Hits) sum += h.Damage;
                    float span = Mathf.Clamp(Time.time - d.Hits.Peek().At, 1f, Window);
                    var top = new Vector3(d.Box.center.x, d.Box.yMax + 0.4f, 0f);
                    UI.CombatText.Show(top, Lang.F("每秒伤害：{0}", Mathf.RoundToInt(sum / span)), UI.CombatText.Notice);
                    Achievements.AchievementTracker.DummyDps(Mathf.RoundToInt(sum / span));
                }
            }
        }

        static bool Same(Rect a, Rect b) => Mathf.Abs(a.x - b.x) < 0.05f && Mathf.Abs(a.y - b.y) < 0.05f;

        /// <summary>A hit's or a debuff's Terraria damage on one, for its damage per second; struck, it rocks.</summary>
        public static void Record(HealthManager h, int damage)
        {
            var d = Of(h);
            if (d == null || damage <= 0) return;
            d.Hits.Enqueue((Time.time, damage));
            DevHits++;
            Achievements.AchievementTracker.DummyHit();
            DevDamage += damage;
            d.Shake = 0.3f;
        }

        /// <summary>Its body over its furniture's box: the box's own size, standing on its bottom.</summary>
        static Dummy Make(Rect box, SpriteRenderer look)
        {
            var go = new GameObject("Target Dummy");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<TerraOwned>();
            go.layer = 11;
            go.transform.position = new Vector3(box.center.x, box.y, Effects.FxBatch.TownZ);
            var col = go.AddComponent<BoxCollider2D>();
            // Solid, as the game's enemies' bodies are (the Knight passes through the enemies' layer): the Cyclone Slash's spinning
            // hits come again on a solid body standing still, once only on a trigger.
            col.isTrigger = false;
            // A kinematic body kept awake: the hits that stay on it (the Cyclone Slash's) go on being reported as on a moving enemy.
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.useFullKinematicContacts = true;
            col.size = new Vector2(box.width, box.height);
            col.offset = new Vector2(0f, box.height / 2f);
            var hm = go.AddComponent<HealthManager>();
            if (hpScale != null && hpScale.GetValue(hm) == null) hpScale.SetValue(hm, System.Activator.CreateInstance(hpScale.FieldType));
            hm.hp = Hp;
            return new Dummy { Go = go, Hm = hm, Box = box, Look = look };
        }

        /// <summary>Dirtmouth's own, once a save: on flat ground ten widths left of Elderbug, else a little nearer or further (the host's in a LAN game).</summary>
        static void PlaceTownOne()
        {
            var mod = TerraMod.Instance;
            if (mod == null || mod.GrantedAbilities.Contains(PlacedKey) || Net.WorldSync.Guest || Time.unscaledTime < nextTry) return;
            var town = USM.GetSceneByName(Scene);
            if (!town.IsValid() || !town.isLoaded) return;
            nextTry = Time.unscaledTime + 1f;
            Transform elder = null;
            foreach (var root in town.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Elderbug")) { elder = t; break; }
                if (elder != null) break;
            }
            if (elder == null) return;
            // Flat ground two units either way (not the well's mouth).
            foreach (float dx in new[] { -Beside, -Beside + 1f, -Beside - 1f, -Beside + 2f, -Beside - 2f })
            {
                if (!Ground(elder.position.x + dx, elder.position.y, out float y)
                    || !Ground(elder.position.x + dx - 2f, elder.position.y, out float yl) || Mathf.Abs(yl - y) >= 0.3f
                    || !Ground(elder.position.x + dx + 2f, elder.position.y, out float yr) || Mathf.Abs(yr - y) >= 0.3f) continue;
                var feet = new Vector2(elder.position.x + dx, y);
                var cell = CellGrid.WorldToCell(feet + new Vector2(0f, 0.1f));
                var region = TerraWorld.RegionAt(cell.x, cell.y);
                if (region == null || region.SceneName != Scene) return;
                Stations.PlaceIn(StationKind.TargetDummy, Scene, feet - (Vector2)region.Root.position);
                mod.GrantedAbilities.Add(PlacedKey);
                Plugin.Log.LogInfo($"[训练假人] 德特茅斯的训练假人放在老虫子左边 ({feet.x:F1}, {feet.y:F1})，老虫子在 ({elder.position.x:F1}, {elder.position.y:F1})；用镐可以挖回物品");
                return;
            }
        }

        /// <summary>The ground under a point near Elderbug's height (within 2.5 units below his feet), if any.</summary>
        static bool Ground(float x, float y, out float at)
        {
            var hit = Physics2D.Raycast(new Vector2(x, y + 1.5f), Vector2.down, 4f, 1 << ChunkColliders.TerrainLayer);
            at = hit.collider != null ? hit.point.y : 0f;
            return hit.collider != null;
        }

        /// <summary>
        /// A hit on one: taken off its health here (the game's TakeDamage would spawn hit pieces a dummy made at run time
        /// lacks); its Terraria number is counted where it is dealt (MeleeHits.Deal: every hit, also one too small to take a
        /// point of health, which never comes here). Last of the hit's prefixes; the Knight's own slash, which
        /// NailItem.NailHit has dealt as the Nail item's hit, is left alone.
        /// </summary>
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        static bool Struck(HealthManager __instance, HitInstance hitInstance)
        {
            if (!Is(__instance)) return true;
            // The Knight's own slash: NailItem.NailHit has dealt it as the Nail item's hit already (that hit came here too).
            if (Items.NailItem.Converts(__instance, hitInstance)) return false;
            int d = Mathf.Max(1, Mathf.RoundToInt(hitInstance.DamageDealt * (hitInstance.Multiplier > 0f ? hitInstance.Multiplier : 1f)));
            __instance.hp -= d;
            return false;
        }
    }
}
