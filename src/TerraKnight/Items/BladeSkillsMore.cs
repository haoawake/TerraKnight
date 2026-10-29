using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The right-click arts beyond the draw and the throw, each from the weapon's own description:
    /// <list type="bullet">
    /// <item>Charged: the Breaker Blade's smash (a shock running both ways along the ground, harder on the unhurt), the
    /// Volcano's (pillars of fire bursting from the ground ahead one after another), the Chlorophyte Claymore's (a burst of
    /// spore clouds, "猛击地面时释放大量孢子云"); the Flying Dragon's wave of dragon fire running ahead ("向前释放心的能量");
    /// the Keybrand's execution (a run through, harder on the hurt); the Bat Bat's home run and the Slap Hand's slap (a
    /// great knock); the Terra Blade's run leaving a field of its light; Starfury's and Star Wrath's stars on the cursor.</item>
    /// <item>Pressed: the Waffle Iron shutting on what is ahead; the Falcon Blade's dive toward the cursor, any way; the Influx
    /// Waver through a portal to the cursor and a cut there; the Psycho Knife behind the nearest enemy to the cursor, a
    /// certain crit; Light's Bane's shadow step; the Tentacle Spike's spike that pulls the player to what it sticks in, the
    /// Candy Cane's hook that pulls an enemy close; the Bee Keeper's swarm; the Horseman's pumpkins on the enemies by the
    /// cursor; the Flymeal's cloud of flies (confusing, "整蛊"); the Bladetongue's spray of ichor; the ice swords' row of ice
    /// spikes, the Blade of Grass's thorns; the Ham Bat's bite (life and Well Fed, "吃得越好…"); the Tax Collector's cane
    /// (geo off each enemy struck, "实现快速征税"); the Blood Butcherer's blood rite (life for harder, bleeding cuts).</item>
    /// <item>The Cutlass's and the Excaliburs' parry: a third of a second's guard; a blow or a shot met in it does nothing,
    /// and a riposte answers. The claws' rush while the button is held.</item>
    /// </list>
    /// </summary>
    internal static partial class BladeSkills
    {
        static readonly List<(float At, System.Action Do)> pending = new List<(float, System.Action)>();
        static void Later(float seconds, System.Action a) => pending.Add((Time.time + seconds, a));

        static BladeArts.Move Cut(string name) => BladeArts.Named(name);

        /// <summary>The ground under a point (looked for down to depth), its top; NaN when none.</summary>
        static float Ground(Vector2 from, float depth)
        {
            for (float y = from.y; y >= from.y - depth; y -= 0.1f)
                if (World.TerraWorld.IsSolidAt(new Vector2(from.x, y))) return Mathf.Ceil(y / World.CellGrid.CellSize) * World.CellGrid.CellSize;
            return float.NaN;
        }

        static bool Press(HeroController hero, ItemDef def, Skill skill)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return false;
            if (IsSpearSkill(skill)) return SpearPress(hero, def, skill);
            if (skill == Skill.Kind)
            {
                var art = KindArtOf(def);
                if (art?.Press == null || !art.Press(hero, def)) return false;
                DevLastArt = art.Name;
                return true;
            }
            switch (skill)
            {
                case Skill.Clamp:
                    TurnToAim(hero);
                    mod.SkillCut(def, Cut("Snip"), 0.3f, 2.5f, 0.3f);
                    mod.PlaySound("Item_178", 0.8f);
                    return true;
                case Skill.Dive:
                {
                    TurnToAim(hero);
                    var from = Shoulder(hero, Facing(hero));
                    var way = (Aim() - from).normalized;
                    if (way == Vector2.zero) way = new Vector2(Facing(hero), 0f);
                    Run(hero, def, way, 4.5f, Damage(def, 1.8f), 0f, RunStyle.Dive, 0f);
                    mod.PlaySound("Item_1", 0.6f, 1.4f);
                    return true;
                }
                case Skill.ShadowStep:
                {
                    float facing = Facing(hero);
                    Run(hero, def, new Vector2(facing, 0f), 3.5f, Damage(def, 1.5f), 0f, RunStyle.Shadow, 0f);
                    mod.PlaySound("Item_103", 0.5f);
                    return true;
                }
                case Skill.Portal: return PortalCut(hero, def);
                case Skill.Assassin: return Assassin(hero, def);
                case Skill.Hook: return Hookshot(hero, def, true);
                case Skill.Pull: return Hookshot(hero, def, false);
                case Skill.Bees: return Bees(hero, def);
                case Skill.Pumpkins: return Pumpkins(hero, def);
                case Skill.Flies:
                {
                    var from = Shoulder(hero, Facing(hero));
                    var at = from + Vector2.ClampMagnitude(Aim() - from, 8f);
                    fields.Add(new Field { Kind = FieldKind.Flies, Item = def, From = at, To = at, Radius = 2.5f, Life = 4f, Every = 0.4f, Damage = Damage(def, 0.25f), Debuff = EnemyDebuffs.Confused });
                    mod.PlaySound("Item_17", 0.6f, 0.7f);
                    return true;
                }
                case Skill.Ichor: return Ichor(hero, def);
                case Skill.Frost: return Erupt(hero, def, PostKind.Ice, 5, 1.1f, 1.3f, EnemyDebuffs.Frostburn, 240);
                case Skill.Thorns: return Erupt(hero, def, PostKind.Thorn, 5, 1f, 1f, EnemyDebuffs.Poisoned, 420);
                case Skill.Eat:
                {
                    int heal = Mathf.Max(5, Mathf.RoundToInt(mod.Stats.LifeMax * 0.2f));
                    mod.Stats.Heal(heal, recovery: true);
                    Player.PlayerBuffs.Add(26, 2 * 60 * 60);
                    mod.PlaySound("Item_2", 0.8f);
                    eaten = 1f;
                    return true;
                }
                case Skill.Tax:
                    TurnToAim(hero);
                    mod.SkillCut(def, Cut("Chop"), 0.25f, 0.9f, 1f);
                    taxUntil = Time.time + 0.3f;
                    return true;
                case Skill.BloodRite:
                {
                    int cost = Mathf.Max(5, Mathf.RoundToInt(mod.Stats.LifeMax * 0.08f));
                    if (mod.Stats.Life <= cost) return false;
                    mod.Stats.Life -= cost;
                    UI.CombatText.Show(hero.transform.position + Vector3.up, cost.ToString(), UI.CombatText.PlayerHurt);
                    bloodUntil = Time.time + 8f;
                    bled = 1f;
                    mod.PlaySound("NPC_Hit_1", 0.8f, 0.8f);
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ the charged arts

        /// <summary>
        /// The greatswords' charged leap (蓄力跳劈): up from the ground, the blade raised, brought down as he lands. In the air
        /// (the user's 10-26 "能在空中生效，不要只在地面") a plunge straight down at the fastest fall instead, the blow where he lands.
        /// </summary>
        static void Slam(HeroController hero, ItemDef def, float c)
        {
            var mod = TerraMod.Instance;
            float facing = Facing(hero);
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            if (hero.cState.onGround)
            {
                rb.linearVelocity = new Vector2(facing * 2.5f * Player.TerraMovement.PxT, (5f + 2f * c) * Player.TerraMovement.PxT);
                float fall = 0.5f + 0.12f * c;
                mod.SkillCut(def, Cut("Smash"), fall / 0.6f, 2f + 2f * c, 1.5f);
                Later(fall, () => { if (hero != null) SlamLand(hero, def, facing, c); });
                return;
            }
            var feet = BladeArts.Feet(hero);
            float below = Ground(feet, 12f);
            float time = float.IsNaN(below) ? 0.3f : Mathf.Clamp((feet.y - below) / (PlungeSpeed * Player.TerraMovement.PxT), 0.12f, 1.2f);
            mod.SkillCut(def, Cut("Smash"), time / 0.6f, 2f + 2f * c, 1.5f);
            mod.StartCoroutine(Plunge(hero, rb, float.IsNaN(below) ? 0.3f : 1.4f, () => { if (hero != null) SlamLand(hero, def, facing, c); }));
        }

        /// <summary>The plunge's speed: Terraria's fastest fall (px an update).</summary>
        const float PlungeSpeed = 10f;

        /// <summary>Straight down at the fastest fall until he lands (at most a while), untouchable, no fall counted; then the blow.</summary>
        static System.Collections.IEnumerator Plunge(HeroController hero, Rigidbody2D rb, float most, System.Action land)
        {
            float until = Time.time + most;
            Player.TerraHurt.Invulnerable(hero, Mathf.CeilToInt(most * 60f) + 6);
            while (hero != null && rb != null && !hero.cState.onGround && !hero.cState.dead && Time.time < until)
            {
                rb.linearVelocity = new Vector2(0f, -PlungeSpeed * Player.TerraMovement.PxT);
                TerraMod.Instance?.ResetFall();
                yield return null;
            }
            TerraMod.Instance?.ResetFall();
            DevPlunges++;
            land();
        }

        /// <summary>Dev: the greatswords' plunges from the air.</summary>
        public static int DevPlunges;

        /// <summary>Dev: dragon fire sent off level in the air.</summary>
        public static int DevAirWaves;

        /// <summary>The leap's or the plunge's blow: on the ground ahead, or burst where he is when there is none under him.</summary>
        static void SlamLand(HeroController hero, ItemDef def, float facing, float c)
        {
            var mod = TerraMod.Instance;
            var type = def.Type;
            var feet = BladeArts.Feet(hero);
            float x = feet.x + facing * 1.2f;
            float ground = Ground(new Vector2(x, feet.y + 1f), 3f);
            bool air = float.IsNaN(ground);
            if (air) ground = feet.y;
            if (type == ItemType.FieryGreatsword)
            {
                int n = 4 + Mathf.RoundToInt(3f * c);
                if (air) EruptAir(hero, def, PostKind.Fire, n, 1.2f + 1.2f * c, EnemyDebuffs.OnFire, 300);
                else EruptFrom(def, new Vector2(x, feet.y), facing, PostKind.Fire, n, 1.2f, 1.2f + 1.2f * c, EnemyDebuffs.OnFire, 300);
            }
            else
                for (int side = -1; side <= 1; side += 2)
                    waves.Add(new Wave
                    {
                        Kind = WaveKind.Shock, Item = def, X0 = x, X = x, Y = ground, Dir = side, Range = (type == ItemType.BreakerBlade ? 3f + 4f * c : 2f + c),
                        Speed = 18f, Height = 1.4f, Damage = Damage(def, type == ItemType.BreakerBlade ? 1.5f + 1.5f * c : 0.8f), Knock = 6f, Air = air,
                    });
            if (type == ItemType.ChlorophyteClaymore)
            {
                var tp = TerraProjectiles.Instance;
                float crit = Equipment.Crit(def);
                int n = 6 + Mathf.RoundToInt(6f * c);
                for (int k = 0; k < n && tp != null; k++)
                {
                    // A fan up off the ground; all round him in the air.
                    float a = (air ? Mathf.Lerp(0f, 360f, (k + 0.5f) / n) : Mathf.Lerp(20f, 160f, (k + 0.5f) / n)) * Mathf.Deg2Rad;
                    tp.Shoot(ProjKind.SporeCloud, new Vector2(x, ground + 0.6f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (1.5f + Random.value * 2.5f), Damage(def, 0.45f), 0f, crit, melee: true);
                }
            }
            Player.TerraCamera.Punch(0.12f + 0.08f * c, 0.25f);
            mod?.PlaySound(type == ItemType.FieryGreatsword ? "Item_74" : "Item_14", 0.6f);
        }

        static void DragonWave(HeroController hero, ItemDef def, float c)
        {
            var mod = TerraMod.Instance;
            float facing = TurnToAim(hero);
            mod.SkillCut(def, Cut("HeavySweep"), 0.4f, 1.5f + c, 1.5f);
            Later(0.18f, () =>
            {
                if (hero == null) return;
                var feet = BladeArts.Feet(hero);
                float ground = Ground(new Vector2(feet.x, feet.y + 0.5f), 2f);
                // Off the ground the fire flies on level at his height.
                bool air = float.IsNaN(ground);
                if (air) DevAirWaves++;
                waves.Add(new Wave
                {
                    Kind = WaveKind.Dragon, Item = def, X0 = feet.x, X = feet.x + facing * 0.8f, Y = air ? feet.y - 0.4f : ground, Dir = facing, Air = air,
                    Range = 5f + 6f * c, Speed = 22f, Height = 2.2f, Damage = Damage(def, 2f + 2f * c), Knock = 5f, Debuff = EnemyDebuffs.Hellfire, DebuffTicks = 300,
                });
                mod.PlaySound("Item_74", 0.6f, 0.9f);
            });
        }

        /// <summary>
        /// The Revenant Edge's soul wave cut (白波斩): the sword brought round and the Vengeful Spirit's own wave (the spell's,
        /// TerraProjectiles.CastSpiritWave, bursting on the first enemy) sent from it at the cursor; charged full, three in a fan.
        /// </summary>
        static void SoulWaveCut(HeroController hero, ItemDef def, float c)
        {
            var mod = TerraMod.Instance;
            float facing = TurnToAim(hero);
            mod?.SkillCut(def, Cut("Crescent"), 0.3f, 1.5f + c, 2f);
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var from = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            var way = Aim() - from;
            way = way.sqrMagnitude < 1e-4f ? new Vector2(facing, 0f) : way.normalized;
            int n = c >= 1f ? 3 : 1;
            int dmg = Damage(def, 1.5f + 1.5f * c);
            float crit = Equipment.Crit(def), kb = Equipment.Knockback(def);
            for (int i = 0; i < n; i++)
            {
                float a = (i - (n - 1) * 0.5f) * 12f * Mathf.Deg2Rad;
                var dir = new Vector2(way.x * Mathf.Cos(a) - way.y * Mathf.Sin(a), way.x * Mathf.Sin(a) + way.y * Mathf.Cos(a));
                tp.CastSpiritWave(from, dir * 11f, dmg, kb, crit);
            }
            mod?.PlaySound("tk_spirit_cast", 0.7f);
            DevSoulWaves += n;
        }

        /// <summary>Dev: the soul waves the Revenant Edge sent.</summary>
        public static int DevSoulWaves;

        static void Execute(HeroController hero, ItemDef def, float c)
        {
            float facing = Facing(hero);
            Run(hero, def, new Vector2(facing, 0f), 2f + 2f * c, Damage(def, 2f + 2f * c), 0.05f, RunStyle.Execute, c);
            TerraMod.Instance?.PlaySound("Item_1", 0.7f, 0.9f);
        }

        static void TerraDash(HeroController hero, ItemDef def, float c)
        {
            float facing = Facing(hero);
            Run(hero, def, new Vector2(facing, 0f), 3f + 4f * c, Damage(def, 1.5f + 1.5f * c), 0.05f, RunStyle.Terra, c);
            TerraMod.Instance?.PlaySound("Item_60", 0.6f);
        }

        static void HomeRun(HeroController hero, ItemDef def, float c)
        {
            TurnToAim(hero);
            TerraMod.Instance?.SkillCut(def, Cut("HeavySweep"), 0.35f, 1.5f + 1.5f * c, 4f + 4f * c);
            homeRunUntil = Time.time + 0.4f;
            homeRunHealed.Clear();
        }

        static void Slap(HeroController hero, ItemDef def, float c)
        {
            TurnToAim(hero);
            TerraMod.Instance?.SkillCut(def, Cut("Sweep"), 0.3f, 1.2f + c, 6f + 5f * c);
            TerraMod.Instance?.PlaySound("Item_175", 0.9f);
        }

        static void Starfall(HeroController hero, ItemDef def, float c)
        {
            bool wrath = def.Type == ItemType.StarWrath;
            int n = (wrath ? 6 : 3) + Mathf.RoundToInt((wrath ? 8f : 4f) * c);
            var target = Aim();
            int dmg = Damage(def, wrath ? 1.2f : 1f);
            float crit = Equipment.Crit(def);
            var top = hero.GetComponent<BoxCollider2D>().bounds.center.y + 600f * Effects.TerraParticles.Px;
            for (int i = 0; i < n; i++)
                Later(i * 0.06f, () =>
                {
                    var at = target + new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(-0.5f, 0.5f));
                    var from = new Vector2(at.x + Random.Range(-3f, 3f), top);
                    var back = (from - at).normalized;
                    var floor = at;
                    for (int g = 0; g < 60 && floor.y < from.y && World.TerraWorld.IsSolidAt(floor); g++) floor += back * 16f * Effects.TerraParticles.Px;
                    TerraProjectiles.Instance?.ShootStar(from, (at - from).normalized * 25f, dmg, crit, floor.y);
                });
            TerraMod.Instance?.PlaySound("Item_9", 0.7f);
        }

        // ------------------------------------------------------------------ the pressed arts

        internal sealed class Portal
        {
            public Vector2 At;
            public float Age;
            /// <summary>A puff of smoke (the Psycho Knife's vanishing), not a portal.</summary>
            public bool Smoke;
        }

        internal static readonly List<Portal> portals = new List<Portal>();

        static bool PortalCut(HeroController hero, ItemDef def)
        {
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var from = (Vector2)box.center;
            var target = from + Vector2.ClampMagnitude(Aim() - from, 9f);
            Vector2? spot = null;
            for (int k = 0; k <= 6 && !spot.HasValue; k++)
            {
                var p = target + Vector2.up * 0.5f * k;
                if (Physics2D.OverlapBox(p, box.size * 0.95f, 0f, TerrainMask) == null) spot = p;
            }
            if (!spot.HasValue) return false;
            portals.Add(new Portal { At = from });
            portals.Add(new Portal { At = spot.Value });
            MoveHero(hero, spot.Value);
            Player.TerraHurt.Invulnerable(hero, 20);
            TurnToAim(hero);
            TerraMod.Instance?.SkillCut(def, Cut("Cross"), 0.3f, 1.6f, 1f);
            TerraMod.Instance?.PlaySound("Item_8", 0.6f, 0.8f);
            return true;
        }

        /// <summary>The hero's box centred on a point.</summary>
        static void MoveHero(HeroController hero, Vector2 centre)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var at = centre + ((Vector2)hero.transform.position - (Vector2)b.center);
            if (rb != null) { rb.position = at; rb.linearVelocity = Vector2.zero; }
            hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
        }

        static bool Assassin(HeroController hero, ItemDef def)
        {
            var mod = TerraMod.Instance;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            Collider2D best = null;
            float bestD = float.MaxValue;
            var aim = Aim();
            foreach (var col in Physics2D.OverlapCircleAll(box.center, 9f))
            {
                var hm = Enemy(col);
                if (hm == null || !CharmEffects.MeleeReaches(col)) continue;
                float d = ((Vector2)col.bounds.center - aim).sqrMagnitude;
                if (d < bestD) { bestD = d; best = col; }
            }
            if (best == null) return false;
            var eb = best.bounds;
            float dir = eb.center.x >= box.center.x ? 1f : -1f;
            var behind = new Vector2(eb.center.x + dir * (eb.extents.x + box.extents.x + 0.2f), eb.min.y + box.extents.y + 0.05f);
            portals.Add(new Portal { At = box.center, Smoke = true });
            if (Physics2D.OverlapBox(behind, box.size * 0.95f, 0f, TerrainMask) == null) MoveHero(hero, behind);
            else dir = -dir;
            if (dir > 0f) hero.FaceLeft(); else hero.FaceRight();
            float facing = -dir;
            Player.TerraHurt.Invulnerable(hero, 20);
            mod.Melee.HitWith(best, Damage(def, 3f), 4f, 100f, facing, hero.GetComponent<BoxCollider2D>().bounds.center);
            SwordFx.Instance?.ArtUse(def, BladeArts.Of(def), Cut("StabFlurry"), 0f, facing, 14f, Equipment.ItemScale(def), false);
            mod.PlaySound("Item_1", 0.7f, 1.5f);
            return true;
        }

        static bool Bees(HeroController hero, ItemDef def)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return false;
            var from = Shoulder(hero, Facing(hero));
            int dmg = Damage(def, 0.35f);
            float crit = Equipment.Crit(def);
            for (int k = 0; k < 8; k++)
            {
                var v = Random.insideUnitCircle.normalized * (3f + Random.value * 3f);
                tp.Shoot(k < 2 ? ProjKind.GiantBee : ProjKind.Bee, from, v, dmg + (k < 2 ? 2 : 0), 0.5f, crit, melee: true);
            }
            TerraMod.Instance?.PlaySound("Item_97", 0.6f);
            return true;
        }

        static bool Pumpkins(HeroController hero, ItemDef def)
        {
            var aim = Aim();
            var list = new List<HealthManager>();
            foreach (var col in Physics2D.OverlapCircleAll(aim, 8f))
            {
                var hm = Enemy(col);
                if (hm != null && !list.Contains(hm)) list.Add(hm);
            }
            if (list.Count == 0) return false;
            list.Sort((a, b) => ((Vector2)a.transform.position - aim).sqrMagnitude.CompareTo(((Vector2)b.transform.position - aim).sqrMagnitude));
            for (int k = 0; k < 5; k++) SwordFx.Instance?.ArtPumpkin(list[k % list.Count], Damage(def, 0.8f), 3f);
            TerraMod.Instance?.PlaySound("Item_8", 0.6f, 0.7f);
            return true;
        }

        static bool Ichor(HeroController hero, ItemDef def)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return false;
            TurnToAim(hero);
            var from = Shoulder(hero, Facing(hero));
            var way = (Aim() - from).normalized;
            if (way == Vector2.zero) way = new Vector2(Facing(hero), 0f);
            float crit = Equipment.Crit(def);
            for (int k = 0; k < 7; k++)
            {
                float a = (k - 3) * 5f * Mathf.Deg2Rad;
                var v = new Vector2(way.x * Mathf.Cos(a) - way.y * Mathf.Sin(a), way.x * Mathf.Sin(a) + way.y * Mathf.Cos(a)) * (10f + Random.value * 3f);
                tp.Shoot(ProjKind.GoldenShowerStream, from, v, Damage(def, 0.45f), 2f, crit, melee: true);
            }
            TerraMod.Instance?.PlaySound("Item_13", 0.6f);
            return true;
        }

        // ------------------------------------------------------------------ what runs along the ground

        internal enum WaveKind : byte { Shock, Dragon }

        internal sealed class Wave
        {
            public WaveKind Kind;
            public ItemDef Item;
            public float X0, X, Y, Dir, Range, Speed, Height, Knock, Age;
            public int Damage, Debuff, DebuffTicks;
            /// <summary>Sent off in the air: it runs on level (Y its foot) instead of along the ground.</summary>
            public bool Air;
            public readonly HashSet<HealthManager> Struck = new HashSet<HealthManager>();
        }

        internal static readonly List<Wave> waves = new List<Wave>();

        static void TickWaves(float dt)
        {
            var mod = TerraMod.Instance;
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                w.Age += dt;
                w.X += w.Dir * w.Speed * dt;
                float g = w.Air ? w.Y : Ground(new Vector2(w.X, w.Y + 1.2f), 2.5f);
                bool end = Mathf.Abs(w.X - w.X0) > w.Range || float.IsNaN(g) || World.TerraWorld.IsSolidAt(new Vector2(w.X, g + (w.Air ? w.Height * 0.5f : 0.6f)));
                if (end) { waves.RemoveAt(i); continue; }
                w.Y = g;
                if (mod == null) continue;
                float crit = Equipment.Crit(w.Item);
                foreach (var col in Physics2D.OverlapBoxAll(new Vector2(w.X, w.Y + w.Height * 0.5f), new Vector2(1f, w.Height), 0f))
                {
                    var hm = Enemy(col);
                    if (hm == null || !w.Struck.Add(hm)) continue;
                    int dmg = w.Damage;
                    if (w.Item.Type == ItemType.BreakerBlade)
                    {
                        var info = EnemyStats.Of(hm);
                        if (info != null && EnemyStats.Life(hm, info) >= EnemyStats.LifeMax(info) * 0.9f) dmg *= 2;
                    }
                    if (mod.Melee.HitWith(col, dmg, w.Knock, crit, w.Dir, new Vector2(w.X - w.Dir, w.Y + 0.5f)) && w.Debuff != 0) EnemyDebuffs.Add(hm, w.Debuff, w.DebuffTicks);
                }
            }
        }

        internal enum PostKind : byte { Fire, Ice, Thorn }

        internal sealed class Post
        {
            public PostKind Kind;
            public ItemDef Item;
            public Vector2 Foot;
            /// <summary>The way it grows from its foot (world, y up): up out of the ground, or out from him in the air.</summary>
            public Vector2 Way = Vector2.up;
            public float Age, Delay, Height;
            public int Damage, Debuff, DebuffTicks;
            public bool Struck, Burst;
        }

        internal static readonly List<Post> posts = new List<Post>();
        internal const float PostLife = 0.6f;

        static bool Erupt(HeroController hero, ItemDef def, PostKind kind, int count, float spacing, float scale, int debuff, int ticks)
        {
            float facing = TurnToAim(hero);
            var feet = BladeArts.Feet(hero);
            if (EruptFrom(def, new Vector2(feet.x + facing * 0.4f, feet.y), facing, kind, count, spacing, scale, debuff, ticks) == 0
                && EruptAir(hero, def, kind, count, scale, debuff, ticks) == 0) return false;
            TerraMod.Instance?.SkillCut(def, Cut("Smash"), 0.3f, 1f, 1f);
            TerraMod.Instance?.PlaySound(kind == PostKind.Ice ? "Item_28" : "Item_17", 0.6f);
            return true;
        }

        /// <summary>
        /// A row of posts out of the ground ahead (fire, ice spikes, thorns), one after another, as far as the ground goes. The
        /// first is looked for well below (20 tiles), so from the air they come up out of the ground under him.
        /// </summary>
        static int EruptFrom(ItemDef def, Vector2 feet, float facing, PostKind kind, int count, float spacing, float scale, int debuff, int ticks)
        {
            int made = 0;
            float lastGround = feet.y;
            for (int i = 0; i < count; i++)
            {
                float x = feet.x + facing * (1f + i * spacing);
                float g = Ground(new Vector2(x, lastGround + 1.2f), i == 0 ? 10f : 3f);
                if (float.IsNaN(g) || World.TerraWorld.IsSolidAt(new Vector2(x, g + 0.8f))) break;
                lastGround = g;
                posts.Add(new Post
                {
                    Kind = kind, Item = def, Foot = new Vector2(x, g), Delay = i * 0.07f, Height = PostHeight(kind),
                    Damage = Damage(def, scale), Debuff = debuff, DebuffTicks = ticks,
                });
                made++;
            }
            return made;
        }

        static float PostHeight(PostKind kind) => kind == PostKind.Fire ? 2.6f : kind == PostKind.Ice ? 2.8f : 1.75f;

        /// <summary>
        /// No ground in reach under him (the user's 10-26 "能在空中生效，不要只在地面"): the posts burst out of him instead, fanned
        /// toward the cursor, the middle first.
        /// </summary>
        static int EruptAir(HeroController hero, ItemDef def, PostKind kind, int count, float scale, int debuff, int ticks)
        {
            var from = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            var way = Aim() - from;
            way = way.sqrMagnitude < 1e-4f ? new Vector2(Facing(hero), 0f) : way.normalized;
            for (int i = 0; i < count; i++)
            {
                float off = i - (count - 1) * 0.5f;
                float a = off * 15f * Mathf.Deg2Rad;
                var dir = new Vector2(way.x * Mathf.Cos(a) - way.y * Mathf.Sin(a), way.x * Mathf.Sin(a) + way.y * Mathf.Cos(a));
                posts.Add(new Post
                {
                    Kind = kind, Item = def, Foot = from + dir * 0.4f, Way = dir, Delay = Mathf.Abs(off) * 0.05f, Height = PostHeight(kind),
                    Damage = Damage(def, scale), Debuff = debuff, DebuffTicks = ticks,
                });
            }
            DevAirPosts += count;
            return count;
        }

        /// <summary>Dev: posts sent out from the air.</summary>
        public static int DevAirPosts;

        static void TickPosts(float dt)
        {
            var mod = TerraMod.Instance;
            for (int i = posts.Count - 1; i >= 0; i--)
            {
                var p = posts[i];
                p.Age += dt;
                if (p.Age > p.Delay + PostLife) { posts.RemoveAt(i); continue; }
                if (p.Struck || p.Age < p.Delay || mod == null) continue;
                p.Struck = true;
                float crit = Equipment.Crit(p.Item);
                var done = new HashSet<HealthManager>();
                float turn = Mathf.Atan2(p.Way.y, p.Way.x) * Mathf.Rad2Deg - 90f;
                foreach (var col in Physics2D.OverlapBoxAll(p.Foot + p.Way * (p.Height * 0.5f), new Vector2(0.9f, p.Height), turn))
                {
                    var hm = Enemy(col);
                    if (hm == null || !done.Add(hm)) continue;
                    if (mod.Melee.HitWith(col, p.Damage, 3f, crit, col.bounds.center.x < p.Foot.x ? -1f : 1f, p.Foot) && p.Debuff != 0) EnemyDebuffs.Add(hm, p.Debuff, p.DebuffTicks);
                }
            }
        }

        // ------------------------------------------------------------------ what lingers

        internal enum FieldKind : byte { Terra, Flies }

        internal sealed class Field
        {
            public FieldKind Kind;
            public ItemDef Item;
            public Vector2 From, To;
            public float Radius, Life, Every, Age, Next;
            public int Damage, Debuff;
        }

        internal static readonly List<Field> fields = new List<Field>();

        static void Lay(Field f) => fields.Add(f);

        static void TickFields(float dt)
        {
            var mod = TerraMod.Instance;
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var f = fields[i];
                f.Age += dt;
                if (f.Age > f.Life) { fields.RemoveAt(i); continue; }
                if (f.Age < f.Next || mod == null) continue;
                f.Next += f.Every;
                Collider2D[] cols;
                if (f.Kind == FieldKind.Terra)
                {
                    var d = f.To - f.From;
                    cols = Physics2D.OverlapBoxAll((f.From + f.To) * 0.5f, new Vector2(d.magnitude + 1f, 1.3f), Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                }
                else cols = Physics2D.OverlapCircleAll(f.From, f.Radius);
                var done = new HashSet<HealthManager>();
                float crit = Equipment.Crit(f.Item);
                foreach (var col in cols)
                {
                    var hm = Enemy(col);
                    if (hm == null || !done.Add(hm)) continue;
                    if (mod.Melee.HitWith(col, f.Damage, 0.5f, crit, col.bounds.center.x < f.From.x ? -1f : 1f, f.From) && f.Debuff != 0) EnemyDebuffs.Add(hm, f.Debuff, 120);
                }
            }
        }

        // ------------------------------------------------------------------ the hooks

        internal sealed class Hook
        {
            public ItemDef Item;
            public Vector2 At, Way, Anchor;
            public float Traveled, Range, Age;
            public bool Self;
            /// <summary>0 going out, 1 pulling the player in, 2 coming back.</summary>
            public int State;
        }

        internal static Hook hook;

        static bool Hookshot(HeroController hero, ItemDef def, bool self)
        {
            if (hook != null) return false;
            float facing = TurnToAim(hero);
            var from = Shoulder(hero, facing);
            var way = (Aim() - from).normalized;
            if (way == Vector2.zero) way = new Vector2(facing, 0f);
            hook = new Hook { Item = def, At = from, Way = way, Range = self ? 8f : 7f, Self = self };
            TerraMod.Instance?.PlaySound("Item_1", 0.5f, 0.7f);
            return true;
        }

        static void TickHook(HeroController hero, float dt)
        {
            var h = hook;
            if (h == null) return;
            var mod = TerraMod.Instance;
            h.Age += dt;
            var home = Shoulder(hero, Facing(hero));
            if (h.State == 0)
            {
                float step = 30f * dt;
                var next = h.At + h.Way * step;
                var wall = Physics2D.Linecast(h.At, next, TerrainMask);
                if (wall.collider != null)
                {
                    h.At = wall.point;
                    if (h.Self) { h.Anchor = wall.point - h.Way * 0.6f; h.State = 1; Player.TerraHurt.Invulnerable(hero, 30); }
                    else h.State = 2;
                    return;
                }
                h.At = next;
                h.Traveled += step;
                foreach (var col in Physics2D.OverlapCircleAll(h.At, 0.45f))
                {
                    var hm = Enemy(col);
                    if (hm == null || mod == null) continue;
                    float towardPlayer = home.x < col.bounds.center.x ? -1f : 1f;
                    bool boss = EnemyStats.Of(hm)?.Boss == true;
                    mod.Melee.HitWith(col, Damage(h.Item, 1f), h.Self || boss ? 0.5f : 11f, Equipment.Crit(h.Item), h.Self ? -towardPlayer : towardPlayer, h.At);
                    if (h.Self)
                    {
                        EnemyDebuffs.Add(hm, EnemyDebuffs.TentacleSpiked, 300);
                        h.Anchor = (Vector2)col.bounds.center - h.Way * (col.bounds.extents.x + 0.7f);
                        h.State = 1;
                        Player.TerraHurt.Invulnerable(hero, 30);
                    }
                    else h.State = 2;
                    DevHooked++;
                    return;
                }
                if (h.Traveled > h.Range) h.State = 2;
            }
            else if (h.State == 1)
            {
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                var d = h.Anchor - (Vector2)b.center;
                var rb = hero.GetComponent<Rigidbody2D>();
                if (d.magnitude < 0.6f || h.Age > 1.2f)
                {
                    if (rb != null) rb.linearVelocity = d.normalized * 3f * Player.TerraMovement.PxT;
                    hook = null;
                    return;
                }
                if (rb != null) rb.linearVelocity = d.normalized * 26f;
            }
            else
            {
                h.At = Vector2.MoveTowards(h.At, home, 40f * dt);
                if ((h.At - home).sqrMagnitude < 0.3f * 0.3f) hook = null;
            }
        }

        public static int DevHooked;

        // ------------------------------------------------------------------ the guard, the rush, the blood, the taxes

        static float parryUntil, taxUntil, bloodUntil, homeRunUntil, lastSteal;
        internal static float eaten, bled;
        static ItemDef guardItem;
        static readonly HashSet<HealthManager> homeRunHealed = new HashSet<HealthManager>();
        static readonly Dictionary<int, float> taxed = new Dictionary<int, float>();
        public static int DevParries, DevTaxes, DevRushCuts;

        /// <summary>The guard is up (its moment and a breath after, for the pose).</summary>
        public static bool Guarding => guardItem != null && Time.time < parryUntil + 0.15f;
        public static ItemDef GuardItem => guardItem;
        /// <summary>The guard's moment: a blow met now is parried.</summary>
        public static bool Parrying => guardItem != null && Time.time < parryUntil;

        static void Guard(ItemDef def)
        {
            guardItem = def;
            parryUntil = Time.time + 0.32f;
            readyAt[def.Type] = Time.time + 0.32f + 0.5f;
            TerraMod.Instance?.PlaySound("Item_1", 0.4f, 1.6f);
            DevUses++;
            DevLast = Skill.Parry;
        }

        /// <summary>
        /// A blow (an enemy's touch or a shot) on the player in the guard's moment, from ahead: turned (no hurt), the player
        /// untouchable a moment, and a riposte cut at three times the blade (an Excalibur's three and a half, its light about it).
        /// </summary>
        public static bool TryParry(HeroController hero, GameObject source, HealthManager attacker)
        {
            if (!Parrying || hero == null || source == null) return false;
            var mod = TerraMod.Instance;
            if (mod == null || mod.Inventory.Current.Def != guardItem) return false;
            float facing = Facing(hero);
            var c = hero.GetComponent<BoxCollider2D>().bounds.center;
            var col = source.GetComponent<Collider2D>();
            float x = col != null ? col.bounds.center.x : source.transform.position.x;
            if (Mathf.Sign(x - c.x) != facing && Mathf.Abs(x - c.x) > 0.3f) return false;
            parryUntil = 0f;
            readyAt[guardItem.Type] = Time.time + 0.2f;
            Player.TerraHurt.Invulnerable(hero, 40);
            mod.PlaySound("NPC_Hit_4", 0.8f, 1.2f);
            bool holy = guardItem.Type != ItemType.Cutlass;
            mod.SkillCut(guardItem, Cut("Cross"), 0.28f, holy ? 3.5f : 3f, 2f);
            parried = 1f;
            DevParries++;
            Achievements.AchievementTracker.Blocked();
            return true;
        }

        internal static float parried;
        static bool rushing;
        static float rushDrift;

        static void Rush(HeroController hero, ItemDef def, bool held, float dt)
        {
            rushing = held;
            if (!held) return;
            var mod = TerraMod.Instance;
            if (mod == null) return;
            if (!mod.IsUsingItem)
            {
                TurnToAim(hero);
                mod.SkillCut(def, BladeArts.Jab(Random.Range(-25f, 25f)), 0.08f, 0.45f, 0.3f);
                DevRushCuts++;
                DevLast = Skill.Rush;
            }
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null && hero.cState.onGround && Mathf.Abs(rb.linearVelocity.x) < 1.5f * Player.TerraMovement.PxT)
                rb.linearVelocity = new Vector2(Facing(hero) * 1.5f * Player.TerraMovement.PxT, rb.linearVelocity.y);
        }

        /// <summary>A remade cut's hit (MeleeHits): the taxes, the blood rite's bleeding, the home run's sip of life, the rush's draining.</summary>
        public static void OnArtHit(ItemDef weapon, HealthManager hm, int damage)
        {
            var mod = TerraMod.Instance;
            if (weapon == null || hm == null || mod == null) return;
            switch (weapon.Type)
            {
                case ItemType.TaxCollectorsStickOfDoom:
                    if (Time.time < taxUntil && (!taxed.TryGetValue(hm.GetInstanceID(), out float at) || Time.time - at > 10f))
                    {
                        taxed[hm.GetInstanceID()] = Time.time;
                        var info = EnemyStats.Of(hm);
                        int stage = info != null ? Mathf.Clamp(info.Level, 0, 5) : 0;
                        Shop.Earn(5 * (stage + 1));
                        DevTaxes++;
                        coins = 1f;
                        coinsAt = hm.transform.position;
                        mod.PlaySound("Coins", 0.6f);
                    }
                    break;
                case ItemType.BloodButcherer:
                    if (Time.time < bloodUntil) EnemyDebuffs.Add(hm, EnemyDebuffs.BloodButchered, 540);
                    break;
                case ItemType.BatBat:
                    if (Time.time < homeRunUntil && homeRunHealed.Add(hm)) mod.Stats.Heal(3, recovery: true);
                    break;
                case ItemType.FetidBaghnakhs:
                    if (rushing && Time.time - lastSteal > 0.15f) { lastSteal = Time.time; mod.Stats.Heal(1, recovery: true); }
                    break;
            }
        }

        internal static float coins;
        internal static Vector2 coinsAt;

        /// <summary>The damage scale of a remade cut now (the blood rite's).</summary>
        public static float CutScale(ItemDef def) => def != null && def.Type == ItemType.BloodButcherer && Time.time < bloodUntil ? 1.3f : 1f;

        public static bool BloodRiteOn => Time.time < bloodUntil;

        static void TickMore(HeroController hero, ItemDef def, float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].At) continue;
                var a = pending[i].Do;
                pending.RemoveAt(i);
                try { a(); }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[右键技] {e.Message}"); }
            }
            TickWaves(dt);
            TickPosts(dt);
            TickFields(dt);
            TickHook(hero, dt);
            for (int i = portals.Count - 1; i >= 0; i--) if ((portals[i].Age += dt) > 0.45f) portals.RemoveAt(i);
            eaten = Mathf.Max(0f, eaten - dt * 2f);
            bled = Mathf.Max(0f, bled - dt * 2f);
            parried = Mathf.Max(0f, parried - dt * 3f);
            coins = Mathf.Max(0f, coins - dt * 3f);
            if (def == null || Of(def) != Skill.Rush) rushing = false;
        }

        static void ResetMore()
        {
            pending.Clear();
            waves.Clear();
            posts.Clear();
            fields.Clear();
            portals.Clear();
            hook = null;
            parryUntil = 0f;
            rushing = false;
        }
    }
}
