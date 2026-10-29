using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>
    /// The ranged weapons' right-click arts shared by the first stages' bows and throws (RangedArts; the user's 10-27 "右键自然也可以
    /// 加"; "同阶段的过渡换皮武器就没必要了"), every shot of them the weapon's own (its next ammo, or the thrown thing itself, spent as
    /// it goes): a bow drawn full and loosed through everything, fanned out, or raining its arrows down on the cursor; knives
    /// and grenades thrown in fans; the Bone Javelin hurled through a line. The guns' are BladeSkillsGuns.cs, the later bows',
    /// repeaters' and throws' BladeSkillsBows.cs; the helpers they share (the load, the fan, the volley, the sky, the row, the
    /// ring) are here, with the weapon shown held out while an art fires.
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterRangedArts()
        {
            void Press(ItemType t, string name, float cooldown, string tip, string tipEn, System.Func<HeroController, ItemDef, bool> press) =>
                RegisterArt(t, new KindArt { Name = name, Cooldown = cooldown, Tip = tip, TipEn = tipEn, Press = press });
            void Charge(ItemType t, string name, float full, string sound, string tip, string tipEn, System.Action<HeroController, ItemDef, float> release) =>
                RegisterArt(t, new KindArt { Name = name, Charged = true, Full = full, Aimed = true, Pose = 0f, ChargeSound = sound, Tip = tip, TipEn = tipEn, Release = release });

            // ---- the first stage's bows (one stage's bows of wood and metal, one of three each): drawn full, fanned, rained down
            void PowerShot(ItemType t, string name) =>
                Charge(t, name, 1f, "tk_bow_draw", "右键：按住拉满弓，松开射出一箭贯穿沿途敌人（伤害最多 3.5 倍）",
                    "Right click: hold to draw the bow full, release to loose an arrow through everything on its way (up to 3.5× damage)",
                    (h, d, c) => Power(h, d, 1.5f + 2f * c, new RangedShot { Pierce = 5, Speed = 1.5f, Trail = 1f, TrailScale = 1.1f }));
            void Scatter(ItemType t, string name, int n, float spread, string tip, string tipEn, float cooldown = 3f) =>
                Press(t, name, cooldown, tip, tipEn, (h, d) => Fan(h, d, n, spread, 0.6f, new RangedShot { Trail = 0.35f }));
            void Rain(ItemType t, string name, int n, float seconds, string tip, string tipEn, float cooldown = 5f) =>
                Press(t, name, cooldown, tip, tipEn, (h, d) => Sky(h, d, n, seconds, 0.55f));

            PowerShot(ItemType.WoodenBow, "满弓");
            Scatter(ItemType.CopperBow, "散射", 5, 14f, "右键：一次扇形射出五箭", "Right click: loose five arrows at once in a fan");
            PowerShot(ItemType.TinBow, "满弓");
            Rain(ItemType.RichMahoganyBow, "林间箭雨", 8, 0.6f, "右键：在鼠标处降下一阵箭雨", "Right click: rain a volley of arrows down on the cursor");
            Scatter(ItemType.PalmWoodBow, "散射", 5, 14f, "右键：一次扇形射出五箭", "Right click: loose five arrows at once in a fan");
            PowerShot(ItemType.BorealWoodBow, "满弓");
            Scatter(ItemType.LeadBow, "散射", 5, 14f, "右键：一次扇形射出五箭", "Right click: loose five arrows at once in a fan");
            PowerShot(ItemType.SilverBow, "满弓");
            Rain(ItemType.EbonwoodBow, "腐雨", 8, 0.6f, "右键：在鼠标处降下一阵箭雨", "Right click: rain a volley of arrows down on the cursor");
            Rain(ItemType.ShadewoodBow, "血林箭雨", 8, 0.6f, "右键：在鼠标处降下一阵箭雨", "Right click: rain a volley of arrows down on the cursor");
            PowerShot(ItemType.TungstenBow, "满弓");
            Scatter(ItemType.PlatinumBow, "散射", 7, 18f, "右键：一次扇形射出七箭", "Right click: loose seven arrows at once in a fan");
            PowerShot(ItemType.IronBow, "满弓");
            Rain(ItemType.GoldBow, "金色箭雨", 10, 0.7f, "右键：在鼠标处降下一阵箭雨", "Right click: rain a volley of arrows down on the cursor");
            Scatter(ItemType.AcornSlingshot, "连珠弹", 5, 12f, "右键：一次弹出五颗橡实", "Right click: sling five acorns at once");
            // Bows whose own arrows are their art.
            Rain(ItemType.DaedalusStormbow, "风暴", 24, 1.4f, "右键：在鼠标处召来一场持续的箭雨风暴", "Right click: call a lasting storm of arrows down on the cursor", 7f);
            Press(ItemType.CobaltRepeater, "连射", 4f, "右键：连续快速射出 5 发", "Right click: fire 5 bolts in a quick run",
                (h, d) => Volley(h, d, 5, 4, 0.8f, 0f, new RangedShot { Trail = 0.3f }));

            // ---- the thrown things (the first stages' in fans; the later ones their own, BladeSkillsBows.cs)
            void Throw(ItemType t, string name, int n, float spread, string what, string whatEn) =>
                Press(t, name, 2.5f, $"右键：一次扇形掷出 {n} 个{what}（用掉其中约六成）", $"Right click: throw {n} {whatEn} at once in a fan (about three in five of them used up)",
                    (h, d) => Fan(h, d, n, spread, 0.6f, new RangedShot { Trail = 0.3f }));
            Throw(ItemType.MudBall, "泥球乱扔", 5, 16f, "泥球", "mud balls");
            Throw(ItemType.Shuriken, "手里剑扇", 5, 16f, "手里剑", "shuriken");
            Throw(ItemType.ThrowingKnife, "飞刀扇", 5, 14f, "飞刀", "throwing knives");
            Throw(ItemType.PoisonedKnife, "毒刀扇", 5, 14f, "毒刀", "poisoned knives");
            Throw(ItemType.RottenEgg, "臭蛋乱扔", 3, 14f, "臭蛋", "rotten eggs");
            Throw(ItemType.BoneDagger, "骨刀扇", 5, 14f, "骨投刀", "bone daggers");
            Throw(ItemType.SpikyBall, "铺刺", 5, 24f, "尖球", "spiky balls");
            Throw(ItemType.PrettyMirror, "碎镜", 3, 14f, "镜子", "mirrors");
            Throw(ItemType.BloodWater, "三连泼", 3, 14f, "血水", "flasks of blood water");
            Throw(ItemType.UnholyWater, "三连泼", 3, 14f, "邪水", "flasks of unholy water");
            Throw(ItemType.HolyWater, "三连泼", 3, 14f, "圣水", "flasks of holy water");
            Throw(ItemType.AleTosser, "三连投", 3, 14f, "麦芽酒", "ales");
            Throw(ItemType.Grenade, "集束投掷", 3, 12f, "手榴弹", "grenades");
            Throw(ItemType.BouncyGrenade, "集束投掷", 3, 12f, "弹力手榴弹", "bouncy grenades");
            Throw(ItemType.StickyGrenade, "集束投掷", 3, 12f, "粘性手榴弹", "sticky grenades");
            // Wound up over the shoulder as it charges (not held out at the cursor).
            RegisterArt(ItemType.BoneJavelin, new KindArt
            {
                Name = "贯穿投", Charged = true, Full = 0.9f, Pose = 150f, ChargeSound = "tk_heavy_charge",
                Tip = "右键：按住蓄力，松开全力掷出，贯穿一排敌人（伤害最多 3.5 倍）",
                TipEn = "Right click: hold to wind up, release to hurl it with all your strength through a line of enemies (up to 3.5× damage)",
                Release = (h, d, c) => Power(h, d, 1.5f + 2f * c, new RangedShot { Pierce = 4, Speed = 1.4f, Trail = 1f }),
            });
        }

        /// <summary>Dev: overdrives begun, ranged art shots made.</summary>
        public static int DevOverdrives, DevRangedArtShots;

        // ------------------------------------------------------------------ the weapon shown while an art fires

        static ItemDef shownItem;
        static float shownUntil;
        static float? shownDeg;
        static bool shownPose;

        /// <summary>
        /// A shot of an art left: the weapon held out at the cursor (raised for the arts that fire skyward) a moment after it,
        /// kicking as each shot goes, the arm along it (an art is no use of the item, so nothing else shows it); the shot's
        /// muzzle look and the hold's kick (RangedFire.Shot).
        /// </summary>
        static void ArtShot(ItemDef def, float? deg = null)
        {
            RangedFire.Shot(def);
            ShowGun(def, deg);
        }

        /// <summary>The weapon held out at the cursor (or raised to `deg`) a moment, the arm along it.</summary>
        static void ShowGun(ItemDef def, float? deg)
        {
            shownItem = def;
            shownUntil = Time.time + 0.4f;
            shownDeg = deg;
            if (!shownPose)
            {
                shownPose = true;
                ArmPoses.Add(facing => RangedShown(out _) && HeroController.SilentInstance != null ? ShownAngle(HeroController.SilentInstance, facing) : (float?)null);
            }
        }

        /// <summary>An art's weapon held out now (its shots going or just gone, a gun aimed or its stance held), and which.</summary>
        internal static bool RangedShown(out ItemDef def)
        {
            var mod = TerraMod.Instance;
            def = shownItem;
            if (mod == null || Charging) return false;
            var cur = mod.Inventory.Current.Def;
            var gun = GunArtOf(cur);
            if (gun != null && (gun.Mode == GunMode.Aim && aimAt >= 0f || gun.Mode == GunMode.Hold && gunHolding))
            {
                def = cur;
                if (!shownPose)
                {
                    shownPose = true;
                    ArmPoses.Add(facing => RangedShown(out _) && HeroController.SilentInstance != null ? ShownAngle(HeroController.SilentInstance, facing) : (float?)null);
                }
                shownDeg = null;
                return !(mod.IsUsingItem && mod.SwingItem == cur);
            }
            return shownItem != null && Time.time < shownUntil && !(mod.IsUsingItem && mod.SwingItem == shownItem);
        }

        /// <summary>How bright the held-out weapon is: an aimed gun by its steadiness (full: its outline's motes), else none.</summary>
        internal static float ShownGlow => GunAiming(out _, out float c) ? c : 0f;

        /// <summary>Its way (degrees from level for the way he faces): at the cursor or raised, kicked up by the last shot.</summary>
        internal static float ShownAngle(HeroController hero, float facing)
        {
            float deg;
            if (shownDeg.HasValue) deg = shownDeg.Value;
            else
            {
                var d = Aim() - Shoulder(hero, facing);
                deg = Mathf.Atan2(d.y, Mathf.Abs(d.x) < 1e-3f ? 1e-3f : d.x * facing) * Mathf.Rad2Deg;
            }
            return Mathf.Clamp(deg + 10f * Mathf.Exp(-(Time.time - RangedFire.LastShotAt) * 9f), -88f, 100f);
        }

        // ------------------------------------------------------------------ what is shot

        /// <summary>What the weapon shoots now: its next ammo (spent), or the thrown thing itself (spent), or its own shot.</summary>
        struct Load
        {
            public ProjKind Kind;
            public ItemDef Ammo;
            public float Speed, Knockback;
            public int Damage;
        }

        /// <summary>The weapon's next load (false when it has none).</summary>
        static bool TakeLoad(ItemDef def, out Load l)
        {
            l = default;
            var mod = TerraMod.Instance;
            if (mod == null) return false;
            l.Kind = def.Shoot;
            l.Speed = def.ShootSpeed;
            if (def.UseAmmo != AmmoKind.None)
            {
                int slot = mod.Inventory.FindAmmo(def.UseAmmo, Equipment.Current.AmmoCycling, AccessoryEffects.AmmoOffset);
                if (slot < 0) return false;
                l.Ammo = mod.Inventory.Slots[slot].Def;
                if (!mod.PickAmmo(def, ref l.Kind, ref l.Speed, out l.Damage, out l.Knockback)) return false;
                if (def.Type == ItemType.MoltenFury && l.Kind == ProjKind.WoodenArrow) l.Kind = ProjKind.FlamingArrow;
                return true;
            }
            if (def.Consumable)
            {
                var cur = mod.Inventory.Current;
                if (cur.IsEmpty || cur.Def != def) return false;
                mod.Inventory.ConsumeSelected();
            }
            l.Damage = Equipment.Damage(def, null);
            l.Knockback = Equipment.Knockback(def);
            return true;
        }

        /// <summary>Its patterns make many at once (a fan, pellets, a held bow): an art shoots their one projectile instead.</summary>
        static bool Many(ItemDef def)
        {
            switch (def.Type)
            {
                case ItemType.Tsunami: case ItemType.ChlorophyteShotbow: case ItemType.Shotgun: case ItemType.QuadBarrelShotgun: case ItemType.OnyxBlaster:
                case ItemType.Xenopopper: case ItemType.TacticalShotgun: case ItemType.Boomstick: case ItemType.ClockworkAssaultRifle: case ItemType.Eventide:
                case ItemType.AerialBane: case ItemType.Flamethrower: case ItemType.ElfMelter: case ItemType.VortexBeater: case ItemType.Phantasm:
                case ItemType.PhantomPhoenix: case ItemType.CelebrationMk2: case ItemType.PiranhaGun: case ItemType.DaedalusStormbow: case ItemType.BloodRainBow:
                    return true;
                default:
                    return def.Channel || def.Pellets > 0;
            }
        }

        /// <summary>One of the weapon's shots for an art, from a point (world) along a way (unit) at its speed × speedK, damage × k, carrying r.</summary>
        static void Shoot(ItemDef def, in Load l, Vector2 from, Vector2 way, float speedK, float k, RangedShot r, Vector2? mouse = null)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            r.Item = def;
            r.Local = true;
            int damage = Mathf.Max(1, Mathf.RoundToInt(l.Damage * k));
            var shot = new ShootPatterns.Shot
            {
                Def = def, Ammo = l.Ammo, Kind = l.Kind, From = from, Mouse = mouse ?? Aim(), Vel = way * l.Speed * speedK, Speed = l.Speed * speedK,
                Knockback = l.Knockback, Crit = Equipment.Crit(def), Damage = damage, UseTicks = Mathf.RoundToInt(def.UseAnimation * 60f),
                Hero = HeroController.SilentInstance,
            };
            TerraProjectiles.SpawnClass = def.Class;
            TerraProjectiles.SpawnForge = Forging.RangedLevel(def);
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try
            {
                if (Many(def))
                {
                    var kind = def.Type == ItemType.AerialBane ? ProjKind.BetsyArrow : l.Kind;
                    if (kind == ProjKind.None || def.Channel && l.Ammo == null) kind = def.UseAmmo == AmmoKind.Arrow ? ProjKind.WoodenArrow : ProjKind.Bullet;
                    // The Celebration Mk2's rockets: the Celebration's fireworks.
                    if (def.Type == ItemType.CelebrationMk2)
                        tp.ShootCelebration(from, shot.Vel, damage, l.Knockback, shot.Crit, way.x < 0f ? -1f : 1f);
                    else if (def.Type == ItemType.Flamethrower || def.Type == ItemType.ElfMelter)
                        tp.ShootFlame(from, shot.Vel, damage, l.Knockback, shot.Crit, def.Type == ItemType.ElfMelter, way.x < 0f ? -1f : 1f);
                    else tp.Shoot(kind, from, shot.Vel, damage, l.Knockback, shot.Crit, 0, shot.UseTicks);
                }
                else RangedFire.Vanilla(shot);
            }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
                TerraProjectiles.SpawnForge = 0;
            }
            DevRangedArtShots++;
        }

        static Vector2 AimWay(HeroController hero)
        {
            var way = Aim() - Centre(hero);
            return way.sqrMagnitude < 1e-4f ? new Vector2(Facing(hero), 0f) : way.normalized;
        }

        static void Sound(ItemDef def, float volume = 0.6f)
        {
            if (def.UseSound != null) TerraMod.Instance?.PlaySound(def.UseSound, volume * def.UseSoundVolume);
        }

        // ------------------------------------------------------------------ the arts

        /// <summary>One shot let go from a charge: damage × k, carrying r (a slug's smoke with it).</summary>
        static void Power(HeroController hero, ItemDef def, float k, RangedShot r, bool slug = false)
        {
            if (!TakeLoad(def, out var l)) return;
            TurnToAim(hero);
            Shoot(def, l, Centre(hero), AimWay(hero), 1f, k, r);
            Sound(def, 0.8f);
            TerraMod.Instance?.PlaySound(slug ? "Item_14" : "Item_5", slug ? 0.35f : 0.4f, slug ? 1.2f : 0.8f);
            ArtShot(def);
            SwordFx.Instance?.RangedPower(def);
        }

        /// <summary>
        /// n at once in a fan `spread` degrees either side of the aim raised by `lift` (the Sandgun's above the floor at the
        /// feet, where its sand would pile), from a little ahead of the player, damage × k each; a load for every three.
        /// </summary>
        static bool Fan(HeroController hero, ItemDef def, int n, float spread, float k, RangedShot r, float lift = 0f)
        {
            TurnToAim(hero);
            var way = AimWay(hero);
            if (lift != 0f) way = Rot(way, Facing(hero) * lift);
            var from = Centre(hero) + way * 10f * Effects.TerraParticles.Px;
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 3 == 0 && !TakeLoad(def, out l)) break;
                float off = n == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (n - 1f)) + Random.Range(-1.5f, 1.5f);
                Shoot(def, l, from, Rot(way, off), Random.Range(0.92f, 1.08f), k, r.Copy());
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            ArtShot(def);
            return true;
        }

        /// <summary>
        /// n shots one after another, `gap` ticks apart, each its own load, aimed where the player aims then (up to `wander`
        /// degrees off), damage × k; `up`: launched skyward in a fan, each turning to come down on the cursor (a barrage).
        /// </summary>
        static bool Volley(HeroController hero, ItemDef def, int n, int gap, float k, float wander, RangedShot r, bool up = false)
        {
            if (!TakeLoad(def, out var first)) return false;
            TurnToAim(hero);
            var target = Aim();
            void One(int i, Load l, Vector2 from, Vector2 way)
            {
                var shot = r.Copy();
                var dir = Rot(way, Random.Range(-wander, wander));
                if (up)
                {
                    float f = n == 1 ? 0f : i / (n - 1f) - 0.5f;
                    // Up and out ahead (a rocket speeds up fast: it is turned early and hard to come down on the spot).
                    dir = Rot(new Vector2(Facing(hero) * 0.75f, 1f).normalized, -Facing(hero) * f * 40f);
                    shot.Point = target + new Vector2(f * 3f, 0f);
                    shot.Seek = 15f;
                    shot.SeekFrom = 5;
                    shot.Trail = Mathf.Max(shot.Trail, 0.5f);
                }
                Shoot(def, l, from, dir, 1f, k, shot, target);
                Sound(def, 0.55f);
                ArtShot(def, up ? 60f : (float?)null);
            }
            One(0, first, Centre(hero), AimWay(hero));
            var tp = TerraProjectiles.Instance;
            for (int i = 1; i < n && tp != null; i++)
            {
                int at = i;
                tp.Later(gap * i, (from, dir) =>
                {
                    var mod = TerraMod.Instance;
                    if (mod == null || mod.Inventory.Current.Def != def || !TakeLoad(def, out var l)) return;
                    One(at, l, from, dir);
                });
            }
            return true;
        }

        /// <summary>n from the sky over the cursor (as high as the open air goes, 22 blocks at most) over `seconds`, damage × k; a load for every four.</summary>
        static bool Sky(HeroController hero, ItemDef def, int n, float seconds, float k)
        {
            if (!TakeLoad(def, out var first)) return false;
            TurnToAim(hero);
            var target = Aim();
            const float Block = 16f * Effects.TerraParticles.Px;
            float high = 22f * Block;
            var hit = Physics2D.Raycast(target, Vector2.up, high, 1 << World.ChunkColliders.TerrainLayer);
            if (hit.collider != null) high = Mathf.Max(3f * Block, hit.distance - Block);
            var tp = TerraProjectiles.Instance;
            Load l = first;
            for (int i = 0; i < n && tp != null; i++)
            {
                int at = i;
                int ticks = Mathf.RoundToInt(seconds * 60f * i / Mathf.Max(1, n));
                void Drop()
                {
                    if (at > 0 && at % 4 == 0)
                    {
                        var mod = TerraMod.Instance;
                        if (mod == null || mod.Inventory.Current.Def != def || !TakeLoad(def, out l)) return;
                    }
                    var spot = target + new Vector2(Random.Range(-3.5f, 3.5f) * Block, 0f);
                    var from = spot + new Vector2(Random.Range(-1.5f, 1.5f) * Block, high);
                    Shoot(def, l, from, (spot - from).normalized, 1.1f, k, new RangedShot { Trail = 0.5f }, spot);
                    if (at % 3 == 0) Sound(def, 0.4f);
                }
                if (ticks <= 0) Drop();
                else tp.Later(ticks, (f, d) => Drop());
            }
            ArtShot(def, 75f);
            return true;
        }

        /// <summary>n set out in a row either side of the cursor (2.5 blocks apart), each its own load.</summary>
        static bool Mines(HeroController hero, ItemDef def, int n)
        {
            TurnToAim(hero);
            var target = Aim();
            var from = Centre(hero);
            const float Block = 16f * Effects.TerraParticles.Px;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (!TakeLoad(def, out var l)) break;
                var spot = target + new Vector2((i - (n - 1) * 0.5f) * 2.5f * Block, 0f);
                Shoot(def, l, from, (spot - from).normalized, 1f, 0.8f, new RangedShot { Trail = 0.3f }, spot);
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            ArtShot(def);
            return true;
        }

        /// <summary>n of its flames out all round the player, damage × k each; a load for every four.</summary>
        static bool Ring(HeroController hero, ItemDef def, int n, float k)
        {
            var from = Centre(hero);
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 4 == 0 && !TakeLoad(def, out l)) break;
                float a = i / (float)n * Mathf.PI * 2f;
                Shoot(def, l, from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 1f, k, new RangedShot());
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            ArtShot(def);
            return true;
        }
    }
}
