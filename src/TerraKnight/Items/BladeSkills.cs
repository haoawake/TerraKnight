using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade swing's right-click arts, on the weapons made for them, each from what the weapon is (the user's 10-26
    /// "武士刀和太刀是蓄力拔刀斩", "激光剑那种原力武器可以扔出去用…", "哪些武器适合右键，都做一下"): held and let go for a charge
    /// (Draw, Slam, Wave, Execute, HomeRun, Slap, TerraDash, Starfall: the weapon held in its pose, brightening, a chime when
    /// full), pressed for the rest. Their cuts go through the remade swing (TerraMod.SkillCut), their runs through Strike,
    /// what is thrown through Thrown, what runs along the ground through Wave and Post, what lingers through Field
    /// (BladeSkillsMore.cs); SwordFx draws them (SwordFxSkills.cs). Other weapons have none: a right click is what it was.
    /// </summary>
    internal static partial class BladeSkills
    {
        internal enum Skill : byte
        {
            None, Draw, Throw, Boomerang, Slam, Wave, Execute, HomeRun, Slap, Clamp, Dive, Portal, Assassin, ShadowStep, TerraDash,
            Hook, Pull, Starfall, Bees, Pumpkins, Flies, Ichor, Frost, Thorns, Parry, Eat, Tax, BloodRite, Rush, SoulWave,
            // The spears' (BladeSkillsSpears.cs), in this order.
            SpearPierce, TriThrust, SpearDash, ThunderVolley, Whirl, SpearThrow, IceRain, SporeRing, MushroomRing,
            // The held blades' (BladeSkillsSpears.cs).
            GaleDash, StarBurst,
            // Every other melee kind's (yoyos, flails and chains, thrown weapons, shortswords, lances, umbrellas, monk staffs, the
            // Zenith): the weapon's own KindArt.
            Kind,
        }

        /// <summary>
        /// The right-click art of a weapon of the other melee kinds (the user's 10-26 "继续升级剩下的这些，并把悠悠球的升级加进去"):
        /// each kind's own file registers its weapons' (RegisterArt, from its Register…Arts) with what pressing, or holding and
        /// letting go, does; the dispatcher here runs it as it runs the rest (the right button first, the charge, the cooldown).
        /// </summary>
        internal sealed class KindArt
        {
            /// <summary>Held and let go (Release) rather than pressed (Press).</summary>
            public bool Charged;
            /// <summary>A charge's full hold (s), the weapon's way while it charges (degrees from level), its charge sound (SoundMix).</summary>
            public float Full = 0.9f, Pose = -20f;
            /// <summary>
            /// Held out at the cursor while it charges, as a gun is, rather than in its set pose (the launchers, the Golem Fist and
            /// the KO Cannon: the user's 10-27 "那些炮类枪类的就应该蓄力的时候炮口枪口朝前").
            /// </summary>
            public bool Aimed;
            public string ChargeSound = "tk_blade_charge";
            /// <summary>Seconds before a pressed art can be used again (by the weapon).</summary>
            public float Cooldown;
            /// <summary>A pressed art: true when it was used.</summary>
            public System.Func<HeroController, ItemDef, bool> Press;
            /// <summary>A charged art let go, with how full the charge was (0..1).</summary>
            public System.Action<HeroController, ItemDef, float> Release;
            /// <summary>The tooltip's line, Chinese and English (the art's own: no lang.json line needed).</summary>
            public string Tip, TipEn;
            /// <summary>Its name (tests: DevLastArt).</summary>
            public string Name;
        }

        static readonly Dictionary<ItemType, KindArt> kindArts = new Dictionary<ItemType, KindArt>();
        static bool kindArtsMade;

        /// <summary>A weapon given its art (from a kind's Register…Arts).</summary>
        internal static void RegisterArt(ItemType type, KindArt art) => kindArts[type] = art;

        // Each kind's registrations, in its own file.
        static partial void RegisterYoyoArts();
        static partial void RegisterFlailArts();
        static partial void RegisterThrownArts();
        static partial void RegisterCloseArts();
        static partial void RegisterZenithArts();
        static partial void RegisterRangedArts();
        static partial void RegisterGunArts();
        static partial void RegisterBowArts();
        static partial void RegisterMagicArts();

        /// <summary>The weapon's kind art (the remade swings on), or null.</summary>
        internal static KindArt KindArtOf(ItemDef d)
        {
            if (!kindArtsMade)
            {
                kindArtsMade = true;
                RegisterYoyoArts();
                RegisterFlailArts();
                RegisterThrownArts();
                RegisterCloseArts();
                RegisterZenithArts();
                RegisterRangedArts();
                // The later bows', repeaters' and throws' own, over the shared ones.
                RegisterBowArts();
                // The guns' (their right button strengthening the left), over the ranged weapons' own.
                RegisterGunArts();
                // The magic weapons' (BladeSkillsMagic).
                RegisterMagicArts();
            }
            return d != null && WeaponRefitting.EnabledFor(d) && kindArts.TryGetValue(d.Type, out var a) ? a : null;
        }

        /// <summary>The arm's way while a kind's art has it (degrees from level for the way he faces), each kind's own; null when not.</summary>
        internal static readonly List<System.Func<float, float?>> ArmPoses = new List<System.Func<float, float?>>();

        /// <summary>Dev: the last kind art used, by its name.</summary>
        public static string DevLastArt = "";

        static readonly Dictionary<ItemType, Skill> skills = new Dictionary<ItemType, Skill>
        {
            [ItemType.Katana] = Skill.Draw, [ItemType.Muramasa] = Skill.Draw,
            [ItemType.LucyTheAxe] = Skill.Boomerang, [ItemType.Sickle] = Skill.Boomerang, [ItemType.IceSickle] = Skill.Boomerang, [ItemType.DeathSickle] = Skill.Boomerang,
            [ItemType.BreakerBlade] = Skill.Slam, [ItemType.FieryGreatsword] = Skill.Slam, [ItemType.ChlorophyteClaymore] = Skill.Slam,
            [ItemType.DD2SquireBetsySword] = Skill.Wave, [ItemType.Keybrand] = Skill.Execute, [ItemType.BatBat] = Skill.HomeRun,
            [ItemType.SlapHand] = Skill.Slap, [ItemType.WaffleIron] = Skill.Clamp, [ItemType.RevenantEdge] = Skill.SoulWave,
            [ItemType.FalconBlade] = Skill.Dive, [ItemType.InfluxWaver] = Skill.Portal, [ItemType.PsychoKnife] = Skill.Assassin,
            [ItemType.LightsBane] = Skill.ShadowStep, [ItemType.TerraBlade] = Skill.TerraDash,
            [ItemType.TentacleSpike] = Skill.Hook, [ItemType.CandyCaneSword] = Skill.Pull,
            [ItemType.Starfury] = Skill.Starfall, [ItemType.StarWrath] = Skill.Starfall, [ItemType.BeeKeeper] = Skill.Bees,
            [ItemType.TheHorsemansBlade] = Skill.Pumpkins, [ItemType.Flymeal] = Skill.Flies, [ItemType.Bladetongue] = Skill.Ichor,
            [ItemType.IceBlade] = Skill.Frost, [ItemType.Frostbrand] = Skill.Frost, [ItemType.BladeofGrass] = Skill.Thorns,
            [ItemType.Cutlass] = Skill.Parry, [ItemType.Excalibur] = Skill.Parry, [ItemType.TrueExcalibur] = Skill.Parry,
            [ItemType.HamBat] = Skill.Eat, [ItemType.TaxCollectorsStickOfDoom] = Skill.Tax, [ItemType.BloodButcherer] = Skill.BloodRite,
            [ItemType.BladedGlove] = Skill.Rush, [ItemType.FetidBaghnakhs] = Skill.Rush,
        };

        public static Skill Of(ItemDef d)
        {
            var spear = SpearSkill(d);
            if (spear != Skill.None) return spear;
            if (KindArtOf(d) != null) return Skill.Kind;
            if (d == null || !BladeArts.Active(d)) return Skill.None;
            if (skills.TryGetValue(d.Type, out var s)) return s;
            return BladeArts.Of(d).Kind == BladeArts.Kind.Light ? Skill.Throw : Skill.None;
        }

        static bool Charged(Skill s) => s == Skill.Draw || s == Skill.Slam || s == Skill.Wave || s == Skill.Execute || s == Skill.HomeRun
                                        || s == Skill.Slap || s == Skill.TerraDash || s == Skill.Starfall || s == Skill.SoulWave
                                        || s == Skill.SpearPierce || s == Skill.TriThrust || s == Skill.SpearDash || s == Skill.ThunderVolley || s == Skill.GaleDash;

        /// <summary>The weapon's right-click art is held and let go (a charge), the remade swings on.</summary>
        internal static bool ChargedArt(ItemDef d)
        {
            var s = Of(d);
            return s == Skill.Kind ? KindArtOf(d)?.Charged == true : s != Skill.None && Charged(s);
        }

        /// <summary>A charge's full hold (s).</summary>
        static float FullCharge(Skill s) => s == Skill.Kind && kindArts.TryGetValue(chargeItem, out var a) ? a.Full : s == Skill.Draw ? 1f : s == Skill.Starfall ? 1.1f : s == Skill.Slam || s == Skill.Wave ? 1.2f : 0.9f;

        /// <summary>The weapon's way while it charges (degrees from level): the katana at the hip, the stars' sword raised, a thrust drawn back, the rest overhead.</summary>
        static float PoseAngle(Skill s) => s == Skill.Kind && kindArts.TryGetValue(chargeItem, out var a) ? a.Pose : s == Skill.Draw ? -150f : s == Skill.Starfall ? 95f
            : s == Skill.Execute || s == Skill.TerraDash || IsSpearSkill(s) ? -20f : 120f;

        /// <summary>Seconds before a pressed art can be used again.</summary>
        static float Cooldown(Skill s)
        {
            switch (s)
            {
                case Skill.Clamp: case Skill.Dive: case Skill.ShadowStep: return 0.8f;
                case Skill.Portal: case Skill.Hook: case Skill.Pull: return 1.1f;
                case Skill.Assassin: case Skill.Ichor: return 2f;
                case Skill.Frost: case Skill.Thorns: return 2.5f;
                case Skill.Bees: return 3f;
                case Skill.Pumpkins: return 4f;
                case Skill.Flies: return 5f;
                case Skill.BloodRite: return 12f;
                case Skill.Whirl: return 1.5f;
                case Skill.SpearThrow: return 1.2f;
                case Skill.MushroomRing: case Skill.StarBurst: return 2.5f;
                case Skill.IceRain: case Skill.SporeRing: return 3f;
                case Skill.Eat: return 30f;
                case Skill.Tax: return 0.5f;
                default: return 0f;
            }
        }

        /// <summary>When each weapon's art is ready again (by the weapon: one's cooldown isn't another's).</summary>
        static readonly Dictionary<ItemType, float> readyAt = new Dictionary<ItemType, float>();
        static bool Ready(ItemDef d) => d == null || !readyAt.TryGetValue(d.Type, out float t) || Time.time >= t;

        public static int DevDraws, DevDrawHits, DevThrows, DevRecalls, DevSaberHits, DevUses;
        /// <summary>Dev: the arts used, by name, the last one.</summary>
        public static Skill DevLast;
        /// <summary>Dev: the right button held / pressed for a test (null: the real one).</summary>
        public static bool? DevHeld;
        public static bool DevPress;

        static int TerrainMask => (1 << 8) | (1 << World.ChunkColliders.TerrainLayer);

        public static void Tick(HeroController hero, ItemDef def, bool pressed, bool held, bool usingItem)
        {
            if (DevHeld.HasValue) held = DevHeld.Value;
            pressed |= DevPress;
            DevPress = false;
            if (hero == null) { StopCharge(); return; }
            int uses = DevUses, throws = DevThrows;
            bool wasCharging = Charging;
            float dt = Time.deltaTime;
            TickStrike(hero, dt);
            TickThrown(hero, def, dt);
            TickMore(hero, def, dt);
            // A gun's aim or held stance (BladeSkillsGuns.cs).
            GunTick(hero, def, held, GameHooks.PlayerHasControl() && !hero.cState.dead);
            MageFire.Tick(hero);
            var skill = Of(def);
            var kind = skill == Skill.Kind ? KindArtOf(def) : null;
            bool charged = kind != null ? kind.Charged : Charged(skill);
            bool control = GameHooks.PlayerHasControl() && !hero.cState.dead;
            if (charged && control && strike == null)
            {
                if (!Charging && pressed)
                {
                    // The right button first: a swing of the left's under way gives way to the charge.
                    if (usingItem) TerraMod.Instance?.CutSwing();
                    chargeAt = Time.time;
                    chargeItem = def.Type;
                    chargeSkill = skill;
                    fullCued = false;
                    chargeSound = kind != null ? kind.ChargeSound : ChargeSound(skill);
                    TerraMod.Instance?.PlaySound(chargeSound, 0.7f);
                }
                if (Charging && chargeItem != def.Type) StopCharge();
                // A launcher charging turns with the cursor, held out at it.
                if (ChargeAimed) TurnToAim(hero);
                if (Charging && held && !fullCued && Charge >= 1f)
                {
                    fullCued = true;
                    TerraMod.Instance?.PlaySound("MaxMana", 0.45f);
                }
                if (Charging && !held)
                {
                    float c = Charge;
                    bool go = ChargeHeld >= ChargeMin;
                    StopCharge();
                    if (go) Release(hero, def, chargeSkill, c);
                }
            }
            else if (Charging) StopCharge();
            if (!control) return;
            if (skill == Skill.Throw)
            {
                if (pressed)
                {
                    if (thrown == null) { if (usingItem) TerraMod.Instance?.CutSwing(); Throw(hero, def, true); }
                    else if (thrown != null && thrown.State != ThrownState.Returning) thrown.State = ThrownState.Returning;
                }
            }
            else if (skill == Skill.Boomerang)
            {
                if (pressed && thrown == null) { if (usingItem) TerraMod.Instance?.CutSwing(); Throw(hero, def, false); }
            }
            else if (skill == Skill.Rush) Rush(hero, def, held, dt);
            else if (skill == Skill.Parry)
            {
                if (pressed && Ready(def) && Time.time >= parryUntil && CutFirst(usingItem)) Guard(def);
            }
            else if (!charged && skill != Skill.None && pressed && Ready(def) && CutFirst(usingItem) && Press(hero, def, skill))
            {
                readyAt[def.Type] = Time.time + (kind != null ? kind.Cooldown : Cooldown(skill));
                DevUses++;
                DevLast = skill;
            }
            // Pressed while it still cools down: its time left over the player.
            if (pressed && !charged && skill != Skill.None && DevUses == uses && !Ready(def)) ShowCooldown(hero, def);
            // An art let go or pressed (a throw, a parry): the left waits out the art's recovery, then carries on.
            if (wasCharging && !Charging || DevUses != uses || DevThrows != throws) LatchLeft();
        }

        /// <summary>
        /// The pose: the arm toward what is thrown, the weapon in its charging pose, out through a run, raised in a guard;
        /// held: the weapon in the hand drawn by SwordFx (not the hand's item).
        /// </summary>
        public static bool Pose(HeroController hero, float facing, out float deg)
        {
            deg = 0f;
            if (thrown != null)
            {
                var d = thrown.At - Shoulder(hero, facing);
                deg = Mathf.Atan2(d.y, Mathf.Abs(d.x) < 1e-3f ? 1e-3f : d.x * facing) * Mathf.Rad2Deg;
                return true;
            }
            if (Charging) { deg = ChargeAngle(hero, facing); return true; }
            if (strike != null && strike.Age < DashTime + strike.Delay) { deg = strike.Way.y * 60f - 5f; return true; }
            if (Guarding) { deg = 70f; return true; }
            if (hook != null) { var d = hook.At - Shoulder(hero, facing); deg = Mathf.Atan2(d.y, Mathf.Abs(d.x) < 1e-3f ? 1e-3f : d.x * facing) * Mathf.Rad2Deg; return true; }
            // A spear art's spear out: the arm along it.
            if (TerraProjectiles.Instance != null && TerraProjectiles.Instance.SkillSpearAngle(facing, out deg)) return true;
            foreach (var pose in ArmPoses)
                if (pose(facing) is float k) { deg = k; return true; }
            return false;
        }

        static Vector2 Shoulder(HeroController hero, float facing) => BladeArts.Feet(hero) + new Vector2(-facing, 25f) * Effects.TerraParticles.Px;

        static float Facing(HeroController hero) => hero.cState.facingRight ? 1f : -1f;

        /// <summary>The cursor (the dev's aim in a test).</summary>
        static Vector2 Aim()
        {
            var mod = TerraMod.Instance;
            return mod == null ? Vector2.zero : mod.DevAim ?? mod.AimPoint;
        }

        /// <summary>Turned toward the cursor; its side (+1 right).</summary>
        static float TurnToAim(HeroController hero)
        {
            float facing = Aim().x < hero.transform.position.x ? -1f : 1f;
            if (facing > 0f) hero.FaceRight(); else hero.FaceLeft();
            return facing;
        }

        // ------------------------------------------------------------------ the charge

        const float ChargeMin = 0.25f;
        static float chargeAt = -1f;
        static ItemType chargeItem;
        static Skill chargeSkill;
        static bool fullCued;

        public static bool Charging => chargeAt >= 0f;

        /// <summary>
        /// The left button held off: an art charging, the claws' rush going, a guard up, a run under way, or the art just gone
        /// off (its recovery).
        /// </summary>
        public static bool HoldsLeft => Charging || rushing || Guarding || Time.time < leftHoldUntil || strike != null && strike.Age < DashTime + strike.Delay;

        /// <summary>
        /// After an art charged or went off, the left button waits this long and then carries on as it was (held, it uses again by
        /// itself): the art isn't joined by the left's use the moment it is loosed (the user's 10-27 "右键蓄力的时候左键还按着就会
        /// 触发双倍动画双倍伤害"), and isn't dead after it either ("如果左键还按着不应该失效而是回到左键的设计流程"). The left's
        /// own cooldown runs on through the art (TerraMod.CutSwing keeps it): a right click can't hurry the left.
        /// </summary>
        const float LeftRecovery = 0.3f;
        static float leftHoldUntil = -1f;
        public static int DevLeftLatches;

        /// <summary>Dev (suite leftlatch): the left button not held off after an art at all, to compare.</summary>
        public static bool DevNoLatch;

        static void LatchLeft()
        {
            if (DevNoLatch) return;
            if (Time.time + LeftRecovery > leftHoldUntil + 0.05f) DevLeftLatches++;
            leftHoldUntil = Mathf.Max(leftHoldUntil, Time.time + LeftRecovery);
        }

        static float cooldownShownAt = -1f;
        public static int DevCooldownShown;
        public static string DevLastCooldown = "";

        /// <summary>
        /// A right click on an art still cooling down: its seconds left popped over the player (the user's 10-27 "所有武器的右键还没
        /// 冷却的时候就应该在角色身上弹出cd时间"), as the Shade Rift Rifle's blink pops its own; not more than one every 0.35 s.
        /// </summary>
        static void ShowCooldown(HeroController hero, ItemDef def)
        {
            if (hero == null || def == null || !readyAt.TryGetValue(def.Type, out float at) || Time.time - cooldownShownAt < 0.35f) return;
            float left = at - Time.time;
            if (left <= 0f) return;
            cooldownShownAt = Time.time;
            DevLastCooldown = Lang.F("冷却 {0} 秒", left.ToString(left < 10f ? "0.0" : "0"));
            UI.CombatText.Show(hero.transform.position + Vector3.up * 1.2f, DevLastCooldown, new Color(0.72f, 0.8f, 1f));
            DevCooldownShown++;
        }

        /// <summary>Seconds before the weapon's art can be used again (0 when ready).</summary>
        internal static float CooldownLeft(ItemDef d) => d != null && readyAt.TryGetValue(d.Type, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

        /// <summary>A swing of the left's under way cut off for a pressed art (always true, for the condition it sits in).</summary>
        static bool CutFirst(bool usingItem)
        {
            if (usingItem) TerraMod.Instance?.CutSwing();
            return true;
        }
        static string chargeSound;

        /// <summary>The charge's sound (Terraria.SoundMix): the katana's bright, the greatswords' low, the star swords' with a chime.</summary>
        static string ChargeSound(Skill s) => s == Skill.Draw ? "tk_iai_charge" : s == Skill.Slam || s == Skill.Wave ? "tk_heavy_charge" : s == Skill.Starfall ? "tk_star_charge" : "tk_blade_charge";

        /// <summary>The charge let go or dropped: its sound cut.</summary>
        static void StopCharge()
        {
            chargeAt = -1f;
            if (chargeSound != null) TerraMod.Instance?.StopSound(chargeSound);
            chargeSound = null;
        }
        public static ItemType ChargeItem => chargeItem;
        public static Skill ChargeSkill => chargeSkill;
        public static float ChargePose => PoseAngle(chargeSkill);
        /// <summary>The art charging is held out at the cursor (KindArt.Aimed).</summary>
        public static bool ChargeAimed => Charging && chargeSkill == Skill.Kind && kindArts.TryGetValue(chargeItem, out var a) && a.Aimed;

        /// <summary>The charging weapon's way (degrees from level for the way he faces): at the cursor for an aimed one, else its pose.</summary>
        public static float ChargeAngle(HeroController hero, float facing)
        {
            if (!ChargeAimed || hero == null) return ChargePose;
            var d = Aim() - Shoulder(hero, facing);
            return Mathf.Atan2(d.y, Mathf.Abs(d.x) < 1e-3f ? 1e-3f : d.x * facing) * Mathf.Rad2Deg;
        }
        /// <summary>How full the charge is (0 at the least hold that lets go, 1 at its full hold).</summary>
        public static float Charge => Charging ? Mathf.Clamp01((Time.time - chargeAt - ChargeMin) / (FullCharge(chargeSkill) - ChargeMin)) : 0f;
        static float ChargeHeld => Charging ? Time.time - chargeAt : 0f;

        static void Release(HeroController hero, ItemDef def, Skill skill, float c)
        {
            DevUses++;
            DevLast = skill;
            switch (skill)
            {
                case Skill.Draw: Draw(hero, def, c); break;
                case Skill.Slam: Slam(hero, def, c); break;
                case Skill.Wave: DragonWave(hero, def, c); break;
                case Skill.Execute: Execute(hero, def, c); break;
                case Skill.HomeRun: HomeRun(hero, def, c); break;
                case Skill.Slap: Slap(hero, def, c); break;
                case Skill.TerraDash: TerraDash(hero, def, c); break;
                case Skill.Starfall: Starfall(hero, def, c); break;
                case Skill.SoulWave: SoulWaveCut(hero, def, c); break;
                default:
                    if (IsSpearSkill(skill)) SpearRelease(hero, def, skill, c);
                    else if (skill == Skill.Kind && kindArts.TryGetValue(def.Type, out var art) && art.Release != null)
                    {
                        DevLastArt = art.Name;
                        art.Release(hero, def, c);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ runs (the draw, the dive, the shadow step, the terra run, the execution)

        internal enum RunStyle : byte { Draw, Dive, Shadow, Terra, Execute }

        internal sealed class Strike
        {
            public ItemDef Item;
            public Vector2 From, To, Way;
            public float Facing, Charge, Age, Delay;
            public int Damage;
            public bool Cut, Shed;
            public RunStyle Style;
        }

        /// <summary>The run under way and the runs' lines still fading (SwordFx draws them).</summary>
        internal static Strike strike;
        internal static readonly List<Strike> lines = new List<Strike>();

        /// <summary>Where a run's cut struck an enemy: a cross of light there a moment (SwordFx).</summary>
        internal sealed class Mark
        {
            public Vector2 At;
            public RunStyle Style;
            public ItemDef Item;
            public float Age, Spin;
        }

        internal static readonly List<Mark> marks = new List<Mark>();
        internal const float DashTime = 0.09f, LineLife = 0.45f;

        /// <summary>A run from the player along a way (unit), that far at most (a wall stops it), untouchable; its cut after delay along it.</summary>
        static Strike Run(HeroController hero, ItemDef def, Vector2 way, float far, int damage, float delay, RunStyle style, float charge)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var hit = Physics2D.BoxCast(b.center, b.size * 0.9f, 0f, way, far, TerrainMask);
            if (hit.collider != null) far = Mathf.Max(0f, hit.distance - 0.05f);
            var s = new Strike
            {
                Item = def, From = b.center, To = (Vector2)b.center + way * far, Way = way, Facing = way.x < 0f ? -1f : 1f, Charge = charge,
                Damage = Mathf.Max(1, damage), Delay = delay, Style = style,
            };
            strike = s;
            lines.Add(s);
            Player.TerraHurt.Invulnerable(hero, 30);
            return s;
        }

        static int Damage(ItemDef def, float scale) => Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def) * scale * BladeArts.Of(def).Boost));

        static void Draw(HeroController hero, ItemDef def, float c)
        {
            // Along the way the player faces (the user's 10-26 "拔刀斩这种的位移方向应该按照人物朝向而不是鼠标方向").
            float facing = Facing(hero);
            Run(hero, def, new Vector2(facing, 0f), 2f + 3f * c, Damage(def, 2f + 3f * c), 0.1f, RunStyle.Draw, c);
            TerraMod.Instance?.PlaySound(def.UseSound ?? "Item_1", 0.7f, 1.25f);
            DevDraws++;
        }

        static void TickStrike(HeroController hero, float dt)
        {
            for (int i = marks.Count - 1; i >= 0; i--) if ((marks[i].Age += dt) > 0.35f) marks.RemoveAt(i);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                lines[i].Age += dt;
                if (lines[i].Age > DashTime + lines[i].Delay + LineLife) lines.RemoveAt(i);
            }
            var s = strike;
            if (s == null) return;
            var rb = hero.GetComponent<Rigidbody2D>();
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            Vector2 offset = (Vector2)hero.transform.position - (Vector2)b.center;
            if (s.Age <= DashTime && rb != null)
            {
                var at = Vector2.Lerp(s.From, s.To, Mathf.Clamp01(s.Age / DashTime)) + offset;
                rb.position = at;
                hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
            }
            else if (!s.Cut && rb != null && s.Age - dt <= DashTime)
                rb.linearVelocity = new Vector2(s.Way.x * 3f * Player.TerraMovement.PxT, Mathf.Max(0f, s.Way.y) * 4f * Player.TerraMovement.PxT);
            if (!s.Cut && s.Age >= DashTime + s.Delay)
            {
                s.Cut = true;
                RunCut(hero, s);
            }
            if (s.Cut) strike = null;
        }

        /// <summary>A run's cut along its way: every enemy on it struck once (an execution harder on the hurt, the terra run leaving its field).</summary>
        static void RunCut(HeroController hero, Strike s)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            var centre = (s.From + s.To) * 0.5f;
            var d = s.To - s.From;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var size = new Vector2(d.magnitude + 1.4f, 1.9f);
            var done = new HashSet<HealthManager>();
            float crit = Equipment.Crit(s.Item);
            foreach (var col in Physics2D.OverlapBoxAll(centre, size, angle))
            {
                var hm = Enemy(col);
                if (hm == null || !done.Add(hm)) continue;
                int dmg = s.Damage;
                if (s.Style == RunStyle.Execute)
                {
                    var info = EnemyStats.Of(hm);
                    float frac = info != null ? Mathf.Clamp01(EnemyStats.Life(hm, info) / (float)Mathf.Max(1, EnemyStats.LifeMax(info))) : 1f;
                    dmg = Mathf.RoundToInt(dmg * (1f + 2f * (1f - frac)));
                }
                if (mod.Melee.HitWith(col, dmg, 7f, crit, s.Facing, s.From)) { DevDrawHits++; marks.Add(new Mark { At = col.bounds.center, Style = s.Style, Item = s.Item, Spin = Random.value * Mathf.PI }); }
            }
            if (s.Style == RunStyle.Terra) Lay(new Field { Kind = FieldKind.Terra, Item = s.Item, From = s.From, To = s.To, Life = 2f, Every = 0.3f, Damage = Damage(s.Item, 0.6f), Debuff = 0 });
            mod.PlaySound(s.Style == RunStyle.Draw ? "Item_71" : s.Style == RunStyle.Terra ? "Item_60" : "Item_1", 0.5f, 1.3f);
        }

        /// <summary>An enemy (or the Target Dummy) a collider belongs to, alive; null for anything else.</summary>
        static HealthManager Enemy(Collider2D col)
        {
            var hm = col != null ? col.GetComponentInParent<HealthManager>() : null;
            if (hm == null || hm.isDead || hm.hp <= 0) return null;
            return UI.EnemyHover.RecordOf(hm) != null || World.TargetDummy.Is(hm) ? hm : null;
        }

        // ------------------------------------------------------------------ what is thrown (the phase blades, Lucy, the sickles)

        internal enum ThrownState : byte { Flying, Hanging, Returning }

        internal sealed class Thrown
        {
            public ItemDef Item;
            public Vector2 At, Target;
            public ThrownState State;
            public bool Hangs;
            public float Spin, HangAt, NextTouch;
            public string Scene;
            public readonly HashSet<HealthManager> Struck = new HashSet<HealthManager>();
        }

        internal static Thrown thrown;
        const float ThrowSpeed = 26f, ReturnSpeed = 30f, ThrowRange = 6f, HangTime = 9f, LeashRange = 14f;

        /// <summary>Where a thrown phase blade of this item hangs (its cuts turn there), null when none does.</summary>
        public static Vector2? RemotePivot(ItemDef d) =>
            thrown != null && thrown.Hangs && thrown.State == ThrownState.Hanging && d != null && thrown.Item.Type == d.Type ? thrown.At : (Vector2?)null;

        /// <summary>The left button is the thrown blade's while it is out (it cuts with it only where it hangs).</summary>
        public static bool TakesLeft(ItemDef d) => thrown != null && d != null && thrown.Item.Type == d.Type;

        /// <summary>The hanging blade is cutting now (drawn by its cut, not turning on its own).</summary>
        public static bool SaberCutting
        {
            get
            {
                var mod = TerraMod.Instance;
                return thrown != null && thrown.State == ThrownState.Hanging && mod != null && mod.IsUsingItem;
            }
        }

        static void Throw(HeroController hero, ItemDef def, bool hangs)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            float facing = TurnToAim(hero);
            var from = Shoulder(hero, facing);
            float range = hangs ? ThrowRange : 7f;
            var to = from + Vector2.ClampMagnitude(Aim() - from, range);
            if (!hangs) to = from + (to - from).normalized * range;
            var hit = Physics2D.Linecast(from, to, TerrainMask);
            if (hit.collider != null) to = hit.point - (to - from).normalized * 0.4f;
            thrown = new Thrown { Item = def, At = from, Target = to, State = ThrownState.Flying, Hangs = hangs, Scene = GameManager.instance != null ? GameManager.instance.sceneName : null };
            mod.PlaySound(def.UseSound ?? "Item_15", 0.7f, hangs ? 0.8f : 0.9f);
            if (def.Type == ItemType.LucyTheAxe) LucySays(from, "LucyTheAxe_ThrownAway", 5);
            DevThrows++;
            DevUses++;
            DevLast = hangs ? Skill.Throw : Skill.Boomerang;
        }

        /// <summary>Lucy talks (Terraria's own lines for her), over her.</summary>
        static void LucySays(Vector2 at, string group, int lines)
        {
            string text = Terraria.TerrariaText.Get(Lang.Culture, group + "." + Random.Range(1, lines + 1));
            if (!string.IsNullOrEmpty(text)) UI.CombatText.Show(at + Vector2.up * 1.2f, text, new Color(1f, 0.55f, 0.75f));
        }

        static void TickThrown(HeroController hero, ItemDef def, float dt)
        {
            var s = thrown;
            if (s == null) return;
            var mod = TerraMod.Instance;
            if (mod == null || hero.cState.dead || (GameManager.instance != null && GameManager.instance.sceneName != s.Scene)) { thrown = null; return; }
            var home = Shoulder(hero, Facing(hero));
            if (def == null || def.Type != s.Item.Type) s.State = ThrownState.Returning;
            s.Spin += dt * (s.State == ThrownState.Hanging ? (SaberCutting ? 0f : 300f) : 1500f);
            float damage = Damage(s.Item, s.Hangs ? 0.8f : 1.1f);
            switch (s.State)
            {
                case ThrownState.Flying:
                    s.At = Vector2.MoveTowards(s.At, s.Target, ThrowSpeed * dt);
                    Touch(s, damage, 4f);
                    if ((s.At - s.Target).sqrMagnitude < 1e-4f)
                    {
                        s.State = s.Hangs ? ThrownState.Hanging : ThrownState.Returning;
                        s.HangAt = Time.time;
                        s.Struck.Clear();
                    }
                    break;
                case ThrownState.Hanging:
                    if (Time.time - s.HangAt > HangTime || (s.At - home).sqrMagnitude > LeashRange * LeashRange) { s.State = ThrownState.Returning; s.Struck.Clear(); break; }
                    if (!SaberCutting && Time.time >= s.NextTouch)
                    {
                        s.NextTouch = Time.time + 0.45f;
                        s.Struck.Clear();
                        Touch(s, damage * 0.45f, 1.5f);
                    }
                    break;
                case ThrownState.Returning:
                    s.At = Vector2.MoveTowards(s.At, home, ReturnSpeed * dt);
                    Touch(s, damage, 4f);
                    if ((s.At - home).sqrMagnitude < 0.5f * 0.5f)
                    {
                        thrown = null;
                        DevRecalls++;
                        mod.PlaySound("Grab", 0.5f);
                        if (s.Item.Type == ItemType.LucyTheAxe) LucySays(home, "LucyTheAxe_PickedUp", 3);
                    }
                    break;
            }
        }

        /// <summary>What the turning weapon touches struck (once a leg, or once a beat while it hangs): the ice sickle's frost, the death sickle's draining.</summary>
        static void Touch(Thrown s, float damage, float knockback)
        {
            var mod = TerraMod.Instance;
            float radius = SwordFx.BladeLength(s.Item, Equipment.ItemScale(s.Item)) * BladeArts.Of(s.Item).Reach * 0.5f * Effects.TerraParticles.Px + 0.2f;
            float crit = Equipment.Crit(s.Item);
            foreach (var col in Physics2D.OverlapCircleAll(s.At, radius))
            {
                var hm = Enemy(col);
                if (hm == null || s.Struck.Contains(hm)) continue;
                s.Struck.Add(hm);
                float side = col.bounds.center.x < s.At.x ? -1f : 1f;
                if (!mod.Melee.HitWith(col, Mathf.Max(1, Mathf.RoundToInt(damage)), knockback, crit, side, s.At)) continue;
                DevSaberHits++;
                if (s.Item.Type == ItemType.IceSickle) EnemyDebuffs.Add(hm, EnemyDebuffs.Frostburn, 240);
                if (s.Item.Type == ItemType.DeathSickle) mod.Stats.Heal(Mathf.Max(1, Mathf.RoundToInt(damage * 0.05f)), recovery: true);
            }
        }

        /// <summary>A scene left, a death: nothing of an art left over.</summary>
        public static void Reset()
        {
            StopCharge();
            strike = null;
            lines.Clear();
            marks.Clear();
            thrown = null;
            ResetMore();
        }
    }
}
