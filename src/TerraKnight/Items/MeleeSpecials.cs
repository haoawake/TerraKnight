using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// What the plain swung items do beyond their blow (sweep S1.A; Player.ProcessHitAgainstNPC,
    /// StatusToNPC, ApplyNPCOnHitEffects, ApplyRapidAttackBonus, 1.4.5.8): the scimitars' rapid
    /// attack bonus, the Breaker Blade on enemies near full life, the Ham Bat by how well fed, the
    /// Striking Moment after a parry; the Bone Sword's bones, the Bladetongue's ichor, the Tentacle
    /// Spike's spikes and the Blood Butcherer's cuts (once a swing), the Bat Bat's sip of life, the
    /// Tax Collector's coins, the Slap Hand's slap; the Psycho Knife's stealth, the Brand of the
    /// Inferno's raised shield and parry, and the umbrellas' slow fall.
    /// </summary>
    [HarmonyPatch]
    internal static class MeleeSpecials
    {
        /// <summary>Striking Moment (198: the next melee hit ×5) and Hearty Meal (336: +6 lifeRegen) from a Ham Bat kill.</summary>
        public const int StrikingMoment = 198, HeartyMeal = 336;

        // _spawnTentacleSpikes, _spawnBloodButcherer, _batbatCanHeal: once a swing (Pl:43795-43806).
        static bool spikeReady, butcherReady, batReady;
        /// <summary>Player.rapidAttackBonus: 0.12 a hit up to 0.62, at most 0.5 counted; 0.005 a tick lost (Pl:18365-18399).</summary>
        static float rapidBonus;

        /// <summary>Player.stealth while the Psycho Knife is held (1 seen .. 0 hidden), and its stealthTimer.</summary>
        public static float Stealth = 1f;
        static int stealthTimer;

        /// <summary>Player.shieldRaised (Brand of the Inferno with a shield worn, the right button held), shieldParryTimeLeft, shield_parry_cooldown.</summary>
        public static bool ShieldRaised { get; private set; }
        public static int ParryLeft { get; private set; }
        static int parryCooldown;

        /// <summary>Dev: counts for the autotest.</summary>
        public static int DevParries, DevCoins, DevBatHeals, DevLastBase, DevLastModified;
        public static readonly System.Collections.Generic.Dictionary<ItemType, int> DevHits = new System.Collections.Generic.Dictionary<ItemType, int>();
        public static float DevRapid => rapidBonus;
        /// <summary>Dev: hold the shield up without the mouse.</summary>
        public static bool DevShield;

        static ItemDef Held
        {
            get
            {
                var mod = TerraMod.Instance;
                return mod == null ? null : mod.Inventory.Current.Def;
            }
        }

        public static bool PsychoHeld => Held != null && Held.Type == ItemType.PsychoKnife;

        /// <summary>The Sergeant United Shield in hand with none of its own out (ownedProjectileCounts[866] &lt; 1).</summary>
        public static bool BouncingShieldInHand => Held != null && Held.Type == ItemType.BouncingShield && TerraProjectiles.Instance != null
                                                   && TerraProjectiles.Instance.DevCount(ProjKind.BouncingShieldProj) == 0;
        static bool UmbrellaHeld => Held != null && (Held.Type == ItemType.Umbrella || Held.Type == ItemType.TragicUmbrella);

        /// <summary>ItemID.Sets.RapidAttackBonusDamage: the Falcon Blade, the Cutlass, the Exotic Scimitar.</summary>
        static bool Rapid(ItemType t) => t == ItemType.FalconBlade || t == ItemType.Cutlass || t == ItemType.DyeTradersScimitar;

        /// <summary>The middle of the swing's box (Terraria px): ProcessHitAgainstNPC's itemRectangle.Center, where the Keybrand aims its sparkles.</summary>
        public static Vector2 SwingPx;

        /// <summary>A use starts (ItemAnimationJustStarted).</summary>
        public static void OnSwingStart(ItemDef w)
        {
            spikeReady = w.Type == ItemType.TentacleSpike;
            butcherReady = w.Type == ItemType.BloodButcherer;
            batReady = w.Type == ItemType.BatBat;
        }

        /// <summary>
        /// ProcessHitAgainstNPC's damage (Pl:44643-44680), before the Pyroclast's strike and the
        /// variation: the rapid attack bonus (rising with every hit), Striking Moment ×5 on a melee
        /// item, the Breaker Blade ×2.5 on an enemy with 90% of its life or more, the Ham Bat +5% a
        /// tier of Well Fed.
        /// </summary>
        public static int ModifyDamage(ItemDef w, HealthManager hm, int damage)
        {
            if (hm == null) return damage;
            DevLastBase = damage;
            DevLastModified = Modify(w, hm, damage);
            return DevLastModified;
        }

        static int Modify(ItemDef w, HealthManager hm, int damage)
        {
            if (Rapid(w.Type))
            {
                float counted = Mathf.Min(rapidBonus, 0.5f);
                rapidBonus = Mathf.Min(rapidBonus + 0.12f, 0.62f);
                damage = (int)(damage * (1f + counted));
            }
            if (w.Class == DamageClass.Melee && PlayerBuffs.Has(StrikingMoment))
            {
                damage *= 5;
                PlayerBuffs.Remove(StrikingMoment);
            }
            var info = EnemyStats.Of(hm);
            if (w.Type == ItemType.BreakerBlade && info != null && EnemyStats.Life(hm, info) >= EnemyStats.LifeMax(info) * 0.9f) damage = (int)(damage * 2.5f);
            if (w.Type == ItemType.HamBat)
            {
                int tier = PlayerBuffs.Has(207) ? 3 : PlayerBuffs.Has(206) ? 2 : PlayerBuffs.Has(26) ? 1 : 0;
                damage = (int)(damage * (1f + 0.05f * tier));
            }
            // NPC.KeyBrandStrike (Pl:44686-44689): up to twice as hard on a badly hurt enemy.
            if (w.Type == ItemType.Keybrand) damage = TerraProjectiles.KeyBrandStrike(hm, damage, SwingPx);
            return damage;
        }

        /// <summary>The Psycho Knife's knockback: more the more hidden (Pl:44011-44014).</summary>
        public static float Knockback(ItemDef w, float kb) => w.Type == ItemType.PsychoKnife ? kb + kb * (1f - Stealth) : kb;

        /// <summary>
        /// After a blow that landed: the Slap Hand's slap (Item_175), the Tax Collector's copper and
        /// classy dust, the Bone Sword's bone at 75% of the item's damage, StatusToNPC (Confused one
        /// time in two for the Purple Clubberfish, Ichor for the Bladetongue, Hellfire one in four
        /// for the Brand of the Inferno), then ApplyNPCOnHitEffects: the Bladetongue's stream at
        /// half the blow, the Psycho Knife's stealth broken, the spike, the cut, the Bat Bat's 1 life.
        /// </summary>
        public static void OnHit(ItemDef w, HealthManager hm, Collider2D col, int baseDamage, int damage, float kb, float crit, float dir, Rect swing)
        {
            DevHits[w.Type] = (DevHits.TryGetValue(w.Type, out int had) ? had : 0) + 1;
            var tp = TerraProjectiles.Instance;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            var info = EnemyStats.Of(hm);
            Vector2 npcPx = TerraParticles.ToPx(col.bounds.center);
            var rectTl = TerraParticles.ToPx(new Vector2(swing.xMin, swing.yMax));
            float rw = swing.width / TerraParticles.Px, rh = swing.height / TerraParticles.Px;
            switch (w.Type)
            {
                case ItemType.SlapHand:
                    // ParticleOrchestrator.Spawn_SlapHand: only its sound.
                    mod?.PlaySound("Item_175");
                    break;
                case ItemType.WaffleIron:
                    // Spawn_WaffleIron: its sound.
                    mod?.PlaySound("Item_178");
                    break;
                case ItemType.TaxCollectorsStickOfDoom:
                    if (info != null && EnemyStats.LifeMax(info) > 5)
                    {
                        // A copper coin flung off the enemy (1 + RollBadLuck(25): no bad luck here); Spawn_ClassyCane's seven gold motes (10).
                        Shop.Earn(1);
                        DevCoins++;
                        var tl = npcPx - new Vector2(10f, 10f);
                        for (int k = 0; k < 7; k++)
                        {
                            int i = TerraParticles.Instance != null ? TerraParticles.Instance.NewDust(tl, 20, 20, 10, 0f, -1f, 80, 1.1f) : -1;
                            if (i >= 0 && Random.Range(0, 3) != 0) TerraParticles.Instance[i].NoGravity = true;
                        }
                        // ...and now and then the gore it spins off (Gore 1218).
                        if (Random.Range(0, 2) == 1)
                            TerraParticles.Instance?.NewGore(tl + new Vector2(Random.Range(0, 20), Random.Range(0, 20)), new Vector2(Random.Range(-10, 11) * 0.4f, -(3f + Random.Range(0, 6) * 0.3f)), 1218, 1f, 1f, default, true, falls: true);
                    }
                    break;
                // Pl:44746-44752: the Bone Sword's and the Bone Pickaxe's bone.
                case ItemType.BoneSword:
                case ItemType.BonePickaxe:
                    if (tp != null)
                    {
                        var at = rectTl + new Vector2(Random.Range(0f, rw), Random.Range(0f, rh / 2f));
                        tp.MeleeShot(ProjKind.BoneSwordBone, at, new Vector2(dir * (1f + Random.value * 5f), -1f - Random.value * 3f), (int)(baseDamage * 0.75f), kb, crit);
                    }
                    break;
            }
            // StatusToNPC.
            if (w.Type == ItemType.PurpleClubberfish && Random.Range(0, 2) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 60 * Random.Range(1, 4));
            if (w.Type == ItemType.Bladetongue) EnemyDebuffs.Add(hm, EnemyDebuffs.Ichor, 60 * Random.Range(5, 10));
            if (w.Type == ItemType.DD2SquireDemonSword && Random.Range(0, 4) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 300);
            if (w.Type == ItemType.WaffleIron && Random.Range(0, 3) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 300);
            if (w.Type == ItemType.StylistKilLaKillScissorsIWish) EnemyDebuffs.Add(hm, EnemyDebuffs.Bleeding, 60 * Random.Range(4, 7));
            // ApplyNPCOnHitEffects.
            if (w.Type == ItemType.Bladetongue && tp != null)
            {
                var v = new Vector2(dir * 100f + Random.Range(-25, 26), Random.Range(-75, 76)).normalized * (Random.Range(30, 41) * 0.1f);
                var from = (rectTl + new Vector2(Random.Range(0f, rw), Random.Range(0f, rh)) + npcPx * 2f) / 3f;
                tp.MeleeShot(ProjKind.IchorSplash, from, v, (int)(damage * 0.5f), kb * 0.7f, crit);
            }
            if (w.Type == ItemType.PsychoKnife) Stealth = 1f;
            Vector2 playerPx = hero != null ? TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center) : npcPx;
            bool chaseable = info != null && EnemyStats.LifeMax(info) > 5 && !hm.isDead;
            if (spikeReady && w.Type == ItemType.TentacleSpike && chaseable && tp != null)
            {
                tp.StickInto(ProjKind.TentacleSpikeStuck, hm, playerPx, damage, kb);
                // StatusNPC: the spike's own debuff while it holds (P:11349).
                EnemyDebuffs.Add(hm, EnemyDebuffs.TentacleSpiked, 540);
                spikeReady = false;
            }
            if (butcherReady && w.Type == ItemType.BloodButcherer && chaseable && tp != null)
            {
                tp.StickInto(ProjKind.BloodButchererStuck, hm, playerPx, damage, kb);
                EnemyDebuffs.Add(hm, EnemyDebuffs.BloodButchered, 540);
                butcherReady = false;
            }
            if (batReady && w.Type == ItemType.BatBat && mod != null && mod.Stats.Life < mod.Stats.LifeMax)
            {
                batReady = false;
                int healed = mod.Stats.Heal(1);
                DevBatHeals++;
                if (hero != null) CombatText.Show(hero.transform.position + Vector3.up * 0.9f, healed.ToString(), CombatText.Heal);
            }
        }

        /// <summary>OnKillNPC (Pl:20625-20628): a Ham Bat kill feeds the player, Hearty Meal for 7 s.</summary>
        public static void OnKill(ItemDef w)
        {
            if (w.Type == ItemType.HamBat) PlayerBuffs.Add(HeartyMeal, 420);
        }

        /// <summary>What these items add to the player's stats this frame.</summary>
        public static Bonuses DynamicBonuses()
        {
            var b = new Bonuses();
            // Pl:25928-25976: hidden, up to +300% melee damage and +30 melee crit.
            if (PsychoHeld)
            {
                b.MeleeDamage = (1f - Stealth) * 3f;
                b.MeleeCrit = (int)((1f - Stealth) * 30f) / 100f;
            }
            // Pl:25909-25912: +20 defense behind the shield.
            if (ShieldRaised) b.Defense = 20;
            // Pl:19281-19285: Hearty Meal's 3 HP a second.
            if (PlayerBuffs.Has(HeartyMeal)) b.LifeRegen = 6;
            return b;
        }

        /// <summary>
        /// Each tick: the rapid bonus fades; the Psycho Knife's stealth (Pl:25928-25976: back up
        /// 0.1 a tick in use or on the move but only while not fully hidden, down 0.02 standing
        /// still once the timer has run out); the shield (ItemCheck_ManageRightClickFeatures_ShieldRaise).
        /// </summary>
        public static void Tick(HeroController hero, bool usingItem, bool rightHeld, bool rightPressed)
        {
            if (rapidBonus > 0f) rapidBonus = Mathf.Max(0f, rapidBonus - 0.005f);
            if (PsychoHeld)
            {
                var rb = hero.GetComponent<Rigidbody2D>();
                var v = rb != null ? rb.linearVelocity / TerraMovement.PxT : Vector2.zero;
                if (usingItem)
                {
                    stealthTimer = 15;
                    if (Stealth > 0f) Stealth += 0.1f;
                }
                else if (Mathf.Abs(v.x) < 0.1f && Mathf.Abs(v.y) < 0.1f)
                {
                    if (stealthTimer == 0 && Stealth > 0f) Stealth = Mathf.Max(0f, Stealth - 0.02f);
                }
                else if (Stealth > 0f) Stealth += 0.1f;
                Stealth = Mathf.Min(1f, Stealth);
                if (stealthTimer > 0) stealthTimer--;
            }
            else
            {
                Stealth = 1f;
                stealthTimer = 0;
            }
            ShieldTick(usingItem, rightHeld || DevShield, rightPressed);
        }

        /// <summary>A right press since the last tick (the shield's fresh press).</summary>
        public static bool RightLatch;

        /// <summary>The swing's itemAnimation and itemAnimationMax this tick (set before SwingDust).</summary>
        public static int SwingAnim, SwingAnimMax;
        static int lastHammush = -1;
        public static int DevHammushShrooms;

        /// <summary>
        /// ItemCheck_EmitHammushProjectiles (Pl:44921-44987): at 90, 70, 50, 30 and 10% of the swing
        /// left, a glowing mushroom (131) from the blade's box, thrown up and then ahead in turn, at
        /// half the damage and no knockback.
        /// </summary>
        static void HammushMushrooms(ItemDef w, Rect swing, float dir)
        {
            int max = SwingAnimMax, now = SwingAnim;
            int a9 = (int)(max * 0.9), a7 = (int)(max * 0.7), a5 = (int)(max * 0.5), a3 = (int)(max * 0.3), a1 = (int)(max * 0.1);
            // itemAnimation counts down a tick at a time; here a frame's timing can skip a count, so every mark passed since last time goes off.
            if (lastHammush < 0 || now > lastHammush) lastHammush = max + 1;
            int from = lastHammush;
            lastHammush = now;
            foreach (int anim in new[] { a9, a7, a5, a3, a1 })
                if (anim >= now && anim < from) HammushMushroom(w, swing, dir, anim, a9, a7, a5, a3, a1);
        }

        static void HammushMushroom(ItemDef w, Rect swing, float dir, int anim, int a9, int a7, int a5, int a3, int a1)
        {
            float vy = 0f, vx = 0f, oy = 0f, ox = 0f;
            if (anim == a9) vy = -7f;
            if (anim == a7) { vy = -6f; vx = 2f; }
            if (anim == a5) { vy = -4f; vx = 4f; }
            if (anim == a3) { vy = -2f; vx = 6f; }
            if (anim == a1) vx = 7f;
            if (anim == a7) ox = 26f;
            if (anim == a3) { ox -= 4f; oy -= 20f; }
            if (anim == a1) oy += 6f;
            if (dir < 0f)
            {
                if (anim == a9) ox -= 8f;
                if (anim == a7) ox -= 6f;
            }
            vy *= 1.5f;
            vx *= 1.5f;
            ox *= dir;
            var centre = TerraParticles.ToPx(swing.center);
            TerraProjectiles.Instance?.MeleeShot(ProjKind.SpearMushroom, centre + new Vector2(ox, oy), new Vector2(dir * vx, vy), Equipment.Damage(w, null) / 2, 0f, Equipment.Crit(w));
            DevHammushShrooms++;
        }

        /// <summary>One 60 Hz tick (from TerraProjectiles.Tick).</summary>
        public static void TickNow()
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool right = !TerraMod.DevNoRealMouse && Player.TerraKeys.Held(Player.TerraKeys.Action.Interact) && !InventoryGui.MouseOverUi && GameHooks.PlayerHasControl();
            Tick(hero, mod.IsUsingItem, right, RightLatch);
            RightLatch = false;
        }

        static bool RaisableShield()
        {
            var mod = TerraMod.Instance;
            if (mod == null) return false;
            foreach (var a in mod.Inventory.EffectiveAccessories)
                if (!a.IsEmpty && a.Def.Shield > 0) return true;
            return false;
        }

        static void ShieldTick(bool usingItem, bool right, bool pressed)
        {
            // Up while the right button is held, between swings or on a fresh press (which ends the swing).
            // The Brand of the Inferno with a shield worn, or the Sergeant United Shield while it isn't thrown (Pl:13173-13176, 31951-31956).
            bool guard = Held != null && ((Held.Type == ItemType.DD2SquireDemonSword && RaisableShield()) || BouncingShieldInHand) && right && (!usingItem || pressed);
            if (parryCooldown > 0 && --parryCooldown == 0)
            {
                // The parry is ready again: Item_25's chime and ten orange motes before the player.
                TerraMod.Instance?.PlaySound("Item_25");
                var hero = HeroController.SilentInstance;
                if (hero != null && TerraParticles.Instance != null)
                {
                    var tl = TerraMovement.TopLeft(hero);
                    int dir = hero.cState.facingRight ? 1 : -1;
                    var at = tl + new Vector2(10f + dir * 6 + (dir == -1 ? -10 : 0), 21f - 14f);
                    for (int k = 0; k < 10; k++)
                    {
                        int i = TerraParticles.Instance.NewDust(at, 10, 16, 45, 0f, 0f, 255, Random.Range(10, 16) * 0.1f);
                        if (i < 0) continue;
                        ref var d = ref TerraParticles.Instance[i];
                        d.Color = FxBatch.C(255, 100, 0, 127);
                        d.NoGravity = true;
                        d.Velocity *= 0.5f;
                    }
                }
            }
            if (ParryLeft > 0 && ++ParryLeft > 20) ParryLeft = 0;
            if (guard == ShieldRaised) return;
            ShieldRaised = guard;
            if (guard)
            {
                if (parryCooldown == 0) ParryLeft = 1;
                TerraMod.Instance?.CancelUse();
            }
            else
            {
                parryCooldown = 15;
                ParryLeft = 0;
                TerraMod.Instance?.AttackCooldown(20);
            }
        }

        /// <summary>
        /// CanParryAgainst (Pl:31673-31680) on an enemy's touch: in the first 20 ticks of the
        /// shield coming up, an enemy on the side the player faces is turned: no hurt, double
        /// thorns at knockback 5, 30 ticks' immunity, Striking Moment for 5 s (Pl:31604-31667).
        /// </summary>
        public static bool TryParry(HeroController hero, HealthManager attacker, int contact)
        {
            if (ParryLeft <= 0 || attacker == null) return false;
            var col = attacker.GetComponent<Collider2D>() ?? attacker.GetComponentInChildren<Collider2D>();
            if (col == null) return false;
            int facing = hero.cState.facingRight ? 1 : -1;
            float side = col.bounds.center.x - hero.GetComponent<BoxCollider2D>().bounds.center.x;
            if (Mathf.Sign(side) != facing) return false;
            int damage = Mathf.Min(1000, contact * 2);
            var mod = TerraMod.Instance;
            if (mod != null && damage > 0) mod.Melee.HitWith(col, damage, 5f, 0f, facing, hero.GetComponent<BoxCollider2D>().bounds.center);
            TerraHurt.Invulnerable(hero, Equipment.Current.LongImmunity ? 60 : 30);
            PlayerBuffs.Add(StrikingMoment, 300);
            DevParries++;
            return true;
        }

        /// <summary>The Brand of the Inferno's swing: a flame mote (6) off the blade each tick (Pl:45237-45243).</summary>
        public static void SwingDust(ItemDef w, Rect swing, float dir, Vector2 playerVelPx)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            if (w.Type == ItemType.Hammush) HammushMushrooms(w, swing, dir);
            var tl = TerraParticles.ToPx(new Vector2(swing.xMin, swing.yMax));
            int rw = Mathf.RoundToInt(swing.width / TerraParticles.Px), rh = Mathf.RoundToInt(swing.height / TerraParticles.Px);
            if (w.Type == ItemType.DD2SquireDemonSword)
            {
                int i = p.NewDust(tl, rw, rh, 6, playerVelPx.x * 0.2f + dir * 3f, playerVelPx.y * 0.2f, 100, 0.7f);
                if (i < 0) return;
                ref var d = ref p[i];
                d.NoGravity = true;
                d.Velocity *= 2f;
                d.FadeIn = 0.9f;
            }
            // The Frostbrand's frost (67) one tick in three and the Beam Sword's gold (64) one in two (Pl:45167-45171, 45224-45229).
            if (w.Type == ItemType.Frostbrand && Random.Range(0, 3) == 0)
            {
                int i = p.NewDust(tl, rw, rh, 67, playerVelPx.x * 0.2f + dir * 3f, playerVelPx.y * 0.2f, 90, 1.5f);
                if (i >= 0)
                {
                    p[i].NoGravity = true;
                    p[i].Velocity *= 0.2f;
                }
            }
            if (w.Type == ItemType.BeamSword && Random.Range(0, 2) == 0)
            {
                int i = p.NewDust(tl, rw, rh, 64, 0f, 0f, 150, 1.2f);
                if (i >= 0) p[i].NoGravity = true;
            }
            // The phaseblades light up where they swing (Pl:45290-45297): their colour at half.
            if (w.Type != ItemType.Keybrand && TerraProjectiles.IsPhaseblade(w.Shoot))
                FxBatch.Instance?.Light(tl + new Vector2(rw / 2f, rh / 2f), TerraProjectiles.PhaseColor(w.Shoot) * 0.5f, 3f);
            // The Zombie Arm sheds a bit of itself one tick in sixty (gores 3-5).
            if (w.Type == ItemType.ZombieArm && Random.Range(0, 60) == 0)
                p.NewGore(tl + new Vector2(rw / 2f, rh / 2f), new Vector2(dir * (2f + Random.value * 3f), -2f - Random.value * 3f), Random.Range(3, 6), 1f, 1f, default, true, falls: true);
        }

        // ------------------------------------------------------------------ useStyle 3 and the hold styles

        /// <summary>
        /// ItemCheck_ApplyUseStyle's useStyle 3 (Pl:51653-51680): hidden for the first third, then
        /// level in front at the waist (24 px down), drawn back in as the use runs out; the
        /// umbrellas 6 px nearer. Relative to the hitbox's top-left as if facing right, Terraria px.
        /// </summary>
        public static bool ThrustLocation(ItemDef w, float remaining, float frameW, float scale, out Vector2 loc, out float rot)
        {
            rot = 0.8f;
            loc = default;
            if (remaining > 0.666f) return false;
            float back = remaining * frameW * scale * 1.2f - 10f;
            if (back < 4f) back = 8f;
            float x = 10f + (frameW * 0.5f - 4f) - back;
            loc = new Vector2(x, 24f);
            if (w.Type == ItemType.Umbrella || w.Type == ItemType.TragicUmbrella) loc.x -= 6f;
            return true;
        }

        /// <summary>DrawPlayer_27_HeldItem (PDL:3329-3333): an umbrella in use is drawn 22 px higher, turned a quarter (the open canopy thrust ahead).</summary>
        public static void ThrustDraw(ItemDef w, ref Vector2 loc, ref float rot)
        {
            if (w.Type != ItemType.Umbrella && w.Type != ItemType.TragicUmbrella) return;
            loc.y -= 22f;
            rot = 1.57f;
        }

        /// <summary>
        /// ItemCheck_GetMeleeHitbox for useStyle 3 (Pl:43890-43945): the item's box at its place,
        /// 1.4 wide, its lower 0.6; the umbrellas 14 px taller and 10 narrower. A Terraria-px rect
        /// (top-left, y down); none in the first third.
        /// </summary>
        public static bool ThrustBox(ItemDef w, Vector2 hitboxTl, int dir, float remaining, float frameW, float frameH, float scale, out Rect box)
        {
            box = default;
            if (!ThrustLocation(w, remaining, frameW, scale, out var loc, out _)) return false;
            float cx = hitboxTl.x + 10f;
            float x = cx + (loc.x - 10f) * dir, y = hitboxTl.y + loc.y;
            float bw = (int)(frameW * scale), bh = (int)(frameH * scale);
            if (dir == -1) x -= bw;
            y -= bh;
            if (dir == -1) x -= (int)(bw * 1.4f - bw);
            bw = (int)(bw * 1.4f);
            y += (int)(bh * 0.6f);
            bh = (int)(bh * 0.6f);
            if (w.Type == ItemType.Umbrella || w.Type == ItemType.TragicUmbrella)
            {
                bh += 14f;
                bw -= 10f;
                if (dir == -1) x += 10f;
            }
            box = new Rect(x, y, bw, bh);
            return true;
        }

        /// <summary>
        /// ItemCheck_ApplyHoldStyle (Pl:50879-51113) and PlayerFrame's hold frames (Pl:36736-36743)
        /// while not in use: the Zombie Arm low in front (body 3), the umbrellas open over the head
        /// and the Breathing Reed up at 45 degrees (body 2). Relative to the hitbox's top-left as if
        /// facing right, Terraria px; rot clockwise radians.
        /// </summary>
        public static bool HoldLocation(ItemDef w, out Vector2 loc, out float rot, out int body)
        {
            loc = default;
            rot = 0f;
            body = 0;
            // A fishing pole while its bobber is out (ItemCheck_HackHoldStyles: holdStyle 1), a little in front (Pl:51000-51002, +4 down).
            if (w != null && w.FishingPower > 0)
            {
                if (!Fishing.Active) return false;
                var pole = TerrariaAssets.Texture(w.Texture);
                loc = new Vector2(10f + (int)(pole.width * 0.18f), 28f);
                body = 3;
                return true;
            }
            if (w == null || w.HoldStyle == 0) return false;
            if (w.Type == ItemType.Umbrella || w.Type == ItemType.TragicUmbrella)
            {
                loc = new Vector2(10f - 16f, 22f);
                body = 2;
                return true;
            }
            if (w.HoldStyle == 2)
            {
                loc = new Vector2(10f + 6f, 16f);
                rot = -0.79f;
                body = 2;
                return true;
            }
            // The Zombie Arm's own place (Pl:51019-51023); anything else just in front of the hand.
            if (w.Type == ItemType.ZombieArm)
            {
                loc = new Vector2(10f + 6f, 26f);
                body = 3;
                return true;
            }
            var tex = TerrariaAssets.Texture(w.Texture);
            loc = new Vector2(10f + tex.width * 0.5f + 2f, 24f);
            body = 3;
            return true;
        }

        /// <summary>
        /// The umbrellas held (not in use): falling no faster than 2 px a tick unless down is held,
        /// and no fall damage (fallStart follows the player, Pl:51078-51101); the shield up: no
        /// faster than a third of the run, and halved on the ground past 3 (Pl:26153-26160).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            var mod = TerraMod.Instance;
            if (!TerraMovement.Enabled || mod == null || World.TerrainSystem.Current == null || __instance.cState.dead) return;
            if (UmbrellaHeld && !mod.IsUsingItem && !__instance.cState.onGround)
            {
                mod.ResetFall();
                float cap = -2f * TerraMovement.PxT;
                if (___rb2d.linearVelocity.y < cap && !TerraMovement.DownHeld)
                {
                    // The step's gravity is paid in advance, so the fall is the cap.
                    float g = -Physics2D.gravity.y * ___rb2d.gravityScale * Time.fixedDeltaTime;
                    ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x, cap + g);
                }
            }
            if (ShieldRaised && __instance.cState.onGround && Mathf.Abs(___rb2d.linearVelocity.x) > 3f * TerraMovement.PxT)
                ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x / 2f, ___rb2d.linearVelocity.y);
        }

        /// <summary>The run speed factor these give (the moveSpeed debuff of the raised shield: a third).</summary>
        public static float MoveSpeedMult => ShieldRaised ? 1f / 3f : 1f;

        /// <summary>The Psycho Knife's hidden player runs slower: maxRunSpeed less half of it × how hidden, and no faster boots (Pl:26816-26821).</summary>
        public static float PsychoSlow => PsychoHeld && Stealth < 1f ? 1f - Stealth : 0f;

        public static void Clear()
        {
            rapidBonus = 0f;
            Stealth = 1f;
            stealthTimer = 0;
            ShieldRaised = false;
            ParryLeft = 0;
            parryCooldown = 0;
        }
    }
}
