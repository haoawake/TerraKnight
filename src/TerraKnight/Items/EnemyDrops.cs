using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Each Hallownest enemy's fixed drops, the way a MapleStory monster has its own: by Hunter's
    /// Journal record, a few items with a "1 in N" chance each (as Terraria's ItemDropRule.Common
    /// rolls them), rolled on every kill and dropped where it died beside its geo (straight into
    /// the inventory in a fight the game ends by taking the player away). The deeper an enemy
    /// lives, the later the stage its drops belong to.
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal static partial class EnemyDrops
    {
        public struct Drop
        {
            public ItemType Type;
            /// <summary>One chance in Den (Num in Den when Num > 1).</summary>
            public int Num, Den;
            public int Min, Max;

            public float Chance => Mathf.Clamp01(Num / (float)Den);
        }

        /// <summary>An enemy's roster entry: English name (when the journal has none), stage, boss.</summary>
        public struct Entry
        {
            public string En, Zh, Kind;
            public int Stage;
            /// <summary>The enemy's own health (the roster's maxHp) and a scene it lives in (the monster book's background).</summary>
            public int Hp;
            public string Scene;
            public bool Boss;
            public Drop[] Drops;
        }

        static readonly Drop[] none = new Drop[0];

        public static IReadOnlyList<Drop> Of(string record) => record != null && table.TryGetValue(record, out var e) && e.Drops != null ? e.Drops : none;

        /// <summary>The table key for a kill: "Record@stage" where that stage has its own table (Hornet in Kingdom's Edge), else the record.</summary>
        public static string KeyFor(string record, int stage)
        {
            string v = record + "@" + stage;
            return table.ContainsKey(v) ? v : record;
        }

        /// <summary>The Hall of Gods' rooms of a waking boss whose dream return has a table of its own (record@5): the waking one's.</summary>
        static readonly HashSet<string> wakingRematches = new HashSet<string> { "GG_False_Knight", "GG_Soul_Master", "GG_Broken_Vessel" };

        /// <summary>The level whose own table a kill in this scene uses (<see cref="KeyFor"/>): the scene's, but a waking boss's Godhome rematch none (its usual table).</summary>
        public static int VariantLevel(string scene) => wakingRematches.Contains(scene) ? -1 : World.Areas.LevelOf(scene);

        public static bool Known(string record) => record != null && table.ContainsKey(record);

        /// <summary>Dev: every table key.</summary>
        public static IEnumerable<string> DevRecords => table.Keys;

        /// <summary>Every boss's scene (its arena) and kind (World.BossRooms).</summary>
        public static IEnumerable<(string Scene, string Kind)> BossScenes()
        {
            foreach (var e in table.Values) if (e.Boss) yield return (e.Scene, e.Kind);
        }

        public static int StageOf(string record) => record != null && table.TryGetValue(record, out var e) ? e.Stage : 0;

        public static bool IsBoss(string record) => record != null && table.TryGetValue(record, out var e) && e.Boss;

        /// <summary>The roster's kind for a record (normal, boss, miniboss, unique, ...), or null.</summary>
        public static string KindOf(string record) => record != null && table.TryGetValue(record, out var e) ? e.Kind : null;

        public static string ZhName(string record) => record != null && table.TryGetValue(record, out var e) ? e.Zh : null;

        public static int HpOf(string record) => record != null && table.TryGetValue(record, out var e) ? e.Hp : 0;

        public static string SceneOf(string record) => record != null && table.TryGetValue(record, out var e) ? e.Scene : null;

        /// <summary>The roster's name for a record (the journal's Chinese, else English) for when the journal isn't loaded.</summary>
        public static string EnglishName(string record) =>
            record != null && table.TryGetValue(record, out var e) ? (!string.IsNullOrEmpty(e.Zh) ? e.Zh : !string.IsNullOrEmpty(e.En) ? e.En : record) : record ?? "";

        /// <summary>
        /// The harder the save, the richer the kill, a little: a drop that isn't certain comes up more
        /// often (Hard 1.1 times, Expert 1.2, Master 1.3, never past certain), and a stack's count
        /// reaches higher (its spread widened upward by 15%, 30%, 50%); the geo more (GeoScale).
        /// What drops is the same item, at the same numbers.
        /// </summary>
        public static float ChanceScale => World.GameDifficulty.Current switch
        {
            World.DifficultyLevel.Hard => 1.1f,
            World.DifficultyLevel.Expert => 1.2f,
            World.DifficultyLevel.Master => 1.3f,
            _ => 1f,
        };

        public static float AmountBonus => World.GameDifficulty.Current switch
        {
            World.DifficultyLevel.Hard => 0.15f,
            World.DifficultyLevel.Expert => 0.3f,
            World.DifficultyLevel.Master => 0.5f,
            _ => 0f,
        };

        /// <summary>The geo an enemy drops (the game's own, HealthManager's geo drops) on the current difficulty: money goes up more than items.</summary>
        public static float GeoScale => World.GameDifficulty.Current switch
        {
            World.DifficultyLevel.Hard => 1.25f,
            World.DifficultyLevel.Expert => 1.5f,
            World.DifficultyLevel.Master => 2f,
            _ => 1f,
        };

        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, int> smallGeo = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("smallGeoDrops");
        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, int> mediumGeo = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("mediumGeoDrops");
        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, int> largeGeo = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("largeGeoDrops");

        /// <summary>A death's geo scaled by the difficulty (each size of piece in turn, rounded, never below what it was).</summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void MoreGeo(HealthManager __instance)
        {
            float k = GeoScale;
            if (__instance == null || __instance.isDead || k <= 1f || TerraMod.Instance == null) return;
            smallGeo(__instance) = Mathf.RoundToInt(smallGeo(__instance) * k);
            mediumGeo(__instance) = Mathf.RoundToInt(mediumGeo(__instance) * k);
            largeGeo(__instance) = Mathf.RoundToInt(largeGeo(__instance) * k);
        }

        /// <summary>A drop's chance on the current difficulty.</summary>
        public static float EffectiveChance(Drop d) => d.Chance >= 1f ? 1f : Mathf.Min(1f, d.Chance * ChanceScale);

        /// <summary>The most a drop gives on the current difficulty (a single item stays one).</summary>
        public static int EffectiveMax(Drop d)
        {
            var def = ItemDb.Get(d.Type);
            if (def == null || def.MaxStack <= 1 || d.Max <= 0) return d.Max;
            int span = d.Max - d.Min + 1;
            return d.Max + Mathf.FloorToInt(span * AmountBonus);
        }

        /// <summary>Rolls a kill's drops, each on its own, and drops what came up at the enemy.</summary>
        public static void Roll(string record, HealthManager hm)
        {
            if (hm == null) return;
            var col = hm.GetComponent<Collider2D>();
            Roll(record, col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position, LeavesOnItsOwn(hm.gameObject.scene.name));
        }

        /// <summary>
        /// Rolls a record's drops at a place. A fight that leaves its scene by itself (a dream boss
        /// waking the player, a Godhome fight sending them back to the hall, an ending) leaves no
        /// time to pick anything up: there the drops go straight into the inventory, and what
        /// doesn't fit falls at the player's feet once back in the world.
        /// </summary>
        public static void Roll(string record, Vector2 at, bool direct = false)
        {
            var drops = Of(record);
            if (drops.Count == 0) return;
            foreach (var d in drops)
            {
                if (Random.value >= EffectiveChance(d)) continue;
                int n = Random.Range(d.Min, EffectiveMax(d) + 1);
                if (n <= 0) continue;
                if (direct) Give(ItemStack.Of(d.Type, n));
                else WorldItems.Drop(ItemStack.Of(d.Type, n), at);
                DevDropped++;
            }
        }

        /// <summary>Dev: items dropped by kills so far, and the ones given straight to the inventory.</summary>
        public static int DevDropped, DevGiven;

        /// <summary>
        /// The dreams (Dream_*, the Nightmare King's Grimm_Nightmare), Godhome (GG_*, but not the
        /// Junk Pit's pipe and pit or the Pale Lurker's cliffs, which are the ordinary world) and the
        /// Hollow Knight's chamber (Room_Final_Boss_*): their fights end with the game taking the
        /// player away.
        /// </summary>
        public static bool LeavesOnItsOwn(string scene) =>
            !string.IsNullOrEmpty(scene) && (scene.StartsWith("Dream_") || scene == "Grimm_Nightmare" || scene.StartsWith("Room_Final_Boss")
                || (scene.StartsWith("GG_") && !scene.StartsWith("GG_Pipeway") && !scene.StartsWith("GG_Waterways") && !scene.StartsWith("GG_Lurker")));

        static readonly List<ItemStack> waiting = new List<ItemStack>();

        static void Give(ItemStack stack)
        {
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || hero == null)
            {
                waiting.Add(stack);
                return;
            }
            int left = mod.Inventory.Pickup(stack.Type, stack.Count);
            if (left < stack.Count)
            {
                UI.PopupText.Show(stack.Def, stack.Count - left, hero.GetComponent<BoxCollider2D>().bounds.center);
                mod.PlaySound("Grab", 0.7f);
                DevGiven += stack.Count - left;
            }
            if (left > 0) waiting.Add(ItemStack.Of(stack.Type, left));
        }

        /// <summary>Each frame in play: what didn't fit falls at the player's feet once they're back in the world and their own.</summary>
        public static void Tick(HeroController hero)
        {
            if (waiting.Count == 0 || hero == null || !GameHooks.PlayerHasControl()) return;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || LeavesOnItsOwn(gm.sceneName)) return;
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            foreach (var s in waiting) WorldItems.Drop(s, at);
            waiting.Clear();
        }

        static Drop D(ItemType t, int den, int min = 1, int max = 1, int num = 1) => new Drop { Type = t, Num = num, Den = den, Min = min, Max = max };
    }
}
