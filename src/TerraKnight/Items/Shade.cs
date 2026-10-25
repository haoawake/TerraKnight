using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Shade Soul's four weapons (<see cref="Shade"/>), one for each class, in the pack's own pictures: the Shade Edge
        /// (a three-cut combo: a downward chop, a level sweep, then the sword thrown spinning), the Shade Rift Rifle (a black
        /// ray through every enemy, marking them; the right button bursts the marks), the Shade Wraith (a ghost sent to the
        /// cursor with dark shocks closing on it; the right button takes the player to it) and the Shade Soul Staff (a little
        /// shade that charges and turns into a shock wave on one enemy).
        /// </summary>
        static void AddShadeWeapons(List<ItemDef> list)
        {
            var edge = Thrown(ItemType.ShadeEdge, "暗魂之锋", 989, DamageClass.Melee, 30, 5f, 18, ProjKind.ShadeThrow, 1f, 0, 2, 3,
                "按住左键连斩三刀：下劈、横斩、上挑，上挑时掷出一道剑影\n剑影旋转着打中周围的所有敌人，穿过墙壁，打完就消散，不耽误挥剑\n松开左键回到第一刀");
            edge.Texture = Shade.Tex("edge");
            // Its cuts are its handler's (ShootPatterns.ShadeEdge); its Shoot the thrown sword, held while it is out (no cut then).
            edge.UseSound = null;
            edge.AutoReuse = true;
            list.Add(edge);

            var rifle = Held(Gun(ItemType.ShadeRiftRifle, "暗魂裂光枪", 514, 9, 1.5f, 16, 14f, null, 0, 2, rare: 3,
                tip: "左键：射出一道穿透一切敌人的黑色裂光，给命中的敌人叠一层印记，最多五层\n右键：引爆所有印记，层数越多爆炸越大"), -14f, 2f);
            rifle.Texture = Shade.Tex("rift_rifle");
            rifle.AltUse = true;
            list.Add(rifle);

            var focus = Staff(ItemType.ShadeSoulFocus, "暗魂幽影", 165, 14, 2f, 20, 12f, 10, ProjKind.ShadeGhost, "tk_shade_cast", 0, 2, rare: 3,
                tip: "按住左键放出幽灵，突袭到光标处，黑暗冲击波从四周汇聚过去，持续消耗魔力\n松开后幽灵慢慢飞回，回到手中才能再放\n幽灵在外时按右键，稍等片刻传送到幽灵身边，耗尽所有魔力，冷却 6 秒\n幽灵穿不过墙，冲击波可以");
            focus.Texture = Shade.Tex("focus");
            focus.HoldOffset = new Vector2(2f, 0f);
            list.Add(focus);

            var staff = Staff2(ItemType.ShadeSoulStaff, "暗魂唤杖", 1309, 36, 2f, 30, MinionKind.ShadeSoul, PlayerBuffs.ShadeSoul, "tk_shade_summon", 0, 2, 3,
                "召唤一只小暗魂为你而战\n它冲向敌人，近了化作一道冲击波重重打中它，然后飞开，过一会再来\n冲刺路上碰到的其他敌人也会受伤");
            staff.Texture = Shade.Tex("staff");
            list.Add(staff);
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> ShadeSources()
        {
            foreach (var t in Shade.Weapons) yield return (t, 2, ItemSource.Drop, 40000);
        }
    }

    /// <summary>
    /// The Shade Soul's four weapons (the user's, 2026-10-20: "黑波", the pictures and frames from 黑波四职业武器素材包, as
    /// Resources/shade_*.png, the sword's picture also turned flat along its blade, shade_edge_flat.png; their sounds
    /// mixed from Terraria's, SoundMix). Absorbing the Shade Soul (PlayerData.fireballLevel 2) puts all four into the bag,
    /// once a save, those that don't fit at the feet. Their shots: Items/TerraProjectilesShade.cs; the little shade:
    /// Items/MinionsShade.cs. Here: the sword's combo, the rifle as held, the staff's right click, the reward, the damage.
    /// The magic one is called 暗魂幽影, not the pack's 暗影之魂: that is already the Soul of Night's name.
    /// </summary>
    internal static class Shade
    {
        public static readonly ItemType[] Weapons = { ItemType.ShadeEdge, ItemType.ShadeRiftRifle, ItemType.ShadeSoulFocus, ItemType.ShadeSoulStaff };

        public static string Tex(string name) => Relics.TexturePrefix + "shade_" + name;

        /// <summary>The pack's black-void palette: void, ink, dusk, plum; steel, silver, frost, the eye's white; lilac.</summary>
        public static readonly Color Void = FxBatch.C(5, 5, 9), Ink = FxBatch.C(25, 23, 39), Dusk = FxBatch.C(38, 33, 59), Plum = FxBatch.C(55, 47, 79),
            Steel = FxBatch.C(149, 163, 194), Silver = FxBatch.C(184, 205, 232), Frost = FxBatch.C(221, 235, 249), Eye = FxBatch.C(255, 253, 235),
            Lilac = FxBatch.C(168, 135, 217), Violet = FxBatch.C(125, 88, 180);

        /// <summary>A colour drawn over what is under it (FxBatch: straight colour at an opacity), for the black parts.</summary>
        public static Color Dark(Color c, float a) => new Color(c.r * a, c.g * a, c.b * a, a);

        // ------------------------------------------------------------------ the sword

        /// <summary>The cut the next swing makes (0 the chop, 1 the sweep, 2 the throw); back to the chop when the button is let go.</summary>
        public static int Combo;

        /// <summary>The flat picture's grip and blade (px, Resources/shade_edge_flat.png: 61×27, the blade along +x).</summary>
        public static readonly Vector2 FlatGrip = new Vector2(12.5f, 14.3f);
        public const float BladePx = 47.5f;
        /// <summary>
        /// The sword drawn and swung this much over its picture (the user's: as big as the Revenant Edge, whose swing turns
        /// on its picture's corner, about 63 px to the tip; this one turns on its grip, 47.5 px).
        /// </summary>
        public const float SwordScale = 1.35f;

        /// <summary>
        /// The third cut's thrown sword (a shadow of it: the one in hand swings on): its reach (px), its time out, hanging
        /// there and fading (ticks); its spin's reach (px) and share of a blow; how far into the swing it leaves the hand.
        /// </summary>
        public const float ThrowReach = 110f, ThrowOut = 12f, ThrowHang = 14f, ThrowFade = 8f, SpinPx = 72f, SpinShare = 0.7f, ThrowAt = 0.45f;
        public const int SpinCooldown = 9;

        // ------------------------------------------------------------------ the rifle

        /// <summary>The ray's reach (px) and how long it shows (ticks); marks: at most, how long one lasts (ticks); a burst's reach and damage by stacks.</summary>
        public const float RayReachPx = 1890f;
        public const int RayTicks = 12, MaxMarks = 5, MarkTicks = 600;

        public static float BlastPx(int stacks) => 28f + 20f * Mathf.Clamp(stacks, 1, MaxMarks);

        public static float BlastShare(int stacks) => 0.6f + 0.55f * Mathf.Clamp(stacks, 1, MaxMarks);

        /// <summary>The rifle's grip and muzzle in its picture (px from its top-left, 导入配置.json).</summary>
        public static readonly Vector2 Muzzle = new Vector2(53f, 15f);

        public static float RecoilPx, RecoilDeg;

        public static void Kick(float px, float deg)
        {
            RecoilPx = Mathf.Max(RecoilPx, px);
            RecoilDeg = Mathf.Max(RecoilDeg, deg);
        }

        public static void Pose(ref Vector2 offset, ref float angle)
        {
            offset.x -= RecoilPx;
            angle += RecoilDeg;
        }

        /// <summary>A point of the rifle's picture as the player sprite holds it out (as SoulWave.RiflePoint: 32 px high).</summary>
        public static Vector2 RiflePoint(Vector2 centrePx, Vector2 dir, float facing, Vector2 picture)
        {
            var def = ItemDb.Get(ItemType.ShadeRiftRifle);
            float s = def.Scale;
            var up = facing > 0f ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);
            float back = def.HoldOffset.x - RecoilPx;
            return centrePx + new Vector2(-2f * facing, def.HoldOffset.y) + dir * ((back + picture.x) * s) + up * ((16f - picture.y) * s);
        }

        // ------------------------------------------------------------------ the staff

        /// <summary>The ghost: how fast it goes for the cursor and comes back (px a tick); a shock every ShockEvery ticks from ShockRingPx out, each a share; mana every ManaEvery ticks.</summary>
        public const float GhostSpeed = 15f, GhostBack = 9f, ShockRingPx = 130f, ShockShare = 0.5f;
        public const int ShockEvery = 5, ShockTicks = 12, ManaEvery = 20;
        /// <summary>The right click's blink to the ghost: its delay (ticks; the user's: a short one, with its sound), then all the mana spent; then a wait before the next (seconds, the user's).</summary>
        public const int BlinkTicks = 24;
        public const float BlinkCooldown = 6f;

        /// <summary>When the next blink may start (Time.time).</summary>
        public static float BlinkReady;

        // ------------------------------------------------------------------ the little shade

        /// <summary>Its charge's speed (px a tick); how near it turns into the wave (px) and the wave's speed; its rest between strikes (ticks).</summary>
        public const float ShadeDash = 13f, StrikeRangePx = 110f, WaveSpeed = 17f;
        /// <summary>What another enemy it meets on its way takes, of a blow (the user's: hurt in passing).</summary>
        public const float PathShare = 0.5f;
        public const int ShadeRest = 24;

        /// <summary>
        /// Their base damage, set after the weapon balance and its boost (ProgressionBalance), for where they come: the Shade
        /// Soul, in the Soul Sanctum (City of Tears, stage 2). Measured on Dirtmouth's training dummy (suite soulwavedps)
        /// beside stage 2's weapons (the Muramasa, the Handgun, the Aqua Scepter and Space Gun, the Imp Staff).
        /// </summary>
        public static void Balance(ItemDef[] items)
        {
            items[(int)ItemType.ShadeEdge].Damage = 36;
            items[(int)ItemType.ShadeRiftRifle].Damage = 31;
            items[(int)ItemType.ShadeSoulFocus].Damage = 22;
            items[(int)ItemType.ShadeSoulStaff].Damage = 56;
        }

        public static int DevBlinks, DevNoGhost;

        /// <summary>
        /// Each frame: the rifle's kick easing off; the sword's combo back to its first cut the moment the button is let go
        /// (or another item taken; a thrown sword flies on); the staff's right click with its ghost out, the blink to it
        /// started. Before SoulWave.Tick, which clears the tests' right click.
        /// </summary>
        public static void Tick(float dt)
        {
            float k = Mathf.Exp(-dt * 16f);
            RecoilPx *= k;
            RecoilDeg *= k;
            if (RecoilPx < 0.05f) RecoilPx = 0f;
            if (RecoilDeg < 0.05f) RecoilDeg = 0f;
            var mod = TerraMod.Instance;
            var tp = TerraProjectiles.Instance;
            if (mod == null) return;
            var held = mod.Inventory.Current;
            var type = held.IsEmpty ? ItemType.None : held.Type;
            if (!mod.UseHeld || type != ItemType.ShadeEdge) Combo = 0;
            if (tp != null && type == ItemType.ShadeSoulFocus && mod.AltPressed && !InventoryGui.Open)
            {
                if (tp.StartShadeBlink()) DevBlinks++;
                else DevNoGhost++;
            }
        }

        // ------------------------------------------------------------------ the reward

        const string RewardKey = "fireballLevel2.shade.v1";
        public static int DevGiven;

        /// <summary>The Shade Soul absorbed (fireballLevel 2; a save that has it, at its next load): the four into the bag, once a save.</summary>
        public static void GrantReward(TerraMod mod, PlayerData pd)
        {
            if (mod == null || pd == null || pd.fireballLevel < 2 || mod.GrantedAbilities.Contains(RewardKey)) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.transitioning) return;
            Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (!SpellRewards.Give(mod, Weapons, at)) return;
            mod.GrantedAbilities.Add(RewardKey);
            DevGiven++;
            mod.PlaySound("tk_shade_summon", 0.8f);
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < 28 && TerraParticles.Instance != null; k++)
            {
                float a = k / 28f * Mathf.PI * 2f;
                int i = TerraParticles.Instance.NewDust(px - new Vector2(4f, 4f), 8, 8, k % 4 == 0 ? 240 : 109, Mathf.Cos(a) * 3.2f, Mathf.Sin(a) * 3.2f, 0, 1.4f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
            Plugin.Log.LogInfo("[黑波] 吸收暗影之魂：四职业武器放进背包（暗魂之锋、暗魂裂光枪、暗魂幽影、暗魂唤杖）");
        }
    }

    internal static partial class ShootPatterns
    {
        static readonly bool shade = Add(ItemType.ShadeEdge, ShadeEdge) & Add(ItemType.ShadeRiftRifle, ShadeRifle) & Add(ItemType.ShadeSoulFocus, ShadeFocus);

        /// <summary>The Shade Edge: the combo's next cut (its first again after the button was let go, Shade.Tick).</summary>
        static void ShadeEdge(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null) return;
            int cut = Shade.Combo;
            Shade.Combo = (cut + 1) % 3;
            tp.ShadeCut(s.Hero, s.Mouse, cut, s.Damage, s.Knockback, s.Crit, s.UseTicks);
        }

        /// <summary>The Shade Rift Rifle: the ray on the left button; on the right, every mark bursts (no round spent).</summary>
        static void ShadeRifle(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            var mod = TerraMod.Instance;
            if (tp == null || mod == null) return;
            if (mod.AltFunction)
            {
                NoAmmo = true;
                tp.DetonateShadeMarks(s.Damage, s.Knockback, s.Crit);
                return;
            }
            tp.ShootShadeRay(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);
        }

        static void ShadeFocus(in Shot s) => TerraProjectiles.Instance?.SendShadeGhost(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);
    }
}
