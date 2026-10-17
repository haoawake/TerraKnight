using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria health and mana. The game's own masks stay full in the background; every hit
    /// that would cost masks is converted to Terraria damage here, and only when Life runs out
    /// is the game allowed to kill the hero, so its death, shade and respawn flow still runs.
    /// Maximum life: Terraria's hearts (100, the Life Crystals, the Life Fruits), then the Knight's
    /// own Mask Shards beyond the first five masks, half a heart each, drawn as cyan hearts after
    /// them (UI.ResourceHud); maximum mana with mana crystals and soul vessels.
    /// </summary>
    internal sealed class TerraStats
    {
        public const int BaseLife = 100, BaseMana = 20;
        public const int MaxLifeCrystals = 15, MaxManaCrystals = 9;
        /// <summary>Terraria life per Mask Shard beyond the Knight's first five masks: half a heart.</summary>
        public const int LifePerShard = 10;

        /// <summary>The share of the maximum the Mask Shards give (the cyan hearts at the end).</summary>
        public int MaskLife;

        /// <summary>Lifeblood's silver hearts (Player.Lifeblood): over the life, taken first by a hit, gone once broken.</summary>
        public int Shield;
        /// <summary>Lifeblood's two mana stars, kept until death (past the 400 cap).</summary>
        public int TempMana;

        public int LifeMax = BaseLife;
        public int Life = BaseLife;
        public int ManaMax = BaseMana;
        public int Mana = BaseMana;
        public int Defense;
        /// <summary>Terraria lifeRegen from equipment (2 = one life a second).</summary>
        public int LifeRegenBonus;
        /// <summary>manaRegenBonus from equipment (the Mana Regeneration Band's 60).</summary>
        public int ManaRegenBonus;
        public int LifeCrystals, ManaCrystals;
        /// <summary>Life Fruits eaten (5 more maximum life each, up to 20, after the crystals).</summary>
        public int LifeFruits;
        public const int MaxLifeFruits = 20;
        /// <summary>The permanent boosters used (Player.usedAegisCrystal ... extraAccessory, ateArtisanBread).</summary>
        public readonly System.Collections.Generic.HashSet<Items.PermanentBoost> Boosts = new System.Collections.Generic.HashSet<Items.PermanentBoost>();
        /// <summary>This frame's Lifeforce (maximum life a fifth more) and Mana Regeneration (the mana as if standing still) potions.</summary>
        public bool LifeForce, ManaRegenBuff;
        /// <summary>Seconds of potion sickness left (no healing potion until 0).</summary>
        public float PotionSickness;
        /// <summary>Player.endurance: the share of each hit taken away after the defense (the Worm Scarf's 17%).</summary>
        public float Endurance;
        /// <summary>Ticks of Honey left (the Honey Comb after a hit): 1 life a second more, and the natural regeneration ramps three times as fast.</summary>
        public float HoneyTicks;
        /// <summary>The Shiny Stone worn; an item in use (no stone regeneration then); whether the stone is healing now.</summary>
        public bool ShinyStone, UsingItem, ShinyStill;
        /// <summary>The Crimson set (crimsonRegen): the clock since a hit runs twice as fast, the natural part ×1.5.</summary>
        public bool CrimsonRegen;
        /// <summary>Main.SceneMetrics.HasCampfire (here the Cozy Fire, a Campfire carried or nearby): the natural part ×1.1.</summary>
        public bool CozyFire;

        /// <summary>lifeRegenTime: ticks since the last hit (the natural regeneration's clock).</summary>
        public float SinceHitTicks => sinceHit * 60f;

        float sinceHit = 10f;
        float regenAccumulator;
        float manaDelay, manaAccumulator;

        /// <summary>Dev: the regeneration clock back to 10 s unhurt, nothing pending (two measurements start alike).</summary>
        public void DevResetRegen()
        {
            sinceHit = 10f;
            regenAccumulator = 0f;
        }

        public bool IsDead => Life <= 0;

        /// <summary>Burning (World.Meteorites): lifeRegen held at 0 or under, so no regeneration.</summary>
        public bool Burning;

        /// <summary>Recomputes the maximums from crystals, the Knight's masks and vessels, and equipment.</summary>
        public void UpdateMaximums(PlayerData pd, int manaBonus)
        {
            // The shards of the masks made beyond the first five, and of the one in the making.
            int shards = pd != null ? Mathf.Max(0, (pd.maxHealthBase - 5) * 4 + Mathf.Clamp(pd.heartPieces, 0, 3)) : 0;
            int vessels = pd != null ? Mathf.Max(0, pd.MPReserveMax / 33) : 0;
            MaskLife = shards * LifePerShard;
            int hearts = BaseLife + LifeCrystals * 20 + LifeFruits * 5;
            // Lifeforce (Pl:10343-10347): statLifeMax / 5 / 20 * 20 more.
            if (LifeForce) hearts += hearts / 5 / 20 * 20;
            LifeMax = Mathf.Min(700, hearts + MaskLife);
            ManaMax = Mathf.Min(400, BaseMana + ManaCrystals * 20 + vessels * 20 + manaBonus) + TempMana;
            if (Life > LifeMax) Life = LifeMax;
            if (Mana > ManaMax) Mana = ManaMax;
        }

        /// <summary>Applies a hit worth this many masks at the given Terraria damage per mask; returns the damage taken.</summary>
        public int TakeMasks(int masks, int perMask, float share = 1f)
        {
            // Terraria: damage minus half the defense, never below 1, then the endurance's share off. The
            // difficulty's share is taken of what is left after defense (perMask is Master's).
            int damage = Items.ArmorEffects.ModifyHurt(Endure(Mathf.Max(1, Mathf.RoundToInt((masks * perMask - Defense / 2) * share))));
            Life = Mathf.Max(0, Life - Shielded(damage));
            sinceHit = 0f;
            regenAccumulator = 0f;
            return damage;
        }

        /// <summary>Damage that isn't measured in masks (falls, explosions); defense applies as usual.</summary>
        public int TakeDirect(int raw)
        {
            int damage = Items.ArmorEffects.ModifyHurt(Endure(Mathf.Max(1, raw - Defense / 2)));
            Life = Mathf.Max(0, Life - Shielded(damage));
            sinceHit = 0f;
            regenAccumulator = 0f;
            return damage;
        }

        /// <summary>What of a hit gets past lifeblood's silver hearts (they take it first).</summary>
        int Shielded(int damage)
        {
            int held = Mathf.Min(Shield, damage);
            Shield -= held;
            return damage - held;
        }

        /// <summary>Player.Hurt: (int)((1 - endurance) * damage), at least 1.</summary>
        int Endure(int damage) => Mathf.Max(1, (int)((1f - Endurance) * damage));

        float nebulaMana;

        public int Heal(int amount)
        {
            int before = Life;
            Life = Mathf.Min(LifeMax, Life + amount);
            return Life - before;
        }

        public int RestoreMana(int amount)
        {
            int before = Mana;
            Mana = Mathf.Min(ManaMax, Mana + amount);
            return Mana - before;
        }

        /// <summary>Spends mana for a magic use; false (nothing spent) when there isn't enough.</summary>
        public bool SpendMana(int cost)
        {
            if (cost <= 0) return true;
            if (Mana < cost) return false;
            Mana -= cost;
            // Player.manaRegenDelay: regeneration waits after casting.
            manaDelay = Mathf.Max(manaDelay, 0.75f);
            manaAccumulator = 0f;
            return true;
        }

        public void FullHeal()
        {
            Life = LifeMax;
            Mana = ManaMax;
        }

        /// <summary>
        /// Natural regeneration, roughly Terraria's: nothing for a while after a hit, then a slow
        /// trickle that speeds up the longer you go unhurt, doubled while standing still, plus the
        /// equipment's lifeRegen. Mana: Terraria's (manaMax / 7 + 1) per 120 ticks rate after a
        /// short delay, faster standing still.
        /// </summary>
        public void Tick(float dt, bool standingStill)
        {
            bool honey = HoneyTicks > 0f;
            if (honey) HoneyTicks -= dt * 60f;
            sinceHit += dt * (honey ? 3f : 1f);
            // The Soul Drain (Pl:19235-19279): (5 + drained) / 2 lifeRegen, its clock running that (and 2) more a tick.
            int drained = Items.EnemyDebuffs.SoulDrainers, soul = drained > 0 ? (5 + drained) / 2 : 0;
            if (drained > 0) sinceHit += dt * (2 + soul);
            if (PotionSickness > 0f) PotionSickness = Mathf.Max(0f, PotionSickness - dt);
            // The Vital Crystal (Pl:19227-19230): lifeRegenTime 0.2 more a tick.
            if (Boosts.Contains(Items.PermanentBoost.AegisCrystal)) sinceHit += dt * 0.2f;
            // The Shiny Stone (Pl:19222-19246), standing still with nothing in use: the clock jumps to 30 s and runs five times as fast, +4 lifeRegen.
            // The Crimson set (Pl:19231-19234): lifeRegenTime one more a tick.
            if (CrimsonRegen) sinceHit += dt;
            bool shiny = ShinyStone && standingStill && !UsingItem;
            ShinyStill = shiny && Life > 0 && Life < LifeMax;
            if (shiny)
            {
                if (sinceHit > 1.5f && sinceHit < 30f) sinceHit = 30f;
                sinceHit += dt * 4f;
            }
            if (Life > 0 && Life < LifeMax && !Burning)
            {
                // Rapid Healing (Palladium) adds whatever the time since a hit (Pl:19382-19386).
                float perSecond = (LifeRegenBonus + (honey ? 2 : 0) + (shiny ? 4 : 0) + soul + Items.ArmorEffects.RegenBonus) * 0.5f;
                if (sinceHit >= 4f)
                {
                    float natural = Mathf.Lerp(0.5f, 2.5f, Mathf.InverseLerp(4f, 30f, sinceHit));
                    natural = standingStill ? natural * 2f : natural;
                    // Past 50 s still, up to 30 lifeRegen more (Pl:19331-19345); the stone's ×1.1, and Master Mode's halving of
                    // the natural part only ×0.75 with it (Pl:19363-19378).
                    if (shiny) natural += Mathf.Min(30f, Mathf.Max(0f, (sinceHit * 60f - 3000f) / 300f)) * 0.5f;
                    if (ShinyStone) natural *= 1.1f * 1.5f;
                    // The Crimson set (Pl:19359-19362): ×1.5.
                    if (CrimsonRegen) natural *= 1.5f;
                    // A Campfire nearby (UpdateLifeRegen, SceneMetrics.HasCampfire): ×1.1.
                    if (CozyFire) natural *= 1.1f;
                    perSecond += natural;
                }
                regenAccumulator += perSecond * dt;
                if (regenAccumulator >= 1f)
                {
                    int whole = Mathf.FloorToInt(regenAccumulator);
                    regenAccumulator -= whole;
                    Heal(whole);
                }
            }

            // The Nebula mana booster: a point every 6 ticks per level, before the regeneration delay (Pl:19690-19705).
            int nebula = Items.ArmorEffects.NebulaManaLevel;
            if (nebula > 0 && Mana < ManaMax)
            {
                nebulaMana += nebula * 10f * dt;
                if (nebulaMana >= 1f)
                {
                    int whole = Mathf.FloorToInt(nebulaMana);
                    nebulaMana -= whole;
                    RestoreMana(whole);
                }
            }
            bool arcane = Boosts.Contains(Items.PermanentBoost.ArcaneCrystal);
            if (manaDelay > 0f)
            {
                // Standing still or the Mana Regeneration potion halves the wait; the Arcane Crystal takes 0.05 a tick off it (Pl:19546-19556).
                manaDelay -= dt * ((standingStill || ManaRegenBuff ? 2f : 1f) + (arcane ? 0.05f : 0f));
                return;
            }
            if (Mana >= ManaMax) return;
            // UpdateManaRegen (Pl:19539): manaMax/3 + manaRegenBonus + 1 (twice standing still or with the potion), by half to whole as the
            // pool fills, at least 2; every 120 of it a point; the Arcane Crystal manaMax / 50 more.
            int regen = ManaMax / 3 + ManaRegenBonus + 1;
            if (standingStill || ManaRegenBuff) regen *= 2;
            if (arcane) regen += ManaMax / 50;
            regen = Mathf.Max(2, (int)(regen * (0.5f + 0.5f * Mana / Mathf.Max(1, ManaMax))));
            float rate = regen * 60f / 120f;
            manaAccumulator += rate * dt;
            if (manaAccumulator >= 1f)
            {
                int whole = Mathf.FloorToInt(manaAccumulator);
                manaAccumulator -= whole;
                RestoreMana(whole);
            }
        }
    }
}
