namespace TerraKnight.Items
{
    /// <summary>
    /// The yoyos' right-click arts (the user's 10-26 "把悠悠球的升级加进去"), with the yoyo out on its string (the left button
    /// held): each from what the yoyo is (TerraProjectilesYoyoArts does them). None moves the player.
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterYoyoArts()
        {
            void Pressed(ItemType t, string name, float cooldown, string tip, string tipEn) => RegisterArt(t, new KindArt
            {
                Name = name, Cooldown = cooldown, Tip = tip, TipEn = tipEn,
                Press = (hero, def) => TerraProjectiles.Instance != null && TerraProjectiles.Instance.YoyoArt(def, -1f),
            });
            void Held(ItemType t, string name, float full, string sound, string tip, string tipEn) => RegisterArt(t, new KindArt
            {
                Name = name, Charged = true, Full = full, Pose = 70f, ChargeSound = sound, Tip = tip, TipEn = tipEn,
                Release = (hero, def, c) => TerraProjectiles.Instance?.YoyoArt(def, c),
            });
            const string Out = "悠悠球放出时右键：";
            const string OutEn = "Right click with the yoyo out: ";
            Pressed(ItemType.WoodYoyo, "抛球", 2.5f, Out + "甩出一颗离绳的悠悠球，在地上滚着撞击敌人", OutEn + "fling a loose yoyo off the string that rolls along the ground into enemies");
            Pressed(ItemType.Rally, "对打", 3f, Out + "悠悠球在附近的敌人之间来回弹击五次", OutEn + "the yoyo bounces between nearby enemies, five blows");
            Pressed(ItemType.CorruptYoyo, "腐化分身", 5f, Out + "分出两颗暗影悠悠球环绕它旋转 4 秒", OutEn + "two shadow yoyos split off and circle it for 4 seconds");
            Pressed(ItemType.CrimsonYoyo, "血涌", 4f, Out + "以悠悠球为中心涌出一圈血浪，降低敌人防御", OutEn + "a ring of blood surges out from the yoyo, lowering enemies' defense");
            Pressed(ItemType.JungleYoyo, "荆棘环", 4f, Out + "以悠悠球为中心炸开一圈丛林之力，敌人中毒", OutEn + "a ring of jungle power bursts from the yoyo, poisoning enemies");
            Pressed(ItemType.Code1, "代码分裂", 5f, Out + "分出两颗悠悠球环绕它旋转 4 秒", OutEn + "two yoyos split off and circle it for 4 seconds");
            Pressed(ItemType.HiveFive, "蜂群", 4f, Out + "从悠悠球里放出一群蜜蜂", OutEn + "release a swarm of bees from the yoyo");
            Held(ItemType.Valor, "英勇砸击", 0.8f, "tk_heavy_charge", Out + "按住蓄力，松开把悠悠球砸向地面，震出冲击波", OutEn + "hold to charge, release to slam the yoyo into the ground for a shockwave");
            Pressed(ItemType.Cascade, "熔流喷发", 3f, Out + "悠悠球朝附近三个敌人喷出火焰", OutEn + "the yoyo jets flame at three nearby enemies");
            Pressed(ItemType.Chik, "水晶迸裂", 3f, Out + "悠悠球迸出一圈水晶碎片", OutEn + "the yoyo bursts into a ring of crystal shards");
            Pressed(ItemType.Amarok, "冰霜环", 4f, Out + "以悠悠球为中心炸开一圈寒霜，冻伤敌人", OutEn + "a ring of frost bursts from the yoyo, frostbiting enemies");
            Held(ItemType.HelFire, "狱火环", 0.9f, "tk_blade_charge", Out + "按住蓄力，松开以悠悠球为中心爆开狱火环（蓄得越久越大）", OutEn + "hold to charge, release for a ring of hellfire round the yoyo (bigger the longer you charge)");
            Pressed(ItemType.Gradient, "彩虹回旋", 3.5f, Out + "把悠悠球绕着自己抡两圈，扫中周围所有敌人", OutEn + "whirl the yoyo round yourself twice, sweeping every enemy about");
            Pressed(ItemType.FormatC, "全神贯注", 8f, Out + "4 秒内悠悠球每一击都是暴击", OutEn + "for 4 seconds every blow of the yoyo is a critical hit");
            Pressed(ItemType.Code2, "三重代码", 5f, Out + "分出三颗悠悠球环绕它旋转 4.5 秒", OutEn + "three yoyos split off and circle it for 4.5 seconds");
            Pressed(ItemType.Yelets, "毒液迸溅", 4f, Out + "以悠悠球为中心溅开一圈毒液", OutEn + "a ring of venom splashes out from the yoyo");
            Pressed(ItemType.RedsYoyo, "红莲回旋", 3f, Out + "把悠悠球绕着自己抡两圈，扫中周围所有敌人", OutEn + "whirl the yoyo round yourself twice, sweeping every enemy about");
            Pressed(ItemType.ValkyrieYoyo, "女武神之羽", 5f, Out + "分出三颗悠悠球环绕它旋转 5 秒", OutEn + "three yoyos split off and circle it for 5 seconds");
            Pressed(ItemType.TheEyeOfCthulhu, "凝视", 4f, Out + "悠悠球放出三只仆从扑向附近的敌人", OutEn + "the yoyo sends three servants lunging at nearby enemies");
            Held(ItemType.Kraken, "深海巨浪", 0.9f, "tk_blade_charge", Out + "按住蓄力，松开从悠悠球掀起一排巨浪（蓄满五道）", OutEn + "hold to charge, release to raise a fan of waves from the yoyo (five at full charge)");
            Held(ItemType.Terrarian, "泰拉迸发", 1f, "tk_star_charge", Out + "按住蓄力，松开悠悠球迸出一圈泰拉光球（蓄满十六颗）", OutEn + "hold to charge, release for a ring of Terrarian orbs from the yoyo (sixteen at full charge)");
        }
    }
}
