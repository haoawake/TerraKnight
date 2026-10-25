using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Vengeful Spirit's four weapons (<see cref="SoulWave"/>), one for each class, in the pack's own pictures: the
        /// Revenant Edge (a sword whose swing throws a soul blade and whose blows call out wraiths), the Revenant Pulse Rifle
        /// (a laser on the left button, a charged soul wave held on the right), the Vengeful Spirit (the wave itself, cast)
        /// and the Wandering Soul Staff (a little soul that gathers itself and pounces).
        /// </summary>
        static void AddSoulWaveWeapons(List<ItemDef> list)
        {
            var edge = Shooter(Sword(ItemType.RevenantEdge, "怨魂之锋", 989, 24, 5f, 22, price: 0, tier: 1, rare: 2,
                tip: "命中敌人时，从它身上飞出一只怨灵\n怨灵追踪附近的敌人再打一次"), 22, ProjKind.SoulSlash, 1f);
            edge.Texture = SoulWave.Tex("revenant_edge");
            edge.UseSound = "tk_soul_swing_*";
            list.Add(edge);

            var rifle = Held(Gun(ItemType.RevenantPulseRifle, "怨魂脉冲枪", 514, 15, 1.5f, 14, 14f, null, 0, 1, rare: 2,
                tip: "左键：从枪口射出一道怨魂光束，击中第一个敌人\n按住右键蓄力 2 秒，自动射出一道大型白波，消耗 5 发子弹，命中时爆炸\n提前松开取消，不耗弹药"), -14f, 2f);
            rifle.Texture = SoulWave.Tex("pulse_rifle");
            rifle.AltUse = true;
            list.Add(rifle);

            var spell = Staff(ItemType.VengefulSpiritFocus, "复仇之魂", 165, 30, 4f, 26, 9f, 8, ProjKind.SpiritWave, "tk_spirit_cast", 0, 1, rare: 2,
                tip: "向前放出一道白波，碰到敌人时爆炸\n爆炸之前按右键，再消耗同样的魔力，白波散成 15 只追踪怨灵");
            spell.Texture = SoulWave.Tex("spirit_focus");
            spell.HoldOffset = new Vector2(2f, 0f);
            list.Add(spell);

            var staff = Staff2(ItemType.WanderingSoulStaff, "游魂唤杖", 1309, 12, 2f, 30, MinionKind.WanderingSoul, PlayerBuffs.WanderingSoul, "tk_soul_summon", 0, 1, 2,
                "召唤一只小怨魂为你而战\n它会蓄势，然后扑向你附近的敌人");
            staff.Texture = SoulWave.Tex("soul_staff");
            list.Add(staff);
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> SoulWaveSources()
        {
            foreach (var t in SoulWave.Weapons) yield return (t, 0, ItemSource.Drop, 20000);
        }
    }

    /// <summary>
    /// The Vengeful Spirit's four weapons (the user's, 2026-10-20: "白波四职业武器", their pictures and the soul wave's and the
    /// little soul's frames from 白波四职业武器素材包, as Resources/soulwave_*.png; their own Terraria-style sounds, Resources/tk_*.wav,
    /// made by scratchpad sfx/gen_sfx.py). Absorbing the Vengeful Spirit (PlayerData.fireballLevel 1) puts all four into the bag,
    /// once a save, those that don't fit at the player's feet. Their shots: Items/TerraProjectilesSoulWave.cs; the little soul:
    /// Items/MinionsSoulWave.cs. Here: the rifle's muzzle and cells as the player sprite holds it, its kick, the reward.
    /// </summary>
    internal static class SoulWave
    {
        public static readonly ItemType[] Weapons = { ItemType.RevenantEdge, ItemType.RevenantPulseRifle, ItemType.VengefulSpiritFocus, ItemType.WanderingSoulStaff };

        public static string Tex(string name) => Relics.TexturePrefix + "soulwave_" + name;

        /// <summary>The charged wave's cost in rounds (the user left it open; more than a laser's one) and its charge (2 seconds).</summary>
        public const int WaveAmmo = 5, ChargeTicks = 120;

        /// <summary>A wraith's share of the blow it came out of.</summary>
        public const float WraithShare = 0.4f;

        /// <summary>The charged wave's damage, in the first of its rounds' (the rifle's with that round's).</summary>
        public const int WaveDamage = 8;

        /// <summary>The bursts' reach (px from the centre) and their share of their wave's damage to the others in it; the spell's split.</summary>
        public const float WaveBlastPx = 56f, WaveBlast = 0.6f, SpellBlastPx = 44f, SpellBlast = 0.7f, SplitShare = 0.2f;
        public const int SplitCount = 15;

        /// <summary>The pack's palette: bone white, near white, pale blue, cyan, blue grey.</summary>
        public static readonly Color Bone = FxBatch.C(255, 252, 232), White = FxBatch.C(243, 247, 250), Pale = FxBatch.C(218, 233, 247),
            Cyan = FxBatch.C(143, 228, 249), Grey = FxBatch.C(145, 177, 210), Slate = FxBatch.C(105, 137, 175);

        /// <summary>A colour drawn added (FxBatch: alpha 0 adds light), at a strength.</summary>
        public static Color Glow(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 0f);

        /// <summary>
        /// Their base damage, set after the weapon balance and its boost (ProgressionBalance), for when they come: just after
        /// the False Knight. Measured on Dirtmouth's training dummy (suite soulwavedps, nothing worn), beside his four rewards
        /// and stage 1's first weapons: the sword about as the Blade of Grass, the rifle's lasers about as the Gold Bow, the
        /// spell between the Ruby Staff and the Vilethorn, the staff's soul a little over the Slime Staff.
        /// </summary>
        public static void Balance(ItemDef[] items)
        {
            items[(int)ItemType.RevenantEdge].Damage = 11;
            items[(int)ItemType.RevenantPulseRifle].Damage = 5;
            items[(int)ItemType.VengefulSpiritFocus].Damage = 20;
            items[(int)ItemType.WanderingSoulStaff].Damage = 12;
        }

        // ------------------------------------------------------------------ the rifle in the hand

        /// <summary>The rifle's kick (px back along the aim, degrees up), easing off; its shiver at a full charge.</summary>
        public static float RecoilPx, RecoilDeg, Shiver;

        public static void Kick(float px, float deg)
        {
            RecoilPx = Mathf.Max(RecoilPx, px);
            RecoilDeg = Mathf.Max(RecoilDeg, deg);
        }

        /// <summary>Each frame: the kick and the shiver ease off (most of it in a tenth of a second).</summary>
        public static void Tick(float dt)
        {
            float k = Mathf.Exp(-dt * 16f);
            RecoilPx *= k;
            RecoilDeg *= k;
            if (RecoilPx < 0.05f) RecoilPx = 0f;
            if (RecoilDeg < 0.05f) RecoilDeg = 0f;
            Shiver = Mathf.Max(0f, Shiver - dt * 6f);
            var mod = TerraMod.Instance;
            // The split: right click with a wave out, the cast's mana paid again (none split without it).
            var tp = TerraProjectiles.Instance;
            if (mod != null && tp != null && mod.Inventory.Current.Type == ItemType.VengefulSpiritFocus && mod.AltPressed && !InventoryGui.Open
                && tp.DevCount(ProjKind.SpiritWave) > 0)
            {
                if (mod.PayMana(ItemDb.Get(ItemType.VengefulSpiritFocus))) tp.SplitSpiritWaves();
                else DevSplitNoMana++;
            }
            if (mod != null) mod.DevAltPress = false;
        }

        /// <summary>What the held rifle's pose takes off this frame (TerraMod's hold: the kick back, the muzzle up, the shiver).</summary>
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

        /// <summary>The rifle's grip and muzzle in its picture (px from its top-left, 白波四职业武器素材包/导入配置.json); its three cells.</summary>
        public static readonly Vector2 Muzzle = new Vector2(53f, 15f);
        public static readonly Vector2[] Cells = { new Vector2(39f, 16f), new Vector2(43f, 16f), new Vector2(47f, 16f) };

        /// <summary>
        /// A point of the rifle's picture (px from its top-left) as the player sprite holds it out (TerraPlayerSprite.PoseItem's
        /// useStyle 5: its back edge HoldOffset.x px out from 2 px behind the centre, the picture centred on the aim), from the
        /// player's centre along dir (Terraria px, y down).
        /// </summary>
        public static Vector2 RiflePoint(Vector2 centrePx, Vector2 dir, float facing, Vector2 picture)
        {
            var def = ItemDb.Get(ItemType.RevenantPulseRifle);
            float s = def.Scale;
            var up = facing > 0f ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);
            float back = def.HoldOffset.x - RecoilPx;
            return centrePx + new Vector2(-2f * facing, def.HoldOffset.y) + dir * ((back + picture.x) * s) + up * ((16f - picture.y) * s);
        }

        // ------------------------------------------------------------------ the reward

        const string RewardKey = "fireballLevel.soulwave.v1";
        public static int DevGiven, DevSplitNoMana;

        /// <summary>
        /// The Vengeful Spirit absorbed (fireballLevel 1 or more; a save that has it already, at its next load): the four weapons
        /// into the bag, those that don't fit at the feet, once a save (TerraMod.GrantedAbilities), with a burst of souls.
        /// </summary>
        public static void GrantReward(TerraMod mod, PlayerData pd)
        {
            if (mod == null || pd == null || pd.fireballLevel < 1 || mod.GrantedAbilities.Contains(RewardKey)) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.transitioning) return;
            Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (!SpellRewards.Give(mod, Weapons, at)) return;
            mod.GrantedAbilities.Add(RewardKey);
            DevGiven++;
            mod.PlaySound("tk_soul_summon", 0.8f);
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < 24 && TerraParticles.Instance != null; k++)
            {
                float a = k / 24f * Mathf.PI * 2f;
                int i = TerraParticles.Instance.NewDust(px - new Vector2(4f, 4f), 8, 8, 180, Mathf.Cos(a) * 3f, Mathf.Sin(a) * 3f, 0, 1.4f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
            Plugin.Log.LogInfo("[白波] 吸收复仇之魂：四职业武器放进背包（怨魂之锋、怨魂脉冲枪、复仇之魂、游魂唤杖）");
        }
    }

    internal static partial class ShootPatterns
    {
        /// <summary>Set by a handler whose use shoots nothing that costs a round (the rifle's charge starting): TerraMod.Fire leaves the ammo.</summary>
        internal static bool NoAmmo;

        static readonly bool soulWave = Add(ItemType.RevenantEdge, RevenantEdge) & Add(ItemType.RevenantPulseRifle, PulseRifle) & Add(ItemType.VengefulSpiritFocus, SpiritFocus);

        /// <summary>The Revenant Edge: its swing's crescent (drawn only; its wraiths come from its blows, MeleeSpecials.OnHit).</summary>
        static void RevenantEdge(in Shot s) => TerraProjectiles.Instance?.SoulSwing(s.From, s.Mouse, s.Vel, s.Damage, s.Knockback, s.Crit, s.UseTicks);

        /// <summary>The Revenant Pulse Rifle: the laser on the left button (none while a charge is held); the charge on the right, costing nothing as it starts.</summary>
        static void PulseRifle(in Shot s)
        {
            var tp = TerraProjectiles.Instance;
            var mod = TerraMod.Instance;
            if (tp == null || mod == null) return;
            if (mod.AltFunction || tp.PulseCharging)
            {
                NoAmmo = true;
                if (mod.AltFunction) tp.StartPulseCharge(s.Hero, s.Mouse, s.Damage, s.Knockback, s.Crit);
                return;
            }
            tp.ShootPulseLaser(s.Hero, s.Mouse, s.Vel, s.Damage, s.Knockback, s.Crit);
        }

        /// <summary>The Vengeful Spirit: the wave from just ahead of the caster.</summary>
        static void SpiritFocus(in Shot s) => TerraProjectiles.Instance?.CastSpiritWave(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);
    }
}
