using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Poisoned (buff 20, Player.UpdateLifeRegen): no regeneration, its clock held at 0, and 4 lifeRegen lost, two life a
    /// second, shown as hurt numbers. Thorns (Greenpath's and the Queen's Gardens' bushes: hazards named for thorns) poison
    /// as Terraria's poison dart trap does (projectile 98: 600 ticks, PlayerHooks.TakeHealth); the Bezoar and the Medicated
    /// Bandage keep it off (PlayerBuffs.Immune).
    /// </summary>
    internal static class Poison
    {
        public const int Buff = 20;
        /// <summary>Projectile.StatusPlayer for the poison dart (type 98): Poisoned for 600 ticks.</summary>
        public const int DartTicks = 600;
        /// <summary>lifeRegen -4: two life a second.</summary>
        const float LifePerSecond = 2f;

        static float carry, shown;
        static int sum;
        public static int DevLost, DevPoisoned;

        /// <summary>A thorn bush's or thorn wall's hazard (its object or a parent up to three named for thorns).</summary>
        public static bool IsThorn(GameObject go)
        {
            var t = go != null ? go.transform : null;
            for (int k = 0; k < 4 && t != null; k++, t = t.parent)
                if (t.name.ToLowerInvariant().Contains("thorn")) return true;
            return false;
        }

        /// <summary>A thorn's prick: Poisoned as the dart's, lengthened to 600 ticks if shorter.</summary>
        public static void Prick()
        {
            bool had = PlayerBuffs.Has(Buff);
            PlayerBuffs.Add(Buff, DartTicks);
            if (!had && PlayerBuffs.Has(Buff))
            {
                DevPoisoned++;
                Plugin.Log.LogInfo("[中毒] 被荆棘扎到，中毒 10 秒");
            }
        }

        /// <summary>Each frame in play: the life the poison takes.</summary>
        public static void Tick(HeroController hero, TerraMod mod)
        {
            if (mod == null) return;
            var stats = mod.Stats;
            stats.Poisoned = !mod.Creative && PlayerBuffs.Has(Buff);
            if (!stats.Poisoned || hero == null || stats.IsDead || PlayerData.instance == null || PlayerData.instance.isInvincible)
            {
                carry = 0f;
                return;
            }
            carry += LifePerSecond * Time.deltaTime;
            int whole = (int)carry;
            if (whole <= 0) return;
            carry -= whole;
            stats.Life = Mathf.Max(0, stats.Life - whole);
            sum += whole;
            DevLost += whole;
            if (Time.time >= shown)
            {
                shown = Time.time + 0.5f;
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, sum.ToString(), UI.CombatText.PlayerHurt);
                sum = 0;
            }
            if (stats.IsDead) hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
        }
    }
}
