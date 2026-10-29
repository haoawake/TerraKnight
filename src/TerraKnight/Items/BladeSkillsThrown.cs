using UnityEngine;
using Flight = TerraKnight.Items.TerraProjectiles.Flight;
using Fly = TerraKnight.Items.TerraProjectiles.Fly;
using ArtFx = TerraKnight.Items.TerraProjectiles.ArtFx;

namespace TerraKnight.Items
{
    /// <summary>
    /// The melee thrown weapons' right-click arts (the user's 10-26 "继续升级剩下的这些…依旧是原来的要求"), each from what the
    /// weapon is: the boomerangs thrown three at once, powered, whirled at the cursor as a wheel of fire, set circling the
    /// player, ricocheting from enemy to enemy, splitting, dropped from the sky; the knives in a ring, curving in, circling
    /// the cursor; Daybreak rained down; the Scourge thrown three at once. Their throws go through TerraProjectiles.ThrowArt
    /// (TerraProjectilesThrownArts.cs): the weapon's own projectile on a flight of its own. The Sergeant United Shield keeps
    /// its own right button (its raised shield, MeleeSpecials).
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterThrownArts()
        {
            void ArtPress(ItemType t, string name, float cooldown, string tip, string tipEn, System.Func<HeroController, ItemDef, bool> press) =>
                RegisterArt(t, new KindArt { Name = name, Cooldown = cooldown, Tip = tip, TipEn = tipEn, Press = press });
            void ArtCharge(ItemType t, string name, float full, string sound, string tip, string tipEn, System.Action<HeroController, ItemDef, float> release) =>
                RegisterArt(t, new KindArt { Name = name, Charged = true, Full = full, Pose = 125f, ChargeSound = sound, Tip = tip, TipEn = tipEn, Release = release });

            ArtPress(ItemType.EnchantedBoomerang, "星辉三连", 2.5f, "右键：一次掷出三枚附魔回旋镖，中间直飞、两边弧线包抄",
                "Right click: throw three Enchanted Boomerangs at once, one straight and two curving in from the sides", (h, d) =>
                {
                    int n = 0;
                    for (int k = -1; k <= 1; k++)
                        if (Toss(h, d, k * 14f, new Flight { Path = k == 0 ? Fly.Straight : Fly.Arc, Side = k == 0 ? 1f : -k, ReachScale = 1.1f }, 0.75f)) n++;
                    return n > 0;
                });
            ArtCharge(ItemType.WoodenBoomerang, "蓄力投", 0.8f, "tk_blade_charge", "右键：按住蓄力，松开用力掷出，飞得更远、贯穿沿途敌人（伤害最多 3 倍）",
                "Right click: hold to charge, release for a power throw that flies further and pierces everything on its way (up to 3× damage)",
                (h, d, c) => Toss(h, d, 0f, new Flight { Path = Fly.Power, ReachScale = 1.4f + 0.6f * c }, 1.4f + 1.6f * c, 1.3f));
            ArtPress(ItemType.Flamarang, "烈焰轮", 3f, "右键：把烈焰回旋镖掷到鼠标处化作火轮，原地旋转灼烧两秒",
                "Right click: send the Flamarang to the cursor, where it whirls as a wheel of fire for two seconds",
                (h, d) => Toss(h, d, 0f, new Flight { Path = Fly.Hover, Centre = AimWithin(h, d, 1.2f), Lift = 12f * Effects.TerraParticles.Px, OutT = 135, Travel = 0.18f }, 0.7f));
            ArtPress(ItemType.ThornChakram, "荆棘护环", 4f, "右键：两枚荆棘旋刃绕身旋转，切割并毒害靠近的敌人",
                "Right click: two Thorn Chakrams circle you, cutting and poisoning enemies that come close", (h, d) =>
                {
                    int n = 0;
                    for (int k = 0; k < 2; k++)
                        if (Toss(h, d, 0f, new Flight { Path = Fly.Orbit, Lift = 3.2f * 16f * Effects.TerraParticles.Px, OutT = 150, Phase = k * Mathf.PI }, 0.6f)) n++;
                    return n > 0;
                });
            ArtCharge(ItemType.IceBoomerang, "冰晶爆", 0.9f, "tk_blade_charge", "右键：按住蓄力，松开掷出，飞到尽头炸开一圈冰矢",
                "Right click: hold to charge, release to throw; at the end of its flight it bursts into a ring of ice bolts",
                (h, d, c) => Toss(h, d, 0f, new Flight { Path = Fly.Power, ReachScale = 1.15f + 0.35f * c, End = ArtFx.IceBurst, FxPower = c }, 1.2f + 0.8f * c, 1.2f));
            ArtPress(ItemType.FruitcakeChakram, "糖霜弹射", 2f, "右键：旋刃在敌人之间来回弹射，最多五个",
                "Right click: the chakram ricochets from enemy to enemy, up to five",
                (h, d) => Toss(h, d, 0f, new Flight { Path = Fly.Chain, ChainLeft = 5, OutT = 30 }, 0.9f));
            ArtPress(ItemType.BloodyMachete, "血刃旋舞", 3f, "右键：砍刀飞到鼠标处原地旋舞，连续切割",
                "Right click: the machete flies to the cursor and spins there, cutting again and again",
                (h, d) => Toss(h, d, 0f, new Flight { Path = Fly.Hover, Centre = AimWithin(h, d, 1.2f), Lift = 8f * Effects.TerraParticles.Px, OutT = 90, Travel = 0.2f, Side = -1f }, 0.8f));
            ArtPress(ItemType.Shroomerang, "蘑菇雨", 4f, "右键：高抛飞过鼠标上空，一路撒下发光蘑菇",
                "Right click: lob it high over the cursor, scattering glowing mushrooms on the way",
                (h, d) => Toss(h, d, 0f, new Flight
                {
                    Path = Fly.Lob, Centre = AimWithin(h, d, 1.1f), Lift = ReachOf(d) * 0.55f,
                    Each = ArtFx.Mushrooms,
                }, 0.9f));
            ArtPress(ItemType.CombatWrench, "扳手弹射", 2f, "右键：扳手在敌人之间弹射，最多三个",
                "Right click: the wrench ricochets between enemies, up to three",
                (h, d) => Toss(h, d, 0f, new Flight { Path = Fly.Chain, ChainLeft = 3, OutT = 30 }, 1f));
            ArtPress(ItemType.Trimarang, "三叉展开", 3f, "右键：三枚同时掷出，两边大弧线、中间绕鼠标回环",
                "Right click: throw three at once: two wide arcs and one looping round the cursor", (h, d) =>
                {
                    float r = ReachOf(d);
                    int n = 0;
                    if (Toss(h, d, 10f, new Flight { Path = Fly.Arc, Side = 1f, Lift = r * 0.45f }, 0.7f)) n++;
                    if (Toss(h, d, 0f, new Flight { Path = Fly.Loop }, 0.7f)) n++;
                    if (Toss(h, d, -10f, new Flight { Path = Fly.Arc, Side = -1f, Lift = r * 0.45f }, 0.7f)) n++;
                    return n > 0;
                });
            ArtCharge(ItemType.Axearang, "天降劈", 1f, "tk_heavy_charge", "右键：按住蓄力，松开高高抛起，从上方劈落在鼠标处（伤害最多 3 倍）",
                "Right click: hold to charge, release to throw it high; it comes down on the cursor from above (up to 3× damage)",
                (h, d, c) => Toss(h, d, 0f, new Flight
                {
                    Path = Fly.Lob, Centre = AimWithin(h, d, 1.2f), Lift = ReachOf(d) * 1.1f,
                    End = ArtFx.AxeImpact, FxPower = c,
                }, 1.5f + 1.5f * c));
            ArtPress(ItemType.LightDisc, "光盘分裂", 2.5f, "右键：飞盘飞到半路一分为三",
                "Right click: the disc splits into three halfway",
                (h, d) => Toss(h, d, 0f, new Flight { Path = Fly.Straight, Mid = ArtFx.Split }, 0.8f));
            ArtPress(ItemType.Bananarang, "香蕉风暴", 3.5f, "右键：向四面八方掷出八只香蕉回旋镖",
                "Right click: throw eight Bananarangs in every direction", (h, d) =>
                {
                    var tp = TerraProjectiles.Instance;
                    var from = Centre(h);
                    int n = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * 45f;
                        if (tp.ThrowArt(d, from, new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)),
                                new Flight { Path = Fly.Arc, Side = k % 2 == 0 ? 1f : -1f, ReachScale = 0.75f }, 0.5f)) n++;
                    }
                    if (n > 0) TerraMod.Instance?.PlaySound("Item_1", 0.6f);
                    return n > 0;
                });
            ArtCharge(ItemType.PossessedHatchet, "狂斧追猎", 0.9f, "tk_blade_charge", "右键：按住蓄力，松开掷出 2～4 把疯狂飞斧追杀敌人",
                "Right click: hold to charge, release to throw 2 to 4 Possessed Hatchets that hunt down enemies", (h, d, c) =>
                {
                    int n = 2 + Mathf.RoundToInt(2f * c);
                    for (int k = 0; k < n; k++)
                        Toss(h, d, (k - (n - 1) * 0.5f) * 18f, new Flight { Path = Fly.Vanilla }, 0.7f);
                });
            ArtCharge(ItemType.PaladinsHammer, "圣锤天降", 1.1f, "tk_heavy_charge", "右键：按住蓄力，松开让圣骑士锤从天而降砸在鼠标处（伤害最多 3 倍）",
                "Right click: hold to charge, release to bring Paladin's Hammer down from the sky onto the cursor (up to 3× damage)", (h, d, c) =>
                {
                    var tp = TerraProjectiles.Instance;
                    if (tp == null) return;
                    TurnToAim(h);
                    var target = AimWithin(h, d, 1.3f);
                    var from = target + Vector2.up * 9f * 16f * Effects.TerraParticles.Px;
                    tp.ThrowArt(d, from, Vector2.down, new Flight
                    {
                        Path = Fly.Dive, Centre = target, OutT = 22, Side = Facing(h), End = ArtFx.HolyImpact, FxPower = c, Scale = 1.75f,
                    }, 1.6f + 1.4f * c);
                    TerraMod.Instance?.PlaySound("Item_1", 0.7f);
                });
            ArtPress(ItemType.VampireKnives, "血月刃环", 3f, "右键：向四周甩出一圈吸血鬼刀",
                "Right click: fling a ring of Vampire Knives all around you", (h, d) =>
                {
                    var tp = TerraProjectiles.Instance;
                    var from = Centre(h);
                    int n = 0;
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * 30f * Mathf.Deg2Rad;
                        if (tp.ThrowArt(d, from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new Flight { Path = Fly.Vanilla }, 0.55f)) n++;
                    }
                    if (n > 0) TerraMod.Instance?.PlaySound("Item_39", 0.8f);
                    return n > 0;
                });
            ArtPress(ItemType.ShadowFlameKnife, "暗焰三连", 2f, "右键：掷出三把暗影焰刀，弧线交汇到鼠标处",
                "Right click: throw three Shadowflame Knives that curve in to meet at the cursor", (h, d) =>
                {
                    int n = 0;
                    for (int k = -1; k <= 1; k++)
                        if (Toss(h, d, 0f, new Flight { Path = Fly.Vanilla, ConvergeTurn = k * 20f }, 0.8f, 1.2f)) n++;
                    return n > 0;
                });
            ArtPress(ItemType.FlyingKnife, "飞刀环舞", 3f, "右键：三把飞刀绕着鼠标飞旋三秒",
                "Right click: three flying knives whirl round the cursor for three seconds", (h, d) =>
                {
                    int n = 0;
                    for (int k = 0; k < 3; k++)
                        if (Toss(h, d, (k - 1) * 20f, new Flight { Path = Fly.Orbit, Lift = 46f, OutT = 180, Phase = k * Mathf.PI * 2f / 3f }, 0.6f)) n++;
                    return n > 0;
                });
            ArtCharge(ItemType.DayBreak, "破晓日冕", 1f, "tk_star_charge", "右键：按住蓄力，松开从天上降下 3～5 支破晓之光插向鼠标处",
                "Right click: hold to charge, release to rain 3 to 5 Daybreaks down onto the cursor", (h, d, c) =>
                {
                    var tp = TerraProjectiles.Instance;
                    if (tp == null) return;
                    TurnToAim(h);
                    var target = AimWithin(h, d, 1.5f);
                    int n = 3 + Mathf.RoundToInt(2f * c);
                    float tile = 16f * Effects.TerraParticles.Px;
                    for (int k = 0; k < n; k++)
                    {
                        float off = (k - (n - 1) * 0.5f) * 2.2f * tile;
                        var from = target + new Vector2(off * 1.6f, 13f * tile);
                        var to = target + new Vector2(off, 0f);
                        tp.ThrowArt(d, from, to - from, new Flight { Path = Fly.Vanilla }, 0.55f, 1.3f);
                    }
                    TerraMod.Instance?.PlaySound("Item_105", 0.6f);
                });
            ArtCharge(ItemType.ScourgeoftheCorruptor, "腐化三投", 0.9f, "tk_blade_charge", "右键：按住蓄力，松开一次掷出三支腐化者之戟",
                "Right click: hold to charge, release to throw three Scourges of the Corruptor at once", (h, d, c) =>
                {
                    for (int k = -1; k <= 1; k++)
                        Toss(h, d, k * 12f, new Flight { Path = Fly.Vanilla }, 0.7f + 0.3f * c, 1.15f);
                });
        }

        /// <summary>An art's throw from the player's middle toward the cursor, turned by `turn` degrees.</summary>
        static bool ThrowFrom(HeroController hero, ItemDef def, float turn, Flight f, float damage, float speed)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null || hero == null) return false;
            TurnToAim(hero);
            var from = Centre(hero);
            var dir = Aim() - from;
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(Facing(hero), 0f);
            bool thrown = tp.ThrowArt(def, from, Rot(dir.normalized, turn), f, damage, speed);
            if (thrown) TerraMod.Instance?.PlaySound(def.UseSound ?? "Item_1", 0.6f);
            return thrown;
        }

        static bool Toss(HeroController hero, ItemDef def, float turn, Flight f, float damage, float speed = 1f) => ThrowFrom(hero, def, turn, f, damage, speed);

        /// <summary>How far the weapon's remade throw goes (world).</summary>
        static float ReachOf(ItemDef def) => TerraProjectiles.Instance != null ? TerraProjectiles.Instance.ThrowReach(def) : 8f;

        /// <summary>The cursor, no farther from the player than `reach` × the weapon's remade throw.</summary>
        static Vector2 AimWithin(HeroController hero, ItemDef def, float reach)
        {
            var from = Centre(hero);
            var to = Aim() - from;
            float max = (TerraProjectiles.Instance != null ? TerraProjectiles.Instance.ThrowReach(def) : 8f) * reach;
            return to.magnitude > max ? from + to.normalized * max : from + to;
        }

        static Vector2 Rot(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
