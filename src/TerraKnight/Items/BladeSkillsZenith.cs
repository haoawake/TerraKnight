using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The right-click arts of the Zenith and the monk staffs (the user's 10-26 "继续升级剩下的这些…并把悠悠球的升级加进去"; the
    /// dispatcher's KindArt): the Zenith's swords gathered and loosed (ZenithArts), the Octopod's march of slams, the Glaive's
    /// ghasts, the Fury's dragon dash (TerraProjectilesMonkArts). The Fury's own right-click thrust is one of its left
    /// button's techniques while its art has the right.
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterZenithArts()
        {
            RegisterArt(ItemType.Zenith, new KindArt
            {
                Name = "万剑归宗", Charged = true, Full = 1.2f, Pose = 95f, ChargeSound = "tk_star_charge",
                Release = (hero, def, c) =>
                {
                    TurnToAim(hero);
                    ZenithSwords.Instance?.Loose(Aim());
                },
                Tip = "右键：按住蓄力，熔铸天顶剑的各把剑聚到身边（蓄满十六把）；松开一齐射向鼠标附近的敌人，穿过后折返",
                TipEn = "Right click: hold to charge, gathering the swords the Zenith is forged from around you (sixteen at full charge); let go and they converge on the enemies near the cursor, pierce through and curl back",
            });
            RegisterArt(ItemType.MonkStaffT1, new KindArt
            {
                Name = "大地苏醒", Charged = true, Full = 1f, Pose = 120f, ChargeSound = "tk_heavy_charge",
                Release = (hero, def, c) =>
                {
                    TurnToAim(hero);
                    TerraProjectiles.Instance?.StaffSkill(def, hero, "大地苏醒", StaffDamage(def, 2.5f + 2.5f * c), c);
                },
                Tip = "右键：按住蓄力，松开把法杖砸进地里，一连串地裂沿地面向前推进（蓄满七道）",
                TipEn = "Right click: hold to charge, let go to drive the staff into the ground: a chain of quakes marches forward along it (seven at full charge)",
            });
            RegisterArt(ItemType.MonkStaffT2, new KindArt
            {
                Name = "百鬼夜行", Cooldown = 4f,
                Press = (hero, def) =>
                {
                    TurnToAim(hero);
                    return TerraProjectiles.Instance != null && TerraProjectiles.Instance.StaffSkill(def, hero, "百鬼夜行", StaffDamage(def, 1f), 0f);
                },
                Tip = "右键：向前一刺，召出四只鬼魂扑向鼠标附近的敌人（4 秒冷却）",
                TipEn = "Right click: thrust and call four ghasts sweeping through the enemies near the cursor (4 s cooldown)",
            });
            RegisterArt(ItemType.MonkStaffT3, new KindArt
            {
                Name = "天龙冲", Charged = true, Full = 1f, Pose = -20f, ChargeSound = "tk_blade_charge",
                Release = (hero, def, c) => TerraProjectiles.Instance?.StaffSkill(def, hero, "天龙冲", StaffDamage(def, 1.5f + 1.5f * c), c),
                Tip = "右键：按住蓄力，松开化作天龙朝面向冲出，穿过敌人，冲到尽头放出一排龙息弹（蓄满五发）",
                TipEn = "Right click: hold to charge, let go to dash the way you face as a sky dragon, through enemies, loosing dragon bolts at the end of the run (five at full charge)",
            });
        }

        /// <summary>A staff art's blow: the staff's damage (with the player's bonuses), that many times.</summary>
        static int StaffDamage(ItemDef def, float scale) => Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def) * scale));
    }
}
