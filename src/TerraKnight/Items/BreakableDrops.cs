using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hallownest's breakables as Terraria's pots: an urn, a lamp, a crate or a pole of the scene's own broken by the
    /// player (Breakable, which the nail and every Terraria weapon hit through HitTaker; BreakableObject and
    /// BreakablePoleSimple, which answer the nail's slash; BreakablePole) gives what WorldGen.SpawnThingsFromPot gives,
    /// its torches and its coins: a roll of seven (one less in Expert and Master), a 1 torches, a 0 torches too while the
    /// player carries fewer than 20, anything else money. The torches are the area's own (World.RegionalTorchAreas), as a
    /// biome's pots give the biome's torch: 2-6, 1-6 more in Expert and Master, 2-6 more for a biome torch. The money is
    /// the pot's copper: 200 ± 100, halved at the surface (the first areas), three quarters underground (stage 1-2), a
    /// quarter more in the deepest (stage 5), ±20%, the small bonus rolls, Expert's ×2.5 and its three more rolls, and
    /// 10% for each of Terraria's bosses beaten (two a stage reached), dropped as Geo (a geo a silver; the copper left
    /// over a chance of one more). A breakable drops once for good: the ones the game keeps broken (PersistentBoolItem)
    /// come back broken, and every other is remembered (by scene, name and place in its scene) in the mod's save, so
    /// one that comes back whole with the room gives nothing again. Breaking one far from the player (a scene loading
    /// beside him), the mod's own objects and the autotests' endless inventory give nothing (a creative save's breakables pay).
    /// </summary>
    [HarmonyPatch]
    internal static class BreakableDrops
    {
        /// <summary>The breakables that have given their drop, by <see cref="KeyOf"/>.</summary>
        static readonly HashSet<string> looted = new HashSet<string>();

        /// <summary>How far from the player (world units) a break still counts as his.</summary>
        const float Reach = 40f;

        /// <summary>Dev: breaks counted, drops given and the last one's description.</summary>
        public static int DevBroken, DevDropped, DevSkipped;
        public static string DevLast = "";
        /// <summary>Dev: every drop given, oldest first (scene, style, torch item and count, geo).</summary>
        public static readonly List<(string Scene, string Key, int Style, ItemType Torch, int Torches, int Geo)> DevLog = new List<(string, string, int, ItemType, int, int)>();

        public static int LootedCount => looted.Count;

        /// <summary>Dev: the next breaks' roll of seven, set (-1..6) instead of rolled; null rolls.</summary>
        public static int? DevRoll;

        /// <summary>Dev: whether the game counts a Breakable broken.</summary>
        public static bool DevIsBroken(Breakable b) => b != null && isBroken(b);

        public static List<string> Save() => new List<string>(looted);

        public static void Load(List<string> saved)
        {
            looted.Clear();
            if (saved != null) foreach (var k in saved) if (!string.IsNullOrEmpty(k)) looted.Add(k);
        }

        public static bool Looted(GameObject go) => go != null && looted.Contains(KeyOf(go));

        /// <summary>A breakable's place in the world for good: its scene, its name and where it is in its scene (a neighbour loaded beside the one played is moved).</summary>
        public static string KeyOf(GameObject go)
        {
            string scene = go.scene.name;
            Vector2 at = go.transform.position;
            var region = TerrainSystem.Find(scene);
            if (region != null) at -= (Vector2)region.Root.position;
            return scene + "|" + go.name + "|" + Mathf.RoundToInt(at.x * 10f) + "," + Mathf.RoundToInt(at.y * 10f);
        }

        // --- The game's breakables. ---

        static readonly AccessTools.FieldRef<Breakable, bool> isBroken = AccessTools.FieldRefAccess<Breakable, bool>("isBroken");
        static readonly AccessTools.FieldRef<BreakableObject, bool> objectActivated = AccessTools.FieldRefAccess<BreakableObject, bool>("activated");
        static readonly AccessTools.FieldRef<BreakablePoleSimple, bool> poleActivated = AccessTools.FieldRefAccess<BreakablePoleSimple, bool>("activated");

        [HarmonyPrefix, HarmonyPatch(typeof(Breakable), nameof(Breakable.Break))]
        static void BeforeBreak(Breakable __instance, out bool __state) => __state = isBroken(__instance);

        [HarmonyPostfix, HarmonyPatch(typeof(Breakable), nameof(Breakable.Break))]
        static void AfterBreak(Breakable __instance, bool __state)
        {
            if (!__state && isBroken(__instance)) Broken(__instance.gameObject, __instance.GetComponent<Collider2D>());
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BreakableObject), "OnTriggerEnter2D")]
        static void BeforeObject(BreakableObject __instance, out bool __state) => __state = objectActivated(__instance);

        [HarmonyPostfix, HarmonyPatch(typeof(BreakableObject), "OnTriggerEnter2D")]
        static void AfterObject(BreakableObject __instance, bool __state)
        {
            if (!__state && objectActivated(__instance)) Broken(__instance.gameObject, __instance.GetComponent<Collider2D>());
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BreakablePoleSimple), "OnTriggerEnter2D")]
        static void BeforePoleSimple(BreakablePoleSimple __instance, out bool __state) => __state = poleActivated(__instance);

        [HarmonyPostfix, HarmonyPatch(typeof(BreakablePoleSimple), "OnTriggerEnter2D")]
        static void AfterPoleSimple(BreakablePoleSimple __instance, bool __state)
        {
            if (!__state && poleActivated(__instance)) Broken(__instance.gameObject, __instance.GetComponent<Collider2D>());
        }

        /// <summary>BreakablePole breaks on a side hit and switches itself off.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(BreakablePole), nameof(BreakablePole.Hit))]
        static void BeforePole(BreakablePole __instance, out bool __state) => __state = __instance.enabled;

        [HarmonyPostfix, HarmonyPatch(typeof(BreakablePole), nameof(BreakablePole.Hit))]
        static void AfterPole(BreakablePole __instance, bool __state)
        {
            if (__state && !__instance.enabled) Broken(__instance.gameObject, __instance.GetComponent<Collider2D>());
        }

        /// <summary>One of the scene's breakables has just broken: its drop, once.</summary>
        static void Broken(GameObject go, Collider2D col)
        {
            try
            {
                DevBroken++;
                var mod = TerraMod.Instance;
                var hero = HeroController.SilentInstance;
                if (mod == null || hero == null || go == null) return;
                // The mod's own things, things kept between scenes, the autotests' endless inventory (Inventory.Infinite).
                if (go.GetComponentInParent<TerraOwned>() != null || !go.scene.IsValid() || go.scene.name == "DontDestroyOnLoad" || mod.Inventory.Infinite)
                {
                    DevSkipped++;
                    return;
                }
                Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)go.transform.position;
                if (hero.cState.transitioning || ((Vector2)hero.transform.position - at).sqrMagnitude > Reach * Reach)
                {
                    DevSkipped++;
                    return;
                }
                string key = KeyOf(go);
                if (!looted.Add(key))
                {
                    DevSkipped++;
                    DevLast = key + "：已经掉过";
                    return;
                }
                Drop(go.scene.name, key, at);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("[可破坏物] 掉落出错：" + e);
            }
        }

        static bool Expert => GameDifficulty.Current >= DifficultyLevel.Expert;

        /// <summary>What the pot gives (WorldGen.SpawnThingsFromPot's torch and coin outcomes) at a place in a scene.</summary>
        static void Drop(string scene, string key, Vector2 at)
        {
            var inv = TerraMod.Instance.Inventory;
            int style = RegionalTorchAreas.StyleOf(scene);
            int torchesHeld = 0;
            for (int i = 0; i < Inventory.Size && torchesHeld < 20; i++)
                if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.Places == CellKind.Torch) torchesHeld += inv.Slots[i].Count;
            var torch = RegionalTorches.ItemOf(style);
            var (torches, geo) = Roll(style, Areas.LevelOf(RegionalTorchAreas.Room(scene)), torchesHeld);
            if (torches > 0) WorldItems.Drop(ItemStack.Of(torch, torches), at);
            if (geo > 0) WorldItems.Drop(ItemStack.Of(ItemType.Geo, geo), at);
            DevDropped++;
            DevLast = $"{key}：{(torches > 0 ? ItemDb.Get(torch).Name + "×" + torches : "吉欧×" + geo)}";
            DevLog.Add((scene, key, style, torch, torches, geo));
            if (DevLog.Count > 200) DevLog.RemoveAt(0);
            Plugin.Log.LogInfo($"[可破坏物] {DevLast}（第 {RegionalTorches.NameOf(style)} 区）");
        }

        /// <summary>
        /// One pot's outcome for a torch style, a stage and the torches carried: its torches (how many) or its Geo, never
        /// both (the roll of seven: 1 torches, 0 torches while fewer than 20 are carried, the rest money).
        /// </summary>
        public static (int Torches, int Geo) Roll(int style, int level, int torchesHeld)
        {
            int roll = Random.Range(0, 7);
            if (Expert) roll--;
            if (DevRoll.HasValue) roll = DevRoll.Value;
            if (roll == 1 || (roll == 0 && torchesHeld < 20))
            {
                int torches = Random.Range(2, 7);
                if (Expert) torches += Random.Range(1, 7);
                // A biome's torch: more of them.
                if (style > 0) torches += Random.Range(2, 7);
                return (torches, 0);
            }
            return (0, Geo(level));
        }

        /// <summary>The pot's coins (copper) as Geo: a geo a silver, the copper left over a chance of one more.</summary>
        public static int Geo(int level)
        {
            float copper = 200 + Random.Range(-100, 101);
            // Main.worldSurface / rockLayer / the underworld: the mod's stages for depth.
            if (level <= 0) copper *= 0.5f;
            else if (level <= 2) copper *= 0.75f;
            else if (level >= 5) copper *= 1.25f;
            copper *= 1f + Random.Range(-20, 21) * 0.01f;
            if (Random.Range(0, 4) == 0) copper *= 1f + Random.Range(5, 11) * 0.01f;
            if (Random.Range(0, 8) == 0) copper *= 1f + Random.Range(10, 21) * 0.01f;
            if (Random.Range(0, 12) == 0) copper *= 1f + Random.Range(20, 41) * 0.01f;
            if (Random.Range(0, 16) == 0) copper *= 1f + Random.Range(40, 81) * 0.01f;
            if (Random.Range(0, 20) == 0) copper *= 1f + Random.Range(50, 101) * 0.01f;
            if (Expert)
            {
                copper *= 2.5f;
                if (Random.Range(0, 2) == 0) copper *= 1.25f;
                if (Random.Range(0, 3) == 0) copper *= 1.5f;
                if (Random.Range(0, 4) == 0) copper *= 1.75f;
            }
            // NPC.downedBoss1..downedFrost, 10% each: two of Terraria's bosses for each stage reached.
            copper *= Mathf.Pow(1.1f, 2 * Mathf.Clamp(Shop.UnlockedStage(), 0, Areas.MaxLevel));
            int geo = Mathf.FloorToInt(copper / 100f);
            if (Random.value < copper / 100f - geo) geo++;
            return geo;
        }
    }
}
