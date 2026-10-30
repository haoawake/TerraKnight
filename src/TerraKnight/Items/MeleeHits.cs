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
        readonly HashSet<string> flymealHit = new HashSet<string>();
        readonly GameObject source;
        readonly NailAttackProxy props;
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        ItemDef weapon;
        float direction;
        bool hitEnemyThisSwing;

        /// <summary>The world box the swing covered this frame (empty when not swinging).</summary>
        public Rect SwingBox { get; private set; }

        /// <summary>The name of the Source object of the player's hits (PropHitFx tells a Terraria hit on a prop by it).</summary>
        internal const string SourceName = "Terra Weapon Hit";

        public MeleeHits(Transform parent)
        {
            // HealthManager places hit effects between the target and the Source, so it must exist.
            source = new GameObject(SourceName);
            source.transform.SetParent(parent, false);
            TerraHitFx.Source = source;
            props = new NailAttackProxy(parent);
            PropHitFx.Install();
        }

        public void Begin(ItemDef item, float facing)
        {
            weapon = item;
            direction = facing;
            hitThisSwing.Clear();
            struckThisSwing.Clear();
            logged.Clear();
            flymealHit.Clear();
            hitEnemyThisSwing = false;
            cutDamage = cutKnock = procScale = 1f;
            Art = null;
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

        /// <summary>The remade swing's cut under way (BladeArts): its share of the use's damage and of the knockback; 1 for the game's own swing.</summary>
        float cutDamage = 1f, cutKnock = 1f;

        /// <summary>Dev: every blow its damage exactly, no crits (damage comparisons without the dice).</summary>
        public static bool DevSteady;

        /// <summary>The share of the blow Deal leaves after the enemy's defense (a remade swing's cut), 1 otherwise.</summary>
        float afterDefense = 1f;

        /// <summary>The remade swing striking now (its hits flash in its colours), null for the game's own.</summary>
        public BladeArts.Art Art;

        /// <summary>
        /// The next cut of a remade swing: every enemy and prop may be struck again, this cut's share of the damage and the
        /// knockback (damage 0: the weapon's own projectile slash does the hitting, the blade only sets props off).
        /// </summary>
        public void NextCut(BladeArts.Art art, int index, float damage, float knock, float time)
        {
            Art = art;
            Cut = index;
            hitThisSwing.Clear();
            struckThisSwing.Clear();
            logged.Clear();
            cutDamage = damage;
            cutKnock = knock;
            // Muramasa's cut and the Volcano's fireball on each cut's first enemy, at the cut's share of a swing.
            hitEnemyThisSwing = false;
            procScale = time;
            props.End();
            if (weapon != null && weapon.IsWeapon) props.Begin(direction, weapon.Damage);
        }

        /// <summary>Dev: the enemies the remade swings' cuts have struck, all and by the cut's place in its combo.</summary>
        public static int DevArcHits;
        public static readonly int[] DevCutHits = new int[16];

        /// <summary>The remade swing's cut under way (its place in the combo), and its share of a swing for the first-hit effects.</summary>
        public int Cut;
        float procScale = 1f;

        /// <summary>
        /// Hits whatever a remade cut has swept so far (BladeArts): the sector about the pivot (the shoulder) between lo and hi
        /// (degrees, facing right, counterclockwise from ahead; mirrored for the left), out to radius (world units). Props go
        /// by the sector's box; an enemy by its collider's box reaching into the sector (its nearest point, its centre or a
        /// corner inside it, or one of the sector's rays every 10 degrees going through it).
        /// </summary>
        public void TickArc(Vector2 pivot, float radius, float lo, float hi, float facing)
        {
            if (weapon == null || !weapon.IsWeapon) return;
            direction = facing;
            Vector2 min = pivot, max = pivot;
            for (float a = lo; ; a += 15f)
            {
                var q = pivot + ArcDir(Mathf.Min(a, hi), facing) * radius;
                min = Vector2.Min(min, q);
                max = Vector2.Max(max, q);
                if (a >= hi) break;
            }
            var size = max - min;
            var center = (min + max) * 0.5f;
            source.transform.position = new Vector3(pivot.x + facing * radius * 0.4f, pivot.y, 0f);
            SwingBox = new Rect(min, size);
            if (weapon.Type == ItemType.Flymeal) Town.TownNpcs.FlymealStrike(SwingBox, flymealHit);
            props.Tick(center, size);
            if (cutDamage <= 0f) return;
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = HitMask };
            overlaps.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
            sweeping = true;
            try
            {
                foreach (var col in overlaps)
                    if (col != null && InSector(col.bounds, pivot, radius, lo, hi, facing)) TryHit(col);
            }
            finally { sweeping = false; }
        }

        static Vector2 ArcDir(float deg, float facing)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * facing, Mathf.Sin(r));
        }

        static bool InSector(Bounds b, Vector2 pivot, float radius, float lo, float hi, float facing)
        {
            var bmin = (Vector2)b.min;
            var bmax = (Vector2)b.max;
            if (pivot.x >= bmin.x && pivot.x <= bmax.x && pivot.y >= bmin.y && pivot.y <= bmax.y) return true;
            var near = new Vector2(Mathf.Clamp(pivot.x, bmin.x, bmax.x), Mathf.Clamp(pivot.y, bmin.y, bmax.y));
            if ((near - pivot).sqrMagnitude > radius * radius) return false;
            if (PointIn(near, pivot, radius, lo, hi, facing) || PointIn(b.center, pivot, radius, lo, hi, facing)
                || PointIn(bmin, pivot, radius, lo, hi, facing) || PointIn(bmax, pivot, radius, lo, hi, facing)
                || PointIn(new Vector2(bmin.x, bmax.y), pivot, radius, lo, hi, facing) || PointIn(new Vector2(bmax.x, bmin.y), pivot, radius, lo, hi, facing))
                return true;
            for (float a = lo; ; a += 10f)
            {
                if (SegmentHitsBox(pivot, pivot + ArcDir(Mathf.Min(a, hi), facing) * radius, bmin, bmax)) return true;
                if (a >= hi) break;
            }
            return false;
        }

        static bool PointIn(Vector2 p, Vector2 pivot, float radius, float lo, float hi, float facing)
        {
            var d = p - pivot;
            if (d.sqrMagnitude > radius * radius) return false;
            float ang = Mathf.Atan2(d.y, d.x * facing) * Mathf.Rad2Deg;
            for (int k = -1; k <= 1; k++)
            {
                float a = ang + 360f * k;
                if (a >= lo && a <= hi) return true;
            }
            return false;
        }

        /// <summary>The segment a-b crosses the box (the slab test).</summary>
        static bool SegmentHitsBox(Vector2 a, Vector2 b, Vector2 bmin, Vector2 bmax)
        {
            float t0 = 0f, t1 = 1f;
            var d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                float da = axis == 0 ? d.x : d.y, aa = axis == 0 ? a.x : a.y;
                float mn = axis == 0 ? bmin.x : bmin.y, mx = axis == 0 ? bmax.x : bmax.y;
                if (Mathf.Abs(da) < 1e-6f)
                {
                    if (aa < mn || aa > mx) return false;
                    continue;
                }
                float ta = (mn - aa) / da, tb = (mx - aa) / da;
                if (ta > tb) { float s = ta; ta = tb; tb = s; }
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
            return true;
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
            if (weapon.Type == ItemType.Flymeal) Town.TownNpcs.FlymealStrike(SwingBox, flymealHit);
            props.Tick(center, size);

            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = HitMask };
            overlaps.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
            if (DebugLog != null)
                foreach (var col in overlaps)
                    if (logged.Add(col.name)) DebugLog($"挥砍覆盖 '{col.name}' layer={col.gameObject.layer} trigger={col.isTrigger} hm={(col.GetComponentInParent<HealthManager>() != null)}");
            sweeping = true;
            try { foreach (var col in overlaps) TryHit(col); }
            finally { sweeping = false; }
        }

        /// <summary>The swing's own arc is striking (not something it threw, not a tool's aimed strike).</summary>
        bool sweeping;

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
            if (PropHits.Revek(col)) return;
            // A barb come to rest is a kinematic body the swing's own slash never touches: told it was struck all the same.
            PropHits.TinkStruck(col, direction);
            HeroInRange(col);
            // Geo deposits break under any weapon as under the nail and the pickaxe (the user's, 2026-10-20): the TAKE DAMAGE below.
            // A boss or a battle's enemy struck from outside its arena: nothing (World.ArenaGuard).
            if (World.ArenaGuard.Refuse(col.GetComponentInParent<HealthManager>())) return;
            // A sleeping boss woken by it: one point, no more (Items.BossWake); one still waking takes nothing.
            var wakeHm = col.GetComponentInParent<HealthManager>();
            var wake = BossWake.Hit(wakeHm);
            if (wake == BossWake.Result.Held) return;
            if (wake == BossWake.Result.Woke)
            {
                if (hitThisSwing.Add(wakeHm.gameObject)) Deal(col, wakeHm, wakeHm.gameObject, 1, 0f, 0f, direction, 0);
                return;
            }
            // DamageEnemies tells the collider's object it was hit; geo rocks and many props listen for this (not another game's
            // enemy in a LAN game: its scripts are off here, and the hit goes to the game that runs it).
            if (struckThisSwing.Add(col.gameObject) && !Net.Enemies.IsPuppet(col.GetComponentInParent<HealthManager>())) FSMUtility.SendEventToGameObject(col.gameObject, "TAKE DAMAGE");
            if (!col.isTrigger) TouchOwnersBox(col);

            var hm = col.GetComponentInParent<HealthManager>();
            GameObject target = hm != null ? hm.gameObject : FindResponder(col);
            if (hm != null && Time.time < NoHitUntil) return;
            // An enemy behind a wall, out of the player's sight (Terraria's Player.CanHit; Longnail reaches through).
            if (sweeping && hm != null && !hitThisSwing.Contains(hm.gameObject) && !CharmEffects.MeleeReaches(col)) return;
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
            // A remade swing's cut: its share of the use's damage, taken after the enemy's defense (a cut is part of one
            // blow, not a lighter blow), and of the knockback (BladeArts).
            kb *= cutKnock;
            ArmorEffects.Context = new ArmorEffects.Hit { Class = weapon.Class, TrueMelee = true };
            int rolled;
            afterDefense = cutDamage;
            try { rolled = Deal(col, hm, target, damage + snap, kb, Equipment.Crit(weapon), direction, b.ArmorPen); }
            finally { afterDefense = 1f; }
            if (hm == null || weapon == null) return;
            if (rolled > 0)
            {
                if (Art != null)
                {
                    DevArcHits++;
                    DevCutHits[Mathf.Clamp(Cut, 0, DevCutHits.Length - 1)]++;
                    SwordFx.Instance?.ArtHit(Art, col.bounds.center, direction);
                    BladeSkills.OnArtHit(weapon, hm, rolled);
                    // Each cut of the Horseman's Blade calls its pumpkin, as its slash does, at the cut's share of a swing.
                    if (weapon.Fx == MeleeFx.HorsemansBlade && !hm.isDead)
                        SwordFx.Instance?.ArtPumpkin(hm, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(weapon) * procScale)), kb);
                }
                MeleeSpecials.OnHit(weapon, hm, col, baseDamage, damage, kb, Equipment.Crit(weapon), direction, SwingBox);
                if (hm.isDead) MeleeSpecials.OnKill(weapon);
            }
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
                if (weapon.Fx == MeleeFx.Muramasa || weapon.Fx == MeleeFx.Volcano)
                    SwordFx.Instance?.OnFirstHit(weapon, col, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(weapon) * procScale)), direction);
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
        public bool HitWith(Collider2D col, int damage, float knockback, float critChance, float facing, Vector2 from, int armorPen = -1, int addAfterVariation = 0, bool autonomous = false, float critDamage = 0f)
        {
            var hm = col.GetComponentInParent<HealthManager>();
            // A prop that answers the nail (a bell, a lever) is set off as by a slash.
            if (hm == null) PropHits.Pulse(col, from, facing, damage, autonomous);
            if (hm == null && !autonomous) StrikeGeo(col);
            GameObject target = hm != null ? hm.gameObject : FindResponder(col);
            if (target == null || (hm != null && hm.isDead)) return false;
            if (World.ArenaGuard.Refuse(hm, autonomous)) return false;
            if (autonomous && BossWake.DreamWindow(hm)) return false;
            // A boss is the minions' only once the player's own blow has landed on one in the room (MinionEngage.BossFought).
            if (hm != null && autonomous && !MinionEngage.BossFought && EnemyStats.Of(hm)?.Boss == true) return false;
            if (hm != null && !autonomous) MinionEngage.StruckBoss(hm);
            var wake = BossWake.Hit(hm);
            if (wake == BossWake.Result.Held) return false;
            if (wake == BossWake.Result.Woke)
            {
                source.transform.position = new Vector3(from.x, from.y, 0f);
                Deal(col, hm, target, 1, 0f, 0f, facing, 0);
                return true;
            }
            // A minion's or sentry's blow takes an enemy's health and nothing else of it (MinionQuiet): its machines never
            // hear it, so none turns on the minion; struck, it comes for the player (EnemyAggro).
            bool quiet = autonomous && hm != null;
            // As a spell's DamageEnemies does, the struck enemy's FSMs hear TAKE DAMAGE first (what wakes some of them).
            if (hm != null && !quiet && !Net.Enemies.IsPuppet(hm)) FSMUtility.SendEventToGameObject(col.gameObject, "TAKE DAMAGE");
            source.transform.position = new Vector3(from.x, from.y, 0f);
            int accepted;
            Autonomous = autonomous;
            try
            {
                int pen = armorPen >= 0 ? armorPen : Equipment.Current.ArmorPen;
                // A minion's or sentry's blow goes through MinionArmorPen of defense besides (the user's, 2026-10-20): its hits
                // are small and many, and an early boss's defense took most of each.
                if (autonomous && pen < 9999) pen += MinionArmorPen;
                accepted = Deal(col, hm, target, damage, knockback, critChance, facing, pen, addAfterVariation, quiet, critDamage);
            }
            finally
            {
                Autonomous = false;
            }
            return hm != null && accepted > 0;
        }

        static readonly Dictionary<int, float> propStruck = new Dictionary<int, float>();
        public static int DevPropHits;

        /// <summary>
        /// A blow of the player's on what isn't an enemy, from an attack whose hit test goes over it frame after frame (a
        /// shot, an explosion, a whip's tip, a yoyo, a flail, the Zenith's blades): as a swing's (TryHit) and HitWith's, the
        /// props that answer the nail set off, a geo deposit struck, a breakable that takes hits (a pot, a jar, a crate:
        /// IHitResponder) hit as by a slash, each at most once a quarter second (the nail's pace: a minigun mustn't empty a
        /// geo deposit in a second). A minion's or a sentry's blow sets off only the props that answer the nail, as before.
        /// </summary>
        public void HitProp(Collider2D col, int damage, float facing, Vector2 from, bool autonomous = false)
        {
            if (col == null || !col.enabled || damage <= 0) return;
            PropHits.Pulse(col, from, facing, damage, autonomous);
            if (autonomous) return;
            PropHitFx.Mark();
            StrikeGeo(col);
            var target = FindResponder(col);
            if (target == null) return;
            int id = target.GetInstanceID();
            float now = Time.time;
            if (propStruck.TryGetValue(id, out float at) && now - at < 0.25f) return;
            propStruck[id] = now;
            if (propStruck.Count > 256) propStruck.Clear();
            DevPropHits++;
            source.transform.position = new Vector3(from.x, from.y, 0f);
            Deal(col, null, target, damage, 0f, 0f, facing, 0);
        }

        static readonly Dictionary<int, float> geoStruck = new Dictionary<int, float>();
        public static int DevGeoStrikes;

        /// <summary>
        /// A geo deposit struck by a shot or a blast of the player's: the "TAKE DAMAGE" its FSM counts (as a swing's and the
        /// pickaxe's), at most once a quarter second, as a slash; its trigger never hears a Terraria hit. Minions leave it alone.
        /// </summary>
        static void StrikeGeo(Collider2D col)
        {
            if (col == null || col.GetComponentInParent<GeoRock>() == null) return;
            int id = col.gameObject.GetInstanceID();
            if (geoStruck.TryGetValue(id, out float at) && Time.time - at < 0.25f) return;
            geoStruck[id] = Time.time;
            if (geoStruck.Count > 256) geoStruck.Clear();
            DevGeoStrikes++;
            PropHitFx.Mark();
            FSMUtility.SendEventToGameObject(col.gameObject, "TAKE DAMAGE");
        }

        /// <summary>A minion's or sentry's blow is being dealt (HitWith autonomous): not the player's own (MinionEngage).</summary>
        public static bool Autonomous;

        /// <summary>The defense a minion's or sentry's blow goes through of its own (on top of the player's armor penetration).</summary>
        public const int MinionArmorPen = 5;

        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, PlayMakerFSM> stunFsm = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, PlayMakerFSM>("stunControlFSM");
        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, GameObject> sendHitTo = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, GameObject>("sendHitTo");
        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, bool> preventBlockFx = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, bool>("preventInvincibleEffect");

        /// <summary>How often a shield shows a blocked hit (s): about a nail swing.</summary>
        const float BlockFxGap = 0.35f;
        static readonly System.Collections.Generic.Dictionary<int, float> blockFxAt = new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>Dev: hits a shield or shell stopped.</summary>
        public static int DevBlocked;
        /// <summary>Development measurement only: an accepted hit's post-defense Terraria damage.</summary>
        internal static System.Action<HealthManager, int> DevAcceptedDamage;

        /// <summary>Terraria's magic against a shield only the Knight's spells get past ("Spell Vulnerable"): dealt as a spell.</summary>
        static bool IsSpellOn(HealthManager hm, DamageClass cls) => hm != null && cls == DamageClass.Magic && EnemyStats.ShellOnlySpells(hm);

        /// <summary>
        /// Whether a hit of this class going this way (facing: +1 rightwards) is stopped by the enemy's shield or shell,
        /// as the nail is (HealthManager.IsBlockingByDirection). Another player's enemy in a LAN game is judged there.
        /// </summary>
        public static bool Blocks(HealthManager hm, float facing, DamageClass cls) =>
            hm != null && !hm.isDead && !Net.Enemies.IsPuppet(hm)
            && hm.IsBlockingByDirection(DirectionUtils.GetCardinalDirection(float.IsNaN(HitDirection) ? (facing > 0f ? 0f : 180f) : HitDirection),
                IsSpellOn(hm, cls) ? AttackTypes.Spell : AttackTypes.Generic);

        /// <summary>
        /// The way the hit being dealt travels (degrees, the game's: 0 right, 90 up, 270 down) when it is the nail's (its
        /// slash's own: NailItem.NailHit, Quick Slash's cut), judged against a shield as the game does; NaN: left or right by
        /// where the enemy stands (every other weapon). The Crystal Crawlers on the walls and ceilings of Crystal Peak are
        /// shelled on their outer side only: struck from above or below they are hurt.
        /// </summary>
        public static float HitDirection = float.NaN;

        /// <summary>
        /// A shield held one way (the game's invincibility from one side only: Husk Sentries, Husk Warriors...), the only
        /// thing that sends a shot back. All-round invincibility is an enemy hiding (in the ground, in a bush, curled up
        /// as the Elder Baldur): it stops a shot, which is spent on it.
        /// </summary>
        public static bool IsShield(HealthManager hm) => hm != null && hm.IsInvincible && hm.InvincibleFromDirection != 0;

        /// <summary>Returns the hit's rolled damage (crit doubled), 0 when nothing took it.</summary>

        int Deal(Collider2D col, HealthManager hm, GameObject target, int baseDamage, float knockback, float critChance, float facing, int armorPen, int addAfterVariation = 0, bool quiet = false, float critDamage = 0f)
        {
            // Who dealt it, for the armor sets' on-hit effects (cleared whatever happens).
            var armorCtx = ArmorEffects.Context;
            ArmorEffects.Context = default;
            // The enemy's banner carried: its DamageDealt before the variation (Pl:20597-20600, 44656-44659; P:13388-13391).
            if (hm != null) baseDamage = Banners.Dealt(hm, baseDamage);
            // Fury of the Fallen's frenzy: everything the player deals doubled for its second.
            if (hm != null) baseDamage = CharmEffects.ScaleOutgoing(baseDamage);
            bool crit = !DevSteady && Random.value * 100f < critChance;
            // Terraria rolls each hit within +-15% of the weapon's damage (Main.DamageVar); a snapping stone's strike comes after.
            // Main.DamageVar: ±15%, rolled again by luck (the better of two with good luck, the worse with bad).
            float variation = DevSteady ? 1f : Random.Range(0.85f, 1.15f);
            float luck = Equipment.Current.Luck;
            if (luck > 0f && Random.value < luck) variation = Mathf.Max(variation, Random.Range(0.85f, 1.15f));
            else if (luck < 0f && Random.value < -luck) variation = Mathf.Min(variation, Random.Range(0.85f, 1.15f));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * variation)) + addAfterVariation;
            int rolled = crit ? damage * 2 : damage;

            int dealtHk = damage;
            float pushSpeed = 0f;
            EnemyStats.Info info = null;
            float carryBefore = 0f;
            // Props (levers, breakables) take the old push; enemies Terraria's.
            float magnitude = knockback / 4f;
            // Shields and shells hold as they do against the nail (HealthManager.IsBlockingByDirection): a shielded husk
            // from the side its shield faces, a curled-up Elder Baldur all round. Terraria's magic is the Knight's spells
            // against a shield only spells get past ("Spell Vulnerable"); every other weapon, whip and minion is stopped.
            // A blocked hit takes nothing, not even towards the enemy's next point of health, and shows no number.
            bool spell = IsSpellOn(hm, armorCtx.Class);
            bool blocked = Blocks(hm, facing, armorCtx.Class);
            if (hm != null)
            {
                info = EnemyStats.Of(hm);
                carryBefore = info.Carry;
                // Struck by the player: it comes for him (Terraria's npc.target), from wherever it stands.
                World.EnemyAggro.Struck(hm, info);
                if (Net.NetSession.Active) Net.EnemyTargets.Struck(hm, Net.NetSession.MySlot);
                damage = EnemyStats.Strike(info, damage, crit, armorPen, critDamage);
                if (afterDefense != 1f)
                {
                    float share = damage * afterDefense;
                    int whole = Mathf.FloorToInt(share);
                    damage = Mathf.Max(1, whole + (Random.value < share - whole ? 1 : 0));
                }
                LastStruck = damage;
                // A Target Dummy counts every hit's Terraria damage, also the ones too small to take a point of its health.
                if (info.Dummy && !blocked) World.TargetDummy.Record(hm, damage);
                // Bosses and elites don't budge (knockBackResist 0), the rest by their level.
                float resist = EnemyStats.NoKnockback(info) ? 0f : World.Areas.KnockbackTaken(info.Level);
                pushSpeed = TerraKnockback.Launch(knockback, resist, crit);
                // Terraria's knockback is the mod's own (TerraKnockback.Push): the game's recoil
                // slide is left out, only its hit events (an FSM's HIT LEFT / RIGHT) still go.
                magnitude = 0f;
                // The enemy's own health counts for several Terraria points each, more the deeper it lives.
                dealtHk = blocked ? 1 : EnemyStats.ToGameDamage(info, damage);
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
            // A boss or elite isn't staggered by every hit: the game counts each one towards stunning it
            // (the Stun Control FSM's STUN DAMAGE), meant for a nail's few slashes, not a hail of shots.
            // A boss counts one at a quick slash's pace, none in Hollow (BossBalance); an elite none.
            PlayMakerFSM heldStun = null;
            if (hm != null && EnemyStats.Unstoppable(info) && (quiet || !BossBalance.StunCounts(info, dealtHk)))
            {
                heldStun = stunFsm(hm);
                stunFsm(hm) = null;
            }
            // A minion's or sentry's: nor do its machines hear the hit (HealthManager.TakeDamage's HIT and TOOK DAMAGE).
            if (quiet)
            {
                MinionQuiet.Body = hm.gameObject;
                MinionQuiet.Forward = sendHitTo(hm);
                MinionQuiet.DevQuieted++;
            }
            // A blocked hit's spark, clank and hit-stop (HealthManager.Invincible) once in BlockFxGap per enemy: a fast
            // weapon would freeze the game on each shot the shield takes.
            bool quietBlock = blocked && blockFxAt.TryGetValue(hm.GetInstanceID(), out float lastFx) && Time.time - lastFx < BlockFxGap;
            bool keptPrevent = hm != null && preventBlockFx(hm);
            if (quietBlock) preventBlockFx(hm) = true;
            try
            {
                // A puppet's hit always goes to its owner, who turns the Terraria damage into health with its own carry.
                if (dealtHk > 0 || hm == null || puppet)
                    HitTaker.Hit(target, new HitInstance
                    {
                        Source = source,
                        AttackType = spell ? AttackTypes.Spell : AttackTypes.Generic,
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
                if (quietBlock) preventBlockFx(hm) = keptPrevent;
                MinionQuiet.Body = MinionQuiet.Forward = null;
            }

            var mod = TerraMod.Instance;
            if (hm == null) return rolled;
            int dealt = hpBefore - Mathf.Max(0, hm.hp);
            // A minion's blow told the boss nothing: its health checks are told what passing them needs (MinionQuiet).
            if (quiet && dealt > 0 && !puppet) MinionQuiet.PhaseChecks(hm);
            // The False Knight's raging armor at its last point takes the blow without going lower: not a block.
            if ((blocked || (dealtHk > 0 && !SpecialDeaths.Raging(hm))) && dealt <= 0 && !hm.isDead && !puppet)
            {
                // Blocked (a shell, a shield): nothing taken, nothing pushed.
                DevBlocked++;
                // A native shield/state change can refuse a hit after conversion. Do
                // not spend the fractional damage earned by earlier accepted hits.
                info.Carry = carryBefore;
                if (!quietBlock) blockFxAt[hm.GetInstanceID()] = Time.time;
                // A shield's blocked damage adds up: its bearer's whole life of it breaks it for a while (ShieldBreak).
                if (blocked && ShieldBreak.Take(hm, info, damage))
                {
                    CombatText.Show(col.bounds.center + Vector3.up * col.bounds.extents.y, Lang.T("破盾！"), CombatText.Notice, true);
                    mod.PlaySound("Shatter");
                }
                return 0;
            }
            // Terraria's knockback: launched back and up (bosses and elites have no push speed).
            if (pushSpeed > 0f && !hm.isDead && !puppet)
            {
                // StrikeNPC's big hit: damage ×10 (×15 in Expert and Master) over the enemy's life.
                int scale = World.GameDifficulty.Current >= World.DifficultyLevel.Expert ? 15 : 10;
                bool bigHit = damage * scale > EnemyStats.LifeMax(info);
                TerraKnockback.Push(hm, pushSpeed, facing, bigHit, World.Areas.KnockbackTaken(info.Level));
            }
            DevAcceptedDamage?.Invoke(hm, damage);
            info.LastHit = Time.time;
            var top = col.bounds.center + Vector3.up * col.bounds.extents.y;
            CombatText.Show(top, damage.ToString(), crit ? CombatText.EnemyCrit : CombatText.EnemyHurt, crit);
            TerraHitFx.Bleed(hm, col.bounds, damage, facing, EnemyStats.LifeMax(info));
            mod.PlaySound(hm.isDead ? "NPC_Killed_1" : "NPC_Hit_1");
            ArmorEffects.OnHitNPC(hm, col.bounds.center, damage, armorCtx);
            Achievements.AchievementTracker.Hit(hm, damage, armorCtx.Class, Autonomous);
            // Soul Catcher's theft, Spell Twister's orb.
            CharmEffects.OnPlayerHit(hm, col.bounds.center, damage, armorCtx, Autonomous);
            // All accepted hits share this path: swings, nails, projectiles, minions and sentries.
            // The caller and the projectile's accessory callback must not roll it again.
            if (Equipment.Current.LuckyCoin) AccessoryEffects.LuckyCoin(hm, facing > 0f ? 1 : -1, armorCtx.TrueMelee);
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
        /// game's hit for its damage (no stagger for a boss or elite; shields and shells hold as for a hit here), Terraria's
        /// knockback, the number, the blood and the sound.
        /// </summary>
        public static void ApplyNetHit(HealthManager hm, int dealtHk, float direction, AttackTypes type, float push, int terra, bool crit, bool bigHit, bool dot, Vector2 from)
        {
            // The teammate's blow: the boss's waking decides, not this game's Knight being held (BossWake.ForOther).
            BossWake.ForOther = true;
            try { ApplyNetHitNow(hm, dealtHk, direction, type, push, terra, crit, bigHit, dot, from); }
            finally { BossWake.ForOther = false; }
        }

        static void ApplyNetHitNow(HealthManager hm, int dealtHk, float direction, AttackTypes type, float push, int terra, bool crit, bool bigHit, bool dot, Vector2 from)
        {
            if (hm == null || hm.isDead || BossWake.Hit(hm) != BossWake.Result.Normal || (dot && BossWake.DreamWindow(hm))) return;
            var info = EnemyStats.Of(hm);
            // A Terraria hit: its damage made the enemy's health here, the remainder carried as for a hit of this game's.
            if (!dot && terra > 0)
            {
                dealtHk = EnemyStats.ToGameDamage(info, terra);
                // Its number and blood came with the one who struck (Net.NetFx).
                if (dealtHk <= 0)
                {
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
            // Another player's blow staggers as the player's own does (BossBalance.StunCounts).
            if (EnemyStats.Unstoppable(info) && !BossBalance.StunCounts(info, dealtHk))
            {
                heldStun = stunFsm(hm);
                stunFsm(hm) = null;
            }
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
            }
            float facing = Mathf.Abs(Mathf.DeltaAngle(direction, 180f)) < 90f ? -1f : 1f;
            if (push > 0f && !hm.isDead) TerraKnockback.Push(hm, push, facing, bigHit, World.Areas.KnockbackTaken(info.Level));
            info.LastHit = Time.time;
            if (dot) return;
            // The number and the blood: the striker's own, sent from there (Net.NetFx).
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
