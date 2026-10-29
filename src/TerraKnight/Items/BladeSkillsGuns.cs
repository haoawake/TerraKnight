using System.Collections.Generic;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;
using Offshoot = TerraKnight.Items.TerraProjectiles.Offshoot;
using SpawnWay = TerraKnight.Items.TerraProjectiles.SpawnWay;

namespace TerraKnight.Items
{
    /// <summary>
    /// The guns' right button (the user's 10-27 "枪的右键不应该松开右键发射，而是应该使下一次的左键得到加强，依旧用左键射出，比如蓄力
    /// 瞄准的枪械按住右键再点左键发射"): it makes the left's next shots stronger, and the left fires them. An aiming gun (Aim) is
    /// held out at the cursor while the right is down, a sight drawn to it, steadying over its Full time (glowing once
    /// steady); a left click then fires its aimed shot, stronger the steadier, and the aim starts again (let go of the right,
    /// nothing is fired). A loading gun (Load) takes special rounds on a right click, its next left shots firing them. A gun
    /// with a stance (Stance) takes it for some seconds on a right click, every left shot in it changed; one held (Hold) has
    /// it while the right is down. Each shot its own projectiles and particles (a slug's smoke, the Onyx Blaster's shadow
    /// crystal bursting into shards, a phoenix round's fire, the sniper's tracer). The bows, the thrown weapons and the rest
    /// keep their own arts (BladeSkillsRanged.cs).
    /// </summary>
    internal static partial class BladeSkills
    {
        internal enum GunMode : byte { Aim, Load, Stance, Hold }

        /// <summary>A gun's strengthened shot: the left's own shot (its ammo spent), how strong (an aim's steadiness, else 1), which of the run.</summary>
        internal delegate void GunShot(in ShootPatterns.Shot s, float power, int n);

        internal sealed class GunArt
        {
            public GunMode Mode;
            public string Name, Tip, TipEn;
            /// <summary>Aim: seconds to steady. Load: rounds. Stance: seconds. Every: a stance's every Nth shot changed.</summary>
            public float Full = 1f, Seconds = 4f, Cooldown;
            public int Rounds = 1, Every = 1;
            /// <summary>A stance's (or held one's) use time × this.</summary>
            public float UseScale = 1f;
            public GunShot Shot;
            /// <summary>Aim: a sure critical hit once steady; the steady shot's worth against the plain shots its wait would have fired.</summary>
            public bool Sure;
            public float Worth = 1.4f;
            /// <summary>A held gun's stance (its shots its own, the left's use firing once): something done every Pulse seconds while it is used.</summary>
            public float Pulse;
            public System.Action<HeroController, ItemDef> OnPulse;
        }

        static readonly Dictionary<ItemType, GunArt> gunArts = new Dictionary<ItemType, GunArt>();

        static ItemType gunItem;
        static int gunRounds, gunShots;
        static float gunStanceUntil = -1f, aimAt = -1f, pulseAt;
        static bool gunHolding, aimFullCued;

        /// <summary>Dev: strengthened shots fired, loads taken, stances begun, aims begun.</summary>
        public static int DevGunShots, DevGunLoads, DevGunStances, DevGunAims;
        public static string DevLastGun = "";

        internal static GunArt GunArtOf(ItemDef d) => d != null && RangedArts.Active(d) && gunArts.TryGetValue(d.Type, out var a) ? a : null;

        /// <summary>
        /// An aimed gun's shot once steady, × its damage: the plain shots the aim's wait would have fired (Full against its use
        /// time), Worth times over, its sure critical hit counted in (the user's 10-27 "狙击步枪…每一次伤害都是五倍…太超标了").
        /// </summary>
        internal static float AimMost(ItemDef def, GunArt a)
        {
            float shots = a.Full / Mathf.Max(1f / 60f, def.UseAnimation);
            float crit = Mathf.Clamp01((def.Crit + 4f) / 100f);
            return Mathf.Max(1.3f, a.Worth * shots * (a.Sure ? (1f + crit) / 2f : 1f));
        }

        /// <summary>An aimed shot's × for an aim p of the way to steady: from 1 as p² up to AimMost, so a shot let off early is barely stronger than a plain one.</summary>
        static float AimK(in ShootPatterns.Shot s, float p)
        {
            var a = GunArtOf(s.Def);
            return a == null ? 1f : 1f + (AimMost(s.Def, a) - 1f) * p * p;
        }

        static float AimCharge(GunArt a) => aimAt < 0f ? 0f : Mathf.Clamp01((Time.time - aimAt) / Mathf.Max(0.1f, a.Full));

        /// <summary>The gun in hand is being aimed (its right held): which and how steady.</summary>
        internal static bool GunAiming(out ItemDef def, out float charge)
        {
            def = TerraMod.Instance?.Inventory.Current.Def;
            charge = 0f;
            var a = GunArtOf(def);
            if (a == null || a.Mode != GunMode.Aim || aimAt < 0f) return false;
            charge = AimCharge(a);
            return true;
        }

        /// <summary>A held stance on now (its right held), or a timed one, for the gun.</summary>
        static bool StanceOn(ItemDef d, GunArt a) =>
            a.Mode == GunMode.Hold ? gunHolding && gunItem == d.Type && Ready(d) : a.Mode == GunMode.Stance && gunItem == d.Type && Time.time < gunStanceUntil;

