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
            /// <summary>What the numbers are made from (-1: an entry from before they were kept): its touch's masks, its own health, Godhome's tier.</summary>
            public int Masks = -1, RawHp;
            public float GodhomeTier = 1f;
        }

        /// <summary>A seen enemy's numbers worked out again from what they are made from: the difficulty now, the rules now.</summary>
        static Seen Fresh(string key, Seen s)
        {
            int at = key.IndexOf('@');
            string rec = at > 0 ? key.Substring(0, at) : key;
            var info = new EnemyStats.Info { Record = rec, Scene = s.Scene, MaxHp = Mathf.Max(1, s.RawHp), Boss = s.Boss, Elite = s.Elite, Level = s.Level, GodhomeTier = s.GodhomeTier };
            if (s.Boss) info.Ref = BossStats.Of(rec, EnemyDrops.VariantLevel(s.Scene));
            return new Seen
            {
                Life = EnemyStats.LifeWhole(info), Defense = info.Defense, Level = s.Level,
                Damage = s.Masks > 0 ? s.Masks * Mathf.RoundToInt(World.Areas.DamagePerMask(s.Level) * EnemyStats.DamageFactor(info)) : 0,
                Knockback = s.Knockback, Boss = s.Boss, Elite = s.Elite, Scene = s.Scene, Masks = s.Masks, RawHp = s.RawHp, GodhomeTier = s.GodhomeTier,
            };
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
            if (Stats.TryGetValue(record, out var s)) return s.Masks >= 0 ? Fresh(record, s) : s;
            int level = EnemyDrops.StageOf(record);
            int hp = Mathf.Max(1, EnemyDrops.HpOf(record));
            string kind = EnemyDrops.KindOf(record);
            bool boss = EnemyStats.IsBossKind(record, hp);
            bool elite = !boss && EnemyStats.EliteKind(record, hp);
            float factor = boss ? World.Areas.BossDamage : elite ? World.Areas.EliteDamage : 1f;
            // A boss as its Terraria stand-in has it, where it is first fought (not a Godhome rematch), with its own measure.
            var stand = boss ? BossStats.Of(record, level) : null;
            var tune = stand != null ? BossBalance.Of(record, level) : null;
            if (tune != null) factor *= tune.Damage;
            return new Seen
            {
                Life = stand != null ? Mathf.CeilToInt(BossStats.LifeOf(stand, null, World.GameDifficulty.Current) * tune.Life * BossPhases.WholeFactor(record, null)) : Mathf.CeilToInt(hp * World.Areas.HealthScale * World.Areas.LevelHealth(level, boss)),
                Defense = stand != null ? EnemyStats.BossDefense(stand, tune) : World.Areas.Defense(level), Level = level,
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
                // A boss in several pools (the False Knight's armour and head): its whole life, not the one pool's.
                Life = EnemyStats.LifeWhole(info), Defense = info.Defense, Level = info.Level,
                Damage = info.Harmful ? masks * Mathf.RoundToInt(World.Areas.DamagePerMask(info.Boss ? info.Level : World.Areas.StatLevel(info.Level)) * (info.Boss ? 1f : World.Areas.CatchUp(info.Level)) * EnemyStats.DamageFactor(info)) : 0,
                Knockback = EnemyStats.NoKnockback(info) || TerraKnockback.Immune(hm) ? 0f : World.Areas.KnockbackTaken(info.Level),
                Boss = info.Boss, Elite = info.Elite, Scene = hm.gameObject.scene.name,
                Masks = info.Harmful ? masks : 0, RawHp = info.MaxHp, GodhomeTier = info.GodhomeTier,
            };
        }

        public static int DevRevived;

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
                // One sent out again from its pool (the Aspid Mother's hatchlings): killed before, alive and whole now, a new
                // enemy: its kill counts again (book, drops) and the damage left over from the last one is gone.
                if (counted.Contains(id))
                {
                    var info = EnemyStats.Of(hm);
                    if (info != null && !info.Boss && hm.hp >= info.MaxHp)
                    {
                        counted.Remove(id);
                        info.Carry = 0f;
                        DevRevived++;
                    }
                }
                if (looked.Contains(id)) continue;
                var col = hm.GetComponent<Collider2D>();
                if (col == null || !col.enabled) continue;
                var v = cam.WorldToViewportPoint(col.bounds.center);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                looked.Add(id);
                if (looked.Count > 4096) looked.Clear();
                string key = KeyOf(hm);
                if (key == null) continue;
                // One in the book already: its numbers as they are now (the difficulty, a newer version's).
                if (Stats.ContainsKey(key)) { Stats[key] = Snapshot(hm); continue; }
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

        /// <summary>An enemy as it goes into HealthManager.Die: out of health, and where it was.</summary>
        struct Dying
        {
            public bool OutOfHealth;
            public Vector2 At;
        }

        /// <summary>The enemy going through HealthManager.Die now (its journal flag is set within it; SpecialDeaths leaves that to this death).</summary>
        public static HealthManager DyingNow { get; private set; }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void BeforeDie(HealthManager __instance, out Dying __state)
        {
            __state = default;
            DyingNow = __instance;
            if (__instance == null || __instance.isDead) return;
            var col = __instance.GetComponent<Collider2D>();
            __state = new Dying { OutOfHealth = __instance.hp <= 0, At = col != null ? (Vector2)col.bounds.center : (Vector2)__instance.transform.position };
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void AfterDie(HealthManager __instance, Dying __state)
        {
            // A special death (Grimm, the Radiance...) never sets isDead: it is at 0 hp. A pooled enemy (the Aspid
            // Mother's hatchlings) is taken back by its pool within Die, whole again and off to its cage: it died all
            // the same, and its drops fall where it was.
            DyingNow = null;
            // The False Knight's armour goes to no health at every break (Check Health gives it back): his death is his
            // FSM's (SpecialDeaths, FalseyControl's Death Anim Start), not this.
            bool ownDeath = __instance != null && PlayMakerFSM.FindFsmOnGameObject(__instance.gameObject, "FalseyControl") != null;
            if (TerraMod.Instance != null && __instance != null && !ownDeath && (__instance.isDead || __instance.hp <= 0 || __state.OutOfHealth))
            {
                bool pooled = __state.OutOfHealth && !__instance.isDead && __instance.hp > 0;
                if (pooled) DevPooledDeaths++;
                Credit(__instance, pooled ? __state.At : (Vector2?)null);
            }
            // A journal flag the death set: counted now, if this death didn't count it.
            SpecialDeaths.AfterDeath(__instance);
        }

        public static int DevPooledDeaths;

        /// <summary>Whether this enemy's kill has been counted (its book entry, its drops).</summary>
        public static bool Credited(HealthManager hm) => hm != null && counted.Contains(hm.GetInstanceID());

        /// <summary>
        /// A kill: the book's count and first-kill note, and the drops. Once for each enemy, whichever
        /// way it went down (HealthManager.Die, or a boss's own death in its FSM: SpecialDeaths).
        /// </summary>
        /// <param name="whole">The whole boss beaten (its FSM's flag): counted whatever bodies of it are still about.</param>
        public static void Credit(HealthManager __instance, Vector2? dropAt = null, bool whole = false)
        {
            if (TerraMod.Instance == null || __instance == null) return;
            if (counted.Contains(__instance.GetInstanceID())) return;
            string rec = UI.EnemyHover.RecordOf(__instance);
            if (rec == null || rec == "Dummy" || !EnemyDrops.Known(rec)) return;
            // A boss in several bodies at once (the Oblobbles, the Mantis Lords, Oro and Mato): one kill, when the last
            // of its bodies up falls. This body isn't counted yet: the kill comes with the last one, or with the boss's
            // own record of its defeat (SpecialDeaths).
            if (!whole && EnemyDrops.IsBoss(rec) && OtherBodyUp(__instance, rec, out var other))
            {
                DevBodiesLeft++;
                Plugin.Log.LogInfo($"[图鉴] {__instance.gameObject.scene.name}/{__instance.name} 倒下，{other} 还在：等最后一个再记");
                return;
            }
            counted.Add(__instance.GetInstanceID());
            // Other bodies can receive their journal/death callbacks later. The completed fight
            // has already paid its loot; remember its dead bodies (and duplicate health components)
            // too, without marking a living opponent during an unfinished fight.
            if (EnemyDrops.IsBoss(rec))
                foreach (var body in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (body != null && body.gameObject.scene == __instance.gameObject.scene && UI.EnemyHover.RecordOf(body) == rec
                        && (whole || body.isDead || body.hp <= 0 || body.gameObject == __instance.gameObject))
                    {
                        counted.Add(body.GetInstanceID());
                        if (whole) EnemyStats.MarkDefeated(body);
                    }
            if (counted.Count > 4096) counted.Clear();
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
            // The fight being paid: its scene and its boss's name tell a variant's relic.
            Relics.ContextScene = __instance.gameObject.scene.name;
            Relics.ContextName = __instance.gameObject.name;
            if (dropAt.HasValue) EnemyDrops.Roll(record, dropAt.Value, EnemyDrops.LeavesOnItsOwn(__instance.gameObject.scene.name));
            else EnemyDrops.Roll(record, __instance);
            Relics.ContextScene = Relics.ContextName = null;
        }

        /// <summary>Dev: a boss's bodies that fell while another was still up (not counted on their own).</summary>
        public static int DevBodiesLeft;

        static bool OtherBodyUp(HealthManager hm, string rec, out string other)
        {
            other = null;
            // A body's second HealthManager (the Mantis Lords', the Soul Tyrant's) takes no hits: not a body still up.
            foreach (var o in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (o != null && o != hm && o.gameObject != hm.gameObject && o.GetComponent<HealthManager>() == o && !o.isDead && o.hp > 0
                    && o.gameObject.activeInHierarchy && !counted.Contains(o.GetInstanceID()) && !EnemyStats.Defeated(o) && UI.EnemyHover.RecordOf(o) == rec)
                {
                    other = o.name;
                    return true;
                }
            return false;
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
