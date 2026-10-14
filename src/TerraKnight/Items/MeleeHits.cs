using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Damage from a swung Terraria item. Each frame of the swing, everything the blade's arc
    /// covers takes one hit per swing, the way the nail's DamageEnemies delivers it: a "TAKE DAMAGE"
    /// event to the object's FSMs (geo rocks, many props) and a hit through HitTaker (enemies
    /// flinch, recoil and die exactly as they do to the nail, breakables break).
    /// </summary>
    internal sealed class MeleeHits
    {
        // The nail's own hitbox layer, and the layers DamageEnemies ignores.
        const int HeroAttackLayer = 17;
        const float BaseCrit = 4f;

        /// <summary>Dev: when set, receives what each swing's arc overlapped.</summary>
        public static System.Action<string> DebugLog;

        readonly HashSet<GameObject> hitThisSwing = new HashSet<GameObject>();
        readonly HashSet<GameObject> struckThisSwing = new HashSet<GameObject>();
        readonly HashSet<string> logged = new HashSet<string>();
        readonly GameObject source;
        readonly NailAttackProxy props;
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        ItemDef weapon;
        float direction;
        bool hitEnemyThisSwing;

        /// <summary>The world box the swing covered this frame (empty when not swinging).</summary>
        public Rect SwingBox { get; private set; }

        public MeleeHits(Transform parent)
        {
            // HealthManager places hit effects between the target and the Source, so it must exist.
            source = new GameObject("Terra Weapon Hit");
            source.transform.SetParent(parent, false);
            TerraHitFx.Source = source;
            props = new NailAttackProxy(parent);
        }

        public void Begin(ItemDef item, float facing)
        {
            weapon = item;
            direction = facing;
            hitThisSwing.Clear();
            struckThisSwing.Clear();
            logged.Clear();
            hitEnemyThisSwing = false;
            MeleeSpecials.OnSwingStart(item);
            // Re-arming the prop trigger each swing lets a lever or pot register every swing.
            props.End();
            if (item.IsWeapon) props.Begin(facing, item.Damage);
        }

        public void End()
        {
            weapon = null;
            SwingBox = default;
            props.End();
        }

        public static int HitMask => Physics2D.GetLayerCollisionMask(HeroAttackLayer) & ~((1 << 20) | (1 << 9) | (1 << 26) | (1 << 31));

        /// <summary>Player.attackCD: no enemy is hit by a swing until then (ApplyAttackCooldown, the shield lowered).</summary>
        public static float NoHitUntil;

        /// <summary>The damage the last strike on an enemy came to after its defense (StrikeNPC's return: what vampireHeal takes a share of).</summary>
        public static int LastStruck;

        /// <summary>Hits whatever the blade covers this frame. hand is the pivot of the swing.</summary>
        public void Tick(Vector2 hand, float bladeLength)
        {
            if (weapon == null || !weapon.IsWeapon) return;
            // The arc sweeps from overhead to the feet in front, so cover that whole half-disc.
            var center = hand + new Vector2(direction * bladeLength * 0.5f, 0.1f);
            var size = new Vector2(bladeLength * 1.1f, bladeLength * 1.8f);
            source.transform.position = new Vector3(hand.x + direction * bladeLength * 0.5f, hand.y, 0f);
            Sweep(center, size);
        }

        /// <summary>Hits whatever a box covers this frame (a thrust's item box, world units).</summary>
        public void TickBox(Rect box)
        {
            if (weapon == null || !weapon.IsWeapon) return;
            source.transform.position = new Vector3(box.center.x - direction * box.width * 0.5f, box.center.y, 0f);
            Sweep(box.center, box.size);
        }

        void Sweep(Vector2 center, Vector2 size)
        {
            SwingBox = new Rect(center - size / 2f, size);
            props.Tick(center, size);

            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = HitMask };
            overlaps.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
            if (DebugLog != null)
                foreach (var col in overlaps)
                    if (logged.Add(col.name)) DebugLog($"挥砍覆盖 '{col.name}' layer={col.gameObject.layer} trigger={col.isTrigger} hm={(col.GetComponentInParent<HealthManager>() != null)}");
            foreach (var col in overlaps) TryHit(col);
        }

        /// <summary>
        /// One hit on a single object regardless of the swing arc, for tools aimed with the mouse
        /// (a pickaxe on a geo rock). Counts as this swing's hit on it.
        /// </summary>
        public void Strike(Collider2D target, ItemDef item, float facing)
        {
            var saved = weapon;
            float savedDirection = direction;
            weapon = item;
            direction = facing;
            source.transform.position = target.bounds.center - new Vector3(facing * 0.5f, 0f, 0f);
            struckThisSwing.Remove(target.gameObject);
            TryHit(target);
            weapon = saved;
            direction = savedDirection;
        }

        /// <summary>
        /// A hit dealt by something the item threw (the Zenith's swords), which keep their own hit
        /// timing instead of the one-hit-per-swing rule. from is where the blow comes from.
        /// </summary>
        public void HitFrom(Collider2D target, ItemDef item, float facing, Vector2 from)
        {
            var saved = weapon;
            float savedDirection = direction;
            weapon = item;
            direction = facing;
            source.transform.position = new Vector3(from.x, from.y, 0f);
            var hm = target.GetComponentInParent<HealthManager>();
            hitThisSwing.Remove(hm != null ? hm.gameObject : target.gameObject);
            struckThisSwing.Remove(target.gameObject);
            TryHit(target);
            weapon = saved;
            direction = savedDirection;
        }

        /// <summary>Dev: off to see a breakable's own range check alone.</summary>
        public static bool HeroRangeFix = true;

        /// <summary>
        /// Breakables that count a hit only while the hero stands by them (King's Pass's first wooden
        /// barrier, break_floor: a "Hero Range" trigger beside it sets a bool on anything coming in
        /// and clears it on anything going out, the swing's own hitbox too, which pressed against the
        /// barrier reaches past the range). A swing hits from where the hero stands: with him within
        /// 3 units of it, the hit counts.
        /// </summary>
        static void HeroInRange(Collider2D col)
        {
            var hero = HeroController.SilentInstance;
            if (!HeroRangeFix || hero == null) return;
            var hb = hero.GetComponent<BoxCollider2D>().bounds;
            if (Vector2.Distance(col.ClosestPoint(hb.center), hb.center) > 3f) return;
            foreach (var fsm in col.GetComponentsInParent<PlayMakerFSM>())
            {
                var range = fsm.FsmVariables.FindFsmBool("Hero Range");
                if (range != null) range.Value = true;
            }
        }

        void TryHit(Collider2D col)
        {
            if (col.CompareTag("Geo")) return;
            HeroInRange(col);
            // Geo deposits are mined, not cut: only a pickaxe gets geo out of them.
            if (weapon.PickPower <= 0 && col.GetComponentInParent<GeoRock>() != null) return;
            // DamageEnemies tells the collider's object it was hit; geo rocks and many props listen for this (not another game's
            // enemy in a LAN game: its scripts are off here, and the hit goes to the game that runs it).
            if (struckThisSwing.Add(col.gameObject) && !Net.Enemies.IsPuppet(col.GetComponentInParent<HealthManager>())) FSMUtility.SendEventToGameObject(col.gameObject, "TAKE DAMAGE");
            if (!col.isTrigger) TouchOwnersBox(col);

            var hm = col.GetComponentInParent<HealthManager>();
            GameObject target = hm != null ? hm.gameObject : FindResponder(col);
            if (hm != null && Time.time < NoHitUntil) return;
            if (target == null || !hitThisSwing.Add(target)) return;
            if (hm != null && hm.isDead) return;
            var b = Equipment.Current;
            // Player.ApplyNPCOnHitEffects' accessories: the Catalyst Band's mark (Pl:6534-6538) before the blow; a
            // charged Pyroclastic Stone's strike added before the damage's variation (Pl:44692-44696).
            int snap = 0;
            if (hm != null)
            {
                if (b.CatalystBand) EnemyDebuffs.Add(hm, EnemyDebuffs.AcceleratePoisons, 300);
                if (b.Pyroclast)
                {
                    // The strike's own hits move the blow's source; this swing's goes back after.
                    var hero = HeroController.SilentInstance;
                    var keep = source.transform.position;
                    snap = AccessoryEffects.SnapStrike(TerraParticles.ToPx(hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : SwingBox.center), hm, 1);
                    source.transform.position = keep;
                }
            }
            // The item's own damage rules before the variation (the rapid bonus, the Breaker Blade, Striking Moment...).
            MeleeSpecials.SwingPx = TerraParticles.ToPx(SwingBox.center);
            int baseDamage = Equipment.Damage(weapon);
            int damage = MeleeSpecials.ModifyDamage(weapon, hm, baseDamage);
            float kb = MeleeSpecials.Knockback(weapon, Equipment.Knockback(weapon));
            ArmorEffects.Context = new ArmorEffects.Hit { Class = weapon.Class, TrueMelee = true };
            int rolled = Deal(col, hm, target, damage + snap, kb, Equipment.Crit(weapon), direction, b.ArmorPen);
            if (hm == null || weapon == null) return;
            if (rolled > 0)
            {
                MeleeSpecials.OnHit(weapon, hm, col, baseDamage, damage, kb, Equipment.Crit(weapon), direction, SwingBox);
                if (hm.isDead) MeleeSpecials.OnKill(weapon);
            }
            // The Lucky Coin's coins off a swing (1-10 at a time).
            if (rolled > 0 && b.LuckyCoin) AccessoryEffects.LuckyCoin(hm, direction > 0f ? 1 : -1, true);
            // Player.StatusToNPC: the Magma Stone sets true melee hits on fire (Hellfire).
            if (rolled > 0 && weapon.Class == DamageClass.Melee) EnemyDebuffs.Magma(hm, false);
            // The Bee Keeper (ApplyNPCOnHitEffects): Confused, and one to three bees out of the swing at a third of the blow.
            if (weapon.Type == ItemType.BeeKeeper && rolled > 0 && !hm.isDead)
            {
                EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 120);
                // The Hive Pack's strong bees (Pl:44855-44866, 12505-12530): one more a third of the time, half of them giant.
                bool strong = b.StrongBees;
                for (int n = Random.Range(1, 4) + (strong && Random.Range(0, 3) == 0 ? 1 : 0); n > 0; n--)
                {
                    var v = new Vector2((direction * 2f + Random.Range(-35, 36) * 0.02f) * 0.2f, -Random.Range(-35, 36) * 0.02f * 0.2f);
                    var kind = strong && Random.Range(0, 2) == 0 ? ProjKind.GiantBee : ProjKind.Bee;
                    TerraProjectiles.Instance?.Shoot(kind, SwingBox.center, v, rolled / 3 + (strong ? Random.Range(1, 4) : Random.Range(0, 2)), strong ? 0.5f : 0f, Equipment.Crit(weapon), melee: true);
                }
            }
            // The sword's own on-hit effects: Muramasa's cut and the Volcano's fireball on its first
            // enemy each swing, and the burning and poisoning blades.
            if (!hitEnemyThisSwing)
            {
                hitEnemyThisSwing = true;
                if (weapon.Fx == MeleeFx.Muramasa || weapon.Fx == MeleeFx.Volcano) SwordFx.Instance?.OnFirstHit(weapon, col, Equipment.Damage(weapon), direction);
            }
            if (weapon.Type == ItemType.FieryGreatsword && Random.value < 0.5f) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
            if (weapon.Type == ItemType.MoltenPickaxe && Random.Range(0, 10) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
            if (weapon.Type == ItemType.BladeofGrass && Random.Range(0, 4) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 420);
        }

        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, float> evasion =
            HarmonyLib.AccessTools.FieldRefAccess<HealthManager, float>("evasionByHitRemaining");

        /// <summary>
        /// One Terraria hit for some damage on whatever a collider belongs to: an enemy (a Terraria
        /// NPC by <see cref="EnemyStats"/>: its level's life, defense and knockback resistance), or
        /// a prop that answers hits. Used by projectiles and explosions. True when an enemy took it.
        /// </summary>
        public bool HitWith(Collider2D col, int damage, float knockback, float critChance, float facing, Vector2 from, int armorPen = -1, int addAfterVariation = 0)
        {
            var hm = col.GetComponentInParent<HealthManager>();
            // A prop that answers the nail (a bell, a lever) is set off as by a slash.
            if (hm == null) PropHits.Pulse(col, from, facing, damage);
            GameObject target = hm != null ? hm.gameObject : FindResponder(col);
            if (target == null || (hm != null && hm.isDead)) return false;
            source.transform.position = new Vector3(from.x, from.y, 0f);
            Deal(col, hm, target, damage, knockback, critChance, facing, armorPen >= 0 ? armorPen : Equipment.Current.ArmorPen, addAfterVariation);
            return hm != null;
        }

        /// <summary>Returns the hit's rolled damage (crit doubled), 0 when nothing took it.</summary>
        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, PlayMakerFSM> stunFsm = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, PlayMakerFSM>("stunControlFSM");

        int Deal(Collider2D col, HealthManager hm, GameObject target, int baseDamage, float knockback, float critChance, float facing, int armorPen, int addAfterVariation = 0)
        {
            // Who dealt it, for the armor sets' on-hit effects (cleared whatever happens).
            var armorCtx = ArmorEffects.Context;
            ArmorEffects.Context = default;
            bool crit = Random.value * 100f < critChance;
            // Terraria rolls each hit within +-15% of the weapon's damage (Main.DamageVar); a snapping stone's strike comes after.
            // Main.DamageVar: ±15%, rolled again by luck (the better of two with good luck, the worse with bad).
            float variation = Random.Range(0.85f, 1.15f);
            float luck = Equipment.Current.Luck;
            if (luck > 0f && Random.value < luck) variation = Mathf.Max(variation, Random.Range(0.85f, 1.15f));
            else if (luck < 0f && Random.value < -luck) variation = Mathf.Min(variation, Random.Range(0.85f, 1.15f));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * variation)) + addAfterVariation;
            int rolled = crit ? damage * 2 : damage;

            int dealtHk = damage;
            float pushSpeed = 0f;
            EnemyStats.Info info = null;
            // Props (levers, breakables) take the old push; enemies Terraria's.
            float magnitude = knockback / 4f;
            if (hm != null)
            {
                info = EnemyStats.Of(hm);
                damage = EnemyStats.Strike(info, damage, crit, armorPen);
                LastStruck = damage;
                // Bosses and elites don't budge (knockBackResist 0), the rest by their level.
                float resist = EnemyStats.Unstoppable(info) ? 0f : World.Areas.KnockbackTaken(info.Level);
                pushSpeed = TerraKnockback.Launch(knockback, resist, crit);
                // Terraria's knockback is the mod's own (TerraKnockback.Push): the game's recoil
                // slide is left out, only its hit events (an FSM's HIT LEFT / RIGHT) still go.
                magnitude = 0f;
                // The enemy's own health counts for several Terraria points each, more the deeper it lives.
                dealtHk = EnemyStats.ToGameDamage(info, damage);
                // Terraria's own immunity rules apply (one hit per swing or projectile); the
                // Knight's short dodge after each nail hit would swallow fast weapons' hits.
                evasion(hm) = 0f;
            }
            else if (crit) damage *= 2;

            int hpBefore = hm != null ? hm.hp : 0;
            // Another player's game runs this enemy (a LAN game): the hit, its knockback and its numbers go there with it
            // (Net.Enemies' HealthManager.Hit prefix sends it); here it is only shown.
            bool puppet = Net.Enemies.IsPuppet(hm);
            if (puppet)
            {
                int bigScale = World.GameDifficulty.Current >= World.DifficultyLevel.Expert ? 15 : 10;
                Net.Enemies.Context = new Net.Enemies.HitContext
                {
                    Push = pushSpeed, TerraDamage = damage, Crit = crit, BigHit = damage * bigScale > EnemyStats.LifeMax(info), Set = true,
                };
            }
            // A boss or elite isn't staggered: the game counts every hit towards stunning it (the
            // Stun Control FSM's STUN DAMAGE), meant for a nail's few slashes, not a hail of shots.
            PlayMakerFSM heldStun = null;
            if (hm != null && EnemyStats.Unstoppable(info))
            {
                heldStun = stunFsm(hm);
                stunFsm(hm) = null;
            }
            // An Elder Baldur's shell keeps out everything but spells: Terraria's hits get through it.
            bool shell = EnemyStats.Shelled(hm);
            if (shell) hm.IsInvincible = false;
            try
            {
                // A puppet's hit always goes to its owner, who turns the Terraria damage into health with its own carry.
                if (dealtHk > 0 || hm == null || puppet)
                    HitTaker.Hit(target, new HitInstance
                    {
                        Source = source,
                        // A shell only spells get through (a curled-up Elder Baldur): Terraria's weapons and minions do too.
                        AttackType = EnemyStats.ShellOnlySpells(hm) ? AttackTypes.Spell : AttackTypes.Generic,
                        DamageDealt = Mathf.Max(1, dealtHk),
                        Direction = facing > 0f ? 0f : 180f,
                        Multiplier = 1f,
                        MagnitudeMultiplier = magnitude,
                        IgnoreInvulnerable = false,
                    });
            }
            finally
            {
                if (heldStun != null) stunFsm(hm) = heldStun;
                if (shell && hm != null && !hm.isDead) hm.IsInvincible = true;
            }

            var mod = TerraMod.Instance;
            if (hm == null) return rolled;
            int dealt = hpBefore - Mathf.Max(0, hm.hp);
            if (dealtHk > 0 && dealt <= 0 && !hm.isDead && !puppet) return 0; // blocked (a shell, a shield)
            // Terraria's knockback: launched back and up (bosses and elites have no push speed).
            if (pushSpeed > 0f && !hm.isDead && !puppet)
            {
                // StrikeNPC's big hit: damage ×10 (×15 in Expert and Master) over the enemy's life.
                int scale = World.GameDifficulty.Current >= World.DifficultyLevel.Expert ? 15 : 10;
                bool bigHit = damage * scale > EnemyStats.LifeMax(info);
                TerraKnockback.Push(hm, pushSpeed, facing, bigHit, World.Areas.KnockbackTaken(info.Level));
            }
            info.LastHit = Time.time;
            var top = col.bounds.center + Vector3.up * col.bounds.extents.y;
            CombatText.Show(top, damage.ToString(), crit ? CombatText.EnemyCrit : CombatText.EnemyHurt, crit);
            TerraHitFx.Bleed(hm, col.bounds, damage, facing, EnemyStats.LifeMax(info));
            mod.PlaySound(hm.isDead ? "NPC_Killed_1" : "NPC_Hit_1");
            ArmorEffects.OnHitNPC(hm, col.bounds.center, damage, armorCtx);
            return rolled;
        }

        /// <summary>
        /// A breakable that feels hits through a trigger box of its own beside its solid (the
        /// cliffs' one-way walls, break_floor: ReceivedDamage watches the box for the slash). Pressed
        /// against the wall the blade reaches only the solid, the box being behind the player: the
        /// slash is taken into the box then, as the nail's would be, once a swing, and only when it
        /// doesn't reach the box by itself.
        /// </summary>
        void TouchOwnersBox(Collider2D col)
        {
            var slash = props.Live;
            if (slash == null) return;
            var t = col.transform.parent;
            for (int depth = 0; depth < 2 && t != null; depth++, t = t.parent)
            {
                if (t.GetComponent<PlayMakerUnity2DProxy>() == null) continue;
                var box = t.GetComponent<Collider2D>();
                if (box == null || !box.isTrigger || !box.enabled) continue;
                if (slash.bounds.Intersects(box.bounds) || !struckThisSwing.Add(t.gameObject)) return;
                DevBoxTouches++;
                t.gameObject.SendMessage("OnTriggerEnter2D", slash, SendMessageOptions.DontRequireReceiver);
                return;
            }
        }

        public static int DevBoxTouches;

        static GameObject netSource;

        /// <summary>
        /// A LAN game: another player's hit on an enemy this game runs (DamageNPC), dealt as theirs was rolled: the
        /// game's hit for its damage (no stagger for a boss or elite, through an Elder Baldur's shell), Terraria's
        /// knockback, the number, the blood and the sound.
        /// </summary>
        public static void ApplyNetHit(HealthManager hm, int dealtHk, float direction, AttackTypes type, float push, int terra, bool crit, bool bigHit, bool dot, Vector2 from)
        {
            if (hm == null || hm.isDead) return;
            var info = EnemyStats.Of(hm);
            // A Terraria hit: its damage made the enemy's health here, the remainder carried as for a hit of this game's.
            if (!dot && terra > 0)
            {
                dealtHk = EnemyStats.ToGameDamage(info, terra);
                if (dealtHk <= 0)
                {
                    var c0 = hm.GetComponent<Collider2D>();
                    var b0 = c0 != null ? c0.bounds : new Bounds(hm.transform.position, Vector3.one);
                    CombatText.Show(b0.center + Vector3.up * b0.extents.y, terra.ToString(), crit ? CombatText.EnemyCrit : CombatText.EnemyHurt, crit);
                    info.LastHit = Time.time;
                    return;
                }
            }
            if (dot)
            {
                // NPC.GetHurtByDebuff: off its life, the kill through the game's own hit.
                if (hm.hp - dealtHk > 0)
                {
                    hm.hp -= dealtHk;
                    return;
                }
                dealtHk = Mathf.Max(1, hm.hp);
            }
            if (netSource == null)
            {
                netSource = new GameObject("Terra Net Hit");
                Object.DontDestroyOnLoad(netSource);
            }
            netSource.transform.position = new Vector3(from.x, from.y, 0f);
            evasion(hm) = 0f;
            PlayMakerFSM heldStun = null;
            if (EnemyStats.Unstoppable(info))
            {
                heldStun = stunFsm(hm);
                stunFsm(hm) = null;
            }
            bool shell = EnemyStats.Shelled(hm);
            if (shell) hm.IsInvincible = false;
            try
            {
                HitTaker.Hit(hm.gameObject, new HitInstance
                {
                    Source = netSource,
                    AttackType = type == AttackTypes.Nail ? AttackTypes.Generic : type,
                    DamageDealt = Mathf.Max(1, dealtHk),
                    Direction = direction,
                    Multiplier = 1f,
                    MagnitudeMultiplier = 0f,
                    IgnoreInvulnerable = dot,
                });
            }
            finally
            {
                if (heldStun != null) stunFsm(hm) = heldStun;
                if (shell && !hm.isDead) hm.IsInvincible = true;
            }
            float facing = Mathf.Abs(Mathf.DeltaAngle(direction, 180f)) < 90f ? -1f : 1f;
            if (push > 0f && !hm.isDead) TerraKnockback.Push(hm, push, facing, bigHit, World.Areas.KnockbackTaken(info.Level));
            info.LastHit = Time.time;
            if (dot) return;
            var col = hm.GetComponent<Collider2D>();
            var b = col != null ? col.bounds : new Bounds(hm.transform.position, Vector3.one);
            if (terra > 0) CombatText.Show(b.center + Vector3.up * b.extents.y, terra.ToString(), crit ? CombatText.EnemyCrit : CombatText.EnemyHurt, crit);
            TerraHitFx.Bleed(hm, b, Mathf.Max(1, terra), facing, EnemyStats.LifeMax(info));
            TerraMod.Instance?.PlaySound(hm.isDead ? "NPC_Killed_1" : "NPC_Hit_1");
        }

        static GameObject FindResponder(Collider2D col)
        {
            var t = col.transform;
            for (int depth = 0; depth < 3 && t != null; depth++, t = t.parent)
                if (t.GetComponent<IHitResponder>() != null) return t.gameObject;
            return null;
        }
    }
}
