using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Dashmaster (charm 31, 护符设计记录.md): the Dash key's dash (the Shield of Cthulhu's and every Terraria dash, the
    /// Mothwing and Shade Cloaks' too) goes toward the cursor, any way at all; a direction tapped twice dashes that way,
    /// up and down as well as to the sides (down only when the armour's own double tap has nothing to do). The dashes'
    /// looks turn with them, and a faint line of light marks the way each one sets out.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int Dashmaster = 31;
        static readonly Color DashColor = new Color(0.75f, 0.9f, 1f);
        public static int DevAimedDashes;

        /// <summary>Dashmaster worn: dashes go any way.</summary>
        public static bool DashAnyWay => On(Dashmaster);

        /// <summary>Dev: the cursor's point for a test's dash (null: the real mouse).</summary>
        public static Vector2? DevDashAim;

        /// <summary>The way from the player to the cursor (the way faced when it's on him).</summary>
        public static Vector2 MouseAim(HeroController hero)
        {
            var c = Centre(hero);
            var facing = hero != null && hero.cState.facingRight ? Vector2.right : Vector2.left;
            Vector2 to = DevDashAim ?? (TerraMod.MouseWorld(out var m) ? m : c + facing);
            var d = to - c;
            return d.sqrMagnitude < 0.04f ? facing : d.normalized;
        }

        /// <summary>The marks of dashes set out: from where (world), which way, when.</summary>
        static readonly List<(Vector2 From, Vector2 Dir, float Time, string Scene)> dashMarks = new List<(Vector2, Vector2, float, string)>();

        /// <summary>A dash set out a way that isn't level (Player.TerraMovement, the shadow dash): its mark.</summary>
        public static void OnAimedDash(HeroController hero, Vector2 dir)
        {
            if (!DashAnyWay || hero == null) return;
            DevAimedDashes++;
            dashMarks.Add((Centre(hero), dir, Time.time, SceneNow));
        }

        static void DrawDashWorld(FxBatch fx)
        {
            if (dashMarks.Count == 0) return;
            bool top = fx.OnTop;
            fx.OnTop = true;
            float t = Time.time;
            string scene = SceneNow;
            for (int i = dashMarks.Count - 1; i >= 0; i--)
            {
                var m = dashMarks[i];
                float k = (t - m.Time) / 0.3f;
                if (k >= 1f || m.Scene != scene) { dashMarks.RemoveAt(i); continue; }
                var px = TerraParticles.ToPx(m.From);
                var d = new Vector2(m.Dir.x, -m.Dir.y);
                float rot = Mathf.Atan2(d.y, d.x);
                float len = Mathf.Lerp(40f, 150f, Mathf.Sqrt(k));
                fx.Draw(Dot, px + d * (len * 0.5f), Glow(DashColor, 0.55f * (1f - k)), rot, new Vector2(len / 32f, 0.1f));
                fx.Draw(Dot, px + d * len, Glow(Color.white, 0.6f * (1f - k)), 0f, 0.3f);
            }
            fx.OnTop = top;
        }
    }
}
