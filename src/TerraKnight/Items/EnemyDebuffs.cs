using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's debuffs on enemies (NPC.AddBuff, BuffApplyDOTs, the buff visuals). Every tick
    /// each running buff counts down; the damage-over-time ones add up their life loss
    /// (lifeRegen, per 120 of which a point goes), and the strongest one's chunk size sets how big
    /// each payout is (Cursed Inferno 10 at a time, Frostburn 2, Venom 15, Potent Acid 60...),
    /// shown in Terraria's orange. Ichor lowers the defense a hit meets by up to 15 (as armour
    /// penetration), Midas makes the enemy drop more geo; Confused only shows (the Knight's
    /// enemies are steered by their own scripts, there's no chase direction to flip).
    /// </summary>
    [HarmonyPatch]
    internal static class EnemyDebuffs
    {
        public const int Poisoned = 20, OnFire = 24, Confused = 31, CursedInferno = 39, Frostburn = 44, Ichor = 69, Venom = 70,
            Midas = 72, Slimed = 137, Celled = 183, Shadowflame = 153, ScytheMark = 310, Hellfire = 323, Frostbite = 324, PotentAcid = 395,
            EelMark = 362, AcceleratePoisons = 398, TentacleSpiked = 337, BloodButchered = 344, Bleeding = 30, Daybroken = 189, Javelined = 169,
            SoulDrain = 151, BetsysCurse = 203, Oiled = 204, RedLightning = 400;

        /// <summary>
        /// Enemies soul-drained within 1100 px of the player while the Soul Drain is in use, counted
        /// each tick (NPC.UpdateNPC_SoulDrainDebuff → Player.soulDrain): the player's regeneration.
        /// </summary>
        public static int SoulDrainers { get; private set; }

        /// <summary>Life lost per 120 a tick (NPC.lifeRegen), the payout chunk (0 = one point at a time).</summary>
        static readonly Dictionary<int, (int Loss, int Chunk)> dots = new Dictionary<int, (int, int)>
        {
            [Poisoned] = (12, 0), [OnFire] = (8, 0), [CursedInferno] = (48, 10), [Frostburn] = (16, 2), [Venom] = (60, 15),
            [Shadowflame] = (30, 5), [Hellfire] = (30, 5), [Frostbite] = (50, 10), [PotentAcid] = (180, 60),
            // The Electric Eel whip's mark (N:92781): DOTTally (100, 50), 50 a second.
            [EelMark] = (100, 50),
            // NPC bleeding (N:92725-92728): AddBloodLoss(24, 4).
            [Bleeding] = (24, 4),
            // The Soul Drain's mark (N:92802-92805): 50, 5 at a time.
            [SoulDrain] = (50, 5),
        };

        sealed class State
        {
            public HealthManager Hm;
            public readonly Dictionary<int, int> Buffs = new Dictionary<int, int>();
            public int RegenCount;
        }

        static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        static readonly List<int> gone = new List<int>();
        static readonly List<int> buffKeys = new List<int>();

        /// <summary>NPC.AddBuff: only lengthens what is left. Not on a boss out of reach (ArenaGuard: the Knight outside its arena) or asleep.</summary>
        public static void Add(HealthManager hm, int buff, int ticks)
        {
            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || ticks <= 0) return;
            if (OutOfReach(hm))
            {
                DevRefused++;
                return;
            }
            if (!states.TryGetValue(hm.GetInstanceID(), out var s)) states[hm.GetInstanceID()] = s = new State { Hm = hm };
            s.Buffs[buff] = s.Buffs.TryGetValue(buff, out int left) ? Mathf.Max(left, ticks) : ticks;
        }

        /// <summary>NPC.DelBuff: the debuff taken off (water putting out the fires, N:94420-94432).</summary>
        public static void Remove(HealthManager hm, int buff)
        {
            if (hm != null && states.TryGetValue(hm.GetInstanceID(), out var s)) s.Buffs.Remove(buff);
        }

        public static bool Has(HealthManager hm, int buff) =>
            hm != null && states.TryGetValue(hm.GetInstanceID(), out var s) && s.Buffs.TryGetValue(buff, out int t) && t > 0;

        /// <summary>NPC.checkArmorPenetration: the hit's armour penetration plus Ichor's 15, half of it (at most half the defense) back.</summary>
        public static int ArmorBonus(HealthManager hm, int defense, int armorPen)
        {
            if (Has(hm, Ichor)) armorPen += 15;
            // Betsy's Curse (N:91912): 40 less defence.
            if (Has(hm, BetsysCurse)) armorPen += 40;
            if (armorPen <= 0 || defense <= 0) return 0;
            return Mathf.Min(armorPen, defense) / 2;
        }

        /// <summary>
        /// The Magma Stone's Hellfire: a true melee hit 6 s one time in four, else 4 s one in two,
        /// else 2 s (Player.StatusToNPC); a melee projectile 6 s one in seven, else 2 s one in three,
        /// else 1 s (Projectile.StatusNPC).
        /// </summary>
        public static void Magma(HealthManager hm, bool projectile)
        {
            if (!Equipment.Current.MagmaStone) return;
            int ticks = projectile
                ? (Random.Range(0, 7) == 0 ? 360 : Random.Range(0, 3) == 0 ? 120 : 60)
                : (Random.Range(0, 4) == 0 ? 360 : Random.Range(0, 2) == 0 ? 240 : 120);
            Add(hm, Hellfire, ticks);
        }

        public static void Tick()
        {
            gone.Clear();
            var hero = HeroController.SilentInstance;
            bool draining = hero != null && TerraMod.Instance != null && TerraMod.Instance.SwingItem != null && TerraMod.Instance.SwingItem.Type == ItemType.SoulDrain;
            Vector2 heroPx = hero != null ? Effects.TerraParticles.ToPx(hero.transform.position) : Vector2.zero;
            SoulDrainers = 0;
            foreach (var kv in states)
            {
                var s = kv.Value;
                var hm = s.Hm;
                // A pooled enemy is no longer the live target that received these debuffs.
                // Never keep damaging or emitting particles from an inactive pool entry.
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || s.Buffs.Count == 0)
                {
                    gone.Add(kv.Key);
                    continue;
                }
                if (draining && s.Buffs.ContainsKey(SoulDrain))
                {
                    var at = Effects.TerraParticles.ToPx(hm.transform.position);
                    if ((at - heroPx).magnitude < 1100f)
                    {
                        SoulDrainers++;
                        // A wisp (235) drawn off it toward the player, two times in three (N:92088-92097).
                        if (Random.Range(0, 3) != 0 && Effects.TerraParticles.Instance != null)
                        {
                            int d = Effects.TerraParticles.Instance.NewDust(at + new Vector2(Random.Range(-100, 100), Random.Range(-100, 100)) * 0.05f, 1, 1, 235);
                            if (d >= 0)
                            {
                                Effects.TerraParticles.Instance[d].Velocity = Vector2.zero;
                                Effects.TerraParticles.Instance[d].Scale = Random.Range(70, 85) * 0.01f;
                                Effects.TerraParticles.Instance[d].FadeIn = 1f;
                            }
                        }
                    }
                }
                int loss = 0, chunk = -1;
                buffKeys.Clear();
                buffKeys.AddRange(s.Buffs.Keys);
                foreach (int b in buffKeys)
                {
                    if (--s.Buffs[b] <= 0)
                    {
                        s.Buffs.Remove(b);
                        continue;
                    }
                    // NPC.AddStackedProjecitle(971, 3, 1), (975, 4, 1) and (636, 100, 4): for each one stuck in it (at least
                    // one), 3, 4 or 100 a second, paid out a second's worth at a time (Daybreak's a quarter of that).
                    // NPC.AddStackedProjecitle(598, 3, 1): the Bone Javelins, 3 a second each.
                    if (b == TentacleSpiked || b == BloodButchered || b == Daybroken || b == Javelined)
                    {
                        var tp = TerraProjectiles.Instance;
                        int per = b == TentacleSpiked || b == Javelined ? 3 : b == BloodButchered ? 4 : 100, divider = b == Daybroken ? 4 : 1;
                        int stuck = tp == null ? 1 : b == Daybroken ? tp.StuckDaybreaks(hm) : b == Javelined ? tp.StuckBoneJavelins(hm)
                            : tp.Stuck(hm, b == TentacleSpiked ? ProjKind.TentacleSpikeStuck : ProjKind.BloodButchererStuck);
                        stuck = Mathf.Max(1, stuck);
                        loss += stuck * 2 * per;
                        chunk = Mathf.Max(chunk, stuck * per / divider);
                    }
                    if (b == Celled)
                    {
                        // NPC.AddStackedProjecitle(614, 20, 1): 20 a second for each seed clinging (at least one).
                        int cells = Mathf.Max(1, TerraProjectiles.Instance != null ? TerraProjectiles.Instance.StuckCells(hm) : 1);
                        loss += 40 * cells;
                        chunk = Mathf.Max(chunk, 20 * cells);
                    }
                    if (dots.TryGetValue(b, out var d))
                    {
                        // Slimed (drippingSlime) doubles what burns (NPC.AddFlamable's list); the Catalyst Band's mark
                        // (acceleratePoisons, N:92826-92831) doubles again every poison, bleeding and burn.
                        int l = s.Buffs.ContainsKey(Slimed) && Flamable(b) ? d.Loss * 2 : d.Loss;
                        if (b != EelMark && s.Buffs.ContainsKey(AcceleratePoisons)) l *= 2;
                        loss += l;
                        if (d.Chunk > 0) chunk = Mathf.Max(chunk, d.Chunk);
                    }
                }
                // Oiled (N:92769-92772): what burns (On Fire!, Cursed Inferno, Hellfire, Frostburn, Frostbite, Shadowflame) 50 more, 10 at a time.
                if (s.Buffs.ContainsKey(Oiled) && (s.Buffs.ContainsKey(OnFire) || s.Buffs.ContainsKey(CursedInferno) || s.Buffs.ContainsKey(Hellfire)
                    || s.Buffs.ContainsKey(Frostburn) || s.Buffs.ContainsKey(Frostbite) || s.Buffs.ContainsKey(Shadowflame)))
                {
                    loss += 50;
                    chunk = Mathf.Max(chunk, 10);
                }
                Visuals(s);
                if (loss == 0) continue;
                s.RegenCount -= loss;
                // No chunk: a backlog is settled in one payout.
                int n = chunk > 0 ? chunk : Mathf.Max(1, s.RegenCount / -120);
                while (s.RegenCount <= -120 * n)
                {
                    s.RegenCount += 120 * n;
                    Damage(hm, n);
                    if (hm.isDead) break;
                }
            }
            foreach (int k in gone) states.Remove(k);
        }

        /// <summary>
        /// A boss the Knight's blows can't reach yet (World.ArenaGuard.Refuse's: he is outside its arena, or it is off the
        /// map) or still asleep (BossWake): no debuff takes on it, and none it carries burns it, so a burning arrow from
        /// outside the Gruz Mother's room neither wakes nor hurts her. The waking blow itself is the arena's.
        /// </summary>
        static bool OutOfReach(HealthManager hm) => World.ArenaGuard.OffMap(hm) || World.ArenaGuard.Outside(hm) || BossWake.Asleep(hm);

        public static int DevRefused, DevHeldOff;

        static bool Flamable(int b) => b == OnFire || b == Hellfire || b == CursedInferno || b == Frostburn || b == Frostbite || b == Shadowflame;

        /// <summary>NPC.GetHurtByDebuff: off its Terraria life (so its own health), the kill through the game's own hit.</summary>
        static void Damage(HealthManager hm, int amount)
        {
            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) return;
            // Out of reach, or invincible all round (burrowed, shut in): nothing off it, and never a waking blow (BossWake.Hit below
            // only holds an awake boss's intro off).
            if (OutOfReach(hm) || EnemyStats.Untouchable(hm))
            {
                DevHeldOff++;
                return;
            }
            if (BossWake.DreamWindow(hm) || BossWake.Hit(hm) != BossWake.Result.Normal) return;
            amount = PlayerChallenges.Outgoing(amount);
            if (World.TargetDummy.Is(hm)) World.TargetDummy.Record(hm, amount);
            var info = EnemyStats.Of(hm);
            int dealt = EnemyStats.ToGameDamage(info, amount);
            var col = hm.GetComponent<Collider2D>();
            var top = col != null ? col.bounds.center + Vector3.up * col.bounds.extents.y : hm.transform.position;
            UI.CombatText.Show(top, amount.ToString(), new Color(1f, 140 / 255f, 40 / 255f));
            if (dealt <= 0) return;
            // Another player's game runs this enemy (a LAN game): the damage is taken off there.
            if (Net.Enemies.ForwardDot(hm, dealt, amount)) return;
            if (hm.hp - dealt > 0)
            {
                hm.hp -= dealt;
                return;
            }
            HitTaker.Hit(hm.gameObject, new HitInstance
            {
                Source = TerraHitFx.Source,
                AttackType = AttackTypes.Generic,
                DamageDealt = Mathf.Max(1, hm.hp),
                Direction = 0f,
                Multiplier = 1f,
                MagnitudeMultiplier = 0f,
                IgnoreInvulnerable = true,
            });
        }

        /// <summary>NPC.UpdateNPC_BuffApplyVFXEffects: flames, bubbles and drips over the enemy.</summary>
        static void Visuals(State s)
        {
            var col = s.Hm.GetComponent<Collider2D>();
            var p = TerraParticles.Instance;
            // Dirtcarvers remain active underground, but their native FSM hides the renderer.
            // Keep the buff timer and invulnerability rules, without drawing fire from underground.
            var renderer = s.Hm.GetComponent<Renderer>();
            if (col == null || !col.enabled || p == null || renderer != null && !renderer.enabled) return;
            var b = col.bounds;
            var topLeft = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
            int w = Mathf.RoundToInt(b.size.x / TerraParticles.Px), h = Mathf.RoundToInt(b.size.y / TerraParticles.Px);
            var middle = topLeft + new Vector2(w / 2f, 16f);
            foreach (var kv in s.Buffs)
            {
                switch (kv.Key)
                {
                    case OnFire:
                    case Hellfire:
                        Flames(p, topLeft, w, h, 6, 100, 3.5f);
                        FxBatch.Instance?.Light(middle, new Color(1f, 0.3f, 0.1f), 3f);
                        break;
                    case CursedInferno:
                        Flames(p, topLeft, w, h, 75, 100, 3.5f);
                        FxBatch.Instance?.Light(middle, new Color(1f, 0.3f, 0.1f), 3f);
                        break;
                    case Frostburn:
                    case Frostbite:
                        Flames(p, topLeft, w, h, 135, 100, 3.5f);
                        FxBatch.Instance?.Light(middle, new Color(0.1f, 0.6f, 1f), 3f);
                        break;
                    case Daybroken:
                        // N:92386-92399: big solar embers (158) three ticks in four, a quarter of them falling; a red-orange light.
                        if (Random.Range(0, 4) < 3)
                        {
                            int i = p.NewDust(topLeft - new Vector2(2f, 2f), w + 4, h + 4, 158, 0f, 0f, 100, 3.5f);
                            if (i >= 0)
                            {
                                ref var d = ref p[i];
                                d.NoGravity = true;
                                d.Velocity *= 2.8f;
                                d.Velocity.y -= 0.5f;
                                if (Random.Range(0, 4) == 0)
                                {
                                    d.NoGravity = false;
                                    d.Scale *= 0.5f;
                                }
                            }
                        }
                        FxBatch.Instance?.Light(middle, new Color(1f, 0.3f, 0.1f), 3f);
                        break;
                    case Shadowflame:
                        if (Random.Range(0, 5) != 0)
                        {
                            int i = p.NewDust(topLeft - new Vector2(2f, 2f), w + 4, h + 4, 27, 0f, 0f, 180, 1.95f);
                            if (i >= 0)
                            {
                                ref var d = ref p[i];
                                d.NoGravity = Random.Range(0, 4) != 0;
                                d.Velocity *= 0.75f;
                                d.Velocity.x *= 0.75f;
                                d.Velocity.y -= 1f;
                                if (!d.NoGravity) d.Scale *= 0.5f;
                            }
                        }
                        break;
                    case Oiled:
                        // N:92417-92433: black drips (4) two ticks in three.
                        if (Random.Range(0, 3) != 0)
                        {
                            int i = p.NewDust(topLeft - new Vector2(2f, 2f), w + 4, h + 2, 4, 0f, 0f, 175, 1.4f);
                            if (i >= 0) p[i].Color = new Color(0f, 0f, 0f, 250f / 255f);
                        }
                        break;
                    case Poisoned:
                        if (Random.Range(0, 30) == 0)
                        {
                            int i = p.NewDust(topLeft, w, h, 46, 0f, 0f, 120, 0.2f);
                            if (i >= 0)
                            {
                                p[i].NoGravity = true;
                                p[i].FadeIn = 1.9f;
                            }
                        }
                        break;
                    case Venom:
                    case PotentAcid:
                        if (Random.Range(0, kv.Key == PotentAcid ? 2 : 10) == 0)
                        {
                            int i = p.NewDust(topLeft, w, h, 171, 0f, 0f, 100, 0.5f);
                            if (i >= 0)
                            {
                                p[i].NoGravity = true;
                                p[i].FadeIn = 1.5f;
                            }
                        }
                        break;
                    case Slimed:
                        // N:92539: blue slime dripping three ticks in four.
                        if (Random.Range(0, 4) != 0)
                        {
                            int alpha = 175 + (Random.Range(0, 2) == 0 ? 25 : 0) + (Random.Range(0, 2) == 0 ? 25 : 0);
                            int i = p.NewDust(topLeft - new Vector2(2f, 2f), w + 4, h + 2, 4, 0f, 0f, alpha, 1.4f);
                            if (i >= 0)
                            {
                                p[i].Color = FxBatch.C(0, 80, 255, 100);
                                p[i].Velocity = p[i].Velocity * 0.2f + new Vector2(0f, 0.2f);
                            }
                        }
                        break;
                    case RedLightning:
                        // N:92286-92294: the Arc Surge's mark, a red spark somewhere on it one tick in ten (no harm of its own).
                        if (Random.Range(0, 10) == 0)
                            FxParticles.RedLightningSmall(topLeft + new Vector2(Random.value * w, Random.value * h), Random.insideUnitCircle);
                        break;
                    case EelMark:
                        // N:92270: a small blue spark somewhere on it one tick in ten.
                        if (Random.Range(0, 10) == 0)
                            FxParticles.BlueLightningSmall(topLeft + new Vector2(Random.value * w, Random.value * h), Random.insideUnitCircle);
                        break;
                    case ScytheMark:
                        // N:92295: Dark Harvest's mark, small black lightning one tick in three.
                        if (Random.Range(0, 3) == 0)
                            FxParticles.BlackLightningSmall(topLeft + new Vector2(Random.value * w, Random.value * h), Random.insideUnitCircle);
                        break;
                    case Confused:
                        // TextureAssets.Confuse over the head, (250,250,250,70), breathing a little.
                        FxBatch.Instance?.Draw(FxBatch.Whole("Confuse"), topLeft + new Vector2(w / 2f, -14f), FxBatch.C(250, 250, 250, 70), 0f,
                            0.9f + 0.1f * Mathf.Sin(Time.time * 4f));
                        break;
                }
            }
        }

        /// <summary>The On Fire! pattern: three in four ticks a flame over the box (a size bigger), most floating up.</summary>
        static void Flames(TerraParticles p, Vector2 topLeft, int w, int h, int type, int alpha, float scale)
        {
            if (Random.Range(0, 4) == 0) return;
            int i = p.NewDust(topLeft - new Vector2(2f, 2f), w + 4, h + 4, type, 0f, 0f, alpha, scale * 0.7f);
            if (i < 0) return;
            ref var d = ref p[i];
            d.NoGravity = Random.Range(0, 4) != 0;
            d.Velocity *= 1.8f;
            d.Velocity.y -= 0.5f;
            if (!d.NoGravity) d.Scale *= 0.5f;
        }

        static readonly AccessTools.FieldRef<HealthManager, int> smallGeo = AccessTools.FieldRefAccess<HealthManager, int>("smallGeoDrops");
        static readonly AccessTools.FieldRef<HealthManager, int> mediumGeo = AccessTools.FieldRefAccess<HealthManager, int>("mediumGeoDrops");
        static readonly AccessTools.FieldRef<HealthManager, int> largeGeo = AccessTools.FieldRefAccess<HealthManager, int>("largeGeoDrops");

        /// <summary>Midas (NPC.cs:80436): an enemy dying with it drops 1.30 to 1.50 times the money.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void BeforeDie(HealthManager __instance)
        {
            if (!__instance.isDead && Has(__instance, Daybroken)) DaybreakDeath(__instance);
            if (__instance.isDead || !Has(__instance, Midas)) return;
            float k = 1f + Random.Range(30, 51) * 0.01f;
            smallGeo(__instance) = Mathf.RoundToInt(smallGeo(__instance) * k);
            mediumGeo(__instance) = Mathf.RoundToInt(mediumGeo(__instance) * k);
            largeGeo(__instance) = Mathf.RoundToInt(largeGeo(__instance) * k);
        }

        static readonly List<Collider2D> spread = new List<Collider2D>();

        /// <summary>
        /// N:82571-82594: dying Daybroken it bursts into twenty embers (6, 259, 158) thrown up, and
        /// sets every other enemy whose centre is within 100 px of its own Daybroken for 5 s.
        /// </summary>
        static void DaybreakDeath(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col == null) return;
            var b = col.bounds;
            var p = TerraParticles.Instance;
            if (p != null)
            {
                var topLeft = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
                int w = Mathf.RoundToInt(b.size.x / TerraParticles.Px), h = Mathf.RoundToInt(b.size.y / TerraParticles.Px);
                for (int k = 0; k < 20; k++)
                {
                    int type = Random.Range(0, 3) switch { 0 => 6, 1 => 259, _ => 158 };
                    int i = p.NewDust(topLeft, w, h, type, 0f, -2.5f, 200);
                    if (i < 0) continue;
                    p[i].Velocity *= 1.4f;
                    p[i].Scale += Random.value;
                }
            }
            spread.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapCircle(b.center, 100f * TerraParticles.Px, filter, spread);
            var done = new HashSet<HealthManager> { hm };
            foreach (var c in spread)
            {
                var other = c.GetComponentInParent<HealthManager>();
                if (other == null || other.isDead || !done.Add(other)) continue;
                var oc = other.GetComponent<Collider2D>() ?? other.GetComponentInChildren<Collider2D>();
                var centre = oc != null ? (Vector2)oc.bounds.center : (Vector2)other.transform.position;
                var info = EnemyStats.Of(other);
                if (info == null || EnemyStats.LifeMax(info) <= 5 || Vector2.Distance(centre, b.center) >= 100f * TerraParticles.Px) continue;
                Add(other, Daybroken, 300);
                DevDaybreakSpread++;
            }
        }

        public static int DevDaybreakSpread;

        public static void Clear() => states.Clear();
    }
}
