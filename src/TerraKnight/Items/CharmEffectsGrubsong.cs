using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Grubsong (charm 3): three butterflies of pale light (Terraria's Prismatic Lacewing, NPC_661, in the charm's green) circle the player, bright while it is ready and dim while it
    /// cools. With the life not full (for half a second, so it doesn't land on the hit's own flash), they swoop in and a
    /// ring of light spreads from the player: a tenth of the life back. Ten seconds before it comes again.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float GrubCooldown = 10f, GrubShare = 0.1f, GrubWait = 0.5f, HaloTime = 0.7f;
        static readonly Color GrubColor = new Color(0.78f, 1f, 0.58f);
        static float notFullFor, haloAt = -10f;
        public static int DevGrubHeals, DevGrubHealed;

        /// <summary>Each butterfly's last few places, for its trail (Terraria pixels).</summary>
        static readonly Vector2[,] grubTrail = new Vector2[3, 4];
        static float grubTrailAt;
        static Vector2 grubLast;

        static void TickGrubsong(HeroController hero)
        {
            var st = Stats;
            if (!On(Grubsong) || st == null || !Playing(hero)) { notFullFor = 0f; return; }
            if (st.Life <= 0 || st.Life >= st.LifeMax) { notFullFor = 0f; return; }
            notFullFor += Time.deltaTime;
            if (notFullFor < GrubWait || CharmSkills.Cooling(Grubsong)) return;
            int healed = st.Heal(Mathf.CeilToInt(st.LifeMax * GrubShare), recovery: true);
            notFullFor = 0f;
            CharmSkills.Cool(Grubsong, GrubCooldown);
            haloAt = Time.time;
            if (healed > 0) UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, healed.ToString(), UI.CombatText.Heal);
            Mod.PlaySound("Item_4", 0.35f);
            DevGrubHeals++;
            DevGrubHealed += healed;
        }

        static void DrawGrubsong(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(Grubsong)) return;
            float t = Time.time;
            float halo = Mathf.Clamp01((t - haloAt) / HaloTime);
            bool haloOn = halo < 1f;
            float bright = (CharmSkills.Cooling(Grubsong) ? 0.45f : 1f) * opacity;
            if (haloOn) bright = Mathf.Max(bright, (1.6f - halo) * opacity);
            // Swooping in for the ring, then out again.
            float pull = haloOn ? Mathf.Sin(halo * Mathf.PI) * 0.55f : 0f;
            // A jump (a new room, a teleport): the trails start again where the butterflies are.
            bool jumped = (c - grubLast).sqrMagnitude > 160f * 160f;
            grubLast = c;
            bool sample = t - grubTrailAt > 0.06f;
            if (sample) grubTrailAt = t;
            for (int i = 0; i < 3; i++)
            {
                float a = t * 1.5f + i * (Mathf.PI * 2f / 3f);
                float r = 28f * (1f - pull);
                var at = c + new Vector2(Mathf.Cos(a) * r, -6f + Mathf.Sin(a) * 9f * (1f - pull) + Mathf.Sin(t * 3f + i * 2f) * 2f);
                bool front = Mathf.Sin(a) > 0f;
                if (jumped) for (int k = 0; k < 4; k++) grubTrail[i, k] = at;
                if (sample)
                {
                    for (int k = 3; k > 0; k--) grubTrail[i, k] = grubTrail[i, k - 1];
                    grubTrail[i, 0] = at;
                }
                fx.OnTop = front;
                float depth = front ? 1f : 0.65f;
                for (int k = 1; k < 4; k++)
                    fx.Draw(Dot, grubTrail[i, k], Glow(GrubColor, bright * depth * 0.3f * (1f - k / 4f)), 0f, 0.12f, overPlayer: front);
                // Its glow, then the lacewing itself (three wing beats a frame row, 24 px each), turned the way it flies.
                fx.Draw(Dot, at, Glow(GrubColor, bright * depth * 0.35f), 0f, 0.75f, overPlayer: front);
                int frame = (int)(t * 12f + i) % 3;
                bool left = -Mathf.Sin(a) < 0f;
                var wing = FxBatch.Frame("NPC_661", 0, frame * 24, 24, 24, 12f, 12f);
                float alpha = Mathf.Clamp01(0.85f * bright * depth);
                fx.Draw(wing, at, new Color(GrubColor.r * alpha, GrubColor.g * alpha, GrubColor.b * alpha, alpha), 0f, 0.62f, flipX: !left, overPlayer: front);
                fx.Draw(wing, at, Glow(GrubColor, bright * depth * 0.45f), 0f, 0.62f, flipX: !left, overPlayer: front);
            }
            if (haloOn)
            {
                fx.OnTop = true;
                float radius = Mathf.Lerp(8f, 46f, 1f - (1f - halo) * (1f - halo));
                float k = (1f - halo) * opacity;
                fx.Draw(Ring, c, Glow(GrubColor, 0.7f * k), 0f, radius * 2f / 64f, overPlayer: true);
                fx.Draw(Dot, c, Glow(GrubColor, 0.25f * k), 0f, radius * 2.4f / 32f, overPlayer: true);
            }
        }
    }
}
