using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The spears' right-click arts (the user's 10-26 "长矛的戳刺类…也有一些右键特殊能力"), and the held blades' (the gale run, the
    /// meteor flurry), on the spears they fit, with the
    /// remade swings on: held and let go for the charged ones (the pike's piercing thrust, the tridents' three, the swordfish's
    /// and the Dark Lance's run through, the Storm Spear's bolts), pressed for the rest (the halberds' whirlwind, Gungnir
    /// thrown, the North Pole's ice rain, the Chlorophyte Partisan's spores, the Mushroom Spear's ring). The spear's own
    /// thrust carries them where it can (TerraProjectiles.SkillSpear: its picture, its shots), its own shots the rest.
    /// </summary>
    internal static partial class BladeSkills
    {
        static readonly Dictionary<ItemType, Skill> spearSkills = new Dictionary<ItemType, Skill>
        {
            [ItemType.PalladiumPike] = Skill.SpearPierce,
            [ItemType.Trident] = Skill.TriThrust, [ItemType.TitaniumTrident] = Skill.TriThrust,
            [ItemType.Swordfish] = Skill.SpearDash, [ItemType.ObsidianSwordfish] = Skill.SpearDash, [ItemType.DarkLance] = Skill.SpearDash,
            [ItemType.ThunderSpear] = Skill.ThunderVolley,
            [ItemType.CobaltNaginata] = Skill.Whirl, [ItemType.MythrilHalberd] = Skill.Whirl, [ItemType.OrichalcumHalberd] = Skill.Whirl,
            [ItemType.AdamantiteGlaive] = Skill.Whirl,
            [ItemType.Gungnir] = Skill.SpearThrow, [ItemType.NorthPole] = Skill.IceRain, [ItemType.ChlorophytePartisan] = Skill.SporeRing,
            [ItemType.MushroomSpear] = Skill.MushroomRing,
            // The held blades.
            [ItemType.Arkhalis] = Skill.GaleDash, [ItemType.Terragrim] = Skill.GaleDash, [ItemType.PiercingStarlight] = Skill.StarBurst,
        };

        /// <summary>A spear's art (the remade swings on), or None.</summary>
        static Skill SpearSkill(ItemDef d) => d != null && WeaponRefitting.EnabledFor(d) && spearSkills.TryGetValue(d.Type, out var s) ? s : Skill.None;

        /// <summary>One of the spears' arts.</summary>
        static bool IsSpearSkill(Skill s) => s >= Skill.SpearPierce && s <= Skill.StarBurst;

        /// <summary>A spear with an art (for its tooltip's line).</summary>
        public static bool HasSpearArt(ItemDef d) => SpearSkill(d) != Skill.None;

        static Vector2 Centre(HeroController hero) => hero.GetComponent<BoxCollider2D>().bounds.center;

        /// <summary>A charged spear art let go.</summary>
        static void SpearRelease(HeroController hero, ItemDef def, Skill skill, float c)
        {
            var tp = TerraProjectiles.Instance;
            var mod = TerraMod.Instance;
            if (tp == null || mod == null) return;
            TurnToAim(hero);
            switch (skill)
            {
                case Skill.SpearPierce:
                    tp.SkillSpear(def, hero, Aim(), "贯穿", 2f + 2f * c, 0.5f);
                    mod.PlaySound(def.UseSound ?? "Item_1", 0.8f, 0.85f);
                    break;
                case Skill.TriThrust:
                    tp.SkillSpear(def, hero, Aim(), "三叉刺", 1.8f + 1.5f * c, 0.5f);
                    mod.PlaySound("Item_1", 0.7f, 1.15f);
                    break;
                case Skill.SpearDash:
                {
                    // Along the way the player faces, the spear held out before him through the run.
                    float facing = Facing(hero);
                    float far = 3f + 3f * c;
                    Run(hero, def, new Vector2(facing, 0f), far, Damage(def, 1.5f + 2f * c), 0.05f, def.Type == ItemType.DarkLance ? RunStyle.Shadow : RunStyle.Draw, c);
                    tp.SkillSpear(def, hero, Centre(hero) + new Vector2(facing * 5f, 0f), "冲锋", 0.6f, 0.35f);
                    mod.PlaySound(def.Type == ItemType.DarkLance ? "Item_103" : "Item_1", 0.7f, 1.2f);
                    break;
                }
                case Skill.ThunderVolley:
                    tp.ThunderVolley(def, hero, Aim(), 3 + Mathf.RoundToInt(2f * c), 1f + 0.4f * c);
                    tp.SkillSpear(def, hero, Aim(), "扎枪", 0.6f, 0.35f);
                    break;
                case Skill.GaleDash:
                {
                    // A run the way he faces, gales flying at the cursor from along it.
                    float facing = Facing(hero);
                    var s = Run(hero, def, new Vector2(facing, 0f), 3f + 3f * c, Damage(def, 1.5f + 2f * c), 0.05f, RunStyle.Draw, c);
                    tp.GaleRun(def, s.From, s.To, 2 + Mathf.RoundToInt(2f * c));
                    mod.PlaySound("Item_1", 0.7f, 1.3f);
                    break;
                }
            }
        }

        /// <summary>A pressed spear art.</summary>
        static bool SpearPress(HeroController hero, ItemDef def, Skill skill)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return false;
            TurnToAim(hero);
            switch (skill)
            {
                case Skill.Whirl:
                    TerraMod.Instance?.PlaySound("Item_7", 0.7f, 0.9f);
                    return tp.SkillSpear(def, hero, Aim(), "旋风", 2.4f, 0.9f);
                case Skill.SpearThrow:
                    return tp.ThrowGungnir(def, hero, Aim());
                case Skill.IceRain:
                    tp.IceRain(def, Aim(), 7);
                    tp.SkillSpear(def, hero, Centre(hero) + new Vector2(Facing(hero) * 0.5f, 5f), "崩枪", 0.6f, 0.35f);
                    return true;
                case Skill.SporeRing:
                    tp.SporeRing(def, Centre(hero), 8);
                    tp.SkillSpear(def, hero, Aim(), "枪花", 0.8f, 0.45f);
                    return true;
                case Skill.MushroomRing:
                    tp.MushroomRing(def, Centre(hero), 14);
                    tp.SkillSpear(def, hero, Aim(), "枪花", 0.8f, 0.45f);
                    return true;
                case Skill.StarBurst:
                    tp.StarBurst(def, hero);
                    return true;
                default:
                    return false;
            }
        }
    }
}
