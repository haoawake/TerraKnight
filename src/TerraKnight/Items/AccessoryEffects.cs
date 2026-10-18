using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The accessories' own timers and what they do each 60 Hz tick or on a hit (accarmor-hm
    /// sec-G2, sec-G3): the snapping stones' charge and strikes, the Mana Cloak's star cooldown,
    /// the Bone Glove's timer, the Lucky Coin and the Gold Ring (coins are geo here), the Shiny
    /// Stone's motes, the jellyfish and ice-barrier lights, the stones' ready light, the
    /// Discount Card's price. The projectiles' part is in TerraProjectilesAcc.cs.
    /// </summary>
    internal static class AccessoryEffects
    {
        const float Px = TerraParticles.Px;

        /// <summary>accSnappingStoneCooldown: 0 = charged; 360 ticks after a strike.</summary>
        public static int SnapCooldown;
        /// <summary>starCloakCooldown: the Mana Cloak's star off a magic hit, at most once in 300 ticks.</summary>
        public static int ManaCloakCooldown;
        static int boneGloveTimer;
        /// <summary>ammoCyclingOffset and ammoCyclingCooldown (the Chaos Cylinders).</summary>
        public static int AmmoOffset, AmmoCooldown;

        /// <summary>Dev: snapping-stone strikes, coins thrown, bones thrown.</summary>
        public static int DevSnaps, DevCoins, DevBones;

        /// <summary>One 60 Hz tick (from TerraProjectiles.Tick).</summary>
        public static void Tick()
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null || hero.cState.dead) return;
            var b = Equipment.Current;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var centre = TerraParticles.ToPx(box.center);
            var fx = FxBatch.Instance;

            // Update_SnappingStone (Pl:28595-28620): the charge comes back with the stone's own sound and motes.
            if (SnapCooldown > 0 && --SnapCooldown == 0) Recharged(hero, b);
            if (SnapCooldown == 0 && fx != null)
            {
                // Pl:24763-24778: the ready glow.
                if (b.SnappingStone) fx.Light(centre, new Color(0.1f, 0.25f, 0.4f), 3f);
                if (b.Pyroclast) fx.Light(centre, new Color(0.4f, 0.25f, 0.1f), 3f);
                if (b.ArmletOfRuin) fx.Light(centre, new Color(0.2f, 0.25f, 0.6f), 3f);
            }
            if (ManaCloakCooldown > 0) ManaCloakCooldown--;
            // Update_AmmoCycler (Pl:28630-28636).
            if (AmmoCooldown > 0 && --AmmoCooldown == 0) AmmoOffset = 0;
            if (Player.TerraHurt.BrainDodgeTicks > 0) Player.TerraHurt.BrainDodgeTicks--;

            // The Bone Glove (Pl:43959-43965, 25660-25663): a bone a second while a weapon is in use.
            if (boneGloveTimer > 0) boneGloveTimer--;
            var using_ = mod.SwingItem;
            if (b.BoneGlove && boneGloveTimer == 0 && using_ != null && using_.Damage > 0)
            {
                boneGloveTimer = 60;
                TerraProjectiles.Instance?.BoneGloveShot(box.center, mod.DevAim ?? mod.AimPoint);
                DevBones++;
            }

            // The jellyfish necklaces' light (Pl:15337-15374): faint dry, bright in liquid.
            if (b.AccLight != AccLightKind.None && fx != null)
            {
                bool wet = Player.AcidLiquid.Swimming(hero);
                var c = b.AccLight == AccLightKind.Jellyfish
                    ? (wet ? new Color(1.8f, 0.4f, 1.2f) : new Color(0.225f, 0.05f, 0.15f))
                    : (wet ? new Color(0.4f, 1.2f, 1.8f) : new Color(0.05f, 0.15f, 0.225f));
                fx.Light(centre, c, 3f);
            }
            // Magiluminescence (Pl:14662-14667): warm light round the waist and a band ahead of the motion (6 ticks' travel).
            if (b.Magiluminescence && fx != null)
            {
                var light = new Color(0.9f, 0.8f, 0.5f);
                fx.Light(centre, light, 2f);
                var rb = hero.GetComponent<Rigidbody2D>();
                var v = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Player.TerraMovement.PxT : Vector2.zero;
                if (v.sqrMagnitude > 0.25f) fx.Light(centre + v * 6f, light, 2f);
            }
            // The ice barrier's light (buff 62, Pl:10549-10572).
            if (Equipment.IceBarrierNow && fx != null) fx.Light(centre, new Color(0.1f, 0.2f, 0.45f), 3f);

            // The Shiny Stone's motes (Pl:19387-19401): drawn in to the player while it heals him standing still.
            if (mod.Stats.ShinyStill && (Random.Range(0, 30000) < mod.Stats.SinceHitTicks || Random.Range(0, 30) == 0)) ShinyMote(centre);

            // The Gold Ring (Pl:35409-35415, 35305-35316): geo within 42 + 350 px pulled in fast.
            if (b.GoldRing || b.TreasureMagnet) PullGeo(box);

            // The Discount Card is worn or not as a shop opens (Pl:25831-25834).
            if (!Town.TownNpcs.ShopOpen) Town.TownNpcs.DiscountLatched = b.Discount;
        }

        static void Recharged(HeroController hero, in Bonuses b)
        {
            var mod = TerraMod.Instance;
            var topLeft = Player.TerraMovement.TopLeft(hero);
            var pp = TerraParticles.Instance;
            int type = b.SnappingStone ? 226 : b.Pyroclast ? 6 : b.ArmletOfRuin ? 15 : -1;
            if (type < 0) return;
            mod.PlaySound(b.SnappingStone ? "Item_30" : b.Pyroclast ? "Item_20" : "Item_28", b.Pyroclast ? 1f : 0.08f);
            if (pp == null) return;
            // PO:418-470: the stone's motes over the player's middle (10 electric, 15 fire, 10 sparkles).
            for (int i = 0; i < (b.Pyroclast && !b.SnappingStone ? 15 : 10); i++)
            {
                int d = pp.NewDust(topLeft, 20, 42, type, 0f, 0f, 255);
                if (d < 0) continue;
                pp[d].NoGravity = true;
                pp[d].Scale = type == 6 ? 1.5f : 0.9f;
                pp[d].FadeIn = 1.1f;
                pp[d].Position.y = topLeft.y + 21f;
                if (type == 6) pp[d].Velocity = Vector2.Scale(Random.insideUnitCircle, new Vector2(2f, 4f));
                else
                {
                    pp[d].Velocity *= 0.5f;
                    pp[d].Velocity.y *= 3f;
                }
                if (type == 15) pp[d].Alpha = 60;
            }
        }

        static void ShinyMote(Vector2 centre)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var dir = Random.insideUnitCircle.normalized;
            int d = pp.NewDust(centre - dir * 34f - new Vector2(4f, 4f), 0, 0, 55, 0f, 0f, 200, 0.5f);
            if (d < 0) return;
            pp[d].Position = centre - dir * 34f;
            pp[d].NoGravity = true;
            pp[d].FadeIn = 1.3f;
            pp[d].Velocity = dir * Random.Range(50, 100) * 0.04f;
        }

        // ------------------------------------------------------------------ enemies about

        static readonly List<Collider2D> found = new List<Collider2D>();

        /// <summary>The live enemies whose hitboxes come within a distance (Terraria px) of a point (world), each once.</summary>
        public static List<HealthManager> Enemies(Vector2 centreWorld, float radiusPx, List<HealthManager> into)
        {
            into.Clear();
            found.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapCircle(centreWorld, radiusPx * Px, filter, found);
            foreach (var c in found)
            {
                var hm = c.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || into.Contains(hm)) continue;
                EnemyStats.Of(hm);
                into.Add(hm);
            }
            return into;
        }

        static readonly List<HealthManager> near = new List<HealthManager>();

        // ------------------------------------------------------------------ the snapping stones (P:14347-14447)

        static readonly List<HealthManager> skip = new List<HealthManager>();

        /// <summary>
        /// TryMakingSnappingStoneStrike: with the stone charged, up to two more enemies the hit's
        /// source can see (120 / 300 / 400 px) take a strike (30 × ranged / 45 × melee / 150 ×
        /// summon damage, kb 5; the Pyroclastic Stone's burn them), and the struck one takes
        /// 40-60 / 70-90 / 150-170 more (fewer others, more), times the class's damage. Six
        /// seconds to recharge. Returns that bonus (0 when not charged).
        /// </summary>
        public static int SnapStrike(Vector2 fromPx, HealthManager victim, int type)
        {
            var melee = TerraMod.Instance?.Melee;
            if (SnapCooldown > 0 || victim == null || melee == null) return 0;
            var b = Equipment.Current;
            float rangedMult = 1f + b.RangedDamage, meleeMult = 1f + b.MeleeDamage, minionMult = 1f + b.MinionDamage;
            int chainDmg = type == 0 ? (int)(30 * rangedMult) : type == 1 ? (int)(45 * meleeMult) : (int)(150 * minionMult);
            float range = type == 0 ? 120f : type == 1 ? 300f : 400f;
            int bonusBase = type == 0 ? 40 : type == 1 ? 70 : 150;
            SnapCooldown = 360;
            skip.Clear();
            skip.Add(victim);
            int chained = 0;
            for (int n = 0; n < 2; n++)
            {
                var t = NearestInSight(fromPx, range);
                if (t == null) break;
                var col = t.GetComponent<Collider2D>() ?? t.GetComponentInChildren<Collider2D>();
                var tc = Centre(t);
                // The chain's projectile (1121 ranged; 1131 melee or ranged by the better crit; 1132 no class, no crit).
                float crit = type == 0 ? 4f + (b.AllCrit + b.RangedCrit) * 100f
                    : type == 1 ? 4f + (b.AllCrit + Mathf.Max(b.MeleeCrit, b.RangedCrit)) * 100f : 0f;
                if (col != null) melee.HitWith(col, chainDmg, 5f, crit, tc.x >= TerraParticles.ToWorld(fromPx).x ? 1f : -1f, tc);
                if (type == 1) EnemyDebuffs.Add(t, EnemyDebuffs.Hellfire, 180);
                var size = SizePx(t);
                var spot = Vector2.Lerp(TerraParticles.ToPx(tc) + new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y), TerraParticles.ToPx(tc), 0.6f);
                Strike(type, spot, Mathf.Min(100f, size.magnitude / 2f) * (0.3f + 1.4f * Random.value), chained + 1);
                skip.Add(t);
                chained++;
            }
            Strike(type, TerraParticles.ToPx(Centre(victim)), SizePx(victim).magnitude / 2f, 0);
            float mult = type == 0 ? rangedMult : type == 1 ? Mathf.Max(meleeMult, rangedMult) : minionMult;
            int bonus = (int)((bonusBase + 10 * (2 - chained)) * mult);
            if (type == 1) EnemyDebuffs.Add(victim, EnemyDebuffs.Hellfire, 180);
            var mod = TerraMod.Instance;
            mod.PlaySound(type == 0 ? "Item_30" : type == 1 ? "Item_20" : "Item_43", type == 1 ? 1f : 0.08f);
            DevSnaps++;
            return bonus;
        }

        static void Strike(int type, Vector2 at, float size, int index)
        {
            if (type == 0) FxParticles.SnappingStone(at, size, index);
            else if (type == 1) FxParticles.SnappingStoneMelee(at);
            else FxParticles.SnappingStoneSummon(at, index);
        }

        /// <summary>FindTargetWithLineOfSight from the hit's source: the nearest chaseable enemy by centre within range it can see, not already struck.</summary>
        static HealthManager NearestInSight(Vector2 fromPx, float range)
        {
            HealthManager best = null;
            float bestD = range;
            foreach (var hm in Enemies(TerraParticles.ToWorld(fromPx), range, near))
            {
                if (skip.Contains(hm)) continue;
                if (EnemyStats.Untouchable(hm)) continue;
                if (EnemyStats.LifeMax(EnemyStats.Of(hm)) <= 5) continue;
                var e = TerraParticles.ToPx(Centre(hm));
                float d = Vector2.Distance(e, fromPx);
                if (d >= bestD || !TerraWorld.LineClearPx(fromPx, e)) continue;
                if (Physics2D.Linecast(TerraParticles.ToWorld(fromPx), TerraParticles.ToWorld(e), 1 << ChunkColliders.TerrainLayer)) continue;
                best = hm;
                bestD = d;
            }
            return best;
        }

        static Vector2 Centre(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
        }

        static Vector2 SizePx(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            return col != null ? (Vector2)col.bounds.size / Px : new Vector2(32f, 32f);
        }

        // ------------------------------------------------------------------ coins (geo)

        static readonly AccessTools.FieldRef<HealthManager, int> smallGeo = AccessTools.FieldRefAccess<HealthManager, int>("smallGeoDrops");
        static readonly AccessTools.FieldRef<HealthManager, int> mediumGeo = AccessTools.FieldRefAccess<HealthManager, int>("mediumGeoDrops");
        static readonly AccessTools.FieldRef<HealthManager, int> largeGeo = AccessTools.FieldRefAccess<HealthManager, int>("largeGeoDrops");
        static readonly AccessTools.FieldRef<HealthManager, GameObject> smallGeoPrefab = AccessTools.FieldRefAccess<HealthManager, GameObject>("smallGeoPrefab");
        static readonly AccessTools.FieldRef<HealthManager, GameObject> largeGeoPrefab = AccessTools.FieldRefAccess<HealthManager, GameObject>("largeGeoPrefab");

        /// <summary>
        /// The Lucky Coin (Pl:44905-44918; P:13739-13752): one hit in five on an enemy worth money
        /// throws a coin out of it, copper 89%, silver 10%, gold 1%, 1-10 of them off a weapon's
        /// swing (one off a projectile). A silver coin is a geo, a gold one a hundred (four big
        /// pieces); copper goes to the purse's change.
        /// </summary>
        public static void LuckyCoin(HealthManager hm, int dir, bool itemHit)
        {
            if (hm == null || !CoinTarget(hm) || Random.Range(0, 5) != 0) return;
            int type = 0;
            if (Random.Range(0, 10) == 0) type = 1;
            if (Random.Range(0, 100) == 0) type = 2;
            int stack = Random.Range(1, 11);
            if (!itemHit) stack = 1;
            DevCoins += stack;
            if (type == 0)
            {
                Shop.Earn(stack);
                UI.CombatText.Show(hm.transform.position + Vector3.up, "+" + Shop.Money(stack), UI.CombatText.Notice);
                return;
            }
            var prefab = type == 1 ? smallGeoPrefab(hm) : largeGeoPrefab(hm);
            int pieces = type == 1 ? stack : stack * 4;
            if (prefab == null)
            {
                long value = type == 1 ? stack * 100L : stack * 10000L;
                Shop.Earn(value);
                UI.CombatText.Show(hm.transform.position + Vector3.up, "+" + Shop.Money(value), UI.CombatText.Notice);
                return;
            }
            FlingUtils.SpawnAndFling(new FlingUtils.Config
            {
                Prefab = prefab, AmountMin = pieces, AmountMax = pieces, SpeedMin = 8f, SpeedMax = 13f,
                AngleMin = dir > 0 ? 10f : 110f, AngleMax = dir > 0 ? 70f : 170f,
            }, hm.transform, Vector3.zero);
        }

        // Summoned/variant enemies can have empty native geo fields while their mod entry
        // is worth money. Arena creatures and spawned helpers worth zero remain ineligible.
        static bool CoinTarget(HealthManager hm)
        {
            if (smallGeo(hm) + mediumGeo(hm) + largeGeo(hm) > 0) return true;
            var info = EnemyStats.Of(hm);
            string record = info?.Record;
            return record != null && EnemyDrops.GeoOf(EnemyDrops.KeyFor(record, EnemyDrops.VariantLevel(hm.gameObject.scene.name))) > 0;
        }

        static readonly List<GameObject> geo = new List<GameObject>();
        static float geoScan;

        internal static float GeoGrabRange(bool goldRing, bool treasureMagnet) =>
            (42f + (goldRing ? 350f : treasureMagnet ? 150f : 0f)) * Px;

        /// <summary>Money pickup accessories attract native geo too; overlapping ranges use the larger bonus.</summary>
        static void PullGeo(Bounds body)
        {
            if (Time.time >= geoScan)
            {
                geoScan = Time.time + 0.25f;
                geo.Clear();
                foreach (var go in GameObject.FindGameObjectsWithTag("Geo")) geo.Add(go);
            }
            float reach = GeoGrabRange(Equipment.Current.GoldRing, Equipment.Current.TreasureMagnet);
            var range = new Bounds(body.center, body.size + Vector3.one * reach * 2f);
            foreach (var g in geo)
            {
                if (g == null || !g.activeInHierarchy || !range.Contains(new Vector3(g.transform.position.x, g.transform.position.y, body.center.z))) continue;
                var rb = g.GetComponent<Rigidbody2D>();
                if (rb == null) continue;
                var to = ((Vector2)body.center - (Vector2)g.transform.position).normalized * 12f * Player.TerraMovement.PxT;
                rb.linearVelocity = (rb.linearVelocity * 4f + to) / 5f;
            }
        }
    }
}