        /// <summary>The use time × a gun stance's (RangedArts.UseScale).</summary>
        internal static float GunUseScale(ItemDef d)
        {
            var a = GunArtOf(d);
            return a != null && a.UseScale != 1f && StanceOn(d, a) ? a.UseScale : 1f;
        }

        /// <summary>Each frame (Tick): the right held for an aim or a held stance; a held gun's stance pulses.</summary>
        static void GunTick(HeroController hero, ItemDef def, bool held, bool control)
        {
            var a = GunArtOf(def);
            if (a == null || !control)
            {
                if (aimAt >= 0f) TerraMod.Instance?.StopSound("tk_gun_charge");
                aimAt = -1f;
                gunHolding = false;
                return;
            }
            if (a.Mode == GunMode.Aim)
            {
                if (held && aimAt < 0f)
                {
                    aimAt = Time.time;
                    aimFullCued = false;
                    TurnToAim(hero);
                    TerraMod.Instance?.PlaySound("tk_gun_charge", 0.55f);
                    DevGunAims++;
                }
                else if (!held && aimAt >= 0f)
                {
                    aimAt = -1f;
                    TerraMod.Instance?.StopSound("tk_gun_charge");
                }
                if (aimAt >= 0f)
                {
                    TurnToAim(hero);
                    if (!aimFullCued && AimCharge(a) >= 1f)
                    {
                        aimFullCued = true;
                        TerraMod.Instance?.PlaySound("MaxMana", 0.4f);
                    }
                }
            }
            else if (a.Mode == GunMode.Hold)
            {
                if (held && !gunHolding && Ready(def))
                {
                    gunItem = def.Type;
                    DevGunStances++;
                    TerraMod.Instance?.PlaySound("tk_gun_charge", 0.4f);
                }
                gunHolding = held;
                if (held) TurnToAim(hero);
            }
            // A held gun's stance (its own projectile firing): what it does every so often while the left is down.
            if (a.OnPulse != null && StanceOn(def, a) && TerraMod.Instance.IsUsingItem && Time.time >= pulseAt)
            {
                pulseAt = Time.time + a.Pulse;
                a.OnPulse(hero, def);
            }
        }

        /// <summary>A right click on a loading gun or one with a timed stance.</summary>
        static bool GunPress(HeroController hero, ItemDef def)
        {
            var a = GunArtOf(def);
            if (a == null) return false;
            gunItem = def.Type;
            gunShots = 0;
            var look = RangedArts.Of(def);
            if (a.Mode == GunMode.Load)
            {
                gunRounds = a.Rounds;
                DevGunLoads++;
                TerraMod.Instance?.PlaySound("tk_gun_charge", 0.35f);
                UI.CombatText.Show(hero.transform.position + Vector3.up * 1.2f, a.Rounds > 1 ? $"{Lang.T(a.Name)} ×{a.Rounds}" : Lang.T(a.Name), Color.Lerp(look.A, Color.white, 0.4f));
            }
            else
            {
                gunStanceUntil = Time.time + a.Seconds;
                DevGunStances++;
                TerraMod.Instance?.PlaySound("MaxMana", 0.45f);
                UI.CombatText.Show(hero.transform.position + Vector3.up * 1.2f, $"{Lang.T(a.Name)} {a.Seconds:0.#}s", Color.Lerp(look.A, Color.white, 0.4f));
            }
            ShowGun(def, null);
            SwordFx.Instance?.RangedPower(def);
            return true;
        }

        /// <summary>
        /// The left's shot (RangedFire.Fire), strengthened if the gun's right made it so: an aimed shot, a loaded round, a stance's
        /// shot. False: the left's own way.
        /// </summary>
        internal static bool GunFire(in ShootPatterns.Shot s)
        {
            var def = s.Def;
            var a = GunArtOf(def);
            if (a == null || a.Shot == null) return false;
            switch (a.Mode)
            {
                case GunMode.Aim:
                {
                    if (aimAt < 0f || Time.time - aimAt < 0.15f) return false;
                    float c = AimCharge(a);
                    a.Shot(s, c, 0);
                    // Steadied again from now for the next.
                    aimAt = Time.time;
                    aimFullCued = false;
                    break;
                }
                case GunMode.Load:
                    if (gunItem != def.Type || gunRounds <= 0) return false;
                    gunRounds--;
                    a.Shot(s, 1f, a.Rounds - gunRounds - 1);
                    break;
                default:
                {
                    if (!StanceOn(def, a)) return false;
                    int n = gunShots++;
                    if (n % Mathf.Max(1, a.Every) != 0) return false;
                    a.Shot(s, 1f, n);
                    if (a.Mode == GunMode.Hold && a.Cooldown > 0f)
                    {
                        // A held stance that spends itself on a shot (the Revolver's fanned six): its rest after.
                        readyAt[def.Type] = Time.time + a.Cooldown;
                        gunHolding = false;
                    }
                    break;
                }
            }
            DevGunShots++;
            DevLastGun = a.Name;
            return true;
        }

        // ------------------------------------------------------------------ what the guns fire

