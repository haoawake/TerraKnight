using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The monster book (Terraria 1.4's bestiary, by Hunter's Journal record). An enemy first seen
    /// on screen unlocks its entry: its picture, its Terraria stats as they were when it was seen
    /// (life, contact damage, defense, knockback taken) and the journal's description, with a note
    /// at the bottom of the screen ("发现了新敌人 - 名字"). Killing it once completes the entry:
    /// what it can drop and the Hunter's notes ("已收集 名字 资料"). Every kill counts toward the
    /// record and rolls its drops, which fall where it died next to the game's own geo. Kept with
    /// the mod's save.
    /// </summary>
    [HarmonyPatch]
    internal static class Bestiary
    {
        /// <summary>Kills by journal record.</summary>
        public static readonly Dictionary<string, int> Kills = new Dictionary<string, int>();
        /// <summary>The name each record was last shown under (the journal's, kept for when it isn't loaded).</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>();
        /// <summary>Records in the order first killed.</summary>
        public static readonly List<string> Order = new List<string>();
        /// <summary>Records in the order first seen, and what was seen of each.</summary>
        public static readonly List<string> SeenOrder = new List<string>();
        public static readonly Dictionary<string, Seen> Stats = new Dictionary<string, Seen>();

        /// <summary>An enemy as Terraria would list it: life, contact damage, defense, knockback taken (0 = none), level, kind, where.</summary>
        public sealed class Seen
        {
            public int Life, Damage, Defense, Level;
            public float Knockback;
            public bool Boss, Elite;
            public string Scene;
        }

        static readonly HashSet<int> counted = new HashSet<int>();
        static readonly HashSet<int> looked = new HashSet<int>();
        static float nextScan;
        public static int DevDiscovered, DevCollected;

        public static int KillsOf(string record) => record != null && Kills.TryGetValue(record, out int n) ? n : 0;
        public static bool IsSeen(string record) => record != null && (AllUnlocked || Stats.ContainsKey(record) || Kills.ContainsKey(record));
        public static bool IsDefeated(string record) => record != null && (AllUnlocked || KillsOf(record) > 0);

        /// <summary>A creative save's book has every entry open, drops and notes too (nothing to discover, no notes at the bottom).</summary>
        public static bool AllUnlocked => TerraMod.Instance != null && TerraMod.Instance.Creative;

        /// <summary>The book's section for a record by its kind (the roster's): 0 common, 1 elite, 2 boss.</summary>
        public static int CategoryOf(string record)
        {
            int hp = Mathf.Max(1, EnemyDrops.HpOf(record));
            if (EnemyStats.IsBossKind(record, hp)) return 2;
            return EnemyStats.EliteKind(record, hp) ? 1 : 0;
        }

        /// <summary>The dreams' returns, which keep their waking self's journal record: the game's own name for each (its Hall of Gods statue's).</summary>
        static readonly Dictionary<string, (string Key, string Zh)> dreamNames = new Dictionary<string, (string, string)>
        {
            ["FalseKnight@5"] = ("NAME_FAILED_CHAMPION", "失败冠军"),
            ["MageLord@5"] = ("NAME_SOUL_TYRANT", "灵魂暴君"),
            ["InfectedKnight@5"] = ("NAME_LOST_KIN", "失落近亲"),
        };

        public static string NameOf(string record)
        {
            if (record == null) return "";
            if (dreamNames.TryGetValue(record, out var dream))
                return TeamCherry.Localization.Language.Has(dream.Key, "CP3") ? TeamCherry.Localization.Language.Get(dream.Key, "CP3") : dream.Zh;
            string live = UI.EnemyHover.NameOfRecord(record);
            if (!string.IsNullOrEmpty(live)) Names[record] = live;
            if (Names.TryGetValue(record, out var n)) return n;
            string zh = EnemyDrops.ZhName(record);
            return !string.IsNullOrEmpty(zh) ? zh : EnemyDrops.EnglishName(record);
        }

        /// <summary>What the book shows of a record: what was seen, else what the roster says (an entry from before seeing was kept).</summary>
        public static Seen StatsOf(string record)
        {
            if (record == null) return null;
            if (Stats.TryGetValue(record, out var s)) return s;
            int level = EnemyDrops.StageOf(record);
            int hp = Mathf.Max(1, EnemyDrops.HpOf(record));
            string kind = EnemyDrops.KindOf(record);
            bool boss = EnemyStats.IsBossKind(record, hp);
            bool elite = !boss && EnemyStats.EliteKind(record, hp);
            float factor = boss ? World.Areas.BossDamage : elite ? World.Areas.EliteDamage : 1f;
            // A boss as its Terraria stand-in has it, where it is first fought (not a Godhome rematch).
            var stand = boss ? BossStats.Of(record, level) : null;
            return new Seen
            {
                Life = stand != null ? BossStats.LifeOf(stand, null, World.GameDifficulty.Current) : Mathf.CeilToInt(hp * World.Areas.HealthScale * World.Areas.LevelHealth(level, boss)),
                Defense = stand != null ? BossStats.DefenseOf(stand) : World.Areas.Defense(level), Level = level,
                Damage = kind == "passive" ? 0 : Mathf.RoundToInt(World.Areas.DamagePerMask(level) * factor),
                Knockback = boss || elite ? 0f : World.Areas.KnockbackTaken(level), Boss = boss, Elite = elite, Scene = EnemyDrops.SceneOf(record),
            };
        }

        /// <summary>The book's key for a live enemy (its record, or the stage's own table), or null for what isn't in the book.</summary>
        public static string KeyOf(HealthManager hm)
        {
            string rec = UI.EnemyHover.RecordOf(hm);
            if (rec == null || rec == "Dummy" || !EnemyDrops.Known(rec)) return null;
            if (EnemyDrops.KindOf(rec) == "non_enemy") return null;
            return EnemyDrops.KeyFor(rec, EnemyDrops.VariantLevel(hm.gameObject.scene.name));
        }

        /// <summary>The Terraria stats of a live enemy (its contact damage from its DamageHero's masks).</summary>
        static Seen Snapshot(HealthManager hm)
        {
            var info = EnemyStats.Of(hm);
            int masks = 0;
            foreach (var d in hm.GetComponentsInChildren<DamageHero>(true)) masks = Mathf.Max(masks, d.damageDealt);
            if (masks <= 0 && info.Harmful) masks = 1;
            return new Seen
            {
                Life = EnemyStats.LifeMax(info), Defense = info.Defense, Level = info.Level,
                Damage = info.Harmful ? masks * Mathf.RoundToInt(World.Areas.DamagePerMask(info.Level) * EnemyStats.DamageFactor(info)) : 0,
                Knockback = EnemyStats.NoKnockback(info) || TerraKnockback.Immune(hm) ? 0f : World.Areas.KnockbackTaken(info.Level),
                Boss = info.Boss, Elite = info.Elite, Scene = hm.gameObject.scene.name,
            };
        }

        /// <summary>Four times a second: any enemy on screen not in the book yet is entered (and announced).</summary>
        public static void Tick(HeroController hero)
        {
            if (hero == null || Time.time < nextScan) return;
            nextScan = Time.time + 0.25f;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy) continue;
                int id = hm.GetInstanceID();
                if (looked.Contains(id)) continue;
                var col = hm.GetComponent<Collider2D>();
                if (col == null || !col.enabled) continue;
                var v = cam.WorldToViewportPoint(col.bounds.center);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                looked.Add(id);
                if (looked.Count > 4096) looked.Clear();
                string key = KeyOf(hm);
                if (key == null || Stats.ContainsKey(key)) continue;
                Discover(key, hm);
            }
        }

        static void Discover(string key, HealthManager hm)
        {
            bool first = !IsSeen(key);
            Stats[key] = Snapshot(hm);
            if (!SeenOrder.Contains(key)) SeenOrder.Add(key);
            NameOf(key);
            if (!first) return;
            DevDiscovered++;
            UI.BestiaryToast.Show(key, false);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void AfterDie(HealthManager __instance)
        {
            // A special death (Grimm, the Radiance...) never sets isDead: it is at 0 hp.
            if (TerraMod.Instance == null || __instance == null || !(__instance.isDead || __instance.hp <= 0)) return;
            Credit(__instance);
        }

        /// <summary>Whether this enemy's kill has been counted (its book entry, its drops).</summary>
        public static bool Credited(HealthManager hm) => hm != null && counted.Contains(hm.GetInstanceID());

        /// <summary>
        /// A kill: the book's count and first-kill note, and the drops. Once for each enemy, whichever
        /// way it went down (HealthManager.Die, or a boss's own death in its FSM: SpecialDeaths).
        /// </summary>
        public static void Credit(HealthManager __instance)
        {
            if (TerraMod.Instance == null || __instance == null) return;
            if (!counted.Add(__instance.GetInstanceID())) return;
            if (counted.Count > 4096) counted.Clear();
            string rec = UI.EnemyHover.RecordOf(__instance);
            if (rec == null || rec == "Dummy" || !EnemyDrops.Known(rec)) return;
            // The book keeps a stage's own table apart (Hornet in Greenpath and in Kingdom's Edge).
            string record = EnemyDrops.KeyFor(rec, EnemyDrops.VariantLevel(__instance.gameObject.scene.name));
            // Killed before it was ever on screen (a minion's, a shot from afar): it is seen now.
            if (!Stats.ContainsKey(record) && EnemyDrops.KindOf(rec) != "non_enemy")
            {
                Stats[record] = Snapshot(__instance);
                if (!SeenOrder.Contains(record)) SeenOrder.Add(record);
            }
            bool firstKill = !Kills.ContainsKey(record) || Kills[record] == 0;
            if (!Kills.ContainsKey(record))
            {
                Kills[record] = 0;
                Order.Add(record);
            }
            Kills[record]++;
            NameOf(record);
            if (firstKill && !AllUnlocked && EnemyDrops.KindOf(rec) != "non_enemy")
            {
                DevCollected++;
                UI.BestiaryToast.Show(record, true);
            }
            EnemyDrops.Roll(record, __instance);
        }

        public static void Clear()
        {
            Kills.Clear();
            Names.Clear();
            Order.Clear();
            SeenOrder.Clear();
            Stats.Clear();
            counted.Clear();
            looked.Clear();
            UI.BestiaryToast.Clear();
        }
    }
}
