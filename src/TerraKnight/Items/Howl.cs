using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Howling Wraiths' four weapons (<see cref="Howl"/>), one for each class, in the pack's own pictures: the Wailing
        /// Deathbell (a bell thrown like a boomerang, bouncing three times with a shock at each, then back to the hand), the
        /// Ghost Pipe Organ (three roars a use, of 1, 2 and 3 rounds, the last a piercing blast after a short charge), the
        /// Howling Wraiths (the wailing three called up toward the cursor, loosing homing soul beams all round) and the
        /// Elegy Mask (a little choir soul that keeps up a 30° cone of sound on an enemy near).
        /// </summary>
        static void AddHowlWeapons(List<ItemDef> list)
        {
            var bell = Thrown(ItemType.WailingDeathbell, "哀嚎丧钟", 55, DamageClass.Melee, 20, 6f, 24, ProjKind.DeathBell, Howl.BellSpeed, 0, 1, 2,
                "按住左键朝光标扔出丧钟，弹跳三次后飞回手中，松开左键立刻飞回\n每次撞到敌人或地形都震出一圈魂啸\n弹跳途中钟回到身上就能马上再扔，最多同时扔出两口");
            bell.Texture = Howl.Tex("deathbell");
            bell.UseSound = "tk_bell_throw";
            bell.AutoReuse = true;
            list.Add(bell);

            var organ = Held(Gun(ItemType.GhostPipeOrgan, "幽魂风琴", 514, 12, 2.5f, Howl.OrganUse, 9f, null, 0, 1, rare: 2,
                tip: "朝前方吼叫三次，分别消耗 1、2、3 发子弹\n白色声浪一直向前，撞墙反弹，前两声碰到敌人就散\n短暂蓄力后的第三声穿透一切"), -13f, 2f);
            organ.Texture = Howl.Tex("pipe_organ");
            // Its use is its burst (TerraProjectiles.StartOrganBurst): held while it lasts, so a use waits for the last.
            organ.Shoot = ProjKind.OrganBurst;
            list.Add(organ);

            var focus = Staff(ItemType.HowlingWraithsFocus, "嚎叫幽灵", 165, 16, 3f, 50, 0f, 12, ProjKind.WraithTriad, "tk_wraiths_appear", 0, 1, rare: 2,
                tip: "在光标处唤出嚎叫的三魂\n它朝四面八方射出白色魂束，追踪敌人，穿过墙壁");
            focus.Texture = Howl.Tex("wraiths_focus");
            focus.HoldOffset = new Vector2(2f, 0f);
            list.Add(focus);

            var mask = Staff2(ItemType.ElegyMask, "挽歌面具", 1309, 9, 1f, 30, MinionKind.ChoirSoul, PlayerBuffs.ChoirSoul, "tk_choir_summon", 0, 1, 2,
                "召唤一只小唱魂为你而战\n它向附近的敌人持续发出一道穿透的声浪");
            mask.Texture = Howl.Tex("elegy_mask");
            list.Add(mask);
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> HowlSources()
        {
            foreach (var t in Howl.Weapons) yield return (t, 1, ItemSource.Drop, 30000);
        }
    }

    /// <summary>
    /// The Howling Wraiths' four weapons (the user's, 2026-10-20: "白吼", the pictures and frames from 白吼四职业武器素材包, as
    /// Resources/howl_*.png, the ripple and the beam drawn to go with them; their sounds mixed from Terraria's, SoundMix).
    /// Absorbing the Howling Wraiths (PlayerData.screamLevel 1) puts all four into the bag, once a save, those that don't
    /// fit at the feet. The bell, the roars, the wraiths: Items/TerraProjectilesHowl.cs; the choir soul: Items/MinionsHowl.cs.
    /// Here: the organ's pipes as the player holds it, its kick, the reward, the damage.
    /// </summary>
    internal static class Howl
    {
        public static readonly ItemType[] Weapons = { ItemType.WailingDeathbell, ItemType.GhostPipeOrgan, ItemType.HowlingWraithsFocus, ItemType.ElegyMask };

        public static string Tex(string name) => Relics.TexturePrefix + "howl_" + name;

        /// <summary>The bell's throw (px a tick) and its bounces before it comes back; its shock's reach (px) and share of the bell's blow.</summary>
        public const float BellSpeed = 13f, ShockPx = 52f, ShockShare = 0.6f;
        public const int BellBounces = 3;
        /// <summary>The bell's size in flight against its picture (40×48): a small hand bell (the user's), about 22×26 px.</summary>
        public const float BellScale = 0.55f;

        /// <summary>The organ: a use's length (ticks); its roars' times, their rounds and their damage against a round's.</summary>
        public const int OrganUse = 60, SecondRoar = 12, ThirdRoar = 34;
        public static readonly int[] RoarRounds = { 1, 2, 3 };
        public static readonly float[] RoarDamage = { 1f, 1.35f, 2.6f };

        /// <summary>How long a roar's piece may fly (ticks): long enough to cross any room; it goes when it leaves the room or after four walls.</summary>
        public const int WaveLife = 600;
        /// <summary>The farthest a roar's piece flies, bounces and all (px; 80 tiles, the user's: a limit it fades at), fading over its last 200.</summary>
        public const float WaveReachPx = 1280f;

        /// <summary>The wraiths: their beams and each beam's share of the cast (called at the cursor, however far).</summary>
        public const int WraithBeams = 8;
        public const float BeamShare = 0.45f;

        /// <summary>The choir soul's shout: its reach (px), its half angle (degrees), how often it strikes each enemy (ticks).</summary>
        public const float ShoutReachPx = 300f, ShoutHalfAngle = 15f;
        public const int ShoutCooldown = 10;

        /// <summary>
        /// Their base damage, set after the weapon balance and its boost (ProgressionBalance), for where they come: the
        /// Howling Wraiths' mound, in the Fungal Wastes (stage 1). Measured on Dirtmouth's training dummy (suite howldps).
        /// </summary>
        public static void Balance(ItemDef[] items)
        {
            items[(int)ItemType.WailingDeathbell].Damage = 18;
            items[(int)ItemType.GhostPipeOrgan].Damage = 4;
            items[(int)ItemType.HowlingWraithsFocus].Damage = 16;
            items[(int)ItemType.ElegyMask].Damage = 7;
        }

        // ------------------------------------------------------------------ the organ in the hand

        public static float RecoilPx, RecoilDeg, Shiver;

        public static void Kick(float px, float deg)
        {
            RecoilPx = Mathf.Max(RecoilPx, px);
            RecoilDeg = Mathf.Max(RecoilDeg, deg);
        }

        public static void Tick(float dt)
        {
            float k = Mathf.Exp(-dt * 14f);
            RecoilPx *= k;
            RecoilDeg *= k;
            if (RecoilPx < 0.05f) RecoilPx = 0f;
            if (RecoilDeg < 0.05f) RecoilDeg = 0f;
            Shiver = Mathf.Max(0f, Shiver - dt * 6f);
        }

        /// <summary>What the held organ's pose takes off this frame (TerraMod's hold: the kick back, the pipes up, the shiver).</summary>
        public static void Pose(ref Vector2 offset, ref float angle)
        {
            offset.x -= RecoilPx;
            angle += RecoilDeg;
            if (Shiver > 0f)
            {
                offset += Random.insideUnitCircle * Shiver;
                angle += Random.Range(-1f, 1f) * Shiver;
            }
        }

        /// <summary>The organ's three pipe mouths in its picture (px from its top-left, 导入配置.json), high to low.</summary>
        public static readonly Vector2[] Pipes = { new Vector2(52f, 13f), new Vector2(50f, 19f), new Vector2(47f, 25f) };

        /// <summary>A point of the organ's picture as the player sprite holds it out (as SoulWave.RiflePoint, the picture 40 px high).</summary>
        public static Vector2 OrganPoint(Vector2 centrePx, Vector2 dir, float facing, Vector2 picture)
        {
            var def = ItemDb.Get(ItemType.GhostPipeOrgan);
            float s = def.Scale;
            var up = facing > 0f ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);
            float back = def.HoldOffset.x - RecoilPx;
            return centrePx + new Vector2(-2f * facing, def.HoldOffset.y) + dir * ((back + picture.x) * s) + up * ((20f - picture.y) * s);
        }

        // ------------------------------------------------------------------ the reward

        const string RewardKey = "screamLevel.howl.v1";
        public static int DevGiven;

        /// <summary>The Howling Wraiths absorbed (screamLevel 1 or more; a save that has it, at its next load): the four into the bag, once a save.</summary>
        public static void GrantReward(TerraMod mod, PlayerData pd)
        {
            if (mod == null || pd == null || pd.screamLevel < 1 || mod.GrantedAbilities.Contains(RewardKey)) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.transitioning) return;
            Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (!SpellRewards.Give(mod, Weapons, at)) return;
            mod.GrantedAbilities.Add(RewardKey);
            DevGiven++;
            mod.PlaySound("tk_wraiths_shriek", 0.7f);
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < 28 && TerraParticles.Instance != null; k++)
            {
                float a = k / 28f * Mathf.PI * 2f;
                int i = TerraParticles.Instance.NewDust(px - new Vector2(4f, 4f), 8, 8, 180, Mathf.Cos(a) * 3.4f, Mathf.Sin(a) * 3.4f, 0, 1.4f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
            Plugin.Log.LogInfo("[白吼] 吸收嚎叫幽灵：四职业武器放进背包（哀嚎丧钟、幽魂风琴、嚎叫幽灵、挽歌面具）");
        }
    }

    internal static partial class ShootPatterns
    {
        static readonly bool howl = Add(ItemType.WailingDeathbell, Deathbell) & Add(ItemType.GhostPipeOrgan, PipeOrgan) & Add(ItemType.HowlingWraithsFocus, WraithsFocus);

        static void Deathbell(in Shot s) => TerraProjectiles.Instance?.ThrowDeathbell(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);

        /// <summary>The organ: its first roar now (its round already paid by the use), the other two from its burst (TerraProjectiles.StepOrganBurst).</summary>
        static void PipeOrgan(in Shot s) => TerraProjectiles.Instance?.StartOrganBurst(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);

        static void WraithsFocus(in Shot s) => TerraProjectiles.Instance?.CallWraiths(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);
    }
}