        /// <summary>The shot changed: damage × k, its kind (null: its own, through the weapon's own pattern), speed × v, turned, carrying r.</summary>
        static void G(in ShootPatterns.Shot s, RangedShot r, float k, ProjKind? kind = null, float v = 1f, float turn = 0f)
        {
            var t = s;
            t.Damage = Mathf.Max(1, Mathf.RoundToInt(s.Damage * k));
            t.Vel = Rot(s.Vel, turn) * v;
            t.Speed = s.Speed * v;
            r.Item = s.Def;
            r.Local = true;
            if (kind.HasValue)
            {
                var keep = TerraProjectiles.SpawnRanged;
                TerraProjectiles.SpawnRanged = r;
                try { TerraProjectiles.Instance?.Shoot(kind.Value, t.From, t.Vel, t.Damage, t.Knockback, t.Crit, t.Extra, t.UseTicks); }
                finally { TerraProjectiles.SpawnRanged = keep; }
            }
            else RangedFire.With(r, t);
        }

        /// <summary>n of the shot in a fan `spread` degrees either side, damage × k each.</summary>
        static void GFan(in ShootPatterns.Shot s, int n, float spread, float k, RangedShot r, ProjKind? kind = null)
        {
            for (int i = 0; i < n; i++)
                G(s, r.Copy(), k, kind, Random.Range(0.92f, 1.08f), n == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (n - 1f)) + Random.Range(-1.5f, 1.5f));
        }

