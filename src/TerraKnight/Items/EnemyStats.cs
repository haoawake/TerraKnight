using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Every enemy as a Terraria NPC: life in Terraria points (the enemy's own health times
    /// <see cref="World.Areas.HealthScale"/>), the level's defense and knockback
    /// resistance, and whether it is a boss. The game's health stays the real store, so its
    /// scripts (phase changes at set health, deaths) work as before; Terraria damage that doesn't
    /// add up to a whole point of it yet is carried over to the next hit.
    /// </summary>
    [HarmonyPatch]
    internal static class EnemyStats
    {
        internal sealed class Info
        {
            public HealthManager Hm;
            public int Level;
            /// <summary>The most health the enemy has had: the game keeps no maximum.</summary>
            public int MaxHp;
            public bool Boss;
            /// <summary>Not a boss, but one of the strong ones (Vengefly King, Soul Warrior, a Great Husk Sentry...): no stagger, no knockback, harder hits.</summary>
            public bool Elite;
            /// <summary>Hurts on touch (npc.damage > 0, for the Radar): has a DamageHero dealing damage.</summary>
            public bool Harmful;
            public string Name;
            /// <summary>Its journal record (UI.EnemyHover.RecordOf).</summary>
            public string Record;
            /// <summary>Terraria damage dealt that didn't make a whole point of the enemy's own health yet.</summary>
            public float Carry;
            /// <summary>A boss that can be broken off: Terraria damage dealt towards its next stagger (<see cref="BossBalance.StunCounts"/>).</summary>
            public float StunCarry;
            /// <summary>Time of the last Terraria hit it took.</summary>
            public float LastHit = -100f;
            /// <summary>A boss's Terraria stand-in (<see cref="BossStats"/>), or null.</summary>
            public BossStats.Ref Ref;
            public string Scene;
            /// <summary>Godhome's Ascended extra health over Attuned (1 elsewhere).</summary>
            public float GodhomeTier = 1f;
            /// <summary>A boss's life for the players there when it was first seen (NPC.GetMyBalance; 1 alone, or below Expert).</summary>
            public float Players = 1f;
            /// <summary>
            /// Terraria life per point of the enemy's own health: a boss's, its stand-in's life over its
            /// health; any other, the level's (its toughness, <see cref="World.Areas.LevelHealth"/>, included).
            /// </summary>
            public float Scale => (Ref != null
                ? BossStats.LifeOf(Ref, Scene, World.GameDifficulty.Current, GodhomeTier) * Tune.Life * BossPhases.Factor(this) / (float)Mathf.Max(1, MaxHp)
                : World.Areas.HealthScale * World.Areas.LevelHealth(Level, Boss)) * Players;
            /// <summary>Terraria defense: a boss's stand-in's (<see cref="BossDefense"/>), else the level's.</summary>
            public int Defense => Ref != null ? BossDefense(Ref, Tune) : World.Areas.Defense(Level);
            /// <summary>A boss's own measure (<see cref="BossBalance"/>): its life, defense and damage by how it fights.</summary>
            public BossBalance.Tune Tune => BossBalance.Of(Record, EnemyDrops.VariantLevel(Scene));
        }

        // By instance id: destroyed Unity objects compare equal to each other.
        static readonly Dictionary<int, Info> known = new Dictionary<int, Info>();
        static readonly System.Reflection.FieldInfo hpScaleField = AccessTools.Field(typeof(HealthManager), "hpScale");
        static System.Reflection.FieldInfo level1Field;

        /// <summary>A kind with this much health of its own counts as a boss even without a Godhome scale (the game marks bosses that way).</summary>
        const int BossHp = 300;
        /// <summary>A common kind this tough of its own counts as an elite (a Husk Guard's 70, a Hive Guardian's 100, a Great Husk Sentry's 170).</summary>
        const int EliteHp = 70;
        static readonly HashSet<string> bossKinds = new HashSet<string> { "boss", "arena_boss", "dream_boss", "warrior_dream", "godhome_boss" };

        /// <summary>
        /// Whether an enemy of this kind is one of the strong ones, by the kind itself (the roster,
        /// from the game's own data), never by what its health happens to be now: one of the
        /// journal's mini-bosses (Vengefly King, Soul Warrior, Pale Lurker), or a kind with 70 or
        /// more health of its own (Husk Guard, Hive Guardian, Stalking Devout, Mawlurk, Great Husk
        /// Sentry, Kingsmould, the Colosseum's heavy fools...). The roster's "unique" ones aren't
        /// elites for that alone (the Siblings, egg sacs, Grub Mimics are weak). A kind the roster
        /// doesn't know goes by the health it was first seen with.
        /// </summary>
        public static bool EliteKind(string record, int firstSeenHp)
        {
            string kind = EnemyDrops.KindOf(record);
            if (kind == "passive" || IsBossKind(record, firstSeenHp)) return false;
            if (kind == "miniboss") return true;
            int hp = EnemyDrops.HpOf(record);
            return (hp > 0 ? hp : firstSeenHp) >= EliteHp;
        }

        /// <summary>
        /// A boss by its kind: the roster's bosses (and dream, Godhome and Colosseum ones), or a
        /// kind with 300+ health of its own. Never one of the journal's mini-bosses, which the
        /// roster also marks as bosses: they are the elites.
        /// </summary>
        public static bool IsBossKind(string record, int firstSeenHp)
        {
            string kind = EnemyDrops.KindOf(record);
            if (kind == "miniboss") return false;
            if (EnemyDrops.IsBoss(record) || (kind != null && bossKinds.Contains(kind))) return true;
            int hp = EnemyDrops.HpOf(record);
            return (hp > 0 ? hp : firstSeenHp) >= BossHp;
        }

        public static Info Of(HealthManager hm)
        {
            if (!known.TryGetValue(hm.GetInstanceID(), out var info))
            {
                info = new Info
                {
                    Hm = hm,
                    Level = World.Areas.LevelOf(hm.gameObject.scene.name),
                    MaxHp = Mathf.Max(1, hm.hp),
                    Name = DisplayName(hm.gameObject.name),
                };
                string record = UI.EnemyHover.RecordOf(hm);
                info.Record = record;
                string kind = EnemyDrops.KindOf(record);
                info.Boss = HasBossScale(hm) || IsBossKind(record, info.MaxHp);
                info.Elite = !info.Boss && EliteKind(record, info.MaxHp);
                if (info.Boss)
                {
                    info.Players = PlayerBalance();
                    info.Scene = hm.gameObject.scene.name;
                    info.Ref = BossStats.Of(record, EnemyDrops.VariantLevel(info.Scene));
                    info.GodhomeTier = GodhomeTier(hm, info.MaxHp);
                }
                var touch = hm.GetComponentInChildren<DamageHero>(true);
                info.Harmful = kind != "passive" && touch != null && touch.damageDealt > 0;
                known[hm.GetInstanceID()] = info;
            }
            // More health later (Godhome's scaling at Start) raises its life, never its kind.
            else if (hm.hp > info.MaxHp)
            {
                info.MaxHp = hm.hp;
                // Godhome's scaling comes before the first blow; a refill later (Oro's second phase) is no tier.
                if (info.Ref != null && info.LastHit < -50f) info.GodhomeTier = GodhomeTier(hm, info.MaxHp);
            }
            return info;
        }

        /// <summary>
        /// Behind a shield only spells get past (the game's "Spell Vulnerable" ones: the shielded husks,
        /// the God Tamer's beast): Terraria's attacks get through as a spell does, so arrows and minions hurt it too.
        /// </summary>
        public static bool ShellOnlySpells(HealthManager hm) =>
            hm != null && hm.IsInvincible && hm.gameObject.CompareTag("Spell Vulnerable");

        /// <summary>NPC.CanBeChasedBy's dontTakeDamage: nothing can hurt it now (invincible all round, and not to spells either, nor a shell Terraria's attacks get through).</summary>
        public static bool Untouchable(HealthManager hm) =>
            hm.IsInvincible && hm.InvincibleFromDirection == 0 && !hm.gameObject.CompareTag("Spell Vulnerable") && !Shelled(hm);

        /// <summary>
        /// An Elder Baldur shut in its shell: its own FSM ("Blocker Control") makes it invincible all
        /// round while the player is near, and only its open mouth or a spell hurts it in the game.
        /// Terraria has no shells: its arrows, bullets, magic and minions hurt it as they would any
        /// enemy (<see cref="MeleeHits"/> lifts the shell for the hit).
        /// </summary>
        public static bool Shelled(HealthManager hm) => hm != null && hm.IsInvincible && IsElderBaldur(hm);

        static readonly Dictionary<int, bool> baldurs = new Dictionary<int, bool>();

        static bool IsElderBaldur(HealthManager hm)
        {
            int id = hm.GetInstanceID();
            if (!baldurs.TryGetValue(id, out bool yes))
            {
                foreach (var fsm in hm.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "Blocker Control") yes = true;
                baldurs[id] = yes;
            }
            return yes;
        }

        /// <summary>Bosses and elites: no stagger and no knockback (Terraria's bosses are never interrupted).</summary>
        public static bool Unstoppable(Info info) => info != null && (info.Boss || info.Elite);

        /// <summary>
        /// Takes no knockback: a boss or elite (knockBackResist 0), but not the Gorgeous Husk (the
        /// game's own nail pushes it, unlike the other uniques); nor a Bluggsac sitting on its egg
        /// or a Fungoon hanging in the air, which the game never moves either.
        /// </summary>
        public static bool NoKnockback(Info info) =>
            info != null && (fixedInPlace.Contains(info.Record) || Unstoppable(info) && !pushedElites.Contains(info.Record));

        static readonly HashSet<string> fixedInPlace = new HashSet<string> { "EggSac", "FungusFlyer" };
        static readonly HashSet<string> pushedElites = new HashSet<string> { "GorgeousHusk" };

        /// <summary>How much harder than the area's common enemies this one hits.</summary>
        public static float DamageFactor(Info info) => info == null ? 1f : info.Boss ? World.Areas.BossDamage * (info.Ref != null ? info.Tune.Damage : 1f) : info.Elite ? World.Areas.EliteDamage : 1f;

        /// <summary>Godhome gives its bosses (and only them) health per difficulty.</summary>
        static bool HasBossScale(HealthManager hm)
        {
            if (hpScaleField == null) return false;
            var scale = hpScaleField.GetValue(hm);
            if (scale == null) return false;
            if (level1Field == null) level1Field = AccessTools.Field(scale.GetType(), "level1");
            return level1Field != null && (int)level1Field.GetValue(scale) > 0;
        }

        /// <summary>Godhome's health for the boss over its Attuned health (Ascended has more), 1 without a Godhome scale.</summary>
        static float GodhomeTier(HealthManager hm, int hp)
        {
            if (hpScaleField == null || !HasBossScale(hm)) return 1f;
            int attuned = (int)level1Field.GetValue(hpScaleField.GetValue(hm));
            return attuned > 0 ? Mathf.Max(1f, hp / (float)attuned) : 1f;
        }

        static string DisplayName(string objectName)
        {
            int cut = objectName.IndexOf('(');
            return (cut > 0 ? objectName.Substring(0, cut) : objectName).Trim();
        }

        /// <summary>Life in Terraria points.</summary>
        public static int Life(HealthManager hm, Info info) =>
            hm.isDead ? 0 : Mathf.Max(0, Mathf.CeilToInt((hm.hp - info.Carry) * info.Scale));

        public static int LifeMax(Info info) => Mathf.CeilToInt(info.MaxHp * info.Scale);

        /// <summary>
        /// A boss's whole fight, every phase of it (its stand-in's life, tuned: <see cref="BossBalance"/>, a whole one a
        /// phase: <see cref="BossPhases"/>); any other enemy's, its own.
        /// </summary>
        public static int LifeWhole(Info info) => info.Ref != null
            ? Mathf.CeilToInt(BossStats.LifeOf(info.Ref, info.Scene, World.GameDifficulty.Current, info.GodhomeTier) * info.Tune.Life * info.Players * BossPhases.WholeFactor(info.Record, info.Scene))
            : LifeMax(info);

        /// <summary>A boss's phase now, all of it (its bodies together): what its staggers are paced by.</summary>
        public static int LifePhase(Info info) => info.Ref != null
            ? Mathf.CeilToInt(BossStats.LifeOf(info.Ref, info.Scene, World.GameDifficulty.Current, info.GodhomeTier) * info.Tune.Life * info.Players * BossPhases.PhaseFactor(info))
            : LifeMax(info);

        /// <summary>
        /// A boss's Terraria defense: half its stand-in's (the user's: Hallownest's bosses are met with less than the gear
        /// Terraria has by then, and fast weapons were left at a point a hit), its own measure only ever lowering it.
        /// </summary>
        public static int BossDefense(BossStats.Ref r, BossBalance.Tune t) => Mathf.RoundToInt(BossStats.DefenseOf(r) * Mathf.Min(1f, t.Defense) * 0.5f);

        /// <summary>
        /// NPC.GetStatScalingFactors for the players in the game now (the local one and every remote one), on Expert and
        /// Master (ScaleStats_ByPlayerCount runs from Expert up): 1 alone; each player more adds the boost, which starts at
        /// 0.35 and grows a third of the way to 1 each time; past 8 the balance bends ((b·2 + 8) / 3).
        /// </summary>
        public static float PlayerBalance()
        {
            if (!Net.NetSession.Active || World.GameDifficulty.Current < World.DifficultyLevel.Expert) return 1f;
            int players = 1 + Net.NetSession.Players.Count;
            float balance = 1f, boost = 0.35f;
            for (int i = 1; i < players; i++)
            {
                balance += boost;
                boost += (1f - boost) / 3f;
            }
            if (balance > 8f) balance = (balance * 2f + 8f) / 3f;
            return Mathf.Min(balance, 1000f);
        }

        /// <summary>
        /// NPC.StrikeNPC: the defense takes half its value off the hit (Main.CalculateDamageNPCsTake),
        /// a crit doubles what's left, and a hit always does at least 1.
        /// </summary>
        public static int Strike(Info info, int damage, bool crit, int armorPen = 0)
        {
            int defense = info.Defense;
            // Armour penetration (the Shark Tooth Necklace's 5, Ichor's 15): half of it, at most half the defense, back.
            float d = damage + EnemyDebuffs.ArmorBonus(info.Hm, defense, armorPen) - defense * 0.5f;
            if (crit) d *= 2f;
            return Mathf.Max(1, Mathf.RoundToInt(d));
        }

        /// <summary>Turns Terraria damage into the enemy's own health points, carrying the remainder.</summary>
        public static int ToGameDamage(Info info, int damage)
        {
            float total = info.Carry + damage / info.Scale;
            int whole = Mathf.FloorToInt(total);
            info.Carry = total - whole;
            return whole;
        }

        /// <summary>Enemies seen so far (some may be gone; check <see cref="Info.Hm"/>).</summary>
        public static IEnumerable<Info> All => known.Values;

        /// <summary>Bosses beaten by their own FSM (the False Knight's fall) whose health manager still holds life: shown and counted as dead.</summary>
        static readonly HashSet<int> defeated = new HashSet<int>();
        public static void MarkDefeated(HealthManager hm) { if (hm != null) defeated.Add(hm.GetInstanceID()); }
        public static bool Defeated(HealthManager hm) => hm != null && defeated.Contains(hm.GetInstanceID());

        /// <summary>Records the starting health, which Start sets (Godhome scales it there).</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), "Start")]
        static void AfterStart(HealthManager __instance)
        {
            if (TerraMod.Instance != null) Of(__instance);
        }

        /// <summary>Drops enemies that are gone (scene unloaded).</summary>
        public static void Prune()
        {
            if (known.Count == 0) return;
            var gone = new List<int>();
            foreach (var kv in known) if (kv.Value.Hm == null) gone.Add(kv.Key);
            foreach (var k in gone) known.Remove(k);
        }
    }
}
