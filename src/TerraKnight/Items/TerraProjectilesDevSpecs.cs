using System.Text;

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        /// <summary>Dev: the projectile whose update is running (what a hit landing now comes from); None between them.</summary>
        internal static ProjKind DevCurrentKind;
        internal static int DevCurrentId;
        static Proj devCurrent;
        /// <summary>Dev: that projectile's maxPenetrate now (its AI may have changed it).</summary>
        internal static int DevCurrentMaxPen => devCurrent != null ? devCurrent.MaxPenetrate : 0;

        /// <summary>Dev: how a kind's hits are bounded: single, local N, once, kind N, shared N, none.</summary>
        internal static string DevImmunityOf(ProjKind kind, int maxPenetrate = int.MinValue)
        {
            if (!specs.TryGetValue(kind, out var s)) return "?";
            if (s.OncePerNpc) return "once";
            if (s.LocalCooldown > 0) return "local " + s.LocalCooldown;
            if (s.TypeCooldown > 0) return "kind " + s.TypeCooldown;
            if ((maxPenetrate != int.MinValue ? maxPenetrate : s.Penetrate) == 1 && s.OwnerImmune == 0) return "single";
            if (s.NoImmunity) return "none";
            return "shared " + (s.OwnerImmune > 0 ? s.OwnerImmune : 10);
        }

        /// <summary>Dev: every kind's texture and what bounds its hits (penetrate, local / per-kind cooldowns, owner immunity), one line each.</summary>
        internal static string DevSpecTable()
        {
            var sb = new StringBuilder();
            foreach (var kv in specs)
            {
                var s = kv.Value;
                sb.Append($"{kv.Key}|{s.Texture}|{s.Penetrate}|{s.LocalCooldown}|{(s.OncePerNpc ? 1 : 0)}|{s.TypeCooldown}|{s.OwnerImmune}|{(s.NoImmunity ? 1 : 0)}|{s.Extra}|{(s.DieOnHit ? 1 : 0)}\n");
            }
            return sb.ToString();
        }
    }
}