        /// <summary>n more of the gun's shots, `gap` ticks apart, each its own ammo, aimed where the player aims then (up to `wander` off).</summary>
        static void GBurst(ItemDef def, int n, int gap, float k, float wander, RangedShot r)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            for (int i = 1; i <= n; i++)
                tp.Later(gap * i, (from, dir) =>
                {
                    var mod = TerraMod.Instance;
                    if (mod == null || mod.Inventory.Current.Def != def || !TakeLoad(def, out var l)) return;
                    Shoot(def, l, from, Rot(dir, Random.Range(-wander, wander)), 1f, k, r.Copy());
                    if (def.UseSound != null) mod.PlaySound(def.UseSound, 0.5f * def.UseSoundVolume);
                    RangedFire.Shot(def);
                });
        }

        /// <summary>A heavy shot's look at the muzzle: its ring and a cloud of smoke.</summary>
        static void Heavy(ItemDef def, string sound = "Item_14", float volume = 0.3f)
        {
            SwordFx.Instance?.RangedPower(def);
            SwordFx.Instance?.RangedBlast(def);
            TerraMod.Instance?.PlaySound(sound, volume, 1.2f);
        }

        static Offshoot Off(ProjKind kind, int count, SpawnWay way, float damage, float speed = 0f, float size = 1f) =>
            new Offshoot { Kind = kind, Count = count, Way = way, Damage = damage, Speed = speed, Size = size };

        static partial void RegisterGunArts()
        {
            void Gun(ItemType t, GunArt a)
            {
                gunArts[t] = a;
                if (a.Mode == GunMode.Aim)
                {
                    string most = AimMost(ItemDb.Get(t), a).ToString("0.#");
                    a.Tip = string.Format(a.Tip, most);
                    a.TipEn = string.Format(a.TipEn, most);
                }
                RegisterArt(t, new KindArt
                {
                    Name = a.Name, Tip = a.Tip, TipEn = a.TipEn, Cooldown = a.Mode == GunMode.Load || a.Mode == GunMode.Stance ? a.Cooldown : 0f,
                    Press = a.Mode == GunMode.Load || a.Mode == GunMode.Stance ? GunPress : (System.Func<HeroController, ItemDef, bool>)null,
                });
            }
            GunArt Aim(string name, float full, string tip, string tipEn, GunShot shot, bool sure = true, float worth = 1.4f) =>
                new GunArt { Mode = GunMode.Aim, Name = name, Full = full, Shot = shot, Sure = sure, Worth = worth, Tip = $"右键：按住瞄准，{full:0.#} 秒稳住后枪身发光，同时点左键" + tip + "；没稳住就开枪，加强得少，每开一枪要重新瞄准",
                    TipEn = $"Right click: hold to aim, steady after {full:0.#} s (the gun glows), and click the left to " + tipEn + "; fired before it is steady it is less strengthened, and each shot starts the aim again" };
            GunArt Load(string name, int rounds, float cd, string tip, string tipEn, GunShot shot) =>
                new GunArt { Mode = GunMode.Load, Name = name, Rounds = rounds, Cooldown = cd, Shot = shot, Tip = $"右键：装填{name}，之后 {rounds} 次左键" + tip + $"（{cd:0.#} 秒冷却）", TipEn = $"Right click: load {rounds} special round{(rounds > 1 ? "s" : "")}; the next left shot{(rounds > 1 ? "s" : "")} " + tipEn + $" ({cd:0.#} s cooldown)" };
            GunArt Stance(string name, float seconds, float cd, string tip, string tipEn, GunShot shot, float useScale = 1f, int every = 1) =>
                new GunArt { Mode = GunMode.Stance, Name = name, Seconds = seconds, Cooldown = cd, Shot = shot, UseScale = useScale, Every = every, Tip = $"右键：{name} {seconds:0.#} 秒，其间左键" + tip + $"（{cd:0.#} 秒冷却）", TipEn = $"Right click: {seconds:0.#} s in which the left " + tipEn + $" ({cd:0.#} s cooldown)" };
            GunArt Hold(string name, string tip, string tipEn, GunShot shot, float useScale = 1f, float cd = 0f) =>
                new GunArt { Mode = GunMode.Hold, Name = name, Shot = shot, UseScale = useScale, Cooldown = cd, Tip = "右键：按住时" + tip, TipEn = "Right click: while it is held, " + tipEn };

            // ---- pistols
            Gun(ItemType.FlintlockPistol, Aim("燧火瞄准", 0.9f, "射出一发燃着火星的子弹，贯穿两个敌人，稳住时必定暴击（伤害最多 {0} 倍）",
                "fire a round trailing sparks through two enemies, always a critical hit once steady (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Pierce = 2, Speed = 1.3f, Trail = 1f, TrailDust = 6, SureCrit = p >= 1f, Debuff = EnemyDebuffs.OnFire, DebuffTicks = 180 }, AimK(s, p)); Heavy(s.Def, "Item_11", 0.4f); }));
            Gun(ItemType.TheUndertaker, Load("猩红弹", 3, 6f, "射出猩红弹：命中处溅出血雾伤及周围，敌人防御降低",
                "fire crimson rounds that burst in a blood mist on whoever stands near, lowering the struck enemy's defense",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Debuff = EnemyDebuffs.Ichor, DebuffTicks = 300, Burst = 56f, BurstDamage = 0.4f, Trail = 1f, TrailDust = 5 }, 1.3f)));
            Gun(ItemType.Handgun, Aim("神射", 0.8f, "射出一发金色子弹，在敌人之间弹射，稳住时弹射三次、必定暴击（伤害最多 {0} 倍）",
                "fire a golden round that glances on to another enemy, on through three and always a critical hit once steady (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Bounces = p >= 1f ? 3 : 1, BounceDamage = 0.8f, SureCrit = p >= 1f, Trail = 1f, Tracer = p >= 1f }, AimK(s, p), null, 1.3f); Heavy(s.Def, "Item_11", 0.4f); }, true, 1.1f));
            Gun(ItemType.PhoenixBlaster, Load("凤凰弹", 4, 6f, "射出凤凰火弹：拖着火焰，命中处炸开一团烈火，点燃敌人",
                "fire phoenix rounds trailing flame that burst in fire where they strike, setting enemies alight",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Size = 1.3f, Burst = 72f, BurstDamage = 0.45f, Debuff = EnemyDebuffs.OnFire, DebuffTicks = 300, Trail = 1f, TrailDust = 6, TrailScale = 1.2f }, 1.25f, ProjKind.MeteorShot)));
            Gun(ItemType.Revolver, Hold("扇击", "左键一按就扇击，连发六枪（之后装弹 1.5 秒）", "a left click fans the hammer through all six rounds (then 1.5 s to reload)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Trail = 0.4f }, 0.8f, null, 1f, Random.Range(-6f, 6f)); GBurst(s.Def, 5, 3, 0.8f, 7f, new RangedShot { Trail = 0.4f }); }, 1f, 1.5f));
            Gun(ItemType.VenusMagnum, Load("藤蔓弹", 8, 8f, "射出叶绿弹：自动追踪敌人，命中处散开孢子使其中毒",
                "fire chlorophyte rounds that seek enemies out and scatter spores that poison them",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Home = 6f, Debuff = EnemyDebuffs.Poisoned, DebuffTicks = 300, Trail = 0.8f, TrailDust = 44 }, 1.1f, ProjKind.ChlorophyteBullet)));
            Gun(ItemType.FlareGun, Load("烟花信号", 3, 5f, "射出的信号弹命中后炸成五颗小信号弹",
                "fire flares that burst into five little flares where they strike",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { OnHit = Off(ProjKind.None, 5, SpawnWay.Ring, 0.35f, 5f, 0.8f), Trail = 1f }, 1.2f)));
            Gun(ItemType.PewMaticHorn, Load("嘟嘟齐鸣", 3, 5f, "一次喷出五样东西", "blare out five things at once",
                (in ShootPatterns.Shot s, float p, int n) => GFan(s, 5, 16f, 0.55f, new RangedShot { Trail = 0.3f })));
            Gun(ItemType.PaintballGun, Stance("彩虹乱射", 4f, 10f, "的彩弹炸开一团颜料，伤及周围，有时让敌人混乱",
                "'s paintballs splash on whoever stands near, sometimes confusing the struck",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Burst = 48f, BurstDamage = 0.4f, Debuff = EnemyDebuffs.Confused, DebuffTicks = 120, DebuffChance = 0.35f, Trail = 0.6f, TrailDust = 66 }, 1f)));
            Gun(ItemType.NailGun, Load("钉阵", 2, 6f, "一次扇形射出七枚钉子", "fire seven nails at once in a fan",
                (in ShootPatterns.Shot s, float p, int n) => GFan(s, 7, 18f, 0.5f, new RangedShot { Trail = 0.3f, Debuff = EnemyDebuffs.Bleeding, DebuffTicks = 240 })));
            Gun(ItemType.DartPistol, Load("剧毒镖", 4, 6f, "射出剧毒镖：敌人中剧毒，命中处冒出一团毒雾",
                "fire venom darts that envenom the struck and leave a toxic cloud where they strike",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Debuff = EnemyDebuffs.Venom, DebuffTicks = 300, OnHit = Off(ProjKind.ToxicCloud, 1, SpawnWay.Ring, 0.25f, 0.2f), Trail = 0.8f, TrailDust = 46 }, 1.15f)));

            // ---- rifles
            Gun(ItemType.RedRyder, Aim("红莱德瞄准", 1f, "射出一发贯穿三个敌人的子弹，拖出一道红光，稳住时必定暴击（伤害最多 {0} 倍）",
                "fire a round through three enemies, a red streak behind it, always a critical hit once steady (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Pierce = p >= 1f ? 3 : 1, SureCrit = p >= 1f, Tracer = p >= 1f }, AimK(s, p), null, 1.4f); Heavy(s.Def, "Item_11", 0.4f); }));
            Gun(ItemType.Musket, Aim("排枪", 1.1f, "射出一颗大铅弹，击退极大，枪口喷出浓烟，稳住时必定暴击（伤害最多 {0} 倍）",
                "fire a big lead ball that knocks enemies far back in a cloud of smoke, always a critical hit once steady (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Knock = 1f + 3f * p, Size = 1f + 0.4f * p, Pierce = 1, SureCrit = p >= 1f, Trail = 1f, TrailDust = 31 }, AimK(s, p)); Heavy(s.Def, "Item_14", 0.35f); }));
            Gun(ItemType.ClockworkAssaultRifle, Load("全自动", 1, 5f, "连续射出九发", "fires a run of nine rounds",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Trail = 0.3f }, 0.8f); GBurst(s.Def, 8, 3, 0.8f, 3f, new RangedShot { Trail = 0.3f }); }));
            Gun(ItemType.DartRifle, Aim("毒刺狙击", 1f, "射出一发毒镖，稳住时贯穿一切，沿途留下毒雾（伤害最多 {0} 倍）",
                "fire a dart, once steady through everything in its way leaving toxic clouds behind (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Pierce = p >= 1f ? 8 : 2, Debuff = EnemyDebuffs.Venom, DebuffTicks = 300, OnHit = p >= 1f ? Off(ProjKind.ToxicCloud, 2, SpawnWay.Ring, 0.25f, 0.4f) : null, Tracer = p >= 1f }, AimK(s, p), null, 1.4f); Heavy(s.Def, "Item_11", 0.4f); }, false, 1.2f));
            Gun(ItemType.SniperRifle, Aim("狙击镜", 1.3f, "射出一发越飞越快的子弹，稳住时贯穿一切、必定暴击，留下一道弹道光（伤害最多 {0} 倍）",
                "fire a round that speeds on, once steady going through everything, always a critical hit, its path left in light (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Pierce = p >= 1f ? 12 : 2, SureCrit = p >= 1f, Tracer = p >= 1f, Size = 1f + 0.2f * p }, AimK(s, p), null, 1.2f + 0.6f * p); Heavy(s.Def, "Item_14", 0.3f); }));
            Gun(ItemType.CandyCornRifle, Stance("糖果风暴", 3.5f, 12f, "射速提高三成，每发糖果玉米在敌人间弹射两次",
                "fires about a third faster, each candy corn glancing on through two enemies",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Bounces = 2, BounceDamage = 0.6f, Trail = 0.5f }, 1f), 0.7f));
            Gun(ItemType.Harpoon, Load("三叉鱼叉", 1, 4f, "一次射出三支鱼叉，命中让敌人流血", "fires three harpoons at once that make the struck bleed",
                (in ShootPatterns.Shot s, float p, int n) => GFan(s, 3, 10f, 0.9f, new RangedShot { Debuff = EnemyDebuffs.Bleeding, DebuffTicks = 300, Trail = 0.6f, TrailDust = 33 })));

            // ---- machine guns
            Gun(ItemType.Minishark, Stance("过载", 3.5f, 12f, "射速提高一半，子弹拖出光尾", "fires about half again as fast, the rounds trailing light",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Trail = 0.6f }, 1f), 0.65f));
            Gun(ItemType.Gatligator, Stance("鳄牙", 4f, 12f, "射出毒液弹，敌人中剧毒并流血", "fires venom rounds that envenom the struck and make them bleed",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Debuff = n % 2 == 0 ? EnemyDebuffs.Venom : EnemyDebuffs.Bleeding, DebuffTicks = 240, Trail = 0.5f, TrailDust = 46 }, 1f, ProjKind.VenomBullet)));
            Gun(ItemType.Megashark, Stance("鲨群", 4f, 12f, "每四发放出一条追咬敌人的鲨鱼", "sends out a shark that hunts enemies down every fourth round",
                (in ShootPatterns.Shot s, float p, int n) =>
                {
                    G(s, new RangedShot { Trail = 0.4f }, 1f);
                    if (n % 4 == 0) G(s, new RangedShot { Home = 6f, Trail = 0.8f, TrailDust = 33 }, 1.2f, ProjKind.SharknadoShark, 0.7f);
                }));
            Gun(ItemType.Uzi, Stance("双持", 3f, 10f, "每次并排射出两发", "fires two rounds side by side each shot",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Trail = 0.3f }, 0.62f, null, 1f, -2f); G(s, new RangedShot { Trail = 0.3f }, 0.62f, null, 1f, 2f); }));
            Gun(ItemType.ChainGun, Hold("预热", "枪管预热：射速提高四成，子弹点燃敌人", "the barrels spin up: two fifths faster, rounds that set enemies alight",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Debuff = EnemyDebuffs.OnFire, DebuffTicks = 180, Trail = 0.5f, TrailDust = 6 }, 1f), 0.6f));
            Gun(ItemType.SDMG, Stance("星辰弹", 4f, 12f, "射出交织穿行的夜明弹，拖着星光", "fires luminite rounds that weave as they go, trailing starlight",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Weave = n % 2 == 0 ? 6f : -6f, Trail = 0.7f, TrailDust = 229 }, 1f, ProjKind.LuminiteBullet)));
            Gun(ItemType.VortexBeater, new GunArt
            {
                Mode = GunMode.Stance, Name = "星旋火箭", Seconds = 4f, Cooldown = 12f, Pulse = 0.4f, OnPulse = (h, d) => GunRocket(h, d, 1.2f),
                Tip = "右键：星旋火箭 4 秒，其间开火时每 0.4 秒另射出一枚追踪火箭（12 秒冷却）",
                TipEn = "Right click: 4 s in which, while firing, a homing rocket goes out every 0.4 s as well (12 s cooldown)",
            });
            Gun(ItemType.CelebrationMk2, new GunArt
            {
                Mode = GunMode.Stance, Name = "终章烟花", Seconds = 4f, Cooldown = 12f, Pulse = 0.3f, OnPulse = (h, d) => GunFirework(h, d),
                Tip = "右键：终章烟花 4 秒，其间开火时每 0.3 秒另放出一发烟花（12 秒冷却）",
                TipEn = "Right click: 4 s in which, while firing, a firework goes up every 0.3 s as well (12 s cooldown)",
            });

            // ---- shotguns
            Gun(ItemType.Boomstick, Load("独头弹", 1, 4f, "射出一发独头重弹：贯穿三个敌人，击退极大，枪口喷出浓烟",
                "fires a heavy slug through three enemies that knocks them far back, in a cloud of smoke",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Pierce = 3, Knock = 3f, Size = 1.5f, Speed = 1.3f, Trail = 1f, TrailDust = 31 }, 3.5f, ProjKind.Bullet); Heavy(s.Def); }));
            Gun(ItemType.QuadBarrelShotgun, Load("四管齐发", 1, 4f, "四管齐发，一次喷出十六颗弹丸", "fires all four barrels at once, sixteen pellets",
                (in ShootPatterns.Shot s, float p, int n) => { GFan(s, 16, 30f, 0.5f, new RangedShot { Near = 1.15f, Far = 0.8f }, s.Kind); Heavy(s.Def); }));
            Gun(ItemType.Shotgun, Load("龙息弹", 2, 6f, "射出龙息弹：弹丸燃着火，命中处炸开，点燃敌人", "fire dragon's breath: burning pellets that burst where they strike and set enemies alight",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Debuff = EnemyDebuffs.OnFire, DebuffTicks = 300, Burst = 40f, BurstDamage = 0.35f, Trail = 0.8f, TrailDust = 6 }, 1f); Heavy(s.Def, "Item_14", 0.2f); }));
            Gun(ItemType.OnyxBlaster, Aim("暗晶充能", 1f, "射出一颗巨大的暗晶，命中后炸成一圈暗影碎晶，带暗影焰（伤害最多 {0} 倍）",
                "fire a great onyx crystal that bursts into a ring of shadow shards where it strikes, wreathed in shadowflame (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) =>
                {
                    G(s, new RangedShot { Size = 1f + p, Debuff = EnemyDebuffs.Shadowflame, DebuffTicks = 300, OnHit = Off(ProjKind.CrystalShard, 2 + Mathf.RoundToInt(8f * p), SpawnWay.Ring, 0.35f, 7f), Trail = 1f, TrailDust = 27, TrailScale = 1.3f }, AimK(s, p), ProjKind.OnyxBlast, 1.2f);
                    GFan(s, 3, 8f, 0.6f, new RangedShot { Debuff = EnemyDebuffs.Shadowflame, DebuffTicks = 180, Trail = 0.6f, TrailDust = 27 }, s.Kind);
                    Heavy(s.Def);
                }, false, 1.1f));
            Gun(ItemType.TacticalShotgun, Load("破片弹", 3, 6f, "射出破片弹：弹丸贯穿一个敌人，命中处碎出两片弹片", "fire fragmenting rounds: pellets through one enemy that shatter into two fragments where they strike",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Pierce = 1, Shards = 2, ShardDamage = 0.35f, Trail = 0.4f }, 1f)));
            Gun(ItemType.Xenopopper, Aim("泡泡核心", 1f, "射出一个巨大的泡泡，破裂时炸出一圈子弹（伤害最多 {0} 倍）",
                "fire one huge bubble that pops into a ring of rounds (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) =>
                {
                    var keep = TerraProjectiles.SpawnRanged;
                    TerraProjectiles.SpawnRanged = new RangedShot { Item = s.Def, Local = true, Size = 1.2f + 1.4f * p, OnDeath = Off(s.Kind, 4 + Mathf.RoundToInt(6f * p), SpawnWay.Ring, 0.35f, 9f), Trail = 1f };
                    try { TerraProjectiles.Instance?.ShootXenoBubble(s.From, s.Vel * 0.08f, Mathf.RoundToInt(s.Damage * AimK(s, p)), s.Knockback, s.Crit, Mathf.Atan2(-s.Vel.y, s.Vel.x), s.Kind, s.Speed); }
                    finally { TerraProjectiles.SpawnRanged = keep; }
                    Heavy(s.Def, "Item_85", 0.5f);
                }, false, 1.1f));

            // ---- launchers and cannons
            Gun(ItemType.GrenadeLauncher, Load("集束榴弹", 2, 6f, "射出集束榴弹，炸开时再迸出六枚碎片", "fire cluster grenades that throw out six fragments as they go off",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { OnDeath = Off(ProjKind.ClusterFragment1, 6, SpawnWay.Ring, 0.25f, 5f), Trail = 0.6f }, 1f)));
            Gun(ItemType.RocketLauncher, Load("火箭齐射", 1, 6f, "朝天齐射五发火箭，落向鼠标处", "launches five rockets skyward that come down on the cursor",
                (in ShootPatterns.Shot s, float p, int n) => GunBarrage(s, 5, 0.6f)));
            Gun(ItemType.ProximityMineLauncher, Load("雷区", 1, 6f, "在鼠标两侧布下一排四枚感应雷", "lays a row of four mines either side of the cursor",
                (in ShootPatterns.Shot s, float p, int n) => GunRow(s, 4, 2.5f, 0.8f)));
            Gun(ItemType.SnowmanCannon, Load("雪崩", 1, 6f, "射出一枚巨大的雪人火箭，炸开时迸出八颗雪球，冻伤敌人", "fires a huge snowman rocket that throws out eight snowballs as it goes off, frostburning enemies",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Size = 1.6f, Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 300, OnDeath = Off(ProjKind.SnowballProj, 8, SpawnWay.Ring, 0.3f, 6f), Trail = 1f, TrailDust = 76 }, 2.5f)));
            Gun(ItemType.Celebration, Load("烟花秀", 1, 6f, "朝天齐射六发烟花，落向鼠标处", "launches six fireworks skyward that come down on the cursor",
                (in ShootPatterns.Shot s, float p, int n) => GunBarrage(s, 6, 0.6f)));
            Gun(ItemType.ElectrosphereLauncher, Load("电网", 1, 6f, "在鼠标两侧布下三颗电球，电弧在敌人之间跳动", "sets three electrospheres either side of the cursor, lightning leaping between enemies",
                (in ShootPatterns.Shot s, float p, int n) => GunRow(s, 3, 6f, 0.8f, new RangedShot { Chain = 2, ChainDamage = 0.4f, Volley = TerraProjectiles.NewVolley() })));
            Gun(ItemType.StakeLauncher, Aim("穿心桩", 1f, "射出一根越飞越快、贯穿一切的木桩，稳住时必定暴击（伤害最多 {0} 倍）",
                "fire a stake that speeds on through everything in its way, always a critical hit once steady (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Pierce = p >= 1f ? 20 : 2, Speed = 1f + 0.6f * p, Tracer = p >= 1f, SureCrit = p >= 1f, Trail = 1f, TrailDust = 7 }, AimK(s, p)); Heavy(s.Def, "Item_11", 0.4f); }));
            Gun(ItemType.JackOLanternLauncher, Load("南瓜大王", 1, 6f, "射出一个巨大的南瓜灯，炸开时迸出五个小南瓜灯，点燃敌人",
                "fires a giant jack 'o lantern that throws out five small ones as it goes off, setting enemies alight",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Size = 1.6f, Debuff = EnemyDebuffs.OnFire, DebuffTicks = 300, OnDeath = Off(ProjKind.None, 5, SpawnWay.Ring, 0.4f, 6f, 0.7f), Trail = 1f, TrailDust = 6 }, 2f)));
            Gun(ItemType.BarrelLauncher, Load("连环桶", 1, 6f, "一次射出三个炸药桶", "fires three barrels at once",
                (in ShootPatterns.Shot s, float p, int n) => GFan(s, 3, 12f, 0.8f, new RangedShot { Trail = 0.5f })));
            Gun(ItemType.Stynger, Load("毒刺破片", 2, 6f, "射出更大的毒刺，炸开范围更大", "fire bigger stynger bolts with a wider blast",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Size = 1.4f, Burst = 112f, BurstDamage = 0.5f, Trail = 1f, TrailDust = 6 }, 1.6f)));
            Gun(ItemType.StarCannon, Load("流星召唤", 1, 6f, "射出的星星命中后从天上召下五颗流星", "fires a star that calls five more down from the sky where it strikes",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { OnHit = Off(ProjKind.None, 5, SpawnWay.Fall, 0.45f), Trail = 1f, TrailDust = 15 }, 1.3f)));
            Gun(ItemType.SuperStarCannon, Stance("星爆", 4f, 10f, "的星星命中后分裂成三颗", "'s stars split into three where they strike",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Shards = 3, ShardDamage = 0.35f, Trail = 0.8f, TrailDust = 15 }, 1f)));
            Gun(ItemType.SnowballCannon, Load("大雪球", 1, 4f, "射出一颗巨大的雪球，贯穿三个敌人并冻伤", "fires a huge snowball through three enemies that frostburns them",
                (in ShootPatterns.Shot s, float p, int n) => { G(s, new RangedShot { Size = 2f, Pierce = 3, Knock = 2f, Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 300, Trail = 1f, TrailDust = 76 }, 3f); Heavy(s.Def, "Item_51", 0.5f); }));
            Gun(ItemType.Sandgun, Load("沙暴", 1, 3f, "朝斜上方喷出九团沙", "blasts out nine balls of sand, a little upward",
                (in ShootPatterns.Shot s, float p, int n) => { var t = s; t.Vel = Rot(s.Vel, (s.Vel.x < 0f ? -1f : 1f) * 16f); GFan(t, 9, 14f, 0.6f, new RangedShot { Trail = 0.35f }); }));

            // ---- sprayers and pipes
            Gun(ItemType.Flamethrower, Hold("猛火", "加大火力：射程更远、伤害提高三成", "more fire: further, about a third more damage",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Debuff = EnemyDebuffs.OnFire, DebuffTicks = 420 }, 1.3f, null, 1.25f), 0.85f));
            Gun(ItemType.ElfMelter, Hold("精灵之焰", "加大火力：射程更远、伤害提高三成，烧出诅咒之火", "more fire: further, about a third more damage, cursed flames",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Debuff = EnemyDebuffs.CursedInferno, DebuffTicks = 300 }, 1.3f, null, 1.25f), 0.85f));
            Gun(ItemType.Toxikarp, Load("毒泡潮", 1, 5f, "一次吐出九个毒泡", "spits out nine toxic bubbles at once",
                (in ShootPatterns.Shot s, float p, int n) => GFan(s, 9, 30f, 0.6f, new RangedShot { Trail = 0.3f })));
            Gun(ItemType.Blowpipe, Aim("深吸气", 0.8f, "吹出一颗贯穿两个敌人的毒种子（伤害最多 {0} 倍）", "blow a seed through two enemies that poisons them (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Pierce = p >= 1f ? 2 : 0, Debuff = EnemyDebuffs.Poisoned, DebuffTicks = 300, Trail = 1f }, AimK(s, p), null, 1.3f), false, 1.4f));
            Gun(ItemType.Blowgun, Aim("毒吹", 0.9f, "吹出一支贯穿三个敌人的毒镖，命中处冒出孢子云（伤害最多 {0} 倍）",
                "blow a dart through three enemies that leaves a spore cloud where it strikes (up to {0}× damage)",
                (in ShootPatterns.Shot s, float p, int n) => G(s, new RangedShot { Pierce = p >= 1f ? 3 : 0, Debuff = EnemyDebuffs.Poisoned, DebuffTicks = 300, OnHit = p >= 1f ? Off(ProjKind.SporeCloud, 1, SpawnWay.Ring, 0.3f, 0.2f) : null, Trail = 1f, TrailDust = 44 }, AimK(s, p), null, 1.3f), false, 1.3f));
        }

        /// <summary>n of the launcher's shots launched up and out in a fan, each turning to come down on the cursor.</summary>
        static void GunBarrage(in ShootPatterns.Shot s, int n, float k)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            float facing = s.Mouse.x < s.From.x ? -1f : 1f;
            var target = s.Mouse;
            for (int i = 0; i < n; i++)
            {
                float f = n == 1 ? 0f : i / (n - 1f) - 0.5f;
                var dir = Rot(new Vector2(facing * 0.75f, 1f).normalized, -facing * f * 40f);
                var r = new RangedShot { Point = target + new Vector2(f * 3f, 0f), Seek = 15f, SeekFrom = 5, Trail = 0.6f };
                var t = s;
                t.Vel = dir * s.Speed;
                t.Mouse = target;
                G(t, r, k);
            }
            Heavy(s.Def);
        }

        /// <summary>n of the launcher's shots set out in a row either side of the cursor (`gap` blocks apart).</summary>
        static void GunRow(in ShootPatterns.Shot s, int n, float gap, float k, RangedShot r = null)
        {
            const float Block = 16f * Effects.TerraParticles.Px;
            for (int i = 0; i < n; i++)
            {
                var spot = s.Mouse + new Vector2((i - (n - 1) * 0.5f) * gap * Block, 0f);
                var t = s;
                t.Vel = (spot - s.From).normalized * s.Speed;
                t.Mouse = spot;
                G(t, r != null ? r.Copy() : new RangedShot { Trail = 0.3f }, k);
            }
        }

        /// <summary>The Vortex Beater's stance: a homing rocket from the player at the cursor, its own ammo.</summary>
        static void GunRocket(HeroController hero, ItemDef def, float k)
        {
            if (!TakeLoad(def, out var l)) return;
            l.Kind = ProjKind.Rocket1;
            Shoot(def, l, Centre(hero), AimWay(hero), 0.8f, k, new RangedShot { Home = 7f, Trail = 0.8f, TrailDust = 229 });
            TerraMod.Instance?.PlaySound("Item_11", 0.4f, 0.7f);
        }

        /// <summary>The Celebration Mk2's stance: a firework at the cursor.</summary>
        static void GunFirework(HeroController hero, ItemDef def)
        {
            if (!TakeLoad(def, out var l)) return;
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = new RangedShot { Item = def, Local = true, Trail = 0.6f };
            TerraProjectiles.SpawnClass = def.Class;
            try { tp.ShootCelebration(Centre(hero), AimWay(hero) * Mathf.Max(8f, l.Speed), Mathf.RoundToInt(l.Damage * 0.8f), l.Knockback, Equipment.Crit(def), Facing(hero)); DevRangedArtShots++; }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
        }
    }
}
