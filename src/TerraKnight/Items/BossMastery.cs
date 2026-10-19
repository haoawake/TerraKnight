using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>Personal permanent growth, awarded once per completed, distinct boss fight.</summary>
    internal static class BossMastery
    {
        public const float PerBoss = 0.005f;
        internal const string Prefix = "boss.mastery.claimed.v1.";
        internal const string Initialized = "boss.mastery.initialized.v1";
        public static int Count
        {
            get
            {
                var mod = TerraMod.Instance;
                if (mod == null) return 0;
                Initialize(mod);
                int count = 0;
                foreach (string flag in mod.GrantedAbilities)
                    if (flag.StartsWith(Prefix, System.StringComparison.Ordinal)
                        && System.Enum.TryParse(flag.Substring(Prefix.Length), out ItemType relic) && Relics.Is(relic)) count++;
                return count;
            }
        }
        internal static void Initialize(TerraMod mod)
        {
            if (mod == null || mod.GrantedAbilities.Contains(Initialized)) return;
            foreach (var kill in Bestiary.Kills)
                if (kill.Value > 0 && EnemyDrops.IsBoss(kill.Key))
                {
                    var relic = Relics.Of(kill.Key, null, null);
                    if (relic.HasValue) mod.GrantedAbilities.Add(Prefix + relic.Value);
                }
            mod.GrantedAbilities.Add(Initialized);
        }
        internal static bool Record(string key, string scene, string bossName)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !EnemyDrops.IsBoss(key)) return false;
            Initialize(mod);
            var relic = Relics.Of(key, scene, bossName);
            // Reuse the complete 48-fight identity map, including the four variants which
            // share journal/drop keys. This does not depend on actually dropping a relic.
            return relic.HasValue && mod.GrantedAbilities.Add(Prefix + relic.Value);
        }
        public static int CountFrom(IEnumerable<KeyValuePair<string, int>> kills)
        {
            var distinct = new HashSet<ItemType>();
            foreach (var kill in kills)
                if (kill.Value > 0 && EnemyDrops.IsBoss(kill.Key))
                {
                    var relic = Relics.Of(kill.Key, null, null);
                    if (relic.HasValue) distinct.Add(relic.Value);
                }
            return distinct.Count;
        }
        public static float Bonus => Count * PerBoss;
        public static void Apply(ref Bonuses bonuses)
        {
            float bonus = Bonus;
            bonuses.Endurance += bonus;
            bonuses.MeleeDamage += bonus;
            bonuses.RangedDamage += bonus;
            bonuses.MagicDamage += bonus;
            bonuses.MinionDamage += bonus;
        }
    }
}
