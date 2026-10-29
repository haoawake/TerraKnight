namespace TerraKnight.Items
{
    /// <summary>
    /// The flails' and the chained weapons' right-click arts (the user's 10-26 "继续升级剩下的这些…也有一些右键特殊能力"), run by
    /// TerraProjectiles (FlailArt, ChainArt): the heavy ones held and let go (the mace's slam, the Meatball's blood, Sunfury's
    /// meteor, the Golem Fist's rocket, the KO Cannon's haymaker, the Anchor's drop), the rest pressed.
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterFlailArts()
        {
            KindArt Charged(string name, bool chain, string tip, string en) => new KindArt
            {
                Charged = true, Full = 1f, Pose = 115f, ChargeSound = "tk_heavy_charge", Name = name, Tip = tip, TipEn = en,
                Release = (hero, def, c) =>
                {
                    TurnToAim(hero);
                    var tp = TerraProjectiles.Instance;
                    if (tp != null && !(chain ? tp.ChainArt(def, hero, name, c) : tp.FlailArt(def, hero, name, c))) DevLastArt = "";
                },
            };
            KindArt Pressed(string name, bool chain, float cooldown, string tip, string en) => new KindArt
            {
                Cooldown = cooldown, Name = name, Tip = tip, TipEn = en,
                Press = (hero, def) =>
                {
                    TurnToAim(hero);
                    var tp = TerraProjectiles.Instance;
                    return tp != null && (chain ? tp.ChainArt(def, hero, name, 1f) : tp.FlailArt(def, hero, name, 1f));
                },
            };

            RegisterArt(ItemType.Mace, Charged("重锤砸地", false, "右键：按住蓄力，松开把链锤抡过头顶砸向前方地面，震伤周围的敌人（2～3.5 倍伤害）",
                "Right click: hold to charge, let go to swing the mace over your head and smash the ground ahead, the shock striking all round (2 to 3.5 times damage)"));
            RegisterArt(ItemType.FlamingMace, Charged("烈焰砸地", false, "右键：按住蓄力，松开抡起链锤砸地，火焰沿地面向前烧出一路，敌人着火",
                "Right click: hold to charge, let go to smash the ground, fire running on along it and setting enemies alight"));
            RegisterArt(ItemType.BallOHurt, Pressed("腐化风暴", false, 3f, "右键：把链球在身边快速抡圆，一圈圈扫打周围的敌人",
                "Right click: whirl the ball fast and wide round you, sweeping all about"));
            RegisterArt(ItemType.TheMeatball, Charged("血肉重砸", false, "右键：按住蓄力，松开砸地，前方地面接连喷出血柱",
                "Right click: hold to charge, let go to smash the ground, columns of blood bursting up ahead"));
            RegisterArt(ItemType.BlueMoon, Pressed("月轮", false, 2.5f, "右键：把蓝月甩出一个大圆，绕身边一圈再收回",
                "Right click: fling the Blue Moon out in a wide circle round you and draw it back"));
            RegisterArt(ItemType.Sunfury, Charged("陨日坠击", false, "右键：按住蓄力，松开把球抛上高空，像陨石一样砸向鼠标处炸开烈焰",
                "Right click: hold to charge, let go to hurl the ball high; it falls on the cursor like a meteor and bursts into flame"));
            RegisterArt(ItemType.DaoofPow, Pressed("太极螺旋", false, 2f, "右键：链球沿螺旋甩出再收回，被打中的敌人陷入混乱",
                "Right click: the ball spirals out and back, confusing what it strikes"));
            RegisterArt(ItemType.DripplerFlail, Pressed("血泉三连", false, 2.5f, "右键：抡一圈后朝鼠标方向甩出三颗血球",
                "Right click: whirl it once and fling three blood balls toward the cursor"));
            RegisterArt(ItemType.FlowerPow, Pressed("花瓣风暴", false, 3f, "右键：在头顶旋转花球，向四周螺旋射出一圈花瓣",
                "Right click: spin the flower over your head, loosing a spiral of petals all round"));
            var bubbles = Pressed("泡泡漩涡", false, 3f, "右键：绕身边甩动猪鲨链球，放出一圈追踪泡泡",
                "Right click: swing the Flairon round you, releasing a ring of homing bubbles");
            RegisterArt(ItemType.FlaironFlail, bubbles);
            RegisterArt(ItemType.Flairon, bubbles);

            // The launchers charge held out at the cursor, the muzzle on it.
            KindArt Aimed(KindArt a) { a.Aimed = true; return a; }
            RegisterArt(ItemType.GolemFist, Aimed(Charged("火箭重拳", true, "右键：按住蓄力，松开射出火箭重拳，飞得更远，命中或撞墙时爆炸（3～5 倍伤害）",
                "Right click: hold to charge, let go to launch a rocket punch that flies farther and explodes on what it meets (3 to 5 times damage)")));
            RegisterArt(ItemType.KOCannon, Aimed(Charged("致胜重拳", true, "右键：按住蓄力，松开打出一记巨大的拳套，击退极大（3～6 倍伤害）",
                "Right click: hold to charge, let go to throw a giant glove with huge knockback (3 to 6 times damage)")));
            RegisterArt(ItemType.ChainKnife, Pressed("链刃旋风", true, 2f, "右键：链刀绕身边飞旋两圈，扫中周围的敌人",
                "Right click: the chain knife whirls twice round you, cutting all about"));
            RegisterArt(ItemType.ChainGuillotines, Pressed("血滴子收割", true, 3f, "右键：两把血滴子一左一右绕身边旋转收割",
                "Right click: both guillotines whirl round you on their chains, opposite each other"));
            RegisterArt(ItemType.Anchor, Charged("落锚", true, "右键：按住蓄力，松开让锚从鼠标处上空砸落，砸碎地面震伤一大片（2.5～4.5 倍伤害）",
                "Right click: hold to charge, let go to drop the anchor from above onto the cursor, smashing the ground wide (2.5 to 4.5 times damage)"));
            RegisterArt(ItemType.SolarEruption, Pressed("日轮", true, 4f, "右键：日耀喷发剑绕身边抡出两圈烈日之轮",
                "Right click: lash the Solar Eruption round you twice in a wheel of sunfire"));
        }
    }
}
