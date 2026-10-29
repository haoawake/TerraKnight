namespace TerraKnight.Items
{
    /// <summary>
    /// The close weapons' right-click arts (the user's 10-26 "继续升级剩下的这些…也有一些右键特殊能力"), each from what the weapon is:
    /// the plain shortswords of the first ores throw a fan of blades (飞刃), those of the later ores a charged thrust through
    /// a whole line (穿心刺); the Gladius a flurry of eight (罗马连刺), the Ruler a measured line that strikes everything on
    /// it (量天尺), the Chlorophyte Saber a charged thrust that leaves spores along its way (孢子穿刺); the jousting lances a
    /// couched charge along the way the player faces (冲锋: the Shadow lance's in shadowflame, the Hallowed one's ending in
    /// light); the Umbrella thrown spinning out and back (回旋伞), the Tragic Umbrella's rain cloud (悲雨). Their pieces are
    /// SwordFxClose's and TerraProjectilesClose's.
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterCloseArts()
        {
            foreach (var t in new[] { ItemType.CopperShortsword, ItemType.TinShortsword, ItemType.IronShortsword, ItemType.LeadShortsword })
                RegisterArt(t, new KindArt
                {
                    Name = "飞刃", Cooldown = 1.4f,
                    Press = (hero, def) =>
                    {
                        if (SwordFx.Instance == null) return false;
                        TurnToAim(hero);
                        SwordFx.Instance.CloseDaggers(def, Aim(), 3, 0.16f, 0.7f);
                        return true;
                    },
                    Tip = "右键：朝鼠标方向扇形掷出三把飞刃，碰到墙会扎在墙上",
                    TipEn = "Right click: throw three blades in a fan toward the cursor; they stick in walls",
                });
            foreach (var t in new[] { ItemType.SilverShortsword, ItemType.TungstenShortsword, ItemType.GoldShortsword, ItemType.PlatinumShortsword })
                RegisterArt(t, new KindArt
                {
                    Name = "穿心刺", Charged = true, Full = 0.8f, Pose = -15f, ChargeSound = "tk_blade_charge",
                    Release = (hero, def, c) =>
                    {
                        TurnToAim(hero);
                        SwordFx.Instance?.ClosePierce(def, Aim(), 1.6f + 1.6f * c, 2f + 0.6f * c, "穿心刺");
                    },
                    Tip = "右键：按住蓄力，松开朝鼠标方向一记穿心长刺，贯穿直线上的所有敌人（1.6～3.2 倍伤害）",
                    TipEn = "Right click: hold to charge, let go for a long piercing thrust toward the cursor that runs through every enemy on its line (1.6-3.2x damage)",
                });
            RegisterArt(ItemType.Gladius, new KindArt
            {
                Name = "罗马连刺", Cooldown = 2.2f,
                Press = (hero, def) =>
                {
                    if (SwordFx.Instance == null) return false;
                    TurnToAim(hero);
                    SwordFx.Instance.CloseFlurry(def, Aim(), 8, 0.32f, 0.5f, "罗马连刺");
                    return true;
                },
                Tip = "右键：一阵八连刺，在扇面里刺向鼠标方向",
                TipEn = "Right click: a flurry of eight stabs fanned toward the cursor",
            });
            RegisterArt(ItemType.Ruler, new KindArt
            {
                Name = "量天尺", Cooldown = 2f,
                Press = (hero, def) =>
                {
                    if (SwordFx.Instance == null) return false;
                    TurnToAim(hero);
                    SwordFx.Instance.CloseMeasure(def, Aim(), 1.6f);
                    return true;
                },
                Tip = "右键：朝鼠标方向量出一条刻度线（最长 14 格，到墙为止），线上的敌人全被击中",
                TipEn = "Right click: measure out a marked line toward the cursor (up to 14 tiles, a wall stops it); every enemy on it is struck",
            });
            RegisterArt(ItemType.ChlorophyteSaber, new KindArt
            {
                Name = "孢子穿刺", Charged = true, Full = 0.9f, Pose = -15f, ChargeSound = "tk_blade_charge",
                Release = (hero, def, c) =>
                {
                    TurnToAim(hero);
                    SwordFx.Instance?.ClosePierce(def, Aim(), 1.4f + 1.4f * c, 2f + 0.4f * c, "孢子穿刺");
                },
                Tip = "右键：按住蓄力，松开一记长刺贯穿敌人，沿途留下伤敌的孢子云",
                TipEn = "Right click: hold to charge, let go for a long thrust through enemies that leaves harmful spore clouds along its way",
            });
            (ItemType Type, string Name, string Tip, string En)[] lances =
            {
                (ItemType.JoustingLance, "冲锋", "右键：按住蓄力，松开朝面向持枪冲锋（最远约 17 格），冲得越快伤害越高",
                    "Right click: hold to charge, let go to charge the way you face with the lance couched (up to about 17 tiles); the faster, the harder it strikes"),
                (ItemType.ShadowJoustingLance, "暗影冲锋", "右键：按住蓄力，松开化作残影持枪冲锋，命中的敌人燃起暗影焰",
                    "Right click: hold to charge, let go to charge in afterimages with the lance couched; enemies struck catch shadowflame"),
                (ItemType.HallowJoustingLance, "圣光冲锋", "右键：按住蓄力，松开持枪冲锋，冲到尽头炸开一圈圣光",
                    "Right click: hold to charge, let go to charge with the lance couched, ending in a burst of light"),
            };
            foreach (var l in lances)
                RegisterArt(l.Type, new KindArt
                {
                    Name = l.Name, Charged = true, Full = 0.9f, Pose = -8f, ChargeSound = "tk_heavy_charge",
                    Release = (hero, def, c) => TerraProjectiles.Instance?.LanceCharge(def, hero, c),
                    Tip = l.Tip, TipEn = l.En,
                });
            RegisterArt(ItemType.Umbrella, new KindArt
            {
                Name = "回旋伞",
                Press = (hero, def) =>
                {
                    if (SwordFx.Instance == null) return false;
                    TurnToAim(hero);
                    return SwordFx.Instance.CloseUmbrellaThrow(def, Aim());
                },
                Tip = "右键：把伞旋转着扔向鼠标处，再飞回手里，去回都打中路上的敌人",
                TipEn = "Right click: throw the umbrella spinning toward the cursor and back to your hand, striking enemies both ways",
            });
            RegisterArt(ItemType.TragicUmbrella, new KindArt
            {
                Name = "悲雨", Cooldown = 4f,
                Press = (hero, def) =>
                {
                    if (SwordFx.Instance == null) return false;
                    SwordFx.Instance.CloseRainCloud(def, Aim(), 0.45f);
                    return true;
                },
                Tip = "右键：在鼠标处招来一片雨云，下 3 秒的雨，雨下的敌人不断受到伤害（4 秒冷却）",
                TipEn = "Right click: call a rain cloud over the cursor; for 3 s its rain keeps striking the enemies under it (4 s cooldown)",
            });
            // The arm held low along the lance while it charges ahead.
            ArmPoses.Add(facing => TerraProjectiles.LanceCharging ? -6f : (float?)null);
            // The arm toward the thrown umbrella (and the umbrella not in the hand meanwhile).
            ArmPoses.Add(facing => SwordFx.Instance != null && SwordFx.Instance.CloseUmbrellaAngle(facing, out float deg) ? deg : (float?)null);
        }
    }
}
