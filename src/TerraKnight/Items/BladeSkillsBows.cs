using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;
using Offshoot = TerraKnight.Items.TerraProjectiles.Offshoot;
using SpawnWay = TerraKnight.Items.TerraProjectiles.SpawnWay;

namespace TerraKnight.Items
{
    /// <summary>
    /// The later bows', repeaters' and thrown weapons' right-click arts, each its own (the user's 10-27 "远程武器的差异化不够，
    /// 比如弓全是贯穿，弩全是连射…我希望每一把武器大家玩起来都会感觉耳目一新，除非是同阶段的过渡换皮武器就没必要了，右键的特效也应该
    /// 带特殊粒子效果"): over the shared ones of BladeSkillsRanged.cs, which the first stage's bows and throws keep. The Tendon Bow's
    /// arrow bursts in ichor, the Demon Bow looses a demon scythe, the Ash Wood Bow's arrow throws out fireballs, Bee's Knees'
    /// bursts into a hive, the Molten Fury's brings fire down after it, the Pearlwood Bow calls hallowed stars, the Ice Bow's
    /// shatters into ice, the Marrow's into a cage of bone, the Shadowflame Bow's raises tentacles, the Aerial Bane's
    /// calls arrows from the sky, the Tsunami looses a wall of arrows, the Eventide a ring of seeking ones round the player,
    /// the Pulse Bow's arcs from enemy to enemy, the Phantasm's phantom arrows pour from around the player. Each repeater
    /// its own; the javelins, grenades, molotovs and stars their own throws.
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterBowArts()
        {
            void Press(ItemType t, string name, float cooldown, string tip, string tipEn, System.Func<HeroController, ItemDef, bool> press) =>
                RegisterArt(t, new KindArt { Name = name, Cooldown = cooldown, Tip = tip, TipEn = tipEn, Press = press });
            void Charge(ItemType t, string name, float full, string tip, string tipEn, System.Action<HeroController, ItemDef, float> release) =>
                RegisterArt(t, new KindArt { Name = name, Charged = true, Full = full, Aimed = true, Pose = 0f, ChargeSound = "tk_bow_draw", Tip = tip, TipEn = tipEn, Release = release });

            // ---- bows
            Charge(ItemType.TendonBow, "血筋箭", 0.9f, "右键：按住拉弓，松开射出一支血箭，贯穿两个敌人，命中处炸出一圈灵液，降低敌人防御（伤害最多 3 倍）",
                "Right click: hold to draw, release to loose a blood arrow through two enemies that bursts into a ring of ichor where it strikes, lowering defense (up to 3× damage)",
                (h, d, c) => Power(h, d, 1.4f + 1.6f * c, new RangedShot { Pierce = 2, Speed = 1.3f, Debuff = EnemyDebuffs.Ichor, DebuffTicks = 420, OnHit = Off(ProjKind.IchorSplash, 6, SpawnWay.Ring, 0.3f, 6f), Trail = 1f, TrailDust = 5 }));
            Press(ItemType.DemonBow, "恶魔镰刃", 3f, "右键：射出一把旋转的恶魔镰刀，越飞越快，带暗影焰", "Right click: loose a spinning demon scythe that speeds up as it goes, wreathed in shadowflame",
                (h, d) => Kind(h, d, ProjKind.DemonScythe, 0.35f, 2f, new RangedShot { Size = 1.2f, Debuff = EnemyDebuffs.Shadowflame, DebuffTicks = 300, Trail = 1f, TrailDust = 27 }));
            Charge(ItemType.AshWoodBow, "余烬风暴", 1f, "右键：按住拉弓，松开射出一支燃烧的箭，命中处炸开并抛出六团火球（伤害最多 3 倍）",
                "Right click: hold to draw, release to loose a burning arrow that bursts where it strikes, throwing out six fireballs (up to 3× damage)",
                (h, d, c) => PowerAs(h, d, ProjKind.FlamingArrow, 1.5f + 1.5f * c, new RangedShot { Burst = 96f, BurstDamage = 0.5f, Debuff = EnemyDebuffs.OnFire, DebuffTicks = 360, OnHit = Off(ProjKind.BallofFire, 6, SpawnWay.Ring, 0.3f, 6f), Trail = 1f, TrailDust = 6 }));
            Press(ItemType.BeesKnees, "蜂巢", 4f, "右键：射出三支蜂箭，命中后炸出一窝蜜蜂（用什么箭都是蜂箭）", "Right click: loose three bee arrows (whatever the arrows) that burst into a swarm of bees where they strike",
                (h, d) => FanAs(h, d, ProjKind.BeeArrow, 3, 10f, 0.9f, new RangedShot { OnHit = Off(ProjKind.Bee, 6, SpawnWay.Ring, 0.25f, 4f), Trail = 0.6f }));
            Charge(ItemType.MoltenFury, "熔火天降", 1f, "右键：按住拉弓，松开射出一支熔岩箭，命中处炸开，天上随之落下三支火箭（伤害最多 3.5 倍）",
                "Right click: hold to draw, release to loose a molten arrow that bursts where it strikes, three fire arrows falling after it (up to 3.5× damage)",
                (h, d, c) => PowerAs(h, d, ProjKind.HellfireArrow, 1.5f + 2f * c, new RangedShot { Speed = 1.4f, Burst = 128f, BurstDamage = 0.6f, Debuff = EnemyDebuffs.Hellfire, DebuffTicks = 300, OnHit = Off(ProjKind.FlamingArrow, 3, SpawnWay.Fall, 0.5f), Trail = 1f, TrailDust = 6 }));
            Press(ItemType.PearlwoodBow, "星辉", 5f, "右键：在鼠标处召下八颗神圣之星", "Right click: call eight hallowed stars down on the cursor",
                (h, d) => Stars(h, d, ProjKind.HallowStar, 8, 0.8f, 0.6f));
            Charge(ItemType.IceBow, "冰封", 1f, "右键：按住拉弓，松开射出一支寒冰箭，命中处碎成一圈冰矢，冻伤敌人（伤害最多 3 倍）",
                "Right click: hold to draw, release to loose a frost arrow that shatters into a ring of ice bolts where it strikes, frostburning enemies (up to 3× damage)",
                (h, d, c) => Power(h, d, 1.4f + 1.6f * c, new RangedShot { Pierce = 2, Speed = 1.3f, Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 480, OnHit = Off(ProjKind.IceBolt, 8, SpawnWay.Ring, 0.3f, 7f), Trail = 1f, TrailDust = 135 }));
            Charge(ItemType.Marrow, "骨牢", 1f, "右键：按住拉弓，松开射出一支骨箭，命中处炸出一圈骨片，让敌人流血（伤害最多 3 倍）",
                "Right click: hold to draw, release to loose a bone arrow that bursts into a cage of bone shards where it strikes, making enemies bleed (up to 3× damage)",
                (h, d, c) => Power(h, d, 1.5f + 1.5f * c, new RangedShot { Pierce = 2, Speed = 1.3f, Debuff = EnemyDebuffs.Bleeding, DebuffTicks = 420, OnHit = Off(ProjKind.BoneShard, 10, SpawnWay.Ring, 0.25f, 6f), Trail = 1f, TrailDust = 26 }));
            Press(ItemType.ShadowFlameBow, "暗影触手", 4f, "右键：射出三支暗影焰箭，命中处伸出暗影触手缠打周围的敌人", "Right click: loose three shadowflame arrows; shadow tentacles lash out where they strike",
                (h, d) => Fan(h, d, 3, 10f, 0.8f, new RangedShot { OnHit = Off(ProjKind.ShadowflameTentacle, 3, SpawnWay.Ring, 0.3f, 3f), Debuff = EnemyDebuffs.Shadowflame, DebuffTicks = 300, Trail = 0.8f, TrailDust = 27 }));
            Press(ItemType.ChlorophyteShotbow, "孢子云阵", 5f, "右键：一次射出九支会追踪敌人的箭，命中处冒出孢子云", "Right click: loose nine seeking arrows at once that leave spore clouds where they strike",
                (h, d) => Fan(h, d, 9, 26f, 0.6f, new RangedShot { Home = 3.5f, OnHit = Off(ProjKind.SporeCloud, 1, SpawnWay.Ring, 0.25f, 0.3f), Trail = 0.5f, TrailDust = 44 }));
            Charge(ItemType.AerialBane, "天穹裂击", 1f, "右键：按住拉弓，松开射出一支天穹箭，命中处从天上落下六支箭（伤害最多 3 倍）",
                "Right click: hold to draw, release to loose a sky arrow that calls six more down from the sky where it strikes (up to 3× damage)",
                (h, d, c) => Power(h, d, 1.4f + 1.6f * c, new RangedShot { Pierce = 2, Speed = 1.4f, OnHit = Off(ProjKind.BetsyArrow, 6, SpawnWay.Fall, 0.45f), Tracer = true, Trail = 1f, TrailDust = 15 }));
            Press(ItemType.Tsunami, "海啸之墙", 5f, "右键：身后竖起一排九支箭，一齐射向鼠标处", "Right click: nine arrows rise in a wall behind you and fly at the cursor together",
                (h, d) => Wall(h, d, 9, 0.7f));
            Press(ItemType.Eventide, "暮光仙阵", 5f, "右键：身边环起六支箭，各自追向敌人", "Right click: six arrows ring you, each seeking out an enemy",
                (h, d) => Halo(h, d, 6, 0.7f));
            Charge(ItemType.PulseBow, "脉冲电网", 1f, "右键：按住拉弓，松开射出一道脉冲，贯穿敌人，电弧在四个敌人之间跳动（伤害最多 3.5 倍）",
                "Right click: hold to draw, release to loose a pulse through enemies that arcs on through four more (up to 3.5× damage)",
                (h, d, c) => Power(h, d, 1.5f + 2f * c, new RangedShot { Pierce = 6, Speed = 1.4f, Chain = 4, ChainDamage = 0.5f, Tracer = true, Trail = 1f, TrailDust = 226 }));
            Press(ItemType.Phantasm, "幻影弓阵", 7f, "右键：身后浮现幻影弓，接连射出十六支幻影箭", "Right click: phantom bows appear behind you and loose sixteen phantom arrows one after another",
                (h, d) => Phantoms(h, d, 16, 1.6f, 0.5f));

            // The bows whose own arrows are their art: what they say, whatever the ammo.
            Press(ItemType.HellwingBow, "蝠群", 3.5f, "右键：一次放出六只地狱蝙蝠扑向敌人（用什么箭都是蝙蝠）", "Right click: send six Hellwing bats at once (whatever the arrows)",
                (h, d) => Bats(h, d, 6, 22f, 0.6f));
            Press(ItemType.BloodRainBow, "血雨倾盆", 6f, "右键：在鼠标处降下一场两倍的血雨", "Right click: bring a downpour of blood twice as heavy down on the cursor",
                (h, d) => RainAs(h, d, 20, 1.1f, 0.55f, i => ProjKind.BloodRainArrow, () => new RangedShot { Debuff = EnemyDebuffs.Bleeding, DebuffTicks = 240 }));
            Press(ItemType.PhantomPhoenix, "凤凰火雨", 6f, "右键：在鼠标处降下一阵燃烧的箭雨，夹着几只火凤凰", "Right click: rain burning arrows down on the cursor, a few fire phoenixes among them",
                (h, d) => RainAs(h, d, 16, 1f, 0.6f, i => i % 4 == 3 ? ProjKind.Phoenix : ProjKind.FlamingArrow, () => new RangedShot { Debuff = EnemyDebuffs.OnFire, DebuffTicks = 300, Trail = 0.6f, TrailDust = 6 }));

            // ---- repeaters
            Press(ItemType.PalladiumRepeater, "钯金爆燃", 4f, "右键：连续快速射出五发，命中处炸开", "Right click: fire five bolts in a quick run that burst where they strike",
                (h, d) => Volley(h, d, 5, 4, 0.75f, 0f, new RangedShot { Burst = 56f, BurstDamage = 0.4f, Trail = 0.5f, TrailDust = 6 }));
            Press(ItemType.MythrilRepeater, "穿刺三矢", 3f, "右键：一次射出三支贯穿三个敌人的弩箭", "Right click: fire three bolts at once that go through three enemies",
                (h, d) => Fan(h, d, 3, 6f, 1f, new RangedShot { Pierce = 3, Speed = 1.3f, Tracer = true }));
            Press(ItemType.OrichalcumRepeater, "花瓣风暴", 4f, "右键：一次射出五支弩箭，命中处飞出山铜花瓣", "Right click: fire five bolts at once that throw out orichalcum petals where they strike",
                (h, d) => Fan(h, d, 5, 14f, 0.7f, new RangedShot { OnHit = Off(ProjKind.OrichalcumPetal, 4, SpawnWay.Ahead, 0.25f, 6f), Trail = 0.5f }));
            Charge(ItemType.AdamantiteRepeater, "破甲重矢", 0.9f, "右键：按住上弦，松开射出一支重矢，贯穿五个敌人并降低防御（伤害最多 3.3 倍）",
                "Right click: hold to wind, release to fire a heavy bolt through five enemies that lowers their defense (up to 3.3× damage)",
                (h, d, c) => Power(h, d, 1.5f + 1.8f * c, new RangedShot { Pierce = 5, Speed = 1.5f, Debuff = EnemyDebuffs.Ichor, DebuffTicks = 420, Tracer = true, Trail = 1f }));
            Press(ItemType.TitaniumRepeater, "钛金碎片", 4f, "右键：一次射出五支弩箭，命中处碎出三片钛金碎片", "Right click: fire five bolts at once that shatter into three titanium shards where they strike",
                (h, d) => Fan(h, d, 5, 14f, 0.7f, new RangedShot { Shards = 3, ShardKind = ProjKind.TitaniumShard, ShardDamage = 0.3f, Trail = 0.5f }));
            Press(ItemType.HallowedRepeater, "圣光齐射", 5f, "右键：连续射出六发，每发命中处落下一颗神圣之星", "Right click: fire six bolts in a run, each calling a hallowed star down where it strikes",
                (h, d) => Volley(h, d, 6, 4, 0.75f, 0f, new RangedShot { OnHit = Off(ProjKind.HallowStar, 1, SpawnWay.Fall, 0.45f), Trail = 0.5f, TrailDust = 57 }));

            // ---- thrown
            Press(ItemType.StarAnise, "星茴环", 3f, "右键：向四周掷出一圈八个星形茴香", "Right click: throw a ring of eight star anise out all round you",
                (h, d) => Ring(h, d, 8, 0.6f));
            Press(ItemType.Beenade, "蜂巢炸弹", 3f, "右键：一次扔出三个蜜蜂手榴弹，炸开时各多放出三只蜜蜂", "Right click: throw three beenades at once, each letting out three more bees as it goes off",
                (h, d) => Fan(h, d, 3, 12f, 0.6f, new RangedShot { OnDeath = Off(ProjKind.Bee, 3, SpawnWay.Ring, 0.25f, 4f) }));
            Press(ItemType.MolotovCocktail, "火墙", 3f, "右键：在鼠标两侧扔下一排三瓶燃烧瓶", "Right click: throw a row of three molotovs either side of the cursor",
                (h, d) => Mines(h, d, 3));
            Press(ItemType.FrostDaggerfish, "冰鱼群", 2.5f, "右键：一次扇形掷出五条寒霜飞鱼，命中处碎出冰矢", "Right click: throw five frost daggerfish at once in a fan that shatter into ice bolts where they strike",
                (h, d) => Fan(h, d, 5, 14f, 0.6f, new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 300, OnHit = Off(ProjKind.IceBolt, 2, SpawnWay.Ahead, 0.25f, 6f), Trail = 0.5f, TrailDust = 135 }));
            Press(ItemType.Javelin, "天降标枪", 3f, "右键：在鼠标处从天上落下五支标枪", "Right click: bring five javelins down from the sky on the cursor",
                (h, d) => Stars(h, d, ProjKind.JavelinProj, 5, 0.6f, 0.7f));
            Press(ItemType.PartyGirlGrenade, "派对时间", 4f, "右键：向四周扔出五个快乐手榴弹，炸开让敌人混乱", "Right click: throw five happy grenades out all round you that confuse enemies as they go off",
                (h, d) => Ring(h, d, 5, 0.6f, new RangedShot { Debuff = EnemyDebuffs.Confused, DebuffTicks = 180 }));
        }

        /// <summary>A charge let go as a projectile of a kind of its own (the Ash Wood Bow's burning arrow), whatever the ammo, damage × k.</summary>
        static void PowerAs(HeroController hero, ItemDef def, ProjKind kind, float k, RangedShot r)
        {
            if (!TakeLoad(def, out var l)) return;
            TurnToAim(hero);
            ShootAs(def, l, kind, Centre(hero), AimWay(hero) * Mathf.Max(10f, l.Speed), k, r);
            Sound(def, 0.8f);
            TerraMod.Instance?.PlaySound("Item_5", 0.4f, 0.8f);
            ArtShot(def);
            SwordFx.Instance?.RangedPower(def);
        }

        /// <summary>n of a kind of its own in a fan `spread` degrees either side (Bee's Knees' bee arrows), whatever the ammo; a load for every three.</summary>
        static bool FanAs(HeroController hero, ItemDef def, ProjKind kind, int n, float spread, float k, RangedShot r)
        {
            TurnToAim(hero);
            var way = AimWay(hero);
            var from = Centre(hero);
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 3 == 0 && !TakeLoad(def, out l)) break;
                float off = n == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (n - 1f)) + Random.Range(-1.5f, 1.5f);
                ShootAs(def, l, kind, from, Rot(way, off) * Mathf.Max(9f, l.Speed), k, r.Copy());
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            ArtShot(def);
            return true;
        }

        /// <summary>The Hellwing Bow's bats: n Hellwing bats in a fan, each bending as the bow's own do; a load for every three.</summary>
        static bool Bats(HeroController hero, ItemDef def, int n, float spread, float k)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return false;
            TurnToAim(hero);
            var way = AimWay(hero);
            var from = Centre(hero);
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 3 == 0 && !TakeLoad(def, out l)) break;
                float off = n == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (n - 1f)) + Random.Range(-3f, 3f);
                var vel = Rot(way, off) * Mathf.Max(8f, l.Speed);
                var bend = Rot(vel, Random.Range(-10f, 10f));
                TerraProjectiles.SpawnClass = def.Class;
                TerraProjectiles.SpawnForge = Forging.RangedLevel(def);
                var keep = TerraProjectiles.SpawnRanged;
                TerraProjectiles.SpawnRanged = new RangedShot { Item = def, Local = true, Trail = 0.4f, Debuff = EnemyDebuffs.OnFire, DebuffTicks = 300 };
                try { tp.ShootHellwing(ProjKind.HellwingBat, from, vel, Mathf.Max(1, Mathf.RoundToInt(l.Damage * k)), l.Knockback, Equipment.Crit(def), new Vector2(bend.x, -bend.y)); }
                finally
                {
                    TerraProjectiles.SpawnRanged = keep;
                    TerraProjectiles.SpawnClass = DamageClass.None;
                    TerraProjectiles.SpawnForge = 0;
                }
                DevRangedArtShots++;
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            ArtShot(def);
            return true;
        }

        /// <summary>A rain of its own kinds (the Blood Rain Bow's blood, the Phantom Phoenix's fire) on the cursor over `seconds`, whatever the ammo; a load for every four.</summary>
        static bool RainAs(HeroController hero, ItemDef def, int n, float seconds, float k, System.Func<int, ProjKind> kindOf, System.Func<RangedShot> make)
        {
            if (!TakeLoad(def, out var first)) return false;
            TurnToAim(hero);
            var target = Aim();
            const float Block = 16f * Effects.TerraParticles.Px;
            float high = 18f * Block;
            var hit = Physics2D.Raycast(target, Vector2.up, high, 1 << World.ChunkColliders.TerrainLayer);
            if (hit.collider != null) high = Mathf.Max(3f * Block, hit.distance - Block);
            var tp = TerraProjectiles.Instance;
            Load l = first;
            for (int i = 0; i < n && tp != null; i++)
            {
                int at = i;
                void Drop()
                {
                    if (at > 0 && at % 4 == 0 && !TakeLoad(def, out l)) return;
                    var spot = target + new Vector2(Random.Range(-3.5f, 3.5f) * Block, 0f);
                    var from = spot + new Vector2(Random.Range(-1.5f, 1.5f) * Block, high);
                    ShootAs(def, l, kindOf(at), from, (spot - from).normalized * Mathf.Max(12f, l.Speed * 1.1f), k, make(), spot.y + 0.5f);
                    if (at % 3 == 0) Sound(def, 0.4f);
                }
                int ticks = Mathf.RoundToInt(seconds * 60f * i / Mathf.Max(1, n));
                if (ticks <= 0) Drop();
                else tp.Later(ticks, (f, d) => Drop());
            }
            ArtShot(def, 75f);
            return true;
        }

        /// <summary>One of a kind of the weapon's own (a demon scythe off the Demon Bow) at the cursor, speed in px a tick × the load's, damage × k.</summary>
        static bool Kind(HeroController hero, ItemDef def, ProjKind kind, float speedK, float k, RangedShot r)
        {
            if (!TakeLoad(def, out var l)) return false;
            TurnToAim(hero);
            ShootAs(def, l, kind, Centre(hero), AimWay(hero) * Mathf.Max(4f, l.Speed * speedK), k, r);
            Sound(def, 0.8f);
            TerraMod.Instance?.PlaySound("Item_8", 0.5f);
            ArtShot(def);
            SwordFx.Instance?.RangedPower(def);
            return true;
        }

        /// <summary>A projectile of a kind for an art, carrying r, damage × k (its class and forge the weapon's).</summary>
        static void ShootAs(ItemDef def, in Load l, ProjKind kind, Vector2 from, Vector2 vel, float k, RangedShot r, float? floor = null)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            r.Item = def;
            r.Local = true;
            TerraProjectiles.SpawnClass = def.Class;
            TerraProjectiles.SpawnForge = Forging.RangedLevel(def);
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { tp.ShootFalling(kind, from, vel, Mathf.Max(1, Mathf.RoundToInt(l.Damage * k)), l.Knockback, Equipment.Crit(def), floor); }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
                TerraProjectiles.SpawnForge = 0;
            }
            DevRangedArtShots++;
        }

        /// <summary>n of a kind down from the sky over the cursor over `seconds` (hallowed stars, javelins), damage × k; a load for every four.</summary>
        static bool Stars(HeroController hero, ItemDef def, ProjKind kind, int n, float seconds, float k)
        {
            if (!TakeLoad(def, out var first)) return false;
            TurnToAim(hero);
            var target = Aim();
            const float Block = 16f * Effects.TerraParticles.Px;
            float high = 18f * Block;
            var hit = Physics2D.Raycast(target, Vector2.up, high, 1 << World.ChunkColliders.TerrainLayer);
            if (hit.collider != null) high = Mathf.Max(3f * Block, hit.distance - Block);
            var tp = TerraProjectiles.Instance;
            Load l = first;
            for (int i = 0; i < n && tp != null; i++)
            {
                int at = i;
                void Drop()
                {
                    if (at > 0 && at % 4 == 0 && !TakeLoad(def, out l)) return;
                    var spot = target + new Vector2(Random.Range(-3f, 3f) * Block, 0f);
                    var from = spot + new Vector2(Random.Range(-2f, 2f) * Block, high);
                    ShootAs(def, l, kind, from, (spot - from).normalized * 14f, k, new RangedShot { Trail = 0.6f }, spot.y + 0.5f);
                    if (at % 2 == 0) TerraMod.Instance?.PlaySound("Item_9", 0.35f);
                }
                int ticks = Mathf.RoundToInt(seconds * 60f * i / Mathf.Max(1, n));
                if (ticks <= 0) Drop();
                else tp.Later(ticks, (f, d) => Drop());
            }
            ArtShot(def, 75f);
            return true;
        }

        /// <summary>The Tsunami's wall: n arrows risen in a column behind the player, all at the cursor together; a load for every three.</summary>
        static bool Wall(HeroController hero, ItemDef def, int n, float k)
        {
            TurnToAim(hero);
            var way = AimWay(hero);
            var up = new Vector2(-way.y, way.x);
            if (up.y < 0f) up = -up;
            const float Block = 16f * Effects.TerraParticles.Px;
            var centre = Centre(hero) - way * 1.2f * Block;
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 3 == 0 && !TakeLoad(def, out l)) break;
                var from = centre + up * (i - (n - 1) * 0.5f) * 0.9f * Block;
                var to = Aim() + up * (i - (n - 1) * 0.5f) * 0.3f * Block;
                ShootAs(def, l, l.Kind == ProjKind.None ? ProjKind.WoodenArrow : l.Kind, from, (to - from).normalized * Mathf.Max(10f, l.Speed), k, new RangedShot { Trail = 0.8f, TrailDust = 33 });
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            TerraMod.Instance?.PlaySound("Item_21", 0.5f);
            ArtShot(def);
            return true;
        }

        /// <summary>The Eventide's halo: n arrows from a ring round the player, each seeking an enemy; a load for every three.</summary>
        static bool Halo(HeroController hero, ItemDef def, int n, float k)
        {
            TurnToAim(hero);
            const float Block = 16f * Effects.TerraParticles.Px;
            var centre = Centre(hero);
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 3 == 0 && !TakeLoad(def, out l)) break;
                float a = i / (float)n * Mathf.PI * 2f + Mathf.PI / 2f;
                var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f * Block;
                ShootAs(def, l, l.Kind == ProjKind.None ? ProjKind.WoodenArrow : l.Kind, centre + off, ((Aim() - centre).normalized * 0.6f + off.normalized * 0.4f).normalized * Mathf.Max(9f, l.Speed), k,
                    new RangedShot { Home = 9f, Trail = 1f, TrailScale = 1f });
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            TerraMod.Instance?.PlaySound("Item_29", 0.45f);
            ArtShot(def);
            return true;
        }

        /// <summary>The Phantasm's bows: n phantom arrows over `seconds` from about the player at the cursor; a load for every two.</summary>
        static bool Phantoms(HeroController hero, ItemDef def, int n, float seconds, float k)
        {
            if (!TakeLoad(def, out var first)) return false;
            TurnToAim(hero);
            const float Block = 16f * Effects.TerraParticles.Px;
            var tp = TerraProjectiles.Instance;
            Load l = first;
            for (int i = 0; i < n && tp != null; i++)
            {
                int at = i;
                void Loose()
                {
                    var h = HeroController.SilentInstance;
                    if (h == null || at > 0 && at % 2 == 0 && !TakeLoad(def, out l)) return;
                    var centre = Centre(h);
                    var from = centre + new Vector2(-Facing(h) * Random.Range(0.5f, 2.5f), Random.Range(0.5f, 3f)) * Block;
                    ShootAs(def, l, l.Kind == ProjKind.None ? ProjKind.WoodenArrow : l.Kind, from, (Aim() - from).normalized * Mathf.Max(12f, l.Speed), k,
                        new RangedShot { Trail = 1f, TrailDust = 229, Speed = 1.2f });
                    if (at % 3 == 0) TerraMod.Instance?.PlaySound("Item_5", 0.35f, 1.2f);
                    ArtShot(def);
                }
                int ticks = Mathf.RoundToInt(seconds * 60f * i / Mathf.Max(1, n));
                if (ticks <= 0) Loose();
                else tp.Later(ticks, (f, d) => Loose());
            }
            return true;
        }

        /// <summary>n of the thrown weapon out all round the player, damage × k each; one used up for every two.</summary>
        static bool Ring(HeroController hero, ItemDef def, int n, float k, RangedShot r)
        {
            var from = Centre(hero);
            Load l = default;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 0 && !TakeLoad(def, out l)) break;
                float a = i / (float)n * Mathf.PI * 2f;
                Shoot(def, l, from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 1f, k, r.Copy());
                made++;
            }
            if (made == 0) return false;
            Sound(def, 0.9f);
            RangedFire.Shot(def);
            return true;
        }
    }
}
