using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Each Hallownest enemy's fixed drops, the way a MapleStory monster has its own: by Hunter's
    /// Journal record, a few items with a "1 in N" chance each (as Terraria's ItemDropRule.Common
    /// rolls them), rolled on every kill and dropped where it died beside its geo (straight into
    /// the inventory in a fight the game ends by taking the player away). A table holds items of
    /// every stage, from its area's Terraria biome: one of a stage the player hasn't unlocked yet
    /// doesn't drop (the monster book shows it as a question mark). Elites and bosses have sure
    /// picks: one item out of a group, among the unlocked ones (ItemDropRule.OneFromOptions). The
    /// geo an enemy drops is the table's (its Terraria counterpart's coins in proportion, scaled
    /// to Hollow Knight's own income in its area).
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
            /// <summary>Above 0: one roll for all the drops of this group, at its first's chance, that gives one of them.</summary>
            public int Group;
            /// <summary>Relative weight inside a group; legacy zero means one.</summary>
            public float Weight;
            /// <summary>Exact mean count for calibrated material pools; zero uses the legacy uniform range.</summary>
            public float MeanCount;
            /// <summary>
            /// The stage it unlocks at: above the unlocked one, it doesn't drop. An enemy's own rows from before the
            /// rework are 0 (they drop as they always have); the rest take their item's stage (a bar's, before it
            /// turns into its ore), set when the table is first read (-1 until then).
            /// </summary>
            public int LockStage;
            /// <summary>Master Mode only (ItemDropRule.MasterModeCommonDrop: a boss's relic).</summary>
            public bool Master;

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
            /// <summary>The geo it drops (-1: the game's own).</summary>
            public int Geo;
            public Drop[] Drops;
        }

        static readonly Drop[] none = new Drop[0];

        static float[] typicalHp;

        /// <summary>The usual health (the median) of a level's common enemies (the roster's), for bringing earlier areas up to a stage.</summary>
        public static float TypicalHp(int level)
        {
            if (typicalHp == null)
            {
                var by = new List<int>[World.Areas.MaxLevel + 1];
                for (int i = 0; i < by.Length; i++) by[i] = new List<int>();
                foreach (var e in table.Values)
                {
                    if (e.Boss || e.Kind != "normal" || e.Hp <= 0 || string.IsNullOrEmpty(e.Scene)) continue;
                    int l = Mathf.Clamp(World.Areas.LevelOf(e.Scene), 0, World.Areas.MaxLevel);
                    by[l].Add(e.Hp);
                }
                typicalHp = new float[by.Length];
                float last = 10f;
                for (int i = 0; i < by.Length; i++)
                {
                    by[i].Sort();
                    if (by[i].Count > 0) last = by[i][by[i].Count / 2];
                    typicalHp[i] = last;
                }
            }
            return typicalHp[Mathf.Clamp(level, 0, typicalHp.Length - 1)];
        }

        public static IReadOnlyList<Drop> Of(string record) => record != null && table.TryGetValue(record, out var e)
            ? EssentialDrops.With(record, Shop.UnlockedStage(), ProgressionLoot.Of(record,e,Shop.UnlockedStage(),Bestiary.KillsOf(record)==0,ProgressionBalance.EntryClear,ProgressionBalance.FinalClear)) : none;

        internal static IReadOnlyList<Drop> At(string record,int stage,bool first,bool entry,bool final) =>
            record != null && table.TryGetValue(record,out var e) ? EssentialDrops.With(record, stage, ProgressionLoot.Of(record,e,stage,first,entry,final)) : none;
        internal static IReadOnlyList<Drop> LegacyOf(string record) => record!=null && table.TryGetValue(record,out var e) ? e.Drops ?? none : none;
        internal static Entry EntryFor(string record) => record!=null && table.TryGetValue(record,out var e) ? e : default;

        /// <summary>The table key for a kill: "Record@stage" where that stage has its own table (Hornet in Kingdom's Edge), else the record.</summary>
        public static string KeyFor(string record, int stage)
        {
            string v = record + "@" + stage;
            return table.ContainsKey(v) ? v : record;
        }

        /// <summary>The Hall of Gods' rooms of a waking boss whose dream return has a table of its own (record@5): the waking one's.</summary>
        static readonly HashSet<string> wakingRematches = new HashSet<string> { "GG_False_Knight", "GG_Soul_Master", "GG_Broken_Vessel" };

        /// <summary>The level whose own table a kill in this scene uses (<see cref="KeyFor"/>): the scene's, but a waking boss's Godhome rematch none (its usual table).</summary>
        public static int VariantLevel(string scene) => wakingRematches.Contains(scene) ? -1 : dreamBosses.Contains(scene) ? 5 : World.Areas.LevelOf(scene);

        /// <summary>The dream bosses' rooms: fought at their own stage (World.Areas.LevelOf), their tables still "record@5".</summary>
        static readonly HashSet<string> dreamBosses = new HashSet<string>
        {
            "Dream_01_False_Knight", "Dream_02_Mage_Lord", "Dream_03_Infected_Knight", "GG_Failed_Champion", "GG_Soul_Tyrant", "GG_Lost_Kin",
        };

        public static bool Known(string record) => record != null && table.ContainsKey(record);

        /// <summary>A record of a boss (the table's Boss mark).</summary>
        public static bool IsBossRecord(string record) => record != null && table.TryGetValue(record, out var e) && e.Boss;

        /// <summary>A record of an elite: a miniboss, or tough enough (70 life or more) not to be a boss.</summary>
        public static bool IsEliteRecord(string record) => record != null && table.TryGetValue(record, out var e) && !e.Boss && (e.Kind == "miniboss" || e.Hp >= 70);

        /// <summary>A drop of a stage the player hasn't unlocked yet: it doesn't drop, and the book hides it.</summary>
        public static bool Locked(Drop d) => d.LockStage > Shop.UnlockedStage();

        /// <summary>The geo a record's kill drops on the current difficulty (-1: the game's own).</summary>
        public static int GeoOf(string key) => key != null && table.TryGetValue(key, out var e) && e.Geo >= 0 ? Mathf.RoundToInt(e.Geo * GeoScale) : -1;

        /// <summary>The bosses of the table (their records; a boss's stage variants "Record@n" are the same boss).</summary>
        public static IEnumerable<string> BossRecords()
        {
            var seen = new HashSet<string>();
            foreach (var kv in table)
            {
                if (!kv.Value.Boss) continue;
                string r = kv.Key;
                int at = r.IndexOf('@');
                if (at >= 0) r = r.Substring(0, at);
                if (seen.Add(r)) yield return r;
            }
        }

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

        /// <summary>
        /// A death's geo: the table's for its record (on the difficulty), in the game's own pieces (large 25, medium 5,
        /// small 1); an enemy the table doesn't know keeps the game's, scaled by the difficulty.
        /// </summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void MoreGeo(HealthManager __instance)
        {
            if (__instance == null || __instance.isDead || TerraMod.Instance == null) return;
            string rec = UI.EnemyHover.RecordOf(__instance);
            int geo = rec != null ? GeoOf(KeyFor(rec, VariantLevel(__instance.gameObject.scene.name))) : -1;
            if (geo >= 0)
            {
                largeGeo(__instance) = geo / 25;
                mediumGeo(__instance) = geo % 25 / 5;
                smallGeo(__instance) = geo % 5;
                DevGeo = geo;
                return;
            }
            float k = GeoScale;
            if (k <= 1f) return;
            smallGeo(__instance) = Mathf.RoundToInt(smallGeo(__instance) * k);
            mediumGeo(__instance) = Mathf.RoundToInt(mediumGeo(__instance) * k);
            largeGeo(__instance) = Mathf.RoundToInt(largeGeo(__instance) * k);
        }

        /// <summary>Dev: the geo the last kill with a table value was given.</summary>
        public static int DevGeo = -1;

        /// <summary>A drop's chance on the current difficulty.</summary>
        public static float EffectiveChance(Drop d) => d.Chance >= 1f ? 1f : Mathf.Min(1f, d.Chance * ChanceScale);

        /// <summary>How many of a drop's group are unlocked now (the book's "one of N"); 0 for a drop in no group.</summary>
        public static int GroupSize(IReadOnlyList<Drop> drops, int group)
        {
            if (group <= 0) return 0;
            int n = 0;
            foreach (var d in drops) if (d.Group == group && !Locked(d)) n++;
            return n;
        }

        /// <summary>The most a drop gives on the current difficulty (a single item stays one).</summary>
        public static int EffectiveMax(Drop d)
        {
            if(d.MeanCount>0) return Mathf.CeilToInt(d.MeanCount*(1+AmountBonus*.5f));
            var def = ItemDb.Get(d.Type);
            if (def == null || def.MaxStack <= 1 || d.Max <= 0) return d.Max;
            int span = d.Max - d.Min + 1;
            return d.Max + Mathf.FloorToInt(span * AmountBonus);
        }

        public static int EffectiveMin(Drop d) => d.MeanCount>0
            ? Mathf.FloorToInt(d.MeanCount*(1+AmountBonus*.5f)) : d.Min;

        /// <summary>Rolls a kill's drops, each on its own, and drops what came up at the enemy.</summary>
        public static void Roll(string record, HealthManager hm, bool? firstKill = null)
        {
            if (hm == null) return;
            var col = hm.GetComponent<Collider2D>();
            Roll(record, col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position, LeavesOnItsOwn(hm.gameObject.scene.name), firstKill);
        }

        /// <summary>
        /// Rolls a record's drops at a place. A fight that leaves its scene by itself (a dream boss
        /// waking the player, a Godhome fight sending them back to the hall, an ending) leaves no
        /// time to pick anything up: there the drops go straight into the inventory, and what
        /// doesn't fit falls at the player's feet once back in the world.
        /// </summary>
        public static void Roll(string record, Vector2 at, bool direct = false, bool? firstKill = null, bool fromBag = false)
        {
            bool first = firstKill ?? Bestiary.KillsOf(record)==0;
            var drops = record != null && table.TryGetValue(record,out var entry)
                ? EssentialDrops.With(record, Shop.UnlockedStage(), ProgressionLoot.Of(record,entry,Shop.UnlockedStage(),first,ProgressionBalance.EntryClear,ProgressionBalance.FinalClear)) : none;
            if (drops.Count == 0) return;
            // Terraria's boss bags (Expert, Master): the loot goes into one bag left at the boss, rolled when it is opened
            // (fromBag); the relic, the hearts, the potions and the crystals drop beside it as ever.
            var bag = !fromBag && IsBoss(record) && TreasureBags.Active ? TreasureBags.For(record, Relics.ContextScene, Relics.ContextName) : ItemType.None;
            // A boss can go down inside a wall, under the floor or high over the arena (burrowing, flying): its loot
            // comes down beside the player then, not out of sight in the ground.
            if (IsBoss(record) && !direct) at = SafeBossDropPoint(at);
            List<int> rolled = null;
            foreach (var d in drops)
            {
                if (Locked(d)) continue;
                if (d.Master && World.GameDifficulty.Current != World.DifficultyLevel.Master) continue;
                if (bag != ItemType.None && !TreasureBags.Outside(d)) continue;
                if (fromBag && TreasureBags.Outside(d)) continue;
                if (d.Group > 0)
                {
                    // A group: one roll, at its first drop's chance, for one of its unlocked drops.
                    if (rolled == null) rolled = new List<int>();
                    if (rolled.Contains(d.Group)) continue;
                    rolled.Add(d.Group);
                    if (Random.value >= EffectiveChance(d)) continue;
                    var options = new List<Drop>();
                    foreach (var o in drops) if (o.Group == d.Group && !Locked(o)) options.Add(o);
                    if (options.Count == 0) continue;
                    var pick = Pick(options);
                    Put(pick, at, direct);
                    DevGroupPicks++;
                    continue;
                }
                if (Random.value >= EffectiveChance(d)) continue;
                var relic = d;
                if (d.Master)
                {
                    // A relic: the variant's, for a fight keyed as another.
                    relic.Type = Relics.Swap(d.Type, record);
                    Relics.DevDropped++;
                }
                Put(relic, at, direct);
            }
            if (bag != ItemType.None) TreasureBags.Leave(bag, at, direct, first);
            // What a bag holds is the boss's; the kill's own extras came with the kill.
            if (fromBag) return;
            // Fishing bait and moss (FishingCrates.cs).
            Fishing.KillDrops(record, at, direct);
            // The pets: an easter egg from any enemy.
            Pets.KillDrop(record, at, direct);
        }

        /// <summary>Dev: when set, kills' drops go here (type, count) instead of into the world, for counting them.</summary>
        public static System.Action<ItemType, int> DevSink;

        internal static Drop Pick(IReadOnlyList<Drop> options)
        {
            float total=0;
            foreach(var d in options) total+=ProgressionLoot.Weight(d);
            float roll=Random.value*total;
            foreach(var d in options)
            { roll-=ProgressionLoot.Weight(d); if(roll<0) return d; }
            return options[options.Count-1];
        }

        public static float MarginalChance(IReadOnlyList<Drop> drops,Drop d)
        {
            if(d.Group<=0) return EffectiveChance(d);
            float total=0;
            foreach(var other in drops) if(other.Group==d.Group && !Locked(other)) total+=ProgressionLoot.Weight(other);
            return total>0 ? EffectiveChance(d)*ProgressionLoot.Weight(d)/total : 0;
        }

        static void Put(Drop d, Vector2 at, bool direct)
        {
            int n;
            if(d.MeanCount>0)
            {
                float mean=d.MeanCount*(1+AmountBonus*.5f);
                n=Mathf.FloorToInt(mean);
                if(Random.value<mean-n) n++;
            }
            else n = Random.Range(d.Min, EffectiveMax(d) + 1);
            if (n <= 0) return;
            if (DevSink != null) { DevSink(d.Type, n); DevDropped++; return; }
            // A boss's hearts: each its own, never into the bag.
            if (d.Type == ItemType.Heart)
            {
                BossPotions.Hearts(n, at, direct);
                DevDropped++;
                return;
            }
            var stack = ItemStack.Of(d.Type, n);
            // Item.NewItem's prefixGiven -1 for an enemy's loot (Item.Prefix(-1)): a weapon or an accessory now and then reforged.
            if (n == 1) stack.Prefix = (byte)Prefixes.RollNatural(ItemDb.Get(d.Type));
            if (direct) Give(stack);
            else WorldItems.Drop(stack, at, personal: true);
            DevDropped++;
        }

        /// <summary>Where a boss's loot comes down: where it died, unless that is inside the ground or far off (then by the player).</summary>
        static Vector2 SafeBossDropPoint(Vector2 at)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return at;
            Vector2 h = hero.GetComponent<BoxCollider2D>().bounds.center;
            bool buried = Physics2D.OverlapPoint(at, 1 << World.ChunkColliders.TerrainLayer) != null || World.TerraWorld.IsSolidAt(at);
            if (!buried && Vector2.Distance(at, h) <= 14f) return at;
            DevBossDropMoved++;
            return h + new Vector2(0f, 0.5f);
        }

        public static int DevBossDropMoved;

        /// <summary>A stack from a kill: into the world at the spot, or straight into the bag where there's no time to pick it up.</summary>
        internal static void Spill(ItemStack stack, Vector2 at, bool direct)
        {
            if (stack.IsEmpty) return;
            if (DevSink != null) { DevSink(stack.Type, stack.Count); return; }
            if (direct) Give(stack);
            else WorldItems.Drop(stack, at, personal: true);
        }

        /// <summary>Dev: sure picks made out of groups so far.</summary>
        public static int DevGroupPicks;

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
            int left = mod.Inventory.Pickup(stack);
            if (left < stack.Count)
            {
                UI.PopupText.Show(stack.Def, stack.Count - left, hero.GetComponent<BoxCollider2D>().bounds.center);
                mod.PlaySound("Grab", 0.7f);
                DevGiven += stack.Count - left;
            }
            if (left > 0)
            {
                var rest = stack;
                rest.Count = left;
                waiting.Add(rest);
                UI.CombatText.Show(hero.GetComponent<BoxCollider2D>().bounds.center + Vector3.up * 1.4f, Lang.T("背包已满：放不下的掉落物会掉在脚下"), UI.CombatText.Notice);
            }
        }

        /// <summary>Godhome's halls (the Hall of Gods, the Atrium, the pantheons' rooms between fights): the game takes no one away from them.</summary>
        static bool GodhomeHall(string scene) => scene == "GG_Workshop" || scene.StartsWith("GG_Atrium") || scene == "GG_Unlock_Wastes" || scene == "GG_Blue_Room"
            || scene.StartsWith("GG_Engine") || scene == "GG_Spa" || scene == "GG_Wyrm" || scene == "GG_Boss_Door_Entrance" || scene == "GG_Door_5_Finale";

        /// <summary>Each frame in play: what didn't fit falls at the player's feet once they're somewhere the game doesn't take them from, and their own.</summary>
        public static void Tick(HeroController hero)
        {
            ProgressionBalance.TickGifts(hero);
            if (waiting.Count == 0 || hero == null || !GameHooks.PlayerHasControl()) return;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.IsInSceneTransition || (LeavesOnItsOwn(gm.sceneName) && !GodhomeHall(gm.sceneName))) return;
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            foreach (var s in waiting) WorldItems.Drop(s, at, personal: true);
            waiting.Clear();
        }

        static Drop D(ItemType t, int den, int min = 1, int max = 1, int num = 1) => new Drop { Type = t, Num = num, Den = den, Min = min, Max = max, LockStage = -1 };

        /// <summary>One of an enemy's own rows from before the rework: never locked.</summary>
        static Drop C(ItemType t, int den, int min = 1, int max = 1, int num = 1) => new Drop { Type = t, Num = num, Den = den, Min = min, Max = max };

        /// <summary>A drop in a sure pick (group): one of the group's for certain, or at 1 in den.</summary>
        static Drop G(int group, ItemType t, int den = 1) => new Drop { Type = t, Num = 1, Den = den, Min = 1, Max = 1, Group = group, LockStage = -1 };
    }
}
