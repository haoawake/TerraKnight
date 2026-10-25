using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Abyss Shriek's four weapons (<see cref="Abyss"/>), one for each class, in the pack's own pictures: the Abyss
        /// Yoyo (black ripples off its blows for six seconds, shadow rends for six more), the Void Sniper (single deliberate
        /// shots; the right button aims, the aimed shot marking, a marked enemy struck again calling a strike of dark bombs),
        /// the Abyss Shriek Staff (the three called up where the cursor is, three bursts, then homing black threads) and the
        /// Dirge Mask (a little dirge soul that fires a laser from its mouth at an enemy).
        /// </summary>
        static void AddAbyssWeapons(List<ItemDef> list)
        {
            list.Add(new ItemDef
            {
                Type = ItemType.AbyssYoyo, Name = "深渊悠悠球", Texture = Abyss.Tex("yoyo"), Class = DamageClass.Melee, Damage = 60, Knockback = 3.5f,
                UseAnimation = 25 * Tick, UseTime = 25 * Tick, NoMelee = true, NoUseGraphic = true, HoldOut = true, Channel = true, AutoReuse = false,
                Shoot = ProjKind.AbyssYoyo, ShootSpeed = 16f, UseSound = "Item_1", Tier = 4, Rare = 5,
                Tooltip = "悠悠球，射程 18 格，持续 12 秒\n前 6 秒命中时荡开黑色波纹，伤害周围的敌人\n后 6 秒每次命中都撕裂敌人一次",
            });

            var sniper = Held(Gun(ItemType.VoidSniper, "虚空狙击枪", 1254, 120, 8f, Abyss.SnipeUse, 16f, null, 0, 4, crit: 20, rare: 5,
                tip: "左键：单发狙击，子弹穿墙\n按住右键瞄准，移动变慢，跳跃或松开右键取消；瞄准时射得更快，但子弹穿不过墙\n瞄准射中的敌人被标记，再射中它会召来一轮黑暗炸弹"), -16f, 2f);
            sniper.Texture = Abyss.Tex("sniper");
            list.Add(sniper);

            var staff = Staff(ItemType.AbyssStaff, "深渊尖啸法杖", 165, 70, 4f, Abyss.CastUse, 0f, 24, ProjKind.AbyssTriad, "tk_abyss_cast", 0, 4, rare: 5,
                tip: "在光标处唤出深渊三魂，最远 22 格，挡在墙前\n三魂连续爆发三次，伤害周围约 4 格的敌人\n随后四散成追踪的黑丝");
            staff.Texture = Abyss.Tex("staff");
            staff.StaffHold = true;
            staff.HoldOffset = new Vector2(2f, 0f);
            list.Add(staff);

            var mask = Staff2(ItemType.DirgeMask, "冥歌面具", 1309, 50, 2f, 30, MinionKind.DirgeSoul, PlayerBuffs.DirgeSoul, "tk_dirge_summon", 0, 4, 5,
                "召唤一只小冥魂为你而战\n它停在敌人约 8 格外，从嘴里射出激光打它，不穿透");
            mask.Texture = Abyss.Tex("mask");
            list.Add(mask);
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> AbyssSources()
        {
            foreach (var t in Abyss.Weapons) yield return (t, 4, ItemSource.Drop, 60000);
        }
    }

    /// <summary>
    /// The Abyss Shriek's four weapons (the user's, 2026-10-20: "黑吼", the pictures and frames from 黑吼四职业武器素材包, as
    /// Resources/abyss_*.png, with a rend's slash and a thread drawn to go with them; their sounds mixed from Terraria's,
    /// SoundMix). Absorbing the Abyss Shriek (PlayerData.screamLevel 2) puts all four into the bag, once a save, those that
    /// don't fit at the feet. The yoyo rides the yoyos' own code (TerraProjectilesYoyo, its life, reach and string), its
    /// ripples and rends, the sniper's shots, marks and bombs, the staff's three: Items/TerraProjectilesAbyss.cs; the dirge
    /// soul: Items/MinionsAbyss.cs. Here: the sniper's aim (its slower step, Player.TerraMovement), its kick, the reward,
    /// the damage.
    /// </summary>
    internal static class Abyss
    {
        public static readonly ItemType[] Weapons = { ItemType.AbyssYoyo, ItemType.VoidSniper, ItemType.AbyssStaff, ItemType.DirgeMask };

        public static string Tex(string name) => Relics.TexturePrefix + "abyss_" + name;

        // ------------------------------------------------------------------ the yoyo (the user's numbers)

        /// <summary>Its reach (px, 18 tiles), its life (seconds) and where its two halves part; one blow on an enemy every 0.2 s.</summary>
        public const float YoyoRangePx = 288f, YoyoLife = 12f, YoyoPhase = 6f;
        public const int YoyoHitCooldown = 12;
        /// <summary>The first half's ripple: once in RippleCooldown ticks (0.5 s), RipplePx round, a share of the blow; the second half's rend, a share.</summary>
        public const int RippleCooldown = 30;
        public const float RipplePx = 56f, RippleShare = 0.5f, RendShare = 0.6f;
        /// <summary>A rend's blade: how far out beyond its enemy it comes from (px) and how fast (px a tick).</summary>
        public const float RendFromPx = 84f, RendSpeed = 13f;

        // ------------------------------------------------------------------ the sniper (the user's numbers)

        /// <summary>A shot's wait (ticks): 1.1 s, aimed 0.8 s; its reach (px); the aim's step (a share of the run).</summary>
        public const int SnipeUse = 66, AimedUse = 48;
        public const float SnipeReachPx = 1890f, AimSlow = 0.5f;
        /// <summary>The strike on a marked enemy struck again: its bombs, each a share of the shot, the burst's reach (px); a mark's life (ticks).</summary>
        public const int Bombs = 5, MarkTicks = 600;
        public const float BombShare = 0.24f, BombBlastPx = 44f;

        /// <summary>The rifle's grip and muzzle in its picture (px from its top-left, 导入配置.json; 76×28).</summary>
        public static readonly Vector2 Muzzle = new Vector2(73f, 13f);

        // ------------------------------------------------------------------ the staff (the user's numbers)

        /// <summary>
        /// A cast's use (ticks, 0.85 s) and reach (px, 22 tiles); the three showing (ticks, 0.2 s), then a burst every
        /// PulseEvery (0.16 s), Pulses of them, PulsePx round (4 tiles), each the cast's blow; then Threads black threads, each a share.
        /// </summary>
        public const int CastUse = 51, AppearTicks = 12, PulseEvery = 10, Pulses = 3, Threads = 8;
        public const float CastReachPx = 352f, PulsePx = 64f, PulseShare = 1f, ThreadShare = 0.35f;
        /// <summary>The little souls: flung out at ThreadFling px a tick, slowing, harmless for ThreadOut updates, then the charge.</summary>
        public const float ThreadFling = 9f, ThreadOut = 16f;

        // ------------------------------------------------------------------ the dirge soul

        /// <summary>How far off its enemy it shoots from (px, 8 tiles); its laser's ticks and a blow every LaserEvery.</summary>
        public const float StandoffPx = 128f;
        public const int LaserTicks = 30, LaserEvery = 6;

        /// <summary>
        /// Their base damage, set after the weapon balance and its boost (ProgressionBalance), for where they come: the Abyss
        /// Shriek, in the Abyss (stage 4). Measured on Dirtmouth's training dummy (suite abyssdps) beside stages 4 and 5.
        /// </summary>
        public static void Balance(ItemDef[] items)
        {
            items[(int)ItemType.AbyssYoyo].Damage = 46;
            items[(int)ItemType.VoidSniper].Damage = 140;
            items[(int)ItemType.AbyssStaff].Damage = 70;
            items[(int)ItemType.DirgeMask].Damage = 62;
        }

        // ------------------------------------------------------------------ the sniper's aim

        /// <summary>The right button held with the sniper in hand (not let go since a jump broke it off): its slower step and quicker shot.</summary>
        public static bool Aiming { get; private set; }
        static bool aimBroken, jumpWas;
        public static int DevAims, DevAimBreaks;

        /// <summary>What the aim leaves of the run (Player.TerraMovement's moveSpeed).</summary>
        public static float MoveSpeedMult => Aiming ? AimSlow : 1f;

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

        /// <summary>A point of the rifle's picture as the player sprite holds it out (as SoulWave.RiflePoint; the picture 28 px high).</summary>
        public static Vector2 RiflePoint(Vector2 centrePx, Vector2 dir, float facing, Vector2 picture)
        {
            var def = ItemDb.Get(ItemType.VoidSniper);
            float s = def.Scale;
            var up = facing > 0f ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);
            float back = def.HoldOffset.x - RecoilPx;
            return centrePx + new Vector2(-2f * facing, def.HoldOffset.y) + dir * ((back + picture.x) * s) + up * ((14f - picture.y) * s);
        }

        /// <summary>
        /// Each frame: the kick easing off; the aim (the user's: held on the right button, broken off by a jump until the
        /// button comes up again), its sight kept up while it lasts.
        /// </summary>
        public static void Tick(float dt)
        {
            float k = Mathf.Exp(-dt * 12f);
            RecoilPx *= k;
            RecoilDeg *= k;
            if (RecoilPx < 0.05f) RecoilPx = 0f;
            if (RecoilDeg < 0.05f) RecoilDeg = 0f;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || hero == null)
            {
                Aiming = false;
                return;
            }
            var held = mod.Inventory.Current;
            bool holding = !held.IsEmpty && held.Type == ItemType.VoidSniper && !InventoryGui.Open && !hero.cState.dead;
            bool alt = mod.AltHeld;
            bool jump = TerraMovement.JumpHeldNow;
            if (!alt || !holding) aimBroken = false;
            bool was = Aiming;
            if (Aiming && jump && !jumpWas)
            {
                aimBroken = true;
                DevAimBreaks++;
            }
            jumpWas = jump;
            Aiming = holding && alt && !aimBroken;
            if (Aiming && !was)
            {
                DevAims++;
                mod.PlaySound("tk_void_aim", 0.5f);
            }
            if (Aiming) TerraProjectiles.Instance?.KeepVoidSight(hero);
        }

        // ------------------------------------------------------------------ the reward

        const string RewardKey = "screamLevel2.abyss.v1";
        public static int DevGiven;

        /// <summary>The Abyss Shriek absorbed (screamLevel 2; a save that has it, at its next load): the four into the bag, once a save.</summary>
        public static void GrantReward(TerraMod mod, PlayerData pd)
        {
            if (mod == null || pd == null || pd.screamLevel < 2 || mod.GrantedAbilities.Contains(RewardKey)) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.transitioning) return;
            Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (!SpellRewards.Give(mod, Weapons, at)) return;
            mod.GrantedAbilities.Add(RewardKey);
            DevGiven++;
            mod.PlaySound("tk_abyss_pulse_2", 0.7f);
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < 28 && TerraParticles.Instance != null; k++)
            {
                float a = k / 28f * Mathf.PI * 2f;
                int i = TerraParticles.Instance.NewDust(px - new Vector2(4f, 4f), 8, 8, k % 4 == 0 ? 240 : 109, Mathf.Cos(a) * 3.4f, Mathf.Sin(a) * 3.4f, 0, 1.4f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
            Plugin.Log.LogInfo("[黑吼] 吸收深渊尖啸：四职业武器放进背包（深渊悠悠球、虚空狙击枪、深渊尖啸法杖、冥歌面具）");
        }
    }

    internal static partial class ShootPatterns
    {
        static readonly bool abyss = Add(ItemType.VoidSniper, VoidSniper) & Add(ItemType.AbyssStaff, AbyssStaff);

        /// <summary>The Void Sniper: an aimed shot (marks, stops at walls, the next shot sooner) while the aim holds, else a plain one through walls.</summary>
        static void VoidSniper(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            var mod = TerraMod.Instance;
            if (tp == null || mod == null) return;
            bool aimed = Abyss.Aiming;
            tp.ShootVoidSniper(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit, aimed);
            if (aimed) mod.SetUseCooldown(Abyss.AimedUse / 60f * Equipment.UseScale(s.Def));
        }

        static void AbyssStaff(in Shot s) => TerraProjectiles.Instance?.CastAbyssTriad(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);
    }
}
