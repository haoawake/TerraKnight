namespace TerraKnight.Items
{
    /// <summary>
    /// The guns and bows of sweep R4a, with Terraria's own numbers (Item.SetDefaults) and the held
    /// offsets of Main.DrawPlayerItemPos; how each shoots is in ShootPatternsGuns.cs.
    /// </summary>
    internal static partial class ItemDb
    {
        static void AddGuns(System.Collections.Generic.List<ItemDef> list)
        {
            // The Fallen Star is the Star Cannons' ammo (I:2302-2315: ammo FallenStar, used up, no shot of its own).
            var star = list.Find(d => d.Type == ItemType.FallenStar);
            if (star != null)
            {
                star.Ammo = AmmoKind.FallenStar;
                star.Consumable = true;
                star.Tooltip = "材料\n弹药\n日出后消失";
            }
            ItemDef G(ItemType t, string name, int id, int dmg, float kb, int use, float speed, string sound, int rare, float hx, float hy, bool auto = true,
                int crit = 0, float scale = 1f, string tip = null)
            {
                var d = Held(Gun(t, name, id, dmg, kb, use, speed, sound, price: 0, tier: 3, crit: crit, rare: rare, tip: tip, scale: scale), hx, hy);
                d.AutoReuse = auto;
                return d;
            }
            ItemDef B(ItemType t, string name, int id, int dmg, float kb, int use, float speed, string sound, int rare, float hx, float hy, bool auto = true,
                int crit = 0, string tip = null)
            {
                var d = Held(Bow(t, name, id, dmg, kb, use, speed, price: 0, tier: 3, rare: rare, tip: tip), hx, hy);
                d.UseSound = sound;
                d.AutoReuse = auto;
                d.Crit = crit;
                return d;
            }
            // I:7741-7754, 20588-20602, 22829-22844, 33553-33566, 38654-38661: the shotguns and pistols.
            list.Add(G(ItemType.Shotgun, "霰弹枪", 534, 24, 6.5f, 45, 7f, "Item_36", 4, -2f, 1f, auto: false, tip: "射出一片子弹"));
            list.Add(G(ItemType.RedRyder, "红莱德枪", 1870, 20, 3.75f, 38, 8f, "Item_11", 1, -8f, 2f, tip: "“别把自己的眼球射出来了！”"));
            list.Add(G(ItemType.Revolver, "左轮手枪", 2269, 20, 4.5f, 22, 16f, "Item_41", 2, 2f, 2f, auto: false, scale: 0.85f, tip: "时机精准的射击可以减少冷却时间并提高暴击率"));
            list.Add(G(ItemType.OnyxBlaster, "玛瑙爆破枪", 3788, 24, 6.5f, 48, 7f, "Item_36", 4, 10f, 0f, auto: false));
            list.Add(G(ItemType.QuadBarrelShotgun, "四管霰弹枪", 4703, 14, 6.5f, 55, 7f, "Item_36", 3, -3f, -2f, auto: false, tip: "“当两管或三管未达预期时”"));
            // I:26709-26725: the Xenopopper's bubbles (glowmask 38).
            var xeno = G(ItemType.Xenopopper, "外星霰弹枪", 2797, 45, 3f, 21, 12f, "Item_95", 8, 0f, 0f);
            xeno.Glow = "Glow_38";
            list.Add(xeno);
            // I:28940-28954, 41000-41013: the Toxikarp's bubbles, the Pew-matic Horn's things (no ammo damage of their own changes).
            var karp = G(ItemType.Toxikarp, "毒弹枪", 3210, 43, 3f, 10, 8.5f, "Item_111", 5, 10f, 0f, tip: "吐出毒泡");
            karp.Shoot = ProjKind.ToxicBubble;
            karp.UseAmmo = AmmoKind.None;
            list.Add(karp);
            var horn = G(ItemType.PewMaticHorn, "气喇叭", 5117, 20, 1f, 15, 14f, "Item_61", 0, -1f, 0f, tip: "可将子弹转换成随机的东西\n“允许无线传输损伤！”");
            horn.Shoot = ProjKind.PewMaticNote;
            list.Add(horn);
            // I:3810-3830, 35671-35683: the Star Cannons, Fallen Stars for ammo (the Super Star Shooter silent but for its star's twinkle).
            var cannon = G(ItemType.StarCannon, "星星炮", 197, 55, 3f, 12, 14f, "Item_9", 2, -5f, 4f, tip: "射出坠落之星");
            cannon.Shoot = ProjKind.StarCannonStar;
            cannon.UseAmmo = AmmoKind.FallenStar;
            list.Add(cannon);
            var super = G(ItemType.SuperStarCannon, "超级星星炮", 4060, 60, 5f, 18, 20f, null, 3, -2f, 4f, tip: "“当然，天才，但你要去哪弄这么多星星呢？”");
            super.Shoot = ProjKind.SuperStar;
            super.UseAmmo = AmmoKind.FallenStar;
            list.Add(super);
            // I:3296-3310: the Harpoon, no ammo, one out at a time.
            var harpoon = G(ItemType.Harpoon, "鱼叉枪", 160, 25, 6f, 30, 15f, "Item_10", 2, -8f, 0f, scale: 1.1f);
            harpoon.Shoot = ProjKind.HarpoonProj;
            harpoon.UseAmmo = AmmoKind.None;
            list.Add(harpoon);
            // I:31161-31179, 31730-31748, 34050-34067: held out while the button is (channel, the weapon drawn by its projectile);
            // the Vortex Beater and the Phantasm save two rounds in three (Pl:54223-54234).
            ItemDef H(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind held, AmmoKind ammo, string sound, int rare, float save, string tip)
            {
                var d = ammo == AmmoKind.Arrow ? Bow(t, name, id, dmg, kb, use, 20f, price: 0, tier: 3, rare: rare, tip: tip) : Gun(t, name, id, dmg, kb, use, 20f, sound, price: 0, tier: 3, rare: rare, tip: tip);
                d.Shoot = held;
                d.UseSound = sound;
                d.Channel = true;
                d.NoUseGraphic = true;
                d.Bonus.AmmoSave = save;
                return d;
            }
            list.Add(H(ItemType.VortexBeater, "星旋机枪", 3475, 50, 2f, 20, ProjKind.VortexBeaterHeld, AmmoKind.Bullet, null, 10, 2f / 3f,
                "66%几率省下弹药\n“劈劈啪啪，乒乒乓乓，一顿混乱，惨绝人寰。”"));
            list.Add(H(ItemType.Phantasm, "幻影弓", 3540, 50, 2f, 12, ProjKind.PhantasmHeld, AmmoKind.Arrow, "Item_5", 10, 2f / 3f, "66%几率省下弹药"));
            list.Add(H(ItemType.PhantomPhoenix, "幽灵凤凰", 3854, 32, 2f, 18, ProjKind.PhoenixBowHeld, AmmoKind.Arrow, "Item_5", 5, 0f, "利用不朽火焰的力量"));
            // --- Sweep R4b: rockets (I:10394-10438, 37579-37673: no shot of their own, the launcher picks it) and the launchers
            // (I:10233-10283, 21088-21102; held offsets M:3442-3784).
            list.Add(Ammo(ItemType.RocketI, "火箭一型", 771, AmmoKind.Rocket, ProjKind.None, 40, 4f, 0f, price: 0, tier: 3, tip: "爆破半径小。不会破坏图格"));
            list.Add(Ammo(ItemType.RocketII, "火箭二型", 772, AmmoKind.Rocket, ProjKind.None, 40, 4f, 0f, price: 0, tier: 3, rare: 1, tip: "爆破半径小。会破坏图格"));
            list.Add(Ammo(ItemType.RocketIII, "火箭三型", 773, AmmoKind.Rocket, ProjKind.None, 65, 6f, 0f, price: 0, tier: 3, rare: 1, tip: "爆破半径大。不会破坏图格"));
            list.Add(Ammo(ItemType.RocketIV, "火箭四型", 774, AmmoKind.Rocket, ProjKind.None, 65, 6f, 0f, price: 0, tier: 3, rare: 2, tip: "爆破半径大。会破坏图格"));
            list.Add(Ammo(ItemType.ClusterRocketI, "集束火箭一型", 4445, AmmoKind.Rocket, ProjKind.None, 50, 4f, 0f, price: 0, tier: 3, tip: "不会破坏图格"));
            list.Add(Ammo(ItemType.ClusterRocketII, "集束火箭二型", 4446, AmmoKind.Rocket, ProjKind.None, 50, 4f, 0f, price: 0, tier: 3, tip: "会破坏图格"));
            list.Add(Ammo(ItemType.MiniNukeI, "迷你核弹一型", 4457, AmmoKind.Rocket, ProjKind.None, 75, 4f, 0f, price: 0, tier: 3, tip: "爆破半径巨大。不会破坏图格"));
            list.Add(Ammo(ItemType.MiniNukeII, "迷你核弹二型", 4458, AmmoKind.Rocket, ProjKind.None, 75, 4f, 0f, price: 0, tier: 3, tip: "爆破半径巨大。会破坏图格"));
            ItemDef L(ItemType t, string name, int id, int dmg, int use, float speed, ProjKind shot, string sound, float hx, float hy, string tip = null)
            {
                var d = G(t, name, id, dmg, 4f, use, speed, sound, 8, hx, hy, tip: tip);
                d.Shoot = shot;
                d.UseAmmo = AmmoKind.Rocket;
                return d;
            }
            list.Add(L(ItemType.GrenadeLauncher, "榴弹发射器", 758, 60, 20, 10f, ProjKind.LauncherGrenade1, "Item_61", -10f, 0f));
            list.Add(L(ItemType.RocketLauncher, "火箭发射器", 759, 55, 30, 5f, ProjKind.Rocket1, "Item_11", -8f, 2f, "会对直接命中的目标造成额外伤害"));
            list.Add(L(ItemType.ProximityMineLauncher, "感应雷发射器", 760, 80, 50, 12f, ProjKind.Mine1, "Item_11", -2f, 2f, "部署好的雷会造成三倍伤害"));
            list.Add(L(ItemType.SnowmanCannon, "雪人炮", 1946, 67, 15, 15f, ProjKind.SnowmanRocket1, "Item_11", -2f, 2f, "发射自动制导的导弹"));
            // I:31934-31948, 34743-34759, 26691-26706: the Celebration, the Celebration Mk2 (held, half its ammo saved: Pl:54227), the Electrosphere Launcher (glowmask 36).
            list.Add(L(ItemType.Celebration, "喜庆弹射器", 3546, 25, 30, 15f, ProjKind.Firework1, "Item_156", -14f, -6f));
            var mk2 = Gun(ItemType.CelebrationMk2, "喜庆弹射器Mk2", 3930, 50, 10f, 6, 17f, null, price: 0, tier: 3, rare: 10, tip: "50%几率省下弹药\n“一切美好的事物都结束于砰的一声……或很多声！”");
            mk2.Shoot = ProjKind.Celeb2Held;
            mk2.UseAmmo = AmmoKind.Rocket;
            mk2.Channel = true;
            mk2.NoUseGraphic = true;
            mk2.Bonus.AmmoSave = 0.5f;
            list.Add(mk2);
            var sphere = L(ItemType.ElectrosphereLauncher, "电圈发射器", 2796, 40, 12, 12f, ProjKind.ElectrosphereMissile, "Item_92", -28f, 2f);
            sphere.Knockback = 2f;
            sphere.AutoReuse = false;
            sphere.Glow = "Glow_36";
            list.Add(sphere);
            // --- Sweep R4b3: the darts (I:4737-4741, 16208-16216, 27803-27838) and dart guns (I:4705-4718, 12714-12730, 27763-27800),
            // and the Sandgun (I:4506-4520) on any sand block (Pl:54156-54172: the corrupt, hallowed and crimson sands 5 harder).
            list.Add(Ammo(ItemType.Seed, "种子", 283, AmmoKind.Dart, ProjKind.SeedProj, 4, 0f, 0f, price: 0, tier: 0, tip: "搭配吹管使用"));
            list.Add(Ammo(ItemType.PoisonDart, "毒镖", 1310, AmmoKind.Dart, ProjKind.PoisonDartProj, 10, 2f, 2f, price: 0, tier: 0, rare: 2, tip: "对敌人施毒"));
            list.Add(Ammo(ItemType.CrystalDart, "水晶镖", 3009, AmmoKind.Dart, ProjKind.CrystalDartProj, 14, 3.5f, 1f, price: 0, tier: 3, rare: 3, tip: "在敌人之间弹来弹去"));
            list.Add(Ammo(ItemType.CursedDart, "诅咒镖", 3010, AmmoKind.Dart, ProjKind.CursedDartProj, 9, 2.2f, 3f, price: 0, tier: 3, rare: 3, tip: "滴落诅咒焰到地上"));
            list.Add(Ammo(ItemType.IchorDart, "灵液镖", 3011, AmmoKind.Dart, ProjKind.IchorDartProj, 10, 2.5f, 3f, price: 0, tier: 3, rare: 3, tip: "炸成多个飞镖"));
            ItemDef DG(ItemType t, string name, int id, int dmg, float kb, int use, float speed, string sound, int rare, float hx, float hy, float scale = 1f, string tip = null)
            {
                var d = G(t, name, id, dmg, kb, use, speed, sound, rare, hx, hy, scale: scale, tip: tip);
                d.Shoot = ProjKind.SeedProj;
                d.UseAmmo = AmmoKind.Dart;
                return d;
            }
            list.Add(DG(ItemType.Blowpipe, "吹管", 281, 9, 3.5f, 25, 11f, "Item_63", 0, 6f, -6f, tip: "可收集种子作为弹药"));
            list.Add(DG(ItemType.Blowgun, "吹箭筒", 986, 27, 4f, 35, 13f, "Item_64", 3, 6f, -10f, tip: "可收集种子作为弹药"));
            list.Add(DG(ItemType.DartPistol, "飞镖手枪", 3007, 28, 3.5f, 22, 13f, "Item_98", 5, 4f, -1f, scale: 0.9f));
            list.Add(DG(ItemType.DartRifle, "飞镖步枪", 3008, 52, 5.5f, 38, 14.5f, "Item_99", 5, -7f, 2f));
            var sandgun = G(ItemType.Sandgun, "沙枪", 266, 30, 5f, 16, 12f, "Item_11", 2, 0f, 2f, tip: "“这主意不错！”");
            sandgun.Shoot = ProjKind.SandBall;
            sandgun.UseAmmo = AmmoKind.Sand;
            list.Add(sandgun);
            foreach (var t in new[] { ItemType.SandBlock, ItemType.EbonsandBlock, ItemType.PearlsandBlock, ItemType.CrimsandBlock })
            {
                var d = list.Find(x => x.Type == t);
                if (d != null) d.Ammo = AmmoKind.Sand;
            }
            // I:7394-7408, 20724-20738: the Flamethrower and the Elf Melter on gel (five bursts a 30 tick use, gel spent once: Pl:54307),
            // I:12120-12145, 19126-19134, 42595-42625: the Flare Gun and its flares.
            ItemDef F(ItemType t, string name, int id, int dmg, float kb, float speed, int rare, string tip)
            {
                var d = G(t, name, id, dmg, kb, 30, speed, "Item_34", rare, 0f, -2f, tip: tip);
                d.Shoot = ProjKind.FlamethrowerFlame;
                d.UseAmmo = AmmoKind.Gel;
                return d;
            }
            list.Add(F(ItemType.Flamethrower, "火焰喷射器", 506, 35, 0.3f, 7f, 5, "用凝胶作为弹药\n忽略15点敌人防御"));
            list.Add(F(ItemType.ElfMelter, "精灵熔枪", 1910, 57, 0.425f, 8.5f, 8, "用凝胶作为弹药\n忽略15点敌人防御"));
            var gel = list.Find(x => x.Type == ItemType.Gel);
            if (gel != null) gel.Ammo = AmmoKind.Gel;
            var flareGun = G(ItemType.FlareGun, "信号枪", 930, 2, 0f, 18, 6f, "Item_11", 1, 10f, 0f, auto: false, scale: 0.9f);
            flareGun.Shoot = ProjKind.FlareProj;
            flareGun.UseAmmo = AmmoKind.Flare;
            list.Add(flareGun);
            list.Add(Ammo(ItemType.Flare, "照明弹", 931, AmmoKind.Flare, ProjKind.FlareProj, 1, 1.5f, 6f, price: 0, tier: 0));
            list.Add(Ammo(ItemType.BlueFlare, "蓝照明弹", 1614, AmmoKind.Flare, ProjKind.BlueFlareProj, 1, 1.5f, 6f, price: 0, tier: 0));
            list.Add(Ammo(ItemType.SpelunkerFlare, "洞穴探险照明弹", 5377, AmmoKind.Flare, ProjKind.SpelunkerFlareProj, 1, 1.5f, 6f, price: 0, tier: 0, tip: "显露附近的宝藏"));
            list.Add(Ammo(ItemType.CursedFlare, "诅咒照明弹", 5378, AmmoKind.Flare, ProjKind.CursedFlareProj, 1, 1.5f, 6f, price: 0, tier: 3));
            // --- Sweep R4b5: the specialist launchers (I:16327-16351, 15589-15603, 19752-19798, 20318-20333, 28759-28772, 33913-33927, 44243-44247)
            // and their ammo (I:12357-12371, 15643-15652, 19771-19810, 20337-20345, 28776-28786).
            ItemDef S(ItemType t, string name, int id, int dmg, float kb, int use, float speed, string sound, int rare, float hx, float hy, ProjKind shot, AmmoKind ammo,
                float save = 0f, bool auto = true, string tip = null)
            {
                var d = G(t, name, id, dmg, kb, use, speed, sound, rare, hx, hy, auto: auto, tip: tip);
                d.Shoot = shot;
                d.UseAmmo = ammo;
                d.Bonus.AmmoSave = save;
                return d;
            }
            // The plain Snowball Cannon (10, 19 ticks, rare 1); 22/6 is the Remix seed's StrongerVariant.
            list.Add(S(ItemType.SnowballCannon, "雪球炮", 1319, 10, 1f, 19, 11f, "Item_11", 1, 0f, 0f, ProjKind.SnowballProj, AmmoKind.Snowball));
            list.Add(S(ItemType.Stynger, "毒刺发射器", 1258, 45, 5f, 22, 9f, "Item_11", 7, 2f, -2f, ProjKind.StyngerBoltProj, AmmoKind.StyngerBolt, tip: "射出爆炸矢\n会对直接命中的目标造成额外伤害"));
            list.Add(S(ItemType.CandyCornRifle, "玉米糖步枪", 1782, 60, 2f, 9, 10f, "Item_11", 8, 0f, 4f, ProjKind.CandyCornProj, AmmoKind.CandyCorn, 1f / 3f, tip: "33%几率省下弹药"));
            list.Add(S(ItemType.JackOLanternLauncher, "杰克南瓜灯发射器", 1784, 85, 5f, 25, 7f, "Item_11", 8, 0f, 4f, ProjKind.JackOLanternProj, AmmoKind.JackOLantern));
            list.Add(S(ItemType.StakeLauncher, "尖桩发射器", 1835, 75, 6.5f, 12, 9f, "Item_5", 8, -2f, 2f, ProjKind.StakeProj, AmmoKind.Stake));
            list.Add(S(ItemType.NailGun, "钉枪", 3107, 85, 0f, 15, 10f, "Item_108", 8, 4f, 2f, ProjKind.NailProj, AmmoKind.Nail));
            list.Add(S(ItemType.AcornSlingshot, "橡实弹弓", 5629, 5, 4f, 20, 10f, "Item_39", 0, 4f, -2f, ProjKind.AcornProj, AmmoKind.Acorn, tip: "发射橡果，落地后会生根发芽"));
            var tosser = S(ItemType.AleTosser, "麦芽酒投掷器", 3821, 32, 4f, 32, 11f, "Item_1", 2, 10f, 0f, ProjKind.AleMug, AmmoKind.Ale, 0.69f, auto: false, tip: "有69%的几率节省弹药");
            tosser.HoldOut = false;
            tosser.NoUseGraphic = true;
            list.Add(tosser);
            list.Add(Ammo(ItemType.Snowball, "雪球", 949, AmmoKind.Snowball, ProjKind.SnowballProj, 8, 5.75f, 7f, price: 0, tier: 0));
            list.Add(Ammo(ItemType.StyngerBolt, "毒刺矢", 1261, AmmoKind.StyngerBolt, ProjKind.StyngerBoltProj, 17, 1f, 2f, price: 0, tier: 3, rare: 5, tip: "爆炸成致命的弹片"));
            list.Add(Ammo(ItemType.CandyCorn, "玉米糖", 1783, AmmoKind.CandyCorn, ProjKind.CandyCornProj, 9, 1.5f, 4f, price: 0, tier: 3));
            list.Add(Ammo(ItemType.ExplosiveJackOLantern, "爆炸杰克南瓜灯", 1785, AmmoKind.JackOLantern, ProjKind.JackOLanternProj, 60, 3f, 4f, price: 0, tier: 3));
            list.Add(Ammo(ItemType.Stake, "尖桩", 1836, AmmoKind.Stake, ProjKind.StakeProj, 25, 4.5f, 3f, price: 0, tier: 3));
            list.Add(Ammo(ItemType.Nail, "钉子", 3108, AmmoKind.Nail, ProjKind.NailProj, 30, 3f, 6f, price: 0, tier: 3, rare: 8));
            foreach (var (t, kind) in new[] { (ItemType.Ale, AmmoKind.Ale), (ItemType.Acorn, AmmoKind.Acorn) })
            {
                var d = list.Find(x => x.Type == t);
                if (d != null) d.Ammo = kind;
            }
            // I:34126-34141, 27389-27402, 27947-27961: the bows that make arrows their own (Aerial Bane's glowmask 234).
            var bane = B(ItemType.AerialBane, "空中祸害", 3859, 38, 4.5f, 30, 11f, "Item_102", 8, -2f, 0f, crit: 3, tip: "射出分裂的箭，对空中的敌人造成更多伤害");
            bane.Glow = "Glow_234";
            list.Add(bane);
            list.Add(B(ItemType.BeesKnees, "蜂膝弓", 2888, 23, 3f, 23, 8f, "Item_97", 3, 6f, 0f, auto: false, tip: "木箭变成一队蜜蜂"));
            list.Add(B(ItemType.HellwingBow, "地狱之翼弓", 3019, 22, 5.5f, 13, 6f, "Item_5", 3, 10f, 0f, tip: "木箭变成烈焰蝙蝠"));
            // I:39976-39991: Eventide, five shots in the first ten ticks of each 30 tick use (useTime 2), one arrow spent a use (Pl:54277-54280).
            list.Add(B(ItemType.Eventide, "日暮", 4953, 50, 2f, 30, 10f, "Item_5", 8, -4f, 0f));
            // I:14215-14230: the Piranha Gun, held (channel), no ammo, three piranhas.
            var piranha = G(ItemType.PiranhaGun, "食人鱼枪", 1156, 40, 1f, 30, 14f, "Item_10", 8, -2f, 0f, auto: false, scale: 1.1f, tip: "咬定敌人来持续造成伤害");
            piranha.Shoot = ProjKind.PiranhaProj;
            piranha.UseAmmo = AmmoKind.None;
            piranha.Channel = true;
            list.Add(piranha);
            // --- Sweep R6: the Paintball Gun (I:31951-31967: three shots a use, useTime 9 of 24, no ammo, the paint's own twang),
            // the Barrel Launcher (I:43046-43060, M:3744-3746), the Rainbow and Shimmer Flares (I:42626-42647) and the liquid rockets
            // (I:37597-37683). Shots in TerraProjectilesGuns3.cs, TerraProjectilesFlames.cs, TerraProjectilesRockets.cs.
            var paintball = G(ItemType.PaintballGun, "彩弹枪", 3350, 12, 1.25f, 9, 10f, null, 2, 2f, 0f, auto: false, crit: 7, scale: 0.85f);
            paintball.UseAnimation = 24 * Tick;
            paintball.Shoot = ProjKind.Paintball;
            paintball.UseAmmo = AmmoKind.None;
            list.Add(paintball);
            var barrel = G(ItemType.BarrelLauncher, "炸药桶发射器", 5460, 100, 8f, 40, 11f, "Custom/deadcells_barrel_launcher_fire", 4, -10f, -6f,
                tip: "木桶弹跳时造成更高伤害。\n“废弃酒厂，2020年陈酿”");
            barrel.Shoot = ProjKind.DeadCellsBarrel;
            barrel.UseAmmo = AmmoKind.None;
            barrel.UseSoundVolume = 0.5f;
            list.Add(barrel);
            list.Add(Ammo(ItemType.RainbowFlare, "彩虹照明弹", 5379, AmmoKind.Flare, ProjKind.RainbowFlareProj, 1, 1.5f, 6f, price: 0, tier: 0));
            list.Add(Ammo(ItemType.ShimmerFlare, "微光照明弹", 5380, AmmoKind.Flare, ProjKind.ShimmerFlareProj, 1, 1.5f, 6f, price: 0, tier: 0));
            list.Add(Ammo(ItemType.WetRocket, "湿火箭", 4447, AmmoKind.Rocket, ProjKind.None, 40, 4f, 0f, price: 0, tier: 3, tip: "碰撞时泼洒水"));
            list.Add(Ammo(ItemType.LavaRocket, "熔岩火箭", 4448, AmmoKind.Rocket, ProjKind.None, 40, 4f, 0f, price: 0, tier: 3, tip: "碰撞时泼洒熔岩"));
            list.Add(Ammo(ItemType.HoneyRocket, "蜂蜜火箭", 4449, AmmoKind.Rocket, ProjKind.None, 40, 4f, 0f, price: 0, tier: 3, tip: "碰撞时泼洒蜂蜜"));
            list.Add(Ammo(ItemType.DryRocket, "干火箭", 4459, AmmoKind.Rocket, ProjKind.None, 40, 4f, 0f, price: 0, tier: 3, tip: "碰撞时吸收液体"));
        }
    }
}
